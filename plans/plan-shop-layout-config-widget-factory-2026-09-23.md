# Plan: 商店页面布局配置统一 (ShopLayoutConfigSO) + 世界控件工厂 (ShopWorldWidgets) (2026-09-23)

- **Date**: 2026-09-23
- **Status**: IMPLEMENTED 2026-09-23 (steps 1-9 + 12 code-complete; step 10 partial: offline `dotnet build` Assembly-CSharp 0 errors / 19 pre-existing CS0618 warnings, no new ones; **step 10 EditMode suite + step 11 基线对照/Play 待 Unity 会话执行** — implementing session had no Unity MCP tools. New files CRLF+Tab; csproj 临时增删仅用于离线编译验证,Unity 刷新后会重生成)
- **Request (user, 2026-09-23)**: 商店页实现复杂、多模块、每个元素的位置/大小不方便调。评估结论(2026-09-23 会话):卡片区与面板已可实时调(ShopUXManager 场景字段 + PanelsTuning),**顶栏整体(chips ×7 / 两颗按钮 / band / avatar+HP 镜像)是代码只读**。本计划落两步收敛:
  - 【1】ShopLayoutConfigSO — 顶栏/镜像的全部布局值收进一份 Resources SO,Play 模式 Inspector 实时调
  - 【2】ShopWorldWidgets — 按钮/chip/label/sliced 拼装配方去重(ShopChrome 与 ShopSectionPanels 各持一份私有拷贝)
- **Context note**: 2026-09-23 08:55-08:57(另一会话)已把 `ShopChromeConfigSO`(仅离开商店按钮两个旋钮)接线进 ShopChrome——Build 读取(:227-229)+ `ApplyExitButtonPlacement` 实时路径(:294-301)+ SO OnValidate 回写。本计划【1】把它吸收进 ShopLayoutConfigSO 后删除其类与资产;实施前须与该会话确认已收尾。
- **Reads**: `plans/plan-shop-sectionpanels-live-tuning-2026-09-22.md` (Tuning 点读 + ApplySharedStyle 重应用先例), `plans/plan-shop-topbar-world-scroll-2026-09-21.md` (band 一次捕获规则), `docs/ShopSystems.md`

## 1. Goal / non-goals

Goal:
1. 顶栏档从「改代码」翻成「Inspector 实时调」:机制与 09-22 panels 调参同构(点读解析 + OnValidate 实时回写),整页只剩一份布局 SO 要开。
2. 世界控件拼装配方收敛为单一工厂,消灭 ShopChrome/ShopSectionPanels 两份重复(含 label 创建的三处近似重复)。
3. **默认值零视觉位移**:SO 缺失(未建资产/headless/测试)时回退现有常量,常量数值原样保留——纯重构层,行为层只新增「可调」。

Non-goals:
- 不动货架/卡组网格场景字段(`xOffset`/`yOffset`/`objPerRow`/`shopItemPos`/`playerDeckPos`)——已可用、用户已习惯。
- 不迁移 `PanelsTuning`(09-22 刚落地、Play 检查未做;列为后续可选项,见 §7.4)。
- 不改坐标语义(视口 vs 世界单位不互换;纵横比 caveat 原样保留,见 §7.2)。
- 不动颜色(仍走 GameColorPalette)、字体 fallback 链、转场/滚动/输入门逻辑。

## 2. Current state (verified facts, 2026-09-23)

### 2.1 值的三层分布(本次要收敛的「顶栏档」)

