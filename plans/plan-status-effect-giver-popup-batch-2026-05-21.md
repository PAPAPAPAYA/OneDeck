# StatusEffectGiverEffect PopUp Batch 方案

## 1. 背景与现状

Pop Up + Slot In 动画基础设施（`AnimationRequestType`、`ICombatVisuals`、`CombatUXManager`、`RecorderAnimationPlayer`）已完备，且 `CurseEffect`（单目标 StatusEffectProjectile）已集成 PopUp → Projectile → SlotIn 的完整序列。

但 `StatusEffectGiverEffect` 的四个主要方法——`GiveStatusEffect`、`GiveAllFriendlyStatusEffect`、`GiveStatusEffectToLastXCards`、`GiveStatusEffectToXFriendly`——在给予 status effect 时，仅 capture 了一个 **batch 多目标**的 `StatusEffectProjectile`（`targetCards` 列表），目标卡没有 Pop Up，玩家难以在叠牌中辨认哪些卡被上了效果。

由于 `StatusEffectGiverEffect` 是 **多目标 batch** 路径，直接套用 PRD 中单目标的顺序 PopUp/SlotIn 会导致动画时间随目标数线性增长（N 目标 ≈ N×0.6s），体验差。因此需要引入 **Batch 并行**机制。

---

## 2. 目标

让 `StatusEffectGiverEffect` 的所有 batch status effect 给予路径，在播放 `StatusEffectProjectile` 前**并行**让所有目标卡 Pop Up，Projectile 结束后**并行**让所有目标卡 Slot In，且总额外动画时间控制在 ~0.6s 以内，与目标数量无关。

---

## 3. 方案概述

新增两个动画请求类型：

- `PopUpBatch` — 并行触发多个目标卡的 `PopUpCard`
- `SlotInBatch` — 并行触发多个目标卡的 `SlotInCard`

`StatusEffectGiverEffect` 的 capture 逻辑从单一 `StatusEffectProjectile` 改为三段式：

```
[PopUpBatch]      // 所有目标同时升起  (~0.25s)
    |
[StatusEffectProjectile]  // batch projectile  (~0.4s + stagger)
    |
[SlotInBatch]     // 所有目标同时落回  (~0.35s)
```

---

## 4. 详细设计

### 4.1 AnimationRequestType 扩展

在 `Assets/Scripts/Managers/AnimationRequest.cs` 的 enum 中追加：

```csharp
public enum AnimationRequestType
{
	// ... existing types ...
	PopUpBatch,
	SlotInBatch
}
```

`AnimationRequest` 本身无需新字段，`targetCards` 已足够承载 batch 目标列表。

### 4.2 RecorderAnimationPlayer 扩展

在 `Assets/Scripts/Managers/RecorderAnimationPlayer.cs` 的 `PlayRequestCoroutine` switch 中新增两个 case。

**PopUpBatch：**

```csharp
case AnimationRequestType.PopUpBatch:
{
	int completedCount = 0;
	int totalCount = request.targetCards != null ? request.targetCards.Count : 0;
	if (totalCount == 0) break;
	foreach (var card in request.targetCards)
	{
		visuals.PopUpCard(card, () =>
		{
			completedCount++;
			if (request.onComplete != null && completedCount >= totalCount)
				request.onComplete();
		});
	}
	yield return new WaitUntil(() => completedCount >= totalCount);
	break;
}
```

**SlotInBatch：**

```csharp
case AnimationRequestType.SlotInBatch:
{
	int completedCount = 0;
	int totalCount = request.targetCards != null ? request.targetCards.Count : 0;
	if (totalCount == 0) break;
	foreach (var card in request.targetCards)
	{
		visuals.SlotInCard(card, () =>
		{
			completedCount++;
			if (request.onComplete != null && completedCount >= totalCount)
				request.onComplete();
		});
	}
	yield return new WaitUntil(() => completedCount >= totalCount);
	break;
}
```

**Deck Focus Guard**：这两个 case 同样需要触发 focus restoration（如果 deck 处于 peeled 状态）。在 switch 前的 focus guard 条件中，将 `PopUpBatch` 和 `SlotInBatch` 加入已有判断列表，与 `PopUp`/`SlotIn`/`MoveToBottomBatch` 等保持一致。

