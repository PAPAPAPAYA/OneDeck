# Plan: 商店顶栏逐元素独立布局 (左缘世界偏移 X) (2026-09-23)

- **Date**: 2026-09-23
- **Status**: IMPLEMENTED 2026-09-23 (步骤 1-8 完成；离线编译 0 错/19 存量 CS0618；EditMode 全量 + Play 验证 §4 待 Unity 会话。实现与 §3 有一处顺延：`ViewportToCanvasAnchored` 等 viewport-X 助手按 §3.2 删除，零残留 grep 已验)
- **Request (user, 2026-09-23)**: 在 cf8c52c（ShopLayoutConfigSO 行模型）基础上二步收敛。用户两思路对比后拍板**思路二：每个 clip 独立配置 XY + 宽高**（参考 UIKitDemo §08 截图；❚❚ 高于行轴、income 是 wins/hearts 的子行——行模型表达不了）。**重叠保护不做**（用户裁定）。**X 采用左缘世界单位偏移**（用户裁定"这一步先左对齐"）：全带统一后 chip 间隙在 aspect 变化下保持固定世界间距。
- **Reads**: `plans/plan-shop-layout-config-widget-factory-2026-09-23.md`（上一阶段，cf8c52c）

## 1. Goal / non-goals

Goal:
1. 顶栏 11 个元素（6 chips + money + 两颗按钮 + avatar/HP 镜像）全部独立配置：`{xFromLeftEdge, viewportY, width, height, fontSize}`（按钮/镜像按各自子集），一份 SO 全页语义统一。
2. **固定间隙数学**：X=距视口左缘的世界单位偏移 → aspect 变化时左缘平移、块内元素相对位置不动（gap = 偏移差 − 宽度，常量）；Y 保持视口比例（纵向由 orthoSize 锁死，本就 aspect 安全）。❚❚ 保持右缘锚（角落元素）。
3. 默认值 = 现有行模型公式在 **ortho 6.06 × 16:9**（2×halfW = 21.5467）下的计算结果——默认画面零位移；用户窗口 aspect ≠ 16:9 时默认位有偏差，Play 里一次性校准（这正是本功能的用途）。

Non-goals:
- 不做 chip 重叠警告（用户裁定）。
- 不动 chips 文本内容/颜色（✦ 带冒号、HP 大小字号是后续表现层项，另记账）。
- 不动 band/root、工厂、转场/滚动逻辑。

## 2. 烘焙默认值（ortho 6.06, aspect 16:9; offset = 旧 viewportX × 21.5467）

| 元素 | xFromLeftEdge | viewportY | width × height | fontSize | 旧值出处 |
|---|---|---|---|---|---|
| 离开商店 | 1.65 (=0.35 边距+2.6/2) | 0.956 | 2.6×0.64 | 2.8 | ExitAnchorX 公式 |
| ❚❚（右缘锚） | rightMargin 0.35 | 0.956 | 0.72×0.64 | 2.4 | optionsX 公式 |
| money | 14.09 (=0.654×21.5467) | 0.959 | 2.4×0.62 | 2.8 | MoneyChipViewport |
| 稀有度✦ | 6.28 (=列 5.43+0.85) | 0.883 | 1.7×0.5 | 1.9 | columnX+0.5w |
| 稀有度✦✦ | 8.23 (=5.43+2.80) | 0.883 | 1.7×0.5 | 1.9 | columnX+1.5w+s |
| 稀有度✦✦✦ | 10.18 (=5.43+4.75) | 0.883 | 1.7×0.5 | 1.9 | columnX+2.5w+2s |
| wins 🜲 | 6.28 | 0.810 | 1.7×0.5 | 1.9 | 同列 |
| hearts ♥ | 8.23 | 0.810 | 1.7×0.5 | 1.9 | 同列 |
| income | 11.28 (=5.43+4.75+1.1) | 0.810 | 2.2×0.5 | 2.2 | 同列+半大宽 |
| avatar | 6.29 (=0.292×21.5467) | 0.956 | scale 0.28 | — | PlayerIconViewport |
| HP 药丸 | 9.42 (=0.437×21.5467) | 0.959 | scale 0.5 | — | HpDisplayViewport |

## 3. Design

### 3.1 ShopLayoutConfigSO（重写字段区；类内自持默认）
- 嵌套 `[System.Serializable] class ElementPlacement { xFromLeftEdge / viewportY / width / height / fontSize }`；`static readonly ElementPlacement XXXDefault` 七份（exit、money、稀有度×3、wins、hearts、income——**默认值唯一源**，Inspector 字段初始器与代码 fallback 共用）。
- 解析器加 `Placement(selector, fallback)`（对象版 V）；V/V2 保留（band/feel/镜像标量用）。
- 字段区：Band & Root 3 项不变；exit = `ElementPlacement`；options = 右缘锚平铺 5 项（rightMargin/viewportY/width/height/fontSize，常量默认）；7 颗 chips 各一个 `ElementPlacement`；镜像 = 平铺 `playerIconXFromLeftEdge/playerIconViewportY/playerIconShopScale` + hp 三件；Button Feel 2 项不变。共 56 字段。
- 旧行模型字段全删（column/odds/stats Y、spacing、共享 small/large 尺寸字号、共享 buttonHeight、moneyChipViewport 等）。

