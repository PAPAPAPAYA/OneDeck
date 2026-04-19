#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Parse CardDesign_GenerationLog.txt and produce a structured Markdown document.
"""

import re
import os
from datetime import datetime

LOG_PATH = os.path.join(os.path.dirname(__file__), "CardDesign_GenerationLog.txt")
OUT_PATH = os.path.join(os.path.dirname(__file__), "3.0_no_cost_CardDesign.md")


def parse_log(path):
    cards = []
    with open(path, "r", encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line.startswith("CARD|"):
                continue
            parts = line.split("|", 3)
            if len(parts) < 4:
                continue
            prefab_name = parts[1]
            asset_path = parts[2]
            kv_part = parts[3]

            # Preprocess: bracketed names like [ju-on], [curse], [rift] in child names
            # break regexes because they contain ']' which is used as tag delimiter.
            # Replace them with parentheses to avoid truncation.
            kv_part = kv_part.replace("[ju-on]", "(ju-on)")
            kv_part = kv_part.replace("[curse]", "(curse)")
            kv_part = kv_part.replace("[rift]", "(rift)")

            # Extract category from path
            # e.g. Assets/Prefabs/Cards/3.0 no cost (current)/Bury and buried/...
            m = re.search(r"/3\.0 no cost \(current\)/([^/]+)/", asset_path)
            category = m.group(1) if m else "Unknown"
            subcategory = ""
            # Check for subfolder like DeathRattle
            m2 = re.search(r"/3\.0 no cost \(current\)/[^/]+/([^/]+)/[^/]+\.prefab", asset_path)
            if m2:
                subcategory = m2.group(1)

            # cardDesc extraction: it may contain semicolons
            card_desc_match = re.search(r"cardDesc=(.*?);isMinion=", kv_part)
            card_desc = card_desc_match.group(1) if card_desc_match else ""
            card_desc = card_desc.replace("\\n", "\n")

            # Simple kv extraction for other fields
            def get_field(key):
                pat = re.escape(key) + r"=(.*?);"
                m = re.search(pat, kv_part)
                return m.group(1) if m else ""

            card = {
                "prefab_name": prefab_name,
                "asset_path": asset_path,
                "category": category,
                "subcategory": subcategory,
                "cardTypeID": get_field("cardTypeID"),
                "displayName": get_field("displayName"),
                "cardDesc": card_desc,
                "isMinion": get_field("isMinion"),
                "buryCost": get_field("buryCost"),
                "delayCost": get_field("delayCost"),
                "exposeCost": get_field("exposeCost"),
                "minionCostCount": get_field("minionCostCount"),
                "minionCostCardTypeID": get_field("minionCostCardTypeID"),
                "minionCostOwner": get_field("minionCostOwner"),
                "statusEffects": get_field("statusEffects"),
                "tags": get_field("tags"),
                "containers": [],
                "effects": [],
            }

            # Extract containers and their triggers/calls
            container_blocks = re.findall(
                r"\[CONTAINER_(\d+) name=(.+?)\](?=\[|$)", kv_part
            )
            for idx, name in container_blocks:
                # Find the slice of kv_part belonging to this container
                # We locate the CONTAINER block and scan forward until next CONTAINER or effect blocks not associated
                # Simpler: regex for trigger and calls immediately after the container tag
                container_start = kv_part.find(f"[CONTAINER_{idx} name={name}]")
                container_end = len(kv_part)
                # Find next CONTAINER
                next_cont = re.search(r"\[CONTAINER_\d+ name=", kv_part[container_start + 1:])
                if next_cont:
                    container_end = container_start + 1 + next_cont.start()
                container_slice = kv_part[container_start:container_end]

                trigger_match = re.search(r"\[TRIGGER_(.+?)\]", container_slice)
                trigger = trigger_match.group(1) if trigger_match else "NONE"

                checks = re.findall(r"\[CHECK_(.+?)\]", container_slice)
                pres = re.findall(r"\[PRE_(.+?)\]", container_slice)
                effects = re.findall(r"\[EFFECT_(.+?)\]", container_slice)

                # Clean assembly names
                def clean_call(s):
                    return s.replace(", Assembly-CSharp", "")

                checks = [clean_call(c) for c in checks]
                pres = [clean_call(c) for c in pres]
                effects = [clean_call(c) for c in effects]

                card["containers"].append({
                    "name": name,
                    "trigger": trigger,
                    "checks": checks,
                    "pres": pres,
                    "effects": effects,
                })

            # Extract standalone effect components (HPALTER, BURY, STAGE, etc.)
            effect_patterns = [
                (r"\[HPALTER_(.+?)\]", "HPAlter"),
                (r"\[SHIELD_(.+?)\]", "ShieldAlter"),
                (r"\[ADDTEMP_(.+?)\]", "AddTempCard"),
                (r"\[CURSE_(.+?)\]", "Curse"),
                (r"\[EXILE_(.+?)\]", "Exile"),
                (r"\[BURY_(.+?)\]", "Bury"),
                (r"\[STAGE_(.+?)\]", "Stage"),
                (r"\[MANIP_(.+?)\]", "CardManip"),
                (r"\[TRANSFER_(.+?)\]", "Transfer"),
                (r"\[CHANGETARGET_(.+?)\]", "ChangeTarget"),
                (r"\[CHANGEHPALTER_(.+?)\]", "ChangeHpAlter"),
                (r"\[HPMAXALTER_(.+?)\]", "HPMaxAlter"),
                (r"\[AMPLIFIER_(.+?)\]", "Amplifier"),
                (r"\[GIVER_(.+?)\]", "StatusGiver"),
                (r"\[CONSUMER_(.+?)\]", "Consume"),
            ]
            for pattern, eftype in effect_patterns:
                for match in re.findall(pattern, kv_part):
                    card["effects"].append({"type": eftype, "data": match})

            cards.append(card)
    return cards


def build_markdown(cards):
    # Group by category, then subcategory
    groups = {}
    for c in cards:
        cat = c["category"]
        sub = c["subcategory"]
        if cat not in groups:
            groups[cat] = {}
        if sub not in groups[cat]:
            groups[cat][sub] = []
        groups[cat][sub].append(c)

    # Sort categories and cards
    category_order = ["General", "Curse", "Conjure", "Bury and buried"]
    sorted_cats = []
    for cat in category_order:
        if cat in groups:
            sorted_cats.append(cat)
    for cat in sorted(groups.keys()):
        if cat not in sorted_cats:
            sorted_cats.append(cat)

    total = len(cards)
    cat_counts = {cat: sum(len(v) for v in groups[cat].values()) for cat in sorted_cats}

    lines = []
    lines.append("# OneDeck 3.0 No Cost Card Design Document")
    lines.append("")
    lines.append("> This document is auto-generated from prefab data under `Assets/Prefabs/Cards/3.0 no cost (current)`.")
    lines.append(f"> Generation date: {datetime.now().strftime('%Y-%m-%d')}")
    lines.append("")
    lines.append("---")
    lines.append("")
    lines.append("## Table of Contents")
    lines.append("")
    lines.append("- [Overview](#overview)")
    lines.append("- [Glossary](#glossary)")
    for cat in sorted_cats:
        anchor = cat.lower().replace(" ", "-")
        lines.append(f"- [{cat}](#{anchor})")
        for sub in sorted(groups[cat].keys()):
            if sub:
                sub_anchor = (cat + "-" + sub).lower().replace(" ", "-")
                lines.append(f"  - [{sub}](#{sub_anchor})")
    lines.append("")
    lines.append("---")
    lines.append("")
    lines.append("## Overview")
    lines.append("")
    lines.append("| Category | Count |")
    lines.append("|----------|-------|")
    for cat in sorted_cats:
        lines.append(f"| {cat} | {cat_counts[cat]} |")
    lines.append(f"| **Total** | **{total}** |")
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
    lines.append("| DeathRattle | Effect triggers when the card is buried |")
    lines.append("| Power | Status effect; each stack increases damage by 1 |")
    lines.append("| Minion Cost | Consume N friendly Minion cards to activate the effect |")
    lines.append("")

    for cat in sorted_cats:
        anchor = cat.lower().replace(" ", "-")
        lines.append(f"## {cat}")
        lines.append("")
        for sub in sorted(groups[cat].keys()):
            if sub:
                sub_anchor = (cat + "-" + sub).lower().replace(" ", "-")
                lines.append(f"### {sub}")
                lines.append("")
            for card in groups[cat][sub]:
                lines.append(f"#### {card['displayName']} (`{card['cardTypeID']}`)")
                lines.append("")
                lines.append("| Field | Value |")
                lines.append("|-------|-------|")
                flags = []
                if card["isMinion"] == "True":
                    flags.append("Minion")
                if card["tags"]:
                    flags.append(f"Tags={card['tags']}")
                if card["statusEffects"]:
                    flags.append(f"Status={card['statusEffects']}")
                flags_str = ", ".join(flags) if flags else "None"
                lines.append(f"| Flags | {flags_str} |")

                costs = []
                if card["buryCost"] != "0":
                    costs.append(f"Bury={card['buryCost']}")
                if card["delayCost"] != "0":
                    costs.append(f"Delay={card['delayCost']}")
                if card["exposeCost"] != "0":
                    costs.append(f"Expose={card['exposeCost']}")
                if card["minionCostCount"] != "0":
                    minion_info = card["minionCostCount"]
                    if card["minionCostCardTypeID"]:
                        minion_info += f"[{card['minionCostCardTypeID']}]"
                    if card["minionCostOwner"]:
                        minion_info += f"({card['minionCostOwner']})"
                    costs.append(f"Minion={minion_info}")
                costs_str = ", ".join(costs) if costs else "None"
                lines.append(f"| Costs | {costs_str} |")

                desc = card["cardDesc"].replace("\n", "<br>")
                lines.append(f"| Desc | {desc} |")

                if card["containers"]:
                    for cont in card["containers"]:
                        trigger = cont["trigger"]
                        cont_str = f"**{cont['name']}** | Trigger: `{trigger}`"
                        if cont["checks"]:
                            cont_str += f"<br>Check: `{', '.join(cont['checks'])}`"
                        if cont["pres"]:
                            cont_str += f"<br>Pre: `{', '.join(cont['pres'])}`"
                        if cont["effects"]:
                            cont_str += f"<br>Effect: `{', '.join(cont['effects'])}`"
                        lines.append(f"| Container | {cont_str} |")

                # Key effect fields
                key_fields = []
                for eff in card["effects"]:
                    if eff["type"] == "HPAlter":
                        key_fields.append(f"HPAlter: {eff['data']}")
                    elif eff["type"] == "Curse":
                        key_fields.append(f"Curse: {eff['data']}")
                    elif eff["type"] == "Amplifier":
                        key_fields.append(f"Amplifier: {eff['data']}")
                    elif eff["type"] == "StatusGiver":
                        key_fields.append(f"StatusGiver: {eff['data']}")
                    elif eff["type"] == "AddTempCard":
                        key_fields.append(f"AddTempCard: {eff['data']}")
                    elif eff["type"] == "Consume":
                        key_fields.append(f"Consume: {eff['data']}")
                    elif eff["type"] == "Transfer":
                        key_fields.append(f"Transfer: {eff['data']}")
                    elif eff["type"] == "Bury":
                        key_fields.append(f"Bury: {eff['data']}")
                    elif eff["type"] == "Stage":
                        key_fields.append(f"Stage: {eff['data']}")
                    elif eff["type"] == "Exile":
                        key_fields.append(f"Exile: {eff['data']}")

                if key_fields:
                    lines.append(f"| Key Fields | {'<br>'.join(key_fields)} |")

                lines.append("")
        lines.append("---")
        lines.append("")

    lines.append("> End of document")
    return "\n".join(lines)


def main():
    cards = parse_log(LOG_PATH)
    md = build_markdown(cards)
    with open(OUT_PATH, "w", encoding="utf-8") as f:
        f.write(md)
    print(f"Generated: {OUT_PATH} ({len(cards)} cards)")


if __name__ == "__main__":
    main()
