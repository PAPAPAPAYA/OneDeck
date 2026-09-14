# Plan: HP 归零后剪断动画队列（死亡中止播放）

日期: 2026-09-14。状态: **已实施** (同日落地, dotnet 离线编译 0 错; EditMode 回归 5 用例全绿 `DeathAbortPlaybackTests`, 全量套件 529/530 仅 1 存量 Ignore; Play 观感待验证; 未提交)。回归项: `docs/RegressionChecklist.md` row 99。

## 背景与问题

- 现状: 战斗中一方 HP 归零后, 当前卡批次的动画（多段攻击、反应链、跳字、状态弹道等）仍然全部播完, 之后才显示 COMBAT FINISHED。
- 需求: HP 归零（玩家看到死亡）后停止播放队列中的剩余动画, 直接继续收尾流程。

## 现状链路（代码事实）

1. **逻辑 HP 在链逻辑阶段即归零**：效果同步执行, `PlayerStatusSO.hp` 在播放开始前已是 ≤ 0。
2. **显示 HP 是队列冻结的**：`CombatInfoDisplayer.SnapshotHpDisplay`（冻结 preHit）→ 动画落地回调 `CommitHpDisplay`（提交该 hit 自身 hpLoss）→ `onHpDisplayCommitted` 生成跳字。快照/提交挂在 `HPAlterEffect.cs:194/206`（攻击路径）与 `:502`（直接伤害路径）。`GetDisplayedOwnerHp/GetDisplayedEnemyHp` 返回"玩家此刻看到的 HP"。
3. **死亡判定太晚**：`CombatManager.cs:884` 的 `hp <= 0` 检查只在 Phase 1（推进下一张卡）执行, 而 `HandleCombatFinished`（`:1197`）还有 `isPlayingEffectAnimations` 守卫——当前批次必然播完。
4. **播放结构**：`PlayRecorderAnimationsAndWait`（`CombatManager.cs:606`）→ 关链 → `RecorderAnimationPlayer.PlayRecordersCoroutine`（root 循环）→ `PlayRecorderCoroutine`（请求 foreach + 子 recorder 递归）→ 全嵌套协程; `finally` 已按"interrupted playback"设计兜底（held 源卡清集合、baseline commit）。

## 方案设计（三个关键决策）

1. **中止谓词 = 显示 HP ≤ 0, 不是逻辑 HP**。逻辑 HP 在播放开始前就已归零, 用它做谓词会连致命一击动画一起剪掉（凭空暴毙感）。显示 HP 归零 = 致命一击已落地（攻击命中 + 跳字 + HP 条归零）的信号, 天然实现"致命一击播完 → 后面全剪"。落点: `CombatManager.IsDeathVisuallyLanded`。
2. **检查点粒度 = 请求之间**（协程内 `yield break`）, 不在中途 kill 正在飞的那一击——致命段完整落地后才剪。
3. **清 pending 快照锁用 `ClearHpDisplayLocks()`**（静默重同步到 live HP）, **禁用补 `CommitHpDisplay`**——后者会触发 `onHpDisplayCommitted`, 给从未播放的动画生成幽灵跳字。`_deathAbortHandled` 标志保证每批只清一次。

## 改动清单

| 文件 | 改动 |
|------|------|
| `Managers/CombatManager.cs:1204` | 新增 `IsDeathVisuallyLanded` 属性: `CombatInfoDisplayer` 存在时取显示 HP ≤ 0, headless（displayer 为 null）回退逻辑 HP |
| `Managers/RecorderAnimationPlayer.cs:77` | `PlayRecordersCoroutine` root 循环顶部检查点（附 VISUAL-FIX(2026-09-14) 完整块） |
| `Managers/RecorderAnimationPlayer.cs:143` | `PlayRecorderCoroutine` 顶部检查点（在 `animationPlayed = true` 之前; 子 recorder 递归与后续 root 都经此方法, 自动覆盖） |
| `Managers/RecorderAnimationPlayer.cs:320` | 请求 foreach 顶部检查点（剪同 recorder 内致命段之后的剩余请求） |
| `Managers/RecorderAnimationPlayer.cs:120` | `AbortPlaybackForDeath(checkpoint)` helper + `_deathAbortHandled` 字段 + 批次开始时 reset |
| `docs/RegressionChecklist.md` | row 99（⚠️ 待 Play 验证） |

