# EffectRecorder-Driven Animation System — Implementation Plan

## 1. Overview

### 1.1 Goal
Replace the current **animation-reference-counter-driven event delay** (`AnimationStateTracker`) with an **EffectRecorder-tree-driven sequential animation playback** system.

All effect logic executes synchronously during a **Logic Phase**, building a complete `EffectRecorder` tree with captured animation intents. After the tree is fully constructed, an **Animation Phase** traverses the tree depth-first and plays animations in strict order.

### 1.2 Concrete Result
Using the user's example:
- Deck: `Spike_Skeleton` (bottom), `Start_Card`, `Eternal_Ghost`, `Grave_Punch` (top)
- `Grave_Punch` revealed, triggers `BuryEffect` + `HPAlterEffect`
- Animation sequence:
  1. Spike_Skeleton bury animation
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
    ├── Effects create EffectRecorders + capture AnimationIntent
    ├── Deck state changes immediately
    ├── HP/shield changes captured in callbacks (deferred to animation hit-point)
    └── Tree fully built
    |
    v
CloseOpenedChain() hands tree to EffectAnimationPlayer
    |
    v
[ANIMATION PHASE] — sequential, blocks input
    |
    ├── Depth-first traversal of EffectRecorder tree
    ├── Each recorder's AnimationIntent played to completion
    ├── HP callbacks execute at animation hit-points
    └── Input restored when all animations complete
```

---

## 2. Architecture

### 2.1 New/Modified Components

```
┌─────────────────────────────────────────────────────────────────────┐
│                     EffectAnimationPlayer (NEW)                      │
│  Singleton. Owns animation phase.                                    │
│                                                                      │
│  PlayRecorderTree(EffectRecorder root)                               │
│  └── coroutine: depth-first traversal, yield per animation           │
└─────────────────────────────────────────────────────────────────────┘
                              ▲
                              │ called by
┌─────────────────────────────────────────────────────────────────────┐
│                     EffectChainManager (MODIFIED)                    │
│                                                                      │
│  - Remove SameCardDifferentObject chain closing                      │
│  - Loop guard: ancestor-only (not all open recorders)                │
│  - Add proper parent/child tree links                                │
│  - On CloseOpenedChain(): hand tree root to EffectAnimationPlayer    │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              │ creates/updates
┌─────────────────────────────────────────────────────────────────────┐
│                     EffectRecorder (MODIFIED)                        │
│  MonoBehaviour on recorder prefab                                    │
│                                                                      │
│  Existing: sessionID, chainID, processedEffectID, cardObject,        │
│            effectObject, open                                        │
│  New:      parentRecorder, childRecorders, siblingIndex,             │
│            hasAnimation, animationIntent, animationPlayed            │
└─────────────────────────────────────────────────────────────────────┘
                              ▲
                              │ captures into
┌─────────────────────────────────────────────────────────────────────┐
│  HPAlterEffect, BuryEffect, StageEffect, etc. (MODIFIED)             │
│  Capture AnimationIntent instead of calling visuals directly         │
└─────────────────────────────────────────────────────────────────────┘
```

### 2.2 AnimationIntent Data Model

```csharp
public enum AnimationType
{
    None,
    Attack,           // HPAlterEffect damage/heal
    MoveToBottom,     // BuryEffect
    MoveToTop,        // StageEffect
    MoveToIndex,      // Generic index move
    Destroy,          // Exile / destroy
    StatusProjectile, // Multi/single projectile
    Shuffle,          // Start card shuffle
}

[System.Serializable]
public class AnimationIntent
{
    public AnimationType type = AnimationType.None;

    // ── Attack ──
    public GameObject attackerCard;
    public bool isAttackingEnemy;
    public Action onHit;          // damage resolution callback
    public Action onComplete;

    // ── Move ──
    public GameObject targetCard;
    public int targetIndex;       // for MoveToIndex
    public float duration = 0.5f;
    public bool useArc = true;

    // ── Destroy ──
    public Action onDestroyComplete;

    // ── Status Projectile ──
    public GameObject giverCard;
    public List<CardScript> receiverCards;
    public Action<CardScript> onEachComplete;
    public Action onAllComplete;

