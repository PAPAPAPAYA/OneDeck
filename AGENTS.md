# OneDeck - AI Agent Documentation

## Project Overview

OneDeck is a **Unity-based card game** (roguelike deck-builder) inspired by games like Slay the Spire. The game features a unique combat system where cards from both player and enemy decks are combined, shuffled, and revealed one by one to trigger effects. The project uses a **ScriptableObject-based event system** and **UnityEvents** for flexible card effect composition.

### Core Game Loop

1. **Shop Phase**: Buy/sell cards to build your deck
2. **Combat Phase**: Combined deck is shuffled, cards revealed one by one
3. **Result Phase**: Win/lose resolved, hearts and wins tracked

### Key Technologies

- **Unity 6000.0.x** (Unity 6)
- **Universal Render Pipeline (URP)** 17.0.4
- **TextMesh Pro** for UI text rendering
- **C#** scripting with modern .NET

---

## Project Structure

```
Assets/
├── Scripts/
│   ├── Managers/           # Singleton managers for game systems
│   ├── Effects/            # Card effect implementations
│   │   ├── shop/           # Shop-specific effects
│   │   └── StatusEffect/   # Status effect related effects
│   ├── Card/               # Core card components
│   ├── SOScripts/          # ScriptableObject definitions
│   ├── Editor/             # Custom Unity Editor tools
│   ├── EffectRecorder.cs   # Effect chain tracking
│   └── GameEventListener.cs # Event listener component
├── Prefabs/
│   ├── Cards/              # Card prefabs organized by type
│   │   ├── Curse/          # Curse cards
│   │   ├── Graveyard/      # Graveyard interaction cards
│   │   ├── Heal/           # Healing cards
│   │   ├── HeartChange/    # Ownership change cards
│   │   ├── Infection/      # Infection status cards
│   │   ├── Linger/         # Linger effects cards
│   │   ├── Mana/           # Mana system cards
│   │   ├── Power/          # Power buff cards
│   │   ├── Selfharm/       # Self-damage cards
│   │   ├── Shield/         # Shield cards
│   │   ├── Shiv/           # Shiv generation cards
│   │   ├── System/         # System cards (fatigue, etc.)
│   │   └── TestCards/      # Testing cards
│   ├── Managers/           # Manager prefabs
│   └── StatusEffectResolvers/ # Status effect resolver prefabs
├── SORefs/                 # ScriptableObject instances
│   ├── CombatRefs/         # Combat-related SO instances
│   ├── Decks/              # Deck definitions
│   ├── EnemyRefs/          # Enemy status references
│   ├── FlowRefs/           # Game flow references
│   ├── GameEvents/         # GameEvent SO instances
│   │   ├── ANY/            # Global events
│   │   └── SPECIFIC/       # Card-specific events
│   ├── PlayerRefs/         # Player status references
│   │   └── attributes/     # Player attributes
│   └── ShopRefs/           # Shop-related references
├── Scenes/                 # Unity scenes
├── Settings/               # URP settings
└── TestWriteRead/          # Deck save/load system
```

---

## Architecture

### Design Patterns Used

1. **Singleton Pattern**: All major managers use singleton pattern for global access
2. **ScriptableObject Pattern**: Game events, player status, and deck data use SOs
3. **Event-Driven Architecture**: Custom GameEvent SOs with listener registration
4. **Component-Based Card System**: Cards composed of multiple effect components

### Core Systems

#### 1. Combat System (`CombatManager.cs`)

The central combat controller that manages:
- **Combat States**: `GatherDeckLists` → `ShuffleDeck` → `Reveal`
- **Combined Deck**: Merges player and enemy cards
- **Zones**: Combined deck, reveal zone, graveyard
- **Overtime**: Fatigue system after certain rounds

Key flow:
```csharp
// Combat state machine in Update()
GatherDecks()     // Instantiate cards from DeckSO
Shuffle()         // Shuffle combined deck
RevealCards()     // Reveal one by one, trigger effects
```

#### 2. Phase System (`PhaseManager.cs`)

Controls game phase transitions:
- `Shop` → `Combat` → `Result` → `Shop` (loop)
- UnityEvents for phase enter/exit
- Tracks wins, hearts, and session number

#### 3. Effect System

**Effect Chain Manager** (`EffectChainManager.cs`):
- Prevents infinite loops (max depth: 99)
- Tracks effect chains using `EffectRecorder` GameObjects
- Prevents same effect from invoking itself
- Closes chains when: same card different effect, or player input awaited

**Cost & Effect Container** (`CostNEffectContainer.cs`):
- Packages costs with effects
- Cost checking via UnityEvent (sets `_costNotMetFlag`)
- Effect invocation through UnityEvent

**Base Effect Script** (`EffectScript.cs`):
- Parent class for all effects
- Provides common context: `combatManager`, `myCard`, `myCardScript`

