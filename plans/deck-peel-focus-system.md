# Deck Peel Focus System

## Goal
Implement a unified deck focus system where:
1. **Primary animations** (attacker, effect giver): The entire deck shifts forward until the target card's X reaches screen center. Cards in front of the target slide down out of the screen one by one until the target is fully exposed (PeelDeck).
2. **Smart focus transitions**: During chained/queued animations, the deck only peels once. If another card triggers mid-animation, the system either continues peeling or partially restores peeled cards to bring the new target into focus.

> **Note**: Secondary card lift animation (receiver cards lifting up slightly) was planned but **not implemented**.

---

## Architecture

### Core State: `DeckFocusState` (in CombatUXManager)
- `isDeckFocused`: Whether a card is currently in focus
- `currentFocusCard`: The CardScript currently focused
- `deckFocusOffset`: Temporary Vector3 offset applied to deck base position during focus
- `peeledCards`: Ordered list of physical cards that have been slid out (front to back order)

### Key Insight: Unified Position Calculation
Instead of manually controlling every card during focus, we introduce a single `deckFocusOffset` that `CalculatePositionAtIndex()` respects. This means:
- Unpeeled cards continue to follow the standard deck layout math
- `UpdateAllPhysicalCardTargets()` works correctly after focus ends
- Attack animation return positions can be computed normally

---

## Modified Files

### 1. `Assets/Scripts/UXPrototype/CombatUXManager.cs`

#### Serialized Fields
```csharp
[Header("DECK FOCUS / PEEL")]
public Transform deckFocusTargetPos;        // Transform marking where focused card X should align
public float peelSlideDistance = 4f;        // How far peeled cards slide down-left
public float deckShiftDuration = 0.3f;      // Duration for whole-deck shift
public float peelCardDuration = 0.18f;      // Duration for single card peel slide
public float peelStaggerDelay = 0.04f;      // Delay between consecutive peel slides
public float revealCardExitDistance = 6f;   // Distance to move reveal zone card downward when peeling starts
public float secondaryLiftHeight = 0.4f;    // How much receiver cards lift up (NOT IMPLEMENTED)
public float secondaryLiftDuration = 0.25f; // Duration of receiver lift (NOT IMPLEMENTED)
public bool enableLiftCardInDeck = false;   // Enable LiftCardInDeck secondary animation (NOT IMPLEMENTED)
public bool enablePeelDeck = true;          // Enable PeelDeck focus animation during attack
```

#### Runtime State
```csharp
private bool _isDeckFocused = false;
private CardScript _currentFocusCard = null;
private Vector3 _deckFocusOffset = Vector3.zero;
private List<GameObject> _peeledCards = new List<GameObject>();
public bool IsDeckFocused => _isDeckFocused;
```

> **Note**: The originally planned `_peeledCardOriginalPositions` dictionary was not needed; positions are restored via `CalculatePositionAtIndex()`.

#### Modified `CalculatePositionAtIndex`
```csharp
public Vector3 CalculatePositionAtIndex(int index)
{
    var count = physicalCardsInDeck.Count;
    var basePos = physicalCardDeckPos.position + _deckFocusOffset;
    return new Vector3(
        basePos.x + xOffset * (count - 1 - index),
        basePos.y + yOffset * (count - 1 - index),
        basePos.z - zOffset * index
    );
}
```

#### Implemented Methods

**`public IEnumerator FocusOnCardCoroutine(CardScript targetCard)`**
- Returns immediately if target is already focused
- If not focused: calls `StartPeelCoroutine(targetIndex)`
- If focused on different card: calls `TransitionFocusCoroutine(newIndex, currentIndex)`
- Sets `_currentFocusCard = targetCard`, `_isDeckFocused = true`

**`private IEnumerator StartPeelCoroutine(int targetIndex)`**
1. Compute `_deckFocusOffset` such that target card X aligns with `deckFocusTargetPos.position.x`
2. Move reveal zone card out of screen downward (respecting deck offset)
3. For cards with index > targetIndex (in front of target), slide each down-left out of screen with stagger
4. For remaining cards, animate to their new offset positions
5. Store peeled cards in `_peeledCards`

