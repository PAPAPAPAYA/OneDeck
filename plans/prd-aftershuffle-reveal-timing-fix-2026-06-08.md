# PRD: afterShuffle Event Timing Fix

## 1. Overview

### 1.1 Background

When the Start Card is revealed and its shuffle effect plays, the `afterShuffle` event is currently raised **inside** `CombatManager.RevealNextCard()`. This causes a critical visual timing issue when the next revealed card (e.g. BOOSTER) has an `afterShuffle`-bound Stage effect:

1. Start Card `PlayRecorderAnimationsAndWait` coroutine starts (plays Shuffle animation).
2. Player clicks to reveal the next card → `RevealNextCard()` executes.
3. Inside `RevealNextCard()`, `afterShuffle.Raise()` fires synchronously.
4. BOOSTER's Stage effect runs immediately → `SyncPhysicalCardsWithCombinedDeck()` reorders `physicalCardsInDeck`.
5. Start Card coroutine continues in the background and reaches line 484 `UpdateAllPhysicalCardTargets`.
6. This `UpdateAllPhysicalCardTargets` sees the **already-reordered** deck list and tweens dummy cards toward their staged positions.
7. When BOOSTER's `MoveToTopPopUpBatch` animation finally plays, the dummy cards are already at (or near) their destination, resulting in a **zero-distance "in-place" animation**.

### 1.2 Goal

Restructure the event timing so that the full flow follows this strict sequence:

1. Start Card effect triggers.
2. Shuffle animation plays.
3. Shuffle animation completes (including the full `PlayRecorderAnimationsAndWait` coroutine).
4. The next card is moved into the reveal zone.
5. `afterShuffle` event fires **after** step 4 and **after** the previous coroutine has fully finished.

### 1.3 Design Rationale

- **Eliminate the timing window**: By moving `afterShuffle.Raise()` out of `RevealNextCard()` and moving `isPlayingEffectAnimations = false` to after `UpdateAllPhysicalCardTargets()`, we guarantee that `SyncPhysicalCardsWithCombinedDeck` never happens while a prior `UpdateAllPhysicalCardTargets` is still pending.
- **Preserve user intent**: The player expects to see the Stage pop-up animation play when the card is revealed. If the cards have already silently tweened to their final positions, the pop-up arc animation becomes invisible.
- **Minimal intrusion**: Only two changes in `CombatManager.cs` are required. No effect components, visual managers, or animation players need modification.

---

## 2. Scope

### 2.1 In Scope

- `CombatManager.PlayRecorderAnimationsAndWait()` — move `isPlayingEffectAnimations = false` to after `UpdateAllPhysicalCardTargets()`.
- `CombatManager.RevealNextCard()` — remove `afterShuffle` event raising.
- `CombatManager.RevealCards()` **Round Start path** — add `isPlayingEffectAnimations` guard (this path previously had none).
- `CombatManager.RevealCards()` **Phase 1** — relocate `afterShuffle.Raise()` to both the auto-reveal path and the normal reveal path.

### 2.2 Out of Scope

- `StartCardShuffleEffect` logic (no changes).
- `StageEffect` logic (no changes).
- `RecorderAnimationPlayer` logic (no changes).
- `CombatUXManager.UpdateAllPhysicalCardTargets` logic (no changes).
- Any card prefab or `GameEvent` asset modifications.

---

## 3. Technical Design

### 3.1 High-Level Flow

```
BEFORE (Current):
  Round Start → RevealNextCard()
    ├── MoveCardToRevealZone(BOOSTER)
    ├── afterShuffle.Raise()          ← triggers Stage logic here
    └── return
  Start Card coroutine (background)
    └── line 484: UpdateAllPhysicalCardTargets  ← sees reordered deck

AFTER (Fixed):
  Round Start → if (isPlayingEffectAnimations) return
    ├── RevealNextCard()
    │      └── MoveCardToRevealZone(BOOSTER)
    └── afterShuffle.Raise()          ← kept here, now guarded
  Start Card coroutine already finished before Round Start entered
```

### 3.2 Move `isPlayingEffectAnimations = false` to after `UpdateAllPhysicalCardTargets`

**File**: `Assets/Scripts/Managers/CombatManager.cs`

