# Plan: 无限侦测正控标本重建 — 合成活环卡对(2026-09-21)

日期:2026-09-21
状态:**已执行完毕(2026-09-21)**。执行记录见 §8。
上游:`plans/plan-revive-loop-mitigation-2026-09-19.md` §10(第二波加闸)、`plans/plan-infinity-detection-2026-09-17.md`(L0/L1 侦测管线,§26 criterion v2)。

## 1. 问题定义

2026-09-20 晚 RELIC_CURSE_REVIVAL 加每回合 3 次闸后,唯一活环标本 `lethal infinite test`(RELIC_CURSE_REVIVAL + CURSE_GARDENER)变成有界环,criterion v2 正确地不再 flag → 依赖「一个真实成环的输入」的 14 个管道测试转红(用户拍板暂留红灯):

| # | 测试 | 覆盖环节 |
|---|---|---|
| 1 | ArrangementCycleDetectorTests.LethalInfiniteDeck_TripsCycleDetector | 排列侦测 |
| 2 | RunBudgetSimTests.LethalSample_ReportsInfinite_AndKillsWithoutL0 | sim 判定(+击杀语义) |
| 3 | RunBudgetSimTests.LethalSample_VsSeededCurseEnemy_ReportsInfinite | sim 判定(带诅咒敌人) |
| 4 | InfinityPipelineTests.Attribution_EnemyDeckLoopsAlone_IsFlagged | 归因 |
| 5 | InfinityPipelineTests.Attribution_OwnerDeckLoopsAlone_FlagsNothing | 归因 |
| 6 | InfinityPipelineTests.LoopReport_CarriesSchemaAndBindingEvidence | 报告 schema |
| 7 | InfinityPipelineTests.Minimize_LethalSample_KeepsBothComboCards | 最小化 |
| 8 | InfinityPipelineTests.Minimize_StripsUtilityPassives_AndReverifiesTheRemainder | 最小化 |
| 9 | InfinityBatchScanTests.Scan_IncludeFlagged_ReMeasuresTheDeck | 批扫 |
| 10 | InfinityBatchScanTests.Scan_LeapingDeck_IsReportedInfiniteWithAProvenMinimum | 批扫 |
| 11 | InfinityBatchScanTests.Scan_WritesAReportThatSurvivesSerialization | 批扫报告序列化 |
| 12 | InfinityTripJournalTests.Processor_TurnsAQueuedTripIntoAComboEntry | 日志处理器 |
| 13 | LoopReportUploaderTests.ProcessPending_DoesNotTouchTheOutbox | 上报 |
| 14 | LoopReportUploaderTests.ProcessPendingAndUpload_QueuesTheAttributedReport | 上报 |

全池无活环后,正控标本只能用测试专用合成卡——与生产卡池零耦合,未来任何加闸都不再波及。

## 2. 合成卡与卡组设计

### 2.1 `TEST_LOOP_HUB` 测试卡(`Assets/Prefabs/Cards/4.0/-1_Test/TEST_LOOP_HUB.prefab`)

以 SPIRIT_CALLER 为模板文本克隆改造(沿用 apply_revive_gate_prefabs*.py 家族的做法,新脚本 `tools/scripts/make_test_loop_hub.py`,新 .meta GUID 用 uuid4 生成):

- `cardTypeID: TEST_LOOP_HUB`;`displayName` 测试枢纽;`cardDesc` 测试用无闸复活枢纽;
- 绑定保持模板形状:`OnMeRevealed → ReviveEffect.ReviveMyCards(1)`;
- `creatureFilter` 2(现象)→ **0(Any)**;**删除 `oncePerRound` 行**(=0,永无闸);`excludeSelf: 1` 保留(只排除同实例,双卡互拉不受影响);
- 其余字段沿用模板。

两张 TEST_LOOP_HUB 即构成最小同轮无限(A 揭 → 拉 B → B 揭 → 拉 A),无成本、无伤害、无外部依赖。

### 2.2 标本卡组(`Assets/SORefs/Decks/test decks/chain tests/4.0/test infinite loop.asset`)

参照 `lethal infinite test.asset` 结构(DeckSO,`deck` 列表仅含卡,Start Card 由测试侧提供):`deck` = 2 项同 GUID 的 TEST_LOOP_HUB 引用。新 .meta GUID。

