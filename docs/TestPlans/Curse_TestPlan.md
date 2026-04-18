# Curse 卡组测试计划

> 路径: `Assets/Prefabs/Cards/3.0 no cost (current)/Curse/`
> 本目录包含 15 张 Curse 派系卡片。

---

## 通用关键脚本

| Script | Path | Responsibility |
|--------|------|----------------|
| `HPAlterEffect` | `Assets/Scripts/Effects/HPAlterEffect.cs` | 伤害 / 治疗计算与交付 |
| `CurseEffect` | `Assets/Scripts/Effects/CurseEffect.cs` | 增强 / 消耗敌方诅咒 |
| `StageEffect` | `Assets/Scripts/Effects/StageEffect.cs` | 置顶卡片 |
| `BuryEffect` | `Assets/Scripts/Effects/BuryEffect.cs` | 埋葬卡片 |
| `AddTempCard` | `Assets/Scripts/Effects/AddTempCard.cs` | 添加 / 复制临时卡片 |
| `TransferStatusEffectEffect` | `Assets/Scripts/Effects/TransferStatusEffectEffect.cs` | 转移状态效果 |
| `StatusEffectGiverEffect` | `Assets/Scripts/Effects/StatusEffect/StatusEffectGiverEffect.cs` | 施加状态效果 |
| `CostNEffectContainer` | `Assets/Scripts/Card/CostNEffectContainer.cs` | 代价检查与效果触发 |
| `CombatManager` | `Assets/Scripts/Managers/CombatManager.cs` | 管理 combinedDeckZone 与 revealZone |
| `GameEventStorage` | `Assets/Scripts/Managers/GameEventStorage.cs` | 集中管理 GameEvent 引用 |

> **注意:** `BaseDmgRef.value = 2`。所有 HPAlterEffect 的 `DecreaseTheirHp` / `DecreaseMyHp` 会自动叠加 `baseDmg.value`。

---

## 1. CROW_CROWD

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/Curse/CROW_CROWD.prefab` |
| **Card Type ID** | `CROW_CROWD` |
| **Description** | 将所有友方的 力量 转移到敌方的 [诅咒] |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onMeRevealed`。
2. `TransferStatusEffectEffect.TransferAllStatusEffectToHostileCurse()`:
   - 查找所有友方带有 Power 的卡片。
   - 查找敌方 curse 卡片（cardTypeID = `CurseCardTypeID`）。
   - 将友方所有 Power 转移到敌方 curse 上。

### Effect Formula

```
源: 所有友方卡片的 Power 层数之和
目标: 敌方 curse 卡片 (JU_ON)
效果: 源卡片移除全部 Power，目标卡片增加同等数量 Power
若目标不存在 -> 效果不执行
```

### Important Implementation Details

- `FindHostileCurseCard` 只搜索 combinedDeckZone，不搜索 revealZone。
- 转移前会计算所有源卡片的 Power 总数，然后一次性施加到目标上。
- 如果找不到敌方 curse 卡片，直接返回（不会创建新卡片）。

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 正常转移 | 友方 3 张卡各有 1 Power，敌方有 JU_ON | JU_ON 获得 3 Power，友方卡片 Power 清零 | 总数守恒 |
| A-2 | 友方无 Power | 友方无 Power | 无效果 | 提前返回 |
| A-3 | 敌方无 curse | 敌方无 JU_ON | 无效果，输出警告日志 | 不创建新卡片 |
| A-4 | 混合阵营 | CROW_CROWD 属于敌方 | 从敌方友方（即玩家）转移 Power 到玩家 curse | 阵营反转正确 |

---

## 2. CURSED_SEKELETON

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/Curse/CURSED_SEKELETON.prefab` |
| **Card Type ID** | `CURSED_SEKELETON` |
| **Description** | 墓地每有 1 友方 |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onMeRevealed`。
2. `CurseEffect.EnhanceCurse(int)` 增强敌方诅咒。
3. 增强层数基于墓地友方卡片数量（需确认具体传递参数）。

### Effect Formula

```
伤害/增强公式: 墓地友方卡片数量 = N -> 敌方 curse Power + N
若敌方无 JU_ON -> 创建 JU_ON 并施加 N Power
```

