# HP Numeric Display — Implementation Plan

Date: 2026-07-19
Status: Proposal (no code changed yet)
Reference demo: `docs/demo/HPNumericDisplayDemo.html` (motion design source of truth)
Related: `plans/plan-hp-compare-bar-2026-07-18.md` (same integration pattern)

## 1. Overview

Add a numeric HP display to the combat UI, matching the validated HTML demo:

- Big fraction-style number: current HP on top, thick divider, max HP below.
- Two instances from one component: **player HP** and **enemy HP**, selected by a
	`side` enum field. Same prefab, two scene objects.
- Pure presentation; no game-logic changes.
- Motion effects (validated in the demo):
	1. Adaptive counting (per-point ticks for small deltas, multi-point steps for
		big ones, ease-out tail).
	2. Digit roll rendering mode (odometer strips; serialized toggle, same tick
		loop drives both modes).
	3. Hit shake with rotation (amplitude scales with damage).
	4. Landing pop (scale overshoot when the count settles).
	5. Low-HP pulse (current <= 25% max and > 0).
	6. Zero-out (number drops through the divider and fades, gray "0" settles,
		divider snap-flash; revive cancels instantly).
	7. Max-HP glide (divider width tweens when the digit count changes).
	8. Same-frame start: the first count step runs synchronously with the shake.

## 2. Key Integration Decision: Displayed-HP Pipeline

Same decision as the compare bar: the display must NOT read `PlayerStatusSO.hp`
directly. `CombatInfoDisplayer` owns the pending-HP display queue
(`SnapshotHpDisplay` / `CommitHpDisplay`, see
`Assets/Scripts/Managers/CombatInfoDisplayer.cs:60-134`): logic updates HP
immediately, but the UI only commits each value when its attack animation lands.
Reading live HP would race ahead of the attack animations.

Approach:

- Poll the existing read-only accessors `GetDisplayedOwnerHp()` /
	`GetDisplayedEnemyHp()` every frame (they already exist for
	`CombatHPBarPresenter` — zero new code on `CombatInfoDisplayer`).
- Max HP is read live from `CombatManager.Me.ownerPlayerStatusRef.hpMax` /
	`enemyPlayerStatusRef.hpMax`. Max HP has no display queue anywhere (the
	existing HP text shows current HP only, see `DisplayStatusInfo`), so a live
	read is the only option and introduces no new inconsistency. Accepted side
	effect: while an `HPMaxAlterEffect` animation is in flight, the denominator
	moves immediately while the queue-frozen numerator catches up when the hit
	lands — the fraction is briefly stale.
- The component diffs each frame's displayed HP against its own stored value;
	all effect triggering derives from that per-side delta — same rule as the bar.

## 3. UI Structure (uGUI, per display instance)

```
HPDisplayRoot (RectTransform; anchored; NOT under a LayoutGroup — shake drives anchoredPosition)
├── CurrentRoot (RectTransform, width = maxDigits * digitWidth)
│   ├── CurrentPlain   TMP_Text (digitRoll = false)
│   └── CurrentStrips  RectTransform (digitRoll = true)
│       └── Digit[i]   RectTransform + RectMask2D (width = digitWidth, height = lineHeight)
│           └── Strip  TMP_Text, "0\n1\n...\n9" x 3 cycles (30 lines)
├── Divider (Image, height = 0.11 * fontSize, width = number width)
└── MaxRoot (same internal structure as CurrentRoot)
```

- Two scene objects, e.g. `PlayerHPDisplay` (suggested: left of combat HUD) and
	`EnemyHPDisplay` (right of combat HUD). Anchors configurable in the scene.
- raycastTarget = off on every graphic. Combat input is click-driven.
- Fixed digit width without CSS-style shrink-wrap: the roots get a FIXED width
	of `maxDigits * digitWidth` (maxDigits = max(digits of max HP, digits of
	current HP) at combat entry, so an overheal above hpMax cannot overflow the
	layout), TMP centered inside. Within a fixed digit count, numbers never shift
	the layout; no measuring loop needed. `digitWidth` =
	`TMP.GetPreferredValues("0").x` at setup.
