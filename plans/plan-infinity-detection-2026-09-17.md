# Plan: 无限检测与防下发(Infinity Detection and Serving Gate)

- 日期: 2026-09-17
- 状态: 方案评审稿;2026-09-17 已落地 L0 全部硬终止 + 置顶墓区排除 + 疲劳复活通配(5a09ebb / 34075e5 / 62f7222,EditMode 559 绿,Play 2026-09-18 用户已验),2026-09-18 §9 结果语义拍板 + 检测方向改拍(排列周期 = 无限递归主判据,预算启发降级;配对责不 flag)。**2026-09-19 三批收尾**:①§15 复核更正——lethal 样本实为回合内循环,§14「整周期」结论作废(脚手架触发接线 artifact),运行时验收按生产接线重做;②§15.6 `InfiniteDeckTerminationTests` 统一到生产接线;③§16 P1b `RunBudgetSim` headless 化补齐(离线编译 0 error,EditMode 4/4 已绿)。P1 至此完成。**2026-09-19 P2 客户端核心落地并收口**(§19;76a3099 / 19b7afb / cddc1e4):归因三连 + ddmin 最小化 + 组合条目 + 延迟归因管线(trip 只记证据,出战斗后归因);loop_reports 落表/上报按 §19.6.1 归 P3;遗留 `ProcessPending` 生产触发时机(§19.6.2)。下一期 P3(§8 服务端);**2026-09-19 P3 设计落定(§20):四项拍板 = flag 粒度取内容指纹 / 一次报告即 flag / 服务端出队过滤 + 客户端缓存清理 / node --test 验收;§8「三来源」更正为两来源**;**2026-09-19 P3 已实现并验证(§20.7)**:服务端 flag 列 + `loop_reports` / `flagged_fingerprints` / `POST /api/loop-reports` / 出队过滤 / admin 解封,客户端 journal 记 ghost deckId + 缓存 purge + `LoopReportUploader`;`npm test` 9/9、全量 EditMode 600 total/599 绿/0 失败/1 既有 Ignore、线上库副本迁移实测通过。遗留:打包内无 verdict(保护链依赖 P5)、`ProcessPendingAndUpload` 生产触发时机(§19.6.2)、客户端↔服务端 E2E 未跑。**2026-09-19 P4 已实现(§21)**:组合库 + 入库门槛 + 匹配时多重集包含过滤 + `blockedCombos`;服务端 `npm test` 17/17。**2026-09-19 P5 已实现并首次写入生产(§23/§23.6)**:只读 dump + 无头扫描 + 默认干跑的上报器(触发定为「随卡改动/发版跑」,§11.4 结案);**判定口径修正后全量 109 行实扫 → 16 副无限 / 13 副已证 → 上报为 6 个组合键 + 14 行 deck flag**(线上 `active_combos 0→6`),未证 4 副(5/55/6/91)留报告;扫描器身份 `onedeck-scan`。**当前状态快照与操作手册见 §24**
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
  - tripSignals / reproSeeds / ~~evidenceRef~~(实现落为 `LoopReport.tripSignal` + `liveTripSignal` 两字段承载证据,不单列 evidenceRef;§19.1)
  - status: candidate → active(admin 复核后)/ retired(卡改动后须复验)
    - **落地状态(2026-09-19,§21)**:组合库 = 服务端 `combos` 表;入库**自动**(sim 门槛:1-最小 + 多 seed 稳健 + 未截断,§7.6);`retired` 由 admin 在面板上置位/恢复,退役后不再参与匹配扣留

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

> **2026-09-19 P3 已实现,本节按实现更新**(设计全文见 §20;下面是结论口径,不是初稿)。

- **敌人来源是两条,不是三条**(更正 2026-09-19):本地 json 回退 2026-09-04 已删,现行链 = `debug > server ghost > default pool`(`DeckSaver.PopulateEnemyDeckBySessionNumber`,DeckSaver.cs:366)。过滤面因此只有两类:**server ghost 由服务端出队过滤**(`GET /api/decks/opponents` 的 `randomDecks`/`randomDecksIncludeSelf` 加 `flag = 0`)+ **客户端清掉已缓存的旧 ghost**(响应带 `flaggedDeckIds`,`OpponentDeckCache.MergeResponse` 删除命中条目);**default pool 不过滤**——它是烤进客户端的开发者资产,不来自玩家,由 §5 的池层审计口径管。
- 服务端(server.js):`decks` 表 ensureColumn 加 `fingerprint` / `flag` / `flag_reason` / `flagged_at`;新增 `loop_reports` 证据表(deck_id, reporter_player_id, game_version, fingerprint, verdict, seed, signals, payload, created_at)+ `flagged_fingerprints` 内容键表;`ensureColumn` 模式现成(:193),decks 表 :73,出队过滤在 `randomDecks`。新增 `POST /api/loop-reports` 与 `POST /admin/decks/unflag`。
- **flag 键 = 卡组内容指纹**(cardTypeID 多重集哈希,非 deck_id、非玩家):每上传一次快照就新增一行,行级 flag 会被下一次上传绕过;指纹全局(不分版本)——同一副卡表在任何版本都是同一个环。等值匹配,P4 升级为子集匹配。
- 信任模型:playerId 即凭证(server.js:19 注释自认),客户端上报可伪造、可诬告——**2026-09-19 拍板:一次带 verdict 的报告即 flag**(§7.6 的自动化延续),admin 事后申诉/解封;每条 report 记 reporter,误报可追溯。verdict 缺省(纯证据)不 flag,留给 P5 离线确认。
- 可选挂点(未做):`OpponentDeckCache.Prefetch`(:128,每 session 预取 6 副)之后后台跑配对预算 sim,判定随缓存条目存。

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
| P2 | ✅ 客户端核心已落地(2026-09-19,§19;76a3099 / 19b7afb / cddc1e4):归因三连 + ddmin 最小化 + 组合条目 + 延迟归因管线;loop_reports 落表/上报按 §19.6.1 归 P3 | 最小集演示在 lethal 样本完成(09-13 三卡环已被 P0 修复拆除,§19.4);遗留:`ProcessPending` 生产触发时机(§19.6.2) |
| P3 | ✅ 已实现(2026-09-19,§20/§20.7):服务端 flag 列 + 内容指纹 + loop_reports/flagged_fingerprints 表 + POST /api/loop-reports + 出队过滤 + flaggedDeckIds + admin 解封;客户端 journal 记 ghost deckId + 缓存 purge + LoopReportUploader | 被 flag 的 deck 不再下发:服务端 `npm test` 9/9(含"报告后出队不再返回"与"重传自动 flag");客户端 EditMode 5/5 + 全量 600/599 绿 |
| P4 | ✅ 已实现(2026-09-19,§21):组合库表 + 入库门槛(1-最小/多 seed 稳健/未截断)+ 匹配时多重集包含过滤 + `blockedCombos` 客户端缓存清理 + admin retire/reactivate | 含标本组合的任意 deck 在匹配时被滤除:服务端 `npm test` 17/17(含"加料 deck 被扣""未证明不入库""退役恢复") |
| P5 | ✅ 已实现并投入生产(2026-09-19,§23/§23.6):`dump_decks.py` 只读 dump + `InfinityBatchScan`(sim + ddmin + 报告分档)+ `post_loop_reports.js`(默认干跑) | 回扫报告落盘**并已下发过滤生效**:全量 109 行实扫 → 16 副无限 / **13 副已证 → 上报为 6 个组合 + 14 行 deck flag**(线上 `active_combos 0→6`);未证 4 副(5/55/6/91)留报告 |

Edit-mode headless 体系复用 HeadlessCombatTestFixture / NullCombatVisuals,不进 Play Mode。

## 11. 待拍板清单

