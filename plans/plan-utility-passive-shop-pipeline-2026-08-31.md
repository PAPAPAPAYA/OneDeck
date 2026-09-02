# Utility 被动卡与商店出货管线实施计划（v2）

日期：2026-08-31（v1）/ 2026-09-01（v2 修订：分板出货、基线成长回归、卡位卡改回购计量条、tag 槽扩族、咒物潮、HP 翻倍）
状态：**待执行**。按步骤 gate 推进：每步完成后汇报，用户确认后继续；整体动码需用户明示「修改代码」。
上游：2026-08-31～09-01 两轮对话拍板；被动引擎基座 = `plans/plan-4.0-passive-cards-2026-08-29.md`（已实施）。

## 1. 已拍板决策（含 v2 对 v1 的推翻记录）

- **utility 新模型**：被动 utility 卡 = `takeUpSpace=true` + `isPassive`：在卡组中即生效、占卡位、可卖出（卖出即失效果）；效果数值由商店侧**从卡组重算**（禁累加式监听器）。
- **【分板出货】（v2 新）**：商店分**战斗板 / utility 板**两种类型，**每次板生成时掷类型**（首板与每次 reroll 各掷一次，进店不定型——用户否决按次访问粘滞方案）。基线概率**按 session 分阶段配置**（镜像 sessionRarityWeights 模式；默认拍板：session 1 起 10% / session 3 起 15% / session 5 起 20%，占位可调）；utility 板固定 3 槽（占位）。reroll 可把 utility 板掷成战斗板（反之亦然），属可见的赌注决策。战斗板 100% 纯净，结构性消除挤占问题；原「每板 utility ≤N 上限」规则**删除**（被分板取代）。
- **【通道卡豁免】（v2 新）**：ODDS 家族不受分板限制、任何板可出——解决自举悖论（utility 卡只在 utility 板出现则永远无人见到第一张）。卡位卡**不豁免**（走 utility 板）。
- **【基线成长回归】（v2 新，推翻 v1 的 Step 6）**：sessionRarityWeights 会话表**保留为稀有度基线**，utility 稀有度卡叠于其上。定位原则：**基线保活得下去（新手无感成长），utility = 基准上的增强**（解决上手门槛）。四轴：稀有度 = 会话表；payday = payCheck + 2×sessionNum；hpMax = hpOg + 2~3×sessionNum + ΣHP 卡；deckSize = deckSizeOg(=3) + 1×sessionNum + Σ卡位卡购买，clamp 到静态天花板。（勘误：deckSize run 起步为 valueOg=3 而非 12，旧 IncreaseDeckSize 卡本来就是 3→12 的购买进程，v2 把它正式化为「基线 + 计量条」）
- **【卡位卡 v2】（推翻 v1「限1 一次性放逐」拍板）**：改为**可无限回购的计量条**——deckSize +1/次购、自我放逐、价格递增（4 + 2×本次 run 已购次数，占位）、**达静态天花板后不再出货**；不再抬 maxDeckSize。天花板封顶总价值 → 早见到卡不再是运气差距（晚到可补购追平）。maxDeckSize 改为**静态绝对天花板**（默认 16，占位 = 12 基线 + 4 session 成长窗口）——原「maxDeckSize 全工程无写入者」bug 以此消解：不再需要任何写入者。「每类限 1」规则明确收窄为**只约束驻扎卡组的被动 utility**。
- **maxHP 常驻化**：+X 上限；进店重算 `hpMax = hpOg + 基线 + Σ`，`hp = min(hp, hpMax)`（战斗结束自动回满血已存在；「买时回满」副效果消失）。v2 数值翻倍：HP_1=+4、HP_2=+8。
- **【出货管线】**：单一 ShopPoolRef + 三阶段——**阶段 0 分板**（掷类型）→ **阶段 1 保底槽**（reserved slot：必从指定谓词池出货，reroll 槽内重摇、槽位不消失；不受分板影响）→ **阶段 2 过滤 roll**（生物潮/咒物潮，仅战斗板）→ **阶段 3 通用 roll**。权重 = 会话表(该稀有度) × shopRollWeightMultiplier × 该稀有度全部权重卡乘算之积。reroll 重跑整条管线。
- **可读性法则**：概率卡要么确定性（必出），要么整面可见摆动；不做不可验证的死概率。
- **稀有度阶梯**（sessionRarityWeights 的玩家驱动补充）：C 档=每店首板保底 1 张 U；U 档=每板保底 1 张 U；R 档=每 3 板保底 1 张 R。保底 tag 槽同节奏参数（每 value2=3 板 1 张，占位）。
- **reroll 经济**：免费 reroll 每店重置（重算得出）；【每 reroll 3 次随机一卡打折】免费 reroll **计入**计数、打折**不累计**（仅当前货架）；店内单一 reroll 计数器同驱打折与 R 保底节奏。
- **限份数**：同一被动 utility 卡（按 cardTypeID）限持 1 份，已持有者不再进出 utility 板池；卡位卡豁免（见上）。
- **商店触发表现**：utility 效果触发时播放 combat 同款 emphasize 脉冲（复用 `SetTargetScale` OutBack 0.25s 模式）；utility 板需视觉标识（section 变色/标注），否则分板不可读。
- **v1 批次**：18 张（16 新 + 2 重做），见 §3.4。
- **墓地计数**：被动照常计数；若审计出问题，再给计数卡加排除被动筛选。
- **死亡螺旋**（utility 挤占资源）：不作为设计约束（分板后已大幅缓解，剩余为玩家学习成本）。
- 经济保持跨战斗留存；利息不在 v1。

