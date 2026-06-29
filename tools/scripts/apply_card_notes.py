import os, re, shutil

folder = 'C:/Users/damen/Documents/Obsidian Vault/OneDeck/卡片库'
backup_folder = 'C:/Users/damen/Documents/Obsidian Vault/OneDeck/卡片库_backup_20260624'


def normalize(s):
	return s.replace('{N}', 'N').replace('{M}', 'M').replace('{X}', 'X').replace('<counter>', '').strip()


split_rules = {
	'造成{N}伤害x本回合被埋葬的敌方数量': ('本回合每有一敌方被埋葬', '造成{N}伤害'),
	'造成友方数量的伤害': ('每有一友方', '造成{N}伤害'),
	'造成所有卡的力量数量的伤害': ('所有卡上每有一力量', '造成{N}伤害'),
	'造成{N}伤害x{M}': ('重复M次', '造成{N}伤害'),
	'转移所有友方的{N}力量到自身': ('消耗所有友方N力量', '给予自身N力量'),
}

explicit_conditions = {'消耗{N}力量', '消耗敌方[诅咒]{N}力量', '置顶力量最多的敌方'}


def base_classify(s):
	s = s.strip()
	if s in explicit_conditions:
		return 'condition'
	if s in ('友方被埋葬', '被埋葬', '被置顶', '被去除'):
		return 'condition'
	if s == '洗牌后':
		return 'condition'
	if s.startswith('当'):
		return 'condition'
	if s.endswith('时'):
		return 'condition'
	if '每' in s:
		return 'condition'
	if s.startswith('再有'):
		return 'condition'
	if s.startswith('埋葬后'):
		return 'condition'
	return 'payoff'


def expand_methods(methods):
	out = []
	for me in methods:
		if me in split_rules:
			cond, pay = split_rules[me]
			out.append(('condition', normalize(cond), me))
			out.append(('payoff', normalize(pay), me))
		else:
			kind = base_classify(me)
			out.append((kind, normalize(me), me))
	return out


def parse_frontmatter(text):
	m = re.search(r'^---\s*\n(.*?)\n---', text, re.DOTALL)
	if not m:
		return None, None, text
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
	return fields, body, text


def build_frontmatter(fields):
	lines = []
	for key, value in fields:
		vlines = value.splitlines()
		# remove leading empty line created when parsing list fields
		if vlines and vlines[0].strip() == '':
			vlines = vlines[1:]
		if len(vlines) == 1 and not vlines[0].strip().startswith('- '):
			lines.append(key + ':' + vlines[0])
		else:
			lines.append(key + ':')
			lines.extend(vlines)
	return '---\r\n' + '\r\n'.join(lines) + '\r\n---'


# Backup
if os.path.exists(backup_folder):
	shutil.rmtree(backup_folder)
shutil.copytree(folder, backup_folder)

changed = 0
for fname in sorted(os.listdir(folder)):
	if not fname.endswith('.md'):
		continue
	fpath = os.path.join(folder, fname)
	with open(fpath, 'r', encoding='utf-8') as f:
		text = f.read()
	fields, body, full = parse_frontmatter(text)
	if fields is None:
		continue
	field_dict = {k: v for k, v in fields}
	fm_text = full[:full.find('---', 3) + 3]
	# Extract methods
	methods = []
	if 'methods' in field_dict:
		mm = field_dict['methods']
		for line in mm.splitlines():
			line = line.strip()
			if line.startswith('- '):
				methods.append(line[2:])
	# conditionTags
	cond_tags = []
	if 'conditionTags' in field_dict:
		ct = field_dict['conditionTags']
		for line in ct.splitlines():
			line = line.strip()
			if line.startswith('- '):
				cond_tags.append(line[2:])
	entries = expand_methods(methods)
	if '萦绕' in cond_tags:
		entries.append(('condition', '当卡片在墓地中', '[tag]萦绕'))
	conds = sorted({norm for kind, norm, _ in entries if kind == 'condition'})
	pays = sorted({norm for kind, norm, _ in entries if kind == 'payoff'})

	# Rebuild fields: remove conditionTags/benefitTags, keep others, add conditions/payoffs after methods
	new_fields = []
	for key, value in fields:
		if key in ('conditionTags', 'benefitTags', 'conditions', 'payoffs'):
			continue
		new_fields.append((key, value))
		if key == 'methods':
			new_fields.append(('conditions', '\n'.join(['  - ' + c for c in conds])))
			new_fields.append(('payoffs', '\n'.join(['  - ' + p for p in pays])))
	# If methods field didn't exist, append at end
	if 'methods' not in field_dict:
		new_fields.append(('conditions', '\n'.join(['  - ' + c for c in conds])))
		new_fields.append(('payoffs', '\n'.join(['  - ' + p for p in pays])))

	new_fm = build_frontmatter(new_fields)
	# Preserve body line endings: ensure body starts with \r\n if non-empty
	if body and not body.startswith('\r\n'):
		body = '\r\n' + body
	new_text = new_fm + body
	with open(fpath, 'wb') as f:
		f.write(new_text.encode('utf-8'))
	changed += 1

print('backed up to', backup_folder)
print('changed files', changed)