1. 阈值标定(2026-09-18 修订):主标定对象改为**周期检测器参数**——同一排列出现次数触发阈值、哈希内容(cardTypeID+阵营序列)、回合边界重置;L0 绝对上限(100/1500/60)与生产对局夹逼照走。K×池等相对预算启发降级为遥测,不再是标定重点;
2. ~~敌责与硬上限触顶的结果语义~~ 2026-09-18 已拍板,见 §9(含 4 条细化项待确认);
3. ~~组合库入库:自动 or admin 复核~~ 已拍板 = 自动入库,admin 事后申诉/退役(§7.6);
4. ~~回扫频率与触发时机~~ **已拍板 2026-09-19:随卡改动/发版跑**(§23.1;`-executeMethod InfinityBatchScan.ScanFromBatch` 可挂流程)。

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
- **跑 Test Runner 前必须先保存 scene,而且每次都要(跑测试本身就会把 scene 弄脏)**:scene 脏(`*` 标记)时 runner 启动会弹「Scene(s) Have Been Modified」模态框,阻塞主线程 → job 报 `failed to initialize (tests did not start within timeout)` 或直接挂起,而 `editor/state` 仍然显示 `ready_for_tools: true`、窗口标题也不变,极易误判成 runner/bridge 坏了。本轮实测多次命中(用户也反馈"遇到很多次")。规范已写入 `AGENTS.md` 的 Agent Post-Mortem Notes;`GameScene.unity` 带用户未提交改动,**严禁自动关掉该弹窗**。 **2026-09-19 实测根因**:脏不是用户造成的,而是**测试运行自己造成的**——Edit Mode 夹具把临时 GameObject 建在活动场景里,跑前标题干净、跑完就出现 `*`(中间无任何用户编辑)。所以「保存一次」不解决问题:**任何一次运行之后的下一次运行都会再弹**。Unity 没有清脏标记的公开 API(只有 `MarkSceneDirty`)。根治手段是把夹具对象建到 `EditorSceneManager.NewPreviewScene()` 里而不是活动场景。**2026-09-19 已在 `HeadlessCombatRig` 上验证**:用 `EditorSceneManager.sceneDirtied` 计数做 A/B —— 改之前一次 rig 运行让 GameScene 脏 **1** 次,改之后 **0** 次,而 sim 结果逐字节一致(同揭示数 17、同 trip hash `23517005`),同 seed 可复现性不变,销毁共享 dummy 后再跑也正常(预览场景会自动重建,无已销毁对象陷阱)。**`HeadlessCombatTestFixture`(约 40 个测试类)已于同日套用同样的处理**,并做了收尾 A/B:全量 **581 个测试跑完GameScene 仍然干净**(跑前跑后标题都无 `*`),套件 **581/581 通过 / 0 失败** —— 说明预览场景隔离对所有测试类无副作用,而「跑测试会弄脏场景」这个反复出现的坑从源头消失了。残留脏面:让**生产代码**在活动场景建对象的测试(如 `ResultStatsPanel.Build`)或任何真实编辑器编辑,所以跑前保存仍是好习惯。

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

### 17.4 建议(✅ 已执行 2026-09-19,8a21caf)

~~把四处 `CreateCurseStubDeck()` 的 2×JU_ON 换成**不预置诅咒**(惰性木桩,或 1×JU_ON),并删掉那条错误注释。这是**保真度清理**:让测试只跑游戏可达的状态。判据不变,实测已证。涉及 `ArrangementCycleDetectorTests` / `InfiniteDeckTerminationTests` / `RunBudgetSimTests`。~~ 已落地:三个测试文件改惰性木桩,另保留一个 1×JU_ON(可达上限)的 deck-vs-deck 用例以覆盖 `Run(deckA, deckB)` 入口;错误注释已删,判据实测不变。

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

## 19. P2 客户端核心落地(2026-09-19):归因 + 最小化 + 组合条目

§10 的 P2 = 「归因三连 + ddmin 最小化 + loop_reports 上报」。本轮先落**不依赖运行时的三件**(离线编译通过),运行时验证与 09-13 复现用例待编辑器空出后补(见 §19.4)。

### 19.1 交付物(`Assets/Scripts/Editor/Headless/`)

| 文件 | 职责 |
|---|---|
| `InfinityAttribution.cs` | §4 三连:敌 deck vs 木桩 → 我 deck vs 木桩 → 原配对重放;输出 `InfinityResponsibility { None, EnemyDeck, OwnerDeck, PairOnly }` 与三步各自的 `BudgetTripReport` 作为证据 |
| `ComboMinimizer.cs` | ddmin(按 chunk 补集试删)+ 多 seed 稳健门槛 + 1-最小性复核;输出 `MinimizeResult`(含 `Truncated` / `MultiSeedStable` / `IsOneMinimal`) |
| `LoopReport.cs` | §4 条目 schema(mySide / enemySide / roles / tripSignals / reproSeeds / status)+ `LoopReportBuilder` + `ComboRoleClassifier`;`JsonUtility` 可直接序列化 |

### 19.2 设计要点(可被质疑的地方都写在这里)

- **木桩血量必须极大**(默认 `1e8`)。理由是 §3/§15 那个「无限血木桩」框架的必然推论:lethal 型 combo 的自终止方式是**打死对面**,若木桩血量正常,它的循环看起来就是有限的、排列永远不会重复到触发线。1e8 按实测的泵增速率约需 1.4e4 次揭晓才能啃穿,远超默认 1500 的全局上限,于是木桩恒久存活、唯一的终止者只剩 L0 熔断,循环本体的重复就暴露出来了。
- **最小化门槛是多 seed 全通过**(§4 原文:仅单 seed 成环不入库)。ddmin 的每次谓词求值 = 每 seed 一次完整 headless 对局,所以带 `maxRuns` 预算;预算耗尽返回 `Truncated`,**不得**当作已证最小集入库。生产 ghost deck(30-60 张)会明显吃紧——这是 P5 需要正视的成本点。
- **roles 是启发式 + 原始证据并列**,不是权威判定:`ComboRoleClassifier` 只看效果方法名(ReviveSelf→Pump / Bury|Stage→Engine / Attack→ChainSwitcher),对 §6 三方环能复现手工映射(SOLDIER_SKELETON=Pump、RELIC_CHAIN_BURIAL=Engine、DEATHBED_GRANT=ChainSwitcher),但对环外卡形就是猜测——所以每条 `roles` 同时记下该卡真实的 `触发事件 -> 效果方法` 列表,便于反证。组合库以**卡片集合**为键,不以角色为键。
- **`enemySide` 恒空**:跨侧无限按 §7.1 不立项,字段只是预留,使管线天然 pair-aware。
- 顺带修掉 `RunVsDummy` 的一个副作用:它曾把 `dummyHp` 写回调用方的 `Options`,而归因要复用同一个 Options 对象跑三局 —— 会把木桩的巨大血量漏进后面的真配对重放。现在血量只走参数。

### 19.3 验证状态(2026-09-19 已全部跑通)

- **已验(离线)**:`dotnet build Assembly-CSharp-Editor.csproj` → 0 error。该路径本轮抓到两个真错误:`LoopReport` 缺 `using DefaultNamespace;` 的 CS0246、以及下面 §19.6 的证据链一跳问题。
- **已验(EditMode)**:`InfinityPipelineTests`(6)+`InfinityRingReproducerTests`(2)= **8/8 绿**;含这两个类在内全量 **589 total / 588 绿 / 0 失败 / 1 既有 Ignore**。跑完 `GameScene` 仍然干净(预览场景隔离对新测试同样成立)。

### 19.4 09-13 三方环实测结论:已被 P0 拆除(2026-09-19)

实测(RunBudgetSim,seed 4242,敌方 = 惰性木桩):

| 敌方木桩 | infinite | reveals | rounds | 结局 | cascadePeak |
|---|---|---|---|---|---|
| 30 HP | no | 67 | 7 | 敌方死亡 | **1** |
| 1e8 HP | no | 100 | 9 | **我方死亡** | **1** |

- **该环不再成环**:唯一的病理签名——213 层链嵌套栈溢出——消失,cascade 深度峰值只有 1。2026-09-17 的修复(同卡不同对象的换链只做 recorder 分组,守卫与 chainDepth 不再被洗掉)确实生效。
- 因此它是**「必须终止」的回归用例**,不是无界循环标本。已落成 `InfinityRingReproducerTests`(2 测):断言不成环 + 由死亡终止 + cascade 深度 ≤ 5(以 213 为历史基线)+ L0 未出手,并且**在对一个杀不死的对手时也一样**。
- **最小化的演示标本改用 lethal 样本**:ddmin 在 2 卡上证明「两张缺一不可」(`Minimize_LethalSample_KeepsBothComboCards`),这才是有效的 1-最小性演示。
- §6 原写的「应产出带角色三卡最小集」在环被拆除后不再适用:改为在仍无界的标本上产出最小集,并保留 `roles` 字段承载**证据**(见 §19.6)。

### 19.5 运行时接线:延迟归因(2026-09-19 用户拍板方案 1,已落地)

拍板内容:**不在战斗帧里跑 sim**;trip 时只记证据,出战斗后再做昂贵归因。

