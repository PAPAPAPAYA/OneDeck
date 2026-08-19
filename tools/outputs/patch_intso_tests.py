# -*- coding: utf-8 -*-
"""Patch IntSOBasedEffectFactionTests curse assertions to attack semantics (CRLF-safe)."""
import io
import re

path = 'Assets/Scripts/Editor/Tests/IntSOBasedEffectFactionTests.cs'
txt = io.open(path, encoding='utf-8', newline='').read()

# normalize for pattern matching, keep original line endings by splitting
lines = txt.splitlines(keepends=True)

patterns = [
    # (regex over joined lines, replacement line(s))
    (r'Assert\.AreEqual\(2, CountStatusEffect\(enemyCurse, EnumStorage\.StatusEffect\.Power\),.*?\);\r?\n.*?\);',
     'Assert.AreEqual(2, enemyCurse.GetComponent<CardScript>().GetAttack(),\r\n\t\t\t"Owner CURSED_SKELETON should add ownerIntSO=2 attack to enemy curse card");'),
    (r'Assert\.AreEqual\(5, CountStatusEffect\(ownerCurse, EnumStorage\.StatusEffect\.Power\),.*?\);\r?\n.*?\);',
     'Assert.AreEqual(5, ownerCurse.GetComponent<CardScript>().GetAttack(),\r\n\t\t\t"Enemy CURSED_SKELETON should add enemyIntSO=5 attack to owner curse card");'),
    (r'Assert\.AreEqual\(3, CountStatusEffect\(enemyCurse, EnumStorage\.StatusEffect\.Power\),.*?\);\r?\n.*?\);',
     'Assert.AreEqual(3, enemyCurse.GetComponent<CardScript>().GetAttack(),\r\n\t\t\t"Owner DETERIORATION should add ownerIntSO=6 / coefficient=2 = 3 attack");'),
    (r'Assert\.AreEqual\(4, CountStatusEffect\(ownerCurse, EnumStorage\.StatusEffect\.Power\),.*?\);\r?\n.*?\);',
     'Assert.AreEqual(4, ownerCurse.GetComponent<CardScript>().GetAttack(),\r\n\t\t\t"Enemy DETERIORATION should add enemyIntSO=9 / coefficient=2 = 4 attack");'),
]

new_txt = txt
for pat, repl in patterns:
    m = re.search(pat, new_txt, re.S)
    if m:
        new_txt = new_txt[:m.start()] + repl + new_txt[m.end():]
        print('patched:', repl[:60])
    else:
        print('NOT FOUND:', pat[:60])

io.open(path, 'w', encoding='utf-8', newline='').write(new_txt)
print('done')
