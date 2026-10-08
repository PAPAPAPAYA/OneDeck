# Plan: HP Name Plate (merged avatar + HP display) — 2026-10-07

- **Date**: 2026-10-07
- **Status**: Implemented 2026-10-07 (steps 1-6 done; EditMode suite 743/742/0/1-skip green). Play verification 2026-10-07 (frame-dense flight probes + screenshots): transitions/mirror handoff/Result visibility/shop mirror values+name all correct; four deviations found — fixes specced in `plans/plan-hp-name-plate-fixes-2026-10-07.md` (anchor clipping, shadow-slab sign flip, mirror HP/Slash empty texts, fallback viewportY). 2026-10-08 style alignment (user request): both canvas displays re-tuned to the hand-retuned mirror's proportions (em=1.0 rebase) — scene-only serialized fields, both sides: `maxFontScale` 0.47→0.5, `maxOffsetYEm` −0.215→−0.19, `nameScaleEm` 0.32→0.35, `nameTrackingEm` 0.05→0, `nameSidePadEm`/`shadowExtendXEm`/`shadowInsetXEm` →0.1, `facePadYEm` −0.02 + `facePadXEm` 0.1046 (solved) → face exactly 3.2×0.96em = mirror 4×1.2@0.8, `labelGapEm` 0.11→−0.04 (HP ink touches digits, mirror rhythm). Enemy palette kept per-side (red face); code defaults untouched (still 0.47/0.32/0.16 — new instances would need re-tune or a future default bump).
- **Request (user, 2026-10-07)**: replace the player icon + HP numerical display with the mockup style — no avatar, username only, merged into the HP display by extending the shadow to hold it; shadow direction uniformly down-right per the UI guideline; the username region is not a colored area but the same shadow as the other buttons, extended. Deliver HTML demo first, then update UI docs, then this plan.
- **Design source**: `docs/demo/HPNamePlateDemo.html` (geometry pixel-measured from the mockup; live tuning + spec export). Doc updates already landed: `docs/UIUX_Guidelines.md` §3.7 + v0.16, `docs/PhaseTransition.md` pending note.
- **Reads**: `GameScene.unity` (HUD wiring), `HPNumericDisplayHorizontal.cs`, `CombatIconPresenter.cs`, `ShopHudPage.prefab` + `Tpl_Avatar.prefab` / `Tpl_HpPill.prefab`, `ShopChrome.cs`, `ShopTopBarLayout.cs`, `ShopMirrorScale.cs`, `ShopHudBinder.cs`, `HudCountBinding.cs`, `HudTextBinding.cs`, `PhaseTransitionDriver.cs`, `PhaseHudFlight.cs`, `PhysButton.cs`, `ShopWorldWidgets.cs`, `PaletteTint.cs`, `GameColorPalette.cs` + `GameColorPalette.asset`, `PlayerIdentity.cs`, `OpponentDeckCache.cs`, `docs/PhaseTransition.md`, `docs/UIUX_Guidelines.md`.

## 1. Goal / non-goals

**Goal**: per side, replace [icon block + name label] (`CombatIconPresenter`) and [horizontal HP pill] (`HPNumericDisplayHorizontal`) with ONE merged **HP name plate**: a face carrying the `HP` + current/slash/max digit row, over the standard hard shadow extended down-right into a username band. Covers Combat, the settled-shop chrome mirror, and the Result phase, keeping all existing motion, phase-visibility, and transition world-flight behavior.

**Non-goals**: HP compare bar (`CombatHPBarPresenter`), damage floaters, shop chips, `ResultStatsPanel`, motion constant retuning, server/identity logic, the retired vertical `HPNumericDisplay` objects (follow-up), any `AGENTS.md` edits (ship-time checklist item).

## 2. Current state (verified facts, 2026-10-07)

### 2.1 Canvas combat HUD (`Assets/Scenes/GameScene.unity`, Screen Space-Camera "Combat Canvas", scaler 1080×1922)

