# Phase Transition (Shop ↔ Combat Continuous World)

Implementation of `docs/demo/PhaseTransitionDemo.html` — plan `plans/plan-phase-transition-world-camera-2026-09-21.md`, shipped 2026-09-21.

## World model

ONE continuous world, two pages stacked vertically:

- **Shop page** at world origin (camera rig Y = 0, free wheel-scroll inside).
- **Combat page** one screen above: `combatPageY = shopPageY + pageH`, `pageH = 2 × Camera.orthographicSize` (12.12 world units at ortho 6.06).
- At scene load `PhaseTransitionDriver` shifts the combat world markers (`physicalCardDeckPos`, `physicalCardRevealPos`, `showPos`, `gravePosition`, `physicalCardNewTempCardPos`, `statusEffectConsumePos`, `deckFocusTargetPos` on `CombatUXManager`) by `+pageH`; all deck/reveal math inherits through `DeckPositionCalculator`. When the driver is unavailable the offset is removed symmetrically and the game behaves byte-for-byte like before the port.

Transitions = the camera rig (`Camera Man`, parent of Main Camera — MilkShake keeps owning the camera child's localPosition) tweens Y between the pages. The world never cuts.

The v1.1 world topo background (2026-10-03) is ONE world-space sheet (`WorldTopoBackground` + `Custom/ContourLinesBackgroundWorld`) spanning both pages plus the config PAD (`pagePadDemoPx`, overshoot headroom). The region above the combat page center (50%, user ruling) is the enemy HP display area — `GameColorPalette.HpBarEnemyColor` base, dark-red contour lines; gray below is the player region. The pattern is world-locked (the travel carries it; shop wheel scroll gains the demo's parallax). It replaces the screen-fixed BackGroundMotion3D RawImage under the Global Canvas (disabled; camera clear stays as fallback), and is what makes the overshoot reveal more background instead of a void. The contour field is the retired backdrop's `VNoise3D` 3D Perlin time-slice ported verbatim (`SliceNoise3D.hlsl` — the graph's muted second branch survives only as the constant `_FBM2Freq × 0.5` bias), with iso-lines at integer `v = h × _Levels` (2026-10-03 look-parity fix, row 145). User-tunable on `Assets/Resources/Materials/WorldTopoBackground.mat` (applies on the next Play session): `_Levels` / `_NoiseScale` (density — `_WorldScale` derives from it at Awake) / `_LineWidth` / `_Speed` / `_Intensity` / `_FBM2Freq` / `_BgColor` / `_LineColor`; binder/palette-owned, do not edit: `_SplitWorldY` (50% ruling), `_RedColor` / `_LineColorRed` (`HpBarEnemyColor`).

## Orchestration: `PhaseTransitionDriver`

Runtime-built singleton (`RuntimeInitializeOnLoadMethod.AfterSceneLoad`); no scene objects. Wraps the existing `PhaseManager` enter/exit pipeline:

- **Shop → Combat** (trigger: the 离开商店 `PhysButton` in `ShopChrome` — the ONLY player-facing trigger since the 2026-10-02 user ruling removed the `PhaseManager` Space shortcut; `DeckTester.autoSpace` still drives the same `PhaseManager` branch for automation; the demo's drag-up gesture is NOT ported — user ruling 2026-09-21): block input, `ExitingShopPhase()` + `EnteringCombatPhase()` at travel start (combat content spawns a page above, off-screen), `BlockInput(driver)` holds `RevealCards` until landing, camera travels up, dummies fly (below), on landing unblock → `InstantiateAllPhysicalCards` + Start Card reveal, dummies destroyed, the compare bar arrives already pinned (it slides with the page). The shared HUD (avatar + HP pill, enemy HUD) world-flies from travel start (HUD world flights below); the chrome band + section panels are page content that scroll away with the camera (fix 4: `ExitShop` skips their hide while `IsTransitioning`).
- **Result phase**: no camera change (camera-space overlay). **Rule change**: player avatar + HP pill stay visible in Result (demo :27-28) — and since the 2026-10-02 re-audit fix 3 the ENEMY HUD does too (the demo keeps it behind the overlay); it slides UP out during the return descent.
- **Result → Shop**: `AdvanceFromResultToShop()` at travel start (shop spawns off-screen below), camera travels down to the remembered shop scroll Y, avatar/HP pill world-fly back to the chrome mirror spots, chrome + section panels scroll INTO view during the descent (shown at travel start — `ShowIfActive` ungated, fix 4b); the world mirrors return inside the flight's `OnComplete` (driver's `SetMirrorsActive(true)` stays as an idempotent safety net).

Static flags other systems consume: `IsTransitioning`, `OwnsDeckCards`, `SuppressCombatCanvasUI` (enemy icon/pill visibility until the camera lands — no longer gates the compare bar), `Travel` (`None`/`ToCombat`/`ToShop` — drives the HUD presenters' world flights), `Available`, plus page-origin statics `ShopPageY` / `CombatPageY`.

### Deck-card flight (demo `flyShared` cards branch)

The shop's player-deck physical cards survive `ClearSpawnedCards` (`OwnsDeckCards` makes the clear methods no-op; shelf cards also persist and simply scroll away) and are borrowed as flight dummies (`ShopUXManager.ReleasePlayerDeckCardsToDriver`). Each dummy: two-segment DOMove via `PhaseFlightPlanner.ArcApex` (midpoint + `cardArc`), delay `i × cardStagger`, both segments (+ scale + fan rotation) on the SHARED ease (`cfg.ApplyEase` — fix 1, 2026-10-02 re-audit: the demo's animation-level easing re-applies per keyframe interval, so Overshoot bends the card paths too), instant cover at `delay + transDur/2` (`SetFaceUp(false, animated:false, force:true)` — fix 6: the demo swaps to the card back at the apex, no animated flip; legal under the never-cover rule's shuffle bypass), scale to `physicalCardDeckSize`, dummy i lands on stack slot i above the deck anchor. The real combat physicals pop into the same slots face-down at landing, so destroying the dummies is invisible. The landing wait is count-aware (2026-10-02 audit F3): `PhaseFlightPlanner.TotalDuration(dur, stagger, cardCount)` = `dur + (cardCount − 1) × stagger` — the last dummy's own landing time — instead of the demo's fixed 3-card `dur + 2 × stagger`, so decks above 3 cards no longer destroy tail dummies mid-arc.

### HUD world flights (canvas, world-locked; 2026-10-02 re-audit fixes 2/3/7)

While `Travel != None`, the shared HUD pieces fly in WORLD space (`HudWorldFlight`, `PhaseHudFlight.cs`): the flight tweens a world point and `LateUpdate` re-projects it onto the canvas rect (`ScreenPointToWorldPointInRectangle`), so the camera's overshoot carries them on screen exactly like the demo — no scale tween on the flight path itself (fix 7; the handoff scale windows at the endpoints are the emulation seam, see the deviations section). Homes: the shop top bar = the built chrome's Avatar/HpPill world centers (`ShopChrome.TryGetAvatarWorldCenter` / `TryGetHpPillWorldCenter`); the combat side = the captured combat anchors shifted to the combat page (`PhaseFlightPlanner.HudHomeAtPage` — its basis is the carrying canvas root's OWN Y, not the camera rig's: the SSC canvas root lags the rig within the travel-start frame and a rig-based home lands short by exactly that lag, VISUAL-FIX(2026-10-03)). The enemy HUD (icon + pill) starts its slide-in TOGETHER with the camera travel and runs the FULL `transDur` from `enemySlide` above (fix 2 — the world position keeps it outside the viewport until the arrival, like the demo) and slides UP out during the return descent (fix 3). Without the chrome mirrors (headless corner) no flight starts and the legacy anchored glide applies. During the travel the world Avatar/HpPill mirrors hide (single shared copy) and return at the handoff. **Handoff seam (2026-10-03)**: the swap is driven by the flight itself — on departure the presenter hides the mirror only after `flight.Begin` has world-locked the canvas copy at the mirror spot (no blank frame), and the canvas copy eases from the mirror's shop scale (`ShopMirrorScale.canvasShopScale`) to the combat scale over the flight's opening `HandoffScaleWindow` (0.35, `Ease.OutQuad`); on the return it eases back over the final window and the flight's `OnComplete` shows the mirror + hides the canvas copy in the same frame (`_handedOffToMirror` suppresses re-activation until the settle edge). The full-screen HP compare bar IS page content: a `HudWorldFlight.Pin` world-locks it at its combat-page home for both travels, so it slides in from the top edge with the page and slides up out on the return; `SuppressCombatCanvasUI` no longer gates the bar (it still gates the icon/pill enemy rules).

## Config: `PhaseTransitionConfigSO` (`Assets/Resources/PhaseTransitionConfig.asset`)

Lazy singleton; a missing asset = transitions off, no error. Demo param bar (`PhaseTransitionDemo.html:514-521`) mapping:

| Demo param | Field | Default |
|---|---|---|
| transDur 800 ms | `transDur` | 0.8 s |
| ease overshoot | `easeMode` | Overshoot → `Ease.OutBack` |
| overshoot 1.7 | `overshoot` | 1.7 |
| cardStagger 70 ms | `cardStagger` | 0.07 s |
| cardArc 90 px | `cardArcDemoPx` | 90 (world = px/740 × pageH) |
| enemySlide 140 px | `enemySlideDemoPx` | 140 |
| PAD 200 px | `pagePadDemoPx` | 200 (v1.1 world topo only) |
| — | `enabled` / `skipInHeadless` | true / true |

All transition tweens run unscaled-time. They are NOT scaled by `CombatAnimationSpeed.SpeedScale` (phase-boundary, not combat playback — recorded decision, plan §14).

## Headless / test bypass (load-bearing)

`Available` is false when: config missing/disabled, `Application.isBatchMode`, `TestManager.overrideCombatSeed != 0` (covers `-odseed N`), or combat visuals are `NullCombatVisuals(Behaviour)`. Callers (`ShopChrome`, `PhaseManager`) fall back to the legacy direct phase calls; the world offset is removed within a frame (verified in Play: `overrideCombatSeed 5 → deckPosY 12.12→0`, restored on 0). Combat sims (infinity batch scan, seed repro, Strategy B) pay zero transition time.

## Arbitration

- **Camera**: only the driver writes rig Y during a transition; `ShopUXManager.HandleCameraScroll` is gated by phase (shop only — fixing the pre-existing combat-wheel bug) AND by `IsTransitioning`. `ResetCameraPosition` no-ops while transitioning.
- **Shop chrome visibility (fix 4)**: `ShopManager.ExitShop` skips its `HideIfActive` pair while `IsTransitioning` — chrome + panels are page content and scroll away with the camera; the legacy hard cut keeps the hide (the camera never moves there). `ShopChrome.ShowIfActive` has no `IsTransitioning` gate (both chrome and panels appear at the Result→Shop travel start and scroll INTO view). `HudViewportPin` (OptionsButton) is always pinned while the band is active (2026-10-03 ruling — row 144); mid-travel and in combat the button stays at the viewport corner while the rest of the band scrolls with the page.
- **Input**: `ShopInputGate` + `CombatManager.BlockInput(driver)` paired across the travel; `PhaseManager` phase keys route through the driver first. **Busy guard (2026-10-02 audit F1)**: while `IsTransitioning`, every phase-key/button call site (`PhaseManager.Update` Shop + Result branches, `ShopHudBinder` LeaveShop) returns instead of running the legacy hard cut — the driver's `false` return means "driver unavailable → legacy is correct", never "busy" (during a Result→Shop travel the phase already reads Shop, so an unguarded Shop branch would double-enter combat mid-flight).

## Verified (2026-09-21 Play Mode)

- Shop→Combat→Result→Shop full loop ×2 (camera Y 0↔12.12, avatar/HP glide both directions, chrome hide/show on landing, shop respawn).
- Mid-flight (slowed to 6 s): overshoot peak rigY 13.31 > 12.12 at t≈3.3/6 s; dummies flip face-down at t≈3.0/6 s (= delay + transDur/2); stagger schedule.
- Result: player icon + player HP pill visible, enemy hidden.
- Headless bypass toggle both directions.
- EditMode: 660 tests green (`PhaseFlightPlannerTests` 7 goldens + pre-existing suite), 1 pre-existing skip.

**2026-10-02 re-audit fixes (F1-F3 + the seven items above) + 2026-10-03 follow-ups (enemy pill root reactivation, canvas-lag flight-home fix)**: EditMode-verified (full suite green; `PhaseFlightPlannerTests` 9 goldens incl. `HudHomeAtPage`) and **user Play-verified 2026-10-03** (Shop→Combat entrance with the enemy HUD slide-in + icon/pill pairing on their anchors, Result → Shop return). Remaining Play matrix items (ease-mode variants, 8+ card deck tail, viewport pin re-pin details) pending.

## Deviations from the demo

### Accepted (user rulings, 2026-09-21)

1. **❚❚ options button — superseded 2026-10-03 (user ruling, v1.1 round):** it never flies AND never moves — `HudViewportPin` is ungated (always pinned while the chrome band is active), so the shop viewport corner serves combat as-is with unchanged press semantics. RegressionChecklist row 144.
2. Combat→Shop uses the shop's existing spawn-pop entry; no return card flight.
3. Shop→combat drag-up gesture not ported (离开商店 button is the trigger).
4. Demo param bar → `PhaseTransitionConfigSO` asset instead of a runtime UI.
5. v1.1 status (updated 2026-10-03): the world topo background is LANDED (row 143); the ❚❚ flight was superseded by the always-pin ruling (item 1); still open: the combat→shop card return flight. The 2026-10-03 overshoot-peek note is resolved by the sheet (its PAD headroom = `pagePadDemoPx`).

### Fixed — 2026-10-02 re-audit items (vs `PhaseTransitionDemo.html`)

User ruling 2026-10-02: the deviations below were not accepted; all seven are now fixed (same day). Supersedes `plan-phase-transition-audit-fixes-2026-10-02` §3.5 (F5 was "document as accepted" — became open item 4 and is fixed too). RegressionChecklist rows 132-138.

1. **Card flights use the shared ease** — `FlyDummiesToCombatStack`'s hardcoded `OutQuad`/`InQuad` segments (and scale/rotation tweens) now carry `cfg.ApplyEase`; Overshoot bends the card paths like the demo (:684-691, easing re-applies per keyframe interval).
2. **Enemy HUD entrance is parallel and full-length** — the slide-in starts at travel start (`Travel` edge) and runs the full `transDur` on the shared ease; the world-space slide keeps it outside the viewport until the arrival, so it is visibly sliding in while the camera lands (demo :724-727). Replaces the old post-landing `transDur × 0.5` variant and the `EnemyEntrancePending` flag.
3. **Enemy HUD stays visible in Result and slides UP out on the return** — visibility rule is Combat ∪ Result ∪ traveling; on Result→Shop it world-flies to `enemySlide` above its combat home while the camera descends (demo :727).
4. **Chrome + panels scroll with the page (symmetric)** — `ExitShop` skips the hide while `IsTransitioning` (they scroll away with the departing page), `ShopChrome.ShowIfActive` is ungated (both appear at travel start and scroll INTO view), and the world Avatar/HpPill mirrors hide during travels so the canvas HUD world-flight is the single shared copy. `HudViewportPin` is settled-shop-only (fix fallout).
5. **Result panel entrance** — `ResultStatsPanel.Build` fades the panel in over 200 ms (linear) + scales 0.92 → 1 over 240 ms on the shared ease (demo :361-385).
6. **Instant mid-arc cover** — the flip at `delay + transDur/2` is `SetFaceUp(false, animated:false, force:true)`: the back appears instantly at the apex (demo :692-694), no animated scaleX flip.
7. **Avatar + HP pill world flight, no scale change** — while traveling, the canvas HUD pieces are world-locked (`HudWorldFlight` world-point tween + per-frame re-projection) between the chrome mirror spots and the combat anchors, so the camera overshoot carries them like every other world object (demo :701-729).

### Emulation seam — 2026-10-03 handoff (no demo counterpart)

6. **Handoff scale window (2026-10-03)**: the canvas HUD eases between the combat scale and the mirror's `canvasShopScale` over the flight's opening/final 35% (`HandoffScaleWindow`, `Ease.OutQuad`). The demo needs no such tween (one shared element, same size both homes); our two-copy emulation (canvas HUD vs world prefab mirror) does. Not demo-visible: the flight path itself still carries no scale tween per fix 7.
