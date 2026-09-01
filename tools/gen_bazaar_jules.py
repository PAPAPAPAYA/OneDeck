# -*- coding: utf-8 -*-
"""Generate The Bazaar Jules pool analysis HTML (StS2-series style) — Mobalytics source."""
import json, re, os
from bazaar_bridge import bridge_report, has_tag, has_word, tag_or_word, render_builds

SNAP = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'tools', 'outputs', 'bazaar', 'mobalytics_static_2026-08-31.json')
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'docs', 'Bazaar_Jules_PoolAnalysis_2026-08-31.html')

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
    pool = [i for i in all_items if 'Jules' in i['heroes'] and not is_template_item(i)]
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
    for kw in ['Heated', 'Chilled', 'Freeze', 'Charge', 'Haste', 'Slow', 'Burn', 'Poison', 'Shield', 'Heal', 'Regen', 'Crit', 'Multicast', 'Quest', 'Destroy']:
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
    rows.append('<html><head><meta charset="utf-8"><title>The Bazaar · Jules 物品池结构拆解</title><style>' + CSS + '</style></head><body>')
    rows.append('<h1>The Bazaar · Jules 物品池结构拆解</h1>\n<div class="sub">2026-08-31 · 数据源:<a href="https://mobalytics.gg/the-bazaar/jules-items">mobalytics.gg</a> TheBazaarStaticDataQuery(persistedQuery, cloudflareCacheVersion v1.0.59,快照 2026-08-31)· 全池 1207 物品中 Jules 专属 120 件(已过滤 1 件模板占位)· 物品英文为准 · 同系列:Vanessa / Pygmalien / Dooley / Mak / Karnok / Stelle / The Dragons / 公共池 / 技能池 / 综合</div>')

    rows.append('<div class="kpis">')
    rows.append(f'<div class="kpi"><div class="num">{N}</div><div class="lbl">物品池总数(120 件)</div></div>')
    rows.append(f'<div class="kpi"><div class="num">{tier_counts.get("Bronze",0)}/{tier_counts.get("Silver",0)}/{tier_counts.get("Gold",0)}/{tier_counts.get("Diamond",0)}</div><div class="lbl">tier 分布 B/S/G/D(+{tier_counts.get("Legendary",0)} Legendary)</div></div>')
    rows.append(f'<div class="kpi"><div class="num">{mech.get("Food",0)}</div><div class="lbl">Food 相关({round(mech.get("Food",0)/N*100)}%)—— 身份关键词</div></div>')
    rows.append(f'<div class="kpi"><div class="num">{mech.get("Heated",0)}/{mech.get("Chilled",0)}</div><div class="lbl">Heated {mech.get("Heated",0)} / Chilled {mech.get("Chilled",0)} —— 双态语汇</div></div>')
    rows.append('<div class="kpi"><div class="num">2+3</div><div class="lbl">主轴:食材×厨具 双态厨房引擎 + 灼烧 / 回复 / 冻结</div></div>')
    rows.append(f'<div class="kpi"><div class="num">{band_counts.get("passive",0)}</div><div class="lbl">无冷却被动件({round(band_counts.get("passive",0)/N*100)}%)</div></div>')
    rows.append('</div>')

    rows.append('''<h2>0. 英雄骨架</h2>
<div class="card">
<div class="lead"><strong>定位</strong>:全游「厨师」英雄——核心机制为「Stove/Cooler 双态厨房引擎」:英雄技能提供 Stove(加热槽)与 Cooler(冷冻槽)位,槽内物品获得 Heated/Chilled 状态并解锁第二效果;食材(Food 58,48%)与厨具(Tool 55)构成「备料→烹饪→出餐」体系。身份轴:Food 88 + Heated 36 + Chilled 21 + Tool 63 + Regen 31 + Shield 34 + Burn 32。</div>
<div class="lead"><strong>身份关键词(Top)</strong>:''' + ' '.join(f'<span class="badge b-tool">Food {mech["Food"]}</span><span class="badge">Heated {mech["Heated"]}</span><span class="badge b-aqua">Chilled {mech["Chilled"]}</span><span class="badge b-tool">Tool {mech["Tool"]}</span><span class="badge">Regen {mech["Regen"]}</span><span class="badge">Burn {mech["Burn"]}</span>') + '</div>')
    rows.append('<div class="lead"><strong>资源轴速览</strong>:Heated/Chilled = 双态条件(攻略确认槽位随机布置,「positioning extremely contextual」);Stove/Cooler = 英雄技能槽位;Grill / Oven / Dishwasher / Freezer = 加热/冷冻源;Regen + Shield = 食物的防御回复底盘;Burn = 灼烧副轴(Rice / Scorchpepper / Hot Sauce);Quest(巨型棒棒糖等)= 长线任务件。Jules 的语法 =「烹饪」——食材进槽,加热出效果。</div>')
    rows.append('</div>')

    rows.append('''<h2>1. 池组成总览</h2>
<div class="card">
<h3>1.1 tier × size</h3>
<table>
<tr><th>Tier</th><th>数量</th><th>占比</th><th>Small</th><th>Medium</th><th>Large</th><th>定位</th></tr>''')
    size_by_tier = {}
    for x in pool:
        size_by_tier.setdefault(tier_of(x), Counter())[x.get('size')] += 1
    desc = {'Bronze': '入门密度层——食材/厨具为主,教学层', 'Silver': '主力层——厨具链与双态件密集', 'Gold': '强度层——厨房引擎与 win 条件', 'Diamond': '封顶件层(Oven / Pantry)'}
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
    typerep = {'Food': 'Rice / Scorchpepper / Gingerbread House', 'Tool': 'Grill / Oven / Dishwasher / Freezer', 'Weapon': 'Cleaver / Chopsticks / Pizza Cutter', 'Property': "Farmer's Market / Pantry / Veggie Garden", 'Apparel': 'Oven Mitts / Apron', 'Vehicle': 'Froyo Cart', 'Friend': '—', 'Relic': '—', 'Aquatic': '—', 'Dragon': 'Dragon Steak / Dragonmelon', 'Trap': '—', 'Toy': '—'}
    badge = {'Weapon': 'b-weap', 'Tool': 'b-tool', 'Property': 'b-props', 'Friend': 'b-friend', 'Vehicle': 'b-veh'}
    for t in ['Food', 'Tool', 'Weapon', 'Property', 'Apparel', 'Vehicle', 'Friend', 'Relic', 'Aquatic', 'Dragon', 'Trap', 'Toy']:
        c = mech.get(t, 0)
        rows.append(f'<tr><td><span class="badge {badge.get(t, "t-silver")}">{t}</span></td><td class="mono">{c}</td><td class="mono">{round(c/N*100)}%</td><td>{typerep.get(t,"")}</td></tr>')
    rows.append('''</table>
<div class="note">本表取 tags 严格标签。Food 58(48%)+ Tool 55(46%) 是两大构成块——食材与厨具各占半壁;Weapon 27 是「厨刀」输出件;Dragon 标签是龙食材彩蛋。多标签物品重复计入各标签。</div>
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
<div class="note">Jules 节奏由「槽位→状态→触发」驱动:Heated 4 秒等窗口期使食物第二效果生效,厨具(Microwave / Egg Timer / Rice Cooker)负责充能与加速。双态引擎的节奏感接近「烹饪计时」——窗口内打完伤害。</div>
</div>''')

    rows.append('''<h2>2. 术语与分级结构</h2>
<div class="card">
<h3>2.1 机制词分布</h3>
<table><tr><th>机制词</th><th>件数</th><th>性质</th></tr>''')
    for k in ['Food', 'Tool', 'Heated', 'Chilled', 'Regen', 'Shield', 'Burn', 'Weapon', 'Charge', 'Haste', 'Freeze', 'Crit', 'Slow', 'Multicast', 'Quest', 'Destroy', 'Heal', 'Property', 'Poison', 'Apparel']:
        rows.append(f'<tr><td class="mono">{k}</td><td class="mono">{mech.get(k,0)}</td><td>—</td></tr>')
    rows.append('''</table>
<div class="note">词表为 tags + descriptions 关键词匹配(多标签重复计入)。Heated(36)+ Chilled(21)是 Jules 的引擎语汇——「双态」贯穿全池;Regen 31 全系列最高密度,是食物体系的防御底盘。</div>
</div>''')

    rows.append('''<div class="card">
<h3>2.2 触发句式</h3>
<table><tr><th>句式</th><th>件数</th><th>代表</th><th>设计含义</th></tr>''')
    def memc(kw):
        return sum(1 for x in pool if kw.lower() in desc_flat(x).lower())
    for kw in ['When you use a Food', 'Heated', 'When you use another Tool', 'When you use an adjacent', 'At the start of each fight', 'When you Slow']:
        rows.append(f'<tr><td>{kw}</td><td class="mono">{memc(kw)}</td><td>—</td><td>—</td></tr>')
    rows.append('</table></div>')

    rows.append('''<h2>3. 构筑轴识别</h2>
<div class="card">
<h3>3.1 食材×厨具双态引擎轴(身份轴)</h3>
<div class="flow">食材(Food 58)进 Stove/Cooler 槽 → Heated/Chilled 双态解锁第二效果 → 厨具链(Grill 加热+灼烧 / Oven 全体加热 / Microwave 全体 Haste / Egg Timer 充能) → 出餐(Rice 灼烧回复 / Cheese Wheel 巨盾)</div>
<div class="lead"><strong>入口</strong>:食材本身(Banu Leaves 用食物自充 / Scorchpepper 加热左件)。<strong>兑现</strong>:双态效果(Blueberry Pie 加热后灼烧、Burrito 加热 +20% 暴击)。<strong>封顶</strong>:Oven(Diamond,全体食物 Heated 4 秒 + Heated 件 +1 Multicast)、Dishwasher(Gold,Heat 工具武器 + Heated 件 +50% 伤)。</div>
<div class="lead"><strong>攻略注</strong>:Stove/Cooler 槽位随机布置——positioning extremely contextual,构筑的位置敏感度全系列最高。</div>
</div>
<div class="card">
<h3>3.2 回复/护盾轴(食物底盘)</h3>
<div class="flow">Regen 31 + Shield 34 → Basket(护盾转回复,核心件)→ Trail Mix(任务件)/ Cheese Wheel / Gingerbread House → Farmer's Market(地产,Regen 按最大生命加成)</div>
<div class="lead"><strong>特征</strong>:Regen 密度全系列最高——食物天然带回复,「防守反打」是 Jules 的默认节奏;Kripp Regen 构筑以 Basket 为 heart and soul。</div>
</div>
<div class="card">
<h3>3.3 灼烧副轴</h3>
<div class="flow">Rice(灼烧+回复双成长)→ Scorchpepper(加热触发)/ Hot Sauce / Black Pepper → Grill(用食物→灼烧,Heated 食物永久 +2)→ Skillet</div>
<div class="lead"><strong>特征</strong>:灼烧与双态深度绑定——加热源(Grill)既是状态引擎又是灼烧引擎,Kripp Burn 构筑是 Jules 最 consistent 的原型。</div>
</div>
<div class="card">
<h3>3.4 冻结/控制轴(小轴)</h3>
<div class="lead"><strong>特征</strong>:Chilled 21 + Freeze 11 —— Freezer(冷冻转伤 win 条件)/ Blender / Snowstorm(技能);Kripp Freeze 构筑定位为 pivot 选项而非从头构筑。</div>
</div>
<div class="card">
<h3>3.5 轴矩阵总结</h3>
<table>
<tr><th>轴</th><th>件数</th><th>tier 分布</th><th>入口/兑现/封顶</th><th>密度评价</th></tr>''')
    jules_axes = {
        '食物': lambda i: has_tag(i, 'Food'),
        '厨具': lambda i: has_tag(i, 'Tool'),
        '加热': lambda i: has_word(i, r'Heated'),
        '冷冻': lambda i: has_word(i, r'Chilled|Freeze'),
        '回复': lambda i: has_word(i, r'Regen|Shield|Heal'),
        '灼烧': lambda i: has_word(i, r'Burn'),
        '输出': lambda i: has_tag(i, 'Weapon') or has_word(i, r'Deal \d|Damage'),
    }
    a1 = [x for x in pool if jules_axes['食物'](x)]
    a2 = [x for x in pool if jules_axes['厨具'](x)]
    a3 = [x for x in pool if jules_axes['加热'](x)]
    a4 = [x for x in pool if jules_axes['冷冻'](x)]
    a5 = [x for x in pool if jules_axes['回复'](x)]
    a6 = [x for x in pool if jules_axes['灼烧'](x)]
    a7 = [x for x in pool if jules_axes['输出'](x)]
    def tierdist(lst):
        c = Counter(tier_of(x) for x in lst)
        return f'B{c.get("Bronze",0)}/S{c.get("Silver",0)}/G{c.get("Gold",0)}/D{c.get("Diamond",0)}+{c.get("Legendary",0)}L'
    rows.append('<tr><td>食材×厨具</td><td class="mono">%d</td><td>%s</td><td>食材进槽 → 双态效果 → 厨具放大</td><td class="good">厚,身份轴</td></tr>' % (len(a1)+len(a2), tierdist(a1+a2)))
    rows.append('<tr><td>加热态</td><td class="mono">%d</td><td>%s</td><td>Heated 第二效果</td><td class="good">厚,引擎语汇</td></tr>' % (len(a3), tierdist(a3)))
    rows.append('<tr><td>冷冻态</td><td class="mono">%d</td><td>%s</td><td>Chilled/冻结控制</td><td class="warn">中</td></tr>' % (len(a4), tierdist(a4)))
    rows.append('<tr><td>回复</td><td class="mono">%d</td><td>%s</td><td>Regen/盾底盘</td><td class="good">厚,全系列最高 Regen 密度</td></tr>' % (len(a5), tierdist(a5)))
    rows.append('<tr><td>灼烧</td><td class="mono">%d</td><td>%s</td><td>状态副轴</td><td class="warn">中</td></tr>' % (len(a6), tierdist(a6)))
    rows.append('<tr><td>输出</td><td class="mono">%d</td><td>%s</td><td>厨刀武器</td><td class="warn">中</td></tr>' % (len(a7), tierdist(a7)))
    rows.append('</table></div>')

    # ===== 3.6 bridge matrix (2026-08-31 framework) =====
    bridge_html, bridge_stats = bridge_report(
        pool, jules_axes,
        closed_loop_desc="食材入口(Scorchpepper 加热左件) → 双态×回复(Blueberry Pie 加热后灼烧 / Cheese Wheel 巨盾) → 厨具×加热(Grill 用食物→灼烧 + 永久叠伤) → 回复×地产(Farmer's Market Regen 按最大生命) → 输出×厨具(Cleaver 一击线):Stove/Cooler 槽位是全部轴的物理交汇点——食物、厨具、双态在同一格上相遇。"
    )
    rows.append(bridge_html)

    rows.append('''<h2>4. 两段式审计(条件制造 → 兑现)</h2>
<div class="card">
<table>
<tr><th>层</th><th>设计职责</th><th>代表件</th><th>tier 观察</th></tr>
<tr><td><strong>入口层</strong>(制造条件/供给)</td><td>食材进槽、加热/冷冻源、充能厨具</td><td>Scorchpepper / Grill / Microwave / Egg Timer</td><td>B/S 为主</td></tr>
<tr><td><strong>兑现层</strong>(消费条件)</td><td>双态第二效果、食物状态引擎</td><td>Blueberry Pie / Burrito / Rice / Cheese Wheel</td><td>B/S 为主</td></tr>
<tr><td><strong>封顶层</strong>(条件 → 乘区)</td><td>全体双态化、Multicast 加成、冷冻转伤</td><td>Oven(D) / Dishwasher / Freezer / Pantry(D)</td><td>G/D 集中</td></tr>
</table>
<div class="verdict"><strong>结论</strong>:Jules 的两段式是「窗口式」:加热/冷冻源开启状态窗口(条件),窗口内的第二效果(兑现)结算——「Heated 4 秒」是全系列最典型的「限时状态」语法。Stove/Cooler 槽位把入口件物理地约束在特定格上,这是 Bazaar 独有的「位形条件」(见 §6)。</div>
</div>''')

    rows.append('''<h2>5. 强度阶梯(高 tier = 高强度?)</h2>
<div class="card">
<h3>5.1 Gold(25)与 Diamond(2)</h3>
<table>
<tr><th>件</th><th>tier</th><th>size</th><th>强度认定</th></tr>''')
    for x in sorted((x for x in pool if tier_of(x) in ('Gold', 'Diamond')), key=lambda z: tier_of(z)):
        rows.append(f'<tr><td>{x["name"]}</td><td><span class="badge t-{tier_of(x).lower()}">{tier_of(x)}</span></td><td>{x.get("size")}</td><td>{esc(" ".join(desc_of(x))[:110])}</td></tr>')
    rows.append('''</table>
<div class="verdict"><strong>结论</strong>:高 tier ≠ 必然强——Diamond 双件(Oven 全体加热引擎 / Pantry 地产)是「厨房系统件」;Gold 25 件承载 win 条件(Freezer 冷冻转伤 / Grill 灼烧引擎 / Giant Lollipop 任务件)。tier 表达「获取难度 + 厨房系统位」。</div>
</div>''')

    # ===== 5.5 typical builds (2026-09-01 framework, v2 detailed) =====
    skills = data['skills']
    jules_builds = [
        dict(name='Burn 灼烧厨房', source='<a href="https://mobalytics.gg/the-bazaar/builds/burn-jules-kripp">Mobalytics / Kripp</a>', date='2026-06-29', grade='<span class="badge t-diamond">最稳定原型</span>',
             logic='灼烧与双态天然一体——Rice 灼烧/回复双成长、Grill 用食物灼烧且 Heated 食物永久 +2;Bronze 级就有高效件,从头到尾路线平滑,是 Jules 最 consistent 的原型。',
             items=['Microwave', 'Rice', 'Scorchpepper', 'Coffee', 'Hot Sauce', 'Black Pepper', 'Butter', 'Grill', 'Gingerbread House', 'Cheese Wheel'],
             skills=['Immolating Spark', 'Final Flame'],
             note='Microwave 是食物构筑的 Haste 首选(低冷却全板效果,占第一个 Stove);Imu(英雄技能 2)可替换 Stove 与多种核心组合(Hotbox / Oven / Spice Rack);攻略板位备注 Zarlic 应换成 Sleeping Potion 以充能 Honeycomb。'),
        dict(name='Freeze 冻结控制', source='<a href="https://mobalytics.gg/the-bazaar/builds/freeze-jules-kripp">Mobalytics / Kripp</a>', date='2026-06-14', grade='<span class="badge t-gold">pivot 型 win 条件</span>',
             logic='少数「克制主流」的原型——Walk-In Freezer(快照名 Freezer)把冻结转成伤害,既是硬控又是 win 条件;Day 6+ 金件到位才真正成型,定位为机会主义 pivot。',
             items=['Freezer', 'Blender', 'Pizza', 'Banu Leaves', 'Sorbet', 'Instant Noodles'],
             skills=['Snowstorm'],
             note='配低冷却/多段食物最大化冻结触发;早期槽位件(Butter / Strawberries)可平滑过渡。'),
        dict(name='Regen 回复反打', source='<a href="https://mobalytics.gg/the-bazaar/builds/regen-jules-kripp">Mobalytics / Kripp</a>', date='2026-05-09', grade='<span class="badge t-silver">防守成长型</span>',
             logic='Basket 是「heart and soul」——护盾快速转回复;Trail Mix 双任务完成后质变;Rice / Prep Station 提供进攻 win 条件,避免纯被动拖后期。',
             items=['Basket', 'Trail Mix', 'Microwave', 'Rice', 'Prep Station', "Farmer's Market"],
             skills=[],
             note="Farmer's Market 无限连携是攻略 10 胜板核心;Microwave + Rice 组合需要 Scorchpepper 启用,否则用 Coffee 替代 Microwave。"),
        dict(name='Weapon 厨刀速攻', source='<a href="https://mobalytics.gg/the-bazaar/builds/weapon-jules-kripp">Mobalytics / Kripp</a>', date='2026-04-15', grade='<span class="badge t-silver">节奏压制型</span>',
             logic='前中期压制——Cleaver 是最强早期兑现(长战可一击),Carving Fork / Knife Set 全板 +伤,Chopsticks 以 Multicast 变现所有加成;Dishwasher 与跨英雄武器让它能撑到后期。',
             items=['Cleaver', 'Carving Fork', 'Knife Sharpener', 'Chopsticks', 'Cooking Mallet', 'Dishwasher', 'Meat Grinder', 'Veggie Garden'],
             skills=['Left-Handed', 'Right-Handed', 'Strength'],
             note='优先早期伤害技能;2 级奖励建议拿铜技能而非附魔物品;Cooking Mallet 需要任务,可在 PvE 回合换食物进场启用。'),
        dict(name='Giant Lollipop 巨型棒棒糖', source='<a href="https://mobalytics.gg/the-bazaar/builds/giant-lollipop-jules-kripp">Mobalytics / Kripp</a>', date='2026-04-12', grade='<span class="badge t-gold">任务型 beatdown</span>',
             logic='双任务(用 100 食物 + 慢 80 次)完成后质变的重型件;通常先玩灼烧再转,Excellent Vintage 加速任务;必须占一个 Cooler 槽。',
             items=['Giant Lollipop', 'Excellent Vintage', 'Honeycomb', 'Dishwasher', 'Serving Platter', 'Cookbook', 'Dragonmelon', 'Rice', 'Skillet'],
             skills=[],
             note='Jules 唯一的 Quest 型构筑——任务件设计与 OneDeck 的「累计条件」句式可对照。'),
    ]
    rows.append(render_builds('Jules', pool, data['items'], skills, jules_axes, jules_builds,
        source_note='来源:Mobalytics Builds(Kripparrian,Jules 五原型全覆盖);物品/技能效果取自 Mobalytics 快照(2026-08-31,cloudflareCacheVersion v1.0.59);轴映射按 §3.6 谓词自动计算。攻略反复强调 Stove/Cooler 槽位随机布置导致位置敏感——构筑稳定性以 Burn 最高、Freeze/Lollipop 最低。'))

    rows.append('''<h2>6. 与 StS2 / OneDeck 的映射</h2>
<div class="card">
<h3>6.1 与 StS2 的结构异同</h3>
<table><tr><th>维度</th><th>StS2(五角色)</th><th>Bazaar(Jules)</th><th>差异判定</th></tr>
<tr><td>入口层</td><td>Common 无能力卡,即打即用</td><td>Bronze 食材/小厨具</td><td>同工:入口=制造条件</td></tr>
<tr><td>兑现层</td><td>Uncommon 桥层</td><td>Silver 双态件与厨具链</td><td>同工:桥在中间 tier</td></tr>
<tr><td>封顶</td><td>Rare 大数字/引擎</td><td>Diamond 厨房系统件(Oven/Pantry)</td><td>同工:高 tier 承载系统化引擎</td></tr>
<tr><td>资源经济</td><td>垃圾经济学四形态</td><td>食材→双态→出餐 窗口经济</td><td>同类,Jules 的「状态窗口」= StS2 的「回合内爆发」</td></tr>
<tr><td>运行</td><td>回合制/能量</td><td>实时秒表/冷却/空间</td><td>差异最大</td></tr>
</table>
<h3>6.2 OneDeck 落点(初筛)</h3>
<table><tr><th>Bazaar 观察</th><th>OneDeck 可借鉴</th><th>不可搬</th></tr>
<tr><td>Heated/Chilled 双态窗口(限时状态第二效果)</td><td><b>OneDeck 的「状态期间」句式(如 Rest 跳过)可对照「窗口内增益」</b>——限时第二效果是状态系统(Infected/Mana/Rest)的扩展方向</td><td>秒表窗口不能直接映射轮次制;可改为「N 次揭晓内」</td></tr>
<tr><td>Stove/Cooler 物理槽位</td><td>OneDeck 无板面位置概念——不建议引入</td><td>位形条件与 OneDeck 的事件模型冲突</td></tr>
<tr><td>Quest 任务件(Giant Lollipop:用 100 食物)</td><td>OneDeck 的累计计数句式(墓地计数/信徒计数)已是同类——「跨回合任务」是可选扩展</td><td>—</td></tr>
<tr><td>Regen 密度全系列最高</td><td>OneDeck 无每秒回复概念——对照「回合开始回复」句式</td><td>—</td></tr>
</table>
</div>''')

    rows.append('''<h2>7. 关键卡清单</h2>
<div class="card"><table>
<tr><th>轴</th><th>物品</th><th>tier</th><th>size</th><th>cd/弹药</th><th>effects 摘要</th></tr>''')
    keycards = [
        ('食材', 'Rice', 't-bronze'), ('食材', 'Scorchpepper', 't-bronze'), ('食材', 'Banu Leaves', 't-bronze'),
        ('食材', 'Blueberry Pie', 't-bronze'), ('食材', 'Burrito', 't-silver'), ('食材', 'Cheese Wheel', 't-silver'),
        ('食材', 'Gingerbread House', 't-gold'), ('食材', 'Dragonmelon', 't-gold'),
        ('厨具', 'Grill', 't-gold'), ('厨具', 'Microwave', 't-silver'), ('厨具', 'Egg Timer', 't-gold'),
        ('厨具', 'Rice Cooker', 't-bronze'), ('厨具', 'Pizza Cutter', 't-silver'), ('厨具', 'Dishwasher', 't-gold'),
        ('封顶', 'Oven', 't-diamond'), ('封顶', 'Pantry', 't-diamond'), ('封顶', 'Freezer', 't-gold'),
        ('回复', 'Basket', 't-bronze'), ('回复', 'Trail Mix', 't-silver'), ('回复', "Farmer's Market", 't-gold'),
        ('输出', 'Cleaver', 't-silver'), ('输出', 'Chopsticks', 't-silver'),
        ('任务', 'Giant Lollipop', 't-gold'), ('地产', "Farmer's Market", 't-gold'),
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

    rows.append('''<h2>8. 文档元信息</h2>
<div class="card">
<table>
<tr><th>项</th><th>值</th></tr>
<tr><td>数据快照</td><td class="mono">2026-08-31 mobalytics.gg TheBazaarStaticDataQuery(persistedQuery, cloudflareCacheVersion v1.0.59),全池 1207 件提取 Jules 专属 120</td></tr>
<tr><td>归属规则</td><td>heroes[] 含 Jules 且非模板占位(description 含 Template 的 1 件已滤)</td></tr>
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

