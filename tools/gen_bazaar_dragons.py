# -*- coding: utf-8 -*-
"""Generate The Bazaar The Dragons pool analysis HTML (StS2-series style) — Mobalytics source."""
import json, re, os
from bazaar_bridge import bridge_report, has_tag, has_word, tag_or_word, render_builds

SNAP = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'tools', 'outputs', 'bazaar', 'mobalytics_static_2026-08-31.json')
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'docs', 'Bazaar_TheDragons_PoolAnalysis_2026-08-31.html')

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
    pool = [i for i in all_items if 'The Dragons' in i['heroes'] and not is_template_item(i)]
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
    for kw in ['Tempo', 'Freeze', 'Charge', 'Haste', 'Slow', 'Burn', 'Poison', 'Shield', 'Heal', 'Regen', 'Crit', 'Multicast', 'Enchant', 'Flying', 'Dragon']:
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
    rows.append('<html><head><meta charset="utf-8"><title>The Bazaar · The Dragons 物品池结构拆解</title><style>' + CSS + '</style></head><body>')
    rows.append('<h1>The Bazaar · The Dragons 物品池结构拆解</h1>\n<div class="sub">2026-08-31 · 数据源:<a href="https://mobalytics.gg/the-bazaar/the-dragons-items">mobalytics.gg</a> TheBazaarStaticDataQuery(persistedQuery, cloudflareCacheVersion v1.0.59,快照 2026-08-31)· 全池 1207 物品中 The Dragons 专属 107 件(已过滤 1 件模板占位)· 物品英文为准 · 同系列:Vanessa / Pygmalien / Dooley / Mak / Karnok / Jules / Stelle / 公共池 / 技能池 / 综合</div>')

    rows.append('<div class="kpis">')
    rows.append(f'<div class="kpi"><div class="num">{N}</div><div class="lbl">物品池总数(107 件,全游最小主力池)</div></div>')
    rows.append(f'<div class="kpi"><div class="num">{tier_counts.get("Bronze",0)}/{tier_counts.get("Silver",0)}/{tier_counts.get("Gold",0)}/{tier_counts.get("Diamond",0)}</div><div class="lbl">tier 分布 B/S/G/D(无 Diamond)</div></div>')
    rows.append(f'<div class="kpi"><div class="num">{mech.get("Tempo",0)}</div><div class="lbl">Tempo 相关({round(mech.get("Tempo",0)/N*100)}%)—— 独有资源机制</div></div>')
    rows.append(f'<div class="kpi"><div class="num">{mech.get("Dragon",0)}/{mech.get("Instrument",0)}</div><div class="lbl">Dragon {mech.get("Dragon",0)} / Instrument {mech.get("Instrument",0)} —— 乐队标签</div></div>')
    rows.append('<div class="kpi"><div class="num">2+3</div><div class="lbl">主轴:Tempo 乐队引擎 / 龙族联动 + 灼烧 / 玩具应援 / 附魔</div></div>')
    rows.append(f'<div class="kpi"><div class="num">{band_counts.get("passive",0)}</div><div class="lbl">无冷却被动件({round(band_counts.get("passive",0)/N*100)}%)</div></div>')
    rows.append('</div>')

    rows.append('''<h2>0. 英雄骨架</h2>
<div class="card">
<div class="lead"><strong>定位</strong>:全游「摇滚乐队」英雄(Roughtown Rockstars)——核心机制为「Tempo 资源」:可累积可消费的战斗资源(26 件涉及),配合 Dragon(龙族成员)/ Instrument(乐器)/ Chibi 应援团标签。Small 69 件(64%)全系列最小件化——「乐队全员小件快节奏」。身份轴:Tempo 26 + Dragon 20 + Instrument 18 + Friend 19 + Tech 25。</div>
<div class="lead"><strong>身份关键词(Top)</strong>:''' + ' '.join(f'<span class="badge">Tempo {mech["Tempo"]}</span><span class="badge b-weap">Dragon {mech["Dragon"]}</span><span class="badge b-tool">Instrument {mech["Instrument"]}</span><span class="badge b-friend">Friend {mech["Friend"]}</span><span class="badge">Burn {mech["Burn"]}</span><span class="badge">Enchant {mech["Enchant"]}</span>') + '</div>')
    rows.append('<div class="lead"><strong>资源轴速览</strong>:Tempo = 可累积可消费资源(获得:Black Lipstick 开局 +4 / 每 10 Tempo +1 Multicast;消费:Fingerless Gloves 花 Tempo 减冷却)——与 Karnok Rage 同为「资源条」但可主动花费;Dragon = 龙族联动(快速触发核心);Instrument = 乐器(Tuba / Guzheng);Chibi = 玩具应援团(邻位 Toy 触发);Notes = 英雄技能半随机放置位(构筑位置敏感,同 Jules)。The Dragons 的语法 =「演出」——攒 Tempo,奏乐器,全团联动。</div>')
    rows.append('</div>')

    rows.append('''<h2>1. 池组成总览</h2>
<div class="card">
<h3>1.1 tier × size</h3>
<table>
<tr><th>Tier</th><th>数量</th><th>占比</th><th>Small</th><th>Medium</th><th>Large</th><th>定位</th></tr>''')
    size_by_tier = {}
    for x in pool:
        size_by_tier.setdefault(tier_of(x), Counter())[x.get('size')] += 1
    desc = {'Bronze': '入门密度层——小件乐队成员/玩具为主(教学层)', 'Silver': '主力层——龙族与 Tempo 件密集', 'Gold': '强度层——乐器大件与引擎(19 件)', 'Diamond': '无 Diamond(B/S/G 三档)'}
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
    typerep = {'Tech': 'Arcade Machine / Event Poster / Jammer', 'Weapon': 'Guzheng / Razor Bow / Whistling Glaive', 'Apparel': 'Bandana / Flame Skirt / Hot Pants', 'Dragon': 'Dragon Statue / Superfan / Visor', 'Friend': 'Backup Dancer / Chibi 应援团', 'Instrument': 'Tuba / Amp / Death Metal Drum Kit', 'Toy': 'Lightstick / Chronos Chibi / Confetti Cannon', 'Tool': 'Boom Mic / Equipment Van 相关', 'Drone': '—', 'Property': 'Green Screen / Arcade Machine', 'Vehicle': 'Equipment Van', 'Food': 'Bubble Gum'}
    badge = {'Weapon': 'b-weap', 'Tool': 'b-tool', 'Property': 'b-props', 'Friend': 'b-friend', 'Vehicle': 'b-veh'}
    for t in ['Tech', 'Weapon', 'Apparel', 'Dragon', 'Friend', 'Instrument', 'Toy', 'Tool', 'Drone', 'Property', 'Vehicle', 'Food']:
        c = mech.get(t, 0)
        rows.append(f'<tr><td><span class="badge {badge.get(t, "t-silver")}">{t}</span></td><td class="mono">{c}</td><td class="mono">{round(c/N*100)}%</td><td>{typerep.get(t,"")}</td></tr>')
    rows.append('''</table>
<div class="note">本表取 tags 严格标签。Small 69(64%)全系列最小件化;Dragon 20 + Instrument 18 + Friend 19 是「乐队阵容」三标签;Chibi 应援团(Toy+Friend)是独有的邻位触发族。多标签物品重复计入各标签。</div>''')

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
<div class="note">The Dragons 节奏 = 「攒 Tempo → 消费爆发」的乐句循环:Black Lipstick 开局 +4、玩具/暴击持续攒,消费端(Fingerless Gloves 花 Tempo 减冷却)决定爆发窗口。小件 64% 使整板冷却天然偏快。</div>
</div>''')

    rows.append('''<h2>2. 术语与分级结构</h2>
