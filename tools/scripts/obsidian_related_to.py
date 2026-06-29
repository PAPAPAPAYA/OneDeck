#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Unify related-to frontmatter for condition/payoff docs and rename rift notes."""

import re
from pathlib import Path

VAULT_ROOT = Path("C:/Users/damen/Documents/Obsidian Vault/OneDeck")
CARD_LIB_DIR = VAULT_ROOT / "卡片库"
CONDITIONS_DIR = CARD_LIB_DIR / "卡片关系/条件"
BENEFITS_DIR = CARD_LIB_DIR / "卡片关系/收益"

SCOPE_DIRS = [CONDITIONS_DIR, BENEFITS_DIR]

RENAME_MAP = {
	"生成N[次元裂缝]": "生成次元裂缝N",
	"去除N[次元裂缝]": "去除次元裂缝N",
}

WIKILINK_RE = re.compile(r"\[\[((?:[^\]]|\](?!\]))+?)(?:\|((?:[^\]]|\](?!\]))+?))?\]\]")
FRONTMATTER_RE = re.compile(r"^---\s*\n(.*?)\n---\s*\n?(.*)$", re.DOTALL)


def normalize_line_endings(text, original_text):
	"""Preserve the dominant line ending of the original text."""
	crlf = original_text.count("\r\n")
	lf = original_text.count("\n") - crlf
	if crlf >= lf:
		return text.replace("\n", "\r\n").replace("\r\r", "\r")
	return text.replace("\r\n", "\n")


def read_file(path):
	with open(path, "r", encoding="utf-8", newline="") as f:
		return f.read()


def write_file(path, text, original_text):
	text = normalize_line_endings(text, original_text)
	with open(path, "w", encoding="utf-8", newline="") as f:
		f.write(text)


def split_frontmatter(content):
	m = FRONTMATTER_RE.match(content)
	if not m:
		return {}, content
	fm_text = m.group(1)
	body = m.group(2)
	fm = parse_simple_yaml(fm_text)
	return fm, body


def parse_simple_yaml(text):
	"""Very small YAML parser sufficient for this vault's frontmatter."""
	result = {}
	current_key = None
	current_list = None
	for raw_line in text.splitlines():
		line = raw_line.rstrip()
		if not line.strip():
			continue
		if line.lstrip().startswith("- "):
			if current_list is not None:
				current_list.append(parse_yaml_value(line.lstrip()[2:].strip()))
			continue
		colon_pos = line.find(":")
		if colon_pos == -1:
			continue
		key = line[:colon_pos].strip()
		rest = line[colon_pos + 1:].strip()
		if rest == "":
			current_key = key
			current_list = []
			result[key] = current_list
		else:
			current_key = key
			current_list = None
			result[key] = parse_yaml_value(rest)
	return result


def parse_yaml_value(value):
	value = value.strip()
	if (value.startswith('"') and value.endswith('"')) or (value.startswith("'") and value.endswith("'")):
		return value[1:-1]
	if value.lower() == "true":
		return True
	if value.lower() == "false":
		return False
	if value.lower() in ("null", "~"):
		return None
	try:
		return int(value)
	except ValueError:
		pass
	try:
		return float(value)
	except ValueError:
		pass
	return value


def apply_rename(target):
	return RENAME_MAP.get(target, target)


def collect_all_stems():
	stems = set()
	for d in SCOPE_DIRS:
		for f in d.glob("*.md"):
			stems.add(f.stem)
	for f in VAULT_ROOT.rglob("*.md"):
		stems.add(f.stem)
	return stems


def extract_wikilinks_robust(text, known_stems):
	"""Extract wiki-link targets, handling file names that end with ']'."""
	links = []
	i = 0
	while i < len(text):
		start = text.find("[[", i)
		if start == -1:
			break
		end = text.find("]]", start + 2)
		if end == -1:
			break
		inner = text[start + 2:end]
		if "|" in inner:
			target_part, display_part = inner.split("|", 1)
		else:
			target_part = inner
			display_part = ""

		actual_target = target_part
		actual_end = end
		if end + 1 < len(text) and text[end + 1] == "]":
			extended_target = target_part + "]"
			if extended_target in known_stems and target_part not in known_stems:
				actual_target = extended_target
				actual_end = end + 1

		links.append((actual_target.strip(), display_part.strip() if display_part else None))
		i = actual_end + 2
	return links


