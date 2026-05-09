# Minimum Prototype: EffectRecorder-Driven Animation

## Goal
Validate the core concept: capture animation intents into EffectRecorders during logic execution, then play them sequentially in tree order after the chain closes.

## Scope
Only modify files directly involved in the **reveal -> effect -> bury/stage/attack** flow. Keep everything else untouched.

## Files to Change

### 1. New: `Assets/Scripts/Managers/AnimationRequest.cs`
A plain data class (not MonoBehaviour) that stores animation intent.
- `AnimationRequestType` enum: `Attack`, `MoveToBottom`, `MoveToBottomBatch`, `MoveToTop`, `MoveToTopBatch`, `MoveToIndex`
- Fields for attacker card, target card(s), callbacks (`onHit`, `onComplete`), duration, useArc, etc.
- `MoveToBottomBatch` / `MoveToTopBatch` carry a `List<GameObject> targetCards` to support parallel playback of multiple card movements.

### 2. New: `Assets/Scripts/Managers/RecorderAnimationPlayer.cs`
A MonoBehaviour singleton that plays captured animations in **interleaved tree order**.
- `PlayRecordersCoroutine(List<GameObject> rootRecorders)` - iterates roots in `closedEffectRecorders` order.
- `PlayRecorderCoroutine(EffectRecorder)` - **interleaved playback**: for each `AnimationRequest` in `animationRequests`, plays the request, then immediately recurses into any **unplayed** direct children (by Transform sibling order). This ensures reactive effects triggered by a request are animated before the next request on the same recorder.
- `PlayRequestCoroutine(AnimationRequest)` - dispatches to `ICombatVisuals`. For batch types (`MoveToBottomBatch` / `MoveToTopBatch`), starts all movements in parallel and yields until the last one completes. For **all** Move types (both single-target and batch), calls `UpdateAllPhysicalCardTargets()` **before** starting the movement so non-moving cards slide in parallel. For batch types, this is critical because BuryEffect and StageEffect no longer call it in the logic phase.

### 3. Modify: `Assets/Scripts/Managers/EffectRecorder.cs`
Add fields:
- `public List<AnimationRequest> animationRequests = new List<AnimationRequest>();`
- `public bool animationPlayed = false;` // used to skip already-processed recorders in closedEffectRecorders

### 4. Modify: `Assets/Scripts/Effects/HPAlterEffect.cs`
In `DecreaseTheirHp()` and `DecreaseMyHp()`:
- **Move damage resolution and event raising to logic phase.** Before capturing the `AnimationRequest`, immediately execute:
  - `ProcessDamage(totalDmg, ...)`
  - `CheckDmgTargets_DealingDmgToSelf(totalDmg)` / `CheckDmgTargets_DealingDmgToOpponent(totalDmg)`
- This ensures `onMyPlayerTookDmg` / `onTheirPlayerTookDmg` events fire during effect execution, allowing reactive cards (e.g. Eternal_Ghost) to be captured into the same EffectRecorder tree.
- Replace `combatManager.RaiseDamageDealtEvent(...)` with capturing an `AnimationRequest` on the current `EffectRecorder`.
- **How to get current recorder:** `var recorder = EffectChainManager.Me.currentEffectRecorder?.GetComponent<EffectRecorder>();`
- The `AnimationRequest.onHit` callback should be **null** (damage is already resolved). `onComplete` can be null or used for cleanup logging.
- If no current EffectRecorder exists **or** `RecorderAnimationPlayer.me` is null, fall back to the old `RaiseDamageDealtEvent` path (which keeps the old `onHit` lambda intact for legacy behavior).

### 5. Modify: `Assets/Scripts/Effects/BuryEffect.cs`
In `BuryChosenCards()`:
- Move the `GameEvent` raises (`onMeBuried`, `onAnyCardBuried`, `onFriendlyCardBuried`) out of the animation `onComplete` callback and into the logic phase (immediately after deck modification).
- **Keep `SyncPhysicalCardsWithCombinedDeck()` in the logic phase** (updates the physical card list order to match the logical deck).
- **Remove `UpdateAllPhysicalCardTargets()` from the logic phase.** Do NOT call it here; card target positions will be updated by `RecorderAnimationPlayer` before playing the move animations.
- **Capture a single `MoveToBottomBatch` request** on the current `EffectRecorder` containing all successfully buried cards. This preserves parallel movement. Example:
  ```csharp
  var recorder = EffectChainManager.Me.currentEffectRecorder?.GetComponent<EffectRecorder>();
  if (recorder != null && RecorderAnimationPlayer.me != null)
  {
      recorder.animationRequests.Add(new AnimationRequest {
          type = AnimationRequestType.MoveToBottomBatch,
          targetCards = buriedCards,
          duration = 0.5f,
          useArc = true
      });
  }
  else { /* fallback: old immediate visual calls */ }
  ```
