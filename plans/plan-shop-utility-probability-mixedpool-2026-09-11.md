# 商店 utility 概率化 + 混池开关实施计划

日期：2026-09-11
状态：**Step1-3 已实施（2026-09-11）**。Step1 管线+开关核对通过（含 EnterShop 补结算折扣、半价 ceil）；Step2 11 张 prefab 批改 + UTILITY_HALFPRICE_1 新建（1_Uncommon，v=50/v2=100）+ ShopPoolRef 入池；Step3 DB 回写 11 张 desc + 新卡建行（中文名「魇市半价」占位待圈改）并回查验证；Step4 测试用例已改写（Pipeline 31 + Bonus 20），未跑绿（待 Unity EditMode run_tests）。
上游：2026-09-11 对话拍板；被改管线基座 = `plans/plan-utility-passive-shop-pipeline-2026-08-31.md`（已实施）。

## 1. 已拍板决策

- **cadence 全改概率，默认 25%**：UTILITY_TAG_AWAKEN / TAG_CURSE / TAG_REVIVE / SLOT_R / SLOT_U_1(初魇) 的节奏触发（`everyBoards` 取模 / firstBoardOnly）改为**每次生成货架掷一次独立概率**（进店首架 + 每次重掷各掷一次，无保底累积）。prefab `utilityValue2` 字段语义从「间隔版数」改为「触发概率 %」，6 处值 3/0 → 25。**本批统一约定：v2 = 触发概率**。
- **连魇（SLOT_U_2）保留每架必出 1 张 ✦✦**（v2 = 100，走同一概率管线、100% 恒真），与初魇形成 25%/100% 梯度。连带修正 desc 漂移：现 desc 误写「首个货架必出」，实为每架触发。
- **魇市赊账（UTILITY_DISCOUNT_1）重做**：原「每重掷 4 次随机 1 件 −$2」→「每次生成货架 25% 概率：1 件随机商品**半价**」。**半价向上取整**（3→2，5→3）。初始货架也参与（现在 EnterShop 不结算折扣，要补调用）。
- **新增半价被动卡**：每版货架必出 1 件半价商品（= 赊账的 100% 版）。占位 cardTypeID `UTILITY_HALFPRICE_1`，建议落 `1_Uncommon`（赊账是 Common，100% 必中档高一档），名字用户圈改。
- **wave 双卡（百怪入梦/死寂的梦）行为不变**：代码里首架本就参与波纹掷（`ApplyWaveFilters` 对每张战斗板生效），仅 desc 口径从「每次重掷」改为「每次生成货架」。
- **ODDS 双卡改语义适配混池**（单一语义、两种模式下都生效）：
  - 魇市深处（原「首个货架必为奇物架」）→「**首个货架必含 1 件 utility 卡**」（保留首架身份）。
  - 窥魇镜（原「奇物架概率 +15%」）→「每次生成货架 **25%** 概率：追加 1 件 utility 卡」。
  - 实现上并 reservedSlots 管线：ReservedSlotSpec 增 utility 谓词模式；原 `firstBoardUtilityForce` / `oddsBonusPercent` 架型加成字段废弃（分架模式下也不再恢复旧语义）。
- **ShopManager 新 bool `splitUtilityCombatBoards`，默认 false**：false = 混池——跳过 Stage0 架型掷，战斗卡与 utility 卡合一个池子掷通用槽，板槽位 = `shopItemAmount + extraOptions`（`utilityBoardSlotCount` 休眠）；wave 过滤作用于合并池；reserved/概率保底候选 = 合并池。true = 现行分架行为。
- **默认假设（未单独拍板）**：概率独立无 pity；折扣/半价多卡同板命中同一件时 percent 叠加、clamp 100（=白送，罕见组合先接受）；保底候选池空则跳过不补偿（沿用现行规则）。

## 2. 代码事实基线（2026-09-11 实测）

