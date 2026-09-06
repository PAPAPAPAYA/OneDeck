# Card Template v1.1 Port (UIKitDemo §03 → Unity)

2026-09-05 · status: **done** (template level; play-mode verification pending user request)

Port of the card-face layout from `docs/demo/UIKitDemo.html` section 03 (card template v1.1, 2026-08-18) into the single runtime card template `Assets/Prefabs/UXPrototype/PhysicalCardParent.prefab` (used by both shop and combat via `CombatUXManager`/`ShopUXManager.physicalCardPrefab`).

## User decisions (scope)

- Colors untouched: faction-based runtime recoloring via `GameColorPalette` stays as-is.
- Card face ratio untouched (3.2×4.6 card-local).
- Card image untouched (position/proportion as-is; it is a masked square in the upper card area, not full-bleed).
- Art-zone dark rounded box skipped — the existing `PhysicalCardBigShadow` look is considered sufficient.
- Buy/sell moved off long-press (UIKitDemo v0.6): separate future task (ShopCardView still long-press; card-side price print unchanged for now).
- Standalone tag row hidden: tags render inline via cardDesc `<tag:X>` + `CardTagTooltip` on hover.
- Status/life print (`CardStatusEffect`) not displayed for now (text still computed for logs).

## What changed

### `Assets/Prefabs/UXPrototype/PhysicalCardParent.prefab` (overrides on the nested `PhysicalCard` instance)

Card-local space: face spans x∈[-1.6, 1.6], y∈[-2.3, 2.3]; prints use RectTransform scale 0.2 (anchoredPosition = card-local xy).

| Print | Before | After |
|---|---|---|
| CardName | 17.25, Left/Top, y=-1, autosize on | 15.6, BottomLeft, no-wrap + Ellipsis, autosize OFF, box 10.5×2.5 @ (-0.332, -1.854) — bottom row left |
| CardDesc | 10, Left/Top, y=-0.8, margins set | 9.5, BottomLeft, Ellipsis, maxVisibleLines 2, margins 0, box 13.82×2.9 @ (0, -1.25) — bottom edge -1.54, hugs divider, grows upward |
| CardRarity | 10, Left/Top, y=-1.3 (shared row with tag) | TopRight, box 5×1.5 @ (0.71, -0.946) — right-aligned just under the art image's bottom-right (actual art bottom ≈ -0.74, not the demo's 56% line, since the image was kept as-is) |
| CardTag | Right/Top, y=-1.3 | GameObject deactivated |
| AttackPrint | 17.25, Right/Bottom, (0.07, -2.03), box 14×2.5 | 20.3 (1.3× name, per demo), box 5×2.5 @ (0.882, -1.854) — same bottom edge as name |
| CardDivider (new) | — | `SpriteRenderer`, same RoundedCorner sprite as the face, Simple drawMode, scale (2.764, 0.028), pos (0, -1.60, -0.06), color = text color @ 65% alpha (wired in `ApplyColor`) |

### `Assets/Scripts/UXPrototype/CardPhysObjScript.cs`

- `UpdateRarityDisplay`: `*` → `✦` (U+2726) stars.
- `UpdateCardDescription`: auto `"> "` prefix; empty desc hides the print (no dangling `>`).
- `UpdateTagDisplay`: always hidden (tags live in desc + tooltip).
- `UpdateStatusEffectDisplay`: `cardStatusEffectPrint` never activated (computed text kept for logging).
- `BuildFlipRoot`: `cardDivider` registered into `_faceElements` (hidden on card backs).
- `ApplyColor`: `cardDivider` follows the faction text color at 65% alpha.
- New field: `public SpriteRenderer cardDivider` (wired on the prefab root).

### Fonts — `Assets/Fonts/NotoSansSymbols2-Regular.ttf` (+ OFL-NotoSansSymbols2.txt) and `NotoSansSymbols2 SDF.asset`

