# UI 窗口比例自适应统一方案（UI Aspect Adaptation）

日期: 2026-09-09
状态: 方案已拍板, 待「修改代码」开工
Re-audit: 2026-10-04 完成 (§6 记录全部事实修订: 商店 canvas 已删 / 相机为正交 / 死件消失), 分阶段清单已按现状刷新
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
| ~~Shop~~ | — | **已随 09-18 世界化整树删除** (85a605af world-entity shop chrome) | 现商店零屏幕空间 canvas; 可视层全部世界空间 (chrome band prefab / panels / 价格按钮 / reroll) → 不受 P1 影响, 只受 P3 视锥变化影响, 见 §1.5 | 原 P2.3 按钮重调作废, 见 §6 |
| Result | 1 | SSS 1080x1920 match=0 | 仅遗留 resultInfoDisplay (活件, PhaseManager 持续写入) | 1000x1840, matchW 方窗下 viewportY=[-0.35,1.35] 上下溢出; matchH 后 1920 纵向空间自动容纳 |

### 1.2 运行时自建根 Canvas

| 组件 | sortingOrder | 现状 | 结论 |
|------|-------------|------|------|
| ResultStatsPanel | 200 | SSS 1080x1920 + match height + 镜像 camera | 基准实现, 不动 |
| CardTagTooltip | 300 | SSS 1920x1080 (横版 ref, 与全项目竖版相反) + 默认 match 宽 | ref 改 1080x1920 + match height; 位置每帧 Screen.width/height 钳制, resize 自适应已具备 |
| UsernameRegistrationPanel (Net) | Overlay | SSS 1080x1920 match=0 + ScreenSpaceOverlay | match 0->1; Overlay -> ScreenSpaceCamera + 镜像 (对齐 tooltip 先例, 被像素化后处理覆盖) |

### 1.3 死件与活件 (2026-10-04 re-audit 更新)

三件疑似死件 (Deck/Shop/General Info Display) 连同整棵 Shop Canvas 已随 09-18 世界实体化删除 (commit 85a605af), P2.1 死件清理**作废**; 场景现仅存 3 个根 canvas (§1.1)。

活件: `resultInfoDisplay` (PhaseManager.cs:85 字段; 写入 :305/:491; canvas 探测 :502-503), 是 Result Canvas 的唯一子物体。`playerStatsDisplay` 已随世界化从代码消失 (全库 0 引用)。

### 1.4 非 Canvas 耦合层 (本次范围: 相机补偿)

- 相机: Main Camera **正交** (orthographic size 6.06, z=-100; fov=7 字段是死数据 — 52c3058a 2026-02-11 起即正交, 6.06 自 b7e3608d 2026-05-29)。可视高度恒定 12.12 单位, 宽 = 12.12 x aspect。运行时代码已全部按 orthoSize 推导 (PhaseFlightPlanner.PageHeightWorld / ShopUXManager.cs:836 / HudViewportPin / WorldTopoBackground); P3 补偿杠杆据此改写为 orthographicSize, 原 z 距离公式作废 (§6)。
- 商店格子/战斗牌堆: 世界空间网格, 商店滚动下限 ComputeDynamicMinY 已按 viewHalfHeight 推导 (相机距离变化自动适应)。
- DamageFloater: 生成时 World->Screen 一次转换, 播放中不追踪 resize (瞬时件, 接受)。
- 像素化后处理: 全屏, 这是所有 canvas 必须镜像 renderMode/camera 的原因。

### 1.5 世界空间可视层清单 (2026-10-04 新增; 09-09 写作时不存在。均不受 P1 canvas 缩放影响, 只受 P3 视锥变化影响)

