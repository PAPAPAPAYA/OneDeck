# Consume Status Effect 动画补充追踪文档

> 本表用于追踪所有"消耗/移除状态层数"效果的动画完整性。  
> 来源：`ConsumeStatusEffect`、`CurseEffect`、`TransferStatusEffectEffect`、`ManaAlterEffect`、`CostNEffectContainer.CheckCost_Rested`。  
> 参考标杆：`StatusEffectGiverEffect` 的 batch 流程：`PopUpBatch → StatusEffectProjectile → SlotInBatch → StatusEffectChange`。

## 状态图例

| 状态 | 含义 |
|------|------|
| ⬜ Pending | 尚未补充动画 |
| 🔄 In Progress | 正在修改/测试中 |
| ✅ Done | 已完成并验证 |
| ❌ Won't Fix | 确认不需要补充 |
| ⏸️ On Hold | 当前未使用，待启用后再补 |

---

## 总体优先级

1. **P0**：完全没有弹出/缩回/投射物，玩家几乎看不到状态被消耗。优先补 `CurseEffect.ConsumeHostileCursePower`、`TransferStatusEffectEffect.TransferAllStatusEffectToHostileCurse`。
2. **P1**：有基础动画但缺少 projectile 或 batch。补 `ConsumeRandomEnemyCardsStatusEffect`、`TransferStatusEffectEffect.TransferOneStatusEffectToSelf`。
3. **P2**：自己消耗自己的效果，视觉反馈偏弱。可补一个 burst/自环粒子。`ConsumeOwnStatusEffect`、`CheckCost_Rested`、`ConsumeMana`。

---

## 详细追踪表

| # | 效果脚本 | 方法 | 使用的卡牌 | 当前动画请求 | 缺失内容 | 建议补充方案 | 优先级 | 状态 | 完成日期 | 备注 |
|---|----------|------|------------|--------------|----------|--------------|--------|------|----------|------|
| 1 | `ConsumeStatusEffect.cs` | `ConsumeOwnStatusEffect(int amount)` | **3.0 活跃**：OVERCHARGED_SUMMONER（Power×1）、DR_MANHATTAN（Power×4）、ADVANCE_PORTAL（Counter×2）、ALMIGHTY（Counter×2）、SLIME（Counter×2）<br>旧版/测试：Fireball 系列（Mana）、Undead Shiv（Revive）、Prince of the Flies（Counter）、GOLEM（Counter）、DOWNED_FIGHTER（None） | `PopUp` → `StatusEffectChange` → `SlotIn` | 缺少 projectile / 自爆粒子 | 在自身位置播放一个状态消散粒子或一个从卡牌飞向自己的微型 projectile，明确表达"状态被吃掉" | P2 | ✅ | - | 目标就是发动卡牌本身，所以 projectile 可以是自环 |
| 2 | `ConsumeStatusEffect.cs` | `ConsumeRandomEnemyCardsStatusEffect(int amount)` | **POWER_TRANSFER**（Power×2） | 对每个目标顺序播放 `PopUp → StatusEffectChange → SlotIn` | 缺少 StatusEffectProjectile；多目标未 batch | 改为 batch 流程：`PopUpBatch` + `StatusEffectProjectile`（source→所有目标，并行）+ `SlotInBatch` + `StatusEffectChange` | P1 | ⬜ Pending | - | 多个目标会排队，效率低 |
| 3 | `CurseEffect.cs` | `ConsumeHostileCursePower(int amount)` | **CURSE_SUMMONER**、**PREMATURE** | 仅 `StatusEffectChange`（tint + particles） | 缺少 PopUp、SlotIn、Projectile | source 卡牌弹出 → projectile 飞向目标诅咒卡 → 目标 PopUp/SlotIn → `StatusEffectChange` | P0 | ⬜ Pending | - | 玩家几乎看不到 Power 被吸走 |
| 4 | `TransferStatusEffectEffect.cs` | `TransferOneStatusEffectToSelf(bool fromFriendly)` | **POWER_SIPHONER**（Power） | self：`PopUp → StatusEffectProjectile(source→self) → SlotIn`；source 卡片：仅 `StatusEffectChange` | source 卡片没有 PopUp/SlotIn | 给每个 source 卡片也加上 `PopUp` 和 `SlotIn`，让来源卡也 visibly 弹出 | P1 | ⬜ Pending | - | 当前只有 self 在动，source 只是变色 |
| 5 | `TransferStatusEffectEffect.cs` | `TransferAllStatusEffectToHostileCurse()` | **CROW_CROWD**（Power） | source 卡片：仅 `StatusEffectChange`；target curse：由 `ApplyStatusEffectCore` 生成 `StatusEffectChange` | 缺少 PopUp、SlotIn、Projectile | source 卡牌 batch 弹出 → projectile 飞向目标 curse → target PopUp/SlotIn；双方 `StatusEffectChange` | P0 | ⬜ Pending | - | 完全静态，最需要补充 |
| 6 | `ManaAlterEffect.cs` | `ConsumeMana(int amount)` | 当前没有 prefab 调用 | 仅 `StatusEffectChange` | 缺少 PopUp、SlotIn、Projectile | 如果启用，参考 `ConsumeOwnStatusEffect` 做 `PopUp → StatusEffectChange → SlotIn`，并考虑自环粒子 | P2 | ⏸️ On Hold | - | 代码存在但无 prefab 使用 |
| 7 | `CostNEffectContainer.cs` | `CheckCost_Rested()` | Mandela Effect（1.0） | 无动画 | 无任何视觉反馈 | 给一个轻微的 Rest 消散粒子或卡牌抖动/变淡 | P2 | ⬜ Pending | - | 这是 cost 检查，不是 effect 事件，动画应极轻 |

