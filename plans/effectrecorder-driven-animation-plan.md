# EffectRecorder-Driven Animation System — Implementation Plan

## 1. Overview

### 1.1 Goal
Replace the current **animation-reference-counter-driven event delay** (`AnimationStateTracker`) with an **EffectRecorder-tree-driven sequential animation playback** system.

All effect logic executes synchronously during a **Logic Phase**, building a complete `EffectRecorder` tree with captured animation requests. After the tree is fully constructed, an **Animation Phase** traverses the tree in effect-instance-boundary interleaved order and plays animations sequentially.

### 1.2 Concrete Result
Using the user's example:
- Deck: `Spike_Skeleton` (bottom), `Start_Card`, `Eternal_Ghost`, `Grave_Punch` (top)
- `Grave_Punch` revealed, triggers `BuryEffect` + `HPAlterEffect`
- Animation sequence:
  1. Spike_Skeleton bury animation (parallel if multiple cards)
  2. Spike_Skeleton attack animation (triggered by `onMeBuried`)
  3. Eternal_Ghost attack animation (triggered by Spike_Skeleton)
  4. Grave_Punch attack animation
  5. Eternal_Ghost attack animation (triggered by Grave_Punch)

### 1.3 Two-Phase Execution Model

```
Player Click -> TriggerRevealedCardEffect()
    |
    v
[LOGIC PHASE] — synchronous, no animations
    |
    ├── Event listeners fire synchronously
    ├── Effects create EffectRecorders + capture AnimationRequest(s)
    ├── Deck state changes immediately
    ├── HP/shield changes immediately (damage resolved in logic phase)
    └── Tree fully built
    |
    v
CombatManager waits for AnimationStateTracker idle, then CloseOpenedChain()
    |
    v
[ANIMATION PHASE] — sequential, blocks input
    |
    ├── Effect-instance-boundary interleaved traversal of EffectRecorder tree
    ├── Each recorder's AnimationRequests played to completion
    ├── Batch movements start in parallel
    └── Input restored when all animations complete
```

---

## 2. Architecture

### 2.1 New/Modified Components

```
┌─────────────────────────────────────────────────────────────────────┐
│                     RecorderAnimationPlayer (NEW)                    │
│  Singleton. Owns animation phase.                                    │
│                                                                      │
│  PlayRecordersCoroutine(List<GameObject> rootRecorders)              │
│  └── iterates roots in closedEffectRecorders order                   │
│      └── PlayRecorderCoroutine(EffectRecorder)                       │
│          └── effect-instance-boundary interleave:                    │
│              plays all AnimationRequests in current recorder,        │
│              then recurses into unplayed direct children             │
│              (by Transform sibling order)                            │
│                                                                      │
│  PlayRequestCoroutine(AnimationRequest)                              │
│  └── dispatches to ICombatVisuals                                    │
│      └── batch types start all movements in parallel                 │
│      └── calls UpdateAllPhysicalCardTargets() before batch moves     │
└─────────────────────────────────────────────────────────────────────┘
                              ▲
                              │ called by
┌─────────────────────────────────────────────────────────────────────┐
│                     CombatManager (MODIFIED)                         │
│                                                                      │
│  RevealCards() Phase 2:                                              │
│  └── StartCoroutine(PlayRecorderAnimationsAndWait())                 │
│      ├── Wait for AnimationStateTracker idle                         │
│      ├── CloseOpenedChain()                                          │
│      ├── Collect root recorders from closedEffectRecorders           │
│      ├── Yield PlayRecordersCoroutine(roots)                         │
│      └── Mark all recorders as animationPlayed=true                  │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              │ creates/updates
┌─────────────────────────────────────────────────────────────────────┐
│                     EffectChainManager (PRESERVED)                   │
│                                                                      │
│  - Keep SameCardDifferentObject chain closing                        │
│  - Keep chainDepth field (flat chain logic)                          │
│  - Keep openedEffectRecorders loop guard                             │
│  - Tree links via Transform parent-child (existing behavior)         │
│  - recorderStack push/pop (existing behavior)                        │
│  - On CloseOpenedChain(): close only, do NOT trigger playback        │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              │ creates
┌─────────────────────────────────────────────────────────────────────┐
│                     EffectRecorder (MODIFIED)                        │
│  MonoBehaviour on recorder prefab                                    │
│                                                                      │
│  Existing: sessionID, chainID, processedEffectID, cardObject,        │
│            effectObject, open                                        │
│  New:      List<AnimationRequest> animationRequests                  │
│            bool animationPlayed                                      │
│                                                                      │
│  Tree navigation: via Transform hierarchy (transform.parent /        │
│  transform.GetChild) — no explicit C# parent/child fields            │
└─────────────────────────────────────────────────────────────────────┘
                              ▲
                              │ captures into
┌─────────────────────────────────────────────────────────────────────┐
│  HPAlterEffect, BuryEffect, StageEffect, etc. (MODIFIED)             │
│  Capture AnimationRequest(s) instead of calling visuals directly     │
│  Damage resolved immediately in logic phase (onHit = null)           │
└─────────────────────────────────────────────────────────────────────┘
```

### 2.2 AnimationRequest Data Model

