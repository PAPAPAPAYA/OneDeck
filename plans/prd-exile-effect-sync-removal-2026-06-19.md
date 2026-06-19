# PRD: Remove Logic-Phase Deck Sync from ExileEffect

## 1. Problem Statement

`ExileEffect.ExileChosenCards` still calls `combatManager.visuals.SyncPhysicalCardsWithCombinedDeck()` at line 338 during the **logic phase**.

`BuryEffect` and `StageEffect` have already removed their logic-phase sync calls as part of the recorder-driven animation refactor. Keeping the sync inside `ExileEffect` is inconsistent with that direction and leaves a stale synchronization point that can subtly affect intermediate `physicalCardsInDeck` state during chained animations.

## 2. Goal

Remove the logic-phase `SyncPhysicalCardsWithCombinedDeck()` call from `ExileEffect` while preserving correct `PopUp + Destroy` animation playback and headless test behavior.

## 3. Current Code

File: `Assets/Scripts/Effects/ExileEffect.cs`

```csharp
private void ExileChosenCards(List<GameObject> cardsToExile, int amount)
{
    // ... cards removed from combinedDeckZone, revealZone cleared, events raised ...

    // Sync physical card list order with logical deck
    combatManager.visuals.SyncPhysicalCardsWithCombinedDeck();  // <-- line 338, remove this

    // Capture animation requests
    var recorderGo = EffectChainManager.Me != null ? EffectChainManager.Me.currentEffectRecorder : null;
    var recorder = recorderGo != null ? recorderGo.GetComponent<EffectRecorder>() : null;
    if (recorder != null && exiledCards.Count > 0)
    {
        for (int i = 0; i < exiledCards.Count; i++)
        {
            bool isLast = (i == exiledCards.Count - 1);

            recorder.animationRequests.Add(new AnimationRequest
            {
                type = AnimationRequestType.PopUp,
                targetCard = exiledCards[i]
            });

            recorder.animationRequests.Add(new AnimationRequest
            {
                type = AnimationRequestType.Destroy,
                targetCard = exiledCards[i],
                onComplete = isLast ? (Action)(() => combatManager.visuals.UpdateAllPhysicalCardTargets()) : null
            });
        }
    }
}
```

## 4. Proposed Change

Delete or comment out the single line:

```csharp
combatManager.visuals.SyncPhysicalCardsWithCombinedDeck();
```

Leave everything else unchanged:
- Keep `combinedDeckZone.Remove(targetCard)` in the logic phase.
- Keep `revealZone = null` handling.
- Keep `onFriendlyCardExiled` event raising.
- Keep `PopUp` + `Destroy` request capture.
- Keep the final `UpdateAllPhysicalCardTargets()` `onComplete` callback.

## 5. Why This Is Safe

`ExileEffect` only captures `AnimationRequestType.PopUp` followed by `AnimationRequestType.Destroy`. Neither request depends on the target card's index inside `physicalCardsInDeck`:

- `PopUpCard` uses the card's current world position, not its deck index.
- `ApplyAnimationResult(Destroy)` removes the physical card from `physicalCardsInDeck` before the destroy animation starts.
- `DestroyCardWithAnimation` has a fallback that scans all `CardPhysObjScript` instances if the dictionary lookup fails.
- The final `UpdateAllPhysicalCardTargets()` still runs after the last destroy, ensuring the remaining cards tween to their correct positions.

Compared with `BuryEffect`/`StageEffect`, the risk is lower because those effects rely on `SlotIn`/`SlotInBatch`, which query `physicalCardsInDeck.IndexOf(physicalCard)` and were therefore sensitive to premature reordering.

## 6. Impact Scope

### 6.1 Files Modified

| File | Change |
|------|--------|
| `Assets/Scripts/Effects/ExileEffect.cs` | Remove line 338 `SyncPhysicalCardsWithCombinedDeck()` call. Add a `VISUAL-FIX` comment block explaining the removal. |

### 6.2 Files Requiring Documentation Updates

