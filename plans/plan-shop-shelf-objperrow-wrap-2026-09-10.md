# Plan: Shop Shelf Dynamic objPerRow Wrap (初魇 extra card layout fix)

Date: 2026-09-10
Status: IMPLEMENTED 2026-09-10 (ShopUXManager.cs) — Play Mode visual verification pending
Trigger bug: 初魇 (UTILITY_SLOT_U_1, utilityKind=RaritySlotU, firstBoardOnly) appends a guaranteed ✦✦ card to the board; the 4th shelf card is laid out at x=+6.4 (single-row formula), off the right screen edge (visible ≈ ±6.2).

## Root Cause

- Data layer is BY DESIGN: `ShopBoardPipeline.GenerateBoard` appends reserved guarantee slots after the generic slots ("never displace generic slots", 2026-09-02 ruling). Board = shopItemAmount(3) + reserved(1).
- Presentation layer bug: `ShopUXManager.GetShopItemSlotPosition` (ShopUXManager.cs:248-251) is a single horizontal row, no wrap:
  `shopItemPos + ((shopIndex - 1) * xOffset, 0, 0)`
- `objPerRow` (=3) only wraps the player-deck grid (`GetPlayerDeckSlotPosition`, :240-242). The shelf never wrapped; the flaw was invisible while shelf count ≤ 3.

## Chosen Fix: 方案 B, dynamic row count (user decision 2026-09-10)

Shelf rows are dynamic by construction: count ≤ objPerRow stays a single row (layout identical to today), count > objPerRow pushes the overflow onto row 2. The deck band shifts down ONLY while the shelf actually has a second row, so the normal 3-card shop has zero regression and there is no permanent empty band.

### 1. Shelf wrap formula (ShopUXManager.cs:248)

Mirror the deck grid math:

```csharp
private Vector3 GetShopItemSlotPosition(int shopIndex)
{
	int row = shopIndex / objPerRow;
	int col = shopIndex % objPerRow;
	return shopItemPos + new Vector3((col - 1) * xOffset, -row * yOffset, 0f);
}
```

Shelf row 2 lands at shopItemPos.y - yOffset = 2.2 - 4.5 = **-2.3**. All shelf positioning funnels through this single function: spawn (:124), RelayoutAll (:324), hover restore (ShopCardView snapshots TargetPosition at enlarge time) — no other call sites.

### 2. Dynamic deck band (NEW: replaces the fixed scene move of the v1 plan)

New pure helper (no serialized field changes, **no scene edits at all**):

```csharp
/// <summary>
/// Deck band origin. Single-row shelf: playerDeckPos (today's layout). A wrapped shelf
/// shifts the whole deck band down by one pitch per extra shelf row, keeping a constant
/// gap between the lowest shelf row and the top deck row.
/// </summary>
private Vector3 CurrentDeckPos()
{
	int perRow = Mathf.Max(1, objPerRow);
	int shelfRows = Mathf.CeilToInt((float)_spawnedShopCards.Count / perRow);
	return playerDeckPos + new Vector3(0f, -Mathf.Max(0, shelfRows - 1) * yOffset, 0f);
}
```

Consumers to switch from `playerDeckPos` to `CurrentDeckPos()` (all found by grep, total 3):

| Line | Use | Change |
|---|---|---|
| :242 `GetPlayerDeckSlotPosition` | deck cards + empty slots + spawn slots | `playerDeckPos + ...` → `CurrentDeckPos() + ...` |
| :541 `ComputeDynamicMinY` | scroll lower bound | `playerDeckPos.y` → `CurrentDeckPos().y` |
| :445 `InstantiatePlayerDeckPhysCards` | spawn-origin fallback | `playerDeckPos` → `CurrentDeckPos()` (cosmetic; startPos is set in scene) |

Resulting geometry (card ≈ 3.3 world tall, pitch 4.5):
- shelf ≤ 3 cards: deck band at y = -2.5 — identical to today.
- shelf 4-6 cards: shelf row 2 at -2.3, deck band at -7.0, gap ≈ 1.4.
- Formula generalizes: N shelf rows → band offset (N-1) * yOffset (row 3 would need 7+ cards; current pool max = 5).

### 3. Band re-layout triggers

New helper consolidating the deck-band part of `RelayoutAll` (empty slots SetPositionImmediate + deck cards tween):