### 3.2 ShopTopBarLayout（换语义）
- 保留：`ViewportToChromeLocalY` 双载（Y 语义不变）、`PlayerIconShopScale/HpDisplayShopScale` 属性、band 属性族。
- 新增属性：`PlayerIconXFromLeftEdge / PlayerIconViewportY / HpDisplayXFromLeftEdge / HpDisplayViewportY`（镜像标量四件，fallback 常量在本类）。
- 新增换算：
  - `ChromeLocalXFromLeftOffset(offset, halfW) = offset - halfW`（chrome 局部 X）；
  - `LeftOffsetToCanvasAnchored(xFromLeftEdge, viewportY, canvas, orthoSize)`——canvas 侧 X 换算 **aspect 自消**：screenPx = offset × Screen.height / (2×orthoSize)（halfW 的 aspect 项与 Screen.width 约分），只依赖屏幕高与 ortho；Y 同旧公式。
  - `MainOrthoSize`（Camera.main.orthoSize，缺省回退 6.06f = 出厂主相机）。
- 删除：`ViewportToCanvasAnchored`、`ViewportToChromeLocal` 双载、PlayerIconViewport/HpDisplayViewport/MoneyChipViewport/HudColumn/Odds/Stats 属性与 Default 常量（全部无残余消费方，§5 步骤 8 grep 兜底）。

### 3.3 ShopChrome
- 顶部落段不变（band/root/captured `_rigPos/_halfW/_orthoSize`）。删除行堆叠算术与 `ExitAnchorX`；`ApplyButton/ApplyChip` 改为按 placement 落位（chip z 0.02、按钮 z 0 不变，`HudChip.ApplyLayout` 复用）；Build 创建读 `XXXDefault` 的尺寸作首帧值（根未激活，不渲染）。
- ShopChrome 上的尺寸/间距/字号/手感 `...Default` 常量族删除（默认迁至 ShopLayoutConfigSO 静态；band 三常量留原地作跨文件 fallback）。

### 3.4 ShopPageHud（镜像锚点换算）
- `BuildAvatar/BuildHpPill` 的中心改为 `(ChromeLocalXFromLeftOffset(XFromLeftEdge, halfW), ViewportToChromeLocalY(viewportY, ortho))`（cam 在手，halfW = ortho×aspect 现算）；缩放读点不变。

### 3.5 Canvas 侧消费点（**本阶段不再零改动**——语义迁移必须跟随）
- `CombatIconPresenter` :150-152、`HPNumericDisplayHorizontal` :447-449/:460-462：`ViewportToCanvasAnchored(X, canvas)` → `LeftOffsetToCanvasAnchored(X, Y, canvas, ShopTopBarLayout.MainOrthoSize)`。4 处调用，机械替换；combat 锚分支不动。

## 4. Verification
1. 离线 `dotnet build Assembly-CSharp.csproj` 0 错（无新文件，csproj 不动）。
2. EditMode 全量（ShopPageHudTests/ShopSectionPanelsTests 只测纯静态，预期不受影响）。
3. Play（用户）：默认位与 cf8c52c 一致（ortho 6.06/16:9 窗口下像素级）；拉伸 Game 窗口 aspect——chip 间隙恒定、整块贴左缘、❚❚ 贴右缘；按参考图重排（❚❚ 抬高、wins/hearts 上线 + income 下线）应纯调参达成；进出店/转场/滚动回归（清单 113 行族）。

## 5. Implementation checklist
1. 本文档（先落方案，用户要求）。
2. ShopLayoutConfigSO 字段区重写（ElementPlacement + Placement 解析器 + 默认静态）。
3. ShopTopBarLayout 换语义（3.2 清单）。
4. ShopChrome ApplyLayout/Build 改造 + 旧常量清除。
5. ShopPageHud 两处镜像锚点。
6. CiP/HPN 四处 canvas 读点。
7. ShopLayoutConfig.asset 重写（56 字段烘焙值，GUID 不变）。
8. 残留 grep（ViewportToCanvasAnchored/ViewportToChromeLocal/旧属性零引用）+ CRLF 检查。
9. 离线编译 → 文档（ShopSystems 节更新、AGENTS.md 09-23 句改写并保 ≤31744B、RegressionChecklist 114 行）→ 计划状态回写。

## 6. Risks / notes
1. **默认值按 16:9 烘焙**：窗口 aspect 不同的首次观感与 cf8c52c 有横向偏差，Play 校准一次即消除（校准结果可存资产）。
2. **整块贴左缘**：超宽屏右侧留白（用户已裁定接受，参考图即左对齐块）。
3. canvas 侧 2 文件 4 读点本次被触碰（上一阶段"零改动"承诺语义性失效，属预期）；换算公式已把 aspect 项约分，无每帧 aspect 依赖。
4. 无新文件、无 csproj 操作；测试零 API 引用（新旧均为纯静态或字段）。
