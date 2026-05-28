# PRD: Start Card EffectRecorder Architecture Unification

## 1. Overview

### 1.1 Background

Currently, Start Card is a second-class citizen in the combat animation pipeline:

- **No EffectRecorder**: Normal cards create an `EffectRecorder` via `CostNEffectContainer.InvokeEffectEvent()` → `EffectChainManager.MakeANewEffectRecorder()`. Start Card bypasses this entirely — its shuffle logic is hard-coded in `CombatManager.TriggerStartCardEffect()`.
- **No Emphasize pulse**: `RecorderAnimationPlayer` plays a scale-pulse emphasis on `recorder.cardObject` before executing animations. Start Card never gets this visual signal.
- **Direct visual call**: Start Card calls `visuals.PlayShuffleAnimation()` directly, outside the `RecorderAnimationPlayer` scheduling system.
- **No CostNEffectContainer**: Start Card prefab only has `CardScript`. Normal cards have `CardScript + CostNEffectContainer + Effect`.
- **Dedicated private field**: `CombatManager` maintains `_startCardInstance` as a private cached reference, while normal cards are looked up from `combinedDeckZone`.

### 1.2 Goal

Unify Start Card into the **EffectRecorder → RecorderAnimationPlayer** animation pipeline, while preserving its intrinsic special properties:

| Property | Behavior | Preservation |
|----------|----------|--------------|
| No owner | `myStatusRef` / `theirStatusRef` are `null` | ✅ Keep `null` |
| Special physical prefab | Uses `startCardPhysicalPrefab` | ✅ Keep `isStartCard` check in `CombatUXManager` |
| Not from DeckSO | Created from `CombatManager.startCardPrefab` directly | ✅ Keep separate prefab reference |
| Event isolation | Does NOT trigger `onMeRevealed` / `onAnyCardRevealed` / `onHostileCardRevealed` | ✅ Keep isolation |

### 1.3 Design Rationale

- **Consistency**: One animation pipeline for all cards reduces maintenance surface and prevents visual desync bugs.
- **Composability**: By giving Start Card a `CostNEffectContainer`, future extensions (e.g. "Start Card also deals 1 fatigue damage") can be configured via prefab instead of hard-coding in `CombatManager`.
- **Visual parity**: Start Card deserves the same Emphasize pulse and recorder-driven scheduling as every other card.
- **Simpler state management**: Removing `_startCardInstance` eliminates a cached reference that can drift from `combinedDeckZone` reality.

---

## 2. Scope

### 2.1 In Scope

- New effect component: `StartCardShuffleEffect`
- New animation request type: `AnimationRequestType.Shuffle`
- `StartCard.prefab` structural refactoring
- `CombatManager` trigger flow refactoring
- `RecorderAnimationPlayer` Shuffle case handling
- `CombatUXManager.ApplyAnimationResult` Shuffle support
- Runtime lookup replacement for `_startCardInstance`

### 2.2 Out of Scope

- `ShuffleOrderOverride` logic (moved, not changed)
- `CombatUXManager.PlayStartCardShuffleAnimation` internal DOTween mechanics
- `AnimationStateTracker` behavior (legacy safety net, left untouched)
- Event system changes (Start Card still skips `onMeRevealed` etc.)
- `CardFactory.CreateStartCard` signature (remains, but remove redundant `isStartCard` assignment)

---

## 3. Technical Design

### 3.1 High-Level Flow

```
BEFORE (Current):
  Phase 2 → IsRevealedCardStartCard() → TriggerStartCardEffect()
    ├── hard-coded shuffle logic
    └── visuals.PlayShuffleAnimation()  [direct call, no recorder]

AFTER (Unified):
  Phase 2 → IsRevealedCardStartCard() → CostNEffectContainer.InvokeEffectEvent()
    ├── EffectChainManager.MakeANewEffectRecorder(startCard, shuffle_effect)
    ├── StartCardShuffleEffect.ExecuteShuffleEffect()
    │      ├── logic: return start card to deck
    │      ├── logic: shuffle deck
    │      └── capture: AnimationRequest(Shuffle) into recorder
    └── EffectChainManager.PopCurrentRecorder()

  PlayRecorderAnimationsAndWait()
    ├── CloseOpenedChain()
    └── RecorderAnimationPlayer.PlayRecordersCoroutine()
           └── PlayRecorderCoroutine(recorder)
                  ├── PlayEmphasizeAnimation(startCard)  [✅ NEW]
                  └── PlayRequestCoroutine(Shuffle)
                         ├── ApplyAnimationResult(Shuffle)
                         └── visuals.PlayShuffleAnimation()  [← same call, now scheduled]
                                └── onComplete → CombatManager.OnStartCardShuffleAnimationComplete()
```

