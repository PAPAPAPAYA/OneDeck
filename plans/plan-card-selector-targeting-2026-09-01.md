# 目标选取统一化：共享 CardSelector 实施计划

日期：2026-09-01
状态：**已全部实施完毕（Step 0-5 完成，2026-10-06 收官）**。Step 0 parity 审计落附录 A 并确认（A.6 裁决 + A.7 审核修订 + #8 复核修正）；**Step 1 引擎 537bdf57；Step 2 Revive 5a16a3bc；Step 3 Bury/Stage aee60287；Step 4 Exile 8336861d（全量 742: 741/0/1 零漂移）**；Step 5 收尾记录见 §7（新卡一律 selector 规范 + Giver 按需另案 + legacy 保留判据状态 + AGENTS.md 引用跳过）。后续事项：存量 74 个组件的逐卡翻转（可选、按需）与 Giver 家族 selector 化（另立计划）。
上游：2026-09-01 对话拍板。相关：`plans/plan-utility-passive-shop-pipeline-2026-08-31.md`（Tag.Revive 打标，与本计划正交）、`plans/plan-4.0-revive-awaken-2026-08-29.md`（ReviveEffect 现状）。

## 1. 已拍板决策（对话裁决记录）

- **病灶认定**：目标定语谓词（阵营/生物/稀有度/卡种/被强化/排序/墓地范围）由各 Effect 类各写一份，约 27 处池子构建、7 个效果类。后果：表达力不均等（Revive 五件套 vs Bury/Stage 仅 creatureFilter，"埋葬1张被强化的敌方卡"当前配不出来）、prefab 字段爆炸、新增定语需挨个改类。
- **药方拍板**：共享 `CardSelector`（`[System.Serializable]` 数据类 + 纯静态 solver，形态抄 `AttackResolverSource` 的"枚举 term + 求值期解析阵营相对性"先例）。**不是**扩展 Tag 系统。
- **Tag 系统边界**：tag 管静态身份族谱（"这张卡属于复活系"——已拍板的 Tag.Revive 追加归 utility 管线，正交）；selector 管战斗中的动态状态查询。**【被强化】等状态定语永远用派生判定（`attackGrowth > 0`），不落 tag**——tag 化会制造第二真相源，所有改 attackGrowth 的点都需同步，漂移即选错目标。
- **触发侧不动**：事件族 + GameEventListener（onMeGainedAttack / onMeRevived / onFriendlyCardBuried …）架构健康，本计划零改动。
- **兼容策略**：加法迁移——各效果新增 `useTargetSelector`（默认 false）+ `targetSelector` 字段；开关关闭时走原代码路径，**存量 prefab 组件实例零改动**（移动四件套活跃 109：Bury 48 / Stage 24 / Exile 8 / Revive 28+1，附录 A.1 口径）；新卡（4.0 roadmap 第 5 步起）一律配 selector；旧字段保留为 deprecated，待 prefab 引用清零（GUID 扫描验证）后再删。
- **时机**：Step 0–4 卡在 4.0 roadmap 第 5 步批量铺卡之前完成，避免铺卡期按旧模式多配一批 per-effect 字段。（2026-10-06 注：铺卡已部分先行——Bury/Stage 的 `creatureFilter` 即 step-5 按旧 per-effect 模式所加、仅覆盖 4/14 入口；「赶在铺卡前」的目标部分落空，但病灶论据由预言变成实证：漂移已发生，统一更紧迫。）
- **入口方法签名不变**：UnityEvent 按 (component, methodName) 绑定，保留 `BuryMyCards(int)` / `ReviveMyCards(int)` 等公开入口 = prefab GameEvent 绑定零迁移。

## 2. 代码事实基线（2026-09-01 实测）

- **Tag enum** = { None, Linger, ManaX, DeathRattle }（`EnumStorage.cs:41`），被 `TagTooltipDatabaseSO` 显示链路耦合（每个 tag 需 displayName + tooltip，cardDesc `<tag:X>` 会渲染）——塞选牌定语会污染玩家可见文本；enum 按值序列化只能追加不能插位（StatusEffect.Revive 槽位保留先例）。→ 这是"不用 tag"的直接论据。（2026-10-06 注：Tag 现为 14 成员（`EnumStorage.cs:41-59`），Revive 已由 utility 管线落地；显示链路耦合论据不变。另：本节行号为 09-01 基线、已漂移——如 `PassesPredicateFilters` 实为 `ReviveEffect.cs:144`——逐方法现状以附录 A parity 表为准。）
- **池子构建分布**（`CopyGameObjectList` 扫描，非测试代码）：BuryEffect×6（:127/:148/:245/:266/:287/:322）、ExileEffect×7（:53/:74/:95/:116/:140/:164/:233）、StageEffect×9（:81/:102/:123/:144/:165/:190/:238/:279/:348）、ReviveEffect×1（:131，RiftOverrideAwareReviveEffect 继承）、HPAlterEffect×2（:269/:304）、AttackGiverEffect×1（:60）、StatusEffectGiverEffect×1（:174）≈ **27 处 / 7 类**。
- **谓词能力矩阵**：ReviveEffect 全集（creatureFilter / typeIDFilter / rarityFilter / onlyEnhanced / sortBy / tagsToCheck / excludeSelf，`ReviveEffect.cs:25-46` + `PassesPredicateFilters:106`）；Bury/Stage 仅 `EffectCreatureFilter`（`EffectScript.cs:13`）；HPAlter / AttackGiver / StatusEffectGiver 各自内联。
- **区域语义**（无墓地数据结构，全部由位置推导）：墓侧 = `index < startCardIndex`（Revive 取池 `ReviveEffect.cs:142`，Bury/Stage 排除 `BuryEffect.cs:91`）；`GetStartCardIndex()` 无 Start Card 返回 -1（Revive 空池 fizzle，Bury `IsCardBelowStartCard` 恒 false = 不命中）；BuryEffect 的 `IsCardAtBottom`（index==0，`BuryEffect.cs:65`）已被"排除墓侧"覆盖，实为冗余防御。干净的双区模型：**GraveSide / DeckSide**（Start Card 本体与中立卡由固定排除处理）。
- **固定排除集不一致（漂移实例）**：Revive 排除 neutral + isPassive + isMinion + revealZone + self；Bury 各方法逐个不同——`BuryEffect.cs:155`（BuryMyCards）无 isPassive、`:329` 有 isPassive（被动引擎实施时逐方法补的）。统一进 selector 的固定排除块正是本次收敛的目标。
- **排序**：`CardScript.FindCardWithMaxAttack/MinAttack` 静态方法（KINGSLAYER / SACRIFICE_WEAKEST）；`ReviveSortBy { None, MaxAttack, MaxExtraAttackTimes }` = 先 shuffle 再稳定降序，平局保持随机（`ReviveEffect.cs:158-166`）。
- **prefab 使用量**（脚本 GUID 扫描 `Assets/Prefabs`）：BuryEffect 47、StageEffect 27、ReviveEffect 22、RiftOverrideAwareReviveEffect 1 ≈ **97 个组件实例**——加法迁移下全部零改动。
- **EditMode fixture**：HeadlessCombatTestFixture 用 SerializedObject 设效果字段建卡；selector 为嵌套序列化对象，设值路径变为 `targetSelector.creatureFilter` 式 FindProperty，fixture 需加 helper（见 §5）。

