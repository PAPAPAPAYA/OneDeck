# -*- coding: utf-8 -*-
"""Phase-3 attack-attribute prefab migration:
- JU_ON: power-based self-damage -> AttackEffect.AttackSelf (own attack)
- THE_FOOL: stage most-status-effect -> stage max attack (位置谓词)
- POWER_TRANSFER: random consume/give -> max-attack consume + min-attack give
- BONE_COMBINATION / BODY_CANON: dynamic segments raise the attack event; extraDmg
  legacy offset (-1, tied to the removed BaseDmgRef=2 constant) zeroed so each
  segment deals the card's printed attack
- ALL_FOR_ONE / FLESH_COMBINATION: pure attack = Y stat cards (design decision) —
  reveal-damage machinery removed, AttackResolverSource added, desc rewritten
- ALMIGHTY: 3-term AttackResolverSource added (grave + friendly rifts + enemy negative)
"""
import io
import os

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
POOL = os.path.join(ROOT, "Assets", "Prefabs", "Cards", "3.0 no cost (current)")
RESOLVER_GUID = "8f0a3c2b9d1e4f5a6b7c8d9e0f1a2b3c"


def esc(s):
    """Unity YAML string escaping: non-ASCII chars -> \\uXXXX, ASCII stays literal."""
    out = []
    for ch in s:
        if ord(ch) > 127:
            out.append("\\u%04x" % ord(ch))
        else:
            out.append(ch)
    return "".join(out)


def load(path):
    with io.open(path, "r", encoding="utf-8", newline="") as f:
        text = f.read()
    crlf = "\r\n" in text
    text = text.replace("\r\n", "\n")
    return text, crlf


def save(path, text, crlf):
    if crlf:
        text = text.replace("\n", "\r\n")
    with io.open(path, "w", encoding="utf-8", newline="") as f:
        f.write(text)


def replace_once(text, old, new, where):
    count = text.count(old)
    assert count == 1, "%s: expected 1 occurrence of %r, found %d" % (where, old, count)
    return text.replace(old, new)


def replace_count(text, old, new, where, expected):
    count = text.count(old)
    assert count == expected, "%s: expected %d occurrence(s) of %r, found %d" % (where, expected, old, count)
    return text.replace(old, new)


