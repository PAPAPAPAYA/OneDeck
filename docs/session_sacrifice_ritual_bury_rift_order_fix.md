# Session 总结：SACRIFICE_RITUAL bury/rift 视觉顺序修复

## 1. 要解决的问题

**卡片**：`SACRIFICE_RITUAL`
**表现**：先置底（bury）1 张友方卡，再生成 2 张 rift。视觉上两张 rift 出现在被置底的友方卡前面（更靠近玩家）。预期是被置底的友方卡在前，rift 在后。

## 2. 根本原因

### 2.1 Chain 分裂
`SACRIFICE_RITUAL` 有两个 `CostNEffectContainer`：
- `bury friendly`（`BuryEffect.BuryMyCards`）
- `add [rift]`（`AddTempCard.AddCardToMe`）

由于它们属于**同一卡片的不同 effect 对象**，`EffectChainManager.CheckShouldIStartANewChain` 判定 `sameCardDiffObj = true`，关闭第一个 chain、开启第二个 chain。导致：
- **逻辑阶段**：`bury` 先执行，`add [rift]` 后执行（同步顺序）
- **动画阶段**：`bury` 的 chain 先播放，`add [rift]` 的 chain 后播放

### 2.2 `ApplyAnimationResult` 的硬编码 reorder 推开了后续 chain 添加的新卡

`add [rift]` 逻辑阶段执行时，`AddPhysicalCardToDeck` 把两张 rift 插入到 `physicalCardsInDeck[0]`：

```
// add 逻辑阶段结束后
combinedDeckZone          = [rift2, rift1, buried, A, B]
physicalCardsInDeck       = [rift2, rift1, buried, A, B]
```

但 `bury` 动画播放前，`ApplyAnimationResult(MoveToBottomBatch)` 仍然用**硬编码的 `Insert(0)`** 把 bury 目标插回 index 0：

```csharp
physicalCardsInDeck.Remove(buried_phys);
physicalCardsInDeck.Insert(0, buried_phys);
```

结果：
```
physicalCardsInDeck       = [buried, rift2, rift1, A, B]  ← 与逻辑顺序不一致
```

### 2.3 `SlotIn` 读取了被污染的物理列表

`SlotIn` 使用 `physicalCardsInDeck.IndexOf(physicalCard)` 计算目标位置：
- rift2 读到 index **1**（应为逻辑 index **0**）
- rift1 读到 index **2**（应为逻辑 index **1**）

导致 rift 飞到了错误的物理位置，视觉上 rift 在 bury 前面。

## 3. 已执行的修复

### 修改文件
`Assets/Scripts/UXPrototype/CombatUXManager.cs` — `ApplyAnimationResult()` 方法

### 修改内容
对以下 5 个 case 的 reorder 逻辑做了调整：

| Case | 修改前 | 修改后 |
|------|--------|--------|
| `MoveToBottomBatch` | `Insert(0, phys)` | 从前往后遍历，跳过 `isPlayingSpecialAnimation == true` 的卡，在第一个"已就绪"卡之前插入 |
| `MoveToTopBatch` | `Add(phys)` | 从后往前遍历，跳过 `isPlayingSpecialAnimation == true` 的卡，在最后一个"已就绪"卡之后插入 |
| `MoveToTopPopUpBatch` | `Add(phys)` | 同上 |
| `MoveToBottom` | `Insert(0, phys)` | 同 batch 逻辑 |
| `MoveToTop` | `Add(phys)` | 同 batch 逻辑 |

### 修复原理
`AddPhysicalCardToDeck` 在 effect chain 内创建新卡时会设置 `isPlayingSpecialAnimation = true`。`ApplyAnimationResult` 现在识别出这些还在等待自己 `SlotIn` 动画的 pending 卡，**不会把它们推开**。

对于 `SACRIFICE_RITUAL`：
- 修改前：`[buried, rift2, rift1, A, B]`
- 修改后：`[rift2, rift1, buried, A, B]` ← 与逻辑顺序一致

## 4. 与 commit `3d0da96` 的兼容性

### `3d0da96` 做了什么
commit `3d0da96`（`when card position changed by multiple events, visual bug fixed`）把 `ApplyAnimationResult(request)` 和 `UpdateAllPhysicalCardTargets()` 从 switch 末尾移到了**每个 deck-move case 的内部、动画播放之前**。

目的是：在同一个 chain 内，当一个效果移动了卡、reactive effect 又移动了同一张卡时，让 `ApplyAnimationResult` 在每个动画开始前就更新 `physicalCardsInDeck`，使其他卡提前 tween 到新位置，避免卡片穿越。

### 本次修改是否影响 `3d0da96`
**不影响。**

本次修改只改变了 `ApplyAnimationResult` 内部 reorder 的**插入策略**（从固定的 `index 0` / `末尾` 改为跳过 pending 卡的动态位置），**完全没有改动** `ApplyAnimationResult` 的**调用时机**和 `UpdateAllPhysicalCardTargets` 的调用顺序。

在同一个 chain 内 bury → stage reactive 的场景中：
- 没有 pending 新卡（`isPlayingSpecialAnimation == false`）
- 插入行为与修改前完全一致
- `3d0da96` 的"动画前更新物理列表"时序完全保留

## 5. 遗留问题（未修复）

### `CalculatePositionAtIndex` 使用了包含 pending 卡的 `deckCount`

`AddPhysicalCardToDeck` 把新卡插入到 `physicalCardsInDeck` 后，`physicalCardsInDeck.Count` 变大了。但 `CalculatePositionAtIndex` 使用这个 count 来计算所有卡的坐标：

```csharp
public Vector3 CalculatePositionAtIndex(int index)
{
    var count = physicalCardsInDeck.Count;  // ← 包含 pending rift
    // ...
    DeckPositionCalculator.CalculatePositionAtIndex(index, count, ...)
}
```

`DeckPositionCalculator`：
```csharp
basePos.x + xOffset * (deckCount - 1 - index)
basePos.y + yOffset * (deckCount - 1 - index)
basePos.z - zOffset * index
```

当 `deckCount` 被 pending 卡撑大后，所有卡片的 x/y 偏移和 z 偏移都会受到影响。

**具体表现**：
- bury 的 x/y 目标位置偏移变大（飞得更远）
- 所有卡的 z 位置被重新计算，视觉堆叠深度变化

**修复方向**：`CalculatePositionAtIndex` 应使用"活跃 deck size"（`physicalCardsInDeck` 中 `isPlayingSpecialAnimation == false` 的卡数量）来计算坐标，而不是 `physicalCardsInDeck.Count`。

**当前状态**：用户决定暂不修复此问题。
