#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Parse CardDesign_GenerationLog.txt and generate 3.0_no_cost_CardDesign.md."""

import re
from datetime import datetime
from pathlib import Path

LOG_PATH = Path(__file__).parent / "CardDesign_GenerationLog.txt"
OUT_PATH = Path(__file__).parent / "3.0_no_cost_CardDesign.md"

CATEGORY_ORDER = ["Bury and buried", "Conjure", "Curse", "General"]


def strip_assembly(name):
	return name.replace(", Assembly-CSharp", "")


def parse_line(line):
	if not line.startswith("CARD|"):
		return None
	# Split into exactly 4 parts: CARD, prefab_name, path, kv_block
	parts = line.split("|", 3)
	if len(parts) < 4:
		return None
	prefab_name = parts[1]
	path = parts[2]
	kv_block = parts[3]

	# Extract category from path
	# e.g. Assets/.../3.0 no cost (current)/Bury and buried/...
	m = re.search(r"3\.0 no cost \(current\)/([^/]+)/", path)
	category = m.group(1) if m else "Unknown"

	# Extract key-value pairs from the block before bracket sections
	# cardDesc may contain ';' so we need to be careful.
	data = {"prefab_name": prefab_name, "path": path, "category": category}

	# Use regex to pull out fields before bracket sections start
	# Sections always start with [UPPERCASE_TYPE_... so we search for that pattern
	m = re.search(r"\[(CONTAINER|TRIGGER|CHECK|PRE|EFFECT|HPALTER|SHIELD|ADDTEMP|CURSE|EXILE|BURY|STAGE|MANIP|TRANSFER|CHANGETARGET|CHANGEHPALTER|HPMAXALTER|AMPLIFIER|POWERREACTION|GIVER|CONSUMER)_", kv_block)
	if m:
		bracket_start = m.start()
		plain = kv_block[:bracket_start]
		brackets = kv_block[bracket_start:]
	else:
		plain = kv_block
		brackets = ""

	# Parse plain key=value; pairs
	# cardDesc may contain ';', so extract it first with a dedicated regex
	cardDesc_match = re.search(r"cardDesc=(.*?);isMinion=", plain)
	if cardDesc_match:
		data["cardDesc"] = cardDesc_match.group(1)
		# Remove cardDesc from plain to avoid double-parsing
		plain = plain.replace("cardDesc=" + data["cardDesc"] + ";", "", 1)

	for m in re.finditer(r"([^=;]+)=([^;]*)", plain):
		data[m.group(1)] = m.group(2)

	# Parse bracket sections
	containers = []
	effects = []
	for m in re.finditer(r"\[([A-Z_]+)_(.*?)\]", brackets):
		section_type = m.group(1)
		content = m.group(2)
		if section_type.startswith("CONTAINER"):
			containers.append({"name": content, "trigger": "", "checks": [], "pres": [], "effects": []})
		elif section_type == "TRIGGER":
			if containers:
				containers[-1]["trigger"] = content
		elif section_type in ("CHECK", "PRE", "EFFECT"):
			if containers:
				containers[-1][section_type.lower() + "s"].append(strip_assembly(content))
		else:
			effects.append({"type": section_type, "content": content})

	data["containers"] = containers
	data["effects"] = effects
	return data


def format_flags(data):
	flags = []
	if data.get("isMinion") == "True":
		flags.append("Minion")
	tags = data.get("tags", "")
	if tags and tags != "None":
		flags.append("Tags=" + tags)
	status = data.get("statusEffects", "")
	if status and status != "None":
		flags.append("Status=" + status)
	return " / ".join(flags) if flags else "None"


def format_costs(data):
	parts = []
	if data.get("buryCost", "0") != "0":
		parts.append("Bury=" + data.get("buryCost", "0"))
	if data.get("delayCost", "0") != "0":
		parts.append("Delay=" + data.get("delayCost", "0"))
	if data.get("exposeCost", "0") != "0":
		parts.append("Expose=" + data.get("exposeCost", "0"))
	minion = data.get("minionCostCount", "0")
	if minion != "0":
		minion_id = data.get("minionCostCardTypeID", "")
		minion_owner = data.get("minionCostOwner", "")
		parts.append("Minion=" + minion + "(" + minion_id + "/" + minion_owner + ")")
	return " / ".join(parts) if parts else "None"


def format_call(call_str):
	# e.g. BuryEffect->BuryMyCards(1,)
	# Strip Assembly-CSharp already done
	return call_str