## 3. 架构

### 3.1 CardSelector 数据类（纯数据，可序列化）

（2026-10-06 重写：吸收 A.6 拍板的 A.5 修订——边界槽排除 / isMinion 正向 / 揭示区双向 / Damager 档；另补审核发现的三处表达力缺口——SelectorSide.Both / SelectorSort.KeepOrder / positiveAttackOnly，见 A.7。）

```csharp
[System.Serializable]
public class CardSelector
{
	public enum SelectorSide { Friendly, Enemy, Both }     // Both = 不做阵营过滤（活跃代码入口 ExileRandomCards / BuryCardsWithTag / StageCardsWithTag / ExileCardsWithTag 需要；ExileRandomCards 唯一绑定在退役池，论据是活跃入口而非活跃绑定；TargetType.Random 先例）
	public enum SelectorZone { Anywhere, GraveSide, DeckSide } // GraveSide = 0 <= index < startCardIndex（仅 deck 成员，zone 语义见 §3.2）
	public enum SelectorRarity { Any, Common, Uncommon, Rare }
	public enum SelectorSort { Random, KeepOrder, MaxAttack, MinAttack, MaxExtraAttackTimes } // KeepOrder = 不 shuffle 不排序（BuryAllMyCards / StageAllFriendlyMinion 保序）
	public enum SelectorMinion { Exclude, Any, Only }      // Only = isMinion 正向（StageMyTokens / StageAllFriendlyMinion / Exile*Minions）；Any = 不过滤（Exile 非 minion 入口）

	public SelectorSide side = SelectorSide.Friendly;      // 求值期按 source.myStatusRef 解析
	public SelectorZone zone = SelectorZone.DeckSide;
	public SelectorMinion minionMode = SelectorMinion.Exclude;
	public EffectScript.EffectCreatureFilter creatureFilter = EffectScript.EffectCreatureFilter.Any; // 复用现有枚举，append-only 追加 Damager 档（IsCreature‖HasAttackAttribute = Giver 伤害能力谓词）
	public SelectorRarity rarityFilter = SelectorRarity.Any;
	public SelectorSort sort = SelectorSort.Random;
	public string typeIDFilter = "";                       // 空 = 不过滤（信徒=RIFT 精确匹配）
	public bool enhancedOnly = false;                      // 【被强化】= attackGrowth > 0，派生判定
	public bool positiveAttackOnly = false;                // GetAttack() > 0（BuryCardWithMinAttack / GiveFriendlyCardWithMinAttack 正攻约束）
	public List<EnumStorage.Tag> tagFilter;                // any-match，空/null = 不过滤（现 tagsToCheck 语义；solver 与 fixture 需 null 容忍）
	public bool excludeSelf = true;

	// 固定排除块：逐项可关；迁移时按附录 A parity 表逐方法锁，不一刀切默认
	public bool excludeNeutral = true;    // ShouldSkipEffectProcessing
	public bool excludePassive = true;    // isPassive
	public bool excludeBottomSlot = false;// index 0（Bury 系）
	public bool excludeTopSlot = false;   // index Count-1（Stage 系）
	public bool includeRevealZone = false;// true = 揭示卡合并进池并去重（Giver / picker 语义）；false = 排除（Revive / 移动四件套语义）
}
```

设计约束：
- `count`（取几张）**不进 selector**——存量入口方法的数量来自 UnityEvent 参数（`BuryMyCards(int amount)`），进 selector 反而破坏签名不变原则。数量留在方法参数。
- 阵营相对性延迟到求解期：selector 不持有阵营引用，`Select(pool, source, spec)` 时按 `source.myStatusRef` 解析 Friendly/Enemy（AttackResolverSource 同款约束：myStatusRef 由 CardFactory 后赋值）。

### 3.2 CardSelectorSolver（静态、可单测）

