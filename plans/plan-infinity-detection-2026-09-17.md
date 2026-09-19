# Plan: 无限检测与防下发(Infinity Detection and Serving Gate)

- 日期: 2026-09-17
- 状态: 方案评审稿;2026-09-17 已落地 L0 全部硬终止 + 置顶墓区排除 + 疲劳复活通配(5a09ebb / 34075e5 / 62f7222,EditMode 559 绿,Play 2026-09-18 用户已验),2026-09-18 §9 结果语义拍板 + 检测方向改拍(排列周期 = 无限递归主判据,预算启发降级;配对责不 flag)。**2026-09-19 三批收尾**:①§15 复核更正——lethal 样本实为回合内循环,§14「整周期」结论作废(脚手架触发接线 artifact),运行时验收按生产接线重做;②§15.6 `InfiniteDeckTerminationTests` 统一到生产接线;③§16 P1b `RunBudgetSim` headless 化补齐(离线编译 0 error,EditMode 测试待跑)。P1 至此完成;P2 起按 §10 分期等「修改代码」开工
- 关联: docs/RngDeterminism.md(Rng/digest 基建)、docs/RegressionChecklist.md、docs/AgentRegistry.md;2026-09-13 埋葬递归 SOE 崩溃诊断
- 核心抽象: `RunBudgetSim(deckA, deckB|木桩, seed) -> BudgetTripReport`,对局归因 / 离线回扫 / 可选预检三处共用一份实现

## 1. 问题与目标

异步 PvP 中玩家上传的 ghost deck 可能含无限循环组合,受害者匹配进该对局会卡死或崩溃(2026-09-13 实际发生 213 层嵌套 BuryNextXCards 的 StackOverflow 崩溃,见 §6)。

目标(已拍板):
1. 玩家永远不会匹配到「会无限」的敌方 deck;
2. 不限制玩家侧构筑:玩家自己的无限卡组可继续使用与上传;
3. 检测体系独立可复用:离线批扫存量 deck、对局中在线归因、喂设计侧修卡,走同一套模拟。

非目标(已拍板,理由见 §7):
- 跨侧(双方卡组组合)无限不做独立工程;
- 良性/恶性无限分类不参与下发判定。

## 2. 总体架构

| 层 | 触发点 | 动作 |
|---|---|---|
| L0 引擎硬终止(前置) | 对局内 | 保证任何对局必然结束:物理嵌套深度闸 + 每回合 reveal 上限 + 强制结清 |
| L1 运行时预算检测(主检测器) | 真实对局中 | 信号触发 → 强制终局 → 归因 |
| L2 归因 + 组合提取 | L1 触发后 | 三连 sim 定责任方;ddmin 最小化提取组合 → 组合库候选 |
| L3 组合库 + 下发过滤 | 匹配选敌时 | 已知组合交集检查 + deck flag 出队过滤 |

辅助:独立批扫工具(同一 RunBudgetSim),离线回扫服务器全量 deck + 本地 RecordedDecks。

定位:L1 是主检测器,不是安全网——安全网是 L0。预算检测判「打不完」而非「是无限」,前者才是玩家可感知的伤害。

## 3. L1 运行时预算检测

判据原则(2026-09-18 用户改拍):主判据 = **直接检测循环本体**——同一回合内完全相同的卡片排列重复出现 = 无限递归,即 flag 判据;不依赖「打不完」的结果推断,L1 相对预算启发(K×池等)降级为辅助/遥测信号。触发任一信号 = 疑似 → headless 归因 sim 确认 → 强制终局 + flag。09-17 原原则「不证明无限,只判打不完」作废。

| 信号 | 阈值(待标定) | 抓什么 |
|---|---|---|
| 单回合 reveal 数 | > K × 回合开始合池大小(K≈3) | 置顶/苏醒回环(回合内不结束) |
| 总回合数 | > 上限(≈30) | 跨回合增长型 |
| chainDepth 触顶事件 | EffectChainManager 现有 | 单链深递归 |
| 物理嵌套深度闸(P0 新增) | 见 §5 | 09-13 型换链级联 |
| 合池总量 | > 上限 | token 增殖型 |
| 状态 digest 重复 → 升级为**同回合排列哈希重复**(2026-09-18 拍板主判据) | 同一排列第 3 次出现(次数可调) | 循环本体 = 无限递归。哈希 = cardTypeID+阵营 序列(**纯排列,不含 HP/power/盾**):带泵循环(lethal infinite 型,每圈增强诅咒但仍按圈重复排列)依然命中;原「排除单调计数器后全状态哈希」反而会漏带泵循环,作废 |

- 复现基建现成:RngService 四通道确定化、TestManager.overrideCombatSeed / -odseed、DeterminismDigest(RngService.cs)。
- 排列周期检测器设计要点(2026-09-18):①哈希每揭晓一次,O(卡组长度),回合边界(Start Card 洗牌)重置哈希集合——§13 回合饿死形态下同回合拖几百揭晓无碍;②「第 2 次出现」即触发会误伤有限循环(SLIME CheckCost_Counter(2) 型),取**第 3 次出现**触发,且触发后先跑归因三连 sim——确认无界才强制终局 + flag,有限则游戏继续、只记遥测,误报代价为零;③盲区 = 增殖型(衍生物/疲劳卡插入改变卡数即打破排列):合池上限信号 + L0 兜底,增殖型是否属 flag 判据另行拍板。
- 木桩定义:敌方 = 中立起手卡 × N + 大血量,无效果。
- 首受害者成本:每个新组合约 1 局受影响对局,之后该 deck 被标记。接受。

## 4. L2 归因与组合提取

触发后 headless 复跑三局(双方 deck 与 seed 都在客户端手里,秒级):

1. 敌 deck vs 木桩 成环 → 敌责 → flag 敌 deck;
2. 我 deck vs 木桩 成环 → 我责 → 不 flag 任何人,判我负(玩家侧无限不封);
3. 原配对重放 成环 → 配对级 → 记配对事件,不 flag。

- 配对重放永久保留:跨侧虽不立项(§7),此槽位零成本,使管线天然 pair-aware。
- 组合最小化:ddmin 逐张剔除 + 复跑;最小集须多 seed 稳健(仅单 seed 成环不入库)。
- 组合条目 schema:
  - mySide: [cardTypeID...]
  - enemySide: [cardTypeID...](跨侧预留,现阶段恒空)
  - roles: 续链角色 engine / chain-switcher / pump(示例见 §6)
  - tripSignals / reproSeeds / evidenceRef
  - status: candidate → active(admin 复核后)/ retired(卡改动后须复验)

组合库必须由 sim 判定入库,不能靠读 desc 手工维护——desc 会因省略而撒谎(例:丧钟×无头武生在 desc 层看似递归,实际被 ReviveSelf 的墓区早退拦死)。
入库判据(2026-09-18 用户拍板)= sim 中检出**无界递归**(同回合排列周期,见 §3),与「预算打不完」解耦。带击杀泵的循环同样必 flag:`lethal infinite test`(RELIC_CURSE_REVIVAL + CURSE_GARDENER,循环每圈增强诅咒、恰好三回合内击杀自终止)是典型标本——验收标准:该 deck 必须被排列周期检测器命中并入库,尽管其自然对局 17 揭晓击杀、预算零触顶。

## 5. L0 引擎硬终止(P0 前置)

「不限制玩家侧」只能解释为「不封牌」,不能是「不打断战斗」:检出的对局仍要有强制终局,否则受害者照样卡在循环里。

