# Shop Top Bar -> World Page Content (whole top bar scrolls with the wheel)

- **Date**: 2026-09-21
- **Status**: IMPLEMENTED 2026-09-21 (all code + docs landed; Play-mode checks below still need the user's run)
- **Execution record (2026-09-21)**: §5 steps 1-4 done as written, with these deltas from the PRD:
	- Step 0 probe found the mirror needs MORE than the PRD's scene dump: the player icon has a `big shadow` (192×276 @ (20,−20)) in addition to `small shadow`/`frame`/`image`; the pill root `PlayerHPDisplayH` is a 0×0 rect with `HPDisplayRootH` at anchored (122.5, 0) — so the world pill is offset by that amount inside its block; the pill also has an "HP" prefix word (mirrored as static text) and its `custom button` background is 0.820/0.804/0.784. All Image/TMP children are walked generically (no hardcoded list), as §7 required.
	- `ShopPageHud.ComputeCanvasScaleFactor` re-derives the ScaleWithScreenSize factor from the CanvasScaler (exponential lerp) instead of reading `canvas.scaleFactor` — the latter is only recomputed on the first willRenderCanvases pass, which runs AFTER `ShopUXManager.Start` where Bootstrap happens (timing hazard the PRD didn't account for).
	- The HP-pill parking problem was deeper than §3.3's one-liner: `ExitVisiblePhase` always restored the COMBAT anchor, which would have made the settled-shop→combat shared-element flight start already home (no glide). It now parks at the SHOP anchor when hiding in a settled shop. `CombatIconPresenter` additionally polls `IsTransitioning` like a phase (`_lastTransitioning`), because the landing flips it while the phase stays Shop and the visibility rule would never re-run (world+canvas double avatar).
	- Verification: EditMode full suite 667/668 green (1 pre-existing Ignore), `ShopPageHudTests` 4/4. Play checks (§6 items 1-8) pending the user's session — rows: RegressionChecklist row 110.
- **Request (user, 2026-09-21)**: the shop info chips must not stay pinned to the viewport top — they should scroll away with the cards on mouse wheel.
- **User rulings (2026-09-21)**:
	1. The avatar + HP display also become world objects (the "all world" option), so the info bar is coherent.
	2. The 离开商店 / ❚❚ buttons ALSO scroll with the page — **no pinned elements at all**; a scrolled-down shop shows no exit button (return by scrolling up, or press Space — the `PhaseManager` shortcut stays).
	3. The HP pill is NOT a frozen string: buying/selling HP utility cards moves hpMax mid-visit (hp refills to full), and the world pill must reflect that with visible count-up feedback.
- **Reads**: `plans/plan-shop-topbar-combat-hud-reuse-2026-09-19.md` (canvas-reuse baseline being replaced), `plans/plan-phase-transition-world-camera-2026-09-21.md` (shared-element flights that must survive), `docs/PhaseTransition.md`.

## 1. Goal / non-goals

Goal: in the settled Shop phase, EVERY element of the top bar (avatar+username, HP pill, money chip, rarity odds row, wins/hearts/income row, 离开商店, ❚❚) is world content fixed to the shop page; wheel scroll (camera rig Y) takes it away together with the cards. Combat and Result behavior stay pixel-identical to today, including the transition shared-element glide.

Non-goals: combat-side HUD rendering, the odometer digit machinery, `ShopSectionPanels` (already world/page content), any game logic, any scene edit.

## 2. Current state (verified facts)

- Shop wheel scroll moves the camera rig ("Camera Man"), not the content: `ShopUXManager.HandleCameraScroll` (`Assets/Scripts/UXPrototype/ShopUXManager.cs:772-797`); world objects are static.
- `ShopChrome` builds ONE scene-root object at runtime carrying `ShopChromeAnchor`, whose `LateUpdate` copies `Camera.main` position every frame (`ShopChromeAnchor.cs:18`) — this anchor is the ONLY reason chrome (chips AND buttons) looks pinned. Children today: 2 buttons + 6 chips (`ShopChrome.cs:155-210`).
- Avatar/username = `CombatIconPresenter` player side, a **canvas** piece (Combat Canvas, Screen Space - Camera, scaler 1080×1920 match-width, `GameScene.unity:114813448-452`). Structure (scene dump): `PlayerIcon` 100×100 root, children `small shadow` 222×306, `frame` 192×276 (Image, sprite guid `ee4c724560d27684d969ba363b913448`, player color cream 0.851/0.835/0.792), `image` 192×192 at y+40 scale 0.9 (same sprite, 0.820/0.804/0.784), `PlayerNameLabel` (TMP, fontSize 30, font guid `78f0e9d33a94e0a4f9407b45d9e9a88f`, dark 0.102/0.188/0.216). Shop placement: `ShopTopBarLayout.PlayerIconViewport` (0.292, 0.956) + scale 0.28 (`CombatIconPresenter.ApplyPhase`, `CombatIconPresenter.cs:103-142`).
- HP pill = `HPNumericDisplayHorizontal` player side (canvas odometer strips, `RectMask2D` masks, ~1000 lines). Shop placement: `HpDisplayViewport` (0.437, 0.959) + scale 0.5 (`ApplyPhasePlacement`, `HPNumericDisplayHorizontal.cs:423-447`). Off-combat HP reads live `PlayerStatusSO.hp` (`:516-538`). The shop HP invariant (`ShopManager.ApplyHpMaxFromDeck`, hp = hpMax on entry/buy/sell) means the shop readout never needs damage shake or heal counting — **but the value DOES change mid-visit** (buying/selling HP utility cards moves hpMax and refills hp to full; user ruling 3), and today's canvas pill visibly counts/rolls to the new value when that happens (`Update` polls the live hp → `SetCounterTarget` → digit strips roll, no shake for a rise). The world pill must reproduce that feedback (lightweight count-up), not a frozen string.
- Transition (`PhaseTransitionDriver`): the canvas avatar/HP glide between shop and combat homes as shared elements (flyShared) whenever `IsTransitioning`; player pieces stay visible through Result; enemy side is combat-only and slides in on landing. Chrome is hidden during travel and re-shown on landing (`PhaseTransitionDriver.cs:277`).
- Shop show/hide funnels: `ShopManager.EnterShop` → `ShopChrome.ShowIfActive` (`ShopManager.cs:413`), `ExitShop` → `HideIfActive` (`:443`). `RefreshIfActive` fires on buy/sell/reroll (`:352, :383, :532`).
- `HudChip` is render-agnostic (SpriteRenderer or Image + TMP_Text) — the world chip recipe already exists.
- Shop exit keyboard path exists independent of the button: Space in `PhaseManager.Update` (routed through the transition driver the same as the button).

## 3. Design

### 3.1 ShopChrome loses the anchor entirely

```
Shop Chrome (scene root; world position written ONCE at Build; never moves afterwards)
├── ExitButton (离开商店) / OptionsButton (❚❚)
├── ChipMoney / ChipRarityCommon / ChipRarityUncommon / ChipRarityRare
├── ChipWins / ChipHearts / ChipIncome
└── ShopPageHud (world avatar + world HP pill, §3.2)
```

- `ShopChromeAnchor` is DELETED (component file + meta; its only user was `ShopChrome.Bootstrap`). The root's position is written once in `Build()` to the same value the anchor would compute (`cam XY + orthoSize − BandInsetFromTop`, `z = cam.z + CameraForwardOffset`) — Bootstrap runs from `ShopUXManager.Start` with the rig at shop base Y, so every existing child local coordinate (buttons AND chips, all computed via `ShopTopBarLayout.ViewportToChromeLocal*`) lands pixel-identical to today at scroll 0, and the whole bar then scrolls with the page like any other world content.
- `BandBottomWorldY()` changes from live-camera to a build-time captured static (`_bandBottomWorldY`, computed once in `Build`); `CheckShelfClearance` semantics become "the shelf must clear the top bar's world zone" — same numbers at scroll 0, still fired after every shelf build.
- Z: unchanged (`cam.z + CameraForwardOffset` at build) — above cards; no card ever travels into the page-top zone, so a scrolled-away top bar can never overlap a card.

### 3.2 `ShopPageHud` — world avatar + HP pill (new runtime-built component)

Own file `Assets/Scripts/UXPrototype/ShopPageHud.cs`, Bootstrap called from `ShopChrome.Bootstrap` after Build, parenting into the chrome root. **Mirror, don't redesign**: read the live canvas pieces and copy their sprites/colors/fonts so the world versions are identical by construction:

- **Avatar**: walk `CombatIconPresenter.playerIcon`'s hierarchy; for every `Image` child (`small shadow`, `frame`, `image`) create a `SpriteRenderer` (sliced draw mode) with the same sprite and color; for `PlayerNameLabel` create a world `TextMeshPro` with the same font/fontStyle/color, polling `PlayerIdentity.Username` with the presenter's diff-guard convention.
- **HP pill**: probe first (step 0 below), then mirror any background/prefix graphics the same way; the digits become ONE world TMP `"hp/hpMax"` (font copied from `HPNumericDisplayHorizontal.currentPlain.font`, color from `GameColorPalette.HpNormalPlayerColor`). No odometer strips, no `RectMask2D` masks; value changes (HP utility buy/sell) instead play a plain integer count-up/down tween (~0.3 s, text rewritten per tick, DOTween) — the player-visible feedback is the number climbing, not the strip mechanics (§2). Refreshed from `ShopChrome.Refresh()` (hp/hpMax cached ints, same diff-guard pattern as the chips) so buy/sell/entry recompute flows (`ShopManager` RefreshIfActive calls) update it with zero new wiring.
- **Calibration (pixel-identical target)**: canvas px → world units formula, one static helper (pure, golden-testable):

```
screenPx = sizeDelta × localScaleChain(= shop scale: icon 0.28 / HP 0.5) × canvas.scaleFactor
world    = screenPx / Screen.height × (2 × Camera.main.orthographicSize)
```

  Applied to every mirrored child's sizeDelta and anchoredPosition offset. Positions of the two blocks come from the existing `ShopTopBarLayout` viewport constants (`ViewportToChromeLocal`), so row 0 keeps the §08 alignment with the chips by construction.
- Nothing interactive added: avatar/HP have no colliders — the world-only input rule (one physics pipeline, `ShopInputGate`) is untouched. The two buttons keep their existing `PhysButton` colliders and simply travel with the page now.

### 3.3 Canvas pieces: shop-visibility handoff rule

One rule change in each presenter (everything else — placement math, glide tweens, Result visibility, enemy entrance — untouched):

```
playerIcon / player HP pill active = inCombat || inResult || (inShop && PhaseTransitionDriver.IsTransitioning)
```

i.e. in a SETTLED shop the canvas versions hide (the world versions show); during a transition the canvas versions stay visible and glide exactly as today. Placement writes continue while hidden (ApplyPhase already retargets positions per phase), so a glide always starts from the correct shop anchor.

### 3.4 Swap timing per path

| Moment | World chrome (chips + buttons + avatar/HP) | Canvas avatar/HP |
|---|---|---|
| Settled shop | visible (scrolls with page) | hidden, parked at shop anchor |
| 离开商店 pressed (or Space) | hidden synchronously by `ExitShop` (`ShopManager.cs:443`) | re-activated next `Update` (phase now Combat), glides shop→combat as today |
| Result→Shop travel | hidden (chrome gated by `IsTransitioning`) | visible, gliding to shop anchor as today |
| Shop landing | `ShowIfActive` (driver, `PhaseTransitionDriver.cs:277`) | hidden on the next presenter poll — 1-frame overlap at the same screen position, invisible after calibration |
| Legacy hard cut (driver bypassed) | hides/shows with the same ShopManager calls | same frame-ish flip, no glide (today's behavior) |

Accepted deviations (recorded, not bugs):

1. **1-frame blink on shop→combat trigger** (world avatar hidden inside the button call, canvas re-enabled next Update). If it proves visible, presenters can subscribe to the phase event instead of polling — noted as the escape hatch.
2. **Scrolled shop has no exit button on screen** (user ruling 2): everything scrolled away together. Return paths: scroll up, or Space. The `phaseInfo` prompt ("Press [SPACE] to EXIT SHOP") already teaches the key.
3. Chips/buttons keep today's hide-during-travel behavior (v1). Now that the whole bar is page content, a v1.1 option is to leave it visible during travel so it scrolls away with the page like the demo — EXCEPT the avatar/HP world copies, which must still hide (the canvas shared elements fly; showing both would double them).

### 3.5 What is deliberately NOT touched

- `HPNumericDisplayHorizontal` internals (odometer/strips/shake/pop): combat behavior bit-identical.
- `CombatIconPresenter` placement math, enemy side, Result rule.
- `PhaseTransitionDriver` (no edits: `IsTransitioning` / landing `ShowIfActive` already provide every hook).
- `ShopUXManager` scroll, `ShopSectionPanels`, `ShopInputGate`, `GameScene.unity` (zero scene edits, same convention as the chrome/panels ports — the scene holds unrelated uncommitted user changes), all RNG/logic paths.

## 4. File inventory

Create:

- `Assets/Scripts/UXPrototype/ShopPageHud.cs` — §3.2 (Bootstrap/build/mirror/refresh; static px→world helper; count-up tween).
- `Assets/Scripts/Editor/Tests/ShopPageHudTests.cs` — EditMode goldens for the px→world helper (§6).
- `plans/plan-shop-topbar-world-scroll-2026-09-21.md` — this file.

Modify (all small):

- `Assets/Scripts/UXPrototype/ShopChrome.cs` — drop the `ShopChromeAnchor` add; write the root position once in `Build()`; `BandBottomWorldY()` becomes build-captured; `Refresh()` also delegates to `ShopPageHud`; header comment rewritten.
- `Assets/Scripts/UXPrototype/CombatIconPresenter.cs` — §3.3 visibility line + comment refresh (the 2026-09-19 header block must be updated to the new rule).
- `Assets/Scripts/UXPrototype/HPNumericDisplayHorizontal.cs` — §3.3 player-side visibility line + the VISUAL-FIX(2026-09-19) block's Regress text updated to the new rule (history kept).
- `docs/UIUX_Guidelines.md` — §3.6 chrome-v3 bullet rewritten (world top bar, scroll behavior, Space exit path) + version history.
- `docs/RegressionChecklist.md` — new row: scroll-away behavior (incl. buttons), landing swap, scrolled-shop exit paths, HP count-up, transition glide unchanged.
- `AGENTS.md` — "Shop top bar v3" bullet updated (whole top bar is page content and scrolls; avatar/HP world-mirrored in shop; canvas versions only fly during transitions) + `wc -c AGENTS.md` ≤ 32 KB.

Delete:

- `Assets/Scripts/UXPrototype/ShopChromeAnchor.cs` (+ `.meta`) — its only user was `ShopChrome.Bootstrap`.

VISUAL-FIX(2026-09-21) blocks where behavior visibly changes (the two presenters' visibility rules; ShopChrome header).

## 5. Implementation order

0. **Probe first** (execute_code, editor running): dump the exact `PlayerIcon` + player-side `HPDisplayRootH` hierarchies (children names, sprites, colors, rects, fonts) so the mirror code matches reality — the scene reading above is partial (pill background/prefix graphics unconfirmed).
1. ShopChrome anchor removal + one-time positioning; play check: whole bar scrolls away, clearance warning intact, Space still exits from a scrolled shop.
2. ShopPageHud world avatar + HP pill (incl. count-up); calibration constants verified by screenshot compare at scroll 0 (row 0 must pixel-match the pre-change layout).
3. Presenter visibility rules; transition both directions; landing swap; legacy hard-cut path (`PhaseTransitionConfigSO.enabled = false`).
4. EditMode goldens + full suite; docs (UIUX_Guidelines, RegressionChecklist, AGENTS.md); this plan's execution record.

## 6. Verification

- **EditMode**: `CanvasPxToWorld` goldens (e.g. 1080×1920 ref, scaleFactor 1, orthoSize 6.06: 100 px → 100/1080×12.12 ≈ 1.1222 u; icon block 222×306 px at 0.28 → 0.697×0.962 u); full suite green (save scenes before `run_tests`; `refresh_unity` after every .cs edit; full-suite `init_timeout` ≥ 180000).
- **Play Mode (needs explicit user grant per AGENTS.md)**:
	1. Settled shop at scroll 0: top bar pixel-matches the pre-change screenshot baseline.
	2. Wheel down: chips + avatar + HP + both buttons all leave with the page; `CheckShelfClearance` silent; wheel up brings them back; Space exits from a fully scrolled shop.
	3. Buy (then sell) an HP utility card: the world pill counts up (down) to the new full HP — matching today's canvas count/roll feedback, minus the strip mechanics.
	4. Shop→Combat: glide identical to baseline; landing swap invisible.
	5. Combat + Result: avatar/HP/odometer/shake/pop unchanged (canvas, untouched).
	6. Result→Shop: glide back; swap on landing invisible.
	7. Headless (`overrideCombatSeed`): world pieces build but never show outside shop; combat results byte-identical (presentation-only change).
	8. Unfocused-editor trap: `Application.runInBackground = true` before any polled assertion; assert `Time.frameCount` climbs in every probe (AGENTS.md post-mortem rules).

## 7. Risks

- **Style drift between world and canvas pill** (font rendering path differs): mitigated by copying the same font asset and palette color; verified in step 2's screenshot compare.
- **Scrolled-shop exit discoverability** (user ruling 2's cost): mitigated by the existing Space shortcut + `phaseInfo` prompt; if playtests show players getting lost, the fallback is a small pinned corner button — deliberately NOT built now.
- **Resolution/aspect change after Build**: chrome already builds once (pre-existing limitation); the mirror math recomputes only at Bootstrap. Same as today; recorded, not fixed here.
- **Mirror misses a decoration** (e.g. an unconfirmed pill background): step 0 probe exists precisely for this; the mirror walks ALL Image/TMP children rather than a hardcoded list.

---

## Fidelity fix addendum (2026-09-21, same day, follow-up session)

User report after playing the f4e28e26 build: the shop avatar looked wrong next to the combat one. Root-caused WITHOUT a live probe (scene-YAML structure dump + rendering-math), then fixed and play-verified:

- **Bug A — child `localScale` dropped by the mirror.** The avatar `image` child is 192×192 at scale 0.9 in the canvas (cream frame margin all around); `MirrorSprite` sized by `sizeDelta` only, so the world copy filled the frame edge-to-edge. Fixed by folding `source.rectTransform.localScale` into the size split (`CopyText` folds it too).
- **Bug B — 9-slice border proportion ~2x the canvas.** Canvas sliced Image border = border/spritePPU x canvas.referencePixelsPerUnit (64/256x100 = 25 local px = 13% of the 192 px frame); SpriteRenderer sliced border = border/spritePPU x transform.localScale, independent of `sr.size` (was 28%). Fix: `sr.size = sizeDelta x childScale / refPPU` with `localScale = refPPU x unit` — final size identical, border lands at exactly canvas-border-px x unit. Pure helpers `SlicedWorldSize`/`SlicedWorldScale` + `ShopPageHudTests` B1-B3 goldens.
- **Pill check (the §5 step-0 worry)**: the pill background IS inside `displayRoot` (`shadow` 400x138 a0.6 + `custom button` bg + `HP` prefix), so the original mirror already copied it; only the two generic bugs applied.
- **Verification**: EditMode 7/7 targeted, 670/671 full suite (1 pre-existing Ignore); play-mode RT captures (`.utmp/shot_shop.png` vs `shot_combat.png`) — margin/corner structure matches the canvas original; scrolled capture shows the whole bar leaving with the page; hpMax poke +2 -> pill reached 27/27 (count-up ran; bridge latency hid the mid-frame); shop->combat transition + landing swap intact; console clean. RegressionChecklist row 111.