    // ── Shuffle ──
    public GameObject startCard;
    public List<GameObject> shuffledCards;
    public Action onShuffleComplete;
}
```

### 2.3 EffectRecorder Tree Structure

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

    // ── NEW: Tree Navigation ──
    public EffectRecorder parentRecorder;
    public List<EffectRecorder> childRecorders = new List<EffectRecorder>();
    public int siblingIndex;

    // ── NEW: Animation Capture ──
    public bool hasAnimation;
    public AnimationIntent animationIntent;

    // ── NEW: Playback State ──
    public bool animationPlayed;
    public bool isPlayingAnimation;
}
```

---

## 3. Implementation Phases

### Phase 1: Core Infrastructure (Files: 4)

**Goal**: Build the foundation. EffectRecorders can form a tree, and the animation player can traverse it.

| # | Task | File |
|---|------|------|
| 1.1 | Add `AnimationIntent` enum and class | `Assets/Scripts/Managers/AnimationIntent.cs` (new) |
| 1.2 | Add tree fields (`parentRecorder`, `childRecorders`, `hasAnimation`, `animationIntent`, etc.) to `EffectRecorder` | `Assets/Scripts/Managers/EffectRecorder.cs` |
| 1.3 | Create `EffectAnimationPlayer` singleton with `PlayRecorderTree` coroutine that does depth-first traversal and invokes `AnimationIntent` callbacks | `Assets/Scripts/Managers/EffectAnimationPlayer.cs` (new) |
| 1.4 | Modify `EffectChainManager` | `Assets/Scripts/Managers/EffectChainManager.cs` |

**EffectChainManager changes (1.4)**:
- Remove `SameCardDifferentObject` chain closing in `CheckShouldIStartANewChain`
- In `MakeANewEffectRecorder`: set `parentRecorder` to the **previous** `currentEffectRecorder` (not `currentEffectRecorderParent`)
- Add child to parent's `childRecorders` list
- Update `currentEffectRecorder` before `effectEvent.Invoke()` and restore it after
- Change `EffectCanBeInvoked` loop guard to check **ancestor chain only** instead of all `openedEffectRecorders`
- In `CloseOpenedChain()`: find the tree root(s) and hand to `EffectAnimationPlayer.PlayRecorderTree()`

**Code sketch for tree building**:
```csharp
public void MakeANewEffectRecorder(GameObject myCard, GameObject myEffectInst)
{
    chainDepth = 0; // reset per recorder? No — this needs rethinking.
    // Actually chainDepth should increment per nested invocation.
    // Currently it's reset in MakeANewEffectRecorder which is wrong for tree building.
    // FIX: only reset chainDepth when creating a NEW tree root.
}
```

> **Important**: `chainDepth` currently resets to 0 in `MakeANewEffectRecorder` and increments in `EffectCanBeInvoked`. This was designed for flat chains. For trees, `chainDepth` should equal the depth from root. We can compute it from `parentRecorder` chain length instead of tracking it separately.

**Restore `currentEffectRecorder` around effect invocation**:
```csharp
// In CostNEffectContainer.InvokeEffectEvent()
EffectChainManager.Me.MakeANewEffectRecorder(_myCardScript.gameObject, gameObject);

if (EffectChainManager.Me.EffectCanBeInvoked(effectString))
{
    EffectChainManager.Me.lastEffectObject = gameObject;
    
    // NEW: push/pop current recorder for proper tree parenting
    var previousRecorder = EffectChainManager.Me.currentEffectRecorder;
    EffectChainManager.Me.currentEffectRecorder = EffectChainManager.Me.openedEffectRecorders[EffectChainManager.Me.openedEffectRecorders.Count - 1];
    
    effectEvent?.Invoke();
    
    EffectChainManager.Me.currentEffectRecorder = previousRecorder;
}
```

### Phase 2: Effect Intent Capture (Files: 5)

**Goal**: Convert key effects from "play animation immediately" to "capture AnimationIntent."

Priority order (most impactful first):

