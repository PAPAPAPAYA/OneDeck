# Roboto Condensed Font Swap — Implementation Plan

Date: 2026-08-15
Status: Approved — implemented same day
Scope: Global TextMeshPro font chain. Card logic, effects, animation pipeline untouched.

## 1. Overview

Replace the Latin/digit typeface of the whole game from Liberation Sans to
**Roboto Condensed** (narrow grotesque, Google Fonts). Chinese glyphs keep
rendering via the existing SourceHanSansCN fallback. As a bonus the swap adds
**true bold** (currently `<b>` renders as faux/no bold because no bold SDF
exists anywhere in the chain):

- `RobotoCondensed-Regular SDF` becomes the TMP default font (Latin, digits, punctuation).
- `RobotoCondensed-Bold SDF` serves bold Latin via the font weight table.
- `SourceHanSansCN-Bold SDF` (new, from the in-project `SourceHanSansCN-Bold.otf`)
  serves bold Chinese.
- `SourceHanSansCN-Regular SDF` (existing) keeps serving normal Chinese.

## 2. Current State

| Item | Value |
|------|-------|
| Default font (TMP Settings `m_defaultFontAsset`) | `LiberationSans SDF.asset`, GUID `8f586378b4e144a9851e7b34d9b748ee` |
| LiberationSans SDF params | static, 86 pt, padding 9, 1024×1024, ASCII 32–126 + Latin-1 160–255 + 8192–8303 + €™□ |
| LiberationSans SDF fallback table | `[SourceHanSansCN-Regular SDF]` (only CJK coverage) |
| SourceHanSansCN-Regular SDF params | static, 41 pt, padding 5, 4096×4096, 6764 custom chars (`characterSetSelectionMode: 8`) |
| Baked font-GUID references | 5 UX prefabs (`PhysicalCard`, `PhysicalCardParent`, `MinionPhysicalCardParent`, `StartCard`, `EmptyCardSpace`) + `Assets/Scenes/GameScene.unity` |
| Runtime-created UI | Result stats panel, tooltips, card pops — inherit TMP Settings default font |
| Unity / TMP | 6000.3.9f1 (Unity 6), TMP package default |

Game text is Chinese (`cardDesc`), with English UI labels and digits — hence the
two-tier chain.

## 3. Font Source & License

- Family: Roboto Condensed, Google Fonts repo `google/fonts` → `ofl/robotocondensed`.
- License: **SIL Open Font License 1.1** (OFL.txt shipped in the repo; the family
  was relicensed from Apache 2.0 to OFL in the variable release). Free for
  commercial use; license file must be bundled with the fonts.
- The Google Fonts repo only hosts the variable face (`RobotoCondensed[wght].ttf`).
  Static instances are pulled from the Fontsource CDN (same OFL build):
  - `latin-400-normal.ttf` → `RobotoCondensed-Regular.ttf`
  - `latin-700-normal.ttf` → `RobotoCondensed-Bold.ttf`
  - `latin` subset covers 0000–00FF + general punctuation + €™□ — matches the
    LiberationSans character range 1:1.
- Destination: `Assets/TextMesh Pro/Fonts/RobotoCondensed/` (fonts + OFL.txt).

## 4. SDF Asset Generation

| Asset | Source | Params |
|-------|--------|--------|
| `RobotoCondensed-Regular SDF.asset` | RobotoCondensed-Regular.ttf | static, 86 pt, padding 9, 1024×1024, charset `32–126, 160–255, 8192–8303, 8364, 8482, 9633` (mirror LiberationSans) |
| `RobotoCondensed-Bold SDF.asset` | RobotoCondensed-Bold.ttf | same as above |
| `SourceHanSansCN-Bold SDF.asset` | SourceHanSansCN-Bold.otf (in project) | static, 39 pt, padding 5, 4096×4096, charset = the exact 6764 chars extracted from `SourceHanSansCN-Regular SDF.asset` `characterSequence` |

Output paths: `Assets/TextMesh Pro/Resources/Fonts & Materials/` (next to the
existing assets, so the TMP default-font path and fallback resolution stay
conventional). Generation via editor code (`TMP_FontAsset.CreateFontAsset` +
`AssetDatabase.CreateAsset`, material + atlas texture added as sub-assets),
matching `FontAssetCreatorWindow` behaviour.

