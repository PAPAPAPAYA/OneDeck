import uuid, os

os.chdir(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))

def gid(): return uuid.uuid4().hex

G = {k: gid() for k in ["cs","folderNames","folderTooltips","nameCreature","nameNone","descCreature","descNone","db"]}
print("GUIDs:", G)

STRINGS_META = """fileFormatVersion: 2
guid: {g}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

FOLDER_META = """fileFormatVersion: 2
guid: {g}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

CS_META = """fileFormatVersion: 2
guid: {g}
MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{instanceID: 0}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""

def string_so(name, value, guid):
    body = (
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
"  m_Script: {fileID: 11500000, guid: 24e8cea97c7341179c6174ad691474ef, type: 3}\n"
"  m_Name: " + name + "\n"
"  m_EditorClassIdentifier: Assembly-CSharp::DefaultNamespace.SOScripts.StringSO\n"
'  value: "' + value + '"\n'
"  reset: 0\n")
    return body, STRINGS_META.format(g=guid)

BS = chr(92)
def esc(s):
    return "".join(c if ord(c) < 128 else BS + "u%04X" % ord(c) for c in s)

os.makedirs("Assets/SORefs/Strings/CardTypeNames", exist_ok=True)
os.makedirs("Assets/SORefs/Strings/CardTypeTooltips", exist_ok=True)

assets = [
    ("Assets/SORefs/Strings/CardTypeNames/CardTypeName_Creature.asset", "CardTypeName_Creature", esc(u"\u5b9e\u4f53"), G["nameCreature"]),
    ("Assets/SORefs/Strings/CardTypeNames/CardTypeName_None.asset", "CardTypeName_None", esc(u"\u73b0\u8c61"), G["nameNone"]),
    ("Assets/SORefs/Strings/CardTypeTooltips/CardTypeTooltip_Creature.asset", "CardTypeTooltip_Creature", esc(u"\u53ef\u653b\u51fb"), G["descCreature"]),
    ("Assets/SORefs/Strings/CardTypeTooltips/CardTypeTooltip_None.asset", "CardTypeTooltip_None", esc(u"\u4e0d\u53ef\u653b\u51fb"), G["descNone"]),
]
for path, name, value, guid in assets:
    body, meta = string_so(name, value, guid)
    open(path, "w", encoding="utf-8", newline="\n").write(body)
    open(path + ".meta", "w", encoding="utf-8", newline="\n").write(meta)
    print("wrote", path)

open("Assets/SORefs/Strings/CardTypeNames.meta", "w", encoding="utf-8", newline="\n").write(FOLDER_META.format(g=G["folderNames"]))
open("Assets/SORefs/Strings/CardTypeTooltips.meta", "w", encoding="utf-8", newline="\n").write(FOLDER_META.format(g=G["folderTooltips"]))
open("Assets/Scripts/SOScripts/CardTypeTooltipDatabaseSO.cs.meta", "w", encoding="utf-8", newline="\n").write(CS_META.format(g=G["cs"]))

db_body = (
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
"  m_Script: {fileID: 11500000, guid: " + G["cs"] + ", type: 3}\n"
"  m_Name: CardTypeTooltipDatabase\n"
"  m_EditorClassIdentifier: Assembly-CSharp::CardTypeTooltipDatabaseSO\n"
"  entries:\n"
"  - cardType: 1\n"
"    displayName: {fileID: 11400000, guid: " + G["nameCreature"] + ", type: 2}\n"
"    description: {fileID: 11400000, guid: " + G["descCreature"] + ", type: 2}\n"
"  - cardType: 0\n"
"    displayName: {fileID: 11400000, guid: " + G["nameNone"] + ", type: 2}\n"
"    description: {fileID: 11400000, guid: " + G["descNone"] + ", type: 2}\n")
open("Assets/Resources/CardTypeTooltipDatabase.asset", "w", encoding="utf-8", newline="\n").write(db_body)
open("Assets/Resources/CardTypeTooltipDatabase.asset.meta", "w", encoding="utf-8", newline="\n").write(STRINGS_META.format(g=G["db"]))
print("wrote database asset")