**Before**:
```csharp
private System.Collections.IEnumerator PlayRecorderAnimationsAndWait()
{
	isPlayingEffectAnimations = true;
	// 1. Safety wait for active animation batches
	while (AnimationStateTracker.me != null && AnimationStateTracker.me.HasActiveBatch)
	{
		yield return null;
	}

	// 2. Close the chain
	EffectChainManager.Me.CloseOpenedChain();

	try
	{
		// 3. Collect root recorders and play animations
		if (RecorderAnimationPlayer.me != null)
		{
			// ... existing collection logic ...
			if (roots.Count > 0)
			{
				yield return StartCoroutine(RecorderAnimationPlayer.me.PlayRecordersCoroutine(roots));
			}
		}
	}
	finally
	{
		// Mark all recorders in closedEffectRecorders as played to prevent replay on exception
		if (EffectChainManager.Me != null && EffectChainManager.Me.closedEffectRecorders != null)
		{
			foreach (var recObj in EffectChainManager.Me.closedEffectRecorders)
			{
				if (recObj == null) continue;
				var recorder = recObj.GetComponent<EffectRecorder>();
				if (recorder != null)
				{
					recorder.animationPlayed = true;
				}
			}
		}

		// Ensure input blocking is released
		ResetInputBlock();
		isPlayingEffectAnimations = false;
	}

	// Wait for attack animations to finish before next reveal
	yield return StartCoroutine(WaitForAttackAnimationsBeforeNextReveal());

	// Ensure all physical cards tween to their final positions after recorder animations complete.
	if (visuals != null)
	{
		visuals.UpdateAllPhysicalCardTargets();
	}
}
```

**After**:
```csharp
private System.Collections.IEnumerator PlayRecorderAnimationsAndWait()
{
	isPlayingEffectAnimations = true;
	// 1. Safety wait for active animation batches
	while (AnimationStateTracker.me != null && AnimationStateTracker.me.HasActiveBatch)
	{
		yield return null;
	}

	// 2. Close the chain
	EffectChainManager.Me.CloseOpenedChain();

	try
	{
		// 3. Collect root recorders and play animations
		if (RecorderAnimationPlayer.me != null)
		{
			// ... existing collection logic ...
			if (roots.Count > 0)
			{
				yield return StartCoroutine(RecorderAnimationPlayer.me.PlayRecordersCoroutine(roots));
			}
		}
	}
	finally
	{
		// Mark all recorders in closedEffectRecorders as played to prevent replay on exception
		if (EffectChainManager.Me != null && EffectChainManager.Me.closedEffectRecorders != null)
		{
			foreach (var recObj in EffectChainManager.Me.closedEffectRecorders)
			{
				if (recObj == null) continue;
				var recorder = recObj.GetComponent<EffectRecorder>();
				if (recorder != null)
				{
					recorder.animationPlayed = true;
				}
			}
		}

		// Ensure input blocking is released
		ResetInputBlock();
		// NOTE: isPlayingEffectAnimations is reset AFTER UpdateAllPhysicalCardTargets below
	}

	// Wait for attack animations to finish before next reveal
	yield return StartCoroutine(WaitForAttackAnimationsBeforeNextReveal());

	// Ensure all physical cards tween to their final positions after recorder animations complete.
	if (visuals != null)
	{
		visuals.UpdateAllPhysicalCardTargets();
	}

	isPlayingEffectAnimations = false;
}
```

### 3.3 Remove `afterShuffle` from `RevealNextCard`

**File**: `Assets/Scripts/Managers/CombatManager.cs`

**Before**:
```csharp
private void RevealNextCard()
{
    // ... existing reveal logic ...

    // Trigger delayed afterShuffle event if pending
    if (_raiseAfterShuffleOnNextReveal)
    {
        Debug.Log("[CombatManager] RevealNextCard raising afterShuffle for card=" + cardRevealed.name);
        _raiseAfterShuffleOnNextReveal = false;
        GameEventStorage.me.afterShuffle.Raise();
        Debug.Log("[CombatManager] RevealNextCard afterShuffle raised DONE for card=" + cardRevealed.name);
    }
}
```

**After**:
```csharp
private void RevealNextCard()
{
    // ... existing reveal logic ...

    // afterShuffle raising removed from here — moved to RevealCards Phase 1
}
```

### 3.4 Add `isPlayingEffectAnimations` guard to Round Start path

**File**: `Assets/Scripts/Managers/CombatManager.cs`

This is the **primary path** after a Start Card shuffle. `HandleNewRoundStart()` resets `cardsRevealedThisRound = 0`, so on the next frame `revealZone == null && cardsRevealedThisRound == 0` is true and this block executes **before Phase 1**. Previously it had no guard.

**Before**:
```csharp
// ========== Round start: automatically reveal Start Card (player sees it directly) ==========
if (revealZone == null && cardsRevealedThisRound == 0)
{
    visuals.InstantiateAllPhysicalCards();
    
    // Reveal Start Card (it's at the bottom of the list but top of the actual deck)
    if (combinedDeckZone.Count > 0)
    {
        RevealNextCard();
        awaitingRevealConfirm = false; // Enter Start Card effect trigger phase
    }

    // Trigger delayed afterShuffle event if pending (e.g. after Start Card shuffle)
    if (_raiseAfterShuffleOnNextReveal)
    {
        _raiseAfterShuffleOnNextReveal = false;
        GameEventStorage.me.afterShuffle.Raise();
    }

    return;
}
```