<div class="card">
<h3>2.1 机制词分布</h3>
<table><tr><th>机制词</th><th>件数</th><th>性质</th></tr>''')
    for k in ['Tempo', 'Dragon', 'Instrument', 'Tech', 'Weapon', 'Apparel', 'Friend', 'Burn', 'Haste', 'Charge', 'Slow', 'Crit', 'Shield', 'Heal', 'Multicast', 'Enchant', 'Flying', 'Toy', 'Regen', 'Freeze']:
        rows.append(f'<tr><td class="mono">{k}</td><td class="mono">{mech.get(k,0)}</td><td>—</td></tr>')
    rows.append('''</table>
<div class="note">词表为 tags + descriptions 关键词匹配(多标签重复计入)。Tempo(26)是 The Dragons 的引擎语汇——全游唯一「可主动消费的战斗资源」;Enchant(附魔)密度全系列最高(Lightstick 花 Tempo 附魔)。</div>''')

    rows.append('''<div class="card">
<h3>2.2 触发句式</h3>
<table><tr><th>句式</th><th>件数</th><th>代表</th><th>设计含义</th></tr>''')
    def memc(kw):
        return sum(1 for x in pool if kw.lower() in desc_flat(x).lower())
    for kw in ['gain Tempo', 'spend Tempo', 'for each Tempo', 'When you use an adjacent Toy', 'Dragon', 'When your items Crit']:
        rows.append(f'<tr><td>{kw}</td><td class="mono">{memc(kw)}</td><td>—</td><td>—</td></tr>')
    rows.append('</table></div>')

    rows.append('''<h2>3. 构筑轴识别</h2>
