# General 卡组测试计划

> 路径: `Assets/Prefabs/Cards/3.0 no cost (current)/General/`
> 本目录包含 25 张 General 派系卡片。

---

## 通用关键脚本

| Script | Path | Responsibility |
|--------|------|----------------|
| `HPAlterEffect` | `Assets/Scripts/Effects/HPAlterEffect.cs` | 伤害 / 治疗计算与交付 |
| `StageEffect` | `Assets/Scripts/Effects/StageEffect.cs` | 置顶卡片 |
| `BuryEffect` | `Assets/Scripts/Effects/BuryEffect.cs` | 埋葬卡片 |
| `StatusEffectGiverEffect` | `Assets/Scripts/Effects/StatusEffect/StatusEffectGiverEffect.cs` | 施加状态效果 |
| `ConsumeStatusEffect` | `Assets/Scripts/Effects/StatusEffect/ConsumeStatusEffect.cs` | 消耗状态效果 |
| `StatusEffectAmplifierEffect` | `Assets/Scripts/Effects/StatusEffect/StatusEffectAmplifierEffect.cs` | 放大状态效果获取量 |
| `TransferStatusEffectEffect` | `Assets/Scripts/Effects/TransferStatusEffectEffect.cs` | 转移状态效果 |
| `CurseEffect` | `Assets/Scripts/Effects/CurseEffect.cs` | 增强 / 消耗敌方诅咒 |
| `AddTempCard` | `Assets/Scripts/Effects/AddTempCard.cs` | 添加临时卡片 |
| `CostNEffectContainer` | `Assets/Scripts/Card/CostNEffectContainer.cs` | 代价检查与效果触发 |
| `CombatManager` | `Assets/Scripts/Managers/CombatManager.cs` | 管理 combinedDeckZone 与 revealZone |
| `ValueTrackerManager` | `Assets/Scripts/Managers/ValueTrackerManager.cs` | 追踪动态卡组计数 |
| `GameEventStorage` | `Assets/Scripts/Managers/GameEventStorage.cs` | 集中管理 GameEvent 引用 |

> **注意:** `BaseDmgRef.value = 2`。所有 HPAlterEffect 的 `DecreaseTheirHp` / `DecreaseMyHp` 会自动叠加 `baseDmg.value`。
> 因此 `totalDmg = baseDmg(2) + extraDmg + selfPowerCount`。

---

## 1. ADVANCE_PORTAL

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/General/ADVANCE_PORTAL.prefab` |
| **Card Type ID** | `ADVANCE_PORTAL` |
| **Description** | 置顶 2 友方 |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onMeRevealed`。
2. `CostNEffectContainer.InvokeEffectEvent()` → `StageEffect.StageMyCards(2)`。

### Effect Formula

```
从 combinedDeckZone 中随机选择 2 张友方非 Minion 卡片置顶。
排除已在顶部的卡片、Minion 卡片、Start Card 等中性卡。
```

### Important Implementation Details

- `StageMyCards` 先 Shuffle 再选取，结果具有随机性。
- 若友方有效卡片不足 2 张，只置顶实际可置顶的数量（`Mathf.Clamp`）。
- 置顶后触发 `onMeStaged` 事件。

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 正常置顶 | 友方有 3 张非 Minion 卡片 | 2 张友方卡片被置顶 | 数量正确 |
| A-2 | 友方不足 | 友方仅 1 张非 Minion 卡片 | 置顶 1 张 | 边界截断 |
| A-3 | 友方无目标 | 友方卡片全是 Minion 或已在顶部 | 无效果 | 空列表处理 |
| A-4 | 敌方持有 | ADVANCE_PORTAL 属于敌方 | 置顶敌方友方（即玩家）卡片 | 阵营反转 |

---

## 2. ALL_FOR_ONE

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/General/ALL_FOR_ONE.prefab` |
| **Card Type ID** | `ALL_FOR_ONE` |
| **Description** | 造成所有卡的 力量 数量的伤害 |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onMeRevealed`。
2. `HPAlterEffect.DecreaseTheirHp_BasedOnIntSO(IntSO)`：将 IntSO 的值叠加到 extraDmg，然后调用 `DecreaseTheirHp()`。

### Effect Formula

```
damage = baseDmg(2) + extraDmg(-2) + intSO.value + selfPower
       = intSO.value + selfPower
intSO: 需 Inspector 确认具体引用（推测为所有卡片的 Power 总数）。
```

### Important Implementation Details

- `extraDmg = -2` 正好抵消 `baseDmg.value = 2`，使最终伤害等于 IntSO 值 + 自身 Power。
- 需确认 IntSO 是否包含 revealZone 中的卡片、是否包含 Start Card。

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 无 Power | 全卡组无 Power | 造成 0 + selfPower 伤害 | base/extra 抵消 |
| A-2 | 全场 3 Power | 卡组中有 3 张卡各 1 Power | 造成 3 + selfPower 伤害 | IntSO 计数正确 |
| A-3 | 自身有 Power | ALL_FOR_ONE 有 2 Power，全场共 3 Power | 造成 5 伤害 | 包含自身 Power |
| A-4 | 敌方持有 | 属于敌方，全场 4 Power | 对玩家造成 4 + selfPower 伤害 | 阵营反转 |

---