现状缺口(代码事实,2026-09-17 复核修订):
- 疲劳 = 塞疲劳卡(CombatManager.cs:440)而非直接扣血,且 reveal 疲劳仅在 totalCardsRevealed 恰等于阈值时触发一次(CombatManager.cs:416)→ 回合不推进则疲劳弹药不补给;
- chainDepth 闸在 EffectChainManager(EffectCanBeInvoked,chainDepth > ChainDepthLimit=99 触顶;历史上的「12」从未生效);
- 勘误(2026-09-17):原稿「stage 回环可让疲劳卡永沉牌底不被揭晓」不准确——置顶选池本扫整张合池(无墓区排除),无类型过滤的置顶环会随机抽到合池内的疲劳卡并 chew 出 HP 税;真死角 = 排疲劳选池的循环(creatureOnly/tag 过滤)与「把疲劳埋进墓区而非揭晓」的混合环(高频埋葬源 × StageSelf 卡),二者是 L1 信号 + 归因 sim 的猎物。
- 置顶轴选区闸已补(2026-09-17,34075e5,EditMode 559 绿,Play 待验):StageEffect 全路径排除 Start Card 以下区域——StageChosenCards 单一 choke point RemoveAll + 两个 max 选择器预过滤(避免「最高攻击者恰在墓侧」时整体落空);复活(ReviveEffect)成为唯一墓区通道。FINAL_ESCORT 遗留 round-end stage 死代码(roundEndStageArmed / ArmRoundEndStageMaxAttackCreature / StageMaxAttackCreatureIfArmed)已清,对应测试替换为墓区排除守卫用例(RoundEndAndBurialCountersTests)。行为变化:RELIC_WHITE_BANNER(引魂幡)回合开始置顶不再能拉回已被消耗的墓侧实体——接受隐藏规则,desc 不动(09-16 疲劳案先例)。

待实施(2026-09-17 修订;✅ = 已落地 5a09ebb):
1. ~~物理嵌套深度独立计数~~ ✅ 已随守卫修复落地(chainDepth 仅 ResetGenerationGuards 清零);
2. ~~埋葬级联(连坐类)每回合上限~~ 已砍(2026-09-17 拍板:chainDepth 熔断已覆盖级联内递归);
3. ~~每回合 reveal 硬上限 → 触顶强制结清~~ ✅ CombatBudgetGuard.maxRevealsPerRound(默认 200):触顶后排空链、剩余牌只翻面不触发(Start Card 洗牌仍发,回合边界存活),下一回合预算重置;
4. ~~全局闸~~ ✅ CombatBudgetGuard.maxTotalReveals(默认 1500)/ maxRounds(默认 60):触顶 ForceConcludeCombat,占位终局语义(不扣 HP,结算读剩余 HP,高者胜/平则平,§7.7 可换);熔断为绝对量,检测阈值(相对量 K×回池)留 P1 双轨标定;被动埋点(per-round 揭晓峰值/级联深度峰值/合池峰值)已进 DeterminismDigest。

09-13 换链环修复状态:2026-09-17 修改 same-card-different-object 分支——中Cascade关链只做 recorder 分组,守卫与深度不再被换链洗掉,仅由 ResetGenerationGuards 在阶段/揭晓边界清零(EffectChainManager.cs:285/:292;SameCardDifferentObject 日志标注 "recorder grouping only; loop guard persists",:96)。三方环每圈换链不再重置计数器,将触闸。已提交(5a09ebb,与 L0 硬终止同交);EditMode 559 绿,Play 待验。

池层防线(2026-09-17 补充):置顶环的必要条件是「置顶触发器每级联重燃」(每次揭晓 ResetGenerationGuards 重发预算);现有置顶绑定全是低频(4.0 活跃池唯一 = RELIC_WHITE_BANNER 回合开始族,FINAL_ESCORT 已改复活),今日不环——但这道护栏是池层惯例不是引擎闸,离无限一张卡(onAnyCardRevealed → StageMyCards(1) 单卡成环:每级联 pop 1 + 置顶 1,合池净不变,回合永不结束)。处置 = 审计而非引擎闸:把「高频事件(onAnyCardRevealed / onFriendlyCardBuried 族)× 置顶/埋葬/自埋自顶」加进 unity-card-infinity-check 审计口径,新卡建卡时拦;L0 兜底。标定前置:先落被动埋点(per-round 揭晓数 / 级联深度峰值 / 合池峰值进 CombatLogs digest,行为零变化),真实对局夹逼出安全边距再定阈值。

疲劳卡 AttackSelf 化(2026-09-18,工作区未提交):Fatigue.prefab 子效果由 HPAlterEffect(DecreaseMyHp + DecreaseTheirHp,双方各 1)改为 AttackEffect.AttackSelf(仅自方 1,走攻击管线;cardDesc 改「对自己攻击」)。影响核查(review 2026-09-18):① overtime 每触发每方疲劳伤害 2→1(双方仍对称竞速,收敛仍成立——疲劳卡每被揭一次打一次,随卡数累积加速);② 新增 onAnyCardAttacked 事件面(GUID 67599e7 验证全卡池零监听,今日惰性;已列入 unity-card-infinity-check 审计关注);③ printedAttack=1 使疲劳卡进入「伤害卡」谓词(IsCreature||HasAttackAttribute),会被 Power 赋予类选中——Power 对 AttackEffect 不计伤害,只产 TestManager 诊断日志噪声;④ 结果面板将出 SYSTEM_FATIGUE 行(IsNeutralCard 仅看 isStartCard,不排除),记 DamageDealtToSelf;⑤ 自伤先扣己方盾(与原路径一致);⑥ BloodPact 转换只作用于攻敌路径,自伤不受影响;⑦ 遗留:子物体名仍叫 "deal 1 dmg to both players",建议改名;⑧ 与复活通配(wildcardTypeFilter,62f7222)配合成立:复活轴捞回疲劳卡即多一次自伤,「复活疲劳自杀」加速,支撑 §9 我责判负分支。

## 6. 一号标本:09-13 三方环

| 角色 | 卡 | 绑定 |
|---|---|---|
| 引擎 | 连坐 RELIC_CHAIN_BURIAL ✦✦(每次友方被埋葬,埋葬卡组顶 1 卡) | OnFriendlyCardBuried → BuryNextXCards |
| 换链器 | 垂死反扑 DEATHBED_GRANT ✦✦(友方实体被埋葬时:该友方实体攻击) | OnFriendlyCardBuried → AttackEffect |
| 油泵 | 骸骨哨兵 SOLDIER_SKELETON_4.0 ✦(遗言:复活自身) | OnMeBuried → ReviveSelf |

环 essentials:任意友方实体被埋 → 连坐挖顶卡 + 垂死反扑令被埋实体攻击 → 新被埋卡再拉起事件 → 修复前「同卡不同对象」的攻击触发开新链,同实例守卫与 chainDepth 被每圈洗掉,连坐每圈重来;骸骨哨兵被埋即遗言复活自身回顶部区,永动供料;213 层嵌套栈溢出崩溃。

事实:全 4.0 池 OnFriendlyCardBuried 监听者仅这两张(GUID 级验证)。

用途:RunBudgetSim 验收用例(必须被 L1 信号命中且被 L0 终止)+ P0 修复回归用例 + 组合最小化演练用例(应产出带角色的三卡最小集)。

## 7. 决策记录(2026-09-17 拍板)

1. 跨侧组合无限:不做独立工程。根因 = 当前引擎不变量(复活选区=墓区限定 + 揭晓区卡不可复活 + ReviveSelf 墓区早退,ReviveEffect.BuildRevivePool;同实例链守卫)使当前池构不成跨侧闭合;勾魂簿×敌方复辟轴、丧钟×无头武生两个候选均被否证。保留两个零成本保险:三连 sim 恒含配对重放;组合条目 enemySide 字段保留(恒空)。
2. 良性/恶性分类:是座位属性不是牌组属性,同一循环对主人良性、对受害者恶性。下发判定只用一比特——「已证单侧无限核 → ghost 不下发,主人可继续使用/上传」。自害型无限对受害者无害(ghost 先死),不标记不拦截。良/恶仅作设计遥测标签(哪些组合该削)。
3. 上传前木桩预检:降级为可选;主防线 = 运行时检测 + 归因。
4. 匹配时「我方牌 × 敌方牌」组合检查:取消;匹配层只做 deck flag 过滤。
5. 点火器顾虑撤回:点火器只够到同侧核既有上限,同侧核上传端即拦。
6. 拦截自动(2026-09-17 拍板):战斗内强制终局全自动、无玩家确认(与 autoReveal / headless 测试一致);组合库入库走 sim 门槛自动进(多 seed 稳健 + ddmin 最小集),admin 只管事后申诉/退役,不做入库前置(取代 §11.3 原「倾向复核」)。
7. 胜负语义延后(2026-09-17 拍板):§9 先不管;P0 的强制结清带可替换占位终局语义(暂按 HP 比例判 / 平局),钩子与自动性先立住,语义随时换。

