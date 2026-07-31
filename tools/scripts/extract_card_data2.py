# Deep extraction of OneDeck card prefabs: CardScript fields, GameEventListener
# bindings (with resolved event names), CostNEffectContainer cost/effect calls,
# and all effect component fields. Read-only. Outputs JSON.
import json
import os
import re
import sys

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
CARD_DIR = os.path.join(ROOT, "Assets", "Prefabs", "Cards", "3.0 no cost (current)")
ASSETS = os.path.join(ROOT, "Assets")
CARDSCRIPT_GUID = "f47b4b127fc943869d9dbca8f00704e8"


def build_guid_maps():
    """guid -> asset/script basename (no ext), from .meta files."""
    guid_map = {}
    for dirpath, _dirs, files in os.walk(ASSETS):
        for fn in files:
            if not fn.endswith(".meta"):
                continue
            p = os.path.join(dirpath, fn)
            try:
                with open(p, "r", encoding="utf-8", errors="replace") as f:
                    head = f.read(400)
            except OSError:
                continue
            m = re.search(r"guid: ([0-9a-f]{32})", head)
            if m:
                guid_map[m.group(1)] = fn[:-5]
    return guid_map


def decode_yaml_dq(text):
    def repl(m):
        esc = m.group(1)
        if esc.startswith("u"):
            return chr(int(esc[1:], 16))
        table = {"n": "\n", "t": "\t", "r": "\r", '"': '"', "\\": "\\"}
        return table.get(esc, esc)
    return re.sub(r"\\(u[0-9A-Fa-f]{4}|.)", repl, text)


def read_scalar(lines, i):
    line = lines[i]
    _, _, raw = line.partition(":")
    raw = raw.strip()
    if raw.startswith('"'):
        body = raw[1:]
        while True:
            m = re.search(r'(?<!\\)"', body)
            if m:
                return decode_yaml_dq(body[: m.start()]), i + 1
            i += 1
            if i >= len(lines):
                return decode_yaml_dq(body), i
            body += " " + lines[i].strip()
    return raw, i + 1


SKIP_FIELDS = {"m_ObjectHideFlags", "m_CorrespondingSourceObject",
               "m_PrefabInstance", "m_PrefabAsset", "m_GameObject",
               "m_Enabled", "m_EditorHideFlags", "m_Script", "m_Name",
               "m_EditorClassIdentifier"}


def parse_component_fields(lines):
    """All simple serialized fields of a MonoBehaviour doc (2-space indent)."""
    fields = {}
    i = 0
    while i < len(lines):
        m = re.match(r"^  (\w+):", lines[i])
        if m and m.group(1) not in SKIP_FIELDS and not lines[i].startswith("   "):
            key = m.group(1)
            if key in ("checkCostEvent", "effectEvent", "response", "event"):
                i += 1
                continue
            val, i = read_scalar(lines, i)
            fields[key] = val
        else:
            i += 1
    return fields


def parse_calls(lines):
    """Parse m_Calls entries inside a doc; returns list of call dicts."""
    calls = []
    cur = None
    in_calls = False
    for ln in lines:
        if "m_Calls:" in ln:
            in_calls = True
            continue
        if not in_calls:
            continue
        m = re.match(r"\s*- m_Target: \{fileID: (-?\d+)", ln)
        if m:
            if cur and cur.get("method"):
                calls.append(cur)
            cur = {"targetFileID": m.group(1)}
            continue
        if cur is None:
            continue
        m = re.match(r"\s*m_TargetAssemblyTypeName: (.*)$", ln)
        if m:
            cur["type"] = m.group(1).strip().rstrip(",")
            continue
        m = re.match(r"\s*m_MethodName: (.*)$", ln)
        if m:
            cur["method"] = m.group(1).strip()
            continue
        m = re.match(r"\s*m_IntArgument: (-?\d+)", ln)
        if m:
            cur["int"] = int(m.group(1))
            continue
        m = re.match(r"\s*m_FloatArgument: (-?[\d.]+)", ln)
        if m and float(m.group(1)) != 0:
            cur["float"] = float(m.group(1))
            continue
        m = re.match(r"\s*m_StringArgument: (.+)$", ln)
        if m:
            cur["str"] = m.group(1).strip()
            continue
        m = re.match(r"\s*m_ObjectArgument: \{fileID: (\d+), guid: ([0-9a-f]{32})", ln)
        if m:
            cur["objGuid"] = m.group(2)
            continue
    if cur and cur.get("method"):
        calls.append(cur)
    return calls


