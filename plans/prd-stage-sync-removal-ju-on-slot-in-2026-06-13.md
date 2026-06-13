# 方案A：移除 StageEffect 逻辑阶段物理牌组同步以修复 JU_ON consume+stage 错位问题

## 1. 问题概述

在 `JU_ON` 被 `PREMATURE` 的 `ConsumeHostileCursePower` 效果消耗诅咒力量并随后被 `Stage` 置顶时，JU_ON 的 `SlotInBatch` 动画会插回到错误的牌组位置（视觉上直接落回置顶位，而不是先回到原位置再由 `MoveToTopPopUpBatch` 动画置顶）。

## 2. 根本原因

`StageEffect.StageChosenCards` 在**逻辑阶段**调用了 `combatManager.visuals.SyncPhysicalCardsWithCombinedDeck()`，在动画播放前就把 `physicalCardsInDeck` 重排为Stage后的顺序。

在该战斗流程中，`PREMATURE` 的效果记录器按顺序捕获了：

1. `ConsumeHostileCursePower`（对 JU_ON）
   - `StatusEffectChange`
   - `PopUpBatch`
   - `StatusEffectProjectile`
   - `SlotInBatch`
2. `StageEffect`（对 JU_ON）
   - `MoveToTopPopUpBatch`

`StageEffect` 在 `SlotInBatch` 的请求已被捕获之后、但动画尚未播放之前，提前同步了物理牌组。导致动画播放时：

- `PopUpBatch` 从 JU_ON 已被置顶的位置弹出。
- `SlotInBatch` 通过 `physicalCardsInDeck.IndexOf(physicalCard)` 查询到的 `deckIndex` 已经是置顶位。
- JU_ON 弹起后直接落回置顶位，形成距离为0的无效动画，视觉效果上就是“插回了错误位置”。

## 3. 方案A：移除 StageEffect 逻辑阶段同步

### 3.1 核心改动

删除 `StageEffect.StageChosenCards` 中逻辑阶段的 `SyncPhysicalCardsWithCombinedDeck()` 调用。

保留逻辑层对 `combinedDeckZone` 的修改（将目标卡移动到列表末尾，即置顶），但**不**在逻辑阶段更新 `physicalCardsInDeck`。

### 3.2 动画阶段行为

`RecorderAnimationPlayer` 在处理 `MoveToTopPopUpBatch` 时，会在协程开头调用：

```csharp
visuals.ApplyAnimationResult(request);
visuals.UpdateAllPhysicalCardTargets();
```

`ApplyAnimationResult` 会正确将目标卡的物理对象移动到 `physicalCardsInDeck` 末尾（置顶位）。因此物理牌组的重排序延迟到动画播放阶段完成。

### 3.3 正确动画序列

移除同步后，动画播放顺序为：

1. `PopUpBatch` — JU_ON 从**原牌组位置**弹起。
2. `StatusEffectProjectile` — 投射物从原位置飞向 `statusEffectConsumePos`。
3. `SlotInBatch` — JU_ON 插回**原牌组索引**。
4. `MoveToTopPopUpBatch` — `ApplyAnimationResult` 先将 JU_ON 移到 `physicalCardsInDeck` 顶部，再播放置顶动画。

## 4. 影响范围

### 4.1 需要修改的文件

- `Assets/Scripts/Effects/StageEffect.cs`
  - 删除或注释掉 `StageChosenCards` 中逻辑阶段的 `SyncPhysicalCardsWithCombinedDeck()` 调用。
  - 保留 `stagedTargetIndices` 快照逻辑（该快照基于逻辑层 `combinedDeck`，与物理同步无关）。

### 4.2 需要同步评估的文件

- `Assets/Scripts/Effects/BuryEffect.cs`
  - 与 `StageEffect` 对称，`BuryChosenCards` 同样在逻辑阶段调用了 `SyncPhysicalCardsWithCombinedDeck()`。
  - 建议一并移除，保持 bury/stage 行为一致。

- `Assets/Scripts/Editor/Tests/VisualsCallTests.cs`
  - `BuryEffect_CallsSyncDeckOnce` 测试断言 `BuryEffect` 调用一次 `SyncPhysicalCardsWithCombinedDeck`。
  - 若移除 `BuryEffect` 的同步，需要更新或删除该测试。

### 4.3 必须同步更新的文档与测试

本方案会触及现有回归清单、单元测试和 `AGENTS.md` 中的记录，不能只改代码。