def format_container(container):
	lines = []
	trigger = container.get("trigger", "")
	if trigger == "NONE":
		trigger = "NONE"
	name = container.get("name", "")
	calls = []
	for c in container.get("checks", []):
		calls.append("Check: " + format_call(c))
	for c in container.get("pres", []):
		calls.append("Pre: " + format_call(c))
	for c in container.get("effects", []):
		calls.append("Effect: " + format_call(c))
	calls_str = "<br>".join(calls)
	lines.append("- **" + name + "** | Trigger:`" + trigger + "` | " + calls_str)
	return "<br>".join(lines)


def format_key_fields(data):
	lines = []
	for eff in data.get("effects", []):
		t = eff["type"]
		c = eff["content"]
		if t == "HPALTER":
			m = re.search(r"baseDmg=(\S+) isStatusEffectDamage=(\S+) extraDmg=(\S+) statusEffectToCheck=(\S+)", c)
			if m:
				lines.append("HPAlter: baseDmg=" + m.group(1) + " extraDmg=" + m.group(3) + " statusEffect=" + m.group(4))
		elif t == "BURY":
			m = re.search(r"tagsToCheck=(.+)", c)
			if m and m.group(1) and m.group(1) != "None":
				lines.append("Bury: tag=" + m.group(1))
			else:
				lines.append("Bury")
		elif t == "STAGE":
			m = re.search(r"tagToCheck=(\S+) targetFriendly=(\S+) statusEffectToCheck=(\S+)", c)
			if m:
				extra = ""
				if m.group(3) and m.group(3) != "None":
					extra = " statusEffect=" + m.group(3)
				lines.append("Stage" + extra)
			else:
				lines.append("Stage")
		elif t == "EXILE":
			lines.append("Exile")
		elif t == "CURSE":
			m = re.search(r"powerCoefficient=(\S+)", c)
			if m:
				lines.append("Curse: powerCoefficient=" + m.group(1))
			else:
				lines.append("Curse")
		elif t == "ADDTEMP":
			m = re.search(r"cardCount=(\S+)", c)
			if m:
				lines.append("AddTemp: cardCount=" + m.group(1))
			else:
				lines.append("AddTemp")
		elif t == "TRANSFER":
			m = re.search(r"isFromFriendly=(\S+) statusEffectToTransfer=(\S+)", c)
			if m:
				lines.append("Transfer: fromFriendly=" + m.group(1) + " effect=" + m.group(2))
			else:
				lines.append("Transfer")
		elif t == "GIVER":
			m = re.search(r"statusEffectToGive=(\S+) statusEffectToCount=(\S+) .*?lastXCardsCount=(\S+) xFriendlyCount=(\S+) statusEffectLayerCount=(\S+) yFriendlyLayerCount=(\S+)", c)
			if m:
				give = m.group(1)
				count = m.group(2) if m.group(2) != "None" else "None"
				lastX = m.group(3) if m.group(3) != "0" else ""
				xFriendly = m.group(4) if m.group(4) != "0" else ""
				layer = m.group(5) if m.group(5) != "0" else ""
				yLayer = m.group(6) if m.group(6) != "0" else ""
				parts = []
				if lastX: parts.append("lastXCards=" + lastX)
				if xFriendly: parts.append("xFriendly=" + xFriendly)
				if layer: parts.append("layerCount=" + layer)
				if yLayer: parts.append("yLayerCount=" + yLayer)
				if count and count != "None":
					parts.insert(0, "count=" + count)
				lines.append("Giver: give=" + give + " " + ", ".join(parts))
			else:
				lines.append("Giver")
		elif t == "AMPLIFIER":
			m = re.search(r"statusEffectMultiplier=(\S+)", c)
			if m:
				lines.append("Amplifier: multiplier=" + m.group(1))
			else:
				lines.append("Amplifier")
		elif t == "POWERREACTION":
			m = re.search(r"powerAmount=(\S+)", c)
			if m:
				lines.append("PowerReaction: powerAmount=" + m.group(1))
			else:
				lines.append("PowerReaction")
		elif t == "CONSUMER":
			m = re.search(r"statusEffectToConsume=(\S+)", c)
			if m:
				lines.append("Consumer: consume=" + m.group(1))
			else:
				lines.append("Consumer")
		elif t == "SHIELD":
			lines.append("Shield")
		elif t == "CHANGETARGET":
			lines.append("ChangeTarget")
		elif t == "CHANGEHPALTER":
			lines.append("ChangeHpAlter")
		elif t == "HPMAXALTER":
			lines.append("HPMaxAlter")
		elif t == "MANIP":
			m = re.search(r"tagToCheck=(\S+)", c)
			if m and m.group(1) != "None":
				lines.append("Manip: tag=" + m.group(1))
			else:
				lines.append("Manip")
	return "<br>".join(lines)


