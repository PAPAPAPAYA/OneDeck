# Physical Card Color Single-Source Refactor: CardPhysObjScript reads GameColorPalette directly

Date: 2026-08-17
Status: Implemented 2026-08-17 (commit d4bc30e, "feat(ux): GameColorPalette single source for card and overlay colors"). The tag tooltip / result stats panel overlay colors listed as out of scope here were folded into the same change (regression checklist rows 70-71). Dead legacy color assets (`Red.asset`, `Red 2.asset`, `FloaterPlayer.asset`, `FloaterEnemy.asset`) still pending cleanup. `GameScene.unity` scene drift was swept into the same commit although unrelated to this plan.

## Goal

Remove the 8 serialized ColorSO fields on `CardPhysObjScript` (and the duplicated wiring in the 5 card template prefabs); card colors (face / back / text / tint) read `GameColorPalette` static properties at runtime — matching the existing HUD component pattern. Also removes the edge color concept and adds start card color wiring.

## Confirmed decisions

1. `palette.opponentCardColor` takes the uncommitted workspace value → `Red 1.asset` (bright red 0.659/0.114/0.153); enemy card faces become bright red after the refactor.
2. `StartCardParent` renderer gets wired; Start card appearance becomes palette-driven (initial values = current baked colors, appearance unchanged).
3. New asset initial values = current baked colors: `StartCardColor` = gold (0.893, 0.613, 0, 1), `StartCardTextColor` = near-black (0.039, 0.051, 0.094, 1).

## Change list

### 1. `Assets/Scripts/SOScripts/GameColorPalette.cs`
- "Physical Card" group: remove `ownerCardEdgeColor`, `opponentCardEdgeColor`; add `startCardColor`, `startCardTextColor`.
- Add 8 static properties (following the HUD white-fallback pattern): `OwnerCardColor`, `OpponentCardColor`, `OwnerTextColor`, `OpponentTextColor`, `StartCardColor`, `StartCardTextColor`, `InfectedTintColor`, `PowerTintColor`.

### 2. New assets in `Assets/SORefs/Colors/`
- `StartCardColor.asset` (gold 0.893/0.613/0), `StartCardTextColor.asset` (near-black 0.039/0.051/0.094), with descriptions.

### 3. `Assets/Resources/GameColorPalette.asset`
- Remove the 2 edge references; add `startCardColor` / `startCardTextColor` references.
- Fix the broken golden test along the way: `hpNormalEnemy` back to `GreyWhite 2.asset` from `GreyWhite.asset` (per the 2026-08-15 unified commit intent; enemy HP digit color slightly darker, no other impact).
- Keep `opponentCardColor` → `Red 1.asset` (the current uncommitted state).

### 4. `Assets/Scripts/UXPrototype/CardPhysObjScript.cs`
- Remove fields: `cardEdge` (L31), 6 color fields (L42-47), `infectedTintColor` / `powerTintColor` (L57/L64). Keep the intensity/duration tuning (`infectedTintIntensity`, `powerTintIntensity`, `tintDuration`, `tintTransitionSpeed`) and the face art fields (`ownerCardFaceSprite`, `opponentCardFaceSprite`).
- `BuildFlipRoot` (L656): remove the L666 color null-guard and its `VISUAL-FIX(2026-07-24)` comment block (palette has no null concept); keep `cardFace == null → return` as the flip master gate; `CardBack` initial color reads `GameColorPalette.OwnerCardColor`; stop adding `cardEdge` to `_faceElements`.
- `ApplyColor` (L1034) refactor:
  - Start card branch: `isPhysicalStartCard` → base `StartCardColor`, text `StartCardTextColor` (placed before the `cardImRepresenting == null` early return).
  - Normal cards: keep the `myStatusRef` ownership check; the `isOwner` flag selects `OwnerCardColor` / `OpponentCardColor`; text color uses the `isOwner` flag (replacing the fragile `baseFaceColor == ownerCardColor.value` value comparison — it mis-picks when both assets are equal and breaks after a recolor).
  - Tint reads `GameColorPalette.InfectedTintColor` / `PowerTintColor`; remove the `cardEdge.color` write.