- Fallback path preserved.

### 6. Modify: `Assets/Scripts/Effects/StageEffect.cs`
In `StageChosenCards()`:
- `onMeStaged` event is **already** raised in the logic phase in current code (after deck modification, before animations). No event timing change is needed — only capture the movement into a **`MoveToTopBatch`** `AnimationRequest` on the current `EffectRecorder`.
- Keep `SyncPhysicalCardsWithCombinedDeck()` in logic phase; **remove `UpdateAllPhysicalCardTargets()`** from logic phase.
- Fallback path preserved.

### 7. Modify: `Assets/Scripts/Managers/CombatManager.cs`
In `RevealCards()`, Phase 2 (after `TriggerRevealedCardEffect()`):
- Remove the direct call to `EffectChainManager.Me.CloseOpenedChain()`.
- Replace `StartCoroutine(WaitForAttackAnimationsBeforeNextReveal())` with `StartCoroutine(PlayRecorderAnimationsAndWait())`.
- New private coroutine `PlayRecorderAnimationsAndWait()`:
  1. **Safety wait for legacy animations:** `while (AnimationStateTracker.me != null && AnimationStateTracker.me.HasActiveBatch) yield return null;`
     - This forces any pending `AnimationStateTracker` delayed events to flush before the chain closes, ensuring reactive cards triggered by damage/bury events are captured into the **open** recorder tree.
  2. **Close the chain:** `EffectChainManager.Me.CloseOpenedChain();`
  3. Collect root recorders from `EffectChainManager.Me.closedEffectRecorders` (those whose Transform.parent is `EffectChainManager` itself and whose `animationPlayed == false`).
  4. If `RecorderAnimationPlayer.me` exists and roots > 0:
     - Yield `PlayRecordersCoroutine(roots)`
     - After completion, mark all played recorders' `animationPlayed = true`.
  5. Afterward, yield the original `WaitForAttackAnimationsBeforeNextReveal()` as safety net for any stray legacy animations.

**Phase 1 `CloseOpenedChain` safety**: The two `CloseOpenedChain()` calls in Phase 1 (auto-reveal and normal reveal paths) remain unchanged. They close the previous card's chain, which should already have had its animations played in Phase 2's `PlayRecorderAnimationsAndWait()`.

**Exception safety in `PlayRecorderAnimationsAndWait()`**: Wrap recorder playback in a `try-finally` block. If an animation coroutine throws, ensure:
  1. All processed recorders still get `animationPlayed = true`.
  2. Input blocking is released (`CombatManager.ResetInputBlock()` or paired `UnblockInput` calls).
  3. The coroutine yields `WaitForAttackAnimationsBeforeNextReveal()` in the finally block as a last resort.

**Also update `ExitCombat()`**: Clear and destroy all recorder GameObjects under `EffectChainManager` to prevent accumulation across combat sessions. Also clear `EffectChainManager.Me.closedEffectRecorders`.

## Key Design Decisions

1. **Events fire in logic phase**: `onMeBuried`, `onMeStaged`, and **damage events** (`onMyPlayerTookDmg`, etc.) are raised immediately during effect execution. For `HPAlterEffect`, this means `ProcessDamage` and `CheckDmgTargets` run synchronously inside `DecreaseTheirHp()`/`DecreaseMyHp()`, **before** the `AnimationRequest` is captured. The captured `AnimationRequest` only drives the visual attack animation; it does not carry damage logic in `onHit`.
2. **Interleaved playback (Scheme A)**: `PlayRecorderCoroutine` plays one request, then recursively plays all unplayed children, then moves to the next request. This ensures reactive effects triggered by a specific action are animated before the next unrelated action on the same card.
3. **Parallel batch movements**: `MoveToBottomBatch` and `MoveToTopBatch` start all card movements simultaneously and wait for the slowest one. This preserves the visual feel of the old system where multiple bury/stage animations ran in parallel.
4. **Deferred chain closing**: `CloseOpenedChain` is moved into `PlayRecorderAnimationsAndWait` **after** waiting for `AnimationStateTracker` to become idle. This prevents reactive events from being stranded in a new chain because they were delayed by a pending legacy animation.
5. **Fallback paths**: Every capture site checks `if (recorder != null && RecorderAnimationPlayer.me != null)`. If either is missing (headless test, inspector test, unexpected path), it falls back to the old immediate-play behavior.
6. **Tree traversal**: Uses the existing Transform parent-child hierarchy already created by `EffectChainManager.MakeANewEffectRecorder`. Root nodes are identified by `transform.parent == EffectChainManager.Me.transform`.
7. **No changes to EffectChainManager core logic**: Keep `SameCardDifferentObject`, chain closing, and loop guard exactly as-is. However, we rely on `EffectRecorder.animationPlayed` to filter out already-processed recorders from `closedEffectRecorders`, which is append-only.
8. **No changes to AnimationStateTracker**: Leave it running as a safety net. The new coroutine explicitly waits for it to idle before closing the chain, ensuring delayed events are flushed naturally.
9. **Physical card list sync stays in logic, target update moves to animation**: `SyncPhysicalCardsWithCombinedDeck` rebuilds the physical card list order immediately so the deck state is consistent. `UpdateAllPhysicalCardTargets` is deferred to `RecorderAnimationPlayer` (called before batch move playback) to prevent cards from sliding before their dedicated move animation starts.

