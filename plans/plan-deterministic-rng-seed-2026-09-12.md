# 随机数种子化与战斗复现方案 (Deterministic RNG Seed)

日期: 2026-09-12
状态: **已实施** (2026-09-15, Step 1-4 全部落地; 双程序集编译 0 错; EditMode 套件 RngDeterminismTests 新增, 全量套件与 Play 观感待验; 工作流文档 docs/RngDeterminism.md)
目标: 用随机种子稳定复现 bug, 关键覆盖战斗内随机 (牌序 / 效果目标随机等)
关联: docs/GameRules.md, docs/RegressionChecklist.md, TestManager 三开关 (testmanager-test-toggles-split), DeckSaver Recorded session 文件夹

## 0. 拍板记录 (2026-09-12, 用户按推荐采纳)

现状比预想乐观: 效果层随机几乎全部经过一个漏斗 (`UtilityFuncManagerScript.ShuffleList`), 散点仅十来处; `ShopBoardPipeline` 已是注入 Random 的干净接口; `ShuffleOrderOverride` 已提供"固定牌序"先例。

| # | 决定 | 采纳 | 依据 |
|---|------|------|------|
| 1 | 总体路线 | 方案 A: 新建 RngService (System.Random 子流) | 见 §2.2 — InitState 单流下"改 UI = 改牌序" |
| 2 | 复现单元 | 战斗级 (seed + 双方 deck) | 见 §2.3 — 战斗无玩家输入分支, 材料现成 |
| 3 | 旧种子兼容 | 一次性断裂可接受; 只承诺同版本内复现 | 见 §4.2 — 现在不存在存量 seed, 断裂零成本 |
| 4 | 子流划分 | Deck / Target / Shop / Setup 四通道 | 见 §2.1 — 消耗序解耦, "改一行 ≠ 换一局牌序" |

一句话立场: **用一次约 15 文件的机械改动 + 明确"跨版本不复现"的预期, 换取复现窗口对 UI 改动、代码迭代、玩家行为三个变量全部免疫。**

## 1. 现状盘点 (2026-09-12 全量 grep 核实)

### 1.1 影响战斗结果的随机 (必须种子化)

| 调用点 | 用途 | 通道归属 |
|--------|------|----------|
| `Managers/UtilityFuncManagerScript.cs:31` `ShuffleList` (`OrderBy(x => Random.value)`) | 唯一洗牌/随机选取原语 | Deck + Target 双用 |
| `Effects/StartCardShuffleEffect.cs:63` | **每回合牌序** (首洗也在这里; `GatherDecks` 本身不洗牌, 只按 玩家牌+敌方牌+StartCard置底 固定顺序合并) | Deck |
| `Effects/StartCardShuffleEffect.cs:78` | Start Card 落位 (Gaussian, 经 `GaussianRandom`, 当前配置 AlwaysBottom 但代码路径在) | Deck |
| `Effects/BuryEffect.cs` 140/161/259/280/301、`Effects/ExileEffect.cs` 66/87/108/132/156/177/199/221/249、`Effects/StageEffect.cs:94`、`Effects/CardManipulationEffect.cs` 79/226、`Effects/ReviveEffect.cs:163` | 目标池打乱后取前 N | Target |
| `Card/CardScript.cs:372` | 同攻击力并列候选的随机 tie-break | Target |
| `Effects/AddTempCard.cs:155`、`Effects/AttackTimesGiverEffect.cs:61`、`Effects/GravePuppeteerEffect.cs:51`、`Effects/StatusEffect/StatusEffectGiverEffect.cs:209/361`、`Effects/StatusEffect/AttackGiverEffect.cs:83/241` | 直接 `Random.Range` 选目标/选卡 | Target |
| `Managers/ShopManager.cs:517` (`new System.Random()` 未种子)、`:717-719` (`RollBoardDiscountOffPercent` 未种子 + `Random.Range` 折扣索引) | 商店货架生成/半价折扣 | Shop |
| `Managers/StartingCardManager.cs:87`、`Managers/CombatStartCardGiver.cs:87`、`Managers/WriteRead/DeckSaver.cs:491` | 起始卡池/奖励池/默认敌方牌池随机选 | Setup (局外) |

