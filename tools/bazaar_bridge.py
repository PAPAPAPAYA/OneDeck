# -*- coding: utf-8 -*-
"""Bazaar pool analysis — bridge (axis-to-axis link) analysis module.

Reusable across per-hero generators. A "bridge" = an item that belongs to >=2 axes.
Axes are defined per hero as predicate functions over (tags, descriptions).
All numbers are computed from the Mobalytics snapshot, reproducible.
"""
import re
from collections import Counter


def _flat(i):
    parts = list(i.get('descriptions') or [])
    for t in i.get('tierStats') or []:
        parts += t.get('descriptions') or []
    return ' '.join(parts)


def _tags(i):
    return i.get('tags') or []


def has_tag(i, tag):
    return tag in _tags(i)


def has_word(i, pattern):
    return re.search(pattern, _flat(i), re.I) is not None


def tag_or_word(i, tag, pattern):
    return has_tag(i, tag) or has_word(i, pattern)


def bridge_report(pool, axes, closed_loop_desc=''):
    """Compute bridge analysis for a pool given {axis_name: predicate}.

    Returns (html_block, stats) where html_block is the rendered <h2>3.6 轴间桥矩阵</h2>.
    """
    N = len(pool)
    names = list(axes.keys())

    def axset(i):
        return tuple(sorted(a for a, f in axes.items() if f(i)))

    # 1. link proportions
    cnt = Counter(len(axset(i)) for i in pool)
    single = cnt.get(1, 0)
    double = cnt.get(2, 0)
    triple = sum(v for k, v in cnt.items() if k >= 3)
    none = cnt.get(0, 0)
    bridge_n = double + triple

    # 2. bridge matrix
    M = {a: {b: 0 for b in names} for a in names}
    bridge_items = {}
    for i in pool:
        ax = axset(i)
        if len(ax) >= 2:
            bridge_items.setdefault(tuple(ax), []).append(i['name'])
        for a in ax:
            for b in ax:
                if a < b:
                    M[a][b] += 1

    # 3. forms: tag-bridge vs text-bridge vs both
    forms = Counter()
    form_items = {'标签桥': [], '文本桥': [], '双重桥': []}
    for i in pool:
        ax = axset(i)
        if len(ax) < 2:
            continue
        # tag-bridge: two axes both matched via tags (no text needed)
        tag_axes = [a for a in ax if has_tag(i, _TAG_OF.get(a, ''))] if False else None
        # simpler classification: count how many axis matches came from tags vs text
        tag_hits = []
        text_hits = []
        for a in ax:
            # predicate-based: check if axis matched purely by tag (heuristic)
            # we approximate: an axis is "tag-matched" if its name's tag is in item tags
            t = _TAG_OF.get(a)
            if t and has_tag(i, t):
                tag_hits.append(a)
            else:
                text_hits.append(a)
        # an axis is text-matched if its predicate involved description words
        # (approximation: if not tag-matched, it was text-matched)
        if len(tag_hits) >= 2 and not text_hits:
            forms['标签桥'] += 1
            form_items['标签桥'].append(i['name'])
        elif len(tag_hits) >= 1 and len(text_hits) >= 1:
            forms['双重桥'] += 1
            form_items['双重桥'].append(i['name'])
        elif len(text_hits) >= 2:
            forms['文本桥'] += 1
            form_items['文本桥'].append(i['name'])
        else:
            forms['其他'] += 1

    # 4. bridge tier distribution
    tier_dist = Counter()
    for i in pool:
        if len(axset(i)) >= 2:
            tier_dist[i.get('baseTier') or 'Unknown'] += 1

    # 5. empty pairs
    empty = [(a, b) for a in names for b in names if a < b and M[a][b] == 0]

    # render
    h = []
    h.append('<h2>3.6 轴间桥矩阵(2026-08-31 框架迭代)</h2>\n<div class="card">')
    h.append(f'<div class="kpis" style="margin:8px 0">')
    h.append(f'<div class="kpi"><div class="num">{single}</div><div class="lbl">单轴件({round(single/N*100)}%)</div></div>')
    h.append(f'<div class="kpi"><div class="num">{bridge_n}</div><div class="lbl">桥(≥2 轴,{round(bridge_n/N*100)}%)</div></div>')
    h.append(f'<div class="kpi"><div class="num">{double}</div><div class="lbl">双轴桥</div></div>')
    h.append(f'<div class="kpi"><div class="num">{triple}</div><div class="lbl">三轴+桥</div></div>')
    h.append(f'<div class="kpi"><div class="num">{none}</div><div class="lbl">无轴件(谓词未覆盖)</div></div>')
    h.append('</div>')
    # axis definitions
    h.append('<h3>轴定义(谓词,tags + 关键词)</h3>')
    h.append('<table><tr><th>轴</th><th>覆盖件数</th><th>谓词口径</th></tr>')
    for a in names:
        n = sum(1 for i in pool if axes[a](i))
        h.append(f'<tr><td class="mono">{a}</td><td class="mono">{n}</td><td>—</td></tr>')
    h.append('</table>')
    # matrix
    h.append('<h3>桥矩阵(轴×轴交集物品数)</h3>')
    h.append('<table><tr><th>×</th>' + ''.join(f'<th class="mono">{a}</th>' for a in names) + '</tr>')
    for a in names:
        h.append(f'<tr><td class="mono">{a}</td>')
        for b in names:
            v = M[a][b] if a < b else ('·' if a == b else M[b][a])
            cls = ''
            if isinstance(v, int) and v > 0:
                cls = ' class="good"' if v >= 10 else ''
            h.append(f'<td class="mono"{cls}>{v}</td>')
        h.append('</tr>')
    h.append('</table>')
    # top bridges
    top = sorted(((a, b, M[a][b]) for a in names for b in names if a < b), key=lambda x: -x[2])[:5]
    h.append('<h3>最密桥(Top 5)</h3><table><tr><th>桥</th><th>件数</th><th>代表物品</th></tr>')
    for a, b, v in top:
        items = bridge_items.get(tuple(sorted((a, b))), [])[:4]
        h.append(f'<tr><td class="mono">{a} × {b}</td><td class="mono">{v}</td><td>{", ".join(items)}</td></tr>')
    h.append('</table>')
    # forms
    h.append('<h3>桥的形式(标签桥 / 文本桥 / 双重桥)</h3>')
    h.append('<table><tr><th>形式</th><th>件数</th><th>代表</th></tr>')
    for k in ['标签桥', '文本桥', '双重桥']:
        h.append(f'<tr><td>{k}</td><td class="mono">{forms.get(k, 0)}</td><td>{", ".join(form_items[k][:5])}</td></tr>')
    h.append('</table>')
    # tier
    h.append('<h3>桥的 tier 分布</h3><table><tr><th>tier</th><th>桥数</th></tr>')
    for t in ['Bronze', 'Silver', 'Gold', 'Diamond', 'Legendary']:
        if tier_dist.get(t):
            h.append(f'<tr><td>{t}</td><td class="mono">{tier_dist[t]}</td></tr>')
    h.append('</table>')
    # empty
    h.append('<h3>空位审计(桥 = 0 的轴对)</h3>')
    if empty:
        h.append('<ul>' + ''.join(f'<li class="bad">{a} × {b}</li>' for a, b in empty) + '</ul>')
    else:
        h.append('<div class="good">无空位——所有轴对都有桥。</div>')
    # closed loop
    if closed_loop_desc:
        h.append(f'<h3>闭环示例(代表构筑链)</h3><div class="flow">{closed_loop_desc}</div>')
    h.append('</div>')
    return '\n'.join(h), {
        'single': single, 'bridge_n': bridge_n, 'double': double,
        'triple': triple, 'none': none, 'matrix': M, 'empty': empty,
        'forms': dict(forms), 'tier_dist': dict(tier_dist),
    }


