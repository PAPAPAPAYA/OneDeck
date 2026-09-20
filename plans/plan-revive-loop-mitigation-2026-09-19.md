# Plan: 无条件复活互拉循环 — 卡面层治理选项(2026-09-19)

日期:2026-09-19
状态:**已拍板(2026-09-20),待执行**。§1-§7 为分析期陈列(当时约束:不含任何代码 / prefab 改动);拍板结果与逐卡处置见 §8。用户方向(2026-09-19 对话):优先卡面层改动,降低无限复活泛滥;引擎层方案(每卡每轮限复活 / 侦测联动)已陈列存档但暂不取。
上游:`plans/plan-fatigue-revive-wildcard-2026-09-16.md`(疲劳弹药)、`plans/plan-infinity-detection-2026-09-17.md`(L0/L1 无限侦测)、`plans/plan-4.0-revive-awaken-2026-08-29.md`(复活引擎)。

## 1. 问题定义

两张「无条件复活友方」卡 A/B 同卡组时成环:

- 复活 = 墓地(`index < startCardIndex`)→ 卡组顶(`ReviveEffect.ReviveChosenCards`,`ReviveEffect.cs:260` 的 Remove+Add)。
- A 揭晓 → 复活 B 至顶 → A 落墓;B 在栈顶被揭 → 复活 A 至顶 → B 落墓;循环。
- Start Card 永远卡在牌堆中段,本轮不再前进(Type A 同轮无限,与 infinity-rules.md 的 StageMyCards 环同构,搬运方向换成复活)。
- 实机痛点:非 autoReveal 下玩家只能无限点击空转,永远无法抵达 Start Card 结束本轮。

规模证据(2026-09-19 全仓 grep 实测):

- 19 张 prefab 绑了 `ReviveMyCards` / `ReviveTheirCards` / `ReviveSelf`;整个 4.0 池只有 RIFT_REVIVER(另有非复活卡 RIFT_GUIDE_4.0)带 `CheckCost` 闸 → **18 张无条件**。
- 无条件 + Any 过滤的「复活 N 友方」枢纽卡 7 张:NECROMANCER / GRAVE_HEXER / KINGSLAYER / REVIVE_SUMMONER / SOUL_TRADER / CURSE_SUMMONER / RIFT_MEDIUM —— 全是现象卡(CardType.None)、全在 Common/Uncommon。
- NECROMANCER / RIFT_MEDIUM 另带「苏醒:复活」子句(子物体 `awaken revive 1`):被复活时立刻再拉一张,是环速放大器。

## 2. 现有防御层盘点(循环不会挂死,只会退化收场)

| 层 | 机制 | 缺口 |
|---|---|---|
| 疲劳弹药(09-16 拍板) | `Fatigue.prefab` 的 `wildcardTypeFilter=1`,仅复活闸口放行 → 循环反复拉起坍塌,每次揭晓双向 1 伤,HP 收敛 | 收敛过程漫长枯燥 |
| L0 `CombatBudgetGuard`(09-17) | 每轮 200 揭晓后当轮效果静默;全局 1500 揭晓 / 60 轮强结(高 HP 方胜,占位语义) | 退化期最长约 200 张/轮 |
| L1 `CombatArrangementCycleDetector`(09-18) | 同一排列一轮内第 3 次出现 = trip,写 `InfinityTripJournal` | **仅观察**,不干预(09-18 §3② 拍板;P2 归属 09-19 暂缓) |
| `EffectChainManager` 环守卫 | 同卡实例+同效果实例每代一次;`chainDepth`>99 截断 | 跨揭晓循环不适用(守卫在揭晓边界复位) |

## 3. 环图结构:枢纽-辐条

以 2026-09-19 prefab 实测为准(触发口取自子物体名 / cardDesc):

