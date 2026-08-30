# 4.0 引擎长尾实施计划（Roadmap 第 4 步）

日期：2026-08-30
上游：`plans/plan-4.0-implementation-roadmap-2026-08-28.md` 第 4 步；Notion「4.0 card database」（文案源，2026-08-30 拉取）；`docs/4.0_CardDesc_Spec.md` §嵌套触发（FINAL_ESCORT 内层触发 = 一次性延迟监听）
状态：**已实施并提交（2026-08-30，用户明示「修改代码」）**。裁决全部落地：D1=JU_ON、D2=施加方口径、D3=不含 fly、D4=带粒子、D5=光环追溯（E7）、D6 按建议（旧计数器维持受害方、新统计施加方）。实施中修正：E8（自方放逐事件）在细读 ExileEffect 后发现 `onFriendlyCardExiled` 本就是**自方自放逐**口径（仅受害卡属于放逐方式时触发）→ 回退 E8 并删除新建资产，EXILE_BERSERKER 直接绑既有事件；D6c 随之作废。批次：1 = E1/E2/E7 + 5 卡（f97e97a，EditMode 354）、2 = E3/E4/E6 + FINAL_ESCORT/RELIC_TALLY（3470a4e，362）、3 = E5 + GRAVE_GIANT/CURSE_EATER/MIMIC_BLADE（eb6c3c2，366）。收尾：listener-check 53 张（6 项疑似均为 matcher 词表误报，绑定手工核对无误）；infinity-check 无新增环（各环均被一次性/单轮触发限界打断）；Notion 同步 10 新卡 + 7 张 X_4.0 改名卡既有漂移全部「已配置」（52 行确认）。

## 0. 与 roadmap 原文的偏差

- 稀有度谓词（86 MASS_REVIVER / 99 DUO_REVIVER）：**已在第 2 步完成**（`ReviveEffect.ReviveRarityFilter`，两卡已配 `ReviveMyCards`），本步不再涉及。
- roadmap 列的 5 项 → 实际引擎缺口收敛为 6 个小改动（E1–E6），其中 GRAVE_GIANT / CURSE_EATER 两卡**零引擎、纯配置**。

## 1. 设计基线（10 张卡，Notion DB 文案）

| ID | 卡 | 文案 | 稀有度 | DB 配置状态 |
|----|----|------|--------|------------|
| 62 | BATTLE_HORN | 本回合友方生物攻击次数+1 | uncommon | 需新机制 |
| 74 | COMBO_STARTER | 攻击；被强化：攻击次数+1 | uncommon | 需新机制 |
| 95 | COMBO_GRANTER | 攻击；本回合1友方生物攻击次数+1 | uncommon | 需新机制 |
| 67 | EXILE_BERSERKER | 攻击；本回合每放逐1友方，攻击次数+1 | uncommon | 需新机制 |
| 57 | RELIC_CURSE_HASTE | 被动：敌方诅咒攻击次数+1 | rare | 需新机制 |
| 21 | FINAL_ESCORT | 攻击；遗言：回合结束：置顶1友方攻击力最高生物 | uncommon | 需新机制 |
| 38 | RELIC_TALLY | 被动：回合结束：本回合每埋葬1生物，强化1敌方诅咒 | uncommon | 需新机制 |
| 42 | GRAVE_GIANT | 攻击力=墓地友方数量；攻击 | uncommon | 需小改 |
| 91 | CURSE_EATER | 攻击；攻击力=敌方诅咒攻击力 | uncommon | 需小改 |
| 81 | MIMIC_BLADE | 攻击力=友方最高攻击力；攻击x2 | uncommon | 需小改 |

## 2. 代码事实（2026-08-30 实测）

### 已存在、直接可用

