# GRAVE_KEEPER Test Plan

## Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/Bury and buried/GRAVE_KEEPER.prefab` |
| **Card Type ID** | `GRAVE_KEEPER` |
| **Description** | 当卡被埋葬时, 置顶自身 |
| **Is Minion** | False |
| **Trigger Events** | `onMeRevealed` (damage), `onAnyCardBuried` / `onFriendlyCardBuried` (stage self) |

---

## Implementation Chain

1. **Card Revealed**: `GameEventListener` on root detects `onMeRevealed` and invokes Container "deal dmg".
2. **Damage Container**: No cost check. `HPAlterEffect.DecreaseTheirHp()` deals `baseDmg(2) + extraDmg(4) = 6` damage to opponent.
3. **Bury Event Trigger**: When any card is buried (including by other cards), a separate listener invokes Container "stage self".
4. **Stage Container**: `StageEffect.StageSelf()` moves GRAVE_KEEPER to the top of `combinedDeckZone` if it is not already there.
5. **Final Result**: Opponent loses 6 HP on reveal. GRAVE_KEEPER stages itself whenever a bury event occurs.

### Effect Formula

```
DecreaseTheirHp():
  totalDmg = baseDmg.value(2) + extraDmg(4) + powerStacksOnSelf
  target = theirStatusRef (opponent)

StageSelf():
  if (GetCardIndexInCombinedDeck(self) != topIndex)
    remove self from current index
    add self to top of combinedDeckZone
```

> **Note:** GRAVE_KEEPER has **two independent containers** bound to **different events**. The damage container is bound to `onMeRevealed`. The stage container is bound to a bury-related event (likely `onAnyCardBuried` or `onFriendlyCardBuried`).

### Important Implementation Details

- **Event Separation**: Damage and stage are not in the same event chain. Stage can trigger multiple times per round if multiple cards are buried.
- **Stage Condition**: `StageSelf` does nothing if the card is already at the top of the deck.
- **Double-Application Check**: `baseDmg = 2`, `extraDmg = 4`. `DecreaseTheirHp()` adds them automatically. Final damage = 6.
- **Power Interaction**: Each Power stack adds +1 to the reveal damage.
- **Faction Perspective**: If GRAVE_KEEPER belongs to the enemy, damage hits the player, and stage still moves the card to the top of the combined deck.

---

## Test Strategies

### Strategy A: Programmatic Unit Test (Editor Mode) - Recommended

Use `unity-MCP` `execute_code` to simulate combat state directly without entering Play Mode or waiting for animations.

#### Steps
1. Load the prefab via `AssetDatabase.LoadAssetAtPath<GameObject>`.
2. Create or locate a `CombatManager` instance and initialize player statuses.
3. Construct a controlled `combinedDeckZone` and place GRAVE_KEEPER appropriately.
4. **Test damage**: Invoke the "deal dmg" container's `InvokeEffectEvent()`.
5. **Test stage**: Bury another card (e.g., via `BuryEffect`), then verify GRAVE_KEEPER's position.
6. Assert HP change and deck order.

#### Test Cases

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | Base reveal damage | GRAVE_KEEPER in revealZone. Opponent has 10 HP, 0 shield. | Opponent HP becomes 4. | Base damage = 2 + 4 = 6. |
| A-2 | Reveal damage with 2 Power | Same as A-1, but card has 2 Power stacks. | Opponent HP becomes 2. | Power adds +1 per stack (6 + 2 = 8). |
| A-3 | Stage self after friendly bury | GRAVE_KEEPER at index 2 in combinedDeckZone. Another friendly card is buried. | GRAVE_KEEPER moves to top index (Count - 1). | Bury event triggers stage correctly. |
| A-4 | Stage self when already at top | GRAVE_KEEPER is already at the top of the deck. A card is buried. | GRAVE_KEEPER remains at top. No exception. | StageSelf is idempotent at top. |
| A-5 | Stage self after enemy bury | GRAVE_KEEPER at index 2. An enemy card is buried. | GRAVE_KEEPER moves to top. | Stage triggers on any card bury (not just friendly). |
| A-6 | Enemy perspective | Card belongs to enemy. Player has 10 HP. | Player HP becomes 4. Card stages to top on bury. | Faction perspective flips for damage; stage logic is owner-agnostic. |
| A-7 | Shield absorption | Opponent has 5 shield, 10 HP. | Shield becomes 0, HP becomes 9. | Shield absorbs 5 of the 6 damage. |

#### Pros
- Fast execution (seconds).
- No dependency on UI or animation timing.
- Easy to iterate and debug.

---

### Strategy B: Play Mode Integration Test

Run the actual combat scene and verify the full player-interaction flow.

#### Steps
1. Open the Combat scene (or transition from Shop to Combat).
2. Ensure the test deck contains GRAVE_KEEPER and other cards that can trigger bury effects.
3. Enter Play Mode and reveal GRAVE_KEEPER. Record damage.
4. Reveal another card that buries cards (e.g., GRAVE_PUNCH). Observe GRAVE_KEEPER's position.
5. Cross-reference observed results with expected logic.
6. Use `read_console` to capture `effectResultString` logs.
7. Optionally use `manage_camera(screenshot)` to capture UI evidence.

#### What to Verify
- Event binding: `onMeRevealed` triggers damage; bury event triggers stage.
- Animation: attack animation plays for damage; stage animation plays when GRAVE_KEEPER moves to top.
- Log output: damage number is 6 (+ Power); stage message appears after bury.
- State consistency: GRAVE_KEEPER is at the top of `combinedDeckZone` after stage.
- No duplication: Stage does not create duplicate cards.

---

## Recommended Execution Order

1. **Run Strategy A first.** Catch arithmetic and logic bugs immediately without scene overhead.
2. **Run Strategy B second.** Validate the full gameplay integration once core logic is confirmed.

---

## Quick Reference: Key Scripts

| Script | Path | Responsibility |
|--------|------|----------------|
| `HPAlterEffect` | `Assets/Scripts/Effects/HPAlterEffect.cs` | Damage calculation and delivery |
| `StageEffect` | `Assets/Scripts/Effects/StageEffect.cs` | Stage cards to top of deck |
| `CostNEffectContainer` | `Assets/Scripts/Card/CostNEffectContainer.cs` | Cost checking and effect invocation |
| `CombatManager` | `Assets/Scripts/Managers/CombatManager.cs` | Manages `combinedDeckZone` and `revealZone` |
| `GameEventStorage` | `Assets/Scripts/Managers/GameEventStorage.cs` | Centralized GameEvent references |
