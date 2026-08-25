# Attack Resolver Recursion — Fix Design

Date: 2026-08-25
Revised: 2026-08-25 (review pass 1 — self-exclusion moved from CollectCards to the
aggregating sites, test-2 expectation corrected, stale line-ending note dropped)
Status: Design only — not implemented (user to approve before coding)
Scope: `AttackResolverSource` (phase-3 常态结算 resolver) infinite-recursion crash. Card
logic/effects, display pipeline, and other phase-3 mechanisms are untouched.
Supersedes: nothing. Follow-up to the phase-3 implementation review (attack-attribute 期 3).

## 1. Problem

### 1.1 Self-inclusion stack overflow (critical)

`CardScript.GetAttack()` (CardScript.cs:90-93) calls the injected resolver with no guard:

```csharp
public int GetAttack()
{
	if (_attackResolver != null) return _attackResolver();
	return printedAttack + attackGrowth + attackModThisRound;
}
```

`AttackResolverSource.CollectCards` (AttackResolverSource.cs:137-160) gathers every card of the
requested faction from `combinedDeckZone` + `revealZone` and has **no self-exclusion** —
`CardMatches` (AttackResolverSource.cs:162-176) checks only faction / cardTypeID /
ShouldSkipEffectProcessing. `SumCardsWithAttack` (AttackResolverSource.cs:118-126) then calls
`GetAttack()` on each gathered card, including the carrier itself.

Trace for ALL_FOR_ONE (term `FriendlyCardTotal`) sitting in the deck:

1. Something asks the card's attack: `GetAttack()` → `_attackResolver()` → `Resolve()`.
2. `Resolve()` → `SumCardsWithAttack(true, null)` → `CollectCards` returns every friendly deck
   card — including ALL_FOR_ONE itself.
3. `total += card.GetAttack()` on the carrier → `_attackResolver()` again → infinite recursion.
4. `StackOverflowException` — not catchable in Unity; the editor process crashes.

Trigger surfaces (any one of them crashes, so this is not a corner case):

- `ValueTrackerManager.UpdateTotalPowerCountInDeck` (ValueTrackerManager.cs:253-273) iterates
  every deck card + the reveal card and calls `GetAttack()` on each. It runs on **every effect
  invocation** via `UpdateAllTrackers()` (ValueTrackerManager.cs:42, called from
  CostNEffectContainer.cs:96) and the ref is wired in GameScene (GameScene.unity:7107).
- The carrier's own reveal: `RefreshAttackDisplay` → `GetAttackForDisplay` → `GetAttack()`
  (reveal-zone branch of `CollectCards` includes the carrier).
- The carrier's own `Attack()` / `AttackTimes()` gate check (`GetAttack() <= 0`).
- Predicates: `CardScript.FindCardWithMaxAttack/MinAttack` and the filters of
  `GiveFriendlyCardWithMinAttack` / `ConsumeEnemyCardWithMaxAttack` call `GetAttack()` on
  candidates — a resolver card in the pool recurses through them too.

Affected cards today: ALL_FOR_ONE, FLESH_COMBINATION, ALMIGHTY (the three prefabs carrying
`AttackResolverSource`). Future resolver consumers (战争英雄 WAR_HERO, 镜面诅咒 V5_CURSE_MIRROR,
咒蚀之眠 V5_CURSE_SLEEP) inherit the same failure.

### 1.2 Cross-card cycle (general case, survives a naive fix)

Two `FriendlyCardTotal` carriers on the same side (ALL_FOR_ONE is Uncommon; a 12-card deck can
hold two): A's resolver sums B's `GetAttack()`, B's resolver sums A's `GetAttack()` — an
unbounded A↔B loop. Self-exclusion alone does not fix this; any resolver pair that reads each
other's dynamic attack forms a cycle (also possible across factions via `EnemyNegativeTotal` /
`EnemyNegativeHighest` when the filtered cards carry resolvers).

### 1.3 Why the current tests missed it

All 9 `AttackResolverSourceTests` create the carrier with `CreateCard(...)` and **never add it
to `combinedDeckZone`**, so `CollectCards` never sees the carrier and the self-inclusion path is
untested. The fixture also does not exercise `UpdateAllTrackers` with a resolver card in the
deck.