# Axis tag hints: which axis's name maps to a tag (for tag-vs-text form classification)
_TAG_OF = {
    '弹药': 'Ammo', '水生': 'Aquatic', '车辆': 'Vehicle', '友军': 'Friend',
    '恐龙': 'Dinosaur', '射线': 'Ray', '核心': 'Core', '地产': 'Property',
    '玩具': 'Toy', '遗物': 'Relic', '食物': 'Food', '技术': 'Tech',
}


def esc(s):
    return s.replace('&', '&amp;').replace('<', '&lt;').replace('>', '&gt;')


def clean_descs(obj):
    """Cleaned description strings for an item/skill (base tier first, then top-level)."""
    dsc = []
    ts = obj.get('tierStats') or []
    if ts:
        dsc += ts[0].get('descriptions') or []
    else:
        dsc += obj.get('descriptions') or []
    out = []
    for x in dsc:
        x = re.sub(r'\{\{::([^:}]+)(:[^}]*)?\}\}', r'', x)
        x = re.sub(r'\{\{[^}]*\}\}', '', x)
        x = re.sub(r'\s*>\s*', '', x)
        x = re.sub(r'\s+', ' ', x).strip()
        if x:
            out.append(x)
    return out


def _tier_badge(obj):
    t = obj.get('baseTier') or 'Unknown'
    cls = {'Bronze': 't-bronze', 'Silver': 't-silver', 'Gold': 't-gold', 'Diamond': 't-diamond', 'Legendary': 't-gold'}.get(t, 't-silver')
    return f'<span class="badge {cls}">{t}</span>'


