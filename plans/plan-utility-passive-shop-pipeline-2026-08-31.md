# Utility 被动卡与商店出货管线实施计划

日期：2026-08-31
状态：**待执行**。按步骤 gate 推进：每步完成后汇报，用户确认后继续下一步；整体动码需用户明示「修改代码」。
上游：2026-08-31 对话拍板（utility 重构 + 出货管线 + 经济模型）；被动引擎基座 = `plans/plan-4.0-passive-cards-2026-08-29.md`（已实施）。

## 1. 已拍板决策（对话裁决记录）

- **utility 新模型**：在卡组中即生效、占卡位、可卖出（卖出即失去效果）。实现形态 = `takeUpSpace=true` + `isPassive`；效果数值由商店侧**从卡组重算**（进店/买卖/reroll 时按当前 deck 推导，禁止累加式监听器）。
- **【增加卡位】= 特例**（用户二次确认，否决维持现状方案）：购入时卡位 +X 并放逐自身。买后即离场，不存在"卖出回收卡位"路径。必须配套：满编购买豁免（否则 12/12 时买不了唯一能扩位的卡，自锁）+ 商店侧自我移除流程。
- **maxDeckSize 现状 bug**：全工程无写入者（仅 ShopManager.cs:34 声明 + DeckSizeIncreaseEffect.cs:12 clamp 读取），两资产均为 12 → 现有卡位卡 `Clamp(12+X,1,12)` 恒等于 12，数学无效。修复语义：卡位卡同时抬 `deckSize` 与 `maxDeckSize`，它就是唯一抬硬上限的来源。
- **maxHP 常驻化**：+X 上限；进店重算 `hpMax = hpMaxOg + Σ`，`hp = min(hp, hpMax)`（安全：hpMaxOg ≥ 1 永不钳死）。战斗结束自动回满血已存在，无治疗语义纠缠；原 HPMaxAlterEffect 的"买时回满"副效果随之消失。
- **出货管线**：单一 ShopPoolRef + 两个原语——**保底槽**（reserved slot：必从指定类别出货，reroll 时槽内重摇、槽位不消失）与**过滤器**（对某次 roll 的候选池限定/加权）。不做物理分池（check-shop-pool-ref 校验链不碎、类别可交叉）。管线顺序 = 保底槽 → 过滤 roll → 通用 roll；reroll 重跑整条管线。
- **可读性原则**：概率卡要么确定性（必出），要么整面可见摆动；不做不可验证的死概率。
- **稀有度**：保底槽 + 权重两种形态都做、各不止一张；U 保底可每块板必出，R 保底按节奏出（默认每 3 次 reroll 一次，参数可调，初始板不含）；**移除 sessionRarityWeights 自动抬升**，稀有度进程完全玩家驱动；商店悬浮 tips 实时显示当前有效稀有度分布（含 utility 修正后）；权重卡的乘算按稀有度独立可拆配置（如仅 U×2）。
- **reroll 经济**：免费 reroll 每店重置（重算得出）；【每 reroll 3 次随机一卡打折】免费 reroll **计入**计数；打折**不累计**（只作用于当前货架，reroll 生成新板后重新判定）。店内单一 reroll 计数器同时驱动打折节奏与 R 保底节奏。
- **限份数**：同一 utility 卡（按 cardTypeID）限持 1 份；已持有者直接不再进出货池（兼解决 utility 稀释战斗卡出货）。稀有度双形态是多张不同卡，按 cardTypeID 去重互不冲突。
- **商店触发表现**：utility 效果触发时播放 combat 同款 emphasize 脉冲（复用 `SetTargetScale` OutBack 0.25s 模式）。
- **v1 批次**：核心 6 + 管线验证 2（§4 卡表）。
- **墓地计数**：被动照常计数；若审计出问题，再给计数卡加排除被动筛选（不做预防性排除）。
- **死亡螺旋**（utility 挤占卡位导致变弱）：不作为设计约束，属玩家需学习的挑战。
- 经济保持跨战斗保留（现状即如此）；利息不在 v1。

## 2. 代码事实基线（2026-08-31 实测）

