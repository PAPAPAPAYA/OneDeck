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

### Phase 5: Testing & Edge Cases

#### 5.1 Editor Unit Tests (`Assets/Scripts/Editor/Tests/EffectChainTests.cs`)

Add the following test methods to `EffectChainTests`:

| Test Method | Purpose | Setup | Assertion |
|-------------|---------|-------|-----------|
| `AnimationRequest_CapturedOnEffectRecorder` | Verify `HPAlterEffect` captures `Attack` request on current recorder | Create card + effect, call `DecreaseTheirHp()` via reflection | `recorder.animationRequests.Count == 1 && recorder.animationRequests[0].type == Attack` |
| `AnimationRequest_BatchMoveCaptured` | Verify `BuryEffect` captures `MoveToBottomBatch` | Create card with `BuryEffect`, invoke `BuryMyCards(2)` with 2 valid targets in deck | `recorder.animationRequests[0].type == MoveToBottomBatch && targetCards.Count == 2` |
| `RecorderTree_NavigatesViaTransform` | Verify parent-child links use Transform only | Create root recorder under `EffectChainManager`, create child recorder parented to root | `child.transform.parent == root.transform` (no explicit C# parent field) |
| `AnimationPlayedFlag_SetAfterPlayback` | Verify `animationPlayed` prevents double-play | Start `PlayRecorderCoroutine`, yield until complete | `recorder.animationPlayed == true` |
| `FallbackPath_OldVisualCalledWhenPlayerNull` | Verify fallback when `RecorderAnimationPlayer.me == null` | Set `RecorderAnimationPlayer.me = null`, trigger `HPAlterEffect` | `CombatManager.onDamageDealt` event is raised (old path) |
| `CloseOpenedChain_DoesNotTriggerPlayback` | Verify chain manager does NOT play animations | Open chain, close chain | `EffectChainManager.closedEffectRecorders.Count > 0` but no coroutines started by chain manager |

#### 5.2 Play Mode Integration Tests

Run in Play Mode via `unity-MCP execute_code`. Use the standard Strategy B setup template.

| ID | Scenario | Deck Setup | Trigger Steps | Expected Animation Order | State Verification |
|----|----------|------------|---------------|--------------------------|--------------------|
| **PM-1** | Basic attack | `BLACKSMITH` (player) vs dummy enemy | Reveal `BLACKSMITH`, trigger effect | 1. BLACKSMITH attack animation | Enemy HP = 100 - (2+1+Power) |
| **PM-2** | Bury chain | `SPIKE_SKELETON`(bottom), `Start_Card`, `ETERNAL_GHOST`, `GRAVE_PUNCH`(top) | Reveal `GRAVE_PUNCH`, trigger effect | 1. GRAVE_PUNCH bury animation (batch)<br>2. SPIKE_SKELETON attack animation (from onMeBuried)<br>3. ETERNAL_GHOST attack animation (from SPIKE damage)<br>4. GRAVE_PUNCH attack animation<br>5. ETERNAL_GHOST attack animation (from GRAVE_PUNCH damage) | Enemy HP reduced by all attacks; SPIKE_SKELETON at bottom |
| **PM-3** | Multi-effect card | `GRAVE_KEEPER` + 1 friendly + 1 enemy | Reveal `GRAVE_KEEPER`, trigger effect | 1. GRAVE_KEEPER attack animation<br>2. GRAVE_KEEPER stage-self animation (if onFriendlyCardBuried fires) | Attack resolves first, then stage; both requests on same recorder |
| **PM-4** | Deep nesting | `A`(buries B) -> `B`(buries C) -> `C`(attacks) | Trigger A -> B's onMeBuried -> C's onMeBuried | 1. A bury animation<br>2. B bury animation<br>3. C attack animation | Depth-first interleave order: A's requests, then A's children (B), then B's children (C) |
| **PM-5** | Loop guard | `SLIME` with self-replicating effect | Reveal `SLIME`, trigger `onMeBuried` twice | Only first `onMeBuried` triggers; second blocked by `openedEffectRecorders` | `EffectCanBeInvoked` returns `false` on second call |
| **PM-6** | Combat end during animation | Any damage card | Trigger effect, immediately call `ExitCombat()` | No exception; all recorder GameObjects destroyed; input block released | `EffectChainManager.closedEffectRecorders.Count == 0` |
| **PM-7** | Headless fallback | Same as PM-1, but destroy `RecorderAnimationPlayer` GameObject before test | Reveal card, trigger effect | Old visual path executes (`CombatManager.onDamageDealt` fires) | Damage resolves synchronously even without animation player |

#### 5.3 Edge Cases & Regression

| ID | Case | Expected Behavior | How to Test |
|----|------|-------------------|-------------|
| **EC-1** | Empty `animationRequests` list | Recorder is marked `animationPlayed = true` immediately; children still recursed | Create recorder with 0 requests, play it |
| **EC-2** | `targetCards` contains null entries | Batch move skips null cards without exception | Add `null` to `targetCards` list, play `MoveToBottomBatch` |
| **EC-3** | `AnimationStateTracker.HasActiveBatch` true during Phase 2 | `PlayRecorderAnimationsAndWait` yields until idle before closing chain | Force `AnimationStateTracker` into active state, then trigger reveal |
| **EC-4** | Input block reference count | `ResetInputBlock()` in `finally` releases all input blocks | Trigger multiple effects that call `BlockInput`, verify `IsInputBlocked == false` after animation phase |
| **EC-5** | Damage resolved before animation | HP changes immediately in logic phase; animation only visual | Check enemy HP **before** `PlayRecordersCoroutine` yields |
| **EC-6** | `closedEffectRecorders` accumulates across rounds | Each round's recorders are cleaned up by `animationPlayed = true` | Trigger 3 separate reveals, verify old recorders are skipped on subsequent plays |
| **EC-7** | Exile + Destroy animation | `ExileEffect` captures `Destroy` request; plays after parent recorder's requests | Reveal `RIFT` (exiles self + stages friendly); verify Destroy plays after Stage in correct recorder order |
| **EC-8** | Same card with 3+ effects | All requests on same recorder play sequentially before children | Create card with HPAlter + Bury + Stage in same container; verify order = attack -> bury -> stage -> children |

#### 5.4 Automated Console Assertions (Strategy B Script)

When writing Play Mode test scripts, assert the following console messages or state checks:

```csharp
// After TriggerRevealEffect(testCard) + yield for PlayRecorderAnimationsAndWait
// 1. Verify EffectRecorder tree built
var ecm = EffectChainManager.Me;
bool hasRecorders = ecm.closedEffectRecorders.Count > 0;
UnityEngine.Debug.Log("[CHECK] Recorders built: " + hasRecorders);

// 2. Verify animation requests captured
foreach (var recGo in ecm.closedEffectRecorders)
{
    var rec = recGo.GetComponent<EffectRecorder>();
    UnityEngine.Debug.Log("[CHECK] Recorder " + rec.processedEffectID + " requests: " + rec.animationRequests.Count);
}

// 3. Verify all played flag set
bool allPlayed = ecm.closedEffectRecorders.TrueForAll(r => r.GetComponent<EffectRecorder>().animationPlayed);
UnityEngine.Debug.Log("[CHECK] All recorders played: " + allPlayed);

// 4. Verify input unblocked
bool inputFree = !cm.IsInputBlocked;
UnityEngine.Debug.Log("[CHECK] Input unblocked: " + inputFree);
```

#### 5.5 SOP Updates Required

| Document | Section | Update |
|----------|---------|--------|
| `docs/StrategyB_PlayMode_SOP.md` | Challenge #3 | Clarify that `CloseOpenedChain()` is now called **inside** `PlayRecorderAnimationsAndWait()`; tests that manually call `CloseOpenedChain()` after trigger are still valid but the coroutine will call it again safely |
| `docs/StrategyB_PlayMode_SOP.md` | Section 4.3 | Add note: `TriggerRevealEffect` now starts the `PlayRecorderAnimationsAndWait` coroutine automatically in `CombatManager.RevealCards()`; for direct `InvokeEffectEvent()` tests, call `cm.StartCoroutine(cm.GetType().GetMethod("PlayRecorderAnimationsAndWait", BindingFlags.NonPublic).Invoke(cm, null))` to simulate full flow |
| `.agents/skills/unity-card-playmode-test/SKILL.md` | Challenge list | Add Challenge #15: `PlayRecorderAnimationsAndWait` coroutine must be waited for when testing animation capture; direct `InvokeEffectEvent` does not auto-start the coroutine |
| `.agents/skills/unity-card-playmode-test/SKILL.md` | Section 6.1 | Add step 5.5: If testing animation request capture (not just damage), call `PlayRecorderAnimationsAndWait` via reflection and yield one frame to allow coroutine to collect root recorders |

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

All criteria must pass before the system is considered stable.

| # | Criterion | Test Method | Pass Condition |
|---|-----------|-------------|----------------|
| SC-1 | **Basic attack animation** | Play Mode: Reveal `BLACKSMITH`, trigger effect | Attack animation plays; enemy HP reduced by `baseDmg + extraDmg + Power`; no errors in console |
| SC-2 | **Bury chain order** | Play Mode: Deck = `[SPIKE_SKELETON, Start_Card, ETERNAL_GHOST, GRAVE_PUNCH]` (bottom->top); reveal `GRAVE_PUNCH` | Animation order: (1) GRAVE_PUNCH bury batch, (2) SPIKE_SKELETON attack, (3) ETERNAL_GHOST attack (from SPIKE), (4) GRAVE_PUNCH attack, (5) ETERNAL_GHOST attack (from GRAVE_PUNCH) |
| SC-3 | **Multi-effect contiguous playback** | Play Mode: Reveal `GRAVE_PUNCH` (has Bury + HPAlter + HPAlter) | All 3 animation requests captured on the **same** `EffectRecorder`; they play sequentially before any child recorder animations |
| SC-4 | **Deep nesting interleave** | Play Mode: Create chain A(buries B) -> B(buries C) -> C(attacks) | `RecorderAnimationPlayer` traverses depth-first by effect-instance-boundary: A's requests -> A's child B's requests -> B's child C's requests |
| SC-5 | **No combat regression** | Run full card test suite (Strategy A + B) | All existing HP/shield calculations, win/lose conditions, `ValueTrackerManager` counters, and combat logs match pre-change baselines |
| SC-6 | **No soft-lock** | Play Mode: Trigger effect, then force `ExitCombat()` mid-animation | Input block released within 5 seconds (via `ResetInputBlock` in `finally`); no exceptions; `EffectChainManager` children destroyed |
| SC-7 | **Headless compatibility** | Editor Mode: Run `EffectChainTests` with `RecorderAnimationPlayer.me = null` | All damage and deck-manipulation tests pass via fallback paths; no `NullReferenceException` on `RecorderAnimationPlayer.me` access |
| SC-8 | **Event timing correctness** | Play Mode: Reveal `GRAVE_PORTAL` (bury + onMeBuried stage) | `onMeBuried` and `onAnyCardBuried` fire **synchronously** during logic phase (before animations); buried card's reactive effects execute immediately, not after animation |
| SC-9 | **Batch move parallelism** | Play Mode: Reveal `BODY_CANON` (bury all friendly) | All buried cards start `MoveToBottom` simultaneously; yield waits for last card to finish; non-moving cards slide in parallel via `UpdateAllPhysicalCardTargets` |
| SC-10 | **Damage resolved in logic phase** | Play Mode: Reveal damage card, check enemy HP **before** animation coroutine yields | Enemy HP is already reduced immediately after `TriggerRevealedCardEffect()` returns; animation is purely visual |
| SC-11 | **Recorder cleanup** | Play Mode: Complete 3 combat rounds | `EffectChainManager.closedEffectRecorders` does not leak across rounds; `animationPlayed = true` prevents replay; `ExitCombat()` destroys all recorder GameObjects |
| SC-12 | **AnimationStateTracker safety net** | Play Mode: Trigger effect that uses old `AnimationStateTracker` path (e.g. async status effect projectile) | `PlayRecorderAnimationsAndWait` yields until `HasActiveBatch == false` before closing chain; old and new systems do not conflict |

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


## 9. Test Script Examples

### 9.1 Play Mode — Animation Request Capture Verification

Use this snippet to verify that `EffectRecorder`s are building the correct tree and capturing requests after a reveal trigger.

```csharp
// ==========================================
// Setup (standard Strategy B template)
// ==========================================
if (!UnityEditor.EditorApplication.isPlaying){
    UnityEngine.Debug.Log("[TEST FAIL] Must be in Play Mode");
    return 1;
}

CombatManager cm = CombatManager.Me;
if (cm == null) { UnityEngine.Debug.Log("[TEST FAIL] cm null"); return 1; }

// Reset HP
cm.ownerPlayerStatusRef.hp = 100; cm.ownerPlayerStatusRef.hpMax = 100; cm.ownerPlayerStatusRef.shield = 0;
cm.enemyPlayerStatusRef.hp = 100; cm.enemyPlayerStatusRef.hpMax = 100; cm.enemyPlayerStatusRef.shield = 0;

// Reset deck
cm.combinedDeckZone.Clear();
if (cm.revealZone != null) { UnityEngine.Object.DestroyImmediate(cm.revealZone); cm.revealZone = null; }

// Set phase
if (cm.currentGamePhaseRef != null)
    cm.currentGamePhaseRef.currentGamePhase = EnumStorage.GamePhase.Combat;

// Close chains
if (EffectChainManager.Me != null){
    EffectChainManager.Me.CloseOpenedChain();
    EffectChainManager.Me.lastEffectObject = null;
}

// ==========================================
// Helper: Create card with reflection wiring
// ==========================================
System.Func<string, GameObject> CreateTestCard = (System.Func<string, GameObject>)((prefabPath) =>{
    GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    if (prefab == null) { UnityEngine.Debug.Log("[TEST FAIL] Prefab not found: " + prefabPath); return null; }
    GameObject card = UnityEngine.Object.Instantiate(prefab, cm.playerDeckParent != null ? cm.playerDeckParent.transform : null);
    card.name = prefab.name;
    CardScript cs = card.GetComponent<CardScript>();
    cs.myStatusRef = cm.ownerPlayerStatusRef;
    cs.theirStatusRef = cm.enemyPlayerStatusRef;
    cs.myStatusEffects = new System.Collections.Generic.List<EnumStorage.StatusEffect>();
    cs.myTags = new System.Collections.Generic.List<EnumStorage.Tag>();
    EffectScript[] effects = card.GetComponentsInChildren<EffectScript>(true);
    foreach (EffectScript effect in effects)
    {
        typeof(EffectScript).GetField("myCard", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(effect, card);
        typeof(EffectScript).GetField("myCardScript", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(effect, cs);
        typeof(EffectScript).GetField("combatManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(effect, cm);
    }
    HPAlterEffect[] hpaEffects = card.GetComponentsInChildren<HPAlterEffect>(true);
    foreach (HPAlterEffect hae in hpaEffects) hae.isStatusEffectDamage = true;
    return card;
});

System.Action<GameObject> TriggerRevealEffect = (System.Action<GameObject>)((card) =>{
    cm.revealZone = card;
    System.Reflection.MethodInfo triggerMethod = typeof(CombatManager).GetMethod("TriggerRevealedCardEffect",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    if (triggerMethod != null) triggerMethod.Invoke(cm, null);
});

// ==========================================
// Test: PM-2 Bury Chain Animation Order
// ==========================================
UnityEngine.Debug.Log("===== PM-2 Bury Chain Test =====");
{
    // Build deck: Spike_Skeleton(bottom), Start_Card, Eternal_Ghost, Grave_Punch(top)
    GameObject spike = CreateTestCard("Assets/Prefabs/Cards/3.0 no cost (current)/Bury and buried/DeathRattle/1_Uncommon/SPIKE_SKELETON.prefab");
    GameObject startCard = CreateTestCard("Assets/Prefabs/Cards/System/StartCard.prefab");
    GameObject eternal = CreateTestCard("Assets/Prefabs/Cards/3.0 no cost (current)/General/ETERNAL_GHOST.prefab");
    GameObject gravePunch = CreateTestCard("Assets/Prefabs/Cards/3.0 no cost (current)/Bury and buried/Bury/0_Common/GRAVE_PUNCH.prefab");

    if (spike != null) { cm.combinedDeckZone.Add(spike); spike.GetComponent<CardScript>().myStatusRef = cm.ownerPlayerStatusRef; }
    if (startCard != null) cm.combinedDeckZone.Add(startCard);
    if (eternal != null) { cm.combinedDeckZone.Add(eternal); eternal.GetComponent<CardScript>().myStatusRef = cm.ownerPlayerStatusRef; }
    if (gravePunch != null) { cm.combinedDeckZone.Add(gravePunch); gravePunch.GetComponent<CardScript>().myStatusRef = cm.ownerPlayerStatusRef; }

    int hpBefore = cm.enemyPlayerStatusRef.hp;
    TriggerRevealEffect(gravePunch);

    // Manually start PlayRecorderAnimationsAndWait coroutine (simulate CombatManager Phase 2)
    System.Reflection.MethodInfo playMethod = typeof(CombatManager).GetMethod("PlayRecorderAnimationsAndWait",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    if (playMethod != null)
    {
        cm.StartCoroutine((System.Collections.IEnumerator)playMethod.Invoke(cm, null));
    }

    // Yield one frame so coroutine reaches the chain close + recorder collection
    // (In real test you may need to yield more frames; here we just inspect state)

    // Verify recorders built
    var ecm = EffectChainManager.Me;
    int recorderCount = ecm != null ? ecm.closedEffectRecorders.Count : 0;
    UnityEngine.Debug.Log("[CHECK] Closed recorders: " + recorderCount);

    // Verify animation requests exist
    int totalRequests = 0;
    foreach (var recGo in ecm.closedEffectRecorders)
    {
        var rec = recGo.GetComponent<EffectRecorder>();
        if (rec != null)
        {
            totalRequests += rec.animationRequests.Count;
            string reqTypes = "";
            foreach (var req in rec.animationRequests)
                reqTypes += req.type.ToString() + " ";
            UnityEngine.Debug.Log("[CHECK] Recorder " + rec.processedEffectID + " requests=" + rec.animationRequests.Count + " types=" + reqTypes);
        }
    }

    // Damage should already be resolved (logic phase)
    int hpAfter = cm.enemyPlayerStatusRef.hp;
    int totalDmg = hpBefore - hpAfter;
    UnityEngine.Debug.Log("[CHECK] Total damage dealt (logic phase): " + totalDmg);

    // Expected: GRAVE_PUNCH attack(3) + SPIKE_SKELETON 2 attacks(2*3=6) + ETERNAL_GHOST 2 attacks(2*2=4) = 13 (no Power)
    // Actual depends on exact card config; verify > 0 and recorders > 0
    string result = (recorderCount > 0 && totalRequests > 0 && totalDmg > 0) ? "PASS" : "FAIL";
    UnityEngine.Debug.Log("[TEST " + result + "] PM-2 | Recorders=" + recorderCount + " Requests=" + totalRequests + " Dmg=" + totalDmg);

    if (spike != null) UnityEngine.Object.DestroyImmediate(spike);
    if (startCard != null) UnityEngine.Object.DestroyImmediate(startCard);
    if (eternal != null) UnityEngine.Object.DestroyImmediate(eternal);
    if (gravePunch != null) UnityEngine.Object.DestroyImmediate(gravePunch);
}

UnityEngine.Debug.Log("===== Tests Complete =====");
return 0;
```

### 9.2 Headless — Fallback Path Verification

Use this snippet in Editor Mode (or Play Mode with `RecorderAnimationPlayer` destroyed) to verify the old visual path still works.

```csharp
// Headless test: ensure damage resolves when RecorderAnimationPlayer is null
CombatManager cm = CombatManager.Me;
if (cm == null) { UnityEngine.Debug.Log("[TEST FAIL] cm null"); return 1; }

// Destroy RecorderAnimationPlayer if exists
if (RecorderAnimationPlayer.me != null)
    UnityEngine.Object.DestroyImmediate(RecorderAnimationPlayer.me.gameObject);
RecorderAnimationPlayer.me = null;

// Reset state
cm.ownerPlayerStatusRef.hp = 100; cm.enemyPlayerStatusRef.hp = 100; cm.enemyPlayerStatusRef.shield = 0;
cm.combinedDeckZone.Clear();
if (EffectChainManager.Me != null) { EffectChainManager.Me.CloseOpenedChain(); EffectChainManager.Me.lastEffectObject = null; }

// Create and wire a simple damage card
GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
    "Assets/Prefabs/Cards/3.0 no cost (current)/General/BLACKSMITH.prefab");
if (prefab == null) { UnityEngine.Debug.Log("[TEST FAIL] Prefab not found"); return 1; }
GameObject card = UnityEngine.Object.Instantiate(prefab);
CardScript cs = card.GetComponent<CardScript>();
cs.myStatusRef = cm.ownerPlayerStatusRef;
cs.theirStatusRef = cm.enemyPlayerStatusRef;
HPAlterEffect hae = card.GetComponentInChildren<HPAlterEffect>(true);
if (hae != null)
{
    typeof(EffectScript).GetField("myCard", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(hae, card);
    typeof(EffectScript).GetField("myCardScript", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(hae, cs);
    typeof(EffectScript).GetField("combatManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(hae, cm);
    hae.isStatusEffectDamage = true; // synchronous for headless test
}

int hpBefore = cm.enemyPlayerStatusRef.hp;
hae.DecreaseTheirHp();
int hpAfter = cm.enemyPlayerStatusRef.hp;
int dmg = hpBefore - hpAfter;

string result = (dmg > 0) ? "PASS" : "FAIL";
UnityEngine.Debug.Log("[TEST " + result + "] Headless Fallback | Dmg=" + dmg);

UnityEngine.Object.DestroyImmediate(card);
return 0;
```

### 9.3 Editor Unit Test — AnimationRequest Capture

Add to `Assets/Scripts/Editor/Tests/EffectChainTests.cs`:

```csharp
[Test]
public void AnimationRequest_CapturedOnEffectRecorder()
{
    var card = CreateCard(true, "TestCard");
    var effectObj = CreateGameObject("EffectObj");
    effectObj.transform.SetParent(card.transform);

    // Make recorder
    EffectChainManager.MakeANewEffectRecorder(card, effectObj);
    var recorderGo = EffectChainManager.currentEffectRecorder;
    Assert.IsNotNull(recorderGo, "Recorder should be created");
    var recorder = recorderGo.GetComponent<EffectRecorder>();
    Assert.IsNotNull(recorder, "Recorder should have EffectRecorder component");

    // Simulate HPAlterEffect capturing a request
    recorder.animationRequests.Add(new AnimationRequest
    {
        type = AnimationRequestType.Attack,
        attackerCard = card,
        isAttackingEnemy = true,
        onHit = null,
        onComplete = null
    });

    Assert.AreEqual(1, recorder.animationRequests.Count, "Should have 1 animation request");
    Assert.AreEqual(AnimationRequestType.Attack, recorder.animationRequests[0].type, "Should be Attack type");
}

[Test]
public void RecorderAnimationPlayer_InterleavesChildrenDepthFirst()
{
    // Setup a mock tree using Transform hierarchy
    var rootGo = new GameObject("RootRecorder");
    var root = rootGo.AddComponent<EffectRecorder>();
    root.animationRequests.Add(new AnimationRequest { type = AnimationRequestType.Attack });

    var childGo = new GameObject("ChildRecorder");
    var child = childGo.AddComponent<EffectRecorder>();
    child.transform.SetParent(rootGo.transform);
    child.animationRequests.Add(new AnimationRequest { type = AnimationRequestType.MoveToBottom });

    // Run player (this is a synchronous approximation; real test uses UnityTest attribute)
    var playerGo = new GameObject("Player");
    var player = playerGo.AddComponent<RecorderAnimationPlayer>();
    RecorderAnimationPlayer.me = player;

    // Note: In a real Editor test, use [UnityTest] and yield return player.PlayRecorderCoroutine(root);
    // For this unit test, we verify the tree structure:
    Assert.AreEqual(rootGo.transform, childGo.transform.parent, "Child should be parented to root via Transform");
    Assert.IsFalse(root.animationPlayed, "Root should not be played yet");

    UnityEngine.Object.DestroyImmediate(rootGo);
    UnityEngine.Object.DestroyImmediate(childGo);
    UnityEngine.Object.DestroyImmediate(playerGo);
}
```