## 3. ALMIGHTY

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/General/ALMIGHTY.prefab` |
| **Card Type ID** | `ALMIGHTY` |
| **Description** | 置顶 1 友方; 埋葬 1 敌方; 给予 1 友方 力量; 生成 1 [次元裂缝] 增强 1 敌方 [诅咒] |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onMeRevealed`。
2. 单一 Container 触发 4 个 effectEvent：
   - `StageEffect.StageMyCards(1)`
   - `BuryEffect.BuryTheirCards(1)`
   - `AddTempCard.AddCardToMe(riftPrefab)`
   - `CurseEffect.EnhanceCurse(1)`

### Effect Formula

```
效果1: 置顶 1 张随机友方非 Minion 卡片
效果2: 埋葬 1 张随机敌方非 Minion 卡片
效果3: 向友方 combinedDeckZone 添加 1 张 [次元裂缝] 临时卡
效果4: 敌方 curse 卡片 (JU_ON) Power +1；若不存在则创建 JU_ON 再 +1 Power
```

> **注意:** Prefab 扫描未捕获到 "给予 1 友方 力量" 的 StatusEffectGiver 组件。实际效果以扫描结果为准：stage + bury + add rift + enhance curse。

### Important Implementation Details

- 4 个效果在同一 Container 中顺序执行。
- `AddTempCard.cardCount = 1`，添加的是预制体引用的卡片。
- `CurseEffect.powerCoefficient = 1`，直接 `EnhanceCurse(1)`。

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 正常触发 | 双方均有非 Minion 卡，敌方有 JU_ON | 置顶 1 友方，埋葬 1 敌方，添加 1 rift，curse +1 Power | 4 效果均执行 |
| A-2 | 敌方无 curse | 敌方无 JU_ON | 创建 JU_ON 并 +1 Power，其余效果正常 | CurseEffect 创建逻辑 |
| A-3 | 无友方可置顶 | 友方全为 Minion 或已在顶部 | stage 0，bury/add/enhance 仍执行 | 部分失败不阻断 |
| A-4 | 敌方持有 | ALMIGHTY 属于敌方 | 阵营全部反转（置顶敌方卡片=玩家卡片） | 阵营正确 |

---

## 4. ANTI_CREATURE_WEAPON

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/General/ANTI_CREATURE_WEAPON.prefab` |
| **Card Type ID** | `ANTI_CREATURE_WEAPON` |
| **Description** | 埋葬 2 敌方 |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onMeRevealed`。
2. `BuryEffect.BuryTheirCards(2)`。

### Effect Formula

```
从 combinedDeckZone 中随机选择 2 张敌方非 Minion 卡片埋葬到底部。
排除已在底部的卡片、Minion 卡片、Start Card。
```

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 正常埋葬 | 敌方有 3 张非 Minion 卡片 | 2 张敌方卡片被埋葬到底部 | 数量正确 |
| A-2 | 敌方不足 | 敌方仅 1 张非 Minion 卡片 | 埋葬 1 张 | 边界截断 |
| A-3 | 敌方无目标 | 敌方卡片全是 Minion 或已在底部 | 无效果 | 空列表处理 |
| A-4 | 追踪计数 | 埋葬 2 张敌方卡片后 | ValueTrackerManager.enemyCardsBuriedCountRef +2 | 计数器更新 |

---

## 5. ARMED_SUMMONER

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/General/ARMED_SUMMONER.prefab` |
| **Card Type ID** | `ARMED_SUMMONER` |
| **Description** | 造成 3 伤害; 置顶 1 友方 |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onMeRevealed`。
2. 单一 Container 触发：
   - `HPAlterEffect.DecreaseTheirHp`：`totalDmg = 2 + 1 + Power = 3 + Power`
   - `StageEffect.StageMyCards(1)`

### Effect Formula

```
伤害 = baseDmg(2) + extraDmg(1) + selfPower = 3 + selfPower
置顶 = 1 张随机友方非 Minion 卡片
```

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 无 Power | ARMED_SUMMONER 无 Power | 造成 3 伤害，置顶 1 友方 | extraDmg 正确 |
| A-2 | 有 1 Power | 有 1 Power | 造成 4 伤害，置顶 1 友方 | Power 叠加 |
| A-3 | 无友方可置顶 | 友方全为 Minion 或已在顶部 | 造成 3 伤害，无置顶 | 部分失败不阻断 |
| A-4 | 敌方持有 | 属于敌方 | 对玩家造成 3+Power 伤害，置顶敌方友方 | 阵营反转 |

---

## 6. BLACKSMITH

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/General/BLACKSMITH.prefab` |
| **Card Type ID** | `BLACKSMITH` |
| **Description** | 造成 3 伤害; 给予一个友方 1 力量 |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onMeRevealed`。
2. 两个 Container：
   - Container "deal dmg": `HPAlterEffect.DecreaseTheirHp` → `3 + Power` 伤害
   - Container "apply power to friendly": `StatusEffectGiverEffect.GiveStatusEffectToXFriendly` → `xFriendlyCount=1, yFriendlyLayerCount=1`

### Effect Formula

