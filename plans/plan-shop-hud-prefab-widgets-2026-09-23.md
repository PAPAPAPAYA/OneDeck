# Plan: 商店顶栏 Prefab 模板化 — 按钮 + 只读 chip (2026-09-23)

- **Date**: 2026-09-23
- **Status**: IMPLEMENTED (2026-09-24 执行完毕:checklist 1-9 + 11 + 复审补录对齐;几何/文本 93 字段基线 diff 为空,颜色 20/20 与旧路径静态量逐位相等,EditMode 671/670/1 与基线一致;仅剩步骤 10 的 Play 验证矩阵待用户目测。评审修订同日:§2.2/§3.4/§3.5/§3.7 补 ShopSectionPanels 消费链与 PhysButton/HudChip 事实、§2.3/§7.9 补 bandInsetFromTop 读链、§2.4 镜像现值勘误、§5 探针字段补充。复审补录同日对齐:§3.5 手感出厂值=模板已显式设 0.05/0.05/0.075 ✓、§3.4 Setup 禁用=binder 从不调用且染色归 PaletteTint ✓、§6.1 颜色门槛=补采探针 `tools/outputs/shop_hud_colors_after.json` 20/20 ✓、步骤 3/7 去冗=HudChip 仅删 ApplyLayout ✓)
- **Request (user, 2026-09-23)**: UIKitDemo 移植效果糟糕——顶栏全部是运行时代码拼装,布局只能改数值盲调。改用「完善 prefab + 可配置通用模板 + 用户自己排版」。范围经逐轮确认:
	- **只做按钮与只读元素两种原子模板**;面板(内容自适应几何)与头像/HP 镜像各自独立,后续单出小步
	- 用户自己排版页面 prefab;字体大小/文字排版/TMP 富文本混排字号(参照战斗 HPNumericDisplayHorizontal)必须支持
- **Reads**: `plans/plan-shop-layout-config-widget-factory-2026-09-23.md` (SO 字段清单、基线探针 §6.1、CRLF 纪律), `plans/plan-shop-topbar-world-scroll-2026-09-21.md` (band 一次捕获、页面随滚动语义), `plans/plan-world-entity-shop-chrome-2026-09-18.md`, `docs/ShopSystems.md`, `docs/demo/UIKitDemo.html` §08

## 1. Goal / non-goals

Goal:
1. 顶栏 7 chip(金钱 / ✦ / ✦✦ / ✦✦✦ / 🜲 胜负 / ♥ 心数 / 收入)+ 2 按钮(离开商店 / ❚❚)从「运行时代码拼装」翻成「prefab 可视化排版」:位置、尺寸、字号、文案全部为 prefab 数据。
2. 两个原子模板 prefab:`Tpl_Chip`(只读信息)与 `Tpl_WorldButton`(交互按钮);dark/light、大小、字号等变体一律走 Prefab Variant,不开新模板。
3. 绑定层解耦排版与数据:`HudTextBinding { key, format }` + `HudActionBinding { key }`;format 为 prefab 字符串字段,支持 TMP 富文本(`<size>` / `<color>`)实现单文本混用字号;多 TMP 复合排版靠多个 binding 并存。
4. 零行为回归:`ShopChrome` 对外 API 全保留,全部既有调用点零改动;基线 JSON diff 为空(硬门槛,§6.1)。

Non-goals:
- 三块内容自适应面板(`ShopSectionPanels`、重掷按钮、`03/05` 计数)——几何自适应是独立问题,§8.1 后续单出计划。
- 头像/HP 镜像(`ShopPageHud` 455 行拷贝机)的替换——本计划只加编辑期 gizmo 参考线,镜像机制一行不动(§3.8);替换见 §8.2。
- `PhysButton` 状态机 / `ShopInputGate` / 滚动 rig / `PhaseTransitionDriver` / 卡牌价格按钮(`ShopCardView.cs:162-201`)/ palette / 字体 fallback 链 / `ShopTopBarLayout` 换算(镜像与 canvas 停靠仍在用,§7.1)。
- 纵横比语义重做:chip/按钮布局按设计分辨率烘焙,旧的「X 距视口左缘」语义随 SO 字段退役(仅 chip/按钮;镜像 6 字段保留该语义,§7.5)。

