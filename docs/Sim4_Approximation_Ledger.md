# Sim4 近似台账(Approximation Ledger)

- 日期:2026-09-09
- 范围:4.0 Common 试样(30 张)+ JU_ON 诅咒 token
- 语义基准:`docs/4.0_Glossary.md`(2026-08-28,含 2026-09-05 攻击每段触发裁定)
- 配套引擎:`tools/scripts/one_deck_damage_sim.py`(`--selftest-40` 为验收入口)
- 精度口径:**精确**=按 glossary 裁定与 prefab 序列化值直接建模;**近似**=语义正确但某细节未逐条核对 Unity;**未建模**=有效果但不进模拟

## 一、引擎级近似(影响全卡,逐卡不重复)

| # | 项 | 建模 | 精度 |
|---|---|------|------|
| E1 | 攻击目标 | 恒为对方玩家(本作无阻断/嘲讽位) | 精确 |
| E2 | 目标选取 | 省略谓词=随机(裁定:战斗中无玩家选择点) | 精确 |
| E3 | 强化/埋葬目标池 | 全牌堆(含墓地)+ 揭晓区卡,锚定 Unity `StatusEffectGiverEffect.CollectFriendlyCards`(includeSelf=true,埋葬系可埋到自身) | 近似(各卡 Unity 池构建未逐卡核对,见 card-selector 记忆「排除集漂移」) |
| E4 | 复活目标池 | 墓地侧(裁定:复活来源区),落牌顶并触发苏醒 | 精确 |
| E5 | 触发门控 | 每卡每触发时机**每回合一次**(Start Card AlwaysBottom:「每张卡每回合触发一次」);链内反循环=同卡同 handler 每开放链一次;深度上限 99 可配 | 精确(裁定级) |
| E6 | 墓地语义 | 循环等待区(下轮洗回,非死区);放逐=唯一永久离场且回弹卡不进墓地不可复活 | 精确 |
| E7 | 被动卡 | 每次洗牌强制置于起始卡下方墓地侧,永不被揭晓、不可被移动/复活/埋葬 | 精确(裁定级) |
| E8 | 信徒 token | RIFT,生成位置=**墓地侧**(源码实锤:CurseEffect 生成卡读回 combinedDeckZone[0]=牌堆底;要等下轮洗牌才会揭晓,本轮不进场)。节奏(用户确认 09-09):侍僧稳态 **+1/轮**(揭晓生成);被复活触发苏醒时**再 +1**。无硬上限,但当前伤害水平 2-4 轮终局,增殖不构成问题,暂不设护栏 | 精确 |
| E9 | 诅咒 | (用户确认 09-09)JU_ON **非组内原生**:每卡组至多 1 张,只能由「强化敌方诅咒」效果生成(无则生成入敌方墓地侧、有则原地强化,放逐后可再生成);base ATK=0(prefab 实测),揭晓时攻击自身侧,靠强化成长 | 精确 |
| E10 | 苏醒 | 仅复活/延迟复活引发;回响回弹、置顶不触发(裁定);延迟复活落起始卡前一格 | 精确 |
| E11 | 不建模项 | 疲劳/overtime、护盾/Rest/Counter(3.0 遗留状态)、商店环境、回合结束触发与嵌套挂起(Common 无用例,框架已备 once 监听) | 未建模(范围内无用例) |
| E12 | 数值来源 | 多 term 攻击入口已备(self_atk/fixed);计数器型来源(攻击力=信徒数等)Common 未用 | 精确(范围内) |

## 二、逐卡台账

### 攻击/遗言/苏醒系(精确建模)

