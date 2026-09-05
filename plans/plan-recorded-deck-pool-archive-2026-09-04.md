# Recorded 敌方卡组池 v1 归档计划（卡组设计 v2 隔离）

日期：2026-09-04
状态：**待执行**。纯资产/场景操作，全程不改代码；涉及场景改动与资源移动，执行前需用户确认。
背景：卡牌设计迭代出 v2 版本，需把 default enemy deck pool 中现存的 v1 recorded 卡组归档，并保证之后新录制的卡组与 v1 不混合。

## 1. 现状基线（2026-09-04 实测）

- 录制来源：`EnemyDeckRecorder`（Play Mode F12 / Record Now）写入 `Assets/SORefs/Decks/Recorded/Session{N}/`；`outputFolder = "SORefs/Decks/Recorded"`，session 子目录按 `DeckSaver.sessionNumber` 自动创建（不存在时 `Directory.CreateDirectory` 重建，`CreateDeckAsset`）。
- 磁盘现状：`Recorded/Session0~7` 共 **70** 个 DeckSO（22/13/11/8/6/6/3/1）。
- 池现状：GameScene.unity 中 DeckSaver 的 `defaultEnemyDeckPool` 共 **10** 个 entry、**56** 个引用——54 个指向 `Recorded/`（pool[0]~[7]），2 个指向手工 `Default Enemy Decks/`（pool[8]、pool[9]）。
- 差异：70 个 recorded 中仅 54 个进了池（16 个未同步，历史遗留；池清空后该差异自然消失）。
- 同步工具：skill `check-default-enemy-deck-pool` 只扫描 `Assets/SORefs/Decks/Recorded/Session*`、只增不删、`--fix` 自动补齐。
- 引用机制：池与卡组之间全部走 GUID（存于 .meta）；项目内带 .meta 移动资源不断引用。
- 敌方卡组填充优先级：`debug > 服务器 ghost（OpponentDeckCache）> default pool`（2026-09-04 起 local json 层已移除，见 plans/plan-remove-local-json-enemy-fallback-2026-09-04.md）。
- 引用面：grep 实测引用 `defaultEnemyDeckPool` / `Decks/Recorded` 的代码只有 `DeckSaver.cs`、`EnemyDeckRecorder.cs`，无 .asset 以文本路径引用该目录。

## 2. 方案总览

整体文件夹搬移 + 池清空，零代码改动：

1. `Recorded/Session0~7` 逐个挪到 `Assets/SORefs/Decks/Deprecated Decks/Recorded_v1/`（复用既有 Deprecated Decks 约定），`Recorded/` 空壳与 `Recorded.meta` 原地保留。
2. `EnemyDeckRecorder.outputFolder` 保持不动 → 新录制自动写入原 `Recorded/Session{N}/`，与 v1 物理隔离。
3. skill 只认 `Recorded/Session*` → 归档后的 v1 永远不会被自动同步回池。
4. GameScene 池清空 → 池内仅剩（可选保留的）session 8/9 手工卡组，之后由 skill 按 v2 录制结果重新填充。

## 3. 执行步骤

### Step 1 归档资产移动（保 GUID）

关闭 Unity（或确保其处于空闲、无资源导入状态）后执行：

```bash
cd "D:/Unity Projects/OneDeck"
mkdir -p "Assets/SORefs/Decks/Deprecated Decks/Recorded_v1"
git mv "Assets/SORefs/Decks/Recorded/Session0" "Assets/SORefs/Decks/Deprecated Decks/Recorded_v1/Session0"
# Session1 ~ Session7 同理逐个 git mv
```

- 也可在 Editor 的 Project 窗口把 8 个 Session 文件夹整体拖入 `Deprecated Decks/` 下新建的 `Recorded_v1`，同样保 GUID。
- 严禁删除重建、或不带 .meta 拷贝——会换 GUID，池引用与历史统计断链。
- `Recorded/` 只剩 `Recorded.meta` 属预期（git 只追踪 .meta，空文件夹留在磁盘上，recorder 继续往里写）。

### Step 2 清空池（场景改动）

- GameScene → DeckSaver → `defaultEnemyDeckPool`：10 个 entry 的 `decks` 列表全部清空，保留 entry 结构。
- 空池行为安全：`PopulateFromDefaultDecks` 对空池/空列表仅 warning + return（DeckSaver.cs:583-604），不抛错。
- 【决策点】pool[8]/[9] 的两个手工卡组是否保留：保留 = session 8/9 不至于空池；但 v2 卡改后其内容已属旧设计，若要求绝对纯净则一并清空。默认建议：保留，另行择期重做。

### Step 3 提交

- GameScene.unity 当前已有未提交改动；本次「文件夹移动 + 池清空」独立成一个 commit（便于单独 revert 恢复 v1 池），不与其余在途改动混合。
- git 历史天然保存 v1 池的 56 项完整映射（GameScene.unity 的 diff 可查）。
- 归档 DeckSO 保持 git 追踪（skill 明确要求 recorded 资产入库；git mv 保留 rename 历史）。

### Step 4 验证

- 重开 Unity，确认 Console 无 Missing / GUID 断链报错。
- 跑同步 skill 确认基线：

```bash
python3 .agents/skills/check-default-enemy-deck-pool/scripts/check-default-enemy-deck-pool.py .
```

预期：Recorded 侧 0 个卡组；池侧 0（或仅 session 8/9 的 2 个手工卡组）。

### Step 5 v2 录制与回填（后续日常流程）

- Play Mode 录制 → 落入 `Recorded/Session{N}/`（全新内容，与 v1 无交集）。
- 积累后跑 skill 加 `--fix` 同步进池；池里永远只有 v2。
- session 编号沿用 `sessionNumber`，流程与工具链零调整。

## 4. 隔离彻底性说明（两条旁路）

池只是敌方卡组的第 4 优先级。若目标是「v2 环境纯净测试」，还需：

- 本地 JSON：一次性 `WipeDeckSaves`（Ctrl+W，或临时 `resetOnStart=true` 启动一次），清掉旧设计的 deckdata.json。
- 服务器 ghost：临时关 `OpponentDeckCache.FetchEnabled`——旧 cardTypeID 若未删除仍可解析，ghost 会混入旧设计卡组。
- 若只关心池不混（本计划的目标），以上两项可不做。

## 5. 回滚方案

- 场景：revert Step 3 的池清空 commit，或手动恢复 GameScene.unity 的 `defaultEnemyDeckPool` 段落。
- 资产：`git mv` 反向挪回（或在 Editor 里把 `Deprecated Decks/Recorded_v1` 拖回原位改名）。
- GUID 全程未变，回滚后池引用直接可用。

## 6. 风险与注意

- git mv 时 Unity 若在运行/导入状态，可能对移动中的资源重复生成 .meta——务必先关闭 Unity 或等其空闲。
- 归档目录命名 `Recorded_v1` 不匹配 skill 的 `Session*` glob，这是防止 v1 被自动回填的关键；不要把 v1 子文件夹留在 `Recorded/` 内。
- 不要把归档卡组移出 `Assets/`（出工程即丢引用、不入库）。
- v2 录制若出现 `Session8+` 文件夹：skill `--fix` 会给池追加对应 entry；DeckSaver 的 poolIndex clamp（超界用最后一个 entry，DeckSaver.cs:592-597）不受影响。
