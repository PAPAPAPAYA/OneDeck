# Reveal-Zone Card Cover Bug — Handoff Notes

Status: **ROOT CAUSE FOUND + FIX IMPLEMENTED 2026-09-12** (see §0). The remaining transient was
the emphasize pulse pinning the reveal card's position while the deck shifted forward — the
V1–V4 machinery (§2–§3) was correct but orthogonal to it. Owner Play verification still owed
(row 95 of docs/RegressionChecklist.md).

Owner-visible symptom: during combat (FloatStack deck layout), the card sitting in the
reveal zone gets covered by a deck card in front of it — sometimes only its name/desc text
bleeds through, sometimes the whole deck card covers it; observed as transient (~0.4–1 s)
and, before the second patch, as a permanent freeze. The owner associates the transient
with the "emphasize" moment when a revealed card triggers its effect.

Related: docs/RenderLayering.md (channel model), docs/DeckLayouts.md,
docs/RegressionChecklist.md rows 84/86/93/94/95.

## 0. RESOLVED: the emphasize pulse pinned the reveal card's position

**Root cause.** `RecorderAnimationPlayer.PlayEmphasizeAnimation` (the 1.2× scale pulse that plays
on a reveal-zone card when its effect triggers — this IS the "emphasize" the owner described) set
`isPlayingSpecialAnimation = true`. That one flag was overloaded: it means both "this animation
drives my position" (pop-up peak, Stage arc, attack flight, peel exit — all of which move the card
directly) and "hands off my scale" (the pulse only runs `DOScale`). Two consumers read it:

- `CombatCardView.UpdateMotion` called `KillTweens()` every frame while it was true → the reveal
  card's live re-clamp position tween was killed each frame → the card sat still.
- `CombatUXManager.ReClampRevealZoneTargetZ` skipped while it was true → but that was harmless here
  because the target was already correct; the card simply could not travel to it.

