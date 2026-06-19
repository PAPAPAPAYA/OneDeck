import os
import re
import glob
from collections import defaultdict

ROOT = r"Assets/Prefabs/Cards/3.0 no cost (current)"

STATUS_EFFECTS = {
    0: "None",
    1: "Infected",
    2: "Mana",
    3: "HeartChanged",
    4: "Power",
    5: "Rest",
    6: "Revive",
    7: "Counter",
}

TARGET_TYPES = {
    0: "Me",
    1: "Them",
    2: "Random",
}

INT_STATUS_METHODS = {
    "GiveSelfStatusEffect",
    "GiveStatusEffect",
    "GiveAllFriendlyStatusEffect",
    "GiveStatusEffectToXFriendly_BasedOnStaged",
}

NOARG_STATUS_METHODS = {
    "GiveStatusEffectToLastXCards": "statusEffectLayerCount",
    "GiveStatusEffectToXFriendly": "yFriendlyLayerCount",
}

DYNAMIC_STATUS_METHODS = {
    "GiveStatusEffectBasedOnStatusEffectCount",
    "GiveSelfStatusEffectBasedOnStatusEffectCount",
}

# Subclasses of StatusEffectGiverEffect that apply status effects directly
EXTRA_STATUS_COMPONENTS = {
    "PowerReactionEffect": ("powerAmount", "Power"),
    "StatusEffectAmplifierEffect": ("statusEffectMultiplier", None),  # effect from statusEffectToGive
}

CURSE_INT_METHODS = {
    "EnhanceCurse",
    "EnhanceFriendlyCurse",
}

CURSE_DYNAMIC_METHODS = {
    "EnhanceCurse_BasedOnIntSO",
    "EnhanceCurseWithCoefficient",
}


def parse_blocks(text):
    """Split YAML into MonoBehaviour blocks keyed by fileID."""
    blocks = {}
    pattern = re.compile(r"--- !u!114 &(-?\d+)\nMonoBehaviour:\n(.*?)(?=\n--- !u!|\Z)", re.DOTALL)
    for fid, body in pattern.findall(text):
        blocks[fid] = body
    return blocks


def get_field(body, name):
    m = re.search(r"^  " + re.escape(name) + r": (.*)$", body, re.MULTILINE)
    if not m:
        return None
    val = m.group(1).strip()
    # strip trailing comments? not needed
    return val


def parse_int(body, name, default=0):
    v = get_field(body, name)
    if v is None:
        return default
    try:
        return int(v)
    except ValueError:
        return default


def parse_bool(body, name, default=False):
    v = get_field(body, name)
    if v is None:
        return default
    return v == "1"


def class_name(body):
    ident = get_field(body, "m_EditorClassIdentifier") or ""
    if "::" in ident:
        name = ident.split("::")[-1]
        return name.split(".")[-1]
    return ""


def parse_calls(text):
    """Return list of persistent call dicts."""
    calls = []
    # Each call is a yaml mapping starting with - m_Target
    call_pattern = re.compile(
        r"- m_Target: \{fileID: (\d+)\}\n"
        r"        m_TargetAssemblyTypeName: (.*?)\n"
        r"        m_MethodName: ([^\n]+?)\n"
        r"        m_Mode: (\d+)\n"
        r"        m_Arguments:.*?"
        r"          m_IntArgument: (\d+).*?"
        r"        m_CallState:",
        re.DOTALL,
    )
    for m in call_pattern.finditer(text):
        calls.append({
            "target_fid": m.group(1),
            "assembly_type": m.group(2).strip().replace("\n", " ").replace("  ", " "),
            "method": m.group(3).strip(),
            "mode": int(m.group(4)),
            "int_arg": int(m.group(5)),
        })
    return calls


def root_gameobject_name(text):
    """Find the top-level GameObject (Transform.m_Father fileID 0) and return its m_Name."""
    gameobjects = {}
    for m in re.finditer(r"--- !u!1 &(-?\d+)\nGameObject:\n(.*?)(?=\n--- !u!|\Z)", text, re.DOTALL):
        fid, body = m.group(1), m.group(2)
        name_match = re.search(r"^  m_Name: (.*)$", body, re.MULTILINE)
        if name_match:
            gameobjects[fid] = name_match.group(1).strip().strip('"')

    root_go = None
    for m in re.finditer(r"--- !u!4 &(-?\d+)\nTransform:\n(.*?)(?=\n--- !u!|\Z)", text, re.DOTALL):
        body = m.group(2)
        father_match = re.search(r"^  m_Father: \{fileID: (-?\d+)\}", body, re.MULTILINE)
        go_match = re.search(r"^  m_GameObject: \{fileID: (-?\d+)\}", body, re.MULTILINE)
        if father_match and father_match.group(1) == "0" and go_match:
            root_go = go_match.group(1)
            break

    if root_go and root_go in gameobjects:
        return gameobjects[root_go]
    # fallback: first GameObject name
    if gameobjects:
        return next(iter(gameobjects.values()))
    return ""


