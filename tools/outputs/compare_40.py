"""Field-level diff: 4.0 card prefabs (engine truth) vs Notion 4.0 card database TSV dump.

Outputs: set diffs (missing/extra CARD_TYPE_ID), rarity / ATK / 中文名 mismatches,
and normalized cardDesc diffs. Chinese output; read stdout as UTF-8.
"""
import json
import os
import re

CARD_ROOT = os.path.join("Assets", "Prefabs", "Cards", "4.0")
EXCLUDE = ("-1_Test",)
DB_TSV = "tools/outputs/notion_40_db.tsv"
RARITY_NAMES = {"0": "normal", "1": "uncommon", "2": "rare"}
TAGMAP = {"Believer": "信徒", "DeathRattle": "遗言", "Linger": "萦绕",
          "Curse": "诅咒", "ManaX": "法力X", "Power": "强化"}


def grab_all(text, field):
    return re.findall(r"^  " + field + r": (.*)$", text, re.M)


def dec(s):
    s = s.strip()
    if s.startswith('"') and s.endswith('"'):
        s = s[1:-1]
    s = re.sub(r"\\u([0-9A-Fa-f]{4})", lambda m: chr(int(m.group(1), 16)), s)
    s = re.sub(r"\\x([0-9A-Fa-f]{2})", lambda m: chr(int(m.group(1), 16)), s)
    return s.replace("\\n", "").strip()


def norm_desc(s):
    if not s:
        return ""
    s = re.sub(r"<tag:(\w+)>", lambda m: TAGMAP.get(m.group(1), m.group(1)), s)
    s = re.sub(r"<[^>]+>", "", s)
    s = s.replace("×", "x").replace("X", "x")
    s = re.sub(r"[\s,，;；:：、.。()（）\[\]【】\"'“”‘’]", "", s)
    return s


def load_engine():
    out = {}
    for dirpath, dirs, files in os.walk(CARD_ROOT):
        if any(m in dirpath for m in EXCLUDE):
            continue
        for f in sorted(files):
            if not f.endswith(".prefab"):
                continue
            text = open(os.path.join(dirpath, f), encoding="utf-8").read()
            # CardScript serializes last; later matches win over the shop-view
            # component that holds a StringSO-reference cardTypeID.
            ctid = grab_all(text, "cardTypeID")
            ctid = [v for v in ctid if re.fullmatch(r"[A-Za-z0-9_]+", v.strip())]
            ctid = dec(ctid[-1]) if ctid else f.replace(".prefab", "")
            rar = grab_all(text, "rarity")
            atk = grab_all(text, "printedAttack")
            ctype = grab_all(text, "cardType")
            name = grab_all(text, "displayName")
            desc = grab_all(text, "cardDesc")
            out[ctid] = {
                "file": f.replace(".prefab", ""),
                "folder": os.path.basename(dirpath),
                "rarity": dec(rar[-1]) if rar else "0",
                "atk": dec(atk[-1]) if atk else "",
                "cardType": dec(ctype[-1]) if ctype else "",
                "name": dec(name[-1]) if name else "",
                "desc": dec(desc[-1]) if desc else "",
            }
    return out


def load_db():
    rows = {}
    for line in open(DB_TSV, encoding="utf-8"):
        line = line.rstrip("\n")
        if not line.strip():
            continue
        p = (line.split("\t") + [""] * 7)[:7]
        rows[p[1]] = {"id": p[0], "rarity": p[2], "atk": p[3], "name": p[4],
                      "desc": p[5], "status": p[6]}
    return rows


def main():
    eng = load_engine()
    db = load_db()
    print("engine=%d db=%d" % (len(eng), len(db)))

    print("\n== engine-only (missing in DB) ==")
    for k in sorted(set(eng) - set(db)):
        c = eng[k]
        print("%s | %s | %s | atk=%s | ctype=%s | %s" %
              (k, c["name"], RARITY_NAMES.get(c["rarity"], c["rarity"]), c["atk"], c["cardType"], c["desc"]))

    print("\n== DB-only (no prefab) ==")
    for k in sorted(set(db) - set(eng)):
        print("%s | %s | status=%s" % (db[k]["id"], k, db[k]["status"] or "(启用)"))

    print("\n== field mismatches (matched ids) ==")
    n_desc, n_name, n_other = 0, 0, 0
    for k in sorted(set(eng) & set(db)):
        e, d = eng[k], db[k]
        er = RARITY_NAMES.get(e["rarity"], e["rarity"])
        probs = []
        if er != d["rarity"]:
            probs.append("rarity engine=%s db=%s" % (er, d["rarity"]))
        if e["name"] != d["name"]:
            if d["name"] == "":
                probs.append("中文名 MISSING (engine: %s)" % e["name"])
            else:
                probs.append("中文名 engine=%s db=%s" % (e["name"], d["name"]))
        e_creature = e["cardType"] == "1"
        if e_creature and d["atk"] == "":
            probs.append("ATK missing in DB (engine=%s)" % e["atk"])
        elif not e_creature and d["atk"] != "":
            probs.append("ATK set in DB but non-creature")
        elif e_creature and e["atk"] != d["atk"]:
            probs.append("ATK engine=%s db=%s" % (e["atk"], d["atk"]))
        if probs:
            n_other += 1
            print("%s %s | %s" % (d["id"], k, "; ".join(probs)))
        if norm_desc(e["desc"]) != norm_desc(d["desc"]):
            n_desc += 1
            print("--- desc diff %s %s\n  E: %s\n  D: %s" % (d["id"], k, e["desc"], d["desc"]))
    print("\nsummary: mismatch-rows=%d desc-diffs=%d" % (n_other, n_desc))


if __name__ == "__main__":
    main()
