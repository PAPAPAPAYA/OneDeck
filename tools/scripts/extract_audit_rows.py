# Generate structured audit rows for the Notion "4.0 审计" database.
# Input : tools/outputs/card_prefab_extract.txt (extract_card_prefabs.py output)
#         tools/outputs/notion_40_db.tsv (rarity/中文名 join)
# Output: tools/outputs/audit_rows_40.json  — machine-readable rows (eff1/2/3 type/n/layer, mode, freq, note)
#         tools/outputs/audit_mapping_40.tsv — human-readable mapping table for user review
#
# Effect-type vocabulary (base values live in the Notion formula, not here):
#   攻击        n = face damage per round (ATK x segments)
#   埋敌方 / 埋友方 / 磨牌 / 置顶友方 / 给力量友方 / 给力量自身 / 强化敌方诅咒
#   复活友方 / 复活敌方诅咒 / 延迟复活 / 放逐友方 / 放逐友方信徒 / 生成信徒
#   攻击次数全体+1 / 攻击次数单体+1 / 敌方诅咒攻击次数+1 / 苏醒转遗言
#   墓地生物攻击 / 复活最强敌方夺攻 / 攻击力翻倍
# Layer: 直发 / 遗言 / 苏醒 / 强化反应 / 全局C

import json
import os
import re
import sys

EXTRACT = "tools/outputs/card_prefab_extract.txt"
DB_TSV = "tools/outputs/notion_40_db.tsv"
OUT_JSON = "tools/outputs/audit_rows_40.json"
OUT_TSV = "tools/outputs/audit_mapping_40.tsv"

