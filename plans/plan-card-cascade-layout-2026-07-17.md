# Card Cascade Deck Layout — Implementation Plan

## 1. Overview

### 1.1 Goal

Replace the current linear-fan physical deck layout with the "Smooth Curve Cascade Stack" validated in `docs/demo/CardArrangementDemo.html` (method A + smooth curve, decision locked 2026-07-17):

- Front card (deck top, first revealed) is largest and sits at the anchor (bottom-right).
- Front segment expands up-left along a smooth curve while size and spacing shrink progressively.
- After the turning point the curve bends up-right; size stabilizes and spacing stays at minimum.
- The result is a continuous arc of depth, not a hard polyline.

### 1.2 Scope

- Combat physical deck only: `CombatUXManager` / `physicalCardsInDeck`.
- Shop (`ShopUXManager`) is untouched.
- The demo's "hover expand" interaction is NOT ported; the existing deck peel/focus system covers deck inspection.
- Methods B (container stack), C (canvas draw), D (3D perspective) from the demo are rejected and kept only as comparison records in the html.

### 1.3 Source of Truth for the Algorithm

`docs/demo/CardArrangementDemo.html`, functions `computeSmoothPositions()` (lines 498-558) and `getCascadeParams()` (lines 560-566). The C# port must reproduce this math 1:1 before any tuning.

---

## 2. Requirements (from clarification)

1. Method A: per-index `localPosition`/`localScale` computed in script. No container refactor, no RenderTexture, no 3D perspective camera.
2. Quadratic Bezier trajectory + arc-length parameterization, ported from the demo.
3. All layout parameters serialized on `CombatUXManager`, with demo values as defaults (converted px -> world units).
4. Keep the existing target-based animation model: cards still animate through `CardPhysObjScript.SetTargetPosition/SetTargetScale` in their own Update. DOTween and `CombatAnimationSpeed.SpeedScale` are untouched.
5. Every existing caller of the position calculation (shuffle, deck focus/peel, pop-up peaks, slot-in, reveal entry, `MoveCardToIndex`) must follow the new curve without caller-side changes.
6. A config flag `enableCascadeDeckLayout` toggles between cascade and the legacy linear layout.
7. Follow `docs/VisualBugPrevention_Guide.md`: `VISUAL-FIX(YYYY-MM-DD):` comment blocks and `docs/RegressionChecklist.md` rows.

---

## 3. Design

### 3.1 Algorithm Port (pure static, unit-testable)

Add a new static helper next to `DeckPositionCalculator`, e.g. `Assets/Scripts/UXPrototype/DeckCascadeLayout.cs`. Pure math, no Unity scene dependencies beyond `Vector2`/`Vector3`.

```csharp
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

	// Computes per-card offsets for a deck of deckCount cards.
	// cascadeIndex 0 = front card at anchor; cascadeIndex deckCount-1 = deepest card.
	public static Vector2[] ComputeOffsets(int deckCount, Params p);
	public static float ComputeScale(int cascadeIndex, int deckCount, Params p);
}
```

Port steps (mirroring the demo):

1. Bezier control points:
	- `P0 = (0,0)` (front card at anchor).
	- `P1 = (-peakX, -peakY)`, `peak = startSpacing * shrinkCount * 0.85` (pulls up-left).
	- `P2`: `tailX = -peakX*(1-tailReturn) + minSpacingX*(deckCount-shrinkCount)*tailReturn*0.6`, `tailY = -peakY - minSpacingY*(deckCount-shrinkCount)`.
2. Sample the curve `arcSamples` times, build the cumulative arc-length table.
3. Walk cards along the curve: per-card step length = `|lerp(startSpacing, minSpacing, easeOutPower(t, spacingPower))| * 0.5`, `t = i/(deckCount-1)`; look up the arc-length table for the position.
4. Scale: `1 - (1-minScale) * easeOutPower(t, scalePower)`.
5. Cache the last result keyed by `(deckCount, Params)` because per-index callers must not recompute the whole curve per card.

### 3.2 Index Mapping

- Demo `cascadeIndex 0` = front card = Unity deck top = `physicalCardsInDeck[count-1]` (first revealed).
- Mapping: `cascadeIndex = deckCount - 1 - unityIndex`.
- `physicalCardDeckPos.position + _deckFocusOffset` becomes the FRONT (top card) anchor; the curve extends away from it. Curve direction sign is configurable; default up-left on screen.
- Z depth keeps the existing formula `basePos.z - zOffset * index` so render sorting is unchanged.

### 3.3 Integration Seam: `CombatUXManager`

Change only the two layout entry points so all callers inherit the curve:

- `CalculatePositionAtIndex(int index)` (line ~747)
- `CalculateAnimationPositionAtIndex(int index)` (line ~771)

Both currently delegate to `DeckPositionCalculator.CalculatePositionAtIndex(index, count, basePos, xOffset, yOffset, zOffset)`. New behavior:

```csharp
if (enableCascadeDeckLayout)
	return DeckCascadeLayout position for (deckCount - 1 - index), anchored at basePos, z as before;
else
	return legacy linear result;
```

Callers that automatically follow the curve with zero changes: `UpdateAllPhysicalCardTargets`, shuffle, deck focus/peel (`_deckFocusOffset` stays additive), pop-up peak positions, slot-in targets, reveal entry, `MoveCardToIndex`.

### 3.4 Per-Index Scale in `UpdateAllPhysicalCardTargets`

