# Shop Top Bar: Combat HUD Reuse + Glyph Completion (HANDOFF — mid-task)

2026-09-19 · status: **COMPLETE (verification + docs closed out the same day; one item deliberately left open — see "Left open")** · supersedes the top-bar half of `plans/plan-shop-panels-port-2026-09-18.md` (its open glyph TODO is now resolved; the follow-up record there is updated)

## Task definition (user, 2026-09-19)

1. Adjust the shop page top bar to the annotated layout (the 2026-09-18 reference screenshot = UIKitDemo §08 wireframe): left block = avatar+username → HP → money; right block = options + rarity odds row + wins/hearts/income row.
2. **Reuse the two combat-phase components in the shop** (user requirement): the player avatar+username (`CombatIconPresenter` player side) and the horizontal HP numeric display (`HPNumericDisplayHorizontal` player side, pill incl. "HP" prefix).
3. Supplement the missing glyphs from the existing plan's open TODO.
4. **Copy is Chinese** (user decision, overriding the earlier English-copy convention): 离开商店 / 商店 / 卡组 / 商店升级 / 重掷 $N / 空卡位 / 从商店购买卡牌; income label is `+$N/ROUND` per the annotation. Wins/hearts are glyph-only labels (`🜲 0/6`, `♥ 3/3`).
5. Play Mode testing permission granted.

## What was done

### Code (all compile clean: `dotnet build Assembly-CSharp.csproj` + `Assembly-CSharp-Editor.csproj`, 0 errors)

- **`Assets/Scripts/UXPrototype/ShopTopBarLayout.cs` (NEW, static)**: single source of viewport anchors shared by the world chrome and the reused canvas components:
  - **Final values (tuned in the second session's play pass)** — row 0: `PlayerIconViewport (0.292, 0.956)`, `HpDisplayViewport (0.437, 0.959)`, `MoneyChipViewport (0.654, 0.959)`; rows 1-2: `OddsRowViewportY 0.883`, `StatsRowViewportY 0.810`, `HudColumnViewportX 0.252`; `PlayerIconShopScale 0.28` (was 0.35 — clipped the icon's 222×306 `small shadow`), `HpDisplayShopScale 0.5`. New helper `ViewportToChromeLocalY` for the chip rows. (Superseded first-tune guesses were (0.045, 0.904) / (0.170, 0.902) / (0.330, 0.902), scale 0.35.)
  - `ViewportToCanvasAnchored(viewport, canvas)` — for canvas pieces with bottom-left (0,0) anchors: `viewport * Screen.px / canvas.scaleFactor`.
  - `ViewportToChromeLocal(viewport, cam)` — into ShopChrome-root local space (root pinned at cam XY + orthoSize − `BandInsetFromTop`).
- **`CombatIconPresenter.cs` (rewritten)**: player icon visible in Combat AND Shop, enemy icon Combat-only; per-phase anchor/scale (combat values captured at Awake, restored on non-shop phases); name label (`PlayerIdentity.Username`, `???` fallback) polls in both phases. Phase detection: `_lastPhase` field + `ApplyPhase(phase)` on change.
- **`HPNumericDisplayHorizontal.cs`**: player side visible in Combat+Shop (`visible = inCombat || (side == Player && inShop)`); **off-combat HP reads live `PlayerStatusSO.hp`** (the CombatInfoDisplayer queue is frozen between combats — without this, shop shows stale/0); placement moves the component's own RectTransform; exit restores combat placement. Enemy side unchanged (combat-only).
- **`ShopChrome.cs` (layout rework, reworked again to §08 in the second session)**: REMOVED the world `_hpChip` + `_avatarChip` (replaced by the reused combat components). Exit button 离开商店 (enlarged: `ExitButtonWidth 2.6`, font 2.8), options `❚❚` (`OptionsButtonWidth 0.72`); money chip (`MoneyChipWidth 2.4`, height 0.62, font 2.8) placed via `MoneyChipViewport`. **§08 order**: exit/options/money on row 0 with the avatar+HP; rarity odds left-aligned on row 1 and wins/hearts/income left-aligned on row 2, both anchored at `HudColumnViewportX` (`OptionsToChipsGap` and the old right-aligned placement deleted). Labels: `🜲 {wins}/{winCon}`, `♥ {hearts}/{heartMax}`, `+$N/ROUND`. `CreateButton`/`CreateChip` take width/height/fontSize params. `SmallChipWidth` 1.9→1.7.
- **`ShopSectionPanels.cs`**: headers 商店/卡组/商店升级 (deck counter untouched); reroll label `重掷 $N`.
- **`Assets/Prefabs/UXPrototype/EmptyCardSpace.prefab`**: `空卡位` / `从商店购买卡牌`, written as Unity-native `\uXXXX` YAML escapes.

