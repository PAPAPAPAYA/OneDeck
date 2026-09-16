# -*- coding: utf-8 -*-
"""Compare Unity prefab extraction vs Notion 4.0 card database snapshot (2026-09-15)."""
import json, re, sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

unity = json.load(open('tools/outputs/unity_cards_current.json', encoding='utf-8'))
rows = json.load(open('tools/outputs/notion_cards_snapshot_20260915.json', encoding='utf-8'))

DB = {}
for cid, cn, rar, atk, desc, tags, status, creature, pageid in rows:
    DB[cid] = dict(cn=cn, rarity=rar, atk=atk, desc=desc or '',
                   tags=[t for t in tags.split('|') if t], status=status,
                   creature=creature, pageid=pageid)

TAGMAP = {'DeathRattle': '遗言', 'Bury': '埋葬', 'Enhance': '强化', 'Believer': '信徒',
          'Exile': '放逐', 'Curse': '诅咒', 'Awaken': '苏醒', 'Passive': '被动',
          'Revive': '复活', 'EnhanceReaction': '强化反应', 'MultiAttack': '多次攻击'}

def norm_desc(s):
    if not s:
        return ''
    s = re.sub(r'</?b>', '', s)
    s = re.sub(r'<tag:(\w+)>', lambda m: '[' + TAGMAP.get(m.group(1), m.group(1)) + ']', s)
    s = s.replace('×', 'x')
    for a, b in [('：', ':'), ('，', ','), ('；', ';'), ('（', '('), ('）', ')')]:
        s = s.replace(a, b)
    s = re.sub(r'\s+', '', s)
    return s

u_by_id = {}
for u in unity:
    u_by_id[u['cardTypeID']] = u

missing_in_db = sorted(set(u_by_id) - set(DB))
extra_in_db = sorted(set(DB) - set(u_by_id))
print('=== COVERAGE ===')
print('unity-only (no DB row):', missing_in_db or 'none')
print('db-only (no prefab):')
for cid in extra_in_db:
    print('  ', cid, 'status=', DB[cid]['status'] or '(启用)')

print()
print('=== FIELD DIFFS (prefab -> DB) ===')
diffs = 0
for u in unity:
    cid = u['cardTypeID']
    if cid not in DB:
        continue
    d = DB[cid]
    # names
    if u['displayName'] != d['cn']:
        print(f'[中文名] {cid}: prefab={u["displayName"]!r} db={d["cn"]!r}')
        diffs += 1
    # rarity
    if u['rarityName'] != d['rarity']:
        print(f'[rarity] {cid}: prefab={u["rarityName"]} db={d["rarity"]}')
        diffs += 1
    # creature select
    is_creature = u['cardType'] == '1'
    want = '生物' if is_creature else '非生物'
    if want != d['creature']:
        print(f'[生物] {cid}: prefab={want} db={d["creature"]}')
        diffs += 1
    # ATK: creatures = printedAttack; non-creatures = should be empty
    pa = u.get('printedAttack') or ''
    if is_creature:
        want_atk = int(pa) if pa != '' else None
        if want_atk != d['atk']:
            print(f'[ATK] {cid}: prefab={want_atk} db={d["atk"]}')
            diffs += 1
    else:
        if d['atk'] is not None:
            print(f'[ATK-clear] {cid}: db has ATK={d["atk"]} but prefab cardType={u["cardType"]} (non-creature)')
            diffs += 1
    # desc (semantic, normalized)
    nd, nu = norm_desc(d['desc']), norm_desc(u['cardDesc'])
    if nd != nu:
        print(f'[desc] {cid}:')
        print(f'   prefab: {u["cardDesc"]}')
        print(f'   db    : {d["desc"]}')
        diffs += 1
    # tag: only report empties on DB side (design field)
    if not d['tags'] and u['cardType'] != '2':
        print(f'[tag-empty] {cid}: db tag empty, prefab desc={u["cardDesc"][:60]}')
        diffs += 1

print()
print('total diff lines:', diffs)
