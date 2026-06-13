# OneDeck 战斗效果动画整理文档

> 基于 `ICombatVisuals`、`CombatUXManager`、`AttackAnimationManager`、`RecorderAnimationPlayer` 及所有 Effect 类的代码分析整理。
> 对应版本：3.0 No Cost
> 整理日期：2026-06-14（已同步 2026-06 动画补全工作）

---

## 目录

- [一、已有的效果动画](#一已有的效果动画)
- [二、仍可补充的效果动画](#二仍可补充的效果动画)
  - [中优先级](#中优先级)
  - [低优先级](#低优先级)
- [三、架构层面的状态](#三架构层面的状态)
- [四、快速修复建议](#四快速修复建议)

---

## 一、已有的效果动画

| 效果类型 | 涉及 Effect 类 | 动画表现 | 实现方式 | 备注 |
|----------|---------------|----------|----------|------|
| **伤害 (Attack)** | `HPAlterEffect` (DecreaseTheirHp / DecreaseMyHp 及所有变体) | Scale↑ + 旋转 → 蓄力 → 冲锋 → 命中(屏幕震动) → 过冲 → 返回 | `AnimationRequestType.Attack` → `AttackAnimationManager` | 逻辑先执行，动画后播放；`isStatusEffectDamage=true` 跳过动画 |
| **治疗 (Heal)** | `HPAlterEffect` (IncreaseMyHp / IncreaseTheirHp) | 当前版本**无治疗卡**，代码存在 | 纯数值变化 + 日志 | 无专门动画，但也没有卡牌调用 |
| **埋葬 (Bury)** | `BuryEffect` | PopUpBatch → 弧线飞行至牌堆底部 (0.5s) | `RecorderAnimationPlayer` (`PopUpBatch` + `MoveToBottomBatch`) | 批量埋葬时并行播放；**2026-06-13 起逻辑阶段不再 `SyncPhysicalCardsWithCombinedDeck`** |
| **置顶 (Stage)** | `StageEffect` | 弧线经 `showPos` 飞至 pop-up peak，再 slot in 到牌堆顶部 | `RecorderAnimationPlayer` (`MoveToTopPopUpBatch`) | 批量置顶时并行播放；**2026-06-13 起逻辑阶段不再 `SyncPhysicalCardsWithCombinedDeck`** |
| **位置调整 (Delay)** | `CardManipulationEffect` | 直线移动到指定索引位 (0.3s, 无弧线) | 直接调用 `visuals.MoveCardToIndex` | 未走 Recorder |
| **放逐/销毁 (Exile/Destroy)** | `ExileEffect`, `MinionCostEffect`, `CardManipulationEffect` | PopUp → 移至墓地位置 + 缩小至消失 (0.3s) | `visuals.DestroyCardWithAnimation` | `ExileEffect` 会先 PopUp 让玩家看清被放逐的卡 |
| **添加卡牌** | `AddTempCard`, `CurseEffect`(创建诅咒卡) | 新卡从生成点飞入牌堆并缩放 | `MoveToPopUpPosition` + `SlotIn` | 每个新卡都有独立的 PopUp/SlotIn |
| **洗牌** | Start Card 触发 | 全体卡牌弧线飞到新位置 (随机延迟) | `PlayShuffleAnimation` | 通过 `showPos` 中点 |
| **费用检查失败反馈** | `CostNEffectContainer` + `CostResultPresenter` | 卡牌左右抖动 | `AnimationRequestType.Shake` | 2026-06-07 加入 |
| **给予状态效果** | `StatusEffectGiverEffect` 及其子类 (`PowerReactionEffect`, `StatusEffectAmplifierEffect`, `ManaAlterEffect`) | PopUpBatch → 抛物线飞行投射物 (0.4s，多层按层数生成，可错开发射) → SlotInBatch | `AnimationRequestType.StatusEffectProjectile` | 多目标并行飞行；逻辑阶段已同步 `ApplyStatusEffectCore`，projectile 落地后提交 `deferDisplayCommit` |
| **诅咒增强** | `CurseEffect.ApplyPowerToCardWithProjectile` | PopUp → Projectile (单目标) → SlotIn | `AnimationRequestType.StatusEffectProjectile` | 目标诅咒卡先弹起，再被投射物命中 |
| **消耗自身状态效果** | `ConsumeStatusEffect.ConsumeOwnStatusEffect` | PopUp → Projectile 飞向 `statusEffectConsumePos` (按层数生成) → StatusEffectChange → SlotIn | `AnimationRequestType.StatusEffectProjectile` + `customProjectileEndPosition` | 自环/吸收表现 |
| **消耗敌方卡牌状态效果** | `ConsumeStatusEffect.ConsumeRandomEnemyCardsStatusEffect` | StatusEffectChange(defer) → PopUpBatch → Projectile 从目标飞回 source (吸收) → SlotInBatch | `CaptureBatchStatusEffectConsumeAnimation` | 多目标并行 |
| **消耗敌方诅咒卡力量** | `CurseEffect.ConsumeHostileCursePower` | StatusEffectChange(defer) → PopUpBatch → Projectile 从目标飞向 `statusEffectConsumePos` (每目标按实际消耗层数生成) → SlotInBatch | `CaptureBatchStatusEffectConsumeAnimation` | 支持非均匀层数 (`projectileCountsPerTarget`) |
| **转移状态效果** | `TransferStatusEffectEffect` | PopUpBatch(sources) → PopUp(target) → Projectile sources→target (并行) → SlotInBatch(sources) → SlotIn(target) → StatusEffectChange | `CaptureBatchStatusEffectTransferAnimation` | `CROW_CROWD`、`POWER_SIPHONER` 等 |
| **状态效果染色/粒子** | 所有调用 `ApplyStatusEffectCore` 的效果 | 目标卡牌位置生成粒子特效 + 变色 (Infected=绿色, Power=红色) | `StatusEffectChange` 请求 + `ApplyStatusTint` | 通过 `deferDisplayCommit` 延迟到 projectile 落地后提交 |

---

## 二、仍可补充的效果动画

### 中优先级

| 遗漏动画 | 涉及 Effect 类 / 方法 | 影响卡片 | 现状 | 建议 |
|----------|----------------------|----------|------|------|
| **Rest 费用消耗提示** | `CostNEffectContainer.CheckCost_Rested()` | Mandela Effect（1.0）| 无任何视觉反馈 | 极轻微的 Rest 消散粒子或卡牌抖动/变淡 |
| **Delay Cost / Expose Cost / Bury Cost** | 当前 3.0 no cost 文档中**无使用这些费用的卡**；对应 Effect 类在代码库中不存在或未激活 | 无 | 仅瞬间移动或不存在 | 如有新卡启用，参考 Stage/Bury 走 Recorder 并添加 PopUp/Projectile |
| **Mana 消耗** | `ManaAlterEffect.ConsumeMana`（如存在调用） | 当前没有 prefab 调用 | 仅 `StatusEffectChange` | 启用后参考 `ConsumeOwnStatusEffect` 做 PopUp + Projectile + SlotIn |

### 低优先级（有基础视觉但可增强）

| 遗漏动画 | 涉及 Effect 类 | 影响卡片 | 现状 |
|----------|---------------|----------|------|
| **护盾格挡** | `HPAlterEffect.ProcessShieldNHp` | 所有带 Shield 的战斗 | shield 抵消伤害时纯数值计算，无盾牌破碎/格挡视觉 |
| **Counter 计数** | Counter 状态机制 | 带 Counter 的卡 | Counter 触发时无专门视觉反馈 |
| **复制卡牌视觉暗示** | `AddTempCard.CopyEnemyCurseCardToThem` | 增殖的厄运 | 新卡从默认位置飞入，没有“复制”的视觉暗示（从源卡分裂/闪烁） |

> **注**：`PowerReactionEffect` 和 `StatusEffectAmplifierEffect` 继承自 `StatusEffectGiverEffect`，已自动获得 batch projectile 动画，不再属于缺失项。

---

## 三、架构层面的状态

| 问题 | 当前状态 | 说明 |
|------|----------|------|
| **部分动画未走 `RecorderAnimationPlayer`** | 已部分解决 | `BuryEffect` / `StageEffect` 已改为仅捕获 `AnimationRequest`，由 `RecorderAnimationPlayer` 统一播放；`CardManipulationEffect.Delay` 仍直接调用 `visuals.MoveCardToIndex` |
| **逻辑阶段提前 `SyncPhysicalCardsWithCombinedDeck`** | 已解决 | `BuryEffect` / `StageEffect` 在 2026-06-13 移除逻辑阶段同步；deck reordering 由 `RecorderAnimationPlayer` 通过 `ApplyAnimationResult` 在动画阶段推进 |
| **`ConsumeStatusEffect` 无视 visuals 层** | 已解决 | 已补充 PopUp/SlotIn/Projectile 完整流程 |
| **`CurseEffect.ConsumeHostileCursePower` 无视觉反馈** | 已解决 | 已补充 batch PopUp/Projectile/SlotIn |
| **`TransferStatusEffectEffect` 无源→目标飞行动画** | 已解决 | 已补充 `CaptureBatchStatusEffectTransferAnimation` |
| **`StatusEffectProjectile` 不反映层数** | 已解决 | `AnimationRequest.projectileCount` / `projectileCountsPerTarget` 已支持按层数生成 projectile |

---

## 四、快速修复建议

1. **Rest 费用检查增加极轻微反馈** — 唯一剩下的中优先级缺失动画。
2. **统一 `CardManipulationEffect.Delay` 走 Recorder** — 保持与 Bury/Stage 行为一致。
3. **护盾/Counter 视觉增强** — 如需提升战斗可读性，可后续补充。

---

> 如需进一步定位具体代码位置或给出某类遗漏动画的详细实现方案，可在此基础上继续迭代。
