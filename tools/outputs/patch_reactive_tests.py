# -*- coding: utf-8 -*-
"""Patch ReactiveChainTests Power-event listeners to attack events."""
import io

path = 'Assets/Scripts/Editor/Tests/ReactiveChainTests.cs'
txt = io.open(path, encoding='utf-8', newline='').read()

repls = [
    # CursePowerTriggersDamageChain: listener event + comment + final assertion
    ('listener.@event = GameEventStorage.onAnyCardGotPower;',
     'listener.@event = GameEventStorage.onAnyCardGainedAttack;'),
    ('GameEventStorage.onAnyCardGotPower.RegisterListener(listener);',
     'GameEventStorage.onAnyCardGainedAttack.RegisterListener(listener);'),
    ('\t\tint powerCount = 0;\r\n\t\tforeach (var effect in curseTarget.GetComponent<CardScript>().myStatusEffects)\r\n\t\t{\r\n\t\t\tif (effect == EnumStorage.StatusEffect.Power) powerCount++;\r\n\t\t}\r\n\t\tAssert.AreEqual(2, powerCount, "Curse target should have 2 Power stacks");',
     '\t\tAssert.AreEqual(2, curseTarget.GetComponent<CardScript>().GetAttack(), "Curse target should have 2 attack");'),
    # NestedReactiveEffects: Card2 listens to onMeGainedAttack
    ('listener2.@event = GameEventStorage.onMeGotPower;',
     'listener2.@event = GameEventStorage.onMeGainedAttack;'),
    ('GameEventStorage.onMeGotPower.RegisterListener(listener2);',
     'GameEventStorage.onMeGainedAttack.RegisterListener(listener2);'),
    ('\t\t// Card1 (Curse) gives Power to Card2\r\n\t\t// Card2 listens to onMeGotPower -> attacks enemy',
     '\t\t// Card1 (Curse) grants attack to Card2\r\n\t\t// Card2 listens to onMeGainedAttack -> attacks enemy'),
    ('\t\t// Card1: CurseEffect gives Power to Card2',
     '\t\t// Card1: CurseEffect grants attack to Card2'),
    ('\t\t// Card2: HPAlterEffect triggered by onMeGotPower',
     '\t\t// Card2: HPAlterEffect triggered by onMeGainedAttack'),
    ('\t\t// Execute: Card1 gives Power to Card2',
     '\t\t// Execute: Card1 grants attack to Card2'),
]

for old, new in repls:
    n = txt.count(old)
    if n == 0:
        print('NOT FOUND:', old[:70])
    txt = txt.replace(old, new)

io.open(path, 'w', encoding='utf-8', newline='').write(txt)
print('done')
