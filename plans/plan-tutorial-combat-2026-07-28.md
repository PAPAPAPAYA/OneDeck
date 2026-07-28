# Plan: First-Launch Tutorial Combat

Date: 2026-07-28
Status: Proposal (no code written yet)

## Goal

On the very first game launch, the player enters a scripted **tutorial combat** instead of the shop:

- Decks: two dedicated tutorial `DeckSO` assets (player + enemy), configured by the designer.
- Reveal order: fully deterministic (scripted).
- HP: custom player/enemy HP values.
- Outcome: scripted player win; result is **not** counted in `wins`/`hearts`/stats.
- After the tutorial combat ends, the game enters the **first real shop phase** and the normal run begins.
- Runs **once ever** (PlayerPrefs flag), no replay entry point.

Non-goals: no tutorial UI / hints / forced-click guidance (pure scripted combat). Tutorial deck contents and HP numbers are configured by the designer, not this plan.

## Existing Seams (verified)

- `PhaseManager.OnEnable()` (`PhaseManager.cs:86-94`) fires `onGameStart` (**zero listeners** in `GameScene.unity`), then unconditionally calls `ExitingCombatPhase()` -> `ExitingResultPhase()` -> `EnteringShopPhase()`. This is the boot seam.
- `PhaseManager.EnteringCombatPhase()` (`PhaseManager.cs:293`) is public and drives combat entry via the `onEnterCombatPhase` UnityEvent, scene-wired to: `CombatManager.EnterCombat`, `DeckSaver.PopulateEnemyDeckBySessionNumber`, `DeckSaver.SavePlayerDeckToJson`.
- Fixed reveal order already exists: `ShuffleOrderOverride` (`Assets/Scripts/Managers/ShuffleOrderOverride.cs`, component on the CombatManager GameObject). When `useCustomOrder == true`, `StartCardShuffleEffect.ExecuteShuffleEffect()` (`StartCardShuffleEffect.cs:38-45`) calls `CombatManager.ApplyCustomShuffleOrder()` (`CombatManager.cs:929`), which imposes an exact reveal order from a prefab list, **first-revealed -> last-revealed**. The initial Start Card reveal triggers this shuffle, so the override covers round 1 ordering completely.
- HP lives on the two `PlayerStatusSO` assets (`PlayerStatusRef.asset` / `EnemyStatusRef.asset`); writing `hp`/`hpMax` before `GatherDecks` is sufficient. `EnteringShopPhase()` resets both SOs (`PhaseManager.cs:376-377`), so tutorial HP is cleaned up automatically when the real game starts.
- No PlayerPrefs usage exists anywhere yet; this adds the first one.

## Design

### New component: `TutorialManager` (`Assets/Scripts/Managers/TutorialManager.cs`)

Singleton-agnostic plain MonoBehaviour with a static `IsTutorialActive` flag.

Serialized fields:

| Field | Type | Purpose |
|-------|------|---------|
| `tutorialPlayerDeck` | `DeckSO` | Player deck for the tutorial combat |
| `tutorialEnemyDeck` | `DeckSO` | Enemy deck for the tutorial combat |
| `tutorialRevealOrder` | `List<GameObject>` | Card prefabs, first-revealed -> last-revealed; fed into `ShuffleOrderOverride.customOrderPrefabs` |
| `tutorialPlayerHP` | `int` | Player hp + hpMax during tutorial |
| `tutorialEnemyHP` | `int` | Enemy hp + hpMax during tutorial |

Methods:

- `CheckTutorialOnGameStart()` — wired as the **first** listener of `PhaseManager.onGameStart`. If `PlayerPrefs.GetInt("OneDeck_TutorialCompleted", 0) == 1` or any required field is unassigned, do nothing. Otherwise set `IsTutorialActive = true`.
- `SetupTutorialCombat()` — wired as the **last** listener of `onEnterCombatPhase`. No-op unless `IsTutorialActive`. Does:
	1. Cache `CombatManager.Me.playerDeck` / `enemyDeck` originals.
	2. Swap in the two tutorial `DeckSO`s. (Safe: `GatherDecks()` reads them later, in `CombatManager.Update()`.)
	3. Set `hp`/`hpMax` on both `PlayerStatusSO` refs.
	4. Ensure a `ShuffleOrderOverride` component exists on the CombatManager GameObject (`GetComponent`, `AddComponent` if missing), set `useCustomOrder = true`, copy `tutorialRevealOrder` into `customOrderPrefabs`.
- `EndTutorial()` — called by `PhaseManager` when the tutorial combat finishes. Does:
	1. `PlayerPrefs.SetInt("OneDeck_TutorialCompleted", 1)` + `Save()`.
	2. Restore cached deck refs on `CombatManager`; also `playerDeck.ResetToDefault()` for safety.
	3. `useCustomOrder = false`, clear `customOrderPrefabs`.
	4. `IsTutorialActive = false`.
- `[ContextMenu("Reset Tutorial Flag")]` — deletes the PlayerPrefs key (dev/testing only).

### `PhaseManager.cs` edits (small, two spots)

1. `OnEnable()` — after `InvokeOnGameStartEvent()`:

```csharp
InvokeOnGameStartEvent();
if (TutorialManager.IsTutorialActive)
{
	EnteringCombatPhase();   // tutorial combat instead of shop
}
else
{
	ExitingCombatPhase();
	ExitingResultPhase();
	EnteringShopPhase();
}
```

