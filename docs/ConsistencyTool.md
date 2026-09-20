# Three-Way Card Consistency Tool

Checks that the same card set agrees across the three places card data lives, and writes a
readable HTML report:

| Side | Source of truth | Produced by |
|------|-----------------|-------------|
| Engine | `Assets/Prefabs/Cards/4.0/**.prefab` | `tools/outputs/extract_unity_cards_40.py` |
| Design | Notion "4.0 card database" | `tools/outputs/notion_query_40db.js` |
| Serving | server `card_catalog` table (ECS) | `tools/outputs/dump_catalog.py [--prod]` |

The extractors are separate scripts so each side can be refreshed on its own; the checker only
reads their JSON/TSV output. All four live under `tools/`, so nothing here needs the Unity Editor.

## Quick start

Double-click `tools/check_consistency.bat`, or from a shell:

```
python tools/check_consistency.py --refresh --prod
```

Either form refreshes all three sides, prints a per-issue summary to the console, writes
`tools/outputs/consistency_report_<date>.html` and exits non-zero if anything is at ERROR level.
The `.bat` also opens the report in your browser and pauses so the summary stays readable.

To check the dumps already on disk without refreshing anything (fast, no network - the mode to
use while the Notion quota is spent, see pitfall 1):

```
tools\check_consistency.bat -cached
```

## Flags

| Flag | Effect |
|------|--------|
| (none) | Check the existing dumps only - fast, no network, no prod access |
| `--refresh` | Re-run the three extractors first |
| `--prod` | Catalog step reads the live ECS DB instead of the local dev DB |
| `--report PATH` | Write the report somewhere other than `tools/outputs/consistency_report_<date>.html` |

The `.bat` wraps the first three: no argument = `--refresh --prod`, `-local` = `--refresh` against
the dev DB, `-cached` = no refresh at all.

Exit codes: `0` clean, `1` ERROR-level drift found (report written), `2` a refresh step failed
(inputs untouched, no new report).

## What it reports

- **coverage** - prefabs with no Notion row, prefabs not uploaded to the catalog, catalog rows
with no prefab (zombie rows), Notion rows never built
- **field drift** - display name, rarity, tags, ATK, extra attack times, card desc, 实体/现象
predicate (each side compared pairwise)
- **catalog health** - cost vs the rarity price baseline, `game_version` row counts, and
staleness (prefab committed after the row was last uploaded)

Severity is per-issue, not global: a card whose Notion row is still marked in-progress
(需新机制/需小改/可直接配置) has its drift downgraded to WARN.

## Requirements

- python 3 and `node` on PATH.
- `--prod` needs the Alibaba Cloud Workbench CLI (`~/.workbench/bin/workbench`) with a profile
that can reach instance `i-uf66n1ofpudgn9b6rg7o`. The remote command is a read-only SELECT.
- Notion access comes from the kimi MCP credential cache
(`C:/Users/damen/.kimi-code/credentials/mcp/notion-*-tokens.json`), refreshed over OAuth by
`notion_query_40db.js` itself. That path is machine-specific: on another machine, point the
`CRED` constant at that machine's cache or the Notion step fails.

## Pitfalls worth knowing

1. **Notion enforces a usage limit on queries.** Once the quota is spent the MCP refuses SQL with
"Your workspace has reached the usage limit for Query Data Source" (it resets later; a paid
plan lifts it). The refresh then exits `2` and leaves the report untouched - check the dumps
already on disk with `tools\check_consistency.bat -cached`, and refresh the Notion side once
the quota is back. Only that one step is affected: the prefab and catalog steps keep working,
so re-running `--refresh` later is not wasted work.
2. **`--prod` matters.** Local mode reads `server/onedeck-api/data/onedeck.db`, which nothing
syncs from prod. A stale local DB does not error - it just produces a report full of phantom
catalog drift. `dump_catalog.py` prints the DB path and mtime so you can spot it; prefer
`--prod` for anything you intend to act on.
3. **Tag conventions differ per side on purpose.** The Notion `tag` column mirrors `myTags`
only; the catalog mirrors `myTags + reservedTag` (that is what `CardCatalogUploader` sends).
`reservedTag` is a functional field (used by `UtilityShopBonus`), not a display tag.
4. **Notion SQL drops number columns on large scans.** A full-table `SELECT *` can return NULL
for number columns at random, while a filtered `WHERE` query returns them intact. The query
script therefore selects an explicit column list and re-fetches `ExtraATKTimes` with a
filtered query, overlaying the result. Do not "simplify" it back to `SELECT *`.
5. **The Notion dump's projection is narrow** (no `id`/`url`/`createdTime`/`side note`). Scripts
or comparisons that expect those columns will silently find nothing.
6. **Rarity is compared canonically** (`normal` == `common`), and descs are normalized before
comparison: `<b>` stripped, `<tag:X>` expanded to its Chinese display name, fullwidth
punctuation folded to halfwidth.
7. **Prefab renames create three-sided cleanup work.** Renaming a `cardTypeID` shows up as four
ERRORs (new ID missing on the Notion and catalog sides) plus zombie rows for the old ID. Do
the rename in all three places, then re-run with `--refresh --prod`.
8. **A short catalog dump is refused, not written.** `dump_catalog.py` compares the rows that
arrived against the row count the remote reported and fails on any mismatch. Transport loss
was observed once (2 of 115 rows never arrived); retrying the dump is the fix, and without the
guard those 2 rows would have shown up as phantom "not uploaded to the server" drift.

## Files

Tracked in git, so a report is archived by its date in the filename:

- `tools/outputs/unity_cards_40_current.json` - engine snapshot
- `tools/outputs/notion_40db_rows.json` - Notion snapshot
- `tools/outputs/catalog_current.tsv` + `catalog_versions.tsv` - serving snapshot
- `tools/outputs/consistency_report_<date>.html` - the report

Refresh them (and commit the result) after any card change, so the next comparison starts from a
faithful baseline.

## Dated-artifact convention (2026-09-20)

Every recurring tools product is written twice: the fixed-path alias above (what consumers read)
plus a dated twin `<stem>_<YYYYMMDD_HHMMSS>.<ext>` — the uploadable, browsable artifact
(`unity_cards_40_20260920_204838.json`, `catalog_20260920_204932.tsv`, …). Generators implementing
it: `extract_unity_cards_40.py`, `notion_query_40db.js`, `dump_catalog.py`, the notion-sync skill's
`extract_unity_cards.py`, and `extract_card_prefabs.py --out`. The shared helper is
`tools/scripts/dated_output.py` — new recurring generators import it (`alias_copy(path)`) instead of
hand-rolling the twin logic. Commit dated twins when you want a snapshot archived (same cadence as
the snapshots above); the aliases stay the pipeline inputs.