## 8. 下发过滤与服务器

- 敌方 deck 三来源统一 flag 过滤:server ghosts(OpponentDeckCache.cs:137,GET /api/decks/opponents)+ 本地 json 回退 + default pool(DeckSaver.defaultEnemyDeckPool);选牌口统一检查,缓存条目须可刷新(MergeResponse)。
- 服务端:decks 表 ensureColumn 加 flag 列;新增 loop_reports 证据表(deck_id, reporter, seed, signals, pair_fingerprint, ts);server.js:193 ensureColumn 模式现成,decks 表 :73,出队过滤 :428。
- 信任模型:playerId 即凭证(server.js:19 注释自认),客户端上报可伪造、可诬告——误伤由归因 sim 防住(敌单独不环不 flag);证据落盘供离线批扫工具复核可疑上报。软标记 = 默认排除出池;硬拉黑留给 admin。
- 可选挂点:OpponentDeckCache.Prefetch(:128,每 session 预取 6 副)之后后台跑配对预算 sim,判定随缓存条目存。

## 9. 结果语义(2026-09-18 用户拍板)

判平局 = 无效局,双方不计胜负。flag 一律指 flag **卡片组合**(§4 组合库条目,非 flag 整副卡组;下发层表现为含该组合的 ghost 不下发,主人可继续用/传,§7.2)。

- 敌责:第一层 = 下发过滤拦(§8/P3-P4);漏网之局判平局。
- 我责(玩家侧无限允许,不封牌):
  - 能打死敌方 → 正常判胜利;造成无限的卡片组合仍 flag;
  - 不能打死 → 疲劳自杀路径(疲劳卡 2026-09-18 改 AttackSelf 自伤,见 §5)正常判负;疲劳也杀不动(被盾/回复顶住)则判平局;
  - 两种结局均 flag 卡片组合。
- 配对责:判平局 + 记录,**不 flag**(2026-09-18 用户拍板维持 §7.1:现无配对才成立的无限;将来立项时 flag 条目需加卡片归属方字段(enemySide)+ 匹配时跨侧组合检测)。
- L0 硬上限触顶:判平局(替换 §7.7 HP 比例占位)。

拍板细化项(review 2026-09-18):

1. ~~「能打死也 flag」与 §4 入库判据冲突~~ **已解决**(2026-09-18 用户拍板):flag 判据 = 无限递归(sim 排列周期检测,见 §3),lethal infinite 型带泵循环必 flag;
2. ~~配对责 flag 与 §7.1/§7.4 冲突~~ **已解决**(2026-09-18 用户拍板):配对责不 flag,维持 §7.1,将来需要时再补归属字段 + 跨侧检测;
3. 「能打死 → 判胜利」要求归因 sim 跑到自然终局(sim-to-completion)确认击杀,而非预算触顶即停;已并入检测器流程(§3 设计要点②:触发先 sim,确认无界才终局);胜负以 sim 复跑结果为准,不重开玩家局;
4. 边界:敌责=平局仅在强制终局介入时成立;若疲劳竞速先自然杀死玩家,该局自然判负——受害者保护实际依赖 L1 早检(检测器)+ 下发过滤,终局语义只兜强制终局的部分。

## 10. 实施分期

| 期 | 内容 | 验收 |
|---|---|---|
| P0 | ✅ 已落地(5a09ebb):揭晓强制结清 + 全局闸 + 被动埋点;连坐每回合上限已砍 | 09-13 复现局必终止 → Play 2026-09-18 用户已验 |
| P1a | ✅ L1 排列周期检测器(§14/§15) | 两块标本被信号命中,EditMode 已验 |
| P1b | ✅ RunBudgetSim headless 化(§16,2026-09-19) | 两块标本经 sim 复现同一结论 + 固定 seed 可复现;离线编译已验,EditMode 待跑 |
| P2 | 归因三连 + ddmin 最小化 + loop_reports 上报 | 标本提取出带角色三卡最小集 |
| P3 | 服务端 flag / 证据表 / 出队过滤 / 三来源统一 / admin | 被 flag 的 deck 不再下发 |
| P4 | 组合库表 + 匹配交集检查 + 复核流 | 含标本组合的任意 deck 在匹配时被滤除 |
| P5 | 存量回扫批工具(服务器全量 + 本地 RecordedDecks) | 回扫报告落盘 |

Edit-mode headless 体系复用 HeadlessCombatTestFixture / NullCombatVisuals,不进 Play Mode。

## 11. 待拍板清单

1. 阈值标定(2026-09-18 修订):主标定对象改为**周期检测器参数**——同一排列出现次数触发阈值、哈希内容(cardTypeID+阵营序列)、回合边界重置;L0 绝对上限(100/1500/60)与生产对局夹逼照走。K×池等相对预算启发降级为遥测,不再是标定重点;
2. ~~敌责与硬上限触顶的结果语义~~ 2026-09-18 已拍板,见 §9(含 4 条细化项待确认);
3. ~~组合库入库:自动 or admin 复核~~ 已拍板 = 自动入库,admin 事后申诉/退役(§7.6);
4. 回扫频率与触发时机。

## 12. 附录:代码事实锚点

| 事实 | 位置 |
|---|---|
| chainDepth 闸(<=12) | EffectChainManager.cs EffectCanBeInvoked |
| 守卫+深度仅阶段边界清零(09-17 修复) | EffectChainManager.cs:285 ResetGenerationGuards / :292 |
| SameCardDifferentObject 仅分组不清守卫(09-17 修复) | EffectChainManager.cs:58 CheckShouldIStartANewChain / :96 |
| 疲劳=塞卡;reveal 疲劳恰等触发一次 | CombatManager.cs:440 / :416 |
| 复活池=墓区限定+揭晓区排除+ReviveSelf 早退 | ReviveEffect.cs BuildRevivePool / ReviveSelf |
| 置顶墓区排除(choke point + max 选择器预过滤,2026-09-17) | StageEffect.cs StageChosenCards / IsCardBelowStartCard |
| OnFriendlyCardBuried 仅 2 监听者 | DEATHBED_GRANT、RELIC_CHAIN_BURIAL(GUID 验证) |
| 敌方 deck 三来源 | OpponentDeckCache SourceServer/SourceLocal/SourcePool |
| 服务器 decks 表 / 出队 / 认证模型 / ensureColumn | server.js:73 / :428 / :19 / :193 |
| RNG 确定化 + digest | RngService.cs(RngChannel、DeterminismDigest) |

## 13. 样本卡组终止性单测(2026-09-18 已完成,提交 b327bc3)

