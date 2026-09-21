# Phase Transition (Shop ↔ Combat Continuous World)

Implementation of `docs/demo/PhaseTransitionDemo.html` — plan `plans/plan-phase-transition-world-camera-2026-09-21.md`, shipped 2026-09-21.

## World model

ONE continuous world, two pages stacked vertically:

- **Shop page** at world origin (camera rig Y = 0, free wheel-scroll inside).
- **Combat page** one screen above: `combatPageY = shopPageY + pageH`, `pageH = 2 × Camera.orthographicSize` (12.12 world units at ortho 6.06).
- At scene load `PhaseTransitionDriver` shifts the combat world markers (`physicalCardDeckPos`, `physicalCardRevealPos`, `showPos`, `gravePosition`, `physicalCardNewTempCardPos`, `statusEffectConsumePos`, `deckFocusTargetPos` on `CombatUXManager`) by `+pageH`; all deck/reveal math inherits through `DeckPositionCalculator`. When the driver is unavailable the offset is removed symmetrically and the game behaves byte-for-byte like before the port.

Transitions = the camera rig (`Camera Man`, parent of Main Camera — MilkShake keeps owning the camera child's localPosition) tweens Y between the pages. The world never cuts.

## Orchestration: `PhaseTransitionDriver`

Runtime-built singleton (`RuntimeInitializeOnLoadMethod.AfterSceneLoad`); no scene objects. Wraps the existing `PhaseManager` enter/exit pipeline:

- **Shop → Combat** (trigger: the 离开商店 `PhysButton` in `ShopChrome`, or Space in `PhaseManager.Update`; the demo's drag-up gesture is NOT ported — user ruling 2026-09-21): block input, `ExitingShopPhase()` + `EnteringCombatPhase()` at travel start (combat content spawns a page above, off-screen), `BlockInput(driver)` holds `RevealCards` until landing, camera travels up, dummies fly (below), on landing unblock → `InstantiateAllPhysicalCards` + Start Card reveal, dummies destroyed, canvas UI unsuppressed, enemy HUD slides DOWN in (counter-direction).
- **Result phase**: no camera change (camera-space overlay). **Rule change**: player avatar + HP pill stay visible in Result (demo :27-28); enemy HUD still hides.
- **Result → Shop**: `AdvanceFromResultToShop()` at travel start (shop spawns off-screen below), camera travels down to the remembered shop scroll Y, avatar/HP pill glide back to the `ShopTopBarLayout` anchors, chrome reappears on landing (`ShopChrome.ShowIfActive` is gated by `IsTransitioning`).

Static flags other systems consume: `IsTransitioning`, `OwnsDeckCards`, `SuppressCombatCanvasUI`, `EnemyEntrancePending`, `Available`.

### Deck-card flight (demo `flyShared` cards branch)

The shop's player-deck physical cards survive `ClearSpawnedCards` (`OwnsDeckCards` makes the clear methods no-op; shelf cards also persist and simply scroll away) and are borrowed as flight dummies (`ShopUXManager.ReleasePlayerDeckCardsToDriver`). Each dummy: two-segment DOMove via `PhaseFlightPlanner.ArcApex` (midpoint + `cardArc`), delay `i × cardStagger`, `SetFaceUp(false, animated, force:true)` at `delay + transDur/2` (mid-arc flip to face-down; legal under the never-cover rule's shuffle bypass), scale to `physicalCardDeckSize`, dummy i lands on stack slot i above the deck anchor. The real combat physicals pop into the same slots face-down at landing, so destroying the dummies is invisible.

### HUD glides (canvas, camera-space)

`CombatIconPresenter` / `HPNumericDisplayHorizontal` replace their per-phase re-anchor snap with `DOAnchorPos`/`DOScale` tweens while `IsTransitioning` (shop top-bar anchors ↔ combat anchors). Enemy side (icon + pill) plays the slide-in on activation while `EnemyEntrancePending`. `CombatHPBarPresenter` stays hidden while `SuppressCombatCanvasUI`, then activates on release.

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
- **Input**: `ShopInputGate` + `CombatManager.BlockInput(driver)` paired across the travel; `PhaseManager` phase keys route through the driver first.

## Verified (2026-09-21 Play Mode)

- Shop→Combat→Result→Shop full loop ×2 (camera Y 0↔12.12, avatar/HP glide both directions, chrome hide/show on landing, shop respawn).
- Mid-flight (slowed to 6 s): overshoot peak rigY 13.31 > 12.12 at t≈3.3/6 s; dummies flip face-down at t≈3.0/6 s (= delay + transDur/2); stagger schedule.
- Result: player icon + player HP pill visible, enemy hidden.
- Headless bypass toggle both directions.
- EditMode: 660 tests green (`PhaseFlightPlannerTests` 7 goldens + pre-existing suite), 1 pre-existing skip.

## Deviations from the demo (accepted)

1. ❚❚ options button does not fly (no combat-side home exists today).
2. Combat→Shop uses the shop's existing spawn-pop entry; no return card flight.
3. Shop→combat drag-up gesture not ported (离开商店 button is the trigger).
4. Demo param bar → `PhaseTransitionConfigSO` asset instead of a runtime UI.
5. v1.1 backlog: world topo background spanning both pages (red band = enemy HP display), ❚❚ flight, combat→shop card return flight.
