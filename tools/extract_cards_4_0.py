# -*- coding: utf-8 -*-
"""Extract 4.0 card prefab configs from the root CardScript component block."""
import re, os, json

ROOT = os.path.join("Assets", "Prefabs", "Cards", "4.0")
RARITY = {"0": "normal", "1": "uncommon", "2": "rare"}

def dec(s):
    s = s.replace("\n", "")
    if s.startswith('"') and s.endswith('"'):
        s = s[1:-1]
    s = re.sub(r"\\u([0-9A-Fa-f]{4})", lambda m: chr(int(m.group(1), 16)), s)
    return s

def field_of(block, name):
    m = re.search(r"^  " + name + r": (.*?)(?=^  \w|^--- )", block, re.M | re.S)
    return m.group(1).strip() if m else ""

out = []
for dirpath, dirs, files in os.walk(ROOT):
    for f in sorted(files):
        if not f.endswith(".prefab"):
            continue
        text = open(os.path.join(dirpath, f), encoding="utf-8").read()
        # Split into component blocks; the root CardScript block contains "rarity:"
        blocks = re.split(r"^--- !u!114 &", text, flags=re.M)
        root = None
        for b in blocks:
            if re.search(r"^  rarity:", b, re.M):
                root = b
                break
        if root is None:
            root = text
        rarity_raw = field_of(root, "rarity").split()[0] if field_of(root, "rarity") else ""
        atk_raw = field_of(root, "printedAttack").split()[0] if field_of(root, "printedAttack") else ""
        out.append({
            "file": f.replace(".prefab", ""),
            "cardTypeID": field_of(root, "cardTypeID"),
            "displayName": dec(field_of(root, "displayName")),
            "rarity": RARITY.get(rarity_raw, "normal"),
            "rarityRaw": rarity_raw,
            "printedAttack": int(atk_raw) if atk_raw.isdigit() else 0,
            "cardDesc": dec(field_of(root, "cardDesc")),
            "folder": os.path.relpath(dirpath, ROOT).replace("\\", "/"),
        })

with open("tools/outputs/unity_cards_4_0.json", "w", encoding="utf-8") as fp:
    json.dump(out, fp, ensure_ascii=False, indent=1)
print("wrote %d cards" % len(out))
