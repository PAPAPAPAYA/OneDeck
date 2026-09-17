# Plan: 疲劳卡复活通配（循环自毁弹药）2026-09-16

## 0. 拍板记录（2026-09-16）

- 意图 = **循环自毁弹药**：无限复活循环会把坍塌（SYSTEM_FATIGUE）反复拉上来，每次揭晓再触发一次双向掉血——疲劳从「堆在墓侧的死牌」变成循环被迫咀嚼的自毁弹药，逼对局收敛。
- 只开**复活**闸口（ReviveEffect）；BuryEffect / StageEffect 不放行。
- 4 张实体复活卡 desc 不处理（接受隐藏规则）；坍塌自身 desc 也不动。
- Notion 4.0 DB 不同步（System 卡不在 DB，本次无 desc/type 变化）。

## 1. 现状事实（2026-09-16 代码核实）

- 疲劳卡 = `Assets/Prefabs/Cards/System/Fatigue.prefab`：cardTypeID=SYSTEM_FATIGUE，名「坍塌」，desc「揭晓:对双方玩家造成 <b>1</b> 伤害」。子物体「deal 1 dmg to both players」挂 CostNEffectContainer + HPAlterEffect（DecreaseMyHp / DecreaseTheirHp，extraDmg=-1），揭晓双向 1 伤，效果配置无需改动。
- cardType 字段未序列化 = 缺省 `None`（现象）；isStartCard=0（非中立）、非 isMinion、非 isPassive——复活池通用门槛全过。
- 生成：overtime 轮（`roundNum > overtimeRoundThreshold`）或揭晓数达 `fatigueRevealThreshold` 时双方各塞 `fatigueAmount` 张，`SpawnCardForPlayer(deckIndex: 0)`（CombatFuncs.cs:35）落地即在墓侧（index 0 < startCardIndex），**无需先被揭晓**即可成为复活候选；阵营归属照常（各侧坍塌只能被己方复活拉起）。
- 类型谓词闸口三处，写法均为 `cardType != X` 互斥：ReviveEffect.cs:113-115 / BuryEffect.cs:167-169 / StageEffect.cs:331-333；筛选枚举 `{Any, Creature, Phenomenon, Token}`。
- 27 张 ReviveEffect 卡筛选分布：Any×20（已能拉）、Phenomenon×3（DEATHBED_PORTER / RIFT_REVIVER / SPIRIT_CALLER，已能拉且 desc 措辞成立）、Creature×4（BEAST_REVIVER / ELITE_REVIVER / FINAL_ESCORT / FLURRY_REVIVER，拉不到）、Token×0。本方案真实增量 = Creature 筛选这 4 张。
- `CardType` 单枚举 `{None, Creature, Token}`（EnumStorage.cs:67，append-only）——「同时算三种」只能在谓词层做通配；改 Flags 位枚举会破坏已存 prefab 序列化值，否决。

## 2. 方案：CardScript 通配旗标 + 仅复活闸口放行

### Step 1 — `Assets/Scripts/Card/CardScript.cs`

在 `isPassive` 字段附近新增序列化 bool（默认 false，全池其余 prefab 零改动）：

```csharp
/// <summary>
/// Revive-pool type wildcard (fatigue loop-breaker ammo, 2026-09-16,
/// plans/plan-fatigue-revive-wildcard-2026-09-16.md): the carrier passes any
/// ReviveEffect CreatureFilter type line, so infinite revive loops keep surfacing
/// fatigue cards and re-pay the fatigue damage on every re-reveal. Consulted ONLY
/// by ReviveEffect.PassesPredicateFilters; Bury/Stage filters, creature auras,
/// damage predicates and face/tooltip display are untouched.
/// </summary>
[Tooltip("ReviveEffect pool wildcard: passes any CreatureFilter type line (revive gate only).")]
public bool wildcardTypeFilter = false;
```

### Step 2 — `Assets/Scripts/Effects/ReviveEffect.cs`

`PassesPredicateFilters` 的类型三行加旁路；`onlyEnhanced` / `typeIDFilter` / `rarityFilter` 照走：

