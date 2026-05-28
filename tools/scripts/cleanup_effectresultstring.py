import os

# 1. CostNEffectContainer.cs
path1 = r'D:\Unity Projects\OneDeck\Assets\Scripts\Card\CostNEffectContainer.cs'
with open(path1, 'r', encoding='utf-8') as f:
    content = f.read()
old = '\t[Tooltip("the string SO that combat info displayer use to display effect result")]\r\n\tpublic StringSO effectResultString;\r\n\r\n'
content = content.replace(old, '')
with open(path1, 'w', encoding='utf-8') as f:
    f.write(content)
print('CostNEffectContainer: done')

# 2. EffectScript.cs
path2 = r'D:\Unity Projects\OneDeck\Assets\Scripts\Effects\EffectScript.cs'
with open(path2, 'r', encoding='utf-8') as f:
    content = f.read()
old = '\t[Tooltip("Deprecated: use AppendLog() instead. Kept for backward compatibility with existing prefabs.")]\r\n\tpublic StringSO effectResultString;\r\n\t\r\n'
content = content.replace(old, '\r\n')
old2 = '\tprotected void AppendLog(string text)\r\n\t{\r\n\t\tif (CombatLog.me != null)\r\n\t\t{\r\n\t\t\tCombatLog.me.Append(text);\r\n\t\t}\r\n\t\telse if (effectResultString != null)\r\n\t\t{\r\n\t\t\t// Fallback for backward compatibility if CombatLog is not available\r\n\t\t\teffectResultString.value += text;\r\n\t\t}\r\n\t}'
new2 = '\tprotected void AppendLog(string text)\r\n\t{\r\n\t\tCombatLog.me?.Append(text);\r\n\t}'
content = content.replace(old2, new2)
with open(path2, 'w', encoding='utf-8') as f:
    f.write(content)
print('EffectScript: done')

# 3. CombatInfoDisplayer.cs
path3 = r'D:\Unity Projects\OneDeck\Assets\Scripts\Managers\CombatInfoDisplayer.cs'
with open(path3, 'r', encoding='utf-8') as f:
    content = f.read()
old = '\tpublic StringSO effectResultString;\r\n\tpublic GamePhaseSO gamePhase;'
content = content.replace(old, '\tpublic GamePhaseSO gamePhase;')
old2 = '\t\tCombatLog.me?.Clear();\r\n\t\teffectResultString.value = "";\r\n\t\teffectResultDisplay.text = "";'
content = content.replace(old2, '\t\tCombatLog.me?.Clear();\r\n\t\teffectResultDisplay.text = "";')
with open(path3, 'w', encoding='utf-8') as f:
    f.write(content)
print('CombatInfoDisplayer: done')

# 4. AddTextEffect.cs
path4 = r'D:\Unity Projects\OneDeck\Assets\Scripts\Effects\AddTextEffect.cs'
with open(path4, 'r', encoding='utf-8') as f:
    content = f.read()
old = '    /// <summary>\r\n    /// Effect to add custom text to effectResultString\r\n    /// </summary>'
content = content.replace(old, '    /// <summary>\r\n    /// Effect to add custom text to the combat log.\r\n    /// </summary>')
old2 = '        [Tooltip("Text content to add to effectResultString")]'
content = content.replace(old2, '        [Tooltip("Text content to add to the combat log")]')
with open(path4, 'w', encoding='utf-8') as f:
    f.write(content)
print('AddTextEffect: done')