```csharp
public static class CardSelectorSolver
{
	// 照 DeckCascadeLayout 先例：静态、无生命周期回调、可单测。战斗态只读——
	// combinedDeck 由调用方传入（快照），revealZone 经 CombatManager.Me 读取（现有单例惯例）
	public static List<GameObject> Select(List<GameObject> combinedDeck, CardScript source,
		CardSelector spec, out int startCardIndex);
	// 内部顺序：快照拷贝 → startCardIndex 定位 → zone 过滤 → 固定排除块 → 阵营
	// → 谓词（creature/typeID/rarity/enhancedOnly/positiveAttackOnly/tag）
	// → ShuffleList（KeepOrder 跳过）→ 稳定排序（平局保随机）→ 返回有序池（取前 N 由调用方做）

	// 位置游走独立入口（BuryNextXCards / GiveStatusEffectToLastXCards 家族，不塞谓词池模型）：
	// 从源位向 index 0 逐卡取。参数表达：下界（Bury = startCardIndex 有界；LastX = 0 无界）、
	// passiveSourceStartsFromTop（Bury=true：passive 源常驻墓侧，其「卡组顶N卡」须指活区顶——RELIC_CHAIN_BURIAL 规则，
	// BuryNextXCards 专属；LastX 家族无此规则传 false，passive 源照常从自身位向墓侧深处走；当前 2 个活跃 LastX 绑定源均 isPassive=0）、
	// 逐卡排除子集复用固定排除块（N/P/M/B）
	// 源定位共通规则：reveal 源从 Count-1 起；源不在 deck 且非 reveal（被放逐/销毁）→ 空池 fizzle（BuryNextXCards 现行为）
	public static List<GameObject> WalkFromSource(List<GameObject> combinedDeck, CardScript source,
		CardSelector spec, int amount, int lowerBound, bool passiveSourceStartsFromTop);
}
```

- **zone 成员语义（2026-10-06 校准）**：谓词只定义在 deck 成员上——GraveSide = `0 <= index < startCardIndex`；DeckSide = `index > startCardIndex`（Start Card 本体由 excludeNeutral 排掉）。候选不在 deck（`IndexOf == -1`：揭示区 / 已放逐 / 已销毁）在两个 zone **都不命中**；揭示卡只能经 `includeRevealZone` 显式回池。**禁止**复用 `IsCardBelowStartCard` 的比较式当 zone 判定：`-1 < startCardIndex` 会把 deck 外卡判进墓侧——现有调用方只在 DeckSide 排除方向受益（IsBuryable / Stage 总闸）；Revive 的池构建则已显式排除 `index < 0`（`ReviveEffect.cs:187`），本就成员正确，陷阱只存在于复用比较式的新代码里。
- **无 Start Card（startCardIndex = -1）**：GraveSide = 空池（Revive fizzle 现状）；DeckSide / Anywhere = 边界不生效、全 deck 命中（与 `IsCardBelowStartCard` 恒 false 的现状一致）。
- **行为顺序固化**：先 shuffle 再 stable orderby（`OrderByDescending` 是稳定排序），平局天然随机——复刻 `ReviveEffect.SortOrShufflePool` 的现行为，测试锁死。
- 不在 solver 内触发事件/动画：池子构建与事件抛出（onMeBuried / 苏醒族）解耦，保持现有时序不变（埋/置顶移动先改逻辑表、快照索引、捕获动画请求、最后抛事件）。

### 3.3 效果接入（加法开关）

每个效果类新增两个字段：

```csharp
[Header("Unified Target Selector")]
[Tooltip("True = use targetSelector; False = legacy per-card fields (existing prefabs)")]
public bool useTargetSelector = false;
public CardSelector targetSelector = new CardSelector();
```

公开入口方法内部分支：`useTargetSelector ? CardSelectorSolver.Select(...) : LegacyBuildPool()`。legacy 路径原样保留，行为对照由存量 EditMode 测试兜底。

施工模式（2026-10-06 补，Step 2–4 接入者照此执行）：

- **入口间差异 = spec 补丁**：序列化 `targetSelector` 表达该组件主入口的可配部分；同一组件上入口间的硬编码差异（排除集 / 排序 / 保序 / 全量）由入口方法在 spec 副本上叠补丁后传入 solver。例：`BuryAllMyCards` 补丁 `excludePassive=true + sort=KeepOrder + 池全量`；max/min picker 入口补丁对应 `SelectorSort` + `side`（`targetFriendly` 序列化 bool 的迁移映射：true→Friendly / false→Enemy，无运行时分支）。
- **UnityEvent 方法参数注入**：方法参数型 typeID（`StageAllFriendlyMinion(string)` / `StageTheirSpecificCard(string)`）在入口内 clone spec 覆盖 `typeIDFilter` 再 Select——`RiftOverrideAwareReviveEffect` 运行时改写 typeIDFilter 的同款先例；`ExileMyCardsWithTypeID` 的 `StringSO cardTypeIDSO` 迁移映射为 `typeIDFilter = cardTypeIDSO.value`。
- **保留 per-method**：`StageCardWithMostStatusEffect`（按 `statusEffectToCheck` 参数化层数排序，进不了 `SelectorSort`；0 绑定）与 A.5 第 6 条范围外各项（find-or-create / 事件上下文目标 / 有放回抽取 / oncePerRound / delayedRevive）不迁移。

## 4. 实施步骤（每步 gate：完成即汇报，确认后继续）

