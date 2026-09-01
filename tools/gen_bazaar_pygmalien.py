# -*- coding: utf-8 -*-
"""Generate The Bazaar Pygmalien pool analysis HTML (StS2-series style) — Mobalytics source."""
import json, re, os
from bazaar_bridge import bridge_report, has_tag, has_word, tag_or_word, render_builds

SNAP = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'tools', 'outputs', 'bazaar', 'mobalytics_static_2026-08-31.json')
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'docs', 'Bazaar_Pygmalien_PoolAnalysis_2026-08-31.html')

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
    pool = [i for i in all_items if 'Pygmalien' in i['heroes'] and not is_template_item(i)]
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
    for kw in ['Weapon', 'Tool', 'Property', 'Toy', 'Relic', 'Apparel', 'Vehicle', 'Friend', 'Core', 'Food', 'Tech', 'Trap', 'Instrument', 'Aquatic']:
        mech[kw] = sum(1 for x in pool if kw in (x.get('tags') or []))
    for kw in ['Value', 'Sell', 'Price', 'Gold', 'Income', 'Buy', 'Spare Change', 'Heal', 'Shield', 'Burn', 'Freeze', 'Haste', 'Slow', 'Crit', 'Multicast']:
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
    rows.append('<html><head><meta charset="utf-8"><title>The Bazaar · Pygmalien 物品池结构拆解</title><style>' + CSS + '</style></head><body>')
    rows.append('<h1>The Bazaar · Pygmalien 物品池结构拆解</h1>\n<div class="sub">2026-08-31 · 数据源:<a href="https://mobalytics.gg/the-bazaar/pygmalien-items">mobalytics.gg</a> TheBazaarStaticDataQuery(persistedQuery, cloudflareCacheVersion v1.0.59,快照 2026-08-31)· 全池 1207 物品中 Pygmalien 专属 153 件(已过滤 1 件模板占位)· 物品英文为准 · 同系列:Vanessa / Dooley / Mak / Karnok / Jules / Stelle / The Dragons / 公共池 / 技能池 / 综合</div>')

    rows.append('<div class="kpis">')
    rows.append(f'<div class="kpi"><div class="num">{N}</div><div class="lbl">物品池总数(全游最大,153 件)</div></div>')
    rows.append(f'<div class="kpi"><div class="num">{tier_counts.get("Bronze",0)}/{tier_counts.get("Silver",0)}/{tier_counts.get("Gold",0)}/{tier_counts.get("Diamond",0)}</div><div class="lbl">tier 分布 B/S/G/D</div></div>')
    rows.append(f'<div class="kpi"><div class="num">{mech.get("Value",0)}</div><div class="lbl">Value 相关({round(mech.get("Value",0)/N*100)}%)—— 身份关键词</div></div>')
    rows.append(f'<div class="kpi"><div class="num">{mech.get("Toy",0)}/{mech.get("Property",0)}</div><div class="lbl">Toy {mech.get("Toy",0)} / Property {mech.get("Property",0)} —— 两大身份标签</div></div>')
    rows.append('<div class="kpi"><div class="num">2+3</div><div class="lbl">主轴:经济(买卖/价值) / 武器护盾摆动 + 玩具 / 地产 / 遗物</div></div>')
    rows.append(f'<div class="kpi"><div class="num">{band_counts.get("passive",0)}</div><div class="lbl">无冷却被动件({round(band_counts.get("passive",0)/N*100)}%)</div></div>')
    rows.append('</div>')

    rows.append('''<h2>0. 英雄骨架</h2>
<div class="card">
<div class="lead"><strong>定位</strong>:全游「经济资本家」英雄——物品池数量全游最大(153),身份轴为「买卖增值 + 玩具/地产/遗物 + 武器护盾摆动」。核心语法:Value(物品价值)→ Sell Price(卖出价格)→ Spare Change(零钱)/ Gold(金币)/ Income(收入),全部围绕「投资—回报」。</div>
<div class="lead"><strong>身份关键词(Top)</strong>:''' + ' '.join(f'<span class="badge b-props">Property {mech["Property"]}</span><span class="badge b-tool">Toy {mech["Toy"]}</span><span class="badge b-tool">Value {mech["Value"]}</span><span class="badge">Sell {mech["Sell"]}</span><span class="badge">Relic {mech["Relic"]}</span><span class="badge b-weap">Weapon {mech["Weapon"]}</span>') + '</div>')
    rows.append('<div class="lead"><strong>资源轴速览</strong>:Value = 物品价值(可被技能/物品放大);Sell Price = 卖价(卖出回收经济);Spare Change / Gold / Income = 硬通货(经济引擎驱动);Weapon ↔ Shield 摆动 = 自循环驱动;Property = 地产经济(被购买/每日收益)。</div>')
    rows.append('</div>')

    rows.append('''<h2>1. 池组成总览</h2>
<div class="card">
<h3>1.1 tier × size</h3>
<table>
<tr><th>Tier</th><th>数量</th><th>占比</th><th>Small</th><th>Medium</th><th>Large</th><th>定位</th></tr>''')
    size_by_tier = {}
    for x in pool:
        size_by_tier.setdefault(tier_of(x), Counter())[x.get('size')] += 1
    desc = {'Bronze': '入门密度层——玩具/地产/工具为主,教学层', 'Silver': '主力层——经济件与遗物集中', 'Gold': '强度层——高价值地产与引擎', 'Diamond': '封顶件层(3 件)'}
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
    typerep = {'Toy': 'Piggy Bank / Abacus / Cash Register', 'Property': 'ATM / Beehive / Billboard', 'Weapon': 'Briefcase / Golf Clubs / Dog', 'Tool': 'Ledger / Bushel / Spices', 'Apparel': 'Apropos Chapeau / Belt / Bag', 'Relic': 'Atlas Stone / Atlas / Crystal Bonsai', 'Tech': 'Laser Security System / Robot', 'Vehicle': 'Ice Cream Truck / Lemonade Stand', 'Friend': 'Dog / Flying Pig / Pigs', 'Food': 'Bushel / Lemonade / Spices', 'Trap': 'Caltrops / Booby Trap', 'Instrument': 'Ganjo / Gramophone'}
    badge = {'Weapon': 'b-weap', 'Tool': 'b-tool', 'Property': 'b-props', 'Friend': 'b-friend', 'Vehicle': 'b-veh'}
    for t in ['Toy', 'Property', 'Weapon', 'Tool', 'Apparel', 'Relic', 'Tech', 'Vehicle', 'Friend', 'Food', 'Trap', 'Instrument']:
        c = mech.get(t, 0)
        rows.append(f'<tr><td><span class="badge {badge.get(t, "t-silver")}">{t}</span></td><td class="mono">{c}</td><td class="mono">{round(c/N*100)}%</td><td>{typerep.get(t,"")}</td></tr>')
    rows.append('''</table>
<div class="note">本表取 tags 严格标签。玩具(玩具店主题)与经济件是最大构成块;Property 是「逛店/地产循环」的底盘。多标签物品重复计入各标签。</div>
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
<div class="note">Pygmalien 节奏偏中慢(经济件以被动/低频为主,战斗件 4-9s)。武器护盾摆动件(28 Hour Fitness 类)是内部自循环驱动的最明显例子。</div>
</div>''')

    rows.append('''<h2>2. 术语与分级结构</h2>
<div class="card">
<h3>2.1 机制词分布</h3>
<table><tr><th>机制词</th><th>件数</th><th>性质</th></tr>''')
    for k in ['Value', 'Sell', 'Price', 'Gold', 'Income', 'Buy', 'Spare Change', 'Toy', 'Property', 'Weapon', 'Tool', 'Relic', 'Heal', 'Shield', 'Burn', 'Freeze', 'Haste', 'Slow', 'Crit', 'Multicast']:
        rows.append(f'<tr><td class="mono">{k}</td><td class="mono">{mech.get(k,0)}</td><td>—</td></tr>')
    rows.append('''</table>
<div class="note">词表为 tags + descriptions 关键词匹配(多标签重复计入)。Pygmalien 的「Value/Sell/Price/Gold/Income」五件经济关键词密度全游最高——经济轴是其身份正身。</div>
</div>''')

    rows.append('''<div class="card">
<h3>2.2 触发句式</h3>
<table><tr><th>句式</th><th>件数</th><th>代表</th><th>设计含义</th></tr>''')
    def memc(kw):
        return sum(1 for x in pool if kw.lower() in desc_flat(x).lower())
    for kw in ['When you buy', 'When you sell', 'At the start of each day', 'When you visit a Merchant', 'Adjacent items gain', 'When an enemy uses']:
        rows.append(f'<tr><td>{kw}</td><td class="mono">{memc(kw)}</td><td>—</td><td>—</td></tr>')
    rows.append('</table></div>')

    rows.append('''<h2>3. 构筑轴识别</h2>
<div class="card">
<h3>3.1 经济投资轴(身份轴)</h3>
<div class="flow">Value 累积(Piggy Bank / Business Card) → Sell Price 放大(Abacus) → 卖出回收(Spare Change / Gold) → 收入引擎(ATM / Vending Machine)</div>
<div class="lead"><strong>入口</strong>:Value 增益件(Sell Price 价值提升)。<strong>兑现</strong>:卖出/回收现金(Spare Change、Gold、Income)。<strong>封顶</strong>:地产生物(值钱地产)+ 每日收益物件(Vending Machine / Beehive)。</div>
<div class="lead"><strong>桥</strong>:Vending Machine / Business Card / 各类「每日生产」Property。「现金流瀑布」是 Pygmalien 引擎。</div>
</div>
<div class="card">
<h3>3.2 武器护盾摆动轴</h3>
<div class="flow">28 Hour Fitness(武器→盾 / 盾→武器伤害) → 其它摆动件 → 自循环(使用武器给盾,用盾给伤)</div>
<div class="lead"><strong>设计特征</strong>:Pygmalien 版「自动机」——武器/盾牌互换产生持续缓冲+输出。</div>
</div>
<div class="card">
<h3>3.3 玩具/地产/遗物密度轴</h3>
<div class="flow">Piggy Bank(玩具,卖价值) → Abacus(工具,价值传递) → 地产(Property 每日收益) → 遗物(Atlas Stone 等)</div>
<div class="lead"><strong>密度</strong>:Toy 40 + Property 40 + Relic 13 ——「逛店/租售」主题,配合买卖经济形成闭环。</div>
</div>
<div class="card">
<h3>3.4 战斗辅助轴(次轴)</h3>
<div class="lead"><strong>特征</strong>:Weapon 36 + Heal 50 + Shield 38 —— Pygmalien 有武器格斗件与大量护盾/治疗,战斗输出偏「耐久滚雪球」。</div>
</div>
<div class="card">
<h3>3.5 轴矩阵总结</h3>
<table>
<tr><th>轴</th><th>件数</th><th>tier 分布</th><th>入口/兑现/封顶</th><th>密度评价</th></tr>''')
    pyg_axes = {
        '经济': lambda i: has_word(i, r'Value|Sell Price|Gold|Income|Spare Change'),
        '摆动': lambda i: has_word(i, r'Weapon.*Shield|Shield.*Weapon'),
        '玩具': lambda i: has_tag(i, 'Toy'),
        '地产': lambda i: has_tag(i, 'Property'),
        '遗物': lambda i: has_tag(i, 'Relic'),
        '战斗': lambda i: has_tag(i, 'Weapon') or has_word(i, r'Shield|Heal'),
    }
    eco = [x for x in pool if pyg_axes['经济'](x)]
    swing = [x for x in pool if pyg_axes['摆动'](x)]
    toyprop = [x for x in pool if pyg_axes['玩具'](x) or pyg_axes['地产'](x)]
    relic = [x for x in pool if pyg_axes['遗物'](x)]
    combat = [x for x in pool if pyg_axes['战斗'](x)]
    def tierdist(lst):
        c = Counter(tier_of(x) for x in lst)
        return f'B{c.get("Bronze",0)}/S{c.get("Silver",0)}/G{c.get("Gold",0)}/D{c.get("Diamond",0)}'
    rows.append('<tr><td>经济投资</td><td class="mono">%d</td><td>%s</td><td>Value 累积 → Sell Price → 现金/收入</td><td class="good">厚,身份轴</td></tr>' % (len(eco), tierdist(eco)))
    rows.append('<tr><td>武器盾摆</td><td class="mono">%d</td><td>%s</td><td>武器→盾→伤自循环</td><td class="warn">中,引擎件</td></tr>' % (len(swing), tierdist(swing)))
    rows.append('<tr><td>玩具/地产</td><td class="mono">%d</td><td>%s</td><td>玩具/地产 → 每日收益 → 递进</td><td class="good">厚,主题轴</td></tr>' % (len(toyprop), tierdist(toyprop)))
    rows.append('<tr><td>遗物</td><td class="mono">%d</td><td>%s</td><td>被动增益件</td><td class="warn">中</td></tr>' % (len(relic), tierdist(relic)))
    rows.append('<tr><td>战斗辅助</td><td class="mono">%d</td><td>%s</td><td>武器格斗 + 耐久</td><td class="warn">中</td></tr>' % (len(combat), tierdist(combat)))
    rows.append('</table></div>')

    # ===== 3.6 bridge matrix (2026-08-31 framework) =====
    bridge_html, bridge_stats = bridge_report(
        pool, pyg_axes,
        closed_loop_desc='经济入口(Piggy Bank 累积 Value) → 兑现(Abacus 卖价传递 / Cash Register 产零钱) → 经济×地产(Vending Machine 每日产货) → 经济×摆动(28 Hour Fitness 武器盾摆) → 战斗×经济(Briefcase 胜场得零钱):经济轴是绝对中心,几乎所有桥都从经济发散。'
    )
    rows.append(bridge_html)

    rows.append('''<h2>4. 两段式审计(条件制造 → 兑现)</h2>
<div class="card">
<table>
<tr><th>层</th><th>设计职责</th><th>代表件</th><th>tier 观察</th></tr>
<tr><td><strong>入口层</strong>(制造条件/供给)</td><td>Value 累积、Sell Price 提升、每日收益</td><td>Piggy Bank / Business Card / Vending Machine</td><td>B/S 为主</td></tr>
<tr><td><strong>兑现层</strong>(消费条件)</td><td>卖出回收、武器盾摆自循环、地产生益</td><td>Abacus / 28 Hour Fitness / Cash Register</td><td>S/G 为主</td></tr>
<tr><td><strong>封顶层</strong>(条件 → 乘区)</td><td>高价值地产/大件、全局收入引擎</td><td>Fort / Tournament Arena / 大型 Property</td><td>G/D 集中</td></tr>
</table>
<div class="verdict"><strong>结论</strong>:Bazaar 的经济轴是「投资—回报」两段式:入口件投资(Value/收入),兑现件回收(Sell/卖出)。Pygmalien 的语法 =「钱生钱」,其 Value/Sell/Income 接口词汇与 StS2 的「垃圾经济学」不同——Pygmalien 用经济兑现金,而不是卡片数。</div>
</div>''')

    rows.append('''<h2>5. 强度阶梯(高 tier = 高强度?)</h2>
<div class="card">
<h3>5.1 Gold(27)与 Diamond(3)</h3>
<table>
<tr><th>件</th><th>tier</th><th>size</th><th>强度认定</th></tr>''')
    for x in sorted((x for x in pool if tier_of(x) in ('Gold', 'Diamond')), key=lambda z: tier_of(z)):
        rows.append(f'<tr><td>{x["name"]}</td><td><span class="badge t-{tier_of(x).lower()}">{tier_of(x)}</span></td><td>{x.get("size")}</td><td>{esc(" ".join(desc_of(x))[:110])}</td></tr>')
    rows.append('''</table>
<div class="verdict"><strong>结论</strong>:高 tier ≠ 必然强——Diamond 3 件(Caltrops / Fort / Tournament Arena)是「全局引擎」而非纯数值;Gold 的高价值地产(Beehive / Billboard)在经济语境下更强。tier 表达「获取难度 + 强度上限潜质」,经济件的高 tier 通常是「日常收益」而非战斗爆发。</div>
</div>''')

    # ===== 5.5 typical builds (2026-09-01 framework, v2 detailed) =====
    skills = data['skills']
    pyg_builds = [
        dict(name='Jabalian Drum 摇滚鼓经济流', source='thebazaarzone / Den', date='2025-05', grade='<span class="badge t-diamond">A+</span>(预期 7-10 胜)',
             logic='前期 Below Average、后期 Great——晚期滚雪球引擎,Pygmalien 当前最强;攻略将 28 Hour Fitness 视为同类替代核心。',
             items=['Jabalian Drum'], skills=[],
             note='28 Hour Fitness(见 Fit Pyg 行)与 Drum 同级可替——「后期 Great」的双核心。'),
        dict(name='Freeze 冻结流', source='thebazaarzone / Den', date='2025-05', grade='<span class="badge t-gold">A</span>(4-10 胜)',
             logic='前期 Poor、后期 Great;冻结敌方节奏,Frozen Assets 扩展解锁的新兴构筑(「冻结太强刚被削」)。',
             items=['Fort', 'Igloo', 'PenFT'], skills=[],
             note='攻略注明:若全板物品价值已超 10 金,PenFT 可移除。'),
        dict(name='Fit Pyg 健身流', source='thebazaarzone / Den', date='2025-05', grade='<span class="badge t-silver">B</span>(4-10 胜)',
             logic='武器盾摆自循环——用武器得盾、用盾得伤,中期成型的耐久流。',
             items=['28 Hour Fitness'], skills=[], note=None),
        dict(name='Fixer Upper 翻新流', source='thebazaarzone / Den', date='2025-03', grade='<span class="badge t-gold">A</span>(7-10 胜)',
             logic='前期 Below Average、后期 Fantastic——地产翻新滚雪球,3 月版最强构筑。',
             items=['Fixer Upper'], skills=[], note=None),
        dict(name='Charging Items 充能玩具流 / Scaling Weapon 成长武器', source='thebazaarzone / Den', date='2025-03', grade='<span class="badge t-silver">B / B+</span>',
             logic='充能流靠 Matchbox/Marbles 起爆,全局 +1s 冷却补丁后启动受挫降至 B;成长武器流靠技能 Lifting 按购入武器数全局 +伤,B+。',
             items=['Matchbox', 'Marbles'], skills=['Lifting'],
             note='Lifting 为 Pygmalien 专属技能——「每购入 1 武器全件 +1 伤」,与商店购买行为直接联动,是 Scaling Weapon 构筑的定义技能。攻略另提及 PvE 技能 Trained(被慢时武器 +5 伤)为过渡选项。'),
    ]
    rows.append(render_builds('Pygmalien', pool, data['items'], skills, pyg_axes, pyg_builds,
        source_note='来源:thebazaarzone(Den)英雄攻略;物品/技能效果取自 Mobalytics 快照(2026-08-31,cloudflareCacheVersion v1.0.59),攻略日期即 meta 快照,跨补丁数值仅作结构参考;轴映射按 §3.6 谓词自动计算。Mobalytics Builds 索引当前无 Pygmalien 构筑页。'))

    rows.append('''<h2>6. 与 StS2 / OneDeck 的映射</h2>
<div class="card">
<h3>6.1 与 StS2 的结构异同</h3>
<table><tr><th>维度</th><th>StS2(五角色)</th><th>Bazaar(Pygmalien)</th><th>差异判定</th></tr>
<tr><td>入口层</td><td>Common 无能力卡,即打即用</td><td>Bronze 经济件/玩具——「投资」是入口</td><td>同工:入口=制造条件</td></tr>
<tr><td>兑现层</td><td>Uncommon 桥层</td><td>Silver 回收件(卖出/摆动)为主</td><td>同工:桥在中间 tier</td></tr>
<tr><td>封顶</td><td>Rare 大数字/引擎</td><td>Gold/Diamond 地产生意/全局引擎</td><td>同工:高 tier 承载乘区</td></tr>
<tr><td>资源经济</td><td>垃圾经济学四形态</td><td>经济兑现(Value→Sell→Gold)</td><td>同类,Bazaar 更「金融化」</td></tr>
<tr><td>运行</td><td>回合制/能量</td><td>实时秒表/冷却/空间</td><td>差异最大</td></tr>
</table>
<h3>6.2 OneDeck 落点(初筛)</h3>
<table><tr><th>Bazaar 观察</th><th>OneDeck 可借鉴</th><th>不可搬</th></tr>
<tr><td>Value→Sell→Gold 经济链</td><td>OneDeck 无经济系统——不适合直接搬,但「投资—回报」句式(条件/条件兑现)可用</td><td>金币/买卖不映射 OneDeck 的轮次制</td></tr>
<tr><td>武器↔盾摆(28 Hour Fitness)</td><td>OneDeck 已有「伤害↔护盾」互转类(攻防互转),可对照</td><td>—</td></tr>
<tr><td>Property 每日收益</td><td>OneDeck 的「每日」句式 = 每次战斗开始(若有),可作对照</td><td>—</td></tr>
<tr><td>玩具/遗物密度</td><td>OneDeck 的「道具/遗物」类机制可参考其「收集价值」感</td><td>—</td></tr>
</table>
</div>''')

    rows.append('''<h2>7. 关键卡清单</h2>
<div class="card"><table>
<tr><th>轴</th><th>物品</th><th>tier</th><th>size</th><th>cd/弹药</th><th>effects 摘要</th></tr>''')
    keycards = [
        ('经济', 'Piggy Bank', 't-bronze'), ('经济', 'Business Card', 't-bronze'), ('经济', 'Abacus', 't-silver'),
        ('经济', 'ATM', 't-bronze'), ('经济', 'Vending Machine', 't-silver'), ('经济', 'Cash Register', 't-bronze'),
        ('摆动', '28 Hour Fitness', 't-gold'), ('摆动', 'Apropos Chapeau', 't-silver'),
        ('地产', 'Fort', 't-diamond'), ('地产', 'Beehive', 't-gold'), ('地产', 'Billboard', 't-gold'),
        ('武器', 'Briefcase', 't-bronze'), ('武器', 'Golf Clubs', 't-bronze'), ('武器', 'Dog', 't-bronze'),
        ('遗物', 'Atlas Stone', 't-silver'),
        ('食物', 'Bushel', 't-bronze'), ('食物', 'Spices', 't-gold'),
        ('新件', 'Tournament Arena', 't-diamond'), ('新件', 'Caltrops', 't-diamond'),
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
<tr><td>数据快照</td><td class="mono">2026-08-31 mobalytics.gg TheBazaarStaticDataQuery(persistedQuery, cloudflareCacheVersion v1.0.59),全池 1207 件提取 Pygmalien 专属 153</td></tr>
<tr><td>归属规则</td><td>heroes[] 含 Pygmalien 且非模板占位(description 含 Template 的 1 件已滤)</td></tr>
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