## 2. Current state (verified facts, 2026-09-23)

### 2.1 本次要替换的运行时装配链

| 文件 | 行数 | 职责 |
|---|---|---|
| `Assets/Scripts/UXPrototype/ShopChrome.cs` | 372 | Build :182-230 运行时拼 2 按钮(:192-215)+ 7 chip(:217-223)+ 子 `ShopPageHud`(:229);根放置 :252;Refresh :316-371 拼全部文案 |
| `Assets/Scripts/UXPrototype/ShopWorldWidgets.cs` | 130 | 静态工厂:CreateSliced / CreateWorldLabel / CreateChip :65-80 / CreateWorldButton :87-128。消费者除 ShopChrome 外还有 `ShopSectionPanels`(:249/:258/:269)——**本计划不删此文件**,仅 chrome 侧停用(§3.7) |
| `Assets/Scripts/SOScripts/ShopLayoutConfigSO.cs` + `Assets/Resources/ShopLayoutConfig.asset` | 158 | 24 个序列化字段(3 band + 8 ElementPlacement + 5 options + 6 镜像 + 2 手感;leaf float 口径 56);chip/按钮/band 共 18 个字段本计划退役,仅留镜像 6 字段(§3.7) |
| `Assets/Scripts/UXPrototype/HudChip.cs` | 66 | 只读 chip 组件(底 + label),现为运行时接线;改为序列化引用后可入 prefab(§3.4) |

### 2.2 必须冻结的对外 API 与调用点(零改动硬约束)

| API(`ShopChrome`) | 调用点 |
|---|---|
| `Bootstrap(...)` | `ShopUXManager.cs:704`(签名改为 `Bootstrap(GameObject hudPagePrefab)`,同址) |
| `ShopSectionPanels.Bootstrap(Sprite, TMP_FontAsset)` | `ShopUXManager.cs:705`;面板把 sprite/font 存为私有字段(:58-59, :92-93),在 :249/:258/:269 经 ShopWorldWidgets 消费——`chromeSprite`/`chromeFont` 两字段必须保留给面板 |
| `ShowIfActive` / `HideIfActive` | `ShopManager.cs:413-414` / `:443-444`;`PhaseTransitionDriver.cs:277`(转场落地) |
| `RefreshIfActive` | `ShopManager.cs:352-353` / `:383-384` / `:532-533`(买/卖/reroll 后) |
| `BandBottomWorldY()` / `CheckShelfClearance(...)` | `ShopChrome.cs:107-110` / `:163-180`;消费点 `ShopUXManager.cs:1246`(reroll 后净空断言) |

### 2.3 契约三字段(迁入 page 根组件,不删)

- `bandHeight` = 2.6 / `bandInsetFromTop` = 0.5 / `cameraForwardOffset` = 2。根放置公式 `transform.position = _rigPos + (0, orthoSize − bandInsetFromTop, cameraForwardOffset)`(:252)依赖运行时活值(rigPos / orthoSize),prefab 表达不了,必须保留为数据输入。
- `bandHeight` 定义 `BandBottomWorldY`,是货架布局系统消费的保留区契约,不等于顶栏视觉外延(留白是口味值);不从 prefab 包围盒自动推导(TMP overflow / 阴影 / pivot 导致包围盒不可靠,且契约要显式)。
- 同步义务:用户在 prefab 里改顶栏高度后须手调 `bandHeight` 匹配;后续可加编辑期一键测量工具(§8.4)。
- `bandInsetFromTop` 不只进根放置公式:`ShopTopBarLayout.ViewportToChromeLocalY`(:87-90)静态读 `ShopChrome.BandInsetFromTop`,被**保留不动**的镜像路径消费(`ShopPageHud.cs:157-158/:192-193` 换算 avatar/HP 的 Y)。SO band 字段删除后,该静态读点改源为已建 page 根组件的 `bandInsetFromTop`(未 Build 时回退 `BandInsetFromTopDefault`),换算签名与镜像行为不变(§3.7);`bandHeight`/`cameraForwardOffset` 无外部读者,可安全退役进根组件。

