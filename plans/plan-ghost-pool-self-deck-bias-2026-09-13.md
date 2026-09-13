# 对手 Ghost 缓存自牌偏置 (只匹配到 test_papaya 旧卡组)

日期: 2026-09-13
状态: **已实施 (2026-09-13)** — 方案 A+B + `fightOwnGhostsOnly` 只选自牌过滤; OpponentDeckCacheTests 13/13 绿, 全量 EditMode 514/515 (1 存量 Ignore)
现象: 新 run 匹配到的 ghost 卡组 username 全是 test_papaya (自己), 服务器上其他玩家 (玩家#1299 / renoxiao) 的卡组排不上队
关联: plans/plan-async-pvp-client-2026-09-03.md (§2.4 对手缓存 / §2.5 注入优先级), Net/OpponentDeckCache.cs, Managers/WriteRead/DeckSaver.cs, Managers/TestManager.cs

## 1. 结论 (TL;DR)

**下载链路没有问题, 是选取偏置。** 本地缓存文件里 玩家#1299 的卡组已经下载到位 (18 条), 但 `OpponentDeckCache.TakeCandidate` 按"列表第一条"取候选, 而旧的自牌 (test_papaya, 早期 includeSelf 时期合入) 全部排在列表前部, 每次 run 又从零开始消耗, 所以永远先命中自牌。

## 2. 证据 (2026-09-13 本机缓存 dump)

文件: `C:\Users\damen\AppData\LocalLow\SmallGrass\OneDeck\opponent_cache.json`, 共 37 条:

| username | 条数 | session 覆盖 | 在列表中的位置 |
|----------|------|--------------|----------------|
| test_papaya | 17 | 0-5 | 最前 (deckId 2-19) |
| renoxiao | 2 | 0-1 | 中间 (deckId 20-21) |
| 玩家#1299 | 18 | 0-6 | 最后 (deckId 22-47) |

`usedDeckIds` 为空 (新 run 刚开始)。

## 3. 根因链

1. **历史遗留**: 09-12 `TestManager.fightOwnGhostsOnly` 时期 `ServerConfig.opponentsIncludeSelf` 被强制置 1, Prefetch 把自己上传的 17 条 deck 合入了缓存, 且插入在列表最前部。
2. **选取按插入序**: `TakeCandidate` (Net/OpponentDeckCache.cs:157) 用 `cache.decks.Find(d => d.sessionNum == sessionNum && 未用)` — `List.Find` 返回第一条匹配, 插入序即优先级。
3. **per-run 去重每局重置**: `OnRunStarted` (PhaseManager.cs:95 / 357) 每次 run 开始清空 `usedDeckIds`, 所以每个新 run 的 session 0-5 又从头命中同一批自牌。
4. **缓存只增不减**: 现配置 `opponentsIncludeSelf: 0` 已停止新自牌入库, 但 Prefetch 合并只做"deckId 不存在则添加", 没有所有权过滤 / 过期机制, 旧 17 条永久留存。
5. **自牌只有两种情况轮得到**: 同一 run 内自牌按 session 耗尽 (一 run 每 session 一场, 实际到不了), 或 session 超出自牌覆盖范围 (test_papaya 缓存只到 session 5, session 6+ 才命中 #1299)。
6. **附带信号**: 自己的 deck 被打时, match report 会被服务器 400 `own_deck` 拒绝 (server.js /api/matches/report), 客户端已按设计 skip — 自牌本就不该出现在对手池。

## 4. 修复方案 (已拍板, 2026-09-13 实施)

| # | 方案 | 内容 | 作用 |
|---|------|------|------|
| A | Prefetch 合并时按所有权过滤 (治本, 防复发) | `opponentsIncludeSelf` 为关时, 合并响应前先移除缓存中 `username == PlayerIdentity.Username` 的条目 | 一次性清掉 test_papaya 残留 + 未来翻回开关后再次关闭也能自愈 |
| B | TakeCandidate 随机化 (去插入序偏置) | 同 session 的未用候选中 `UnityEngine.Random.Range` 随机取一 | 与服务器端 `randomDecks` 的随机语义对齐, 缓存顺序不再决定优先级 |

**方案 B 与 `fightOwnGhostsOnly` 的交互 (2026-09-13 补)**: 该开关的实现只有两点 (`TestManager.cs:170`/`:197`) — `onlyGhostEnemyDeck` 封默认池 + 强制 `opponentsIncludeSelf`; `TakeCandidate` 从未按 username 过滤, "只打自己"目前靠自牌恰好排列表最前的插入序巧合。随机化会拆掉这个巧合: 开关 ON 时同 session 未用候选里混有他人卡组 (当前缓存 renoxiao session 0-1 / 玩家#1299 session 0-6), 随机可能命中, 且打他人卡组会正常上报 match report (只有自牌被 400 own_deck skip), 测试对局会污染真实战绩。因此 B 实施时须同步做: `fightOwnGhostsOnly` ON 时 `TakeCandidate` 只在 `username == PlayerIdentity.Username` 的候选中选取, 无则返回 null — 正好接上 `onlyGhostEnemyDeck` 的 cache-dry 语义 (敌方卡组清空), 开关的"只打自己"从顺序巧合变为显式过滤。

既存漏洞 (拍板不管, 2026-09-13): 自牌缓存只覆盖到 session 5, 开关 ON 时 session 6+ 的选取 (first-match 或随机) 都会落到他人卡组。裁定: 不修, 等自己打出 session 6+ 的卡组数据、自牌覆盖自然补齐即可。

不推荐做"以服务器响应全量替换缓存" — 会破坏离线回退语义 (网络失败时旧缓存是唯一的对手来源); 过滤 + 随机两步已足够。

不改代码的一次性清理: 删除本机 `opponent_cache.json`, 下次 run 重新拉取 (服务器现已排除自牌, 拉回的全是他人卡组)。

## 5. 测试 (实施时补)

`OpponentDeckCacheTests`:
- includeSelf 关闭时, Prefetch 合并清掉缓存中的自牌旧条目 (InjectForTests 预埋 username = 自己的条目, 模拟响应合并后断言被清除);
- TakeCandidate 不恒取第一条 (固定 `Random.InitState` 种子, 注入多条同 session 候选, 断言多次选取能命中非首条);
- `fightOwnGhostsOnly` ON: 同 session 预埋一条他人条目 + 一条自牌条目, 断言 TakeCandidate 恒选自牌; 移除自牌条目后断言返回 null (cache-dry 语义)。
