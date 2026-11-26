# Plan: Draw accounting - natural + forced (L0 detection), server reporting and admin display (2026-11-26, rev. 3)

- **Date**: 2026-11-26 (rev. 3 same day: reviewer fixes; rev. 2: user rulings; rev. 1: forced draws only)
- **Status**: IMPLEMENTED 2026-11-26 (user ruling 「修改代码」; Steps 0-5 + section 5 tests all landed, see §10). D1/D2 resolved and shipped, D3 deferred, D4 manual-toggle confirmed (no code touched `Assets/Resources/ServerConfig.asset`). Server `npm test` 27/27 (incl. the new `tests/draws.test.js` 8 cases); Unity EditMode full suite 843 run / 842 passed / 0 failed / 1 pre-existing skip; not committed, Play Mode not run (not requested).
- **Request (user, 2026-11-25)**: forced draws are intermittently missing from the server - provide a plan to detect and record them, and show them in the admin dashboard.
- **Revision (user ruling, 2026-11-26)**: natural draws (both HP <= 0) must be counted too, and every draw must be recorded and displayed split by kind (`natural` / `forced`). D2 decided: a draw no longer credits a ghost with a defense win; the ghost deck accumulates per-kind draw counters instead. D1 decided: loss point 1 is fixed (the L0 conclusion always settles; combats that run past round 60 end as forced draws there). D3 deferred: a dedicated trip event waits until quit-game handling is added.
- **Audit (2026-11-26)**: glm-reviewer verdict FIX-FIRST (no P0; 3 P1, 6 P2, 5 P3). All findings are incorporated in rev. 3; the P1 claims were re-verified against the code by the main session (server switch state, latch lifetime, EditMode reachability).
- **Evidence basis**: diagnosis by code tracing only. Not reproduced at runtime; the production database was not queried. Every line number below was re-verified against `main` at `fdf83cb0` (2026-11-26). Section 8 lists the zero-code checks that can confirm each loss point.
- **Earlier decisions this plan builds on**: `plans/plan-infinity-detection-2026-09-17.md` section 9 - an L0 hard-cap trip is judged a DRAW (replaces the section 7.7 HP-ratio placeholder); section 7 item 6 - the forced conclusion is fully automatic, no player confirmation; section 20.5 (2026-09-19) - forced-draw reporting keeps the status quo (`won=false`) until draw semantics are unified. This plan is that unification, now covering natural draws as well.

## 1. Goal / non-goals

Goal:
1. Every L0 forced conclusion actually ends the combat (today most are swallowed - section 2.2).
2. Every settled draw reaches the server with its kind: `natural` (both HP <= 0) or `forced` (L0 cap, with reason `rounds` / `totalReveals`), and is no longer dropped for payload size. Accepted gaps: a player who quits and never relaunches (D3 deferred) and match reports for no-ghost / no-identity cases (unchanged).
3. Natural draws are counted end-to-end. Today they are rendered as `lost` and are absent from card win-rate stats entirely (section 2.3).
4. The admin dashboard shows draws as draws and distinguishes natural from forced: run page, overview counters, card win-rate table (per-kind draw counts), ghost defense table (per-kind draw counters).
5. A run record is never silently dropped because of its payload size.

Non-goals:
- Changing the player-facing handling of natural draws (result text `DRAW`, no heart/win bookkeeping - already correct).
- Reclassifying historical rows: pre-plan rows carry no draw information and stay as they are (section 6).
- The L1 `CombatArrangementCycleDetector` / loop-report evidence flow.
- The per-round reveal cap (`maxRevealsPerRound = 100`) - it only skips effects and is not a conclusion.
- `DeckTester` win/loss counters and per-session damage averages - they keep their current treatment. One exception: draws must advance the automation session counter (section 3 Step 0), otherwise an all-draw `autoSpace` batch never terminates.

## 2. Current state - facts verified by code reading

### 2.0 Precondition - the server master switch is OFF at HEAD

- `Assets/Resources/ServerConfig.asset:15` reads `enabled: 0` (commit `f1e80653`, 2026-09-21, "disable server integration"; no later commit re-enabled it). `UploadOutbox.IsUploadEnabled` returns false for every kind when `!config.enabled` (`Assets/Scripts/Net/UploadOutbox.cs:164-169`).
- `BuildConfigValidator.CheckServerConfigAsset` (`Assets/Scripts/Editor/BuildConfigValidator.cs:156-168`) validates `environment`, `markAsTest` and `opponentsIncludeSelf`, but not `enabled`.
- Consequence: a build made from HEAD uploads nothing, so "reports are missing" is reproducible for every kind, not only draws. The switch is intentional and manual (user ruling 2026-11-26): it is turned off during Play-mode testing so test traffic never reaches the server, and release builds flip it by hand. No build-gate check is added (D4 resolved); the implementation and rollout only need to confirm `enabled = 1` for the build under test (section 8). This is a precondition, not a root-cause claim.

### 2.1 The chain today