### 2.4 数据与资产依赖

- 数据源(现 `ShopChrome.Refresh` :316-371):`ShopManager.me.purse` / `GetCurrentPayday()` / `GetRarityOddsPercents(...)` / `PhaseManager.wins / winCon / hearts / heartMax`。
- 按钮动作:exit → `PhaseTransitionDriver.RequestShopToCombat(_phaseManager)`,失败回退 legacy `ExitingShopPhase/EnteringCombatPhase`(:206-215);options → placeholder `Debug.Log`(:200)。
- 资产:`Assets/Sprites/RoundedCorner.png`(sliced,border 64,PPU 256,现 `ShopUXManager.chromeSprite`);`RobotoCondensed-Regular SDF` + fallback 链(SourceHanSansCN Regular/Bold、NotoSansSymbols2 = ♥❚、NotoSansSymbolsAlchemical = 🜲,现 `ShopUXManager.chromeFont`);颜色全部走 `GameColorPalette` 静态量(ShopPanelBg / TooltipBg / TooltipText / OwnerCard / OwnerText / CardShadow / CardTextSoft 等)。
- 镜像保留字段(SO 中仅剩的 6 个)——**以资产现值为准**(用户已 Play 调过,资产当前有未提交修改):`playerIconXFromLeftEdge/ViewportY/ShopScale` = 2.57 / 0.88 / 0.41、`hpDisplayXFromLeftEdge/ViewportY/ShopScale` = 4.02 / 0.88 / 0.74(`ShopLayoutConfig.asset`:71-76);代码默认 6.29/0.956/0.28、9.42/0.959/0.5 只是 `ShopTopBarLayout` 的 fallback consts,不代表现值。canvas 停靠消费点 `CombatIconPresenter.cs:148-152`、`HPNumericDisplayHorizontal.cs:443-462`。
- 按钮手感现值:`restShadowUnits` 0.05 / `denyShiftUnits` 0.075 / `hoverLift = restShadow`。
- `PhysButton` 铁律:collider 永不动,只 tween Visual 组(:17-21);运行时接线七连 setter(:117/:127/:137/:155 + 模板字段 + label)——价格按钮与重掷按钮仍走此路径,setter 全保留。
- 测试面:`ShopPageHudTests` / `ShopSectionPanelsTests` 只测纯静态,零引用被移动成员;`ShopChrome` 本身无 EditMode golden(基线靠 §6.1 的运行时探针)。

## 3. Design

### 3.1 总览

```
Tpl_Chip.prefab ──Variant──┐
Tpl_WorldButton.prefab ──Variant──┤
                                  ▼
                    ShopHudPage.prefab (用户排版;根组件 = 契约三字段)
                                  │ 运行时
                    ShopChrome (loader, ~100 行):实例化 → 一次性摆根 → Bind
                                  │
                    ShopHudBinder.Refresh() ← 老调用点 (RefreshIfActive)
```

原则:**绑定层只推字符串与动作,视觉的每一寸都是 prefab 数据**。能点的就是 button 变体,只显示的就是 chip 变体;两种都套不进来的新交互才配开新模板。

### 3.2 PaletteTint(新组件,`ExecuteAlways`)

- 字段:`PaletteColorSlot slot`(枚举:ShopPanelBg / TooltipBg / TooltipText / OwnerCard / OwnerText / CardShadow / CardTextSoft / …,映射 `GameColorPalette` 静态量)。
- `OnEnable`/`OnValidate` 把槽位色染到同 GameObject 的 SpriteRenderer 与/或 TMP;编辑期即所见(palette 静态量编辑期可用,AGENTS.md 既有先例),Play 中随 palette 不漂移。
- 变体改色 = Variant 覆盖 `slot` 字段,不改代码。

