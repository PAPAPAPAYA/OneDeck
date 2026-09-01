# -*- coding: utf-8 -*-
"""Apply 2026-09-01 user-decided drift fixes to 4.0 prefabs.

1. printedAttack: 5 cards -> DB ATK value (user: ATK drift follows DB)
2. cardDesc terminology: [次元裂缝]->[信徒] (8 cards), 被强化->强化反应 (4 cards)
3. Create RELIC_ATTACK_BURIAL.prefab (clone of RELIC_CHAIN_BURIAL, listens onAnyFriendlyCardAttacked)
"""
import re, os, json, uuid

ROOT = os.path.join("Assets", "Prefabs", "Cards", "4.0")

ATK_FIXES = {
    "SPIKE_SKELETON_4.0": 1,  # was 2
    "GRAVE_FIST": 3,          # was 4
    "GRAVE_TOGETHER_4.0": 1,  # was 2
    "SNOWBALL": 1,            # was 2
    "SOLDIER_SKELETON_4.0": 1,  # was 2
}

RIFT_RENAME = [
    "RELIC_HIVE", "REVIVE_SUMMONER", "RIFT_HATCHERY", "RIFT_INSECT_4.0",
    "RIFT_MEDIUM", "RIFT_PRIEST", "RIFT_SHEPHERD", "RIFT_STRIKER",
]
POWER_REACTION_RENAME = ["COMBO_STARTER", "UNDYING_WARRIOR", "SNOWBALL", "WEAPON_SPIRIT"]

def decode(s):
    if s.startswith('"') and s.endswith('"'):
        s = s[1:-1]
    s = re.sub(r"\\u([0-9A-Fa-f]{4})", lambda m: chr(int(m.group(1), 16)), s)
    return s

def encode(s):
    return s.encode("unicode_escape").decode("ascii").replace("\\\\", "\\")

def cardscript_block(text):
    blocks = re.split(r"^--- !u!114 &", text, flags=re.M)
    for b in blocks:
        if re.search(r"^  rarity:", b, re.M):
            return b
    return None

def field_of(block, name):
    m = re.search(r"^  " + name + r": (.*?)(?=^  \w|^--- )", block, re.M | re.S)
    return m.group(1).strip() if m else ""

# ---------- 1 & 2: patch existing prefabs ----------
changed = []
for dirpath, dirs, files in os.walk(ROOT):
    for f in sorted(files):
        if not f.endswith(".prefab"):
            continue
        path = os.path.join(dirpath, f)
        text = open(path, encoding="utf-8").read()
        block = cardscript_block(text)
        if block is None:
            print("SKIP (no CardScript block):", f)
            continue
        cid = field_of(block, "cardTypeID")
        if cid in ATK_FIXES or cid in RIFT_RENAME or cid in POWER_REACTION_RENAME:
            new_text = text
            edits = []
            blk_start = new_text.index(block)
            blk_end = blk_start + len(block)
            region = new_text[blk_start:blk_end]
            # printedAttack fix
            if cid in ATK_FIXES:
                old = "printedAttack: %d" % (ATK_FIXES[cid] + 1)
                new = "printedAttack: %d" % ATK_FIXES[cid]
                if old in region:
                    region = region.replace(old, new, 1)
                    edits.append("ATK %s -> %s" % (old, new))
            # cardDesc terminology fix
            m = re.search(r"^  cardDesc: (.*?)(?=^  \w|^--- )", block, re.M | re.S)
            if m:
                desc_raw = m.group(1).strip()
                desc = decode(desc_raw)
                new_desc = desc
                if cid in RIFT_RENAME and "[次元裂缝]" in new_desc:
                    new_desc = new_desc.replace("[次元裂缝]", "[信徒]")
                    edits.append("次元裂缝->信徒")
                if cid in POWER_REACTION_RENAME:
                    if cid == "WEAPON_SPIRIT":
                        if "被强化时" in new_desc:
                            new_desc = new_desc.replace("友方生物被强化时", "友方生物触发强化反应时")
                            edits.append("被强化->强化反应(WEAPON_SPIRIT)")
                    elif "被强化" in new_desc:
                        new_desc = new_desc.replace("被强化", "强化反应")
                        edits.append("被强化->强化反应")
                if new_desc != desc:
                    region = re.sub(
                        r"^  cardDesc: .*?(?=^  \w|^--- )",
                        lambda m: "  cardDesc: \"%s\"\n" % encode(new_desc),
                        region, count=1, flags=re.M | re.S)
            if edits:
                new_text = new_text[:blk_start] + region + new_text[blk_end:]
                open(path, "w", encoding="utf-8", newline="\n").write(new_text)
                changed.append((cid, edits))

for cid, edits in changed:
    print("PATCHED %-24s %s" % (cid, "; ".join(edits)))

# ---------- 3: create RELIC_ATTACK_BURIAL from RELIC_CHAIN_BURIAL template ----------
SRC = os.path.join(ROOT, "1_Uncommon", "RELIC_CHAIN_BURIAL.prefab")
DST = os.path.join(ROOT, "1_Uncommon", "RELIC_ATTACK_BURIAL.prefab")
FRIENDLY_ATTACK_EVENT_GUID = "dbf36e9ae6c27ff43a27b1e01d59eda6"  # onAnyFriendlyCardAttacked
BURIED_EVENT_GUID = "93242982157fc464c8e3ea5e9aa64154"  # OnFriendlyCardBuried (template)

text = open(SRC, encoding="utf-8").read()
new = text
new = new.replace("cardTypeID: RELIC_CHAIN_BURIAL", "cardTypeID: RELIC_ATTACK_BURIAL")
new = new.replace("m_Name: friendly buried bury top 1", "m_Name: friendly attack bury top 1")
new = re.sub(
    r"^  cardDesc: .*?(?=^  \w|^--- )",
    lambda m: "  cardDesc: \"%s\"\n" % encode("被动:友方每次攻击时,埋葬卡组顶 <b>1</b> 卡"),
    new, count=1, flags=re.M | re.S)
assert BURIED_EVENT_GUID in new
new = new.replace(BURIED_EVENT_GUID, FRIENDLY_ATTACK_EVENT_GUID, 1)
open(DST, "w", encoding="utf-8", newline="\n").write(new)
print("CREATED", DST)

# meta: clone template meta with a fresh guid
new_guid = uuid.uuid4().hex
meta = open(SRC + ".meta", encoding="utf-8").read()
meta = re.sub(r"^guid: [0-9a-f]{32}$", "guid: " + new_guid, meta, count=1, flags=re.M)
open(DST + ".meta", "w", encoding="utf-8", newline="\n").write(meta)
print("CREATED", DST + ".meta", "guid:", new_guid)