---

## 按优先级分组检查清单

### P0（必须优先补）

- [ ] `CurseEffect.ConsumeHostileCursePower` 补全 PopUp / Projectile / SlotIn / StatusEffectChange
- [ ] `TransferStatusEffectEffect.TransferAllStatusEffectToHostileCurse` 补全 source 与 target 的完整动画

### P1（有基础但缺 projectile / batch）

- [ ] `ConsumeRandomEnemyCardsStatusEffect` 改为 batch + projectile
- [ ] `TransferStatusEffectEffect.TransferOneStatusEffectToSelf` source 卡片也弹出/缩回

### P2（自己消耗自己或已废弃）

- [ ] `ConsumeOwnStatusEffect` 增加自环/消散粒子
- [ ] `CheckCost_Rested` 增加极轻微提示动画（如抖动/变淡）
- [ ] `ConsumeMana` 待启用后参考 P2 方案补充

---

## 代码位置速查

| 代码 | 路径 |
|------|------|
| ConsumeStatusEffect | `Assets/Scripts/Effects/StatusEffect/ConsumeStatusEffect.cs` |
| CurseEffect | `Assets/Scripts/Effects/CurseEffect.cs` |
| TransferStatusEffectEffect | `Assets/Scripts/Effects/TransferStatusEffectEffect.cs` |
| ManaAlterEffect | `Assets/Scripts/Effects/StatusEffect/ManaAlterEffect.cs` |
| CostNEffectContainer | `Assets/Scripts/Card/CostNEffectContainer.cs` |
| 动画请求定义 | `Assets/Scripts/Managers/AnimationRequest.cs` |
| 动画播放器 | `Assets/Scripts/Managers/RecorderAnimationPlayer.cs` |
| 标杆 batch 参考 | `Assets/Scripts/Effects/StatusEffect/StatusEffectGiverEffect.cs` |

---

## 修改记录

| 日期 | 修改人 | 内容摘要 |
|------|--------|----------|
| 2026-06-12 | Kimi | 建立追踪表，标记所有待补充项 |
