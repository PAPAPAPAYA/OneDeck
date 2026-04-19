# OneDeck Infinite Loop Detection Rules

## Core Game Rules

- Both decks merged into a single `combinedDeckZone` (index 0 = bottom, Count-1 = top).
- One `Start Card` is added to the bottom of the combined deck each round.
- Cards are revealed one by one from top. When revealed, `onMeRevealed` triggers.
- After a card is revealed, it is placed at the bottom of the deck.
- When `Start Card` is revealed, the round ends, deck is shuffled, and a new round begins.
- `Linger` tag: card can trigger effects while in the graveyard (before Start Card).
- `DeathRattle` tag: triggers when the card is buried.

## Infinite Loop Definitions

### Type A: Same-Turn Infinite (Critical)

Two or more cards can loop indefinitely **within the same turn**, preventing the Start Card from ever being reached.

**Mechanism**: Card A's `onMeRevealed` stages Card B to top. Card B's `onMeRevealed` stages Card A to top. This cycles forever because the Start Card gets permanently stuck in the middle of the deck.

**Deck simulation** (2 looping cards + Start Card):
```
Initial: A | B | START
1. Reveal A -> stage B -> A to bottom
   Deck: B | START | A
2. Reveal B -> stage A -> B to bottom
   Deck: A | START | B
3. Reveal A -> stage B -> A to bottom
   Deck: B | START | A
   ...START is permanently stuck at position 2, never reached.
```

### Type B: Resource Infinite (High Risk)

A card or combo can generate unlimited copies of itself or another resource without net consumption.

**Examples**:
- A card that adds itself back to the deck every time it's buried, with no consumption.
- Counter-based self-replication that doesn't consume the counter.

### Type C: State Explosion (Medium Risk)

A numeric state (Power, HP, etc.) grows without bound across turns, breaking game balance even if not an immediate infinite loop.

## High-Risk Effect Patterns

### Pattern 1: StageMyCards(N) + Unconditional Trigger

Any card with `StageMyCards` (stage friendly cards to top) triggered unconditionally on `onMeRevealed` is a **two-card infinite risk**.

**Affected cards** (verify against current design):
- `ARMED_SUMMONER` — stages 1 friendly
- `ALMIGHTY` — stages 1 friendly
- `UNSTABLE_PORTAL` — stages 1 friendly
- `OVERCHARGED_SUMMONER` — stages 1 friendly
- `CURSE_SUMMONER` — stages 1 friendly
- `ADVANCE_PORTAL` — stages 2 friendly
- `BOOSTER` — stages 2 friendly (verify if onMeRevealed or afterShuffle event)
- `DR_MANHATTON` — stages 2 friendly (conditional on Power >= 4)
- `RIFT_SUMMONER` — stages 1 friendly (conditional on Minion Cost)

**Exception**: `RIFT` stages 1 friendly but immediately `ExileSelf`, removing itself from the deck.

**Rule**: If a player has **two or more** copies of any of these cards, they can form a Type A infinite loop.

### Pattern 2: DeathRattle Self-Replication

Cards that add copies of themselves when buried.

**Affected cards**:
- `SLIME` — buried with Counter=2 -> AddSelfToMe

**Risk**: If `CheckCost_Counter` does **not consume** the Counter after checking, every subsequent bury generates a new copy (infinite deck inflation).

### Pattern 3: Linger Event Feedback Loop

Linger cards that respond to events by triggering the same event again.

**Affected cards**:
- `WEAPON_SPIRIT` — when friendly card gains Power, give that card +1 Power. If this "giving" triggers the same event, it chains infinitely (capped by EffectChainManager max depth 9, but still a design flaw).

### Pattern 4: Exponential Power Growth

Cards that multiply their own Power each reveal.

**Affected cards**:
- `UNFINISHED_ROBOT` — doubles its own Power on reveal.
- `POWER_CRAVER` — gains 3x current Power on reveal.

**Risk**: Combined with cards that stage the highest-Power enemy (`THE_FOOL`), this creates exponential growth across turns.

## Checklist for Analysis

When analyzing a card design document or prefab set:

1. **Extract all StageMyCards effects**
   - Record: card name, stage count, trigger condition
   - Flag unconditional `onMeRevealed` StageMyCards as **CRITICAL**
   - Flag conditional StageMyCards as **HIGH** if condition is easily met

2. **Extract all DeathRattle self-replication**
   - Check if the replication condition consumes its resource
   - If not consumed -> Type B infinite

3. **Extract all Linger event listeners**
   - Check if the response action re-triggers the same event
   - If yes -> Type A or C infinite (depending on chain depth)

4. **Extract all self-referential state multiplication**
   - Power doubling/tripling on reveal
   - Check if the card can be repeatedly staged by other cards

5. **Cross-reference Stage cards with Staging enablers**
   - Does the deck contain cards that stage the high-risk card back to top?
   - If yes, check for two-card or three-card loops.

## Output Format

Report findings in this structure:

```
## Infinite Loop Check Report

### Type A: Same-Turn Infinite (Critical)
| Combo | Loop Mechanism | Risk Level |

### Type B: Resource Infinite (High)
| Card/Combo | Mechanism | Risk Level |

### Type C: State Explosion (Medium)
| Card/Combo | Mechanism | Risk Level |

### Design Flaws / Concerns
- List of suspicious interactions that may not be strict infinites but break balance

### Recommendations
- Suggested fixes for each finding
```
