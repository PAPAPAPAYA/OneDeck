# -*- coding: utf-8 -*-
import io
import sys

def read(p):
    return io.open(p, 'r', encoding='utf-8', newline='').read()

def apply_edits(path, edits):
    txt = read(path)
    for old, new, count in edits:
        n = txt.count(old)
        if n != count:
            print('FAIL', path, '| expected', count, 'found', n)
            print('  anchor:', repr(old[:70]))
            sys.exit(1)
        txt = txt.replace(old, new)
    io.open(path, 'w', encoding='utf-8', newline='').write(txt)
    print('[ok]', path)

T = '\t'

# ---------- AttackEffect.cs: add AttackSelf / AttackSelfTimes ----------
atk = 'Assets/Scripts/Effects/AttackEffect.cs'
anchor = (
    T + T + '// Attack-action timepoint: raised once per attack action, not per segment.\r\n'
    + T + T + 'GameEventStorage.me?.onAnyCardAttacked?.Raise();\r\n'
    + T + '}\r\n'
    + '}\r\n'
)
new_methods = (
    T + T + '// Attack-action timepoint: raised once per attack action, not per segment.\r\n'
    + T + T + 'GameEventStorage.me?.onAnyCardAttacked?.Raise();\r\n'
    + T + '}\r\n'
    + '\r\n'
    + T + '/// <summary>\r\n'
    + T + '/// Self-attack: the card\'s attack resolves against its own player (attack self-damage).\r\n'
    + T + '/// Same segment rules as Attack().\r\n'
    + T + '/// </summary>\r\n'
    + T + 'public void AttackSelf()\r\n'
    + T + '{\r\n'
    + T + T + 'if (myCardScript == null) return;\r\n'
    + T + T + 'AttackSelfTimes(myCardScript.GetAttackTimes());\r\n'
    + T + '}\r\n'
    + '\r\n'
    + T + '/// <summary>\r\n'
    + T + '/// Self-attack with an explicit segment count (e.g. a woken card attacking itself once).\r\n'
    + T + '/// No-ops when the card has no attack to resolve (0 or negative).\r\n'
    + T + '/// </summary>\r\n'
    + T + 'public void AttackSelfTimes(int times)\r\n'
    + T + '{\r\n'
    + T + T + 'if (myCardScript == null || times <= 0 || myCardScript.GetAttack() <= 0) return;\r\n'
    + T + T + 'for (int i = 0; i < times; i++)\r\n'
    + T + T + '{\r\n'
    + T + T + T + 'DecreaseMyHp();\r\n'
    + T + T + '}\r\n'
    + T + T + '// Attack-action timepoint: raised once per attack action, not per segment.\r\n'
    + T + T + 'GameEventStorage.me?.onAnyCardAttacked?.Raise();\r\n'
    + T + '}\r\n'
    + '}\r\n'
)
apply_edits(atk, [(anchor, new_methods, 1)])

# ---------- CardPhysObjScript.cs: include serialized attack print in flip faces ----------
phys = 'Assets/Scripts/UXPrototype/CardPhysObjScript.cs'
faces_old = T + T + T + T + 'if (cardStatusEffectPrint != null) faces.Add(cardStatusEffectPrint.transform);\r\n'
faces_new = faces_old + T + T + T + T + 'if (cardAttackPrint != null) faces.Add(cardAttackPrint.transform);\r\n'
apply_edits(phys, [(faces_old, faces_new, 1)])

print('DONE')
