# 三大管理类拆分方案:CombatManager / RecorderAnimationPlayer / CombatUXManager

日期: 2026-09-16
状态: **拍板完成,未实施** (2026-09-16 四项全部按低风险选项拍板,见 §7;AGENTS.md Code Placement Guide 已落地;其余切片未动工)
目标: 评估三个最庞大管理类是否该拆、怎么拆,给出可分步执行、每步可验证的低风险路线
关联: plans/refactor-combat-ux-manager-2026-05-17.md (阶段2未执行的旧案,本文档 §4 复活并修正), docs/RegressionChecklist.md, docs/AnimationSystem.md, docs/DeckLayouts.md

## 0. 一页结论

| 类 | 行数 | 裁决 | 一句话理由 |
|----|------|------|-----------|
| CombatManager | 1367 | **拆,低风险切片** | 真问题是 RevealCards 单方法 ~250 行;类本身未失控,facade 不动、职责外移即可 |
| RecorderAnimationPlayer | 1671 | **缓拆,只摘纯计算** | 单一职责的播放引擎,复杂度是内在的;真问题是 PlayRequestCoroutine 单方法 ~730 行;但它是全项目 bug 密集区,动它收益低风险高 |
| CombatUXManager | 4637 | **必须拆,最高优先级** | 4 个月涨 2669 行(+136%),05-17 旧案阶段2从未执行;但旧案"五组件一次拆"被证明推不动,改为 partial-class 过渡 + 逐个组件摘除 |

全局前置条件:等当前工作区未提交工作(RNG 验证 / 触发顺序 / 商店尾区 / PhysButton)落库后再动任何一片;每片一个 commit,全量 EditMode 绿(基线 ~545)才进下一片。

## 1. 体量事实 (2026-09-16 实测)

### 1.1 三类对比

| 指标 | CombatManager | RecorderAnimationPlayer | CombatUXManager |
|------|--------------|------------------------|-----------------|
| 行数 | 1367 | 1671 | **4637** |
| 方法数(约) | 60 | 30 | 106 |
| 最大方法 | RevealCards ~250 行 (783-1030) | **PlayRequestCoroutine ~730 行 (941-1671)** | SyncPhysicalCards ~373 行 (299-672) |
| 200+ 行方法数 | 1 | 1 | 5 (373/287/269/243/220) |
| 外部引用文件数 | 15+ (CM.Me);Editor 测试 32 文件 | ~30 文件 | 19 文件 |
| 对其他两类的依赖 | 创建 RAP (Awake) | CM.Me ×20 处, CUX ×23 处 | 被 RAP 依赖 |

### 1.2 外部耦合面(决定拆分成本)

- **CombatManager**:外部高频访问集中在 `revealZone`(13 处)、`combinedDeckZone`(13 处)、`visuals`(8 处)、双方 PlayerStatusRef(13 处) → Zones + 状态引用是共享心脏,**不能动 API**,只能内部切片。
- **RecorderAnimationPlayer**:被 ~30 个文件引用,但公共入口很窄(`PlayRecordersCoroutine` + 少量标记/查询方法)。它同时握有"播放计划"(哪些 delta 何时提交)和"播放执行"(协程时序)两种职责。
- **CombatUXManager**:外部调用面窄得意外 —— `CombatUXManager.me` 裸传参 21 处,具体成员合计仅 ~20 处(statusEffectConsumePos 6、deckMoveArcDuration 5、hover 锚点 4、physicalCardInRevealZone 2、其余零星)。**对外收缩成 facade 完全可行**;它的臃肿几乎全是内部问题。

## 2. CombatManager 方案 (定稿级)

原则:**`CombatManager.Me` 公共 API 100% 不变**,所有 ~100 个外部调用点零改动;内部按风险从低到高摘切片。

| # | 切片 | 去向 | 搬什么 | 风险 |
|---|------|------|--------|------|
| CM-1 | InputBlockCounter | 新组件/纯 C# 类 | BlockInput / UnblockInput / ResetInputBlock / IsInputBlocked / InputBlockCount (~40 行,无 serialized 字段) | 极低 |
| CM-2 | ThroneZone 组件 | 新 MonoBehaviour(同 GameObject) | FindStartCardInstance / GetStartCardIndex / GetThroneZoneCards / IsCardInThroneZone / MoveCardToThroneZone | 低 |
| CM-3 | FatigueHandler 组件 | 新 MonoBehaviour(同 GameObject) | CheckFatigueNAddFatigue / CheckFatigueByRevealCount / AddFatigueCards / CaptureFatigueCardAnimation + overtime/fatigue 配置字段 | 低 |
| CM-4 | RevealCards 方法级拆分 | 原类内 | 250 行切成命名阶段方法(RevealNext / TriggerEffect / AdvanceRound / FinishCombat);**不换类** | 中,545 测试兜底 |

- CM-4 完成后再评估 reveal 管线是否值得独立成类;**预判不需要**,本项目的形态就是"胖主循环 + 细职责组件"。
- 组件挂同一 GameObject、由 CombatManager.Awake 自行 GetComponent 创建(或 lazy),**场景不需要重拖引用**(config 字段随切片走,用 FormerlySerializedAs 保持反序列化)。
- 测试注意:Headless 测试通过反射塞 CombatManager 私有字段;overtime/fatigue 字段搬家后需同步 fixture(预计 1-2 个 fixture 小改,拆前先 grep `fatigueRevealThreshold` 定位)。