### 4.3 StatusEffectGiverEffect 集成

修改 `Assets/Scripts/Effects/StatusEffect/StatusEffectGiverEffect.cs` 中以下四个方法，在 capture `StatusEffectProjectile` 的前后插入 `PopUpBatch` 和 `SlotInBatch`。

#### 4.3.1 GiveStatusEffect

当前代码块（末尾的 recorder capture）：

```csharp
var recorderGo = EffectChainManager.Me != null ? EffectChainManager.Me.currentEffectRecorder : null;
var recorder = recorderGo != null ? recorderGo.GetComponent<EffectRecorder>() : null;
if (recorder != null && targetCards.Count > 0)
{
	var targetGameObjects = new List<GameObject>();
	foreach (var t in targetCards)
	{
		if (t != null) targetGameObjects.Add(t.gameObject);
	}
	recorder.animationRequests.Add(new AnimationRequest
	{
		type = AnimationRequestType.StatusEffectProjectile,
		attackerCard = myCard,
		targetCards = targetGameObjects
	});
}
```

新逻辑：

```csharp
var recorderGo = EffectChainManager.Me != null ? EffectChainManager.Me.currentEffectRecorder : null;
var recorder = recorderGo != null ? recorderGo.GetComponent<EffectRecorder>() : null;
if (recorder != null && targetCards.Count > 0)
{
	var targetGameObjects = new List<GameObject>();
	foreach (var t in targetCards)
	{
		if (t != null) targetGameObjects.Add(t.gameObject);
	}

	// 1. Parallel Pop Up all targets
	recorder.animationRequests.Add(new AnimationRequest
	{
		type = AnimationRequestType.PopUpBatch,
		targetCards = targetGameObjects
	});

	// 2. Batch projectile while cards are elevated
	recorder.animationRequests.Add(new AnimationRequest
	{
		type = AnimationRequestType.StatusEffectProjectile,
		attackerCard = myCard,
		targetCards = targetGameObjects
	});

	// 3. Parallel Slot In all targets
	recorder.animationRequests.Add(new AnimationRequest
	{
		type = AnimationRequestType.SlotInBatch,
		targetCards = targetGameObjects
	});
}
```

#### 4.3.2 GiveAllFriendlyStatusEffect

同理，将末尾的 `StatusEffectProjectile` capture 替换为三段式（PopUpBatch → StatusEffectProjectile → SlotInBatch），使用 `targetCardScripts` 构建 `targetGameObjects`。

#### 4.3.3 GiveStatusEffectToLastXCards

同理，替换末尾的单一 `StatusEffectProjectile` capture。

#### 4.3.4 GiveStatusEffectToXFriendly

同理，替换末尾的单一 `StatusEffectProjectile` capture。

### 4.4 边缘情况处理

| ID | 场景 | 处理 |
|----|------|------|
| SEGB-1 | `targetCards` 数量为 0 或全为 null | 提前 `break`，不添加任何 batch request。 |
| SEGB-2 | 目标卡在 PopUp 后、SlotIn 前被销毁（如连锁触发了 Exile） | `SlotInCard` 内部已有防御性检查（`physicalCard == null` 则直接 `onComplete?.Invoke()`），无需额外处理。 |
| SEGB-3 | 目标卡中包含 reveal zone 卡 | `PopUpCard` 使用当前世界坐标计算 peak，reveal zone 卡也能正确升起；`SlotInCard` 检测到 `deckIndex < 0` 时会直接释放 `isPlayingSpecialAnimation` 不移动，符合 PRD EC-1。 |
| SEGB-4 | Deck 处于 focused/peeled 状态 | `RecorderAnimationPlayer` 的 focus guard 会在 `PopUpBatch` / `SlotInBatch` 执行前自动 restore deck focus，与现有 move 类型行为一致。 |
| SEGB-5 | `RecorderAnimationPlayer.me == null`（headless 测试） | `StatusEffectGiverEffect` 的逻辑执行已在 capture 之前完成，batch requests 仅在 `recorder != null` 时添加，headless 模式下无 recorder 则静默跳过动画，逻辑不受影响。 |
| SEGB-6 | `GiveSelfStatusEffect`（单卡、无 projectile） | 不在本方案范围内。`GiveSelfStatusEffect` 调用 `ApplyStatusEffectCore`，后者会自动 capture `StatusEffectChange`，不经过 `StatusEffectGiverEffect` 的 batch 路径。 |

