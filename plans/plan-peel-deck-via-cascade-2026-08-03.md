# Peel Deck via Cascade Front Slot

Date: 2026-08-03
Status: Proposal (no code changed yet — awaiting explicit "修改代码")

## Goal

Rework the deck peel focus so that, after the cards in front of the focus card are
peeled down off-screen, the remaining deck segment is re-laid out with the **cascade
algorithm** using the focus card as the cascade front slot (cascadeIndex 0):

- Focus card lands exactly at the `physicalCardDeckPos` anchor with the maximum
	cascade scale (`physicalCardDeckSize * DeckCascadeLayout.ComputeScale(0, segCount, params)`).
- Cards behind it (unity indices `0 .. targetIndex-1`) continue along the cascade
	curve with `deckCount = targetIndex + 1` (the remaining segment count).
- `deckFocusTargetPos` and the whole `_deckFocusOffset` translation mechanism are
	retired — position comes from the cascade math, not from aligning to a marker.

## Confirmed Decisions (from user, 2026-08-03)

1. Peeled cards: keep current behavior — slide down off-screen with stagger.
2. Focus position basis: `physicalCardDeckPos` anchor + front-slot max scale.
	 `deckFocusTargetPos` is deprecated.
3. Segment cascade count: remaining count (`targetIndex + 1`), recomputed per focus.
4. Restore: also a cascade re-layout — tween every card to the full-deck cascade
	 positions AND scales (current restore only tweens positions).

## Core Design: Segment-Count Layout Seam

Replace the offset-based focus with a count-based focus:

- New runtime state: `private int _focusSegmentCount = 0;` (0 = not focused).
	`_deckFocusOffset` is deleted together with all its reference sites.
- While `_isDeckFocused`, the layout seams use `_focusSegmentCount` instead of
	`GetCascadeDeckCount()`:
	- `CalculatePositionAtIndex(int index)` (CombatUXManager.cs:1061) — with
		`deckCount = _focusSegmentCount`, every segment card keeps its own unity index
		and the focus card (index `targetIndex = segCount - 1`) maps to cascadeIndex 0,
		i.e. offset (0,0) at the anchor. Z formula is index-based and unchanged.
	- `GetDeckScaleAtIndex(int, int)` (CombatUXManager.cs:1021) — callers pass the
		segment count while focused; focus card gets the front scale.
	- `GetCascadeJitterScale` (CombatUXManager.cs:1046) — same segment count.
- `CalculateAnimationPositionAtIndex` (CombatUXManager.cs:1085) stays on the FULL
	deck count (its callers pass logical indices against `combinedDeckZone.Count`;
	`RecorderAnimationPlayer` restores deck focus before any deck-move request, so it
	never runs mid-focus). Document this split in the method comment.
- `GetCascadeDeckCount()` itself is untouched — non-focus callers (reveal flow,
	`MoveRevealedCardToBottom`, shuffle) keep their exact current behavior.

Because `GetFinalDeckPositionForCard(physScript, i)` (CombatUXManager.cs:~1109)
funnels through `CalculatePositionAtIndex`, the peel coroutines keep calling it and
inherit the segment layout for free.

## Changes per Method (all in `Assets/Scripts/UXPrototype/CombatUXManager.cs`)

### Runtime state / fields
- Add `_focusSegmentCount`; delete `_deckFocusOffset` and all usages
	(lines ~750, ~787, ~1064, ~1088, ~1160, ~1510, and the peel coroutines).
- `deckFocusTargetPos`: keep the serialized field (avoids scene/prefab asset churn)
	but stop reading it; retooltip as deprecated. `peelSlideDistance`,
	`peelCardDuration`, `peelStaggerDelay`, `deckShiftDuration`,
	`revealCardExitDistance`, `enablePeelDeck` unchanged.

### `StartPeelCoroutine(int targetIndex)` (line 1871)
1. `_isDeckFocused = true; _focusSegmentCount = targetIndex + 1;` (reveal-zone card
	 is exiting, so it does NOT occupy a cascade slot — the +1 from
	 `revealCardCountsAsDeckFront` must not leak into the segment count).
2. Reveal-zone card exit: unchanged (straight down by `revealCardExitDistance`,
	 minus the `_deckFocusOffset` term).
3. Peeled cards (`i > targetIndex`): peel from **current transform position**
	 straight down by `peelSlideDistance` with the existing stagger — NOT from
	 `GetFinalDeckPositionForCard`, because under the segment seam that helper would
	 clamp out-of-segment indices onto the focus slot.
