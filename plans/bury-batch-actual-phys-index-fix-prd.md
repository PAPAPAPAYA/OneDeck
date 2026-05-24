# PRD: Bury/Stage Batch 动画目标索引与实际物理位置偏差修复

## 1. 问题背景 (Background)

在 `sacrificial_spirit + soldier_skeleton + start card` 卡组中，`sacrificial_spirit` 揭晓时触发 curse effect 创建 `JU_ON`（pending 状态），随后同 chain 中的 bury effect 将 `soldier_skeleton` 置底。由于 `StageSelf`（reactive effect）在逻辑阶段调用了 `SyncPhysicalCardsWithCombinedDeck()`，污染了 `physicalCardsInDeck` 的顺序，导致 `ApplyAnimationResult` 的 pending skip 逻辑将 `soldier_skeleton` 插入到了 `JU_ON(pending)` 之后（index 1），而非预期的底部（index 0）。

然而 `RecorderAnimationPlayer` 的 `MoveToBottomBatch` 仍按旧公式 `correctedIndex = totalCount - 1 - i` 计算动画目标索引（=0），造成：
- **动画目标位置**（index 0）与 **实际物理位置**（index 1）不一致
- 视觉上表现为"往后一个卡位"的突兀跳跃

## 2. 目标 (Objective)

让 Bury/Stage batch 动画的目标索引与 `ApplyAnimationResult` 后的实际物理 deck 索引保持一致，消除"往后一个卡位"的视觉偏差，同时不破坏 Regression Checklist Row 5（AddTempCard → Bury/Stage 的 pending 卡保护逻辑）。

### 2.1 与现有修复的关系

| Row | 关系 | 说明 |
|-----|------|------|
| Row 5 (2026-05-24) | **依赖** | `ApplyAnimationResult` 的 pending skip 逻辑是本方案的前置条件。本方案在 Row 5 生效后读取实际物理索引，不改变 pending 卡的插入行为。 |
| Row 7 (2026-05-24) | **互补** | Row 7 修复了 `CalculateAnimationPositionAtIndex` 在 pending 卡存在时使用错误 `activeCount` 的问题（index/count 不匹配）。本方案修复的是**传入该函数的 index 值本身错误**（`correctedIndex` 未反映 `ApplyAnimationResult` 后的真实位置）。两者共同解决 `sacrificial_spirit + soldier_skeleton` 场景的动画偏移。 |
| Row 2 (2026-05-15) | **正交** | Bury-then-Stage reactive chain 的最终 deck 位置正确性由 `EffectChainManager` 保证，本方案只影响动画阶段的索引计算，不改变逻辑层行为。 |

## 3. 范围 (Scope)

### 3.1 In Scope
- `RecorderAnimationPlayer.cs` 中以下 batch move 类型的动画目标索引计算：
  - `MoveToBottomBatch`
  - `MoveToTopBatch`
  - `MoveToTopPopUpBatch`

### 3.2 Out of Scope
- `ApplyAnimationResult` 的 pending skip 逻辑（Row 5 的核心保护机制）
- `BuryEffect.cs` / `StageEffect.cs` 的逻辑层代码
- `SlotInCard` / `MoveToPopUpPosition` 等独立动画
- 非 batch 的 `MoveToBottom` / `MoveToTop`（见 4.2.4，它们使用固定边界位置，不受本问题影响）
- `MoveToIndex`（独立动画类型，使用 logic phase 的 `snapshotIndex`，已由调用方保证正确性）

## 4. 技术方案 (Technical Design)

### 4.1 核心策略

**"ApplyAnimationResult 后读取实际物理索引"**

在 `RecorderAnimationPlayer` 中，先调用 `ApplyAnimationResult(request)` 更新 `physicalCardsInDeck`，再通过 `CombatUXManager.GetPhysicalCard()` + `GetPhysicalCardDeckIndex()` 读取每张 target card 的实际索引，以此作为动画目标索引传入 `MoveCardToIndex()` / `MoveCardToTopPopUpBatch()`。

### 4.2 详细改动

#### 4.2.1 MoveToBottomBatch

**当前逻辑：**
```csharp
int correctedIndex = totalCount - 1 - i;
correctedIndex = Mathf.Clamp(correctedIndex, 0, currentCount - 1);
visuals.MoveCardToIndex(card, correctedIndex, ...);
```