- The demo's `✦` and the TMP ellipsis char `…` (U+2026) exist in NO project font atlas (RobotoCondensed Regular/Bold, SourceHanSansCN Regular/Bold are all subset-baked).
- New TMP font asset created from Noto Sans Symbols 2 (OFL): **static** atlas with exactly three glyphs: U+2726 ✦, U+2727 ✧, U+2026 ….
- Wired into `RobotoCondensed-Regular SDF` and `RobotoCondensed-Bold SDF` `fallbackFontAssetTable` (last position).
- `fontWeightTable` self-typefaces: `[Regular].italic`, `[Bold].regular`, `[Bold].italic` = self.

## TMP pitfalls hit (important for future font work)

- **Styled lookups never read the fallback's own character table.** In `TMP_FontAssetUtilities.GetCharacterFromFontAsset_Internal`, text with bold/italic style only checks the typeface assigned in `fontWeightTable`; if null, it jumps straight to that asset's fallbacks and returns null without ever checking the asset's own `characterLookupTable`. Regular text then renders via TMP_Text's retry-with-Normal-style path (synthetic bold/italic). The ellipsis-character setup (`TMP_Text.GetEllipsisSpecialCharacter`) has NO such retry — it must resolve through typefaces, hence the self-typeface wiring.
- **Self-typeface on a DYNAMIC font hijacks unrelated characters.** Any character lookup reaching the dynamic fallback re-bakes that character into it (our test strings polluted the atlas with `>`, `:`, `4`, `A`–`P`), and styled lookups then resolve those chars from the symbols font instead of the primary font with synthetic style. Fixed by baking only the 3 needed glyphs and switching the asset to Static.
- TMP's ellipsis character is hardcoded U+2026; when unresolvable, overflow silently degrades Ellipsis → Truncate on the live instance (console warning) — the prefab-stage instance keeps the switched value until reloaded; the asset on disk is unaffected.
- `TMP_Text.maxLines` does not exist in TMP 3.x (ugui 2.0) — use `maxVisibleLines`.
- The composite-unicode style/weight lookup cache is in-memory per session; font table changes need a domain reload (`EditorUtility.RequestScriptReload`) for reliable retesting.
- Fullwidth CJK punctuation (`：` U+FF1A, `，` U+FF0C, …) is in NO project font atlas — renders as □. cardDesc data already uses halfwidth punctuation everywhere (verified across prefabs); keep it that way. The UIKitDemo sample texts use fullwidth — do not copy verbatim.

## Verification (editor, prefab stage, no play mode)

- Rarity `✦✦✦` renders, right-aligned under art.
- Desc `> ` prefix, bottom-anchored above divider, 2-line clamp with injected `…` (tested with 38-char text → 2 lines + `并…`).
- Name bottom-left with `…` truncation (`名字很长的…`); attack bottom-right same baseline, larger.
- Bold Latin regression check: `AB` renders (synthetic bold via retry-normal) — broken mid-implementation by the dynamic-font hijack, fixed by the static + 3-glyph recreation.
- Console: zero errors after final state.

## Not done / follow-ups

- ShopCardView buy/sell is still long-press (`holdTimeRequired 0.5s`); the price print is still a card-below label, not the v0.6 price button. Needs its own change (user must explicitly request code modification).
- `CardStatusEffect` content (status icons, ❤ life) has no zone in v1.1 — decide a home before re-enabling.
- Art-zone dark box may still be wanted once real illustrations land (demo §03 open question: star readability on art).

## Follow-ups

- 2026-09-06 · Card-name overflow switched from Ellipsis to unlimited horizontal squash (`TMP_Text.characterHorizontalScale` via `CardPhysObjScript.FitCardNamePrint`; inert `m_charWidthMaxAdj` override removed). UIKitDemo v0.7 / UIUX_Guidelines v0.7 synced. Plan: `plans/plan-card-name-horizontal-squash-2026-09-06.md`. The desc print's 2-line `…` clamp is unchanged.