1. `CombatBudgetGuard.NotifyReveal` (`Assets/Scripts/Managers/CombatBudgetGuard.cs:69`) runs on every reveal from `CombatManager.RevealNextCardCore` (`Assets/Scripts/Managers/CombatManager.cs:1090`). Global caps: `totalCardsRevealed >= maxTotalReveals` or `currentRound >= maxRounds`.
2. On a trip it sets `_concluded`, `RoundForceClearActive`, `ConcludeRequested` (`CombatBudgetGuard.cs:79-81`) and, in play mode only (`:84`), calls `CombatManager.Me.ForceConcludeCombat(...)` (`:86`).
3. `ForceConcludeCombat` (`CombatManager.cs:1295`): logs `FORCE CONCLUDE COMBAT` (`:1298`), closes the chain, resets generation guards, calls `HandleCombatFinished()` (`:1301`). No HP is touched.
4. `HandleCombatFinished` (`CombatManager.cs:1304`) ends with `combatFinished.value = true` (`:1319`) but returns early on two guards: `isPlayingEffectAnimations` (`:1310`, logs `blocked`) and `!ShouldAutoConfirm()` (`:1317` - no click this frame and autoReveal/autoSpace off).
5. `PhaseManager.Update` (combat branch, `Assets/Scripts/Managers/PhaseManager.cs:157-260`) settles once `combatFinished` is true: result branches `:176-210`, then `ExitingCombatPhase()` `:223`, `ReportMatchResult(playerWon)` `:229`, `RunRecorder.RecordCombatEnd(...)` `:236`.
6. Production values (`Assets/Scenes/GameScene.unity:6118-6120`): `maxRevealsPerRound: 100`, `maxTotalReveals: 1500`, `maxRounds: 60`.

### 2.2 Loss point 1 - the forced conclusion is swallowed (largest impact)

- Only two call sites of `HandleCombatFinished` exist: the HP check `ownerPlayerStatusRef.hp <= 0 || enemyPlayerStatusRef.hp <= 0` (`CombatManager.cs:929-931`) and `ForceConcludeCombat` (`:1301`). A forced conclusion leaves HP untouched, so after either early return nothing ever retries it.
- The round cap always lands on the swallowed path. `StartCardShuffleEffect.ExecuteShuffleEffect` increments the round (`Assets/Scripts/Effects/StartCardShuffleEffect.cs:32`), zeroes `cardsRevealedThisRound` (`:33`), nulls `revealZone` (`:40`) and sets `_raiseAfterShuffleOnNextReveal` (`:111`). The next `RevealCards()` therefore takes the "Round start" branch (`CombatManager.cs:813`, condition `:814`), whose `_raiseAfterShuffleOnNextReveal` sub-branch sets `isPlayingEffectAnimations = true` (`:857`) right before `RevealNextCard(...)` (`:858`). No scene or prefab UnityEvent binds `ForceConcludeCombat`, so the guard is its only caller. `NotifyReveal` runs synchronously inside that reveal, so `currentRound >= 60` first becomes true on the first (automatic) reveal of round 60, with `isPlayingEffectAnimations == true`: `HandleCombatFinished` returns at `:1310`. This holds in manual and in auto-reveal play alike.
- The reveal cap (1500) settles only when the 1500th reveal is a player-click reveal: the click makes `ShouldAutoConfirm()` (`:1329`) true in the same frame. If the 1500th reveal is the automatic round-start reveal, or the "revealed card was exiled/destroyed, reveal the next one" step (`CombatManager.cs:908-926`), there is no click in that frame and `HandleCombatFinished` returns at `:1317` (manual play only; auto-reveal/autoSpace settles).
- After a swallowed trip `_concluded` stays latched (`CombatBudgetGuard.cs:44`, early return `:73`), i.e. the L0 backstop is disarmed for the rest of that combat; `NotifyRoundStart` clears `RoundForceClearActive` (`:54-56`) so the next round plays normally. The combat ends by natural death or by the player quitting, and is reported as an ordinary win/loss.
- Logs over-count: `FORCE CONCLUDE COMBAT` (`CombatManager.cs:1298`) is written before the early returns. The animation-blocked case also logs `HandleCombatFinished blocked: effect animations still playing` (`:1312`); the `!ShouldAutoConfirm()` case logs nothing.
- The two "always / only" paths above are code-trace conclusions; the log check in section 8 confirms them at runtime without any code change.

### 2.3 Loss point 2 - settled, but recorded as a loss (natural draws included)