```csharp
public enum AnimationRequestType
{
    Attack,
    MoveToBottom,
    MoveToBottomBatch,
    MoveToTop,
    MoveToTopBatch,
    MoveToIndex
}

public class AnimationRequest
{
    public AnimationRequestType type;

    // ── Attack ──
    public GameObject attackerCard;
    public bool isAttackingEnemy;
    public Action onHit;          // null — damage already resolved in logic phase
    public Action onComplete;

    // ── Single Move ──
    public GameObject targetCard;
    public float duration = 0.5f;
    public bool useArc = true;

    // ── Batch Move ──
    public List<GameObject> targetCards; // for MoveToBottomBatch / MoveToTopBatch

    // ── MoveToIndex ──
    public int targetIndex;
}
```

**Design rationale for batch types**: `MoveToBottomBatch` and `MoveToTopBatch` carry a `List<GameObject> targetCards` so that `RecorderAnimationPlayer` can start all card movements simultaneously and yield until the last one completes. This preserves the visual feel of the old system where multiple bury/stage animations ran in parallel.

### 2.3 EffectRecorder Structure

```csharp
public class EffectRecorder : MonoBehaviour
{
    // ── Existing ──
    public int sessionID;
    public int chainID;
    public string processedEffectID;
    public GameObject cardObject;
    public GameObject effectObject;
    public bool open = true;

    // ── NEW: Animation Capture ──
    public List<AnimationRequest> animationRequests = new List<AnimationRequest>();

    // ── NEW: Playback State ──
    public bool animationPlayed = false;
}
```

**Tree navigation**: The recorder tree uses the existing Transform parent-child hierarchy already created by `EffectChainManager.MakeANewEffectRecorder`. Root nodes are identified by `transform.parent == EffectChainManager.Me.transform`. No explicit `parentRecorder` / `childRecorders` C# fields are added.

### 2.4 Tree Traversal Order

`RecorderAnimationPlayer` uses **effect-instance-boundary interleave**:

1. Play **all** `AnimationRequest`s in the current recorder sequentially.
2. Then recurse into any **unplayed** direct children (by Transform sibling order).

This ensures all animations produced by the same effect instance are played contiguously, while reactive effects triggered by that instance are animated afterward.

---

## 3. Implementation Phases

### Phase 1: Core Infrastructure (Files: 4)

**Goal**: Build the foundation. EffectRecorders can capture animation requests, and the animation player can traverse the existing Transform tree.

| # | Task | File |
|---|------|------|
| 1.1 | Add `AnimationRequestType` enum and `AnimationRequest` class | `Assets/Scripts/Managers/AnimationRequest.cs` (new) |
| 1.2 | Add `animationRequests` and `animationPlayed` fields to `EffectRecorder` | `Assets/Scripts/Managers/EffectRecorder.cs` |
| 1.3 | Create `RecorderAnimationPlayer` singleton with interleaved traversal | `Assets/Scripts/Managers/RecorderAnimationPlayer.cs` (new) |
| 1.4 | Ensure `RecorderAnimationPlayer` singleton exists at runtime | `Assets/Scripts/Managers/CombatManager.cs` |

**RecorderAnimationPlayer responsibilities (1.3)**:
- `Awake()` sets up singleton `me`.
- `PlayRecordersCoroutine(List<GameObject> rootRecorders)` — iterates roots in `closedEffectRecorders` order, wrapping playback in `AttackAnimationManager.HoldDeckFocus()` / `ReleaseDeckFocus()`.
- `PlayRecorderCoroutine(EffectRecorder)` — effect-instance-boundary interleave: plays all `AnimationRequest`s in the current recorder sequentially, then recurses into unplayed direct children by Transform sibling order.
- `PlayRequestCoroutine(AnimationRequest)` — dispatches to `ICombatVisuals`. For batch types, starts all movements in parallel and yields until the last completes. For all Move types, calls `UpdateAllPhysicalCardTargets()` **before** starting the movement.

**CombatManager changes (1.4)**:
- In `Awake()`: dynamically create a GameObject with `RecorderAnimationPlayer` component if `me == null`.
- In `RevealCards()`, Phase 2: replace `StartCoroutine(WaitForAttackAnimationsBeforeNextReveal())` with `StartCoroutine(PlayRecorderAnimationsAndWait())`.
- New coroutine `PlayRecorderAnimationsAndWait()`:
  1. Safety wait: `while (AnimationStateTracker.me != null && AnimationStateTracker.me.HasActiveBatch) yield return null;`
  2. Close the chain: `EffectChainManager.Me.CloseOpenedChain();`
  3. Collect root recorders from `closedEffectRecorders` (Transform.parent == EffectChainManager, `animationPlayed == false`).
  4. If `RecorderAnimationPlayer.me` exists and roots > 0: yield `PlayRecordersCoroutine(roots)`.
  5. In `try-finally`: mark all recorders in `closedEffectRecorders` as `animationPlayed = true` and call `ResetInputBlock()`.
  6. Afterward, yield `WaitForAttackAnimationsBeforeNextReveal()` as safety net.

### Phase 2: Effect Intent Capture (Files: 3)

**Goal**: Convert key effects from "play animation immediately" to "capture AnimationRequest(s)."

