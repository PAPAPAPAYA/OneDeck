"""Extract card configs from OneDeck 4.0 card prefabs into JSON.

Variant of unity-notion-card-sync/extract_unity_cards.py pointed at the
4.0 pool folder (excluding -1_Test). Writes file name, displayName,
rarity, cardDesc and folder for each card.

Usage:
	python extract_unity_cards_40.py [project_root] [--out PATH]

Defaults: project_root = cwd, --out = tools/outputs/unity_cards_40_current.json
"""
import re
import os
import sys
import json

CARD_ROOT = os.path.join("Assets", "Prefabs", "Cards", "4.0")
EXCLUDE_MARKERS = ("-1_Test",)
RARITY_NAMES = {"0": "common", "1": "uncommon", "2": "rare"}


def grab(text, field):
	m = re.search(r"^  " + field + r": (.*?)(?=^  \w|^--- )", text, re.M | re.S)
	return m.group(1).strip() if m else ""


def dec(s):
	s = s.replace("\n", "")
	if s.startswith('"') and s.endswith('"'):
		s = s[1:-1]
	s = re.sub(r"\\u([0-9A-Fa-f]{4})", lambda m: chr(int(m.group(1), 16)), s)
	return s


def main():
	root = sys.argv[1] if len(sys.argv) > 1 and not sys.argv[1].startswith("--") else "."
	out_path = "tools/outputs/unity_cards_40_current.json"
	if "--out" in sys.argv:
		out_path = sys.argv[sys.argv.index("--out") + 1]

	card_root = os.path.join(root, CARD_ROOT)
	out = []
	for dirpath, dirs, files in os.walk(card_root):
		if any(marker in dirpath for marker in EXCLUDE_MARKERS):
			continue
		for f in sorted(files):
			if not f.endswith(".prefab"):
				continue
			text = open(os.path.join(dirpath, f), encoding="utf-8").read()
			rarity_raw = grab(text, "rarity").split()[0] if grab(text, "rarity") else ""
			out.append({
				# Prefab file name without extension; this is the canonical "file name".
				"file": f.replace(".prefab", ""),
				"cardTypeID": dec(grab(text, "cardTypeID")),
				"displayName": dec(grab(text, "displayName")),
				# Missing field means Unity deserializes rarity to 0 = common.
				"rarity": rarity_raw,
				"rarityName": RARITY_NAMES.get(rarity_raw, "common"),
				"cardDesc": dec(grab(text, "cardDesc")),
				"printedAttack": grab(text, "printedAttack").replace("value: ", "").split()[0]
					if grab(text, "printedAttack") else "",
				"utilityKind": grab(text, "utilityKind").replace(": ", "").strip()
					if grab(text, "utilityKind") else "",
				"folder": os.path.relpath(dirpath, card_root).replace("\\", "/"),
			})

	out_path = os.path.join(root, out_path) if not os.path.isabs(out_path) else out_path
	os.makedirs(os.path.dirname(out_path), exist_ok=True)
	with open(out_path, "w", encoding="utf-8") as fp:
		json.dump(out, fp, ensure_ascii=False, indent=1)
	# Print ASCII-safe summary: Windows consoles often use GBK and mangle CJK.
	print("wrote %d cards to %s" % (len(out), out_path))
	for c in out:
		print("%s | r%s | %s" % (c["file"], c["rarity"] or "0", c["folder"]))


if __name__ == "__main__":
	main()
