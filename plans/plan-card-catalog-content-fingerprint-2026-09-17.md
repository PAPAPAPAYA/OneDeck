# 计划：卡目录上传去重改内容指纹（治本「改卡不重传」）

日期：2026-09-17
上游：`plans/plan-combat-completion-upload-gate-2026-09-06.md`（§2.8 卡目录上传；本计划只换去重键，触发链路沿用）
来源：09-10 卡表名字漂移事故的治本方向（memory: card-catalog-upload-staleness-20260910），2026-09-17 拍板。

## 1. 现状与事故复盘

- 去重机制：`CardCatalogUploader.MaybeUpload()` 的跳过条件是 `ReadLocalVersion() == currentVersion`（CardCatalogUploader.cs:40），哨兵 `persistentDataPath/catalog_version.txt` 存上次成功入队时的 `DeckNetworkClient.GameVersion`（打包版本号）。
- 缺陷：版本号只在打包时变化。编辑器内改 displayName / tags / rarity / 价格、增删池内 prefab，版本号不动 → 哨兵照旧匹配 → 永不重传，服务器 catalog 冻结在上次打包时点。
- 事故（2026-09-10）：线上 card_catalog 只有 09-06 一次上传，与 prefab 比对 108/109 名字不一致；临时解法 = 挪走本机哨兵文件强制一次重传。
- 触发点（2026-09-17 实测）：生产唯一调用点 = `CombatCompletionGate.MarkCompleted()`（CombatCompletionGate.cs:37，战斗结算处）；旧计划提过的注册补传 / 场景启动直调已收敛进这一处，测试另有直调。

## 2. 方案

### 2.1 去重键替换（唯一实质改动，全部在 CardCatalogUploader.cs）

MaybeUpload 内部顺序调整：

```
CollectCards(saver)                                  // 照旧
fingerprint = ComputeFingerprint(cards)              // 新增
if (ReadLocalFingerprint() == fingerprint) return;   // 版本号比对 → 指纹比对
UploadOutbox.Enqueue(..., gameVersion = DeckNetworkClient.GameVersion, cards);  // 照旧
WriteLocalFingerprint(fingerprint);
```

- 载荷 `gameVersion` 字段照旧传打包版本号：服务器 `(game_version, card_type_id)` 幂等 upsert、admin `loadCatalogMap` 的「行数最多版本」取数逻辑全部不动（server.js 零改动）。
- 哨兵文件名不变（catalog_version.txt），内容语义从版本号换成指纹。旧文件内容（版本号字符串）永远不等于新指纹 → 升级后第一场战斗自动多传一次，幂等 upsert 吃掉，不需要迁移代码。

### 2.2 ComputeFingerprint 设计

`public static string ComputeFingerprint(List<CatalogCardEntry> cards)`

- 纯静态、无场景依赖（`System.Security.Cryptography.SHA1`，编辑器 / 真机都可用），EditMode 可直接断言。
- 序列化规则（确定性是硬要求）：
	1. 条目按 `cardTypeID` CompareOrdinal 排序（复制后排序，不改入参——同一 List 还要进 Enqueue 载荷，保持 prefab 原序）。
	2. 每条字段定序：cardTypeID → name → tags → rarity → cost；tags 复制后排序、逗号连接（对 myTags 序列化顺序不敏感）。
	3. 防结构性碰撞：逐字段 `len:value|` 长度前缀拼接，字段值里出现任何分隔符也不会串位。
	4. UTF-8 字节流 → SHA-1 → 16 进制小写。
- cost 进指纹 ⇒ `GetCardPrice` 结果变化（调价公式、ShopRarityWeight 改动）同样自动触发重传。

### 2.3 会触发重传的内容面（全覆盖清单）

改名 displayName / myTags、reservedTag / rarity / cost / cardTypeID（= 同时加卡+删卡）/ 池子增删（shopPoolRef.deck 与 additionalCardPrefabs）。

### 2.4 明确不治的范围

- **删了没清**：catalog 只 upsert 不删除，卡移出池子后旧行永留服务器（3 张 3.0 僵尸行即此来源，来自 additionalCardPrefabs 挂的 3.0 卡）。指纹方案不覆盖；清理需另行拍板（服务器全量替换模式或定期清理任务）。
- **admin 侧字段缺口**：catalog 本身没有 desc / ATK 列（上传器就没传），本方案不扩列；三方一致性检查工具（另案）会标注「该字段 admin 侧不可查」。

## 3. 测试计划（EditMode，CombatCompletionGateTests.cs）

- 改造 `GateAlreadyOpen_CatalogUploadsAgainOnlyOnVersionDrift`（:114）：改名 `...OnlyOnContentDrift`，注释从 "Version idempotence" 改为指纹口径；断言行为不变（脚手架内容不变 → 第二次 MarkCompleted 仍不新增 PendingCount）。
- 新增：
	1. **指纹纯函数敏感性**（不依赖 DeckSaver / 资产）：同集合不同输入顺序 → 同指纹；依次变动 name / tags 内容 / rarity / cost / 集合增删 → 指纹全变；tags 仅顺序变 → 指纹不变。
	2. **迁移兼容**：哨兵文件预置旧格式（版本号字符串）→ 首次 MaybeUpload 重传一次，之后再不传。
	3. **内容漂移全链**：入队一次后污染哨兵（写入乱值）→ 再 MarkCompleted → PendingCount+1（等价于内容变化的重传路径，避免改 prefab 资产造成污染）。
- 说明：不在 EditMode 里改 prefab 资产字段模拟「真改名」（资产污染风险）；内容敏感性由纯函数用例 + MaybeUpload「指纹取自实际载荷」的顺序保证。
- 跑全量前先 SaveScene 清 dirty（runtests 教训）。

## 4. 执行清单

- [x] CardCatalogUploader.cs：类头注释更新（"once per game version" → 内容指纹口径）；ReadLocalVersion / WriteLocalVersion → ReadLocalFingerprint / WriteLocalFingerprint；新增 ComputeFingerprint + AppendField；MaybeUpload 重排
- [x] CombatCompletionGateTests.cs：1 改 3 增
- [x] NetDtos.cs / server.js / Notion：零改动
- [x] EditMode 全量 + 汇报（向用户说明：落地后第一场战斗会多一次全量重传，属迁移预期）——2026-09-17 全量 549/548 绿（1 存量 Ignore），CombatCompletionGateTests 10/10 单跑确认
- [ ] 验收（Play 观察项）：故意改一张卡名 → 下一场战斗结算 → 服务器 catalog 该卡名字已更新