| 层 | 位置 | 消费方 | 现状 |
|---|---|---|---|
| 顶栏行位/镜像位/镜像缩放 | `ShopTopBarLayout.cs:29-38,46-47` 静态量:PlayerIconViewport(0.292,0.956)、HpDisplayViewport(0.437,0.959)、MoneyChipViewport(0.654,0.959)、OddsRowViewportY 0.883、StatsRowViewportY 0.810、HudColumnViewportX 0.252、PlayerIconShopScale 0.28、HpDisplayShopScale 0.5 | ShopChrome(:219-222)、ShopPageHud(:104,:112,:124,:157)、CombatIconPresenter(:150-152)、HPNumericDisplayHorizontal(:447-449,:460-462) | 代码只读 |
| chrome 自身尺寸/间距 | `ShopChrome.cs:49-71` 20 个 const:BandHeight 2.6(public)、CameraForwardOffset 2、BandInsetFromTop 0.5(public)、EdgeMargin 0.35、ChipSpacing 0.25、ChipWidth 2.2、ChipHeight 0.5、SmallChipWidth 1.7、SmallChipFontSize 1.9、MoneyChipWidth 2.4、MoneyChipHeight 0.62、MoneyChipFontSize 2.8、OptionsButtonWidth 0.72、ButtonHeight 0.64、ExitButtonWidth 2.6、ChipFontSize 2.2、ButtonFontSize 2.4、ExitButtonFontSize 2.8、RestShadowUnits 0.05、DenyShiftUnits 0.075 | ShopChrome.Build | 代码只读 |
| 退出按钮例外 | `ShopChromeConfigSO`(Resources/ShopChromeConfig):exitButtonOffsetX=0、exitButtonViewportY=0.956 | ShopChrome :227-229(Build)、:294-301(实时) | 已实时可调(今日接线) |

### 2.2 关键机制事实

- **Build 一次捕获**:chrome 根位置与 `_bandBottomWorldY` 在 Build 写一次(:203-208,页面内容不随滚动);`_halfW`/`_orthoSize` 已为 ApplyExitButtonPlacement 建立捕获(:93-96,:194-195),但**相机 rig 的 XYZ 尚未捕获**——实时重摆根需要它(§3.4)。
- **Canvas 侧读点是「每次摆位时」**:CombatIconPresenter/HPNumericDisplayHorizontal 在每次 phase 摆位/hide-park 时读 ShopTopBarLayout(CiP :150-152;HPN :447-462)。settled shop 里 canvas 件隐藏,所以**镜像值改 SO 后 canvas 件自动在下一次 handoff 生效,无需实时回写**。
- **镜像世界件是 Build 一次烘焙**:ShopPageHud.BuildAvatar/BuildHpPill 把 canvas 子件的 anchoredPosition × unit 烘进 localPosition(ShopPageHud :122-206),scale/viewport 改动无法原地重用烘焙值 → 实时路径 = 重建(§3.4)。OnDestroy 清 static(:288-291);count tween 有 OnDisable 清理(:281-286)。
- **货架净空契约**:`CheckShelfClearance` 硬编码 `2.5f` 卡面下沿系数(ShopChrome :182),与 `ShopSectionPanels.FaceBelowHalfHeightFactor`(=2.5,:50)同源不同处。
- **配方重复**:世界按钮配方(Shadow z+0.04 / Face z+0.02 under Visual / BoxCollider2D / PhysButton 七连设置)在 ShopChrome.CreateButton(:303-336)与 ShopSectionPanels.CreateRerollButton(:278-312)两份;label 创建三处(ShopChrome.CreateLabel :360-375、ShopSectionPanels.CreateHeader :257-274、CreateButtonLabel :314-331);sliced 精灵 SetupSliced 两份。
- **测试面**:ShopPageHudTests 只测纯静态(CanvasPxToWorld/SlicedWorldSize;PlayerIconShopScale 仅出现在注释 :24);ShopSectionPanelsTests 只调参数化静态。**两个测试文件零引用将被移动的成员 API**——前提是 §3.3 的「保留属性名」路线。

## 3. Design

### 3.1 默认值策略:常量留原地作 fallback,SO 是唯一覆盖层

不走「把常量搬进 SO」而走「SO 字段初始值引用原常量」:

- `ShopChrome` 与 `ShopTopBarLayout` 的现有常量**原地保留**(数值与注释不动),全部转为 fallback;
- `ShopLayoutConfigSO` 字段初始化器引用它们(`public float bandHeight = ShopChrome.BandHeight;`);
- 读取一律经解析器 `V/V2`(§3.2),SO 存在读 SO,缺失读 fallback 常量。