> **注意:** 批量读取未捕获 `EnhanceCurse` 的具体 int 参数。需 Inspector 确认 `effectEvent` 绑定的参数值是否为墓地友方计数，或是否为固定值。

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 墓地有友方 | 墓地有 3 张友方卡片 | 敌方 curse 获得 3 Power | 数量匹配 |
| A-2 | 墓地无友方 | 墓地无友方卡片 | 敌方 curse 获得 0 Power (或 1，需确认参数) | 边界条件 |
| A-3 | 敌方无 curse | 敌方无 JU_ON | 创建 JU_ON 并赋予 Power | CurseEffect.CreateEnemyCard |

---

## 3. CURSE_CORPSE

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/Curse/CURSE_CORPSE.prefab` |
| **Card Type ID** | `CURSE_CORPSE` |
| **Description** | 增强 1 敌方 [诅咒] |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onMeRevealed`。
2. `CurseEffect.EnhanceCurse(1)`：敌方 curse Power +1。
3. `HPAlterEffect.DecreaseTheirHpTimesX(times)`：多次造成伤害。

### Effect Formula

```
效果1: 敌方 curse Power +1
效果2: 造成 (baseDmg(2) + extraDmg(-1) + Power) = 1 + Power 伤害，共 X 次
```

> **注意:** `extraDmg = -1`，因此单次伤害 = `2 + (-1) + Power = 1 + Power`。`times` 参数需 Inspector 确认。

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 无 Power | CURSE_CORPSE 无 Power | curse +1 Power，造成 1 伤害 X 次 | extraDmg 正确抵消部分 baseDmg |
| A-2 | 有 Power | CURSE_CORPSE 有 1 Power | curse +1 Power，造成 2 伤害 X 次 | Power 叠加正确 |
| A-3 | 敌方无 curse | 敌方无 JU_ON | 创建 JU_ON +1 Power，伤害仍执行 | 创建与伤害并行 |

---

## 4. CURSE_ENCHANTMENT

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/Curse/CURSE_ENCHANTMENT.prefab` |
| **Card Type ID** | `CURSE_ENCHANTMENT` |
| **Description** | 萦绕: 当敌人受到伤害, 增强 1 敌方 [诅咒] |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onTheirPlayerTookDmg`（敌方受伤）。
2. `CheckCost_IndexBeforeStartCard` 确认在墓地。
3. `CurseEffect.EnhanceCurse(1)`：敌方 curse Power +1。

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 墓地触发 | CURSE_ENCHANTMENT 在墓地，敌方受伤 | 敌方 curse +1 Power | Linger + 受伤事件 |
| A-2 | 不在墓地 | CURSE_ENCHANTMENT 在 Start Card 上方 | 无效果 | CheckCost 阻止 |
| A-3 | 连续受伤 | 敌方一回合内受伤 2 次 | curse +2 Power（每次独立触发） | 事件触发频率 |

---

## 5. CURSE_SUMMONER

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/Curse/CURSE_SUMMONER.prefab` |
| **Card Type ID** | `CURSE_SUMMONER` |
| **Description** | 增强 1 敌方[诅咒] |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onMeRevealed`。
2. `CurseEffect.EnhanceCurse(1)`：敌方 curse Power +1。
3. `StageEffect.StageMyCards(1)`：置顶 1 友方卡片。

### Effect Formula

```
效果1: 敌方 curse Power +1
效果2: 置顶 1 友方卡片（随机，排除 Minion 和已在顶部的卡片）
```

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 正常触发 | 敌方有 JU_ON，友方有非 Minion 卡片 | curse +1 Power，置顶 1 友方 | 两个效果均执行 |
| A-2 | 敌方无 curse | 敌方无 JU_ON | 创建 JU_ON +1 Power，置顶友方 | 创建与 stage 并行 |

---

## 6. CURSE_THIRST_BEAST

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/Curse/CURSE_THIRST_BEAST.prefab` |
| **Card Type ID** | `CURSE_THIRST_BEAST` |
| **Description** | 萦绕: 当敌方 [诅咒] 揭晓时, 置顶自身 |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onEnemyCurseCardRevealed`。
2. Container "stage self": `StageEffect.StageSelf()`：将自身置顶。
3. Container "deal dmg": `HPAlterEffect.DecreaseTheirHp`：`totalDmg = 2 + 2 + Power = 4 + Power`。