| # | Task | File | What changes |
|---|------|------|-------------|
| 2.1 | `HPAlterEffect` capture attack intent | `Assets/Scripts/Effects/HPAlterEffect.cs` | `DecreaseTheirHp()`, `DecreaseMyHp()`: move `ProcessDamage()` + `CheckDmgTargets_*()` to **logic phase** (before capture); capture `AnimationRequest` with `onHit = null` |
| 2.2 | `BuryEffect` capture batch move intent + immediate event raise | `Assets/Scripts/Effects/BuryEffect.cs` | `BuryChosenCards()`: raise events immediately; capture single `MoveToBottomBatch` request; remove `UpdateAllPhysicalCardTargets()` from logic phase |
| 2.3 | `StageEffect` capture batch move intent + immediate event raise | `Assets/Scripts/Effects/StageEffect.cs` | Same pattern as BuryEffect: capture `MoveToTopBatch` |

**HPAlterEffect pattern (2.1)**:
```csharp
public void DecreaseTheirHp()
{
    DmgCalculator();
    int totalDmg = extraDmg + dmgAmountAlter;

    if (isStatusEffectDamage)
    {
        ProcessDamage(totalDmg, myCardScript.theirStatusRef);
        CheckDmgTargets_DealingDmgToOpponent(totalDmg);
        dmgAmountAlter = 0;
        return;
    }

    bool isAttackingEnemy = myCardScript.theirStatusRef != combatManager.ownerPlayerStatusRef;

    // 1. Resolve damage IMMEDIATELY in logic phase
    ProcessDamage(totalDmg, myCardScript.theirStatusRef);
    CheckDmgTargets_DealingDmgToOpponent(totalDmg);

    // 2. Capture animation request
    var recorderGo = EffectChainManager.Me != null ? EffectChainManager.Me.currentEffectRecorder : null;
    var recorder = recorderGo != null ? recorderGo.GetComponent<EffectRecorder>() : null;
    if (recorder != null && RecorderAnimationPlayer.me != null)
    {
        recorder.animationRequests.Add(new AnimationRequest {
            type = AnimationRequestType.Attack,
            attackerCard = myCard,
            isAttackingEnemy = isAttackingEnemy,
            onHit = null, // damage already resolved
            onComplete = null
        });
    }
    else
    {
        // Fallback: old immediate visual path
        combatManager.RaiseDamageDealtEvent(myCard, isAttackingEnemy, onHit: null, onComplete: null);
    }

    dmgAmountAlter = 0;
}
```

**BuryEffect pattern (2.2)**:
```csharp
private void BuryChosenCards(List<GameObject> cardsToBury, int amount)
{
    // ... existing deck modification logic ...

    // Sync physical cards immediately
    combatManager.visuals.SyncPhysicalCardsWithCombinedDeck();

    // DO NOT call UpdateAllPhysicalCardTargets() here — deferred to RecorderAnimationPlayer

    // Raise events IMMEDIATELY (logic phase)
    foreach (var buriedCard in buriedCards)
    {
        GameEventStorage.me.onMeBuried.RaiseSpecific(buriedCard);
        GameEventStorage.me.onAnyCardBuried.Raise();
        // ... faction events ...
    }

    // Capture a single batch animation request
    var recorderGo = EffectChainManager.Me != null ? EffectChainManager.Me.currentEffectRecorder : null;
    var recorder = recorderGo != null ? recorderGo.GetComponent<EffectRecorder>() : null;
    if (recorder != null && RecorderAnimationPlayer.me != null)
    {
        recorder.animationRequests.Add(new AnimationRequest {
            type = AnimationRequestType.MoveToBottomBatch,
            targetCards = buriedCards,
            duration = 0.5f,
            useArc = true
        });
    }
    else
    {
        // Fallback: old immediate visual calls
    }
}
```

### Phase 3: Animation Playback Integration (Files: 3)

**Goal**: `RecorderAnimationPlayer` can actually execute the captured requests using existing visual infrastructure.

| # | Task | File |
|---|------|------|
| 3.1 | Implement `PlayRequestCoroutine` switch that delegates to existing visual methods | `Assets/Scripts/Managers/RecorderAnimationPlayer.cs` |
| 3.2 | Ensure `AttackAnimationManager` queue works with recorder-driven playback | `Assets/Scripts/Managers/AttackAnimationManager.cs` |
| 3.3 | Ensure `CombatUXManager` move methods work with deferred target updates | `Assets/Scripts/UXPrototype/CombatUXManager.cs` |

