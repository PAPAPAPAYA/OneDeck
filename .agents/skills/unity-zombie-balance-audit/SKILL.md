---
name: unity-zombie-balance-audit
last_reviewed: 2026-08-01
description: Regenerate the OneDeck zombie-baseline card balance audit (docs/CardBalanceAudit_ZombieBaseline.md) from card prefabs + the Notion card library, and optionally refresh the Notion mirror page / Desktop HTML report. Use when the user asks to update, refresh, or regenerate the balance audit, zombie audit, 僵尸审计, 平衡审计, or after any card value rebalance.
---

# Unity Zombie-Baseline Balance Audit

Recompute every card's expected per-round value against the ZOMBIE baseline (2.0 dmg/round) under the full-cycle model (Start Card fixed at deck bottom, q=1), and rewrite the audit document. Related: `unity-notion-card-sync` (syncs Notion data with prefabs; this skill *evaluates balance*, it does not sync data).

## Fixed References

| Item | Value |
|------|-------|
| Extract script | `tools/scripts/extract_card_prefabs.py` (offline YAML parser, no Unity needed) |
| Extract output | `tools/outputs/card_prefab_extract.txt` |
| Audit document | `docs/CardBalanceAudit_ZombieBaseline.md` (overwrite in place, CRLF, no versioned copies) |
| Card prefab root | `Assets/Prefabs/Cards/3.0 no cost (current)` |
| Notion card DB | `one deck demo cards`, data source `collection://2d5827b8-c3c1-81e8-9b87-000b8686c3cd` (fields: name / file name / description / rarity / category / tag) |
| Notion mirror page | `僵尸基准全卡审计` id `3b0827b8-c3c1-814a-9474-d35df8d4e85b` (under OD page) |
| Desktop HTML | `C:/Users/damen/Desktop/OneDeck_平衡与稳定性结论_2026-08-01.html` (only regenerate on explicit request) |
| Baseline | ZOMBIE = 2.0 dmg/round; HP 20; combat window R≈4; model: full cycle (AlwaysBottom, q=1) |

## Workflow

1. **Re-extract prefab data**:
   `python tools/scripts/extract_card_prefabs.py > tools/outputs/card_prefab_extract.txt`
   If new effect classes/fields were added to cards since the last run, extend the script's `FIELD_PAT` first.

2. **Fetch rarity/category from Notion** (SQL):
   `SELECT name, "file name", description, rarity, category, tag FROM "collection://2d5827b8-c3c1-81e8-9b87-000b8686c3cd"`
   Fallback when Notion is unreachable: rarity from prefab folder (`0_Common` / `1_Uncommon` / `2_Rare`).

3. **Join + recompute** each card's per-round value using the conventions in §1 of the audit doc (restated below). Join key = prefab file name ↔ Notion `file name`. Flag any card present in one source but missing in the other.

4. **Rewrite `docs/CardBalanceAudit_ZombieBaseline.md`** keeping the existing section structure (§1 conventions / §2 table by rarity / §3 findings / §4 rarity assessment / §5 regeneration), then convert to CRLF:
   `python -c "s=open('docs/CardBalanceAudit_ZombieBaseline.md',encoding='utf-8').read(); open('docs/CardBalanceAudit_ZombieBaseline.md','wb').write(s.replace('\n','\r\n').encode('utf-8'))"`

5. **Notion mirror** (only when asked): update page `3b0827b8-c3c1-814a-9474-d35df8d4e85b` via notion-update-page `replace_content` with the Chinese mirror of the table + key findings.

6. **Desktop HTML** (only when asked): regenerate `OneDeck_平衡与稳定性结论_*.html` with the project's dark report style (see `summaries/stats_report.html` for CSS).

## Valuation Conventions (canonical: audit doc §1)

Direct dmg X = X ｜ bury 1 hostile = +2.0 ｜ bury 1 friendly = −1.2 ｜ stage 1 friendly = +1.5 ｜ 1 Power to random friendly = +0.8 ｜ 1 Power to self (damage card) = +1.5 ｜ Power to next-X cards (faction-blind) ≈ 0, can be negative ｜ add 1 rift = +1.0 ｜ consume 1 rift = −1.0 ｜ enhance 1 curse = +1.2 (ramp, R=4) ｜ consume hostile curse 1 power = −1.2 ｜ exile friendly (non-rift) = −2.0 ｜ Linger trigger ×0.5 uptime ｜ global listener w/o zone check ×1.0 ｜ counter "every 2 reveals" ×0.5 ｜ conditional trigger ×0.3 unsupported / ×0.8 supported.

**Verdict bands**: common 1.6–2.4 ｜ uncommon 2.4–4.0 ｜ rare 4.0–8.0 or high-variance build-around. Verdicts: `++` / `+` / `=` / `-` / `!` (anti-value) / `C` (global-listener engine).

## Computation Rules & Gotchas

- **Damage semantics** (verified in `HPAlterEffect.cs`): damage = `baseDmg.value` (shared SO = 2) + `extraDmg` field + card's Power stacks. `DecreaseTheirHpTimesX(n)` = n hits of that. `_BasedOnIntSO` variants use the IntSO value as hits or bonus. **`m_IntArgument` in prefab UnityEvents is ignored when `m_Mode: 1`** — do not add it to damage; read `extraDmg` from the component instead.
- **C-class detection**: Trigger = global event (`OnFriendlyCardBuried` / `OnFriendlyCardExiled` / `OnTheirPlayerTookDmg` / `OnHostileCurseRevealed` / `OnEnemyCurseCardGotPower` / `OnAnyCardRevealed` / `AfterShuffle` …) AND container has no `checkCostEvent` ⇒ fires from any zone, every round, N times ⇒ price as zero-variance engine (current list: 守墓人, 冥界大炮, 飞蛾人, 咒食的野兽, 咒食的召唤师, 次元吞噬者).
- **EnhanceCurse auto-spawns** the curse if absent (never fizzles); new cards enter at index 0 (graveyard) but are consumable from anywhere.
- **Event bindings may live on CardScript itself** (it inherits GameEventListener), not only on separate GameEventListener components — the extractor already handles this.
- Prefab YAML strings use `\uXXXX` escapes; the extractor unescapes them.
- Conditional triggers (被埋/被置顶/被去除) are concentration-limited: show both unsupported (×0.3) and supported (×0.8) values.
- Zombie is the pity floor, NOT the common mid-point (accepted design decision 2026-08-01): commons are expected to average ~2.8–3.0.
