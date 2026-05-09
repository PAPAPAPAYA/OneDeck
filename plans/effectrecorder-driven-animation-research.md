# EffectRecorder-Driven Animation System - Research Document

## 1. Current System Overview

### 1.1 Animation Coordination: `AnimationStateTracker`
The current system uses a **global animation reference counter** (`_pendingAnimations`) to coordinate when `GameEvent.Raise*` calls execute:

- `RegisterAnimation()` increments counter when any animation starts
- `CompleteAnimation()` decrements counter when any animation ends
- `TryExecute(Action)` queues events if `_pendingAnimations > 0`, otherwise executes immediately
- `FlushDelayedEvents()` runs queued events when counter reaches 0

This creates a **time-based serialization**: events are delayed based on *when animations happen to finish*, not based on *causal structure*.

### 1.2 Attack Animation: `AttackAnimationManager`
Attack animations are queued in a `Queue<AttackAnimData>` and processed sequentially per frame:
- Multiple `RequestAttackAnimation` calls in the same frame all enqueue
- `ProcessQueue()` dequeues and plays one at a time
- `DelayedStartQueue()` waits one frame to batch all synchronous requests

### 1.3 Effect Chain Tracking: `EffectChainManager`
`EffectChainManager` creates `EffectRecorder` GameObjects to track effect invocation:

```csharp
public class EffectRecorder : MonoBehaviour
{
    public int sessionID;
    public int chainID;
    public string processedEffectID;
    public GameObject cardObject;       // the card triggering this effect
    public GameObject effectObject;     // the CostNEffectContainer
    public bool open = true;
}
```

Key behaviors:
- **New chain triggered** when `openedEffectRecorders.Count == 0` OR same card + different effect object is found
- **Loop guard**: Same `cardObject + effectObject` cannot be invoked twice within open chains
- **Depth limit**: `chainDepth > 99` blocks further effects
- **Parenting**: New recorder is parented under `currentEffectRecorderParent` (root if null)
- **CloseOpenedChain()**: Marks all open recorders as closed, copies to `closedEffectRecorders`, clears list

### 1.4 Effect Invocation: `CostNEffectContainer`
```csharp
public CostCheckResult InvokeEffectEvent()
{
    // 1. Check cost
    // 2. Execute pre-effect
    // 3. Make new EffectRecorder
    EffectChainManager.Me.CheckShouldIStartANewChain(_myCardScript.gameObject, gameObject);
    EffectChainManager.Me.MakeANewEffectRecorder(_myCardScript.gameObject, gameObject);
    
    if (EffectChainManager.Me.EffectCanBeInvoked(effectString))
    {
        EffectChainManager.Me.lastEffectObject = gameObject;
        effectEvent?.Invoke(); // <-- animations and events interleave here
    }
    return CostCheckResult.Success();
}
```

### 1.5 The Problem with Current System
The current system has **two separate coordination mechanisms** that don't know about each other:
1. `EffectChainManager` tracks logical causality (recorder tree)
2. `AnimationStateTracker` tracks runtime animation state (reference counter)

Because effects execute immediately and animations start immediately, the animation order depends on:
- When DOTween sequences complete
- When `GameEvent` flush cycles happen
- When `AttackAnimationManager` processes its queue

This makes the animation order **emergent and hard to predict**.

---

## 2. Proposed System: EffectRecorder-Driven Animation

### 2.1 Core Idea
Instead of animations starting immediately during effect execution, **capture all animation intent into the EffectRecorder tree first, then play animations by traversing the tree**.

**Two-phase execution:**
1. **Logic Phase**: Execute all effects immediately (no animations). Build the complete EffectRecorder tree with animation metadata.
2. **Animation Phase**: Traverse the EffectRecorder tree in depth-first order, playing each recorder's animation sequentially.

### 2.2 User's Concrete Example

