# Physical Deck Layout (Cascade / Arc Loop / Float Stack)

(Moved from AGENTS.md on 2026-09-05 during the size diet; that file keeps a condensed summary.)

- Selector: `deckLayoutMode` enum {Linear, Cascade, ArcLoop, FloatStack} — single source of truth.
- Cascade: front card (deck top) largest at the `physicalCardDeckPos` anchor; front sweeps up-left shrinking; tail hooks back tight. Shop unaffected. Legacy `xOffset/yOffset/zOffset` fields only serve the Linear fallback.
- `revealCardCountsAsDeckFront` (default `true`, cascade/arc only): the reveal-zone card holds the layout front slot, so revealing does not re-layout the deck; it slides one step on return. Source of truth: `GetCascadeDeckCount()`.
- All position math funnels through one seam: `DeckPositionCalculator.CalculatePositionAtIndex(...)`; every caller (layout, popup, slot-in, reveal, peel focus) inherits the active layout.
- **Peel deck focus**: `_focusSegmentCount` (focus index + 1) drives the layout seams via `GetLayoutDeckCount()` — focus card on the front slot at the anchor (max scale); peeled cards slide off-screen; restore = full re-layout. `deckFocusTargetPos` deprecated.
- `DeckCascadeLayout` (pure static, unit-testable): Bezier + arc-length math ported 1:1 from `docs/demo/CardArrangementDemo.html`; cached per `(deckCount, pxToWorld, Params)`.
- Cascade index mapping: `cascadeIndex = deckCount - 1 - unityIndex` (0 = front card = deck top). Z: `basePos.z - zOffset * index`.
- Per-index scale: `GetDeckScaleAtIndex(i)` = `physicalCardDeckSize` × layout scale; `cascadeScaleJitterWithCard` multiplies position jitter by the same scale.
- **Coverage normalization**: one stretch-only factor (cap `cascadeCoverageCap`) lets small decks reach the curve's hook region; large decks unaffected.
- EditMode coverage: `DeckCascadeLayoutTests.cs` / `DeckArcLoopLayoutTests.cs` / `DeckFloatStackLayoutTests.cs` (demo goldens).
- **Dynamic arc midpoint (replaces `showPos`)**: `useDynamicArcMidpoint` (default on) — deck-bound arcs take their midpoint from the layout walk at `arcMidpointCurveT` + `arcMidpointOffset` (`TryGetArcMidpointPosition`). Fallback: explicit `CardMoveConfig.arcMidpoint` > dynamic > `showPos`.
- **Arc Loop mode**: superellipse loop; slots by curvature-weighted arc length (w=0 = uniform); deck top = tilted loop's visual lowest point, deck bottom adjacent up the right; scale by screen height, z by depth rank; cards upright. PRD: `plans/plan-arc-loop-deck-layout-2026-08-12.md`.
- **Float Stack mode**: centered stack; slot 0 (lowest) drives reveal & big-shadow offsets. Math: `DeckFloatStackLayout.ComputeFrame`. PRD: `plans/plan-float-stack-center-scale-2026-08-15.md`; demo: `docs/demo/CardStackRevealDemo.html`.
- **Float Stack big shadow is follow-driven**: `BigShadowFollower` tracks the reveal card per frame; anti-light lift via `SetBigShadowLift` (ramped by `AttackAnimationManager` / `RecorderAnimationPlayer`). Enable/disable only (never destroy+re-add); self-destructs the shadow GO if the card dies mid-drive. Tunables: `plans/plan-float-stack-shadow-follow-2026-08-16.md`.