### 3.3 绑定层(三个新组件)

- `HudTextBinding { BindingKey key; string format; TMP_Text target; }`:format 支持 `{0}`/`{1}` 两参及 TMP 富文本标签;`Apply(string v0, string v1)` 内 diff-guard(现 Refresh 的逐字比对逻辑搬入)。
- `HudActionBinding { ActionKey key; PhysButton button; }`:Bind 时 `button.SetWorldAction(() => ShopHudBinder.Execute(key))`。
- `ShopHudBinder`(挂 page 根):
	- `Collect()`:`GetComponentsInChildren` 收全部 binding(一个 chip 多个 binding 天然支持 → 多 TMP 混排字号)。
	- `Refresh()`:按 key 从 §2.4 数据源取值推送;`Execute(ActionKey)`:LeaveShop → 现 exit 逻辑原样搬入(:206-215),Options → placeholder log。
	- key 枚举(本计划范围):`Money / Income / RarityCommon / RarityUncommon / RarityRare / Wins / Hearts`;`LeaveShop / Options`。预留后续:`Hp / HpMax / Username / DeckUsed / DeckSize / RerollPrice`。

### 3.4 Tpl_Chip.prefab

- 层次:`Root(HudChip + HudTextBinding + PaletteTint)` → `Panel(SpriteRenderer, sliced RoundedCorner)` + `Label(世界 TMP,RobotoCondensed,richText = true)`。
- `HudChip` 适配:panel/label 引用**已是** `[SerializeField]`(HudChip.cs:12-14),prefab 直接可拖,适配量 ≈ 0;运行时 `Bind()` 保留给 ShopWorldWidgets(面板路径继续用)。chip 迁入 prefab 后 `HudChip.ApplyLayout` 的唯一调用链(ShopChrome.ApplyChip)消失,随步骤 7 一并删除。
- **prefab 路径禁用 `HudChip.Setup`**(复审补录 2026-09-24):`Setup`(HudChip.cs:30-40)会把面板/文本强制染成 TooltipBg/TooltipText,冲掉 `PaletteTint` 的 dark/light 槽位色。prefab 路径染色归 `PaletteTint`,binder 只调 `SetText`。
- 默认 format 逐字抄现串(§4),保证基线逐字相等;之后用户随意改。
- 已知边界(用户已确认):底板为固定尺寸,不随文字撑开;Overflow 模式用户自设;自适应宽见 §8.3。

### 3.5 Tpl_WorldButton.prefab + PhysButton 序列化适配

- 层次(与现运行时配方字节级一致):`Root(BoxCollider2D + PhysButton + HudActionBinding)` → `Visual` → `Face(SpriteRenderer, z+0.02)` + `Label`;`Shadow(SpriteRenderer, z+0.04)` 为 Root 子级。
- `PhysButton` 适配(状态机零改动):`restShadow`/`hoverLift`/`denyShift` 本来就是 public 序列化字段(:31/:33/:39,默认 0.1085/0.1085/0.1628),无需改;唯一改动是私有引用 `_faceRenderer`/`_shadow`/`_visualGroup`(:56-58)加 `[SerializeField]`——prefab 上直接拖引用,运行时建造路径继续走原 setter(setter 全为字段赋值,含 `SetWorldAction` :155-158,非事件订阅,双路径无重复接线风险)。价格按钮、重掷按钮零影响。`Sync Collider To Face` context menu 不变。
- 新增 context menu `Sync Collider To Face`:内部调现有 `ConfigureWorldFaceSize`(:165-186)的并集算法,用户在 prefab 里改 Face 尺寸后一键同步 BoxCollider2D。
- **出厂手感值必须显式设为现值**(复审补录 2026-09-24):代码默认 0.1085/0.1085/0.1628 ≠ 现值——现行为由工厂(ShopWorldWidgets.cs:122-124)与 `ApplyButton`(ShopChrome.cs:297-299)按 SO 写入 restShadow 0.05 / hoverLift 0.05 / denyShift 0.075。`Tpl_WorldButton` 三参出厂即设 0.05/0.05/0.075,否则影深与拒绝抖动变 2 倍有余;且 `ConfigureWorldFaceSize` 的 collider 并集(PhysButton.cs:183-184 取 restShadow+denyShift)随之变大,`Sync Collider To Face` 会烘出错热区。root 级基线探针抓不到此差异,故列为模板出厂硬约束。

