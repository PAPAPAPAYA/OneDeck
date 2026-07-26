# Damage Floater — Implementation Plan

Date: 2026-07-26
Status: Implemented (code + tests + scene wiring done; Play Mode visual check pending)
Reference demo: `docs/demo/DamageFloaterDemo.html` (motion design source of truth)
Related: `plans/plan-hp-numeric-display-2026-07-19.md` (same integration pattern;
floating delta numbers were out of scope there — this plan picks them up)

## 1. Overview

Add floating damage numbers to the combat UI, matching the validated HTML demo:

- When a player takes damage, a red `-N` text spawns at that side's attack
	target position (`AttackAnimationManager.playerTargetPos` /
	`enemyTargetPos` — the world position the attacking card charges to),
	plays **punch in -> hold -> float up + fade out**, then destroys itself.
- One presenter handles both sides (player / enemy), driven by the same
	displayed-HP pipeline as `CombatHPBarPresenter` and `HPNumericDisplay`.
- Pure presentation; no game-logic, effect, or animation-system changes.
- Motion effects (validated in the demo):
	1. Punch entrance: scale 2.2 -> squash 0.85 -> overshoot 1.08 -> settle 1.0,
		with opacity fading in over the first 32% of the punch and a slight
		upward drift (8 / 14 / 18 px waypoints).
	2. Hold: number stays fully visible, drifts up another 4 px.
	3. Fade: floats up to 62 px total while fading out and shrinking to 0.95.
	4. Horizontal spawn jitter (+-20 px) so burst hits don't perfectly overlap.

## 2. Key Integration Decision: Poll-Based Spawn

Same decision as the HP bar and the numeric display: the floater must NOT read
`PlayerStatusSO.hp` directly. `CombatInfoDisplayer` owns the pending-HP display
queue (`SnapshotHpDisplay` / `CommitHpDisplay`,
`Assets/Scripts/Managers/CombatInfoDisplayer.cs:60-134`): logic updates HP
immediately, but the UI only commits each value when its attack animation
lands. A floater reading live HP would race ahead of the attack animations.

Approach (mirrors `CombatHPBarPresenter.Update()` at
`Assets/Scripts/UXPrototype/CombatHPBarPresenter.cs:143-160` and
`HPNumericDisplay.Update()` at
`Assets/Scripts/UXPrototype/HPNumericDisplay.cs:203-239`):

- Poll `CombatInfoDisplayer.me.GetDisplayedOwnerHp()` /
	`GetDisplayedEnemyHp()` every frame and diff against stored per-side values.
- Negative delta -> spawn a floater showing the HP actually lost
	(`amount = -delta`) on that side. Positive delta -> ignored (heal floaters
	are out of scope, section 9).

Why polling instead of extending the animation system:

- **Timing is already correct.** `CommitHpDisplay` is wired as the `onHit`
	callback of the Attack animation (`HPAlterEffect.cs:165-177`, fired at
	`AttackAnimationManager.cs:294`), so the poll sees the delta on the exact
	frame the hit lands — frame-synced with the bar, the numeric display, and
	the camera shake, with zero new wiring.
- **Amount needs no new plumbing.** The displayed-HP delta IS the post-shield
	HP loss, identical to what the numeric display counts through (same
	accessor = strongest oracle). The alternative — adding a damage field to
	`AnimationRequest` and threading `totalDmg` through `HPAlterEffect` capture
	— would touch the effect/animation layers to carry a pre-shield number the
	rest of the UI doesn't show. Rejected.
- **Coverage caveat (accepted):** full-shield absorbs produce delta 0 -> no
	floater. This is consistent with the HP bar and numeric display, which also
	show nothing when HP doesn't move.

## 3. UI Structure & Components

### 3.1 Scene structure (on the existing combat HUD canvas)

```
FloaterLayer (RectTransform, full-stretch, sibling of the HP displays;
              NOT under a LayoutGroup; no Graphic of its own)
DamageFloaterPresenter (component, on any object under the same canvas)
```

- All floaters are runtime-created children of `FloaterLayer`; every graphic
	has `raycastTarget = false` (combat input is click-driven).
- Spawn base position: the attacked side's attack target, read at spawn time
	from `AttackAnimationManager.me.playerTargetPos` / `enemyTargetPos`
	(`AttackAnimationManager.cs:24-26`). No scene anchors needed.
- Position conversion: world position -> screen point via `Camera.main` ->
	local point in `FloaterLayer` via
	`RectTransformUtility.ScreenPointToLocalPointInRectangle` (camera = null
	for ScreenSpaceOverlay), plus random x jitter.
- The attack targets sit at/outside the camera frustum edges, so the final
	local point is clamped into `FloaterLayer` (`ClampToLayer`), reserving the
	float-up distance + text height above the spawn point so the whole
	animation stays on screen.

