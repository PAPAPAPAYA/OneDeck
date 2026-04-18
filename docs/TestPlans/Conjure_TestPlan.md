# Conjure 卡组测试计划

> 路径: `Assets/Prefabs/Cards/3.0 no cost (current)/Conjure/`
> 本目录包含 11 张 Conjure 派系卡片。

---

## 通用关键脚本

| Script | Path | Responsibility |
|--------|------|----------------|
| `HPAlterEffect` | `Assets/Scripts/Effects/HPAlterEffect.cs` | 伤害 / 治疗计算与交付 |
| `BuryEffect` | `Assets/Scripts/Effects/BuryEffect.cs` | 埋葬卡片到卡组底部 |
| `StageEffect` | `Assets/Scripts/Effects/StageEffect.cs` | 置顶卡片到卡组顶部 |
| `ExileEffect` | `Assets/Scripts/Effects/ExileEffect.cs` | 放逐卡片 |
| `CurseEffect` | `Assets/Scripts/Effects/CurseEffect.cs` | 增强敌方诅咒 |
| `AddTempCard` | `Assets/Scripts/Effects/AddTempCard.cs` | 添加临时卡片 |
| `MinionCostEffect` | `Assets/Scripts/Effects/MinionCostEffect.cs` | Minion 消耗代价 |
| `StatusEffectGiverEffect` | `Assets/Scripts/Effects/StatusEffect/StatusEffectGiverEffect.cs` | 施加状态效果 |
| `CostNEffectContainer` | `Assets/Scripts/Card/CostNEffectContainer.cs` | 代价检查与效果触发 |
| `CombatManager` | `Assets/Scripts/Managers/CombatManager.cs` | 管理 combinedDeckZone 与 revealZone |
| `GameEventStorage` | `Assets/Scripts/Managers/GameEventStorage.cs` | 集中管理 GameEvent 引用 |

> **注意:** `BaseDmgRef.value = 2`。所有 HPAlterEffect 的 `DecreaseTheirHp` / `DecreaseMyHp` 会自动叠加 `baseDmg.value`。

---

