# Plan: Tag Tooltip Description Text + Card-Anchored Positioning

Date: 2026-07-26
Status: Implemented (2026-07-26)
Supersedes: the "follows mouse" tooltip canvas decision in `plans/plan-hover-card-tag-tooltip-2026-07-22.md`

## Goal

1. The hover tag tooltip shows, for each tag, an explanation text next to the tag name. Explanation texts are provided as `StringSO` assets so designers can edit wording without touching code.
2. The tooltip no longer follows the mouse. It anchors to the hovered physical card: right side of the card by default; if the panel would overflow the right screen edge, it flips to the left side.

## Current State

- `CardTagTooltip` (`Assets/Scripts/UXPrototype/CardTagTooltip.cs`) is a runtime-built singleton (Screen Space - Overlay canvas, sorting order 300). `UpdatePosition()` anchors the panel to `Input.mousePosition`.
- Tag text comes from `CardPhysObjScript.GetTagText()` ("[Tag] [Tag]", space-separated, skips `Tag.None`), shared with the in-card tag print.
- `StringSO` (`Assets/Scripts/SOScripts/StringSO.cs`) has `reset = true` by default, which clears `value` in `OnEnable`. Persistent description assets **must** set `reset = false`.
- `CardTagTooltip` is created at runtime (`EnsureInstance`), so it has no Inspector for serialized fields. The project convention for runtime-loaded config is `GameColorPalette`: asset in `Assets/Resources` + lazy static singleton.

## Approved Decisions

| Decision | Choice |
|----------|--------|
| Tag → description mapping storage | New `TagTooltipDatabaseSO` asset in `Assets/Resources`, loaded lazily (GameColorPalette convention) |
| Multi-tag display format | Per tag: bold `[Tag]` title line, explanation on the next line (revised 2026-07-26) |
| Vertical alignment | Centered on the card, clamped to the screen top/bottom |
| Missing description (StringSO null or empty) | Fall back to tag name only, no error |

## Design

### 1. TagTooltipDatabaseSO

- New file `Assets/Scripts/SOScripts/TagTooltipDatabaseSO.cs`:
	- `[CreateAssetMenu(fileName = "TagTooltipDatabase", menuName = "SORefs/TagTooltipDatabase")]`
	- `[Serializable] public class Entry { public EnumStorage.Tag tag; public StringSO description; }`
	- `public List<Entry> entries;`
	- `public StringSO GetDescription(EnumStorage.Tag tag)` — first matching entry, null if none.
	- Lazy static `Me` that `Resources.Load<TagTooltipDatabaseSO>("TagTooltipDatabase")` once (mirrors `GameColorPalette.Me`).
- Assets:
	- `Assets/Resources/TagTooltipDatabase.asset` — one entry per visible tag (`Linger`, `ManaX`, `DeathRattle`); `Tag.None` needs no entry.
	- One `StringSO` per tag under `Assets/SORefs/Strings/TagTooltips/` (new folder), each with **`reset = false`** so the text survives `OnEnable`.

### 2. Tooltip Text (one line per tag)

- Text building moves into `CardTagTooltip` (it owns the database dependency). `CardPhysObjScript.GetTagText()` stays untouched — the in-card tag print keeps its current single-line format.
- New build logic in `CardTagTooltip`:
	- Iterate `card.cardImRepresenting.myTags`, skip `Tag.None`.
	- Per tag: a bold `[Tag]` title line, then the explanation (from the tag's StringSO, when present and non-empty) on the next line; tag blocks separated by a blank line. Tags without a description show the title only.
	- Empty result → do not show (unchanged behavior).
- Rich text: keep the existing plain white style; tag name may reuse palette colors later — out of scope.

### 3. Card-Anchored Positioning

Replace the mouse-follow `UpdatePosition()`:

- **Anchor point**: the hovered card's world bounds from its `BoxCollider2D` (`_hoverCollider.bounds`, already cached in `BeginHover`); fall back to `transform.position` if the collider is missing. Convert to screen space with the camera that renders the physical cards (`Camera.main`; if null, fall back to the old mouse-follow behavior).
- **Horizontal** (the core requirement):
	- Default right: panel pivot `(0, 0.5)`, position x = cardRightScreenX + margin (16px).
	- Overflow check: `panelWidth * canvas.scaleFactor` — if `x + panelWidth > Screen.width`, flip to the left side: pivot `(1, 0.5)`, position x = cardLeftScreenX - margin.
	- If it overflows on both sides (panel wider than screen), prefer the left side and clamp x into the screen.
- **Vertical**: pivot y stays `0.5`, position y = card center screen y; then clamp y so the whole panel stays within `[0, Screen.height]` (top/bottom clamp per approved decision).
- `Update()` already repositions every frame while visible, so the panel tracks the card during pop-up/slot-in tweens with no extra work.
- The canvas stays Screen Space - Overlay (unchanged); `panel.position` accepts screen-pixel coordinates directly.

### 4. Unchanged Behavior

- Show/hide triggers, `hoverDelay`, hover z-arbitration, combat pop-up + autoReveal pause, face-down rule, force-hide conditions — all stay as implemented in the 2026-07-22 plan.
- Shop cards get the same card-anchored positioning (they use the same tooltip path).

## Implementation Files

| Change | File |
|--------|------|
| New database SO + lazy loader | `Assets/Scripts/SOScripts/TagTooltipDatabaseSO.cs` (new) |
| Database asset + per-tag StringSO assets | `Assets/Resources/TagTooltipDatabase.asset`, `Assets/SORefs/Strings/TagTooltips/*.asset` (new, `reset = false`) |
| Per-tag line building + card-anchored positioning | `Assets/Scripts/UXPrototype/CardTagTooltip.cs` |
| Expose collider/bounds accessor if needed | `Assets/Scripts/UXPrototype/CardPhysObjScript.cs` (small getter only) |
| Regression tracking | `docs/RegressionChecklist.md` (append row per visual-change rules) |

## Edge Cases

- Card hugging the right screen edge (e.g. reveal zone / shop slots near the edge) → flips left automatically.
- Very long description text: the panel uses `ContentSizeFitter`; consider `LayoutElement` max width or TMP `enableWordWrapping` with a preferred width if any description exceeds ~1/3 screen width (tune when writing actual copy).
- Camera swap / null camera: fall back to mouse-follow so the tooltip never breaks.
- `StringSO.reset` left at `true` on a description asset → text silently wiped; verify every new asset.

## Out of Scope

- Rich-text styling/colors for tag names inside the tooltip.
- Localization of description texts.
- Changes to the in-card tag print (`GetTagText`) or hover detection logic.
