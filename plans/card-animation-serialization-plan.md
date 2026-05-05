# Card Animation Serialization Plan (Event-Level Animation Delay)

## Goal

Implement a global animation coordination system where:

1. **Animations from the same card remain parallel**: Card A's attack animation and Card A's bury animation play simultaneously.
2. **Animations from different cards are serialized by causality**: Card B's self-stage animation (triggered by being buried) waits until **all animations initiated by Card A** have fully completed.
3. **Recursive serialization**: If Card B's animation triggers Card C's animation, C waits for B; B waits for A.

> **Premise**: All effects are assumed to have associated animations or visual effects. Under this premise, intercepting at the `GameEvent` layer is the most natural and least intrusive approach.

### Concrete Scenario

To make the abstract goal concrete, consider the following card interaction:

- **Card A** (e.g., an attack card with a bury side-effect):
  - `DecreaseTheirHp` → triggers the **attack animation** (card rushes to enemy, deals damage, returns)
  - `BuryEffect` → triggers the **bury animation** (target card arcs to the bottom of the deck)
  - These two animations belong to Card A and play **in parallel**.

- **Card B** (the card being buried):
  - Listens to `onMeBuried` event
  - `StageEffect` → triggers the **stage animation** (Card B arcs to the top of the deck)
  - This animation must wait until **Card A's attack and bury animations have both fully completed**.

**Current behavior (before this plan)**: Card A attacks, Card B is buried, and Card B stages itself — all three animations overlap on screen.

**Desired behavior (after this plan)**: Card A attacks **while** burying Card B (parallel). Only after both finish does Card B stage itself (serialized).

---

## Architecture

### Core Insight

All card effects are triggered through `GameEvent.Raise()` / `RaiseSpecific()` / `RaiseOwner()` / `RaiseOpponent()`. If we intercept at this layer:

- When any animation is playing (`pendingAnimations > 0`), newly raised events are **queued** instead of executed immediately.
- Once all pending animations complete, the queued events are **flushed** in FIFO order.
- Each flushed event may start new animations, which blocks further flushing until those animations complete.

This creates a **natural cascading serialization** without modifying any Effect script logic.

### Components

```
┌─────────────────────────────────────────────────────────────┐
│                    AnimationStateTracker                     │
│  (MonoBehaviour Singleton)                                   │
│                                                              │
│  - pendingAnimations: int (reference count)                  │
│  - delayedEvents: Queue<Action>                              │
│                                                              │
│  RegisterAnimation()   -> pendingAnimations++                │
│  CompleteAnimation()   -> pendingAnimations--                │
│                         if 0: FlushDelayedEvents()           │
│  TryExecute(Action)    -> if pending > 0: enqueue            │
│                         else: execute immediately            │
└─────────────────────────────────────────────────────────────┘
                              ▲
                              │ called by
┌─────────────────────────────────────────────────────────────┐
│                      GameEvent (SO)                          │
│                                                              │
│  Raise()           -> AnimationStateTracker.TryExecute(...)  │
│  RaiseOwner()      -> AnimationStateTracker.TryExecute(...)  │
│  RaiseOpponent()   -> AnimationStateTracker.TryExecute(...)  │
│  RaiseSpecific()   -> AnimationStateTracker.TryExecute(...)  │
└─────────────────────────────────────────────────────────────┘
```

---

## Modified Files

### 1. New File: `Assets/Scripts/Managers/AnimationStateTracker.cs`

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Global animation coordinator. When animations are playing, all GameEvent raises
/// are delayed until the current animation batch completes.
/// </summary>
public class AnimationStateTracker : MonoBehaviour
{
	#region SINGLETON
	public static AnimationStateTracker me;
	void Awake() { me = this; }
	#endregion

	[Header("SAFETY")]
	[Tooltip("Maximum seconds an animation batch can hold before forced release")]
	public float timeoutSeconds = 5f;

	private int _pendingAnimations;
	private Queue<Action> _delayedEvents = new Queue<Action>();
	private bool _isFlushing;
	private float _batchStartTime;
	private bool _hasActiveBatch;

	public int PendingAnimations => _pendingAnimations;
	public bool HasActiveBatch => _pendingAnimations > 0;

	/// <summary>
	/// Call when any animation starts.
	/// </summary>
	public void RegisterAnimation()
	{
		if (_pendingAnimations == 0)
		{
			_batchStartTime = Time.time;
			_hasActiveBatch = true;
		}
		_pendingAnimations++;
	}

