# 4.0 复活/苏醒引擎实施计划（ReviveEffect + onMeRevived）

日期：2026-08-29
上游：`plans/plan-4.0-implementation-roadmap-2026-08-28.md` 第 2 步（主线）；`docs/4.0_CardDesc_Spec.md` §Revive & Awaken（ratified 2026-08-26）
状态：**已实施（2026-08-29，用户明示「修改代码」后执行）**。开放问题裁决：#1 isPassive 预留=加；#2 不重置 currentLife/本轮攻击修饰（实现即默认行为）；#3 RIFT token 收窄确认；#4 OnAnyCardRevived 复用。实施偏差：稀有度过滤器（开放问题 #1 之外）直接实现而非留空钩子——`CardScript.rarity` 已存在，实现零成本且避免"配置后静默 fizzle"陷阱。

## 1. 设计基线（已批准，spec 摘录）

- 复活 = 墓地 → 卡组顶；延迟复活 = 墓地 → start card 前一槽（queue tail，index = startCardIndex + 1，与 R2 bounce 同落位）。
- 苏醒（OnMeRevived）= "when revived"，**仅由复活效果触发**——Stage / R2 bounce 不触发。
- 选取：仅 `index < startCardIndex`；排除被动（isPassive，第 3 步落地）与中立卡；已放逐与揭晓区卡不可复活；空墓地 = fizzle。
- 复活时：raise onMeRevived + faction 复活事件 + 每回合复活计数器。
- 术语：目标在墓地一律写「复活」，不写「置顶」；信徒 token 的「置顶1友方」→「复活1友方」，「置顶敌方诅咒」→「复活敌方诅咒」。

## 2. 代码事实（2026-08-29 逐项实测）

### 2.1 可复用件

| 件 | 位置 | 用途 |
|----|------|------|
| `CombatManager.GetStartCardIndex()` | CombatManager.cs:490 | 墓地边界 |
| `CombatManager.MoveCardToThroneZone()` | :524 | **延迟复活落位直接复用**（insert at startCardIndex+1，已处理移除补偿；注释即"Start Card 前"语义） |
| `ResolveGravePlacement`（R2 bounce） | :1003 | bounce 不触发苏醒的边界佐证（bounce 走grave-placement，不走复活路径） |
| `StageEffect.StageChosenCards()` | StageEffect.cs:358 | 移动模板：逻辑 Remove+Add(顶) → 快照 → ValueTracker 计数 → 动画捕获 |
| `BuryEffect` 方法面 | BuryEffect.cs | 选区模板：BurySelf / BuryMyCards / BuryMyCardsWithTag / BuryTheirCards / BuryTheirCardsWithTag / BasedOnIntSO / 私有 GetStartCardIndex / IsCardBelowStartCard / IsCardAtBottom |
| 事件 raise 惯例 | CombatManager.cs:948 | card-specific 用 `RaiseSpecific(card)`（onMeRevealed 先例）；faction 用 `RaiseOwner()/RaiseOpponent()` |

### 2.2 legacy Revive 残留清单（清理对象）

| 位置 | 内容 | 处置 |
|------|------|------|
| EnumStorage.cs:34 | `StatusEffect.Revive`（隐式值 6） | **保留槽位 + 注释弃用**。枚举无显式值，删除会使 Counter 7→6，破坏所有已序列化资产的 int 引用——spec 的"remove or comment"在隐式枚举下只能取"注释弃用" |
| CostNEffectContainer.cs:135 | `CheckCost_Revive(int)` | 删除（唯一 prefab 使用者同步删，见下行） |
| Assets/Prefabs/StatusEffectResolvers/Undead Resolver.prefab | 唯一引用 CheckCost_Revive 的资产 | 删除（删前全仓 grep 确认无场景/池引用） |
| EffectScript.cs:263 | StatusEffect→显示串「复活」分支 | 删除分支 |
| TransferStatusEffectEffect.cs:445 | 同上 | 删除分支 |
| CombatInfoDisplayer.cs:246-257 | `[N Revive]` 显示逻辑 | 删除分支 |
| AGENTS.md「Graveyard Removed」条目 | 声称 `CardManipulationEffect.Revive*` 是 no-op | **已过时**（方法已不存在），顺手修正该行 |

### 2.3 事件现状

- `GameEventStorage` **无任何 revive 字段**（GameScene 场景组件 dump 证实）。
- `Assets/SORefs/GameEvents/_OTHER/OnAnyCardRevived.asset` 已存在（GUID 0d43dad2…），但无字段承载，仅被 2 张 _DEPRECATED 1.0 prefab 引用 → **复用**为全局复活事件。
- 注意命名陷阱：`GAME FLOW/BeforeRoundFinished.asset` 实为「回合开始」事件（见 spec 触发表 2026-08-29 注记）。新建复活事件资产放 `_OTHER/`，命名一次做对。

