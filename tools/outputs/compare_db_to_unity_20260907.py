# -*- coding: utf-8 -*-
"""Compare Notion 4.0 DB (source of truth) against Unity prefabs (DB -> Unity direction)."""
import json
import re
import io
import sys

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

UNITY_JSON = 'tools/outputs/unity_cards_current.json'

# DB rows captured via Notion MCP query (2026-09-07), keyed by CARD_TYPE_ID.
# rarity: normal/uncommon/rare; atk: printed ATK (None = non-creature / unset);
# ctype: 'c' = creature, 'n' = non-creature; status: DB lifecycle (备用/已删 -> skip).
# NOTE: manual snapshot of the query result; edit here if DB changes.
DB = {
    "SPIKE_SKELETON_4.0": dict(cn="医学院的标本", rarity="normal", atk=2, ctype="c", desc="攻击；遗言：攻击x2"),
    "RIFT_ACOLYTE": dict(cn="自己签满的签到表", rarity="normal", atk=1, ctype="c", desc="攻击；生成1信徒；苏醒：生成1信徒"),
    "REANIMATOR": dict(cn="站台尽头的东西", rarity="uncommon", atk=0, ctype="c", desc="攻击；攻击力=本回合复活友方数"),
    "RIFT_STRIKER": dict(cn="出过事的那条街", rarity="uncommon", atk=1, ctype="c", desc="攻击x2；生成1信徒"),
    "RIFT_INSECT_4.0": dict(cn="门口多出的鞋印", rarity="normal", atk=2, ctype="c", desc="攻击；生成1信徒"),
    "GRAVE_FIST": dict(cn="替死鬼", rarity="normal", atk=4, ctype="c", desc="埋葬1友方；攻击"),
    "WAKING_FIGHTER": dict(cn="梦游的拳手", rarity="normal", atk=2, ctype="c", desc="攻击；苏醒：强化自身1"),
    "LAST_GIFT": dict(cn="遗物整理师", rarity="normal", atk=2, ctype="c", desc="攻击；遗言：强化2友方生物"),
    "RIFT_REAPER": dict(cn="挨个熄灭的灯", rarity="rare", atk=None, ctype="n", desc="放逐所有信徒，每放逐1，强化1友方生物"),
    "RELIC_HIVE": dict(cn="看热闹的影子", rarity="rare", atk=None, ctype="n", desc="被动：友方攻击时：生成1信徒"),
    "RIFT_MEDIUM": dict(cn="自己动的笔", rarity="uncommon", atk=None, ctype="n", desc="生成2信徒；苏醒：复活1友方"),
    "GRAVE_MILLER": dict(cn="太平间的保洁", rarity="rare", atk=2, ctype="c", desc="攻击；埋葬2友方，埋葬卡组顶5卡"),
    "SWARM_CURSER": dict(cn="旺得反常的香炉", rarity="rare", atk=None, ctype="n", desc="生成1信徒；每有1友方信徒，强化1敌方诅咒"),
    "RELIC_DEATH_KNELL": dict(cn="多敲了一下的钟", rarity="rare", atk=None, ctype="n", desc="被动：友方苏醒时：触发其遗言"),
    "FINAL_ESCORT": dict(cn="引路的白灯笼*", rarity="uncommon", atk=1, ctype="c", desc="攻击；遗言：回合结束前：复活1墓地的最高攻击力友方生物"),
    "DEATHBED_PORTER": dict(cn="纸别墅*", rarity="uncommon", atk=2, ctype="c", desc="攻击；遗言：攻击；置顶1友方非生物"),
    "NECROMANCER": dict(cn="楼下的叫魂声*", rarity="uncommon", atk=None, ctype="n", desc="复活1友方；苏醒：复活1友方"),
    "AWAKENED_REAPER": dict(cn="来领人的家属", rarity="uncommon", atk=2, ctype="c", desc="攻击；苏醒：埋葬1敌方"),
    "RIFT_HATCHERY": dict(cn="香火不断的野庙", rarity="uncommon", atk=None, ctype="n", desc="生成3信徒；回合开始：埋葬自身"),
    "GRAVE_DREDGER": dict(cn="还没停的挖掘机", rarity="normal", atk=2, ctype="c", desc="攻击；埋葬卡组顶3卡"),
    "FUNERAL_WILL": dict(cn="贴在门上的保险书", rarity="uncommon", atk=None, ctype="n", desc="埋葬2卡组顶；遗言：延迟复活1友方"),
    "SLIME_4.0": dict(cn="管道里的活物*", rarity="rare", atk=2, ctype="c", desc="遗言：复制自身；攻击"),
    "GRAVE_TOGETHER_4.0": dict(cn="并排的三座新坟", rarity="uncommon", atk=1, ctype="c", desc="攻击；埋葬1友方；埋葬2敌方"),
    "SOUL_TRADER": dict(cn="验尸官", rarity="uncommon", atk=None, ctype="n", desc="复活2友方；埋葬1友方"),
    "REVIVE_SUMMONER": dict(cn="跟回家的人影*", rarity="uncommon", atk=None, ctype="n", desc="复活1友方；生成1信徒"),
    "EULOGIST": dict(cn="哭灵人*", rarity="uncommon", atk=2, ctype="c", desc="埋葬1张tag为[遗言]的友方卡；攻击"),
    "RELIC_TALLY": dict(cn="路口的纸灰圈", rarity="uncommon", atk=None, ctype="n", desc="被动：回合结束前：本回合每埋葬1生物，强化1敌方诅咒"),
    "MASS_SACRIFICE": dict(cn="多出来的第十三张床", rarity="rare", atk=None, ctype="n", desc="埋葬所有友方；每埋葬1友方，生成1信徒"),
    "SWARM_QUEEN": dict(cn="不下台的歌女", rarity="rare", atk=2, ctype="c", desc="攻击；每有1攻击力，生成1信徒"),
    "GRAVE_GIANT": dict(cn="滴血的行李袋*", rarity="uncommon", atk=0, ctype="c", desc="攻击力=墓地友方数量；攻击"),
    "RELIC_CHAIN_BURIAL": dict(cn="水鬼*", rarity="uncommon", atk=None, ctype="n", desc="被动：友方被埋葬时：埋葬1卡组顶卡"),
    "GRAVE_PUPPETEER": dict(cn="监控里的人", rarity="uncommon", atk=None, ctype="n", desc="让墓地1友方生物攻击；墓地无友方则埋葬1张tag为[遗言]的友方卡"),
    "DEATHBED_GRANT": dict(cn="入殓师*", rarity="uncommon", atk=None, ctype="n", desc="被动：友方生物被埋葬时：该友方生物攻击"),
    "RIFT_SHEPHERD": dict(cn="来接人的黑车", rarity="normal", atk=1, ctype="c", desc="攻击；复活1张tag为[信徒]的友方卡"),
    "DETERIORATION_4.0": dict(cn="洇开的污渍", rarity="rare", atk=None, ctype="n", desc="强化2敌方诅咒；敌方诅咒每有3攻击力，额外强化1"),
    "UNDYING_WARRIOR": dict(cn="没拉平的心电图", rarity="uncommon", atk=2, ctype="c", desc="攻击；强化反应：复活自身"),
    "GRAVE_ROBBER": dict(cn="收尸人*", rarity="uncommon", atk=0, ctype="c", desc="复活1攻击力最高敌方；攻击力变为该卡攻击力"),
    "GRAVE_PUNCH_4.0": dict(cn="没有指纹的凶器", rarity="normal", atk=2, ctype="c", desc="埋葬1友方；攻击x2"),
    "RELIC_CURSE_HASTE": dict(cn="越来越快的脚步声", rarity="rare", atk=None, ctype="n", desc="被动：敌方诅咒攻击次数+1"),
    "WEAPON_SPIRIT": dict(cn="要喂血的刀", rarity="rare", atk=None, ctype="n", desc="被动：友方生物触发强化反应时：强化1该生物"),
    "RELIC_CURSE_REVIVAL": dict(cn="广播里在找人", rarity="rare", atk=None, ctype="n", desc="被动：敌方诅咒揭晓时：复活1友方"),
    "RELIC_BLOOD_PACT": dict(cn="接反的输液管", rarity="rare", atk=None, ctype="n", desc="被动：友方攻击不再造成伤害，而是强化等量敌方诅咒"),
    "SPIRIT_CALLER": dict(cn="深夜来电", rarity="normal", atk=2, ctype="c", desc="攻击；复活1友方非生物"),
    "BATTLE_HORN": dict(cn="最后一节课的铃", rarity="uncommon", atk=None, ctype="n", desc="本回合友方生物攻击次数+1"),
    "RIFT_PRIEST": dict(cn="点传师", rarity="uncommon", atk=None, ctype="n", desc="生成2信徒；强化1友方生物"),
    "ELITE_REVIVER": dict(cn="描过金的牌位", rarity="normal", atk=1, ctype="c", desc="攻击；复活1被强化过的友方生物"),
    "DOOM_HERALD": dict(cn="递请柬的人", rarity="rare", atk=0, ctype="c", desc="攻击；复活1攻击力最高敌方；强化4敌方诅咒"),
    "RELIC_CURSE_GRAVE": dict(cn="准时来的清运车", rarity="rare", atk=None, ctype="n", desc="被动：敌方诅咒揭晓时：埋葬1敌方"),
    "EXILE_BERSERKER": dict(cn="送行酒", rarity="uncommon", atk=2, ctype="c", desc="攻击；本回合每放逐1友方，攻击次数+1"),
    "SACRIFICE_WEAKEST": dict(cn="折出来的替身", rarity="uncommon", atk=None, ctype="n", desc="埋葬1攻击力最低友方；强化1该卡"),
    "TWIN_STRIKER": dict(cn="一对纸人*", rarity="normal", atk=1, ctype="c", desc="攻击x2"),
    "CURSE_REVIVER": dict(cn="洗不掉的印子", rarity="normal", atk=0, ctype="c", desc="攻击；复活1敌方诅咒"),
    "FLURRY_REVIVER": dict(cn="棺材里的敲门声*", rarity="uncommon", atk=None, ctype="n", desc="复活1攻击次数最多友方生物"),
    "KINGSLAYER": dict(cn="迁坟队", rarity="uncommon", atk=None, ctype="n", desc="埋葬1攻击力最高敌方；复活1友方"),
    "COMBO_STARTER": dict(cn="添灯油的长明灯", rarity="uncommon", atk=1, ctype="c", desc="攻击；强化反应：攻击次数+1"),
    "RELIC_ATTACK_HEX": dict(cn="隔墙有耳", rarity="rare", atk=None, ctype="n", desc="被动：有卡攻击时：强化1敌方诅咒"),
    "MILLBLADE": dict(cn="少了一页的住户名单", rarity="rare", atk=2, ctype="c", desc="攻击；每有1攻击力，埋葬卡组顶1卡"),
    "WAR_TRAINER": dict(cn="点眼的扎纸匠", rarity="normal", atk=None, ctype="n", desc="强化2友方生物"),
    "CURSE_GARDENER": dict(cn="疯长的绿萝", rarity="uncommon", atk=None, ctype="n", desc="强化1敌方诅咒；复活1敌方诅咒"),
    "MIMIC_BLADE": dict(cn="挥刀的影子", rarity="uncommon", atk=0, ctype="c", desc="攻击力=友方最高攻击力；攻击x2"),
    "SACRIFICIAL_SPIRIT": dict(cn="陪葬的活物*", rarity="normal", atk=0, ctype="c", desc="攻击；埋葬1友方；强化5敌方诅咒"),
    "UNFINISHED_ROBOT_4.0": dict(cn="生长人偶*", rarity="rare", atk=0, ctype="c", desc="攻击；攻击力翻倍"),
    "RELIC_RIFT_OVERRIDE": dict(cn="半夜换告示的手", rarity="rare", atk=None, ctype="n", desc="被动：友方信徒效果变为：\"复活1敌方诅咒；放逐自身\""),
    "MASS_REVIVER": dict(cn="回魂夜*", rarity="uncommon", atk=None, ctype="n", desc="复活3友方normal卡"),
    "BLACKSMITH_4.0": dict(cn="巷口的磨刀人", rarity="normal", atk=2, ctype="c", desc="攻击；强化1友方生物"),
    "DECIMATION": dict(cn="还留着体温的空床", rarity="rare", atk=3, ctype="c", desc="埋葬6友方，本回合每埋葬过1友方，埋葬数-1；攻击x3"),
    "BEAST_REVIVER": dict(cn="笼里的东西", rarity="normal", atk=1, ctype="c", desc="攻击；复活1生物友方"),
    "HEXER": dict(cn="对面楼的那场火", rarity="normal", atk=0, ctype="c", desc="攻击；强化3敌方诅咒"),
    "CURSE_EATER": dict(cn="吞玻璃的人", rarity="uncommon", atk=0, ctype="c", desc="攻击；攻击力=敌方诅咒攻击力"),
    "SNOWBALL": dict(cn="数不完的纸钱", rarity="uncommon", atk=1, ctype="c", desc="攻击x2；强化反应：强化自身1"),
    "GRAVE_HEXER": dict(cn="白事先生*", rarity="normal", atk=None, ctype="n", desc="复活1友方；强化2敌方诅咒"),
    "COMBO_GRANTER": dict(cn="多摆的一副碗筷", rarity="uncommon", atk=1, ctype="c", desc="攻击；本回合1友方生物攻击次数+1"),
    "QUAD_STRIKER": dict(cn="抬着空轿的纸人*", rarity="rare", atk=1, ctype="c", desc="攻击x4"),
    "RELIC_GRAVE_CURSE": dict(cn="堆满楼道的花圈*", rarity="rare", atk=None, ctype="n", desc="被动：敌方诅咒攻击力=墓地友方卡数量"),
    "DUO_REVIVER": dict(cn="阴婚*", rarity="rare", atk=None, ctype="n", desc="复活2友方uncommon卡"),
    "HEXBLADE": dict(cn="越擦越脏的玻璃", rarity="rare", atk=0, ctype="c", desc="攻击；每有1攻击力，强化1敌方诅咒"),
    "CURSE_THIRST_BEAST_4.0": dict(cn="偷吃供品的黄皮子", rarity="uncommon", atk=2, ctype="c", desc="敌方诅咒揭晓时：复活自身；攻击"),
    "SOUL_SWAPPER": dict(cn="换命契*", rarity="uncommon", atk=2, ctype="c", desc="攻击；复活1友方；埋葬1敌方", status="备用"),
    "RELIC_GRAVE_LORD": dict(cn="地下传来的叩击", rarity="rare", atk=None, ctype="n", desc="被动：墓地中的友方生物攻击力+1"),
    "WEAKENING_FIELD": dict(cn="停电", rarity="uncommon", atk=None, ctype="n", desc="所有生物本回合攻击力-1"),
    "RELIC_TRAINER": dict(cn="半夜喂奶的黑影", rarity="uncommon", atk=None, ctype="n", desc="被动：回合开始：强化1攻击力最低友方生物"),
    "LAST_RITES": dict(cn="讣告", rarity="rare", atk=None, ctype="n", desc="遗言：触发所有在墓地友方的遗言"),
    "RIFT_GUIDE": dict(cn="画了押的字据", rarity="uncommon", atk=None, ctype="n", desc="放逐1友方信徒，埋葬2敌方非生物"),
    "RIFT_REVIVER": dict(cn="撕了封条的库房", rarity="uncommon", atk=None, ctype="n", desc="放逐1友方信徒，复活2友方非生物"),
    "WOKEN_BLADE": dict(cn="头七的刀", rarity="normal", atk=2, ctype="c", desc="攻击；苏醒：攻击x2"),
    "WOKEN_HEX": dict(cn="三更开着的窗", rarity="uncommon", atk=None, ctype="n", desc="强化1敌方诅咒；苏醒：强化2敌方诅咒"),
    "RELIC_ATTACK_BURIAL": dict(cn="自己翻页的黄历", rarity="uncommon", atk=None, ctype="n", desc="被动：友方每次攻击：埋葬卡组顶1卡"),
    "SOLDIER_SKELETON_4.0": dict(cn="换岗的哨兵", rarity="normal", atk=2, ctype="c", desc="攻击；遗言：复活自身"),
    "AVENGER_4.0": dict(cn="长高的坟头", rarity="normal", atk=1, ctype="c", desc="攻击x2；遗言：强化自身1"),
    "CURSE_SUMMONER": dict(cn="替人叫魂的喇叭", rarity="uncommon", atk=None, ctype="n", desc="复活1友方；复活1敌方诅咒"),
    "SYSTEM_INCREASE_DECK_SIZE_LITE": dict(cn="卡位扩张", rarity="normal", atk=None, ctype="n", desc="购买后卡位上限+1，本卡即刻自我消耗（不入卡组）；价格随本次冒险的已购次数递增，达上限后停售"),
    "SYSTEM_INCREASE_HP_MAX": dict(cn="早睡早起", rarity="normal", atk=None, ctype="n", desc="生命值上限+4（常驻：在卡组即生效，卖出即失效）"),
    "UTILITY_CREATURES_1": dict(cn="生物潮汐", rarity="uncommon", atk=None, ctype="n", desc="每次生成货架有20%概率：战斗架的全部商品变为生物卡"),
    "UTILITY_DISCOUNT_1": dict(cn="黑市交情", rarity="normal", atk=None, ctype="n", desc="每重掷4次，随机一件商品降价1（仅当前货架）"),
    "UTILITY_INCOME_1": dict(cn="教团津贴", rarity="normal", atk=None, ctype="n", desc="每场战斗后的收入+2（在卡组即生效，卖出即失效）"),
    "UTILITY_ODDS_1": dict(cn="帷幕之后", rarity="normal", atk=None, ctype="n", desc="每家商店的首个货架必为奇物架"),
    "UTILITY_ODDS_2": dict(cn="裂缝观测仪", rarity="uncommon", atk=None, ctype="n", desc="奇物架的出现概率+15%"),
    "UTILITY_OPTION_1": dict(cn="摊位契约", rarity="normal", atk=None, ctype="n", desc="商店货架的通用槽位+1"),
    "UTILITY_REROLL_1": dict(cn="重掷许可", rarity="normal", atk=None, ctype="n", desc="每家商店的免费重掷次数+1"),
    "UTILITY_SLOT_R": dict(cn="红字担保", rarity="rare", atk=None, ctype="n", desc="每3个货架必出1张稀有卡"),
    "UTILITY_SLOT_U_1": dict(cn="低语引荐*", rarity="normal", atk=None, ctype="n", desc="每家商店的首个货架必出1张罕见(蓝)卡"),
    "UTILITY_SLOT_U_2": dict(cn="蓝字担保", rarity="uncommon", atk=None, ctype="n", desc="每个货架必出1张罕见卡"),
    "UTILITY_SPELLS_1": dict(cn="咒物潮汐", rarity="uncommon", atk=None, ctype="n", desc="每次生成货架有20%概率：战斗架的全部商品变为非生物卡"),
    "UTILITY_TAG_AWAKEN": dict(cn="苏醒之钟", rarity="rare", atk=None, ctype="n", desc="每3个货架必出1张带[苏醒]的卡"),
    "UTILITY_TAG_CURSE": dict(cn="诅咒圣物", rarity="rare", atk=None, ctype="n", desc="每3个货架必出1张带[诅咒]的卡"),
    "UTILITY_TAG_REVIVE": dict(cn="复活祭坛", rarity="rare", atk=None, ctype="n", desc="每3个货架必出1张带[复活]的卡"),
    "UTILITY_WEIGHT_R": dict(cn="殉道者的赞歌", rarity="rare", atk=None, ctype="n", desc="稀有(红)卡的出货权重×2"),
    "UTILITY_WEIGHT_U": dict(cn="传播者的赞歌", rarity="uncommon", atk=None, ctype="n", desc="罕见卡的出货权重×2"),
    "CURSE_THIRST_SHAMAN_4.0": dict(cn="咒食的萨满", rarity="uncommon", atk=None, ctype="n", desc="敌方诅咒每有1攻击力，强化1友方生物"),
    "RELIC_WHITE_BANNER": dict(cn="开道的白幡", rarity="uncommon", atk=None, ctype="n", desc="被动：回合开始：置顶1友方攻击力最高生物"),
    "RIFT": dict(cn="次元裂缝", rarity=None, atk=0, ctype="n", desc="揭晓时:复活 1 友方,去除自身", token=True),
    "JU_ON": dict(cn="诅咒", rarity=None, atk=0, ctype="n", desc="揭晓时:对自身造成[攻击力]点伤害", token=True),
}