- **攻击次数原语**：`CardScript.extraAttackTimes`（永久段数，随埋/置顶保留）+ `GetAttackTimes()`（= 1 + extraAttackTimes）+ `ModifyAttackTimes(int)`——**无任何调用者**，缺授予方；角标显示 = `GetAttackForDisplay × GetAttackTimes`，段数变化自动反映。
- **墓地友方计数 term**：`AttackResolverSource.GraveyardFriendlyCount`（GRAVE_GIANT 直接用）。
- **敌方指定类型攻击合计 term**：`AttackResolverSource.EnemyNegativeTotal(cardTypeID)`（CURSE_EATER 配 JU_ON 直接用）。
- **置顶最高攻击力**：`StageEffect.StageCardWithMaxAttack()`——支持 `targetFriendly`（THE_FOOL 敌方侧在用）、已排除 isPassive / 揭晓区顶 / minion / 自身（`excludeSelf`）、平局随机；**无生物过滤**。
- **强化诅咒**：`CurseEffect.EnhanceCurse(int attackAmount)`——找一张 `cardTypeID` StringSO 指定的敌方卡加攻，**没有则按 cardPrefab 生成一张**；`EnhanceCurse_BasedOnIntSO()` 走 ownerIntSO/enemyIntSO（阵营相对）。已建被动 RELIC_ATTACK_HEX（76「有卡攻击时：强化1敌方诅咒」）即此先例 → 4.0 的「敌方诅咒」口径 = **JU_ON token**（`GameEventStorage.curseCardTypeID` 资产值实测 `JU_ON`；D1 已裁决采纳）。
- **被强化反应事件**：`onMeGainedAttack`，`EffectScript.ApplyAttackCore`（:189）`RaiseSpecific(target)`——只投递给被强化卡自身 → COMBO_STARTER 纯配置。
- **放逐事件**：`onFriendlyCardExiled`（`ExileEffect` :318-333 按**受害方**触发）。注意 `onFriendlyFlyExiled` **只有声明、无 raise 点（死事件）**，D3 裁决不含 fly 与现状一致，无需额外处理。
- **敌方诅咒揭晓事件**：`onEnemyCurseCardRevealed`（`CombatManager` 揭晓处理中按 `curseCardTypeID` 匹配触发；已建被动 RELIC_CURSE_REVIVAL / RELIC_CURSE_GRAVE 均绑它，先例成熟）。
- **每轮边界**：揭晓指针到达 start card → shuffle（逻辑期，`StartCardShuffleEffect` :112 `ResetShuffleTrackersPublic()` **此时已清** buried/staged 计数器）→ shuffle 动画 → `CombatManager.OnStartCardShuffleAnimationComplete()` → `HandleNewRoundStart()`（重置生命/本轮攻击修正/复活计数 → raise `beforeRoundStart`）。**回合结束事件的落点 = `OnStartCardShuffleAnimationComplete` 内、`HandleNewRoundStart()` 之前**：置顶落位在已洗好的新牌序顶（下一轮首揭晓，符合 FINAL_ESCORT side note「养大哥」意图），且此刻新计数器尚未清零（TALLY 可读完整一轮）。
- **既有埋葬/置顶计数器语义**：`BuryEffect.BuryChosenCards` 按**受害方**递增 `owner/enemyCardsBuriedCountRef`（受害卡 faction 决定哪个计数器 +1）；`StageEffect.StageChosenCards` 同理按**受害方**递增 `stagedOwner/EnemyRef`。均与施加方无关 → 见 §4 审计（D2 即因此改用新计数器，不动旧计数器）。施加方信息在两处递增点均可得（`myCardScript`，`RecordBury/RecordStage` 已在用）。

### 缺口（即本步引擎工作）

1. 攻击次数没有「本轮」维度：`GetAttackTimes()` 只读永久 `extraAttackTimes`。
2. 没有任何跨卡/对目标的攻击次数授予方法。
3. 没有回合结束事件（`GameEventStorage` 无 round-end 字段；注意既有资产 `BeforeRoundFinished.asset` 实为回合开始——新资产命名必须避开此坑）。
4. 没有「生物被埋葬」的每轮计数（既有计数器不分生物/非生物、受害方口径且时序不可用）。
5. 解析器缺「友方最高攻击力」term（最高攻击力只有敌方侧 `EnemyNegativeHighest`）。
6. `StageCardWithMaxAttack` 缺生物过滤。

