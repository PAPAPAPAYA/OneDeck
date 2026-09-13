# -*- coding: utf-8 -*-
# Audit scanner: for every card prefab under Cards/4.0 and Cards/3.0 no cost (current),
# dump cardDesc + per-container structure (event, cost checks, effect calls, StringSO refs)
# so "cost-like first effect + follow-up effect" cards can be reviewed for gating.
import os, re, json, sys, io

ROOT = r"D:\Unity Projects\OneDeck"
FOLDERS = [
    os.path.join(ROOT, r"Assets\Prefabs\Cards\4.0"),
    os.path.join(ROOT, r"Assets\Prefabs\Cards\3.0 no cost (current)"),
]
OUT_JSON = os.path.join(ROOT, "tools", "outputs", "prefab_container_scan.json")

# guid -> script name from Assets/Scripts/**.cs.meta
script_guids = {}
for dirpath, _, files in os.walk(os.path.join(ROOT, "Assets", "Scripts")):
    for f in files:
        if f.endswith(".cs.meta"):
            p = os.path.join(dirpath, f)
            try:
                with io.open(p, "r", encoding="utf-8") as fh:
                    m = re.search(r"guid: ([0-9a-f]{32})", fh.read(4096))
            except OSError:
                continue
            if m:
                script_guids[m.group(1)] = f[:-8]

# one-time guid -> asset path index over SORefs/Resources (+ Scripts fallback)
meta_map = {}
for _dir in ("Assets/SORefs", "Assets/Resources", "Assets/Scripts"):
    for _dp, _, _fs in os.walk(os.path.join(ROOT, _dir.replace("/", os.sep))):
        for _f in _fs:
            if _f.endswith(".meta"):
                _p = os.path.join(_dp, _f)
                try:
                    with io.open(_p, "r", encoding="utf-8") as _fh:
                        _m = re.search(r"guid: ([0-9a-f]{32})", _fh.read(4096))
                except OSError:
                    continue
                if _m:
                    meta_map[_m.group(1)] = _p[:-5]

def resolve_guid(g):
    return meta_map.get(g)

def string_so_value(path):
    if not path:
        return None
    try:
        with io.open(path, "r", encoding="utf-8") as fh:
            m = re.search(r"^  value: (.*)$", fh.read(), re.M)
        return m.group(1).strip() if m else None
    except OSError:
        return None

def unescape(s):
    def rep(m):
        return chr(int(m.group(1), 16))
    return re.sub(r"\\u([0-9a-fA-F]{4})", rep, s or "")

GUID_RE = re.compile(r"guid: ([0-9a-f]{32})")
FIELD_RE = re.compile(r"^  (\w+): (.*)$", re.M)