| CID | 中文名 | desc | 精度 | 说明 |
|-----|--------|------|------|------|
| TWIN_STRIKER | 连体人 | 攻击x2 | 精确 | attack_times=2 来自 prefab extraAttackTimes |
| AVENGER_4.0 | 厉鬼 | 攻击x2;遗言:强化自身1 | 精确 | 遗言强化落墓地卡身,复活保留 |
| SPIKE_SKELETON_4.0 | 带刺的标本 | 攻击;遗言:攻击x2 | 精确 | 遗言攻击从墓地直接结算,卡留墓地 |
| SOLDIER_SKELETON_4.0 | 骸骨哨兵 | 攻击;遗言:复活自身 | 精确 | 自复活受 E5 每回合一门约束 |
| WOKEN_BLADE | 尸变 | 攻击;苏醒:攻击x2 | 精确 | 苏醒攻击为独立一次 x2 结算 |
| WAKING_FIGHTER | 回魂尸 | 攻击;苏醒:强化自身1 | 精确 | |
| LAST_GIFT | 遗赠 | 攻击;遗言:强化2友方生物 | 精确 | 强化N=数值+随机1目标(E2) |
| GRAVE_DREDGER | 掘墓人 | 攻击;埋葬卡组顶3卡 | 精确 | 埋卡组顶不触发揭晓 |
| GRAVE_FIST | 血祭 | 埋葬1友方;攻击 | 近似 | 埋葬池含自身(E3);Unity 侧是否排除自身未逐卡核对 |
| GRAVE_PUNCH_4.0 | 剜心祭 | 埋葬1友方;攻击x2 | 近似 | 同上 |

### 复活系(精确)

| CID | 中文名 | desc | 精度 | 说明 |
|-----|--------|------|------|------|
| BEAST_REVIVER | 马戏团的地下室 | 攻击;复活1生物友方 | 精确 | |
| SPIRIT_CALLER | 降灵会 | 攻击;复活1友方非生物 | 近似 | 非生物池含信徒 token(token 非生物;Unity 是否排除 token 未核实) |
| ELITE_REVIVER | 镀金圣髑 | 攻击;复活1被强化过的友方生物 | 精确 | 【被强化】=enhance_total>0 永久标记 |
| RIFT_SHEPHERD | 牧羊人 | 攻击;复活1张tag为[信徒]的友方卡 | 精确 | 谓词=信徒 tag 或 token 身份 |
| CURSE_REVIVER | 收蛊人 | 攻击;复活1敌方诅咒 | 近似 | 敌方无诅咒时失效;依赖 E9 组牌配比 |
| GRAVE_HEXER | 降头师 | 复活1友方;强化1敌方诅咒 | 精确 | 非生物无攻击(09-08 重设计后);省略谓词=随机友方;诅咒生成同 E9 |

### 诅咒强化/信徒系(精确)

| CID | 中文名 | desc | 精度 | 说明 |
|-----|--------|------|------|------|
| HEXER | 蛊婆 | 攻击;强化2敌方诅咒 | 精确 | 专用动词:无诅咒则生成+强化(E9);诅咒在墓地则原地强化 |
| RIFT_ACOLYTE | 侍僧 | 攻击;生成1信徒;苏醒:生成1信徒 | 精确 | 生成入墓地侧(E8);稳态+1/轮、苏醒再+1(用户确认) |
| RIFT_INSECT_4.0 | 新皈依者 | 攻击;生成1信徒 | 精确 | 生成入墓地侧(E8) |
| SACRIFICIAL_SPIRIT | 活殉 | 埋葬1友方;强化4敌方诅咒 | 近似 | 非生物无攻击(09-08 重设计后);埋葬池含自身(E3);诅咒生成同 E9 |
| BLACKSMITH_4.0 | 骨匠 | 攻击;强化1友方生物 | 精确 | |
| WAR_TRAINER | 开刃 | 强化2友方生物 | 精确 | 非生物、无攻击、会被正常揭晓 |

### 系统/商店卡(按机制建模)

| CID | 中文名 | desc | 精度 | 说明 |
|-----|--------|------|------|------|
| SYSTEM_INCREASE_HP_MAX | 沉魇 | 被动:生命值上限+4 | 精确 | 静态 hook,战斗 setup 时生效;仅 HP cap 模式有意义 |
| SYSTEM_INCREASE_DECK_SIZE_LITE | 长夜 | 卡位+1,放逐自身 | 精确 | takeUpSpace=0,战斗不实例化(AGENTS 规则) |
| UTILITY_INCOME_1 | 冥币 | 被动:收入+2 | 精确 | 商店被动,战斗零效果;占卡位稀释(E7 墓地侧常驻) |
| UTILITY_DISCOUNT_1 | 魇市赊账 | 被动:每重掷4次,随机商品降价2 | 精确 | 同上 |
| UTILITY_ODDS_1 | 魇市深处 | 被动:首个货架必为奇物架 | 精确 | 同上 |
| UTILITY_OPTION_1 | 魇市新摊 | 被动:商店货架+1 | 精确 | 同上 |
| UTILITY_REROLL_1 | 噩梦重播 | 被动:免费重掷次数+1 | 精确 | 同上 |
| UTILITY_SLOT_U_1 | 初魇 | 被动:首个货架必出1张✦✦卡 | 精确 | 同上 |