Currently uniform: `physScript.SetTargetScale(physicalCardDeckSize)` (line ~1153). In cascade mode:

```csharp
physScript.SetTargetScale(physicalCardDeckSize * cascadeScale[cascadeIndex]);
```

Pending cards are included via the full deck count, matching the existing `CalculateAnimationPositionAtIndex` convention (VISUAL-FIX 2026-05-24), so peak/slot-in scales agree with the final layout.

### 3.5 Random Jitter (`_deckOffsetProvider`)

Jitter is already a pure additive overlay applied AFTER the base layout (`GetFinalDeckPositionForCard` = layout pos + jitter; rotation likewise). The cascade branch produces the base position through the same path, so `cascadePos + jitter` works with zero extra plumbing, and the existing serialized ranges (`randomDeckPositionOffsetRange` / `randomDeckRotationOffsetRange`, zero = off) stay the single control point.

Impact assessment: amplitude is +/-0.05 world units. Front segment spacing is ~0.6-0.7 units (demo 60-70px at pxToWorld 0.01), so jitter is invisible there. Tail spacing shrinks to ~0.08-0.12 units, so the same jitter is ~50% of tail spacing and can make the tail look ragged, breaking the smooth arc.

Mitigation (one line): multiply the POSITION jitter by the card's cascade scale, so deeper (smaller) cards get proportionally smaller jitter. Serialized as `cascadeScaleJitterWithCard = true`. Rotation jitter is scale-independent and stays as-is.

### 3.6 Serialized Parameters

On `CombatUXManager`:

```csharp
public enum CascadeTailBend { Mirror, Same } // Mirror = bend toward opposite side (demo), Same = keep front direction

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
public Vector2 cascadeDirection = new Vector2(-1f, 1f); // up-left by default
public CascadeTailBend cascadeTailBend = CascadeTailBend.Mirror;
public bool cascadeScaleJitterWithCard = true; // see 3.5
```

Direction and bend are resolved inside `DeckCascadeLayout`: the curve is computed in canonical demo space (front up-left, tail bending up-right), the tail-return term in P2 is multiplied by `+1` (Mirror) or `-1` (Same), then the final offsets are mirrored by the sign of `cascadeDirection`. No caller is affected.

Legacy `xOffset/yOffset/zOffset` fields stay for the fallback path.

### 3.7 Unaffected Systems (verify, do not change)

- `ApplyAnimationResult` deck-order advancement and `RecorderAnimationPlayer` parallel tween flow.
- `CombatAnimationSpeed.SpeedScale` (applied inside `CardPhysObjScript` during Combat phase only).
- Reveal-zone card position/scale (`physicalCardRevealPos`, `physicalCardRevealSize`).
- Deck focus translate: `_deckFocusOffset` is added to the anchor exactly as today.

---

## 4. Files to Modify

| File | Change |
|------|--------|
| `Assets/Scripts/UXPrototype/DeckCascadeLayout.cs` | New pure static layout helper (section 3.1). |
| `Assets/Scripts/UXPrototype/CombatUXManager.cs` | Serialized params + flag, cascade branch in both position calculators, per-index scale in `UpdateAllPhysicalCardTargets`, jitter gating. |
| `docs/RegressionChecklist.md` | Add regression rows per project rules. |
| `AGENTS.md` | Update the deck layout description (Zones / Card Movement sections) to the cascade model. |

---

## 5. Edge Cases

| Case | Handling |
|------|----------|
| `deckCount == 1` | Single card at anchor, scale 1. Guard `t = i/(deckCount-1)` division. |
| `deckCount == 2` | Curve degenerates to one step; place second card at first spacing step. |
| `deckCount < shrinkCount` | Clamp `shrinkCount` to `deckCount - 1` so the peak stays inside the curve. |
| Pending cards (`isPendingSlotIn`) | Cascade computed with FULL deck count (existing VISUAL-FIX 2026-05-24 convention). |
| Deck focused / peeled | `_deckFocusOffset` remains additive on the anchor; verify peel and popup centering still land on `deckFocusTargetPos`. |
| `enableCascadeDeckLayout == false` | Legacy linear path preserved byte-for-byte. |
| Shop phase | `ShopUXManager` never calls these combat layout functions; no impact. |

---

## 6. Visual Bug Prevention Notes

This is a presentation change in `UXPrototype/`; per `docs/VisualBugPrevention_Guide.md`:

- Add `VISUAL-FIX(2026-07-17):` comment blocks around the cascade branches in both position calculators and the scale change in `UpdateAllPhysicalCardTargets`.
- Append rows to `docs/RegressionChecklist.md` covering at minimum:
	- Reveal entry path (top card flies from the cascade front anchor to reveal zone).
	- Bury / Stage / Exile animations (peak and slot-in positions on the curve).
	- Shuffle animation (whole-deck re-layout on the curve).
	- Deck focus peel and popup centering (anchor translation).
	- `AddTempCard` slot-in with pending cards.
	- Reactive chain bury -> stage (intermediate `ApplyAnimationResult` states on the curve).

---

## 7. Open Decisions

1. **`cascadePxToWorld` and anchor placement**: the demo anchors at (55%, 65%) of the canvas; the Unity anchor is `physicalCardDeckPos`. Needs one in-scene tuning pass to place the front card where the old top card sat, so the reveal path does not visually jump.
2. **Default tail bend**: `cascadeTailBend = Mirror` reproduces the demo. Flip to `Same` during the tuning pass if the scene composition favors a one-sided sweep.

Confirm these before or during implementation.