**Deck order** (bottom to top): `Spike_Skeleton`, `Start_Card`, `Eternal_Ghost`, `Grave_Punch`

**Card behaviors** (hypothetical):
- `Grave_Punch` (revealed): has `BuryEffect` (bury next card) + `HPAlterEffect` (deal damage)
- `Spike_Skeleton` (buried): listens to `onMeBuried`, triggers `HPAlterEffect` (deal damage)
- `Eternal_Ghost`: listens to damage events, triggers `HPAlterEffect` (deal damage)

**Desired EffectRecorder tree:**
```
[Implicit Root: Grave_Punch Revealed]
├── Recorder #1: Grave_Punch -> BuryEffect (bury Spike_Skeleton)
│   └── Recorder #3: Spike_Skeleton -> HPAlterEffect (damage)
│       └── Recorder #4: Eternal_Ghost -> HPAlterEffect (damage)
└── Recorder #2: Grave_Punch -> HPAlterEffect (damage)
    └── Recorder #5: Eternal_Ghost -> HPAlterEffect (damage)
```

**Desired animation sequence** (depth-first order):
1. Spike_Skeleton bury animation
2. Spike_Skeleton attack animation
3. Eternal_Ghost attack animation (triggered by Spike_Skeleton)
4. Grave_Punch attack animation
5. Eternal_Ghost attack animation (triggered by Grave_Punch)

---

## 3. Can Current System Produce This Tree?

### 3.1 Current Tree Generation Behavior

Tracing the user's example through current `EffectChainManager`:

**Step 1**: Grave_Punch's `BuryEffect` invokes
- `openedEffectRecorders.Count == 0` -> new chain
- `CloseOpenedChain()` (no-op)
- `currentEffectRecorderParent = null`
- **Recorder #1** created: `card=Grave_Punch, effect=BuryEffect, isRoot=true`
- `currentEffectRecorderParent = Recorder#1`
- Effect executes: buries Spike_Skeleton

**Step 2**: BuryEffect raises `onMeBuried` on Spike_Skeleton
- Spike_Skeleton listener fires, its `HPAlterEffect` invokes
- `SameCardDifferentObject(Spike_Skeleton, HPAlterEffect)`:
  - Compare with Recorder#1: card=Grave_Punch (different card) -> `sameCard = false`
  - Result: `sameCardDiffObj = false`
- `shouldIMakeANewChain = false`
- **Recorder #3** created: `card=Spike_Skeleton, effect=HPAlterEffect, parent=Recorder#1`
- Effect executes: deals damage, raises event

**Step 3**: Spike_Skeleton damage raises event triggering Eternal_Ghost
- Eternal_Ghost's `HPAlterEffect` invokes
- `SameCardDifferentObject(Eternal_Ghost, HPAlterEffect)`:
  - Compare with Recorder#1: card=Grave_Punch (different) -> false
  - Compare with Recorder#3: card=Spike_Skeleton (different) -> false
  - Result: `sameCardDiffObj = false`
- **Recorder #4** created: `card=Eternal_Ghost, effect=HPAlterEffect, parent=Recorder#3`
- Effect executes

**Step 4**: Back to Grave_Punch, its `HPAlterEffect` invokes
- `SameCardDifferentObject(Grave_Punch, HPAlterEffect)`:
  - Compare with Recorder#1: card=Grave_Punch (same), effect=BuryEffect (different) -> `sameCardDiffObj = true`
- `shouldIMakeANewChain = true`
- `CloseOpenedChain()` closes Recorder#1, #3, #4
- `currentEffectRecorderParent = null`
- **Recorder #2** created: `card=Grave_Punch, effect=HPAlterEffect, isRoot=true`
- `currentEffectRecorderParent = Recorder#2`
- Effect executes: deals damage, raises event

