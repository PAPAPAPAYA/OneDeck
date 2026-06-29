import os, re

folder = 'C:/Users/damen/Documents/Obsidian Vault/OneDeck/卡片库'
map_path = 'C:/Users/damen/Documents/Obsidian Vault/OneDeck/卡片关系/methods收益条件mapping.md'
cond_dir = 'C:/Users/damen/Documents/Obsidian Vault/OneDeck/卡片关系/条件'
payoff_dir = 'C:/Users/damen/Documents/Obsidian Vault/OneDeck/卡片关系/收益'


def normalize(s):
	return s.replace('{N}', 'N').replace('{M}', 'M').replace('{X}', 'X').replace('<counter>', '').strip()


split_rules = {
	'造成{N}伤害x本回合被埋葬的敌方数量': ('本回合每有一敌方被埋葬', '造成{N}伤害'),
	'造成友方数量的伤害': ('每有一友方', '造成{N}伤害'),
	'造成所有卡的力量数量的伤害': ('所有卡上每有一力量', '造成{N}伤害'),
	'造成{N}伤害x{M}': ('重复M次', '造成{N}伤害'),
	'转移所有友方的{N}力量到自身': ('消耗所有友方N力量', '给予自身N力量'),
}

source_to_payoff = {}
for src, (cond, pay) in split_rules.items():
	source_to_payoff.setdefault(normalize(src), []).append(normalize(pay))


def doc_pay_to_ours(p, payoffs):
	if p in payoffs:
		return [p]
	if p in source_to_payoff:
		return source_to_payoff[p]
	return []


def parse_frontmatter(text):
	m = re.search(r'^---\s*\r?\n(.*?)\r?\n---', text, re.DOTALL)
	if not m:
		return None, text[m.end():] if m else text
	body = text[m.end():]
	lines = m.group(1).splitlines()
	fields = []
	current_key = None
	current_value_lines = []
	for line in lines:
		if re.match(r'^[\w-]+:', line):
			if current_key is not None:
				fields.append((current_key, '\n'.join(current_value_lines)))
			current_key, rest = line.split(':', 1)
			current_value_lines = [rest]
		else:
			current_value_lines.append(line)
	if current_key is not None:
		fields.append((current_key, '\n'.join(current_value_lines)))
	return fields, body


def build_frontmatter(fields):
	lines = []
	for key, value in fields:
		vlines = value.splitlines()
		if vlines and vlines[0].strip() == '':
			vlines = vlines[1:]
		if len(vlines) == 1 and not vlines[0].strip().startswith('- '):
			lines.append(key + ':' + vlines[0])
		else:
			lines.append(key + ':')
			lines.extend(vlines)
	return '---\r\n' + '\r\n'.join(lines) + '\r\n---'


# Collect conditions/payoffs from current card notes
conditions = set()
payoffs = set()
card_data = []
for fname in sorted(os.listdir(folder)):
	if not fname.endswith('.md'):
		continue
	fpath = os.path.join(folder, fname)
	with open(fpath, 'r', encoding='utf-8') as f:
		text = f.read()
	fields, body = parse_frontmatter(text)
	if fields is None:
		continue
	field_dict = {k: v for k, v in fields}
	conds = []
	pays = []
	for key in ('conditions', 'payoffs'):
		if key not in field_dict:
			continue
		for line in field_dict[key].splitlines():
			line = line.strip()
			if line.startswith('- '):
				val = line[2:].strip().strip('"')
				if key == 'conditions':
					conds.append(val)
				else:
					pays.append(val)
	conditions.update(conds)
	payoffs.update(pays)
	card_data.append({'file': fname, 'fields': fields, 'body': body, 'conds': conds, 'pays': pays})

# Parse mapping doc
with open(map_path, 'r', encoding='utf-8') as f:
	doc_lines = f.read().splitlines()

# condition -> set of payoff labels
mapping = {}
for line in doc_lines:
	line = line.strip()
	if not line or line.startswith('| condition') or line.startswith('|---'):
		continue
	parts = line.split('|')
	if len(parts) >= 3:
		cond = normalize(parts[1])
		if not cond or cond == 'condition' or set(cond) == {'-'}:
			continue
		if cond == '萦绕':
			cond = '当卡片在墓地中'
		if cond not in conditions:
			continue
		for p_raw in parts[2].split('；'):
			p = normalize(p_raw)
			if not p or p == '自然发生':
				continue
			for p_our in doc_pay_to_ours(p, payoffs):
				if p_our in payoffs:
					mapping.setdefault(cond, set()).add(p_our)

