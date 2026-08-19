# -*- coding: utf-8 -*-
"""Detailed inventory: for every card prefab, dump bindings, desc, baseDmg, printedAttack.

Prints a per-card table suitable for planning the phase-2 migration.
"""
import re
import os
import glob
import json
import io

guid_map = {}
for meta in glob.glob('Assets/**/*.cs.meta', recursive=True):
    txt = open(meta, encoding='utf-8').read()
    m = re.search(r'guid: ([0-9a-f]+)', txt)
    if m:
        cls = os.path.basename(meta)[:-7]
        guid_map[m.group(1)] = cls


def parse_prefab(path):
    txt = open(path, encoding='utf-8', errors='replace').read()
    # script guid -> class on each MonoBehaviour (fileID -> guid)
    # find all MonoBehaviour blocks with m_Script
    blocks = re.findall(r'--- !u!114 &(-?\d+)\nMonoBehaviour:(.*?)(?=\n--- !u!|\Z)', txt, re.S)
    mono = {}
    for fid, body in blocks:
        m = re.search(r'm_Script: \{fileID: 11500000, guid: ([0-9a-f]+), type: 3\}', body)
        if m:
            mono[fid.strip()] = guid_map.get(m.group(1), '?')

    m_id = re.search(r'cardTypeID: ([A-Z0-9_]+)', txt)
    m_desc = re.search(r'cardDesc: "((?:[^"\\]|\\.)*)"', txt)
    m_atk = re.search(r'printedAttack: (-?\d+)', txt)
    bd = re.findall(r'baseDmg: \{fileID: 11400000, guid: ([0-9a-f]+), type: 2\}', txt)

    # UnityEvent bindings: CostNEffectContainer effectEvent + GameEventListener response
    binds = []
    for m in re.finditer(r'm_Target: \{fileID: (-?\d+)\}\n\s+m_TargetAssemblyTypeName: ([^,\n]+), Assembly-CSharp\n\s+m_MethodName: (\w+)\n\s+m_Mode: \d+\n\s+m_Arguments:\n\s+m_ObjectArgument: \{fileID: (\d+)\}\n\s+m_ObjectArgumentAssemblyTypeName: [^\n]+\n\s+m_IntArgument: (-?\d+)\n', txt):
        fid, cls, method, objarg, intarg = m.groups()
        binds.append({'targetClass': cls, 'method': method, 'intArg': int(intarg)})

    classes = [mono[k] for k in mono]
    return {
        'file': path.replace('Assets/Prefabs/Cards/3.0 no cost (current)/', ''),
        'id': m_id.group(1) if m_id else '?',
        'desc': m_desc.group(1) if m_desc else '',
        'printedAttack': m_atk.group(1) if m_atk else None,
        'baseDmgGuids': bd,
        'classes': classes,
        'bindings': binds,
    }


cards = []
for p in glob.glob('Assets/Prefabs/Cards/3.0 no cost (current)/**/*.prefab', recursive=True):
    if '_DONT INCLUDE' in p:
        continue
    c = parse_prefab(p)
    if '<dmg' in c['desc'] or c['baseDmgGuids'] or 'AttackEffect' in c['classes']:
        cards.append(c)

out = io.open('tools/outputs/_detailed_inventory.txt', 'w', encoding='utf-8')
for c in sorted(cards, key=lambda x: x['file']):
    out.write('=' * 100 + '\n')
    out.write('%s | %s | printedAttack=%s\n' % (c['id'], c['file'], c['printedAttack']))
    out.write('  desc: %s\n' % c['desc'])
    out.write('  classes: %s\n' % ', '.join(sorted(set(c['classes']))))
    for b in c['bindings']:
        out.write('  bind: %s.%s(%s)\n' % (b['targetClass'], b['method'], b['intArg']))
out.close()
print('done, %d cards' % len(cards))
