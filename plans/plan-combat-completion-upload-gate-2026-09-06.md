# 计划：上传门禁——完成过一次战斗才上传/记录统计数据

日期：2026-09-06
上游：`plans/plan-async-pvp-client-2026-09-03.md`（§2.3 outbox / §2.4 幽灵牌库 / §2.5 对局报告 / §2.6 单局记录 / §2.7 累计统计快照 / §2.8 卡目录上传）
需求（用户拍板 2026-09-06）：**所有上传与本地记录的数据都必须来自"完成过至少一次战斗"的玩家**。没打完任何一场战斗就退出的玩家，不产生服务器数据，也不写入本地统计文件。

## 1. 现状审计（2026-09-06 全链核对）

| 数据路径 | 触发点 | 现状 | 判定 |
|---|---|---|---|
| 对局报告 `/api/matches/report` | 战斗结算处（PhaseManager.cs:206） | 完成战斗后才发，且依赖 ghost 对局存在 | ✅ 合规 |
| 单局记录 `/api/runs`（RunRecorder） | `CloseRun` / 残局恢复 | 双重防御：零战斗的局永不上传（RunRecorder.cs:374、389） | ✅ 合规 |
| 胜负记录 `card_winrate.json` | `RecordCombatResult`，战斗结束时 | 天然合规 | ✅ 合规 |
| 累计统计快照 `/api/stats/snapshot` + `shop_stats.json` | 商店退出 `CommitStagedVisit()` + `UploadIfDirty()`（PhaseManager.cs:540-542） | 第一场战斗**打完前**就提交 + 落盘 + 上传 | ❌ 违规 |
| 幽灵牌库 `/api/decks`（DeckSnapshot） | 场景 `onEnterCombatPhase` UnityEvent 绑定 `SavePlayerDeckSnapshot`（GameScene.unity:12767） | 每次进战斗即上传，时机在战斗开始**前**；开局牌库会进 ghost 池 | ❌ 违规 |
| 敌方来源计数 `enemy_source_counters.json` | 进战斗选牌库时 `RecordEnemySource`（DeckSaver.cs:346、352） | 战斗放弃也计数，随后随快照 meta 上传 | ❌ 违规 |
| 卡目录 `/api/cards/catalog` | 场景启动 `MaybeUpload()`（PhaseManager.cs:100）+ 注册成功补传（UsernameRegistrationPanel.cs:170） | 与战斗完全无关 | ❌ 按口径纳入门禁 |
| 服务器端 | `requirePlayer` 仅身份校验 | 无任何战斗门禁 | （可选加固，见 §2.7） |

- 豁免：注册 `/api/players/register`（身份基础设施，playerId 是全部数据的关联键，不含玩法数据）。
- 读取不受限：ghost 牌库拉取（GET `/api/decks/opponents`）是读操作，不在本口径内。
- 口径拍板：
	- "完成一次战斗" = 到达 PhaseManager 战斗结算处（`Update()` 战斗分支、`RunRecorder.RecordCombatEnd` 所在位置）。平局也算完成（结算处可达，与 RunRecorder 计 combats 一致）。
	- 教程战斗不算：教程分支提前 return（PhaseManager.cs:144-152），哨兵放在结算处天然排除，与 RunRecorder / 统计簿记口径一致。

## 2. 方案

### 2.1 哨兵 `CombatCompletionGate`（新增 `Assets/Scripts/Net/CombatCompletionGate.cs`）

- 静态类，持久化 `persistentDataPath/has_completed_combat.flag`（只判存在性，不存内容语义）。
- `HasCompletedCombat`：内存缓存 + 首次读盘；`MarkCompleted()`：幂等置位（写文件 + 置缓存），首次置位时补传卡目录（§2.4）。
- Test seam：`OverrideDirectoryForTests` + `ResetForTests`，与 `UploadOutbox` / `RunRecorder` 同款。
- 哨兵是"玩家经历过战斗"的终身事实，**不**跟随 `resetOnStart` / Ctrl+Shift+R 统计重置（与身份文件同理正交）；flag 被删则自愈型回退为未完成，下一场战斗结束重新置位，无历史数据丢失。

### 2.2 置位点：PhaseManager 战斗结算处

- 位置：`Update()` 战斗分支，`RunRecorder.RecordCombatEnd(...)` 之前（PhaseManager.cs:207 附近），一行 `CombatCompletionGate.MarkCompleted()`。
- 独立调用、不塞进 `RunRecorder.RecordCombatEnd` 内部：后者有 `current == null || ended` 早退，哨兵不应被 RunRecorder 的状态牵连。
- 结算处顺序（本计划新增动作汇聚点）：`MarkCompleted()` → `CommitStagedEnemySource()`（§2.6）→ `CommitStagedVisit()`（§2.3）→ `UploadIfDirty()`（§2.3 首次合规上传点）。