**Step 5**: Grave_Punch damage raises event triggering Eternal_Ghost
- Eternal_Ghost's `HPAlterEffect` invokes
- `SameCardDifferentObject(Eternal_Ghost, HPAlterEffect)`:
  - Compare with Recorder#2: card=Grave_Punch (different) -> false
  - Result: `sameCardDiffObj = false`
- **Recorder #5** created: `card=Eternal_Ghost, effect=HPAlterEffect, parent=Recorder#2`
- Effect executes

### 3.2 Result: Current System Produces Two Separate Trees

```
Tree A (closed):
Recorder #1: Grave_Punch -> BuryEffect
└── Recorder #3: Spike_Skeleton -> HPAlterEffect
    └── Recorder #4: Eternal_Ghost -> HPAlterEffect

Tree B (closed):
Recorder #2: Grave_Punch -> HPAlterEffect
└── Recorder #5: Eternal_Ghost -> HPAlterEffect
```

**The current system produces two separate root chains**, not one unified tree. This is because `SameCardDifferentObject` closes all open chains when the same card triggers a different effect.

### 3.3 Why This Closing Behavior Exists
The `SameCardDifferentObject` logic exists to prevent scenarios like:
- Card A has Effect1 and Effect2
- Effect1 triggers some event that eventually triggers Effect2 on the same card
- Without closing, Effect2 would be nested under Effect1, creating a confusing tree
- The intent was: each "distinct effect invocation on a card" starts a fresh causal chain

### 3.4 What the Proposed System Needs
To achieve the user's desired single tree, the chain closing logic must change:

**Current rule**: Same card + different effect object -> close all chains, start fresh root.
**Proposed rule**: Same card + different effect object -> create sibling recorder under the SAME parent (the card's reveal action).

But this creates ambiguity: what IS the parent? Currently there is no "reveal action" recorder. The parent is whatever `currentEffectRecorderParent` happens to be.

---

## 4. Required Architecture Changes

### 4.1 Data Model: Enhanced EffectRecorder

```csharp
public class EffectRecorder : MonoBehaviour
{
    // Existing fields
    public int sessionID;
    public int chainID;
    public string processedEffectID;
    public GameObject cardObject;
    public GameObject effectObject;
    public bool open = true;
    
    // NEW: Tree navigation
    public EffectRecorder parentRecorder;
    public List<EffectRecorder> childRecorders = new List<EffectRecorder>();
    public int siblingIndex; // order among siblings
    
    // NEW: Animation metadata (captured during logic phase)
    public AnimationIntent animationIntent;
    public bool hasAnimation;
    
    // NEW: Playback state
    public bool animationPlayed;
    public bool animationPlaying;
}

public enum AnimationIntentType
{
    None,           // No animation (pure logic effect)
    Attack,         // HPAlterEffect attack animation
    Bury,           // Move card to bottom
    Stage,          // Move card to top
    Destroy,        // Destroy card
    StatusProjectile, // Status effect projectile
    Shuffle,        // Deck shuffle
    Custom          // Other visual effect
}

public class AnimationIntent
{
    public AnimationIntentType type;
    
    // Attack-specific
    public GameObject attackerCard;
    public bool isAttackingEnemy;
    public int damageAmount;
    
    // Move-specific
    public GameObject targetCard;
    public CardMoveType moveType;
    public int targetIndex;
    
    // Status-specific
    public GameObject giverCard;
    public List<CardScript> targetCards;
    
    // Callback for logic that must run at animation hit-point
    public Action onAnimationHit;
    public Action onAnimationComplete;
}
```

### 4.2 New Component: `EffectAnimationPlayer`

A new singleton that owns the animation phase:

```csharp
public class EffectAnimationPlayer : MonoBehaviour
{
    public static EffectAnimationPlayer me;
    
    // The root of the current effect tree being animated
    private EffectRecorder _currentTreeRoot;
    private bool _isPlayingAnimations;
    
    /// <summary>
    /// Called by EffectChainManager after CloseOpenedChain().
    /// Starts playing animations for the closed recorder tree.
    /// </summary>
    public void PlayRecorderTree(EffectRecorder root)
    {
        if (_isPlayingAnimations)
        {
            // Queue for later, or append to current tree
            return;
        }
        StartCoroutine(PlayTreeCoroutine(root));
    }
    
    private IEnumerator PlayTreeCoroutine(EffectRecorder root)
    {
        _isPlayingAnimations = true;
        yield return PlayRecorderAndChildren(root);
        _isPlayingAnimations = false;
    }
    
    private IEnumerator PlayRecorderAndChildren(EffectRecorder recorder)
    {
        // Skip if no animation
        if (!recorder.hasAnimation || recorder.animationIntent == null)
        {
            // Execute any immediate callbacks
            recorder.animationIntent?.onAnimationHit?.Invoke();
            recorder.animationIntent?.onAnimationComplete?.Invoke();
        }
        else
        {
            yield return PlayAnimationIntent(recorder.animationIntent);
        }
        
        // Play children in order
        foreach (var child in recorder.childRecorders)
        {
            yield return PlayRecorderAndChildren(child);
        }
    }
    
    private IEnumerator PlayAnimationIntent(AnimationIntent intent)
    {
        switch (intent.type)
        {
            case AnimationIntentType.Attack:
                yield return PlayAttackAnimation(intent);
                break;
            case AnimationIntentType.Bury:
                yield return PlayMoveAnimation(intent);
                break;
            // ... etc
        }
    }
}
```

### 4.3 Modified EffectChainManager

```csharp
public class EffectChainManager : MonoBehaviour
{
    // ... existing fields ...
    
    // NEW: Track the tree root for the current reveal action
    public EffectRecorder currentTreeRoot;
    
    public void MakeANewEffectRecorder(GameObject myCard, GameObject myEffectInst)
    {
        // ... existing setup ...
        
        var newChainScript = newEffectChain.GetComponent<EffectRecorder>();
        newChainScript.sessionID = sessionNumberRef.value;
        newChainScript.chainID = chainNumber;
        newChainScript.cardObject = myCard;
        newChainScript.effectObject = myEffectInst;
        newChainScript.open = true;
        
        // NEW: Set up tree relationships
        if (currentEffectRecorderParent == null)
        {
            // This is a root-level effect
            currentTreeRoot = newChainScript;
            newChainScript.parentRecorder = null;
        }
        else
        {
            newChainScript.parentRecorder = currentEffectRecorderParent.GetComponent<EffectRecorder>();
            newChainScript.parentRecorder.childRecorders.Add(newChainScript);
        }
        
        currentEffectRecorder = newEffectChain;
        openedEffectRecorders.Add(newEffectChain);
    }
    
    public void CloseOpenedChain()
    {
        // ... existing closing logic ...
        
        // NEW: Trigger animation playback for the closed tree
        if (currentTreeRoot != null)
        {
            EffectAnimationPlayer.me?.PlayRecorderTree(currentTreeRoot);
            currentTreeRoot = null;
        }
    }
}
```

### 4.4 Modified HPAlterEffect (Capture Intent Instead of Playing)

```csharp
public void DecreaseTheirHp()
{
    DmgCalculator();
    int totalDmg = extraDmg + dmgAmountAlter;
    
    // Status effect damage: no animation, apply immediately
    if (isStatusEffectDamage)
    {
        ProcessDamage(totalDmg, myCardScript.theirStatusRef);
        CheckDmgTargets_DealingDmgToOpponent(totalDmg);
        dmgAmountAlter = 0;
        return;
    }
    
    // NEW: Capture animation intent instead of playing immediately
    bool isAttackingEnemy = myCardScript.theirStatusRef != combatManager.ownerPlayerStatusRef;
    
    var currentRecorder = EffectChainManager.Me.currentEffectRecorder?.GetComponent<EffectRecorder>();
    if (currentRecorder != null)
    {
        currentRecorder.hasAnimation = true;
        currentRecorder.animationIntent = new AnimationIntent
        {
            type = AnimationIntentType.Attack,
            attackerCard = myCard,
            isAttackingEnemy = isAttackingEnemy,
            damageAmount = totalDmg,
            onAnimationHit = () =>
            {
                ProcessDamage(totalDmg, myCardScript.theirStatusRef);
                CheckDmgTargets_DealingDmgToOpponent(totalDmg);
            },
            onAnimationComplete = null
        };
    }
    
    dmgAmountAlter = 0;
}
```

### 4.5 Modified BuryEffect (Capture Intent)

```csharp
private void BuryChosenCards(List<GameObject> cardsToBury, int amount)
{
    // ... existing logic deck modification ...
    
    // Sync physical card list order with logical deck before animation
    combatManager.visuals.SyncPhysicalCardsWithCombinedDeck();
    
    // NEW: Capture animation intents
    var currentRecorder = EffectChainManager.Me.currentEffectRecorder?.GetComponent<EffectRecorder>();
    
    foreach (var card in buriedCards)
    {
        // Create child recorders for each bury animation
        // OR: store multiple intents in parent recorder
        
        // Option A: Parent recorder holds all bury intents
        if (currentRecorder != null)
        {
            currentRecorder.hasAnimation = true;
            currentRecorder.animationIntent = new AnimationIntent
            {
                type = AnimationIntentType.Bury,
                targetCard = card,
                onAnimationComplete = () =>
                {
                    // Events that were previously raised after animation
                    GameEventStorage.me.onMeBuried.RaiseSpecific(card);
                    GameEventStorage.me.onAnyCardBuried.Raise();
                    // ... faction events ...
                }
            };
        }
    }
    
    combatManager.visuals.UpdateAllPhysicalCardTargets();
}
```

Wait - there's a problem here. The `onMeBuried` event currently triggers MORE effects (like Spike_Skeleton's damage). In the proposed system, those effects must execute during the **Logic Phase** to build the tree. But if we put the event raise in `onAnimationComplete`, it would execute during the **Animation Phase**, which is wrong.

