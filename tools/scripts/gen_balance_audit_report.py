# Regenerate docs/CardBalanceAudit_ZombieBaseline.html from tools/outputs/audit_rows_40.json.
# Scoring is self-contained (2026-09-06: Notion 4.0 审计 DB deprecated; conventions live in
# this file + the report's 估值约定 section). Conventions ratified 2026-09-06:
#   强化三类统一 1.0/层 (给力量友方 0.8 / 给力量自身 1.5 / 强化敌方诅咒 1.2 -> all 1.0)
#   条件层减分: 遗言 x0.3 / 苏醒 x0.5 / 强化反应 x0.5 / 条件 x0.5 (was all x0.8)
#   生成1信徒 +1.0 (believer = revive 1 friendly + self-exile)
#   tag定向复活 = 复活1友方 +2.0 全额
# Patch list below carries the 2026-09-06 card DB value edits (rarity=normal) so the
# report matches the card DB even though audit_rows_40.json is prefab-derived.
# Usage: python tools/scripts/gen_balance_audit_report.py
import json
import os
import re

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ROWS_PATH = os.path.join(ROOT, "tools", "outputs", "audit_rows_40.json")
OUT_PATH = os.path.join(ROOT, "docs", "CardBalanceAudit_ZombieBaseline.html")

# ---------------------------------------------------------------- conventions
BASE_UNIT = {
	"攻击": lambda n: n,
	"埋敌方": lambda n: 2.0 * n,
	"埋友方": lambda n: -1.2 * n,
	"磨牌": lambda n: -0.8 * n,
	"置顶友方": lambda n: 1.5 * n,
	"给力量友方": lambda n: 1.0 * n,
	"给力量自身": lambda n: 1.0 * n,
	"强化敌方诅咒": lambda n: 1.0 * n,
	"复活友方": lambda n: 2.0 * n * (0.8 if (n >= 2 and float(n).is_integer()) else 1.0),
	"复活敌方诅咒": lambda n: 2.0 * n,
	"延迟复活": lambda n: 1.4 * n,
	"放逐友方": lambda n: -2.0 * n,
	"放逐友方信徒": lambda n: -1.0 * n,
	"生成信徒": lambda n: 1.0 * n,
	"攻击次数全体+1": lambda n: 4.0 * n,
	"攻击次数单体+1": lambda n: 1.5 * n,
	"敌方诅咒攻击次数+1": lambda n: 2.0 * n,
	"苏醒转遗言": lambda n: 2.0 * n,
	"墓地生物攻击": lambda n: 2.0 * n,
	"复活最强敌方夺攻": lambda n: 3.0 * n,
	"攻击力翻倍": lambda n: 0.0,
}
LAYER_MULT = {"直发": 1.0, "遗言": 0.3, "苏醒": 0.5, "强化反应": 0.5, "条件": 0.5, "全局C": 1.0}
BANDS = {"normal": (1.6, 2.4), "uncommon": (2.4, 4.0), "rare": (4.0, 8.0)}

EFF_TEXT = {
	"攻击": "攻{n}",
	"埋敌方": "埋{n}敌方",
	"埋友方": "埋{n}友方",
	"磨牌": "磨{n}",
	"置顶友方": "置顶{n}友方",
	"给力量友方": "强化{n}友方",
	"给力量自身": "强化自身{n}",
	"强化敌方诅咒": "强化{n}敌方诅咒",
	"复活友方": "复活{n}友方",
	"复活敌方诅咒": "复活{n}敌方诅咒",
	"延迟复活": "延迟复活{n}友方",
	"放逐友方": "放逐{n}友方",
	"放逐友方信徒": "放逐{n}友方信徒",
	"生成信徒": "生成{n}信徒",
	"攻击次数全体+1": "全体友方生物攻次+{n}",
	"攻击次数单体+1": "1友方生物攻次+{n}",
	"敌方诅咒攻击次数+1": "敌方诅咒攻次+{n}",
	"苏醒转遗言": "苏醒→触发其遗言",
	"墓地生物攻击": "墓地1友方生物攻击",
	"复活最强敌方夺攻": "复活1攻击力最高敌方并夺攻",
	"攻击力翻倍": "攻击力翻倍",
}
LAYER_PREFIX = {"遗言": "遗言:", "苏醒": "苏醒:", "强化反应": "强化反应:", "条件": "条件:", "全局C": "被动(C):"}