#### 4. Event System (`GameEvent.cs`, `GameEventStorage.cs`)

Custom ScriptableObject event system:

```csharp
// Event types:
// - Specific: RaiseSpecific(GameObject) - targets a card
// - Owner: RaiseOwner() - session owner's cards
// - Opponent: RaiseOpponent() - enemy's cards
// - Global: Raise() - all listeners
```

Available events in `GameEventStorage`:

**Card-specific:**
- `onMeRevealed` - When card is revealed
- `onMeSentToGrave` - When card enters graveyard
- `onMeBought` - When card is bought in shop
- `onThisTagResolverAttached` - When status effect resolver is attached

**Global:**
- `onAnyCardRevealed` - Any card revealed
- `onAnyCardSentToGrave` - Any card enters graveyard
- `onAnyCardRevived` - Any card revived from graveyard
- `afterShuffle` - After deck shuffle
- `beforeRoundStart` - Before new round starts
- `onMyPlayerTookDmg` / `onTheirPlayerTookDmg` - Damage events
- `onMyPlayerHealed` / `onTheirPlayerHealed` - Heal events
- `onMyPlayerShieldUpped` / `onTheirPlayerShieldUpped` - Shield events

#### 5. Status Effect System

Status effects are enums stored in `CardScript.myStatusEffects`:
- `None` - No status effect
- `Infected` - Triggers damage when revealed
- `Mana` - Resource for paying costs
- `Power` - Increases damage dealt
- `HeartChanged` - Tracks ownership change
- `Rest` - Used for rested cards (graveyard interaction)
- `Revive` - Undead status - returns card from graveyard to deck

**Status Effect Resolvers** (`ResolverScript.cs`):
- Attached to cards when status effect is applied
- Listens to GameEvents and triggers effects

#### 6. Shop System (`ShopManager.cs`)

- Buy cards: Number keys 1-6
- Sell cards: Press 'S' to toggle sell mode, then number keys
- Reroll shop: Press 'R'
- Payday on entering shop
- Deck size limit enforcement
- Cards can have `takeUpSpace` = false (temporary cards)

#### 7. Deck Persistence (`DeckSaver.cs`)

JSON-based deck saving/loading:
- Save: Ctrl+S
- Load: Ctrl+L
- Wipe: Ctrl+W
- Matches enemy decks by session number

---

## Code Organization

### Namespaces

- `DefaultNamespace` - Core game classes
- `DefaultNamespace.Managers` - Manager classes
- `DefaultNamespace.Effects` - Effect implementations
- `DefaultNamespace.SOScripts` - ScriptableObject classes
- `TagSystem` - Status effect related
- `TestWriteRead` - Deck persistence

### Key Enums (`EnumStorage.cs`)

```csharp
GamePhase { Combat, Shop, Result }
CombatState { GatherDeckLists, ShuffleDeck, Reveal }
TargetType { Me, Them, Random }
StatusEffect { None, Infected, Mana, HeartChanged, Power, Rest, Revive }
```

---

## Card Structure

A card prefab consists of:

```
CardPrefab (GameObject)
├── CardScript (Component) - Core card data
├── GameEventListener(s) (Components) - Listen to events
└── EffectContainer(s) (GameObject(s))
    ├── CostNEffectContainer (Component) - Cost check & invoke
    └── Effect Script(s) (Component) - Effect logic
```

### CardScript Properties

```csharp
int cardID;                    // Unique card ID
string cardDesc;               // Card description
bool takeUpSpace;              // Whether card counts toward deck size
int price;                     // Card price in shop
PlayerStatusSO myStatusRef;    // Card owner's status
PlayerStatusSO theirStatusRef; // Opponent's status
List<StatusEffect> myStatusEffects; // Applied status effects
```

### Creating a New Card

1. Duplicate an existing card prefab
2. Modify `CardScript` values (description, price, takeUpSpace, etc.)
3. Add/configure effect containers with:
   - Cost check functions
   - Effect implementations
4. Wire up GameEventListeners to appropriate GameEvents

---

## Effect Types

Located in `Assets/Scripts/Effects/`:

### Core Effects

| Effect | Description |
|--------|-------------|
| `HPAlterEffect` | Deal damage or heal HP. Supports Power buffs, shield processing |
| `ShieldAlter` | Add/remove shield |
| `CardManipulationEffect` | Move cards (stage, bury, grave, revive) |
| `ChangeCardTarget` | Change card ownership (heart-change) |
| `AddTempCard` | Generate temporary cards |
| `ChangeHpAlterAmountEffect` | Modify damage/heal amounts dynamically |
| `HpMaxAlterEffect` | Change max HP |
| `PrintEffect` | Debug logging |

### Status Effect Related (`Effects/StatusEffect/`)

