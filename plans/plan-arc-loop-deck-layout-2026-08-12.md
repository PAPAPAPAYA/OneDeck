# Elliptical Arc Loop Deck Layout — PRD / Implementation Plan

Date: 2026-08-12
Status: Approved design (doc only; implementation pending)
Interactive reference: `docs/demo/CardArcArrangementDemo.html` (validated in browser, incl. reveal-conveyor simulation)
Scope: combat-phase physical deck layout only. Shop layout, card logic/effects, animation request pipeline are untouched.

## 1. Overview

Add a third combat deck layout mode, the **Elliptical Arc Loop**, coexisting with the
legacy linear fan and the Smooth Curve Cascade:

- All deck slots sit on one full superellipse loop, distributed by **curvature-weighted
  arc length** (bends pack denser; w = 0 degrades to uniform arc length).
- The deck top (next card to reveal) is the visually **lowest, front-most** card of the
  loop; the deck bottom (`combinedDeckZone[0]`) is the slot adjacent to it up the right
  side. Revealing = conveyor: pop the bottom slot, insert at the deck-bottom slot, every
  card advances one slot — structurally identical to today's reveal-to-bottom flow.
- The whole loop can be **rigidly tilted** in the screen plane (left half lower, right
  half higher) without re-anchoring; the deck-top slot is re-pinned to the tilted loop's
  visual lowest point. Cards themselves stay upright.
- The mode is selected by a new Inspector toggle; when the toggle is OFF, behavior is
  byte-identical to today.

## 2. Demo Reference

`docs/demo/CardArcArrangementDemo.html` is the single source of truth for the math and
the intended look. Port 1:1, the same way `DeckCascadeLayout` ported
`docs/demo/CardArrangementDemo.html`. The demo also simulates the reveal conveyor
("模拟揭示" button) and exposes every parameter below as a slider.

## 3. Layout Semantics (spec settled in the demo)

### 3.1 Loop geometry

- Base point (untilted, demo canvas coords y-down):
  `base(α) = (side · radiusX · spow(sin α, 2/e), radiusY · spow(cos α, 2/e))`,
  `spow(v,p) = sign(v)·|v|^p`. α = 0 is the loop bottom; increasing α climbs the right
  side first. `side = mirror ? −1 : +1`. `e = 2` is a true ellipse; `e > 2` is squarer.
- Tilt: rigid rotation `(x·cos φ + y·sin φ, −x·sin φ + y·cos φ)`, φ = tiltDeg.
  Positive φ = left half lower, right half higher. No re-anchoring translation.

### 3.2 Slot distribution (curvature-weighted arc length)

- Sample the untilted loop at M = 720 points (closing segment included). Rotation
  (tilt) preserves arc length and curvature, so the table is tilt-independent.
- Per segment: chord length `len_i`; per sample: Menger curvature
  `κ_i = 2·|cross(ab, bc)| / (|ab|·|bc|·|ca|)` (wraparound neighbors);
  `κ̄ = Σ(κ_i·len_i) / L` (≈ 2π/L for a convex loop).
- Density `ρ = 1 + w·κ/κ̄`; cumulative weighted length `cumDensity` by trapezoid;
  total `D`. Slot step = `D / N`.
- Deck-top anchor: `bottomD = cumDensity[argmax(tilted y over samples)]` — the visual
  lowest point of the tilted loop.
- Slot k (unity deck index k, 0 = deck bottom, N−1 = deck top) sits at weighted
  coordinate `(bottomD + (k+1)/N · D) mod D` → binary-search the segment, lerp, tilt.
- Deck top lands exactly on the visual lowest point by construction.

### 3.3 Scale & z-order (visual depth)

- Scale by screen height: `depthT = (yMax − y_k) / (yMax − yMin)` over the N slots;
  `falloff = 1 − (1 − depthT)^scalePower`; `scale = 1 − (1 − minScale) · falloff`.
  Lowest (front-most) slot = 1.0, highest = minScale. Same "height = depth" source as z.