- **Step 0 审计（不动码）**：枚举 27 处池子构建 → parity 表（方法名 / 阵营 / 区域 / 固定排除集 / 谓词 / 排序 / 数量来源 / UnityEvent 绑定面）。重点核对 Bury 系固定排除集逐方法差异（:155 vs :329 漂移）与 HPAlter/AttackGiver/StatusEffectGiver 内联逻辑的可表达性。产出表格落本文件附录。*gate：用户确认 parity 表*。
- **Step 1 引擎（纯新增）**：CardSelector + CardSelectorSolver + EditMode 单测。goldens：Start Card 两侧边界 / 无 Start Card / revealZone·passive·minion·neutral 排除 / enhancedOnly 零值边界 / 稀有度 / typeID 精确匹配 / 排序平局随机 / 阵营相对性 / Both 阵营 / KeepOrder 保序 / positiveAttackOnly / Damager 档 / includeRevealZone 合并去重 / IndexOf==-1 候选双 zone 不命中 / minionMode 三态 / walk 有界·无界·reveal源从顶起·passive源按 passiveSourceStartsFromTop 分岔·源不在deck且非reveal→fizzle / includeRevealZone×excludeSelf 交互（揭示卡==源且 excludeSelf=false 才并入——Giver 去重分支的真实用例）。*gate：测试全绿*。
- **Step 2 ReviveEffect 接入**（含 RiftOverrideAwareReviveEffect 继承链）：开关默认关，存量 22+1 prefab 零改动；ReviveEffectTests + 全量 EditMode 绿；抽 1 张 prefab 打开开关做行为对照。*gate：汇报*。
- **Step 3 BuryEffect + StageEffect 接入**：同模式；parity 表核对排除集；全量 EditMode 绿。*gate：汇报*。
- **Step 4 ExileEffect 接入**：信徒放逐轴（4.0 三轴之一）铺卡前完成；全量 EditMode 绿。*gate：汇报*。
- **Step 5 铺卡期采用与收尾**：4.0 roadmap 第 5 步起新卡一律 selector；HPAlter/AttackGiver/StatusEffectGiver 剩余 4 处按需迁移（不阻塞铺卡）；legacy 清理判据 = GUID 扫描 prefab 引用为零 + 无 prefab 使用 `tagsToCheck` 语义（当前 Revive 系 prefab 中 tagsToCheck 配置数 = 0，清理阻力小）。

## 5. 风险与对策

- **固定排除集漂移**（现状已存在）：Step 0 parity 表逐方法锁定；开关关闭路径不动旧代码；行为对照测试兜底。
- **fixture 设值复杂化**：嵌套属性 `FindProperty("targetSelector.creatureFilter")`；在 HeadlessCombatTestFixture 加 `SetSelector(spec)` helper，测试侧写起来与平铺字段等价。
- **排序平局随机性回退**：solver 内固化 shuffle→stable sort 顺序，单测锁行为；禁止实现成先 sort 后 shuffle。
- **双轨期认知成本**：新卡配置规范（一律 selector）写进本文档 + 铺卡批量脚本约定（`plans/plan-4.0-card-prefab-config-conventions` 同源）；AGENTS.md 仅在限额富余时加一行引用本文档，超限不动（32 KB 硬限制优先）。
- **事件时序回归**：solver 只产池子不抛事件；Step 2–4 的 gate 必跑全量 EditMode（苏醒/埋葬反应链测试已有覆盖）。
- **表达力缺口回验**：Step 2–4 接入时对照 parity 表逐入口验证 spec 可表达（2026-10-06 审核已补出 Both / KeepOrder / positiveAttackOnly 三个 A.5 漏项，证明清单未收敛）；发现新缺口回写本文档附录，不就地特设。

## 6. 非目标

- 不动触发侧事件族 / GameEventListener / RaiseSpecific·RaiseOwner·RaiseOpponent 作用域。
- 不动 `AttackResolverSource`（攻击数值解析与目标选取是两个问题；未来若需共享"按条件收卡"原语另立计划）。
- 不改 Tag enum 本体（Tag.Revive 追加归 utility 管线 Step 1，正交推进）；不引入"隐藏 tag"显示概念。
- 不删任何 legacy 字段/分支，直至 prefab 引用清零（§4 Step 5 判据）。
- 不做通用过滤器 SO / 运行时规则组合（当前 90+ 卡规模下序列化字段组合已够；避免过度设计）。

## 7. Step 5 收尾记录（2026-10-06）

### 7.1 新卡配置规范（一律 selector）

自本计划收官起，新卡（5.0 / 平衡扩充 / 铺卡批量脚本）的移动效果类组件一律配 `useTargetSelector = true` + `targetSelector`，**不再配置 legacy 逐效果字段**（它们是冻结的 parity 垫片，仅为存量 109 prefab 存在）。可配置面与硬编码面的边界：

- **spec 可配**（序列化 `targetSelector`）：`side`（主入口阵营形状）、`creatureFilter`（含 `Damager` 档）、`rarityFilter`、`typeIDFilter`、`enhancedOnly`、`positiveAttackOnly`、`sort`（含 `KeepOrder`）、`tagFilter`、`excludeSelf`、`includeRevealZone`（Giver 式揭示并池升级）、`zone`（GraveSide/DeckSide/Anywhere）。
- **入口硬编码，勿在 spec 上配**（Step 2-4 已按附录 A parity 表锁死，spec 值会被覆盖）：各入口的排除集漂移部分（哪些入口排/不排 passive、minion 正向等）；BuryNextXCards 的 walk 形状（`WalkFromSource` 固定参数，TEST 旁路走 `ignoreStartCardBoundary`）；`*WithTag` 入口开 tagFilter、非 Tag 入口强制剥离；Stage 系 zone=Anywhere（总闸语义）与 max picker 的 DeckSide 预滤；Exile 系强制 Anywhere/minion Any/self 不排。
- 数量永远走入口方法参数/IntSO（§3.1 拍板不变）；oncePerRound / delayedRevive 是执行侧配置，与 selector 正交、照常配。

### 7.2 Giver 家族处置

维持 A.5 #10 拍板：**按需迁移，不阻塞**。新卡若需 Giver 式选取（全域+揭示合并 / 伤害能力谓词 / 有放回随机），移动四件套的 spec 已能表达大半（`includeRevealZone=true` + `creatureFilter=Damager`）；Giver 类本体（StatusEffectGiver/AttackGiver/AttackTimesGiver ≈57 绑定）的 selector 化另立计划，本计划不动。

