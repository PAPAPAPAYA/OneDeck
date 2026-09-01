# -*- coding: utf-8 -*-
"""Generate The Bazaar Stelle pool analysis HTML (StS2-series style) — Mobalytics source."""
import json, re, os
from bazaar_bridge import bridge_report, has_tag, has_word, tag_or_word, render_builds

SNAP = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'tools', 'outputs', 'bazaar', 'mobalytics_static_2026-08-31.json')
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'docs', 'Bazaar_Stelle_PoolAnalysis_2026-08-31.html')

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
    pool = [i for i in all_items if 'Stelle' in i['heroes'] and not is_template_item(i)]
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
    for kw in ['Weapon', 'Tool', 'Property', 'Toy', 'Relic', 'Apparel', 'Vehicle', 'Friend', 'Core', 'Food', 'Tech', 'Trap', 'Instrument', 'Aquatic', 'Drone', 'Ray']:
        mech[kw] = sum(1 for x in pool if kw in (x.get('tags') or []))
    for kw in ['Flying', 'Freeze', 'Charge', 'Haste', 'Slow', 'Burn', 'Poison', 'Shield', 'Heal', 'Regen', 'Crit', 'Multicast', 'Quest', 'Destroy', 'Repair']:
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
    rows.append('<html><head><meta charset="utf-8"><title>The Bazaar · Stelle 物品池结构拆解</title><style>' + CSS + '</style></head><body>')
    rows.append('<h1>The Bazaar · Stelle 物品池结构拆解</h1>\n<div class="sub">2026-08-31 · 数据源:<a href="https://mobalytics.gg/the-bazaar/stelle-items">mobalytics.gg</a> TheBazaarStaticDataQuery(persistedQuery, cloudflareCacheVersion v1.0.59,快照 2026-08-31)· 全池 1207 物品中 Stelle 专属 121 件(已过滤 1 件模板占位)· 物品英文为准 · 同系列:Vanessa / Pygmalien / Dooley / Mak / Karnok / Jules / The Dragons / 公共池 / 技能池 / 综合</div>')

    rows.append('<div class="kpis">')
    rows.append(f'<div class="kpi"><div class="num">{N}</div><div class="lbl">物品池总数(121 件)</div></div>')
    rows.append(f'<div class="kpi"><div class="num">{tier_counts.get("Bronze",0)}/{tier_counts.get("Silver",0)}/{tier_counts.get("Gold",0)}/{tier_counts.get("Diamond",0)}</div><div class="lbl">tier 分布 B/S/G/D(无 Diamond)</div></div>')
    rows.append(f'<div class="kpi"><div class="num">{mech.get("Flying",0)}</div><div class="lbl">Flying 相关({round(mech.get("Flying",0)/N*100)}%)—— 身份关键词</div></div>')
    rows.append(f'<div class="kpi"><div class="num">{mech.get("Vehicle",0)}/{mech.get("Drone",0)}</div><div class="lbl">Vehicle {mech.get("Vehicle",0)} / Drone {mech.get("Drone",0)} —— 载具/无人机标签</div></div>')
    rows.append('<div class="kpi"><div class="num">2+3</div><div class="lbl">主轴:起降循环 / 自毁爆发 + 载具无人机 / 灼烧 / 慢控</div></div>')
    rows.append(f'<div class="kpi"><div class="num">{band_counts.get("passive",0)}</div><div class="lbl">无冷却被动件({round(band_counts.get("passive",0)/N*100)}%)</div></div>')
    rows.append('</div>')

    rows.append('''<h2>0. 英雄骨架</h2>
<div class="card">
<div class="lead"><strong>定位</strong>:全游「飞行技师」英雄——核心机制为「起降循环」:物品反复 start/stop Flying,两个转换方向各有触发(起飞 +伤/灼烧,降落 装填/自爆);Flying 66 件(55%)是全系列最高标签密度。身份轴:Flying 66 + Vehicle 28 + Drone 15 + Haste 38 + Burn 25。</div>
<div class="lead"><strong>身份关键词(Top)</strong>:''' + ' '.join(f'<span class="badge b-aqua">Flying {mech["Flying"]}</span><span class="badge b-veh">Vehicle {mech["Vehicle"]}</span><span class="badge b-friend">Drone {mech["Drone"]}</span><span class="badge">Haste {mech["Haste"]}</span><span class="badge">Burn {mech["Burn"]}</span><span class="badge">Destroy {mech["Destroy"]}</span>') + '</div>')
    rows.append('<div class="lead"><strong>资源轴速览</strong>:Flying = 双态开关(与 Jules Heated/Chilled 同构但频率更高);起 = Air-Pressure Rifle +25 伤 / Balloon Engine 灼烧;降 = 装填(Bomb Voyage / Buster 自爆 = 自毁轴核心);While Flying = Aerial Turret +1 Multicast。Vehicle/Drone = 载具无人机标签(28/15)。Stelle 的语法 =「起飞—降落—再起飞」的高频循环。</div>')
    rows.append('</div>')

    rows.append('''<h2>1. 池组成总览</h2>
<div class="card">
<h3>1.1 tier × size</h3>
<table>
<tr><th>Tier</th><th>数量</th><th>占比</th><th>Small</th><th>Medium</th><th>Large</th><th>定位</th></tr>''')
    size_by_tier = {}
    for x in pool:
        size_by_tier.setdefault(tier_of(x), Counter())[x.get('size')] += 1
    desc = {'Bronze': '入门密度层——飞行小件/无人机为主,教学层', 'Silver': '主力层——起降引擎件密集', 'Gold': '强度层——自毁爆发与慢控引擎(27 件)', 'Diamond': '无 Diamond(B/S/G 三档)'}
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
    typerep = {'Tool': 'Freefall Simulator / Altimeter / Binoculars', 'Weapon': 'Aerial Turret / Bomb Voyage / Boosted Saucer', 'Vehicle': 'Balloon Engine / Hang Glider / Pillbuggy', 'Tech': 'Headset / Radar Dome / Flashbang', 'Drone': 'Flame Jet Drone / Repair Drone / Relay Drone', 'Property': "Stelle's Workshop / Radar Dome", 'Apparel': 'Rocket Boots / Tethers', 'Toy': 'Clockwork Disc / Paper Airplane / Kite', 'Food': '—', 'Friend': '—', 'Ray': '—', 'Trap': '—'}
    badge = {'Weapon': 'b-weap', 'Tool': 'b-tool', 'Property': 'b-props', 'Friend': 'b-friend', 'Vehicle': 'b-veh'}
    for t in ['Tool', 'Weapon', 'Vehicle', 'Tech', 'Drone', 'Property', 'Apparel', 'Toy', 'Food', 'Friend', 'Ray', 'Trap']:
        c = mech.get(t, 0)
        rows.append(f'<tr><td><span class="badge {badge.get(t, "t-silver")}">{t}</span></td><td class="mono">{c}</td><td class="mono">{round(c/N*100)}%</td><td>{typerep.get(t,"")}</td></tr>')
    rows.append('''</table>
<div class="note">本表取 tags 严格标签。Tool 39 + Weapon 36 是两大构成块;Vehicle 28 + Drone 15 是天空体系标签(Drone 为 Stelle 独有主力标签)。多标签物品重复计入各标签。</div>''')

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
<div class="note">Stelle 节奏由「起飞→触发→降落→触发」的高频循环驱动——双转换方向都有收益(起飞 +伤/灼烧,降落 装填/自爆),Freefall Simulator 等件负责快速起降。27 件 Gold(22%)承载自毁爆发线。</div>
</div>''')

    rows.append('''<h2>2. 术语与分级结构</h2>
