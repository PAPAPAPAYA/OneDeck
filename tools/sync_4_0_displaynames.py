# -*- coding: utf-8 -*-
"""Batch sync displayName (DB v2 Chinese names) -> 4.0 prefab root CardScript blocks.

Source: tools/outputs/worldview_v2_names.json (pushed 2026-08-31, 92 rows).
Cards without a mapping (e.g. BURY test card) are skipped.
"""
import re, os, json

ROOT = os.path.join("Assets", "Prefabs", "Cards", "4.0")
NAMES = json.load(open("tools/outputs/worldview_v2_names.json", encoding="utf-8"))

def encode(s):
    return s.encode("unicode_escape").decode("ascii").replace("\\\\", "\\")

changed, skipped = [], []
for dirpath, dirs, files in os.walk(ROOT):
    for f in sorted(files):
        if not f.endswith(".prefab"):
            continue
        path = os.path.join(dirpath, f)
        text = open(path, encoding="utf-8").read()
        blocks = re.split(r"^--- !u!114 &", text, flags=re.M)
        root_idx = None
        for i, b in enumerate(blocks[1:], 1):
            if re.search(r"^  rarity:", b, re.M):
                root_idx = i
                break
        if root_idx is None:
            skipped.append((f, "no CardScript block"))
            continue
        block = blocks[root_idx]
        m = re.search(r"^  cardTypeID: (\S+)", block, re.M)
        cid = m.group(1) if m else ""
        name = NAMES.get(cid)
        if name is None:
            skipped.append((f, "no name mapping"))
            continue
        new_block = re.sub(
            r"^  displayName: .*?(?=^  \w|^--- )",
            lambda m: "  displayName: \"%s\"\n" % encode(name),
            block, count=1, flags=re.M | re.S)
        if new_block == block:  # displayName field missing -> insert after cardTypeID
            new_block = re.sub(
                r"^(  cardTypeID: \S+\n)",
                lambda m: m.group(1) + "  displayName: \"%s\"\n" % encode(name),
                block, count=1, flags=re.M)
        blocks[root_idx] = new_block
        open(path, "w", encoding="utf-8", newline="\n").write("--- !u!114 &".join(blocks))
        changed.append((cid, name))

print("SYNCED %d cards" % len(changed))
for cid, name in changed:
    print("  %-24s -> %s" % (cid, name))
if skipped:
    print("SKIPPED:")
    for f, why in skipped:
        print("  %s (%s)" % (f, why))
