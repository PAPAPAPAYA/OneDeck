# Plan: 商店后对手选择页（选敌页）

日期：2026-10-06
状态：**方向已拍板（2026-10-06 四决策 D1-D4）+ 实施细节已拍齐（2026-10-10 第二轮：关键卡规则 / camera 入场编排 / O1-O4 全落定，见 §4.3/§4.4/§9），实施待「修改代码」**。按 step-gate 协议推进：每步完成后停下汇报，确认后继续下一步。
关联：`plans/plan-async-pvp-client-2026-09-03.md`（§2.4 对手缓存 / §2.5 注入）、`plans/plan-ghost-pool-self-deck-bias-2026-09-13.md`（TakeCandidate 随机化 + 过滤）、`plans/plan-phase-transition-world-camera-2026-09-21.md`（转场系统）、`docs/PhaseTransition.md`

## 0. 拍板记录（2026-10-06 对话）

| # | 决策点 | 拍板 |
|---|--------|------|
| D1 | 交互形态 | 转场中途独立页：商店 → 选敌页 → 战斗，一屏一页 |
| D2 | 候选来源与数量 | 固定 N 个（2-3，配置可调）；只来自 OpponentDeckCache 的 ghost 候选，不混 default pool |
| D3 | 未选候选处置 | 未选回池：只有被选中的 deckId 标记已用，未选的保持未用 |
| D4 | 重选 | 无重选：只能从展示批次里挑一个走；**不设返回商店按钮**（回店再出=重抽，会绕过 D4） |

## 1. 背景与目标

现状：点「离开商店」直接起飞进战斗，对手由注入链自动选取（debug > server ghost > default pool），玩家无感知。目标：在商店与战斗之间插入一个世界空间选敌页，玩家从 N 个 ghost 候选中显式挑选本场对手，然后再进入战斗。转场系统从单段飞行变为两段。

## 2. 现状基线（2026-10-06 实测）

- 触发链：离开商店按钮 → `PhaseTransitionDriver.RequestShopToCombat(pm)`（ShopHudBinder.cs:123）；PhaseManager 内部路径 PhaseManager.cs:144。
- 页面几何：`_combatPageY = _shopPageY + _pageH`（PhaseTransitionDriver.cs:120-121）——战斗页 = 商店页正上方一屏；rig `DOMoveY` 飞行（:246）。
- 选敌链：`DeckSaver.PopulateEnemyDeckBySessionNumber`（DeckSaver.cs:366）= debug 卡组 → `OpponentDeckCache.TakeCandidate(session)`（DeckSaver.cs:426；随机取未用 + `fightOwnGhostsOnly` 时 username 过滤）→ `defaultEnemyDeckPool`（DeckSaver.cs:480）。
- 去重：`usedDeckIds` 每 run 开始清空（PhaseManager.OnRunStarted）。
- 预取时机：进店补仓保证下一场候选已落缓存（async-pvp plan §2.4）→ 选敌页读缓存零网络等待（符合「战斗入口只用缓存，绝不现等网络」）。
- 转场 bypass 现状：config off / batch / overrideCombatSeed / NullVisuals → 硬切。
- 上报面：VS 行（CombatInfoDisplayer）、match report `opponentDeckId`、`run_combats.opponent_deck_id`、来源计数（server/pool）。

## 3. 总体方案

```
离开商店点击
 ├─ bypass 命中（§4.5）→ 现状单段转场/硬切，行为完全不变
 └─ 选敌页启用
     → leg1：rig 飞到选敌页（+1 屏），玩家从 N 候选中点选
     → pick：ConsumeCandidate + 立即注入敌方卡组（entrance 飞行需要内容）
     → leg2：rig 飞到战斗页（+2 屏），落地即现有 combat entrance 编排
     → EnterCombatPhase（populate 幂等 no-op）
```

- 选敌页占据现战斗页的 +1 屏位置；选敌页启用时战斗页上移到 +2 屏（`_combatPageY = _shopPageY + 2*_pageH`），未启用时几何与今天完全一致。
- 选敌页停留期间 PhaseManager 仍处 Shop phase；离店钩子（`CommitStagedVisit`/`UploadIfDirty`/shop_visit 收口 goldExit）仍在离店点击时触发，时机不变。
- 「未选回池」在现行线性流程下与「展示即消耗」实际表现一致（同 session 选敌每 run 恰好发生一次，未选者无处重现），实现上就是「不标记」，无需额外状态层——D3 落地成本为零。

