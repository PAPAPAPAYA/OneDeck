# EXECUTED 2026-09-20 — DO NOT RERUN: every target prefab now has oncePerRound
# serialized, so a rerun aborts at the first file by design. Kept as the migration record.
#
# Second-wave per-round revive gates (user ruling 2026-09-20, after
# plan-revive-loop-mitigation-2026-09-19 §8): int charge counts — SPIRIT_CALLER/RIFT_REVIVER
# get 2, RELIC_CURSE_REVIVAL gets 3, ELITE/MASS/DUO get 1. For each prefab: insert
# `oncePerRound: N` into the single ReviveEffect MonoBehaviour (verified by script guid +
# host child GameObject fileID) between excludeSelf and roundEndReviveArmed, and replace the
# root CardScript cardDesc with the new wording (Unity backslash-u ASCII-escape style, single line).
# Strict assertions; aborts on any mismatch.
import re, sys

REVIVE_GUID = "5bddc72b2099c374583edd4d0a65f9b5"
BASE = "Assets/Prefabs/Cards/4.0/"

def esc(s):
    return "".join(c if 32 <= ord(c) < 127 else "\\u%04X" % ord(c) for c in s)

EDITS = [
    ("0_Common/SPIRIT_CALLER.prefab", "revive 1 noncreature", 2,
     "每回合两次,复活 <b>1</b> 友方现象"),
    ("0_Common/ELITE_REVIVER.prefab", "revive enhanced", 1,
     "攻击;每回合一次,复活 <b>1</b> 被强化过的友方实体"),
    ("1_Uncommon/RIFT_REVIVER.prefab", "exile 1 rift", 2,
     "放逐 <b>1</b> 友方信徒: 每回合两次,复活 <b>2</b> 友方现象"),
    ("1_Uncommon/RELIC_CURSE_REVIVAL.prefab", "enemy curse revealed revive 1", 3,
     "被动:敌方诅咒揭晓时,每回合三次,复活 <b>1</b> 友方"),
    ("2_Rare/MASS_REVIVER.prefab", "revive 3 common", 1,
     "每回合一次,复活 <b>3</b> 友方✦卡"),
    ("2_Rare/DUO_REVIVER.prefab", "revive 2 uncommon", 1,
     "每回合一次,复活 <b>2</b> 友方✦✦卡"),
]

def fail(msg):
    print("ABORT: " + msg)
    sys.exit(1)

for rel, child, charges, desc in EDITS:
    path = BASE + rel
    text = open(path, encoding="utf-8", newline="").read()
    if "\r\n" in text:
        fail(path + " has CRLF (expected LF Unity YAML)")
    if "oncePerRound" in text:
        fail(path + " already contains oncePerRound")

    # Split into YAML documents, keeping the '--- !u!X &id' header line.
    docs = re.split(r"(?m)^--- ", text)
    header_of = {}
    for i in range(1, len(docs)):
        m = re.match(r"!u!(\d+) &(\d+)", docs[i])
        if m:
            header_of[i] = (int(m.group(1)), m.group(2))

    # 1. Child GameObject fileID by exact m_Name line.
    child_ids = [fid for i, (cls, fid) in header_of.items()
                 if cls == 1 and re.search(r"(?m)^  m_Name: " + re.escape(child) + r"$", docs[i])]
    if len(child_ids) != 1:
        fail(path + ": child GameObject '" + child + "' matched " + str(len(child_ids)))
    child_fid = child_ids[0]

    # 2. The ReviveEffect MonoBehaviour on that GameObject.
    revive_docs = [i for i, (cls, fid) in header_of.items()
                   if cls == 114
                   and "m_GameObject: {fileID: " + child_fid + "}" in docs[i]
                   and REVIVE_GUID in docs[i]]
    if len(revive_docs) != 1:
        fail(path + ": ReviveEffect on '" + child + "' matched " + str(len(revive_docs)))
    bi = revive_docs[0]
    block = docs[bi]
    if block.count("excludeSelf: 1\n") != 1:
        fail(path + ": excludeSelf count != 1 in target block")
    if "roundEndReviveArmed: 0" not in block:
        fail(path + ": roundEndReviveArmed anchor missing in target block")
    block = block.replace("excludeSelf: 1\n", "excludeSelf: 1\n  oncePerRound: " + str(charges) + "\n", 1)
    docs[bi] = block

    # 3. cardDesc in the root CardScript block (holds cardTypeID).
    card_id = rel.split("/")[1].replace(".prefab", "")
    cs_docs = [i for i, (cls, fid) in header_of.items()
               if cls == 114 and "cardTypeID: " + card_id in docs[i] and "cardDesc:" in docs[i]]
    if len(cs_docs) != 1:
        fail(path + ": CardScript block matched " + str(len(cs_docs)))
    ci = cs_docs[0]
    block = docs[ci]
    new_block, n = re.subn(r'(?ms)^  cardDesc: ".*?"\n(?=  \w)',
                           lambda m: '  cardDesc: "' + esc(desc) + '"\n', block, count=1)
    if n != 1:
        fail(path + ": cardDesc replace count " + str(n))
    docs[ci] = new_block

    open(path, "w", encoding="utf-8", newline="").write("--- ".join(docs))
    print("OK " + rel + "  child=" + child + "  gate=" + str(charges) + ", desc updated")
print("ALL 6 PREFABS EDITED")