- `PhaseManager.Update` only branches on `hp <= 0` (`PhaseManager.cs:176-210`): both <= 0 -> `DRAW` (`:181`); player <= 0 -> `LOSE` (`:187`); enemy <= 0 -> `WIN` (`:201`). Both alive -> no branch runs: `_resultText` keeps the previous combat's text (the empty string on the first combat after a scene load), and no wins/hearts bookkeeping happens.
- Natural draws additionally vanish from local card statistics: `CardWinRateTracker.RecordCombatResult` is invoked only from the win and lose branches (`PhaseManager.cs:195`, `:209`), so a natural draw increments no card's `combats` / `wins` / `losses` (`CardWinRateTracker.cs:117-137`, `:181-201`). The combat is absent from the card win-rate table.
- `DeckTester.currentSessionAmount` is incremented only in the win and lose branches (`PhaseManager.cs:190`, `:204`), and `DeckTester.Update` stops an `autoSpace` batch when `currentSessionAmount >= sessionAmountTarget` (`DeckTester.cs:86`). After D1, a deck that stalls every combat produces a forced draw every combat, advances no session and the batch never terminates.
- `playerWon = enemyStatusRef.hp <= 0 && playerStatusRef.hp > 0` (`:228`) is false -> `ReportMatchResult(false)` (`:229`; doc comment `:437-439` "Draws report won=false") and `RecordCombatEnd(won: false, ...)` (`:236`).
- Server side: `match_reports.won = 0` credits the ghost with a defense win (`server/onedeck-api/server.js:748-749`); `run_combats.won = 0`; the admin run page renders `(c.won ? 'won' : 'lost')` (`server.js:1542`).
- History cannot be repaired: before this plan neither table stores HP or a draw marker, so an existing `won = 0` row cannot be told apart from a genuine loss.

### 2.4 Loss point 3 - the whole run record is dropped (payload cap)

- `/api/runs` bodies are capped by the global JSON parser `express.json({ limit: '256kb' })` (`server.js:604`) = 262,144 bytes; overflow answers `413 payload_too_large` (`:611`).
- Client: `DeckNetworkClient.SendWithRetry` treats every 4xx as non-retryable (`Assets/Scripts/Net/DeckNetworkClient.cs:120-122`); `UploadOutbox.FlushCoroutine` then drops the head as a permanent rejection (`Assets/Scripts/Net/UploadOutbox.cs:144-150`, warning text `:146-147`).
- Size: one `RunCombatSample` (`Assets/Scripts/Net/NetDtos.cs:191-208`) serializes to about 176 bytes (20-character cardTypeID estimate) -> 500 samples about 88 KB, 900 about 158 KB, 1500 about 264 KB. A single reveal-cap combat alone exceeds the limit, and the longer the combat the likelier the whole run record is dropped.
- The match report is small and unaffected, but the admin dashboard lists no match reports - only a count card (`server.js:1223`) and per-deck defense aggregates (`server.js:1271-1295`).
- nginx caps request bodies at 1 MB (`server/onedeck-api/nginx/onedeck.conf:5`).
- On ingest the server truncates each combat's `series` to 500 samples from the front (`server.js:953`), so a long combat's HP curve on the run page only covers its first 500 reveals and its end state is lost.

### 2.5 Loss point 4 - delayed, or not covered

- Run records upload only at run end (`RunRecorder.CloseRun`, `Assets/Scripts/Net/RunRecorder.cs:76-89`) or at the next app start, as "abandoned" (`RecoverUnfinishedRun`, `:353-378`).
- Non-ghost opponents (default pool) produce no match report at all - `ReportMatchResult` returns at `PhaseManager.cs:444`.
- A player who quits right after a forced draw and never relaunches: that combat is never uploaded.

### 2.6 Why no test caught it

- In Edit Mode `Application.isPlaying` is false, so `NotifyReveal` never calls `ForceConcludeCombat` (`CombatBudgetGuard.cs:84-88`). `CombatBudgetHardStopTests` assert guard state only, and the headless sims break on `guard.ConcludeRequested` themselves (`Assets/Scripts/Editor/Headless/RunBudgetSim.cs:207`). The play-mode wiring - the two early returns and the missing retry - has no coverage.
- No test asserts that a natural draw reaches `CardWinRateTracker` or the upload DTOs with a draw marker, because no draw marker exists.

## 3. Design

### Step 0 - One outcome value drives everything (client)

- New append-only enum `CombatOutcome { Win, Loss, DrawNatural, DrawForced, Abnormal }` (small file under `Assets/Scripts/Managers/`).
- Resolve it exactly once, at the top of the `PhaseManager.Update` settlement block, before the result-text branches. The budget latch is still live there: `CombatBudgetGuard.ResetState()` runs only inside `CombatManager.ExitCombat` (`CombatManager.cs:271`), which `ExitingCombatPhase()` triggers later at `PhaseManager.cs:223`. Copy the conclusion record into a local before `ExitingCombatPhase()`.
- Resolution order:
	- player HP <= 0 and enemy HP <= 0 -> `DrawNatural`
	- player HP <= 0 -> `Loss`
	- enemy HP <= 0 -> `Win`
	- both alive and the guard's conclusion latch is tripped -> `DrawForced`
	- both alive and no latch -> `Abnormal` (no heart, win or stats change; error log; upload as an ordinary loss with `won=false` and empty `drawKind`). This state is unreachable by construction (section 2.2: the only `combatFinished` writers are the HP check and the forced conclusion), and is asserted by a test so an engine anomaly cannot silently cost a heart.
