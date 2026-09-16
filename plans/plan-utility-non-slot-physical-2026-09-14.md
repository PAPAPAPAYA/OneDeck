# utility 被动「不占卡位但保留实体」实施计划（takeUpSpace 两轴拆分）

日期：2026-09-14
状态：**已实施（2026-09-14）**。代码双轴拆分 + 8 消费点迁移 + 20 张 prefab 批改 + 3 文档 + tools 双键 + Notion 18 行 desc 回写全部完成；双程序集离线编译 0 错误。EditMode 测试已写入但**未跑**（需 Unity Test Runner，本会话无 Unity MCP）。
上游：2026-09-14 对话两轮拍板。第一轮方案（直接复用 `takeUpSpace=false`）被推翻——牌库条必须仍显示已购 utility、且仍可出售。

## 1. 拍板记录（2026-09-14，全部已定）

1. **战斗牌库继续包含 utility（方案A）**：现状本来就在战斗序列里（死牌稀释），保留；`GatherDecks` 仅改名零行为变化。GameRules「never reveals in combat」确认为文档漂移，已修正。
2. **desc 措辞（方案B）**：行内追加「, 不占用卡位」（半角逗号+空格，同「放逐自身」句式）。
3. **字段命名（方案A）**：`takeUpSpace` → `physicalDeckCard`（`FormerlySerializedAs` 保数据）+ 新增 `occupiesDeckSlot`（默认 true）。
4. **Notion 4.0 DB**：18 行 desc 已回写（含按 prefab 全量对齐，顺带修复 3 行漂移）。
5. **牌库条溢出展示（方案A）**：与普通卡同货架流换行，零额外工作。

## 2. 设计：单 bool 拆两正交轴

| 轴 | 字段 | 默认 | 语义 | 管什么 |
|----|------|------|------|--------|
| 占卡位 | `occupiesDeckSlot`（新） | true | 购入是否消耗 deckSize 容量 | BuyFunc :303 上限检查、CountSlotOccupyingCards、CombatStartCardGiver :78 |
| 实体卡 | `physicalDeckCard`（原 takeUpSpace） | true | 是否作为实体卡存在 | SellFunc 禁售门、GatherPlayerDeckInfo、GatherDecks、ShopUXManager 牌库条实例化/买后即毁 |

- 沉魇（IncreaseHpMax）/ 长夜（IncreaseDeckSizeLite）显式补 `occupiesDeckSlot=0`：新字段默认 true 会让长夜购入计入上限（回归），必须显式关。
- 18 张 UTILITY_*：`occupiesDeckSlot=0`，`physicalDeckCard` 保持 1（YAML 键名不变，`FormerlySerializedAs` 兜底）。
- 其余 100+ 张卡未触碰：新字段走默认 true。

## 3. 实施记录

1. **CardScript.cs**：`using UnityEngine.Serialization` + 字段拆分（:28-34 附近）。8 个消费点全部迁移：ShopManager :303/:311/:369/:469（:303 为唯一门禁切换点）、CombatManager :306/:316（仅改名）、ShopUXManager :517/:714（仅改名）、CombatStartCardGiver :78（随函数改名自动切到占位口径）。
2. **函数改名**：`UtilityFuncManagerScript.CountCardsTakingUpSpace` → `CountSlotOccupyingCards`，内部判据 `takeUpSpace` → `occupiesDeckSlot`。全局 grep 零残留（仅 CardScript 的 `FormerlySerializedAs("takeUpSpace")` 字符串保留）。
3. **prefab 批改 20 张**（YAML 直改，Unity 同款转义 `\uXXXX`/`\xXX`；幂等脚本已删）：18 张 utility 加 `occupiesDeckSlot: 0` + desc 追加；沉魇/长夜只加字段。批后全量扫描验证 20/20 ok（附录A）。**注意**：批改中途并发会话落了新卡 UTILITY_OPTION_P_1（魇市游摊，kind=13 ShopOptionChance，untracked），按「除两张外全部」指令一并纳入。
4. **文档**：GameRules.md :332 重写（占位口径 + 漂移修正）；ShopSystems.md :13 尾段改写；AGENTS.md Shop Systems 小节一句 + `wc -c` = 18274B（余量充足）。
5. **tools 双键**：extract_card_prefabs.py（FIELD_PAT）、extract_card_data.py（wanted set）、analyze_mechanics.py（机制 pattern）、one_deck_damage_sim.py（take_up_space 双键回退 + 2 处文案 + 注释）。**analyze_mechanics.py 存在先行语法损坏**（HEAD 即有的乱码字符串 :58 等），与本次补丁无关，未扩大修复。
6. **Notion 18 行**：4.0 card database「card desc」逐行 update-page 并 fetch 抽验落库；按 prefab 全量对齐顺带修 3 行漂移（UTILITY_WEIGHT_R 缺「被动:」前缀、UTILITY_WEIGHT_U 全角冒号、ODDS_2 等缺 `<b>`）。
7. **编译**：`dotnet build` Assembly-CSharp + Assembly-CSharp-Editor 双双 0 错误（仅先行警告）。