收益:默认值单一源不复制(不会漂移)、默认注释(如 avatar 装饰件 0.28 上限的推导,ShopTopBarLayout :42-45)留在原地、headless/测试零风险。这与 ShopSectionPanels 的 `Tuning(selector, fallbackConst)`(:118-122)同构,是本仓库已验证的模式。

### 3.2 ShopLayoutConfigSO(新文件 `Assets/Scripts/SOScripts/ShopLayoutConfigSO.cs`)

- ScriptableObject;lazy Resources 单例 `Me`(`Resources.Load<ShopLayoutConfigSO>("ShopLayoutConfig")`,`_loadAttempted` 防重试;**资产缺失合法,返回 null 走 fallback**,照抄 ShopChromeConfigSO :22-37)。
- 解析器(静态,SO 上):

```csharp
public static float V(System.Func<ShopLayoutConfigSO, float> selector, float fallback)
	public static Vector2 V2(System.Func<ShopLayoutConfigSO, Vector2> selector, Vector2 fallback)
```

- 字段分组([Header],完整清单见 §4):Band & Root / Row 0 / Rows 1-2 / Avatar & HP Mirror / Button Feel。Row 0 含被吸收的 `exitButtonOffsetX`/`exitButtonViewportY`。
- `OnValidate()`(照抄 ShopChromeConfigSO :39-44 的实时路径):

```csharp
private void OnValidate()
{
	if (ShopChrome.Instance != null) ShopChrome.Instance.ApplyLayout();
	if (ShopPageHud.Instance != null) ShopPageHud.Instance.ApplyMirrorLayout();
}
```

### 3.3 读取路径(各消费方改法)

| 消费方 | 改法 |
|---|---|
| `ShopTopBarLayout` | 数据静态量 → **同名解析属性**(API 不变,消费方零改动):`public static Vector2 PlayerIconViewport => ShopLayoutConfigSO.V2(c => c.playerIconViewport, PlayerIconViewportDefault);` 原静态量改名 `...Default` 留作 fallback。换算助手(ViewportToCanvasAnchored / ViewportToChromeLocal ×2)不动,但其内部引用的 `ShopChrome.BandInsetFromTop` 改读 `V(c => c.bandInsetFromTop, ShopChrome.BandInsetFromTop)` |
| `ShopChrome` | Build 全部落地点改读解析器(band/根位/行位/列 X/尺寸/字号);`ApplyExitButtonPlacement` 泛化为 `ApplyLayout`(§3.4);`ExitAnchorX`/`ExitLocalY` 改读 SO 旋钮 |
| `ShopPageHud` | 不改读取点(仍读 ShopTopBarLayout 属性);只加实时重建入口(§3.4) |
| `CombatIconPresenter` / `HPNumericDisplayHorizontal` | **零改动**(属性同名,自动经 SO) |
| `ShopChromeConfigSO` | 删除(§3.5) |

### 3.4 实时回写路径

**ShopChrome.ApplyLayout()**(替代 ApplyExitButtonPlacement;全部用 Build 捕获几何,不读活相机,滚动安全——沿用 :288-293 注释的既定原则):

1. Build 增捕 rig 基准 `_rigPosX/_rigPosY/_rigPosZ`(Build 时相机即 shop base,与现 band 公式同值);
2. ApplyLayout 重写:根位置(`_rigPosY + _orthoSize - bandInsetFromTop` / `_rigPosZ + cameraForwardOffset`)、`_bandBottomWorldY`(= `_rigPosY + _orthoSize - bandHeight`)、两颗按钮(位置+`ConfigureWorldFaceSize`+label 字号/rect)、七颗 chip(位置+`panel.size`+label 字号/rect,含行内 `chipSpacing` 推进与 `columnX` 对齐——即 Build :215-269 的落段原式重跑,输入全换成解析值);
3. 中途旅行(IsTransitioning)时照常执行:写的是页面局部坐标,chrome 根此时隐藏,落地即见新值。

**ShopPageHud.ApplyMirrorLayout()**(镜像实时路径 = 重建):

