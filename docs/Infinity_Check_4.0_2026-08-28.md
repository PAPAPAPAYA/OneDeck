# 4.0 池无限循环检查报告（Roadmap 第 1 步）

日期：2026-08-28
检查范围：`Assets/Prefabs/Cards/4.0` 全部 23 张 prefab（0_Common 8 / 1_Uncommon 10 / 2_Rare 5）+ 运行时 token RIFT（`3.0 no cost (current)/_DONT INCLUDE/Token/RIFT.prefab`）
数据来源：`docs/CardDesc_Response_Check_4.0.txt`（Unity 提取的 desc + 事件绑定）
规则依据：`.agents/skills/unity-card-infinity-check`（Type A/B/C 分类）

## 摘要

| 类型 | 结论 |
|------|------|
| Type A 同回合无限（Critical） | **未发现** |
| Type B 资源无限（High） | **未发现无限**；2 个线性增殖 watch 项（SLIME / RIFT_HATCHERY） |
| Type C 状态爆炸（Medium） | 1 项（UNFINISHED_ROBOT），当前池无加速器；复活引擎落地后须复检 |

## Type A: 同回合无限（Critical）

| Combo | Cards Needed | Loop Mechanism |
|-------|-------------|----------------|

无。池内唯一置顶卡是 SOLDIER_SKELETON（遗言:置顶自身，需外部埋葬触发，非揭晓无条件触发），不构成双无条件置顶对。

RIFT token 置顶 1 友方后**放逐自身**（ExileSelf），自消耗：N 个 token 连锁最多产生 N 次揭晓后终止，线性有界。

## Type B: 资源无限（High）

| Card/Combo | Mechanism | Fix Needed |
|------------|-----------|------------|

无无限循环。两个线性增殖 watch 项（按 4.0「线性增殖」设计哲学属预期，但需关注长局牌堆膨胀）：

1. **SLIME**（2_Rare）：遗言:复制自身，绑定 `OnMeBuried → AddTempCard.AddSelfToMe`，**无成本门槛**（3.0 版有 `CheckCost_Counter(2)` 门）。每次友方埋葬触发净 +1 SLIME。池内友方埋葬源 6 张（EULOGIST / GRAVE_PUNCH / GRAVE_TOGETHER / GRAVE_MILLER / GRAVE_FIST / SACRIFICIAL_SPIRIT），长局牌堆线性膨胀。与 GRAVE_MILLER（埋 2 友方 + 埋顶 5 卡）同 deck 时增速最高。**结论**：有界线性，符合规格文案，不动；若实测膨胀过快，优先恢复 Counter 门槛而非削埋葬源。
2. **RIFT_HATCHERY**（1_Uncommon）：~~回合边界埋葬自身 + 生成 3 [次元裂缝]，每轮循环 +3 张 token~~ **（2026-08-29 已按用户拍板重构：揭晓时：生成3[次元裂缝]；回合开始：埋葬自身 —— 自然循环下每轮 0 张 token，仅被复活/置顶拉出时爆发 3 张，见 Design Concerns #1 的裁决记录）**。线性；token 本体自消耗（见 Type A），无指数风险。**结论**：有界。

## Type C: 状态爆炸（Medium）

| Card/Combo | Mechanism | Risk |
|------------|-----------|------|

- **UNFINISHED_ROBOT**（2_Rare）：`OnMeRevealed → DoubleOwnAttack`，每次揭晓攻击力翻倍（1→2→4→…，第 N 次揭晓 ATK=2^(N-1)）。**当前池内无外部置顶/复活加速器**（SOLDIER_SKELETON 只置顶自身），增速=自然轮换，风险可控。**复活/苏醒引擎（第 2 步）落地后该卡可被复活源反复揭晓 → 指数放大，属第 2 步引擎落地后必复检项**（Roadmap §3 已排）。

SNOWBALL × 强化源（BLACKSMITH / WAR_TRAINER / RIFT_PRIEST）：`OnMeGainedAttack → GiveSelfAttack` 单次反弹，EffectChainManager 同卡同组件单链守卫阻断二次触发，外部 +N 实际 +N+1，有界。无风险。

## Design Concerns（非无限，但需拍板）