- **枢纽(无条件 + Any 过滤,互为目标池)**:NECROMANCER† / GRAVE_HEXER / KINGSLAYER / REVIVE_SUMMONER / SOUL_TRADER / CURSE_SUMMONER / RIFT_MEDIUM†(† = 带苏醒放大器)。
- **辐条(窄目标,能与枢纽成环但互拉脆)**:BEAST_REVIVER(实体,拉实体)/ ELITE_REVIVER(实体,拉被强化)/ SPIRIT_CALLER(实体,拉非实体 → 能拉现象枢纽)/ RIFT_SHEPHERD(实体,拉信徒)/ DEATHBED_PORTER(实体,拉现象)/ FLURRY_REVIVER(现象,MaxExtraAttackTimes 排序——池内全 0 时退化为随机 Any)/ MASS_REVIVER(现象,拉 Common)/ DUO_REVIVER(现象,拉 Uncommon → 能拉 Uncommon 枢纽)。
- **特殊触发口(已自带降频,非环源)**:FINAL_ESCORT(轮末武装-消耗)/ FUNERAL_WILL(亡语延迟复活)/ RELIC_CURSE_REVIVAL(敌方诅咒揭晓时)/ UNDYING_WARRIOR·REVIVING_STRIKER·CURSE_THIRST_BEAST(复活自身)。
- **复辟系(拉敌方)**:GRAVE_ROBBER / DOOM_HERALD / CURSE_GARDENER / CURSE_REVIVER —— 目标在敌方墓地,与友方环正交,本期不管。

推论:**砍枢纽 ≈ 塌掉环图**;辐条互拉(如 BEAST↔ELITE 需「被强化」叠加)是可接受的残余。

## 4. 卡面层方案(可单选 / 可叠加)

### 4.1 触发降频(主荐之一)

枢纽卡的「揭晓:复活」改绑稀缺触发口,现成模式全在池内:

- 亡语(FUNERAL_WILL 先例);
- 轮末武装-消耗(FINAL_ESCORT 的 `ArmRoundEndRevive` 先例);
- 轮首自动机(Linger + `beforeRoundStart`,`LingerAutomatonTests` 已验证,军号/暮鼓同款)。

效果:每卡每轮至多复活 1 次,环降为固定节拍,Start Card 必然到达。
代价:卡变慢一档,需数值/稀有度补偿;7 张 listener 重绑 + desc 改词。
残余:亡语版可被第三方埋葬效果反复喂,但埋葬本身是稀缺资源。
注意:**改「延迟复活」不算降频**——落位 startCardIndex+1 只是把环挪到 Start Card 正上方继续转,本轮仍不结束。

### 4.2 目标分层

枢纽 `creatureFilter` Any → Creature(仅实体)。枢纽全是现象卡 → 现象拉不到现象,枢纽互拉结构性消失;疲劳弹药不受影响(`wildcardTypeFilter` 绕开的正是类型行)。
代价:复活失去拉现象卡的泛用性;SPIRIT_CALLER(拉非实体)与 DEATHBED_PORTER(拉现象)会重新打开缺口,需同步收窄或改型。BEAST↔ELITE 等实体辐条环仍在(条件脆,可接受)。

### 4.3 弹药化

「复活 N 友方,去除自身」(信徒 RIFT 的 ExileSelf 先例,纯 prefab 重绑,零引擎)。环每圈销毁一件引擎,复活变有限资源管理。
代价:卡定位剧变(持续引擎 → 一次性弹药),价格/稀有度重做;观感从「无限可能」变「省着用」。

### 4.4 卡面限次(唯一要碰引擎的方案)

卡面文本「本场战斗限 K 次」;`CostNEffectContainer` 加每卡每场调用计数(约 20 行 + EditMode 测试)。
效果:两张枢纽 = 2K 次硬封顶;保留「揭晓即复活」手感;规则上卡、无隐藏机制。
注意:`CheckCost_Counter` 只检不耗(`CostNEffectContainer.cs:250-256`,SLIME 同款隐患)→ 「Counter 当弹药」必须先补消耗语义,否则等于无条件,故不荐 Counter 变体。

### 4.5 供给端收紧(打底)

枢纽升 Rare + 降 `shopRollWeightMultiplier` → 「同卡组两张」概率大降。不消灭环、只降发生率,不单用。
加强版「已有复活卡则不再刷复活卡」要在 ShopManager 购买路径加跨卡检查(引擎活,超本期)。