### 1.4 Which terms are safe

Only terms that call `GetAttack()` on gathered cards can recurse:

- `FriendlyCardTotal` (SumCardsWithAttack) — unsafe (self-inclusion + cycles).
- `EnemyNegativeTotal` (SumCardsWithAttack) / `EnemyNegativeHighest` (HighestAttack,
  AttackResolverSource.cs:215-224) — unsafe when a filtered enemy card carries a resolver.
- Count terms (`FriendlyCardCount`, `GraveyardFriendlyCount`, `FriendlyRiftCount`) only count
  cards and never call `GetAttack()` — no recursion risk. Their semantics stay unchanged
  (including the open question whether a buried carrier counts itself in the grave term —
  it terminates, so it is a design question, not a crash).

## 2. Design goals

1. Termination guarantee for any resolver graph — every cycle must be cut.
2. Nested dynamic-in-dynamic must keep working: WAR_HERO summing friendly attack should see
   ALMIGHTY's live dynamic value, not a base-value fallback.
3. Minimal blast radius: no change to count terms, no change to resolver evaluation order.
4. Semantics of the cut are explicit and documented.

## 3. Options considered

- **A. Self-exclusion only** — skip the carrier at the aggregation points (`SumCardsWithAttack` /
  `HighestAttack`). Fixes 1.1 cheaply but leaves 1.2 (cross-card cycles) and does not protect
  future resolver consumers.
- **B. Reentrancy guard in `CardScript.GetAttack()`** — a per-card flag; a second entry into the
  same card while its own resolver is on the stack returns the base attack
  (`printedAttack + attackGrowth + attackModThisRound`) instead of recursing. Fixes both 1.1 and
  1.2 for **all** resolver consumers, present and future. Nested dynamic-in-dynamic is preserved
  because a *different* card's `GetAttack()` still evaluates normally.