- Depth rank: sort slots by (canvas y asc, then lower index first); rank 0 = back-most,
  N−1 = front-most (always the deck top).
- z position: `z = basePos.z − zOffset · depthRank`. Front-most card subtracts
  `zOffset·(N−1)`, exactly matching the analytic front-z in
  `CombatUXManager.GetRevealZonePosition` (`CombatUXManager.cs:1546-1547`), so reveal
  occlusion clamping stays exact.

### 3.4 Anchor & coordinate conversion

- Offsets are relative to the deck-top slot: deck top = `Vector2.zero` at the anchor,
  same convention as cascade's front card. `physicalCardDeckPos` remains the front-card
  anchor; the loop center floats above it.
- Canvas → Unity: compute everything in demo canvas coords (y-down) 1:1, then
  `unityOffset = (canvas_dx, −canvas_dy) · pxToWorld`. The deck top sits below the loop
  center; the rest of the ring rises above the anchor.

## 4. Parameters & Defaults

Defaults from the final demo tuning (user-confirmed screenshot 2026-08-12):

| Param | Default | Range (Inspector) | Meaning |
|---|---|---|---|
| enableArcLoopDeckLayout | false | bool | Mode toggle (OFF = current behavior unchanged) |
| arcLoopPxToWorld | 0.01 | float | Demo px → world unit conversion |
| arcLoopRadiusX | 195 | float | Loop horizontal radius (demo px) |
| arcLoopRadiusY | 155 | float | Loop vertical radius (demo px) |
| arcLoopExponent | 2.0 | 1.5–5 | Superellipse exponent (2 = ellipse, higher = squarer) |
| arcLoopTiltDeg | +45 | −45..45 | Rigid in-plane tilt; positive = left lower, right higher |
| arcLoopCurveDensity | 3.0 | 0–3 | Curvature weight w (0 = uniform arc length) |
| arcLoopMinScale | 0.70 | 0.3–1 | Scale of the highest/back-most card |
| arcLoopScalePower | 1.0 | 0.5–3 | Height-scale falloff steepness |
| arcLoopMirror | false | bool | Deck bottom left of deck top (mirrored loop) |
| arcLoopSamples | 720 | int | Arc-length/curvature table resolution |

