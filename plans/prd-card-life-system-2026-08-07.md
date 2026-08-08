# PRD: Card Life System (Reveals per Round)

## 1. Overview

### 1.1 Background

Design discussion (2026-08-07, Obsidian vault + Notion card DB as source of truth) identified two
frustration sources in the current rules:

1. **Targeting randomness** - bury/stage/power targets are picked uniformly at random from a shuffled
   pool (`UtilityFuncManagerScript.ShuffleList`), so combo chains (bury -> deathrattle, stage from
   grave, power on the right card) fire at coin-flip probability. Player intent cannot be expressed.
   This axis is addressed separately by per-effect targeting predicates (the give-power "filter to
   damage dealers" option already shipped this pattern). **Not part of this PRD.**
2. **Binary reveal economics** - with `StartCardPlacement.AlwaysBottom` the count axis is already
   deterministic (every card reveals exactly once per round). But burying an enemy card is an all-or-
   nothing 0/1 event: the target either reveals this round or it does not. When both sides bury,
   the outcome is a coin flip over whose cards reveal.

This PRD introduces an opt-in **life** stat: "how many times this card may be revealed per round".
When a life > 0 card would enter the grave (below the Start Card), it consumes 1 life and instead
bounces to the queue tail (immediately above the Start Card), so it will be revealed again this
round. Life 0 cards keep the current behavior exactly.

The bounce position (queue tail) is also the natural **setup** slot: a "bury the deepest active
friendly" targeting predicate (future feature) will consume exactly this position, turning the
bounce into a self-sustaining deathrattle engine. The life system provides the target slot for free.

The game is heading to async PVP (recorded enemy decks are test/fallback only), so every rule in
this PRD is symmetric by construction: both sides run the same code path, and per-card life values
apply identically to player and enemy cards.

### 1.2 Goal

- Add an opt-in per-card `life` stat = deterministic reveal count per round (baseline for life 0
  cards unchanged: exactly one reveal per round).
- Turn enemy-bury from a 0/1 binary into a degree for life > 0 cards: burying an enemy creature
  reduces its remaining reveals instead of deleting it from the round.
- Keep the change rollback-safe: all existing cards default to life 0, which reproduces current
  behavior exactly.
- Provide the setup slot (queue tail) that future positional targeting predicates will consume.

### 1.3 Design Rationale

- **Bounce to queue tail, not random**: determinism is the point. The tail slot is unique (no
  tie-breaking), visible on the cascade layout, and preserves the "every card reveals at most once
  per position" pacing. Extra triggers cluster at the round end (the "parade"), which is accepted
  consciously as the round's payoff arc (see Risks).
- **Life resets every round** (ruling): prevents snowballing (attrition never carries over) and
  keeps each round a fresh game. A persistent life would activate permanent-removal/revive design
  space and is explicitly out of scope.
- **Default life 0 for all existing cards** (ruling): zero behavior change until values are assigned
  during balance iteration; every commit stays reversible.
- **Rest still consumes life and still bounces** (ruling): one rule, no exceptions - "every grave
  entry consumes 1 life". Rest only skips the trigger.
- **Fatigue keeps counting total reveals** (ruling): life cards accelerate fatigue. This is an
  accepted balance lever (high-life decks trade round length for fatigue pressure), documented in
  GameRules.md, no code change to the fatigue counters.
- **Symmetry**: async PVP means rules must be expressible identically for both sides.

---

## 2. Scope

### 2.1 In Scope

- `CardScript`: add `lifeMax` (serialized config, default 0) and `currentLife` (runtime).
- Bounce rule applied at the two canonical grave placements:
  - `CombatManager.PutRevealedCardToBottom()` (`Assets/Scripts/Managers/CombatManager.cs:905`).
  - `BuryEffect` placement (`Assets/Scripts/Effects/BuryEffect.cs:335`, `Insert(0, targetCard)`),
    and any other effect that places cards into the grave (ToBottom/ToIndex targeting the grave
    region - via the shared placement seam).
- Life reset at round start (in the round-start path, alongside `beforeRoundStart` at
  `CombatManager.cs:309` / after the shuffle animation completes).
- Card face UI: life pips showing `currentLife` (face-up cards only, same rule as power display).
- Shop card view: show `lifeMax`.
- `docs/GameRules.md`: document the life rules (R1-R13 below) and the fatigue lever.
- This PRD's rules table becomes the authoritative reference.

### 2.2 Out of Scope

- **Per-card life values** - all cards default 0; values are assigned during balance iteration
  (expected: most cards 0-1, a few 2, cap 3; curses and rift tokens stay 0, see R6).
- **Targeting predicates** (tag filter, queue-tail burial, power-based picks, scry) - separate
  feature; the give-power filter option is the existing pattern.
- **Recorded enemy deck revalidation** - enemy decks are test/fallback only (async PVP is the
  target); the symmetric rule set means no special-casing is needed, just per-card values later.
- **Fatigue threshold re-tuning** - fatigue semantics unchanged by design (R11).
- **Shop pricing of life** - balance iteration.
- **Monte Carlo / simulation work** - deferred until values exist.
- CardDesc text changes, new card assets, `TagTooltipDatabaseSO` changes.

---

## 3. Game Rules

- **R1 (Life stat)**: Every card has `lifeMax` (configured, >= 0, default 0; design range 0-3) and
  `currentLife` (runtime). `lifeMax` is part of the card's serialized configuration; `currentLife`
  is reset to `lifeMax` at the start of every round (R4).
- **R2 (Bounce rule)**: Whenever a card with `currentLife > 0` would be placed into the grave
  (destination index 0 region, below the Start Card): decrement `currentLife` by 1 and place the
  card at the queue tail instead - the slot immediately above the Start Card
  (`startCardIndex + 1`). The card will be revealed again this round.
- **R3 (Life 0)**: A card with `currentLife == 0` (or `lifeMax == 0`) is placed into the grave
  normally. Current behavior is unchanged.
- **R4 (Reset)**: At round start (Start Card shuffle completes / before the round's first reveal),
  every card in `combinedDeckZone` has `currentLife = lifeMax`. Neutral/Start Card: `lifeMax` fixed
  at 0.
- **R5 (Rest)**: Rest skips the card's trigger but **does not** skip life consumption or the
  bounce. The card still consumes 1 life and bounces to the queue tail.
- **R6 (Curses and tokens)**: Curse cards (`[curse]`, e.g. JU_ON) and rift tokens (`[次元裂缝]`)
  are hard-coded to `lifeMax = 0` unless an explicit exception is approved. This is a rules
  decision, not a balance decision (a creature curse would multiply enemy curse-stuffing by its
  life).
- **R7 (Symmetric)**: The rules apply identically to both sides. Enemy cards use the same fields
  and the same code path.
- **R8 (Neutral)**: The Start Card is neutral (`isStartCard`), never consumes life, never bounces.
- **R9 (Stage)**: Staging a card from the grave gives it one extra reveal but does **not** restore
  life. When the staged card returns, the normal rule applies (R2/R3).
- **R10 (Exile)**: Exile removes the card from the game; no life interaction. Exile remains the
  only hard removal (its relative value rises as life values spread - tracked in balance).
- **R11 (Fatigue)**: `totalCardsRevealed` counts every reveal, including bounce re-reveals. Life
  cards therefore accelerate fatigue - an accepted lever, not a bug.
- **R12 (Delay exception)**: Delay moves a card one slot toward index 0 and does **not** trigger
  the bounce, even if the shift crosses below the Start Card. A delayed life > 0 card may sit in
  the grave for the rest of the round without consuming life. (Delay is a position shift, not a
  grave placement; this exception preserves Delay's existing function.)
- **R13 (Shuffle window)**: Bounces do not occur during the end-of-round shuffle resolution (Start
  Card revealed). Cards placed into the grave in that window stay there; life reset (R4) follows
  immediately.

---

## 4. Technical Design

### 4.1 Fields (`Assets/Scripts/Card/CardScript.cs`)

```
[Header("Life")]
[Tooltip("How many times this card may be revealed per round. 0 = current behavior (once).")]
public int lifeMax = 0;        // serialized config, never mutated at runtime
public int currentLife = 0;    // runtime, reset to lifeMax each round
```

Placement: near `myStatusEffects` / `myTags` in the existing "Status Effects" region of CardScript.

### 4.2 Placement Seam

Introduce one helper that owns the grave-entry decision, so every grave placement funnels through
the same rule:

```
// Returns the destination index in combinedDeckZone.
// If the card would go below the Start Card and has life left:
//   currentLife--, destination = startCardIndex + 1 (queue tail)
// Else: destination = 0 (grave, current behavior)
int ResolveGravePlacement(CardScript card, List<GameObject> combinedDeck)
```

Callers:
- `CombatManager.PutRevealedCardToBottom()` - replace `combinedDeckZone.Insert(0, cardToBottom)`
  with the seam result; visuals call `MoveCardToIndex` instead of `MoveRevealedCardToBottom` when
  the destination is the queue tail (see 4.6).
- `BuryEffect` (line 335 `_combinedDeck.Insert(0, targetCard)`) - via the same seam. The captured
  animation request becomes `MoveToIndexBatch` (tail) instead of `MoveToBottomBatch` when bouncing.
- Any other placement into the grave region (ToBottom/ToIndex effects) - route through the seam.

### 4.3 Bounce Insertion

- `startCardIndex = combinedDeckZone.IndexOf(startCardGo)`; if `-1` (Start Card in reveal zone /
  shuffle window), fall back to the grave (R13).
- Insert at `startCardIndex + 1`. Multiple bounced cards stack LIFO: the last card bounced is the
  topmost of the tail stack, i.e. revealed first. (Documented; the cascade layout renders the tail
  stack in insertion order.)
- Works identically in Gaussian mode: the tail is still "immediately above the Start Card", keeping
  the card in the active region of the round.

### 4.4 Round-Start Reset

Reset `currentLife = lifeMax` for every card in `combinedDeckZone` at the round-start path - after
the Start Card shuffle resolves and before/with the `beforeRoundStart` raise
(`CombatManager.cs:309`). Reveal-zone card (if any) is excluded by R13 ordering.

### 4.5 Reactive Ordering (bury path)

When a life > 0 card is buried:

1. The bury effect performs its normal placement at index 0 (the card IS in the grave during
   resolution - `onMeBuried` / `onAnyCardBuried` / `onFriendlyCardBuried` fire as today).
2. Reactive effects resolve (deathrattle, linger, chain reactions).
3. **After** the reactive resolution, if the card is still in the grave (not staged, exiled, or
   destroyed by reactions): apply R2 - consume 1 life, move to the queue tail.

This ordering keeps `CheckCost_IndexBeforeStartCard` semantics correct during resolution (the
buried card is still "in the grave" while its own events fire) and lets reactions override the
bounce (a staged deathrattle card does not also bounce).

### 4.6 Animation

- Reveal-return bounce: `visuals.MoveCardToIndex(card, tailIndex, ...)` (arc to the queue tail),
  reusing the existing movement pipeline. No new `AnimationRequestType` is required; `MoveToIndex`
  already exists and `ApplyAnimationResult` reorders `physicalCardsInDeck` per request.
- Bury-driven bounce: `BuryEffect` captures `MoveToIndexBatch` (tail) instead of
  `MoveToBottomBatch` when the seam resolved a bounce. All batch/parallel semantics unchanged.
- The tail slot is a normal cascade index; `DeckPositionCalculator.CalculatePositionAtIndex`
  renders it without layout changes.

### 4.7 UI

- Card face: life pips (e.g. small hearts) next to the power display, showing `currentLife`, only
  on face-up cards (`CardPhysObjScript.SetFaceUp` path). Zero pips = nothing shown (life 0 cards
  render exactly as today).
- Shop card view (`ShopCardView`): show `lifeMax` (static, no pips needed).

### 4.8 Edge Cases

| Case | Behavior |
|------|----------|
| Life 0 card | R3 - unchanged, no UI |
| Card bounced twice in a row | Each bounce consumes 1 life; life 0 on second bounce -> stays in grave |
| Bury while card has 1 life left | Buries (events fire), consumes the last life, bounces once more; the NEXT grave entry stays |
| Reaction stages/exiles the buried card | Bounce skipped (4.5 step 3) |
| Start Card in reveal zone during a bounce | R13 - no bounce, grave placement |
| Gaussian placement mode | Tail = above the Start Card, works unchanged |
| Enemy card bounce | Same code path (R7) |
| Rest + bounce | R5 - skip trigger, still consume + bounce |
| Deck at exactly 1 card + Start Card | Tail slot exists (index 1); no special case |

---

## 5. Interactions

| System | Impact | Note |
|--------|--------|------|
| Power | Life > 0 damage cards deliver Power on every hit -> Power value scales with life | Power economy re-tune expected; give-power targeting predicates should prefer damage-dealers with life (future) |
| DeathRattle | Burying a life > 0 deathrattle card = repeated engines (life caps per-round deathrattle count) | Low-life deathrattle cards (1-2) are the intended design zone |
| Grave-scaling (人间大炮 / 冥界邀请 / 被诅咒的骷髅) | Fuel shrinks proportionally to life density (life > 0 cards stay in rotation, not the grave) | Accepted, mild at low life density; re-check if life spreads |
| Stage | Stage from grave = manual extra reveal; does not restore life (R9) | Stage and life compose on the same axis (reveals per round) |
| Bury (friendly) | Bury a life > 0 friendly = spend 1 life for a deathrattle trigger; miss = life wasted | Targeting predicates (out of scope) are the mitigation |
| Bury (hostile) | The flagship fix: enemy life > 0 card loses 1 reveal instead of being deleted | Gradient, not binary |
| Exile | Only hard removal; relative value rises | Track in balance |
| Fatigue | Accelerates with life density (R11) | Accepted lever |
| Rest | Skip trigger, still spend life (R5) | Documented exception-free rule |

---

## 6. Balance & Playtest Validation

After values are assigned to a first batch of cards (suggested: 5-8 damage/deathrattle creatures,
life 1-3), playtest 3-5 combats and record two numbers:

1. **Per-round total reveal count** - must stay close to deck size + modest overhead (target:
   <= deck size x 1.5). Blowout = life values too high.
2. **Parade share** - of the last 5 reveals before the Start Card, how many are bounce re-reveals.
   If > 3, the round end is scripted past the comfort point.

Also watch: friendly-bury miss frequency (predicate PRD must land before life spreads), grave-scale
card win rate, curse-card reveal damage.

## 7. Risks & Open Questions

- **Round-end parade**: extra triggers cluster at the tail. Accepted consciously as the round's
  payoff arc; the fallback would be a non-deterministic bounce position, which defeats the design
  and is not recommended.
- **"Scripted death" window**: with no reaction interface, a player can compute a tail-parade death
  mid-round with no counterplay. Mitigation lives in deckbuilding (shield/heal/exile) and in
  keeping life values low - not in the rules.
- **Grave fuel**: documented, proportional to life density (see Interactions).
- **Power economy**: Power x life multiplicative value; give-power predicates need a life dimension
  (future).
- **UI burden**: one more per-card mutable stat on face-up cards; acceptable at life range 0-3.

## 8. Implementation Checklist (each step independently revertible)

1. CardScript: `lifeMax` / `currentLife` fields (R1).
2. Placement seam `ResolveGravePlacement`; wire `PutRevealedCardToBottom` and `BuryEffect`.
3. Round-start reset (R4) in the round-start path.
4. Bury reactive ordering (4.5) - verify `onMeBuried` chains still resolve with the card at index 0.
5. Card face pips + shop `lifeMax` display (4.7).
6. `docs/GameRules.md` rules section update (R1-R13) + fatigue note.
7. Assign life values to the first 5-8 cards; playtest (section 6).
8. Regression: life 0 deck reproduces current behavior exactly (diff against recorded enemy decks).

## 9. References

- `Assets/Scripts/Managers/CombatManager.cs` - `PutRevealedCardToBottom()` (905), round-start path (309), `totalCardsRevealed` / fatigue.
- `Assets/Scripts/Effects/BuryEffect.cs` - `_combinedDeck.Insert(0, targetCard)` (335).
- `Assets/Scripts/Card/CardScript.cs` - field regions (Status Effects / Tags).
- `Assets/Scripts/UXPrototype/CombatUXManager.cs` - `MoveCardToIndex`, cascade layout, face-up UI.
- `Assets/SORefs/ShopRefs/DeckSize/PlayerDeckSizeRef.asset` - deck size 12 (design context).
- `docs/GameRules.md` - Damage Multiplication section (corrected 2026-08-07).
