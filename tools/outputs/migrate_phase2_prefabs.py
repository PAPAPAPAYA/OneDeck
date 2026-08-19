# -*- coding: utf-8 -*-
"""Phase-2 prefab migration script.

Rules per card:
1. HPAlterEffect component -> AttackEffect (damage source becomes the attack attribute).
2. DecreaseTheirHp bindings -> Attack (single segment); DecreaseTheirHpTimesX(n) ->
   Attack + extraAttackTimes = n-1 (multi-action cards use AttackTimes(1) for the 1-segment action).
3. printedAttack / extraAttackTimes inserted into CardScript from the design pool values.
4. cardDesc: <dmg> damage phrases -> attack keywords; 力量 -> 攻击力.
5. Power components (StatusEffectGiverEffect/StatusEffectAmplifierEffect/PowerReactionEffect)
   -> attack counterparts; transfer/consume/check-cost methods renamed; event assets swapped.
"""
import io
import json
import re
import os
import glob

POOL = {c['id']: c for c in json.load(open('tools/outputs/_pool_final.json', encoding='utf-8'))}

HP_ALTER = '3d68556e3bb3ce54bb4153a1a476a087'
ATTACK_EFFECT = '9f01314777625ad4bb0bc22828e9728c'
GIVER = '190c5b71c5f9492db489365cb0734f9b'
ATTACK_GIVER = '6ed2d574f8860134d91897187ad70437'
AMPLIFIER = '283f32d8bf0dc73499e93ee581a2e654'
POWER_REACTION = 'bc6889e6c567d804ab804fe4840a1fb0'
ATTACK_REACTION = 'ad05c62daec75f44daa23684e98c7f6e'

EVT_FRIENDLY_POWER = 'f82fc4ec5b8a0c14eb626a5a0d4336ff'
EVT_FRIENDLY_ATK = '197fbf2543e09b84899ac569a3b85f87'
EVT_ME_STATUS = '1ad4399e33473264eada636d43124e08'
EVT_ME_ATK = 'ce262bc70356d0d41b924fee428719d1'

CARD_SCRIPT_GUID = 'f47b4b127fc943869d9dbca8f00704e8'

# Cards whose damage comes from the attack attribute (component swap + binding rename).
# value = (printedAttack, extraAttackTimes)
ATTACK_CARDS = {
    'GRAVE_PUNCH': (1, 1),
    'CORPSE_CANON': (1, 0),
    'GRAVE_INVITATION': (1, 0),
    'BODY_CANON': (1, 0),
    'SOLDIER_SKELETON': (1, 0),
    'AVENGER': (1, 0),
    'CURSED_CORPSE': (1, 2),
    'SCAPEGOAT': (1, 0),
    'SPIKE_SKELETON': (1, 1),
    'GRAVE_KEEPER': (1, 0),
    'SLIME': (1, 0),
    'RIFT_INSECT': (1, 0),
    'RIFT_DRAGON': (4, 0),
    'RIFT_MONSTER': (1, 0),
    'RIFT_DEVOURER': (1, 0),
    'POISONER': (1, 0),
    'CURSE_THIRST_BEAST': (1, 0),
    'SMALL_SCALE_DEATH': (1, 0),
    'BLACKSMITH': (1, 0),
    'COFFIN_MAKER': (1, 0),
    'THE_FOOL': (4, 0),
    'BONE_COMBINATION': (1, 0),
    'GOBLIN_ASSASSIN_TEAM': (1, 0),
    'GOBLIN_CHARGE_TEAM': (1, 0),
    'POWER_CRAVER': (1, 0),
    'POWER_SURGE': (1, 0),
    'SNATCHER': (1, 0),
    'TACTICAL_BREACHER': (1, 0),
    'ALMIGHTY': (1, 0),
    'ETERNAL_GHOST': (1, 0),
    'POWER_SIPHONER': (1, 1),
    'UNFINISHED_ROBOT': (1, 0),
    'ALL_FOR_ONE': (0, 0),
    'FLESH_COMBINATION': (0, 0),
}

# DecreaseTheirHpTimesX(n) cards: binding -> Attack (reads GetAttackTimes via extraAttackTimes).
TIMES_X_TO_ATTACK = {'GRAVE_PUNCH', 'CURSED_CORPSE', 'SPIKE_SKELETON', 'POWER_SIPHONER'}

# Cards whose 1-segment action must stay exactly 1 segment (AttackTimes(1)):
# SPIKE_SKELETON reveal action coexists with its x2 buried action.
SINGLE_SEGMENT_ACTIONS = {'SPIKE_SKELETON'}

