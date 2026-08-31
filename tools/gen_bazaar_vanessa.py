# -*- coding: utf-8 -*-
"""Generate The Bazaar Vanessa pool analysis HTML (StS2-series style) — Mobalytics source."""
import json, re, os
from bazaar_bridge import bridge_report, has_tag, has_word, tag_or_word

SNAP = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'tools', 'outputs', 'bazaar', 'mobalytics_static_2026-08-31.json')
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'docs', 'Bazaar_Vanessa_PoolAnalysis_2026-08-31.html')

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
    pool = [i for i in all_items if 'Vanessa' in i['heroes'] and not is_template_item(i)]
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
    size_counts = Counter(x.get('size') for x in pool)

    # tags distribution
    type_counts = Counter()
    for x in pool:
        for t in x.get('tags') or []:
            type_counts[t] += 1

    # cooldown bands from base tier
    def cd_band(v):
        if v is None or v == '':
            return 'passive'
        v = float(str(v).split('/')[0])
        if v <= 3: return '≤3s'
        if v <= 6: return '4-6s'
        if v <= 9: return '7-9s'
        return '10s+'
    band_counts = Counter(cd_band(cd_of(x)) for x in pool)
    # multi-tier cooldown (upgrade line) = items whose tierStats cooldowns differ
    multi = [x for x in pool if len({str(t.get('cooldown')) for t in x.get('tierStats') or [] if t.get('cooldown')}) > 1]

    # mechanic keywords on tags + descriptions
    def mem(x, kw):
        return kw in (x.get('tags') or []) or kw.lower() in desc_flat(x).lower()
    mech = {}
    for kw in ['Weapon', 'Aquatic', 'Friend', 'Tool', 'Property', 'Vehicle', 'Apparel', 'Toy', 'Relic', 'Potion', 'Core', 'Food']:
        mech[kw] = sum(1 for x in pool if kw in (x.get('tags') or []))
    for kw in ['Ammo', 'Haste', 'Slow', 'Burn', 'Crit', 'Poison', 'Freeze', 'Multicast', 'Shield']:
        mech[kw] = sum(1 for x in pool if mem(x, kw))

    rows = []
    rows.append('<html><head><meta charset="utf-8"><title>The Bazaar · Vanessa 物品池结构拆解</title><style>' + CSS + '</style></head><body>')
    rows.append('<h1>The Bazaar · Vanessa 物品池结构拆解</h1>\n<div class="sub">2026-08-31 · 数据源:<a href="https://mobalytics.gg/the-bazaar/vanessa-items">mobalytics.gg</a> TheBazaarStaticDataQuery(persistedQuery, cloudflareCacheVersion v1.0.59,快照 2026-08-31)· 全池 1207 物品中 Vanessa 专属 138 件(已过滤 1 件模板占位)· 物品英文为准 · 同系列:Pygmalien / Dooley / Mak / Karnok / Jules / Stelle / The Dragons / 公共池 / 技能池 / 综合</div>')

    rows.append('<div class="kpis">')
    rows.append(f'<div class="kpi"><div class="num">{N}</div><div class="lbl">物品池总数(全游最大梯队,较 wiki 口径 +18)</div></div>')
    rows.append(f'<div class="kpi"><div class="num">{tier_counts.get("Bronze",0)}/{tier_counts.get("Silver",0)}/{tier_counts.get("Gold",0)}/{tier_counts.get("Diamond",0)}</div><div class="lbl">tier 分布 B/S/G/D(总 {N})</div></div>')
    rows.append(f'<div class="kpi"><div class="num">{mech.get("Ammo",0)}</div><div class="lbl">弹药相关件({round(mech.get("Ammo",0)/N*100)}%)—— 身份关键词</div></div>')
    rows.append(f'<div class="kpi"><div class="num">{mech.get("Aquatic",0)}</div><div class="lbl">Aquatic 物品({round(mech.get("Aquatic",0)/N*100)}%)—— 贯穿性主题</div></div>')
    rows.append('<div class="kpi"><div class="num">2+4</div><div class="lbl">主轴:弹药武器 / 水生友军 + 暴击 / 慢控 / 经济 / 车辆</div></div>')
    rows.append(f'<div class="kpi"><div class="num">{band_counts.get("passive",0)}</div><div class="lbl">无冷却被动件({round(band_counts.get("passive",0)/N*100)}%)</div></div>')
    rows.append('</div>')

    rows.append('''<h2>0. 英雄骨架</h2>
<div class="card">
<div class="lead"><strong>定位</strong>:全游「侵略性构建」的基准英雄——物品池数量全游最大(138),无专属全局资源(Damage 为通用乘区),靠「武器数量优势 + 弹药节奏 + 水生分支」三条腿。</div>
<div class="lead"><strong>开局三选一</strong>:12 金 + 3 收入 / 附魔青铜小件 + 2 金 / 黄金技能(推荐,指向构筑)。起始无固定物品,身份靠「技能 + 商店池密度」教学。</div>
<div class="lead"><strong>身份关键词(Top)</strong>:''' + ' '.join(f'<span class="badge b-weap">Weapon {mech["Weapon"]}</span><span class="badge b-aqua">Aquatic {mech["Aquatic"]}</span><span class="badge b-tool">Ammo {mech["Ammo"]}</span><span class="badge b-friend">Friend {mech["Friend"]}</span><span class="badge">Haste {mech["Haste"]}</span><span class="badge">Crit {mech["Crit"]}</span>') + '</div>')
    rows.append('<div class="lead"><strong>资源轴速览</strong>:弹药 = 充能经济(用完靠 reload,火力 = 弹药 × 单发伤害);Crit = 爆发乘区;Poison/Burn/Freeze/Slow = 状态 DOT 与控制;Value/Economy = 买卖回流(支线);Vehicle = 大件 buff 流。</div>')
    rows.append('</div>')

    # 1.1 tier x size
    rows.append('''<h2>1. 池组成总览</h2>
<div class="card">
<h3>1.1 tier × size</h3>
<table>
<tr><th>Tier</th><th>数量</th><th>占比</th><th>Small</th><th>Medium</th><th>Large</th><th>定位</th></tr>''')
    size_by_tier = {}
    for x in pool:
        size_by_tier.setdefault(tier_of(x), Counter())[x.get('size')] += 1
    desc = {'Bronze': '入门密度层——小件武器/工具/友军为主,教学层', 'Silver': '主力层——弹药武器与经济件集中', 'Gold': '强度层——大数字与稀有引擎', 'Diamond': '封顶件层'}
    for t in ['Bronze', 'Silver', 'Gold', 'Diamond']:
        n = tier_counts.get(t, 0)
        if n == 0:
            continue
        sd = size_by_tier.get(t, {})
        rows.append(f'<tr><td><span class="badge t-{t.lower()}">{t}</span></td><td class="mono">{n}</td><td class="mono">{round(n/N*100)}%</td><td class="mono">{sd.get("Small",0)}</td><td class="mono">{sd.get("Medium",0)}</td><td class="mono">{sd.get("Large",0)}</td><td>{desc.get(t,"")}</td></tr>')
    rows.append('</table></div>')

    # 1.2 type tags
    rows.append('''<div class="card">
<h3>1.2 类型标签分布(多标签重复计入,基数 %s)</h3>
<table>
<tr><th>类型</th><th>数量</th><th>占比</th><th>代表件</th></tr>''' % N)
    typerep = {'Weapon': 'Cutlass / Rifle / Cannon', 'Aquatic': 'Jellyfish / Coral / Sea Shell', 'Friend': 'Calico / Piranha / Mr. Richardson', 'Tool': 'Powder Horn / Ramrod / Fishing Rod', 'Property': 'Cove / Lighthouse / Tropical Island', 'Vehicle': 'Flagship / Submarine / Tortuga', 'Apparel': 'Coral Armor / Holsters / Disguise', 'Toy': 'Beach Ball / Pet Rock / Nesting Doll', 'Relic': 'Figurehead / Korxena Crest / Star Chart', 'Food': '—', 'Core': '—'}
    badge = {'Weapon': 'b-weap', 'Tool': 'b-tool', 'Property': 'b-props', 'Aquatic': 'b-aqua', 'Friend': 'b-friend', 'Vehicle': 'b-veh'}
    for t in ['Weapon', 'Aquatic', 'Friend', 'Tool', 'Property', 'Vehicle', 'Apparel', 'Toy', 'Relic', 'Core', 'Food']:
        c = mech.get(t, 0)
        rows.append(f'<tr><td><span class="badge {badge.get(t, "t-silver")}">{t}</span></td><td class="mono">{c}</td><td class="mono">{round(c/N*100)}%</td><td>{typerep.get(t,"")}</td></tr>')
    rows.append('''</table>
<div class="note">本表取 tags 严格标签;§2.1 机制词表为 tags + descriptions 关键词计数(文本提及并入)。武器与水生是两大构成块;Friend 组件是「友军流」底盘。Food 0——英雄无食物轴。多标签物品重复计入各标签。</div>
</div>''')

    # 1.3 rhythm
    rows.append('''<div class="card">
<h3>1.3 节奏结构(冷却分布,base tier 口径)</h3>
<table>
<tr><th>冷却带</th><th>数量</th><th>占比</th><th>说明</th></tr>''')
    bd = {'passive': 'Passive(无冷却/触发式)', '≤3s': '≤3s', '4-6s': '4-6s', '7-9s': '7-9s', '10s+': '10s+'}
    for b in ['passive', '≤3s', '4-6s', '7-9s', '10s+']:
        n = band_counts.get(b, 0)
        rows.append(f'<tr><td>{bd[b]}</td><td class="mono">{n}</td><td class="mono">{round(n/N*100)}%</td><td>—</td></tr>')
    rows.append(f'<tr><td>多值(升级线,不同 tier 冷却不同)</td><td class="mono">{len(multi)}</td><td class="mono">{round(len(multi)/N*100)}%</td><td>随等级渐快——升级即加速,Bazaar 的「卡牌成长」语法</td></tr>')
    rows.append('''</table>
<div class="note">Mobalytics tierStats 直接给出每级冷却(如 7/6/5/4),升级填的是节奏曲线,不单是数值——等价于 StS2 的「费用降级」。</div>
<div class="note">弹药经济:消耗件(Weapon+Ammo/弹药类)与供给件(reload)构成「供给链」构建——Bazaar 最像引擎的结构,也是本次分析最值得 OneDeck 参考的点(见 §6)。</div>
</div>''')

    # 2.1 mechanic
    rows.append('''<h2>2. 术语与分级结构</h2>
<div class="card">
<h3>2.1 机制词分布</h3>
<table><tr><th>机制词</th><th>件数</th><th>性质</th></tr>''')
    for k in ['Weapon', 'Aquatic', 'Ammo', 'Haste', 'Crit', 'Friend', 'Tool', 'Slow', 'Burn', 'Poison', 'Freeze', 'Multicast', 'Shield', 'Property', 'Vehicle', 'Relic', 'Heal', 'Economy/Value']:
        n = mech.get(k, 0)
        rows.append(f'<tr><td class="mono">{k}</td><td class="mono">{n}</td><td>—</td></tr>')
    rows.append('''</table>
<div class="note">词表为 tags + descriptions 关键词匹配(多标签重复计入)。Slow/Burn 密度高——状态层是 Vanessa 的第三腿。</div>
</div>''')

    # 2.2 trigger phrases
    rows.append('''<div class="card">
<h3>2.2 触发句式</h3>
<table><tr><th>句式</th><th>件数</th><th>代表</th><th>设计含义</th></tr>''')
    def memc(kw):
        return sum(1 for x in pool if kw.lower() in desc_flat(x).lower())
    for kw in ['Deal', 'When you use', 'When your enemy uses', 'start of each day', 'adjacent']:
        rows.append(f'<tr><td>{kw}</td><td class="mono">{memc(kw)}</td><td>—</td><td>—</td></tr>')
    rows.append('</table></div>')

    # 3 axes (kept conceptual, generation backfills counts from live data)
    rows.append('''<h2>3. 构筑轴识别</h2>
<div class="card">
<h3>3.1 弹药武器轴(身份轴)</h3>
<div class="flow">reload 供给(Powder Horn / Ramrod / Port / Captain's Quarters) → 弹药消耗武器(Rifle / Repeater / Blunderbuss) → 暴击自装填(Revolver / Throwing Knives) → 封顶(The Boulder / Ballista)</div>
<div class="lead"><strong>入口</strong>:reload 件与弹药来源。<strong>兑现</strong>:消耗子弹的武器,单发 20-120。<strong>封顶</strong>:The Boulder / Ballista——一次填弹 = 一次巨型火力。</div>
<div class="lead"><strong>桥</strong>:Incendiary Rounds / Dive Weights / Captain's Quarters。<strong>密度</strong>:Ammo 相关''' + str(mech.get('Ammo', 0)) + ''' 件 = ''' + str(round(mech.get('Ammo', 0) / N * 100)) + '''%——全池最厚轴,Vanessa 的身份正身。</div>
</div>
<div class="card">
<h3>3.2 水生友军控伤轴(身份副轴)</h3>
<div class="flow">Jellyfish / Pufferfish / Catfish / Yeti Crab(友军状态施放) → Haste 触发链 → Shipwreck(+1 Multicast,封顶) / Slumbering Primordial(状态累积大数字)</div>
<div class="lead"><strong>入口</strong>:Aquatic Friend 状态件。<strong>兑现</strong>:Jellyfish「邻水生即加速」与 Pufferfish「收到 Haste 即充值」互锁自动机。<strong>封顶</strong>:Shipwreck(全池唯一 Diamond)全体 Aquatic +1 Multicast;Slumbering Primordial 状态→充能+成长三职齐备。</div>
</div>
<div class="card">
<h3>3.3 暴击轴(多件叠率)</h3>
<div class="flow">Custom Scope(右件 +crit) → Swash Buckle(邻件 +crit) → Wanted Poster(全版 crit + XP) → Cutlass(crit 双倍伤害)</div>
<div class="lead"><strong>设计特征</strong>:Crit 是常驻 buff 件 + 武器暴击标签的堆叠游戏,从 Bronze 到 Gold 全档。<strong>单武器极值</strong>:Sniper Rifle(100 + 只武器 3-10 倍)与 Cutlass 双倍暴击——「单武器过滤」是 Vanessa 高倍率句式的标准出法。</div>
</div>
<div class="card">
<h3>3.4 慢控轴(防御输出一体)</h3>
<div class="flow">Tripwire / Iceberg(敌用品即慢/冻) → Bolas / Fishing Net(大范围慢) → Jitte / Blowgun(慢件带伤)</div>
<div class="lead"><strong>密度</strong>:Slow/Freeze 件高——PvP 语境下「对方节奏惩罚」,直接类比 OneDeck 敌方诅咒。</div>
</div>
<div class="card">
<h3>3.5 经济 / 车辆辅助轴(弱轴)</h3>
<div class="lead"><strong>经济件</strong>:Cove / Ambergris / Lockbox / Orange Julian——4 件,全池最薄,支线补充。<strong>车辆(7 件)</strong>:Flagship / Submarine / Submersible / Tortuga / Rowboat / Seashadow / Shipwreck,与 Captain's Quarters 构成「大件 buff 流」,后期成型。</div>
</div>
<div class="card">
<h3>3.5 轴矩阵总结</h3>
<table>
<tr><th>轴</th><th>件数</th><th>tier 分布</th><th>入口/兑现/封顶</th><th>密度评价</th></tr>''')
    # live counts for matrix (same predicates as §3.6 bridge axes)
    vanessa_axes = {
        '输出': lambda i: has_tag(i, 'Weapon') or has_word(i, r'Deal \d|Damage'),
        '弹药': lambda i: has_tag(i, 'Ammo') or has_word(i, r'Ammo'),
        '水生': lambda i: has_tag(i, 'Aquatic'),
        '暴击': lambda i: has_tag(i, 'Crit') or has_word(i, r'Crit'),
        '慢控': lambda i: has_word(i, r'Slow|Freeze'),
        '经济': lambda i: has_word(i, r'Value|Sell Price|Gold|Income'),
        '车辆': lambda i: has_tag(i, 'Vehicle'),
    }
    ammo_axis = [x for x in pool if vanessa_axes['弹药'](x)]
    aqua_axis = [x for x in pool if vanessa_axes['水生'](x)]
    crit_axis = [x for x in pool if vanessa_axes['暴击'](x)]
    slow_axis = [x for x in pool if vanessa_axes['慢控'](x)]
    economy_axis = [x for x in pool if vanessa_axes['经济'](x)]
    veh_axis = [x for x in pool if vanessa_axes['车辆'](x)]
    def tierdist(lst):
        c = Counter(tier_of(x) for x in lst)
        return f'B{c.get("Bronze",0)}/S{c.get("Silver",0)}/G{c.get("Gold",0)}/D{c.get("Diamond",0)}'
    rows.append('<tr><td>弹药武器</td><td class="mono">%d</td><td>%s</td><td>reload 供给 → 弹药消耗武器 → Boulder/Ballista</td><td class="good">厚,身份轴 A</td></tr>' % (len(ammo_axis), tierdist(ammo_axis)))
    rows.append('<tr><td>水生友军</td><td class="mono">%d</td><td>%s</td><td>状态友军 → 互锁自动机 → Shipwreck</td><td class="good">厚,身份轴 B</td></tr>' % (len(aqua_axis), tierdist(aqua_axis)))
    rows.append('<tr><td>暴击</td><td class="mono">%d</td><td>%s</td><td>常驻 buff → 武器叠率 → 双倍伤害</td><td class="warn">中,散布全档</td></tr>' % (len(crit_axis), tierdist(crit_axis)))
    rows.append('<tr><td>慢控</td><td class="mono">%d</td><td>%s</td><td>敌用即慢 → 大范围慢 → 慢件带伤</td><td class="warn">中,PvP 语境</td></tr>' % (len(slow_axis), tierdist(slow_axis)))
    rows.append('<tr><td>经济</td><td class="mono">%d</td><td>%s</td><td>买卖价值 → 盾/奶/大价值</td><td class="bad">薄,支线</td></tr>' % (len(economy_axis), tierdist(economy_axis)))
    rows.append('<tr><td>车辆</td><td class="mono">%d</td><td>%s</td><td>大件 + buff 件 → 自动攻击</td><td class="warn">中,后期成型</td></tr>' % (len(veh_axis), tierdist(veh_axis)))
    rows.append('</table></div>')

    # ===== 3.6 bridge matrix (2026-08-31 framework) =====
    bridge_html, bridge_stats = bridge_report(
        pool, vanessa_axes,
        closed_loop_desc='弹药入口(Powder Horn reload) → 弹药兑现(Revolver 暴击自装填) → 暴击×输出(Cutlass 双倍暴击) → 慢控×水生(Iceberg 敌用即冻) → 车辆×输出(Flagship 自动攻击):弹药与水生两条主轴的桥让整条链自持。'
    )
    rows.append(bridge_html)

    # 4 two-phase
    rows.append('''<h2>4. 两段式审计(条件制造 → 兑现)</h2>
<div class="card">
<table>
<tr><th>层</th><th>设计职责</th><th>代表件</th><th>tier 观察</th></tr>
<tr><td><strong>入口层</strong>(制造条件/供给)</td><td>reload、Haste、状态附身、Value 累积、Crit 常驻</td><td>Powder Horn / Jellyfish / Custom Scope / Cove</td><td>B/S 为主——入口层 = Bronze-Silver,与 StS2 Common-Uncommon 同理</td></tr>
<tr><td><strong>兑现层</strong>(消费条件)</td><td>弹药武器、状态累积大伤、Crit 双倍、慢控惩罚</td><td>Revolver / Blunderbuss / Slumbering Primordial / Cutlass</td><td>S/G 为主</td></tr>
<tr><td><strong>封顶层</strong>(条件 → 乘区)</td><td>Multicast 加法、单武器倍率、状态→成长</td><td>Shipwreck / Sniper Rifle / Slumbering Primordial</td><td>G/D 集中——与 StS2 Rare 同工</td></tr>
</table>
<div class="verdict"><strong>结论</strong>:Bazaar 的两段式比 StS2 更「物理化」——条件不只是计数,而是供给量(弹药/Haste/时间)。入口件与兑现件靠明确的供给—消费接口词汇(Ammo/Charge/Haste)连接,这是 Bazaar 卡池语法的最小单位;OneDeck 对应物为「次数 × 大小」公式(见 §6)。</div>
</div>''')

    # 5 strength ladder
    rows.append('''<h2>5. 强度阶梯(高 tier = 高强度?)</h2>
<div class="card">
<h3>5.1 Gold 与 Diamond 件</h3>
<table>
<tr><th>件</th><th>tier</th><th>size</th><th>强度认定</th></tr>''')
    for x in sorted((x for x in pool if tier_of(x) in ('Gold', 'Diamond')), key=lambda z: tier_of(z)):
        rows.append(f'<tr><td>{x["name"]}</td><td><span class="badge t-{tier_of(x).lower()}">{tier_of(x)}</span></td><td>{x.get("size")}</td><td>{esc(" ".join(desc_of(x))[:110])}</td></tr>')
    rows.append('''</table>
<div class="verdict"><strong>结论</strong>:高 tier ≠ 必然强——套件核心(Iceberg 敌方即冻、Holsters 全局 Haste)在控制语境下比数值爆发更重要。tier 表达「获取难度 + 强度上限潜质」;Diamond 单件(乘区件)才真正封顶。tier 越高越倾向「条件化高倍率」,而非线性数值。</div>
</div>''')

    # 6 mapping
    rows.append('''<h2>6. 与 StS2 / OneDeck 的映射</h2>
<div class="card">
<h3>6.1 与 StS2 的结构异同</h3>
<table><tr><th>维度</th><th>StS2(五角色)</th><th>Bazaar(Vanessa)</th><th>差异判定</th></tr>
<tr><td>入口层</td><td>Common 无能力卡,即打即用</td><td>Bronze 供给型为主</td><td>同工:入口=制造条件,不做大兑现</td></tr>
<tr><td>兑现层</td><td>Uncommon 桥层</td><td>Silver 桥密度高</td><td>同工:桥多在中间 tier</td></tr>
<tr><td>封顶</td><td>Rare 大数字/引擎/形态升级</td><td>Gold+Diamond 封顶(乘区/单件极值)</td><td>同工:高 tier 承载乘区与条件化倍率</td></tr>
<tr><td>资源经济</td><td>垃圾经济学四形态</td><td>弹药经济(供给—消费)+ Value 回流</td><td>同类,Bazaar 更物理更直观</td></tr>
<tr><td>运行</td><td>回合制,手牌/能量</td><td>实时秒表,冷却/空间</td><td>差异最大——节奏是时间,不是资源</td></tr>
</table>
<h3>6.2 OneDeck 落点(初筛,正式建议在综合文档)</h3>
<table><tr><th>Bazaar 观察</th><th>OneDeck 可借鉴</th><th>不可搬</th></tr>
<tr><td>弹药供给—消费链路</td><td>「供给件→消费件」有明确接口词汇,构筑深度来自链长——OneDeck 的「埋→墓→复活」链已是同类,可参考<b>供给量可视</b>设计</td><td>秒表式实时冷却不能映射到轮次制</td></tr>
<tr><td>事件源三职(Slumbering Primordial)</td><td>OneDeck 的 xN 段数 × 力量公式已是同类——对照台面</td><td>—</td></tr>
<tr><td>「相邻/右侧」位形语法</td><td>OneDeck 是类型/阵营触发,位形语法不建议引入</td><td>除非做专门位形玩法</td></tr>
<tr><td>敌用品响应件(PvP 惩罚)</td><td>等价 OneDeck 敌方诅咒/慢件——已有</td><td>PvP 秒表语境的「节奏破坏」更直接</td></tr>
<tr><td>升级=节奏化</td><td>OneDeck 无等级制——保留,不做升级</td><td>不能搬(卡与武器体系不同)</td></tr>
</table>
</div>''')

    # 7 key cards
    rows.append('''<h2>7. 关键卡清单</h2>
<div class="card"><table>
<tr><th>轴</th><th>物品</th><th>tier</th><th>size</th><th>cd/弹药</th><th>effects 摘要</th></tr>''')
    keycards = [
        ('弹药入口', 'Powder Horn', 't-bronze'), ('弹药供给', 'Port', 't-silver'),
        ('弹药兑现', 'Revolver', 't-bronze'), ('弹药兑现', 'Rifle', 't-bronze'), ('弹药兑现', 'Blunderbuss', 't-gold'),
        ('弹药封面', 'The Boulder', 't-gold'), ('弹药桥', 'Incendiary Rounds', 't-silver'),
        ('水生', 'Jellyfish', 't-bronze'), ('水生兑现', 'Pufferfish', 't-silver'), ('水生封面', 'Shipwreck', 't-diamond'),
        ('事件源', 'Slumbering Primordial', 't-gold'),
        ('暴击', 'Custom Scope', 't-silver'), ('暴击', 'Swash Buckle', 't-gold'), ('暴击', 'Cutlass', 't-bronze'),
        ('单武器', 'Sniper Rifle', 't-silver'),
        ('慢控', 'Tripwire', 't-gold'), ('慢控', 'Iceberg', 't-gold'), ('慢控', 'Fishing Net', 't-bronze'),
        ('经济', 'Cove', 't-bronze'), ('车辆', "Captain's Quarters", 't-silver'),
        ('新件', 'Bilge Worm', 't-bronze'), ('新件', 'Burnacuda', 't-bronze'), ('新件', 'Marlon', 't-silver'),
    ]
    for axis, name, tk in keycards:
        try:
            x = lookup(name)
        except StopIteration:
            rows.append(f'<tr><td>{axis}</td><td>{name}</td><td colspan="4">(Mobalytics 无此条目)</td></tr>')
            continue
        rows.append(f'<tr><td>{axis}</td><td>{name}</td><td><span class="badge {tk}">{tier_of(x)}</span></td><td class="mono">{x.get("size")}</td><td class="mono">{cd_ammo(x)}</td><td>{esc(fx_short(x))}</td></tr>')
    rows.append('</table></div>')

    # 8 metadata
    rows.append('''<h2>8. 文档元信息</h2>
<div class="card">
<table>
<tr><th>项</th><th>值</th></tr>
<tr><td>数据快照</td><td class="mono">2026-08-31 mobalytics.gg TheBazaarStaticDataQuery(persistedQuery, cloudflareCacheVersion v1.0.59),全池 1207 件提取 Vanessa 专属 138</td></tr>
<tr><td>归属规则</td><td>heroes[] 含 Vanessa 且非模板占位(description 含 Template 的 1 件已滤)</td></tr>
<tr><td>与 wiki 差异</td><td>wiki Cargo 120 件;Mobalytics 138 件(+18)。新增 20 件(Bilge Worm / Burnacuda / Marlon / Cyber-Sai / Dart Launcher 等);wiki 的 Powder Flask → Mobalytics 更名 Powder Horn;Silencer 不在 Mobalytics</td></tr>
<tr><td>字段说明</td><td>tierStats 为四级数值;cooldown/ammo/critchance/multicast 底层字段化;descriptions 含配色模板标记已清洗</td></tr>
<tr><td>已知缺口</td><td>附魔(enchantments)已随 Mobalytics 全量收录,综合文档定量;Karnok/The Dragons 已纳入全样本</td></tr>
<tr><td>本系列</td><td>Pygmalien / Dooley / Mak / Karnok / Jules / Stelle / The Dragons / 公共池 / 技能池 / 综合(逐个完成即停,step-gate)</td></tr>
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
