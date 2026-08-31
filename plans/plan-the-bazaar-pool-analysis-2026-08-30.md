# 分析计划:The Bazaar 卡池组成分析(对照 StS2 系列)

## 0. 目标

对 Tempo 的《The Bazaar》卡池(物品池)做与 2026-08-17 StS2 五角色系列同级的组成分析:

- 先逐份产出**每英雄 / 每池的拆解分析**(6 英雄物品池 + 怪物/公共物品池 + 技能池),最后一份**综合设计总结 → OneDeck 落点建议**。
- 该系列此前喂出了 `docs/4.0_Rarity_Iteration_StS2_2026-08-28.md`(稀有度迭代),本期产出将作为 OneDeck 后续卡池设计的第二个外部参照系。
- 执行方式:**一次只产出一份分析文档,完成后停下汇报,等用户确认再继续下一份**(遵循 roadmap step-gate 协议;用户在计划确认后按此逐个触发)。

## 1. 数据源(已验证可用)

### 1.0 数据源总览(2026-08-31 已升级为双源)

| 源 | 覆盖 | 字段质量 | 用途 |
|----|------|----------|------|
| **Mobalytics**(`mobalytics.gg` persistedQuery `TheBazaarStaticDataQuery`) | **全 9 英雄 1207 物品 + 522 技能 + 9 英雄元数据**(含 Karnok 118 / The Dragons 107 / Common 166) | tierStats 四级数值、enchantments 附魔、字段化 crit/ammo/multicast/lifesteal | **主数据源**(可做全量组成统计 + 附魔统计) |
| thebazaar.wiki.gg(Cargo API) | 6 英雄 636 物品 + 312 技能 | `type/size/starting tier/collection` 标签化、`effects` 免费文本 | 交叉验证 + `type` 标签历史口径对照 |

- Mobalytics 抓取方式:持久化 GraphQL(query hash `60a432cd2fcc262f84828ce78f1b1e6672b5ea3a3567ea8950dd78f0d8bd10b1`),需浏览器态标头 `x-moba-client: mobalytics-web` + `x-moba-proxy-gql-ops-name` + `apollo-require-preflight`,curl 直连会 403;用 Playwright 浏览器 fetch 成功。
- Mobalytics 版本标:`cloudflareCacheVersion v1.0.59`(快照 2026-08-31)。
- 原始快照:`tools/outputs/bazaar/mobalytics_static_2026-08-31.json`(1207 items + 522 skills,规范化后);wiki 快照:`bz_items.json / bz_items2.json / bz_skills.json`。

### 1.1 样本边界(重要,2026-08-31 更新)

**样本 = Mobalytics 全量 1207 件物品**,英雄归属按 `heroes[]` 字段:

- 9 英雄物品池:Vanessa 139 / Pygmalien 154 / Dooley 144 / Mak 141 / Stelle 122 / Jules 121 / **Karnok 118** / **The Dragons 107** / Common 166(含怪物/公共/Junk/Loot 等)。
- 注意与 wiki Cargo 口径差异:wiki 只记 6 英雄(Stelle 28 / Jules 13 严重滞后于 Mobalytics 的 122/121——wiki 未补全新英雄);**本系列以 Mobalytics 为准**,wiki 仅作交叉验证。
- **附魔(Enchantments)**:Mobalytics 每件物品带 `enchantments[]`(名称+描述),**可全量统计**——覆盖 wiki 缺口,综合文档的附魔小节从定性升级为定量。
- Karnok / The Dragons:数据完整,纳入正常分析(执行顺序表更新,见 §3)。

### 1.2 数据已知脏值(执行时统一清洗)

| 字段 | 脏值 | 清洗 |
|------|------|------|
| starting tier | `Bronzw` | → Bronze |
| starting tier | `<span style="color:#E16D0D">Legendary</span>` / `<span style="color:#AFB1C3">Silver</span>` | → Legendary / Silver |
| starting tier | `Unknown`、空串(9 行) | → 记为 `Unknown`,列入文档「未知」分组,不猜 |
| starting tier | `Dimaond`(skills 表 1 行) | → Diamond |
| collection | 多值逗号分隔(如 `"Junk, Monster"` / `"Monster, Treat"`) | 按 `,` split 后 trim;归属判定规则见 §2.3 |
| collection | skills 表组合极多(6 英雄全通用等) | 按「英雄子集」计数;单英雄专属 vs 多英雄共享分类 |
| size | 无脏值(Small 284 / Medium 260 / Large 92) | — |
| type | 为列表字段;技能表里混入非许可值(详见 §2.4 映射) | 见 §2.4 |

