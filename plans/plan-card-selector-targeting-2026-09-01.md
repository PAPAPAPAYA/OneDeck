# 目标选取统一化：共享 CardSelector 实施计划

日期：2026-09-01
状态：**方向已拍板（共享 CardSelector，不扩 Tag 系统），实施待「修改代码」**。按步骤 gate 推进：每步完成后汇报，用户确认后继续下一步。
上游：2026-09-01 对话拍板。相关：`plans/plan-utility-passive-shop-pipeline-2026-08-31.md`（Tag.Revive 打标，与本计划正交）、`plans/plan-4.0-revive-awaken-2026-08-29.md`（ReviveEffect 现状）。

## 1. 已拍板决策（对话裁决记录）

- **病灶认定**：目标定语谓词（阵营/生物/稀有度/卡种/被强化/排序/墓地范围）由各 Effect 类各写一份，约 27 处池子构建、7 个效果类。后果：表达力不均等（Revive 五件套 vs Bury/Stage 仅 creatureFilter，"埋葬1张被强化的敌方卡"当前配不出来）、prefab 字段爆炸、新增定语需挨个改类。
- **药方拍板**：共享 `CardSelector`（`[System.Serializable]` 数据类 + 纯静态 solver，形态抄 `AttackResolverSource` 的"枚举 term + 求值期解析阵营相对性"先例）。**不是**扩展 Tag 系统。
- **Tag 系统边界**：tag 管静态身份族谱（"这张卡属于复活系"——已拍板的 Tag.Revive 追加归 utility 管线，正交）；selector 管战斗中的动态状态查询。**【被强化】等状态定语永远用派生判定（`attackGrowth > 0`），不落 tag**——tag 化会制造第二真相源，所有改 attackGrowth 的点都需同步，漂移即选错目标。
- **触发侧不动**：事件族 + GameEventListener（onMeGainedAttack / onMeRevived / onFriendlyCardBuried …）架构健康，本计划零改动。
- **兼容策略**：加法迁移——各效果新增 `useTargetSelector`（默认 false）+ `targetSelector` 字段；开关关闭时走原代码路径，**存量约 97 个 prefab 组件实例零改动**；新卡（4.0 roadmap 第 5 步起）一律配 selector；旧字段保留为 deprecated，待 prefab 引用清零（GUID 扫描验证）后再删。
- **时机**：Step 0–4 卡在 4.0 roadmap 第 5 步批量铺卡之前完成，避免铺卡期按旧模式多配一批 per-effect 字段。
- **入口方法签名不变**：UnityEvent 按 (component, methodName) 绑定，保留 `BuryMyCards(int)` / `ReviveMyCards(int)` 等公开入口 = prefab GameEvent 绑定零迁移。

## 2. 代码事实基线（2026-09-01 实测）

- **Tag enum** = { None, Linger, ManaX, DeathRattle }（`EnumStorage.cs:41`），被 `TagTooltipDatabaseSO` 显示链路耦合（每个 tag 需 displayName + tooltip，cardDesc `<tag:X>` 会渲染）——塞选牌定语会污染玩家可见文本；enum 按值序列化只能追加不能插位（StatusEffect.Revive 槽位保留先例）。→ 这是"不用 tag"的直接论据。
- **池子构建分布**（`CopyGameObjectList` 扫描，非测试代码）：BuryEffect×6（:127/:148/:245/:266/:287/:322）、ExileEffect×7（:53/:74/:95/:116/:140/:164/:233）、StageEffect×9（:81/:102/:123/:144/:165/:190/:238/:279/:348）、ReviveEffect×1（:131，RiftOverrideAwareReviveEffect 继承）、HPAlterEffect×2（:269/:304）、AttackGiverEffect×1（:60）、StatusEffectGiverEffect×1（:174）≈ **27 处 / 7 类**。
- **谓词能力矩阵**：ReviveEffect 全集（creatureFilter / typeIDFilter / rarityFilter / onlyEnhanced / sortBy / tagsToCheck / excludeSelf，`ReviveEffect.cs:25-46` + `PassesPredicateFilters:106`）；Bury/Stage 仅 `EffectCreatureFilter`（`EffectScript.cs:13`）；HPAlter / AttackGiver / StatusEffectGiver 各自内联。
- **区域语义**（无墓地数据结构，全部由位置推导）：墓侧 = `index < startCardIndex`（Revive 取池 `ReviveEffect.cs:142`，Bury/Stage 排除 `BuryEffect.cs:91`）；`GetStartCardIndex()` 无 Start Card 返回 -1（Revive 空池 fizzle，Bury `IsCardBelowStartCard` 恒 false = 不命中）；BuryEffect 的 `IsCardAtBottom`（index==0，`BuryEffect.cs:65`）已被"排除墓侧"覆盖，实为冗余防御。干净的双区模型：**GraveSide / DeckSide**（Start Card 本体与中立卡由固定排除处理）。
- **固定排除集不一致（漂移实例）**：Revive 排除 neutral + isPassive + isMinion + revealZone + self；Bury 各方法逐个不同——`BuryEffect.cs:155`（BuryMyCards）无 isPassive、`:329` 有 isPassive（被动引擎实施时逐方法补的）。统一进 selector 的固定排除块正是本次收敛的目标。
- **排序**：`CardScript.FindCardWithMaxAttack/MinAttack` 静态方法（KINGSLAYER / SACRIFICE_WEAKEST）；`ReviveSortBy { None, MaxAttack, MaxExtraAttackTimes }` = 先 shuffle 再稳定降序，平局保持随机（`ReviveEffect.cs:158-166`）。
- **prefab 使用量**（脚本 GUID 扫描 `Assets/Prefabs`）：BuryEffect 47、StageEffect 27、ReviveEffect 22、RiftOverrideAwareReviveEffect 1 ≈ **97 个组件实例**——加法迁移下全部零改动。
- **EditMode fixture**：HeadlessCombatTestFixture 用 SerializedObject 设效果字段建卡；selector 为嵌套序列化对象，设值路径变为 `targetSelector.creatureFilter` 式 FindProperty，fixture 需加 helper（见 §5）。