## 3. 引擎改动清单（E1–E6）

### E1 本轮攻击次数维度 — `CardScript.cs`

- 新字段 `attackTimesModThisRound`（`[HideInInspector] public int`，镜像 `attackModThisRound` 注释风格）。
- `GetAttackTimes()` → `1 + extraAttackTimes + attackTimesModThisRound`。
- `ResetRoundAttackModifiers()` 追加 `attackTimesModThisRound = 0`（每轮 start 清零，由 `HandleNewRoundStart` 调用链保证）。
- `HasAttackDisplay` 追加 `|| attackTimesModThisRound != 0`（与 `attackModThisRound` 对齐）。
- 新方法 `ModifyAttackTimesThisRound(int delta)`（正数才生效，镜像 `ModifyAttack` 卫语句风格）。
- D5 若拍板「追溯」：BATTLE_HORN 改走 E7 全局光环（见 §5），E1 保持不变（COMBO_GRANTER 单体授予仍需要它）。

### E2 攻击次数授予效果 — 新文件 `Assets/Scripts/Effects/AttackTimesGiverEffect.cs`

继承 `AttackGiverEffect`（复用 `StatusEffectGiverEffect` 的目标谓词/选取机制与 `PassesDamageFilter` 生物过滤）。方法（均含空池 fizzle + `CombatInfoDisplayer.RefreshDeckInfo()`）：

- `GiveAllFriendlyCreaturesAttackTimes(int times)`：遍历 deck+reveal 中友方生物，逐卡 `ModifyAttackTimesThisRound(times)`，批量捕获动画。→ BATTLE_HORN（D5 未拍板追溯时）
- `GiveRandomFriendlyCreatureAttackTimes(int times)`：友方生物池随机取 1 张（含自身）。→ COMBO_GRANTER
- `GiveSelfAttackTimes(int times)`：`ModifyAttackTimesThisRound(times)` 于自身。→ COMBO_STARTER（绑 `onMeGainedAttack`）、EXILE_BERSERKER（绑 `onFriendlyCardExiled`，口径见 D6c）
- `GiveRevealedCurseAttackTimes(int times)`：读 `combatManager.revealZone`，卫语句：非空 + `cardTypeID == GameEventStorage.curseCardTypeID` + 阵营为敌方 → `ModifyAttackTimes(times)`（**永久**，挂在诅咒卡实例上；被动本体永不退场故无需回收）。→ RELIC_CURSE_HASTE

表现（D4 已裁决**带粒子**）：每个授予方法在授予后调用继承自 `StatusEffectGiverEffect` 的 `CaptureBatchStatusEffectAnimation(targets, times)`，与 `AttackGiverEffect` 各授予方法同款强化粒子；角标/文本随每段 projectile 落地逐段刷新（沿用既有 per-projectile display commit 机制）。

### E3 回合结束事件 — `GameEventStorage.cs` + 资产 + `CombatManager.cs`

- `GameEventStorage` 新字段 `public GameEvent onRoundEnd;`（全局 `Raise()`，非阵营事件；双方 TALLY/ESCORT 监听各自结算，阵营相对性由各效果内部处理）。
- 事件资产：`Assets/SORefs/GameEvents/GAME FLOW/OnRoundEnd.asset`（命名避开 `BeforeRoundFinished` 陷阱）。
- Raise 落点：`CombatManager.OnStartCardShuffleAnimationComplete()` 开头、`HandleNewRoundStart()` 之前（时序依据见 §2）。战斗首shuffle（开局洗牌）也会触发一次，届时计数为 0 / 无监听，无副作用。

### E4 每轮生物埋葬计数（施加方口径，D2 已裁决）— `ValueTrackerManager.cs` + `BuryEffect.cs` + 资产

