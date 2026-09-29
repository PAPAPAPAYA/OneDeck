# Plan: 卡组计数器 HP 样式化 — 双标签大字占用 + 小字总量 (2026-09-29)

- **Date**: 2026-09-29
- **Status**: IMPLEMENTED 2026-09-29（本会话实施；EditMode 664/663/1 基线零漂移。注意：**首跑绿是旧程序集假绿**——refresh_unity `refresh_triggered:false` 未扫盘 + 未聚焦延迟，run_tests 664 全绿但程序集 mtime 未动；`AssetDatabase.Refresh()` + `CompilationPipeline.RequestScriptCompilation()` 强制真实编译（其间抓到 CS0034，见 §5）后重跑为准。§4.2 Play 目测矩阵待用户）
- **Request (user, 2026-09-29)**: 卡组 panel 右上角的槽位计数器（现显示 `03/05`）改成类似 HP/HpMax 那样的显示：**占用数和 `/` 字号 10、总数数字号 5**。拍板三项：**保留两位零填充**（`03/05`，不去零）；~~两个字号挂 `ShopUXManager.panels` 调参~~（v1 拍板，v2 仍然有效）；**拆成两条独立 TMP 让用户自己调位置**（v2 追加，承载方式选 A = 运行时双标签 + 偏移调参，非 prefab 化）。
- **Reads**: `plans/plan-shop-sectionpanels-live-tuning-2026-09-22.md`（PanelsTuning 点读模式）, `plans/plan-shop-mirror-prefab-2026-09-24.md`（HpValue/HpMax 双标签参照与调优坑）, `plans/plan-shop-reroll-button-prefab-2026-09-29.md`（同文件并行会话，§5 排序约束）

## 1. Goal / non-goals

Goal:
1. DeckCounter 拆成**两条独立运行时 TMP**（HP pill HpValue/HpMax 同构）：
	- `DeckCounterUsed`：文本 `{used:00}/`，基础字号 = 新调参 `counterUsedFontSize`（默认 **10**）；
	- `DeckCounterTotal`：文本 `{total:00}`，字号 = 新调参 `counterTotalFontSize`（默认 **5**）。
2. 位置控制：两标签各挂一个**锚点相对偏移**调参（`counterUsedOffset` / `counterTotalOffset`，Vector2，默认 `(-1.0, 0)` / `(0, 0)`），锚点 = 面板右上 header 锚（现计数器定位点，随面板重排自动移动）——用户在 Play/Inspector 里改数字实时挪位置，这就是「自己调整位置」的承载。
3. 零填充保留 → 两段文本宽度恒定（各恒 2 字符 + `/`），固定偏移永远不错位，**无需**按 `preferredWidth` 动态重排（双标签方案成立的关键性质，见 §2.2）。

Non-goals:
- HP pill 那条 `HudCountBinding` 0.3s 数字滚动动画（计数器维持瞬时刷新，现状即如此）。
- DeckCounter.prefab 化（用户已选承载方式 A；reroll plan §6 的 chrome prefab 化后续项不含它）。
- `FormatSlotCount` 去零填充（拍板保留）。
- 基线自动对齐逻辑：两标签 Y 关系交给用户手调（与 HP pill 手调 1.24/-0.18 同一工作方式），代码不做基线计算。

## 2. Current state (verified facts, 2026-09-29)

### 2.1 DeckCounter 现行链（全部在 `ShopSectionPanels.cs`）

| 位置 | 现状 |
|---|---|
| `Build` (:247) | `_deckCounter = CreateHeader("DeckCounter", string.Empty, TextAlignmentOptions.Right, GameColorPalette.TooltipTextColor)` — 右对齐、pivot(1, 0.5)、白色（TooltipTextColor） |
| `CreateHeader` (:257-265) | 经 `ShopWorldWidgets.CreateWorldLabel`（:42-61）创建：`enableWordWrapping=false`、`overflowMode=Overflow`，fontSize = Build 时的 `headerFontSize` |
| `ApplySharedStyle` (:179-192) | 每次 RefreshLayout 只覆写 `_deckCounter.fontStyle`（synthetic bold，fontBold 调参），**不覆写 fontSize** → 现状 Play 调 `headerFontSize` 计数器字号不跟随（死值 quirk，本计划顺带修） |
| `RefreshCounter` (:392-401) | `used = CountUsedSlots(...)`（与 BuyFunc 门禁同源）/ `total = deckSize.value`，双 diff-guard 后写 `FormatSlotCount(used, total)` |
| `FormatSlotCount` (:212-216) | `used.ToString("00") + "/" + total.ToString("00")` → 等宽零填充 `03/05`，纯文本 |
| `FitPanel` (:328-372) | 计数器定位 `X = max.x - headerRightMargin, Y = headerY, Z = HeaderZ`（:361-364），右缘锚定、文本向左生长；`counter` 形参只有卡组 panel 传非空（:171），商店/升级传 null |
| 测试 | `ShopSectionPanelsTests.cs:79-82` 断言 `"03/05"`/`"12/12"`；`:112-115` 断言 `"01/03"`（2 个用例 3 处断言需同步改） |

