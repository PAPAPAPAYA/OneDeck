# Plan: 重掷按钮 Prefab 化 — 样式权威移交 prefab (2026-09-29)

- **Date**: 2026-09-29
- **Status**: IMPLEMENTED 2026-09-29（Unity MCP 路线，本会话实施；烘焙基准 = 场景 panels 现值 1.75/0.52/4 + fontBold=True；EditMode 664/663/1 = 基线零漂移；§4.3 Play 目测矩阵待用户）
- **Request (user, 2026-09-29)**: 重掷按钮想像 ShopChrome 顶栏一样 prefab 化，主要为了能配置样式（颜色 / 字体资产 / 字号 / 加粗 / 按压手感）。拍板：**尺寸权威 = prefab**（PanelsTuning 不再覆写实例尺寸）。
- **Reads**: `plans/plan-shop-hud-prefab-widgets-2026-09-23.md`（Tpl_WorldButton / PaletteTint / 变体机制 / SyncColliderToFace §3.5）, `plans/plan-shop-sectionpanels-live-tuning-2026-09-22.md`（PanelsTuning 14 参数与 Play 调参）, `plans/plan-shop-price-button-prefab-2026-09-29.md`（姊妹计划，共享 `_faceColor` 捕获修复）, `plans/plan-shop-price-text-style-centralization-2026-09-29.md`

## 1. Goal / non-goals

Goal:
1. 新建 `Assets/Prefabs/ShopHud/RerollButton.prefab`（`Tpl_WorldButton` 的变体，ExitButton 同款做法）：颜色（PaletteTint 槽位）、字体资产、字号、加粗、尺寸、按压手感全部为 prefab 数据，prefab-stage 可视化编辑。
2. `ShopSectionPanels` 从「运行时搭建」改为「实例化 prefab + 代码接线」：动作 / 动态文本 / 滚动守卫 / disabled 逻辑全部保留在代码，样式字段代码不再触碰。
3. 代码侧删除对实例的样式覆写（`ApplySharedStyle` reroll 块），`FitPanel` 定位改读实例面片宽度；PanelsTuning 退役 5 个死字段。
4. 零视觉变化（以当前场景调参值为烘焙基准）：颜色走同一套 palette 槽位、尺寸/字号以场景现值为准。

Non-goals:
- 面板背景 / 标题文字 / 卡组计数器的 prefab 化（同一 recipe 可后续推广，本次不做）。
- `HudTextBinding` / `HudActionBinding` 绑定化（`ActionKey.Reroll` 等）——动态文本逻辑已在 `RefreshRerollState`，收益只是少一个硬编码字符串；且 `ShopHudBinder` 只扫 ShopHudPage children，panels 侧需自建接线，§6 后续项。
- `PhysButton` 状态机 / `ShopInputGate` / `ShopWorldWidgets`（回退路径仍在用）/ 买/卖价格按钮（姊妹计划）/ palette / 字体 fallback 链。

## 2. Current state (verified facts, 2026-09-29)

### 2.1 现行运行时装配与样式覆写链

| 位置 | 现状 |
|---|---|
| `ShopSectionPanels.CreateRerollButton` (:281-295) | 调 `ShopWorldWidgets.CreateWorldButton` (:88-130) 逐个 `new GameObject` 拼 Root(BoxCollider2D)/Visual/Shadow/Face/Label；颜色写死读 `GameColorPalette` 静态量（CardShadowColor / ButtonFaceColor / OwnerTextColor） |
| `ShopSectionPanels.ApplySharedStyle` (:176-203) | **每次 RefreshLayout 覆写实例**：`ConfigureWorldFaceSize(buttonWidth, buttonHeight)`、restShadow/hoverLift/denyShift、label fontSize/fontStyle/rect sizeDelta——全部来自 `ShopUXManager.panels`（PanelsTuning） |
| `ShopSectionPanels.FitPanel` (:348-351) | X = `max.x − headerRightMargin − buttonWidth×0.5 − 0.15`（buttonWidth 来自 Tuning），Y = headerY |
| `ShopSectionPanels.RefreshRerollState` (:384-404) | 动态文本（免费 `重掷 $0` / 否则 `重掷 $价格`）、`SetDisabled`、label 颜色直接 flip（CardTextSoftColor ↔ OwnerTextColor） |
| `ShopSectionPanels.SetRerollRolling` (:147-153) | 滚动期间拒绝输入的守卫 |
| `ShopUXManager.cs:708-709` | Bootstrap 接线点：`ShopChrome.Bootstrap(hudPagePrefab)` + `ShopSectionPanels.Bootstrap(chromeSprite, chromeFont)` |

