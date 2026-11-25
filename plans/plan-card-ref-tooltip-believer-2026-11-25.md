# Plan: Hover tooltip card-reference block (believer token 信徒) (2026-11-25)

- **Date**: 2026-11-25
- **Status**: DONE (2026-11-25 evening) - implemented, Play-verified by the user, committed to main (feature commit + a separate GameScene chore commit). EditMode green (17 new tests; full suite 783 = 782 passed / 0 failed / 1 pre-existing skip, re-run on a fresh assembly). The 8.4 layout concern was settled by the user's Play screenshot: an Edit Mode emulation artifact, no layout fix applied.
- **Request (user, 2026-11-25)**: cards that generate or consume the believer token need the hover tooltip to show the believer card's effect, in the form of the current tooltip.
- **Decisions (user, 2026-11-25)**: block title = the bare name `信徒` (no brackets); block position = last; the curse (诅咒) axis is deferred and NOT part of this plan; documentation work goes to the flash subagent.

## 1. Goal / non-goals

Goal:
1. A hovered card whose cardDesc names the believer token gets a trailing card-reference block in the hover tooltip: the token's display name as a bold bare title, then the token's own cardDesc on the next line.
2. Zero card-prefab edits: which cards get the block is derived from the cardDesc notation.
3. The existing card-type block and tag blocks stay byte-identical.
4. The new block is a separately tunable primitive (own label + style tunables).

Non-goals:
- The curse axis (JU_ON token): the same mechanism with one more database entry, but it would hit about 22 cards - evaluate separately.
- Showing the 伪经-rewritten believer effect (follow-up, section 7).
- Any change to tag tooltip texts or cardDesc content.

## 2. Current state (verified facts, 2026-11-25)