### 3.2 `DamageFloaterTimeline` (pure static, unit-testable)

Location: `Assets/Scripts/UXPrototype/DamageFloaterTimeline.cs`. Computes the
keyframe times and y waypoints from the tunable parameters so EditMode tests
can pin the demo math (same pattern as `DeckCascadeLayout` /
`HPNumericCounter`):

- `KeyframeTimes(punchIn, hold, fade)` -> `(s1, s2, s3, holdEnd, total)` where
	`s1 = 0.32 * punchIn`, `s2 = 0.58 * punchIn`, `s3 = punchIn`,
	`holdEnd = punchIn + hold`, `total = punchIn + hold + fade`.
- Y waypoints (demo px, up-positive in Unity): 8 at s1, 14 at s2, 18 at s3,
	22 at holdEnd, `floatUpDist` at total.

### 3.3 `DamageFloaterPresenter` (presenter)

Location: `Assets/Scripts/UXPrototype/DamageFloaterPresenter.cs` (next to
`CombatHPBarPresenter`). One component, both sides.

Serialized:

- `gamePhaseRef` (`GamePhaseSO`, same asset as the bar), `floaterLayer`,
	`canvas` (auto-resolve via `GetComponentInParent`).
- Optional `font` (TMP_FontAsset; null = TMP default, same as
	`CardTagTooltip`/`ResultStatsPanel` runtime text).
- All tuning constants with demo defaults (section 7).

`Update()` flow (mirrors `CombatHPBarPresenter`):

1. Phase edge handling via `gamePhaseRef.Value()`:
	- Entering Combat: silent sync of stored displayed HP for both sides — no
		floaters on stale diffs.
	- Leaving Combat: kill all owned tweens, destroy live floaters. Nothing
		leaks into shop/result.
2. Poll displayed HP per side; on negative delta, `SpawnFloater(side, -delta)`.

`SpawnFloater`:

1. Create a `GameObject` under `FloaterLayer` with `TextMeshProUGUI`
	(text `-N`, bold-ish size from `fontSize`, color
	`GameColorPalette.Me.damage.value`, alignment center, `raycastTarget =
	false`) plus a uGUI `Shadow` component (offset (0, -2), black) for the
	demo's `text-shadow`. Null-guard the palette like
	`ResultStatsPanel.FactionColor` (`ResultStatsPanel.cs:278-285`).
2. Add a `CanvasGroup` for alpha; set initial `alpha = 0`,
	`localScale = punchScale`, anchored position = anchor + jitter.
3. Play the timeline as one DOTween `Sequence` (section 4), wrapped in the
	bar's `ApplySpeed` pattern (`tween.timeScale =
	CombatAnimationSpeed.SpeedScale`, `CombatHPBarPresenter.cs:491-495`) so
	`SetDelay`-style offsets scale too.
4. `OnComplete`: destroy the floater GameObject.

No pooling: a handful of short-lived TMP objects per combat is cheap. Pooling
is out of scope (section 9).

## 4. Animation Timeline (demo -> DOTween)

Demo keyframes (`DamageFloaterDemo.html:190-197`, CSS y is down-positive;
Unity flips the sign so all waypoints below are up-positive):

| Time (fraction) | Opacity | Scale | Y (up px) |
|---|---|---|---|
| 0 | 0 | 2.2 | 0 |
| s1 = 0.32*punchIn | 1 | 0.85 | 8 |
| s2 = 0.58*punchIn | 1 | 1.08 | 14 |
| s3 = punchIn | 1 | 1.0 | 18 |
| holdEnd = punchIn+hold | 1 | 1.0 | 22 |
| total | 0 | 0.95 | floatUpDist (62) |

DOTween `Sequence` construction (px / `canvas.scaleFactor` -> local units,
same conversion as the bar's shake):

- `Insert(0, DOScale(0.85, s1).From(2.2))`,
	`Insert(s1, DOScale(1.08, s2 - s1))`,
	`Insert(s2, DOScale(1.0, s3 - s2))`,
	`Insert(holdEnd, DOScale(0.95, fade))`.
- `Insert(0, canvasGroup.DOFade(1, s1).From(0))`,
	`Insert(holdEnd, canvasGroup.DOFade(0, fade))`.
- Y as five `DOAnchorPosY` segments inserted at 0 / s1 / s2 / s3 / holdEnd
	with durations matching the gaps (8 -> 14 -> 18 -> 22 -> 62 px).
- Easings: the demo applies one global `cubic-bezier(.2,.8,.3,1)`. Accepted
	deviation: per-segment `Ease.OutQuad` for the punch phase and
	`Ease.InQuad` for the final fade — visually equivalent at 1.2 s scale;
	fine-tune in Play Mode against the demo side by side.

## 5. Demo-to-Unity Mapping

| Demo feature | Unity implementation |
|---|---|
| Click / Trigger / Burst spawn | Poll-based spawn on displayed-HP drop (section 2) |
| Card anchor at 55% stage height | `AttackAnimationManager.me.playerTargetPos` / `enemyTargetPos` (world), converted to layer-local via `Camera.main` |
| Punch scale/squash/overshoot | `DOScale` segments on the floater root (section 4) |
| Opacity keyframes | `CanvasGroup.DOFade` segments |
| Y drift waypoints | `DOAnchorPosY` segments, px / `canvas.scaleFactor` |
| Global cubic-bezier easing | Per-segment eases (accepted deviation, section 4) |
| `text-shadow` | uGUI `Shadow` component |
| Red `#ff3b30` | `GameColorPalette.Me.damage.value` (single source of truth; never hardcode) |
| x jitter +-20 px | `Random.Range(-jitter/2, jitter/2)` px / `canvas.scaleFactor` added to the spawn x |
| `animationend` -> remove element | Sequence `OnComplete` -> `Destroy` |
| CSS animation speed | `ApplySpeed(tween)` -> `tween.timeScale = CombatAnimationSpeed.SpeedScale` |
| Slider-tunable params + keyframe export | Serialized fields with demo defaults; export was a demo evaluation tool, not ported |

