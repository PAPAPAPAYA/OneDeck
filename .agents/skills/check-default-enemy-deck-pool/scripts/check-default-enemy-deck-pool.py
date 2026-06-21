#!/usr/bin/env python3
"""
Check (and optionally update) DeckSaver.defaultEnemyDeckPool against recorded decks.

Usage:
    python check-default-enemy-deck-pool.py [project-root]
    python check-default-enemy-deck-pool.py [project-root] --fix

Without --fix: only reports discrepancies.
With --fix:    adds missing recorded decks to the pool (never removes entries).
"""

import argparse
import re
import shutil
import sys
from collections import defaultdict
from pathlib import Path

DECK_SAVER_SCRIPT_GUID = "2c309896258a493caad265bdb907438f"
SCENE_REL_PATH = "Assets/Scenes/GameScene.unity"
RECORDED_ROOT_REL = "Assets/SORefs/Decks/Recorded"


def collect_recorded_decks(project_root: Path):
    """Return dict session_num -> list of {name, guid, path}."""
    recorded_root = project_root / RECORDED_ROOT_REL
    recorded = defaultdict(list)
    if not recorded_root.exists():
        return recorded

    for session_dir in sorted(recorded_root.glob("Session*")):
        if not session_dir.is_dir():
            continue
        m = re.match(r"Session(\d+)", session_dir.name)
        if not m:
            continue
        session_num = int(m.group(1))
        for asset in sorted(session_dir.glob("*.asset")):
            meta = asset.with_suffix(".asset.meta")
            guid = None
            if meta.exists():
                for line in meta.read_text(encoding="utf-8").splitlines():
                    if line.startswith("guid:"):
                        guid = line.split(":", 1)[1].strip()
                        break
            recorded[session_num].append({
                "name": asset.stem,
                "guid": guid,
                "path": str(asset.relative_to(project_root)).replace("\\", "/"),
            })
    return recorded


def build_guid_to_deck_map(project_root: Path):
    """Map every DeckSO asset GUID under Assets/SORefs/Decks to its name/path."""
    guid_to_deck = {}
    decks_root = project_root / "Assets/SORefs/Decks"
    if not decks_root.exists():
        return guid_to_deck

    for asset in decks_root.rglob("*.asset"):
        meta = asset.with_suffix(".asset.meta")
        if not meta.exists():
            continue
        guid = None
        for line in meta.read_text(encoding="utf-8").splitlines():
            if line.startswith("guid:"):
                guid = line.split(":", 1)[1].strip()
                break
        if guid:
            guid_to_deck[guid] = {
                "name": asset.stem,
                "path": str(asset.relative_to(project_root)).replace("\\", "/"),
            }
    return guid_to_deck


def parse_pool_from_scene(scene_text: str):
    """
    Return list of {guids: [...]} for defaultEnemyDeckPool entries,
    plus the index in scene_text where the pool section starts.
    """
    # Locate DeckSaver MonoBehaviour by script GUID
    saver_marker = f"m_Script: {{fileID: 11500000, guid: {DECK_SAVER_SCRIPT_GUID}, type: 3}}"
    saver_start = scene_text.find(saver_marker)
    if saver_start == -1:
        raise RuntimeError(f"DeckSaver component (script {DECK_SAVER_SCRIPT_GUID}) not found in scene.")

    pool_start = scene_text.find("defaultEnemyDeckPool:", saver_start)
    if pool_start == -1:
        raise RuntimeError("defaultEnemyDeckPool field not found in DeckSaver component.")

    lines = scene_text[pool_start:].splitlines()
    pool_entries = []
    current_entry = None
    end_offset = 0

    for i, line in enumerate(lines):
        if i == 0:
            continue
        # Stop when we leave the pool block: a top-level field (no leading spaces or new section)
        if line and not line.startswith("  "):
            break
        stripped = line.lstrip()
        if stripped.startswith("- decks:"):
            if current_entry is not None:
                pool_entries.append(current_entry)
            current_entry = {"guids": []}
        elif stripped.startswith("- {fileID:"):
            m = re.search(r"guid:\s*([a-f0-9]+)", stripped)
            if m:
                current_entry["guids"].append(m.group(1))
        end_offset += len(line) + 1  # +1 for the newline consumed by splitlines

    if current_entry is not None:
        pool_entries.append(current_entry)

    pool_end = pool_start + end_offset
    return pool_entries, pool_start, pool_end


