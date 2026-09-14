# Plan: UTILITY_OPTION_P_1 概率货架卡 + OPTION_1 升 Uncommon

日期: 2026-09-14。状态: **已实施** (2026-09-14 同日落地)。

## 实施记录

- Phase 1 (引擎): `UtilityKind.ShopOptionChance` (=13, append-only) / `Bonus.extraBoardSlots` + `extraBoardSlotsChancePercent` / `Compute` case (slots clamp ≥0, chance 累加, ≤0 由 `ChanceOrPercent` 落 25) / `GenerateBoard` 从既有 `bonus` 参数读取并独立 roll (未加新参数, 比 plan 原稿更简) / 诊断 log 增 `chanceExtraSlots`。`ShopManager` 零改动 (bonus 已在传参链)。dotnet 离线编译双程序集 0 错。
- Phase 1 (测试): `UtilityShopBonusTests` +2 用例 (求和/负数 clamp) + EmptyDeck 补断言; `ShopBoardPipelineTests` +7 用例 (战斗架/奇物架/混合模式/0格守卫/多格/chance 0→默认25 统计界/reserved 不被挤占)。EditMode 套件待 Unity 内跑。
- Phase 2 (资产): 新 prefab `0_Common/UTILITY_OPTION_P_1.prefab` (复制 OPTION_1: kind 13, v=1, v2=25, rarity 0, myTags Passive, 新 GUID `f36803977b7a43a4a76b1c6ca3361157`); OPTION_1 `git mv` → `1_Uncommon/` (GUID 不变); ShopPoolRef 增引用条目 (113 项)。
- Phase 3 (DB/docs): Notion 4.0 DB 新增 UTILITY_OPTION_P_1 行 (魇市游摊/normal/被动/已配置, ID 待系统自增) + OPTION_1 rarity normal→uncommon + side note 记录; GameRules.md Board Composition 增 chance-extra-slots 条; ShopSystems.md metadata 行补 ShopOptionChance。
- **命名勘误**: 家族前缀是「魇市」(\u9B47=魇), plan 原稿误写「魅市」; prefab/DB/文档均已按魇市落。
- **待办**: Unity 内 refresh 后跑 EditMode 全量套件; 卡名「魇市游摊」待圈改。

## 拍板记录 (2026-09-14)

1. **稀有度阶梯**: 新卡 Common；UTILITY_OPTION_1 升 Uncommon。对齐 SLOT 家族「概率版低稀有、必出版高稀有」阶梯（SLOT_U_1 25%✦✦ Common → SLOT_U_2 必出✦✦ Uncommon）。
2. **触发时机**: 每次生成货架独立 roll（含重掷，无保底无节奏），与 SLOT 家族同语义；重掷可能丢/得 +1，已拍板接受。
3. **cardTypeID**: `UTILITY_OPTION_P_1`（避开 OddsUtility 语义占用的 ODDS 字样）。

## 新卡规格

| 字段 | 值 |
|------|----|
| cardTypeID | UTILITY_OPTION_P_1 |
| displayName | 魅市游摊（占位，待圈改） |
| rarity | 0（Common，✦） |
| utilityKind | ShopOptionChance（**新枚举值，append-only 追加在末尾**） |
| utilityValue | 1（命中时货架 +N） |
| utilityValue2 | 25（概率 %；≤0 → 默认 25，沿用 `ChanceOrPercent` 语义） |
| cardDesc | `被动: 每次生成货架 <b>25%</b> 概率: 货架 <b>+1</b>`（halfwidth 标点） |
| prefab | `Assets/Prefabs/Cards/4.0/0_Common/UTILITY_OPTION_P_1.prefab`（复制 UTILITY_OPTION_1 改造） |

- 语义: 持有期间每次生成货架（战斗架/奇物架都生效，含重掷）roll 一次，命中则该板 generic 槽位 +1（加权正常 roll，同 OPTION_1 的 +1 语义，只是条件触发）。
- own-once（owned typeID 从商店池剔除）保证单张，25% 无叠加问题；与 OPTION_1 可共存 → 理论 +2，设计内。
- utility passive 标准属性: deck-resident、占卡位、可出售（半价）、不进 result per-card stats。

## OPTION_1 连带改动

- prefab 移动 `0_Common/` → `1_Uncommon/`（.meta 随行，GUID 不变，引用安全）；ID/desc/displayName 不动。
- Notion 4.0 DB rarity 更新 Common → Uncommon。
- 价格自动切 Uncommon 价签（`ShopManager.GetCardPrice` 按 rarity IntSO），商店权重按新稀有度走 session 表。

## 实施步骤

1. `EnumStorage.UtilityKind` 末尾追加 `ShopOptionChance`（append-only，不重排现有值）。
2. `UtilityShopBonus`: `Bonus` 增 `extraBoardSlotCount` / `extraBoardSlotChancePercent`；`Compute` 新 case: count += utilityValue，chance += utilityValue2（clamp 0..100，≤0 → 25）。
3. `ShopBoardPipeline.GenerateBoard`: 新增两参数（默认 0）；板类型确定后、generic roll 前独立 roll，命中 `genericSlotCount += extraBoardSlotCount`（mixed/split、战斗架/奇物架通吃）；诊断 log 加 extraSlots 字段。
4. `ShopManager.GenerateShopItems`: 传 bonus 两字段。
5. 新 prefab 按 unity-card-factory 约定创建；勾入 ShopPoolRef（用 check-shop-pool-ref skill 验证覆盖）。
6. OPTION_1 prefab 移文件夹 + DB rarity 更新。
7. Notion 4.0 DB 新增 UTILITY_OPTION_P_1 行（注意: SQL 路径写库会静默失败，须用 MCP update/create 走 search 解析 id）。
8. docs: `docs/ShopSystems.md` utility kind 说明补一行；GameRules.md 若有货架/utility 枚举处同步。
9. EditMode 测试: `ShopBoardPipelineTests`（chance 0 = 恒不加格 / 100 = 恒 +1 / seed 确定性 / split 模式两类板 / mixed 模式）+ `UtilityShopBonusTests`（recompute 求和、≤0 默认 25）。

## 边界确认

- 重掷重 roll（可能丢 +1）— 已拍板接受。
- reserved 槽位（rarity/tag/ODDS）不受影响，appended last 逻辑不变。
- 货架槽位无上限（与 OPTION_1 现状一致，own-once 天然封顶）。
- 改动不触碰埋葬/战斗链路，仅商店生成层。
