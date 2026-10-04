# 三大管理类拆分方案:CombatManager / RecorderAnimationPlayer / CombatUXManager

日期: 2026-09-16
状态: **拍板完成,未实施** (2026-09-16 四项全部按低风险选项拍板,见 §7;AGENTS.md Code Placement Guide 已落地;其余切片未动工。2026-10-03/04 复核:三 god 文件切片仍全部未动工,体量/行号数据已刷新,复核记录见 §4.0;下一代拆分候选见 §9)
目标: 评估三个最庞大管理类是否该拆、怎么拆,给出可分步执行、每步可验证的低风险路线
关联: plans/refactor-combat-ux-manager-2026-05-17.md (阶段2未执行的旧案,本文档 §4 复活并修正), docs/RegressionChecklist.md, docs/AnimationSystem.md, docs/DeckLayouts.md

## 0. 一页结论

| 类 | 行数 | 裁决 | 一句话理由 |
|----|------|------|-----------|
| CombatManager | 1425 | **拆,低风险切片** | 真问题是 RevealCards 单方法 ~220 行;类本身未失控,facade 不动、职责外移即可 |
| RecorderAnimationPlayer | 1671 | **缓拆,只摘纯计算** | 单一职责的播放引擎,复杂度是内在的;真问题是 PlayRequestCoroutine 单方法 ~730 行;但它是全项目 bug 密集区,动它收益低风险高 |
| CombatUXManager | 4670 | **必须拆,最高优先级** | 4 个月涨 2669 行(+136%),05-17 旧案阶段2从未执行;但旧案"五组件一次拆"被证明推不动,改为 partial-class 过渡 + 逐个组件摘除 |

全局前置条件:等当前工作区未提交工作落库后再动任何一片;每片一个 commit,全量 EditMode 绿(开工时实测基线,2026-09-16 时 ~545)才进下一片。(2026-10-03:工作区干净,原列出的未提交工作——RNG 验证 / 触发顺序 / 商店尾区 / PhysButton——均已落库,前置条件已满足。CM 后续 5 次提交 2026-09-16..19 均为功能/修复,非切片)

## 1. 体量事实 (2026-09-16 实测,2026-10-03 刷新)

### 1.1 三类对比

| 指标 | CombatManager | RecorderAnimationPlayer | CombatUXManager |
|------|--------------|------------------------|-----------------|
| 行数 | 1425 | 1671 | **4670** |
| 方法数(约) | 60 | 30 | 106 |
| 最大方法 | RevealCards ~220 行 (801-1018) | **PlayRequestCoroutine ~730 行 (941-1671)** | SyncPhysicalCards ~373 行 (299-672) |
| 200+ 行方法数 | 1 | 1 | 5 (373/287/269/243/220) |
| 外部引用文件数 | 15+ (CM.Me);Editor 测试 32 文件 | ~30 文件 | 20 文件 (2026-10-03 实测) |
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

### 4.0 复核记录 (2026-10-03)

- **切片未动工确认**:CUX-0..4 / CUX-6 全部未执行,`CombatUXManager.cs` 仍是单文件(无 partial 拆分文件)。三类中唯一动过 CUX 本体的是视觉修复 d7086d70;CM 的 5 次后续提交(RNG 迁移 / 触发顺序 / L0-L1 防护)与 RAP 零提交均非切片;`InputBlockCounter`/`ThroneZone`/`FatigueHandler`/`RecorderPlaybackPlanner`/`DeckFocusController`/`StatusEffectProjectileSystem` 组件类均不存在(`ThroneZoneTests` 只是测 CombatManager 静态 throne 区方法)。
- **增长已放缓,§7-#4 落位指引实效得到验证**:2026-09-16 → 2026-10-03(17 天)CUX 仅 +33 行(4637 → 4670,≈60 行/月,此前 ~670 行/月)。全部涨幅来自一个视觉修复 d7086d70(2026-09-18,popup peak live-follows deck slot,+49/-16),hunks 全部落在 ICombatVisuals 区(4124-4670)。同期商店 chrome v5 + 阶段转场工作全部落进约 19 个新独立文件(ShopChrome*/ShopHud*/Hud*/PhaseFlightPlanner/PhaseHudFlight/ShopSectionPanels 等)——新功能有处可归,CUX 本体不再被动吸行。
- **优先级判断维持**:CUX 不再是失控增长曲线,但 4670 行仍是 CombatManager 的 3.3 倍、region 结构原封未动;CUX-0(零风险、当日见效)仍是第一个该执行的切片。
- **数据刷新范围**:§0/§1.1 行数与最大方法行距、§4.2 region 行号均已刷至当日;region 划分与 2026-09-16 完全一致(无新增/合并/改名)。
- **2026-10-04 增核**:当日四笔 v1.1 代码提交全部落独立文件(等高线 shader、OptionsButton 换角滑行 → HudViewportPin.cs 净 +89、假想飞行 squash flip → PhaseTransitionDriver/PhaseFlightPlanner);三大 god 文件与 §9 候选行数逐行未变,本节数字仍然有效。

### 4.1 为什么旧案要修正