| # | Task | File | What changes |
|---|------|------|-------------|
| 2.1 | `HPAlterEffect` capture attack intent | `Assets/Scripts/Effects/HPAlterEffect.cs` | `DecreaseTheirHp()`, `DecreaseMyHp()`, and all variants capture `AnimationIntent` with `onHit` callback containing `ProcessDamage()` + `CheckDmgTargets_*()` |
| 2.2 | `BuryEffect` capture move intent + immediate event raise | `Assets/Scripts/Effects/BuryEffect.cs` | `BuryChosenCards()` raises `onMeBuried` events **immediately** (logic phase); captures `MoveToBottom` intents for animation phase |
| 2.3 | `StageEffect` capture move intent + immediate event raise | `Assets/Scripts/Effects/StageEffect.cs` | Same pattern as BuryEffect |
| 2.4 | `ExileEffect` / `CardManipulationEffect` capture destroy/move intent | `Assets/Scripts/Effects/ExileEffect.cs`, `CardManipulationEffect.cs` | Capture destroy or move intents |
| 2.5 | Status effect application — decide on particle/tint timing | `Assets/Scripts/Effects/EffectScript.cs` | `ApplyStatusEffectCore()` may need to split logic (immediate) from visuals (deferred to intent) |

**Key pattern for all effects**:
```csharp
// OLD (immediate animation):
combatManager.RaiseDamageDealtEvent(myCard, isAttackingEnemy, onHit, onComplete);

// NEW (capture intent):
var recorder = EffectChainManager.Me.currentEffectRecorder?.GetComponent<EffectRecorder>();
if (recorder != null)
{
    recorder.hasAnimation = true;
    recorder.animationIntent = new AnimationIntent
    {
        type = AnimationType.Attack,
        attackerCard = myCard,
        isAttackingEnemy = isAttackingEnemy,
        onHit = () => { ProcessDamage(totalDmg, targetStatus); CheckDmgTargets(...); },
        onComplete = null
    };
}
```

**BuryEffect event timing change**:
```csharp
// OLD: events raised in animation onComplete callback
combatManager.visuals.MoveCardToBottom(card, ..., onComplete: () => {
    GameEventStorage.me.onMeBuried.RaiseSpecific(card);
});

// NEW: logical changes + events happen immediately; animation deferred
// 1. Modify deck
// 2. Raise events immediately (triggers listeners synchronously, builds tree deeper)
foreach (var buriedCard in buriedCards)
{
    GameEventStorage.me.onMeBuried.RaiseSpecific(buriedCard);
    GameEventStorage.me.onAnyCardBuried.Raise();
    // ... faction events ...
}
// 3. Capture animation intents
// 4. Update physical card targets immediately so cards snap to logical positions
```

### Phase 3: Animation Playback Integration (Files: 3)

**Goal**: `EffectAnimationPlayer` can actually execute the captured intents using existing visual infrastructure.

| # | Task | File |
|---|------|------|
| 3.1 | Implement `PlayAnimationIntent` switch that delegates to existing visual methods | `Assets/Scripts/Managers/EffectAnimationPlayer.cs` |
| 3.2 | Add coroutine-based attack animation API to `AttackAnimationManager` (or reuse existing queue) | `Assets/Scripts/Managers/AttackAnimationManager.cs` |
| 3.3 | Ensure `CombatUXManager` move/destroy methods can be called with yield-able coroutines | `Assets/Scripts/UXPrototype/CombatUXManager.cs` |

**AnimationIntent execution sketch**:
```csharp
private IEnumerator PlayAnimationIntent(AnimationIntent intent)
{
    switch (intent.type)
    {
        case AnimationType.Attack:
            yield return PlayAttackIntent(intent);
            break;
        case AnimationType.MoveToBottom:
            yield return PlayMoveIntent(intent);
            break;
        // ... etc
    }
}

private IEnumerator PlayAttackIntent(AnimationIntent intent)
{
    bool complete = false;
    CombatManager.Me.visuals.PlayAttackAnimation(
        intent.attackerCard,
        intent.isAttackingEnemy,
        onHit: () => { intent.onHit?.Invoke(); },
        onComplete: () => { complete = true; }
    );
    yield return new WaitUntil(() => complete);
}
```