### 2.2 Tpl_WorldButton 模板事实（prefab 烘焙的原料）

- 模板层级 Root(BoxCollider2D)/Visual/Shadow(z 0.04)/Face(z 0.02)/Label；`PaletteTint` 槽位已挂：Shadow=**CardShadow(5)**、Label=**OwnerText(4)**、Face=**ButtonFace(13)**——与运行时写的三个静态色一一对应（slot 序号 = PaletteTint.Slot 枚举声明序，append-only）。
- 模板手感显式烘焙：restShadow 0.05 / hoverLift 0.05 / denyShift 0.075（= PanelsTuning 出厂默认，重掷无需覆盖这三项）。
- Shadow localPosition (0.05, −0.05, 0.04) = restShadow 向量偏移的烘焙结果；注意 `ConfigureWorldFaceSize`（PhysButton.cs:187-208）运行时会按 `restShadow` 重写 shadow localPosition 与 collider 包络——烘焙一致性靠 `SyncColliderToFace`（PhysButton.cs:210-219，ContextMenu，按运行时同款公式重算）。
- `PhysButton` 私有接线引用已 `[SerializeField]`（:59-61），prefab 实例自带、无需重 wire——ExitButton/OptionsButton 变体已在生产验证此链。
- 变体机制先例：ExitButton = Tpl_WorldButton 变体（override m_Name / Face+Shadow m_Size / collider m_Size / m_fontSize+m_fontSizeBase）；OptionsButton 另挂 HudViewportPin（唯一钉视口例外，与本计划无关）。
- **09-28 chip「变体字号死数据」坑在此天然不存在**：panels 根是运行时 `new GameObject`，没有持久化实例 override 层；`RerollButton.prefab` 资产是唯一编辑面，改了必生效。

### 2.3 必须补的一个代码缺口（姊妹计划共享）

`PhysButton._faceColor`（disabled 恢复用原色）**只在 `SetWorldFace` 里捕获**（PhysButton.cs:62 初始化为 `Color.white`，:162 唯一捕获点；:316-320 `ApplyDisabledVisual` 恢复时用）。prefab 实例没人调 setter → 重掷从 disabled 恢复时 face 会变**纯白**而非 ButtonFace 色。chrome 现有 Exit/Options 未踩到是因为它们永不 disabled。修法：实例化后补一句 `SetWorldFace(face子物体)`（公共 API，零 PhysButton 改动）。

### 2.4 字体样式可资产化依据

- 字号 = TMP `m_fontSize`（序列化）；**加粗 = `m_FontStyle = Bold`**（TMP 合成加粗 = 顶点外扩，穿 CJK fallback 链，零资产，09-25 探针验证，见 `tmp-bold-synthetic-cjk-fallback` 记录）；字体资产 = `m_font`（换字体须自带 CJK 覆盖，文本「重掷 $0」现走 RobotoCondensed→SourceHanSansCN fallback）。
- 删除 `ApplySharedStyle` 覆写后无竞争写入者：`RefreshRerollState` 只写 text/color，`PhysButton` hover 只重写 text。
- 语义变化（已知悉）：`PanelsTuning.fontBold` 此后只管 3 个标题 + 卡组计数器；重掷 label 的粗细由 prefab 自己表达。

## 3. 方案

### 3.1 烘焙 RerollButton.prefab（纯编辑器操作）

1. `Tpl_WorldButton` 变体，命名 `RerollButton`，override：m_Name、Face/Shadow m_Size、collider m_Size、Label `m_fontSize`(+`m_fontSizeBase`)、Label 文本 `重掷 $0`（首刷占位，运行时 `RefreshRerollState` 必写）。
2. 数值来源 = **当前场景 `ShopUXManager.panels` 现值**（用户 09-22 起 Play 调过参；出厂默认 buttonWidth 2.0 / buttonHeight 0.56 / buttonFontSize 2.4）。删字段前先抄现值。
3. Label rect sizeDelta = (宽 − 0.2, 高)（与运行时 :201 同规则）。
4. 改完 Face m_Size 后右键 PhysButton → **Sync Collider To Face**（shadow 尺寸/偏移 + collider 包络一并按公式重写）。
5. 加粗 / 字体资产 / 颜色按用户口味在 prefab 里直接设；PaletteTint 槽位继承模板（CardShadow/OwnerText/ButtonFace），换色 = 改槽位或改 ColorSO 资产。
6. disabled 文字色：`RefreshRerollState` 的直接 color flip 改为**翻槽位**（OwnerText ↔ CardTextSoft(7)）+ `PaletteTint.Apply`，避免与 PaletteTint 的重刷（OnEnable / 编辑器 Changed 广播）打架。