1. `PhaseTransitionDriver.IsTransitioning` → 置 `_mirrorDirty` 直接返回(旅行中 canvas 源件的 anchoredPosition 正被 tween,读了会烘进错误偏移);
2. 否则立即重建:销毁 Avatar/HpPill 两个子根 → 重跑 Build()(源件重找、unit 按新 scale 值重导)→ `WriteHpText()` 保值回写(`_shownHp/_shownHpMax` 字段在重建中存活);
3. `_mirrorDirty` 在 `Update()` 消费(chrome 隐藏时 Update 不跑,标志自然等到下次激活)——兜住「战斗中改 SO」的场景。

**Canvas 件**:无回写代码;下次摆位(进/出店 handoff、hide-park)自动取新值(§2.2)。

### 3.5 吸收并删除 ShopChromeConfigSO

1. 实施第一步先读 `Assets/Resources/ShopChromeConfig.asset` 现值(若已被调过非默认,抄进新资产对应字段);
2. ShopChrome 删 `ShopChromeConfigSO` 引用,exit 旋钮改读 ShopLayoutConfigSO;
3. 删 `Assets/Scripts/SOScripts/ShopChromeConfigSO.cs`(+.meta)与 `Assets/Resources/ShopChromeConfig.asset`(+.meta)。

### 3.6 ShopWorldWidgets 工厂(新文件 `Assets/Scripts/UXPrototype/ShopWorldWidgets.cs`,静态类)

```csharp
public static class ShopWorldWidgets
{
	// sliced 精灵背景(面板/chip 面/按钮 Shadow+Face 共用;收编两处 SetupSliced)
	public static SpriteRenderer CreateSliced(Transform parent, string name, Sprite sprite,
		Color color, Vector3 localPosition, Vector2 size)

	// 世界 TMP label(收编 ShopChrome.CreateLabel / ShopSectionPanels.CreateHeader + CreateButtonLabel)
	public static TextMeshPro CreateWorldLabel(Transform parent, string name, TMP_FontAsset font,
		string text, float fontSize, Color color, TextAlignmentOptions alignment,
		Vector2 size, Vector2 pivot, Vector3 localPosition)

	// chip(原 ShopChrome.CreateChip 整体迁入;label rect = (width-0.15, height),根 z=0.02,
	// darkPanel ? ShopPanelBgColor : TooltipBgColor,label TooltipTextColor)
	public static HudChip CreateChip(Transform parent, string name, Sprite sprite, TMP_FontAsset font,
		float x, float y, float width, float height, float fontSize, bool darkPanel)

	// 世界按钮(收编 ShopChrome.CreateButton + ShopSectionPanels.CreateRerollButton;
	// BoxCollider2D + Visual/Shadow z+0.04 + Face z+0.02 + label rect=(width-0.2, height)
	// + PhysButton 七连设置,face/collider = (width, height))
	public static PhysButton CreateWorldButton(Transform parent, string name, TMP_FontAsset font,
		Sprite sprite, string label, float x, float y, float width, float height, float fontSize,
		float restShadow, float denyShift, out TextMeshPro labelTmp)
}
```

迁移规则:
- **字节级不变量**(两边现配方完全一致,工厂内固化):层次名 Root/Visual/Shadow/Face/Label;z 偏移(按钮 Shadow +0.04、Face +0.02;chip 根 0.02);颜色(CardShadowColor / OwnerCardColor / OwnerTextColor);`hoverLift = restShadow`(两处现调用都是同值);TMP 共性(不换行、Overflow、Center)。
- 调用点改造:ShopChrome.CreateButton/CreateChip/CreateLabel/SetupSliced 删除,Build 改调工厂(font/sprite 仍来自 `_font`/`_sprite`);ShopSectionPanels.CreateRerollButton/CreateButtonLabel/CreateHeader/CreatePanel 删除,改调工厂(SetWorldAction、`_rerollLabel` 赋值等实例接线留在调用点);panels 的 Tuning 点读时机不变(工厂只收已解析值)。
- z 语义:panels 的 PanelZ/HeaderZ 经 localPosition 参数传入,不进工厂。

### 3.7 净空系数统一(顺手)

