# 异步 PvP 客户端实施计划（Unity 侧）

日期：2026-09-03
上游：服务器端已上线（`server/onedeck-api/`，ECS `8.153.150.197`，Express + better-sqlite3，pm2 托管 + Nginx 反代，端到端验证通过）；本文件只做 Unity 客户端
状态：**待实施**
审核：2026-09-04 补数据模型增补（新增 §0.1，§2.5-2.8 相应扩充，批次表加 S0）、勘误 §5 的 .gitignore 声明、扩写 §3 开关面板（本地/云端环境 + 分项上传开关）
## 0. 服务器端既成事实

- Base URL：`http://8.153.150.197`（HTTP+IP 起步；将来买域名备案后切 HTTPS，只改一处配置）。
- 接口（详见 `server/onedeck-api/README.md`）：

| 方法 | 路径 | 幂等键 |
|------|------|--------|
| POST | `/api/players/register` | 用户名唯一（409 冲突） |
| POST | `/api/decks` | 追加式，无需幂等 |
| GET | `/api/decks/opponents?playerId&gameVersion&maxSession&perSession` | 只读 |
| POST | `/api/matches/report` | `reportId` |
| POST | `/api/stats/snapshot` | 按 (playerId,kind,version,card,session) upsert，重传安全 |
| POST | `/api/runs` | `runId` |
| POST | `/api/cards/catalog` | 按 (version,cardTypeID) upsert |
| GET | `/api/health` | — |

- 身份模型：注册后服务器下发 `playerId`（GUID），即全部接口的凭证；无密码。
- 管理后台：`/admin?token=...`（token 存服务器 `data/admin_token.txt`）。

### 0.1 服务器 schema 增补（2026-09-04 评审新增，先于客户端批 A 部署）

服务器已上线但尚无真实数据，现在加列成本最低。全部为加列/加字段，不破坏既有幂等语义；better-sqlite3 启动时判重后 `ALTER TABLE ADD COLUMN`，admin 页同步加展示列。

| 位置 | 增补 | 用途 |
|------|------|------|
| `run_combats` | 加 `rounds` INTEGER | 战斗轮次（战斗内节奏）；口径与 Result 屏一致 = `roundsLastCombat - 1` |
| `run_combats` | 加 `opponent_deck_id` INTEGER NULL | 该场对手幽灵 deckId（本地兜底时为空）→ 与 match_reports 对账，算「打 session N 幽灵的胜率曲线」 |
| `run_combats.per_card` | 单一 `damageDealt` 拆为 `damageToOpponent` / `damageToSelf` | Tracker 本就区分两者，客户端零成本；自伤型卡牌代价侧可见 |
| `stats_meta` | 加 `enemy_source_server` / `enemy_source_local` / `enemy_source_pool` 计数列 | 回退遥测：判断 ghost 池真实覆盖率（冷启动期关键样本代表性） |

per_card 的 heal/shield 计数不在本期：Tracker 无现成 hook，需改 HPAlterEffect 治疗分支，列为二期可选。辅助型卡牌在 perCard 侧仍不可度量（只能靠胜率关联），已知限制。

## 1. 客户端总体结构

新增目录 `Assets/Scripts/Net/`（纯新增，不动既有架构）：

```
ServerConfig        ScriptableObject（Resources/ServerConfig.asset）：环境/分项开关面板，字段详设见 §3.1
PlayerIdentity      常驻单例：用户名注册流程，identity.json 持久化
DeckNetworkClient   UnityWebRequest 封装：GET/POST JSON、超时、指数退避重试、UTF-8
UploadOutbox        发件箱：outbox.json，失败请求积压重试，上限 100 条丢最旧
OpponentDeckCache   对手卡组缓存：开局批量预取 + 商店补仓 + 磁盘缓存 + 本局去重
RunRecorder         对局记录：runId、current_run.jsonl 增量落盘、结局整包上传
```

既有代码改动点（最小侵入）：

- `DeckSaver.PopulateEnemyDeckBySessionNumber()`（DeckSaver.cs:413）：回退链最顶端插入"服务器缓存卡组"分支，优先级 debug → **服务器** → 本地 JSON → 默认池。
- `DeckSaver.SavePlayerDeckToJson()`（DeckSaver.cs:363）：成功后追加一次卡组快照上传（发件箱兜底）。
- `ShopStatsManager` / `CardWinRateTracker`：记录方法加 `sessionNum` 参数，存储改 (card, session) 分桶（JsonUtility 不支持 Dictionary，用平铺 List）。
- `CombatInfoDisplayer` 区域：战斗 UI 显示 "VS {对手用户名}"。

