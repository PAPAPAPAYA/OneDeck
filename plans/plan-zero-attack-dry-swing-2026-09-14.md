# Plan: 0 攻击力生物空挥（dry swing）— 2026-09-14

状态：dry-swing 主体**待拍板，未实施**（等「修改代码」）；关联改动「GRAVE_ROBBER 补攻击」已于 2026-09-14 按纯 prefab 方案**实施完毕**（见下）。

## 背景与根因

- 现状门禁：`AttackEffect.AttackTimes` / `AttackSelfTimes`（`Assets/Scripts/Effects/AttackEffect.cs:44` / `:115`）
  `GetAttack() <= 0` 直接 return。0 攻生物根本不进 segment 循环：
  无伤害管线、无攻击动画捕获、无 `onAnyCardAttacked` / `onAnyFriendlyCardAttacked`。
- 受影响卡（0 攻时刻整卡哑火；9 张 = 3 张 `AttackResolverSource` 动态 + 6 张静态 0 攻靠成长/强化）：
  收蛊人 CURSE_REVIVER / 蛊婆 HEXER / 吞蛊人 CURSE_EATER / 报丧人 DOOM_HERALD（绑 `AttackTimes` 变体）/
  骸骨巨人 GRAVE_GIANT / 咒刃 HEXBLADE / 模仿犯 MIMIC_BLADE（x2）/ 百鬼夜行 REANIMATOR / 缝合人 UNFINISHED_ROBOT_4.0。
  注：食尸鬼 GRAVE_ROBBER **不在此列**——prefab 无 AttackEffect，攻击力经 `attackSnapshotValue` 生效
  （`CardScript.cs:177`），不受本门禁影响；其 desc 第三句「攻击」原本无出口，已由关联改动补上。
- `PerformAttackAs` 链路（垂死反扑 DEATHBED_GRANT、操尸人 GRAVE_PUPPETEER）最终也走同一门禁
  （`EffectScript.cs:557` → `AttackEffect.Attack()`）。

## 关联改动（已实施 2026-09-14）：GRAVE_ROBBER 补攻击

- 背景：cardDesc「复活 1 攻击力最高敌方;攻击力变为该卡攻击力;攻击」——第三句「攻击」在 prefab 中无出口
  （审核发现：无 AttackEffect 组件、无攻击绑定，snapshot 只被外部读取）。
- 方案（拍板：纯 prefab，不动代码）：子 GO `rob the strongest` 追加 `AttackEffect` 组件
  （序列化形态照抄 4.0 攻击卡模板，baseDmg 引用同款 IntSO）；现有 container 的 `effectEvent.m_Calls`
  追加第二个调用 `AttackEffect.Attack`，**排在 `ReviveStrongestEnemyAndSnapshot` 之后**
  （同一 UnityEvent 内按序列化顺序执行 → 先算 snapshot、后结算攻击）。
- 落空语义：复活失败时 `attackSnapshotValue` 保持 -1 → `GetAttack()=0` → 现行 `<=0` 门禁下不攻击；
  dry-swing 落地后该形态变为**空挥**（动画 + 攻击事件，拍板时已知悉并接受）。
- 攻击方向：敌方（标准 `Attack()` → `DecreaseTheirHp` 路径；血契转换、per-card stats、攻击事件均照常适用）。
- 已验证（2026-09-14）：prefab 导入无 YAML 错误；`effectEvent` 2 个调用顺序为
  `[ReviveStrongestEnemyAndSnapshot, Attack]`；AttackEffect 挂在 `rob the strongest`。

## 设计拍板项（推荐案）

1. 门禁改 `GetAttack() < 0`：0 可攻；负攻仍不攻（负 = 被削弱到负，语义"无攻击"；负值进 `ProcessShieldNHp`
   会反向加盾，故负攻绝不能进管线）。
2. 0 伤害 segment = 空挥：不进 HPAlter 管线 →
   不触发 `onMyPlayerTookDmg` / `onTheirPlayerTookDmg`（否则 ETERNAL_GHOST 等"受到伤害"响应会在 0 伤害时误触发，
   语义污染；已核实 ETERNAL_GHOST 监听 `OnTheirPlayerTookDmg`），不写战斗日志"造成[0]点伤害"。
   但捕获 Attack 动画请求（`onHit=null`）+ 照常 `RaiseAttackEvents`（每 segment 一次）。
   注：per-card stats 无需特别处理——`RecordDamage` 自带 `amount <= 0` 早退
   （`CombatPerCardStatsTracker.cs:218`）；且"0 伤害不触发受伤响应"**仅适用于攻击路径**，
   非 AttackEffect 的 HPAlterEffect 在 0 伤害时仍触发 tookDmg（现状语义，有意保留，拍板时知悉）。
3. 每 segment 进入时重读 `totalDmg = GetAttack() + extraDmg`：前段反应加了攻 → 后段照常造成伤害
   （与现 per-segment 语义一致，`DecreaseTheirHp` 每次调 `ComputeTotalDamage()`，`HPAlterEffect.cs:455`）。
4. 血契 RELIC_BLOOD_PACT：`totalDmg <= 0` 的 segment 跳过转换分支（转 0 无意义；
   现行 `EnhanceCurseForBloodPact` 用 `GetAttack()`，`AttackEffect.cs:94`）。

## 改动点

仅 `Assets/Scripts/Effects/AttackEffect.cs`（dry-swing 主体）：