def parse_prefab(path):
    with open(path, "r", encoding="utf-8", errors="replace") as f:
        text = f.read()
    docs = {}
    for m in re.finditer(r"^--- !u!(\d+) &(-?\d+)\n(.*?)(?=^--- !u!|\Z)",
                         text, flags=re.M | re.S):
        class_id, file_id, body = m.group(1), m.group(2), m.group(3)
        docs[file_id] = (class_id, body)

    go_names = {}       # fileID -> GameObject name
    comps = {}          # component fileID -> info
    for fid, (cid, body) in docs.items():
        lines = body.splitlines()
        if cid == "1":
            for ln in lines:
                m = re.match(r"\s*m_Name: (.*)$", ln)
                if m:
                    go_names[fid] = m.group(1).strip()
                    break
        elif cid == "114":
            script_guid = ""
            m = re.search(r"m_Script: \{fileID: \d+, guid: ([0-9a-f]{32})", body)
            if m:
                script_guid = m.group(1)
            go_fid = ""
            m = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", body)
            if m:
                go_fid = m.group(1)
            comps[fid] = {
                "scriptGuid": script_guid,
                "go": go_fid,
                "body": body,
                "lines": lines,
            }

    out = {"listeners": [], "containers": [], "components": {}}
    for fid, c in comps.items():
        body, lines = c["body"], c["lines"]
        go_name = go_names.get(c["go"], "?")
        is_card = c["scriptGuid"] == CARDSCRIPT_GUID
        has_check_cost = "\n  checkCostEvent:" in body
        has_event_field = re.search(r"\n  event: \{fileID: 11400000", body)
        if is_card:
            fields = parse_component_fields(lines)
            out["card"] = fields
        elif has_check_cost:
            # CostNEffectContainer: split checkCostEvent / effectEvent sections
            sec = {}
            for sec_name in ("checkCostEvent", "effectEvent"):
                m = re.search(r"\n  " + sec_name + r":\n(.*?)(?=\n  \w|\Z)",
                              body, flags=re.S)
                if m:
                    sec[sec_name] = parse_calls(m.group(1).splitlines())
            out["containers"].append({
                "fileID": fid, "go": go_name,
                "fields": parse_component_fields(lines),
                "checkCost": sec.get("checkCostEvent", []),
                "effect": sec.get("effectEvent", []),
            })
        elif has_event_field:
            m = re.search(r"\n  event: \{fileID: 11400000, guid: ([0-9a-f]{32})",
                          body)
            ev_guid = m.group(1) if m else ""
            m2 = re.search(r"\n  response:\n(.*?)(?=\n  \w|\Z)", body, flags=re.S)
            resp = parse_calls(m2.group(1).splitlines()) if m2 else []
            out["listeners"].append({
                "fileID": fid, "go": go_name, "eventGuid": ev_guid,
                "response": resp,
            })
        else:
            fields = parse_component_fields(lines)
            out["components"][fid] = {
                "scriptGuid": c["scriptGuid"], "go": go_name,
                "fields": fields,
            }
    return out


def resolve(calls, comps, guid_map, comp_types):
    res = []
    for call in calls:
        tgt = call.get("targetFileID", "")
        ctype = comp_types.get(tgt) or call.get("type", "?").split(",")[0]
        entry = {"on": ctype, "method": call.get("method", "?")}
        if "int" in call:
            entry["int"] = call["int"]
        if "float" in call:
            entry["float"] = call["float"]
        if "str" in call:
            entry["str"] = call["str"]
        if "objGuid" in call:
            entry["obj"] = guid_map.get(call["objGuid"], call["objGuid"])
        if tgt in comps:
            entry["targetGo"] = comps[tgt]["go"]
        res.append(entry)
    return res


def main():
    guid_map = build_guid_maps()
    result = []
    for dirpath, _dirs, files in os.walk(CARD_DIR):
        if "_DONT INCLUDE" in dirpath:
            continue
        for fn in sorted(files):
            if not fn.endswith(".prefab"):
                continue
            path = os.path.join(dirpath, fn)
            rel = os.path.relpath(path, CARD_DIR).replace("\\", "/")
            parsed = parse_prefab(path)
            comps = parsed["components"]
            # component type names from script guids
            comp_types = {}
            comp_dump = {}
            for fid, c in comps.items():
                tname = guid_map.get(c["scriptGuid"], c["scriptGuid"][:8])
                comp_types[fid] = tname
                comp_dump[fid] = {"type": tname, "go": c["go"],
                                  "fields": c["fields"]}
            entry = {
                "prefab": rel,
                "card": parsed.get("card", {}),
                "listeners": [
                    {"event": guid_map.get(l["eventGuid"], l["eventGuid"]),
                     "go": l["go"],
                     "response": resolve(l["response"], comps, guid_map,
                                         comp_types)}
                    for l in parsed["listeners"]
                ],
                "containers": [
                    {"go": ct["go"],
                     "checkCost": resolve(ct["checkCost"], comps, guid_map,
                                          comp_types),
                     "effect": resolve(ct["effect"], comps, guid_map,
                                       comp_types)}
                    for ct in parsed["containers"]
                ],
                "components": comp_dump,
            }
            result.append(entry)
    json.dump(result, sys.stdout, ensure_ascii=False, indent=1)


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