<div class="card">
<h3>3.1 Tempo 乐队引擎轴(身份轴)</h3>
<div class="flow">Tempo 获得(Black Lipstick 开局 +4 / Confetti Cannon 玩具计数 / Guzheng 暴击 +3) → 累积乘区(Arcade Machine 每 10 Tempo +1 Multicast / Backup Dancer 按 Tempo 回复盾) → 消费(Fingerless Gloves 花 Tempo 减邻件冷却)</div>
<div class="lead"><strong>入口</strong>:Tempo 获得件。<strong>兑现</strong>:按 Tempo 数值的累积件。<strong>封顶</strong>:Guzheng(每 Tempo 15 伤+15 盾)。Tempo 与 Karnok 的 Rage 同为「资源条」,但 Tempo 可主动消费——全游唯一。</div>
</div>
<div class="card">
<h3>3.2 龙族联动轴</h3>
<div class="flow">Dragon 触发(Visor 刷龙触发 / Event Poster 充能开门) → 龙族件(Dragon Statue 近无限回复 / Superfan 主 win 条件) → Patch 暴击解全板</div>
<div class="lead"><strong>特征</strong>:攻略判读——龙族构筑「利用快触发把数值滚到荒谬高度」,Multicast + 技能 + 邻位触发三线并进。</div>
</div>
<div class="card">
<h3>3.3 Chibi 应援团轴(独有邻位族)</h3>
<div class="lead"><strong>特征</strong>:Chronos / Cobweb / Quixel Chibi(Toy+Friend 双标签)——邻位 Toy 使用 → +1 Tempo / 充能 / 慢,三兄弟是全系列最整齐的「邻位互触发」家族。</div>
</div>
<div class="card">
<h3>3.4 灼烧/附魔副轴</h3>
<div class="lead"><strong>特征</strong>:Burn 21(Flame Skirt 慢转灼烧)+ Enchant 全系列最高密度(Lightstick 花 Tempo 附魔、G Note 附魔小件)——附魔是 The Dragons 的第三资源面。</div>
</div>
<div class="card">
<h3>3.5 轴矩阵总结</h3>
<table>
<tr><th>轴</th><th>件数</th><th>tier 分布</th><th>入口/兑现/封顶</th><th>密度评价</th></tr>''')
    dragons_axes = {
        'Tempo': lambda i: has_word(i, r'Tempo'),
        '龙族': lambda i: has_tag(i, 'Dragon') or has_word(i, r'Dragon'),
        '乐器': lambda i: has_tag(i, 'Instrument'),
        '灼烧': lambda i: has_word(i, r'Burn'),
        '玩具应援': lambda i: has_tag(i, 'Toy'),
        '附魔': lambda i: has_word(i, r'Enchant'),
        '输出': lambda i: has_tag(i, 'Weapon') or has_word(i, r'Deal \d|Damage'),
    }
    a1 = [x for x in pool if dragons_axes['Tempo'](x)]
    a2 = [x for x in pool if dragons_axes['龙族'](x)]
    a3 = [x for x in pool if dragons_axes['乐器'](x)]
    a4 = [x for x in pool if dragons_axes['灼烧'](x)]
    a5 = [x for x in pool if dragons_axes['玩具应援'](x)]
    a6 = [x for x in pool if dragons_axes['附魔'](x)]
    a7 = [x for x in pool if dragons_axes['输出'](x)]
    def tierdist(lst):
        c = Counter(tier_of(x) for x in lst)
        return f'B{c.get("Bronze",0)}/S{c.get("Silver",0)}/G{c.get("Gold",0)}/D{c.get("Diamond",0)}+{c.get("Legendary",0)}L'
    rows.append('<tr><td>Tempo 乐队</td><td class="mono">%d</td><td>%s</td><td>攒 Tempo → 累积乘区 → 消费</td><td class="good">厚,身份轴</td></tr>' % (len(a1), tierdist(a1)))
    rows.append('<tr><td>龙族</td><td class="mono">%d</td><td>%s</td><td>快触发滚雪球</td><td class="good">厚,身份轴 B</td></tr>' % (len(a2), tierdist(a2)))
    rows.append('<tr><td>乐器</td><td class="mono">%d</td><td>%s</td><td>大件乐器输出</td><td class="warn">中</td></tr>' % (len(a3), tierdist(a3)))
    rows.append('<tr><td>玩具应援</td><td class="mono">%d</td><td>%s</td><td>邻位 Chibi 触发</td><td class="warn">中</td></tr>' % (len(a5), tierdist(a5)))
    rows.append('<tr><td>灼烧</td><td class="mono">%d</td><td>%s</td><td>Flame Skirt 慢转灼烧</td><td class="warn">中</td></tr>' % (len(a4), tierdist(a4)))
    rows.append('<tr><td>附魔</td><td class="mono">%d</td><td>%s</td><td>全系列最高密度</td><td class="warn">中,资源面</td></tr>' % (len(a6), tierdist(a6)))
    rows.append('<tr><td>输出</td><td class="mono">%d</td><td>%s</td><td>武器输出</td><td class="warn">中</td></tr>' % (len(a7), tierdist(a7)))
    rows.append('</table></div>')

    # ===== 3.6 bridge matrix (2026-08-31 framework) =====
    bridge_html, bridge_stats = bridge_report(
        pool, dragons_axes,
        closed_loop_desc='Tempo 获得(Black Lipstick 开局 +4) → Tempo×输出(Guzheng 每 Tempo 伤+盾) → 龙族×暴击(Visor 刷龙触发 / Patch 暴击解全板) → 玩具×Tempo(Chibi 邻位 Toy +1 Tempo) → 附魔×Tempo(Lightstick 花 Tempo 附魔):Tempo 是资金流,乐队全员(Dragon/Instrument/Chibi)各自向它存取。'
    )
    rows.append(bridge_html)

    rows.append('''<h2>4. 两段式审计(条件制造 → 兑现)</h2>