- Digit-count growth mid-combat (e.g. max HP 99 -> 100): recompute `maxDigits`
	and rebuild the root widths once, in the same frame as the divider glide
	(section 4 step 7). A one-time relayout at the digit boundary is accepted —
	the fixed-width guarantee applies between digit-count changes.
- `Divider` is an `Image` (wire `Assets/Sprites/WhiteSquare.png`, same as the
	bar). Its width = current number width; tweened on change (the glide).
- Digit strips (roll mode only):
	- Line height must be exactly 1 em so `anchoredPosition.y = -index * em`
		lands on whole digits. Set `strip.lineSpacing = fontAsset.faceInfo.pointSize
		- fontAsset.faceInfo.lineHeight` (makes rendered line height == fontSize).
		Fallback if a font misbehaves: 30 separate one-line TMP children stacked
		manually at `-i * em`.
	- Canonical resting offset for digit d is `-(10 + d) * em` (middle cycle), so
		rolls crossing 0/9 always have room either way; after a roll completes,
		snap back to the middle cycle without a tween (same digit shown —
		invisible), exactly like the demo.
	- One strip TMP per digit slot: 6 strips per display worst case (3-digit
		current + 3-digit max), 12 TMP components per display. Cheap.
- Font: needs a heavy/black TMP Font Asset (demo uses Arial Black). Use the
	project's existing TMP font as a placeholder; swap when art direction decides.

## 4. New Components

### 4.1 `HPNumericCounter` (pure static, unit-testable)

Location: `Assets/Scripts/UXPrototype/HPNumericCounter.cs`. Ports the demo's
tick math verbatim so EditMode tests can pin it (same pattern as
`DeckCascadeLayout`):

- `StepSizeFor(remaining)` — 1 when `remaining <= easeOutPoints`, else
	`max(1, ceil(remaining / fastSteps))` with `fastSteps = floor(targetCountMs /
	stepMs)`.
- `StepDelay(remaining, easeOut)` — `stepMs` above the tail; stretched delays in
	the last `easeOutPoints` points.

### 4.2 `HPNumericDisplay` (presenter)

Location: `Assets/Scripts/UXPrototype/HPNumericDisplay.cs` (next to
`CombatHPBarPresenter`). One component per display instance.

Serialized:

- `side` (enum `Player | Enemy`) — selects the HP accessor and the
	`PlayerStatusSO` to read `hpMax` from.
- `gamePhaseRef` (`GamePhaseSO`, same asset as the bar), `canvas` (auto-resolve
	via `GetComponentInParent`), the Current/Max roots, divider Image, both TMP
	mode children, digit strip prefab references.
- `digitRoll` (bool, default false) — mode switch, applied at build time. The
	demo's runtime toggle was for evaluation; the shipped component picks one mode
	per instance in the inspector.
- All tuning constants with demo defaults (section 7).

Runtime ticker (Update-driven, NOT a coroutine and NOT a DOTween int-tween — the
adaptive step logic is a state machine):

- Accumulate `_elapsed += Time.deltaTime * CombatAnimationSpeed.SpeedScale`;
	when `_elapsed >= StepDelay(remaining)`, consume and tick one step. This keeps
	the count on the global combat animation speed like every other tween.
- New target: run the FIRST step immediately in the same frame (demo fix —
	shake and count start together), then continue on the ticker.
- Per tick: plain mode sets `text`; roll mode re-aims each strip with a short
	DOTween (`DOAnchorPosY(-k * em, rollDur)` with per-strip `DOKill` first; kill
	+ snap-to-canonical on complete). Direction = sign of the tick.

`Update()` flow (mirrors `CombatHPBarPresenter`):

1. Phase edge handling via `gamePhaseRef.Value()`:
	- Entering Combat: silent sync (displayed HP, max HP, digit widths, divider
		width) — no tweens, no effects; show the root.
	- Leaving Combat: kill all owned tweens, reset color/alpha/scale/offsets,
		hide the root. Nothing leaks into the next combat.