<div class="card">
<h3>2.1 机制词分布</h3>
<table><tr><th>机制词</th><th>件数</th><th>性质</th></tr>''')
    for k in ['Flying', 'Tool', 'Vehicle', 'Weapon', 'Haste', 'Burn', 'Charge', 'Shield', 'Slow', 'Destroy', 'Repair', 'Multicast', 'Crit', 'Heal', 'Freeze', 'Regen', 'Poison', 'Quest', 'Flying 增益', 'Drone']:
        rows.append(f'<tr><td class="mono">{k}</td><td class="mono">{mech.get(k,0)}</td><td>—</td></tr>')
    rows.append('''</table>
<div class="note">词表为 tags + descriptions 关键词匹配(多标签重复计入)。Flying(66)是全系列最高单标签密度——「起降」双态贯穿全池;Destroy(自毁)+ Repair(修复)是自毁轴的成对语汇。</div>''')

    rows.append('''<div class="card">
<h3>2.2 触发句式</h3>
<table><tr><th>句式</th><th>件数</th><th>代表</th><th>设计含义</th></tr>''')
    def memc(kw):
        return sum(1 for x in pool if kw.lower() in desc_flat(x).lower())
    for kw in ['start Flying', 'stop Flying', 'While this is Flying', 'When this is destroyed', 'When you Burn', 'When you Slow']:
        rows.append(f'<tr><td>{kw}</td><td class="mono">{memc(kw)}</td><td>—</td><td>—</td></tr>')
    rows.append('</table></div>')

    rows.append('''<h2>3. 构筑轴识别</h2>