<div class="card">
<table>
<tr><th>层</th><th>设计职责</th><th>代表件</th><th>tier 观察</th></tr>
<tr><td><strong>入口层</strong>(制造条件/供给)</td><td>Tempo 获得、龙族/玩具触发</td><td>Black Lipstick / Confetti Cannon / Chibi 三兄弟</td><td>B/S 为主</td></tr>
<tr><td><strong>兑现层</strong>(消费条件)</td><td>按 Tempo 累积、龙族滚雪球、附魔</td><td>Backup Dancer / Arcade Machine / Dragon Statue / Superfan</td><td>B/S 为主</td></tr>
<tr><td><strong>封顶层</strong>(条件 → 乘区)</td><td>Tempo 大乘区(Guzheng)、附魔引擎(Lightstick)</td><td>Guzheng / Lightstick / Fingerless Gloves</td><td>G 集中(无 Diamond)</td></tr>
</table>
<div class="verdict"><strong>结论</strong>:The Dragons 的两段式是「存取式」:Tempo 获得件存款,累积/消费件取款并放大——全游唯一「可主动消费的资源条」让节奏自主(对比 Karnok Rage 的自动触发)。Tempo 是 The Dragons 的最小语法单位,Small 64% 的小件化支持「高频存取」(见 §6)。</div>
</div>''')

    rows.append('''<h2>5. 强度阶梯(高 tier = 高强度?)</h2>
