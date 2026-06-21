#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Generate an HTML report from OneDeck card winrate and shop stats JSON files."""

import argparse
import json
from datetime import datetime
from pathlib import Path

DEFAULT_DATA_DIR = Path.home() / "AppData/LocalLow/SmallGrass/OneDeck"
DEFAULT_OUT_PATH = Path("summaries/stats_report.html")

HTML_TEMPLATE = """<!DOCTYPE html>
<html lang="zh-CN">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>OneDeck 统计报告</title>
    <style>
        :root {{ --bg: #0d1117; --surface: #161b22; --border: #30363d; --text: #c9d1d9; --muted: #8b949e; --accent: #58a6ff; --win: #3fb950; --loss: #f85149; --buy: #a371f7; }}
        * {{ box-sizing: border-box; }}
        body {{ margin: 0; font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Helvetica, Arial, sans-serif; background: var(--bg); color: var(--text); line-height: 1.6; }}
        .container {{ max-width: 1200px; margin: 0 auto; padding: 32px 24px; }}
        header {{ border-bottom: 1px solid var(--border); padding-bottom: 24px; margin-bottom: 32px; }}
        h1 {{ margin: 0 0 8px; font-size: 32px; }}
        .subtitle {{ color: var(--muted); font-size: 14px; }}
        .grid {{ display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 24px; margin-bottom: 32px; }}
        .card {{ background: var(--surface); border: 1px solid var(--border); border-radius: 12px; padding: 20px; }}
        .card h2 {{ margin: 0 0 16px; font-size: 18px; color: var(--accent); }}
        .metric {{ display: flex; justify-content: space-between; align-items: center; padding: 10px 0; border-bottom: 1px solid var(--border); }}
        .metric:last-child {{ border-bottom: none; }}
        .metric .value {{ font-weight: 600; font-size: 18px; }}
        table {{ width: 100%; border-collapse: collapse; font-size: 14px; }}
        th, td {{ padding: 12px; text-align: left; border-bottom: 1px solid var(--border); }}
        th {{ background: rgba(88, 166, 255, 0.1); color: var(--accent); font-weight: 600; position: sticky; top: 0; }}
        tr:hover {{ background: rgba(255,255,255,0.03); }}
        .rate-bar {{ height: 6px; background: var(--border); border-radius: 3px; overflow: hidden; margin-top: 6px; }}
        .rate-bar > div {{ height: 100%; border-radius: 3px; }}
        .tag {{ display: inline-block; padding: 2px 8px; border-radius: 12px; font-size: 12px; font-weight: 600; }}
        .tag-win {{ background: rgba(63, 185, 80, 0.2); color: var(--win); }}
        .tag-loss {{ background: rgba(248, 81, 73, 0.2); color: var(--loss); }}
        .tag-buy {{ background: rgba(163, 113, 247, 0.2); color: var(--buy); }}
        .section {{ background: var(--surface); border: 1px solid var(--border); border-radius: 12px; padding: 24px; margin-bottom: 24px; }}
        .section h2 {{ margin: 0 0 20px; font-size: 20px; color: var(--accent); }}
        .footer {{ margin-top: 32px; padding-top: 24px; border-top: 1px solid var(--border); color: var(--muted); font-size: 13px; }}
    </style>
</head>
<body>
    <div class="container">
        <header>
            <h1>OneDeck 统计报告</h1>
            <div class="subtitle">数据来源：Application.persistentDataPath（SmallGrass/OneDeck）</div>
        </header>

        <div class="grid">
            <div class="card">
                <h2>战斗胜率概览</h2>
                <div class="metric"><span>追踪卡牌数</span><span class="value">{tracked_cards}</span></div>
                <div class="metric"><span>总战斗场次</span><span class="value">{total_combats}</span></div>
                <div class="metric"><span>数据更新时间</span><span class="value">{winrate_updated}</span></div>
            </div>
            <div class="card">
                <h2>商店购买概览</h2>
                <div class="metric"><span>商店访问次数</span><span class="value">{shop_visits}</span></div>
                <div class="metric"><span>总刷新次数</span><span class="value">{total_rerolls}</span></div>
                <div class="metric"><span>数据更新时间</span><span class="value">{shop_updated}</span></div>
            </div>
        </div>

        <div class="section">
            <h2>卡牌战斗胜率详情</h2>
            <table>
                <thead><tr><th>卡牌类型 ID</th><th>总战斗</th><th>胜场</th><th>败场</th><th>胜率</th></tr></thead>
                <tbody>{winrate_rows}</tbody>
            </table>
        </div>

        <div class="section">
            <h2>商店卡牌出现与购买详情</h2>
            <table>
                <thead><tr><th>卡牌类型 ID</th><th>卡牌名称</th><th>出现次数</th><th>购买次数</th><th>购买率</th></tr></thead>
                <tbody>{shop_rows}</tbody>
            </table>
        </div>

        <div class="footer">报告生成时间：{generated_at} · 原始文件：card_winrate.json / shop_stats.json</div>
    </div>
</body>
</html>"""


