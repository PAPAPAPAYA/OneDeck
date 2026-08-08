# HP Numeric Display (Horizontal) — Implementation Plan

Date: 2026-08-08
Status: Implemented (2026-08-08)
Related: `plans/plan-hp-numeric-display-2026-07-19.md` (vertical version this is derived from)

## 1. Overview

A second numeric HP display variant, arranged horizontally as `12/20`:

- Row, left to right: current-HP digit group, a static `/` glyph, max-HP digit
	group at a smaller font (`maxFontScale`, default 0.75).
- Odometer digit-roll strips per digit, same mechanics as the vertical version
	(3x 0-9 cycles per strip, per-digit stagger, canonical line 10+d).
- Kept effects: adaptive counting (`HPNumericCounter`), hit shake + landing pop,
	digit-count growth (row re-centers with a glide).
- NOT carried over (user decision): low-HP pulse, zero-out. The plain TMP texts
	exist only for the Edit Mode preview; runtime always renders the strips.
- Edit Mode preview included (same OnValidate + delayCall pattern as the
	vertical version, added 2026-08-08).

## 2. Layout Math

- `em = currentPlain.fontSize` (authored 115), `maxEm = em * maxFontScale`.
- Per-group digit widths measured at their own font size via
	`GetPreferredValues("0")`; slash width via `GetPreferredValues("/")`.
- Row (centered on displayRoot pivot 0.5/0.5):
	`total = currentW + gap + slashW + gap + maxW`, `gap = em * groupGapEm`.
- Each digit group is a top-pivot (0.5, 1) box whose top sits at +lineHeight/2,
	so the single visible strip line lands exactly on the row center; the slash
	is pivot (0.5, 0.5) at y = 0.
- Line advance: same font-relative lineSpacing formula as the vertical version
	(`100*(pointSize-lineHeight)/pointSize`); one value serves both groups.
	`VerifyStripMetrics` re-measures per group at runtime.

## 3. Digit Growth

- `99 -> 100` style growth: `EnsureStripCount` grows the group from the left,
	then the row layout is recomputed, groups re-centered, and the three
	group/slash x-positions glide from their old values with
	`dividerGlideDuration` so the re-centering lands smoothly.

## 4. Scene Wiring

- New `PlayerHPDisplayH` / `EnemyHPDisplayH` holders, same transforms as the
	vertical ones (player: anchors 0,0 at (240, 291); enemy: anchors 1,1 at
	(-200, 0)). Hierarchy per side:
	`Holder > HPDisplayRootH > { CurrentRoot (CurrentPlain + CurrentStrips), Slash, MaxRoot (MaxPlain + MaxStrips) }`.
- Old vertical displays stay in the scene but are deactivated until the new
	version is confirmed.

## 5. Verification

- Edit Mode: preview shows `12/20` with the real font sizes and row layout;
	root size = 2x digitW + gap + slashW + gap + 2x maxDigitW, height = em.
- Play Mode: enter combat — numbers count on hits with shake/pop; 99->100
	growth glides; odometer rolls per digit.
