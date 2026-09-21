# EXECUTED 2026-09-21 — DO NOT RERUN: regenerating would overwrite any later hand-edit
# to the specimen assets. Kept as the generation record.
#
# Synthetic positive-control specimen for the infinity-detection pipeline
# (plans/plan-infinity-specimen-synthetic-2026-09-21.md §2, option 2B): a test-only
# card pair that keeps forming a real same-round revive loop after every production
# revive gate shipped. Two copies of TEST_LOOP_HUB (OnMeRevealed -> ReviveMyCards(1),
# ungated, no creature filter) pull each other out of the grave forever — the loop
# the 14 pipeline tests need as their "true positive" input, with zero coupling to
# the production pool.
#
# Outputs (both with fresh uuid4 .meta guids):
#   Assets/Prefabs/Cards/4.0/-1_Test/TEST_LOOP_HUB.prefab   (text clone of SPIRIT_CALLER)
#   Assets/SORefs/Decks/test decks/chain tests/4.0/test infinite loop.asset
#     (DeckSO, deck = 2 x TEST_LOOP_HUB, the Start Card comes from the test side)
#
# Prefab deltas vs the SPIRIT_CALLER template:
#   root m_Name + cardTypeID -> TEST_LOOP_HUB
#   displayName -> 测试枢纽, cardDesc -> 测试用无闸复活枢纽
#   ReviveEffect.creatureFilter 2 (noncreature) -> 0 (Any)
#   oncePerRound line removed (0 = unlimited, the whole point of the specimen)
#   excludeSelf stays 1: it only excludes the source instance, so the two copies
#   still revive each other.
# Strict assertions; aborts on any mismatch.
import os, re, sys, uuid

TEMPLATE = "Assets/Prefabs/Cards/4.0/0_Common/SPIRIT_CALLER.prefab"
PREFAB_OUT = "Assets/Prefabs/Cards/4.0/-1_Test/TEST_LOOP_HUB.prefab"
DECK_DIR = "Assets/SORefs/Decks/test decks/chain tests/4.0"
DECK_OUT = os.path.join(DECK_DIR, "test infinite loop.asset")

DECK_SO_GUID = "b6efbe0d49340fc4b911481940925fb9"  # DeckSO script guid
ROOT_GO_FILEID = "4234701159843460202"           # SPIRIT_CALLER root GameObject fileID

DISPLAY_NAME = "测试枢纽"
CARD_DESC = "测试用无闸复活枢纽"


def esc(s):
    return "".join(c if 32 <= ord(c) < 127 else "\\u%04X" % ord(c) for c in s)


def fail(msg):
    print("ABORT: " + msg)
    sys.exit(1)


for out in (PREFAB_OUT, PREFAB_OUT + ".meta", DECK_OUT, DECK_OUT + ".meta"):
    if os.path.exists(out):
        fail(out + " already exists — DO NOT RERUN (see header)")

text = open(TEMPLATE, encoding="utf-8", newline="").read()
if "\r\n" in text:
    fail(TEMPLATE + " has CRLF (expected LF Unity YAML)")

# 1. Root GameObject name.
if text.count("m_Name: SPIRIT_CALLER\n") != 1:
    fail("m_Name: SPIRIT_CALLER anchor count " + str(text.count("m_Name: SPIRIT_CALLER\n")))
text = text.replace("m_Name: SPIRIT_CALLER\n", "m_Name: TEST_LOOP_HUB\n", 1)

# 2. CardScript identity + wording (raw strings: the file holds literal \uNNNN escapes).
if text.count("cardTypeID: SPIRIT_CALLER\n") != 1:
    fail("cardTypeID anchor count " + str(text.count("cardTypeID: SPIRIT_CALLER\n")))
text = text.replace("cardTypeID: SPIRIT_CALLER\n", "cardTypeID: TEST_LOOP_HUB\n", 1)

old_display = '  displayName: "\\u964D\\u7075\\u4F1A"\n'
if text.count(old_display) != 1:
    fail("displayName anchor count " + str(text.count(old_display)))
text = text.replace(old_display, '  displayName: "' + esc(DISPLAY_NAME) + '"\n', 1)

m = re.search(r'(?m)^  cardDesc: ".*?"\n', text)
if m is None:
    fail("cardDesc line not found")
text = text.replace(m.group(0), '  cardDesc: "' + esc(CARD_DESC) + '"\n', 1)

# 3. ReviveEffect: any-creature revive, no per-round gate.
if text.count("  creatureFilter: 2\n") != 1:
    fail("creatureFilter anchor count " + str(text.count("  creatureFilter: 2\n")))
text = text.replace("  creatureFilter: 2\n", "  creatureFilter: 0\n", 1)
if text.count("  oncePerRound: 2\n") != 1:
    fail("oncePerRound anchor count " + str(text.count("  oncePerRound: 2\n")))
text = text.replace("  oncePerRound: 2\n", "", 1)
if "excludeSelf: 1" not in text:
    fail("excludeSelf anchor missing")

prefab_guid = uuid.uuid4().hex
deck_guid = uuid.uuid4().hex

open(PREFAB_OUT, "w", encoding="utf-8", newline="").write(text)
open(PREFAB_OUT + ".meta", "w", encoding="utf-8", newline="").write(
    "fileFormatVersion: 2\n"
    "guid: %s\n"
    "PrefabImporter:\n"
    "  externalObjects: {}\n"
    "  userData: \n"
    "  assetBundleName: \n"
    "  assetBundleVariant: \n" % prefab_guid)

deck = (
    "%YAML 1.1\n"
    "%TAG !u! tag:unity3d.com,2011:\n"
    "--- !u!114 &11400000\n"
    "MonoBehaviour:\n"
    "  m_ObjectHideFlags: 0\n"
    "  m_CorrespondingSourceObject: {fileID: 0}\n"
    "  m_PrefabInstance: {fileID: 0}\n"
    "  m_PrefabAsset: {fileID: 0}\n"
    "  m_GameObject: {fileID: 0}\n"
    "  m_Enabled: 1\n"
    "  m_EditorHideFlags: 0\n"
    f"  m_Script: {{fileID: 11500000, guid: {DECK_SO_GUID}, type: 3}}\n"
    "  m_Name: test infinite loop\n"
    "  m_EditorClassIdentifier: \n"
    "  deck:\n"
    f"  - {{fileID: {ROOT_GO_FILEID}, guid: {prefab_guid}, type: 3}}\n"
    f"  - {{fileID: {ROOT_GO_FILEID}, guid: {prefab_guid}, type: 3}}\n"
    "  defaultDeck: {fileID: 0}\n"
    "  resetOnStart: 0\n"
    "  description: two TEST_LOOP_HUB copies revive each other - minimal same-round loop\n"
)

open(DECK_OUT, "w", encoding="utf-8", newline="").write(deck)
open(DECK_OUT + ".meta", "w", encoding="utf-8", newline="").write(
    "fileFormatVersion: 2\n"
    f"guid: {deck_guid}\n"
    "NativeFormatImporter:\n"
    "  externalObjects: {}\n"
    "  mainObjectFileID: 11400000\n"
    "  userData: \n"
    "  assetBundleName: \n"
    "  assetBundleVariant: \n"
)

print("OK prefab guid=%s" % prefab_guid)
print("OK deck   guid=%s  (deck = 2 x TEST_LOOP_HUB)" % deck_guid)