- 新 IntSO ×2：`CreaturesBuriedByOwnerThisRoundRef` / `CreaturesBuriedByEnemyThisRoundRef`（资产 `Assets/SORefs/CombatRefs/TRACKING VALUES/BuriedAmount/`，命名显式带 By 侧）。
- `BuryEffect.BuryChosenCards` 既有受害方计数块旁追加：victim `isCreature` **且** 施加方（`myCardScript`）有阵营 → 按**施加方** faction 递增对应计数器（施加方为中立/空 → 跳过）。受害方口径的既有两计数器不动。
- 消费：`GetIntSOForOwner(ownerIntSO, enemyIntSO)` 阵营相对读取——我的 TALLY 读 ByOwner（我方造成的埋葬，含我方自献祭埋我方生物；不含敌方造成的埋葬）。
- 重置点：`HandleNewRoundStart()`（roundEnd raise 之后才执行 → TALLY 读取时值完整）。

### E5 解析器 term — `AttackResolverSource.cs`

- enum 新增 `FriendlyHighest`；`Resolve()` 加 case → `HighestAttack(myCardFaction: true, typeID: term.cardTypeID)`（既有私有方法，自带排除自身）。→ MIMIC_BLADE

### E6 置顶最高攻击力加生物过滤 — `StageEffect.cs`

- `StageCardWithMaxAttack` 新增 serialized 字段 `creatureOnly`（默认 false，THE_FOOL prefab 不动）；过滤分支追加 `|| (creatureOnly && !cardScript.isCreature)`。→ FINAL_ESCORT

## 4. 既有统计口径审计（回应「怀疑之前有统计包含敌方」）

| 计数器/事件 | 口径 | 递增/触发点 | 现有消费方 | 敌方泄漏 |
|---|---|---|---|---|
| `owner/enemyCardsBuriedCountRef`（本回合埋葬数） | **受害方** | `BuryEffect.BuryChosenCards` | BONE_COMBINATION（`AttackTimesBasedOnOpponentBuriedCount`）、`HPAlterEffect` 伤害=数/次数=数 两方法、卡面动态显示 `OpponentBuriedCount` | ✅ 有：敌方埋自己的卡也计入我读的「被埋葬的敌方数」 |
| `stagedOwner/EnemyRef`（本回合置顶数） | **受害方** | `StageEffect.StageChosenCards` | `GiveStatusEffectToXFriendly_BasedOnStaged` / `GiveAttackToXFriendly_BasedOnStaged` | ✅ 有：敌方置顶**我的**卡计入我读的「我方置顶数」 |
| `owner/enemyRevivedCount(ThisRound)Ref` | **受害方** | `ReviveEffect` :230-239 | 暂无外部消费（B 组 7 REANIMATOR 未来用） | ⚠️ 反向漏：我复辟**敌方**诅咒 → 计入敌方侧，我方「本回合复活数」读不到 |
| `onFriendlyCardExiled` 事件 | **受害方** | `ExileEffect` :318-333 | EXILE_BERSERKER（本步待配）等 | ✅ 有：敌方放逐我的卡也触发 |
| `onFriendlyFlyExiled` 事件 | — | **无 raise 点（死事件）** | 无 | — |
| `onFriendlyCardBuried` / `onAnyCardBuried` 事件 | **受害方** | `BuryEffect` | 复仇类卡（AVENGER 等，已配） | 设计意图：复仇语义需要受害口径，非遗漏 |
| 墓地存量 `owner/enemyInGraveAmountRef` / 解析器 `GraveyardFriendlyCount` | 区域 | index < startCardIndex 实时统计 | BODY_CANON、GRAVE_GIANT | 不涉及施加方 |
| `CombatPerCardStatsTracker.RecordBury/RecordStage` | **双口径**（施加方分裂 Friendly/EnemyBuried + 受害方 TimesBuried） | BuryEffect/StageEffect | 结果面板 | 已正确分侧 |

审计结论：用户的怀疑成立——**既有每轮计数器全部是受害方口径**。新统计（E4）已改施加方；既有计数器是否统一改口径见 D6a/D6b。

## 5. 卡片配置矩阵