1. ~~**RIFT_HATCHERY 触发事件与文案不一致（P2）**~~ **已裁决并落地（2026-08-29）**。完整结论（含对本报告初版的修正）：
   - **修正**：初版判定「`BeforeRoundFinished` 全代码库零 Raise、卡是死卡」**错误**。实际是命名倒置——场景 `GameScene.unity` 的 `GameEventStorage.beforeRoundStart` 字段接线的资产就是 `GAME FLOW/BeforeRoundFinished.asset`（`CombatManager.cs:317` 战斗开始 + `:1050` 每轮洗牌后 `beforeRoundStart.Raise()` 实际 Raise 的是该资产）。**该资产语义 = 回合开始**，HATCHERY 原配置（回合开始：自埋 + 直接生成3）一直有效。已把资产名注记补进 `docs/4.0_CardDesc_Spec.md` 触发表。
   - **用户拍板的设计**：直接每轮 3 信徒过强 → 重构为复活门控休眠引擎：`揭晓时：生成3[次元裂缝]` + `回合开始：埋葬自身`。每轮洗牌把它捞回活区后立即自埋（永不自然揭晓）；仅被复活源（第 2 步）或 RIFT token 随机置顶（`StageEffect.StageMyCards` 无墓地边界过滤，过渡期通路）拉出时揭晓爆发一次 3 张，随后自埋循环自动重建。每次复活 = 恰好 3 张。
   - **落地改动**：prefab 监听拆分——原单监听（BeforeRoundFinished → 自埋 + 生成3）拆为：埋葬自身留在 BeforeRoundFinished（即回合开始事件，无需改绑），生成3 新增监听改绑 `OnMeRevealed`；cardDesc 更新为 `揭晓时:生成 <b>3</b> [次元裂缝];回合开始:埋葬自身`。Notion DB ID29 行同步（desc + Unity 配置状态=已配置）。
   - **已接受的副作用**：每轮回合开始 BurySelf 触发埋葬事件族（onMeBuried / onFriendlyCardBuried / onAnyCardBuried）一次——埋葬轴的稳定每轮引擎，87 卡全池中「友方被埋葬时」反应卡会吃到这个触发，平衡时计入。
   - **遗留债务**：资产名 `BeforeRoundFinished.asset` 与语义（回合开始）相反，仅 HATCHERY 与两张 _DEPRECATED 1.0 prefab 引用；第 4 步做「回合结束事件」时建议一并正名（新建 RoundStart 资产 + 场景重新接线 + 迁移 listener），本报告不阻塞。
2. DETERIORATION 条件段（敌方[诅咒]每有 3 攻击力:强化 1）已按 spec §143 确认为**揭晓时一次性求值**，非持久光环，绑定 `OnMeRevealed` 正确。无问题。
3. Notion 4.0 DB 缺 **SOLDIER_SKELETON / AVENGER** 两行（第 0 步已发现，见交付汇报）。

## Recommendations

- ~~RIFT_HATCHERY 事件绑定/文案二选一修正~~ 已落地（2026-08-29，见 Design Concerns #1）。
- 第 2 步复活/苏醒引擎落地后：复跑本检查（重点 UNFINISHED_ROBOT × 复活源、SLIME × 复活源复活链、**HATCHERY × 复活源爆发节奏**）。

## 引擎落地增量（2026-08-29，第 2 步实施后）

复活/苏醒引擎（`Effects/ReviveEffect`）已实施（详见 `plans/plan-4.0-revive-awaken-2026-08-29.md`）。无限循环视角的增量结论：

- **当前 23 张配置池内无新增无限风险**：复活源中目前只有 RIFT token（`ReviveMyCards(1)`，随机、每轮每 token 至多一次且自放逐）。复活是"grave → 顶"的单向移动，不产生同回合自持循环；苏醒事件链受 EffectChainManager 同卡同组件守卫 + 深度上限 99 约束。
- **RIFT token 收窄净降风险**：原「置顶1友方」可加速任意活卡（含 UNFINISHED_ROBOT 类状态爆炸卡）；改为纯复活后 token 失去加速活卡的能力，Type C 风险面缩小。
- **UNFINISHED_ROBOT**：当前无复活源能定向拉它（token 复活是随机友方选取，墓地中的 ROBOT 有低概率被拉出 → 每次拉出 = 一次翻倍）。仍是第 5 步配置 18 复活源时的**重点复检对象**（定向复活/复活自身类卡 × ROBOT 组合）。
- **18 复活源批量配置（第 5 步）时必复跑本检查**，重点：复活自身（53/101）× 苏醒反应链、多投复活（35/86/99）× 苏醒生成链、复辟族（54/65/70/80/85）× 敌方诅咒增长。
- 第 4 步「回合结束事件」实施时，一并给 `BeforeRoundFinished.asset` 正名（见 Design Concerns #1 遗留债务）。