def main():
    report = []

    # ---------- JU_ON: power self-damage -> attack self-damage ----------
    p = os.path.join(POOL, "_DONT INCLUDE", "Token", "JU_ON.prefab")
    text, crlf = load(p)
    text = replace_once(text, 'm_Name: deal dmg to self based on power', 'm_Name: attack self with own attack', p)
    text = replace_once(text, 'cardDesc: "\\u63ed\\u6653\\u65f6:\\u5bf9\\u81ea\\u5df1\\u9020\\u6210[\\u529b\\u91cf]\\u5c42\\u6570\\u7684\\u4f24\\u5bb3"',
                        'cardDesc: "' + esc("揭晓时:对自身造成[攻击力]点伤害") + '"', p)
    # HPAlterEffect -> AttackEffect on the self-damage component
    text = replace_once(text, 'm_Script: {fileID: 11500000, guid: 3d68556e3bb3ce54bb4153a1a476a087, type: 3}',
                        'm_Script: {fileID: 11500000, guid: 9f01314777625ad4bb0bc22828e9728c, type: 3}', p)
    text = replace_once(text, 'm_EditorClassIdentifier: Assembly-CSharp::HPAlterEffect',
                        'm_EditorClassIdentifier: Assembly-CSharp::AttackEffect', p)
    text = replace_once(text, 'baseDmg: {fileID: 11400000, guid: fb52aef52820fa34883c2e79218e4ad7, type: 2}',
                        'baseDmg: {fileID: 0}', p)
    text = replace_once(text, 'extraDmg: -2', 'extraDmg: 0', p)
    text = replace_once(text, 'm_TargetAssemblyTypeName: HPAlterEffect, Assembly-CSharp',
                        'm_TargetAssemblyTypeName: AttackEffect, Assembly-CSharp', p)
    text = replace_once(text, 'm_MethodName: DecreaseMyHp', 'm_MethodName: AttackSelf', p)
    save(p, text, crlf)
    report.append("JU_ON: power self-damage -> AttackSelf (own attack)")

    # ---------- THE_FOOL: stage card with max attack ----------
    p = os.path.join(POOL, "General", "0_Common", "THE_FOOL.prefab")
    text, crlf = load(p)
    text = replace_once(text, 'm_MethodName: StageCardWithMostStatusEffect',
                        'm_MethodName: StageCardWithMaxAttack', p)
    save(p, text, crlf)
    report.append("THE_FOOL: StageCardWithMostStatusEffect -> StageCardWithMaxAttack")

    # ---------- POWER_TRANSFER: max-attack consume + min-attack give ----------
    p = os.path.join(POOL, "General", "1_Uncommon", "POWER_TRANSFER.prefab")
    text, crlf = load(p)
    text = replace_once(text, 'm_Name: consume hostile power', 'm_Name: consume enemy max attack', p)
    text = replace_once(text, 'm_Name: give friendly power', 'm_Name: give friendly min attack', p)
    text = replace_once(text, 'm_MethodName: ConsumeRandomEnemyCardsAttack',
                        'm_MethodName: ConsumeEnemyCardWithMaxAttack', p)
    text = replace_once(text, 'm_MethodName: GiveAttack',
                        'm_MethodName: GiveFriendlyCardWithMinAttack', p)
    # The two legacy int arguments (amount 2 each) become 1 (design v5: 1 attack each way).
    text = replace_count(text, 'm_IntArgument: 2', 'm_IntArgument: 1', p, 2)
    text = replace_once(text, 'cardDesc: "' + esc("揭晓时:去除 <b>2</b> 敌方 <b>1</b> 攻击力,给予友方 <b>1</b> 攻击力 <b>2</b> 次") + '"',
                        'cardDesc: "' + esc("揭晓时:去除 1 张敌方[攻击者](最高攻击力)1 攻击力:给予 1 张友方[攻击者](最低攻击力)1 攻击力") + '"', p)
    save(p, text, crlf)
    report.append("POWER_TRANSFER: predicate-based 1:1 transfer (max consume / min give)")

    # ---------- BONE_COMBINATION: dynamic segments + attack event ----------
    p = os.path.join(POOL, "General", "1_Uncommon", "BONE_COMBINATION.prefab")
    text, crlf = load(p)
    text = replace_once(text, 'm_MethodName: DecreaseTheirHpTimes_BasedOnOpponentBuriedCount',
                        'm_MethodName: AttackTimesBasedOnOpponentBuriedCount', p)
    text = replace_once(text, 'extraDmg: -1', 'extraDmg: 0', p)
    save(p, text, crlf)
    report.append("BONE_COMBINATION: AttackTimesBasedOnOpponentBuriedCount + extraDmg 0 (was -1)")

    # ---------- BODY_CANON: dynamic segments + attack event ----------
    p = os.path.join(POOL, "Bury and buried", "Bury", "2_Rare", "BODY_CANON.prefab")
    text, crlf = load(p)
    text = replace_once(text, 'm_MethodName: DecreaseTheirHpTimes_BasedOnIntSO',
                        'm_MethodName: AttackTimesBasedOnIntSO', p)
    text = replace_once(text, 'extraDmg: -1', 'extraDmg: 0', p)
    save(p, text, crlf)
    report.append("BODY_CANON: AttackTimesBasedOnIntSO + extraDmg 0 (was -1)")

    # ---------- ALL_FOR_ONE: pure attack = friendly total (no reveal damage) ----------
    p = os.path.join(POOL, "General", "1_Uncommon", "ALL_FOR_ONE.prefab")
    text, crlf = load(p)
    text, crlf = add_resolver(text, crlf, p, "ALL_FOR_ONE",
                              resolver_file_id="6600112233445566771",
                              terms=[("0", "")],
                              root_go="8718476725744197808",
                              child_go="2635718925342889943",
                              child_components=("4178670049436012962", "88236208953752898", "5316563494723346686"),
                              listener_call_file_id="88236208953752898")
    text = replace_once(text, 'cardDesc: "' + esc("揭晓时:造成所有卡攻击力总和的伤害") + '"',
                        'cardDesc: "' + esc("攻击力 = 所有友方卡攻击力总和(常态结算)") + '"', p)
    save(p, text, crlf)
    report.append("ALL_FOR_ONE: pure attack = friendly total stat card (damage removed, resolver added)")

    # ---------- FLESH_COMBINATION: pure attack = friendly count ----------
    p = os.path.join(POOL, "General", "1_Uncommon", "FLESH_COMBINATION.prefab")
    text, crlf = load(p)
    text, crlf = add_resolver(text, crlf, p, "FLESH_COMBINATION",
                              resolver_file_id="6600112233445566772",
                              terms=[("1", "")],
                              root_go="8718476725744197808",
                              child_go="270621688628183383",
                              child_components=("834418638395040871", "3408854323514059765", "1359544507321340075"),
                              listener_call_file_id="3408854323514059765")
    text = replace_once(text, 'cardDesc: "' + esc("揭晓时:造成友方数量的伤害") + '"',
                        'cardDesc: "' + esc("攻击力 = 友方卡牌数量(常态结算)") + '"', p)
    save(p, text, crlf)
    report.append("FLESH_COMBINATION: pure attack = friendly count stat card (damage removed, resolver added)")

    # ---------- ALMIGHTY: 3-term resolver (grave + friendly rifts + enemy negative) ----------
    p = os.path.join(POOL, "General", "2_Rare", "ALMIGHTY.prefab")
    text, crlf = load(p)
    text, crlf = add_resolver(text, crlf, p, "ALMIGHTY",
                              resolver_file_id="6600112233445566773",
                              terms=[("2", ""), ("3", "RIFT"), ("4", "JU_ON")],
                              root_go="8718476725744197808",
                              child_go=None,
                              child_components=(),
                              listener_call_file_id=None)
    text = replace_once(text, 'cardDesc: "' + esc("揭晓 <b>2<counter></b> 次:攻击,置顶 <b>1</b> 友方,埋葬 <b>1</b> 敌方,给予 <b>1</b> 友方攻击力,生成 <b>1</b> [次元裂缝],增强 <b>1</b> 敌方[诅咒]") + '"',
                        'cardDesc: "' + esc("揭晓 <b>2<counter></b> 次:攻击,攻击力 = 墓地友方卡数 + 友方[次元裂缝]数 + 敌方[负面]攻击力总和(常态结算),置顶 <b>1</b> 友方,埋葬 <b>1</b> 敌方,给予 <b>1</b> 友方攻击力,生成 <b>1</b> [次元裂缝],增强 <b>1</b> 敌方[诅咒]") + '"', p)
    save(p, text, crlf)
    report.append("ALMIGHTY: 3-term AttackResolverSource (grave + rifts + enemy negative)")

    print("\n".join(report))


