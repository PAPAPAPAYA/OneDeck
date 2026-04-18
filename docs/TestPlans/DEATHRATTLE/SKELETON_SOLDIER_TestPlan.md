# SKELETON_SOLDIER Test Plan

## Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/Bury and buried/DeathRattle/SKELETON_SOLDIER.prefab` |
| **Card Type ID** | `SKELETON_SOLDIER` |
| **Description** | When buried: stage self to top |
| **Is Minion** | False |
| **Trigger Event** | OnMeRevealed / OnMeBuried |
| **Tags** | Death Rattle |

---

## Implementation Chain

1. **OnMeRevealed** -> `CostNEffectContainer.InvokeEffectEvent()` on child "deal dmg"
   - `HPAlterEffect.DecreaseTheirHp()` with `baseDmg=2`, `extraDmg=1` -> **3 damage total**
2. **OnMeBuried** -> `CostNEffectContainer.InvokeEffectEvent()` on child "stage self"
   - `StageEffect.StageSelf()` -> moves self to top of deck

### Effect Formula

```
Reveal: damage = baseDmg.value(2) + extraDmg(1) + Power stacks on self = 3 + Power stacks
Burial (DeathRattle): Self is moved to the top of the deck
```

> **Note:** `StageSelf` is a no-op if the card is already at the top of the deck (`index == Count - 1`).

### Important Implementation Details

- Reveal damage uses standard `DecreaseTheirHp()` formula
- Burial effect stages self regardless of card ownership (always stages this specific card)
- `StageSelf` does not exclude Minions for self (it directly stages `myCard`)
- If self is already at top, the method returns early with no animation
- No cost checks on either container

---

## Test Strategies

### Strategy A: Programmatic Unit Test (Editor Mode) - Recommended

Use `unity-MCP` `execute_code` to simulate combat state directly without entering Play Mode or waiting for animations.

#### Steps
1. Load the prefab via `AssetDatabase.LoadAssetAtPath<GameObject>`.
2. Create or locate a `CombatManager` instance and initialize player statuses.
3. Set opponent HP to a known value.
4. Instantiate SKELETON_SOLDIER for the owner player and place it in `revealZone`.
5. Trigger `OnMeRevealed` and assert 3 damage dealt.
6. Place card in `combinedDeckZone` at a non-top position, then trigger `OnMeBuried`.
7. Assert card is now at top index (`Count - 1`).

#### Test Cases

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | Reveal - base damage | No Power on card | Opponent takes 3 damage | baseDmg(2) + extraDmg(1) = 3 |
| A-2 | Burial - stage self | Card at index 2 in 6-card deck | Card moves to index 5 (top) | StageSelf works |
| A-3 | Burial - already at top | Card at index 5 in 6-card deck | Card stays at index 5, no error | IsCardAtTop early return |
| A-4 | Burial - at bottom | Card at index 0 in 6-card deck | Card moves to index 5 | Works from any position |
| A-5 | Enemy perspective | Card owned by enemy | Enemy's opponent takes damage, self stages for enemy | Faction perspective correct |
| A-6 | Full cycle | Reveal then bury | 3 dmg, then self staged to top | Both events work |
| A-7 | Burial - single card deck | Only SKELETON_SOLDIER in deck | Card is already at top, no change | Edge case: 1-card deck |

#### Pros
- Fast execution (seconds).
- No dependency on UI or animation timing.
- Easy to iterate and debug.

---

### Strategy B: Play Mode Integration Test

Run the actual combat scene and verify the full player-interaction flow.

#### Steps
1. Open the Combat scene.
2. Ensure the test deck contains SKELETON_SOLDIER.
3. Enter Play Mode and reveal the card.
4. Verify opponent takes 3 damage.
5. Continue play until the card is buried.
6. Verify the card moves to the top of the deck visually.
7. If the card is revealed again quickly, verify it is indeed the next revealed card.

#### What to Verify
- Event binding: `OnMeRevealed` triggers damage, `OnMeBuried` triggers self-stage.
- Animation: attack animation for damage, arc animation for staging.
- Log output: damage and staging messages appear.
- State consistency: deck order is valid after execution.

---

### Strategy C: Regression Batch Test (Optional)

Compare with GRAVE_PORTAL (also has stage effect) to verify consistent `StageEffect` behavior.

---

## Recommended Execution Order

1. **Run Strategy A first.** Catch arithmetic and logic bugs immediately without scene overhead.
2. **Run Strategy B second.** Validate the full gameplay integration once core logic is confirmed.

---

## Quick Reference: Key Scripts

| Script | Path | Responsibility |
|--------|------|----------------|
| `HPAlterEffect` | `Assets/Scripts/Effects/HPAlterEffect.cs` | Damage calculation and delivery |
| `StageEffect` | `Assets/Scripts/Effects/StageEffect.cs` | Staging logic, `StageSelf` |
| `CostNEffectContainer` | `Assets/Scripts/Card/CostNEffectContainer.cs` | Cost checking and effect invocation |
| `CombatManager` | `Assets/Scripts/Managers/CombatManager.cs` | Manages `combinedDeckZone` and `revealZone` |
| `GameEventStorage` | `Assets/Scripts/Managers/GameEventStorage.cs` | Centralized GameEvent references |