# Reverse mapping: payoff -> conditions
reverse = {p: set() for p in payoffs}
for cond, ps in mapping.items():
	for p in ps:
		if p in reverse:
			reverse[p].add(cond)

os.makedirs(cond_dir, exist_ok=True)
os.makedirs(payoff_dir, exist_ok=True)


def safe_filename(s):
	# Windows forbidden: < > : " / \ | ? *
	for ch in '<>:"/\\|?*':
		s = s.replace(ch, '-')
	return s


# Create condition notes
for cond in sorted(conditions):
	fn = safe_filename(cond) + '.md'
	path = os.path.join(cond_dir, fn)
	lines = []
	lines.append('# ' + cond)
	lines.append('')
	lines.append('## 需要该条件的卡片')
	lines.append('')
	lines.append('```dataview')
	lines.append('TABLE displayName AS 卡片, rarity AS 稀有度')
	lines.append('FROM "卡片库"')
	lines.append('WHERE any(map(conditions, (c) => contains(c, "[[" + this.file.path + "|" + this.file.name + "]]"))')
	lines.append('```')
	lines.append('')
	lines.append('## 能满足该条件的收益')
	lines.append('')
	ps = sorted(mapping.get(cond, []))
	if ps:
		for p in ps:
			lines.append('- [[卡片关系/收益/' + safe_filename(p) + '|' + p + ']]')
	else:
		lines.append('（暂无）')
	lines.append('')
	with open(path, 'w', encoding='utf-8', newline='\r\n') as f:
		f.write('\r\n'.join(lines))
		f.write('\r\n')

# Create payoff notes
for p in sorted(payoffs):
	fn = safe_filename(p) + '.md'
	path = os.path.join(payoff_dir, fn)
	lines = []
	lines.append('# ' + p)
	lines.append('')
	lines.append('## 拥有该收益的卡片')
	lines.append('')
	lines.append('```dataview')
	lines.append('TABLE displayName AS 卡片, rarity AS 稀有度')
	lines.append('FROM "卡片库"')
	lines.append('WHERE any(map(payoffs, (p) => contains(p, "[[" + this.file.path + "|" + this.file.name + "]]"))')
	lines.append('```')
	lines.append('')
	lines.append('## 该收益可满足的条件')
	lines.append('')
	cs = sorted(reverse.get(p, []))
	if cs:
		for c in cs:
			lines.append('- [[卡片关系/条件/' + safe_filename(c) + '|' + c + ']]')
	else:
		lines.append('（暂无）')
	lines.append('')
	with open(path, 'w', encoding='utf-8', newline='\r\n') as f:
		f.write('\r\n'.join(lines))
		f.write('\r\n')

# Update card notes: wrap conditions/payoffs in [[...]]
changed = 0
for card in card_data:
	fields = card['fields']
	new_fields = []
	for key, value in fields:
		if key in ('conditions', 'payoffs'):
			vlines = value.splitlines()
			if vlines and vlines[0].strip() == '':
				vlines = vlines[1:]
			new_vlines = []
			for line in vlines:
				stripped = line.strip()
				if stripped.startswith('- '):
					val = stripped[2:].strip().strip('"')
					if key == 'conditions':
						link = '卡片关系/条件/' + safe_filename(val)
					else:
						link = '卡片关系/收益/' + safe_filename(val)
					new_vlines.append('  - "[[' + link + '|' + val + ']]"')
				else:
					new_vlines.append(line)
			new_fields.append((key, '\n'.join(new_vlines)))
		else:
			new_fields.append((key, value))
	new_fm = build_frontmatter(new_fields)
	body = card['body']
	if body and not body.startswith('\r\n'):
		body = '\r\n' + body
	new_text = new_fm + body
	fpath = os.path.join(folder, card['file'])
	with open(fpath, 'wb') as f:
		f.write(new_text.encode('utf-8'))
	changed += 1

print('conditions', len(conditions))
print('payoffs', len(payoffs))
print('updated cards', changed)