## 2. 模块详设

### 2.1 PlayerIdentity

- 首次启动弹输入框（2-16 字符，可跳过则随机 `玩家#XXXX`）；调 `/api/players/register`，409 则提示换名。
- 成功后在 `persistentDataPath/player_identity.json` 存 `{playerId, username}`；之后所有请求携带。
- 断网/失败：标记 `registered=false`，每次进商店重试，成功前联机功能静默降级。

### 2.2 DeckNetworkClient

- `Post<TReq,TRes>(path, dto, onOk, onFail)` / `Get<TRes>(path, query, onOk, onFail)`，协程驱动。
- 超时 10s；失败重试 2 次（1s/3s 退避）；仍失败交给调用方决定（入发件箱或走回退）。
- DTO 用 `[Serializable]` class + `List<string>`（JsonUtility 兼容，禁止 Dictionary）。
- 注意 Windows 编辑器控制台 curl 的中文 GBK 坑与 Unity 无关——UnityWebRequest 始终发 UTF-8 字节，服务端已验证正确存储。

### 2.3 UploadOutbox（发件箱）

- 持久化 `persistentDataPath/outbox.json`：`List<PendingRequest>{kind, jsonPayload, enqueuedAt}`。
- 触发冲刷：游戏启动、离开商店、对局结束。每条成功后移除；上限 100 条，溢出丢最旧。
- 卡组快照与战绩上报是事件型，走发件箱；统计快照是累计型，只记"脏标记"，下次直接发最新全量，不进队列。

### 2.4 OpponentDeckCache（对手卡组）

- **开局预取**：进主界面/开新一局时 `GET /api/decks/opponents?maxSession=6&perSession=2`。
- **商店补仓**：每次进商店检查 session N+1 候选数，不足则补拉。
- **磁盘缓存** `opponent_cache.json`：开局就断网时用上次缓存兜底。
- 本局内记录已用 `deckId`，同一局不重复匹配同一副卡组。
- 消费校验：取候选时逐张过 `DeckSaver.GetCardPrefabByTypeID`，**含未知 cardTypeID 整副弃用**换下一张，再空则落入既有回退链（本地 JSON → 默认池），任何时刻不断网可玩。

### 2.5 战斗注入与战绩上报

- `PopulateEnemyDeckBySessionNumber` 服务器分支：把候选写入 `enemyDeckToPopulate.deck`，`enemyStatusRef.hpMax = entry.hpMax`（沿用 JSON 分支同款逻辑），并暂存 `deckId/username` 供 UI 与上报。
- 注入时（含回退到本地 JSON / 默认池时）累计敌方来源计数器（server/local/default），随 §2.7 快照全量上传；服务器分支另暂存 `deckId` 供 combat_end 写入 `run_combats.opponent_deck_id`（§0.1）。
- 战斗结束（结果已知时）上报 `POST /api/matches/report`：`reportId=Guid.NewGuid()`、`opponentDeckId`、`won`、`sessionNum`；入发件箱。
- **时序注意**：`CombatPerCardStatsTracker.BeginSession()` 在 `GatherDecks()` 清数据，战斗统计必须在结果阶段、下一场 `GatherDecks` 之前收割。

### 2.6 RunRecorder（对局记录）

- `run_start`：`PhaseManager.ResetRun` 时生成 `runId=Guid`，清空 `seenCardTypeIDs`（HashSet）。
- `shop_visit`（离开商店时落盘一条）：
  - `offered`/`utilityOffered`：本轮刷出卡（商店生成点埋点）；`bought[]`：本阶段购买（可多张可重复可为空）
  - `rerollCount`：本阶段刷新次数；`goldEnter`（payday 前）、`goldAfterPayday`（消费前购买力）、`goldExit`（离场，含刷新花费）
  - `seenPoolPct`：本局已见过卡种数 / 商店卡池 distinct cardTypeID 数（分母含功能板卡；口径如需调整再议）