### 3.6 ShopHudPage.prefab + 根组件

- 根组件 `ShopHudPage`:契约三字段(§2.3)+ `OpenPage(rigPos, orthoSize)`(一次性摆根,公式同 :252,捕获语义同 Build :184-187)/ `BandBottomWorldY` / `Show` / `Hide`;`ShopChrome.BandInsetFromTop` 静态读点改指本组件(§2.3 镜像链)。
- 编辑期辅助(纯 gizmo,零运行时行为):`OnDrawGizmos` 画 `bandHeight` 下缘净空线,并按 SO 镜像 6 字段换算画出 avatar / HP 占位框——用户排版时能看见镜像将来出现的位置,镜像机制本身不动(§3.8)。
- 内容:用户拖入 7 个 chip 变体 + 2 个按钮变体,自由摆位;首次按基线 1:1 复刻(§5 步骤 6),验收后自由重排。
- 排版在 prefab stage 完成(世界 TMP + PaletteTint 编辑期可见,所见即所得);Play 中改 prefab 不保留,这点替代原 SO 实时通道(§7.6)。

### 3.7 ShopChrome → loader

- 保留:`Instance`、`Bootstrap`、`ShowIfActive`/`HideIfActive`/`RefreshIfActive`、`BandBottomWorldY()`、`CheckShelfClearance`(含改引 `ShopSectionPanels.FaceBelowHalfHeightFactor` 的现值)。root 摆放 + `BandBottomWorldY` 捕获语义(现 ApplyLayout :249-253)迁入 `ShopHudPage.OpenPage`——`ApplyLayout` 本身整个删除(它没有任何镜像内容;镜像是 `ShopPageHud.ApplyMirrorLayout` 独立通道,从不经过它)。
- 删除:Build 的 chip/按钮装配段(:192-223)、`CreateChipFromDefault`/`ApplyButton`/`ApplyChip`/`ApplyLayout`、对 `ShopWorldWidgets` 的**消费**(文件保留,见下)。
- **`ShopWorldWidgets.cs` 不删**(评审修订 2026-09-24):`ShopSectionPanels` 仍在 :249/:258/:269 消费三个工厂方法,而面板是本计划 non-goal(§8.1);只把文件头注释改为「现仅 ShopSectionPanels 消费,chrome 已迁 prefab」。
- 变更:`Bootstrap(chromeSprite, chromeFont)` → `Bootstrap(GameObject hudPagePrefab)`(空引用告警文案 :121 同步改);`ShopUXManager` **新增**字段 `hudPagePrefab`,`chromeSprite`/`chromeFont` **原样保留**给 `ShopSectionPanels.Bootstrap`(:705;原「换型」方案会断面板,评审修订 2026-09-24)。GameScene 场景新增 `hudPagePrefab` 接线,必须与代码同提交(§7.2)。
- `ShopLayoutConfigSO`:删 18 个序列化字段(3 band + 8 ElementPlacement + 5 options + `restShadowUnits`/`denyShiftUnits`;按钮手感三参本就是 PhysButton 序列化字段,SO 字段直接退役,无需「迁入」),仅留镜像 6 字段(现值见 §2.4);`OnValidate` 只回写 `ShopPageHud.ApplyMirrorLayout()`。
- `ShopChrome.BandInsetFromTop` 静态读点改源为已建 page 根组件并保留默认回退(§2.3 镜像链),`ViewportToChromeLocalY` 签名不动。
- `ShopTopBarLayout` **保留**(§7.1);`ChromeLocalXFromLeftOffset`/`ViewportToChromeLocalY` 被镜像继续消费(`ShopPageHud.cs:157-158/:192-193`)不删;逐个确认后删真正无读者的 chip 换算助手(核对结果:预计 0 个)。

