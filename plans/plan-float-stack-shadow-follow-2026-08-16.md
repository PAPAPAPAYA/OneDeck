# Float Stack — Follow-Mode Big Shadow (Attack / Flip Sync) — Iteration Plan

Date: 2026-08-16
Status: Proposal (doc only; no code changed)
Supersedes: nothing — extends `plans/plan-float-stack-center-scale-2026-08-15.md` (shadow-home
semantics stay; only the drive mechanism changes from static pin to per-frame follow).
Scope: combat-phase Float Stack driven big shadow. Other layout modes, card logic/effects,
and the animation request pipeline are untouched.

## 1. Problem

Float Stack detaches the revealed card's `PhysicalCardBigShadow` from the card:
`CombatUXManager.TryDriveRevealBigShadow` (:1787) re-parents it to the `physicalCardDeckPos`
anchor and `CardPhysObjScript.DriveBigShadowToPose` (:783) tweens it to a static slot-0 home
pose. While driven, the shadow never moves again, so every revealed-card motion loses its
shadow sync:

- **Reveal flip** — `MoveCardToRevealZone` calls `SetFaceUp(true, true)`, which squashes
  `_flipRoot.localScale.x` 1→0→1. The shadow was re-parented out of `_flipRoot`, so it stays
  a full-size static blob through the flip.
- **Attack animation** — `AttackAnimationManager.PlayAttackAnimationCoroutine` tweens the
  card root (wind-up, 1.4x scale-up, rotate toward target, charge, 0.85x scale-down,
  overshoot, return); the shadow stays pinned at the home pose.
- Same detachment on the emphasize scale pulse (`RecorderAnimationPlayer` :431, physical-card
  DOScale 1.2x) and on hover pop-up of the revealed card.

Other layout modes never re-parent the shadow (it stays under `_flipRoot`), so they inherit
flip/attack sync for free from the transform hierarchy. Only Float Stack needs a follow
mechanism.

## 2. Design: always-on follow (one seam, no call-site churn)

Key invariant of the shipped layout: at rest the revealed card sits at a **constant offset**
from the shadow home ("bound pair", center-scale plan §1). Therefore a follower that places
the shadow at `cardLivePose + restOffset` every frame reproduces the CURRENT static pose
exactly at rest, and continuously tracks every motion — one mechanism covers rest, reveal
flight, flip, attack, emphasize, pop-up, and the return flight. No changes needed in
`AttackAnimationManager`, `RecorderAnimationPlayer`, or `SetFaceUp`: they keep tweening the
card, the follower just observes it.

Per-frame follow rule (LateUpdate, applied while driven):

- x/y = `card.position + offsetWorld`, where `offsetWorld = shadowHomeWorld − revealHomeWorld`
  captured at drive start from the two known rest poses.
- Flip pivot correction: `posX = card.x + offsetWorld.x * squashX`, so the squash collapses
  around the card center (the flip pivot) instead of the shadow's own center.
- z = pinned anchor-local z (existing front-gap formula, `TryDriveRevealBigShadow` :1796).
  The reveal-z re-clamp and arc-return z drift never leak into the shadow.
- scale = `card.lossyScale ⊙ bakedShadowHomeLocalScale`, then `x *= squashX`. Preserves the
  VISUAL-FIX(2026-08-16) baked-scale invariant: rest pose == `revealScale * globalScale *
  baked` on every re-drive; no compounding.
