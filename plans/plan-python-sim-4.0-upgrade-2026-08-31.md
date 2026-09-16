# Python Sim 升级到 4.0 口径 — 实施计划

日期:2026-08-31
状态:执行中(按 step-gate 协议,每步完成后停下汇报,等确认再继续)
试样范围调整(2026-09-08/09):Common 试样 Step 1-6 全通后已扩 U 层(2026-09-09,78 张卡表,对数全绿,U handler 40 张+RELIC U 被动 6 张,五段自测+四配置报告重跑)。池子较计划写作时扩容,数量以实测为准;R 层已补齐(2026-09-16,全池 112 张对数绿、89 handler、血量档改 HP25/HP50 双档去无上限)。下一步=逐场数据导出+两卡组合枚举+特征回归归因(带重尾截尾/批次协变量/已知近似混淆清单三项防护)
目标文件:`tools/scripts/one_deck_damage_sim.py`(现有 3.0 口径,1590 行)

## 1. 背景与目标

现有 `one_deck_damage_sim.py` 是 3.0 卡池的蒙特卡洛模拟器:随机组牌、输出每卡每轮期望伤害与胜率。但它读 `Assets/Prefabs/Cards/3.0 no cost (current)/`,只建模 3.0 机制;4.0 的 87 张卡(C15/U45/R27,见 `docs/4.0_Rarity_Iteration_StS2_2026-08-28.md`)的复活/苏醒/信徒/被动等轴完全不支持。

目标:将模拟器升级为 4.0 口径,使其能对 4.0 卡池产出可信的数值强度数据(每卡期望伤害、出场贡献、胜率),作为后续"两卡组合枚举找异常"和"特征回归归因"的数据底座。

**非目标**:不做 Unity headless 批量模拟路线;不做商店环境建模;不做两卡组合枚举与 ML 归因(后续独立计划)。评估环境升级(轴条件化抽样 + 卡组种群共演化)在本计划内,见 Step 6 Layer A / Step 7 Layer B。

## 2. 现状盘点

现有模拟器已具备(可复用):

- GameState 骨架:双方卡组、起始卡位置、揭晓循环、回合重置、warmup 统计
- 基础动作:`bury_card` / `stage_card` / `exile_card` / `add_card` / `give_power` / `damage_enemy`
- 事件骨架:`on_me_buried` / `on_friendly_exiled` / `on_enemy_curse_revealed` / `after_shuffle` 等
- 诅咒(=4.0 信徒前身 RIFT)已有 own/enemy curse 的 enhance/consume 模型
- 3.0 遗言集合 `DEATHRATTLE_CIDS`、Linger 门控 `LINGER_CIDS`

主要缺口(对照 `docs/4.0_Glossary.md`):

| 缺口 | 说明 |
|------|------|
| Power → 攻击力 | 3.0 的 Power 概念需迁移为 4.0 ATK(含 攻击力=X 计算型、翻倍、多段 攻击xN) |
| 复活/苏醒 | 墓地(index < startCardIndex)拉回卡组顶;苏醒触发;延迟复活变体 |
| 信徒 token | RIFT token 的生成/揭晓/消耗;`攻击力=本回合放逐的信徒数量` 型计数器 |
| 被动卡 | 19 张 RELIC_:洗牌后固定置于起始卡后、永不被揭晓、每事件触发 |
| 攻击事件 | 友方攻击时/有卡攻击时(RELIC_HIVE / RELIC_ATTACK_HEX) |
| 新触发 | 被强化、友方被强化、友方被埋葬、有卡被埋葬、回合开始/结束、苏醒、友方苏醒、信徒揭晓 |
| 新动词 | 强化N(随机目标)、置顶、延后、生成N信徒、复制自身、攻击次数+N、回响X、触发X的遗言、交换、让墓地友方攻击 |
| 4.0 数据层 | prefab 读取路径指向 3.0 目录;需改读 `Cards/4.0/{0_Common,1_Uncommon,2_Rare}` |

## 3. 语义基准(单一事实源)

- 机制定义:`docs/4.0_Glossary.md`(2026-08-28 版,含所有 Q&A 裁定)
- 近期裁定落在 git 提交:MASS_SACRIFICE 无延迟、BLOOD_PACT 攻击转化为强化、RIFT_OVERRIDE 信徒效果改写、GRAVE_PUPPETEER 墓地打击、GRAVE_ROBBER 快照、DECIMATION 总配额等(18252b2 及此前若干提交)
- 模拟器建模的是**设计语义**(含引擎尚未实现的部分,如苏醒/被动),不是当前 Unity 实现快照——这正是做这层模拟的价值

