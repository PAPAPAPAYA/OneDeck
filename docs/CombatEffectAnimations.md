# OneDeck 战斗效果动画整理文档

> 基于 `ICombatVisuals`、`CombatUXManager`、`AttackAnimationManager`、`RecorderAnimationPlayer` 及所有 Effect 类的代码分析整理。
> 对应版本：3.0 No Cost
> 整理日期：2026-05-10

---

## 目录

- [一、已有的效果动画](#一已有的效果动画)
- [二、遗漏的效果动画](#二遗漏的效果动画)
  - [高优先级（常用效果无动画）](#高优先级常用效果无动画)
  - [中优先级（费用辅助效果动画缺失）](#中优先级费用辅助效果动画缺失)
  - [低优先级（有基础视觉但可增强）](#低优先级有基础视觉但可增强)
- [三、架构层面的不一致](#三架构层面的不一致)
- [四、快速修复建议](#四快速修复建议)

---

## 一、已有的效果动画

| 效果类型 | 涉及 Effect 类 | 动画表现 | 实现方式 | 备注 |
|----------|---------------|----------|----------|------|
| **伤害 (Attack)** | `HPAlterEffect` (DecreaseTheirHp / DecreaseMyHp 及所有变体) | Scale↑ + 旋转 → 蓄力 → 冲锋 → 命中(屏幕震动) → 过冲 → 返回 | `AnimationRequestType.Attack` → `AttackAnimationManager` | 逻辑先执行，动画后播放；`isStatusEffectDamage=true` 跳过动画 |
| **埋葬 (Bury)** | `BuryEffect` | 弧线飞行至牌堆底部 (0.5s) | `RecorderAnimationPlayer` (`MoveToBottomBatch`) | 批量埋葬时并行播放 |
| **埋葬消耗 (Bury Cost)** | `BuryCostEffect` | 弧线飞行至牌堆底部 (0.5s) | **直接调用** `visuals.MoveCardToBottom` | 未走 Recorder，事件绑定在 `onComplete` |
| **置顶 (Stage)** | `StageEffect` | 弧线飞行至牌堆顶部 (0.5s) | `RecorderAnimationPlayer` (`MoveToTopBatch`) | 批量置顶时并行播放 |
| **位置调整 (Delay)** | `CardManipulationEffect` | 直线移动到指定索引位 (0.3s, 无弧线) | **直接调用** `visuals.MoveCardToIndex` | 未走 Recorder |
| **放逐/销毁 (Exile/Destroy)** | `ExileEffect`, `MinionCostEffect`, `CardManipulationEffect` | 移至墓地位置 + 缩小至消失 (0.3s) | `visuals.DestroyCardWithAnimation` | 无专门的 Exile VFX，复用销毁动画 |
| **状态效果投射物** | `StatusEffectGiverEffect`, `CurseEffect` | 抛物线飞行投射物 (0.4s)，到达后触发效果 | `PlayMultiStatusEffectProjectile` | 多目标时有 0.05s 错开延迟 |
| **状态效果粒子** | 所有调用 `ApplyStatusEffectCore` 的效果 | 目标卡牌位置生成粒子特效 | `PlayStatusEffectParticle` | 依赖 prefab 配置 |
| **状态效果染色** | 所有调用 `ApplyStatusEffectCore` 的效果 | 卡牌变色 (Infected=绿色, Power=红色) | `ApplyStatusTint` | 仅 Infected 和 Power 支持 |
| **添加卡牌** | `AddTempCard`, `CurseEffect`(创建诅咒卡) | 新卡从生成点飞入牌堆并缩放 | `CardFactory` → `AddCardToDeckVisual` | 自动处理 |
| **洗牌** | Start Card 触发 | 全体卡牌弧线飞到新位置 (随机延迟) | `PlayShuffleAnimation` | 通过 `showPos` 中点 |

---

## 二、遗漏的效果动画

### 高优先级（常用效果无动画）

| 遗漏动画 | 涉及 Effect 类 / 方法 | 影响卡片 | 现状 | 建议 |
|----------|----------------------|----------|------|------|
| **消耗状态效果** | `ConsumeStatusEffect` (ConsumeOwnStatusEffect / ConsumeRandomEnemyCardsStatusEffect) | 能量溢出召唤师、曼哈顿博士、力量转移、高等传送门、全能人、史莱姆 | 直接从 `List.RemoveAt` 移除，**无任何视觉反馈** | 状态图标缩小消失 + 消散粒子 |
| **诅咒力量消耗** | `CurseEffect.ConsumeHostileCursePower` | 拔苗助长、咒食的召唤师 | 直接从诅咒卡移除 Power，**无视觉反馈** | Power 图标从诅咒卡飞回施法者/消散 |
| **状态效果转移** | `TransferStatusEffectEffect` (TransferAllStatusEffectToHostileCurse / TransferOneStatusEffectToSelf) | 乌合之众、力量虹吸人 | 源卡移除 + 目标卡添加（有粒子和染色），但**无源→目标的飞行动画** | 添加类似 `PlayMultiStatusEffectProjectile` 的反向飞行动画 |
| **复制卡牌** | `AddTempCard.CopyEnemyCurseCardToThem` | 增殖的厄运 | 新卡从默认位置飞入，**没有"复制"的视觉暗示** | 从源卡分裂/闪烁后生成新卡 |

### 中优先级（费用/辅助效果动画缺失）

| 遗漏动画 | 涉及 Effect 类 | 影响卡片 | 现状 |
|----------|---------------|----------|------|
| **Delay Cost** | `DelayCostEffect.ExecuteDelayCost` | 3.0 no cost 文档中**无使用此费用的卡** | 仅 `SyncPhysicalCardsWithCombinedDeck` + `UpdateAllPhysicalCardTargets`（瞬间移动） |
| **Expose Cost** | `ExposeCostEffect.ExecuteExposeCost` | 3.0 no cost 文档中**无使用此费用的卡** | 同上，仅瞬间移动 |
| **治疗 (Heal)** | `HPAlterEffect` (IncreaseMyHp / IncreaseTheirHp) | 当前版本**无治疗卡**，但代码存在 | 纯数值变化 + 日志，无任何视觉反馈 |

### 低优先级（有基础视觉但可增强）

| 遗漏动画 | 涉及 Effect 类 | 影响卡片 | 现状 |
|----------|---------------|----------|------|
| **力量反应** | `PowerReactionEffect.GivePowerToCardThatGotPower` | 武器精灵 | 调用 `ApplyStatusEffectCore`（有粒子和染色），但**无投射物飞行动画** |
| **力量放大** | `StatusEffectAmplifierEffect.AmplifyStatusEffectGain` | 力量渴求者 | 调用 `ApplyStatusEffectCore`（有粒子和染色），但**无"放大"特殊视觉**（如闪光、脉冲） |
| **护盾格挡** | `HPAlterEffect.ProcessShieldNHp` | 所有带 Shield 的战斗 | shield 抵消伤害时**纯数值计算**，无盾牌破碎/格挡视觉 |
| **Counter 计数** | Counter 状态机制 | 带 Counter 的卡 | Counter 触发时**无专门视觉反馈** |

---

## 三、架构层面的不一致

| 问题 | 详情 | 影响 |
|------|------|------|
| **部分动画未走 `RecorderAnimationPlayer`** | `BuryCostEffect`、`CardManipulationEffect.Delay` 直接调用 `visuals.MoveCardToXxx`，没有添加到 `EffectRecorder.animationRequests` | 这些动画与效果链的批量动画**不同步**，可能在错误的时间点播放 |
| **ExposeCostEffect / DelayCostEffect 无任何动画调用** | 仅执行逻辑后 `Sync` + `UpdateAllPhysicalCardTargets` | 卡牌会**瞬间跳跃**到新位置，玩家无法感知发生了什么 |
| **ConsumeStatusEffect 完全无视 `visuals` 层** | 直接操作 `myStatusEffects.RemoveAt` | 力量层数、Counter 层数的变化是**静默的** |
| **CurseEffect.ConsumeHostileCursePower 无视 `visuals` 层** | 直接操作 `myStatusEffects.RemoveAt` + 手动 `TriggerTintForStatusEffect` | 诅咒力量被吸走时无飞行动画 |

---

## 四、快速修复建议

1. **给 ConsumeStatusEffect 添加粒子消散** — 影响面最大（几乎所有"消耗力量"的卡）。
2. **统一 BuryCostEffect / CardManipulationEffect 走 Recorder** — 解决动画时序问题。
3. **给 DelayCostEffect / ExposeCostEffect 添加 `MoveCardToIndex` 动画** — 费用效果也需要视觉反馈。
4. **给 TransferStatusEffectEffect 复用 `PlayMultiStatusEffectProjectile`** — 乌合之众和力量虹吸人的体验会大幅提升。

---

> 如需进一步定位具体代码位置或给出某类遗漏动画的详细实现方案，可在此基础上继续迭代。
