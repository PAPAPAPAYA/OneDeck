# -*- coding: utf-8 -*-
"""Generate The Bazaar Karnok pool analysis HTML (StS2-series style) — Mobalytics source."""
import json, re, os
from bazaar_bridge import bridge_report, has_tag, has_word, tag_or_word, render_builds

SNAP = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'tools', 'outputs', 'bazaar', 'mobalytics_static_2026-08-31.json')
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'docs', 'Bazaar_Karnok_PoolAnalysis_2026-08-31.html')

def splitlist(v):
    if not v:
        return []
    return [x.strip() for x in v.split(',') if x.strip()]

def is_template_item(i):
    dsc = []
    dsc += i.get('descriptions') or []
    for t in i.get('tierStats') or []:
        dsc += t.get('descriptions') or []
    return any(('Template' in s) for s in dsc)

def desc_of(i, tier_idx=0):
    """Get cleaned descriptions at given tier index (default base)."""
    dsc = []
    ts = i.get('tierStats') or []
    if ts and tier_idx < len(ts):
        dsc += ts[tier_idx].get('descriptions') or []
    dsc += i.get('descriptions') or []
    # strip color/markup templates: {{::X:color.(...)}} and stray > separators
    out = []
    for s in dsc:
        s = re.sub(r'\{\{::([^:}]+)(:[^}]*)?\}\}', r'\1', s)
        s = re.sub(r'\{\{[^}]*\}\}', '', s)
        s = re.sub(r'\s*>\s*', '', s)
        s = re.sub(r'\s+', ' ', s).strip()
        if s:
            out.append(s)
    return out

def desc_flat(i):
    """All descriptions across tiers, joined (for keyword search)."""
    return ' '.join(desc_of(i))

def tier_of(i):
    return i.get('baseTier') or 'Unknown'

def clean_num(v):
    """Clean numeric-ish field values like '{{::8:d,color.(#f5503d)}}' or '1/2/3'."""
    if v is None:
        return None
    s = str(v)
    # extract digit tokens while preserving / separators; e.g. "{{::2:d,color.(#e4b60e)}} > 4" -> "2 > 4"
    s = re.sub(r'\{\{::([^:}]+)(:[^}]*)?\}\}', r'\1', s)
    s = re.sub(r'[^0-9./\s>]', '', s)
    s = re.sub(r'\s+', ' ', s).strip()
    s = s.strip('/')
    if not s:
        return None
    # normalize "2 > 4" -> "2/4" style? keep as-is but trim spaces around >
    s = re.sub(r'\s*>\s*', '>', s)
    s = s.replace('>', '/')
    return s

def cd_of(i, tier_idx=0):
    ts = i.get('tierStats') or []
    if ts and tier_idx < len(ts) and ts[tier_idx].get('cooldown'):
        return clean_num(ts[tier_idx]['cooldown'])
    return clean_num(i.get('cooldown')) or ''

def ammo_of(i):
    # tierStats[i] holds clean per-tier numeric values; top-level ammo may be dirty template string
    ts = i.get('tierStats') or []
    if ts:
        for t in ts:
            if t.get('ammo') is not None:
                return clean_num(t['ammo'])
    return clean_num(i.get('ammo'))

def esc(s):
    return s.replace('&', '&amp;').replace('<', '&lt;').replace('>', '&gt;')

