"""Compare the production card_catalog (dumped to _prod_catalog.tsv) with the
current card prefabs' CardScript.cardTypeID / displayName.

Parsing notes (learned the hard way):
- Match per MonoBehaviour block, not per line: CurseEffect also has a
  `cardTypeID` field, but it is an asset reference, not a string.
- Unity writes non-ASCII YAML strings escaped and double-quoted
  (`displayName: "\u4F8D\u50E7"`), so strip the quotes before unescaping.
Read-only diagnostic.
"""
import io
import os
import re
import sys

ROOT = r"D:\Unity Projects\OneDeck"
PREFAB_ROOT = os.path.join(ROOT, "Assets", "Prefabs", "Cards", "4.0")
TSV = os.path.join(ROOT, "tools", "outputs", "_prod_catalog.tsv")
CARD_SCRIPT_GUID = "f47b4b127fc943869d9dbca8f00704e8"

BLOCK = re.compile(r"^--- !u!114 &(\d+)")
SCRIPT_GUID = re.compile(r"^  m_Script: \{fileID: 11500000, guid: ([0-9a-f]+), type: 3\}")
FIELD = re.compile(r"^  (cardTypeID|displayName): (.*)$")
ESCAPES = (("\\u", "u"), ("\\\\", "\\"), ('\\"', '"'))


def unescape(v: str) -> str:
	if len(v) >= 2 and v.startswith('"') and v.endswith('"'):
		v = v[1:-1]
	def repl(m):
		return chr(int(m.group(1), 16))
	return re.sub(r"\\u([0-9a-fA-F]{4})", repl, v)


def card_script_fields(path):
	"""(cardTypeID, displayName) from the CardScript block, or (None, None)."""
	with io.open(path, encoding="utf-8", errors="replace") as fh:
		lines = fh.read().splitlines()
	in_block = False
	is_card = False
	type_id = name = None
	for line in lines:
		m = BLOCK.match(line)
		if m:
			# CardScript is rarely the last component: stop at the next block once
			# its fields have been read, instead of letting it reset them.
			if type_id is not None:
				break
			in_block, is_card = True, False
			continue
		if not in_block:
			continue
		m = SCRIPT_GUID.match(line)
		if m:
			is_card = m.group(1) == CARD_SCRIPT_GUID
			continue
		if not is_card:
			continue
		m = FIELD.match(line)
		if m:
			val = unescape(m.group(2).strip())
			if m.group(1) == "cardTypeID":
				type_id = val
			else:
				name = val
	if type_id is None:
		return None, None
	if not name:
		name = os.path.splitext(os.path.basename(path))[0]
	return type_id, name


def scan(root, skip_test=True):
	out, dup = {}, {}
	for dirpath, dirnames, filenames in os.walk(root):
		if skip_test:
			dirnames[:] = [d for d in dirnames if d != "-1_Test"]
		for fn in filenames:
			if not fn.endswith(".prefab"):
				continue
			path = os.path.join(dirpath, fn)
			type_id, name = card_script_fields(path)
			if not type_id:
				continue
			rel = os.path.relpath(path, ROOT)
			if type_id in out:
				dup.setdefault(type_id, [out[type_id]]).append((name, rel))
			out[type_id] = (name, rel)
	return out, dup


def load_prod():
	prod = {}
	with io.open(TSV, encoding="utf-8") as fh:
		for line in fh:
			parts = line.rstrip("\n").split("\t")
			if parts[0] != "CAT" or len(parts) < 3:
				continue
			prod[parts[1]] = parts[2]
	return prod


def main():
	prefabs, dup = scan(PREFAB_ROOT)
	prod = load_prod()
	print("4.0 prefabs (CardScript) : %d" % len(prefabs))
	print("prod catalog rows        : %d" % len(prod))

	stale = [(k, prod[k], prefabs[k][0]) for k in sorted(prod) if k in prefabs and prod[k] != prefabs[k][0]]
	missing = sorted(k for k in prefabs if k not in prod)
	orphan = sorted(k for k in prod if k not in prefabs)
	same = len([k for k in prod if k in prefabs and prod[k] == prefabs[k][0]])

	print("\nmatching names           : %d" % same)
	print("stale names              : %d" % len(stale))
	for k, old, new in stale:
		print("  %-32s %s  ->  %s" % (k, old, new))

	print("\nin prefabs, not in catalog : %d" % len(missing))
	for k in missing:
		print("  %-28s %s   (%s)" % (k, prefabs[k][0], prefabs[k][1]))

	print("\nin catalog, no 4.0 prefab  : %d" % len(orphan))
	# where else could they live?
	for k in orphan:
		hits = []
		for base in ("Assets/Prefabs", "Assets/SORefs"):
			for dirpath, _dirnames, filenames in os.walk(os.path.join(ROOT, base)):
				for fn in filenames:
					if fn.endswith((".prefab", ".asset")) and k.split("_4.0")[0].lower() in fn.lower():
						hits.append(os.path.relpath(os.path.join(dirpath, fn), ROOT))
		print("  %-30s %-24s elsewhere: %s" % (k, prod[k], ", ".join(sorted(set(hits))[:3]) or "-"))

	if dup:
		print("\nsame cardTypeID in >1 prefab : %d" % len(dup))
		for k, rows in sorted(dup.items()):
			print("  " + k)
			for n, p in rows:
				print("     %-24s %s" % (n, p))
	return 0


if __name__ == "__main__":
	sys.exit(main())