### 1.2 纯视觉随机 (保留 UnityEngine.Random 不动)

| 调用点 | 用途 |
|--------|------|
| `UXPrototype/DeckLayoutOffsetProvider.cs:25-33` | 卡片落位 jitter (每次 relayout 消耗 6 次) |
| `UXPrototype/DamageFloaterPresenter.cs:296-297` | 跳字 jitter (每次 hit 消耗 2 次) |
| `UXPrototype/CombatUXManager.cs:1751/3781/3893/3988` | stagger 动画延迟 / 弹出范围 (洗牌一次最多消耗几十次) |
| `Net/PlayerIdentity.cs:50` | 随机玩家名 |
| `Managers/UtilityFuncManagerScript.cs:122` `RandomPointOnUnitCircle` | 当前无调用者 (死代码, 顺手确认) |

### 1.3 已成立的利好前提 (决定方案可行性)

1. **逻辑/动画两段式**: 效果逻辑阶段同步执行, 动画后放 (RecorderAnimationPlayer)。RNG 消耗顺序天然单线程确定, 与动画时长/帧率无关。
2. **ShopBoardPipeline 已注入 Random**: `GenerateBoard(..., System.Random)` 接口干净, grep 确认内部零 `UnityEngine.Random` 泄漏; 商店只需把调用处的 `new System.Random()` 换成种子流。
3. **ShuffleOrderOverride 先例**: TestManager 已有开关把洗牌替换为固定 prefab 序列 (Tutorial 同机制), 证明"牌序可固定"已有基础设施; 本方案把它从"只固定首洗"推广到"全部随机可固定"。
4. **Effects 目标池无 HashSet/Dictionary**: 池子均由 `combinedDeckZone` 等 List 线性构建, 迭代顺序确定, 无哈希容器顺序风险 (已 grep 核实)。
5. **DeckSaver session 文件夹已存双方 deck**: 复现材料只缺 seed 一项。

## 2. 方案设计

### 2.1 RngService: 主种子 + 命名子流

静态服务持有 master seed, 按通道名哈希派生子流 (SplitMix 风格), 每条子流一个 `System.Random` 实例:

```
Rng.Deck      — 战斗牌序 (StartCardShuffleEffect 洗牌 + Gaussian 落位)
Rng.Target    — 效果目标随机 (ShuffleList 默认通道 + 全部散点 Random.Range)
Rng.Shop      — 商店货架 / 折扣 (ShopBoardPipeline 注入 + ApplyBoardDiscount)
Rng.Setup     — 局外: 起始卡 / 奖励 / 默认敌方牌池
```

API 草图:

```csharp
public static class Rng
{
	public static void InitMaster(int seed);
	public static System.Random Channel(RngChannel c);   // SplitMix 派生: hash(masterSeed, channel)
	public static List<T> Shuffle<T>(RngChannel c, List<T> list);  // Fisher-Yates
	public static int Next(RngChannel c, int maxInclusive);
}
```

`ShuffleList` 加通道参数 (默认 `Rng.Target`), 调用方签名基本不变; 内部由 `OrderBy(Random.value)` 换 Fisher-Yates。

**分通道的核心动机 — 消耗序解耦**: 设想往某个 effect 的目标选取里加一行调试用 `Rng.Next`, 或某张新卡机制上多摇一次随机 — 单流下这一行代码会让**同 seed 战斗的所有后续洗牌结果漂移** ("改一行 = 换一局牌序"), 回归 diff 全红且无从定位。分通道后, Target 流里的额外消耗只影响目标选取, Deck 牌序纹丝不动; digest 校验时也能直接看出是哪个通道的消耗变了。

**为什么恰好四条** (按"消耗节奏 + 变更耦合风险"分层, 不多不少):