### Effect Formula

```
触发条件: 敌方 curse 卡片揭晓
效果1: 自身置顶到 combinedDeckZone 顶部
效果2: 造成 4 + Power 伤害
```

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 敌方 curse 揭晓 | 敌方 JU_ON 被揭晓 | CURSE_THIRST_BEAST 置顶，造成 4+Power 伤害 | 两个 Container 均触发 |
| A-2 | 非 curse 揭晓 | 敌方非 curse 卡片揭晓 | 无效果 | 事件不匹配 |
| A-3 | 自身已在顶部 | CURSE_THIRST_BEAST 已在顶部 | 无 stage 效果（IsCardAtTop 检查），伤害仍执行 | StageSelf 边界条件 |

---

## 7. CURSE_THIRST_SHARMAN

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/Curse/CURSE_THIRST_SHARMAN.prefab` |
| **Card Type ID** | `CURSE_THIRST_SHARMAN` |
| **Description** | 敌方 [诅咒] 每有 1 力量, 给予 1 友方 1 力量 |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onMeRevealed`。
2. `StatusEffectGiverEffect.GiveStatusEffectToXFriendly_BasedOnIntSO(intSO)`:
   - `intSO` 为敌方 curse 的 Power 层数。
   - 给予 `xFriendlyCount = intSO.value` 张友方卡片各 1 层 Power。

### Effect Formula

```
X = 敌方 JU_ON 的 Power 层数
效果: 随机选择 X 张友方卡片，每张 +1 Power
```

> **注意:** 需确认 `intSO` 的具体引用（应为敌方 curse Power 计数器）。若敌方无 curse，X = 0，无效果。

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 敌方 curse 有 Power | 敌方 JU_ON 有 3 Power | 3 张友方卡片各 +1 Power | X 与 curse Power 匹配 |
| A-2 | 敌方 curse 无 Power | 敌方 JU_ON 有 0 Power | 无效果 | X = 0 |
| A-3 | 友方卡片不足 X | 友方只有 2 张卡片，X = 5 | 2 张友方各 +1 Power（实际数量取最小值） | 边界条件 |
| A-4 | 敌方无 curse | 敌方无 JU_ON | 无效果 | 无计数来源 |

---

## 8. DETERIORATION

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/Curse/DETERIORATION.prefab` |
| **Card Type ID** | `DETERIORATION` |
| **Description** | 敌方 [诅咒] 每有 2 力量, 增强 1 敌方 [诅咒] |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onMeRevealed`。
2. `CurseEffect.EnhanceCurseWithCoefficient(IntSO)`:
   - `powerCoefficient = 2`
   - `enhanceStacks = IntSO.value / 2`

### Effect Formula

```
N = 敌方 JU_ON Power 层数
增强层数 = N / 2 (整数除法)
效果: 敌方 curse Power + (N / 2)
```

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 偶数 Power | 敌方 JU_ON 有 4 Power | curse +2 Power | 4 / 2 = 2 |
| A-2 | 奇数 Power | 敌方 JU_ON 有 3 Power | curse +1 Power | 3 / 2 = 1 (整数除法) |
| A-3 | 0 Power | 敌方 JU_ON 有 0 Power | 无效果 | 0 / 2 = 0 |
| A-4 | 敌方无 curse | 敌方无 JU_ON | 创建 JU_ON，施加 (0/2)=0 Power | EnhanceCurse(0) 直接返回 |

---

## 9. JU_ON

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/Curse/JU_ON.prefab` |
| **Card Type ID** | `JU_ON` |
| **Description** | 对自己造成[力量]层数的伤害 |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onMeRevealed`。
2. `HPAlterEffect.DecreaseMyHp`:
   - `DmgCalculator()` 叠加 `baseDmg.value(2)` + 自身 Power 层数。
   - `totalDmg = extraDmg(-2) + dmgAmountAlter(2 + Power) = Power`。

### Effect Formula

```
selfDamage = baseDmg(2) + extraDmg(-2) + Power = Power
若 Power = 0 -> 0 伤害
若 Power = 3 -> 3 伤害
```

### Important Implementation Details

