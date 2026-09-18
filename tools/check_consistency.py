# -*- coding: utf-8 -*-
"""Three-way card consistency checker: engine prefabs / Notion 4.0 DB / server card_catalog.

Data flow (inputs are produced by the paired scripts, all kept under tools/outputs/):
	unity_cards_40_current.json   <- python extract_unity_cards_40.py   (engine side)
	notion_40db_rows.json         <- node notion_query_40db.js          (Notion side)
	catalog_current.tsv (+versions) <- python dump_catalog.py [--prod]  (admin side)

Checks: ID coverage per side, name/rarity/tags/ATK/desc/extraAttackTimes drift,
catalog cost vs rarity price baseline, catalog staleness vs prefab git history,
game_version health. Writes an HTML report and exits non-zero on error-level drift.

Usage:
	python tools/check_consistency.py [--refresh [--prod]] [--report PATH]
"""
import datetime
import io
import json
import os
import re
import subprocess
import sys

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

OUT = os.path.join("tools", "outputs")
UNITY_JSON = os.path.join(OUT, "unity_cards_40_current.json")
NOTION_JSON = os.path.join(OUT, "notion_40db_rows.json")
CATALOG_TSV = os.path.join(OUT, "catalog_current.tsv")
CATALOG_VER_TSV = os.path.join(OUT, "catalog_versions.tsv")
TAGNAMES_DIR = os.path.join("Assets", "SORefs", "Strings", "TagNames")
PRICE_DIR = os.path.join("Assets", "SORefs", "ShopRefs", "Money")
PREFAB_ROOT = "Assets/Prefabs/Cards/4.0"
# Deck-slot meter cards price dynamically via DeckSizeIncreaseEffect — skip baseline.
DECK_SLOT_METER_PREFIX = ("UTILITY_SLOT_", "SYSTEM_INCREASE_DECK_SIZE")

RARITY_CANON = {"normal": "common", "common": "common", "uncommon": "uncommon", "rare": "rare"}
# Notion 生物 column predicate (2026-09-14 taxonomy): Creature=实体, others=现象.
NOTION_CREATURE_VALUE = {"Creature": "生物", "None": "非生物", "Token": "非生物"}


def dec(s):
	r"""Decode a Unity YAML escaped string literal (\uNNNN, \xNN, folded lines)."""
	s = re.sub(r"\n\s*", "", s or "")
	if s.startswith('"') and s.endswith('"'):
		s = s[1:-1]
	s = re.sub(r"\\u([0-9A-Fa-f]{4})", lambda m: chr(int(m.group(1), 16)), s)
	s = re.sub(r"\\x([0-9A-Fa-f]{2})", lambda m: chr(int(m.group(1), 16)), s)
	return s


def load_tagmap():
	"""EN enum name -> Chinese display name, from the TagName StringSO assets."""
	mapping = {}
	for f in sorted(os.listdir(TAGNAMES_DIR)):
		if not f.startswith("TagName_") or not f.endswith(".asset"):
			continue
		en = f[len("TagName_"):-len(".asset")]
		text = open(os.path.join(TAGNAMES_DIR, f), encoding="utf-8").read()
		m = re.search(r"value: (.*)", text)
		if m:
			mapping[en] = dec(m.group(1).strip())
	return mapping


def load_price_refs():
	"""Rarity -> base shop price, from the Common/Uncommon/RarePriceRef IntSO assets."""
	prices = {}
	for rarity in ("Common", "Uncommon", "Rare"):
		path = os.path.join(PRICE_DIR, "%sPriceRef.asset" % rarity)
		if not os.path.exists(path):
			continue
		text = open(path, encoding="utf-8").read()
		m = re.search(r"value: (-?\d+)", text)
		if m:
			prices[rarity.lower()] = int(m.group(1))
	return prices


def load_unity():
	cards = json.load(open(UNITY_JSON, encoding="utf-8"))
	by_id, dupes = {}, []
	for c in cards:
		cid = c["cardTypeID"]
		if cid in by_id:
			dupes.append((cid, c["file"], by_id[cid]["file"]))
			continue
		by_id[cid] = c
	return by_id, dupes


def load_notion():
	data = json.load(open(NOTION_JSON, encoding="utf-8"))
	by_id, no_id, backup = {}, [], []
	for r in data.get("results", []):
		cid = (r.get("CARD_TYPE_ID") or "").strip()
		if not cid:
			no_id.append(r.get("中文名") or r.get("id"))
			continue
		if cid in by_id:
			no_id.append(cid + " (duplicate row)")
			continue
		by_id[cid] = r
		if r.get("状态"):
			backup.append(cid)
	return by_id, no_id, backup