`ShopChrome.CheckShelfClearance` 的 `2.5f`(:182)改引 `ShopSectionPanels.FaceBelowHalfHeightFactor`(private const → public)。两处同一事实,消一处硬编码。

## 4. Field inventory(ShopLayoutConfigSO 条目)

默认列 = 现值;「实时」列:A=ApplyLayout 立即,M=镜像重建立即,C=下次摆位自动(canvas 件)。

### Band & Root
| 字段 | 默认 | 支配 | 实时 |
|---|---|---|---|
| bandHeight | 2.6 | 预留净空带高、`_bandBottomWorldY`、货架净空上限 | A |
| bandInsetFromTop | 0.5 | chrome 根距视口顶下移量 | A |
| cameraForwardOffset | 2.0 | chrome z 前移 | A |

### Row 0
| 字段 | 默认 | 支配 | 实时 |
|---|---|---|---|
| exitButtonViewportY | 0.956 | 离开商店按钮 Y(自 ShopChromeConfigSO 吸收) | A |
| exitButtonOffsetX | 0 | 离开商店按钮 X 偏移(自 ShopChromeConfigSO 吸收) | A |
| exitButtonWidth | 2.6 | 离开商店面宽+label rect | A |
| exitButtonFontSize | 2.8 | 离开商店字号 | A |
| buttonHeight | 0.64 | 两颗按钮面高+label rect | A |
| buttonFontSize | 2.4 | ❚❚ 字号 | A |
| optionsButtonWidth | 0.72 | ❚❚ 面宽+右锚位 | A |
| worldEdgeMargin | 0.35 | exit 左锚 / options 右锚的边缘距 | A |
| moneyChipViewport | (0.654, 0.959) | 金钱 chip 中心 | A |
| moneyChipWidth / Height / FontSize | 2.4 / 0.62 / 2.8 | 金钱 chip | A |

### Rows 1-2
| 字段 | 默认 | 支配 | 实时 |
|---|---|---|---|
| hudColumnViewportX | 0.252 | 两行 chip 左对齐列(躲重掷按钮的既定约束随值迁移) | A |
| oddsRowViewportY / statsRowViewportY | 0.883 / 0.810 | 稀有度行 / 胜负行 Y | A |
| chipSpacing | 0.25 | 行内 chip 间距 | A |
| smallChipWidth / chipHeight / smallChipFontSize | 1.7 / 0.5 / 1.9 | ✦/🜲/♥ 五颗小 chip | A |
| chipWidth / chipFontSize | 2.2 / 2.2 | 收入 chip(大号) | A |

### Avatar & HP Mirror(canvas 共享)
| 字段 | 默认 | 支配 | 实时 |
|---|---|---|---|
| playerIconViewport | (0.292, 0.956) | 世界镜像 + canvas icon 中心 | M + C |
| hpDisplayViewport | (0.437, 0.959) | 世界镜像 + canvas 药丸中心 | M + C |
| playerIconShopScale | 0.28 | 商店内 icon 缩放(上限受装饰件 222×306 约束,默认注释留原地) | M + C |
| hpDisplayShopScale | 0.5 | 商店内药丸缩放 | M + C |

### Button Feel
| 字段 | 默认 | 支配 | 实时 |
|---|---|---|---|
| restShadowUnits | 0.05 | 两颗按钮 rest/hover 影深 | A |
| denyShiftUnits | 0.075 | 两颗按钮 deny 抖动 | A |

共 30 字段。不进 SO:卡面几何系数(FaceHalfWidth/Above/Below,卡 prefab 烘焙事实非布局口味)、panels 13 参(§7.4)、网格场景字段。

## 5. Implementation checklist