# --------------------------------------------------------------- data patches
# 2026-09-06 用户 card DB 改动 (rarity=normal, 已确认完整) + 口径修正。
PATCHES = {
	"AVENGER_4.0": {
		"eff": [{"type": "攻击", "n": 2, "layer": "直发"}, {"type": "给力量自身", "n": 1, "layer": "遗言"}],
		"note": "09-06 DB改动: 攻1→攻击x2(ATK1)",
	},
	"BEAST_REVIVER": {
		"eff": [{"type": "攻击", "n": 1, "layer": "直发"}, {"type": "复活友方", "n": 1, "layer": "直发"}],
		"note": "09-06 DB改动: ATK 2→1",
	},
	"ELITE_REVIVER": {
		"eff": [{"type": "攻击", "n": 1, "layer": "直发"}, {"type": "复活友方", "n": 1, "layer": "直发"}],
		"note": "09-06 DB改动: ATK 2→1; 复活目标=被强化过的, 与折出来的替身成combo",
	},
	"LAST_GIFT": {
		"eff": [{"type": "攻击", "n": 2, "layer": "直发"}, {"type": "给力量友方", "n": 2, "layer": "遗言"}],
		"note": "09-06 DB改动: 遗言强化1→2友方生物",
	},
	"RIFT_ACOLYTE": {
		"eff": [{"type": "攻击", "n": 1, "layer": "直发"}, {"type": "生成信徒", "n": 1, "layer": "直发"}, {"type": "生成信徒", "n": 1, "layer": "苏醒"}],
		"note": "09-06 DB改动: ATK 2→1; 苏醒payload勘误=1(④)",
	},
	"RIFT_INSECT_4.0": {
		"eff": [{"type": "攻击", "n": 2, "layer": "直发"}, {"type": "生成信徒", "n": 1, "layer": "直发"}],
		"note": "09-06 DB改动: ATK 3→2",
	},
	"RIFT_SHEPHERD": {
		"mode": "标准",
		"eff": [{"type": "攻击", "n": 1, "layer": "直发"}, {"type": "复活友方", "n": 1, "layer": "直发"}],
		"override": "", "reason": "",
		"note": "09-06 DB改动: ATK 2→1; tag定向复活[信徒]全额+2.0(⑥), 池=信徒tag卡13张(含自身); 原0.2空白体口径作废",
	},
	"SOLDIER_SKELETON_4.0": {
		"eff": [{"type": "攻击", "n": 2, "layer": "直发"}, {"type": "复活友方", "n": 1, "layer": "遗言"}],
		"note": "09-06 DB改动: 遗言置顶自身→复活自身(墓地置顶=复活引擎同价)",
	},
	"DOOM_HERALD": {
		"override": 3.5, "reason": "攻0+复活最强敌方(陷阱折减≈-0.5)+强化4x1.0; 期望3.5±3 (强化新规后自4.3下调)",
	},
	"LAST_RITES": {
		"override": 3.4, "reason": "遗言:触发所有墓地遗言 0.3x(3~6DRx2.5)≈3.4 (条件层系数0.8→0.3)",
	},
	"SWARM_CURSER": {
		"extra_val": 1.0,
		"note": "生成1信徒(+1.0, 09-06拍板)另加; 每有1友方信徒强化1(信徒≈2.4/轮)",
	},
	"RELIC_HIVE": {
		"mode": "引擎",
		"eff": [{"type": "生成信徒", "n": 1, "layer": "全局C"}], "freq": 3.0, "override": "", "reason": "",
		"note": "信徒=+1.0后转引擎口径: 友方攻击≈3段/轮x1.0; 原『信徒=0』口径作废",
	},
}

# ------------------------------------------------------------------- scoring
def fmt(x):
	if abs(x - round(x, 1)) < 1e-9:
		return ("%g" % round(x, 1))
	return ("%g" % round(x, 2))

def base_expr(t, n):
	if t == "攻击":
		return "%g" % n
	if t == "复活友方" and n >= 2 and float(n).is_integer():
		return "2.0×%g×0.8" % n
	f = BASE_UNIT[t](1.0)
	if n != 1:
		return "%g×%g" % (f, n)
	return "%g" % f

def score(row):
	patch = PATCHES.get(row["CARD_TYPE_ID"], {})
	mode = patch.get("mode", row.get("mode") or "标准")
	effs = patch.get("eff", row.get("eff") or [])
	freq = patch.get("freq", row.get("freq"))
	override = patch.get("override", row.get("override"))
	extra = patch.get("extra_val", 0.0)
	if mode in ("人工覆盖", "构建核心"):
		return (float(override) if override not in ("", None) else 0.0), mode, []
	terms, total = [], 0.0
	for e in effs:
		v = BASE_UNIT[e["type"]](e["n"]) * LAYER_MULT[e["layer"]]
		total += v
		base = base_expr(e["type"], e["n"])
		m = LAYER_MULT[e["layer"]]
		expr = "%s×%s" % (base, fmt(m)) if m != 1.0 else base
		terms.append((v, expr))

	def term_str(i, v, ex):
		s = ex if v >= 0 else "−" + ex.lstrip("-")
		return ("+" + s) if (i > 0 and v >= 0) else s

	strs = [term_str(i, v, ex) for i, (v, ex) in enumerate(terms)]
	if mode == "引擎" and freq not in ("", None):
		total = total * float(freq)
		if len(terms) == 1:
			exprs = ["%s×%s/轮" % (terms[0][1], fmt(float(freq)))]
		else:
			exprs = ["(%s)×%s/轮" % ("".join(strs), fmt(float(freq)))]
	else:
		exprs = strs
	if extra:
		total += extra
		exprs.append("+生成1信徒%s" % fmt(extra))
	return total, mode, exprs

