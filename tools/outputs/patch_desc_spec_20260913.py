# -*- coding: utf-8 -*-
# Mirror the 2026-09-13 desc-convention change into docs/4.0_CardDesc_Spec.md (CRLF preserved).
import io

PATH = r"D:\Unity Projects\OneDeck\docs\4.0_CardDesc_Spec.md"
with io.open(PATH, "r", encoding="utf-8", newline="") as fh:
    text = fh.read()

def rep(old, new):
    global text
    assert old in text, "MISS: " + old[:60]
    assert text.count(old) == 1, "DUP: " + old[:60]
    text = text.replace(old, new)

# 1. grammar template
rep("[trigger][：][cost,]effect-clause[；effect-clause]*",
    "[trigger][：][cost:]effect-clause[；effect-clause]*")

# 2. cost bullet
rep("- A cost/gate clause leads, separated by 「，」: `放逐1友方信徒，埋葬2敌方非生物`\r\n"
    "  (if the cost cannot be paid, later clauses do not run).",
    "- A cost/gate clause leads, separated by 「：」: `放逐1友方信徒：复活2友方非生物`\r\n"
    "  (if the cost cannot be paid, later clauses do not run; separator switched from 「，」 to\r\n"
    "  「：」 on 2026-09-13 — see Decisions 2026-09-13. The actual desc text uses a half-width\r\n"
    "  `: ` — full-width punctuation has no glyphs in the card fonts).")

# 3. rewrite example row
rep("| 放逐1友方信徒：埋葬2敌方非生物 | 放逐1友方信徒，埋葬2敌方非生物 (cost uses 「，」) |",
    "| 放逐1友方信徒，埋葬2敌方非生物 (old 「，」 cost notation) | 放逐1友方信徒：埋葬2敌方非生物 (cost uses 「：」, changed 2026-09-13) |")

# 4. new decisions section
tail = ("    RIFT_HATCHERY −苏醒 (desc lacks the clause; if the Unity final state is the\r\n"
        "    revive-gated dormancy engine, the DESC needs 苏醒, not the tag). 27 cards updated\r\n"
        "    and verified via notion-fetch.\r\n")
assert text.endswith(tail), "tail mismatch"
text += (
    "\r\n## Decisions (ratified 2026-09-13)\r\n\r\n"
    "1. Cost/gate clause separator switched to 「：」: when the first of two effect clauses is a\r\n"
    "   cost/gate (unsatisfiable → the rest does not run), write `放逐1友方信徒：复活2友方非生物`;\r\n"
    "   plain sequential clauses keep 「；」. Supersedes the 2026-08-26 \"cost uses 「，」\" ruling.\r\n"
    "   First card landed on the new notation: RIFT_REVIVER (以人易物) desc is\r\n"
    "   `放逐 <b>1</b> 友方信徒: 复活 <b>2</b> 友方非生物`; its prefab merged the two containers into\r\n"
    "   one CostNEffectContainer with `CheckCost_HasOwnCardOfType(1)` (targetCardTypeID =\r\n"
    "   RiftCardTypeID) — when the cost fails, neither the exile nor the revive executes.\r\n"
    "2. Implementation rule: the cost clause and the effect clause must live in the SAME\r\n"
    "   CostNEffectContainer (effectEvent ordered [cost effect, effect]; the check bound on\r\n"
    "   checkCostEvent). Do NOT split them into two containers — a split container lets the\r\n"
    "   follow-up effect run even when the cost clause fizzles. Already implemented this way:\r\n"
    "   3.0 RIFT_GUIDE / RIFT_MONSTER / RIFT_SUMMONER, DR_MANHATTAN, PREMATURE (拔苗助长),\r\n"
    "   咒食的大召唤师, CURSE_THIRST_SUMMONER_OLD (咒食的召唤师), DOWNED_FIGHTER (倒地的战士),\r\n"
    "   GOLEM (巨人), ADVANCE_PORTAL (高等传送门), QUICK_RESPONSE_PROTOCOL (快速响应协议),\r\n"
    "   全能人.\r\n")

with io.open(PATH, "w", encoding="utf-8", newline="") as fh:
    fh.write(text)
print("patched OK")