def load_catalog():
	rows, versions = {}, []
	for line in open(CATALOG_VER_TSV, encoding="utf-8"):
		parts = line.rstrip("\n").split("\t")
		if len(parts) == 2:
			versions.append((parts[0], int(parts[1])))
	for line in open(CATALOG_TSV, encoding="utf-8"):
		parts = line.rstrip("\n").split("\t")
		if len(parts) >= 7 and parts[0] == "CAT":
			rows[parts[1]] = {
				"name": parts[2], "rarity": parts[3], "cost": parts[4],
				"tags": parts[5], "updated_at": parts[6],
			}
	return rows, versions


def git_prefab_dates():
	"""Prefab path -> date (YYYY-MM-DD) of its last commit under the 4.0 root."""
	proc = subprocess.run(
		["git", "log", "--name-only", "--format=@%ad", "--date=short", "--", PREFAB_ROOT],
		capture_output=True, text=True, encoding="utf-8", errors="replace")
	dates, current = {}, None
	for line in proc.stdout.splitlines():
		line = line.strip()
		if line.startswith("@"):
			current = line[1:]
		elif line and current:
			dates.setdefault(line, current)  # newest-first log: first hit wins
	return dates


def norm_desc(s, tagmap):
	if not s:
		return ""
	s = re.sub(r"</?b>", "", s)
	s = re.sub(r"<tag:(\w+)>", lambda m: "[" + tagmap.get(m.group(1), m.group(1)) + "]", s)
	# prefab desc convention wraps the placeholder in brackets: tag为[<tag:X>]的…
	s = s.replace("[[", "[").replace("]]", "]")
	s = s.replace("×", "x")
	for a, b in (("：", ":"), ("，", ","), ("；", ";"), ("（", "("), ("）", ")")):
		s = s.replace(a, b)
	return re.sub(r"\s+", "", s)


def parse_tags_json(raw):
	try:
		return json.loads(raw) if raw else []
	except ValueError:
		return ["<unparseable: %s>" % raw]


def to_int(v):
	try:
		return int(str(v).strip())
	except (ValueError, TypeError):
		return 0