日志走既有 `[RecorderAnimationPlayer]` 前缀, 无需新登记 TestManager 路由。

## 中止后的收尾（零改动, 走既有路径）

外层 `PlayRecorderAnimationsAndWait` 的 finally 照常: 标记全部 recorder `animationPlayed=true` → `ResetInputBlock` → `WaitForAttackAnimationsBeforeNextReveal` → `UpdateAllPhysicalCardTargets` → `isPlayingEffectAnimations=false`。下一次推进时 `CombatManager.cs:884` 命中 → `HandleCombatFinished` → "COMBAT FINISHED"（手动模式仍需一次点击, `autoReveal` 下一帧直接进结算）。

## 明确不改的部分

- **逻辑层链照常跑完**: 死亡后的反应效果（亡语、连坐等）在逻辑上仍结算, 本方案只剪它们的动画。若要连逻辑一起剪（死亡即断链）, 涉及复活/亡语次序语义, 风险高, 属独立方案。
- `HandleCombatFinished` / `PhaseManager` / HP 显示组件不动。

## 边界与风险

- **xN 多段攻击**: 致命段落地后, 同批剩余段被剪; 逻辑伤害已全部生效, `ClearHpDisplayLocks` 把显示一次性重同步到 live 0——有轻微跳变, 战斗已结束, 可接受。
- **回合开始 flush / afterShuffle 自动播**（`RoundStartFlushThenRevealRoutine` 等）与主流程同一入口, 自动覆盖。
- **中止时 held 在 popup 峰值的源卡**: `finally` 只清集合不做 slot-in（该路径注释即为此设计）; `UpdateAllPhysicalCardTargets` 拉回, `ClearAllPhysicalCards` 收尾, 无残留。
- **死亡后逻辑又奶回一口**（监听者给已死方回血致逻辑 HP > 0）: 884 行本就不会判结束——既有语义, 不触碰。
- **双方同批死亡**: 同一路径, 一次中止, 先落地的一方决定剪断时刻。
- **headless/EditMode**: 无 `CombatInfoDisplayer.me` 时回退逻辑 HP; `NullCombatVisuals` 下无播放, 无行为变化。

## 验证方案

- **Play（row 99, 待执行）**: 多段攻击致死（剪在致命段落地后）/ 单击致死 / 反应链致死（致命一击在子 recorder）; 胜负双向; 非致命批次完整播放不受影响; 检查无幽灵跳字、HP 显示终值为 live 值、无输入锁泄漏。
- **EditMode 用例（已实施 2026-09-14）**: `Assets/Scripts/Editor/Tests/DeathAbortPlaybackTests.cs` 5 用例 — 谓词只认显示 HP（逻辑 HP=0 + 冻结 4 → false, commit 落 0 → true）/ 多段致命段落地后剪断（onHit→Commit 复现落地, 剪后无幽灵 commit、清锁恰一次、显示重同步 0）/ 非致命批完整播放（清锁 0 次）/ headless 逻辑 HP 兜底（临时置空 `CombatInfoDisplayer.me`）/ 每批重臂（第二批 root 检查点再剪再清锁）。驱动模式要点: 直接驱动 `PlayRecorderCoroutine`（套件既有可靠模式）, wrapper 内嵌 `StartCoroutine` 在 EditMode runner 下时序不稳（见 RecorderAnimationPlayerTests 存量 Ignore 注释）, root 检查点改用单步 `MoveNext` 覆盖（中止在首个 yield 之前, 完全同步确定）。

## 执行状态

- [x] CombatManager `IsDeathVisuallyLanded`
- [x] RecorderAnimationPlayer 三检查点 + helper + 每批一次锁清理
- [x] RegressionChecklist row 99
- [x] EditMode 回归用例 `DeathAbortPlaybackTests`（5 用例全绿; 全量套件 529/530, 1 存量 Ignore）
- [x] dotnet 离线编译 0 错（13 存量警告, 与本次无关）
- [ ] Play 观感验证（用户）
- [ ] 提交（验证后拆分）
