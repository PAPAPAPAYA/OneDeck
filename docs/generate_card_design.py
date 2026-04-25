#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Parse CardDesign_GenerationLog.txt and generate a Markdown card design document."""

import re
import os
from datetime import datetime

LOG_PATH = os.path.join(os.path.dirname(__file__), "CardDesign_GenerationLog.txt")
OUTPUT_PATH = os.path.join(os.path.dirname(__file__), "3.0_no_cost_CardDesign.md")


def strip_assembly(name):
	return name.replace(", Assembly-CSharp", "")


def parse_log(path):
	cards = []
	with open(path, "r", encoding="utf-8") as f:
		for line in f:
			line = line.strip()
			if not line.startswith("CARD|"):
				continue
			# Split into exactly 4 parts: CARD, name, path, kv+containers
			parts = line.split("|", 3)
			if len(parts) < 4:
				continue
			prefab_name = parts[1]
			asset_path = parts[2]
			rest = parts[3]

			# Extract category from path
			category = "Unknown"
			m = re.search(r"3\.0 no cost \(current\)/([^/]+)/", asset_path)
			if m:
				category = m.group(1)
			# Also check for subcategory like DeathRattle
			sub_m = re.search(r"3\.0 no cost \(current\)/[^/]+/([^/]+)/[^/]+\.prefab", asset_path)
			subcategory = sub_m.group(1) if sub_m else None

			# Extract cardDesc using regex because it may contain semicolons
			card_desc = ""
			desc_m = re.search(r"cardDesc=(.*?);isMinion=", rest)
			if desc_m:
				card_desc = desc_m.group(1).replace("\\n", "\n")

			# Parse key=value pairs before first bracket
			kv_part = rest
			first_bracket = rest.find("[")
			if first_bracket != -1:
				kv_part = rest[:first_bracket]

			fields = {}
			for pair in kv_part.split(";"):
				if "=" not in pair:
					continue
				k, v = pair.split("=", 1)
				fields[k.strip()] = v.strip()

			# Parse containers and effects from the rest
			containers = []
			effects = []
			if first_bracket != -1:
				bracket_part = rest[first_bracket:]
				# Parse containers
				container_pattern = r"\[CONTAINER_(\d+) name=(.+?)\](?=\[|$)"
				for cm in re.finditer(container_pattern, bracket_part):
					cidx = cm.group(1)
					cname = cm.group(2)
					# Find the segment for this container (from its start to next CONTAINER or end)
					start_pos = cm.start()
					next_container = re.search(r"\[CONTAINER_", bracket_part[start_pos + 1:])
					if next_container:
						seg_end = start_pos + 1 + next_container.start()
					else:
						seg_end = len(bracket_part)
					segment = bracket_part[start_pos:seg_end]

					trigger = ""
					trigger_m = re.search(r"\[TRIGGER_(.+?)\]", segment)
					if trigger_m:
						trigger = trigger_m.group(1)

					checks = []
					for check_m in re.finditer(r"\[CHECK_(.+?)\]", segment):
						checks.append(strip_assembly(check_m.group(1)))

					pres = []
					for pre_m in re.finditer(r"\[PRE_(.+?)\]", segment):
						pres.append(strip_assembly(pre_m.group(1)))

					effs = []
					for eff_m in re.finditer(r"\[EFFECT_(.+?)\]", segment):
						effs.append(strip_assembly(eff_m.group(1)))

					containers.append({
						"index": cidx,
						"name": cname,
						"trigger": trigger,
						"checks": checks,
						"pres": pres,
						"effects": effs,
					})

				# Parse effect components (non-container brackets that have type prefixes)
				# These are outside of container segments typically, or we can scan whole bracket_part
				effect_types = [
					("HPALTER", r"\[HPALTER_(.+?) baseDmg=(.+?) isStatusEffectDamage=(.+?) extraDmg=(.+?) statusEffectToCheck=(.+?)\]"),
					("SHIELD", r"\[SHIELD_(.+?)\]"),
					("ADDTEMP", r"\[ADDTEMP_(.+?) cardCount=(.+?) curseCardTypeID=(.*?)\]"),
					("CURSE", r"\[CURSE_(.+?) cardTypeID=(.*?) cardPrefab=(.*?) powerCoefficient=(.+?)\]"),
					("EXILE", r"\[EXILE_(.+?) tagToCheck=(.+?)\]"),
					("BURY", r"\[BURY_(.+?) tagToCheck=(.+?)\]"),
					("STAGE", r"\[STAGE_(.+?) tagToCheck=(.+?) targetFriendly=(.+?) statusEffectToCheck=(.+?)\]"),
					("MANIP", r"\[MANIP_(.+?) tagToCheck=(.+?)\]"),
					("TRANSFER", r"\[TRANSFER_(.+?) isFromFriendly=(.+?) statusEffectToTransfer=(.+?) curseCardTypeID=(.*?)\]"),
					("CHANGETARGET", r"\[CHANGETARGET_(.+?)\]"),
					("CHANGEHPALTER", r"\[CHANGEHPALTER_(.+?)\]"),
					("HPMAXALTER", r"\[HPMAXALTER_(.+?)\]"),
					("AMPLIFIER", r"\[AMPLIFIER_(.+?) statusEffectToGive=(.+?) statusEffectToCount=(.+?) statusEffectMultiplier=(.+?) target=(.+?) includeSelf=(.+?) lastXCardsCount=(.+?) xFriendlyCount=(.+?) statusEffectLayerCount=(.+?) yFriendlyLayerCount=(.+?)\]"),
					("POWERREACTION", r"\[POWERREACTION_(.+?) powerAmount=(.+?) excludeSelf=(.+?) statusEffectToGive=(.+?) statusEffectToCount=(.+?) target=(.+?) includeSelf=(.+?) lastXCardsCount=(.+?) xFriendlyCount=(.+?) statusEffectLayerCount=(.+?) yFriendlyLayerCount=(.+?)\]"),
					("GIVER", r"\[GIVER_(.+?) statusEffectToGive=(.+?) statusEffectToCount=(.+?) target=(.+?) includeSelf=(.+?) lastXCardsCount=(.+?) xFriendlyCount=(.+?) statusEffectLayerCount=(.+?) yFriendlyLayerCount=(.+?)\]"),
					("CONSUMER", r"\[CONSUMER_(.+?) statusEffectToConsume=(.+?)\]"),
				]
				for eff_name, eff_pattern in effect_types:
					for em in re.finditer(eff_pattern, bracket_part):
						effects.append((eff_name, em.groups()))

			cards.append({
				"prefab_name": prefab_name,
				"asset_path": asset_path,
				"category": category,
				"subcategory": subcategory,
				"card_type_id": fields.get("cardTypeID", ""),
				"display_name": fields.get("displayName", ""),
				"card_desc": card_desc,
				"is_minion": fields.get("isMinion", "False"),
				"bury_cost": fields.get("buryCost", "0"),
				"delay_cost": fields.get("delayCost", "0"),
				"expose_cost": fields.get("exposeCost", "0"),
				"minion_cost_count": fields.get("minionCostCount", "0"),
				"minion_cost_card_type_id": fields.get("minionCostCardTypeID", ""),
				"minion_cost_owner": fields.get("minionCostOwner", ""),
				"status_effects": fields.get("statusEffects", ""),
				"tags": fields.get("tags", ""),
				"containers": containers,
				"effects": effects,
			})
	return cards


