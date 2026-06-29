# Game Event Trigger Probability Analysis Framework

> Analysis based on the OneDeck 3.0 no-cost card pool (69 cards parsed from `docs/CardDesign_GenerationLog.txt`).
> Last updated: 2026-06-27

## 1. Why Trigger Probability Matters

In OneDeck, most card effects are reactive: a card installs a `GameEventListener` on a specific `GameEvent` and waits for that event to be raised. If the event is rarely raised, or if the raise is unlikely to hit the listening card, the card becomes inconsistent or dead. This document provides a framework for measuring and improving that consistency.

## 2. Framework Dimensions

For every `GameEvent`, evaluate four dimensions:

| Dimension | Question | Metric |
|-----------|----------|--------|
| **Source Density** | How many cards/effects can raise this event? | Count of raising effects + frequency of their triggers |
| **Raise Reach** | When raised, how many listeners can possibly hear it? | `Raise()` = all, `RaiseOwner/RaiseOpponent()` = faction-half, `RaiseSpecific()` = one target |
| **Selection Precision** | For `RaiseSpecific`, how precisely can a source choose the listener? | Deterministic (last X, all friendly), stochastic (random friendly), or self-only |
| **Listener Count** | How many cards are listening? | Count from the card pool |

A card's **effective trigger probability** can be approximated as:

```
P(trigger) ≈ P(event is raised) × P(listener is in the reachable set) × P(listener is selected | reachable)
```

Events with low source density, narrow reach, and random selection are the most fragile.

## 3. Event Classification in 3.0

### 3.1 High-reliability events

| Event | Listeners | Raise Rule | Why reliable |
|-------|-----------|------------|--------------|
| `onMeRevealed` | 69 | per card | Every card triggers itself on reveal; guaranteed |
| `onAnyCardRevealed` | implicit | `Raise()` | Fired every reveal; global reach |
| `onTheirPlayerTookDmg` / `onMyPlayerTookDmg` | 2 | faction | Combat core loop generates damage constantly |

### 3.2 Medium-reliability events

| Event | Listeners | Notes |
|-------|-----------|-------|
| `onMeBuried` | 9 | Requires bury synergy; several sources exist |
| `onMeStaged` | 5 | Less common than bury but has dedicated cards |
| `onFriendlyCardExiled` | 3 (2 Linger) | Tied to Conjure/minion-cost archetype |
| `onHostileCardRevealed` | 2 (both Linger) | Curse/Linger synergy |

### 3.3 Low-reliability / design-risk events

| Event | Listeners | Risk |
|-------|-----------|------|
| `onMeGotStatusEffect` | 1 (POWER_CRAVER) | Very narrow listener base; source must hit exactly this card |
| `onMeGotPower` | 0 | Empty design space |
| `onAnyCardGotPower` | 0 | Empty design space |
| `onEnemyCardGotPower` | 0 | Empty design space |
| `onFriendlyCardGotPower` | 1 (WEAPON_SPIRIT, Linger) | Only one enabler, but uses `lastCardGotPower` so it is deterministic once the event fires |

## 4. Deep Dive: `onMeGotStatusEffect`

### 4.1 How it is raised

`EffectScript.ApplyStatusEffectCore` raises `onMeGotStatusEffect` via `RaiseSpecific(targetCardScript.gameObject)`. Therefore **every** status-effect gain in the game raises this event, but only on the exact card that received the effect. The source density is actually high (roughly 17 status-giving effects in 3.0).

### 4.2 Why it still feels unreliable

The bottleneck is **target specificity**. For a listener on `onMeGotStatusEffect` to fire, the card gaining the status effect must be the exact card with the listener. Most status givers in 3.0 use random or area targeting:

| Source Card | Method | Targeting | P(hit POWER_CRAVER) |
|-------------|--------|-----------|---------------------|
| BLACKSMITH | `GiveStatusEffectToXFriendly(1)` | 1 random friendly | `1 / N_friendly_valid` |
| CURSE_THIRST_SHAMAN | `GiveStatusEffectToXFriendly_BasedOnIntSO` | random friendly per curse power | `1 / N_friendly_valid` per iteration |
| MAD_SCIENTIST | `GiveStatusEffectToLastXCards(3)` | last 3 cards in deck order | deterministic if POWER_CRAVER is in last 3 |
| BLIND_COMBAT_PRIEST | `GiveStatusEffectToLastXCards(1)` | last 1 card | deterministic if POWER_CRAVER is next |
| OVERCHARGED_SUMMONER | `GiveStatusEffectToLastXCards(1)` | last 1 card | deterministic if POWER_CRAVER is next |
| MARTYR | `GiveAllFriendlyStatusEffect` | all friendly | 1 (guaranteed) |
| POWER_TRANSFER | `GiveStatusEffect(2)` | random friendly | `1 / N_friendly_valid` per layer |
| ELDER_SORCERER | `GiveStatusEffectToXFriendly_BasedOnStaged` | random friendly per staged count | `1 / N_friendly_valid` per iteration |
| ALMIGHTY | `GiveStatusEffect(1)` | random ? | `1 / N_friendly_valid` |
| AVENGER | `GiveSelfStatusEffect` | self | 0 for POWER_CRAVER |
| TACTICAL_BREACHER | `GiveSelfStatusEffect` | self | 0 for POWER_CRAVER |

