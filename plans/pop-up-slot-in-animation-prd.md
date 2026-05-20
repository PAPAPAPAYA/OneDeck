# Pop Up + Slot In Animation — PRD

## 1. Overview

### 1.1 Goal
Add a **Pop Up + Slot In** animation pair to the existing EffectRecorder-driven animation system.

- **Pop Up**: A physical card lifts vertically from its deck position (like a toaster ejecting toast), briefly scales up, and moves to the visual foreground so the player can clearly read which card is being affected.
- **Slot In**: The same card descends back to its correct deck position (like a card being inserted into a slot), scaling back to deck size.

These two animations are played **around** other effect animations to provide visual clarity on *which* card is being added, exiled, buffed, or debuffed.

### 1.2 Four Usage Scenarios

| # | Scenario | Current Behavior | New Behavior |
|---|----------|-----------------|--------------|
| A | **Add Card** | New card spawns at `physicalCardNewTempCardPos` and tweens directly into deck slot. | New card spawns, **Pop Up** from spawn position, holds while player sees it, then **Slot In** to deck. |
| B | **Exile** | Card is removed from deck list and plays `Destroy` (move to grave + shrink). | Card **Pop Up** first so player sees what is being exiled, then plays `Destroy` from the elevated position. |
| C | **Status Effect Projectile** | Projectile flies from giver to target; target only gets tint. | Target card **Pop Up** when projectile starts, stays elevated during flight, then **Slot In** after projectile completes. |
| D | **Status Effect Consume** | Target card tint changes instantly. | Target card **Pop Up**, tint changes while elevated, then **Slot In**. |

### 1.3 Design Rationale

In the current stacked-deck visual, multiple cards overlap heavily. When a card is buried, staged, exiled, or receives a status effect, the player often cannot tell *which* specific card was affected. Pop Up forces the card to the visual foreground, holds it there during the critical moment, then gracefully returns it.

---

## 2. Animation Definition

### 2.1 Pop Up

```
Start: Card at current deck position (or spawn position for new cards)
  |
  v  (0.25s, Ease.OutQuad)
Peak: Card translated by +Y (popUpYOffset) and +Z toward camera (popUpZBoost)
      Scale = physicalCardDeckSize * popUpScaleMultiplier
      isPlayingSpecialAnimation = true  (blocks UpdateAllPhysicalCardTargets)
  |
  |  (HOLD — duration determined by what happens between PopUp and SlotIn)
  |
  v  (triggered by SlotIn)
End: Card descends to final deck position
```

**Parameters** (configurable on CombatUXManager):
- `popUpYOffset` = 1.5f  — vertical lift distance
- `popUpZBoost` = -1.0f  — move toward camera (smaller Z = closer/frontmost)
- `popUpScaleMultiplier` = 1.15f  — temporary scale increase
- `popUpDuration` = 0.25f  — time to reach peak
- `popUpEase` = Ease.OutQuad  — fast start, gentle deceleration