def verdict(v, rarity, mode):
	lo, hi = BANDS[rarity]
	if mode == "构建核心":
		return "build"
	if v <= 0 or v < 1.0:
		return "bang"
	if v < lo:
		return "under"
	if v > hi + (hi - lo):
		return "over2"
	if v > hi:
		return "over"
	return "in"

def eff_text(row):
	patch = PATCHES.get(row["CARD_TYPE_ID"], {})
	effs = patch.get("eff", row.get("eff") or [])
	if not effs:
		# manual/build rows carry no eff slots — fall back to the prefab desc text
		txt = re.sub(r"</?b>", "", row.get("desc") or "特殊复合")
		return txt.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
	out = []
	for e in effs:
		seg = EFF_TEXT[e["type"]].format(n=("%g" % e["n"]))
		out.append(LAYER_PREFIX.get(e["layer"], "") + seg)
	return "；".join(out)

def verdict_badge(v):
	cls = {"over2": "over", "in": "in", "under": "under", "bang": "bang", "build": "build", "over": "over"}[v]
	label = {"over2": "++", "over": "+", "in": "=", "under": "−", "bang": "!", "build": "B"}[v]
	return '<span class="badge b-%s">%s</span>' % (cls, label)

# ------------------------------------------------------------------ generate
rows = json.load(open(ROWS_PATH, encoding="utf-8"))
for r in rows:
	rarity = r["rarity"]
	if r["CARD_TYPE_ID"] in PATCHES:
		r.update({k: v for k, v in PATCHES[r["CARD_TYPE_ID"]].items() if k != "extra_val"})
		if "extra_val" in PATCHES[r["CARD_TYPE_ID"]]:
			r["_extra"] = PATCHES[r["CARD_TYPE_ID"]]["extra_val"]
	r["_mode"] = r.get("mode") or "标准"
	if r["_mode"] in ("人工覆盖", "构建核心"):
		r["_val"] = float(r.get("override") or 0)
	else:
		v, _, _ = score(r)
		r["_val"] = v
	r["_verdict"] = verdict(r["_val"], rarity, r["_mode"])
	r["_hasC"] = any(e.get("layer") == "全局C" for e in (r.get("eff") or []))
	r["_efftxt"] = eff_text(r)

rows.sort(key=lambda r: (BANDS.keys().__iter__().__next__() if False else ["normal", "uncommon", "rare"].index(r["rarity"]), r["CARD_TYPE_ID"]))
rarity_label = {"normal": "Common", "uncommon": "Uncommon", "rare": "Rare"}
rarity_letter = {"normal": "C", "uncommon": "U", "rare": "R"}

def mean(rs):
	return sum(r["_val"] for r in rs) / len(rs) if rs else 0

counts = {rr: len([r for r in rows if r["rarity"] == rr]) for rr in ("normal", "uncommon", "rare")}
verdict_counts = {}
for r in rows:
	verdict_counts[r["_verdict"]] = verdict_counts.get(r["_verdict"], 0) + 1

def tr(r):
	v = verdict_badge(r["_verdict"])
	note = r.get("note") or ""
	vtxt = fmt(r["_val"]) + ("/轮" if r["_mode"] == "引擎" and r.get("freq") not in ("", None) else "")
	eng = ' <span class="badge b-eng">C</span>' if r["_hasC"] else ""
	dv = "over" if r["_verdict"] == "over2" else r["_verdict"]
	return ('<tr data-r="%s" data-v="%s"><td>%s</td><td class="cid">%s</td><td>%s</td><td>%s</td><td class="num">%s</td>'
		'<td>%s %s%s</td></tr>') % (
		r["rarity"], dv, r["中文名"] or r["CARD_TYPE_ID"], r["CARD_TYPE_ID"], r["_efftxt"],
		"".join(r.get("_exprs", [])) if r.get("_exprs") else "", vtxt, v, eng, note)

# build per-rarity tables with 估值式 strings
body_rows = {rr: [] for rr in ("normal", "uncommon", "rare")}
for r in rows:
	_, mode, exprs = score(r)
	if r["_mode"] in ("人工覆盖", "构建核心"):
		exprs = [r.get("reason", "人工覆盖")]
	if r.get("_extra"):
		pass
	r["_exprs"] = exprs
	body_rows[r["rarity"]].append(tr(r))

tables_html = ""
for rr in ("normal", "uncommon", "rare"):
	rs = [r for r in rows if r["rarity"] == rr]
	lo, hi = BANDS[rr]
	tables_html += '\t<h3>%s (%d) — 均值 ≈ %s (带 %s–%s, 09-06 全口径)</h3>\n\t<table>\n' % (
		rarity_label[rr], len(rs), fmt(round(mean(rs), 2)), fmt(lo), fmt(hi))
	tables_html += '\t<thead><tr><th>卡</th><th>ID</th><th>效果</th><th>估值式</th><th>值</th><th>判定</th></tr></thead>\n\t<tbody>\n'
	tables_html += "\n".join(body_rows[rr]) + "\n\t</tbody>\n\t</table>\n\n"