Meanwhile the deck cards have no such guard: each `AddPhysicalCardToDeck` (one per generated token)
shifts every deck card 0.5 toward the camera. The deck front card therefore crossed in front of the
frozen reveal card and covered it, for exactly as long as the pulse held the flag (~0.5 s = 2 ×
`CombatAnimationSpeed.ScaleDuration(0.25f)`), then recovered via the V2 falling-edge reconverge.
That is the reported 0.4–1 s transient. With 1 generated card the crossing is a near-exact tie
(reveal text bleeds through the coverer's back); with 2+ it is a hard cover — matching the owner's
"1 never reproduces, 2+ does" and "sometimes only the text bleeds through".

**Evidence (Editor-prev.log, last session's log).** Mining all 448 `SETTLED` snapshots:
22 hard inversions (deck front card's z smaller than the reveal card's z). Split by whether the
V2 falling-edge fix was compiled (the log shows `ReconvergeToTargetPosition` in stack traces only
from line 657372 onward):

- Before it: 16 snapshots frozen at reveal pos z=−7.233 vs target −8.000, `special=False`,
  `tween=False`, gap −0.267, repeating for thousands of lines — the "permanent freeze" variant,
  cured by V2. Consistent: that build predates the fix.
- After it: **every** inversion has `special=True`, `tween=False` (tween killed by the per-frame
  `KillTweens`), penetration up to **−0.378** (lines 665392/665406/696226/749061). Exactly the
  signature above; the pulses happened at `CostNEffectContainer → SpawnCardForPlayer →
  AddPhysicalCardToDeck` inside `TriggerRevealedCardEffect`, i.e. the effect-trigger moment.

**Instrumentation blind spot that hid it (important).** `InferCategory` routes
`"[RecorderAnimationPlayer]"-prefixed` messages to `LogCategory.AnimationPlayback`, and the scene
ships `logAnimationPlayback: 0`. So `PlayEmphasizeAnimation START` and the ZTRACE probe produced
**zero visible lines**, which is why §4.2 concluded the emphasize path was "unidentified" and why
ZTRACE never appeared in any log. Only `[CombatUXManager]`/`[CardPhysObjScript]` lines were visible
(`logVisualSync: 1`). Fixed: `InferCategory` now routes any `[RevealZDiag]` message to
`LogCategory.VisualSync`, checked before every other prefix branch.

**Fix.** A card now declares whether its special animation owns the position:

- `CardPhysObjScript.BeginSpecialAnimation(bool drivesPosition)` (uses `CallerMemberName` for a
  diagnostic `lastSpecialAnimationTag`), `SpecialAnimationPinsPosition` (true when
  `isPlayingSpecialAnimation && (specialAnimationDrivesPosition || isPoppedUp)`),
  `KillScaleAndRotationTweens()`; `SetTargetPosition` early-returns on `SpecialAnimationPinsPosition`
  instead of on the raw flag; `StopSpecialAnimation` restores the default.
- `CombatCardView.UpdateMotion` kills the position tween only when the animation owns it, otherwise
  kills scale/rotation only — so the reveal card keeps gliding to its re-clamped z through the pulse.
- `PlayEmphasizeAnimation` declares `drivesPosition: false`; all 14 position-driving starters in
  CombatUXManager, the AttackAnimationManager flight and the shuffle pass `true`.
- `ReClampRevealZoneTargetZ` skips only on `SpecialAnimationPinsPosition` (so the re-clamp also
  works during the pulse, not just after it).
- Bonus hardening: `PopUpCard`'s base z for a reveal-zone card is now the live
  `GetRevealZonePosition().z` instead of a captured mid-shift transform pose, so a deck that grows
  during a hover pop-up can no longer leave the peak behind the deck front card.

**New verification tooling.** A `LateUpdate` **per-frame COVER monitor** in CombatUXManager scans
every physical card (`CardPhysObjScript.AllPhysicalCards` registry, allocation-free) and reports
the exact frame another card's plane crosses in front of the reveal card's plane while the two
overlap on screen: `COVER-START` (penetration, HARD vs text-slab band, deckCount, deckFocused,
effectAnims, both cards' pos/target/tween/special/specialTag/poppedUp/inDeckIdx, idealRevealZ),
`COVER-CONTINUE` (1 Hz heartbeat), `COVER-END` (duration, frames, worst penetration). No existing
probe could catch this: `LogRevealZoneZViolation`/`SETTLED`/`NEAR-TIE` all skip animating cards or
sample at discrete moments. `EMPHASIZE-START` is also logged now.

**How to verify (owner).** Turn ON Log Switches → **Log Visual Sync** only. Repro with RIFT_PRIEST
(2 tokens) and RIFT_HATCHERY (3 tokens) on a large deck; expect **zero** `COVER-START` lines and a
revealed card that glides forward with the deck during the pulse. Full step/check list in
RegressionChecklist row 95.

## 1. Ground facts (all verified, needed to reason about anything below)

- Camera at z=-100, smaller world z = closer = drawn later = on top. ALL physical-card
  renderers (sprites + world TMP) are queue 3000 / Default layer / sortingOrder 0, so
  **cross-card draw order is decided purely by root z**. docs/RenderLayering.md.
- Every physical card is a z-slab. Inner prefab `Assets/Prefabs/UXPrototype/PhysicalCard.prefab`
  (nested by PhysicalCardParent / MinionPhysicalCardParent / StartCard prefabs):
  CardName/CardDesc/CardRarity/CardTag local z = **-0.06**, CardPrice/CardCost -0.02,
  face 0, edge/shadows +0.1..+0.25. Text pokes 0.06 IN FRONT of its own face; full slab
  span 0.31. Consequence: any two cards whose ROOT z differ by less than 0.06 show the
  back card's text through the front card's face; below 0 they fully interleave.
- Scene (GameScene, CombatUXManager): deckLayoutMode=3 (FloatStack), zOffset=0.5
  (per-index z step, all modes share `basePos.z - zOffset*index`),
  revealZoneZGap=-1 (auto → gap = |zOffset| = 0.5), revealCardCountsAsDeckFront=1,
  popUpZBoost=0 (hover popup is X-only), randomDeckPositionOffsetRange.z=0.
- Rest state is SAFE by a wide margin (0.5 step > 0.31 slab). Every observed cover is a
  transient/state-transition artifact or a frozen mid-flight state.
- AddPhysicalCardToDeck Insert(0) shifts every deck card one index → every deck card's
  target z moves 0.5 toward the camera; deck cards re-target immediately (no guard).
  The reveal card's matching shift is a separate "re-clamp" path — all variants below are
  failures of THAT path's timing.

## 2. The four diagnosed variants (chronological, each with evidence)

### V1 — deferred re-clamp chase (original transient)
Re-clamp was skipped while ANY reveal position tween played
(`TryReClampRevealZoneTargetZ` guard). Deck cards have no guard. On batch inserts
(RIFT_PRIEST generating 2+ tokens, batchMoveStagger=0.06) the 2nd insert's deck shift
launched immediately while the reveal shift waited a full tween duration: the deck front
card landed on its new slot and sat in front of the still-chasing reveal card.
Evidence: log `SetTargetPosition SPIKE_SKELETON ... -6.00` while
`RIFT_PRIEST currentPos z=-5.58` chasing -6.50. Single-token inserts never deferred →
owner detail "1 card never reproduces, 2+ does" matched exactly.
Targets were always correct (gap 0.5) — which is also why the strict-inversion probe
logged nothing (it early-returns while tweens play).

### V2 — permanent freeze via CombatCardView.KillTweens
`CombatCardView.UpdateMotion` calls `KillTweens()` EVERY FRAME while
`isPlayingSpecialAnimation` (its job: let CombatUXManager/AttackAnimationManager drive the
transform directly). This kills `_positionTween` mid-flight; the special animation's
slot-back restores the captured pose and nothing re-drives the card → frozen forever.
Captured live (play-mode probe): reveal actual z=-7.233, target -8.000, no tween playing,
deck front at -7.500 in front of it. Plan A's immediate restarts made mid-flight
interruptions far more likely, which converted V1's transient into this permanent state.

### V3 — PopUpCard mid-flight capture
`PopUpCard` KillTweens()s first, then captures `transform.position` as BOTH the pop-up
base and the restore point (`popUpOriginalPosition`). For the reveal-zone source card
interrupted mid re-clamp flight, the whole pop-up + slot-in ran anchored at the stale
mid-flight pose. SETTLED snapshot caught it: reveal frozen z=-5.624 (special=True) while
front card z=-5.974 was in front; 3 s later reveal correctly at -6.5.

### V4 — reveal-ENTRY tween deferral (double-distance chase) — LAST DIAGNOSED
The reveal-ENTRY tween carries `wrappedOnComplete` (input unblock + re-clamp on landing),
so the guard had to defer to it. Tokens inserted mid-entry deferred BOTH re-clamps; when
the entry landed, one chase ran from the STALE reveal z across the slot the front card
already occupied. Evidence: `StartPositionTween START card=RIFT_PRIEST from=(-0.32,-1.84,-5.50)
to=(-0.32,-1.84,-6.50)` (double distance) while front card was already resting at -6.00.
A `[Hover] EndHover ... force-hide` also restarted that chase mid-flight (from -5.63),
extending the window.

## 3. Fixes applied (working tree, UNCOMMITTED; dotnet build 0 errors)

All in the three UXPrototype scripts + one manager; function-level summary:

1. `CardPhysObjScript`:
   - `_positionTweenHasCompletionCallback` flag + `PositionTweenHasCompletionCallback`
     property; stash `_positionTweenCompletionCallback` (+`RetakePositionTweenCompletionCallback()`).
   - `StartPositionTween` always wires OnComplete (clears flags, samples the reveal-z probe,
     then runs the user callback).
   - `KillTweens` clears the flag/stash.
   - `ReconvergeToTargetPosition()` — re-tween home if no tween playing and pos != target.
   - `CompleteInFlightPositionTween()` — DOTween `Complete(false)` fast-forward for
     callback-less tweens only (never touches the entry tween's callback timing).
2. `CombatCardView`: special-animation FALLING EDGE (`_wasSpecialAnimating`) →
   `ReconvergeToTargetPosition()` (fixes V2).
3. `CombatUXManager`:
   - `TryReClampRevealZoneTargetZ` three branches: no tween → ReClamp; callback-less tween
     playing → ReClamp (plain restart, same-frame sync); ENTRY tween playing →
     `RetakePositionTweenCompletionCallback()` + `SetTargetPosition(latestRevealZ, callback)`
     (fixes V4; unblock fires exactly once on the restarted tween's landing).
   - `NotifyRevealCardPositionTweenLanded()` → probe sample `RevealTweenLanded` (probe
     blind-spot: every other call site runs while tweens play and is skipped).
   - `ScheduleSettledStateDump`/`DumpSettledRevealZState` — SETTLED rest-state snapshot
     (1 s / 3 s after mutations) + NEAR-TIE detector (any two rest cards |Δz| < 0.07).
   - `PopUpCard` starts with `CompleteInFlightPositionTween()` (fixes V3).
4. `RecorderAnimationPlayer`: `TraceRevealZDuringEmphasize` 10 Hz ZTRACE coroutine started
   from `PlayEmphasizeAnimation` — **note: wrong site, see §5 lead 1.**
5. `docs/RegressionChecklist.md` row 94 documents V1–V3 (V4 partially).

Do NOT revert these files wholesale: they also contain earlier uncommitted work
(2026-09-09 hover slot-anchor fix, the [RevealZDiag] probes from the first session, and
unrelated changes elsewhere in the tree).

## 4. Current state / the two false leads from the previous round

Superseded by §0 — kept because both mistakes are easy to repeat.

1. **Stale-assembly risk is real, and both leads below are explained by it plus log routing.**
   Confirm the loaded build from log markers before trusting any "still broken". Marker timeline
   found in Editor-prev.log (each one exists only in one build generation):
   - V1-era: `TryReClamp SKIP positionTweenPlaying` (lines 457722–576064; absent from current source)
   - V4-era: `SKIP completionCallbackTween` (lines 590539–833920; absent from current source)
   - current source: `TryReClamp RESTART entryTweenWithCallback` — **0 occurrences in the whole log**,
     i.e. that branch never fired during the captured sessions
   - row-95 era: `EMPHASIZE-START`, `COVER-START` / `COVER-CONTINUE` / `COVER-END`, and
     `ReClamp SKIP specialAnimationPinsPosition`
   Note `ReconvergeToTargetPosition` only appears in stack traces from line 657372 onward — before
   that the V2 fix was not compiled, which is exactly where the 16 frozen `special=False` snapshots
   (§0 evidence) live.
2. **"The emphasize is not `PlayEmphasizeAnimation`" was WRONG — it was a log-routing artifact.**
   The 8 `PlayEmphasizeAnimation START card=SourceCard` lines came from test-fixture cards (the name
   `SourceCard` only exists in `Assets/Scripts/Editor/Tests/`), and the function's later real
   invocations were invisible because `[RecorderAnimationPlayer]` routes to
   `logAnimationPlayback`, which the scene ships OFF. Same reason ZTRACE produced zero lines.
   `PlayEmphasizeAnimation` IS the pulse on the reveal-zone card; §0 fixed both the routing and the
   bug. Lesson: before declaring a code path unidentified, verify the *log switch routing* of the
   tag you are grepping for (`TestManager.InferCategory`), not just that the log line exists.

## 5. Leads for the next person (ordered)

1. **~~Find the real emphasize path.~~ DONE — it is `PlayEmphasizeAnimation`, see §0.** The earlier
   "unidentified" conclusion came from `logAnimationPlayback` being OFF in the scene; the probes
   are now routed to VisualSync and `EMPHASIZE-START` prints on every reveal-zone effect trigger.
2. **Suspect remaining capture/kill sites** for the same V3 pattern:
   `InterruptActivePopUpSlotIn`, `MoveCardWithAnimation` arc apex z
   (`arcMidpoint.z -= zOffset * (finalIndex - minFinalIndex)`), the
   `EndHover force-hide` path that restarts tweens without callbacks (seen in the V4 log),
   AttackAnimationManager return flight. **The arc apex is the one to look at if a cover ever
   comes back**: `arcMidpoint.z -= zOffset * (finalIndex - minFinalIndex)` pushes a batch's apex
   forward by a full zOffset step per rank, so a Stage batch of 3+ cards whose start index is near
   the deck front can put the apex in front of the reveal card mid-arc (algebra:
   `i_start > count − 2k + 3`). Not observed in the §0 logs, but it is a real second path.
3. **If a near-tie remains at rest**, NEAR-TIE detector will print it
   (`[RevealZDiag] NEAR-TIE gap=...`); the 0.07 threshold covers the text-slab band.
4. **THE RECOMMENDED DURABLE FIX — sortingOrder takeover (was "Plan B", owner-approved in
   principle, never implemented).** Assign every physical card renderer an explicit
   sortingOrder derived from deck front-to-back rank (e.g. `1000 + (Count-1-index)*10`,
   reveal card highest rank). Transparent sorting consults sortingOrder BEFORE z, so
   cross-card order becomes deterministic regardless of any tween/freeze/capture state;
   z keeps only intra-card ordering (already self-consistent). This kills every variant
   above visually, in one stroke, including as-yet-undiscovered transients.
   Implementation cautions:
   - Retune the WHOLE order ladder in the same change: Global canvas -1, Combat/Shop/Result
     canvases +1 (2026-09-09 decision), ResultStatsPanel 200, CardTagTooltip 300 must all
     move above the card band (e.g. cards 1000+, canvases 20000+); particles stay above
     card queue by material queue (3050) — queue beats order across groups, verify.
   - Sync every renderer: card back (copies cardFace.sortingOrder), runtime AttackPrint,
     FlipRoot children, BigShadowFollower.
   - Natural hook: `UpdateAllPhysicalCardTargets` already iterates all cards on every
     mutation; give the reveal card rank = front slot + 1; add a temporary +boost while a
     card is special-animating if the popup should overlay.
   - The z/re-clamp machinery can then stay as-is (input/hover arbitration still uses
     positions); it stops being visual-critical.

## 6. Diagnostic tooling inventory (all temporary — remove once resolved)

Tagged `[RevealZDiag]`, routed to TestManager category **VisualSync** (`InferCategory` now checks
the tag FIRST, so no probe can be swallowed by its source prefix again):

- **`CombatUXManager` per-frame cover monitor** (§0) — `LateUpdate` → `MonitorRevealZoneCover`,
  using the `CardPhysObjScript.AllPhysicalCards` registry. Prints `COVER-START` (penetration,
  HARD vs text-slab band, deckCount, deckFocused, effectAnims, both cards' full state incl.
  `specialTag`/`drivesPos`/`pinsPos`, idealRevealZ), `COVER-CONTINUE` (1 Hz heartbeat) and
  `COVER-END` (duration/frames/worst penetration). Episodes via `EndCoverEpisode`.
- `CombatUXManager.LogRevealZoneZViolation(context)` — strict root-z inversion detector
  (TARGET and ACTUAL levels). Contexts: `UpdateAllPhysicalCardTargets`,
  `AddPhysicalCardToDeck`, `RevealEntryLanded`, `RevealTweenLanded`.
- `TryReClamp` decision lines: `RESTART entryTweenWithCallback`, `ReClamp ... applied=`,
  `ReClamp SKIP specialAnimationPinsPosition` (current; the older `SKIP positionTweenPlaying` /
  `SKIP completionCallbackTween` strings only exist in pre-2026-09-12 builds).
- `AddPhysicalCardToDeck` probe line (spawnZ / deckCount / revealCard).
- `ScheduleSettledStateDump` / `DumpSettledRevealZState` — SETTLED snapshots (1 s/3 s
  after mutations/landings) + `NEAR-TIE` detector (threshold 0.07).
- `CardPhysObjScript.NotifyRevealCardTweenLandedToCombatUX` — landed probe hook.
- `RecorderAnimationPlayer.PlayEmphasizeAnimation` `EMPHASIZE-START` line + the
  `TraceRevealZDuringEmphasize` ZTRACE coroutine (now registry-based, `P` marks a
  position-pinning card).

Enabling: GameScene TestManager → Log Switches → **Log Visual Sync** only. Scene value today:
`logVisualSync: 1` (uncommitted), every other switch 0. Grep targets: `RevealZDiag`, `COVER-`,
`NEAR-TIE`, `VIOLATION`, `ZTRACE`, `EMPHASIZE-START`, `TryReClamp`. Editor.log:
`C:\Users\damen\AppData\Local\Unity\Editor\Editor.log` (the previous launch is kept as
`Editor-prev.log` — that is where the §0 evidence was mined from).

Removal checklist: delete the probe blocks + the helper members listed above + every
`TestManager.Log/LogWarning` line tagged `[RevealZDiag]` + the `AllPhysicalCards` registry and
its `OnEnable`/`OnDisable` lines in CardPhysObjScript, revert the `InferCategory` branch, then
re-verify row 95. **Keep** the non-diagnostic half of the fix: `BeginSpecialAnimation` /
`SpecialAnimationPinsPosition` / `KillScaleAndRotationTweens` and their call sites.

## 7. Reproduction

- Deck with RIFT_PRIEST (生成2信徒) — reveal it, let the effect generate 2 RIFT tokens
  (AddTempCard → AddPhysicalCardToDeck ×2, staggered 0.06 s). RIFT_HATCHERY (3 tokens) is
  a heavier case; single-token generators are the control (a single insert only ties, it does
  not cross).
- Watch the revealed card against the deck front card through: token inserts, **the
  source-card emphasize pulse** (the scale pulse — this is where the cover used to happen), and
  the return-to-deck.
- Deck size matters: the reveal z clamp only engages once the deck is deep enough
  (`frontMostZ − 0.5 < physicalCardRevealPos.z`), so a 16-card deck is a reliable size.
- Keep Log Visual Sync ON the whole time; leave the state for ≥3 s before leaving Play so
  SETTLED snapshots print; live-state probes via Unity MCP `execute_code` reading
  `CombatUXManager.me` (physicalCardInRevealZone pos/target/flags + physicalCardsInDeck
  per-card pos/target) proved decisive twice — keep the Play session alive when asking for help.
- **Pass criterion:** zero `COVER-START` lines in the whole session, and no
  `REVEAL-Z VIOLATION` / `NEAR-TIE`.
