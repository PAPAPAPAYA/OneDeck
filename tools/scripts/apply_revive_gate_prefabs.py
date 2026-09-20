# EXECUTED 2026-09-20 (commit 027fdd0a) — DO NOT RERUN: every target prefab now has
# oncePerRound serialized, so a rerun aborts at the first file by design. NECROMANCER
# has additionally MOVED to 2_Rare/ after this script ran. Kept as the migration record.
#
# One-shot YAML gate insertion for plan-revive-loop-mitigation-2026-09-19 §8.3 (group 1).
# For each prefab: insert `oncePerRound: 1` into the ReviveEffect MonoBehaviour on the named
# child GameObject (verified by m_GameObject fileID + ReviveEffect script guid), replace the
# root CardScript cardDesc with the §8.3 wording (Unity \uXXXX ASCII style, single line),
# and for NECROMANCER also bump rarity 1 -> 2. Strict assertions; aborts on any mismatch.
import re, sys

REVIVE_GUID = "5bddc72b2099c374583edd4d0a65f9b5"
BASE = "Assets/Prefabs/Cards/4.0/"

def esc(s):
    return "".join(c if 32 <= ord(c) < 127 else "\\u%04X" % ord(c) for c in s)

EDITS = [
    ("0_Common/GRAVE_HEXER.prefab", "revive 1 friend",
     "强化 <b>1</b> 敌方诅咒;每回合一次,复活 <b>1</b> 友方", None),
    ("0_Common/BEAST_REVIVER.prefab", "revive 1 creature",
     "攻击;每回合一次,复活 <b>1</b> 友方实体", None),
    ("0_Common/RIFT_SHEPHERD.prefab", "revive 1 believer",
     "攻击;每回合一次,复活 <b>1</b> 张tag为[<tag:Believer>]的友方卡", None),
    ("1_Uncommon/KINGSLAYER.prefab", "revive 1 friend",
     "埋葬 <b>1</b> 攻击力最高敌方;每回合一次,复活 <b>1</b> 友方", None),
    ("1_Uncommon/REVIVE_SUMMONER.prefab", "revive 1",
     "生成 <b>1</b> 信徒;每回合一次,复活 <b>1</b> 友方", None),
    ("1_Uncommon/SOUL_TRADER.prefab", "revive 2",
     "埋葬 <b>1</b> 友方;每回合一次,复活 <b>2</b> 友方", None),
    ("1_Uncommon/CURSE_SUMMONER.prefab", "revive 1 friend",
     "复活 <b>1</b> 敌方诅咒;每回合一次,复活 <b>1</b> 友方", None),
    ("1_Uncommon/NECROMANCER.prefab", "revive 1 friendly",
     "苏醒:复活 <b>1</b> 友方;每回合一次,复活 <b>1</b> 友方", "2"),
]

def fail(msg):
    print("ABORT: " + msg)
    sys.exit(1)

for rel, child, desc, rarity in EDITS:
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

    # 2. ReviveEffect MonoBehaviour on that GameObject.
    revive_docs = [i for i, (cls, fid) in header_of.items()
                   if cls == 114
                   and "m_GameObject: {fileID: " + child_fid + "}" in docs[i]
                   and REVIVE_GUID in docs[i]]
    if len(revive_docs) != 1:
        fail(path + ": ReviveEffect on '" + child + "' matched " + str(len(revive_docs)))
    bi = revive_docs[0]
    block = docs[bi]
    if block.count("excludeSelf: 1") != 1:
        fail(path + ": excludeSelf count != 1 in target block")
    block = block.replace("excludeSelf: 1\n", "excludeSelf: 1\n  oncePerRound: 1\n", 1)
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
    if rarity is not None:
        new_block, n2 = re.subn(r"(?m)^  rarity: 1$", "  rarity: " + rarity, new_block, count=1)
        if n2 != 1:
            fail(path + ": rarity replace count " + str(n2))
    docs[ci] = new_block

    open(path, "w", encoding="utf-8", newline="").write("--- ".join(docs))
    print("OK " + rel + "  child=" + child + "  gated, desc+rarity updated")
print("ALL 8 PREFABS EDITED")