## 4. 模块详设

### 4.1 OpponentDeckCache 新 API

- `PeekCandidates(sessionNum, n)`：该 session 未用候选中随机取 n 个返回（不足给实际数），**不标记**。过滤沿用 `TakeCandidate` 现有逻辑（含 `fightOwnGhostsOnly` username 过滤）。随机方式与 `TakeCandidate` 一致（UnityEngine.Random；匹配类随机在确定性 RNG 规则的 combat/shop/setup 范围之外，沿用现状并在此注明）。
- `ConsumeCandidate(sessionNum, deckId)`：`usedDeckIds` 标记 + 设 `Current` + 写入 reservation（暂存 picked entry 供注入）。同 deckId 重复 consume 幂等忽略。
- `ResetRun` 清 reservation（与现有 usedDeckIds 清空同点）。

### 4.2 DeckSaver 预留分支

- `PopulateEnemyDeckBySessionNumber` 头部检查 reservation：命中本 session → 直接按 picked entry 填充（deck / hpMax / username 走现有 ghost 分支同款路径），清 reservation，return；未命中走现状链。
- 幂等护栏：reservation 已消费且 `Current` 属本 session 的重复 populate（leg2 后进入 combat 时那一次）→ 从 `Current` 重新填充同内容，**不得**再落 `TakeCandidate`（防二次选取换人）。
- debug / default pool 分支不动。

### 4.3 选敌页内容（2026-10-10 修订：v2 prefab 基准）

- **面板 prefab = `Assets/Prefabs/ShopHud/OpponentSelectPanel v2.prefab`**（用户手摆固化，view refs 已接；内部布局与页面摆位实测值见 `plans/plan-opponent-select-page-layout-2026-10-10.md`，N=2 并排）。页面构建器新文件（如 `Assets/Scripts/UXPrototype/OpponentSelectPage.cs`，不进 god 文件）：按布局计划 §2 实测值实例化 N 个 v2、按 §4.3 关键卡规则 + 候选数据 Fill（契约见 OpponentSelectPanel.cs 头注）。无 canvas uGUI 输入，交互全走 PhysButton（world-only 裁定）。生命周期：离店点击 Peek 通过后建页填数据，leg2 落地后 teardown。
- **关键卡选卡规则（2026-10-10 拍板）**：ghost 卡组按 cardTypeID 去重、排除 utility 被动后，按稀有度从高到低**逐层随机取、补满 3 为止**（层内随机；上层取完进下层）；层内随机用 `UnityEngine.Random`（TakeCandidate 匹配类口径，同 §4.1 注明）；去重后总数不足 3 张 → 空槽隐藏（SetActive false），不补假卡。展示顺序：稀有度降序、同层按卡组顺序左→右。Fill 时面板占位卡（PhysicalCardParent 显示体）隐藏，按真实卡 prefab 重建卡脸并推 name/desc/atk。
- 交互：点【选择】= ConsumeCandidate + 注入 + leg2（O2 单击即选）。关键卡 hover 弹起 + 点击放大（PopUpCard 同款，走 ShopInputGate 门）排入 **S3 打磨步，可独立砍**。
- 【选择 +$N】纯展示占位（P3）：运行时数值来自 `PhaseTransitionConfigSO.bountyPlaceholder`（默认 0，v2 prefab authored 文本为「选择 +$4」），赏金语义拍板后替换真实来源。
- 无返回商店按钮（D4）。缓存 0 候选不进本页（时序见 §4.5 尾注）；1 候选 → 单面板 x=0 居中（布局计划 O-L2）。
- 位置/尺寸基准 = 布局计划实测值（不再零散挂 config；面板级调参直接在 v2 prefab 上做）。
- UI demo（2026-10-06；2026-10-08 v4 迭代）：`docs/demo/OpponentSelectDemo.html` — 视觉规格参考（ShopPanelBg @0.5 平铺面板、硬阴影、名牌阴影带 0.5）留存；**2026-10-10 裁定：入场实施 camera 模式（§4.4），demo 推荐的 morph 编排（镜头不动/背景 clip/名牌原位变身）不实施**；demo 的「面板名牌 34px 与战斗同字号纯平移」口径同步作废（v2 面板名牌 em 1.0，衔接见 §4.4/O-L1）。
- v1 面板 prefab（`OpponentSelectPanel.prefab`，2026-10-09 实施）弃用留档；清理时机 = 布局计划 SL2（删场景实例前经用户确认）。