- cadence 判定：`ShopBoardPipeline.ReservedSlotFires`（:225）`boardIndex % every == every - 1`；`GenerateBoard` 由 `GenerateShopItems` 驱动，EnterShop 与 Reroll 都会跑（天然满足「首架+重掷各检测一次」）。
- 计数器：`_boardsGeneratedThisVisit` EnterShop 清零（:400/:688）；`_rerollsThisVisit` 仅 Reroll() 递增；折扣结算 `ApplyBoardDiscount`（:699）目前仅 Reroll() 调用、`_boardDiscounts[script] = totalOff` 平减写入，划线价显示（`GetBoardDiscount`）与成交价（`GetEffectiveBuyPrice`）已就绪。
- 架型掷：`GenerateBoard` Stage0（:55-64），`DefaultUtilityBoardChancePercent=10` + 会话表 + `oddsBonusPercent`；`firstBoardUtilityForce` 仅 board 0 生效。purity 裁定（2026-09-02）：reserved 候选来自已分类板池 → 混池后该限制自然消解。
- ClassifyPools 中 OddsUtility 同时进 combat 与 utility 两池——合并混池时须去重，否则概率翻倍。
- prefab 现值：TAG×3 `utilityKind=10, v2=3`；SLOT_R `kind=6, v2=3`；初魇 `kind=5, v2=0`(firstBoardOnly)；连魇 `kind=5, v2=1`(every=1)；赊账 `kind=8, v=2, v2=4`；wave×2 `kind=11/12, v=20`；ODDS `kind=9`（深处 v=100,v2=1；窥魇镜 v=15,v2=0）。

## 3. 实施步骤（每步一停）

1. **管线 + 开关**：`UtilityShopBonus`（ReservedSlotSpec 增 `chancePercent`/utility 谓词、RerollDiscountSpec 改 `{chancePercent, percentOff}`、Compute 分支迁移、v2=0 兜底 25）；`ShopBoardPipeline`（ReservedSlotFires→概率掷、mixedPool 参数与合并池分支、ClassifyPools 合并去重、架型字段休眠）；`ShopManager`（bool 字段默认 false、EnterShop 补调 ApplyBoardDiscount、半价 off = price − Ceil(price/2)）。
2. **prefab 批改 + 新卡**：8 张改 v2/desc（TAG×3、SLOT_R、初魇 25；连魇 100+desc 修正；赊账 v=50/v2=25+desc；窥魇镜 25+desc；深处 desc；wave×2 仅 desc 口径）；新建 UTILITY_HALFPRICE_1（v=50, v2=100）；ShopPoolRef 入池。
3. **DB 同步**：desc 批量回写 Notion 4.0 DB + 新卡建行（走 unity-notion-card-sync）。
4. **测试补绿**：`ShopBoardPipelineTests` cadence 用例改概率用例（chance 0/100 全确定）；混池用例（不分架 / OddsUtility 不重复入池 / 波纹作用合并池 / reserved 候选含 utility）；赊账首架触发 + 半价 ceil 用例；`UtilityShopBonusTests` 字段迁移。

## 4. 平衡观察项

- 连魇每架必出 U + 混池使 utility 获取率不再受 10% 架型门限制 → shop_stats 观察 utility 持有率与经济通胀。
- 赊账/半价卡从平减 −$2 改半价：对高稀有度商品侵蚀放大（R 卡 12→6）。
- ODDS 双卡旧强度（架型操控）被替换为 utility 追加，强度口径变化记录在案。

## 5. 执行记录（2026-09-11）

- desc 用语：utility 卡用户向称呼取「被动卡」（GameRules「被动 utility 卡」）；节奏口径统一为「每次生成货架」；连魇 desc 按实际行为修正为「每次生成货架必出」（弃「首个货架」误写）。
- prefab 直改 YAML（本会话无 Unity MCP）：cardDesc 单行 + utilityValue/utilityValue2 字段行替换，LF 保持，非 ASCII 写 \uXXXX 转义；新卡复制 UTILITY_DISCOUNT_1 层级、meta 换新 guid 85d0f9d92c6946289e78d5b4c11f4bd0。
- DB 回写用 update-page 逐行（SQL 写路静默失败坑），新卡 create-pages；回查 12/12 全对齐。
- 计划外发现（未动，待拍板）：DB rarity 漂移 3 处 = TAG_AWAKEN/TAG_REVIVE DB=rare vs prefab uncommon；UTILITY_WEIGHT_U/R 前批移夹（0_Common/1_Uncommon）后 DB 未跟（uncommon/rare vs prefab r0/r1）。
- 文档已同步（2026-09-11）：GameRules.md 商店经济节（Money Flow 折扣行 / Board Composition 混池默认 / Guaranteed Slots 概率保底 / Waves）与 ShopSystems.md（plan 引用 + utilityValue2 语义 + GenerateBoard 流程 + 新增 ApplyBoardDiscount 条目）均改为概率+混池口径；其余旧口径命中（CardBalanceAudit HTML、CardDesc_Response_Check.txt、Worldview v3 快照、Sim4 ledger、08-31 旧 plan）属生成物/历史快照，随下次再生成对齐，不手改。