<div class="card">
<h3>5.1 Gold(19)与 Diamond(0)</h3>
<table>
<tr><th>件</th><th>tier</th><th>size</th><th>强度认定</th></tr>''')
    for x in sorted((x for x in pool if tier_of(x) in ('Gold', 'Diamond')), key=lambda z: tier_of(z)):
        rows.append(f'<tr><td>{x["name"]}</td><td><span class="badge t-{tier_of(x).lower()}">{tier_of(x)}</span></td><td>{x.get("size")}</td><td>{esc(" ".join(desc_of(x))[:110])}</td></tr>')
    rows.append('''</table>
<div class="verdict"><strong>结论</strong>:高 tier ≠ 必然强——The Dragons 无 Diamond,Gold 19 件全是「乐队大件」(Death Metal Drum Kit / Amp / Equipment Van / Guzheng / Lightstick)。三层 B/S/G 结构 + Small 64% 让后期成长依赖「升级与附魔」而非大件——与 Karnok/Stelle 的「无 Diamond pivot 型」同构但更极端。</div>
</div>''')

    # ===== 5.5 typical builds (2026-09-01 framework, v2 detailed) =====
    skills = data['skills']
    dragons_builds = [
        dict(name='Burn / Flame Skirt 慢转灼烧', source='<a href="https://mobalytics.gg/the-bazaar/builds/the-dragons-burn">Mobalytics</a>', date='2026-08-14', grade='<span class="badge t-diamond">最新构筑</span>',
             logic='把海量 Slow 触发转成灼烧——Flame Skirt 是核心输出转化器;ISO-Belle 低冷却多目标慢,Tuba 中件大范围慢(需解决 Tempo 供给),Black Lipstick 开局 +4 Tempo 启动。',
             items=['ISO-Belle', 'Flame Skirt', 'Tuba', 'Wings', 'Hot Pants', 'Black Lipstick', 'Cobweb Chibi'],
             skills=[],
             note='不需要特定技能——适配性最高;Notes(英雄技能位)半随机放置,位置依旧敏感;优先升级 ISO-Belle 增加慢目标数。'),
        dict(name='Dragon / Superfan 龙族滚雪球', source='<a href="https://mobalytics.gg/the-bazaar/builds/the-dragons-dragon">Mobalytics</a>', date='2026-08-14', grade='<span class="badge t-gold">快触发成长型</span>',
             logic='利用快触发把数值滚到荒谬高度——Visor 刷龙触发、Event Poster 充能开门(解锁非龙件)、Dragon Statue 近无限回复、Superfan 是数值解决后的主 win 条件、Patch 借 Statue 触发解全板暴击。',
             items=['Bandana', 'Event Poster', 'Visor', 'Dragon Statue', 'Superfan', 'Maracas', 'Patch'],
             skills=[],
             note='Bandana 只有配 Event Poster 才值得用;Maracas 是副输出兼 Superfan 充能源。'),
    ]
    rows.append(render_builds('The Dragons', pool, data['items'], skills, dragons_axes, dragons_builds,
        source_note='来源:Mobalytics(2026-08-14,最新);物品效果取自 Mobalytics 快照(2026-08-31,cloudflareCacheVersion v1.0.59);轴映射按 §3.6 谓词自动计算。Notes 英雄技能位半随机放置,构筑位置敏感。'))

    rows.append('''<h2>6. 与 StS2 / OneDeck 的映射</h2>