- 旧 utility = `takeUpSpace=false`：BuyFunc:209 占位分支 / SellFunc:258 禁卖 / GatherPlayerDeckInfo:348 不显示 / 战斗不实例化；购买时 Instantiate 临时实例触发 onMeBought（ShopManager.cs:230-235），退出商店统一销毁。
- `DeckSizeIncreaseEffect`：deckSize += X 后 clamp [1, maxDeckSize] + SpawnAdditionalEmptySpaces；`HPMaxAlterEffect`：hpMax += X 且 hp = hpMax。
- 被动引擎已落地（84058b8）：`CardScript.isPassive`；每次洗牌后钉在 Start Card 之下墓侧（StartCardShuffleEffect.cs:89-108），永不被揭示；埋/置顶/延后/放逐/复活选取池全排除；全局监听常驻触发；**计入墓地计数**（PassiveCardTests）。
- 经济：EnterShop payday `purse += payCheck`（ShopManager.cs:288，固定值，无修正钩子）；reroll 仅付费档（Reroll():466-488，无免费计数）；出货量 = 固定 int `shopItemAmount`（:54），ShopUXManager 实物卡按 `_spawnedCards` 数量动态生成（选项+N 布局无忧）；权重 = sessionRarityWeights（按 sessionNum 会话表）× shopRollWeightMultiplier（:370-372），无全局权重修正钩子；卖出退款 = 价格/2 硬编码（:259）；满编检查 CountCardsTakingUpSpace（:217-218）。
- `Tag` enum = { None, Linger, ManaX, DeathRattle }，无复活 tag——必出复活卡需隐式追加枚举值（照 StatusEffect.Revive 槽位保留先例，不重编号）。
- ShopUXManager.OnCardPurchased 对 takeUpSpace=false 有"直接移除"分支（:484）——自我放逐卡需新增第三分支。
- 结果面板：ResultStatsPanel 行由 RegisterDeckComposition 预创建，utility 被动会出现全 0 行，需过滤。

## 3. 架构

### 3.1 卡上元数据（CardScript 新增）

- `UtilityKind` 枚举：None / HpMax / Income / ShopOption / FreeReroll / RaritySlotU / RaritySlotR / RarityWeight / RerollDiscount / ReservedUtility / ReservedRevive / RerollCreatureWave。隐式值追加，不重编号。
- `utilityValue`（int）+ `utilityValue2`（int）：通用参数位。例：收入=+X；RaritySlotR = 每 value2 次 reroll 一次；打折 = -value 元、每 value2 次。kind=None 即普通卡，零开销。
- **每稀有度权重乘算**：`utilityRarityWeightMults`（List<(Rarity, float)>，缺省不写=不变）——RarityWeight 类专用，U/R 可拆开独立配置（如仅 U×2，或 U×2 + R×1.5）；多张权重卡按稀有度乘算合并。条目形状沿用 ShopRarityWeightSO.RarityWeightEntry。
- 识别"是否 utility"= `utilityKind != None`（配合 isPassive 驱动战斗侧行为）。

### 3.2 商店重算层（纯静态、可单测）

- `UtilityShopBonus`（纯静态）：输入 playerDeckRef.deck，输出 { income, extraOptions, freeRerolls, rerollDiscountPct 板参数, reservedSlots[], rarityWeightMultByRarity（每稀有度独立乘算、多卡合并）, 已持有 utility cardTypeID 集合 }。
- 调用点：EnterShop（payday + hpMax 重算 + hp 钳制）、每次 buy/sell 之后、每次 GenerateShopItems / Reroll（按需重取）。deck 只在商店期变化，按需重算即全一致。
- 战斗期无重算点（v1 全部为商店域效果；疲劳类如进 v2 再加 GatherDecks 点）。

### 3.3 出货管线

- 板规划（纯静态）：`板 = 通用槽(shopItemAmount + extraOptions) + 保底槽(追加)`。保底槽**追加**在通用槽之后，只增不挤。上界 = 限1 规则天然封顶。
- 保底槽种类（v1）：utility 类别槽、RaritySlotU（每板）、RaritySlotR（rerollCount % N == 0 的板）、Revive tag 槽。
- 过滤器（v1）：RerollCreatureWave——每次 reroll 20% 判定，命中则**该板通用槽**全部只从 isMinion 池 roll（保底槽不受影响）。
- roll 执行：沿用加权 roll（base 表 = rarityWeightRef（该稀有度）× shopRollWeightMultiplier × 该稀有度上全部 RarityWeight 卡乘算之积），每个槽可带类别谓词。
- 去重：已持有 utility cardTypeID 不进任何槽的候选池。
- reroll：整条管线重跑；同一 per-visit 计数器驱动 [免费reroll余量 → 打折判定 → R保底判定]。
- 打折结算：板生成后若 (rerollCount % 3 == 0) 随机一卡价格 -X（仅本板，买入按打折价，显示划线/变色；reroll 自然失效）。

### 3.4 v1 卡表（10 张新 + 2 张重做）

