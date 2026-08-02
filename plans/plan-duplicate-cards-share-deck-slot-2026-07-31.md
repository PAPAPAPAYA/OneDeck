# Duplicate Cards Share Deck Slot — Implementation Plan

Date: 2026-07-31
Status: Implemented (code + tests + scene wiring done; Play Mode visual check pending)
Scope confirmed with user: shop purchase check + shop UI empty-slot display + duplicate stacking display. `CombatStartCardGiver` and the enemy deck are explicitly out of scope.

## 1. Overview

When the feature toggle is ON:

- Cards sharing the same `cardTypeID` occupy **one** deck slot together: the first
	copy takes 1 slot, every further copy is free.
	Example: 3 copies of card A + 1 copy of card B = 2 slots used.
- In the Shop phase player-deck display, copies of the same card are rendered as a
	**stack**: the first copy sits on its grid slot, each further copy is offset
	toward the **upper-left** so the pile reads as one stack.
- When the toggle is OFF, every behavior is byte-identical to today.

## 2. Current State

- Slot counting: `UtilityFuncManagerScript.CountCardsTakingUpSpace(DeckSO)`
	(`Assets/Scripts/Managers/UtilityFuncManagerScript.cs:46`) counts cards with
	`CardScript.takeUpSpace == true`.
- Enforcement points (player deck only):
	- `ShopManager.BuyFunc` (`Assets/Scripts/Managers/ShopManager.cs:175-176`) blocks
		purchase when `actualSize >= deckSize.value`.
	- `CombatStartCardGiver` (`Assets/Scripts/Managers/CombatStartCardGiver.cs:78-83`)
		checks capacity when granting start cards. **Unchanged by this feature.**
- Shop UI: `ShopUXManager` lays out `_spawnedPlayerCards` (real cards + empty-slot
	placeholders) on a grid (`objPerRow`, `xOffset`, `yOffset`, `playerDeckPos`):
	- Initial spawn: `InstantiatePlayerDeckPhysCards` (line 179), placeholders =
		`deckSize - cardCount` (line 267).
	- Buy: `OnCardPurchased` (line 354) removes one placeholder, moves the card into
		its grid slot.
	- Sell: `OnCardSold` (line 461) + `SpawnEmptySpaceAt` (line 533) put a
		placeholder back at the sold card's index.
	- Deck-size increase: `SpawnAdditionalEmptySpaces` (line 572).
- Sell interaction: `ShopCardView.TrySell` -> `GetPlayerCardIndex()` returns the
	first `playerDeckRef.deck` entry matching the represented prefab. Identical
	copies are interchangeable, so this stays correct for stacks without changes.

## 3. Feature Toggle

- New `BoolSO` asset `Assets/SORefs/ShopRefs/DuplicateCopiesShareSlotRef.asset`:
	`value = false` (default OFF), `valueOg = false`, `resetOnStart = false`
	(the Inspector value is the persistent config; sits next to `DeckSize/` refs).
- `ShopManager` new serialized field + null-safe accessor:

```csharp
[Header("duplicate slot rule")]
public BoolSO duplicateCopiesShareSlotRef;
public bool DuplicateCopiesShareSlot =>
	duplicateCopiesShareSlotRef != null && duplicateCopiesShareSlotRef.value;
```

- One switch drives the whole feature: slot rule **and** shop stacking display.
	Rationale: stacking without the slot rule desyncs the grid (hidden copies vs
	placeholder count). If stacking alone is ever wanted, split a plain bool field
	on `ShopUXManager` later.

## 4. Logic Changes

### 4.1 Counting overload (`UtilityFuncManagerScript`)

```csharp
public static int CountCardsTakingUpSpace(DeckSO deck) =>
	CountCardsTakingUpSpace(deck, false);

public static int CountCardsTakingUpSpace(DeckSO deck, bool duplicatesShareSlot)
```

- `duplicatesShareSlot == false`: existing loop, unchanged.
- `duplicatesShareSlot == true`: count each **distinct non-empty `cardTypeID`**
	once among `takeUpSpace` cards. Cards with a null/empty `cardTypeID` are never
	deduplicated — every copy counts (cannot prove identity).
- Existing single-arg callers (`CombatStartCardGiver`) keep legacy behavior for
	free.

### 4.2 Purchase check (`ShopManager.BuyFunc`)

Slot cost of the incoming card:

| Case | Cost |
|------|------|
| `takeUpSpace == false` | 0 (existing path, no check) |
| Toggle ON and deck already holds the same non-empty `cardTypeID` | 0 |
| Otherwise | 1 |

Implementation: compute `actualSize = CountCardsTakingUpSpace(playerDeckRef,
DuplicateCopiesShareSlot)`; for cost-1 cards keep the current
`actualSize >= deckSize.value` block; for cost-0 duplicates skip the check.
Notably this allows buying a duplicate even when all slots are filled with
unique cards — intended.

