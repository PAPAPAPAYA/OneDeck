# SMALL_DEATH Test Plan

## Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/Bury and buried/SMALL_DEATH.prefab` |
| **Card Type ID** | `SMALL_DEATH` |
| **Description** | 埋葬后 2 卡 |
| **Is Minion** | False |
| **Trigger Event** | `onMeRevealed` |

---

## Implementation Chain

1. **Card Revealed**: `GameEventListener` on root detects `onMeRevealed` and invokes the `CostNEffectContainer`.
2. **Cost Check**: None.
3. **Pre-Effect**: None.
4. **Effect Execution**:
   - `BuryEffect.BuryLastXCards(2)` buries up to 2 cards preceding this card.
   - `CurseEffect.EnhanceCurse(1)` finds or creates an enemy curse card and applies 1 stack of Power to it.
5. **Final Result**: Up to 2 preceding cards are buried. An enemy curse card gains 1 Power (or is spawned if absent).

### Effect Formula

```
BuryLastXCards(2):
  if (card is in revealZone)
    startIndex = combinedDeckZone.Count - 1
  else
    startIndex = thisCardIndex - 1

  cardsToBury = []
  for i from startIndex down to 0:
    if cardsToBury.Count >= 2: break
    target = combinedDeckZone[i]
    if ShouldSkipEffectProcessing(target): continue
    if target.isMinion: continue
    if target.IsAtBottom: continue
    cardsToBury.Add(target)
  BuryChosenCards(cardsToBury, cardsToBury.Count)

EnhanceCurse(1):
  targetTypeID = CurseEffect.cardTypeID.value (or GameEventStorage.curseCardTypeID)
  targetCard = FindEnemyCardWithTypeID(targetTypeID)
  if targetCard == null:
    targetCard = CreateEnemyCard(CurseEffect.cardPrefab)
  ApplyPowerToCardInternal(targetCard, 1)
```

> **Note:** `EnhanceCurse` depends on the `CurseEffect` component's serialized fields (`cardTypeID`, `cardPrefab`). If these are not configured, the effect logs a warning and does nothing.

### Important Implementation Details

- **Two-Part Effect**: Bury and curse enhancement are sequential calls within the same `effectEvent`. Bury executes first.
- **Curse Target**: The curse card type ID is determined by the `CurseEffect` component's `cardTypeID` field. This must match the project's curse card configuration.
- **Card Creation**: If no enemy curse card exists in `combinedDeckZone`, a new one is instantiated via `CombatFuncs.me.AddCard_TargetSpecific` and placed at index 0.
- **Event Trigger**: If the target card is an enemy curse card (matching `GameEventStorage.curseCardTypeID`), `onEnemyCurseCardGotPower` is raised.
- **Bury Behavior**: Same as `BuryLastXCards` in other cards — skips minions, neutral cards, and bottom cards.

---

## Test Strategies

### Strategy A: Programmatic Unit Test (Editor Mode) - Recommended

Use `unity-MCP` `execute_code` to simulate combat state directly without entering Play Mode or waiting for animations.

#### Steps
1. Load the prefab via `AssetDatabase.LoadAssetAtPath<GameObject>`.
2. Create or locate a `CombatManager` instance and initialize player statuses.
3. Construct a controlled `combinedDeckZone` with at least 2 cards preceding SMALL_DEATH.
4. Ensure a curse card configuration exists (or mock it).
5. Place SMALL_DEATH in the deck or reveal zone.
6. Invoke the `CostNEffectContainer.InvokeEffectEvent()` directly.
7. Assert deck order, buried counts, and curse card Power stacks.

#### Test Cases

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | Base case: 2 valid cards behind, enemy curse exists | combinedDeckZone has 2 valid cards behind SMALL_DEATH and 1 enemy curse card. | 2 cards buried. Curse card gains 1 Power. | Both effects execute successfully. |
| A-2 | No valid cards behind | Only Start Card and minions behind SMALL_DEATH. | No bury. Curse card still gains 1 Power. | Curse enhancement is independent of bury. |
| A-3 | Enemy curse does not exist | No enemy curse card in deck. `cardPrefab` is set. | 2 cards buried. New enemy curse card is created at index 0 and gains 1 Power. | Card creation fallback works. |
| A-4 | CurseEffect fields not configured | `cardTypeID` is null or empty. | 2 cards buried. Curse effect logs warning and does nothing. | Graceful failure when config is missing. |
| A-5 | Card in revealZone | SMALL_DEATH in revealZone. combinedDeckZone has 4 valid cards. | Top 2 valid cards buried. Curse card gains 1 Power. | revealZone starts search from deck top. |
| A-6 | Enemy perspective | Card belongs to enemy. | 2 cards buried (from enemy's deck perspective). Player's curse card gains 1 Power. | Faction flips for curse target (theirStatusRef). |
| A-7 | Curse card already has Power | Enemy curse card already has 2 Power. | Curse card ends with 3 Power. `onEnemyCurseCardGotPower` event raised. | Power stacks correctly; event fires. |

#### Pros
- Fast execution (seconds).
- No dependency on UI or animation timing.
- Easy to iterate and debug.

---

### Strategy B: Play Mode Integration Test

Run the actual combat scene and verify the full player-interaction flow.

#### Steps
1. Open the Combat scene (or transition from Shop to Combat).
2. Ensure the test deck contains SMALL_DEATH with cards behind it and a curse card configuration.
3. Enter Play Mode and advance until SMALL_DEATH is revealed.
4. Record the relevant game state before and after the effect triggers.
5. Cross-reference the observed result with the expected logic.
6. Use `read_console` to capture `effectResultString` logs.
7. Optionally use `manage_camera(screenshot)` to capture UI evidence.

#### What to Verify
- Event binding: `onMeRevealed` triggers the container.
- Animation: bury animations play for moved cards; status effect projectile plays for curse card.
- Log output: bury messages show correct cards; curse enhancement is logged.
- State consistency: curse card has correct Power count; new card is created only when needed.
- Event firing: `onEnemyCurseCardGotPower` is raised when applicable.

---

## Recommended Execution Order

1. **Run Strategy A first.** Catch arithmetic and logic bugs immediately without scene overhead.
2. **Run Strategy B second.** Validate the full gameplay integration once core logic is confirmed.

---

## Quick Reference: Key Scripts

| Script | Path | Responsibility |
|--------|------|----------------|
| `BuryEffect` | `Assets/Scripts/Effects/BuryEffect.cs` | Bury preceding cards to bottom |
| `CurseEffect` | `Assets/Scripts/Effects/CurseEffect.cs` | Enhance enemy curse cards with Power |
| `CombatFuncs` | `Assets/Scripts/Managers/CombatFuncs.cs` | Adds cards to deck mid-combat |
| `CostNEffectContainer` | `Assets/Scripts/Card/CostNEffectContainer.cs` | Cost checking and effect invocation |
| `CombatManager` | `Assets/Scripts/Managers/CombatManager.cs` | Manages `combinedDeckZone` and `revealZone` |
| `GameEventStorage` | `Assets/Scripts/Managers/GameEventStorage.cs` | Centralized GameEvent references |