## 3. RecorderAnimationPlayer 方案 (缓拆)

### 3.1 诊断

- 它是单一职责的**播放引擎**:复杂度来自"多 recorder 树交错 + 协程时序 + 死亡中断"的内在难度,不是职责混杂。组件化拆不出边界。
- 但两类可治理的问题:
	1. **PlayRequestCoroutine ~730 行**,按 request 类型铺开的巨型协程,历次 bug(回底竞态 / defer-landing / death-abort)全落在这一个方法里。
	2. 播放前有一段**纯计算前置段**(~595-941,约 350 行):MarkDeferredDisplayCommits / ApplySpawnDeltasForProjectile / ComputeAndApplyDisplayBaselines / CollectAllRecorders / ComputeSourceCardPendingCounts / ApplyDeferredDeltasForTarget。它们无协程、无时序、输入输出明确。

### 3.2 步骤

| # | 动作 | 说明 |
|---|------|------|
| RAP-1 | 纯计算前置段外移为静态类(如 `RecorderPlaybackPlanner`,纯 C# 无 MonoBehaviour) | ~350 行机械搬移;这些方法可直接单测,是现有 RecorderAnimationPlayerTests 之外的增量覆盖点 |
| RAP-2 | PlayRequestCoroutine 按 AnimationRequestType 拆成 per-type 私有协程 | 主方法退化为 switch 分发;**纯搬代码不改时序**,death-abort 检查点(AbortPlaybackForDeath)的调用位置逐字保留 |
| RAP-3 | (暂缓)播放器组件化 | 不做。等 RAP-1/2 落地后再看是否还有必要 |

- 红线:RAP 与 CM/CUX 各 20+ 处互相引用,三者的调用顺序是动画系统正确性本身;RAP 一律排在 CM、CUX 切片**之后**做,避免同时动三角的两条边。
- 现有兜底:RecorderAnimationPlayerTests / DeathAbortPlaybackTests / HeadlessCombatTestFixture。

## 4. CombatUXManager 方案 (最高优先级,复活并修正 05-17 旧案)

### 4.1 为什么旧案要修正

- 05-17 案阶段1(CardMoveConfig / DeckPositionCalculator 抽取 + 字典缓存)已落地;**阶段2"五组件一次拆"至今未执行**。
- 期间 CUX 从 1968 → 4637 行(4 个月 +2669 行,约 670 行/月)。结论:**大爆炸式组件拆分在这个项目推不动**(等一个"完整窗口"永远等不到),必须改成"每片独立可合入"的流水线,并加增长控制。

### 4.2 Region 盘点 (今日行号,漂移 ±几行不影响结论)

| Region | 行范围 | 行数 | 去向 |
|--------|--------|------|------|
| SINGLETON | 11-293 | 282 | 主文件保留 |
| Responsibility 1: 物理卡列表同步 | 293-685 | 392 | 主文件(心脏,暂不动) |
| Universal card move animation | 685-1261 | 576 | partial → 后期组件(旧案 C) |
| Cascade Deck Layout helpers | 1261-2006 | 745 | **纯化进 DeckPositionCalculator 系静态类** |
| Responsibility 1 ext: reset/sync | 2006-2027 | 21 | 主文件 |
| Responsibility 2: CardScript→Physical 字典 | 2027-2084 | 57 | 主文件(05-17 已缓存化) |
| Responsibility 3: 按序目标位 | 2084-2912 | 828 | partial → 后期组件(旧案 B 的一半) |
| Deck Focus / Peel | 2912-3349 | 437 | partial → DeckFocusController(旧案 D) |
| Cleanup | 3349-3655 | 306 | partial |
| Initialization | 3655-3725 | 70 | 主文件 |
| Status Effect Projectile | 3725-4124 | 399 | **StatusEffectProjectileSystem 组件(旧案 E/P0)** |
| ICombatVisuals 实现 | 4124-4637 | 513 | 主文件(对外接口墙,签名永不动) |

### 4.3 步骤(每步独立 commit)

| # | 动作 | 风险 | 说明 |
|---|------|------|------|
| CUX-0 | **partial class 物理拆文件**:CombatUXManager.Layout.cs / .Peel.cs / .Projectile.cs / .Cleanup.cs / .Visuals.cs | 零 | 同类同名,只是分文件;场景/测试/调用点/序列化全不动。当日见效:主文件 4637 → ~1500,新功能从此有"该进哪个文件"的归置 |
| CUX-1 | Cascade Layout helpers 纯化 → DeckPositionCalculator 系静态 | 低 | 沿 05-17 阶段1路线继续;数学已在 docs/DeckLayouts.md 有 golden 测试(三个 Layout 测试类) |
| CUX-2 | StatusEffectProjectileSystem 组件 | 低 | 旧案 P0 原案执行;依赖最少;外部仅 statusEffectConsumePos 等 6 处引用 |
| CUX-3 | DeckFocusController 组件 | 中 | 旧案 D;先做 StartPeelCoroutine/TransitionFocusCoroutine 去重(AnimateCardToPeelPosition/AnimateCardToDeckPosition)再搬 |
| CUX-4 | (降级/暂缓)PhysicalCardManager + DeckSynchronizer | 高 | `physicalCardsInDeck` 被 RAP.ApplyAnimationResult 直接推进、被 CM 读;牵连三角,放最后,等 CM/RAP 稳定后再议 |
| CUX-5 | **功能落位指引(已落地 2026-09-16)** | 零 | 拍板修正:不做「CUX 单独增长禁令」,改为 AGENTS.md「Code Placement Guide」通用落位表(§7-#4);新功能按表落位,god 文件内只收 bug-fix 级改动;可选 CUX-6 行数 tripwire 测试见 §8 |

- 组件均挂同一 GameObject、Awake 自建,场景不重拖;`CombatUXManager.me` facade 转发,外部 ~20 处成员访问零改动。
- Headless 测试走 NullCombatVisualsBehaviour,不触 CUX 本体,天然免疫。

## 5. 全局执行守则

1. **顺序**:CM-1..4 → CUX-0..3 → RAP-1..2(三角关系:CM 和 CUX 可并行推进,RAP 垫后)。
2. **前置**:等当前未提交工作落库;开工前跑一次全量 EditMode 记基线(~545 绿)。
3. **每片纪律**:一个切片 = 一个 commit = 全量 EditMode 绿;动 scene/prefab 序列化的 commit 单独拆。
4. **三条红线**:不动 `CombatManager.Me` / `RecorderAnimationPlayer.me` / `CombatUXManager.me` 公共 API;不动 ICombatVisuals 签名;不动 GameEvent/Effect 逻辑层代码。

## 6. 明确不做清单

- 不做"整体组件化/子系统全家桶"(05-17 阶段2原案) —— 已被 4 个月停滞证伪。
- 不把 Zones(combinedDeckZone/revealZone)搬进新类 —— 它们是全项目的共享心脏,搬了等于改 API。
- 不给 RAP 做 MonoBehaviour 组件拆分 —— 播放时序是它的本体,拆了反而多一层跨组件协程。
- 不在本轮清理 `lastCardXxx` 8 个追踪字段 —— ValueTrackerManager 读它们 25 处,等 CM 切片完成后单独评估。

## 7. 拍板记录 (2026-09-16, 全部按低风险选项采纳)

| # | 议题 | 采纳 |
|---|------|------|
| 1 | 执行顺序 | CM 先行、CUX 紧随、RAP 垫后 (§5.1) |
| 2 | CUX-0 partial 拆文件 | **单独先做**,不等其他未提交工作落库(零风险,场景/测试/序列化全不动) |
| 3 | CM 切片形态 | ThroneZone / Fatigue = 同 GameObject 子组件;InputBlock = 纯 C# 类 |
| 4 | 增长控制 | 用户修正原案:不做 CUX 单独禁令行,改为 AGENTS.md 通用「Code Placement Guide」功能落位指引;业界依据见 §8;**已写入 AGENTS.md** |

## 8. 业界实践对照 (2026-09-16 检索)

| 业界实践 | 出处 | 本项目落法 |
|---|---|---|
| **Clean as You Code**:质量门只管新代码,存量豁免(accepted issues),随代码被触碰逐步还债 | SonarQube 官方文档 | 不追求清零 god 文件;规则=新增功能必须落 partial/组件/pipeline,god 文件只接受 bug-fix 级小改 |
| **FreezingArchRule**:冻结存量违规,只对新增违规报错 | ArchUnit User Guide | 同一思路在架构规则上的版本;本项目暂无等价工具,靠 AGENTS.md 落位表 + review |
| **Architecture fitness function**:架构约束写成可执行断言,漂移自动暴露(行数/依赖阈值) | Thoughtworks / InfoQ (Building Evolutionary Architecture) | 可选 **CUX-6**:EditMode 冒烟测试断言三个 god 文件行数上限(CM 1450 / RAP 1750 / CUX 主文件拆分后现值+50),超限即红。行数阈值只做 tripwire 不做质量目标,防止「为拆而拆」的 gaming。需「修改代码」授权 |
| **Strangler pattern**:绞杀者式渐进替换,拒绝大爆炸 | 业界通用(legacy 重构共识) | 05-17 阶段2 大爆炸方案停滞 4 个月即反例;本文档每片独立 commit 可合入即此路线 |
| **Package-by-feature + asmdef**:按能力域组织文件夹,程序集定义在编译期强制依赖边界 | Unity 官方组织指南 / 社区共识 | 长期方向(全部切片完成后另议);当前单 Assembly-CSharp 不动,避免 Editor 测试宿主(Assembly-CSharp-Editor)迁移风险 |
| **正向落位规则优于负向禁令** | SonarQube 规则粒度 / 主流风格指南惯例 | AGENTS.md 用「功能 → 去处」正向表,而非「禁止增长」负向单行 —— 负向规则会诱发 gaming 且不回答「该放哪」 |
