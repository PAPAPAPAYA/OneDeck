# Debug.Log / print Inventory

Generated: 2026-06-19
Scope: `Assets/` (Unity scripts only; `Library/`, `Temp/`, packages excluded)

## Summary

| Kind | Active | Commented out |
|------|--------|---------------|
| `Debug.Log` / `Debug.LogWarning` / `Debug.LogError` | ~76 | ~65 |
| `print(...)` | 0 | ~13 |
| `Console.WriteLine` | 0 | 0 |

> Most of the noise is in the new animation/recorder path (`CombatManager`, `CombatUXManager`, `RecorderAnimationPlayer`, `EffectChainManager`, `BuryEffect`, `StageEffect`).

## Proposed TestManager switch categories

Based on the tags in the messages, the following toggle groups would cover the active logs cleanly:

| Switch | Covers prefixes / files | Active log count |
|--------|------------------------|------------------|
| `logCombatFlow` | `[CombatManager]`, `[PhaseManager]` | 13 |
| `logEffectChains` | `[EffectChainManager]`, `[BuryEffect]`, `[StageEffect]`, `[ApplyStatusEffectCore]` / `EffectScript` | 11 |
| `logAnimationPlayback` | `[RecorderAnimationPlayer]`, `[AnimationStateTracker]` | 18 |
| `logVisualSync` | `[CombatUXManager]`, `[CardPhysObjScript]` | 18 |
| `logEditorTools` | `[EnemyDeckRecorder]`, `[CardTypeIDValidator]`, `ReadmeEditor` | 14 |
| `logTestManager` | `[TestManager]` | 1 |

A helper like `TestManager.Log(category, message)` (or a conditional `Debug.Log` wrapper) can then replace the direct `Debug.Log` calls.

## Active `Debug.Log*` by category

### Combat flow (`[CombatManager]`, `[PhaseManager]`)

| File | Line | Level | Message prefix |
|------|------|-------|----------------|
| `Assets/Scripts/Managers/CombatManager.cs` | 429 | Log | `[CombatManager] PlayRecorderAnimationsAndWait STARTED` |
| `Assets/Scripts/Managers/CombatManager.cs` | 436 | Log | `[CombatManager] PlayRecorderAnimationsAndWait aborted during HasActiveBatch wait: object destroyed.` |
| `Assets/Scripts/Managers/CombatManager.cs` | 439 | Log | `[CombatManager] PlayRecorderAnimationsAndWait waiting for HasActiveBatch...` |
| `Assets/Scripts/Managers/CombatManager.cs` | 446 | Log | `[CombatManager] PlayRecorderAnimationsAndWait aborted after wait: object destroyed or not in Combat phase.` |
| `Assets/Scripts/Managers/CombatManager.cs` | 467 | Log | `[CombatManager] Collecting recorder chainID=...` |
| `Assets/Scripts/Managers/CombatManager.cs` | 481 | Log | `[CombatManager] Playing N root recorder(s).` |
| `Assets/Scripts/Managers/CombatManager.cs` | 486 | Log | `[CombatManager] No root recorders to play.` |
| `Assets/Scripts/Managers/CombatManager.cs` | 492 | Log | `[CombatManager] PlayRecorderAnimationsAndWait finally block.` |
| `Assets/Scripts/Managers/CombatManager.cs` | 502 | Log | `[CombatManager] finally marking animationPlayed=true...` |
| `Assets/Scripts/Managers/CombatManager.cs` | 515 | Log | `[CombatManager] PlayRecorderAnimationsAndWait aborted before final layout...` |
| `Assets/Scripts/Managers/CombatManager.cs` | 531 | Log | `[CombatManager] PlayRecorderAnimationsAndWait COMPLETE` |
| `Assets/Scripts/Managers/CombatManager.cs` | 844 | Log | `[CombatManager] HandleCombatFinished blocked: effect animations still playing.` |
| `Assets/Scripts/Managers/PhaseManager.cs` | 116 | Log | `[PhaseManager] Combat finished but effect animations still playing; waiting...` |

### Effect chains & effect logic (`[EffectChainManager]`, `[BuryEffect]`, `[StageEffect]`, `[ApplyStatusEffectCore]`)