- `ApplyBackColor` (L967): read palette statics; Start card uses `StartCardColor`.
- No `GameColorPalette.Changed` subscription (physical cards are runtime instances; no Edit Mode preview value).

### 5. Prefab YAML (5 files, all under `Assets/Prefabs/UXPrototype/`)
- `PhysicalCardParent.prefab`: remove 8 color field lines (L611-616, 619, 621). Keep the `PhysicalCardEdge` GO (baked inactive, never activated → same invisible-as-now behavior).
- `MinionPhysicalCardParent.prefab`: remove 8 color field lines + clean up stale `opponentCardColor.r/g/b` instance overrides pointing at a deleted fileID (L529-537; Unity would drop them on next import anyway).
- `StartCardParent.prefab`: remove 8 color field lines; wire `cardFace` (the nested `StartCard.prefab`'s `PhysicalCardFace` renderer), `cardNamePrint`, `cardDescPrint` (stripped refs, following `PhysicalCardParent`'s style). Start card `isFaceUp` defaults true and is never flipped → `BuildFlipRoot` leaves the Awake-built appearance unchanged; only colors become palette-driven.
- `EmptyCardSpace.prefab`: remove 8 color field lines (its inner component's `cardImRepresenting` is null, `ApplyColor` early-returns; baked appearance unchanged).
- `EmptyCardSpaceParent.prefab`: `cardFace` / `cardEdge` → `{fileID: 0}` — the empty slot keeps going through the `cardFace == null` flip gate, consistent with the documented `VISUAL-FIX(2026-07-24)` behavior (no FlipRoot / card back).
- Keep original line endings in all YAML edits; no wholesale conversion.

### 6. `Assets/Scripts/UXPrototype/ResultStatsPanel.cs`
- `FactionColor` (L369-375) uses the new statics `GameColorPalette.OwnerCardColor` / `OpponentCardColor` (behavior unchanged; palette is authoritative, the ENEMY title follows bright red automatically).

### 7. `Assets/Scripts/Editor/Tests/GameColorPaletteWiringTests.cs`
- Add `CardFields_AllWired` and `CardFields_WiredToCanonicalAssets` (goldens: ownerCardColor → GreyWhite.asset, opponentCardColor → Red 1.asset, ownerTextColor → Black.asset, opponentTextColor → OpponentTextColor.asset, startCardColor → StartCardColor.asset, startCardTextColor → StartCardTextColor.asset, infectedTint → InfectedTint.asset, powerTint → PowerTint.asset).
- Extend `ResolvedColors_MatchWiredAssets` to the 8 new static properties. The `hpNormalEnemy` golden turns green after the asset fix.

### 8. Docs
- `docs/RegressionChecklist.md`: append a row (2026-08-17 card colors single-source refactor; regression points: shop/combat both sides' face & back colors, Start card gold appearance, empty slots, infected/power tint, minion, `ResultStatsPanel` title colors).
- This plan is archived at `plans/plan-card-colors-palette-single-source-2026-08-17.md`.

## Verification

- Run EditMode tests (`GameColorPaletteWiringTests` all green + no regression in existing tests).
- Zero compile errors; check the 5 prefabs serialize without leftover fields.
- No Play Mode (per AGENTS.md); visual results (shop / combat / Start card / empty slots) confirmed by the user in the editor. Expected: nothing changes visually except the enemy card face becoming bright red.

## Out of scope (untouched)

- `GameScene.unity` uncommitted changes (anchoredPosition/localScale, unrelated to this plan).
- Fully deleting the unreferenced `Red.asset` / `Red 2.asset` / `FloaterPlayer.asset` / `FloaterEnemy.asset` assets (can be a separate cleanup round; this change only guarantees no new dead references).
- Hardcoded colors in `CardTagTooltip` (L130/142, tag tooltip colors — not card body colors). *(Addressed in the same 2026-08-17 commit via the Overlay Panels palette group; see regression row 71.)*