# Per-file binding method renames (old -> new)
METHOD_RENAMES = {
    'GiveSelfStatusEffect': 'GiveSelfAttack',
    'GiveStatusEffect': 'GiveAttack',
    'GiveAllFriendlyStatusEffect': 'GiveAllFriendlyAttack',
    'GiveStatusEffectToLastXCards': 'GiveAttackToLastXCards',
    'GiveStatusEffectToXFriendly': 'GiveAttackToXFriendly',
    'GiveStatusEffectToXFriendly_BasedOnIntSO': 'GiveAttackToXFriendly_BasedOnIntSO',
    'GiveStatusEffectToXFriendly_BasedOnStaged': 'GiveAttackToXFriendly_BasedOnStaged',
    'GiveSelfStatusEffectBasedOnStatusEffectCount': 'DoubleOwnAttack',
    'AmplifyStatusEffectGain': 'AmplifyAttackGain',
    'GivePowerToCardThatGotPower': 'GiveAttackToCardThatGainedAttack',
    'TransferAllStatusEffectToHostileCurse': 'TransferAllAttackToHostileCurse',
    'TransferOneStatusEffectToSelf': 'TransferOneAttackToSelf',
    'ConsumeHostileCursePower': 'ConsumeEnemyCurseAttack',
    'ConsumeRandomEnemyCardsStatusEffect': 'ConsumeRandomEnemyCardsAttack',
    'CheckCost_EnemyCursedCardHasPower': 'CheckCost_EnemyCurseCardHasAttack',
    'CheckCost_Power': 'CheckCost_OwnAttack',
}

# Per-file extra method renames (only for specific cards)
EXTRA_RENAMES = {
    'DR_MANHATTAN': {'ConsumeOwnStatusEffect': 'ConsumeOwnAttack'},
}

# Component guid swaps: old guid -> new guid + class identifier suffix
COMPONENT_SWAPS = {
    HP_ALTER: ('ATTACK_EFFECT', ATTACK_EFFECT, 'AttackEffect'),
    GIVER: ('ATTACK_GIVER', ATTACK_GIVER, 'DefaultNamespace.Effects.AttackGiverEffect'),
    AMPLIFIER: ('ATTACK_REACTION', ATTACK_REACTION, 'DefaultNamespace.Effects.AttackGainReactionEffect'),
    POWER_REACTION: ('ATTACK_REACTION', ATTACK_REACTION, 'DefaultNamespace.Effects.AttackGainReactionEffect'),
}

# Field renames inside swapped components (old field name -> new field name)
FIELD_RENAMES = {
    'powerAmount': 'attackAmount',
    'statusEffectMultiplier': 'attackMultiplier',
}

# Event asset swaps in GameEventListener (old guid -> new guid)
EVENT_SWAPS = {
    EVT_FRIENDLY_POWER: EVT_FRIENDLY_ATK,
    EVT_ME_STATUS: EVT_ME_ATK,
}


def decode_desc(raw):
    try:
        return raw.encode('utf-8').decode('unicode_escape')
    except Exception:
        return raw


def encode_desc(text):
    return text.encode('unicode_escape').decode('ascii')


def desc_patterns():
    return [
        ('\u9020\u6210 <b><dmg></b> \u4f24\u5bb3 x <b>2</b>', '\u653b\u51fb \u00d7<b>2</b>'),  # 造成 <b><dmg></b> 伤害 x <b>2</b> -> 攻击 ×<b>2</b>
        ('\u9020\u6210 <b><dmg></b> \u4f24\u5bb3 x <b>3</b>', '\u653b\u51fb \u00d7<b>3</b>'),
        ('\u9020\u6210 <b><dmg></b> \u4f24\u5bb3 x \u672c\u56de\u5408\u88ab\u57cb\u846c\u7684\u654c\u65b9\u6570\u91cf',
         '\u653b\u51fb \u00d7\u672c\u56de\u5408\u88ab\u57cb\u846c\u7684\u654c\u65b9\u6570\u91cf'),
        ('\u9020\u6210 <b>1</b> \u6b21 <b><dmg></b> \u4f24\u5bb3', '\u653b\u51fb'),
        ('\u9020\u6210 <b><dmg:staged></b> \u4f24\u5bb3', '\u653b\u51fb'),
        ('\u9020\u6210 <b><dmg></b> x <b>3</b> \u4f24\u5bb3', '\u653b\u51fb \u00d7<b>3</b>'),
        ('\u9020\u6210 <b><dmg></b> \u4f24\u5bb3', '\u653b\u51fb'),
        ('\u9020\u6210 <b>4</b> \u4f24\u5bb3', '\u653b\u51fb'),
        ('\u9020\u6210\u6240\u6709\u5361\u7684\u529b\u91cf\u6570\u91cf\u7684\u4f24\u5bb3',
         '\u9020\u6210\u6240\u6709\u5361\u653b\u51fb\u529b\u603b\u548c\u7684\u4f24\u5bb3'),
        ('\u529b\u91cf', '\u653b\u51fb\u529b'),
    ]


