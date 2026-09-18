# World-Entity Shop Chrome — all interactive UI converges to world physical buttons

2026-09-18 · status: **implemented** (offline compile 0 errors, both assemblies; play-look verification pending user — chrome geometry constants are first-tune guesses; the scene YAML was hand-edited, reload the scene in Unity before entering Play)

Supersedes the uGUI half of `plans/plan-physbutton-price-button-2026-09-16.md` (PhysButton UI mode) and the canvas bar of `plans/plan-hud-topbar-inventory-2026-09-17.md` (ShopHudBar on "Shop Canvas"). The price-button world mode and the HudChip concept are kept; their render/event substrate changes.

## Direction (user decision 2026-09-18)

Every interactive shop element becomes a **world-space physical button** (`PhysButton` world mode). The shop stops maintaining two input pipelines (physics `OnMouse*` + uGUI EventSystem) for its chrome. The canvas is removed from the shop entirely once migration completes.

### Why (root problem, from the 2026-09-18 review)

- The two pipelines are independent and never arbitrate: a click where a world collider and a canvas graphic overlap fires **both** (concrete case: a price button scrolled under the reroll button — canvas SortingOrder 1 renders on top, so the player sees "reroll", clicks, and buys the card underneath at the same time).
- Modal / global input blocking must be implemented twice; one forgotten path leaks clicks to the world side.
- Hover regions diverge from visual regions: non-raycast chips still occlude world content (canvas always renders above the world), so price buttons hover "behind" the HUD bar.
- Every future interaction idiom (hold, drag, controller/touch) pays double integration tax (PhysButton already carries a World/UI mode pair for this reason).

World-only interaction = one pipeline, one arbitration point, one place to gate input.

## Target architecture

Runtime-built, no scene edit until the final cleanup step (precedent: `ShopHudBar.BootstrapForShop`, price buttons, `AttachPhysButtonsToShopCanvasButtons`).

### New: `ShopChromeAnchor` (tiny component, `UXPrototype/`)

LateUpdate pins the chrome root to the camera's XY at a fixed chrome z (between card layer and camera), so the band stays viewport-fixed while the shop world scrolls beneath it. Camera-rig parent is NOT used: the rig ("Camera Man") is the scroll target and moves; parenting would carry the band away. MilkShake owns the camera's localPosition — the anchor reads `Camera.main.transform.position`, never writes to the camera.

### Chrome contents (left to right, per Guidelines §3.6 order)

