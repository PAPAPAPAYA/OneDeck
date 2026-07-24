# Plan: Result Screen Per-Card Combat Stats

Date: 2026-07-23
Status: Approved design (decisions locked 2026-07-23, revised after plan review same day; no code written yet)

## Goal

Replace the current Result-phase text blob with a per-card statistics view of the
combat that just finished:

- Damage dealt to the opponent per card
- Damage dealt to self per card
- Trigger count per card
- Power stacks granted per card (PowerGiven)
- Power stacks received per card (PowerReceived)

The stat set must be cheap to extend (new stats will keep being added).

## Locked decisions (2026-07-23, revised)

- Display: **Option A — scrollable per-card row list** (see Display section).
- **Rows are keyed by `(cardTypeID, faction)`.** The same card type on the
  player side and the enemy side produces two separate rows. (Plain `cardTypeID`
  aggregation was rejected: it merges both factions into one row and scrambles
  damage attribution.)
- Damage columns use **card-perspective** naming: `DamageDealtToOpponent` and
  `DamageDealtToSelf`. "Opponent" always means the opponent of the card's owner,
  so the same column works for both factions.
- Damage records the **raw pre-shield, pre-clamp amount** (what the card
  output), hooked at `CheckDmgTargets_*`. HP-only accounting was rejected.
- `PowerGiven` counts **stacks (amount)**, not number of grants. Power moved by
  `TransferStatusEffectEffect` counts as given by the transferring card.
- `PowerReceived` counts stacks received by the card; the receiver side of a
  transfer counts automatically (it goes through the same hook).
- **No cross-combat persistence.** Session stats are display-only; the store is
  reset at every combat start.

## Current State (verified)

- Result UI is a single `TextMeshProUGUI resultInfoDisplay` built by
  `PhaseManager.ShowResult()` (`Assets/Scripts/Managers/PhaseManager.cs:183-219`).
  No list/prefab infrastructure exists.
- **`ShowResult()` runs every frame** during the Result phase
  (`PhaseManager.cs:170-171`, called from the Update loop). The stats panel
  must be built once on phase entry (or behind a dirty flag), never per frame.
- No per-card damage/trigger counting exists anywhere in combat.
  `DeckTester` only accumulates deck-level damage in auto-test mode.
- `CardWinRateTracker` (`Assets/Scripts/Managers/WriteRead/CardWinRateTracker.cs`)
  is the established pattern for card stats: singleton, keyed by `cardTypeID`,
  `[Serializable]` data class, JSON to `persistentDataPath`.
- Combat end carries no data payload: `combatFinished` BoolSO flips, card
  GameObjects are destroyed in `CombatManager.ExitCombat()`. Per-card stats must
  live in a plain C# store that survives card destruction.
- **`ShouldSkipEffectProcessing()` does NOT guard `InvokeEffectEvent()`.** It is
  only used in effect *target filtering*. The Start Card triggers its shuffle
  effect through `container?.InvokeEffectEvent()` (`CombatManager.cs:756-757`),
  so a trigger hook in `InvokeEffectEvent()` WILL see neutral cards. Neutral
  exclusion must be explicit in the tracker.

## Design

### 1. Data layer — `CombatPerCardStatsTracker` (new)

New singleton in `Assets/Scripts/Managers/WriteRead/`, modeled 1:1 on
`CardWinRateTracker`:

	CombatPerCardStatsTracker.Me
		Dictionary<string, PerCardStatRecord> records   // key: cardTypeID + "|" + faction

`PerCardStatRecord` holds `displayName`, `faction`, and
`Dictionary<CombatStatType, float> values`.

Faction is a small enum `{ Player, Enemy }`, resolved by
`cardScript.myStatusRef == CombatManager.Me.ownerPlayerStatusRef`.

**Neutral guard:** the central `Add()` returns early when
`card.IsNeutralCard` is true. This is the single exclusion point — do not rely
on `ShouldSkipEffectProcessing()` (see Current State).

