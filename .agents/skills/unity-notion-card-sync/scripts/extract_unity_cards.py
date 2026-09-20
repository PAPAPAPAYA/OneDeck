"""Extract card configs from OneDeck 4.0 card prefabs into JSON.

Reads every .prefab under Assets/Prefabs/Cards/4.0 (excluding -1_Test),
and dumps file name, cardTypeID, displayName, rarity, cardDesc,
printedAttack, cardType and folder for each card.

Usage:
	python extract_unity_cards.py [project_root] [--out PATH]

Defaults: project_root = cwd, --out = tools/outputs/unity_cards_current.json
"""
import re
import os
import sys
import json

CARD_ROOT = os.path.join("Assets", "Prefabs", "Cards", "4.0")
EXCLUDE_MARKERS = ("-1_Test",)
RARITY_NAMES = {"0": "normal", "1": "uncommon", "2": "rare"}


def grab(text, field):
	# cardDesc etc. are multi-line YAML scalars: capture to the next 2-space
	# field boundary. CardScript serializes last, so the last match wins over
	# any earlier component that reuses the same field name.
	matches = re.findall(r"^  " + field + r": (.*?)(?=^  \w|^--- )", text, re.M | re.S)
	return matches[-1].strip() if matches else ""


def grab_last_literal(text, field):
	# Some prefabs carry an earlier shop-view component whose cardTypeID is a
	# StringSO reference ({fileID: ..., guid: ...}); only the literal form
	# matches this pattern, and CardScript's value comes last.
	vals = re.findall(r"^  " + field + r": ([A-Za-z0-9_]+)\s*$", text, re.M)
	return vals[-1] if vals else ""


def dec(s):
	s = s.replace("\n", "")
	if s.startswith('"') and s.endswith('"'):
		s = s[1:-1]
	s = re.sub(r"\\u([0-9A-Fa-f]{4})", lambda m: chr(int(m.group(1), 16)), s)
	s = re.sub(r"\\x([0-9A-Fa-f]{2})", lambda m: chr(int(m.group(1), 16)), s)
	return s


def main():
	root = sys.argv[1] if len(sys.argv) > 1 and not sys.argv[1].startswith("--") else "."
	out_path = "tools/outputs/unity_cards_current.json"
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
				# Prefab file name without extension.
				"file": f.replace(".prefab", ""),
				# Falls back to the file name only if no literal cardTypeID exists.
				"cardTypeID": grab_last_literal(text, "cardTypeID") or f.replace(".prefab", ""),
				"displayName": dec(grab(text, "displayName")),
				# Missing field means Unity deserializes rarity to 0 = normal.
				"rarity": rarity_raw,
				"rarityName": RARITY_NAMES.get(rarity_raw, "normal"),
				"cardDesc": dec(grab(text, "cardDesc")),
				"printedAttack": grab(text, "printedAttack"),
				"cardType": grab(text, "cardType"),
				"folder": os.path.relpath(dirpath, card_root).replace("\\", "/"),
			})

	out_path = os.path.join(root, out_path) if not os.path.isabs(out_path) else out_path
	os.makedirs(os.path.dirname(out_path), exist_ok=True)
	with open(out_path, "w", encoding="utf-8") as fp:
		json.dump(out, fp, ensure_ascii=False, indent=1)
	# Dated-artifact convention (2026-09-20, tools/scripts/dated_output.py):
	# the _current alias feeds consumers; the dated twin is the uploadable artifact.
	sys.path.insert(0, "tools/scripts")
	from dated_output import alias_copy
	print("dated twin: %s" % alias_copy(out_path))
	# Print ASCII-safe summary: Windows consoles often use GBK and mangle CJK.
	print("wrote %d cards to %s" % (len(out), out_path))
	for c in out:
		print("%s | r%s | %s" % (c["file"], c["rarity"] or "0", c["folder"]))


if __name__ == "__main__":
	main()