- `Assets/Scripts/Managers/InfinityTripJournal.cs`(**运行时程序集**,刻意如此):trip 时入队的证据 = 排列哈希 / 重复数 / 本回合采样数 / **combat seed** / 双方 DeckSO 与名字 / UTC 时间戳。有上限(32 条,超了丢最旧),双向 FIFO,`Drain()` 移交。
- 挂钩:`CombatArrangementCycleDetector` 在 trip 分支里 `Record(...)`(不跑任何模拟)。`OnCycleTripped` 事件保留给其它消费者。
- `Assets/Scripts/Editor/Headless/InfinityAttributionProcessor.cs`(编辑器侧):`ProcessPending()` 排空队列 → `InfinityAttribution` 三连 → 定责方做 `ComboMinimizer` → 产出 `LoopReport`(**由调用方决定何时跑**:菜单项、编辑器 update tick 且确认不在战斗中、或测试)。未复现的 trip **不产出条目**(绝不猜);配对责产出 `mySide` 为空的条目(§7.1,不 flag 任何人)。
- `LoopReport.liveTripSignal` 新增:记录**真实那次** trip(哈希/重复数/combat seed/时间),与 `tripSignal`(headless 复现)区分开。

**架构约束(重要)**:`RunBudgetSim` 依赖 `SerializedObject` + `AssetDatabase`,两者都是 editor-only,所以**打包后的游戏跑不了归因**。这不是缺口而是分工——正式包里的正确形态是:检测器只负责**采集证据并上传**(P3 的 `loop_reports`),归因由离线/服务端完成(P5 批扫与 P2 的 sim 共用同一份实现,正是 §2 的初衷)。

**跑测试时发现的真 bug(已修)**:`ProcessPending` 跑归因 sim 时,sim 里的真实检测器**也会触发**,把复现用的 trip 又写回队列 —— 队列永远排不空(实测 `ProcessPending` 消费 1 条后残留 2 条)。修法是给 journal 加**抑制深度**:`RunBudgetSim` 每次运行都包在 `PushSuppression()/PopSuppression()` 里,`Record` 在抑制期直接返回。"模拟不是真实 trip"这条语义现在由代码表达。

- 测试:`InfinityTripJournalTests`(4 测,随 cddc1e4 落地)——journal FIFO 顺序 / 有界丢最旧 / Drain 清空移交 / 处理器对未复现 trip 不产条目。

### 19.6 待补

1. `loop_reports` 落表与上报:表在服务端,按 §8 属 P3 的「证据表」;P2 只产出 payload(已具备)。
2. 编辑器侧触发时机:目前 `ProcessPending` 由调用方决定,生产场景里需要一个"不在战斗中"的 tick 或战斗结束回调来定时排空(与帧预算解耦)。

(2026-09-19 审核确认两项仍成立:服务端 `loop_reports`/`flag` 零实现;`ProcessPending` 全库仅 `InfinityTripJournalTests` 调用,生产侧无触发方。)

### 19.7 记录:证据链必须追两层,不能只看一跳

首次跑测试时 `roles` 记成了 `RELIC_CURSE_REVIVAL <Unknown> [OnHostileCurseRevealed -> InvokeEffectEventVoid]` —— 因为卡上的 `GameEventListener` 调的是容器的 `InvokeEffectEventVoid`,**真正的效果方法在容器自己的 `effectEvent` 里**。只读一跳会让每张卡的证据都一样、且角色全部落到 `Unknown`。已修为:经 `listener.response.GetPersistentTarget(i)` 取到 `CostNEffectContainer`,再读它的 `effectEvent`,容器外的目标才回落到直接方法名。触发事件名本身一直是准的。

## 20. P3 设计:服务端 flag / 证据表 / 出队过滤 / 客户端过滤面(2026-09-19)

### 20.1 四项拍板(2026-09-19 用户)

1. **flag 粒度 = 内容指纹**,不是 deck 行、不是玩家。decks 表每上传一次快照就新增一行(`DeckSaver.EnqueueSnapshot` → `POST /api/decks`),只 flag 被举报的那一行,对方下一次快照上传即绕过。指纹 = 该 deck 的 cardTypeID **多重集**(排序后 join)sha256 前 16 hex;当前用等值匹配,P4 把同一张表升级为子集(组合)匹配,结构不变。
2. **采信门槛 = 一次报告即 flag**(§7.6「入库自动、admin 只管事后申诉/退役」的延续),report 记 reporter,误报可追溯;admin 可解封。
3. **客户端过滤面 = 服务端出队过滤 + 缓存清理**。磁盘缓存 `OpponentDeckCache` 只增不改(`MergeResponse` 对已存在的 deckId 直接 continue),响应需带 `flaggedDeckIds` 让客户端清掉已缓存的条目;default pool 不参与过滤(开发者维护的资产,§5 池层防线与审计口径管,不来自玩家)。
4. **验收 = node --test**(新增 `server/onedeck-api/tests/`,临时 DATA_DIR):上传 → 报告 → flag → 出队不再返回 → 解封恢复。
5. **forced 平局的上报维持现状**(2026-09-19 用户):P3 不碰 match report。被 flag 的 deck 仍会因平局积 `defense_wins`(虚高),等将来统一平局语义时再改 —— 见 §20.5。

顺带更正 §8:**「三来源」已过时成两来源** —— 本地 json 回退 2026-09-04 已删,现行链是 `debug > server ghost > default pool`(`DeckSaver.PopulateEnemyDeckBySessionNumber`,DeckSaver.cs:366)。

### 20.2 服务端(server.js)

表(沿用 `CREATE TABLE IF NOT EXISTS` + `ensureColumn` 双轨,ensureColumn 现成于 server.js:194):

| 表 | 字段 |
|---|---|
| `decks`(改) | `flag INTEGER NOT NULL DEFAULT 0` / `flag_reason TEXT NOT NULL DEFAULT ''` / `flagged_at TEXT` —— 审计与展示面;过滤判据是指纹集合,flag 列是它的物化快照 |
| `flagged_fingerprints`(新) | `fingerprint TEXT PRIMARY KEY, first_deck_id INTEGER, report_count INTEGER, created_at TEXT, updated_at TEXT` |
| `loop_reports`(新) | `report_id TEXT PRIMARY KEY, deck_id INTEGER, reporter_player_id TEXT, game_version TEXT, fingerprint TEXT, verdict TEXT, seed INTEGER, signals TEXT, payload TEXT, created_at TEXT` |

端点:

- `POST /api/loop-reports`:`{playerId, gameVersion, opponentDeckId, verdict, seed, signals, minimizedCards, payload}`。校验 deck 存在(404)/ 非自己(400 own_deck);写 `loop_reports`;`report_id` 由 `(deck_id, fingerprint, seed)` 派生 → 天然幂等(沿用 matches/report 的 reportId 模式)。**只有带 verdict(已证单侧无限)的报告触发 flag**:该行 + 同 game_version 下所有同指纹行置 flag,指纹入 `flagged_fingerprints` 并累加 `report_count`。
- `POST /api/decks`(改):插入前算指纹,命中 `flagged_fingerprints` → 新行直接落 flag(重传同内容即刻拦住)。
- `GET /api/decks/opponents`(改):两条随机查询加 `AND flag = 0`;响应新增 `flaggedDeckIds` = 该 game_version 下 `session_num <= maxSession` 且 flag=1 的 deck_id 列表(覆盖客户端 prefetch 范围,服务端不必知道客户端缓存内容)。
- `POST /api/admin/decks/unflag?token=...`:按 deck_id 或按指纹解封(二选一参数);admin 页面新增 section(loop_reports 列表 + flagged decks + 解封按钮;改状态走 POST,不用 GET)。
- 结构性最小改动:`server.js` 末尾把 listen 包进 `if (require.main === module)`,`module.exports = { app, db }` —— 生产行为零差异,但 node 测试能 require 起临时库(现在 boot 即 listen,测不了)。

### 20.3 客户端