## 2. 代码事实基线（2026-08-31/09-01 实测）

- 旧 utility = `takeUpSpace=false`：BuyFunc:209 占位分支 / SellFunc:258 禁卖 / GatherPlayerDeckInfo:348 不显示 / 战斗不实例化；购买时 Instantiate 临时实例触发 onMeBought（ShopManager.cs:230-235），退出商店统一销毁。
- `DeckSizeIncreaseEffect`：deckSize += X 后 clamp [1, maxDeckSize] + SpawnAdditionalEmptySpaces；`HPMaxAlterEffect`：hpMax += X 且 hp = hpMax。maxDeckSize 全工程无写入者（仅声明 + clamp 读取），两资产均 12——v2 起改为静态天花板语义。（勘误：旧卡位卡并非全程无效——deckSize 随 ResetRun 重置为 valueOg=3、由旧卡购买爬升至 12，仅到顶后 clamp 失效）
- 被动引擎已落地（84058b8）：`CardScript.isPassive`；每次洗牌后钉在 Start Card 之下墓侧（StartCardShuffleEffect.cs:89-108），永不被揭示；埋/置顶/延后/放逐/复活选取池全排除；全局监听常驻触发；**计入墓地计数**（PassiveCardTests）。
- 经济实值：payCheck=12、C/U/R 价=4/8/12（卖=半价 2/4/6）、reroll=2、起始 purse=2；sessionRarityWeights 表已存在 early/mid/late 三档（实测 C:U ≈ 90:9 / 75:18 / 60:30，R 值未读取）——v2 保留为基线。
- `Tag` enum 已于 2026-09-01 随 Notion tag 列同步刷新（追加式 +10：Bury/Enhance/Believer/Exile/Curse/Awaken/Passive/Revive/EnhanceReaction/MultiAttack），4.0 prefab myTags 已全量打标（实测 89 prefab：Revive 24 / Bury 22 / Curse 21 / Passive 16 / Enhance 12 / Believer 13 / Awaken 8 / DeathRattle 10 / Exile 5 / MultiAttack 6 / EnhanceReaction 4）。保底 tag 槽谓词直接用 `Tag` 枚举值。**myTags 为十六进制内联 native array**（每元素 4 字节小端 int32，如 `050000000a000000`=Enhance+Passive），明文 grep 不可见，审计须 hex 解析。
- ShopUXManager 实物卡按 `_spawnedShopCards` 数量动态生成（板型切换布局无忧）；OnCardPurchased 对 takeUpSpace=false 有专门分支（:484）——卡位卡自我移除需第三分支。
- ResultStatsPanel 行由 RegisterDeckComposition 预创建，被动 utility 会出现全 0 行，需过滤。

