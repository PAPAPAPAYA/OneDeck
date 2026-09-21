# Phase Transition Port (Shop ↔ Combat Continuous World) — PRD / Implementation Plan

- **Date**: 2026-09-21
- **Status**: PRD, not yet implemented
- **Interactive reference**: `docs/demo/PhaseTransitionDemo.html` (validated in browser; demo behavior is the single source of truth — port 1:1 unless a deviation is listed in §13)
- **Referenced standards**: `docs/demo/CardStackRevealDemo.html` (combat stack look), `docs/demo/UIKitDemo.html` (physics / tokens, overshoot default), `docs/FaceDownFlipSystem.md` (never-cover rule)

## 1. Goal

Replace today's hard cut between Shop / Combat / Result with the demo's continuous-world transition:

- ONE continuous world, two pages stacked vertically — combat page on top, shop page below (`docs/demo/PhaseTransitionDemo.html:10-16`). Transition = the camera rig moves up/down between pages; the world never cuts.
- Shared elements animate between their two layout homes during the travel (`:19-25`): player avatar + HP pill (shop top-left → combat bottom-left), options ❚❚ button (shop top-right → combat bottom-right), deck cards (shop deck row → combat float stack, arc flight, flip face-down at the arc midpoint). Enemy HUD enters counter-direction (slides DOWN in from above).
- Result phase = camera-space overlay, camera stays at combat height; player icon + HP stay visible (new rule vs current Result behavior, `:27-28`).
- Shop shelf cards / prices / chips are world content and simply scroll away — this already matches the world-chrome architecture (2026-09-18) and needs no work.

Non-goals: combat-internal animation (`RecorderAnimationPlayer`), shop board pipeline, any game logic. The transition is presentation-only.

## 2. Current state (port leverage and gaps)

Established facts from code reading:

- Single scene (`Assets/Scenes/GameScene.unity`, only scene in build). No transition visual exists: `PhaseManager` UnityEvents destroy the old phase's objects and respawn the new phase's at the same world origin (`Assets/Scripts/Managers/PhaseManager.cs:117-250`; scene wiring `GameScene.unity:11276-11432`).
- Camera rig: `Camera Man` GameObject at (0, 0, −100), child `Main Camera` ortho size 6.06 (`GameScene.unity:5531-5546, 1837`). MilkShake owns the camera's `localPosition`; all camera motion writes to the **parent rig** (`ShopUXManager.cs:92-96, 662-670`). Shop wheel-scroll already moves rig Y (instant, ungated — also fires during combat: `ShopUXManager.cs:678-686, 754-783`).
- `onExitShopPhase` currently calls `ShopUXManager.ClearSpawnedCards` (Destroy) + `ShopUXManager.ResetCameraPosition` (`GameScene.unity:11421-11432`).
- Shop deck cards and combat deck cards are DIFFERENT GameObjects: shop physicals from `ShopUXManager.InstantiatePlayerDeckPhysCards` (`ShopUXManager.cs:557-653`), combat physicals from `CombatUXManager.InstantiateAllPhysicalCards` (`CombatUXManager.cs:3660-3721`, face-down, `SetPositionImmediate` at final slots).
- Avatar + HP pill are canvas components already re-anchored per phase by instant snap: `CombatIconPresenter.ApplyPhase` (`CombatIconPresenter.cs:86-106`), `HPNumericDisplayHorizontal.ApplyPhasePlacement` (`HPNumericDisplayHorizontal.cs:399-415`), anchors from the static `ShopTopBarLayout` viewport constants (`ShopTopBarLayout.cs:29-47`). Enemy HUD appears by `SetActive` snap, no entrance animation. Result phase hides player icon/HP today (`CombatIconPresenter.cs:86-106`).
- Combat layout funnels through `DeckPositionCalculator.CalculatePositionAtIndex` seeded from `physicalCardDeckPos.position` (`CombatUXManager.cs:581, 1613`) — the single world-offset seam. Reveal slot: `CombatUXManager.GetRevealZonePosition` (`:2112-2133`).
- Arc flight idiom exists: `CombatUXManager.MoveCardWithAnimation` (`:692-823`, two-segment DOMove via arc midpoint, `startDelay` stagger hooks). Flip API: `CardPhysObjScript.SetFaceUp(faceUp, animated)` (`CardPhysObjScript.cs:1104-1161`), squash flip on `_flipRoot`, unscaled time. Never-cover rule has a shuffle bypass (`ClearRevealedMemory` `:1167`) — combat start IS a shuffle, so the demo's mid-arc flip to face-down is legal.
- Result overlay is already camera-space (`ResultStatsPanel` spawns its own SSC canvas, `ResultStatsPanel.cs:117-145`).
- Easing convention: demo overshoot `cubic-bezier(0.34, 1.7, 0.64, 1)` ≈ `Ease.OutBack` (`docs/UIUX_Guidelines.md:64`); demo smooth ≈ `Ease.OutQuad` (`:150`).
- `docs/UIUX_Guidelines.md:3` already reserves this port: "phase-transition motion is specced separately in `docs/demo/PhaseTransitionDemo.html` (no Unity port plan yet)" — this plan fills that slot; update the line.

