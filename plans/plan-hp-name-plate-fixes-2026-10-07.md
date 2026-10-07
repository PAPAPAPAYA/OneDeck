# Plan: HP Name Plate — verification fixes — 2026-10-07

- **Date**: 2026-10-07
- **Status**: Implemented + verified 2026-10-07 (EditMode 743: 742/0/1-skip zero drift; Play probes: face corners on-canvas w/ 42.5 px inner margin, slab edges == demo spec on both sides incl. growth-glide resize, mirror "HP 27/27" at the Result→Shop swap, fallback Y 874.8 vs live 875.05; rows 153/154 ✅). Shipped in commit 2a89d7c9. Fix 5 (slash full-size ruling) implemented + verified the same day: EditMode 743 zero drift again; Play — slash fontSize == `_em` on both sides, centerline y 0, combat inner margin 33.5 px on-canvas, mirror big slash, swap face-width match 3.4529 vs 3.4524 wu; row 155 ✅. Shipped in commit aee90fdb. Fix 6 (row-group pivots, found in Fix 5 verification) implemented + verified: ink gaps 1.36/1.64 px, Play combat/Result screenshots clean; EditMode 743 run had ONE unrelated environment-state failure (`OpponentDeckCacheTests.CacheFile_PersistsAcrossReload` — the non-hermetic test reads the machine's real ghost-deck cache, polluted by today's play probes; not a code regression).
- **Request (user, 2026-10-07)**: 修法落成文档 — write the fixes found in the implementation verification into a plan document.
- **Parent plan**: `plans/plan-hp-name-plate-2026-10-07.md` (shipped, uncommitted working tree).
- **Verification basis**: EditMode suite 743 (742 green + 1 pre-existing skip); Play probes with `runInBackground=true`, frame-dense flight logging (`Logs/flight_probe.txt`, deleted after analysis); live rect reads; Game-view screenshots. All evidence values below were measured live in Play on 2026-10-07.

## 1. Context