## 3. 架构

### 3.1 卡上元数据（CardScript 新增）

- `UtilityKind` 枚举（隐式值追加）：None / HpMax / Income / ShopOption / FreeReroll / RaritySlotU / RaritySlotR / RarityWeight / RerollDiscount / **OddsUtility** / **ReservedTag** / **RerollCreatureWave** / **RerollSpellWave**。
- `utilityValue` / `utilityValue2`（int）通用参数位：收入=+X；RaritySlotR / ReservedTag = 每 value2 板 1 张；打折 = -value 元、每 value2 次；Odds = +value%。`utilityRarityWeightMults`（List<(Rarity, float)>，缺省不写=不变）——RarityWeight 类专用，U/R 可拆开独立配置（如仅 U×2），多张按稀有度乘算合并，条目形状沿用 ShopRarityWeightSO.RarityWeightEntry。
- ReservedTag 卡另带 tag 谓词字段（`Tag` 枚举）——一个 tag 一张 prefab，零新增代码。
- 卡位卡**不走 UtilityKind**：保留 onMeBought + 自我放逐 + run 级购买计数（IntSO 资产，随 run 重置），是重算纯度的唯一例外。

### 3.2 商店重算层（纯静态 + 一个例外）

- `UtilityShopBonus`（纯静态）：输入 playerDeckRef.deck + sessionNum + run 计数，输出 { payday 合成, hpMax 合成, freeRerolls, 打折参数, reservedSlots[], rarityWeightMultByRarity, boardOdds 修正, 已持有被动 utility ID 集 }。
- 基线成长公式落此：payday = payCheck + 2×sessionNum；hpMax = hpOg + 2~3×sessionNum + ΣHP 卡（进店钳 hp=min(hp,hpMax)）；deckSize = deckSizeOg(=3) + 1×sessionNum + Σ卡位卡，clamp [1, 16 静态]。
- 调用点：EnterShop（payday/hpMax）、每次 buy/sell 后、每次板生成/reroll（按需重取）。

### 3.3 出货管线

- 阶段 0 分板：每次板生成掷 p（会话阶段表，默认 session 1/3/5 → 10%/15%/20%，叠加 Σ OddsUtility 修正，封顶 100%）定类型；utility 板 = 3 槽 utility 池（含豁免的 ODDS 家族）；战斗板 = 通用槽全战斗池。ODDS 豁免 = 两板皆可出。
- 阶段 1 保底槽（追加不挤占，不受分板影响）：RaritySlotU（C 档仅首板 / U 档每板）、RaritySlotR（每 3 板）、ReservedTag（每 3 板，按 tag 谓词从全池出）。
- 阶段 2 过滤 roll：CreatureWave / SpellWave——每次板生成判定，命中则**该战斗板**通用槽全生物/全非生物（保底槽不受影响）。
- 阶段 3 通用 roll：加权 roll，每槽可带类别谓词。
- 动态排除：已持有被动 utility（按 cardTypeID）不进 utility 板池；deckSize 达天花板时卡位卡不进池。
- reroll：整条管线重跑（板类型重掷）；单一 per-visit 计数器驱动 [免费reroll 余量 → 打折判定 → R 保底/tag 保底节奏]。
- 打折结算：板生成后若 rerollCount % value2 == 0，随机一卡价格 -value（仅本板，买入按折后价，显示划线/变色）。
- 卡位卡动态价格 = 4 + 2×已购次数（占位），商店价格显示需动态化钩子。