**AnimationRequest execution sketch**:
```csharp
private IEnumerator PlayRequestCoroutine(AnimationRequest request)
{
    switch (request.type)
    {
        case AnimationRequestType.Attack:
            yield return PlayAttackRequest(request);
            break;
        case AnimationRequestType.MoveToBottom:
            yield return PlayMoveToBottomRequest(request);
            break;
        case AnimationRequestType.MoveToBottomBatch:
            yield return PlayMoveToBottomBatchRequest(request);
            break;
        case AnimationRequestType.MoveToTop:
            yield return PlayMoveToTopRequest(request);
            break;
        case AnimationRequestType.MoveToTopBatch:
            yield return PlayMoveToTopBatchRequest(request);
            break;
        case AnimationRequestType.MoveToIndex:
            yield return PlayMoveToIndexRequest(request);
            break;
    }
}

private IEnumerator PlayAttackRequest(AnimationRequest request)
{
    bool done = false;
    CombatManager.Me.visuals.PlayAttackAnimation(
        request.attackerCard,
        request.isAttackingEnemy,
        onHit: () => { request.onHit?.Invoke(); },
        onComplete: () => { request.onComplete?.Invoke(); done = true; }
    );
    yield return new WaitUntil(() => done);
}

private IEnumerator PlayMoveToBottomBatchRequest(AnimationRequest request)
{
    // Update targets before moving so non-moving cards slide in parallel
    CombatManager.Me.visuals.UpdateAllPhysicalCardTargets();

    var coroutines = new List<Coroutine>();
    var completions = new List<bool>();
    for (int i = 0; i < request.targetCards.Count; i++)
    {
        completions.Add(false);
        int index = i;
        coroutines.Add(StartCoroutine(MoveSingleCard(request.targetCards[index], request.duration, request.useArc, () => completions[index] = true)));
    }
    yield return new WaitUntil(() => completions.TrueForAll(b => b));
}
```

**Input blocking**: Individual `ICombatVisuals` methods (`PlayAttackAnimation`, `MoveCardToBottom`, etc.) still manage their own `BlockInput`/`UnblockInput` calls, so player input remains blocked during the entire recorder playback sequence.

### Phase 4: Event System Cleanup (Files: 1)

**Goal**: Keep `AnimationStateTracker` as a safety net during transition.

| # | Task | File | Rationale |
|---|------|------|-----------|
| 4.1 | Keep `AnimationStateTracker` in scene, do NOT remove `TryExecute` wrapping | `Assets/Scripts/SOScripts/GameEvent.cs`, `AnimationStateTracker.cs` | The new coroutine explicitly waits for `HasActiveBatch == false` before closing the chain, ensuring delayed events flush naturally. Full removal can be a future Phase 5 after stability is proven. |

**Decision**: Do NOT modify `GameEvent.cs` in this iteration. `AnimationStateTracker` continues to run as a secondary guard. The new system works *alongside* it, not *instead of* it, during the transition period.

### Phase 5: Testing & Edge Cases (Files: 2)

| # | Task | File |
|---|------|------|
| 5.1 | Update `EffectChainTests` if needed | `Assets/Scripts/Editor/Tests/EffectChainTests.cs` |
| 5.2 | Update PlayMode test SOPs that reference `CloseOpenedChain()` behavior | `docs/StrategyB_PlayMode_SOP.md`, `.agents/skills/unity-card-playmode-test/SKILL.md` |

**Test scenarios**:
1. Single card reveal -> attack animation plays normally
2. Bury -> onMeBuried -> stage: animations play in bury -> stage order
3. Same card with two effects (e.g., attack + bury): both animations captured, played contiguously
4. Deep nesting (A -> B -> C -> D): animations play A -> B -> C -> D
5. Loop guard: same card+effect in opened chains blocks (existing behavior)
6. Combat end during animation: `RecorderAnimationPlayer` stops gracefully
7. Headless test: fallback path works when `RecorderAnimationPlayer.me == null`

---

## 4. File-by-File Change Detail

### 4.1 New Files

**`Assets/Scripts/Managers/AnimationRequest.cs`**
```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

public enum AnimationRequestType
{
    Attack,
    MoveToBottom,
    MoveToBottomBatch,
    MoveToTop,
    MoveToTopBatch,
    MoveToIndex
}

public class AnimationRequest
{
    public AnimationRequestType type;

    // Attack
    public GameObject attackerCard;
    public bool isAttackingEnemy;
    public Action onHit;
    public Action onComplete;

    // Single Move
    public GameObject targetCard;
    public float duration = 0.5f;
    public bool useArc = true;

    // Batch Move
    public List<GameObject> targetCards;

    // MoveToIndex
    public int targetIndex;
}
```

