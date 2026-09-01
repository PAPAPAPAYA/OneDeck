# -*- coding: utf-8 -*-
"""Generate the remaining Bazaar analysis docs: Common pool, Skills pool, Design Synthesis."""
import json, re, os
from collections import Counter

SNAP = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'tools', 'outputs', 'bazaar', 'mobalytics_static_2026-08-31.json')
OUTDIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'docs')

def is_tmpl(i):
    dsc = list(i.get('descriptions') or [])
    for t in i.get('tierStats') or []:
        dsc += t.get('descriptions') or []
    return any(('Template' in s) for s in dsc)

def dd(i):
    out = []
    ts = i.get('tierStats') or []
    dscs = (ts[0].get('descriptions') if ts else []) or i.get('descriptions') or []
    for s in dscs:
        s = re.sub(r'\{\{::([^:}]+)(:[^}]*)?\}\}', r'\1', s)
        s = re.sub(r'\{\{[^}]*\}\}', '', s)
        s = re.sub(r'\s*>\s*', '', s)
        s = re.sub(r'\s+', ' ', s).strip()
        if s:
            out.append(s)
    return out

def flat(i):
    parts = list(i.get('descriptions') or [])
    for t in i.get('tierStats') or []:
        parts += t.get('descriptions') or []
    return ' '.join(parts)

def esc(s):
    return s.replace('&', '&amp;').replace('<', '&lt;').replace('>', '&gt;')

CSS = '''
:root {
--bg: #14161c; --card: #1d2129; --card2: #232834; --text: #c8ccd4; --dim: #8b93a3;
--accent: #7aa2f7; --warn: #e0af68; --good: #9ece6a; --bad: #f7768e; --line: #2e3442;
--purple: #bb9af7; --cyan: #7dcfff;
}
* { box-sizing: border-box; margin: 0; padding: 0; }
body { background: var(--bg); color: var(--text); font-family: "Segoe UI", "Microsoft YaHei", system-ui, sans-serif; line-height: 1.6; padding: 24px; max-width: 1380px; margin: 0 auto; }
h1 { font-size: 1.5rem; margin-bottom: 4px; color: #e6e9ef; }
h2 { font-size: 1.12rem; margin: 30px 0 12px; color: #e6e9ef; border-left: 3px solid var(--accent); padding-left: 10px; }
h3 { font-size: 1rem; margin: 20px 0 8px; color: #dfe3ea; }
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
.t-legendary { color: #e06c9f; border-color: #e06c9f88; }
ul, ol { margin: 6px 0 6px 22px; font-size: 0.88rem; }
li { margin: 3px 0; }
.lead { font-size: 0.92rem; margin: 8px 0; }
.dim { color: var(--dim); } .warn { color: var(--warn); } .good { color: var(--good); } .bad { color: var(--bad); }
.verdict { border-left: 3px solid var(--warn); padding: 10px 14px; margin: 10px 0; background: #1a1e27; font-size: 0.88rem; }
.flow { background: #1a1e27; border: 1px solid var(--accent); border-radius: 10px; padding: 12px 16px; margin: 12px 0; font-family: Consolas, monospace; font-size: 0.88rem; color: #dfe3ea; }
@media print { body { background: #fff; color: #222; } .card { border-color: #ccc; } }
'''

SRC = '数据源:<a href="https://mobalytics.gg/the-bazaar/database/items-and-enchantments">mobalytics.gg</a> TheBazaarStaticDataQuery(persistedQuery, cloudflareCacheVersion v1.0.59,快照 2026-08-31)· 物品英文为准'

def page(title, body_rows):
    return '<html><head><meta charset="utf-8"><title>' + title + '</title><style>' + CSS + '</style></head><body>' + '\n'.join(body_rows) + '</body></html>'

def write(name, html):
    p = os.path.join(OUTDIR, name)
    with open(p, 'w', encoding='utf-8', newline='\r\n') as f:
        f.write(html)
    print('written', len(html), '->', p)

def kpi(n, l):
    return f'<div class="kpi"><div class="num">{n}</div><div class="lbl">{l}</div></div>'

