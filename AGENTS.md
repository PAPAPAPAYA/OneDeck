# OneDeck - AI Agent Documentation

Unity roguelike card game. Both decks are merged, shuffled, and cards are revealed one by one to trigger effects.

## Development Standards

| Item | Requirement |
|------|-------------|
| **Line Endings** | `\r\n` (CRLF) |
| **Indentation** | Tab (`\t`), spaces are strictly forbidden |
| **Command Separator** | PowerShell uses `;` instead of `&&` |
| **Comments & Docs** | All comments and documentation must be written in English |
| **Encoding** | Never use non-UTF-8 encoded characters under any circumstances |

## Core Loop

`Shop` -> `Combat` -> `Result` -> `Shop`

## Project Structure

```
Assets/
├── Scripts/
│   ├── Managers/
│   │   ├── CombatManager.cs          # Combat core
│   │   ├── PhaseManager.cs           # Phase control
│   │   ├── ShopManager.cs            # Shop system
│   │   ├── CombatFuncs.cs            # Combat helper functions
│   │   ├── CombatUXManager.cs        # Card movement & animation
│   │   ├── AttackAnimationManager.cs # Attack animation queue
│   │   ├── EffectChainManager.cs     # Effect chain tracking & anti-loop (max depth 99)
│   │   ├── EffectRecorder.cs         # Records effect history
│   │   ├── GameEventStorage.cs       # Centralized GameEvent SO refs
│   │   ├── ValueTrackerManager.cs    # Deck value trackers
│   │   ├── EnumStorage.cs            # Enums & static helpers
│   │   └── WriteRead/                # Save/load data
│   ├── Effects/
│   │   ├── EffectScript.cs           # Effect base class
│   │   ├── HPAlterEffect.cs          # Damage / Heal
│   │   ├── HPMaxAlterEffect.cs       # Max HP change
│   │   ├── ShieldAlterEffect.cs      # Shield
│   │   ├── CardManipulationEffect.cs # Delay / Destroy Minion
│   │   ├── StageEffect.cs            # Stage cards to top
│   │   ├── BuryEffect.cs             # Bury cards to bottom
│   │   ├── BuryCostEffect.cs         # Bury cost check & execute
│   │   ├── DelayCostEffect.cs        # Delay cost check & execute
│   │   ├── ExposeCostEffect.cs       # Expose cost check & execute
│   │   ├── MinionCostEffect.cs       # Minion cost check & execute
│   │   ├── AddTempCard.cs            # Add temporary cards
│   │   ├── ExileEffect.cs            # Exile cards
│   │   ├── CurseEffect.cs            # Curse mechanics
│   │   ├── ChangeCardTarget.cs       # Change effect target
│   │   ├── ChangeHpAlterAmountEffect.cs # Modify damage amount
│   │   ├── StatusEffect/             # Status effect effects
│   │   └── shop/                     # Shop-only effects
│   ├── Card/
│   │   ├── CardScript.cs             # Card data
│   │   ├── CostNEffectContainer.cs   # Cost check & effect trigger
│   │   └── CardEventTrigger.cs       # Card event hooks
│   ├── SOScripts/
│   │   ├── GameEvent.cs              # Event system
│   │   ├── PlayerStatusSO.cs         # Player status
│   │   ├── StatusEffectSO.cs         # Status effect data
│   │   ├── DeckSO.cs                 # Deck data
│   │   └── IntSO / BoolSO / StringSO / GamePhaseSO
│   └── UXPrototype/
│       ├── CombatUXManager.cs        # Combat UI/UX
│       ├── ShopUXManager.cs          # Shop UI/UX
│       └── CardPhysObjScript.cs      # Physical card object
├── Prefabs/
│   └── Cards/
│       ├── 3.0 no cost (current)     # Currently in-use cards
│       ├── System/                   # StartCard, Fatigue, etc.
│       └── StatusEffectResolvers/    # Status effect resolver prefabs
└── docs/
    ├── StatusEffectProjectileSystem.md
    ├── bury_cost_test_guide.md
    └── ChineseFontSetup.md
```