- `combat_end`：sessionNum、输赢、剩心、`rounds`（= roundsLastCombat - 1）、`opponentDeckId`（可空）、每卡触发次数 / `damageToOpponent` / `damageToSelf`（取自 CombatPerCardStatsTracker，不再合并伤害；schema 见 §0.1）。
- `run_end`：result(victory/defeat/abandoned)、finalSession、heartsLeft、finalDeck、最终 seenPoolPct → 整包 `POST /api/runs`（发件箱兜底）。
- 增量落盘 `current_run.jsonl` 防崩溃；下次启动发现未完结记录 → 补 `abandoned` 后上传。
- **零战斗剔除（2026-09-05）**：`combats` 为空的 run（第一场战斗完成前退出，含战斗中途退出）永不上传——恢复路径与 `CloseRun` 兜底双重门禁，服务器 `/api/runs` 同口径跳过入库（返回 ok 让 outbox 丢弃）。

### 2.7 统计快照上传

- 两个追踪器加 session 分桶后，上传载荷 = 两个追踪器全量分桶 + meta(totalShopVisits/totalRerolls) + gameVersion。
- 时机：离开商店时若脏则传；服务器 upsert 保证重传安全。
- meta 除 `totalShopVisits`/`totalRerolls` 外，附敌方来源计数（server/local/default，§0.1），随快照全量覆盖。
- **零战斗剔除（2026-09-05）**：商店统计改为 per-visit 暂存，`PhaseManager.ExitingShopPhase`（离店 = 进入战斗的唯一路径）先 `CommitStagedVisit()` 再 `UploadIfDirty()`；商店内直接退出/崩溃的 visit 不计入累计值。代价：商店内退出会丢该次 visit 的商店数据。

### 2.8 卡牌目录上传

- 启动时（注册成功后）若本地记录的 catalogVersion != 当前版本：遍历 `shopPoolRef.deck` + `additionalCardPrefabs`，读 CardScript 的 cardTypeID/displayName/tag 等可得字段，POST `/api/cards/catalog`。
- CardScript 费用字段已在 3.0 移除；cost 取商店基础售价（`GetCardPrice` 的非递增基础价，不含 DeckSlot 递增部分），rarity 取卡面稀有度字段（实现时确认）。**两者必须填真值**：留 0/空则金币曲线分析（钱花在哪类卡）与「实际出现率 vs 设计权重」对账全部失效。

## 3. 配置与开关

### 3.1 ServerConfig.asset（Inspector 勾选面板）

| 字段 | 类型 / 默认 | 作用 |
|------|------------|------|
| `enabled` | bool，默认 false | 总开关：关 = 纯单机，现状行为零变化 |
| `environment` | enum {Local, Production}，默认 Local | 选 baseUrl：Local 用 `localBaseUrl`，Production 用 `productionBaseUrl`（二选一，杜绝手抄 URL） |
| `localBaseUrl` | string，默认 `http://127.0.0.1:3000` | 本地起服地址；数据落仓库内 `server/onedeck-api/data/`（S0 批已 gitignore），可随时删库重来 |
| `productionBaseUrl` | string，默认 `http://8.153.150.197` | ECS 生产 |
| `uploadDeckSnapshots` | bool，默认 true | 卡组快照上传（§2.5 前半） |
| `uploadMatchReports` | bool，默认 true | 战绩上报（§2.5 后半） |
| `uploadStatsSnapshots` | bool，默认 true | 累计统计快照（§2.7，含来源计数 meta） |
| `uploadRunRecords` | bool，默认 true | 整局记录（§2.6） |
| `uploadCardCatalog` | bool，默认 true | 卡牌目录（§2.8） |
| `fetchOpponentDecks` | bool，默认 true | 拉取幽灵对手（§2.4）；关 = 注入永远走本地回退链，来源计数 local/default 照记 |
| `markAsTest` | bool，默认 false | 注册用户名自动加 `test_` 前缀，生产库按名清理；仅在 Production 联调时勾 |

规则：

- 分项开关在**采集/入队口**生效（关 = 根本不记录、不进发件箱）；发件箱冲刷只看 `enabled` + 当前 `environment`。
- Inspector 里改 `environment` 或 `enabled` 时（OnValidate）**清空发件箱**——本地采的 payload 绝不发给云，反之亦然。
- 注册不受分项开关影响（identity 是一切上传的前提）；分项全关时各模块静默空转、不弹错。
- 预设组合：日常开发 = `Local + 全开`；生产联调 = `Production + markAsTest=true`；正式发布 = `Production + 全开 + markAsTest=false`。Play Mode 与打包共用同一 `persistentDataPath`，身份互通，故 Production 联调务必勾 `markAsTest`。