## 4. 实施步骤

### Step 1 — 数据层:4.0 卡表加载

- `_load_card_info()` 改读 `Assets/Prefabs/Cards/4.0/` 三个稀有度子目录
- 提取字段:`cardTypeID`、`displayName`、printed ATK、rarity(按目录)、生物 flag(ATK 列非空)
- 产出一张 4.0 卡表(脚本内 dict),并与 Notion DB 的 87 张对数,缺失/多余显式报出
- 已知数据风险:`GRAVE_PUNCH` printedAttack=2 存在用户本地未定改动,建模时单独标注
- 验收:卡表数量与池子一致,ATK 缺失卡清单人工过目

**执行结果(2026-09-08,仅 Common 试样)**:完成。块作用域解析器按「内联 cardTypeID + displayName」定位 CardScript 组件(30/30 唯一;CurseEffect 的同名字段是 SO 引用会被内联正则跳过);cardTypeID 字符类须含点号(`AVENGER_4.0` 等 6 张,旧 3.0 加载器会静默截断成 `AVENGER_4`——遗留 bug,3.0 路径暂未修)。产出:`--dump-pool common40` → `tools/outputs/sim4/prefab_card_table_common.json`(30 张=20 生物/10 非生物,rarity 目录↔字段零漂移);对数 `sim4_reconcile_pool.py` vs Notion 快照 `notion_common_snapshot_2026-09-08.json` 全绿(缺失/多余/ATK/生物 flag 零漂移;中文名尾 `*` 为用户圈改标记已剥离)。风险闭环:GRAVE_PUNCH prefab=2=DB=2,已对齐。打印清单供人工过目:ATK=0 生物 3 张(CURSE_REVIVER/HEXER/SACRIFICIAL_SPIRIT,与 DB 一致)、utility 被动 7 张、takeUpSpace=0 1 张(卡位扩张)。

**一致性二次彻查+修复(2026-09-08 晚)**:对数维度扩至 6 项(cid/中文名/ATK/生物/desc 归一化/myTags↔DB tag)。发现 DB 中文名已全量改版(怪谈风新命名)而 prefab 已同步、无漂移;修复 5 处真漂移——prefab 侧(Unity MCP):ELITE_REVIVER desc 补「过」、WOKEN_BLADE myTags 补 MultiAttack;DB 侧(prefab→DB 回写):GRAVE_HEXER 强化2→1、HEXER 强化3→2、SACRIFICIAL_SPIRIT 生物→非生物/去攻击/强化5→4。注意:HEXER/GRAVE_HEXER/SACRIFICIAL 是用户 Unity 侧进行中的重设计,后续批次重跑对数可能再现漂移,属预期。工具口径:desc 半角(prefab 字体规范)↔全角(DB 可读版)标点宽度不等价于漂移;myTags 为小端 int32 hex。**勘误(用户指正)**:被动 tag 必须成对出现(glossary 硬规则),非"机制 flag DB 不标"——7 张 desc 带被动:前缀的 normal 卡(沉魇+6 UTILITY)DB tag 列已补 [被动];长夜/卡位扩张无被动前缀,两侧均不标。Passive 现纳入正常比对。

### Step 2 — 状态与区域引擎升级

- 卡对象增加 `atk`(替代 power 语义)、`attackTimes`、`enhancedFlag`(【被强化】谓词)、`isBeliever`(RIFT token)、`isPassive`、`echoCounter`(回响)
- 区域不变式落地:墓地 = index < startCardIndex;被动卡每次洗牌后强制置于起始卡后;回响回弹卡不进墓地、不可复活
- 攻击结算统一走一个 `resolve_attack()` 入口(对齐 Unity AttackResolverSource 单入口思路),支持多 term 数值来源(固定值/自身攻击力/计数器/引用)
- 事件骨架扩展:新增苏醒、攻击、被强化、回合开始/结束等触发点

### Step 3 — 通用动词库

按术语表逐词实现为纯函数,每词一个单元可测:

攻击/攻击xN、强化N(随机目标+「额外强化」沿用上一目标)、弱化(仅 WEAKENING_FIELD 特例)、埋葬N[目标]、埋葬卡组顶N、置顶(仅未揭晓区)、延后、放逐、生成N信徒、复制自身、攻击力翻倍、攻击力=X、复活N、延迟复活(落起始卡前一格)、攻击次数+N、回响X、触发X的遗言(目标留墓地、不产生埋葬事件)、交换、让墓地友方攻击(留墓地直接结算、触发攻击事件)

同时实现规范扩展写法:遗言触发两次(RELIC_REQUIEM)、给予遗言永久化(洗牌不失效)、攻击转化为强化(RELIC_BLOOD_PACT)、信徒效果改写(RELIC_RIFT_OVERRIDE)、相邻检查(RELIC_TAINT,任何顺序变化后逐事件检查)、墓地攻击光环(RELIC_GRAVE_LORD)

**Step 3 执行结果(2026-09-09,仅 Common 子集)**:完成。已实现动词:攻击/攻击xN(`verb_attack`)、强化N(`verb_enhance`,N=数值+每子句随机 1 目标)、埋葬N[目标](`verb_bury_targets`,N=数量)、埋葬卡组顶N(`verb_bury_deck_top`,不触发揭晓)、复活N(`verb_revive`,池=墓地侧,每张触发苏醒)、生成N信徒(`verb_spawn_believer`,RIFT 入活区底)、放逐(`verb_exile`)。谓词:生物/非生物/被强化/信徒tag/诅咒/阵营/随机(省略=随机已裁定)。目标池锚定 Unity `StatusEffectGiverEffect.CollectFriendlyCards`:强化/埋葬池=全 combined deck(含墓地)+揭晓区卡;复活池=墓地侧。Card40 补 `tags` 字段(谓词输入)。验收:`--selftest-40` 含 verbs 段全绿(含空池失效、xN 单反应窗)。**延期到 U/R 批**:弱化/置顶/延后/复制自身/攻击力翻倍/攻击力=X/攻击次数+N/回响X(Step 2 已有 primitive)/触发X的遗言/交换/让墓地友方攻击/全部 RELIC_ 扩展写法。

### Step 4 — 触发框架

- 触发注册表:key = 时机词,value = 卡 + handler;每次事件发生遍历注册表(对齐被动卡"每事件触发、无每回合上限"裁定)
- 触发来源谓词:阵营/生物/信徒/诅咒/遗言/稀有度/攻击力最高最低/【被强化】/除了【X】
- 嵌套触发支持:外层触发后内层子句挂起为一次性监听(FINAL_ESCORT)
- 反循环:沿用现有 chain depth 上限思路,同卡同事件单次触发;上限对齐 Unity 的 99(当前 sim 为 12,改为可配置并默认调高)

**Step 4 执行结果(2026-09-09,Common 范围)**:完成。`EventBus40` 升级为触发注册表:entry=(card, handler, 来源谓词, once);来源谓词按 payload 过滤(阵营/自身/生物/诅咒等,谓词失败不消耗链守卫);**同 (card, handler) 每条开放链只触发一次**(root dispatch 期间守卫不清,对齐 Unity EffectChainManager 语义,谓词失败先于守卫判断);深度上限默认 99、按 state 可配(`bus.max_depth`);一次性监听(once=True)= FINAL_ESCORT 嵌套触发机制(外层 handler 内注册,内层时机执行一次即移除);被动=无每回合上限(每次匹配事件即触发)。引擎侧接线:reveal_top_40 发 revealed;bury 发 any_buried(遗言=自指谓词);revive/delayed_revive 发 awaken;give_enhance 发 enhanced;shuffle_round 发 round_start/end;**修正**:resolve_attack_40 攻击事件改为每段触发(2026-09-05 裁定,xN=N 个反应窗)。验收:`--selftest-40` 三段(引擎/动词/触发)全绿,3.0 冒烟回归过。

### Step 5 — 逐卡建模(87 张)

- 按 cardTypeID 手写 handler(不做 desc NLP 解析),desc 原文以注释贴在 handler 旁供 review
- 顺序:U(45)→C(15)→R(27),每批完成后跑 Step 6 的 sanity 检查再进下一批
- **近似台账** `docs/Sim4_Approximation_Ledger.md`:每张卡记录 建模精度(精确/近似/未建模) + 近似点说明;台账是报告可信度声明的一部分,沿用旧 sim 文件头"approximated"做法但显式成文
- 验收:87 张全部有 handler 或台账条目,零静默跳过