## Core Architecture

- **Singleton**: `CombatManager.Me`, `ShopManager.me`, `GameEventStorage.me`, `ValueTrackerManager.me`, `EffectChainManager.Me`
- **Event-driven**: `GameEvent` SO + `GameEventListener`
- **Component-based Cards**: `CardScript` + `EffectContainers` + `Effects`

## Combat System

### Flow
1. **GatherDecks**: Merge both decks and add the Start Card to the bottom.
2. **Reveal**: Reveal cards one by one.
3. **Start Card**: When revealed, triggers shuffle + new round.

### Zones
- `combinedDeckZone` - Merged deck (index 0 = bottom, index Count-1 = top)
- `revealZone` - Currently revealed card

### Controls
- First click: Reveal the next card.
- Second click: Trigger the effect and place the card at the bottom of the deck.

## Effect System

### Trigger Flow
`CostNEffectContainer.InvokeEffectEvent()`: Check cost -> Check effect chain -> Execute effect.

### Effect Chain Manager
`EffectChainManager` prevents infinite loops and tracks nested effect invocations:
- **Chain creation**: A new chain starts when no chains are open, or when the same card triggers a *different* effect object.
- **Loop guard**: The same `effectID` cannot be invoked twice within an open chain.
- **Depth limit**: When `chainDepth` exceeds **99**, further effects are blocked with an error log.
- **Chain closing**: `CloseOpenedChain()` finalizes all open recorders and clears tracking state.

### Cost Types
| Method | Description |
|--------|-------------|
| `Mana(n)` | Requires n stacks of Mana |
| `Rested()` | Consumes Rest status |
| `Revive(n)` | Requires n stacks of Revive |
| `HasEnemyCard(n)` | Requires n enemy cards in the deck |
| `Token Cost` | Consume N friendly Minions of a specified type |
| `Bury Cost` | When activated, place N friendly cards at the bottom |
| `Delay Cost` | When activated, delay N own cards by 1 position |
| `Expose Cost` | When activated, expose N enemy cards to the top |

### Status Effects
```csharp
enum StatusEffect { None, Infected, Mana, HeartChanged, Power, Rest, Revive, Counter }
```

| Effect | Description |
|--------|-------------|
| `Power` | Damage +1 |
| `HeartChanged` | Ownership change |
| `Rest` | Skip trigger |
| `Counter` | Counter-attack / block mechanic |

### Tags
```csharp
enum Tag { None, Linger, ManaX, DeathRattle }
```

## Events

### Card-Specific
| Event | Timing |
|-------|--------|
| `onMeRevealed` | Card revealed |
| `onMeBought` | Card bought in shop |
| `onMeStaged` | Card staged to top |
| `onMeBuried` | Card buried to bottom |
| `onMeGotPower` | Card gains Power |
| `onMeGotStatusEffect` | Card gains any status effect |
| `onThisTagResolverAttached` | Tag resolver attached |