- **C. Aggregate base attacks (non-resolver)** — the sum uses a "printed + growth + round"
  accessor for other cards. Terminates, but loses dynamic-in-dynamic (ALMIGHTY's live value
  would not flow into WAR_HERO's sum) and silently changes card-face behavior — rejected.

**Recommended: B + A** — the guard is the general fix; self-exclusion additionally makes the
common case semantically clean (a card's "sum of friendly attack" reads other friendly cards,
not itself).

## 4. Chosen design

### 4.1 Reentrancy guard — `CardScript.GetAttack()`

```csharp
[System.NonSerialized]
private bool _resolvingAttack;

public int GetAttack()
{
	if (_attackResolver == null) return printedAttack + attackGrowth + attackModThisRound;
	if (_resolvingAttack) return printedAttack + attackGrowth + attackModThisRound; // cycle cut
	_resolvingAttack = true;
	try { return _attackResolver(); }
	finally { _resolvingAttack = false; }
}
```

Why a per-instance bool is sufficient: any cycle must revisit its starting card, and the second
entry into that card happens while its own resolver is still on the stack — which is exactly the
condition the flag detects. Legitimate nesting (A reads B reads C, all distinct instances) never
sets the flag on a re-entered card. The `finally` guarantees the flag clears even if a resolver
throws, so a one-off failure cannot permanently disable dynamic attack.

The cut value is the base attack (no resolver call), so the guard itself cannot recurse.

### 4.2 Self-exclusion — at the aggregating sites, NOT in `CollectCards()`

Skip the carrier only where `GetAttack()` is actually called on gathered cards — i.e. in
`SumCardsWithAttack` and `HighestAttack`:

```csharp
private int SumCardsWithAttack(bool myCardFaction, string typeID)
{
	int total = 0;
	foreach (var card in CollectCards(myCardFaction, typeID))
	{
		if (card == _cardScript) continue; // a "sum of friendly attack" reads OTHER cards
		total += card.GetAttack();
	}
	return total;
}
```

(`HighestAttack` gets the same `continue`.) Both deck and reveal-zone self-inclusion flow through
these loops, so one check covers every collection path — `CollectCards` itself stays untouched.

Why not `CollectCards`: the count terms route through it (`CountCards` → `CollectCards`), so
excluding the carrier there would also drop it from `FriendlyCardCount` / `FriendlyRiftCount` —
FLESH_COMBINATION would silently lose 1 attack (it currently counts itself). Only the
`GetAttack()`-calling aggregators exclude self; count terms keep their exact current semantics.

For `EnemyNegativeTotal` / `EnemyNegativeHighest` the carrier is friendly, so the exclusion is a
no-op — those terms rely on the 4.1 guard for enemy-resolver-card cycles.

### 4.3 Semantics contract (documented in code comments)

- Resolver terms read other cards' **current** attack; dynamic values are included and each card
  is evaluated once per query.
- Sum/highest terms never include the carrier itself — the exclusion lives only at the
  `GetAttack()`-calling aggregating sites (4.2), not in card collection.
- Re-entry into the same card's `GetAttack()` returns its **base** attack — the cycle is cut at
  the node where the loop closes.
- Count terms are untouched and keep counting the carrier when it is a valid member
  (FLESH_COMBINATION still counts itself in 友方卡牌数量; a buried ALMIGHTY still counts itself
  in 墓地友方卡数 — both terminate, no `GetAttack()` involved).

## 5. Test plan

New cases in `AttackResolverSourceTests` (fixture already supports deck placement):

1. `FriendlyCardTotal_CarrierInDeck_ExcludesSelf` — carrier (printedAttack 2) plus two friendly
   cards (2, 3) in the deck; assert `GetAttack() == 5` (not 7) and no overflow. Guards 1.1.
2. `FriendlyCardTotal_TwoCarriersSameSide_NoRecursion` — two `FriendlyCardTotal` carriers A and B
   (printedAttack 0) plus friendlies C(2) and D(3) in the deck; `GetAttack()` on either carrier
   terminates. Expected value is the *nested* result, not the base: A sums B+C+D, and B's own
   evaluation cuts the re-entered A to base 0, so B resolves to 5 and `A.GetAttack() == 10`
   (symmetrically B == 10 when queried first). Do NOT assert 5 — A reads B's resolved value,
   not B's base. Guards 1.2.
3. `Resolver_GuardResetsAfterEvaluation` — call `GetAttack()` twice sequentially on the same
   carrier; both calls return the same value (flag cleared between calls).
4. `UpdateAllTrackers_WithResolverCardInDeck_NoOverflow` — wire `totalPowerCountInDeckRef` in
   the fixture, put a resolver carrier in the deck, call `ValueTrackerManager.UpdateAllTrackers()`;
   assert no exception and the aggregate equals the guard-cut sum. Guards the every-effect path.
5. `EnemyNegativeTotal_EnemyCardCarriesResolver_NoRecursion` (optional) — cross-faction cycle
   through the enemy term.

Existing tests keep passing unchanged: no current test places the carrier in the deck, and the
guard is transparent when no cycle exists. EditMode suite must be run with the project unlocked
(close the open Editor instance first).

## 6. Regression risk and verification

- Behavior change to check: `FriendlyCardTotal` no longer includes the carrier's own attack.
  ALL_FOR_ONE (printedAttack 0) resolves identically; FLESH_COMBINATION is count-based and
  untouched (still counts itself); ALMIGHTY's terms (grave/rift counts + enemy negative sum)
  never read itself. All three prefabs behave identically except that the crash disappears.
- Play-mode spot checks: deck containing ALL_FOR_ONE triggers any other card's effect; a deck
  with two ALL_FOR_ONE copies; ALMIGHTY revealed (display + own attack action); ALMIGHTY buried
  (grave count term still counts itself — confirm the intended number with the design doc).

## 7. Implementation steps

1. `CardScript.cs` — add `_resolvingAttack` + guarded `GetAttack()` (4.1), update the field
   region comment.
2. `AttackResolverSource.cs` — self-exclusion at the two aggregating sites
   (`SumCardsWithAttack` / `HighestAttack`) (4.2) + semantics comment (4.3).
3. `AttackResolverSourceTests.cs` — add cases 1-4 (and 5 if cheap); keep existing tests.
4. Run the full EditMode suite (project unlocked) — expect the phase-3 suite green plus the new
   cases.
5. Optional: add a RegressionChecklist row only if the display path changes behavior; the guard
   itself is logic-only.