```
伤害 = baseDmg(2) + extraDmg(1) + selfPower = 3 + selfPower
Power 给予 = 1 张随机友方卡片 +1 Power
```

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 正常触发 | 双方各有非 Minion 卡 | 造成 3+Power 伤害，1 友方 +1 Power | 两 Container 均触发 |
| A-2 | 无友方 | 友方无卡片（除 BLACKSMITH 自身） | 造成 3 伤害，无 Power 给予 | 边界条件 |
| A-3 | 有 Power | BLACKSMITH 有 2 Power | 造成 5 伤害 | Power 叠加正确 |
| A-4 | 敌方持有 | 属于敌方 | 对玩家造成伤害，给敌方友方（玩家卡片）Power | 阵营反转 |

---

## 7. BLIND_COMBAT_PRIEST

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/General/BLIND_COMBAT_PRIEST.prefab` |
| **Card Type ID** | `BLIND_COMBAT_PRIEST` |
| **Description** | 给予下 1 卡 3 力量 |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onMeRevealed`。
2. `StatusEffectGiverEffect.GiveStatusEffectToLastXCards`：
   - `lastXCardsCount = 1`
   - `statusEffectLayerCount = 3`
   - `statusEffectToGive = Power`

### Effect Formula

```
目标: 当前卡片在 combinedDeckZone 中 index - 1 方向的 1 张卡片
效果: 目标卡片获得 3 层 Power
若当前卡片在 revealZone，则从 combinedDeckZone 底部开始计算
```

### Important Implementation Details

- `GiveStatusEffectToLastXCards` 从当前卡片的索引往前数（index 递减方向）。
- 跳过 `ShouldSkipCard` 的卡片（Start Card 等）。
- 若找不到有效目标，无效果。

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 正常给予 | 下方有 1 张非 Skip 卡片 | 该卡片获得 3 Power | layerCount=3 |
| A-2 | 下方多张 | 下方有 3 张卡片 | 仅最近 1 张获得 3 Power | lastXCardsCount=1 |
| A-3 | 下方无目标 | 下方卡片全是 Start Card 或 Minion | 无效果 | 跳过逻辑 |
| A-4 | 在 revealZone | BLIND_COMBAT_PRIEST 在 revealZone | 从 combinedDeckZone 底部选 1 张给 3 Power | revealZone 分支 |

---

## 8. BONE_COMBINATION

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/General/BONE_COMBINATION.prefab` |
| **Card Type ID** | `BONE_COMBINATION` |
| **Description** | 造成 1 伤害 x 本回合埋葬敌方数量 |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onMeRevealed`。
2. `HPAlterEffect.DecreaseTheirHpTimes_BasedOnOpponentBuriedCount`：
   - 获取 `ValueTrackerManager.enemyCardsBuriedCountRef.value`
   - 循环调用 `DecreaseTheirHp()` 该次数

### Effect Formula

```
hitCount = 本回合敌方被埋葬的卡片数量 (ValueTrackerManager)
单次伤害 = baseDmg(2) + extraDmg(-1) + selfPower = 1 + selfPower
总伤害 = hitCount * (1 + selfPower)
```

### Important Implementation Details

- 每次循环独立调用 `DecreaseTheirHp()`，每次都会重新计算 `DmgCalculator()`（包括 Power）。
- 若 `enemyCardsBuriedCountRef = 0`，无伤害。
- 回合结束后计数器通常会重置（需确认 CombatManager 逻辑）。

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 无埋葬 | 本回合未埋葬敌方卡片 | 0 伤害 | hitCount=0 |
| A-2 | 埋葬 2 张 | 本回合埋葬 2 张敌方卡片，无 Power | 2 次 1 伤害 = 总计 2 伤害 | 次数匹配 |
| A-3 | 有 Power | 埋葬 2 张，BONE_COMBINATION 有 1 Power | 2 次 2 伤害 = 总计 4 伤害 | Power 每 hit 叠加 |
| A-4 | 敌方持有 | 属于敌方 | 使用 ownerCardsBuriedCountRef（玩家埋葬数） | 计数器阵营反转 |

---