# ============================================================
# 1. Per-card structured interpretation.
# Hand-mapped from the ratified conventions (2026-09-05) against
# the prefab CONT lines. mode: std / engine / build / manual.
# engine rows carry freq (per-round trigger count assumption).
# manual rows carry override value + reason.
# ============================================================
ROWS = {
	# ---- Common (18) ----
	"AVENGER_4.0":          dict(mode="std", eff=[("攻击", 1, "直发"), ("给力量自身", 1, "遗言")]),
	"BEAST_REVIVER":        dict(mode="std", eff=[("攻击", 2, "直发"), ("复活友方", 1, "直发")]),
	"BLACKSMITH_4.0":       dict(mode="std", eff=[("攻击", 2, "直发"), ("给力量友方", 1, "直发")]),
	"ELITE_REVIVER":        dict(mode="std", eff=[("攻击", 2, "直发"), ("复活友方", 1, "直发")], note="复活目标=被强化过的"),
	"GRAVE_DREDGER":        dict(mode="std", eff=[("攻击", 2, "直发"), ("磨牌", 3, "直发")]),
	"GRAVE_FIST":           dict(mode="std", eff=[("埋友方", 1, "直发"), ("攻击", 4, "直发")]),
	"GRAVE_PUNCH_4.0":      dict(mode="std", eff=[("埋友方", 1, "直发"), ("攻击", 4, "直发")], note="攻x2: ATK2x2=4"),
	"LAST_GIFT":            dict(mode="std", eff=[("攻击", 2, "直发"), ("给力量友方", 1, "遗言")]),
	"RIFT_ACOLYTE":         dict(mode="std", eff=[("攻击", 2, "直发"), ("生成信徒", 1, "直发"), ("生成信徒", 2, "苏醒")]),
	"RIFT_INSECT_4.0":      dict(mode="std", eff=[("攻击", 3, "直发"), ("生成信徒", 1, "直发")]),
	"RIFT_SHEPHERD":        dict(mode="manual", override=2.2, reason="攻2+复活[信徒]; 信徒=空白体≈0.2, 仅转化器存在时有值"),
	"SOLDIER_SKELETON_4.0": dict(mode="std", eff=[("攻击", 2, "直发"), ("置顶友方", 1, "遗言")], note="置顶自身"),
	"SPIKE_SKELETON_4.0":   dict(mode="std", eff=[("攻击", 2, "直发"), ("攻击", 4, "遗言")], note="遗言攻x2"),
	"SPIRIT_CALLER":        dict(mode="std", eff=[("攻击", 2, "直发"), ("复活友方", 0.8, "直发")], note="复活非生物, 质量折减0.8"),
	"TWIN_STRIKER":         dict(mode="std", eff=[("攻击", 2, "直发")], note="1x2"),
	"WAKING_FIGHTER":       dict(mode="std", eff=[("攻击", 2, "直发"), ("给力量自身", 1, "苏醒")]),
	"WAR_TRAINER":          dict(mode="std", eff=[("给力量友方", 2, "直发")]),
	"WOKEN_BLADE":          dict(mode="std", eff=[("攻击", 2, "直发"), ("攻击", 4, "苏醒")], note="苏醒攻x2"),
	# ---- Uncommon (45) ----
	"AWAKENED_REAPER":      dict(mode="std", eff=[("攻击", 2, "直发"), ("埋敌方", 1, "苏醒")]),
	"BATTLE_HORN":          dict(mode="std", eff=[("攻击次数全体+1", 1, "直发")]),
	"COMBO_GRANTER":        dict(mode="std", eff=[("攻击", 1, "直发"), ("攻击次数单体+1", 1, "直发")]),
	"COMBO_STARTER":        dict(mode="std", eff=[("攻击", 1, "直发"), ("攻击次数单体+1", 1, "强化反应")], note="目标=自身"),
	"CURSE_EATER":          dict(mode="std", eff=[("攻击", 3, "直发")], note="攻=敌方诅咒攻击力(参考3), 无诅咒时0"),
	"CURSE_GARDENER":       dict(mode="std", eff=[("强化敌方诅咒", 1, "直发"), ("复活敌方诅咒", 1, "直发")]),
	"CURSE_REVIVER":        dict(mode="std", eff=[("攻击", 0, "直发"), ("复活敌方诅咒", 1, "直发")]),
	"CURSE_SUMMONER":       dict(mode="std", eff=[("复活友方", 1, "直发"), ("复活敌方诅咒", 1, "直发")]),
	"CURSE_THIRST_BEAST_4.0": dict(mode="std", eff=[("攻击", 2, "直发"), ("复活友方", 1, "条件")], note="敌方诅咒揭晓时复活自身, 条件层x0.8"),
	"DEATHBED_GRANT":       dict(mode="engine", freq=1.5, eff=[("攻击", 2, "全局C")], note="被埋友方生物攻击, 友方埋葬≈1.5/轮"),
	"DEATHBED_PORTER":      dict(mode="std", eff=[("攻击", 2, "直发"), ("攻击", 2, "遗言"), ("置顶友方", 1, "遗言")], note="置顶非生物"),
	"EULOGIST":             dict(mode="manual", override=2.5, reason="埋[遗言]友方=主动点火(−2.0+DR期望2.5)+攻2; 公式只能得0.8"),
	"EXILE_BERSERKER":      dict(mode="std", eff=[("攻击", 2, "直发"), ("攻击次数单体+1", 1, "条件")], note="每放逐1友方攻击次数+1; 放逐源仅3张"),
	"FINAL_ESCORT":         dict(mode="std", eff=[("攻击", 1, "直发"), ("置顶友方", 1, "遗言")], note="回合末置顶最高攻"),
	"FLURRY_REVIVER":       dict(mode="std", eff=[("复活友方", 1.25, "直发")], note="目标=攻击次数最多, 质量修正1.25"),
	"FUNERAL_WILL":         dict(mode="std", eff=[("磨牌", 2, "直发"), ("延迟复活", 1, "遗言")]),
	"GRAVE_GIANT":          dict(mode="std", eff=[("攻击", 3, "直发")], note="攻=墓地友方数(参考3), 随自磨成长"),
	"GRAVE_HEXER":          dict(mode="std", eff=[("复活友方", 1, "直发"), ("强化敌方诅咒", 2, "直发")]),
	"GRAVE_PUPPETEER":      dict(mode="manual", override=2.0, reason="墓地生物攻击2.0+无友方时埋[遗言]fallback"),
	"GRAVE_ROBBER":         dict(mode="manual", override=3.0, reason="复活最强敌方并夺取攻击力, 复合特殊效果"),
	"GRAVE_TOGETHER_4.0":   dict(mode="std", eff=[("攻击", 1, "直发"), ("埋友方", 1, "直发"), ("埋敌方", 2, "直发")]),
	"HEXER":                dict(mode="std", eff=[("攻击", 0, "直发"), ("强化敌方诅咒", 3, "直发")]),
	"KINGSLAYER":           dict(mode="std", eff=[("埋敌方", 1.25, "直发"), ("复活友方", 1, "直发")], note="埋最强敌方, 质量修正1.25"),
	"MASS_REVIVER":         dict(mode="manual", override=4.0, reason="复活3张normal卡, 质量折减(2.0x3x0.8=4.8→4.0)"),
	"MIMIC_BLADE":          dict(mode="std", eff=[("攻击", 6, "直发")], note="攻=最高友方攻(参考3)x2段"),
	"NECROMANCER":          dict(mode="std", eff=[("复活友方", 1, "直发"), ("复活友方", 1, "苏醒")]),
	"REANIMATOR":           dict(mode="std", eff=[("攻击", 2.5, "直发")], note="攻=本回合复活友方数(参考2.5)"),
	"RELIC_ATTACK_BURIAL":  dict(mode="engine", freq=3.0, eff=[("磨牌", 1, "全局C")], note="友方每次攻击磨1, 友方攻击≈3段/轮"),
	"RELIC_CHAIN_BURIAL":   dict(mode="engine", freq=1.2, eff=[("磨牌", 1, "全局C")], note="友方被埋葬≈1.2次/轮"),
	"RELIC_TALLY":          dict(mode="engine", freq=2.0, eff=[("强化敌方诅咒", 1, "全局C")], note="回合末按本回合埋葬数强化, 埋葬≈2/轮"),
	"RELIC_TRAINER":        dict(mode="engine", freq=1.5, eff=[("给力量友方", 1, "全局C")], note="回合开始强化最低攻, 利用率折算1.5"),
	"REVIVE_SUMMONER":      dict(mode="std", eff=[("复活友方", 1, "直发"), ("生成信徒", 1, "直发")]),
	"RIFT_GUIDE":           dict(mode="std", eff=[("放逐友方信徒", 1, "直发"), ("埋敌方", 2, "直发")], note="埋敌方非生物"),
	"RIFT_HATCHERY":        dict(mode="std", eff=[("生成信徒", 3, "直发"), ("埋友方", 1, "条件")], note="回合开始埋自身按条件层计"),
	"RIFT_MEDIUM":          dict(mode="std", eff=[("生成信徒", 2, "直发"), ("复活友方", 1, "苏醒")]),
	"RIFT_PRIEST":          dict(mode="std", eff=[("生成信徒", 2, "直发"), ("给力量友方", 1, "直发")]),
	"RIFT_REVIVER":         dict(mode="std", eff=[("放逐友方信徒", 1, "直发"), ("复活友方", 2, "直发")], note="复活非生物"),
	"RIFT_STRIKER":         dict(mode="std", eff=[("攻击", 2, "直发"), ("生成信徒", 1, "直发")], note="1x2"),
	"SACRIFICE_WEAKEST":    dict(mode="std", eff=[("埋友方", 1, "直发"), ("给力量友方", 1, "直发")], note="强化的是被埋的该卡"),
	"SACRIFICIAL_SPIRIT":   dict(mode="std", eff=[("攻击", 0, "直发"), ("埋友方", 1, "直发"), ("强化敌方诅咒", 5, "直发")]),
	"SNOWBALL":             dict(mode="std", eff=[("攻击", 2, "直发"), ("给力量自身", 1, "强化反应")], note="1x2"),
	"SOUL_TRADER":          dict(mode="std", eff=[("复活友方", 2, "直发"), ("埋友方", 1, "直发")]),
	"UNDYING_WARRIOR":      dict(mode="std", eff=[("攻击", 2, "直发"), ("复活友方", 1, "强化反应")], note="复活自身"),
	"WEAKENING_FIELD":      dict(mode="build", override=0.0, reason="所有生物−1攻敌我不分; 配诅咒/动态攻击引擎才强"),
	"WOKEN_HEX":            dict(mode="std", eff=[("强化敌方诅咒", 1, "直发"), ("强化敌方诅咒", 2, "苏醒")]),
	# ---- Rare (26) ----
	"DECIMATION":           dict(mode="std", eff=[("埋友方", 4, "直发"), ("攻击", 9, "直发")], note="埋6递减按平均4张; 攻x3=3x3"),
	"DETERIORATION_4.0":    dict(mode="std", eff=[("强化敌方诅咒", 2, "直发"), ("强化敌方诅咒", 0.8, "直发")], note="每3攻额外+1按中期诅咒攻折算0.8层"),
	"DOOM_HERALD":          dict(mode="manual", override=4.3, reason="攻0+复活最强敌方(含陷阱折减)+强化4; 期望4.3±3"),
	"DUO_REVIVER":          dict(mode="manual", override=4.0, reason="复活2张uncommon, 质量修正"),
	"GRAVE_MILLER":         dict(mode="std", eff=[("攻击", 2, "直发"), ("埋友方", 2, "直发"), ("磨牌", 5, "直发")]),
	"HEXBLADE":             dict(mode="std", eff=[("攻击", 0, "直发"), ("强化敌方诅咒", 0, "直发")], note="每有1攻击力强化1; 无自成长源, 真空0"),
	"LAST_RITES":           dict(mode="manual", override=6.0, reason="遗言:触发所有墓地遗言 0.8x(3~6DRx2.5)"),
	"MASS_SACRIFICE":       dict(mode="std", eff=[("埋友方", 5, "直发"), ("生成信徒", 5, "直发")], note="埋所有友方(参考5)+等量信徒; 遗言全点火另计"),
	"MILLBLADE":            dict(mode="std", eff=[("攻击", 2, "直发"), ("磨牌", 2, "直发")], note="每有1攻击力磨1, 按ATK2计"),
	"QUAD_STRIKER":         dict(mode="std", eff=[("攻击", 4, "直发")], note="1x4"),
	"RELIC_ATTACK_HEX":     dict(mode="engine", freq=5.0, eff=[("强化敌方诅咒", 1, "全局C")], note="有卡攻击即触发(双方), ≈5段/轮"),
	"RELIC_BLOOD_PACT":     dict(mode="build", override=0.0, reason="友方攻击转强化等量敌方诅咒; 真空≈0, 成型后极高"),
	"RELIC_CURSE_GRAVE":    dict(mode="engine", freq=1.5, eff=[("埋敌方", 1, "全局C")], note="敌方诅咒揭晓≈1.5次/轮(诅咒架); 无诅咒死卡"),
	"RELIC_CURSE_HASTE":    dict(mode="engine", freq=1.5, eff=[("敌方诅咒攻击次数+1", 1.3, "全局C")], note="诅咒揭晓≈1.5次/轮x平均诅咒攻1.3折算"),
	"RELIC_CURSE_REVIVAL":  dict(mode="engine", freq=1.5, eff=[("复活友方", 1, "全局C")], note="敌方诅咒揭晓≈1.5次/轮"),
	"RELIC_DEATH_KNELL":    dict(mode="engine", freq=1.0, eff=[("苏醒转遗言", 1, "全局C")], note="友方苏醒≈1次/轮"),
	"RELIC_GRAVE_CURSE":    dict(mode="build", override=2.5, reason="敌方诅咒攻击力=墓地友方数; 墓地不自动增长, build-around"),
	"RELIC_GRAVE_LORD":     dict(mode="engine", freq=3.75, eff=[("给力量友方", 1, "全局C")], note="墓地生物+1攻, 经复活/遗言攻击兑现≈3.75单位/轮"),
	"RELIC_HIVE":           dict(mode="build", override=0.0, reason="友方攻击时生成1信徒(按段); 信徒=0, 燃料引擎"),
	"RELIC_RIFT_OVERRIDE":  dict(mode="engine", freq=1.2, eff=[("复活敌方诅咒", 1, "全局C")], note="信徒揭晓≈1.2次/轮, 每次复活敌方诅咒+自逐"),
	"RIFT_REAPER":          dict(mode="std", eff=[("给力量友方", 4, "直发")], note="放逐所有信徒(参考4张), 每张强化1友方生物; 放逐即转化本身, 不另计信徒费用"),
	"SLIME_4.0":            dict(mode="std", eff=[("攻击", 2, "直发"), ("复活友方", 1, "遗言")], note="复制自身=新卡入组"),
	"SWARM_CURSER":         dict(mode="engine", freq=2.4, eff=[("强化敌方诅咒", 1, "全局C")], note="每有1友方信徒强化1(信徒≈2.4), 生成1信徒另计0"),
	"SWARM_QUEEN":          dict(mode="std", eff=[("攻击", 2, "直发"), ("生成信徒", 2, "直发")], note="每有1攻击力生成1信徒, 按ATK2计"),
	"UNFINISHED_ROBOT_4.0": dict(mode="build", override=0.0, reason="攻0+攻击力翻倍; 需外部强化启动"),
	"WEAPON_SPIRIT":        dict(mode="engine", freq=3.1, eff=[("给力量友方", 1, "全局C")], note="强化反应≈3次/轮(仅4张反应卡)"),
}