### 3.2 New Component: `StartCardShuffleEffect`

**File**: `Assets/Scripts/Effects/StartCardShuffleEffect.cs`

**Responsibility**: Encapsulate the logic and animation capture that is currently hard-coded in `CombatManager.TriggerStartCardEffect()`.

```csharp
public class StartCardShuffleEffect : MonoBehaviour
{
    public void ExecuteShuffleEffect()
    {
        var cm = CombatManager.Me;
        var startCard = cm.revealZone;
        cm.revealZone = null;

        // Logic: return Start Card to deck
        cm.combinedDeckZone.Add(startCard);

        // Logic: shuffle (with Custom Shuffle Order support)
        var shuffleOverride = cm.GetComponent<ShuffleOrderOverride>();
        if (shuffleOverride != null && shuffleOverride.useCustomOrder
            && shuffleOverride.customOrderPrefabs != null
            && shuffleOverride.customOrderPrefabs.Count > 0)
        {
            cm.combinedDeckZone = cm.ApplyCustomShuffleOrder(
                cm.combinedDeckZone, shuffleOverride.customOrderPrefabs);
        }
        else
        {
            cm.combinedDeckZone = UtilityFuncManagerScript.ShuffleList(cm.combinedDeckZone);
        }

        // Logic: set post-shuffle flags (moved from TriggerStartCardEffect callback)
        cm.SetRaiseAfterShuffleOnNextReveal(true);
        cm.ResetShuffleTrackersPublic();

        // Capture animation request into current EffectRecorder
        var recorderGo = EffectChainManager.Me.currentEffectRecorder;
        if (recorderGo != null)
        {
            var recorder = recorderGo.GetComponent<EffectRecorder>();
            recorder.animationRequests.Add(new AnimationRequest
            {
                type = AnimationRequestType.Shuffle,
                sourceCard = startCard,
                targetCards = new List<GameObject>(cm.combinedDeckZone),
                onComplete = () => cm.OnStartCardShuffleAnimationComplete()
            });
        }
    }
}
```

**Key design decisions**:
- `SetRaiseAfterShuffleOnNextReveal(true)` and `ResetShuffleTrackersPublic()` are called **before** animation capture because they are logic-phase operations, not animation-phase.
- `onComplete` callback delegates to `CombatManager.OnStartCardShuffleAnimationComplete()` to keep lifecycle control in `CombatManager` while keeping the effect component stateless.

### 3.3 New Animation Request Type: `Shuffle`

**File**: `Assets/Scripts/Managers/AnimationRequest.cs`

**Enum addition**:

```csharp
public enum AnimationRequestType
{
    // ... existing types
    Shuffle
}
```

**Field addition**:

```csharp
public class AnimationRequest
{
    // ... existing fields
    public GameObject sourceCard; // Used for Shuffle request (Start Card instance)
}
```

**Field usage**:

| Field | Purpose for Shuffle |
|-------|---------------------|
| `sourceCard` | Start Card instance (flies from Reveal Zone) |
| `targetCards` | Shuffled deck order (used for `ApplyAnimationResult` sync) |
| `onComplete` | Callback to `CombatManager.OnStartCardShuffleAnimationComplete()` |

### 3.4 Prefab Refactoring: `StartCard.prefab`

**Current structure**:
```
StartCard (root)
  └── CardScript
```

**Target structure**:
```
StartCard (root)
  ├── CardScript
  └── shuffle_effect (new child GameObject)
         ├── CostNEffectContainer
         │      checkCostEvent: (empty)
         │      effectEvent: → StartCardShuffleEffect.ExecuteShuffleEffect()
         └── StartCardShuffleEffect
```

