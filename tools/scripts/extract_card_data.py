# Extract CardScript fields + effect call summary from OneDeck card prefabs.
# Read-only analysis tool. Outputs JSON to stdout.
import json
import os
import re
import sys

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
CARD_DIR = os.path.join(ROOT, "Assets", "Prefabs", "Cards", "3.0 no cost (current)")
CARDSCRIPT_GUID = "f47b4b127fc943869d9dbca8f00704e8"


def decode_yaml_dq(text):
    """Decode a YAML double-quoted scalar body (without surrounding quotes)."""
    def repl(m):
        esc = m.group(1)
        if esc.startswith("u"):
            return chr(int(esc[1:], 16))
        table = {"n": "\n", "t": "\t", "r": "\r", '"': '"', "\\": "\\", "0": "\0"}
        return table.get(esc, esc)
    return re.sub(r"\\(u[0-9A-Fa-f]{4}|.)", repl, text)


def read_scalar(lines, i):
    """Read field value starting at line i ('  field: value'). Handles folded
    double-quoted scalars spanning multiple lines. Returns (value, next_i)."""
    line = lines[i]
    _, _, raw = line.partition(":")
    raw = raw.strip()
    if raw.startswith('"'):
        # accumulate until closing unescaped quote
        body = raw[1:]
        while True:
            # find closing quote (quote not preceded by backslash)
            m = re.search(r'(?<!\\)"', body)
            if m:
                out = body[: m.start()]
                # YAML folding joins continuation lines with a space; the
                # trailing chunk after quote is ignored.
                return decode_yaml_dq(out), i + 1
            i += 1
            if i >= len(lines):
                return decode_yaml_dq(body), i
            body += " " + lines[i].strip()
    return raw, i + 1


def parse_cardscript(block_lines):
    fields = {}
    i = 0
    wanted = {"cardID", "cardTypeID", "displayName", "cardDesc", "rarity",
              "shopRollWeightMultiplier", "takeUpSpace", "isStartCard",
              "isMinion", "myTags", "myStatusEffects", "price"}
    while i < len(block_lines):
        line = block_lines[i]
        m = re.match(r"^  (\w+):", line)
        if m and m.group(1) in wanted:
            val, i = read_scalar(block_lines, i)
            fields[m.group(1)] = val
        else:
            i += 1
    return fields


def parse_prefab(path):
    with open(path, "r", encoding="utf-8", errors="replace") as f:
        text = f.read()
    docs = re.split(r"^--- !u!", text, flags=re.M)
    card = None
    go_names = []
    calls = []
    comp_types = []
    for doc in docs:
        lines = doc.splitlines()
        header = lines[0] if lines else ""
        if "m_Script: {fileID: 11500000, guid: " + CARDSCRIPT_GUID in doc:
            card = parse_cardscript(lines[1:])
        if header.startswith("1 &"):  # GameObject
            for ln in lines:
                m = re.match(r"\s*m_Name: (.*)$", ln)
                if m:
                    go_names.append(m.group(1).strip())
                    break
        # unity event calls
        if "m_MethodName:" in doc:
            cur = {}
            for ln in lines:
                m = re.match(r"\s*m_TargetAssemblyTypeName: (.*)$", ln)
                if m:
                    cur["type"] = m.group(1).strip().rstrip(",")
                m = re.match(r"\s*m_MethodName: (.*)$", ln)
                if m:
                    cur["method"] = m.group(1).strip()
                m = re.match(r"\s*m_IntArgument: (.*)$", ln)
                if m:
                    cur["int"] = m.group(1).strip()
                m = re.match(r"\s*m_StringArgument: (.*)$", ln)
                if m and m.group(1).strip():
                    cur["str"] = m.group(1).strip()
                m = re.match(r"\s*m_CallState: (.*)$", ln)
                if m and cur.get("method"):
                    calls.append(cur)
                    cur = {}
        m = re.search(r"m_EditorClassIdentifier: (\S.*)$", doc)
        if m and m.group(1).strip():
            comp_types.append(m.group(1).strip())
    # effect component field details: dump raw numeric fields of known effect scripts
    return card, go_names, calls, comp_types


def main():
    out = []
    for dirpath, _dirs, files in os.walk(CARD_DIR):
        if "_DONT INCLUDE" in dirpath:
            continue
        for fn in sorted(files):
            if not fn.endswith(".prefab"):
                continue
            path = os.path.join(dirpath, fn)
            rel = os.path.relpath(path, CARD_DIR).replace("\\", "/")
            card, go_names, calls, comp_types = parse_prefab(path)
            entry = {"prefab": rel}
            if card:
                entry.update(card)
            entry["childObjects"] = go_names[1:] if go_names else []
            entry["effectCalls"] = calls
            entry["componentTypes"] = comp_types
            out.append(entry)
    json.dump(out, sys.stdout, ensure_ascii=False, indent=1)


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    main()