| File | Update |
|------|--------|
| `AGENTS.md` | Update the paragraph that lists effects no longer calling `SyncPhysicalCardsWithCombinedDeck` to include `ExileEffect`. Note that `CardManipulationEffect` still needs evaluation. |
| `docs/RegressionChecklist.md` | Append a new row tracking this change (optional unless a regression is discovered). |

### 6.3 Test Impact

| Test File | Impact |
|-----------|--------|
| `Assets/Scripts/Editor/Tests/ExileEffectTests.cs` | No assertions on sync count; no changes needed. |
| `Assets/Scripts/Editor/Tests/VisualsCallTests.cs` | `ExileEffect_CapturesDestroyRequest` only asserts request types/count; no changes needed. |

## 7. Risks and Regression Verification

| Risk | Explanation | Verification |
|------|-------------|--------------|
| Exiling the currently revealed card | `revealZone` is cleared in logic phase, but `physicalCardInRevealZone` is only cleared inside `DestroyCardWithAnimation`. | Reveal a card, then exile it. Confirm `PopUp` starts from the reveal zone and `Destroy` completes normally; next reveal continues without freeze. |
| `SlotInBatch` on the same target earlier in the chain | If another effect captured `SlotInBatch` for a card that is later exiled, the slot-in animation may play before the exile `PopUp`. | Build a deck where `GiveStatusEffect` targets a card and `Exile` removes the same card in the same chain. Verify no freeze and visuals are acceptable. |
| Legacy path (`recorder == null`) | No animation requests are captured; `physicalCardsInDeck` will not be updated until the next sync. This matches the current behavior of refactored `BuryEffect`/`StageEffect` and is acceptable. | Confirm with headless tests that logical deck state is still correct after exile. |
| `DestroyCardWithAnimation` dictionary lookup | If cache is invalidated between `ApplyAnimationResult` and `DestroyCardWithAnimation`, the fallback scan still finds the physical card. | Verify in Play Mode that exiled cards are destroyed and do not linger as invisible objects. |

## 8. Recommended Implementation Order

1. Remove `SyncPhysicalCardsWithCombinedDeck()` from `ExileEffect.ExileChosenCards`.
2. Add a `VISUAL-FIX(YYYY-MM-DD)` comment block above the animation capture section referencing this PRD.
3. Run all Edit Mode tests (`ExileEffectTests`, `VisualsCallTests`, `ReactiveChainTests`).
4. Run Play Mode regression:
   - Any `ExileSelf` / `ExileMyCards` / `ExileTheirCards` card.
   - Exiling the currently revealed card.
   - A chain where `GiveStatusEffect` and `Exile` affect the same target.
5. Update `AGENTS.md` to reflect that `ExileEffect` no longer syncs in the logic phase.
6. Append a row to `docs/RegressionChecklist.md` if a new verification scenario is identified.

## 9. VISUAL-FIX Comment Template

Add the following comment block in `ExileEffect.cs` near the removed sync line:

```csharp
// VISUAL-FIX(2026-06-19): Remove logic-phase deck sync in Exile to align with Bury/Stage refactor
//   Cause:    SyncPhysicalCardsWithCombinedDeck in logic phase prematurely removes exiled cards
//             from physicalCardsInDeck, creating an inconsistent intermediate state during
//             chained animations and diverging from the recorder-driven animation model.
//   Fix:      Physical deck removal is deferred to RecorderAnimationPlayer via
//             ApplyAnimationResult(Destroy) during the animation phase.
//   Affects:  ExileEffect, RecorderAnimationPlayer, ApplyAnimationResult
//   Regress:  Reveal any Exile card, exile the currently revealed card, and chain
//             GiveStatusEffect -> Exile on the same target.
//   Related:  PRD exile-effect-sync-removal-2026-06-19
```

## 10. Acceptance Criteria

- [ ] `ExileEffect.cs` no longer calls `SyncPhysicalCardsWithCombinedDeck()` inside `ExileChosenCards`.
- [ ] A `VISUAL-FIX` comment block is present explaining the removal.
- [ ] All Edit Mode tests pass.
- [ ] Play Mode regression scenarios complete without visual freezes or incorrect card positions.
- [ ] `AGENTS.md` is updated to include `ExileEffect` in the list of effects that defer physical deck sync to the animation phase.
