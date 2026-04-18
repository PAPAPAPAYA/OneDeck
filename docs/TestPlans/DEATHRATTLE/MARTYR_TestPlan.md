# MARTYR Test Plan

## Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/Bury and buried/DeathRattle/MARTYR.prefab` |
| **Card Type ID** | `MARTYR` |
| **Description** | When buried: all friendly cards gain 1 Power |
| **Is Minion** | False |
| **Trigger Event** | OnMeBuried only |
| **Tags** | Death Rattle |

---

## Implementation Chain

1. **OnMeBuried** -> `CostNEffectContainer.InvokeEffectEvent()` on child "give all friendly power"
2. Invokes `StatusEffectGiverEffect.GiveAllFriendlyStatusEffect(int amount)` with `amount = 1`
3. Iterates all friendly cards in `combinedDeckZone` + `revealZone` and adds `Power` status effect

### Effect Formula

```
Burial (DeathRattle): All friendly cards gain 1 stack of Power
```

> **Note:** `GiveAllFriendlyStatusEffect(1)` adds exactly 1 Power stack per call. The field `statusEffectLayerCount=1` on the component is overridden by the method argument. `includeSelf=False` means the MARTYR card itself does NOT receive Power.

### Important Implementation Details

- Only triggers on `OnMeBuried` (no `OnMeRevealed` listener)
- `target=Me` means it affects cards owned by the same player as MARTYR
- `includeSelf=False` explicitly excludes the MARTYR card itself
- `canStatusEffectBeStacked=True` allows multiple Power stacks to accumulate
- Power stacks are counted in `CardScript.myStatusEffects` list
- No cost checks (no `checkCostEvent` binding)

---

## Test Strategies

### Strategy A: Programmatic Unit Test (Editor Mode) - Recommended

Use `unity-MCP` `execute_code` to simulate combat state directly without entering Play Mode or waiting for animations.

#### Steps
1. Load the prefab via `AssetDatabase.LoadAssetAtPath<GameObject>`.
2. Create or locate a `CombatManager` instance and initialize player statuses.
3. Construct a controlled `combinedDeckZone` with a mix of friendly and enemy cards.
4. Instantiate MARTYR for the owner player.
5. Bury the card and trigger `GameEventStorage.me.onMeBuried.RaiseSpecific(card)`.
6. Count Power stacks on each friendly card.

#### Test Cases

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | Burial - normal case | 4 friendly cards, 3 enemy cards | All 4 friendly cards gain 1 Power | GiveAllFriendlyStatusEffect works |
| A-2 | Burial - self excluded | 1 MARTYR + 3 friendly cards | MARTYR has 0 Power, other 3 have 1 Power | includeSelf=False works |
| A-3 | Burial - no friendly cards | 0 friendly cards (only enemy) | Nothing happens, no error | Graceful empty handling |
| A-4 | Burial - Power stacking | Friendly cards already have 2 Power | Friendly cards now have 3 Power | canStatusEffectBeStacked=True |
| A-5 | Reveal - no effect | MARTYR revealed | No Power applied to any card | No OnMeRevealed listener |
| A-6 | Enemy perspective | MARTYR owned by enemy, 3 enemy cards | Enemy cards gain 1 Power | Faction perspective correct |
| A-7 | Burial - neutral cards excluded | 2 friendly, 1 Start Card, 2 enemy | Start Card gets 0 Power, friendly get 1 | ShouldSkipEffectProcessing excludes neutral |

#### Pros
- Fast execution (seconds).
- No dependency on UI or animation timing.
- Easy to iterate and debug.

---

### Strategy B: Play Mode Integration Test

Run the actual combat scene and verify the full player-interaction flow.

#### Steps
1. Open the Combat scene.
2. Ensure the test deck contains MARTYR and multiple friendly cards.
3. Enter Play Mode and advance until MARTYR is revealed.
4. Verify no Power is applied on reveal.
5. Continue play until MARTYR is buried.
6. Verify all friendly cards (except MARTYR) gain 1 Power.
7. Check for Power particle effect (`PS_PowerWispWIP`).

#### What to Verify
- Event binding: only `OnMeBuried` triggers the effect.
- Power stacks are visible on card UI.
- Log output shows Power application messages.
- State consistency: `myStatusEffects` lists are valid after execution.

---

### Strategy C: Regression Batch Test (Optional)

Compare with other `GiveAllFriendlyStatusEffect` cards (e.g., GRAVE_KEEPER if applicable) to verify consistent `includeSelf` and `canStatusEffectBeStacked` configurations.

---

## Recommended Execution Order

1. **Run Strategy A first.** Catch arithmetic and logic bugs immediately without scene overhead.
2. **Run Strategy B second.** Validate the full gameplay integration once core logic is confirmed.

---

## Quick Reference: Key Scripts

| Script | Path | Responsibility |
|--------|------|----------------|
| `StatusEffectGiverEffect` | `Assets/Scripts/Effects/StatusEffect/StatusEffectGiverEffect.cs` | Applies status effects to target cards |
| `CostNEffectContainer` | `Assets/Scripts/Card/CostNEffectContainer.cs` | Cost checking and effect invocation |
| `CombatManager` | `Assets/Scripts/Managers/CombatManager.cs` | Manages `combinedDeckZone` and `revealZone` |
| `GameEventStorage` | `Assets/Scripts/Managers/GameEventStorage.cs` | Centralized GameEvent references |