def generate_markdown(cards):
	# Group by category, preserving order
	from collections import OrderedDict
	groups = OrderedDict()
	for card in cards:
		cat = card["category"]
		if cat not in groups:
			groups[cat] = []
		groups[cat].append(card)

	# Build category table
	cat_table = ""
	cat_toc = ""
	total = 0
	for cat, cat_cards in groups.items():
		count = len(cat_cards)
		total += count
		cat_table += "| {0} | {1} |\n".format(cat, count)
		anchor = cat.lower().replace(" ", "-").replace("&", "and")
		cat_toc += "- [{0}](#{1})\n".format(cat, anchor)

	# Build category sections
	sections = ""
	for cat, cat_cards in groups.items():
		anchor = cat.lower().replace(" ", "-").replace("&", "and")
		sections += "## {0}\n\n".format(cat)
		for card in cat_cards:
			sections += render_card(card)
			sections += "\n"

	# Template
	md = """# OneDeck 3.0 No Cost Card Design Document

> This document is auto-generated from prefab data under `Assets/Prefabs/Cards/3.0 no cost (current)`.
> Generation date: {date}

---

## Table of Contents

- [Overview](#overview)
- [Glossary](#glossary)
{toc}

---

## Overview

| Category | Count |
|----------|-------|
{cat_table}| **Total** | **{total}** |

---

## Glossary

| Term | Description |
|------|-------------|
| Bury | Move card to the bottom of the deck |
| Stage | Move card to the top of the deck |
| Exile | Remove card from the game |
| Linger | Card can trigger effects while positioned before the Start Card in deck |
| DeathRattle | Effect triggers **only** when the card is buried (OnMeBuried). Exile, Stage, or other zone changes do **not** trigger DeathRattle. |
| Power | Status effect; each stack increases damage by 1 |
| Minion Cost | Consume N friendly Minion cards to activate the effect |

---

{sections}

---

> End of document
""".format(
		date=datetime.now().strftime("%Y-%m-%d %H:%M"),
		toc=cat_toc,
		cat_table=cat_table,
		total=total,
		sections=sections,
	)
	return md


