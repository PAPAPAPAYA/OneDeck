# UI 窗口比例自适应统一方案（UI Aspect Adaptation）

日期: 2026-09-09
状态: 方案已拍板, 待「修改代码」开工
参考实现: `ResultStatsPanel` B′ 修复 (2026-09-09, 独立根 Canvas + 镜像 renderMode/camera + match height)
关联: docs/RenderLayering.md, docs/UIUX_Guidelines.md, docs/RegressionChecklist.md 行 84-86

## 0. 拍板记录 (2026-09-09, 用户)

| # | 决定 | 选择 |
|---|------|------|
| 1 | 目标比例 | 三档验收: 竖 9:16 / 方 1:1 / 横 16:9, 且战斗/商店/结算中拖拽 resize 不破版 |
| 2 | 世界内容 | **UI + 相机补偿** (窄窗拉远相机保证牌列不被裁; 构图基准改变, 商店/战斗布局需重验) |
| 3 | 缩放轴 | 全部统一 1080x1920 + match height = 1 (含 Global Canvas 从 ConstantPixelSize 迁移) |
| 4 | 遗留件 | 死件从场景删 (删除前逐个证据清单确认), 活件纳入适配 |

## 1. UI 全量清单 (2026-09-09 编辑器探针实测)

### 1.1 场景根 Canvas (父节点 `-----UX-----`, 全部 ScreenSpaceCamera + Main Camera)

| Canvas | sortingOrder | Scaler 现状 | 内容 | 比例行为 |
|--------|-------------|-------------|------|----------|
| Global | -1 | ConstantPixelSize | HPBarRoot(HP对比条) + 全屏背景 RawImage | 背景自适应; 条不随窗口缩放; HPBarRoot 编辑态实测 viewportX 到 1.85 (超右屏 85%), 需战斗态复核 |
| Combat | 1 | SSS 1080x1920 match=0 | 状态/牌堆文字 x4, Tips/Revealed/EffectResult 中央堆, HPNumericDisplay x2 + Horizontal x2, Player/Enemy Icon, DamageFloaterPresenter + FloaterLayer | 角锚件随角走; 中央堆 y=300/450/600 绝对像素, matchH 后恢复设计纵向占比 (16%-42%) |
| Shop | 1 | SSS 1080x1920 match=0 | Player Stats Display (活), Deck/Shop/General Info Display (死, 见 1.3), Reroll/exit 按钮 | Reroll 按钮右缘锚偏移 -420, 横窗下离右缘 12% 视宽, P2 重调 |
| Result | 1 | SSS 1080x1920 match=0 | 仅遗留 resultInfoDisplay (活件, PhaseManager 持续写入) | 1000x1840, matchW 方窗下 viewportY=[-0.35,1.35] 上下溢出; matchH 后 1920 纵向空间自动容纳 |

### 1.2 运行时自建根 Canvas

| 组件 | sortingOrder | 现状 | 结论 |
|------|-------------|------|------|
| ResultStatsPanel | 200 | SSS 1080x1920 + match height + 镜像 camera | 基准实现, 不动 |
| CardTagTooltip | 300 | SSS 1920x1080 (横版 ref, 与全项目竖版相反) + 默认 match 宽 | ref 改 1080x1920 + match height; 位置每帧 Screen.width/height 钳制, resize 自适应已具备 |
| UsernameRegistrationPanel (Net) | Overlay | SSS 1080x1920 match=0 + ScreenSpaceOverlay | match 0->1; Overlay -> ScreenSpaceCamera + 镜像 (对齐 tooltip 先例, 被像素化后处理覆盖) |

### 1.3 疑似死件 (grep Assets/Scripts 无代码引用)

| 对象 | 现象 | 删除前置检查 |
|------|------|--------------|
| Shop Canvas / Deck Display | 无引用 | grep GameScene.unity GUID 引用 + m_MethodName (UnityEvent 绑定 grep .cs 看不到) |
| Shop Canvas / Shop Display | 中心锚 +1075, matchW 方窗上半截出屏 | 同上 |
| Shop Canvas / General Info Display | 底部锚 +2475, 任何比例全程出屏 | 同上 |

活件 (保留并适配): resultInfoDisplay (PhaseManager.cs:82,291,453), playerStatsDisplay (ShopManager.cs:203,449,581)。

### 1.4 非 Canvas 耦合层 (本次范围: 相机补偿)

- 相机: Main Camera z=-100, fov=7 -> 世界可视高度恒定 12.24 单位, 宽 = 高 x aspect。
- 商店格子/战斗牌堆: 世界空间网格, 商店滚动下限 ComputeDynamicMinY 已按 viewHalfHeight 推导 (相机距离变化自动适应)。
- DamageFloater: 生成时 World->Screen 一次转换, 播放中不追踪 resize (瞬时件, 接受)。
- 像素化后处理: 全屏, 这是所有 canvas 必须镜像 renderMode/camera 的原因。

## 2. 统一约定 (写入 docs/UIUX_Guidelines.md 新节)

