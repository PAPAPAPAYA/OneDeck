#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Update Obsidian card tags based on effect rules."""

import re
from pathlib import Path

VAULT_PATH = Path("C:/Users/damen/Documents/Obsidian Vault")
CARDS_DIR = VAULT_PATH / "OneDeck" / "卡片库"

TAG_RULES = {
    "伤害": ["ALL_FOR_ONE", "ALMIGHTY", "AVENGER", "BLACKSMITH", "BODY_CANON", "BONE_COMBINATION",
            "COFFIN_MAKER", "CORPSE_CANON", "CURSED_CORPSE", "CURSE_THIRST_BEAST", "ETERNAL_GHOST",
            "FLESH_COMBINATION", "GOBLIN_ASSASSIN_TEAM", "GOBLIN_CHARGE_TEAM", "GRAVE_INVITATION",
            "GRAVE_KEEPER", "GRAVE_PUNCH", "POISONER", "POWER_CRAVER", "POWER_SIPHONER", "POWER_SURGE",
            "RIFT_DEVOURER", "RIFT_DRAGON", "RIFT_INSECT", "RIFT_MONSTER", "SCAPEGOAT", "SLIME",
            "SMALL_SCALE_DEATH", "SNATCHER", "SOLDIER_SKELETON", "SPIKE_SKELETON", "TACTICAL_BREACHER",
            "THE_FOOL", "UNFINISHED_ROBOT"],
    "埋葬": ["ALMIGHTY", "ANTI_CREATURE_WEAPON", "BODY_CANON", "BOOSTER", "COFFIN_MAKER",
            "CONFUSED_PORTALMANCER", "CORPSE_CANON", "DR_MANHATTAN", "FALL_INTO_RIFT",
            "GOBLIN_ASSASSIN_TEAM", "GRAVE_INVITATION", "GRAVE_PORTAL", "GRAVE_PUNCH", "GRAVE_TOGETHER",
            "LARGE_SCALE_DEATH", "RIFT_COFFIN", "RIFT_GUIDE", "SACRIFICE_RITUAL", "SACRIFICIAL_CURSE",
            "SACRIFICIAL_SWORD", "SNATCHER", "UNSTABLE_PORTAL", "WISE_BURIAL"],
    "亡语": ["AVENGER", "CONFUSED_PORTALMANCER", "CORPSE_CANON", "CURSED_CORPSE",
            "GRAVE_KEEPER", "GRAVE_PORTAL", "MARTYR", "SCAPEGOAT", "SLIME",
            "SOLDIER_SKELETON", "SPIKE_SKELETON", "UNDEAD_CURSER"],
    "萦绕": ["CURSE_ENCHANTMENT", "DEATHBED_CURSE", "ETERNAL_GHOST", "QUICK_RESPONSE_PROTOCOL",
             "RIFT_COFFIN", "WEAPON_SPIRIT"],
    "污染": ["ALMIGHTY", "CURSED_CORPSE", "CURSED_SKELETON", "CURSE_ENCHANTMENT", "CURSE_SUMMONER",
            "DEATHBED_CURSE", "DETERIORATION", "POISONER", "RIFT_CURSE", "SACRIFICIAL_CURSE",
            "SIDE_EFFECT_PORTAL", "SMALL_SCALE_DEATH", "UNDEAD_CURSER"],
    "任意门": ["ALMIGHTY", "CONFUSED_PORTALMANCER", "FALL_INTO_RIFT", "RIFT_CURSE", "RIFT_DRAGON",
              "RIFT_INSECT", "SACRIFICE_RITUAL"],
    "吞噬": ["RIFT_GUIDE", "RIFT_MONSTER", "RIFT_SUMMONER"],
    "预言": ["ADVANCE_PORTAL", "ALMIGHTY", "BOOSTER", "CURSE_SUMMONER", "CURSE_THIRST_BEAST",
            "CURSE_THIRST_SUMMONER", "DR_MANHATTAN", "GRAVE_KEEPER", "GRAVE_PORTAL",
            "MOTH_MAN", "QUICK_RESPONSE_PROTOCOL", "SCAPEGOAT", "SNATCHER",
            "SOLDIER_SKELETON", "UNSTABLE_PORTAL"],
    "强化": ["ALMIGHTY", "BLACKSMITH", "BLIND_COMBAT_PRIEST", "CURSE_THIRST_SHAMAN",
            "CURSE_THIRST_SUMMONER", "ELDER_SORCERER", "MAD_SCIENTIST", "MARTYR", "POWER_CRAVER",
            "POWER_SIPHONER", "POWER_SURGE", "POWER_TRANSFER", "RIFT_DEVOURER", "SACRIFICIAL_SWORD",
            "TACTICAL_BREACHER", "UNFINISHED_ROBOT", "WEAPON_SPIRIT"],
    "消化": ["CURSE_SUMMONER", "CURSE_THIRST_SUMMONER", "DR_MANHATTAN", "POWER_SIPHONER"],
}


def build_card_tags():
    """Build a mapping from card filename stem to sorted list of tags."""
    card_tags = {}
    for tag, cards in TAG_RULES.items():
        for card in cards:
            card_tags.setdefault(card, set()).add(tag)
    return {card: sorted(tags) for card, tags in card_tags.items()}


def update_frontmatter_tags(content: str, new_tags: list[str]) -> str:
    """Replace tags in markdown frontmatter while preserving everything else."""
    if not content.startswith("---"):
        return content

    # Split frontmatter and body
    match = re.match(r"---\r?\n(.*?)\r?\n---\r?\n(.*)", content, re.DOTALL)
    if not match:
        return content

    frontmatter = match.group(1)
    body = match.group(2)

    lines = frontmatter.splitlines()
    new_lines = []
    skip = False

    for line in lines:
        if skip:
            # Continue skipping tag list lines (start with whitespace + dash)
            if re.match(r"^\s+-\s", line):
                continue
            # Also skip continuation of inline tags if somehow multi-line
            skip = False

        stripped = line.strip()
        if stripped.startswith("tags:"):
            skip = True
            continue

        new_lines.append(line)

    # Remove trailing blank lines from frontmatter
    while new_lines and new_lines[-1].strip() == "":
        new_lines.pop()

    # Add new tags block
    if new_tags:
        new_lines.append("tags:")
        for tag in new_tags:
            new_lines.append(f"  - {tag}")
    else:
        new_lines.append("tags: []")

    new_frontmatter = "\r\n".join(new_lines)
    return f"---\r\n{new_frontmatter}\r\n---\r\n{body}"


def main():
    card_tags = build_card_tags()
    unmatched = []

    for md_file in sorted(CARDS_DIR.glob("*.md")):
        if md_file.name == "README.md":
            continue

        stem = md_file.stem
        tags = card_tags.get(stem, [])

        content = md_file.read_text(encoding="utf-8", newline="")

        if not tags:
            unmatched.append(stem)
            new_content = update_frontmatter_tags(content, [])
            md_file.write_text(new_content, encoding="utf-8", newline="")
            print(f"Cleared tags for {stem}")
            continue

        new_content = update_frontmatter_tags(content, tags)
        md_file.write_text(new_content, encoding="utf-8", newline="")
        print(f"Updated {stem}: {tags}")

    print("\nUnmatched cards:")
    for card in unmatched:
        print(f"  - {card}")


if __name__ == "__main__":
    main()