def parse_prefab(path):
    with io.open(path, "r", encoding="utf-8") as fh:
        lines = fh.readlines()
        text = "".join(lines)

    def field_full(name):
        # value of `  name: <rest>`, folding YAML multi-line quoted scalars
        for i, ln in enumerate(lines):
            m = re.match(r"^  " + name + r": (.*)$", ln.rstrip("\n"))
            if not m:
                continue
            val = m.group(1)
            while val.count('"') % 2 == 1 and i + 1 < len(lines):
                i += 1
                val += " " + lines[i].strip()
            return val
        return None
    blocks = []
    for chunk in re.split(r"^--- !u!", text, flags=re.M):
        chunk = chunk.strip()
        if not chunk:
            continue
        head = re.match(r"(\d+) &(\d+)", chunk)
        if not head:
            continue
        blocks.append({"tag": head.group(1), "fid": head.group(2), "body": chunk})

    gos = {}      # fid -> name
    comps = {}    # fid -> block
    for b in blocks:
        if b["tag"] == "1":
            m = re.search(r"^  m_Name: (.*)$", b["body"], re.M)
            gos[b["fid"]] = m.group(1).strip() if m else "?"
        elif b["tag"] == "114":
            comps[b["fid"]] = b

    # root card script
    card = None
    for b in blocks:
        if b["tag"] != "114":
            continue
        sg = GUID_RE.search(re.search(r"^  m_Script: (.*)$", b["body"], re.M).group(1))
        if sg and script_guids.get(sg.group(1)) == "CardScript":
            card = {
                "cardTypeID": unescape((field_full("cardTypeID") or "").strip('"')),
                "displayName": unescape((field_full("displayName") or "").strip('"')),
                "rarity": (field_full("rarity") or "").strip(),
                "cardDesc": unescape((field_full("cardDesc") or "").strip('"')),
            }
            break
    if card is None:
        return None

    # map component fid -> GO fid, GO -> components
    comp_go, go_comps = {}, {}
    for b in blocks:
        if b["tag"] != "114":
            continue
        mg = re.search(r"^  m_GameObject: \{fileID: (\d+)\}", b["body"], re.M)
        if mg:
            comp_go[b["fid"]] = mg.group(1)
            go_comps.setdefault(mg.group(1), []).append(b["fid"])

    # root listeners: event guid + target container fid
    listeners = []
    root_go = None
    for fid, b in comps.items():
        sg = GUID_RE.search(re.search(r"^  m_Script: (.*)$", b["body"], re.M).group(1))
        if sg and script_guids.get(sg.group(1)) == "GameEventListener":
            ev = re.search(r"^  event: \{fileID: 11400000, guid: ([0-9a-f]{32})", b["body"], re.M)
            tgts = re.findall(r"m_Target: \{fileID: (\d+)\}", b["body"])
            listeners.append({"event_guid": ev.group(1) if ev else None, "targets": tgts, "fid": fid})
            if comp_go.get(fid):
                root_go = comp_go[fid]

    containers = []
    for fid, b in comps.items():
        sg = GUID_RE.search(re.search(r"^  m_Script: (.*)$", b["body"], re.M).group(1))
        if not sg or script_guids.get(sg.group(1)) != "CostNEffectContainer":
            continue
        go = comp_go.get(fid)
        # event that fires this container
        ev_guid = None
        for l in listeners:
            if fid in l["targets"]:
                ev_guid = l["event_guid"]
        # split body into checkCostEvent / effectEvent sections
        cc = re.search(r"checkCostEvent:(.*?)(?:preEffectEvent:|effectEvent:)", b["body"], re.S)
        ee = re.search(r"effectEvent:(.*?)$", b["body"], re.S)
        pre = re.search(r"preEffectEvent:(.*?)(?:effectEvent:)", b["body"], re.S)
        def calls(section):
            out = []
            if not section:
                return out
            for cm in re.finditer(r"m_MethodName: (\S+)[\s\S]*?m_IntArgument: (-?\d+)", section.group(1)):
                out.append(cm.group(1) + "(" + cm.group(2) + ")")
            return out
        tt = re.search(r"^  targetCardTypeID: \{fileID: (11400000, )?guid: ([0-9a-f]{32})", b["body"], re.M)
        tcs = re.search(r"^  cursedCardTypeID: \{fileID: (11400000, )?guid: ([0-9a-f]{32})", b["body"], re.M)
        containers.append({
            "child": gos.get(go, "?"),
            "event": resolve_guid(ev_guid) if ev_guid else None,
            "costChecks": calls(cc),
            "preEffects": calls(pre),
            "effects": calls(ee),
            "targetCardTypeID": string_so_value(resolve_guid(tt.group(2))) if tt else None,
        })
    card["folder"] = os.path.basename(os.path.dirname(path))
    card["file"] = os.path.basename(path)
    card["containers"] = containers
    return card

results = []
for folder in FOLDERS:
    for dirpath, dirnames, files in os.walk(folder):
        dirnames[:] = [d for d in dirnames if "-1_Test" not in d]
        for f in sorted(files):
            if f.endswith(".prefab"):
                c = parse_prefab(os.path.join(dirpath, f))
                if c:
                    results.append(c)

with io.open(OUT_JSON, "w", encoding="utf-8") as fh:
    json.dump(results, fh, ensure_ascii=False, indent=1)

print("cards:", len(results))
for c in results:
    cs = " | ".join(
        (x["child"] + " ev=" + str(x["event"]).split("\\")[-1]
         + " cost=" + (",".join(x["costChecks"]) or "-")
         + " fx=" + (",".join(x["effects"]) or "-")) for x in c["containers"])
    print(c["cardTypeID"], c["rarity"], "|", c["displayName"], "|", c["cardDesc"], "||", cs)