### 3.2 代码改动清单

| 文件 | 改动 |
|---|---|
| `ShopUXManager.cs` | 新增 `[Tooltip] public GameObject rerollButtonPrefab;`（与 `hudPagePrefab` 同排 :72-77）；`Bootstrap` 调用点 (:709) 增参 |
| `ShopSectionPanels.cs` | `Bootstrap(Sprite, TMP_FontAsset, GameObject)` 增参存字段；`CreateRerollButton` prefab 分支：`Instantiate` → `SetWorldAction` → `SetWorldFace(face)`（补 `_faceColor` 捕获）→ 缓存 `_rerollLabel`；**未接线回退**现有运行时构建（保测试 / 旧场景） |
| `ShopSectionPanels.ApplySharedStyle` | 删整个 reroll 块（:190-202：ConfigureWorldFaceSize / restShadow / hoverLift / denyShift / label fontSize+fontStyle+rect）；headers/counter 部分保留（fontBold 语义收窄，§2.4） |
| `ShopSectionPanels.FitPanel` | reroll X 定位改读实例面宽：`PhysButton` 加 `public Vector2 WorldFaceSize => _faceRenderer != null ? _faceRenderer.size : default;`，X = `max.x − headerRightMargin − faceW×0.5 − 0.15` |
| `ShopSectionPanels.cs` consts | 删 ButtonWidthDefault / ButtonHeightDefault / ButtonFontSizeDefault / RestShadowUnitsDefault / DenyShiftUnitsDefault |
| `ShopUXManager.PanelsTuning` | 删 5 个死字段 buttonWidth / buttonHeight / buttonFontSize / restShadowUnits / denyShiftUnits（全项目唯一消费者是 ShopSectionPanels，grep 已核；`fontBold` 保留） |

## 4. Verification gates

1. **零视觉变化基线**：改前用 Play 调参现值烘焙 prefab；改后 EditMode 下对比实例 Face/Shadow m_Size、collider、label fontSize/fontStyle 与改前运行时值（烘焙现值 vs 探针，参照 09-24 计划的探针纪律）。
2. **EditMode 套件**：基线 664/663/1。`ShopSectionPanelsTests` 不引用被删字段（已核），但删 PanelsTuning 字段后须全量跑——注意 stale-assembly 纪律（refresh_unity + DLL mtime > 源 mtime）与 run_tests 前先 `EditorSceneManager.SaveOpenScenes()`。
3. **Play 目测矩阵**（用户）：重掷点击 / 免费↔收费文案翻转 / 无钱 disabled 变暗 + 恢复后 face 颜色正确（`_faceColor` 缺口回归点）/ hover 抬升 / deny 摆动 / 滚动中拒绝 / 面板 refit 后按钮 X 随内容走。
4. 行尾纪律：改动 `.cs` 确认 CRLF + Tab；`ls-files --eol` 验证。

## 5. 坑与注意

- prefab 分支必须在 `SetActive(false)` 的根下构建（与现 Bootstrap 同款时序），`Awake` 延迟不 clobber 序列化引用（09-25 VISUAL-FIX 已修的 guard 依赖此）。
- 回退路径保留意味着 `ShopWorldWidgets.CreateWorldButton` 不删；两个分支都补 `SetWorldFace`。
- 变体编辑字号时 Unity 会连写 `m_fontSize` + `m_fontSizeBase`（ExitButton 先例），无需手工配对。

## 6. Open items（后续可选项）

1. 文本/动作绑定化：`HudActionBinding.ActionKey.Reroll` + `HudTextBinding.BindingKey.RerollLabel`（枚举 append-only）；`ShopHudBinder` 只扫 ShopHudPage children，panels 侧需自建 mini-binder 或泛化收集——收益小，等有第二个面板按钮再做。
2. 同 recipe 推广到面板背景 / 标题 / 计数器（用户未要求）。