> This mirrors the normal card structure: `CardScript` + `CostNEffectContainer` + `Effect`.

### 3.5 `CombatManager` Refactoring

#### 3.5.1 Remove `_startCardInstance` cached field

```csharp
// REMOVE: private GameObject _startCardInstance;
```

#### 3.5.2 Add runtime lookup helper

```csharp
/// <summary>
/// Find Start Card instance in combinedDeckZone (or revealZone) by isStartCard flag.
/// Replaces the removed _startCardInstance cached reference.
/// </summary>
public GameObject FindStartCardInstance()
{
    // Check revealZone first (Start Card might be there during combat)
    if (revealZone != null)
    {
        var cs = revealZone.GetComponent<CardScript>();
        if (cs != null && cs.isStartCard)
            return revealZone;
    }
    foreach (var card in combinedDeckZone)
    {
        if (card == null) continue;
        var cs = card.GetComponent<CardScript>();
        if (cs != null && cs.isStartCard)
            return card;
    }
    return null;
}
```

#### 3.5.3 `GatherDecks()` — stop caching

**Before**:
```csharp
_startCardInstance = factory.CreateStartCard(startCardPrefab, playerDeckParent.transform);
if (_startCardInstance != null)
    combinedDeckZone.Add(_startCardInstance);
```

**After**:
```csharp
var startCardInstance = factory.CreateStartCard(startCardPrefab, playerDeckParent.transform);
if (startCardInstance != null)
    combinedDeckZone.Add(startCardInstance);
```

#### 3.5.4 `CleanupCombat()` — use lookup

**Before**:
```csharp
if (_startCardInstance != null)
{
    Destroy(_startCardInstance);
    _startCardInstance = null;
}
```

**After**:
```csharp
var startCard = FindStartCardInstance();
if (startCard != null)
    Destroy(startCard);
```

#### 3.5.5 Phase 2 trigger logic — direct CostNEffectContainer invocation

**Before**:
```csharp
if (IsRevealedCardStartCard())
{
    TriggerStartCardEffect();   // hard-coded shuffle + direct visual call
}
else
{
    TriggerRevealedCardEffect(); // normal event-driven flow
}
```

**After**:
```csharp
if (IsRevealedCardStartCard())
{
    // Start Card does NOT trigger onMeRevealed/onAnyCardRevealed/onHostileCardRevealed.
    // It goes through CostNEffectContainer to create an EffectRecorder,
    // but skips the normal reveal-event broadcast.
    var container = revealZone.GetComponentInChildren<CostNEffectContainer>();
    container?.InvokeEffectEvent();
}
else
{
    TriggerRevealedCardEffect();
}
```

**Why this preserves event isolation**:
- `TriggerRevealedCardEffect()` is where `onAnyCardRevealed.Raise()`, `onMeRevealed.RaiseSpecific()`, and `onHostileCardRevealed.RaiseOwner/RaiseOpponent()` live.
- By calling `GetComponentInChildren<CostNEffectContainer>()` directly, Start Card bypasses all reveal-event broadcasting.
- It still creates an `EffectRecorder` via `InvokeEffectEvent()` → `MakeANewEffectRecorder()`, so the animation pipeline is unified.

#### 3.5.6 Remove `TriggerStartCardEffect()` method

All logic has been migrated to `StartCardShuffleEffect`. Delete the entire method.

#### 3.5.7 Add `OnStartCardShuffleAnimationComplete()`

```csharp
/// <summary>
/// Called by RecorderAnimationPlayer via AnimationRequest.onComplete
/// after Start Card shuffle animation finishes.
/// </summary>
public void OnStartCardShuffleAnimationComplete()
{
    _infoDisplayer.RefreshDeckInfo();
    HandleNewRoundStart();
}
```

> Note: `_raiseAfterShuffleOnNextReveal = true` and `ResetShuffleTrackers()` are now called in `StartCardShuffleEffect.ExecuteShuffleEffect()` (logic phase), not in this callback (animation phase).

#### 3.5.8 Expose internal setters

```csharp
public void SetRaiseAfterShuffleOnNextReveal(bool value) => _raiseAfterShuffleOnNextReveal = value;
public void ResetShuffleTrackersPublic() => ResetShuffleTrackers();
```