### 3.4 v1 卡表（18 张 = 16 新 + 2 重做；数值全部占位）

| ID 提案 | kind/形态 | rarity | 效果（占位默认） | 豁免分板 |
|---|---|---|---|---|
| 卡位卡（重做） | 购入效果卡 | C | +1 槽/次购，自我放逐；价递增；达顶不出货 | 否 |
| UTILITY_HP_1（重做） | HpMax | C | hpMax +4 常驻 | —（被动） |
| UTILITY_INCOME_1 | Income | C | payday +2 | — |
| UTILITY_OPTION_1 | ShopOption | C | 通用槽 +1 | — |
| UTILITY_REROLL_1 | FreeReroll | C | 每店免费 reroll +1 | — |
| UTILITY_DISCOUNT_1 | RerollDiscount | C | 每 4 次 reroll 随机一卡 -1 | — |
| UTILITY_SLOT_U_1 | RaritySlotU | C | 每店首板保底 1 张 U | — |
| UTILITY_SLOT_U_2 | RaritySlotU | U | 每板保底 1 张 U | — |
| UTILITY_SLOT_R | RaritySlotR | R | 每 3 板保底 1 张 R | — |
| UTILITY_WEIGHT_U | RarityWeight | U | U 权重 ×2（叠会话表） | — |
| UTILITY_WEIGHT_R | RarityWeight | R | R 权重 ×2 | — |
| UTILITY_ODDS_1 | OddsUtility | C | 每店首板必为 utility 板 | **是** |
| UTILITY_ODDS_2 | OddsUtility | U | utility 板概率 +15% | **是** |
| UTILITY_TAG_REVIVE | ReservedTag(Revive) | R | 每 3 板保底 1 张复活卡 | — |
| UTILITY_TAG_CURSE | ReservedTag(Curse) | R | 每 3 板保底 1 张诅咒卡 | — |
| UTILITY_TAG_AWAKEN | ReservedTag(Awaken) | R | 每 3 板保底 1 张苏醒卡 | — |
| UTILITY_CREATURES_1 | RerollCreatureWave | U | 每 reroll 20% 战斗板全生物 | — |
| UTILITY_SPELLS_1 | RerollSpellWave | U | 每 reroll 20% 战斗板全非生物 | — |

**v1.5（延后，同 kind 零代码）**：INCOME_2(U +4) / OPTION_2(U +2) / REROLL_2(U +2) / HP_2(U +8) / DISCOUNT_2(U 每3次-1) / DISCOUNT_3(R 每3次-2) / CREATURES_2(R 40%) / SPELLS_2(R 40%)；ReservedTag 扩族按需补（Enhance/Believer/Bury）。**注意 Tag.Passive 槽永不做**——utility 卡自带 Passive tag，会把分板打穿。

## 4. 分步实施（gate 制）

