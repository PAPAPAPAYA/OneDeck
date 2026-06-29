import os, re

folder = 'C:/Users/damen/Documents/Obsidian Vault/OneDeck/卡片库'
map_path = 'C:/Users/damen/Documents/Obsidian Vault/OneDeck/卡片关系/methods收益条件mapping.md'
out_dir = 'C:/Users/damen/Documents/Obsidian Vault/OneDeck/卡片关系'


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


# Parse cards
cards = []
all_entries = []
for fname in sorted(os.listdir(folder)):
	if not fname.endswith('.md'):
		continue
	fpath = os.path.join(folder, fname)
	with open(fpath, 'r', encoding='utf-8') as f:
		text = f.read()
	m = re.search(r'^---\s*\n(.*?)\n---', text, re.DOTALL)
	if not m:
		continue
	fm = m.group(1)
	mm = re.search(r'^methods:\s*\n((?:  - .+\n)*)', fm, re.MULTILINE)
	methods = []
	if mm:
		methods = [line.strip()[2:] for line in mm.group(1).splitlines() if line.strip().startswith('- ')]
	ct = re.search(r'^conditionTags:\s*\n((?:  - .+\n)*)', fm, re.MULTILINE)
	cond_tags = []
	if ct:
		cond_tags = [line.strip()[2:] for line in ct.group(1).splitlines() if line.strip().startswith('- ')]
	rarity = ''
	display = ''
	rm = re.search(r'^rarity:\s*(.+)$', fm, re.MULTILINE)
	if rm:
		rarity = rm.group(1).strip()
	dm = re.search(r'^displayName:\s*(.+)$', fm, re.MULTILINE)
	if dm:
		display = dm.group(1).strip()
	entries = expand_methods(methods)
	if '萦绕' in cond_tags:
		entries.append(('condition', '当卡片在墓地中', '[tag]萦绕'))
	cards.append({'file': fname, 'display': display, 'rarity': rarity, 'entries': entries, 'cond_tags': cond_tags})
	all_entries.extend(entries)

conditions = {norm for kind, norm, _ in all_entries if kind == 'condition'}
payoffs = {norm for kind, norm, _ in all_entries if kind == 'payoff'}


# Build mapping from methods收益条件mapping.md (authoritative)
def normalize_doc(s):
	return s.replace('{N}', 'N').replace('{M}', 'M').replace('{X}', 'X').replace('<counter>', '').strip()


source_to_payoff = {}
for src, (cond, pay) in split_rules.items():
	source_to_payoff.setdefault(normalize_doc(src), []).append(normalize_doc(pay))


def doc_pay_to_ours(p):
	if p in payoffs:
		return [p]
	if p in source_to_payoff:
		return source_to_payoff[p]
	return []


with open(map_path, 'r', encoding='utf-8') as f:
	doc_lines = f.read().splitlines()

doc_map = {}
for line in doc_lines:
	line = line.strip()
	if not line or line.startswith('| condition') or line.startswith('|---'):
		continue
	parts = line.split('|')
	if len(parts) >= 3:
		cond = normalize_doc(parts[1])
		if not cond or cond == 'condition' or set(cond) == {'-'}:
			continue
		if cond == '萦绕':
			cond = '当卡片在墓地中'
		if cond not in conditions:
			continue
		for p_raw in parts[2].split('；'):
			p = normalize_doc(p_raw)
			if not p or p == '自然发生':
				continue
			for p_our in doc_pay_to_ours(p):
				if p_our in payoffs:
					doc_map.setdefault(cond, set()).add(p_our)

our = {p: set() for p in payoffs}
for cond, ps in doc_map.items():
	for p in ps:
		our[p].add(cond)

# Generate Methods classification report
unique_src = {}
for kind, norm, src in all_entries:
	unique_src.setdefault(src, set()).add((kind, norm))
method_rows = []
for src in sorted(unique_src):
	for kind, norm in sorted(unique_src[src]):
		marker = ''
		if src in split_rules:
			marker = ' (拆分)'
		if src == '[tag]萦绕':
			marker = ' (来自tag)'
		method_rows.append((src, kind, norm, marker))

lines = []
lines.append('# Methods 分类与收益→条件映射确认')
lines.append('')
lines.append('> 已按你的确认更新：消耗力量/置顶力量最多敌方/埋葬后N卡均视为 condition；萦绕映射为「当卡片在墓地中」。')
lines.append('')
lines.append('## 1. Method / Tag 分类表')
lines.append('')
lines.append('| 原始 method / tag | 分类 | 归一化标签 |')
lines.append('| --- | --- | --- |')
for src, kind, norm, marker in method_rows:
	lines.append('| `' + src + '`' + marker + ' | ' + kind + ' | ' + norm + ' |')
