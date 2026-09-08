# Render Layering Reference (Cards / World Text / UI)

What renders in front of what in the OneDeck GameScene, why the current config fights
itself, and the target layering spec. Source of truth for future HUD/board work, e.g.
the HP-display-over-card-face bug fixed on 2026-09-08/09.
Related: docs/DeckLayouts.md, docs/UIUX_Guidelines.md, docs/RegressionChecklist.md.

## 1. Rendering channels

| Channel | Renderer type | Sorting layer / order | Material queue | Depth rule |
|---------|---------------|----------------------|----------------|------------|
| World sprites (card face/image/edge/shadows/divider, empty slots) | SpriteRenderer | Default / 0 | 3000 | z: smaller z = closer (camera at z=-100) |
| World TMP text (card name/desc/price/rarity, "Shop:"/"Your Deck:" captions) | TextMeshPro (MeshRenderer) | Default / 0 | 3000 | z |
| Particles (wisp WIPs) | ParticleSystemRenderer | Default / 0 | 3050 (Mat_Wisp), 3000 | z |
| UGUI canvases | Canvas (Screen Space Camera, planeDistance=100) | Default / -1..0 | 3000 | canvas plane sits at world z = 0 |
| Runtime-created canvases | ResultStatsPanel (200), CardTagTooltip (300) | Default / 200/300 | - | - |

Four root canvases in GameScene:

- Global Canvas: order **-1**, ConstantPixelSize. Children: HPBarRoot (top HP compare
  bar), HPBarPresenter, background (RawImage contour lines).
- Combat Canvas: order **0**, ScaleWithScreenSize 1080x1920. Deck/Status zones,
  RevealedCardDisplay, EffectResultDispaly, CombatTipsDisplay, HP numeric displays
  (PlayerHPDisplayH / EnemyHPDisplayH active; vertical PlayerHPDisplay / EnemyHPDisplay
  inactive legacy), player/enemy icons, DamageFloaterPresenter + FloaterLayer
  (damage floaters are TextMeshProUGUI, i.e. UI layer), CombatIconPresenter.
- Shop Canvas: order **0**, ScaleWithScreenSize 1080x1920. Deck Display, Shop Display,
  General Info Display, Player Stats Display, Reroll / Exit buttons.
- Result Canvas: order **0**. ResultInfoDisplay.

Physical card back renderer is created at runtime and copies cardFace.sortingOrder (0).

## 2. The z=0 collision (root cause of the HP cover bug)

Camera at z=-100 with planeDistance=100 => every canvas quad is exactly at world z=0.

Deck layout formula (any mode): z = basePos.z - zOffset * index (FloatStack active,
zOffset = 0.5, deckPos base z = 0):

- deck bottom (index 0: bury target, last revealed) => z = 0: EXACT tie with canvas plane
- every other index => z < 0: in front of all UI
- reveal zone => z = -5: in front of all UI

Shop: card slots (shopItemPos / playerDeckPos, z = 0) tie with the canvas; empty-slot
offset z=+0.1 and duplicate-stack offset z=+0.02 (scene value) sit behind the canvas plane.

Card subparts (local z on the physical card prefab): image -0.02 (front), face 0,
edge/shadow +0.10..+0.25 (behind the canvas plane).

Tie condition: same sorting layer (Default) + same sorting order (0) + same z (0).
Draw order is then unspecified. Empirically it resolves differently between Edit Mode
and Play Mode and between runs: a probe card placed at z=0 drew over the HP text in one
capture, under it in another. Any card that lands on the deck bottom or a shop slot can
be covered by any overlapping order-0 UI (HP numerics at the deck corners, shop stats
above the deck). This, not a specific z value, is why the HP display "sometimes"
covers card faces.

## 3. Target spec (back -> front)

Final decision 2026-09-09 (supersedes the "1. HP numerics go behind cards" draft):
the HP compare bar stays behind cards, but HP numerics, player/enemy icons and damage
floaters go IN FRONT of cards - the numerics and icons are planned to become
clickable buttons, so they must never be covered.

| Order | Elements | Mechanism |
|-------|----------|-----------|
| -1 | background contour lines + HP compare bar | Global Canvas (unchanged, already -1) |
| 0 | physical cards (world sprites/TMP, z-sorted; card back copies face order) | unchanged |
| +1 | Combat/Shop/Result canvas content: HP numerics, player/enemy icons, damage floaters, reveal/effect popups, combat tips, shop buttons/stats, result info | phase canvas sortingOrder 0 -> 1 |
| 200 | result per-card stats panel (runtime canvas) | unchanged |
| 300 | card tag tooltip (runtime canvas) | unchanged |

Design intent:

- Cards are the board. The HP compare bar sits "world-adjacent" on/next to the physical
  stack, so it renders behind the cards (confirmed decision 2026-09-08, unchanged).
- HP numerics, player/enemy icons and damage floaters are interactive/reading HUD and
  must always be readable and clickable: they render in front of the cards
  (decision 2026-09-09). Numerics/icons become buttons later, so they also take
  UI-raycast priority by order.
- Background lines are decoration and stay below everything.
- One order step between every channel (-1 / 0 / +1): no z-tie remains possible for
  any card-vs-canvas pair, regardless of where a card lands (deck bottom z=0 included).

## 4. Changes to reach the target (scene-only, no C# changes)

1. Combat / Shop / Result Canvas sortingOrder: 0 -> 1.
2. Global Canvas: keep -1 (already behind cards; no change).
3. No nested canvases needed: HP numerics/icons sit on the Combat Canvas and move with it.
4. Shop empty-slot / duplicate-stack z: keep (+0.1 / +0.02 are safely behind the base
   card; canvas-covers-card is decided by order, not z, after change 1).
5. Append a row to docs/RegressionChecklist.md.

Residual risks to verify in playtest:

- UI-raycast precedence between canvases shifts with sorting order (EventSystem
  UI-raycast only; world colliders unaffected). Re-check shop button clickability and
  that the Combat Canvas content (damage floaters, reveal popups) does not block card
  clicks; the known ResultInfoDisplay blocker (2026-08-08) is untouched by this change.
- The reveal-zone card (z=-5) stays order 0; RevealedCardDisplay / CombatTips UI is now
  guaranteed above it (previously a tie) - behavior improvement.

## 5. Superseded plans

2026-09-08 plan: nested-canvas -1 for HP displays + emptySlot/duplicateStack z fixes.
Fully superseded by the order-based spec above: no nested canvas, no z edits, and HP
numerics go in front rather than behind the cards (reversed 2026-09-09 per the
buttons-planned requirement).