### 1.3 版本风险

wiki 无补丁/赛季标注字段,数据可能滞后于当前补丁(2026-08 实时为 15.x 赛季环境)。每份文档副标题标注「wiki 快照 2026-08-30,未标注补丁」,结论定性表述(「当前 wiki 记录」),不做「游戏实际版本」断言。

## 2. Bazaar 与 StS2 的系统差异(分析口径的前提)

Bazaar 没有 C/U/R 稀有度、没有费用/能量、没有回合制手牌——它的对应物是:

| StS2 维度 | Bazaar 对应物 | 分析中怎么处理 |
|-----------|--------------|----------------|
| 稀有度(C/U/R) | **starting tier**(Bronze/Silver/Gold/Diamond/Legendary)+ **升级体系**(每物品 3 级,cost 12/24/48) | tier 分布 = 「初始强度分布」;升级体系 = 「纵向投资阶梯」,单独章节分析 |
| 卡牌类型(攻/技/能) | **type 标签**(Weapon/Tool/Property/Aquatic/Friend/Vehicle/Core/Food + 物品自身机制词) | type 分布 + 「被动/主动」二分(是否依赖 cooldown/ammo) |
| 费用(能量曲线) | **cooldown(秒)+ ammo(次数)** | 冷却分布 = 节奏曲线,代替费用曲线 |
| 卡位成本 | **size**(Small/Medium/Large)→ 6 格背包 | size 分布 = 空间经济学 |
| 卡牌效果文本 | `effects` 文本 + 机制词(via type 列表杂糅) | 文本按「when 句式」与「机制词表」正则提取 |
| 单角色卡池 | 单英雄物品池(collection 字段) | 每英雄一份文档 |

### 2.1 机制词表(从 type 字段提取,跨文件统一)

参考词(按出现频次归一,`*Reference` 视为同基词):

`Damage / Shield / Heal / Regen / Burn / Poison / Freeze / Slow / Crit / Haste / Cooldown / Charge / Ammo / Lifesteal / Health / Weapon / Tool / Property / Aquatic / Friend / Vehicle / Core / Food / Potion / Toy / NonWeapon / Flying / Value / Gold / Income / Economy`

文档中统一使用「基词 + Reference」合并统计(如 BurnReference 并入 Burn)。

### 2.2 条件句式(效果文本的语法层)

`effects` 文本以「When you ... (X)」为主的触发句式。拆解时按语义聚类:

- 自触发(Use X 本身)
- 事件源触发(when X happens / when you have / on use of other item)
- 全局循环(每 tick / 每战斗开始)
- 条件性 state(若拥有某属性则更强)

### 2.3 归属判定规则

- 单英雄专属:collection = 唯一英雄。
- 怪物/公共:collection 含 `Monster`/`Junk`/`Loot`/`Treat`/`Common` 或为空 → 归「怪物/公共池」,不并入各英雄池(防止多计数)。
- 英雄内多标签(如 Weapon+Aquatic)不拆分子计数,计入所属池并标注多标签。

### 2.4 type 字段映射说明

`items.type` 在 Cargo 中混入了机制词(DamageReference/Shield 等)与真实物品类型(Weapon/Tool/Property…)。清洗规则:类型标签仅取「物品类型」白名单(Weapon/Tool/Property/Aquatic/Friend/Vehicle/Core/Food/Merchant/Unsellable/NonWeapon/Apparel/Toy/Potion),机制词全部进 §2.1 机制词表。

## 3. 交付清单与执行顺序(2026-08-31 更新,按 Mobalytics 全量重排)

