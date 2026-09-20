# Plan: 无条件复活互拉循环 — 卡面层治理选项(2026-09-19)

日期:2026-09-19
状态:**待拍板**。本篇只做分析与方案陈列,不含任何代码 / prefab 改动。用户方向(2026-09-19 对话):优先卡面层改动,降低无限复活泛滥;引擎层方案(每卡每轮限复活 / 侦测联动)已陈列存档但暂不取。
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

## 6. 待拍板

1. 采用哪个(些)方案?是否按 §5 组合推进?
2. 若推进:下一步出 19 张复活卡逐卡处置表(触发口/过滤/稀有度逐卡列明),确认后再动 prefab。
3. 引擎层备胎(本篇不取,存档备查):每卡每轮限复活 1 次(`revivedThisRound` 旗标);侦测器 trip 即当轮静默效果(需推翻 09-18「仅观察」拍板)。

## 7. 明确不做

- 不改任何 prefab / .cs;不动 ReviveEffect 选区语义;不动 L0/L1 参数;不动复辟系;Notion 4.0 DB 不同步(拍板后再走 unity-notion-card-sync)。
