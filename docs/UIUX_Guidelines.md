# OneDeck UI/UX Guidelines

Frozen decision record for OneDeck UI interaction feel, distilled from the executable spec `docs/demo/UIKitDemo.html` (v0.6, 2026-09-05). The demo remains the interactive tuning surface — tune there, then sync the numbers here and into the Unity implementation. Card-flight and shop↔combat phase-transition motion are specced separately in `docs/demo/PhaseTransitionDemo.html` (no Unity port plan yet) and are out of scope here.

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
| `tipDelay` | 250 ms | tooltip appear delay — **discrepancy**: Unity `CombatUXManager.hoverPopUpDelay` is currently 0.1 s; reconcile when porting |
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
3. **Effect text** — automatic `> ` prefix (authored marker, not part of `cardDesc`), clamps to 2 lines. The rarity row flex-grows to absorb free space, so the effect **hugs the divider and a second line grows upward**. Cards with no effect **omit the effect row entirely** (no dangling `>`).
4. **Divider**.
5. **Bottom row** — name (left, bold, ellipsis on overflow) + attack (right, same baseline, larger font).

Layout rules:

- All inner sizes are em-based off the card's font-size — zooming the card = changing font-size only.
- Card interaction physics are identical to buttons (same state machine).
- Price and other shop attachments hang **outside** the card face, never inside it.
- Open question: rarity-star readability once real illustrations land.

### 3.3 Price-Button Buy/Sell

- Buy/sell = **single click on the price button** under the card, fired on release (R7). Long-press was dropped 2026-09-05: it was the only long-press idiom in the UI, an inconsistent interaction language.
- The price is a **button**, not a label: full state machine (hover lift / press / drag-out cancel / disabled refusal). **Hover text swap** (`$4` → `买入`, `$2` → `售出`) is the only pre-transaction hint — instant swap, no fade.
- The card body stays interactive on its own (click = enlarge preview) and never transacts; the price button only transacts. The two never interfere.
- Sell price = half the base price (current rule).
- **The trigger confirmation IS the consequence motion**: no flash/invert on fire — in the real shop the bought card flies to a deck slot and the sold card flies out; a color blink before that flight breaks object permanence. (Flight motion itself is specced in `PhaseTransitionDemo.html`.)
- Unaffordable: card face and price button are both dim-disabled; hovering plays the denial animation once per enter.

### 3.4 Tooltip

- A tooltip is an **attachment of an interactive host**, never a standalone element. The host keeps its own hover physics (cards still lift); the tooltip appears after `tipDelay` (250 ms) and disappears **immediately** on leave.
- Placement: host's **left side, vertically centered**; flips to the right when the left gap < `tipMargin` (24 px); always clamped inside the viewport with an 8 px hard margin; **re-follows the host in real time** on move / resize / scroll.
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

## 4. Unity Implementation Notes

- Durations map ms → s; offsets map px → UI px or world units per context (see 2.1).
- Easing: overshoot mode ≈ `Ease.OutBack`(overshoot 1.7); smooth mode = `cubic-bezier(.2,.7,.3,1)`.
- All colors via `GameColorPalette` fields (1.1) — no hardcoded hex.
- Input-side rules (activation on release, drag-out cancel, price-button transactions) belong to the component event layer, not the animation layer.

## Version History

- v0.6 · 2026-09-05 · Buy/sell dropped long-press for price-button single click (section 04 rewritten; `lpThreshold` param and `lp-fill` removed); added section 08 shop page (2026-09-05 mockup layout; all interaction params referenced from the global bar).

- v0.5 · 2026-08-19 · Card-flight / phase-transition motion split out to `PhaseTransitionDemo.html`.
- v0.4 · 2026-08-18 · Section 01 swatches annotated with `GameColorPalette` mappings.
- v0.3 · 2026-08-18 · Effect text hugs the divider and grows upward; no-effect cards omit the row; ease defaults to overshoot; wiggle count defaults to 1.
- v0.2 · 2026-08-18 · Section 03 card template added (art / rarity ✦1–3 / effect / name+attack bottom row); sections 04–05 migrated to it.
- v0.1 · 2026-08-15 · Initial version (tokens / button / long-press card / tooltip / read-only & recessed / state matrix).