| 卡 | 触发绑定 | 效果绑定 | 依赖 |
|----|---------|---------|------|
| BATTLE_HORN | onMeRevealed | 追溯方案（E7）：置 `creatureAttackTimesAuraThisRound` 阵营光环；非追溯方案：`GiveAllFriendlyCreaturesAttackTimes(1)` | E1 E2/E7（D5） |
| COMBO_STARTER | 攻击（Attack）；onMeGainedAttack | `GiveSelfAttackTimes(1)` | E1 E2 |
| COMBO_GRANTER | 攻击（Attack）；onMeRevealed | `GiveRandomFriendlyCreatureAttackTimes(1)` | E1 E2 |
| EXILE_BERSERKER | 攻击（Attack）；放逐触发 | `GiveSelfAttackTimes(1)`；触发事件口径见 D6c | E1 E2 |
| RELIC_CURSE_HASTE | isPassive；onEnemyCurseCardRevealed | `GiveRevealedCurseAttackTimes(1)` | E1 E2 |
| FINAL_ESCORT | 攻击（Attack，ATK 1）；onMeBuried | 遗言置位标志 + 常驻 onRoundEnd 监听：标志为真 → `StageCardWithMaxAttack(creatureOnly, targetFriendly=true)` + 清标志 | E3 E6 |
| RELIC_TALLY | isPassive；onRoundEnd | `EnhanceCurseTimes_BasedOnIntSO`（循环 count 次 `EnhanceCurse(1)`），ownerIntSO=`CreaturesBuriedByOwnerThisRoundRef` / enemyIntSO=`CreaturesBuriedByEnemyThisRoundRef`；子 CurseEffect 配 cardTypeID=JU_ON + cardPrefab | E3 E4 |
| GRAVE_GIANT | 攻击（Attack） | AttackResolverSource term `GraveyardFriendlyCount` | 无 |
| CURSE_EATER | 攻击（Attack） | AttackResolverSource term `EnemyNegativeTotal(cardTypeID=JU_ON)` | 无 |
| MIMIC_BLADE | 攻击（Attack，`extraAttackTimes=1` 即 ×2） | AttackResolverSource term `FriendlyHighest` | E5 |

实现说明：

- **FINAL_ESCORT 零新组件**：遗言（onMeBuried）只置一个布尔标志（新小 effect 方法 `ArmRoundEndStageMaxAttackCreature()`），prefab 常驻监听 onRoundEnd → 效果先查标志、为真才置顶并清标志。卡被埋但对象存活，标志随之存活；复活后再埋可再次武装。
- **RELIC_TALLY 强化语义**：B 次埋葬 → 循环 B 次 `EnhanceCurse(1)`（每次独立找/生成一张 JU_ON，与 RELIC_ATTACK_HEX 每次攻击强化 1 张一致），非「一张诅咒 +B」。新包装方法 `EnhanceCurseTimes_BasedOnIntSO()` 放 `CurseEffect`。
- **COMBO_GRANTER** 随机含自身（文案「1友方生物」无限定）。
- 事件资产接线：新 GameEvent 字段需在场景中 GameEventStorage 组件上拖资产（注意存盘 dirty；`runtests-save-scene-first` 约束同样适用）。

## 6. 开放决策