### 7.3 Legacy 清理判据执行状态

- 判据 1「无 prefab 使用 `tagsToCheck` 语义」：**✓ 成立**（2026-10-06 复扫：四类全部 prefab 的 tagToCheck/tagFilter 均无非零配置）。
- 判据 2「GUID 扫描 legacy 字段 prefab 引用清零」：**未达成**——翻转积压 = **74 个组件实例**（Bury 28 / Stage 20 / Exile 4 / Revive 22，含非默认 legacy 配置；其中 `oncePerRound` / `delayedRevive` 为执行侧配置、翻转时不迁移，`targetFriendly` 仅 picker 入口需要映射为 side）。字段/分支删除推迟到积压逐卡清零后（翻转映射按 §3.3 与附录 A.2 执行）。
- 结论：**legacy 路径与字段全部保留，本计划不删任何代码**。

### 7.4 AGENTS.md 引用行

**跳过**：当前 31737/32768 字节，加一行（~150B）后余量 <1 KB，违反「Keep ≥ 1 KB headroom」硬规则；§5 预案「超限不动」生效。新卡规范以本节为准（铺卡批量脚本与 unity-card-factory skill 后续引用此处）。

## 附录 A：Step 0 parity 审计（2026-10-04，基于当时 main 全量复核）

> 本附录为 Step 0 产出，以审计时点代码为准；§2 的 2026-09-01 数字保留作历史基线。口径：prefab 组件实例 = 脚本 GUID 扫描 `Assets/Prefabs`（含 `_DEPRECATED`，单列拆分）；UnityEvent 绑定 = `m_MethodName` 扫描 `Assets/**/*.{prefab,unity}`（含退役池绑定，读数只作相对权重；全部绑定都在 prefab 内，GameScene.unity 零命中）。

### A.1 面基线（替换 §2 的「27 处 / 7 类」口径）

| 家族 | 类 | 活跃实例（总/退役） | 直接绑定合计 |
|---|---|---|---|
| 移动四件套（本期迁移面） | BuryEffect | 48（48/0） | ≈58 |
| | StageEffect | 24（25/1） | ≈37 |
| | ExileEffect | 8（8/0） | ≈15 |
| | ReviveEffect（+RiftOverrideAwareReviveEffect 1） | 28（28/0） | ≈31 |
| Giver 家族（事实第二选择器） | StatusEffectGiverEffect | 5（15/10） | ≈22 |
| | AttackGiverEffect | 27（27/0） | ≈29 |
| | AttackTimesGiverEffect | 6（6/0） | 6 |
| 强化/消耗/转移 | CurseEffect | 27 | ≈29 |
| | ConsumeStatusEffect | 7（13/6） | ≈13 |
| | TransferStatusEffectEffect | 2 | 2 |
| 计数源（非选牌） | HPAlterEffect | 6（57/51） | — |
| 退役死面 | CardManipulationEffect | **0**（26/26 全退役） | 0 |

- 09-01 后新增的池子消费者：AttackTimesGiverEffect、DeathrattleTriggerEffect、GraveRobberEffect、GravePuppeteerEffect、MassSacrificeEffect、BuriedCreatureAttackEffect（后四个为组合器/事件上下文，不建池）。
- **CardManipulationEffect 整类退役**（Delay/DestroyMinions 的 isMinion 正向池只剩退役池引用，0 绑定）——无需迁移，是 legacy 清理候选（另案）。
- HPAlterEffect 的两处 deck 遍历（§2 曾计为池子构建）实为伤害计数循环（数 Infected / 数 friendly typeID），不是目标选取，selector 不吸收。

### A.2 移动四件套 parity 主表

排除集速记：N=ShouldSkipEffectProcessing（neutral/start），P=isPassive，M=isMinion，S=excludeSelf 生效，B=排除 index0（atBottom），G=排除墓侧（index<startCardIndex），T=排除顶槽（index Count-1），R=排除 revealZone。

**BuryEffect**（48 实例；执行统一走 BuryChosenCards：埋至 index0 + 动画快照 + onMeBuried 族）

| 入口 | 阵营 | 排除集 | 谓词 | 排序 | 数量来源 | 绑定 |
|---|---|---|---|---|---|---|
| BuryMyCards | 友 | N+M+S+B+G（**无P**） | creatureFilter | shuffle | UnityEvent int | 16 |
| BuryMyCardsWithTag | 友 | 同上 | tag（List）**无 creatureFilter** | shuffle | UnityEvent int | 0 |
| BuryMyCards_BasedOnIntSO | 友 | →BuryMyCards | | | IntSO（owner/enemy） | 0 |
| BuryMyCards_CountBasedOnFriendlyBuried | 友 | →BuryMyCards | | | param−本回合友葬计数 | 1 |
| BuryTheirCards | 敌 | N+M+B+G（无P 无S） | creatureFilter | shuffle | UnityEvent int | 23 |
| BuryTheirCardsWithTag | 敌 | 同上 | tag 无 creatureFilter | shuffle | UnityEvent int | 0 |
| BuryTheirCards_BasedOnIntSO | 敌 | →BuryTheirCards | | | IntSO | 1 |
| BuryAllMyCards | 友 | N+**P**+M+S+B+G | **无 creatureFilter** | 无 shuffle（保序） | 池全量 | 1 |
| BuryCardsWithTag | 双 | N+M+S+B+G（无P） | tag | shuffle | UnityEvent int | 0 |
| BuryCardWithMaxAttack | targetFriendly | N+P+M+S+B+G+creatureFilter（IsBuryable） | 攻最高，平局随机 | — | 1 | 1 |
| BuryCardWithMinAttack | 同 | 同 + 正攻击 | 攻最低，平局随机 | — | 1 | 1 |
| BuryNextXCards | 位置游走（无阵营） | 逐卡 N（可 TEST 旁路）+P+M+B；下界=startCardIndex；源在 reveal/isPassive→从顶起 | 无 | 位置序 | UnityEvent int | 7 |
| BuryNextXCards_BasedOnAttack | 同 | 同 | 无 | 位置序 | 自身 GetAttack() | 1 |
| BurySelf | 自身 | B/G 命中即 no-op | 无 | — | 1 | 2 |

