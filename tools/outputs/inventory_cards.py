# -*- coding: utf-8 -*-
"""Inventory attack-related cards in the 3.0 prefab folder.

For each prefab that references <dmg>, baseDmg or AttackEffect, prints the
cardTypeID, printedAttack, cardDesc and effect classes bound to it.
"""
import re
import os
import glob
import json

guid_map = {}
for meta in glob.glob('Assets/**/*.cs.meta', recursive=True):
    txt = open(meta, encoding='utf-8').read()
    m = re.search(r'guid: ([0-9a-f]+)', txt)
    if m:
        cls = os.path.basename(meta)[:-7]
        guid_map[m.group(1)] = cls


def parse_prefab(path):
    txt = open(path, encoding='utf-8').read()
    scripts = re.findall(r'm_Script: \{fileID: 11500000, guid: ([0-9a-f]+), type: 3\}', txt)
    m_id = re.search(r'cardTypeID: ([A-Z0-9_]+)', txt)
    m_desc = re.search(r'cardDesc: "((?:[^"\\]|\\.)*)"', txt)
    m_atk = re.search(r'printedAttack: (-?\d+)', txt)
    bd = re.findall(r'baseDmg: \{fileID: \d+, guid: ([0-9a-f]+), type: 2\}', txt)
    classes = [guid_map.get(g, '?') for g in dict.fromkeys(scripts)]
    return {
        'file': path.replace('Assets/Prefabs/Cards/3.0 no cost (current)/', ''),
        'id': m_id.group(1) if m_id else '?',
        'desc': m_desc.group(1) if m_desc else '',
        'printedAttack': m_atk.group(1) if m_atk else None,
        'baseDmgGuids': bd,
        'classes': classes,
    }


cards = []
for p in glob.glob('Assets/Prefabs/Cards/3.0 no cost (current)/**/*.prefab', recursive=True):
    if '_DONT INCLUDE' in p:
        continue
    c = parse_prefab(p)
    if '<dmg' in c['desc'] or c['baseDmgGuids'] or 'AttackEffect' in c['classes']:
        cards.append(c)

print('%d cards with dmg/attack references' % len(cards))
for c in sorted(cards, key=lambda x: x['file']):
    print('%-24s atk=%-5s | %s' % (c['id'], str(c['printedAttack']), c['file'][:70]))
json.dump(cards, open('tools/outputs/_attack_cards_inventory.json', 'w', encoding='utf-8'),
          ensure_ascii=False, indent=1)