## 3. Design overview

Shop page stays where the shop already lives (world origin region). The combat page moves UP by one screen height. The camera rig tweens between the two page anchors. Page height is derived at runtime from the orthographic camera, not hardcoded:

	pageH = 2 × Camera.main.orthographicSize        // 12.12 world units today
	combatPageY = shopPageY + pageH                  // shopPageY = rig initial Y (0)

Demo px → world: canonical mapping is page-height based, `pxToWorld = pageH / 740` (demo PAGE_H = 740 px) ≈ 0.0164. Serialized tunables store world units; the config asset computes defaults from this factor so the demo numbers (arc 90 px ≈ 1.48 u, enemy slide 140 px ≈ 2.29 u, PAD 200 px ≈ 3.27 u) remain recognizable in comments.

Combat content offset: at runtime the driver shifts the combat world markers — `physicalCardDeckPos`, `physicalCardRevealPos`, `newCardPos` — by `+pageH` in Y once at Bootstrap (runtime transform edit, zero scene edits, per the "no new scene objects" convention of the chrome/panels ports). All deck/reveal math inherits through `DeckPositionCalculator`. Canvas-space combat UI (HP bar, numeric displays, icons) is camera-locked and needs no offset.

## 4. Orchestration: `PhaseTransitionDriver`

New runtime-built component (Bootstrap idempotent pattern, precedent `ShopChrome.Bootstrap`), created on first phase change; owns the rig Y and every shared flight for the duration of a transition. It does NOT replace `PhaseManager`; it wraps the existing UnityEvent pipeline.

Shop → Combat sequence (demo `goCombat` + `flyShared`, `PhaseTransitionDemo.html:701-739`):