### 3.8 镜像处理(本计划零改动)

`ShopPageHud` 拷贝机(:154-242, :345-415)、username 轮询(:311-321)、HP count-up(:275-292)、`_mirrorDirty` 重建路径全部原样保留,仍由 chrome loader 建为 page 实例的子级,仍读 SO 镜像 6 字段。仅 §3.6 的 gizmo 给用户排版参考。替换为 prefab 头像/血条(删拷贝机、转场锚点配对、canvas 停靠改锚点驱动)是 §8.2 的独立计划。

## 4. Binding key 与默认 format 清单

默认值以 `ShopChrome.Refresh`(:316-371)现串为准,实施步骤 5 先逐字读出再抄入 prefab 字段(基线硬门槛要求逐字一致)。下表为预期形态(demo §08 参照,以代码现串为准):

| key | 数据源 | 预期 format(prefab 字段) |
|---|---|---|
| Money | `ShopManager.me.purse` | `"${0}"` |
| Income | `GetCurrentPayday()` | `"+${0}/ROUND"` |
| RarityCommon / Uncommon / Rare | `GetRarityOddsPercents(...)` | `"✦ {0}%"` / `"✦✦ {0}%"` / `"✦✦✦ {0}%"` |
| Wins | `PhaseManager.wins / winCon` | `"🜲 {0}/{1}"` |
| Hearts | `PhaseManager.hearts / heartMax` | `"♥ {0}/{1}"` |

混排字号示例(用户已确认需求,无需代码):`"<size=150%>{0}</size><size=80%>/{1}</size>"`。

## 5. Implementation checklist

1. 注册 `.agent_registry/<时间戳>-shop-hud-prefab-widgets.md`(声明将改文件与 SaveScene 需求);扫描他方声明无重叠。
2. 新文件 `PaletteTint` / `HudTextBinding` / `HudActionBinding` / `ShopHudBinder` / `ShopHudPage`(纯新增,零风险)→ `refresh_unity`(compile: request)→ 编译 0 错。
3. `PhysButton` 三个私有引用(`_faceRenderer`/`_shadow`/`_visualGroup`,:56-58)加 `[SerializeField]` + 新增 `Sync Collider To Face` context menu;`HudChip` 零改动(§3.4)→ 编译 0 错(setter 路径回归:价格按钮仍由 `ShopCardView` 运行时建)。
4. Unity 内建 `Tpl_Chip.prefab` / `Tpl_WorldButton.prefab`(编辑器内创建以生成 .meta);**按钮手感三参出厂设 restShadow 0.05 / hoverLift 0.05 / denyShift 0.075(SO 现值,§3.5 硬约束)**,chip 深浅变体槽位按现映射(money/income 浅 TooltipBg,rarity×3/wins/hearts 深 ShopPanelBg)。
5. Play 进店(scroll 0)用 execute_code 导出基线 JSON 到 `tools/outputs/`:chrome 根 position 与 parent + 每颗 chip/按钮 localPosition + SpriteRenderer.size 与 sortingOrder + **面板 SpriteRenderer.color 与 label color**(深浅映射 + 按钮 Face/Shadow/Label 三色——变体槽位配反时纯几何 diff 抓不到)+ label fontSize + **label 文本逐字**;同探针读出 Refresh 现串。探针会话先设 `Application.runInBackground = true` 并断言 `Time.frameCount` 前进(AGENTS.md Play 冻结陷阱);本次 Play 顺带当作 checklist row 113/114(⚠️ 待验)的进店目测验收。
6. 按基线拼 `ShopHudPage.prefab`(1:1 复刻;根组件三字段抄现值 2.6 / 0.5 / 2)。
7. `ShopChrome` 改 loader;`ShopUXManager` **新增** `hudPagePrefab` 字段(`chromeSprite`/`chromeFont` 原样留给面板)+ 场景接线;`ShopWorldWidgets.cs` 保留、仅改头注释;SO 缩至镜像 6 字段;删 `HudChip.ApplyLayout`。
8. 重导探针 JSON 对比:**diff 必须为空**(浮点精确相等 + 文本逐字相等)。
9. EditMode 全量:`refresh_unity` → `isCompiling == false` 且 DLL mtime > 源 mtime → `EditorSceneManager.SaveOpenScenes()` → `run_tests`(init_timeout 180000;基线 671/670/1 Ignore)。
10. Play 验证矩阵(用户目测,§6.2)→ 用户自由重排页面。
11. 文档:`docs/ShopSystems.md` 顶栏节重写;AGENTS.md 商店 bullet 更新(改后 `wc -c` ≤ 32768,留 ≥1KB headroom);`docs/RegressionChecklist.md` 追加行;删注册声明。

