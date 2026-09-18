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
# CardScript.cs.meta guid: other components (e.g. CurseEffect) also carry a
# cardTypeID field, often as an asset reference, so field grabs must be scoped
# to the root CardScript MonoBehaviour block.
CARD_SCRIPT_GUID = "f47b4b127fc943869d9dbca8f00704e8"
# Mirror of EnumStorage.Tag (append-only enum; ints are serialized in prefabs).
TAG_ENUM = {"0": "None", "1": "Linger", "2": "ManaX", "3": "DeathRattle",
		    "4": "Bury", "5": "Enhance", "6": "Believer", "7": "Exile",
		    "8": "Curse", "9": "Awaken", "10": "Passive", "11": "Revive",
		    "12": "EnhanceReaction", "13": "MultiAttack"}
# Mirror of EnumStorage.CardType (append-only; Status renamed Token, value 2 unchanged).
CARD_TYPE_ENUM = {"0": "None", "1": "Creature", "2": "Token"}


# myTags serializes as one hex string of little-endian int32 values,
# e.g. "0300000005000000" = [DeathRattle, Enhance]; empty/missing = no tags.
def parse_my_tags(raw):
	hexstr = raw.strip().strip('"')
	if len(hexstr) < 8:
		return []
	names = []
	for i in range(0, len(hexstr) - len(hexstr) % 8, 8):
		value = int.from_bytes(bytes.fromhex(hexstr[i:i + 8]), "little")
		names.append(TAG_ENUM.get(str(value), "Unknown%d" % value))
	return names


def grab(text, field):
	# \Z: a field can be the LAST one in its component block (the split drops
	# the `--- !u!114` delimiter), so the lookahead must also accept end-of-text.
	m = re.search(r"^  " + field + r": (.*?)(?=^  \w|^--- |\Z)", text, re.M | re.S)
	return m.group(1).strip() if m else ""


def dec(s):
	# Unity YAML folds long quoted strings across lines with indentation, and
	# escapes U+00FF and below as \xNN (not \uNNNN) — rejoin and decode both.
	s = re.sub(r"\n\s*", "", s)
	if s.startswith('"') and s.endswith('"'):
		s = s[1:-1]
	s = re.sub(r"\\u([0-9A-Fa-f]{4})", lambda m: chr(int(m.group(1), 16)), s)
	s = re.sub(r"\\x([0-9A-Fa-f]{2})", lambda m: chr(int(m.group(1), 16)), s)
	return s


def card_script_block(text):
	"""Return the CardScript MonoBehaviour block (fallback: block with rarity:)."""
	for b in re.split(r"^--- !u!114 &", text, flags=re.M):
		if CARD_SCRIPT_GUID in b:
			return b
	for b in re.split(r"^--- !u!114 &", text, flags=re.M):
		if re.search(r"^  rarity:", b, re.M):
			return b
	return text


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
			cs_block = card_script_block(text)
			rarity_raw = grab(cs_block, "rarity").split()[0] if grab(cs_block, "rarity") else ""
			# Empty-at-runtime cardTypeID falls back to the GameObject name,
			# mirroring CardCatalogUploader.cs line 78.
			cid = dec(grab(cs_block, "cardTypeID"))
			if cid.startswith("{") or not cid:
				cid = f.replace(".prefab", "")
			out.append({
				# Prefab file name without extension; this is the canonical "file name".
				"file": f.replace(".prefab", ""),
				"cardTypeID": cid,
				"displayName": dec(grab(cs_block, "displayName")),
				# Missing field means Unity deserializes rarity to 0 = common.
				"rarity": rarity_raw,
				"rarityName": RARITY_NAMES.get(rarity_raw, "common"),
				"cardDesc": dec(grab(cs_block, "cardDesc")),
				"printedAttack": grab(cs_block, "printedAttack").replace("value: ", "").split()[0]
					if grab(cs_block, "printedAttack") else "",
				"utilityKind": grab(cs_block, "utilityKind").replace(": ", "").strip()
					if grab(cs_block, "utilityKind") else "",
				"myTags": parse_my_tags(grab(cs_block, "myTags")),
				"reservedTag": TAG_ENUM.get(grab(cs_block, "reservedTag").split()[0]
					if grab(cs_block, "reservedTag") else "0", "None"),
				"cardType": CARD_TYPE_ENUM.get(grab(cs_block, "cardType").split()[0]
					if grab(cs_block, "cardType") else "0", "None"),
				"extraAttackTimes": grab(cs_block, "extraAttackTimes").split()[0]
					if grab(cs_block, "extraAttackTimes") else "0",
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