- `extraDmg = -2` 正好抵消 `baseDmg.value = 2`，使得最终伤害等于 Power 层数。
- 如果 Power = 0，`totalDmg = 0`，`ProcessDamage(0, ...)` 不会减少 HP。

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 无 Power | JU_ON 无 Power | 0 自伤 | baseDmg + extraDmg = 0 |
| A-2 | 有 3 Power | JU_ON 有 3 Power | 3 自伤 | Power 层数 = 伤害 |
| A-3 | 有护盾 | JU_ON 所属玩家有 5 Shield，3 Power | Shield 3 -> 0，HP 不变 | 护盾优先抵消 |
| A-4 | 敌方持有 | JU_ON 属于敌方，有 2 Power | 敌方受到 2 自伤 | 阵营正确 |

#### Strategy B

- 验证 JU_ON 揭示时的动画和日志输出。
- 确认 `isStatusEffectDamage = false`，因此会播放攻击动画。

---

## 10. MOTH_MAN

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/Curse/MOTH_MAN.prefab` |
| **Card Type ID** | `MOTH_MAN` |
| **Description** | 萦绕: 当敌方 [诅咒] 获得力量, 置顶 1 友方 |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onEnemyCurseCardGotPower`。
2. `CheckCost_IndexBeforeStartCard` 确认在墓地。
3. `StageEffect.StageMyCards(1)`：置顶 1 友方卡片。

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 墓地触发 | MOTH_MAN 在墓地，敌方 curse 获得 Power | 置顶 1 友方 | Linger + curse 获 Power |
| A-2 | 不在墓地 | MOTH_MAN 在 Start Card 上方 | 无效果 | CheckCost 阻止 |
| A-3 | 无友方可置顶 | 友方卡片都在顶部或是 Minion | stage 为 0 | 边界条件 |

---

## 11. POISNER

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/Curse/POISNER.prefab` |
| **Card Type ID** | `POISNER` |
| **Description** | 增强 1 敌方[诅咒] |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onMeRevealed`。
2. `CurseEffect.EnhanceCurse(1)`：敌方 curse Power +1。
3. `HPAlterEffect.DecreaseTheirHp`：`totalDmg = 2 + 1 + Power = 3 + Power`。

### Effect Formula

```
效果1: 敌方 curse Power +1
效果2: 造成 3 + Power 伤害
```

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 无 Power | POISNER 无 Power | curse +1，造成 3 伤害 | extraDmg = 1 |
| A-2 | 有 1 Power | POISNER 有 1 Power | curse +1，造成 4 伤害 | Power 叠加 |
| A-3 | 敌方无 curse | 敌方无 JU_ON | 创建 JU_ON +1 Power，造成 3+Power 伤害 | 创建与伤害并行 |

---

## 12. PREMATURE

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/Curse/PREMATURE.prefab` |
| **Card Type ID** | `PREMATURE` |
| **Description** | 敌方 [诅咒] 力量 - 1 |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onMeRevealed`。
2. `CurseEffect.ConsumeHostileCursePower(1)`：从敌方 curse 消耗 1 层 Power。
3. `StageEffect.StageTheirSpecificCard("JU_ON")`：将敌方 JU_ON 置顶。

### Effect Formula

```
效果1: 敌方所有 JU_ON 的 Power 总计 -1（从一张卡上逐层移除）
效果2: 将敌方 JU_ON 置顶到 combinedDeckZone 顶部
若敌方 JU_ON 总 Power < 1 -> 效果1 不执行，效果2 仍执行
```

### Important Implementation Details

- `ConsumeHostileCursePower` 会在所有敌方 JU_ON 中搜索 Power，如果总 Power < amount，直接返回。
- `StageTheirSpecificCard` 会在敌方卡组中搜索 cardTypeID == "JU_ON" 的卡片置顶。若找不到，显示失败信息。

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 敌方 curse 有 Power | 敌方 JU_ON 有 2 Power | JU_ON Power -> 1，JU_ON 置顶 | 消耗 + stage |
| A-2 | 敌方 curse 无 Power | 敌方 JU_ON 有 0 Power | 无消耗，JU_ON 置顶 | 消耗失败不阻止 stage |
| A-3 | 敌方无 JU_ON | 敌方无 JU_ON | 无消耗，stage 失败 | 两个效果均失败 |

