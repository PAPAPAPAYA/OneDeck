# -*- coding: utf-8 -*-
"""Generate the phase-2 migration table: per-card design attack, bindings, desc, target mapping."""
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
        guid_map[m.group(1)] = os.path.basename(meta)[:-8]

pool = {c['id']: c for c in json.load(open('tools/outputs/_pool_final.json', encoding='utf-8'))}


def parse_prefab(path):
    txt = open(path, encoding='utf-8', errors='replace').read()
    blocks = re.findall(r'--- !u!114 &(-?\d+)\nMonoBehaviour:(.*?)(?=\n--- !u!|\Z)', txt, re.S)
    mono = {}
    for fid, body in blocks:
        m = re.search(r'm_Script: \{fileID: 11500000, guid: ([0-9a-f]+), type: 3\}', body)
        if m:
            mono[fid.strip()] = guid_map.get(m.group(1), '?')
    m_id = re.search(r'cardTypeID: ([A-Z0-9_]+)', txt)
    m_desc = re.search(r'cardDesc: "((?:[^"\\]|\\.)*)"', txt)
    binds = []
    for m in re.finditer(r'm_Target: \{fileID: (-?\d+)\}\n\s+m_TargetAssemblyTypeName: ([^,\n]+), Assembly-CSharp\n\s+m_MethodName: (\w+)\n\s+m_Mode: \d+\n\s+m_Arguments:\n\s+m_ObjectArgument: \{fileID: (\d+)\}\n\s+m_ObjectArgumentAssemblyTypeName: [^\n]+\n\s+m_IntArgument: (-?\d+)\n', txt):
        binds.append((m.group(2), m.group(3), int(m.group(5))))
    return m_id.group(1) if m_id else '?', m_desc.group(1) if m_desc else '', binds, mono


out = io.open('tools/outputs/_migration_table.txt', 'w', encoding='utf-8')
# Part 1: dmg cards
for p in sorted(glob.glob('Assets/Prefabs/Cards/3.0 no cost (current)/**/*.prefab', recursive=True)):
    if '_DONT INCLUDE' in p:
        continue
    cid, desc, binds, mono = parse_prefab(p)
    if '<dmg' not in desc:
        continue
    d = pool.get(cid, {})
    out.write('### %s (design atk=%s | design effect: %s)\n' % (cid, d.get('attack', '?'), d.get('effect', '?')))
    out.write('  desc: %s\n' % desc)
    for cls, method, arg in binds:
        if method != 'InvokeEffectEventVoid' and 'CheckCost' not in method:
            out.write('  bind: %s.%s(%s)\n' % (cls, method, arg))
    out.write('\n')
out.close()
print('part1 done')