## 9. BOOSTER

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/General/BOOSTER.prefab` |
| **Card Type ID** | `BOOSTER` |
| **Description** | 洗牌后, 置顶 2 友方; 埋葬 1 友方 |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `afterShuffle`（2 个 Listener 可能分别触发 2 个 Container）。
2. Container "bury friendly": `BuryEffect.BuryMyCards(1)`
3. Container "stage friendly": `StageEffect.StageMyCards(2)`

### Effect Formula

```
触发条件: afterShuffle 事件
效果1: 埋葬 1 张随机友方非 Minion 卡片
效果2: 置顶 2 张随机友方非 Minion 卡片
```

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 洗牌后触发 | 洗牌事件触发 | 1 张友方被埋葬，2 张友方被置顶 | afterShuffle 事件 |
| A-2 | 揭示时 | BOOSTER 被揭示（非洗牌） | 无效果 | 事件不匹配 |
| A-3 | 友方不足 | 友方仅 1 张非 Minion 卡片 | 埋葬 1 张，置顶 1 张 | 边界截断 |
| A-4 | 无友方目标 | 友方全为 Minion | 无效果 | 空列表处理 |

---

## 10. COFFIN_MAKER

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/General/COFFIN_MAKER.prefab` |
| **Card Type ID** | `COFFIN_MAKER` |
| **Description** | 造成 3 伤害; 埋葬 1 敌方 |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onMeRevealed`。
2. 单一 Container 触发：
   - `HPAlterEffect.DecreaseTheirHp`：`3 + Power` 伤害
   - `BuryEffect.BuryTheirCards(1)`

### Effect Formula

```
伤害 = baseDmg(2) + extraDmg(1) + selfPower = 3 + selfPower
埋葬 = 1 张随机敌方非 Minion 卡片
```

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 正常触发 | 敌方有非 Minion 卡 | 3+Power 伤害，埋葬 1 敌方 | 两效果均执行 |
| A-2 | 无敌方目标 | 敌方全为 Minion 或已在底部 | 3+Power 伤害，无埋葬 | 部分失败不阻断 |
| A-3 | 有 Power | COFFIN_MAKER 有 2 Power | 造成 5 伤害 | Power 叠加 |
| A-4 | 敌方持有 | 属于敌方 | 对玩家造成伤害，埋葬玩家卡片 | 阵营反转 |

---

## 11. DR_MANHATTON

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/General/DR_MANHATTON.prefab` |
| **Card Type ID** | `DR_MANHATTON` |
| **Description** | 消耗 4 力量: 置顶 2 友方, 埋葬 2 敌方 |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onMeRevealed`。
2. `CostNEffectContainer`：
   - `checkCostEvent` → `CheckCost_Power`（需 4 层 Power）
   - `effectEvent` → `ConsumeOwnStatusEffect(4)` → `StageMyCards(2)` → `BuryTheirCards(2)`

### Effect Formula

```
前提: 自身 Power 层数 >= 4
消耗: 4 层 Power
效果1: 置顶 2 张友方非 Minion 卡片
效果2: 埋葬 2 张敌方非 Minion 卡片
若 Power < 4 -> 全部效果不执行
```

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 足够 Power | DR_MANHATTON 有 4 Power | 消耗 4 Power，置顶 2 友方，埋葬 2 敌方 | 代价扣除正确 |
| A-2 | Power 不足 | 有 3 Power | 无效果，Power 不消耗 | 代价检查阻止 |
| A-3 | 恰好 4 Power | 有 4 Power | 消耗后 Power = 0，执行效果 | 边界条件 |
| A-4 | 目标不足 | 有 4 Power，敌方仅 1 张非 Minion | 消耗 4 Power，置顶 2 友方，埋葬 1 敌方 | 边界截断 |
| A-5 | 敌方持有 | 属于敌方，有 4 Power | 阵营反转（置顶敌方卡片，埋葬玩家卡片） | 阵营正确 |

---

## 12. ELDER_SOURCEROR

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/General/ELDER_SOURCEROR.prefab` |
| **Card Type ID** | `ELDER_SOURCEROR` |
| **Description** | 本回合每置顶过 1 友方, 给予 1 友方 1 力量 |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onMeRevealed`。
2. `StatusEffectGiverEffect.GiveStatusEffectToXFriendly_BasedOnStaged(layerCount=1)`：
   - `xCount = ValueTrackerManager.stagedOwnerRef.value`（或 stagedEnemyRef）
   - `yFriendlyLayerCount = 1`

### Effect Formula

```
X = 本回合己方置顶过的友方卡片数量 (stagedOwnerRef / stagedEnemyRef)
效果: 随机选择 X 张友方卡片，每张 +1 Power
若 X = 0 -> 无效果
```

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 置顶 3 张 | 本回合已置顶 3 张友方卡片 | 3 张友方各 +1 Power | X=3 |
| A-2 | 无置顶 | 本回合未置顶友方卡片 | 无效果 | X=0 |
| A-3 | 友方不足 X | 置顶 5 张，友方仅 2 张 | 2 张友方各 +1 Power | 最小值截断 |
| A-4 | 敌方持有 | 属于敌方 | 使用 stagedEnemyRef，给敌方友方 Power | 计数器阵营反转 |

---

## 13. ETERNAL_GHOST

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/General/ETERNAL_GHOST.prefab` |
| **Card Type ID** | `ETERNAL_GHOST` |
| **Description** | 萦绕; 敌人受到伤害时, 造成 1 伤害 |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onTheirPlayerTookDmg`（敌方受伤）。
2. `CheckCost_IndexBeforeStartCard`：确认自身在墓地（Start Card 下方）。
3. `HPAlterEffect.DecreaseTheirHp`：`totalDmg = 2 + (-1) + selfPower = 1 + selfPower`

### Effect Formula

```
触发条件: 敌方受到任何伤害，且 ETERNAL_GHOST 在墓地
伤害 = baseDmg(2) + extraDmg(-1) + selfPower = 1 + selfPower
```

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 墓地带伤 | 在墓地，敌方受伤 1 次 | 造成 1+Power 伤害 | Linger + 受伤触发 |
| A-2 | 不在墓地 | 在 Start Card 上方，敌方受伤 | 无效果 | CheckCost 阻止 |
| A-3 | 连续受伤 | 敌方一回合受伤 3 次 | 触发 3 次，每次 1+Power | 触发频率 |
| A-4 | 有 Power | 在墓地，有 2 Power，敌方受伤 | 造成 3 伤害 | Power 叠加 |

---

## 14. FLESH_COMBINATION

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/General/FLESH_COMBINATION.prefab` |
| **Card Type ID** | `FLESH_COMBINATION` |
| **Description** | 造成友方数量的伤害 |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onMeRevealed`。
2. `HPAlterEffect.DecreaseTheirHp_BasedOnFriendlyCardCountInDeck`：
   - `friendlyCount = ValueTrackerManager.ownerCardCountInDeckRef.value`（或 enemyCardCountInDeckRef）
   - `extraDmg += friendlyCount`，然后 `DecreaseTheirHp()`

### Effect Formula

```
damage = baseDmg(2) + extraDmg(-2) + friendlyCount + selfPower
       = friendlyCount + selfPower
