import os, re

folder = 'C:/Users/damen/Documents/Obsidian Vault/OneDeck/卡片库'


def parse_frontmatter(text):
	m = re.search(r'^---\s*\r?\n(.*?)\r?\n---', text, re.DOTALL)
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
		if vlines and vlines[0].strip() == '':
			vlines = vlines[1:]
		if len(vlines) == 1 and not vlines[0].strip().startswith('- '):
			lines.append(key + ':' + vlines[0])
		else:
			lines.append(key + ':')
			lines.extend(vlines)
	return '---\r\n' + '\r\n'.join(lines) + '\r\n---'


changed = 0
for fname in sorted(os.listdir(folder)):
	if not fname.endswith('.md'):
		continue
	fpath = os.path.join(folder, fname)
	with open(fpath, 'r', encoding='utf-8') as f:
		text = f.read()
	fields, body, _ = parse_frontmatter(text)
	if fields is None:
		continue
	if 'methods' not in [k for k, _ in fields]:
		continue
	new_fields = [(k, v) for k, v in fields if k != 'methods']
	new_fm = build_frontmatter(new_fields)
	if body and not body.startswith('\r\n'):
		body = '\r\n' + body
	new_text = new_fm + body
	with open(fpath, 'wb') as f:
		f.write(new_text.encode('utf-8'))
	changed += 1

print('removed methods from', changed, 'files')