**目标逻辑：**
```csharp
int actualIndex = -1;
var combatUX2 = visuals as CombatUXManager;
if (combatUX2 != null)
{
    var phys = combatUX2.GetPhysicalCard(card);
    if (phys != null) actualIndex = combatUX2.GetPhysicalCardDeckIndex(phys);
}

// Fallback: 当无法获取实际索引时（如 physical card 不存在），回退到原有公式
if (actualIndex < 0)
{
    actualIndex = totalCount - 1 - i;
    actualIndex = Mathf.Clamp(actualIndex, 0, currentCount - 1);
}

visuals.MoveCardToIndex(card, actualIndex, ...);
```

#### 4.2.2 MoveToTopBatch

**当前逻辑：**
```csharp
int correctedIndex = currentCount - totalCount + i;
correctedIndex = Mathf.Clamp(correctedIndex, 0, currentCount - 1);
visuals.MoveCardToIndex(card, correctedIndex, ...);
```

**目标逻辑：** 同上，用 `actualIndex` 替换 `correctedIndex`，fallback 为 `currentCount - totalCount + i`。

#### 4.2.3 MoveToTopPopUpBatch

**当前逻辑：**
```csharp
var finalIndices = new List<int>();
for (int i = 0; i < totalCount; i++)
{
    int correctedIndex = currentCount - totalCount + i;
    correctedIndex = Mathf.Clamp(correctedIndex, 0, currentCount - 1);
    finalIndices.Add(correctedIndex);
}
visuals.MoveCardToTopPopUpBatch(request.targetCards, finalIndices, ...);
```

**目标逻辑：** 同上，用 `actualIndex` 构建 `finalIndices`，fallback 为 `currentCount - totalCount + i`。

#### 4.2.4 非 batch 移动（MoveToBottom / MoveToTop）—— 无需修改

非 batch 的 `MoveToBottom` 和 `MoveToTop` 在 `CombatUXManager.MoveCardWithAnimation` 中分别调用：
- `CalculateAnimationPositionAtIndex(0)` — 固定底部
- `CalculateAnimationPositionAtIndex(physicalCardsInDeck.Count - 1)` — 固定顶部

它们使用**固定边界位置**，不依赖具体卡片在 deck 中的动态索引，因此不受 reactive effect 污染 `physicalCardsInDeck` 的影响，无需纳入本次修复范围。

### 4.3 时序图 —— MoveToBottomBatch

```
逻辑阶段 (Logic Phase):
  BuryEffect: 修改 combinedDeck -> [soldier_skeleton, JU_ON, start_card]
              SyncPhysicalCardsWithCombinedDeck() 
              snapshot targetIndices=[0] for soldier_skeleton
              Raise onMeBuried -> StageSelf
  StageSelf:  修改 combinedDeck -> [JU_ON, start_card, soldier_skeleton]
              SyncPhysicalCardsWithCombinedDeck()  // 污染了 physical deck

动画阶段 (Animation Phase):
  RecorderAnimationPlayer.PlayRequestCoroutine(MoveToBottomBatch)
    1. visuals.ApplyAnimationResult(request)
       -> physicalCardsInDeck = [JU_ON(pending), soldier_skeleton, start_card]
          // pending skip 把 soldier_skeleton 推到了 index 1
    2. visuals.UpdateAllPhysicalCardTargets()
    3. [FIX] 读取 actualPhysIndex = 1
    4. visuals.MoveCardToIndex(soldier_skeleton, actualIndex=1, ...)
       -> 动画飞往 index 1 的位置，与实际物理位置一致
```

### 4.4 MoveToTopBatch 触发场景示例

同样的根因也影响 `MoveToTopBatch`。示例：
- `RisingFlame` 触发 `StageSelf`，逻辑阶段 capture `MoveToTopBatch target=[RisingFlame]`
- 同一 chain 中的后续 effect（如 `AddTempCard`）创建了 `RIFT_INSECT(pending)` 并 `SyncPhysicalCardsWithCombinedDeck()`
- 动画阶段：`ApplyAnimationResult(MoveToTopBatch)` 移除 `RisingFlame` 后，从尾部扫描跳过 pending 卡，将其插入到 `appendIndex = 1` 而非 `currentCount - 1 = 2`
- 旧公式 `correctedIndex = currentCount - totalCount + i` 得到 2，与实际物理索引 1 不一致
- 修复后读取 `actualPhysIndex = 1`，动画目标位置正确

