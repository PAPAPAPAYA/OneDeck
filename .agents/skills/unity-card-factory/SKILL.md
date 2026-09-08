---
name: unity-card-factory
description: Create a new OneDeck card prefab from a Notion "4.0 card database" row or a card design. Use when the user asks to build, add, or create a new card prefab (建卡/新建卡/加一张卡), when a Notion DB card has no prefab yet, or when duplicating an existing card with modified numbers/effects. Covers prefab anatomy, effect binding recipes, cardDesc formatting rules, ShopPoolRef registration, and self-checks.
---

# OneDeck Card Factory (建卡)

Pipeline: **Notion DB row → pick template prefab → build via execute_code → register in ShopPoolRef → desc rules → self-check**. Card effects are assembled from the existing effect library; never write new C# for a card unless the user says 修改代码.

## 0. Get the card spec

1. Fetch the Notion row by `CARD_TYPE_ID` (MCP-over-HTTP RPC pattern: refresh OAuth token from `C:/Users/damen/.kimi-code/credentials/mcp`, then `notion-search` + `notion-fetch`; working script: `tools/outputs/_fetch_reviving_striker.js`). If Notion is unreachable, ask the user for: rarity, ATK, desc.
2. Fields → prefab values: `中文名` → `displayName`; `card desc` → `cardDesc` (convert fullwidth punctuation to halfwidth); `rarity` common/uncommon/rare → int 0/1/2 and folder `0_Common`/`1_Uncommon`/`2_Rare`; `ATK` → `printedAttack`.
3. Confirm no prefab already exists: scan all prefabs under `Assets` by root `CardScript.cardTypeID` (DB-only cards exist — e.g. REVIVING_STRIKER before 2026-09-08).

## 1. Pick a template prefab

- Use the closest family sibling (same trigger event + same effect kinds). Read its YAML to copy the structure verbatim: root `CardScript` + `GameEventListener`(s) + one child GameObject per desc clause; each child = `CostNEffectContainer` + effect component(s).
- Resolve event asset GUIDs → names via `AssetDatabase.GUIDToAssetPath` before wiring — never guess (event asset naming trap: `BeforeRoundFinished.asset` is really the round-start event). Common ones: `OnMeRevealed`, `OnMeBuried`.
- Reference builds: `SOLDIER_SKELETON_4.0` (attack + bury→ReviveSelf), `EXILE_BERSERKER` (AttackTimesGiverEffect / `GiveSelfAttackTimes`, mode=3 int=1), `CURSE_SUMMONER` (typeID-filtered revive).

## 2. Build (Unity MCP `execute_code`, compiler auto/Roslyn)

1. `PrefabUtility.LoadPrefabContents(template)` → rename root GameObject to the new `cardTypeID` → edit `CardScript` fields → `SaveAsPrefabAsset(root, dstPath)` → `UnloadPrefabContents(root)` in `finally`. dst = `Assets/Prefabs/Cards/4.0/<rarityFolder>/<cardTypeID>.prefab`. The new asset gets a fresh GUID; check `AssetPathToGUID` differs from the template.
2. `rarity` is an enum: set via `SerializedObject.FindProperty("rarity").intValue`, not direct field assignment.
3. To add an effect component with donor values: `AddComponent(donorType)` on the child, then copy all serialized properties with the `SerializedObject` iterator (`CopyFromSerializedProperty`, skip `m_Script`). `ComponentUtility` is NOT available inside execute_code.
4. UnityEvent persistent calls: `SerializedObject` on the container → `effectEvent.m_PersistentCalls.m_Calls` → **`InsertArrayElementAtIndex`** (plain `arraySize` expansion NREs here) → set `m_Target` (component ref), `m_TargetAssemblyTypeName` (`"Ns.Type, Assembly-CSharp"`), `m_MethodName`, `m_Mode` (1 = void call, 3 = int argument), `m_Arguments.m_IntArgument`, `m_CallState` = 2. Control clause order with `MoveArrayElement` — call order must follow desc clause order.
5. Type resolution: `Type.GetType("X, Assembly-CSharp")` can return null from the dynamic assembly — instead find components by `GetType().Name` matching.
6. Wrap everything in step-logged try/catch and return the log; check `File.Exists(dstPath)` first. A failed run leaves nothing on disk if the save is last.

## 3. Register in the shop pool

Append the prefab ref to the `deck` list of `Assets/SORefs/ShopRefs/ShopPoolRef.asset` (SerializedObject, `arraySize + 1`, `ApplyModifiedPropertiesWithoutUndo`, `SetDirty`, `SaveAssets`), then run the `check-shop-pool-ref` skill to confirm the pool is 1:1 with the 4.0 folder.

## 4. cardDesc formatting rules

- Halfwidth punctuation only (`;` `:` `,`) — no font has fullwidth glyphs; DB rows may show fullwidth, convert on write.
- Numbers: wrap as ` <b>N</b> ` — sign/multiplier/percent INSIDE the bold (`攻击力 <b>+1</b>`, `埋葬数 <b>-1</b>`, `<b>20%</b>`, `权重 <b>×2</b>`), single spaces outside; no space against punctuation. Multi-hit `攻击x2/x3` stays bare.
- Tag references `<tag:X>` per `docs/CardDesc_TagReference_Convention_v2.md`; clause heads (`遗言:` `苏醒:` `被动:` `回响:` `强化反应:`) stay bare; tokens keep mechanism-transparent names (信徒/诅咒).
- desc ↔ bindings must match 1:1 — verify with the `unity-card-listener-check` skill.

## 5. Self-check

1. Reload the saved prefab and dump: `CardScript` fields, root listeners (event asset name → container), each container's calls with mode/int args.
2. EditMode smoke: instantiate a copy, check `GetDisplayName()` and component presence (full effect tests need the HeadlessCombatTestFixture patterns — prefab instances need reflection-injected refs and `callState` flip; see `CurseSummonerPrefabSmokeTests`). Do NOT run Play Mode tests (user-gated, `unity-card-playmode-test`).
3. Flags: `myTags` family tags (e.g. DeathRattle for 遗言 cards); `isPassive` = 1 for RELIC passives; `utilityKind` only for `UTILITY_*`; `takeUpSpace` = 1 for normal cards.
4. Anti-loop rule: never attach multiple looping effect instances to the same card.
5. displayName uses the current worldview naming doc (`docs/OneDeck_Worldview_Naming_v3_CultPulp.md`) — tokens exempt (机制透明).