<div class="card">
<h3>3.1 起降循环轴(身份轴)</h3>
<div class="flow">起飞触发(Balloon Engine 灼烧 / Air-Pressure Rifle +25 伤) → 状态期(Aerial Turret While Flying +1 Multicast) → 降落触发(装填 / Bird Cage +75 盾 / 自爆件引爆) → Freefall Simulator / Clockwork Disc 循环驱动</div>
<div class="lead"><strong>入口</strong>:起降驱动件(Freefall Simulator 是 Kripp 五篇构筑的公共核心;Clockwork Disc 单槽自循环)。<strong>兑现</strong>:双转换触发。<strong>封顶</strong>:Daggerwing / Stellar Swallowtail(Gold 大件)。</div>
<div class="lead"><strong>结构注</strong>:起降循环与 Jules 的 Heated/Chilled 同构(双态开关),但频率更高、方向更对称——起飞与降落都有收益,这让 Stelle 的引擎「永动机化」。</div>
</div>
<div class="card">
<h3>3.2 自毁爆发轴</h3>
<div class="flow">自爆件(Boom Boom Bot 爆 80 伤 / Bomb Voyage 停飞自毁 ×10 伤 / Buster)→ Destroy 触发充能(Boosted Saucer)→ Repair 循环(Recycler Bot)→ 反复引爆</div>
<div class="lead"><strong>特征</strong>:Destroy 25 + Repair——「引爆→修复→再引爆」的循环;Kripp Self-Destroy 构筑定位 mid-late pivot。</div>
</div>
<div class="card">
<h3>3.3 载具/无人机轴(标签体系)</h3>
<div class="flow">Vehicle 28(Balloon Engine / Hang Glider / Pillbuggy)+ Drone 15(Flame Jet / Repair / Relay Drone)→ 载具起飞联动(Buster:载具/无人机起飞则自己起飞)</div>
<div class="lead"><strong>特征</strong>:Drone 是 Stelle 独有的主力标签——无人机兼具武器/工具功能,与起降轴天然互锁。</div>
</div>
<div class="card">
<h3>3.4 灼烧/慢控副轴</h3>
<div class="lead"><strong>特征</strong>:Burn 25(炽热起飞系)+ Slow 13(Lightning Rod 慢控转伤,攻略类比 Dooley 的 Proboscis)——两个副轴都从起降循环发散。</div>
</div>
<div class="card">
<h3>3.5 轴矩阵总结</h3>
<table>
<tr><th>轴</th><th>件数</th><th>tier 分布</th><th>入口/兑现/封顶</th><th>密度评价</th></tr>''')
    stelle_axes = {
        '飞行': lambda i: has_word(i, r'Flying'),
        '载具': lambda i: has_tag(i, 'Vehicle'),
        '无人机': lambda i: has_tag(i, 'Drone'),
        '自毁': lambda i: has_word(i, r'Destroy|destroy'),
        '灼烧': lambda i: has_word(i, r'Burn'),
        '慢控': lambda i: has_word(i, r'Slow'),
        '输出': lambda i: has_tag(i, 'Weapon') or has_word(i, r'Deal \d|Damage'),
    }
    a1 = [x for x in pool if stelle_axes['飞行'](x)]
    a2 = [x for x in pool if stelle_axes['载具'](x)]
    a3 = [x for x in pool if stelle_axes['无人机'](x)]
    a4 = [x for x in pool if stelle_axes['自毁'](x)]
    a5 = [x for x in pool if stelle_axes['灼烧'](x)]
    a6 = [x for x in pool if stelle_axes['慢控'](x)]
    a7 = [x for x in pool if stelle_axes['输出'](x)]
    def tierdist(lst):
        c = Counter(tier_of(x) for x in lst)
        return f'B{c.get("Bronze",0)}/S{c.get("Silver",0)}/G{c.get("Gold",0)}/D{c.get("Diamond",0)}+{c.get("Legendary",0)}L'
    rows.append('<tr><td>起降循环</td><td class="mono">%d</td><td>%s</td><td>起飞/降落双触发 → 引擎循环</td><td class="good">厚,身份轴(55%%)</td></tr>' % (len(a1), tierdist(a1)))
    rows.append('<tr><td>载具</td><td class="mono">%d</td><td>%s</td><td>天空载具标签</td><td class="good">厚,标签体系</td></tr>' % (len(a2), tierdist(a2)))
    rows.append('<tr><td>无人机</td><td class="mono">%d</td><td>%s</td><td>独有标签,与起降互锁</td><td class="warn">中</td></tr>' % (len(a3), tierdist(a3)))
    rows.append('<tr><td>自毁</td><td class="mono">%d</td><td>%s</td><td>引爆 → Repair 循环</td><td class="warn">中,爆发线</td></tr>' % (len(a4), tierdist(a4)))
    rows.append('<tr><td>灼烧</td><td class="mono">%d</td><td>%s</td><td>起飞灼烧副轴</td><td class="warn">中</td></tr>' % (len(a5), tierdist(a5)))
    rows.append('<tr><td>慢控</td><td class="mono">%d</td><td>%s</td><td>Lightning Rod 转伤</td><td class="bad">小轴</td></tr>' % (len(a6), tierdist(a6)))
    rows.append('<tr><td>输出</td><td class="mono">%d</td><td>%s</td><td>武器输出</td><td class="warn">中</td></tr>' % (len(a7), tierdist(a7)))
    rows.append('</table></div>')

    # ===== 3.6 bridge matrix (2026-08-31 framework) =====
    bridge_html, bridge_stats = bridge_report(
        pool, stelle_axes,
        closed_loop_desc='起降驱动(Freefall Simulator 起飞+降落双向触发) → 飞行×灼烧(Balloon Engine 起飞→灼烧) → 自毁×输出(Boosted Saucer 吃 Destroy 触发充能) → 载具×无人机×飞行(Buster 载具起飞则自机起飞) → 慢控×输出(Lightning Rod 慢转伤):起降循环是物理引擎,所有轴都挂在 start/stop 两个转换点上。'
    )
    rows.append(bridge_html)

    rows.append('''<h2>4. 两段式审计(条件制造 → 兑现)</h2>