- **Step 1 元数据 + 资产**：CardScript 增 UtilityKind/utilityValue/utilityValue2/utilityRarityWeightMults/ReservedTag 的 tag 字段；run 级卡位购买计数 IntSO 资产；maxDeckSize 资产改 16（静态天花板语义）。*gate*
- **Step 2 重算层 + 基线成长**：UtilityShopBonus 纯静态；payday/hpMax/deckSize 三公式；免费 reroll；打折结算；已持有去重；卡位动态价格钩子。EditMode 纯静态测试先行。*gate*
- **Step 3 出货管线**：GenerateShopItems 重构——分板阶段 + 保底槽 + 过滤器 + 权重层（叠会话表）+ 动态排除 + reroll 重跑。EditMode 覆盖：类型掷点、保底节奏、去重、20% 判定、冲突规则。*gate*
- **Step 4 现存两卡重做**：卡位卡 = 无限回购计量条（+1/次、自我放逐、递增价、达顶停售、OnCardPurchased 第三分支）；HP 卡 = +4 常驻被动化（cardTypeID 不变保统计断档）。*gate*
- **Step 5 v1 批量 + 商店 UX**：16 张新 prefab（走 A组批量构建约定）；ShopPoolRef 增补；utility 板视觉标识；稀有度分布悬浮 tips；打折价显示；免费 reroll 按钮态；emphasize 脉冲（payday 早于玩家卡实例生成的时序注意）；跑 check-shop-pool-ref。*gate*
- **Step 6 统计与审计**：ShopStatsManager 增类别/板型出货占比列；分 session 胜率盯基线成长曲线与敌方难度匹配；墓地计数联动审计（InGrave/GRAVE_*）；ResultStatsPanel 过滤被动 utility 全 0 行；pool-audit + infinity-check 跑批；敌方 recorded 卡组 / StartingDeckPool 确认无混入。*gate*
- **Step 7 文档**：AGENTS.md 增补 utility/分板/基线小节（wc -c ≤ 32KB 检查）；GameRules.md 经济节更新。

## 6. 执行状态备忘

**Step 3 完成（2026-09-02）**，EditMode 30/30 绿（UtilityShopBonusTests 11 + ShopBoardPipelineTests 19）；全量 EditMode 套件回归通过。改动仍在工作区未提交。

**09-02 用户追加拍板（已落地，管线+测试同步改，34/34 绿 + 全量 425/426 零失败）**——推翻 plan 原「保底槽从全池出」字面口径：
- **保底槽板型纯净**：候选改从**已分类的板型池**取——战斗板保底只出战斗卡，utility 板保底只出 utility 卡；去重/天花板剔除随分类内嵌，`RollReservedCandidate` 不再自带排除逻辑。
- **utility 板保底降级**：谓词（稀有度/tag）在 utility 池无匹配时**不强制**——按通用权重层从 utility 池随机出一卡（任意稀有度），依然绝不出战斗卡；战斗板保持无匹配即跳槽（其池实际不会枯竭）。
- **空池不出 utility 板**：分类后 utility 池为空（典型=被动全持有 + 卡位到顶剔卡位卡）时 board-type 掷点短路为战斗板，杜绝白板 utility 板。池内仅剩 ODDS 豁免卡时池非空，utility 板仍可出。

- **resolver 修正已落地**：`BuildRaritySlotSpec` 保底稀有度改 kind 决定（RaritySlotU→Uncommon、RaritySlotR→Rare），测试断言同步。
- **新文件**：`Managers/ShopBoardPipeline.cs`（纯静态 GenerateBoard → BoardResult{cards,isUtilityBoard}；分类/去重/天花板剔除、保底槽 fire 模型（boardIndex 从 0 计、firstBoardOnly=板 0，否则 boardIndex%every==every-1）、潮汐过滤器仅战斗板通用槽（先生物后咒物，滤后池空回退全池防空店）、板型概率 = staged<0 回退内置 10% + oddsBonus 封顶 100%、System.Random 注入）；`Editor/Tests/ShopBoardPipelineTests.cs`（23 测，确定性断言：chance 0/100、潮汐 100/0、boardIndex 节奏；含板型纯净/降级回退/空池短路四测）。
- **ShopManager 接线**：`SessionBoardChanceEntry` 列表（默认 session 1/3/5→10%/15%/20%，无匹配回传 -1 由管线兜底）、`utilityBoardSlotCount=3`（+extraShopOptions，战斗板=shopItemAmount+extra）、`_boardsGeneratedThisVisit`（ResetVisitCounters 清零、GenerateShopItems 先自增）、`CurrentBoardIsUtility` 属性（Step 5 视觉标识读）、GenerateShopItems 重构走管线（权重委托=会话表×shopRollWeightMultiplier×bonus 乘算），旧 RollWeightedCard 删除。
- ~~**按 plan 字面保留的口径**：保底槽候选=全池按谓词~~（已被上方 09-02 拍板推翻，保底槽按板型池过滤）。

