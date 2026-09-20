---
name: unity-zombie-balance-audit
last_reviewed: 2026-09-05
description: Regenerate the OneDeck zombie-baseline card balance audit (docs/CardBalanceAudit_ZombieBaseline.html) from the 4.0 card prefabs + the Notion 4.0 card database. Use when the user asks to update, refresh, or regenerate the balance audit, zombie audit, 僵尸审计, 平衡审计, or after any card value rebalance.
---

# Unity Zombie-Baseline Balance Audit

Recompute every combat card's expected per-activation value against the ZOMBIE baseline (2.0 dmg/round) under the full-cycle model (Start Card fixed at deck bottom, q=1), and rewrite the HTML report in place. Related: `unity-notion-card-sync` (syncs Notion data with prefabs; this skill *evaluates balance*, it does not sync data).

## Fixed References

| Item | Value |
|------|-------|
| Extract script | `tools/scripts/extract_card_prefabs.py` (offline YAML parser, no Unity needed; ROOT = `Assets/Prefabs/Cards/4.0`, excludes `-1_Test`; resolves IntSO/GameEvent refs to names) |
| Extract output | `tools/outputs/card_prefab_extract.txt` |
| Audit report | `docs/CardBalanceAudit_ZombieBaseline.html` — single self-contained page (inline CSS/JS, file:// openable); overwrite in place, no versioned copies. Replaced the retired `.md` artifact on 2026-09-05. |
| Audit DB (DEPRECATED 2026-09-06) | Notion `4.0 审计` data source `collection://3d2827b8-c3c1-8078-8aef-000b15d633ec` (under OD) — 89 combat rows kept as frozen history, **do NOT write to it** (rows or formulas). Scoring lives in `tools/scripts/gen_balance_audit_report.py` (BASE_UNIT / LAYER_MULT / BANDS + per-iteration PATCHES). |
| Row-mapping script | `tools/scripts/extract_audit_rows.py` + `tools/outputs/audit_rows_40.json` (machine rows) / `audit_mapping_40.tsv` (human review table) — maps prefab CONT effects to the DB's structured vocabulary |
| Card prefab root | `Assets/Prefabs/Cards/4.0/` (`0_Common` / `1_Uncommon` / `2_Rare`) |
| Notion card DB | `4.0 card database`, data source `collection://3c7827b8-c3c1-8002-8b45-000bc02fa836` (title = `CARD_TYPE_ID`; 中文名 / rarity / ATK / card desc / 状态) |
| Notion mirror page | `僵尸基准全卡审计` id `3b0827b8-c3c1-814a-9474-d35df8d4e85b` (under OD page; only refresh on explicit request) |
| Style reference | `summaries/stats_report.html` (project dark-report palette: --bg #0d1117 / --surface #161b22 / --border #30363d / --accent #58a6ff) |
| Baseline | ZOMBIE = 2.0 dmg/round; HP 20; combat window R≈4; model: full cycle (AlwaysBottom, q=1) |

## Workflow

1. **Re-extract prefab data**:
   `python tools/scripts/extract_card_prefabs.py --out tools/outputs/card_prefab_extract.txt`
   (writes the alias plus a dated twin per the 2026-09-20 convention in `tools/scripts/dated_output.py`; legacy stdout redirect still works)
   If new effect classes/fields were added to cards since the last run, extend the script's `FIELD_PAT` first (see Computation Rules for known field pitfalls).

2. **Fetch rarity/中文名 from Notion** (SQL over the 4.0 data source; join key `CARD_TYPE_ID` ↔ prefab `cardTypeID`, NOT file name). Fallback when Notion is unreachable: rarity from prefab folder. `状态=备用` rows have no prefab — list them, never score or "fix" them.

3. **Score via the generator script** (since 2026-09-06; audit DB deprecated): `python tools/scripts/gen_balance_audit_report.py` reads `tools/outputs/audit_rows_40.json` (regenerate with the mapping script first and eyeball the TSV), applies the PATCHES dict (per-iteration card DB value edits), and encodes all ratified conventions (BASE_UNIT / LAYER_MULT / BANDS). Fractional n = 数量×质量系数 (multi-revive discount applies at integer n ≥ 2). Changing any base value or 频率 assumption = convention change → 拍板 first.

4. **The script rewrites `docs/CardBalanceAudit_ZombieBaseline.html`** in place (CRLF conversion handled inside the script). The section requirements below describe the template the script emits — keep them in sync when hand-editing either side:

   Required sections & features (keep parity with the existing report):
   - Header meta (generation date, data sources, baseline) + verdict legend with colored badges.
   - Summary cards: pool snapshot / per-rarity vacuum means / problem-card counts / top findings.
   - Combat tables grouped by rarity — one row per card: 中文名 / CARD_TYPE_ID / effect (slot-normalized text; manual/build rows fall back to prefab desc, HTML-escaped) / 估值式 / value / verdict badge / note. **Value and verdict are computed by the generator script from the ratified conventions** (the audit DB is no longer the source); the 估值式 text mirrors the row's effect slots. Every table row carries `data-r` (rarity) and `data-v` (verdict class: `over` / `in` / `under` / `bang` / `build`) attributes.
   - Toolbar JS: rarity filter buttons + verdict `<select>` + text search + shown-count. No external dependencies.
   - Findings sections: over-band outliers; anti-value / vacuum-zero engines (with their conversion paths); predicate traps; and the two user-requested variance lists — **A. rare-but-not-build-around**, **B. non-rare-but-high-variance**.
   - Shop utility/system cards (currently 18) get their own section, listed but NEVER scored (2026-09-05 user decision).
   - Valuation conventions inside a collapsed `<details>` block.

5. **Notion mirror** (only when asked): update page `3b0827b8-c3c1-814a-9474-d35df8d4e85b` via notion-update-page `replace_content` with a Chinese mirror of the tables + key findings. There is no separate Desktop HTML anymore — the report IS the HTML; copy it elsewhere only on explicit request.

## Verdict system

`++` severe over / `+` over / `=` in band / `-` under / `!` anti-value / `C` global-listener engine / `B` build-around (vacuum value + high variance). Bands: common 1.6–2.4 / uncommon 2.4–4.0 / rare 4.0–8.0 or build-around.

## Computation Rules & Gotchas (code-verified 2026-09-05)

- **Attack damage = the card's attack attribute × segments** (`AttackEffect.ComputeTotalDamage` = `GetAttack()`; the baseDmg SO is deprecated for attack cards). `Attack()` = `GetAttackTimes()` segments (1 + `extraAttackTimes` + round mods + creature aura); `AttackTimes(N)` = exactly N segments. Attack events fire **per segment** — RELIC_HIVE / RELIC_ATTACK_HEX / RELIC_ATTACK_BURIAL scale with multi-hit.
- **Curse JU_ON** (CardType.Status, the only Status) damages its OWN SIDE's player on reveal (`AttackSelf` → `DecreaseMyHp` on `myStatusRef`; no-op at attack ≤ 0); printedAttack 0, grows only via EnhanceCurse — so 强化敌方诅咒 weaponizes the enemy's curse against them. **Believer = RIFT token (次元裂缝, CardType.None)**, generated by the 11 believer generators via `AddTempCard.AddCardToMe` bound to RIFT.prefab (`m_ObjectArgument`; the `curseCardTypeID` field only feeds `CopyEnemyCurseCardToThem`); on reveal it revives 1 friendly (`RiftOverrideAwareReviveEffect`, rewritten by RELIC_RIFT_OVERRIDE to revive an enemy curse) then exiles itself. Both tokens: `Prefabs/Cards/3.0 no cost (current)/_DONT INCLUDE/Token/`, not in the shop pool, catalogued in the Notion card DB.
- **Mill** (`BuryNextXCards`) fires the milled cards' 遗言. Ratified mill value −0.8 assumes friendly DR support (user ruling). 生成1信徒 ratified **+1.0** (09-06 拍板: believer reveal = revive 1 friendly + self-exile, 2.0 − 1.0; the earlier −0.5 proposal was voided by the believer-semantics correction). Tag-directed selection (`ReviveMyCardsWithTag` etc., e.g. RIFT_SHEPHERD 复活1张tag为[信徒]的友方卡) = full 复活1友方 +2.0 — tag selectors cannot target tokens (tokens carry no tags; 信徒-tag pool = 13 real cards, C3/U5/R5). Converters (RIFT_REAPER / RELIC_RIFT_OVERRIDE / RIFT_GUIDE / RIFT_REVIVER) turn believers into value.
- **`m_IntArgument` in prefab UnityEvents is ignored when `m_Mode: 1`** — buff amounts live in `xFriendlyCount` (target count) / `yFriendlyLayerCount` (layers per target), not the UnityEvent arg.
- Conditional trigger layers (遗言/苏醒/强化反应): 09-05 rule (×0.3/×0.8 by support) was **withdrawn and replaced on 09-06 (拍板)** — conditions always deduct; 苏醒/强化反应 ×0.5, 遗言 ×0.3 (post-trigger lifecycle asymmetry: 遗言 leaves the card dead in graveyard, 苏醒 returns it to deck top still revealing); generic "条件" layer ×0.5. Applied pool-wide 09-06 round 2 together with 强化三类统一 1.0/层 (给力量友方/给力量自身/强化敌方诅咒 = +n) and 生成信徒 +1.0.
- **C-class detection**: trigger = global event (OnFriendlyCardBuried / OnFriendlyCardExiled / OnHostileCurseRevealed / OnFriendlyCardRevived / OnMeGainedAttack / onAnyCardAttacked / OnRoundEnd / BeforeRoundFinished / AfterShuffle …) AND no `checkCostEvent` ⇒ fires from any zone, every round ⇒ zero-variance engine (all RELIC_* passives are C-class).
- EnhanceCurse auto-spawns the curse if absent (never fizzles). Curses are excluded from creature predicates and from WEAKENING_FIELD's −1.
- Prefab YAML strings use `\uXXXX` / `\xNN` escapes; the extractor unescapes them. Some prefabs carry an earlier shop-view component whose `cardTypeID` is a StringSO reference — take the LAST literal match (see `unity-notion-card-sync` guardrails for the card list).

## Guardrails

- Ratified convention values encode designer intent — never re-derive or silently change them; the 拍板 record lives in the report's §1 header.
- UTILITY_* / SYSTEM_* shop cards are never scored (no combat value口径).
- Reference shape 2026-09-06 (89 combat cards): C 19 (21%) / U 44 (49%) / R 26 (29%); CURSE_REVIVER downgraded U→C (5f567e8). Tokens RIFT/JU_ON live in the 3.0 Token folder, are catalogued in the Notion card DB (rarity empty), and are NOT scored or counted in rarity ratios.
- The HTML is the single artifact — do not recreate the `.md`.
