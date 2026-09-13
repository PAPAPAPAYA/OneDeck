# -*- coding: utf-8 -*-
"""Compare unity_cards_current.json (truth) against notion_cards_current.json snapshot."""
import json, io, sys, re

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")

TAG_MAP = {"<tag:DeathRattle>": "遗言", "<tag:Believer>": "信徒", "<tag:Awaken>": "苏醒",
           "<tag:Curse>": "诅咒", "<tag:Revive>": "复活", "<tag:Enhance>": "强化"}

def norm_desc(s):
    if s is None:
        return ""
    s = TAG_MAP.get(s, s)
    for k, v in TAG_MAP.items():
        s = s.replace(k, v)
    s = re.sub(r"</?b>", "", s)
    s = s.replace("：", ":").replace("；", ";").replace("，", ",").replace("。", ".")
    s = s.replace("×", "x")
    s = re.sub(r"\s+", "", s)
    return s

unity = json.load(open("tools/outputs/unity_cards_current.json", encoding="utf-8"))
notion = json.load(open("tools/outputs/notion_cards_current.json", encoding="utf-8"))

u_by = {c["cardTypeID"]: c for c in unity}
n_by = {r["ctid"]: r for r in notion}

print("== coverage ==")
missing_in_notion = sorted(set(u_by) - set(n_by))
no_prefab = sorted(set(n_by) - set(u_by))
print("unity cards missing from notion:", missing_in_notion or "NONE")
for ct in no_prefab:
    r = n_by[ct]
    print(f"notion row without prefab: {ct} (状态={r['zt']})")

print("\n== field diffs ==")
for ctid in sorted(u_by):
    u, n = u_by[ctid], n_by.get(ctid)
    if not n:
        continue
    diffs = []
    if u["displayName"] != n["name"]:
        diffs.append(f"name: unity[{u['displayName']}] db[{n['name']}]")
    if u["rarityName"] != n["rarity"]:
        diffs.append(f"rarity: unity[{u['rarityName']}] db[{n['rarity']}]")
    # ATK: creatures only
    u_bio = "生物" if u["cardType"] == "1" else "非生物"
    if u_bio != n["bio"]:
        diffs.append(f"bio: unity[{u_bio}] db[{n['bio']}]")
    if u_bio == "生物":
        u_atk = int(u["printedAttack"] or 0)
        n_atk = n["atk"]
        if n_atk is None or int(n_atk) != u_atk:
            diffs.append(f"atk: unity[{u_atk}] db[{n_atk}]")
    else:
        if n["atk"] is not None:
            diffs.append(f"atk: unity[empty for non-creature] db[{n['atk']}]")
    ud, nd = norm_desc(u["cardDesc"]), norm_desc(n["desc"])
    if ud != nd:
        diffs.append(f"desc:\n  unity[{u['cardDesc']}]\n  db   [{n['desc']}]")
    if diffs:
        print(f"- {ctid} (id {n['id']}, url {n['url']}):")
        for d in diffs:
            print(f"  {d}")

print("\n== db empties to fill (tag / Unity配置状态) ==")
for ctid in sorted(n_by):
    n = n_by[ctid]
    if ctid not in u_by:
        continue
    if not n["tags"]:
        print(f"- {ctid}: tag empty (db desc: {n['desc']})")
    if not n["ucs"]:
        print(f"- {ctid}: Unity配置状态 empty")