**Step 4 完成（2026-09-02）**，全量 EditMode 429 总数 428 绿 0 失败（1 个既有 Ignore），新增 DeckSlotMeterTests 3 测。prefab 序列化验真通过，场景接线补齐，改动仍未提交。

- **卡位卡计量条化**：`DeckSizeIncreaseEffect` 增 `deckSlotPurchasesRef` 字段，`IncreaseDeckSizeBy` 内同额 bump 计数（clamp 在 ceiling 时计数仍前进，公式下次进店同点收敛）；`ShopManager.BuyFunc` 卡位卡分支=达顶拒购（deckSize≥maxDeckSize 直接 return，防同板第二张浪费购）+ **跳过进牌组**（自我放逐=从不入组，无卖价/占位/战斗问题）；`GetCardPrice` override=计量价 `GetDeckSlotPrice(base, step, purchases)`（显示/买价同源，ShopCardView 走此单点）。**两张卡位卡（Lite +1 C / 中 +2 U）都接了计数 ref、共用同一计量条**——中/+2 与 Lite 同价不同量存在支配问题，占位待 Step 5 调参裁决（可能摘出池）。
- **与 plan 原文的偏差**：plan §2 预告「OnCardPurchased 需第三分支」——实际**不需要**：卡位卡保持 takeUpSpace=false（购入效果卡形态），既有 takeUpSpace=false 分支（移除+销毁、不加玩家卡、不重排）天然就是自我放逐的正确视觉；空槽生成走 effect 内既有 `SpawnAdditionalEmptySpaces`。
- **HP 卡被动化**：`IncreaseHpMax.prefab`（typeID SYSTEM_INCREASE_HP_MAX 不变保统计断档）→ takeUpSpace=true + isPassive=true + kind=HpMax + utilityValue=4 + rarity U→C（按 §3.4 卡表）+ myTags+Passive；**删 onMeBought listener 与 HPMaxAlterEffect 效果子物体**（无视觉损失，renderers=0）——防买时效果与重算双算，进店/卖出时 `RefreshUtilityBonus`+`ApplyHpMaxFromDeck` 单源生效。
- **场景接线补缺**：Step 1/2 只落了 DeckSlotPurchasesRef 资产没接场景——本次发现 `ShopManager.deckSlotPurchasesRef=NULL` 并补接；`PhaseManager` 增 `deckSlotPurchasesRef` 字段并接线，`ResetRun()` 增重置（与 purse 同点，兑现「run 级计数随 run 重置」）。
- **下一步 Step 5**：v1 批量 16 张新 prefab（A组批量构建约定）+ ShopPoolRef 增补 + 商店 UX（utility 板视觉标识/打折价显示/免费 reroll 按钮态/emphasize 脉冲）+ 卡位卡余量提示（动态价已通）+ 跑 check-shop-pool-ref。

**Step 5 完成（2026-09-02）**，全量 EditMode 432 总数 431 绿 0 失败（1 既有 Ignore）；check-shop-pool-ref 通过（74 张 3.0 中唯一 missing=有意摘出的「中」卡位卡；16 个 orphan=新建 4.0 utility 卡，skill 只扫 3.0 目录）。改动仍未提交。

