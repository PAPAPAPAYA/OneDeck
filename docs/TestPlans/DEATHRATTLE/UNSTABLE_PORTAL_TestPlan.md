# UNSTABLE_PORTAL Test Plan

## Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/Bury and buried/DeathRattle/UNSTABLE_PORTAL.prefab` |
| **Card Type ID** | `UNSTABLE_PORTAL` |
| **Description** | Stage 1 friendly card |
| **Is Minion** | False |
| **Trigger Event** | OnMeRevealed / OnMeBuried |
| **Tags** | Death Rattle |

---

## Implementation Chain

1. **OnMeRevealed** -> `CostNEffectContainer.InvokeEffectEvent()` on child "stage friendly"
   - `StageEffect.StageMyCards(1)` -> stages 1 friendly non-Minion card to top
2. **OnMeBuried** -> `CostNEffectContainer.InvokeEffectEvent()` on child "bury hostile"
   - `BuryEffect.BuryTheirCards(2)` -> buries 2 enemy non-Minion cards to bottom

### Effect Formula

```
Reveal: Stage 1 random friendly non-Minion card to top of deck
Burial (DeathRattle): Bury 2 random enemy non-Minion cards to bottom of deck
```

> **Note:** `StageMyCards` excludes Minions, neutral/Start Cards, and cards already at the top. `BuryTheirCards` excludes Minions, neutral/Start Cards, and cards already at the bottom. Both shuffle eligible candidates before selection.

### Important Implementation Details

- Reveal effect stages friendly cards (same owner)
- Burial effect buries enemy cards (opposite owner)
- Both effects exclude Minions and neutral/Start Cards
- `BuryTheirCards` triggers `onAnyCardBuried` and `onFriendlyCardBuried` events for each buried card
- No cost checks on either container
- The card has a dual-purpose DeathRattle: reveal helps self, burial hurts enemy

---

## Test Strategies

### Strategy A: Programmatic Unit Test (Editor Mode) - Recommended

Use `unity-MCP` `execute_code` to simulate combat state directly without entering Play Mode or waiting for animations.

#### Steps
1. Load the prefab via `AssetDatabase.LoadAssetAtPath<GameObject>`.
2. Create or locate a `CombatManager` instance and initialize player statuses.
3. Construct a controlled `combinedDeckZone` with a mix of friendly, enemy, Minion, and neutral cards.
4. Instantiate UNSTABLE_PORTAL for the owner player and place it in `revealZone`.
5. Trigger `OnMeRevealed` and verify exactly 1 friendly non-Minion card is at the top.
6. Reset deck, bury the card, trigger `OnMeBuried`.
7. Verify exactly 2 enemy non-Minion cards are at the bottom (index 0 and 1).

#### Test Cases

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | Reveal - stage friendly | 4 friendly non-Minion, 3 enemy | 1 friendly card staged to top | StageMyCards(1) works |
| A-2 | Burial - bury hostile | 3 friendly, 4 enemy non-Minion | 2 enemy cards buried to bottom | BuryTheirCards(2) works |
| A-3 | Reveal - no friendly targets | 0 friendly non-Minion | Nothing staged, no error | Graceful empty handling |
| A-4 | Burial - no enemy targets | 0 enemy non-Minion | Nothing buried, no error | Graceful empty handling |
| A-5 | Burial - only 1 enemy target | 3 friendly, 1 enemy non-Minion | 1 enemy card buried (clamped) | Clamp works correctly |
| A-6 | Burial - minions excluded | 2 enemy Minions, 2 enemy non-Minions | 2 non-Minions buried, Minions ignored | Minion filter works |
| A-7 | Enemy perspective | Card owned by enemy | Enemy's friendly staged, player's cards buried | Faction perspective correct |
| A-8 | Burial - already at bottom | 2 enemy cards already at bottom | Those cards are not buried again | IsCardAtBottom exclusion |

#### Pros
- Fast execution (seconds).
- No dependency on UI or animation timing.
- Easy to iterate and debug.

---

### Strategy B: Play Mode Integration Test

Run the actual combat scene and verify the full player-interaction flow.

#### Steps
1. Open the Combat scene.
2. Ensure the test deck contains UNSTABLE_PORTAL and a mix of friendly/enemy/Minion cards.
3. Enter Play Mode and reveal UNSTABLE_PORTAL.
4. Verify 1 friendly card is staged to the top.
5. Continue play until UNSTABLE_PORTAL is buried.
6. Verify 2 enemy cards are moved to the bottom.

#### What to Verify
- Event binding: `OnMeRevealed` triggers staging, `OnMeBuried` triggers burying.
- Animation: arc trajectory animations play for both staged and buried cards.
- Log output: staging and burial messages appear in `effectResultString`.
- State consistency: deck order is valid after execution.
- Burial events: `onAnyCardBuried` and `onFriendlyCardBuried` are raised appropriately.

---

### Strategy C: Regression Batch Test (Optional)

Compare UNSTABLE_PORTAL with GRAVE_PORTAL (similar reveal effect) to verify consistent `StageEffect` behavior. Compare burial effect with dedicated burial cards to verify consistent `BuryEffect` behavior.

---

## Recommended Execution Order

1. **Run Strategy A first.** Catch arithmetic and logic bugs immediately without scene overhead.
2. **Run Strategy B second.** Validate the full gameplay integration once core logic is confirmed.

---

## Quick Reference: Key Scripts

| Script | Path | Responsibility |
|--------|------|----------------|
| `StageEffect` | `Assets/Scripts/Effects/StageEffect.cs` | Staging logic, `StageMyCards` |
| `BuryEffect` | `Assets/Scripts/Effects/BuryEffect.cs` | Burying logic, `BuryTheirCards` |
| `CostNEffectContainer` | `Assets/Scripts/Card/CostNEffectContainer.cs` | Cost checking and effect invocation |
| `CombatManager` | `Assets/Scripts/Managers/CombatManager.cs` | Manages `combinedDeckZone` and `revealZone` |
| `GameEventStorage` | `Assets/Scripts/Managers/GameEventStorage.cs` | Centralized GameEvent references |
