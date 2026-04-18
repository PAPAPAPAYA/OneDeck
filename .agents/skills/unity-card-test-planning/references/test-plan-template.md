# {{CardName}} Test Plan

## Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `{{PrefabPath}}` |
| **Card Type ID** | `{{CardTypeID}}` |
| **Description** | {{CardDescription}} |
| **Is Minion** | {{IsMinion}} |
| **Trigger Event** | {{TriggerEvent}} |

---

## Implementation Chain

1. {{EventListenerDescription}}
2. {{CostCheckDescription}}
3. {{EffectInvocationDescription}}
4. {{FinalResultDescription}}

### Effect Formula

```
{{EffectFormula}}
```

> **Note:** {{FormulaNote}}

### Important Implementation Details

- {{Detail1}}
- {{Detail2}}
- {{Detail3}}

---

## Test Strategies

### Strategy A: Programmatic Unit Test (Editor Mode) - Recommended

Use `unity-MCP` `execute_code` to simulate combat state directly without entering Play Mode or waiting for animations.

#### Steps
1. Load the prefab via `AssetDatabase.LoadAssetAtPath<GameObject>`.
2. Create or locate a `CombatManager` instance and initialize player statuses.
3. Construct a controlled `combinedDeckZone` and `revealZone`.
4. Instantiate the target card, assign ownership, and place it appropriately.
5. Call `ValueTrackerManager.me?.UpdateAllTrackers()` if the effect relies on tracked counts.
6. Invoke the effect method directly (or trigger the bound `UnityEvent`).
7. Assert the resulting state matches expectations.

#### Test Cases

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | {{Scenario1}} | {{Setup1}} | {{Expected1}} | {{Validation1}} |
| A-2 | {{Scenario2}} | {{Setup2}} | {{Expected2}} | {{Validation2}} |
| A-3 | {{Scenario3}} | {{Setup3}} | {{Expected3}} | {{Validation3}} |
| A-4 | {{Scenario4}} | {{Setup4}} | {{Expected4}} | {{Validation4}} |
| A-5 | {{Scenario5}} | {{Setup5}} | {{Expected5}} | {{Validation5}} |

#### Pros
- Fast execution (seconds).
- No dependency on UI or animation timing.
- Easy to iterate and debug.

---

### Strategy B: Play Mode Integration Test

Run the actual combat scene and verify the full player-interaction flow.

#### Steps
1. Open the Combat scene (or transition from Shop to Combat).
2. Ensure the test deck contains the target card.
3. Enter Play Mode and advance until the card is revealed.
4. Record the relevant game state before and after the effect triggers.
5. Cross-reference the observed result with the expected logic.
6. Use `read_console` to capture `effectResultString` logs.
7. Optionally use `manage_camera(screenshot)` to capture UI evidence.

#### What to Verify
- Event binding: the correct `GameEvent` triggers the `CostNEffectContainer`.
- Animation: attack / effect animation plays as expected (or is skipped when flagged).
- Log output: damage / heal / shield numbers match expectations.
- State consistency: HP, shield, and deck counts remain valid after execution.

---

### Strategy C: Regression Batch Test (Optional)

If testing multiple similar cards, batch-read all prefabs in the same folder to verify consistent field configurations (e.g., `baseDmg` / `extraDmg` pairs, missing event bindings, incorrect cost fields).

---

## Recommended Execution Order

1. **Run Strategy A first.** Catch arithmetic and logic bugs immediately without scene overhead.
2. **Run Strategy B second.** Validate the full gameplay integration once core logic is confirmed.

---

## Quick Reference: Key Scripts

| Script | Path | Responsibility |
|--------|------|----------------|
| `HPAlterEffect` | `Assets/Scripts/Effects/HPAlterEffect.cs` | Damage / heal calculation and delivery |
| `ShieldAlterEffect` | `Assets/Scripts/Effects/ShieldAlterEffect.cs` | Shield calculation and delivery |
| `ValueTrackerManager` | `Assets/Scripts/Managers/ValueTrackerManager.cs` | Tracks dynamic deck counts |
| `CostNEffectContainer` | `Assets/Scripts/Card/CostNEffectContainer.cs` | Cost checking and effect invocation |
| `CombatManager` | `Assets/Scripts/Managers/CombatManager.cs` | Manages `combinedDeckZone` and `revealZone` |
| `GameEventStorage` | `Assets/Scripts/Managers/GameEventStorage.cs` | Centralized GameEvent references |