def add_resolver(text, crlf, path, card_id, resolver_file_id, terms, root_go,
                 child_go, child_components, listener_call_file_id):
    """Add an AttackResolverSource component to the root GameObject of a prefab.
    Optionally removes the reveal-damage child GameObject and empties the root
    listener's call (used for ALL_FOR_ONE / FLESH_COMBINATION)."""
    if child_go is not None:
        # Cut the child GO block: from its GO header to the root GO header.
        start_marker = "--- !u!1 &%s\n" % child_go
        end_marker = "--- !u!1 &%s\n" % root_go
        start = text.index(start_marker)
        end = text.index(end_marker)
        text = text[:start] + text[end:]

        # Remove the child transform from the root's m_Children.
        text = replace_once(text, "  m_Children:\n  - {fileID: %s}\n" % child_components[0],
                            "  m_Children: []\n", path)

        # Empty the root listener call that pointed at the removed container.
        call_block = ("  response:\n    m_PersistentCalls:\n      m_Calls:\n"
                      "      - m_Target: {fileID: %s}\n" % listener_call_file_id +
                      "        m_TargetAssemblyTypeName: CostNEffectContainer, Assembly-CSharp\n"
                      "        m_MethodName: InvokeEffectEventVoid\n"
                      "        m_Mode: 1\n"
                      "        m_Arguments:\n"
                      "          m_ObjectArgument: {fileID: 0}\n"
                      "          m_ObjectArgumentAssemblyTypeName: UnityEngine.Object, UnityEngine\n"
                      "          m_IntArgument: 0\n"
                      "          m_FloatArgument: 0\n"
                      "          m_StringArgument: \n"
                      "          m_BoolArgument: 0\n"
                      "        m_CallState: 2\n")
        assert call_block in text, "%s: listener call block not found" % path
        text = text.replace(call_block, "  response:\n    m_PersistentCalls:\n      m_Calls: []\n")

    # Register the resolver component on the root GameObject.
    marker = "  - component: {fileID: -4728943355192809547}\n"
    assert marker in text, "%s: root component list marker not found" % path
    text = text.replace(marker, marker + "  - component: {fileID: %s}\n" % resolver_file_id, 1)

    # Append the resolver MonoBehaviour block at the end of the prefab file.
    terms_yaml = ""
    for source, type_id in terms:
        terms_yaml += "  - source: %s\n    cardTypeID: %s\n" % (source, type_id)
    block = (
        "--- !u!114 &%s\n" % resolver_file_id +
        "MonoBehaviour:\n"
        "  m_ObjectHideFlags: 0\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n"
        "  m_GameObject: {fileID: %s}\n" % root_go +
        "  m_Enabled: 1\n"
        "  m_EditorHideFlags: 0\n"
        "  m_Script: {fileID: 11500000, guid: %s, type: 3}\n" % RESOLVER_GUID +
        "  m_Name: \n"
        "  m_EditorClassIdentifier: \n"
        "  terms:\n" + terms_yaml)
    text = text.rstrip("\n") + "\n" + block + "\n"
    return text, crlf


if __name__ == "__main__":
    main()