lines.append('')
lines.append('## 2. 收益 → 条件 映射建议')
lines.append('')
lines.append('| 收益 (payoff) | 能满足的条件 |')
lines.append('| --- | --- |')
for p in sorted(payoffs):
	cand = sorted(our[p])
	lines.append('| ' + p + ' | ' + ('、'.join(cand) if cand else '（暂无）') + ' |')
lines.append('')
lines.append('## 3. 每张卡片的 Conditions / Payoffs 预览')
lines.append('')
for card in cards:
	conds = [norm for kind, norm, _ in card['entries'] if kind == 'condition']
	pays = [norm for kind, norm, _ in card['entries'] if kind == 'payoff']
	lines.append('### ' + card['display'] + ' (`' + card['file'].replace('.md', '') + '`) — ' + card['rarity'])
	lines.append('- **conditions**: ' + (', '.join(conds) if conds else '（无条件）'))
	lines.append('- **payoffs**: ' + (', '.join(pays) if pays else '（无收益）'))
	lines.append('')

report_path = os.path.join(out_dir, 'Methods分类确认.md')
with open(report_path, 'w', encoding='utf-8', newline='\r\n') as f:
	f.write('\r\n'.join(lines))
	f.write('\r\n')

# Generate diff with doc
def normalize_doc(s):
	return s.replace('{N}', 'N').replace('{M}', 'M').replace('{X}', 'X').replace('<counter>', '').strip()


with open(map_path, 'r', encoding='utf-8') as f:
	doc_lines = f.read().splitlines()
doc = {}
for line in doc_lines:
	line = line.strip()
	if not line or line.startswith('| condition') or line.startswith('|---'):
		continue
	parts = line.split('|')
	if len(parts) >= 3:
		cond = normalize_doc(parts[1])
		if not cond or cond == 'condition' or set(cond) == {'-'}:
			continue
		pays = [normalize_doc(p) for p in parts[2].split('；') if normalize_doc(p) and normalize_doc(p) != '自然发生']
		doc.setdefault(cond, []).extend(pays)

source_to_payoff = {}
for src, (cond, pay) in split_rules.items():
	source_to_payoff.setdefault(normalize_doc(src), []).append(normalize_doc(pay))


def doc_pay_to_ours(p):
	if p in payoffs:
		return [p]
	if p in source_to_payoff:
		return source_to_payoff[p]
	return []


omissions = []
extras = []
for cond, doc_pays in doc.items():
	if cond not in conditions:
		continue
	doc_ours = set()
	for p in doc_pays:
		doc_ours.update(doc_pay_to_ours(p))
	our_pays = {p for p, cands in our.items() if cond in cands}
	for p in sorted(doc_ours - our_pays):
		omissions.append((cond, p))
	for p in sorted(our_pays - doc_ours):
		extras.append((cond, p))

unknown_conds = sorted(set(doc) - conditions)
our_only_conds = sorted(conditions - set(doc))

diff = []
diff.append('# Mapping 差异检查（更新后）')
diff.append('')
diff.append('> 已按你的确认调整分类与映射。')
diff.append('')
diff.append('## 1. 文档有、当前映射缺失的关系')
diff.append('')
if omissions:
	diff.append('| condition | 缺失的 payoff |')
	diff.append('| --- | --- |')
	for cond, p in sorted(omissions):
		diff.append('| ' + cond + ' | ' + p + ' |')
else:
	diff.append('无')
diff.append('')
diff.append('## 2. 当前映射有、文档未列出的关系')
diff.append('')
if extras:
	diff.append('| condition | 多出的 payoff |')
	diff.append('| --- | --- |')
	for cond, p in sorted(extras):
		diff.append('| ' + cond + ' | ' + p + ' |')
else:
	diff.append('无')
diff.append('')
diff.append('## 3. 文档当作 condition、当前未识别的项')
diff.append('')
if unknown_conds:
	for c in unknown_conds:
		diff.append('- ' + c)
else:
	diff.append('无')
diff.append('')
diff.append('## 4. 当前是 condition、文档未出现的项')
diff.append('')
if our_only_conds:
	for c in our_only_conds:
		diff.append('- ' + c)
else:
	diff.append('无')
diff.append('')

diff_path = os.path.join(out_dir, 'Mapping差异检查.md')
with open(diff_path, 'w', encoding='utf-8', newline='\r\n') as f:
	f.write('\r\n'.join(diff))
	f.write('\r\n')

print('conds', len(conditions), 'pays', len(payoffs))
print('omissions', len(omissions), 'extras', len(extras), 'unknown', len(unknown_conds), 'our_only', len(our_only_conds))