<div class="card">
<table>
<tr><th>层</th><th>设计职责</th><th>代表件</th><th>tier 观察</th></tr>
<tr><td><strong>入口层</strong>(制造条件/供给)</td><td>起降驱动、Haste 支撑</td><td>Freefall Simulator / Clockwork Disc / Headset / Ornithopter</td><td>B/S 为主</td></tr>
<tr><td><strong>兑现层</strong>(消费条件)</td><td>起飞/降落触发、自爆引爆</td><td>Balloon Engine / Air-Pressure Rifle / Boom Boom Bot / Bomb Voyage</td><td>B/S 为主</td></tr>
<tr><td><strong>封顶层</strong>(条件 → 乘区)</td><td>自毁×N 转伤害、慢控转伤、While Flying 乘区</td><td>LavaRoller / Lightning Rod / Buster / Daggerwing</td><td>G 集中(无 Diamond)</td></tr>
</table>
<div class="verdict"><strong>结论</strong>:Stelle 的两段式是「往复式」:起/降各是独立条件与兑现(双段对称),自毁轴再叠加「引爆→修复」的第二循环——高频双态让引擎接近自持。Flying 接口词汇是 Stelle 的最小语法单位,与 Karnok(Rage 双态)构成「双态引擎」的两种频率形态(见 §6)。</div>
</div>''')

    rows.append('''<h2>5. 强度阶梯(高 tier = 高强度?)</h2>
