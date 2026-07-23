# Plan: Hover Card Tag Tooltip

Date: 2026-07-22 (revised 2026-07-23)
Status: Approved (Form B + Rule 1 + z-arbitration + combat hover pop-up), pending implementation

## Goal

When the player hovers the mouse over a physical card, display that card's tags in a readable floating tooltip. In Combat, hovering a face-up card additionally pops the card up and pauses `autoReveal` so the player can inspect it.

## Current State

- `CardPhysObjScript.UpdateTagDisplay()` (`Assets/Scripts/UXPrototype/CardPhysObjScript.cs:224`) writes `cardImRepresenting.myTags` into `cardTagPrint` every frame.
- Two blind spots:
	1. Face-down deck cards: face writers (including tags) are skipped while face-down (face-down rule), so deck cards show no tag info at all.
	2. Deep cascade cards: even face-up cards (kept face-up by the never-cover rule) are scaled down so much in the cascade tail that the tag text is unreadable.
- No hover detection exists anywhere in the project (no `OnMouseEnter`). The physical card prefab (`Assets/Prefabs/UXPrototype/PhysicalCardParent.prefab`) carries a `BoxCollider2D` on its root, and Unity mouse messages (`OnMouseEnter/Exit/Down`) work with 2D colliders — proven by `ShopCardView.OnMouseDown/OnMouseExit`.

## Approved Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Display form | **B: floating tooltip panel** | Form A (enlarging `cardTagPrint` in place) stays occluded by neighbours in the cascade deck and is unusable there. B matches the existing presenter pattern and is reusable in both Combat and Shop. |
| Face-down cards | **Rule 1: no tag display** | Deck contents are hidden information; showing tags on hover would be free scouting and change game balance. Implementation cost is one `isFaceUp` check, consistent with the existing face-down writer skip. |
| Cascade overlap arbitration | **Static hover owner + z comparison** | Unity fires `OnMouseEnter/Exit` for *every* collider under the cursor, so overlapping cascade cards would all trigger hover. A static `_currentHoverOwner` on `CardPhysObjScript` arbitrates: on enter, a card claims ownership if no owner exists or it is strictly closer to the camera (smaller world z — deck z is `basePos.z - zOffset * index`, so the front card has the smallest z); on exit, the owner releases. Non-owners do nothing. No raycast, no per-frame polling. |
| Combat hover on face-up card | **Immediate pop-up + pause autoReveal; tooltip still shown** | On owner enter (Combat phase, face-up): immediately save `CombatManager.autoReveal`, set it to `false`, and call `ICombatVisuals.PopUpCard(cardImRepresenting.gameObject)`. On exit: `SlotInCard` and restore the saved `autoReveal` value (never hardcode `true`). Pausing must be immediate — a delayed pause would let autoReveal advance during `hoverDelay`. The tooltip still appears after `hoverDelay`: pop-up is for reading the card face, tooltip is for stable tag text. |
| Reveal-zone card hover | **Tooltip + pause autoReveal, no pop-up** | The reveal-zone card is already fully displayed; pop-up is redundant. Pausing still applies so the player can inspect it under autoReveal. |
| Hover during animation playback | **Fully disabled** | While `CombatManager.isPlayingEffectAnimations` or `CombatManager.IsInputBlocked` is true, enter events are ignored (no pop-up, no tooltip). Exit/restore logic still runs so a hover interrupted by an animation start cleans up correctly. |
| Tooltip architecture | **Self-contained scene object, presenter convention** | `CombatIconPresenter` / `CombatHPBarPresenter` are standalone scene objects polling `GamePhaseSO`, not wired into the UX managers. `CardTagTooltip` follows the same convention and exposes a static accessor for `CardPhysObjScript` to call — no new serialized fields on `CombatUXManager` / `ShopUXManager`. |
| Tooltip canvas | **Screen Space - Camera, follows mouse** | A world-space panel would inherit the cascade scale/occlusion problems that ruled out Form A. Sorting order above the existing Combat/Shop canvases. |

## Design

### 1. Hover Detection & Overlap Arbitration

