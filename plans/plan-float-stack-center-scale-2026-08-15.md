# Float Stack — Center / Compress / Bottom-Limit / Reveal-at-Bottom — PRD / Implementation Plan

Date: 2026-08-15
Status: Approved design (doc only; implementation pending)
Interactive reference: `docs/demo/CardStackRevealDemo.html` (updated 2026-08-15 with the full
final semantics; user reviewed in browser)
Supersedes the layout-semantics sections of `plans/plan-float-stack-reveal-layout-2026-08-13.md`
(anchor meaning, reveal pose, shadow home). Everything not mentioned here stays as shipped.
Scope: combat-phase physical deck layout + reveal-zone presentation. Shop layout, card
logic/effects, animation request pipeline are untouched.

## 1. Overview

Four changes to the Float Stack layout, all settled in the demo:

- **Centered stack**: the anchor (`physicalCardDeckPos`) is redefined from "stack bottom"
  to "stack center" — the deck is vertically symmetric around the anchor at any count.
- **Global compression**: when `stepY·deckCount` exceeds `maxHeightPx`, a uniform
  `globalScale = clamp(maxHeightPx / (stepY·deckCount), minScale, 1)` applies to every card
  AND to the step itself (compress-only; small decks are never stretched).
- **Bottom limit**: when the lowest card-face point (max of deck bottom edge and revealed
  card bottom edge) sinks more than `bottomLimitPx` below the anchor, the whole stack lifts
  and pins that point at the line; further growth extends upward only ("unhandled"). When
  the deck shrinks back, lift returns to 0 continuously and the stack re-centers.
