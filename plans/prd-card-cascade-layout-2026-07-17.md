# PRD: Card Cascade Deck Layout

> **v2 (2026-07-17)** — revised after lead-programmer review. See section 10 for the change log. Key corrections: the layout integration seam is `DeckPositionCalculator`, not the two `CombatUXManager` wrappers; per-index scale requires 8 call sites beyond `UpdateAllPhysicalCardTargets`; the canvas-to-Unity Y-flip is now an explicit port step.

## 1. Overview

### 1.1 Background

The current combat deck layout is a linear fan: `DeckPositionCalculator.CalculatePositionAtIndex` spaces cards with constant `xOffset`/`yOffset` per index and a uniform scale (`physicalCardDeckSize`). This reads as a flat, mechanical stack.

A layout exploration was conducted in `docs/demo/CardArrangementDemo.html` (currently untracked; this feature adds it to git, see section 4), which iterated through three target shapes:

1. **v1 — Diagonal Cascade Stack**: pure diagonal, exponential shrink.
2. **v2 — Polyline Cascade Stack**: front segment up-left with shrinking, then a hard turn up-right at minimum spacing ("hook / L-shape").
3. **Final — Smooth Curve Cascade Stack**: the polyline replaced by a smooth quadratic Bezier arc (current html content).

The demo compared four implementation strategies:

| Method | Unity equivalent | Verdict |
|--------|------------------|---------|
| A. Absolute position + per-index transform | Script-computed `localPosition`/`localScale` | **SELECTED (locked 2026-07-17)** |
| B. Container stack + transform | Parent object as layout anchor | Rejected |
| C. Canvas 2D draw | RenderTexture / dynamic mesh | Rejected (incompatible with `CardPhysObjScript` animation system) |
| D. WebGL/Three.js perspective | True 3D perspective camera | Rejected (conflicts with 2D UI animation system) |

### 1.2 Goal

Replace the linear-fan combat deck layout with the **Smooth Curve Cascade Stack** from the demo, using Method A:

- Front card (deck top, first revealed) is largest and sits at the anchor (bottom-right).
- Front segment expands up-left along a smooth curve while size and spacing shrink progressively.
- After the turning point, the curve bends up-right; size stabilizes and spacing stays at minimum.
- The result is a continuous arc of depth, not a hard polyline.

### 1.3 Design Rationale

- **Unified layout core**: every deck position in combat ultimately flows through `DeckPositionCalculator.CalculatePositionAtIndex` — either via the three `CombatUXManager` wrappers (`CalculatePositionAtIndex`, `CalculateAnimationPositionAtIndex`, `CalculatePositionForPendingCard`) or via three raw-math call sites that duplicate the formula inline (`MoveRevealedCardToBottom`, `StartPeelCoroutine`, `TransitionFocusCoroutine`). The cascade branch is implemented **once** inside the calculator; the three raw-math sites are rerouted through it (full inventory in 3.3). After this reroute, every consumer (shuffle, focus/peel, pop-up peaks, slot-in, reveal entry, reveal-to-bottom, `MoveCardToIndex`) inherits the curve.
- **Per-index scale is a separate cross-cutting concern**: position is centralized, scale is not. Eight call sites outside `UpdateAllPhysicalCardTargets` force the uniform `physicalCardDeckSize` (full inventory in 3.4). A new `GetDeckScaleAtIndex(unityIndex)` helper is applied at all of them, including one in `AttackAnimationManager`.
- **Pure math port**: the Bezier + arc-length parameterization is pure math. Porting it 1:1 into a static helper keeps it unit-testable (golden-value EditMode tests, section 6.1) and free of scene dependencies.
- **No animation-system intrusion**: cards still animate through `CardPhysObjScript.SetTargetPosition/SetTargetScale`. DOTween, `CombatAnimationSpeed.SpeedScale`, `ApplyAnimationResult`, and `RecorderAnimationPlayer` are untouched.
- **Reversible**: a config flag `enableCascadeDeckLayout` falls back to the legacy linear layout byte-for-byte.