def generate_markdown(cards):
	categories = {}
	for c in cards:
		cat = c["category"]
		categories.setdefault(cat, []).append(c)

	# Sort categories
	ordered_cats = []
	for cat in CATEGORY_ORDER:
		if cat in categories:
			ordered_cats.append((cat, categories[cat]))
	for cat in sorted(categories.keys()):
		if cat not in CATEGORY_ORDER:
			ordered_cats.append((cat, categories[cat]))

	lines = []
	lines.append("# OneDeck 3.0 No Cost Card Design Document")
	lines.append("")
	lines.append("> This document is auto-generated from prefab data under `Assets/Prefabs/Cards/3.0 no cost (current)`.")
	lines.append("> Generation date: " + datetime.now().strftime("%Y-%m-%d %H:%M"))
	lines.append("")
	lines.append("---")
	lines.append("")
	lines.append("## Table of Contents")
	lines.append("")
	lines.append("- [Overview](#overview)")
	lines.append("- [Glossary](#glossary)")
	for cat, _ in ordered_cats:
		anchor = cat.lower().replace(" ", "-")
		lines.append("- [" + cat + "](#" + anchor + ")")
	lines.append("")
	lines.append("---")
	lines.append("")
	lines.append("## Overview")
	lines.append("")
	lines.append("| Category | Count |")
	lines.append("|----------|-------|")
	total = 0
	for cat, cards_in_cat in ordered_cats:
		lines.append("| " + cat + " | " + str(len(cards_in_cat)) + " |")
		total += len(cards_in_cat)
	lines.append("| **Total** | **" + str(total) + "** |")
	lines.append("")
	lines.append("---")
	lines.append("")
	lines.append("## Glossary")
	lines.append("")
	lines.append("| Term | Description |")
	lines.append("|------|-------------|")
	lines.append("| Bury | Move card to the bottom of the deck |")
	lines.append("| Stage | Move card to the top of the deck |")
	lines.append("| Exile | Remove card from the game |")
	lines.append("| Linger | Card can trigger effects while positioned before the Start Card in deck |")
	lines.append("| DeathRattle | Effect triggers **only** when the card is buried (OnMeBuried). Exile, Stage, or other zone changes do **not** trigger DeathRattle. |")
	lines.append("| Power | Status effect; each stack increases damage by 1 |")
	lines.append("| Minion Cost | Consume N friendly Minion cards to activate the effect |")
	lines.append("")
	lines.append("---")
	lines.append("")

	for cat, cards_in_cat in ordered_cats:
		lines.append("## " + cat)
		lines.append("")
		# Sort cards by displayName or prefab_name
		cards_in_cat.sort(key=lambda x: x.get("displayName", x["prefab_name"]))
		for data in cards_in_cat:
			name = data.get("displayName", data["prefab_name"])
			card_id = data.get("cardTypeID", data["prefab_name"])
			desc = data.get("cardDesc", "").replace("\\n", "<br>")
			lines.append("### " + name + " (`" + card_id + "`)")
			lines.append("")
			lines.append("| Field | Value |")
			lines.append("|-------|-------|")
			lines.append("| Name | `" + name + "` (`" + card_id + "`) |")
			lines.append("| Flags | " + format_flags(data) + " |")
			lines.append("| Costs | " + format_costs(data) + " |")
			lines.append("| Desc | " + desc + " |")
			containers_str = "<br>".join([format_container(c) for c in data.get("containers", [])])
			lines.append("| Containers | " + containers_str + " |")
			key_fields = format_key_fields(data)
			lines.append("| Key Fields | " + key_fields + " |")
		lines.append("")

	lines.append("---")
	lines.append("")
	lines.append("> End of document")
	return "\r\n".join(lines)


def main():
	log_text = LOG_PATH.read_text(encoding="utf-8")
	cards = []
	for line in log_text.splitlines():
		line = line.strip()
		if not line:
			continue
		parsed = parse_line(line)
		if parsed:
			cards.append(parsed)
	md = generate_markdown(cards)
	OUT_PATH.write_text(md, encoding="utf-8")
	print("Wrote", OUT_PATH, "with", len(cards), "cards")


if __name__ == "__main__":
	main()
