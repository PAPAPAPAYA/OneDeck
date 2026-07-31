# Side-by-side readable report: Notion rows vs Unity prefab ground truth.
import json
import os
import sys

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
OUT = os.path.join(ROOT, "tools", "outputs")

unity = json.load(open(os.path.join(OUT, "card_data2.json"), encoding="utf-8"))
notion = json.load(open(os.path.join(OUT, "notion_cards.json"), encoding="utf-8"))

RARITY = {"0": "common", "1": "uncommon", "2": "rare", None: "?"}
STATUS = {"0": "None", "1": "Infected", "2": "Mana", "3": "HeartChanged",
          "4": "Power", "5": "Rest", "6": "Revive", "7": "Counter"}

u_by_file = {}
for c in unity:
    base = os.path.splitext(os.path.basename(c["prefab"]))[0]
    c["file"] = base
    u_by_file[base] = c
n_by_file = {r["file"]: r for r in notion}

# manual alias mapping (Notion file -> Unity file) for known renames
ALIAS = {
    "GOBLIN_ASSASIN_TEAM": "GOBLIN_ASSASSIN_TEAM",
    "SKELETON_SOLDIER": "SOLDIER_SKELETON",
}

SKIP_FIELD_KEYS = {"baseDmg", "price", "myStatusRef", "theirStatusRef",
                   "ownerIntSO", "enemyIntSO", "statusEffectParticlePrefab",
                   "cardToSummon", "myStatusEffectResolverScript"}


def fmt_call(call):
    s = f"{call['on'].replace('.cs','')}.{call['method']}"
    args = []
    if "int" in call:
        args.append(str(call["int"]))
    if "float" in call:
        args.append(str(call["float"]))
    if "str" in call:
        args.append(repr(call["str"]))
    if "obj" in call:
        args.append(call["obj"].replace(".asset", "").replace(".prefab", ""))
    if args:
        s += "(" + ", ".join(args) + ")"
    return s


def fmt_comp(fields):
    parts = []
    for k, v in fields.items():
        if k in SKIP_FIELD_KEYS or "fileID" in str(v):
            continue
        if k in ("statusEffectToGive", "statusEffectToCount",
                 "statusEffectToCheck"):
            v = STATUS.get(str(v), v)
        parts.append(f"{k}={v}")
    return ", ".join(parts)


def dump_card(w, nfile):
    r = n_by_file.get(nfile)
    ufile = ALIAS.get(nfile, nfile)
    c = u_by_file.get(ufile)
    w.write("=" * 78 + "\n")
    if r:
        w.write(f"NOTION {nfile} [{r['rarity']}] name={r['name']!r} "
                f"category={r['category']}\n")
        w.write(f"  N-desc: {r['desc']!r}\n")
    else:
        w.write(f"NOTION {nfile}: -- no row --\n")
    if not c:
        w.write("  UNITY: -- missing --\n")
        return
    card = c["card"]
    w.write(f"UNITY  {c['prefab']}\n")
    w.write(f"  U-name: {card.get('displayName','')!r}  "
            f"rarity={RARITY.get(card.get('rarity'), card.get('rarity'))}  "
            f"tags={card.get('myTags','')!r}  "
            f"status={card.get('myStatusEffects','')!r}\n")
    w.write(f"  U-desc: {card.get('cardDesc','')!r}\n")
    for l in c["listeners"]:
        ev = l["event"].replace(".asset", "")
        resp = "; ".join(fmt_call(x) for x in l["response"])
        w.write(f"  listen: {ev} -> {resp}\n")
    for ct in c["containers"]:
        cost = "; ".join(fmt_call(x) for x in ct["checkCost"]) or "-"
        eff = "; ".join(fmt_call(x) for x in ct["effect"]) or "-"
        w.write(f"  container[{ct['go']}]: cost={cost} | effect={eff}\n")
    for fid, comp in c["components"].items():
        detail = fmt_comp(comp["fields"])
        w.write(f"  comp {comp['type'].replace('.cs','')}"
                f"[{comp['go']}]: {detail}\n")


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    w = sys.stdout
    # all notion rows in rarity/category order (as DB sorts)
    order = {"common": 0, "uncommon": 1, "rare": 2}
    rows = sorted(notion, key=lambda r: (order.get(r["rarity"], 9), r["file"]))
    for r in rows:
        dump_card(w, r["file"])
    # unity-only cards at the end
    aliased_unity = set(ALIAS.values())
    for c in sorted(unity, key=lambda c: c["file"]):
        if c["file"] in n_by_file or c["file"] in aliased_unity:
            continue
        w.write("=" * 78 + "\n")
        w.write(f"UNITY-ONLY {c['prefab']}\n")
        card = c["card"]
        w.write(f"  U-name: {card.get('displayName','')!r}  "
                f"rarity={RARITY.get(card.get('rarity'), card.get('rarity'))}\n")
        w.write(f"  U-desc: {card.get('cardDesc','')!r}\n")
        for l in c["listeners"]:
            ev = l["event"].replace(".asset", "")
            resp = "; ".join(fmt_call(x) for x in l["response"])
            w.write(f"  listen: {ev} -> {resp}\n")
        for ct in c["containers"]:
            cost = "; ".join(fmt_call(x) for x in ct["checkCost"]) or "-"
            eff = "; ".join(fmt_call(x) for x in ct["effect"]) or "-"
            w.write(f"  container[{ct['go']}]: cost={cost} | effect={eff}\n")
        for fid, comp in c["components"].items():
            detail = fmt_comp(comp["fields"])
            w.write(f"  comp {comp['type'].replace('.cs','')}"
                    f"[{comp['go']}]: {detail}\n")


if __name__ == "__main__":
    main()
