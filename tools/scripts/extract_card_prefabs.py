# Offline Unity prefab extractor for the zombie-baseline balance audit.
# Parses card prefab YAML directly (no Unity needed):
#   per card: rarity folder, displayName, cardTypeID, cardDesc, minion/tags,
#   per container: trigger event, cost checks, effect calls (Class->Method(arg)) + key fields.
# Output: one compact line per card to stdout (UTF-8).

import os, re, sys, io

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")

ROOT = "Assets/Prefabs/Cards/4.0"
EXCLUDE_DIRS = ("-1_Test",)

# 1. guid -> script class name map
guid2class = {}
for dirpath, _, files in os.walk("Assets/Scripts"):
    for fn in files:
        if fn.endswith(".cs.meta"):
            p = os.path.join(dirpath, fn)
            with open(p, encoding="utf-8", errors="replace") as f:
                m = re.search(r"guid: ([0-9a-f]{32})", f.read())
            if m:
                guid2class[m.group(1)] = fn[:-8]  # strip .cs.meta

# 1b. GameEvent SO guid -> event name map
guid2event = {}
for dirpath, _, files in os.walk("Assets/SORefs/GameEvents"):
    for fn in files:
        if fn.endswith(".asset"):
            p = os.path.join(dirpath, fn)
            mp = p + ".meta"
            if not os.path.exists(mp):
                continue
            with open(mp, encoding="utf-8", errors="replace") as f:
                gm = re.search(r"guid: ([0-9a-f]{32})", f.read())
            with open(p, encoding="utf-8", errors="replace") as f:
                nm = re.search(r"m_Name: (.*)", f.read())
            if gm and nm:
                guid2event[gm.group(1)] = nm.group(1).strip()

# 1c. SO asset guid -> (m_Name, value) map (IntSO/BoolSO/StringSO under Assets/SORefs)
guid2so = {}
for dirpath, _, files in os.walk("Assets/SORefs"):
    for fn in files:
        if not fn.endswith(".asset"):
            continue
        p = os.path.join(dirpath, fn)
        mp = p + ".meta"
        if not os.path.exists(mp):
            continue
        with open(mp, encoding="utf-8", errors="replace") as f:
            gm = re.search(r"guid: ([0-9a-f]{32})", f.read())
        with open(p, encoding="utf-8", errors="replace") as f:
            t = f.read()
        nm = re.search(r"^  m_Name: (.*)$", t, re.M)
        val = re.search(r"^  value: (-?\d+)$", t, re.M)
        if gm:
            guid2so[gm.group(1)] = (nm.group(1).strip() if nm else fn[:-6],
                                    val.group(1) if val else None)

# fields we care about per component
FIELD_PAT = re.compile(
    r"^  (cardTypeID|displayName|cardDesc|isMinion|isStartCard|extraDmg|cardCount|powerCoefficient|"
    r"lastXCardsCount|xFriendlyCount|statusEffectLayerCount|yFriendlyLayerCount|layerCount|"
    r"statusEffectToGive|statusEffectToCheck|statusEffectToConsume|amount|powerAmount|multiplier|"
    r"statusEffectMultiplier|excludeSelf|isFromFriendly|fromFriendly|give|targetCardTypeID|"
    r"curseCardTypeID|shopRollWeightMultiplier|takeUpSpace|myTags|yFriendlyLayerCount|"
    r"baseDmg|dmgAmountAlter|healAmountAlter|creatureFilter|tagsToCheck|typeIDFilter|"
    r"rarityFilter|sortBy|reviveTargetSide|delayedRevive|targetFriendly|curseEngine|"
    r"ownerIntSO|enemyIntSO|cardType|isPassive|printedAttack|attackTimes|extraAttackTimes|attackGrowth|consumeHostileCurse|"
    r"statusEffectToCount|countSourceSide|transferAmount|graveFilter): ?(.*)$", re.M)

DOC_RE = re.compile(r"^--- !u!(\d+) &(\d+)", re.M)

def unesc(s):
    return re.sub(r"\\u([0-9a-fA-F]{4})", lambda m: chr(int(m.group(1), 16)), s)

