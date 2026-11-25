# Plan: Hover tooltip card-reference block — curse axis (JU_ON 诅咒) (2026-11-25)

- **Date**: 2026-11-25
- **Status**: IMPLEMENTED, tests green, uncommitted (2026-11-25 ~23:00 +08:00). Targeted `CardRefTooltipBlocksTests` 21/21 and full suite 787 = 786 passed / 0 failed / 1 pre-existing skip, both on a fresh assembly. Play verification per section 6 checklist, then commit.
- **Request (user, 2026-11-25 evening)**: the curse token gets the same hover-tooltip card-reference block as the believer token. Proposal presented with one decision point; user picked option A and said 修改代码.

## 1. Correction recorded (proposal vs reality)

The proposal claimed the JU_ON token's display name is 蛊咒 (a glyph misread of `\u8BC5\u5492` by the
main session) and therefore proposed a new `Entry.descName` alias field. `chr(0x8BC5)+chr(0x5492)`
is 诅咒 — the token's display name IS the desc word. Option A's outcome (block title = the word
the card text uses) is the NATIVE behavior of the shipped mechanism, so the alias field was dropped
and NO production code changed. Only the database asset gained an entry.

## 2. Verified facts (4.0 pool = 114 cards; rule identical to the believer axis)

- JU_ON token: `Assets/Prefabs/Cards/3.0 no cost (current)/_DONT INCLUDE/Token/JU_ON.prefab`
  (guid `670314465ad32b1469ca4556495e4456`), cardTypeID `JU_ON`, displayName 诅咒, cardType Token,
  cardDesc `对自身造成[攻击力]点伤害`, not a Start Card. Its desc bare-names neither token, so the
  token itself keeps showing no tooltip (same as RIFT).
- Bare-name hits: 诅咒 22, 蛊咒 0 (蛊咒 does not exist anywhere), 信徒 15 (believer gold reproduced —
  scanner sanity). All 22 curse hits are genuine card references (uniform 敌方诅咒 phrases; the
  convention doc's wiring section confirms: EnhanceCurse → `FindEnemyCardWithTypeID(JU_ON)`,
  `onEnemyCurseCardRevealed` fires on `cardTypeID == curseCardTypeID`).
- The 22 (cardTypeID | name): CURSE_REVIVER 收蛊人, GRAVE_HEXER 降头师, HEXER 蛊婆,
  SACRIFICIAL_SPIRIT 活殉, CURSE_EATER 吞蛊人, CURSE_GARDENER 养蛊人, CURSE_SUMMONER 走阴人,
  CURSE_THIRST_BEAST_4.0 噬咒兽, CURSE_THIRST_SHAMAN_4.0 噬咒萨满, DETERIORATION_4.0 蛊噬,
  DOOM_HERALD 报丧人, HEXBLADE 咒刃, RELIC_CURSE_GRAVE 勾魂簿, RELIC_CURSE_REVIVAL 耳语唤尸,
  RELIC_GRAVE_CURSE 积尸气, RELIC_TALLY 血账, WOKEN_HEX 咒茧, RELIC_ATTACK_HEX 饲咒,
  RELIC_BLOOD_PACT 血契, RELIC_CURSE_HASTE 提前发作, RELIC_RIFT_OVERRIDE 伪经, SWARM_CURSER 诅咒唱诗班.
- Wiring (two forms): a serialized object reference to the JU_ON prefab (11 cards) and an inline
  `cardTypeID: JU_ON` string on a component (CURSE_EATER only) — 12 cards, all inside the 22.
  `CurseCardTypeID.asset` is NOT usable as wiring evidence: a default-valued `cursedCardTypeID`
  field pointing at it is serialized on most pool cards (93 hits = noise).
- Dual-reference cards (both blocks, believer first = database order): RELIC_RIFT_OVERRIDE 伪经,
  SWARM_CURSER 诅咒唱诗班.
- `Tag.Curse` exists in the enum but has no `TagTooltipDatabase` entry — a card carrying the tag
  shows a bare `[诅咒]` title-only tag block above the new card block (bracket vs bare, same
  pattern as [信徒]/信徒).
- Compiled-API probes (execute_code, 2 entries in the asset): RELIC_HIVE → believer block only;
  CURSE_GARDENER / CURSE_EATER → `<b>诅咒</b>\n对自身造成[攻击力]点伤害`; SWARM_CURSER /
  RELIC_RIFT_OVERRIDE → both blocks believer-first; RIFT_SHEPHERD / RIFT / JU_ON → empty.