`N_friendly_valid` is the number of friendly cards that can receive the status effect (excluding full/neutral cards). In a typical mid-game deck this can be 10-30 cards, making the random-target probability very low.

### 4.3 Case study: BLACKSMITH → POWER_CRAVER

- BLACKSMITH reveals and gives 1 layer of Power to 1 random friendly card.
- `onMeGotStatusEffect` is raised on that single target.
- POWER_CRAVER's listener fires only if it was the chosen target.
- Approximate probability: `1 / N_friendly_valid`.

For example, with 15 friendly cards: `P ≈ 6.7%` per BLACKSMITH reveal. That is a low-probability combo for a card whose entire value depends on it.

### 4.4 Better partners for POWER_CRAVER

- **MARTYR** (`GiveAllFriendlyStatusEffect` on bury): guaranteed to hit POWER_CRAVER.
- **Self-givers on POWER_CRAVER itself**: impossible unless POWER_CRAVER has a self-gain effect.
- **Last-X givers** (MAD_SCIENTIST, BLIND_COMBAT_PRIEST, OVERCHARGED_SUMMONER): reliable only if the deck is ordered so POWER_CRAVER sits in the last X slot.

## 5. Contrast with `onFriendlyCardGotPower`

`WEAPON_SPIRIT` listens to `onFriendlyCardGotPower` while Lingering in the graveyard. When any friendly card gains Power, `lastCardGotPower` is recorded, and `PowerReactionEffect.GivePowerToCardThatGotPower` gives additional Power to **that same card**.

This design is far more reliable because:
1. The source event (`onFriendlyCardGotPower`) is raised by every Power gain.
2. The reaction does not need to randomly select the listener; it buffs the card that caused the event.
3. `excludeSelf=false` allows chaining.

Lesson: reactive cards work best when the event payload carries the actual target, or when the listener is in the guaranteed target set (all friendly / self).

## 6. Design Recommendations

1. **Treat `onMeGotStatusEffect` as a self-referential event.** Because it uses `RaiseSpecific`, cards that listen to it should either:
   - Have a way to give status effects to themselves, or
   - Be in an archetype with guaranteed all-friendly status givers.

2. **Avoid putting high-value payoffs solely behind `onMeGotStatusEffect` with random sources.** A 6.7% combo is not a build-around; it is a lottery.

3. **Consider adding `onAnyCardGotStatusEffect`.** If you want cross-card status combos (like BLACKSMITH triggering another card whenever it buffs anything), a global event with payload would be much more usable than `onMeGotStatusEffect`.

4. **Fill empty Power event space.** `onMeGotPower`, `onAnyCardGotPower`, and `onEnemyCardGotPower` currently have zero listeners. This is either a missed archetype opportunity or dead code that can be removed.

5. **When designing random status givers, ask:** "If a card cares about receiving this status effect, how often will it actually receive it?" If the answer is "only when RNG smiles," either change the targeting or lower the payoff's importance.

## 7. Quick Difficulty Score

A practical score for event reliability:

```
Reliability Score = SourceDensity × ReachMultiplier × PrecisionMultiplier
```

| Raise Type | ReachMultiplier | PrecisionMultiplier |
|------------|-----------------|---------------------|
| `Raise()` | 1.0 | 1.0 (all listeners fire) |
| `RaiseOwner/RaiseOpponent()` | 0.5 | 1.0 (faction half) |
| `RaiseSpecific` deterministic | 1.0 | 1.0 (all friendly, last X planned) |
| `RaiseSpecific` random 1 of N | 1.0 | `1/N` |
| `RaiseSpecific` self | 1.0 | 1.0 for self-listener, 0 otherwise |

Apply this score before finalizing cards that depend on reactive events.

## 8. Data Source

Generated by `tools/scripts/analyze_event_trigger_probability.py` from `docs/CardDesign_GenerationLog.txt`.