- Tooltip structure: `CardTagTooltip.BuildTooltipText` builds one string - card-type block first, then one block per visible tag; each block is a bold `[Name]` title line plus its explanation on the next line, blocks separated by one blank line. The tooltip is a runtime-built canvas with a single TextMeshPro label "Text" (font size 28) under a `VerticalLayoutGroup` + `ContentSizeFitter` panel.
- The `[信徒]` tag block explanation is only `生成或增益[信徒]的效果` - it does not say what the token does (`复活 1 友方,去除自身`).
- Believer token prefab: `Assets/Prefabs/Cards/3.0 no cost (current)/_DONT INCLUDE/Token/RIFT.prefab` (guid `e8e562bae93b8c546a9a5d8c51f25855`), cardTypeID `RIFT`, displayName `信徒`, cardType Token, not a Start Card. Hovering the token itself shows no tooltip (Token has no card-type entry; the token carries no tags).
- Active pool `Assets/Prefabs/Cards/4.0`: 114 cards (117 prefabs including 3 under `-1_Test/`). 15 of them bare-name the token: 12 carry the `Believer` tag, the 3 consumer cards do not (RIFT_GUIDE_4.0 献祭司事, RIFT_REVIVER 以人易物, RIFT_REAPER 血飨) and their tooltip never mentioned the token before.
- 14 pool cards are wired to the token (they reference the token prefab or `Assets/SORefs/CombatRefs/RiftCardTypeID.asset`); all 14 are inside the 15. The 15th, RELIC_RIFT_OVERRIDE 伪经, only rewrites the token's effect.
- RIFT_SHEPHERD 牧羊人 is correctly excluded: its desc uses the tag phrase `tag为[<tag:Believer>]的友方卡` (a tag reference, not the token).
- The 15 cards (table: cardTypeID | name | folder | raw cardDesc | wired via):
	| cardTypeID | name | folder | raw cardDesc | wired via |
	|---|---|---|---|---|
	| RIFT_ACOLYTE | 侍僧 | 0_Common | `生成<b>1</b>信徒;苏醒:生成<b>1</b>信徒` | token prefab (AddTempCard event, twice) |
	| RIFT_INSECT_4.0 | 新皈依者 | 0_Common | `攻击;生成<b>1</b>信徒` | token prefab |
	| RIFT_MEDIUM | 扶乩 | 1_Uncommon | `生成<b>2</b>信徒;苏醒:复活<b>1</b>友方` | token prefab |
	| RIFT_PRIEST | 施洗者 | 1_Uncommon | `生成<b>2</b>信徒;强化<b>1</b>友方实体` | token prefab |
	| RIFT_STRIKER | 鞭笞者 | 1_Uncommon | `攻击;生成<b>1</b>信徒` | token prefab |
	| RIFT_HATCHERY | 黑弥撒 | 1_Uncommon | `生成<b>3</b>信徒;回合开始:埋葬自身` | token prefab |
	| REVIVE_SUMMONER | 复活见证人 | 1_Uncommon | `生成<b>1</b>信徒;每回合一次,复活<b>1</b>友方` | token prefab |
	| RIFT_GUIDE_4.0 | 献祭司事 | 1_Uncommon | `放逐<b>1</b>友方信徒:埋葬<b>2</b>敌方现象` | RiftCardTypeID (consumer, no Believer tag) |
	| RIFT_REVIVER | 以人易物 | 1_Uncommon | `放逐<b>1</b>友方信徒:每回合两次,复活<b>2</b>友方现象` | RiftCardTypeID (consumer, no Believer tag) |
	| RELIC_HIVE | 以血布道 | 2_Rare | `被动:友方攻击时,生成<b>1</b>信徒` | token prefab |
	| RELIC_RIFT_OVERRIDE | 伪经 | 2_Rare | `被动:友方信徒效果变为:复活<b>1</b>敌方诅咒;放逐自身` | not wired (rewrites the token's effect only) |
	| SWARM_CURSER | 诅咒唱诗班 | 2_Rare | `生成<b>1</b>信徒;每有<b>1</b>友方信徒,强化<b>1</b>敌方诅咒` | both |
	| SWARM_QUEEN | 黑圣母 | 2_Rare | `攻击;每有<b>1</b>攻击力,生成<b>1</b>信徒` | token prefab |
	| MASS_SACRIFICE | 集体自焚 | 2_Rare | `埋葬所有友方;每埋葬<b>1</b>友方,生成<b>1</b>信徒` | token prefab (direct field `riftToken` on `MassSacrificeEffect`) |
	| RIFT_REAPER | 血飨 | 2_Rare | `放逐所有信徒,每放逐<b>1</b>,强化<b>1</b>友方实体` | RiftCardTypeID (consumer, no Believer tag) |
- Takeover note: a first implementation attempt (session stopped by the user) left the two new scripts uncompilable (`CardRefTooltipBlocks.cs` lacked `using System;` -> CS0103) and with LF line endings; both were fixed on 2026-11-25 before this session continued.

## 3. Design

- Display form, example 以血布道 (RELIC_HIVE) - new block is the last one:

```
[现象]
不可攻击

[信徒]
生成或增益[信徒]的效果

[被动]
在场期间持续生效的效果

信徒
复活 1 友方,去除自身
```

- Title without brackets: follows the cardDesc notation (`[ ]` = tag phrase, bare = card), and cannot collide with the existing `[信徒]` tag block. No 【】: the tooltip font chain (RobotoCondensed-Regular SDF) has no glyph for it.
- Body = the token prefab's raw `cardDesc`, so the tooltip follows any card-face edit automatically; an optional StringSO `descriptionOverride` lets a designer replace it.
- Trigger rule (`CardRefTooltipBlocks.DescReferencesCard`): strip every `<...>` span (rich text and placeholders such as `<tag:Believer>`), then every `[...]` tag phrase; the block is added when the bare display name still occurs (ordinal contains).
- Block order: card-type block, tag blocks, card-reference block(s).
- The block lives on its own TextMeshPro label "CardRefs" under the panel, not concatenated into the existing string. Tunables (all in `CardTooltipDatabaseSO.cardBlock`, all optional):

	| Tunable | Type | Default | Meaning |
	|---|---|---|---|
	| `titleFormat` | StringSO | null | `{0}` = the referenced card's display name; null/empty = bare name |
	| `titleColor` | ColorSO | null | title color; null = inherits the label color (`GameColorPalette.TooltipTextColor`) |
	| `bodyColor` | ColorSO | null | body color; null = inherits the label color |
	| `fontSize` | float | 0 | card block font size; 0 or below = same as the type/tag text (28) |
	| `spacingBefore` | float | 0 | extra pixels above the block; 0 or below = none |

- Gap above the block = one blank line of the main label (the same separator as between the blocks above) + `max(0, spacingBefore)`.
- Show gate: the tooltip appears when the main text OR the card-reference text is non-empty (`ShowFor` and the enlarge-preview `PinFor`). Consequence: the 3 consumer cards now get a tooltip in the enlarge preview although they carry no Believer tag.
- Known limitation: the match is a substring test on the display name, so a future card whose bare name contains `信徒` would also trigger the block for cards naming it.

## 4. Implementation

| File | Role |
|---|---|
| `Assets/Scripts/SOScripts/CardTooltipDatabaseSO.cs` (new) | data only: `entries` (`cardPrefab` + optional `descriptionOverride`) and the `cardBlock` tunables; lazy singleton `Me` loading `CardTooltipDatabase` from Resources (same pattern as `TagTooltipDatabaseSO`). Any StringSO assigned here must keep `reset = false`. |
| `Assets/Resources/CardTooltipDatabase.asset` (new) | one entry pointing at the RIFT token prefab; tunables at defaults. Created programmatically through the editor. |
| `Assets/Scripts/UXPrototype/CardRefTooltipBlocks.cs` (new) | pure static helpers, no MonoBehaviour: `DescReferencesCard`, `Resolve`, `GetTitle`, `GetBody`, `BuildCardRefText` (two overloads; the one-argument overload uses `CardTooltipDatabaseSO.Me`). |
| `Assets/Scripts/UXPrototype/CardTagTooltip.cs` | private `TooltipContent` (`MainText` + `CardRefText`); the text builders now take the `CardScript` (output unchanged); second label "CardRefs" built in `BuildUI`; `ApplyContent` feeds both labels (each active only with text) and sets the layout gap; `ShowFor` / `PinFor` gate on `HasContent`. `ApplyContent` measures the gap with `GetPreferredValues(string)` (which measures in place on this TMP version) and then writes the main text with `SetText`, because the `text` setter early-outs on an unchanged string. |
| `Assets/Scripts/Editor/Tests/CardRefTooltipBlocksTests.cs` (new) | see section 6. |
| `docs/CardDesc_TagReference_Convention_v2.md`, `docs/RegressionChecklist.md` (row 154), `AGENTS.md` | new tooltip section / row 154 / tooltip bullet rewritten. |

Not touched: `CardScript`, all card prefabs, the tag and card-type databases.

## 5. Rejected alternatives

- Concatenate the block into the existing tooltip string: smaller change, but the block could not be styled separately - rejected (the block must be a tunable primitive).
- Bracketed title: `【信徒】` has no glyph in the tooltip font chain; `[信徒]` is reserved for tag phrases and would duplicate the tag block.
- `信徒(卡牌)` / `信徒(衍生物)` as title: rejected by the user; 衍生物 has not appeared in any card text.
- Position right after the card-type block: rejected by the user (last block wins).

## 6. Verification

EditMode (`CardRefTooltipBlocksTests`), groups:
1. Rule gold: the bare-name test on markup / placeholder / bracket-phrase / empty inputs.
2. Database + text: the Resources asset loads with the RIFT entry; title = bold bare name, body = the prefab cardDesc; style overrides on a transient database.
3. Pool gold: the hit set over the active 4.0 pool is exactly the 15 cards; RIFT_SHEPHERD is not a hit.
4. Wiring guard: every pool card that references the token prefab or `RiftCardTypeID` must be a hit (protects against a future rename or a missed cardDesc silently dropping the block).
5. Zero drift: every non-referencing pool card has an empty card-reference text, so its tooltip is unchanged; the type/tag text keeps its format.
6. Layout: the "CardRefs" label is active only with text; layout gap is 0 unless both labels are active.

Full-suite baseline: pre-change 766 tests (765 passed / 0 failed / 1 pre-existing skip `RecorderAnimationPlayerTests.PlayRecordersCoroutine_SameSourceMultipleRecorders_PopsUpOnce`); required after the change: 0 failed, the same single skip, passed = 765 + new tests. Measured after the change by the implementation subagent: full suite total 783, passed 782, failed 0, skipped 1 (job `64b6edf0affd443e81c749aeb851f450`); targeted `CardRefTooltipBlocksTests` 17/17 (job `61596c55a759426494df5abc09ae7562`). Re-run independently by the main session on the same assembly: full suite 783 total, 782 passed, 0 failed, 1 skipped (job `2b0b56cd7603491c800cafee1adf1e2e`); targeted `CardRefTooltipBlocksTests` 17 / 17 (job `fb94af00958049b68ec72118bca2be83`).

Play verification is done by the user: shop enlarge preview with 以血布道 (RELIC_HIVE) and with a consumer card such as 献祭司事 or 血飨; combat hover on 血飨; hover a believer token on the field (still no tooltip); hover 牧羊人 (no card-reference block); with 伪经 on the field (the block still shows the base text); a card without any believer reference must look exactly as before.

Play verification (user, 2026-11-25 evening): shop enlarge preview on a Believer generator (RIFT_INSECT_4.0, hovered via a one-card PlayerDeckRef rig). Screenshot reviewed by the main session: card-type block, [信徒] tag block, then the new card-reference block (信徒 / 复活 1 友方,去除自身) last - full-width lines, block gaps, no ~200-unit wrapping, no overlap, background sized to the content. The user accepted the feature as done; the remaining checklist spots are covered by the EditMode pool gold and wiring tests (groups 3-5 above).

## 7. Follow-ups

- With 伪经 (RELIC_RIFT_OVERRIDE) on the field, show the rewritten believer effect: `RiftOverrideAwareReviveEffect` switches only the revive half by the `riftOverride…ThisRoundRef` flags and the rewritten text has no field of its own - a StringSO must be added first. Not in this plan.
- Curse axis (JU_ON token): one more database entry, about 22 cards - evaluate separately.

## 8. Progress log (2026-11-25)

Snapshot taken at 20:05 (+08:00) by the main session. This plan was taken over from a stopped session (its claim file is orphaned in `.agent_registry/`); the plan itself only existed in that session's chat and was recovered from its history.

### 8.1 Working tree (uncommitted)

| File | State |
|---|---|
| `Assets/Scripts/SOScripts/CardTooltipDatabaseSO.cs` (+ `.meta`) | new, CRLF, compiles |
| `Assets/Scripts/UXPrototype/CardRefTooltipBlocks.cs` (+ `.meta`) | new, CRLF, compiles; the first attempt left it without `using System;` (CS0103) - fixed |
| `Assets/Resources/CardTooltipDatabase.asset` (+ `.meta`) | new, created programmatically; one entry -> the RIFT token prefab; tunables at defaults |
| `Assets/Scripts/UXPrototype/CardTagTooltip.cs` | modified: `TooltipContent`, builders take `CardScript`, second label "CardRefs", `ApplyContent` / `ComputeCardRefGap` / `ApplyCardBlockStyle`, `SetText` after the gap probe |
| `Assets/Scripts/Editor/Tests/CardRefTooltipBlocksTests.cs` (+ `.meta`) | new, 17 EditMode tests, CRLF |
| `plans/plan-card-ref-tooltip-believer-2026-11-25.md` | new (this file) |
| `docs/CardDesc_TagReference_Convention_v2.md` | +37 lines ("Hover tooltip mapping" section) |
| `docs/RegressionChecklist.md` | +1 row (154) |
| `AGENTS.md` | tooltip bullet rewritten; 31,734 bytes |
| `Assets/Scenes/GameScene.unity` | side effect, unrelated to this feature: (a) the pre-approved `SaveOpenScenes()` persisted Unity's own removal of 4 dangling `m_AddedComponents` entries (`addedObject: {fileID: 0}`) on a PhysicalCardParent prefab instance (+1/-13), (b) one PhysicalCardParent scene instance deactivated (`m_IsActive` 1 -> 0) during the Play verification. Both went into a separate chore commit (user decision, 2026-11-25) |
| `Assets/SORefs/PlayerRefs/PlayerDeckRef.asset` | the user's Play rig, appeared after the 20:05 snapshot: deck = RIFT_INSECT_4.0 only, `resetOnStart` 1 -> 0, `defaultDeck` re-pointed from lethal-infinite-test to PlayerDefaultDeckRef. Stays uncommitted in the working tree (user decision, 2026-11-25) |
| `.zcode/agents/*.md` | untracked subagent definitions (not part of the feature) |

### 8.2 Verified by the main session

- Gold data, twice (YAML scan script + the compiled API): 114 active cards in the 4.0 pool; 15 bare-name the token (12 with the `Believer` tag, 3 consumer cards without); 14 cards are wired to the token and all 14 are inside the 15; RELIC_RIFT_OVERRIDE is the only hit that is not wired; RIFT_SHEPHERD is not a hit.
- `CardTooltipDatabase.asset` loads through `Resources.Load`; `BuildCardRefText` returns `<b>信徒</b>\n复活 1 友方,去除自身` for RELIC_HIVE and RIFT_REAPER, and an empty string for the RIFT token itself and for RIFT_SHEPHERD.
- Pre-change full EditMode baseline (job `ba255d15bfc145a8bdd3f84fe559bc0f`): 766 total, 765 passed, 0 failed, 1 skipped.
- `CardTagTooltip.cs` diff reviewed against the spec; the test file was read in full.

### 8.3 Test evidence (implementation subagent, confirmed by an independent main-session re-run)

- Targeted `CardRefTooltipBlocksTests`: 17 / 17 (job `61596c55a759426494df5abc09ae7562`).
- Full EditMode suite: 783 total, 782 passed, 0 failed, 1 skipped (job `64b6edf0affd443e81c749aeb851f450`) = 765 + 17.
- The subagent's first targeted run was 16 / 17: its own test expectation was wrong (a bracketed title); the expectation was fixed, not the code.
- Main-session re-run at 20:08 on the same assemblies (`Assembly-CSharp.dll` 12:18:37, `Assembly-CSharp-Editor.dll` 12:19:52, both newer than their sources): targeted 17 / 17 (job `fb94af00958049b68ec72118bca2be83`); full suite 783 total, 782 passed, 0 failed, 1 skipped (job `2b0b56cd7603491c800cafee1adf1e2e`), the skip being the same pre-existing test.

### 8.4 RESOLVED: two-label layout at runtime (Edit Mode emulation artifact)

Edit Mode probe by the main session (preview scene, real code, real RELIC_HIVE content, `Canvas.ForceUpdateCanvases()` + `LayoutRebuilder.ForceRebuildLayoutImmediate(panel)` x 4). All sizes in canvas units:

| Scenario | Panel | Main label rect | CardRefs label rect | Generated mesh |
|---|---|---|---|---|
| A. main text only, current flags (= the pre-change tooltip) | 224.0 x 66.0 | 200 x 50 | (inactive) | main: 10 lines, 377.1 tall |
| B. main + card-ref, current flags (current code) | 224.0 x 148.8 | 200 x 50 | 200 x 50 | main: 10 lines, 377.1 tall; card-ref: 3 lines, 108.8 tall |
| C. main + card-ref, candidate fix (section 8.5 item 2) | 332.0 x 438.8 | 308.0 x 308.9 | 256.2 x 81.1 | main: 8 lines, 296.0 tall; card-ref: 2 lines, 69.4 tall |

Facts behind the numbers:

- `VerticalLayoutGroup` as created by `BuildUI` (`AddComponent`, defaults of uGUI 2.0.0): `childControlWidth` / `childControlHeight` = false, `childForceExpandWidth` / `childForceExpandHeight` = true. With control off the group uses each child's own `sizeDelta` both for the panel size and for stacking - it never asks TMP for a preferred size.
- TMP Settings (`Assets/TextMesh Pro/Resources/TMP Settings.asset`) has `m_autoSizeTextContainer: 0`, so a runtime-created label keeps its default 200 x 50 container and never resizes itself.
- Word wrapping is on, so the text wraps at 200 units (about 7 CJK glyphs per line) and overflows the 50-high rect. Asked directly, TMP reports 308.0 x 390.0 (main) and 256.2 x 121.6 (card-ref) as preferred sizes.
- `_layout.spacing` = 32.81 (one blank line of the main label), as designed.

Interpretation: in scenario B the CardRefs rect starts 50 + 32.81 units below the panel top while the main text is 377 units tall, so the two texts overlap IF Play mode lays out like this Edit Mode emulation. But scenario A shows that the pre-change tooltip has the same 200-wide wrapping behaviour under the emulation, which contradicts the tooltip's Play-verified history. So either the emulation is not faithful to Play mode, or the shipped tooltip already wraps at about 200 units. This cannot be settled in Edit Mode, and project rules forbid agent-driven Play-mode visual diagnosis.

Test gap: `ApplyContent_RectCheck_CardRefsBelowMainLabelSharedLeftEdge` passes with 200 x 50 rects because it only asserts stacking order and the shared left edge. It proves nothing about label sizing and must be strengthened once the fix is decided.

TMP fact learned here (com.unity.ugui 2.0.0): `GetPreferredValues(string)` measures its argument in place by overwriting the label's internal text arrays, and the `text` setter early-outs on an unchanged string. `ApplyContent` therefore writes the main text with `SetText`; `Layout_SameContentAppliedTwice_MainLabelStillMeasuredFromRealText` guards it.

Resolution (2026-11-25 evening): the user's Play screenshot settled the open question - in Play the tooltip renders full-width lines with clean stacking and a content-sized background; none of the emulated 200 x 50 symptoms appear. The emulation is unfaithful to Play mode, the candidate fix (former item 2 below) is NOT applied, and the shipped layout path is unchanged.

### 8.5 Closure (all items resolved 2026-11-25 evening)

1. RESOLVED - the requested Play screenshot arrived: no ~7-glyph wrapping, clean stacking, background covers the text (8.4 resolution). The layout problem was neither pre-existing nor new - it existed only in the Edit Mode emulation.
2. RESOLVED - the candidate fix is NOT applied and the narrower variant is moot: Play layout needs no change.
3. RESOLVED - the rect-test strengthening is deliberately skipped: asserting preferred-height equality would fail against the unfaithful Edit Mode emulation (200 x 50 rects). The existing structural assertions (stacking order, shared left edge, gap only with both labels) hold in both worlds and stay; no code changed after the 8.3 re-runs, so the test evidence stands.
4. Done - section 6 carries the Play verdict.
5. Done - user verified in Play and accepted the feature (2026-11-25 evening).
6. Done - committed to main: the feature commit (code + asset + tests + docs + this plan) and a separate chore commit for `GameScene.unity` (both hunks, user decision). `PlayerDeckRef.asset` stays uncommitted (user decision); `.zcode/` agent definitions stay untracked.
7. Done - the closeout session's own claim (`20261125-203221-card-ref-tooltip-closeout.md`) deleted on completion. The two claims of the stopped sessions (`20261125-112246` orphan, `20261125-114806` takeover) become stale-cleanable on 2026-11-27.

### 8.6 Session notes

- Subagents: `flash-worker` (fact finding), `flash-writer` (docs) and `flash-implementer` (code + tests + Unity verification) all launched and completed in this session. A spec correction was delivered to the running implementer with SendMessage and applied.
- Root cause of "cannot use subagents" in the stopped session: the definitions in `.zcode/agents/` lacked a thought level (startup error about a missing thought level), and definitions are cached when a session starts, so a new or edited definition only takes effect in the next session.
