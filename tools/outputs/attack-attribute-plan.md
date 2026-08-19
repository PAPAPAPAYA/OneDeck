# 攻击力属性实施到 Unity 的方案

- 日期：2026-08-19
- 依据：docs/OneDeck_Final_CardPool_Design.html（149 张最终卡池设计）
- 前置调研：代码核实 —— CardScript / HPAlterEffect / EffectScript / CombatInfoDisplayer / CardPhysObjScript / ValueTrackerManager / CombatManager.GatherDecks

## 决策记录（2026-08-19 已拍板）

1. **易伤机制本期不实施**，带有易伤机制的卡片从实施范围剔除：
   - 弱点标注 MARK_WEAKNESS（U）—— 敌方受到的攻击伤害 +1（每段，可叠加，永久）
   - 弱点解析 V5_WEAKNESS_ANALYZE（R）—— 萦绕：每回合开始 敌方受到的攻击伤害 +1
   - 设计文档 A1 数值经济/关键词行的「易伤」定义、A4 自动机批次中的弱点解析行，待下次修订设计文档时一并清理；卡池总数 149 → 147
2. **卡面攻击力显示位：右下角**。具体样式不做设计，后续统一刷新 UI；本期只落地数值显示与占位。
3. **攻击力成长不跨战斗持久化**：与现「力量」状态一致 —— 每场战斗 GatherDecks 从 prefab 全新实例化，成长仅在单场战斗内跨回合保留，战斗结束即失。

## 1. 现状与差距（代码核实）

| 设计要求 | 现状 | 差距 |
|---|---|---|
| 攻击力 = 卡面基础 + 永久成长，唯一数值 | 伤害写死在 HPAlterEffect.baseDmg（IntSO）+ extraDmg + 力量层数，`<dmg>` 占位符算进 cardDesc | 新属性模型 |
| 攻击 = 攻击力 × 段数，每段独立结算 | DecreaseTheirHpTimesX(n) 循环调用 DecreaseTheirHp()，已是逐段独立（每段一次 Attack 动画 + 伤害事件） | 管线可复用，只换数值来源 |
| 攻击 ×N / 攻击 +N 次（段数可叠加、被埋葬后保留） | 段数写死在效果调用参数里 | CardScript 需要持久段数字段（设计文档 B3 标注「仍需 CardScript 新字段」） |
| 动态攻击力（攻击力 = Y 常态结算，可读敌方侧） | 只有 dynamicDmgDisplaySource 显示层枚举（TotalPowerCount / FriendlyCardCount / OpponentBuriedCount），结算层无统一入口 | 攻击力解析器，显示与结算共用 |
| +N 攻击力（原力量 + 攻击力成长合并） | StatusEffect.Power 每层 +1 伤害，走 ApplyStatusEffectCore + 事件 + 投射物动画 | 并入攻击力字段，迁移事件 |
| 增强（敌方[负面]/[诅咒] 攻击力 +1，无则生成 JU_ON） | 无；诅咒力量的授予/消耗走 CurseEffect | 新效果 + 自动生成管线（AddTempCard 已有） |
| 位置谓词（最高/最低攻击力） | 只有「最多状态效果」先例（StageEffect.StageCardWithMostStatusEffect） | 各效果类加查询方法 |

关键架构事实：
- 卡每场战斗重新实例化（CombatManager.GatherDecks 从 playerDeck.deck 的 prefab 引用 CreateLogicalCard）；状态只单场战斗内持久。
- 3.0 无费用改版后费用角位空置，但按决策显示位定在**右下角**。
- Power 枚举被 17 个文件引用（含测试），删除有序列化偏移风险，迁移必须「先加后删」。

## 2. 数据模型 — CardScript.cs

```csharp
[Header("Attack")]
[Tooltip("卡面基础攻击力（打印值，prefab 常量）")]
public int printedAttack;          // 每张卡 prefab 设定
[HideInInspector]
public int attackGrowth;           // 永久成长（原力量+成长），战斗内跨回合保留
[HideInInspector]
public int attackModThisRound;     // 本回合临时修正（虚弱诅咒 -2 等），回合开始清零
[HideInInspector]
public int extraAttackTimes;       // 攻击 +N 次（永久段数），可叠加

public int GetAttack()      // 唯一结算入口：解析器 > 基础+成长+本回合修正
public void ModifyAttack(int delta)          // 永久 ±N（增强/降攻/转移/虹吸）
public void ModifyAttackThisRound(int delta) // 本回合 ±N
public int GetAttackTimes()                  // 1 + extraAttackTimes
public void ModifyAttackTimes(int delta)
```

- 动态攻击力：CardScript 增加 `Func<int> attackResolver`（由动态攻击组件注入），GetAttack() 结算时实时调用 —— 「常态结算」天然成立，卡面显示与伤害结算同一入口。动态来源枚举仿 DynamicDmgDisplaySource：友方攻击者总和 / 友方最高 / 敌方负面总和或最高 / 墓地友方数 / 裂缝数 / 当前回合数等，配 ValueTrackerManager 新 IntSO。
- 本回合修正：现有系统无回合内临时值概念，新增字段 + beforeRoundStart 时机清零（该时机已在沉睡回合条件中使用，可复用）。

## 3. 结算 — 新 AttackEffect（或扩展 HPAlterEffect）

