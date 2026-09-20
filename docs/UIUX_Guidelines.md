# OneDeck UI/UX Guidelines

Frozen decision record for OneDeck UI interaction feel, distilled from the executable spec `docs/demo/UIKitDemo.html` (v0.8, 2026-09-08). The demo remains the interactive tuning surface — tune there, then sync the numbers here and into the Unity implementation. Card-flight and shop↔combat phase-transition motion are specced separately in `docs/demo/PhaseTransitionDemo.html` (no Unity port plan yet) and are out of scope here.

## R0. Light Source (inviolable)

- Global light source is **top-left**. Every interactive element lifts toward the top-left on hover; its hard shadow stays anchored at the bottom-right "ground" position.
- Recessed elements (empty slots) invert this: inner shading sits at the top-left.
- No element (card, button, tooltip, popup, slot) may cast a shadow that violates this convention.

## 1. Design Tokens

### 1.1 Colors — authority lives in Unity

The demo hex values are stand-ins only and are never ported. Production colors come from `GameColorPalette` / `ColorSO` assets — reference the field, never hardcode hex.

| Demo token | Role | `GameColorPalette` field | ColorSO asset |
|---|---|---|---|
| `--paper` | stage gray (page background) | — not in palette | — |
| `--panel` | section panel background | — not in palette | — |
| `--recess` | recessed empty slot | `slotRecess` | `SlotRecess` |
| `--face` | interactive face (lit) | `ownerCardColor` | `GreyWhite` |
| `--face-dim` | disabled face | `cardFaceDim` | `CardFaceDim` |
| `--ink` | primary text / dark card | `ownerTextColor` | `Navy` |
| `--ink-soft` | secondary text | `cardTextSoft` | `CardTextSoft` |
| `--shadow-c` | hard shadow | `cardShadow` | `UIShadow` |
| `--card-art` | card art zone (dark navy) | `cardArtBg` | `Black` |
| tooltip bg / text | tooltip item | `tooltipBg` / `tooltipText` | `Navy` / `GreyWhite` |
| price & numeric accent | price, emphasized numbers | `highlight` | `LogHighlight` (#FFEB04) |

### 1.2 Shape

- Corner radius (demo px, map to Unity UI corners): button 8, card 10, chip 6.

## 2. Physical Interaction Model (`.phys` state machine)

One state machine covers buttons and cards alike. States: `rest → hover → pressed → activate on release`; leave-while-pressed cancels; re-enter-while-held re-presses.

| State | Face position | Shadow | Cursor | Response |
|---|---|---|---|---|
| rest | origin | `rs` | pointer | — |
| hover | −`hl` toward top-left | `rs + hl` (ground-anchored) | pointer | — |
| pressed | +`rs` (lands on shadow) | 0 | pointer | — |
| held-out | back to hover/rest | restored | pointer | cancelled; release does not fire |
| disabled | origin (denial anim on enter) | 0 (dim face) | not-allowed | refusal feedback only |
| read-only | origin | 0 (flat) | default | none |

Rules:

- **R2 interactivity**: interactive = lit face + shadow (lifted); read-only = flat; disabled = dim face + no shadow + denial animation. The converse must hold — information displays (HP numbers, chips) stay flat so players never try to press them.
- **R4 hover**: shadow grows by the same offset the face travels, so the shadow visually stays on the ground.
- **R5 press**: face lands on the shadow (`pressTarget: ground`), shadow collapses to 0. Press must feel crisp — its duration is always shorter than hover.
- **R6 hold & cancel**: holding keeps the pressed state; dragging out cancels (release won't fire); dragging back in while still held re-presses.
- **R7 activation timing**: clicks fire on **release**, never on press — leaving room to cancel.

### 2.1 Spec values (demo defaults = the numbers to port)

| Parameter | Value | Notes |
|---|---|---|
| `restShadow` (rs) | 4 px | rest shadow offset |
| `hoverLift` (hl) | 4 px | extra travel toward the light on hover |
| `hoverDur` | 120 ms | hover / release duration |
| `pressDur` | 60 ms | press duration (< hoverDur, always) |
| `ease` | overshoot, strength 1.7 | CSS `cubic-bezier(0.34, 1.7, 0.64, 1)`; DOTween ≈ `Ease.OutBack` with overshoot ≈ 1.7 |
| `pressTarget` | ground | face lands on the shadow (alternative "rest" kept only for feel comparison) |
| `tipDelay` | 250 ms | tooltip appear delay — **resolved 2026-09-16 (user decision): the engine value wins** — Unity `CombatUXManager.hoverPopUpDelay` stays at 0.1 s |
| `tipMargin` | 24 px | min left-gap before tooltip flips to the right side |
| tooltip viewport clamp | 8 px | hard margin, always inside viewport |

Unit notes: ms → s on the Unity side; px offsets are design-space units — convert per context (screen-space UI px directly, world-space via the project's `pxToWorld` where relevant).

### 2.2 Disabled refusal ("head-shake no")

Plays **once per pointer enter** (re-enter replays). Phases, all parameters tunable:

1. Slide out toward the light by `denyShift` (6 px) over `denyOut` (80 ms).
2. `denyWiggles` (1) wiggle pairs around the out point; each swing takes `denyWiggleDur` (110 ms), amplitude = `denyShift × denyAmp` (0.4), decaying ×0.55 per wiggle. Mode: horizontal translate (default) or rotation at `denyRotAmp` (5°).
3. One extra swing recenters at the out point.
4. Hold for `denyHold` (150 ms).
5. Retreat to origin over `denyRetreat` (120 ms).

Total duration = `denyOut + (2·denyWiggles + 1)·denyWiggleDur + denyHold + denyRetreat` = **680 ms** at defaults. The shadow mirrors the face offset (ground-anchored) throughout.

## 3. Components

### 3.1 Push Button

- Four states per the state machine above.
- **Hover text swap** (text-only faces): rest shows the price, hover swaps to the action label (e.g. `$4` → `买入`). The swap is **instant — no fade**; the lift animation already carries the transition. Click behavior unchanged; original text restored on leave.

### 3.2 Card Template v1.1 (2026-08-18)

Zones, top to bottom:

1. **Art zone** — dark rounded box (illustration placeholder), 56% of card height.
2. **Rarity** — ✦×1–3, right-aligned just under the art's bottom-right.
3. **Effect text** — automatic `> ` prefix (authored marker, not part of `cardDesc`), wraps to full text — **never truncated**. The rarity row flex-grows to absorb free space (and shrinks first when the effect needs more lines), so the effect **hugs the divider and extra lines grow upward**. Cards with no effect **omit the effect row entirely** (no dangling `>`).
4. **Divider**.
5. **Bottom row** — name (left, bold, squashed horizontally to fit on overflow — never ellipsized, no squash floor) + attack (right, same baseline, larger font).

Layout rules:

- All inner sizes are em-based off the card's font-size — zooming the card = changing font-size only.
- Card interaction physics are identical to buttons (same state machine).
- Price and other shop attachments hang **outside** the card face, never inside it.
- Open question: rarity-star readability once real illustrations land.
- Unity port (2026-09-05, `plans/plan-card-template-v1.1-port-2026-09-05.md`): implemented on `PhysicalCardParent.prefab`. Adaptations: rarity sits under the actual art-image bottom (the image is kept larger than the demo's 56% zone); `✦` glyph comes from a bundled Noto Sans Symbols 2 fallback font; cardDesc text must use halfwidth punctuation only (no fullwidth glyph coverage).

### 3.3 Price-Button Buy/Sell

- Buy/sell = **single click on the price button** under the card, fired on release (R7). Long-press was dropped 2026-09-05: it was the only long-press idiom in the UI, an inconsistent interaction language.
- The price is a **button**, not a label: full state machine (hover lift / press / drag-out cancel / disabled refusal). **Hover text swap** (`$4` → `买入`, `$2` → `售出`) is the only pre-transaction hint — instant swap, no fade.
- The card body stays interactive on its own (click = enlarge preview) and never transacts; the price button only transacts. The two never interfere.
- Sell price = half the base price (current rule).
- **The trigger confirmation IS the consequence motion**: no flash/invert on fire — in the real shop the bought card flies to a deck slot and the sold card flies out; a color blink before that flight breaks object permanence. (Flight motion itself is specced in `PhaseTransitionDemo.html`.)
- Unaffordable: card face and price button are both dim-disabled; hovering plays the denial animation once per enter.
- Unity port (2026-09-16, `plans/plan-physbutton-price-button-2026-09-16.md`): implemented via the `PhysButton` component (world-sprite mode) — the price print under the card became the button label with a sliced face + ground shadow; buy/sell fires on release inside; hover text swap (`$4` → `买入` / `$2` → `售出`); unaffordable dims the card face (`CardPhysObjScript.SetFaceDimmed`) and the button, with the §2.2 denial on hover. Long-press removed from `ShopCardView`. The consequence motion (buy = card tweens to its deck slot, sell = card flies to the shop start and shrinks) already existed, so no flash/invert was needed. The uGUI reroll/exit buttons get the same feel via `PhysButton` UI mode, attached at runtime by `ShopUXManager` (no scene edit); their activation stays owned by uGUI `Button`.
- Card body interaction (2026-09-16 user decision): **hover preview in combat** (`CardPhysObjScript` hover popup); in the shop the card body click-to-enlarge preview stays and never transacts.
- **Direction change (2026-09-18, `plans/plan-world-entity-shop-chrome-2026-09-18.md`): all interactive shop chrome became world physical buttons; the PhysButton UI mode (this port note's uGUI half) is deleted along with "Shop Canvas" — two independent input pipelines (physics `OnMouse*` + EventSystem) with no arbitration was the root defect it papers over.

### 3.4 Tooltip

- A tooltip is an **attachment of an interactive host**, never a standalone element. The host keeps its own hover physics (cards still lift); the tooltip appears after `tipDelay` (250 ms) and disappears **immediately** on leave.
- Placement: host's **left side, vertically centered**; flips to the right when the left gap < `tipMargin` (24 px); always clamped inside the viewport with an 8 px hard margin; **re-follows the host in real time** on move / resize / scroll. **Resolved 2026-09-16 (user decision): the engine placement wins** — Unity `CardTagTooltip` sits on the host's **right side, flipping left on overflow**, and that is the frozen convention (the demo's left-default is a deliberate divergence, do not "fix" either side toward the other).
- One host may carry **multiple tooltips**, stacked vertically (a hard cap is TBD).
- Tooltips are flat, non-physical, and never intercept the pointer. Content = tag name + description.
- Unity counterparts: `CardTagTooltip` (hover via `CardPhysObjScript`) and `CombatUXManager.hoverPopUpDelay`.

### 3.5 Read-only & Recessed

- HUD chips and numeric displays are flat: no shadow, no displacement, default cursor.
- Empty deck slots are **recessed**: inner shadow at the top-left (same light source, inverted), non-interactive.

### 3.6 Shop Page Layout (2026-09-05 mockup)

- Page structure: top HUD bar (**leave shop**, avatar + username, HP, money, rarity odds, deck slots / hearts, income, **options**) + **Shop** panel (**reroll** + shelf cards, each with a buy price button) + **Deck** panel (slot counter, owned cards each with a sell price button, empty slots).
- The gray topographic background is the player HP display base (the seamless-world concept lives in `PhaseTransitionDemo.html`); shop/deck panels are dark translucent overlays.
- Interactive elements — leave shop, options, reroll, price buttons — all reuse the global physics params (2.1); no page-specific tuning. HP / money / rarity odds / counters / income are read-only flats (the R2 converse).
- Empty deck slots are recessed (3.5); buy/sell follows 3.3.
- Unity port (2026-09-17, `plans/plan-hud-topbar-inventory-2026-09-17.md`): the top HUD bar is a runtime-built `ShopHudBar` (no scene edit) of flat `HudChip` read-only units (R2 converse) — panel = `TooltipBg`, text = `TooltipText`, accent numbers = `highlight`; chips Money / Income (payday +$N/combat) / HP (cur/max) / Deck (cur/max). The HP chip is current/max by design so the combat-side own-HP display can reuse the same prefab. Legacy debug TMP displays are hidden at runtime; free-rerolls count lives only on the reroll button label.
- **Direction change (2026-09-18, `plans/plan-world-entity-shop-chrome-2026-09-18.md`): the bar (and reroll/exit) moved from this canvas to a world-space header strip with world buttons — "Shop Canvas" is deleted from the shop. Chips stay flat/read-only (R2 converse) as world `HudChip`s. Plan has motivation + migration order.
- **Chrome v2 + section panels (2026-09-18, `plans/plan-shop-panels-port-2026-09-18.md`)**: the annotated layout shipped — chrome top bar rows ([avatar+username / HP / $] | [✦/✦✦/✦✦✦ rarity-odds % from the active `ShopRarityWeightSO`] | [Wins / Hearts / +$N/combat]) with Exit + options placeholder; the deck chip moved into the Deck panel header as a `03/05` counter, and the reroll button moved into the Shop panel header. Three runtime-built translucent `ShopSectionPanels` (Shop/Deck/Upgrades) wrap the rows; owned utility passives (`occupiesDeckSlot` = false) render in the Upgrades row below the deck band. Wins/Hearts = `PhaseManager.wins/winCon/hearts/heartMax`; username = `PlayerIdentity.Username`. Adaptations: the annotated layout's icons `❚❚` (U+275A, options) / `🜲` (U+1F732, wins — not the demo's ▦) / `♥` (U+2665, hearts) are outside every bundled static font atlas (✦ only). fontTools-verified: `♥`/`❚` exist in Noto Sans Symbols 2 (✦-style fallback-atlas bake can provide them); `🜲` exists in NO bundled font — Wins/Hearts use full-word labels and the options button shows "||" until the atlas is baked.

- **Chrome v3 — §08 row order + combat-HUD reuse + glyph completion (2026-09-19, `plans/plan-shop-topbar-combat-hud-reuse-2026-09-19.md`)**: the top bar is now the §08 three-row column — row 0 = **离开商店** | the reused combat avatar+username | the reused combat horizontal HP display | money chip … **❚❚** (all level); row 1 = rarity odds; row 2 = **🜲** wins / **♥** hearts / `+$N/ROUND` — rows 1-2 left-aligned on the HUD column. The avatar (`CombatIconPresenter` player side) and the HP pill (`HPNumericDisplayHorizontal` player side) are the SAME combat components, re-anchored per phase from `ShopTopBarLayout` viewport constants and restored on combat entry; both stay read-only (`raycastTarget` = false), preserving the single physics input pipeline. Chrome copy is Chinese (离开商店 / 商店 / 卡组 / 商店升级 / 重掷 $N / 空卡位 / 从商店购买卡牌). The v0.12 glyph TODO is resolved: `♥` U+2665 and `❚` U+275A are baked into `NotoSansSymbols2 SDF`; `🜲` U+1F732 comes from the new `NotoSansSymbolsAlchemical SDF` (Noto Sans Symbols, OFL), appended last on the `RobotoCondensed-Regular SDF` fallback chain. `PlayerIconShopScale` is 0.28, not the combat 0.4 — the icon's `small shadow` child is 222×306, so a larger shop scale clips it at the viewport top.
- **Known-open (2026-09-19)**: the Shop panel header row（商店 + 重掷 $2）sits inside the chrome band (world Y 4.33 vs band bottom 3.46). This is cosmetic — `ShopChrome.CheckShelfClearance` guards shelf *cards*, not the panel header, and the header no longer collides with any chrome element (nearest is the HUD-column chips at X −3.01 vs the header at −4.82). Moving it below the band requires lowering the shelf ~1.4u, which at the current scene pitches (`shopItemPos.y` 1.50 / `playerDeckPos.y` −3.40 / `yOffset` 4.5) would push the shelf's price buttons into the deck row and the deck panel ~0.2u off-screen — it needs a shop-pitch re-tune, not a header nudge. Note also that `shopItemPos`/`playerDeckPos` are **scene-serialized** (`GameScene.unity`), not code constants.

## 4. Unity Implementation Notes

- Durations map ms → s; offsets map px → UI px or world units per context (see 2.1).
- Easing: overshoot mode ≈ `Ease.OutBack`(overshoot 1.7); smooth mode = `cubic-bezier(.2,.7,.3,1)`.
- All colors via `GameColorPalette` fields (1.1) — no hardcoded hex.
- Input-side rules (activation on release, drag-out cancel, price-button transactions) belong to the component event layer, not the animation layer.

## Version History

- v0.13 · 2026-09-19 · Shop top bar reordered to the §08 three-row column (row 0 = 离开商店 | reused combat avatar+username | reused combat horizontal HP display | money … ❚❚; rows 1-2 = rarity odds, then 🜲 wins / ♥ hearts / +$N/ROUND, left-aligned). The combat avatar and HP pill are reused in the shop and re-anchored per phase by `ShopTopBarLayout`. Fixed `HPNumericDisplayHorizontal` never re-placing on a visible→visible phase change (Shop→Combat left the pill at the shop anchor/scale). Glyph TODO closed: `♥`/`❚` baked into `NotoSansSymbols2 SDF`, `🜲` added via the new OFL `NotoSansSymbolsAlchemical SDF` on the chrome font's fallback chain. Chrome copy switched to Chinese. Plan: `plans/plan-shop-topbar-combat-hud-reuse-2026-09-19.md`. See 3.6.

- v0.12 · 2026-09-18 · Shop-page panels port (§08): chrome top bar v2 (avatar+username, rarity-odds chips, Wins/Hearts, options placeholder), three world `ShopSectionPanels` with headers + `03/05` deck counter, reroll relocated into the Shop panel header, owned utility passives moved into the Upgrades panel row. Wins/Hearts = `PhaseManager` IntSOs; `❚❚`/`🜲`/`♥` (U+275A/U+1F732/U+2665) absent from every static font atlas (`🜲` not in any bundled font; `♥`/`❚` bakeable from Noto Sans Symbols 2) → full-word Wins/Hearts labels, "||" options. Plan: `plans/plan-shop-panels-port-2026-09-18.md`. See 3.6 port notes.

- v0.11 · 2026-09-18 · All interactive shop chrome ported to world physical buttons: runtime `ShopChrome` (band + `HudChip`s + reroll/exit, `ShopChromeAnchor` viewport pin) replaces the canvas `ShopHudBar` + uGUI reroll/exit; "Shop Canvas" deleted from the scene; PhysButton UI mode + `PhysButtonPointerRelay` deleted; single input pipeline gated by `ShopInputGate`; shelf-below-band layout contract. Rationale: two un-arbitrated input pipelines (physics vs EventSystem) — click-through double-fire, duplicated modal blocking, hover/visual region divergence. Plan: `plans/plan-world-entity-shop-chrome-2026-09-18.md`. See 3.3/3.6 port notes.

- v0.10 · 2026-09-17 · Recessed empty slots (§3.5) ported as a prefab bake; top HUD bar ported (§3.6): runtime `ShopHudBar` + `HudChip` read-only chips, legacy debug texts retired. HP chip = current/max (combat reuse planned). See 3.5/3.6.

- v0.9 · 2026-09-16 · Price-button buy/sell ported (§3.3): `PhysButton` world mode on shop cards, long-press removed; uGUI reroll/exit buttons get the §2 feel via runtime-attached UI mode. Decisions frozen: engine wins on tooltip placement (right-side default, flip left) and `tipDelay` (0.1 s); card body = hover preview in combat, click-enlarge in shop (§3.4, §2.1).

- v0.8 · 2026-09-08 · Card effect text: 2-line `…` clamp removed — desc wraps to full text, never truncated (Unity: desc TMP overflow Ellipsis→Overflow via `PhysicalCardParent.prefab` override; demo `.card-effect` line-clamp removed, rarity row shrinks first). See 3.2.

- v0.7 · 2026-09-06 · Card-name overflow: ellipsis replaced by unlimited horizontal squash (demo `scaleX`; Unity `TMP_Text.characterHorizontalScale` in `CardPhysObjScript.FitCardNamePrint`, plan `plans/plan-card-name-horizontal-squash-2026-09-06.md`). Desc 2-line clamp keeps its `…` (removed in v0.8).

- v0.6 · 2026-09-05 · Buy/sell dropped long-press for price-button single click (section 04 rewritten; `lpThreshold` param and `lp-fill` removed); added section 08 shop page (2026-09-05 mockup layout; all interaction params referenced from the global bar).

- v0.5 · 2026-08-19 · Card-flight / phase-transition motion split out to `PhaseTransitionDemo.html`.
- v0.4 · 2026-08-18 · Section 01 swatches annotated with `GameColorPalette` mappings.
- v0.3 · 2026-08-18 · Effect text hugs the divider and grows upward; no-effect cards omit the row; ease defaults to overshoot; wiggle count defaults to 1.
- v0.2 · 2026-08-18 · Section 03 card template added (art / rarity ✦1–3 / effect / name+attack bottom row); sections 04–05 migrated to it.
- v0.1 · 2026-08-15 · Initial version (tokens / button / long-press card / tooltip / read-only & recessed / state matrix).