2. Poll displayed HP + live `hpMax`; set counter targets.
3. Classify from the per-side displayed-HP delta:
	- Drop -> shake (amplitude `min(10, 2 + dmg * 0.3)` px, rotation
		`min(2.2, amplitude * 0.22)` deg; px converted via `canvas.scaleFactor`,
		same as the bar). Cache the root's base anchoredPosition at setup (the
		bar's `_barRootBasePos` pattern) and restore it before every shake. The
		root-level `DOKill()` before a shake intentionally also kills a running
		landing pop — shake and pop share the root, and restart-on-new-hit is
		the desired semantics for both.
	- Rise -> count only (demo behavior; heal tint is an optional extension).
4. When the counter settles: landing pop (`DOScale` 1 -> 1.07 -> 1, 140ms;
	skipped when settling on 0).
5. Zero state: when the DISPLAYED value reaches 0 (i.e., after the count
	lands) -> zero-out sequence; when it leaves 0 -> cancel the sequence and
	restore color immediately. The cancel path is the reachable case: a
	displayed 0 committed mid-chain followed by a heal commit. Under
	`autoReveal` the combat-finish transition usually out-runs the count +
	zero-out and ExitCombat truncates the sequence — accepted behavior (see
	verification 4).
6. Low-HP pulse state from the logical (already-polled) values: starts the
	moment the hit lands, not when the count finishes.
7. Max-HP change: recompute digit width; tween the divider `sizeDelta.x` from
	its current width to the new one (240ms + opacity pulse), the
	ResizeObserver-equivalent — a width diff detected right here in Update. If
	the digit count changed, rebuild the root widths in the same frame (see
	section 3) so the relayout and the divider glide land together.

## 5. Demo-to-Unity Mapping

