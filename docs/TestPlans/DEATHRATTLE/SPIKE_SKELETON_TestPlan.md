# SPIKE_SKELETON Test Plan

## Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/Bury and buried/DeathRattle/SPIKE_SKELETON.prefab` |
| **Card Type ID** | `SPIKE_SKELETON` |
| **Description** | Deal 3 damage |
| **Is Minion** | False |
| **Trigger Event** | OnMeRevealed / OnMeBuried |
| **Tags** | Death Rattle |

---

## Implementation Chain

1. **OnMeRevealed** -> `CostNEffectContainer.InvokeEffectEvent()` on child "deal dmg"
   - `HPAlterEffect.DecreaseTheirHp()` with `baseDmg=2`, `extraDmg=1` -> **3 damage total**
2. **OnMeBuried** -> triggers child "deal dmg (1)" via **two duplicate listeners**
   - Each call: `HPAlterEffect.DecreaseTheirHp()` with `baseDmg=2`, `extraDmg=0` -> **2 damage per call**
   - **Total burial damage: 4** (2 x 2)

### Effect Formula

```
Reveal: damage = baseDmg.value(2) + extraDmg(1) + Power stacks on self = 3 + Power stacks
Burial (DeathRattle): damage = 2 x (baseDmg.value(2) + extraDmg(0) + Power stacks) = 4 + 2*Power stacks
```

> **Warning:** The prefab contains **two duplicate `OnMeBuried` GameEventListeners** both targeting the same `CostNEffectContainer` "deal dmg (1)". This causes the 2-damage effect to fire twice on burial. Verify whether this is intentional ("Deal 2 damage twice") or a prefab duplication bug.

### Important Implementation Details

- Reveal uses standard `DecreaseTheirHp()` with `extraDmg=1`
- Burial uses `DecreaseTheirHp()` with `extraDmg=0` (so only `baseDmg.value(2)` + Power)
- Two `OnMeBuried` listeners on the root prefab both invoke the same `CostNEffectContainer`
- Each burial trigger results in 2 separate damage calculations and animations
- No cost checks on either container
- `isStatusEffectDamage=False` on both HPAlterEffects

---

## Test Strategies

### Strategy A: Programmatic Unit Test (Editor Mode) - Recommended

Use `unity-MCP` `execute_code` to simulate combat state directly without entering Play Mode or waiting for animations.

#### Steps
1. Load the prefab via `AssetDatabase.LoadAssetAtPath<GameObject>`.
2. Create or locate a `CombatManager` instance and initialize player statuses.
3. Set opponent HP to a known value.
4. Instantiate SPIKE_SKELETON for the owner player and place it in `revealZone`.
5. Trigger `OnMeRevealed` and assert 3 damage dealt.
6. Bury the card and trigger `OnMeBuried`.
7. Count how many times `DecreaseTheirHp` is invoked and total damage dealt.

#### Test Cases

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | Reveal - base damage | No Power on card | Opponent takes 3 damage | baseDmg(2) + extraDmg(1) = 3 |
| A-2 | Burial - base damage (duplicate listeners) | No Power on card | Opponent takes 4 damage total | 2 listeners x 2 dmg = 4 |
| A-3 | Burial - with Power | Card has 1 Power | Opponent takes 6 damage total | 2 listeners x (2+1) = 6 |
| A-4 | Reveal - with Power | Card has 2 Power | Opponent takes 5 damage | 3 + 2 = 5 |
| A-5 | Enemy perspective | Card owned by enemy | Enemy's opponent takes damage | Faction perspective correct |
| A-6 | Burial - single listener check | Inspect GameEventListener count | 2 OnMeBuried listeners found | Duplicate listener verification |
| A-7 | Full cycle | Reveal then bury | 3 dmg + 4 dmg = 7 total | Both events contribute damage |

#### Pros
- Fast execution (seconds).
- No dependency on UI or animation timing.
- Easy to iterate and debug.

---

### Strategy B: Play Mode Integration Test

Run the actual combat scene and verify the full player-interaction flow.

#### Steps
1. Open the Combat scene.
2. Ensure the test deck contains SPIKE_SKELETON.
3. Enter Play Mode and reveal the card.
4. Verify opponent takes 3 damage.
5. Continue play until the card is buried.
6. Observe whether the attack animation plays once or twice.
7. Verify total damage dealt on burial matches expectation (4 if duplicate is intentional).

#### What to Verify
- Event binding: `OnMeRevealed` triggers 3 damage; `OnMeBuried` triggers the burial effect.
- Animation: observe if attack animation queues twice (indicating duplicate listener).
- Log output: check if damage log appears twice for burial.
- State consistency: HP remains valid after all damage instances.

---

### Strategy C: Regression Batch Test (Optional)

Compare SPIKE_SKELETON with other DeathRattle damage cards (e.g., REVENGER, SKELETON_SOLDIER) to verify if the duplicate listener is unique to this prefab. If unintended, remove one `OnMeBuried` `GameEventListener` from the prefab.

---

## Recommended Execution Order

1. **Run Strategy A first.** Catch arithmetic and logic bugs immediately without scene overhead.
2. **Run Strategy B second.** Validate the full gameplay integration once core logic is confirmed.
3. **Run Strategy C** to verify the duplicate listener is intentional.

---

## Quick Reference: Key Scripts

| Script | Path | Responsibility |
|--------|------|----------------|
| `HPAlterEffect` | `Assets/Scripts/Effects/HPAlterEffect.cs` | Damage calculation and delivery |
| `CostNEffectContainer` | `Assets/Scripts/Card/CostNEffectContainer.cs` | Cost checking and effect invocation |
| `CombatManager` | `Assets/Scripts/Managers/CombatManager.cs` | Manages `combinedDeckZone` and `revealZone` |
| `GameEventStorage` | `Assets/Scripts/Managers/GameEventStorage.cs` | Centralized GameEvent references |