## 3. Changes

| File | Change |
|---|---|
| `Assets/Resources/CardTooltipDatabase.asset` | entry 2 → JU_ON prefab (via editor execute_code, SetDirty + SaveAssets; no schema change) |
| `Assets/Scripts/Editor/Tests/CardRefTooltipBlocksTests.cs` | 17 → 21 tests: `Database_Singleton_LoadsRiftAndCurseEntries` (renamed/extended), curse golden asserts, curse rule examples, pool hit-set gold becomes the 15+22 union, NEW `Resolve_CardPool_CurseHitSet_MatchesJuOnEntry`, NEW `WiringGuard_OnlyHitCardsReferenceJuOnPrefab` (JU_ON prefab only, asset noise documented), NEW `WiringGuard_CurseInlineCardTypeIdStringIsHit` (string-property scan), `TooltipContent_NonHitsHaveEmptyCardRefText_HitsHaveGolden` → `TooltipContent_CardRefTextMatchesComposition_AllPool` (composition mirror), NEW `TooltipContent_DualReferenceCards_GetBothBlocksBelieverFirst` |
| `docs/CardDesc_TagReference_Convention_v2.md` | hover section: entry list updated (RIFT + JU_ON), dual-card rule, no-alias note |
| `docs/RegressionChecklist.md` | row 155 |
| `plans/plan-card-ref-tooltip-curse-2026-11-25.md` | this file |

NOT changed: `CardRefTooltipBlocks.cs`, `CardTooltipDatabaseSO.cs`, `CardTagTooltip.cs`,
all card prefabs, `AGENTS.md` (the tooltip bullet is already generic — "a cardDesc that
bare-names a DB card" — and the file keeps its ≥1 KB size headroom).

## 4. Rejected alternatives

- `Entry.descName` alias field (the proposal's own mechanism): unnecessary once the glyph misread
  was corrected — display name == desc word.
- Renaming the token or touching descs: the desc word already equals the display name.
- Wiring-derived triggers via `CurseCardTypeID.asset`: 93 default-value hits make it meaningless.
- Per-entry block styling: both entries share the one `cardBlock` tunables; extend the schema only
  if a real styling need appears.

## 5. Known limitations

- Substring match on 诅咒: a future desc using the word generically (e.g. 被诅咒的) would trigger a
  false block. Current pool: 0 such cases. Same known limitation class as the believer axis.
- Dual cards get a longer tooltip (two blocks + blank line).

## 6. Verification

EditMode `CardRefTooltipBlocksTests` 21/21 (job `8063d35992ee40abb39ef96f20459634`, fresh assembly:
`Assembly-CSharp-Editor.dll` 22:47:34 > tests.cs 22:47:22, `isCompiling` false). Full suite:
787 total, 786 passed, 0 failed, 1 skipped (job `0e4efd6cdf754f35a18897a4c81ba688`) —
782 previously-passing + 4 new tests, the skip being the same pre-existing
`RecorderAnimationPlayerTests` nested-coroutine ignore. Zero drift.

Play verification (user): shop enlarge on 养蛊人 (single curse block) and 吞蛊人; enlarge
诅咒唱诗班 and 伪经 (believer block first, then 诅咒 block); a card with no token reference looks
exactly as before; hovering the 蛊咒/诅咒 token or the 信徒 token on the field still shows no
tooltip; 牧羊人 unchanged.

## 7. Progress log

- 2026-11-25 ~22:37: claim `20261125-223715-curse-ref-tooltip-impl.md` created; pool scan (escape-decoding
  YAML scanner, believer gold reproduced 15/15); glyph correction 诅咒 (not 蛊咒); proposal revised to
  zero-code-change; user approved option A + 修改代码.
- 2026-11-25 ~22:45: asset entry 2 added via execute_code + gold probes green.
- 2026-11-25 ~22:47: test file updated (python binary-mode edits, CRLF + tabs verified, 21 tests),
  `refresh_unity` compile requested, assembly freshness verified by mtime, `SaveOpenScenes()` before runs.
- 2026-11-25 ~22:48: targeted run 21/21 (job `8063d35992ee40abb39ef96f20459634`).
- 2026-11-25 ~22:52: full suite 787 = 786 passed / 0 failed / 1 pre-existing skip (job
  `0e4efd6cdf754f35a18897a4c81ba688`); docs (convention / checklist row 155 / this plan) landed; commit pending user's go-ahead.