### 2.4 复活源选择器需求（迭代文档 87 卡枚举）

| 选择器 | 需求卡（迭代文档 ID） |
|--------|----------------------|
| 复活自身 | 53 UNDYING_WARRIOR、101 CURSE_THIRST_BEAST |
| 复活1/2友方（随机） | 27 NECROMANCER、35 SOUL_TRADER、36 REVIVE_SUMMONER、73 KINGSLAYER、94 GRAVE_HEXER |
| 苏醒触发的复活（触发=OnMeRevived，效果仍是友方复活） | 16 RIFT_MEDIUM、27、59 RELIC_CURSE_REVIVAL（被动层，第 3 步） |
| 延迟复活1友方 | 32 FUNERAL_WILL、40 MASS_SACRIFICE（并生成信徒延迟复活） |
| 谓词：信徒 typeID | 49 RIFT_SHEPHERD |
| 谓词：非生物 | 61 SPIRIT_CALLER、109 RIFT_REVIVER |
| 谓词：生物 | 89 BEAST_REVIVER |
| 谓词：被强化+生物 | 64 ELITE_REVIVER |
| 谓词：攻击次数最多 | 71 FLURRY_REVIVER |
| 稀有度谓词（normal/uncommon） | 86 MASS_REVIVER、99 DUO_REVIVER —— **第 4 步稀有度谓词**，本期只留钩子 |
| 复辟：复活敌方（最高攻 / 诅咒） | 54 GRAVE_ROBBER、65 DOOM_HERALD、70 CURSE_REVIVER、80 CURSE_GARDENER、85 RELIC_RIFT_OVERRIDE（信徒重写，被动层） |
| 计数消费：攻击力=本回合复活友方数 | 47 REANIMATOR |

## 3. 引擎设计

### 3.1 新文件 `Effects/ReviveEffect.cs`（MonoBehaviour，镜像 BuryEffect/StageEffect 惯例）

序列化面：

- `reviveTarget` enum { MyCards, TheirCards, Self }
- `amount` int（UnityEvent int 参数亦可，二选一以 UnityEvent 传参为准，与 Bury 一致）
- `delayedRevive` bool（true → `MoveCardToThroneZone` 落位 startCardIndex+1）
- `creatureFilter` enum { Any, Creature, NonCreature }
- `typeIDFilter` string（"" 不过滤；"RIFT" 信徒；敌方诅咒走 curse typeID，参照 `GameEventStorage.curseCardTypeID` 的取法）
- `sortBy` enum { None, MaxAttack, MaxExtraAttackTimes }
- `rarityFilter` enum { Any, Normal, Uncommon, Rare }（**本期仅 Any 生效**，第 4 步接实现）

UnityEvent 可绑方法（镜像 Bury 命名）：

- `ReviveSelf()`
- `ReviveMyCards(int amount)`
- `ReviveMyCardsWithTag(int amount)`
- `ReviveTheirCards(int amount)`
- `ReviveTheirCardsWithTag(int amount)`
- `ReviveMyCards_BasedOnIntSO()` / `ReviveTheirCards_BasedOnIntSO()`（存量计数变体，仅在有卡需要时加）

私有核心 `ReviveChosenCards(List<GameObject> cards, int amount)` 流程：

1. 过滤（faction + creatureFilter + typeIDFilter + rarityFilter + sortBy；随机源先 `ShuffleList` 再取前 N，sortBy 非空时先排序后取）。
2. Guard：目标不在 `combinedDeckZone`（已放逐/销毁）跳过；`revealZone` 中的卡显式排除（防御性，选区按理论不可达仍加 guard）；`isPassive` 排除（见开放问题 #1）。
3. **快照目标索引**（AGENTS：移动效果须在 raise 反应事件前快照，防反应链改序污染）。
4. 逻辑移动：`combinedDeckZone.Remove(card)` → delayed ? `MoveCardToThroneZone(card)` : `combinedDeckZone.Add(card)`（顶）。
5. 计数：`ValueTrackerManager` 新增 IntSO —— `ownerRevivedCountRef` / `enemyRevivedCountRef`（全场累计）+ `ownerRevivedCountThisRoundRef` / `enemyRevivedCountThisRoundRef`（每回合），镜像 `stagedOwnerRef` 模式（static+instance 双访问供测试）；每回合计数在 `HandleNewRoundStart()` 重置（与 currentLife 重置同点）。
6. 事件：`onMeRevived.RaiseSpecific(card)`；`onFriendlyCardRevived.RaiseOwner(card)` / `onEnemyCardRevived.RaiseOpponent(card)`（按被复活卡 faction）；`OnAnyCardRevived.Raise()`。
7. 动画捕获：对齐 StageEffect 现行捕获模式（PopUpBatch + SlotInBatch 家族），实现时逐行对照并过 VISUAL-FIX 规范（`docs/VisualBugPrevention_Guide.md`）。
8. **苏醒语义边界**：苏醒事件 raise 只存在于 `ReviveChosenCards` 内——Stage / bounce / R2 走各自路径，架构上不可能误触发。