### 2.3 ShopStats 提交时机后移（同时修"记录"与"上传"）

- `CommitStagedVisit()` 调用点从 `ExitingShopPhase`（PhaseManager.cs:540）移到 §2.2 结算处。效果：没打完战斗就退出的玩家，staged 数据只活在内存，退出即丢——本地 JSON 不落盘、服务器不上传。
- sessionNum 语义验证过不变：`sessionNum.value++` 在 Result→Shop 切换时（PhaseManager.cs:228），战斗结算时仍是本局编号，桶归属与现状一致。
- 上传时机：结算处 `CommitStagedVisit()`（内部 MarkDirty）之后紧跟一次 `StatsSnapshotUploader.UploadIfDirty()`（首次合规上传：含商店第 1 次访问 + 第 1 场胜负）；商店退出的 `UploadIfDirty()`（PhaseManager.cs:542）保留为重试触发。
- `StatsSnapshotUploader.UploadIfDirty()` 加哨兵门禁：未完成过战斗时**不清 Dirty 直接 return**（数据不丢，下次触发重试）。现状是先清再传、失败才回置——门禁必须放在清位之前。
- 注释同步：PhaseManager.cs:537-539 与 ShopStatsManager.cs:104-107 的 "exiting the shop is the only path into combat" 理由注释改写为战斗结算口径。
- `RunRecorder.CloseShopVisit`（PhaseManager.cs:547）**不动**：它由 run 层零战斗门禁兜住（中途退出 → 无 run_end → 恢复时 combats==0 不上传）。

### 2.4 卡目录上传后移

- `CardCatalogUploader.MaybeUpload()` 从 `PhaseManager.OnEnable`（PhaseManager.cs:100）移除，改由 `CombatCompletionGate.MarkCompleted()` 首次置位时调用（`catalog_version.txt` 版本幂等不变，重复调用安全）。
- `UsernameRegistrationPanel.HandleResult` 成功路径的 `MaybeUpload()` 补传（UsernameRegistrationPanel.cs:170）**删除**（`UploadOutbox.Flush()` 保留）：注册成功时必然还没打完战斗，补传必被哨兵拦下；置位点已覆盖该需求。

### 2.5 DeckSnapshot 哨兵

- `DeckSaver.UploadDeckSnapshot()` 入口加 `if (!CombatCompletionGate.HasCompletedCombat) return;`（DeckSaver.cs:277 附近）。放私有方法而非 `SavePlayerDeckSnapshot`：场景 UnityEvent 绑定（GameScene.unity:12767）与 Ctrl+S 热键（DeckSaver.cs:464）两条路一起被拦。场景绑定本身不动。
- 效果：第一场战斗开始前的开局牌库快照被拦（永不成 ghost）；战斗 1 完成后商店改牌，进战斗 2 时上传的快照自然覆盖。符合口径：没打过战斗的玩家的牌不该进 ghost 池。
- 教程守卫已在（`SavePlayerDeckSnapshot` 的 `IsTutorialActive` return），哨兵与之并列。

### 2.6 敌方来源计数后移

- `OpponentDeckCache` 新增 `stagedSource` 静态字段 + `StageEnemySource(source)`（暂存）+ `CommitStagedEnemySource()`（落到现有 `RecordEnemySource` 计数并落盘）；`ResetCacheForTests` 同步清 staged。
- `DeckSaver.PopulateEnemyDeckBySessionNumber` 的两处 `RecordEnemySource`（DeckSaver.cs:346、352）改调 `StageEnemySource`；调试牌库路径维持不经过、不计数（现状不变）。
- `CommitStagedEnemySource()` 挂 §2.2 结算处（在 `CommitStagedVisit` 之前），计数口径 = 完成过的战斗；随后 §2.3 的快照上传 meta 自然携带正确计数。
- ghost 注入（`TakeCandidate` / `SetCurrentOpponent`）与 prefetch 拉取全部不动。

### 2.7 服务器端纵深加固（可选，默认不做）

- `/api/decks` 与 `/api/stats/snapshot` 可查该玩家是否已有战斗记录（runs/combats>0）才接受。客户端门禁已满足需求，此条仅防伪造与未来客户端 bug。列为后续可选项，本期不动服务器。

## 3. 文件改动

