---
name: unity-onedeck-stats-report
description: Generate an HTML report from OneDeck's card_winrate.json and shop_stats.json persistence files. Use when the user asks to view, summarize, export, or format combat win rate and shop purchase statistics.
---

# Unity OneDeck Stats Report

Generate a dark-themed HTML report summarizing OneDeck card win rates and shop purchase statistics.

## Input Files

Both files live in Unity's `Application.persistentDataPath`:

```
C:\Users\<User>\AppData\LocalLow\SmallGrass\OneDeck\card_winrate.json
C:\Users\<User>\AppData\LocalLow\SmallGrass\OneDeck\shop_stats.json
```

## Workflow

### Step 1: Run the Generator Script

Execute the bundled Python script from the project root:

```bash
python3 .agents/skills/unity-onedeck-stats-report/scripts/generate_stats_report.py
```

Optional arguments:

```bash
python3 .agents/skills/unity-onedeck-stats-report/scripts/generate_stats_report.py \
  --data-dir "C:/Users/<User>/AppData/LocalLow/SmallGrass/OneDeck" \
  --out "summaries/stats_report.html"
```

### Step 2: Open the Report

The default output path is `summaries/stats_report.html`. Open it in a browser:

```bash
start "" "summaries/stats_report.html"
```

## Output Sections

1. **战斗胜率概览** - tracked card count, total combats, last update time.
2. **商店购买概览** - shop visits, reroll count, last update time.
3. **卡牌战斗胜率详情** - per-card combats, wins, losses, win-rate bar.
4. **商店卡牌出现与购买详情** - per-card appearances, buys, purchase-rate bar.

## Notes

- If a JSON file is missing, the script raises `FileNotFoundError`.
- Tables are sorted by rate (descending), then by volume.
- The report is self-contained (no external CSS/JS dependencies).