- **Revealed card at the deck bottom**: the reveal pose and the shadow both derive from
  **slot 0** (one step below the lowest stack slot = the shadow's home), which follows the
  centered/pinned stack bottom. The revealed card keeps a constant offset from the shadow
  (bound pair, per user review). With the default `revealUpY = −14` the revealed card is
  the lowest element of the whole composition, matching the shipped look.

## 2. Confirmed Defaults

From the final demo state (2026-08-15, user-approved):

| Param | Default | Notes |
|---|---|---|
| floatStackPxToWorld | 0.01 | demo px → world (unchanged) |
| floatStackStepY | 14 | signed; negative = downward stack (skips scale/limit) |
| floatStackRevealScale | 1.07 | revealed card scale multiplier (unchanged) |
| floatStackRevealFloatX | 16 | left offset from the shadow home (px; unchanged) |
| floatStackRevealUpY | −14 | up offset from the shadow home (px; negative = below = lowest element) |
| floatStackShadowOffset | (0, 30) | extra shadow offset from slot 0 (px, canvas y-down; unchanged) |
| floatStackShadowOpacity | 0.85 | driven shadow target alpha (unchanged) |
| **floatStackMaxHeightPx** | 250 | compression trigger height; ≤ 0 = off |
| **floatStackMinScale** | 0.55 | global scale floor |
| **floatStackBottomLimitPx** | 270 | max px any card face may sink below the anchor; ≤ 0 = off |
| **floatStackCardHalfHeightPx** | 105 | card-face half height in demo px (= card prefab face world height at scale 1 ÷ 2 ÷ pxToWorld) |

Suggested sizing rule for the future mechanics-level hard cap N:
`minScale = maxHeightPx / (stepY·N)` — the scale reaches the floor exactly at the cap, so
the stack height never exceeds `maxHeightPx` before the pin takes over.

## 3. Layout Semantics (final, ported 1:1 from the demo)

All vertical quantities below are in demo px, converted by `pxToWorld`. Unity is y-up; the
demo runs canvas y-down — the demo's "below anchor" magnitudes map 1:1 to Unity's negative-y.

### 3.1 Frame (one entry point for every consumer)

```
ComputeFrame(deckCount, p) -> { globalScale, effectiveStepY, liftPx }
  globalScale    = p.maxHeightPx > 0 && p.stepY > 0 && deckCount > 0
                   ? clamp(p.maxHeightPx / (p.stepY·deckCount), p.minScale, 1) : 1
  effectiveStepY = p.stepY · globalScale
  if p.bottomLimitPx > 0 && effectiveStepY > 0 && deckCount > 0:
      slot0        = effectiveStepY·(deckCount+1)/2          // shadow home, px below anchor
      deckBottom   = effectiveStepY·(deckCount−1)/2 + cardHalfHeight·globalScale
      revealBottom = slot0 − revealUpY + cardHalfHeight·globalScale·revealScale
      drop         = max(deckBottom, revealBottom)           // lowest CARD-FACE point
      liftPx       = max(0, drop − bottomLimitPx)            // upward shift (y-up positive)
  else liftPx = 0
```

The soft drop shadow is NOT part of the limit — only card faces are constrained (user decision).

### 3.2 Positions (Unity y-up, offsets from the anchor)

- **Stack card** at unity index `j` (0 = deck bottom, `count−1` = next to reveal):
  `y = effectiveStepY·(count − 2j − 1)/2 + liftPx` → symmetric around the anchor;
  `count = 1` lands exactly on the anchor. z keeps `basePos.z − zOffset·j` (unchanged).
- **Revealed card**: `(−floatX,  revealUpY − effectiveStepY·(count+1)/2 + liftPx)` —
  i.e. slot 0 + `(−floatX, revealUpY)`. Scale = `physicalCardDeckSize × revealScale × globalScale`.
- **Shadow home** (driven big shadow local pos on `physicalCardDeckPos`):
  `y = liftPx − effectiveStepY·(count+1)/2 − shadowOffset.y` (x = `shadowOffset.x`);
  scale = `revealScale × globalScale`; z keeps the existing front-gap formula.
- **Arc midpoint** (dynamic arc midpoint + gizmo):
  `y = basePos.y + (effectiveStepY·(count+1)/2 + liftPx)·pxToWorld` (one step beyond the
  stack's back/top slot, lifted with the stack).

### 3.3 Count seams (unchanged)

Raw `physicalCardsInDeck.Count` in Float Stack mode (no reveal +1); peel-focus segment
count still takes precedence via `GetLayoutDeckCount()` — a focused segment is
centered/compressed/pinned by its own segment count (accepted behavior).

### 3.4 Behavior regimes (defaults)

- ≤ 17 deck cards: pure centering, scale 1, lift 0.
- 18–~45: compression eases in (hyperbolic onset), floored at minScale.
- beyond: lowest card-face point pinned at `bottomLimitPx`; deck top grows upward unhandled.

### 3.5 Regressions accepted by design

- Pre-change behavior is NOT preserved: even with the new params at 0, the stack is
  centered and the reveal pose/shadow derive from slot 0 (Float Stack shipped 2 days ago;
  this is the intended new base behavior, approved by the user).
- Reveal pose now drifts with deck count for small decks (it tracks the stack bottom).
  In-combat count is ~constant (reveal→bottom conveyor), so the attention point is stable
  in practice.

## 4. Code Changes

### 4.1 `Assets/Scripts/UXPrototype/DeckFloatStackLayout.cs` (rewrite, stays pure static)

- `Params` += `maxHeightPx`, `minScale`, `bottomLimitPx`, `cardHalfHeightPx`.
- New `Frame` struct { `globalScale`, `effectiveStepY`, `liftPx` } + `ComputeFrame(count, p)`.
- `ComputeSlotOffset(unityIndex, deckCount, p, pxToWorld)` — signature unchanged; body
  becomes the centered+lifted formula (§3.2). `DeckPositionCalculator` needs NO change.
- `ComputeRevealOffset(p, pxToWorld)` → `ComputeRevealOffset(deckCount, p, pxToWorld)`
  (new count parameter; the pose is slot-0-derived now).
- New helpers so `CombatUXManager` never re-derives signs:
  `ComputeShadowHomeYPx(count, p)` = `liftPx − effectiveStepY·(count+1)/2` and
  `ComputeArcMidYPx(count, p)` = `liftPx + effectiveStepY·(count+1)/2`.

### 4.2 `Assets/Scripts/UXPrototype/CombatUXManager.cs`

- New serialized fields (defaults §2): `floatStackMaxHeightPx`, `floatStackMinScale`,
  `floatStackBottomLimitPx`, `floatStackCardHalfHeightPx` (+ tooltips; group with the
  existing float-stack fields).
- `BuildFloatStackLayoutParams()` (:1135) — populate the 4 new params.
- `GetDeckScaleAtIndex(int, int)` float-stack branch (:1215) —
  `physicalCardDeckSize * ComputeFrame(count, …).globalScale`.
- `GetCascadeJitterScale` float-stack branch (:1243) — return `frame.globalScale`
  (jitter decays with compression, replacing the constant 1).
- `GetRevealZonePosition()` float-stack branch (:1710) — use
  `ComputeRevealOffset(physicalCardsInDeck.Count, …)`; z clamp unchanged.
- `GetRevealZoneScale()` (:1735) — `physicalCardDeckSize * floatStackRevealScale * frame.globalScale`.
- `TryDriveRevealBigShadow` (:1747) — `targetLocalPos.y = ComputeShadowHomeYPx(count, …)·px − floatStackShadowOffset.y·px`;
  scale argument → `floatStackRevealScale * frame.globalScale`; z/front-gap unchanged.
- `TryGetArcMidpointPosition` float-stack branch (:828) and `OnDrawGizmosSelected` (:883) —
  `midY = basePos.y + ComputeArcMidYPx(count, …)·px (+ arcMidpointOffset.y)`.

All other seams (`DeckPositionCalculator`, `CalculatePositionAtIndex`,
`CalculateAnimationPositionAtIndex`, `UpdateAllPhysicalCardTargets`,
`MoveRevealedCardToBottom`, shadow suppression, z clamps) inherit the new math through the
helpers above without edits. Preserve every existing `VISUAL-FIX(...)` comment block.

### 4.3 Tests — `Assets/Scripts/Editor/Tests/DeckFloatStackLayoutTests.cs`

- Regenerate goldens for the new base behavior (params with maxHeight/bottomLimit = 0):
  8 deck cards → GoldenY `{0.49, 0.35, 0.21, 0.07, −0.07, −0.21, −0.35, −0.49}`;
  reveal offset (count 8) = `(−0.16, −0.77)`; `deckCount 1 → offset 0`; negative stepY
  flips the symmetric table. Update C1–C5 accordingly (C2 gains the deckCount argument).
- New frame goldens (stepY 14, revealScale 1.07, upY −14, cardHalf 105, maxHeight 250,
  minScale 0.55, bottomLimit 270):
  - F1 disabled: maxHeightPx 0 → scale 1, lift 0. bottomLimitPx 0 → lift 0.
  - F2 onset: count 24 → scale `250/336 = 0.7440`, lift 0 (drop 227.8 < 270).
  - F3 floor: count 59 → scale 0.55.
  - F4 lift, reveal-edge binds: count 59 → drop `max(281.05, 306.79) = 306.79` → lift `36.79`.
  - F5 lift, deck-edge binds: same but upY `+70` → reveal edge shrinks below deck edge →
    lift from deck edge only.
  - F6 negative stepY → scale 1, lift 0 (pure centering).
  - F7 slot symmetry with lift 0 (`offset(j) == −offset(count−1−j)`); lift shifts all
    slots by the same amount; reveal offset equals slot0 + upY relation.

## 5. Scene Migration (manual, one-time)

- Move `physicalCardDeckPos` to the intended screen-center position (its current position
  was tuned as the stack BOTTOM). Nothing else moves: `physicalCardRevealPos` only
  supplies z in Float Stack mode; shadow/reveal derive from the anchor.
- New serialized fields take the §2 code defaults — no scene value edits required.

## 6. Verification & Rollout

1. `DeckFloatStackLayoutTests` green (EditMode).
2. Play Mode, Float Stack mode: sweep deck sizes ~8 / 25 / 40 / 60 (DeckTester or
   AddTempCard) — centering, compression, pin, reveal-at-bottom, shadow sync during
   reveal/return; compare against the demo side by side.
3. Update `AGENTS.md` Float Stack section (anchor semantics, frame, new params, shadow
   home, reveal-at-bottom) — keep the 32 KB cap (`wc -c AGENTS.md`).
4. Demo ↔ code parity: any later retune happens in the demo first, then ports.

## 7. Out of Scope / Follow-ups

- Mechanics-level hard deck-count cap (user will implement separately; §2 sizing rule
  aligns the visual floor with it).
- Shop layout, other layout modes (Linear/Cascade/ArcLoop untouched).
- `bottomLimitPx` does not constrain the soft drop shadow (user decision; shadow may
  bleed past the line).