<div class="card">
<h3>6.1 与 StS2 的结构异同</h3>
<table><tr><th>维度</th><th>StS2(五角色)</th><th>Bazaar(The Dragons)</th><th>差异判定</th></tr>
<tr><td>入口层</td><td>Common 无能力卡,即打即用</td><td>Bronze 小件乐队成员</td><td>同工:入口=制造条件</td></tr>
<tr><td>兑现层</td><td>Uncommon 桥层</td><td>Silver 龙族/Tempo 件密集</td><td>同工:桥在中间 tier</td></tr>
<tr><td>封顶</td><td>Rare 大数字/引擎</td><td>Gold 乐队大件(乐器/引擎)</td><td>同工:高 tier 承载引擎</td></tr>
<tr><td>资源经济</td><td>垃圾经济学四形态</td><td>Tempo 存取式资源(可主动消费)</td><td>同类且独有——「可消费资源条」是全游唯一</td></tr>
<tr><td>运行</td><td>回合制/能量</td><td>实时秒表/冷却/空间</td><td>差异最大</td></tr>
</table>
<h3>6.2 OneDeck 落点(初筛)</h3>
<table><tr><th>Bazaar 观察</th><th>OneDeck 可借鉴</th><th>不可搬</th></tr>
<tr><td>Tempo 可消费资源条</td><td><b>OneDeck 的 Mana 状态已是「可消费资源」雏形</b>——Tempo 的「累积乘区 + 消费减冷却」双出口设计可对照 Mana 的扩展方向</td><td>秒表冷却消费不映射轮次制</td></tr>
<tr><td>Chibi 邻位互触发族</td><td>OneDeck 无相邻概念——同型「家族卡」可用「同 tag 计数」表达</td><td>位形语法不搬</td></tr>
<tr><td>附魔系统(全系列最高密度)</td><td>OneDeck 的「增强诅咒」是敌方侧附魔雏形——可对照附魔的「物品改造」广度</td><td>附魔作为商店系统不搬</td></tr>
<tr><td>Small 64% 小件化</td><td>OneDeck 卡无尺寸概念(12 卡位已限空间)——对照意义有限</td><td>尺寸经济不搬</td></tr>
</table>
</div>''')

    rows.append('''<h2>7. 关键卡清单</h2>
<div class="card"><table>
<tr><th>轴</th><th>物品</th><th>tier</th><th>size</th><th>cd/弹药</th><th>effects 摘要</th></tr>''')
    keycards = [
        ('Tempo', 'Black Lipstick', 't-bronze'), ('Tempo', 'Confetti Cannon', 't-bronze'), ('Tempo', 'Backup Dancer', 't-silver'),
        ('Tempo', 'Arcade Machine', 't-bronze'), ('Tempo', 'Fingerless Gloves', 't-gold'), ('Tempo', 'Guzheng', 't-gold'),
        ('龙族', 'Dragon Statue', 't-silver'), ('龙族', 'Superfan', 't-silver'), ('龙族', 'Visor', 't-silver'),
        ('龙族', 'Event Poster', 't-silver'), ('龙族', 'Patch', 't-silver'),
        ('应援', 'Chronos Chibi', 't-silver'), ('应援', 'Cobweb Chibi', 't-silver'), ('应援', 'Quixel Chibi', 't-silver'),
        ('灼烧', 'Flame Skirt', 't-silver'), ('灼烧', 'ISO-Belle', 't-silver'),
        ('附魔', 'Lightstick', 't-gold'), ('附魔', 'G Note', 't-silver'),
        ('乐器', 'Tuba', 't-silver'), ('乐器', 'Amp', 't-gold'),
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

    rows.append('</table></div>')

    rows.append('''<h2>8. 文档元信息</h2>
<div class="card">
<table>
<tr><th>项</th><th>值</th></tr>
<tr><td>数据快照</td><td class="mono">2026-08-31 mobalytics.gg TheBazaarStaticDataQuery(persistedQuery, cloudflareCacheVersion v1.0.59),全池 1207 件提取 The Dragons 专属 107</td></tr>
<tr><td>归属规则</td><td>heroes[] 含 The Dragons 且非模板占位(description 含 Template 的 1 件已滤)</td></tr>
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