## 5. Shop UI Changes (`ShopUXManager`)

All gated by `ShopManager.me != null && ShopManager.me.DuplicateCopiesShareSlot`.

### 5.1 New tunables

```csharp
[Header("Duplicate Stacking")]
public Vector3 duplicateStackOffset = new Vector3(-0.12f, 0.12f, -0.02f);
public int duplicateStackMaxOffsetCount = 5;
```

- Offset per copy index: `-x` = left, `+y` = up; z-step keeps each higher copy
	in front so it receives clicks (sign verified in scene).
- Copy offsets clamp at `duplicateStackMaxOffsetCount` so huge stacks do not
	drift off the slot.

### 5.2 Central layout helper

Grid slot = one **unique cardTypeID** (first-appearance order in
`playerDeck.deck`) or one placeholder. Add:

- `Vector3 GetPlayerDeckSlotPosition(int slotIndex)` — existing row/col math
	extracted verbatim.
- `RelayoutPlayerDeckCards()` — walks `_spawnedPlayerCards`; first occurrence of
	a type targets the next grid slot, copy k targets `slotPos +
	duplicateStackOffset * min(k, maxOffsetCount)`, placeholders fill the
	remaining slots (`deckSize - uniqueTypeCount`); everything via
	`SetTargetPosition` so cards glide.

### 5.3 Call-site updates

- `InstantiatePlayerDeckPhysCards`: copies still spawn (each is a real,
	sellable card) but target stack positions; grid `cardCount` increments only
	on first occurrences; placeholders = `deckSize - uniqueTypeCount`.
- `OnCardPurchased`: toggle ON and a stack for that `cardTypeID` already
	exists -> do **not** remove a placeholder; insert the purchased card after
	the last copy of its stack in `_spawnedPlayerCards`, then
	`RelayoutPlayerDeckCards()`. Otherwise: existing behavior.
- `OnCardSold`: toggle ON and the stack still has >= 1 copy after the sale ->
	skip `SpawnEmptySpaceAt`; relayout so remaining copies tighten down.
	Selling the last copy: existing placeholder behavior.
- `SpawnAdditionalEmptySpaces`: card count = unique-type count when toggle ON.
- Price display: `ShopCardView.suppressPriceDisplay` hides the price on stacked
	copies; only the base card of a stack shows it. Set from
	`Assign(..., out isStackedCopy)` at spawn and in `RelayoutPlayerDeckCards`
	(selling the base card promotes a surviving copy, so its price reappears).

### 5.4 Untouched interactions

- Click-to-enlarge, long-press buy/sell, camera scroll: no changes. Overlapped
	copies are hit-tested by Unity colliders; the front-most copy (highest
	offset) gets the click and sells one copy.

## 6. Out of Scope / Unaffected

- `CombatStartCardGiver`: uses the legacy single-arg count — start-card grants
	still count every copy.
- **Enemy deck**: unaffected by construction. No enforcement point exists for
	the enemy deck — it never passes a slot check; `deckSize` only gates player
	purchases, and combat merges the DeckSO as-is.
- Combat-phase visuals, result stats, shop stats, `DeckSaver`: unchanged.

## 7. Edge Cases

- Empty `cardTypeID`: never deduplicated (count + display), each copy gets its
	own grid slot.
- `takeUpSpace == false`: unchanged — never displayed, never counted.
- Deck full of unique cards + buying a duplicate: allowed (cost 0).
- Selling the base (bottom) card of a stack while copies remain: stack stays,
	no placeholder; a surviving copy becomes the new stack bottom visually.
- Toggling the BoolSO mid-shop in Play Mode: call
	`InstantiatePlayerDeckPhysCards()` to rebuild; acceptable for a design toggle.

## 8. Tests

New EditMode test file `Assets/Scripts/Editor/Tests/DuplicateSlotCountTests.cs`
(pure static counting, no scene needed):

- Toggle OFF -> legacy count (regression guard).
- Toggle ON: N copies of one type -> 1; mixed types -> distinct count.
- Empty `cardTypeID` copies -> each counts.
- `takeUpSpace == false` -> excluded in both modes.

Manual Play Mode checklist:

- Toggle ON: buy 2 copies of the same card -> second copy stacks upper-left,
	placeholder count unchanged; deck-full-of-uniques still accepts a duplicate.
- Sell from a 3-stack -> one copy flies out, no placeholder appears; sell the
	last copy -> placeholder returns.
- `DeckSizeIncreaseEffect` purchase -> placeholders appear relative to
	unique-type count.
- Toggle OFF: exact legacy layout and purchase blocking.

## 9. Implementation Order

1. `BoolSO` asset + `ShopManager` field/accessor.
2. Counting overload + `BuyFunc` cost check.
3. EditMode tests for counting.
4. `ShopUXManager` tunables, layout helper, call-site updates.
5. Scene wiring (BoolSO reference) + Play Mode checklist above.