**After**:
```csharp
// ========== Round start: automatically reveal Start Card (player sees it directly) ==========
if (revealZone == null && cardsRevealedThisRound == 0)
{
    // Guard: don't auto-reveal Start Card while effect recorder animations are playing
    if (isPlayingEffectAnimations)
    {
        return;
    }

    visuals.InstantiateAllPhysicalCards();
    
    // Reveal Start Card (it's at the bottom of the list but top of the actual deck)
    if (combinedDeckZone.Count > 0)
    {
        RevealNextCard();
        awaitingRevealConfirm = false; // Enter Start Card effect trigger phase
    }

    // Trigger delayed afterShuffle event if pending (e.g. after Start Card shuffle)
    if (_raiseAfterShuffleOnNextReveal)
    {
        _raiseAfterShuffleOnNextReveal = false;
        GameEventStorage.me.afterShuffle.Raise();
    }

    return;
}
```

### 3.5 Relocate `afterShuffle` in `RevealCards` Phase 1 (both paths)

**File**: `Assets/Scripts/Managers/CombatManager.cs`

This is a **defensive** relocation for non-round-start scenarios (e.g. revealZone card was exiled mid-round).

**Before**:
```csharp
// ========== Phase 1: Wait to process current card and reveal next ==========
if (awaitingRevealConfirm)
{
    // Guard: don't advance state while effect recorder animations are playing
    if (isPlayingEffectAnimations)
    {
        return;
    }

    // Auto-reveal next card if current revealed card was removed
    if (revealZone == null && combinedDeckZone.Count > 0)
    {
        RevealNextCard();
        awaitingRevealConfirm = false;
        EffectChainManager.Me.CloseOpenedChain();
        return;
    }

    // Combat end check ...

    // Prompt text ...
    visuals.InstantiateAllPhysicalCards();
    if (!Input.GetKeyDown(KeyCode.Space) && !DeckTester.me.autoSpace && !Input.GetMouseButtonDown(0)) return;
    CombatLog.me?.Clear();

    // 1. Put current card back to bottom of deck
    PutRevealedCardToBottom();

    // 2. Reveal next card (if any)
    if (combinedDeckZone.Count > 0)
    {
        RevealNextCard();
        awaitingRevealConfirm = false; // Enter effect trigger phase
    }

    EffectChainManager.Me.CloseOpenedChain();
}
```

**After**:
```csharp
// ========== Phase 1: Wait to process current card and reveal next ==========
if (awaitingRevealConfirm)
{
    // Guard: don't advance state while effect recorder animations are playing
    if (isPlayingEffectAnimations)
    {
        return;
    }

    // Auto-reveal next card if current revealed card was removed from game (exiled/destroyed)
    if (revealZone == null && combinedDeckZone.Count > 0)
    {
        RevealNextCard();
        awaitingRevealConfirm = false;

        // Trigger delayed afterShuffle event if pending
        if (_raiseAfterShuffleOnNextReveal)
        {
            _raiseAfterShuffleOnNextReveal = false;
            GameEventStorage.me.afterShuffle.Raise();
        }

        EffectChainManager.Me.CloseOpenedChain();
        return;
    }

    // Combat end check ...

    // Prompt text ...
    visuals.InstantiateAllPhysicalCards();
    if (!Input.GetKeyDown(KeyCode.Space) && !DeckTester.me.autoSpace && !Input.GetMouseButtonDown(0)) return;
    CombatLog.me?.Clear();

    // 1. Put current card back to bottom of deck
    PutRevealedCardToBottom();

    // 2. Reveal next card (if any)
    if (combinedDeckZone.Count > 0)
    {
        RevealNextCard();
        awaitingRevealConfirm = false; // Enter effect trigger phase
    }

    // Trigger delayed afterShuffle event if pending
    if (_raiseAfterShuffleOnNextReveal)
    {
        _raiseAfterShuffleOnNextReveal = false;
        GameEventStorage.me.afterShuffle.Raise();
    }

    EffectChainManager.Me.CloseOpenedChain();
}
```

---

## 4. Timing & State Analysis

### 4.1 Coroutine Boundary Guarantee

`isPlayingEffectAnimations` is set to `true` at the start of `PlayRecorderAnimationsAndWait()` and now set to `false` **after** `UpdateAllPhysicalCardTargets()`. The guard `if (isPlayingEffectAnimations) return;` now protects **both the Round Start path and Phase 1**, ensuring the entire coroutine — including the previously problematic line 484 `UpdateAllPhysicalCardTargets` — has fully terminated before any reveal path is allowed to proceed.