- 样本(用户拍板):`Assets/SORefs/Decks/test decks/chain tests/4.0/lethal infinite test.asset`(RELIC_CURSE_REVIVAL + CURSE_GARDENER,自终止=打死对面,生产熔断不该出手)与 `non-lethal infinite test.asset`(GRAVE_HEXER + SPIRIT_CALLER,无攻击,期望 overtime 疲劳收敛打死;测试内 overtimeRoundThreshold=2,60 回合全局闸只许兜底)。
- 数据判读(2026-09-18):09:05 digest(reveals=17/池6/深度2/零熔断)= lethal 击杀自终止,符合预期;昨晚 22:48/22:55/23:19 三局 Seed 落盘、无 digest = 不击杀循环跑挂(当时编辑器未载 22:30 提交)。
- **结果(2026-09-18 接手会话)**:`InfiniteDeckTerminationTests` 两测全绿(提交 b327bc3,含 .meta),全量 EditMode 561 total/560 绿/0 失败/1 既有 Ignore。
- **追加(2026-09-18,833d385)**:non-lethal 样本启用按揭晓数疲劳——`fatigueRevealThreshold` 0→生产值 40,驱动器每次 RevealTopCard 后反射补调私有 `CheckFatigueByRevealCount`(生产挂 RevealNextCardCore:1077,fixture 原语绕过该路径,不补调则阈值无效)。动机:overtime 型疲劳时钟=回合数,会被复活拉环饿死回合边界冻住(round 1 拖 200 揭晓);按揭晓数型时钟=总揭晓数,饿死回合内照常走针,是该类循环唯一有效的疲劳计量器。启用后 non-lethal 收敛 1.76s→0.26s。
- **接手会话定位并修复的四个断环(全部在测试文件内,产品代码零改动)**:
  1. **链代守卫不复位**:fixture 原语不带 `CloseOpenedChain + ResetGenerationGuards`,第一圈后守卫历史烧毁所有(卡,效果)对,后续揭晓全部静默(revivedO/E 卡 1、零伤害)。修复=驱动器每次触发循环末尾补两连调(镜像 CombatManager 确认路径 :952)。
  2. **`fatigueAmount` 默认 0**:fixture 裸 AddComponent 建的 CombatManager 该字段为 0,AddFatigueCards 空转。修复=测试显式 `fatigueAmount = 1`。
  3. **Start Card 用了裸桩**:fixture 的 CreateStartCard 只有 isStartCard=true 的 CardScript,无容器/洗牌效果 → 回合边界只发生一次(第一张),之后起始卡被复活轴搅动压在牌堆中部永不再浮出 → rounds 冻结 → thisRound 累到 200 触发 per-round 熔断 → 全部非起始卡触发被跳过。修复=改载真 `StartCard.prefab`,且驱动器镜像生产特判(CombatManager.RevealCards:起始卡不走 reveal 事件广播,直接 `container.InvokeEffectEvent()`)。真身 ExecuteShuffleEffect 内含 round++/疲劳检查/Rng 全洗/AlwaysBottom 回底。
  4. **`CombatBudgetGuard.Me` 为 null**:EditMode 不跑 Awake,静态单例从不指向测试 guard,HandleNewRoundStart 的 NotifyRoundStart 落空 → forceClear 一旦触发永不清除。修复=CreateGuard 反射写静态 Me。
- **语义修正(拍板记录)**:non-lethal 样本并非「无杀伤」——GRAVE_HEXER 的 EnhanceCurse 叠攻敌方 JU_ON,诅咒自噬约 3 回合即杀死敌方,比疲劳收敛更快。测试改断言:死亡终止 + 过劳机制确已介入(rounds 越过阈值、疲劳卡已入堆)+ 熔断未出手;不再要求玩家被疲劳扣血。
- **引擎动态观察**:复活轴(ReviveBatch 移顶)+ 回底插 0 的组合可把起始卡压在牌堆中部饿死回合边界(round 1 曾拖到 200 揭晓)——per-round 熔断的「起始卡洗牌仍触发」设计恰好兜住此场景,边界最终存活。生产整局若出现超大 perRoundRevealPeak 即此形态。
- 编辑器事故记录:10:09 起主线程长时间无响应(用户处理一次弹窗后短暂恢复;测试启动撞域重载,MCP 插件会话断开;期间出现第 3 个 Unity.exe 进程)。接手会话同款事故复现一次:强同步重编译期间弹「Scene(s) Have Been Modified」模态框阻塞主线程,经 Win32 BM_CLICK「Don't Save」(丢弃的是测试对象脏标记)解除;后续流程先 refresh 编译完再跑测试,未再复现。
- 已提交:594eed5(tag 登记)、21af9e3(cardsRevealedThisRound 回合重置)、b327bc3(本测试)。registry 20260918-090854 已删除。
- **Play Mode 验证(2026-09-18,用户复现)**:09-13 型复现局在 P0 硬终止下按预期收场,P0 验收闭环;两处「Play 待验」状态随之更新。

## 14. 排列周期检测器落地(2026-09-19,P1 核心件,编译/测试待验)

- 实现:`Assets/Scripts/Managers/CombatArrangementCycleDetector.cs` — 纯排列哈希(cardTypeID+阵营 序列,RngDigest.FNV-1a),每次揭晓在确认路径(CombatManager 两条 confirm 路径的链代重置后)采样一次;同一回合第 3 次出现同一排列 → Tripped + `OnCycleTripped(uint hash)` 事件(P2 归因挂点)+ TestManager 日志(已登记进 CombatFlow 路由)。观察-only:不强制终局(§3 要点② 的 sim 门归 P2),L0 保底不变。
- CombatManager 挂钩(均镜像 CombatBudgetGuard 模式):Awake 自动创建组件、战斗清理 ResetState、回合开始 NotifyRoundStart(洗牌后旧排列时代作废)。TestManager.InferCategory 已登记 `[CombatArrangementCycleDetector]`。
- 静默期安全论证(已被测试钉住):静止卡组满回合每个排列每回合只被采样一次(采样周期=卡组数、每回合样本数=卡组数-1,起始卡回顶即回合边界),回合重置把跨回合重样排除 → 安静对局不可能误触;触发线取「第 3 次出现」以避开有限循环(SLIME CheckCost_Counter(2) 型最多重复两圈)。
- 测试:`Assets/Scripts/Editor/Tests/ArrangementCycleDetectorTests.cs` — 6 个单测(三次触发/两次不触/回合重置清计数/ResetState 清锁/静止满回合旋转不误触/顺序+阵营都入哈希且哈希无副作用/事件只在阈值触发)+ 1 个集成验收(lethal infinite test deck 必须触发检测器——循环本体无界,尽管自然对局 17 揭晓击杀)。
- TDD:RED 已验证(测试先写,csc 离线编译报 CS0246 类型不存在);GREEN 已于 2026-09-19 10:10 经 Unity 真实编译器验证通过(Editor.log 零 error,新文件零 warning)。**剩 EditMode 测试运行**(Test Runner → ArrangementCycleDetectorTests,7 个)——实现落盘时编辑器在 Play 模式,编译推迟到退出后已自动完成,测试运行交用户点击。
- 盲区备忘(沿用 §3):周期跨回合边界(依赖洗牌参与循环)的组合检测不到——重洗打破排列;增长型靠合池上限 + L0。属已知接受的盲区。
- **验收调查(2026-09-19,CycDiag 取证)**:lethal 样本在运行时检测器下**不触发**——根因 = 该引擎周期恰好 = 一整回合(回合 1/3/4/6 采样哈希逐位相同),排列重复点全部落在回合边界,被回合重置精确擦除;非实现 bug,是「同回合内第 3 次出现」规则的信息极限。**用户拍板(方案 A)**:运行时检测器只保卡死型(回合内循环);整周期引擎(lethal 型)归 P2 RunBudgetSim 判(无限血木桩下击杀不落、循环无界)。运行时验收标本改用 non-lethal 样本(回合饿死形态,全局长在单回合内,回合内重复可抓);整周期模式由单测 `RoundPeriodicArrangement_DoesNotTripWithinRoundRule` 钉为规格行为。sim 侧将来可安全启用跨回合规则(误报代价为零)。另确认:洗牌吃推进式 RngChannel.Deck,安静卡组跨回合排列不重复,故跨回合规则在小卡组(<~12 张,洗牌空间不足)有误报面——sim 内使用亦需注意。
- **尺寸修正(2026-09-19 用户质询后澄清)**:「整周期盲区」部分是 5 张木桩的 artifact——lethal 置顶轴不压起始卡,回合照常每 ~卡组张数 次揭晓推进;生产合池 30-60 张时单回合长度 ≫ 该引擎 17 揭晓的击杀,**整个循环发生在单回合内,运行时检测器可抓**。真正盲区仅「引擎周期 > 一个真实回合长度」的慢引擎(deck-dependent,击杀速度随诅咒密度变化)。lethal 验收保持归 P2 sim(木桩尺寸下无法演示),non-lethal 饿形态保留为运行时集成验收。