def render_card(card):
	lines = []
	lines.append("### {0} (`{1}`)".format(card["display_name"], card["card_type_id"]))
	lines.append("")

	# Flags
	flags = []
	if card["is_minion"] == "True":
		flags.append("Minion")
	if card["tags"]:
		flags.append("Tags=" + card["tags"])
	if card["status_effects"]:
		flags.append("Status=" + card["status_effects"])
	flags_str = " / ".join(flags) if flags else "None"

	# Costs
	costs = []
	if card["bury_cost"] != "0":
		costs.append("Bury=" + card["bury_cost"])
	if card["delay_cost"] != "0":
		costs.append("Delay=" + card["delay_cost"])
	if card["expose_cost"] != "0":
		costs.append("Expose=" + card["expose_cost"])
	if card["minion_cost_count"] != "0":
		minion_cost = "Minion=" + card["minion_cost_count"]
		if card["minion_cost_card_type_id"]:
			minion_cost += "[" + card["minion_cost_card_type_id"] + "]"
		if card["minion_cost_owner"]:
			minion_cost += "(" + card["minion_cost_owner"] + ")"
		costs.append(minion_cost)
	costs_str = " / ".join(costs) if costs else "None"

	lines.append("| Field | Value |")
	lines.append("|-------|-------|")
	lines.append("| Name | `{0}` (`{1}`) |".format(card["display_name"], card["card_type_id"]))
	lines.append("| Flags | {0} |".format(flags_str))
	lines.append("| Costs | {0} |".format(costs_str))
	# Description with newlines converted to <br>
	desc = card["card_desc"].replace("\n", "<br>")
	lines.append("| Desc | {0} |".format(desc))

	# Containers
	if card["containers"]:
		container_lines = []
		for c in card["containers"]:
			trigger = c["trigger"] if c["trigger"] else "NONE"
			calls = []
			if c["checks"]:
				calls.append("Check: " + ", ".join(c["checks"]))
			if c["pres"]:
				calls.append("Pre: " + ", ".join(c["pres"]))
			if c["effects"]:
				calls.append("Effect: " + ", ".join(c["effects"]))
			calls_str = "; ".join(calls) if calls else "None"
			container_lines.append("- **{0}** | Trigger:`{1}` | {2}".format(c["name"], trigger, calls_str))
		lines.append("| Containers | {0} |".format("<br>".join(container_lines)))

	# Key effect fields
	key_fields = []
	for eff_name, eff_vals in card["effects"]:
		if eff_name == "HPALTER":
			# name, baseDmg, isStatusEffectDamage, extraDmg, statusEffectToCheck
			base = eff_vals[1]
			extra = eff_vals[3]
			se = eff_vals[4]
			key_fields.append("HPAlter: baseDmg={0} extraDmg={1} statusEffect={2}".format(base, extra, se))
		elif eff_name == "CURSE":
			# name, cardTypeID, cardPrefab, powerCoefficient
			coef = eff_vals[3]
			key_fields.append("Curse: powerCoefficient={0}".format(coef))
		elif eff_name == "ADDTEMP":
			# name, cardCount, curseCardTypeID
			count = eff_vals[1]
			key_fields.append("AddTemp: cardCount={0}".format(count))
		elif eff_name == "AMPLIFIER":
			# name, statusEffectToGive, statusEffectToCount, multiplier, target, includeSelf, ...
			mult = eff_vals[3]
			key_fields.append("Amplifier: multiplier={0}".format(mult))
		elif eff_name == "POWERREACTION":
			# name, powerAmount, excludeSelf, ...
			pamt = eff_vals[1]
			key_fields.append("PowerReaction: powerAmount={0}".format(pamt))
		elif eff_name == "GIVER":
			# name, statusEffectToGive, statusEffectToCount, target, includeSelf, lastXCardsCount, xFriendlyCount, statusEffectLayerCount, yFriendlyLayerCount
			seg = eff_vals[0]
			give = eff_vals[1]
			count = eff_vals[2]
			lastx = eff_vals[5]
			xf = eff_vals[6]
			slc = eff_vals[7]
			yflc = eff_vals[8]
			parts = []
			if lastx != "0":
				parts.append("lastXCards={0}".format(lastx))
			if xf != "0":
				parts.append("xFriendly={0}".format(xf))
			if slc != "0":
				parts.append("layerCount={0}".format(slc))
			if yflc != "0":
				parts.append("yLayerCount={0}".format(yflc))
			key_fields.append("Giver: give={0} count={1} {2}".format(give, count, ", ".join(parts)))
		elif eff_name == "CONSUMER":
			# name, statusEffectToConsume
			sec = eff_vals[1]
			key_fields.append("Consumer: consume={0}".format(sec))
		elif eff_name == "STAGE":
			# name, tagToCheck, targetFriendly, statusEffectToCheck
			tag = eff_vals[1]
			tf = eff_vals[2]
			sec = eff_vals[3]
			parts = []
			if tag != "None":
				parts.append("tag={0}".format(tag))
			if tf != "False":
				parts.append("targetFriendly={0}".format(tf))
			if sec != "None":
				parts.append("statusEffect={0}".format(sec))
			key_fields.append("Stage: {0}".format(", ".join(parts)) if parts else "Stage")
		elif eff_name == "BURY":
			tag = eff_vals[1]
			if tag != "None":
				key_fields.append("Bury: tag={0}".format(tag))
			else:
				key_fields.append("Bury")
		elif eff_name == "EXILE":
			tag = eff_vals[1]
			if tag != "None":
				key_fields.append("Exile: tag={0}".format(tag))
			else:
				key_fields.append("Exile")
		elif eff_name == "TRANSFER":
			# name, isFromFriendly, statusEffectToTransfer, curseCardTypeID
			iff = eff_vals[1]
			setr = eff_vals[2]
			key_fields.append("Transfer: fromFriendly={0} effect={1}".format(iff, setr))

	if key_fields:
		lines.append("| Key Fields | {0} |".format("<br>".join(key_fields)))

	return "\n".join(lines)


def main():
	cards = parse_log(LOG_PATH)
	md = generate_markdown(cards)
	with open(OUTPUT_PATH, "w", encoding="utf-8") as f:
		f.write(md)
	print("Generated {0} with {1} cards.".format(OUTPUT_PATH, len(cards)))


if __name__ == "__main__":
	main()
