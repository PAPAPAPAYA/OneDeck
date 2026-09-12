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

## 2. Why cards covered UI: depth write, not the z-tie (verified 2026-09-10)

Camera at z=-100 with planeDistance=100 => every canvas quad is exactly at world z=0.

Deck layout formula (any mode): z = basePos.z - zOffset * index (Float Stack active,
zOffset = 0.5, deckPos base z = 0):

- deck bottom (index 0: bury target, last revealed) => z = 0: tie with the canvas plane
- every other index => z < 0: in front of the canvas plane
- reveal zone => z = -5: in front of the canvas plane

Shop: card slots (shopItemPos / playerDeckPos, z = 0) tie with the canvas; empty-slot
offset z=+0.1 and duplicate-stack offset z=+0.02 (scene value) sit behind the canvas plane.

Card subparts (local z on the physical card prefab): image -0.02 (front), face 0,
edge/shadow +0.10..+0.25 (behind the canvas plane).

The 2026-09-08/09 model blamed the order-0/order-0 z-tie at z=0. That tie is real but
second-order. The dominant mechanism is a depth-test punch, verified against URP package
source (com.unity.render-pipelines.universal@7327e77c1cc2):

- Every Screen Space - Camera canvas is depth tested with ZTest LEqual (only Overlay
  canvases get ZTest Always via unity_GUIZTestMode). sortingOrder can never beat a failed
  depth test, so raising a canvas order does not stop cards covering its content.
- The card art and soft-shadow materials (Mat_RoundedCornerMask, Mat_RoundedCornerSoftEdge)
  were ShaderGraph assets on UniversalSpriteUnlitSubTarget. Under the 3D UniversalRenderer
  (PC_Renderer carries SSAO, so it is not the 2D renderer) sprites draw through the sprite
  FORWARD pass, whose render states are hardcoded to CoreRenderStates.Default (Cull + Blend
  only - no ZWrite descriptor; Editor/2D/ShaderGraph/Targets/UniversalSpriteUnlitSubTarget.cs).
  ShaderGraphs Generator emits only the descriptors present, so the generated pass carries
  no ZWrite line and ShaderLabs default applies: ZWrite On.
- The graph's m_ZWriteControl setting feeds only the Universal2D pass (2D Renderer) and
  the depth-only passes - never the sprite forward pass that actually runs here. Setting
  it to Force Disabled would have changed nothing.
- A card in front of the canvas plane therefore wrote camera depth over its whole art, and
  any later-drawn SSC canvas pixel at that screen position failed LEqual and was discarded:
  cards punched holes in HP numerics, damage floaters, the result panel and the tag tooltip
  wherever a card overlapped them. The deck-bottom z=0 tie only decided the ambiguous
  equal-depth rows.

Fix 2026-09-10 (asset-only): both card materials now use the package shader
"Universal Render Pipeline/2D/Sprite-Unlit-Default" (explicit ZWrite [_ZWrite] default Off,
Blend SrcAlpha, Queue=Transparent = same 3000 as before, texture still bound per-renderer
from the SpriteRenderer, saved _ZWrite set to 0). The icon material
Mat_RoundedCornerSoftEdge_forIcon kept the graph - it renders on the canvas plane itself,
where equal-depth LEqual passes. docs/RegressionChecklist.md row 90.
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
  This requires cards to not write camera depth - guaranteed since the 2026-09-10
  material swap in section 2.

## 4. Changes to reach the target (status)

1. Combat / Shop / Result Canvas sortingOrder: 0 -> 1. DONE (2026-09-09, committed).
2. Global Canvas: keep -1 (already behind cards; no change).
3. No nested canvases needed: HP numerics/icons sit on the Combat Canvas and move with it.
4. Shop empty-slot / duplicate-stack z: keep (+0.1 / +0.02 sit safely behind the base card).
5. Append a row to docs/RegressionChecklist.md (rows 84 and 90).
6. Card art / soft-shadow materials -> package shader "Universal Render Pipeline/2D/Sprite-Unlit-Default"
   (explicit ZWrite Off; DONE 2026-09-10, see section 2). Without this, change 1 alone
   cannot stop cards covering canvas content - the depth test outranks sorting order.

Residual risks to verify in playtest:

- UI-raycast precedence between canvases shifts with sorting order (EventSystem
  UI-raycast only; world colliders unaffected). Re-check shop button clickability and
  that the Combat Canvas content (damage floaters, reveal popups) does not block card
  clicks; the known ResultInfoDisplay blocker (2026-08-08) is untouched by this change.
- The reveal-zone card (z=-5) stays order 0; RevealedCardDisplay / CombatTips UI is now
  guaranteed above it (previously a tie) - behavior improvement.
- The 2026-09-10 material swap turns card art / soft-shadow blending from Opaque (alpha
  ignored) to SrcAlpha: sprite alpha is now honored, so the art corners render as cutout
  (the graphs corner SDF finally applies) and the soft shadow gains its alpha softness.
  Verify the card art and shadow look in playtest; revert = restore the two .mat m_Shader
  GUIDs and saved _ZWrite 1.
## 5. Superseded plans

2026-09-08 plan: nested-canvas -1 for HP displays + emptySlot/duplicateStack z fixes.
Fully superseded by the order-based spec above: no nested canvas, no z edits, and HP
numerics go in front rather than behind the cards (reversed 2026-09-09 per the
buttons-planned requirement).

2026-09-08 "z-tie instability" model (old section 2): partially superseded 2026-09-10 -
the dominant card-over-UI mechanism was the depth-test punch (sprite-subtarget ShaderGraph
writes depth; SSC canvas ZTest LEqual). The z-tie remains only for equal-depth rows.