漂移定性（§5 预言证实）：**isPassive 仅 BuryAllMyCards 与 max/min pickers 排除，其余 5 个建池入口不排**；**creatureFilter 仅 4/14 入口生效**（4.0 step-5 加的，只改了 My/Their 主干）；B 与 G 在 AlwaysBottom（startCardIndex==0）下冗余。`ignoreStartCardBoundary` 是 TEST-ONLY 字段，仅 BuryNextXCards 读。

**StageEffect**（24 活跃；执行总闸 = StageChosenCards 开头 `RemoveAll(IsCardBelowStartCard)`，2026-09-17 起**所有**路径统一排墓侧，含 StageSelf 与 IndexOf==-1 的卡；各池方法另排 T）

| 入口 | 阵营 | 排除集 | 谓词 | 排序 | 数量来源 | 绑定 |
|---|---|---|---|---|---|---|
| StageMyCards | 友 | N+P+M+S+T | creatureFilter | shuffle | UnityEvent int | 22 |
| StageMyCardsWithTag | 友 | 同 | tag（单） | shuffle | UnityEvent int | 1 |
| StageCardsWithTag | 双 | 同 | tag | shuffle | UnityEvent int | 0 |
| StageTheirCardsWithTag | 敌 | 同（无S） | tag | shuffle | UnityEvent int | 0 |
| StageMyTokens | 友 | 同 | **isMinion 正向** | shuffle | UnityEvent int | 1 |
| StageAllFriendlyMinion | 友 | 同 | isMinion 正向 + typeID（方法参数，非字段） | 无 shuffle | 池全量 | 1 |
| StageTheirSpecificCard | 敌 | 同 | typeID（方法参数） | shuffle | 1 | 1 |
| StageCardWithMaxAttack | targetFriendly | N+P+M+S+T+G | creatureOnly→IsCreature | 攻最高平局随机 | 1 | 2 |
| StageCardWithMostStatusEffect | targetFriendly | N+P+M+S+T+G | 状态层数最多（statusEffectToCheck） | 平局随机 | 1 | 0 |
| StageMyCards_BasedOnIntSO | 友 | →StageMyCards | | | IntSO | 0 |
| StageSelf | 自身 | T 命中 no-op（+总闸 G） | 无 | — | 1 | 9 |

**ExileEffect**（8 实例；zone=**全域含墓侧**，无 B/G/T/R 过滤、无 self 排除、无 creatureFilter；执行时若目标=揭示卡会清 revealZone）

| 入口 | 阵营 | 排除集 | 谓词 | 排序 | 数量来源 | 绑定 |
|---|---|---|---|---|---|---|
| ExileMyCards / ExileTheirCards / ExileRandomCards | 友/敌/双 | N+P | 无 | shuffle | UnityEvent int | 5/1/1 |
| ExileMyCardsWithTag / Their / Both | 友/敌/双 | N+P | tag（单） | shuffle | UnityEvent int | 0/0/0 |
| ExileMyMinions / ExileTheirMinions | 友/敌 | N（**无P**） | **isMinion 正向** | shuffle | UnityEvent int | 0/0 |
| ExileMyCardsWithTypeID | 友 | N（**无P**） | typeID（StringSO） | shuffle | UnityEvent int | 6 |
| ExileMyCards_BasedOnIntSO / Their | →上两者 | | | | IntSO（>0 才走） | 0/0 |
| ExileSelf | 自身 | 无 | 无 | — | 1 | 2 |

**ReviveEffect**（28+1；池=BuildRevivePool：墓侧 only（无 Start Card→空池 fizzle），固定排除 N+P+M+R+S 全开）

| 入口 | 阵营 | 谓词全集 | 排序 | 数量来源 | 绑定 |
|---|---|---|---|---|---|
| ReviveMyCards | 友 | creatureFilter（自有枚举，wildcardTypeFilter 旁路类型行）+onlyEnhanced（attackGrowth>0）+typeIDFilter（字段）+rarityFilter | shuffle→稳定降序（sortBy=None/MaxAttack/MaxExtraAttackTimes） | UnityEvent int | 19 |
| ReviveMyCardsWithTag | 友 | +tag（List） | 同 | UnityEvent int | 1 |
| ReviveTheirCards / TheirWithTag | 敌 | 同 | 同 | UnityEvent int | 4/0 |
| ReviveSelf | 自身须在墓 | — | — | 1 | 4 |
| ReviveMyCardsIfArmed | 友 | roundEndReviveArmed 门 | 同 | 1 | 1 |
| ArmRoundEndRevive（设旗） | — | — | — | — | 1 |

执行侧（不进 selector）：oncePerRound 组件级轮门、delayedRevive 落 Start Card 尾、onMeRevived 族只从 ReviveChosenCards 抛。RiftOverrideAwareReviveEffect.FriendRiftRevealOrOverride（绑定 1）= 运行时改写 typeIDFilter=curseCardTypeID 后 ReviveTheirCards(1)，否则 ReviveMyCards(1)。

### A.3 Giver 家族 = 事实上的第二套选择器（StatusEffectGiver 5 + AttackGiver 27 + AttackTimesGiver 6）

