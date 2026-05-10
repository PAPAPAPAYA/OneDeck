# OneDeck - AI Agent Documentation

Unity roguelike card game. Both decks are merged, shuffled, and cards are revealed one by one to trigger effects.

## Development Standards

| Item | Requirement |
|------|-------------|
| **Line Endings** | `\r\n` (CRLF) |
| **Indentation** | Tab (`\t`), spaces forbidden |
| **Command Separator** | PowerShell uses `;` instead of `&&` |
| **Comments & Docs** | English only |
| **Encoding** | UTF-8 only |

## Core Loop

`Shop` -> `Combat` -> `Result` -> `Shop`

## Project Structure

```
Assets/
├── Scripts/
│   ├── Managers/       # CombatManager, ShopManager, PhaseManager, CombatFuncs, EffectChainManager, GameEventStorage, ValueTrackerManager, EnumStorage, AnimationStateTracker, AttackAnimationManager, CardFactory, CardIDRetriever, CombatInfoDisplayer, CombatLog, CombatStartCardGiver, CombatStatsLogger, CostResultPresenter, DeckTester, EffectRecorder, RecorderAnimationPlayer, GameEventListener, ICombatVisuals + Null*, ShopStatsManager, StartingCardManager, UtilityFuncManagerScript, WriteRead/
│   ├── Effects/        # EffectScript, HPAlterEffect, ShieldAlterEffect, StageEffect, BuryEffect, ExileEffect, CurseEffect, AddTempCard, AddTextEffect, CardManipulationEffect, ChangeCardTarget, ChangeHpAlterAmountEffect, HPMaxAlterEffect, PrintEffect, TransferStatusEffectEffect, BuryCostEffect, DelayCostEffect, ExposeCostEffect, MinionCostEffect, shop/DeckSizeIncreaseEffect, StatusEffect/
│   ├── Card/           # CardScript, CostNEffectContainer, CardEventTrigger
│   ├── SOScripts/      # GameEvent, PlayerStatusSO, StatusEffectSO, DeckSO, BoolSO, CostCheckResult, GamePhaseSO, IntSO, ShopRarityWeightSO, StringSO
│   └── UXPrototype/    # CombatUXManager, ShopUXManager, CardPhysObjScript, CombatCardView, ShopCardView
├── Prefabs/Cards/      # 3.0 no cost (current), System/, StatusEffectResolvers/
└── docs/
```

## Core Architecture

- **Singletons**: `CombatManager.Me`, `ShopManager.me`, `GameEventStorage.me`, `ValueTrackerManager.me`, `EffectChainManager.Me`, `CombatFuncs.me`, `CardFactory.me`, `CardIDRetriever.Me`, `AnimationStateTracker.me`, `CombatInfoDisplayer.me`, `CombatLog.me`, `CostResultPresenter.me`, `RecorderAnimationPlayer.me`
- **Event-driven**: `GameEvent` SO + `GameEventListener`
- **Component-based Cards**: `CardScript` + `EffectContainers` + `Effects`
- **Visual Abstraction**: `ICombatVisuals` interface. `CombatManager.visuals` falls back to `CombatUXManager.visuals`, or inject via `visualsOverride` (e.g. `NullCombatVisualsBehaviour` for headless tests).

## Combat System

### Flow
1. **GatherDecks**: Merge both decks, add Start Card to bottom.
2. **Reveal**: Reveal cards one by one.
3. **Start Card**: When revealed, triggers shuffle + new round.

### Zones
- `combinedDeckZone` - Merged deck (index 0 = bottom, index Count-1 = top)
- `revealZone` - Currently revealed card

### Controls
- First click: Reveal next card.
- Second click: Trigger effect and place card at bottom.

### Input Blocking
`CombatManager.IsInputBlocked` uses reference counting via `BlockInput(requester)` / `UnblockInput(requester)`.

### Fatigue / Overtime
- `fatigueRevealThreshold` + `totalCardsRevealed` - Fatigue after N reveals.
- `overtimeRoundThreshold` + `fatigueAmount` - Fatigue after N rounds.

## Effect System

### Trigger Flow
`CostNEffectContainer.InvokeEffectEvent()` returns `CostCheckResult`.
Flow: Check cost -> `preEffectEvent` -> Check effect chain -> Execute effect.

### Effect Chain Manager
- **Chain creation**: Starts when no chains open, or same card triggers a *different* effect object.
- **Loop guard**: Same `effectID` cannot be invoked twice within an open chain.
- **Depth limit**: `chainDepth` > **99** blocks further effects.
- **Chain closing**: `CloseOpenedChain()` finalizes recorders and clears state.