def find_prefab(card_id):
    for p in glob.glob('Assets/Prefabs/Cards/3.0 no cost (current)/**/*.prefab', recursive=True):
        if '_DONT INCLUDE' in p:
            continue
        txt = open(p, encoding='utf-8', errors='replace').read()
        m = re.search(r'cardTypeID: ([A-Z0-9_]+)', txt)
        if m and m.group(1) == card_id:
            return p
    return None


def apply_file(card_id, txt, desc_pairs=None):
    changes = []

    # 1. Component guid swaps (HPAlterEffect -> AttackEffect etc.)
    for old_guid, (name, new_guid, cls) in COMPONENT_SWAPS.items():
        old_ref = 'm_Script: {fileID: 11500000, guid: %s, type: 3}' % old_guid
        new_ref = 'm_Script: {fileID: 11500000, guid: %s, type: 3}' % new_guid
        n = txt.count(old_ref)
        if n:
            txt = txt.replace(old_ref, new_ref)
            changes.append('%s guid x%d' % (name, n))
            # class identifier
            old_ci = 'm_EditorClassIdentifier: Assembly-CSharp::HPAlterEffect'
            if old_guid == HP_ALTER and old_ci in txt:
                txt = txt.replace(old_ci, 'm_EditorClassIdentifier: Assembly-CSharp::AttackEffect')
                changes.append('class id HPAlterEffect')
            elif old_guid == GIVER:
                txt = txt.replace('m_EditorClassIdentifier: Assembly-CSharp::DefaultNamespace.Effects.StatusEffectGiverEffect',
                                  'm_EditorClassIdentifier: Assembly-CSharp::DefaultNamespace.Effects.AttackGiverEffect')
                changes.append('class id StatusEffectGiverEffect')
            elif old_guid == AMPLIFIER:
                txt = txt.replace('m_EditorClassIdentifier: Assembly-CSharp::DefaultNamespace.Effects.StatusEffectAmplifierEffect',
                                  'm_EditorClassIdentifier: Assembly-CSharp::DefaultNamespace.Effects.AttackGainReactionEffect')
                changes.append('class id StatusEffectAmplifierEffect')
            elif old_guid == POWER_REACTION:
                txt = txt.replace('m_EditorClassIdentifier: Assembly-CSharp::DefaultNamespace.Effects.PowerReactionEffect',
                                  'm_EditorClassIdentifier: Assembly-CSharp::DefaultNamespace.Effects.AttackGainReactionEffect')
                changes.append('class id PowerReactionEffect')

    # 2. Binding class names in m_TargetAssemblyTypeName (cross-line tolerant)
    type_renames = {
        'DefaultNamespace.Effects.StatusEffectGiverEffect': 'DefaultNamespace.Effects.AttackGiverEffect',
        'DefaultNamespace.Effects.StatusEffectAmplifierEffect': 'DefaultNamespace.Effects.AttackGainReactionEffect',
        'DefaultNamespace.Effects.PowerReactionEffect': 'DefaultNamespace.Effects.AttackGainReactionEffect',
        'HPAlterEffect': 'AttackEffect',
    }
    for old, new in type_renames.items():
        if old in txt:
            txt = txt.replace(old, new)
            changes.append('type rename %s -> %s' % (old.split('.')[-1], new.split('.')[-1]))

    # 3. Binding method renames (global)
    for old, new in METHOD_RENAMES.items():
        if old in txt:
            txt = txt.replace(old, new)
            changes.append('method %s -> %s' % (old, new))

    # 4. Per-card method renames
    for cid, renames in EXTRA_RENAMES.items():
        if card_id == cid:
            for old, new in renames.items():
                if old in txt:
                    txt = txt.replace(old, new)
                    changes.append('method %s -> %s' % (old, new))

    # 5. DecreaseTheirHpTimesX -> Attack for multi-segment cards
    if card_id in TIMES_X_TO_ATTACK:
        if 'DecreaseTheirHpTimesX' in txt:
            txt = txt.replace('DecreaseTheirHpTimesX', 'Attack')
            changes.append('DecreaseTheirHpTimesX -> Attack')
        # the 1-segment reveal action of SPIKE_SKELETON becomes AttackTimes(1)
        if card_id in SINGLE_SEGMENT_ACTIONS:
            # first exact DecreaseTheirHp occurrence -> AttackTimes; int arg 0 -> 1
            idx = txt.find('DecreaseTheirHp')
            if idx >= 0:
                txt = txt[:idx] + 'AttackTimes' + txt[idx + len('DecreaseTheirHp'):]
                # set m_IntArgument right after this binding to 1
                m = re.search(r'(m_MethodName: AttackTimes\n\s+m_Mode: \d+\n\s+m_Arguments:\n\s+m_ObjectArgument: \{fileID: \d+\}\n\s+m_ObjectArgumentAssemblyTypeName: [^\n]+\n\s+m_IntArgument: )\d+', txt)
                if m:
                    txt = txt[:m.start(1)] + m.group(1) + '1' + txt[m.end():]
                    changes.append('AttackTimes int arg -> 1')
    else:
        # exact DecreaseTheirHp (followed by newline, i.e. not a _BasedOn/Times/From variant) -> Attack
        new_txt, n = re.subn(r'DecreaseTheirHp(?=\n)', 'Attack', txt)
        if n:
            txt = new_txt
            changes.append('DecreaseTheirHp -> Attack x%d' % n)

    # 6. Field renames in swapped components
    for old, new in FIELD_RENAMES.items():
        if old in txt:
            txt = txt.replace(old, new)
            changes.append('field %s -> %s' % (old, new))

    # 7. Event asset swaps
    for old, new in EVENT_SWAPS.items():
        old_ref = 'guid: %s, type: 2' % old
        if old_ref in txt:
            txt = txt.replace(old_ref, 'guid: %s, type: 2' % new)
            changes.append('event swap')

    # 8. printedAttack / extraAttackTimes on CardScript
    atk, extra = ATTACK_CARDS.get(card_id, (None, None))
    if atk is not None and 'printedAttack:' not in txt:
        marker = '  myTags: \n'
        if marker in txt:
            insert = ('  printedAttack: %d\n'
                      '  attackGrowth: 0\n'
                      '  attackModThisRound: 0\n'
                      '  extraAttackTimes: %d\n') % (atk, extra)
            txt = txt.replace(marker, marker + insert, 1)
            changes.append('printedAttack=%d extraAttackTimes=%d' % (atk, extra))
        else:
            changes.append('WARN no myTags marker!')

    # 9. cardDesc replacement (multi-line YAML folded strings supported)
    if desc_pairs:
        desc_m = re.search(r'cardDesc: "((?:[^"\\]|\\.|\n    )*)"', txt)
        if desc_m:
            raw = desc_m.group(1)
            # fold YAML continuation lines (physical newline + 4 spaces) into a space
            folded = raw.replace('\n    ', ' ')
            decoded = decode_desc(folded)
            for old, new in desc_pairs:
                decoded = decoded.replace(old, new)
            reencoded = encode_desc(decoded)
            if reencoded != raw:
                txt = txt.replace('cardDesc: "%s"' % raw, 'cardDesc: "%s"' % reencoded, 1)
                changes.append('desc updated')

    return txt, changes


def main():
    # desc patterns are unicode; decode list from escaped source
    patterns = desc_patterns()
    all_cards = set(ATTACK_CARDS.keys()) | {
        'WEAPON_SPIRIT', 'POWER_CRAVER', 'CROW_CROWD', 'POWER_SIPHONER', 'DR_MANHATTAN', 'PREMATURE',
        'CURSE_THIRST_ARCH_SUMMONER', 'CURSE_THIRST_SHAMAN', 'MARTYR', 'MAD_SCIENTIST', 'SACRIFICIAL_SWORD',
        'ELDER_SORCERER', 'POWER_TRANSFER',
    }
    for card_id in sorted(all_cards):
        path = find_prefab(card_id)
        if not path:
            print('MISSING PREFAB:', card_id)
            continue
        txt = open(path, encoding='utf-8', errors='replace').read()
        new_txt, changes = apply_file(card_id, txt, patterns)
        if new_txt != txt:
            open(path, 'w', encoding='utf-8', newline='').write(new_txt)
            print('%-24s %s' % (card_id, '; '.join(changes) if changes else 'no change?'))


if __name__ == '__main__':
    main()