def file_exists_for_link(target):
	candidates = [
		CONDITIONS_DIR / (target + ".md"),
		BENEFITS_DIR / (target + ".md"),
		CARD_LIB_DIR / (target + ".md"),
		VAULT_ROOT / (target + ".md"),
	]
	return any(p.exists() for p in candidates)


def is_in_scope(target):
	return (CONDITIONS_DIR / (target + ".md")).exists() or (BENEFITS_DIR / (target + ".md")).exists()


def collect_scope_files():
	files = []
	for d in SCOPE_DIRS:
		for f in sorted(d.glob("*.md")):
			files.append(f)
	return files


def get_existing_related_to(fm, known_stems):
	rt = fm.get("related to")
	if rt is None:
		return []
	if isinstance(rt, str):
		rt = [rt]
	targets = []
	for item in rt:
		s = item.strip()
		links = extract_wikilinks_robust(s, known_stems)
		for target, _ in links:
			targets.append(apply_rename(target))
		if not links:
			targets.append(apply_rename(s))
	return targets


def extract_body_links(body, known_stems):
	links = []
	for target, _ in extract_wikilinks_robust(body, known_stems):
		links.append(apply_rename(target))
	return links


def build_graph(scope_files):
	known_stems = collect_all_stems()
	graph = {}
	for f in scope_files:
		content = read_file(f)
		fm, body = split_frontmatter(content)
		existing = get_existing_related_to(fm, known_stems)
		body_links = extract_body_links(body, known_stems)
		combined = set(existing) | set(body_links)
		combined.discard(f.stem)
		graph[f.stem] = combined
	return graph


def add_bidirectional_edges(graph):
	for name in list(graph.keys()):
		for target in list(graph[name]):
			if target in graph:
				graph[target].add(name)


def compute_final_related(graph):
	final = {}
	skipped = {}
	for name in sorted(graph.keys()):
		related = set(graph[name])
		related.discard(name)
		valid = set()
		for r in sorted(related):
			if r in graph:
				valid.add(r)
			elif is_in_scope(r):
				# Should not happen if graph is complete, but guard anyway.
				valid.add(r)
			else:
				skipped.setdefault(name, []).append(r)
		final[name] = sorted(valid)
	return final, skipped


def rebuild_frontmatter(content, related_list):
	fm, body = split_frontmatter(content)
	fm.pop("related to", None)

	related_block = "related to:\n" + "".join(f'  - "[[{r}]]"\n' for r in related_list)

	if not fm:
		return f"---\n{related_block}---\n{body}"

	other_lines = []
	for key, value in fm.items():
		if isinstance(value, list):
			other_lines.append(f"{key}:")
			for item in value:
				other_lines.append(f'  - "{item}"')
		elif isinstance(value, bool):
			other_lines.append(f"{key}: {'true' if value else 'false'}")
		elif value is None:
			other_lines.append(f"{key}:")
		elif isinstance(value, (int, float)):
			other_lines.append(f"{key}: {value}")
		else:
			other_lines.append(f'{key}: "{value}"')

	other_block = "\n".join(other_lines)
	return f"---\n{other_block}\n{related_block}---\n{body}"


def update_related_to_properties(scope_files, final_related):
	for f in scope_files:
		content = read_file(f)
		related_list = final_related.get(f.stem, [])
		new_content = rebuild_frontmatter(content, related_list)
		write_file(f, new_content, content)


