# Float Stack — Follow-Mode Big Shadow (Attack / Flip Sync) — Iteration Plan

Date: 2026-08-16
Status: Iteration 1 implemented 2026-08-16 (`BigShadowFollower` + drive/restore rewiring +
`AttackAnimationManager` lift hooks; compiles clean). Tunable values are placeholders pending
play-mode tuning by the user. Iteration 2/3 (pop-up gate, rotation-default revisit, EditMode
goldens) not started.
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

## 2. Design: always-on follow (one seam, one new attack-lift hook)

Key invariant of the shipped layout: at rest the revealed card sits at a **constant offset**
from the shadow home ("bound pair", center-scale plan §1). The offset is in fact **exact for
any deckCount**: in `DeckFloatStackLayout`, `shadowHomeY − revealY = −(shadowOffset.y +
revealUpY)` and `shadowHomeX − revealX = shadowOffset.x + floatX` — `liftPx` and
`effectiveStepY·(count+1)/2` cancel out exactly. So the offset captured at drive start stays
correct even when bury/stage/generated cards change the deck count mid-reveal; only
OnValidate parameter edits need a re-drive (already in place). Therefore a follower that
places the shadow at `cardLivePose + restOffset` every frame reproduces the CURRENT static
pose exactly at rest, and continuously tracks every motion — one mechanism covers rest,
reveal flight, flip, attack, emphasize, pop-up, and the return flight. `RecorderAnimationPlayer`
and `SetFaceUp` stay untouched: they keep tweening the card, the follower just observes it.
The single new call-site is `AttackAnimationManager`, which drives the attack-lift weight on
the follower (see the lift bullet below).

Per-frame follow rule (LateUpdate, applied while driven):

- x/y = `card.position + offsetWorld * scaleK`, where `offsetWorld = shadowHomeWorld −
  revealHomeWorld` captured at drive start from the two known rest poses, and `scaleK =
  card.lossyScale / restScale` (`restScale` captured at drive start; == 1 at rest). Decided
  2026-08-16: the offset is directly proportional to the card's live size — the attack
  charge shrink (0.85x) pulls the shadow in, the wind-up grow (1.4x) pushes it out, the
  emphasize pulse breathes likewise. Rest identity preserved (scaleK == 1 at rest).
- Flip pivot correction: `posX = card.x + offsetWorld.x * scaleK * squashX`, so the squash
  collapses around the card center (the flip pivot) instead of the shadow's own center.
- z = pinned anchor-local z (existing front-gap formula, `TryDriveRevealBigShadow` :1796).
  The reveal-z re-clamp and arc-return z drift never leak into the shadow.
- scale = `card.lossyScale ⊙ bakedShadowHomeLocalScale`, then `x *= squashX`. Preserves the
  VISUAL-FIX(2026-08-16) baked-scale invariant: rest pose == `physicalCardDeckSize *
  revealScale * globalScale ⊙ baked` (the full `GetRevealZoneScale()` formula — the Iteration
  3 EditMode golden must pin this complete expression) on every re-drive; no compounding.