<div class="card">
<h3>5.1 Gold(27)与 Diamond(0)</h3>
<table>
<tr><th>件</th><th>tier</th><th>size</th><th>强度认定</th></tr>''')
    for x in sorted((x for x in pool if tier_of(x) in ('Gold', 'Diamond')), key=lambda z: tier_of(z)):
        rows.append(f'<tr><td>{x["name"]}</td><td><span class="badge t-{tier_of(x).lower()}">{tier_of(x)}</span></td><td>{x.get("size")}</td><td>{esc(" ".join(desc_of(x))[:110])}</td></tr>')
    rows.append('''</table>
<div class="verdict"><strong>结论</strong>:高 tier ≠ 必然强——Stelle 无 Diamond,Gold 27 件(22%)承载自毁爆发(Bomb Voyage / Buster / The Big One)与慢控引擎(Lightning Rod)。Kripp 对 LavaRoller 的判读:「Gold 起步 → pivot 构筑」,与 Karnok 相同的「无 Diamond = 后期依赖 pivot」结构。</div>
</div>''')

    # ===== 5.5 typical builds (2026-09-01 framework, v2 detailed) =====
    skills = data['skills']
    stelle_builds = [
        dict(name='Balloon Engine 起飞灼烧', source='<a href="https://mobalytics.gg/the-bazaar/builds/balloon-engine-stelle-kripp">Mobalytics / Kripp</a>', date='2025-12-08', grade='<span class="badge t-gold">Day 1 成型</span>',
             logic=' explosively-fast 灼烧——Balloon Engine + Freefall Simulator 双件核即可运转,最大化 start/stop Flying 触发;Day 1 可组装,但 Day 12 后开始乏力。',
             items=['Balloon Engine', 'Freefall Simulator', 'Flame Jet Drone', 'Clockwork Disc', 'Fire Bomb', 'Headset', 'Tethers'],
             skills=['Final Flame'],
             note='2 级重摇找位置型灼烧技能;Curio 店找 Salamander Pup;Rocket Boots 为后续选择。'),
        dict(name='Start-Stop Flying 起降循环', source='<a href="https://mobalytics.gg/the-bazaar/builds/start-stop-flying-stelle-kripp">Mobalytics / Kripp</a>', date='2026-02-28', grade='<span class="badge t-diamond">身份构筑</span>',
             logic='「Stelle 身份的核心部分」(Kripp 原文)——把 start/stop 触发效率推到极限的万金油引擎;Freefall Simulator 升级优先,Clockwork Disc 单槽自循环配 Gyro Gunsight + Headset 打前期。',
             items=['Freefall Simulator', 'Clockwork Disc', 'Paper Airplane', 'Ornithopter', 'Tethers', 'Gyro Gunsight', 'Daggerwing', 'Hang Glider', 'Pillbuggy'],
             skills=[],
             note='填充件 Angle Grinder Drone / Flycycle / Kite / Pinwheel;规划 Orbital Polisher 时不要过早把物品升过 Silver;Toolbox 为经济件。'),
        dict(name='Self-Destroy 自毁爆发', source='<a href="https://mobalytics.gg/the-bazaar/builds/self-destroy-stelle-kripp">Mobalytics / Kripp</a>', date='2026-02-16', grade='<span class="badge t-gold">中后期 pivot</span>',
             logic='「加速机器直到爆炸,修复后再来」——Boosted Saucer 吃 Destroy 触发充能,Fire Bomb / Boom Boom Bot(Bronze)提供引爆,Recycler Bot 修复循环;Ornithopter / Flycycle 的定向飞行可控引爆 Boom Boom Bot。',
             items=['Boosted Saucer', 'Fire Bomb', 'Boom Boom Bot', 'Recycler Bot', 'Precision Calipers', 'Ornithopter', 'Flycycle'],
             skills=[],
             note='组件与其他构筑兼容性差,基本是独立 pivot 线;早期 Boosted Saucer 可直接构筑,否则先玩 Balloon Engine。'),
        dict(name='Lightning Rod 慢控转伤', source='<a href="https://mobalytics.gg/the-bazaar/builds/lightning-rod-stelle-kripp">Mobalytics / Kripp</a>', date='2026-01-27', grade='<span class="badge t-silver">pivot 构筑</span>',
             logic='Stelle 版 Proboscis(Kripp 类比)——把 Weather Machine / Fire Hose / Flashbang 的海量 Slow 触发转成伤害;Slow and Steady 技能是理想成长件。',
             items=['Lightning Rod', 'Weather Machine', 'Fire Hose', 'Flashbang', 'Cloud Tanker'],
             skills=['Slow and Steady'],
             note='双核心 Lightning Rod + Weather Machine 分开找、可 Stash;Caracara + Aerial Turret 常规线是最常见过渡。'),
        dict(name='LavaRoller 熔岩滚轮', source='<a href="https://mobalytics.gg/the-bazaar/builds/lavaroller-stelle-kripp">Mobalytics / Kripp</a>', date='2026-03-21', grade='<span class="badge t-gold">全游最狂灼烧</span>',
             logic='自翻倍机制让数值失控——「全 Bazaar 最疯狂的灼烧原型」;Gold 起步 + 复杂自毁性,从 Balloon Engine 壳过渡最顺;灼烧技能保留可用。',
             items=['LavaRoller', 'Balloon Engine', 'Freefall Simulator', 'Flame Jet Drone', 'Marshalling Lights', 'Headset', 'Tethers', 'Clockwork Disc', 'Molten Ball Blaster'],
             skills=[],
             note='Marshalling Lights 让 Freefall Simulator 多起飞少降落(偏置件);从 Molten Ball Blaster 也可过渡。'),
    ]
    rows.append(render_builds('Stelle', pool, data['items'], skills, stelle_axes, stelle_builds,
        source_note='来源:Mobalytics Builds(Kripparrian,Stelle 五原型全覆盖);物品/技能效果取自 Mobalytics 快照(2026-08-31,cloudflareCacheVersion v1.0.59);轴映射按 §3.6 谓词自动计算。'))

    rows.append('''<h2>6. 与 StS2 / OneDeck 的映射</h2>