### 3.6 `RecorderAnimationPlayer` — Shuffle Case

**File**: `Assets/Scripts/Managers/RecorderAnimationPlayer.cs`

Add inside `PlayRequestCoroutine` switch:

```csharp
case AnimationRequestType.Shuffle:
{
    // Sync physical deck order before animation starts
    visuals.ApplyAnimationResult(request);

    bool done = false;
    visuals.PlayShuffleAnimation(request.sourceCard, request.targetCards, () =>
    {
        done = true;
        if (request.onComplete != null) request.onComplete();
    });
    yield return new WaitUntil(() => done);
    break;
}
```

### 3.7 `CombatUXManager.ApplyAnimationResult` — Shuffle Support

**File**: `Assets/Scripts/UXPrototype/CombatUXManager.cs`

Add inside `ApplyAnimationResult` switch:

```csharp
case AnimationRequestType.Shuffle:
    // Update physicalCardsInDeck order to match shuffled logical order.
    // Actual transform movement is handled by PlayShuffleAnimationInternal.
    RebuildPhysicalDeckFromShuffledList(request.targetCards);
    break;
```

**Why `RebuildPhysicalDeckFromShuffledList` is safe here**:
- It only updates the `physicalCardsInDeck` List<GameObject> order.
- It does NOT move transforms.
- `PlayShuffleAnimationInternal` calculates target positions and drives DOTween separately.

**Note on double rebuild**: `ApplyAnimationResult(Shuffle)` rebuilds the list **before** the animation starts so that sibling cards tween to the correct post-shuffle positions in parallel. `PlayStartCardShuffleAnimation` rebuilds the list **again** in its `onComplete` callback as a final-state confirmation. Both rebuilds are intentional and safe.

---

## 4. Timing & State Analysis

### 4.1 Two-Phase Consistency

Start Card now follows the same two-phase model as all other effects:

| Phase | Normal Card (e.g. HPAlterEffect) | Start Card (new) |
|-------|-----------------------------------|------------------|
| **Logic Phase** | Deal damage, capture `Attack` request | Return to deck, shuffle, capture `Shuffle` request |
| **Animation Phase** | `RecorderAnimationPlayer` plays `Attack` tween | `RecorderAnimationPlayer` plays `Shuffle` tween |
| **Post-Animation** | `onHit` / `onComplete` callbacks | `OnStartCardShuffleAnimationComplete()` |

### 4.2 Input Blocking

```
CombatManager.Phase2
  ├── StartCardShuffleEffect.ExecuteShuffleEffect()  [logic, instant]
  └── StartCoroutine(PlayRecorderAnimationsAndWait())
         ├── isPlayingEffectAnimations = true
         ├── while (AnimationStateTracker.HasActiveBatch)  [← legacy safety, usually false here]
         ├── EffectChainManager.CloseOpenedChain()
         ├── RecorderAnimationPlayer.PlayRecordersCoroutine()
         │      └── PlayShuffleAnimation()  [← internally calls BlockInput/UnblockInput]
         └── finally: ResetInputBlock(), isPlayingEffectAnimations = false
```

`CombatUXManager.PlayStartCardShuffleAnimation` internally calls `BlockInput(this)` / `UnblockInput(this)` (reference-counted). `ResetInputBlock()` in `PlayRecorderAnimationsAndWait` is a safety-net `finally` that will not conflict because reference counting handles nested pairs correctly.

### 4.3 Emphasize Animation

`RecorderAnimationPlayer.PlayRecorderCoroutine` checks:

```csharp
if (recorder.animationRequests.Count > 0 && recorder.cardObject != null)
{
    yield return StartCoroutine(PlayEmphasizeAnimation(recorder.cardObject));
}
```

- Start Card recorder has 1 `Shuffle` request → **passes count check**.
- `MakeANewEffectRecorder(startCard, shuffle_effect)` sets `recorder.cardObject = startCard` → **passes null check**.
- Result: Start Card gets the same 1.2x scale pulse as every other card. ✅

---

## 5. Files Changed