# findings
overs = [r for r in rows if r["_verdict"] in ("over", "over2")]
overs.sort(key=lambda r: -r["_val"])
bangers = [r for r in rows if r["_verdict"] == "bang"]
bangers.sort(key=lambda r: r["_val"])
unders = [r for r in rows if r["_verdict"] == "under"]
rare_lows = [r for r in rows if r["rarity"] == "rare" and r["_verdict"] == "under"]
rare_lows.sort(key=lambda r: -r["_val"])
nonrare_high = [r for r in rows if r["rarity"] != "rare" and (
	r["_val"] >= 4.0 or r["_verdict"] == "build" or (r["_mode"] == "引擎" and r["_val"] >= 2.4))]
nonrare_high.sort(key=lambda r: -r["_val"])

def list_rows(rs, cols):
	out = ""
	for r in rs:
		vtxt = fmt(r["_val"]) + ("/轮" if r["_mode"] == "引擎" else "")
		note = r.get("note") or ""
		out += "<tr><td>%s %s</td><td>%s</td><td>%s</td><td>%s</td></tr>\n" % (
			r["中文名"] or r["CARD_TYPE_ID"], r["CARD_TYPE_ID"], rarity_letter[r["rarity"]], vtxt, note)
	return out

findings_over = "".join("<tr><td>%s %s</td><td>%s</td><td>%s</td><td>%s</td></tr>\n" % (
	r["中文名"] or r["CARD_TYPE_ID"], r["CARD_TYPE_ID"], rarity_letter[r["rarity"]], fmt(r["_val"]) + ("/轮" if r["_mode"] == "引擎" else ""),
	(r.get("note") or "")) for r in overs[:12])
findings_bang = "".join("<tr><td>%s %s</td><td>%s</td><td>%s</td><td>%s</td></tr>\n" % (
	r["中文名"] or r["CARD_TYPE_ID"], r["CARD_TYPE_ID"], rarity_letter[r["rarity"]], fmt(r["_val"]) + ("/轮" if r["_mode"] == "引擎" else ""),
	(r.get("note") or "")) for r in bangers)