def _axes_of(item, axes):
    return [a for a, f in axes.items() if f(item)]


def _lookup(name, *collections):
    for c in collections:
        for x in c:
            if x.get('name') == name:
                return x
    return None


def render_builds(hero, pool, all_items, all_skills, axes, builds, source_note):
    """Render the 5.5 typical-builds section.

    builds: list of dicts {name, source, date, grade(html), logic,
                           items:[names], skills:[names], note(optional)}
    Items are resolved against the hero pool first, then the global item list
    (cross-hero picks are annotated with their hero). Skills resolve against
    the global skill list. Axis mapping is computed from the hero's predicates.
    """
    h = []
    h.append('<h2>5.5 典型构筑(社区攻略)</h2>')
    for b in builds:
        h.append('<div class="card">')
        h.append(f'<h3>{b["name"]} <span class="dim">· {b["source"]} {b["date"]} · {b["grade"]}</span></h3>')
        h.append(f'<div class="lead">{b["logic"]}</div>')
        # items table
        h.append('<table><tr><th>物品</th><th>tier / size</th><th>效果(快照 base tier)</th><th>轴映射</th></tr>')
        for name in b['items']:
            it = _lookup(name, pool, all_items)
            if not it:
                h.append(f'<tr><td>{name}</td><td>—</td><td>(快照未收录)</td><td>—</td></tr>')
                continue
            axes_s = '×'.join(_axes_of(it, axes)) or '—'
            hero_note = ''
            if hero not in (it.get('heroes') or []):
                hero_note = f' <span class="dim">[{"、".join(it["heroes"])}]</span>'
            fx = esc(' '.join(clean_descs(it))[:170])
            h.append(f'<tr><td>{it["name"]}{hero_note}</td><td>{_tier_badge(it)} / {it.get("size") or "—"}</td><td>{fx}</td><td class="mono">{axes_s}</td></tr>')
        h.append('</table>')
        # skills table
        if b.get('skills'):
            h.append('<table><tr><th>技能</th><th>tier</th><th>效果</th><th>轴映射</th></tr>')
            for name in b['skills']:
                sk = _lookup(name, all_skills)
                if not sk:
                    h.append(f'<tr><td>{name}</td><td>—</td><td>(快照未收录)</td><td>—</td></tr>')
                    continue
                # skills are not in the item pool; axes computed against a pseudo-item
                pseudo = {'tags': sk.get('tags') or [], 'tierStats': sk.get('tierStats'), 'descriptions': sk.get('descriptions')}
                axes_s = '×'.join(_axes_of(pseudo, axes)) or '—'
                hero_note = ''
                if hero not in (sk.get('heroes') or []):
                    hero_note = f' <span class="dim">[{"、".join(sk["heroes"])}]</span>'
                fx = esc(' '.join(clean_descs(sk))[:170])
                h.append(f'<tr><td>{sk["name"]}{hero_note}</td><td>{_tier_badge(sk)}</td><td>{fx}</td><td class="mono">{axes_s}</td></tr>')
            h.append('</table>')
        if b.get('note'):
            h.append(f'<div class="note">{b["note"]}</div>')
        h.append('</div>')
    h.append(f'<div class="note">{source_note}</div>')
    return chr(10).join(h)