1. **前置**:读 `ShopChromeConfig.asset` 现值并记录(非默认则迁移);确认 08:5x 接线会话已收尾。
2. 新建 `ShopLayoutConfigSO.cs`(+Resources 资产,Unity 内建以生成 .meta;**CRLF+Tab**)。
3. `ShopTopBarLayout`:静态量 → `...Default` fallback + 同名解析属性;`ViewportToChromeLocal*` 的 BandInsetFromTop 改经解析器。
4. `ShopChrome`:Build 增捕 `_rigPos*`;Build 落段改读解析器;`ApplyExitButtonPlacement` → `ApplyLayout`;删 ShopChromeConfigSO 引用。
5. `ShopPageHud`:`ApplyMirrorLayout` + `_mirrorDirty`(Update 消费)。
6. `ShopLayoutConfigSO.OnValidate` → chrome.ApplyLayout + pageHud.ApplyMirrorLayout(§3.2)。
7. 迁值后删 `ShopChromeConfigSO.cs` + `ShopChromeConfig.asset`(+.meta ×2)。
8. 新建 `ShopWorldWidgets.cs`;迁移 ShopChrome 与 ShopSectionPanels 四组配方调用点;删私有配方(§3.6)。
9. `CheckShelfClearance` 2.5f → `ShopSectionPanels.FaceBelowHalfHeightFactor`(转 public)。
10. `refresh_unity`(compile: request)→ 0 错 → `EditorSceneManager.SaveOpenScenes()` → EditMode 全量(init_timeout 180000;基线 671/670/1 Ignore)。
11. **基线对照**(§6.1)→ Play 验证(用户,视觉诊断惯例不主动进 Play)。
12. 文档:`docs/ShopSystems.md` 顶栏节补 SO 一段;AGENTS.md 商店 bullet 加一句(改后 `wc -c` ≤ 32KB);`docs/RegressionChecklist.md` 追加行。

## 6. Verification

### 6.1 默认值零位移(硬门槛)
实施前在 Play(进店、scroll 0)用 execute_code 导出基线 JSON 到 `tools/outputs/`:chrome 根 position + 每颗 chip/按钮 localPosition + SpriteRenderer.size + label fontSize + 三面板 size/中心 + 镜像 Avatar/HpPill localPosition。实施后同探针重导,diff 必须为空(浮点精确相等)。

### 6.2 实时调参矩阵(Play,用户验)
| 编辑 SO 字段 | 预期 |
|---|---|
| exitButtonOffsetX / ViewportY | 按钮当帧移动,滚动到页底再回仍对(页面坐标) |
| moneyChip* / smallChip* / chipSpacing / 列与行 Y | 对应 chip 当帧变 |
| bandHeight / bandInsetFromTop | 根与 band 上限当帧变;触一次 reroll 看 CheckShelfClearance 用新上限 |
| playerIcon* / hpDisplay* | 世界镜像当帧重建;离开商店→战斗,canvas 件按新值 glide |
| 战斗中编辑 mirror 组 | 旅行不重建(无畸形镜像),回店落地生效 |

### 6.3 回归
进出店循环(Shop→Combat→Result→Shop)×2;reroll/买/卖一次;EditMode 全量绿;console 无新告警(CheckShelfClearance 按新值正常判)。

## 7. Risks / notes

1. **并行会话**:08:55-08:57 的 ShopChromeConfigSO 接线非本会话所做;步骤 1/7 前须确认那边的 Play 验证与提交状态,避免删其未收尾资产。
2. **纵横比 caveat 不变**:chips/按钮是世界单位、行位是视口比例,不同 aspect 下相对关系仍会偏(ShopTopBarLayout 头注释既有限定)。本次只让值可调,不改语义;若要彻底解决须把 X 全改视口分数(行为变化,另立计划)。
3. **OnValidate 抖动**:拖数值时逐帧触发;ApplyLayout 纯 transform 写(廉价),镜像重建走 `_mirrorDirty` 每帧至多一次。
4. **PanelsTuning 迁移(后续可选)**:panels 13 参可平移进本 SO(解析器同构,ShopSectionPanels.Tuning 换源即可),让「整页一份 SO」成立;但因 09-22 刚落地且 Play 未验,本计划不动。用户拍板后另起小步。
5. **新文件格式**:两个新 .cs 均须 CRLF + Tab(Write 工具出 LF,写后 sed 转换);Plan 文件同。
6. **EditMode**:测试零 API 引用(§2.2),预期全量绿;若 ShopPageHudTests 后续新增 scale 断言,读 fallback 常量而非 SO(Me 单例在测试域可能已被加载)。
