# -*- coding: utf-8 -*-
"""Find all Power-related cards across prefabs (Power word or Power-ish components)."""
import re
import os
import glob

guid_map = {}
for meta in glob.glob('Assets/**/*.cs.meta', recursive=True):
    txt = open(meta, encoding='utf-8').read()
    m = re.search(r'guid: ([0-9a-f]+)', txt)
    if m:
        guid_map[m.group(1)] = os.path.basename(meta)[:-8]


def parse(path):
    txt = open(path, encoding='utf-8', errors='replace').read()
    blocks = re.findall(r'--- !u!114 &(-?\d+)\nMonoBehaviour:(.*?)(?=\n--- !u!|\Z)', txt, re.S)
    classes = []
    for fid, body in blocks:
        m = re.search(r'm_Script: \{fileID: 11500000, guid: ([0-9a-f]+), type: 3\}', body)
        if m:
            classes.append(guid_map.get(m.group(1), '?'))
    m_id = re.search(r'cardTypeID: ([A-Z0-9_]+)', txt)
    m_desc = re.search(r'cardDesc: "((?:[^"\\]|\\.)*)"', txt)
    return m_id.group(1) if m_id else '?', m_desc.group(1) if m_desc else '', classes


power_components = {'StatusEffectGiverEffect', 'StatusEffectAmplifierEffect', 'TransferStatusEffectEffect',
                    'PowerReactionEffect', 'CurseEffect', 'ConsumeStatusEffect'}
found = {}
for p in glob.glob('Assets/Prefabs/Cards/3.0 no cost (current)/**/*.prefab', recursive=True):
    if '_DONT INCLUDE' in p:
        continue
    cid, desc, classes = parse(p)
    powerish = set(classes) & power_components
    has_power_word = '\u529b\u91cf' in desc  # 力量
    if powerish or has_power_word:
        found[cid] = {'file': p.split('current\\')[-1], 'desc': desc,
                      'classes': sorted(set(classes) & power_components), 'powerWord': has_power_word}

for cid in sorted(found):
    f = found[cid]
    print('%-22s powerWord=%s comps=%s' % (cid, f['powerWord'], f['classes']))
    print('    desc: %s' % f['desc'])
    print('    file: %s' % f['file'])