def analyze_prefab(path):
    with open(path, "r", encoding="utf-8") as f:
        text = f.read()

    card_name = root_gameobject_name(text)
    blocks = parse_blocks(text)
    calls = parse_calls(text)

    results = []

    # Build component map by fileID -> class name for target resolution
    comp_class = {}
    for fid, body in blocks.items():
        comp_class[fid] = class_name(body)

    for call in calls:
        method = call["method"]
        mode = call["mode"]
        int_arg = call["int_arg"]
        target_fid = call["target_fid"]
        target_class = comp_class.get(target_fid, "")

        # StatusEffectGiverEffect direct int methods
        if target_class == "StatusEffectGiverEffect" and method in INT_STATUS_METHODS:
            if mode == 3 and int_arg > 1:
                body = blocks.get(target_fid, "")
                effect = STATUS_EFFECTS.get(parse_int(body, "statusEffectToGive"), "?")
                results.append({
                    "card": card_name,
                    "path": path,
                    "type": "StatusEffectGiverEffect",
                    "method": method,
                    "effect": effect,
                    "detail": f"int argument = {int_arg} layer(s) per target / total",
                })

        # StatusEffectGiverEffect no-arg methods relying on configured layer count
        if target_class == "StatusEffectGiverEffect" and method in NOARG_STATUS_METHODS:
            body = blocks.get(target_fid, "")
            field = NOARG_STATUS_METHODS[method]
            layer_count = parse_int(body, field)
            if layer_count > 1:
                effect = STATUS_EFFECTS.get(parse_int(body, "statusEffectToGive"), "?")
                results.append({
                    "card": card_name,
                    "path": path,
                    "type": "StatusEffectGiverEffect",
                    "method": method,
                    "effect": effect,
                    "detail": f"{field} = {layer_count} layer(s) per selected card",
                })

        # Dynamic status methods (amount depends on game state)
        if target_class == "StatusEffectGiverEffect" and method in DYNAMIC_STATUS_METHODS:
            body = blocks.get(target_fid, "")
            effect = STATUS_EFFECTS.get(parse_int(body, "statusEffectToGive"), "?")
            counted = STATUS_EFFECTS.get(parse_int(body, "statusEffectToCount"), "?")
            results.append({
                "card": card_name,
                "path": path,
                "type": "StatusEffectGiverEffect",
                "method": method,
                "effect": effect,
                "detail": f"dynamic layers (equals count of {counted} on self)",
            })

        # CurseEffect direct int methods -> always Power
        if target_class == "CurseEffect" and method in CURSE_INT_METHODS:
            if mode == 3 and int_arg > 1:
                results.append({
                    "card": card_name,
                    "path": path,
                    "type": "CurseEffect",
                    "method": method,
                    "effect": "Power",
                    "detail": f"int argument = {int_arg} Power layer(s)",
                })

        # CurseEffect dynamic methods -> can grant multiple Power layers
        if target_class == "CurseEffect" and method in CURSE_DYNAMIC_METHODS:
            results.append({
                "card": card_name,
                "path": path,
                "type": "CurseEffect",
                "method": method,
                "effect": "Power",
                "detail": "dynamic Power layers (based on IntSO/coefficient)",
            })

    # Direct subclass components that apply status effects with multi-layer fields
    for fid, body in blocks.items():
        cls = class_name(body)
        if cls in EXTRA_STATUS_COMPONENTS:
            field_name, fixed_effect = EXTRA_STATUS_COMPONENTS[cls]
            field_val = parse_int(body, field_name)
            if field_val > 1:
                effect = fixed_effect
                if effect is None:
                    effect = STATUS_EFFECTS.get(parse_int(body, "statusEffectToGive"), "?")
                # Only count if the component is actually invoked by an event
                invoked = any(
                    c["target_fid"] == fid and c["method"] in {
                        "GivePowerToCardThatGotPower",
                        "AmplifyStatusEffectGain",
                    }
                    for c in calls
                )
                if invoked:
                    results.append({
                        "card": card_name,
                        "path": path,
                        "type": cls,
                        "method": "(event-driven)",
                        "effect": effect,
                        "detail": f"{field_name} = {field_val} layer(s)",
                    })

    return results


def main():
    pattern = os.path.join(ROOT, "**", "*.prefab")
    files = glob.glob(pattern, recursive=True)
    all_results = []
    for f in files:
        all_results.extend(analyze_prefab(f))

    if not all_results:
        print("No cards found that grant multi-layer status effects.")
        return

    # group by path
    grouped = defaultdict(list)
    for r in all_results:
        rel = os.path.relpath(r["path"], ROOT)
        grouped[rel].append(r)

    print(f"Found {len(all_results)} multi-layer status-effect grant(s) across {len(grouped)} card(s):\n")
    for rel in sorted(grouped.keys()):
        rs = grouped[rel]
        card_name = rs[0]["card"]
        print(f"[{card_name}]  ({rel})")
        for r in rs:
            print(f"  - {r['type']}.{r['method']} -> {r['effect']}  ({r['detail']})")
        print()


if __name__ == "__main__":
    main()
