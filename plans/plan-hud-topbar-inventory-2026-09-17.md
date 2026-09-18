# HUD Top-Bar Inventory + Reusable Info-Chip Design (Guidelines §3.6, shop build-out step 2)

2026-09-17 · status: **implemented & play-verified** (user confirmed 2026-09-17; offline compile 0 errors; edit-mode smoke test passed)

> 2026-09-18 direction update: the canvas `ShopHudBar` from this plan moves to a world-space header strip as part of the all-world-buttons convergence; the shop canvas is retired (`plans/plan-world-entity-shop-chrome-2026-09-18.md`). `HudChip` (flat read-only unit, palette-driven) is kept, generalized to a world prefab.

Implementation record (user decisions applied: HP chip = current/max for future combat reuse, others default):
- `Assets/Resources/HudChip.prefab` (new): panel Image (shop `UISprite`, sliced, raycast off, `TooltipBg`/Navy) + `Value` TMP (`RobotoCondensed-Regular SDF`, 24pt, `TooltipText`/GreyWhite) + `LayoutElement` + `HudChip` (refs wired). Built in-editor via execute_code.
- `Assets/Scripts/UXPrototype/HudChip.cs` (new): flat read-only chip; `Setup(text, accent)` / `SetText` / `SetVisible`; accent = `GameColorPalette.HighlightColor` (new static over the existing `highlight` ColorSO).
- `Assets/Scripts/UXPrototype/ShopHudBar.cs` (new): runtime-built under Shop Canvas (idempotent `BootstrapForShop()` from `ShopUXManager.Start`; no scene edit — PhysButton precedent), horizontal layout, chips Money($, accent) / Income(+$N/combat) / HP(cur/max) / Deck(cur/max); change-cached `Refresh()`; starts hidden, shown by `EnterShop`, hidden by `ExitShop`; also hides the 4 legacy debug TMPs at runtime.
- `ShopManager.cs`: retired dead display writers (`GatherPlayerDeckInfo`, `UpdateShopItemInfo`, `ShowDeck`, `ShowShopItems`, `ShowShopTips`, `ShowPlayerStats`, `_deckInfoStr`/`_shopInfoStr` — all consumers were already commented out); `RefreshIfActive()` wired at buy / sell / reroll-deduction / reroll-spawn; `GetCurrentPayday()` now public.
- Mode chip skipped: `ShowShopTips` (Buy/Sell mode text) was already commented out in live code — sell mode is communicated by the price buttons (售出/买入); re-adding would be new UI, not preservation.
- Free rerolls now live only on the reroll button label (decision #2).
- Note: rarity-weights/奇物架 header strings lived inside the deleted dead builders; the future odds chip will need its own aggregation over `ShopBoardPipeline` weights.

- Fix 2026-09-17 (first Play): bar existed but stayed disabled — the enter-shop UnityEvent (`onEnterShopPhase` → `EnterShop`, scene-bound) fired before `ShopUXManager.Start`'s bootstrap, so `ShowIfActive` hit a null instance. Fix: `BootstrapForShop` ends with a phase check — if the game is already in Shop phase it calls `ShowIfActive` itself; either boot order now shows the bar. Note: the visible `Shop:` / `YourDeck:` scene labels are sectionIdentifier section headers (separate objects from the retired canvas debug TMPs) — kept as wayfinding.

## A. Inventory — §3.6 mockup list vs Unity today

Data plumbing (verified): `ShopManager` owns 4 TMP fields — `phaseInfoDisplay`, `deckInfoDisplay` ("Your Deck: \n\n" debug), `shopInfoDisplay` ("Shop: \n\n" debug), `playerStatsDisplay` — plus `rerollButton`/`rerollButtonBg`/`exitButton`/`sectionIdentifier`. `ShowPlayerStats()` writes one multi-line blob:

```
HP Max: 20
You have: $12 (+$3/combat)
Free Rerolls: 1   (only while > 0)
```

Scene ("Shop Canvas"): `Player Stats Display` (the blob), `Deck Display` / `Shop Display` (debug labels), `General Info Display` (empty), Reroll + Exit buttons (both already PhysButton UI mode). Shop world-space cards/price buttons are separate and fine.

| §3.6 mockup item | Current state | Notes |
|---|---|---|
| leave shop | ✅ exit button | uGUI + PhysButton UI mode |
| reroll | ✅ button | free-reroll-aware label (`UpdateRerollButtonLabel`) |
| money | ⚠️ text line | `purse.value`, only inside the blob |
| income | ⚠️ inline text | mechanic EXISTS: `UtilityShopBonus.ComputePayday(payCheck, session, incomeGrowthPerStep, sessionsPerIncomeStep, _utilityBonus)`; paid at shop entry (`purse.value += GetCurrentPayday()`) |
| HP | ⚠️ text line | "HP Max" only — shop invariant = full HP; hpMax recomputed from deck size (`ApplyHpMaxFromDeck`) |
| deck slots / hearts | ❌ not in HUD | live text no longer shows Deck Size; slot info lives on the `DeckSizeIncreaseEffect` meter card inside the deck; baseline growth = start 3 → cap 16 |
| avatar + username | ❌ in shop | exists combat-side only (`CombatIconPresenter` labels, `???` fallback; Net identity available) |
| rarity odds | ❌ display | data exists: `ShopRarityWeightSO` + per-card `shopRollWeightMultiplier`, consumed by `ShopBoardPipeline` |
| options | ❌ | no settings surface exists |

Also found: free-rerolls display currently lives in BOTH the stats blob and the reroll button label (redundant).

## B. Design proposal — generic reusable chip (user direction, confirmed sensible)

**`HudChip`** (new prefab + component, `UXPrototype/`): one flat read-only info unit, reused for every top-bar stat. Per §3.5 read-only rules + §1.1: no shadow, no lift, default cursor (R2 converse — players never try to press it), colors from `GameColorPalette` statics at build time, zero per-frame cost (refresh-on-demand, not Update).

Structure: root (RectTransform) → flat panel Image (dark translucent, §3.6 panel family) → optional icon Image → one TMP value label. Component API: `SetText(string)`, `SetVisible(bool)`; optional `SetAccent` variant for price-highlight color (money uses `highlight` per §1.1).

**`ShopHudBar`** (new component on Shop Canvas or a bar object): owns a horizontal layout of chips; `Refresh()` pulls all values and is called from the existing `ShopManager` display points (shop entry / after buy / after sell / after reroll / after payday). ShopManager keeps owning logic; the bar is a dumb view — Code Placement Guide compliant (shop UI → ShopManager/ShopUXManager wiring, new file, no god-file growth).

Chip set for phase 1: **Money** (accent color), **Income** (`+$N/combat`), **HP** (`N/N`, full in shop), **Deck** (`current/max`). Phase 2 candidates: Rarity odds (aggregate `ShopBoardPipeline` weights → ✦ percentages), Avatar+username (reuse Net identity + combat label fallback rules). Options button: defer until a settings surface exists.

## C. Decisions needed (defaults proposed)

1. **Retire the debug texts?** `Deck Display` / `Shop Display` / `Your Deck:` list texts duplicate what the physical cards already show. Proposed: remove the TMP wiring + the `_deckInfoStr`/`_shopInfoStr` writers; keep `phaseInfoDisplay` content (Buy/Sell mode) as a small chip or line.
2. **Free rerolls**: proposed — drop from stats blob, keep ONLY the reroll button label (contextual where the action is).
3. **Bar placement**: full-width strip at screen top on Shop Canvas (uGUI, horizontal layout group), world cards untouched.
4. **HP chip shows `hpMax`** (current = max invariant in shop) — confirm no desire to show something else.
5. Commit granularity: HudChip prefab + components + ShopManager wiring = one commit after Play look.

## D. Out of scope here

Combat-phase HUD (already has its own presenter family); PhaseTransition topo background (postponed per user); any new mechanics (options/settings, avatar art).
