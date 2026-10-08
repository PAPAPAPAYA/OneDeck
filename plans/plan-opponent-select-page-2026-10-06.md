# Plan: 商店后对手选择页（选敌页）

日期：2026-10-06
状态：**方向已拍板（2026-10-06 四决策，见 §0），实施待「修改代码」**。按 step-gate 协议推进：每步完成后停下汇报，确认后继续下一步。
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

### 4.3 选敌页内容（新组件，不进 god 文件）

- 新文件（如 `Assets/Scripts/UXPrototype/OpponentSelectPage.cs`），世界内容按 `ShopSectionPanels`/`ShopWorldWidgets` 同款模式构建（chromeSprite/chromeFont + PaletteTint 槽位）；无 canvas uGUI 输入，交互全走 PhysButton（world-only 交互裁定）。
- 版式：标题 chip「选择下一场对手」+ N 张候选面板横排（PhysButton）：username / session / deckSize（hpMax 可选）。点击 = 选中即起飞（无确认步，见 O2）。
- UI demo（2026-10-06；2026-10-08 v4 迭代）：`docs/demo/OpponentSelectDemo.html` — 三页全流程（商店→选敌→战斗）。v4 按 10-07 mockup + 10-08 两轮反馈：候选面板 = **ShopSectionPanels 同款平铺半透明深色圆角矩形**（ShopPanelBg #1A3037 @ 0.5，贴合内容、面板自身无阴影，阴影只属于内容）内左 3 张**红色关键卡**（硬阴影、hover 弹起、点击放大、无伤害数字）+ 右侧敌方**名牌**与【选择 +$N】**同宽同右缘**（卡顶=名牌顶、卡底=按钮底；+$N 语义按「挑选奖励」占位待确认）；名牌移植 `docs/demo/HPNamePlateDemo.html` 规格，**阴影带透明度 0.5**（引擎实测渲染 ≈0.45-0.5：Image 名义 0.698 经九宫格 sprite 衰减）；面板名牌与战斗名牌同字号（34px），morph 飞行纯平移。无标题/提示语/页签；最下面一行留给玩家名牌 + 选项按钮（= 战斗位，leg2 不再移动）。两种入场方式可切换：**原位 morph（demo 默认·推荐：镜头不动，HP bar 红退回战斗带/灰区上长，被选名牌直飞战斗右上位，关键卡弧飞入牌堆后方翻背）** vs 镜头移动（plan 原 +2 几何，选敌页背景按备选裁定玩家/敌人二分）。morph 若成立，§8 的 `_combatPageY` 上移风险与消费方 sweep 均消失。
- 无返回商店按钮（D4）。缓存 0 候选不进本页；1..N-1 有几个显示几个。
- 全部位置/尺寸参数挂 config 可调（用户偏好：可独立调参的原件）。

### 4.4 转场接线（PhaseTransitionDriver）

- 新增 `RequestShopToSelection(pm)` 与选敌→战斗 routine；`_selectionPageY = _shopPageY + _pageH`；选敌页启用时 `_combatPageY = _shopPageY + 2*_pageH`，未启用保持现状。
- leg1 为最小编排（rig 飞行 + 商店页滑出/选敌页滑入）；leg2 落地复用现有 `ScheduleCombatEntranceFlights`（敌方/起始卡飞入编排不动）。
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

### 4.6 PhaseTransitionConfigSO 新增

- `selectionPageEnabled`（默认 false，Play 验证通过后置 true）、`selectionCandidateCount`（2-3，默认见 O1）、选敌页飞行时长/滑距等调参字段。

## 5. 上报与遥测口径

- 全部不变：picked deckId 进 match report / `run_combats.opponent_deck_id`；来源计数照旧；未选候选无任何计数事件。
- 选敌页不产生新遥测（「展示了哪些候选」的样本二期再议）。

## 6. 实施批次（step-gate：每步完成停下汇报，确认后继续）

| 步 | 内容 | 验证 |
|---|------|------|
| S1 | §4.1 + §4.2：cache Peek/Consume/reservation + DeckSaver 预留分支 + 幂等护栏 | EditMode 专项（§7.1）+ 全量回归零漂移 |
| S2 | §4.3 + §4.6：选敌页世界内容 + config 字段 + 输入门 | EditMode bypass 矩阵；编辑器内目检 |
| S3 | §4.4：两段转场接线 + `_combatPageY` 上移 + 消费方清点 | Play Mode 人工全流程（§7.2）+ RegressionChecklist 追加行 |

## 7. 测试

### 7.1 EditMode（并入 OpponentDeckCacheTests 或新建 OpponentSelectTests）

- Peek：不标记已用；数量 ≤ N；`fightOwnGhostsOnly` 过滤生效；固定 `Random.InitState` 下多次调用可命中非首条（延续 09-13 测试手法）。
- Consume：标记 used + `Current` 更新；重复 consume 幂等；未选条目仍可被后续 `TakeCandidate` 命中（D3 回池断言）。
- 预留分支：populate 使用 picked 内容（deck/hpMax/username 对上）+ 清 reservation；无 reservation 走现状链；ResetRun 清预留；同 session 重复 populate 不换人。
- Bypass：§4.5 全行断言（选敌页不创建、离店走现状请求）。

### 7.2 Play Mode（人工）

- 正常流：离开商店 → 选敌页 → 点选 → leg2 → 战斗对手 = 所选（VS 行 username 对上、上报 deckId 对上）。
- bypass 各分支走现状行为；离线 0 候选走 default pool；两段飞行视觉与 entrance 编排无回归。

## 8. 风险与注意

- `_combatPageY` 上移一屏：读 `CombatPageY` static 的消费方自动跟随；但任何「商店 + 一屏」硬编码假设要清点（CombatHPBarPresenter HudWorldFlight、entrance 落点探针、PhaseTransitionDemo）。**S3 首项做消费方 sweep**。
- pick 时立即注入是时序变化：注入只写 `enemyDeckToPopulate` + hpMax（无 Raise），需确认对 `AnimationStateTracker`/`GameEventStorage` 无副作用。
- entrance 飞行依赖敌方卡组在 leg2 起飞前就绪：pick → 注入 → leg2 顺序强制。
- 选敌页停留期间退出 app：shop_visit 已收口 + 零战斗剔除保护上传；picked 已标 used 但战斗未打——下 run 去重本就重置，无漂移。
- 既有编辑器坑照旧适用：改 `.cs` 后 refresh + 确认 assembly mtime，`run_tests` 前 SaveScene（AGENTS.md 09-19 判例）。

## 9. 开放点（实施中再拍）

- O1：`selectionCandidateCount` 默认 3 还是 2（先按 3 配）。
- O2：单击即选 vs 选中 + 确认按钮（先按单击即选）。
- O3：候选面板是否带 hpMax / 卡组构成预览（v1 文本 chip，卡面预览二期）。
- O4：选敌页背景是否复用 v1.1 topo 红带样式。
