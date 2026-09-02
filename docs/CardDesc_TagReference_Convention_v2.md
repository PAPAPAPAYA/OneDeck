# Card Desc Tag-Reference Convention v2 (2026-09-02)

Distinguishes **card-type references** (a specific card, keyed by `cardTypeID`) from
**tag references** (any card carrying a `Tag`). Notation rule:

- **Card-type reference → bare card name.** 信徒 = the RIFT token (`cardTypeID: RIFT`),
  诅咒 = the JU_ON token (`cardTypeID: JU_ON`). Examples: `复活1敌方诅咒` (ReviveEffect
  `typeIDFilter: JU_ON`), `生成1信徒` (spawns the RIFT token prefab).
- **Tag reference → mandatory carrier phrase.** `N张tag为【X】的[主人]卡`. The 【】
  brackets are reserved exclusively for tag phrases — never for card names, emphasis, or states.
- **Clause-head trigger keywords stay bare** (self-referential grammar, not target refs):
  `遗言：` `苏醒：` `回响：` `被动：` `强化反应：`.
- **State/numeric conditions stay bare, no brackets**: 被强化 (= `attackGrowth > 0`),
  攻击力最高, 非生物.

## Sentence patterns

| Scenario | Template | Example |
|---|---|---|
| Target selection | `动词 N 张tag为【X】的[主人]卡` | 复活1张tag为【信徒】的友方卡 |
| All | `动词 所有tag为【X】的[主人]卡` | 埋葬所有tag为【诅咒】的敌方卡 |
| Trigger | `tag为【X】的[主人]卡[动作]时：` | tag为【诅咒】的敌方卡揭晓时：复活1友方 |
| Count | `每有1张tag为【X】的[主人]卡，…` | 每有1张tag为【信徒】的友方卡，强化1敌方诅咒 |
| Existence | `若无tag为【X】的[主人]卡，…` | 若无tag为【诅咒】的敌方卡，生成1敌方诅咒 |

## Unity rendering mapping (no code change)

`ComputeDynamicCardDesc` does plain-text replacement of `<tag:EnumName>` with the display
name from `TagTooltipDatabaseSO`, so the prefab writes the carrier phrase with the
placeholder inside literal brackets:

```
DB text:   攻击；复活1张tag为【信徒】的友方卡
prefab:    攻击;复活 <b>1</b> 张tag为【<tag:Believer>】的友方卡
rendered:  攻击;复活 1 张tag为【信徒】的友方卡
```

`<tag:Believer>` → 信徒, `<tag:DeathRattle>` → 遗言 (`Assets/SORefs/Strings/TagNames/`).

## Why the distinction is load-bearing

- RIFT_SHEPHERD revives via `ReviveMyCardsWithTag` (Believer **tag**) — a tag reference;
  it can never revive the RIFT token itself (tokens carry no tags, ruling 2026-09-01).
- RELIC_RIFT_OVERRIDE changes only RIFT-token instances (`RiftOverrideAwareReviveEffect`
  is bound solely on the RIFT token prefab) and revives `typeIDFilter: JU_ON` — both are
  card-type references; its desc must stay bracket-free.
- The curse axis is uniformly typeID-keyed: `EnhanceCurse` →
  `FindEnemyCardWithTypeID(JU_ON)` (spawn-if-missing); `onEnemyCurseCardRevealed` fires on
  `cardTypeID == curseCardTypeID`. All `敌方诅咒` texts are card references.

## Migration applied 2026-09-02

Forward (true tag selections → carrier phrase): RIFT_SHEPHERD, EULOGIST
(`BuryMyCardsWithTag`), GRAVE_PUPPETEER (fallback `BuryMyCardsWithTag` inside
`RaiseGraveCreatureOrBuryFallback`).

Reverse (card/state references → strip brackets): RIFT_INSECT_4.0, REVIVE_SUMMONER,
RIFT_HATCHERY, RIFT_MEDIUM, RIFT_PRIEST, RIFT_STRIKER, RELIC_HIVE (`[信徒]` → `信徒`);
HEXER, DETERIORATION_4.0 (×2), SACRIFICIAL_SPIRIT, DOOM_HERALD, RELIC_TALLY,
RELIC_ATTACK_HEX, RELIC_CURSE_GRAVE, RELIC_CURSE_REVIVAL, CURSE_THIRST_BEAST_4.0
(`敌方[诅咒]` → `敌方诅咒`); ELITE_REVIVER (`【被强化】` → `被强化`).

Notion 4.0 DB updated in the same pass (desc column + side-note audit lines):
RELIC_RIFT_OVERRIDE, RIFT_SHEPHERD, EULOGIST, GRAVE_PUPPETEER
(`tools/outputs/notion_update_40db_tagref_descs.js`).

Hygiene: cleared dead `tagsToCheck: [None]` on GRAVE_DREDGER, GRAVE_PUNCH_4.0,
GRAVE_FIST, GRAVE_TOGETHER_4.0, GRAVE_MILLER, SACRIFICIAL_SPIRIT (behavior-neutral:
`Tag.None` never matches; these prefabs bind no `*WithTag` methods). Test card
`4.0/-1_Test/BURY` left untouched.

## Open item

WEAKENING_FIELD desc still reads `除了【诅咒】,所有生物本回合攻击力-1`, but
`ModifyAllCreatureAttackThisRoundExceptCurse` no longer excludes curses (2026-09-02 type
split: creature filter only, creature curses like JU_ON are hit). Desc wording needs a
ruling: either drop `除了诅咒` or restore the exclusion.
