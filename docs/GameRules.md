# OneDeck Game Rules Document

> Auto-generated from card design document (`docs/3.0_no_cost_CardDesign.md`) and project architecture (`AGENTS.md`).
> This document serves as the authoritative reference for all game mechanics, keywords, and card behaviors.
> Version: 3.0 No Cost

---

## Table of Contents

- [Game Overview](#game-overview)
- [Victory Condition](#victory-condition)
- [Deck and Zones](#deck-and-zones)
- [Core Loop: Combat Flow](#core-loop-combat-flow)
- [Player Stats](#player-stats)
- [Keywords and Mechanics](#keywords-and-mechanics)
- [Card Tags](#card-tags)
- [Cost Types](#cost-types)
- [Status Effects](#status-effects)
- [Event Triggers](#event-triggers)
- [Faction / Archetype Mechanics](#faction--archetype-mechanics)
- [Card Categories Summary](#card-categories-summary)
- [Anti-Loop and Chain Rules](#anti-loop-and-chain-rules)
- [Notes and Edge Cases](#notes-and-edge-cases)

---

## Game Overview

OneDeck is a roguelike card game where both players' decks are merged into a single deck, shuffled together, and cards are revealed one by one. Each revealed card triggers its effects based on its owner, type, and current game state.

The core loop outside combat is:

```
Shop -> Combat -> Result -> Shop
```

---

## Victory Condition

Combat ends when one player's HP reaches 0. The surviving player wins the combat.

---

## Deck and Zones

### `combinedDeckZone`

The merged deck containing all cards from both players.

- Index `0` = bottom of the deck.
- Index `Count - 1` = top of the deck.
- New cards are typically added to the bottom unless an effect states otherwise.

### `revealZone`

The currently revealed card. Only one card can be in the reveal zone at a time.

### Graveyard ("墓地")

Conceptually, cards positioned **before the Start Card** in the `combinedDeckZone` are considered "in the graveyard". The Start Card acts as the dividing line between the active deck and the graveyard.

### Exile

A card removed from the game entirely. It no longer exists in any zone.

---

## Core Loop: Combat Flow

### 1. Gather Decks

At the start of combat:

- Both players' decks are merged into `combinedDeckZone`.
- The **Start Card** is added to the **bottom** of the merged deck.

### 2. Round Structure

A round consists of revealing cards from the top of `combinedDeckZone` one by one until the Start Card is reached.

### 3. Reveal and Trigger

**First click**: Reveal the next card from the top of the deck and move it to `revealZone`.

**Second click**: Trigger the revealed card's effects and then place the card at the **bottom** of `combinedDeckZone`.

### 4. Start Card

When the Start Card is revealed:

- It triggers a shuffle of `combinedDeckZone`.
- After the shuffle, the Start Card is reinserted at a position determined by its `StartCardShuffleEffect.startCardPlacement`: `Gaussian` (default) = Gaussian distribution centered on the deck middle (never at the top, to avoid an immediate re-shuffle); `AlwaysBottom` = fixed at the **bottom**. A `ShuffleOrderOverride` custom order takes precedence over both.
- A new round begins.

> Start Cards are **neutral** (`isStartCard == true`). They are skipped by effect processing.

---

## Player Stats

Each player has the following stats during combat:

| Stat | Description |
|------|-------------|
| `HP` | Current hit points. Reaching 0 means defeat. |
| `Max HP` | Maximum hit points. Can be altered by effects. |
| `Shield` | Absorbs incoming damage before HP is reduced. |

### Damage Application Order

1. Shield absorbs damage first.
2. Remaining damage reduces HP.
3. If HP drops to 0 or below, the player is defeated.

---

## Keywords and Mechanics

### Reveal ("揭晓")

When a card is drawn from the top of `combinedDeckZone` and placed into `revealZone`. This is the primary trigger for most card effects.

### Bury ("埋葬")

Move a card to the **bottom** of `combinedDeckZone`.

- Burying is not exiling; the card remains in the deck.
- Burying a card can trigger `OnMeBuried` and `OnFriendlyCardBuried` events.

### Stage ("置顶")

Move a card to the **top** of `combinedDeckZone`.

- Staged cards will be revealed sooner.
- Staging can trigger `OnMeStaged` events.

### Exile ("去除")

Remove a card from the game entirely. The card leaves all zones and will not be shuffled back.

- Exiling can trigger `OnFriendlyCardExiled` events.

### Power ("力量")

A stackable status effect. Each stack of Power increases the card's damage output by **+1**.

- Power can be gained, consumed, or transferred.
- A card's Power is used as a modifier in some damage calculations.

### Enhance Curse ("增强诅咒")

Increase the Power of a curse card (`JU_ON`). Each "enhance" typically adds **+1 Power** to the target curse.

### Counter ("计数器")

A generic stackable status effect used by some cards to track internal state (e.g., how many times a card has been revealed). When a Counter requirement is met, a secondary effect triggers.

### Linger / Grave ("萦绕")

Cards with the `Linger` tag can trigger their effects even while positioned **before the Start Card** (i.e., in the graveyard portion of the deck).

- Linger effects typically check `CheckCost_IndexBeforeStartCard` to ensure the card is in the graveyard before triggering.

### DeathRattle ("亡语")

A tag indicating that the card has an effect that triggers **exclusively** when it is **buried** (`OnMeBuried`). DeathRattle effects are **not** triggered by Exile, Stage, or any other zone transition.

### Minion ("随从")

A card flagged as `isMinion == true`. Minion cards can be consumed as a cost for certain effects.

### Damage Multiplication (`x N`)

Some effects deal damage "x N" (e.g. `HPAlterEffect.DecreaseTheirHpTimesX(N)`), meaning the damage effect is executed N separate times:

- Each hit is a **separate damage application**: damage is resolved against shield/HP independently per hit, and each hit plays its own attack animation.
- Power stacks are counted **per hit** (`DmgCalculator()` runs on every loop iteration), so the total damage is `N x (baseDmg + Power)` — Power scales the entire package, not just one hit.
- The hits are **not** independent reaction events: the whole loop executes inside a single effect invocation with the chain kept open, and the anti-loop guard (`EffectChainManager.EffectCanBeInvoked` — same card instance + same effect object already processed in the opened chains) lets on-damage reactions trigger only **once** per chain, not once per hit.

### Life (Reveals per Round)

Some cards carry a `life` stat (`CardScript.lifeMax`): how many times the card may be revealed per round. `lifeMax = 0` (the default for all current cards) reproduces the classic behavior exactly — the card reveals once per round. The authoritative rules are R1–R13 below (plan: `plans/prd-card-life-system-2026-08-07.md`).

- **R1 (Life stat)**: Every card has `lifeMax` (configured, >= 0, default 0; design range 0–3) and `currentLife` (runtime). `currentLife` is reset to `lifeMax` at the start of every round.
- **R2 (Bounce rule)**: Whenever a card with `currentLife > 0` would be placed into the grave (destination index 0 region, below the Start Card): decrement `currentLife` by 1 and place the card at the **queue tail** instead — the slot immediately above the Start Card (`startCardIndex + 1`). The card will be revealed again this round.
- **R3 (Life 0)**: A card with `currentLife == 0` (or `lifeMax == 0`) is placed into the grave normally. Current behavior is unchanged.
- **R4 (Reset)**: At round start (Start Card shuffle completes), every card in `combinedDeckZone` has `currentLife = lifeMax`. The Start Card is fixed at `lifeMax = 0`.
- **R5 (Rest)**: Rest skips the card's trigger but **not** the life consumption or the bounce.
- **R6 (Curses and tokens)**: Curse cards (`[curse]`, e.g. JU_ON) and rift tokens (`[次元裂缝]`) are hard-fixed to `lifeMax = 0` unless an explicit exception is approved.
- **R7 (Symmetric)**: The rules apply identically to both sides (async PVP target; recorded enemy decks are test/fallback only).
- **R8 (Neutral)**: The Start Card never consumes life and never bounces.
- **R9 (Stage)**: Staging a card from the grave gives it one extra reveal but does **not** restore life. The normal rule applies when the staged card returns.
- **R10 (Exile)**: Exile removes the card from the game; no life interaction. Exile remains the only hard removal.
- **R11 (Fatigue)**: `totalCardsRevealed` counts every reveal, including bounce re-reveals — life cards accelerate fatigue (accepted lever, not a bug).
- **R12 (Delay exception)**: Delay shifts a card one slot toward the bottom and never triggers the bounce, even if the shift crosses below the Start Card.
- **R13 (Shuffle window)**: Bounces do not occur during the end-of-round shuffle resolution; the life reset (R4) follows immediately.

**Bounce placement details**: multiple bounced cards stack at the queue tail **LIFO** (the last card bounced is the topmost of the tail stack, i.e. revealed first). When a card with life left is buried, its bury events (`onMeBuried` / `onAnyCardBuried` / `onFriendlyCardBuried`) fire normally with the card in the grave; the bounce applies **after** reactive resolution, and only if reactions did not stage, exile, or destroy the card. The queue tail doubles as the **setup** slot for positional targeting predicates (e.g. “bury the deepest active friendly”).

---

## Card Tags

| Tag | Name | Effect |
|-----|------|--------|
| `Linger` | 萦绕 | Card triggers while in graveyard (before Start Card). |
| `DeathRattle` | 亡语 | Card triggers an effect when buried. |
| `ManaX` | - | Reserved for mana-related mechanics. |

---

## Cost Types

Not all cards have costs. In the 3.0 "No Cost" design, most effects trigger freely on reveal. However, some cards require specific conditions or consume resources:

| Cost Method | Description |
|-------------|-------------|
| `None` | Effect triggers automatically upon the specified event. |
| `Power(n)` | Requires the card to have `n` stacks of Power. Consumes them on activation. |
| `Counter(n)` | Requires the card to have `n` stacks of Counter. Consumes them on activation. |
| `MinionCost` | Consume `N` friendly Minion cards from `combinedDeckZone` as a prerequisite. Can be filtered by `cardTypeID`. |
| `BuryCost` | When activated, place `N` friendly cards at the bottom as part of the cost. |
| `DelayCost` | When activated, delay `N` own cards by 1 position. |
| `ExposeCost` | When activated, expose `N` enemy cards to the top. |
| `HasEnemyCard(n)` | Requires `n` enemy cards to exist in the deck. |
| `EnemyCurseHasPower(n)` | Requires enemy curse card(s) to have at least `n` Power. Consumes the Power. |

### Cost Check Flow

```
1. Check prerequisite cost (if any).
2. Check effect chain constraints (anti-loop).
3. Execute effect(s).
```

If any cost check fails, the entire effect container is skipped.

---

## Status Effects

```csharp
enum StatusEffect { None, Infected, Mana, HeartChanged, Power, Rest, Revive, Counter }
```

| Status Effect | Description |
|---------------|-------------|
| `None` | No status effect. |
| `Infected` | Unused in current design. |
| `Mana` | Used for mana-cost mechanics. |
| `HeartChanged` | Ownership change mechanic. |
| `Power` | Each stack increases damage by +1. |
| `Rest` | Card skips its trigger. |
| `Revive` | Used for revive-cost mechanics. |
| `Counter` | Generic stackable counter for internal tracking. |

---

## Event Triggers

### Card-Specific Events

| Event | Timing |
|-------|--------|
| `onMeRevealed` | This card is revealed from the deck. |
| `onMeBought` | This card is bought in the shop. |
| `onMeStaged` | This card is staged to the top of the deck. |
| `onMeBuried` | This card is buried to the bottom of the deck. |
| `onMeGotPower` | This card gains a Power status effect. |
| `onMeGotStatusEffect` | This card gains any status effect. |
| `onThisTagResolverAttached` | A tag resolver is attached to this card. |

### Global / Faction-Specific Events

| Event | Timing | Raise Rule |
|-------|--------|------------|
| `onAnyCardRevealed` | Any card is revealed. | `Raise()` |
| `onHostileCardRevealed` | A hostile card is revealed. | `Raise()` |
| `afterShuffle` | Deck is shuffled. | `Raise()` |
| `beforeRoundStart` | Before a new round starts. | `Raise()` |
| `onTheirPlayerTookDmg` | Opponent took damage. | `RaiseOwner()` / `RaiseOpponent()` |
| `onMyPlayerTookDmg` | Self took damage. | `RaiseOwner()` / `RaiseOpponent()` |
| `onTheirPlayerHealed` | Opponent healed. | `RaiseOwner()` / `RaiseOpponent()` |
| `onMyPlayerHealed` | Self healed. | `RaiseOwner()` / `RaiseOpponent()` |
| `onMyPlayerShieldUpped` | Self gained shield. | `RaiseOwner()` / `RaiseOpponent()` |
| `onTheirPlayerShieldUpped` | Opponent gained shield. | `RaiseOwner()` / `RaiseOpponent()` |
| `onFriendlyMinionAdded` | Friendly minion added. | `RaiseOwner()` |
| `onFriendlyCardExiled` | Friendly card exiled. | `RaiseOwner()` / `RaiseOpponent()` |
| `onFriendlyFlyExiled` | Friendly fly exiled. | `RaiseOwner()` / `RaiseOpponent()` |
| `onAnyCardBuried` | Any card buried. | `Raise()` |
| `onFriendlyCardBuried` | Friendly card buried. | `RaiseOwner()` / `RaiseOpponent()` |
| `onEnemyCurseCardRevealed` | Enemy curse card revealed. | `RaiseOwner()` / `RaiseOpponent()` |
| `onEnemyCurseCardGotPower` | Enemy curse card got Power. | `RaiseOwner()` / `RaiseOpponent()` |
| `onAnyCardGotPower` | Any card got Power. | `Raise()` |
| `onFriendlyCardGotPower` | Friendly card got Power. | `RaiseOwner()` / `RaiseOpponent()` |
| `onEnemyCardGotPower` | Enemy card got Power. | `RaiseOwner()` / `RaiseOpponent()` |

> **Important**: For faction-specific events, always use `RaiseOwner()` or `RaiseOpponent()`. Direct `Raise()` is strictly prohibited for these events.

---

## Faction / Archetype Mechanics

The 3.0 card pool is divided into four thematic categories. Each category has distinct mechanical identities.

### 1. Bury and Buried (墓地 / 亡语)

**Core Theme**: Manipulating the deck by burying cards and leveraging DeathRattle triggers.

**Key Mechanics**:

- **Bury**: Move cards to the bottom of the deck.
- **DeathRattle**: Effects that trigger when a card is buried.
- **Graveyard Scaling**: Effects that scale based on the number of friendly cards in the graveyard (before the Start Card).

**Example Behaviors**:

- Deal damage when revealed, then trigger a powerful effect when buried.
- Bury friendly cards to fuel DeathRattle effects.
- Scale damage based on graveyard size.
- Linger effects that react to friendly cards being buried.

### 2. Conjure (次元 / 召唤)

**Core Theme**: Creating and consuming "Rift" (`次元裂缝`) tokens as a resource.

**Key Mechanics**:

- **Rift Tokens**: Temporary cards (`次元裂缝`) that are Minions and can be exiled as a cost.
- **Minion Cost**: Many Conjure cards require exiling a `[次元裂缝]` as a prerequisite.
- **Exile Synergy**: Some cards have Linger effects that trigger when friendly cards are exiled.

**Example Behaviors**:

- Generate `[次元裂缝]` tokens.
- Consume `[次元裂缝]` to deal damage, stage friendly cards, or bury enemies.
- Linger effects that trigger on friendly exile.

### 3. Curse (诅咒)

**Core Theme**: Manipulating the `JU_ON` curse card by enhancing its Power and leveraging its self-damaging nature.

**Key Mechanics**:

- **Curse Card (`JU_ON`)**: A special card that deals damage to its owner equal to its Power stacks when revealed.
- **Enhance Curse**: Add Power to enemy curse cards, increasing their self-damage.
- **Curse Power Consumption**: Some effects consume Power from enemy curses as a cost.
- **Curse Synergy**: Effects that trigger when enemy curses are revealed or gain Power.

**Example Behaviors**:

- Enhance enemy curse to make it deal more self-damage.
- Consume curse Power to stage friendly cards.
- Linger effects that trigger when enemy curse gains Power.
- Transfer friendly Power to enemy curse.

### 4. General (通用)

**Core Theme**: Flexible utility cards with a mix of damage, Power manipulation, and deck control.

**Key Mechanics**:

- **Power Manipulation**: Give, consume, transfer, or amplify Power.
- **Direct Damage**: Simple damage effects.
- **Deck Control**: Stage and bury without faction-specific restrictions.
- **Scaling Effects**: Damage or effects that scale with deck state (e.g., number of friendly cards, staged count, total Power).

**Example Behaviors**:

- Deal damage and give Power to friendly cards.
- Consume Power for multi-part effects.
- Scale damage with total Power across all cards.
- Counter-based effects that trigger every N reveals.

---

## Card Categories Summary

| Category | Count | Core Identity |
|----------|-------|---------------|
| Bury and Buried | 16 | Graveyard manipulation, DeathRattle triggers |
| Conjure | 11 | Rift token generation and consumption |
| Curse | 15 | JU_ON curse enhancement and exploitation |
| General | 26 | Flexible utility, Power manipulation, direct damage |
| **Total** | **68** | |

---

## Anti-Loop and Chain Rules

### Effect Chain Manager

The `EffectChainManager` prevents infinite loops:

- **Chain Creation**: A new chain starts when no chains are open, or when the same card triggers a **different** effect object.
- **Loop Guard**: The same `effectID` cannot be invoked twice within an open chain.
- **Depth Limit**: When `chainDepth` exceeds **99**, further effects are blocked with an error log.
- **Chain Closing**: `CloseOpenedChain()` finalizes all open recorders and clears tracking state.

### Best Practices

- Do not attach multiple looping effect instances to the same card.
- Ensure Linger effects have proper cost checks (`CheckCost_IndexBeforeStartCard`) to prevent unintended triggers.

---

## Notes and Edge Cases

### Damage Calculation

`HPAlterEffect` automatically adds `baseDmg.value` to the calculated damage. If you want a specific fixed value, set `baseDmg` to 0 and use `extraDmg`.

### Neutral Cards

`isStartCard == true` cards are neutral. They are skipped by `CombatManager.ShouldSkipEffectProcessing()`.

### Card Type ID

`cardTypeID` is used for saving, statistics, and Minion cost filtering. It is **not** an instance ID.

### Color Tags for UI

| Type | Tag |
|------|-----|
| Damage | `<color=red>` |
| Heal | `<color=#90EE90>` |
| Shield | `<color=grey>` |
| Friendly | `<color=#87CEEB>` |
| Enemy | `<color=orange>` |

---

> End of Game Rules Document
