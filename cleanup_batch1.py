import re

path = r'D:\Unity Projects\OneDeck\Assets\Scripts\Batch1.cs'
with open(path, 'r', encoding='utf-8') as f:
    content = f.read()

# Detect line ending
if '\r\n' in content:
    nl = '\r\n'
else:
    nl = '\n'

# Remove the variable declaration
content = content.replace('StringSO effectResultStr = (StringSO)ScriptableObject.CreateInstance(typeof(StringSO));' + nl, nl)

# Remove all assignments
patterns = [
    ('cnt.effectResultString = effectResultStr;' + nl, ''),
    ('hae.effectResultString = effectResultStr;' + nl, ''),
    ('ce.cardTypeID = jtid; ce.effectResultString = effectResultStr;' + nl, 'ce.cardTypeID = jtid;' + nl),
    ('W(fitr, mce); mce.effectResultString = effectResultStr;' + nl, 'W(fitr, mce);' + nl),
    ('W(fitr, be); be.effectResultString = effectResultStr;' + nl, 'W(fitr, be);' + nl),
    ('W(rift, se); se.effectResultString = effectResultStr;' + nl, 'W(rift, se);' + nl),
    ('W(rift, ee); ee.effectResultString = effectResultStr;' + nl, 'W(rift, ee);' + nl),
]

for old, new in patterns:
    content = content.replace(old, new)

with open(path, 'w', encoding='utf-8') as f:
    f.write(content)

# Verify
lines = content.split(nl)
remaining = []
for i, line in enumerate(lines, 1):
    if 'effectResultString' in line or 'effectResultStr' in line:
        remaining.append((i, line))

if remaining:
    print('Remaining references:')
    for i, line in remaining:
        print(f'  L{i}: {line}')
else:
    print('Batch1: all effectResultString references removed')