The HP name plate implementation landed per the parent plan and verified mostly clean. Four deviations were found in the first pass, none blocking the EditMode suite; Fix 5 was added the same day after the user's slash-size ruling (the shipped code renders the slash at the max group's scale). Each fix below is specific to file/field/line and carries its own verification. One further anomaly (verification combat #2 plate misplacement) was traced to a test artifact — see §6, no code change.

## 2. Fix 1 — combat anchors clip the plates off-screen (visible, must fix)

**Symptom**: in combat both plates render partially off-canvas — the player plate's `HP` label reads as a clipped "A 5", the enemy plate's `/max` group leaves the right edge.

**Evidence (live, Play)**: face `sizeDelta = (406.32, 130.56)` at root scale 0.8 → face half-width ≈ 162.5 screen px. Root anchors: player (0,0)/(70, 90), enemy (1,1)/(−70, −90) → face inner edge lands at 70 − 162.5 = **−92.5 px** past the canvas edge (mirrored for the enemy).

**Root cause**: the anchors were carried over from the deleted 100×100 icons, but the plate face is centered on the root and ~3.3× wider. The confirmed demo (`docs/demo/HPNamePlateDemo.html` stage mock) places the face's inner edge ~42 px from the screen edge, not the face's center.

**Fix** (scene-only, `GameScene.unity`):
- `PlayerHPDisplayH` RectTransform `anchoredPosition` → **(205, 90)**.
- `EnemyHPDisplayH` RectTransform `anchoredPosition` → **(−205, −90)**.
- 205 = face half (162.5) + demo margin (42). Digit-count growth expands the row symmetrically: one extra digit adds ≈ 28 px per side (digit width ≈ 70 px at em 128 × 0.8 ÷ 2), so 3-digit HP keeps a ≈ 14 px inner margin — acceptable; 4+ digits would re-clip and is out of scope (HP is balanced well below 1000).

**Verification**: Edit Mode preview — both plates fully on-canvas at 16:9 and square aspects incl. the `HP` label and the max group; Play — combat corners fully readable; growth glide (max 99 → 100 forced via a status change) keeps the inner edge on-screen.

## 3. Fix 2 — `LayoutPlate` shadow slab shifted left by `inset` (one-liner)

**Symptom**: the name band is flush with the face's left edge instead of inset, and the shadow's right overhang is visibly smaller than the demo.

**Evidence (live rects, Play, displayRoot space)**: shadow slab spans [−203.16, +209.56]; the demo spec is [−189.08, +223.64] — the slab is offset left by exactly `inset` (0.11em = 14.08 px at em 128): left inset missing, right overhang 6.4 px (0.05em) instead of 20.48 px (0.16em).

**Root cause** (`HPNumericDisplayHorizontal.LayoutPlate`): `shadowRt.anchoredPosition = new Vector2((extX - inset) * 0.5f, -extY * 0.5f);` — the sign on `inset` is flipped. For the slab to span `[faceLeft + inset, faceRight + extX]` its center must be `(extX + inset) * 0.5f`.

**Fix**: `(extX - inset)` → `(extX + inset)` in that one expression, with a `VISUAL-FIX(2026-10-07):` block per `docs/VisualBugPrevention_Guide.md` and a `docs/RegressionChecklist.md` row (regress: live slab edges must equal [faceLeft + inset, faceRight + extX] on both sides; name band top == face bottom unchanged).

**Verification**: live rect read matches the spec edges; side-by-side with the demo's detail plate.

## 4. Fix 3 — mirror prefab `HP`/`Slash` static texts empty (prefab data)

**Symptom**: the shop mirror plate renders "27 27" — no `HP` prefix, no slash — while the canvas plate shows "HP 27/27" (its texts are written in `Awake`).

**Evidence (prefab read)**: `Tpl_HpNamePlate.prefab` children `HP` and `Slash` have `m_text = ''`. They carry no bindings (they are static labels), so nothing ever fills them. This also breaks the transition handoff seam: the canvas copy and the world mirror are supposed to be visually identical at the swap.

**Fix** (prefab-stage, no code): `Tpl_HpNamePlate.prefab` — `HP` child TMP text → `HP`; `Slash` child TMP text → `/`.

**Verification**: settled shop mirror reads "HP 27/27"; Result→Shop landing frame shows no text pop at the mirror swap.

## 5. Fix 4 — `ShopTopBarLayout` fallback viewport Y (headless corner only)

**Symptom**: none in the normal path (the live path reads the built page). The headless/no-chrome fallback parks the canvas plate at 35% viewport height (mid-screen) instead of the top bar.

**Root cause**: `FallbackHpDisplayViewportY = 0.351f` encodes the page-LOCAL Y (−1.8) of the NamePlate child: (−1.8 + 6.06)/12.12 = 0.3514. Per the comment's own derivation the mirror's WORLD Y is 3.76, so the fraction must be (3.76 + 6.06)/12.12 ≈ 0.81.

**Fix**: `FallbackHpDisplayViewportY = 0.81f`. (X = 6.42 verified correct against the same derivation at 16:9.)

**Verification**: headless bypass (`-odseed N` or NullVisuals) shop phase — the canvas plate parks at the top bar region, not mid-screen.

## 5.5 Fix 5 — slash back to the full digit size (user ruling 2026-10-07)

**Context**: the parent plan specced the slash at the max group's scale (measured from the small mockup) and the implementation shipped it that way (`slashText.fontSize = _maxEm`). User ruling 2026-10-07 (new mockup + explicit call): the separator between hp and hpmax renders at the FULL digit size — identical to the pre-plate component. The demo is already updated (`docs/demo/HPNamePlateDemo.html`).

**Fix** (`HPNumericDisplayHorizontal.cs`):
- Awake + `ApplyEditModePreview`: `slashText.fontSize = _maxEm;` → `slashText.fontSize = _em;` (`_slashWidth` is already measured after the assignment — order is correct).
- `maxFontScale` tooltip "Max digits AND the slash render at em * maxFontScale." → "Max digits render at em * maxFontScale (the slash always renders full-size)."
- `slashOffsetYEm`: the slash at full em shares the current digits' centerline → 0 (code default `slashOffsetYEm = -0.215f` → `0f`, and both scene instances re-serialized 0; its tooltip stops referencing the max-group raise. `maxOffsetYEm` keeps −0.215 for the max group).

**Mirror prefab** (`Tpl_HpNamePlate.prefab`, extends Fix 3's edit): the `Slash` child goes full size — fontSize ≈ the `HpValue` size (4.844 → 10.306) and y back to the row center (−0.22 → 0).

**Verification**: demo detail plate side-by-side (slash at the big digits' height, bottom-aligned); live read: slash fontSize == current digits' fontSize on both sides; the shop mirror shows the big slash.

## 5.6 Fix 6 — canvas row mis-tiling: `currentRoot`/`maxRoot` pivot (found in Fix 5 verification, user report 2026-10-07)

**Symptom**: in combat the digit row renders with a ~66 px hole between the current digits and the slash, and the slash ink overlaps the max group's first digit by ~29 px — the user's screenshot ("HP12 ⁄20" cluster) after Fix 5.

**Evidence (live TMP ink bounds, displayRoot space, "HP 12/20")**: before the fix — current ink [−90.35, 27.74], slash ink [93.80, 137.85] (66 px hole), max ink [109.09, 166.73] (28.8 px overlap with the slash). After — current [−25.65, 92.44], slash [93.80, 137.85], max [139.49, 197.14]: gaps 1.36 / 1.64 px, right margin to the face 1.93 px — matches the demo's tight rendering.

**Root cause**: `LayoutRoots` places `currentRoot`/`maxRoot` by their box LEFT edge (`anchoredPosition = rowX` / `slashX + slashW + gap`, no +half correction — unlike `hpLabel`/`slashText`, which get `+width*0.5` for their center pivots), but both group roots carry pivot (0.5, 1) in the scene, so each rendered half-its-width left of the intended tile. Invisible before the plate (no face wrapped the row; the old pill sprite was hand-sized around whatever the row did) and made glaring by Fix 5's full-size slash. The mirror prefab was hand-tuned to the intended math, which is why the prefab looked right while combat did not.

**Fix** (scene-only, `GameScene.unity`): `currentRoot.pivot` and `maxRoot.pivot` → **(0, 1)** on BOTH `PlayerHPDisplayH` and `EnemyHPDisplayH` (x 0.5 → 0; y stays 1 — the vertical math is top-pivot by design). No code change.

**Verification**: ink gaps above; Play combat/Result screenshots — both plates render "HP 19⁄25" tightly like the demo; EditMode suite re-run (see §7 note — one unrelated environment-state failure).

## 6. Test artifact — combat #2 misplacement (no code change)

In verification combat #2 both plates sat misplaced for the whole combat (player at the shop park, enemy off-screen above) and self-healed at the Result phase edge. Root cause: a `Thread.Sleep(3000)` inside the same editor-script call that triggered LeaveShop froze the main thread at travel start; the unscaled DOTween tweens elapsed during the freeze and the flights jumped. Combat #1/#3 (no freeze, frame-dense probe) landed exactly on the anchors. Production risk is a >maximumDeltaTime (0.33 s) hitch exactly at a travel-start frame — same jump, self-healing at the next phase edge. Optional hardening: none required for ship; note only.

## 7. Implementation checklist + ritual

1. Fix 2 + Fix 5 code-side (LayoutPlate one-liner, slash back to `_em` + tooltip/offset defaults) with VISUAL-FIX blocks + RegressionChecklist rows.
2. Fix 1 (scene anchors) + Fix 3/Fix 5 prefab-side (`Tpl_HpNamePlate` static texts + full-size slash) + Fix 4 (const) + Fix 5's `slashOffsetYEm` re-serialize on both scene instances.
3. Ritual: `refresh_unity` (compile: request) → assembly DLL mtime > source mtime → `SaveOpenScenes()` → full EditMode `run_tests` (`init_timeout` 180000) green.
4. Play spot-check: §2/§3/§4/§5 verification bullets; both transition directions; headless bypass once.
5. Close-out: RegressionChecklist row 153 → ✅ (after Play), commit per the straight-to-`main` workflow.