<div class="card">
<h3>6.1 与 StS2 的结构异同</h3>
<table><tr><th>维度</th><th>StS2(五角色)</th><th>Bazaar(Stelle)</th><th>差异判定</th></tr>
<tr><td>入口层</td><td>Common 无能力卡,即打即用</td><td>Bronze 飞行小件/无人机</td><td>同工:入口=制造条件</td></tr>
<tr><td>兑现层</td><td>Uncommon 桥层</td><td>Silver 起降引擎件密集</td><td>同工:桥在中间 tier</td></tr>
<tr><td>封顶</td><td>Rare 大数字/引擎</td><td>Gold 自毁爆发/慢控引擎(无 Diamond)</td><td>同工:高 tier 承载放大</td></tr>
<tr><td>资源经济</td><td>垃圾经济学四形态</td><td>起降往复 + 自毁修复循环</td><td>同类,「自毁=Expire 类收益」与 StS2 消耗同构</td></tr>
<tr><td>运行</td><td>回合制/能量</td><td>实时秒表/冷却/空间</td><td>差异最大</td></tr>
</table>
<h3>6.2 OneDeck 落点(初筛)</h3>
<table><tr><th>Bazaar 观察</th><th>OneDeck 可借鉴</th><th>不可搬</th></tr>
<tr><td>start/stop Flying 双向触发</td><td><b>与 OneDeck 的「被强化/被埋葬」双向事件同构</b>——状态获得/失去各挂触发的句式可直接映射到 Rest/Revive 状态</td><td>秒表状态窗口不映射轮次制</td></tr>
<tr><td>自毁+Repair 循环</td><td>OneDeck 的「放逐自身+回收」句式已是同类——「引爆→修复→再引爆」可对照复活系循环</td><td>—</td></tr>
<tr><td>While Flying 持续乘区</td><td>OneDeck 的状态期间增益(如 Rest 跳过)可对照扩展</td><td>—</td></tr>
<tr><td>Drone 独有标签</td><td>OneDeck 的信徒(次元裂缝)标签已是同类——独占标签作为构筑身份锚点</td><td>—</td></tr>
</table>
</div>''')

    rows.append('''<h2>7. 关键卡清单</h2>
