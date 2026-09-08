#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
OneDeck damage-per-round Monte Carlo simulator.
Assumptions (per user request):
- Real card pool, random 6v6 decks with replacement.
- Only steady-state rounds (first full cycle discarded as warmup).
- No fatigue.
- Damage to enemy includes self-damage dealt by enemy cards (e.g. JU_ON).
- Power/Counter are included where implemented; Counter/Rest skip effects are NOT modelled.
- Some graveyard/RIFT/curse interactions are approximated where exact Unity config is ambiguous.
"""

import argparse
import json
import os
import random
import re
import codecs
import sys
from collections import defaultdict, Counter

MAX_CHAIN_DEPTH = 12
MAX_POWER_PER_CARD = 20
MAX_FRIENDLY_CARDS = 12
MAX_TOTAL_DECK = 28
WARMUP_ROUNDS = 2000
RECORD_ROUNDS = 10000

START_STD_FACTOR = 0.15

DEATHRATTLE_CIDS = {
	'SOLDIER_SKELETON', 'GRAVE_PORTAL', 'AVENGER', 'CONFUSED_PORTALMANCER',
	'CURSED_CORPSE', 'SCAPEGOAT', 'SPIKE_SKELETON', 'GRAVE_KEEPER',
	'MARTYR', 'SLIME', 'WISE_BURIAL'
}

# Cards whose CostNEffectContainer uses CheckCost_IndexBeforeStartCard in Unity.
# Their effects only trigger while the card is positioned before (below) the Start Card.
LINGER_CIDS = {
	'ETERNAL_GHOST', 'CURSE_ENCHANTMENT', 'WEAPON_SPIRIT',
	'RIFT_COFFIN', 'DEATHBED_CURSE', 'QUICK_RESPONSE_PROTOCOL'
}


def meets_linger_cost(state, card):
	"""Returns True if a [Linger] card is currently before the Start Card in deck order."""
	return is_below_start(state, card)


# ---------------------------------------------------------------------------
# Card metadata (display name + rarity) from prefabs
# ---------------------------------------------------------------------------
RARITY_LABELS = {0: 'Common', 1: 'Uncommon', 2: 'Rare'}


def _load_card_info():
	"""Parse prefabs under Assets/Prefabs/Cards for displayName and rarity."""
	script_dir = os.path.dirname(os.path.abspath(__file__))
	cards_dir = os.path.join(script_dir, '..', '..', 'Assets', 'Prefabs', 'Cards', '3.0 no cost (current)')
	info = {}
	if not os.path.isdir(cards_dir):
		return info
	for root, _, files in os.walk(cards_dir):
		for fname in files:
			if not fname.endswith('.prefab'):
				continue
			path = os.path.join(root, fname)
			cid = None
			display = None
			rarity = None
			with open(path, 'r', encoding='utf-8', errors='ignore') as f:
				for line in f:
					m = re.search(r'^\s*cardTypeID:\s*([A-Za-z0-9_]+)$', line)
					if m:
						cid = m.group(1).strip()
					m = re.search(r'^\s*displayName:\s*"(.*)"$', line)
					if m:
						raw = m.group(1).strip()
						try:
							display = codecs.decode(raw, 'unicode_escape')
						except Exception:
							display = raw
					m = re.search(r'^\s*rarity:\s*(\d+)$', line)
					if m:
						rarity = int(m.group(1))
			if cid:
				info[cid] = {'display_name': display or cid, 'rarity': rarity}
	return info


CARD_INFO = _load_card_info()


def card_display(cid):
	return CARD_INFO.get(cid, {}).get('display_name', cid)


def card_rarity(cid):
	r = CARD_INFO.get(cid, {}).get('rarity')
	return RARITY_LABELS.get(r, 'Unknown')


# ---------------------------------------------------------------------------
# 4.0 card pool data layer (Step 1 of plan-python-sim-4.0-upgrade-2026-08-31)
# Trial scope: Common only. Extend TRIAL_RARITY_DIRS for later batches
# ('1_Uncommon', '2_Rare').
# ---------------------------------------------------------------------------
TRIAL_RARITY_DIRS = ('0_Common',)

# EnumStorage.CardType: None=0, Creature=1, Status=2
CARD_TYPE_CREATURE = 1

_PREFAB_BLOCK_SPLIT = re.compile(r'^--- !u!\d+ &\d+\s*$', re.M)
# NOTE: cardTypeID may contain a dot (e.g. AVENGER_4.0). The 3.0 loader's
# [A-Za-z0-9_]+ class silently truncates such IDs; never reuse that class here.
_CID_RE = re.compile(r'^\s+cardTypeID: ([A-Za-z0-9_.]+)\s*$', re.M)
_DISPLAY_RE = re.compile(r'^\s+displayName:\s*"(.*)"\s*$', re.M)


# EnumStorage.Tag (append-only enum; prefab myTags serializes LE int32s hex).
# Names mirror the enum order; DB tag column uses the Chinese display names.
TAG_ENUM_NAMES = {
	1: 'Linger', 2: 'ManaX', 3: 'DeathRattle', 4: 'Bury', 5: 'Enhance',
	6: 'Believer', 7: 'Exile', 8: 'Curse', 9: 'Awaken', 10: 'Passive',
	11: 'Revive', 12: 'EnhanceReaction', 13: 'MultiAttack',
}

_MY_TAGS_RE = re.compile(r'^\s+myTags: ([0-9a-fA-F]*)\s*$', re.M)


def _extract_quoted_field(block, name):
	"""Extract a double-quoted YAML scalar, tolerating Unity's line folding.

	Long strings are serialized across multiple lines inside the quotes; the
	YAML fold joins them with a single space, mirrored here.
	"""
	out, collecting = [], False
	for ln in block.split('\n'):
		if collecting:
			rest = ln.strip()
		else:
			m = re.match(r'^\s+%s: "(.*)$' % name, ln)
			if not m:
				continue
			rest = m.group(1)
			collecting = True
		if rest.endswith('"') and not rest.endswith('\\"'):
			out.append(rest[:-1])
			return ' '.join(out)
		out.append(rest)
	return None


def _parse_my_tags(block):
	m = _MY_TAGS_RE.search(block)
	if not m or not m.group(1):
		return []
	hexstr = m.group(1)
	values = [int.from_bytes(bytes.fromhex(hexstr[i:i + 8]), 'little')
			  for i in range(0, len(hexstr), 8)]
	return [TAG_ENUM_NAMES.get(v, f'Unknown{v}') for v in values]


def _int_field(block, name):
	m = re.search(r'^\s+%s: (-?\d+)\s*$' % name, block, re.M)
	return int(m.group(1)) if m else None


def _load_card_pool_40():
	"""Block-scoped prefab parse for the 4.0 card pool.

	Each prefab has exactly one CardScript block; it is identified by holding
	an inline (non-SO-reference) cardTypeID together with a displayName. Other
	components (e.g. CurseEffect) also carry a cardTypeID field but as a
	{fileID/guid} reference, which the inline regex skips.
	"""
	script_dir = os.path.dirname(os.path.abspath(__file__))
	pool = {}
	for rarity_dir in TRIAL_RARITY_DIRS:
		full_dir = os.path.join(script_dir, '..', '..', 'Assets', 'Prefabs',
								'Cards', '4.0', rarity_dir)
		if not os.path.isdir(full_dir):
			print(f'[sim4] WARNING: rarity dir not found: {full_dir}')
			continue
		for fname in sorted(os.listdir(full_dir)):
			if not fname.endswith('.prefab'):
				continue
			with open(os.path.join(full_dir, fname), 'r', encoding='utf-8',
					  errors='ignore') as f:
				text = f.read()
			blocks = [b for b in _PREFAB_BLOCK_SPLIT.split(text)
					  if _CID_RE.search(b) and _DISPLAY_RE.search(b)]
			if len(blocks) != 1:
				print(f'[sim4] WARNING: {fname}: {len(blocks)} CardScript-like '
					  f'blocks found, skipped')
				continue
			block = blocks[0]
			cid = _CID_RE.search(block).group(1)
			m = _DISPLAY_RE.search(block)
			raw = m.group(1).strip() if m else cid
			try:
				display = codecs.decode(raw, 'unicode_escape')
			except Exception:
				display = raw
			raw_desc = _extract_quoted_field(block, 'cardDesc')
			if raw_desc is not None:
				try:
					card_desc = codecs.decode(raw_desc.strip(),
											  'unicode_escape')
				except Exception:
					card_desc = raw_desc.strip()
			else:
				card_desc = None
			card_type = _int_field(block, 'cardType')
			is_passive = _int_field(block, 'isPassive')
			utility_kind = _int_field(block, 'utilityKind')
			pool[cid] = {
				'cid': cid,
				'display_name': display or cid,
				# Raw prefab cardDesc (halfwidth punctuation + markup per
				# Card Face Template v1.1); DB-side desc is the fullwidth
				# readable variant of the same content.
				'card_desc': card_desc,
				# Rarity by directory is the source of truth; rarity_field is
				# the CardScript rarity int kept for cross-checking.
				'rarity': rarity_dir,
				'rarity_field': _int_field(block, 'rarity'),
				'printed_attack': _int_field(block, 'printedAttack'),
				'attack_growth': _int_field(block, 'attackGrowth'),
				'extra_attack_times': _int_field(block, 'extraAttackTimes'),
				'card_type': card_type,
				'is_creature': card_type == CARD_TYPE_CREATURE,
				'is_passive': bool(is_passive),
				'utility_kind': utility_kind,
				# Mirrors CardScript.IsUtilityPassive:
				# isPassive && utilityKind != UtilityKind.None
				'is_utility_passive': bool(is_passive)
					and utility_kind not in (None, 0),
				'take_up_space': bool(_int_field(block, 'takeUpSpace')),
				'is_start_card': bool(_int_field(block, 'isStartCard')),
				'tags': _parse_my_tags(block),
				'source_file': fname,
			}
	return pool


CARD_POOL_40 = _load_card_pool_40()


def dump_card_pool_40(out_path):
	"""Write the trial card table as JSON and print a load summary.

	Prints the review lists required by plan Step 1: rarity dir-vs-field
	mismatch, creatures with missing/zero printed ATK, non-creatures, utility
	passives, and non-instantiable cards (takeUpSpace=false).
	"""
	os.makedirs(os.path.dirname(out_path), exist_ok=True)
	with open(out_path, 'w', encoding='utf-8') as f:
		json.dump(sorted(CARD_POOL_40.values(), key=lambda c: c['cid']),
				  f, ensure_ascii=False, indent='\t')
		f.write('\n')

	entries = list(CARD_POOL_40.values())
	print(f'[sim4] trial scope dirs: {", ".join(TRIAL_RARITY_DIRS)}')
	print(f'[sim4] card table size: {len(entries)}')
	creatures = [c for c in entries if c['is_creature']]
	print(f"[sim4] creatures: {len(creatures)}, "
		  f"non-creatures: {len(entries) - len(creatures)}")

	def _names(items):
		return ', '.join(f"{c['cid']}({c['display_name']})" for c in items) or '(none)'

	rarity_mismatch = [c for c in entries
					   if c['rarity_field'] is not None
					   and not c['rarity'].startswith(str(c['rarity_field']) + '_')]
	print(f'[sim4] rarity dir-vs-field mismatch: {_names(rarity_mismatch)}')
	atk_missing = [c for c in creatures if c['printed_attack'] is None]
	print(f'[sim4] creatures with printedAttack missing: {_names(atk_missing)}')
	atk_zero = [c for c in creatures if c['printed_attack'] == 0]
	print(f'[sim4] creatures with printedAttack == 0 (review): {_names(atk_zero)}')
	passives = [c for c in entries if c['is_utility_passive']]
	print(f'[sim4] utility passives: {_names(passives)}')
	non_instantiable = [c for c in entries if not c['take_up_space']]
	print(f'[sim4] non-instantiable (takeUpSpace=0): {_names(non_instantiable)}')
	print(f'[sim4] card table written: {out_path}')
	return entries


# ---------------------------------------------------------------------------
# 4.0 state & zone engine (Step 2, Common-scope trial)
# Semantics: docs/4.0_Glossary.md (2026-08-28). Zones on the combined deck:
#   graveyard = deck[0:start_card_index]   (bottom side, revealed cards rest)
#   start card = deck[start_card_index]
#   alive deck = deck[start_card_index+1:] (unrevealed; reveals pop the top)
# Passive cards sit on the graveyard side right under the start card after
# every shuffle, are never revealed and never move. Echo-bounce cards never
# enter the graveyard and cannot be revived.
# ---------------------------------------------------------------------------
BELIEVER_TOKEN_CID = 'RIFT'
CURSE_TOKEN_CID = 'JU_ON'
MAX_CHAIN_DEPTH_40 = 99  # aligned with Unity EffectChainManager


class _TriggerEntry:
	"""One registry entry: a trigger owned by a card (or engine-instrumented
	when card is None), gated by a source predicate on the event payload."""

	__slots__ = ('card', 'handler', 'pred', 'once', 'done')

	def __init__(self, card, handler, pred=None, once=False):
		self.card = card
		self.handler = handler
		self.pred = pred
		self.once = once
		self.done = False


class EventBus40:
	"""Step 4 trigger framework.

	- Registry keyed by timing word (glossary 触发时机): entries are
	  (card, handler, source-predicate). Passive cards simply hold entries
	  with no per-round cap (2026-08-28 ruling: every matching event fires).
	- Source predicates filter by payload (faction / creature / believer /
	  curse / deathrattle-self / enhanced, ...). A failed predicate does NOT
	  consume the chain guard.
	- Anti-loop: the same (card, handler) fires at most once per OPEN chain
	  (root dispatch tree), mirroring Unity EffectChainManager; depth cap
	  default 99 (MAX_CHAIN_DEPTH_40, per-state configurable).
	- One-shot entries (once=True) model nested triggers (FINAL_ESCORT):
	  the outer trigger's handler subscribes a pending one-shot listener
	  that fires once at the inner timing, then removes itself.
	"""

	def __init__(self):
		self.listeners = defaultdict(list)
		self.depth = 0
		self.max_depth = MAX_CHAIN_DEPTH_40
		self.fired = Counter()
		self.chain_guard = set()

	def subscribe(self, event, handler):
		"""Engine-instrumentation listener (no card, no guard)."""
		self.listeners[event].append(_TriggerEntry(None, handler))

	def subscribe_trigger(self, event, card, handler, pred=None, once=False):
		self.listeners[event].append(_TriggerEntry(card, handler, pred, once))

	def fire(self, event, payload):
		self.fired[event] += 1
		if self.depth >= self.max_depth:
			return
		if self.depth == 0:
			self.chain_guard.clear()
		self.depth += 1
		try:
			for entry in list(self.listeners.get(event, ())):
				if entry.once and entry.done:
					continue
				if entry.card is not None and entry.card.exiled:
					continue
				if entry.pred is not None and not entry.pred(payload):
					continue
				key = None
				if entry.card is not None:
					key = (entry.card.id, id(entry.handler))
					if key in self.chain_guard:
						continue
					self.chain_guard.add(key)
				try:
					entry.handler(payload)
				finally:
					if entry.once:
						entry.done = True
						entries = self.listeners.get(event, [])
						if entry in entries:
							entries.remove(entry)
		finally:
			self.depth -= 1
			if self.depth == 0:
				self.chain_guard.clear()


class Card40:
	__slots__ = ('id', 'cid', 'owner', 'atk', 'attack_times', 'enhance_total',
				 'card_type', 'is_believer', 'is_passive', 'echo_bounce',
				 'echo_counter', 'exiled', 'reveal_count', 'is_start', 'tags')
	_id_counter = 0

	def __init__(self, cid, owner, atk=0, attack_times=1, card_type=0,
				 is_believer=False, is_passive=False, is_start=False,
				 tags=None):
		Card40._id_counter += 1
		self.id = Card40._id_counter
		self.cid = cid
		self.owner = owner
		# atk replaces the 3.0 `power` semantics: current attack value
		# (printed attack + enhancements, 4.0 强化N).
		self.atk = atk
		self.attack_times = attack_times
		self.enhance_total = 0
		self.card_type = card_type
		self.is_believer = is_believer
		self.is_passive = is_passive
		self.echo_bounce = False
		self.echo_counter = 0
		self.exiled = False
		self.reveal_count = 0
		self.is_start = is_start
		self.tags = list(tags or [])

	@property
	def is_enhanced(self):
		# 【被强化】 predicate: has received at least one enhance.
		return self.enhance_total > 0

	def __repr__(self):
		return f"Card40({self.cid},{self.owner},atk={self.atk})"


def build_card40(cid, owner):
	"""Build a Card40 from the trial card table (tokens use table defaults)."""
	if cid == BELIEVER_TOKEN_CID:
		return Card40(cid, owner, is_believer=True)
	if cid == CURSE_TOKEN_CID:
		# JU_ON is the only CardType.Status token (curse-type).
		return Card40(cid, owner, card_type=2)
	info = CARD_POOL_40.get(cid)
	if info is None:
		raise KeyError(f'build_card40: cid not in trial table: {cid}')
	return Card40(cid, owner,
				  atk=info['printed_attack'] or 0,
				  attack_times=1 + (info['extra_attack_times'] or 0),
				  card_type=info['card_type'] or 0,
				  is_passive=info['is_passive'],
				  tags=info['tags'])


class GameState40:
	"""Combined-deck state for one 4.0 combat (both sides share the deck)."""

	def __init__(self, hp_max=None):
		self.deck = []
		self.start_card_index = -1
		self.hp = {'A': hp_max, 'B': hp_max}  # None = no HP limit
		self.round_num = 0
		self.bus = EventBus40()
		self.divergence = None  # reason string when an engine guard trips
		self.rng = random.Random()  # injected/randomized per combat in drivers
		# "Every card triggers once per round" (Start Card AlwaysBottom):
		# keys (card.id, timing) consumed for the current round.
		self.round_triggered = set()

	def _trip(self, reason):
		if self.divergence is None:
			self.divergence = reason


def graveyard_cards(state):
	return state.deck[:state.start_card_index]


def alive_cards(state):
	return state.deck[state.start_card_index + 1:]


def deck_index(state, card):
	return state.deck.index(card)


def is_in_graveyard_40(state, card):
	return deck_index(state, card) < state.start_card_index


def setup_start_card_40(state, card):
	card.is_start = True
	state.deck.insert(0, card)
	state.start_card_index = 0
	return card


def _remove_from_deck(state, card):
	idx = deck_index(state, card)
	del state.deck[idx]
	if idx < state.start_card_index:
		state.start_card_index -= 1
	return idx


def bury_card_40(state, card):
	"""Bury to deck bottom (index 0, graveyard). Fires 'any_buried'."""
	if card.exiled:
		return card
	if card.echo_bounce:
		# Echo-bounce cards never enter the graveyard (glossary 回响X).
		state._trip(f'bury on echo-bounce card {card.cid}')
		return card
	if card.is_passive:
		state._trip(f'bury on passive card {card.cid}')
		return card
	if card in state.deck and is_in_graveyard_40(state, card):
		return card  # already buried, no double-bury
	# A revealed card sits outside the deck (reveal zone); the guards above
	# still apply, it just re-enters straight at the bottom.
	if card in state.deck:
		_remove_from_deck(state, card)
	state.deck.insert(0, card)
	state.start_card_index += 1
	state.bus.fire('any_buried', {'card': card})
	return card


def reveal_top_40(state):
	"""Pop the alive-deck top into the reveal zone (outside the deck).
	None = alive deck drained (normal round end: only start card + passives
	remain below)."""
	if state.start_card_index + 1 >= len(state.deck):
		return None
	card = state.deck.pop()
	card.reveal_count += 1
	state.bus.fire('revealed', {'card': card})
	return card


def revive_card_40(state, card):
	"""Graveyard -> deck top; fires 'awaken'. Passive/echo cards excluded."""
	if card.is_passive or card.echo_bounce:
		state._trip(f'revive on passive/echo card {card.cid}')
		return card
	if not is_in_graveyard_40(state, card):
		return card
	_remove_from_deck(state, card)
	state.deck.append(card)
	state.bus.fire('awaken', {'card': card})
	return card


def delayed_revive_card_40(state, card):
	"""Revive variant landing at 起始卡前一格 (one above the start card)."""
	if card.is_passive or card.echo_bounce:
		state._trip(f'delayed revive on passive/echo card {card.cid}')
		return card
	if not is_in_graveyard_40(state, card):
		return card
	_remove_from_deck(state, card)
	state.deck.insert(state.start_card_index + 1, card)
	state.bus.fire('awaken', {'card': card})
	return card


def echo_bounce_card_40(state, card):
	"""Revealed echo card bounces to 起始卡前一格; no graveyard, no awaken."""
	card.echo_bounce = True
	state.deck.insert(state.start_card_index + 1, card)
	return card


def stage_to_top_40(state, card):
	"""置顶: move an UNREVEALED (alive-zone) card to the deck top."""
	if is_in_graveyard_40(state, card):
		state._trip(f'stage on graveyard card {card.cid} (use revive)')
		return card
	if card.is_passive:
		state._trip(f'stage on passive card {card.cid}')
		return card
	_remove_from_deck(state, card)
	state.deck.append(card)
	return card


def add_alive_40(state, card, position='bottom'):
	"""Add an unrevealed card into the alive zone.

	position 'bottom' = index start_card_index+1 (revealed last),
	position 'top' = deck top (revealed next).
	"""
	if position == 'top':
		state.deck.append(card)
	else:
		state.deck.insert(state.start_card_index + 1, card)
	return card


def exile_card_40(state, card):
	_remove_from_deck(state, card)
	card.exiled = True
	state.bus.fire('any_exiled', {'card': card})
	return card


def give_enhance_40(state, card, amount, source=None):
	"""强化N: atk +N (permanent), tracks the 【被强化】 predicate."""
	if amount <= 0:
		return card
	card.atk += amount
	card.enhance_total += amount
	state.bus.fire('enhanced', {'card': card, 'amount': amount,
								'source': source})
	return card


def place_passives_40(state):
	"""Force every passive to the graveyard side right under the start card.

	Glossary 被动: after each shuffle passives sit 起始卡之后（墓地侧）,
	never revealed, never moved. Called after every shuffle.
	"""
	passives = [c for c in state.deck if c.is_passive]
	for p in passives:
		_remove_from_deck(state, p)
	for p in passives:
		state.deck.insert(state.start_card_index, p)
		state.start_card_index += 1
	return passives


def shuffle_round_40(state, rng):
	"""Round boundary (Start Card AlwaysBottom config, 2026-09-08): every
	non-start card is shuffled and the start card returns to the bottom
	(index 0), so each round reveals every card exactly once. The graveyard
	is the waiting area for the next pass, not a dead zone. Passives are
	re-forced below the start card (glossary 被动)."""
	state.bus.fire('round_end', {'round': state.round_num})
	start_card = state.deck[state.start_card_index]
	others = [c for c in state.deck if c is not start_card]
	rng.shuffle(others)
	state.deck = [start_card] + others
	state.start_card_index = 0
	place_passives_40(state)
	state.round_num += 1
	state.round_triggered.clear()
	state.bus.fire('round_start', {'round': state.round_num})


def resolve_attack_40(state, attacker, target_state_owner=None, terms=None,
					  times=None):
	"""Unified attack resolution (mirrors the single-entry idea of Unity
	AttackResolverSource).

	terms: list of ('self_atk',) | ('fixed', n); default [('self_atk',)].
	times: number of hits; default attacker.attack_times.
	N hits apply as ONE resolution with a single reaction window (xN rule).
	"""
	if attacker.exiled:
		return 0
	if times is None:
		times = attacker.attack_times
	if terms is None:
		terms = [('self_atk',)]
	total_per_hit = 0
	for term in terms:
		if term[0] == 'self_atk':
			total_per_hit += attacker.atk
		elif term[0] == 'fixed':
			total_per_hit += term[1]
		else:
			state._trip(f'unknown attack term {term!r}')
			return 0
	total = 0
	target = target_state_owner or opp(attacker.owner)
	for segment in range(times):
		if state.hp.get(target) is not None:
			state.hp[target] -= total_per_hit
		total += total_per_hit
		# 2026-09-05 ruling: attack events fire PER SEGMENT (攻击xN -> N
		# reaction windows), not once per resolution.
		state.bus.fire('attack', {'attacker': attacker,
								  'target_owner': target,
								  'total': total_per_hit,
								  'segment': segment + 1, 'segments': times})
	return total


def check_invariants_40(state):
	"""Return a list of zone-invariant violations (empty list = healthy)."""
	violations = []
	seen_ids = set()
	for idx, card in enumerate(state.deck):
		if card.id in seen_ids:
			violations.append(f'duplicate card id {card.id} ({card.cid})')
		seen_ids.add(card.id)
		if card.is_start and idx != state.start_card_index:
			violations.append(f'start card at {idx}, '
							  f'expected {state.start_card_index}')
		if card.echo_bounce and idx < state.start_card_index:
			violations.append(f'echo card {card.cid} inside graveyard')
		if card.is_passive and idx >= state.start_card_index:
			violations.append(f'passive {card.cid} outside graveyard side')
	if state.start_card_index >= len(state.deck):
		violations.append('start_card_index out of range')
	return violations


def selftest_40():
	"""Step 2 acceptance: exercise zones/invariants/attack with real cards."""
	import random as _random
	rng = _random.Random(40)
	state = GameState40(hp_max=25)
	setup_start_card_40(state, Card40('START', 'N', is_start=True))
	deck_cids = [
		('A', 'GRAVE_FIST'), ('B', 'TWIN_STRIKER'),
		('A', 'SPIKE_SKELETON_4.0'),
		('A', 'WOKEN_BLADE'), ('B', 'JU_ON'), ('A', 'RIFT_ACOLYTE'),
		('B', 'GRAVE_DREDGER'), ('A', 'SYSTEM_INCREASE_HP_MAX'),
		('B', 'CURSE_REVIVER'), ('A', 'RIFT_INSECT_4.0'),
	]
	for owner, cid in deck_cids:
		add_alive_40(state, build_card40(cid, owner), position='top')
	place_passives_40(state)  # deck assembly -> passive placement (pre-round)
	assert len(check_invariants_40(state)) == 0, 'setup invariants'
	passive = next(c for c in state.deck if c.is_passive)
	awakened = []
	state.bus.subscribe('awaken', lambda p: awakened.append(p['card'].cid))

	shuffle_round_40(state, rng)
	assert graveyard_cards(state) == [passive], 'passive placed under start'
	assert len(alive_cards(state)) == len(deck_cids) - 1, 'alive excludes passive'

	# Reveal & resolve three cards: attack entry + bury flow.
	fist = next(c for c in alive_cards(state) if c.cid == 'GRAVE_FIST')
	dmg = resolve_attack_40(state, fist)
	assert dmg == 4 and state.hp['B'] == 21, f'FIST attack {dmg}'
	bury_card_40(state, reveal_top_40(state))
	bury_card_40(state, reveal_top_40(state))
	spike = next(c for c in state.deck if c.cid == 'SPIKE_SKELETON_4.0')
	if not is_in_graveyard_40(state, spike):
		bury_card_40(state, spike)
	assert is_in_graveyard_40(state, spike)
	assert state.start_card_index == 4, 'graveyard = 3 buries + passive'

	# Revive pulls to top and fires awaken.
	revive_card_40(state, spike)
	assert awakened == ['SPIKE_SKELETON_4.0'] and not is_in_graveyard_40(state, spike)
	assert state.deck[-1] is spike, 'revived card at deck top'

	# Enhance + multi-term multi-hit attack: WOKEN (atk2,x1 -> +1, +fixed1, x2).
	woken = next(c for c in alive_cards(state) if c.cid == 'WOKEN_BLADE')
	enhanced_log = []
	state.bus.subscribe('enhanced', lambda p: enhanced_log.append(p['card'].cid))
	give_enhance_40(state, woken, 1)
	assert woken.atk == 3 and woken.is_enhanced and enhanced_log == ['WOKEN_BLADE']
	woken.attack_times = 2
	dmg = resolve_attack_40(state, woken, terms=[('self_atk',), ('fixed', 1)])
	assert dmg == 2 * (3 + 1), f'multi-term damage {dmg}'

	# Echo bounce: revealed card returns to 起始卡前一格, never revivable.
	echoed = build_card40('TWIN_STRIKER', 'B')
	add_alive_40(state, echoed, position='top')
	assert reveal_top_40(state) is echoed
	echo_bounce_card_40(state, echoed)
	assert not is_in_graveyard_40(state, echoed)
	assert state.deck[state.start_card_index + 1] is echoed
	revive_card_40(state, echoed)  # guard must trip, not move
	assert state.divergence is not None

	assert len(check_invariants_40(state)) == 0, check_invariants_40(state)
	print('[sim4] selftest 40 PASS: zones/invariants/attack/echo/bus all ok')
	print(f"[sim4] events fired: {dict(state.bus.fired)}")


# ---------------------------------------------------------------------------
# 4.0 verb library (Step 3, Common-scope subset)
# Semantics: docs/4.0_Glossary.md (2026-08-28). Key rulings encoded here:
#   强化N: N is the AMOUNT; each clause picks ONE random target.
#   埋葬N[目标]: N is the quantity; omitted predicate = random pick
#   (no player choice points in combat).
#   Enhance-style pools mirror Unity StatusEffectGiverEffect.CollectFriendlyCards:
#   the WHOLE combined deck (any zone) plus the reveal-zone card.
#   Revive pools are graveyard-side only (revive's source zone).
#   Empty target pool -> verb fizzles (returns empty / None), no crash.
# Deferred to the U/R batch: 弱化/置顶/延后/复制自身/攻击力翻倍/攻击力=X/
#   攻击次数+N/回响X/触发X的遗言/交换/让墓地友方攻击 and all RELIC_ extended
#   constructions. (回响 has its Step 2 primitive: echo_bounce_card_40.)
# ---------------------------------------------------------------------------
CARD_TYPE_STATUS = 2  # EnumStorage.CardType.Status (JU_ON curse tokens)


def pred_creature(c):
	return c.card_type == CARD_TYPE_CREATURE


def pred_non_creature(c):
	return c.card_type != CARD_TYPE_CREATURE


def pred_enhanced(c):
	return c.is_enhanced


def pred_believer_tag(c):
	# 复活1张tag为[信徒]的友方卡: believers by identity or by Believer tag.
	return c.is_believer or 'Believer' in c.tags


def pred_curse(c):
	# 敌方诅咒 = JU_ON-style Status tokens.
	return c.card_type == CARD_TYPE_STATUS


def deck_pool_40(state, source=None):
	"""Whole combined deck plus the reveal-zone card (Unity pool semantics)."""
	pool = list(state.deck)
	if source is not None and source not in pool:
		pool.append(source)
	return pool


def faction_filter(cards, faction):
	if faction is None:
		return list(cards)
	return [c for c in cards if c.owner == faction]


def select_targets_40(state, pool, predicate, rng, count=1):
	candidates = [c for c in pool if predicate(c)]
	rng.shuffle(candidates)
	return candidates[:count]


def verb_attack(state, card, times=None, terms=None):
	"""攻击 / 攻击xN: N segments via the unified attack entry."""
	return resolve_attack_40(state, card, terms=terms, times=times)


def verb_enhance(state, source, amount, predicate, rng, faction=None):
	"""强化N[目标]: ONE random target gets +amount (N = amount, not count)."""
	pool = faction_filter(deck_pool_40(state, source), faction)
	targets = select_targets_40(state, pool, predicate, rng, count=1)
	if not targets:
		return None
	give_enhance_40(state, targets[0], amount, source=source)
	return targets[0]


def verb_bury_targets(state, source, predicate, rng, faction=None, count=1):
	"""埋葬N[目标]: bury N random matching cards (N = quantity). Passives
	are immovable (glossary 被动) and never valid move targets."""
	pool = faction_filter(deck_pool_40(state, source), faction)
	pool = [c for c in pool if not c.is_passive]
	targets = select_targets_40(state, pool, predicate, rng, count=count)
	for t in targets:
		bury_card_40(state, t)
	return targets


def verb_bury_deck_top(state, source, count):
	"""埋葬卡组顶N卡: bury the top N UNREVEALED cards (no reveal triggers)."""
	buried = []
	for _ in range(min(count, len(alive_cards(state)))):
		card = state.deck.pop()
		bury_card_40(state, card)
		buried.append(card)
	return buried


def verb_revive(state, source, predicate, rng, faction=None, count=1):
	"""复活N[目标]: graveyard-side pool only (passives excluded — they are
	immovable and non-revivable); fires awaken per card."""
	pool = [c for c in graveyard_cards(state) if not c.is_passive]
	pool = faction_filter(pool, faction)
	targets = select_targets_40(state, pool, predicate, rng, count=count)
	for t in targets:
		revive_card_40(state, t)
	return targets


def verb_spawn_believer(state, owner, count):
	"""生成N信徒: N RIFT tokens join the alive zone (revealed last)."""
	spawned = []
	for _ in range(count):
		token = build_card40(BELIEVER_TOKEN_CID, owner)
		add_alive_40(state, token, position='bottom')
		spawned.append(token)
	return spawned


def verb_enhance_enemy_curse(state, source, amount):
	"""强化敌方诅咒 (curse-axis entry point, user ruling 2026-09-09).

	The enemy deck holds AT MOST ONE JU_ON, and a curse exists only because
	a curse-enhancing card created it: if absent, spawn one into the enemy's
	alive zone; then enhance it (+amount) wherever it sits (graveyard
	included). Exiled curses count as gone — a new one may be created.
	"""
	enemy = opp(source.owner)
	curses = [c for c in state.deck
			  if c.cid == CURSE_TOKEN_CID and c.owner == enemy]
	if curses:
		target = curses[0]
	else:
		target = build_card40(CURSE_TOKEN_CID, enemy)
		add_alive_40(state, target, position='bottom')
	give_enhance_40(state, target, amount, source=source)
	return target


def verb_exile(state, source, card):
	"""放逐: remove from the game; loses all granted effects (modeled as
	exiled flag; revival pool never sees exiled cards)."""
	return exile_card_40(state, card)


def selftest_verbs_40():
	"""Step 3 acceptance: exercise every Common-scope verb with real cards."""
	import random as _random
	rng = _random.Random(340)
	state = GameState40(hp_max=25)
	setup_start_card_40(state, Card40('START', 'N', is_start=True))

	# Side A: three creatures + one non-creature; side B: curse + creature.
	a_cards = [build_card40(cid, 'A') for cid in
			   ('BLACKSMITH_4.0', 'LAST_GIFT', 'BEAST_REVIVER', 'WAR_TRAINER')]
	b_cards = [build_card40(cid, 'B') for cid in ('JU_ON', 'TWIN_STRIKER')]
	for card in a_cards + b_cards:
		add_alive_40(state, card, position='top')
	blacksmith = a_cards[0]

	# 强化N semantics: N is amount, ONE random friendly creature target.
	target = verb_enhance(state, blacksmith, 1, pred_creature, rng,
						  faction='A')
	assert target is not None and target.card_type == CARD_TYPE_CREATURE
	assert target.atk == (2 if target.cid == 'BLACKSMITH_4.0' else
						  (1 if target.cid == 'LAST_GIFT' else 1)) + 1
	assert target.is_enhanced and target.enhance_total == 1
	enhanced_creature = target

	# 敌方诅咒强化: curse (any zone) gets +amount.
	curse = b_cards[0]
	bury_card_40(state, curse)  # curses live in the graveyard after revealing
	got = verb_enhance(state, blacksmith, 2, pred_curse, rng, faction='B')
	assert got is curse and curse.atk == 2 and curse.is_enhanced

	# 埋葬卡组顶N卡: top 3 unrevealed buried without reveal triggers.
	reveals_before = sum(c.reveal_count for c in state.deck)
	buried = verb_bury_deck_top(state, blacksmith, 3)
	assert len(buried) == 3
	assert all(is_in_graveyard_40(state, c) for c in buried)
	assert sum(c.reveal_count for c in state.deck) == reveals_before
	assert state.bus.fired['any_buried'] >= 3

	# 埋葬N[目标]: one random friendly card (any zone) buried.
	more = verb_bury_targets(state, blacksmith, lambda c: True, rng,
							 faction='A', count=1)
	assert len(more) == 1 and is_in_graveyard_40(state, more[0])

	# 复活N predicates against the now-populated graveyard.
	# creature: BEAST_REVIVER-style
	got = verb_revive(state, blacksmith, pred_creature, rng, faction='A')
	assert len(got) == 1 and got[0].card_type == CARD_TYPE_CREATURE
	assert not is_in_graveyard_40(state, got[0])
	assert state.deck[-1] is got[0], 'revive lands on deck top'
	# non-creature: SPIRIT_CALLER-style
	got = verb_revive(state, blacksmith, pred_non_creature, rng, faction='A')
	assert len(got) == 1 and got[0].card_type != CARD_TYPE_CREATURE
	# enhanced creature: ELITE_REVIVER-style (bury the enhanced one first)
	bury_card_40(state, enhanced_creature)
	got = verb_revive(state, blacksmith,
					  lambda c: pred_creature(c) and pred_enhanced(c),
					  rng, faction='A')
	assert len(got) == 1 and got[0] is enhanced_creature
	# enemy curse: CURSE_REVIVER-style
	got = verb_revive(state, blacksmith, pred_curse, rng, faction='B')
	assert len(got) == 1 and got[0] is curse
	# fizzle on empty pool (复活: 空墓地则失效)
	assert verb_revive(state, blacksmith, pred_curse, rng,
					   faction='B') == []

	# 生成N信徒: RIFT tokens join alive zone bottom with Believer identity.
	spawned = verb_spawn_believer(state, 'A', 2)
	assert len(spawned) == 2
	assert all(c.is_believer and c.cid == 'RIFT' for c in spawned)
	assert all(c in alive_cards(state) for c in spawned)
	# tag为[信徒] revival pool matches tokens by identity.
	bury_card_40(state, spawned[0])
	got = verb_revive(state, blacksmith, pred_believer_tag, rng, faction='A')
	assert len(got) == 1 and got[0] is spawned[0]

	# 放逐: exiled card leaves the deck for good.
	victim = verb_bury_targets(state, blacksmith, lambda c: c.owner == 'B',
							   rng, faction='B', count=1)
	assert len(victim) == 1
	verb_exile(state, blacksmith, victim[0])
	assert victim[0].exiled and victim[0] not in state.deck

	# 攻击xN via verb (GRAVE_PUNCH-4.0-style: atk 2, x2 segments).
	punch = build_card40('GRAVE_PUNCH_4.0', 'A')
	add_alive_40(state, punch, position='top')
	assert punch.attack_times == 2
	dmg = verb_attack(state, punch)
	assert dmg == 4 and state.hp['B'] == 25 - 4, f'attack xN {dmg}'

	assert len(check_invariants_40(state)) == 0, check_invariants_40(state)
	print('[sim4] selftest verbs 40 PASS: attack/enhance/bury/revive/'
		  'spawn/exile + predicates all ok')


def selftest_triggers_40():
	"""Step 4 acceptance: registry, source predicates, anti-loop, depth cap,
	one-shot nested listeners, per-segment attack events."""
	import random as _random
	rng = _random.Random(440)
	state = GameState40(hp_max=25)
	setup_start_card_40(state, Card40('START', 'N', is_start=True))
	soldier = build_card40('SOLDIER_SKELETON_4.0', 'A')
	waker = build_card40('WAKING_FIGHTER', 'A')
	fist = build_card40('GRAVE_FIST', 'A')
	twin = build_card40('TWIN_STRIKER', 'B')
	insect = build_card40('RIFT_INSECT_4.0', 'A')
	# Fodder for the burial/exile sequences below.
	fodder = [build_card40(cid, owner) for cid, owner in
			  (('RIFT_ACOLYTE', 'A'), ('GRAVE_DREDGER', 'B'),
			   ('CURSE_REVIVER', 'A'), ('WOKEN_BLADE', 'B'))]
	for card in (soldier, waker, fist, twin, insect) + tuple(fodder):
		add_alive_40(state, card, position='top')

	# 1) 遗言 self-scoped: pred filters to the card's own burial.
	deathrattles = []
	state.bus.subscribe_trigger('any_buried', soldier,
								lambda p: deathrattles.append(p['card'].cid),
								pred=lambda p: p['card'] is soldier)
	bury_card_40(state, fist)
	assert deathrattles == [], 'other cards burial must not trigger'
	bury_card_40(state, soldier)
	assert deathrattles == ['SOLDIER_SKELETON_4.0']

	# 2) faction predicate (友方苏醒时 style).
	friendly_awakens = []
	state.bus.subscribe_trigger('awaken', waker,
								lambda p: friendly_awakens.append(p['card'].cid),
								pred=lambda p: p['card'].owner == 'A')
	revive_card_40(state, twin)  # enemy awaken: not counted
	assert friendly_awakens == []
	revive_card_40(state, soldier)  # friendly awaken: counted
	assert friendly_awakens == ['SOLDIER_SKELETON_4.0']

	# 3) anti-loop: a handler re-firing its own timing within the same
	# open chain must not run twice.
	loop_calls = []
	def retrigger(payload):
		loop_calls.append(1)
		state.bus.fire('any_buried', payload)
	state.bus.subscribe_trigger('any_buried', twin, retrigger,
								pred=lambda p: True)
	bury_card_40(state, insect)  # root dispatch; nested fire is guarded
	assert loop_calls == [1], f'anti-loop broken: {len(loop_calls)} runs'

	# 3b) exiled cards hold no live triggers.
	verb_exile(state, twin, twin)
	bury_card_40(state, reveal_top_40(state))  # any other burial
	assert len(loop_calls) == 1, 'exiled card trigger must be skipped'

	# 4) depth cap is configurable and stops runaway chains.
	state2 = GameState40()
	state2.bus.max_depth = 4
	ping_runs = []
	def ping(payload):
		ping_runs.append(1)
		state2.bus.fire('ping', payload)
	state2.bus.subscribe('ping', ping)
	state2.bus.fire('ping', {})
	assert len(ping_runs) == 4, f'depth cap: {len(ping_runs)} runs'

	# 5) one-shot nested listener (FINAL_ESCORT mechanism): register on the
	# outer timing, fires once at the inner timing, then removes itself.
	escorts = []
	def register_escort(payload):
		state.bus.subscribe_trigger('any_buried', waker,
									lambda p: escorts.append(p['card'].cid),
									pred=lambda p: True, once=True)
	state.bus.subscribe_trigger('revealed', waker, register_escort,
								pred=lambda p: p['card'] is waker)
	stage_to_top_40(state, waker)  # 置顶: bring waker to the deck top
	assert reveal_top_40(state) is waker  # arms the one-shot listener
	bury_card_40(state, waker)
	assert escorts == ['WAKING_FIGHTER']
	bury_card_40(state, reveal_top_40(state))
	assert escorts == ['WAKING_FIGHTER'], 'one-shot must fire exactly once'

	# 6) passives fire on EVERY matching event, no per-round cap.
	passive_hits = []
	state.bus.subscribe('any_buried', lambda p: passive_hits.append(1))
	bury_card_40(state, reveal_top_40(state))
	bury_card_40(state, reveal_top_40(state))
	assert len(passive_hits) == 2

	# 7) attack events fire per segment (2026-09-05 ruling).
	segments = []
	state.bus.subscribe('attack', lambda p: segments.append(p['segment']))
	punch = build_card40('GRAVE_PUNCH_4.0', 'A')
	add_alive_40(state, punch, position='top')
	verb_attack(state, punch)  # 攻击x2
	assert segments == [1, 2], f'per-segment events: {segments}'

	assert len(check_invariants_40(state)) == 0, check_invariants_40(state)
	print('[sim4] selftest triggers 40 PASS: registry/predicates/anti-loop/'
		  'depth-cap/one-shot/per-segment all ok')


# ---------------------------------------------------------------------------
# Step 5: per-card handlers (Common trial, 30 cards + JU_ON token)
# Every handler is hand-written per cardTypeID (no desc NLP); the original
# cardDesc is quoted next to each handler for review. Semantics follow
# docs/4.0_Glossary.md; approximations are declared in
# docs/Sim4_Approximation_Ledger.md. Clause order inside a handler follows
# desc clause order.
# ---------------------------------------------------------------------------
HANDLERS_40 = {}
LEDGERED_40 = {
	# 被动：生命值上限+4 — static effect applied at combat setup
	# (apply_static_passives_40); no trigger handler.
	'SYSTEM_INCREASE_HP_MAX',
	# 卡位+1，放逐自身 — takeUpSpace=0: never instantiated in combat
	# (shop-only deck-cap meter card).
	'SYSTEM_INCREASE_DECK_SIZE_LITE',
	# Shop passives: no combat effect; occupy deck slots as never-revealed
	# passives (graveyard side), diluting the pool — modeled by placement.
	'UTILITY_INCOME_1', 'UTILITY_DISCOUNT_1', 'UTILITY_ODDS_1',
	'UTILITY_OPTION_1', 'UTILITY_REROLL_1', 'UTILITY_SLOT_U_1',
}


def _handler40(cid, **kw):
	HANDLERS_40[cid] = kw


def _revive_pred(*preds):
	return lambda c: all(p(c) for p in preds)


# AVENGER_4.0 厉鬼: 攻击x2；遗言：强化自身1
def _avenger_reveal(state, card):
	verb_attack(state, card)  # attack_times = 2 from prefab


def _avenger_buried(state, card):
	give_enhance_40(state, card, 1, source=card)


_handler40('AVENGER_4.0', on_revealed=_avenger_reveal,
		   on_buried=_avenger_buried)


# BEAST_REVIVER 笼里的东西/马戏团的地下室: 攻击；复活1生物友方
def _beast_reviver_reveal(state, card):
	verb_attack(state, card)
	verb_revive(state, card, pred_creature, state.rng, faction=card.owner)


_handler40('BEAST_REVIVER', on_revealed=_beast_reviver_reveal)


# BLACKSMITH_4.0 巷口的磨刀人/骨匠: 攻击；强化1友方生物
def _blacksmith_reveal(state, card):
	verb_attack(state, card)
	verb_enhance(state, card, 1, pred_creature, state.rng, faction=card.owner)


_handler40('BLACKSMITH_4.0', on_revealed=_blacksmith_reveal)


# CURSE_REVIVER 洗不掉的印子/收蛊人: 攻击；复活1敌方诅咒
def _curse_reviver_reveal(state, card):
	verb_attack(state, card)
	verb_revive(state, card, pred_curse, state.rng, faction=opp(card.owner))


_handler40('CURSE_REVIVER', on_revealed=_curse_reviver_reveal)


# ELITE_REVIVER 描过金的牌位/镀金圣髑: 攻击；复活1被强化过的友方生物
def _elite_reviver_reveal(state, card):
	verb_attack(state, card)
	verb_revive(state, card, _revive_pred(pred_creature, pred_enhanced),
				state.rng, faction=card.owner)


_handler40('ELITE_REVIVER', on_revealed=_elite_reviver_reveal)


# GRAVE_HEXER 白事先生/降头师 (非生物, no attack): 复活1友方；强化1敌方诅咒
def _grave_hexer_reveal(state, card):
	verb_revive(state, card, lambda c: True, state.rng, faction=card.owner)
	verb_enhance_enemy_curse(state, card, 1)


_handler40('GRAVE_HEXER', on_revealed=_grave_hexer_reveal)


# GRAVE_DREDGER 还没停的挖掘机/掘墓人: 攻击；埋葬卡组顶3卡
def _grave_dredger_reveal(state, card):
	verb_attack(state, card)
	verb_bury_deck_top(state, card, 3)


_handler40('GRAVE_DREDGER', on_revealed=_grave_dredger_reveal)


# GRAVE_FIST 替死鬼/血祭: 埋葬1友方；攻击
def _grave_fist_reveal(state, card):
	verb_bury_targets(state, card, lambda c: True, state.rng,
					  faction=card.owner)
	verb_attack(state, card)


_handler40('GRAVE_FIST', on_revealed=_grave_fist_reveal)


# GRAVE_PUNCH_4.0 没有指纹的凶器/剜心祭: 埋葬1友方；攻击x2
def _grave_punch_reveal(state, card):
	verb_bury_targets(state, card, lambda c: True, state.rng,
					  faction=card.owner)
	verb_attack(state, card)  # attack_times = 2 from prefab


_handler40('GRAVE_PUNCH_4.0', on_revealed=_grave_punch_reveal)


# HEXER 对面楼的那场火/蛊婆: 攻击；强化2敌方诅咒
def _hexer_reveal(state, card):
	verb_attack(state, card)
	verb_enhance_enemy_curse(state, card, 2)


_handler40('HEXER', on_revealed=_hexer_reveal)


# LAST_GIFT 遗物整理师/遗赠: 攻击；遗言：强化2友方生物
def _last_gift_reveal(state, card):
	verb_attack(state, card)


def _last_gift_buried(state, card):
	verb_enhance(state, card, 2, pred_creature, state.rng, faction=card.owner)


_handler40('LAST_GIFT', on_revealed=_last_gift_reveal,
		   on_buried=_last_gift_buried)


# RIFT_ACOLYTE 自己签满的签到表/侍僧: 攻击；生成1信徒；苏醒：生成1信徒
def _rift_acolyte_reveal(state, card):
	verb_attack(state, card)
	verb_spawn_believer(state, card.owner, 1)


def _rift_acolyte_awaken(state, card):
	verb_spawn_believer(state, card.owner, 1)


_handler40('RIFT_ACOLYTE', on_revealed=_rift_acolyte_reveal,
		   on_awaken=_rift_acolyte_awaken)


# RIFT_INSECT_4.0 门口多出的鞋印/新皈依者: 攻击；生成1信徒
def _rift_insect_reveal(state, card):
	verb_attack(state, card)
	verb_spawn_believer(state, card.owner, 1)


_handler40('RIFT_INSECT_4.0', on_revealed=_rift_insect_reveal)


# RIFT_SHEPHERD 来接人的黑车/牧羊人: 攻击；复活1张tag为[信徒]的友方卡
def _rift_shepherd_reveal(state, card):
	verb_attack(state, card)
	verb_revive(state, card, pred_believer_tag, state.rng, faction=card.owner)


_handler40('RIFT_SHEPHERD', on_revealed=_rift_shepherd_reveal)


# SACRIFICIAL_SPIRIT 陪葬的活物/活殉 (非生物, no attack):
# 埋葬1友方；强化4敌方诅咒
def _sacrificial_spirit_reveal(state, card):
	verb_bury_targets(state, card, lambda c: True, state.rng,
					  faction=card.owner)
	verb_enhance_enemy_curse(state, card, 4)


_handler40('SACRIFICIAL_SPIRIT', on_revealed=_sacrificial_spirit_reveal)


# SOLDIER_SKELETON_4.0 换岗的哨兵/骸骨哨兵: 攻击；遗言：复活自身
def _soldier_reveal(state, card):
	verb_attack(state, card)


def _soldier_buried(state, card):
	revive_card_40(state, card)  # self-revive, fires own awaken (none)


_handler40('SOLDIER_SKELETON_4.0', on_revealed=_soldier_reveal,
		   on_buried=_soldier_buried)


# SPIKE_SKELETON_4.0 医学院的标本/带刺的标本: 攻击；遗言：攻击x2
def _spike_reveal(state, card):
	verb_attack(state, card)


def _spike_buried(state, card):
	verb_attack(state, card, times=2)  # attacks from the graveyard, stays


_handler40('SPIKE_SKELETON_4.0', on_revealed=_spike_reveal,
		   on_buried=_spike_buried)


# SPIRIT_CALLER 深夜来电/降灵会: 攻击；复活1友方非生物
def _spirit_caller_reveal(state, card):
	verb_attack(state, card)
	verb_revive(state, card, pred_non_creature, state.rng, faction=card.owner)


_handler40('SPIRIT_CALLER', on_revealed=_spirit_caller_reveal)


# TWIN_STRIKER 一对纸人/连体人: 攻击x2
def _twin_striker_reveal(state, card):
	verb_attack(state, card)  # attack_times = 2 from prefab


_handler40('TWIN_STRIKER', on_revealed=_twin_striker_reveal)


# WAKING_FIGHTER 梦游的拳手/回魂尸: 攻击；苏醒：强化自身1
def _waking_fighter_reveal(state, card):
	verb_attack(state, card)


def _waking_fighter_awaken(state, card):
	give_enhance_40(state, card, 1, source=card)


_handler40('WAKING_FIGHTER', on_revealed=_waking_fighter_reveal,
		   on_awaken=_waking_fighter_awaken)


# WOKEN_BLADE 头七的刀/尸变: 攻击；苏醒：攻击x2
def _woken_blade_reveal(state, card):
	verb_attack(state, card)


def _woken_blade_awaken(state, card):
	verb_attack(state, card, times=2)


_handler40('WOKEN_BLADE', on_revealed=_woken_blade_reveal,
		   on_awaken=_woken_blade_awaken)


# WAR_TRAINER 点眼的扎纸匠/开刃 (非生物, no attack): 强化2友方生物
def _war_trainer_reveal(state, card):
	verb_enhance(state, card, 2, pred_creature, state.rng, faction=card.owner)


_handler40('WAR_TRAINER', on_revealed=_war_trainer_reveal)


# JU_ON 诅咒 token (enemy-deck native): 揭晓时攻击自身 — attacks the side
# whose deck it haunts; entry point for the whole curse axis.
def _ju_on_reveal(state, card):
	resolve_attack_40(state, card, target_state_owner=card.owner)


_handler40('JU_ON', on_revealed=_ju_on_reveal)


def register_card_triggers_40(state, card):
	"""Attach the card's reveal/burial/awaken handlers to the registry.
	Returns False for handler-less cards (tokens / ledgered passives).

	Every handler is gated once-per-round ((card, timing) consumed until the
	next shuffle) — the Start Card AlwaysBottom ruling "every card triggers
	once per round" is what keeps self-revive / re-reveal cycles bounded.
	"""

	def gated(timing, f):
		def wrapped(payload):
			key = (card.id, timing)
			if key in state.round_triggered:
				return
			state.round_triggered.add(key)
			f(state, card)
		return wrapped

	h = HANDLERS_40.get(card.cid)
	if not h:
		return False
	if h.get('on_revealed'):
		state.bus.subscribe_trigger(
			'revealed', card,
			gated('revealed', h['on_revealed']),
			pred=lambda p, c=card: p['card'] is c)
	if h.get('on_buried'):
		state.bus.subscribe_trigger(
			'any_buried', card,
			gated('buried', h['on_buried']),
			pred=lambda p, c=card: p['card'] is c)
	if h.get('on_awaken'):
		state.bus.subscribe_trigger(
			'awaken', card,
			gated('awaken', h['on_awaken']),
			pred=lambda p, c=card: p['card'] is c)
	return True


def coverage_report_40():
	"""Zero-silent-skip check: every trial card is handled or ledgered."""
	missing = sorted(set(CARD_POOL_40) - set(HANDLERS_40) - LEDGERED_40)
	extra = sorted((set(HANDLERS_40) | LEDGERED_40) - set(CARD_POOL_40)
				   - {'JU_ON'})
	return missing, extra


def setup_combat_40(deck_a, deck_b, hp_max=None):
	state = GameState40(hp_max=hp_max)
	setup_start_card_40(state, Card40('START', 'N', is_start=True))
	for card in list(deck_a) + list(deck_b):
		add_alive_40(state, card, position='top')
		register_card_triggers_40(state, card)
	apply_static_passives_40(state)
	return state


def apply_static_passives_40(state):
	"""SYSTEM_INCREASE_HP_MAX: max HP +4 per owned instance (HP-cap runs)."""
	for owner in ('A', 'B'):
		bonus = 4 * sum(1 for c in state.deck
						if c.cid == 'SYSTEM_INCREASE_HP_MAX'
						and c.owner == owner)
		if bonus and state.hp.get(owner) is not None:
			state.hp[owner] += bonus


def run_combat_40(state, rng=None, max_rounds=40, max_reveals=3000):
	"""Full combat driver: rounds of reveal -> resolve -> bury until a side
	dies (HP-capped runs) or max_rounds is reached. Returns the state."""
	if rng is not None:
		state.rng = rng
	place_passives_40(state)
	reveals = 0
	for _ in range(max_rounds):
		shuffle_round_40(state, state.rng)
		while True:
			card = reveal_top_40(state)
			if card is None:
				break  # only the start card (+ passives) remain
			reveals += 1
			if reveals > max_reveals:
				state._trip(f'max_reveals exceeded ({max_reveals})')
				return state
			# 'revealed' handlers already ran inside reveal_top_40.
			bury_card_40(state, card)  # fires any_buried (遗言)
		a, b = state.hp['A'], state.hp['B']
		if (a is not None and a <= 0) or (b is not None and b <= 0):
			break
	return state


def selftest_combat_40():
	"""Step 5 acceptance: full combats with real Common decks; every trial
	card handled or ledgered; no divergence; invariants hold."""
	missing, extra = coverage_report_40()
	assert not missing, f'cards without handler/ledger: {missing}'
	assert not extra, f'handlers for unknown cids: {extra}'

	rng = random.Random(1055)
	deck_a = [build_card40(cid, 'A') for cid in (
		'GRAVE_FIST', 'SPIKE_SKELETON_4.0', 'WOKEN_BLADE', 'RIFT_ACOLYTE',
		'HEXER', 'SOLDIER_SKELETON_4.0', 'TWIN_STRIKER', 'AVENGER_4.0',
		'ELITE_REVIVER', 'SYSTEM_INCREASE_HP_MAX')]
	deck_b = [build_card40(cid, 'B') for cid in (
		'GRAVE_DREDGER', 'LAST_GIFT', 'BLACKSMITH_4.0', 'RIFT_SHEPHERD',
		'CURSE_REVIVER', 'SACRIFICIAL_SPIRIT', 'WAR_TRAINER',
		'BEAST_REVIVER', 'SPIRIT_CALLER', 'RIFT_INSECT_4.0')]

	state = setup_combat_40(deck_a, deck_b, hp_max=25)
	# HP_MAX static: A owns one -> 25 + 4.
	assert state.hp['A'] == 29, f'HP_MAX static not applied: {state.hp}'
	run_combat_40(state, rng, max_rounds=12)
	assert state.divergence is None, f'divergence: {state.divergence}'
	assert len(check_invariants_40(state)) == 0, check_invariants_40(state)
	assert 1 <= state.round_num <= 12, f'round count odd: {state.round_num}'
	assert state.hp['A'] <= 0 or state.hp['B'] <= 0, \
		'combat ended without a death'
	# Curse axis: HEXER (side A) must have GENERATED the single JU_ON into
	# side B's deck (no native curses; max one per deck).
	curses = [c for c in state.deck
			  if c.cid == 'JU_ON' and c.owner == 'B']
	assert len(curses) == 1, f'curse generation broken: {len(curses)}'
	# Damage happened on at least one side; curse/believer axis exercised.
	spawned_believers = sum(1 for c in state.deck if c.is_believer)
	assert spawned_believers > 0, 'no believers spawned'
	assert state.bus.fired['attack'] > 0
	assert state.bus.fired['any_buried'] > 0
	assert state.bus.fired['awaken'] > 0, 'awaken axis never exercised'

	# Deterministic seed smoke: a few full runs stay healthy.
	for seed in range(5):
		s = setup_combat_40(
			[build_card40(cid, 'A') for cid in (
				'GRAVE_FIST', 'SPIKE_SKELETON_4.0', 'HEXER',
				'WAKING_FIGHTER', 'RIFT_SHEPHERD')],
			[build_card40(cid, 'B') for cid in (
				'GRAVE_DREDGER', 'SOLDIER_SKELETON_4.0', 'TWIN_STRIKER',
				'RIFT_INSECT_4.0', 'WOKEN_BLADE', 'LAST_GIFT')],
			hp_max=25)
		run_combat_40(s, random.Random(seed), max_rounds=20)
		assert s.divergence is None, f'seed {seed}: {s.divergence}'
		assert len(check_invariants_40(s)) == 0, (seed, check_invariants_40(s))
	print('[sim5] selftest combat 40 PASS: 21 handlers + 9 ledgered, '
		  'combats healthy, coverage complete')


class Card:
	__slots__ = ('id', 'cid', 'owner', 'power', 'bury_count', 'is_start', 'exiled', 'reveal_count')
	_id_counter = 0

	def __init__(self, cid, owner, is_start=False):
		Card._id_counter += 1
		self.id = Card._id_counter
		self.cid = cid
		self.owner = owner
		self.power = 0
		self.bury_count = 0
		self.is_start = is_start
		self.exiled = False
		self.reveal_count = 0

	def __repr__(self):
		return f"Card({self.cid},{self.owner})"


def opp(owner):
	return 'B' if owner == 'A' else 'A'


class GameState:
	def __init__(self, hp_per_side=None):
		self.deck = []
		self.start_card = None
		self.reveal_card = None
		self.reveal_placed = False
		self.round_num = 0
		self.round_started = False
		self.after_shuffle_pending = False

		self.cards_revealed_this_round = 0
		self.revealed_this_round = []
		self.friendly_buried_this_round = {'A': 0, 'B': 0}
		self.enemy_buried_this_round = {'A': 0, 'B': 0}
		self.staged_this_round = {'A': 0, 'B': 0}
		self.enemy_revealed_this_round = {'A': 0, 'B': 0}
		self.graveyard_counts = {'A': 0, 'B': 0}

		self.chain_depth = 0

		self.total_damage_instance = defaultdict(float)
		self.total_damage_persistent = defaultdict(float)
		self.rounds_with_damage_instance = defaultdict(int)
		self.present_rounds = defaultdict(int)
		self.id_to_card = {}
		self.total_rounds_recorded = 0

		# HP mode support
		self.hp_per_side = hp_per_side
		if hp_per_side is not None:
			self.hp = {'A': hp_per_side, 'B': hp_per_side}
			self.game_over = False
			self.winner = None
		else:
			self.hp = None
			self.game_over = False
			self.winner = None


# ---------------------------------------------------------------------------
# Deck helpers
# ---------------------------------------------------------------------------
def iter_listeners(state):
	for c in state.deck:
		if not c.exiled:
			yield c
	if state.reveal_card and not state.reveal_card.exiled:
		yield state.reveal_card


def is_below_start(state, card):
	if state.start_card is None or state.start_card not in state.deck:
		return False
	try:
		return state.deck.index(card) < state.deck.index(state.start_card)
	except ValueError:
		return False


def cards_in_deck(state, owner=None, exclude_start=True):
	out = []
	for c in state.deck:
		if c.exiled:
			continue
		if exclude_start and c.is_start:
			continue
		if owner is not None and c.owner != owner:
			continue
		out.append(c)
	return out


def all_friendly(state, owner):
	return cards_in_deck(state, owner)


def all_enemy(state, owner):
	return cards_in_deck(state, opp(owner))


def random_friendly(state, owner, exclude_self=None):
	pool = [c for c in all_friendly(state, owner) if c is not exclude_self]
	return random.choice(pool) if pool else None


def random_enemy(state, owner):
	pool = all_enemy(state, owner)
	return random.choice(pool) if pool else None


# ---------------------------------------------------------------------------
# Core actions
# ---------------------------------------------------------------------------
def add_power_clamped(card, amount):
	if amount <= 0:
		return
	card.power = min(MAX_POWER_PER_CARD, card.power + amount)


def damage_enemy(state, card, amount):
	if amount <= 0:
		return
	state.total_damage_instance[card.id] += amount
	state.total_damage_persistent[card.id] += amount
	if state.hp is not None:
		target = opp(card.owner)
		state.hp[target] -= amount
		if state.hp[target] <= 0:
			state.game_over = True
			state.winner = card.owner
			return
	if state.chain_depth >= MAX_CHAIN_DEPTH:
		return
	state.chain_depth += 1
	event_on_their_took_dmg(state, card.owner, amount)
	state.chain_depth -= 1


def give_power(state, target, amount, source_card=None):
	if amount <= 0 or target is None:
		return
	add = min(amount, MAX_POWER_PER_CARD - target.power)
	if add <= 0:
		return
	target.power += add
	if state.chain_depth >= MAX_CHAIN_DEPTH:
		return
	state.chain_depth += 1
	event_on_friendly_got_power(state, target, amount)
	event_on_enemy_got_power(state, target, amount)
	state.chain_depth -= 1


def stage_card(state, card, by_owner):
	if card is None or card.exiled:
		return
	if card in state.deck and is_below_start(state, card):
		return
	if card in state.deck:
		state.deck.remove(card)
		state.deck.append(card)
	elif state.reveal_card is card:
		state.reveal_placed = True
		state.deck.append(card)
	else:
		return
	state.staged_this_round[by_owner] += 1
	event_on_me_staged(state, card)


def bury_card(state, card, by_owner):
	if card is None or card.exiled:
		return
	if card in state.deck and is_below_start(state, card):
		return
	if card in state.deck:
		state.deck.remove(card)
		state.deck.insert(0, card)
	elif state.reveal_card is card:
		state.reveal_placed = True
		state.deck.insert(0, card)
	else:
		return
	if by_owner == card.owner:
		state.friendly_buried_this_round[by_owner] += 1
		state.graveyard_counts[by_owner] += 1
	else:
		state.enemy_buried_this_round[by_owner] += 1
	event_on_me_buried(state, card)
	event_on_friendly_buried(state, card)


def exile_card(state, card, by_owner):
	if card is None or card.exiled:
		return
	if card in state.deck:
		state.deck.remove(card)
	card.exiled = True
	event_on_friendly_exiled(state, card)


def count_side(state, owner):
	return sum(1 for c in state.deck if not c.is_start and c.owner == owner and not c.exiled)


def add_card(state, card, position='bottom'):
	if card.is_start:
		return
	if count_side(state, card.owner) >= MAX_FRIENDLY_CARDS:
		return
	state.id_to_card[card.id] = card
	if position == 'top':
		state.deck.append(card)
	else:
		state.deck.insert(0, card)


def trim_deck(state):
	while len(state.deck) > MAX_TOTAL_DECK:
		for i, c in enumerate(state.deck):
			if not c.is_start:
				state.deck.pop(i)
				break


# ---------------------------------------------------------------------------
# Event dispatch
# ---------------------------------------------------------------------------
def event_on_me_buried(state, card):
	handler = CARD_REGISTRY.get(card.cid, {}).get('on_buried')
	if handler:
		handler(state, card)


def event_on_friendly_buried(state, buried_card):
	for c in iter_listeners(state):
		if c.owner == buried_card.owner:
			handler = CARD_REGISTRY.get(c.cid, {}).get('on_friendly_buried')
			if handler:
				handler(state, c, buried_card)


def event_on_me_staged(state, card):
	handler = CARD_REGISTRY.get(card.cid, {}).get('on_staged')
	if handler:
		handler(state, card)


def event_on_their_took_dmg(state, dealer_owner, amount):
	for c in iter_listeners(state):
		if c.owner == dealer_owner:
			handler = CARD_REGISTRY.get(c.cid, {}).get('on_their_took_dmg')
			if handler:
				handler(state, c, amount)


def event_on_friendly_exiled(state, exiled_card):
	for c in iter_listeners(state):
		if c.owner == exiled_card.owner:
			handler = CARD_REGISTRY.get(c.cid, {}).get('on_friendly_exiled')
			if handler:
				handler(state, c, exiled_card)


def event_on_friendly_got_power(state, target_card, amount):
	for c in iter_listeners(state):
		if c.owner == target_card.owner:
			handler = CARD_REGISTRY.get(c.cid, {}).get('on_friendly_got_power')
			if handler:
				handler(state, c, target_card, amount)


def event_on_enemy_got_power(state, target_card, amount):
	for c in iter_listeners(state):
		if c.owner == opp(target_card.owner):
			handler = CARD_REGISTRY.get(c.cid, {}).get('on_enemy_got_power')
			if handler:
				handler(state, c, target_card, amount)


def event_on_enemy_curse_revealed(state, curse_owner, curse_card):
	listener_owner = opp(curse_owner)
	for c in iter_listeners(state):
		if c.owner == listener_owner:
			handler = CARD_REGISTRY.get(c.cid, {}).get('on_enemy_curse_revealed')
			if handler:
				handler(state, c, curse_card)


def event_after_shuffle(state):
	for c in iter_listeners(state):
		if c.cid == 'BOOSTER':
			handler = CARD_REGISTRY.get('BOOSTER', {}).get('after_shuffle')
			if handler:
				handler(state, c)


# ---------------------------------------------------------------------------
# Curse / RIFT helpers
# ---------------------------------------------------------------------------
def find_enemy_curse(state, owner):
	for c in state.deck:
		if c.cid == 'JU_ON' and c.owner == opp(owner):
			return c
	return None


def get_or_create_enemy_curse(state, owner):
	ju = find_enemy_curse(state, owner)
	if ju is None:
		ju = Card('JU_ON', opp(owner))
		add_card(state, ju, 'bottom')
	return ju


def find_own_curse(state, owner):
	for c in state.deck:
		if c.cid == 'JU_ON' and c.owner == owner:
			return c
	return None


def get_or_create_own_curse(state, owner):
	ju = find_own_curse(state, owner)
	if ju is None:
		ju = Card('JU_ON', owner)
		add_card(state, ju, 'bottom')
	return ju


def enhance_enemy_curse(state, owner, amount):
	if amount <= 0:
		return
	ju = get_or_create_enemy_curse(state, owner)
	give_power(state, ju, amount, None)


def enhance_own_curse(state, owner, amount):
	if amount <= 0:
		return
	ju = get_or_create_own_curse(state, owner)
	give_power(state, ju, amount, None)


def reduce_enemy_curse_power(state, owner, amount):
	ju = find_enemy_curse(state, owner)
	if ju is None or ju.power < amount:
		return False
	ju.power -= amount
	return True


def total_enemy_curse_power(state, owner):
	ju = find_enemy_curse(state, owner)
	return ju.power if ju else 0


def consume_rift(state, owner, count):
	rifts = [c for c in state.deck if c.cid == 'RIFT' and c.owner == owner]
	if len(rifts) < count:
		return False
	chosen = random.sample(rifts, count)
	for c in chosen:
		exile_card(state, c, owner)
	return True


# ---------------------------------------------------------------------------
# Reveal / round flow
# ---------------------------------------------------------------------------
def record_round(state):
	state.total_rounds_recorded += 1
	for c in state.deck:
		if c.is_start or c.exiled:
			continue
		state.present_rounds[c.id] += 1
	for cid_id, dmg in state.total_damage_instance.items():
		if dmg > 0.0001:
			state.rounds_with_damage_instance[cid_id] += 1


def reset_round(state):
	state.cards_revealed_this_round = 0
	state.revealed_this_round = []
	state.friendly_buried_this_round = {'A': 0, 'B': 0}
	state.enemy_buried_this_round = {'A': 0, 'B': 0}
	state.staged_this_round = {'A': 0, 'B': 0}
	state.enemy_revealed_this_round = {'A': 0, 'B': 0}
	state.graveyard_counts = {'A': 0, 'B': 0}
	state.total_damage_instance = defaultdict(float)
	trim_deck(state)


def shuffle_deck(state):
	start = state.start_card
	if start in state.deck:
		state.deck.remove(start)
	others = state.deck[:]
	random.shuffle(others)
	total = len(others) + 1
	mean = (total - 1) / 2.0
	std = max(1.0, total * START_STD_FACTOR)
	idx = int(round(random.gauss(mean, std)))
	idx = max(0, min(total - 2, idx))
	others.insert(idx, start)
	state.deck = others
	state.after_shuffle_pending = True


def on_start_card_revealed(state, card):
	if state.round_started:
		record_round(state)
		reset_round(state)
	state.round_num += 1
	state.round_started = True
	state.start_card = card
	shuffle_deck(state)


def reveal_card(state, card):
	state.cards_revealed_this_round += 1
	state.revealed_this_round.append(card)
	state.reveal_card = card
	state.reveal_placed = False

	if card in state.deck:
		state.deck.remove(card)

	if state.after_shuffle_pending:
		state.after_shuffle_pending = False
		event_after_shuffle(state)

	# Track enemy reveals for Quick Response Protocol
	for owner in ('A', 'B'):
		if card.owner != owner:
			state.enemy_revealed_this_round[owner] += 1
			if state.enemy_revealed_this_round[owner] % 3 == 0:
				for c in iter_listeners(state):
					if c.owner == owner and c.cid == 'QUICK_RESPONSE_PROTOCOL' and meets_linger_cost(state, c):
						target = random_friendly(state, owner, exclude_self=c)
						if target:
							stage_card(state, target, owner)

	if card.cid == 'JU_ON':
		event_on_enemy_curse_revealed(state, card.owner, card)

	handler = CARD_REGISTRY.get(card.cid, {}).get('on_reveal')
	if handler:
		handler(state, card)

	if not card.exiled and not state.reveal_placed:
		state.deck.insert(0, card)

	state.reveal_card = None
	state.reveal_placed = False



# ---------------------------------------------------------------------------
# Card effect registry
# ---------------------------------------------------------------------------
CARD_REGISTRY = {}


def register(cid, **kwargs):
	CARD_REGISTRY[cid] = kwargs


# ---------------- Bury and buried ----------------
def reveal_grave_punch(state, card):
	target = random_friendly(state, card.owner, exclude_self=card)
	if target:
		bury_card(state, target, card.owner)
	damage_enemy(state, card, (3 + card.power) * 2)


register('GRAVE_PUNCH', on_reveal=reveal_grave_punch)


def reveal_grave_together(state, card):
	ft = random_friendly(state, card.owner, exclude_self=card)
	if ft:
		bury_card(state, ft, card.owner)
	es = all_enemy(state, card.owner)
	chosen = random.sample(es, min(2, len(es))) if len(es) >= 2 else es
	for t in chosen:
		bury_card(state, t, card.owner)


register('GRAVE_TOGETHER', on_reveal=reveal_grave_together)


def reveal_corpse_canon(state, card):
	target = random_friendly(state, card.owner, exclude_self=card)
	if target:
		bury_card(state, target, card.owner)


def on_friendly_buried_corpse_canon(state, card, buried_card):
	damage_enemy(state, card, 2 + card.power)


register('CORPSE_CANON', on_reveal=reveal_corpse_canon,
		 on_friendly_buried=on_friendly_buried_corpse_canon)


def reveal_grave_invitation(state, card):
	damage_enemy(state, card, 4 + card.power)
	count = state.graveyard_counts[card.owner]
	es = all_enemy(state, card.owner)
	chosen = random.sample(es, min(count, len(es))) if count > 0 and es else []
	for t in chosen:
		bury_card(state, t, card.owner)


register('GRAVE_INVITATION', on_reveal=reveal_grave_invitation)


def reveal_grave_keeper(state, card):
	damage_enemy(state, card, 6 + card.power)


def on_friendly_buried_grave_keeper(state, card, buried_card):
	stage_card(state, card, card.owner)


register('GRAVE_KEEPER', on_reveal=reveal_grave_keeper,
		 on_friendly_buried=on_friendly_buried_grave_keeper)


def reveal_body_canon(state, card):
	count = max(0, len(all_friendly(state, card.owner)) - 1)
	damage_enemy(state, card, 3 * count + card.power)
	for c in all_friendly(state, card.owner):
		if c is card:
			continue
		bury_card(state, c, card.owner)


register('BODY_CANON', on_reveal=reveal_body_canon)


def reveal_large_scale_death(state, card):
	top = [c for c in reversed(state.deck) if not c.is_start][:4]
	for t in top:
		bury_card(state, t, card.owner)


register('LARGE_SCALE_DEATH', on_reveal=reveal_large_scale_death)


def reveal_small_scale_death(state, card):
	top = [c for c in reversed(state.deck) if not c.is_start][:2]
	for t in top:
		bury_card(state, t, card.owner)
	enhance_enemy_curse(state, card.owner, 1)


register('SMALL_SCALE_DEATH', on_reveal=reveal_small_scale_death)


def reveal_unstable_portal(state, card):
	tf = random_friendly(state, card.owner, exclude_self=card)
	if tf:
		stage_card(state, tf, card.owner)
	bf = random_friendly(state, card.owner, exclude_self=card)
	if bf:
		bury_card(state, bf, card.owner)


register('UNSTABLE_PORTAL', on_reveal=reveal_unstable_portal)


def on_staged_wise_burial(state, card):
	pool = [c for c in all_friendly(state, card.owner) if c.cid in DEATHRATTLE_CIDS]
	if pool:
		target = random.choice(pool)
		bury_card(state, target, card.owner)


register('WISE_BURIAL', on_staged=on_staged_wise_burial)


def reveal_grave_portal(state, card):
	target = random_enemy(state, card.owner)
	if target:
		bury_card(state, target, card.owner)


def on_buried_grave_portal(state, card):
	target = random_friendly(state, card.owner, exclude_self=card)
	if target:
		stage_card(state, target, card.owner)


register('GRAVE_PORTAL', on_reveal=reveal_grave_portal,
		 on_buried=on_buried_grave_portal)


def reveal_soldier_skeleton(state, card):
	damage_enemy(state, card, 3 + card.power)


def on_buried_soldier_skeleton(state, card):
	stage_card(state, card, card.owner)


register('SOLDIER_SKELETON', on_reveal=reveal_soldier_skeleton,
		 on_buried=on_buried_soldier_skeleton)


register('UNDEAD_CURSER')  # no clear damage effect in descriptions


def reveal_avenger(state, card):
	damage_enemy(state, card, 3 + card.power)


def on_buried_avenger(state, card):
	give_power(state, card, 2, card)


register('AVENGER', on_reveal=reveal_avenger, on_buried=on_buried_avenger)


def reveal_confused_portalmancer(state, card):
	target = random_friendly(state, card.owner, exclude_self=card)
	if target:
		bury_card(state, target, card.owner)


def on_buried_confused_portalmancer(state, card):
	for _ in range(3):
		add_card(state, Card('RIFT', card.owner), 'bottom')


register('CONFUSED_PORTALMANCER', on_reveal=reveal_confused_portalmancer,
		 on_buried=on_buried_confused_portalmancer)


def reveal_cursed_corpse(state, card):
	enhance_enemy_curse(state, card.owner, 1)


def on_buried_cursed_corpse(state, card):
	damage_enemy(state, card, (1 + card.power) * 3)


register('CURSED_CORPSE', on_reveal=reveal_cursed_corpse,
		 on_buried=on_buried_cursed_corpse)


def on_buried_scapegoat(state, card):
	damage_enemy(state, card, 3 + card.power)
	target = random_friendly(state, card.owner, exclude_self=card)
	if target:
		stage_card(state, target, card.owner)


register('SCAPEGOAT', on_buried=on_buried_scapegoat)


def reveal_spike_skeleton(state, card):
	damage_enemy(state, card, 3 + card.power)


def on_buried_spike_skeleton(state, card):
	damage_enemy(state, card, (2 + card.power) * 2)


register('SPIKE_SKELETON', on_reveal=reveal_spike_skeleton,
		 on_buried=on_buried_spike_skeleton)


def on_buried_martyr(state, card):
	for c in all_friendly(state, card.owner):
		give_power(state, c, 1, card)


register('MARTYR', on_buried=on_buried_martyr)


def reveal_slime(state, card):
	damage_enemy(state, card, 4 + card.power)


def on_buried_slime(state, card):
	card.bury_count += 1
	if card.bury_count >= 2:
		card.bury_count = 0
		add_card(state, Card('SLIME', card.owner), 'bottom')


register('SLIME', on_reveal=reveal_slime, on_buried=on_buried_slime)


# ---------------- Conjure ----------------
def reveal_fall_into_rift(state, card):
	if consume_rift(state, card.owner, 1):
		target = random_enemy(state, card.owner)
		if target:
			bury_card(state, target, card.owner)


register('FALL_INTO_RIFT', on_reveal=reveal_fall_into_rift)


def reveal_rift_insect(state, card):
	add_card(state, Card('RIFT', card.owner), 'bottom')


register('RIFT_INSECT', on_reveal=reveal_rift_insect)


def reveal_sacrifice_ritual(state, card):
	fs = [c for c in all_friendly(state, card.owner) if c is not card]
	chosen = random.sample(fs, min(2, len(fs))) if len(fs) >= 2 else fs
	for t in chosen:
		bury_card(state, t, card.owner)
	for _ in range(2):
		add_card(state, Card('RIFT', card.owner), 'bottom')


register('SACRIFICE_RITUAL', on_reveal=reveal_sacrifice_ritual)


def on_friendly_exiled_rift_coffin(state, card, exiled_card):
	if not meets_linger_cost(state, card):
		return
	target = random_enemy(state, card.owner)
	if target:
		bury_card(state, target, card.owner)


register('RIFT_COFFIN', on_friendly_exiled=on_friendly_exiled_rift_coffin)


def reveal_rift_dragon(state, card):
	if consume_rift(state, card.owner, 2):
		damage_enemy(state, card, 6 + card.power)


register('RIFT_DRAGON', on_reveal=reveal_rift_dragon)


def reveal_rift_guide(state, card):
	if consume_rift(state, card.owner, 2):
		es = all_enemy(state, card.owner)
		chosen = random.sample(es, min(2, len(es))) if len(es) >= 2 else es
		for t in chosen:
			bury_card(state, t, card.owner)


register('RIFT_GUIDE', on_reveal=reveal_rift_guide)


def reveal_rift_monster(state, card):
	if consume_rift(state, card.owner, 1):
		damage_enemy(state, card, 4 + card.power)


register('RIFT_MONSTER', on_reveal=reveal_rift_monster)


def reveal_rift_summoner(state, card):
	if consume_rift(state, card.owner, 1):
		target = random_friendly(state, card.owner, exclude_self=card)
		if target:
			stage_card(state, target, card.owner)


register('RIFT_SUMMONER', on_reveal=reveal_rift_summoner)


def on_friendly_exiled_deathbed_curse(state, card, exiled_card):
	if not meets_linger_cost(state, card):
		return
	enhance_enemy_curse(state, card.owner, 1)


register('DEATHBED_CURSE', on_friendly_exiled=on_friendly_exiled_deathbed_curse)


def reveal_rift_devourer(state, card):
	damage_enemy(state, card, 2 + card.power)


def on_friendly_exiled_rift_devourer(state, card, exiled_card):
	give_power(state, card, 1, card)


register('RIFT_DEVOURER', on_reveal=reveal_rift_devourer,
		 on_friendly_exiled=on_friendly_exiled_rift_devourer)


# Tokens
def reveal_rift(state, card):
	target = random_friendly(state, card.owner, exclude_self=card)
	if target:
		stage_card(state, target, card.owner)
	exile_card(state, card, card.owner)


register('RIFT', on_reveal=reveal_rift)


def reveal_ju_on(state, card):
	if card.power > 0:
		damage_enemy(state, card, card.power)


register('JU_ON', on_reveal=reveal_ju_on)


# ---------------- Curse ----------------
def reveal_curse_summoner(state, card):
	if reduce_enemy_curse_power(state, card.owner, 1):
		target = random_friendly(state, card.owner, exclude_self=card)
		if target:
			stage_card(state, target, card.owner)


register('CURSE_SUMMONER', on_reveal=reveal_curse_summoner)


def reveal_poisoner(state, card):
	enhance_enemy_curse(state, card.owner, 1)
	damage_enemy(state, card, 3 + card.power)


register('POISONER', on_reveal=reveal_poisoner)


def reveal_rift_curse(state, card):
	enhance_enemy_curse(state, card.owner, 1)
	add_card(state, Card('RIFT', card.owner), 'bottom')


register('RIFT_CURSE', on_reveal=reveal_rift_curse)


def reveal_cursed_skeleton(state, card):
	enhance_enemy_curse(state, card.owner, state.graveyard_counts[card.owner])


register('CURSED_SKELETON', on_reveal=reveal_cursed_skeleton)


def reveal_curse_thirst_beast(state, card):
	damage_enemy(state, card, 4 + card.power)


def on_enemy_curse_revealed_ctb(state, card, curse_card):
	stage_card(state, card, card.owner)


register('CURSE_THIRST_BEAST', on_reveal=reveal_curse_thirst_beast,
		 on_enemy_curse_revealed=on_enemy_curse_revealed_ctb)


def reveal_curse_thirst_shaman(state, card):
	power = total_enemy_curse_power(state, card.owner)
	fs = all_friendly(state, card.owner)
	for _ in range(power):
		if fs:
			give_power(state, random.choice(fs), 1, card)


register('CURSE_THIRST_SHAMAN', on_reveal=reveal_curse_thirst_shaman)


def on_enemy_got_power_moth_man(state, card, target_card, amount):
	if target_card.cid == 'JU_ON':
		target = random_friendly(state, card.owner, exclude_self=card)
		if target:
			stage_card(state, target, card.owner)


register('MOTH_MAN', on_enemy_got_power=on_enemy_got_power_moth_man)


def reveal_premature(state, card):
	if reduce_enemy_curse_power(state, card.owner, 1):
		ju = find_enemy_curse(state, card.owner)
		if ju:
			stage_card(state, ju, card.owner)


register('PREMATURE', on_reveal=reveal_premature)


def reveal_sacrificial_curse(state, card):
	target = random_friendly(state, card.owner, exclude_self=card)
	if target:
		bury_card(state, target, card.owner)
	enhance_enemy_curse(state, card.owner, 2)


register('SACRIFICIAL_CURSE', on_reveal=reveal_sacrificial_curse)


def reveal_crow_crowd(state, card):
	ju = get_or_create_enemy_curse(state, card.owner)
	for c in all_friendly(state, card.owner):
		if c.power > 0:
			add_power_clamped(ju, c.power)
			c.power = 0


register('CROW_CROWD', on_reveal=reveal_crow_crowd)


def on_their_took_dmg_curse_enchantment(state, card, amount):
	if not meets_linger_cost(state, card):
		return
	enhance_enemy_curse(state, card.owner, 1)


register('CURSE_ENCHANTMENT', on_their_took_dmg=on_their_took_dmg_curse_enchantment)


def reveal_deterioration(state, card):
	power = total_enemy_curse_power(state, card.owner)
	enhance_enemy_curse(state, card.owner, power // 2)


register('DETERIORATION', on_reveal=reveal_deterioration)


def reveal_proliferating_curse(state, card):
	ju = find_enemy_curse(state, card.owner)
	if ju:
		add_card(state, Card('JU_ON', opp(card.owner)), 'bottom')


register('PROLIFERATING_CURSE', on_reveal=reveal_proliferating_curse)


# ---------------- General ----------------
def reveal_blacksmith(state, card):
	target = random_friendly(state, card.owner, exclude_self=card)
	if target:
		give_power(state, target, 1, card)
	damage_enemy(state, card, 3 + card.power)


register('BLACKSMITH', on_reveal=reveal_blacksmith)


def reveal_blind_combat_priest(state, card):
	targets = [c for c in reversed(state.deck) if not c.is_start][:1]
	for t in targets:
		give_power(state, t, 3, card)


register('BLIND_COMBAT_PRIEST', on_reveal=reveal_blind_combat_priest)


def reveal_coffin_maker(state, card):
	target = random_enemy(state, card.owner)
	if target:
		bury_card(state, target, card.owner)
	damage_enemy(state, card, 3 + card.power)


register('COFFIN_MAKER', on_reveal=reveal_coffin_maker)


def reveal_curse_thirst_summoner(state, card):
	if reduce_enemy_curse_power(state, card.owner, 1):
		target = random_friendly(state, card.owner, exclude_self=card)
		if target:
			stage_card(state, target, card.owner)


register('CURSE_THIRST_SUMMONER', on_reveal=reveal_curse_thirst_summoner)


def reveal_mad_scientist(state, card):
	targets = [c for c in reversed(state.deck) if not c.is_start][:3]
	for t in targets:
		give_power(state, t, 2, card)


register('MAD_SCIENTIST', on_reveal=reveal_mad_scientist)


def reveal_sacrificial_sword(state, card):
	target = random_friendly(state, card.owner, exclude_self=card)
	if target:
		bury_card(state, target, card.owner)
	fs = [c for c in all_friendly(state, card.owner) if c is not card]
	chosen = random.sample(fs, min(2, len(fs))) if len(fs) >= 2 else fs
	for t in chosen:
		give_power(state, t, 1, card)


register('SACRIFICIAL_SWORD', on_reveal=reveal_sacrificial_sword)


def reveal_side_effect_portal(state, card):
	enhance_own_curse(state, card.owner, 2)
	target = random_friendly(state, card.owner, exclude_self=card)
	if target:
		stage_card(state, target, card.owner)


register('SIDE_EFFECT_PORTAL', on_reveal=reveal_side_effect_portal)


def reveal_the_fool(state, card):
	enemies = all_enemy(state, card.owner)
	if enemies:
		max_power = max(c.power for c in enemies)
		candidates = [c for c in enemies if c.power == max_power]
		target = random.choice(candidates)
		stage_card(state, target, card.owner)
	damage_enemy(state, card, 4 + card.power)


register('THE_FOOL', on_reveal=reveal_the_fool)


def reveal_advance_portal(state, card):
	card.reveal_count = getattr(card, 'reveal_count', 0) + 1
	if card.reveal_count % 2 == 0:
		for _ in range(2):
			target = random_friendly(state, card.owner, exclude_self=card)
			if target:
				stage_card(state, target, card.owner)


register('ADVANCE_PORTAL', on_reveal=reveal_advance_portal)


def reveal_all_for_one(state, card):
	total = sum(c.power for c in state.deck if not c.is_start)
	if state.reveal_card and not state.reveal_card.is_start:
		total += state.reveal_card.power
	damage_enemy(state, card, total + card.power)


register('ALL_FOR_ONE', on_reveal=reveal_all_for_one)


def reveal_almighty(state, card):
	card.reveal_count = getattr(card, 'reveal_count', 0) + 1
	if card.reveal_count % 2 == 0:
		damage_enemy(state, card, 1 + card.power)
		target = random_friendly(state, card.owner, exclude_self=card)
		if target:
			stage_card(state, target, card.owner)
		te = random_enemy(state, card.owner)
		if te:
			bury_card(state, te, card.owner)
		tfp = random_friendly(state, card.owner, exclude_self=card)
		if tfp:
			give_power(state, tfp, 1, card)
		add_card(state, Card('RIFT', card.owner), 'bottom')
		enhance_enemy_curse(state, card.owner, 1)


register('ALMIGHTY', on_reveal=reveal_almighty)


def reveal_anti_creature_weapon(state, card):
	es = all_enemy(state, card.owner)
	chosen = random.sample(es, min(2, len(es))) if len(es) >= 2 else es
	for t in chosen:
		bury_card(state, t, card.owner)


register('ANTI_CREATURE_WEAPON', on_reveal=reveal_anti_creature_weapon)


def reveal_bone_combination(state, card):
	damage_enemy(state, card, state.enemy_buried_this_round[card.owner] + card.power)


register('BONE_COMBINATION', on_reveal=reveal_bone_combination)


def reveal_dr_manhattan(state, card):
	if card.power >= 4:
		card.power -= 4
		for _ in range(2):
			target = random_friendly(state, card.owner, exclude_self=card)
			if target:
				stage_card(state, target, card.owner)
		es = all_enemy(state, card.owner)
		chosen = random.sample(es, min(2, len(es))) if len(es) >= 2 else es
		for t in chosen:
			bury_card(state, t, card.owner)


register('DR_MANHATTAN', on_reveal=reveal_dr_manhattan)


def reveal_flesh_combination(state, card):
	damage_enemy(state, card, len(all_friendly(state, card.owner)) + card.power)


register('FLESH_COMBINATION', on_reveal=reveal_flesh_combination)


def reveal_goblin_assassin_team(state, card):
	damage_enemy(state, card, 4 + card.power)


def on_staged_goblin_assassin_team(state, card):
	target = random_enemy(state, card.owner)
	if target:
		bury_card(state, target, card.owner)


register('GOBLIN_ASSASSIN_TEAM', on_reveal=reveal_goblin_assassin_team,
		 on_staged=on_staged_goblin_assassin_team)


def reveal_goblin_charge_team(state, card):
	damage_enemy(state, card, 2 + card.power)


def on_staged_goblin_charge_team(state, card):
	damage_enemy(state, card, 4 + card.power)


register('GOBLIN_CHARGE_TEAM', on_reveal=reveal_goblin_charge_team,
		 on_staged=on_staged_goblin_charge_team)


def reveal_power_craver(state, card):
	damage_enemy(state, card, 3 + card.power)
	give_power(state, card, 3, card)


register('POWER_CRAVER', on_reveal=reveal_power_craver)


def reveal_power_surge(state, card):
	damage_enemy(state, card, 3 + card.power)


def on_staged_power_surge(state, card):
	fs = [c for c in all_friendly(state, card.owner) if c is not card]
	chosen = random.sample(fs, min(2, len(fs))) if len(fs) >= 2 else fs
	for t in chosen:
		give_power(state, t, 1, card)


register('POWER_SURGE', on_reveal=reveal_power_surge,
		 on_staged=on_staged_power_surge)


register('QUICK_RESPONSE_PROTOCOL')  # handled inline in reveal_card


def reveal_snatcher(state, card):
	damage_enemy(state, card, 3 + card.power)


def on_staged_snatcher(state, card):
	target = random_friendly(state, card.owner, exclude_self=card)
	if target:
		stage_card(state, target, card.owner)


def on_buried_snatcher(state, card):
	target = random_enemy(state, card.owner)
	if target:
		bury_card(state, target, card.owner)


register('SNATCHER', on_reveal=reveal_snatcher, on_staged=on_staged_snatcher,
		 on_buried=on_buried_snatcher)


def reveal_tactical_breacher(state, card):
	damage_enemy(state, card, 4 + card.power)


def on_staged_tactical_breacher(state, card):
	give_power(state, card, 1, card)


register('TACTICAL_BREACHER', on_reveal=reveal_tactical_breacher,
		 on_staged=on_staged_tactical_breacher)


def on_friendly_got_power_weapon_spirit(state, card, target_card, amount):
	if not meets_linger_cost(state, card):
		return
	# Direct increment to avoid recursive Power explosion.
	if target_card is not card:
		add_power_clamped(target_card, 1)


register('WEAPON_SPIRIT', on_friendly_got_power=on_friendly_got_power_weapon_spirit)


def after_shuffle_booster(state, card):
	for _ in range(2):
		target = random_friendly(state, card.owner, exclude_self=card)
		if target:
			stage_card(state, target, card.owner)
	target = random_friendly(state, card.owner, exclude_self=card)
	if target:
		bury_card(state, target, card.owner)


register('BOOSTER', after_shuffle=after_shuffle_booster)


def reveal_elder_sorcerer(state, card):
	for _ in range(state.staged_this_round[card.owner]):
		fs = all_friendly(state, card.owner)
		if fs:
			give_power(state, random.choice(fs), 1, card)


register('ELDER_SORCERER', on_reveal=reveal_elder_sorcerer)


def on_their_took_dmg_eternal_ghost(state, card, amount):
	if not meets_linger_cost(state, card):
		return
	damage_enemy(state, card, 1 + card.power)


register('ETERNAL_GHOST', on_their_took_dmg=on_their_took_dmg_eternal_ghost)


def reveal_power_siphoner(state, card):
	total = 0
	for c in all_friendly(state, card.owner):
		if c is card:
			continue
		total += c.power
		c.power = 0
	add_power_clamped(card, total)
	damage_enemy(state, card, (2 + card.power) * 2)


register('POWER_SIPHONER', on_reveal=reveal_power_siphoner)


def reveal_power_transfer(state, card):
	es = all_enemy(state, card.owner)
	for _ in range(2):
		if es:
			target = random.choice(es)
			if target.power > 0:
				target.power -= 1
	fs = all_friendly(state, card.owner)
	chosen = random.sample(fs, min(2, len(fs))) if len(fs) >= 2 else fs
	for t in chosen:
		give_power(state, t, 1, card)


register('POWER_TRANSFER', on_reveal=reveal_power_transfer)


def reveal_unfinished_robot(state, card):
	card.power = min(MAX_POWER_PER_CARD, card.power * 2)
	damage_enemy(state, card, 0 + card.power)


register('UNFINISHED_ROBOT', on_reveal=reveal_unfinished_robot)


# ---------------------------------------------------------------------------
# Card pool
# ---------------------------------------------------------------------------
POOL = list(CARD_REGISTRY.keys())
# Do not include tokens in the initial random pool.
POOL = [c for c in POOL if c not in ('RIFT', 'JU_ON', 'START')]


# ---------------------------------------------------------------------------
# Simulation driver
# ---------------------------------------------------------------------------
def init_game(deck_size_each=6, hp_per_side=None):
	state = GameState(hp_per_side=hp_per_side)
	deck_cards = []
	for _ in range(deck_size_each):
		deck_cards.append(Card(random.choice(POOL), 'A'))
	for _ in range(deck_size_each):
		deck_cards.append(Card(random.choice(POOL), 'B'))
	state.deck = deck_cards
	start = Card('START', 'A', is_start=True)
	state.start_card = start
	state.deck.insert(0, start)
	for c in deck_cards:
		state.id_to_card[c.id] = c
	state.id_to_card[start.id] = start
	return state


def run_sim(deck_size_each=6, sessions=20, rounds_per_session=500,
			warmup_per_session=200, output_path=None, hp_per_side=None):
	global_present = defaultdict(int)
	global_damage = defaultdict(float)
	global_rounds_dmg = defaultdict(int)
	id_to_cid = {}
	total_rounds = 0
	total_steps_all = 0

	# HP mode session-level stats
	hp_mode = hp_per_side is not None
	win_counts = {'A': 0, 'B': 0, 'Draw': 0}
	total_game_rounds = 0
	total_game_damage = 0.0

	for sess in range(sessions):
		state = init_game(deck_size_each=deck_size_each, hp_per_side=hp_per_side)
		target = rounds_per_session + warmup_per_session
		total_steps = 0
		while state.round_num < target:
			if not state.deck or state.game_over:
				break
			card = state.deck.pop()
			if card.is_start:
				on_start_card_revealed(state, card)
			else:
				reveal_card(state, card)
			total_steps += 1
			total_steps_all += 1
			if total_steps_all % 100000 == 0:
				print(f"  ... session {sess + 1}/{sessions}, rounds recorded {total_rounds}, steps={total_steps_all}")

		for cid_id, present in state.present_rounds.items():
			global_present[cid_id] += present
		for cid_id, dmg in state.total_damage_persistent.items():
			global_damage[cid_id] += dmg
		for cid_id, r in state.rounds_with_damage_instance.items():
			global_rounds_dmg[cid_id] += r
		for cid_id, card in state.id_to_card.items():
			id_to_cid[cid_id] = card.cid
		total_rounds += state.total_rounds_recorded

		if hp_mode:
			if state.winner is not None:
				win_counts[state.winner] += 1
			else:
				win_counts['Draw'] += 1
			total_game_rounds += state.total_rounds_recorded
			total_game_damage += sum(state.total_damage_persistent.values())

	# Aggregate results
	totals = defaultdict(lambda: {'damage': 0.0, 'present': 0, 'rounds_dmg': 0})
	for cid_id, present in global_present.items():
		cid = id_to_cid.get(cid_id)
		if cid is None:
			continue
		totals[cid]['damage'] += global_damage.get(cid_id, 0.0)
		totals[cid]['present'] += present
		totals[cid]['rounds_dmg'] += global_rounds_dmg.get(cid_id, 0)

	rows = []
	total_damage = 0.0
	for cid, d in totals.items():
		avg = d['damage'] / d['present'] if d['present'] else 0.0
		prob = d['rounds_dmg'] / d['present'] if d['present'] else 0.0
		rows.append((cid, avg, prob, d['damage'], d['present']))
		total_damage += d['damage']

	rows.sort(key=lambda x: x[1], reverse=True)

	lines = []
	lines.append("")
	lines.append(f"# OneDeck Damage Per Round (steady state, random {deck_size_each}v{deck_size_each}, with replacement)")
	lines.append(f"Sessions: {sessions}, rounds per session: {rounds_per_session}, warmup per session: {warmup_per_session}")
	if hp_mode:
		lines.append(f"HP per side: {hp_per_side}")
		lines.append(f"A wins: {win_counts['A']} ({win_counts['A']/sessions*100:.1f}%) | B wins: {win_counts['B']} ({win_counts['B']/sessions*100:.1f}%) | Draws: {win_counts['Draw']} ({win_counts['Draw']/sessions*100:.1f}%)")
		lines.append(f"Avg rounds per game: {total_game_rounds/sessions:.2f} | Avg total damage per game: {total_game_damage/sessions:.1f}")
	lines.append(f"Total rounds recorded: {total_rounds:,}")
	lines.append(f"Unique card instances tracked: {len(global_present)}")
	lines.append(f"Unique CIDs in totals: {len(totals)}")
	if total_rounds > 0:
		lines.append(f"Total damage to enemy per round (both sides): {total_damage / total_rounds:.3f}")
	lines.append("")
	lines.append("| Card | Display Name | Rarity | Avg Dmg/Round (when present) | Prob Dmg/Round | Total Dmg | Present Rounds |")
	lines.append("|---|---|---|---|---|---|---|")
	for cid, avg, prob, dmg, present in rows:
		lines.append(f"| {cid} | {card_display(cid)} | {card_rarity(cid)} | {avg:.3f} | {prob:.3f} | {dmg:.1f} | {present:,} |")

	output = '\n'.join(lines)
	print(output)
	if output_path:
		with open(output_path, 'w', encoding='utf-8') as f:
			f.write(output)
		print(f"\n[Saved results to {output_path}]")


def write_report(deck_size_each, raw_path, report_path, hp_per_side=None):
	with open(raw_path, 'r', encoding='utf-8') as f:
		raw = f.read()
	hp_line = ""
	if hp_per_side is not None:
		hp_line = f"- 血量限制：敌我双方各 {hp_per_side} HP，任意一方 HP 降至 0 时该 session 结束。\n"
	report = (
		"# OneDeck 每回合伤害分析（蒙特卡洛模拟）\n\n"
		"## 模拟参数\n"
		f"- 卡组：真实 3.0 no cost 卡池，敌我双方各随机 {deck_size_each} 张，可重复。\n"
		"- 回合：只看稳态，两次 Start Card 触发之间为一回合。\n"
		"- Start Card 位置：按 Unity 代码中的高斯分布（mean=中间，std=deckSize×0.15， clamp 不到顶）。\n"
		"- 计入 Power/Counter 效果（Counter/Rest 跳过未建模）。\n"
		"- [Linger] 卡牌已按 Unity 的 `CheckCost_IndexBeforeStartCard` 条件判定："
		"效果只在卡牌位于 Start Card 之前（index 更小、更靠近牌底）时触发。\n"
		+ hp_line +
		"- 统计视角：对敌方玩家造成的伤害，包括敌方 JU_ON 等诅咒卡对自己的伤害。\n"
		"- 模拟量：**100 个独立 session × 每 session 500 个记录回合**。\n\n"
		"## 关键近似与限制\n"
		"1. 为了控制运行时间和防止状态爆炸，对每方卡牌总数做了软上限（约 12~28 张），"
		"RIFT/JU_ON/SLIME 复制等 token 超过上限后会被丢弃。\n"
		"2. Power 单层上限设为 20，防止 WEAPON_SPIRIT、CURSE_ENCHANTMENT 等互动出现指数爆炸。\n"
		"3. Graveyard 按“每回合内友方被埋葬数量”近似（回合结束重置）。\n"
		"4. 未实现 Counter、Rest 跳过、Shield、敌方 AI 差异等细节。\n"
		"5. 一些高成本卡（RIFT_DRAGON、DR_MANHATTAN 等）因 token/Power 资源不足，"
		"模拟中很少触发，结果可能偏低。\n\n"
		"## 结果说明\n"
		"- **Card / Display Name / Rarity**：卡牌 ID、显示名称（从 prefab 解析）和稀有度（Common=普通，Uncommon=稀有，Rare=罕见）。\n"
		"- **Avg Dmg/Round (when present)**：该卡在场时，平均每回合对敌方造成的伤害。\n"
		"- **Prob Dmg/Round (when present)**：该卡在场时，每回合至少造成一次伤害的概率。\n"
		"- **Present Rounds**：该卡（包括复制/token）在所有 session 中累计在场的回合数。\n\n"
		+ raw
	)
	with open(report_path, 'w', encoding='utf-8') as f:
		f.write(report)
	print(f"[Saved report to {report_path}]")


if __name__ == '__main__':
	base_dir = os.path.dirname(os.path.abspath(__file__))
	parser = argparse.ArgumentParser(description='OneDeck damage-per-round Monte Carlo simulator')
	parser.add_argument('--deck-size-each', type=int, default=None,
						help='Number of cards per side (default: ask / run all presets if omitted)')
	parser.add_argument('--hp-per-side', type=int, default=None,
						help='HP cap per side; omit for no HP limit')
	parser.add_argument('--sessions', type=int, default=100)
	parser.add_argument('--rounds-per-session', type=int, default=500)
	parser.add_argument('--warmup-per-session', type=int, default=200)
	parser.add_argument('--output', type=str, default=None,
						help='Raw result markdown path (auto-named if omitted)')
	parser.add_argument('--report', type=str, default=None,
						help='Formatted report path (auto-named if omitted)')
	parser.add_argument('--preset', choices=['all', 'none'], default='none',
						help='Run all preset configs (6/10 with and without 25 HP)')
	parser.add_argument('--dump-pool', choices=['common40'], default=None,
						help='Write the 4.0 trial card table to JSON and exit '
							 '(no simulation)')
	parser.add_argument('--selftest-40', action='store_true',
						help='Run the 4.0 engine self-test (zones, invariants, '
							 'attack entry) and exit (no simulation)')
	args = parser.parse_args()

	if args.selftest_40:
		selftest_40()
		selftest_verbs_40()
		selftest_triggers_40()
		selftest_combat_40()
		sys.exit(0)
	if args.dump_pool:
		out_path = os.path.join(base_dir, '..', 'outputs', 'sim4',
								'prefab_card_table_common.json')
		dump_card_pool_40(out_path)
		sys.exit(0)

	if args.preset == 'all':
		# Run all preset configs
		for size in (6, 10):
			raw_path = os.path.join(base_dir, f'sim_results_{size}v{size}.md')
			rpt_path = os.path.join(base_dir, f'damage_analysis_report_{size}v{size}.md')
			run_sim(deck_size_each=size, sessions=args.sessions,
					rounds_per_session=args.rounds_per_session,
					warmup_per_session=args.warmup_per_session,
					output_path=raw_path)
			write_report(size, raw_path, rpt_path)
			print()
		for size in (6, 10):
			raw_path = os.path.join(base_dir, f'sim_results_{size}v{size}_hp25.md')
			rpt_path = os.path.join(base_dir, f'damage_analysis_report_{size}v{size}_hp25.md')
			run_sim(deck_size_each=size, sessions=args.sessions,
					rounds_per_session=args.rounds_per_session,
					warmup_per_session=args.warmup_per_session,
					output_path=raw_path, hp_per_side=25)
			write_report(size, raw_path, rpt_path, hp_per_side=25)
			print()
	elif args.deck_size_each is None:
		parser.error('must specify --deck-size-each or use --preset all')
	else:
		hp_tag = f"_hp{args.hp_per_side}" if args.hp_per_side is not None else ""
		raw_path = args.output or os.path.join(base_dir,
			f'sim_results_{args.deck_size_each}v{args.deck_size_each}{hp_tag}.md')
		rpt_path = args.report or os.path.join(base_dir,
			f'damage_analysis_report_{args.deck_size_each}v{args.deck_size_each}{hp_tag}.md')
		run_sim(deck_size_each=args.deck_size_each, sessions=args.sessions,
				rounds_per_session=args.rounds_per_session,
				warmup_per_session=args.warmup_per_session,
				output_path=raw_path, hp_per_side=args.hp_per_side)
		write_report(args.deck_size_each, raw_path, rpt_path,
					 hp_per_side=args.hp_per_side)