- rotation = card z-rotation (toggle `floatStackShadowFollowRotation`, default on; identity at
  rest = today's drive target).
- alpha untouched by the follower — the drive fade-in and release fade-out stay as they are.
- `squashX = _flipRoot.localScale.x` (null → 1; the Start Card has no flip root).

## 3. Iteration 1 — core follow (flip + attack + emphasize + flights)

1. **Demo first** (project parity rule: retunes settle in the demo, then port): extend
   `docs/demo/CardStackRevealDemo.html` with a flip + attack-lunge simulation; validate the
   follow offset, pivot-corrected squash, and rotation follow on/off in the browser. Port the
   settled feel.
2. **New `Assets/Scripts/UXPrototype/BigShadowFollower.cs`** — tiny component on the shadow
   GameObject, added at drive start, destroyed at restore. Fields: `source` (card root),
   `squashSource` (`_flipRoot`, nullable), `offsetWorld`, `bakedLocalScale`, `pinnedLocalZ`,
   `followRotation`, and a blend weight 0→1 over `moveDuration` so the shadow glides from its
   in-deck pose into the follow pose (matches today's tween-in). LateUpdate applies §2. If
   `source == null` (card destroyed/exiled mid-drive) → `Destroy(gameObject)`; this also
   cleans up the pre-existing invisible orphan-shadow leak when
   `RestoreBigShadowFromDrive`'s restore callback early-returns on a destroyed card.
3. **`CardPhysObjScript.DriveBigShadowToPose`** — extend signature with `followOffsetWorld`
   (and the rotation flag). Keep the re-parent, the alpha fade-in, and the baked-scale
   capture; replace the three one-shot DOLocalMove/DOScale/DOLocalRotateQuaternion tweens with
   follower attach + blend-weight tween. Preserve the VISUAL-FIX(2026-08-16) comment block.
4. **`CombatUXManager.TryDriveRevealBigShadow`** — compute
   `followOffsetWorld = physicalCardDeckPos.TransformPoint(targetLocalPos) - GetRevealZonePosition()`
   and pass it through. No other call-site changes.
5. **`CardPhysObjScript.RestoreBigShadowFromDrive`** — keep the fade; the follower stays alive
   during the fade so the shadow tracks the return arc while fading out; destroy the follower
   inside the restore callback (and immediately on the instant path).
6. **Edge cases to cover**:
   - Start Card: no `_flipRoot` (squash = 1); shuffle-path release (VISUAL-FIX 2026-08-15)
     must still fire before the shuffle flight.
   - Exile/destroy while revealed (:2688 release path) — release + follower self-destruct.
   - `OnValidate` live re-drive (:1861) — follower recreated; baked-scale invariant holds.
   - `wasPoppedUp` attack return — follower tracks the card back to the popup peak.
   - Large decks (`globalScale < 1`) — shadow still matches the reveal card exactly at rest.

## 4. Iteration 2 — optional polish (after Iteration 1 lands and feels right)

- **Elevation fade**: fade alpha (and optionally scale) by distance from the rest home during
  attacks — sells the "card lifts off" read. New param `floatStackShadowFollowFadeDistance`
  (0 = off, default off until reviewed).
- **Pop-up follow gate**: if the shadow tracking the hover pop-up feels wrong, freeze the
  follower while `isPoppedUp` (one-flag guard).
- Revisit the rotation-follow default after play feel.

## 5. Iteration 3 — tests & docs

- Factor the follow-pose math into a pure static (`ComputeFollowPose`) and add EditMode
  goldens (style of `DeckFloatStackLayoutTests`): rest == home identity, squash pivot
  collapse, scale proportionality, z pin.
- Append rows to `docs/RegressionChecklist.md` (flip sync, attack sync, orphan cleanup).
- Update the `AGENTS.md` Float Stack section: the big shadow is follow-driven, not static;
  keep the 32 KB cap (`wc -c AGENTS.md`).

## 6. Play-mode verification (manual; per AGENTS.md only when the user requests it)

- Float Stack: reveal any card — the shadow squashes in sync with the flip and glides to the
  slot-0 home.
- Attack with the revealed card (e.g. BLACKSMITH) — the shadow tracks wind-up / charge /
  overshoot / return, scales 1.4x / 0.85x with the card, and lands exactly back at home.
- Emphasize pulse and hover pop-up on the revealed card — the shadow follows.
- Resolve the effect (card returns to deck bottom) — the shadow tracks the return arc while
  fading out; no ghost shadow.
- Exile the revealed card — no orphaned shadow under the anchor.
- Play-mode Inspector retune of `floatStack*` — no shadow growth (VISUAL-FIX 2026-08-16
  invariant).
- Cascade / Linear / ArcLoop — unchanged (shadow still card-local under `_flipRoot`).

## 7. Out of scope

- Shop, other layout modes, the rim shadow `PhysicalCardShadow` (stays card-local in all
  modes), and the soft-shadow bottom-limit semantics (shadow may bleed past the line — user
  decision, center-scale plan §3.1/§7).