| 通道 | 消耗节奏 | 独立出来的理由 |
|------|----------|----------------|
| Deck | 每回合一次 (洗牌+落位) | 牌序是复现的锚, 最怕被别的消耗挪动 |
| Target | 每效果一次, 频率最高 | 改动最频繁的区域 (4.0 卡池持续迭代) |
| Shop | 每次进商店/reroll | 消耗次数取决于玩家行为, 混入战斗流会随玩家 reroll 次数漂移 |
| Setup | 每局一次 | 起始卡/敌方牌池, 频率最低, 隔离成本为零 |

再细 (按 effect 分) 没有意义: 同一通道内消耗顺序本来就确定, 通道只按耦合风险分层; 再粗 (战斗/非战斗两条) 也能用, 但 Shop 消耗次数依赖玩家操作, 混进 Setup 会连累敌方牌池选择跟着漂 — 省一条流的成本是零, 没必要省。实现成本可忽略: SplitMix 哈希多算三次 + 三个额外 `System.Random` 实例。

### 2.2 为什么不用 Random.InitState 单流 (已否决的备选)

**方案 B 长什么样**: `GatherDecks` 里加一行 `Random.InitState(seed)`, 现有调用点一行不改自动被种子化, 半天上线 — 这是它唯一优点。

**致命伤是 Unity Random 的全局单流本质**: 全项目所有消费者从同一条序列取数, 包括纯视觉三处 (§1.2 前三行)。后果用具体例子说:

1. 关掉 `useStaggeredShuffleAnimation` → 洗牌阶段少消耗几十次随机 → **之后每一回合牌序全变**;
2. 将来某个 hover/popup 修复让一张卡多 relayout 一次 → 牌序漂移;
3. 即"改 UI = 改牌序", 种子复现窗口被动画设置、UI 改动这些本不该相关的变量污染, 且无任何编译期保护, 只能靠纪律 — 本项目 UI 高频迭代, 纪律守不住。

方案 A 的本质是把战斗随机从公共流搬进自己的 `System.Random` 实例, 视觉随机留在 Unity Random 不管。代价是约 15 文件机械替换。判断依据一条: 项目临近收尾只想快速抓 bug 时 B 才是对的; OneDeck 还在活跃开发 (4.0 池子、UX 都在动), B 的脆弱耦合会持续制造"种子一样但结果不一样"的假案, 排查假案的时间远超 A 的一次性成本。

### 2.3 种子作用域与入口

