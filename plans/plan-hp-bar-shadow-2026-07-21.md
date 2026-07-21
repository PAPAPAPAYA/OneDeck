# HP Compare Bar Drop Shadow — Implementation Plan

Date: 2026-07-21 (rev 3 — review feedback applied: single tuning entry point,
manual verification by user, built-in Shadow rejection rationale)
Status: Proposal (no code changed yet)
Related: `plans/plan-hp-compare-bar-2026-07-18.md` (the bar itself)

## 1. Overview

Add a drop shadow to the combat HP compare bar (top-center HUD) so the bar reads
clearly against busy combat backgrounds.

**Rev 2 context:** Unity Editor is available again, so scene authoring and
visual tuning are back on the table. Verified live scene state
(`GameScene`, via MCP):

- `CombatHPBarPresenter` sits on GameObject `HPBarPresenter`.
- `barRoot` = `Combat Canvas/HPBarRoot`, sizeDelta `(925, 250)`,
	anchoredPosition `(0, -50)`.
- `barRoot` children, in render order: `PlayerSeg`, `EnemySeg`, `PlayerGhost`,
	`EnemyGhost`, `PlayerFlash`, `EnemyFlash` — no shadow child yet.

## 2. Chosen Approach: Scene-Authored Shadow + Runtime Safety Net

With the Editor available, author the shadow **in the scene** (better art
control, tweakable without recompiling), and keep a small runtime guarantee in
the presenter so the feature can never silently go missing.

**Why not Unity's built-in `Shadow`/`Outline` component on each segment?**
Each filled region would cast its own offset copy, producing a stray shadow
line at the player/enemy seam; during ghost-trail and flash animations every
overlay would additionally cast its own moving shadow, giving a fragmented
look. One stretched shadow behind the whole bar silhouette avoids both
problems, so per-segment `Shadow` components are rejected.

### 2.1 Scene changes (Editor)

Under `Combat Canvas/HPBarRoot`, add one child:

```
HPBarRoot
├── BarShadow   <- NEW, sibling index 0 (renders behind everything)
├── PlayerSeg
├── ... (existing 6 children unchanged)
```

`BarShadow` setup:

- `Image` component, `type = Simple`, `raycastTarget = false`.
- Anchors stretched `(0,0)-(1,1)`. Rough offsets are fine for edit-time
	preview — at runtime the presenter normalizes the rect from its own
	`shadowOffset`/`shadowPadding` fields (see 2.2), so the presenter is the
	single tuning entry point for offset, padding, and color.
- Sprite: create/import a soft rounded-square sprite
	(`Assets/Sprites/SoftShadow.png`, 9-slice not required — the bar is a fixed
	aspect; a simple radial/gaussian falloff square works). Until art exists,
	wire the existing `Assets/Sprites/WhiteSquare.png` (hard edge, still a valid
	shadow).
- Color: any placeholder (e.g. black, alpha 0.35) — overridden by the
	presenter's `shadowColor` at runtime.

Because `BarShadow` is a child of `HPBarRoot`, it automatically inherits:

- the damage shake (`barRoot.DOShakePosition`),
- combat enter/exit `SetActive` toggling,
- canvas scaling.

No animation or lifecycle code changes are needed for any of these.

### 2.2 Presenter changes (`CombatHPBarPresenter.cs`) — safety net + single tuning entry

Small, additive, and idempotent:

1. New serialized fields:
	- `public Image barShadow;` — wired to `BarShadow` in the scene.
		**Must NOT be added to the missing-reference guard at the top of
		`Awake()`** (`CombatHPBarPresenter.cs:69-76`): disabling the component
		when it is unwired would make the fallback path below unreachable.
	- `public Vector2 shadowOffset = new Vector2(4f, -4f);`
	- `public float shadowPadding = 6f;`
	- `public Color shadowColor = new Color(0f, 0f, 0f, 0.35f);`
2. In `Awake()`, after `EnsureFilledSprites()`:
	- If `barShadow == null`: create a `BarShadow` child at sibling index 0
		with an `Image` (`type = Simple`, `raycastTarget = false`), sprite =
		`playerSeg.sprite` (guaranteed non-null by `EnsureFilledSprites()`), and
		log a `Debug.LogWarning` telling the user to author one in the scene.
		Same self-healing philosophy as `EnsureFilledSprites()`
		(`CombatHPBarPresenter.cs:416-452`). The fallback is Awake-time only —
		it does not heal mid-session deletion (see 4.5).
	- Always (wired or fallback): normalize the shadow — stretched anchors,
		`offsetMin = (shadowOffset.x - shadowPadding, shadowOffset.y - shadowPadding)`,
		`offsetMax = (shadowOffset.x + shadowPadding, shadowOffset.y + shadowPadding)`,
		`color = shadowColor`, sibling index 0. This makes the presenter fields
		the single tuning entry point; the scene object only owns the sprite.
3. No tweens on the shadow, nothing added to `CleanupVisuals()`, no changes to
	`LateUpdate` flash mirroring.

Estimated code delta: ~35 lines in one file.

## 3. Editor Workflow (do these in order)

1. Create `Assets/Sprites/SoftShadow.png` (or generate via
	`manage_texture` MCP: radial gradient white square, import as Sprite).
2. In `GameScene`: add `BarShadow` under `HPBarRoot`, move to sibling index 0,
	configure per 2.1.
3. Wire `HPBarPresenter.barShadow` -> `BarShadow`.
4. Enter Play Mode, take damage, tune `shadowOffset` / `shadowPadding` /
	`shadowColor` live **on the presenter** (the scene Image's own offsets and
	color are overwritten at `Awake()`); bake the final values into the
	presenter component in the scene.

## 4. Manual Verification (performed by the user in the Editor)

The agent implements the code and scene changes but does **not** run these
checks — the user verifies each item by hand in Play Mode:

1. Enter combat -> shadow visible behind the full bar silhouette, offset
	down-right; all six segments render on top (sibling order check).
2. Take a hit -> bar shakes; shadow shakes with it, never detaches or lags.
3. Ghost trail / flash / low-HP pulse -> shadow shape unchanged (whole-bar
	silhouette, not per-segment).
4. Combat -> Shop -> Combat -> exactly one `BarShadow` child at all times
	(idempotence: no duplicates from the safety net).
5. Fallback path: remove the `barShadow` wiring (or delete the `BarShadow`
	child), then **re-enter Play Mode** so `Awake()` re-runs -> warning logged,
	fallback shadow appears, bar still fully functional. The fallback is
	Awake-time only: deleting the child mid-session is not healed until the
	next `Awake()`.
6. Screenshot before/after for visual confirmation.

## 5. Documentation Obligations

- If a visual bug is found during implementation, add a `VISUAL-FIX` block per
	`docs/VisualBugPrevention_Guide.md`.
- Append a row to `docs/RegressionChecklist.md` covering: shadow renders behind
	all segments, follows the shake, single instance after phase transitions,
	fallback works when unwired.

## 6. Out of Scope

- Shadows for the HP numeric display (`HPNumericDisplay`) or any other HUD
	element.
- Blurred/directional lighting-grade shadows (post-processing).