html = """<!DOCTYPE html>
<html lang="zh-CN">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>OneDeck Zombie 基准卡牌平衡审计 (4.0)</title>
<style>
	:root { --bg:#0d1117; --surface:#161b22; --border:#30363d; --text:#c9d1d9; --muted:#8b949e; --accent:#58a6ff;
		--over:#f85149; --over2:#da3633; --in:#3fb950; --under:#58a6ff; --bang:#f0883e; --build:#a371f7; --eng:#d29922; }
	* { box-sizing:border-box; }
	body { margin:0; font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",Helvetica,Arial,sans-serif; background:var(--bg); color:var(--text); line-height:1.6; }
	.container { max-width:1280px; margin:0 auto; padding:32px 24px; }
	header { border-bottom:1px solid var(--border); padding-bottom:24px; margin-bottom:24px; }
	h1 { margin:0 0 8px; font-size:30px; }
	.subtitle { color:var(--muted); font-size:14px; }
	.grid { display:grid; grid-template-columns:repeat(auto-fit,minmax(260px,1fr)); gap:16px; margin-bottom:24px; }
	.card { background:var(--surface); border:1px solid var(--border); border-radius:12px; padding:16px 20px; }
	.card h2 { margin:0 0 10px; font-size:15px; color:var(--accent); }
	.metric { display:flex; justify-content:space-between; padding:6px 0; border-bottom:1px solid var(--border); font-size:14px; }
	.metric:last-child { border-bottom:none; }
	.metric .value { font-weight:600; }
	.section { background:var(--surface); border:1px solid var(--border); border-radius:12px; padding:20px 24px; margin-bottom:24px; }
	.section h2 { margin:0 0 14px; font-size:19px; color:var(--accent); }
	.section h3 { margin:18px 0 8px; font-size:15px; color:var(--text); }
	.note { color:var(--muted); font-size:13px; }
	.legend { display:flex; flex-wrap:wrap; gap:8px; margin:12px 0 4px; }
	table { width:100%%; border-collapse:collapse; font-size:13.5px; }
	th,td { padding:8px 10px; text-align:left; border-bottom:1px solid var(--border); vertical-align:top; }
	th { background:rgba(88,166,255,0.08); color:var(--accent); font-weight:600; position:sticky; top:0; }
	tr:hover td { background:rgba(255,255,255,0.03); }
	td.num { white-space:nowrap; font-weight:600; }
	td.cid { color:var(--muted); font-size:12px; white-space:nowrap; }
	.badge { display:inline-block; min-width:30px; text-align:center; padding:1px 8px; border-radius:12px; font-size:12px; font-weight:700; }
	.b-over2 { background:rgba(218,54,51,0.25); color:var(--over2); }
	.b-over  { background:rgba(248,81,73,0.2);  color:var(--over); }
	.b-in    { background:rgba(63,185,80,0.2);  color:var(--in); }
	.b-under { background:rgba(88,166,255,0.2); color:var(--under); }
	.b-bang  { background:rgba(240,136,62,0.22);color:var(--bang); }
	.b-build { background:rgba(163,113,247,0.22);color:var(--build); }
	.b-eng   { background:rgba(210,153,34,0.22); color:var(--eng); }
	.toolbar { display:flex; flex-wrap:wrap; gap:10px; align-items:center; margin-bottom:14px; }
	.toolbar button, .toolbar select, .toolbar input { background:var(--bg); color:var(--text); border:1px solid var(--border); border-radius:8px; padding:6px 12px; font-size:13px; }
	.toolbar button.active { border-color:var(--accent); color:var(--accent); }
	.toolbar input { flex:1; min-width:180px; }
	.count { color:var(--muted); font-size:13px; }
	details { border:1px solid var(--border); border-radius:10px; padding:12px 16px; margin-bottom:14px; background:rgba(255,255,255,0.02); }
	details summary { cursor:pointer; color:var(--accent); font-weight:600; }
	.footer { margin-top:28px; padding-top:20px; border-top:1px solid var(--border); color:var(--muted); font-size:12.5px; }
	.footer code { background:var(--bg); border:1px solid var(--border); border-radius:6px; padding:1px 6px; }
	.list-tbl td:first-child { font-weight:600; white-space:nowrap; }
</style>
</head>
<body>
<div class="container">

<header>
	<h1>Zombie 基准卡牌平衡审计 — 4.0 卡池</h1>
	<div class="subtitle">生成 2026-09-06 (第2轮·全池统一口径) · 基线 ZOMBIE = 2.0 伤害/轮 · HP 20 · 战斗窗口 R≈4 · 全循环模型 (Start Card 固定卡组底, q=1) · 数据源: Notion「4.0 card database」(09-06 normal 卡数值已更新) + <code>tools/outputs/audit_rows_40.json</code> (prefab 抽取) + 改动补丁 (<code>tools/scripts/gen_balance_audit_report.py</code>) · <b style="color:var(--bang)">2026-09-06: 审计 DB 弃用, 评分由本报告按 §约定 自算</b>; 强化三类统一 1.0/层; 条件层减分 (遗言 0.3 / 苏醒·强化反应·条件 0.5) 全池生效; 生成1信徒 +1.0 全池生效</div>
	<div class="legend">
		<span class="badge b-over2">++</span><span class="note">严重超带 (超带宽1倍)</span>
		<span class="badge b-over">+</span><span class="note">超带</span>
		<span class="badge b-in">=</span><span class="note">带内</span>
		<span class="badge b-under">−</span><span class="note">低于带</span>
		<span class="badge b-bang">!</span><span class="note">反值/真空负</span>
		<span class="badge b-build">B</span><span class="note">build-around 高方差</span>
		<span class="badge b-eng">C</span><span class="note">全局监听引擎</span>
	</div>
</header>

<div class="grid">
	<div class="card"><h2>池子快照</h2>
		<div class="metric"><span>战斗卡 (启用)</span><span class="value">89</span></div>
		<div class="metric"><span>商店 utility/system</span><span class="value">18</span></div>
		<div class="metric"><span>稀有度 C / U / R</span><span class="value">19 / 44 / 26</span></div>
		<div class="metric"><span>token (RIFT/JU_ON, 不评分)</span><span class="value">2</span></div>
	</div>
	<div class="card"><h2>段位真空均值</h2>
		<div class="metric"><span>Common (带 1.6–2.4)</span><span class="value">≈%s</span></div>
		<div class="metric"><span>Uncommon (带 2.4–4.0)</span><span class="value">≈%s</span></div>
		<div class="metric"><span>Rare (带 4.0–8.0)</span><span class="value">≈%s</span></div>
		<div class="metric"><span>结论</span><span class="value" style="color:var(--bang)">信徒+1.0 后生成器回归带内; R 档仍偏低</span></div>
	</div>
	<div class="card"><h2>问题卡计数 (机械判定)</h2>
		<div class="metric"><span>超带 (+ / ++)</span><span class="value" style="color:var(--over)">%d / %d</span></div>
		<div class="metric"><span>带内 (=)</span><span class="value" style="color:var(--in)">%d</span></div>
		<div class="metric"><span>低于带 (−)</span><span class="value" style="color:var(--under)">%d</span></div>
		<div class="metric"><span>反值 (!) / build (B)</span><span class="value" style="color:var(--bang)">%d / %d</span></div>
	</div>
	<div class="card"><h2>本轮口径影响</h2>
		<div class="metric" style="display:block"><span>① 用户 8 张 normal 卡数值改动已入库重算: Common 均值 2.8→2.61 (带内 4/19, 超带 14/19)</span></div>
		<div class="metric" style="display:block"><span>② 强化 1.0/层: RIFT_REAPER 3.2→4.0 回带底; RELIC_TALLY 2.4→2.0/轮 落带下; DOOM_HERALD 4.3→3.5</span></div>
		<div class="metric" style="display:block"><span>③ 生成信徒 +1.0: 野庙/点传师/歌女 3 张反值卡 + 笔/那条街/人影 3 张低于带卡回归带内; RELIC_HIVE 转引擎 3/轮</span></div>
		<div class="metric" style="display:block"><span>④ 条件层 0.5/0.3 全池: DEATHBED_PORTER 4.8→3.05, CURSE_THIRST 3.6→3.0, LAST_RITES 6.0→3.4</span></div>
	</div>
</div>

<div class="section">
	<h2 id="audit">战斗卡评分表 (89)</h2>
	<div class="toolbar">
		<button class="rfilter active" data-r="all">全部</button>
		<button class="rfilter" data-r="normal">Common (%d)</button>
		<button class="rfilter" data-r="uncommon">Uncommon (%d)</button>
		<button class="rfilter" data-r="rare">Rare (%d)</button>
		<select id="vfilter">
			<option value="all">全部判定</option>
			<option value="over">超带 (++/+)</option>
			<option value="in">带内 (=)</option>
			<option value="under">低于带 (−)</option>
			<option value="bang">反值 (!)</option>
			<option value="build">build-around (B)</option>
		</select>
		<input id="search" type="text" placeholder="搜索卡名 / ID / 效果…">
		<span class="count" id="rcount"></span>
	</div>

%s
</div>

<div class="section">
	<h2>核心发现</h2>
	<h3>① 超带卡 (强度离群, 前12)</h3>
	<table class="list-tbl">
	<thead><tr><th>卡</th><th>稀有度</th><th>值</th><th>备注</th></tr></thead>
	<tbody>
%s	</tbody>
	</table>

	<h3>② 真空反值 / 零值引擎 (需确认设计意图)</h3>
	<table class="list-tbl">
	<thead><tr><th>卡</th><th>稀有度</th><th>真空值</th><th>备注</th></tr></thead>
	<tbody>
%s	</tbody>
	</table>
	<p class="note"><b>信徒轴 09-06 复盘</b>: 生成1信徒 +1.0 后生成器家族 (野庙/笔/库房/那条街/签到表/鞋印) 全部回归带内或贴近带底; 转化器 RIFT_REAPER 4.0 回带底。原「信徒轴结构性偏弱」结论作废。</p>

	<h3>③ 谓词陷阱</h3>
	<p class="note"><b>递请柬的人 DOOM_HERALD</b>「复活1攻击力最高敌方」: 当敌方诅咒不是最高攻卡时 (前期/无诅咒对局), 会复活对面的最强生物 (≈−2.5 摆动)。强化新规后期望 4.3→3.5±3, 仍建议改为只复活敌方诅咒或加保底。</p>
</div>

<div class="section">
	<h2>稀有度 × 方差错位 (评审重点)</h2>
	<h3>A. Rare 但真空不足 4.0 (非 build-around)</h3>
	<table class="list-tbl">
	<thead><tr><th>卡</th><th>稀有度</th><th>值</th><th>备注</th></tr></thead>
	<tbody>
%s	</tbody>
	</table>
	<h3>B. 非 Rare 但真空已达 rare 带值 (≥4.0) 或高频引擎 (≥2.4/轮)</h3>
	<table class="list-tbl">
	<thead><tr><th>卡</th><th>稀有度</th><th>值</th><th>备注</th></tr></thead>
	<tbody>
%s	</tbody>
	</table>
	<p class="note">判定规则 (机械): 带 C 1.6–2.4 / U 2.4–4.0 / R 4.0–8.0; &gt;带顶=+, ≥带顶+带宽=++, &lt;带底=−, ≤0 或 &lt;1.0=!, 构建核心=B, 全局C 行另挂 C 徽章。边界值含带顶 (=)。</p>
</div>

<div class="section">
	<h2>商店 Utility / System 卡 (18, 不评分)</h2>
	<p class="note">每轮战斗价值口径不适用于商店经济卡 (2026-09-05 用户决定)。全部 18 张已在 Notion 4.0 card database 建档。</p>
	<table>
	<thead><tr><th>卡</th><th>中文名</th><th>稀有度</th><th>效果</th></tr></thead>
	<tbody>
	<tr><td class="cid">UTILITY_DISCOUNT_1</td><td>黑市交情</td><td>normal</td><td>每重掷4次, 随机一件商品降价1 (仅当前货架)</td></tr>
	<tr><td class="cid">UTILITY_INCOME_1</td><td>教团津贴</td><td>normal</td><td>每场战斗后的收入+2 (在卡组即生效, 卖出即失效)</td></tr>
	<tr><td class="cid">UTILITY_ODDS_1</td><td>帷幕之后</td><td>normal</td><td>每家商店的首个货架必为奇物架</td></tr>
	<tr><td class="cid">UTILITY_OPTION_1</td><td>摊位契约</td><td>normal</td><td>商店货架的通用槽位+1</td></tr>
	<tr><td class="cid">UTILITY_REROLL_1</td><td>重掷许可</td><td>normal</td><td>每家商店的免费重掷次数+1</td></tr>
	<tr><td class="cid">UTILITY_SLOT_U_1</td><td>低语引荐</td><td>normal</td><td>每家商店的首个货架必出1张罕见(蓝)卡</td></tr>
	<tr><td class="cid">SYSTEM_INCREASE_DECK_SIZE_LITE</td><td>卡位扩张</td><td>normal</td><td>购买后卡位上限+1, 即刻自我消耗; 价格随已购次数递增</td></tr>
	<tr><td class="cid">SYSTEM_INCREASE_HP_MAX</td><td>早睡早起</td><td>normal</td><td>生命值上限+4 (常驻: 在卡组即生效)</td></tr>
	<tr><td class="cid">UTILITY_CREATURES_1</td><td>生物潮汐</td><td>uncommon</td><td>每次生成货架 20%% 概率: 战斗架商品全变生物卡</td></tr>
	<tr><td class="cid">UTILITY_ODDS_2</td><td>裂缝观测仪</td><td>uncommon</td><td>奇物架出现概率+15%%</td></tr>
	<tr><td class="cid">UTILITY_SLOT_U_2</td><td>蓝字担保</td><td>uncommon</td><td>每个货架必出1张罕见(蓝)卡</td></tr>
	<tr><td class="cid">UTILITY_SPELLS_1</td><td>咒物潮汐</td><td>uncommon</td><td>每次生成货架 20%% 概率: 战斗架商品全变非生物卡</td></tr>
	<tr><td class="cid">UTILITY_WEIGHT_U</td><td>传播者的赞歌</td><td>uncommon</td><td>罕见(蓝)卡出货权重×2</td></tr>
	<tr><td class="cid">UTILITY_SLOT_R</td><td>红字担保</td><td>rare</td><td>每3个货架必出1张史诗(红)卡</td></tr>
	<tr><td class="cid">UTILITY_TAG_AWAKEN</td><td>苏醒之钟</td><td>rare</td><td>每3个货架必出1张带[苏醒]的卡</td></tr>
	<tr><td class="cid">UTILITY_TAG_CURSE</td><td>诅咒圣物</td><td>rare</td><td>每3个货架必出1张带[诅咒]的卡</td></tr>
	<tr><td class="cid">UTILITY_TAG_REVIVE</td><td>复活祭坛</td><td>rare</td><td>每3个货架必出1张带[复活]的卡</td></tr>
	<tr><td class="cid">UTILITY_WEIGHT_R</td><td>殉道者的赞歌</td><td>rare</td><td>史诗(红)卡出货权重×2</td></tr>
	</tbody>
	</table>
</div>

<div class="section">
	<h2>估值约定 (2026-09-06 全口径)</h2>
	<details>
	<summary>展开: 09-06 两轮修订 / 沿用 3.0 的约定 / 4.0 引擎语义 / 判定规则</summary>
	<h3>2026-09-06 第二轮 (本轮拍板)</h3>
	<p class="note">⑦ <b>强化三类统一 1.0/层</b>: 给力量友方 0.8 / 给力量自身 1.5 / 强化敌方诅咒 1.2 → 全部改为「强化 N 层 = +N 分」(09-06 拍板)。攻击次数类 (+1/全体+1) 不属于强化, 维持原值。⑧ <b>Notion 4.0 审计 DB 弃用</b> (09-06): 评分改由 <code>tools/scripts/gen_balance_audit_report.py</code> 按本节约定自算; 数据链 = prefab 抽取 (<code>audit_rows_40.json</code>) + card DB 当前值改动补丁 (脚本内 PATCHES)。DB 行与公式不再维护。⑨ 用户 normal 卡数值改动 8 张 (AVENGER 攻x2 / BEAST·ELITE·RIFT_ACOLYTE·RIFT_SHEPHERD ATK↓ / LAST_GIFT 强化2 / RIFT_INSECT ATK2 / SOLDIER 遗言复活自身) 已入库重算。⑩ 条件层「条件」选项系数 0.8→0.5 (并入 09-06 第一轮的条件层减分体系)。</p>
	<h3>2026-09-06 第一轮 (已拍板)</h3>
	<p class="note">① 条件层一律减分: 苏醒(OnMeRevived) <b>×0.5</b> / 强化反应 <b>×0.5</b> — 触发后卡回卡组顶继续揭晓; 遗言(OnMeBuried) <b>×0.3</b> — 触发后卡入墓地不再揭晓。② 生成1信徒 <b>+1.0</b> (信徒揭晓=复活1友方+放逐自身, 2.0−1.0)。③ 信徒=RIFT token (11 张生成器经 AddCardToMe 绑 RIFT.prefab 生成), 诅咒=JU_ON token (揭晓对持有方玩家造成[攻击力]伤害, 0攻 no-op)。两 token rarity 不挂、不进评分池。④ tag定向复活/选取 (「复活1张tag为[X]的友方卡」) 按 复活1友方 +2.0 全额计; tag选区不适用 token (token 无 tag) — RIFT_SHEPHERD = ReviveMyCardsWithTag[信徒], 池=信徒tag卡13张 (C3/U5/R5)。⑤ RIFT_ACOLYTE 苏醒 payload = 生成1信徒 (prefab 勘误)。</p>
	<h3>沿用 3.0</h3>
	<p class="note">直接伤害 X = X｜埋葬1敌方 +2.0｜埋葬1友方 −1.2｜置顶1友方 +1.5｜敌我不分 ≈0｜放逐友方(非信徒) −2.0｜放逐友方信徒 −1.0｜全局监听无区域检查 ×1.0 (C类, 按每轮触发次数计)｜Linger ×0.5。</p>
	<h3>4.0 引擎语义 (代码验证)</h3>
	<p class="note">攻击伤害 = 卡攻击属性 × 段数 (<code>Attack()</code>=1+extraAttackTimes, <code>AttackTimes(N)</code>=N段), 每段独立触发攻击事件。复活=墓地→卡组顶 (触发苏醒); 遗言=OnMeBuried; 强化反应=OnMeGainedAttack; 磨牌会触发被磨卡遗言。复活N张 (整数N≥2) ×0.8 多复质量折减; 分数 n = 数量×质量系数 (不再折减)。</p>
	<h3>新机制估值 (拍板值)</h3>
	<p class="note">磨牌1卡 <b>−0.8</b> (默认遗言配合)｜生成1信徒 <b>+1.0</b>｜复活1友方 <b>+2.0</b> (±0.5/攻, N张×0.8)｜复活1敌方诅咒 +2.0｜延迟复活 ×0.7｜全体友方生物攻击次数+1 <b>+4.0</b>｜1友方攻次+1 +1.5｜敌方诅咒攻次+1 +2.0｜苏醒→遗言转换 +2.0｜墓地生物攻击 +2.0｜复活最强敌方夺攻 +3.0｜敌方诅咒攻击力=墓地友方数 build-around (base 2.5)｜友方攻击→强化诅咒 build-around｜所有生物−1攻 build-around (真空≈0)｜攻击力翻倍 build-around (真空0)。</p>
	</details>
</div>

<div class="footer">
	重新生成: <code>python tools/scripts/gen_balance_audit_report.py</code> (读 <code>tools/outputs/audit_rows_40.json</code> + 脚本内改动补丁 → 原地覆盖本文件) · 改卡数值后: 改 card DB → 更新脚本 PATCHES → 重跑 · Notion 镜像页仅明确要求时生成 · 审计 DB (Notion「4.0 审计」) 已弃用 · 技能: <code>.agents/skills/unity-zombie-balance-audit</code>
</div>

</div>

<script>
(function () {
	var rows = Array.prototype.slice.call(document.querySelectorAll('h2#audit ~ table tbody tr'));
	var rarity = 'all', verdict = 'all', q = '';
	var rcount = document.getElementById('rcount');
	function apply() {
		var shown = 0;
		rows.forEach(function (tr) {
			var okR = rarity === 'all' || tr.getAttribute('data-r') === rarity;
			var okV = verdict === 'all' || tr.getAttribute('data-v') === verdict;
			var okQ = !q || tr.textContent.toLowerCase().indexOf(q) !== -1;
			var show = okR && okV && okQ;
			tr.style.display = show ? '' : 'none';
			if (show) shown++;
		});
		rcount.textContent = '显示 ' + shown + ' / ' + rows.length + ' 张';
	}
	document.querySelectorAll('.rfilter').forEach(function (btn) {
		btn.addEventListener('click', function () {
			document.querySelectorAll('.rfilter').forEach(function (b) { b.classList.remove('active'); });
			btn.classList.add('active');
			rarity = btn.getAttribute('data-r');
			apply();
		});
	});
	document.getElementById('vfilter').addEventListener('change', function (e) { verdict = e.target.value; apply(); });
	document.getElementById('search').addEventListener('input', function (e) { q = e.target.value.toLowerCase().trim(); apply(); });
	apply();
})();
</script>
</body>
</html>
"""

