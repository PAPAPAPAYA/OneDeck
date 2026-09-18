# Plan: 无限检测与防下发(Infinity Detection and Serving Gate)

- 日期: 2026-09-17
- 状态: 方案评审稿;2026-09-17 已落地 L0 全部硬终止 + 置顶墓区排除 + 疲劳复活通配(5a09ebb / 34075e5 / 62f7222,EditMode 559 绿,Play 待验),P1 起按 §10 分期等「修改代码」开工
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

判据原则:不证明无限,只判「打不完」(预算超时)。触发任一 = 疑似 → 强制终局 → 归因。

| 信号 | 阈值(待标定) | 抓什么 |
|---|---|---|
| 单回合 reveal 数 | > K × 回合开始合池大小(K≈3) | 置顶/苏醒回环(回合内不结束) |
| 总回合数 | > 上限(≈30) | 跨回合增长型 |
| chainDepth 触顶事件 | EffectChainManager 现有 | 单链深递归 |
| 物理嵌套深度闸(P0 新增) | 见 §5 | 09-13 型换链级联 |
| 合池总量 | > 上限 | token 增殖型 |
| 状态 digest 重复 | 排除单调计数器后全状态哈希重复 | 真·严格无限(提前退出 + 循环节证据) |

- 复现基建现成:RngService 四通道确定化、TestManager.overrideCombatSeed / -odseed、DeterminismDigest(RngService.cs)。
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

## 9. 结果语义(待拍板)

- 敌责:判玩家胜,还是无效局不扣?
- 我责:判负,牌不封(已定);
- 配对责:无效局 + 记录;
- L0 硬上限触顶时的残局判定规则(胜负如何结算;2026-09-17 拍板延后,P0 先用占位,见 §7.7)。

## 10. 实施分期

| 期 | 内容 | 验收 |
|---|---|---|
| P0 | ✅ 已落地(5a09ebb):揭晓强制结清 + 全局闸 + 被动埋点;连坐每回合上限已砍 | 09-13 复现局必终止 → 待 Play 复现验证 |
| P1 | RunBudgetSim headless 化 + L1 信号接入 | 09-13 复现局被信号命中 |
| P2 | 归因三连 + ddmin 最小化 + loop_reports 上报 | 标本提取出带角色三卡最小集 |
| P3 | 服务端 flag / 证据表 / 出队过滤 / 三来源统一 / admin | 被 flag 的 deck 不再下发 |
| P4 | 组合库表 + 匹配交集检查 + 复核流 | 含标本组合的任意 deck 在匹配时被滤除 |
| P5 | 存量回扫批工具(服务器全量 + 本地 RecordedDecks) | 回扫报告落盘 |

Edit-mode headless 体系复用 HeadlessCombatTestFixture / NullCombatVisuals,不进 Play Mode。

## 11. 待拍板清单

1. 阈值标定:K、回合上限、池上限(前置 = 被动埋点采数,再拿 09-13 复现局与正常局夹逼);
2. 敌责与硬上限触顶的结果语义(占位先按 §7.7,后拍板);
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
