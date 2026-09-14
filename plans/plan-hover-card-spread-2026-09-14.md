# Plan: Hover Card Spread (open a gap around the hovered deck card)

Date: 2026-09-14
Status: Implemented 2026-09-14. Defaults are OFF (both spread axes 0) — tune live in the Inspector.

## Goal

While a combat deck card is hovered (and therefore pops out for inspection), displace the
neighbouring deck cards away from it along a configurable axis, so the popped card reads as a
separate element instead of sitting on top of its neighbours.

Two knobs matter:

- How far neighbours move (magnitude).
- Which direction they move (X and/or Y), because the deck layouts stack differently
	(FloatStack stacks on Y at a near-constant X; Cascade/ArcLoop fan mostly on X).

## Verified current state

- Every deck-card display position comes from one seam:
	`CombatUXManager.GetFinalDeckPositionForCard(physScript, index)`
	(CombatUXManager.cs:1531) = `CalculatePositionAtIndex(index)` (pure layout) + per-card
	jitter (`_deckOffsetProvider.GetPositionOffset` x `GetCascadeJitterScale`). Editing this
	seam reaches every layout mode (`deckLayoutMode` is the single selector).
- `UpdateAllPhysicalCardTargets()` (CombatUXManager.cs:2536) walks `physicalCardsInDeck` and
	calls `SetTargetPosition/SetTargetScale/SetTargetRotation` on each card; the card animates
	its own transform with DOTween (`CardPhysObjScript.SetTargetPosition`, CardPhysObjScript.cs:552).
	So "change the targets and call `UpdateAllPhysicalCardTargets()`" is the whole animation
	work — no new tween code.
- The pop-up peak of a deck card is anchored on the same seam: `popupBasePos =
	GetFinalDeckPositionForCard(physScript, popupDeckIndex)` (CombatUXManager.cs:4213-4215).
- Hover ownership has exactly two entry points and one exit:
	`OnMouseEnter` (acquire / transfer) -> `BeginHover()` (CardPhysObjScript.cs:1737), and
	`EndHover()` (CardPhysObjScript.cs:1784) as the only release path (force-hide, cursor-left,
	ownership loss, shuffle reset, OnDestroy).
- Hover hit-test and z arbitration are **slot-anchored and X-free** (VISUAL-FIX 2026-09-09):
	`TryGetDeckSlotPosition` (CombatUXManager.cs:1547) plus the pure-Y band test
	(CardPhysObjScript.cs:1677). The 09-09 fix removed X from the test precisely because a
	pop-up slides out horizontally.
- A popped card cannot be moved by target tracking: `SetTargetPosition` early-returns while
	`SpecialAnimationPinsPosition` is true, and that property is true whenever `isPoppedUp`
	(CardPhysObjScript.cs:154). A relayout during an active hover pop-up therefore cannot fight
	the pop-up tween.
- `UpdateAllPhysicalCardTargets()` already refuses to run while the deck is peel-focused
	(`_isDeckFocused`, CombatUXManager.cs:2539).

## Design

### 1. The offset lives in the display layer, next to the jitter

Add the spread offset at the END of `GetFinalDeckPositionForCard`, i.e. display-only:

```csharp
return basePos
	+ _deckOffsetProvider.GetPositionOffset(physScript) * GetCascadeJitterScale(index, GetLayoutDeckCount())
	+ GetHoverSpreadOffset(index);
```

`CalculatePositionAtIndex` (the logical layout) stays untouched, so every effect-driven deck
move (`CalculateAnimationPositionAtIndex`, `CalculatePositionForPendingCard`, bury/stage arcs)
keeps targeting the un-spread slot. Nothing but the resting display pose and the hover pop-up
peak (which is intentionally anchored on the display pose) sees the spread.

### 2. Anchor = the hovered card, resolved live

`_hoverSpreadAnchor` stores the hovered **physical** card GameObject (the hover code already
lives on the physical card, so `gameObject` is the right handle). The centre index is resolved
from the anchor on every query instead of being cached:

```csharp
int center = physicalCardsInDeck.IndexOf(_hoverSpreadAnchor);
```

Deck reordering (`ApplyAnimationResult`, bury/stage, temp-card inserts) then cannot leave a stale
centre index behind, and there is no invalidation logic to get wrong. Cost is O(n) per card per
relayout with n <= 16, i.e. negligible.

### 3. Spread math

```csharp
int dist = index - center;
if (dist == 0) return Vector3.zero;                 // the hovered card keeps its own slot
int abs = Mathf.Abs(dist);
if (abs > hoverSpreadReach) return Vector3.zero;
float falloff = 1f - (abs - 1) / (float)hoverSpreadReach;  // nearest neighbour = 1, farthest = 1/reach
float sign = Mathf.Sign(dist);
return new Vector3(hoverSpreadX * falloff * sign, hoverSpreadY * falloff * sign, 0f);
```

Neighbours part on both sides of the hovered card and the amount decays with distance, so the
deck fans open around the hover rather than shifting as a block. The hovered card's own offset
is 0, which is what keeps its pop-up peak and its hover retention band anchored on its real slot.

### 4. Config surface (CombatUXManager, new `HOVER` header)

| Field | Type | Default | Meaning |
|-------|------|---------|---------|
| `hoverSpreadX` | float | 0 | World-unit X displacement of the nearest neighbour at full amount. Positive = right side goes right, left side goes left. 0 = off. |
| `hoverSpreadY` | float | 0 | Same on Y (the FloatStack stack axis). 0 = off. |
| `hoverSpreadReach` | int | 1 | How many neighbours per side are displaced. 1 = only the immediate neighbours, at full amount. 0 = feature off. |

