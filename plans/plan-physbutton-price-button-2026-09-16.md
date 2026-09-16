# PhysButton + Price-Button Buy/Sell Port (UIKitDemo §02/§04 → Unity)

2026-09-16 · status: **implemented** (offline compile 0 errors; play-mode verification pending)

Phase 1 of the UI-kit interaction port: the `.phys` state machine from `docs/demo/UIKitDemo.html` §02 as a reusable component, and §04's price-button buy/sell replacing the last long-press idiom. Phase 2 (consequence flight motion) needs no new work — buy/sell/reroll flight animations already existed (`ShopUXManager.OnCardPurchased` / `OnCardSold` / `OnReroll`).

## User decisions (2026-09-16)

- Card template §03 closed; play-mode hover look was user-verified earlier.
- Tooltip placement and `tipDelay`: **engine wins** (right-side default / flip left; 0.1 s). Synced into `docs/UIUX_Guidelines.md` §2.1/§3.4.
- Card body interaction: hover preview in combat; shop keeps click-to-enlarge, card body never transacts.
- "修改代码" authorized for phase 1; phase 2 confirmed as already-implemented on the Unity side.

## What changed

### `Assets/Scripts/UXPrototype/PhysButton.cs` (new)

One component, two render modes:

- **World** (price button): SpriteRenderer face on the object + a child shadow transform (`SetShadowTransform`) + BoxCollider2D + `OnMouse*` input. Release inside invokes `SetWorldAction` (R7); the release is read via a deterministic `Input.GetMouseButtonUp` poll because `OnMouseUp` alone cannot distinguish inside-release from drag-out release (R6). Face/shadow/collider sizing: `ConfigureWorldFaceSize(size, center)`.
- **UI** (reroll/exit): coexists with the uGUI `Button` — `PhysButton` adds only the visuals via EventSystem interfaces; the Button keeps activation (uGUI fires on release-over, which is R7). Shadow is a runtime-created sibling `Image` (sprite/color copied, `raycastTarget` off, sibling index behind the button), so the scene file is untouched. Bootstrap: `ShopUXManager.AttachPhysButtonsToShopCanvasButtons()` in `Start` (component scan of "Shop Canvas", idempotent).

State machine: rest → hover (face `(-hl,+hl)` toward the top-left light, `Ease.OutBack` 120 ms) → press (face lands on the shadow `(+rs,-rs)`, `Ease.OutQuad` 60 ms) → release. Re-enter while held re-presses; exit while held stays pressed until the release poll cancels. Shadow position = its base `+ (rs,-rs) − faceOffset`, so it never leaves the ground (R4). Disabled: dim face (`GameColorPalette.CardFaceDimColor`), shadow hidden (R2), refusal animation once per pointer enter (§2.2): slide out `(-deny,+deny)` 80 ms → `denyWiggles` swing pairs at `denyShift×0.4×0.55^(k−1)` amplitude, 110 ms each, linear → recenter → hold 150 ms → retreat 120 ms. Hover text swap (`$4` → `买入`): instant, only while hovered and enabled; the rest text stays owned by the caller.

Params are face-local units; caller converts design px per context (card-local = 3.2 units / 118 demo px ⇒ rs/hl 0.1085, deny 0.1628; UI canvas = 1 px ⇒ 4/4/6).

### `Assets/Scripts/UXPrototype/ShopCardView.cs`

