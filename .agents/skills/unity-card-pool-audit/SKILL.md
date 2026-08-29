---
name: unity-card-pool-audit
last_reviewed: 2026-08-28
description: Audit the 4.0 card pool (Notion "4.0 card database") for duplicate/overlapping card designs, rarity misalignment, overloaded axis-to-axis links, and thin trigger-layer support. Use whenever the user adds, removes, or modifies cards in the pool and wants a structure or duplicate audit, or mentions 卡池审计, 重复卡, 功能重复, 稀有度检查, 轴链接, 删并, 卡池结构 — even without saying "audit".
---

# Unity 4.0 Card Pool Audit

Runs the repeatable card-database audit (duplicates, rarity, axis links, trigger-layer support) against the Notion "4.0 card database" — the single source of truth for 4.0 pool design. Produces a ranked report of redundant-card clusters and candidate cuts; applies changes to the DB only after the user 拍板 (decides).

## Fixed references

- Data source: `collection://3c7827b8-c3c1-8002-8b45-000bc02fa836` (Notion page `4.0 card database`, under `OD` page `333827b8-c3c1-80fa-9bae-eb547a15270d`).
- Local doc: `docs/4.0_Rarity_Iteration_StS2_2026-08-28.md` — rarity ladder + last audit record. It drifts from the DB; **the DB is truth**.
- Notion definition page: `4.0 轴触发对应定义 v1` (https://app.notion.com/p/3ca827b8c3c181b08999ed14b358682c) — axis/trigger-layer definitions as decided 2026-08-28.
- Status update script: `scripts/apply_status_updates.js` in this skill (pattern source: `tools/outputs/notion_update_40db_status.js`).

## Database state semantics (状态 column)

- `状态` select: **empty = 启用** (active), `备用` (reserved), `已删` (deleted). Deleted/reserved rows keep their last `rarity` value for audit history.
- Notion MCP has no archive/delete tool and the REST token (`NOTION_TOKEN`) is usually not in the environment — "deleting" means setting `状态=已删` + appending the reason to `side note`. Real archiving stays manual in the Notion UI.
- Every status change appends a `【YYYY-MM-DD 已删/备用】reason` line to `side note`; the reason is a short audit citation (e.g. 复辟四重奏删并, 与 92 SNOWBALL 孪生删一).

## Axis vocabulary (4.0 词表)

| Axis | DB keywords | Notes |
|---|---|---|
| 复活 R | 复活/苏醒/延迟复活 | 墓地→牌组顶; trigger layer = 苏醒 (fires when revived) |
| 埋葬遗言 M | 埋葬/磨牌/墓地/遗言 | 遗言 folds INTO this axis (fires when buried — every bury path raises onMeBuried, code: BuryChosenCards). 点火源 = any bury EXCEPT enemy-only (bury friendly/self 12 + mill deck-top 6 − overlap = 17); 纯埋葬敌方 ignites the OPPONENT's 遗言, not yours |
| 信徒 B | 信徒/放逐信徒 | believer resource |
| 诅咒 C | 强化诅咒/诅咒攻击力/诅咒揭晓 | |
| 强化 F | 强化/被强化 | trigger layer = 被强化 (reaction on being buffed) |
| 攻击次数 N | 攻击xN/次数+1/次数兑现 | |
| 复辟 E | 复活1攻击力最高敌方 / 复活1敌方诅咒 | merged into ONE family (2026-08-28 拍板) |

Trigger correspondences (已拍板): 复活/信徒 → 苏醒; 磨牌/埋葬友方 → 遗言; 强化 → 被强化.

## Link caps (轴间链接封顶 — do not add beyond these)

- R×M = 6 — capped; no new 复活⇄埋葬 double cards
- R×B = 6 — structural exemption (believers all carry revive), do not re-flag
- R×C = 4, M×C = 4 — capped
- Any pair ≥ 4 links needs justification; 3 links = normal density

## Rarity ladder (StS2 结构)

- **normal (C)**: 即打即用 teaching layer — single effect or 攻击+1 unit resource bridge; small numbers; no engine/growth/large cap; 苏醒/遗言 only as small conditional gifts.
- **uncommon (U)**: bridges/components/light engines — double-invest bridges, light payoffs, light event automata (single small gain), predicate revive/复辟 components, control pieces.
- **rare (R)**: caps/engines/form upgrades — 存量×N big numbers, global/system engines, system rewrites, self-growth ×N (每有1攻击力→X), 复辟+large-value combos, x4 次数 cap.
- Targets: C 15-17% / U 45-52% / R 27-31% of the ACTIVE pool (启用 rows only).

## Workflow

### 1. Pull the database (DB is truth)

Query all rows via Notion `query-data-sources`:

```sql
SELECT url, "userDefined:ID", "CARD_TYPE_ID", "rarity", "ATK", "card desc", "tag", "生物", "side note", "状态"
FROM "collection://3c7827b8-c3c1-8002-8b45-000bc02fa836"
ORDER BY "userDefined:ID" ASC
```

### 2. Baseline: rarity distribution

- Count 启用 rows by rarity; compare with the targets and with the last audit (doc §3); state the delta.
- Flag rarity-criterion violations on new/changed cards (esp. U rows that are caps/global engines).

### 3. Duplicate detection — seven passes

| # | Pass | Method | 2026-08-28 example |
|---|---|---|---|
| 1 | Same-core family | Cluster by normalized core phrase; check whether members share rarity and only the rider differs | 复活1攻击力最高敌方 ×5 (54/65/75/93/97) → cut to 2 |
| 2 | Strict superset | A = B + extra benefit, same rarity | KINGSLAYER ⊃ HEADHUNTER (both U) → delete B |
| 3 | Twin pairs | Same mechanic + same rarity, rider differs | COMBO_WARRIOR ≈ SNOWBALL; RIFT_TWINS ≈ RIFT_PRIEST |
| 4 | Same-shape engines | Passive "when X → Y" with ≥3 identical trigger shapes | mill ×3 (11/43/114); believer-gen ×3 (15/24/41); curse-buff ×3 (20/76/38) |
| 5 | Predicate collapse | Overused targeting predicates | 攻击力最高 ×9 (8 target enemy) |
| 6 | Mechanic islands | Mechanics with only 1 card | 回响/交换/延后 → mark 备用, do not add support in-pool |
| 7 | Author flags | side note remarks like "可能重复/是否太多" | 18/20/38/52 (信徒↔诅咒 links) |

### 4. Axis link matrix

- Tag every active card with the axes it touches; a card bridging two axes = one link for that pair (3-axis cards count once per pair).
- Count links per axis pair; compare vs the caps; list axis sizes (touching-card share).
- Expected shape from the last audit: M ~38%, R ~30% (pool center of gravity), C 23%, F 22%, B 21%, N 21%, E 6%.

### 5. Trigger-layer support density

For each engine axis count trigger sources : payloads:

- 遗言: all non-enemy-only bury sources : deathrattle cards — 17:11 healthy (1.5:1); in a real match both sides' sources fire, so effective ignition is roughly 2× (and the opponent's enemy-only buriers bury YOUR cards)
- 苏醒: revive sources : awaken cards — 18:8 wide
- 被强化: buff sources : reactions — 19:5 thinnest (payoff gap)