This is a critical design issue.

---

## 5. Critical Design Issues

### 5.1 Issue: Events Must Fire During Logic Phase

In the current system, `BuryEffect` raises `onMeBuried` AFTER the bury animation completes:

```csharp
combatManager.visuals.MoveCardToBottom(card, duration: 0.5f, useArc: true, onComplete: () =>
{
    // Events raised here, during animation
    GameEventStorage.me.onMeBuried.RaiseSpecific(card);
});
```

In the proposed system, if we move these events to `onAnimationComplete`, they would fire during the Animation Phase. But those events trigger new effects that need to be in the tree!

**Resolution**: Events must fire during the Logic Phase, but the animation that precedes them must be deferred.

Revised BuryEffect:
```csharp
private void BuryChosenCards(List<GameObject> cardsToBury, int amount)
{
    // 1. Modify logical deck
    // 2. Capture animation intent
    // 3. RAISE EVENTS IMMEDIATELY (logic phase)
    foreach (var buriedCard in buriedCards)
    {
        GameEventStorage.me.onMeBuried.RaiseSpecific(buriedCard);
        // ... more events ...
    }
    // 4. Animation will play later, but events already fired
}
```

But wait - this changes behavior. Currently, `onMeBuried` listeners can assume the bury animation has already played. If we raise events before animation, the causal order is: logic happens -> tree built -> animations play. The listeners execute synchronously and add to the tree. This is actually correct for the proposed model.

### 5.2 Issue: Same-Frame Event Cascades

If `onMeBuried` raises synchronously and its listener immediately invokes another effect, that effect executes synchronously too. The entire tree is built in one synchronous call stack. This is actually desirable.