c_mean = mean([r for r in rows if r["rarity"] == "normal"])
u_mean = mean([r for r in rows if r["rarity"] == "uncommon"])
r_mean = mean([r for r in rows if r["rarity"] == "rare"])
html = html % (
	fmt(round(c_mean, 2)), fmt(round(u_mean, 2)), fmt(round(r_mean, 2)),
	verdict_counts.get("over", 0), verdict_counts.get("over2", 0), verdict_counts.get("in", 0),
	verdict_counts.get("under", 0), verdict_counts.get("bang", 0), verdict_counts.get("build", 0),
	counts["normal"], counts["uncommon"], counts["rare"],
	tables_html,
	findings_over,
	findings_bang,
	list_rows(rare_lows, None),
	list_rows(nonrare_high, None),
)

data = html.encode("utf-8").decode("utf-8")
with open(OUT_PATH, "wb") as f:
	f.write(data.replace("\r\n", "\n").replace("\n", "\r\n").encode("utf-8"))
print("written", OUT_PATH)
print("means C=%.2f U=%.2f R=%.2f" % (c_mean, u_mean, r_mean))
print("verdicts:", verdict_counts)
for rr in ("normal", "uncommon", "rare"):
	vd = {}
	for r in rows:
		if r["rarity"] == rr:
			vd[r["_verdict"]] = vd.get(r["_verdict"], 0) + 1
	print(rr, vd)
print("over2 cards:", [r["CARD_TYPE_ID"] for r in rows if r["_verdict"] == "over2"])