```csharp
// Fatigue wildcard (2026-09-16): bypass ONLY the type lines; enhancement/ID/rarity gates still apply.
if (!cardScript.wildcardTypeFilter)
{
	if (creatureFilter == CreatureFilter.Creature && !cardScript.IsCreature) return false;
	if (creatureFilter == CreatureFilter.Phenomenon && cardScript.cardType != EnumStorage.CardType.None) return false;
	if (creatureFilter == CreatureFilter.Token && cardScript.cardType != EnumStorage.CardType.Token) return false;
}
```

### Step 3 — `Assets/Prefabs/Cards/System/Fatigue.prefab`

通过 unity-mcp `execute_code`（SerializedObject）把 CardScript 的 `wildcardTypeFilter` 置 1 并保存资产；回退方案为 YAML 直改（CardScript 块 `myTags:` 行后补 `  wildcardTypeFilter: 1`）。其余 prefab 不动（缺字段 = false）。

### Step 4 — EditMode 测试 `Assets/Scripts/Editor/Tests/FatigueWildcardReviveTests.cs`

仿 `RoundEndAndBurialCountersTests` / `EnemyDamagingTargetFilterTests` 的 fixture 模式：

1. `WildcardFatigue_PassesCreatureFilter`：creatureFilter=Creature，坍塌（None + 旗标）在墓侧 → 复活池含它 / ReviveMyCards 后被移到牌顶。
2. `FatigueWithoutFlag_StillExcluded`：同配置无旗标 → 排除（默认行为回归保护）。
3. `WildcardFatigue_StillBlockedByOnlyEnhanced`：onlyEnhanced=true 且坍塌 attackGrowth=0 → 排除（ELITE_REVIVER 语义保持）。
4. `WildcardFatigue_BuryStageStillExclude`：BuryEffect / StageEffect 的筛选路径对带旗卡片仍 false（拍板「不开埋葬/置顶」的回归保护）。

### Step 5 — 验证

- 离线编译：`dotnet build Assembly-CSharp.csproj` 0 错（不开 Unity）。
- EditMode 套件由用户在 Test Runner 跑（含 4 个新用例）。
- Play 观察点（用户执行）：overtime 后 BEAST_REVIVER / FINAL_ESCORT 随机池能拉坍塌；拉上的坍塌揭晓双向 1 伤；FLURRY_REVIVER（攻击次数排序）与 ELITE_REVIVER（被强化过）依旧拉不到。

## 3. 明确不做

- BuryEffect / StageEffect 闸口；`IsCreature` / `HasAttackDisplay` / "damaging card" 谓词；BATTLE_HORN / RELIC_GRAVE_LORD 实体光环；hover 类型行；任何 desc / 中文名 / Notion DB；cardType 枚举 Flags 化。

## 4. 风险与预期管理

- **排序稀释**：MaxAttack / MaxAttackTimes sorter 会把 0 攻的坍塌排后，自毁弹药主要经**随机池**兑现（BEAST_REVIVER / FINAL_ESCORT）。若实测兑现率不足需再议 sorter 权重，本方案不碰。
- **循环边界**：复活 → 揭晓 → 双向伤 → 回底是跨轮循环，由 HP 收敛兜底，不产生同回合无限链（揭晓伤害效果不自启复活）；chainDepth 12 不涉及。
- **量变**：overtime 每轮双方各 +fatigueAmount 张可复活弹药，循环强度越高死得越快——与拍板意图一致。

## 5. 执行状态

- [x] Step 1 CardScript 字段（62f7222）
- [x] Step 2 ReviveEffect 旁路（62f7222）
- [x] Step 3 Fatigue.prefab 旗标（62f7222；YAML 直改 `wildcardTypeFilter: 1`，编辑器外完成）
- [x] Step 4 EditMode 测试（FatigueWildcardReviveTests 4 用例；用例 4 以 Stage 路径代表非复活闸口——Bury 谓词同属无旁路事实，未单测）
- [x] Step 5 编译（双程序集 0 错）+ EditMode 全量 559 绿（2026-09-17）；余 Play 观察点待用户（overtime 后 BEAST_REVIVER / FINAL_ESCORT 随机池能拉坍塌、揭晓双向 1 伤、FLURRY/ELITE 依旧拉不到）
