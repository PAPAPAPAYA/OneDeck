# Plan: Dynamic Arc Midpoint (showPos) for Cascade Deck Layout

Date: 2026-07-28
Status: Implemented (compile clean; EditMode tests 10/10 passed)

## Problem

`showPos` is a fixed scene Transform used as the arc midpoint for deck-bound card flights.
With the Smooth Curve Cascade Stack the deck's shape changes with deck count (front pinned at
`physicalCardDeckPos`, tail extending/hooking along the curve, coverage normalization stretching
small decks), so a hand-placed static midpoint drifts away from the deck's visual center.
Additionally `showPos.scale` is never read: arcs run a single joined
`DOScale(current -> target)` with no mid-flight "display" scale.

Use sites (all read `showPos.position` only; z is overridden to the start/target midpoint):

1. `CombatUXManager.MoveRevealedCardToBottom` (~line 443) — reveal zone -> deck bottom.
2. `CombatUXManager.MoveCardWithAnimation` (~line 536) — `config.arcMidpoint ?? showPos`
   fallback for all ToTop / ToBottom / ToIndex / ToPosition arcs.
3. `CombatUXManager.MoveCardToTopPopUpBatch` (~line 764) — Stage arcs.
4. `CombatUXManager.PlayStartCardShuffleAnimation` (~line 1205) — shuffle arcs.

## Decisions (confirmed with user)

- Scope: all 4 use sites.
- Mechanism: compute a virtual midpoint at animation start; the scene `showPos` Transform is
  NOT moved (kept as legacy fallback only).
- Position anchor: a point along the cascade curve at a configurable normalized t, plus a
  serialized world-space offset.
- Mid-arc scale: fixed multiplier (`physicalCardDeckSize * arcMidScaleMultiplier`), applied as a
  two-phase scale tween (current -> mid -> landing scale).

## Design

### 1. New serialized fields on `CombatUXManager` (new `[Header("DYNAMIC ARC MIDPOINT")]`)

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| `useDynamicArcMidpoint` | bool | true | Master toggle. Off = legacy `showPos` behavior byte-for-byte. |
| `arcMidpointCurveT` | float [0,1] | 0.5 | Normalized position along the cascade walk (0 = front, 1 = deepest tail). |
| `arcMidpointOffset` | Vector2 | (0, 0) | World-space x/y offset added after the curve point (replaces hand-placing showPos). |
| `arcMidScaleMultiplier` | float | 1.2 | Mid-arc scale = `physicalCardDeckSize * multiplier`. |
| `arcMidScaleEnabled` | bool | true | Off = keep the legacy single joined scale tween. |

### 2. New pure helper in `DeckCascadeLayout` (unit-testable)

```csharp
/// <summary>
/// Offset (pre-direction-mirror) at a fractional position along the cascade walk.
/// t in [0,1]: 0 = front card, 1 = deepest card. Interpolates between the two
/// bracketing card offsets from the cached ComputeOffsets, so coverage
/// normalization and small-deck clamping are inherited for free.
/// </summary>
public static Vector2 ComputeOffsetAtCurveT(int deckCount, float t, Params p, float pxToWorld)
```

Implementation: `ci = t * (deckCount - 1)`, lerp `ComputeOffsets(...)` between
`floor(ci)` / `ceil(ci)`. No new Bezier math; reuses the existing per-(count, pxToWorld, Params)
cache.

### 3. New seam in `CombatUXManager`

```csharp
/// <summary>
/// Arc midpoint for deck-bound flights. Cascade + toggle on: point at arcMidpointCurveT
/// along the cascade curve (via the same anchor/pxToWorld/direction as layout) plus
/// arcMidpointOffset; z = midpoint of start/target z (existing GetArcMidpoint rule).
/// Otherwise: legacy showPos-based midpoint. explicitOverride (CardMoveConfig.arcMidpoint)
/// always wins for back-compat.
/// </summary>
private Vector3 GetArcMidpointPosition(Transform explicitOverride, Vector3 startPos, Vector3 targetPos)
```

