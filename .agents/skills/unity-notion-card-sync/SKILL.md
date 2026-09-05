---
name: unity-notion-card-sync
last_reviewed: 2026-09-05
description: Sync the Notion "4.0 card database" (inside the "OD" page) with the actual card prefab configs under Assets/Prefabs/Cards/4.0 in the OneDeck Unity project. Use when the user asks to compare, audit, sync, or update the Notion card list/database against the real Unity card configurations, or mentions "更新notion卡片列表", "sync notion cards", "对比卡片库", "4.0 db 一致性", or similar requests.
---

# Unity <-> Notion Card Sync

Compare the Notion 4.0 card database against the actual card prefabs and update Notion to match. Unity prefabs are the source of truth for card config; the DB owns design lifecycle fields (状态 / side note) that Unity never drives.

## Fixed references

- Notion page: `OD` (id `333827b8-c3c1-80fa-9bae-eb547a15270d`).
- Database: `4.0 card database`; data source `collection://3c7827b8-c3c1-8002-8b45-000bc02fa836`; title property = `CARD_TYPE_ID`.
- Historical: `one deck demo cards` (`collection://2d5827b8-c3c1-81e8-9b87-000b8686c3cd`, 71 rows) is the retired 3.0 library — NOT the sync target.
- Card prefabs: `Assets/Prefabs/Cards/4.0/` (subfolders `0_Common` / `1_Uncommon` / `2_Rare`; exclude `-1_Test/`; rarity is encoded in both the folder tier and the serialized field).
- Pool structure / duplicate audits (axis links, 删并, 状态 lifecycle) belong to the `unity-card-pool-audit` skill, not this one.

## Database schema (property name -> source)

| Notion property | Type | Unity source |
|---|---|---|
| `CARD_TYPE_ID` | **title** | `CardScript.cardTypeID` (literal string) |
| `中文名` | text | `CardScript.displayName` (Chinese) |
| `rarity` | select (`normal`/`uncommon`/`rare`) | `CardScript.rarity` (0/1/2); matches folder tier |
| `ATK` | number | `CardScript.printedAttack` (creatures only; empty for 非生物) |
| `生物` | select (`生物`/`非生物`) | `CardScript.cardType == Creature(1)` |
| `card desc` | text | Chinese shorthand of `CardScript.cardDesc` (strip markup) |
| `tag` | multi_select (combat axes: 埋葬/遗言/强化/信徒/放逐/诅咒/苏醒/被动/复活/强化反应/多次攻击) | design taxonomy, inferred from cardDesc |
| `状态` | select (empty=启用 / `备用` / `已删`) | DB-side lifecycle — NOT synced from Unity |
| `Unity 配置状态` | select (`已配置`/`可直接配置`/`需小改`/`需新机制`) | judged from whether the prefab implements the desc |

`UTILITY_*` / `SYSTEM_*` shop-utility cards have no combat axis: leave `tag` empty and `ATK` unset (a dedicated shop tag is a pending user decision).

## Workflow

1. **Extract Unity cards.** From the project root:

   ```bash
   python .agents/skills/unity-notion-card-sync/scripts/extract_unity_cards.py
   ```

   Writes `tools/outputs/unity_cards_current.json` (UTF-8): file name, cardTypeID, displayName, rarity, cardDesc, printedAttack, cardType, folder. Do NOT rely on console stdout for Chinese text — Windows consoles often render GBK mojibake; read the JSON file instead.

2. **Query the Notion database** via the Notion MCP `query-data-sources` tool:

   ```sql
   SELECT "userDefined:ID", "CARD_TYPE_ID", "中文名", "rarity", "ATK", "card desc", "tag", "状态", "Unity 配置状态", "url"
   FROM "collection://3c7827b8-c3c1-8002-8b45-000bc02fa836"
   ORDER BY "userDefined:ID" ASC
   ```

3. **Compare** (prefabs are truth; join on `cardTypeID` ↔ `CARD_TYPE_ID`):
   - Coverage both ways: engine cards missing from Notion → create rows; Notion rows with no prefab → expected when `状态=备用`/`已删` (report, never delete or "fix"); an 启用 row with no prefab is a finding.
   - `中文名`, `rarity`, `ATK`, `生物`: direct field comparison; fill empty `中文名` from `displayName`.
   - `card desc`: semantic comparison in the existing Chinese shorthand style. Normalize before diffing: strip `<b>` markup, map `<tag:X>` to its display name (e.g. `<tag:DeathRattle>` → 遗言), unify `x`/`×` and full/half-width punctuation, drop whitespace. The DB style systematically omits the `揭晓时:` prefix — that is NOT a divergence. Real divergences: numbers, targeting (友方/敌方), trigger conditions (遗言/苏醒/被动), tag refs, clause order that changes meaning. Update only where meaning changes.
   - `tag` / `Unity 配置状态`: design fields — only fill empties; add a tag option only for a genuinely new concept.
   - `状态` / `side note`: never touch (the pool-audit skill owns them).

4. **Update** via Notion MCP:
   - Missing rows: `create-pages` with `parent: {data_source_id: 3c7827b8-c3c1-8002-8b45-000bc02fa836}` (a batch of ~18 pages in one call works; leave `状态` unset = 启用).
   - Field fixes: `update-page` (`command: update_properties`), one call per row, `page_id` = last UUID segment of the row `url` (dashes optional). `CARD_TYPE_ID` is the title property but is set by name like any other.
   - Multi-select `tag` takes a JSON array of option names matching existing options exactly.

5. **Verify** by re-querying (row counts + changed rows).

6. **Report** a compact table of changes plus judgment-call items.

## Guardrails (learned 2026-08-02, 2026-09-05)

- Skip `-1_Test/` prefabs (test cards). Everything else under `4.0/` is pool material — including `UTILITY_*` and `SYSTEM_INCREASE_*` (the old `_UTILITY/` exclusion applied to 3.0 only).
- **cardTypeID pitfall**: some prefabs carry an earlier shop-view component whose `cardTypeID` is a StringSO reference (`{fileID: ..., guid: ...}`) — currently GRAVE_HEXER, HEXER, RELIC_TALLY, SACRIFICIAL_SPIRIT, DETERIORATION_4.0, DOOM_HERALD. Always take the LAST literal `cardTypeID: <WORD>` line (CardScript serializes last); the brace form never matches a literal pattern.
- **cardDesc is multi-line YAML**: a line-anchored regex (`^  cardDesc: (.*)$`) truncates long values. Use the field-boundary lookahead form `^  cardDesc: (.*?)(?=^  \w)` with `re.S`, and parse occurrences with findall + last-match.
- A prefab with no serialized `rarity` deserializes to `0` = normal; a missing field is not an error.
- `cardDesc` markup: `<b>`, `<tag:EnumName>`, literal `\n`. Strip when comparing; never copy markup into Notion descs.
- **【】 is deprecated (2026-09-04)**: the font lacks those glyphs; tag refs render `[ ]` via `<tag:X>`. If a DB `card desc` still contains 【】 (even non-tag emphasis like 【强化反应】), align it with the prefab text.
- Prefab file name and internal `cardTypeID` can diverge; `CARD_TYPE_ID` tracks cardTypeID — report file-name divergence as a Unity-side issue, do not "fix" it in Notion.
- Notion MCP plan limits: single-data-source SQL (`query-data-sources`) has a shared usage limit on lower plans but is enough here; `query-multiple-data-sources` (JOINs) requires a paid upgrade — never needed for this sync.