### 3.2 本地起服流程

```
cd server/onedeck-api
npm install
DATA_DIR=data node server.js        # Git Bash；PowerShell: $env:DATA_DIR="data"; node server.js
```

- DATA_DIR 必须显式传：服务端默认值是 `__dirname/../data`（ECS 布局专用，pm2 日志也在那里），本地不传会落到 `server/data/`。
- admin 看板：`http://127.0.0.1:3000/admin?token=<data/admin_token.txt 内容>`；清库 = 停服删 `data/onedeck.db*` 再重启（WAL 有 -wal/-shm 伴生文件）。
- 生产库只做两件事：S0 部署验证、发版前最终冒烟（`markAsTest=true`）。其余一切联调数据只落本地。

### 3.3 其他

- 场景联调前置：`DeckSaver` 的 `useDebugEnemyDeck` 置 0（当前为 1，会绕过一切敌方卡组来源）；`resetOnStart` 视测试需要。
- gameVersion 来源：`Application.version`（实现时核对 ProjectSettings 里的值），全链路匹配键。

## 4. 实施批次

| 批 | 内容 | 验证 |
|----|------|------|
| S0 | 服务器 §0.1 schema 增补（rounds / opponentDeckId / perCard 拆分 / 来源计数列）+ admin 展示列 + .gitignore 补 `/server/onedeck-api/data/` | 本地起服冒烟：新列可写、旧字段 payload 兼容；部署 ECS 后 `/api/health` |
| A | ServerConfig（§3.1 开关面板 + OnValidate 清箱）+ PlayerIdentity + DeckNetworkClient + UploadOutbox | EditMode：DTO 序列化往返、发件箱入队/冲刷/上限/清箱、开关矩阵（分项关 = 不入队） |
| B | 卡组上传 + OpponentDeckCache + 注入 + VS 显示 + 战绩上报 | EditMode：候选校验/弃用逻辑；Play Mode：双端互见卡组 |
| C | 两个追踪器加 session 维度 + 快照上传 | EditMode：分桶累计；admin 看板核对 |
| D | RunRecorder + 埋点 + seenPoolPct | Play Mode 打一局，admin 单局详情页核对 |
| E | 卡牌目录上传 | admin 看板体系列有名字 |
| F | 联调收尾：测试数据清理、AGENTS.md/文档更新 | — |

每批结束跑既有 EditMode 测试防回归；C 批改追踪器签名时需同步更新全部调用点与既有测试。

## 5. 风险与注意

- 纯增量设计：ServerConfig.enabled=false 时零行为变化，可随时回滚。
- 所有网络代码不得阻塞主流程：战斗入口只用缓存，绝不现等网络。
- admin token、玩家 playerId 不进仓库；`server/onedeck-api/data/` **尚未**加入 .gitignore（2026-09-04 勘误：原稿声称已在，实际没有）——批 S0 先补 `/server/onedeck-api/data/` 再在本地起服测试。
- 服务器已知低危（不阻塞客户端，批 F 一并处理）：matches/report 的防御战绩自增未与 insertReport 包同一事务（中间崩溃可漂移）；`trust proxy=true` 依赖 HOST 绑 127.0.0.1，3000 端口不得直接暴露公网（补进 README Ops notes）。
- HTTP 明文的篡改风险由"未知卡整副弃用 + 回退链 + JSON 校验"吸收（已知并接受）。

## 6. 执行状态