| File | Line | Level | Message prefix |
|------|------|-------|----------------|
| `Assets/Scripts/Managers/EffectChainManager.cs` | 113 | Log | `[EffectChainManager] MakeANewEffectRecorder...` |
| `Assets/Scripts/Managers/EffectChainManager.cs` | 148 | Error | `ERROR: chain depth reached limit` |
| `Assets/Scripts/Managers/EffectChainManager.cs` | 167 | Log | `[EffectChainManager] PopCurrentRecorder...` |
| `Assets/Scripts/Managers/EffectChainManager.cs` | 200 | Log | `[EffectChainManager] CloseOpenedChain...` |
| `Assets/Scripts/Effects/BuryEffect.cs` | 232 | Log | `[BuryEffect] BuryNextXCards START...` |
| `Assets/Scripts/Effects/BuryEffect.cs` | 266 | Log | `[BuryEffect] BuryNextXCards found cardsToBury=...` |
| `Assets/Scripts/Effects/BuryEffect.cs` | 271 | Log | `[BuryEffect] BuryNextXCards found NO cards to bury` |
| `Assets/Scripts/Effects/BuryEffect.cs` | 352 | Log | `[BuryEffect] Capture request to recorder=...` |
| `Assets/Scripts/Effects/StageEffect.cs` | 374 | Log | `[StageEffect] StageChosenCards combinedDeck AFTER logic move:...` |
| `Assets/Scripts/Effects/StageEffect.cs` | 401 | Log | `[StageEffect] Capture request to recorder=...` |
| `Assets/Scripts/Effects/EffectScript.cs` | 127 | Log | `[ApplyStatusEffectCore] Capturing StatusEffectChange for...` |

### Animation playback (`[RecorderAnimationPlayer]`, `[AnimationStateTracker]`)

| File | Line | Level | Message prefix |
|------|------|-------|----------------|
| `Assets/Scripts/Managers/RecorderAnimationPlayer.cs` | 19 | Log | `[RecorderAnimationPlayer] PlayRecordersCoroutine START...` |
| `Assets/Scripts/Managers/RecorderAnimationPlayer.cs` | 27 | Log | `[RecorderAnimationPlayer] Skipping null/destroyed root recorder.` |
| `Assets/Scripts/Managers/RecorderAnimationPlayer.cs` | 33 | Log | `[RecorderAnimationPlayer] Skipping root recorder:...` |
| `Assets/Scripts/Managers/RecorderAnimationPlayer.cs` | 50 | Log | `[RecorderAnimationPlayer] PlayRecorderCoroutine: recorder GameObject was destroyed, skipping.` |
| `Assets/Scripts/Managers/RecorderAnimationPlayer.cs` | 56 | Log | `[RecorderAnimationPlayer] PlayRecorderCoroutine chainID=...` |
| `Assets/Scripts/Managers/RecorderAnimationPlayer.cs` | 71 | Log | `[RecorderAnimationPlayer] Skipping emphasize:...` |
| `Assets/Scripts/Managers/RecorderAnimationPlayer.cs` | 188 | Log | `[RecorderAnimationPlayer] PlayRequest type=...` |
| `Assets/Scripts/Managers/RecorderAnimationPlayer.cs` | 214 | Log | `[RecorderAnimationPlayer] PlayRequest type=...` |
| `Assets/Scripts/Managers/RecorderAnimationPlayer.cs` | 259 | Log | `[RecorderAnimationPlayer] MoveToBottomBatch skipping destroyed card at index...` |
| `Assets/Scripts/Managers/RecorderAnimationPlayer.cs` | 278 | Log | `[RecorderAnimationPlayer] MoveToBottomBatch calling MoveCardToIndex...` |
| `Assets/Scripts/Managers/RecorderAnimationPlayer.cs` | 323 | Log | `[RecorderAnimationPlayer] MoveToTopBatch skipping destroyed card at index...` |
| `Assets/Scripts/Managers/RecorderAnimationPlayer.cs` | 342 | Log | `[RecorderAnimationPlayer] MoveToTopBatch calling MoveCardToIndex...` |
| `Assets/Scripts/Managers/RecorderAnimationPlayer.cs` | 382 | Log | `[RecorderAnimationPlayer] MoveToTopPopUpBatch skipping destroyed card at index...` |
| `Assets/Scripts/Managers/RecorderAnimationPlayer.cs` | 396 | Log | `[RecorderAnimationPlayer] MoveToTopPopUpBatch card=...` |
| `Assets/Scripts/Managers/RecorderAnimationPlayer.cs` | 674 | Log | `[RecorderAnimationPlayer] PopUpBatch skipping destroyed card at index...` |
| `Assets/Scripts/Managers/RecorderAnimationPlayer.cs` | 702 | Log | `[RecorderAnimationPlayer] SlotInBatch skipping destroyed card at index...` |
| `Assets/Scripts/Managers/AnimationStateTracker.cs` | 40 | Log | `[AnimationStateTracker] RegisterAnimation...` |
| `Assets/Scripts/Managers/AnimationStateTracker.cs` | 49 | Log | `[AnimationStateTracker] CompleteAnimation...` |

