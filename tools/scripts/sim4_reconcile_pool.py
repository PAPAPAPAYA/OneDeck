#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Reconcile a sim4 prefab card table against a Notion DB export.

Usage:
	python sim4_reconcile_pool.py <prefab_table.json> <notion_export.json>

The prefab table is produced by:
	python one_deck_damage_sim.py --dump-pool common40

The Notion export is a JSON array of rows with keys: CARD_TYPE_ID, 中文名,
ATK, 生物, 状态, tag, card desc. Rows whose 状态 is 备用/已删 are excluded
before comparison (empty / 启用 = active).

Checks:
	FAIL: cid present on one side only (missing / extra card)
	FAIL: creature card ATK mismatch (prefab printedAttack vs Notion ATK)
	FAIL: creature flag mismatch (prefab cardType vs Notion 生物)
	INFO: display-name drift (trailing '*' in Notion 中文名 is a user marker
	      and is stripped before comparison)
Exit code 0 when no FAIL items, 1 otherwise.
"""

import json
import sys

EXCLUDED_STATUS = ('备用', '已删')


def load_rows(prefab_path, notion_path):
	with open(prefab_path, 'r', encoding='utf-8') as f:
		prefab = {c['cid']: c for c in json.load(f)}
	with open(notion_path, 'r', encoding='utf-8') as f:
		rows = json.load(f)
	notion = {r['CARD_TYPE_ID']: r for r in rows
			  if r.get('状态') not in EXCLUDED_STATUS}
	return prefab, notion


def main():
	if len(sys.argv) != 3:
		print(__doc__)
		sys.exit(2)
	prefab, notion = load_rows(sys.argv[1], sys.argv[2])

	fails = []
	infos = []

	for cid in sorted(prefab.keys() - notion.keys()):
		fails.append(f'prefab-only (missing in Notion): {cid}')
	for cid in sorted(notion.keys() - prefab.keys()):
		fails.append(f'Notion-only (missing prefab): {cid}')

	for cid in sorted(prefab.keys() & notion.keys()):
		p, n = prefab[cid], notion[cid]
		if p['is_creature']:
			if n.get('生物') != '生物':
				fails.append(f'creature flag mismatch: {cid} prefab=creature '
							 f'notion={n.get("生物")}')
			elif n.get('ATK') != p['printed_attack']:
				fails.append(f'ATK mismatch: {cid} prefab={p["printed_attack"]} '
							 f'notion={n.get("ATK")}')
		elif n.get('生物') != '非生物':
			fails.append(f'creature flag mismatch: {cid} prefab=non-creature '
						 f'notion={n.get("生物")}')
		pn = (n.get('中文名') or '').rstrip('*')
		if pn and pn != p['display_name']:
			infos.append(f'display drift: {cid} prefab={p["display_name"]} '
						 f'notion={pn}')

	print(f'[reconcile] prefab cards: {len(prefab)}, '
		  f'active Notion cards: {len(notion)}')
	for line in infos:
		print(f'[reconcile] INFO  {line}')
	for line in fails:
		print(f'[reconcile] FAIL  {line}')
	if fails:
		print(f'[reconcile] result: {len(fails)} mismatch(es)')
		sys.exit(1)
	print('[reconcile] result: pool consistent (no missing / extra / ATK / '
		  'creature-flag drift)')


if __name__ == '__main__':
	main()