**复现单元定为一场战斗** (拍板 #2):

- **整局回放不可行的原因**: 一场战斗的完整输入只有两样 — 双方 deck 内容 + seed, 因为战斗流程没有玩家分支 (揭示顺序由牌序决定, `autoReveal` 只是把点击自动化; 效果目标由引擎决定, 玩家不选)。而整局里商店阶段有真实玩家输入 (买什么/卖什么/reroll 几次), 要整局复现就得做输入录制回放系统, 工程量大一个数量级, 且用户报 bug 时说不清"当时第 3 次进商店买了哪张"。
- **战斗级的材料是现成的**: session 文件夹已存双方 deck, 缺的只有 seed 一个数字; 复现工作流收敛为"把 session 文件夹给我就行"。bug 主体恰在战斗内; 商店侧随机本就有干净接口 (`ShopBoardPipeline` 可注入种子), 不依赖回放。两者不互斥: 以后要复现局级 bug (如"连续 N 局商店不出某卡") 可在此之上加输入录制。

种子来源与入口:

- **自动模式**: combat seed = hash(runSeed, sessionNumber); runSeed 每次 ResetRun 生成。
- **覆盖模式**: TestManager 加序列化字段 `overrideCombatSeed` (0 = 关闭走自动); 打包版经 `Application.arguments` 解析 `-odseed N`。
- **日志**: `GatherDecks` 时输出 `[Seed] combat=N seed=X playerDeckHash=... enemyDeckHash=...`; `[Seed]` tag 必须登记进 `TestManager.InferCategory`, 否则被路由静默吞 (前缀盖过 tag 的坑见 memory)。
- **落盘**: DeckSaver/EnemyDeckRecorder 的 Recorded session 文件夹内追加写 `seed.txt` (含 seed + Application.version + 两个 deck 文件名)。bug 报告 = session 文件夹, 即完整复现材料。

### 2.4 验证闭环: determinism digest

每场战斗对以下内容做 FNV-1a 哈希, 写入 session 文件夹 `determinism_digest.txt`:

1. 揭晓序列: 逐张 `(cardTypeID, ownerSide)` 拼接哈希;
2. 伤害账本: 每 `(cardTypeID, creatorSide)` 的 HP 损失合计。

**同 seed + 同双 deck → digest 必须逐字节相同。** 落成 EditMode 测试 (沿用 `HeadlessCombatTestFixture` + `NullCombatVisuals` 现成体系): 同种子跑两遍断言 digest 相等, 把"确定性"变成回归套件可守的东西, 而非肉眼对局。

## 3. 落地步骤 (等「修改代码」再动)

| Step | 内容 | 涉及文件 |
|------|------|----------|
| 1 | RngService.cs + TestManager.overrideCombatSeed + GatherDecks 种子日志/落盘 (不含调用点迁移) | 新增 1, 改 2 |
| 2 | ShuffleList 加通道参数; 迁移 StartCardShuffleEffect (Deck) + GaussianRandom; 同种子双局 digest 校验 | UtilityFuncManagerScript, StartCardShuffleEffect |
| 3 | Target 散点迁移 (§1.1 Target 行全部), 机械替换 | Bury/Exile/Stage/CardManipulation/Revive/CardScript/AddTempCard/AttackTimesGiver/GravePuppeteer/StatusEffectGiver/AttackGiver |
| 4 | Shop/Setup 三处 + digest EditMode 测试 + docs 复现工作流一页 | ShopManager, StartingCardManager, CombatStartCardGiver, DeckSaver, 新增测试 |

规模: 新增 1 个服务文件 + 约 15 个文件的机械改动。注意: `StartingCardManager.cs` 现为空格缩进, 动它时按 AGENTS.md 规范顺手转 Tab。

## 4. 边界与风险

### 4.1 Listener 注册顺序

GameEvent 监听顺序 = OnEnable 顺序; 卡实例按牌序创建 (确定), 场景静态对象按序列化顺序 (同场景稳定)。同版本 + 同场景下无风险; 改场景结构可能改变触发顺序 — 属于"种子保不住"的合理范围, digest 测试会立刻暴露。

### 4.2 版本承诺边界 (拍板 #3)

- **现在不存在"旧种子"**: 当前代码从未调用 `InitState`, UnityEngine.Random 默认每次启动用不同熵源, 本来就没有可复现性 — 切换不存在"存量 seed 失效"的迁移成本, 断裂零代价, 趁现在不做以后才要付成本。
- **本项真正拍的是对未来版本的承诺边界**: seed 复现只保证"同一代码版本内成立", 跨版本不承诺。种子序列对实现极度敏感 — Fisher-Yates 实现细节、某通道多消耗一次、甚至某池子构建顺序变化, 同 seed 就会出不同结果; 要跨版本复现就得冻结 RNG 实现和所有消耗点, 会实质拖慢之后的每次效果改动, 不值。
- **语义预期立住**: 1.2.3 版的 seed 拿到 1.2.4 版复现不出来不是 bug。seed.txt 记录 `Application.version`, 报 bug 时版本对不上先换版本再说。

### 4.3 异步 PvP

战斗是本地模拟, 敌方 deck 来自服务器固定数据, 单机 seed 即可覆盖; ghost 对局的 deck 材料 session 文件夹本来就有。

### 4.4 视觉随机不迁移

§1.2 全部保留 UnityEngine.Random, 有意与战斗随机解耦 (见 §2.2)。