### 2.3 关键安全检查(执行时验证)

- `BuildCardPrefabMap` 扫 `Assets/Prefabs/Cards` 全树(`InfinityBatchScan.cs:33`)→ 合成卡可被解析(正控需要这一点);
- unity-notion-card-sync 与卡牌提取脚本排除 `-1_Test/`(已有先例 BURY.prefab / TEST_CUTOFFANIMATION.prefab)→ 需顺手验证 `check_consistency` 不把 TEST_LOOP_HUB 报成「prefab 缺 Notion 行」,若报则在该工具加同样的排除;
- 验证商店管线不从 `-1_Test/` 抽卡(预期安全,先例在先;执行时跑一条 ShopBoardPipeline 断言或人工确认池来源)。

## 3. 测试重接(14 个,逐文件)

原则:标本名从 `lethal infinite test` 换为 `test infinite loop`;期望值里的 `CURSE_GARDENER` / `RELIC_CURSE_REVIVAL` 证据串换成 `TEST_LOOP_HUB`;凡同时钉「击杀」语义的断言拆开。

| 文件 | 处置 |
|---|---|
| ArrangementCycleDetectorTests.cs(1) | 换标本;合成环每 2 揭晓重复排列,detector 必然 trip |
| RunBudgetSimTests.cs(2) | 换标本。`LethalSample_ReportsInfinite_AndKillsWithoutL0` 拆分:无限判定改由合成标本钉(改名 `LoopingSpecimen_ReportsInfinite`);击杀语义已由 `InfiniteDeckTerminationTests.LethalInfiniteDeck_KillsStubOpponent`(现绿,不换标本)覆盖,删除 kill 半。`VsSeededCurseEnemy` 换成合成标本(枢纽不关心诅咒,环照转) |
| InfinityPipelineTests.cs(4-8) | 换标本;`LoopReport` 的证据串断言换 TEST_LOOP_HUB;`Minimize_KeepsBothComboCards` 的最小核 = TEST_LOOP_HUB×2(同 typeID 双份),断言相应改写 |
| InfinityBatchScanTests.cs(9-11) | `CandidateFromSampleDeck("lethal infinite test")` 全部换 `test infinite loop`(共 7 处调用,含未红但用同标本的) |
| InfinityTripJournalTests.cs(12) | 换标本 |
| LoopReportUploaderTests.cs(13-14) | 换标本 |

**不动**:`InfiniteDeckTerminationTests.LethalInfiniteDeck_KillsStubOpponent`(lethal 标本继续钉击杀收敛,现绿);`NonLethalInfiniteDeck_NoLongerLoopsAndMustNotTrip` / `NonLethalSample_NoLongerLoops_AndTerminatesNaturally`(non-lethal 标本继续钉「不再成环」,现绿);`lethal infinite test` / `non-lethal infinite test` 两个旧卡组资产原样保留。

## 4. 防回归锁

`ReviveOncePerRoundPrefabTests`(或新建的 InfinitySpecimenTests)加一条永久断言:**TEST_LOOP_HUB 的 oncePerRound == 0 且卡组资产含恰好 2 份** —— 防止未来加闸清扫/卡组编辑误杀正控。

## 5. 验证

目标:全量 EditMode 回到全绿(649 + 新增),14 红归零。执行后用 LoopDetection 选项手跑一次 `test infinite loop`  sanity(proven infinite)。

## 6. 明确不做

- 不动任何生产代码(RunBudgetSim / 扫描器 / 归因器零签名改动 —— 这是 2B 对 2A 的核心优势);
- 不动 lethal / non-lethal 旧标本资产与 InfiniteDeckTerminationTests;
- 不上服务器、不同步 Notion、不动商店池;
- 2A(Options 级 prefab override 注入口)存档备选,本期不取。

## 7. 执行顺序

1. `make_test_loop_hub.py` 生成 prefab + 卡组资产,AssetDatabase 刷新确认解析;
2. §2.3 三项安全检查;
3. §3 逐文件重接;
4. §4 防回归锁;
5. 全量回归 + 执行记录回填本节下方。

## 8. 执行记录(2026-09-21)