---

## 5. 实施步骤

### Phase 1：基础设施（2 个文件）

| # | 任务 | 文件 |
|---|------|------|
| 1.1 | 在 `AnimationRequestType` enum 中追加 `PopUpBatch`、`SlotInBatch` | `Assets/Scripts/Managers/AnimationRequest.cs` |
| 1.2 | 在 `RecorderAnimationPlayer.PlayRequestCoroutine` 中新增并行 switch case；更新 focus guard 条件 | `Assets/Scripts/Managers/RecorderAnimationPlayer.cs` |

### Phase 2：Effect 集成（1 个文件）

| # | 任务 | 文件 |
|---|------|------|
| 2.1 | 修改 `GiveStatusEffect` 末尾的 recorder capture 为三段式 | `Assets/Scripts/Effects/StatusEffect/StatusEffectGiverEffect.cs` |
| 2.2 | 修改 `GiveAllFriendlyStatusEffect` 末尾的 recorder capture 为三段式 | 同上 |
| 2.3 | 修改 `GiveStatusEffectToLastXCards` 末尾的 recorder capture 为三段式 | 同上 |
| 2.4 | 修改 `GiveStatusEffectToXFriendly` 末尾的 recorder capture 为三段式 | 同上 |

### Phase 3：验证（Play Mode 测试）

| # | 场景 | 验证点 |
|---|------|--------|
| 3.1 | 一张卡给 1 个敌方卡上 status effect | 目标卡 PopUp → Projectile → SlotIn，顺序正确，时间正常 |
| 3.2 | 一张卡给 3 个友方卡上 status effect | 3 张卡同时 PopUp（并行），Projectile 后同时 SlotIn，总时间无明显延迟 |
| 3.3 | 一张卡给 reveal zone 卡上 status effect | reveal zone 卡也能 PopUp，SlotIn 时因不在 deck 中而直接释放 flag，不报错 |
| 3.4 | Headless 测试（`NullCombatVisualsBehaviour`） | 逻辑正确结算，无异常抛出 |

---

## 6. 与现有 PRD 的关系

本方案是 `pop-up-slot-in-animation-prd.md` 的**扩展与补完**：

- PRD 的 Scenario C（Status Effect Projectile）给出了 `CurseEffect` 的单目标示例。
- PRD 的 Phase 2.5 提到修改 `CurseEffect`，但未覆盖 `StatusEffectGiverEffect` 的 batch 路径。
- 本方案填补了 batch 多目标路径的缺口，使所有通过 projectile 给予 status effect 的入口都具备一致的 Pop Up 体验。

---

## 7. 风险与回滚

| 风险 | 可能性 | 影响 | 缓解措施 |
|------|--------|------|----------|
| Batch 并行导致 DOTween tween 冲突 | 低 | 中 | `PopUpCard` 和 `SlotInCard` 内部已实现 `KillTweens()`，可安全覆盖 |
| `targetCards` 包含重复引用导致 callback 计数异常 | 低 | 高 | `StatusEffectGiverEffect` 的 target 选择逻辑天然去重（按 CardScript 实例收集），无需额外处理 |
| 动画总时长仍显拖沓 | 低 | 低 | 并行后总增量固定为 ~0.6s，与目标数无关；若仍觉长，可后续调短 `popUpDuration` / `slotInDuration` |

**回滚方式**：若需紧急回滚，只需将 `StatusEffectGiverEffect.cs` 中的三段式 capture 恢复为原来的单一 `StatusEffectProjectile` capture，无需改动 `AnimationRequest.cs` 和 `RecorderAnimationPlayer.cs`（新增的 enum 和 switch case 是安全的向后兼容扩展）。