### 4.5 Fallback 偏差说明

当 `GetPhysicalCard(card)` 返回 null 时（如 headless 测试或物理对象尚未创建），fallback 回退到旧公式 `totalCount - 1 - i` / `currentCount - totalCount + i`。

- **headless 模式**：视觉偏差无关紧要，fallback 是可接受的降级。
- **生产环境**：若触发 fallback，卡片仍会飞往旧公式的目标位置，存在已知偏差。此情况在正常运行中极少出现，因为 batch move 的目标卡片必然已在物理 deck 中存在。

## 5. 回归检查 (Regression Checklist)

修复完成后需验证以下场景：

| # | 场景 | 验证方法 | 预期结果 |
|---|------|---------|---------|
| 1 | sacrificial_spirit → soldier_skeleton 置底 | Play Mode 复现用户场景 | soldier_skeleton 平滑飞往 index 1，无"往后卡位"跳跃 |
| 2 | RIFT_INSECT (AddTempCard) → Bury | Play Mode 测试 Row 5 | pending 的 RIFT_INSECT 保持在原位，bury 的卡落在其后 |
| 3 | StoneShell → RisingFlame (Bury-then-Stage) | Play Mode 测试 Row 2 | 最终 deck 位置正确，动画无 distance-zero |
| 4 | 普通 Bury（无 pending 卡） | Play Mode 任意 Bury 卡 | actualIndex == correctedIndex，行为与修复前一致 |
| 5 | 普通 Stage（无 pending 卡） | Play Mode 任意 Stage 卡 | actualIndex == correctedIndex，行为与修复前一致 |

### 5.1 RegressionChecklist 更新策略

本 PR 是对 Row 7（2026-05-24）场景的**残余问题修正**，按 `AGENTS.md` 要求需更新 `docs/RegressionChecklist.md`：

- **不新增行**：本修复与 Row 7 属于同一根因（`sacrificial_spirit + soldier_skeleton` 动画偏移），Row 7 的验证场景已覆盖本修复。
- **更新 Row 7 的 Verification 列**：在现有验证说明末尾追加：
  > "同时验证 `RecorderAnimationPlayer` 使用 `actualPhysIndex` 而非 `correctedIndex` 作为动画目标索引（日志中 `actualPhysIndex == targetIndex`）。"
- **更新 Row 7 的 System 列**：追加 `RecorderAnimationPlayer`。

## 6. 文件变更清单 (File Changes)

| 文件 | 变更类型 | 说明 |
|------|---------|------|
| `Assets/Scripts/Managers/RecorderAnimationPlayer.cs` | 修改 | `MoveToBottomBatch`、`MoveToTopBatch`、`MoveToTopPopUpBatch` 中使用 actualPhysIndex |

## 7. 附录 (Appendix)

### 7.1 相关 VISUAL-FIX 注释

本次修改涉及以下已有的 VISUAL-FIX 注释，修改时需保持注释不动：

- `ApplyAnimationResult` 中的 `VISUAL-FIX(2026-05-24): ApplyAnimationResult inserts moved cards before pending slot-in cards`
- `RecorderAnimationPlayer` 中的 `VISUAL-FIX(2026-05-18): Deck-move animations play in wrong peeled/focused layout`

### 7.2 关键日志字段说明

新增/已有的日志字段用于调试验证：

| 字段 | 出处 | 含义 |
|------|------|------|
| `snapshotIndex` | `RecorderAnimationPlayer` | Bury/Stage 在 logic phase 计算的逻辑索引。修复后**仅用于日志对比**，不再作为动画目标索引 |
| `correctedIndex` | `RecorderAnimationPlayer` | 旧公式计算的动画目标索引。修复后**仅用于 fallback**，正常流程不再使用 |
| `actualPhysIndex` | `RecorderAnimationPlayer` / `CombatUXManager` | ApplyAnimationResult 后的实际物理 deck 索引。修复后作为**唯一真实的动画目标索引** |
| `targetIndex` | `CombatUXManager.MoveCardWithAnimation` | MoveCardToIndex 传入的动画目标索引 |

健康状态：
- **无 pending 卡、无 reactive effect 干扰时**：`snapshotIndex == actualPhysIndex == targetIndex`
- **有 pending 卡或 reactive effect 干扰时**：`snapshotIndex` 可能过时，`actualPhysIndex` 必须与 `targetIndex` 一致