### 交接状态(2026-09-19,给接手会话)

> **本节的「待办(按序)」已被 §15 取代(2026-09-19 接手会话)**:待办 1 已完成(测试已跑,7/8 绿);
> 待办 2 的「删埋点」现应等到 §15 的验收重做之后;待办 3 的答案见 §15 根因分析。以下为历史记录,保留不删。

**已完成(本会话)**:
- `CombatArrangementCycleDetector.cs`(新)+ CombatManager 5 处挂钩(Awake 自动创建/:264 ResetState/:1206 NotifyRoundStart/两条 confirm 路径 NotifyRevealBoundary)+ TestManager InferCategory 路由登记 + `ArrangementCycleDetectorTests.cs`(新,8 测:6 单测 + 整周期规格钉住 + non-lethal 集成验收)。
- RED 验证(csc CS0246)→ GREEN 验证(Unity 真实编译,Editor.log 零 error,2026-09-19 10:10)。TDD 流程完整。

**待办(按序)**:
1. **用户在 Test Runner 重跑 `ArrangementCycleDetectorTests`**。若本会话观察器已死:直接 `grep CycDiag "C:/Users/Papaya/AppData/Local/Unity/Editor/Editor.log"` 读新序列。
2. 全绿后:删除 `RunDriverWithDetector` 里的 `[CycDiag]` 临时埋点块(注释标了 temporary;`LastSampledHash` 属性可保留);跑全量 EditMode 确认无回归;删除 registry claim(若还在)。
3. non-lethal 若不触发:读 CycDiag 序列区分「不重复」vs「重复晚于击杀」,再按 §14 分析定(补 tweak 或运行时验收改纯构造单测)。
4. 提交建议:检测器+测试+两处挂钩一个 commit(`feat(infinity): arrangement-cycle detector`),plan 文档单独 commit。**勿带入**下列用户自己的改动。

**本会话文件归属**(工作区混杂,提交前核对):
- 本任务:`Assets/Scripts/Managers/CombatArrangementCycleDetector.cs`(.meta 同)、`Assets/Scripts/Editor/Tests/ArrangementCycleDetectorTests.cs`(.meta 同)、`Assets/Scripts/Managers/TestManager.cs`(仅 InferCategory 一行)、`plans/plan-infinity-detection-2026-09-17.md`。`CombatManager.cs` 是本任务 5 处挂钩 + 可能混有用户改动,commit 前 `git diff` 分辨。
- **用户自己的未提交改动(勿动勿提交)**:Fatigue.prefab(AttackSelf 改造,审查结论见 §5)、GameScene.unity、PlayerDeckRef.asset、ServerConfig.asset、Fonts/Shop UI 一批(ShopTopBarLayout 等)。

**环境备忘**:
- Unity MCP 在接手会话大概率不可用(select_tools 4 个候选名全 unknown)——替代路线已验证:Editor.log(`C:/Users/Papaya/AppData/Local/Unity/Editor/Editor.log`)+ 后台观察器轮询;Play 模式下编译推迟,观察器等程序集重建信号。
- 编辑器 Play 模式期间勿用 GUI 自动化操作(§13 事故史;用户场景有未保存改动)。

## 15. 接手会话复核:harness 保真度修正(2026-09-19)

**结论:lethal 样本是回合内循环,运行时检测器可抓;§14 的「整周期」判断是测试脚手架 artifact,不是引擎事实。**

### 15.1 根因:headless 脚手架不还原真实触发接线

- `HeadlessCombatTestFixture` 的 `GameEventStorage` 每个事件字段都是 `CreateScriptableObject<GameEvent>()` 新建实例,`curseCardTypeID` 是新建 `StringSO`(`reset` 默认 true → `value` 为空)。预制体里的 listener 序列化指向的是**真实事件资产**,与夹具实例不是同一个对象,所以 `BridgeCard` 只能把**每张卡的每个 listener 一律改指 `onMeRevealed`**("listener re-point & registration")。
- 生产 `GameScene.unity` 的真实接线(本节已逐字段提取核对):
  - `onMeRevealed` → `Assets/SORefs/GameEvents/REVEAL/OnMeRevealed.asset`
  - `onEnemyCurseCardRevealed` → `Assets/SORefs/GameEvents/REVEAL/OnHostileCurseRevealed.asset`
  - `curseCardTypeID` → JU_ON CardTypeSO(guid `07a2aa37…`)
- 后果(两块,都是本 combo 的关键路径):
  1. `TriggerRevealedCard()` 的 `onEnemyCurseCardRevealed.RaiseOwner()` 分支因 `curseCardTypeID` 为空**永不触发**;
  2. **RELIC_CURSE_REVIVAL(耳语唤尸)的真实触发是 `OnHostileCurseRevealed`**(预制体验证),脚手架收不到 → 它退化成「耳语唤尸**自己揭晓**时复活友方」。CURSE_GARDENER 的真实触发恰是 `OnMeRevealed`,所以它没被扭曲——一个 combo 里只有一半的触发是对的。

### 15.2 实测证据(临时诊断 3 变体,结束即删)

同一 lethal deck(player = `lethal infinite test`;enemy = N×JU_ON),驱动复用现有 driver 原语;唯一变量是接线与木桩尺寸:

| 变体 | 接线 | 回合推进 | 检测器 | 敌方死亡 |
|---|---|---|---|---|
| A-current2 | 现状脚手架(N=2) | 每 ~5 揭晓推进一次(round 1→8) | **不触发** | i=32 |
| B-faithful2 | 生产接线(N=2) | **全程 round=1,从不推进** | **i=7 触发** | i=25 |
| C-faithful20 | 生产接线(N=20,deck=23) | **全程 round=1** | **i=18 触发** | i=66 |

B/C 的排列在同一回合内只在 4 / 15 个不同排列间打转,单个排列一个回合内重复 **149 / 144 次** —— 教科书式紧回合内循环,与用户描述完全一致(养蛊人复活诅咒 → 诅咒揭晓触发耳语唤尸 → 复活养蛊人 → 无限重复;`hex 1` 每圈 `EnhanceCurse` 强化诅咒,至击杀自终止)。A 变体的「回合照常推进 + 不触发」正是 §14 观测到的现象,现已证明是接线 artifact。

### 15.3 修正记录

- §14「验收调查」的「引擎周期恰好 = 一整回合」与「尺寸修正」的「lethal 置顶轴不压起始卡,回合照常推进」**均作废**——两者都建立在 A 变体之上。
- 真实形态:lethal 与 non-lethal **同为回合饿死形态**,差别只在 lethal 自带击杀泵(强化诅咒 → 自噬击杀)。方案 A(「运行时只保卡死型、lethal 归 P2 sim」)的前提因此不成立。
- 单测 `RoundPeriodicArrangement_DoesNotTripWithinRoundRule` 钉住的是 artifact,**不应作为规格行为**;`ArrangementCycleDetectorTests` 类注释里同源的「division of labor」描述也需同步。
- 运行时验收标本可以回到 lethal(即 §4 原定验收标准:该 deck 必须被检测器命中,尽管其自然对局自终止)——但**必须用生产接线**,否则测的不是同一个循环。
- **检测器实现本身无 bug**,`CombatArrangementCycleDetector.cs` 无需改动。

### 15.4 已交付(2026-09-19,用户授权「修改代码」后;改动全部落在 `ArrangementCycleDetectorTests.cs`)