LAYER_TSV = {"直发": "直发", "遗言": "遗言", "苏醒": "苏醒", "强化反应": "强化反应", "全局C": "全局C"}
MODE_TSV = {"std": "标准", "engine": "引擎", "build": "构建核心", "manual": "人工覆盖"}


def load_extract():
	text = open(EXTRACT, encoding="utf-8").read()
	out = {}
	cur = None
	for line in text.splitlines():
		if line.startswith("CARD|"):
			parts = line.split("|")
			fields = dict(p.split("=", 1) for p in parts[2:] if "=" in p)
			cur = {"file": parts[1], "display": fields.get("display", ""), "type": fields.get("type", ""),
			       "atk": fields.get("atk", ""), "desc": fields.get("desc", "")}
			out[cur["type"]] = cur
	return out


def load_db():
	rows = {}
	for line in open(DB_TSV, encoding="utf-8"):
		line = line.rstrip("\n")
		if not line.strip():
			continue
		p = (line.split("\t") + [""] * 7)[:7]
		rows[p[1]] = {"id": p[0], "rarity": p[2], "name": p[4]}
	return rows


# Base-value mirror of the Notion formula (for the TSV preview only).
BASE = {"攻击": 1.0, "埋敌方": 2.0, "埋友方": -1.2, "磨牌": -0.8, "置顶友方": 1.5,
        "给力量友方": 0.8, "给力量自身": 1.5, "强化敌方诅咒": 1.2, "复活友方": 2.0,
        "复活敌方诅咒": 2.0, "延迟复活": 1.4, "放逐友方": -2.0, "放逐友方信徒": -1.0,
        "生成信徒": 0.0, "攻击次数全体+1": 4.0, "攻击次数单体+1": 1.5,
        "敌方诅咒攻击次数+1": 2.0, "苏醒转遗言": 2.0, "墓地生物攻击": 2.0,
        "复活最强敌方夺攻": 3.0, "攻击力翻倍": 0.0}