# ============ Doc 9: Common pool ============
def gen_common(data):
    items = data['items']
    pool = [i for i in items if i['heroes'] == ['Common'] and not is_tmpl(i)]
    N = len(pool)
    tier = Counter(i.get('baseTier') for i in pool)
    tc = Counter()
    for i in pool:
        for t in i.get('tags') or []:
            tc[t] += 1
    loot = sorted(i['name'] for i in pool if 'Loot' in (i.get('tags') or []))
    leg = sorted(i['name'] for i in pool if i.get('baseTier') == 'Legendary')
    r = []
    r.append('<h1>The Bazaar · 公共池(Common)结构拆解</h1>')
    r.append(f'<div class="sub">2026-09-01 · {SRC} · 全池 1207 件中 Common 归属 166 件 · 同系列:九英雄拆解 / 技能池 / 综合总结</div>')
    r.append('<div class="kpis">')
    r.append(kpi(N, '公共池总数'))
    r.append(kpi(f"{tier.get('Legendary',0)}", f"Legendary 件(16%)—— 全游最高浓度(PvE 奖励)"))
    r.append(kpi(len(loot), 'Loot token(战利品/资源 token)'))
    r.append(kpi('59%', 'Small 件占比(98 件)—— 轻量接口件'))
    r.append('</div>')
    r.append('''<h2>0. 池定位</h2>
<div class="card">
<div class="lead">公共池不是「第十个英雄」,而是<b>跨英雄接口层</b>:① Loot token(25 件)——由英雄物品/技能生成的资源 token(Chunk of Lead / Spare Change / Pelt / Gunpowder);② 传说奖励(26 件 Legendary)——PvE 事件/掉的终局件(Dragon Heart / Necronomicon / 各 Warden);③ 通用支援件(约 115 件)——Curio 商店/商人的跨英雄小件(九英雄构筑的「off-Hero items」都出自这里,如 Vanessa Boulder 构筑的 Gunpowder、Stelle 构筑的 Rocket Boots)。</div>
<div class="lead">设计意义:公共池把「英雄间不相通的物品池」重新连接——它是 Bazaar 的「中性词表」,让 pivot 与混编有落点。</div>
</div>''')
    r.append('''<h2>1. 组成结构</h2>
<div class="card">
<h3>1.1 tier 分布(异常结构)</h3>
<table><tr><th>tier</th><th>数量</th><th>占比</th><th>说明</th></tr>
<tr><td><span class="badge t-bronze">Bronze</span></td><td class="mono">%d</td><td class="mono">%d%%</td><td>token 与轻量接口件</td></tr>
<tr><td><span class="badge t-silver">Silver</span></td><td class="mono">%d</td><td class="mono">%d%%</td><td>通用支援件主力</td></tr>
<tr><td><span class="badge t-gold">Gold</span></td><td class="mono">%d</td><td class="mono">%d%%</td><td>少量</td></tr>
<tr><td><span class="badge t-diamond">Diamond</span></td><td class="mono">%d</td><td class="mono">%d%%</td><td>高价值支援</td></tr>
<tr><td><span class="badge t-legendary">Legendary</span></td><td class="mono">%d</td><td class="mono">%d%%</td><td><b>全游最高 Legendary 浓度</b>——PvE 事件奖励/终局件</td></tr>
</table></div>''' % (tier.get('Bronze',0), round(tier.get('Bronze',0)/N*100), tier.get('Silver',0), round(tier.get('Silver',0)/N*100), tier.get('Gold',0), round(tier.get('Gold',0)/N*100), tier.get('Diamond',0), round(tier.get('Diamond',0)/N*100), tier.get('Legendary',0), round(tier.get('Legendary',0)/N*100)))
    r.append('<div class="card"><h3>1.2 类型标签</h3><table><tr><th>标签</th><th>数量</th><th>占比</th><th>代表</th></tr>')
    reps = {'Weapon': 'Claws / Clockwork Blades / Scythe', 'Relic': 'Ancient Specimen / Cosmic Amulet / Teddy', 'Loot': 'Chunk of Gold / Spare Change 类 token', 'Friend': 'Busy Bee / Octopus / Salamander Pup', 'Apparel': 'Agility Boots / Championship Belt', 'Food': 'Chocolate Bar / Coconut / Citrus', 'Tech': 'Cinders / Echo Crystal', 'Tool': 'Sharpening Stone / Bar of Soap'}
    for t, n in tc.most_common(10):
        r.append(f'<tr><td class="mono">{t}</td><td class="mono">{n}</td><td class="mono">{round(n/N*100)}%</td><td>{reps.get(t,"")}</td></tr>')
    r.append('</table></div>')
    r.append('''<h2>2. Loot token 经济(跨英雄接口)</h2>
<div class="card">
<h3>2.1 token 清单(25 件)</h3>
<div class="flow">''' + ' · '.join(loot) + '''</div>
<h3>2.2 生成关系(快照可验证样例)</h3>
<table><tr><th>token</th><th>生成者(英雄物品)</th></tr>
<tr><td class="mono">Pelt</td><td>Hunting Knife(Karnok:胜怪掉皮)</td></tr>
<tr><td class="mono">Scrap</td><td>Salvage Yard</td></tr>
<tr><td class="mono">Bag of Jewels</td><td>Safe</td></tr>
<tr><td class="mono">Gunpowder</td><td>多源(Port 发放的 off-Hero 弹药物等)——Vanessa Boulder 构筑的备选件</td></tr>
<tr><td class="mono">Chunk of Lead / Chunk of Gold</td><td>Mak 转化线(Retort 每日产铅→炼金成金)</td></tr>
</table>
<div class="verdict"><strong>结论</strong>:Loot token 是「英雄池→中性池」的单向阀——英雄物品生成中性 token,token 再被任意英雄的消费件兑现。这让「经济/资源」类构筑跨英雄通用,Mak 的铅→金甚至成为公共经济件。OneDeck 对照:次元裂缝 token 的「生成→兑现」结构与此同构,但 Bazaar 的 token 池是全英雄共享的。</div>
</div>''')
    r.append('''<h2>3. Legendary 奖励层(26 件)</h2>
<div class="card">
<div class="flow">''' + ' · '.join(leg) + '''</div>
<div class="lead">Warden 系列(Barbspike / Blazehowl / Icewatch)与 Ticket 系列(Crash Site / Temple Expedition)是 PvE 事件奖励;Dragon Heart 出现在 The Dragons 灼烧构筑的 10 胜板(Imu + Dragon Heart 组合)——公共池 Legendary 是英雄构筑的合法延伸件,而非独立体系。</div>
</div>''')
    r.append('''<h2>4. 与 OneDeck 的映射</h2>
<div class="card">
<table><tr><th>观察</th><th>可借鉴</th><th>不可搬</th></tr>
<tr><td>Loot token 中性池(25 件,跨英雄兑现)</td><td>OneDeck 的生成卡(裂缝/诅咒)是阵营绑定的——「中性 token 池」可让生成物跨阵营兑现,扩大混编空间</td><td>商店购买语境</td></tr>
<tr><td>Legendary PvE 奖励(26 件)</td><td>OneDeck 无 PvE 事件层——「战斗外成长奖励」可对照结果屏/连败补偿设计</td><td>PvE 层本身</td></tr>
<tr><td>off-Hero 支援件(构筑混编落点)</td><td>OneDeck 双 deck 天然混编,无需此层</td><td>—</td></tr>
</table>
</div>''')
    r.append(f'''<h2>5. 文档元信息</h2><div class="card"><table>
<tr><th>项</th><th>值</th></tr>
<tr><td>数据快照</td><td class="mono">2026-08-31 mobalytics.gg 全池 1207 件中 Common 归属 166(过滤 1 件模板占位)</td></tr>
<tr><td>口径</td><td>heroes==['Common'];多英雄共享件(Augment Reagents 等)计入各英雄文档</td></tr>
<tr><td>系列</td><td>九英雄拆解(Vanessa/Pygmalien/Dooley/Mak/Karnok/Jules/Stelle/The Dragons)已全部产出;技能池与综合总结随后</td></tr>
</table></div>''')
    write('Bazaar_CommonPool_PoolAnalysis_2026-08-31.html', page('The Bazaar · 公共池结构拆解', r))