## 5. Fallback & Weight Chain

```
Normal text:  RobotoCondensed-Regular SDF
                ├─ fontWeightTable[6] (Bold) → RobotoCondensed-Bold SDF   (true bold Latin/digits)
                └─ fallbackFontAssetTable    → [SourceHanSansCN-Regular SDF, SourceHanSansCN-Bold SDF]
Bold text:    RobotoCondensed-Bold SDF
                └─ fallbackFontAssetTable    → [SourceHanSansCN-Bold SDF, SourceHanSansCN-Regular SDF]
```

- TMP resolves `<b>` via `GetFontAssetForWeight(700)`, so bold text renders
  from the Bold assets and its CJK fallback (true bold Chinese).
- `SourceHanSansCN-Regular SDF` stays untouched.
- `LiberationSans SDF.asset` + its `- Fallback` sibling are left in place
  (unused) as rollback artifacts.

## 6. Reference Migration

1. Set `TMP Settings.asset` `m_defaultFontAsset` → `RobotoCondensed-Regular SDF`
   (editor-side, covers runtime-created UI).
2. Replace GUID `8f586378b4e144a9851e7b34d9b748ee` → new RobotoCondensed-Regular
   SDF GUID in: `PhysicalCard.prefab`, `PhysicalCardParent.prefab`,
   `MinionPhysicalCardParent.prefab`, `StartCard.prefab`,
   `EmptyCardSpace.prefab`, `Assets/Scenes/GameScene.unity`.
3. `Assets/_Recovery/0.unity` (historical backup scene) is left untouched.

## 7. Verification

- Editor console clean (no missing-glyph / shader errors).
- Play mode: card names + `cardDesc` (Chinese renders via SourceHanSansCN),
  `<b>` segments (bold Latin AND bold Chinese), UI labels ("ROUND START",
  "Shuffle"), digits (`$77`, HP numbers), result stats panel, card tooltip.
- Screenshot capture of combat + shop views.

## 8. Implementation Notes (deviations from plan)

- **TMP 6.3 static generation**: `TMP_FontAsset.TryAddCharacters` refuses to
  work on `AtlasPopulationMode.Static` assets (returns false, logs a warning),
  and the packing/render APIs (`FontEngine.TryPackGlyphsInAtlas`,
  `RenderGlyphsToTexture`) are `internal` in Unity 6000.3 — invisible to the
  C# compiler. The assets were therefore generated with creator-window parity:
  glyph collection → `TryPackGlyphsInAtlas` → `RenderGlyphsToTexture` invoked
  via reflection, tables/material assembled via private-field writes, then
  `CreateAsset` + sub-assets + `ImportAsset` + `ReadFontAssetDefinition`.
- **CN-Bold point size 41 → 39**: at 41 pt the bold face overflowed the 4096²
  atlas by 237 glyphs (bold CJK bitmaps are wider than regular); 39 pt packs
  all 6764 with zero overflow. TMP normalizes by `faceInfo.pointSize`, so the
  rendered size is unchanged.
- **Latin subset coverage**: the fontsource `latin` subset ships 211 of the
  308 requested code points (missing 96 = general punctuation 0x2000–0x206F
  beyond the subset, €/™ extras and `□` 9633 — the TMP missing-glyph
  placeholder). ASCII/Latin-1/digits are complete; any missing char falls
  through to SourceHanSansCN.
- **Rollback assets kept**: `LiberationSans SDF.asset`, `LiberationSans SDF -
  Fallback.asset` and `Assets/_Recovery/0.unity` are untouched.
- **Verification state**: all serialized wiring verified on disk (fallback
  tables, weight-table bold slot, TMP Settings default, zero remaining old
  GUID references). Play-mode visual check still pending — the Unity MCP
  session stalled during the final asset refresh, so the in-editor screenshot
  was not captured.

## 9. Rollback

`git checkout` the touched prefabs/scene + TMP Settings; LiberationSans assets
were never modified, so the old chain is fully recoverable.

## 10. Out of Scope

- Italic faces (no italic fonts in the chain; `<i>` keeps faux behavior).
- Variable-font axis tuning.
- Shop-specific or per-panel fonts.