| Moment | `isPlayingEffectAnimations` | Round Start / Phase 1 allowed? |
|--------|----------------------------|-------------------------------|
| Shuffle animation playing | `true` | ❌ Blocked |
| `OnStartCardShuffleAnimationComplete` called | `true` | ❌ Blocked |
| `finally` block executes | `true` | ❌ Blocked |
| `WaitForAttackAnimationsBeforeNextReveal` yields | `true` | ❌ Blocked |
| `UpdateAllPhysicalCardTargets` executes | `true` | ❌ Blocked |
| `isPlayingEffectAnimations = false` (end of coroutine) | `false` | ✅ Allowed |
| Player clicks reveal | `false` | ✅ Allowed |

### 4.2 Event Ordering

```
Start Card Phase 2
  └── StartCoroutine(PlayRecorderAnimationsAndWait)
         ├── isPlayingEffectAnimations = true
         ├── RecorderAnimationPlayer.PlayRecordersCoroutine()
         │      └── PlayShuffleAnimation()
         │             └── onComplete → OnStartCardShuffleAnimationComplete()
         │                    └── HandleNewRoundStart()
         │                           └── cardsRevealedThisRound = 0
         ├── finally: ResetInputBlock()
         ├── yield WaitForAttackAnimationsBeforeNextReveal()
         ├── UpdateAllPhysicalCardTargets()  ← old list, no premature tween
         └── isPlayingEffectAnimations = false  ← guard now passes

Next frame (auto-advance, no click needed)
  └── Round Start path
         ├── isPlayingEffectAnimations == false → pass
         ├── RevealNextCard() → BOOSTER enters reveal zone
         ├── afterShuffle.Raise()  ← kept here, now guarded
         │      └── Booster Stage → SyncPhysicalCardsWithCombinedDeck → capture MoveToTopPopUpBatch
         └── return

Player clicks trigger BOOSTER
  └── Phase 2
         └── PlayRecorderAnimationsAndWait
                ├── Collect chain#2 (MoveToTopPopUpBatch)
                ├── Play MoveToTopPopUpBatch
                │      ├── ApplyAnimationResult → reorder list
                │      ├── UpdateAllPhysicalCardTargets → tween from current positions
                │      └── MoveCardToTopPopUpBatch → visible arc + slot-in
                └── finally → done
```

---

## 5. Files Changed

| # | File | Action | Lines of Change |
|---|------|--------|-----------------|
| 1 | `Assets/Scripts/Managers/CombatManager.cs` | Move `isPlayingEffectAnimations = false` to after `UpdateAllPhysicalCardTargets`; remove `afterShuffle` block from `RevealNextCard`; add guard to Round Start path; add relocated event in both Phase 1 paths | ~30 |

---

## 6. Regression Checklist

| # | Scenario | Verification Method | Expected Result |
|---|----------|--------------------:|-----------------|
| 1 | Start Card → shuffle → reveal BOOSTER with afterShuffle Stage | Play Mode, deck: `[start card, booster, dummy, dummy]` | Dummy cards do **not** silently tween before Stage animation; `MoveToTopPopUpBatch` arc animation is visible |
| 2 | Normal card after Start Card (no afterShuffle) | Play Mode, any deck without afterShuffle listener | Normal reveal flow unchanged; no delay or stutter |
| 3 | Rapid click during Shuffle / post-shuffle sync | Play Mode, spam click during and immediately after Shuffle | Input blocked by `isPlayingEffectAnimations` until `UpdateAllPhysicalCardTargets` completes |
| 4 | Combat restart / cleanup | Play Mode, finish combat then restart | No stale `_raiseAfterShuffleOnNextReveal` flag; next combat starts cleanly |
| 5 | Headless test compatibility | Run headless combat tests | `isPlayingEffectAnimations` guard does not deadlock headless flow |

---

## 7. Notes

- The `Debug.Log` lines added for investigation in `CombatManager.cs`, `StageEffect.cs`, `CombatUXManager.cs`, and `EffectChainManager.cs` should be commented out or removed before merging this fix to keep production logs clean.
- This PRD does **not** modify `StageEffect.SyncPhysicalCardsWithCombinedDeck()` call. The premature tween was caused by event timing, not by the sync call itself.
- `_raiseAfterShuffleOnNextReveal` is only ever set by `StartCardShuffleEffect`. After a Start Card shuffle, the **Round Start path** (`revealZone == null && cardsRevealedThisRound == 0`) is the primary path that executes, because `StartCardShuffleEffect.ExecuteShuffleEffect()` sets `cm.revealZone = null` and `HandleNewRoundStart()` resets `cardsRevealedThisRound = 0`. The Phase 1 auto-reveal path is defensive coverage for mid-round exile scenarios.
