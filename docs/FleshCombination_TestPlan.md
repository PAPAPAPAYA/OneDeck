# Flesh Combination (血肉聚集体) Test Plan

## Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/General/血肉聚集体.prefab` |
| **Card Type ID** | `FLESH_COMBINATION` |
| **Description** | 造成友方数量的 damage |
| **Is Minion** | False |
| **Trigger Event** | `OnMeRevealed` |

---

## Implementation Chain

1. `GameEventListener` listens to `OnMeRevealed` event.
2. When triggered, it calls `CostNEffectContainer.InvokeEffectEvent()`.
3. The effect method invoked is `HPAlterEffect.DecreaseTheirHp_BasedOnFriendlyCardCountInDeck()`.
4. Damage source is `ValueTrackerManager.me.ownerCardCountInDeckRef` (or `enemyCardCountInDeckRef` when used by the enemy).

### Damage Formula

```
Total Damage = friendlyCardCountInDeck + powerStackCount
```

> **Note:** `HPAlterEffect.baseDmg = 2` and `HPAlterEffect.extraDmg = -2` cancel each other out in the default prefab configuration. The final damage is determined purely by the friendly card count.

### Important Implementation Detail

`ValueTrackerManager.UpdateOwnerCardCountInDeck()` only scans `CombatManager.combinedDeckZone`. It **does NOT include `revealZone`**. This means when `血肉聚集体` is revealed and sitting in `revealZone`, it **will not count itself** as a friendly card.

---

## Test Strategies

### Strategy A: Programmatic Unit Test (Editor Mode) - Recommended

Use `unity-MCP` `execute_code` to simulate combat state directly without entering Play Mode or waiting for animations.

#### Steps
1. Load the prefab via `AssetDatabase.LoadAssetAtPath<GameObject>`.
2. Create a temporary `CombatManager` instance (or use existing scene instance).
3. Initialize `combinedDeckZone` and player statuses (`ownerPlayerStatusRef`, `enemyPlayerStatusRef`).
4. Instantiate `血肉聚集体`, assign ownership, and place it into `revealZone`.
5. Populate `combinedDeckZone` with a controlled set of cards.
6. Call `ValueTrackerManager.me.UpdateAllTrackers()` to refresh counts.
7. Manually invoke `HPAlterEffect.DecreaseTheirHp_BasedOnFriendlyCardCountInDeck()`.
8. Assert the opponent's HP reduction matches the expected value.

#### Test Cases

| ID | Scenario | Deck Setup (combinedDeckZone) | Expected Damage | Validation Point |
|----|----------|------------------------------|-----------------|------------------|
| A-1 | Zero friendly cards | Only enemy cards + Start Card | 0 | Boundary: no friendly cards means zero damage |
| A-2 | One friendly card (card itself in revealZone) | 1 other friendly card + Flesh Combination in revealZone | 1 | Card in revealZone must NOT be counted |
| A-3 | Three friendly cards in deck | 3 friendly cards + Flesh Combination in revealZone | 3 | Normal counting logic |
| A-4 | Power buff applied | 2 friendly cards + Flesh Combination has 1 Power stack | 3 | Power status effect correctly adds +1 damage |
| A-5 | Enemy perspective | Enemy owns Flesh Combination, enemy deck has 4 cards | 4 | Perspective switch uses `enemyCardCountInDeckRef` |

#### Pros
- Fast execution (seconds).
- No dependency on UI or animation timing.
- Easy to iterate and debug.

---

### Strategy B: Play Mode Integration Test

Run the actual combat scene and verify the full player-interaction flow including event bindings, animation queue, and combat log output.

#### Steps
1. Open the Combat scene (or transition from Shop to Combat).
2. Ensure the test deck contains `血肉聚集体`.
3. Enter Play Mode and advance turns until the card is revealed.
4. Record the opponent's HP before and after the effect triggers.
5. Cross-reference the damage value with the number of friendly cards visible in the deck.
6. Use `read_console` to capture `effectResultString` logs for evidence.
7. Optionally use `manage_camera(screenshot)` to capture the combat log / HP panel.

#### What to Verify
- Event binding: `OnMeRevealed` correctly triggers the `CostNEffectContainer`.
- Animation: Attack animation plays (unless skipped by `isStatusEffectDamage`).
- Log output: Damage number matches expected friendly card count.
- State consistency: HP, shield, and deck counts remain valid after execution.

---

### Strategy C: Regression Batch Test (Optional Extension)

If testing multiple cards, batch-read all prefabs under `General/` to verify `HPAlterEffect` configurations. Specifically check that `baseDmg` and `extraDmg` pairs are correctly configured so that "friendly card count" is the sole damage determinant.

---

## Recommended Execution Order

1. **Run Strategy A first.** If the core arithmetic is broken, it will be caught immediately without scene overhead.
2. **Run Strategy B second.** Once numbers are confirmed correct, validate the full gameplay integration (events, logs, animations).

---

## Quick Reference: Key Scripts

| Script | Path | Responsibility |
|--------|------|----------------|
| `HPAlterEffect` | `Assets/Scripts/Effects/HPAlterEffect.cs` | Damage calculation and delivery |
| `ValueTrackerManager` | `Assets/Scripts/Managers/ValueTrackerManager.cs` | Tracks `ownerCardCountInDeckRef` / `enemyCardCountInDeckRef` |
| `CostNEffectContainer` | `Assets/Scripts/Card/CostNEffectContainer.cs` | Cost checking and effect invocation |
| `CombatManager` | `Assets/Scripts/Managers/CombatManager.cs` | Manages `combinedDeckZone` and `revealZone` |

---

## Appendix: Skill Reference

For reading serialized prefab data programmatically, use the local skill:
- **Skill Path:** `.agents/skills/unity-read-prefab-serialized/SKILL.md`
- **Key Constraint:** When using `execute_code` with the default `codedom` compiler, avoid file-level `using` statements, string interpolation (`$""`), and null-conditional operators (`?.`).