| 文件 | 改动 |
|---|---|
| `plans/plan-combat-completion-upload-gate-2026-09-06.md` | 本文档（新增） |
| `Assets/Scripts/Net/CombatCompletionGate.cs` | 新增：哨兵（flag 文件 + `MarkCompleted` + `HasCompletedCombat` + test seams + 首次置位补传卡目录） |
| `Assets/Scripts/Managers/PhaseManager.cs` | 结算处：`MarkCompleted` + `CommitStagedEnemySource` + `CommitStagedVisit` 后移 + `UploadIfDirty`；`ExitingShopPhase` 移除 `CommitStagedVisit`（保留 `UploadIfDirty`/`Flush`/`CloseShopVisit`）；`OnEnable` 移除 `MaybeUpload`；注释更新 |
| `Assets/Scripts/Net/StatsSnapshotUploader.cs` | `UploadIfDirty` 加哨兵门禁（未完成不清 Dirty） |
| `Assets/Scripts/Managers/WriteRead/DeckSaver.cs` | `UploadDeckSnapshot` 加哨兵；两处 `RecordEnemySource` 改 `StageEnemySource` |
| `Assets/Scripts/Net/OpponentDeckCache.cs` | 新增 `StageEnemySource` / `CommitStagedEnemySource` / `stagedSource`；`ResetCacheForTests` 清 staged |
| `Assets/Scripts/Net/UsernameRegistrationPanel.cs` | 删除成功路径的 `CardCatalogUploader.MaybeUpload()`（保留 `Flush`） |
| `Assets/Scripts/Managers/ShopStatsManager.cs` | 无代码改动（只动调用点）；`CommitStagedVisit` 注释同步口径 |
| `Assets/Scripts/Editor/Tests/CombatCompletionGateTests.cs` | 新增：置位幂等 / 文件持久 / override 隔离 / 首次置位触发卡目录 |
| `Assets/Scripts/Editor/Tests/StatsSnapshotUploaderTests.cs` | 现有用例 Setup 置位哨兵；新增"门禁不过不清 Dirty 不发请求"用例 |
| `Assets/Scripts/Editor/Tests/OpponentDeckCacheTests.cs` | 新增 staging→commit 计数用例 |
| `Assets/Scripts/Editor/Tests/ShopStatsManagerTests.cs` | 不改（用例直调 `CommitStagedVisit`，不受调用点移动影响） |
| `Assets/Scripts/Editor/Tests/RunRecorderTests.cs` | 不改（零战斗门禁已覆盖） |

## 4. 测试

- EditMode（全部 hermetic，无网络）：
	- 哨兵：`MarkCompleted` 幂等、flag 文件落盘、跨 `ResetForTests` 重载后仍为已完成、`OverrideDirectoryForTests` 隔离。
	- 门禁：`UploadIfDirty` 未完成时保留 Dirty 且不触碰网络；完成后正常清位。
	- 计数：`StageEnemySource` → `CommitStagedEnemySource` 落盘计数；调试路径不计数；`ResetCacheForTests` 清 staged。
	- 卡目录：首次置位恰触发一次 `MaybeUpload` 路径（版本文件幂等由现有逻辑保证）。
- 手工（合入下个联网批次一并）：新档 → 逛商店买卡 → ESC 退出 → 检查服务器无 stats/decks/catalog、本地无 shop_stats.json；重开 → 打完第一场 → 结算后检查快照上传 + counters + catalog 到位；admin 端核对。
- 回归：现有 EditMode 全量。

## 5. 执行状态（2026-09-07 实施完成）

| 步骤 | 内容 | 状态 |
|---|---|---|
| 1 | 哨兵类 + 结算处置位 + DeckSnapshot / `UploadIfDirty` 门禁（最小闭环） | ✅ |
| 2 | ShopStats 提交后移 + 结算处上传 + 注释更新 | ✅ |
| 3 | 敌方来源计数 staging | ✅ |
| 4 | 卡目录后移（OnEnable / 注册面板两处） | ✅ |
| 5 | EditMode 测试补充 + 全量回归 | ✅ |

实施记录（2026-09-07）：

- `MarkCompleted()` 每次结算都调用（非仅首次）：置位幂等，且每次都跑 `CardCatalogUploader.MaybeUpload()`——版本幂等使重复调用零成本，同时让老玩家版本更新后第一场战斗就能补传新版本卡目录。
- 测试：新增 `CombatCompletionGateTests`（6 用例，含卡目录回传端到端：手写 identity 文件 + DeckSaver/DeckSO 脚手架 + outbox 文件断言，全程无网络）；`StatsSnapshotUploaderTests` SetUp 置位哨兵 + 新增关门禁保留 Dirty 用例；`OpponentDeckCacheTests` 新增 staging→commit 用例。
- 回归：门禁相关 6 个测试类 45/45 绿；全量 EditMode 485 中 483 过，2 个失败为 `AfterShuffleTimingTests` 存量问题（stash 掉本次全部改动后依然失败，已验证与门禁无关）。
- §2.7 服务器端加固：未做（按计划列为后续可选）。