**`Assets/Scripts/Managers/RecorderAnimationPlayer.cs`**
```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RecorderAnimationPlayer : MonoBehaviour
{
    public static RecorderAnimationPlayer me;
    void Awake() { me = this; }

    public IEnumerator PlayRecordersCoroutine(List<GameObject> rootRecorders)
    {
        AttackAnimationManager.me?.HoldDeckFocus();
        try
        {
            foreach (var rootGo in rootRecorders)
            {
                var recorder = rootGo.GetComponent<EffectRecorder>();
                if (recorder != null && !recorder.animationPlayed)
                {
                    yield return PlayRecorderCoroutine(recorder);
                }
            }
        }
        finally
        {
            AttackAnimationManager.me?.ReleaseDeckFocus();
        }
    }

    private IEnumerator PlayRecorderCoroutine(EffectRecorder recorder)
    {
        if (recorder == null || recorder.animationPlayed) yield break;

        // Play all requests in this recorder sequentially
        foreach (var request in recorder.animationRequests)
        {
            yield return PlayRequestCoroutine(request);
        }

        recorder.animationPlayed = true;

        // Recurse into unplayed direct children by Transform sibling order
        int childCount = recorder.transform.childCount;
        for (int i = 0; i < childCount; i++)
        {
            var childRecorder = recorder.transform.GetChild(i).GetComponent<EffectRecorder>();
            if (childRecorder != null && !childRecorder.animationPlayed)
            {
                yield return PlayRecorderCoroutine(childRecorder);
            }
        }
    }

    private IEnumerator PlayRequestCoroutine(AnimationRequest request)
    {
        switch (request.type)
        {
            case AnimationRequestType.Attack:
                yield return PlayAttackRequest(request);
                break;
            case AnimationRequestType.MoveToBottom:
                yield return PlayMoveToBottomRequest(request);
                break;
            case AnimationRequestType.MoveToBottomBatch:
                yield return PlayMoveToBottomBatchRequest(request);
                break;
            case AnimationRequestType.MoveToTop:
                yield return PlayMoveToTopRequest(request);
                break;
            case AnimationRequestType.MoveToTopBatch:
                yield return PlayMoveToTopBatchRequest(request);
                break;
            case AnimationRequestType.MoveToIndex:
                yield return PlayMoveToIndexRequest(request);
                break;
        }
    }

    private IEnumerator PlayAttackRequest(AnimationRequest request)
    {
        bool done = false;
        CombatManager.Me.visuals.PlayAttackAnimation(
            request.attackerCard,
            request.isAttackingEnemy,
            onHit: () => { request.onHit?.Invoke(); },
            onComplete: () => { request.onComplete?.Invoke(); done = true; }
        );
        yield return new WaitUntil(() => done);
    }

    private IEnumerator PlayMoveToBottomRequest(AnimationRequest request)
    {
        bool done = false;
        CombatManager.Me.visuals.UpdateAllPhysicalCardTargets();
        CombatManager.Me.visuals.MoveCardToBottom(
            request.targetCard,
            request.duration,
            request.useArc,
            onComplete: () => { done = true; }
        );
        yield return new WaitUntil(() => done);
    }

    private IEnumerator PlayMoveToBottomBatchRequest(AnimationRequest request)
    {
        CombatManager.Me.visuals.UpdateAllPhysicalCardTargets();

        var completions = new List<bool>();
        for (int i = 0; i < request.targetCards.Count; i++)
        {
            completions.Add(false);
        }

        for (int i = 0; i < request.targetCards.Count; i++)
        {
            int index = i;
            CombatManager.Me.visuals.MoveCardToBottom(
                request.targetCards[index],
                request.duration,
                request.useArc,
                onComplete: () => { completions[index] = true; }
            );
        }

        yield return new WaitUntil(() => {
            foreach (var c in completions) if (!c) return false;
            return true;
        });
    }

    private IEnumerator PlayMoveToTopRequest(AnimationRequest request)
    {
        bool done = false;
        CombatManager.Me.visuals.UpdateAllPhysicalCardTargets();
        CombatManager.Me.visuals.MoveCardToTop(
            request.targetCard,
            request.duration,
            request.useArc,
            onComplete: () => { done = true; }
        );
        yield return new WaitUntil(() => done);
    }

    private IEnumerator PlayMoveToTopBatchRequest(AnimationRequest request)
    {
        CombatManager.Me.visuals.UpdateAllPhysicalCardTargets();

        var completions = new List<bool>();
        for (int i = 0; i < request.targetCards.Count; i++)
        {
            completions.Add(false);
        }

        for (int i = 0; i < request.targetCards.Count; i++)
        {
            int index = i;
            CombatManager.Me.visuals.MoveCardToTop(
                request.targetCards[index],
                request.duration,
                request.useArc,
                onComplete: () => { completions[index] = true; }
            );
        }

        yield return new WaitUntil(() => {
            foreach (var c in completions) if (!c) return false;
            return true;
        });
    }

    private IEnumerator PlayMoveToIndexRequest(AnimationRequest request)
    {
        bool done = false;
        CombatManager.Me.visuals.UpdateAllPhysicalCardTargets();
        CombatManager.Me.visuals.MoveCardToIndex(
            request.targetCard,
            request.targetIndex,
            request.duration,
            request.useArc,
            onComplete: () => { done = true; }
        );
        yield return new WaitUntil(() => done);
    }
}
```

### 4.2 Modified Files

**`Assets/Scripts/Managers/EffectRecorder.cs`**
- Add `using System.Collections.Generic;`
- Add fields:
  ```csharp
  public List<AnimationRequest> animationRequests = new List<AnimationRequest>();
  public bool animationPlayed = false;
  ```

**`Assets/Scripts/Managers/EffectChainManager.cs`**

**No changes required** for core chain logic. Keep:
- `SameCardDifferentObject` chain closing in `CheckShouldIStartANewChain`
- `chainDepth` field and its usage in `EffectCanBeInvoked`
- Flat `openedEffectRecorders` loop guard
- `recorderStack` push/pop
- Transform-based parenting in `MakeANewEffectRecorder`
- `CloseOpenedChain()` closes only, does not trigger playback

**`Assets/Scripts/Card/CostNEffectContainer.cs`**

**No changes required**. The existing `recorderStack` push/pop in `EffectChainManager` already handles nested recorder creation correctly.

**`Assets/Scripts/Effects/HPAlterEffect.cs`**