| Demo effect | Unity implementation |
|---|---|
| Adaptive counting | Update-driven ticker using `HPNumericCounter.StepSizeFor/StepDelay`; dt scaled by `CombatAnimationSpeed.SpeedScale` |
| Same-frame start | First tick runs synchronously when a new target is set |
| Digit roll | Per-digit masked strip TMP; per-tick `strip.DOKill()` + `DOAnchorPosY(-k * em, rollDur).SetEase(Ease.OutCubic)`; snap-to-canonical on complete; right-to-left stagger via `.SetDelay(fromRight * 0.045f)` |
| Fixed digit width | Fixed root widths (`maxDigits * digitWidth`); per-digit mask width = `GetPreferredValues("0").x` |
| Hit shake + rotation | `DOShakePosition(0.32f, new Vector3(strength, strength * 0.2f, 0), 10, 0, false, true)` + `DOShakeRotation(0.32f, new Vector3(0, 0, rot), 10, 0, true)` on the display root; strength/rot from the demo formulas, px -> anchored via `canvas.scaleFactor` |
| Landing pop | `root.DOScale(1.07f, 0.063f)` then `DOScale(1f, 0.077f)` (140ms total, peak at 45% matching the demo's keyframe offset); skipped when settling on 0 |
| Low-HP pulse | `TMP.DOColor(lowHpColor, 0.575f).SetLoops(-1, LoopType.Yoyo)` on the current number; kill + restore color on exit. CSS text-shadow glow has no uGUI equivalent — color pulse only (same intentional-deviation rule as the bar's alpha pulse) |
| Zero-out | Sequence on CurrentRoot: `DOAnchorPosY(+0.8em_down, 0.38s).SetEase(InCubic)` + `DOFade(0, 0.38s)` + slight scale down; then cancel-back and gray settle (`DOFade(1, 0.2s)` from `y = -0.08em`, color = gray); divider flash = `DOFade` blink x2 over 420ms. Revive: `DOKill` the sequence + restore color/alpha/offset at once |
| Max-HP glide | `DOTween.To(() => divider.sizeDelta.x, x => divider.sizeDelta = ..., newWidth, 0.24f).SetEase(Ease.OutQuad)` + divider `DOFade` pulse 0.35 -> 1 |
| Divider flash (zero) | See zero-out row |

Every tween gets `tween.timeScale = CombatAnimationSpeed.SpeedScale` (the bar's
`ApplySpeed` pattern, so `SetDelay` staggers scale too).

## 6. Change List (Estimated)

1. New `Assets/Scripts/UXPrototype/HPNumericCounter.cs` (~40 lines, pure static).
2. New `Assets/Scripts/UXPrototype/HPNumericDisplay.cs` (~350-400 lines, pure
	presentation).
3. New `Assets/Scripts/Editor/Tests/HPNumericCounterTests.cs` — golden values
	for `StepSizeFor` / `StepDelay` ported from the demo (same pattern as
	`DeckCascadeLayoutTests`).
4. Combat scene: build the section-3 hierarchy twice (`PlayerHPDisplay`,
	`EnemyHPDisplay`), wire references (including `gamePhaseRef` and side).
5. No changes to `CombatInfoDisplayer`, `HPAlterEffect`, the two-phase
	animation system, or any effect logic. Keep the existing HP text and the
	compare bar in place (run all three in parallel for observation; decide
	removal later).
6. `AGENTS.md`: add `HPNumericDisplay` to the UXPrototype list when implemented.

## 7. Constants (from the demo)

| Constant | Value |
|---|---|
| Tick interval / count window | 50ms base; fast phase aims to finish bulk in ~500ms |
| Ease-out tail | last 5 points, +35ms extra per step |
| Roll per step / stagger | 90ms + 50ms per digit passed; 45ms right-to-left stagger |
| Shake | 320ms; amplitude `min(10, 2 + dmg * 0.3)` px; rotation `min(2.2, amp * 0.22)` deg |
| Landing pop | 140ms, peak 1.07x at 45% (63ms up / 77ms down) |
| Low-HP threshold | current <= 25% of max and > 0 |
| Low-HP pulse | 1.15s period yoyo, red (`#c0392b`) |
| Zero-out | drop 380ms (ease-in cubic) -> gray settle 200ms; divider flash 420ms |
| Zero gray | `#8a8a90` |
| Max-HP glide | 240ms ease-out + opacity pulse |

## 8. Verification Plan

EditMode (fast gate):

1. `HPNumericCounter` golden tests: step size 1 for remaining <= 5; 100 -> 9
	fast steps + tail; ease tail delays match the demo formula.

Play Mode (`unity-card-playmode-test` strategy, `autoReveal` on except where
noted):

2. Single attack -> the number starts counting exactly when the hit lands
	(proves the queue integration), never earlier.
3. Multi-hit chain -> the number starts counting on the exact frame the HP
	text changes and always settles on the text's value (same accessor =
	strongest oracle; during the count the number intentionally trails the
	text). Shake amplitude grows with damage.
4. (Manual confirms, `autoReveal` OFF.) Drive one side to 0 -> zero-out
	sequence plays after the count lands; defeat path does not leak tweens into
	the result phase. Accepted behavior: with `autoReveal` on, the combat-finish
	transition out-runs the count + zero-out and ExitCombat's tween cleanup
	truncates the sequence — which is why this item must run with manual
	confirms.
5. Below-25% -> pulse starts on the landing frame and stops (with color
	restored) on heal above the threshold.
6. Heal -> count only, no shake, no phantom effects on the other display.
7. Max-HP change mid-combat -> divider glides; digit count change (99 -> 100)
	grows a digit without layout jumps.
8. Combat -> Shop -> Combat -> silent re-sync, no phantom damage/heal, no
	leftover tweens.
9. Digit roll on AND off: both modes produce identical settle timing and
	values (same tick loop).

## 9. Out of Scope

- Shield display (stays in the existing status text).
- Shop-phase HP display (combat-only, matching the bar and the existing text).
- Removing or restyling the existing HP text / compare bar.
- Heal color tint and floating delta numbers (demo extensions not selected).
- Final font/art direction for the numeric display.