def compare(recorded, pool_entries, guid_to_deck):
    """Print comparison table and return (all_ok, missing_per_session)."""
    max_session = max(list(recorded.keys()) + [len(pool_entries) - 1])
    all_ok = True
    missing_per_session = defaultdict(list)

    print("=== Default Enemy Deck Pool Check ===")
    print("(Only checks that every deck in Recorded is present in the pool.)")
    print("(Extra pool entries are reported for info but are not treated as errors.)\n")
    for s in range(max_session + 1):
        recorded_guids = {d["guid"] for d in recorded.get(s, []) if d["guid"]}
        pool_guids = set(pool_entries[s]["guids"]) if s < len(pool_entries) else set()
        missing = recorded_guids - pool_guids
        extra = pool_guids - recorded_guids

        print(f"Session {s}: recorded={len(recorded_guids)} pool={len(pool_guids)}", end="")
        if missing:
            all_ok = False
            missing_per_session[s] = [next(d for d in recorded[s] if d["guid"] == g) for g in missing]
            print(f" MISSING={len(missing)}")
            for d in missing_per_session[s]:
                print(f"    - {d['guid']} -> {d['name']}")
        elif extra:
            print(f" EXTRA={len(extra)}")
            for g in extra:
                info = guid_to_deck.get(g, {"name": "UNKNOWN", "path": ""})
                print(f"    - {g} -> {info['name']}")
        else:
            print(" OK")

    print()
    return all_ok, missing_per_session


def update_scene(scene_path: Path, recorded, pool_entries, missing_per_session):
    """Add missing recorded decks to the scene file. Returns number of additions."""
    scene_text = scene_path.read_text(encoding="utf-8")
    original_text = scene_text
    total_added = 0

    # Process sessions in reverse order so earlier offsets are not invalidated by later inserts.
    for s in sorted(missing_per_session.keys(), reverse=True):
        missing_decks = missing_per_session[s]
        if s >= len(pool_entries):
            print(f"Warning: Session {s} has no pool entry; cannot add (pool size={len(pool_entries)}).", file=sys.stderr)
            continue

        # Locate the position right after the last deck GUID of this session.
        # We re-parse the current scene_text each iteration to keep offsets correct.
        pool_entries_current, pool_start, _ = parse_pool_from_scene(scene_text)

        # Find the line offset in the scene text for the last GUID of session s.
        lines = scene_text[pool_start:].splitlines()
        session_index = -1
        last_guid_line_local = -1
        last_guid_match_end_local = -1
        for i, line in enumerate(lines):
            if i == 0:
                continue
            if not (line.startswith("  - decks:") or line.startswith("    - {fileID:")):
                break
            stripped = line.lstrip()
            if stripped.startswith("- decks:"):
                session_index += 1
                continue
            if session_index == s and stripped.startswith("- {fileID:"):
                last_guid_line_local = i
                last_guid_match_end_local = len(line)

        if last_guid_line_local == -1:
            print(f"Warning: Could not locate insertion point for session {s}.", file=sys.stderr)
            continue

        # Build the insertion text (preserve 4-space indent used by the scene).
        new_lines = []
        for d in missing_decks:
            new_lines.append(f"    - {{fileID: 11400000, guid: {d['guid']}, type: 2}}")
        insert_text = "\n".join(new_lines) + "\n"

        # Compute absolute position in scene_text.
        prefix_lines = lines[:last_guid_line_local + 1]
        prefix_length = sum(len(l) + 1 for l in prefix_lines)
        insert_pos = pool_start + prefix_length

        scene_text = scene_text[:insert_pos] + insert_text + scene_text[insert_pos:]
        total_added += len(missing_decks)

    if total_added > 0:
        backup_path = scene_path.with_suffix(".unity.bak")
        shutil.copy2(scene_path, backup_path)
        scene_path.write_text(scene_text, encoding="utf-8")
        print(f"Updated {scene_path} (backup: {backup_path}). Added {total_added} entries.")
    else:
        print("No entries to add.")

    return total_added


def main():
    parser = argparse.ArgumentParser(
        description="Check/update DeckSaver.defaultEnemyDeckPool against Assets/SORefs/Decks/Recorded."
    )
    parser.add_argument("project_root", nargs="?", default=".", help="Project root directory")
    parser.add_argument("--fix", action="store_true", help="Add missing recorded decks to the pool")
    args = parser.parse_args()

    project_root = Path(args.project_root).resolve()
    scene_path = project_root / SCENE_REL_PATH

    if not scene_path.exists():
        print(f"Error: Scene not found at {scene_path}", file=sys.stderr)
        sys.exit(1)

    recorded = collect_recorded_decks(project_root)
    guid_to_deck = build_guid_to_deck_map(project_root)
    scene_text = scene_path.read_text(encoding="utf-8")
    pool_entries, _, _ = parse_pool_from_scene(scene_text)

    all_ok, missing_per_session = compare(recorded, pool_entries, guid_to_deck)

    if args.fix:
        print("=== Applying fixes ===\n")
        added = update_scene(scene_path, recorded, pool_entries, missing_per_session)
        if added > 0:
            # Re-verify
            scene_text = scene_path.read_text(encoding="utf-8")
            pool_entries, _, _ = parse_pool_from_scene(scene_text)
            print("\n=== Re-verification ===")
            compare(recorded, pool_entries, guid_to_deck)
    elif not all_ok:
        print("Run with --fix to add the missing recorded decks.")
        sys.exit(1)
    else:
        print("All recorded decks are present in the pool.")


if __name__ == "__main__":
    main()