- `InfinityTripJournal.Entry` 增 `EnemyDeckId` / `PlayerDeckId`(trip 时从 `OpponentDeckCache.Current` 取;本地 pool 对手 = 0)。这是 P2 遗留的「报告没有落点」缺口。
- `OpponentDecksResponse` DTO 加 `flaggedDeckIds`;`OpponentDeckCache.MergeResponse` 处理它 → 从磁盘缓存 RemoveAll 命中条目 + 日志。
- `NetUploadKind.LoopReport` + `ServerConfig.uploadLoopReports = true` + `UploadOutbox.EndpointFor` 分支 + `NetDtos.LoopReportUploadRequest`;新 `LoopReportUploader`(runtime)把 `LoopReport` 与对应的 `Entry.EnemyDeckId` 组装入 outbox。`EnemyDeckId == 0` 不上报(没有可 flag 的行,池由审计口径管)。
- **上报源与保护链(重要)**:今天能产出 verdict 的只有编辑器侧 `InfinityAttributionProcessor`;打包内跑不了归因(RunBudgetSim 依赖 SerializedObject/AssetDatabase,§19.5 已记)。所以生产保护链是「运行时检测 → 证据上传(P3 表)→ 离线确认(P5 批扫)→ flag 过滤(P3)」,「一次报告即 flag」作用于**带 verdict 的报告**(今日 = 编辑器 / playtest 客户端);verdict 为空的报告只落证据不 flag。P3 把两种形态收进同一张表,P5 是把无 verdict 证据变成 flag 的那一环。
- **§9 联动(2026-09-19 用户拍板:维持现状,P3 不碰)**:`PhaseManager.ReportMatchResult`(PhaseManager.cs:401,调用点 :205)把非决定性收场一律按 `won=false` 上报 —— 含 L0 强制终局与 §9 判平 —— 服务端于是给被 flag 的 deck 记一次 `defense_win`。这与「判平局 = 无效局,双方不计胜负」的字面不一致,但代价限于「被 flag 的 deck 的 defense 胜率虚高」这个展示面问题,值得单独一轮统一平局语义(届时 match_reports 的 `won` 需改三态)时处理。**P3 不改 `PhaseManager`,实施顺序第 3 步不含此项。**

### 20.4 测试与验收

- 服务端 `tests/loop-reports.test.js`(node 内置 test runner;package.json 加 `"test": "node --test tests/"`):①同内容两行、报告其一 → 出队两条都不返回;②报告后再上传同内容 → 新行落库即 flag;③异内容 deck 不受影响;④报告自己的 deck → 400、不存在的 deck → 404;⑤重复报告幂等;⑥admin 解封后出队恢复;⑦`flaggedDeckIds` 只含该版本该 session 范围。
- 客户端 EditMode:`OpponentDeckCacheTests` 扩展(purge 命中项 + 保留未命中项),`LoopReportUploader` payload / 开关 / `EnemyDeckId = 0` 跳过(沿用既有 hermetic 假 HTTP 模式)。

### 20.5 已拍板(无遗留待确认)

- **forced 平局的上报 = 维持现状**(2026-09-19 用户,推翻本轮设计的初版建议):不再提「非决定性收场一律不上报」,`PhaseManager` 在 P3 内零改动。已知接受的不一致:被 flag 的 deck 会因平局积 `defense_wins`;将来做「平局三态」时一并解决(需要 `match_reports.won` 改 `result TEXT` + admin 的 defense 统计口径跟着改)。
- 其他三项拍板见 §20.1;P3 没有未决问题。

### 20.6 实施顺序

1. `server.js`(表 / 指纹 / 端点 / 出队过滤 / admin)→ 2. node 测试 → 3. 客户端(journal deckId、缓存 purge、uploader + 开关)→ 4. EditMode 测试 → 5. 文档(§8 两来源更正、server README 端点表 + 测试命令)。

### 20.7 实施记录(2026-09-19,用户授权「修改代码」后)

**已落地**:

| 面 | 改动 |
|---|---|
| 服务端 | `server.js`:decks 加 `fingerprint/flag/flag_reason/flagged_at`(CREATE TABLE + ensureColumn + **启动回填**存量行指纹);新表 `loop_reports`(证据,含 reporter 与 payload)与 `flagged_fingerprints`(内容键);`POST /api/loop-reports`(verdict=EnemyDeck 才 flag,report_id 幂等 = deck+指纹+seed+verdict+reporter);`POST /api/decks` 命中指纹即落 flag;出队两条查询加 `flag = 0`;响应新增 `flaggedDeckIds`(版本 + session 范围内);admin 面板新增 Infinity gate 段(flag 列表 / 报告列表 / 指纹表 + POST 解封表单);listen 包进 `require.main` 守卫 + `module.exports` |
| 客户端 | `InfinityTripJournal.Entry.EnemyDeckId`(+ `Record` 参数);检测器 trip 时从 `OpponentDeckCache.Current` 取 ghost deckId;`OpponentDecksResponse.flaggedDeckIds` + `MergeResponse` 清除命中缓存;`NetUploadKind.LoopReport` + `ServerConfig.uploadLoopReports` + outbox 端点;新 DTO `LoopReportUploadRequest`;新 runtime `LoopReportUploader.EnqueueEvidence`(只入队不 flush);编辑器侧 `InfinityAttributionProcessor.ProcessPendingEntries` / `ProcessPendingAndUpload`(原 `ProcessPending` 保持不碰 outbox) |

**验证**:

- 服务端 `npm test` → **9/9 绿**(临时 DATA_DIR,`node --test`):内容指纹连带 flag、重传自动 flag、超集/子集不误伤、无 verdict 不 flag、自报/不存在 deck 拒绝、按 reporter 幂等 + 第二受害者另记证据、admin 解封 + 解封后不再自动 flag、purge 列表版本/范围限定、admin 面板渲染。
- 迁移实测:对**线上库的副本**启动 → 新列 + 新表 + 索引就位,7/7 存量 deck 回填指纹,无报错(线上迁移走同一路径 `ensureColumn`)。
- 客户端 EditMode:`LoopReportUploaderTests` 5/5(本地 pool 跳过 / 入队内容与路由 / 开关关闭不入队 / 归因后入队并带 live deckId / 只归因不碰 outbox);`OpponentDeckCacheTests` 新增 2 条 purge 用例;`ArrangementCycleDetectorTests.LethalInfiniteDeck_TripsCycleDetector` 增 ghost deckId 断言。**全量 EditMode 600 total / 599 绿 / 0 失败 / 1 既有 Ignore**;跑完 GameScene 仍干净。
- 一处踩坑记录:**用文件工具改完 .cs 后必须 `refresh_unity` 再跑测试**,否则 Test Runner 用的是上一次编译的程序集——本轮实测出现过一次(第一次刷新后立刻跑,运行于旧 DLL,报的是旧断言的失败文案;刷新落盘时间与 run 启动时间只差 2 秒)。另外全量跑仍需 `init_timeout` 放宽(默认 15s 会报 `failed to initialize`)。

**未做 / 边界**:

- **打包内产不出 verdict**:归因 sim 依赖 SerializedObject/AssetDatabase(§19.5),正式包只能上「无 verdict 的证据」,而服务端只对带 verdict 的报告 flag ⇒ 生产保护链 = 运行时检测 → 证据上传(P3 表)→ 离线确认(P5)→ flag 过滤(P3)。今天能真正触发 flag 的是编辑器/playtest 客户端。
- **`ProcessPendingAndUpload` 没有生产触发方**(§19.6.2 未决):跑归因会清空活动单例(`HeadlessCombatRig.Create` → `CleanupSingletons`),所以不能挂在游戏运行中的 tick 上;需要保存/恢复单例或加「无活动对局」硬闸,单独立项。
- `PlayerDeckId` 未加(§20.3 初稿曾列):玩家正在使用的卡组没有服务端行身份,记它无意义;证据里只需要被指控的 ghost deckId。
- `minimizedCards` 未单列请求字段(§20.2 初稿曾列):它已在 `payload`(LoopReport JSON)里,避免两处真相。
- 客户端 ↔ 真实服务端的 E2E(play mode 触发上传 → 服务端 flag → 下次 prefetch 不再下发)未跑:两端的契约各自有测试钉住(服务端 HTTP 集成 + 客户端 payload 断言),端到端留给 `ProcessPendingAndUpload` 接上触发方之后一起验。

## 21. P4 组合库 + 匹配交集检查(2026-09-19)

### 21.1 为什么 P4 必须存在(与 §20 的分工)

P3 的 flag 是**等值内容键**:只有"卡表恰好等于被证明那一副"的 deck 被扣。改一张牌就是新指纹,照样下发 —— 服务端测试 `flagging is content-scoped: supersets and subsets stay served` 把这个边界钉成了规格。§1 目标 1(玩家永远不会匹配到会无限的敌方 deck)真正成立需要**子集判定**:含该组合的任意 deck 都扣,不管它另外还带了多少张牌。P4 就是这一层。