friendlyCount: 己方在 combinedDeckZone 中的卡片数量（含 revealZone 吗？需确认 ValueTrackerManager）
```

### Important Implementation Details

- 需确认 `ValueTrackerManager` 的计数是否包含 `revealZone`、是否包含 Start Card。
- `extraDmg = -2` 抵消 `baseDmg = 2`，最终伤害等于友方数量 + 自身 Power。

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 5 张友方 | 己方有 5 张卡片在 deck | 造成 5 + selfPower 伤害 | 数量匹配 |
| A-2 | 0 张友方 | 己方无卡片（除 FLESH_COMBINATION） | 造成 0 + selfPower 伤害 | 边界条件 |
| A-3 | 有 Power | 5 张友方，自身 2 Power | 造成 7 伤害 | Power 叠加 |
| A-4 | 敌方持有 | 属于敌方 | 使用 enemyCardCountInDeckRef | 计数器阵营反转 |

---

## 15. GOBLIN_ASSASIN_TEAM

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/General/GOBLIN_ASSASIN_TEAM.prefab` |
| **Card Type ID** | `GOBLIN_ASSASIN_TEAM` |
| **Description** | 造成 4 伤害; 被置顶: 埋葬 1 敌方 |
| **Is Minion** | False |

### Implementation Chain

1. Listener 1 监听 `onMeRevealed` → Container "deal dmg": `HPAlterEffect.DecreaseTheirHp` → `4 + Power`
2. Listener 2 监听 `onMeStaged` → Container "bury hostile": `BuryEffect.BuryTheirCards(1)`

### Effect Formula

```
揭示时伤害 = baseDmg(2) + extraDmg(2) + selfPower = 4 + selfPower
被置顶时 = 埋葬 1 张随机敌方非 Minion 卡片
```

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 揭示触发 | 正常揭示 | 造成 4+Power 伤害 | onMeRevealed |
| A-2 | 置顶触发 | 被其他效果置顶 | 埋葬 1 敌方 | onMeStaged |
| A-3 | 双触发 | 揭示并随后被置顶 | 造成伤害 + 埋葬 | 两个 Listener 独立 |
| A-4 | 无敌方可埋 | 被置顶时敌方无有效目标 | 埋葬 0 | 边界条件 |

---

## 16. MAD_SCIENTIST

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/General/MAD_SCIENTIST.prefab` |
| **Card Type ID** | `MAD_SCIENTIST` |
| **Description** | 给予下 3 卡 2 力量 |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onMeRevealed`。
2. `StatusEffectGiverEffect.GiveStatusEffectToLastXCards`：
   - `lastXCardsCount = 3`
   - `statusEffectLayerCount = 2`
   - `statusEffectToGive = Power`

### Effect Formula

```
目标: 当前卡片下方最近的 3 张非 Skip 卡片
效果: 每张获得 2 层 Power
```

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 下方 3 卡 | 下方有 3 张非 Skip 卡片 | 3 张各 +2 Power | 数量与层数正确 |
| A-2 | 下方仅 2 卡 | 下方仅 2 张非 Skip 卡片 | 2 张各 +2 Power | 边界截断 |
| A-3 | 下方无目标 | 下方全是 Start Card | 无效果 | 跳过逻辑 |
| A-4 | 在 revealZone | MAD_SCIENTIST 在 revealZone | 从 deck 底部选 3 张各 +2 Power | revealZone 分支 |

---

## 17. OVERCHARGED_SUMMONER

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/General/OVERCHARGED_SUMMONER.prefab` |
| **Card Type ID** | `OVERCHARGED_SUMMONER` |
| **Description** | 置顶 1 友方; 给予下 2 卡 1 力量 |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onMeRevealed`。
2. 单一 Container 触发：
   - `StageEffect.StageMyCards(1)`
   - `StatusEffectGiverEffect.GiveStatusEffectToLastXCards`：`lastXCardsCount=2, statusEffectLayerCount=1`

### Effect Formula

```
置顶 = 1 张随机友方非 Minion 卡片
Power = 下方 2 张卡片各 +1 Power
```

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 正常触发 | 友方有非 Minion 卡，下方有 2 张卡 | 置顶 1 友方，下方 2 卡各 +1 Power | 两效果均执行 |
| A-2 | 无友方可置顶 | 友方全为 Minion | 无置顶，下方 2 卡仍 +1 Power | 部分失败不阻断 |
| A-3 | 下方仅 1 卡 | 下方仅 1 张非 Skip 卡片 | 置顶 1 友方，1 卡 +1 Power | 边界截断 |
| A-4 | 敌方持有 | 属于敌方 | 置顶敌方友方，给敌方下方卡片 Power | 阵营反转 |

---