# ============ Doc 10: Skills pool ============
def gen_skills(data):
    skills = data['skills']
    N = len(skills)
    tier = Counter(s.get('baseTier') for s in skills)
    hc = Counter()
    for s in skills:
        for h in s.get('heroes') or []:
            hc[h] += 1
    def sflat(s):
        parts = list(s.get('descriptions') or [])
        for t in s.get('tierStats') or []:
            parts += t.get('descriptions') or []
        return ' '.join(parts)
    kw = Counter()
    for k in ['Heal','Damage','Burn','Crit','Shield','Charge','Haste','Poison','Slow','Regen','Freeze','Rage','Flying','Ammo','Tempo','Multicast','Gold','Value','Destroy','Reload','Heated','Enchant']:
        kw[k] = sum(1 for s in skills if k.lower() in sflat(s).lower())
    r = []
    r.append('<h1>The Bazaar · 技能池(Skills)结构拆解</h1>')
    r.append(f'<div class="sub">2026-09-01 · {SRC} · 技能全量 {N} 条 · 同系列:九英雄拆解 / 公共池 / 综合总结</div>')
    r.append('<div class="kpis">')
    r.append(kpi(N, '技能总数'))
    r.append(kpi(f"{tier.get('Bronze',0)}/{tier.get('Silver',0)}/{tier.get('Gold',0)}/{tier.get('Diamond',0)}", 'tier 分布 B/S/G/D(+%d Legendary)' % tier.get('Legendary',0)))
    r.append(kpi('133', '全英雄通用技能(Common)—— 构筑胶水'))
    r.append(kpi(kw.most_common(1)[0][1], '%s 相关(最高频机制词)' % kw.most_common(1)[0][0]))
    r.append('</div>')
    r.append('''<h2>0. 技能层定位</h2>
<div class="card">
<div class="lead">技能 = Bazaar 的<b>被动层</b>:不占 10/12 格板位,以「构筑方向修正器」存在(社区共识:Vanessa/Pygmalien 开局三选一里黄金技能常是最优解——技能定方向,物品填 body)。各英雄 §5.5 构筑表中的技能(Augmented Weaponry / Juggler / Lifting / Machine Learning / Slow and Steady…)都是本池成员。</div>
</div>''')
    r.append('<div class="card"><h3>1. 英雄 × 数量</h3><table><tr><th>归属</th><th>技能数</th><th>备注</th></tr>')
    order = ['Vanessa','Pygmalien','Dooley','Mak','Karnok','Jules','Stelle','The Dragons','Common']
    notes = {'Common':'全英雄通用——构筑胶水层','The Dragons':'新英雄技能池最厚之一(含乐队 Tempo 技能)'}
    for h in order:
        r.append(f'<tr><td class="mono">{h}</td><td class="mono">{hc.get(h,0)}</td><td>{notes.get(h,"")}</td></tr>')
    r.append('</table><div class="note">多英雄共享技能在各归属重复计入;Common 133 条任意英雄可出。</div></div>')
    r.append('<div class="card"><h3>2. 机制词分布</h3><table><tr><th>机制词</th><th>技能数</th><th>设计含义</th></tr>')
    mean = {'Heal':'防御回复类最厚——技能是「补防」的主要来源','Damage':'直伤/增益类','Burn':'状态类技能(灼烧构筑的技能轴)','Crit':'暴击修正(远程构建向)','Shield':'护盾类','Charge':'充能加速(Dooley/Stelle 技能轴)','Haste':'加速','Poison':'毒系','Slow':'慢控','Regen':'持续回复','Freeze':'冻结(少量高价值)','Rage':'Karnok 专属机制泄漏','Flying':'Stelle 专属机制泄漏','Tempo':'The Dragons 专属机制泄漏','Heated':'Jules 专属机制泄漏(仅 5 条——英雄机制尽量不进技能层)'}
    for k, v in kw.most_common():
        r.append(f'<tr><td class="mono">{k}</td><td class="mono">{v}</td><td>{mean.get(k,"")}</td></tr>')
    r.append('</table><div class="note">Rage 29 / Tempo 16 / Flying 20 vs Heated 5——新英雄(Rage/Tempo/Flying)的机制大量泄漏进技能层,而 Jules 的 Heated 几乎不进技能(槽位机制与技能层天然冲突)。这是「机制是否技能化」的设计分界样本。</div></div>')
    r.append('''<h2>3. 通用技能的胶水作用</h2>
<div class="card">
<div class="lead">Common 133 条中的关键子集在各英雄构筑反复出现:<span class="mono">Rigged</span>(首次使用→全体 Haste,Dooley Power Drill / 通用开局)、<span class="mono">Juggler</span>(小件用→大件充能,The Boulder 构筑)、<span class="mono">Lefty Loosey / Captain's Charge</span>(位置触发)、<span class="mono">Final Flame / Immolating Spark</span>(灼烧位置技能,Burn 构筑通用)。通用技能是「跨构筑可迁移的成长投资」——攻略共识「拿金技能优于升级物品」即源于此。</div>
</div>''')
    r.append('''<h2>4. 与 OneDeck 的映射</h2>
<div class="card">
<table><tr><th>观察</th><th>可借鉴</th><th>不可搬</th></tr>
<tr><td>技能 = 不占板位的被动层</td><td>OneDeck 无场外被动位;「战斗开始时生效的 tag/被动卡」是最接近形态——若引入需守「不占 12 卡位」边界</td><td>技能购买经济</td></tr>
<tr><td>机制技能化分界(Heated 5 vs Rage 29)</td><td>OneDeck 的新机制是否进入「通用事件池」可参照:位置/槽位机制不进,资源/计数机制可进</td><td>—</td></tr>
<tr><td>通用技能胶水(133 条 Common)</td><td>OneDeck 的通用轴(力量/通用埋葬)已是同类——「跨构筑迁移投资」的定位值得保持</td><td>—</td></tr>
</table>
</div>''')
    r.append(f'''<h2>5. 文档元信息</h2><div class="card"><table>
<tr><th>项</th><th>值</th></tr>
<tr><td>数据快照</td><td class="mono">2026-08-31 mobalytics.gg 技能全量 {N} 条(theBazaarSkills)</td></tr>
<tr><td>口径</td><td>机制词按 descriptions 关键词匹配;tier 按 baseTier</td></tr>
</table></div>''')
    write('Bazaar_Skills_PoolAnalysis_2026-08-31.html', page('The Bazaar · 技能池结构拆解', r))