	/// <summary>
	/// Call when any animation completes.
	/// </summary>
	public void CompleteAnimation()
	{
		_pendingAnimations--;
		if (_pendingAnimations <= 0)
		{
			_pendingAnimations = 0;
			_hasActiveBatch = false;
			if (!_isFlushing)
			{
				FlushDelayedEvents();
			}
		}
	}

	/// <summary>
	/// Attempts to execute an action immediately. If animations are playing,
	/// the action is queued for later execution.
	/// Called by GameEvent.Raise methods.
	/// </summary>
	public void TryExecute(Action action)
	{
		if (_pendingAnimations > 0)
		{
			_delayedEvents.Enqueue(action);
			return;
		}
		action();
	}

	private void FlushDelayedEvents()
	{
		_isFlushing = true;
		while (_delayedEvents.Count > 0)
		{
			var evt = _delayedEvents.Dequeue();
			evt();

			// If the executed event started new animations, stop flushing.
			// Those animations will trigger another flush when they complete.
			if (_pendingAnimations > 0)
			{
				break;
			}
		}
		_isFlushing = false;
	}

	private void Update()
	{
		// Safety: force release if batch exceeds timeout
		if (_hasActiveBatch && Time.time - _batchStartTime > timeoutSeconds)
		{
			Debug.LogWarning(
				"[AnimationStateTracker] Animation batch timed out after " + timeoutSeconds +
				"s. Pending=" + _pendingAnimations + ". Forcing release.");
			_pendingAnimations = 0;
			_hasActiveBatch = false;
			FlushDelayedEvents();
		}
	}
}
```

> **Confirmed**: 5 seconds default, exposed as Inspector field. Sufficient for current animation durations. Adjustable per-project if needed.

---

### 2. `Assets/Scripts/SOScripts/GameEvent.cs`

All four raise methods are wrapped with `AnimationStateTracker.TryExecute`.

```csharp
using System.Collections.Generic;
using DefaultNamespace;
using DefaultNamespace.Effects;
using UnityEngine;

[CreateAssetMenu]
public class GameEvent : ScriptableObject
{
	private List<GameEventListener> _listeners = new List<GameEventListener>();

	public void Raise()
	{
		var tracker = AnimationStateTracker.me;
		if (tracker != null)
		{
			tracker.TryExecute(() => ExecuteRaise());
			return;
		}
		ExecuteRaise();
	}

	public void RaiseOwner()
	{
		var tracker = AnimationStateTracker.me;
		if (tracker != null)
		{
			tracker.TryExecute(() => ExecuteRaiseOwner());
			return;
		}
		ExecuteRaiseOwner();
	}

	public void RaiseOpponent()
	{
		var tracker = AnimationStateTracker.me;
		if (tracker != null)
		{
			tracker.TryExecute(() => ExecuteRaiseOpponent());
			return;
		}
		ExecuteRaiseOpponent();
	}

	public void RaiseSpecific(GameObject target)
	{
		if (target == null) return;
		var tracker = AnimationStateTracker.me;
		if (tracker != null)
		{
			tracker.TryExecute(() => ExecuteRaiseSpecific(target));
			return;
		}
		ExecuteRaiseSpecific(target);
	}

	// --- Internal execution methods ---

	private void ExecuteRaise()
	{
		for (var i = _listeners.Count - 1; i >= 0; i--)
		{
			_listeners[i].OnEventRaised();
		}
	}

	private void ExecuteRaiseOwner()
	{
		for (var i = _listeners.Count - 1; i >= 0; i--)
		{
			if (_listeners[i].GetComponent<CardScript>().myStatusRef == CombatManager.Me.ownerPlayerStatusRef)
			{
				_listeners[i].OnEventRaised();
			}
		}
	}

	private void ExecuteRaiseOpponent()
	{
		for (var i = _listeners.Count - 1; i >= 0; i--)
		{
			if (_listeners[i].GetComponent<CardScript>().myStatusRef == CombatManager.Me.enemyPlayerStatusRef)
			{
				_listeners[i].OnEventRaised();
			}
		}
	}

	private void ExecuteRaiseSpecific(GameObject target)
	{
		var listeners = target.GetComponentsInChildren<GameEventListener>();
		foreach (var listenerFromParentOrChild in listeners)
		{
			if (_listeners.Contains(listenerFromParentOrChild))
			{
				listenerFromParentOrChild.OnEventRaised();
			}
		}
	}