But `AnimationStateTracker` currently delays events that fire while animations are playing. In the proposed system, there are NO animations during the Logic Phase, so `AnimationStateTracker` would never delay anything. This simplifies things.

### 5.3 Issue: Combat Log and HP Display Timing

Currently, combat log entries and HP changes happen interleaved with animations:
- Attack animation starts
- At "hit" point, HP changes, log entry appears
- Animation completes

In the proposed system:
- Logic Phase: ALL HP changes happen immediately, ALL log entries appear at once
- Animation Phase: Purely visual replay

**Impact**: Player sees HP drop before seeing the attack animation. This is a significant UX change.

**Mitigation options**:
1. **Accept it**: Under the "animations are visual replay" model, numbers changing first is acceptable
2. **Defer state changes**: Don't apply HP changes in Logic Phase; apply them in `onAnimationHit` during Animation Phase. This is closer to current behavior.
3. **Hybrid**: Apply HP changes in Logic Phase, but delay log entries until animation phase

Option 2 is most faithful to current UX but requires the `AnimationIntent` callbacks to carry the actual state change logic.

### 5.4 Issue: `SameCardDifferentObject` Chain Closing

As shown in Section 3, the current `SameCardDifferentObject` rule closes chains and starts fresh roots. The proposed system needs a unified tree.

**Option A**: Remove `SameCardDifferentObject` closing. Always nest new effects under the current parent.
- Risk: Same card with multiple effects could create deep nesting that doesn't match intended causality

**Option B**: Introduce an explicit "action root" recorder for each player action (reveal, trigger, event response).
- When player reveals Grave_Punch: create root recorder `Grave_Punch_Revealed`
- All effects triggered by this reveal are children of this root
- `SameCardDifferentObject` no longer closes chains; it just creates siblings

**Option B is cleaner** and maps directly to the user's mental model.

### 5.5 Issue: `EffectCanBeInvoked` Loop Guard

The current loop guard checks if the same `cardObject + effectObject` has already been invoked in open chains:

```csharp
public bool EffectCanBeInvoked(string effectID)
{
    foreach (var chain in openedEffectRecorders)
    {
        var wipChainScript = chain.GetComponent<EffectRecorder>();
        if (wipChainScript.cardObject == myCard &&
            wipChainScript.effectObject == myEffect &&
            !string.IsNullOrEmpty(wipChainScript.processedEffectID))
        {
            return false; // already invoked
        }
    }
    // ...
}
```

In the proposed system, if we keep this guard but allow siblings, we need to make sure:
- Grave_Punch's BuryEffect and HPAlterEffect are DIFFERENT effectObjects, so they don't trigger the loop guard
- Eternal_Ghost's damage triggered by Spike_Skeleton vs by Grave_Punch: same card + same effect, but triggered by different parents. Should this be allowed?

If Eternal_Ghost listens to a global damage event (`onAnyCardRevealed` or `onMyPlayerTookDmg`), it fires twice - once from Spike_Skeleton's damage and once from Grave_Punch's damage. The loop guard should allow this because the invocations happen in different causal branches.

But the current guard looks at ALL `openedEffectRecorders` flatly, not considering tree branches. This would block the second Eternal_Ghost invocation if the first is still "open".

**Resolution**: The loop guard should check only along the direct ancestor chain, not all open recorders.

```csharp
public bool EffectCanBeInvoked(string effectID)
{
    var currentRec = currentEffectRecorder.GetComponent<EffectRecorder>();
    var myCard = currentRec.cardObject;
    var myEffect = currentRec.effectObject;
    
    // Check only ancestors, not siblings or cousins
    var ancestor = currentRec.parentRecorder;
    while (ancestor != null)
    {
        if (ancestor.cardObject == myCard &&
            ancestor.effectObject == myEffect &&
            !string.IsNullOrEmpty(ancestor.processedEffectID))
        {
            return false; // loop detected in this branch
        }
        ancestor = ancestor.parentRecorder;
    }
    
    if (chainDepth > 99) return false;
    
    currentRec.processedEffectID = effectID;
    chainDepth++;
    return true;
}
```