Modify `DecreaseTheirHp()` and `DecreaseMyHp()`:

```csharp
public void DecreaseTheirHp()
{
    DmgCalculator();
    int totalDmg = extraDmg + dmgAmountAlter;

    if (isStatusEffectDamage)
    {
        ProcessDamage(totalDmg, myCardScript.theirStatusRef);
        CheckDmgTargets_DealingDmgToOpponent(totalDmg);
        dmgAmountAlter = 0;
        return;
    }

    bool isAttackingEnemy = myCardScript.theirStatusRef != combatManager.ownerPlayerStatusRef;

    // Resolve damage immediately in logic phase
    ProcessDamage(totalDmg, myCardScript.theirStatusRef);
    CheckDmgTargets_DealingDmgToOpponent(totalDmg);

    // Capture animation request
    var recorderGo = EffectChainManager.Me != null ? EffectChainManager.Me.currentEffectRecorder : null;
    var recorder = recorderGo != null ? recorderGo.GetComponent<EffectRecorder>() : null;
    if (recorder != null && RecorderAnimationPlayer.me != null)
    {
        recorder.animationRequests.Add(new AnimationRequest {
            type = AnimationRequestType.Attack,
            attackerCard = myCard,
            isAttackingEnemy = isAttackingEnemy,
            onHit = null, // damage already resolved
            onComplete = null
        });
    }
    else
    {
        // Fallback: old immediate visual path
        combatManager.RaiseDamageDealtEvent(myCard, isAttackingEnemy, onHit: null, onComplete: null);
    }

    dmgAmountAlter = 0;
}
```

Same pattern for `DecreaseMyHp()`.

All `DecreaseTheirHp_BasedOn*` and `DecreaseTheirHpTimes*` variants ultimately call `DecreaseTheirHp()`, so they will automatically capture intent.

**`Assets/Scripts/Effects/BuryEffect.cs`**

Modify `BuryChosenCards()`:

```csharp
private void BuryChosenCards(List<GameObject> cardsToBury, int amount)
{
    amount = Mathf.Clamp(amount, 0, cardsToBury.Count);
    if (amount == 0) return;

    var buriedCards = new List<GameObject>();
    for (var i = 0; i < amount; i++)
    {
        var targetCard = cardsToBury[i];
        var targetCardScript = targetCard.GetComponent<CardScript>();

        if (_combinedDeck.Contains(targetCard))
        {
            _combinedDeck.Remove(targetCard);
            _combinedDeck.Insert(0, targetCard);
            buriedCards.Add(targetCard);

            // Track buried counts (existing logic)
            if (ValueTrackerManager.me != null)
            {
                if (targetCardScript.myStatusRef == combatManager.ownerPlayerStatusRef)
                {
                    if (ValueTrackerManager.me.ownerCardsBuriedCountRef != null)
                        ValueTrackerManager.me.ownerCardsBuriedCountRef.value++;
                }
                else
                {
                    if (ValueTrackerManager.me.enemyCardsBuriedCountRef != null)
                        ValueTrackerManager.me.enemyCardsBuriedCountRef.value++;
                }
            }

            // Log (existing logic)
            string myColor = GetMyCardColorTag();
            string targetColor = GetCardColorTag(targetCard);
            AppendLog("// [<color=" + myColor + ">" + myCard.gameObject.name + "</color>]将[<color=" + targetColor + ">" +
                targetCardScript.gameObject.name + "</color>]埋入牌库底端");
        }
    }

    // Sync physical cards to match logical deck immediately
    combatManager.visuals.SyncPhysicalCardsWithCombinedDeck();

    // DO NOT call UpdateAllPhysicalCardTargets() here

    // Capture animation intent
    var recorderGo = EffectChainManager.Me != null ? EffectChainManager.Me.currentEffectRecorder : null;
    var recorder = recorderGo != null ? recorderGo.GetComponent<EffectRecorder>() : null;
    if (recorder != null && RecorderAnimationPlayer.me != null && buriedCards.Count > 0)
    {
        recorder.animationRequests.Add(new AnimationRequest {
            type = AnimationRequestType.MoveToBottomBatch,
            targetCards = buriedCards,
            duration = 0.5f,
            useArc = true
        });
    }
    else
    {
        // Fallback: old immediate visual calls for each card
        foreach (var card in buriedCards)
        {
            combatManager.visuals.MoveCardToBottom(card, 0.5f, true, null);
        }
        combatManager.visuals.UpdateAllPhysicalCardTargets();
    }

    // Raise events IMMEDIATELY (logic phase)
    foreach (var buriedCard in buriedCards)
    {
        GameEventStorage.me.onMeBuried.RaiseSpecific(buriedCard);
        GameEventStorage.me.onAnyCardBuried.Raise();
        var cardStatus = buriedCard.GetComponent<CardScript>()?.myStatusRef;
        if (cardStatus != null && GameEventStorage.me.onFriendlyCardBuried != null)
        {
            if (cardStatus == combatManager.ownerPlayerStatusRef)
                GameEventStorage.me.onFriendlyCardBuried.RaiseOwner();
            else
                GameEventStorage.me.onFriendlyCardBuried.RaiseOpponent();
        }
    }
}
```