## Test Strategy
Build the prototype, then run a combat with this deck order (bottom to top):
1. `Spike_Skeleton` (has onMeBuried -> self-damage or stage)
2. `Start_Card`
3. `Eternal_Ghost` (listens to damage events)
4. `Grave_Punch` (has BuryEffect + HPAlterEffect)

Verify animation order:
1. Spike_Skeleton bury animation (parallel if multiple cards)
2. Spike_Skeleton triggered animation (from onMeBuried)
3. Eternal_Ghost triggered animation (from Spike_Skeleton)
4. Grave_Punch attack animation
5. Eternal_Ghost triggered animation (from Grave_Punch)

## Risk Mitigation
- **Fallback paths** ensure the game doesn't break if a recorder is missing or `RecorderAnimationPlayer` is not present.
- **Deferred chain closing** prevents reactive cards from being orphaned when `AnimationStateTracker` delays their events; by waiting for `HasActiveBatch == false` before calling `CloseOpenedChain`, we guarantee reactive effects join the open recorder tree.
- **`closedEffectRecorders` filtering** via `animationPlayed` prevents stale recorders from being replayed.
- **Original `WaitForAttackAnimationsBeforeNextReveal` called after** the new coroutine catches any stray legacy animations that bypassed the recorder system.
- `AnimationStateTracker` stays active as a secondary guard.
- **Headless test compatibility**: The fallback condition `RecorderAnimationPlayer.me != null` guarantees that headless tests without the new player component continue to work through the old `RaiseDamageDealtEvent` / direct `ICombatVisuals` paths.
- **Damage logic moved to logic phase** prevents reactive cards from being orphaned outside the recorder tree; without this change, Eternal_Ghost-style reactive animations would fire mid-playback and bypass the sequential system entirely.
- **Input blocking preserved**: Individual `ICombatVisuals` methods (`PlayAttackAnimation`, `MoveCardToBottom`, etc.) still manage their own `BlockInput`/`UnblockInput` calls, so player input remains blocked during the entire recorder playback sequence.

## Known Limitations / TODO
- `EffectRecorder` GameObjects are not destroyed after playback to keep the prototype minimal. `CombatManager.ExitCombat()` should clean them up; long-running editor sessions may accumulate objects if not addressed in a future iteration.
- `CombatUXManager`'s subscription to `CombatManager.onDamageDealt` becomes dead code when the recorder path is active. It does not harm functionality but should be cleaned up in a follow-up refactor.
- `AnimationRequestType.Destroy` is omitted from this prototype; only Attack, MoveToBottom, MoveToBottomBatch, MoveToTop, MoveToTopBatch, and MoveToIndex are implemented.

## Implementation Checklist
- [ ] `RecorderAnimationPlayer` singleton setup: add `public static RecorderAnimationPlayer me;` + `Awake() { me = this; }`.
- [ ] `EffectRecorder.animationRequests` field initialization verified on instantiated prefab (prefab should have `new List<AnimationRequest>()` in the field initializer).
- [ ] `PlayRequestCoroutine`: call `UpdateAllPhysicalCardTargets()` before **both** single-target and batch move requests.
- [ ] `StageEffect`: confirm `onMeStaged` is not moved — only `MoveCardToTop` calls are replaced with `MoveToTopBatch` capture.
- [ ] `CombatManager.PlayRecorderAnimationsAndWait`: implement with `try-finally` for exception safety.
- [ ] `CombatManager.ExitCombat`: destroy recorder GameObjects under `EffectChainManager` and clear `closedEffectRecorders`.
