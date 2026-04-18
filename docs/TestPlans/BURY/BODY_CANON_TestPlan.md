# BODY_CANON Test Plan

## Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/Bury and buried/BODY_CANON.prefab` |
| **Card Type ID** | `BODY_CANON` |
| **Description** | 埋葬所有友方 |
| **Is Minion** | False |
| **Trigger Event** | `onMeRevealed` |

---

## Implementation Chain

1. **Card Revealed**: `GameEventListener` on root detects `onMeRevealed` and invokes both child `CostNEffectContainer`s.
2. **Container 1 - "bury 12 friendly cards"**: No cost check. `BuryEffect.BuryMyCards(12)` shuffles and buries up to 12 eligible friendly cards.
3. **Container 2 - "deal 3 dmg x friendly after start card"**: No cost check. `ValueTrackerManager.UpdateAllTrackers()` refreshes `FriendlyInGraveAmountRef`. `HPAlterEffect.DecreaseTheirHpTimesIntSO(FriendlyInGraveAmountRef)` deals damage repeatedly.
4. **Final Result**: Opponent receives `3 * N` damage (where `N = FriendlyInGraveAmountRef.value`), and up to 12 friendly cards are moved to the bottom of the deck.

### Effect Formula

```
BuryMyCards(12):
  eligibleFriendlyCards = combinedDeckZone
    .Where(card => !ShouldSkipEffectProcessing)
    .Where(card => card.owner == this.owner)
    .Where(card => !card.IsAtBottom)
    .Where(card => !card.isMinion)
  shuffle(eligibleFriendlyCards)
  buryCount = min(12, eligibleFriendlyCards.Count)

DecreaseTheirHpTimesIntSO(FriendlyInGraveAmountRef):
  damagePerHit = baseDmg.value(2) + extraDmg(1) + powerStacksOnSelf
  hitCount = FriendlyInGraveAmountRef.value
  totalDamage = damagePerHit * hitCount
```

> **Note:** `FriendlyInGraveAmountRef` counts friendly cards with index **smaller** than the Start Card's index in `combinedDeckZone`. Burying cards moves them to index 0 (bottom), which may change this count for subsequent effects if trackers are refreshed.

### Important Implementation Details

- **Execution Order**: Two containers are siblings under the root. UnityEvent invocation order follows sibling order (top-to-bottom in Hierarchy). Container 1 (bury) runs before Container 2 (damage). `UpdateAllTrackers()` inside Container 2 will recalculate `FriendlyInGraveAmountRef` **after** the bury operation.
- **Bury Filter**: `BuryMyCards` excludes minions, neutral cards, cards already at the bottom, and enemy cards.
- **Double-Application Check**: `baseDmg = 2`, `extraDmg = 1`. `DecreaseTheirHp()` adds them automatically. Each hit deals `3` base damage.
- **Power Interaction**: If BODY_CANON has Power status effects, each hit gains `+1` damage.
- **Empty Deck Edge Case**: If no friendly cards are eligible, bury does nothing. If `FriendlyInGraveAmountRef = 0`, damage container fires zero times.

---

## Test Strategies

### Strategy A: Programmatic Unit Test (Editor Mode) - Recommended

Use `unity-MCP` `execute_code` to simulate combat state directly without entering Play Mode or waiting for animations.

#### Steps
1. Load the prefab via `AssetDatabase.LoadAssetAtPath<GameObject>`.
2. Create or locate a `CombatManager` instance and initialize player statuses.
3. Construct a controlled `combinedDeckZone` and place the instantiated card at the top (reveal zone or last index).
4. Call `ValueTrackerManager.me?.UpdateAllTrackers()`.
5. Invoke each `CostNEffectContainer.InvokeEffectEvent()` directly.
6. Assert deck state, buried counts, and opponent HP.