<div class="card"><table>
<tr><th>轴</th><th>物品</th><th>tier</th><th>size</th><th>cd/弹药</th><th>effects 摘要</th></tr>''')
    keycards = [
        ('起降', 'Freefall Simulator', 't-silver'), ('起降', 'Clockwork Disc', 't-bronze'), ('起降', 'Ornithopter', 't-silver'),
        ('起降', 'Paper Airplane', 't-bronze'), ('起降', 'Air-Pressure Rifle', 't-bronze'), ('起降', 'Anemometer', 't-silver'),
        ('飞行乘区', 'Aerial Turret', 't-bronze'), ('飞行乘区', 'Caracara', 't-bronze'), ('飞行乘区', 'Altimeter', 't-silver'),
        ('自毁', 'Boosted Saucer', 't-silver'), ('自毁', 'Boom Boom Bot', 't-bronze'), ('自毁', 'Bomb Voyage', 't-gold'),
        ('自毁', 'Buster', 't-gold'), ('自毁', 'Recycler Bot', 't-silver'),
        ('灼烧', 'Balloon Engine', 't-bronze'), ('灼烧', 'LavaRoller', 't-gold'), ('灼烧', 'Flame Jet Drone', 't-bronze'),
        ('慢控', 'Lightning Rod', 't-gold'), ('慢控', 'Weather Machine', 't-silver'),
        ('无人机', 'Repair Drone', 't-silver'), ('无人机', 'Relay Drone', 't-gold'),
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

    rows.append('</table></div>')

    rows.append('</table></div>')

    rows.append('''<h2>8. 文档元信息</h2>
<div class="card">
<table>
<tr><th>项</th><th>值</th></tr>
<tr><td>数据快照</td><td class="mono">2026-08-31 mobalytics.gg TheBazaarStaticDataQuery(persistedQuery, cloudflareCacheVersion v1.0.59),全池 1207 件提取 Stelle 专属 121</td></tr>
<tr><td>归属规则</td><td>heroes[] 含 Stelle 且非模板占位(description 含 Template 的 1 件已滤)</td></tr>
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