## 18. POWER_CRAVER

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/General/POWER_CRAVER.prefab` |
| **Card Type ID** | `POWER_CRAVER` |
| **Description** | 造成 3 伤害; 获得 3 倍力量 |
| **Is Minion** | False |

### Implementation Chain

1. Listener 1 监听 `onMeRevealed` → Container "deal dmg": `HPAlterEffect.DecreaseTheirHp` → `3 + Power`
2. Listener 2 监听 `onMeGotStatusEffect` → Container "gain triple power":
   - `StatusEffectAmplifierEffect.AmplifyStatusEffectGain`
   - `statusEffectMultiplier = 3`, `statusEffectToCount = Power`, `statusEffectToGive = Power`

### Effect Formula

```
揭示时伤害 = baseDmg(2) + extraDmg(1) + selfPower = 3 + selfPower
放大效果: 当获得 Power 时，额外获得 2 * amount 层 Power（总计 3x）
```

### Important Implementation Details

- `AmplifyStatusEffectGain` 检查 `combatManager.lastCardGotStatusEffect` 是否等于自身。
- 检查 `lastAppliedStatusEffectRef` 是否为 Power，才执行放大。
- 放大只作用于通过 `ApplyStatusEffectCore` 获得 Power 的情况。

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 揭示伤害 | 正常揭示，无 Power | 造成 3 伤害 | 基础伤害 |
| A-2 | 获得 Power | 被其他卡片给予 1 Power | 实际获得 3 Power（1 + 2） | 3 倍放大 |
| A-3 | 连续获 Power | 连续获得 2 次 1 Power | 每次各放大为 3，总计 6 Power | 多次触发 |
| A-4 | 获得非 Power | 被给予 Rest 状态 | Rest 正常获得，不触发放大 | 类型过滤 |

---

## 19. POWER_SIPHONER

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/General/POWER_SIPHONER.prefab` |
| **Card Type ID** | `POWER_SIPHONER` |
| **Description** | 转移所有友方的 力量 到自身; 造成 2 伤害 x 2 |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onMeRevealed`。
2. 单一 Container 触发：
   - `TransferStatusEffectEffect.TransferAllStatusEffectToHostileCurse()`：将所有友方 Power 转移到敌方 curse 卡
   - `HPAlterEffect.DecreaseTheirHpTimesX`：多次造成伤害

### Effect Formula

```
效果1: 收集所有友方卡片的 Power 层数之和 -> 转移到敌方 JU_ON
         若敌方无 JU_ON -> 效果不执行（输出警告）
效果2: 造成 X 次伤害，每次 = baseDmg(2) + extraDmg(0) + selfPower = 2 + selfPower
         X 参数需 Inspector 确认（描述暗示 X=2）
```

> **注意:** `TransferAllStatusEffectToHostileCurse` 的 `isFromFriendly = True`，目标为敌方 curse 卡（JU_ON）。
> 这与描述 "转移到自身" 有出入。实际代码转移给敌方 curse。

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 正常转移 | 友方 3 张卡各有 1 Power，敌方有 JU_ON | 友方 Power 清零，JU_ON +3 Power，造成 X 次伤害 | 转移 + 伤害 |
| A-2 | 友方无 Power | 友方无 Power | 无转移，仍造成伤害 | 空源处理 |
| A-3 | 敌方无 curse | 敌方无 JU_ON | 无转移（警告日志），仍造成伤害 | 目标缺失 |
| A-4 | 伤害次数 | 无 Power，baseDmg=2 | 每次 2 伤害，共 X 次 | 次数与数值 |

---

## 20. POWER_TRANSFER

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/General/POWER_TRANSFER.prefab` |
| **Card Type ID** | `POWER_TRANSFER` |
| **Description** | 去除 2 敌方 1 力量; 给予 2 友方 1 力量 |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onMeRevealed`（2 个 Listener 分别触发 2 个 Container，或同一事件触发同一 Container 两次 —— 需确认）。
2. Container "consume hostile power; give friendly power"：
   - `ConsumeStatusEffect.ConsumeRandomEnemyCardsStatusEffect(2)`：从 2 张随机敌方卡片各消耗 1 Power
   - `StatusEffectGiverEffect.GiveStatusEffect`：给予友方 Power

### Effect Formula

```
效果1: 随机选择 2 张有 Power 的敌方卡片，各移除 1 层 Power
         若敌方有 Power 的卡片不足 2 张 -> 尽可能移除
效果2: 给予友方 Power（具体数量需 Inspector 确认 xFriendlyCount/yFriendlyLayerCount）
```

> **注意:** 扫描显示只有 1 个 Container 但 2 个 Listener。若两 Listener 绑定同一事件，会导致双次触发（潜在 Bug）。
> 需 Inspector 确认 Listener 的事件绑定。

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 正常触发 | 敌方 2 张卡各有 1 Power | 2 敌方各 -1 Power，友方获得 Power | 消耗 + 给予 |
| A-2 | 敌方无 Power | 敌方无 Power | 无消耗，友方仍获得 Power | 空源处理 |
| A-3 | 敌方仅 1 张有 Power | 1 张敌方有 2 Power | 该卡 -1 Power，友方获得 Power | 边界截断 |
| A-4 | 双 Listener | 若两 Listener 同事件 | 效果执行两次（Bug？） | 触发频率验证 |

---

## 21. QUICK_RESPONSE_PROTOCAL

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/General/QUICK_RESPONSE_PROTOCAL.prefab` |
| **Card Type ID** | `QUICK_RESPONSE_PROTOCAL` |
| **Description** | 萦绕; 每 3 敌人被揭晓: 置顶 1 友方 |
| **Is Minion** | False |
| **Tags** | Linger |