共享基础设施（StatusEffectGiverEffect）：`target: TargetType` 选阵营；`includeSelf` **默认 false**（与移动四件套 excludeSelf 默认 true 相反）；`onlyTargetEnemyDamagingCards` = 伤害能力谓词（IsCreature‖HasAttackAttribute，≠ CardType 谓词）；`CollectFriendlyCards` = N + 阵营 + self + 伤害谓词 + CanReceive，**并把揭示卡并入池（去重）**；`spreadEvenly` = 无放回均摊 vs 每 1 点有放回随机。固定排除：**无 P、无 M、无任何区域限制（全域含墓侧）**。

| 入口（两代） | 选取形状 | 绑定 |
|---|---|---|
| GiveStatusEffect / GiveAttack | 全域+揭示合并，shuffle，每点有放回随机 | 11/1 |
| GiveSelfStatusEffect / GiveSelfAttack / DoubleOwnAttack | 自身，无池 | 8/7/2 |
| GiveAllFriendlyStatusEffect / GiveAllFriendlyAttack | CollectFriendlyCards 全量 | 0/1 |
| GiveStatusEffectToLastXCards / GiveAttackToLastXCards | 位置游走（同 BuryNextX 形状，无墓侧界） | 2/2 |
| GiveStatusEffectToXFriendly / GiveAttackToXFriendly | CollectFriendlyCards shuffle 取前 X（AttackGiver 额外 RemoveAll(!IsCreature) 收紧为纯实体） | 0/7 |
| GiveStatusEffectToXFriendly_BasedOnIntSO / GiveAttackTo…_BasedOnIntSO | 逐点重收池 + 有放回随机 | 0/3 |
| …_BasedOnStaged | X=回合计数，委托 XFriendly | 0/1 |
| GiveFriendlyCardWithMinAttack（AttackGiver） | 友方攻最低（正攻），平局随机 | 3 |
| GiveAttackToLastGainedAttack | 事件上下文 lastCardGainedAttack | 1 |
| ModifyAllCreatureAttackThisRoundExceptCurse | 双方全体实体 + 揭示实体 | 1 |
| GiveStatusEffectBasedOnStatusEffectCount / GiveSelf… | 数自身状态层数→委托 | 1/0 |
| AttackTimesGiver：GiveSelfAttackTimes / …Permanent / _PerExiledCount / GiveRandomFriendlyCreatureAttackTimes / BumpFriendlyCreatureAttackTimesAura / GiveRevealedCurseAttackTimes | 自身 / 按放逐计数 / CollectFriendlyCreatures 随机 / 光环全量 / 揭示诅咒 | 各 1 |

### A.4 其余建池类定性（本期范围外或部分外）

- **CurseEffect**（27 活跃）：Find{Enemy,Friendly}CardWithTypeID = 全域 typeID 精确匹配 + 揭示区合并，**find-first（非随机）+ find-or-create（找不到即 spawn）**；ConsumeEnemyCurseAttack = 全体敌方 typeID 卡 round-robin 扣攻。形状特殊，建议留在类内。
- **ConsumeStatusEffect**（7 活跃）/ **TransferStatusEffectEffect**（2）：源池 = 持有状态/攻击的己/敌卡 + 揭示区合并；ConsumeEnemyCardWithMaxAttack = 攻最高（绑定 1）。
- **组合器（零自建池）**：MassSacrificeEffect（BuryAllMyCards + lastSuccessfulBuryCount + AddTempCard）、GraveRobberEffect（ReviveTheirCards + lastCardRevived 快照）、GravePuppeteerEffect（墓侧友方实体随机 1 PerformAttackAs，空则委托 BuryMyCardsWithTag）、BuriedCreatureAttackEffect（lastCardBuried）。
- **DeathrattleTriggerEffect**（2）：墓侧友方游走重抛 onMeBuried（选取=事件对象，链代守卫防重）；TriggerDeathrattleOfLastRevivedFriendly = 事件上下文。
- **HPAlterEffect**（6 活跃）：两处 deck 遍历为计数循环（数 Infected / 数 friendly typeID），非选牌。

### A.5 对 §3 设计的修订输入（audit 驱动）

selector 若要覆盖移动四件套 + Giver 家族，§3.1 草图需补：

1. **边界槽排除**：Bury 系排 index0、Stage 系排顶槽（T≈当前揭示卡在顶时）——固定排除块需 `excludeBottomSlot` / `excludeTopSlot`（或 zone 细化），§3.1 现稿没有。
2. **isMinion 正向模式**：StageMyTokens / StageAllFriendlyMinion / Exile*Minions 需要正向谓词（固定排除块的 excludeMinion 只能反向）。
3. **伤害能力谓词档**：Giver 家族的 IsCreature‖HasAttackAttribute ≠ EffectCreatureFilter 的 CardType 判定，creatureFilter 需加 `Damager` 档或独立开关。
4. **揭示区双向语义**：Revive=R 排除；Giver/Curse/Consume/Transfer=include-with-dedup。§3.1 只有 excludeRevealZone。
5. **位置游走**：BuryNextXCards / Delay（已退役）/ LastX 家族是「从源位向 index0 的位置序游走」，不是谓词池——建议 solver 增设独立 walk 入口或保留 per-method，不硬塞谓词模型。
6. **范围外（明确）**：find-or-create（Curse）、事件上下文目标（lastCardBuried/Revived/GainedAttack）、有放回抽取（调用方语义）、oncePerRound / delayedRevive（执行侧）。
7. **枚举映射**：ReviveEffect 自有 CreatureFilter{Any,Creature,Phenomenon,Token} 与 EffectCreatureFilter 同值 1:1；ReviveSortBy ⊂ SelectorSort；wildcardTypeFilter 旁路须在 solver 保留。
8. **excludeSelf 缺省相反**：移动四件套 true / Giver 家族 includeSelf false——迁移时按 parity 表逐方法锁，不能一刀切默认。
9. **count 来源六种**：UnityEvent int、IntSO（owner/enemy 对）、自身 GetAttack、回合计数器、池全量、组件字段（lastXCardsCount/xFriendlyCount）——维持拍板「数量留在入口方法」，selector 只产有序池。
10. **迁移优先级（按活跃绑定权重）**：Revive(31)→Bury(58)→Stage(37)→Exile(15)；Giver 家族选择逻辑最异构（≈57 绑定）维持「按需迁移」拍板；Curse/Consume/Transfer 不进本期。