| # | File | Action | Lines of Change |
|---|------|--------|-----------------|
| 1 | `Assets/Scripts/Effects/StartCardShuffleEffect.cs` | **Create new** | ~50 |
| 2 | `Assets/Scripts/Managers/AnimationRequest.cs` | Append enum member | ~1 |
| 3 | `Assets/Scripts/Managers/CombatManager.cs` | Refactor Phase 2, remove `TriggerStartCardEffect`, remove `_startCardInstance`, add helpers | ~80 |
| 4 | `Assets/Scripts/Managers/RecorderAnimationPlayer.cs` | Add `Shuffle` switch case | ~15 |
| 5 | `Assets/Scripts/UXPrototype/CombatUXManager.cs` | Add `Shuffle` to `ApplyAnimationResult` | ~5 |
| 6 | `Assets/Scripts/Managers/AnimationRequest.cs` | Add `sourceCard` field | ~1 |
| 7 | `Assets/Scripts/Managers/CardFactory.cs` | Remove redundant `isStartCard = true` assignment | ~3 |
| 8 | `Assets/Prefabs/Cards/System/StartCard.prefab` | Add `shuffle_effect` child with `CostNEffectContainer` + `StartCardShuffleEffect` | Unity Inspector |

---

## 6. Regression Checklist

After implementation, verify the following scenarios:

| # | Scenario | Verification Method | Expected Result |
|---|----------|--------------------:|-----------------|
| 1 | Start Card reveal → shuffle → new round | Play Mode, any deck with Start Card | Shuffle animation plays; round counter increments; deck is reshuffled; no error logs |
| 2 | Start Card Emphasize pulse | Play Mode, slow motion or frame step | Start Card plays 1.2x scale pulse before shuffle animation begins |
| 3 | Start Card input blocking | Play Mode, spam click during shuffle | Input is blocked during shuffle; unblocks after animation completes |
| 4 | Custom Shuffle Order | Play Mode with `ShuffleOrderOverride` component | Custom order is respected; animation reflects custom order |
| 5 | Normal card after Start Card | Play Mode, reveal cards after round restart | Normal cards still create EffectRecorder, play Emphasize, and execute animations correctly |
| 6 | Combat restart / cleanup | Play Mode, finish combat then restart | `CleanupCombat()` destroys Start Card via `FindStartCardInstance()`; no null reference |
| 7 | Headless test compatibility | Run headless combat tests | `FindStartCardInstance()` works without `_startCardInstance`; tests pass |
| 8 | Start Card staged/delayed then revealed again | Play Mode, apply Stage/Delay to Start Card | Next round Start Card still triggers shuffle via `InvokeEffectEvent()`; `EffectCanBeInvoked` does not block it |
| 9 | `FindStartCardInstance` across combat phases | Play Mode, log the lookup at key moments | Returns correct instance whether Start Card is in deck or revealZone; no leakage after cleanup |

---

## 7. Open Questions / Follow-ups

| # | Question | Status |
|---|----------|--------|
| 1 | Should `CardFactory.CreateStartCard` remove the redundant `cardScript.isStartCard = true` assignment since prefab already has it? | **Included in PRD** (Section 5, File 7) |
| 2 | If future designs want Start Card to trigger `onMeRevealed`, should we add a separate `GameEvent` binding to the prefab instead of changing `CombatManager`? | **Out of scope** — current design preserves event isolation |
| 3 | Does `CombatStatsLogger.OnCardRevealed` need to skip Start Card to avoid polluting win-rate stats? | **No change needed** — `RevealNextCard` already calls it for all revealed cards including Start Card. If stats should exclude Start Card, that is a separate analytics PRD. |
| 4 | Will `CostNEffectContainer.EffectCanBeInvoked(effectString)` block Start Card if its `effectString` is empty or unexpected? | **Verify before implementation** — `InvokeEffectEvent()` calls `EffectCanBeInvoked` before executing `effectEvent`. If this guard can reject Start Card, either configure a valid `effectString` on the prefab or bypass the check for Start Card. |
| 5 | Does `CostNEffectContainer` auto-resolve `myCardScript` via `GetComponentInParent<CardScript>()` when placed as a child of Start Card, or does it need an Inspector reference? | **Verify before prefab edit** — Check `CostNEffectContainer.Awake()` / `Start()` to confirm initialization logic. If it requires a manual reference, include it in the prefab editing steps. |