def rename_rift_notes():
	renames = []
	# Scope folders
	old_benefit = BENEFITS_DIR / "生成N[次元裂缝].md"
	new_benefit = BENEFITS_DIR / "生成次元裂缝N.md"
	old_condition = CONDITIONS_DIR / "去除N[次元裂缝].md"
	new_condition = CONDITIONS_DIR / "去除次元裂缝N.md"

	if old_benefit.exists() and not new_benefit.exists():
		old_benefit.rename(new_benefit)
		renames.append((str(old_benefit), str(new_benefit)))
	if old_condition.exists() and not new_condition.exists():
		old_condition.rename(new_condition)
		renames.append((str(old_condition), str(new_condition)))

	return renames


def apply_rename_plain_text(content):
	"""Plain-text replacement for the old rift note names.

	This catches bare mentions, display aliases, and wiki-links that the
	regex cannot parse because the target ends with ']'.
	"""
	for old, new in RENAME_MAP.items():
		# Wiki-link target
		content = content.replace(f"[[{old}]]", f"[[{new}]]")
		# Wiki-link with alias
		content = content.replace(f"[[{old}|", f"[[{new}|")
		# Display alias end
		content = content.replace(f"|{old}]]", f"|{new}]]")
		# Bare mentions anywhere else
		content = content.replace(old, new)
	return content


def apply_rename_to_all_wikilinks():
	"""Apply RENAME_MAP to every wiki-link target across the vault."""
	for md_file in VAULT_ROOT.rglob("*.md"):
		content = read_file(md_file)
		original = content
		changed = False

		# First pass: plain text for bracketed names.
		new_content = apply_rename_plain_text(content)
		if new_content != content:
			changed = True
			content = new_content

		# Second pass: regex for regular wiki-links.
		def replace_link(match):
			nonlocal changed
			target = match.group(1)
			display = match.group(2)
			new_target = apply_rename(target)
			if new_target != target:
				changed = True
				if display is None or display == target:
					return f"[[{new_target}]]"
				new_display = apply_rename(display) if display else None
				if new_display and new_display != display:
					return f"[[{new_target}|{new_display}]]"
				return f"[[{new_target}|{display}]]"
			return match.group(0)

		new_content = WIKILINK_RE.sub(replace_link, content)
		if new_content != content:
			changed = True
			content = new_content

		if changed:
			write_file(md_file, content, original)


def update_titles_for_renames(renames):
	title_re = re.compile(r"^(#\s+)(.+)$", re.MULTILINE)
	for old, new in renames:
		old_stem = Path(old).stem
		new_stem = Path(new).stem
		f = Path(new)
		content = read_file(f)
		original = content

		def replace_title(match):
			prefix = match.group(1)
			title = match.group(2)
			if title == old_stem:
				return prefix + new_stem
			return match.group(0)

		new_content = title_re.sub(replace_title, content)
		if new_content != content:
			write_file(f, new_content, original)


def main():
	# Step 1: normalize wiki-links across the vault (handles bracketed old names).
	apply_rename_to_all_wikilinks()

	# Step 2: rename files so stems and links align.
	renames = rename_rift_notes()

	# Step 3: update H1 titles for renamed files.
	update_titles_for_renames(renames)

	# Step 4: collect scope files (now with new names) and build graph.
	scope_files = collect_scope_files()
	print(f"Scope files: {len(scope_files)}")

	graph = build_graph(scope_files)
	add_bidirectional_edges(graph)
	final_related, skipped = compute_final_related(graph)

	# Step 4: write updated frontmatter.
	update_related_to_properties(scope_files, final_related)

	print("\n=== Renamed files ===")
	for old, new in renames:
		print(f"  {old} -> {new}")

	print("\n=== related to updates ===")
	for f in sorted(scope_files, key=lambda x: x.stem):
		rt = final_related.get(f.stem, [])
		print(f"  {f.stem}: {len(rt)} links")

	print("\n=== Skipped non-existent / out-of-scope links ===")
	if skipped:
		for name, links in sorted(skipped.items()):
			for link in links:
				print(f"  {name} -> {link}")
	else:
		print("  None")


if __name__ == "__main__":
	main()