| Effect | Description |
|--------|-------------|
| `StatusEffectGiverEffect` | Base class for applying status effects to cards |
| `InfectionEffect` | Infected status (triggers damage on reveal) |
| `HeartChangeEffect` | Applies HeartChanged status |
| `ManaAlterEffect` | Consume mana status effects |
| `GivePowerStatusEffectEffect` | Apply Power buffs |
| `ConsumeStausEffect` | Remove/consume status effects |

### Shop Effects (`Effects/shop/`)

| Effect | Description |
|--------|-------------|
| `DeckSizeIncreaseEffect` | Increase max deck size |

### Cost Types (in `CostNEffectContainer.cs`)

- `CheckCost_Infected()` - Requires Infected status
- `CheckCost_Mana(int)` - Requires X mana stacks
- `CheckCost_InGrave()` - Must be in graveyard
- `CheckCost_Revive(int)` - Requires X Revive (Undead) stacks
- `CheckCost_Rested(int)` - Requires X Rest stacks

---

## Editor Tools

Located in `Assets/Scripts/Editor/`:

- `RaiseGameEventButton.cs` - "Raise" button in GameEvent SO inspector
- `AddADividerToCostNEffectScript.cs` - Visual separator in CostNEffectContainer

---

## Testing & Balancing

### DeckTester (`DeckTester.cs`)

Automated testing system:
- Set `autoSpace = true` for automated play
- Set `sessionAmountTarget` for test iterations
- Tracks: Win rates, average HP, damage output statistics

Metrics collected:
- Win rates for both decks
- Average HP at end of combat
- Average damage to opponent per session
- Average damage to self per session

---

## Build & Development

### Requirements

- Unity 6000.0.x or later
- URP template compatible version

### Key Input Controls

**Combat:**
- `Space` - Confirm/Advance/Reveal next card

**Shop:**
- `Space` - Leave shop
- `1-6` - Buy/Sell cards
- `S` - Toggle sell mode
- `R` - Reroll shop

**Debug:**
- `Ctrl+S` - Save deck to JSON
- `Ctrl+L` - Load deck from JSON
- `Ctrl+W` - Wipe saved decks

### ScriptableObject Creation

Available create menu paths:
- `SORefs/PlayerStatusSO` - Player status definition
- `SORefs/DeckSO` - Deck definition
- `SORefs/GamePhaseSO` - Game phase tracker
- Root menu for `GameEvent` - Custom game events

---

## Important Implementation Notes

1. **Effect Chain Safety**: Effect chains close when:
   - Same card, different effect tries to invoke
   - Player input is awaited
   - Chain depth exceeds 99

2. **No `beforeIDealDmg` Event**: Removed to prevent stack overflow; HPAlterEffect calculates damage before dealing

3. **Tag Damage Attribution**: Damage from status effects counts as the tag owner's card damage

4. **Instance ID Warning**: Beware of changing prefab instance IDs (affects deck saving)

5. **Card ID System**: Cards get unique IDs via `CardIDRetriever` for tracking

6. **Heart-Change Strategy**: Having only heart-change cards is overpowered; design costs accordingly

7. **Overtime/Fatigue**: After `overtimeRoundThreshold` rounds, fatigue cards are added to both decks each shuffle

---

## Dependencies (from Packages/manifest.json)

Core packages:
- `com.unity.render-pipelines.universal` - URP rendering
- `com.unity.inputsystem` - New Input System
- `com.unity.test-framework` - Unit testing
- `com.unity.textmeshpro` - Text rendering
- `com.unity.timeline` - Cinematic sequencing
- `com.unity.visualscripting` - Visual scripting support

---

## File Locations Quick Reference

| Purpose | Path |
|---------|------|
| Main combat logic | `Assets/Scripts/Managers/CombatManager.cs` |
| Phase control | `Assets/Scripts/Managers/PhaseManager.cs` |
| Card definition | `Assets/Scripts/Card/CardScript.cs` |
| Effect base class | `Assets/Scripts/Effects/EffectScript.cs` |
| GameEvent SO | `Assets/Scripts/SOScripts/GameEvent.cs` |
| Deck definition SO | `Assets/Scripts/SOScripts/DeckSO.cs` |
| Player status SO | `Assets/Scripts/SOScripts/PlayerStatusSO.cs` |
| Save/load system | `Assets/TestWriteRead/DeckSaver.cs` |
| Development notes | `Assets/DevLog.cs` |

---

## DevLog Notes (Key Design Decisions)

See `Assets/DevLog.cs` for detailed development notes including:
- Card design patterns (Infection, Mana, Shield, Graveyard, etc.)
- Refactoring history
- Abandoned features
- Implementation quirks and warnings

Key quirks to remember:
- Deck and grave effects don't show fail messages (intentional)
- After changing the combined deck, shuffle
- Currently only use round num to match decks
- Don't put multiple effect instances with loopable effect in one card (will stack overflow)
- Put multiple loopable effects in same effect instance instead