**Input blocking**: `EffectAnimationPlayer` should call `CombatManager.Me.visuals.BlockInput(this)` when starting a tree and `UnblockInput(this)` when done.

### Phase 4: Event System Cleanup (Files: 2)

**Goal**: Remove `AnimationStateTracker` event delay since events now fire synchronously during logic phase.

| # | Task | File | Rationale |
|---|------|------|-----------|
| 4.1 | Remove `AnimationStateTracker.TryExecute` wrapping from all `Raise*` methods | `Assets/Scripts/SOScripts/GameEvent.cs` | Events must fire synchronously to build the complete tree |
| 4.2 | Remove `AnimationStateTracker` usage from all animation methods (or keep for safety timeout only) | `Assets/Scripts/Managers/AnimationStateTracker.cs`, `CombatUXManager.cs`, `AttackAnimationManager.cs` | If `EffectAnimationPlayer` blocks input and runs animations sequentially, the global pending counter is no longer needed for coordination |

**Decision on AnimationStateTracker**: Keep the component in scene but disable its event-delay functionality. It can still serve as a safety timeout during transition. Or fully remove it once Phase 5 tests pass.

### Phase 5: Testing & Edge Cases (Files: 2)

| # | Task | File |
|---|------|------|
| 5.1 | Update `EffectChainTests` to test tree structure instead of flat chain | `Assets/Scripts/Editor/Tests/EffectChainTests.cs` |
| 5.2 | Update PlayMode test SOPs that reference `CloseOpenedChain()` behavior | `docs/StrategyB_PlayMode_SOP.md`, `.agents/skills/unity-card-playmode-test/SKILL.md` |

**Test scenarios**:
1. Single card reveal -> attack animation plays normally
2. Bury -> onMeBuried -> stage: animations play in bury -> stage order
3. Same card with two effects (e.g., attack + bury): both animations capture, tree has two sibling children
4. Deep nesting (A -> B -> C -> D): animations play A -> B -> C -> D
5. Loop guard: same card+effect in ancestor chain blocks; same card+effect in sibling branch allows
6. Combat end during animation: `EffectAnimationPlayer` stops gracefully, `StopAllAnimations()` called

---

## 4. File-by-File Change Detail

### 4.1 New Files

**`Assets/Scripts/Managers/AnimationIntent.cs`**
```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

public enum AnimationType
{
    None,
    Attack,
    MoveToBottom,
    MoveToTop,
    MoveToIndex,
    Destroy,
    StatusProjectileSingle,
    StatusProjectileMulti,
    Shuffle
}

[Serializable]
public class AnimationIntent
{
    public AnimationType type = AnimationType.None;

    // Attack
    public GameObject attackerCard;
    public bool isAttackingEnemy;
    public Action onHit;
    public Action onComplete;

    // Move
    public GameObject targetCard;
    public int targetIndex = -1;
    public float duration = 0.5f;
    public bool useArc = true;

    // Destroy
    public Action onDestroyComplete;

    // Status Projectile
    public GameObject giverCard;
    public CardScript singleReceiver;
    public List<CardScript> multiReceivers;
    public Action<CardScript> onEachProjectileComplete;
    public Action onAllProjectilesComplete;

    // Shuffle
    public GameObject shuffleStartCard;
    public List<GameObject> shuffleResultCards;
    public Action onShuffleComplete;
}
```