### 3.2 GameEvent 资产与接线

- 新建 `_OTHER/OnMeRevived.asset`、`_OTHER/OnFriendlyCardRevived.asset`、`_OTHER/OnEnemyCardRevived.asset`。
- 复用 `_OTHER/OnAnyCardRevived.asset`。
- `GameEventStorage` 增 4 个 public 字段（`onMeRevived` / `onFriendlyCardRevived` / `onEnemyCardRevived` / `onAnyCardRevived`），GameScene 场景组件接线（编辑器脚本 SerializedObject 改场景对象 + 保存场景）。

### 3.3 RIFT token（信徒）切换

- 现状：listener(OnMeRevealed) → 两容器（`ExileEffect.ExileSelf` + `StageEffect.StageMyCards(1)`），desc「揭晓时:置顶 1 友方,去除自身」。
- 改：`StageMyCards(1)` → `ReviveEffect.ReviveMyCards(1)`（复用同一容器，仅换 effectEvent 绑定）；desc → 「揭晓时:复活 1 友方,去除自身」。
- 行为差异声明：token 从「加速器（可置顶任何非顶友方）」收窄为「纯复活器（仅墓地）」。与 HATCHERY 的组合由「随机置顶」变为「定向复活」。此为 ratified 语义（信徒=复活）。

### 3.4 与现有系统的交互确认

- HATCHERY：复活→揭晓→3信徒→底部→下轮自埋循环重建（2026-08-29 已验证的环）✓
- UNFINISHED_ROBOT：复活→揭晓→攻击力再翻倍，指数放大——引擎落地后 infinity-check 必复检（Roadmap §3 已排）✓
- 敌我共用一张混合牌堆：复辟（敌方诅咒/最高攻）= 同一墓地选区 + faction 过滤 ✓
- 被复活卡的 `currentLife` / 本轮攻击修饰：**维持原值不重置**（spec 未言明，取最小语义，见开放问题 #2）

## 4. 实施步骤（获「修改代码」后按序）

1. legacy 清理（§2.2 全表）→ 编译通过
2. GameEvent 资产 ×3 新建 + 复用 1 + GameEventStorage 字段 + GameScene 接线
3. `ReviveEffect.cs`（核心 + 选择器 + 计数 + 事件 + 动画捕获）
4. `CardScript.isPassive` 预留字段（若开放问题 #1 批准；默认 false、零行为）
5. RIFT token 切换（§3.3）
6. 复跑提取 + listener-check + infinity-check（重点 ROBOT×复活、HATCHERY×复活节奏）
7. spec 触发表「苏醒」❌→✅、动词表「复活/延迟复活」❌→✅ 更新

## 5. 测试计划（EditMode，HeadlessCombatTestFixture 体系）

| 用例 | 断言 |
|------|------|
| 选区边界 | start card 之上（未揭晓区）的卡不可被选中；仅 index < startCardIndex 可选 |
| faction 过滤 | ReviveMyCards 只取己方；ReviveTheirCards 只取敌方 |
| 谓词 | creatureFilter/typeIDFilter/sortBy 各一例（BEAST_REVIVER/RIFT_SHEPHERD/GRAVE_ROBBER 语义） |
| fizzle | 空墓地 → 无移动无事件无异常 |
| 落位 | 普通复活 → index == Count-1；延迟复活 → index == startCardIndex+1 |
| 事件 | onMeRevived / faction / any 各恰 raise 1 次 |
| 苏醒边界 | 对同一卡执行 StageMove → onMeRevived 不 raise（反例） |
| 计数 | this-round 计数在 HandleNewRoundStart 后归零，全场累计保留 |
| legacy | CheckCost_Revive 删除后全仓无引用、编译通过 |

## 6. 开放问题（需拍板，默认值即建议）

1. **isPassive 预留**：本期在 `CardScript` 加 `isPassive` bool（默认 false、零行为），复活排除点一次到位；否则第 3 步被动卡落地时 ReviveEffect 要二次改。建议：加。
2. **复活是否重置 currentLife / 本轮攻击修饰**：默认维持原值（最小语义）。
3. **RIFT token 收窄**：置顶加速能力永久移除，确认预期（believer=复活语义）。
4. **OnAnyCardRevived.asset 复用**：默认复用现资产（名字正确、无活引用）。