## 6. Verification

### 6.1 基线零位移(硬门槛)

步骤 5 与步骤 8 的 JSON diff 为空:每颗 chip/按钮的 localPosition、size、fontSize、面板/文本颜色浮点精确相等,label 文本逐字相等。证明「prefab 系统行为等价于原运行时装配」,之后用户重排不再受此约束。

**实施结果(2026-09-24)**:几何/文本/字号 93 字段逐位对比为空 ✓(`tools/outputs/shop_hud_baseline_before/after.json`);颜色按复审补录补采,7 chip 面板+label 与 2 按钮 Face/Shadow/Label 共 20 项与旧路径所赋 palette 静态量逐位相等 ✓(`tools/outputs/shop_hud_colors_after.json`;旧路径每处颜色即直接赋同一静态量,等值即等价)。两条偏差记录:
1. **YAML 浮点精度**:`OptionsButton` labelRect 的 `0.72f-0.2f = 0.520000041` 无法以逐位相等落盘——Unity prefab YAML 只保 ~7 位有效数字(`0.52` 重载为 `0.51999998`)。已手补 YAML 为全精度值达成门槛;该变体未来被重存时会静默丢回末位 ulp(视觉差 1.6e-8 世界单位,可忽略;旧运行时路径是每次活算故从未落盘)。若要根治可在运行时由 face 宽度推导 labelRect(当前按 plan 原则不加代码)。
2. **镜像 import 抖动(非缺陷)**:首个 after 会话出现 pageHud 4 子节点(默认值+资产值各一对)——SO 资产重导入在该局 Play 内触发 OnValidate 的时序抖动;新开会话与 5000+ 帧稳态均为 2 子节点、资产调参位。

### 6.2 Play 验证矩阵(用户)

| 操作 | 预期 |
|---|---|
| 进店(scroll 0) | 顶栏与基线像素一致(含 avatar/HP 镜像——bandInsetFromTop 改源后 Y 无漂移);滚轮下滚顶栏随页面滚走,回滚复原 |
| 买 / 卖 / reroll 一次 | 对应 chip 文本当帧刷新(diff-guard 无闪烁) |
| 离开商店 → 战斗 → Result → 再进店 ×2 | 顶栏显隐与转场落地表现同现状;`CheckShelfClearance` 按新 band 值正常判 |
| prefab stage 改 chip 颜色槽 / 字号 / format(如加 `<size>` 混排) | 编辑期即所见;下次进店生效,控制台无新告警 |
| prefab 里改按钮 Face 尺寸 → `Sync Collider To Face` | 点击热区与视觉一致;rest/hover/press/deny 四态正常 |

### 6.3 回归

EditMode 全量绿;进出店循环无异常;console 无新告警;卡牌价格按钮(运行时路径)表现不变。