## 6. Change List (Estimated)

1. New `Assets/Scripts/UXPrototype/DamageFloaterTimeline.cs` (~30 lines, pure
	static).
2. New `Assets/Scripts/UXPrototype/DamageFloaterPresenter.cs` (~200 lines,
	pure presentation).
3. New `Assets/Scripts/Editor/Tests/DamageFloaterTimelineTests.cs` — golden
	values for `KeyframeTimes` ported from the demo defaults (same pattern as
	`DeckCascadeLayoutTests` / `HPNumericCounterTests`).
4. Combat scene: build the section-3.1 hierarchy, wire `gamePhaseRef` and
	`floaterLayer`.
5. No changes to `CombatInfoDisplayer`, `HPAlterEffect`,
	`RecorderAnimationPlayer`, `AttackAnimationManager`, or any effect logic.
6. `AGENTS.md`: add `DamageFloaterPresenter` to the UXPrototype list when
	implemented.

## 7. Constants (from the demo)

| Constant | Value |
|---|---|
| Font size | 30 px (demo default) |
| Punch scale / squash / overshoot | 2.2 / 0.85 / 1.08 |
| Punch in / hold / fade | 0.38 s / 0.45 s / 0.36 s (total 1.19 s) |
| Punch sub-keyframe ratios | 0.32 / 0.58 / 1.0 of punchIn |
| Y waypoints | 8 / 14 / 18 / 22 / 62 px |
| Final scale | 0.95 |
| X jitter | 40 px total range (+-20) |
| Color | `GameColorPalette.Me.damage.value` (demo `#ff3b30`) |

Golden test values (demo defaults): s1 = 0.1216, s2 = 0.2204, s3 = 0.38,
holdEnd = 0.83, total = 1.19.

## 8. Verification Plan

EditMode (fast gate):

1. `DamageFloaterTimeline` golden tests: keyframe times for the demo defaults
	match section 7; times are strictly increasing; y waypoints are
	non-decreasing.

Play Mode (`unity-card-playmode-test` strategy, `autoReveal` on except where
noted):

2. Single attack -> one floater spawns on the exact frame the hit lands
	(same frame the HP numeric display starts counting — the queue-integration
	proof), shows the same amount the display counts through, and is gone after
	~1.19 s / SpeedScale.
3. Multi-hit chain -> one floater per commit, each with its own amount; x
	jitter keeps burst floaters readable; no floater on the untouched side.
4. Damage fully absorbed by shield -> no floater (delta 0).
5. Heal -> no floater (out of scope); no phantom floater on the other side.
6. Combat -> Shop -> Combat -> silent re-sync: no floaters from stale diffs,
	no leftover floaters or tweens in the shop.
7. `combatAnimationSpeedScale` 2x -> floater lifetime halves; 0.5x -> doubles.
8. (Optional, manual) Compare side by side with the HTML demo: punch feel and
	float distance match at SpeedScale 1.

## 9. Out of Scope

- Heal floaters (positive deltas; the poll already sees them, so this is a
	small follow-up using `GameColorPalette.Me.heal.value` and `+N` text).
- Shield-absorb numbers / "BLOCKED" style feedback (no HP delta exists to
	trigger on; would need the rejected `AnimationRequest` plumbing).
- Floater pooling, per-amount styling (crit scale/color), and world-space
	floaters over individual cards (damage targets players; the HP display
	area is the anchor).
- Final font/art direction (TMP default placeholder, same as the HP numeric
	display's placeholder rule).