### 1.4 Source Documents

| Document | Role |
|----------|------|
| `docs/demo/CardArrangementDemo.html` (`computeSmoothPositions`, `getCascadeParams`) | Algorithm source of truth |
| `plans/plan-card-cascade-layout-2026-07-17.md` | Working plan this PRD supersedes |

---

## 2. Scope

### 2.1 In Scope

- New static helper `Assets/Scripts/UXPrototype/DeckCascadeLayout.cs` (pure math).
- `Assets/Scripts/UXPrototype/DeckPositionCalculator.cs`: cascade branch + cascade config carrier (3.3).
- `Assets/Scripts/UXPrototype/CombatUXManager.cs`: serialized cascade parameters + flag; reroute 3 wrappers + 3 raw-math position sites onto the calculator (3.3); `GetDeckScaleAtIndex` helper + 8 call sites (3.4); scale-aware jitter consolidation (3.5).
- `Assets/Scripts/Managers/AttackAnimationManager.cs`: one scale call site at attack return-to-deck (3.4 #8).
- `Assets/Scripts/Editor/Tests/DeckCascadeLayoutTests.cs`: golden-value EditMode tests (6.1).
- `Assets/Scripts/UXPrototype/CardPhysObjScript.cs`: fix the stale `isPendingSlotIn` XML comment (still says "exclude pending cards from active deck count"; the code has used FULL count since VISUAL-FIX 2026-05-24). One comment block only, no logic change.
- Combat physical deck only (`physicalCardsInDeck`).
- `docs/RegressionChecklist.md` rows and `VISUAL-FIX` comment blocks per project rules.
- `AGENTS.md` deck layout description update.
- Add `docs/demo/CardArrangementDemo.html` to git (no content change) — it is the algorithm source of truth and must not remain a working-tree-only file.

### 2.2 Out of Scope

- Shop (`ShopUXManager`) — untouched. Verified: it has its own `xOffset`/`yOffset` layout math and never calls the combat layout functions.
- The demo's "hover expand" interaction — NOT ported (deck peel/focus already covers inspection).
- `RecorderAnimationPlayer`, `EffectRecorder`, effect components, card prefabs, `GameEvent` assets — no changes.
- `ApplyAnimationResult` deck-order logic — no changes.
- Play Mode execution of the section-6.2 regression tests — performed manually by the project owner.

---

## 3. Technical Design

### 3.1 Algorithm Port — `DeckCascadeLayout`

New pure static helper next to `DeckPositionCalculator`. No Unity scene dependencies beyond `Vector2`/`Vector3`.

```csharp
public enum CascadeTailBend { Mirror, Same } // Mirror = bend toward opposite side (demo), Same = keep front direction

public static class DeckCascadeLayout
{
	public struct Params
	{
		public int shrinkCount;    // front segment length (demo: 6)
		public float minScale;     // smallest card scale (demo: 0.55)
		public float scalePower;   // scale falloff steepness (demo: 2)
		public float startSpacingX, startSpacingY; // front spacing (demo: 60, 70)
		public float minSpacingX, minSpacingY;     // tail spacing (demo: 8, 12)
		public float spacingPower; // spacing falloff steepness (demo: 2)
		public float tailReturn;   // demo "curveWidth": 0 = straight peak, 1 = strong right return (demo: 0.55)
		public float tailBendSign; // +1 = bend toward mirror side (demo), -1 = keep front direction
		public int arcSamples;     // Bezier sampling density (demo: 300)
	}

	// cascadeIndex 0 = front card at anchor; cascadeIndex deckCount-1 = deepest card.
	// Returned offsets are in UNITY world space (y-up), scaled by pxToWorld, BEFORE the
	// cascadeDirection mirror (canonical: front up-RIGHT; the caller applies the
	// per-component direction sign, see 3.6 — default (-1,+1) reproduces the demo).
	public static Vector2[] ComputeOffsets(int deckCount, Params p, float pxToWorld);
	public static float ComputeScale(int cascadeIndex, int deckCount, Params p);
}
```

Port steps (mirroring the demo 1:1; formula transcription verified 2026-07-17 against `computeSmoothPositions` / `getCascadeParams`):

1. **Edge guards**: `deckCount <= 0` returns an empty array; `deckCount == 1` returns a single zero offset with scale 1 (guards the `t = i/(deckCount-1)` division, same as the demo's `Math.max(1, N-1)`); clamp `shrinkCount` to `deckCount - 1` so the peak stays inside the curve.
2. Bezier control points (demo canvas space, y-down):
	- `P0 = (0,0)` — front card at anchor.
	- `P1 = (-peakX, -peakY)`, `peak = startSpacing * shrinkCount * 0.85` — pulls up-left.
	- `P2`: `tailX = -peakX*(1-tailReturn) + tailBendSign * minSpacingX*(deckCount-shrinkCount)*tailReturn*0.6`, `tailY = -peakY - minSpacingY*(deckCount-shrinkCount)`.
3. Sample the curve `arcSamples` (300) times; build the cumulative arc-length table.
4. Walk cards along the curve: per-card step length = `|lerp(startSpacing, minSpacing, easeOutPower(t, spacingPower))| * 0.5` with `t = i/(deckCount-1)`; look up the arc-length table for each position.
5. Scale: `1 - (1-minScale) * easeOutPower(t, scalePower)`.
6. **Canvas-to-Unity flip (explicit)**: the demo runs in canvas coordinates (y grows downward, so negative offsetY = up); Unity world space is y-up. Negate BOTH axes of every offset after step 4, so the canonical Unity curve reads front up-RIGHT / tail hooking left. The `cascadeDirection` mirror (3.6) is applied by the caller AFTER this flip — with the default direction `(-1, +1)` (mirror X) the result reproduces the demo shape exactly (front up-left, tail hooking right). Never fold the flip into `cascadeDirection`, or the two transforms become order-dependent and the curve inverts. (The demo's own Three.js variant confirms a flip is required: `position.set(offsetX*k, -offsetY*k, z)`.)
7. Multiply both components by `pxToWorld` (demo px -> world units).
8. Cache the last result keyed by `(deckCount, Params, pxToWorld)` so per-index callers do not recompute the whole curve per card.

### 3.2 Index Mapping

- Demo `cascadeIndex 0` = front card = Unity deck top = `physicalCardsInDeck[count-1]` (first revealed).
- Mapping: `cascadeIndex = deckCount - 1 - unityIndex`.
- `physicalCardDeckPos.position + _deckFocusOffset` becomes the FRONT (top card) anchor; the curve extends away from it. This matches the legacy convention where the top card (index count-1) sits at zero offset.
- Z depth keeps the existing formula `basePos.z - zOffset * index` so render sorting is unchanged (verified: top card keeps smallest z = frontmost).

### 3.3 Integration Seam — `DeckPositionCalculator` + Call-Site Reroute

The cascade branch lives in ONE place: `DeckPositionCalculator.CalculatePositionAtIndex`, extended with a cascade config carrier. When the config is null/disabled, the legacy formula runs byte-for-byte.

```csharp
public sealed class CascadeConfig // built by CombatUXManager from its serialized fields
{
	public bool enabled;
	public DeckCascadeLayout.Params layoutParams;
	public float pxToWorld;
	public Vector2 direction;          // consumed as per-component sign only (3.6)
	public CascadeTailBend tailBend;
}

public static Vector3 CalculatePositionAtIndex(
	int index, int deckCount, Vector3 basePos,
	float xOffset, float yOffset, float zOffset,
	CascadeConfig cascade = null)
```

Position call-site inventory (verified 2026-07-17). Sites 1-3 already delegate; sites 4-6 currently duplicate the linear formula inline and MUST be rerouted — PRD v1 missed them.

| # | Site (`CombatUXManager.cs` unless noted) | Current form | Change |
|---|------------------------------------------|--------------|--------|
| 1 | `CalculatePositionAtIndex` (~:747) | delegates to calculator | pass cascade config |
| 2 | `CalculateAnimationPositionAtIndex` (~:771) | delegates, full count (VISUAL-FIX 2026-05-24) | pass cascade config |
| 3 | `CalculatePositionForPendingCard` (~:842) | delegates, full count; used by `SlotInCard` (~:2753) and `MoveCardToPopUpPosition` (~:2817) | pass cascade config |
| 4 | `MoveRevealedCardToBottom` (~:354-358) | RAW: `deckPos + xOffset*(effectiveCount-1)` | replace with calculator call: `index 0`, `count = effectiveCount`, `basePos = physicalCardDeckPos.position` (no focus offset) |
| 5 | `StartPeelCoroutine` (~:1451-1452) | RAW: computes `noOffsetX/Y` for `_deckFocusOffset` | replace with calculator call at `targetIndex` (basePos without focus offset); then `offsetX = desiredX - noOffsetPos.x`, `offsetY = deckPosY - noOffsetPos.y` as today |
| 6 | `TransitionFocusCoroutine` (~:1558-1559) | RAW: same as #5 | same as #5 |

Zero-change consumers after the reroute (each reaches deck positions only through the six sites above): `GetFinalDeckPositionForCard`, `UpdateAllPhysicalCardTargets`, `CalculateShuffleTargets`, `MoveCardWithAnimation` targets, pop-up peaks (`PopUpCard`, `MoveCardToPopUpPosition`, batch peak in `MoveCardToTopPopUpBatch`), slot-in targets, peel card targets, and `AttackAnimationManager`'s return-to-deck position (`CalculatePositionAtIndex` at ~:325/:358). Keep the existing `TestManager.Log` lines in the wrappers.

### 3.4 Per-Index Scale — `GetDeckScaleAtIndex` + Full Call-Site Inventory

New helper on `CombatUXManager`:

```csharp
public Vector3 GetDeckScaleAtIndex(int unityIndex)
{
	if (!enableCascadeDeckLayout) return physicalCardDeckSize;
	int count = physicalCardsInDeck.Count;
	int cascadeIndex = count - 1 - unityIndex;
	return physicalCardDeckSize * DeckCascadeLayout.ComputeScale(cascadeIndex, count, cascadeLayoutParams);
}
```

(`physicalCardDeckSize` is a `Vector3`; multiply by the scalar cascade scale. Pending cards are included via the FULL deck count, matching the `CalculateAnimationPositionAtIndex` convention, so peak/slot-in scales agree with the final layout.)

Scale call-site inventory (verified 2026-07-17) — every place that currently forces the uniform `physicalCardDeckSize` on a deck-bound card. PRD v1 covered only #1.

| # | Site | Context | Change |
|---|------|---------|--------|
| 1 | `UpdateAllPhysicalCardTargets` (~:1153) | global layout update | `SetTargetScale(GetDeckScaleAtIndex(i))` |
| 2 | `MoveCardWithAnimation` (~:512-516) | single moves: Bury/Stage/Delay (`ToTop`/`ToBottom`/`ToIndex`) | `targetScale = GetDeckScaleAtIndex(target deck index)` for deck-bound move types; `cardDestroyTargetSize` unchanged for `ToGrave` |
| 3 | Batch slot-in (~:719, ~:726) | `MoveCardToTopPopUpBatch` phase 2 | per-card `GetDeckScaleAtIndex(finalIndex)` in both the `DOScale` and the completion `SetTargetScale` |
| 4 | Shuffle (~:1020, ~:1030) | `PlayShuffleAnimationInternal` | per-card `GetDeckScaleAtIndex(i)` for the shuffled index `i`, in both the `DOScale` and the completion `SetTargetScale` |
| 5 | `SlotInCard` (~:2764, ~:2772) | single slot-in | `GetDeckScaleAtIndex(deckIndex)` |
| 6 | `AddPhysicalCardToDeck` (~:2019) | new temp card auto-tween target | `GetDeckScaleAtIndex(i)` at the card's final index |
| 7 | `InstantiateAllPhysicalCards` (~:2079 + init loop ~:2086-2095) | combat-start spawn | `SetScaleImmediate(GetDeckScaleAtIndex(i))` inside the init loop (immediate, not tweened) so the deck never opens with a uniform-scale frame |
| 8 | `AttackAnimationManager.cs` (~:360) | attack return-to-deck target scale | `_combatUXManager.GetDeckScaleAtIndex(deckIndex)` |

Intentionally UNCHANGED (uniform by design):

- Pop-up peak scale (`physicalCardDeckSize * popUpScaleMultiplier`, ~:661/:2663/:2820): pop-up is an emphasis pose; a uniform peak keeps it readable and identical across depths. The card grows from its cascade scale to the peak scale, which reads as a focus zoom.
- Reveal-zone scale (`physicalCardRevealSize`).
- `SlotInCard`'s reveal-zone fallback path restores `popUpOriginalScale` — unchanged, it is depth-agnostic.

Without sites 2-8, every deck-bound animation would stomp the per-index scale back to uniform mid-playback and the card would visibly "breathe" when the next `UpdateAllPhysicalCardTargets` re-applies the cascade scale.

### 3.5 Random Jitter (`_deckOffsetProvider`)

Jitter is a pure additive overlay applied AFTER the base layout. Amplitude is +/-0.05 world units: invisible at front spacing (~0.6-0.7 units) but ~50% of tail spacing (~0.08-0.12 units), so the tail looks ragged without compensation.

Jitter application sites (verified 2026-07-17): `GetFinalDeckPositionForCard` (~:800), batch slot-in (~:713), `CalculateShuffleTargets` (~:931), `SlotInCard` (~:2754), `MoveRevealedCardToBottom` (~:367). This is NOT a one-line change as v1 claimed.

Consolidation (do this first): since all position calculators now use the FULL deck count, the batch slot-in, `CalculateShuffleTargets`, and `SlotInCard` sites can call `GetFinalDeckPositionForCard(physScript, index)` instead of "calculator + raw `GetPositionOffset`" — collapsing position-jitter application to two places (`GetFinalDeckPositionForCard`, plus the special `effectiveCount` path in `MoveRevealedCardToBottom`).

Scale-aware jitter (then one line per place):

```csharp
float jitterScale = (enableCascadeDeckLayout && cascadeScaleJitterWithCard) ? cascadeScaleAt(unityIndex) : 1f;
finalPos = basePos + _deckOffsetProvider.GetPositionOffset(physScript) * jitterScale;
```

Rotation jitter is scale-independent and stays as-is. The existing serialized ranges (`randomDeckPositionOffsetRange` / `randomDeckRotationOffsetRange`, zero = off) stay the single control point; `cascadeScaleJitterWithCard = true` is added per 3.7.

### 3.6 Direction & Tail Bend Configuration

Transform order is fixed and MUST be implemented in this sequence:

1. Compute the curve in demo canvas space (y-down), with the P2 tail-return term multiplied by `tailBendSign` (`+1` = Mirror / demo behavior, `-1` = Same / tail keeps the front direction).
2. Negate BOTH axes to convert to Unity world space (3.1 step 6): the canonical curve reads front up-RIGHT / tail hooking left.
3. Mirror the final offsets per-component by `sign(cascadeDirection.x)` / `sign(cascadeDirection.y)`.

`cascadeDirection` is consumed as a per-component sign only; its magnitude is meaningless. Default `(-1, +1)` = front up-left. No caller is affected. Both knobs are serialized on `CombatUXManager` (3.7).

### 3.7 Serialized Parameters

```csharp
[Header("Cascade Deck Layout")]
public bool enableCascadeDeckLayout = true;
public float cascadePxToWorld = 0.01f; // tune so 150px card ~= current physical card width
public int cascadeShrinkCount = 6;
public float cascadeMinScale = 0.55f;
public float cascadeScalePower = 2f;
public Vector2 cascadeStartSpacing = new Vector2(60f, 70f);
public Vector2 cascadeMinSpacing = new Vector2(8f, 12f);
public float cascadeSpacingPower = 2f;
[Range(0f, 1f)] public float cascadeTailReturn = 0.55f;
public Vector2 cascadeDirection = new Vector2(-1f, 1f); // per-component sign only; up-left by default
public CascadeTailBend cascadeTailBend = CascadeTailBend.Mirror;
public bool cascadeScaleJitterWithCard = true; // see 3.5
```

Legacy `xOffset`/`yOffset`/`zOffset` fields stay for the fallback path.

### 3.8 Unaffected Systems (verify, do not change)

- `ApplyAnimationResult` deck-order advancement and `RecorderAnimationPlayer` parallel tween flow.
- `CombatAnimationSpeed.SpeedScale` (applied inside `CardPhysObjScript` during Combat phase only).
- Reveal-zone card position/scale (`physicalCardRevealPos`, `physicalCardRevealSize`).
- Deck focus translate mechanism: `_deckFocusOffset` remains additive on the anchor; only its computed VALUE changes (derived from the cascade position of the focused card via site 3.3 #5/#6, so the focused card still lands on `deckFocusTargetPos`).
- `AttackAnimationManager` position logic — it already calls `CalculatePositionAtIndex`; only its one scale line changes (3.4 #8).

---

## 4. Files Changed

| # | File | Action | Estimate |
|---|------|--------|----------|
| 1 | `Assets/Scripts/UXPrototype/DeckCascadeLayout.cs` | New pure static layout helper (3.1) | ~120 lines |
| 2 | `Assets/Scripts/UXPrototype/DeckPositionCalculator.cs` | Cascade branch + `CascadeConfig` carrier (3.3) | ~40 lines |
| 3 | `Assets/Scripts/UXPrototype/CombatUXManager.cs` | Serialized params + flag, 6 position call-site reroutes (3.3), `GetDeckScaleAtIndex` + 7 call sites (3.4 #1-7), jitter consolidation + scaling (3.5) | ~150 lines |
| 4 | `Assets/Scripts/Managers/AttackAnimationManager.cs` | Scale helper at return-to-deck (3.4 #8) | ~2 lines |
| 4b | `Assets/Scripts/UXPrototype/CardMoveConfig.cs` | `targetScaleOverride` field for `ToPosition` moves (3.4 #2 support) | 1 line |
| 5 | `Assets/Scripts/Editor/Tests/DeckCascadeLayoutTests.cs` | Golden-value + edge EditMode tests (6.1) | ~80 lines |
| 6 | `Assets/Scripts/UXPrototype/CardPhysObjScript.cs` | Fix stale `isPendingSlotIn` XML comment (2.1) | 1 comment block |
| 7 | `docs/RegressionChecklist.md` | One row per section-6.2 scenario | 12 rows |
| 8 | `AGENTS.md` | Update deck layout description (Zones / Card Movement) | ~5 lines |
| 9 | `docs/demo/CardArrangementDemo.html` | Add to git, no content change (2.1) | — |

---

## 5. Edge Cases

| Case | Handling |
|------|----------|
| `deckCount == 0` | `ComputeOffsets` returns an empty array; callers' existing empty-deck guards are unchanged. |
| `deckCount == 1` | Single card at anchor, scale 1. Guard `t = i/(deckCount-1)` division (demo's `Math.max(1, N-1)` convention). |
| `deckCount == 2` | Curve degenerates to one step; second card at the first spacing step. |
| `deckCount < shrinkCount` | Clamp `shrinkCount` to `deckCount - 1` so the peak stays inside the curve. |
| Pending cards | Cascade computed with FULL deck count (VISUAL-FIX 2026-05-24 convention), positions AND scales. |
| Deck focused / peeled | `_deckFocusOffset` remains additive on the anchor; its value is computed from the cascade position (3.3 #5/#6). |
| `enableCascadeDeckLayout == false` | Legacy linear path preserved byte-for-byte (position AND scale). |
| Shop phase | `ShopUXManager` never calls combat layout functions; no impact. |

---

## 6. Regression & Test Plan

### 6.1 Automated — EditMode (executed by the implementing agent)

`DeckCascadeLayout` is pure math; verify it before any scene work using the existing EditMode infrastructure (`Assets/Scripts/Editor/Tests/`).

| # | Test | Expected |
|---|------|----------|
| A1 | Golden values: bake the demo's `(offsetX, offsetY, scale)` table for a 20-card deck with default params into the test as constants | `ComputeOffsets`/`ComputeScale` match the demo within epsilon at every index — this is the acceptance check named in section 9 |
| A2 | Edge counts: `deckCount` = 0 / 1 / 2 / `shrinkCount`-1 | Empty array; anchor-only scale 1; single step; clamped peak — no exceptions, no NaN |
| A3 | Scale monotonicity | Front-to-back scale is non-increasing, front = 1, tail >= `minScale` |
| A4 | Y-flip + direction mirror | Default output has front segment up-left (`x <= 0`, `y >= 0` in Unity space); flipping `cascadeDirection.x` mirrors X exactly |
| A5 | Legacy branch | With cascade disabled, `DeckPositionCalculator.CalculatePositionAtIndex` returns the pre-change linear result byte-for-byte |

### 6.2 Manual — Play Mode (executed by the project owner)

**All Play Mode verification in this section is executed manually by the project owner.** The implementing agent delivers code, serialized defaults, and this checklist; the owner runs the scenes, tunes parameters, and reports results. The agent does not drive Play Mode for this feature.

| # | Scenario | Steps | Expected Result |
|---|----------|-------|-----------------|
| 1 | Static cascade shape | Enter combat with a ~20-card deck | Front (top) card largest at anchor; smooth up-left arc; tail bends up-right with stable size and minimum spacing; no overlapping front cards; per-card scales match the curve from the first frame (no opening scale drift) |
| 2 | Parameter tuning | Adjust `cascadeShrinkCount` / `minScale` / spacings / `tailReturn` in Inspector during Play | Layout responds live; demo-equivalent shapes reproducible |
| 3 | Tail bend config | Toggle `cascadeTailBend` Mirror/Same; flip `cascadeDirection` sign | Tail bends toward opposite side vs keeps front direction; whole curve mirrors; curve is NEVER upside-down (Y-flip regression check) |
| 4 | Reveal entry | Click to reveal cards one by one | Top card flies from the cascade front anchor to the reveal zone; remaining cards re-flow along the curve |
| 5 | Bury / Stage / Exile | Play cards with Bury, Stage, Exile effects | Peak and slot-in positions land on the curve; moved cards arc correctly; other cards slide smoothly in parallel; **the moved card's scale lands at its cascade depth scale with no post-landing "breathing"** |
| 6 | Shuffle (Start Card) | Reach Start Card reveal | Whole-deck shuffle re-layouts on the curve; each card's scale tweens to its own cascade scale; afterShuffle flow timing unchanged |
| 7 | Deck focus peel & popup centering | Trigger an Attack from an off-reveal card; trigger a popup effect | Focused card lands on `deckFocusTargetPos` (validates 3.3 #5/#6); deck translates as before; attack return-to-deck restores the card's cascade scale; restore returns to the curve |
| 8 | `AddTempCard` pending cards | Play RIFT_INSECT / BLACKSMITH | New card's pop-up peak and slot-in target match its logical index in the FULL deck count on the curve (validates 3.3 #3); slot-in scale matches its depth |
| 9 | Reactive chain bury -> stage | Play a bury-triggers-stage combo | Intermediate `ApplyAnimationResult` states display correctly on the curve; no zero-distance animations |
| 10 | Reveal-to-bottom (second click) | Reveal a card, click again to place it at the bottom | The revealed card arcs to the cascade TAIL end (validates 3.3 #4), not to the old linear bottom position; scale lands at tail scale |
| 11 | Jitter comparison | Toggle `cascadeScaleJitterWithCard`; also try zeroing `randomDeckPositionOffsetRange` | With scaling ON the tail stays clean while front keeps organic feel; OFF shows ragged tail (documents the knob's effect) |
| 12 | Fallback flag | Set `enableCascadeDeckLayout = false` | Legacy linear fan restored exactly; all animations behave as before the feature |

---

## 7. Visual Bug Prevention Notes

This is a presentation change in `UXPrototype/`; per `docs/VisualBugPrevention_Guide.md`:

- Add `VISUAL-FIX(2026-07-17):` comment blocks around the cascade branch in `DeckPositionCalculator`, the six rerouted position sites, and the scale changes (3.4 sites).
- Append one row per section-6.2 scenario (12 rows) to `docs/RegressionChecklist.md`. Obsolete rows are never deleted; mark them `~~strikethrough~~` with `(Obsolete YYYY-MM-DD)`.

---

## 8. Open Decisions

1. **`cascadePxToWorld` and anchor placement**: the demo anchors at (55%, 65%) of the canvas; the Unity anchor is `physicalCardDeckPos`. Needs one in-scene tuning pass (owner, during test #2) to place the front card where the old top card sat, so the reveal path does not visually jump.
2. **Default tail bend**: `cascadeTailBend = Mirror` reproduces the demo. Flip to `Same` during the tuning pass if the scene composition favors a one-sided sweep.

---

## 9. Notes

- The html demo is currently untracked in git and exists only in the working tree; this feature adds it to version control (section 4 #9). The algorithm it contains is fully reproduced in section 3.1 of this PRD (transcription verified 2026-07-17 against `computeSmoothPositions` / `getCascadeParams`), so the PRD remains actionable even if the html is lost.
- The demo's slider defaults (6 / 0.55 / 2 / 60,70 / 8,12 / 2 / 0.55) are the serialized defaults; they were tuned against a 20-card reference image and are expected to need exactly one tuning pass for the real card aspect ratio.
- `DeckCascadeLayout` is pure C# math and is the only genuinely new logic in this feature. If its output matches the demo for the same `(deckCount, Params)` — enforced by test A1 — all remaining risk is integration-side and covered by the 3.3/3.4 call-site inventories plus the section-6.2 checklist.

---

## 10. Revision History

- **v1 (2026-07-17)**: Initial PRD.
- **v2 (2026-07-17)**: Revised after lead-programmer review.
	- **P1 — seam corrected**: v1 claimed two `CombatUXManager` wrappers were the only layout entry points. Code inspection found four bypasses: `CalculatePositionForPendingCard` (third wrapper, used by `SlotInCard`/`MoveCardToPopUpPosition`), and raw inline `xOffset`/`yOffset` math in `MoveRevealedCardToBottom`, `StartPeelCoroutine`, and `TransitionFocusCoroutine` (the latter two compute `_deckFocusOffset`). The cascade branch moved down into `DeckPositionCalculator`; full 6-site reroute inventory added (3.3).
	- **P2 — scale inventory added**: v1 changed only `UpdateAllPhysicalCardTargets`. Eight sites force the uniform deck scale (single moves, batch slot-in, shuffle, `SlotInCard`, `AddPhysicalCardToDeck`, `InstantiateAllPhysicalCards`, `AttackAnimationManager`); `GetDeckScaleAtIndex` helper + full inventory added (3.4). `AttackAnimationManager.cs` added to scope and file table.
	- **P3 — Y-flip documented**: the demo's canvas space is y-down, Unity is y-up; the flip is now an explicit, ordered port step (3.1 step 6, 3.6) instead of an implicit consequence of `cascadeDirection`.
	- Added automated EditMode tests (6.1), `deckCount == 0` edge case, jitter-site consolidation (3.5), corrected the demo path (`docs/`, not project root), aligned the checklist row count with the scenario count, added reveal-to-bottom as scenario #10, and added the html demo to version control.