**`Assets/Scripts/Managers/EffectAnimationPlayer.cs`**
```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EffectAnimationPlayer : MonoBehaviour
{
    public static EffectAnimationPlayer me;
    void Awake() { me = this; }

    private bool _isPlaying;
    public bool IsPlaying => _isPlaying;

    public void PlayRecorderTree(EffectRecorder root)
    {
        if (root == null) return;
        if (_isPlaying)
        {
            Debug.LogWarning("[EffectAnimationPlayer] Tree playback requested while already playing. Queueing not yet implemented.");
            return;
        }
        StartCoroutine(PlayTreeCoroutine(root));
    }

    private IEnumerator PlayTreeCoroutine(EffectRecorder root)
    {
        _isPlaying = true;
        CombatManager.Me?.visuals?.BlockInput(this);

        yield return PlayRecorderRecursive(root);

        CombatManager.Me?.visuals?.UnblockInput(this);
        _isPlaying = false;
    }

    private IEnumerator PlayRecorderRecursive(EffectRecorder recorder)
    {
        if (recorder == null) yield break;

        if (recorder.hasAnimation && recorder.animationIntent != null)
        {
            yield return ExecuteIntent(recorder.animationIntent);
            recorder.animationPlayed = true;
        }

        for (int i = 0; i < recorder.childRecorders.Count; i++)
        {
            yield return PlayRecorderRecursive(recorder.childRecorders[i]);
        }
    }

    private IEnumerator ExecuteIntent(AnimationIntent intent)
    {
        switch (intent.type)
        {
            case AnimationType.Attack:
                yield return ExecuteAttack(intent);
                break;
            case AnimationType.MoveToBottom:
                yield return ExecuteMoveToBottom(intent);
                break;
            case AnimationType.MoveToTop:
                yield return ExecuteMoveToTop(intent);
                break;
            case AnimationType.MoveToIndex:
                yield return ExecuteMoveToIndex(intent);
                break;
            case AnimationType.Destroy:
                yield return ExecuteDestroy(intent);
                break;
            case AnimationType.StatusProjectileSingle:
                yield return ExecuteSingleProjectile(intent);
                break;
            case AnimationType.StatusProjectileMulti:
                yield return ExecuteMultiProjectile(intent);
                break;
            case AnimationType.Shuffle:
                yield return ExecuteShuffle(intent);
                break;
            default:
                intent.onComplete?.Invoke();
                break;
        }
    }

    private IEnumerator ExecuteAttack(AnimationIntent intent)
    {
        bool done = false;
        CombatManager.Me.visuals.PlayAttackAnimation(
            intent.attackerCard,
            intent.isAttackingEnemy,
            onHit: () => { intent.onHit?.Invoke(); },
            onComplete: () => { intent.onComplete?.Invoke(); done = true; }
        );
        yield return new WaitUntil(() => done);
    }

    private IEnumerator ExecuteMoveToBottom(AnimationIntent intent)
    {
        bool done = false;
        CombatManager.Me.visuals.MoveCardToBottom(
            intent.targetCard,
            intent.duration,
            intent.useArc,
            onComplete: () => { intent.onComplete?.Invoke(); done = true; }
        );
        yield return new WaitUntil(() => done);
    }

    private IEnumerator ExecuteMoveToTop(AnimationIntent intent)
    {
        bool done = false;
        CombatManager.Me.visuals.MoveCardToTop(
            intent.targetCard,
            intent.duration,
            intent.useArc,
            onComplete: () => { intent.onComplete?.Invoke(); done = true; }
        );
        yield return new WaitUntil(() => done);
    }

    private IEnumerator ExecuteMoveToIndex(AnimationIntent intent)
    {
        bool done = false;
        CombatManager.Me.visuals.MoveCardToIndex(
            intent.targetCard,
            intent.targetIndex,
            intent.duration,
            intent.useArc,
            onComplete: () => { intent.onComplete?.Invoke(); done = true; }
        );
        yield return new WaitUntil(() => done);
    }

    private IEnumerator ExecuteDestroy(AnimationIntent intent)
    {
        bool done = false;
        CombatManager.Me.visuals.DestroyCardWithAnimation(
            intent.targetCard,
            onComplete: () => { intent.onDestroyComplete?.Invoke(); done = true; }
        );
        yield return new WaitUntil(() => done);
    }

    private IEnumerator ExecuteSingleProjectile(AnimationIntent intent)
    {
        bool done = false;
        CombatManager.Me.visuals.PlayStatusEffectProjectile(
            intent.giverCard,
            intent.singleReceiver.gameObject,
            onComplete: () => { intent.onComplete?.Invoke(); done = true; }
        );
        yield return new WaitUntil(() => done);
    }

    private IEnumerator ExecuteMultiProjectile(AnimationIntent intent)
    {
        bool done = false;
        CombatManager.Me.visuals.PlayMultiStatusEffectProjectile(
            intent.giverCard,
            intent.multiReceivers,
            onEachComplete: intent.onEachProjectileComplete,
            onAllComplete: () => { intent.onAllProjectilesComplete?.Invoke(); done = true; }
        );
        yield return new WaitUntil(() => done);
    }

    private IEnumerator ExecuteShuffle(AnimationIntent intent)
    {
        bool done = false;
        CombatManager.Me.visuals.PlayShuffleAnimation(
            intent.shuffleStartCard,
            intent.shuffleResultCards,
            onComplete: () => { intent.onShuffleComplete?.Invoke(); done = true; }
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
  public EffectRecorder parentRecorder;
  public List<EffectRecorder> childRecorders = new List<EffectRecorder>();
  public int siblingIndex;
  public bool hasAnimation;
  public AnimationIntent animationIntent;
  public bool animationPlayed;
  public bool isPlayingAnimation;
  ```