1. **触发保真度**:`BridgeCard` 不再一律改指 `onMeRevealed`,改为 `MapRealEventToFixtureEvent` —— 按 listener 序列化所指向的**真实事件资产名**映射到夹具的事件实例(`OnHostileCurseRevealed → onEnemyCurseCardRevealed` 等);未登记的名字回落 `onMeRevealed`,保持 combo 外卡片的旧行为。新增 `EnableProductionTriggerWiring()` 设 `curseCardTypeID.value = "JU_ON"`(与 `Assets/SORefs/CombatRefs/CurseCardTypeID.asset` 及场景接线一致),须在 `GatherDecks` 前调用。
2. **driver 终止条件改为逻辑 HP**(`enemyPlayerStatusRef.hp <= 0 || ownerPlayerStatusRef.hp <= 0`),弃用 `IsDeathVisuallyLanded`:夹具挂了 dummy `CombatInfoDisplayer`,而 `GetDisplayedEnemyHp()` 在有待提交伤害时返回**队列冻结值**,紧循环里击杀落地后该标志仍不置位(实测旧接线为 True、生产接线恒 False)。同时按 `InfiniteDeckTerminationTests` 的成熟做法接入 L0 guard(200/1500/60)+ 每揭晓反射补调 `CheckFatigueByRevealCount` + force-clear 跳过,并启用按揭晓数疲劳(`PrepareOvertimeFatigue`,复用生产值)。
3. **验收标本**:新增 `LethalInfiniteDeck_TripsCycleDetector`(生产接线下 lethal 必须触发检测器 + 泵击杀 + L0 未出手);`NonLethalInfiniteDeck_TripsCycleDetector` 保留并修好终止与断言;原 `RoundPeriodicArrangement_DoesNotTripWithinRoundRule` 更名 `CrossRoundPeriodicArrangement_DoesNotTrip`,注释改为「纯规则边界单测,**不**建模 lethal 样本」。
4. `[CycDiag]` 临时埋点已删;测试类头注释同步改写(去掉「ruling A / 分工」的错误叙述,写明两个标本同为回合饿死形态)。
5. **未动**:`HeadlessCombatTestFixture`(改共享夹具会波及他人测试)、`CombatArrangementCycleDetector.cs`(实现无 bug)。`InfiniteDeckTerminationTests.cs` 的接线统一见 §15.6。

实测(2026-09-19 11:4x,测试内 `[CycleDetector]` 汇总行):

| 标本 | iters | reveals | 末回合 | 检测器 | 结局 |
|---|---|---|---|---|---|
| lethal(2 诅咒) | 26 | 25 | **1** | 触发 | eHp=0 / oHp=30,L0 未出手 |
| non-lethal(2 诅咒) | 250 | 249 | 3(严重饿死) | 触发 | eHp=0 / oHp=29,疲劳卡已入堆,L0 未出手 |

- `ArrangementCycleDetectorTests`:**9/9 绿**(0.9s)。
- 全量 EditMode:**577 total / 1 失败** —— 仅 `ShopSectionPanelsTests.ComputeContentBounds_SingleCenter_ReturnsCenteredBounds`(并行 shop 任务的未提交改动,非本任务回归;改动前的基线同样红)。
- 提交待定:工作区混有用户与并行会话的未提交改动,按 §14「勿带入」的约束,commit 需先与用户确认范围。

### 15.5 本轮环境与基线(2026-09-19 11:0x–11:3x UTC+8)

- **Unity MCP 可用**(`http://127.0.0.1:8080/mcp`,`OneDeck@033fe4d4cdd447fb`,Unity 6000.3.9f1;由上一会话的 MCP for Unity 窗口启动)。本会话经 HTTP 直连调用 MCP 工具(会话内无原生 unity 工具绑定),`run_tests` / `read_console` / `refresh_unity` 均正常。
- 注意:`read_console` **带 filter** 在大 console 下极易超 2.0s ping 预算(反复失败),建议诊断输出落盘(`Application.dataPath + "/../…"`)再读文件——已验证稳定。
- `ArrangementCycleDetectorTests`:**8 测 / 7 绿 / 1 红**(红 = `NonLethalInfiniteDeck_TripsCycleDetector`,driver 不终止;检测器本身在 non-lethal 上**确实触发**,周期 2,round 恒为 1)。
- 全量 EditMode:**579 total / 2 失败** —— ① 本任务 non-lethal;② `ShopSectionPanelsTests.ComputeContentBounds_SingleCenter_ReturnsCenteredBounds`(属并行 shop 任务的未提交改动,非本任务回归)。
- 编辑器期间出现一次 Unity「Hold on」模态(强同步重编译进度框),**自行消失**,本会话未做任何弹窗点击(未触碰用户未保存的 GameScene)。

### 15.6 后续:termination 测试统一到生产接线(2026-09-19,用户授权「修改代码」)

- `InfiniteDeckTerminationTests.cs` 已从旧接线迁到生产接线:新增 `EnableProductionTriggerWiring()`(设 `curseCardTypeID.value = "JU_ON"`,须在 `GatherDecks` 前调用)与 `MapRealEventToFixtureEvent`(按 listener 序列化的**真实资产名**映射),`BridgeCard` 不再一律改指 `onMeRevealed`;driver 终止条件由 `IsDeathVisuallyLanded` 改为**逻辑 HP**(同 §15.4 点 2 的根因);文件头与 `BridgeCard` 注释同步更正。
- **影响面实测**(全样本卡触发事件扫描):四个卡里只有 `RELIC_CURSE_REVIVAL`(OnHostileCurseRevealed)会被旧桥改写;`GRAVE_HEXER` / `SPIRIT_CALLER` / `JU_ON` / `Fatigue` 全是 OnMeRevealed,`StartCard` 无 listener。所以 **non-lethal 那个测试行为不变**,lethal 那个现在跑的才是真循环。
- 注意:lethal 测试**只换接线不换终止条件会直接变红**——它的 `IsDeathVisuallyLanded` 只在旧接线下置位(dummy `CombatInfoDisplayer` 的队列冻结值),换接线后会跑满 5000 上限。两处必须同改。
- 复验:`InfiniteDeckTerminationTests` + `ArrangementCycleDetectorTests` 共 **11/11 绿**;全量 EditMode **577 total / 1 失败**,仍仅为 shop 任务的 `ShopSectionPanelsTests`(与本任务无关)。
- 遗留:`MapRealEventToFixtureEvent` 目前在两个测试类各持一份(沿用「BridgeCard 由各测试文件自持」的既有惯例,未动共享夹具)。若后续还要第三个文件用,可考虑提到 `HeadlessCombatTestFixture` 作 protected 助手。
- 另记一次环境事件:全量套件某次启动报 `Test job failed to initialize (tests did not start within timeout)`(默认 15s init 窗口),重试时把 `run_tests.init_timeout` 提到 120000 后正常跑完 577 项;期间编辑器一度 `ready_for_tools=false`(`stale_status`),非模态阻塞。

## 16. P1b:RunBudgetSim headless 化(2026-09-19)

§2 把 `RunBudgetSim(deckA, deckB|木桩, seed) -> BudgetTripReport` 定为核心抽象:P2 归因、P5 回扫、上传前预检三处共用一份实现。P1 之前只落了检测器,这份抽象一直缺失——本节点补齐。

### 16.1 为什么必须搬出测试程序集

`HeadlessCombatTestFixture` 住在 `Assets/Scripts/Editor/Tests/`,非测试代码(批扫工具、将来的 sim 门)根本无法复用;而「给两个 deck 跑一局并判定成环」正是 P2/P5 都要用的能力。项目**没有 asmdef**,所以 `Assets/Scripts/Editor/**` 整体落在 `Assembly-CSharp-Editor`:把 sim 放进这个非 Tests 目录即可被 batch `-executeMethod` 调用,同时仍然拿得到 `SerializedObject`(UnityEvent callState 翻转)与 `AssetDatabase`(预制体加载)这两个 **editor-only** API——而所有预期消费者本来就都跑在编辑器 / batchmode 里。