def main():
	refresh = "--refresh" in sys.argv
	if refresh:
		print("refreshing inputs...")
		subprocess.run([sys.executable, os.path.join(OUT, "extract_unity_cards_40.py")], check=True)
		subprocess.run(["node", os.path.join(OUT, "notion_query_40db.js")], check=True,
			capture_output=True, text=True, encoding="utf-8")
		dump_cmd = [sys.executable, os.path.join(OUT, "dump_catalog.py")]
		if "--prod" in sys.argv:
			dump_cmd.append("--prod")
		subprocess.run(dump_cmd, check=True)

	tagmap = load_tagmap()
	prices = load_price_refs()
	unity, dupes = load_unity()
	notion, notion_no_id, notion_backup = load_notion()
	catalog, versions = load_catalog()
	git_dates = git_prefab_dates()

	issues = []  # {card, sev, field, detail}  sev: ERROR / WARN / INFO

	def add(card, sev, field, detail):
		issues.append({"card": card, "sev": sev, "field": field, "detail": detail})

	# --- Coverage -----------------------------------------------------------------
	unity_ids, notion_ids, catalog_ids = set(unity), set(notion), set(catalog)
	active_notion = {cid for cid in notion_ids if not notion[cid].get("状态")}
	unconfigured = {cid for cid in notion_ids
	                if notion[cid].get("Unity 配置状态") and notion[cid]["Unity 配置状态"] != "已配置"}

	for cid in sorted(unity_ids - notion_ids):
		add(cid, "ERROR", "coverage", "prefab 无 Notion 行")
	for cid in sorted(unity_ids - catalog_ids):
		add(cid, "ERROR", "coverage", "prefab 未上传到服务器 catalog")
	for cid in sorted(catalog_ids - unity_ids):
		add(cid, "ERROR", "coverage", "catalog 僵尸行(无对应 prefab, 疑似 3.0 残留)")
	for cid in sorted(notion_ids - unity_ids):
		if notion[cid].get("状态"):
			continue  # 备用/已删 rows are intentionally un-built
		sev = "INFO" if cid in unconfigured else "WARN"
		add(cid, sev, "coverage", "Notion 行无 prefab(%s%s)" % (
			notion[cid].get("状态") or "启用",
			", " + notion[cid]["Unity 配置状态"] if notion[cid].get("Unity 配置状态") != "已配置" else ""))
	for cid, f1, f2 in dupes:
		add(cid, "WARN", "duplicate", "重复 cardTypeID: %s / %s" % (f1, f2))

	# --- Field drift (engine vs notion vs catalog) --------------------------------
	for cid in sorted(unity_ids):
		u, n, c = unity[cid], notion.get(cid), catalog.get(cid)
		# In-progress Notion rows (需新机制/需小改/可直接配置) downgrade diffs to WARN.
		floor = "WARN" if cid in unconfigured else "ERROR"
		if not n:
			continue
		if u["displayName"] != (n.get("中文名") or ""):
			add(cid, floor, "中文名", "engine=%r notion=%r" % (u["displayName"], n.get("中文名")))
		u_rar = RARITY_CANON.get(u["rarityName"], u["rarityName"])
		n_rar = RARITY_CANON.get((n.get("rarity") or "").lower(), (n.get("rarity") or "").lower())
		if u_rar != n_rar:
			add(cid, floor, "rarity", "engine=%s notion=%s" % (u_rar, n_rar))
		# tags: two per-side conventions, each mirroring that side's real source —
		# Notion tag column tracks myTags only; CardCatalogUploader uploads
		# myTags + reservedTag, so the catalog side must compare against the union.
		reserved = [u["reservedTag"]] if u["reservedTag"] != "None" else []
		u_tags_notion = sorted(tagmap.get(t, t) for t in u["myTags"])
		u_tags_catalog = sorted(tagmap.get(t, t) for t in u["myTags"] + reserved)
		n_tags = sorted(parse_tags_json(n.get("tag") or "[]"))
		unmapped = [t for t in u["myTags"] + reserved if t not in tagmap]
		if unmapped:
			add(cid, "WARN", "tags", "未映射 tag 枚举: %s" % unmapped)
		elif u_tags_notion != n_tags:
			add(cid, floor, "tags", "engine(myTags)=%s notion=%s" % (u_tags_notion, n_tags))
		# ATK: catalog has no ATK column, two-way only.
		if to_int(u["printedAttack"]) != to_int(n.get("ATK")):
			add(cid, floor, "ATK", "engine=%s notion=%s" % (to_int(u["printedAttack"]), to_int(n.get("ATK"))))
		if to_int(u["extraAttackTimes"]) != to_int(n.get("ExtraATKTimes")):
			add(cid, floor, "extraAttackTimes", "engine=%s notion=%s" % (
				to_int(u["extraAttackTimes"]), to_int(n.get("ExtraATKTimes"))))
		# desc: normalized (b-strip, tag placeholder -> Chinese phrase, fullwidth -> half).
		if norm_desc(u["cardDesc"], tagmap) != norm_desc(n.get("card desc"), tagmap):
			add(cid, floor, "desc", "engine=%r notion=%r" % (u["cardDesc"], n.get("card desc")))
		# creature predicate
		if n.get("生物") and NOTION_CREATURE_VALUE.get(u["cardType"]) != n["生物"]:
			add(cid, floor, "实体/现象", "engine=%s(%s) notion=%s" % (u["cardType"], NOTION_CREATURE_VALUE.get(u["cardType"]), n["生物"]))
		# --- engine vs catalog (name/rarity/tags only; no desc/ATK columns server-side)
		if c:
			if u["displayName"] != c["name"]:
				add(cid, "ERROR", "catalog.name", "engine=%r catalog=%r (catalog 疑似过期)" % (u["displayName"], c["name"]))
			c_rar = RARITY_CANON.get(c["rarity"].lower(), c["rarity"].lower())
			if u_rar != c_rar:
				add(cid, "ERROR", "catalog.rarity", "engine=%s catalog=%s" % (u_rar, c_rar))
			c_tags = sorted(tagmap.get(t, t) for t in parse_tags_json(c["tags"]))
			if u_tags_catalog != c_tags:
				add(cid, "ERROR", "catalog.tags", "engine(myTags+reserved)=%s catalog=%s" % (u_tags_catalog, c_tags))
			# cost baseline: rarity price refs; deck-slot meter card is dynamic, skip.
			if not cid.startswith(DECK_SLOT_METER_PREFIX) and u_rar in prices:
				if to_int(c["cost"]) != prices[u_rar]:
					add(cid, "WARN", "catalog.cost", "catalog=%s 基线(%s)=%s" % (c["cost"], u_rar, prices[u_rar]))
			# staleness: prefab committed after the last catalog upload of this row.
			up_at = (c["updated_at"] or "")[:10]
			prefab_key = os.path.join(PREFAB_ROOT, u["folder"], u["file"] + ".prefab").replace("\\", "/")
			commit = git_dates.get(prefab_key)
			if commit and commit > up_at:
				add(cid, "WARN", "catalog.stale", "prefab 改于 %s, catalog 上传于 %s (改卡后未重传)" % (commit, up_at))

	# --- Report -------------------------------------------------------------------
	err = [i for i in issues if i["sev"] == "ERROR"]
	warn = [i for i in issues if i["sev"] == "WARN"]
	info = [i for i in issues if i["sev"] == "INFO"]
	print("unity=%d notion=%d(active %d) catalog=%d | ERROR=%d WARN=%d INFO=%d" % (
		len(unity), len(notion), len(active_notion), len(catalog), len(err), len(warn), len(info)))
	report_path = write_report(issues, unity, notion, catalog, versions, prices, tagmap,
		dupes, notion_no_id, notion_backup)
	print("report: %s" % report_path)
	for sev in ("ERROR", "WARN"):
		for i in issues:
			if i["sev"] == sev:
				print("%s %-18s %-16s %s" % (sev, i["card"], i["field"], i["detail"]))
	sys.exit(1 if err else 0)