## 4. 测试状态

- `DuplicateSlotCountTests`：helper 参数 `takeUpSpace` → `occupiesDeckSlot`；用例改名 `SlotFree_ExcludedInBothModes`；**新增** `UtilityPassive_PhysicalButSlotFree_NotCounted`（占位豁免+实体保留的计数断言）。其余用例纯改名。
- **未跑**：EditMode 全量需 Unity Test Runner（run_tests），本会话无 Unity MCP。下次开 Unity 后先跑一遍（预期基线 504+ 全绿 + 新用例）。
- Play 观感待验：牌库条显示已购 utility、买后 pulse 动画恢复、溢出换行。

## 5. 行为变化对照（已生效）

| 场景 | utility ×18 | 沉魇/长夜 | 普通卡 |
|------|-------------|-----------|--------|
| 购入上限计数 | 不计（变化） | 不计（不变） | 计（不变） |
| 牌库条显示 | 显示（变化） | 隐藏（不变） | 显示（不变） |
| 出售 | 可（不变） | 不可（不变） | 可（不变） |
| 战斗牌库 | 进（方案A，不变） | 不进（不变） | 进（不变） |
| 被动加成 | 照常（不变） | — | — |

## 6. 平衡观察项

- 占位代价归零 = 全 utility 变强（混池后获取率已高）；盯 shop_stats 持有率与经济通胀（Income/Reroll 叠买）。
- 战斗稀释仍在（方案A 保留的隐性代价）；观察是否被感知为负体验。
- 16 上限吃满时 utility 仍可出货可买（上限检查跳过）。

## 7. UI 规范跟进（2026-09-15）

- `docs/demo/UIKitDemo.html` §08 商店页 v1.1 新增**「商店升级」面板**：位于卡组板块下方，样式与卡组面板一致（半透明深色浮层 + 卡排 + 价格按钮），展示**已拥有的 utility 卡**。规范要点：utility 不占卡位 → 头部只有标题、无卡位计数、不做空卡位凹槽；卡面无攻击力（底行仅卡名）；交互与卡组面板相同（点按预览、价格按钮售出，hover `$N → 售出`，售价 = 半价）。
- 与本计划 §1.5「与普通卡同货架流换行」的关系：该面板是已购 utility **独立分区展示**的 UI 规范稿（混排牌库条之外的界面方向）；Unity 侧 `ShopUXManager` 牌库条**未改动**，实装与否待拍板。若实装，显示/售出逻辑可复用现有 `physicalDeckCard` 管线，仅改牌库条分区呈现。
- demo 中的卡名/效果（日进斗金/幸运币/百宝囊）为占位示例，实装时替换为真实 utility 卡。

## 附录A：prefab 批改清单（20 张，全部 `occupiesDeckSlot: 0`）

18 张 utility（desc 均追加「, 不占用卡位」）：UTILITY_DISCOUNT_1 / INCOME_1 / ODDS_1 / ODDS_2 / OPTION_1（1_Uncommon）/ OPTION_P_1 / REROLL_1 / SLOT_U_1 / SLOT_U_2 / SLOT_R / WEIGHT_U / WEIGHT_R / CREATURES_1 / HALFPRICE_1 / SPELLS_1 / TAG_AWAKEN / TAG_CURSE / TAG_REVIVE。
2 张 ghost（仅字段）：IncreaseHpMax（沉魇）/ IncreaseDeckSizeLite（长夜）。