- How `PhaseManager` reads the latch: a small `CombatManager.TryGetForcedConclusion(out BudgetCapConclusion)` accessor that reads `CombatBudgetGuard.Me.ConclusionTripped` / `.Conclusion`. There is **no separate pending field in `CombatManager`**: the guard's latch is the single source of truth and `ResetState()` clears it in `ExitCombat` (`CombatManager.cs:271`). `EnterCombat()` only clears `combatFinished` (`:232-237`), so any other latch would leak into the next combat.
- The value drives `_resultText` (`"DRAW"` for both draws), hearts/wins (untouched for both draws and `Abnormal`), `CardWinRateTracker`, `ReportMatchResult` and `RecordCombatEnd`. Downstream code never re-derives it from HP.
- Draws also advance the automation session counter: increment `DeckTester.currentSessionAmount`, append both HP entries and call `CalculateSessionAveDmg()` for every draw, exactly like the win/lose branches do (`PhaseManager.cs:188-193`, `:202-207`). Win/loss counters stay untouched. Without this, an all-draw `autoSpace` batch never reaches `sessionAmountTarget` (`DeckTester.cs:86`).
- God-file rule: this is a bug-fix-sized edit in `PhaseManager.cs`; the enum is a new small file.

### Step 1 - Detection: the forced conclusion always settles (client)

- a. Latch the facts at the trip frame in `CombatBudgetGuard`: a public record `BudgetCapConclusion` (cap kind `Rounds` / `TotalReveals`, round, totalReveals, poolSize, peakCascadeDepth) plus `ConclusionTripped`. HP is **not** in this record - settlement HP is authoritative (two snapshots would be ambiguous).
- b. Keep the facts readable after the combat: `CombatManager` gets a `lastCombatConclusion` holder, captured in `ExitCombat` before `CombatBudgetGuard.ResetState()` (`CombatManager.cs:271`) and before `totalCardsRevealed = 0` (`:267`) - the same pattern as `roundsLastCombat` (`:186`, captured `:264`). Add `revealsLastCombat` next to it, and keep the per-combat seed in a `lastCombatSeed` field where the seed is computed (`:321-322`).
- c. Replace fire-and-forget with a **guard-latch retry**: at the top of `RevealCards()` - after the `IsInputBlocked` and attack-animation waits, before the "Round start" branch (`:813`) - if `CombatBudgetGuard.Me.ConclusionTripped && !combatFinished.value && !isPlayingEffectAnimations`, call `ForceConcludeCombat(reasonFromTheLatch)` and return. `ForceConcludeCombat` is already idempotent (`combatFinished` early return; `CloseOpenedChain` no-ops on an empty list, `ResetGenerationGuards` no-ops when clean), so a repeat call is safe. The retry is driven by the guard latch rather than the play-mode-only call, so it works in EditMode too.
- d. Split the confirmation gate out of `HandleCombatFinished`: `HandleCombatFinished(bool forced = false)` keeps the animation gate but skips `ShouldAutoConfirm()` when `forced`. `ForceConcludeCombat` passes `forced: true`; the HP path keeps the default. This is what makes the forced conclusion "fully automatic, no player confirmation" (infinity plan section 7 item 6) instead of depending on a click.
- e. `PhaseManager.Update`: no separate both-alive branch is needed beyond Step 0's `DrawForced` case; the settlement trigger (both alive + tripped latch) is the same condition.
- f. Logs become accurate: keep `FORCE CONCLUDE COMBAT` as the request line and add `FORCE CONCLUDE COMBAT settled` where `combatFinished` flips, so the two no longer over-count.
- g. God-file rule: (c)/(d) are bug-fix-sized edits in `CombatManager.cs`; the latch and record live in `CombatBudgetGuard` (child component).
- h. EditMode reachability: because the retry reads the guard latch, the production trip -> settle sequence is testable in EditMode without the `Application.isPlaying` call. Drive a real reveal with the guard wired (as `HeadlessCombatRig.cs:132-138` does), set `maxRounds = 1`, pump `RevealCards()` after the animations flag clears, and assert `combatFinished` flips with no click. Do not test by calling `ForceConcludeCombat` directly - that is the path that already passes.

### Step 2 - Recording: both uploads carry the draw kind (client)