| # | 文档 | 样本 | 文件 |
|---|------|------|------|
| 1 | Vanessa | 139 | `docs/Bazaar_Vanessa_PoolAnalysis_2026-08-30.html`(已产出,2026-08-30 wiki 口径撰写;后续按需复核) |
| 2 | Pygmalien | 154 | `docs/Bazaar_Pygmalien_PoolAnalysis_2026-08-31.html` |
| 3 | Dooley | 144 | `docs/Bazaar_Dooley_PoolAnalysis_2026-08-31.html` |
| 4 | Mak | 141 | `docs/Bazaar_Mak_PoolAnalysis_2026-08-31.html` |
| 5 | Karnok | 118 | `docs/Bazaar_Karnok_PoolAnalysis_2026-08-31.html` |
| 6 | Jules | 121 | `docs/Bazaar_Jules_PoolAnalysis_2026-08-31.html` |
| 7 | Stelle | 122 | `docs/Bazaar_Stelle_PoolAnalysis_2026-08-31.html` |
| 8 | The Dragons | 107 | `docs/Bazaar_TheDragons_PoolAnalysis_2026-08-31.html` |
| 9 | 公共池(Common/Monster) | 166 | `docs/Bazaar_CommonPool_PoolAnalysis_2026-08-31.html` |
| 10 | 技能池 | 522 | `docs/Bazaar_Skills_PoolAnalysis_2026-08-31.html` |
| 11 | 综合总结 → OneDeck 落点建议 | 汇总 | `docs/Bazaar_DesignSynthesis_ForOneDeck_2026-08-31.html` |

执行顺序按池大小先大后小(Pygmalien → Dooley → Mak → Karnok → Jules → Stelle → The Dragons → 公共 → 技能 → 综合),每份完成即停。样本数字以 Mobalytics `heroes[]` 归属为准(全池 1207,与 wiki Cargo 口径不同——wiki 只记 6 英雄且新英雄滞后)。

## 4. 每份分析文档的统一模板(与 StS2 单角色系列对齐,按 Bazaar 改写)

> 每份文档顶格放同款 KPI 数字卡(title + 数据源 + 副标题),以下为固定章节结构。

#### 0. 英雄骨架

- 英雄定位(背景/身份信息,简述即可)
- 起始物品(起始机制教育单元——Bazaar 每个英雄的起始物品演示其核心机制)
- 身份关键词(该英雄的机制词表 Top 5)

#### 1. 池组成总览(KPI)

- 池大小、tier 分布(B/S/G/D/L + Unknown)、size 分布、type 分布、主动/被动占比(按 cooldown/ammo 字段判定)、多标签占比。
- 与全池对照的小结(该英雄 X 占比高于/低于全局前 N)。

#### 2. 术语与分级结构

- 触发句式分布(§2.2 聚类)。
- cooldown 分布直方(0 秒=被动,1-4s 快节奏,5-10s 中,10s+ 慢/大件)。
- ammo 类物品(爆炸物/枪械为一次性,弹药=约束资源)统计。
- tier 与成本(12/24/48 升级线)的交织:高 tier 是否对应高 cost。

#### 3. 构筑轴识别

- 按机制词共现 + effects 文本聚类出该英雄 3-5 条主轴(每轴:入口卡 → 兑现卡 → 封顶件)。
- 轴间桥(跨轴物资)标注。
- 弱轴/孤卡(无轴可挂)点名。

#### 3.6 轴间桥矩阵(2026-08-31 新增,对齐 OneDeck v4 卡池分析 §5「构筑之间的桥梁」)

> 桥 = 一件物品同时属于 ≥2 条轴。轴的定义是**可计算谓词**(tags 白名单 + effects 关键词 regex),全部从 Mobalytics 数据自动算出,保证每份文档口径一致、可复现。

- **轴定义表**:该英雄 4-7 条轴,每轴给出标签/关键词谓词与覆盖件数(如 Vanessa:输出 / 弹药 / 水生 / 暴击 / 慢控 / 经济 / 车辆)。
- **链接比例 KPI**:单轴件数 / 双轴桥数 / 三轴+件数 / 无轴件数 及各自占比(如 Vanessa 60 单轴 / 51 双轴桥 / 14 三轴 / 11 无轴)。
- **桥矩阵**:轴×轴对称矩阵,格子 = 两轴交集物品数(只显示 >0;空位即 0)。标注最密桥(如 Vanessa 慢控×水生 18)。
- **桥的形式分类**(对桥物品逐一归类):
  - **标签桥** = 物品 type tags 本身跨轴(如 Weapon+Friend);
  - **文本桥** = 效果文本引用另一轴的机制词(如「When you use a Core or a Ray」);
  - **双重桥** = 两者皆有。
