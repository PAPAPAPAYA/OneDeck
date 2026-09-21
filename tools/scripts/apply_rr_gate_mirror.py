# One-shot 2026-09-21: wire RIFT_REVIVER's gate mirror (plan-revive-gate-mirror-cost-2026-09-21).
# On the "exile 1 rift" child of RIFT_REVIVER.prefab:
#   1. set the CostNEffectContainer's gateSource -> the sibling ReviveEffect component;
#   2. append a second persistent call CheckCost_ReviveGateOpen to checkCostEvent.
# Strict assertions; aborts on any mismatch. Same fileID+GUID dual-location style as
# apply_revive_gate_prefabs*.py.
import re, sys

CONTAINER_GUID = "a21da06ba55646f29c59d9dbf90834b3"  # CostNEffectContainer.cs.meta
REVIVE_GUID = "5bddc72b2099c374583edd4d0a65f9b5"     # ReviveEffect.cs.meta
PATH = "Assets/Prefabs/Cards/4.0/1_Uncommon/RIFT_REVIVER.prefab"
CHILD = "exile 1 rift"

NEW_CALL_TAIL = (
    "      - m_Target: {fileID: %s}\n"
    "        m_TargetAssemblyTypeName: CostNEffectContainer, Assembly-CSharp\n"
    "        m_MethodName: CheckCost_ReviveGateOpen\n"
    "        m_Mode: 1\n"
    "        m_Arguments:\n"
    "          m_ObjectArgument: {fileID: 0}\n"
    "          m_ObjectArgumentAssemblyTypeName: \n"
    "          m_IntArgument: 0\n"
    "          m_FloatArgument: 0\n"
    "          m_StringArgument: \n"
    "          m_BoolArgument: 0\n"
    "        m_CallState: 2\n"
)

def fail(msg):
    print("ABORT: " + msg)
    sys.exit(1)

text = open(PATH, encoding="utf-8", newline="").read()
if "\r\n" in text:
    fail(PATH + " has CRLF (expected LF Unity YAML)")
if "gateSource" in text:
    fail(PATH + " already contains gateSource")
if "CheckCost_ReviveGateOpen" in text:
    fail(PATH + " already contains CheckCost_ReviveGateOpen")

docs = re.split(r"(?m)^--- ", text)
header_of = {}
for i in range(1, len(docs)):
    m = re.match(r"!u!(\d+) &(\d+)", docs[i])
    if m:
        header_of[i] = (int(m.group(1)), m.group(2))

# 1. Child GameObject fileID by exact m_Name line.
child_ids = [fid for i, (cls, fid) in header_of.items()
             if cls == 1 and re.search(r"(?m)^  m_Name: " + re.escape(CHILD) + r"$", docs[i])]
if len(child_ids) != 1:
    fail("child GameObject '" + CHILD + "' matched " + str(len(child_ids)))
child_fid = child_ids[0]

# 2. ReviveEffect component on that child.
revive_entries = [(i, fid) for i, (cls, fid) in header_of.items()
                  if cls == 114
                  and "m_GameObject: {fileID: " + child_fid + "}" in docs[i]
                  and REVIVE_GUID in docs[i]]
if len(revive_entries) != 1:
    fail("ReviveEffect on '" + CHILD + "' matched " + str(len(revive_entries)))
revive_fid = revive_entries[0][1]

# 3. CostNEffectContainer component on that child.
container_entries = [(i, fid) for i, (cls, fid) in header_of.items()
                     if cls == 114
                     and "m_GameObject: {fileID: " + child_fid + "}" in docs[i]
                     and CONTAINER_GUID in docs[i]]
if len(container_entries) != 1:
    fail("CostNEffectContainer on '" + CHILD + "' matched " + str(len(container_entries)))
ci, container_fid = container_entries[0]
block = docs[ci]

# Sanity: RR's existing cost check must be present exactly once.
if block.count("m_MethodName: CheckCost_HasOwnCardOfType") != 1:
    fail("expected exactly one CheckCost_HasOwnCardOfType call in container block")

# 4. gateSource reference appended at the end of the container block.
if not block.endswith("\n"):
    fail("container block does not end with newline")
block = block + "  gateSource: {fileID: " + revive_fid + "}\n"

# 5. Second persistent call appended into checkCostEvent's m_Calls.
anchor = "        m_CallState: 2\n  effectEvent:"
if block.count(anchor) != 1:
    fail("checkCostEvent tail anchor matched " + str(block.count(anchor)))
block = block.replace(anchor, "        m_CallState: 2\n" + (NEW_CALL_TAIL % container_fid) + "  effectEvent:", 1)

docs[ci] = block
open(PATH, "w", encoding="utf-8", newline="").write("--- ".join(docs))
print("OK RIFT_REVIVER: gateSource -> " + revive_fid + ", CheckCost_ReviveGateOpen call appended")