- **生成**:`tools/scripts/make_test_loop_hub.py`(LF,沿用 apply_revive_gate_prefabs 家族模式,严格断言 + DO-NOT-RERUN 头)产出:
	- `Assets/Prefabs/Cards/4.0/-1_Test/TEST_LOOP_HUB.prefab`(guid `87e2e1691e244ee19872f950d70483b9`)——SPIRIT_CALLER 文本克隆,与模板 diff 恰好 6 处:根 m_Name / cardTypeID → TEST_LOOP_HUB、displayName → 测试枢纽、cardDesc → 测试用无闸复活枢纽、creatureFilter 2 → 0(Any)、oncePerRound 行删除;excludeSelf 1 与 OnMeRevealed 绑定不变。
	- `Assets/SORefs/Decks/test decks/chain tests/4.0/test infinite loop.asset`(guid `abdeddb9f49a4a48ada4d21840914ebe`)——DeckSO,deck = 2 份同 GUID 的 TEST_LOOP_HUB 根 GameObject 引用。
- **§2.3 三项安全检查全过**:
	1. `BuildCardPrefabMap`(`InfinityBatchScan.cs:481`)扫 `Assets/Prefabs/Cards` 全树,编辑器探针确认 `mapHasHub=True`;并把该断言永久钉进 `InfinityBatchScanTests.PrefabMap_ResolvesTheComboCards`。
	2. `tools/outputs/extract_unity_cards_40.py` 已自带 `EXCLUDE_MARKERS = ("-1_Test",)`(第 18 行),文件夹在产出行之前被整体跳过 → TEST_LOOP_HUB 不可能进入 `unity_cards_40_current.json`,check_consistency 不会报「prefab 缺 Notion 行」;无需改工具。
	3. 商店池 = `ShopPoolRef.asset` 显式 GUID 列表(112 项,无文件夹扫描),探针确认 0 项落在 `-1_Test/`;`ShopBoardPipeline.GenerateBoard` 只消费注入池,天然安全。
- **§3 重接**:7 个文件 14 个测试全部换用 `test infinite loop`;期望值证据串换 TEST_LOOP_HUB。`LethalSample_ReportsInfinite_AndKillsWithoutL0` 拆为 `LoopingSpecimen_ReportsInfinite`(击杀半删除,由 InfiniteDeckTerminationTests 覆盖);`LethalSample_VsSeededCurseEnemy_ReportsInfinite` → `LoopingSpecimen_VsRealEnemyDeck_ReportsInfinite`(击杀断言拆出:枢纽无伤害,该对局由生产疲劳时钟随机结束,与 flag 无关);`LethalInfiniteDeck_TripsCycleDetector` → `LoopingSpecimen_TripsCycleDetector`(删除 EnableProductionTriggerWiring 调用——枢纽不需要诅咒接线)。lethal / non-lethal 旧资产、InfiniteDeckTerminationTests、其余未红测试未动。
- **§4 防回归锁**:新建 `InfinitySpecimenTests.cs`(避开另一并行会话持有的 ReviveOncePerRoundPrefabTests.cs,用户 2026-09-21 拍板):`HubIsAnUngatedReviveHub`(oncePerRound==0、creatureFilter==0、excludeSelf==true)+ `SpecimenDeckIsExactlyTwoHubs`(deck 恰好 2 份同 GUID)。
- **§5 验证**:
	- LoopDetection sanity(标本生成后即跑):`infinite=YES cycles=19(p=1) starved=True repeats=199`,L0-concluded,proven infinite。
	- 定向回归 8 类 50/50 绿;全量 EditMode **653/653(652 过 + 1 既存 Ignore),0 失败**;TestResults.xml 逐一复核 19 个关键用例(14 重接 + 2 防回归锁 + 3 保留绿)全部 Passed。
- **过程插曲**:第一次全量 run_tests 被一次域重载(与另一会话的编译交错)杀掉,bridge 报 "Test job failed to initialize";按既定预案(SaveOpenScenes + 清死任务)重试一次即通过。场景最终 clean。
- **未做**(按 §6):生产代码零改动、lethal / non-lethal 资产与 InfiniteDeckTerminationTests 未动、未上服务器、未同步 Notion、未动商店池。
- **未提交**:用户未要求 commit,全部改动留在工作区(与另一会话的 RIFT_REVIVER 闸门镜像改动同区,互不重叠)。