### 16.2 交付物

| 文件 | 职责 |
|---|---|
| `Assets/Scripts/Editor/Headless/HeadlessCombatRig.cs` | 非测试版 headless 战斗环境:单例装配 / 拆卸(镜像 fixture)、卡与 DeckSO 工厂、揭晓原语、**生产接线 bridge**、`SetCombatSeed`、`CheckFatigueByRevealCount` |
| `Assets/Scripts/Editor/Headless/BudgetTripReport.cs` | 报告数据(`[Serializable]`,P5 可直接 `JsonUtility` 序列化);两条独立判据见下 |
| `Assets/Scripts/Editor/Headless/RunBudgetSim.cs` | `Run(deckA, deckB, seed)` / `RunVsDummy(deckA, size, hp, seed)` / `BuildDummyDeck`;Options(生产默认值);同步揭晓循环 |
| `Assets/Scripts/Editor/Tests/RunBudgetSimTests.cs` | 两标本回归 + **固定 seed 可复现** + 木桩隔离形态 |

### 16.3 两条判据刻意分开(对齐 §3/§4/§15)

报告同时给出两个互不推导的字段:

- `SuspectedInfinite` = 排列周期检测器触发 = **flag 判据**(无界递归本体,§3 主判据)。
- `BudgetCapConcluded` = L0 熔断出手 = **玩家可感伤害**("打不完"),是预算/遥测信号,**不是** flag 判据。

这把 §3「预算启发降级为辅助、循环本体才是主判据」和 §15 的更正落实成了类型。`NeedsAttribution` = 两者取或,供 P2 归因筛选。

### 16.4 确定化走生产路径

`GatherDecks` 自己会 seed(`CombatManager.cs:321`:`overrideSeed != 0 ? overrideSeed : Rng.ComputeCombatSeed(session)` → `Rng.InitCombat`),所以 rig **建了一个 `TestManager` 并把 seed 写进 `overrideCombatSeed`**,而不是绕过它自己去调 `Rng.InitCombat`——即 `-odseed N` 的同一条路。这是 P2「原配对重放」和「敌 deck vs 木桩」能站得住的前提。注意 `Rng.RunSeed` 是 run 级随机、`Setup` 通道由它派生,而 headless func 不经过商店/选牌,故单局复现只需 combat seed。

### 16.5 已验 / 待验

- **已验(离线编译)**:`dotnet build Assembly-CSharp-Editor.csproj` → **0 error / 18 warning(全部在无关文件)**。方法:把 4 个新文件临时加进 Unity 生成的 csproj(该文件在 `.gitignore` 内且 Unity 会重新生成)。**MCP 不可用时的替代验证路径,记于此。**
- **已验(EditMode)**:`RunBudgetSimTests` **4/4 绿**(2.06s)——两块标本 + 固定 seed 可复现 + 木桩隔离。
  - lethal 标本实测:`lethal infinite test vs curse-stub(2xJU_ON) seed=4242 infinite=YES [cycle hash=efa31007 repeats=9] reveals=19 rounds=1 iters=20 deck=5 oHp=30 eHp=0 cascadePeak=2`。整局 19 次揭晓全在第 1 回合,检测器第 3 次同排列即触发,L0 熔断未出手。
  - 控制台同时可见确定的**生产接线**在跑:`[Seed] combat=0 seed=4242 (override)`(即 `overrideCombatSeed` → `Rng.InitCombat` 这条生产路径),以及用户描述的原始环:CURSE_GARDENER「hex 1」→「revive enemy curse」→ JU_ON 揭晓 → RELIC_CURSE_REVIVAL「enemy curse revealed revive 1」→ ReviveBatch 回顶 → 回到 CURSE_GARDENER。
- **已验(全量 EditMode)**:**581 total / 580 绿 / 0 失败 / 1 既有 Ignore**。顺带确认:`ShopSectionPanelsTests.ComputeContentBounds_SingleCenter_ReturnsCenteredBounds` 已由并行 shop 任务修掉,本任务的三批改动之外无回归。
- **本轮额外修复(§15.6 的连带,非 P1b 引入)**:跑全量时 `InfiniteDeckTerminationTests.NonLethalInfiniteDeck_FatigueConverges` 红了(`rounds must have crossed the overtime threshold — expected >2, got 2`,连跑 3 次稳定复现)。根因两层:①§15.6 把 driver 终止条件换成逻辑 HP 后,敌方在**第 2 回合**就死,而 `CheckFatigueNAddFatigue` 的判据是 `roundNum > overtimeRoundThreshold`(=2)——第 3 回合的回合钟疲劳永远没机会触发;旧断言之所以一直绿,是靠旧终止条件(显示层滞后)多攒出来的回合数。②该测试**没有固定 seed**,而 fixture 不建 `TestManager`,于是 `GatherDecks` 走 `Rng.ComputeCombatSeed` ← `Rng.RunSeed` ← `UnityEngine.Random`,同一份代码在不同编辑器会话里跑出不同战局(实测:重启前绿、重启后稳定红)。处置:给该类 `SetUp/TearDown` 增加固定 seed(`PinnedCombatSeed`,走 `TestManager.overrideCombatSeed` 即 `-odseed N` 的同一条路,并在 TearDown 清 `TestManager.Me` 防泄漏),同时把回合钟断言换成**按揭晓数钟**断言(§13 自己认定的「该类循环唯一有效的疲劳计量器」)。产品代码零改动。
- **待验**:无。
- 木桩用**无效果普通卡**而非 §3 字面的「中立起手卡 × N」:`isStartCard` 是驱动的回合边界标志,拿它当木桩会让每次木桩揭晓都变成回合边界,故改用无容器的惰性卡,同样无效果但不劫持边界。

### 16.6 与既有测试的关系

`RunBudgetSimTests` 与 `ArrangementCycleDetectorTests` / `InfiniteDeckTerminationTests` 跑**同一批标本**,但走的是两套独立实现(fixture 驱动 vs rig 驱动)。两份实现互相印证是刻意的——不要为了消重把其中一份删掉;要统一的话,留一份当另一份的对照。`MapRealEventToFixtureEvent` 目前三份复制(两个测试类 + rig),沿用「bridge 由各文件自持」的既有惯例,未动共享夹具。

### 16.7 运维备注(本轮踩到的)

- `run_tests` 若报 `Test job failed to initialize` 或反复起不来,先查 `mcpforunity://editor/state` 的 `tests.current_job_id`:域重载会留下**孤儿 job** 并阻塞后续所有运行,用 `run_tests` 带 `clear_stuck: true` 清掉即可(本轮实测有效)。
- 编辑器重启后 MCP bridge 需要一段时间才重新注册;期间 `resources/read` 无响应容易被误判为"服务已死",先查 8080 是否 LISTENING 再下结论。
- **跑 Test Runner 前必须先保存 scene**:scene 脏(`*` 标记)时 runner 启动会弹「Scene(s) Have Been Modified」模态框,阻塞主线程 → job 报 `failed to initialize (tests did not start within timeout)` 或直接挂起,而 `editor/state` 仍然显示 `ready_for_tools: true`、窗口标题也不变,极易误判成 runner/bridge 坏了。本轮实测多次命中(用户也反馈"遇到很多次")。规范已写入 `AGENTS.md` 的 Agent Post-Mortem Notes;`GameScene.unity` 带用户未提交改动,**严禁自动关掉该弹窗**。

## 17. 核查:诅咒数量与标本木桩形状(2026-09-19,用户质询后)

用户指出两点,源码核查 + 实测结论如下。

### 17.1 源码事实

