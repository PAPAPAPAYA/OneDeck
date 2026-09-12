"""Which of the 4.0 prefabs actually ride the card-catalog upload?
CardCatalogUploader posts DeckSaver.shopPoolRef.deck + DeckSaver.additionalCardPrefabs.
Read-only."""
import io
import os
import re
import sys

ROOT = r"D:\Unity Projects\OneDeck"
PREFAB_ROOT = os.path.join(ROOT, "Assets", "Prefabs", "Cards", "4.0")
POOL = os.path.join(ROOT, "Assets", "SORefs", "ShopRefs", "ShopPoolRef.asset")
SCENE = os.path.join(ROOT, "Assets", "Scenes", "GameScene.unity")
DECKSAVER_META = os.path.join(ROOT, "Assets", "Scripts", "Managers", "WriteRead", "DeckSaver.cs.meta")
REF = re.compile(r"guid: ([0-9a-f]{32})")
PREV_SCRIPT = re.compile(r"^  m_Script: \{fileID: 11500000, guid: ([0-9a-f]+), type: 3\}")


def prefab_guids():
	"""guid -> (cardTypeID, relpath) for every 4.0 prefab."""
	out = {}
	for dirpath, dirnames, filenames in os.walk(PREFAB_ROOT):
		dirnames[:] = [d for d in dirnames if d != "-1_Test"]
		for fn in filenames:
			if not fn.endswith(".prefab.meta"):
				continue
			path = os.path.join(dirpath, fn)
			guid = REF.search(io.open(path, encoding="utf-8").read())
			if guid:
				out[guid.group(1)] = os.path.relpath(path[:-5], ROOT)
	return out


def pool_guids():
	guids = []
	for line in io.open(POOL, encoding="utf-8"):
		if line.startswith("  - {") and "guid:" in line:
			guids.append(REF.search(line).group(1))
	return guids


def additional_guids():
	"""additionalCardPrefabs list from the DeckSaver component in GameScene."""
	saver_guid = REF.search(io.open(DECKSAVER_META, encoding="utf-8").read()).group(1)
	lines = io.open(SCENE, encoding="utf-8", errors="replace").read().splitlines()
	in_saver = False
	in_list = False
	guids = []
	for line in lines:
		if line.startswith("--- !u!"):
			in_saver, in_list = False, False
			continue
		m = PREV_SCRIPT.match(line)
		if m:
			in_saver = m.group(1) == saver_guid
			continue
		if not in_saver:
			continue
		if re.match(r"^  additionalCardPrefabs:", line):
			in_list = True
			continue
		if in_list:
			if re.match(r"^  - \{", line):
				g = REF.search(line)
				if g:
					guids.append(g.group(1))
				continue
			if re.match(r"^  \S", line):
				in_list = False
	return guids


def main():
	prefabs = prefab_guids()
	pool = pool_guids()
	add = additional_guids()
	print("4.0 prefabs            : %d" % len(prefabs))
	print("shopPoolRef.deck refs  : %d" % len(pool))
	print("additionalCardPrefabs  : %d" % len(add))
	missing_pool = [g for g in prefabs if g not in pool]
	extra_pool = [g for g in pool if g not in prefabs]
	print("\nprefabs NOT in shopPoolRef : %d" % len(missing_pool))
	for g in missing_pool:
		print("  " + prefabs[g])
	print("shopPoolRef refs with no 4.0 prefab : %d" % len(extra_pool))
	for g in extra_pool:
		print("  guid " + g)
	add_missing = [g for g in prefabs if g in add]
	print("\nadditionalCardPrefabs also in 4.0 pool : %d" % len(add_missing))
	for g in add_missing:
		print("  " + prefabs[g])
	return 0


if __name__ == "__main__":
	sys.exit(main())