CSS = '''
:root {
--bg: #14161c; --card: #1d2129; --card2: #232834; --text: #c8ccd4; --dim: #8b93a3;
--accent: #7aa2f7; --warn: #e0af68; --good: #9ece6a; --bad: #f7768e; --line: #2e3442;
--purple: #bb9af7; --cyan: #7dcfff;
}
* { box-sizing: border-box; margin: 0; padding: 0; }
body {
background: var(--bg); color: var(--text);
font-family: "Segoe UI", "Microsoft YaHei", system-ui, sans-serif;
line-height: 1.6; padding: 24px; max-width: 1380px; margin: 0 auto;
}
h1 { font-size: 1.5rem; margin-bottom: 4px; color: #e6e9ef; }
h2 { font-size: 1.12rem; margin: 30px 0 12px; color: #e6e9ef; border-left: 3px solid var(--accent); padding-left: 10px; }
h3 { font-size: 1rem; margin: 20px 0 8px; color: #dfe3ea; }
h4 { font-size: 0.92rem; margin: 14px 0 6px; color: #dfe3ea; }
.sub { color: var(--dim); font-size: 0.85rem; margin-bottom: 20px; }
.kpis { display: flex; gap: 14px; flex-wrap: wrap; margin: 18px 0; }
.kpi { background: var(--card); border: 1px solid var(--line); border-radius: 10px; padding: 14px 18px; flex: 1 1 170px; }
.kpi .num { font-size: 1.5rem; font-weight: 700; font-family: Consolas, monospace; color: #e6e9ef; }
.kpi .lbl { font-size: 0.78rem; color: var(--dim); margin-top: 2px; }
.card { background: var(--card); border: 1px solid var(--line); border-radius: 10px; padding: 16px 18px; margin: 14px 0; overflow-x: auto; }
table { border-collapse: collapse; width: 100%; font-size: 0.84rem; }
th, td { padding: 7px 10px; border-bottom: 1px solid var(--line); vertical-align: top; }
th { color: var(--dim); font-weight: 600; background: var(--card2); position: sticky; top: 0; text-align: left; white-space: nowrap; }
.mono { font-family: Consolas, monospace; }
.note { font-size: 0.8rem; color: var(--dim); margin-top: 10px; }
.badge { display: inline-block; font-size: 0.7rem; padding: 1px 8px; border-radius: 10px; border: 1px solid; margin: 0 2px 2px 0; white-space: nowrap; }
.t-bronze { color: #c98a5e; border-color: #c98a5e88; }
.t-silver { color: #aeb5c2; border-color: #aeb5c288; }
.t-gold { color: #e0af68; border-color: #e0af6888; }
.t-diamond { color: #7dcfff; border-color: #7dcfff88; }
.b-weap { color: #f7768e; border-color: #f7768e88; }
.b-tool { color: #e0af68; border-color: #e0af6888; }
.b-props { color: #bb9af7; border-color: #bb9af788; }
.b-aqua { color: #7dcfff; border-color: #7dcfff88; }
.b-friend { color: #9ece6a; border-color: #9ece6a88; }
.b-veh { color: #ff9e64; border-color: #ff9e6488; }
.star { color: #e0af68; font-weight: 700; }
ul, ol { margin: 6px 0 6px 22px; font-size: 0.88rem; }
li { margin: 3px 0; }
.lead { font-size: 0.92rem; margin: 8px 0; }
.dim { color: var(--dim); }
.warn { color: var(--warn); }
.good { color: var(--good); }
.bad { color: var(--bad); }
.purple { color: var(--purple); }
.cyan { color: var(--cyan); }
.verdict { border-left: 3px solid var(--warn); padding: 10px 14px; margin: 10px 0; background: #1a1e27; font-size: 0.88rem; }
.flow { background: #1a1e27; border: 1px solid var(--accent); border-radius: 10px; padding: 12px 16px; margin: 12px 0; font-family: Consolas, monospace; font-size: 0.88rem; color: #dfe3ea; }
@media print { body { background: #fff; color: #222; } .card { border-color: #ccc; } }
'''