#### Test Cases

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | Base case: 3 friendly cards, 1 enemy, Start Card in middle | combinedDeckZone = [Start, Enemy, FriendlyA, FriendlyB, FriendlyC] (bottom to top). Card at top. | Buries 3 friendly cards to bottom. FriendlyInGraveAmountRef = 0 (all friendly above Start). Total damage = 0. | Bury count capped by eligible cards; damage scales with grave count. |
| A-2 | Friendly cards below Start Card | combinedDeckZone = [FriendlyA, Start, Enemy, FriendlyB] (bottom to top). Card at top. | Buries FriendlyA only (1 card). FriendlyInGraveAmountRef = 1. Damage = 3 * 1 = 3. | Grave counting excludes cards above Start Card. |
| A-3 | More than 12 friendly cards eligible | 15 friendly cards in deck, none at bottom, no minions. | Buries exactly 12 friendly cards. Damage = 3 * graveCount (after bury). | Bury amount clamps at parameter (12). |
| A-4 | Zero eligible friendly cards | Deck contains only enemy cards and Start Card. | Bury does nothing. FriendlyInGraveAmountRef = 0. Damage = 0. | Graceful no-op when cost/target is missing. |
| A-5 | Card has 2 Power status effects | Same as A-2, but BODY_CANON has 2 Power stacks. | Damage per hit = 3 + 2 = 5. Total = 5 * graveCount. | Power correctly adds +1 per stack to every hit. |
| A-6 | Buried cards include minions | Deck has 3 friendly cards, 2 of which are minions. | Bury skips minions. Only 1 non-minion friendly is buried. | Minion filter works in BuryMyCards. |
| A-7 | Enemy perspective (card belongs to enemy) | Instantiate card for enemy player. Deck mirrors A-2. | Buries enemy's own friendly cards. Damages player (owner). | Faction perspective flips correctly. |

#### Pros
- Fast execution (seconds).
- No dependency on UI or animation timing.
- Easy to iterate and debug.

---

### Strategy B: Play Mode Integration Test

Run the actual combat scene and verify the full player-interaction flow.

#### Steps
1. Open the Combat scene (or transition from Shop to Combat).
2. Ensure the test deck contains BODY_CANON and a mix of friendly/enemy cards.
3. Enter Play Mode and advance until BODY_CANON is revealed.
4. Record the relevant game state before and after the effect triggers.
5. Cross-reference the observed result with the expected logic.
6. Use `read_console` to capture `effectResultString` logs.
7. Optionally use `manage_camera(screenshot)` to capture UI evidence.

#### What to Verify
- Event binding: `onMeRevealed` correctly triggers both `CostNEffectContainer`s.
- Animation: bury animation plays for moved cards; attack animation plays for damage.
- Log output: damage numbers match `3 * graveCount`; bury messages list correct cards.
- State consistency: `combinedDeckZone` order is valid after bury; no duplicate or missing cards.
- Tracker reset: `ownerCardsBuriedCountRef` and `enemyCardsBuriedCountRef` reset to 0 after shuffle.

---

### Strategy C: Regression Batch Test (Optional)

Batch-read all prefabs in the `Bury and buried` folder to verify consistent field configurations (e.g., `baseDmg` / `extraDmg` pairs, missing event bindings, incorrect cost fields).

---

## Recommended Execution Order

1. **Run Strategy A first.** Catch arithmetic and logic bugs immediately without scene overhead.
2. **Run Strategy B second.** Validate the full gameplay integration once core logic is confirmed.

---

## Quick Reference: Key Scripts

| Script | Path | Responsibility |
|--------|------|----------------|
| `HPAlterEffect` | `Assets/Scripts/Effects/HPAlterEffect.cs` | Damage calculation and delivery |
| `BuryEffect` | `Assets/Scripts/Effects/BuryEffect.cs` | Bury cards to bottom of deck |
| `ValueTrackerManager` | `Assets/Scripts/Managers/ValueTrackerManager.cs` | Tracks `FriendlyInGraveAmountRef` |
| `CostNEffectContainer` | `Assets/Scripts/Card/CostNEffectContainer.cs` | Cost checking and effect invocation |
| `CombatManager` | `Assets/Scripts/Managers/CombatManager.cs` | Manages `combinedDeckZone` and `revealZone` |
| `GameEventStorage` | `Assets/Scripts/Managers/GameEventStorage.cs` | Centralized GameEvent references |