- 05-17 案阶段1(CardMoveConfig / DeckPositionCalculator 抽取 + 字典缓存)已落地;**阶段2"五组件一次拆"至今未执行**。
- 期间 CUX 从 1968 → 4637 行(4 个月 +2669 行,约 670 行/月)。结论:**大爆炸式组件拆分在这个项目推不动**(等一个"完整窗口"永远等不到),必须改成"每片独立可合入"的流水线,并加增长控制。

### 4.2 Region 盘点 (行号刷至 2026-10-03,漂移 ±几行不影响结论)

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
| ICombatVisuals 实现 | 4124-4670 | 546 (+33,d7086d70) | 主文件(对外接口墙,签名永不动) |

### 4.3 步骤(每步独立 commit)

| # | 动作 | 风险 | 说明 |
|---|------|------|------|
| CUX-0 | **partial class 物理拆文件**:CombatUXManager.Layout.cs / .Peel.cs / .Projectile.cs / .Cleanup.cs / .Visuals.cs | 零 | 同类同名,只是分文件;场景/测试/调用点/序列化全不动。当日见效:主文件 4670 → ~1550,新功能从此有"该进哪个文件"的归置 |
| CUX-1 | Cascade Layout helpers 纯化 → DeckPositionCalculator 系静态 | 低 | 沿 05-17 阶段1路线继续;数学已在 docs/DeckLayouts.md 有 golden 测试(三个 Layout 测试类) |
| CUX-2 | StatusEffectProjectileSystem 组件 | 低 | 旧案 P0 原案执行;依赖最少;外部仅 statusEffectConsumePos 等 6 处引用 |
| CUX-3 | DeckFocusController 组件 | 中 | 旧案 D;先做 StartPeelCoroutine/TransitionFocusCoroutine 去重(AnimateCardToPeelPosition/AnimateCardToDeckPosition)再搬 |
| CUX-4 | (降级/暂缓)PhysicalCardManager + DeckSynchronizer | 高 | `physicalCardsInDeck` 被 RAP.ApplyAnimationResult 直接推进、被 CM 读;牵连三角,放最后,等 CM/RAP 稳定后再议 |
| CUX-5 | **功能落位指引(已落地 2026-09-16)** | 零 | 拍板修正:不做「CUX 单独增长禁令」,改为 AGENTS.md「Code Placement Guide」通用落位表(§7-#4);新功能按表落位,god 文件内只收 bug-fix 级改动;可选 CUX-6 行数 tripwire 测试见 §8 |

执行状态 (2026-10-03):CUX-0..4 / CUX-6 均未动工;CUX-5 已落地,其增长放缓实效见 §4.0。

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

## 9. 下一代拆分候选 (2026-10-04 增补)

三大 god 文件之外的千行级/准千行级代码文件盘点(行数 2026-10-04 实测;增量为 09-16 → 10-03,10-04 增核逐行未变):

| 文件 | 行数 | 增量 | 判读 |
|---|---|---|---|
| CardPhysObjScript | 2137 | +157 | 第二大代码文件;物理卡实体,四 region(翻转 / Float Stack 大影子 / 特写动画 / 悬停 tooltip),~68 方法;稳步增长,暂无失控迹象 |
| ShopUXManager | 1476 | **+376** | **增长最危险**:17 天 ≈660 行/月,正是 CUX 失控期速率;hover lift / enlarge preview / reroll flip / price button 等一串商店交互全落本体(+522/-146) |
| HPNumericDisplayHorizontal | 1193 | +289 | HP 横条组件被商店顶栏 v5 镜像 + 阶段转场飞行连续加码(10-03/04 仍有提交) |
| HPNumericDisplay | 1042 | 0 | 同族竖条版,完全稳定 |
| CardScript | 965 | +11 | 卡片核心组件,缓慢爬升,暂不紧迫 |

边角:Assets/DevLog.cs(721 行)不是代码——伪装成 .cs 的纯注释开发日志,仅占行数;Editor 测试大文件(Step5BatchBEngineTests 702 / ShopBoardPipelineTests 650 等)属测试宿主,不处理。

### 9.1 ShopUXManager 落位盲区(制度性根因)

- AGENTS.md「Code Placement Guide」shop 行写的是「shop state/UI → ShopManager / ShopUXManager」,只指路、没有 partial/组件出口——商店新功能「按表落位」就合法落进 ShopUXManager 本体。落位指引拦住了 CUX,却把 ShopUXManager 当成合法泄洪口。
- 修法(提议,待拍板后改 AGENTS.md):shop 行改为「shop 生成 → ShopBoardPipeline;shop 状态 → ShopManager;shop 交互/呈现 → ShopUXManager partial 或独立组件(PhysButton 绑定类 / ShopSectionPanels 类新文件),不再进 ShopUXManager.cs 本体」——shop chrome v5(ShopChrome/Hud* 家族)已证明 shop 呈现层可以独立成文件。

### 9.2 处置建议

- 不开「第四方案」:先按 §4.0 结论执行 CUX-0,验证 partial 流水线跑通后,把同一套手法复制给 CardPhysObjScript(候选 #1)与 ShopUXManager(候选 #2,先落 §9.1 的盲区修正)。
- HPNumericDisplayHorizontal 的增长源(商店镜像 + 转场飞行)属阶段转场 v1.1 收尾工作,收口后再评估;若继续涨,摘「转场飞行」为独立组件。
- CUX-6 行数 tripwire(§8)若落地,可把 CardPhysObjScript / ShopUXManager 加进同一断言统一守护。