## 3. 架构

### 3.1 CardSelector 数据类（纯数据，可序列化）

```csharp
[System.Serializable]
public class CardSelector
{
	public enum SelectorSide { Friendly, Enemy }
	public enum SelectorZone { Anywhere, GraveSide, DeckSide } // GraveSide = index < startCardIndex
	public enum SelectorRarity { Any, Common, Uncommon, Rare }
	public enum SelectorSort { Random, MaxAttack, MinAttack, MaxExtraAttackTimes }

	public SelectorSide side = SelectorSide.Friendly;      // 求值期按 source.myStatusRef 解析
	public SelectorZone zone = SelectorZone.DeckSide;
	public EffectScript.EffectCreatureFilter creatureFilter = EffectScript.EffectCreatureFilter.Any; // 复用现有枚举
	public SelectorRarity rarityFilter = SelectorRarity.Any;
	public string typeIDFilter = "";                       // 空 = 不过滤（信徒=RIFT 精确匹配）
	public bool enhancedOnly = false;                      // 【被强化】= attackGrowth > 0，派生判定
	public List<EnumStorage.Tag> tagFilter;                // any-match，空 = 不过滤（现 tagsToCheck 语义）
	public bool excludeSelf = true;

	// 固定排除块：默认全开，逐项可关；迁移时按 Step 0 parity 表映射各方法差异
	public bool excludeNeutral = true;    // ShouldSkipEffectProcessing
	public bool excludePassive = true;    // isPassive
	public bool excludeMinion = true;     // isMinion
	public bool excludeRevealZone = true; // combatManager.revealZone
}
```

设计约束：
- `count`（取几张）**不进 selector**——存量入口方法的数量来自 UnityEvent 参数（`BuryMyCards(int amount)`），进 selector 反而破坏签名不变原则。数量留在方法参数。
- 阵营相对性延迟到求解期：selector 不持有阵营引用，`Select(pool, source, spec)` 时按 `source.myStatusRef` 解析 Friendly/Enemy（AttackResolverSource 同款约束：myStatusRef 由 CardFactory 后赋值）。

### 3.2 CardSelectorSolver（纯静态，可单测）

```csharp
public static class CardSelectorSolver
{
	// 照 DeckCascadeLayout"纯静态、可单测"先例；不碰 Unity 生命周期
	public static List<GameObject> Select(List<GameObject> combinedDeck, CardScript source,
		CardSelector spec, out int startCardIndex);
	// 内部顺序：快照拷贝 → startCardIndex 定位（-1 时 GraveSide 返回空池 / DeckSide 视为不命中）
	// → zone 过滤 → 固定排除块 → 阵营 → 谓词（creature/typeID/rarity/enhancedOnly/tag）
	// → ShuffleList → 稳定排序（平局保随机）→ 返回有序池（取前 N 由调用方做）
}
```

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

## 4. 实施步骤（每步 gate：完成即汇报，确认后继续）

- **Step 0 审计（不动码）**：枚举 27 处池子构建 → parity 表（方法名 / 阵营 / 区域 / 固定排除集 / 谓词 / 排序 / 数量来源 / UnityEvent 绑定面）。重点核对 Bury 系固定排除集逐方法差异（:155 vs :329 漂移）与 HPAlter/AttackGiver/StatusEffectGiver 内联逻辑的可表达性。产出表格落本文件附录。*gate：用户确认 parity 表*。
- **Step 1 引擎（纯新增）**：CardSelector + CardSelectorSolver + EditMode 单测。goldens：Start Card 两侧边界 / 无 Start Card / revealZone·passive·minion·neutral 排除 / enhancedOnly 零值边界 / 稀有度 / typeID 精确匹配 / 排序平局随机 / 阵营相对性。*gate：测试全绿*。
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

## 6. 非目标

- 不动触发侧事件族 / GameEventListener / RaiseSpecific·RaiseOwner·RaiseOpponent 作用域。
- 不动 `AttackResolverSource`（攻击数值解析与目标选取是两个问题；未来若需共享"按条件收卡"原语另立计划）。
- 不改 Tag enum 本体（Tag.Revive 追加归 utility 管线 Step 1，正交推进）；不引入"隐藏 tag"显示概念。
- 不删任何 legacy 字段/分支，直至 prefab 引用清零（§4 Step 5 判据）。
- 不做通用过滤器 SO / 运行时规则组合（当前 90+ 卡规模下序列化字段组合已够；避免过度设计）。
