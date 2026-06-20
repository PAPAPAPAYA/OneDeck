---
name: check-shop-pool-ref
description: Verify that the OneDeck ShopPoolRef ScriptableObject contains every card prefab under the 3.0 card folder (including subfolders), while excluding the _DONT INCLUDE folder and its contents. Use when the user asks to check, validate, compare, audit, or sync ShopPoolRef against the 3.0 card prefabs. Triggers include: "check ShopPoolRef", "validate shop pool", "compare ShopPoolRef with prefabs", "are all 3.0 cards in the shop pool", "ShopPoolRef missing cards", or any similar request about ShopPoolRef completeness.
---

# Check ShopPoolRef

Run the bundled script to compare `Assets/SORefs/ShopRefs/ShopPoolRef.asset` against the 3.0 card prefab folder under `Assets/Prefabs/Cards/`.

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

1. Locates the 3.0 card folder (the directory under `Assets/Prefabs/Cards` whose name starts with `3.0`).
2. Collects GUIDs from every `.prefab` in that folder and its subfolders, excluding anything under `_DONT INCLUDE`.
3. Extracts GUIDs from `ShopPoolRef.asset`'s `deck` list.
4. Reports:
   - Total prefab count vs. ShopPoolRef deck count.
   - Prefabs missing from `ShopPoolRef`.
   - `ShopPoolRef` entries that no longer match an existing 3.0 prefab.