## 7. Risks / notes

1. **`ShopTopBarLayout` 不能删**:镜像与 canvas 停靠仍在消费(`CombatIconPresenter.cs:148-152`、`HPNumericDisplayHorizontal.cs:443-462`);仅 chip 专属换算助手可能变死代码,删除前逐一确认引用。
2. **场景序列化变更**:`ShopUXManager` 新增 `hudPagePrefab` 字段后,GameScene 必须同提交完成接线,否则 chrome 不建(Bootstrap 空引用告警文案同步改为 hudPagePrefab);`chromeSprite/chromeFont` 既有接线不动(面板继续消费,§3.7)。
3. **format 富文本**:label 需 `richText = true`(TMP 默认开);模板出厂即配好,用户新增 TMP 时自查。
4. **基线逐字硬门槛**:步骤 5 必须先读 `ShopChrome.Refresh` :316-371 的实际格式串(表 §4 只是预期),逐字抄入 prefab;不允许凭 demo 猜。
5. **纵横比语义退役(仅 chip/按钮)**:chip/按钮的「X 距视口左缘」字段随退役消失,布局按设计分辨率烘焙(用户已确认接受);镜像 6 字段保留同一 X 语义与跨 aspect 恒定行为,不受影响。真需要时另加可选 `HudRow` 锚点组件(左/中/右对齐 + 固定间距),不在本计划。
6. **SO 实时通道收窄**:chip/按钮失去 Play 中实时调参(prefab 改动 Play 不保留);排版主战场转移到 prefab stage(所见即所得,优于数值盲调)。镜像 6 字段保留原实时通道。
7. **新文件格式**:CRLF + Tab(Write 工具出 LF,写后 sed 转换);预制体/.meta 由 Unity 编辑器生成。
8. 工作量预估:新增 ~400 行(PaletteTint ~60 / binding 三件套 ~200 / page 根 ~70 / 杂项),`ShopChrome` 372 → ~100;SO 24 个序列化字段删 18 留 6(leaf float 口径 56 → 6);`ShopWorldWidgets` 保留(仅改头注释);`PhysButton` 改动缩为 3 个 `[SerializeField]`。
9. **bandInsetFromTop 读链**(评审修订 2026-09-24):`ViewportToChromeLocalY`(:87-90)静态读 `ShopChrome.BandInsetFromTop`,被保留的镜像路径消费(§2.3);SO band 字段删除后该静态必须改源 page 根组件并保留默认回退,漏改则镜像 Y 整体错位 bandInset 个世界单位。
10. **AGENTS.md 余量极紧**(评审修订 2026-09-24):当前余量仅 ~1 KB,步骤 11 改商店 bullet 时顶栏 v3/v4 段落必须净压缩(被 prefab 化取代的细节移入本 plan),否则超 32 KB。

## 8. 后续独立小步(不在本计划)

1. **面板模板化**:`Tpl_SectionPanel` + `PanelEdgeAnchor`(edge + padding);fitter 只保留纯几何(`ComputeContentBounds` 有测试),改写面板根 position 与 `SpriteRenderer.size`;重掷按钮、`03/05` 计数迁入面板模板;届时面板不再走运行时配方,`ShopWorldWidgets` 唯一消费者消失,文件可一并删除。
2. **镜像 prefab 化**:手做世界版 `Tpl_Avatar` / `Tpl_HpPill`(本质仍是 chip 复合 + count-up / username 绑定),删 `ShopPageHud` 拷贝机 455 行;转场 shared-element 锚点配对 + canvas 停靠改锚点驱动是其中风险点。
3. **FitToText 增强件**:读 `preferredWidth` 反推 sliced 底板 size,chip 宽随文字自适应(编辑期可预览)。
4. **bandHeight 一键测量**:编辑期工具测 page 最低可视元素,建议值写进根组件。
5. **长期**:战斗/结算 HUD 迁同一世界 prefab 族,头像/HP 达成真正单一视觉源。