def parse_prefab(path):
    with open(path, encoding="utf-8", errors="replace") as f:
        text = f.read()
    docs = []
    for m in DOC_RE.finditer(text):
        docs.append((m.group(1), m.group(2), m.start()))
    comps = {}  # fileID -> dict
    for i, (unity_type, fid, start) in enumerate(docs):
        end = docs[i+1][2] if i+1 < len(docs) else len(text)
        body = text[start:end]
        if unity_type != "114":
            continue
        sm = re.search(r"m_Script: \{fileID: \d+, guid: ([0-9a-f]{32})", body)
        cls = guid2class.get(sm.group(1), "?" + (sm.group(1)[:6] if sm else "?"))
        # scalar fields
        fields = {}
        for fm in FIELD_PAT.finditer(body):
            fields[fm.group(1)] = unesc(fm.group(2).strip().strip('"'))
        # resolve SO / GameEvent references to readable names (and IntSO values)
        for k, v in list(fields.items()):
            rm = re.search(r"guid: ([0-9a-f]{32})", v)
            if rm:
                g = rm.group(1)
                if g in guid2event:
                    fields[k] = "EVENT:" + guid2event[g]
                elif g in guid2so:
                    nm2, val2 = guid2so[g]
                    fields[k] = "SO:%s=%s" % (nm2, val2 if val2 is not None else "?")
        # unityevent calls: track current 2-space field name
        calls = []  # (eventField, targetFileID, method, intArg)
        cur_field = None
        cur_call = None
        for line in body.splitlines():
            fm = re.match(r"^  (\w+):\s*$", line)
            if fm:
                cur_field = fm.group(1)
            tm = re.search(r"- m_Target: \{fileID: (\d+)\}", line)
            if tm:
                cur_call = {"event": cur_field, "target": tm.group(1), "method": None, "arg": None}
                calls.append(cur_call)
            mm = re.search(r"m_MethodName: (\w+)", line)
            if mm and cur_call is not None and cur_call["method"] is None:
                cur_call["method"] = mm.group(1)
            am = re.search(r"m_IntArgument: (-?\d+)", line)
            if am and cur_call is not None and cur_call["arg"] is None:
                cur_call["arg"] = am.group(1)
        comps[fid] = {"cls": cls, "name": fields.get("displayName") or None,
                      "fields": fields, "calls": calls,
                      "cname": None}
        # component's own m_Name (container names live here)
        nm = re.search(r"^  m_Name: (.*)$", body, re.M)
        if nm:
            comps[fid]["cname"] = nm.group(1).strip()
        # GameEvent SO reference (GameEventListener.event)
        ev = re.search(r"^  event: \{fileID: \d+, guid: ([0-9a-f]{32})", body, re.M)
        if ev:
            comps[fid]["eventRef"] = ev.group(1)
    return comps

def card_summary(path, rel):
    comps = parse_prefab(path)
    root = None
    for fid, c in comps.items():
        if c["cls"] == "CardScript":
            root = (fid, c)
            break
    if root is None:
        return None
    rfid, rc = root
    out = []
    out.append(f"CARD|{rel}|display={rc['fields'].get('displayName','')}|type={rc['fields'].get('cardTypeID','')}"
               f"|atk={rc['fields'].get('printedAttack','')}|ctype={rc['fields'].get('cardType','')}"
               f"|extraTimes={rc['fields'].get('extraAttackTimes','')}|growth={rc['fields'].get('attackGrowth','')}"
               f"|passive={rc['fields'].get('isPassive','')}"
               f"|minion={rc['fields'].get('isMinion','')}|tags={rc['fields'].get('myTags','')}"
               f"|desc={rc['fields'].get('cardDesc','')}")
    # find trigger bindings: any component holding a GameEvent `event` ref + response calls
    # (CardScript inherits GameEventListener, so the binding may live on CardScript itself)
    for fid, c in comps.items():
        if not c.get("eventRef"):
            continue
        trig = guid2event.get(c.get("eventRef", ""), "?" + c.get("eventRef", "")[:6])
        for call in c["calls"]:
            if call["method"] in ("InvokeEffectEventVoid", "InvokeEffectEvent"):
                cont = comps.get(call["target"])
                if cont is None:
                    continue
                parts = [f"TRIGGER={trig}"]
                parts.append(f"name={cont['cname']}")
                for ccall in cont["calls"]:
                    tgt = comps.get(ccall["target"])
                    tcls = tgt["cls"] if tgt else "?"
                    arg = f"({ccall['arg']})" if ccall["arg"] not in (None, "0") else ""
                    extra = ""
                    if tgt:
                        keyfields = []
                        for k in ("extraDmg", "baseDmg", "dmgAmountAlter", "cardCount", "powerCoefficient",
                                  "lastXCardsCount", "xFriendlyCount", "statusEffectLayerCount",
                                  "yFriendlyCount", "statusEffectToGive", "statusEffectToCheck",
                                  "statusEffectToConsume", "statusEffectMultiplier", "powerAmount",
                                  "creatureFilter", "tagsToCheck", "typeIDFilter", "rarityFilter",
                                  "sortBy", "reviveTargetSide", "delayedRevive", "targetFriendly",
                                  "curseEngine", "ownerIntSO", "enemyIntSO", "amount", "attackTimes",
                                  "consumeHostileCurse", "statusEffectToCount", "countSourceSide",
                                  "transferAmount", "graveFilter", "excludeSelf", "isFromFriendly",
                                  "fromFriendly", "give", "targetCardTypeID", "curseCardTypeID",
                                  "multiplier", "yFriendlyLayerCount"):
                            if k in tgt["fields"]:
                                keyfields.append(f"{k}={tgt['fields'][k]}")
                        if keyfields:
                            extra = "[" + ",".join(keyfields) + "]"
                    parts.append(f"{ccall['event']}:{tcls}->{ccall['method']}{arg}{extra}")
                out.append("  CONT| " + " | ".join(parts))
    return "\n".join(out)

results = []
for dirpath, _, files in os.walk(ROOT):
    if any(x in dirpath for x in EXCLUDE_DIRS):
        continue
    for fn in sorted(files):
        if fn.endswith(".prefab"):
            p = os.path.join(dirpath, fn)
            rel = os.path.relpath(p, ROOT).replace("\\", "/")
            s = card_summary(p, rel)
            if s:
                results.append(s)
print("\n".join(results))