`physicalCardDeckSize` stays the base card size (the demo's "card scale" slider).
Demo "card count" maps to the live deck size; nothing to serialize.

## 5. Coexistence & Toggle Design

- `CombatUXManager` keeps `enableCascadeDeckLayout` as the master shaped-layout switch
  (false = legacy linear fan, byte-identical). New bool `enableArcLoopDeckLayout`
  selects Arc Loop over Cascade when the master is on.
- Effective mode: `!enableCascadeDeckLayout` → Linear; `enableArcLoopDeckLayout` → Arc
  Loop; otherwise → Cascade. Resolved in one helper (`IsArcLoopLayoutActive`).
- All count seams are layout-agnostic and are reused unchanged:
  `GetCascadeDeckCount()` (+1 reveal-slot reservation, `CombatUXManager.cs:1021`),
  `GetLayoutDeckCount()` (peel-focus segment count, `CombatUXManager.cs:1034`),
  `MoveRevealedCardToBottom` effectiveCount (`CombatUXManager.cs:424-430`).
  In Arc Loop mode the reveal-zone card reserves the loop-bottom slot, the same
  "reveal card counts as deck front" semantics as cascade: revealing does not move
  the rest of the deck; the returned card lands at the deck-bottom slot.
- Peel focus: the segment count flows through the same seams; the focus card (unity
  index focusCount−1) maps to the sub-loop's deck-top slot = anchor at scale 1,
  matching today's "focus card on cascade front slot" behavior.

## 6. Unity Integration Design

### 6.1 New file `Assets/Scripts/UXPrototype/DeckArcLoopLayout.cs`

Pure static helper mirroring `DeckCascadeLayout` structure (English comments, CRLF,
tabs, cache-last-result pattern):

```csharp
public static class DeckArcLoopLayout
{
	[System.Serializable]
	public struct Params // cache key, like cascade Params
	{
		public float radiusX; public float radiusY; public float exponent;
		public float tiltDeg; public float curveDensity;
		public float minScale; public float scalePower;
		public bool mirror; public int arcSamples;
	}

	public sealed class Slot // indexed by UNITY index (0 = deck bottom, count-1 = deck top)
	{
		public Vector2 offset;    // relative to deck-top slot, Unity units (pre-anchor)
		public float scale;       // height-normalized depth scale (deck top = 1)
		public int depthRank;     // 0 = back-most, count-1 = front-most (deck top)
	}

	public static Slot[] ComputeSlots(int deckCount, Params p, float pxToWorld);
	public static float ComputeScale(int unityIndex, int deckCount, Params p, float pxToWorld);
	public static Vector2 ComputeOffsetAtCurveT(int deckCount, float t, Params p, float pxToWorld);
	// t in [0,1] walks from deck top (0) around the loop (1); lerp between bracketing
	// slot offsets. Used by the dynamic arc-midpoint seam (same contract as cascade).
}
```

Internals: loop table (points, cumDensity, totalDensity) cached per
`(Params, pxToWorld)` — count-independent; `Slot[]` cached per
`(deckCount, Params, pxToWorld)`.

### 6.2 `Assets/Scripts/UXPrototype/DeckPositionCalculator.cs`

- New carrier `public sealed class ArcLoopConfig { public bool enabled; public DeckArcLoopLayout.Params layoutParams; public float pxToWorld; }`.
- `CalculatePositionAtIndex(...)` gains optional trailing param `ArcLoopConfig arcLoop = null`.
  Branch order: Arc Loop (when `arcLoop.enabled` and deckCount > 0) → Cascade → Linear.
  Arc branch: `Slot s = DeckArcLoopLayout.ComputeSlots(deckCount, arcLoop.layoutParams, arcLoop.pxToWorld)[Mathf.Clamp(index, 0, deckCount-1)]`;
  return `new Vector3(basePos.x + s.offset.x, basePos.y + s.offset.y, basePos.z - zOffset * s.depthRank)`.

### 6.3 `Assets/Scripts/UXPrototype/CombatUXManager.cs`

- New `[Header("ARC LOOP DECK LAYOUT")]` serialized fields per section 4 table.
- `BuildArcLoopLayoutParams()` + `BuildArcLoopConfig()` mirroring
  `BuildCascadeLayoutParams()` (`CombatUXManager.cs:973`) / `BuildCascadeConfig()`
  (`CombatUXManager.cs:998`).
- Pass `BuildArcLoopConfig()` at the three calculator call sites:
  `CalculatePositionAtIndex` (`:1100`), `CalculateAnimationPositionAtIndex` (`:1126`),
  `MoveRevealedCardToBottom` (`:439`).
- `GetDeckScaleAtIndex(unityIndex, count)` (`:1054`) and `GetCascadeJitterScale` (`:1079`):
  arc branch first (`DeckArcLoopLayout.ComputeScale`), else existing logic.
- `TryGetArcMidpointPosition` (`:758`) and `OnDrawGizmosSelected` (`:799`): arc branch
  using `DeckArcLoopLayout.ComputeOffsetAtCurveT` (offsets already deck-top-relative;
  tilt/mirror live inside Params, no direction mirror needed).

### 6.4 Explicitly untouched

- `GetRevealZonePosition` z clamp (analytic formula identical, see 3.3).
- Reveal/shuffle/batch-move/popup/slot-in animations (they consume the layout seams).
- Face-down/flip system, `ICombatVisuals` surface, `RecorderAnimationPlayer`,
  `ApplyAnimationResult` ordering.
- Shop phase (`ShopUXManager`) and legacy linear/cascade paths.
- No scene/prefab edits required; all defaults are code-side (Inspector tuning optional).

## 7. Edge Cases

- **deckCount 0 / 1**: empty slot array / single zero-offset slot at scale 1
  (mirrors cascade edge behavior, see `DeckCascadeLayoutTests.cs:75-80`).
- **deckCount 2**: slots at the visual bottom and the diametrically opposite point.
- **Odd N**: the two slots flanking the deck top are asymmetric by construction
  (deck bottom is one weighted step away) — accepted, demo-validated.
- **Large N (> ~20)**: adjacent slots near the loop bottom crowd together (angular
  step shrinks); mitigate by enlarging radiusX/Y or lowering curveDensity. Documented
  in the demo's eval notes.
- **tilt = ±45°**: bottomD tracking stays exact (sampled argmax over 720 points).
- **w = 0**: consecutive slot distances are equal (uniform arc length) — used by tests.
- **Reveal-card slot reservation**: with a card in the reveal zone, the deck's top
  physical card sits one loop step above the (reserved) bottom slot — same as cascade.

## 8. Test Plan

### 8.1 EditMode — new `Assets/Scripts/Editor/Tests/DeckArcLoopLayoutTests.cs`

Template: `DeckCascadeLayoutTests.cs`. Cases:

1. **Golden positions**: 8 cards, section-4 defaults. Goldens generated from the demo
   page (evaluate `slotPosition(k, 8, loopBottomDensityCoord())` for k = 0..7 with the
   same params, canvas coords), converted by the rule in 3.4, tolerance 1e-3.
2. Deck-top offset == `Vector2.zero`; deck-bottom slot right of deck top (`offset.x > 0`,
   mirror off).
3. w = 0 ⇒ consecutive slot distances equal (uniform arc length, tolerance 1e-2).
4. Scale: deck top == 1; non-increasing with depth rank; min slot scale ≥ minScale − ε.
5. `depthRank` is a permutation of 0..N−1; deck-top rank == N−1.
6. Edge cases: deckCount 0 (empty), 1 (single zero slot, scale 1), 2.
7. `ComputeOffsetAtCurveT`: t=0 == deck-top offset, clamps outside [0,1].

### 8.2 Manual Play-Mode checklist (Combat scene)

- Toggle `enableArcLoopDeckLayout` on/off → deck re-lays out on the next layout update.
- Reveal + second-click return-to-bottom lands on the loop bottom slot; the deck does
  not shift on reveal (reserved front slot).
- Bury/Stage arcs use the arc-loop dynamic midpoint; gizmo renders it.
- Peel focus puts the focus card at the anchor (scale 1).
- Reveal-zone occlusion: reveal card stays in front of the loop's front card at 30+ cards.
- Shop phase unchanged.

### 8.3 Regression

- Existing `DeckCascadeLayoutTests` and the full EditMode suite stay green.
- With the toggle OFF (default), behavior is byte-identical to today.

## 9. Implementation Milestones (for the later code step)

1. `DeckArcLoopLayout.cs` (port table + slots 1:1 from the demo JS).
2. `DeckPositionCalculator`: `ArcLoopConfig` + branch.
3. `CombatUXManager`: fields, config builders, arc branches (scale/jitter/midpoint/gizmo).
4. `DeckArcLoopLayoutTests.cs` with demo-generated goldens; run EditMode suite.
5. Manual Play-Mode checklist (8.2).
6. `AGENTS.md` update (Physical Deck Layout paragraph + Key Files entry) — note:
   AGENTS.md has < 1 KB headroom against the 32 KB limit at the time of writing;
   keep the addition compact and run `wc -c AGENTS.md` afterwards.

## 10. Notes

- New feature, not a visual bug fix: no `VISUAL-FIX` comment block and no
  `docs/RegressionChecklist.md` row required by `docs/VisualBugPrevention_Guide.md`.
- If the arc mode later becomes the default, scene serialization keeps working:
  the toggle is a plain serialized bool on `CombatUXManager` (default false).