**`Assets/Scripts/Effects/StageEffect.cs`**
Same pattern as BuryEffect: immediate logical deck change, immediate event raise, capture `MoveToTopBatch` intent. Keep `SyncPhysicalCardsWithCombinedDeck()` in logic phase; remove `UpdateAllPhysicalCardTargets()` from logic phase.

**`Assets/Scripts/SOScripts/GameEvent.cs`**
**No changes**. Keep `AnimationStateTracker.TryExecute` wrapping intact.

**`Assets/Scripts/Managers/AnimationStateTracker.cs`**
**No changes**. Keep running as a safety net. The new `PlayRecorderAnimationsAndWait` coroutine explicitly waits for `HasActiveBatch == false` before closing the chain.

**`Assets/Scripts/Managers/CombatManager.cs`**

In `Awake()`:
```csharp
// Ensure RecorderAnimationPlayer singleton exists
if (RecorderAnimationPlayer.me == null)
{
    var go = new GameObject("RecorderAnimationPlayer");
    go.AddComponent<RecorderAnimationPlayer>();
}
```

In `RevealCards()`, Phase 2 (after `TriggerRevealedCardEffect()`):
- Remove the direct call to `EffectChainManager.Me.CloseOpenedChain()`.
- Replace `StartCoroutine(WaitForAttackAnimationsBeforeNextReveal())` with `StartCoroutine(PlayRecorderAnimationsAndWait())`.

New private coroutine `PlayRecorderAnimationsAndWait()`:
```csharp
private IEnumerator PlayRecorderAnimationsAndWait()
{
    // 1. Safety wait for legacy animations
    while (AnimationStateTracker.me != null && AnimationStateTracker.me.HasActiveBatch)
        yield return null;

    // 2. Close the chain
    EffectChainManager.Me.CloseOpenedChain();

    // 3. Collect root recorders
    var roots = new List<GameObject>();
    foreach (var rec in EffectChainManager.Me.closedEffectRecorders)
    {
        if (rec.transform.parent == EffectChainManager.Me.transform)
        {
            var recorder = rec.GetComponent<EffectRecorder>();
            if (recorder != null && !recorder.animationPlayed)
                roots.Add(rec);
        }
    }

    // 4. Play recorder animations
    if (RecorderAnimationPlayer.me != null && roots.Count > 0)
    {
        yield return RecorderAnimationPlayer.me.PlayRecordersCoroutine(roots);
    }

    // 5. Cleanup in try-finally
    try { }
    finally
    {
        foreach (var rec in EffectChainManager.Me.closedEffectRecorders)
        {
            var recorder = rec.GetComponent<EffectRecorder>();
            if (recorder != null)
                recorder.animationPlayed = true;
        }
        ResetInputBlock();
    }

    // 6. Safety net for stray legacy animations
    yield return StartCoroutine(WaitForAttackAnimationsBeforeNextReveal());
}
```

Also update `ExitCombat()`:
```csharp
// Clear and destroy all recorder GameObjects under EffectChainManager
if (EffectChainManager.Me != null)
{
    foreach (var rec in EffectChainManager.Me.closedEffectRecorders)
    {
        if (rec != null) Destroy(rec);
    }
    EffectChainManager.Me.closedEffectRecorders.Clear();

    // Also destroy any children under EffectChainManager transform
    for (int i = EffectChainManager.Me.transform.childCount - 1; i >= 0; i--)
    {
        Destroy(EffectChainManager.Me.transform.GetChild(i).gameObject);
    }
}
```

---

## 5. Data Flow Comparison

### Before (Current System)
```
Grave_Punch revealed
  -> onMeRevealed raised
     -> BuryEffect executes
        -> MoveCardToBottom(animation starts, pending=1)
        -> onMeBuried raised
           -> AnimationStateTracker queues onMeBuried (pending>0)
        -> BuryEffect returns
     -> HPAlterEffect executes
        -> RaiseDamageDealtEvent
           -> AttackAnimationManager queues attack
     -> CloseOpenedChain()

[Animation playing] Bury animation runs...
[Animation complete] pending=0
[Flush] onMeBuried executes
  -> Spike_Skeleton effect executes
     -> RaiseDamageDealtEvent
        -> AttackAnimationManager queues attack

[Attack queue] Grave_Punch attack plays...
[Attack queue] Spike_Skeleton attack plays...
```

**Result**: Animation order depends on DOTween timing, event flush cycles, and attack queue processing. Order is emergent.