### 4.6 独立设计规矩(无论选哪组都建议立)

复活卡自身不再携带「苏醒:复活」子句 —— 砍 NECROMANCER / RIFT_MEDIUM 放大器;苏醒复活只给非复活卡。

## 5. 推荐组合

4.5 打底(降发生率)+ 4.1 与 4.4 瓜分 7 张枢纽(例:3 张改轮首/亡语、2 张限 K=1、2 张保留经典揭晓复活维持原型手感)+ 4.6 禁令。

## 6. 待拍板(2026-09-20 已拍板,结果见 §8)

1. 采用哪个(些)方案?是否按 §5 组合推进? → 已决:以「每回合一次」限次为主(§4.4 的每轮刷新版),不按 §5 瓜分;§4.5 留作后续平衡工具;§4.6 暂不执行。
2. 若推进:下一步出 19 张复活卡逐卡处置表(触发口/过滤/稀有度逐卡列明),确认后再动 prefab。 → 已出,见 §8.3(实测为 27 张)。
3. 引擎层备胎(本篇不取,存档备查):每卡每轮限复活 1 次(`revivedThisRound` 旗标);侦测器 trip 即当轮静默效果(需推翻 09-18「仅观察」拍板)。

## 7. 明确不做

- 不改任何 prefab / .cs;不动 ReviveEffect 选区语义;不动 L0/L1 参数;不动复辟系;Notion 4.0 DB 不同步(拍板后再走 unity-notion-card-sync)。

## 8. 拍板结果与逐卡处置表(2026-09-20)

§7 的「不改」为分析期约束,自本节起解除,执行以 §8 为准。

### 8.1 拍板

1. **主方案 = 思路 1「每回合一次」**(§4.4 的每轮刷新版):枢纽揭晓复活 = 每卡实例每轮限 1 次,保留「揭晓即复活」手感与持续引擎定位。推演(最小 2 枢纽卡组):A 空转 → B 拉 A → A 拉 B → B 被闸空转 → Start Card 到达;N 张枢纽 = 每轮至多 N 次,线性有界。
2. **否决思路 2(复活定向 = 拉墓地最靠近 Start Card)**:墓地布局为「越早结算越靠近 Start Card」(结算 `Insert(0)`,`CombatManager.cs:1172`);两枢纽先于其他卡揭晓时墓地里只有枢纽、必然互拉,死锁依旧 —— 只是把「必然成环」改成「取决于洗牌排列」的概率缓解,弱于 §4.5 且要动 `ReviveEffect` 选区语义(原 §7 不动项),投入产出不成立。
3. **§4.6 禁令暂不执行**:NECROMANCER / RIFT_MEDIUM 保留「苏醒:复活」。用户理由(2026-09-20):正常卡组中随机复活难以精确命中放大器,放大器拉放大器的级联概率更低,以稀有度压制出现率即可;级联对疲劳收敛的稀释另案处理。
4. **放大器处置不对称**:NECROMANCER 有揭晓复活口 = 枢纽,加闸 + 升 Rare;RIFT_MEDIUM 实测无揭晓复活口(仅 `add 2 rifts` + `awaken revive 1`),非环源,维持 Uncommon、不加闸、不动。
5. §4.5(供给端)不单用,留作后续平衡工具。
6. **卡面规范**:多效果卡加闸时,受限复活子句一律放最后,格式「每回合一次,复活 N …」(KINGSLAYER 式)。

### 8.2 前置引擎改动(所有加闸卡共用)

`ReviveEffect` 加序列化 `oncePerRound`(bool)+ 每轮计数:

- 粒度 = 卡实例 × ReviveEffect 组件(NECROMANCER 的揭晓闸与苏醒子句互相独立;同名双卡各占 1 次/轮)。
- 计数时机 = `ReviveChosenCards` 实际成功(`revivedCards.Count > 0`)才 +1;墓地空弹不消耗。
- 重置 = 每轮边界(`HandleNewRoundStart` / 轮号惰性戳)。
- 不复用 `CheckCost_Counter`(只检不耗,§4.4 已标记)。
- 预估 15-20 行 + EditMode 测试(两枢纽互拉各 1 次后 Start Card 必达;空墓不消耗;同名双实例各自计次)。