### Cost Types
| Method | Description |
|--------|-------------|
| `Mana(n)` | Requires n Mana stacks |
| `Rested()` | Consumes Rest status |
| `Revive(n)` | Requires n Revive stacks |
| `HasEnemyCard(n)` | Requires n enemy cards in deck |
| `Token Cost` | Consume N friendly Minions of specified type |
| `Bury Cost` | Place N friendly cards at bottom |
| `Delay Cost` | Delay N own cards by 1 position |
| `Expose Cost` | Expose N enemy cards to top |

### Status Effects
```csharp
enum StatusEffect { None, Infected, Mana, HeartChanged, Power, Rest, Revive, Counter }
```
| Effect | Description |
|--------|-------------|
| `Power` | Damage +1 |
| `HeartChanged` | Ownership change |
| `Rest` | Skip trigger |
| `Counter` | Counter-attack / block |

### Tags
```csharp
enum Tag { None, Linger, ManaX, DeathRattle }
```

## Events

### Card-Specific
`onMeRevealed`, `onMeBought`, `onMeStaged`, `onMeBuried`, `onMeGotPower`, `onMeGotStatusEffect`, `onThisTagResolverAttached`

### Global (use `Raise()`)
`onAnyCardRevealed`, `onHostileCardRevealed`, `afterShuffle`, `beforeRoundStart`, `onAnyCardBuried`, `onAnyCardGotPower`

### Faction-Specific (use `RaiseOwner()` / `RaiseOpponent()`)
`onTheirPlayerTookDmg`, `onMyPlayerTookDmg`, `onTheirPlayerHealed`, `onMyPlayerHealed`, `onMyPlayerShieldUpped`, `onTheirPlayerShieldUpped`, `onFriendlyMinionAdded`, `onFriendlyCardExiled`, `onFriendlyFlyExiled`, `onFriendlyCardBuried`, `onEnemyCurseCardRevealed`, `onEnemyCurseCardGotPower`, `onFriendlyCardGotPower`, `onEnemyCardGotPower`

### Target-Specific (use `RaiseSpecific()`)
`RaiseSpecific(GameObject target)` raises event only on target and its children listeners.

## Key Files

| Name | Path |
|------|------|
| `CombatManager` | `Assets/Scripts/Managers/CombatManager.cs` |
| `CombatFuncs` | `Assets/Scripts/Managers/CombatFuncs.cs` |
| `HPAlterEffect` | `Assets/Scripts/Effects/HPAlterEffect.cs` |
| `CardScript` | `Assets/Scripts/Card/CardScript.cs` |
| `CostNEffectContainer` | `Assets/Scripts/Card/CostNEffectContainer.cs` |
| `CombatUXManager` | `Assets/Scripts/UXPrototype/CombatUXManager.cs` |
| `GameEventStorage` | `Assets/Scripts/Managers/GameEventStorage.cs` |
| `ValueTrackerManager` | `Assets/Scripts/Managers/ValueTrackerManager.cs` |
| `EnumStorage` | `Assets/Scripts/Managers/EnumStorage.cs` |
| `AnimationStateTracker` | `Assets/Scripts/Managers/AnimationStateTracker.cs` |
| `RecorderAnimationPlayer` | `Assets/Scripts/Managers/RecorderAnimationPlayer.cs` |
| `CardFactory` | `Assets/Scripts/Managers/CardFactory.cs` |
| `ICombatVisuals` | `Assets/Scripts/Managers/ICombatVisuals.cs` |
| `CombatLog` | `Assets/Scripts/Managers/CombatLog.cs` |
| `GameRules` | `docs/GameRules.md` |

## Minion Cost Mechanism

Consumes N eligible Minion cards (`isMinion == true`) from `combinedDeckZone`.
- `minionCostCount` - Number required
- `minionCostCardTypeID` - Filter by card type ID (empty = no restriction)
- `minionCostOwner` - `Me` / `Them` / `Random`

## Animation System

### Two-Phase Execution Model
1. **Logic Phase** — All effect logic executes synchronously. Effects capture `AnimationRequest`s into the current `EffectRecorder` instead of playing visuals immediately. Deck state, HP, and shields resolve immediately.
2. **Animation Phase** — After the chain closes, `CombatManager.PlayRecorderAnimationsAndWait()` collects root recorders and yields to `RecorderAnimationPlayer.PlayRecordersCoroutine()` for sequential playback.