1. 每个 UI 簇 = 独立根 Canvas; 禁止嵌套 canvas (嵌套丢失自身 CanvasScaler, 2026-09-09 已踩)。
2. ScreenSpaceCamera + worldCamera/planeDistance 镜像游戏 canvas (保像素化/排序路径)。
3. ScaleWithScreenSize, 参考 1080x1920, match height = 1: 1 单位 = 屏高 1/1920, 纵向构图在任意比例恒定; 横向单位数 = 1920 x aspect。
4. 布局: 纵向 fraction 锚点 + 布局组 + auto-sizing 文本; 横向角/边锚点 + 像素偏移。
5. 做屏幕数学的组件每帧用 Screen.width/height 重算 (tooltip 先例)。

## 3. 已知代价 (拍板时已知, 验收时勿误判为 bug)

- **方窗 UI 整体线性缩小到 56.25%**: match height 下 scale = 1080/1920 = 0.5625 (现状 match 宽 scale = 1)。竖窗 9:16 尺寸不变; 横窗同比例缩小但纵向构图完整。ResultStatsPanel 当前已如此显示。
- **窄窗世界内容缩小**: 相机补偿后 9:16 下相机距离 100 -> 177.8, 卡牌屏幕尺寸约 0.56 倍, 商店行距/战斗 HUD 与世界卡的相对位置需重调 (P3)。

## 4. 分阶段实施 (每阶段停下汇报, 等确认再继续)

### P1 缩放轴统一 (纯设置改动, 零布局代码)
1. GameScene: Combat / Shop / Result Canvas 的 CanvasScaler.matchWidthOrHeight 0 -> 1 (SaveScene)。
2. Global Canvas: ConstantPixelSize -> ScaleWithScreenSize 1080x1920 + match height。
3. CardTagTooltip: referenceResolution (1920,1080) -> (1080,1920), matchWidthOrHeight = 1。
4. UsernameRegistrationPanel: matchWidthOrHeight 0 -> 1; renderMode Overlay -> ScreenSpaceCamera + worldCamera/planeDistance 镜像 (照抄 ResultStatsPanel.Build)。
5. 方窗/竖窗冒烟: 各 phase 打开看整体缩放是否符合第 3 节预期。

### P2 死件清理 + 活件重锚
1. 死件删除证据清单 (scene GUID / UnityEvent / 代码三路 grep) -> 用户确认 -> 从 GameScene 删 (死件不删 prefab)。
2. resultInfoDisplay: 确认 matchH 后 1920 空间内的显示范围, 必要时 body 改 fraction 锚。
3. Reroll/exit 按钮偏移 -420/-175 重调 (横窗 12% 视宽问题)。
4. HPBarRoot 几何复核: 编辑态实测超右屏 85%, 先战斗态 runtime 探针确认 presenter 是否运行时重设, 再决定场景值是否修正。
5. Combat 中央堆 (Tips/Revealed/EffectResult) 与四角 HUD 在 1920 空间内重验重叠与占位。

### P3 相机补偿 (新组件)
1. 新组件 `CameraAspectCompensator` (UXPrototype): 挂相机 rig, 只动 z。
   公式: aspect < 1.0 时 dist = 100 / aspect (保持世界可视宽度 >= 方窗基准 12.24); aspect >= 1.0 时 dist = 100; clamp dist 到 [100, 180]。
2. Update 轮询 aspect 变化 (窗口拖拽), 相位切换时重算; 与商店 HandleCameraScroll (只动 Y) 正交, ComputeDynamicMinY 自动适应新 viewHalfHeight。
3. 9:16 下商店滚动边界 / 战斗牌堆构图 / HUD 与世界卡相对位置逐项重验调参。

### P4 回归与固化
1. docs/RegressionChecklist.md 新增行: 方窗 UI 缩放 (P1), 死件删除 (P2), 窄窗相机补偿 (P3), tooltip 字号跨比例稳定 (P1)。
2. docs/UIUX_Guidelines.md 追加第 2 节约定; AGENTS.md 一句话指针 (注意 32KB 限额)。
3. 验收矩阵 (每格: 无破版 + 无遮挡 + 文本可读):

| 阶段 x 窗口 | 1080x1920 竖 | 1080x1080 方 | 1920x1080 横 |
|-------------|--------------|--------------|--------------|
| Shop (含滚轮到最深行) | ✓ | ✓ | ✓ |
| Combat (含战斗中拖拽 resize) | ✓ | ✓ | ✓ |
| Result (统计面板 + resultInfoDisplay) | ✓ | ✓ | ✓ |
| 悬停 tooltip | ✓ | ✓ | ✓ |
| 网络注册面板 | ✓ | ✓ | ✓ |

## 5. 风险与验证项

- matchH 切换后所有场景 HUD 像素偏移的屏幕占比变化 ~1.78 倍, P2 逐项重验, 预期需要一轮 Inspector 调参 (ShopUXManager.OnValidate 实时调参已支持)。
- HPBarRoot 编辑态几何异常, 若 runtime 也超屏则是存量 bug, 顺带修。
- 像素化在相机距离变化下的视觉稳定性 (像素块尺寸是屏幕空间的, 世界缩小 = 卡面像素更细)。
- TMP SubMeshUI (NotoSansSymbols2) 在缩放切换下的 fallback 表现。
- Editor Game 窗口 aspect 锁定与 Free Aspect 拖拽都纳入验收。
