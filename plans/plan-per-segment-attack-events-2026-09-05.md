# 每段攻击独立事件（per-segment attack events）（2026-09-05）

## 背景

- 攻击可有多段：prefab `extraAttackTimes`（永久）、`attackTimesModThisRound` + 生物攻击次数光环（本回合，`AttackTimesGiverEffect`）、动态段数（BONE_COMBINATION 按埋葬数、BODY_CANON 按 IntSO）。
- 攻击动作事件 `onAnyCardAttacked` / `onAnyFriendlyCardAttacked` 目前每次攻击动作只触发一次（`AttackEffect.RaiseAttackEvents` 在段循环之外），多段攻击下 RELIC_HIVE（友方攻击时生成 1 信徒）、RELIC_ATTACK_HEX（有卡攻击时强化 1 敌方诅咒）、RELIC_ATTACK_BURIAL（友方每次攻击时埋葬卡组顶 1 卡）只结算一份。
- 拍板：**每段攻击 = 独立事件 + 独立链作用域**，攻击事件反应每段触发一次。

## 根因：为什么只把事件移进循环不够

1. **配对守卫** `EffectCanBeInvoked`（EffectChainManager.cs）：同一开放链内，同（卡实例，效果组件）对已处理即拦截。整次攻击动作（含全部反应）活在同一条开放链里，直到动画阶段 `PlayRecorderAnimationsAndWait` 才 `CloseOpenedChain`（CombatManager.cs:616），反应卡第 2 段起全部被拦。
2. **lastEffectObject 自调守卫**（CostNEffectContainer.cs:104）：段间连续两次调同一 reactor 的 container 会被 "prevent effect invoking self" 拦截。
- 同类隐性限制：每段伤害事件（`onMyPlayerTookDmg` 等）本就每段触发，但其 reactor 同样被配对守卫限一次（如 ETERNAL_GHOST 对多段攻击只反应一次）——本方案一并解决。

## 方案：段级 sub-recorder + 段级作用域关闭

每段执行：

1. 压入**段级 recorder**（挂在攻击者当前 recorder 之下，`MakeANewEffectRecorder` 既有父子逻辑）。
2. 结算该段（`DecreaseTheirHp` 或血誓转化）——段级 recorder 持有本段 `Attack` 动画请求，反应嵌套挂段级 recorder 之下。
3. 触发段级攻击事件（`RaiseAttackEvents` 移入每段）。
4. `PopCurrentRecorder` → **段级 scope close**：把本段产生的、不在进行中调用栈（`recorderStack`）上的 opened recorder 移入 `closedEffectRecorders`，**保持 transform 父子关系**。
5. 段间把 `lastEffectObject` 恢复为攻击者自己的 container（防"攻击卡监听自身攻击事件经同一 container 自循环"跨段绕过守卫）。

- **动画表现**：根 recorder 收集按 `transform.parent == EffectChainManager.transform` 判定（CombatManager.cs:630），段级 recorder 是攻击者 recorder 的子节点、走树递归播放 → 播放顺序从"N 下全打完再反应"变为"打 1 下 → 反应 → 再打 1 下 → 反应"。
- **times capture-once**：段数在动作开始取值一次（`GetAttackTimes()` / 显式参数 / 动态 tracker 值），段中不重读——防止"反应给攻击次数 +1"式反应造成段数自增死循环。

## 必须一起修

- **chainDepth 死代码**：`MakeANewEffectRecorder` 每次都 `chainDepth = 0`（EffectChainManager.cs:83），而每次 container 调用都先建 recorder，`>99` 深度上限永不生效。改为只在 `CloseOpenedChain` 归零、每次放行调用 +1——段级独立化后配对守卫保护被削弱，这条保险丝必须真正可用（防 A 攻击→B 反应攻击→A… 跨段递归）。
- **血誓转化段仍触发段级攻击事件**（与 2026-08-31 裁定"攻击动作已发生，仅结算改变"一致；RELIC_HIVE 对转化段也生成信徒）。
- **自伤 `AttackSelfTimes`**：每段触发 `onAnyCardAttacked`，永不触发 `onAnyFriendlyCardAttacked`（规则不变，粒度变段）。

## 实施步骤

1. `EffectChainManager`：删 `MakeANewEffectRecorder` 的 `chainDepth = 0`；新增 `BeginAttackSegmentScope(attackerCard, attackEffectObj)` / `EndAttackSegmentScope()`。
2. `AttackEffect`：`AttackTimes` / `AttackSelfTimes` 段级化，`RaiseAttackEvents` 移入每段；更新方法注释（"once per action" → "once per segment"）。
3. `GameEventStorage` 事件注释 + `docs/4.0_Glossary.md` 攻击时条目补"每段"语义。
4. `AttackEffectTests`：once-per-action 断言改为 per-segment；动画捕获断言改读段级 recorder；新增"多段攻击下同一反应卡每段各触发一次"用例。
5. EditMode 测试 + Unity 编译验证（refresh_unity + console）。

## 已知边界

- 同一卡有多个 container 且都在一次攻击动作内反应（如伤害事件反应 C1 + 攻击事件反应 C2）时，`CheckShouldIStartANewChain` 会在段中 `CloseOpenedChain`（既有行为）：已捕获的段级请求保留在 closedEffectRecorders，下一段重新压段级 recorder 恢复捕获，但该情况下播放顺序退化为"先已关闭段、后后续段"。
- 段级 recorder 无 `processedEffectID`，不参与配对守卫匹配（守卫要求非空）。
- 血誓转化段不捕获 `Attack` 动画请求（无伤害结算），仅有逻辑层事件。

## 执行记录（2026-09-05）

- 已实施 Step 1-4：`EffectChainManager`（chainDepth 保险丝修复 + `BeginAttackSegmentScope`/`EndAttackSegmentScope`，owner 走 LIFO 栈支持嵌套攻击）、`AttackEffect`（AttackTimes/AttackSelfTimes 段级化）、`GameEventStorage` 注释、`docs/4.0_Glossary.md` 攻击时条目、`docs/RegressionChecklist.md` 第 81 行。
- 测试：`AttackEffectTests` 18/18 通过（once-per-action 断言已反转；新增 `Attack_FiresSameReactorOncePerSegment` 走真实 GameEventListener→CostNEffectContainer 链验证配对守卫豁免）。全量 EditMode 467 用例中 465 通过；仅 `RunRecorderTests` 2 例失败（服务器战报 outbox 恢复，与本改动零交集，属预先存在）。
- 教训：测试里 `AddComponent<CostNEffectContainer>()` 后 UnityEvent 字段为 null（仅 Inspector 序列化时初始化），必须用夹具 `CreateCostContainer`（反射注入 + 显式 new UnityEvent）。
- 待办（人工）：Play Mode 验证 RELIC_HIVE 多段生成信徒与动画交错表现；RELIC 组合平衡复核（unity-card-infinity-check / pool audit）。