- Add `OnMouseEnter()` / `OnMouseExit()` to `CardPhysObjScript` (single implementation covering Combat, Shop, reveal zone, and popup cards — no per-scene duplication). No conflict with `ShopCardView`'s own `OnMouseExit`: Unity delivers the message to every component.
- Static arbitration (see decision table): owner = frontmost card under the cursor. Ownership can transfer when a closer card enters while an owner exists.
- Caveat: z comparison is only meaningful within one stack. Cross-zone overlap (reveal zone vs deck) does not occur in practice; if it ever does, the reveal-zone card should win by convention.
- A small `hoverDelay` (~0.15–0.3s) gates only the tooltip; a pending tooltip is canceled if the card exits or loses ownership before the delay elapses.

### 2. Combat Hover Behavior

- Phase check via `currentGamePhaseRef` (already on `CardPhysObjScript`).
- Face-down card → nothing (Rule 1).
- Face-up deck card (owner only) → immediate:
	1. Save `CombatManager.Me.autoReveal`, set to `false`.
	2. `CombatUXManager.visuals.PopUpCard(cardImRepresenting.gameObject)` (`CardPhysObjScript` already caches `CombatUXManager.me`; `PopUpCard`/`SlotInCard` exist on `ICombatVisuals`).
	3. Tooltip after `hoverDelay` (shared path with Shop).
- On exit (or ownership loss): `SlotInCard`, restore the saved `autoReveal`, cancel/hide tooltip.
- Reveal-zone card: skip pop-up (already displayed); still pause `autoReveal` and show the tooltip.
- Disabled during animation playback (see decision table).
- Pop-up/slot-in tweens inherit `CombatAnimationSpeed.SpeedScale` like other combat card animations; `hoverDelay` is fixed and unaffected.

### 3. Tooltip Panel

- New `CardTagTooltip.cs` as a self-contained scene object with serialized refs (panel, TMP text, `GamePhaseSO`) and a static accessor, following the `CombatIconPresenter` convention.
- On show:
	- Build tag text via a shared `GetTagText()` extracted from `UpdateTagDisplay()` — reused by both the in-card print and the tooltip (single source of truth; skips `Tag.None`, space-separated `[Tag]` format).
	- Empty tag text → do not show.
	- Panel follows the mouse.
- Force-hide on: mouse exit, ownership loss, card flip to face-down, card destroy, game phase change (polled in `Update` like the other presenters), animation playback start.
- Optional: subtle highlight on the hovered card (outline or slight scale pulse).

### 4. Face-Down Rule

- Hover display only fires when `isFaceUp == true` (face-down cards are skipped, matching Rule 1).
- A future "scouting" card effect that previews deck tags would be a separate gameplay feature and is out of scope.

### 5. Edge Cases

- Sweeping across the deck: ownership transfers only to closer cards; pending tooltips cancel — no flicker.
- Card animates away while hovered (reveal, bury, shuffle force-cover): the collider leaving the cursor fires `OnMouseExit`, which restores `autoReveal`, slots the card back in, and hides the tooltip. Flip/destroy additionally force-hide.
- Shop: tooltip only — no pop-up, no `autoReveal`. `ShopCardView` long-press purchase and enlarge-on-click are unaffected.
- `CombatAnimationSpeed.SpeedScale` does not affect the tooltip.

## Implementation Files (estimate)

| Change | File |
|--------|------|
| Hover detection + static z-arbitration + combat pop-up/autoReveal pause | `Assets/Scripts/UXPrototype/CardPhysObjScript.cs` (new `OnMouseEnter/Exit`) |
| Tag text extraction | `CardPhysObjScript.UpdateTagDisplay()` → shared `GetTagText()` |
| Tooltip panel | New `Assets/Scripts/UXPrototype/CardTagTooltip.cs` + scene Canvas (self-contained, presenter convention) |
| No changes needed | `CombatUXManager` (`PopUpCard`/`SlotInCard` already exist), `CombatManager` (`autoReveal` is a public bool) |
| Regression tracking | `docs/RegressionChecklist.md` (append row per visual-change rules) |

## Out of Scope

- Changing how tags are stored or granted (`myTags`, `Tag` enum untouched).
- Scouting / preview mechanics for face-down cards.
- Touch/platform input.