### Visual sync / deck layout (`[CombatUXManager]`, `[CardPhysObjScript]`)

| File | Line | Level | Message prefix |
|------|------|-------|----------------|
| `Assets/Scripts/UXPrototype/CombatUXManager.cs` | 153 | Log | `[CombatUXManager] SyncPhysicalCardsWithCombinedDeck START...` |
| `Assets/Scripts/UXPrototype/CombatUXManager.cs` | 189 | Log | `[CombatUXManager] SyncPhysicalCardsWithCombinedDeck done...` |
| `Assets/Scripts/UXPrototype/CombatUXManager.cs` | 407 | Log | `[CombatUXManager] MoveCardWithAnimation START...` |
| `Assets/Scripts/UXPrototype/CombatUXManager.cs` | 665 | Log | `[CombatUXManager] CalculatePositionAtIndex...` |
| `Assets/Scripts/UXPrototype/CombatUXManager.cs` | 689 | Log | `[CombatUXManager] CalculateAnimationPositionAtIndex...` |
| `Assets/Scripts/UXPrototype/CombatUXManager.cs` | 710 | Log | `[CombatUXManager] CalculatePositionForPendingCard...` |
| `Assets/Scripts/UXPrototype/CombatUXManager.cs` | 985 | Log | `[CombatUXManager] UpdateAllPhysicalCardTargets START...` |
| `Assets/Scripts/UXPrototype/CombatUXManager.cs` | 996 | Log | `[CombatUXManager] UpdateAllPhysicalCardTargets card=...` |
| `Assets/Scripts/UXPrototype/CombatUXManager.cs` | 1002 | Log | `[CombatUXManager] UpdateAllPhysicalCardTargets END` |
| `Assets/Scripts/UXPrototype/CombatUXManager.cs` | 1020 | Log | `[CombatUXManager] ApplyAnimationResult START...` |
| `Assets/Scripts/UXPrototype/CombatUXManager.cs` | 1197 | Log | `[CombatUXManager] ApplyAnimationResult END...` |
| `Assets/Scripts/UXPrototype/CombatUXManager.cs` | 1946 | Log | `[CombatUXManager] PlayStatusEffectProjectile for receiver=...` |
| `Assets/Scripts/UXPrototype/CombatUXManager.cs` | 1988 | Log | `[CombatUXManager] PlayMultiStatusEffectProjectile START...` |
| `Assets/Scripts/UXPrototype/CombatUXManager.cs` | 2143 | Log | `[CombatUXManager] PlayStatusEffectProjectileToPosition...` |
| `Assets/Scripts/UXPrototype/CombatUXManager.cs` | 2550 | Log | `[CombatUXManager] SlotInCard...` |
| `Assets/Scripts/UXPrototype/CombatUXManager.cs` | 2613 | Log | `[CombatUXManager] MoveCardToPopUpPosition...` |
| `Assets/Scripts/UXPrototype/CardPhysObjScript.cs` | 289 | Log | `[CardPhysObjScript] SetTargetPosition...` |
| `Assets/Scripts/UXPrototype/CardPhysObjScript.cs` | 352 | Log | `[CardPhysObjScript] StartPositionTween START...` |

### Editor / write-read tools (`[EnemyDeckRecorder]`, `[CardTypeIDValidator]`, `ReadmeEditor`)

