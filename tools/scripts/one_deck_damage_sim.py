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
	args = parser.parse_args()

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