### After (Prototype-Aligned System)
```
Grave_Punch revealed
  -> onMeRevealed raised (synchronous)
     -> BuryEffect executes
        -> Logical deck modified
        -> onMeBuried raised (synchronous)
           -> Spike_Skeleton effect executes
              -> ProcessDamage() + CheckDmgTargets() (immediate)
              -> Capture attack request (Recorder #3)
              -> onTheirPlayerTookDmg raised (synchronous)
                 -> Eternal_Ghost effect executes
                    -> ProcessDamage() + CheckDmgTargets() (immediate)
                    -> Capture attack request (Recorder #4)
        -> Capture bury batch request (Recorder #1)
     -> HPAlterEffect executes
        -> ProcessDamage() + CheckDmgTargets() (immediate)
        -> Capture attack request (Recorder #2)
        -> onTheirPlayerTookDmg raised (synchronous)
           -> Eternal_Ghost effect executes
              -> ProcessDamage() + CheckDmgTargets() (immediate)
              -> Capture attack request (Recorder #5)
     -> CombatManager.PlayRecorderAnimationsAndWait()
        -> Wait for AnimationStateTracker idle
        -> CloseOpenedChain()
        -> EffectChainManager.Me.closedEffectRecorders = [Recorder #1, #2, #3, #4, #5]
        -> Collect roots: Recorder #1
        -> RecorderAnimationPlayer.PlayRecordersCoroutine([#1])

[Animation Phase]
  1. Recorder #1: Bury animation (batch, parallel)
     └── Recorder #1 children: #3 (Spike attack), #4 (Eternal from Spike)
  2. Recorder #3: Spike_Skeleton attack
  3. Recorder #4: Eternal_Ghost attack (from Spike)
  4. Return to Recorder #1's siblings
  5. Recorder #2: Grave_Punch attack
     └── Recorder #2 children: #5 (Eternal from Grave)
  6. Recorder #5: Eternal_Ghost attack (from Grave_Punch)
```

**Result**: Deterministic effect-instance-boundary interleaved order. No race conditions. Damage resolved immediately in logic phase.

---

## 6. Risks & Mitigations

| # | Risk | Severity | Mitigation |
|---|------|----------|------------|
| 1 | Extensive file changes break existing combat flow | High | Implement in phases; test after each phase; keep `AnimationStateTracker` as fallback during transition |
| 2 | BuryEffect/StageEffect event timing change breaks card interactions | High | Move events to synchronous immediately; fallback paths preserve old behavior if recorder is missing |
| 3 | AttackAnimationManager queue + RecorderAnimationPlayer double-queue | Medium | `RecorderAnimationPlayer` calls `PlayAttackAnimation` which enqueues; attack queue processes internally. `RecorderAnimationPlayer` waits for `onComplete` which fires after the attack queue finishes. |
| 4 | Same-card parallel animations lost (attack + bury no longer simultaneous) | Medium | Same effect instance's requests play contiguously (attack then bury). If UX feels too slow, consider parallel sibling playback in future iteration. |
| 5 | Loop guard behavior unchanged | Low | Flat `openedEffectRecorders` check is preserved exactly as-is; no new loop patterns introduced |
| 6 | `CombatUXManager.MoveCardWithAnimation` relies on `AnimationStateTracker` | Low | `AnimationStateTracker` is kept running as safety net. `RecorderAnimationPlayer` uses `onComplete` callbacks for its own sequencing. |
| 7 | Input blocking gaps between animations allow player to click | Low | Individual `ICombatVisuals` methods manage Block/Unblock. `AttackAnimationManager.HoldDeckFocus` prevents focus drift during batch playback. |
| 8 | Combat end delayed because damage resolved in logic phase but checked too early | Low | Damage is resolved in Phase 2 (effect execution), combat end is checked in Phase 1 (next reveal). This is the existing flow and works correctly. |
| 9 | `closedEffectRecorders` accumulates objects across combat sessions | Medium | `CombatManager.ExitCombat()` destroys all recorder GameObjects and clears the list. |
| 10 | Headless tests break without RecorderAnimationPlayer | Low | Fallback condition `RecorderAnimationPlayer.me != null` ensures headless tests continue through old paths. |

---

## 7. Success Criteria

1. **Basic combat**: Reveal a card with simple damage effect -> attack animation plays normally
2. **Bury chain**: Card with bury effect targets another card -> bury animation plays first, then buried card's effect animation plays
3. **Multi-effect card**: Card with attack + bury -> both requests captured on same recorder, animations play contiguously
4. **Deep nesting**: A -> B -> C -> D chain -> animations play A -> B -> C -> D
5. **No regression**: Combat win/lose conditions, HP calculations, and combat log remain correct
6. **No soft-lock**: If animation system breaks, timeout releases input within 5 seconds
7. **Headless compatibility**: Tests without `RecorderAnimationPlayer` work through fallback paths

---

## 8. Recommended Execution Order

1. **Create branch** (if using version control)
2. **Phase 1** (Core Infrastructure): Create `AnimationRequest.cs`, `RecorderAnimationPlayer.cs`, modify `EffectRecorder.cs`, `CombatManager.cs`
3. **Test Phase 1**: Verify tree builds correctly, `PlayRecorderAnimationsAndWait` coroutine runs without error
4. **Phase 2** (Effect Capture): Modify `HPAlterEffect.cs`, `BuryEffect.cs`, `StageEffect.cs`
5. **Test Phase 2**: Verify combat log shows correct order, animations captured but not yet played via new system
6. **Phase 3** (Animation Playback): Ensure `RecorderAnimationPlayer` dispatches correctly to `ICombatVisuals`
7. **Test Phase 3**: Full combat flow — animations should play in effect-instance-boundary interleaved order
8. **Phase 4** (Conservative Cleanup): Verify `AnimationStateTracker` still works as safety net; no `GameEvent.cs` changes
9. **Phase 5** (Tests): Update unit tests and PlayMode SOPs if needed
10. **Full regression test**: Run full card test suite