- rotation = card z-rotation (toggle `floatStackShadowFollowRotation`, default on; identity at
  rest = today's drive target). Confirmed ON for attacks (2026-08-16 user decision).
- **Lift offset (decided 2026-08-16)**: attack = card lifted; the emphasize pulse is a small
  lift. While lifted, the follow offset grows by a constant world-space anti-light vector
  `liftOffsetWorld = antiLightDir * floatStackShadowFollowLiftDistance * liftK` — ramping 0→1
  with the attack wind-up / emphasize scale-up, held through charge/overshoot, eased back on
  the return / scale-down. Anti-light default from the prefab's `PhysicalCardBigShadow` local
  offset (0.15, −0.15) = screen right-down (45°). The lift vector is world-space: NOT
  multiplied by squashX, but it DOES scale with `scaleK` like the rest offset (so the
  emphasize 1.2x pulse reads proportionally smaller than the attack 1.4x lift). `liftK` is
  driven via `CardPhysObjScript.SetBigShadowLift` by `AttackAnimationManager` (0→1 over the
  wind-up, →0 across the return phase, 0 in the finally) and by `RecorderAnimationPlayer`'s
  emphasize (0→1 over the first half, →0 over the second).
- alpha untouched by the follower — the drive fade-in and release fade-out stay as they are.
- `squashX = _flipRoot.localScale.x` (null → 1; the Start Card has no flip root).

## 3. Iteration 1 — core follow (flip + attack + emphasize + flights)

1. **Demo first** (project parity rule: retunes settle in the demo, then port): extend
   `docs/demo/CardStackRevealDemo.html` with a flip + attack-lunge simulation; validate the
   follow offset, pivot-corrected squash, and rotation follow on/off in the browser. Port the
   settled feel.
   **Resolved 2026-08-16 (user decision)**: attack = card lifted — the shadow grows an
   anti-light lift offset (wind-up ramp-in, hold through charge/overshoot, ease back on
   return), keeps following position + rotation, and its z stays pinned at the deck front.
   Demoting the shadow under the stack was explicitly rejected (the lifted topmost card's
   shadow cannot sit under other cards); the elevation-fade alternative is superseded by the
   lift offset. Demo controls: 浮起偏移 (distance) + 偏移方向 (angle) sliders.
   **Demo validation results (2026-08-16, Playwright, follow mode)**: rest identity exact
   (error 0.00px); attack tracked with error 0.000 through wind-up/charge/overshoot/return
   (scale 1.4x/0.85x and rotation in sync, lands exactly back at home); flip squash pivot
   exact (shadow sx = s·squashX, x = card.x + offset.x·squashX; pivot-off shows the wrong
   own-center read); static mode reproduces the shipped desync (shadow pinned at home
   mid-charge); mode-switch blend glides 0→1 in 0.45s with error 0 after; offset exactly
   (55, −14)px at 3/8/40 cards (count-invariant). Lift round (post-decision): liftK
   ramps 0→1 with the wind-up, holds 1 through charge/overshoot (lift vector (21.2, 21.2)px
   at the 45°/30px defaults), eases to 0 across the return; follow error 0.000 at every
   sample; z stays pinned at n+5 mid-attack; shadow lands exactly back at home. Scale round
   (scale-proportional offset, decided above): effective offset (47.9, 45.2)px at wind-up
   k=1.35, (32.5, 30.8)px at charge k=0.85, back to exactly (17, 15)px on landing; follow
   error 0.000 at every sample. Demo defaults synced to the user's 2026-08-16 tuning:
   floatX 17, upY −15, shadowBlur 0, shadowOffsetY 30 (Unity shipped defaults drift:
   `floatStackRevealFloatX` 16 / `floatStackRevealUpY` −14 — sync the Inspector values at
   port time).
   Screenshots: `.playwright-mcp/sim-follow-*.png` / `sim-static-attack-midcharge.png`.
2. **New `Assets/Scripts/UXPrototype/BigShadowFollower.cs`** — tiny component on the shadow
   GameObject, added at drive start, destroyed at restore. Fields: `source` (card root),
   `squashSource` (`_flipRoot`, nullable), `offsetWorld`, `bakedLocalScale`, `pinnedLocalZ`,
   `followRotation`, `restScale` (captured at drive start, for `scaleK`), `liftOffsetWorld` +
   `liftK` (lift weight, driven through `CardPhysObjScript.SetBigShadowLift` by
   `AttackAnimationManager` and the emphasize path in `RecorderAnimationPlayer`), and a blend
   weight 0→1 over `GetCombatScaledDuration(moveDuration)` so
   the shadow glides from its in-deck pose into the follow pose (matches today's tween-in at
   every `CombatAnimationSpeed.SpeedScale`). LateUpdate applies §2. If
   `source == null` (card destroyed/exiled mid-drive) → `Destroy(gameObject)`; this also
   cleans up the pre-existing invisible orphan-shadow leak when
   `RestoreBigShadowFromDrive`'s restore callback early-returns on a destroyed card.
   Tween-target constraint: run the blend tween on the follower component (e.g. DOFloat on
   the weight field) — never on the card transform (`CombatCardView` kills card tweens every
   frame during special animations), and never on the shadow transform
   (`RestoreBigShadowFromDrive`'s `t.DOKill()` would kill it while the follower must still
   track the return arc during the fade).
3. **`CardPhysObjScript.DriveBigShadowToPose`** — extend signature with `followOffsetWorld`
   (and the rotation flag). Keep the re-parent, the alpha fade-in, and the baked-scale
   capture; replace the three one-shot DOLocalMove/DOScale/DOLocalRotateQuaternion tweens with
   follower attach + blend-weight tween. Preserve the existing VISUAL-FIX(2026-08-16)
   baked-scale comment block, and ADD a new VISUAL-FIX(2026-08-16) block for the desync this
   plan fixes (Cause: the shadow was re-parented to the anchor with no follow mechanism, so
   flip/attack/emphasize/pop-up motions of the revealed card never reached it).
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

- ~~**Elevation fade**~~ — superseded 2026-08-16 by the attack lift-offset decision (§2):
  the "card lifts off" read is sold by the anti-light offset, not by fading.
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
  overshoot / return, scales 1.4x / 0.85x and rotates with the card, grows the anti-light
  lift offset through the attack, and lands exactly back at home with the offset settled.
- Emphasize pulse and hover pop-up on the revealed card — the shadow follows; the emphasize
  pulse also grows + settles the lift offset with its 1.2x scale pulse.
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