4. Segment cards (`i <= targetIndex`): tween to
	 `GetFinalDeckPositionForCard(physScript, i)` (now segment-based via the seam)
	 with `deckShiftDuration`, AND `DOScale`/`SetTargetScale` to
	 `GetDeckScaleAtIndex(i, _focusSegmentCount)` so the focus card grows to the
	 front-slot scale.
5. Delete the offset-computation block and the two VISUAL-FIX(2026-07-17) comments
	 (superseded; strike the matching rows in `docs/RegressionChecklist.md` and add a
	 new row for this change, per `docs/VisualBugPrevention_Guide.md`).

### `TransitionFocusCoroutine(int newTargetIndex, int currentTargetIndex)` (line 1988)
- Same structure as today, minus offset math:
	- `_focusSegmentCount = newTargetIndex + 1`.
	- Cards newly peeled: from current position straight down (as above).
	- Cards restored into the segment / staying: tween position (seam) + scale to the
		new segment layout.
	- Update `_peeledCards` as today.

### `RestoreDeckFocusCoroutine()` (line 2089)
- Clear focus FIRST (`_focusSegmentCount = 0`, `_isDeckFocused = false` timing kept
	consistent with the current guard semantics — set the count to 0 at the point
	where `_deckFocusOffset` is zeroed today) so the seam returns full-deck cascade
	positions/scales.
- Tween every deck card to `GetFinalDeckPositionForCard` + `GetDeckScaleAtIndex(i)`
	(full count): peeled cards return from below with the existing stagger, segment
	cards slide/scale back with `deckShiftDuration`. This is the user-confirmed
	"restore = cascade re-layout" (adds the missing scale tween).
- Reveal-zone card return: unchanged.
- Tail (`UpdateAllPhysicalCardTargets()`, state clearing): unchanged.

### `AttackAnimationManager` / `RecorderAnimationPlayer`
- No changes expected. Attack return position goes through
	`CalculatePositionAtIndex(index)`, which under focus yields the segment position
	(focus card at anchor) — same contract the offset version provided.
	`RecorderAnimationPlayer` already restores deck focus before deck-move requests
	and skips popup for off-reveal Attack recorders.

## Edge Cases

- `targetIndex == count - 1` (focus card is deck top): no peels; with a reveal-zone
	card present the segment count is `count` vs layout count `count + 1`, so the deck
	correctly slides one cascade step forward when the reveal card exits.
- Single-card deck / `targetIndex == 0`: segment count 1, focus card at anchor.
- Card destroyed/exiled while focused: existing null checks in restore cover it.
- Dynamic arc midpoint (`TryGetArcMidpointPosition`, lines ~731/~782) uses
	`GetCascadeDeckCount()`; leave on the FULL count — deck-bound arcs only run after
	focus restore, and this keeps arc geometry stable. Revisit only if a future
	feature arcs cards mid-focus.

## Risks

| Risk | Mitigation |
|------|-----------|
| Peeled cards' positions computed through the segment seam get clamped onto the focus slot | Peel from current transform position, never through the seam |
| Stale `_deckFocusOffset` reference left behind | Grep `_deckFocusOffset` after edit; must be 0 hits |
| `UpdateAllPhysicalCardTargets` snapping scales mid-focus | Existing `_isDeckFocused` guard (line ~1561) unchanged |
| Scale not tweening back on restore | Restore tweens scale explicitly; `UpdateAllPhysicalCardTargets` at the tail re-syncs targets |
| Scene assets referencing `deckFocusTargetPos` | Field kept (deprecated), no scene rewire needed |

## Regression Checks (after implementation)

1. Off-reveal attack (e.g. SPIKE_SKELETON bury trigger): focus card lands on the
	 deck anchor at max scale; front cards peel off-screen; restore re-lays out the
	 full cascade including scales.
2. Chained focus switch deeper AND shallower (plan-deck-peel-focus-system test
	 cases 4/5): transition re-computes the segment and diffs peeled cards correctly.
3. Reveal-zone attack: no peel, unchanged behavior.
4. `enableCascadeDeckLayout = false`: legacy linear fan path must still work —
	 segment-count focus should degrade gracefully (evaluate whether to keep the old
	 offset path for legacy mode or let the count seam drive both; decide during
	 implementation, default: count seam drives cascade mode only, legacy mode falls
	 back to the current offset math kept in a small private helper).
5. Append a new row to `docs/RegressionChecklist.md`; strikethrough the obsolete
	 2026-07-17 peel-focus rows with `(Obsolete 2026-08-03)`.