- `MatchReportRequest` (`NetDtos.cs:105-113`) gains `drawKind` (`""` / `"natural"` / `"forced"`), `ownerHp`, `enemyHp`.
- `RunCombatEntry` (`:211-223`) gains `drawKind`, `forcedReason` (`""` / `"rounds"` / `"totalReveals"`; written only for `DrawForced`), `ownerHp`, `enemyHp` (HP at settlement, read from the status refs as `PhaseManager.cs:228` already does), `reveals` (`revealsLastCombat`), `combatSeed` (`lastCombatSeed`, full int32 - `Rng.StableHash` returns a signed value, `RngService.cs:136-140`).
- `PhaseManager.ReportMatchResult` (`:441-460`) and `RunRecorder.RecordCombatEnd` (`RunRecorder.cs:186-203`) take the `CombatOutcome` plus the optional conclusion record; ordinary combats keep the defaults (`JsonUtility` still emits `"drawKind":""`). The six `RecordCombatEnd(..., bool, ...)` call sites in `Assets/Scripts/Editor/Tests/RunRecorderTests.cs` (lines 74, 115, 218, 230, 252, 268) are rewritten with the enum.
- Reading after `ExitingCombatPhase()` is safe because `ExitCombat` copies the latch into the last-combat holders before resetting (`CombatManager.cs:267-271`); the scene binds `onExitCombatPhase` to `CombatManager.ExitCombat` (`GameScene.unity:10846-10848`).
- `ReportMatchResult` keeps its early returns (`PhaseManager.cs:444-448`): no ghost opponent, no identity, own deck.
- Local card stats become draw-aware: `CardStats` and `SessionCardStats` live in `Assets/Scripts/Managers/WriteRead/CardWinRateData.cs:11-40` (not in the tracker file). Add `drawsNatural` / `drawsForced` (missing JSON fields load as 0) and update `CardStats.ToString()` / `WinRate`. `RecordCombatResult(CombatOutcome, int)` replaces the bool: `combats++` for every real outcome; player cards take the outcome as-is, enemy cards invert only `Win` <-> `Loss`; both sides increment the matching draw counter. `Abnormal` records nothing. The CSV writes both the header and the rows (`CardWinRateTracker.cs:300`, `:310`): add the two columns and fix the existing header-only `CardName` column at the same time (drop it or emit the name). `StatsWinrateRow` gains `drawsNatural` / `drawsForced`; `StatsSnapshotUploader.BuildRequest` maps them (`StatsSnapshotUploader.cs:105-121`).

### Step 3 - Server: additive columns, same endpoints

- `ensureColumn` (`server.js:249-254`):
	- `run_combats`: `draw_kind TEXT NOT NULL DEFAULT ''`, `forced_reason TEXT NOT NULL DEFAULT ''`, `owner_hp INTEGER`, `enemy_hp INTEGER`, `reveals INTEGER NOT NULL DEFAULT 0`, `combat_seed INTEGER NOT NULL DEFAULT 0`.
	- `match_reports`: `draw_kind TEXT NOT NULL DEFAULT ''`, `owner_hp INTEGER`, `enemy_hp INTEGER`.
	- `stats_snapshots`: `draws_natural INTEGER`, `draws_forced INTEGER` (nullable on purpose: legacy clients omit them).
	- `decks`: `defense_draws_natural INTEGER NOT NULL DEFAULT 0`, `defense_draws_forced INTEGER NOT NULL DEFAULT 0`.
