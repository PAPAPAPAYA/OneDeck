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
| Card prefab root | `Assets/Prefabs/Cards/4.0/` (`0_Common` / `1_Uncommon` / `2_Rare`) |
| Notion card DB | `4.0 card database`, data source `collection://3c7827b8-c3c1-8002-8b45-000bc02fa836` (title = `CARD_TYPE_ID`; 中文名 / rarity / ATK / card desc / 状态) |
| Notion mirror page | `僵尸基准全卡审计` id `3b0827b8-c3c1-814a-9474-d35df8d4e85b` (under OD page; only refresh on explicit request) |
| Style reference | `summaries/stats_report.html` (project dark-report palette: --bg #0d1117 / --surface #161b22 / --border #30363d / --accent #58a6ff) |
| Baseline | ZOMBIE = 2.0 dmg/round; HP 20; combat window R≈4; model: full cycle (AlwaysBottom, q=1) |

## Workflow

1. **Re-extract prefab data**:
   `python tools/scripts/extract_card_prefabs.py > tools/outputs/card_prefab_extract.txt`
   If new effect classes/fields were added to cards since the last run, extend the script's `FIELD_PAT` first (see Computation Rules for known field pitfalls).

2. **Fetch rarity/中文名 from Notion** (SQL over the 4.0 data source; join key `CARD_TYPE_ID` ↔ prefab `cardTypeID`, NOT file name). Fallback when Notion is unreachable: rarity from prefab folder. `状态=备用` rows have no prefab — list them, never score or "fix" them.

3. **Join + recompute** each card's value using the conventions in the report's collapsed §1 (canonical, user-ratified 2026-09-05). **Do NOT silently change ratified values** — propose adjustments and get the user's 拍板 first; then update §1 together with the scores.

4. **Rewrite `docs/CardBalanceAudit_ZombieBaseline.html`** in place, then convert to CRLF:
   `python -c "s=open('docs/CardBalanceAudit_ZombieBaseline.html',encoding='utf-8').read(); open('docs/CardBalanceAudit_ZombieBaseline.html','wb').write(s.replace('\r\n','\n').replace('\n','\r\n').encode('utf-8'))"`

   Required sections & features (keep parity with the existing report):
   - Header meta (generation date, data sources, baseline) + verdict legend with colored badges.
   - Summary cards: pool snapshot / per-rarity vacuum means / problem-card counts / top findings.
   - Combat tables grouped by rarity — one row per card: 中文名 / CARD_TYPE_ID / effect from the prefab / 估值式 / value / verdict badge / note. Every row carries `data-r` (rarity) and `data-v` (verdict class: `over` / `in` / `under` / `bang` / `build`) attributes.
   - Toolbar JS: rarity filter buttons + verdict `<select>` + text search + shown-count. No external dependencies.
   - Findings sections: over-band outliers; anti-value / vacuum-zero engines (with their conversion paths); predicate traps; and the two user-requested variance lists — **A. rare-but-not-build-around**, **B. non-rare-but-high-variance**.
   - Shop utility/system cards (currently 18) get their own section, listed but NEVER scored (2026-09-05 user decision).
   - Valuation conventions inside a collapsed `<details>` block.

5. **Notion mirror** (only when asked): update page `3b0827b8-c3c1-814a-9474-d35df8d4e85b` via notion-update-page `replace_content` with a Chinese mirror of the tables + key findings. There is no separate Desktop HTML anymore — the report IS the HTML; copy it elsewhere only on explicit request.

## Verdict system

`++` severe over / `+` over / `=` in band / `-` under / `!` anti-value / `C` global-listener engine / `B` build-around (vacuum value + high variance). Bands: common 1.6–2.4 / uncommon 2.4–4.0 / rare 4.0–8.0 or build-around.

## Computation Rules & Gotchas (code-verified 2026-09-05)

- **Attack damage = the card's attack attribute × segments** (`AttackEffect.ComputeTotalDamage` = `GetAttack()`; the baseDmg SO is deprecated for attack cards). `Attack()` = `GetAttackTimes()` segments (1 + `extraAttackTimes` + round mods + creature aura); `AttackTimes(N)` = exactly N segments. Attack events fire **per segment** — RELIC_HIVE / RELIC_ATTACK_HEX / RELIC_ATTACK_BURIAL scale with multi-hit.
- **Curse JU_ON** (CardType.Status) self-damages its OWNER on reveal (`AttackSelf`); printedAttack 0, grows only via EnhanceCurse. So 强化敌方诅咒 is a payoff, and **your own believers reveal as 0-ATK blanks**. Believer = JU_ON token (`CurseCardTypeID` SO) added via `AddTempCard->AddCardToMe`.
- **Mill** (`BuryNextXCards`) fires the milled cards' 遗言. Ratified mill value −0.8 assumes friendly DR support (user ruling). 生成1信徒 is ratified **0** (neutral resource); converters (RIFT_REAPER / RELIC_RIFT_OVERRIDE / RIFT_GUIDE / RIFT_REVIVER) turn believers into value.
- **`m_IntArgument` in prefab UnityEvents is ignored when `m_Mode: 1`** — buff amounts live in `xFriendlyCount` (target count) / `yFriendlyLayerCount` (layers per target), not the UnityEvent arg.
- Conditional trigger layers (遗言/苏醒/强化反应) ×0.3 unsupported / ×0.8 supported — 4.0 ignition density is healthy (bury sources 17+, revive sources ~18, buff sources ~19), default to supported.
- **C-class detection**: trigger = global event (OnFriendlyCardBuried / OnFriendlyCardExiled / OnHostileCurseRevealed / OnFriendlyCardRevived / OnMeGainedAttack / onAnyCardAttacked / OnRoundEnd / BeforeRoundFinished / AfterShuffle …) AND no `checkCostEvent` ⇒ fires from any zone, every round ⇒ zero-variance engine (all RELIC_* passives are C-class).
- EnhanceCurse auto-spawns the curse if absent (never fizzles). Curses are excluded from creature predicates and from WEAKENING_FIELD's −1.
- Prefab YAML strings use `\uXXXX` / `\xNN` escapes; the extractor unescapes them. Some prefabs carry an earlier shop-view component whose `cardTypeID` is a StringSO reference — take the LAST literal match (see `unity-notion-card-sync` guardrails for the card list).

## Guardrails

- Ratified convention values encode designer intent — never re-derive or silently change them; the 拍板 record lives in the report's §1 header.
- UTILITY_* / SYSTEM_* shop cards are never scored (no combat value口径).
- Reference shape 2026-09-05 (89 combat cards): C 18 (20%) / U 45 (51%) / R 26 (29%); U/R in the StS2 target bands, C inflated by common-slot utilities.
- The HTML is the single artifact — do not recreate the `.md`.
