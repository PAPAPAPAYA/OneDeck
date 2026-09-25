# Plan: Palette 颜色解耦 — SlotRecess 实时化 + Chip/Button/Tooltip 分色

- 日期: 2026-09-25
- 状态: **已实施未提交**(2026-09-25,两案全落地 + 实施勘误 2 条见 §6);EditMode 664/663/1 绿,Play 目检待用户
- 范围: GameColorPalette 新增槽位 / PaletteTint 枚举追加 / 3 个 prefab 换绑 / 2 处代码读点
- 前置调查: 2026-09-25 会话(palette 链路、PaletteTint 槽位分布、EmptyCardSpace 烘焙约定均已逐一验证)

## 0. 背景:四处颜色耦合(全部实证)

| # | 耦合 | 证据 |
|---|---|---|
| C1 | EmptyCardSpace 凹陷空卡位不读 palette:`PhysicalCardFace` 的 m_Color 是 09-17 烘焙死值(=SlotRecess.asset),`CardPhysObjScript.ApplyColor`(`:1320-1327`)对占位卡早退("keep their baked prefab colors") | 改 SlotRecess.asset 无任何效果 |
| C2 | chip 底板/文字与 hover tooltip 共槽:`Tpl_Chip` Panel=TooltipBg、Label=TooltipText;`CardTagTooltip.cs:169/181` 读同两槽 | 改 tooltip 色会连带 chip |
| C3 | chip 文字与按钮 Face 共资产:`tooltipText` 与 `ownerCardColor` 两个字段都指 **GreyWhite.asset** | 改按钮脸=改 tooltip 字=改 chip 字 |

> §0 勘误(实施期运行时核实):C3 的"共资产"实际是 `tooltipText` → **GreyWhite 2.asset**(0.749 暖灰),`ownerCardColor` → GreyWhite.asset(0.820)——两者并非同资产,chip 文字与按钮脸的耦合只在 chip 侧槽位,不在资产层。真正的三槽共享资产是 **Navy Alpha**:`tooltipBg` = `shopPanelBg` = `cardShadow` 全指它(0.102,0.188,0.216,0.502)。方案结论不受影响(chip 仍需从 tooltip 槽位解耦),仅背景事实修正,详见 §6。
| C4 | 按钮脸三处读点分散:顶栏按钮(prefab Tpl_WorldButton Face=OwnerCard)、重掷按钮(`ShopWorldWidgets.cs:113` 代码读)、价格按钮(`ShopCardView.cs:195` 代码读) | 只解耦 prefab 会漏掉两个运行时按钮 |

用户裁定(2026-09-25 AskUserQuestion):
- **裁定 1**:7 个 chip 底板统一成半透明那种(即 ChipBg 初始值 = ShopPanelBg 现值;Money/Income 从实心 Navy 变半透明是**接受的视觉变化**)。
- **裁定 2**:按钮 Face 一起解耦(卡面 ownerCardColor 不动,只动按钮绑点/读点)。

## 1. Plan 1 — SlotRecess 实时化(EmptyCardSpace 跟随 palette)

改动:
1. `PaletteTint.cs`:`Slot` 枚举**末尾**追加 `SlotRecess`(枚举按 int 序列化,append-only 不动已有槽位值,与 CardType 同纪律);`Resolve` 加 `case Slot.SlotRecess: return GameColorPalette.SlotRecessColor;`(静态读点已存在,`GameColorPalette.cs:126`)。
2. 编辑器操作:prefab stage 打开 `Assets/Prefabs/UXPrototype/EmptyCardSpace.prefab` → `PhysicalCardFace` 子物体挂 `PaletteTint`,slot=SlotRecess。ExecuteAlways 即时上色;原烘焙 m_Color 沦为初始值(每次 OnEnable 被 Apply 覆盖,无害)。

安全性:
- `ApplyColor` 早退保留不动 — 占位卡脸色归属权移交 PaletteTint,两个涂色者不打架。
- EmptyCardSpace 仅作空卡位占位使用(grep 证实:只有 ShopUXManager.SpawnEmptySlots 引用,无"占位转真卡"流程)。
- 零每帧开销(事件驱动)。

效果:改 `SlotRecess.asset` 的 value → 场景内已生成实例实时变色(编辑/Play 模式均可,走 palette Changed 广播)。

## 2. Plan 2 — Chip / Button / Tooltip 分色

### 2.1 新资产(3 个,初值=现状,除裁定 1 的统一变化)

| 资产(Assets/SORefs/Colors/) | 初值 (r,g,b,a) | 来源 |
|---|---|---|
| `ChipBg.asset` | (0.1020, 0.1882, 0.2157, **0.5020**) | =ShopPanelBg 现值(裁定 1:半透明) |
| ChipText.asset | **(0.7490, 0.7373, 0.7098, 1)** | =改动前 chip 文字实值(见 §6 勘误 2;非 GreyWhite 值) |
| `ButtonFace.asset` | (0.8196, 0.8039, 0.7843, 1) | =GreyWhite 现值 |

### 2.2 GameColorPalette 新增(代码)

新 `[Header("Shop Top Bar Widgets")]` 组 + 字段 `chipBg` / `chipText` / `buttonFace`(引用上表资产)+ 静态读点 `ChipBgColor` / `ChipTextColor` / `ButtonFaceColor`(照 `:100-126` 现有模式,含 null 兜底)。字段落 SO 后在 `Assets/Resources/GameColorPalette.asset` 上接线。