---

## 6. Required File Changes Summary

| File | Change Type | Description |
|------|-------------|-------------|
| `EffectRecorder.cs` | Major | Add tree navigation, animation intent, playback state |
| `EffectChainManager.cs` | Major | Remove `SameCardDifferentObject` chain closing; add tree root tracking; ancestor-only loop guard; trigger animation playback on close |
| `EffectAnimationPlayer.cs` | New | Singleton that traverses recorder tree and plays animations sequentially |
| `HPAlterEffect.cs` | Major | Capture `AnimationIntent` instead of calling `RaiseDamageDealtEvent` |
| `BuryEffect.cs` | Major | Capture `AnimationIntent` instead of calling `MoveCardToBottom` with callback events; raise events immediately in logic phase |
| `StageEffect.cs` | Major | Same as BuryEffect |
| `CombatUXManager.cs` | Minor | Keep animation implementations but call them from `EffectAnimationPlayer` instead of directly from effects |
| `AttackAnimationManager.cs` | Minor | Provide coroutine-based API for `EffectAnimationPlayer` to yield on |
| `AnimationStateTracker.cs` | Remove/Deprecate | No longer needed if animations are purely sequential |
| `GameEvent.cs` | Minor | Remove `AnimationStateTracker.TryExecute` wrapping (events fire synchronously during logic phase) |
| `CostNEffectContainer.cs` | Minor | May need to handle action-root recorder creation |

---

## 7. Pros and Cons

### Pros
1. **Deterministic animation order**: Animations always play in exact causal order, never overlapping unexpectedly
2. **Complete visual causality**: Players can clearly see "A caused B caused C" because animations play strictly in that sequence
3. **Simpler mental model**: One tree = one animation sequence
4. **No `AnimationStateTracker` complexity**: No global reference counter, no delayed event queue, no flush cycles
5. **Easier to debug**: Can inspect the entire EffectRecorder tree before any animation plays
6. **Supports pause/rewind**: Since animations are decoupled from logic, could potentially replay or skip animations
7. **No race conditions**: Events and animations are in separate phases, no interleaving

### Cons
1. **Major refactoring**: Nearly every effect script and animation system needs modification
2. **UX change**: HP/logic changes may appear before their associated animations (unless deferred)
3. **Longer perceived wait**: All logic completes instantly, then a long animation sequence plays. Player sees "nothing happens" -> "everything animates"
4. **Lose parallel animations**: Currently Grave_Punch's attack and bury can animate simultaneously. In proposed system, they animate sequentially.
5. **Event timing changes**: Effects that rely on `onMeBuried` firing AFTER visual bury would now fire BEFORE
6. **Tree memory overhead**: Must hold entire tree in memory before playing animations
7. **Complex edge cases**: What if an effect needs to know animation result? (e.g., "if target is still alive after animation...")

---

## 8. Alternative: Hybrid Approach (Recommended for Investigation)

Instead of a full two-phase separation, consider a **lightweight recorder-driven queue**:

1. Keep effect execution mostly as-is (immediate)
2. When an effect wants to play an animation, instead of playing it, **emit an `AnimationRequest` tied to the current EffectRecorder**
3. An `AnimationRequest` can be:
   - `Immediate`: play right now (for same-card parallel animations like attack + bury)
   - `Sequential`: wait for parent's animation to complete before playing
4. Maintain a flat queue of `AnimationRequest` ordered by recorder depth-first traversal
5. `AnimationStateTracker` is replaced by a simple queue processor

This preserves current parallel behavior for same-card effects while still serializing cross-card animations by causality.

### Hybrid Architecture