**Rules**:
- Pop Up **must** set `isPlayingSpecialAnimation = true` on the physical card so that `UpdateAllPhysicalCardTargets()` does not pull it back to its deck slot prematurely.
- Pop Up **must** kill any existing DOTween tweens on the physical card before starting (to avoid fighting with AddPhysicalCardToDeck's auto-tween or lingering deck position tweens).
- Pop Up **must** compute the target peak position based on the card's **current world position** at the moment of playback, not a cached deck index. This ensures correctness even if the deck shifts between logic-phase capture and animation-phase playback.

### 2.2 Slot In

```
Start: Card at Pop Up peak position (or any current elevated position)
  |
  v  (0.35s, Ease.InOutQuad)
End: Card at correct deck index position
      Scale = physicalCardDeckSize
      isPlayingSpecialAnimation = false  (releases to UpdateAllPhysicalCardTargets)
      TargetPosition / TargetScale are set so the card's own Update() tween takes over
```

**Parameters**:
- `slotInDuration` = 0.35f
- `slotInEase` = Ease.InOutQuad
- `slotInScaleRestore` = true  — always restore to `physicalCardDeckSize`

**Rules**:
- Slot In **must** calculate the final deck position by calling `CalculatePositionAtIndex()` with the card's **current** index in `physicalCardsInDeck`.
- Slot In **must** call `physScript.SetTargetPosition(finalPos)` and `physScript.SetTargetScale(finalScale)` before releasing `isPlayingSpecialAnimation`, so the card's ongoing `Update()` tween smoothly maintains position.
- Slot In **must** restore deck focus if `IsDeckFocused == true` (reuse existing `RestoreDeckFocusCoroutine()` logic, or defer to RecorderAnimationPlayer's existing focus restoration).

### 2.3 Combined Sequence (Typical)

```
[Pop Up]  ---->  [Other Animation(s)]  ---->  [Slot In]
   |                    |                       |
0.25s              variable                  0.35s
```

- **Scenario A (Add Card)**: Pop Up -> (hold 0.2s) -> Slot In
- **Scenario B (Exile)**: Pop Up -> (hold 0.1s) -> Destroy
- **Scenario C (Projectile)**: Pop Up -> StatusEffectProjectile (0.4s+stagger) -> Slot In
- **Scenario D (Consume)**: Pop Up -> StatusEffectChange (tint + particles, ~0.3s) -> Slot In

---

## 3. System Architecture Changes

### 3.1 New AnimationRequestTypes

Add to `Assets/Scripts/Managers/AnimationRequest.cs`:

```csharp
public enum AnimationRequestType
{
    // ... existing types ...
    PopUp,      // Lift card from current deck position to pop-up peak
    SlotIn      // Return card from pop-up peak to correct deck position
}
```

No new fields are required on `AnimationRequest` itself; PopUp and SlotIn reuse `targetCard`. Default durations/easing are read from `CombatUXManager` at playback time.

### 3.2 ICombatVisuals Extension

Add to `Assets/Scripts/Managers/ICombatVisuals.cs`:

```csharp
/// <summary>
/// Pop Up a card from its current position so the player can see it clearly.
/// Sets isPlayingSpecialAnimation=true. Card remains at peak until SlotIn is called.
/// </summary>
void PopUpCard(GameObject logicalCard, Action onComplete = null);

/// <summary>
/// Slot In a card from its pop-up position back to its correct deck position.
/// Clears isPlayingSpecialAnimation and syncs target position/scale.
/// </summary>
void SlotInCard(GameObject logicalCard, Action onComplete = null);
```

### 3.3 CombatUXManager Implementation

Add `[Header("POP UP / SLOT IN")]` settings and implement the two new interface methods.

**`PopUpCard`** implementation sketch:

```csharp
public void PopUpCard(GameObject logicalCard, Action onComplete = null)
{
    if (logicalCard == null) { onComplete?.Invoke(); return; }
    
    var cardScript = logicalCard.GetComponent<CardScript>();
    if (cardScript == null) { onComplete?.Invoke(); return; }
    
    BuildCardScriptToPhysicalDictionary();
    var physicalCard = GetPhysicalCardFromLogicalCard(cardScript);
    if (physicalCard == null) { onComplete?.Invoke(); return; }
    
    var physScript = physicalCard.GetComponent<CardPhysObjScript>();
    if (physScript == null) { onComplete?.Invoke(); return; }
    
    // Kill existing tweens to prevent conflicts
    physScript.KillTweens();
    
    // Compute peak position from CURRENT world position
    Vector3 currentPos = physicalCard.transform.position;
    Vector3 peakPos = currentPos + Vector3.up * popUpYOffset;
    peakPos.z += popUpZBoost;  // toward camera
    
    Vector3 peakScale = physicalCardDeckSize * popUpScaleMultiplier;
    
    physScript.isPlayingSpecialAnimation = true;
    AnimationStateTracker.me?.RegisterAnimation();
    BlockInput(this);
    
    Sequence seq = DOTween.Sequence();
    seq.Append(physicalCard.transform.DOMove(peakPos, popUpDuration).SetEase(popUpEase));
    seq.Join(physicalCard.transform.DOScale(peakScale, popUpDuration).SetEase(popUpEase));
    seq.OnComplete(() =>
    {
        AnimationStateTracker.me?.CompleteAnimation();
        UnblockInput(this);
        onComplete?.Invoke();
    });
    seq.Play();
}
```

**`SlotInCard`** implementation sketch:

```csharp
public void SlotInCard(GameObject logicalCard, Action onComplete = null)
{
    if (logicalCard == null) { onComplete?.Invoke(); return; }
    
    var cardScript = logicalCard.GetComponent<CardScript>();
    if (cardScript == null) { onComplete?.Invoke(); return; }
    
    BuildCardScriptToPhysicalDictionary();
    var physicalCard = GetPhysicalCardFromLogicalCard(cardScript);
    if (physicalCard == null) { onComplete?.Invoke(); return; }
    
    var physScript = physicalCard.GetComponent<CardPhysObjScript>();
    if (physScript == null) { onComplete?.Invoke(); return; }
    
    // Find current deck index
    int deckIndex = physicalCardsInDeck.IndexOf(physicalCard);
    if (deckIndex < 0)
    {
        // Card not in deck (e.g. reveal zone) — skip slot-in
        physScript.isPlayingSpecialAnimation = false;
        onComplete?.Invoke();
        return;
    }
    
    Vector3 targetPos = CalculatePositionAtIndex(deckIndex);
    
    AnimationStateTracker.me?.RegisterAnimation();
    BlockInput(this);
    
    Sequence seq = DOTween.Sequence();
    seq.Append(physicalCard.transform.DOMove(targetPos, slotInDuration).SetEase(slotInEase));
    seq.Join(physicalCard.transform.DOScale(physicalCardDeckSize, slotInDuration).SetEase(slotInEase));
    seq.OnComplete(() =>
    {
        physScript.isPlayingSpecialAnimation = false;
        physScript.SetTargetPosition(targetPos);
        physScript.SetTargetScale(physicalCardDeckSize);
        AnimationStateTracker.me?.CompleteAnimation();
        UnblockInput(this);
        onComplete?.Invoke();
    });
    seq.Play();
}
```

### 3.4 RecorderAnimationPlayer Extension

In `Assets/Scripts/Managers/RecorderAnimationPlayer.cs`, add two new switch cases inside `PlayRequestCoroutine`:

```csharp
case AnimationRequestType.PopUp:
{
    bool done = false;
    visuals.PopUpCard(request.targetCard, () => { done = true; if (request.onComplete != null) request.onComplete(); });
    yield return new WaitUntil(() => done);
    break;
}
case AnimationRequestType.SlotIn:
{
    bool done = false;
    visuals.SlotInCard(request.targetCard, () => { done = true; if (request.onComplete != null) request.onComplete(); });
    yield return new WaitUntil(() => done);
    break;
}
```

**Deck Focus Guard**: PopUp and SlotIn are NOT deck-move types per se, but they involve cards leaving/returning to deck positions. If `combatUX.IsDeckFocused == true` when either request plays, the existing focus restoration in RecorderAnimationPlayer (for MoveToBottomBatch etc.) should already have run earlier in the same recorder. If PopUp/SlotIn are the *first* requests in a recorder and the deck is focused, we should restore focus before PopUp. Add the same focus-restoration guard for `PopUp` and `SlotIn` as for the move types.

---

## 4. Integration Points — Per Scenario

### 4.1 Scenario A: Add Card (AddTempCard → CardFactory)

**Current flow**:
1. `AddTempCard.AddCardToMe()` calls `CombatFuncs.AddCard_TargetSpecific()`
2. `CardFactory.SpawnCardForPlayer()` → `SpawnCardToDeck()` creates logical + physical card
3. `CombatUXManager.AddPhysicalCardToDeck()` spawns physical card at `physicalCardNewTempCardPos` and immediately tweens it to deck position

**New flow**:
1. Same as above, but `AddPhysicalCardToDeck` must **not** auto-tween the new card when the call originates from an active effect chain.
2. `AddTempCard` (or `CardFactory`) captures `PopUp` + `SlotIn` into the current `EffectRecorder`.

**Minimal-change implementation**:
- Modify `AddPhysicalCardToDeck`: after inserting the new card into `physicalCardsInDeck` at index 0, check `EffectChainManager.Me?.currentEffectRecorder != null`.
  - If **inside an effect chain**: do **NOT** call `SetTargetPosition` / `SetTargetScale` for the new card. Leave it at the spawn position.
  - If **outside an effect chain** (e.g. initial deck setup): keep existing tween behavior.
- Modify `AddTempCard.AddCardToMe()` / `AddCardToThem()` / `AddSelfToMe()` / `AddSelfToThem()`:
  - After `CombatFuncs.me.AddCard_TargetSpecific(...)`, capture PopUp + SlotIn:
    ```csharp
    var recorderGo = EffectChainManager.Me?.currentEffectRecorder;
    var recorder = recorderGo?.GetComponent<EffectRecorder>();
    if (recorder != null)
    {
        // PopUp on the newly added card
        recorder.animationRequests.Add(new AnimationRequest {
            type = AnimationRequestType.PopUp,
            targetCard = newCard  // the returned card from AddCard_TargetSpecific
        });
        // SlotIn after a brief hold (the hold is implicit because RecorderAnimationPlayer
        // plays requests sequentially; we add an empty delay or just let the next request follow)
        recorder.animationRequests.Add(new AnimationRequest {
            type = AnimationRequestType.SlotIn,
            targetCard = newCard
        });
    }
    ```

**Note**: `AddCard_TargetSpecific` returns the logical `GameObject`. We should capture PopUp/SlotIn for each created card.

### 4.2 Scenario B: Exile (ExileEffect)

**Current flow**:
- `ExileChosenCards()` removes cards from `combinedDeckZone`, syncs physical cards, then captures `Destroy` requests.

**New flow**:
- Before each `Destroy` request, insert a `PopUp` request for the same card.
- The `Destroy` animation will then play from the elevated pop-up position.

Change in `ExileEffect.cs`:
```csharp
if (recorder != null && exiledCards.Count > 0)
{
    for (int i = 0; i < exiledCards.Count; i++)
    {
        bool isLast = (i == exiledCards.Count - 1);
        
        // 1. Pop Up so player sees the card
        recorder.animationRequests.Add(new AnimationRequest
        {
            type = AnimationRequestType.PopUp,
            targetCard = exiledCards[i]
        });
        
        // 2. Destroy from the elevated position
        recorder.animationRequests.Add(new AnimationRequest
        {
            type = AnimationRequestType.Destroy,
            targetCard = exiledCards[i],
            onComplete = isLast ? (Action)(() => combatManager.visuals.UpdateAllPhysicalCardTargets()) : null
        });
    }
}
```

**Visual note**: `DestroyCardWithAnimation` already removes the card from `physicalCardsInDeck` and moves it to `gravePosition`. If the card is at the pop-up position when Destroy starts, the destroy sequence will move from that elevated position toward the grave. This is acceptable — the card visibly leaves the deck area.

### 4.3 Scenario C: Status Effect Projectile (CurseEffect)

**Current flow**:
- `ApplyPowerToCardWithProjectile()` applies logic immediately, then captures a single `StatusEffectProjectile` request.

**New flow**:
- Capture three requests in order: `PopUp` → `StatusEffectProjectile` → `SlotIn`.

Change in `CurseEffect.cs`:
```csharp
public void ApplyPowerToCardWithProjectile(CardScript targetCard, int amount)
{
    if (targetCard == null || amount <= 0) return;

    ApplyPowerToCardInternal(targetCard, amount);

    var recorderGo = EffectChainManager.Me != null ? EffectChainManager.Me.currentEffectRecorder : null;
    var recorder = recorderGo != null ? recorderGo.GetComponent<EffectRecorder>() : null;
    if (recorder != null)
    {
        // 1. Pop Up target card
        recorder.animationRequests.Add(new AnimationRequest
        {
            type = AnimationRequestType.PopUp,
            targetCard = targetCard.gameObject
        });
        
        // 2. Play projectile while card is elevated
        recorder.animationRequests.Add(new AnimationRequest
        {
            type = AnimationRequestType.StatusEffectProjectile,
            attackerCard = myCard,
            targetCard = targetCard.gameObject
        });
        
        // 3. Slot In after projectile completes
        recorder.animationRequests.Add(new AnimationRequest
        {
            type = AnimationRequestType.SlotIn,
            targetCard = targetCard.gameObject
        });
    }
}
```

### 4.4 Scenario D: Status Effect Consume (ConsumeStatusEffect)

**Current flow**:
- `ConsumeOwnStatusEffect()` and `ConsumeRandomEnemyCardsStatusEffect()` call `CaptureStatusEffectChangeAnimationRequest()` which adds a single `StatusEffectChange` request.

**New flow**:
- For consumption that targets cards in the deck, capture `PopUp` → `StatusEffectChange` → `SlotIn`.

**Decision**: Not *all* status-effect changes need PopUp/SlotIn. For example, applying Infected via a direct effect (not projectile) might not need it. The user specifically asked for:
- Status effect projectile **target** → PopUp + SlotIn (Scenario C, handled in CurseEffect)
- Status effect **consumed** → PopUp + SlotIn (Scenario D, handled in ConsumeStatusEffect)

Change in `ConsumeStatusEffect.cs`:
```csharp
// In ConsumeOwnStatusEffect, after removing status effects:
CapturePopUpStatusEffectChangeSlotIn(myCardScript.gameObject, statusEffectToConsume, -amountRemoved);

// In ConsumeRandomEnemyCardsStatusEffect, inside the loop:
CapturePopUpStatusEffectChangeSlotIn(targetCard.gameObject, statusEffectToConsume, -1);
```

Add helper in `EffectScript.cs` (base class):
```csharp
protected void CapturePopUpStatusEffectChangeSlotIn(GameObject targetCard, EnumStorage.StatusEffect effect, int amount)
{
    var recorderGo = EffectChainManager.Me?.currentEffectRecorder;
    var recorder = recorderGo?.GetComponent<EffectRecorder>();
    if (recorder == null) return;
    
    // 1. Pop Up
    recorder.animationRequests.Add(new AnimationRequest
    {
        type = AnimationRequestType.PopUp,
        targetCard = targetCard
    });
    
    // 2. Status Effect Change (tint + particles)
    recorder.animationRequests.Add(new AnimationRequest
    {
        type = AnimationRequestType.StatusEffectChange,
        targetCard = targetCard,
        statusEffect = effect,
        statusEffectAmount = amount
    });
    
    // 3. Slot In
    recorder.animationRequests.Add(new AnimationRequest
    {
        type = AnimationRequestType.SlotIn,
        targetCard = targetCard
    });
}
```

**Important**: `CaptureStatusEffectChangeAnimationRequest` is still used by other effects (e.g. `TransferStatusEffectEffect`, `ManaAlterEffect`) that do NOT need PopUp/SlotIn. Do NOT change the existing helper; add the new `CapturePopUpStatusEffectChangeSlotIn` helper instead.

---

## 5. Configuration Parameters

Add to `CombatUXManager` Inspector:

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `popUpYOffset` | float | 1.5f | Vertical lift distance (world units) |
| `popUpZBoost` | float | -1.0f | Z offset toward camera (negative = closer/frontmost) |
| `popUpScaleMultiplier` | float | 1.15f | Scale multiplier at peak |
| `popUpDuration` | float | 0.25f | Time to reach peak position |
| `popUpEase` | Ease | OutQuad | Easing for pop-up movement |
| `slotInDuration` | float | 0.35f | Time to return to deck position |
| `slotInEase` | Ease | InOutQuad | Easing for slot-in movement |

---

## 6. Edge Cases & Compatibility

| ID | Case | Handling |
|----|------|----------|
| EC-1 | Card is in reveal zone when PopUp is called | PopUp still works (uses current world position). SlotIn should detect `deckIndex < 0` and simply release `isPlayingSpecialAnimation` without moving. |
| EC-2 | Card is destroyed between PopUp and SlotIn | SlotIn should defensively check `logicalCard == null` or `physicalCard == null` and exit gracefully. |
| EC-3 | Deck is focused/peeled during PopUp/SlotIn | RecorderAnimationPlayer already restores deck focus before MoveToBottomBatch/etc. Add the same guard for PopUp and SlotIn. |
| EC-4 | PopUp called on a card already playing special animation | `PopUpCard` kills existing tweens (`KillTweens()`), so it safely overrides. `isPlayingSpecialAnimation` stays true. |
| EC-5 | Multiple PopUps on same card in same recorder | Each PopUp overwrites the previous tween. Final SlotIn brings it back once. |
| EC-6 | NullCombatVisuals (headless tests) | PopUp/SlotIn are no-ops (same pattern as other ICombatVisuals methods). No crash. |
| EC-7 | AddPhysicalCardToDeck without active effect recorder | Keep existing auto-tween behavior so initialization and non-effect add paths still work. |
| EC-8 | SlotIn after Destroy | Should not happen if integration is correct (Destroy is the last request for an exiled card). If it does, SlotIn detects missing physical card and exits. |

---

## 7. Implementation Order

### Phase 1: Core Infrastructure (Files: 4)

| # | Task | File |
|---|------|------|
| 1.1 | Add `PopUp` and `SlotIn` to `AnimationRequestType` enum | `Assets/Scripts/Managers/AnimationRequest.cs` |
| 1.2 | Add `PopUpCard()` and `SlotInCard()` to `ICombatVisuals` | `Assets/Scripts/Managers/ICombatVisuals.cs` |
| 1.3 | Implement `PopUpCard()` and `SlotInCard()` in `CombatUXManager` + add config fields | `Assets/Scripts/UXPrototype/CombatUXManager.cs` |
| 1.4 | Add `PopUp` and `SlotIn` switch cases in `RecorderAnimationPlayer.PlayRequestCoroutine` | `Assets/Scripts/Managers/RecorderAnimationPlayer.cs` |

### Phase 2: Effect Integration (Files: 4)

| # | Task | File | What changes |
|---|------|------|-------------|
| 2.1 | Add `CapturePopUpStatusEffectChangeSlotIn` helper to `EffectScript` | `Assets/Scripts/Effects/EffectScript.cs` | New protected helper method |
| 2.2 | Modify `AddPhysicalCardToDeck` to skip auto-tween when inside effect chain | `Assets/Scripts/UXPrototype/CombatUXManager.cs` | Check `EffectChainManager.Me?.currentEffectRecorder != null` |
| 2.3 | Capture PopUp + SlotIn in `AddTempCard` methods | `Assets/Scripts/Effects/AddTempCard.cs` | After `AddCard_TargetSpecific`, capture requests |
| 2.4 | Insert PopUp before Destroy in `ExileEffect` | `Assets/Scripts/Effects/ExileEffect.cs` | Two requests per exiled card: PopUp then Destroy |
| 2.5 | Change `CurseEffect.ApplyPowerToCardWithProjectile` to capture PopUp + Projectile + SlotIn | `Assets/Scripts/Effects/CurseEffect.cs` | Three requests instead of one |
| 2.6 | Change `ConsumeStatusEffect` to use new helper | `Assets/Scripts/Effects/StatusEffect/ConsumeStatusEffect.cs` | Replace `CaptureStatusEffectChangeAnimationRequest` with `CapturePopUpStatusEffectChangeSlotIn` for deck-targeted consumption |

### Phase 3: Testing (Files: 1-2)

| # | Task | Method |
|---|------|--------|
| 3.1 | Play Mode test: Add Card with PopUp+SlotIn | Strategy B: reveal a card that adds temp cards; verify console logs show PopUp then SlotIn requests |
| 3.2 | Play Mode test: Exile with PopUp+Destroy | Strategy B: reveal an exile card; verify physical card lifts before shrinking |
| 3.3 | Play Mode test: Status Effect Projectile | Strategy B: reveal a curse card that enhances curse; verify target pops up during projectile |
| 3.4 | Play Mode test: Status Effect Consume | Strategy B: reveal a card that consumes status effect; verify target pops up, tint changes, then slots in |
| 3.5 | Edge case: headless fallback | Run with `NullCombatVisualsBehaviour`; verify no exception and logic resolves correctly |

---

## 8. Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| PopUp conflicts with existing `AddPhysicalCardToDeck` auto-tween | Medium | High | Phase 2.2 explicitly suppresses auto-tween when inside effect chain |
| `isPlayingSpecialAnimation` left true after SlotIn interruption | Low | Medium | `SlotInCard` always releases flag in `OnComplete`; `StopAllSpecialAnimations()` is emergency override |
| Z-order fighting: popped-up card still behind other cards | Low | High | `popUpZBoost` moves card toward camera; value should be larger than max deck z-offset |
| Performance: extra sequential requests lengthen animation phase | Low | Low | PopUp/SlotIn are short (0.25s + 0.35s); total impact per effect is <1s |
| Regression: existing effects that use `CaptureStatusEffectChangeAnimationRequest` accidentally changed | Medium | High | Do NOT modify existing helper; create new helper `CapturePopUpStatusEffectChangeSlotIn` |

---

## 9. SOP Updates Required

| Document | Update |
|----------|--------|
| `AGENTS.md` | Add `PopUp` and `SlotIn` to AnimationRequestType list; document new usage scenarios |
| `.agents/skills/unity-card-playmode-test/SKILL.md` | Add test scenario for PopUp+SlotIn animation verification |

---

## 10. Open Questions

1. **Should ALL card additions use PopUp+SlotIn, or only mid-combat additions?**
   - Decision: Only mid-combat additions (AddTempCard) use PopUp+SlotIn. Initial deck setup (`InstantiateAllPhysicalCards`) should remain instant to avoid a slow start-of-combat sequence.

2. **Should the reveal zone card ever PopUp?**
   - Decision: Yes. PopUp is not restricted to deck cards. Any card with a physical representation can pop up, including the reveal zone card. This is useful when the revealed card itself is about to be exiled or receives a status effect. SlotIn gracefully handles reveal-zone cards by releasing `isPlayingSpecialAnimation` without moving (see §6 EC-1).

3. **Should PopUp play a sound effect?**
   - Decision: Out of scope for this PRD. Audio can be added later in `CombatUXManager.PopUpCard` if desired.