def load_json(path):
    if not path.exists():
        raise FileNotFoundError(f"Stats file not found: {path}")
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)


def format_winrate_rows(stats):
    for s in stats:
        s["winRate"] = s["wins"] / s["totalCombats"] if s["totalCombats"] > 0 else 0
    stats.sort(key=lambda x: (-x["winRate"], -x["totalCombats"]))

    rows = []
    for s in stats:
        wr = s["winRate"] * 100
        color = "var(--win)" if wr >= 50 else "var(--loss)"
        rows.append(
            f"<tr><td><strong>{s['cardTypeID']}</strong></td>"
            f"<td>{s['totalCombats']}</td>"
            f'<td><span class="tag tag-win">{s["wins"]}</span></td>'
            f'<td><span class="tag tag-loss">{s["losses"]}</span></td>'
            f'<td>{wr:.1f}%<div class="rate-bar"><div style="width:{wr:.1f}%; background:{color};"></div></div></td>'
            f"</tr>"
        )
    return "".join(rows)


def format_shop_rows(stats):
    for s in stats:
        s["purchaseRate"] = s["boughtCount"] / s["appearCount"] if s["appearCount"] > 0 else 0
    stats.sort(key=lambda x: (-x["purchaseRate"], -x["appearCount"]))

    rows = []
    for s in stats:
        pr = s["purchaseRate"] * 100
        rows.append(
            f"<tr><td><strong>{s['cardTypeID']}</strong></td>"
            f"<td>{s['cardName']}</td>"
            f"<td>{s['appearCount']}</td>"
            f'<td><span class="tag tag-buy">{s["boughtCount"]}</span></td>'
            f'<td>{pr:.1f}%<div class="rate-bar"><div style="width:{pr:.1f}%; background:var(--buy);"></div></div></td>'
            f"</tr>"
        )
    return "".join(rows)


def generate_report(data_dir, out_path):
    winrate_data = load_json(data_dir / "card_winrate.json")
    shop_data = load_json(data_dir / "shop_stats.json")

    winrate_rows = format_winrate_rows(winrate_data.get("allCardStats", []))
    shop_rows = format_shop_rows(shop_data.get("cardStats", []))

    html = HTML_TEMPLATE.format(
        tracked_cards=len(winrate_data.get("allCardStats", [])),
        total_combats=sum(s.get("totalCombats", 0) for s in winrate_data.get("allCardStats", [])),
        winrate_updated=winrate_data.get("lastUpdated", "N/A"),
        shop_visits=shop_data.get("totalShopVisits", 0),
        total_rerolls=shop_data.get("totalRerolls", 0),
        shop_updated=shop_data.get("lastUpdated", "N/A"),
        winrate_rows=winrate_rows,
        shop_rows=shop_rows,
        generated_at=datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
    )

    out_path = Path(out_path)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(html, encoding="utf-8")
    return out_path


def main():
    parser = argparse.ArgumentParser(description="Generate OneDeck stats HTML report.")
    parser.add_argument("--data-dir", type=Path, default=DEFAULT_DATA_DIR,
                        help="Directory containing card_winrate.json and shop_stats.json.")
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT_PATH,
                        help="Output HTML file path.")
    args = parser.parse_args()

    out_path = generate_report(args.data_dir, args.out)
    print(f"HTML report saved to: {out_path.absolute()}")


if __name__ == "__main__":
    main()