- Draw predicate everywhere: `draw_kind <> ''`. HP columns are display/audit data, never the authority. `drawKind` outranks `won` (a payload with both is a draw); unknown kinds coerce to `''` - never a 400, because the outbox treats any 4xx as a permanent rejection and would drop the whole run.
- `/api/matches/report` (`server.js:732-751`): whitelist `drawKind`; on a draw increment the matching `decks.defense_draws_*` counter; otherwise keep the existing `won` branch. Legacy clients (no `drawKind`) keep today's behavior.
- `/api/runs` (`:891-1018`, combat cleaning `:924-981`): parse the new per-combat fields; `combatSeed` uses the full int32 range (`toInt(value, -2147483648, 2147483647, 0)`; the loop-report precedent's `0..2147483647` clamp, `:769`, would zero half the signed seeds). Extend `stmts.insertCombat`.
- `/api/stats/snapshot` (`:831-890`): parse `drawsNatural` / `drawsForced` per winrate row; when the property is absent bind `null` and let the upsert keep the stored value - `draws_natural = COALESCE(excluded.draws_natural, stats_snapshots.draws_natural)` - the same pattern as `meta.enemySource` (`:868-870`). Shop rows must bind null for the same columns. Without this, every legacy snapshot would zero the counters.
- `won` keeps its meaning in this plan (0 for draws).

### Step 4 - Admin

- Overview counters: natural draws / forced draws (`SELECT COUNT(*) FROM run_combats WHERE draw_kind = 'natural'` / `'forced'`).
- Card win-rate table (`server.js:1254-1267`): add `draws nat` and `draws forced` columns. Rate stays `wins / combats`; new clients count draws in `combats`, so `combats = wins + losses + natural + forced` holds per `game_version` key (legacy and new clients must not share a version string; the key includes `game_version`).
- Ghost deck defense table (`server.js:1271-1295`): add `defense draws nat` / `defense draws forced`; the win-rate formula stays `dw / (dw + dl)` (a win/loss ratio; draws are shown separately, deliberately unlike the card table's all-combats denominator).
- Run page combats table (`server.js:1524-1549`): result cell renders `won ? 'won' : drawKind ? 'draw (' + kind + (reason ? ': ' + reason : '') + ')' : 'lost'`; add HP-left and reveals columns.
- New Draws section (the rev.1 plan called it `Forced conclusions (L0)` but it was never implemented): latest 50 rows `WHERE draw_kind <> ''` from `run_combats`, **LEFT JOIN** `decks` (an inner join would drop every draw against a default-pool opponent, whose `opponent_deck_id` is NULL - `server.js:976`, `:1544`) plus `runs` / `players`. Columns: when | kind | cap reason | player | version | session | rounds | reveals | HP left | seed | opponent deck | run link. Built with `section(...)` (`server.js:1095`), placed after the Infinity gate section; all dynamic text through `esc()` (`:1070`).
- Match reports stay unlisted (existing dashboard design); a draw's match-report effect is visible in the ghost defense table.

### Step 5 - Payload robustness

- Raise the global JSON limit from `'256kb'` to `'900kb'` (`server.js:604`), below nginx's 1 MB (`server/onedeck-api/nginx/onedeck.conf:5`). Express' limit is per request; other endpoints bound their own fields (`payload` <= 200 KB at `server.js:771`, catalog <= 1000 cards at `:1029`).
- **Thinning is required, not optional**: before enqueueing a `RunRecord`, cap every combat's `series` to 500 samples (first 450 + last 50) so the curve keeps its end state. The server keeps only the first 500 anyway (`server.js:953`); a 1500-reveal combat drops from ~264 KB to ~88 KB. Together with the 900 KB limit this covers ~10 capped combats per run, and the strip fallback below covers the rest.
- Client fallback in `UploadOutbox.FlushCoroutine` (`UploadOutbox.cs:121-162`): on `statusCode == 413` for a `RunRecord` item, enqueue once a copy with every combat's `series` set to null (`JsonUtility.FromJson<RunUploadRequest>` -> null each `series` -> `ToJson`; strip, never drop), then continue. Guard against loops with a per-item flag (e.g. `seriesStripped`) so a second 413 drops the item with an error log. Non-RunRecord 413 keeps today's drop.

## 4. Implementation order

1. Release precondition: confirm the build under test has `ServerConfig.enabled = 1` (manual toggle: off during Play-mode tests to avoid dirty uploads; user ruling 2026-11-26). No build-gate change.
2. Server (Steps 3, 4 and the server half of 5) + `npm test` - deployable on its own; old clients keep working.
3. Client Steps 0-2 **in one build** (the outcome value must be consumed by the DTOs/records in the same build, otherwise a `DrawForced` uploads as a loss) + regression tests.
4. Client half of Step 5 (required thinning + 413 fallback).
5. Docs: server README (columns, limits), an implementation note in `plans/plan-infinity-detection-2026-09-17.md` sections 9 / 20.5, and this plan's status line.

## 5. Tests

- Server - new `server/onedeck-api/tests/draws.test.js`, pattern = `tests/loopReports.test.js` (`node --test`, throwaway `DATA_DIR` set before `require('../server.js')`):
	1. `POST /api/runs` with a forced combat -> `draw_kind = 'forced'`, reason, HP, reveals and a negative `combat_seed` all stored.
	2. `POST /api/runs` with a natural draw -> `draw_kind = 'natural'`, both HP 0, `won = 0`.
	3. Legacy payload (fields absent) -> `draw_kind = ''`, HP NULL, behavior unchanged.
	4. `POST /api/matches/report` with each kind -> `match_reports.draw_kind` stored, the matching `decks.defense_draws_*` increments, `defense_wins` / `defense_losses` unchanged. Replay the same `reportId` -> deduped, counters unchanged.
	5. `POST /api/stats/snapshot`: a snapshot with draws stores them; a legacy snapshot (fields absent) keeps the stored values (regression for the zeroing trap).
	6. A payload with both `won: true` and `drawKind: 'forced'` stores as a draw (precedence); an unknown kind coerces to `''` and still returns 2xx.
	7. `GET /admin` shows the split counters and the draws section (including a default-pool draw, proving the LEFT JOIN); `GET /admin/run/:id` shows `draw (natural)` and `draw (forced`.
	8. A ~900 KB run body is accepted; a body over the 1 MB nginx cap is rejected upstream (documented, not unit-tested).
- Client EditMode:
	1. Outcome resolution table: win / loss / natural draw / forced draw / abnormal (no latch, both alive) -> no heart or stats change.
	2. Real trip -> settle: wire the guard (`HeadlessCombatRig.cs:132-138` style), `maxRounds = 1`, drive a real round-start reveal, clear `isPlayingEffectAnimations`, pump `RevealCards()`, assert `combatFinished` flips with no click. Do not call `ForceConcludeCombat` directly as the only test.
	3. Latch lifetime: after a forced settle, run `ExitCombat` -> `EnterCombat`, one `RevealCards()` tick, assert `combatFinished` stays false (no cross-combat leak) and `CombatBudgetGuard.ResetState()` cleared the latch.
	4. `RunRecorderTests`: forced and natural entries write the new fields; ordinary combats emit defaults; all six call sites compile with the enum.
	5. `CardWinRateTracker` split: win / loss / natural / forced counts for both player and enemy cards; legacy JSON loads with zero draws; CSV header and rows have the same column count.
	6. `UploadOutboxTests`: 413 on a RunRecord -> the stripped copy is sent and succeeds; a non-RunRecord 413 is still dropped; a second 413 drops the item with one error log; a 500-sample thinning test keeps first 450 + last 50.
- Protocol: after every `.cs` edit `refresh_unity` (compile), confirm the assembly DLL is newer than the source, save the scene (`SaveOpenScenes` is pre-approved), then `run_tests` with `init_timeout` ~180000 for suites. A run of a brand-new test class that matches 0 tests returns `passed` without compiling - check `isApplicationActive` and the DLL/source mtimes before believing a suspiciously clean green. Create the `.agent_registry` claim and scan existing claims before starting the implementation; baseline at HEAD is EditMode 804 green (reported by the 2026-11-26 attack-buff session).

## 6. Behavior changes / rollout notes

- (D1 approved, 2026-11-26) A combat that reaches round 60 ends as a forced draw at the first reveal of that round (no heart lost, no win counted) instead of continuing; the reveal-cap conclusion no longer depends on which reveal is the 1500th; the "one round of effect-less flips" after a swallowed trip disappears.
- Natural draws now reach the server as draws: the ghost's `defense_wins` no longer increments (2026-11-26 D2 ruling) and `decks.defense_draws_natural` increments instead; the run page shows `draw (natural)` instead of `lost`.
- Card win-rate stats gain rows they never had: a natural draw now increments every participating card's `combats` + `drawsNatural` on both sides, so the displayed win-rate denominator grows. Existing local JSON loads the new counters as 0.
- Forced draws likewise increment `drawsForced` for both sides; `forcedReason` lives in `run_combats` only (the admin draws section), not in the per-card aggregates.
- Draws advance `DeckTester.currentSessionAmount` (and the per-session HP/damage arrays) so `autoSpace` batches terminate; win/loss counters still ignore draws.
- Historical rows stay untouched: `decks.defense_wins` keeps whatever legacy draws were (wrongly) credited, and old `run_combats` / `match_reports` rows carry no draw information.
- A run continues after a draw (Result -> Shop); a draw neither costs a heart nor counts toward the win condition.
- Server migration is additive (`ensureColumn`); rollback = the previous `server.js` (the columns can stay). Old clients keep working (missing `drawKind` -> not a draw; missing draw counts -> COALESCE keeps stored values).
- Old clients still report their natural draws as losses until they update; single-developer project, accepted.
- A new client recovering a legacy upload journal (`RunRecorder.cs:337`, `:368`) deserializes missing ints as 0, so those old combats upload with `ownerHp: 0`, `enemyHp: 0` and empty `drawKind` - they stay ordinary non-draw rows; accepted (one run per player at upgrade).

## 7. Open decisions

| # | Decision | Recommendation |
|---|---|---|
| D1 | Fix loss point 1 so the L0 forced conclusion always settles (behavior change: combats that today run past round 60 end as draws at round 60's first reveal). | RESOLVED 2026-11-26 (user): yes - L0 is the only hard "every combat ends" guarantee, and section 9 of the infinity plan already defines the outcome as a draw. |
| D2 | Accounting for draws in ghost defense stats. | RESOLVED 2026-11-26 (user): a draw no longer credits a defense win; record per-kind draw counters (`natural` / `forced`) instead. |
| D3 | Also emit a small dedicated event at the trip (new endpoint + table) so a forced draw is recorded even if the player quits and never relaunches. | DEFERRED 2026-11-26 (user): revisit after quit-game handling is added. |
| D4 | Add `ServerConfig.enabled` to the release build gate. | RESOLVED 2026-11-26 (user): no - the switch is a manual toggle used to keep Play-mode tests from uploading dirty data; keep it manual and just confirm `enabled = 1` for the build under test. |

## 8. Zero-code verification (before any implementation)

- Precondition: confirm the build under test has `ServerConfig.enabled = 1` (`Assets/Resources/ServerConfig.asset:15` is `0` at HEAD, commit `f1e80653`). If it is off, every upload kind is missing and the draw diagnosis cannot be observed from that build.
- Local repro (forced): with autoReveal on, play a deck that stalls past round 60 and check the log for `[CombatBudgetGuard] Global budget cap hit ... round=60/60 -> force conclude requested`, then `[CombatManager] FORCE CONCLUDE COMBAT`, then `[CombatManager] HandleCombatFinished blocked: effect animations still playing` - and the combat continues.
- Local repro (natural): fight to a double KO; the result screen shows `DRAW`, the upload/admin path records it as a loss, and no row appears in the local card win-rate stats for that combat.
- Production (read-only; needs the user's go-ahead to connect to the ECS box): `run_combats` rows with `rounds > 59` dated after L0 shipped are evidence of swallowed trips (the combat ran past the cap); 413 responses in the nginx access log are evidence for loss point 3; confirm the deployed nginx config matches `server/onedeck-api/nginx/onedeck.conf` (1 MB).
- Payload path: send an oversized body through the deployed nginx and confirm UnityWebRequest surfaces status 413 (not 0). If it surfaces as 0, the outbox's 413 branch never fires and the head stalls - handle that case before relying on the strip fallback.

## 9. References

- `plans/plan-infinity-detection-2026-09-17.md` - sections 7 (item 6), 9, 20.1 (item 5), 20.5.
- `plans/plan-combat-completion-upload-gate-2026-09-06.md` - the settlement point every real combat reaches (draws included).
- `server/onedeck-api/README.md` - endpoints, deployment, tests.
- `AGENTS.md` - god-file rule (Code Placement Guide), session registry, CRLF/Tab standard.

## 10. Implementation note (2026-11-26)

All steps landed per the reviewer-fixed design; tests green. Files: `CombatOutcome.cs` (new enum + `CombatOutcomeResolver`), `CombatBudgetGuard.cs` (single `ConclusionTripped`/`Conclusion` latch + `BudgetCapConclusion`/`BudgetCapKind`), `CombatManager.cs` (latch retry in `RevealCards`, `HandleCombatFinished(forced:)`, `TryGetForcedConclusion`, `revealsLastCombat`/`lastCombatSeed`/`lastCombatConclusion` capture in `ExitCombat`, stale comments fixed), `PhaseManager.cs` (one-shot outcome resolution + draw bookkeeping), `NetDtos.cs`/`RunRecorder.cs`/`StatsSnapshotUploader.cs` (draw fields), `CardWinRateData.cs`/`CardWinRateTracker.cs` (per-kind counters + CSV column fix), `UploadOutbox.cs` (required series thinning at enqueue + 413 strip-once fallback with `seriesStripped`), `server.js` (additive columns, draw predicate, `drawKind` outranks `won`, signed-seed parse, COALESCE upsert, admin draws section/tables, `900kb` limit), README + admin, and the tests listed in section 5.

Deviations (all additive to the plan text, none reopen a ruling):
1. `run_combats` also coerces `won = 0` server-side when a draw kind is present (the plan said "`drawKind` outranks `won`"; `match_reports` already computed this - the runs endpoint now does the same instead of storing `won=1` alongside a draw).
2. Required thinning runs in `UploadOutbox.Enqueue` (serialize -> cap -> re-serialize) rather than inside `RunRecorder`, so the recovery path (`RecoverUnfinishedRun`) is covered by the same cap; the journal still stores the full series for local forensics.
3. The second-413 drop logs via `Debug.LogError` (plan: "error log"); the first 413 retry logs a warning.
4. `ForceConcludeCombat`'s request log now carries the latched `BudgetCapConclusion.DescribeReason()` (round/totalReveals facts) instead of the old static string.
5. Verification: `cd server/onedeck-api && npm test` = 27/27 (19 pre-existing + 8 new). Unity EditMode: new filters 21/21 + 21/21, full suite 843 total / 842 passed / 0 failed / 1 skipped (`RecorderAnimationPlayerTests` pre-existing `[Ignore]`). `PhaseManagerOutcomeTests` drives `Update()` via reflection and expects the by-design Abnormal error via `LogAssert`; `ForcedConclusionSettleTests` wraps `ExitCombat` in `LogAssert.ignoreFailingMessages` (production `Destroy` logs an EditMode error; play mode is unaffected). Baseline 804 green was from the attack-buff session; other sessions added tests since (total is now 843).
6. Commit/push: done - main `083ced97` (2026-11-26), pushed to origin. Play Mode still not run (not requested); `ServerConfig.asset` untouched (D4).
7. Independent verification (main session, 2026-11-26): re-ran `npm test` (27/27) and the Unity EditMode suite (targeted filters 40/40; full suite 843 total / 842 passed / 0 failed / 1 pre-existing skip, assembly DLL 12:28:27 newer than both edited sources). Two fixes made while reviewing: the `CombatBudgetGuard` class doc comment had ended up attached to `BudgetCapKind` (moved back above the class), and the forced path no longer writes the "TAP / SPACE to continue" tip before auto-settling (`HandleCombatFinished` shows it only on the non-forced path).
8. Deployment + production evidence (2026-11-26, user-authorized): deployed `server.js` to the ECS box (`i-uf66n1ofpudgn9b6rg7o`) after an online DB backup; `pm2 restart` OK, `/api/health` 200, all new columns present, `/admin` 200 with the Draws section/counters/columns. Deploying also activated the pending §26 criterion-v2 `unbounded` gate change that was committed but never deployed (the live file predated it by 2 hunks). Pre-deploy forensics are EMPTY: production run/match data ends 2026-09-16 (98 combats, all rounds < 20; 0 rows with rounds >= 60; 0 413s in the retained nginx logs), so loss points 1/3 could not be confirmed from production data - the diagnosis stays code-trace based. New data only appears once a client build with `ServerConfig.enabled = 1` runs; old clients still upload draws as losses.