- **ODDS_1 管线缺口补齐**：管线原只支持概率修正，卡表 ODDS_1 语义是「每店首板必为 utility 板」——`UtilityShopBonus.Bonus` 增 `firstBoardUtilityForce`（Compute：OddsUtility 卡 `utilityValue2>0` → force 首板，`=0` → 概率修正），管线 boardIndex==0 时短路为 utility（空池 fallthrough 仍优先）。新测 3 个（解析/首板 force/空池不被 force）。
- **16 张新 prefab**：以被动化的 IncreaseHpMax 为模板批量构建（根 CardScript、无效果链、takeUpSpace=true+isPassive=true+myTags=[Passive]），落 `Cards/4.0/{0_Common,1_Uncommon,2_Rare}/`；displayName/desc 为**占位中文**（世界观圈改走 Notion 那条线）；desc 按 desc-notation 规范（【复活】【诅咒】【苏醒】=tag 群体）。ODDS_1=v100/v2=1（force 形态）、ODDS_2=v15/v2=0。
- **ShopPoolRef**：74→89（+16 新卡、-1 摘出 SYSTEM_INCREASE_DECK_SIZE「中」卡位卡——**共享计量条下 +2 与 +1 同价支配，默认摘出池**，prefab 保留可随时回池；此为 Step 5 调参默认裁决，待用户追认）。
- **重做卡 desc**：HP 卡=「生命值上限 +4（常驻:在卡组即生效，卖出即失效）」；Lite 卡位卡改名「卡位扩张」+ 计量条规则 desc。
- **商店 UX**：①奇物架标识=UpdateShopItemInfo 头部插「◆ 奇物架」行（文本面板，零场景接线；场景变色留 polish）②打折价=ShopCardView.UpdatePriceDisplay 划线原价+绿色折后价，数据源新 ShopManager.GetBoardDiscount ③免费 reroll 按钮态=UpdateRerollButtonLabel（「Reroll: 免费 xN / $X」），EnterShop+Reroll 后刷新；playerStatsDisplay 加 Free Rerolls 行 ④买入 utility 被动时 PulsePlayerCard emphasize 脉冲（1.2x OutBack 0.12s+回弹；payday 时序=进店不脉冲，实体尚未生成）⑤shopInfo 头部加当前稀有度权重表行（tips 轻量版）。
- **下一步 Step 6**：统计与审计（ShopStatsManager 板型/类别占比列、分 session 胜率盯基线、墓地计数联动审计、ResultStatsPanel 过滤被动全 0 行、pool-audit+infinity-check 跑批、敌方 recorded 卡组确认无混入）。

**Step 6 完成（2026-09-02）**，全量 EditMode 432 总数 431 绿 0 失败（1 既有 Ignore）。改动仍未提交。

- **ShopStatsManager 板型占比**：CardShopStats 增 `utilityBoardAppearCount`/`utilityBoardBoughtCount`（旧 JSON 缺字段反序列化为 0，兼容）；RecordCardAppeared/RecordCardBought 加 `onUtilityBoard` 默认参数，ShopManager 两处调用传 `_currentBoardIsUtility`；报表加「Utility board share: P1 offers (n/m), k bought」汇总行，CSV 加两列。顺带把该文件从 4 空格缩进转换为 CRLF+Tab 合规。
- **ResultStatsPanel 过滤**：CardScript 增 `IsUtilityPassive`（isPassive && kind!=None）；CombatPerCardStatsTracker 的 `RegisterDeckComposition`（跳行预建+副本计数）与 `EnsureRecord`（唯一排除点）两处排除——被动 utility 不再出现在结算面板。
- **墓地计数审计**（结论）：①CheckCost_InGrave 已废弃恒成功，无联动；②ValueTrackerManager ownerInGrave/enemyInGrave 计数器**被动计入但当前无战斗读者**（遗留 tracker，Passive_CountsTowardGraveCount 锁定保持），未来接卡时再裁决；③**唯一真实供数点=CardScript.CountGraveyardCardsOf**（RELIC_GRAVE_CURSE override 基数）——玩家每持 1 张被动=敌方诅咒卡 +1 攻，单向不可读，**已修**（排除 IsUtilityPassive，注释注明 deck 人口轴仍计入）；④GRAVE_LORD 光环受体限 IsCreature，无联动；⑤ownerCardCountInDeck 牌库人口轴被动照常计入=「占卡位即人口」设计口径，保留。
- **敌方 recorded 卡组清洗**：扫描 156 个 DeckSO——16 张新 UTILITY_* 零混入、StartingDeckPool 干净；但旧「中」卡（~80 处）与旧 HP 卡（~30 处，重做后已是被动）混在 Session0-7 敌方录制卡组。按 plan「不得混入」执行清洗：45 个卡组移除 123 条死引用，残余 0（录制卡组=商店随机模拟可再生数据，git 可回滚）。
- **pool-audit/infinity-check（增量口径）**：16 张新卡无战斗效果链（构建时确认根上仅 CardScript）→ 单/多卡战斗循环不可能；商店层经济循环受 own-once（每 kind 限 1）+ 卡位天花板+递增价钳制，无自增殖；kind 互不重复、与 4.0 战斗轴零重叠、Tag.Passive 槽未做。全量 Notion pool-audit 跑批如需另行开任务。
- **分 session 胜率基线观察（未执行，需跑局）**：统计管线已就位（shop_stats.json 板型占比 + card_winrate.json）；基线成长曲线 vs 敌方难度匹配需 Play Mode 跑局产数，按 AGENTS.md 等用户明示授权后跑（或手动 playtest 观察 session 1/3/5/7 的胜率与 purse/hp 曲线）。
- **下一步 Step 7**：文档——AGENTS.md 增 utility/分板/基线小节（wc -c ≤32KB 检查）；GameRules.md 经济节更新。