### 2.3 PaletteTint 枚举追加(代码,与 Plan 1 合并一次编辑)

`Slot` 末尾追加 `SlotRecess`, `ChipBg`, `ChipText`, `ButtonFace` 四项 + 对应 Resolve case。

### 2.4 Prefab 换绑(编辑器操作,无代码)

| Prefab | 操作 |
|---|---|
| `Tpl_Chip.prefab` | Panel 的 PaletteTint slot → `ChipBg`;Label → `ChipText` |
| 5 个信息 chip 变体(Rarity×3/Wins/Hearts) | **删除** Panel slot 的 override(现值 0=ShopPanelBg),回落继承模板 ChipBg。实施时须先确认 m_Modifications 的 target 是 Panel 的 PaletteTint 实例 |
| `Tpl_WorldButton.prefab` | Face 的 slot → `ButtonFace`(Shadow=CardShadow、Label=OwnerText **不动**) |
| hover tooltip | **不动**(继续 TooltipBg/TooltipText = Navy/GreyWhite) |

### 2.5 代码读点换绑(2 行)

- `ShopWorldWidgets.cs:113`:`face.color = GameColorPalette.OwnerCardColor` → `ButtonFaceColor`(重掷按钮)。
- `ShopCardView.cs:195`:`faceSr.color = GameColorPalette.OwnerCardColor` → `ButtonFaceColor`(价格按钮)。

### 2.6 明确不动清单

- `ownerCardColor`(GreyWhite.asset):己方卡面/卡背(`CardPhysObjScript`)、ResultStatsPanel 阵营色 — 语义是"卡",继续与 tooltip 文字共享 GreyWhite,本计划不拆。
- 按钮文字(OwnerText → Navy.asset)、按钮阴影(CardShadow → UIShadow.asset):不在本次诉求内。
- 面板底 ShopPanelBg、面板头/计数器 TooltipText:`ShopWorldWidgets`/`ShopSectionPanels` 代码读点不变(chip 换绑后 ShopPanelBg 只剩面板底一个消费者)。
- EditMode 测试 `GameColorPaletteWiringTests`:现有断言不受影响;实施时可顺带补 3 条新字段 wiring 断言(可选)。

## 3. 实施顺序

1. `PaletteTint.cs`(枚举 + 4 个 case)
2. 3 个 ColorSO 资产创建 + `GameColorPalette.cs`(字段/静态)+ palette SO 接线
3. `ShopWorldWidgets.cs:113` / `ShopCardView.cs:195` 换 ButtonFaceColor
4. Prefab stage:EmptyCardSpace 挂 tint → Tpl_Chip 换绑 → 删 5 变体 override → Tpl_WorldButton Face 换绑
5. 全量验证(下节)→ RegressionChecklist 追加行

## 4. 验收

- 编译 0 错(assembly mtime > 源码);EditMode 全量 = 664/663/1 基线。
- Play 探针矩阵(改各资产 value 后读回 renderer 颜色):
  - ChipBg 变 → 7 chip 底全变,tooltip/面板底不变;
  - ChipText 变 → 7 chip 字全变,tooltip 字不变;
  - ButtonFace 变 → 顶栏+重掷+价格按钮脸全变,卡面/tooltip 不变;
  - SlotRecess 变 → 空卡位实时变;
  - Navy/GreyWhite 变 → hover tooltip 变、chip/按钮**不**变(解耦回归项)。
- 视觉:Money/Income chip 变半透明(裁定 1 的预期变化,需用户 Play 目检)。

## 5. 风险

- 删变体 override 时 target 选错(可能删到 Label 的绑定)— 实施时逐个核对 m_Modifications 的 propertyPath 与目标 transform。
- 枚举若插中间会静默错位所有已序列化 slot — 只允许末尾追加。
- `ResizePriceButtonFace`(`ShopCardView.cs:137`)等运行时重涂路径若另有 OwnerCardColor 读取需一并排查(当前 grep 仅 :195 一处)。

## 6. 实施勘误(2026-09-25 实测后修正)

1. **PaletteTint 补订阅 `ColorSO.Changed`**:原设计只挂 `GameColorPalette.Changed`(palette 资产被重编辑才触发),改 ColorSO 的 **value** 不会实时传导。实施补上 per-asset 订阅(`OnColorSoChanged` → `Apply`,与 HPNumericDisplay 等 HUD 预览同模式),Play 探针证实 Inspector 式改值当帧生效。运行时构建的按钮(重掷/价格)脸仍只在下次重建时刷新(无 PaletteTint,接受)。
2. **chip 文字初值勘误**:`tooltipText` 实际指向 GreyWhite 2.asset(0.749 暖灰,见 §0 勘误),ChipText.asset 初值已修为 (0.749, 0.737, 0.710, 1) = 改动前 chip 文字实值,采纳瞬间零视觉变化。
3. **真实资产接线(运行时核实)**:`tooltipBg` = `shopPanelBg` = `cardShadow` → **Navy Alpha**(0.102,0.188,0.216,0.502,三槽共享一资产);`tooltipText` → GreyWhite 2;`ownerCardColor` → GreyWhite;`ownerTextColor` → Navy(实心)。后续若解耦 tooltip/面板底/按钮阴影,注意它们目前同资产。