	public void RegisterListener(GameEventListener listener)
	{
		_listeners.Add(listener);
	}

	public void UnregisterListener(GameEventListener listener)
	{
		if (_listeners != null)
		{
			_listeners.Remove(listener);
		}
	}

	public int ReturnAmountOfListeners()
	{
		return _listeners.Count;
	}
}
```

> **Note**: `RaiseSpecific`'s early `target == null` return is preserved before the tracker check. This avoids enqueuing a no-op action.

---

### 3. `Assets/Scripts/Managers/AttackAnimationManager.cs`

Register at queue dequeue; complete after full coroutine finishes.

```csharp
private IEnumerator ProcessQueue()
{
	_isProcessingQueue = true;

	while (_attackQueue.Count > 0)
	{
		var data = _attackQueue.Dequeue();
		AnimationStateTracker.me?.RegisterAnimation();
		yield return StartCoroutine(PlayAttackAnimationCoroutine(data));
		AnimationStateTracker.me?.CompleteAnimation();
	}

	_isProcessingQueue = false;

	if (_combatUXManager != null && _combatUXManager.IsDeckFocused)
	{
		yield return StartCoroutine(_combatUXManager.RestoreDeckFocusCoroutine());
	}
}
```

> **Confirmed**: `RestoreDeckFocusCoroutine` is registered as a pending animation. Events raised during deck focus restoration are delayed until restoration completes, preventing visual misalignment.

---

### 4. `Assets/Scripts/UXPrototype/CombatUXManager.cs`

#### `MoveCardWithAnimation`

Register when Sequence is created; complete in `OnComplete`.

```csharp
public void MoveCardWithAnimation(GameObject logicalCard, CardMoveConfig config)
{
	// ... existing validation ...

	AnimationStateTracker.me?.RegisterAnimation();

	Sequence moveSequence = DOTween.Sequence();
	// ... existing move/scale setup ...

	moveSequence.OnComplete(() =>
	{
		AnimationStateTracker.me?.CompleteAnimation();
		// ... existing OnComplete logic ...
	});

	moveSequence.Play();
}
```

#### `PlayShuffleAnimationInternal`

Register once for the entire shuffle batch; complete after the last card finishes.

```csharp
private void PlayShuffleAnimationInternal(Dictionary<GameObject, Vector3> shuffleTargets, Action onComplete)
{
	if (shuffleTargets.Count == 0)
	{
		onComplete?.Invoke();
		return;
	}

	AnimationStateTracker.me?.RegisterAnimation();

	int completedCount = 0;
	int totalCount = shuffleTargets.Count;
	// ... existing setup ...

	foreach (var kvp in shuffleTargets)
	{
		// ... existing sequence creation ...

		moveSequence.OnComplete(() =>
		{
			// ... existing per-card cleanup ...
			completedCount++;
			if (completedCount >= totalCount)
			{
				AnimationStateTracker.me?.CompleteAnimation();
				onComplete?.Invoke();
			}
		});

		moveSequence.Play();
	}
}
```

#### `DestroyCardWithAnimation`

Register when Sequence is created; complete in `OnComplete`.

```csharp
public void DestroyCardWithAnimation(GameObject logicalCard, System.Action onComplete = null)
{
	// ... existing validation ...

	AnimationStateTracker.me?.RegisterAnimation();

	Sequence destroySequence = DOTween.Sequence();
	// ... existing move/scale setup ...

	destroySequence.OnComplete(() =>
	{
		AnimationStateTracker.me?.CompleteAnimation();
		Destroy(physicalCard);
		Destroy(logicalCard);
		onComplete?.Invoke();
	});
}
```

#### Deck Focus / Peel Coroutines

Register at coroutine start; complete after `yield return WaitUntil(...)`.

**`StartPeelCoroutine`:**
```csharp
private IEnumerator StartPeelCoroutine(int targetIndex)
{
	// ... early returns ...

	AnimationStateTracker.me?.RegisterAnimation();

	// ... existing setup ...

	yield return new WaitUntil(() => animCompletedCount >= animTotalCount);

	AnimationStateTracker.me?.CompleteAnimation();
}
```

**`TransitionFocusCoroutine`:**
```csharp
private IEnumerator TransitionFocusCoroutine(int newTargetIndex, int currentTargetIndex)
{
	// ... early returns ...

	AnimationStateTracker.me?.RegisterAnimation();

	// ... existing setup ...

	yield return new WaitUntil(() => animCompletedCount >= animTotalCount);

	// ... update peeled state ...

	AnimationStateTracker.me?.CompleteAnimation();
}
```

**`RestoreDeckFocusCoroutine`:**
```csharp
public IEnumerator RestoreDeckFocusCoroutine()
{
	if (!enablePeelDeck) yield break;
	if (!_isDeckFocused) yield break;

	AnimationStateTracker.me?.RegisterAnimation();

	// ... existing setup ...

	if (totalCount > 0)
	{
		yield return new WaitUntil(() => completedCount >= totalCount);
	}

	// ... clear state ...

	AnimationStateTracker.me?.CompleteAnimation();
}
```

#### `PlayStatusEffectProjectile`

Register when Sequence is created; complete in `OnComplete`.

```csharp
public void PlayStatusEffectProjectile(GameObject giverCard, GameObject receiverCard, Action onComplete = null)
{
	// ... validation ...

	AnimationStateTracker.me?.RegisterAnimation();

	Sequence projectileSequence = DOTween.Sequence();
	// ... existing setup ...

	projectileSequence.OnComplete(() =>
	{
		AnimationStateTracker.me?.CompleteAnimation();
		Destroy(projectile);
		onComplete?.Invoke();
	});

	projectileSequence.Play();
}
```

#### `PlayMultiStatusEffectProjectile`

**Batch tracking**: Register once for the entire multi-projectile burst; complete after the last projectile lands.

```csharp
public void PlayMultiStatusEffectProjectile(
	GameObject giverCard,
	List<CardScript> targetCards,
	System.Action<CardScript> onEachComplete,
	System.Action onAllComplete = null,
	float? customStaggerDelay = null)
{
	if (targetCards == null || targetCards.Count == 0)
	{
		onAllComplete?.Invoke();
		return;
	}

	if (statusEffectProjectilePrefab == null || giverCard == null)
	{
		foreach (var target in targetCards)
		{
			onEachComplete?.Invoke(target);
		}
		onAllComplete?.Invoke();
		return;
	}

	BlockInput(this);
	AnimationStateTracker.me?.RegisterAnimation();

	float staggerDelay = customStaggerDelay ?? projectileStaggerDelay;
	int completedCount = 0;
	int totalCount = targetCards.Count;

	for (int i = 0; i < targetCards.Count; i++)
	{
		var targetCardScript = targetCards[i];
		
		DOVirtual.DelayedCall(i * staggerDelay, () =>
		{
			PlayStatusEffectProjectile(
				giverCard, 
				targetCardScript.gameObject, 
				() =>
				{
					onEachComplete?.Invoke(targetCardScript);
					
					completedCount++;
					if (completedCount >= totalCount)
					{
						UnblockInput(this);
						AnimationStateTracker.me?.CompleteAnimation();
						onAllComplete?.Invoke();
					}
				}
			);
		});
	}
}
```

> **Confirmed**: Entire multi-projectile burst is tracked as one batch. `pending` stays > 0 until the last projectile completes, preventing events from slipping through stagger gaps.

---

### 5. `Assets/Scripts/Effects/BuryCostEffect.cs`

Register during the visual bury phase; complete per card.

```csharp
// In the visual bury loop:
combatManager.visuals.MoveCardToBottom(card, duration: 0.5f, useArc: true, onComplete: () =>
{
	// ... existing event raises and logic ...
});
```

> **Note**: `MoveCardToBottom` delegates to `MoveCardWithAnimation`, which already handles Register/Complete. No direct changes needed here unless there are additional visual steps.

---

### 6. `Assets/Scripts/Effects/MinionCostEffect.cs`

The `DestroyCardWithAnimation` call already delegates to `CombatUXManager`, which handles Register/Complete. No changes needed.

---

## Decisions (All Confirmed)

| # | Decision | Rationale |
|---|----------|-----------|
| 1 | **Timeout**: 5 seconds default, exposed as Inspector field (`public float timeoutSeconds`). | Current animation durations (attack ~0.85s, shuffle ~0.8s, multi-projectile ~0.85s) are well within 5s. Adjustable per-project if slow-motion or cinematic animations are added later. |
| 2 | **`RestoreDeckFocusCoroutine`**: **Counted as pending animation**. | Deck is physically moving during restore; allowing concurrent effect animations could cause visual misalignment. |
| 3 | **`PlayMultiStatusEffectProjectile`**: **Batch tracking** — register once for the entire burst, complete after the last projectile lands. | Conservative approach. Prevents events from slipping through stagger gaps between individual projectiles. |
| 4 | **Reveal flow**: `MoveRevealedCardToBottom` and `MoveCardToRevealZone` **do NOT register** as pending animations. No event whitelist needed. | These are core flow animations, not effect-triggered animations. Normal reveal timing (player reaction > 0.5s) means `onMeRevealed` fires when pending is already 0. In extreme fast-click edge cases, a 0.5s delay is acceptable. |
| 5 | **Effect chain depth**: Accept current behavior. | `CloseOpenedChain()` already fires in Phase 1 (player click), which happens after animations complete. Delayed events see the same chain state as they would without this system. Loop guard improvements are out of scope for this plan. |
| 6 | **Input blocking**: **Keep existing approach** (individual animations call `BlockInput`/`UnblockInput`). `AnimationStateTracker` does NOT manage input blocking. | `CombatManager.BlockInput` already uses reference counting, correctly handling concurrent animations. Some animations (Peel, Destroy, single projectile) currently lack `BlockInput`; these can be added individually later if needed. |

---

## Implementation Order

1. **Create `AnimationStateTracker.cs`** with singleton, reference counting, delayed queue, and timeout safety.
2. **Modify `GameEvent.cs`** to wrap all four raise methods with `TryExecute`.
3. **Modify `AttackAnimationManager.cs`** to Register/Complete around `PlayAttackAnimationCoroutine`.
4. **Modify `CombatUXManager.cs`**:
   - `MoveCardWithAnimation`
   - `PlayShuffleAnimationInternal`
   - `DestroyCardWithAnimation`
   - `StartPeelCoroutine`
   - `TransitionFocusCoroutine`
   - `RestoreDeckFocusCoroutine`
   - `PlayStatusEffectProjectile`
5. **Add `AnimationStateTracker` to scene**: Attach to an existing manager GameObject (e.g., `CombatManager` or a dedicated "Systems" object).
6. **Test with debug logging**: Temporarily add `Debug.Log` to `RegisterAnimation` / `CompleteAnimation` / `TryExecute` to verify event delay behavior.

---

## Risks & Mitigations

| Risk | Mitigation |
|------|-----------|
| Animation entry point missed -> `pending` never reaches 0 -> deadlock | Timeout failsafe (5s forced release + Warning log) |
| Animation completes before `RegisterAnimation` is called (race condition) | Ensure Register is called **before** `Sequence.Play()` or `StartCoroutine()` |
| Event delay changes critical timing (e.g., `onMyPlayerTookDmg` triggering healing that must happen before HP hits 0) | Under the "all effects have animations" premise, this is acceptable. If pure logic effects exist, they should ideally also have instantaneous visual feedback to justify the delay. |
| Reveal flow: `onMeRevealed` delayed by `MoveRevealedCardToBottom` during extreme fast-click | Edge case (< 0.5s between clicks). Normal player reaction time avoids this. No whitelist added per decision #4. |
| Flush loop causes stack overflow (event A triggers event B which triggers event C, all in same frame) | `FlushDelayedEvents` breaks the loop when `_pendingAnimations > 0`. DOTween animations complete on subsequent frames, naturally throttling the flush. |
| `GameEventListener` destroyed while its event is delayed | `GameEventListener.OnDisable` calls `UnregisterListener`. When the delayed `Raise` executes, the listener is no longer in `_listeners`. Safe. |
| Multiple `RestoreDeckFocusCoroutine` calls (one from attack queue, one from manual stop) | Each registers independently. If two run concurrently, `pending` goes to 2, then 0. Correct. |

---

## Test Cases

### 1. Basic Serialization: Card A attacks + buries Card B, Card B stages itself

**Setup**: Card A has `DecreaseTheirHp` + `BuryEffect` (targets B). Card B listens to `onMeBuried` with `StageEffect` (targets self).

**Expected behavior**:
1. A's attack animation starts (pending = 1)
2. A's bury animation starts (pending = 2)
3. B's bury animation completes -> `onMeBuried` is raised
4. `AnimationStateTracker` detects `pending > 0` -> B's `onMeBuried` is **queued**
5. A's attack animation completes (pending = 1 -> still > 0)
6. A's bury animation completes (pending = 0)
7. Flush begins -> B's `onMeBuried` executes
8. B's `StageEffect` triggers -> B's stage animation starts (pending = 1)
9. B's stage animation completes (pending = 0)

**Verify**: B's stage animation never overlaps with A's attack animation.

### 2. Parallel Within Same Card

**Setup**: Card A has two `DecreaseTheirHp` calls (e.g., `DecreaseTheirHpTimesX(2)`).

**Expected behavior**:
1. First attack enqueued to `AttackAnimationManager` (pending = 1 after dequeue)
2. Second attack enqueued (pending = 1, queue has 1 pending)
3. First attack plays, completes (pending = 0)
4. Flush runs (no delayed events) -> second attack starts (pending = 1)

Wait — `AttackAnimationManager` already serializes attacks internally. The pending count goes: Register (1) -> Complete (0) -> Register (1) -> Complete (0). So there is no window where both attacks are counted as pending simultaneously.

**Expected behavior (revised)**: `pending` never exceeds 1 for attack queue. Events raised during the gap between attack 1 complete and attack 2 start would execute immediately. This is existing behavior and acceptable.

### 3. Multi-Projectile Status Effect

**Setup**: Card A applies a status effect to 3 cards via `PlayMultiStatusEffectProjectile`.

**Expected behavior** (batch tracking):
- `PlayMultiStatusEffectProjectile` calls `RegisterAnimation()` once
- 3 projectiles launch with stagger
- `pending` remains 1 for the entire duration (0 ~ 0.5s)
- Any events raised during this window are queued
- After the 3rd projectile lands, `CompleteAnimation()` is called
- Queued events are flushed

**Verify**: No events slip through stagger gaps.

### 4. Shuffle During Active Animations

**Setup**: A card effect triggers an attack, and during that attack, the Start Card is revealed (triggering shuffle).

**Expected behavior**:
- Attack animation is playing (pending >= 1)
- Start Card reveal triggers `onMeRevealed` -> `TriggerStartCardEffect`
- Shuffle animation is requested
- `PlayShuffleAnimationInternal` calls `RegisterAnimation`
- pending increments
- Both attack and shuffle play concurrently (same batch)
- When both complete, pending reaches 0
- Flush runs

**Verify**: Shuffle and attack can still play concurrently if they belong to the same causal chain.

### 5. Deep Nesting: A -> B -> C

**Setup**: A buries B. B's `onMeBuried` stages B. B's `onMeStaged` triggers damage to C.

**Expected behavior**:
1. A's bury animation plays
2. B's `onMeBuried` is queued (pending > 0)
3. A's animation completes, flush runs
4. B's stage animation plays
5. B's `onMeStaged` is queued (pending > 0)
6. B's animation completes, flush runs
7. C's damage animation plays

**Verify**: A, B, C animations are strictly serialized. No overlap.

### 6. Timeout Failsafe

**Setup**: Intentionally omit `CompleteAnimation()` in one animation entry point (simulating a bug).

**Expected behavior**:
- After 5 seconds, `AnimationStateTracker` logs a warning, forces `pending = 0`, and flushes the queue.
- The game continues (degraded) rather than soft-locking.

---

## Known Limitations

### Pure Logic Effects Are Also Delayed

Effects that do not trigger any animation (e.g., `ShieldAlterEffect`, `AddTextEffect`, or counter-attacks with no visual feedback) will still have their `GameEvent` triggers delayed if they fire while another animation is playing.

**Example**: Card A's attack animation is playing. Card B has a Counter status that triggers a pure-logic retaliation via `onMyPlayerTookDmg`. The retaliation logic will execute only after Card A's attack animation fully completes, rather than at the exact moment of damage resolution.

**Impact**:
- Combat log entries may appear slightly later than the action they describe.
- Shield/heal applications are deferred until the current animation batch finishes.
- Counter/retaliation timing is shifted to the end of the triggering animation.

**Mitigation**: Under the project premise that "all effects will eventually have associated animations or visual effects," this limitation is temporary. Once pure-logic effects are given even a minimal animation (e.g., a floating number, a flash tint, or a particle burst), they naturally participate in the serialization system without additional code changes.

---

## Open Design Decisions

1. **Should `AnimationStateTracker` also centralize input blocking?** *(Decision: No — keep existing per-animation BlockInput calls per Decision #6)*
   - Currently: Each animation calls `BlockInput` / `UnblockInput` individually.
   - `CombatManager` already uses reference counting, correctly handling concurrent animations.
   - Some animations (Peel, Destroy, single projectile) currently lack `BlockInput`; these can be added individually if play-testing reveals issues.

> **Note**: Delayed event queue uses simple FIFO ordering. Events are flushed in the order they were raised. This is sufficient for current design; no priority system needed.
