import os
import re
import sys
from pathlib import Path
from collections import defaultdict

GUID_EFFECT_RESULT_STRING = "5a7c1c84ed1d98841916b9d65ac649dd"
GUID_BASE_DMG_REF = "fb52aef52820fa34883c2e79218e4ad7"
GUID_CARD_SCRIPT = "f47b4b127fc943869d9dbca8f00704e8"
GUID_COST_N_EFFECT = "a21da06ba55646f29c59d9dbf90834b3"
GUID_GAME_EVENT_LISTENER = "3cc6290dc6e64dadb7d801d93a3ba7a2"

BASE_DMG_VALUE = 2


def parse_prefab(path):
    with open(path, "r", encoding="utf-8") as f:
        content = f.read()

    pattern = re.compile(r"--- !u!(\d+) &(-?\d+)\n(.*?)(?=--- !u!|\Z)", re.DOTALL)

    game_objects = {}
    mono_behaviours = {}
    transforms = {}

    for m in pattern.finditer(content):
        type_id = m.group(1)
        file_id = int(m.group(2))
        obj_text = m.group(3).strip()

        if obj_text.startswith("GameObject:"):
            name_match = re.search(r"m_Name:\s*(.+)", obj_text)
            name = name_match.group(1).strip().strip('"') if name_match else ""
            comp_match = re.findall(r"component:\s*\{fileID:\s*(-?\d+)\}", obj_text)
            game_objects[file_id] = {
                "name": name,
                "components": [int(c) for c in comp_match],
            }
        elif obj_text.startswith("Transform:"):
            father_match = re.search(r"m_Father:\s*\{fileID:\s*(-?\d+)\}", obj_text)
            children_match = re.findall(r"- \{fileID:\s*(-?\d+)\}", obj_text)
            transforms[file_id] = {
                "father_file_id": int(father_match.group(1)) if father_match else 0,
                "children": [int(c) for c in children_match],
            }
        elif obj_text.startswith("MonoBehaviour:"):
            script_match = re.search(r"m_Script:\s*\{fileID:\s*\d+,\s*guid:\s*([a-f0-9-]+)", obj_text)
            guid = script_match.group(1) if script_match else ""
            fields = {}
            for line in obj_text.splitlines():
                kv = re.match(r"^(\s*)([a-zA-Z0-9_]+):\s*(.*)$", line)
                if kv:
                    indent = len(kv.group(1))
                    key = kv.group(2)
                    val = kv.group(3).strip()
                    if indent <= 2:
                        fields[key] = val
            mono_behaviours[file_id] = {
                "guid": guid,
                "fields": fields,
                "raw": obj_text,
            }

    return {
        "game_objects": game_objects,
        "mono_behaviours": mono_behaviours,
        "transforms": transforms,
    }


def extract_unityevent_calls(obj_text, event_name):
    calls = []
    lines = obj_text.splitlines()
    start_idx = None
    for i, line in enumerate(lines):
        if line.strip().startswith(f"{event_name}:"):
            start_idx = i
            break
    if start_idx is None:
        return calls

    event_indent = len(lines[start_idx]) - len(lines[start_idx].lstrip())

    # Collect lines belonging to this event until next field at same or lesser indent
    section_lines = []
    for j in range(start_idx + 1, len(lines)):
        line = lines[j]
        stripped = line.strip()
        if not stripped:
            section_lines.append(line)
            continue
        line_indent = len(line) - len(line.lstrip())
        # If we hit another field at same or lesser indent, stop
        if line_indent <= event_indent and not stripped.startswith("-"):
            break
        section_lines.append(line)

    section = "\n".join(section_lines)
    call_blocks = re.split(r"- m_Target:", section)[1:]
    for block in call_blocks:
        target = re.search(r"\{fileID:\s*(-?\d+)\}", block)
        method = re.search(r"m_MethodName:\s*(\S+)", block)
        assembly = re.search(r"m_TargetAssemblyTypeName:\s*(\S+)", block)
        mode = re.search(r"m_Mode:\s*(\d+)", block)
        int_arg = re.search(r"m_IntArgument:\s*(\d+)", block)
        string_arg = re.search(r"m_StringArgument:\s*(.*)", block)
        obj_arg = re.search(r"m_ObjectArgument:\s*\{fileID:\s*(-?\d+)(?:,\s*guid:\s*([a-f0-9-]+))?", block)
        calls.append({
            "target_fileid": int(target.group(1)) if target else 0,
            "method": method.group(1) if method else "",
            "assembly": assembly.group(1) if assembly else "",
            "mode": int(mode.group(1)) if mode else 0,
            "int_arg": int(int_arg.group(1)) if int_arg else 0,
            "string_arg": string_arg.group(1).strip() if string_arg else "",
            "obj_guid": obj_arg.group(2) if obj_arg and obj_arg.group(2) else "",
        })
    return calls