### Token

| CID | 说明 | 精度 |
|-----|------|------|
| JU_ON | 诅咒 token(用户确认 09-09):非组内原生,每卡组至多 1 张,仅由「强化敌方诅咒」生成(墓地侧入口,源码 CurseEffect combinedDeckZone[0] 实锤);揭晓时攻击自身侧;base ATK=0(prefab 实测),靠强化成长 | 精确 |
| RIFT | 信徒 token;无自体效果,墓地侧生成、循环占位;可被复活系/强化系按谓词命中 | 精确(E8) |

## 二·五、机制理解确认(2026-09-09,用户逐条拍板,附源码定论)

| # | 项 | 结论 |
|---|-----|------|
| R1 | 强化反应 vs 被强化 | 「被强化:」触发词**无使用**;唯一被强化相关谓词=ELITE_REVIVER「被强化过的」;其余一律写「强化反应:」=该卡自身被强化时触发(自指)。观察者视角(如 WEAPON_SPIRIT「友方生物触发强化反应时」)按 desc 原文 |
| R2 | 延迟复活 | 触发苏醒(复活变体) |
| R3 | 回响X | **废弃不实现**:CURSE_ECHO 为备用卡,不在引擎池 |
| R4 | 生成卡落点 | 一切生成(信徒/诅咒/**复制自身**)均进墓地侧,统一 spawn_to_graveyard_40 |
| R5 | 「本回合」限定 | desc 带「本回合」→ 回合边界清一次(如 EXILE_BERSERKER 攻击次数、WEAKENING_FIELD 弱化);**无限定 → 永续**(如 COMBO_STARTER 强化反应:攻击次数+1 永久叠加)。判据=desc 原文,Unity 侧实现为 attackModThisRound/attackTimesModThisRound 槽位 |
| R6 | attackGrowth(源码定论) | =**永久攻击力变化累计账本**(CardScript.ModifyAttack:强化/弱化/转移/虹吸,stackable,combat 内不清);GetAttack=printedAttack+attackGrowth+attackModThisRound+墓地光环;attackModThisRound=本回合槽位,Unity 注释「cleared at each round start」;ELITE_REVIVER 的「被强化过的」谓词在 Unity 侧=attackGrowth>0(ReviveEffect:113),与 sim 的 enhance_total>0 同构。**注意**:弱化走同一账本可为负,U 批若引入弱化,【被强化】谓词需改带符号判断 |
| R7 | 攻击转化每段 | 源码测试实证:3 段攻击→3 次独立+1 强化(每次一段一个转化),与 09-05 每段触发裁定同构 |

## 二·六、R1-R7 全池 DB 验证(2026-09-09,全稀有度扫描)

**逐条验证结果**:
- R1 ✅:「被强化」全池仅 ELITE_REVIVER(谓词形态「被强化过的」);强化反应系=UNDYING_WARRIOR/COMBO_STARTER/SNOWBALL(自指)+WEAPON_SPIRIT(观察者「友方生物触发强化反应时」),零例外
- R5 ✅:EXILE_BERSERKER desc 已更新为双「本回合」(每放逐1友方计数、攻击次数均限本回合);COMBO_STARTER 无「本回合」=永续;BATTLE_HORN/COMBO_GRANTER/WEAKENING_FIELD 均带「本回合」;REVIVING_STRIKER 遗言「攻击次数+1」无「本回合」=永续成长,判据=desc 原文成立。⚠️ glossary「攻击次数+N」词条仍无条件写「本回合」,与实际口径不符,待维护
- R3 ✅ 扩大:CURSE_ECHO(回响)、**DELAYER(延后)、SOUL_SWAPPER(交换)均=备用** → U 批动词清单再删「延后/交换」两项
- 遗言触发两次/给予遗言永久化:RELIC_REQUIEM/RELIC_UNDYING_WILL **行已不存在**,全池零使用 → 仅框架备忘,不实现
- 相邻时(RELIC_TAINT)=备用 → 相邻检查不实现
- 延迟复活活跃使用仅 FUNERAL_WILL 一张(MASS_SACRIFICE 已改版为埋葬所有友方+生成信徒)
- JU_ON desc=「对自身造成[攻击力]点伤害」=攻击自身侧,与引擎模型一致 ✓

**数据修复**:REVIVING_STRIKER 生物列空缺(ATK=1+desc 有攻击,违反 生物⟺ATK非空)→ 已补「生物」

**U 批动词清单(收缩后)**:置顶(DEATHBED_PORTER)/复制自身(SLIME)/攻击力翻倍(UNFINISHED_ROBOT)/攻击力=X 六卡(GRAVE_GIANT/MIMIC_BLADE/CURSE_EATER/GRAVE_ROBBER/REANIMATOR/RELIC_GRAVE_CURSE 动态光环)/攻击次数+N 六卡/触发X的遗言(LAST_RITES/RELIC_DEATH_KNELL)/让墓地友方攻击(GRAVE_PUPPETEER/DEATHBED_GRANT)/延迟复活(FUNERAL_WILL)。RELIC 扩展写法保留:BLOOD_PACT(每段转化,源码已实证)/RIFT_OVERRIDE(效果改写)/GRAVE_LORD(墓地光环)/DEATH_KNELL(苏醒触发遗言)/TAINT 系(已废弃)

- DECIMATION 读法确认(09-09 用户拍板):埋葬数 = 6 − 本回合已埋葬友方数(防滚雪球上限);R 批建模按此

## 二·七、U 层扩批执行结果(2026-09-09)

- 范围:TRIAL_RARITY_DIRS += 1_Uncommon;卡表 78 张(36 生物/42 非生物);对数 normal+uncommon 全绿(78=78)
- **同步修复 7 处**:COMBO_STARTER DB rarity rare→uncommon(唯一 prefab 在 U 目录,rarity=1);EXILE_BERSERKER prefab desc 补第二「本回合」;MIMIC_BLADE prefab 补 MultiAttack tag;百怪入梦/窥魇镜/连魇/死寂的梦/魇影幢幢 5 张 desc 补「被动:」前缀 + DB tag [被动](前缀⟺tag 成对)
- **新发现并修正**:RIFT 信徒 desc=「复活 1 友方,去除自身」=纯复活器(此前 Common 批误建模为死重量)——「去除自身」按放逐建模;RELIC_RIFT_OVERRIDE(R 批)的「效果变为」改写对象即此
- **引擎重构**:Card40 攻击力账本三槽位(printed_atk/enhance_total 永久带符号/atk_mod_round 本回合)+ attack_times 双槽位;回合开始清本回合槽+计数器;【被强化】=enhance_total>0(带符号,弱化可打穿);新增 on_enhanced/on_round_start/on_events(门控自定义时机)与 passive_events(无每回合一门)注册路径;PASSIVE_PRESENCE_DEF 旗标机制(血契转化/信徒改写/提前发作/白骨王座光环/积尸气 set-aura 钩子就绪,R 批填卡)
- 新动词:攻击力=X(账本吸收差值)/翻倍(计为强化)/攻击次数+N(本回合/永续)/全体弱化(本回合槽)/复制自身(新基卡入墓地)/触发X的遗言(消耗每回合门)/让墓地X攻击/最高最低选择器;延迟复活/置顶已有原语
- 新计数器:本回合放逐友方数(狂信徒)、本回合埋葬生物数(血账)、本回合复活友方数(百鬼夜行)
- 台账 40 张 U handler + 5 张 utility 入 LEDGERED;覆盖 67 handler cid 全过 12×12 压力;守恒 30/30
- **修了三个真 bug**:verb_bury_deck_top 循环前一次性评估活区数,活区见底后 pop 到起始卡(埋了 START);GRAVE_ROBBER 敌方墓地池未滤被动;exile 不容忍揭晓区卡(信徒去除自身崩)
- R 层 backlog:2_Rare 32 张 + 稀有 utility;PASSIVE_PRESENCE_DEF 待填卡

## 三、验收

- 覆盖:30/30 + JU_ON 全部「有 handler 或台账条目」,零静默跳过(`coverage_report_40` 强制)
- 入口:`python one_deck_damage_sim.py --selftest-40`(引擎/动词/触发/战斗四段)
- 用户已拍板(09-09):JU_ON 无原生配比、每卡组至多 1 张、由强化敌方诅咒生成;侍僧稳态 +1/轮、苏醒再 +1。信徒增殖暂不设护栏(E8)
- Step 6 前剩余:无(可直接进校准与报告)