# ============ Doc 11: Synthesis ============
def gen_synthesis(data):
    items = data['items']
    ench = Counter()
    for i in items:
        if is_tmpl(i):
            continue
        for e in i.get('enchantments') or []:
            nm = e.get('name') or ''
            m = re.search(r'\{\{::([A-Za-z]+):', nm)
            if m:
                ench[m.group(1)] += 1
    r = []
    r.append('<h1>The Bazaar 卡池设计总结 → OneDeck 卡池调整建议</h1>')
    r.append(f'<div class="sub">2026-09-01 · 基于 {SRC}(1207 物品 + 522 技能 + 附魔全域)· 九英雄拆解 + 公共池 + 技能池的汇总 · 对齐 StS2 综合总结(2026-08-17)体例 · 目标:为 OneDeck 4.0 后续卡池设计提供第二个外部参照系</div>')
    r.append('<div class="kpis">')
    r.append(kpi('9+2', '覆盖:九英雄 + 公共池 + 技能池'))
    r.append(kpi('1729', '物品+技能总样本'))
    r.append(kpi('13', '全域附魔系统(每系约 1187 件)'))
    r.append(kpi('4', '双态/资源引擎家族(Heated/Flying/Rage/Tempo)'))
    r.append(kpi('43-82%', '桥比例区间(OneDeck/StS2 约 10-20%)'))
    r.append('</div>')
    r.append('''<h2>0. 核心结论(TL;DR)</h2>
<div class="card">
<div class="lead"><strong>OneDeck 已经做对的(不必动)</strong>:两段式条件/兑现、多乘区(次数×力量)、轴身份清晰(桥少是特征不是缺陷)、无上限自限(疲劳/链深)。</div>
<div class="lead"><strong>Bazaar 相对 StS2 的增量语法</strong>(本系列九份拆解的证据):① 双态/资源引擎四兄弟——Jules Heated/Chilled(窗口式)、Stelle start/stop Flying(往复式)、Karnok Rage→Enrage(循环式)、The Dragons Tempo(存取式);② 接口词汇统一(每个英雄一个最小语法单位:Ammo/Charge/Transform/Rage/Tempo…);③ 桥密度 43-82%(物品实体天然多标签);④ 附魔全域 13 系(数值重掷层);⑤ pivot 文化(攻略语境里 pivot 构筑是正式分类);⑥ token 中性池跨英雄兑现。</div>
<div class="lead"><strong>不需要做的</strong>:实时冷却/秒表、板面位形(Stove/Cooler/Notes/相邻)、尺寸经济、技能购买层、附魔商店——OneDeck 的轮次制与 12 卡位没有这些载体,搬过来只会变成伪回合制。</div>
</div>''')
    r.append('''<h2>1. 九英雄机制对照总表</h2>
<div class="card">
<table><tr><th>英雄</th><th>件数</th><th>身份机制(最小语法单位)</th><th>独有标签</th><th>桥比例</th><th>双态家族</th></tr>
<tr><td>Vanessa</td><td class="mono">138</td><td>弹药供给—消费(Ammo)</td><td>Aquatic</td><td class="mono">51%</td><td>—</td></tr>
<tr><td>Pygmalien</td><td class="mono">153</td><td>价值投资—回收(Value/Sell)</td><td>Toy/Property</td><td class="mono">50%</td><td>—</td></tr>
<tr><td>Dooley</td><td class="mono">143</td><td>核心→链充能(Charge)</td><td>Core/Ray</td><td class="mono">43%</td><td>—</td></tr>
<tr><td>Mak</td><td class="mono">140</td><td>试剂→药剂转化(Transform)</td><td>Reagent/Potion</td><td class="mono">72%</td><td>—</td></tr>
<tr><td>Karnok</td><td class="mono">118</td><td>Rage→Enrage 循环(Rage)</td><td>Apparel(皮甲)</td><td class="mono">64%</td><td>循环式</td></tr>
<tr><td>Jules</td><td class="mono">120</td><td>加热/冷冻窗口(Heated)</td><td>Food</td><td class="mono">82%</td><td>窗口式</td></tr>
<tr><td>Stelle</td><td class="mono">121</td><td>起降往复(Flying)</td><td>Drone</td><td class="mono">55%</td><td>往复式</td></tr>
<tr><td>The Dragons</td><td class="mono">107</td><td>Tempo 存取(Tempo)</td><td>Instrument</td><td class="mono">—</td><td>存取式</td></tr>
<tr><td>公共池</td><td class="mono">166</td><td>token 中性接口(Loot)</td><td>—</td><td>—</td><td>—</td></tr>
</table>
<div class="note">桥比例谓词口径见各英雄文档 §3.6;The Dragons 桥值见其文档。双态家族:Jules/Karnok/Stelle/Dragons 各自实现了「状态获得→兑现」的不同频率形态。</div>
</div>''')
    r.append('''<h2>2. Bazaar 设计语法(九份拆解的证据汇总)</h2>
<div class="card">
<table><tr><th>#</th><th>语法</th><th>证据</th><th>OneDeck 对应物</th></tr>
<tr><td>1</td><td><b>双态/资源引擎家族</b></td><td>Heated 36(窗口 4s)/ Flying 66(双向触发)/ Rage 68+Enrage 54(阈值循环)/ Tempo 26(可消费)</td><td>状态系统(Infected/Rest)可扩展为「窗口内第二效果」</td></tr>
<tr><td>2</td><td><b>每英雄一个接口词汇</b></td><td>Ammo/Value/Charge/Transform/Rage/Tempo…构筑深度=该词汇的链长</td><td>OneDeck 的「埋葬/置顶/揭晓」三动作+轴 tag 已是同类</td></tr>
<tr><td>3</td><td><b>桥密度 43-82%</b></td><td>物品实体多标签(Weapon+Friend+Vehicle);三轴桥 9-37 件/英雄</td><td>OneDeck 桥少是轴身份清晰的表现——不建议提升到 Bazaar 水平</td></tr>
<tr><td>4</td><td><b>升级=节奏化</b></td><td>多值冷却 31-44 件/英雄(7/6/5/4s)</td><td>OneDeck 无等级——「升级」可对照为稀有度提升的节奏收益</td></tr>
<tr><td>5</td><td><b>单核 win 条件与 pivot 文化</b></td><td>The Boulder/Calcinator/Hydraulic Press/Lightning Rod;攻略正式分类 pivot 构筑</td><td>OneDeck 的「build-around 骨架卡」需保留+保护( Rare 层封顶体验)</td></tr>
<tr><td>6</td><td><b>任务件</b></td><td>Giant Lollipop(用 100 食物+慢 80)/ Trail Mix / Cooking Mallet</td><td>跨回合累计条件——OneDeck 可做「全场累计」句式</td></tr>
<tr><td>7</td><td><b>附魔全域 13 系</b></td><td>每系约 1187 件覆盖(Heavy/Icy/Turbo/Shielded/Toxic/Fiery/Mossy/Restorative/Obsidian/Deadly/Shiny/Golden/Radiant)</td><td>OneDeck 的「增强诅咒」是敌方侧——全域附魔不建议搬(经济系统不同)</td></tr>
<tr><td>8</td><td><b>位置敏感槽位</b></td><td>Jules Stove/Cooler、Dragons Notes、相邻语法 21+ 件</td><td>不搬——OneDeck 无板面位置,事件模型冲突</td></tr>
<tr><td>9</td><td><b>token 中性池</b></td><td>Loot 25 件跨英雄兑现(Chunk of Gold/Pelt/Gunpowder)</td><td>OneDeck 生成卡可考虑「中性 token」扩大混编</td></tr>
<tr><td>10</td><td><b>小件化快节奏</b></td><td>The Dragons Small 64%;公共池 Small 59%</td><td>OneDeck 12 卡位已限空间,无尺寸轴</td></tr>
<tr><td>11</td><td><b>技能=方向修正器</b></td><td>522 条;开局选金技能定构筑;机制技能化分界(Heated 5 vs Rage 29)</td><td>若引入被动层,守「不占卡位」边界;槽位机制不进技能层</td></tr>
<tr><td>12</td><td><b>停怒/降落第二波</b></td><td>Karnok 停怒件(Firefly Lantern 等)、Stelle 降落触发</td><td>OneDeck 的「状态结束触发」是可选扩展方向</td></tr>
</table>
</div>''')
    r.append('''<h2>3. 正式建议(P0 / P1 / P2)</h2>
<div class="card">
<h3>3.1 P0(建议 4.1 优先评估)</h3>
<ol>
<li><b>「窗口式状态第二效果」句式</b>(源:Jules Heated / Stelle While Flying):为既有状态(Infected/Mana/Rest/Revive)增加「处于状态时的第二效果」,以「N 次揭晓内」替代秒表窗口。证据:四英雄双态家族全是各自池的最厚轴;OneDeck 状态系统已有载体。</li>
<li><b>可消费资源条扩展 Mana</b>(源:Dragons Tempo / Fingerless Gloves):Mana 状态目前只有静态出口;增加「每点 Mana 可兑换 X」的消费句式,让攒mana型构筑有主动决策。证据:Tempo 存取式是全游唯一可消费资源条,其构筑(Chibi 邻位攒/花 Tempo 减冷却)是完整闭环。</li>
<li><b>「供给件→消费件」接口词汇规范化</b>(源:Vanessa Ammo 链 / Mak Transform 链):OneDeck 4.0 已有「复活/信徒/诅咒」三链——建议为每条链显式定义接口词汇(如信徒=信 token,供给=生成,兑现=按信徒数),新卡文案强制使用,降低理解成本。证据:Bazaar 每英雄一词的纪律是其构筑可学性的核心。</li>
</ol>
<h3>3.2 P1(4.x 中期可选)</h3>
<ol>
<li><b>状态结束触发(第二波)</b>(源:Karnok 停怒 / Stelle 降落):Rest 消失时、Revive 触发后的一次性奖励——「双段兑现」扩展单段体验。</li>
<li><b>全场累计任务件</b>(源:Giant Lollipop):「本局累计埋葬 X 张→变形/质变」的任务句式,喂给 Rare 层做封顶体验(R 层形态升级空位是 StS2 审计③的延续)。</li>
<li><b>构筑原型学自检</b>(源:九英雄构筑表):卡池同时提供「单核 win 条件 / 引擎滚雪球 / 任务型 / pivot 转换件」四类原型各 ≥1——OneDeck 4.0 R 层可按此清单补位。</li>
</ol>
<h3>3.3 P2(明确不搬)</h3>
<ol>
<li>实时冷却/秒表与所有「每秒」句式(Regen/Charge 窗口)——无轮次载体。</li>
<li>板面位形(相邻/右侧/Stove 槽位/Notes)——事件模型冲突,强行引入会破坏 OneDeck 的「无位置、只有序」特性。</li>
<li>尺寸经济(Small/Medium/Large)与技能购买层——12 卡位与商店结构已承担同等约束。</li>
<li>全域附魔商店——数值重掷层与 OneDeck 的「卡即全部」哲学冲突;增强诅咒已覆盖敌方侧需求。</li>
</ol>
</div>''')
    r.append(f'''<h2>4. 附魔定量小节</h2>
<div class="card">
<div class="lead">全域 {sum(ench.values())} 条附魔实例、13 系命名(Heavy 物理加深 / Icy 冻结 / Turbo 加速 / Shielded 盾 / Toxic 毒 / Fiery 烧 / Mossy 再生 / Restorative 奶 / Obsidian 直伤 / Deadly 暴击 / Shiny 多段 / Golden 价值 / Radiant 免疫),每系覆盖约 1133-1187 件物品——<b>几乎每件物品都有全部 13 种改造可能</b>。这是「同一卡池的二次深度」:物品数值设计只需保证 13 系下都有意义。OneDeck 不搬系统,但「设计时校验卡在多语法下的表现」的思路可借鉴(如:这张卡被增强诅咒/被洗成敌方时是否仍成立——OneDeck 已有 curse copy 继承力量的同类校验)。</div>
<table><tr><th>附魔</th><th>覆盖物品数</th></tr>''')
    for k, v in ench.most_common():
        r.append(f'<tr><td class="mono">{k}</td><td class="mono">{v}</td></tr>')
    r.append('</table></div>')
    r.append(f'''<h2>5. 方法论与数据源注记</h2><div class="card"><table>
<tr><th>项</th><th>值</th></tr>
<tr><td>主数据源</td><td class="mono">mobalytics.gg TheBazaarStaticDataQuery(persistedQuery hash 60a432cd…,v1.0.59,快照 2026-08-31)——1207 物品 + 522 技能 + 附魔;Playwright 浏览器态 fetch(curl 直连 403)</td></tr>
<tr><td>交叉源</td><td class="mono">thebazaar.wiki.gg Cargo(6 英雄滞后口径,仅对照);thebazaarzone / Mobalytics Builds / bazaar-builds.net(构筑,共 20+ 篇)</td></tr>
<tr><td>已知缺口</td><td>wiki 无 Karnok/Dragons(以 Mobalytics 为准);「Regen Stacked」等个别攻略技能未收录进快照;构筑 meta 随补丁漂移(日期已逐篇标注)</td></tr>
<tr><td>系列产出</td><td>九英雄拆解 + 公共池 + 技能池 + 本总结,共 11 份;执行记录见 plans/plan-the-bazaar-pool-analysis-2026-08-30.md(step-gate 全程)</td></tr>
</table></div>''')
    write('Bazaar_DesignSynthesis_ForOneDeck_2026-08-31.html', page('The Bazaar 设计总结 → OneDeck 建议', r))

def main():
    data = json.load(open(SNAP, encoding='utf-8'))
    gen_common(data)
    gen_skills(data)
    gen_synthesis(data)

if __name__ == '__main__':
    main()