### Implementation Chain

1. Listener 监听 `onHostileCardRevealed`（或 `onAnyCardRevealed`）。
2. Container "if in grave, add counter":
   - `CheckCost_IndexBeforeStartCard`：确认在墓地
   - `StatusEffectGiverEffect.GiveSelfStatusEffect(1)`：自身获得 1 层 Counter
3. Container "if counter requirement met, stage friendly":
   - `CheckCost_Counter`：确认 Counter 层数达到阈值（推测为 3）
   - `StageEffect.StageMyCards(1)`

### Effect Formula

```
触发条件: 敌方卡片被揭晓，且自身在墓地
步骤1: 每触发 1 次 -> 自身 Counter +1
步骤2: 当 Counter >= 3 -> 置顶 1 友方，Counter 被消耗（需确认是否消耗）
```

### Important Implementation Details

- 需确认 `CheckCost_Counter` 的具体阈值（推测为 3，与描述一致）。
- 需确认 Counter 是否在阶段 2 后被消耗。
- Linger 卡片在墓地才生效。

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 3 次触发 | 在墓地，敌方揭示 3 张卡 | 第 3 次触发后置顶 1 友方 | Counter 累积 |
| A-2 | 不在墓地 | 在 Start Card 上方，敌方揭示 | 无效果 | CheckCost 阻止 |
| A-3 | 2 次触发 | 在墓地，敌方揭示 2 张卡 | Counter=2，无置顶 | 阈值未达 |
| A-4 | 6 次触发 | 在墓地，敌方揭示 6 张卡 | 置顶 2 次（每 3 次 1 次） | 多次达标 |

---

## 22. TACTICAL_BREACHER

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/General/TACTICAL_BREACHER.prefab` |
| **Card Type ID** | `TACTICAL_BREACHER` |
| **Description** | 造成 4 伤害; 被置顶: 获得 1 力量 |
| **Is Minion** | False |

### Implementation Chain

1. Listener 1 监听 `onMeRevealed` → Container "deal dmg": `HPAlterEffect.DecreaseTheirHp` → `4 + Power`
2. Listener 2 监听 `onMeStaged` → Container "gain power": `StatusEffectGiverEffect.GiveSelfStatusEffect(1)` → 自身 +1 Power

### Effect Formula

```
揭示时伤害 = baseDmg(2) + extraDmg(2) + selfPower = 4 + selfPower
被置顶时 = 自身获得 1 层 Power
```

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 揭示触发 | 正常揭示 | 造成 4+Power 伤害 | onMeRevealed |
| A-2 | 置顶触发 | 被其他效果置顶 | 自身 +1 Power | onMeStaged |
| A-3 | 双触发 | 揭示并随后被置顶 | 造成伤害 + 获得 Power | 两个 Listener 独立 |
| A-4 | 有 Power | 自身已有 2 Power，被置顶 | +1 Power -> 总计 3 Power | 叠加正确 |

---

## 23. THE_FOOL

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/General/THE_FOOL.prefab` |
| **Card Type ID** | `THE_FOOL` |
| **Description** | 置顶 力量最多的 敌方; 造成 4 伤害 |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onMeRevealed`。
2. 两个 Container（或同一 Listener 触发两个 Container）：
   - Container "stage hostile with most power": `StageEffect.StageCardWithMostStatusEffect` → `targetFriendly=false, statusEffectToCheck=Power`
   - Container "deal dmg": `HPAlterEffect.DecreaseTheirHp` → `4 + Power`

### Effect Formula

```
置顶: 在敌方非 Minion 卡片中查找 Power 层数最多的 1 张，置顶到 deck 顶部
       若多张并列最多 -> 随机选 1 张
伤害 = baseDmg(2) + extraDmg(2) + selfPower = 4 + selfPower
```

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 正常触发 | 敌方有 1 张 3 Power 卡，其余 0 Power | 置顶该卡，造成 4+Power 伤害 | 最大值查找 |
| A-2 | 并列最多 | 敌方 2 张卡各有 2 Power | 随机置顶其中 1 张 | 随机选择 |
| A-3 | 敌方无 Power | 敌方所有卡 0 Power | 随机置顶 1 张敌方卡（maxCount=-1 时所有卡并列） | 零值边界 |
| A-4 | 敌方无卡 | 敌方无非 Minion 卡片 | 无置顶，仍造成 4+Power 伤害 | 部分失败不阻断 |

---

## 24. UNFINISHED_ROBOT

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/General/UNFINISHED_ROBOT.prefab` |
| **Card Type ID** | `UNFINISHED_ROBOT` |
| **Description** | 造成 0 伤害; 翻倍自身力量 |
| **Is Minion** | False |
| **Status Effects** | Power, Power |

### Implementation Chain

1. `GameEventListener` 监听 `onMeRevealed`。
2. `StatusEffectGiverEffect.GiveSelfStatusEffectBasedOnStatusEffectCount`：
   - `statusEffectToCount = Power`
   - `statusEffectToGive = Power`
   - 给予自身 Power 层数 = 当前自身 Power 层数

### Effect Formula

```
初始状态: 2 层 Power（Prefab 自带）
揭示时: 获得 2 层 Power（基于当前 Power 数量）
HPAlterEffect: 未在扫描中捕获直接伤害组件，但 CardDesc 说 "造成 0 伤害"
```