def badge(sev):
	color = {"ERROR": "#c0392b", "WARN": "#b8860b", "INFO": "#566"}[sev]
	return '<span class="b" style="background:%s">%s</span>' % (color, sev)


def esc(s):
	return (str(s).replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;"))


def write_report(issues, unity, notion, catalog, versions, prices, tagmap, dupes, notion_no_id, notion_backup):
	by_card = {}
	for i in issues:
		by_card.setdefault(i["card"], []).append(i)
	now = datetime.datetime.now().strftime("%Y-%m-%d %H:%M")
	path = os.path.join(OUT, "consistency_report_%s.html" % datetime.date.today().isoformat())
	h = ['<!DOCTYPE html><html lang="zh"><head><meta charset="utf-8"><title>OneDeck 三侧一致性检查</title><style>',
	     "body{font-family:'Segoe UI',sans-serif;margin:24px;color:#222;max-width:1200px}",
	     "h2{border-bottom:2px solid #888;padding-bottom:4px;margin-top:36px}",
	     "table{border-collapse:collapse;font-size:13px;width:100%}",
	     "th,td{border:1px solid #bbb;padding:4px 8px;text-align:left;vertical-align:top}",
	     "th{background:#eee}.b{color:#fff;border-radius:4px;padding:1px 6px;font-size:11px}",
	     ".muted{color:#777}.num{font-size:26px;font-weight:bold;margin-right:18px}",
	     "code{background:#f4f4f4;padding:0 3px}</style></head><body>",
	     "<h1>OneDeck 三侧一致性检查</h1><p class=muted>generated %s (UTC+8) | unity=%d notion=%d catalog=%d | "
	     "ERROR=%d WARN=%d INFO=%d | 价格基线=%s</p>" % (
		     esc(now), len(unity), len(notion), len(catalog),
		     sum(1 for i in issues if i["sev"] == "ERROR"),
		     sum(1 for i in issues if i["sev"] == "WARN"),
		     sum(1 for i in issues if i["sev"] == "INFO"), esc(prices)),
	     ]
	# Issue table grouped by card.
	h.append("<h2>问题明细（按卡分组）</h2>")
	h.append("<table><tr><th>卡</th><th>级别</th><th>字段</th><th>详情</th></tr>")
	for cid in sorted(by_card):
		rows = by_card[cid]
		h.append("<tr><td rowspan=%d><code>%s</code><br>%s</td>" % (
			len(rows), esc(cid), esc(unity.get(cid, {}).get("displayName") or notion.get(cid, {}).get("中文名") or "")))
		for k, i in enumerate(rows):
			if k:
				h.append("<tr>")
			h.append("<td>%s</td><td>%s</td><td>%s</td></tr>" % (
				badge(i["sev"]), esc(i["field"]), esc(i["detail"])))
	h.append("</table>")
	if not by_card:
		h.append("<p>无问题 ✓</p>")
	# Version health.
	h.append("<h2>catalog game_version 健康</h2><table><tr><th>game_version</th><th>行数</th></tr>")
	for v, n in versions:
		h.append("<tr><td>%s</td><td>%d</td></tr>" % (esc(v), n))
	h.append("</table><p class=muted>admin 取名口径 = 行数最多的版本（server.js loadCatalogMap）。</p>")
	# Notion design-only rows.
	h.append("<h2>Notion 侧附加信息</h2><ul>")
	h.append("<li>备用/已删行（不要求 prefab）: %s</li>" % (esc(", ".join(notion_backup)) or "无"))
	h.append("<li>无 CARD_TYPE_ID 或重复行: %s</li>" % (esc(", ".join(map(str, notion_no_id))) or "无"))
	h.append("<li>重复 cardTypeID prefab: %d 组</li>" % len(dupes))
	h.append("</ul></body></html>")
	with open(path, "w", encoding="utf-8", newline="\r\n") as fp:
		fp.write("\n".join(h))
	return path


if __name__ == "__main__":
	main()