### 4.4 转场接线（PhaseTransitionDriver；2026-10-10 camera 裁定细化）

- 新增 `RequestShopToSelection(pm)` 与选敌→战斗 routine；`_selectionPageY = _shopPageY + _pageH`；选敌页启用时 `_combatPageY = _shopPageY + 2*_pageH`，未启用保持现状。
- **leg1（商店→选敌，2026-10-10 裁定）**：rig 飞行（overshoot 页外 PAD 余量照 demo 规矩）＋三件 HUD 事：①**玩家名牌 handoff 提前到 leg1**——商店名牌→战斗名牌交接原是一次性（shop→combat 单段旅行内完成），拆两段后交接点前移，战斗名牌自选敌页起上岗（商店期 HP 恒满，显示无歧义）；②**选项按钮平移**：屏幕右上（商店钉死位）→ 右下战斗位，随 leg1 平移、之后不动（它不是纯视口钉死物——钉的只是商店位）；③选敌面板**固定在 +1 页随镜头揭示**，无独立入场动画。选敌页背景 = **玩家灰全页延伸**（O4 落定：无红；背景覆盖需 +1 页）。
- **leg2（选敌→战斗，落地编排）**：相机再上一页至 +2；**敌人名牌**面板位→战斗位（+8.64, +5.27）平移——对象恒常同玩家名牌处理，尺寸衔接 O-L1（面板 em 1.0 vs 战斗位渲染尺寸）落地前实测，纯平移或补 scale 到时定；**关键卡**面板槽位「起飞即生成」physical 实例（复用 PartD 机制，面板显示体同刻隐藏）飞入牌堆——同 cardTypeID 多副本时面板张对应其中一实例，其余副本走上方组；**剩余敌卡 + start card** 屏幕外上方移入；**玩家卡**屏幕外下方移入。三组来源参数进 `PhaseTransitionConfigSO`。**本条取代原「entrance 编排不动」**——`ScheduleCombatEntranceFlights` 的来源分组要改。
- ShopHudBinder 离店绑定改为按 config 分流：选敌页启用 → `RequestShopToSelection`，否则现状 `RequestShopToCombat`。PhaseManager.cs:144 legacy 路径同步分流。
- 选敌页停留期间输入门：`BlockInput`/`UnblockInput` 配对（页面自持 requester），leg1/leg2 期间沿用转场既有屏蔽。

### 4.5 Bypass 矩阵（跳过选敌页 = 现状行为）

| 条件 | 行为 |
|---|---|
| `useDebugEnemyDeck` | 跳过（debug 卡组已指定对手） |
| batch / headless / NullVisuals / overrideCombatSeed | 跳过（与转场 bypass 同清单） |
| 选敌页 config off | 跳过 |
| `fightOwnGhostsOnly` / `onlyGhostEnemyDeck` | 页面照常，候选按现有过滤；0 候选 → 跳页走现状 cache-dry 链 |
| 缓存 0 候选（离线等） | 跳页，现状链落 default pool |

时序注（2026-10-10）：**离店点击先 `PeekCandidates` 再分流**——0 候选当场走现状 `RequestShopToCombat` 链，≥1 候选才建页 + `RequestShopToSelection`；Peek 不标记，之后正式 populate 才消耗。

### 4.6 PhaseTransitionConfigSO 新增

- `selectionPageEnabled`（默认 false，Play 验证通过后置 true）、`selectionCandidateCount`（默认 **2**，O1 落定）、`bountyPlaceholder`（+$N 展示值，默认 0）、leg1/leg2 飞行时长/滑距、**entrance 三组来源参数**（关键卡面板位 / 敌卡+start card 屏外上 / 玩家卡屏外下：弧高/时长/交错）。

## 5. 上报与遥测口径

- 全部不变：picked deckId 进 match report / `run_combats.opponent_deck_id`；来源计数照旧；未选候选无任何计数事件。
- 选敌页不产生新遥测（「展示了哪些候选」的样本二期再议）。

## 6. 实施批次（step-gate：每步完成停下汇报，确认后继续）

