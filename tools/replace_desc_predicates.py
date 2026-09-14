import os

os.chdir(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))

ROOT = "Assets/Prefabs/Cards/4.0"
TARGETS = {
    "BEAST_REVIVER","BLACKSMITH_4.0","ELITE_REVIVER","LAST_GIFT","SPIRIT_CALLER","WAR_TRAINER",
    "BATTLE_HORN","COMBO_GRANTER","CURSE_THIRST_SHAMAN_4.0","DEATHBED_GRANT","DEATHBED_PORTER",
    "FINAL_ESCORT","FLURRY_REVIVER","GRAVE_PUPPETEER","RELIC_TALLY","RELIC_TRAINER",
    "RELIC_WHITE_BANNER","RIFT_GUIDE","RIFT_PRIEST","RIFT_REVIVER","UTILITY_CREATURES_1",
    "UTILITY_SPELLS_1","WEAKENING_FIELD","RELIC_GRAVE_LORD","RIFT_REAPER",
}

# escapes as they appear in prefab YAML
SHENGWU = "\\u751F\\u7269"        # 生物
FEISHENGWU = "\\u975E\\u751F\\u7269"  # 非生物
XIANXIANG = "\\u73B0\\u8C61"      # 现象
SHITI = "\\u5B9E\\u4F53"          # 实体

changed, untouched = [], []
for root, dirs, files in os.walk(ROOT):
    for fn in files:
        if not fn.endswith(".prefab") or fn[:-7] not in TARGETS:
            continue
        path = os.path.join(root, fn)
        with open(path, "r", encoding="utf-8", newline="") as f:
            lines = f.readlines()
        hits = 0
        for i, line in enumerate(lines):
            if not line.lstrip().startswith("cardDesc:"):
                continue
            new = line.replace(FEISHENGWU, XIANXIANG).replace(SHENGWU, SHITI)
            if new != line:
                lines[i] = new
                hits += new.count(XIANXIANG) - line.count(XIANXIANG) + new.count(SHITI) - line.count(SHITI)
        if hits:
            with open(path, "w", encoding="utf-8", newline="") as f:
                f.writelines(lines)
            changed.append((fn[:-7], hits))
        else:
            untouched.append(fn[:-7])

print("changed %d:" % len(changed))
for name, hits in sorted(changed):
    print("  %-28s %d replacement(s)" % (name, hits))
if untouched:
    print("NO HIT (unexpected):", untouched)