### 8.3 逐卡处置表(27 张,2026-09-20 prefab 实测)

数据修正:§1「19 张」偏少,实测 4.0 共 27 张绑复活效果(漏算 ReviveSelf 系与 GRAVE_ROBBER 的间接绑定 —— 它由 `GraveRobberEffect` 内部调 `reviveEngine`,不走 UnityEvent,纯 grep `m_MethodName` 会漏)。§3 修正:FLURRY_REVIVER 的 `creatureFilter=Creature`,池内全 0 时退化为随机**实体**,非 Any。

**组一:加闸(8 个 ReviveEffect 组件,`oncePerRound=true`)**

| 卡 | 稀有度 | 类型 | 加闸位置 | 新 cardDesc |
|---|---|---|---|---|
| GRAVE_HEXER | Common(不动) | 现象 | 揭晓复活(Any) | `强化 <b>1</b> 敌方诅咒;每回合一次,复活 <b>1</b> 友方` |
| KINGSLAYER | Uncommon(不动) | 现象 | 揭晓复活(Any) | `埋葬 <b>1</b> 攻击力最高敌方;每回合一次,复活 <b>1</b> 友方` |
| REVIVE_SUMMONER | Uncommon(不动) | 现象 | 揭晓复活(Any) | `生成 <b>1</b> 信徒;每回合一次,复活 <b>1</b> 友方` |
| SOUL_TRADER | Uncommon(不动) | 现象 | 揭晓复活(Any,拉 2;闸按触发计) | `埋葬 <b>1</b> 友方;每回合一次,复活 <b>2</b> 友方` |
| CURSE_SUMMONER | Uncommon(不动) | 现象 | 只闸友方半;敌方半不动 | `复活 <b>1</b> 敌方诅咒;每回合一次,复活 <b>1</b> 友方` |
| NECROMANCER | **Uncommon→Rare** | 现象 | 只闸揭晓半;苏醒保留;prefab 移 `2_Rare/` | `苏醒:复活 <b>1</b> 友方;每回合一次,复活 <b>1</b> 友方` |
| BEAST_REVIVER | Common(不动) | 实体 | 揭晓复活(Creature);堵 BEAST×2 同名环 | `攻击;每回合一次,复活 <b>1</b> 友方实体` |
| RIFT_SHEPHERD | Common(不动) | 实体 | 揭晓复活(tag=Believer);堵 SHEPHERD×2 同名环(自身带 Believer) | `攻击;每回合一次,复活 <b>1</b> 张tag为[<tag:Believer>]的友方卡` |

**组二:不动 — 辐条(7)**:ELITE_REVIVER(拉被强化实体,条件脆)/ SPIRIT_CALLER(拉现象,拉到枢纽也死于对方闸门)/ DEATHBED_PORTER(亡语拉现象,亡语稀缺)/ FLURRY_REVIVER(拉 MaxExtraAttackTimes 实体)/ MASS_REVIVER(Rare 拉 Common×3,拉中 GRAVE_HEXER 死于对方闸门)/ DUO_REVIVER(Rare 拉 Uncommon×2,同理)/ RIFT_REVIVER(自带 `CheckCost_HasOwnCardOfType` + 放逐弹药,天然限次)。

**组三:不动 — 特殊触发口(7)**:FINAL_ESCORT(亡语武装→`BeforeStartCardReveal` 消耗,天然每轮 1 次)/ FUNERAL_WILL(亡语延迟复活)/ RELIC_CURSE_REVIVAL(`isPassive`,自身不可被复活)/ SOLDIER_SKELETON_4.0 / UNDYING_WARRIOR / CURSE_THIRST_BEAST_4.0 / REVIVING_STRIKER(ReviveSelf 自复活,不进互拉池)。