Notes:
- The world point is derived through the same base position / direction mirror the layout seam
  uses, so peel-focus offsets (`AttackAnimationManager.HoldDeckFocus`) are inherited automatically.
- Count comes from `GetCascadeDeckCount()` so `revealCardCountsAsDeckFront` semantics carry over.
- During batch/shuffle animations every card computes the same point (deck state is static
  mid-animation); the offsets cache makes this O(1) per card.

### 4. Two-phase scale tween

When an arc uses the dynamic midpoint AND `arcMidScaleEnabled`:

- Phase 1 (first half duration): `DOScale(physicalCardDeckSize * arcMidScaleMultiplier, half)`.
- Phase 2 (second half): `DOScale(existingTargetScale, half)` — the existing per-moveType
  cascade depth scale logic (`GetDeckScaleAtIndex`, `targetScaleOverride`, shuffle per-card
  depth scale, stage `peakScale`) is unchanged.

When off / legacy path: keep the current single joined `DOScale(target, full)` byte-for-byte.

### 5. Per-site edits

1. `MoveCardWithAnimation` (~536-571):
   - Replace `Transform arcPoint = config.arcMidpoint ?? showPos` + `GetArcMidpoint(arcPoint.position, ...)`
     with `GetArcMidpointPosition(config.arcMidpoint, current, target)`.
   - Replace the single joined scale tween with the two-phase variant when applicable.
2. `MoveRevealedCardToBottom` (~437-449): pass `arcMidpoint = null` in the config (the seam
   resolves dynamic/legacy); no other change — `targetScaleOverride` stays.
3. `MoveCardToTopPopUpBatch` (~764-778): replace `GetArcMidpoint(showPos.position, current, peakPos)`
   with the seam; two-phase scale ends at `peakScale` instead of the deck depth scale.
4. Shuffle (~1205-1216): replace `GetArcMidpoint(showPos.position, ...)` with the seam;
   two-phase scale ends at the existing per-card `GetDeckScaleAtIndex(deckIndex)`.

### 6. Fallback matrix

| `useDynamicArcMidpoint` | `enableCascadeDeckLayout` | `config.arcMidpoint` | Result |
|---|---|---|---|
| off | any | null | legacy `showPos` midpoint, single scale tween |
| on | off | null | legacy `showPos` midpoint, single scale tween |
| on | on | null | dynamic curve point + offset, two-phase scale |
| any | any | set | explicit Transform wins (position only), single scale tween |

### 7. Editor aid (optional, low cost)

`OnDrawGizmosSelected` on `CombatUXManager`: when `useDynamicArcMidpoint`, draw a wire sphere at
the computed dynamic midpoint (using the current deck count) so `arcMidpointCurveT` /
`arcMidpointOffset` can be tuned visually in the scene view.

### 8. Tests

- `Assets/Scripts/Editor/Tests/DeckCascadeLayoutTests.cs`: add cases for
  `ComputeOffsetAtCurveT` — t=0 equals offsets[0], t=1 equals offsets[count-1], t=0.5 equals the
  midpoint of the bracketing pair, monotonic along the walk, coverage-normalized small deck.
- No PlayMode test required for the plan; manual regression:
  - `enableCascadeDeckLayout = false` or `useDynamicArcMidpoint = false` -> all four animations
    identical to current behavior.
  - Cascade on: reveal->bottom, Stage, Bury, Delay, shuffle arcs pass through the curve-following
    midpoint; cards visibly enlarge mid-flight and land at their cascade depth scale.

## Out of scope

- Shop layout (unaffected by cascade).
- Pop-up peak math (`popUpYOffset` / `popUpZBoost`) — peaks already derive from per-card deck
  positions.
- Scene `showPos` Transform removal — kept as the legacy fallback.