**`private IEnumerator TransitionFocusCoroutine(int newTargetIndex, int currentTargetIndex)`**
1. Recompute ideal `_deckFocusOffset` for new target
2. Determine diff:
   - Cards that should no longer be peeled (index <= newTargetIndex but were peeled): restore them to deck
   - Cards that should now be peeled (index > newTargetIndex but weren't): peel them
3. Play restore/peel animations accordingly in parallel
4. Update `_peeledCards` to new state

**`public IEnumerator RestoreDeckFocusCoroutine()`**
1. Clear `_deckFocusOffset` so cards calculate normal positions
2. Animate all deck cards to final normal positions in parallel
3. Peeled cards return with stagger (reverse order, closest to target first)
4. Reveal zone card returns to reveal position after deck cards start restoring
5. Clear all focus state
6. Call `UpdateAllPhysicalCardTargets()`

**`public int GetPhysicalCardDeckIndex(GameObject physicalCard)`**
- Returns index of physical card in `physicalCardsInDeck`, or -1

#### Guard in `UpdateAllPhysicalCardTargets`
```csharp
if (_isDeckFocused)
{
    return;
}
```
- Prevents interference during peel animation

#### NOT IMPLEMENTED

**`public IEnumerator LiftCardInDeckCoroutine(CardScript targetCard)`**
- For secondary animations (receiver)
- Find physical card, lift it up by `secondaryLiftHeight`
- After delay/callback, lower it back
- Does NOT interfere with PeelDeck focus state
- **Status**: Not implemented

---

### 2. `Assets/Scripts/Managers/AttackAnimationManager.cs`

#### Modified `ProcessQueue()`
```csharp
private IEnumerator ProcessQueue()
{
    _isProcessingQueue = true;

    while (_attackQueue.Count > 0)
    {
        var data = _attackQueue.Dequeue();
        yield return StartCoroutine(PlayAttackAnimationCoroutine(data));
    }

    _isProcessingQueue = false;

    // All attack animations done: restore deck focus
    if (_combatUXManager != null && _combatUXManager.IsDeckFocused)
    {
        yield return StartCoroutine(_combatUXManager.RestoreDeckFocusCoroutine());
    }
}
```

#### Modified `PlayAttackAnimationCoroutine`
1. At start, determine `bool isInRevealZone = _combatManager.revealZone == data.attackerLogicalCard;`
2. If NOT in reveal zone:
   - `yield return StartCoroutine(_combatUXManager.FocusOnCardCoroutine(cardScript));`
   - `startPos = physicalCard.transform.position;` (now at focused position)
3. In `try` block: Phases 1-5 unchanged
4. Phase 6 and `finally` block:
   - If in reveal zone: return to `revealPos` / `revealSize` (existing behavior)
   - If NOT in reveal zone:
     - Compute return position via `_combatUXManager.CalculatePositionAtIndex(index)` (respects focus offset)
     - Return to deck position instead of reveal zone
     - Scale back to original scale

#### Modified `StopAllAttackAnimations`
- Added call to `RestoreDeckFocusCoroutine()` if deck is focused
- Ensures focus state is cleaned up when animations are force-stopped

---

### 3. `Assets/Scripts/Effects/HPAlterEffect.cs`

No direct changes needed. The integration happens through `CombatManager.RaiseDamageDealtEvent` -> `CombatUXManager.HandleDamageDealt` -> `AttackAnimationManager.RequestAttackAnimation`. AttackAnimationManager handles PeelDeck focus automatically when the attacker is not in the reveal zone.

---

## Future Integration Points (not implemented)

- **`LiftCardInDeckCoroutine`**: Secondary animation for receiver cards (status effect targets, buried cards, etc.) — **NOT IMPLEMENTED**
- **`StatusEffectGiverEffect`**: Call `CombatUXManager.me.LiftCardInDeckCoroutine(receiverCard)` when applying status effects to a card in deck — blocked by above
- **`BuryEffect` / `StageEffect`**: Use PeelDeck for the card doing the bury/stage; use LiftCard for cards being buried/staged — partially available (PeelDeck works, LiftCard does not)
- **`CombatUXManager.PlayStatusEffectProjectile`**: Already uses `GetCardWorldPosition()`, automatically uses focused positions

---

## Implementation Order (as completed)

1. ✅ **CombatUXManager.cs**: Added serialized fields, runtime state, `CalculatePositionAtIndex` modification
2. ✅ **CombatUXManager.cs**: Implemented `StartPeelCoroutine`, `RestoreDeckFocusCoroutine`
3. ✅ **CombatUXManager.cs**: Implemented `TransitionFocusCoroutine` (handles mid-animation focus switching)
4. ❌ **CombatUXManager.cs**: `LiftCardInDeckCoroutine` — NOT IMPLEMENTED
5. ✅ **AttackAnimationManager.cs**: Modified `ProcessQueue` and `PlayAttackAnimationCoroutine`
6. ✅ **Testing**: Verified chained attacks peel once and restore after queue drains

---

## Risks & Mitigations

| Risk | Mitigation | Status |
|------|-----------|--------|
| `UpdateAllPhysicalCardTargets` called during peel and snaps cards back | Guard clause: skip update when `_isDeckFocused` | ✅ Implemented |
| Card destroyed while peeled (e.g., Exile during animation) | Null-check in restore loop; remove from `_peeledCards` if null | ✅ Handled implicitly by null checks |
| Deck empty or card not found in `physicalCardsInDeck` | Early return in `FocusOnCardCoroutine` with warning log | ✅ Implemented |
| `DecreaseTheirHpTimesX` enqueues many attacks; peel once, restore once | `ProcessQueue` handles this naturally: peel at first non-reveal attack, restore after queue drains | ✅ Verified |
| StopAllAttackAnimations mid-peel | `StopAllAttackAnimations` calls `RestoreDeckFocusCoroutine` | ✅ Implemented |
| Focus target card is Start Card (neutral) | Start Card is at bottom (index=0); all other cards peel. Valid scenario. | ✅ Works correctly |

---

## Test Cases

1. ✅ **SPIKE_SKELETON**: Buried -> triggers `OnMeBuried` -> `DecreaseTheirHpTimesX(2)`. Deck peels once, 2 attack animations play from focused position, deck restores.
2. ✅ **GOBLIN_CHARGE_TEAM**: Staged -> triggers `OnMeStaged` -> single `DecreaseTheirHp`. Deck peels, attack plays, deck restores.
3. ✅ **ETERNAL_GHOST**: In deck, listens to `OnTheirPlayerTookDmg`. When enemy takes damage from another source, ETERNAL_GHOST's effect triggers. Deck peels to focus ETERNAL_GHOST, attack plays, restores.
4. ✅ **Chained focus switch (deeper)**: Card A triggers attack (peels to A). Before A's attack ends, Card B (deeper in deck) triggers. System continues peeling to expose B. After all attacks, restores.
5. ✅ **Chained focus switch (shallower)**: Card A triggers (peels to A). Card C (shallower than A) triggers. System restores some peeled cards until C reaches center.
6. ✅ **Reveal zone card**: Normal reveal -> attack. No peel occurs. Card returns to reveal zone as before.
7. ❌ **Secondary lift animation**: Status effect applied to a card in deck. Card should lift slightly then return. Not testable (feature not implemented).