| 批 | 状态 | 备注 |
|----|------|------|
| S0 | ✅ 完成（2026-09-04，commit 76932fe） | 本地冒烟全过：迁移路径（预置旧 schema 库加列 + legacy 行保留）、新/旧 perCard 字段、enemySource 非清零语义、run 去重、防御战绩、admin 新展示列。**踩坑**：本地跑必须 `DATA_DIR=data`（默认值是 ECS 布局专用）；stats_meta 新列必须可空（INSERT 分支的 NULL 会撞 NOT NULL） |
| A | ✅ 完成并验证（2026-09-04，commit 67aea02 + 编译修复） | `Assets/Scripts/Net/`：ServerConfig（§3.1 全字段 + OnValidate 清箱）、NetDtos、DeckNetworkClient（10s 超时、1s/3s 重试、4xx 不重试）、UploadOutbox（100 上限丢最旧、tmp+Replace 原子写）、PlayerIdentity（markAsTest 前缀、409 → username_taken）。`Resources/ServerConfig.asset` 已创建。EditMode：专项 12/12 过；全量回归 447 中 446 过 / 1 既有跳过。**踩坑**：onOk 是 `Action<string>`，lambda 不能写成无参 |
| B | ✅ 代码完成 + EditMode 验证（2026-09-04；Play Mode 双端互见待人工跑） | `OpponentDeckCache`（磁盘缓存 + 本局去重 + 来源计数遥测 + `Current` 对手暂存）+ `DeckSaver` 服务器注入分支（debug → server → JSON → 默认池，未知卡整副弃用）+ `SavePlayerDeckToJson` 快照入发件箱 + `PhaseManager` 四挂钩（OnEnable/ResetRun 预取、进店补仓、战斗结束上报 + 冲刷）+ `CombatInfoDisplayer` VS 行。`DeckSaver.cs` 已按仓库标准转为 Tab 缩进。EditMode：`OpponentDeckCacheTests` 8/8；全量 455 中 454 过 / 1 既有跳过。**踩坑**：嵌套类与同名属性共存 = CS0102，属性改名 `Current` |
| C | ✅ 完成并验证（2026-09-04） | `SessionCardStats`/`ShopSessionStats` 平铺桶（保留扁平总量供显示/CSV）；记录方法内部经 `StatsSnapshotUploader.CurrentSessionNum()`（DeckSaver 共享 IntSO）取 session，ShopManager 调用点零改动；`StatsSnapshotUploader` 纯函数映射 + 脏标记 + 离店直发（不进发件箱，失败仅回脏标记）；`PhaseManager` 离店触发 + 两处 RecordCombatResult 传 sessionNum。`CardWinRateTracker.cs`/`CardWinRateData.cs` 已转 Tab。EditMode：`StatsSnapshotUploaderTests` 4/4 |
| D | ✅ 完成并验证（2026-09-04） | `RunRecorder`：run_start（ResetRun/场景启动，先恢复未完结 journal）/ shop_visit（ShopManager 四埋点：OnShopEnter/OnPayday 金币两时点、OnCardOffered 区分功能板、OnCardBought、OnReroll；PhaseManager 离店时 CloseShopVisit 收 goldExit）/ combat_end（rounds 用 Result 屏口径，perCard 取 CombatPerCardStatsTracker 的 Player 侧行）/ run_end（defeat/victory 判定即 CloseRun，finalDeck 取 playerDeckRef）。`current_run.jsonl` 逐快照追加，恢复时坏尾行跳过、未完结补 abandoned 上传。EditMode：`RunRecorderTests` 4/4 |
| E | ✅ 完成并验证（2026-09-04） | `CardCatalogUploader`：场景启动检查版本漂移 → 遍历 shopPoolRef + additionalCardPrefabs（cardTypeID 去重），name=GetDisplayName、tags=myTags∪reservedTag、rarity=枚举名、cost=GetCardPrice（开局即基价）→ 入发件箱，成功入队后记录版本文件。PhaseManager.OnEnable 触发 |
| G（零战斗剔除） | ✅ 完成并验证（2026-09-05） | **A. run 记录门禁**：`RunRecorder.RecoverUnfinishedRun`/`UploadCurrent` 在 `combats` 为空时不上传（journal 照常落盘/清理，恢复语义不变）；`/api/runs` 零战斗跳过入库、返回 ok 让 outbox 丢弃。**B. 商店统计门禁**：`ShopStatsManager` 改 per-visit 暂存（Record* 只写暂存），`PhaseManager.ExitingShopPhase` 先 `CommitStagedVisit()` 再 `UploadIfDirty()`；`totalShopVisits`/`totalRerolls`/session 桶全部随提交生效。新增 `ShopStatsManager.OverrideDirectoryForTests` + 惰性初始化（**踩坑**：EditMode `AddComponent` 不触发 Awake，Awake 只留 `Me` 赋值 + 预初始化）。存量库清理 SQL（可选）：先删 `run_shop_visits` 再删 `runs` 中 `NOT EXISTS run_combats` 的行。EditMode：`RunRecorderTests` 6/6 + `ShopStatsManagerTests` 4/4；全量回归 473 中 472 过 / 1 既有跳过 |
| F | ⬜ 未开始（需人工联调） | 全部代码批已完成。剩余：Play Mode 双端互见验证、本地起服全流程联调（方案 §3.2）、测试数据清理、部署 S0 到 ECS、AGENTS.md 收尾更新 |
