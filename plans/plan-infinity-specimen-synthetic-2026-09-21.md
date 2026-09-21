# Plan: 无限侦测正控标本重建 — 合成活环卡对(2026-09-21)

日期:2026-09-21
状态:**已拍板方向(方案 2B),待执行**。
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