### A.6 用户裁决（2026-10-04）

- **parity 表确认**，Step 0 gate 通过。
- **A.5 修订按审计建议吸收进 §3.1 设计**：1/2/4 必须吸收（边界槽排除 / isMinion 正向模式 / 揭示区双向语义）；3 以 creatureFilter 增设 `Damager` 档解决；5 做成 solver 独立 walk 入口；6-10（范围外明确化 / 枚举映射 / excludeSelf 逐方法锁 / count 留入口 / 迁移优先级）按审计结论执行。
- 实施仍待用户明示「修改代码」后从 Step 1 起。

### A.7 审核修订记录（2026-10-06）

外部审核（对代码全量抽查 ≈15 条承重声明——四效果类排除集 / Revive 排序与 wildcard 旁路 / Stage 总闸 / Giver 共享基础设施 / CardScript 谓词字段，全部属实）后落实的修订：

1. **§3.1 重写**：A.6 拍板的吸收项落进数据类草图（此前 §3.1 停在 09-01 原稿，与 A.6 裁决自相矛盾）。
2. **新增表达力缺口三处**（A.5 漏项，均有活跃绑定 / 活跃入口支撑，非理论问题）：
   - `SelectorSide.Both`——`ExileRandomCards` 不做阵营过滤（`ExileEffect.cs:91`）；同族 `ExileCardsWithTag` / `BuryCardsWithTag` / `StageCardsWithTag` 同形（0 绑定）。注意：ExileRandomCards 的唯一绑定在退役池（Mass Burial.prefab），Both 的论据是四个活跃**代码入口**而非活跃绑定。先例：`EnumStorage.TargetType.Random`（`EnumStorage.cs:19-24`）。
   - `SelectorSort.KeepOrder`——`BuryAllMyCards`（`BuryEffect.cs:319-337`）与 `StageAllFriendlyMinion`（`StageEffect.cs:216-248`）无 shuffle 保序。
   - `positiveAttackOnly`——`BuryCardWithMinAttack` 的正攻约束（`BuryEffect.cs:201`，`GetAttack() > 0`）；Giver 侧 `GiveFriendlyCardWithMinAttack` 同形。
3. **zone 成员语义校准**（§3.2）：墓地卡在 list 内（`0 <= index < startCardIndex`，埋卡 `Insert(0)`），并非 index -1；-1 = 揭示 / 放逐 / 销毁（不在 deck）。`IsCardBelowStartCard` 的比较式把两个集合 lump 成一个布尔：对 DeckSide 排除是受益（IsBuryable / Stage 总闸顺带排掉揭示卡），反过来当 GraveSide 成员判定则是陷阱——solver 禁止复用，-1 候选双 zone 不命中，揭示卡只能经 `includeRevealZone` 显式回池（Revive 的池构建显式检查 `index < 0`，本就成员正确，非兜底）。
4. **保留 per-method 增补**：`StageCardWithMostStatusEffect`（按 `statusEffectToCheck` 参数化层数排序，进不了 `SelectorSort`；0 绑定）。
5. **施工模式落 §3.3**：spec 补丁（同一组件入口间差异）与 UnityEvent 方法参数注入（方法参数 typeID / StringSO→string 映射）。
6. **基线对齐**：§1 实例数 97 → 109（A.1 口径；09-01 数不含 Exile）；§2 Tag enum 4 → 14 成员（Tag.Revive 已由 utility 管线落地，`EnumStorage.cs:41-59`）；§1 时机目标注记（Bury/Stage 的 `creatureFilter` 系 step-5 铺卡期按旧 per-effect 模式所加、仅覆盖 4/14 入口——漂移已由预言变实证）；§2 行号漂移声明（以附录 A parity 表为准）。
7. **Step 1 goldens 扩充**（§4）：Both / KeepOrder / positiveAttackOnly / Damager / includeRevealZone 合并去重 / IndexOf==-1 双 zone 不命中 / minionMode 三态 / walk 三规则。
8. **同日复核修正（ZCode 独立复核本 A.7）**：
   - **P1（设计级）**：walk 的「reveal/isPassive 源从顶起」系 BuryNextXCards 专属规则（RELIC_CHAIN_BURIAL），不得硬编码进共享入口——已参数化为 `passiveSourceStartsFromTop`（LastX 家族无此规则；已验证当前 2 个活跃 LastX 绑定源 MAD_SCIENTIST / CURSE_THIRST_ARCH_SUMMONER 均 `isPassive=0`，故今天零破坏，属未来卡的静默 parity 陷阱）。
   - **P3（goldens 补两条）**：walk 的「源不在 deck 且非 reveal → fizzle」分支显式化（BuryNextXCards 的 `currentIndex < 0 return`）；`includeRevealZone` × `excludeSelf` 交互（揭示卡==源且 excludeSelf=false 才并入，Giver includeSelf 去重分支的真实用例）。
   - **P2（事实勘误三处）**：Tag 实为 **14** 成员（4+10，非 13）；`ExileRandomCards` 唯一绑定在退役池 `_DEPRECATED/1.0 no cost/Exile/Mass Burial.prefab`，Both 的论据改为四个活跃**代码入口**；Revive 池构建显式检查 `index < 0`（`ReviveEffect.cs:187`）本就成员正确，原「靠兜底、不是谓词正确」表述降格失实。