```csharp
public class AnimationRequest
{
    public EffectRecorder recorder;
    public AnimationIntent intent;
    public AnimationSequenceMode mode;
}

public enum AnimationSequenceMode
{
    ParallelWithParent,  // Same card: play simultaneously with parent
    AfterParent,         // Different card: wait for parent to finish
    AfterAllSiblings     // Wait for all siblings to finish
}
```

**Rules for mode assignment**:
- If `request.recorder.cardObject == request.recorder.parentRecorder.cardObject`:
  - `mode = ParallelWithParent` (same card's effects animate together)
- Else:
  - `mode = AfterParent` (caused-by effects animate after causer)

**Playback**:
```
1. Grave_Punch BuryEffect request -> mode=AfterParent (no parent, so play immediately)
   - Start Spike_Skeleton bury animation
2. Grave_Punch HPAlterEffect request -> mode=ParallelWithParent (same card as BuryEffect)
   - Start Grave_Punch attack animation (concurrent with bury)
3. Spike_Skeleton HPAlterEffect request -> mode=AfterParent (Spike_Skeleton != Grave_Punch)
   - Queue until Grave_Punch bury finishes, then play
4. Eternal_Ghost (from Spike_Skeleton) request -> mode=AfterParent
   - Queue until Spike_Skeleton attack finishes, then play
5. Eternal_Ghost (from Grave_Punch) request -> mode=AfterParent
   - Queue until Grave_Punch attack finishes, then play
```

This gives the user's desired sequence:
1. Spike_Skeleton bury + Grave_Punch attack (parallel)
2. Spike_Skeleton attack
3. Eternal_Ghost attack (from Spike_Skeleton)
4. Eternal_Ghost attack (from Grave_Punch)

Actually, the user's desired sequence was:
1. Spike_Skeleton bury
2. Spike_Skeleton attack
3. Eternal_Ghost attack (from Spike_Skeleton)
4. Grave_Punch attack
5. Eternal_Ghost attack (from Grave_Punch)

The hybrid approach makes #1 and #4 parallel. If strict sequentiality is desired even for same-card effects, use `AfterParent` for all.

---

## 9. Conclusion

### Feasibility
The proposed system is **feasible but requires extensive refactoring** (~8-10 core files).

### Current System Can Almost Produce the Tree
The current `EffectChainManager` already builds a tree structure via `currentEffectRecorderParent` and Transform parenting. The main blocker is:
1. `SameCardDifferentObject` closes chains, splitting the tree
2. Effects immediately trigger animations, interleaving with tree construction
3. `AnimationStateTracker` delays events based on animation timing, not tree structure

### Minimal Change Path
If the goal is specifically "animate by EffectRecorder order," the smallest change would be:
1. **Remove `SameCardDifferentObject` chain closing** -> unified tree per player action
2. **Add `AnimationIntent` to `EffectRecorder`** -> capture animation metadata during effect execution
3. **Defer animation start** -> don't play animations during `effectEvent.Invoke()`; instead queue them in the recorder
4. **Play animations on `CloseOpenedChain()`** -> traverse the closed tree depth-first and play captured animations
5. **Remove `AnimationStateTracker`** -> events fire synchronously during logic; animations play later

This is still a significant change but conceptually clean.

### Risk Assessment
| Risk | Severity |
|------|----------|
| Extensive refactoring across effect system | High |
| UX change (logic before animation) | Medium |
| Loss of parallel same-card animations | Medium |
| Event timing changes breaking card interactions | High |
| Edge cases in loop guard with tree branches | Medium |

### Recommendation
If the team decides to pursue this, start with:
1. A **spike/prototype** on a single card interaction (e.g., Bury -> onMeBuried -> Stage)
2. Implement only `BuryEffect` and `HPAlterEffect` with `AnimationIntent` capture
3. Build a simple `EffectAnimationPlayer` that handles just these two animation types
4. Run the existing test suite to measure behavioral differences
5. Iterate on the hybrid approach if full sequentiality feels too slow