### Fonts (all verified on disk)

- **`Assets/Fonts/NotoSansSymbols2 SDF.asset`**: now 5 glyphs — ✦ 10022, ✧ 10023, … 8230, **♥ 9829**, **❚ 10074**. Method (via MCP `execute_code`): set `m_SourceFontFile` via SerializedObject if null → `atlasPopulationMode = Dynamic` → `TryAddCharacters` → back to `Static` → SetDirty on asset+textures+material → `AssetDatabase.SaveAssets()`.
- **`Assets/Fonts/NotoSansSymbols-Regular.ttf`** (NEW, OFL — Noto Sans Symbols v2.003, the only Noto family member covering U+1F732; downloaded from notofonts.github.io through the user's Clash proxy `http://127.0.0.1:7897`) + `Assets/Fonts/OFL-NotoSansSymbols.txt` (copied from the Symbols2 OFL file).
- **`Assets/Fonts/NotoSansSymbolsAlchemical SDF.asset`** (NEW): 1 glyph, 🜲 unicode 128818, SDFAA, 512×512, padding 9, static. GUID `567027bf313f8e444a86e2353b878a25`.
- **`RobotoCondensed-Regular SDF.asset`** (the shop chrome font): `m_FallbackFontAssetTable` is now 4 entries — SourceHanSansCN-Regular, SourceHanSansCN-Bold, NotoSansSymbols2, **NotoSansSymbolsAlchemical** (appended last).

**Unity 6.3 TMP pitfalls hit (important for any future bake):**
- `TMP_FontAsset.TryAddCharacters(uint[])` **truncates codepoints to `char`** (`GetCodePoint(uint[])` casts to char) — supplementary-plane chars (U+1F732) must be passed as the UTF-16 **surrogate pair** `uint[]{0xD83D, 0xDF32}`; the string overload does NOT combine surrogates either (iterates raw chars).
- `CreateFontAsset(...)` leaves a **1×1 placeholder atlas**; TMP reinitializes it on first add (`m_AtlasTextures[i].Reinitialize(m_AtlasWidth, m_AtlasHeight)`) — not an error.
- `TMP_FontAsset.sourceFontFile`, `atlasPadding`, `atlasWidth` etc. are read-only properties — write via `SerializedObject` (`m_SourceFontFile`, `m_AtlasPadding`, …). `GlyphRenderMode` lives in `UnityEngine.TextCore.LowLevel`.

### MCP for Unity bridge (how the next session gets editor automation)

- The session's tool list has no Unity MCP tools; the bridge was started manually: editor window "MCP for Unity" → **Start Server** (Transport: HTTP Local, `http://127.0.0.1:8080`). **The server is still running at handoff** (stop it from the same window if unwanted).
- Client helpers (temp, not project files): `.utmp/mcp.py` — `python .utmp/mcp.py <tool> '<jsonArgs>'` (tools: `execute_code`, `manage_editor` ({"action":"play"|"stop"}), `read_console` ({"action":"get","count":N}), `run_tests`, …); `.utmp/mcp_exec.py <file.cs.txt>` — runs a C# file through `execute_code`.
- `execute_code` constraints: **method-body statements only — no `using` directives** (fully-qualified names); Roslyn; runs on Unity main thread; `return <expr>` yields the result JSON. If a type isn't resolved, `System.Type.GetType("TypeName, Assembly-CSharp")`.
- Screenshots: `ScreenCapture.CaptureScreenshot` did NOT produce a file (silently no-op'd) — use kimi-cu `get_app_state` (app "Unity", mode image) and crop the Game view region, e.g. via ReadMediaFile `region`.

## Play-mode verification — CLOSED OUT 2026-09-19 (second session)

**The "open anomaly" was not a code bug — it was the Editor's player loop freezing.** `Application.runInBackground` is `false` in this project, so while the Unity Editor window is unfocused (the normal state during MCP-driven testing) **no frames run at all**: `Time.frameCount` and `Time.time` freeze and `Update()` never executes. The phase SO is mutated synchronously inside the MCP call, so the polled-phase components read one phase behind — which looks exactly like "the asset never ran its placement". The first probe of this session reproduced the reported symptom (`_lastPhase=Shop` while the phase SO read `Combat`) *and* showed `frameCount=3 / time=0.04` — no frame had run since the phase flipped. Setting `Application.runInBackground = true` (runtime-only) took the session from frame 3 to frame 1868 and `CombatIconPresenter` caught up on its own (`_lastPhase=Combat`, icon back at (70,90)/0.4). The handoff's "manually invoking `Update()` fixes it" observation was the same artifact. **Method rule for the future: always assert `frameCount` is climbing in the same probe, and set `runInBackground = true` before trusting any polled state.** (Recorded in `AGENTS.md` post-mortem notes and a project memory.)

**A real bug was found and fixed in its place.** `HPNumericDisplayHorizontal` (player side) never re-placed on a **visible→visible** phase change: placement was applied only from `EnterVisiblePhase`, which fires on an invisible→visible edge, and the player side is visible in *both* Shop and Combat — so `_wasVisible` stayed `true` across a Shop→Combat switch and the pill kept the shop anchor *and* shop scale for the entire fight. Combat→Shop happened to work only because the Result phase sits in between and does cross an edge, so **the first transition of every run was the broken one**. Fixed with a `_lastPhase` field + phase-change guard calling `ApplyPhasePlacement(...)`, mirroring `CombatIconPresenter.ApplyPhase`; VISUAL-FIX(2026-09-19) block added. Verified: in combat the player pill is at (290, 89)/0.8 with `_wasVisible` still `true` across the switch (the exact previously-broken case).

**Full cycle verified** (play mode, `runInBackground = true`, driven through the real button action + PhaseManager methods):

| Phase | playerIcon | Player HP | Enemy HP / icon |
|---|---|---|---|
| Shop | (315.36, 1032.48) / 0.28 | (471.96, 1035.72) / 0.5 visible | hidden at (-290,−89)/0.8 ; enemy icon hidden |
| Shop → Combat | (70, 90) / 0.4 | **(290, 89) / 0.8** ← was broken | (-290,−89) / 0.8 visible ; enemy icon shown |
| Combat → Result | hidden | hidden, anchor restored | hidden, anchor restored |
| Result → Shop | back at the shop anchor | back at the shop anchor | hidden |

Two further defects found and fixed during the pass:

1. **Avatar clipped at the viewport top** once row 0 moved onto the 离开商店 line. The icon's `PlayerIcon` rect is 100×100 but its `small shadow` child is **222×306** (`frame` 192×276, `image` 192×192), so the visible block is ~3.06× the rect height. Fixed by `PlayerIconShopScale` 0.35 → **0.28** and `PlayerIconViewport.y` 0.959 → 0.956; the whole block now sits inside the viewport (verified per-child screen rects, top at y=8px).
2. **Row order** reworked to the §08 column (row 0 = 离开商店 | avatar | HP | $ | ❚❚ on one line; row 1 = rarity odds; row 2 = wins/hearts/income, rows 1-2 left-aligned on the HUD column), with `HudColumnViewportX` set so the odds row clears the 重掷 button in the Shop panel header.

**EditMode suite: 577 tests, 1 failure** — the only failure is the pre-existing, unrelated `OpponentDeckCacheTests.CacheFile_PersistsAcrossReload`. The `ShopSectionPanelsTests.ComputeContentBounds_SingleCenter_*` failure carried in the handoff as "possibly stale" is **pre-existing and was a wrong assertion, now fixed**: with `aboveHalfHeight=2.0` / `belowHalfHeight=2.5` the bounds center is necessarily `c.y + (2.0−2.5)/2 = 1.75`, not the input center — the assertion only holds for symmetric extents, and `FitPanel` consumes `min`/`max`, never `center`. `ComputeContentBounds` is byte-identical to HEAD, so this predates the task.

## Left open (deliberate, needs a separate decision)

**The Shop panel header row (商店 + 重掷 $2) still sits inside the chrome band** (world Y 4.33 vs band bottom 3.46), and the panel's translucent top edge (4.93) rises above the money/odds rows. This is now **cosmetic only** — the §08 reorder removed the collision that motivated it (the nearest chrome element is the HUD-column chips at X −3.01; the header sits at −4.82, a 1.1-unit gap), and `ShopChrome.CheckShelfClearance` guards shelf *cards*, not the panel header.

Moving the header below the band was the chosen remedy, but it is **not implementable at the current shop pitches**. The header is derived from the shelf (`content.max.y + TopPadding + HeaderHeight`), so clearing the band needs the shelf lowered ~1.4u; at `shopItemPos.y` 1.50 / `playerDeckPos.y` −3.40 / `yOffset` 4.5 that slides the shelf's price buttons into the deck row (0.40u overlap) and pushes the deck panel ~0.2u off-screen. Doing it properly is a **shop-pitch re-tune** (card scale / `yOffset` / `objPerRow`), not a header nudge — and note `shopItemPos`/`playerDeckPos` are **scene-serialized** in `GameScene.unity` (which was deliberately not edited by this task, since it holds unrelated uncommitted user changes). Recorded in `docs/UIUX_Guidelines.md` §3.6 as a known-open item.

## Docs updated (2026-09-19 second session)

- `docs/UIUX_Guidelines.md` — §3.6 chrome-v3 bullet + known-open note, version history v0.13.
- `docs/RegressionChecklist.md` — new row 107 (the three defects + the player-loop-freeze note).
- `AGENTS.md` — "Shop top bar v3" bullet; post-mortem note on the play-mode verification trap (24,676 bytes, under the 32 KB limit).
- `plans/plan-shop-panels-port-2026-09-18.md` — glyph TODO marked RESOLVED with the bake recipe and the Unity 6.3 TMP pitfalls.
- Memory: `unity-playmode-unfocused-freeze` (project).


## Do-not-touch / traps

- Uncommitted **user** changes in the working tree: `GameScene.unity` (CombatBudgetGuard add, TestManager toggles), `GameColorPalette.cs`, `docs/RegressionChecklist.md`, `docs/UIUX_Guidelines.md`, `plans/plan-infinity-detection-2026-09-17.md`, `Fatigue.prefab`, `ServerConfig.asset`, `PlayerDeckRef.asset`, plus untracked test decks. `ShopSectionPanels.cs` mixes user's and this task's edits. Commit only explicitly-listed paths, if ever asked.
- `GameScene.unity` was deliberately NOT edited by this task (placement is computed in code from `ShopTopBarLayout` constants — zero scene changes).
- The shop is world-only by design (2026-09-18 decision); the reused combat pieces are canvas (Combat Canvas). They are read-only (raycastTarget=false), so the single-input-pipeline rule is preserved — do not add uGUI interactive elements to the shop.
- Fullwidth punctuation (：，) has NO glyph in any atlas — use halfwidth in card/shop strings. `✦` only exists in NotoSansSymbols2 SDF; 🜲 only in the new Alchemical SDF.
- `.utmp/` is scratch: `mcp.py`, `mcp_exec.py`, `probe_*.cs.txt`, `bake_alchemical.cs.txt`, `fontcheck/` (downloaded fonts) — safe to keep or delete.