All three live in the Inspector for play-mode tuning, and the feature is fully off until the
user sets a non-zero axis, so landing this cannot change the shipped look by itself.

Enabled predicate: `hoverSpreadReach > 0 && (hoverSpreadX != 0f || hoverSpreadY != 0f)`.

### 5. Trigger points (and the A -> B centre swap)

The spread is tied to hover **ownership**, not to the pop-up, because `hoverPopUpDelay`
(0.1 s in the scene) would otherwise leave a collapse-then-reopen gap on every handover.

- `BeginHover()`: after the combat-phase guard and after the reveal-zone early return, call
	`CombatUXManager.me?.SetHoverSpreadAnchor(gameObject)`. Shop phase and the reveal-zone card
	never anchor (reveal zone is not a deck slot, so `IndexOf` would return -1 anyway).
- `EndHover()`: call `CombatUXManager.me?.ClearHoverSpreadAnchor(gameObject)`.
- **Ownership transfer A -> B**: set the anchor to the winner **before** `loser.EndHover(...)`,
	so the centre is swapped in place. `ClearHoverSpreadAnchor` is identity-guarded — it returns
	early when the stored anchor is not the caller — so the loser's `EndHover` cannot drop the
	winner's spread, and the winner's own `BeginHover` call is a no-op on an already-correct
	anchor. Net effect: one relayout per handover, no intermediate collapse to the un-spread
	layout (no flicker). Applied in both transfer sites: `OnMouseEnter` and `UpdatePendingHover`.
- `CombatUXManager.ClearHoverSpreadAnchor(null)` forces a clear (used by the shuffle reset path
	so a stale anchor cannot survive a shuffle even if the owner reference was lost).

### 6. Interaction with the hover retention band (see the companion plan)

`TryGetDeckSlotPosition` returns the spread-aware position, so a **neighbour's** retention band
travels with the neighbour as it parts. That is the desired behaviour (the band must match what
the player sees). The **hovered** card's own offset is 0, so its band is unaffected.

## Non-goals / limitations (v1)

- No spread while the deck is peel-focused: `UpdateAllPhysicalCardTargets` early-returns while
	`_isDeckFocused`. The offset is still applied on the next unfocused relayout. Accepted; peel
	focus and hover inspection are not meant to be used together.
- Hovering a card outside the deck (reveal zone, minions) does not spread the deck.
- The spread does not scale with the slot's resting scale — it is a constant world-unit
	displacement, which the user tunes live. A scale-coupled variant is possible later if the
	back of a deep stack looks over-spread.

## Verification

EditMode: `GetFinalDeckPositionForCard` distance = layout + jitter when spread is off, so all
existing deck-layout golden tests (`DeckCascadeLayoutTests`, `DeckArcLoopLayoutTests`,
`DeckFloatStackLayoutTests`) must stay green with the defaults.

Play Mode (manual, folded into RegressionChecklist row 98): hover a mid-stack card and confirm
the neighbours part symmetrically by the configured amount; leave / move down a row and confirm
they glide back; quickly sweep the cursor across the stack (A -> B -> C) and confirm the parts
follow the owner with no collapse-then-reopen flicker; confirm the popped card is not displaced
and does not stutter while the neighbours move under it.

## Revision 2026-09-14 (ratified + coded 2026-09-14): spread direction semantics

User ruling after live tuning (X is one-sided, Y is screen-space parting):

- `hoverSpreadX` becomes ONE-SIDED: every affected neighbour (falloff + hoverSpreadReach
	unchanged) is displaced toward screen RIGHT (+X, enemy side) — the same direction the
	pop-up itself slides (popUpXOffset = +4). The per-side `sign` term is removed for X.
	Geometry check at the tooltip's own starting value: spreadX 8 puts the nearest
	neighbour's left edge at 8 - 1.55 = 6.45, clearing the popped card's right edge at
	4 + 1.55 = 5.55, so the corridor opens in the pop direction.
- `hoverSpreadY` keeps its magnitude but the sign is resolved in SCREEN space: a slot
	visually ABOVE the anchor gets +Y, a slot visually BELOW gets -Y. Implement by
	comparing `CalculatePositionAtIndex(index).y` vs `CalculatePositionAtIndex(center).y`
	instead of the raw index sign. Reason: FloatStack (stepY = +14 > 0, DeckFloatStackLayout.
	ComputeSlotOffset) maps higher index to LOWER on screen, so the shipped index-based sign
	renders as a SQUEEZE toward the hovered card — the opposite of the intent; the slot-Y
	comparison fixes it and stays layout-agnostic. Edge: Cascade slots share a base Y, so
	Mathf.Sign(0) = +1 pushes every neighbour up; harmless there (Y spread targets FloatStack).
- Note: `screen right` = world +X (scene: PlayerIcon anchored bottom-left, EnemyIcon
	top-right; camera unrotated).

## Hover acquisition kill (diagnosed 2026-09-14, fixed 2026-09-14)

With the spread ENABLED, an ownership transfer re-centres the spread on the winner, which
zeroes the winner's own offset and retargets it to its un-spread rest slot while the cursor
is standing on its DISPLACED pose. The first UpdateHover cursor-left poll (band anchored on
the rest slot) then fails, EndHover fires before hoverPopUpDelay (0.1s) elapses, and
PopUpCard is never called; the collapse-retrigger loop can repeat. Fix direction (agreed in
review): let unpopped hover holders (owner pre-pop + pending cards, shared IsCursorOverCard)
also pass on a live-collider OverlapPoint hit; popped owners keep the 09-09 slot band. LANDED 2026-09-14: the fallback is in CardPhysObjScript.IsCursorOverCard
(VISUAL-FIX(2026-09-14) block); checklist row 100.
