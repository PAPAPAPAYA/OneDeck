# -*- coding: utf-8 -*-
"""Fix double-escaped cardDesc from the first migration run and apply desc patterns.

First run encoded descs with unicode_escape, turning \\uXXXX into \\\\uXXXX and
physical newlines into \\n. This script normalizes the value (fold YAML continuations,
collapse to single line), applies the attack-keyword replacements and writes a clean
single-line \\uXXXX-escaped desc.
"""
import io
import re
import glob
import sys

sys.path.insert(0, 'tools/outputs')
import migrate_phase2_prefabs as m


def fix_desc_in_file(path, patterns):
    txt = io.open(path, encoding='utf-8', newline='').read()
    m_desc = re.search(r'cardDesc: "((?:[^"\\]|\\.|\n    )*)"', txt)
    if not m_desc:
        return False
    raw = m_desc.group(1)
    # restore single-backslash escapes produced by the earlier unicode_escape round-trip
    normalized = raw.replace('\\\\u', '\\u').replace('\\\\n', '\\n')
    # fold YAML continuation lines (escaped newline + 4 spaces) into a space BEFORE decoding
    folded = normalized.replace('\\n    ', ' ')
    decoded = m.decode_desc(folded)
    if '\u653b\u51fb' in decoded and '<dmg>' not in decoded and '\u529b\u91cf' not in decoded:
        return False  # already migrated
    for old, new in patterns:
        decoded = decoded.replace(old, new)
    reencoded = m.encode_desc(decoded)
    txt = txt.replace('cardDesc: "%s"' % raw, 'cardDesc: "%s"' % reencoded, 1)
    io.open(path, 'w', encoding='utf-8', newline='').write(txt)
    return True


def main():
    patterns = m.desc_patterns()
    all_cards = set(m.ATTACK_CARDS.keys()) | {
        'WEAPON_SPIRIT', 'POWER_CRAVER', 'CROW_CROWD', 'POWER_SIPHONER', 'DR_MANHATTAN', 'PREMATURE',
        'CURSE_THIRST_ARCH_SUMMONER', 'CURSE_THIRST_SHAMAN', 'MARTYR', 'MAD_SCIENTIST', 'SACRIFICIAL_SWORD',
        'ELDER_SORCERER', 'POWER_TRANSFER',
    }
    for card_id in sorted(all_cards):
        path = m.find_prefab(card_id)
        if not path:
            print('MISSING PREFAB:', card_id)
            continue
        changed = fix_desc_in_file(path, patterns)
        if changed:
            print('desc fixed:', card_id)


if __name__ == '__main__':
    main()
