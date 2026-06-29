# OneDeck Monte Carlo Simulator Skill

Run the OneDeck damage-per-round Monte Carlo simulator with configurable deck size and optional HP cap.

## When to Use

Use this skill when the user asks for:
- A new damage simulation / 模拟 / Monte Carlo run
- Comparing deck sizes (e.g. 6v6 vs 10v10)
- Adding or changing an HP cap
- Rerunning a specific configuration

Do **not** automatically run all preset configurations unless the user explicitly asks for it.

## Simulator Location

`tools/scripts/one_deck_damage_sim.py`

## Key Parameters

| CLI Flag | Meaning | Default |
|---|---|---|
| `--deck-size-each` | Cards per side (e.g. `6` for 6v6, `10` for 10v10) | `None` |
| `--hp-per-side` | HP cap per side (e.g. `25`). Omit for no HP limit. | `None` |
| `--sessions` | Number of independent sessions | `100` |
| `--rounds-per-session` | Recorded rounds per session | `500` |
| `--warmup-per-session` | Warmup rounds discarded per session | `200` |
| `--output` | Raw markdown output path | auto-named |
| `--report` | Formatted report path | auto-named |
| `--preset all` | Run all presets: 6v6, 10v10, 6v6 HP25, 10v10 HP25 | `none` |

Auto-named files are written to `tools/scripts/`:
- `sim_results_{N}v{N}.md` / `damage_analysis_report_{N}v{N}.md` (no HP)
- `sim_results_{N}v{N}_hp{HP}.md` / `damage_analysis_report_{N}v{N}_hp{HP}.md` (with HP)

## Interaction Rules

1. **If the user does not specify deck size or HP**, ask:
   - What deck size per side? (e.g. 6 or 10)
   - Any HP cap? (e.g. none, 25, 50)
   - How many sessions? (default 100 if not specified)
2. **Run only what the user asked for.** Use `--deck-size-each` and `--hp-per-side` explicitly.
3. Use `--preset all` only when the user says something like “run all configs” or “compare everything”.

## Example Commands

```bash
# 10v10 with 25 HP cap
cd "d:/Unity Projects/OneDeck/tools/scripts"
python one_deck_damage_sim.py --deck-size-each 10 --hp-per-side 25

# 6v6 no HP cap, more sessions for stability
python one_deck_damage_sim.py --deck-size-each 6 --sessions 500

# All presets
python one_deck_damage_sim.py --preset all
```

## Output Interpretation

The formatted report includes:
- **Win rates / avg rounds / avg total damage** when an HP cap is used.
- **Per-card table** with `Card`, `Display Name`, `Rarity`, `Avg Dmg/Round`, `Prob Dmg/Round`, `Total Dmg`, `Present Rounds`.

## Important Implementation Notes

- The simulator uses the real `3.0 no cost` card pool.
- `[Linger]` cards are gated by `CheckCost_IndexBeforeStartCard` (card must be before the Start Card in deck order).
- HP mode ends a session as soon as one side reaches 0 HP.
- Results can be high-variance, especially when ETERNAL_GHOST is involved; increase `--sessions` if numbers look unstable.