Extensibility core — a static stat registry:

	enum CombatStatType { DamageDealtToOpponent, DamageDealtToSelf, TriggerCount, PowerGiven, PowerReceived, ... }

	static class CombatStatRegistry
		List<StatDef> { type, columnHeader, displayColor, columnSortPriority }

Adding a new stat later = one enum entry + one registry entry + one `Record*()`
call at the hook site. Data, report string, and UI columns all derive from the
registry, so nothing else needs to change. Note: registry `columnSortPriority`
controls **column** order only; the **row** sort key is defined separately
(see Display).

Public API (thin wrappers over a generic
`Add(CardScript card, CombatStatType stat, float amount)` that resolves the
`(cardTypeID, faction)` key and applies the neutral guard):

- `RecordDamageToOpponent(CardScript source, float amount)`
- `RecordDamageToSelf(CardScript source, float amount)`
- `RecordTrigger(CardScript source)`
- `RecordPowerGiven(CardScript giver, int amount)`
- `RecordPowerReceived(CardScript receiver, int amount)`
- `BeginSession()` / `GetSessionRows()`

`cardTypeID` resolution mirrors `CardWinRateTracker.GetCardTypeID`
(`CardWinRateTracker.cs:176-187`): `cardTypeID`, fallback to GameObject name.
Multiple copies of the same `(cardTypeID, faction)` aggregate into one row
(consistent with existing trackers).

### 2. Session lifecycle

- `BeginSession()` at combat start, from `CombatManager.GatherDecks()`
  (`CombatManager.cs:294-295`, same seam that already snapshots decks into
  `CardWinRateTracker`). Clears the dictionary.
- Records accumulate during combat.
- Result phase reads the store; next combat's `BeginSession()` overwrites it.
  The store is plain C# data, so card destruction in `ExitCombat()` is harmless.
- Neutral/start cards (`isStartCard`) are excluded by the explicit
  `IsNeutralCard` guard in `Add()` — NOT by `ShouldSkipEffectProcessing()`.

### 3. Collection hooks (one line each)