| 件 | 位置 | P3 下行为 |
|----|------|----------|
| ShopHudPage chrome band + ShopSectionPanels + 价格按钮 + reroll 翻转 + HP 镜像 | 世界空间 (v5 prefab / runtime panels) | 视锥变高 (9:16 下纵向 21.55 单位) → band 构图/行距逐项重验 |
| OptionsButton (`HudViewportPin`) | 世界空间; LateUpdate 每帧按 orthoSize x aspect 重算角点 → **自适应** | 无代码冲突; 但 inset 是世界单位 (0.25/0.105), 9:16 下纵向 inset 屏占比减半 → 调参 |
| PhaseTransitionDriver 编排 (rig DOMoveY / 卡牌弧飞 / avatar 滑行) | 世界空间 | `PageHeightWorld(orthoSize)` 随补偿自动缩放, 但全部调参基于 6.06 → **前置门: 先等 10-04 transition 两项裁定落地** (plan-transition-entrance-and-shadow-audit-2026-10-04.md) |
| 商店滚动下限 `ComputeDynamicMinY` | viewHalfHeight 由 orthoSize 推导 (ShopUXManager.cs:836) | 自动适应, 只需复验 |

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

### P1 缩放轴统一 (纯设置改动, 零布局代码; 2026-10-04 re-audit 后目标 canvas = Combat/Result/Global 三个, Shop canvas 已不存在)
1. GameScene: Combat / Result Canvas 的 CanvasScaler.matchWidthOrHeight 0 -> 1 (SaveScene)。
2. Global Canvas: ConstantPixelSize -> ScaleWithScreenSize 1080x1920 + match height。
3. CardTagTooltip: referenceResolution (1920,1080) -> (1080,1920), matchWidthOrHeight = 1。
4. UsernameRegistrationPanel: matchWidthOrHeight 0 -> 1; renderMode Overlay -> ScreenSpaceCamera + worldCamera/planeDistance 镜像 (照抄 ResultStatsPanel.Build)。
5. 方窗/竖窗冒烟: 各 phase 打开看整体缩放是否符合第 3 节预期。

### P2 活件重锚 (2026-10-04 re-audit: 死件清理作废, 原因见 §1.3)
1. ~~死件删除证据清单 -> 用户确认 -> 从 GameScene 删~~ **作废** (Shop Canvas 整树已随 85a605af 删除)。
2. resultInfoDisplay: 确认 matchH 后 1920 空间内的显示范围, 必要时 body 改 fraction 锚。
3. ~~Reroll/exit 按钮偏移 -420/-175 重调 (横窗 12% 视宽问题)~~ **作废** (原 canvas 按钮已删; reroll 现居 ShopSectionPanels header 世界空间, OptionsButton 已由 HudViewportPin 视口钉住 09-28 + 跨页滑行 10-04 — 只剩 P3 纵向 inset 调参)。
4. ~~HPBarRoot 几何复核~~ (2026-09-10 完成, 升级为全屏纵向重做): 探针定根因 = 2000x2000 固定 px + 90° 旋转伪纵向 (旋转矩形脱离锚点跟随, ConstantPixelSize 无补偿)。已实施: HPBarRoot 全屏拉伸锚 (0,0)-(1,1) 去旋转; 4 张 Filled Image (两段+两闪光) fillMethod Horizontal→Vertical (玩家 Bottom/敌人 Top); CombatHPBarPresenter ghost 带 X 轴→Y 轴 (anchor Y 区间 + DOScaleY 向本方底边/顶边塌缩)。验证: 视口角点 = (0,0)-(1,1) 与 Canvas 重合, 20/20 时两段 fill 各 0.5 分割线压中线。详见 CombatHPBarPresenter VISUAL-FIX(2026-09-10) + RegressionChecklist 行 87。
5. Combat 中央堆 (Tips/Revealed/EffectResult) 与四角 HUD 在 1920 空间内重验重叠与占位。