**Step 5 执行结果(2026-09-09,Common 30 张)**:完成。HANDLERS_40 逐卡手写(desc 原文注释锚定,子句顺序=desc 顺序)+ JU_ON token(揭晓攻击自身侧,base ATK=0 实测);LEDGERED_40=9 张(HP_MAX 静态 hook、长夜不实例化、6 商店被动占位);`coverage_report_40` 强制零静默跳过(实测抓到 GRAVE_HEXER 漏写)。**语义修正两处**:①shuffle_round_40 改 AlwaysBottom 全堆洗+起始卡回底(每轮每卡恰触发一次;旧实现只洗活区会使墓地变死区);②每卡每触发每回合一次门(round_triggered,shuffle 时清)——没有它「遗言:复活自身」同轮无限自循环。同时埋葬/复活池排除被动卡(不可被移动)。combat 驱动器 setup_combat_40/run_combat_40(setup 发静态被动;轮=洗牌→逐张揭晓→handler→埋;HP≤0 终局)。验收:`--selftest-40` 五段全绿;3.0 冒烟回归过。台账落盘 `docs/Sim4_Approximation_Ledger.md`(E1-E12 引擎级 + 逐卡)。Step 6 前待拍板:JU_ON 组内配比、信徒增殖护栏。**用户拍板(09-09)**:JU_ON 非组内原生,每卡组至多 1 张,仅由「强化敌方诅咒」生成(`verb_enhance_enemy_curse`:无则生成入敌方活区底+强化、有则原地强化、放逐后可再生);侍僧稳态 +1/轮、苏醒(被复活)再 +1(引擎本就如此建模,汇报口误已更正);信徒增殖暂不设护栏。自测卡组已去原生 JU_ON,新增「HEXER 生成唯一 JU_ON」断言,五段全绿。

### Step 2 执行结果(2026-09-08,仅 Common 试样)

完成。新增(纯 additive,3.0 路径零改动):`Card40`(atk/attackTimes/enhance_total→【被强化】谓词/card_type/is_believer/echo_bounce/echo_counter)、`GameState40`(合并牌堆,`start_card_index` 边界:墓地=index<start_card_index,被动卡洗牌后强制置于起始卡下墓地侧、永不被揭晓/不可移动/不可复活)、动作层(bury/reveal_top/revive→触发苏醒/delayed_revive(落起始卡前一格,触发苏醒)/echo_bounce(不进墓地)/stage_to_top(仅活区)/add_alive/exile/give_enhance)、`resolve_attack_40` 单入口(multi-term:self_atk/fixed;多段=单次结算单反应窗,xN 规则)、`EventBus40` 事件骨架(round_start/end、attack、any_buried、awaken、enhanced、any_exiled,深度上限 99 对齐 Unity)。验收:`--selftest-40` 全绿;过程中修掉两个引擎缺口(揭晓区卡 bury 前不在 deck 会崩;组牌后需 place_passives)。3.0 主路径冒烟回归通过。

### Step 6 — 校准与报告

- Sanity 检查清单:总伤害守恒(双方 HP 流向对账)、无卡时基准伤害、单卡全同卡组(12×X)压力用例不发散、已知无限循环组合(参照 `docs/Infinity_Check_4.0_2026-08-28.md`)确认被 chain 上限截断
- 输出报告沿用现有格式,字段升级为 4.0 口径:每卡 Avg Dmg/Round、触发次数、苏醒次数、复活拉回数、信徒生成/消耗数、被动触发数
- 与 3.0 sim 的重合卡(若有同 ID)做方向性对比,差异大则回查建模
- **Layer A — 轴条件化抽样**:组牌函数从均匀随机改为可按原型条件化——以某一轴(复活/遗言/信徒/埋葬等)为核心组牌(轴内卡占 50%+,剩余坑位随机填充),每轴各跑一批
- 每卡输出双列指标:裸强度(均匀随机环境)+ 轴内强度(轴条件化环境),修复 payoff 卡被随机组牌稀释的问题
- 最终产出:一次全池 run(建议 6v6 与 10v10、HP25 与无 HP 四配置 × 各轴条件化批次),数据存 `tools/outputs/sim4/`