def check_prefab(path):
    issues = []
    data = parse_prefab(path)

    go = data["game_objects"]
    mb = data["mono_behaviours"]
    tr = data["transforms"]

    comp_to_go = {}
    for go_id, go_data in go.items():
        for comp_id in go_data["components"]:
            comp_to_go[comp_id] = go_id

    root_go_id = None
    root_transform_id = None
    for tr_id, tr_data in tr.items():
        if tr_data["father_file_id"] == 0:
            root_go_id = comp_to_go.get(tr_id)
            root_transform_id = tr_id
            break

    if root_go_id is None:
        issues.append("Cannot find root GameObject")
        return issues

    root_name = go[root_go_id]["name"]

    # Find CardScript
    card_script_id = None
    card_script_fields = {}
    for mb_id, mb_data in mb.items():
        if comp_to_go.get(mb_id) == root_go_id and mb_data["guid"] == GUID_CARD_SCRIPT:
            card_script_id = mb_id
            card_script_fields = mb_data["fields"]
            break

    if card_script_id is None:
        issues.append(f"Root GameObject '{root_name}' missing CardScript")
        return issues

    is_start_card = card_script_fields.get("isStartCard", "0") == "1"
    bury_cost = int(card_script_fields.get("buryCost", "0") or 0)
    delay_cost = int(card_script_fields.get("delayCost", "0") or 0)
    expose_cost = int(card_script_fields.get("exposeCost", "0") or 0)
    minion_cost_count = int(card_script_fields.get("minionCostCount", "0") or 0)
    price_ref = card_script_fields.get("price", "")

    if is_start_card:
        child_transforms = [tid for tid, tdata in tr.items() if tdata["father_file_id"] == root_transform_id]
        if child_transforms:
            issues.append("StartCard should not have child effect containers")
        return issues

    if not card_script_fields.get("cardTypeID", "").strip().strip('"'):
        issues.append("CardScript.cardTypeID is empty")

    if price_ref == "{fileID: 0}":
        issues.append("CardScript.price is not assigned")

    # Find all child CostNEffectContainers
    child_container_ids = []
    for mb_id, mb_data in mb.items():
        go_id = comp_to_go.get(mb_id)
        if go_id is None:
            continue
        go_tr_id = None
        for tid, tdata in tr.items():
            if comp_to_go.get(tid) == go_id:
                go_tr_id = tid
                break
        if go_tr_id and tr[go_tr_id]["father_file_id"] == root_transform_id and mb_data["guid"] == GUID_COST_N_EFFECT:
            child_container_ids.append(mb_id)

    if not child_container_ids:
        issues.append("No CostNEffectContainer children found")
        return issues

    # Find all GameEventListeners on root and collect all invoked target file IDs
    all_invoked_targets = set()
    listener_found = False
    for mb_id, mb_data in mb.items():
        if comp_to_go.get(mb_id) == root_go_id and mb_data["guid"] == GUID_GAME_EVENT_LISTENER:
            listener_found = True
            calls = extract_unityevent_calls(mb_data["raw"], "response")
            for call in calls:
                if call["method"] == "InvokeEffectEvent":
                    all_invoked_targets.add(call["target_fileid"])

    if not listener_found:
        issues.append("Root GameObject missing GameEventListener")
    else:
        # Every child container should be invoked by at least one listener
        for cid in child_container_ids:
            if cid not in all_invoked_targets:
                cname = go.get(comp_to_go.get(cid), {}).get("name", "unknown")
                issues.append(f"CostNEffectContainer '{cname}' is not invoked by any GameEventListener")

    # Check each child container
    has_bury_cost_pre = False
    has_delay_cost_pre = False
    has_expose_cost_pre = False
    has_minion_cost_pre = False

    for container_id in child_container_ids:
        container_data = mb[container_id]
        container_name = go.get(comp_to_go.get(container_id), {}).get("name", "unknown")
        raw = container_data["raw"]
        fields = container_data["fields"]

        effect_result = fields.get("effectResultString", "")
        if GUID_EFFECT_RESULT_STRING not in effect_result:
            issues.append(f"CostNEffectContainer '{container_name}' missing or wrong effectResultString")

        pre_calls = extract_unityevent_calls(raw, "preEffectEvent")
        effect_calls = extract_unityevent_calls(raw, "effectEvent")

        for call in pre_calls:
            if call["method"] == "ExecuteBuryCost":
                has_bury_cost_pre = True
            if call["method"] == "ExecuteDelayCost":
                has_delay_cost_pre = True
            if call["method"] == "ExecuteExposeCost":
                has_expose_cost_pre = True
            if call["method"] == "ExecuteMinionCost":
                has_minion_cost_pre = True

    # Cost consistency
    if bury_cost > 0 and not has_bury_cost_pre:
        issues.append(f"Card has buryCost={bury_cost} but no preEffectEvent calls BuryCostEffect.ExecuteBuryCost")
    if delay_cost > 0 and not has_delay_cost_pre:
        issues.append(f"Card has delayCost={delay_cost} but no preEffectEvent calls DelayCostEffect.ExecuteDelayCost")
    if expose_cost > 0 and not has_expose_cost_pre:
        issues.append(f"Card has exposeCost={expose_cost} but no preEffectEvent calls ExposeCostEffect.ExecuteExposeCost")
    if minion_cost_count > 0 and not has_minion_cost_pre:
        issues.append(f"Card has minionCostCount={minion_cost_count} but no preEffectEvent calls MinionCostEffect.ExecuteMinionCost")

    if bury_cost == 0 and has_bury_cost_pre:
        issues.append("Card has buryCost=0 but a preEffectEvent calls ExecuteBuryCost")
    if delay_cost == 0 and has_delay_cost_pre:
        issues.append("Card has delayCost=0 but a preEffectEvent calls ExecuteDelayCost")
    if expose_cost == 0 and has_expose_cost_pre:
        issues.append("Card has exposeCost=0 but a preEffectEvent calls ExecuteExposeCost")
    if minion_cost_count == 0 and has_minion_cost_pre:
        issues.append("Card has minionCostCount=0 but a preEffectEvent calls ExecuteMinionCost")

    return issues


def main():
    base_dir = Path("Assets/Prefabs/Cards/3.0 no cost (current)")
    prefabs = list(base_dir.rglob("*.prefab"))

    all_issues = []
    prefab_with_issues = 0
    clean_count = 0

    for p in sorted(prefabs):
        rel = p.relative_to("Assets/Prefabs/Cards")
        issues = check_prefab(str(p))
        if issues:
            prefab_with_issues += 1
            all_issues.append(f"\n[{rel}]")
            for issue in issues:
                all_issues.append(f"  - {issue}")
        else:
            clean_count += 1

    print(f"Checked {len(prefabs)} prefabs. {prefab_with_issues} have issues, {clean_count} are clean.")
    print("\n".join(all_issues))

    if prefab_with_issues > 0:
        sys.exit(1)


if __name__ == "__main__":
    main()