| ID 提案 | kind | 参数（默认，可调） | 触发点 |
|---|---|---|---|
| UTILITY_INCOME | Income | payday +X | 进店 payday |
| UTILITY_SHOP_OPTION | ShopOption | 通用槽 +N | 出货管线 |
| UTILITY_FREE_REROLL | FreeReroll | 每店 +K 免费 | Reroll() |
| UTILITY_RARITY_SLOT_U | RaritySlotU | 每板必出 1 张 U | 保底槽 |
| UTILITY_RARITY_SLOT_R | RaritySlotR | 每 value2(=3) 次 reroll 必出 1 张 R | 保底槽 |
| UTILITY_RARITY_WEIGHT | RarityWeight | 每稀有度独立乘算（v1 起步：U×2 + R×2；可拆如仅 U×2） | 权重层 |
| UTILITY_REROLL_DISCOUNT | RerollDiscount | 每 3 次 reroll 随机一卡 -X | 打折结算 |
| UTILITY_SLOT_UTILITY | ReservedUtility | +1 保底 utility 槽 | 保底槽 |
| UTILITY_SLOT_REVIVE | ReservedRevive | +1 保底复活 tag 槽 | 保底槽 |
| UTILITY_REROLL_CREATURES | RerollCreatureWave | 每 reroll 20% 本板全生物 | 过滤器 |
| （重做）增加卡位 | —（一次性） | deckSize/maxDeckSize +X 后自我放逐 | onMeBought |
| （重做）增加最大生命 | HpMax | +X 常驻 | 进店重算 |

稀有度双形态各不止一张 = RaritySlotU / RaritySlotR / RarityWeight 至少各一张起步，后续按稀有度分档扩卡（v1 先各 1 张验证管线，扩档为纯 prefab 工作）。命名 displayName 走世界观 v2 圈改流程；cardTypeID 保持英文不变。

## 4. 分步实施（gate 制）

- **Step 1 元数据**：CardScript 增 UtilityKind/utilityValue/utilityValue2；Tag 隐式追加 Revive；4.0 复活系 prefab 批量打 tag（名单从 Notion 4.0 DB 复活轴取）。*gate*
- **Step 2 重算层**：UtilityShopBonus 纯静态 + payday/收入 + hpMax 重算钳制 + 免费reroll 计数 + reroll 单计数器与打折结算 + 已持有去重。EditMode 纯静态测试先行。*gate*
- **Step 3 出货管线**：GenerateShopItems 重构为 板规划(静态) + roll 执行；保底槽/过滤器/权重层接入；Reroll 重跑管线。EditMode 覆盖冲突规则（多保底共存、去重、20% 判定、R 节奏）。*gate*
- **Step 4 现存两卡重做**：卡位卡 = takeUpSpace=true + onMeBought(+deckSize&maxDeckSize + 自我从 deck 移除) + 满编豁免 + OnCardPurchased 第三分支；maxHP 卡 = 常驻被动化（prefab 原地改，cardTypeID 不变保统计断档）。*gate*
- **Step 5 v1 批量 + 商店 UX**：10 张 prefab（走 A组批量构建约定：SerializedObject 直改、事件资产名陷阱、desc 规范化）；ShopPoolRef 增补；稀有度分布悬浮 tips（挂店招/section 区，hover 显示 C/U/R 有效权重）；打折价显示；免费 reroll 按钮态；商店版 emphasize 脉冲（注意 payday 早于玩家卡实例生成的时序，脉冲挂生成完成后）。跑 check-shop-pool-ref。*gate*
- **Step 6 移除 sessionRarityWeights**：删 ShopManager 会话表逻辑与字段引用（GetActiveRarityWeightRef 直返基础表），资产清理。*gate*
- **Step 7 统计与审计**：ShopStatsManager 增类别出货占比列；墓地计数联动审计（InGrave 成本检查、GRAVE_* 计数，有问题再上排除被动筛选）；ResultStatsPanel 过滤 utility 全 0 行；pool-audit + infinity-check 跑批；敌方 recorded 卡组 / StartingDeckPool 确认无混入；胜率数据盯移除 session 抬升后的难度曲线。*gate*
- **Step 8 文档**：AGENTS.md 增补 utility/管线小节（wc -c ≤ 32KB 检查）；GameRules.md 经济节更新。

## 5. 风险与开放项

- **移除 session 抬升的曲线风险**：R 获取完全由 RaritySlotR/RarityWeight 驱动；定价过贵则前期商店全程 C 平底、难度曲线失配。Step 7 用胜率/购买数据验证，参数再调。
- **必出 R 节奏参数**：默认 N=3、初始板不含 R 保底；U 保底含初始板。体感不对只调 prefab 参数。
- **保底槽叠加**：限1 后同卡不叠；U 保底卡与 R 保底卡共存 = 两槽并存，按 §3.3 顺序填充。
- **自我放逐的视觉**：逻辑帧内移除、不进玩家卡陈列；反馈靠 SpawnAdditionalEmptySpaces + emphasize 脉冲（若观感不足再加飘字）。
- **墓地计数**：utility 被动放大 InGrave/GRAVE_* 联动，Step 7 审计后裁决是否加排除筛选。
- **enemy/起始池**：authoring 红线，utility 卡不得录入（check-default-enemy-deck-pool 不受影响）。
