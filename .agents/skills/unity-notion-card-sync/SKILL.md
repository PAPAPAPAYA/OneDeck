---
name: unity-notion-card-sync
last_reviewed: 2026-08-02
description: Sync the Notion card database ("one deck demo cards" inside the "OD" page) with the actual card prefab configs in the OneDeck Unity project. Use when the user asks to compare, audit, sync, or update the Notion card list/database against the real Unity card configurations, or mentions "更新notion卡片列表", "sync notion cards", "对比卡片库", or similar requests.
---

# Unity <-> Notion Card Sync

Compare the Notion card database against the actual card prefabs and update Notion to match. Unity prefabs are the source of truth.

## Fixed references

- Notion page: `OD` (id `333827b8-c3c1-80fa-9bae-eb547a15270d`), under `Design Notes`.
- Database: `one deck demo cards` (id `2d5827b8-c3c1-8009-bb86-cbdff51eb413`).
- Data source: `collection://2d5827b8-c3c1-81e8-9b87-000b8686c3cd`.
- Card prefabs: `Assets/Prefabs/Cards/3.0 no cost (current)/` (subfolders `Bury and buried/`, `Conjure/`, `Curse/`, `General/`; rarity is also encoded in the `0_Common` / `1_Uncommon` / `2_Rare` folder tier).

## Database schema (property name -> source)

| Notion property | Type | Unity source |
|---|---|---|
| `name` | text | `CardScript.displayName` (Chinese) |
| `file name` | text | prefab file name without `.prefab` |
| `description` | **title** | short English shorthand of `CardScript.cardDesc` |
| `rarity` | select (`common`/`uncommon`/`rare`) | `CardScript.rarity` (0/1/2); matches folder tier |
| `tag` | multi_select (design taxonomy) | inferred from cardDesc semantics |
| `category` | select (`无条件`/`必定能满足条件`/`需要满足条件`/`产出资源`/`消耗资源`) | inferred from cardDesc semantics |

## Workflow

1. **Extract Unity cards.** From the project root:

   ```bash
   python .agents/skills/unity-notion-card-sync/scripts/extract_unity_cards.py
   ```

   Writes `tools/outputs/unity_cards_current.json` (UTF-8). Do NOT rely on console stdout for Chinese text — Windows consoles often render GBK mojibake; read the JSON file instead.

2. **Query the Notion database** via the Notion MCP `query-data-sources` tool:

   ```sql
   SELECT "name", "file name", "description", "rarity", "tag", "category", "url"
   FROM "collection://2d5827b8-c3c1-81e8-9b87-000b8686c3cd"
   ```

3. **Compare** (prefabs are truth):
   - Coverage: cards missing from Notion (create rows via `create-pages` with parent `data_source_id: 2d5827b8-c3c1-81e8-9b87-000b8686c3cd`); Notion rows with no prefab (report before deleting — may be planned cards).
   - `file name`, `name`, `rarity`: direct field comparison.
   - `description`: semantic comparison in the existing English shorthand style (e.g. `bury 1 friendly; deal dmg x 2`). Check numbers, targeting (friendly vs hostile), trigger conditions (`if buried`, `if staged`, `linger:`), and exclusions. Update where the meaning diverges; ignore pure wording differences.
   - `tag` / `category`: only fill when empty (infer from semantics following existing rows of the same archetype); do not re-tag filled rows unless clearly wrong.

4. **Update** via Notion MCP `update-page` (`command: update_properties`). Page id = last UUID segment of the row `url`. `description` is the title property but is set by name like any other property. Multi-select `tag` takes a JSON array of option names (must match existing options exactly; add new options only if a genuinely new concept appears).

5. **Verify** by re-querying the changed rows.

6. **Report** a compact table of changes plus the judgment-call items below.

## Guardrails (learned 2026-08-02)

- Skip `_DONT INCLUDE/` (tokens, recycle bin, test cards) and `_UTILITY/` prefabs.
- A prefab with no serialized `rarity` field deserializes to `0` = common (e.g. `GRAVE_TOGETHER`, `MAD_SCIENTIST`); do not treat the missing field as an error.
- `tag`/`category` are a design taxonomy, not serialized Unity data. Never silently change them based on guesses — only fill empties, and report inconsistencies (e.g. rows tagged `linger` whose prefab has empty `myTags` and no `<tag:Linger>` in cardDesc, like `MOTH_MAN` / `CURSE_THIRST_BEAST`).
- Prefab file name and internal `cardTypeID` can diverge (e.g. file `GOBLIN_ASSASSIN_TEAM.prefab` with `cardTypeID: GOBLIN_ASSASIN_TEAM`). The Notion `file name` column tracks the **file** name; report the `cardTypeID` mismatch as a Unity-side issue, do not "fix" it in Notion.
- Duplicate file names across folders are possible: `CURSE_THIRST_SUMMONER.prefab` exists in both `Curse/0_Common` (咒食的召唤师) and `General/1_Uncommon` (咒食的大召唤师). Both Notion rows share the same `file name` and are distinguished by `name`/`rarity` — this is expected, not a data error.
- `cardDesc` contains markup: `<b>`, `<dmg>`, `<dmg:staged>`, `<counter>`, `<tag:EnumName>` placeholders, and literal `\n` sequences. Strip mentally when comparing semantics; do not copy markup into Notion descriptions.