### 2.2 HP pill 参照与本方案的映射

- `Tpl_HpPill.prefab` = 两条独立 TMP：`HpValue`（fontSize 10.306，格式 `{0}/`）+ `HpMax`（fontSize 5，格式 `{1}`），位置手调（页面实例 override 1.24/-0.18，09-28 调优踩过「显小」等坑）。
- v2 按用户拍板照抄这个**结构**（两条 TMP、大字带 `/`、小字纯数），但承载不同：panels 是运行时 `new GameObject`，没有 prefab transform 可拖，所以「手调位置」落成 **anchor + Vector2 偏移调参**（Play 实时生效），语义上等价于 HP pill 的 transform 手调。
- **零填充是双标签方案的承重墙**：deckSize 上限 16、used ≤ 16，两段恒 2 字符 → 每段渲染宽度恒定 → 固定偏移在任意数值组合下都不错位。若未来去零填充或上限超 3 位数，才需要引入 `preferredWidth` 动态重排（本计划明确不做）。

### 2.3 场景现值基线（GameScene.unity `ShopUXManager.panels` 块）

`headerFontSize: 6`、`fontBold: 1`、`headerRightMargin: 0.23`、`headerHeight: 0.8`。即现状计数器渲染 = 字号 6 + 合成加粗 + 白色。used 侧字号提到 10 后渲染高度约增大 67%，在 0.8 高的 header 带内可能显得顶满——字号与位置都已挂调参，Play 里自行微调，本计划不动布局常量。

## 3. 方案

### 3.1 `ShopUXManager.PanelsTuning` +4 字段（ShopUXManager.cs:94-106）

```csharp
[Tooltip("Deck counter used side (\"{used}/\") TMP point size — the label's base size")]
public float counterUsedFontSize = 10f;
[Tooltip("Deck counter total side TMP point size")]
public float counterTotalFontSize = 5f;
[Tooltip("Deck counter used label position, offset from the panel's top-right header anchor")]
public Vector2 counterUsedOffset = new Vector2(-1.0f, 0f);
[Tooltip("Deck counter total label position, offset from the panel's top-right header anchor")]
public Vector2 counterTotalOffset = Vector2.zero;
```

字段初始化器即拍板值（10/5）与起点偏移。**场景零改动**：GameScene 反序列化缺字段时按初始化器回填，下次保存自然落盘。偏移默认值是估摸起点（used 右缘 ≈ 锚点左侧 1.0 单位，让 `/` 落在 `05` 左边），Play 里调到间距舒服为止。

### 3.2 `ShopSectionPanels.cs` 改动

1. **常量池**（:42-44 池，manager 缺席 corner 用）：`CounterUsedFontSizeDefault = 10f`、`CounterTotalFontSizeDefault = 5f`、`static readonly Vector2 CounterUsedOffsetDefault = new(-1.0f, 0f)`、`static readonly Vector2 CounterTotalOffsetDefault = Vector2.zero`（Vector2 不能做 const）。
2. **字段**（:65）：`_deckCounter` 拆为 `_deckCounterUsed` + `_deckCounterTotal`（diff-guard 缓存 `_lastCounterUsed/_lastCounterTotal` 保留，语义不变）。
3. **Tuning helper 加 Vector2 重载**（:124-128 旁，同款点读 + fallback）：

```csharp
private static Vector2 TuningVec2(System.Func<ShopUXManager.PanelsTuning, Vector2> selector, Vector2 fallback)
{
	ShopUXManager ux = ShopUXManager.Instance;
	return ux != null && ux.panels != null ? selector(ux.panels) : fallback;
}
```

4. **`Build`**：`CreateHeader` 两次（同对齐/同色），各补一行创建即设字号（与 header「创建即设 + ApplySharedStyle 再设」同模式）。pivot 随 Right 对齐自动 = (1, 0.5)，两标签都向左生长。
5. **`FormatSlotCount` 拆成两个 static**（`FormatSlotCount` 删除，无其他调用方，测试同步改）：

```csharp
/// <summary>Deck counter used side: zero-padded two digits + trailing slash ("03/"),
/// rendered at the used label's base size (HpValue format convention).</summary>
public static string FormatCounterUsed(int used) => used.ToString("00") + "/";

/// <summary>Deck counter total side: zero-padded two digits ("05"), rendered at the
/// total label's smaller size (HpMax format convention).</summary>
public static string FormatCounterTotal(int total) => total.ToString("00");
```

6. **`RefreshCounter` 末行**改为写两条文本（`FormatCounterUsed(used)` / `FormatCounterTotal(total)`），diff-guard 判断不变。
7. **`FitPanel`**：`counter` 形参改双参 `counterUsed, counterTotal`（商店/升级传 `null, null`），计数器块改为：

