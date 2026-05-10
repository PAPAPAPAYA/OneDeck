#!/usr/bin/env python3
# -*- coding: utf-8 -*-
import re

md = open('docs/3.0_no_cost_CardDesign.md', 'r', encoding='utf-8').read()
current_cat = None
cards = {}
for line in md.splitlines():
    m = re.match(r'^## (.+)$', line)
    if m and m.group(1) not in ['Table of Contents', 'Overview', 'Glossary']:
        current_cat = m.group(1).strip()
        cards[current_cat] = []
    m = re.match(r'^### (.+?) \(`', line)
    if m and current_cat:
        cards[current_cat].append(m.group(1))

out = open('docs/category_count.txt', 'w', encoding='utf-8')
for cat, lst in cards.items():
    out.write(f'{cat}: {len(lst)}\n')
    for c in lst:
        out.write(f'  - {c}\n')
    out.write('\n')
out.close()
print('Done')