**组四:不动 — 复辟系(4 + 半)**:CURSE_REVIVER / CURSE_GARDENER / DOOM_HERALD / GRAVE_ROBBER + CURSE_SUMMONER 敌方半。与友方环正交。

**组五:不动 — 放大器保留**:RIFT_MEDIUM(维持 Uncommon,无揭晓口,无闸)。

### 8.4 残余环清单(加闸后)

- 枢纽×枢纽、枢纽×辐条、MASS/DUO→枢纽:全部死于闸门。
- 同名双卡:BEAST×2 / RIFT_SHEPHERD×2 已被组一堵死;ELITE×2 需外部强化(脆,接受);MASS×2 / DUO×2 / FLURRY×2 / SPIRIT×2 / PORTER×2 结构性不可能(自身类型/稀有度不匹配自身过滤)。
- 亡语复活被第三方埋葬反复喂:埋葬稀缺,接受。
- 苏醒级联(NECROMANCER 拉 RIFT_MEDIUM 等):保留,以 Rare 压制出现率;实测仍泛滥则回头执行 §4.6。

### 8.5 附带发现(另案处理)

1. SPIRIT_CALLER 的 `attack` 子物体是死绑定(container 无 listener 调用,攻击从未触发);且是 `printedAttack=0` 的实体。
2. GRAVE_ROBBER 的 cardDesc 折行注入多余空格(「…该卡攻击力; 攻击」)。

### 8.6 执行顺序

1. 引擎闸 + EditMode 测试。
2. 组一 8 处 prefab 加闸 + NECROMANCER 升 Rare(移文件夹 GUID 安全:运行时无按路径枚举,路径字面量仅在 editor/test/tools;rarity 档权重自动生效)。
3. 回归:InfinityPipelineTests / InfiniteDeckTerminationTests / CurseSummonerPrefabSmokeTests + 全量 EditMode。
4. Notion 4.0 DB 走 unity-notion-card-sync(稀有度 + 卡面措辞)。
5. 疲劳稀释问题另案。

## 9. 执行记录(2026-09-20)

- **引擎闸**:commit `3919a102` — `ReviveEffect.oncePerRound`(组件实例粒度,成功才计次,`roundNumRef` 惰性戳重置)+ `ReviveOncePerRoundGateTests`(7 例,含双枢纽互拉 Start Card 第 5 揭晓必达 / 无闸对照组永不到达)。
- **组一 prefab**:commit `027fdd0a` — 8 组件 `oncePerRound: 1`(脚本 `tools/scripts/apply_revive_gate_prefabs.py`,fileID+脚本 GUID 双重定位);desc 按 §8.3;NECROMANCER 移 `2_Rare/` + `rarity: 2`(GUID `b78f1e5b` 不变,git rename 检出)。`ReviveOncePerRoundPrefabTests`(8 例)锁定闸位/未闸位/desc/稀有度。
- **回归**:全量 EditMode 639 跑 637 绿。2 红 = `ArrangementCycleDetectorTests.NonLethalInfiniteDeck_TripsCycleDetector` + `RunBudgetSimTests.NonLethalSample_ReportsInfinite` —— **预期内**:标本卡组 "non-lethal infinite test" = GRAVE_HEXER(已闸)× SPIRIT_CALLER,即 §8.4 所言「枢纽×辐条死于闸门」;战斗 5 轮 52 揭晓正常收场(敌方死于疲劳),侦测器无从 trip。用户拍板(2026-09-20):**暂留红灯**,标本重造另案;FLURRY×SPIRIT 替代对亦不成环(SPIRIT_CALLER 经并行会话修改后无法攻击)。
- **Notion 同步**:7 行 desc 注入「每回合一次,」+ NECROMANCER `rare`;GRAVE_HEXER 行用户已手改(语义已到,逗号差异按归一规则跳过)。
- **另案**:侦测器标本重造(需现存仍成环的卡对);§4.6 苏醒禁令(保留观察);疲劳稀释;§8.5 附带发现(SPIRIT_CALLER 已由并行会话处置,GRAVE_ROBBER desc 同)。