| File | Line | Level | Message prefix |
|------|------|-------|----------------|
| `Assets/Scripts/Managers/WriteRead/EnemyDeckRecorder.cs` | 51 | Warning | `[EnemyDeckRecorder] Recording is only available in Play Mode.` |
| `Assets/Scripts/Managers/WriteRead/EnemyDeckRecorder.cs` | 58 | Warning | `[EnemyDeckRecorder] DeckSaver singleton is null.` |
| `Assets/Scripts/Managers/WriteRead/EnemyDeckRecorder.cs` | 65 | Warning | `[EnemyDeckRecorder] DeckSaver.playerDeck is null.` |
| `Assets/Scripts/Managers/WriteRead/EnemyDeckRecorder.cs` | 71 | Warning | `[EnemyDeckRecorder] Player deck is empty.` |
| `Assets/Scripts/Managers/WriteRead/EnemyDeckRecorder.cs` | 108 | Warning | `[EnemyDeckRecorder] Could not resolve N missing cards...` |
| `Assets/Scripts/Managers/WriteRead/EnemyDeckRecorder.cs` | 114 | Error | `[EnemyDeckRecorder] No cards could be resolved. Aborting.` |
| `Assets/Scripts/Managers/WriteRead/EnemyDeckRecorder.cs` | 121 | Log | `[EnemyDeckRecorder] Asset creation is only available in Editor.` |
| `Assets/Scripts/Managers/WriteRead/EnemyDeckRecorder.cs` | 130 | Warning | `[EnemyDeckRecorder] Card ... has duplicate typeID...` |
| `Assets/Scripts/Managers/WriteRead/EnemyDeckRecorder.cs` | 161 | Log | `[EnemyDeckRecorder] Recorded enemy deck: ...` |
| `Assets/Scripts/Editor/CardTypeIDValidator.cs` | 42 | Warning | `[CardTypeIDValidator] Duplicate cardTypeID ...` |
| `Assets/Scripts/Editor/CardTypeIDValidator.cs` | 49 | Warning | `[CardTypeIDValidator] Empty cardTypeID in: ...` |
| `Assets/Scripts/Editor/CardTypeIDValidator.cs` | 54 | Log | `[CardTypeIDValidator] All cardTypeIDs are valid...` |
| `Assets/TutorialInfo/Scripts/Editor/ReadmeEditor.cs` | 39 | Log | `Could not find the Readme folder at ...` |
| `Assets/TutorialInfo/Scripts/Editor/ReadmeEditor.cs` | 90 | Log | `Couldn't find a readme` |

### TestManager (`[TestManager]`)

| File | Line | Level | Message prefix |
|------|------|-------|----------------|
| `Assets/Scripts/Managers/TestManager.cs` | 90 | Log | `[TestManager] Test mode ENABLED / DISABLED` |

## Commented-out logs (summary)

A large number of `Debug.Log*` and `print` calls are already commented out. The files with the most commented logs are:

- `Assets/Scripts/UXPrototype/ShopUXManager.cs`
- `Assets/Scripts/UXPrototype/CombatUXManager.cs`
- `Assets/Scripts/Managers/WriteRead/DeckSaver.cs`
- `Assets/Scripts/Managers/WriteRead/CardWinRateTracker.cs`
- `Assets/Scripts/Managers/CombatStatsLogger.cs`
- `Assets/Scripts/Managers/ShopStatsManager.cs`
- `Assets/Scripts/Effects/CurseEffect.cs`
- `Assets/Scripts/Managers/CombatManager.cs`
- `Assets/Scripts/Managers/CardFactory.cs`
- `Assets/Scripts/Managers/CombatFuncs.cs`
- `Assets/Scripts/Managers/StartingCardManager.cs`
- `Assets/Scripts/Managers/CombatStartCardGiver.cs`
- `Assets/Scripts/Effects/TransferStatusEffectEffect.cs`
- `Assets/Scripts/Effects/AddTempCard.cs`
- `Assets/Scripts/UXPrototype/ShopCardView.cs`
- `Assets/Scripts/Managers/GameEventListener.cs`
- `Assets/Scripts/Managers/PhaseManager.cs`
- `Assets/Scripts/Managers/DeckTester.cs`
- `Assets/Scripts/Effects/HPAlterEffect.cs`
- `Assets/Scripts/Effects/PrintEffect.cs`
- `Assets/Scripts/Effects/shop/DeckSizeIncreaseEffect.cs`
- `Assets/Scripts/Managers/AnimationStateTracker.cs`

These do not currently emit anything, so they do not need switches unless you plan to re-enable them.
