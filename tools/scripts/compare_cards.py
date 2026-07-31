# Mechanical comparison: Notion DB rows vs extracted Unity prefab data.
import io
import json
import os
import sys

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
OUT = os.path.join(ROOT, "tools", "outputs")

unity = json.load(open(os.path.join(OUT, "card_data_3.0.json"), encoding="utf-8"))
notion = json.load(open(os.path.join(OUT, "notion_cards.json"), encoding="utf-8"))

RARITY = {"0": "common", "1": "uncommon", "2": "rare"}

u_by_file = {}
for c in unity:
    base = os.path.splitext(os.path.basename(c["prefab"]))[0]
    c["file"] = base
    c["folderRarity"] = ("common" if "/0_Common/" in c["prefab"]
                         else "uncommon" if "/1_Uncommon/" in c["prefab"]
                         else "rare" if "/2_Rare/" in c["prefab"] else "?")
    u_by_file[base] = c

n_by_file = {r["file"]: r for r in notion}

w = io.StringIO()

# --- list diff -------------------------------------------------------------
only_notion = sorted(set(n_by_file) - set(u_by_file))
only_unity = sorted(set(u_by_file) - set(n_by_file))
w.write("== ONLY IN NOTION (missing from Unity set) ==\n")
for f in only_notion:
    r = n_by_file[f]
    w.write(f"  {f} [{r['rarity']}] name={r['name']!r}\n")
w.write("== ONLY IN UNITY (missing from Notion) ==\n")
for f in only_unity:
    c = u_by_file[f]
    w.write(f"  {f} [{c['folderRarity']}] displayName={c.get('displayName','')!r} ({c['prefab']})\n")

# --- fuzzy hints for near matches ------------------------------------------
w.write("\n== NEAR-MATCH HINTS ==\n")
import difflib
for f in only_notion:
    close = difflib.get_close_matches(f, only_unity, n=3, cutoff=0.6)
    if close:
        w.write(f"  Notion {f} ~ Unity {close}\n")
for f in only_unity:
    close = difflib.get_close_matches(f, only_notion, n=3, cutoff=0.6)
    if close:
        w.write(f"  Unity {f} ~ Notion {close}\n")

# --- rarity ----------------------------------------------------------------
w.write("\n== RARITY MISMATCHES (matched files) ==\n")
for f in sorted(set(n_by_file) & set(u_by_file)):
    n_r = n_by_file[f]["rarity"]
    c = u_by_file[f]
    u_r = RARITY.get(c.get("rarity", ""), "?")
    if n_r != u_r or n_r != c["folderRarity"]:
        w.write(f"  {f}: notion={n_r} unityField={u_r} unityFolder={c['folderRarity']}\n")

# --- name vs displayName ----------------------------------------------------
w.write("\n== NAME vs DISPLAYNAME ==\n")
for f in sorted(set(n_by_file) & set(u_by_file)):
    n_name = n_by_file[f]["name"]
    u_disp = u_by_file[f].get("displayName", "")
    if (n_name or "") != (u_disp or ""):
        w.write(f"  {f}: notion={n_name!r} unity={u_disp!r}\n")

# --- unity cards whose displayName empty (info) -----------------------------
w.write("\n== UNITY displayName EMPTY (info) ==\n")
for f in sorted(u_by_file):
    if not u_by_file[f].get("displayName"):
        w.write(f"  {f}\n")

sys.stdout.reconfigure(encoding="utf-8")
sys.stdout.write(w.getvalue())