### 21.2 设计要点

- **组合库表 `combos`**:`combo_key`(组合卡表的多重集指纹,复用 §20 的 `deckFingerprint`,所以 deck 与组合共用同一套键概念)、`cards`(JSON 卡表,多重集)、`status`(active/retired)、`first_report_id`、`report_count`、`created_at/updated_at/retired_at/retired_reason`。**卡表不可手改** —— 它来自 sim 证明的最小集,手改等于宣称 sim 错了。
- **入库门槛 = 三条件全满足**(§4 的「多 seed 稳健 + ddmin 最小集」落成可判定字段):payload 里 `oneMinimal === true` 且 `truncated !== true` 且 `multiSeedStable !== false`,且 `mySide` 非空。未知/缺失一律视为「未证明」—— 宁可不入库。**为此给 `LoopReport` 补了 `multiSeedStable`**(此前只有 oneMinimal/truncated;`ComboMinimizer.MultiSeedStable` 一直存在,只是没进 payload)。顺带澄清:`IsOneMinimal == true` 已蕴含「多 seed 全过且未截断」(最小化在 `!MultiSeedStable` 时提前返回、IsOneMinimal 保持 false),两个字段是**冗余但显式**的证据,便于反证。
- **匹配时判定**:`GET /api/decks/opponents` 取出候选行后在 JS 里做**多重集包含**判定(组合要两张 X,deck 里就得有两张 X),命中者不进响应。SQL 的 `ORDER BY RANDOM() LIMIT n` 保留但 limit 放大 20 倍(`CANDIDATE_OVERFETCH`),采样裁剪移到 JS —— 被扣的 deck 极少,20 倍余量足够填满响应;真出现某 session 95% 以上被扣才可能少给,而客户端本就容忍少给(会回落本地池)。
- **缓存侧**:响应新增 `blockedCombos`(active 组合的卡表),客户端 `MergeResponse` 删除**含该组合**的缓存条目。理由同 §20.3:服务端看不到客户端磁盘缓存,而组合在注册之前就可能已被缓存。
- **复核流**(§10 P4 第三项):admin 面板新增 Combo library 段,`retire`(卡片已修)/ `reactivate` 两个动作 + 退役原因。**退役组合收到新报告不会自动复活**:只累加 `report_count` 并打 WARN 日志,交 admin 判断 —— 退役通常意味着「卡改了」,而 sim 证据本身分不清「没修好」和「客户端版本旧」。
- **版本无关**:组合键与 §20 指纹一样不分 game_version(同一副卡表在任何版本都是同一个环);`blockedCombos` 下发全量 active 列表(数量小),`flaggedDeckIds` 才按版本 + session 范围裁剪。
- **默认池仍不参与**(§20.1 第 3 条不变):它是开发者资产,不来自玩家。

### 21.3 实施记录(2026-09-19)

| 面 | 改动 |
|---|---|
| 服务端 | `combos` 表;`extractProvenCombo`(入库门槛);`POST /api/loop-reports` 在 flag 的同时入库组合并回传 `comboKey`/`comboStatus`;出队路径改为「取候选 → 多重集包含过滤 → JS 采样」;响应新增 `blockedCombos`;admin Combo library 段 + `POST /admin/combos/status`(retire/reactivate + 退役原因);新增 `adminParam`(表单体或 query 都认)与 `express.urlencoded` 解析 |
| 客户端 | `OpponentBlockedCombo` DTO + `OpponentDecksResponse.blockedCombos`;`OpponentDeckCache.ContainsBlockedCombo`(多重集包含)+ `MergeResponse` 按组合清理缓存 |
| 证据 | `LoopReport.multiSeedStable` + `LoopReportBuilder` 接线 |

**验证**:

- 服务端 `npm test` → **17/17 绿**(P4 新增 8 条):含组合的**加料 deck** 被扣而无关 deck 照发;六种「未证明」payload(非 1-最小 / 截断 / 非多 seed 稳健 / 缺标志 / 空集 / 非 JSON)一律不入库**但仍 flag 卡组内容**;多重集语义(`[A,A,B]` 不被单张 A 满足);证据型 verdict 永不入库;`blockedCombos` 下发且退役后消失;退役恢复下发、重新激活再次扣留、退役元数据清空;退役组合的新报告只累加证据计数并保持退役;未授权退役 403;面板渲染。
- 客户端 EditMode:`OpponentDeckCacheTests` 新增 2 条(含组合的缓存条目被清、半个组合与多重集不误伤);全量 **602 total / 0 失败 / 1 既有 Ignore**(P3 那次 600,新增 2 条),按 AGENTS.md 的 stale-assembly 规矩先 refresh 并核对程序集新鲜度后才跑。

**遗留**:

- 匹配层只做「服务端出队过滤 + 客户端缓存清理」,没有匹配瞬间的最终校验 —— 理论上仍有窗口:某次 prefetch 之后新注册的组合,要等下一次 prefetch 才作用到本地缓存(客户端每次进商店都会 `EnsureStockForSession` → `Prefetch`,窗口最长一个 session)。
- §4 的 `retired(卡改动后须复验)`只是字段 + admin 手动动作;**卡改动自动触发复验**未做(需要 P5 回扫或建卡流程挂钩)。
- `enemySide` 仍恒空(§7.1 跨侧不立项)。

## 22. P3/P4 上线记录(2026-09-19)

**通道**:本机没有 SSH 密钥(项目运维本来也不走 SSH——`summaries/` 归档写着「无公网 IP 运维走 workbench-cli skill」,`tools/outputs/dump_catalog.py` 是同一套)。所以先补 CLI:Windows 不在官方 install.sh 支持范围内,手动下载 `workbench-windows-amd64.zip`、**校验官方 sha256**(`e15c039…`)→ `~/.workbench/bin/workbench.exe`(v1.0.1);用户提供 `onedeck-workbench` RAM 子账号 AK 写入 `~/.workbench/config.json`(凭据不进对话记录)。

**目标**:实例 `i-uf66n1ofpudgn9b6rg7o`(cn-shanghai,公网 8.153.150.197);会话身份 root,pm2 进程 `onedeck-api` 亦 root。

| 步 | 动作 | 结果 |
|---|---|---|
| 1 | 只读侦察 | 远端 `/var/www/onedeck/{server,data}`、node v22.22.1、pm2 online |
| 2 | **覆盖前溯源** | box 上 `server.js` 的 sha256 与仓库任何提交都不符 → 下载回本机,**去掉 CR 后与 `8c52211` 逐字节相同**(1100 行)——哈希不符是 CRLF/LF 假象,**机器上没有未入库改动**,确认可覆盖 |
| 3 | `workbench upload` 两个部署脚本 → `scripts/` | 通过(该通道在仓库里此前从未被用过);坑见下 |
| 4 | 迁移前基线 `inspect-db.js` | 无新表新列,`decks=109 players=5` |
| 5 | WAL 安全在线备份 | `onedeck.db.bak-2026-09-19T10-21-14-601.db`(1518 页 / 109 decks,脚本重开副本核验) |
| 6 | `cp -p server.js server.js.bak-20260919-p3p4` → 上传为 `.new` → **sha256 与本地逐字节一致** → `node --check` → `mv` + `pm2 restart` | 沿用机器既有 `.bak-<日期>` 惯例;上传 `5a9ea88e…` 双方相同 |
| 7 | 验证 | `/api/health` 200(uptime 归零=重启生效);`POST /api/loop-reports` **404 → 401**;`POST /admin/combos/status` **403**(存在且鉴权);`inspect-db` → 三表四列就位、**109/109 指纹回填**、flagged=0;日志 `backfilled 109 deck fingerprint(s)` + `listening`,error log 空 |

**两个 shell 陷阱(已补进 server README)**:

1. Git Bash 会把**远端路径** `/var/www/onedeck/...` 改写成 `D:/Program Files/Git/var/www/...`,上传直接报 `InvalidParameter.Path` → 必须 `MSYS_NO_PATHCONV=1`(exec 的 `--command` 不受影响,因为它不以 `/` 开头)。
2. `download` 的**本地路径**必须是 Windows 形态:Windows 版 CLI 把 `/tmp/x.js` 理解成 `D:\tmp\x.js`(会真的在 D 盘建目录)。

**刻意跳过 `npm install`**:依赖块自 `8c52211` 未变(仅内置模块 + 已装的 express/better-sqlite3),不必要地动线上 node_modules 是多余风险。README runbook 里那步保留为「包依赖变动时执行」。