2. `Update()` combat branch — right after the `isPlayingEffectAnimations` guard (`PhaseManager.cs:118-122`), before the WIN/LOSE bookkeeping:

```csharp
if (TutorialManager.IsTutorialActive)
{
	// Tutorial combat: scripted outcome, skip wins/hearts/stats, go straight to first shop.
	ExitingCombatPhase();
	TutorialManager.EndTutorial();
	EnteringShopPhase();
	return;
}
```

`EnteringShopPhase()` resets both status SOs (clears tutorial HP) and fires `onEnterShopPhase`, so `StartingCardManager.TryGiveStartingCard` runs normally (`sessionNum` is still 0). The Result phase is skipped for the tutorial.

### `DeckSaver.cs` guards (2 early-returns)

Both methods are wired to `onEnterCombatPhase` and would otherwise clobber the tutorial setup:

- `PopulateEnemyDeckBySessionNumber()` — first line: `if (TutorialManager.IsTutorialActive) return;` (prevents enemy deck swap + HP bonus).
- `SavePlayerDeckToJson()` — first line: same guard (prevents persisting the tutorial deck as the "player deck" for future enemies).

### `CombatManager.cs` guard (1 spot)

`GatherDecks()` (`CombatManager.cs:301-302`): wrap the two `CardWinRateTracker` snapshot calls in `if (!TutorialManager.IsTutorialActive)`. Otherwise the tutorial deck snapshot stays pending and leaks into the next real combat's win-rate record. `CombatPerCardStatsTracker.BeginSession()` needs no guard (session-scoped, wiped next combat).

### Scene / asset setup (manual, one time)

1. Create a `TutorialManager` GameObject in `GameScene.unity`, add the `TutorialManager` component.
2. Wire `PhaseManager.onGameStart` -> `TutorialManager.CheckTutorialOnGameStart` (first/only listener).
3. Wire `PhaseManager.onEnterCombatPhase` -> `TutorialManager.SetupTutorialCombat`, ordered **after** the existing three listeners.
4. Designer creates `Assets/SORefs/Decks/Tutorial/TutorialPlayerDeck.asset` + `TutorialEnemyDeck.asset`, assigns them plus `tutorialRevealOrder` and HP values.

## Configuration Notes for the Designer

- **Reveal order**: the Start Card is always revealed first and triggers the shuffle; `tutorialRevealOrder` then defines the exact order for everything else, **first-revealed -> last-revealed**, matching `ShuffleOrderOverride` semantics. Design decks/HP so the combat ends within round 1 (before the Start Card comes around a second time).
- **Duplicate prefabs**: `ApplyCustomShuffleOrder` matches prefab -> instance first-come-first-served and is faction-agnostic. Avoid using the same prefab in both tutorial decks (or in two scripted positions where identity matters).
- **Scripted win**: guaranteed by deck/HP tuning. No code safety net in v1; if the player somehow loses, the tutorial still ends and the run starts normally (only the narrative breaks). An optional HP clamp (player HP never drops below 1 while `IsTutorialActive`) can be added later if playtesting shows losses.
- **Round-2 safety**: if the tutorial deck is too big to finish in round 1, the second Start Card reveal shuffles again with the same override list — already-revealed cards simply won't match, so order stays deterministic but cards returning to the deck re-appear in list order. Keep it to one round.

## Edge Cases

- **Player quits mid-tutorial**: PlayerPrefs flag is only written at tutorial end, so next launch replays the tutorial from the start. Acceptable and desirable.
- **Editor iteration**: use the `Reset Tutorial Flag` context menu to re-test.
- **`DeckTester.autoSpace` / `autoReveal`**: unaffected; tutorial works with both.
- **ESC quit** in `PhaseManager.Update` is unaffected.

## Testing

1. Reset tutorial flag -> Play: game boots into tutorial combat, cards reveal in `tutorialRevealOrder` order, custom HP shown, player wins.
2. Combat end: straight into shop (no Result screen), starting card is given, both HP bars back to 20/20, `wins`/`hearts` unchanged, `sessionNum == 0`.
3. Shop -> combat: normal enemy deck from `DeckSaver`, tutorial decks fully restored away; `deckdata.json` contains no tutorial cards.
4. Stop and re-Play without resetting the flag: boots straight into shop, no tutorial.
5. `CardWinRateTracker` report after steps 1-4 shows no tutorial card entries.

## Files Touched

| File | Change |
|------|--------|
| `Assets/Scripts/Managers/TutorialManager.cs` | New (~80 lines) |
| `Assets/Scripts/Managers/PhaseManager.cs` | `OnEnable` branch + tutorial early-exit in `Update` (~12 lines) |
| `Assets/Scripts/Managers/WriteRead/DeckSaver.cs` | 2 guard lines |
| `Assets/Scripts/Managers/CombatManager.cs` | 1 guard around win-rate snapshots |
| `Assets/Scenes/GameScene.unity` | New GameObject + 2 UnityEvent wirings |
| `Assets/SORefs/Decks/Tutorial/*.asset` | Designer-authored deck assets |

After implementation: update `AGENTS.md` (singleton/flow notes) if the team wants the tutorial flow documented there.