**Step 6 执行结果(2026-09-09,Common 试样收官)**:完成。sanity 四项全过——伤害守恒(30/30 对账:攻击台账=HP 流失,零 phantom/丢失)、无卡基准(空场惰性)、12×X 全 23 个 handler cid 压力用例稳定、自复活洪流有界(288 揭晓/6轮,每轮一次门生效)。报告:`--report-40` → `tools/outputs/sim4/report_{6v6,6v6_nohp,10v10_hp25,10v10_nohp}.md`,字段=每卡 Dmg/Round、Reveals/Round、Awakens/Round、Burials/Round、Presence、Win%;Layer A 七轴(复活/遗言/信徒/埋葬/诅咒/强化/苏醒)双列指标(裸强度 vs 轴内强度)+ 信徒/诅咒经济表。3.0 对照:同 ID 交集为空,按「去 _4.0 后缀前身」6 对做数量级哨兵(报告内嵌;3.0 为旧膨胀池,仅防建模量级错误)。试样链 Step 1-6 全通。**用户拍板(09-09):先扩 U 层再议 Step 7**(理由:Step 7 是环境质量放大器,21 张有效卡的 Common 池放大噪声;U 层补引擎覆盖率+轴密度,且是计划内欠账)。机制理解六条已逐条确认(R1-R7 入台账二·五节,含 attackGrowth 源码定论=永久攻击变化累计账本、ELITE 谓词=attackGrowth>0)。U 批范围:45 张 handler + 延期动词(置顶/延后/复制自身/攻击力翻倍/攻击力=X/攻击次数+N/触发X的遗言/交换/让墓地友方攻击)+ RELIC 扩展写法 + 「本回合」槽位(attackModThisRound 对齐:回合开始清);回响X 废弃不实现(CURSE_ECHO 备用)。

### Step 7 — Layer B:卡组种群共演化

模拟"玩家会优化"这件事,不依赖真实录制数据(Recorded 数据 2026-08-13 分析结论为 ≈ 随机商店抽取,23/71 从未被选,无法修复稀释;且全部为 3.0 卡组,版本不匹配):

- 初始种群:随机组牌 100 副(可混入少量轴条件化卡组加速收敛)
- 迭代:种群内抽样互战 → 按胜率选择 → 变异(随机换 1-2 张卡)→ 下一代;建议 20-30 代或胜率分布收敛即止
- 收敛后的种群即元环境近似;支持协同放大与 meta 制衡,这是随机抽样测不到的
- 卡强度用**边际贡献法**:强卡组中去掉该卡 vs 换成随机卡,胜率差;对局量 O(卡数 × 强卡组数),比 Layer A 贵约一个数量级
- 报告必须标注代数与评估协议(选择率/变异率/对局抽样方式),否则不同批次数字不可比
- **真实 Recorded 卡组互打保留为第三层验证**:等 4.0 实际对局积累出构筑型卡组后作校验输入,不作主数据源

## 5. 风险与对策

| 风险 | 对策 |
|------|------|
| 双源漂移:Unity 侧继续迭代,裁定变化导致 sim 过时 | 台账 + 语义基准章节显式记录"以 glossary + 裁定提交为准";每次 Unity 侧裁定变更后回查对应 handler |
| 被动卡每事件触发在高频事件下性能/发散 | 全局事件计数 + 单局步数上限(超限记为发散并单独上报,不计入统计) |
| 87 张 handler 工作量大、易抄错 desc | handler 旁贴 desc 原文;分批走 step-gate;写批量 diff 脚本核对 handler 内数值与卡表 ATK |
| 高方差卡(类似 3.0 ETERNAL_GHOST)导致数字不稳 | 报告附带置信区间(现 sim 已有基础,补上分位数) |
| 共演化结果漂移:卡强度随种群收敛而变,批次间不可比 | 报告固定标注代数 + 完整评估协议参数;比较强度只允许同协议批次之间 |
| 共演化对局量大导致单步耗时过长 | 先用小种群/少代数试跑估时;单局模拟保持在毫秒级,必要时减少边际贡献法的替换采样次数 |

## 6. 执行协议

按 step-gate 协议执行:每步完成后汇报结果并停下,等确认再进下一步。Step 5 内部按 U/C/R 三批各自过 gate。Step 7 依赖 Step 5 完成且经 Step 6 sanity 验证后的模拟器,不提前启动。