### EffectRecorder Tree
- `EffectRecorder` MonoBehaviour carries `animationRequests` (captured intents) and `animationPlayed` flag.
- Tree navigation uses existing Transform parent-child hierarchy under `EffectChainManager`.
- Traversal order is **effect-instance-boundary interleave**: play all requests in current recorder, then recurse into unplayed direct children by sibling order.

### AnimationRequest Types
```csharp
enum AnimationRequestType { Attack, MoveToBottom, MoveToBottomBatch, MoveToTop, MoveToTopBatch, MoveToIndex, Destroy, StatusEffectChange }
```
- `HPAlterEffect` captures `Attack` requests (damage already resolved in logic phase; `onHit` is null).
- `BuryEffect` captures `MoveToBottomBatch`.
- `StageEffect` captures `MoveToTopBatch`.
- `ExileEffect` captures `Destroy`.
- `ApplyStatusEffectCore`, `ConsumeStatusEffect`, `ManaAlterEffect`, and `TransferStatusEffectEffect` capture `StatusEffectChange` requests (status effect visuals are deferred to the animation phase; resolver instantiation stays in the logic phase).
- Batch types run all card movements in parallel and yield until the last completes.

### RecorderAnimationPlayer
- Singleton. Owns the animation-phase coroutine.
- Wraps playback in `AttackAnimationManager.HoldDeckFocus()` / `ReleaseDeckFocus()`.
- Calls `UpdateAllPhysicalCardTargets()` before each move request.
- Falls back to old visual path when `RecorderAnimationPlayer.me == null`.

### AnimationStateTracker (Legacy Safety Net)
Still active as a secondary guard. `PlayRecorderAnimationsAndWait` yields until `HasActiveBatch == false` before closing the chain, ensuring any legacy-queued events flush naturally.

### Card Movement (`ICombatVisuals` / `CombatUXManager`)
- `MoveCardToBottom(card, duration, useArc, onComplete)`
- `MoveCardToTop(card, duration, useArc, onComplete)`
- `MoveCardToIndex(card, index, duration, useArc, onComplete)`
- `DestroyCardWithAnimation(card, onComplete)`
- `AddCardToDeckVisual(card)`
- `SyncPhysicalCardsWithCombinedDeck()`
- `PlayShuffleAnimation(startCard, shuffledCards, onComplete)`

## Critical Rules

- **HPAlterEffect**: Automatically adds `baseDmg.value`; set `baseDmg` to 0 when passing a specific value.
- **cardTypeID**: Used for saving / statistics / Minion cost filtering (not instance ID).
- **Anti-loop**: Do not attach multiple looping effect instances to the same card.
- **GameEvent.Raise**: Use `Raise()` only for non-faction-specific events. For owner/opponent events, use `RaiseOwner()` / `RaiseOpponent()` based on the trigger object's faction. Direct `Raise()` on faction events is prohibited.
- **Neutral Cards**: `isStartCard == true` cards are neutral and skipped by `ShouldSkipEffectProcessing()`.
- **CardScript Cost Fields**: `buryCost`, `delayCost`, `exposeCost`, `minionCostCount`, `minionCostCardTypeID`, `minionCostOwner`.
- **CardScript Properties**: `displayName` (falls back to GameObject name via `GetDisplayName()`), `shopRollWeightMultiplier`, `IsNeutralCard`, `CanBeAffectedByEffects`, `takeUpSpace`.
- **Graveyard Removed**: Graveyard mechanic is deprecated. `CardManipulationEffect.Revive*` methods are no-ops.
- **Input Block Reference Counting**: `BlockInput`/`UnblockInput` use reference counting; always pair them.

## Color Tags

| Type | Tag |
|------|-----|
| Damage | `<color=red>` |
| Heal | `<color=#90EE90>` |
| Shield | `<color=grey>` |
| Friendly | `<color=#87CEEB>` |
| Enemy | `<color=orange>` |

---

## Unity MCP `execute_code` (CodeDom)

Default compiler is `codedom` (C# 6). Constraints:

| Forbidden | Alternative |
|-----------|-------------|
| `using` declarations | Fully-qualified names (`UnityEngine.Debug.Log`) |
| `return;` (void) | `return <value>;` on **all** paths |
| `$""` interpolation | `+` or `string.Format` |
| `?.` null-conditional | Explicit `!= null` checks |
| `yield return` | No coroutines |

If a project type is not resolved (e.g. `GameEventListener`), use `System.Type.GetType("GameEventListener, Assembly-CSharp")`.

---

**Glob**: Use `Assets/**/FileName.cs` instead of `**/FileName.cs`