- **Exit world button** (`PhysButton` world mode): sliced face + grounded hard shadow + world TMP label "Exit"/离开; `SetWorldAction(() => ShopManager.me.ExitShop())`.
- **Header strip**: none — the first play look (2026-09-18) showed a navy band behind navy chips hides them; Guidelines 3.6 / the demo put the units directly on the shop background. The chips float as individual flat `TooltipBg` panels in a reserved top zone (`BandHeight` = clearance contract only).
- **Chips** (read-only, flat — R2 converse): generalized `HudChip` on a **world prefab variant**: `SpriteRenderer` panel + world `TMP`. `TMP_Text` is the common base of UI/world TMP, so the label field covers both; the panel gets `Image | SpriteRenderer` dual serialized fields, `Setup` picks whichever is present. Accent/palette logic unchanged. Chip set: Money (accent) / Income / HP (cur/max, combat-reuse intact) / Deck (**owned/capacity** — fixes review finding #1: `playerDeckRef.deck.Count / deckSize.value`, not capacity/ceiling).
- **Reroll world button** (`PhysButton` world mode): label carries the existing free/paid text (`UpdateRerollButtonLabel` logic, target swapped to the world TMP); `SetWorldAction(() => ShopManager.me.Reroll())`.

### `PhysButton`

- UI mode is no longer attached (`AttachPhysButtonsToShopCanvasButtons` deleted from `ShopUXManager.Start`). The UI-mode code (`ConfigureUIButton`, `PhysButtonPointerRelay`, hitbox/shadow-sibling creation) stays unused for one release as revert insurance, then is deleted in the cleanup step. World mode is untouched.

### `ShopManager`

- Retire the `rerollButton` / `rerollButtonBg` / `exitButton` uGUI GameObject refs and the `EnterShop`/`ExitShop` SetActive calls on them; the world chrome owns its own visibility (shown by `ShopChromeAnchor`/bar bootstrap + `EnterShop`, hidden by `ExitShop`).
- **Disabled treatment for reroll** (closes the gap from the 2026-09-16 play checklist item 4 that never existed in code): unaffordable and no free rolls left → `SetDisabled(true)` (dim + §2.2 denial once per enter); recompute at `EnterShop` / after each `Reroll` / on buy-sell of FreeReroll utilities. **Mid-roll disable**: `Reroll()` sets it disabled until `SpawnNewShopCardsAfterDelay` completes — double-click during the exit/spawn animation can no longer queue a second roll.
- Price lookup perf (review finding #3): cache `isDeckSlotCard` per `CardScript` (resolve once in `ShopCardView.OnEnable`) instead of `GetComponentInChildren<DeckSizeIncreaseEffect>(true)` on every `GetCardPrice` call from the per-frame `UpdatePriceDisplay`.

### Input gate: `ShopInputGate` (new static, refcounted Block/Unblock)

Mirrors the `CombatManager.IsInputBlocked` pattern. Consulted by every shop interaction entry: `PhysButton` (OnMouseDown + release), `CardPhysObjScript` click-to-enlarge, `ShopCardView` restore-click. Initially blocked during the reroll animation. With a single pipeline, one gate covers the whole shop; a canvas popup can no longer leak purchases through the world side, because there is no world side left outside the gate.

### Layout contract (the other half of the fix)

The chrome band's viewport footprint is a **reserved zone**: card layout and camera-scroll bounds must keep cards out of it. Scroll is downward-only (`ComputeDynamicMinY` governs the bottom), so the binding constraint is the initial view: the shelf layout constants must place the topmost card below the band's bottom edge, with the margin as a named constant. Contract is documented in `AGENTS.md` at implementation time.

## Migration order

1. **World reroll/exit buttons + `ShopChromeAnchor`** — build, wire actions/labels/disabled, delete `AttachPhysButtonsToShopCanvasButtons`. (Kills the dual-pipeline double-fire risk immediately.)
2. **`ShopInputGate`** — wire PhysButton + card-body click paths; mid-roll disable rides along.
3. **World header strip + generalized `HudChip` + world chip prefab** — `ShopHudBar` rebuilds as world layout (manual left-to-right placement, spacing constant; no `HorizontalLayoutGroup` in world space); deck-chip fix and price-cache ride along. Legacy debug texts stay hidden until step 4.
4. **Cleanup (one scene edit)** — delete the "Shop Canvas" objects from `GameScene.unity`; delete PhysButton UI-mode code + `PhysButtonPointerRelay`; remove `LegacyDisplayNames` hiding; retire the `ShopManager` uGUI refs. Sync `docs/UIUX_Guidelines.md` + `AGENTS.md`.

## Verification

1. Band + buttons stay viewport-fixed at every scroll position; no card ever enters the band (all resolutions).
2. Reroll: click rolls once; double-click mid-animation rolls once; unaffordable → dim + head-shake once per enter, label live; free-count text correct; exit button leaves the shop.
3. Where canvas buttons used to overlap world content, clicks now have exactly one effect (single pipeline).
4. Chips refresh on enter / buy / sell / reroll; deck chip shows owned/capacity and moves when buying.
5. Enlarge-preview click + restore still work and are gated with the price buttons.
6. No missing-reference console errors; offline compile 0 errors; play look verified by user.

## Out of scope

- Combat-phase canvas HUD (separate phase with its own presenters; its own convergence decision later if desired).
- The options/settings surface — becomes a world button when that feature exists.
- Tooltip engine changes (engine placement convention already frozen).

## Sub-decision flagged for user

Chips moving onto the world band is the recommendation, but it is separable: chips are non-interactive, so they never suffered the dual-input bug. If you want the smaller step, do 1 + 2 + 4 only — keep `ShopHudBar` on canvas as read-only overlay. The cost of that veto: the canvas stays as a render layer that always draws above the world (enlarge/hover visuals pass "under" the bar; the retired-canvas cleanup of step 4 is partial). Default in this plan: full move (1–4).
