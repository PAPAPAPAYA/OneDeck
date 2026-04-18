# GRAVE_PUNCH Test Plan

## Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/Bury and buried/GRAVE_PUNCH.prefab` |
| **Card Type ID** | `GRAVE_PUNCH` |
| **Description** | 埋葬 1 友方 |
| **Is Minion** | False |
| **Trigger Event** | `onMeRevealed` |

---

## Implementation Chain

1. **Card Revealed**: `GameEventListener` on root detects `onMeRevealed` and invokes all child `CostNEffectContainer`s in sibling order.
2. **Container 1 - "bury"**: `BuryEffect.BuryMyCards(1)` buries 1 eligible friendly card.
3. **Container 2 - "deal dmg"**: `HPAlterEffect.DecreaseTheirHp()` deals `baseDmg(2) + extraDmg(1) = 3` damage.
4. **Container 3 - "deal dmg (1)"**: Another `HPAlterEffect.DecreaseTheirHp()` deals another `3` damage.
5. **Final Result**: 1 friendly card is buried. Opponent receives `3 + 3 = 6` total damage.

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

DecreaseTheirHp() (Container 2):
  totalDmg = baseDmg.value(2) + extraDmg(1) + powerStacksOnSelf

DecreaseTheirHp() (Container 3):
  totalDmg = baseDmg.value(2) + extraDmg(1) + powerStacksOnSelf
```

> **Note:** Containers 2 and 3 are separate `CostNEffectContainer`s with identical `HPAlterEffect` configurations. Each container independently calculates damage, so Power stacks apply to **both** hits. Total damage = `(3 + power) * 2`.

### Important Implementation Details

- **Triple-Container Structure**: This card has three sibling containers. Execution order is bury -> damage -> damage.
- **Double-Application Check**: Both damage containers have `baseDmg = 2`, `extraDmg = 1`. Each hit deals 3 base damage.
- **Power Interaction**: If GRAVE_PUNCH has N Power stacks, each hit gains +N damage. Total = `(3 + N) * 2`.
- **Bury Edge Case**: If no eligible friendly cards exist, bury does nothing, but both damage containers still fire.
- **Animation Queue**: Each `DecreaseTheirHp()` call requests an attack animation. Two animations will be queued sequentially.

---

## Test Strategies

### Strategy A: Programmatic Unit Test (Editor Mode) - Recommended

Use `unity-MCP` `execute_code` to simulate combat state directly without entering Play Mode or waiting for animations.

#### Steps
1. Load the prefab via `AssetDatabase.LoadAssetAtPath<GameObject>`.
2. Create or locate a `CombatManager` instance and initialize player statuses.
3. Construct a controlled `combinedDeckZone` and place GRAVE_PUNCH in the reveal zone.
4. Invoke each container's `InvokeEffectEvent()` in sibling order.
5. Assert deck state and opponent HP.

#### Test Cases

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | Base case: 1 eligible friendly, 0 Power | combinedDeckZone has 1 eligible friendly card and GRAVE_PUNCH at top. Opponent has 10 HP. | 1 friendly buried. Opponent HP becomes 4 (10 - 3 - 3). | Two separate 3-damage hits sum to 6. |
| A-2 | No eligible friendly cards | Deck has only enemy cards and Start Card. | No bury. Opponent HP becomes 4. | Damage fires independently of bury success. |
| A-3 | Card has 1 Power | Same as A-1, but card has 1 Power stack. | 1 friendly buried. Opponent HP becomes 2 (10 - 4 - 4). | Power applies to both damage containers. |
| A-4 | Opponent has 4 shield | Opponent has 4 shield, 10 HP. | Shield becomes 0, HP becomes 8. | First hit consumes 3 shield + 0 HP; second hit consumes 1 shield + 2 HP. Wait - actually shield is shared. First hit: shield 4 -> 1, HP 10. Second hit: shield 1 -> 0, HP 9. So HP = 9. Let me recalculate. ProcessDamage: shield -= dmg. If shield < 0, hp += shield (negative), shield = 0. First hit 3: shield = 4-3 = 1, HP = 10. Second hit 3: shield = 1-3 = -2, HP = 10-2 = 8, shield = 0. So HP = 8. | Shield is shared across both hits. |
| A-5 | Enemy perspective | Card belongs to enemy. Player has 10 HP. | 1 enemy-friendly card buried. Player HP becomes 4. | Faction flips for both bury and damage. |
| A-6 | Multiple eligible friendly cards | 3 eligible friendly cards in deck. | Exactly 1 random friendly card is buried. | Bury count clamps at parameter (1). |

#### Pros
- Fast execution (seconds).
- No dependency on UI or animation timing.
- Easy to iterate and debug.

---

### Strategy B: Play Mode Integration Test

Run the actual combat scene and verify the full player-interaction flow.

#### Steps
1. Open the Combat scene (or transition from Shop to Combat).
2. Ensure the test deck contains GRAVE_PUNCH and at least 1 friendly card.
3. Enter Play Mode and advance until GRAVE_PUNCH is revealed.
4. Record the relevant game state before and after the effect triggers.
5. Cross-reference the observed result with the expected logic.
6. Use `read_console` to capture `effectResultString` logs.
7. Optionally use `manage_camera(screenshot)` to capture UI evidence.

#### What to Verify
- Event binding: `onMeRevealed` triggers all three containers in correct order.
- Animation: bury animation plays; two attack animations queue and execute sequentially.
- Log output: two separate damage lines, each showing 3 (+ Power); one bury line.
- State consistency: exactly one friendly card is at the bottom after bury; no duplicate cards.

---

## Recommended Execution Order

1. **Run Strategy A first.** Catch arithmetic and logic bugs immediately without scene overhead.
2. **Run Strategy B second.** Validate the full gameplay integration once core logic is confirmed.

---

## Quick Reference: Key Scripts

| Script | Path | Responsibility |
|--------|------|----------------|
| `HPAlterEffect` | `Assets/Scripts/Effects/HPAlterEffect.cs` | Damage calculation and delivery |
| `BuryEffect` | `Assets/Scripts/Effects/BuryEffect.cs` | Bury friendly cards to bottom |
| `CostNEffectContainer` | `Assets/Scripts/Card/CostNEffectContainer.cs` | Cost checking and effect invocation |
| `CombatManager` | `Assets/Scripts/Managers/CombatManager.cs` | Manages `combinedDeckZone` and `revealZone` |
| `GameEventStorage` | `Assets/Scripts/Managers/GameEventStorage.cs` | Centralized GameEvent references |