- `Attack()`：segments = GetAttackTimes()；每段 damage = GetAttack()；每段复用 DecreaseTheirHp 完整管线（ProcessDamage 即时扣血 + Attack 动画捕获 + CheckDmgTargets_DealingDmgToOpponent 事件与统计）。多段循环调用 —— 保持现有 xN 规则（N 次伤害应用、一链一次反应窗口）不变。
- `AttackTimes(int n)`：显式段数（苏醒之雷「其攻击 ×1」等）。
- 攻击动作完成时 Raise 新事件 `onAnyCardAttacked`（**每次攻击动作一次，非每段**）—— 战旗「友方攻击时削攻」挂此点，与每段结算解耦。
- 非攻击固定伤害（慢性侵蚀 1 点/回合）保留 baseDmg 管线不动。
- `<dmg>` 占位符退役：攻击类卡 cardDesc 只写「攻击」/「攻击 ×N」关键词，不再出现数字；AppendDynamicDamageSuffix 显示系统同步退役。

## 4. 卡面显示 — CardPhysObjScript

- 新增 `cardAttackPrint`（TMP），位置**右下角**（样式后续统一刷新，本期仅数值 + 占位）。
- 攻击 ×N 的卡显示 `X×N`。动态攻击力实时刷新，动画期间冻结由现有 SnapshotDisplayState / SetDisplayBaseline 机制覆盖（_displayCardDesc 同款）。
- 攻击力增减反馈：复用 StatusEffectChange 动画路径或新增浮动数字（CombatInfoDisplayer 模式），显示快照增量逻辑照搬现有 statusEffectDelta 机制。
- 注意：UXPrototype/ 显示改动按规范需 VISUAL-FIX(YYYY-MM-DD) 块 + docs/RegressionChecklist.md 回归行。

## 5. 力量（Power）并入攻击力 — 迁移清单

| 现状 | 迁移到 |
|---|---|
| EffectScript.ApplyStatusEffectCore Power 分支 + onAnyCardGotPower 事件家族（17 处引用） | CardScript.ModifyAttack + onAnyCardGainedAttack 事件家族（RaiseOwner/RaiseOpponent 规范不变） |
| PowerReactionEffect（攻击力渴求者 ×2、武器精灵 +1） | AttackGainReactionEffect：挂在「获得攻击力」事件上 |
| ValueTrackerManager.totalPowerCountInDeckRef（全员力量聚合，270-302 行） | 攻击力聚合 refs：友方攻击者总和 / 敌方负面总和 / 最高最低查询（供 战争英雄、人人为我、镜面诅咒、军议、军号、威慑光环） |
| CombatPerCardStatsTracker.RecordPowerGiven/Received | RecordAttackGiven/Received，钩子移到 ModifyAttack |
| CostNEffectContainer.CheckCost_EnemyCursedCardHasPower（300 行） | CheckCost_EnemyCurseCardHasAttack(N)（大召唤师 / 拔苗助长 / 萨满 / 曼哈顿博士的消耗语义） |
| CurseEffect.ConsumeHostileCursePower / TransferStatusEffectEffect | ConsumeEnemyCurseAttack / TransferAttack，动画复用 StatusEffectProjectile 语义（目标从「层」变「数字」） |

StatusEffect.Power 枚举值**保留**（避免序列化偏移），旧卡全部改写后不再有任何卡授予，最后标记 Obsolete 待清理。

## 6. 新机制落地点（本期范围，易伤已剔除）

| 机制 | 落地 | 难度 |
|---|---|---|
| 增强 | 新 EnhanceEffect.Enhance()：目标 ModifyAttack(+1)；无敌方负面/诅咒时经 CardFactory 生成 JU_ON 入敌方牌库再增强 | 低 |
| 攻击 +N 次 | extraAttackTimes 字段，Attack() 读取；亘古沉眠者 = 遍历友方攻击者 ModifyAttackTimes(+1) | 低 |
| 本回合临时降攻 | attackModThisRound + beforeRoundStart 清零 | 低 |
| 最高/最低攻击力谓词 | 各效果类加 GetCardWithMax/MinAttack（仿 StageCardWithMostStatusEffect） | 中 |
| 王座区（Start Card 前 N 张） | 新位置谓词，与「下 N 张」同族 | 中 |
| 敌方侧常态结算 | resolver 读敌方负面攻击力聚合 | 中 |
| 萦绕自动机 | 复用 Tag.Linger + beforeRoundStart（文档确认时机已存在） | 低 |

## 7. 测试

- 新增 AttackEffectTests（EditMode）：攻击力每段结算、段数（基础 ×N + extraAttackTimes 叠加）、本回合修正清零、动态解析器实时取值、0 攻击力不结算。
- 适配现有：HPAlterEffectTests（数值来源变更）、PowerReactionEffectTests → 攻击获得反应、ValueTrackerEffectTests（聚合 refs）。
- 回归重点：现有攻击旧卡（尸爆 DecreaseTheirHpTimesX + `<dmg>`）在引擎期后行为不变；攻击伤害浮动数字逐段正确（现有 DamageFloater 已逐段显示）。

## 8. 分期

1. **期 1 引擎**：CardScript 攻击字段 + AttackEffect + 卡面右下角显示 + 测试。旧卡零影响（printedAttack=0 时 Attack() 即旧行为）。
2. **期 2 迁移**：约 30 张攻击旧卡（baseDmg + `<dmg>` → printedAttack + 攻击关键词）、力量类旧卡、事件与追踪器迁移。
3. **期 3 新机制**：增强 / 消耗 / 段数 / 谓词 / 敌方侧常态结算 / 王座区。
4. **期 4 清理**：Power 枚举标记、legacy 显示系统退役、回归检查表、Notion 卡片库同步。

## 9. 待办（非本期阻塞）

- 设计文档修订时移除：弱点标注（MARK_WEAKNESS）、弱点解析（V5_WEAKNESS_ANALYZE），以及 A1 易伤关键词定义、A4 弱点解析行。
- 卡池总数同步：149 → 147（C 22 / U 68 / R 57）。