### P3 视锥补偿 (新组件; 2026-10-04 按正交相机重写)
**前置门**: plan-transition-entrance-and-shadow-audit-2026-10-04.md 两项裁定 (entrance-slide / flip-shadow) 落地后才开工 — 转场编排基于 orthoSize 6.06 调参, 先动补偿必二次返工。
1. 新组件 `CameraAspectCompensator` (UXPrototype): 挂 Main Camera, 只写 `orthographicSize` (投影属性, 无任何其他系统占用 — rig Y 归 PhaseTransitionDriver, 相机子级 Y 归商店滚轮/Shaker, 零轴重叠)。
   公式: aspect < 1.0 时 size = 6.06 / aspect (保持可视宽度 >= 12.12); aspect >= 1.0 时 size = 6.06; clamp size 到 [6.06, 10.77] (9:16 -> 10.773)。
2. Update 轮询 aspect 变化 (窗口拖拽), 相位切换时重算。下游正交件全部自动适应: ComputeDynamicMinY (ShopUXManager.cs:836)、PageHeightWorld、HudViewportPin 角点、WorldTopoBackground。
3. 9:16 重验清单: 商店滚动边界 / 战斗牌堆构图 / HUD 与世界卡相对位置 / chrome band 与 panels 纵向构图 (§1.5) / OptionsButton 纵向 inset 屏占比 / 转场编排观感。

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
- ~~HPBarRoot 编辑态几何异常~~ 2026-09-10 已随全屏纵向重做吸收 (见 P2.4), P1 迁 SSS matchH 与其正交 (拉伸锚在两种 scaler 下都铺满)。
- 像素化在 orthoSize 变化下的视觉稳定性 (像素块尺寸是屏幕空间的, 视锥变高 = 卡面像素更细)。
- TMP SubMeshUI (NotoSansSymbols2) 在缩放切换下的 fallback 表现。
- Editor Game 窗口 aspect 锁定与 Free Aspect 拖拽都纳入验收。

## 6. Re-audit 记录 (2026-10-04, 只读探针 + 静态 grep, 零代码/场景改动)

| # | 修订 | 证据 |
|---|------|------|
| 1 | Shop Canvas 整树 (含三死件 + Player Stats Display) 已于 09-18 世界实体化删除; 商店现零屏幕空间 canvas, 可视层全世界空间 (§1.5); P1 目标从 4 canvas 减为 3 | `git log -S "Shop Canvas" -- GameScene.unity` → 85a605af; 场景仅存 Global/Combat/Result 三根 canvas (YAML + 编辑器探针双确认) |
| 2 | Main Camera 是正交相机 (size 6.06, z=-100), §1.4 原 "fov=7 透视" 描述自写作日起即不成立; P3 杠杆由 z 距离改为 orthographicSize, clamp [6.06, 10.77] | 场景 YAML `orthographic: 1 / size 6.06` + 探针 `ortho=True size=6.06`; 正交自 52c3058a (2026-02-11), 6.06 自 b7e3608d (2026-05-29) |
| 3 | 活/死件判定刷新: resultInfoDisplay 活 (PhaseManager.cs:85,305,491,502), playerStatsDisplay 代码 0 引用已消失; P2.1 / P2.3 作废 | 全库 grep + 探针 (Result Canvas 唯一子 = ResultInfoDisplay, inactive 属 Result 阶段常态) |
| 4 | P1 其余目标现状核实与 plan 一致仍待做: Global ConstantPixelSize(800x600) / tooltip SSC+1920x1080+match 默认 0 / 注册面板 Overlay+1080x1920+match0 | 探针 scaler 现值; CardTagTooltip.cs:192-201; UsernameRegistrationPanel.cs:21,84-90 |
| 5 | P3 下游正交件清点: ComputeDynamicMinY / PageHeightWorld / HudViewportPin / WorldTopoBackground 均由 orthoSize 推导自动适应; 新增 §1.5 世界空间清单与 P3 前置门 (transition 10-04 裁定) | ShopUXManager.cs:836; PhaseFlightPlannerTests.cs:12-13 (6.06/12.12 为测试参数, 非生产硬编码); HudViewportPin.cs:22-23,129-178 |
| 6 | 时序结论: P1+P2 可独立先行 (屏幕空间 HUD 近期无改动), P3 等 transition 裁定落地; 战役期建议暂停其他 UI 调参 | 本节 + PlanTracker 状态行同步更新 |