RARITY_MAP = {"normal": "0", "uncommon": "1", "rare": "2"}
FOLDER_MAP = {"normal": "0_Common", "uncommon": "1_Uncommon", "rare": "2_Rare"}


def strip_markup(s):
    s = re.sub(r'<b>|</b>', '', s)
    s = re.sub(r'<tag:(\w+)>', r'[\1]', s)
    s = s.replace('\\n', ' ')
    return s


def norm(s):
    if s is None:
        return ''
    s = strip_markup(s)
    s = s.replace('揭晓时:', '').replace('揭晓时：', '')
    s = s.replace('：', ';').replace('，', ';').replace(',', ';').replace('、', ';')
    s = s.replace('。', ';').replace(' ', '').replace('\\n', '')
    s = s.replace('x', 'x').replace('×', 'x')
    return s.lower()


def main():
    with open(UNITY_JSON, encoding='utf-8') as f:
        unity = json.load(f)
    by_id = {}
    for c in unity:
        by_id.setdefault(c['cardTypeID'], []).append(c)

    diffs = []
    db_ids = set(DB.keys())
    unity_ids = set(by_id.keys())

    for tid in sorted(db_ids | unity_ids):
        dbrow = DB.get(tid)
        ucards = by_id.get(tid)
        if dbrow is None:
            diffs.append((tid, 'NO_DB_ROW', 'Unity prefab has no DB row'))
            continue
        if dbrow.get('status') == '备用':
            diffs.append((tid, 'SKIP_备用', 'DB row is 备用 (no prefab expected) — Unity: %s' % ('YES' if ucards else 'no')))
            continue
        if dbrow.get('token'):
            # RIFT/JU_ON tokens: no prefab under 4.0; skip field diffs but note.
            if not ucards:
                continue
            diffs.append((tid, 'TOKEN_HAS_PREFAB', 'token row has prefab(s) under 4.0'))
            continue
        if ucards is None:
            diffs.append((tid, 'NO_PREFAB', 'DB row enabled but no prefab under 4.0'))
            continue
        u = ucards[-1]
        # 中文名
        db_cn = dbrow['cn']
        if u['displayName'] != db_cn:
            diffs.append((tid, '中文名', 'DB=%r UNITY=%r' % (db_cn, u['displayName'])))
        # rarity
        db_r = RARITY_MAP[dbrow['rarity']]
        if u['rarity'] != db_r:
            diffs.append((tid, 'rarity', 'DB=%s UNITY=%s' % (db_r, u['rarity'])))
        if u['folder'] != FOLDER_MAP[dbrow['rarity']]:
            diffs.append((tid, 'folder', 'DB=%s UNITY=%s' % (FOLDER_MAP[dbrow['rarity']], u['folder'])))
        # ATK
        db_atk = dbrow['atk']
        u_atk = int(u['printedAttack'])
        if dbrow['ctype'] == 'c':
            if db_atk is not None and u_atk != db_atk:
                diffs.append((tid, 'ATK', 'DB=%s UNITY=%s' % (db_atk, u_atk)))
        else:
            if db_atk not in (None,) and u_atk != 0:
                diffs.append((tid, 'ATK(non-creature)', 'DB=%s UNITY=%s' % (db_atk, u_atk)))
        # 生物
        db_ct = '1' if dbrow['ctype'] == 'c' else '0'
        if u['cardType'] != db_ct:
            diffs.append((tid, '生物/cardType', 'DB=%s UNITY=%s' % (db_ct, u['cardType'])))
        # desc
        if norm(u['cardDesc']) != norm(dbrow['desc']):
            diffs.append((tid, 'desc', 'DB=%r UNITY=%r' % (dbrow['desc'], strip_markup(u['cardDesc']))))

    print('=== DIFFS (%d) ===' % len(diffs))
    for tid, kind, detail in diffs:
        print('%-28s %-18s %s' % (tid, kind, detail))


if __name__ == '__main__':
    main()