def main():
    data = json.load(open(SNAP, encoding='utf-8'))
    all_items = data['items']
    pool = [i for i in all_items if 'Karnok' in i['heroes'] and not is_template_item(i)]
    N = len(pool)

    def lookup(name):
        return next(x for x in pool if x['name'] == name)

    def fx_short(x):
        return ' '.join(desc_of(x))[:130]

    def cd_ammo(x):
        cd = str(cd_of(x)) or '—'
        am = ammo_of(x)
        return cd + (f' / {am}发' if am else '')

    from collections import Counter
    tier_counts = Counter(tier_of(x) for x in pool)

    def mem(x, kw):
        return kw in (x.get('tags') or []) or kw.lower() in desc_flat(x).lower()

    mech = {}
    for kw in ['Weapon', 'Tool', 'Property', 'Toy', 'Relic', 'Apparel', 'Vehicle', 'Friend', 'Core', 'Food', 'Tech', 'Trap', 'Instrument', 'Aquatic', 'Potion', 'Food']:
        mech[kw] = sum(1 for x in pool if kw in (x.get('tags') or []))
    for kw in ['Rage', 'Enrage', 'Charge', 'Haste', 'Slow', 'Burn', 'Freeze', 'Poison', 'Shield', 'Heal', 'Crit', 'Multicast', 'Max Health', 'Flying']:
        mech[kw] = sum(1 for x in pool if mem(x, kw))

    def cd_band(v):
        if v is None or v == '':
            return 'passive'
        v = float(str(v).split('/')[0])
        if v <= 3: return '≤3s'
        if v <= 6: return '4-6s'
        if v <= 9: return '7-9s'
        return '10s+'
    band_counts = Counter(cd_band(cd_of(x)) for x in pool)
    multi = [x for x in pool if len({str(t.get('cooldown')) for t in x.get('tierStats') or [] if t.get('cooldown')}) > 1]

    rows = []
    rows.append('<html><head><meta charset="utf-8"><title>The Bazaar · Karnok 物品池结构拆解</title><style>' + CSS + '</style></head><body>')
    rows.append('<h1>The Bazaar · Karnok 物品池结构拆解</h1>\n<div class="sub">2026-08-31 · 数据源:<a href="https://mobalytics.gg/the-bazaar/karnok-items">mobalytics.gg</a> TheBazaarStaticDataQuery(persistedQuery, cloudflareCacheVersion v1.0.59,快照 2026-08-31)· 全池 1207 物品中 Karnok 专属 118 件(已过滤 1 件模板占位)· 物品英文为准 · 同系列:Vanessa / Pygmalien / Dooley / Mak / Jules / Stelle / The Dragons / 公共池 / 技能池 / 综合</div>')

    rows.append('<div class="kpis">')
    rows.append(f'<div class="kpi"><div class="num">{N}</div><div class="lbl">物品池总数(118 件)</div></div>')
    rows.append(f'<div class="kpi"><div class="num">{tier_counts.get("Bronze",0)}/{tier_counts.get("Silver",0)}/{tier_counts.get("Gold",0)}/{tier_counts.get("Diamond",0)}</div><div class="lbl">tier 分布 B/S/G/D(+{tier_counts.get("Legendary",0)} Legendary)</div></div>')
    rows.append(f'<div class="kpi"><div class="num">{mech.get("Rage",0)}</div><div class="lbl">Rage 相关({round(mech.get("Rage",0)/N*100)}%)—— 身份关键词</div></div>')
    rows.append(f'<div class="kpi"><div class="num">{mech.get("Enrage",0)}</div><div class="lbl">Enrage 相关({round(mech.get("Enrage",0)/N*100)}%)—— 狂暴兑现语汇</div></div>')
    rows.append('<div class="kpi"><div class="num">2+3</div><div class="lbl">主轴:Rage→Enrage 双态引擎 / 皮甲 / 野兽随从 + 捕猎控制 / 陷阱 / 最大生命</div></div>')
    rows.append(f'<div class="kpi"><div class="num">{band_counts.get("passive",0)}</div><div class="lbl">无冷却被动件({round(band_counts.get("passive",0)/N*100)}%)</div></div>')
    rows.append('</div>')

    rows.append('''<h2>0. 英雄骨架</h2>
<div class="card">
<div class="lead"><strong>定位</strong>:全游「猎人/狂暴战士」英雄——核心机制为「Rage→Enrage 双态引擎」:物品使用累积 Rage(68 件涉及),达到阈值触发 Enrage(54 件兑现),狂暴期间有强化/冷却缩减,狂暴结束触发「停怒」效果。身份轴:Rage 68 + Enrage 54 + Apparel 26 + Friend 22 + Slow 25。</div>
<div class="lead"><strong>身份关键词(Top)</strong>:''' + ' '.join(f'<span class="badge b-weap">Rage {mech["Rage"]}</span><span class="badge">Enrage {mech["Enrage"]}</span><span class="badge b-tool">Apparel {mech["Apparel"]}</span><span class="badge b-friend">Friend {mech["Friend"]}</span><span class="badge">Slow {mech["Slow"]}</span><span class="badge b-weap">Weapon {mech["Weapon"]}</span>') + '</div>')
    rows.append('<div class="lead"><strong>资源轴速览</strong>:Rage = 狂暴能量(使用物品/暴击/治疗/慢/烧累积);Enrage = 狂暴状态(达成阈值触发,持续期间强化 + 冷却缩减);停怒 = 狂暴结束触发第二波效果(Firefly Lantern / Frog Hollow / Tent);Apparel = 皮甲(26 件,野兽/猎人主题);Friend = 野兽随从(22 件);Slow = 捕猎控制。Karnok 的语法 =「愤怒循环」——越打越狂,狂完再爆一波。</div>')
    rows.append('</div>')

    rows.append('''<h2>1. 池组成总览</h2>
<div class="card">
<h3>1.1 tier × size</h3>
<table>
<tr><th>Tier</th><th>数量</th><th>占比</th><th>Small</th><th>Medium</th><th>Large</th><th>定位</th></tr>''')
    size_by_tier = {}
    for x in pool:
        size_by_tier.setdefault(tier_of(x), Counter())[x.get('size')] += 1
    desc = {'Bronze': '入门密度层——武器/皮甲/随从为主,教学层', 'Silver': '主力层——Rage/Enrage 引擎件密集', 'Gold': '强度层——狂暴兑现与野兽', 'Diamond': '封顶件层'}
    for t in ['Bronze', 'Silver', 'Gold', 'Diamond']:
        n = tier_counts.get(t, 0)
        if n == 0:
            continue
        sd = size_by_tier.get(t, {})
        rows.append(f'<tr><td><span class="badge t-{t.lower()}">{t}</span></td><td class="mono">{n}</td><td class="mono">{round(n/N*100)}%</td><td class="mono">{sd.get("Small",0)}</td><td class="mono">{sd.get("Medium",0)}</td><td class="mono">{sd.get("Large",0)}</td><td>{desc.get(t,"")}</td></tr>')
    rows.append('</table></div>')

    rows.append('''<div class="card">
<h3>1.2 类型标签分布(多标签重复计入,基数 %s)</h3>
<table>
<tr><th>类型</th><th>数量</th><th>占比</th><th>代表件</th></tr>''' % N)
    typerep = {'Weapon': 'Battle Axe / Hunting Rifle / Tree Club', 'Apparel': 'Bear Mask / Boar Mask / Leather Jacket', 'Tool': 'Bandoleer / Campfire / Lifting Stones', 'Friend': 'Wolf / Wild Bear / Honey Badger', 'Relic': 'Runed Amulet / Ancient Locket / Beast Tooth', 'Property': 'Fairy Circle / Fogshroom', 'Food': 'Healing Draught / Yarrow Paste', 'Potion': '—', 'Trap': 'Bear Trap / Log Trap / Trapping Pit', 'Vehicle': '—', 'Aquatic': '—', 'Instrument': '—'}
    badge = {'Weapon': 'b-weap', 'Tool': 'b-tool', 'Property': 'b-props', 'Friend': 'b-friend', 'Vehicle': 'b-veh'}
    for t in ['Weapon', 'Apparel', 'Tool', 'Friend', 'Relic', 'Property', 'Food', 'Potion', 'Trap', 'Vehicle', 'Aquatic', 'Instrument']:
        c = mech.get(t, 0)
        rows.append(f'<tr><td><span class="badge {badge.get(t, "t-silver")}">{t}</span></td><td class="mono">{c}</td><td class="mono">{round(c/N*100)}%</td><td>{typerep.get(t,"")}</td></tr>')
    rows.append('''</table>
<div class="note">本表取 tags 严格标签。Weapon 42(36%)+ Apparel 26(22%) 是两大构成块;Friend 22 是「野兽随从」底盘;Trap 4 是捕猎主题小标签。多标签物品重复计入各标签。</div>
</div>''')

    rows.append('''<div class="card">
<h3>1.3 节奏结构(冷却分布,base tier 口径)</h3>
<table>
<tr><th>冷却带</th><th>数量</th><th>占比</th><th>说明</th></tr>''')
    bd = {'passive': 'Passive(无冷却/触发式)', '≤3s': '≤3s', '4-6s': '4-6s', '7-9s': '7-9s', '10s+': '10s+'}
    for b in ['passive', '≤3s', '4-6s', '7-9s', '10s+']:
        n = band_counts.get(b, 0)
        rows.append(f'<tr><td>{bd[b]}</td><td class="mono">{n}</td><td class="mono">{round(n/N*100)}%</td><td>—</td></tr>')
    rows.append(f'<tr><td>多值(升级线,不同 tier 冷却不同)</td><td class="mono">{len(multi)}</td><td class="mono">{round(len(multi)/N*100)}%</td><td>随等级渐快——升级即加速</td></tr>')
    rows.append('''</table>
<div class="note">Karnok 节奏主力 4-9s(50+19=69 件)+ 被动 36 件(31%)。Rage/Enrage 引擎的冷却缩减(Enrage 期间减冷却)是节奏加速器——狂暴期间整条链提速,停怒后回弹。31 件多值冷却(升级即加速)比例高。</div>
</div>''')

    rows.append('''<h2>2. 术语与分级结构</h2>
<div class="card">
<h3>2.1 机制词分布</h3>
<table><tr><th>机制词</th><th>件数</th><th>性质</th></tr>''')
    for k in ['Rage', 'Enrage', 'Apparel', 'Friend', 'Weapon', 'Tool', 'Slow', 'Heal', 'Crit', 'Haste', 'Charge', 'Shield', 'Max Health', 'Burn', 'Multicast', 'Poison', 'Freeze', 'Flying', 'Trap', 'Ammo']:
        rows.append(f'<tr><td class="mono">{k}</td><td class="mono">{mech.get(k,0)}</td><td>—</td></tr>')
    rows.append('''</table>
<div class="note">词表为 tags + descriptions 关键词匹配(多标签重复计入)。Rage(68)+ Enrage(54)是 Karnok 的引擎语汇——「愤怒双态」贯穿全池,是全系列最高单机制密度(58%)。</div>
</div>''')

    rows.append('''<div class="card">
<h3>2.2 触发句式</h3>
<table><tr><th>句式</th><th>件数</th><th>代表</th><th>设计含义</th></tr>''')
    def memc(kw):
        return sum(1 for x in pool if kw.lower() in desc_flat(x).lower())
    for kw in ['gain Rage', 'When you Enrage', 'While you are Enraged', 'When you stop being Enraged', 'When you use a Friend', 'When you Slow']:
        rows.append(f'<tr><td>{kw}</td><td class="mono">{memc(kw)}</td><td>—</td><td>—</td></tr>')
    rows.append('</table></div>')

    rows.append('''<h2>3. 构筑轴识别</h2>
<div class="card">
<h3>3.1 Rage→Enrage 双态引擎轴(身份轴)</h3>
<div class="flow">Rage 获取(用武器/暴击/治疗/慢/烧/敌用件 → +Rage) → 触发 Enrage(阈值) → 狂暴兑现(伤害/冷却缩减/最大生命/Multicast) → 停怒第二波(Firefly Lantern / Frog Hollow / Tent)</div>
<div class="lead"><strong>入口</strong>:Rage 获取件(Eagle Sigil 暴击→Rage、Tranquil Sigil 治疗→Rage、Enervating Sigil 慢→Rage、Vengeful Sigil 敌用→Rage、Bear Mask 首次使用→30 Rage)。<strong>兑现</strong>:Enrage 触发件(Assault Sigil 狂暴→10% 最大生命伤害、Battle Axe 狂暴+60 伤、Great Eagle 狂暴→3 件起飞)。<strong>封顶</strong>:Tree Club(1000 伤,狂暴期间自充)、Outlands Terror(狂暴期间吸生+半冷却+双倍 Rage 获取)、Wild Bear(狂暴+20% 最大生命)。</div>
<div class="lead"><strong>停怒态</strong>:Firefly Lantern(停怒→使用)、Frog Hollow(停怒→全体 Haste)、Tent(停怒→20% 最大生命盾)——「愤怒循环」的第二波,是 Karnok 独有节奏。</div>
</div>
<div class="card">
<h3>3.2 皮甲/防御轴(身份标签)</h3>
<div class="flow">Apparel 26 件 → 最大生命(Forest Cloak +20% 最大生命、狂暴期间半伤)→ 防护(Masks 首次使用→Rage + 狂暴 Max Health)</div>
<div class="lead"><strong>密度</strong>:Apparel 26(22%)——皮甲是 Karnok 的「耐久」标签,几乎都挂 Rage/Enrage 之一(如 Bear Mask 首次使用+30 Rage、狂暴+10% 最大生命)。</div>
</div>
<div class="card">
<h3>3.3 野兽随从轴</h3>
<div class="flow">Friend 22 件(Wolf / Wild Bear / Honey Badger / Great Eagle / Outlands Terror)→ 狂暴期间友军强化(Wolf 狂暴→Haste 友军 / Karst 用友军→Rage+盾)</div>
<div class="lead"><strong>特征</strong>:野兽随从与 Rage 深度绑定——友军既是 Rage 来源(Karst)又是狂暴兑现目标(Wolf)。</div>
</div>
<div class="card">
<h3>3.4 捕猎/慢控轴(次轴)</h3>
<div class="lead"><strong>特征</strong>:Slow 25 + Trap 4 —— 猎人主题的「布陷阱 + 减速猎物」控制(Log Trap 首次敌用→500 伤 + 全慢),Trap 在狂暴后可重复触发。</div>
</div>
<div class="card">
<h3>3.5 轴矩阵总结</h3>
<table>
<tr><th>轴</th><th>件数</th><th>tier 分布</th><th>入口/兑现/封顶</th><th>密度评价</th></tr>''')
    karnok_axes = {
        '狂暴': lambda i: has_word(i, r'Rage|Enrage'),
        '皮甲': lambda i: has_tag(i, 'Apparel'),
        '野兽': lambda i: has_tag(i, 'Friend'),
        '慢控': lambda i: has_word(i, r'Slow'),
        '输出': lambda i: has_tag(i, 'Weapon') or has_word(i, r'Deal \d|Damage'),
        '耐久': lambda i: has_word(i, r'Max Health|Shield'),
        '陷阱': lambda i: has_tag(i, 'Trap'),
    }
    eco = [x for x in pool if karnok_axes['狂暴'](x)]
    swing = [x for x in pool if karnok_axes['皮甲'](x)]
    toyprop = [x for x in pool if karnok_axes['野兽'](x)]
    relic = [x for x in pool if karnok_axes['慢控'](x)]
    combat = [x for x in pool if karnok_axes['输出'](x)]
    regen = [x for x in pool if karnok_axes['耐久'](x)]
    trap = [x for x in pool if karnok_axes['陷阱'](x)]
    def tierdist(lst):
        c = Counter(tier_of(x) for x in lst)
        return f'B{c.get("Bronze",0)}/S{c.get("Silver",0)}/G{c.get("Gold",0)}/D{c.get("Diamond",0)}+{c.get("Legendary",0)}L'
    rows.append('<tr><td>狂暴双态</td><td class="mono">%d</td><td>%s</td><td>Rage 获取 → Enrage 兑现 → 停怒二波</td><td class="good">厚,身份轴(58%%)</td></tr>' % (len(eco), tierdist(eco)))
    rows.append('<tr><td>皮甲</td><td class="mono">%d</td><td>%s</td><td>耐久 + 狂暴联动</td><td class="good">厚,身份标签</td></tr>' % (len(swing), tierdist(swing)))
    rows.append('<tr><td>野兽</td><td class="mono">%d</td><td>%s</td><td>随从 + Rage 来源/兑现</td><td class="warn">中</td></tr>' % (len(toyprop), tierdist(toyprop)))
    rows.append('<tr><td>慢控</td><td class="mono">%d</td><td>%s</td><td>减速 + Rage 获取</td><td class="warn">中</td></tr>' % (len(relic), tierdist(relic)))
    rows.append('<tr><td>输出</td><td class="mono">%d</td><td>%s</td><td>武器 + 狂暴放大</td><td class="warn">中</td></tr>' % (len(combat), tierdist(combat)))
    rows.append('<tr><td>耐久</td><td class="mono">%d</td><td>%s</td><td>最大生命/盾乘区</td><td class="warn">中</td></tr>' % (len(regen), tierdist(regen)))
    rows.append('<tr><td>陷阱</td><td class="mono">%d</td><td>%s</td><td>布陷阱 + 慢</td><td class="bad">小轴</td></tr>' % (len(trap), tierdist(trap)))
    rows.append('</table></div>')

    # ===== 3.6 bridge matrix (2026-08-31 framework) =====
    bridge_html, bridge_stats = bridge_report(
        pool, karnok_axes,
        closed_loop_desc='Rage 入口(Bear Mask 首次使用+30 Rage) → 狂暴×输出(Assault Sigil 狂暴→10% 最大生命伤害) → 狂暴×皮甲(Bear Mask 狂暴+10% 最大生命) → 狂暴×野兽(Karst 用友军→Rage+盾) → 停怒二波(Firefly Lantern 停怒→再使用):狂暴是绝对中心,皮甲/野兽/慢控/输出全部从狂暴发散——全池最高单轴密度(58%)。'
    )
    rows.append(bridge_html)

    rows.append('''<h2>4. 两段式审计(条件制造 → 兑现)</h2>
<div class="card">
<table>
<tr><th>层</th><th>设计职责</th><th>代表件</th><th>tier 观察</th></tr>
<tr><td><strong>入口层</strong>(制造条件/供给)</td><td>Rage 获取(用武器/暴击/治疗/慢/烧)</td><td>Eagle Sigil / Tranquil Sigil / Enervating Sigil / Bear Mask</td><td>B/S 为主</td></tr>
<tr><td><strong>兑现层</strong>(消费条件)</td><td>Enrage 触发(伤害/冷却缩减/最大生命)</td><td>Assault Sigil / Battle Axe / Great Eagle / Wild Bear</td><td>S/G 为主</td></tr>
<tr><td><strong>封顶层</strong>(条件 → 乘区)</td><td>狂暴期自充/吸生/双倍 Rage、最大生命乘区</td><td>Tree Club / Outlands Terror / Wild Bear / Forest Cloak</td><td>S/G 集中</td></tr>
</table>
<div class="verdict"><strong>结论</strong>:Karnok 的两段式是「循环式」:入口件累积 Rage(条件),兑现件触发 Enrage(消费条件),停怒件再触发第二波——「愤怒循环」是双段式的自持变体。Rage/Enrage 接口词汇是 Karnok 的最小语法单位,其「条件→双态兑现→第二波」结构对 OneDeck 的「条件→兑现→事件链」有直接对照价值(见 §6)。</div>
</div>''')

    rows.append('''<h2>5. 强度阶梯(高 tier = 高强度?)</h2>
<div class="card">
<h3>5.1 Gold(21)与 Diamond(0)</h3>
<table>
<tr><th>件</th><th>tier</th><th>size</th><th>强度认定</th></tr>''')
    for x in sorted((x for x in pool if tier_of(x) in ('Gold', 'Diamond')), key=lambda z: tier_of(z)):
        rows.append(f'<tr><td>{x["name"]}</td><td><span class="badge t-{tier_of(x).lower()}">{tier_of(x)}</span></td><td>{x.get("size")}</td><td>{esc(" ".join(desc_of(x))[:110])}</td></tr>')
    rows.append('''</table>
<div class="verdict"><strong>结论</strong>:高 tier ≠ 必然强——Karnok 无 Diamond(0 件),Gold 21 件承载狂暴兑现件(Tree Club / Outlands Terror / Wild Bear / Great Eagle)与陷阱(Log Trap)。tier 表达「获取难度 + 狂暴放大潜力」,Gold 是「愤怒兑现」层。</div>
</div>''')

    # ===== 5.5 typical builds (2026-09-01 framework, v2 detailed) =====
    skills = data['skills']
    karnok_builds = [
        dict(name='Karst Enrage 狂暴循环流', source='<a href="https://mobalytics.gg/the-bazaar/builds/karst-enrage-karnok-kripp">Mobalytics / Kripp</a>', date='2026-03-16', grade='<span class="badge t-gold">狂暴刷取型</span>',
             logic='不追求「待在狂暴里」,而是反复进出狂暴——Karst 把狂暴时长减半,从而高频触发全板的「When you Enrage」效果;Firefly Lantern + Warpaint 构成无限循环,Stretch Pants/Honey Badger/Tinderbox 是常见开局线。构筑非常灵活,早期成型一致性高。',
             items=['Karst', 'Firefly Lantern', 'Warpaint', 'Frog Hollow', 'Stretch Pants', 'Unibou', 'Dryad', 'Tinderbox', 'Torch', 'Snow Wisp'],
             skills=[],
             note='Warpaint 为 Karnok 物品(非技能);Stretch Pants(防御)配 Unibou 把 Multicast 转盾增长,Dryad 提供快充;Tinderbox + Firefly Lantern 是对标 Pygmalien Matchbox 线的烧灼开局,起始附魔命中其一即加强。'),
        dict(name='Rage Haste Friend 野兽速攻', source='<a href="https://bazaar-builds.net/rage-haste-friend-karnok-10-win-build-seanx/">bazaar-builds.net / seanX</a>', date='2026-03-05', grade='<span class="badge t-diamond">10 胜实证</span>',
             logic='全友军板——野兽互相充能/Haste 叠狂暴增益,以高频使用刷 Rage 与「When you Enrage」全板效果;6 件全 Small/Medium 保证启动速度。',
             items=['Beast Tooth', 'Honey Badger', 'Hunting Hawk', 'Messenger Sparrow', 'Spear', 'Wolf'],
             skills=[],
             note='社区提交的 10 胜实战板(带胜场截图验证);与 §3.3 野兽轴的「友军既是 Rage 来源又是狂暴兑现」判读一致。'),
    ]
    rows.append(render_builds('Karnok', pool, data['items'], skills, karnok_axes, karnok_builds,
        source_note='来源:Mobalytics Builds(Kripparrian)与 bazaar-builds.net(社区 10 胜实证);物品效果取自 Mobalytics 快照(2026-08-31,cloudflareCacheVersion v1.0.59),攻略日期即 meta 快照,跨补丁数值仅作结构参考;轴映射按 §3.6 谓词自动计算。thebazaarzone 暂无 Karnok 攻略页(DLC 新英雄)。'))

    rows.append('''<h2>6. 与 StS2 / OneDeck 的映射</h2>
<div class="card">
<h3>6.1 与 StS2 的结构异同</h3>
<table><tr><th>维度</th><th>StS2(五角色)</th><th>Bazaar(Karnok)</th><th>差异判定</th></tr>
<tr><td>入口层</td><td>Common 无能力卡,即打即用</td><td>Bronze Rage 获取件(暴击/治疗/慢→Rage)</td><td>同工:入口=制造条件</td></tr>
<tr><td>兑现层</td><td>Uncommon 桥层</td><td>Silver Enrage 兑现件密集</td><td>同工:桥在中间 tier</td></tr>
<tr><td>封顶</td><td>Rare 大数字/引擎</td><td>Gold 狂暴放大(Tree Club 1000 伤 / Outlands Terror 吸生)</td><td>同工:高 tier 承载放大</td></tr>
<tr><td>资源经济</td><td>垃圾经济学四形态</td><td>Rage 双态循环(累积→兑现→停怒二波)</td><td>同类,「愤怒循环」= StS2 的「力量/易伤」成长循环</td></tr>
<tr><td>运行</td><td>回合制/能量</td><td>实时秒表/冷却/空间</td><td>差异最大</td></tr>
</table>
<h3>6.2 OneDeck 落点(初筛)</h3>
<table><tr><th>Bazaar 观察</th><th>OneDeck 可借鉴</th><th>不可搬</th></tr>
<tr><td>Rage→Enrage→停怒 双态循环</td><td><b>OneDeck 的「条件→兑现→事件链」可对照其双态设计</b>——OneDeck 的力量/攻击力成长是单向,「狂暴二波」式的第二兑现是可选扩展</td><td>秒表冷却不映射轮次制</td></tr>
<tr><td>停怒第二波(Firefly Lantern / Tent)</td><td>OneDeck 的「状态结束触发」句式(如苏醒/诅咒结束)可参考「双段兑现」</td><td>—</td></tr>
<tr><td>最大生命乘区(Wild Bear / Forest Cloak)</td><td>OneDeck 的 HP 成长类机制(生命系统)可对照「最大生命×伤害」</td><td>—</td></tr>
<tr><td>皮甲=耐久+狂暴联动(26 件)</td><td>OneDeck 的「防御+条件联动」句式可参考</td><td>—</td></tr>
</table>
</div>''')

    rows.append('''<h2>7. 关键卡清单</h2>
<div class="card"><table>
<tr><th>轴</th><th>物品</th><th>tier</th><th>size</th><th>cd/弹药</th><th>effects 摘要</th></tr>''')
    keycards = [
        ('狂暴', 'Bear Mask', 't-silver'), ('狂暴', 'Eagle Sigil', 't-silver'), ('狂暴', 'Tranquil Sigil', 't-silver'),
        ('狂暴', 'Enervating Sigil', 't-silver'), ('狂暴', 'Vengeful Sigil', 't-silver'), ('狂暴', 'Karst', 't-silver'),
        ('兑现', 'Assault Sigil', 't-gold'), ('兑现', 'Battle Axe', 't-bronze'), ('兑现', 'Great Eagle', 't-gold'),
        ('兑现', 'Wild Bear', 't-gold'), ('兑现', 'Outlands Terror', 't-silver'), ('兑现', 'Tree Club', 't-gold'),
        ('停怒', 'Firefly Lantern', 't-bronze'), ('停怒', 'Frog Hollow', 't-silver'), ('停怒', 'Tent', 't-silver'),
        ('皮甲', 'Forest Cloak', 't-silver'), ('皮甲', 'Leather Jacket', 't-gold'), ('皮甲', 'Wolf Mask', 't-silver'),
        ('野兽', 'Wolf', 't-silver'), ('野兽', 'Honey Badger', 't-bronze'),
        ('陷阱', 'Log Trap', 't-gold'), ('陷阱', 'Bear Trap', 't-bronze'),
    ]
    for axis, name, tk in keycards:
        try:
            x = lookup(name)
        except StopIteration:
            rows.append(f'<tr><td>{axis}</td><td>{name}</td><td colspan="4">(Mobalytics 无此条目)</td></tr>')
            continue
        rows.append(f'<tr><td>{axis}</td><td>{name}</td><td><span class="badge {tk}">{tier_of(x)}</span></td><td class="mono">{x.get("size")}</td><td class="mono">{cd_ammo(x)}</td><td>{esc(fx_short(x))}</td></tr>')
    rows.append('</table></div>')

    rows.append('</table></div>')

    rows.append('''<h2>8. 文档元信息</h2>
<div class="card">
<table>
<tr><th>项</th><th>值</th></tr>
<tr><td>数据快照</td><td class="mono">2026-08-31 mobalytics.gg TheBazaarStaticDataQuery(persistedQuery, cloudflareCacheVersion v1.0.59),全池 1207 件提取 Karnok 专属 118</td></tr>
<tr><td>归属规则</td><td>heroes[] 含 Karnok 且非模板占位(description 含 Template 的 1 件已滤)</td></tr>
<tr><td>字段说明</td><td>tierStats 为四级数值;cooldown/ammo/critchance/multicast 底层字段化;descriptions 含配色模板标记已清洗</td></tr>
<tr><td>已知缺口</td><td>附魔(enchantments)已随 Mobalytics 全量收录,综合文档定量</td></tr>
<tr><td>本系列</td><td>Vanessa / Dooley / Mak / Karnok / Jules / Stelle / The Dragons / 公共池 / 技能池 / 综合(逐个完成即停,step-gate)</td></tr>
</table>
<div class="note">计数口径:机制词按 tags + descriptions 关键词匹配,多标签重复计入;tier/size 按 Mobalytics 字段原值。</div>
</div>''')
    rows.append('</body></html>')

    html = '\n'.join(rows)
    with open(OUT, 'w', encoding='utf-8', newline='\r\n') as f:
        f.write(html)
    print('written', len(html), 'chars ->', OUT)

if __name__ == '__main__':
    main()