**刻意未做——零生产写入**:没有测试上报、没有造 flag、没有建 test 玩家。功能面验收由 17 条 node 测试在临时库上覆盖(同一份代码);若要做上线后功能性冒烟,按 2026-09-13 `tools/outputs/_verify_cst8_run_smoke.js` 的既有模式(一次性 `test_` 玩家 + 自查自清)单独授权执行。

**上线后的真实边界(不变)**:闸门现在**在线可用**,但**产不出 verdict**(打包内跑不了归因 sim,§19.5),所以除非有人从编辑器/playtest 客户端上报,线上不会出现 flag。生产保护仍取决于 P5(离线确认)与 §19.6.2(归因触发时机)。

## 23. P5 存量回扫批工具(2026-09-19)

### 23.1 设计要点

- **核心洞见**:P5 要回答的问题**就是**下发闸门要问的那一个——「这副 deck 作为对手时会不会自环」。所以不必跑 §4 的归因三连,只需要「敌 vs 木桩」这一腿:每副候选 `RunVsDummy(deck, dummySize=3, dummyHp=1e8)`,命中 `SuspectedInfinite` 的再 ddmin 取最小集。木桩参数直接复用 `InfinityAttribution.DefaultDummySize/DefaultDummyHp`(§17 已证 0/1/2 张诅咒结论一致)。
- **职责按「谁能做什么」切开**(沿用既有边界):网络/运维留在脚本侧,sim 留在 Unity 侧,而**写生产**这一步必须能单独干跑、单独计数:

  | 文件 | 职责 |
  |---|---|
  | `tools/outputs/dump_decks.py` | 只读 dump 线上 `decks` 表 → `tools/outputs/decks_current.json`(走 workbench exec,同 dump_catalog.py) |
  | `Assets/Scripts/Editor/Headless/InfinityBatchScan.cs` | 建 cardTypeID→prefab 映射(`AssetDatabase` 扫 `Assets/Prefabs/Cards`)→ 按卡表去重 → 跳过已 flag → 逐副 sim → 命中则 ddmin → 写 `infinity_scan_<UTC>.json` + `.md`;`[MenuItem]` + `-executeMethod` 入口;也扫本地 `Assets/SORefs/Decks/Recorded/**` 但**不参与上报**(无服务端行)。另含环追踪(`TraceRingsFromLastScan` / `FindTailPeriod`,见 §23.4) |
  | `tools/outputs/post_loop_reports.js` | 读报告 → POST `/api/loop-reports`;**默认干跑**,`--post` 才真发 |

- **门槛比「检测到」更严**:poster 默认只发**已证**条目(1-最小 + 多 seed 稳健 + 未截断 = 服务端入库门槛),单 seed 成环的**默认扣下并列出**。这条不是拍脑袋——首跑实测 9 副命中里就有 4 副是单 seed 成环(`multiSeedStable=false`),若按 status 直接上报,会把 4 副真实玩家的 deck 按弱证据 flag 掉,违背 §4「仅单 seed 成环不入库」。`--include-unproven` 供人判断后刻意放行。
- 报告分档:`.md` 把 infinite 拆成「proven minimum(postable)」与「unproven(NOT posted by default)」两段;`InfinityBatchScan.IsProven` 与 poster 的门槛同构(跨语言镜像,两处注释互指)。
- 去重按**卡多重集**(deck 行是每次上传一行,首跑 109 行里只有 105 种内容);**卡片解析不全则拒绝模拟**(掉一张牌可能把环变成非环,而错误结论会 flag 真实 deck)。
- 扫描拒绝在 Play 模式运行:rig 建起来会清空活动单例(与 §19.6.2 同源)。
- **触发 = 随卡改动/发版跑**(2026-09-19 用户拍板,§11.4 由此结案):`-executeMethod InfinityBatchScan.ScanFromBatch` 可挂进流程;菜单项 `Tools/Infinity/Batch Scan (report only)` 供手动。

### 23.2 实施记录

- 首跑(2026-09-19,dump 线上 109 行):**candidates=109 / scanned=105 / infinite=9 / clean=96 / deduped=4 / unresolved=0**,两条警告(`RIFT_GUIDE`、`WEAPON_SPIRIT` 各有两个 prefab 共用同一 cardTypeID——扫描保留第一个并告警;这是卡资产层面的重复 ID,建议单独清理)。
- 9 副命中里 **5 副已证**(deck 5 / 66 / 67 / 109 / 110:1-最小 + 多 seed 稳健),**4 副未证**(deck 88 / 89 / 107 / 111:`multiSeedStable=false`,单 seed 成环)。命中集中在同一族卡表(`CURSE_GARDENER` / `RELIC_CURSE_REVIVAL` / `CURSE_REVIVER` / `GRAVE_HEXER` / `RELIC_CHAIN_BURIAL` / `RELIC_CURSE_GRAVE`),与 §4 标本同源。
- **本次为零生产写入**(用户裁定「首跑只出报告」):未 POST、未建 flag、未动线上数据;仅只读 dump + 本地报告。那 5 副已证条目是否上报,待用户看完报告再定。
- 报告落盘:`tools/outputs/infinity_scan_<UTC>.json` + `.md`;poster 干跑对同一文件即可预演上报清单。

**测试**:`InfinityBatchScanTests` 8/8(命中即最小集 + payload 可反序列化回 LoopReport + 单卡非环 + 不可解析不得模拟 + 内容去重 + 已 flag 跳过 + 报告序列化与分档 + prefab 映射);`tools/outputs/post_loop_reports.test.js` 6/6(默认干跑零请求 + 缺 reporter 拒绝 + 只发已证条目且 payload 逐字不变 + `--include-unproven` 刻意放行 + 无条目零请求 + 非报告文件报错退出)。

### 23.3 一个环境坑(值得记住)

**Unity 失焦时会挂起脚本编译**:`InternalEditorUtility.isApplicationActive == false` 期间 `refresh_unity` 只排队不执行(实测卡住 4 分钟、程序集 mtime 不动),窗口重新获得焦点那一刻才编译。而 `run_tests` **匹配到 0 个测试时不会触发强制重编译**——于是「新写的测试类还没编译 → 按类名跑 → 0 个测试、状态 passed」是个极具迷惑性的假绿。处置:跑测试前先确认 `isApplicationActive` 与「程序集 mtime > 源文件 mtime」。

同一失焦状态下 **`EditorApplication.delayCall` 的排程也静默不触发**:定时批扫(当时窗口在前台)跑完了;几分钟后同方式的环追踪排程,失焦期间既无日志也无产物。长任务改为在 `execute_code` 里**同步执行**(超时也无妨,主线程会继续并把产物写完),或仅在窗口激活时排程。

### 23.4 环的可检查证据(2026-09-19,用户要求「展示它们的环」)

只给最小集清单不足以复核,所以补了「把环读出来」的能力:`BudgetTripReport.RevealTrace`(每次揭晓记 `侧:cardTypeID`,Start Card 带 `*`)+ `Options.RecordRevealTrace`;扫描器侧 `TraceRingsFromLastScan` 对每副命中 deck **只跑它的最小集**(最小集才是环,整副是填充)并自动找出**重复窗口**;产物 `tools/outputs/infinity_rings_<UTC>.{json,md}`。

**方法论修正(第一次跑就撞上)**:追踪最初用生产参数,于是引擎按揭晓数疲劳往牌堆里塞 `SYSTEM_FATIGUE`,周期搜索抓到的是「每揭晓一次出一张疲劳卡」——deck 66/67 报出 `period 1 = O:SYSTEM_FATIGUE`,是引擎噪声不是环。**追踪改为关掉疲劳**后环才显形(扫描本身仍用生产参数,判定不变);差异写进了产物说明。

实测(seed 4242,木桩 3 卡 1e8 HP,疲劳关):