## 1. DEATHBED_CURSE

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/Conjure/DEATHBED_CURSE.prefab` |
| **Card Type ID** | `DEATHBED_CURSE` |
| **Description** | 萦绕: 当友方被去除, 敌方[行将就木]获得 1 力量 |
| **Is Minion** | False |
| **Tags** | Linger |

### Implementation Chain

1. `GameEventListener` 监听 `onFriendlyCardExiled`。
2. `CostNEffectContainer` 调用 `CheckCost_IndexBeforeStartCard`（确认卡片在墓地，即 Start Card 之前）。
3. `CurseEffect.EnhanceCurse(int)` 增强敌方诅咒卡片（JU_ON）1 层 Power。
4. 若敌方没有 JU_ON，则使用 `cardPrefab` 创建一张。

### Effect Formula

```
触发条件: onFriendlyCardExiled + 卡片在墓地 (index < StartCard index)
效果: 敌方 JU_ON Power +1
若 JU_ON 不存在 -> 创建 JU_ON 并施加 Power
```

### Important Implementation Details

- Linger 卡片在墓地（Start Card 下方）才触发效果。
- `CheckCost_IndexBeforeStartCard` 会跳过位于 Start Card 上方（存活区）的触发。
- 创建诅咒卡片时，`CreateEnemyCard` 会将其添加到 combinedDeckZone[0]（底部）。

### Test Cases

#### Strategy A: Programmatic Unit Test

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | Linger 在墓地触发 | combinedDeckZone 包含 DEATHBED_CURSE(墓地) + 友方卡片被放逐 | 敌方 JU_ON 获得 1 Power | 墓地检查通过，诅咒增强 |
| A-2 | Linger 不在墓地 | DEATHBED_CURSE 在 Start Card 上方 | 无效果 | CheckCost_IndexBeforeStartCard 阻止触发 |
| A-3 | 敌方无 JU_ON | 敌方卡组无 JU_ON | 创建 JU_ON 并赋予 1 Power | CurseEffect.CreateEnemyCard 被调用 |
| A-4 | 敌方已有 JU_ON | 敌方卡组已有 JU_ON(无 Power) | JU_ON 获得 1 Power | 不重复创建，直接增强 |

#### Strategy B: Play Mode Integration Test

- 将 DEATHBED_CURSE 置入墓地，观察友方卡片被放逐时是否正确触发。
- 验证 `onEnemyCurseCardGotPower` 事件是否被正确 Raise。

---

## 2. FALL_INTO_RIFT

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/Conjure/FALL_INTO_RIFT.prefab` |
| **Card Type ID** | `FALL_INTO_RIFT` |
| **Description** | 去除 1 [次元裂缝] |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onMeRevealed`。
2. `CostNEffectContainer` 先执行 `preEffectEvent` -> `MinionCostEffect.ExecuteMinionCost`。
3. MinionCost: 消耗 1 个友方 Minion 卡片。
4. 代价成功后，执行 `effectEvent` -> `BuryEffect.BuryTheirCards(1)`。

### Effect Formula

```
代价: 1 友方 Minion
效果: 埋葬 1 张敌方卡片（随机选择，排除已在底部的卡片和 Minion）
```

### Important Implementation Details

- `MinionCostEffect` 从 combinedDeckZone 中筛选 `isMinion == true` 且属于卡片所有者的卡片。
- 如果 Minion 不足，效果不触发并显示失败信息。
- `BuryTheirCards` 会跳过敌方 Minion 和已在底部的卡片。

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 代价充足 | 友方有 1+ Minion | 消耗 1 Minion + 埋葬 1 敌方卡片 | MinionCost 成功，Bury 执行 |
| A-2 | 代价不足 | 友方无 Minion | 无效果，显示代价失败信息 | MinionCost 阻止后续效果 |
| A-3 | 敌方无有效目标 | 友方有 Minion，但敌方卡片都在底部或是 Minion | 消耗 Minion，但 bury 数量为 0 | BuryEffect 优雅处理空列表 |

#### Strategy B

- 进入战斗，确保友方卡组包含 Minion。
- 观察 FALL_INTO_RIFT 揭示时是否正确消耗 Minion 并埋葬敌方卡片。

---

## 3. RIFT

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/Conjure/RIFT.prefab` |
| **Card Type ID** | `RIFT` |
| **Description** | 置顶 1 友方 |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onMeRevealed`。
2. `CostNEffectContainer` 触发 `effectEvent`。
3. `StageEffect.StageMyCards(1)` 置顶 1 张友方卡片。
4. `ExileEffect.ExileSelf()` 放逐自身。

### Effect Formula

```
效果1: 置顶 1 友方卡片（随机，排除 Minion 和已在顶部的卡片）
效果2: 放逐 RIFT 自身
```

### Important Implementation Details

- `StageMyCards` 从 combinedDeckZone 中筛选友方非 Minion 且不在顶部的卡片。
- `ExileSelf` 将自身从 combinedDeckZone 移除并播放销毁动画。
- 两个效果在同一个 `effectEvent` 中顺序执行。

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 正常触发 | combinedDeckZone 有友方非 Minion 卡片 | 1 友方被置顶，RIFT 被放逐 | 顺序执行，状态一致 |
| A-2 | 无友方可置顶 | 友方卡片都在顶部或都是 Minion | RIFT 被放逐，stage 数量为 0 | Exile 仍执行 |
| A-3 | 自身在 revealZone | RIFT 在 revealZone | 放逐后 combinedDeckZone 不含 RIFT | ExileSelf 正确移除 |

#### Strategy B

- 验证揭示 RIFT 后，UI 动画是否正确（先 stage 动画，再 exile 动画）。

---

## 4. RIFT_COFFIN

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/Conjure/RIFT_COFFIN.prefab` |
| **Card Type ID** | `RIFT_COFFIN` |
| **Description** | 萦绕: 当友方被去除, 埋葬 1 敌方 |
| **Is Minion** | False |
| **Tags** | Linger |

### Implementation Chain

1. `GameEventListener` 监听 `onFriendlyCardExiled`。
2. `CheckCost_IndexBeforeStartCard` 确认在墓地。
3. `BuryEffect.BuryTheirCards(1)` 埋葬 1 张敌方卡片。

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 墓地触发 | RIFT_COFFIN 在墓地，友方卡片被放逐 | 埋葬 1 敌方卡片 | Linger + 墓地条件满足 |
| A-2 | 不在墓地 | RIFT_COFFIN 在 Start Card 上方 | 无效果 | CheckCost 阻止 |
| A-3 | 敌方无有效目标 | 敌方卡片都在底部或是 Minion | bury 数量为 0 | 优雅处理 |