1. Trigger: the world-space 离开商店 `PhysButton` in `ShopChrome` — its action today calls `PhaseManager.ExitingShopPhase()` + `EnteringCombatPhase()` directly (`ShopChrome.cs:176-181`). This button is THE canonical trigger (user ruling 2026-09-21; the demo's drag-up gesture is not ported, §13). The driver wraps this exact path: the button press first hands control to the driver, which runs the transition and fires the two phase calls at the sequenced moments below. The Space shortcut in `PhaseManager.Update` (`PhaseManager.cs:128-133`) stays as a debug path and gets the same driver treatment. On trigger the driver blocks input (`ShopInputGate` + `CombatManager.BlockInput(this)`) and records current shop scroll Y.
2. Fire the normal exit/enter pipeline immediately, with two driver-scoped exceptions (see §5 file edits): shop PLAYER DECK cards are NOT destroyed (driver borrows them as flight dummies); `ResetCameraPosition` is skipped (driver owns rig Y). Combat world content instantiates on the combat page, one screen above — off-screen, so pop-in is invisible. Combat canvas UI (HP bar, HUD) stays suppressed until landing (§4.4).
3. Camera: rig `DOMoveY(combatPageY, transDur)` with the configured ease (default `Ease.OutBack`).
4. Shared flights run concurrently (§6).
5. On camera land (plus `2 × cardStagger` tail, mirroring the demo's `wait(transDur + 2*cardStagger)` `:739`): destroy landed dummies (real combat physicals already sit face-down at identical slots — identical card-back faces make the swap invisible), unsuppress combat canvas UI, enemy HUD slides DOWN in from above (counter-direction, `:724-727`), unblock input, hand control to the normal combat start (Start Card reveal).

Result phase (demo `endCombat`, `:741-745`): unchanged mechanic — overlay in camera space, camera stays at combat height. One rule change: player icon + HP pill remain visible in Result (edit the visibility conditions in §5). Overlay fade-in can reuse the existing panel build; a 200 ms opacity fade matches the demo (`:372`).

Result → Shop sequence (demo `goShop`, `:746-752`):

1. Result overlay fades out. Input blocked.
2. Camera rig `DOMoveY` back to the recorded shop scroll Y, same ease/duration.
3. Avatar + HP pill glide back to their shop anchors (`ShopTopBarLayout` constants). Chrome reactivates on landing (fade/slide optional). Shop content re-instantiates via the existing `EnterShop` pipeline (its spawn-pop entry tweens are the return counterpart; flying combat cards back down is v1.1, §13).
4. On land: unblock, phase proceeds as today.

Phase enum: `EnumStorage.GamePhase` is NOT extended. The driver is a presentation layer that runs while the SO still reads Shop/Combat/Result; polling presenters consult the driver's suppression flags (minimal, explicit) rather than a new enum value that would ripple through every `Update` poll.

## 5. File inventory

Create:

- `Assets/Scripts/Managers/PhaseTransitionDriver.cs` — orchestrator: Bootstrap, page geometry, camera tween, flight scheduling, input block, suppression flags (`SuppressCombatCanvasUI`, `OwnsDeckCards`, `IsTransitioning`), shop scroll Y memory, completion callbacks.
- `Assets/Scripts/UXPrototype/PhaseFlightPlanner.cs` — pure static planner (testable, precedent `DamageFloaterTimeline`): px→world conversion, per-card flight schedule `{ delay, duration, arcApexWorld, flipTime }`, dummy→stack-slot mapping (top N indices via `DeckPositionCalculator` inputs), enemy slide offset. All demo-math lives here and only here.
- `Assets/Scripts/Editor/Tests/PhaseFlightPlannerTests.cs` — EditMode goldens (§9).
- `Assets/Resources/PhaseTransitionConfig.asset` + `PhaseTransitionConfigSO.cs` — lazy singleton from Resources (precedent `TagTooltipDatabaseSO`); holds §8 parameters. Runtime driver reads it; defaults computed from the 740 px page formula.
- `docs/PhaseTransition.md` — feature doc (world model, config knobs, bypass rules, known deviations).

Modify (all small, facade-respecting):

- `ShopUXManager.cs` — gate `HandleCameraScroll` by phase AND `PhaseTransitionDriver.IsTransitioning` (fixes the existing ungated-scroll bug as a side effect); split `ClearSpawnedCards` so player deck cards survive when the driver claims them (`OwnsDeckCards`); `ResetCameraPosition` no-ops while the driver owns rig Y.
- `ShopChrome.cs` — route the 离开商店 button action (`:176-181`) through the driver (`PhaseTransitionDriver.BeginShopToCombat()` when active, direct phase calls otherwise); no visual change to the button itself.
- `CombatIconPresenter.cs` — replace snap re-anchor with `DOAnchorPos`/`DOScale` glide between the two captured anchor sets (duration/ease from config, unscaled-time per house convention); player icon visible in Result; enemy icon participates in the landing slide-in.
- `HPNumericDisplayHorizontal.cs` — same glide for the HP pill; player side visible in Result; enemy side slides with the enemy entrance.
- `CombatHPBarPresenter.cs` — defer bar activation while `SuppressCombatCanvasUI` (activate on the driver's landing callback, optional 200 ms fade).
- `CombatUXManager.PhaseFlight.cs` (NEW partial, per god-file split rule) — exposes read-only stack-slot world positions/top-N mapping for the dummy flights; no edits to the `CombatUXManager.cs` main file.
- `PhaseManager.cs` — one-line hooks only if UnityEvent subscription from the driver proves insufficient; prefer driver-side `AddListener` on the existing public UnityEvents (zero PhaseManager edits).
- `docs/UIUX_Guidelines.md:3` — replace "no Unity port plan yet" with this plan + `docs/PhaseTransition.md` reference; Version History entry.
- `docs/RegressionChecklist.md` — append rows (§9.3).
- `AGENTS.md` — one bullet under Shop Systems or Animation System pointing at `docs/PhaseTransition.md`; run `wc -c AGENTS.md` (≤ 32 KB).

## 6. Shared element flights (demo `fly` / `flyShared`, `:673-729`)

Player avatar + HP pill (canvas): tween `anchoredPosition` + `localScale` between the combat capture (Awake) and the `ShopTopBarLayout` shop values — both endpoint sets already exist in code. No arc; straight glide, same duration/ease as camera.

Deck cards (world): the borrowed shop physicals fly from their shop grid slots to the float-stack top slots:

- Path: two-segment DOMove via an arc midpoint `((from+to)/2 + cardArc)` — reuse the `MoveCardWithAnimation` idiom (`apexZOffset` brings the dummy in front mid-flight).
- Stagger: `delay = i × cardStagger` (`:719`).
- Flip: `SetFaceUp(false, animated)` scheduled at `delay + transDur/2` (`flipAtMid`, `:692-694`) — legal under the never-cover shuffle bypass.
- Landing slot mapping: dummy i → stack slot for index `count-1-i` (top of stack), positions from the `CombatUXManager.PhaseFlight` partial (which reads `DeckPositionCalculator` with the combat-page offset already applied).
- Count mismatch (player deck N vs combined deck M+N+1): dummies only occupy the top N slots; lower slots are filled by real combat physicals from the start. The demo's look (3 cards landing above the red/gold decor) is preserved.
- Scale: shop card scale → `physicalCardDeckSize` stack scale, tweened over the same flight.

Enemy HUD (canvas): on landing, enemy icon + enemy HP pill start from `+enemySlide` Y offset and glide DOWN into their combat anchors (counter to the camera's up travel). Combat→Shop reverses this on the way down (slides up out).

Options ❚❚ button: v1 keeps chrome shop-only (fade out at travel start, fade in at return landing). The demo's shared-button flight top-right→bottom-right is v1.1 (§13) — combat currently has no options button home, so there is no landing target today.

## 7. Background

v1: keep the camera-space `BackGroundMotion3D` material (continuous across the travel by construction) and the camera-space `CombatHPBarPresenter` split bar (suppressed until landing, §5). The world never cuts, and world content scrolling under a fixed background already reads as camera travel.

v1.1 (§13): the demo's continuous world topo — one mesh/quad spanning both pages + PAD, red band over the combat page's top 55% (`RED_BOTTOM = COMBAT_TOP + 407`, `:548-578`) as the enemy HP display, gray topo elsewhere as the player HP display. This replaces/merges with the split bar and is a separate visual milestone with its own golden-image checklist; it also makes overshoot peek well-defined (PAD gives the background 3.27 u of headroom beyond each page edge).

## 8. Parameters & defaults (`PhaseTransitionConfigSO`)

| Demo param (`:514-521`) | Config field | Default | Range | Notes |
|---|---|---|---|---|
| `transDur` 800 ms | `transDur` | 0.8 s | 0.3–1.6 | camera + all flights |
| `ease` overshoot | `easeMode` | Overshoot | Overshoot / Smooth / Linear | → `Ease.OutBack` / `Ease.OutQuad` / `Ease.Linear` |
| `overshoot` 1.7 | `overshoot` | 1.7 | 1–3 | `Ease.OutBack` overshoot param via `EaseCurve` |
| `cardStagger` 70 ms | `cardStagger` | 0.07 s | 0–0.22 | per-card delay |
| `cardArc` 90 px | `cardArc` | 1.48 u | 0–3.6 | 90/740 × pageH |
| `enemySlide` 140 px | `enemySlide` | 2.29 u | 0–5.2 | 140/740 × pageH |
| `PAD` 200 px | `pagePad` | 3.27 u | — | v1.1 topo headroom only |
| — | `enabled` | true | bool | master switch |
| — | `skipInHeadless` | true | bool | §10 |

## 9. Test plan

### 9.1 EditMode (goldens, `PhaseFlightPlannerTests`)

Fixture style: `ScriptableObject.CreateInstance` config, no scene, precedent `DeckArcLoopLayoutTests`.

- px→world: 740 px page → exactly `2 × orthoSize`; 90 px arc → 1.48 u (tolerance 1e-3).
- Flight schedule: card i delay = `i × cardStagger`; flip time = `delay + transDur/2`; arc apex = midpoint + arc × up; all from the demo formulas with golden numbers.
- Dummy slot mapping: 3 dummies over a 7-card combined deck map to indices 6, 5, 4 (top-first) and positions equal `DeckPositionCalculator` output + combat offset.

Reminder traps honored: save open scenes before every `run_tests` (pre-approved), `refresh_unity` (compile request) after every `.cs` edit and verify assembly mtime > source mtime, full-suite `init_timeout` ≥ 180000.

### 9.2 Manual Play-Mode checklist

1. Shop → Space: camera travels up with overshoot; shelf/chrome scroll away; deck cards arc, flip face-down at mid-arc, land on the stack; enemy HUD slides down after landing; Start Card reveal then proceeds normally.
2. Ease modes: Overshoot peeks past the page edge (no void — neighbor page content visible); Smooth/Linear travel cleanly.
3. Wheel scroll during transition: ignored; after return to shop, scroll resumes at the remembered Y.
4. Result: overlay fades in; player avatar + HP pill remain visible; enemy HUD behavior unchanged.
5. Result → Shop: camera travels down; avatar/HP glide back to shop top bar; shop content re-spawns with entry pops.
6. `enabled = false`: behavior identical to today (hard cut).
7. `DeckTester.autoSpace` full-loop soak: no stuck input block, no camera drift across 10+ cycles.
8. Headless (`TestManager.overrideCombatSeed` / `-odseed N`): transitions skipped, combat results byte-identical to baseline seed files.

### 9.3 Regression checklist

Append rows to `docs/RegressionChecklist.md`: transition up/down travel, mid-arc flip-down (relates to existing row 56), enemy HUD counter-entry, Result player-HUD visibility, scroll arbitration, headless bypass.

## 10. Headless / test bypass (load-bearing)

Combat sims (infinity batch scan, determinism digests, Strategy B Play Mode tests) must not pay transition time nor depend on tween completion. When `skipInHeadless` is on, the driver no-ops (all pipeline calls fall through to today's synchronous behavior, including `ClearSpawnedCards` + `ResetCameraPosition`) if any of: `Application.isBatchMode`, `TestManager.overrideCombatSeed` set, `-odseed` on the command line, or `CombatManager.visualsOverride` is `NullCombatVisualsBehaviour`. Verified in 9.2.8.

## 11. Input & ownership arbitration

- Camera: only the driver writes rig Y during a transition; `ShopUXManager.HandleCameraScroll` is phase-gated (shop only) AND driver-gated. MilkShake keeps owning the camera child's localPosition — untouched.
- Input: `ShopInputGate` blocked for shop chrome/cards; `CombatManager.BlockInput(driver)` / `UnblockInput(driver)` paired across the whole travel; result overlay buttons gated until fade completes.
- Tween hygiene: all flights `SetUpdate(UpdateType.Normal, true)` (unscaled) per card-tween convention; `KillTweens` on dummy destruction; combat-phase durations are NOT scaled by `CombatAnimationSpeed.SpeedScale` (transition is phase-boundary, not combat playback) — decision recorded here for review.

## 12. Milestones

- **M1 — camera + pages**: page geometry, combat marker offset, rig tween, scroll arbitration, `enabled`/headless bypass, EditMode goldens for geometry. Observable: eased camera travel with today's content cut hidden off-screen.
- **M2 — shared HUD glide**: avatar/HP pill tweens both directions, Result visibility rule, enemy HUD slide-in, HP bar deferral.
- **M3 — deck card flight**: dummy borrow, arc + stagger + mid-flip, slot mapping, landing swap.
- **M4 — polish + docs**: config tuning pass against the demo side-by-side, `docs/PhaseTransition.md`, guideline/checklist/AGENTS updates.
- **v1.1 (separate plan)**: world topo background spanning pages, ❚❚ shared-button flight, combat→shop card return flight, shop-content parallax polish.

## 13. Deviations from the demo (accepted, recorded)

1. Options ❚❚ button does not fly in v1 (no combat-side home exists; combat has no options button today).
2. Combat→Shop reuses shop spawn-pop entry instead of flying cards back down (combat instances are destroyed by `ExitCombat` today; a return flight needs dummy spawns on the combat page — v1.1).
3. Demo decor (red/gold stack cards, shelf cards, chips) is demo-only content; Unity has real content in those roles.
4. Drag-up gesture on the shop page (`:769-780`) is NOT ported and is not planned — the shop→combat trigger is the existing 离开商店 button (user ruling 2026-09-21).
5. Demo param bar → `PhaseTransitionConfigSO` asset (no runtime param UI); tunables iterate in the Inspector via the config asset's custom inspector if needed.

## 14. Open questions

1. Overshoot peek shows the neighboring page's real content (pages are adjacent). Acceptable, or do we want a small inter-page gap (extra `pageGap` world units, default 0)?
2. Should the transition respect `CombatAnimationSpeed.SpeedScale`? Current decision: no (§11) — confirm.
3. Result→Shop camera target: remembered shop scroll Y vs always shop page top (current demo returns to the shop page as a whole, `:746-752`; remembered scroll Y is the better UX).

---

## Execution Record (2026-09-21)

Implemented M1–M4 in one session (user ruling: 离开商店 button is the trigger, drag-up gesture dropped).

- **Created**: `Assets/Scripts/Managers/PhaseTransitionDriver.cs`, `Assets/Scripts/UXPrototype/PhaseFlightPlanner.cs`, `Assets/Scripts/SOScripts/PhaseTransitionConfigSO.cs`, `Assets/Resources/PhaseTransitionConfig.asset`, `Assets/Scripts/Editor/Tests/PhaseFlightPlannerTests.cs`, `docs/PhaseTransition.md`.
- **Modified**: `PhaseManager.cs` (driver hooks + `AdvanceFromResultToShop` extraction), `ShopChrome.cs` (exit-button route, `ShowIfActive` gate), `ShopUXManager.cs` (scroll phase+transition gate — also fixes the pre-existing combat-wheel bug, `OwnsDeckCards` clear skip, `ReleasePlayerDeckCardsToDriver`, `ResetCameraPosition` gate), `CombatIconPresenter.cs` (Result visibility + glide + enemy entrance), `HPNumericDisplayHorizontal.cs` (same trio), `CombatHPBarPresenter.cs` (suppress gate), `docs/UIUX_Guidelines.md:3`, `docs/RegressionChecklist.md` (row 108), `AGENTS.md` (bullet).
- **Verification**: EditMode 660 tests green (7 new goldens; 1 pre-existing skip). Play Mode (user-granted): full loop ×2 (rigY 0↔12.12, glides, chrome hide/show, respawn); slowed 6 s run — overshoot peak rigY 13.31 at t≈3.3 s, dummies flip face-down at t≈3.0 s = delay+dur/2; Result player-HUD rule verified; headless bypass toggled both directions (offset 12.12↔0).
- **Deviations from §5**: no `CombatUXManager.PhaseFlight.cs` partial needed — the driver reads the 7 public marker fields directly (zero edits to god files). `PhaseManager.AdvanceFromResultToShop` made public instead of driver-side event subscription.
- **Open questions 1–3 left as decided in §11/§4** (adjacent-page peek accepted; no SpeedScale; remembered shop scroll Y).