- `AttackTimes`(:44) / `AttackSelfTimes`(:115)：门禁 `GetAttack() <= 0` → `GetAttack() < 0`。
- segment 循环内按 `int totalDmg = GetAttack() + extraDmg;` 分支：
  - `totalDmg > 0`：现有路径（血契 / `DecreaseTheirHp` 或 `DecreaseMyHp` + `RaiseAttackEvents`）。
  - `totalDmg <= 0`：新私有 `CaptureDrySwingAnimation(bool isAttackingEnemy)`
    （复制 HPAlterEffect 的 recorder 捕获段 `AnimationRequestType.Attack`，`onHit=null`，
    不 SnapshotHpDisplay / CommitHpDisplay）+ `RaiseAttackEvents(isSelf)`；血契分支跳过。
  - 自攻方向照 `DecreaseMyHp` 口径：`isAttackingEnemy = myStatusRef != ownerPlayerStatusRef`（**HPAlterEffect.cs:161**）。
- 动画安全性**已核实（2026-09-14）**：两条路径 `onHit` 均可空——`CombatUXManager.cs:4191`（fallback
  `onHit?.Invoke()`）与 `AttackAnimationManager.cs:301`（`data.onHit?.Invoke()`）。headless（无 recorder）自然无动画，与现状一致。
- 附带清理（改动前置，防止 common 卡白喂被动）：移除 **RIFT_ACOLYTE** / **SACRIFICIAL_SPIRIT** 的
  OnMeRevealed 死攻击绑定（审核发现：两卡 `printedAttack 0` 但 `effectEvent` 活绑 `AttackEffect.Attack`，
  desc 均无攻击语义，系建卡模板残留；今日靠 0 攻门禁兜底成 no-op，dry-swing 落地后每次揭晓都会空挥 +
  触发攻击事件喂三张被动）。
- 附带核查：用脚本 sweep 一遍 3.0 池 32 张带 AttackEffect 的卡（有无 `printedAttack 0` + 活绑定）——
  口径注意：`AGENTS.md` 仍标注 3.0 为 current，本文按「4.0 = 现行池」处理，需拍板确认。
- 记录在案：空挥同样覆写 `combatManager.lastCardAttacked`（`RaiseAttackEvents` 内）；
  当前全代码无 gameplay 读取（仅测试），低风险。
- 明确不动：CurseEffect 消耗攻击的 `GetAttack() <= 0 continue`（`CurseEffect.cs:501`，0 攻无可消耗，by design）、
  ConsumeStatusEffect 同理（`ConsumeStatusEffect.cs:244`）、`HasAttackAttribute` 谓词不变。

## 行为影响面（拍板前须知）

- 三张攻击时点被动吃满燃料：埋骨地（每次攻击埋顶卡）/ 饲咒（强化诅咒）/ 以血布道（生成信徒；
  三卡 cardDesc 均为「友方攻击时…」，触发事件为 `onAny(Friendly)CardAttacked`，已核实）。
  9 张 0 攻生物每次揭晓都成为引擎燃料；GRAVE_ROBBER 复活成功后也加入（新增攻击源）。
  强度面变化大——这正是本次目的，但需要平衡观察。
- 垂死反扑 / 操尸人对 0 攻被埋生物从无操作变空挥 → 与埋骨地构成更长递归链
  （chainDepth 99 + 同卡同实例守卫兜底；实施后建议跑 unity-card-infinity-check）。
- xN 卡（模仿犯 x2）空挥 = N 次动画 + N 次攻击事件（符合 per-segment 规则）。
- legacy `extraDmg>0` 且 `printedAttack=0` 的 3.0 退役卡会从"不攻"变"造成 extraDmg 伤害"；
  4.0 现行池无此形态，风险仅限退役池。
- 战斗节奏：0 攻生物每次揭晓多一段 lunge 动画，autoReveal 下节奏变长。
- 语义边界：本次只统一「攻击」路径的 0 伤害语义；法术类 HPAlterEffect 0 伤害仍触发受伤响应（现状保留）。

## 测试

- 改写 3 个存量断言（`AttackEffectTests.cs:63` / `:157` / `:189`）为新语义：
  0 攻 → HP 不变 + 有 recorder 时动画请求捕获 + `onAnyCardAttacked` 每 segment 1 次
  + `onMy/onTheirPlayerTookDmg` 不触发。
- 新增：负攻仍不攻；空挥 x2 = 2 事件 2 动画；空挥中段被加攻后后续 segment 造成伤害；
  血契 + 0 攻不 EnhanceCurse。
- 新增（关联改动回归）：GRAVE_ROBBER 时序测试（仿 `Step5BatchBEngineTests` 风格：复活 snapshot 后
  攻击结算，伤害 = 被复活卡攻击力；复活落空不攻击）；RIFT_ACOLYTE / SACRIFICIAL_SPIRIT 清理断言
  （reveal 容器不再含 `Attack` 调用）。
- 回归：`AttackEffectTests` / `AttackAttributeConsistencyTests` / `Step5BatchBEngineTests` 全绿
  （:434 "grave 无生物不攻"用例不经过攻击路径，不受影响）。

## 修订记录

- 2026-09-14 审核修订：受影响卡清单修正（移除 GRAVE_ROBBER——无 AttackEffect；DOOM_HERALD 实为
  `AttackTimes` 绑定）；新增 RIFT_ACOLYTE / SACRIFICIAL_SPIRIT 死绑定清理；引用修正
  （HPAlterEffect.cs:161、CombatUXManager.cs:4191、AttackAnimationManager.cs:301）；
  删除 RecordDamage(0) 冗余理由（自带早退）；补充 lastCardAttacked 记录、3.0 sweep、法术 0 伤害边界。
- 2026-09-14 关联改动实施：GRAVE_ROBBER 补攻击（纯 prefab；绑定顺序
  `[ReviveStrongestEnemyAndSnapshot, Attack]` 已用 Unity 反射验证）。
