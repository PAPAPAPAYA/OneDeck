# Recessed Empty Deck Slots (UIKitDemo §06 `.slot` → Unity)

2026-09-17 · status: **implemented** (prefab edited via `PrefabUtility.LoadPrefabContents`/`SaveAsPrefabAsset`; read-back verified against the plan table; play-mode look verification pending user)

Implementation notes: applied exactly per the change set — face recolored from the live `SlotRecess` ColorSO value (0.141, 0.208, 0.235), edge disabled, 4 strips added (serialized `z: -0.005` confirmed in YAML; read-back display rounds it to -0.01), texts recolored from the live `CardTextSoft` value (0.353, 0.400, 0.412), strips reuse the face's sprite material and the package Square sprite (`com.unity.2d.sprite`, 1×1 u @ PPU 256 — the sprite guid lives in a package, not Assets). `cardImRepresenting` stays null so `ApplyColor`'s placeholder early-return keeps the baked look authoritative.

Shop build-out step 1: restyle the empty deck slots from "face-down card" to the demo's recessed hole look (Guidelines §3.5: inner shadow at top-left, light source inverted, non-interactive; §3.6 references it for the shop deck panel).

## Spec (demo, `docs/demo/UIKitDemo.html` `.slot`)

- Background `--recess`; authority = `GameColorPalette.slotRecess` / ColorSO `SlotRecess` (§1.1 — demo hex #3f4441 never ported).
- `box-shadow: inset 3px 3px 0 rgba(0,0,0,.35), inset -2px -2px 0 rgba(255,255,255,.05)` — hard-edged (blur 0): dark inner L along top+left, faint white inner L along bottom+right.
- Label: single muted line, bottom-centered (13px, demo #7e837c stand-in).

## Current Unity state (verified 2026-09-17)

`Assets/Prefabs/UXPrototype/EmptyCardSpace.prefab`: root `EmptyCardSpace` (`CardPhysObjScript`, baked root scale 0.9) + children:

| Child | Size (local) | z | Look |
|---|---|---|---|
| `PhysicalCardFace` | 3.2 × 4.6 | 0 | WhiteSquare sprite, baked dark navy (0.198, 0.204, 0.226) |
| `PhysicalCardEdge` | 3.5 × 4.9 | +0.01 (behind) | WhiteSquare, cream — reads as the card border |
| `CardName` / `CardDesc` | TMP | −0.01 (front) | "Empty Space" / "Buy a card from shop", cream |

- `CardPhysObjScript.ApplyColor()` early-returns for placeholders (`cardImRepresenting == null`; comment: "Shop empty-slot placeholders keep their baked prefab colors") — **the look is 100% prefab-baked; runtime never recolors it**.
- Consumers: `ShopUXManager` only (initial spawn, `SpawnAdditionalEmptySpaces`, `SpawnEmptySlots` — all `Instantiate` this prefab; pop-in tween scales the ROOT so new children follow; `emptySlotZOffset` +0.1 = behind cards). Also referenced by dead `Assets/_Recovery/0.unity` (inherits the restyle harmlessly).
- Non-interactive already holds: no collider; `SetFaceDimmed` never targets placeholders.

## Change set (Option A — bake in prefab; recommended)

Editor-only prefab edit: **zero code, zero scene edit, no new assets**.

1. `PhysicalCardFace` recolor → current `SlotRecess` ColorSO value (read off `GameColorPalette` in Inspector).
2. Disable `PhysicalCardEdge` (cream border is the face-down-card idiom, contradicts the recess; keep the object for easy revert).
3. Add 4 inset strips: WhiteSquare sprite children of root, z = −0.005 (front of face, behind text). Conversion: card-local 3.2 u / 118 demo px ⇒ 0.02712 u/px; 3 px = 0.0814 u, 2 px = 0.0542 u. Face half-extents: 1.6 × 2.3.

| Strip | Size | Center | Color |
|---|---|---|---|
| top (dark) | 3.2 × 0.0814 | y = 2.3 − 0.0407 | black, α 0.35 |
| left (dark) | 0.0814 × (4.6 − 0.0814) | x = −1.6 + 0.0407, y = −0.0407 | black, α 0.35 |
| bottom (light) | 3.2 × 0.0542 | y = −2.3 + 0.0271 | white, α 0.05 |
| right (light) | 0.0542 × (4.6 − 0.0542) | x = 1.6 − 0.0271, y = −0.0271 | white, α 0.05 |

Dark strips are shortened so the two dark strips do not overlap (avoids an alpha-stacked darker corner pixel the CSS single-shadow does not have); same for the light pair.

4. Text: recolor both TMPs to the baked `CardTextSoft` value (muted recess label; baked color matches the established placeholder convention). Repositioning to the demo's bottom-center label layout: optional follow-up, keep current positions for now.

Color note: black/white α overlays are light-source shading math (per the demo CSS), not palette brand tokens — same treatment as the demo. Palette-purist alternative: two new ColorSO slots; not doing now.

## Play verification

1. Shop deck panel: empty slots read as recessed holes (dark top-left inner shadow, faint bottom-right light edge), no cream border, no bright card text.
2. Buy → card covers a slot; sell → recess shows through again.
3. Deck-size increase: new row pops in already recessed (pop tween untouched).
4. Hover an empty slot: no lift/press response (non-interactive); combat unaffected (slots exist only in the shop).

## Option B (fallback)

Tiny `RecessedSlotVisual` component (UXPrototype) on the prefab root: Awake applies `GameColorPalette.SlotRecessColor` / `CardTextSoftColor` to face/text at spawn; strip alphas serialized. Only if live palette authority is wanted (ColorSO retune propagates to freshly spawned slots). Costs a new file + manual csproj add; skipped — the placeholder path already chose baked colors.

## Shop build-out roadmap (after this step)

Per §3.6 mockup, remaining gaps in suggested order (each = own mini plan + 修改代码 gate):

1. HUD top bar inventory & consolidation: existing = HP bar, money, username label (Net), deck-slot meter card; likely missing = rarity odds, income, options button. Inventory first, then restyle/assemble into the top bar.
2. Panels: shop/deck as dark translucent overlays per mockup (background topo gray = HP display base, PhaseTransition family — transition motion postponed per user).
3. Reroll/exit feel: done (PhysButton UI mode, 2026-09-16).
