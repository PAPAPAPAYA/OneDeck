---
name: check-shop-pool-ref
description: Verify that the OneDeck ShopPoolRef ScriptableObject contains every card prefab under the 4.0 card folder (including subfolders, excluding the -1_Test folder). Use when the user asks to check, validate, compare, audit, or sync ShopPoolRef against the card prefabs. Triggers include: "check ShopPoolRef", "validate shop pool", "compare ShopPoolRef with prefabs", "are all 4.0 cards in the shop pool", "ShopPoolRef missing cards", or any similar request about ShopPoolRef completeness.
---

# Check ShopPoolRef

Run the bundled script to compare `Assets/SORefs/ShopRefs/ShopPoolRef.asset` against the 4.0 card prefab folder under `Assets/Prefabs/Cards/`.

The expected pool layout (rebuilt 2026-09-04): all 4.0 prefabs, nothing else. IncreaseHpMax and IncreaseDeckSizeLite (hpMax gain / deck-slot meter) were moved into `4.0/0_Common/` on 2026-09-04, so they are covered by the normal 4.0 scan. All other 3.0 cards are intentionally absent — including `IncreaseDeckSize` (still at 3.0 `_UTILITY/`), which stays out of the pool on purpose (dominance vs. the Lite deck-slot card) and outside this scan.

## Usage

From the project root:

```bash
.agents/skills/check-shop-pool-ref/scripts/check-shop-pool.sh
```

Or with an explicit project root:

```bash
.agents/skills/check-shop-pool-ref/scripts/check-shop-pool.sh "d:/Unity Projects/OneDeck"
```

## What it does

1. Locates the 4.0 card folder (the directory under `Assets/Prefabs/Cards` whose name starts with `4.0`).
2. Collects GUIDs from every `.prefab` in that folder and its subfolders, excluding anything under `-1_Test` or `_DONT INCLUDE`.
3. Extracts GUIDs from `ShopPoolRef.asset`'s `deck` list.
4. Reports:
   - Total prefab count (4.0 folder) vs. ShopPoolRef deck count.
   - Prefabs missing from `ShopPoolRef`.
   - `ShopPoolRef` entries that are not a 4.0 prefab (orphaned/removed).