### Global / Faction-Specific
| Event | Timing | Raise Method |
|-------|--------|--------------|
| `onAnyCardRevealed` | Any card revealed | `Raise()` |
| `onHostileCardRevealed` | Hostile card revealed | `Raise()` |
| `afterShuffle` | After shuffle | `Raise()` |
| `beforeRoundStart` | Before round starts | `Raise()` |
| `onTheirPlayerTookDmg` | Opponent took damage | `RaiseOwner()` / `RaiseOpponent()` |
| `onMyPlayerTookDmg` | Self took damage | `RaiseOwner()` / `RaiseOpponent()` |
| `onTheirPlayerHealed` | Opponent healed | `RaiseOwner()` / `RaiseOpponent()` |
| `onMyPlayerHealed` | Self healed | `RaiseOwner()` / `RaiseOpponent()` |
| `onMyPlayerShieldUpped` | Self gained shield | `RaiseOwner()` / `RaiseOpponent()` |
| `onTheirPlayerShieldUpped` | Opponent gained shield | `RaiseOwner()` / `RaiseOpponent()` |
| `onFriendlyMinionAdded` | Friendly minion added | `RaiseOwner()` |
| `onFriendlyCardExiled` | Friendly card exiled | `RaiseOwner()` / `RaiseOpponent()` |
| `onFriendlyFlyExiled` | Friendly fly exiled | `RaiseOwner()` / `RaiseOpponent()` |
| `onAnyCardBuried` | Any card buried | `Raise()` |
| `onFriendlyCardBuried` | Friendly card buried | `RaiseOwner()` / `RaiseOpponent()` |
| `onEnemyCurseCardRevealed` | Enemy curse card revealed | `RaiseOwner()` / `RaiseOpponent()` |
| `onEnemyCurseCardGotPower` | Enemy curse card got Power | `RaiseOwner()` / `RaiseOpponent()` |
| `onAnyCardGotPower` | Any card got Power | `Raise()` |
| `onFriendlyCardGotPower` | Friendly card got Power | `RaiseOwner()` / `RaiseOpponent()` |
| `onEnemyCardGotPower` | Enemy card got Power | `RaiseOwner()` / `RaiseOpponent()` |

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

## Minion Cost Mechanism

When activated, consumes N eligible Minion cards (`isMinion == true`) from the `combinedDeckZone`. If the condition is not met, the effect does not activate.

- `minionCostCount` - Number of minions required
- `minionCostCardTypeID` - Filter by card type ID (empty = no restriction)
- `minionCostOwner` - `Me` / `Them` / `Random`

## Animation System

### Attack Animation
`AttackAnimationManager` plays in queue. Flow: Scale & Rotate -> Dash -> Recoil -> Damage calculation.
- Status Effect damage sets `isStatusEffectDamage = true` to skip the animation.

### Card Movement
`CombatUXManager` provides the following methods:
- `MoveCardToBottom(card, onComplete, duration, useArc)` - Move to bottom
- `MoveCardToTop(card, onComplete, duration, useArc)` - Move to top
- `MoveCardToIndex(card, index, duration, useArc)` - Move to specified index
- `DestroyCardWithAnimation(card)` - Destroy a card with animation
- `AddPhysicalCardToDeck(card)` - Add new physical card to deck
- `SyncPhysicalCardsWithCombinedDeck()` - Sync visuals with logic

### Start Card + Shuffle
Use `PlayStartCardExitWithShuffleAnimation()` to make the Start Card exit and shuffle simultaneously.

## Notes

1. **HPAlterEffect**: Automatically adds `baseDmg.value`. When passing a specific value, set `baseDmg` to 0.
2. **cardTypeID**: Used for saving / statistics / Minion cost filtering (not instance ID).
3. **Anti-loop**: Do not attach multiple looping effect instances to the same card.
4. **GameEvent.Raise Usage Rules**: Use `Raise()` only when the event is **not faction-specific** (e.g., `afterShuffle`, `onAnyCardRevealed`). If the event carries owner/opponent semantics (e.g., `onEnemyCurseCardRevealed`, `onMyPlayerTookDmg`), you must use `RaiseOwner()` or `RaiseOpponent()` based on the actual faction of the trigger object. Directly calling `Raise()` is strictly prohibited.
5. **Neutral Cards**: `isStartCard == true` cards are neutral and skipped by `CombatManager.ShouldSkipEffectProcessing()`.
6. **CardScript Cost Fields**: `buryCost`, `delayCost`, `exposeCost`, `minionCostCount`, `minionCostCardTypeID`.

## Color Tags

| Type | Tag |
|------|-----|
| Damage | `<color=red>` |
| Heal | `<color=#90EE90>` |
| Shield | `<color=grey>` |
| Friendly | `<color=#87CEEB>` |
| Enemy | `<color=orange>` |

---

**Glob**: Use `Assets/**/FileName.cs` instead of `**/FileName.cs`
