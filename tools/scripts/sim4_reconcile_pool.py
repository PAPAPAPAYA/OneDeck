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
	FAIL: desc content mismatch (markup-stripped, punctuation-width normalized)
	INFO: display-name drift (trailing '*' in Notion 中文名 is a user marker
	      and is stripped before comparison)
Exit code 0 when no FAIL items, 1 otherwise.
"""

import json
import re
import sys

EXCLUDED_STATUS = ('备用', '已删')
# Tokens: DB rows exist but engine-side they are generated, never prefabs
# under the 4.0 folders; exclude from the missing-prefab direction.
TOKEN_CIDS = ('JU_ON', 'RIFT')
# Reconcile scope = current TRIAL_RARITY_DIRS; other rarities are deferred
# batches and counted, not failed.
RARITY_IN_SCOPE = ('normal', 'uncommon', 'rare')


TAG_CN_TO_ENUM = {
	'埋葬': 'Bury', '遗言': 'DeathRattle', '强化': 'Enhance',
	'信徒': 'Believer', '放逐': 'Exile', '诅咒': 'Curse',
	'苏醒': 'Awaken', '被动': 'Passive', '复活': 'Revive',
	'强化反应': 'EnhanceReaction', '多次攻击': 'MultiAttack',
}
TAG_ENUM_TO_CN = {v: k for k, v in TAG_CN_TO_ENUM.items()}


def strip_markup(s):
	# <tag:X> renders the tag's display name; the DB writes the display form
	# directly as [信徒]. Normalize both to the same bracketed display form.
	s = re.sub(r'<tag:(\w+)>', lambda m: '[%s]' % TAG_ENUM_TO_CN.get(
		m.group(1), m.group(1)), s)
	s = re.sub(r'<b>|</b>', '', s)
	# Prefab tag references serialize as [<tag:X>] while the DB writes the
	# display form [信徒]; brackets carry no content, drop them on both sides.
	s = s.replace('[', '').replace(']', '')
	return s.replace('\\n', ' ')


def norm_desc(s):
	"""Content-level desc normalization.

	Based on tools/outputs/compare_db_to_unity_20260907.py `norm`, with the
	fullwidth semicolon and ASCII colon mapped too. Prefab descs are
	halfwidth-punctuation (Card Face Template v1.1: the font has no fullwidth
	glyphs) while the DB keeps the fullwidth readable variant, so punctuation
	width must not count as drift.
	"""
	if s is None:
		return ''
	s = strip_markup(s)
	s = s.replace('揭晓时:', '').replace('揭晓时：', '')
	for ch in ('：', '；', '，', '、', '。', ',', ';', ':'):
		s = s.replace(ch, ';')
	s = s.replace('×', 'x').replace(' ', '')
	return s.lower()


def load_rows(prefab_path, notion_path):
	with open(prefab_path, 'r', encoding='utf-8') as f:
		prefab = {c['cid']: c for c in json.load(f)}
	with open(notion_path, 'r', encoding='utf-8') as f:
		rows = json.load(f)
	notion = {r['CARD_TYPE_ID']: r for r in rows
			  if r.get('状态') not in EXCLUDED_STATUS
			  and r.get('rarity') in RARITY_IN_SCOPE}
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
		if cid in TOKEN_CIDS:
			continue
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
		if norm_desc(p['card_desc']) != norm_desc(n.get('card desc')):
			fails.append(f'desc mismatch: {cid}\n'
						 f'    prefab: {p["card_desc"]}\n'
						 f'    notion: {n.get("card desc")}')
		# DB tag column may arrive as a JSON-encoded string or an array.
		raw_tag = n.get('tag') or '[]'
		if isinstance(raw_tag, str):
			try:
				notion_tags = json.loads(raw_tag)
			except json.JSONDecodeError:
				notion_tags = []
		else:
			notion_tags = raw_tag
		notion_tag_set = {TAG_CN_TO_ENUM.get(t, t) for t in notion_tags}
		# Linger/ManaX are 3.0-legacy prefab tags with no DB counterpart;
		# Passive IS a real DB tag (glossary: 被动 prefix and tag must be
		# paired), so it is compared like any other tag.
		prefab_tag_set = set(p.get('tags') or []) - {'Linger', 'ManaX'}
		if notion_tag_set != prefab_tag_set:
			fails.append(f'tag mismatch: {cid} prefab={sorted(prefab_tag_set)} '
						 f'notion={sorted(notion_tag_set)}')
		pn = (n.get('中文名') or '').rstrip('*')
		if pn and pn != p['display_name']:
			infos.append(f'display drift: {cid} prefab={p["display_name"]} '
						 f'notion={pn}')

	print(f'[reconcile] prefab cards: {len(prefab)}, '
		  f'active in-scope Notion cards: {len(notion)} '
		  f'(scope: {"+".join(RARITY_IN_SCOPE)})')
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
