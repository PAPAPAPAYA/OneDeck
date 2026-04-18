# LARGE_SCALE_DEATH Test Plan

## Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `Assets/Prefabs/Cards/3.0 no cost (current)/Bury and buried/LARGE_SCALE_DEATH.prefab` |
| **Card Type ID** | `LARGE_SCALE_DEATH` |
| **Description** | 埋葬后 4 卡 |
| **Is Minion** | False |
| **Tags** | Linger |
| **Trigger Event** | `onMeRevealed` |

---

## Implementation Chain

1. **Card Revealed**: `GameEventListener` on root detects `onMeRevealed` and invokes the `CostNEffectContainer`.
2. **Cost Check**: None.
3. **Pre-Effect**: None.
4. **Effect Execution**: `BuryEffect.BuryLastXCards(4)` buries up to 4 cards that precede this card in deck order.
5. **Final Result**: Up to 4 preceding cards are moved to the bottom of the deck.

### Effect Formula

```
BuryLastXCards(4):
  if (card is in revealZone)
    startIndex = combinedDeckZone.Count - 1
  else
    currentIndex = combinedDeckZone.IndexOf(thisCard)
    if currentIndex < 0: return
    startIndex = currentIndex - 1

  cardsToBury = []
  for i from startIndex down to 0:
    if cardsToBury.Count >= 4: break
    targetCard = combinedDeckZone[i]
    if ShouldSkipEffectProcessing(targetCard): continue
    if targetCard.isMinion: continue
    if targetCard.IsAtBottom: continue
    cardsToBury.Add(targetCard)

  BuryChosenCards(cardsToBury, cardsToBury.Count)
```

> **Note:** `BuryLastXCards` iterates **backwards** from the card's position (or from deck top if in revealZone). It skips neutral cards, minions, and cards already at the bottom. The search continues until 4 valid targets are found or the deck bottom is reached.

### Important Implementation Details

- **Linger Tag**: The card carries the `Linger` tag. Its specific gameplay implication depends on project-level Linger handling.
- **Reveal Zone Behavior**: If LARGE_SCALE_DEATH is in `revealZone`, the search starts from the top of `combinedDeckZone` (index `Count - 1`) and moves downward.
- **Self-Exclusion**: The card itself is never buried by this effect (search starts at `currentIndex - 1` or `Count - 1`).
- **Bury Count Cap**: Even if more than 4 eligible cards exist, only 4 are buried.
- **Tracker Updates**: Each buried card increments `ownerCardsBuriedCountRef` or `enemyCardsBuriedCountRef` based on the card's owner.

---

## Test Strategies

### Strategy A: Programmatic Unit Test (Editor Mode) - Recommended

Use `unity-MCP` `execute_code` to simulate combat state directly without entering Play Mode or waiting for animations.

#### Steps
1. Load the prefab via `AssetDatabase.LoadAssetAtPath<GameObject>`.
2. Create or locate a `CombatManager` instance and initialize player statuses.
3. Construct a controlled `combinedDeckZone` with at least 4 cards preceding LARGE_SCALE_DEATH.
4. Place LARGE_SCALE_DEATH in the deck or reveal zone.
5. Invoke the `CostNEffectContainer.InvokeEffectEvent()` directly.
6. Assert final deck order and buried counts.

#### Test Cases

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
| A-1 | Base case: 4 valid cards behind | combinedDeckZone = [FriendlyA, EnemyB, FriendlyC, EnemyD, LARGE_SCALE_DEATH] (bottom to top). Card at index 4. | FriendlyA, EnemyB, FriendlyC, EnemyD are all buried to bottom in that order. | Exactly 4 cards buried; order preserved relative to iteration. |
| A-2 | More than 4 valid cards behind | 6 valid cards precede LARGE_SCALE_DEATH. | Exactly 4 cards buried (the 4 closest to LARGE_SCALE_DEATH). | Count clamps at parameter (4). |
| A-3 | Only 2 valid cards behind | combinedDeckZone = [Start, FriendlyA, EnemyB, LARGE_SCALE_DEATH]. Start at 0. | FriendlyA and EnemyB buried. Start Card skipped. | Count clamps to available targets. |
| A-4 | Card in revealZone | LARGE_SCALE_DEATH in revealZone. combinedDeckZone = [FriendlyA, EnemyB, FriendlyC, EnemyD, Start] (bottom to top). | EnemyD, FriendlyC, EnemyB, FriendlyA buried (from top down). | revealZone starts search from deck top. |
| A-5 | Preceding cards include minions | 3 valid cards + 2 minions precede the card. | 3 valid cards buried. Minions skipped. | isMinion filter works. |
| A-6 | Preceding card already at bottom | combinedDeckZone = [FriendlyA(bottom), FriendlyB, LARGE_SCALE_DEATH]. FriendlyA is at index 0. | Only FriendlyB buried. FriendlyA skipped. | IsAtBottom filter works. |
| A-7 | Enemy perspective | Card belongs to enemy. Deck mirrors A-1. | Same 4 cards buried. Buried counts tracked under correct owner. | Faction perspective does not affect target selection. |

#### Pros
- Fast execution (seconds).
- No dependency on UI or animation timing.
- Easy to iterate and debug.

---

### Strategy B: Play Mode Integration Test

Run the actual combat scene and verify the full player-interaction flow.

#### Steps
1. Open the Combat scene (or transition from Shop to Combat).
2. Ensure the test deck contains LARGE_SCALE_DEATH with at least 4 cards behind it.
3. Enter Play Mode and advance until LARGE_SCALE_DEATH is revealed.
4. Record the relevant game state before and after the effect triggers.
5. Cross-reference the observed result with the expected logic.
6. Use `read_console` to capture `effectResultString` logs.
7. Optionally use `manage_camera(screenshot)` to capture UI evidence.

#### What to Verify
- Event binding: `onMeRevealed` triggers the container.
- Animation: bury animations play for all moved cards.
- Log output: bury messages show correct card names and owner color tags.
- State consistency: buried cards are at the bottom of `combinedDeckZone` in the correct order.
- Tracker accuracy: buried counts increment correctly for each owner.

---

## Recommended Execution Order

1. **Run Strategy A first.** Catch arithmetic and logic bugs immediately without scene overhead.
2. **Run Strategy B second.** Validate the full gameplay integration once core logic is confirmed.

---

## Quick Reference: Key Scripts

| Script | Path | Responsibility |
|--------|------|----------------|
| `BuryEffect` | `Assets/Scripts/Effects/BuryEffect.cs` | Bury preceding cards to bottom |
| `ValueTrackerManager` | `Assets/Scripts/Managers/ValueTrackerManager.cs` | Tracks buried counts |
| `CostNEffectContainer` | `Assets/Scripts/Card/CostNEffectContainer.cs` | Cost checking and effect invocation |
| `CombatManager` | `Assets/Scripts/Managers/CombatManager.cs` | Manages `combinedDeckZone` and `revealZone` |
| `GameEventStorage` | `Assets/Scripts/Managers/GameEventStorage.cs` | Centralized GameEvent references |
