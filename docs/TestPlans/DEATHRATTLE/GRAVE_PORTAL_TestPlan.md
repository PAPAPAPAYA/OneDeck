# GRAVE_PORTAL Test Plan

## Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/Bury and buried/DeathRattle/GRAVE_PORTAL.prefab` |
| **Card Type ID** | `GRAVE_PORTAL` |
| **Description** | Stage 1 friendly card |
| **Is Minion** | False |
| **Trigger Event** | OnMeRevealed / OnMeBuried |
| **Tags** | Death Rattle |

---

## Implementation Chain

1. **OnMeRevealed** -> `CostNEffectContainer.InvokeEffectEvent()` on child "stage friendly"
2. **OnMeBuried** -> `CostNEffectContainer.InvokeEffectEvent()` on child "stage friendly (1)"
3. Both invoke `StageEffect.StageMyCards(int amount)`
   - Reveal: `StageMyCards(1)`
   - Burial: `StageMyCards(2)`

### Effect Formula

```
Reveal: Stage 1 random friendly non-Minion card to top of deck
Burial (DeathRattle): Stage 2 random friendly non-Minion cards to top of deck
```

> **Note:** `StageMyCards` excludes Minions, neutral/Start Cards (via `ShouldSkipEffectProcessing`), and cards already at the top. Eligible cards are shuffled before selection.

### Important Implementation Details

- `StageMyCards` filters by `myStatusRef == myCardScript.myStatusRef` (same owner)
- Minion cards (`isMinion == true`) are excluded from staging
- Cards already at the top of the deck (`index == Count - 1`) are excluded
- The `DeathRattle` tag means the burial effect triggers when the card is buried
- No cost checks on either container (no `checkCostEvent` bindings)

---

## Test Strategies

### Strategy A: Programmatic Unit Test (Editor Mode) - Recommended

Use `unity-MCP` `execute_code` to simulate combat state directly without entering Play Mode or waiting for animations.

#### Steps
1. Load the prefab via `AssetDatabase.LoadAssetAtPath<GameObject>`.
2. Create or locate a `CombatManager` instance and initialize player statuses.
3. Construct a controlled `combinedDeckZone` with a mix of friendly, enemy, Minion, and neutral cards.
4. Instantiate the target card for the owner player and place it in `revealZone`.
5. Trigger `GameEventStorage.me.onMeRevealed.RaiseSpecific(card)`.
6. Verify exactly 1 friendly non-Minion card is moved to the top.
7. Reset deck, bury the card, trigger `GameEventStorage.me.onMeBuried.RaiseSpecific(card)`.
8. Verify exactly 2 friendly non-Minion cards are moved to the top.

#### Test Cases

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | Reveal - normal case | 6 friendly non-Minion, 3 enemy, 1 Minion | 1 friendly card staged to top | StageMyCards(1) works |
| A-2 | Burial - normal case | 6 friendly non-Minion, 3 enemy, 1 Minion | 2 friendly cards staged to top | DeathRattle triggers StageMyCards(2) |
| A-3 | Reveal - no eligible friendly cards | 0 friendly non-Minion (only enemy + Minions) | Nothing staged, no error | Graceful empty-deck handling |
| A-4 | Burial - only 1 eligible friendly card | 1 friendly non-Minion, 5 enemy | 1 card staged (clamped to available count) | Clamp works correctly |
| A-5 | Reveal - self already at top | 3 friendly, card itself is at top index | Card remains at top, other friendly may be staged | IsCardAtTop exclusion works |
| A-6 | Burial - minions excluded | 2 friendly Minions, 2 friendly non-Minions | 2 non-Minions staged, Minions ignored | Minion filter works |
| A-7 | Enemy perspective | Card owned by enemy, 4 enemy non-Minions | Stages enemy-owned cards | Faction perspective correct |

#### Pros
- Fast execution (seconds).
- No dependency on UI or animation timing.
- Easy to iterate and debug.

---

### Strategy B: Play Mode Integration Test

Run the actual combat scene and verify the full player-interaction flow.

#### Steps
1. Open the Combat scene (or transition from Shop to Combat).
2. Ensure the test deck contains GRAVE_PORTAL and a mix of friendly/enemy/Minion cards.
3. Enter Play Mode and reveal GRAVE_PORTAL.
4. Record which card is staged to the top.
5. Continue play until GRAVE_PORTAL is buried.
6. Verify 2 friendly cards are staged to the top.

#### What to Verify
- Event binding: `OnMeRevealed` triggers the correct container.
- `OnMeBuried` triggers the DeathRattle container.
- Animation: arc trajectory animation plays for staged cards.
- Log output: staging messages appear in `effectResultString`.
- State consistency: deck order is valid after execution.

---

### Strategy C: Regression Batch Test (Optional)

Compare GRAVE_PORTAL with UNSTABLE_PORTAL (similar reveal effect) to verify consistent field configurations.

---

## Recommended Execution Order

1. **Run Strategy A first.** Catch arithmetic and logic bugs immediately without scene overhead.
2. **Run Strategy B second.** Validate the full gameplay integration once core logic is confirmed.

---

## Quick Reference: Key Scripts

| Script | Path | Responsibility |
|--------|------|----------------|
| `StageEffect` | `Assets/Scripts/Effects/StageEffect.cs` | Staging logic, `StageMyCards` |
| `CostNEffectContainer` | `Assets/Scripts/Card/CostNEffectContainer.cs` | Cost checking and effect invocation |
| `CombatManager` | `Assets/Scripts/Managers/CombatManager.cs` | Manages `combinedDeckZone` and `revealZone` |
| `GameEventStorage` | `Assets/Scripts/Managers/GameEventStorage.cs` | Centralized GameEvent references |