```csharp
private void RelayoutDeckBand()
{
	for (int i = 0; i < _spawnedEmptySlots.Count; i++)
	{
		GameObject slot = _spawnedEmptySlots[i];
		if (slot == null) continue;
		Vector3 slotPosition = GetPlayerDeckSlotPosition(i) + new Vector3(0f, 0f, emptySlotZOffset);
		var physObj = slot.GetComponent<CardPhysObjScript>();
		if (physObj != null) physObj.SetPositionImmediate(slotPosition);
		else slot.transform.position = slotPosition;
	}
	RelayoutPlayerDeckCards();
}
```

- `RelayoutAll` (:315) refactors to: shelf loop + `RelayoutDeckBand()` (no behavior change for live-tuning).
- `RemoveFromShopCards` (:380-391): call `RelayoutDeckBand()` at the end, after re-indexing. Covers all three purchase paths (take-space, stack, no-space) — a purchase that crosses the 4→3 threshold slides the band back up. Existing `RelayoutPlayerDeckCards` calls at :634/:651 become redundant-but-harmless (same targets, tween is idempotent); leave them.
- `InstantiateShopPhysCards` (:87-162): call `RelayoutDeckBand()` after the spawn loop (before/after LogSpawnedShopCardIds). Do NOT reuse `RelayoutAll` here — its shelf loop uses SetPositionImmediate and would kill the spawn entry animation.

Order is safe: on shop enter the shelf list is rebuilt first, so deck cards tween to the new band; on purchase the shelf list is decremented before the band recompute, so all deck cards (including the just-bought one when added afterwards) tween to the same final band.

### 4. Camera / UI: untouched

- Scroll bounds are relative: `ComputeDynamicMinY` now reads `CurrentDeckPos().y`, so the lower bound follows the band automatically; `cameraMaxY` is relative to initial Y, unchanged. Rest view (rig Y=0, ≈ ±6.2) shows both shelf rows and deck row 0 top in the wrapped state.
- Screen-space UI (Your Deck: / Shop / Player Stats labels, Reroll/Exit buttons) is screen-anchored, unaffected.

### 5. VISUAL-FIX block + regression row

- `VISUAL-FIX(2026-09-10):` block in ShopUXManager.cs per docs/VisualBugPrevention_Guide.md (cause = shelf formula had no wrap; regress = enter shop with 初魇 owned → 3+1 shelf with deck band lowered, reroll → band back up, buy a shelf card → reflow).
- Append one row to docs/RegressionChecklist.md.

## Verification

1. No 初魇 owned: shop layout pixel-identical to today (shelf single row, deck at -2.5).
2. 初魇 in deck, board 0: shelf = 3+1; row-2 card at (-3.2, -2.3) fully on screen; deck band tweens to -7.0, empty slots follow.
3. Reroll: shelf = 3 → deck band tweens back to -2.5 (初魇 is firstBoardOnly).
4. Buy the row-2 card → shelf reflows (no holes), band slides up.
5. Buy a no-space utility from a 4-card shelf → band still slides up.
6. Wheel-scroll lower bound tracks the current band; 16-slot deck fully reachable.
7. Hover-enlarge/restore correct (restore uses snapshot; RelayoutAll skips enlarged cards).
8. Live Inspector tuning (xOffset/yOffset/playerDeckPos) still live-updates via RelayoutAll.

## Transition feel (recorded)

Band slides are tweened for deck cards but snap for empty slots (same as the existing RelayoutAll empty-slot path). Slides happen only at shop enter with 初魇, reroll, and threshold-crossing purchases. If the snap reads badly in play, optional polish: tween empty slots too (SetTargetPosition + stagger). Not in scope.

## Capacity bound (recorded)

Current pool max shelf = 3 generic + 1 reserved (初魇) + 1 extraShopOptions (魇市新摊) = 5 → 2 rows (6 slots). The generalized band formula already handles a hypothetical 3rd shelf row; a 7th card would still overlap the deck band under today's viewport, so if a second ShopOption utility ever ships, revisit 3-row banding and view sizing.

## Implementation Log (2026-09-10)

Implemented in `Assets/Scripts/UXPrototype/ShopUXManager.cs` per this plan (52 insertions, 4 deletions):
wrap formula, `CurrentDeckPos`, `RelayoutDeckBand`, `RelayoutAll` refactor, band re-layout in `RemoveFromShopCards`, `InstantiateShopPhysCards` and — discovered during implementation — `SpawnShopCardsInternal` (the reroll respawn path via `SpawnNewShopCardsAfterDelay`; the plan listed only the entry path, the reroll path would have kept the band lowered otherwise).

No scene edits. Roslyn validate_script: 0 errors (1 pre-existing Update() string-concat warning). `docs/RegressionChecklist.md` row 91 appended. Play Mode visual verification per the Verification section is pending (not run; AGENTS.md Play Mode rule).