---

## 5. RIFT_DEVOURER

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/Conjure/RIFT_DEVOURER.prefab` |
| **Card Type ID** | `RIFT_DEVOURER` |
| **Description** | 造成 2 伤害 |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onMeRevealed`。
2. Container "deal dmg": `HPAlterEffect.DecreaseTheirHp` -> `totalDmg = baseDmg(2) + extraDmg + Power`。
3. Container "gain power": `StatusEffectGiverEffect.GiveSelfStatusEffect(1)` -> 自身获得 1 Power。

### Effect Formula

```
伤害: baseDmg(2) + extraDmg(0) + Power = 2 + Power
自身获得 1 Power
```

> 注: prefab 中未显示 extraDmg 值，推断为 0。需确认 Inspector。

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 无 Power | RIFT_DEVOURER 无 Power | 造成 2 伤害 | baseDmg = 2 |
| A-2 | 有 1 Power | RIFT_DEVOURER 有 1 Power | 造成 3 伤害 | Power +1 |
| A-3 | 伤害后自身 Power | 触发前无 Power | 伤害后自身获得 1 Power | GiveSelfStatusEffect 正确执行 |

---

## 6. RIFT_DRAGON

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/Conjure/RIFT_DRAGON.prefab` |
| **Card Type ID** | `RIFT_DRAGON` |
| **Description** | 去除 2 [次元裂缝] |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onMeRevealed`。
2. `preEffectEvent` -> `MinionCostEffect.ExecuteMinionCost`：消耗 2 友方 Minion。
3. `effectEvent` -> `HPAlterEffect.DecreaseTheirHp`：`totalDmg = 2 + 2 + Power = 4 + Power`。

### Effect Formula

```
代价: 2 友方 Minion
伤害: baseDmg(2) + extraDmg(2) + Power = 4 + Power
```

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 代价充足 | 友方有 2+ Minion | 消耗 2 Minion，造成 4+Power 伤害 | MinionCost 成功 |
| A-2 | 代价不足 | 友方只有 1 Minion | 无效果，显示失败信息 | MinionCost 阻止 |
| A-3 | 有 Power 加成 | RIFT_DRAGON 有 1 Power，代价充足 | 造成 5 伤害 | Power 正确叠加 |

---

## 7. RIFT_GUIDE

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/Conjure/RIFT_GUIDE.prefab` |
| **Card Type ID** | `RIFT_GUIDE` |
| **Description** | 去除 2 [次元裂缝] |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onMeRevealed`。
2. Container "exile rift and bury hostile" 包含 `ExileEffect` 和 `BuryEffect`。
3. 日志显示 `effectEvent` -> `BuryEffect.BuryTheirCards`。
4. `ExileEffect` 存在但未在日志中显示 event 绑定，需 Inspector 确认。

### Effect Formula

```
推测效果:
- 放逐 RIFT 自身或敌方 RIFT（需确认 ExileEffect 绑定的方法）
- 埋葬敌方卡片
```

> **注意:** prefab 序列化数据中 `ExileEffect` 的 event 绑定未在批量读取中完整捕获，建议单独 Inspector 确认其 `effectEvent` 绑定的方法名和参数。

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 确认完整效果链 | 友方有 RIFT_GUIDE + 敌方有非 Minion 卡片 | 需确认 Exile 目标 + 埋葬 1+ 敌方 | 完整 event 绑定验证 |
| A-2 | 敌方无有效目标 | 敌方卡片都在底部或是 Minion | Exile 执行（若有），bury 为 0 | 边界条件 |

---

## 8. RIFT_INSECT

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/Conjure/RIFT_INSECT.prefab` |
| **Card Type ID** | `RIFT_INSECT` |
| **Description** | 生成 1 [次元裂缝] |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onMeRevealed`。
2. `AddTempCard.AddCardToMe(RIFT)`：将 1 张 RIFT 添加到友方卡组。

### Effect Formula

```
效果: 友方 combinedDeckZone 底部添加 1 张 RIFT
```

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 正常添加 | combinedDeckZone 有 N 张卡片 | N+1 张，底部为 RIFT | AddCardToMe 正确执行 |
| A-2 | 检查归属 | RIFT_INSECT 属于敌方 | 添加的 RIFT 属于敌方 | 归属跟随 myStatusRef |

