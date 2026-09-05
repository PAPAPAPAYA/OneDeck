# 移除敌方卡组 local json 中间层（无 ghost 直落 default pool）

日期：2026-09-04
状态：**待执行**。需用户确认决策点并明确「修改代码」后再动代码。
背景：敌方卡组 fallback 链现为 `debug > server ghost > local json (deckdata.json) > default pool`。目标：删除 local json 层，无 ghost 时直接落 default pool；数据上报侧同步收敛。

## 1. 现状基线（2026-09-04 实测）

- 链路入口：`DeckSaver.PopulateEnemyDeckBySessionNumber`（DeckSaver.cs:437）。GameScene 中 PhaseManager 的 `onEnterCombatPhase` UnityEvent 以旧名 `LoadJsonToEnemyDeckSo` 调用它（GameScene.unity:12740），随后同一事件还调用 `SavePlayerDeckToJson`（GameScene.unity:12752）。
- local json 读：`TryLoadFromJson`（DeckSaver.cs:531），按 sessionNum 匹配 deckdata.json 里**本次 app 启动以来**保存的玩家卡组（场景 `switchOnSaveLoad: 1`、`resetOnStart: 1` → 每次启动清空、当次启动内有效），命中后 `RecordEnemySource(SourceLocal)`（DeckSaver.cs:463）。
- local json 写：`SavePlayerDeckToJson`（DeckSaver.cs:363）= 写 deckdata.json + `UploadDeckSnapshot`（ghost 快照上传，outbox 背书）。读写两半共用 `CreateDeckSaveEntry`。
- 遥测上报面（batch C）：
  - `OpponentDeckCache.EnemySourceCounters`（server/local/pool 生命周期计数，持久化 enemy_source_counters.json，永不重置）。
  - `StatsSnapshotUploader.BuildRequest` → `StatsEnemySource.local` → POST /api/stats/snapshot 的 `meta.enemySource`。
  - server：stats_meta 的 `enemy_source_local` 列（可空 default 0，COALESCE upsert，server.js:121/243）；admin 仪表盘 "local json fallback" 列（server.js:818-830）。
  - **关键行为**：客户端若发送 meta.enemySource 对象但缺 `local` 字段，server 端 `toInt(undefined,...)` 返回 0 而非 null（server.js:513），COALESCE 会用 0 覆盖该玩家已累计的 local 计数。
- run journal（batch D）：`RunCombatEntry.opponentDeckId` 非 ghost 时恒 0，无 per-combat source 字段 → 本改动不涉及 RunRecorder。
- 测试引用面：`OpponentDeckCacheTests.RecordEnemySource_CountsAndPersistsAcrossReload`（只断言 counters.local 初始为 0）、`StatsSnapshotUploaderTests`（local=2 的 DTO 映射）。两者只依赖字段存在，不依赖 local 分支被调用。

## 2. 决策点

**D1 移除范围**
- B（推荐）：读写一起删。读侧删除后，deckdata.json 写入成为每场战斗的无效 IO + 死数据堆积，且归档计划 §4 关心的 v1 残留 deckdata.json 纯净性问题被永久解决。
- A（最小）：只删 fallback 读分支，保留 json 写。会留下死方法 TryLoadFromJson 与误导性方法名。

**D2 遥测 local 字段：保留（推荐）**
`SourceLocal` 常量、`EnemySourceCounters.local`、`StatsEnemySource.local`、server 列、仪表盘列全部不动；只删 DeckSaver 里的 `RecordEnemySource(SourceLocal)` 调用点。效果：历史 local 计数冻结保留并继续随快照上传，仪表盘 local 列从此不再增长但历史值有效。
（若选删字段：客户端停发 local → server 按上述关键行为把存量 local 清零，还得多改 server 一处；不推荐。）

**D3 场景绑定方法名**
`SavePlayerDeckToJson` 若改名需同步改 GameScene.unity 的 m_MethodName（顺带把 :12740 的过时名 LoadJsonToEnemyDeckSo 指向现名）。改名 + 场景重绑（推荐，语义干净）；或保留旧名避免动场景（留误导性名字）。

**D4 空池行为（必须知晓，非本方案引入的回归）**
v1 归档后 pool[0]~[7] 为空（仅 8/9 有手工卡组，超界 clamp 到最后一个 entry）。无 ghost + json 层删除后，离线打 session 0-7 → `PopulateFromDefaultDecks` 空池 warning + return → enemyDeckToPopulate 维持旧内容（本启动首战 = DeckSO 初始序列化内容；后续场次 = 上一场敌人卡组）。今天该路径已可达（resetOnStart 清空后的首战），但 json 层删除后，离线场景从「同一启动内第二次 run 起被 json 遮蔽」变为「常态」。缓解选项：
- v2 开发期接受（联网时 ghost 覆盖绝大多数对局）；
- 提前录 v2 卡组，用 skill `check-default-enemy-deck-pool --fix` 回填池；
- 可选代码兜底：空池时 fallback 到 debugEnemyDeck（数行改动，是否要加由用户定）。

