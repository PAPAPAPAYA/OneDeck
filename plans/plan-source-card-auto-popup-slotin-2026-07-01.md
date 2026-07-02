# Source-Card 自动 PopUp / SlotIn 重构方案

## 1. 目标

把“off-reveal source card 先 popup、再播动画、最后 slotin”的逻辑从各 Effect 的 `AnimationRequest` 捕获层抽出来，统一放到 `RecorderAnimationPlayer` 中处理，让规则变成：

> **每张 off-reveal 的 source card，在本 recorder 的动画开始前自动 popup，在本 recorder 自己的动画结束后自动 slotin。**

## 2. 当前问题

- 逻辑分散：`BuryEffect`、`StageEffect`、`StatusEffectGiverEffect` 等各自捕获 `PopUpBatch`、`SlotInBatch`、`MoveToTopPopUpBatch`。
- 职责不清：`RecorderAnimationPlayer` 里已经有一部分 source-card popup/slotin 的补丁逻辑（`PlayOffRevealPopupCoroutine`、`SlotInSourceCardCoroutine`、`_heldSourceCards`），但 slotin 时机发生在**整棵子树播放完后**，并且要和内置请求做重复过滤，理解成本高。
- 特殊路径多：off-reveal attack 走 peel deck focus；`MoveToTopPopUpBatch` 自己内部做了 popup+slotin；cost-fail 走 shake。

## 3. 已确认的需求边界

根据沟通结果，行为边界如下：

| 问题 | 结论 |
|------|------|
| 哪些卡片需要自动 popup/slotin？ | 仅 **source card**（`recorder.cardObject`）。被 bury/stage 的 target cards 走它们自己的内置请求。 |
| popup  timing | 在 emphasize / shake / `StatusEffectChange` 等请求**之前**；**不包含** Attack（attack 仍走 peel deck focus）。 |
| slotin timing | 仅当前 **recorder 自己的 `animationRequests` 播完**后就 slotin；不等待子 recorder。 |
| destroy / exile | 卡片已不存在于 deck 或已进 reveal zone，则**不 slotin**。 |
| 内置 PopUp/SlotIn 请求 | **保留**，`RecorderAnimationPlayer` 自动跳过对 source card 的重复 popup/slotin。 |
| peel deck focus | 对 off-reveal Attack **保留**，不 popup。 |
| 示例 | off-reveal 卡 A 触发 bury B、C：A popup → emphasize → B/C 的 `PopUpBatch`+`MoveToBottomBatch` → A slotin。 |

## 4. 核心设计

### 4.1 单 Recorder 播放流程（`PlayRecorderCoroutine`）

```
1. 判断 source card 是否 off-reveal
   ├─ 是 Attack recorder ──► peel deck focus（保持现有逻辑）
   └─ 非 Attack recorder ──► 若未被 ancestor popup，则自动 popup source card

2. 播放 source card 反馈
   ├─ cost-fail recorder ──► 播放 shake（animationRequests[0]）
   └─ 普通 recorder ──► 播放 emphasize

3. 顺序播放本 recorder 的 animationRequests
   ├─ 跳过对 source card 的重复 PopUp / PopUpBatch
   ├─ 跳过对 source card 的重复 SlotIn / SlotInBatch
   ├─ 对包含 source card 的 MoveToTopPopUpBatch 做拆分/降级
   └─ 其余请求正常播放

4. 本 recorder 自己的请求全部播完后
   └─ 若 source card 仍存在、仍在 deck、仍在 popup 状态 ──► 自动 slotin

5. 递归播放子 recorder（与 source card slotin 解耦）
```

### 4.2 重复请求判定规则

新增辅助方法 `ShouldSkipRequestForSourceCard(request, sourceCard)`：

| 请求类型 | source card 的处理 |
|----------|-------------------|
| `PopUp` | 若 `targetCard == sourceCard` 且已 popup，跳过。 |
| `PopUpBatch` | 若列表中某卡是 sourceCard 且已 popup，从列表移除；移除后列表为空则整个跳过。 |
| `SlotIn` | 若 `targetCard == sourceCard`，跳过（由自动 slotin 负责）。 |
| `SlotInBatch` | 若列表中某卡是 sourceCard，从列表移除；移除后列表为空则整个跳过。 |
| `MoveToTopPopUpBatch` | 不拆分、保留原请求。source card 已由自动 popup 顶起，batch 会把它移动到 top peak 再 slotin。 |

### 4.3 `MoveToTopPopUpBatch` 与 source card 的关系

`MoveCardToTopPopUpBatch` 把“arc 到 popup peak + slotin”耦合在一起。source card 已经由自动 popup 顶起，因此不会重复从 deck 位置 arc 到 peak；它会从当前 peak 位置移动到 top 对应的 peak 位置，再 slotin 到 deck top。

**方案**：不拆分，保留原 `MoveToTopPopUpBatch` 请求。

- 自动 popup 先把 source card 顶起。
- `MoveToTopPopUpBatch` 正常播放；Phase 1 把卡片从当前 peak 移到 top peak，Phase 2 slotin 到 deck top。
- 由于 batch 已经把 source card slotin，`isPoppedUp` 会被置为 false，最后的自动 slotin 自然跳过。

> 注意：这要求 `MoveCardToTopPopUpBatch` 在 source card 已 popup 时不会把它压回 deck。当前实现会先 `KillTweens()` 并设置 `isPlayingSpecialAnimation = true`，从当前位置 arc 到 peak，因此行为正确。

### 4.4 状态持有

