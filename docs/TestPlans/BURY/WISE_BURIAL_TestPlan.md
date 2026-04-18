# WISE_BURIAL Test Plan

## Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/Bury and buried/WISE_BURIAL.prefab` |
| **Card Type ID** | `WISE_BURIAL` |
| **Description** | 被置顶: 埋葬 1 友方[被埋葬:] |
| **Is Minion** | False |
| **Trigger Event** | `onMeRevealed` |

---

## Implementation Chain

1. **Card Revealed**: `GameEventListener` on root detects `onMeRevealed` and invokes the `CostNEffectContainer`.
2. **Cost Check**: `CheckCost_IndexBeforeStartCard()` verifies the card's index in `combinedDeckZone` is smaller than the Start Card's index.
3. **Effect Execution**: `StatusEffectGiverEffect.GiveStatusEffectToLastXCards()` applies Power to up to 2 cards preceding this card in deck order.
4. **Final Result**: If the card is before the Start Card, up to 2 preceding cards receive 2 layers of Power each.

### Effect Formula

```
CheckCost_IndexBeforeStartCard():
  costMet = (thisCardIndex < startCardIndex)

GiveStatusEffectToLastXCards():
  if (card is in revealZone)
    startIndex = combinedDeckZone.Count - 1
  else
    startIndex = thisCardIndex - 1

  targetCards = []
  for i from startIndex down to 0:
    if targetCards.Count >= lastXCardsCount(2): break
    card = combinedDeckZone[i]
    if ShouldSkipEffectProcessing(card): continue
    if !CanReceiveStatusEffect(card, Power): continue
    targetCards.Add(card)

  foreach targetCard in targetCards:
    ApplyStatusEffectCore(targetCard, Power, statusEffectLayerCount(2))
```

> **Note:** Prefab inspection shows `lastXCardsCount = 2` and `statusEffectLayerCount = 2`. Each valid target receives **2 stacks** of Power. The card description mentions "被置顶: 埋葬 1 友方", but the actual serialized effect is identical to GRUDGE's Power-giving linger effect. This discrepancy should be flagged for design review.

### Important Implementation Details

- **Cost Gate**: The effect only fires if the card is positioned before the Start Card. If revealed after the Start Card, the cost check fails and the effect does nothing.
- **Description Mismatch**: The card description references "bury 1 friendly" and "staged" behavior, but the inspected prefab only contains a `StatusEffectGiverEffect` giving Power. This is a test-critical discrepancy.
- **Power Stacking**: `canStatusEffectBeStacked = True`, so multiple Power layers can be applied.
- **Skip Rules**: Neutral cards (Start Card) and cards that cannot receive Power are skipped.
- **Reveal Zone Behavior**: If the card is in `revealZone`, the search starts from the top of the deck.

---

## Test Strategies

### Strategy A: Programmatic Unit Test (Editor Mode) - Recommended

Use `unity-MCP` `execute_code` to simulate combat state directly without entering Play Mode or waiting for animations.

#### Steps
1. Load the prefab via `AssetDatabase.LoadAssetAtPath<GameObject>`.
2. Create or locate a `CombatManager` instance and initialize player statuses.
3. Construct a controlled `combinedDeckZone` with the Start Card at a known index.
4. Place WISE_BURIAL at various positions relative to the Start Card.
5. Invoke the `CostNEffectContainer.InvokeEffectEvent()` directly.
6. Assert target cards' `myStatusEffects` lists and verify no bury effect occurs.

#### Test Cases

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | Base case: before Start Card, 2 valid targets behind | combinedDeckZone = [WISE_BURIAL, FriendlyA, FriendlyB, Start, Enemy] (bottom to top). WISE_BURIAL at index 0, Start at index 3. | FriendlyA and FriendlyB each gain 2 Power. | Cost met (0 < 3). Targets are indices 1 and 2. |
| A-2 | After Start Card | combinedDeckZone = [Start, WISE_BURIAL, FriendlyA, FriendlyB]. Start at 0, WISE_BURIAL at 1. | No effect. Cost check fails. | IndexBeforeStartCard correctly blocks effect. |
| A-3 | Only 1 valid target behind | combinedDeckZone = [WISE_BURIAL, FriendlyA, Start, Enemy]. WISE_BURIAL at 0, Start at 2. | FriendlyA gains 2 Power. Only 1 target applied. | lastXCardsCount clamps to available targets. |
| A-4 | No valid targets behind | combinedDeckZone = [WISE_BURIAL, Start, Enemy]. WISE_BURIAL at 0, Start at 1. | No effect (Start Card is skipped). | ShouldSkipEffectProcessing excludes Start Card. |
| A-5 | Card in revealZone | WISE_BURIAL in revealZone. combinedDeckZone = [FriendlyA, FriendlyB, Start, Enemy] (bottom to top). | FriendlyB (top) and FriendlyA each gain 2 Power. | revealZone mode starts search from deck top. |
| A-6 | Targets already have Power | FriendlyA already has 1 Power. Same as A-1. | FriendlyA ends with 3 Power (1 + 2). FriendlyB gets 2 Power. | canStatusEffectBeStacked allows stacking. |
| A-7 | Enemy perspective | Card belongs to enemy. Deck mirrors A-1. | Enemy-friendly cards gain 2 Power each. | Target filter uses card owner, not session owner. |
| A-8 | Verify no bury occurs | Same as A-1. | No cards are moved to bottom. Only Power is applied. | Confirms description mismatch — no bury effect exists. |

#### Pros
- Fast execution (seconds).
- No dependency on UI or animation timing.
- Easy to iterate and debug.

---

### Strategy B: Play Mode Integration Test

Run the actual combat scene and verify the full player-interaction flow.

#### Steps
1. Open the Combat scene (or transition from Shop to Combat).
2. Ensure the test deck contains WISE_BURIAL positioned before the Start Card.
3. Enter Play Mode and advance until WISE_BURIAL is revealed.
4. Record the relevant game state before and after the effect triggers.
5. Cross-reference the observed result with the expected logic.
6. Use `read_console` to capture `effectResultString` logs.
7. Optionally use `manage_camera(screenshot)` to capture UI evidence.

#### What to Verify
- Event binding: `onMeRevealed` triggers the container.
- Cost check: if WISE_BURIAL is after Start Card, no effect triggers.
- Animation: status effect projectile animation plays for target cards.
- Log output: Power application is logged for each target.
- **No bury animation**: Confirm that no cards are moved to the bottom (validates the description mismatch).
- State consistency: target cards' `myStatusEffects` lists contain the correct number of Power entries.

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
| `EnumStorage` | `Assets/Scripts/Managers/EnumStorage.cs` | StatusEffect and Tag enums |
