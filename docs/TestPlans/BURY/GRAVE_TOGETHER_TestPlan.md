# GRAVE_TOGETHER Test Plan

## Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/Bury and buried/GRAVE_TOGETHER.prefab` |
| **Card Type ID** | `GRAVE_TOGETHER` |
| **Description** | 埋葬 1 友方 |
| **Is Minion** | False |
| **Trigger Event** | `onMeRevealed` |

---

## Implementation Chain

1. **Card Revealed**: `GameEventListener` on root detects `onMeRevealed` and invokes both child `CostNEffectContainer`s in sibling order.
2. **Container 1 - "bury friendly"**: `BuryEffect.BuryMyCards(1)` buries 1 eligible friendly card.
3. **Container 2 - "bury hostile"**: `BuryEffect.BuryTheirCards(2)` buries up to 2 eligible enemy cards.
4. **Final Result**: 1 friendly card and up to 2 enemy cards are moved to the bottom of the deck.

### Effect Formula

```
BuryMyCards(1):
  eligibleFriendlyCards = combinedDeckZone
    .Where(card => !ShouldSkipEffectProcessing)
    .Where(card => card.owner == this.owner)
    .Where(card => !card.IsAtBottom)
    .Where(card => !card.isMinion)
  shuffle(eligibleFriendlyCards)
  buryCount = min(1, eligibleFriendlyCards.Count)

BuryTheirCards(2):
  eligibleEnemyCards = combinedDeckZone
    .Where(card => !ShouldSkipEffectProcessing)
    .Where(card => card.owner != this.owner)
    .Where(card => !card.IsAtBottom)
    .Where(card => !card.isMinion)
  shuffle(eligibleEnemyCards)
  buryCount = min(2, eligibleEnemyCards.Count)
```

> **Note:** Both bury operations are independent. The friendly bury does not affect the enemy bury's target pool (except by changing deck order, but both operate on the same `combinedDeckZone` snapshot within their respective containers).

### Important Implementation Details

- **No Direct Damage**: This card only manipulates deck order; it deals no HP or shield changes.
- **Execution Order**: Friendly bury executes before enemy bury (sibling order).
- **Bury Filters**: Both operations exclude minions, neutral cards, and cards already at the bottom.
- **Randomness**: `ShuffleList` randomizes the selection order. Tests should either mock the RNG or assert on the final deck state rather than specific card identities.
- **Tracker Updates**: Each `BuryChosenCards` call increments `ownerCardsBuriedCountRef` or `enemyCardsBuriedCountRef` respectively.

---

## Test Strategies

### Strategy A: Programmatic Unit Test (Editor Mode) - Recommended

Use `unity-MCP` `execute_code` to simulate combat state directly without entering Play Mode or waiting for animations.

#### Steps
1. Load the prefab via `AssetDatabase.LoadAssetAtPath<GameObject>`.
2. Create or locate a `CombatManager` instance and initialize player statuses.
3. Construct a controlled `combinedDeckZone` with known friendly and enemy card distribution.
4. Place GRAVE_TOGETHER in the reveal zone.
5. Invoke each container's `InvokeEffectEvent()` in sibling order.
6. Assert final deck order and buried counts.

#### Test Cases

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | Base case: 1 friendly, 2 enemy eligible | combinedDeckZone has 1 friendly and 2 enemy eligible cards. | 1 friendly buried to bottom. 2 enemies buried to bottom (order depends on shuffle). | Both bury operations succeed. |
| A-2 | No friendly eligible, 2 enemy eligible | Deck has 0 eligible friendly, 2 eligible enemy. | No friendly bury. 2 enemies buried. | Friendly bury no-op; enemy bury still works. |
| A-3 | 1 friendly eligible, 0 enemy eligible | Deck has 1 eligible friendly, 0 eligible enemy. | 1 friendly buried. No enemy bury. | Enemy bury no-op; friendly bury still works. |
| A-4 | Zero eligible on both sides | Deck contains only Start Card and minions/bottom cards. | No bury operations. | Graceful no-op when no targets exist. |
| A-5 | 1 friendly, 5 enemy eligible | Deck has 1 friendly, 5 enemy eligible. | 1 friendly buried. Exactly 2 enemies buried. | Enemy bury clamps at parameter (2). |
| A-6 | Enemy perspective | Card belongs to enemy. | 1 enemy-friendly card buried. 2 player-friendly cards buried. | Faction perspective flips for both operations. |
| A-7 | Buried count trackers | Start with ownerBuried=0, enemyBuried=0. Bury 1 friendly and 2 enemies. | ownerBuried=1, enemyBuried=2. | ValueTrackerManager counts update correctly. |

#### Pros
- Fast execution (seconds).
- No dependency on UI or animation timing.
- Easy to iterate and debug.

---

### Strategy B: Play Mode Integration Test

Run the actual combat scene and verify the full player-interaction flow.

#### Steps
1. Open the Combat scene (or transition from Shop to Combat).
2. Ensure the test deck contains GRAVE_TOGETHER and a mix of friendly/enemy cards.
3. Enter Play Mode and advance until GRAVE_TOGETHER is revealed.
4. Record the relevant game state before and after the effect triggers.
5. Cross-reference the observed result with the expected logic.
6. Use `read_console` to capture `effectResultString` logs.
7. Optionally use `manage_camera(screenshot)` to capture UI evidence.

#### What to Verify
- Event binding: `onMeRevealed` triggers both containers in correct order.
- Animation: bury animations play for all moved cards.
- Log output: bury messages show correct owner color tags (friendly = #87CEEB, enemy = orange).
- State consistency: moved cards are at the bottom of `combinedDeckZone`.
- Tracker accuracy: `ownerCardsBuriedCountRef` and `enemyCardsBuriedCountRef` reflect the operations.

---

## Recommended Execution Order

1. **Run Strategy A first.** Catch arithmetic and logic bugs immediately without scene overhead.
2. **Run Strategy B second.** Validate the full gameplay integration once core logic is confirmed.

---

## Quick Reference: Key Scripts

| Script | Path | Responsibility |
|--------|------|----------------|
| `BuryEffect` | `Assets/Scripts/Effects/BuryEffect.cs` | Bury friendly and enemy cards |
| `ValueTrackerManager` | `Assets/Scripts/Managers/ValueTrackerManager.cs` | Tracks buried counts |
| `CostNEffectContainer` | `Assets/Scripts/Card/CostNEffectContainer.cs` | Cost checking and effect invocation |
| `CombatManager` | `Assets/Scripts/Managers/CombatManager.cs` | Manages `combinedDeckZone` and `revealZone` |
| `GameEventStorage` | `Assets/Scripts/Managers/GameEventStorage.cs` | Centralized GameEvent references |
