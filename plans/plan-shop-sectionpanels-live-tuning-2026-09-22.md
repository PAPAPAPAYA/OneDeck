# Shop Section Panels — Live Inspector Tuning (shared tuning set)

- **Date**: 2026-09-22
- **Status**: IMPLEMENTED 2026-09-22 (code landed; Play checks §6 still need the user's run)
- **Execution record (2026-09-22)**: §5 steps 1-2 done as written, no deltas. Compile clean (assembly mtime > both source mtimes, console 0 errors); EditMode full suite 671 total / 670 passed / 0 failed / 1 pre-existing Ignore (RecorderAnimationPlayer nested-coroutine timing, passes standalone). Play checks (§6) pending the user's session. Uncommitted at implementation time.
- **Request (user, 2026-09-22)**: make `ShopSectionPanels` values Inspector-tunable in Play Mode — one component at a time, chrome/top bar explicitly out of scope for now. The panels must keep following their card areas: Shop panel ↔ shelf cards, Deck panel ↔ deck cards (+ empty slots), Upgrades panel ↔ owned utility cards.
- **User rulings (2026-09-22)**:
	1. ONE shared tuning set for all three panels (13 fields), not per-panel values.
	2. Confirmed semantics kept as-is: the Deck panel covers the whole grid even at 0 cards (empty slots ≡ deckSize, same model as the 2026-09-21 counter fix); an empty shelf hides the Shop panel + header + reroll button entirely, an empty Upgrades row hides that panel — seeing a panel disappear while tuning means the content list is empty, not a bug.
- **Reads**: `plans/plan-shop-panels-port-2026-09-18.md` (panel port baseline), `plans/plan-shop-topbar-world-scroll-2026-09-21.md` (page-content context; chrome tuning is a follow-up, NOT in this plan).

## 1. Goal / non-goals

Goal: the 13 panel appearance parameters become `ShopUXManager` Inspector fields, re-applied to the live panels on the next frame via the existing `_layoutDirty` channel. Tuned values live on `ShopUXManager` (a persistent scene component) so a Play-mode tuning session can be copied back into the scene and saved.

Non-goals: `ShopChrome` consts (needs build-baseline capture — own plan later), `ShopTopBarLayout` statics (shared with the canvas presenters `CombatIconPresenter` / `HPNumericDisplayHorizontal`, which only re-apply at shop handoff — separate effort), `PanelZ`/`HeaderZ` (z-layering contract stays const), `Face*` bake factors (structural, not visual knobs), and ANY change to the bounds-follow derivation or the relayout→refit chain.

## 2. Current state (verified facts)

- **Follow behavior already exists** — `RefreshLayout()` fits each panel around its own content targets (`ShopSectionPanels.cs:126-130`): Shop ← `SpawnedShopCards`; Deck ← `SpawnedPlayerCards` + `SpawnedEmptySlots`; Upgrades ← `SpawnedUtilityCards`. Nothing in this plan touches that.
- **Refit choke point**: `RelayoutPlayerDeckCards` ends with `ShopSectionPanels.Instance?.RefreshLayout()` (`ShopUXManager.cs:434`). Every card-list change reaches it: buy/sell/deckSize growth call `RelayoutPlayerDeckCards` directly (`:868, :892, :945, :970, :1009`); entry/reroll share `SpawnShopCardsInternal`, which ends with `RelayoutDeckBand()` → cascade (`:1210`).
- **The 13 tunable consts** (`ShopSectionPanels.cs:23-35`): `HeaderHeight` 1.2, `SidePadding` 0.55, `TopPadding` 0.35, `BottomPadding` 0.5, `FitTweenDuration` 0.3, `HeaderFontSize` 3.2, `HeaderLeftMargin` 0.25, `HeaderRightMargin` 0.25, `ButtonWidth` 2.0, `ButtonHeight` 0.56, `ButtonFontSize` 2.4, `RestShadowUnits` 0.05, `DenyShiftUnits` 0.075.
- **Build-once style values that do NOT re-apply today** (a retune must rewrite them on the cached refs): header TMPs' `fontSize` (`CreateHeader`, `:207`); reroll label `fontSize` + rect `sizeDelta` (`CreateButtonLabel`, `:262-267`); reroll face/collider size (`ConfigureWorldFaceSize`, `:249`); PhysButton press/hover params `restShadow`/`hoverLift`/`denyShift` (`:245-247`).
- **Existing live-tuning channel**: `ShopUXManager.OnValidate` → `_layoutDirty` → `Update` → `RelayoutAll()` (`ShopUXManager.cs:691-708`). OnValidate fires for ANY ShopUXManager field edit — the extra refit trigger is intended and cheap (transform writes + tweens).
- **Tween safety**: `FitPanel` already `DOKill`s before re-tweening (`:307-308`), so rapid re-tunes can't stack tweens.
- **Tests**: `ShopSectionPanelsTests` only calls parameterized statics (`ShopSectionPanelsTests.cs:42-115`) — zero impact.

## 3. Design

### 3.1 Where the values live
`ShopUXManager` gets a nested `[Serializable] public class PanelsTuning` with the 13 public float fields (defaults = today's const values) and `public PanelsTuning panels = new PanelsTuning();`. Rationale: the panels are a runtime-built scene-root object — tuned values on it would die on exit and can never persist; `ShopUXManager` is the persistent scene component and the established home of shop tunables (same surface as `xOffset`/`shopItemPos`).

### 3.2 Point-of-use resolution, no snapshots
Existing consts stay as `XxxDefault` fallbacks. Every use site resolves the tuning through a small private helper (`ShopUXManager.Instance != null ? Instance.panels : null`, else default) at READ time — values are always current, no copies to sync.

### 3.3 Style re-apply block in `RefreshLayout()`
New block at the head of `RefreshLayout()`: the three header TMPs' `fontSize`; reroll label `fontSize` + `sizeDelta = (ButtonWidth − 0.2, ButtonHeight)`; reroll `ConfigureWorldFaceSize(new Vector2(ButtonWidth, ButtonHeight), Vector2.zero)`; reroll PhysButton `restShadow`/`hoverLift`/`denyShift`. Panel geometry itself already re-derives from the paddings on every `FitPanel` call, so this block only covers the Build-once leftovers.

### 3.4 Snap when hidden
`FitPanel` passes tween duration 0 when `!gameObject.activeSelf` (panels hide during phase travel); the active case keeps the gliding fit (`FitTweenDuration`, now tunable).

### 3.5 Wiring
`ShopUXManager.Update`'s dirty consumer adds after `RelayoutAll()`:

```csharp
if (!PhaseTransitionDriver.IsTransitioning) ShopSectionPanels.RefitIfBuilt();
```

New static entry `public static void RefitIfBuilt() { if (_instance != null) _instance.RefreshLayout(); }`. The `IsTransitioning` guard skips pointless mid-flight work; landing re-shows via `ShowIfActive` → `RefreshLayout()` with current values (`ShopSectionPanels.cs:96-102`).

## 4. Tuning fields (name → default → effect)

| Field | Default | Effect |
|---|---|---|
| `headerHeight` | 1.2 | Header band height above content (panel top grows) |
| `sidePadding` | 0.55 | Horizontal content→panel-edge gap |
| `topPadding` | 0.35 | Content→panel-top gap (below header) |
| `bottomPadding` | 0.5 | Content→panel-bottom gap |
| `fitTweenDuration` | 0.3 | Panel glide on refit |
| `headerFontSize` | 3.2 | 商店 / 卡组 / 商店升级 header size (re-applied live) |
| `headerLeftMargin` / `headerRightMargin` | 0.25 / 0.25 | Header inset from panel edges |
| `buttonWidth` / `buttonHeight` | 2.0 / 0.56 | Reroll button face + collider |
| `buttonFontSize` | 2.4 | Reroll label size (re-applied live) |
| `restShadowUnits` | 0.05 | Reroll rest/hover shadow depth |
| `denyShiftUnits` | 0.075 | Reroll deny shake distance |

Kept const on purpose: `PanelZ` 0.5 / `HeaderZ` 0.4 (z contract), `Face*` factors (bake-derived).

## 5. Implementation checklist

1. `Assets/Scripts/UXPrototype/ShopUXManager.cs`: `PanelsTuning` class + `panels` field + Update wiring (+~25 lines).
2. `Assets/Scripts/UXPrototype/ShopSectionPanels.cs`: consts → `*Default`, resolver helper, `RefreshLayout` style block (§3.3), `FitPanel` snap (§3.4), `RefitIfBuilt` (+~40 lines).
3. No scene edit (new fields take initializer defaults); `Bootstrap(sprite, font)` signature unchanged; tests untouched.

## 6. Verification

- Compile 0 errors (offline csproj build or `refresh_unity`).
- EditMode full suite green (baseline 667/668, 1 pre-existing Ignore).
- Play (user session): tune each field → all three panels re-fit next frame; after tuning, follow behavior still live on buy / sell / reroll / deckSize growth; empty-shelf panel hiding unchanged; panels stay behind cards / headers in front (z contract).

## 7. Risks / notes

- Play-mode edits do not persist by themselves — values sit on the scene component, so copy the component in Play mode, paste back in Edit mode, save.
- OnValidate over-triggers the refit for unrelated ShopUXManager edits — accepted by design (negligible cost, keeps one dirty channel).
- Chrome + `ShopTopBarLayout` tuning stays open; it needs Build-baseline capture and a canvas-presenter re-apply path — do NOT reuse this plan's shape blindly there.