| deck | 判定 | 最小集 | 周期 | 窗口 | 机制(来自 roles 证据) |
|---|---|---|---|---|---|
| 66 / 67 | 已证 | `CURSE_GARDENER` + `RELIC_CURSE_REVIVAL` | **2 ×97** | `O:CURSE_GARDENER → E:JU_ON` | 养蛊人揭晓→复活/强化敌方诅咒(`ReviveTheirCards`/`EnhanceCurse`),诅咒揭晓→`OnHostileCurseRevealed:ReviveMyCards` 把养蛊人拉回。**与 §4 标本同一个环** |
| 109 | 已证 | `GRAVE_HEXER` ×2 | **1 ×195** | `O:GRAVE_HEXER` | 坟冢巫妖 `OnMeRevealed:ReviveMyCards,EnhanceCurse` —— 两张互捞,同类型卡自环 |
| 110 | 已证 | 2×`CURSE_REVIVER` + `GRAVE_HEXER` + `CURSE_SUMMONER` + 2 张 utility | **2 ×24** | `O:CURSE_SUMMONER → O:GRAVE_HEXER` | 两个捞牌源互相喂(`ReviveMyCards`/`ReviveTheirCards`) |
| 5 | 已证 | 6 张(`REVIVE_SUMMONER`×2 + `EXILE_BERSERKER`/`RIFT_REAPER`/`RIFT_PRIEST`/`RELIC_HIVE`) | 1 ×3(最松) | `O:REVIVE_SUMMONER` | 复活召唤 + 放逐/攻击 + 蜂巢(`onAnyFriendlyCardAttacked:AddCardToMe`)的宽环 |
| 88 / 89 | 未证 | 16–18 张 | 无周期 | — | 环在爬升/未收敛,与「单 seed 成环」一致 |
| 107 / 111 | 未证 | 10 / 17 张 | 2 ×9 / ×22 | `O:CURSE_SUMMONER → O:GRAVE_HEXER` | **与 deck 110 同形**;但在生产参数 + 双 seed 下不可复现,故仍属未证 |

**两条对 P4 组合库有影响的观察**:

1. **最小集里有时留着 utility 被动与 `SYSTEM_INCREASE_HP_MAX`**(110/111),而 deck 109 的同类牌被 ddmin 剔掉了——说明 1-最小性的「必要性」可能是**结构性**的(少一张牌就改变排列周期,查不到第 3 次重复)而非**因果性**的。若把这种集合原样做成组合键,匹配面会跟着这些被动牌走。**上线前建议**:对入库组合在多层卡表上复验同一最小集是否稳定,或把 utility/被动排除在组合键之外,否则 P4 的子集判定会漏掉「同一元凶 + 不同被动」的变体。
2. deck 66 与 67 的最小集相同 → 会落到**同一个组合键**;110 与 107/111 的核心同形 → 一旦 107/111 被证明,会与 110 合成同一组合的不同实例。这正是组合库「以卡片集合为键」的设计意图,记录备查。

### 23.5 判定口径修正 + 编辑器 OOM 事故(2026-09-19)

**口径修正(用户拍板「改:关疲劳测环」)**:`RunBudgetSim.Options.LoopDetection()` = 生产默认但**关掉 overtime 疲劳**(并把回合钟推到 999)。理由是可测的:疲劳每 `FatigueRevealThreshold` 次揭晓往牌堆塞惰性卡,改变排列 → 把 flag 判据要找的重复冲掉。实测 deck 107/111「整副 + 非被动核心」在 3 个 seed 上**全部成环**(repeats 162–199),而生产参数下它们在 2 个 seed 上都测不出——**此前「单 seed 成环」的结论是假阴性**。§16.3 本来就把两件事分开(`SuspectedInfinite` = flag 判据;`BudgetCapConcluded`/疲劳 = 玩家可感伤害),本次让扫描与 P2 归因都按这个拆分走:
- 扫描判定与 ddmin 用 `LoopDetection()`;**生产参数结果降级为遥测列**(`DeckResult.productionTripped/productionRepeats/productionReveals`,只在检测到环的 deck 上补跑一次);MD 表加两列。
- P2 归因(`InfinityAttributionProcessor`)同样改用 `LoopDetection()`。
- 新增 `BudgetTripReport.RevealHashes`(与 RevealTrace 同开关):**排列哈希序列**。这揭示了 88/89 与紧环的本质差别——`repeats=65` 统计的是「同一排列本回合出现 65 次」,**不是连续 65 次**;88/89 的重复是**不连续**的(其他揭晓夹在中间),所以相邻周期检测对它们必然找不到窗口。`RingTrace` 增加 `recurrenceIndices`/`recurrenceGaps`,报告改为「无相邻窗口 + 出现位置与间隔」而非「无周期」。

**实测(关疲劳,3 个 seed: 4242/7/11)**:107/110/111 的 full 与 core 变体 **12/12 全成环**(repeats 45–199);88 full 3/3(repeats 65)、89 full 3/3(56–199),但 **88 core 在 seed 7 不成环(repeats=1)、89 core 在 seed 11 不成环(repeats=2)** → 88/89 属「宽环 + 低重复度 + 对牌组构成敏感」,它们的被动卡是承重的。**用户对 107/111 的「需要最先连续两张复活友方」假设未获支持**:3 个 seed 的开头各不相同(有 DUMMY 开头、有 ZOMBIE 开头),均照样进环。

**被动剔除(用户要求)**:`ComboMinimizer` 在 ddmin 之后剔除 `CardScript.IsUtilityPassive`(实测覆盖全部 12 张 `UTILITY_*` 与 `SYSTEM_INCREASE_HP_MAX`,它们 `isPassive=true` 且 0 个效果容器),**但必须先复验**「剔除后仍在所有 seed 上成环」才采用;否则保留原集合并把证据写进 `LoopReport.stripNote`(例如 deck 110 在旧口径下就是「剔了就不成环」)。小牌组里 ddmin 自己会剔掉被动,该逻辑轮不到;大牌组才需要它。

**事故:编辑器 OOM 崩溃**。在关疲劳 + `GuardTotal=1500` + ddmin 数百次谓词求值的组合下重跑全量扫描,Unity 抛 `Could not allocate memory: System out of memory!` → `Crash!!!`(崩溃报告 `%TEMP%/Unity/Editor/Crashes`),进程退出;同机 bash 也同时被 `0xC000012D`(提交上限)杀掉。取证:rig 每次都正确 `Dispose`(销毁对象 + 关 preview scene),**不是单纯泄漏**——单次调用里 sim 数 × 每次揭晓数 × 每次分配的累积把机器的提交上限吃光。**处置(已落代码,待编译验证)**:`InfinityBatchScan.ScanGuardTotal = 400`(所有实测 trip 都落在 ~60 揭晓内,6 倍余量)、`ScanMinimizerMaxRuns = 120`(超预算 → Truncated,本就不入库),并把长扫描改为**分批调用**(`Run` 已支持传入过滤后的候选列表,不必改代码)。

**未落库**:以上改动在工作区,**重启编辑器后需先 refresh + 跑 `InfinityBatchScanTests`/`InfinityPipelineTests` 复验再提交**。旧口径的扫描报告(`infinity_scan_2026-09-19T10-57-36…`,5 已证 / 4 未证)是旧口径产物,新口径的分布尚未测出;上报集合(用户已把「先不定」的裁定更新为「先看 88/89 的具体环」)仍待定。

### 23.6 首次生产写入:6 个组合入库 + 13 副 deck flag(2026-09-19)

用户裁定「6 个组合全报」后执行(`post_loop_reports.js --post`,先干跑给用户看过完整清单):

| 组合键 | 卡片 | 覆盖 deck | reports |
|---|---|---|---|
| `dc13975a6287828a` | `GRAVE_HEXER` ×2 | 106,107,108,109,110,111 | 6 |
| `288e0bd3ce6ed9cd` | `CURSE_GARDENER` + `RELIC_CURSE_REVIVAL` | 65,66,67 | 3 |
| `4c18bc4a33cf0d9e` | `GRAVE_HEXER` + `SPIRIT_CALLER` | 64 | 1 |
| `baae04663b71b769` | `KINGSLAYER` + `CURSE_SUMMONER` | 88 | 1 |
| `4f752e40d0e06982` | `CURSE_SUMMONER` ×2 | 89 | 1 |
| `6fa8b86dda76b5fa` | `RIFT_ACOLYTE` + `REVIVE_SUMMONER` + `RIFT_STRIKER` + `GRAVE_GIANT` | 56 | 1 |

**写前/写后(线上实测)**:`flagged 0 → 14`,`loop_reports 0 → 13`,`active_combos 0 → 6`,`flagged_fingerprints 0 → 13`,`players 5 → 6`。13 条报告里 12 条来自 `test_papaya`、1 条(deck 56)来自新建的扫描器身份。

**两处身份问题(都已解决,值得记住)**:

1. **本机身份文件里的 `papaya` 在生产不存在** → 首轮 13 次全是 HTTP 401 `unknown_player`。生产现有玩家只有 fiff / renoxiao / test_papaya / papayatk / 玩家#1299。上报者身份必须取自**生产库**的玩家,不能想当然认为本机身份可用(本机身份可能是对着本地 dev 库注册的)。上报者 id 用只读 SQL 从线上库取,**不落对话、不落库**。
2. **deck 56 属于 `test_papaya` 自己** → 被服务端按规则拒(`400 own_deck`)。用户裁定「给扫描器一个专用身份」,于是注册了 **`onedeck-scan`**(玩家数 5→6);它的 playerId 存在 `tools/outputs/_scan_reporter.txt`(已 gitignore——playerId 本身即 API 凭据)。**以后 P5 上报一律用这个身份**:既不受 own_deck 限制(能报自己账号的 deck),审计上也能一眼区分扫描上报与玩家上报(海报还会在 `signals` 里附 `batch-scan`)。

**残留(未上报)**:deck 5 / 55(多 seed 成环但 ddmin 在 120 次预算内没收敛出 1-最小集)、deck 6(repeats=3 且单 seed)、deck 91(与 88/89 内容重复)。它们仍是报告里的 `infinite` 条目,若将来要覆盖需放宽 `ScanMinimizerMaxRuns` 后重跑并复核(注意 §23.5 的 OOM 教训)。

## 24. 现状快照与操作手册(2026-09-19 收尾)

> 给接手的人:读完这一节就知道「现在什么在跑、什么还没做、命令怎么写」。设计细节在 §20(服务端+客户端)、§21(组合库)、§23(P5),事故与教训在 §23.3/§23.5。

### 24.1 线上现状(实测,2026-09-19)

| 事实 | 值 |
|---|---|
| 服务端版本 | P3+P4 已上线(`feefd0d`/`a454189` 的服务端 → 已部署到 `i-uf66n1ofpudgn9b6rg7o`);`POST /api/loop-reports` 返回 401 而非 404 即为新版 |
| 下发闸门 | `decks.flag` **14 行**被扣、`active_combos` **6 个**、`flagged_fingerprints` 13、`loop_reports` 13 |
| 6 个组合键 | `GRAVE_HEXER`×2(6 副);`CURSE_GARDENER`+`RELIC_CURSE_REVIVAL`(3 副);`GRAVE_HEXER`+`SPIRIT_CALLER`;`KINGSLAYER`+`CURSE_SUMMONER`;`CURSE_SUMMONER`×2;`RIFT_ACOLYTE`+`REVIVE_SUMMONER`+`RIFT_STRIKER`+`GRAVE_GIANT` |
| 上报者 | 12 条来自 `test_papaya`,1 条(deck 56)来自专用扫描器身份 **`onedeck-scan`**;其 playerId 在 `tools/outputs/_scan_reporter.txt`(**gitignore**,playerId 即凭据) |
| 数据库 | 109 副 deck / 6 个 player;迁移是 boot 时增量完成(§22) |

**玩家实际获得的保护 = L0 熔断(任何对局必然结束)+ 下发闸门(6 个组合不再出现在对手里)。** 闸门覆盖面仍取决于「谁产出 verdict」:打包内跑不了归因 sim(§19.5),所以今天能产出 verdict 的只有**编辑器里的归因**(playtest)与 **P5 离线回扫**。正式包里只会上传无 verdict 的证据,服务端不据此 flag。

### 24.2 还没做的(按重要性)

1. **§19.6.2 归因触发时机**——`ProcessPendingAndUpload` 没有生产调用方:跑归因会清空活动单例(`HeadlessCombatRig.Create` → `CleanupSingletons`),所以不能挂在游戏运行中的 tick 上。需要「无活动对局」硬闸或单例保存/恢复。
2. **打包内的 verdict 来源**——P5 目前只能在编辑器里跑;若要让正式包也能出 verdict,要么把 sim 移出编辑器程序集(2 个编辑器 API 边界,见 §23.1 之外的讨论),要么接受「离线确认」的延迟。
3. **卡改动后的组合复验**——§4 的 `retired(卡改动后须复验)`目前只有 admin 手动 retire/reactivate;自动复验需要挂到建卡/发版流程(§11.4 已定为「随卡改动/发版跑」,但**没有 CI 钩子**,目前靠人记得跑)。
4. **P4 匹配层缺端到端验证**——服务端单测证明「含组合的 deck 被扣」,但没有跑过「真实客户端 → 真的不再下发」的联调;客户端缓存清理(`blockedCombos`)也只有单测。
5. **未上报的 4 副**:deck 5/55(多 seed 成环但 ddmin 在 120 次预算内没收敛出 1-最小集)、deck 6(repeats=3 且单 seed)、deck 91(与 88/89 内容重复)。要覆盖需放宽 `ScanMinimizerMaxRuns` 并在切片下小步重跑(§23.5 的 OOM 教训)。
6. **§11.1 阈值标定**仍未做(周期检测器的触发次数/哈希内容/回合重置),L0 绝对上限也没做生产对局夹逼。

### 24.3 操作手册(全部在仓库里,零手工文件)

```bash
# 1) 只读 dump 线上 decks(workbench exec;Windows 需 MSYS_NO_PATHCONV=1)
export PATH="$PATH:/c/Users/Papaya/.workbench/bin"; MSYS_NO_PATHCONV=1 python tools/outputs/dump_decks.py --prod

# 2) 扫描(必须在编辑器里;菜单 Tools/Infinity/Batch Scan 或 -executeMethod InfinityBatchScan.ScanFromBatch)
#    务必分批:Run(candidates, ...) 接受过滤后的候选列表,每批 20-40 副
#    成本上限已在代码里:ScanGuardTotal=400 / ScanMinimizerMaxRuns=120(§23.5 的 OOM 教训)

# 3) 干跑上报清单(零请求)
node tools/outputs/post_loop_reports.js tools/outputs/infinity_scan_<stamp>.json

# 4) 真发(用扫描器身份;id 从本地 gitignore 文件读,别粘进对话)
node tools/outputs/post_loop_reports.js <report.json> --post --player-id "$(cat tools/outputs/_scan_reporter.txt)"

# 5) 核对线上:flag/组合/日志
workbench exec --instance-id i-uf66n1ofpudgn9b6rg7o --output json --command 'cd /var/www/onedeck/server && node scripts/inspect-db.js'
workbench exec --instance-id i-uf66n1ofpudgn9b6rg7o --output json --command 'pm2 logs onedeck-api --lines 40 --nostream'
```

**跑扫描/测试前必读**(都是这轮踩过的):

- **先 `refresh_unity` 并核对「程序集 mtime > 源文件 mtime」**;窗口失焦时 Unity 挂起编译,而 `run_tests` 匹配到 0 个测试**不会**强制重编译 → 假绿(AGENTS.md 有专条)。
- **`EditorApplication.delayCall` 在失焦时不触发**;长任务同步跑或分批。
- **不要一次性跑全量 ddmin**:关疲劳后每次 sim 都不提前结束,几百次谓词求值会把机器提交上限吃穿(§23.5,编辑器 OOM 崩溃)。
- 跑测试前保存场景;`pm2 restart` 属服务重启,执行前先说明(workbench 技能的规则)。

### 24.4 文件地图

| 面 | 位置 |
|---|---|
| 检测器 / 证据队列 | `Assets/Scripts/Managers/CombatArrangementCycleDetector.cs`、`InfinityTripJournal.cs` |
| L0 熔断 | `Assets/Scripts/Managers/CombatBudgetGuard.cs` |
| 无头仿真 / 归因 / 最小化 / 组合条目 | `Assets/Scripts/Editor/Headless/{HeadlessCombatRig,RunBudgetSim,BudgetTripReport,InfinityAttribution,ComboMinimizer,LoopReport,InfinityAttributionProcessor,InfinityBatchScan}.cs` |
| 服务端 | `server/onedeck-api/server.js`(+ `tests/loopReports.test.js`、`tests/combos.test.js`、`scripts/{backup-db,inspect-db}.js`) |
| 客户端网络面 | `Assets/Scripts/Net/{OpponentDeckCache,UploadOutbox,LoopReportUploader,NetDtos,ServerConfig}.cs` |
| 运维脚本 | `tools/outputs/{dump_decks.py,post_loop_reports.js}`(+ 其 node 测试) |
| 报告产物 | `tools/outputs/infinity_scan_*.md`(入库)、`infinity_rings_*.md`、`_exp_8889.txt`(88/89 环证据);`*.json` 含玩家名 → **gitignore** |