```csharp
		if (counterUsed != null)
		{
			Vector3 anchor = new Vector3(max.x - headerRightMargin, headerY, HeaderZ);
			// Explicit Vector2 -> Vector3 cast: UnityEngine defines the conversion both ways,
			// so anchor + Vector2 is an ambiguous '+' (CS0034).
			counterUsed.transform.position = anchor + (Vector3)TuningVec2(t => t.counterUsedOffset, CounterUsedOffsetDefault);
			if (counterTotal != null)
				counterTotal.transform.position = anchor + (Vector3)TuningVec2(t => t.counterTotalOffset, CounterTotalOffsetDefault);
		}
```

（`SetActive` 同步两标签；偏移是 Vector2，z 落 HeaderZ。）
8. **`ApplySharedStyle`** 计数器块（现状 :191 一行）扩为两标签各设 `fontSize` + `fontStyle`，并在末尾把 `_lastCounterUsed/_lastCounterTotal` 复位 `int.MinValue`——强制下一次 `RefreshCounter` 重写文本，使 Play 改字号即时可见（RefreshLayout 先于 Refresh，`ShowIfActive` :137-143 顺序保证；重写成本每布局一次可忽略）。
9. 类顶部 summary 中 `(03/05)` 描述更新为双标签表述（注释级）。

### 3.3 测试更新（`ShopSectionPanelsTests.cs`）

```csharp
Assert.AreEqual("03/", ShopSectionPanels.FormatCounterUsed(3));
Assert.AreEqual("05", ShopSectionPanels.FormatCounterTotal(5));
Assert.AreEqual("12/", ShopSectionPanels.FormatCounterUsed(12));
Assert.AreEqual("12", ShopSectionPanels.FormatCounterTotal(12));
// occupancy 用例：
Assert.AreEqual("01/", ShopSectionPanels.FormatCounterUsed(ShopSectionPanels.CountUsedSlots(deck, false)));
Assert.AreEqual("03", ShopSectionPanels.FormatCounterTotal(3));
```

## 4. 验收

1. **EditMode 全量**：先 `SaveOpenScenes`（预批），`refresh_unity compile:request` + 确认 isCompiling=false 且程序集 mtime > 源文件 mtime（陈旧程序集坑），再 `run_tests`。预期基线 664/663/1（断言改写不加用例 → 零漂移）。
2. **Play 目测矩阵（用户验）**：
	- 进商店：卡组 panel 右上角出现两条标签 `03/`（大）与 `05`（小），默认偏移下 `/` 在 `05` 左侧、间距合理；白色、合成加粗。
	- **调位置**：Play 改 `panels.counterUsedOffset` / `counterTotalOffset` → 实时移动；买卡位卡（面板重排）后两标签仍相对锚点位置不变（跟随验证）。
	- **调字号**：Play 改两个 `counter*FontSize` → 实时生效；默认 Y=0 是垂直居中，两字号基线不齐属预期——把 total 的 `offset.y` 微调即可对齐基线（HP pill 同款手调工作流）。
	- 买/卖/重掷：两段数字即时更新，右缘锚点不动、向左生长。
	- `fontBold` 开关对两标签同时生效；HP pill 显示不受任何影响（两套系统无共享代码路径）。

## 5. 并行会话排序与风险

- **排序约束（硬）**：并行 claim `20260929-101200-reroll-button-prefab.md` 正在改 `ShopSectionPanels.cs`（ApplySharedStyle trim、**FitPanel** 宽度行）与 `ShopUXManager.cs`（PanelsTuning 删 5 死字段）。本计划触点（FitPanel 计数器块、ApplySharedStyle 计数器块、PanelsTuning 追加字段）与它同文件同方法，**必须在 reroll 会话落地（提交）后再实施**，或并入同一批实施；不得并行编辑。
- **GameScene 无冲突**：本计划零场景改动，不存在与 reroll 会话场景保存互踩；唯一交点 = ShopUXManager 组件块的新序列化字段，由 Unity 反序列化回填，无需手工 YAML。
- **字号 6→10 视觉余量**：used 侧在 headerHeight 0.8 带内变高（§2.3），如显顶满优先调 `counterUsedFontSize` / `offset.y` 或 `headerHeight`，不改布局代码。
- **未来去零填充的代价**（记录，非本计划）：两段宽度不再恒定，固定偏移会漂，需引入 `preferredWidth` 动态重排——本计划的「零填充拍板 + 恒宽性质」挡住这个复杂度。
- **实施纪要（2026-09-29）**：①`anchor + Vector2` 触发 CS0034（UnityEngine 的 Vector2↔Vector3 双向隐式转换使 `+` 二义），显式 `(Vector3)` 转型修复；②陈旧程序集假绿判例升级：`refresh_unity` 返回 `refresh_triggered:false` 且 `compile_requested:true` 时请求可能不落地（未聚焦），Editor.log 尾部 grep `error CS` 是比 read_console 更稳的错误源；修法 = `execute_code` 里直接 `AssetDatabase.Refresh()` + `CompilationPipeline.RequestScriptCompilation()`，然后必须复核程序集 mtime 与符号存在性再跑测试。