- **`CurseEffect.EnhanceCurse(amount)` 在没有目标诅咒时会生成一张**:先 `FindEnemyCardWithTypeID(cardTypeID.value)`(扫 `combinedDeckZone` + `revealZone`,跳过中立卡);找不到就 `CreateEnemyCard(cardPrefab)` → `CombatFuncs.AddCard_TargetSpecific` → `CardFactory.SpawnCardForPlayer(..., deckIndex: 0)`,然后给这张新卡加攻击。**找到时只强化找到的那一张**(首个匹配),不会生成第二张。
- **「至多一张 JU_ON」是涌现性质,不是显式约束**:全库没有任何咒语唯一性守卫(`AddCard_TargetSpecific` 只做 `SpawnCardForPlayer`,无去重)。游戏内能造诅咒的路径只有 `CurseEffect.EnhanceCurse` / `EnhanceFriendlyCurse`(**仅当一张都没有时才生成**)与 `ReviveEffect`(把已有的移回)。所以任意时刻 ≤1 张,但这是这些效果的性质,不是引擎不变量。
- 因此**「敌方预置 2×JU_ON」是游戏不可达状态**——这是历史测试脚手架的产物,不是设计。

### 17.2 实测:木桩形状对结论无影响

`TempCurseCountDiag`(临时,已删;源码留 `.agent_tmp/TempCurseCountDiag.cs.saved`),seed 4242,同一 deck 换三种敌方:

| deck | 敌方 | infinite | reveals | rounds | repeats | eHp | L0 |
|---|---|---|---|---|---|---|---|
| lethal | 0 诅咒(惰性木桩) | True | 17 | 1 | 8 | 0 | False |
| lethal | 1×JU_ON | True | 18 | 1 | 9 | 0 | False |
| lethal | 2×JU_ON(现状) | True | 19 | 1 | 9 | 0 | False |
| non-lethal | 0 诅咒 | True | 255 | 3 | 100 | 0 | False |
| non-lethal | 1×JU_ON | True | 249 | 3 | 99 | 0 | False |
| non-lethal | 2×JU_ON(现状) | True | 257 | 3 | 100 | 0 | False |

**结论:三者判据完全一致**(都 `infinite=True`、同回合数、都无 L0、都被击杀自终止),只有揭晓数差 1–8 次。所以两个标本**不是因为木桩形状才过的**,现有结论成立。

### 17.3 由此作废的一条旧说法

`InfiniteDeckTerminationTests.CreateCurseStubDeck` 的注释写着「without curses in the enemy deck the "add curse" leg fizzles forever and nothing churns」——**与源码不符**,`EnhanceCurse` 会自己生成第一张。0 诅咒那一行实测就是反证(17 揭晓、rounds=1、检定触发、击杀)。这也与 §3 的木桩定义(敌方 = 无效果卡 + 大血量)一致:combo 自带点火,木桩不需要喂诅咒。

### 17.4 建议(待「修改代码」)

把四处 `CreateCurseStubDeck()` 的 2×JU_ON 换成**不预置诅咒**(惰性木桩,或 1×JU_ON),并删掉那条错误注释。这是**保真度清理**:让测试只跑游戏可达的状态。判据不变,实测已证。涉及 `ArrangementCycleDetectorTests` / `InfiniteDeckTerminationTests` / `RunBudgetSimTests`。

### 17.5 附带修掉的自身缺陷

`HeadlessCombatRig` 的共享 dummy UI 用 `new GameObject` 建在**活动场景**里且不受 `Dispose` 管,跑完会在场景里留孤儿 → `GameScene` 变脏 → **下一次 Test Runner 运行弹保存框**(即用户反复遇到的那个坑)。已补 `HeadlessCombatRig.DestroySharedResources()`,并在 `RunBudgetSimTests.OneTimeTearDown` 调用;`AGENTS.md` 与记忆里的条目也据此补了「测试自身会弄脏场景」这一层。

## 18. 验证可见性缺口:检测器在生产配置下是隐形的(2026-09-19,用户验证 step 4 时发现)

用户在 Play 模式按 §16 的验证步骤跑端到端,**看不到 trip 日志**。核查结论:

- 日志确实**受 TestManager 分类开关管理**:`TestManager.InferCategory` 把 `[CombatArrangementCycleDetector]` 路由到 `LogCategory.CombatFlow`(TestManager.cs:363-366),`IsEnabled` 再读 `Me.logCombatFlow`(字段声明默认 `true`)。
- **但 `GameScene.unity` 里 TestManager 的日志开关序列化值全是 `0`**:`logCombatFlow` / `logEffectChains` / `logAnimationPlayback` / `logVisualSync` / `logEditorTools` / `logTestManager` / `logDynamicDamageDisplay` / `logStatusEffectDisplay` / `logDamageFloater` / `logShopFlow` / `logUncategorized` 全 0。所以 Play 模式下 `LogInternal` 直接 `return`,日志被静默丢弃。组件挂在场景内名为 `TestManager` 的对象上(单实例,active)。
- 验证时的开启方式:Hierarchy 选 `TestManager` 对象 → Inspector 勾 **`logCombatFlow`** → 进 Play → Console 过滤 `Arrangement cycle tripped`。顺带:场景里 `autoReveal = 1`,战斗会自己跑完,不需要手点。

**更值得注意的**:`Tripped` / `TripCount` / `LastTripHash` 都是**属性**(`get; private set;`),而 Unity Inspector 只显示字段,所以运行时**没有**任何别的可视化途径——trip 唯一的输出就是这条被开关拦掉的日志。也就是说:**按当前生产配置,检测器即使触发了也无人知晓**。这与「我们已经有检测能力」是两回事;P2 接上 `OnCycleTripped` 之前,这个缺口一直在。

引擎事件要让玩家/开发者看见,库里已有的范式是 `CombatLog.me?.Append(...)` + `GameColorPalette`(疲劳就是这么播报的:`CheckFatigueNAddFatigue` 里 `CombatLog.me?.Append(疲劳提示)`),检测器没走这条。是否该让 trip 进入战斗内可见日志是**设计决定**(玩家该不该看到「检测到无限递归」?),留给后续拍板;但至少「能检出」与「看得见」要分开记账。

### 18.1 已加专属开关(2026-09-19,用户要求)

用户验证 step 4 时先靠 `logCombatFlow` 看到了 trip 日志,但那会把 `[CombatManager]` / `[PhaseManager]` / `[CombatBudgetGuard]` 一起放出来。已给检测器单开一类:

- `TestManager.LogCategory` 新增 `InfinityDetection`;新增字段 `public bool logInfinityDetection = true`(带 Tooltip);`IsEnabled` 加对应 case;`InferCategory` 把 `[CombatArrangementCycleDetector]` 从 CombatFlow 分支里**独立出来**单独返回 `InfinityDetection`。
- 影响面核查:`LogCategory` 仅在 `TestManager` 内部使用(无序列化依赖、无 int 索引、无外部引用),新增成员是纯增量的;改动 = 日志路由,**无游戏行为变化**。
- **实测新字段在既有场景实例上的取值**:`logInfinityDetection=True | logCombatFlow=True | logEffectChains=False`。即新字段取的是**字段初始化值 `true`**——Unity 对序列化数据里缺失的字段沿用初始值(场景尚未保存该字段故未覆盖)。所以 trip 日志现在**默认可见**,且可以把 `logCombatFlow` 关回去而不失去它。
- 验证:离线 `dotnet build` 0 error;全量 EditMode **581 total / 580 绿 / 0 失败 / 1 既有 Ignore**。

两处**刻意没动**:

1. `[CombatBudgetGuard]` 留在 CombatFlow——它的 `FORCE CONCLUDE COMBAT` 也能由非无限原因(如正常长局触到 `maxRounds`)触发,语义比"无限检测"宽。
2. trip 是否要进**战斗内可见日志**:**该路线已作废**——用户 2026-09-19 明确「战斗内日志不显示给玩家了,已经淘汰」。所以 trip 目前唯一可见面就是上面的 TestManager 开关(开发者向);玩家侧要不要感知「检测到无限递归」,在 P2 接上 `OnCycleTripped` 时需重新设计出口,不能再往 `CombatLog` 上挂。