---

## 9. RIFT_MONSTER

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/Conjure/RIFT_MONSTER.prefab` |
| **Card Type ID** | `RIFT_MONSTER` |
| **Description** | 去除 1 [次元裂缝] |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onMeRevealed`。
2. `preEffectEvent` -> `MinionCostEffect.ExecuteMinionCost`：消耗 1 友方 Minion。
3. `effectEvent` -> `HPAlterEffect.DecreaseTheirHp`：`totalDmg = 2 + 2 + Power = 4 + Power`。

### Effect Formula

```
代价: 1 友方 Minion
伤害: baseDmg(2) + extraDmg(2) + Power = 4 + Power
```

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 正常触发 | 友方有 1+ Minion | 消耗 1 Minion，造成 4+Power 伤害 | 代价成功，伤害正确 |
| A-2 | 代价不足 | 友方无 Minion | 无效果 | MinionCost 阻止 |

---

## 10. RIFT_SUMMONER

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/Conjure/RIFT_SUMMONER.prefab` |
| **Card Type ID** | `RIFT_SUMMONER` |
| **Description** | 去除 1 [次元裂缝] |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onMeRevealed`。
2. `preEffectEvent` -> `MinionCostEffect.ExecuteMinionCost`：消耗 1 友方 Minion。
3. `effectEvent` -> `StageEffect.StageMyCards(1)`：置顶 1 友方卡片。

### Effect Formula

```
代价: 1 友方 Minion
效果: 置顶 1 友方卡片（随机，排除 Minion 和已在顶部的卡片）
```

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 正常触发 | 友方有 1+ Minion + 1+ 非 Minion 友方 | 消耗 1 Minion，置顶 1 友方 | 代价成功，Stage 执行 |
| A-2 | 代价不足 | 友方无 Minion | 无效果 | MinionCost 阻止 |
| A-3 | 无友方可置顶 | 友方非 Minion 卡片都在顶部 | 消耗 Minion，stage 为 0 | 边界条件 |

---

## 11. SACRIFICE_RITUAL

### Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/Conjure/SACRIFICE_RITUAL.prefab` |
| **Card Type ID** | `SACRIFICE_RITUAL` |
| **Description** | 埋葬 2 友方 |
| **Is Minion** | False |

### Implementation Chain

1. `GameEventListener` 监听 `onMeRevealed`。
2. `effectEvent`:
   - `BuryEffect.BuryMyCards(2)`：埋葬 2 张友方卡片。
   - `AddTempCard.AddCardToMe(RIFT)`：添加 2 张 RIFT 到友方卡组。

### Effect Formula

```
效果1: 埋葬 2 友方卡片（随机，排除 Minion 和已在底部的卡片）
效果2: 友方添加 2 张 RIFT
```

### Test Cases

#### Strategy A

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | 正常触发 | 友方有 2+ 非 Minion 卡片 | 埋葬 2 友方，添加 2 RIFT | 两个效果均执行 |
| A-2 | 友方不足 2 张 | 友方只有 1 张非 Minion 卡片 | 埋葬 1 友方，添加 2 RIFT | Bury 按可用数量执行 |
| A-3 | 友方无有效目标 | 友方卡片都在底部或是 Minion | bury 为 0，仍添加 2 RIFT | AddTempCard 独立执行 |

#### Strategy B

- 验证 SACRIFICE_RITUAL 揭示后，UI 先播放 bury 动画，再添加 RIFT 的动画/显示。

---

## Strategy C: 回归批量测试 (Conjure)

对 Conjure 目录下所有 prefab 批量验证以下字段一致性:

1. **Minion Cost 配置检查**: FALL_INTO_RIFT、RIFT_DRAGON、RIFT_MONSTER、RIFT_SUMMONER 的 `minionCostCount` 是否正确。
2. **Linger 卡片墓地检查**: DEATHBED_CURSE、RIFT_COFFIN 的 `checkCostEvent` 是否绑定 `CheckCost_IndexBeforeStartCard`。
3. **HPAlterEffect 双伤检查**: RIFT_DEVOURER、RIFT_DRAGON、RIFT_MONSTER 的 `baseDmg` + `extraDmg` 是否导致预期伤害值。
4. **AddTempCard 目标检查**: RIFT_INSECT、SACRIFICE_RITUAL 的 `AddCardToMe` 是否正确绑定 RIFT prefab。
