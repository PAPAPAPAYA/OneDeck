---
name: check-default-enemy-deck-pool
description: Verify that DeckSaver.defaultEnemyDeckPool contains every DeckSO under Assets/SORefs/Decks/Recorded, grouped by session folder, and optionally add missing decks. Use when the user asks to check, validate, compare, audit, sync, or update the default enemy deck pool against recorded decks. Triggers include "check DefaultEnemyDeckPool", "validate enemy deck pool", "sync recorded decks to pool", "update defaultEnemyDeckPool", or similar requests.
---

# Check Default Enemy Deck Pool

Run the bundled script to compare `DeckSaver.defaultEnemyDeckPool` (serialized in `Assets/Scenes/GameScene.unity`) against the recorded decks under `Assets/SORefs/Decks/Recorded/`.

## Usage

From the project root:

```bash
python3 .agents/skills/check-default-enemy-deck-pool/scripts/check-default-enemy-deck-pool.py
```

With an explicit project root:

```bash
python3 .agents/skills/check-default-enemy-deck-pool/scripts/check-default-enemy-deck-pool.py "d:/Unity Projects/OneDeck"
```

To add missing recorded decks to the pool:

```bash
python3 .agents/skills/check-default-enemy-deck-pool/scripts/check-default-enemy-deck-pool.py . --fix
```

## What it does

1. Scans `Assets/SORefs/Decks/Recorded/Session*` for `.asset` files.
2. Extracts each recorded deck's GUID from its `.asset.meta` file and groups decks by session number parsed from the folder name.
3. Parses the `defaultEnemyDeckPool` list inside the `DeckSaver` component in `Assets/Scenes/GameScene.unity`.
4. Reports, per session:
   - Number of recorded decks vs. pool entries.
   - Recorded decks missing from the pool.
   - Pool entries with no matching recorded deck (informational only, not treated as an error).
5. With `--fix`: inserts the missing recorded deck GUIDs into the correct session pool. A `.bak` backup of the scene is created before writing.

## Notes

- The script only **adds** entries; it never removes existing pool entries.
- Session folders must be named `Session0`, `Session1`, etc.
- Pool index 0 corresponds to session 0, index 1 to session 1, and so on.
- The script relies on the DeckSaver component's script GUID (`2c309896258a493caad265bdb907438f`) to locate the correct `MonoBehaviour` in the scene.
