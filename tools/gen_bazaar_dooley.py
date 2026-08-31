# -*- coding: utf-8 -*-
"""Generate The Bazaar Dooley pool analysis HTML (StS2-series style) — Mobalytics source."""
import json, re, os
from bazaar_bridge import bridge_report, has_tag, has_word, tag_or_word

SNAP = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'tools', 'outputs', 'bazaar', 'mobalytics_static_2026-08-31.json')
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'docs', 'Bazaar_Dooley_PoolAnalysis_2026-08-31.html')

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
        s = re.sub(r'\{\{::([0-9]+)(:[^}]*)?\}\}', r'\1', s)
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
    s = re.sub(r'\{\{::([0-9]+)(:[^}]*)?\}\}', r'\1', s)
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
    pool = [i for i in all_items if 'Dooley' in i['heroes'] and not is_template_item(i)]
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
    for kw in ['Weapon', 'Tool', 'Property', 'Toy', 'Relic', 'Apparel', 'Vehicle', 'Friend', 'Core', 'Food', 'Tech', 'Trap', 'Instrument', 'Aquatic', 'Dinosaur', 'Ray', 'Drone']:
        mech[kw] = sum(1 for x in pool if kw in (x.get('tags') or []))
    for kw in ['Charge', 'Haste', 'Slow', 'Burn', 'Freeze', 'Poison', 'Shield', 'Heal', 'Crit', 'Multicast', 'Flying', 'Destroy']:
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
    rows.append('<html><head><meta charset="utf-8"><title>The Bazaar · Dooley 物品池结构拆解</title><style>' + CSS + '</style></head><body>')
    rows.append('<h1>The Bazaar · Dooley 物品池结构拆解</h1>\n<div class="sub">2026-08-31 · 数据源:<a href="https://mobalytics.gg/the-bazaar/dooley-items">mobalytics.gg</a> TheBazaarStaticDataQuery(persistedQuery, cloudflareCacheVersion v1.0.59,快照 2026-08-31)· 全池 1207 物品中 Dooley 专属 143 件(已过滤 1 件模板占位)· 物品英文为准 · 同系列:Vanessa / Pygmalien / Mak / Karnok / Jules / Stelle / The Dragons / 公共池 / 技能池 / 综合</div>')

    rows.append('<div class="kpis">')
    rows.append(f'<div class="kpi"><div class="num">{N}</div><div class="lbl">物品池总数(143 件)</div></div>')
    rows.append(f'<div class="kpi"><div class="num">{tier_counts.get("Bronze",0)}/{tier_counts.get("Silver",0)}/{tier_counts.get("Gold",0)}/{tier_counts.get("Diamond",0)}</div><div class="lbl">tier 分布 B/S/G/D(+{tier_counts.get("Legendary",0)} Legendary)</div></div>')
    rows.append(f'<div class="kpi"><div class="num">{mech.get("Tech",0)}</div><div class="lbl">Tech 相关({round(mech.get("Tech",0)/N*100)}%)—— 身份关键词</div></div>')
    rows.append(f'<div class="kpi"><div class="num">{mech.get("Charge",0)}</div><div class="lbl">Charge 相关({round(mech.get("Charge",0)/N*100)}%)—— 核心引擎语汇</div></div>')
    rows.append('<div class="kpi"><div class="num">3+3</div><div class="lbl">主轴:核心→链引擎 / 友军 / 技术 + 恐龙 / 车辆 / 射线</div></div>')
    rows.append(f'<div class="kpi"><div class="num">{band_counts.get("passive",0)}</div><div class="lbl">无冷却被动件({round(band_counts.get("passive",0)/N*100)}%)</div></div>')
    rows.append('</div>')

    rows.append('''<h2>0. 英雄骨架</h2>
<div class="card">
<div class="lead"><strong>定位</strong>:全游「机器人工程师」英雄——核心机制为「核心→链引擎」:12 件 Core 都是「使用左侧件→充能自身/右侧」的自动机,把整条物品链串成持续自循环。身份轴:Tech 71 + Charge 54 + Friend 39 + Core 12 + Ray 5 + Dinosaur 9 + Vehicle 10。</div>
<div class="lead"><strong>身份关键词(Top)</strong>:''' + ' '.join(f'<span class="badge b-tool">Tech {mech["Tech"]}</span><span class="badge">Charge {mech["Charge"]}</span><span class="badge b-friend">Friend {mech["Friend"]}</span><span class="badge">Core {mech["Core"]}</span><span class="badge b-weap">Weapon {mech["Weapon"]}</span><span class="badge">Dinosaur {mech["Dinosaur"]}</span>') + '</div>')
    rows.append('<div class="lead"><strong>资源轴速览</strong>:Core = 核心引擎(使用左件→充能);Charge = 充能(加速冷却的通用语汇);Friend = 友军(机器人/恐龙随从);Tech = 技术标签;Ray = 射线族(5 件互充);Dinosaur = 恐龙随从;Vehicle = 车辆(Combat Core 等)。「核心→右侧链」是 Dooley 版「自动机链」——比 StS2 任何角色都更接近 OneDeck 的「事件自动机」。</div>')
    rows.append('</div>')

    rows.append('''<h2>1. 池组成总览</h2>
<div class="card">
<h3>1.1 tier × size</h3>
<table>
<tr><th>Tier</th><th>数量</th><th>占比</th><th>Small</th><th>Medium</th><th>Large</th><th>定位</th></tr>''')
    size_by_tier = {}
    for x in pool:
        size_by_tier.setdefault(tier_of(x), Counter())[x.get('size')] += 1
    desc = {'Bronze': '入门密度层——小型武器/工具/友军为主,教学层', 'Silver': '主力层——核心件与 Tech 密集', 'Gold': '强度层——战斗核心与引擎', 'Diamond': '封顶件层(4 件含 Legendary)'}
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
    typerep = {'Tech': 'Fiber Optics / Cooling Fan / Charging Station', 'Weapon': 'Railgun / Plasma Rifle / Kinetic Cannon', 'Friend': 'Bellelista / Clawrence / Dooltron', 'Core': 'Combat Core / The Core / Companion Core', 'Tool': 'Hacksaw / Forklift / Crane', 'Vehicle': 'Bill Dozer / Race Carl / Dooltron', 'Dinosaur': 'Momma-Saur / Tanky Anky / Terry-Dactyl', 'Ray': 'Alpha Ray / Beta Ray / Gamma Ray', 'Relic': 'Dino Saddle / Primal Core / Rex Spex', 'Property': 'Robotic Factory / Unstable Grav Well', 'Apparel': "Dooley's Scarf / Dino Disguise", 'Food': '—', 'Toy': '—'}
    badge = {'Weapon': 'b-weap', 'Tool': 'b-tool', 'Property': 'b-props', 'Friend': 'b-friend', 'Vehicle': 'b-veh'}
    for t in ['Tech', 'Weapon', 'Friend', 'Core', 'Tool', 'Vehicle', 'Dinosaur', 'Ray', 'Relic', 'Property', 'Apparel', 'Food', 'Toy']:
        c = mech.get(t, 0)
        rows.append(f'<tr><td><span class="badge {badge.get(t, "t-silver")}">{t}</span></td><td class="mono">{c}</td><td class="mono">{round(c/N*100)}%</td><td>{typerep.get(t,"")}</td></tr>')
    rows.append('''</table>
<div class="note">本表取 tags 严格标签。Tech 71(50%)+ Charge 54(38%) 是最大构成块;Friend 39 是「友军/机器人」底盘;Core 12 是引擎件。多标签物品重复计入各标签。</div>
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
<div class="note">Dooley 节奏主力 4-9s(74+35=109 件),配合 Charge 语汇(54 件)让冷却可被加速——「充能」是 Dooley 的节奏引擎。44 件多值冷却(升级即加速)也全游最高比例之一。</div>
</div>''')

    rows.append('''<h2>2. 术语与分级结构</h2>
<div class="card">
<h3>2.1 机制词分布</h3>
<table><tr><th>机制词</th><th>件数</th><th>性质</th></tr>''')
    for k in ['Tech', 'Charge', 'Friend', 'Core', 'Weapon', 'Tool', 'Haste', 'Shield', 'Burn', 'Slow', 'Freeze', 'Poison', 'Multicast', 'Ray', 'Dinosaur', 'Vehicle', 'Flying', 'Destroy', 'Heal', 'Crit']:
        rows.append(f'<tr><td class="mono">{k}</td><td class="mono">{mech.get(k,0)}</td><td>—</td></tr>')
    rows.append('''</table>
<div class="note">词表为 tags + descriptions 关键词匹配(多标签重复计入)。Charge(54)是 Dooley 的引擎语汇——几乎每件核心件都以「充能」为接口,与 StS2 的「能量/降费」异曲同工。</div>
</div>''')

    rows.append('''<div class="card">
<h3>2.2 触发句式</h3>
<table><tr><th>句式</th><th>件数</th><th>代表</th><th>设计含义</th></tr>''')
    def memc(kw):
        return sum(1 for x in pool if kw.lower() in desc_flat(x).lower())
    for kw in ['When you use any item to the left', 'When you use another Friend', 'Charge this', 'At the start of each fight', 'When you use a Core', 'When an enemy uses']:
        rows.append(f'<tr><td>{kw}</td><td class="mono">{memc(kw)}</td><td>—</td><td>—</td></tr>')
    rows.append('</table></div>')

    rows.append('''<h2>3. 构筑轴识别</h2>
<div class="card">
<h3>3.1 核心→链引擎轴(身份轴)</h3>
<div class="flow">Core(Combat / The Core / Weaponized Core)「使用左侧件→充能自身/右侧」 → 物品链(任意件在左触发) → 充能加速(Critical Core 暴击自充 / Focused Core 右件充能) → 引擎闭环</div>
<div class="lead"><strong>入口</strong>:Core 件本身(12 件),以及任何「左侧触发」的物品。<strong>兑现</strong>:核心输出(Combat Core 50 伤+50 盾、The Core 充能右件)。<strong>封顶</strong>:Diamond 引擎(Robotic Factory 全体 Friend +1 Multicast、Oblivion Core 摧毁敌人+自充)。</div>
<div class="lead"><strong>桥</strong>:C.O.R.A(敌毒→伤 / 用右件→毒)、Primal Core(恐龙/遗物充能)、Focused Core(右件充能+提速)。「核心→右侧链」是 Dooley 版自动机链——比 StS2 任何角色更接近 OneDeck 的事件自动机。</div>
</div>
<div class="card">
<h3>3.2 友军/恐龙轴</h3>
<div class="flow">Companion Core(友军充能) → Friend 武器(Bellelista / Clawrence / Dooltron) → Dinosaur 随从(Momma-Saur / Tanky Anky / Terry-Dactyl) → Robotic Factory(全体 +1 Multicast)</div>
<div class="lead"><strong>密度</strong>:Friend 39(27%)+ Dinosaur 9(6%)。Friend 件多是「战斗型友军」(武器+Friend 19 件),随从流可独立成军。</div>
</div>
<div class="card">
<h3>3.3 射线轴(小轴)</h3>
<div class="flow">Alpha Ray(用 Core/Ray→武器+伤) → Beta Ray(用 Core/另一 Ray→自充) → Gamma/Epsilon/Omega Ray</div>
<div class="lead"><strong>特征</strong>:5 件 Ray 互充互养——「用任一 Core 或 Ray 触发另一 Ray」的小型自动机族,配合核心轴极佳。</div>
</div>
<div class="card">
<h3>3.4 状态/战斗轴(次轴)</h3>
<div class="lead"><strong>特征</strong>:Burn 31 + Poison 14 + Freeze 15 + Shield 31 —— Dooley 有大量状态武器(Plasma Rifle / Flamethrower / C.O.R.A)与护盾,是「技术向战斗」。</div>
</div>
<div class="card">
<h3>3.5 轴矩阵总结</h3>
<table>
<tr><th>轴</th><th>件数</th><th>tier 分布</th><th>入口/兑现/封顶</th><th>密度评价</th></tr>''')
    dooley_axes = {
        '核心': lambda i: has_tag(i, 'Core'),
        '友军': lambda i: has_tag(i, 'Friend'),
        '射线': lambda i: has_tag(i, 'Ray'),
        '恐龙': lambda i: has_tag(i, 'Dinosaur'),
        '车辆': lambda i: has_tag(i, 'Vehicle'),
        '技术': lambda i: has_tag(i, 'Tech'),
        '状态': lambda i: has_word(i, r'Burn|Poison|Freeze|Slow'),
    }
    eco = [x for x in pool if dooley_axes['核心'](x)]
    swing = [x for x in pool if dooley_axes['友军'](x)]
    toyprop = [x for x in pool if dooley_axes['射线'](x)]
    relic = [x for x in pool if dooley_axes['恐龙'](x) or dooley_axes['车辆'](x)]
    combat = [x for x in pool if dooley_axes['状态'](x)]
    def tierdist(lst):
        c = Counter(tier_of(x) for x in lst)
        return f'B{c.get("Bronze",0)}/S{c.get("Silver",0)}/G{c.get("Gold",0)}/D{c.get("Diamond",0)}+{c.get("Legendary",0)}L'
    rows.append('<tr><td>核心→链</td><td class="mono">%d</td><td>%s</td><td>Core 触发链 → 充能 → 引擎闭环</td><td class="good">厚,身份轴</td></tr>' % (len(eco), tierdist(eco)))
    rows.append('<tr><td>友军/恐龙</td><td class="mono">%d</td><td>%s</td><td>友军随从 → 战斗输出</td><td class="good">厚,身份轴 B</td></tr>' % (len(swing), tierdist(swing)))
    rows.append('<tr><td>射线</td><td class="mono">%d</td><td>%s</td><td>Ray 互充互养</td><td class="warn">小轴,配合核心</td></tr>' % (len(toyprop), tierdist(toyprop)))
    rows.append('<tr><td>恐龙/车辆</td><td class="mono">%d</td><td>%s</td><td>随从+载具</td><td class="warn">中</td></tr>' % (len(relic), tierdist(relic)))
    rows.append('<tr><td>状态战斗</td><td class="mono">%d</td><td>%s</td><td>Burn/毒/冻/盾</td><td class="warn">中</td></tr>' % (len(combat), tierdist(combat)))
    rows.append('</table></div>')

    # ===== 3.6 bridge matrix (2026-08-31 framework) =====
    bridge_html, bridge_stats = bridge_report(
        pool, dooley_axes,
        closed_loop_desc='核心入口(Combat Core 触发链) → 核心×技术(The Core 充能右件) → 友军×技术(Companion Core 友军充能) → 恐龙×技术(Primal Core 恐龙/遗物充能) → 射线×技术(Alpha Ray 用 Core/Ray→武器+伤):核心与技术是 Dooley 的两大枢纽,桥把友军/恐龙/射线全部串进充能引擎。'
    )
    rows.append(bridge_html)

    rows.append('''<h2>4. 两段式审计(条件制造 → 兑现)</h2>
<div class="card">
<table>
<tr><th>层</th><th>设计职责</th><th>代表件</th><th>tier 观察</th></tr>
<tr><td><strong>入口层</strong>(制造条件/供给)</td><td>Core 触发、充能、友军生成</td><td>Combat Core / The Core / Companion Core</td><td>B/S 为主</td></tr>
<tr><td><strong>兑现层</strong>(消费条件)</td><td>核心输出、友军战斗、状态武器</td><td>C.O.R.A / Bellelista / Plasma Rifle</td><td>S/G 为主</td></tr>
<tr><td><strong>封顶层</strong>(条件 → 乘区)</td><td>全局 Multicast、摧毁引擎、复制件</td><td>Robotic Factory / Oblivion Core / 3D Printer</td><td>D(+L) 集中</td></tr>
</table>
<div class="verdict"><strong>结论</strong>:Dooley 的两段式是「引擎式」:入口件(Core 触发链)制造充能,兑现件(核心输出/友军)消费。其「使用左侧→充能右侧」的自动机语法,比 StS2 的「事件自动机」(每回合 4-6 张)更直接——这几乎是 Bazaar 对 OneDeck「事件自动机」的最佳对应物(见 §6)。</div>
</div>''')

    rows.append('''<h2>5. 强度阶梯(高 tier = 高强度?)</h2>
<div class="card">
<h3>5.1 Gold(39)与 Diamond(4,含 1 Legendary)</h3>
<table>
<tr><th>件</th><th>tier</th><th>size</th><th>强度认定</th></tr>''')
    for x in sorted((x for x in pool if tier_of(x) in ('Gold', 'Diamond')), key=lambda z: tier_of(z)):
        rows.append(f'<tr><td>{x["name"]}</td><td><span class="badge t-{tier_of(x).lower()}">{tier_of(x)}</span></td><td>{x.get("size")}</td><td>{esc(" ".join(desc_of(x))[:110])}</td></tr>')
    rows.append('''</table>
<div class="verdict"><strong>结论</strong>:高 tier ≠ 必然强——Diamond/Legendary 件(3D Printer 复制 / Robotic Factory 全局 +1 Multicast / Oblivion Core 摧毁引擎 / Dino Saddle 车辆 Multicast)都是「引擎级」而非纯数值;Gold 的战斗核心(Combat Core / C.O.R.A)在链引擎里才最强。tier 表达「获取难度 + 引擎潜力」。</div>
</div>''')

    rows.append('''<h2>6. 与 StS2 / OneDeck 的映射</h2>
<div class="card">
<h3>6.1 与 StS2 的结构异同</h3>
<table><tr><th>维度</th><th>StS2(五角色)</th><th>Bazaar(Dooley)</th><th>差异判定</th></tr>
<tr><td>入口层</td><td>Common 无能力卡,即打即用</td><td>Bronze 触发件/友军为主</td><td>同工:入口=制造条件</td></tr>
<tr><td>兑现层</td><td>Uncommon 桥层</td><td>Silver 核心件+Tech 密集</td><td>同工:桥在中间 tier</td></tr>
<tr><td>封顶</td><td>Rare 大数字/引擎</td><td>Diamond/Legendary 全局引擎(复制/摧毁/+1 Multicast)</td><td>同工:高 tier 承载引擎</td></tr>
<tr><td>资源经济</td><td>垃圾经济学四形态</td><td>充能经济(Charge = 通用加速语汇)</td><td>同类,Dooley 的「充能」= StS2 的「降费/能量」</td></tr>
<tr><td>运行</td><td>回合制/能量</td><td>实时秒表/冷却/空间</td><td>差异最大</td></tr>
</table>
<h3>6.2 OneDeck 落点(初筛)</h3>
<table><tr><th>Bazaar 观察</th><th>OneDeck 可借鉴</th><th>不可搬</th></tr>
<tr><td>核心→链引擎(使用左件→充能右件)</td><td><b>这是全 Bazaar 最接近 OneDeck「事件自动机」的结构</b>——OneDeck 的「使用→触发→充能」事件链完全同构,可对照其「链长→收益」设计</td><td>秒表实时冷却不能映射轮次制</td></tr>
<tr><td>Charge 通用语汇(54 件)</td><td>OneDeck 的「充能/加速」类机制(若有)可参考其「万物皆可充能」的统一接口</td><td>—</td></tr>
<tr><td>Friend 随从 + 战斗</td><td>OneDeck 的「友军」概念已有,可参考随从流密度</td><td>—</td></tr>
<tr><td>射线互充族(Ray 5 件)</td><td>OneDeck 的「同类互触发」家族(如诅咒/裂缝)已有,可对照小自动机族设计</td><td>—</td></tr>
</table>
</div>''')

    rows.append('''<h2>7. 关键卡清单</h2>
<div class="card"><table>
<tr><th>轴</th><th>物品</th><th>tier</th><th>size</th><th>cd/弹药</th><th>effects 摘要</th></tr>''')
    keycards = [
        ('核心', 'Combat Core', 't-gold'), ('核心', 'The Core', 't-silver'), ('核心', 'Weaponized Core', 't-silver'),
        ('核心', 'Focused Core', 't-silver'), ('核心', 'Ignition Core', 't-silver'), ('核心', 'Armored Core', 't-silver'),
        ('引擎', 'C.O.R.A', 't-gold'), ('引擎', 'Robotic Factory', 't-diamond'), ('引擎', 'Oblivion Core', 't-gold'),
        ('引擎', '3D Printer', 't-diamond'), ('引擎', 'Dino Saddle', 't-diamond'),
        ('友军', 'Companion Core', 't-silver'), ('友军', 'Bellelista', 't-gold'), ('友军', 'Clawrence', 't-silver'),
        ('恐龙', 'Momma-Saur', 't-gold'), ('恐龙', 'Tanky Anky', 't-gold'), ('恐龙', 'Terry-Dactyl', 't-gold'),
        ('射线', 'Alpha Ray', 't-silver'), ('射线', 'Beta Ray', 't-silver'),
        ('战斗', 'Plasma Rifle', 't-silver'), ('战斗', 'Flamethrower', 't-gold'),
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
<tr><td>数据快照</td><td class="mono">2026-08-31 mobalytics.gg TheBazaarStaticDataQuery(persistedQuery, cloudflareCacheVersion v1.0.59),全池 1207 件提取 Dooley 专属 143</td></tr>
<tr><td>归属规则</td><td>heroes[] 含 Dooley 且非模板占位(description 含 Template 的 1 件已滤)</td></tr>
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