**Step 7 完成（2026-09-02）——方案七步全部完成。**

- **AGENTS.md**：压缩外移 Result Screen / Duplicate Slot Rule 两小节细节（对应 plan 文档均已承载），新增「Shop Utility Passives & Board Pipeline」小节（元数据/重算层+卡位例外/管线三段/统计口径，引用 plan §6）；wc -c = 31,531，headroom 1,237B ≥ 1KB，CRLF ✓。
- **GameRules.md**：新增「Shop Economy (v2 Utility Pipeline)」节（Money Flow / Board Types / Guaranteed Slots / Utility Passives / Waves，占位数值标注）+ TOC 同步；CRLF ✓。
- **整体遗留（非 Step 7 范围）**：①16 张新卡 displayName/desc 中文占位待世界观圈改（Notion 流程）；②摘「中」卡位卡出池待用户追认；③分 session 胜率基线观察需 Play Mode 跑局待授权；④全部改动未提交，建议分批 commit（Step1-2 基建 / Step3-5 管线+批量 / Step6 审计+清洗 / Step7 文档）。

**历史（2026-09-01 断连交接，已消化）**：Step 1（UtilityKind/CardScript 五字段/DeckSlotPurchasesRef/天花板 16）、Step 2（UtilityShopBonus 重算 + ShopManager 六处接线 + 测试 11 绿）当时未提交；本会话接续时仍在工作区，Step 3 依 §6 草图完成后一并保持未提交。

## 5. 风险与开放项

- **基线成长 vs 敌方难度匹配**：payday/hpMax/deckSize 三线成长后 run 后期玩家强度上移，敌方 recorded 卡组多样性是否提供对等挑战——Step 6 分 session 胜率验证，数值可调。
- **utility 板存在感**：p=10% 时 4 板口径约 34% 的访问至少见到一次 utility 板（1-0.9⁴）；是否偏低由 playtest 定，p 与 ODDS 数值都是常量/prefab 参数。
- **重算纯度例外**：卡位卡的 run 级计数与动态价格是唯一非重算状态，实现时文档化（reset 点 = run 重置，与 purseRef.ResetToDefault 同点）。
- **墓地计数**：被动 utility 放大 InGrave/GRAVE_* 联动，Step 6 审计后裁决是否加排除筛选。
- **录入面**：敌方 recorded 卡组、StartingDeckPool 不得混入 utility 卡。
- **数值占位声明**：§3.2/§3.4 全部数字为占位默认，Step 5 批量时统一调参定稿。