## 3. 改动清单（按 B + D2 保留字段 + D3 改名）

引擎（DeckSaver.cs）：
1. `PopulateEnemyDeckBySessionNumber`：删 TryLoadFromJson 分支（:460-465），方法注释改为 `debug > server ghost > default pool (plan §2.5)`。
2. 删 `TryLoadFromJson` 方法。
3. `SavePlayerDeckToJson` → 改名 `SavePlayerDeckSnapshot`（D3 选保留旧名则跳过改名）：保留 Tutorial 挡板、`CreateDeckSaveEntry`、`UploadDeckSnapshot`；删 `_currentData.savedDecks.Add(deckEntry)` 与 `SaveData()` 调用。
4. 删 `switchOnSaveLoad` / `resetOnStart` 字段、`LoadData` / `SaveData` / `MigrateOldData` / `WipeDeckSaves` / `PrintSavedDecksInfo` 方法、`_currentData` / `_savePath` 字段；`Start()` 只留 `BuildCardDatabaseCache()`；类头注释（:8-12）与 `useDebugEnemyDeck` tooltip 同步改写。
5. 行为变化说明：删 `switchOnSaveLoad` 后 ghost 快照上传不再被该开关挡（原来开关关 = 不上传）；场景当前为 ON，无实际差异。`GetCardPrefabByTypeID`（EnemyDeckRecorder 在用）与 `sessionNumber` 等引用不动。

数据文件（DeckData.cs）：删 `DeckData` 类，保留 `DeckSaveEntry`（上传管线内部载体，字段原样）。该文件现为 4 空格缩进，编辑时按仓库规范转 Tab + CRLF。

遥测（不改面）：OpponentDeckCache.cs / StatsSnapshotUploader.cs / NetDtos.cs / server.js 全部不动（D2）。RunRecorder.cs 不动。

场景（GameScene.unity，仅当 D3 选改名；Unity 关闭或空闲时文本编辑，或 Editor 内重绑 UnityEvent）：
- `m_MethodName: LoadJsonToEnemyDeckSo` → `PopulateEnemyDeckBySessionNumber`
- `m_MethodName: SavePlayerDeckToJson` → `SavePlayerDeckSnapshot`
- DeckSaver 上残留的 `switchOnSaveLoad: 1` / `resetOnStart: 1` 序列化行无害，可留可删。

文案：TestManager.cs:29 tooltip "bypasses JSON save and default pool" → "bypasses ghost fetch and default pool"。

文档：plans/plan-recorded-deck-pool-archive-2026-09-04.md 第 15 行优先级链更新为 `debug > server ghost > default pool`（或加注 local json 层已移除）。AGENTS.md 未记载该链路，无需改。

## 4. 执行步骤

1. 代码改动（§3）。
2. 场景绑定改名（Unity 关闭时文本改，或 Editor 里重绑）。
3. 一次性手动删 persistentDataPath/deckdata.json（可选；此后代码不再读它）。
4. 验证（§5）。
5. 文档更新；引擎 + 场景 + 文档单独成一个 commit，不与其它在途改动混合。

## 5. 验证

- 编译无错（refresh_unity + console 检查）。
- EditMode 网络测试（hermetic，非 Play Mode）：OpponentDeckCacheTests、StatsSnapshotUploaderTests、RunRecorderTests、NetClientBatchATests 全绿。
- Play Mode 手验：
  - ServerConfig 关 + `useDebugEnemyDeck=false`：进战斗，行为不再有 json 读取；session 8/9 出池内手工卡组；session 0-7 出空池 warning（D4 预期行为）。
  - ServerConfig 开 + 已注册身份：ghost 正常注入 VS 显示与战报上报；enemy_source_counters 只有 server/pool 增长，local 冻结。
  - 商店退出触发 stats snapshot 上传；admin 仪表盘三列仍在，local 列值不增。
  - run journal：非 ghost 战斗 opponentDeckId 仍为 0。

## 6. 回滚

- revert 该 commit（代码 + 场景同 commit）。deckdata.json 写路径随代码恢复；enemy_source 计数文件从未被删，无数据损失。

## 7. 风险

- 场景文本编辑撞上 Unity 占用文件 → 关闭 Unity 或等空闲再改（同归档计划教训）。
- 改名后漏改场景绑定 → UnityEvent 显示 Missing，进入战斗即报错；§5 验证覆盖。
- 离线 + pool[0]~[7] 空 = 空敌人（D4）是已知接受项，不是本方案回归；如需兜底见 D4 第三选项。