**`Assets/Scripts/Managers/EffectChainManager.cs`**

Changes:
1. Remove `chainDepth` field (compute from ancestor chain instead)
2. In `CheckShouldIStartANewChain`: remove `SameCardDifferentObject` logic. Only start a new chain when `openedEffectRecorders.Count == 0`.
3. In `MakeANewEffectRecorder`:
   - Remove `chainDepth = 0;`
   - Set up tree links:
     ```csharp
     var newChainScript = newEffectChain.GetComponent<EffectRecorder>();
     // ... existing fields ...
     
     var parent = currentEffectRecorder != null
         ? currentEffectRecorder.GetComponent<EffectRecorder>()
         : null;
     newChainScript.parentRecorder = parent;
     newChainScript.siblingIndex = parent != null ? parent.childRecorders.Count : 0;
     if (parent != null)
         parent.childRecorders.Add(newChainScript);
     ```
   - Keep `currentEffectRecorder = newEffectChain;` but also track it properly
4. In `EffectCanBeInvoked`:
   - Replace flat `openedEffectRecorders` loop with ancestor chain walk:
     ```csharp
     var ancestor = currentRec.parentRecorder;
     while (ancestor != null)
     {
         if (ancestor.cardObject == myCard &&
             ancestor.effectObject == myEffect &&
             !string.IsNullOrEmpty(ancestor.processedEffectID))
         {
             return false;
         }
         ancestor = ancestor.parentRecorder;
     }
     ```
   - Compute depth from ancestor chain:
     ```csharp
     int depth = 0;
     var d = currentRec.parentRecorder;
     while (d != null) { depth++; d = d.parentRecorder; }
     if (depth > 99) { Debug.LogError("ERROR: chain depth reached limit"); return false; }
     ```
5. In `CloseOpenedChain`:
   - After closing recorders, find tree root(s) and trigger animation playback
   - Root = any recorder with `parentRecorder == null`
   - For now, expect exactly one root per close call:
     ```csharp
     EffectRecorder root = null;
     foreach (var rec in closedEffectRecorders)
     {
         var r = rec.GetComponent<EffectRecorder>();
         if (r.parentRecorder == null) { root = r; break; }
     }
     if (root != null)
         EffectAnimationPlayer.me?.PlayRecorderTree(root);
     ```

**`Assets/Scripts/Card/CostNEffectContainer.cs`**

In `InvokeEffectEvent()`, after `MakeANewEffectRecorder`, add push/pop of `currentEffectRecorder`:

```csharp
EffectChainManager.Me.MakeANewEffectRecorder(_myCardScript.gameObject, gameObject);

if (EffectChainManager.Me.EffectCanBeInvoked(effectString))
{
    EffectChainManager.Me.lastEffectObject = gameObject;
    
    // Push current recorder for tree nesting
    var previousRecorder = EffectChainManager.Me.currentEffectRecorder;
    EffectChainManager.Me.currentEffectRecorder = EffectChainManager.Me.openedEffectRecorders[EffectChainManager.Me.openedEffectRecorders.Count - 1];
    
    effectEvent?.Invoke();
    
    // Pop back
    EffectChainManager.Me.currentEffectRecorder = previousRecorder;
}
```

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
    
    var recorder = EffectChainManager.Me.currentEffectRecorder?.GetComponent<EffectRecorder>();
    if (recorder != null)
    {
        recorder.hasAnimation = true;
        recorder.animationIntent = new AnimationIntent
        {
            type = AnimationType.Attack,
            attackerCard = myCard,
            isAttackingEnemy = isAttackingEnemy,
            onHit = () =>
            {
                ProcessDamage(totalDmg, myCardScript.theirStatusRef);
                CheckDmgTargets_DealingDmgToOpponent(totalDmg);
            },
            onComplete = null
        };
    }
    else
    {
        // Fallback if no recorder (should not happen in normal combat)
        ProcessDamage(totalDmg, myCardScript.theirStatusRef);
        CheckDmgTargets_DealingDmgToOpponent(totalDmg);
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
    
    // NEW: Capture animation intents for all buried cards
    var currentRecorder = EffectChainManager.Me.currentEffectRecorder?.GetComponent<EffectRecorder>();
    foreach (var card in buriedCards)
    {
        if (currentRecorder != null)
        {
            // Create child recorder for each bury animation
            // Alternatively: store multiple intents in a list on the parent recorder
            // For simplicity, create child recorders
            // But we need to make sure these are in the tree...
        }
    }
    
    // Update other cards' positions immediately
    combatManager.visuals.UpdateAllPhysicalCardTargets();
    
    // NEW: Raise events IMMEDIATELY (logic phase)
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

> **Design note for BuryEffect**: Each buried card could have its own child recorder, OR the bury animation intents could be stored in a list. The simplest approach for Phase 2 is: the parent effect recorder captures ONE intent that represents the entire bury operation. Since multiple cards being buried is part of the same effect invocation, they can share one recorder. `AnimationIntent` could be extended to support a list of target cards for batch move. For MVP, just capture the first card or add a `List<GameObject> batchMoveTargets` to `AnimationIntent`.

**`Assets/Scripts/Effects/StageEffect.cs`**
Same pattern as BuryEffect: immediate logical deck change, immediate event raise, capture move intent.

**`Assets/Scripts/SOScripts/GameEvent.cs`**
Remove `AnimationStateTracker.TryExecute` wrapping. All `Raise*` methods call `ExecuteRaise*` directly.

```csharp
public void Raise()
{
    ExecuteRaise();
}

public void RaiseOwner()
{
    ExecuteRaiseOwner();
}

public void RaiseOpponent()
{
    ExecuteRaiseOpponent();
}

public void RaiseSpecific(GameObject target)
{
    if (target == null) return;
    ExecuteRaiseSpecific(target);
}
```

**`Assets/Scripts/Managers/AnimationStateTracker.cs`**
Keep file in project but disable event-delay functionality. Comment out `TryExecute` body and make it execute immediately. Or remove entirely after Phase 5 confirms stability.

**`Assets/Scripts/Managers/CombatManager.cs`**
Remove `WaitForAttackAnimationsBeforeNextReveal()` coroutine or simplify it to check `EffectAnimationPlayer.me?.IsPlaying` instead of `visuals.HasPendingAnimations()`.

In `RevealCards()`:
```csharp
// OLD
if (visuals != null && visuals.IsPlayingAttackAnimation()) return;

// NEW
if (EffectAnimationPlayer.me != null && EffectAnimationPlayer.me.IsPlaying) return;
```

Also update `IsInputBlocked` check or ensure `EffectAnimationPlayer` properly blocks/unblocks input.

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

### After (Proposed System)
```
Grave_Punch revealed
  -> onMeRevealed raised (synchronous)
     -> BuryEffect executes
        -> Logical deck modified
        -> onMeBuried raised (synchronous)
           -> Spike_Skeleton effect executes
              -> Capture attack intent (Recorder #3)
              -> onTheirPlayerTookDmg raised (synchronous)
                 -> Eternal_Ghost effect executes
                    -> Capture attack intent (Recorder #4)
        -> Capture bury intent (Recorder #1)
     -> HPAlterEffect executes
        -> Capture attack intent (Recorder #2)
        -> onTheirPlayerTookDmg raised (synchronous)
           -> Eternal_Ghost effect executes
              -> Capture attack intent (Recorder #5)
     -> CloseOpenedChain()
        -> EffectAnimationPlayer.PlayRecorderTree(Recorder #1 root)

[Animation Phase]
  1. Recorder #1: Bury animation
  2. Recorder #3: Spike_Skeleton attack
  3. Recorder #4: Eternal_Ghost attack (from Spike)
  4. Recorder #2: Grave_Punch attack
  5. Recorder #5: Eternal_Ghost attack (from Grave_Punch)
```

**Result**: Deterministic depth-first order. No race conditions.

---

## 6. Risks & Mitigations

| # | Risk | Severity | Mitigation |
|---|------|----------|------------|
| 1 | Extensive file changes (10+ files) break existing combat flow | High | Implement in phases; test after each phase; keep `AnimationStateTracker` as fallback during transition |
| 2 | BuryEffect/StageEffect event timing change breaks card interactions | High | Move events to synchronous immediately; if cards depend on animation-complete timing, adjust those cards individually |
| 3 | AttackAnimationManager queue + EffectAnimationPlayer double-queue | Medium | `EffectAnimationPlayer` should call `PlayAttackAnimation` which enqueues; but then attack queue processes internally. This is fine — `EffectAnimationPlayer` waits for `onComplete` which fires after the attack queue finishes that specific attack. |
| 4 | Same-card parallel animations lost (attack + bury no longer simultaneous) | Medium | If UX feels too slow, implement hybrid mode where same-card sibling recorders play in parallel |
| 5 | Loop guard with ancestor-only check allows new loop patterns | Medium | Test extensively with known loop-prone cards; the ancestor check is actually stricter in some ways (allows siblings) and looser in others (allows different branches) |
| 6 | `CombatUXManager.MoveCardWithAnimation` relies on `AnimationStateTracker` | Low | `MoveCardWithAnimation` uses `RegisterAnimation/CompleteAnimation` for counting. `EffectAnimationPlayer` uses `onComplete` callbacks, so it doesn't need the global counter. Remove or keep as no-op. |
| 7 | Input blocking gaps between animations allow player to click | Low | `EffectAnimationPlayer` holds input block for the ENTIRE tree playback, not per-animation |

---

## 7. Success Criteria

1. **Basic combat**: Reveal a card with simple damage effect → attack animation plays normally
2. **Bury chain**: Card with bury effect targets another card → bury animation plays first, then buried card's effect animation plays
3. **Multi-effect card**: Card with attack + bury → both intents captured, tree has two children, animations play sequentially
4. **Deep nesting**: A -> B -> C -> D chain → animations play in A-B-C-D order
5. **No regression**: Combat win/lose conditions, HP calculations, and combat log remain correct
6. **No soft-lock**: If animation system breaks, timeout releases input within 5 seconds

---

## 8. Recommended Execution Order

1. **Create branch** (if using version control)
2. **Phase 1** (Core Infrastructure): Create `AnimationIntent.cs`, `EffectAnimationPlayer.cs`, modify `EffectRecorder.cs`, `EffectChainManager.cs`, `CostNEffectContainer.cs`
3. **Test Phase 1**: Use existing PlayMode tests or manual play to verify tree builds correctly
4. **Phase 2** (Effect Capture): Modify `HPAlterEffect.cs`, `BuryEffect.cs`, `StageEffect.cs`
5. **Test Phase 2**: Verify combat log shows correct order, animations captured but not played yet
6. **Phase 3** (Animation Playback): Wire up `EffectAnimationPlayer` intent execution
7. **Test Phase 3**: Full combat flow — animations should play in tree order
8. **Phase 4** (Cleanup): Remove `AnimationStateTracker` event delay, update `GameEvent.cs`
9. **Phase 5** (Tests): Update unit tests and PlayMode SOPs
10. **Full regression test**: Run full card test suite
