# GRAVE_INVITATION Test Plan

## Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/Bury and buried/GRAVE_INVITATION.prefab` |
| **Card Type ID** | `GRAVE_INVITATION` |
| **Description** | 造成 4 伤害 |
| **Is Minion** | False |
| **Trigger Event** | `onMeRevealed` |

---

## Implementation Chain

1. **Card Revealed**: `GameEventListener` on root detects `onMeRevealed` and invokes the `CostNEffectContainer`.
2. **Cost Check**: None.
3. **Pre-Effect**: None.
4. **Effect Execution**:
   - `HPAlterEffect.DecreaseTheirHp()` deals base damage (`baseDmg = 2`) + extra damage (`extraDmg = 2`) = 4 damage to opponent.
   - `BuryEffect.BuryTheirCards_BasedOnIntSO(FriendlyInGraveAmountRef)` buries N enemy cards, where N equals the number of friendly cards below the Start Card.
5. **Final Result**: Opponent loses 4 HP (before shield), and up to N enemy cards are moved to the bottom of the deck.

### Effect Formula

```
DecreaseTheirHp():
  totalDmg = baseDmg.value(2) + extraDmg(2) + powerStacksOnSelf
  target = theirStatusRef (opponent)

BuryTheirCards_BasedOnIntSO(FriendlyInGraveAmountRef):
  N = FriendlyInGraveAmountRef.value
  eligibleEnemyCards = combinedDeckZone
    .Where(card => !ShouldSkipEffectProcessing)
    .Where(card => card.owner != this.owner)
    .Where(card => !card.IsAtBottom)
    .Where(card => !card.isMinion)
  shuffle(eligibleEnemyCards)
  buryCount = min(N, eligibleEnemyCards.Count)
```

> **Note:** `FriendlyInGraveAmountRef` is refreshed by `ValueTrackerManager.UpdateAllTrackers()` inside `CostNEffectContainer.InvokeEffectEvent()` before the effect fires. The damage and bury are sequential calls within the **same** `effectEvent`, so they share the same tracker snapshot.

### Important Implementation Details

- **Same-Event Ordering**: Damage is registered as Call 0, Bury as Call 1 within the same `UnityEvent`. Damage executes before bury.
- **Bury Target**: Enemy cards only. Minions and cards already at the bottom are excluded.
- **Double-Application Check**: `baseDmg = 2`, `extraDmg = 2`. `DecreaseTheirHp()` adds them automatically. Final damage per hit = 4.
- **Power Interaction**: Each Power stack on GRAVE_INVITATION adds +1 to the damage amount.
- **Zero Grave Case**: If `FriendlyInGraveAmountRef = 0`, the bury call does nothing (amount = 0).

---

## Test Strategies

### Strategy A: Programmatic Unit Test (Editor Mode) - Recommended

Use `unity-MCP` `execute_code` to simulate combat state directly without entering Play Mode or waiting for animations.

#### Steps
1. Load the prefab via `AssetDatabase.LoadAssetAtPath<GameObject>`.
2. Create or locate a `CombatManager` instance and initialize player statuses.
3. Construct a controlled `combinedDeckZone` with known Start Card position and friendly/enemy card distribution.
4. Instantiate GRAVE_INVITATION, assign ownership, and place it in the reveal zone.
5. Call `ValueTrackerManager.me?.UpdateAllTrackers()`.
6. Invoke the `CostNEffectContainer.InvokeEffectEvent()` directly.
7. Assert opponent HP change and deck order.

#### Test Cases

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | Base case: 2 friendly in grave, 3 enemy eligible | combinedDeckZone = [FriendlyA, FriendlyB, Start, EnemyA, EnemyB, EnemyC]. Card in revealZone. | Opponent takes 4 damage. 2 enemy cards are buried to bottom. | Damage is fixed 4; bury count equals grave count. |
| A-2 | No friendly cards in grave | Start Card at index 0 (bottom). All friendly cards above it. | Opponent takes 4 damage. No enemy cards buried. | Zero grave count yields zero bury. |
| A-3 | More grave count than eligible enemy cards | FriendlyInGraveAmountRef = 5, but only 2 eligible enemy cards exist. | Opponent takes 4 damage. Exactly 2 enemy cards buried. | Bury count clamps to eligible target count. |
| A-4 | Enemy cards include minions and bottom cards | 3 enemy cards: 1 minion, 1 at bottom, 1 eligible. FriendlyInGraveAmountRef = 3. | Opponent takes 4 damage. Only 1 enemy card buried. | Minion and bottom filters work for enemy bury. |
| A-5 | Card has 1 Power status effect | Same as A-1, but card has 1 Power. | Opponent takes 5 damage. 2 enemy cards buried. | Power adds +1 damage. |
| A-6 | Enemy perspective | Card belongs to enemy. Deck mirrors A-1. | Player (owner) takes 4 damage. 2 friendly cards buried. | Faction perspective flips correctly for damage and bury. |
| A-7 | Opponent has shield | Enemy has 3 shield, 10 HP. Damage = 4. | Shield reduced to 0, HP reduced to 9. | Shield is consumed before HP. |

#### Pros
- Fast execution (seconds).
- No dependency on UI or animation timing.
- Easy to iterate and debug.

---

### Strategy B: Play Mode Integration Test

Run the actual combat scene and verify the full player-interaction flow.

#### Steps
1. Open the Combat scene (or transition from Shop to Combat).
2. Ensure the test deck contains GRAVE_INVITATION and a mix of friendly/enemy cards with known grave composition.
3. Enter Play Mode and advance until GRAVE_INVITATION is revealed.
4. Record the relevant game state before and after the effect triggers.
5. Cross-reference the observed result with the expected logic.
6. Use `read_console` to capture `effectResultString` logs.
7. Optionally use `manage_camera(screenshot)` to capture UI evidence.

#### What to Verify
- Event binding: `onMeRevealed` correctly triggers the `CostNEffectContainer`.
- Animation: attack animation plays for damage; bury animation plays for moved enemy cards.
- Log output: damage number is 4 (+ Power); bury messages list correct enemy cards.
- State consistency: buried enemy cards appear at the bottom of `combinedDeckZone`.
- Tracker accuracy: `enemyCardsBuriedCountRef` increments correctly.

---

## Recommended Execution Order

1. **Run Strategy A first.** Catch arithmetic and logic bugs immediately without scene overhead.
2. **Run Strategy B second.** Validate the full gameplay integration once core logic is confirmed.

---

## Quick Reference: Key Scripts

| Script | Path | Responsibility |
|--------|------|----------------|
| `HPAlterEffect` | `Assets/Scripts/Effects/HPAlterEffect.cs` | Damage calculation and delivery |
| `BuryEffect` | `Assets/Scripts/Effects/BuryEffect.cs` | Bury enemy cards to bottom of deck |
| `ValueTrackerManager` | `Assets/Scripts/Managers/ValueTrackerManager.cs` | Tracks `FriendlyInGraveAmountRef` |
| `CostNEffectContainer` | `Assets/Scripts/Card/CostNEffectContainer.cs` | Cost checking and effect invocation |
| `CombatManager` | `Assets/Scripts/Managers/CombatManager.cs` | Manages `combinedDeckZone` and `revealZone` |
| `GameEventStorage` | `Assets/Scripts/Managers/GameEventStorage.cs` | Centralized GameEvent references |