| Stat | Hook site | Notes |
|------|-----------|-------|
| TriggerCount | `CostNEffectContainer.InvokeEffectEvent()` (`Assets/Scripts/Card/CostNEffectContainer.cs:112-117`), **inside the `EffectCanBeInvoked(effectString)` true branch**, alongside `effectEvent?.Invoke()` | Placing it inside the branch is mandatory: the loop-guard early return (lines 104-108) and the `else` branch (118-121) must NOT count. Counts every successful container invocation, including reactive chains. Semantic: per-container, not per-card — a card with N containers that all pass cost counts N triggers per event (accepted, documented). |
| DamageDealtToOpponent | `HPAlterEffect.CheckDmgTargets_DealingDmgToOpponent(totalDmg)` (`Assets/Scripts/Effects/HPAlterEffect.cs:589`) | All damage-to-opponent variants funnel into this method; `myCardScript` is in scope. Records raw `totalDmg` (locked decision). Card-perspective naming keeps this correct for both factions: an enemy card damaging the player is that card's `DamageDealtToOpponent`, shown on the enemy row. |
| DamageDealtToSelf | `HPAlterEffect.CheckDmgTargets_DealingDmgToSelf(totalDmg)` (`Assets/Scripts/Effects/HPAlterEffect.cs:611`) | Same funnel, self-damage path. |
| PowerGiven + PowerReceived | `EffectScript.ApplyStatusEffectCore(...)` Power branch (`Assets/Scripts/Effects/EffectScript.cs:85-99`) | Single choke point for all status application. One hook produces both stats: `RecordPowerGiven(myCardScript, amount)` attributes to the giver (the effect's card), `RecordPowerReceived(targetCardScript, amount)` to the receiver. |

**Attribution rules (accepted, documented):**

- `TransferStatusEffectEffect` goes through `ApplyStatusEffectCore`, so the
  transferring card gets `PowerGiven` and the receiver gets `PowerReceived`
  (locked decision: transfers count).
- Status resolvers execute on the carrier card: `PowerReactionEffect` and
  `isStatusEffectDamage = true` resolver damage (`HPAlterEffect.cs:140-145`)
  resolve `myCardScript` to the card *carrying* the status, so their
  Power/damage attributes to the carrier, not the original applier.

### 4. Display — scrollable per-card row list (locked: Option A)

- New `ResultStatsPanel` under the existing Result canvas: a `ScrollRect` with a
  vertical layout group, fed by a `ResultStatRow` prefab
  (card name + faction label + one TMP column per stat).
- Header row and row columns are generated from `CombatStatRegistry`, so a new
  stat automatically becomes a new column — this is what "info will keep
  expanding" demands.
- **Build once, not per frame:** the panel is populated on entering the Result
  phase (or on the first `ShowResult()` call behind a dirty flag).
  `ShowResult()` keeps building the WIN/LOSE/wins/hearts header text as-is;
  only the body below it is replaced.
- Rows show faction (label text or tint via the existing
  `ownerCardColor`/`opponentCardColor` palette assets).
- Row sorting: by `DamageDealtToOpponent` desc, then faction (Player first).
  This row sort key is defined in `GetSessionRows()` and is independent of the
  registry's `columnSortPriority` (which orders columns).

### 5. Persistence

None. Session stats are display-only; `BeginSession()` wipes the store at every
combat start. (Locked decision 2026-07-23.)

## Edge cases

- Empty `cardTypeID` → GameObject-name fallback (same as `CardWinRateTracker`).
- Same `cardTypeID` on both factions → two rows (locked decision).
- Known `cardTypeID` collisions exist (`Assets/duplicate_cardTypeID_report.json`);
  same-faction aggregation by ID inherits them, acceptable and consistent.
- Reactive/repeated triggers all count (e.g. a card triggering 5 times shows 5).
- Multi-container cards count one trigger per container invocation (accepted).
- Resolver Power/damage attributes to the carrier card (accepted rule above).
- Self-damage is a first-class stat (`DamageDealtToSelf`), displayed as its own
  column — locked decision 2026-07-23.
- Neutral/start cards reach `InvokeEffectEvent()` (verified
  `CombatManager.cs:756-757`) and are filtered only by the tracker's
  `IsNeutralCard` guard.
- `takeUpSpace == false` cards never instantiate in combat, so they never appear.
- Cards that never triggered/produced any stat have no record and no row.

## Implementation steps

1. `Assets/Scripts/Managers/WriteRead/CombatPerCardStatsTracker.cs` — singleton,
   `CombatStatType` enum, `CombatStatRegistry`, `PerCardStatRecord` (with
   faction), `(cardTypeID, faction)` key resolution, `IsNeutralCard` guard,
   record API, `BeginSession()`, `GetSessionRows()`.
2. EditMode tests for the tracker (project has `Assets/Scripts/Editor/Tests/`
   infrastructure): aggregation, faction split, neutral guard, PowerGiven/
   PowerReceived amount semantics.
3. Hook `CostNEffectContainer.InvokeEffectEvent()` (trigger), inside the
   `EffectCanBeInvoked` true branch.
4. Hook `HPAlterEffect.CheckDmgTargets_*` (damage, raw amount).
5. Hook `EffectScript.ApplyStatusEffectCore` Power branch (PowerGiven +
   PowerReceived, with `amount`).
6. Call `BeginSession()` from `CombatManager.GatherDecks()`.
7. UI: `ResultStatsPanel` + `ResultStatRow` prefab, wired into the Result canvas;
   panel populated once on Result phase entry; `PhaseManager.ShowResult()`
   keeps header text only.
8. Play Mode verification: run a combat with known cards (e.g. a damage card,
   a Power-giving card), confirm counts match expectations on the Result
   screen, including a same-`cardTypeID`-on-both-sides case.
9. Update `AGENTS.md` (managers list + architecture section) after implementation.

## Out of scope

- Cross-combat accumulation / persistence (explicitly rejected 2026-07-23).
- Changes to `card_winrate.json` / shop stats.