- **桥的 tier 分布**:桥集中落在哪个 tier(对照 StS2「桥多在 Uncommon」的结论,Bazaar 预期 Silver)。
- **空位审计**:桥数 = 0 的轴对,点名缺口(如 Vanessa 弹药×暴击空位)——对应 OneDeck 的「诅咒×沉睡空位」审计。
- **闭环示例**:挑 1 条代表构筑链,展示桥如何把多轴串成一个循环(对应 OneDeck 的「五轴接通」示例)。

#### 4. 两段式审计

- 条件制造/兑现二分:哪些物品是「入口」(造条件),哪些是「兑现」(消费条件),哪些既是。
- 与 StS2 结论对照:Bazaar 的入口层在哪些 tier(预期 Silver 为主,类似 StS2 Uncommon=桥层)。

#### 5. 强度阶梯

- Diamond/Legendary 封顶件清单;验证「高 tier ≠ 必强」误判(社区共识强度 vs 资料卡强度)。
- 升级体系对该英雄的特殊打法(特定核心件是否值得 3 级投资)。

#### 6. 与 StS2 / OneDeck 的映射

- 该英雄池与 StS2 五角色池在结构上的异同点(一两段话)。
- OneDeck 落点初筛(仅作标注,不展开;正式建议全在综合文档)。

#### 7. 关键卡清单(表格)

- 每轴选 3-5 张代表卡,列 name / tier / size / type / cooldown / effects 摘要。

#### 8. 文档元信息

- 数据快照日期、文件、清洗说明、已知缺口(如 hero 页面未完成)。

## 5. 综合文档模板(`Bazaar_DesignSynthesis_ForOneDeck`)

0. 核心结论(TL;DR,对齐 StS2 综合文档风格:P0/P1/P2 建议)。
1. Bazaar 卡池设计语法提炼(组成结构 → 设计规律,证据必带计数)。
2. 与 OneDeck 的系统映射:可借鉴 / 不可借鉴(两者语法差异:6 格空间经济、冷却节奏、tier 升级线、PvP 秒表、垃圾/怪物池——逐一判定)。
3. 附魔定性小节(种类、覆盖面,标注无全量数据)。
4. Karnok/The Dragons 缺口说明。

## 6. 执行检查清单(每份文档)

- [ ] 数据快照日期、来源 URL 写入副标题
- [ ] 题材计数与原始快照核对一致(脚本统计,可复现)
- [ ] 脏值清洗规则落实(§1.2)
- [ ] CRLF + Tab 缩进规范(HTML 文件遵守 AGENTS.md 格式约定)
- [ ] KPI 数字卡有「全部 / 单英雄」对照
- [ ] 每份结束停下汇报(step-gate),等确认再执行下一份

## 7. 范围外(本次不做)

- Karnok / The Dragons 物品池(无数据,若用户提供数据源可扩)。
- 附魔全量统计(无结构化数据)。
- Events 表(战斗事件)分析与技能表独立成文档后,若综合需要再引用。
- 社区强度天梯(Unduel/Mobalytics tier list)只作为综合文档定性引用,不做全量爬取。

## 8. 待确认项(2026-08-30 已拍板)

1. 计划按此执行 ✅。
2. 执行顺序:**池大→池小**(Pygmalien → Dooley → Mak → Karnok → Jules → Stelle → The Dragons → 怪物 → 技能 → 综合)✅。
3. 每份拆解文档**都包含**「与 StS2/OneDeck 映射」章节,综合文档收敛为正式建议 ✅。
4. 计划拍板后直接开始第一份(Vanessa),每份完成停下汇报 ✅。

## 9. 框架迭代决策(2026-08-31 追加)

- 桥量化:**谓词自动计算**(每英雄手写 4-7 条轴谓词,桥 = 物品同时属 ≥2 轴,全部从 Mobalytics 数据算出)✅。
- 已完成的 Vanessa / Pygmalien / Dooley 三份:**全部补上桥矩阵章节(§3.6)重新生成** ✅。
- 闭环示例(代表构筑链):**每份都带** ✅。