| Object | Anchor | anchoredPosition | scale | Children |
|---|---|---|---|---|
| `PlayerIcon` (GO `1026264816`) | (0,0) | (70, 90) | 0.4 | `big shadow`, `small shadow`, `frame` (#D9D5CA), `image` (Navy), `PhysicalCardShadow` (inactive), `PlayerNameLabel` (TMP 30 bold, RobotoCondensed-Regular SDF, Navy, pos (0,-95)) |
| `EnemyIcon` (GO `309960881`) | (1,1) | (-70, -90) | 0.4 | `shadow`, `frame` (dark red), `image` (beige), `EnemyNameLabel` (same text setup) |
| `PlayerHPDisplayH` (GO `1001007102`) | (0,0) | (290, 89) | 0.8 | `HPDisplayRootH` = `shadow` + `custom button` (pill face #D1CDC8) + `CurrentRoot`/`CurrentPlain`+`CurrentStrips` + `Slash` + `MaxRoot`/`MaxPlain`+`MaxStrips` + `HP` label |
| `EnemyHPDisplayH` (GO `1707367311`) | (1,1) | (-290, -89) | 0.8 | same shape |

- `HPNumericDisplayHorizontal` serialized: `maxFontScale` 0.5, `groupGapEm` −0.2, `maxOffsetYEm` −0.21 (player) / −0.17 (enemy); digits font **RobotoCondensed-Bold SDF**, `currentPlain` 128 / `maxPlain` 64; `slashText.fontSize` is forced to full `_em` at Awake (code, `HPNumericDisplayHorizontal.cs:196`).
- `CombatIconPresenter` lives on its own GO under Combat Canvas, wires the two icons + the two name labels; name poll = `PlayerIdentity.Username` / `OpponentDeckCache.Current.username`, `???` fallback, diff-guarded (`CombatIconPresenter.cs:342-376`).
- Icon references: only the canvas children list + the presenter — nothing else in scenes/prefabs/scripts (no tests).
- Dead objects: inactive vertical `PlayerHPDisplay` / `EnemyHPDisplay` with `HPNumericDisplay` still in scene.

### 2.2 Shop chrome mirror (`Assets/Prefabs/ShopHud/ShopHudPage.prefab`)

- `Avatar` (Tpl_Avatar instance, pos (−4.45, −1.8), scale 0.4): `HudTextBinding` key `Username`; `ShopMirrorScale.canvasShopScale` 0.41; world name label `PaletteTint` slot 8 (IconNameLabel).
- `HpPill` (Tpl_HpPill instance, pos (−2.24, −1.8)): two `HudCountBinding`s — `'{0}/'` → `HpValue`, `'{1}'` → `HpMax`; `ShopMirrorScale.canvasShopScale` 0.74; HP texts `PaletteTint` slot 9 (HpNormalPlayer); shadow/face SpriteRenderers sliced `RoundedCorner.png` (PPU 256, border 64), shadow tint slot 5 (cardShadow).
- Runtime fill: `ShopHudBinder.Refresh()` pushes `PlayerStatusSO.hp/hpMax` into every `HudCountBinding` under the page (0.3 s count-up, no odometer) and `PlayerIdentity.Username ?? "???"` into `HudTextBinding.Username`.
- `ShopChrome`: `SetMirrorsActive(bool)` finds children literally named `"Avatar"` / `"HpPill"`; `TryGetAvatarWorldCenter` / `TryGetHpPillWorldCenter`; `AvatarShopScale` / `HpDisplayShopScale` read `ShopMirrorScale`.

### 2.3 Phase transition HUD flights

- `CombatIconPresenter` flies the avatar (`ShopChrome.TryGetAvatarWorldCenter` ↔ combat anchor); `HPNumericDisplayHorizontal` flies the pill (`TryGetHpPillWorldCenter` ↔ combat anchor). Both: `HudWorldFlight` world-lock, no scale tween on path, `canvasShopScale` ease over `HandoffScaleWindow` 0.35 at both ends, mirror swap in flight `OnComplete` (`_handedOffToMirror`), mirrors hidden during travel (`SetMirrorsActive(false)`).
- Visibility: player icon+pill = Combat ∪ Result ∪ (Shop while transitioning, pre-handoff); enemy icon+pill = Combat(¬suppressed) ∪ Result ∪ traveling. Enemy entrance/exit = full-`transDur` world slide from `enemySlide` above.

### 2.4 Palette / assets

- `GreyWhite.asset` = **#D1CDC8 — identical to the mockup face** and to the current pill's literal face color.
- `cardShadow` → "Navy Alpha" #1A3037 @ 0.698 — what every world button/prefab shadow uses (PaletteTint slot 5). **`UIShadow.asset` (#0A0E19 @ 0.6) is unreferenced — do not use.**
- `hpNormalPlayer` = Navy #1A3037; `hpNormalEnemy` = GreyWhite 2 #BFBDB5; `hpBarEnemy` = Red 2 #801424; `iconNameLabel` = Navy.
- Fonts: digits RobotoCondensed-Bold SDF; current name labels RobotoCondensed-Regular SDF (fallback chain carries `♥`/`❚`/`🜲`; CJK via chain).
- Palette discipline (`docs/UIUX_Guidelines.md` §4): no hardcoded hex in components — wire through `GameColorPalette`.

### 2.5 Name data

`PlayerIdentity.Username` (cached `player_identity.json`, set by `/api/players/register`); `OpponentDeckCache.Current.username` (set when a ghost deck is taken, may arrive shortly after the phase switch — poll, don't one-shot).

## 3. Design

### 3.1 The plate

```
 face (rounded rect)                 shadow slab (rounded rect, cardShadow)
 ┌─────────────────────────┐        ┌─ inset 0.11em from face left
 │ HP 20/20                │        │  top-left hidden behind face
 └─────────────────────────┘        │  right strip = ext-x 0.16em
                          └─────────┴─ bottom band = ext-y 0.41em
                                 NAME ─ right edge == face right edge
```

Geometry (em; 1em = current-digit font size = 128 canvas px at combat scale 0.8) — confirmed demo spec:

| Parameter | Value | Meaning |
|---|---|---|
| pad-x / pad-y | 0.12 / 0.01 | face padding (the 1em line box provides vertical air) |
| corner radius | 0.21 | face + shadow |
| extend-x | 0.16 | shadow right overhang |
| extend-y | 0.41 | shadow bottom extension = name band height |
| shade inset | 0.11 | band inset from the face's left edge |
| max scale | 0.47 | max digits only; the slash stays at full em (user ruling 2026-10-07, no code change) |
| max / slash raise | 0.05 | off the baseline (`maxOffsetYEm` ≈ −0.05; today −0.21/−0.17) |
| HP label gap | 0.11 | label at full digit size |
| name scale / tracking | 0.32 / 0.05 | shrink floor 55%, then ellipsis |
| name side pad | 0.16 | == extend-x → name right edge == face right edge |

Colors (all existing ColorSOs, wired through new `GameColorPalette` "HP Plate" fields — §4):

| Slot | Player | Enemy |
|---|---|---|
| face | GreyWhite #D1CDC8 | Red 2 #801424 |
| digit ink | Navy (hpNormalPlayer) | GreyWhite 2 (hpNormalEnemy) |
| band | cardShadow "Navy Alpha" (both sides) | same |
| name | GreyWhite #D1CDC8 | GreyWhite 2 #BFBDB5 |

Layout rule: identical on both sides — shadow always down-right (R0), name right-aligned; enemy differs only by face/ink colors. Read-only with a shadow is a user-approved R2 exception; `raycastTarget = false` everywhere.

### 3.2 Combat anchors

With the icon gone, the plate takes the icon's corner spot (the demo's stage mock): player plate root anchors (0,0) at **(70, 90)**, enemy (1,1) at **(−70, −90)**, scale 0.8 unchanged. The band hangs ~0.41em × 128 × 0.8 ≈ 42 px below the face — clear of the bottom edge; the enemy band hangs into the field (darkens over the red topo region, correct for a shadow).

### 3.3 Component strategy (canvas)

Extend `HPNumericDisplayHorizontal` in place — it already owns counting, odometer strips, shake/pop, digit-growth glide, phase visibility, shop parking, and the world flight. Additions:

1. **Layout**: `LayoutRoots()` grows to (a) include the `HP` label width + `labelGapEm` in the row, (b) size/anchor the face Image (row width + pads, height ≈ 1.02em, centered on `displayRoot`), (c) size/anchor the shadow Image (face rect + inset-left/extend-right/extend-down), (d) place the name label in the band. Digit-growth glide also tweens face/shadow widths in the same `dividerGlideDuration`.
2. **Slash at full em**: `slashText.fontSize = _em` — unchanged from the pre-plate component (user ruling 2026-10-07; measure `_slashWidth` AFTER assigning). Only the max group renders at `_maxEm`.
3. **Name**: new serialized `TMP_Text nameLabel` (child of `displayRoot`, band region); poll per frame with the presenter's diff-guard (`PlayerIdentity.Username` / `OpponentDeckCache.Current.username`, `???` fallback); auto-shrink to a 55% floor then TMP ellipsis; color = plate-name palette field. Enemy poll gated like today (Combat branch).
4. **Edit Mode preview**: extend `ApplyEditModePreview` to lay out face/shadow/name (`previewHp`/`previewHpMax` + a `previewName`) so the plate is scene-visible without Play.
5. Retire `CombatIconPresenter` (component + GO) and delete the `PlayerIcon`/`EnemyIcon` subtrees; move its name-poll responsibility into (3). The retired vertical `PlayerHPDisplay`/`EnemyHPDisplay` stay untouched (follow-up).

Class/file keep the `HPNumericDisplayHorizontal` name (scene refs bind by GUID; the header comment gains the plate description + demo/plan links).

### 3.4 Shop chrome mirror

- New `Assets/Prefabs/ShopHud/Tpl_HpNamePlate.prefab` (world): sliced-sprite `Shadow` (PaletteTint slot 5) extended into the band + `Face` (new PaletteTint slot `HpPlateFace`) + `HP`/`HpValue`/`HpMax` world TMPs (slot 9) + `NameLabel` world TMP (`HudTextBinding` key `Username`, new slot `HpPlateName`) + `ShopMirrorScale` (retune from 0.74; plate is taller).
- `ShopHudPage.prefab`: delete the `Avatar` instance, replace the `HpPill` instance with the plate instance, re-anchored to the HUD column's left (the avatar's old x ≈ −4.45); rebalance the row-0 gap (band contract 2.6/0.5/2 unchanged).
- `ShopChrome`: `SetMirrorsActive` looks up `"NamePlate"` instead of `"Avatar"`/`"HpPill"`; `TryGetHpPillWorldCenter` → `TryGetNamePlateWorldCenter`; `TryGetAvatarWorldCenter` + `AvatarShopScale` deleted. Callers updated: `HPNumericDisplayHorizontal` (plate flight home), `CombatIconPresenter` deletions die with the class; `PhaseTransitionDriver` SetMirrorsActive calls unchanged.
- `ShopTopBarLayout`: `FallbackAvatar*` constants deleted; `FallbackHpDisplay*` retuned to the plate's mirror spot; `ShopAnchorHpDisplay` / `ShopScaleHpDisplay` keep their names (callers unchanged).
- `ShopHudBinder.Refresh()` needs no change (bindings discovered under the page); the mirror HP keeps the plain 0.3 s count-up (no odometer in the shop).

### 3.5 Phase transitions / visibility

Visibility matrix and flight contract unchanged, with ONE element per side instead of two: player plate = Combat ∪ Result ∪ (Shop while transitioning, pre-handoff); enemy plate = Combat(¬suppressed) ∪ Result ∪ traveling; enemy slide-in/out full `transDur` from `enemySlide` above. The flight's shop home = the plate mirror's world center; combat home = the (70,90) anchor via `PhaseFlightPlanner.HudHomeAtPage` (canvas-plane-Y basis). Handoff window 0.35 with the plate's `canvasShopScale`.

### 3.6 Edit Mode preview

`editModePreview` gains face/shadow/name rendering (plain text, same layout math) so GameScene shows the plate without Play; palette-change subscriptions already re-apply it.

## 4. Palette & binding tables

New `GameColorPalette` "HP Plate" group (fields → existing ColorSOs; **no new ColorSO asset**):

| Field | ColorSO |
|---|---|
| `hpPlateFacePlayer` | GreyWhite |
| `hpPlateFaceEnemy` | Red 2 (hpBarEnemy) |
| `hpPlateInkPlayer` | Navy (hpNormalPlayer) |
| `hpPlateInkEnemy` | GreyWhite 2 (hpNormalEnemy) |
| `hpPlateNamePlayer` | GreyWhite |
| `hpPlateNameEnemy` | GreyWhite 2 |
| `hpPlateShadow` | Navy Alpha (cardShadow) |

`GameColorPaletteWiringTests`: assert the new fields are wired (and facePlayer ≠ faceEnemy), mirroring the existing HP assertions. New `PaletteTint.Slot` entries `HpPlateFace` / `HpPlateName` for the world mirror.

Mirror prefab bindings: `HudCountBinding` `'{0}/'` → HpValue, `'{1}'` → HpMax (same as Tpl_HpPill); `HudTextBinding` key `Username` → NameLabel.

## 5. Implementation checklist

1. Palette: add the 7 fields on `GameColorPalette.asset` (reuse existing ColorSOs) + the 2 `PaletteTint.Slot` entries → EditMode `GameColorPaletteWiringTests` green (SaveScene + `refresh_unity` compile + assembly-mtime check before `run_tests`; full-suite `init_timeout` ≥ 180000).
2. `HPNumericDisplayHorizontal.cs`: layout additions (label in row, face/shadow sizing, band/name), slash at `_maxEm`, name poll + auto-shrink, preview extension, palette statics. Field retune: `maxFontScale` 0.47, `groupGapEm` 0, `maxOffsetYEm` −0.05 (both sides).
3. GameScene: rebuild `HPDisplayRootH` children per §3.3 (face/shadow sliced Images wired to palette colors, `HP` label kept, `NameLabel` TMP added, RobotoCondensed-Bold SDF); move `PlayerHPDisplayH` → (70,90), `EnemyHPDisplayH` → (−70,−90); delete `PlayerIcon`/`EnemyIcon` subtrees + the `CombatIconPresenter` GO; delete `CombatIconPresenter.cs`. SaveScene.
4. New `Tpl_HpNamePlate.prefab`; `ShopHudPage.prefab` swap (delete `Avatar`, plate in the HUD column, retune `ShopMirrorScale`); `ShopChrome`/`ShopTopBarLayout` API changes + caller updates; `HudWorldFlight` homes verified.
5. Full EditMode suite green; then the §6 Play matrix with the user.
6. Ship-time docs: `AGENTS.md` Key Files + shop-top-bar bullet (`CombatIconPresenter` removal, Tpl_Avatar/Tpl_HpPill → Tpl_HpNamePlate), `docs/PhaseTransition.md` pending note → shipped behavior, `docs/UIUX_Guidelines.md` §3.6 forward note cleanup, `docs/RegressionChecklist.md` new row.

## 6. Verification

- **EditMode**: existing suite (no HUD-component tests exist; `HPNumericCounterTests` math untouched) + the new palette-wiring assertions. Scene smoke: GameScene opens, no missing references after the icon deletion (grep the scene for the deleted GO fileIDs before saving).
- **Play matrix (user)**: combat hits (shake amplitude, odometer roll both groups, landing pop on the whole plate), max-HP growth glide incl. face/shadow width, zero HP (no zero-out — parity), low HP (no pulse — parity), long/CJK/empty usernames (shrink/ellipsis/`???`), shop mirror values + name (buy/sell HP utility count-up), Shop→Combat and Result→Shop flights (single plate, mirror handoff, no blank frame, no double copy), Result visibility both sides, headless bypass (`-odseed` / NullVisuals → legacy hard cut).
- **Regression focus**: enemy pill root reactivation + canvas-lag flight-home fixes (2026-10-02/03) must still hold with one element per side; compare bar pinning untouched.

## 7. Risks / notes

1. **Scene surgery blast radius** — `GameScene.unity` is ~12k lines and the icon subtrees are large; delete in-editor (or via one-off `execute_code`) and immediately SaveScene; never hand-edit the YAML.
2. **Digit overflow vs face** — strip slots already RectMask2D-mask each digit; the face must fully contain the 1em row windows (height ≈ 1.02em) so rolls never visually leak outside the plate.
3. **Name vs face z-order** — name sits between shadow and face; the band top == face bottom (no overlap by construction; keep it that way in `LayoutRoots`).
4. **CJK glyphs in the name** — use RobotoCondensed-Bold SDF (digits' font) for the name; verify its fallback chain covers CJK in Play (the demo can't prove it); fall back to the old label's RobotoCondensed-Regular SDF if not.
5. **Band legibility over dark world backgrounds** — intended (it reads as a real shadow); if combat readability suffers, the lever is the shared `cardShadow` alpha, not a new color.
6. **Shop band layout** — the plate mirror is wider than the old pill; confirm the row-0 column fit against `ShopChrome.CheckShelfClearance` and the 2.6/0.5/2 band contract.
7. **Two copies during travel** — the canvas plate and the world plate mirror must stay visually identical at the handoff scale (the 2026-10-03 seam); the plate's extra band makes a scale mismatch more visible than the old pill's — verify at the 0.35 window in Play.

## 8. Follow-ups (not in this plan)

- Delete the inactive vertical `PlayerHPDisplay`/`EnemyHPDisplay` scene objects + `HPNumericDisplay.cs` + `HPNumericDisplayDemo.html` reference sweep.
- `UIShadow.asset` is unreferenced — either wire it where it was meant to serve or delete it (separate cleanup).
- The mockup's all-caps username styling: if registration should store/display case-preserving names, that's a server/`PlayerIdentity` decision, not a HUD one.

## 9. Implementation notes (2026-10-07 session)

1. **Palette**: 7 "HP Name Plate" fields added to `GameColorPalette.cs`/`.asset` + statics; `PaletteTint.Slot` gained `HpPlateFace` (14) / `HpPlateName` (15); `GameColorPaletteWiringTests` gained `HpPlateFields_PlayerEnemyPairsDiffer` + 7 static assertions. Tests 7/7 green.
2. **maxOffsetYEm correction**: §3.1's "(maxOffsetYEm ≈ −0.05)" was a unit slip — the field offsets the max group's TOP-pivot position (`y = maxEm·0.5 + off`), so the demo's "bottoms aligned + raise 0.05em" resolves to **off = −0.215** (= −0.5 + 0.05 + 0.47·0.5). Both sides set to −0.215, and `slashOffsetYEm` −0.215 (the slash rides the same raise; its center-pivot convention makes +0.05 an error). Code defaults updated to match.
3. **Scene surgery** (execute_code + SaveScene, zero YAML hand-editing): both `HPDisplayRootH`s rebuilt — `shadow`/`custom button`→`face` Images (sliced, palette colors), new `NameLabel` TMP (Bold SDF, Right+Middle, pivot (1,0.5)), enemy's HP label renamed `HP (1)`→`HP`, sibling order shadow<NameLabel<face<row; displayRoot recentred to (0,0) (the old scene offset +122.5 was pre-plate hand-placement); roots moved to (±70, ±90) scale 0.8; serialized retune maxFontScale 0.47 / groupGapEm 0 / slashOffsetXEm 0; `PlayerIcon`/`EnemyIcon`/`CombatIconPresenter` GOs deleted — scene greps clean of their fileIDs.
4. **Measured plate geometry** (em=128 canvas px): row 375.6px = 2.934em ("HP 12/20" preview), face 406.3×130.6 = 3.174×1.020em, shadow 412.7×183.0, name band right edge == face right edge. The world mirror authors the same numbers at em = 1.0306 wu (the pill's digit font size — this is why `ShopMirrorScale.canvasShopScale` stays 0.74, digits unchanged).
5. **Mirror**: `Tpl_HpNamePlate.prefab` authored in-editor (z order Shadow 0.06 < NameLabel 0.055 < Face 0.05 < texts 0.01; slash is its own 4.844-size TMP so it matches the canvas max-scale slash — the pill's baked-in `"{0}/"` slash would have rendered full-em). `ShopHudPage.prefab`: `Avatar`/`HpPill` instances deleted, `NamePlate` instantiated at the avatar spot (−4.45, −1.8, 0). Row-0 gap left as-is for Play tuning.
6. **Code waves**: `HPNumericDisplayHorizontal` rework + `ShopChrome`/`ShopTopBarLayout` API rename + `CombatIconPresenter.cs` deletion compiled clean (0 errors; assembly mtime verified > source after two real compile errors were fixed — one missed caller rename, one `Sequence.Join` receiver). Unfocused-editor compile needed `refresh_unity force` + `RequestScriptCompilation()`; verify assembly mtime, not `isCompiling`.
7. **EditMode suite**: 743/742/0/1-skip (the pre-existing ignored `RecorderAnimationPlayer` nested-coroutine test). Zero drift vs the 09-30 baseline family.
8. **Follow-ups still open**: §8 items; plus the row-0 column gap retune and the band-hangs-below-band-contract look (`NamePlate` band bottom ≈ 2.81 wu vs contract line 3.46 — cosmetic, chrome-only, shelf contract unchanged).
