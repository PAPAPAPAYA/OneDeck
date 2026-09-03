# 异步 PvP 客户端实施计划（Unity 侧）

日期：2026-09-03
上游：服务器端已上线（`server/onedeck-api/`，ECS `8.153.150.197`，Express + better-sqlite3，pm2 托管 + Nginx 反代，端到端验证通过）；本文件只做 Unity 客户端
状态：**待实施**

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

## 1. 客户端总体结构

新增目录 `Assets/Scripts/Net/`（纯新增，不动既有架构）：

```
ServerConfig        ScriptableObject（Resources/ServerConfig.asset）：baseUrl、enabled、gameVersion 覆盖
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
- 战斗结束（结果已知时）上报 `POST /api/matches/report`：`reportId=Guid.NewGuid()`、`opponentDeckId`、`won`、`sessionNum`；入发件箱。
- **时序注意**：`CombatPerCardStatsTracker.BeginSession()` 在 `GatherDecks()` 清数据，战斗统计必须在结果阶段、下一场 `GatherDecks` 之前收割。

### 2.6 RunRecorder（对局记录）

- `run_start`：`PhaseManager.ResetRun` 时生成 `runId=Guid`，清空 `seenCardTypeIDs`（HashSet）。
- `shop_visit`（离开商店时落盘一条）：
  - `offered`/`utilityOffered`：本轮刷出卡（商店生成点埋点）；`bought[]`：本阶段购买（可多张可重复可为空）
  - `rerollCount`：本阶段刷新次数；`goldEnter`（payday 前）、`goldAfterPayday`（消费前购买力）、`goldExit`（离场，含刷新花费）
  - `seenPoolPct`：本局已见过卡种数 / 商店卡池 distinct cardTypeID 数（分母含功能板卡；口径如需调整再议）
- `combat_end`：sessionNum、输赢、剩心、每卡触发次数/伤害（取自 CombatPerCardStatsTracker）。
- `run_end`：result(victory/defeat/abandoned)、finalSession、heartsLeft、finalDeck、最终 seenPoolPct → 整包 `POST /api/runs`（发件箱兜底）。
- 增量落盘 `current_run.jsonl` 防崩溃；下次启动发现未完结记录 → 补 `abandoned` 后上传。

### 2.7 统计快照上传

- 两个追踪器加 session 分桶后，上传载荷 = 两个追踪器全量分桶 + meta(totalShopVisits/totalRerolls) + gameVersion。
- 时机：离开商店时若脏则传；服务器 upsert 保证重传安全。

### 2.8 卡牌目录上传

- 启动时（注册成功后）若本地记录的 catalogVersion != 当前版本：遍历 `shopPoolRef.deck` + `additionalCardPrefabs`，读 CardScript 的 cardTypeID/displayName/tag 等可得字段，POST `/api/cards/catalog`。
- CardScript 费用字段已在 3.0 移除，cost 字段实现时从定价来源取或留 0；稀有度取卡面配置（实现时确认字段名）。

## 3. 配置与开关

- `ServerConfig.enabled` 总开关：关掉 = 纯单机（现状行为不变）。
- 场景联调前置：`DeckSaver` 的 `useDebugEnemyDeck` 置 0（当前为 1，会绕过一切敌方卡组来源）；`resetOnStart` 视测试需要。
- gameVersion 来源：`Application.version`（实现时核对 ProjectSettings 里的值），全链路匹配键。

## 4. 实施批次

| 批 | 内容 | 验证 |
|----|------|------|
| A | ServerConfig + PlayerIdentity + DeckNetworkClient + UploadOutbox | EditMode：DTO 序列化往返、发件箱入队/冲刷/上限 |
| B | 卡组上传 + OpponentDeckCache + 注入 + VS 显示 + 战绩上报 | EditMode：候选校验/弃用逻辑；Play Mode：双端互见卡组 |
| C | 两个追踪器加 session 维度 + 快照上传 | EditMode：分桶累计；admin 看板核对 |
| D | RunRecorder + 埋点 + seenPoolPct | Play Mode 打一局，admin 单局详情页核对 |
| E | 卡牌目录上传 | admin 看板体系列有名字 |
| F | 联调收尾：测试数据清理、AGENTS.md/文档更新 | — |

每批结束跑既有 EditMode 测试防回归；C 批改追踪器签名时需同步更新全部调用点与既有测试。

## 5. 风险与注意

- 纯增量设计：ServerConfig.enabled=false 时零行为变化，可随时回滚。
- 所有网络代码不得阻塞主流程：战斗入口只用缓存，绝不现等网络。
- admin token、玩家 playerId 不进仓库；`server/onedeck-api/data/` 已在 .gitignore。
- HTTP 明文的篡改风险由"未知卡整副弃用 + 回退链 + JSON 校验"吸收（已知并接受）。