| 文档 / 测试 | 当前记录 | 需要做的更新 |
|-------------|----------|--------------|
| `docs/RegressionChecklist.md` 第 1 行 | “Bury/Stage animation has no visible movement (distance-zero)” 2026-05-18 ✅ | 标记为 `~~strikethrough~~ (Obsolete 2026-06-13)`，并新增 JU_ON / PREMATURE consume+stage 的回归条目 |
| `docs/RegressionChecklist.md` 第 10 行 | afterShuffle Stage zero-distance tween 2026-06-08 ✅ | 实施本方案后必须重新验证 BOOSTER afterShuffle→Stage 场景 |
| `Assets/Scripts/Editor/Tests/VisualsCallTests.cs` | `BuryEffect_CallsSyncDeckOnce` 断言调用一次 sync | 删除或改为断言“不调用 sync” |
| `AGENTS.md` | “Fallback: ... `BuryEffect`/`StageEffect` still call `SyncPhysicalCardsWithCombinedDeck`.” | 删除或改为说明在 recorder 模式下由 `ApplyAnimationResult` 处理；fallback 路径另行处理 |

## 5. 风险与回归验证

| 风险点 | 说明 | 验证方式 |
|--------|------|----------|
| 与 Regression Checklist 第 1 行冲突 | 2026-05-24 的 `cdc7f8c` 恢复同步是为了修复 distance-zero；但同期 `CardPhysObjScript.UpdateTargetPositionOnly` 和 `CombatUXManager.AddPhysicalCardToDeck` 的修改才是核心保护。 | 按 4.3 更新清单后，播放 StoneShell / RisingFlame 确认牌组卡片有明显位移动画。 |
| 与 `AGENTS.md` fallback 描述冲突 | `AGENTS.md` 写明 `RecorderAnimationPlayer.me == null` 时 Bury/Stage 仍调用 sync。 | 同步修改 `AGENTS.md`，并确认 headless 测试不需要该 fallback 调用。 |
| `VisualsCallTests.BuryEffect_CallsSyncDeckOnce` 失败 | 该测试直接断言 sync 调用次数。 | 删除或重写该测试。 |
| 反应链 `onMeStaged → BurySelf` | `ApplyAnimationResult` 与快照索引共同保证动画正确性。 | 测试 RisingFlame 或类似具有 `onMeStaged → BurySelf` 反应的卡牌。 |
| 反应链 `onMeBuried → StageSelf` | 同上。 | 测试 StoneShell + RisingFlame 组合。 |
| 待定卡（pending slot-in）位置 | `ApplyAnimationResult` 中已跳过 `isPendingSlotIn` 的卡片。 | 测试 RIFT_INSECT / BLACKSMITH + Bury/Stage 组合。 |
| afterShuffle Stage 零距离回归 | 06-08 PRD 明确说不改 StageEffect sync；本方案改了。 | 重新测试 Start Card shuffle → BOOSTER（afterShuffle→Stage），确认 arc 动画可见。 |
| 其他逻辑阶段同步调用点 | `CardManipulationEffect`、`ExileEffect` 也在逻辑阶段调用同步，需评估是否类似问题。 | 审查这些效果的 consume/popup/slot-in 组合。 |

## 6. 推荐实施顺序

1. 仅在 `StageEffect.StageChosenCards` 中移除同步。
2. 运行 EditMode 测试，确认 `ReactiveChainTests` 通过；更新或删除 `VisualsCallTests.BuryEffect_CallsSyncDeckOnce`。
3. 在 PlayMode 中复现 JU_ON + PREMATURE 场景，确认弹起/投射/插回/置顶动画正确。
4. 若验证通过，再对称移除 `BuryEffect` 的同步，并再次运行 `ReactiveChainTests` / `VisualsCallTests`。
5. **必须同步完成**：
   - 更新 `docs/RegressionChecklist.md`（第 1 行标记 obsolete，新增 JU_ON 回归条目，第 10 行重新验证）。
   - 更新 `AGENTS.md` 中关于 Bury/Stage fallback sync 的描述。
   - 更新 `VisualsCallTests.cs`。
6. 全量回归：StoneShell / RisingFlame / BOOSTER / RIFT_INSECT / BLACKSMITH。

## 7. 与现有回归清单/文档的冲突说明

本方案**不是**无冲突的纯增量修复。它修改了 `RegressionChecklist.md` 第 1 行所记录的修复代码（`cdc7f8c` 恢复的无条件 sync），因此：

- 第 1 行需要标记为 obsolete，而不是直接删除（清单规则）。
- 第 10 行（afterShuffle Stage zero-distance）必须重新跑一遍，因为 06-08 PRD 明确声明“不改 StageEffect sync”，而本方案改变了这一前提。
- `AGENTS.md` 的 fallback 描述失效，必须同步修改。
- `VisualsCallTests.BuryEffect_CallsSyncDeckOnce` 必须删除或改写。

如果不做这些文档/测试更新，代码虽然能跑，但会留下过时的回归记录和失败的单元测试。

## 8. 不修复的替代说明

本方案不采用“让 `SlotInBatch` 也携带捕获索引”的改法，原因：

- `SlotInBatch` 当前设计依赖物理牌组的实时索引，改动会扩大影响面。
- 根本问题是物理牌组在动画播放前被提前重排；修复同步时机比修改 `SlotInBatch` 语义更直接、风险更小。