Ratios far from 1.3:1, or payloads with no sources, are support-density problems to report.

### 6. Report

Deliver the report in chat (see format below). Do NOT touch the DB in this step — analysis first, apply after 拍板.

### 7. Apply (only after user 拍板)

- Set 状态 + append side-note reason via `scripts/apply_status_updates.js` (reads `updates.json`: `[{"page_id": "...", "status": "已删", "note": "<FULL new side note>"}]`). Page id = last UUID segment of the row `url`. If the `状态` column is missing, add it first via `update-data-source` (colors must be unquoted: `ADD COLUMN "状态" SELECT('启用':green, '备用':yellow, '已删':red)`).
- Update `docs/4.0_Rarity_Iteration_StS2_2026-08-28.md` (§3 distribution, §4 active table, §6 删并/备用 record). Keep CRLF.
- Update memory `card-pool-v4.0-design`.

## Report format

```
## Pool snapshot
启用 N (Cx/Uy/Rz) + 备用 k + 已删 m

## Duplicate clusters (by severity)
| ID | CARD_TYPE_ID | core | rider | rarity | action |
(suggest 删/留/改 + one-line reason)

## Candidate cuts
list + expected distribution change (e.g. U 45→42, R 27→25)

## Over-cap links & thin layers
pairs at/over cap; trigger layers with poor support
```

## Guardrails (learned 2026-08-28)

- **Engine cards missing from the DB are a finding, not a crash**: when a card exists under `Assets/Prefabs/Cards/4.0/` but has no DB row, extract its config from the prefab (cardDesc / printedAttack / rarity / isCreature), audit it anyway, and report the gap with a recommendation to add the row (or confirm it is not an official 4.0 card). The DB stays truth for DESIGN; the prefab folder is where engine cards appear first (2026-08-28 example: SOLDIER_SKELETON / AVENGER).
- **DB is truth**: the local doc §4 descriptions drifted from the DB (50/83/86/90/40/46/64/71) — always re-pull before auditing, never audit from the doc.
- **复活 density is parked**: believers structurally carry revive; do not re-raise R×B=6 or the raw revive count as a primary issue.
- **遗言 is not an axis**: it folds into 埋葬 (M). Never present 遗言×埋葬 as a weak link — it is the axis's internal trigger layer.
- **Report cuts by rarity count** (e.g. the 17-cut example was 9U+8R) so the distribution impact is visible.
- **Ignition = any bury except enemy-only**: 埋葬友方/自埋 AND 磨牌 (mill) both raise onMeBuried (code: BuryNextXCards → BuryChosenCards → onMeBuried) — milled cards' 遗言 fire. 纯埋葬敌方 ignites the opponent's 遗言, so it is excluded from YOUR ignition count. Engine quirk: BuryNextXCards skips `isMinion` cards (3.0 legacy) — 4.0 engine pass must clear it.
- Interaction convention: analysis first, apply after 拍板. The user decides which candidate cuts to keep.