LAYER_MULT = {"直发": 1.0, "遗言": 0.8, "苏醒": 0.8, "强化反应": 0.8, "条件": 0.8, "全局C": 1.0}


def eff_value(t, n, layer):
	v = BASE[t] * n
	if t == "复活友方" and n >= 2:
		v *= 0.8
	return round(v * LAYER_MULT[layer], 2)


def main():
	extract = load_extract()
	db = load_db()
	json_rows, tsv_rows, missing = [], [], []
	for ctid, spec in ROWS.items():
		if ctid not in extract:
			missing.append(ctid)
			continue
		d = extract[ctid]
		r = db.get(ctid, {})
		mode = spec["mode"]
		if mode == "manual":
			value = spec["override"]
		elif mode == "build":
			value = spec.get("override", 0.0)
		elif mode == "engine":
			value = round(sum(eff_value(*e) for e in spec["eff"]) * spec.get("freq", 1.0), 2)
		else:
			value = round(sum(eff_value(*e) for e in spec["eff"]), 2)
		row = {"CARD_TYPE_ID": ctid, "中文名": d["display"], "rarity": r.get("rarity", ""),
		       "mode": MODE_TSV[mode], "freq": spec.get("freq", ""),
		       "eff": [{"type": e[0], "n": e[1], "layer": e[2]} for e in spec.get("eff", [])],
		       "note": spec.get("note", ""), "override": spec.get("override", ""),
		       "reason": spec.get("reason", ""), "value_preview": value,
		       "desc": d["desc"]}
		json_rows.append(row)
		eff_txt = " + ".join("%s%s[%s]" % (e[0], e[1] if e[1] != "" else "", e[2]) for e in spec.get("eff", []))
		tsv_rows.append([ctid, d["display"], r.get("rarity", ""), MODE_TSV[mode],
		                 spec.get("freq", ""), eff_txt, value, spec.get("note", "") or spec.get("reason", "")])

	with open(OUT_JSON, "w", encoding="utf-8") as fp:
		json.dump(json_rows, fp, ensure_ascii=False, indent=1)
	with open(OUT_TSV, "w", encoding="utf-8") as fp:
		fp.write("CARD_TYPE_ID\t中文名\trarity\t口径\t频率/轮\t效果拆解\t值预览\t备注\n")
		for r in tsv_rows:
			fp.write("\t".join(str(x) for x in r) + "\n")
	print("rows=%d missing=%s" % (len(json_rows), missing))
	modes = {}
	for r in json_rows:
		modes[r["mode"]] = modes.get(r["mode"], 0) + 1
	print("modes:", modes)


if __name__ == "__main__":
	main()