---

## 13. PROLIFERATING_CURSE

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/Curse/PROLIFERATING_CURSE.prefab` |
| **Card Type ID** | `PROLIFERATING_CURSE` |
| **Description** | 复制敌方 1 [诅咒] |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onMeRevealed`。
2. `AddTempCard.CopyEnemyCurseCardToMe()`:
   - 搜索敌方 cardTypeID == `CurseCardTypeID` 的卡片。
   - 随机选择一张，复制到友方卡组（保留原有状态效果）。

### Effect Formula

```
效果: 随机复制 1 张敌方 curse 卡片到友方 combinedDeckZone
若敌方无 curse -> 不执行
```

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 敌方有 curse | 敌方有 2 JU_ON（各有 1 Power） | 复制 1 张 JU_ON 到友方，保留 1 Power | 状态效果保留 |
| A-2 | 敌方无 curse | 敌方无 JU_ON | 不执行，输出日志 | 边界条件 |
| A-3 | 属于敌方 | PROLIFERATING_CURSE 属于敌方 | 复制玩家 curse 到敌方 | 阵营反转 |

---

## 14. RIFT_CURSE

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/Curse/RIFT_CURSE.prefab` |
| **Card Type ID** | `RIFT_CURSE` |
| **Description** | 增强 1 敌方[诅咒] |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onMeRevealed`。
2. `CurseEffect.EnhanceCurse(1)`：敌方 curse Power +1。
3. `StageEffect.StageMyCards(1)`：置顶 1 友方卡片。

### Effect Formula

```
效果1: 敌方 curse Power +1
效果2: 置顶 1 友方卡片
```

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 正常触发 | 敌方有 JU_ON，友方有非 Minion 卡片 | curse +1，置顶 1 友方 | 与 CURSE_SUMMONER 类似 |

---

## 15. SACRIFICIAL_SPIRIT

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/Curse/SACRIFICIAL_SPIRIT.prefab` |
| **Card Type ID** | `SACRIFICIAL_SPIRIT` |
| **Description** | 埋葬 1 友方 |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onMeRevealed`。
2. `BuryEffect.BuryMyCards(1)`：埋葬 1 友方卡片。
3. `CurseEffect.EnhanceCurse(1)`：敌方 curse Power +1。

### Effect Formula

```
效果1: 埋葬 1 友方卡片（随机，排除 Minion 和已在底部的卡片）
效果2: 敌方 curse Power +1
```

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 正常触发 | 友方有 1+ 非 Minion，敌方有 JU_ON | 埋葬 1 友方，curse +1 Power | 两个效果均执行 |
| A-2 | 友方无有效目标 | 友方卡片都在底部或是 Minion | bury 为 0，curse +1 Power | Bury 边界条件 |
| A-3 | 敌方无 curse | 敌方无 JU_ON | 埋葬 1 友方，创建 JU_ON +1 Power | 创建与 bury 并行 |

---

## Strategy C: 回归批量测试 (Curse)

对 Curse 目录下所有 prefab 批量验证以下字段一致性:

1. **CurseEffect 配置检查**: 所有使用 `CurseEffect` 的卡片，确认 `cardPrefab` = JU_ON，`cardTypeID` = `CurseCardTypeID`。
2. **HPAlterEffect 双伤检查**:
   - CURSE_THIRST_BEAST: `baseDmg(2) + extraDmg(2)` = 4 基础伤害。
   - CURSE_CORPSE: `baseDmg(2) + extraDmg(-1)` = 1 基础伤害。
   - POISNER: `baseDmg(2) + extraDmg(1)` = 3 基础伤害。
   - JU_ON: `baseDmg(2) + extraDmg(-2)` = 0 基础伤害（依赖 Power）。
3. **Linger 卡片墓地检查**: CURSE_ENCHANTMENT、MOTH_MAN 的 `checkCostEvent` 是否绑定 `CheckCost_IndexBeforeStartCard`。
4. **TransferStatusEffectEffect 配置**: CROW_CROWD 的 `isFromFriendly` = True，`statusEffectToTransfer` = Power。
5. **AddTempCard 配置**: PROLIFERATING_CURSE 的 `curseCardTypeID` = `CurseCardTypeID`。
