# -*- coding: utf-8 -*-
"""Generate The Bazaar Mak pool analysis HTML (StS2-series style) — Mobalytics source."""
import json, re, os
from bazaar_bridge import bridge_report, has_tag, has_word, tag_or_word, render_builds

SNAP = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'tools', 'outputs', 'bazaar', 'mobalytics_static_2026-08-31.json')
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'docs', 'Bazaar_Mak_PoolAnalysis_2026-08-31.html')

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
    pool = [i for i in all_items if 'Mak' in i['heroes'] and not is_template_item(i)]
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
    for kw in ['Weapon', 'Tool', 'Property', 'Toy', 'Relic', 'Apparel', 'Vehicle', 'Friend', 'Core', 'Food', 'Tech', 'Trap', 'Instrument', 'Aquatic', 'Dinosaur', 'Ray', 'Potion', 'Reagent', 'Loot']:
        mech[kw] = sum(1 for x in pool if kw in (x.get('tags') or []))
    for kw in ['Charge', 'Haste', 'Slow', 'Burn', 'Freeze', 'Poison', 'Shield', 'Heal', 'Crit', 'Multicast', 'Regen', 'Transform', 'Enchant']:
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
    rows.append('<html><head><meta charset="utf-8"><title>The Bazaar · Mak 物品池结构拆解</title><style>' + CSS + '</style></head><body>')
    rows.append('<h1>The Bazaar · Mak 物品池结构拆解</h1>\n<div class="sub">2026-08-31 · 数据源:<a href="https://mobalytics.gg/the-bazaar/mak-items">mobalytics.gg</a> TheBazaarStaticDataQuery(persistedQuery, cloudflareCacheVersion v1.0.59,快照 2026-08-31)· 全池 1207 物品中 Mak 专属 140 件(已过滤 1 件模板占位)· 物品英文为准 · 同系列:Vanessa / Pygmalien / Dooley / Karnok / Jules / Stelle / The Dragons / 公共池 / 技能池 / 综合</div>')

    rows.append('<div class="kpis">')
    rows.append(f'<div class="kpi"><div class="num">{N}</div><div class="lbl">物品池总数(140 件)</div></div>')
    rows.append(f'<div class="kpi"><div class="num">{tier_counts.get("Bronze",0)}/{tier_counts.get("Silver",0)}/{tier_counts.get("Gold",0)}/{tier_counts.get("Diamond",0)}</div><div class="lbl">tier 分布 B/S/G/D(+{tier_counts.get("Legendary",0)} Legendary)</div></div>')
    rows.append(f'<div class="kpi"><div class="num">{mech.get("Relic",0)}</div><div class="lbl">Relic 相关({round(mech.get("Relic",0)/N*100)}%)—— 身份关键词</div></div>')
    rows.append(f'<div class="kpi"><div class="num">{mech.get("Potion",0)}/{mech.get("Reagent",0)}</div><div class="lbl">Potion {mech.get("Potion",0)} / Reagent {mech.get("Reagent",0)} —— 药剂/试剂双标签</div></div>')
    rows.append('<div class="kpi"><div class="num">3+3</div><div class="lbl">主轴:转化引擎 / 遗物 / 毒烧状态 + 药剂 / 试剂 / 工具</div></div>')
    rows.append(f'<div class="kpi"><div class="num">{band_counts.get("passive",0)}</div><div class="lbl">无冷却被动件({round(band_counts.get("passive",0)/N*100)}%)</div></div>')
    rows.append('</div>')

    rows.append('''<h2>0. 英雄骨架</h2>
<div class="card">
<div class="lead"><strong>定位</strong>:全游「炼金术士/转化师」英雄——核心机制为「转化引擎」:试剂(Reagent)→ 药剂(Potion)→ 催化(Catalyst)的每日循环,配合「类型累积」(Cauldron / Vat of Acid 获得你拥有物品的类型)与 Regen 乘区。身份轴:Relic 48 + Potion 21 + Reagent 13 + Poison 46 + Burn 27 + Transform 29。</div>
<div class="lead"><strong>身份关键词(Top)</strong>:''' + ' '.join(f'<span class="badge b-props">Relic {mech["Relic"]}</span><span class="badge b-tool">Potion {mech["Potion"]}</span><span class="badge">Reagent {mech["Reagent"]}</span><span class="badge">Poison {mech["Poison"]}</span><span class="badge">Burn {mech["Burn"]}</span><span class="badge b-tool">Tool {mech["Tool"]}</span>') + '</div>')
    rows.append('<div class="lead"><strong>资源轴速览</strong>:转化 = 每日生产试剂/催化剂并转化为药剂;Regen = Mak 独有乘区(Secret Formula 把 Regen 转成毒/烧);Relic = 遗物混合件(48 件全游最多);Potion = 一次性药剂(可被装填/再转化);Reagent = 试剂原料;Poison/Burn = 状态输出。Mak 的语法 =「炼金流水线」——输入试剂,输出毒/烧/药。</div>')
    rows.append('</div>')

    rows.append('''<h2>1. 池组成总览</h2>
<div class="card">
<h3>1.1 tier × size</h3>
<table>
<tr><th>Tier</th><th>数量</th><th>占比</th><th>Small</th><th>Medium</th><th>Large</th><th>定位</th></tr>''')
    size_by_tier = {}
    for x in pool:
        size_by_tier.setdefault(tier_of(x), Counter())[x.get('size')] += 1
    desc = {'Bronze': '入门密度层——工具/药剂/试剂为主,教学层', 'Silver': '主力层——遗物与转化件密集', 'Gold': '强度层——转化引擎与 Regen 乘区(29 件异常多)', 'Diamond': '封顶件层(1 件)'}
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
    typerep = {'Relic': "Philosopher's Stone / Book of Secrets / Soul Ring", 'Weapon': 'Runic Blade / Sunlight Spear / Plague Glaive', 'Tool': 'Aludel / Retort / Mortar & Pestle', 'Potion': 'Noxious Potion / Fire Potion / Rainbow Potion', 'Reagent': 'Sulphur / Myrrh / Nightshade', 'Friend': '—', 'Property': 'Laboratory / Apothecary / Library', 'Apparel': 'Soul Ring / Earrings', 'Vehicle': 'Palanquin / Atmospheric Sampler', 'Dragon': '—', 'Trap': '—', 'Loot': 'Catalyst'}
    badge = {'Weapon': 'b-weap', 'Tool': 'b-tool', 'Property': 'b-props', 'Friend': 'b-friend', 'Vehicle': 'b-veh'}
    for t in ['Relic', 'Weapon', 'Tool', 'Potion', 'Reagent', 'Property', 'Apparel', 'Vehicle', 'Friend', 'Dragon', 'Trap', 'Loot']:
        c = mech.get(t, 0)
        rows.append(f'<tr><td><span class="badge {badge.get(t, "t-silver")}">{t}</span></td><td class="mono">{c}</td><td class="mono">{round(c/N*100)}%</td><td>{typerep.get(t,"")}</td></tr>')
    rows.append('''</table>
<div class="note">本表取 tags 严格标签。Relic 48(34%)是最大构成块(全游最多遗物);Potion 21 + Reagent 13 是「炼金流水线」标签;Poison/Burn 状态是输出方式。多标签物品重复计入各标签。</div>
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
<div class="note">Mak 节奏偏中快(药剂类短冷却、遗物类被动),配合 Charge(19 件)与「每日生产」(Catalyst/Reagent)循环。Regen 是持续型乘区而非节奏。</div>
</div>''')

    rows.append('''<h2>2. 术语与分级结构</h2>
<div class="card">
<h3>2.1 机制词分布</h3>
<table><tr><th>机制词</th><th>件数</th><th>性质</th></tr>''')
    for k in ['Relic', 'Potion', 'Reagent', 'Poison', 'Burn', 'Transform', 'Regen', 'Tool', 'Weapon', 'Property', 'Charge', 'Haste', 'Slow', 'Freeze', 'Multicast', 'Crit', 'Enchant', 'Shield', 'Heal', 'Ammo']:
        rows.append(f'<tr><td class="mono">{k}</td><td class="mono">{mech.get(k,0)}</td><td>—</td></tr>')
    rows.append('''</table>
<div class="note">词表为 tags + descriptions 关键词匹配(多标签重复计入)。Transform(29)是 Mak 的引擎语汇——「转化」贯穿试剂/药剂/类型累积,是 Mak 区别于其他英雄的核心接口。</div>
</div>''')

    rows.append('''<div class="card">
<h3>2.2 触发句式</h3>
<table><tr><th>句式</th><th>件数</th><th>代表</th><th>设计含义</th></tr>''')
    def memc(kw):
        return sum(1 for x in pool if kw.lower() in desc_flat(x).lower())
    for kw in ['At the start of each day', 'When you buy this', 'transform', 'When you use a Potion', 'When you visit a Merchant', 'When you sell this']:
        rows.append(f'<tr><td>{kw}</td><td class="mono">{memc(kw)}</td><td>—</td><td>—</td></tr>')
    rows.append('</table></div>')

    rows.append('''<h2>3. 构筑轴识别</h2>
<div class="card">
<h3>3.1 转化引擎轴(身份轴)</h3>
<div class="flow">Reagent 生产(Catalyst / Alembic / Retort 每日) → Potion 转化(Alembic / Potion Distillery 把左件变药剂) → 类型累积(Cauldron / Vat of Acid 获得物品类型) → Regen 乘区(Secret Formula 把 Regen 转毒/烧)</div>
<div class="lead"><strong>入口</strong>:Reagent / Catalyst 每日生产件。<strong>兑现</strong>:Potion 使用与转化(Recycling Bin 用后变新药剂)。<strong>封顶</strong>:类型复制(Mirror 复制左件 / Vat of Acid 按类型倍毒烧)、Regen 大乘区(Secret Formula + Soul Ring)。</div>
<div class="lead"><strong>桥</strong>:Cauldron(类型累积→毒/烧)、Laboratory(附魔+充能遗物)、Potion Distillery(药剂充能+转化)。「炼金流水线」是 Mak 引擎——输入试剂,输出毒/烧/药。</div>
</div>
<div class="card">
<h3>3.2 遗物轴(身份标签)</h3>
<div class="flow">Relic 混合件(48 件全游最多)→ 遗物充能(Laboratory / Pendulum)→ Regen/附魔乘区</div>
<div class="lead"><strong>密度</strong>:Relic 48(34%)—— 遗物不是独立流派而是「混合载体」,几乎都带毒/烧/转化/附魔之一。</div>
</div>
<div class="card">
<h3>3.3 毒/烧状态轴(输出方式)</h3>
<div class="flow">Poison 46 + Burn 27 → 状态武器(Plague Glaive / Runic Blade)→ 状态乘区(C.O.R.A 类敌毒→伤 / Secret Formula Regen→毒烧)</div>
<div class="lead"><strong>特征</strong>:毒/烧是 Mak 的主要输出形态,但多作为「附带的第二效」而非主轴——真正主轴是转化。</div>
</div>
<div class="card">
<h3>3.4 工具/地产辅助轴(次轴)</h3>
<div class="lead"><strong>特征</strong>:Tool 24(转化件载体)+ Property 9(实验室/图书馆类全局)。工具承担「炼金设备」角色。</div>
</div>
<div class="card">
<h3>3.5 轴矩阵总结</h3>
<table>
<tr><th>轴</th><th>件数</th><th>tier 分布</th><th>入口/兑现/封顶</th><th>密度评价</th></tr>''')
    mak_axes = {
        '转化': lambda i: has_word(i, r'transform|Transmute|Catalyst|Reagent|Potion|Enchant'),
        '遗物': lambda i: has_tag(i, 'Relic'),
        '药剂': lambda i: has_tag(i, 'Potion'),
        '试剂': lambda i: has_tag(i, 'Reagent'),
        '状态': lambda i: has_word(i, r'Poison|Burn|Freeze|Slow'),
        '输出': lambda i: has_tag(i, 'Weapon') or has_word(i, r'Deal \d|Damage'),
        'Regen': lambda i: has_word(i, r'Regen'),
    }
    eco = [x for x in pool if mak_axes['转化'](x)]
    swing = [x for x in pool if mak_axes['遗物'](x)]
    toyprop = [x for x in pool if mak_axes['药剂'](x)]
    relic = [x for x in pool if mak_axes['试剂'](x)]
    combat = [x for x in pool if mak_axes['状态'](x)]
    regen = [x for x in pool if mak_axes['Regen'](x)]
    def tierdist(lst):
        c = Counter(tier_of(x) for x in lst)
        return f'B{c.get("Bronze",0)}/S{c.get("Silver",0)}/G{c.get("Gold",0)}/D{c.get("Diamond",0)}+{c.get("Legendary",0)}L'
    rows.append('<tr><td>转化引擎</td><td class="mono">%d</td><td>%s</td><td>试剂/催化剂 → 药剂转化 → 类型累积</td><td class="good">厚,身份轴</td></tr>' % (len(eco), tierdist(eco)))
    rows.append('<tr><td>遗物</td><td class="mono">%d</td><td>%s</td><td>混合载体 + 充能</td><td class="good">厚,身份标签</td></tr>' % (len(swing), tierdist(swing)))
    rows.append('<tr><td>药剂</td><td class="mono">%d</td><td>%s</td><td>一次性消耗 → 再转化</td><td class="warn">中,消费件</td></tr>' % (len(toyprop), tierdist(toyprop)))
    rows.append('<tr><td>试剂</td><td class="mono">%d</td><td>%s</td><td>原料 → 转化计数</td><td class="warn">中,入口件</td></tr>' % (len(relic), tierdist(relic)))
    rows.append('<tr><td>状态</td><td class="mono">%d</td><td>%s</td><td>毒/烧输出</td><td class="warn">中,输出形态</td></tr>' % (len(combat), tierdist(combat)))
    rows.append('<tr><td>Regen</td><td class="mono">%d</td><td>%s</td><td>持续乘区 → 毒/烧</td><td class="warn">中,乘区件</td></tr>' % (len(regen), tierdist(regen)))
    rows.append('</table></div>')

    # ===== 3.6 bridge matrix (2026-08-31 framework) =====
    bridge_html, bridge_stats = bridge_report(
        pool, mak_axes,
        closed_loop_desc='转化入口(Alembic 每日产催化剂+转化左件为药剂) → 药剂×转化(Recycling Bin 用药后变新药剂) → 类型累积×状态(Cauldron / Vat of Acid 按类型倍毒烧) → Regen×状态(Secret Formula 把 Regen 转毒/烧) → 遗物×转化(Laboratory 附魔+充能遗物):转化是 Mak 的绝对中心,状态/遗物/药剂全部从转化发散。'
    )
    rows.append(bridge_html)

    rows.append('''<h2>4. 两段式审计(条件制造 → 兑现)</h2>
<div class="card">
<table>
<tr><th>层</th><th>设计职责</th><th>代表件</th><th>tier 观察</th></tr>
<tr><td><strong>入口层</strong>(制造条件/供给)</td><td>试剂/催化剂生产、类型累积</td><td>Catalyst / Alembic / Retort / Cellar</td><td>B/S 为主</td></tr>
<tr><td><strong>兑现层</strong>(消费条件)</td><td>药剂使用/转化、状态武器、Regen 转输出</td><td>Recycling Bin / Plague Glaive / Secret Formula</td><td>S/G 为主</td></tr>
<tr><td><strong>封顶层</strong>(条件 → 乘区)</td><td>类型复制、Regen 大乘区、全局附魔</td><td>Mirror / Vat of Acid / Laboratory</td><td>G/D 集中</td></tr>
</table>
<div class="verdict"><strong>结论</strong>:Mak 的两段式是「流水线式」:入口件生产原料(试剂/催化剂),兑现件转化与消费(药剂/类型累积),乘区(Regen/类型数)放大输出。转化接口词汇(Transform/Reagent/Potion)是 Mak 的最小语法单位——比 StS2 的「垃圾经济学」更接近 OneDeck 的「制造条件→兑现」结构(见 §6)。</div>
</div>''')

    rows.append('''<h2>5. 强度阶梯(高 tier = 高强度?)</h2>
<div class="card">
<h3>5.1 Gold(29)与 Diamond(1)</h3>
<table>
<tr><th>件</th><th>tier</th><th>size</th><th>强度认定</th></tr>''')
    for x in sorted((x for x in pool if tier_of(x) in ('Gold', 'Diamond')), key=lambda z: tier_of(z)):
        rows.append(f'<tr><td>{x["name"]}</td><td><span class="badge t-{tier_of(x).lower()}">{tier_of(x)}</span></td><td>{x.get("size")}</td><td>{esc(" ".join(desc_of(x))[:110])}</td></tr>')
    rows.append('''</table>
<div class="verdict"><strong>结论</strong>:高 tier ≠ 必然强——Diamond(Atmospheric Sampler 附魔/飞行件互充)是引擎件;Gold 29 件异常多(全池最高 Gold 比例 21%),承载大量转化引擎(Secret Formula / Vat of Acid / Laboratory / Mirror)。tier 表达「获取难度 + 引擎潜力」,Mak 的 Gold 是「炼金设备」层。</div>
</div>''')

    # ===== 5.5 typical builds (2026-09-01 framework, v2 detailed) =====
    skills = data['skills']
    mak_builds = [
        dict(name='Calcinator 灼烧转化流', source='<a href="https://mobalytics.gg/the-bazaar/builds/calcinator-mak-kripp">Mobalytics / Kripp</a>', date='2026-07-05', grade='<span class="badge t-gold">单核 win 条件(不可 pivot)</span>',
             logic='快节奏滴答流——Calcinator 按「本局已转化的试剂」数 +3 灼烧且数值不封顶,起手即出大数字;Day 1-2 拿到才值得投入,Day 4-5 还没成型立刻弃。Retort 是同型慢速版(毒),可并列使用。',
             items=['Calcinator', 'Retort', "Philosopher's Stone", 'Aludel', 'Mortar & Pestle', 'Library', 'Scales', 'Sunlight Spear'],
             skills=[],
             note="运营节奏:每件小试剂都买、尽快凑 1-2 个催化剂发生器(Aludel / Mortar & Pestle)、Fungal Spores / Potion Potion 补催化剂;Peacewrought 为经济件(铅锭→金锭稳定后可撤);Philosopher's Stone 早升级则终板可留。攻略板位所列「Regen Stacked」技能未收录进快照,故技能表暂缺。"),
    ]
    rows.append(render_builds('Mak', pool, data['items'], skills, mak_axes, mak_builds,
        source_note='来源:Mobalytics Builds(Kripparrian);物品效果取自 Mobalytics 快照(2026-08-31,cloudflareCacheVersion v1.0.59),攻略日期即 meta 快照,跨补丁数值仅作结构参考;轴映射按 §3.6 谓词自动计算。thebazaarzone 暂无 Mak 攻略页(Mobalytics 单构筑覆盖)。'))

    rows.append('''<h2>6. 与 StS2 / OneDeck 的映射</h2>
<div class="card">
<h3>6.1 与 StS2 的结构异同</h3>
<table><tr><th>维度</th><th>StS2(五角色)</th><th>Bazaar(Mak)</th><th>差异判定</th></tr>
<tr><td>入口层</td><td>Common 无能力卡,即打即用</td><td>Bronze 试剂/催化剂生产件</td><td>同工:入口=制造条件</td></tr>
<tr><td>兑现层</td><td>Uncommon 桥层</td><td>Silver 转化件+遗物密集</td><td>同工:桥在中间 tier</td></tr>
<tr><td>封顶</td><td>Rare 大数字/引擎</td><td>Gold 转化引擎(29 件异常多)/ Diamond 附魔引擎</td><td>同工:高 tier 承载引擎</td></tr>
<tr><td>资源经济</td><td>垃圾经济学四形态</td><td>试剂→药剂→催化 流水线</td><td>同类,Mak 的「转化」= StS2 的「回收/二次使用」</td></tr>
<tr><td>运行</td><td>回合制/能量</td><td>实时秒表/冷却/空间</td><td>差异最大</td></tr>
</table>
<h3>6.2 OneDeck 落点(初筛)</h3>
<table><tr><th>Bazaar 观察</th><th>OneDeck 可借鉴</th><th>不可搬</th></tr>
<tr><td>试剂→药剂→催化 流水线(29 件转化)</td><td><b>OneDeck 的「制造条件→兑现」结构与其同构</b>——OneDeck 的复活/信徒生成链可对照「流水线」设计:原料卡→转化卡→封顶卡</td><td>每日生产(At the start of each day)不映射轮次制</td></tr>
<tr><td>Regen 乘区(Secret Formula 转毒/烧)</td><td>OneDeck 的「力量/攻击力」乘区已是同类——可对照「持续乘区→定向输出」</td><td>—</td></tr>
<tr><td>类型累积(Cauldron / Vat of Acid)</td><td>OneDeck 的「类型谓词」已有——可参考「按拥有类型数倍率」的累积感</td><td>—</td></tr>
<tr><td>遗物混合载体(48 件)</td><td>OneDeck 无遗物系统——参考其「遗物不是流派而是载体」的密度设计</td><td>遗物概念本身不搬</td></tr>
</table>
</div>''')

    rows.append('''<h2>7. 关键卡清单</h2>
<div class="card"><table>
<tr><th>轴</th><th>物品</th><th>tier</th><th>size</th><th>cd/弹药</th><th>effects 摘要</th></tr>''')
    keycards = [
        ('转化', 'Aludel', 't-bronze'), ('转化', 'Alembic', 't-silver'), ('转化', 'Retort', 't-bronze'),
        ('转化', 'Potion Distillery', 't-silver'), ('转化', 'Recycling Bin', 't-silver'), ('转化', 'Mirror', 't-gold'),
        ('类型', 'Cauldron', 't-silver'), ('类型', 'Vat of Acid', 't-gold'), ('类型', 'Laboratory', 't-gold'),
        ('Regen', 'Secret Formula', 't-gold'), ('Regen', 'Soul Ring', 't-gold'), ('Regen', 'Apothecary', 't-silver'),
        ("遗物", "Philosopher's Stone", "t-bronze"), ('遗物', 'Book of Secrets', 't-silver'), ('遗物', 'Memento Mori', 't-gold'),
        ('输出', 'Plague Glaive', 't-gold'), ('输出', 'Runic Blade', 't-gold'), ('输出', 'Sunlight Spear', 't-gold'),
        ('药剂', 'Noxious Potion', 't-bronze'), ('药剂', 'Flying Potion', 't-bronze'),
        ('引擎', 'Atmospheric Sampler', 't-diamond'), ('引擎', 'Scales', 't-gold'),
    ]
    for axis, name, tk in keycards:
        try:
            x = lookup(name)
        except StopIteration:
            rows.append(f'<tr><td>{axis}</td><td>{name}</td><td colspan="4">(Mobalytics 无此条目)</td></tr>')
            continue
        rows.append(f'<tr><td>{axis}</td><td>{name}</td><td><span class="badge {tk}">{tier_of(x)}</span></td><td class="mono">{x.get("size")}</td><td class="mono">{cd_ammo(x)}</td><td>{esc(fx_short(x))}</td></tr>')
    rows.append('</table></div>')

    rows.append('''<h2>8. 文档元信息</h2>
<div class="card">
<table>
<tr><th>项</th><th>值</th></tr>
<tr><td>数据快照</td><td class="mono">2026-08-31 mobalytics.gg TheBazaarStaticDataQuery(persistedQuery, cloudflareCacheVersion v1.0.59),全池 1207 件提取 Mak 专属 140</td></tr>
<tr><td>归属规则</td><td>heroes[] 含 Mak 且非模板占位(description 含 Template 的 1 件已滤)</td></tr>
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