复用并精简现有的 `_heldSourceCards`：

```csharp
// key:   source card GameObject
// value: 负责自动 slotin 的 EffectRecorder
private Dictionary<GameObject, EffectRecorder> _heldSourceCards;
```

- 某个 recorder 自动 popup 了 source card，就把它加入 `_heldSourceCards`。
- 该 recorder 自己的请求播完后，由它负责 slotin，然后从字典移除。
- 若 ancestor 已经 popup（字典已包含），当前 recorder 不 popup、也不负责 slotin。

### 4.5 slotin 条件

自动 slotin 前检查：

```csharp
bool shouldSlotIn =
    thisRecorderHoldsSourceCard &&              // 本 recorder 负责 popup
    sourceCard != null &&                       // 未被销毁
    !IsInRevealZone(sourceCard) &&              // 未进入 reveal zone
    sourcePhys != null &&
    sourcePhys.isPoppedUp;                      // 仍处于 popup 状态
```

## 5. 需要修改的文件

| 文件 | 改动 |
|------|------|
| `Assets/Scripts/Managers/RecorderAnimationPlayer.cs` | 核心重构：调整 `PlayRecorderCoroutine` 顺序、新增重复请求判定与 `MoveToTopPopUpBatch` 拆分、slotin 提前到本 recorder 请求后。 |
| `Assets/Scripts/UXPrototype/CombatUXManager.cs` | 可选：新增/暴露 `MoveCardToTopBatch`（若尚不可用）。 |
| `Assets/Scripts/Managers/ICombatVisuals.cs` | 可选：接口中暴露 `MoveCardToTopBatch` 或确认已有 `MoveCardToTop` 可直接复用。 |
| `Assets/Scripts/UXPrototype/CardPhysObjScript.cs` | 确认 `isPoppedUp` 状态在 `MoveCardToTopBatch` 后仍保持 true，直到 slotin。 |
| `docs/RegressionChecklist.md` | 按项目规范新增回归检查行。 |

## 6. 关键时序示例

### 6.1 普通 off-reveal source card（Bury）

```
A (source, off-reveal)
├─ PopUp A            ← 自动
├─ Emphasize A        ← 自动
├─ PopUpBatch(B,C)    ← 内置请求，B/C 未 popup，正常播放
├─ MoveToBottomBatch(B,C) ← 内置请求
└─ SlotIn A           ← 自动
```

### 6.2 StageSelf（source 同时是 target）

```
A (source, off-reveal)
├─ PopUp A                 ← 自动
├─ Emphasize A             ← 自动
├─ MoveToTopPopUpBatch(A)  ← 保留原请求：从当前 peak 移到 top peak，再 slotin
└─ SlotIn A                ← 自动，因 batch 已 slotin 而跳过
```

### 6.3 off-reveal Attack（保持现有）

```
A (source, off-reveal, Attack)
├─ Peel Deck Focus    ← 保持
├─ Emphasize A        ← 保持
├─ Attack A           ← 内置请求
└─ （无自动 slotin，因为走 peel focus 路径）
```

### 6.4 Cost-Fail

```
A (source, off-reveal, cost-fail)
├─ PopUp A            ← 自动
├─ Shake A            ← 自动（animationRequests[0]）
└─ SlotIn A           ← 自动
```

## 7. 边界情况与回归检查

| 场景 | 期望行为 |
|------|----------|
| Source card 被 `ExileEffect` destroy | 自动 slotin 前检查到卡片已不存在，跳过。 |
| Source card 被移到 reveal zone | `IsInRevealZone` 为 true，跳过 slotin。 |
| 子 recorder 也 targeting source card | source card 已 slotin，子 recorder 若 off-reveal 会再次 popup → 播放 → slotin。可能产生“弹起-落回-再弹起”的抖动，这是按“不等待子 recorder”规则的正常结果。 |
| Ancestor 已 popup source card | 当前 recorder 不重复 popup，也不负责 slotin。 |
| 内置 `SlotInBatch` 包含 source card | 从列表中移除 source card，其余 target 正常 slotin。 |

## 8. 风险与权衡

1. **子 recorder 抖动**：source card 在本 recorder 结束后 slotin，若子 recorder 立刻又让它 popup，视觉上会“落回再弹起”。若希望避免，需要把 slotin 推迟到子树完成（回到旧逻辑）或让子 recorder 感知“source card 刚被 popup 过”。当前按需求保持“不等待子 recorder”。
2. **`MoveToTopPopUpBatch` 与已 popup source card**：需要确认 batch 在 source card 已 popup 时，Phase 1 不会把它压回 deck，而是从当前 peak 位置移动到 top peak。建议在 `CombatUXManager.MoveCardToTopPopUpBatch` 中验证对 `isPoppedUp` 卡片的处理。
3. **`AnimationRequest` 字段不可变**：目前 request 的 `targetCards` 是 `List<GameObject>`，拆分/过滤时会修改 request 对象。因为 recorder 一般只播放一次，这是安全的；若以后复播需改为复制列表。

## 9. 实施顺序建议

1. 在 `RecorderAnimationPlayer` 中把 slotin 从“子树后”移到“本 recorder 请求后”。
2. 新增 `ShouldSkipRequestForSourceCard` 和 `SplitMoveToTopPopUpBatchForSource`。
3. 更新 emphasize/shake 与自动 popup 的顺序：popup → emphasize/shake → requests → slotin。
4. 保留 peel deck focus 路径不变。
5. 跑回归：off-reveal Bury、StageSelf、Attack、Cost-Fail、Exile source card。
