# Shop OptionsButton -> Viewport-Pinned Top-Right Corner (configurable insets)

- **Date**: 2026-09-28
- **Status**: IMPLEMENTED 2026-09-28 (code + prefab + docs + baseline probes landed; Play matrix §5 pending the user's run; see §7 execution record)
- **Request (user, 2026-09-28)**: pin the shop-page OptionsButton to the SCREEN top-right corner — option B, i.e. viewport-pinned so it survives wheel scroll — and expose the corner inset as configurable values.
- **Ruling amendment (user, 2026-09-28)**: this partially supersedes the 2026-09-21 world-scroll ruling #2 ("no pinned elements at all", plan-shop-topbar-world-scroll-2026-09-21). The amendment is scoped to the OptionsButton ONLY: chips, ExitButton, avatar and HP pill remain page content and still scroll away. A scrolled-down shop now shows exactly one chrome element — the options button.
- **Reads**: `plans/plan-shop-topbar-world-scroll-2026-09-21.md` (the ruling being amended + the VISUAL-FIX that deleted the old LateUpdate pinning), `plans/plan-shop-hud-prefab-widgets-2026-09-23.md` (v5 prefab architecture this plugs into).

## 1. Goal / non-goals

Goal: while the shop chrome exists, the OptionsButton always renders at the camera viewport's top-right corner — regardless of wheel scroll, window resize, or aspect change — with the inset from the corner authored as two prefab floats the user can tune (including live in Play mode).

Non-goals: any other widget's placement; ExitButton behavior; combat/result HUD; `ShopChrome`'s frozen loader API; `ShopHudPage.OpenPage` root placement; the band contract (`BandBottomWorldY`, shelf clearance).

## 2. Current state (verified facts)

- OptionsButton is a PrefabInstance child of `ShopHudPage.prefab` with authored local position (x=+5, y=0, z=0) and size override `m_Size.x=0.72` (y stays 0.64 from `Tpl_WorldButton`; pivot 0.5/0.5, centered anchors). No code computes its position today — it is dead-authored data.
- The page root is placed ONCE at build relative to the camera (`ShopHudPage.OpenPage`, `Assets/Scripts/UXPrototype/ShopHudPage.cs:29-33`): x = rigPos.x, y = rigPos.y + orthoSize − bandInsetFromTop, z = rigPos.z + cameraForwardOffset. Authored band values on the prefab root equal the defaults (bandHeight 2.6 / bandInsetFromTop 0.5 / cameraForwardOffset 2). The button center (local y=0) is therefore 0.5 below the viewport top at scroll 0 → current top-edge gap = 0.5 − 0.32 (half height) = **0.18**.
- Shop wheel scroll moves the "Camera Man" rig from `ShopUXManager.Update` (`HandleCameraScroll`, `Assets/Scripts/UXPrototype/ShopUXManager.cs:823`); a `LateUpdate` reader sees the post-scroll camera with zero frame lag.
- The chrome root is `SetActive(false)` outside the Shop phase and during `PhaseTransitionDriver` flights (`ShopChrome.ShowIfActive` gates on `IsTransitioning`), so a child `LateUpdate` only runs while the shop is settled.
- No EditMode tests assert chrome geometry; the v5 geometric baseline (`tools/outputs/shop_hud_baseline_*.json`) was one-shot `execute_code` dumps, not a committed tool.
- World-space RectTransforms have no screen anchors (anchors resolve against the parent rect only) — "anchoring" in world space means per-frame recompute from `Camera.main` (ortho half-width = `orthographicSize × aspect`).

## 3. Design

New component **`HudViewportPin`** (`Assets/Scripts/UXPrototype/HudViewportPin.cs`), authored on the OptionsButton node inside `ShopHudPage.prefab` (prefab-stage edit; v5 convention: the prefab owns placement, the loader stays frozen).

Serialized fields — the exposed insets, world units, **edge-gap semantics** (gap between the viewport edge and the button's bounding edge, computed internally from `RectTransform.rect × lossyScale × 0.5`; no pivot surgery):

- `insetFromRight` — default **0.25** (suggested start; user tunes).
- `insetFromTop` — default **0.18** (preserves the current band row at scroll 0: bandInsetFromTop 0.5 − half height 0.32).

Placement math, run every `LateUpdate` and once in `OnEnable` (avoids a one-frame flash of the authored x=+5 at the first visible frame):

```
corner.x = cam.transform.position.x + cam.orthographicSize * cam.aspect
corner.y = cam.transform.position.y + cam.orthographicSize
position = (corner.x - halfW - insetFromRight, corner.y - halfH - insetFromTop, keep current z)
```

- Z is never written (the page plane and sibling draw order are preserved).
- `Camera.main` null → keep the last position (headless/batch safe).
- Self-contained: `ShopChrome` / `ShopHudPage` / `ShopHudBinder` untouched — wiring is purely prefab data, so the frozen loader API holds.
- The authored local x=+5 becomes dead data once pinned; optional cleanup zeroes it to the default pinned position so the editor view matches runtime.

Config surface = the prefab inspector on the OptionsButton node (Play-mode live tweak works — plain serialized field). This deliberately does NOT resurrect the deleted `ShopChromeConfigSO`; v5 moved placement into the prefab and it stays there.

## 4. Steps (execution order)

1. Create the `.agent_registry` claim (editor claims: `execute_code` prefab edit, `refresh_unity`, `run_tests`; SaveScene pre-approved).
2. Write `HudViewportPin.cs` (CRLF + Tab, UTF-8, English comments; `Write` output must be converted to CRLF before Unity import).
3. Prefab edit via `execute_code` (edit mode): open `ShopHudPage.prefab` contents, `AddComponent<HudViewportPin>` on the OptionsButton instance node, set the two insets, save the prefab asset.
4. Optional cleanup in the same save: author the button's local position to the default pinned spot (kills the stale x=+5).
5. `refresh_unity` (`compile: request`); assert 0 errors AND the Assembly-CSharp DLL mtime > the new `.cs` mtime (stale-assembly trap).
6. Regression run: `SaveOpenScenes()` immediately before, then EditMode shop suite (`ShopSectionPanelsTests`, `ShopBoardPipelineTests`, `ShopRarityWeightSOTests`, `ShopStatsManagerTests`, `UtilityShopBonusTests`; `init_timeout` 180000) — expectation: unchanged green (the geometry contract is untouched).
7. Re-bake the geometric baseline before/after JSON dumps if step 4 was done (plan-shop-hud-prefab-widgets §verification convention).
8. Docs: one clause in the AGENTS.md shop v5 bullet (`wc -c` after, keep ≤ 32 KB), `docs/ShopSystems.md` v5 section, one RegressionChecklist row (behavior change: viewport-pinned widget, 09-21 ruling amendment).
9. Commit straight to `main` (ruling 2026-09-19), single commit; delete the registry claim.

## 5. Play verification matrix (user session)

1. Shop at scroll 0: button sits at the top-right corner with the configured insets; the rest of the band unchanged.
2. Wheel down: the entire band scrolls away EXCEPT the options button — it stays glued to the corner (the ruling change).
3. Wheel up: the band returns; the button did not double-move (no drift between authored and pinned positions).
4. Resize the game window mid-shop (including aspect change): the button re-pins on the next frame to the new corner.
5. 离开商店 / Space → combat: the button is gone (chrome hidden); Result → back to shop: it re-pins on landing with no flash of the authored position.
6. Click the pinned button mid-scroll: the options menu opens (PhysButton collider rides the transform; `ShopInputGate` unaffected).
7. Phase transition flight: the chrome (button included) stays hidden during the glide, per the existing `IsTransitioning` gate.

## 6. Risks / notes

- Narrow aspects: at 4:3 the viewport half-width ≈ 8.08 → pinned button left edge ≈ 7.1 vs the rightmost chip (ChipHearts at x=3.9 + width) — no collision down to 4:3; portrait would overlap (desktop-landscape assumption, unchanged by this plan).
- If future code writes the camera in a `LateUpdate` ordered after this component, the pin trails one frame — acceptable; escape hatch is `[DefaultExecutionOrder]`.
- Insets are user-authored with no clamping: a negative or oversized value can push the button off-screen or over the chips. Tooltips state units and edge-gap semantics; the author is responsible for sane values.
- Scope discipline: ONLY the OptionsButton is pinned. Generalizing to other children means re-amending the 09-21 ruling first.

## 7. Execution record (2026-09-28)

All steps of §4 done as written, with these deltas:

- **Design correction (§2/§3 assumptions were wrong about the widget's nature)**: the OptionsButton root is a plain `Transform` + `BoxCollider2D` — the v5 chrome is a plain-Transform sprite hierarchy with NO RectTransform on the button roots (the plan's §2 "pivot 0.5/0.5, size 0.72×0.64" had misattributed a Label sizeDelta and the Face's 9-slice sprite size to the root). `HudViewportPin` as built measures the **BoxCollider2D footprint** (0.845 × 0.765, offset −0.0125/+0.0125) with offset-aware, lossyScale-aware edge deltas; no collider → insets apply to the transform origin. Consequence: `insetFromTop` default corrected **0.18 → 0.105** (= 0.5 − collider top delta 0.395, still exactly the old band row), and the §4 step-4 editor-view cleanup value corrected 10.1633 → **10.1133**. `insetFromRight` stays 0.25. An intermediate RectTransform-cast version was caught by the baseline dump (comps column showed no RectTransform) BEFORE any Play run — the dump-first discipline paid for itself.
- **Traps hit (all known, all recovered)**: (1) unfocused compile deferral — `refresh_unity` reported `compile_requested` but `HudViewportPin` did not resolve and `isApplicationActive=False`; cleared by a non-empty `run_tests` (SaveScene first, per rule). (2) That forcing run then died as `Test job failed to initialize` — the domain reload consumed the init window; the job self-cleared (`clear_stuck` found nothing), and the post-compile verification (type resolves + assembly mtime 21:19:53 > source 21:19:35) passed. (3) `refresh_triggered:false` again confirmed unreliable — the mtime check is the only truth.
- **Verification**: baseline before/after diff (`tools/outputs/shop_hud_options_pin_before/after.json`, 43 nodes) = the OptionsButton line ONLY (+`HudViewportPin` in comps, localPos 5 → 10.1133); everything else byte-identical. EditMode full suite **664/663 + 1 pre-existing Ignore, 0 failed** = the row-116 baseline. Note: `ShopHudPage.prefab` carries pre-existing uncommitted overrides from the in-flight 09-24/09-27 chip/font work (present in the working tree before this task; the baseline dump is what isolates this change's true delta).
- **Docs**: AGENTS.md v5 bullet got one clause (final size 31735 B, headroom 1033 ≥ 1 KB); `docs/ShopSystems.md` new section "OptionsButton Viewport Pin"; RegressionChecklist row 120 (⚠️ Play pending).
- **Remaining**: the user's Play matrix (§5).