> **注意:** Prefab 扫描未捕获到 HPAlterEffect 组件。但 CardDesc 明确写有 "造成 0 伤害"。
> 可能 `extraDmg = -2` 的 HPAlterEffect 未被识别，或实际无伤害逻辑。

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 首次揭示 | 首次被揭示 | Power 从 2 -> 4 | 翻倍正确 |
| A-2 | 第二次揭示 | 已有 4 Power，再次被揭示 | Power 从 4 -> 8 | 指数增长 |
| A-3 | 伤害验证 | 若有 HPAlterEffect extraDmg=-2 | 伤害 = 2 + (-2) + Power = Power | 0 基础伤害验证 |
| A-4 | 敌方持有 | 属于敌方 | 翻倍敌方 UNFINISHED_ROBOT 的 Power | 阵营正确 |

---

## 25. WEAPON_SPIRIT

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/General/WEAPON_SPIRIT.prefab` |
| **Card Type ID** | `WEAPON_SPIRIT` |
| **Description** | 萦绕; 当友方获得 力量 时, 给予该友方 1 力量 |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onFriendlyCardGotPower`。
2. `CheckCost_IndexBeforeStartCard`：确认自身在墓地。
3. `PowerReactionEffect.GivePowerToCardThatGotPower(1)`：
   - 给予最近获得 Power 的友方卡片额外 1 层 Power

### Effect Formula

```
触发条件: 友方卡片获得 Power，且 WEAPON_SPIRIT 在墓地
效果: 该获得 Power 的友方卡片额外 +1 Power
```

### Important Implementation Details

- `PowerReactionEffect` 需要 `combatManager.lastCardGotStatusEffect` 来追踪最近获得 Power 的卡片。
- `excludeSelf = False`，因此如果 WEAPON_SPIRIT 自己获得 Power，它也会给自己 +1（但通常 Linger 卡不在 deck 中）。

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 墓地带伤 | 在墓地，友方某卡获得 1 Power | 该卡实际获得 2 Power（1 + 1） | 额外叠加 |
| A-2 | 不在墓地 | 在 Start Card 上方 | 无效果 | CheckCost 阻止 |
| A-3 | 连续触发 | 2 张不同友方卡各获得 1 Power | 每张各额外 +1 Power | 多目标正确 |
| A-4 | 自身触发 | WEAPON_SPIRIT 自己获得 Power（罕见） | 若 excludeSelf=False，自身 +1 | 自引用边界 |

---

## Strategy C: 回归批量测试 (General)

对 General 目录下所有 prefab 批量验证以下字段一致性:

1. **HPAlterEffect 伤害公式检查**:
   - ARMED_SUMMONER: `baseDmg(2) + extraDmg(1)` = 3 基础伤害
   - BLACKSMITH: `baseDmg(2) + extraDmg(1)` = 3 基础伤害
   - COFFIN_MAKER: `baseDmg(2) + extraDmg(1)` = 3 基础伤害
   - GOBLIN_ASSASIN_TEAM: `baseDmg(2) + extraDmg(2)` = 4 基础伤害
   - TACTICAL_BREACHER: `baseDmg(2) + extraDmg(2)` = 4 基础伤害
   - THE_FOOL: `baseDmg(2) + extraDmg(2)` = 4 基础伤害
   - POWER_CRAVER: `baseDmg(2) + extraDmg(1)` = 3 基础伤害
   - ETERNAL_GHOST: `baseDmg(2) + extraDmg(-1)` = 1 基础伤害
   - BONE_COMBINATION: `baseDmg(2) + extraDmg(-1)` = 1 基础伤害（每次 hit）
   - FLESH_COMBINATION: `baseDmg(2) + extraDmg(-2)` = 0 基础伤害（依赖 friendlyCount）
   - UNFINISHED_ROBOT: 若存在 HPAlterEffect，应为 `extraDmg = -2` 以达成 0 基础伤害

2. **Linger 卡片墓地检查**:
   - ETERNAL_GHOST、QUICK_RESPONSE_PROTOCAL、WEAPON_SPIRIT 的 `checkCostEvent` 是否绑定 `CheckCost_IndexBeforeStartCard`。

3. **Stage/Bury 目标过滤检查**:
   - 所有 StageEffect 和 BuryEffect 是否正确排除了 `isMinion` 和 `ShouldSkipEffectProcessing` 卡片。

4. **StatusEffectGiver 配置检查**:
   - BLIND_COMBAT_PRIEST: `lastXCardsCount=1, statusEffectLayerCount=3`
   - MAD_SCIENTIST: `lastXCardsCount=3, statusEffectLayerCount=2`
   - OVERCHARGED_SUMMONER: `lastXCardsCount=2, statusEffectLayerCount=1`
   - ELDER_SOURCEROR: `GiveStatusEffectToXFriendly_BasedOnStaged(1)`

5. **多 Container 卡片触发检查**:
   - BOOSTER、GOBLIN_ASSASIN_TEAM、TACTICAL_BREACHER、POWER_TRANSFER 等有多个 Listener/Container 的卡片，确认各 Listener 绑定的事件不重复（避免双次触发）。

6. **Cost 卡片检查**:
   - DR_MANHATTON: `checkCostEvent` 绑定 `CheckCost_Power`，`ConsumeStatusEffect` 消耗 4 Power。