- **D1 敌方诅咒口径**：✅ 已裁决 = JU_ON token（三处既有先例一致）。
- **D2 TALLY 埋葬计数口径**：✅ 已裁决 = **施加方口径**（统计卡片持有方造成的埋葬，含持有方自献祭埋己方生物；不含敌方造成的埋葬）→ E4 双计数器；既有受害方计数器不动（是否一并改口径 → D6a/D6b）。
- **D3 EXILE_BERSERKER 放逐范围**：✅ 已裁决 = 不含 fly。补充事实：`onFriendlyFlyExiled` 本就是死事件（无 raise 点），裁决与现状一致。
- **D4 攻击次数授予表现**：✅ 已裁决 = 带粒子（与强化授予同款，见 E2）。
- **D5 BATTLE_HORN 追溯（待拍板）**：现状方案（实例级授予）= 揭晓瞬间给当时在场生物 +1；**本回合之后才生成的卡（信徒 token、AddTempCard 临时卡、RIFT 召唤）吃不到**；复活/置顶/埋葬是实例移动不受影响。追溯方案（E7）：`owner/enemyCreatureAttackTimesAuraThisRoundRef` 阵营级光环，`GetAttackTimes()` 对 `isCreature` 卡读取己侧光环值，`HandleNewRoundStart` 清零——后生成生物自动覆盖、下轮自动失效。推荐 E7（语义即「本回合友方生物攻击次数+1」的直觉读法，且省去逐卡授予的遍历）。
- **D6 既有统计口径（待拍板，基于 §4 审计）**：
  - **D6a** 本回合埋葬计数器（BONE_COMBINATION 等 3.0 消费方）：维持受害方（文案「被埋葬的敌方数量」本就是被动语态）或改施加方（动 3 个消费点 + 卡面显示，改变现有卡平衡）。推荐维持。
  - **D6b** 置顶计数器（BasedOnStaged 两方法）：同上。推荐维持。
  - **D6c** EXILE_BERSERKER 触发口径：绑受害方 `onFriendlyCardExiled`（敌方放逐我的卡也+1）或新增「自方造成的友方放逐」口径事件（ExileEffect 按施加方追加 raise，约 +10 行）。按 D2 精神推荐后者。
  - **D6d** 复活计数器反向漏（复辟敌方诅咒不计入我方复活数）：本步无消费方，B 组 7 REANIMATOR 配置前再拍板；届时加施加方口径计数器即可，不在本步实施。

### E7（D5 追溯方案时启用）— 阵营生物攻击次数光环

- `ValueTrackerManager` 新 IntSO ×2：`CreatureAttackTimesAuraOwnerThisRoundRef` / `CreatureAttackTimesAuraEnemyThisRoundRef`（资产 TRACKING VALUES 下）。
- `CardScript.GetAttackTimes()`：`isCreature` 时追加读取己侧光环值（faction 相对）。
- `HandleNewRoundStart` 清零；BATTLE_HORN 揭晓 → 己侧光环 +1。

### E8（D6c 拍板「自方造成」时启用）— 自方放逐事件

- `GameEventStorage` 新事件 `onCardExiledByOwnSide`（施加方视角 RaiseOwner/RaiseOpponent）；`ExileEffect` 既有触发点旁追加 raise（源与受害同侧时才触发，即 self-exile）。
- EXILE_BERSERKER 改绑该事件。

## 7. 实施顺序与验证

1. 批次 1（E1+E2(+E7/E8 视 D5/D6c) → BATTLE_HORN / COMBO_STARTER / COMBO_GRANTER / EXILE_BERSERKER / RELIC_CURSE_HASTE）→ EditMode 测试（`AttackEffectTests` / 新 `AttackTimesGiverEffectTests`，fixture 参照 `HeadlessCombatTestFixture`；注意 OnEnable 不跑、疲劳阈值 999 等既有坑）→ listener-check 本批 → 提交。
2. 批次 2（E3+E4+E6 → FINAL_ESCORT / RELIC_TALLY）→ 测试（roundEnd 时序：逻辑清零 vs 动画完成点；TALLY 施加方口径计数/清零断言——敌方造成埋葬不计数）→ listener-check → 提交。
3. 批次 3（E5 → GRAVE_GIANT / CURSE_EATER / MIMIC_BLADE）→ 解析器断言并入既有 AttackAttributeConsistencyTests → 提交。
4. 引擎落地后跑 `unity-card-infinity-check`（重点：COMBO_STARTER 被强化→+次数 是否与强化源成环；RELIC_TALLY 埋葬→强化诅咒→诅咒揭晓攻击 是否自馈）。
5. Notion「Unity 配置状态」列同步（需新机制 → 已配置）。

## 8. 约束

- 每批次实施前需用户明示「修改代码」（AGENTS.md）。
- 新 log tag 需登记 `TestManager.InferCategory`，否则警告并静默吞日志。
- prefab 批量绑定沿用 A 组约定（SerializedObject 直改 AttackTimes/int/Object 参数；事件资产名先查重）。
- 本步不触碰第 5 步范围（61 张剩余卡、displayName 中文命名、3 张备用卡）。
