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
| 1 | `ConsumeStatusEffect.cs` | `ConsumeOwnStatusEffect(int amount)` | **3.0 活跃**：OVERCHARGED_SUMMONER（Power×1）、DR_MANHATTAN（Power×4）、ADVANCE_PORTAL（Counter×2）、ALMIGHTY（Counter×2）、SLIME（Counter×2）<br>旧版/测试：Fireball 系列（Mana）、Undead Shiv（Revive）、Prince of the Flies（Counter）、GOLEM（Counter）、DOWNED_FIGHTER（None） | `PopUp` → `StatusEffectProjectile`（自身→`statusEffectConsumePos`）→ `StatusEffectChange` → `SlotIn` | - | - | P2 | ✅ Done | 2026-06-13 | 目标就是发动卡牌本身，projectile 飞向 `statusEffectConsumePos` 作为自环/吸收表现；`StatusEffectChange` 在 projectile 落地后提交 |
| 2 | `ConsumeStatusEffect.cs` | `ConsumeRandomEnemyCardsStatusEffect(int amount)` | **POWER_TRANSFER**（Power×2） | 改为 batch：`StatusEffectChange` → `PopUpBatch` → `StatusEffectProjectile`（所有目标→source，并行，吸收）→ `SlotInBatch` | - | - | P1 | ✅ Done | 2026-06-13 | 复用 `EffectScript.CaptureBatchStatusEffectConsumeAnimation(sourceCard, targets, effect, 1)`；`reverseProjectile=true`，终点为 source 卡位置；`StatusEffectChange` 先加入队列并被 `RecorderAnimationPlayer` 标记为 defer，projectile 落地后统一 commit |
| 3 | `CurseEffect.cs` | `ConsumeHostileCursePower(int amount)` | **CURSE_SUMMONER**、**PREMATURE** | batch：`StatusEffectChange` → `PopUpBatch` → `StatusEffectProjectile`（所有目标→`statusEffectConsumePos`，并行，吸收）→ `SlotInBatch` | - | - | P0 | ✅ Done | 2026-06-13 | 调用 `CaptureBatchStatusEffectConsumeAnimation(myCard, affectedTargets, Power, removedAmounts, consumePos)`；`projectileCountsPerTarget=removedAmounts` 保证每层 Power 对应一个 projectile；目标卡 display 在 projectile 落地后 commit |
| 4 | `TransferStatusEffectEffect.cs` | `TransferOneStatusEffectToSelf(bool fromFriendly)` | **POWER_SIPHONER**（Power） | batch：`PopUpBatch(sources) → PopUp(self) → StatusEffectProjectile(sources→self, 并行) → SlotInBatch(sources) → SlotIn(self) → StatusEffectChange(sources) → StatusEffectChange(self)` | - | - | P1 | ✅ Done | 2026-06-13 | 调用 `CaptureBatchStatusEffectTransferAnimation(sources, myCardScript, effect, amounts)`；`RecorderAnimationPlayer` 用 `attackerCards` + `targetCard` 路径播放多 source → 单 target projectile；target（self）display 在 projectile 落地后 commit，source display 在各自的 `StatusEffectChange` 中 commit（位于 SlotIn 之后） |
| 5 | `TransferStatusEffectEffect.cs` | `TransferAllStatusEffectToHostileCurse()` | **CROW_CROWD**（Power） | batch：`PopUpBatch(sources) → PopUp(targetCurse) → StatusEffectProjectile(sources→targetCurse, 并行) → SlotInBatch(sources) → SlotIn(targetCurse) → StatusEffectChange(sources) → StatusEffectChange(targetCurse)` | - | - | P0 | ✅ Done | 2026-06-13 | `TransferStatusEffects()` 先调用 `CaptureBatchStatusEffectTransferAnimation`，再移除 source 状态并调用 `ApplyStatusEffectCore(targetCurse)`；targetCurse 的 `StatusEffectChange` 由 `ApplyStatusEffectCore` 追加并在 projectile 落地后 commit |
| 6 | `ManaAlterEffect.cs` | `ConsumeMana(int amount)` | 当前没有 prefab 调用 | 仅 `StatusEffectChange` | 缺少 PopUp、SlotIn、Projectile | 如果启用，参考 `ConsumeOwnStatusEffect` 做 `PopUp → StatusEffectChange → SlotIn`，并考虑自环粒子 | P2 | ⏸️ On Hold | - | 代码存在但无 prefab 使用 |
| 7 | `CostNEffectContainer.cs` | `CheckCost_Rested()` | Mandela Effect（1.0） | 无动画 | 无任何视觉反馈 | 给一个轻微的 Rest 消散粒子或卡牌抖动/变淡 | P2 | ⬜ Pending | - | 这是 cost 检查，不是 effect 事件，动画应极轻 |

---

## 按优先级分组检查清单

### P0（必须优先补）

- [x] `CurseEffect.ConsumeHostileCursePower` 补全 PopUp / Projectile / SlotIn / StatusEffectChange
- [x] `TransferStatusEffectEffect.TransferAllStatusEffectToHostileCurse` 补全 source 与 target 的完整动画

### P1（有基础但缺 projectile / batch）

- [x] `ConsumeRandomEnemyCardsStatusEffect` 改为 batch + projectile
- [x] `TransferStatusEffectEffect.TransferOneStatusEffectToSelf` source 卡片也弹出/缩回

### P2（自己消耗自己或已废弃）

- [x] `ConsumeOwnStatusEffect` 已补充 projectile 自环/吸收动画
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
| 2026-06-13 | Kimi | 完成 `ConsumeRandomEnemyCardsStatusEffect` batch + projectile 动画；新增 `EffectScript.CaptureBatchStatusEffectConsumeAnimation` 公共 Helper |
| 2026-06-13 | Kimi | 完成 `CurseEffect.ConsumeHostileCursePower` batch + 自定义终点 projectile 动画；扩展 `CaptureBatchStatusEffectConsumeAnimation` 支持每目标不同层数与 `statusEffectConsumePos` |
| 2026-06-13 | Kimi | 完成 `TransferStatusEffectEffect.TransferAllStatusEffectToHostileCurse` 与 `TransferOneStatusEffectToSelf` 的完整 batch 动画；新增 `EffectScript.CaptureBatchStatusEffectTransferAnimation` 公共 Helper |
| 2026-06-13 | Kimi | 复核并更新 `ConsumeOwnStatusEffect`：实际已实现 `PopUp → StatusEffectProjectile → StatusEffectChange → SlotIn`，补充审计文档 |
| 2026-06-13 | Kimi | 复核 P0/P1 项：`ConsumeHostileCursePower`、`ConsumeRandomEnemyCardsStatusEffect`、`TransferOneStatusEffectToSelf`、`TransferAllStatusEffectToHostileCurse` 实现与文档一致；修正 Transfer 项的动画请求顺序与 commit 时机描述 |
