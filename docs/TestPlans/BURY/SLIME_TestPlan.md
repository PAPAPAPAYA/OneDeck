# SLIME Test Plan

## Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/Bury and buried/DeathRattle/SLIME.prefab` |
| **Card Type ID** | `SLIME` |
| **Description** | Buried 2 times: add self to deck |
| **Is Minion** | False |
| **Trigger Event** | OnMeRevealed / OnMeBuried |
| **Tags** | Death Rattle |

---

## Implementation Chain

1. **OnMeRevealed** -> `CostNEffectContainer.InvokeEffectEvent()` on child "deal dmg"
   - `HPAlterEffect.DecreaseTheirHp()` with `baseDmg=2`, `extraDmg=2` -> **4 damage total**
2. **OnMeBuried** -> triggers two containers:
   - "add counter": `StatusEffectGiverEffect.GiveSelfStatusEffect(1)` -> adds 1 Counter stack
   - "add a copy of self": 
     - `checkCostEvent` -> `CheckCost_Counter(2)` -> requires 2 Counter stacks
     - `effectEvent` -> `AddTempCard.AddSelfToMe()` -> adds 1 copy of self to owner's deck

### Effect Formula

```
Reveal: damage = baseDmg.value(2) + extraDmg(2) + Power stacks on self = 4 + Power stacks
Burial (DeathRattle):
  1. Self gains 1 Counter stack
  2. If Counter >= 2: add 1 copy of self to owner's deck
     Else: effect fails, log "Not enough [Counter]"
```

> **Note:** `CheckCost_Counter` only checks the counter count; it does **not consume** Counter stacks. After the copy is added, the Counter stacks remain on the card. The copy is added via `Instantiate(myCard)` and added to the owner's deck via `CombatFuncs.me.AddCard_TargetSpecific`.

### Important Implementation Details

- Reveal deals 4 damage (higher than typical 3-damage cards)
- First burial adds 1 Counter but fails the cost check (needs 2)
- Second burial adds another Counter (now 2 total) and succeeds, adding a copy
- Since Counter is not consumed, third burial would also succeed (2+ Counter)
- `AddTempCard.cardCount=1` means exactly 1 copy is added per successful trigger
- The copy is a full GameObject instantiation, not a reference
- No `OnMeRevealed` listener for the Counter/add-copy effects

---

## Test Strategies

### Strategy A: Programmatic Unit Test (Editor Mode) - Recommended

Use `unity-MCP` `execute_code` to simulate combat state directly without entering Play Mode or waiting for animations.

#### Steps
1. Load the prefab via `AssetDatabase.LoadAssetAtPath<GameObject>`.
2. Create or locate a `CombatManager` instance and initialize player statuses.
3. Set opponent HP to a known value.
4. Instantiate SLIME for the owner player and place it in `revealZone`.
5. Trigger `OnMeRevealed` and assert 4 damage dealt.
6. Bury SLIME once, trigger `OnMeBuried`, assert 1 Counter gained, assert no copy added.
7. Bury SLIME again, trigger `OnMeBuried`, assert copy added, assert Counter count unchanged (not consumed).

#### Test Cases

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | Reveal - base damage | No Power on SLIME | Opponent takes 4 damage | baseDmg(2) + extraDmg(2) = 4 |
| A-2 | First burial - counter only | 0 Counter on SLIME | SLIME gains 1 Counter, no copy added | CheckCost_Counter(2) fails |
| A-3 | Second burial - copy added | 1 Counter on SLIME | SLIME gains 1 Counter (now 2), copy added | Cost met, AddSelfToMe called |
| A-4 | Third burial - still works | 2 Counter on SLIME | Copy added again (Counter not consumed) | Counter persists across triggers |
| A-5 | Burial - cost fail message | 0 Counter, card in revealZone | Log shows "Not enough [Counter]" | Fail message only in revealZone |
| A-6 | Enemy perspective | SLIME owned by enemy | Enemy's opponent takes 4 dmg, copy added to enemy deck | Faction perspective correct |
| A-7 | Reveal - with Power | SLIME has 1 Power | Opponent takes 5 damage | Power adds +1 damage |
| A-8 | Copy added - deck count | 5 cards in deck before burial | 6 cards after successful burial | AddCard_TargetSpecific increases deck size |

#### Pros
- Fast execution (seconds).
- No dependency on UI or animation timing.
- Easy to iterate and debug.

---

### Strategy B: Play Mode Integration Test

Run the actual combat scene and verify the full player-interaction flow.

#### Steps
1. Open the Combat scene.
2. Ensure the test deck contains SLIME.
3. Enter Play Mode and reveal SLIME.
4. Verify opponent takes 4 damage.
5. Continue play until SLIME is buried the first time.
6. Verify 1 Counter is applied to SLIME, but no copy appears.
7. Bury SLIME a second time.
8. Verify a copy of SLIME is added to the deck.

#### What to Verify
- Event binding: `OnMeRevealed` triggers damage; `OnMeBuried` triggers Counter + copy.
- Animation: attack animation for damage.
- Log output: damage number and Counter/copy messages match expectations.
- State consistency: deck count increases by 1 after successful copy.

---

### Strategy C: Regression Batch Test (Optional)

Verify that `CheckCost_Counter(2)` is not consuming Counter stacks. If consumption is intended, the prefab or `CostNEffectContainer` logic may need adjustment.

---

## Recommended Execution Order

1. **Run Strategy A first.** Catch arithmetic and logic bugs immediately without scene overhead.
2. **Run Strategy B second.** Validate the full gameplay integration once core logic is confirmed.

---

## Quick Reference: Key Scripts

| Script | Path | Responsibility |
|--------|------|----------------|
| `HPAlterEffect` | `Assets/Scripts/Effects/HPAlterEffect.cs` | Damage calculation and delivery |
| `StatusEffectGiverEffect` | `Assets/Scripts/Effects/StatusEffect/StatusEffectGiverEffect.cs` | Applies Counter status effect |
| `AddTempCard` | `Assets/Scripts/Effects/AddTempCard.cs` | Adds copy of self to deck |
| `CostNEffectContainer` | `Assets/Scripts/Card/CostNEffectContainer.cs` | Cost checking (`CheckCost_Counter`) and effect invocation |
| `CombatManager` | `Assets/Scripts/Managers/CombatManager.cs` | Manages `combinedDeckZone` and `revealZone` |
| `GameEventStorage` | `Assets/Scripts/Managers/GameEventStorage.cs` | Centralized GameEvent references |