| 步 | 内容 | 验证 |
|---|------|------|
| S1 | §4.1 + §4.2：cache Peek/Consume/reservation + DeckSaver 预留分支 + 幂等护栏 | EditMode 专项（§7.1）+ 全量回归零漂移 |
| S2 | §4.3 + §4.6：OpponentSelectPage 构建器（v2 实例 ×N + 关键卡规则 Fill + bountyPlaceholder）+ config 字段 + 输入门 | EditMode bypass 矩阵 + 选卡规则专项；编辑器内目检 |
| S3 | §4.4：两段转场接线（含名牌 handoff 前移 / 选项按钮平移 / entrance 三组来源改编排）+ `_combatPageY` 上移 + 消费方清点 + 关键卡 hover/放大（打磨步，可砍） | Play Mode 人工全流程（§7.2）+ RegressionChecklist 追加行 |

## 7. 测试

### 7.1 EditMode（并入 OpponentDeckCacheTests 或新建 OpponentSelectTests）

- Peek：不标记已用；数量 ≤ N；`fightOwnGhostsOnly` 过滤生效；固定 `Random.InitState` 下多次调用可命中非首条（延续 09-13 测试手法）。
- 关键卡选卡规则（§4.3，纯函数化后测）：cardTypeID 去重；utility 被动排除；逐层补满 3（固定 `Random.InitState` 下断言层序与随机命中）；去重后 <3 → 返回实际数（空槽由 Fill 隐藏）；展示顺序 = 稀有度降序、同层卡组顺序。
- Consume：标记 used + `Current` 更新；重复 consume 幂等；未选条目仍可被后续 `TakeCandidate` 命中（D3 回池断言）。
- 预留分支：populate 使用 picked 内容（deck/hpMax/username 对上）+ 清 reservation；无 reservation 走现状链；ResetRun 清预留；同 session 重复 populate 不换人。
- Bypass：§4.5 全行断言（选敌页不创建、离店走现状请求）。

### 7.2 Play Mode（人工）

- 正常流：离开商店 → 选敌页 → 点选 → leg2 → 战斗对手 = 所选（VS 行 username 对上、上报 deckId 对上）。
- bypass 各分支走现状行为；离线 0 候选走 default pool；两段飞行视觉与 entrance 编排无回归。

## 8. 风险与注意

- `_combatPageY` 上移一屏：读 `CombatPageY` static 的消费方自动跟随；但任何「商店 + 一屏」硬编码假设要清点（CombatHPBarPresenter HudWorldFlight、entrance 落点探针、PhaseTransitionDemo）。**S3 首项做消费方 sweep**（camera 裁定下本项不豁免）。
- **entrance 编排改动**（2026-10-10）：三组来源改在 10-04/05 刚重构建的 PartC/PartD 之上——起飞即生成机制复用，但来源分组/目标序要动；回归靠 §7.2 Play 目检 + PhaseFlightPlanner goldens。
- **名牌 handoff 前移**：交接原语复用现有 shop→combat 流程（canvasShopScale 链），前移后防双名牌同屏闪烁——leg1 期间商店名牌随页出视口、战斗名牌落位，时序上错帧交接。
- pick 时立即注入是时序变化：注入只写 `enemyDeckToPopulate` + hpMax（无 Raise），需确认对 `AnimationStateTracker`/`GameEventStorage` 无副作用。
- entrance 飞行依赖敌方卡组在 leg2 起飞前就绪：pick → 注入 → leg2 顺序强制。
- 选敌页停留期间退出 app：shop_visit 已收口 + 零战斗剔除保护上传；picked 已标 used 但战斗未打——下 run 去重本就重置，无漂移。
- 既有编辑器坑照旧适用：改 `.cs` 后 refresh + 确认 assembly mtime，`run_tests` 前 SaveScene（AGENTS.md 09-19 判例）。

## 9. 开放点（2026-10-10 全部落定）

- O1 ✅ `selectionCandidateCount` 默认 **2**（2026-10-10，布局计划实测两面板并排）。
- O2 ✅ **单击即选**（2026-10-10 追认；点【选择】= Consume + 注入 + leg2）。
- O3 ✅ 卡面预览 = **v2 真卡脸 ×3**（关键卡规则见 §4.3；hpMax 在名牌上，session/deckSize 文本 chip 不做）。
- O4 ✅ 选敌页背景 = **玩家灰全页延伸、无红**（2026-10-10；背景覆盖 +1 页）。
