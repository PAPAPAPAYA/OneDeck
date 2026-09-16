# 随机数种子化与 bug 复现工作流 (Deterministic RNG)

日期: 2026-09-15
方案: `plans/plan-deterministic-rng-seed-2026-09-12.md` (已拍板并实施)
实现: `Assets/Scripts/Managers/RngService.cs` (Rng / RngDigest / DeterminismTracker), EditMode 测试 `RngDeterminismTests.cs`

## 1. 原理

战斗逻辑随机全部走 `Rng` 服务 (System.Random 实例), 与 `UnityEngine.Random` (纯视觉: 落位 jitter / 跳字 jitter / stagger 延迟) 完全隔离。四条通道:

| 通道 | 用途 | 种子来源 |
|------|------|----------|
| `RngChannel.Deck` | 每回合牌序 (Start Card 洗牌 + Gaussian 落位) | combat seed |
| `RngChannel.Target` | 效果目标随机 (`ShuffleList` 默认 + 散点选取) | combat seed |
| `RngChannel.Shop` | 商店货架 / 半价折扣 | combat seed |
| `RngChannel.Setup` | 起始卡 / 战斗奖励 / 默认敌方牌池 | run seed (每次 ResetRun 重摇) |

- combat seed = override > 0 ? override : hash(runSeed, sessionNumber);每场战斗 `GatherDecks` 时重播 Deck/Target/Shop 三条通道。
- 冷启动（场景加载即进商店, 早于任何显式 init）同样可复现: 任一通道首次被用时若 runSeed 未生成则先 `NewRun()`（Setup 随即接入）, 战斗通道以 `ComputeCombatSeed(0)`（session 0 = 首战前窗口）播种, 与第一场战斗 `GatherDecks` 同值重播。正常游玩不应再出现 `[Rng]` 懒初始化警告。
- 关键性质: **通道间消耗互不干扰** — 往 Target 链路加一行调试摇号不会改变牌序;改 UI/动画不会改变战斗。
- 承诺边界: 只保证**同一代码版本 + 同 Unity 版本内**复现。跨版本 RNG 实现变化会改变序列, 不是 bug。

## 2. 复现一次战斗 (三步)

1. **拿材料**: bug 报告附带 `CombatLogs/Seed_SessionN_*.txt` (combat 起始时自动写入, 含 seed / session / version / 双方 deck 名)。同一文件夹的 `DeterminismDigest_SessionN_*.txt` 是该战斗的指纹。
2. **设种子**: 任选其一 —
   - 编辑器: `TestManager.overrideCombatSeed` 填 seed (0 = 正常随机);
   - 打包版: 命令行加 `-odseed N`。
   同一 run 内每场战斗都会用这个 seed 重播 (log 里 `combat=N seed=X (override)` 区分场次)。
3. **复跑**: 重进该场战斗 (deck 必须一致 — `Seed_*.txt` 里的 deck 名 + 对应 deck 存档;ghost 对局用 `onlyGhostEnemyDeck`/缓存控制)。对比 `DeterminismDigest` 的 `revealDigest` / `damageDigest`: 完全一致 = 复现成功;不一致 = 存在 RNG 泄漏或 listener 顺序漂移 (见 §4)。

## 3. 日常使用

- **修 bug**: 设 override seed → 稳定复现 → 修复 → 同 seed 重跑验证 digest 一致。
- **回归哨兵**: `Assets/Scripts/Editor/Tests/RngDeterminismTests.cs` 守住"同种子同序列 / 通道隔离 / digest 顺序敏感"三条底线;全战斗级 digest 对照 (同 seed 双局) 可用 headless fixture 扩展。
- **新代码规范**: 战斗/商店/局内逻辑一律 `Rng.Next(RngChannel.X, n)` / `Rng.Shuffle(RngChannel.X, list)` / `UtilityFuncManagerScript.ShuffleList(list, RngChannel.X)`;禁止在逻辑路径新增 `UnityEngine.Random`。视觉随机 (jitter/stagger) 保持 UnityEngine.Random 不动。
- **日志**: `[Seed]` 前缀已登记进 `TestManager.InferCategory` → CombatFlow 开关;`CombatLogs/` 下的 seed/digest 文件仅在 Play 模式写入 (EditMode 测试不产生文件)。

## 4. 已知边界

- **digest 不一致排查顺序**: ① 版本是否一致 (seed.txt 的 `version=`);② 双方 deck 是否一致;③ 是否改过场景结构 (GameEvent 监听按 OnEnable 顺序, 场景对象序列化顺序变化会改变触发顺序 — 属"种子保不住"的合理范围);④ 最后才是逻辑路径 RNG 泄漏 (grep 新增的 `UnityEngine.Random` 调用)。
- **商店随机跨战斗耦合**: Shop 通道每场战斗重播, 同一场战斗内多次 reroll 是确定序列;跨战斗比较商店内容需同 seed 同 session。
- **旧存档/旧 digest**: 实施前的 digest 文件与新序列不可比。