- Long-press buy/sell removed (`HandleHoldToBuy`, hold timers, press-shake feedback). Short click keeps enlarging the preview (`_pressActive` gates `EnlargeCard`; `HandleClickToRestore` consumes the click so restore doesn't re-enlarge).
- `UpdatePriceDisplay` now also drives the price button: lazily built around the existing `CardPrice` print (print becomes the label, its `PriceStrikeLine` child follows), sliced face sprite copied from the card face, hard shadow from `GameColorPalette.CardShadowColor`, face lit with `OwnerCardColor`. Face/collider sized from the label's `textBounds × 0.2` + padding, rebuilt only when the text changes.
- Buy/sell action and hover label flip per frame with `shopItemIndex` (shelf card = buy `买入`; deck card = half-price sell `售出`), via cached delegates (no per-frame alloc).
- Unaffordable shop item: `PhysButton.SetDisabled(true)` + `CardPhysObjScript.SetFaceDimmed(true)`; both reset whenever the price stops showing (dim can never leak into combat).

### `Assets/Scripts/UXPrototype/CardPhysObjScript.cs`

- `SetFaceDimmed(bool)` + `_faceDimmed`: applied inside `ApplyColor` (which rewrites colors every frame, so an external tint would be clobbered): face → `CardFaceDimColor`, all prints → `CardTextSoftColor`, divider follows.

### `Assets/Scripts/UXPrototype/ShopUXManager.cs`

- `using UnityEngine.UI;` + `AttachPhysButtonsToShopCanvasButtons()` called first in `Start`.

### `docs/UIUX_Guidelines.md`

- v0.9: §2.1 `tipDelay` resolved (engine 0.1 s wins); §3.3 port note + card-body decision; §3.4 tooltip placement frozen as engine-side right-default.

## Verification (offline compile only)

- `dotnet build` Assembly-CSharp and Assembly-CSharp-Editor: 0 errors / 0 new warnings.

### Fix 2026-09-16 (first play screenshot)

The face SpriteRenderer was on the button root, and `ConfigureWorldFaceSize` repositions the face onto the label's text-bounds center — with face == root that teleported the whole button (label included) onto the card's upper area, leaving stray `$4` labels on the card faces and a detached pill at the screen edge. The label's bounds center itself was correct (`CardPrice` has a 15×10 rect, TopLeft aligned, so the text sits at its top-left corner; center = `(-6.49, 3.95)` local × 0.2). Fix: face moved to a dedicated `Face` child (`PhysButton.SetWorldFace`), root keeps only the collider + component and is never touched by sizing; label stays at the root origin.

### Fix 2026-09-16b (hover edge flicker)

The hit target moved with the face (world: collider on the root; UI: raycast on the button rect), so a cursor at the edge oscillated enter/exit as the lift carried the collider away and the rest position brought it back. Structure changed so **the hit target never moves, only the visuals**:

- World: root stays put; its `BoxCollider2D` is sized once to the envelope — the union of the face at rest/hover/press, the grounded shadow, and the deny excursion (`size + (rs+denyShift)` per axis, offset shifted toward the press/shadow side). A `Visual` group child (face + label) is the only thing `ApplyOffset` moves; the shadow is a grounded root child that never moves (press = face lands on it, reading as the demo's shadow collapse). This also fixed press-at-edge clicks being eaten (face moving out from under a held cursor read as drag-out cancel).
- UI: a static transparent envelope-sized hitbox `Image` sibling does all hit-testing via `PhysButtonPointerRelay` (same file); the button graphic and its TMP labels stop raycasting (a raycastable label would bubble events to the Button and double-fire). Release over the hitbox invokes the Button's own `onClick` (gated by `interactable`); releases bubbling from the button's own graphics are left to the Button natively.

## Play-mode checklist (pending)

1. Price button: hover lifts toward top-left with shadow staying grounded; press lands on the shadow; release inside buys/sells; drag-out releases don't transact.
2. Hover text swap `$4` ↔ `买入` / `$2` ↔ `售出` restores the price (incl. struck-through discount) instantly on leave.
3. Unaffordable: card face + button dim, hover plays the head-shake once per enter; buying enough money re-enables live.
4. Reroll / exit uGUI buttons: lift/press feel + disabled denial (reroll is briefly disabled mid-roll); their onClick behavior unchanged.
5. Buy → card flies into deck slot and shows the sell-side price button; sell → flies out and shrinks. Deck-full click is a silent no-op (parity with old long-press, not disabled — deliberate).
6. Stacked duplicate copies: only the stack base shows a button (follows `suppressPriceDisplay`).

## Known scope notes

- `CardPhysObjScript.holdTimeRequired` field kept (unused) to avoid prefab serialized diffs.
- Deny rotation mode (demo `denyRotAmp`) not ported — horizontal translate is the demo default.
- Deck-full does not disable the price button; `BuyFunc` still validates and no-ops.
- New file added to `Assembly-CSharp.csproj` manually for offline builds; Unity regenerates the csproj (and the `.meta`) on next focus/refresh.
