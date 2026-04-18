# REVENGER Test Plan

## Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/Bury and buried/DeathRattle/REVENGER.prefab` |
| **Card Type ID** | `REVENGER` |
| **Description** | Deal 3 damage |
| **Is Minion** | False |
| **Trigger Event** | OnMeRevealed / OnMeBuried |
| **Tags** | Death Rattle |

---

## Implementation Chain

1. **OnMeRevealed** -> `CostNEffectContainer.InvokeEffectEvent()` on child "deal dmg"
   - `HPAlterEffect.DecreaseTheirHp()` with `baseDmg=2`, `extraDmg=1` -> **3 damage total**
2. **OnMeBuried** -> `CostNEffectContainer.InvokeEffectEvent()` on child "gain power"
   - `StatusEffectGiverEffect.GiveSelfStatusEffect(int amount)` with `amount=3` -> **gain 3 Power stacks**

### Effect Formula

```
Reveal: damage = baseDmg.value(2) + extraDmg(1) + Power stacks on self = 3 + Power stacks
Burial (DeathRattle): Self gains 3 stacks of Power
```

> **Note:** `GiveSelfStatusEffect(3)` passes `amount=3` directly to the method, overriding the component's `statusEffectLayerCount=1` field. The card gains exactly 3 Power stacks on burial.

### Important Implementation Details

- Reveal damage uses `DecreaseTheirHp()` which adds `baseDmg.value` automatically
- `extraDmg=1` is statically configured on the prefab
- Each Power stack on the card adds +1 to the reveal damage (via `dmgAmountAlter` in `DmgCalculator`)
- Burial effect gives 3 Power stacks to self (not 1)
- No cost checks on either container
- `isStatusEffectDamage=False` means attack animation plays normally

---

## Test Strategies

### Strategy A: Programmatic Unit Test (Editor Mode) - Recommended

Use `unity-MCP` `execute_code` to simulate combat state directly without entering Play Mode or waiting for animations.

#### Steps
1. Load the prefab via `AssetDatabase.LoadAssetAtPath<GameObject>`.
2. Create or locate a `CombatManager` instance and initialize player statuses.
3. Set opponent HP to a known value (e.g., 20).
4. Instantiate REVENGER for the owner player and place it in `revealZone`.
5. Trigger `GameEventStorage.me.onMeRevealed.RaiseSpecific(card)`.
6. Assert opponent HP decreased by expected amount.
7. Bury the card and trigger `GameEventStorage.me.onMeBuried.RaiseSpecific(card)`.
8. Assert REVENGER has exactly 3 Power stacks.

#### Test Cases

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | Reveal - base damage | No Power on REVENGER | Opponent takes 3 damage | baseDmg(2) + extraDmg(1) = 3 |
| A-2 | Reveal - with Power | REVENGER has 2 Power | Opponent takes 5 damage | Power adds +1 per stack |
| A-3 | Burial - gain Power | No Power on REVENGER | REVENGER gains 3 Power | GiveSelfStatusEffect(3) |
| A-4 | Burial - Power stacking | REVENGER already has 1 Power | REVENGER now has 4 Power | Stacks accumulate |
| A-5 | Enemy perspective | REVENGER owned by enemy | Enemy's opponent (player) takes damage | Faction perspective correct |
| A-6 | Reveal - zero opponent HP | Opponent HP = 1 | Opponent HP goes to 0 (or min) | Damage floor behavior |
| A-7 | Full cycle | Reveal then burial | 3 dmg dealt, then 3 Power gained | Both events work independently |

#### Pros
- Fast execution (seconds).
- No dependency on UI or animation timing.
- Easy to iterate and debug.

---

### Strategy B: Play Mode Integration Test

Run the actual combat scene and verify the full player-interaction flow.

#### Steps
1. Open the Combat scene.
2. Ensure the test deck contains REVENGER.
3. Enter Play Mode and reveal REVENGER.
4. Record opponent HP before and after.
5. Continue play until REVENGER is buried.
6. Verify REVENGER gains 3 Power stacks.
7. If REVENGER is revealed again, verify damage is increased by Power.

#### What to Verify
- Event binding: `OnMeRevealed` triggers damage, `OnMeBuried` triggers Power gain.
- Animation: attack animation plays for reveal damage.
- Log output: damage and Power application messages match expectations.
- State consistency: HP and status effect counts remain valid.

---

### Strategy C: Regression Batch Test (Optional)

Verify that `GiveSelfStatusEffect(3)` is intentional (gain 3 Power on burial) and not a misconfiguration where `statusEffectLayerCount=1` was expected to be used.

---

## Recommended Execution Order

1. **Run Strategy A first.** Catch arithmetic and logic bugs immediately without scene overhead.
2. **Run Strategy B second.** Validate the full gameplay integration once core logic is confirmed.

---

## Quick Reference: Key Scripts

| Script | Path | Responsibility |
|--------|------|----------------|
| `HPAlterEffect` | `Assets/Scripts/Effects/HPAlterEffect.cs` | Damage calculation and delivery |
| `StatusEffectGiverEffect` | `Assets/Scripts/Effects/StatusEffect/StatusEffectGiverEffect.cs` | Applies status effects to target cards |
| `CostNEffectContainer` | `Assets/Scripts/Card/CostNEffectContainer.cs` | Cost checking and effect invocation |
| `CombatManager` | `Assets/Scripts/Managers/CombatManager.cs` | Manages `combinedDeckZone` and `revealZone` |
| `GameEventStorage` | `Assets/Scripts/Managers/GameEventStorage.cs` | Centralized GameEvent references |
