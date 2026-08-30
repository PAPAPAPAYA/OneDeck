# 4.0 被动卡引擎实施计划（isPassive）

日期：2026-08-29
上游：`plans/plan-4.0-implementation-roadmap-2026-08-28.md` 第 3 步；`docs/4.0_CardDesc_Spec.md` §Passive Cards（ratified 2026-08-26；per-event firing ratified 2026-08-28）
状态：**已实施（2026-08-29，用户明示「修改代码」后执行）**。开放问题裁决：#1 自定义顺序不含被动、强制垫底 ✓；#2 被动物理表现交由现有布局，实施后视觉验证待做；#3 TAINT 相邻语义留待第 5 步前拍板。

## 1. 设计基线（spec 摘录）

- 无独立区域：被动 = 普通卡 + `isPassive` 标记。
- 放置：每次洗牌（含战斗开始）置于 start card 之后（index < startCardIndex，墓地侧）；永不揭晓、不占揭晓位、不吃疲劳。
- 免疫一切移动效果（埋葬/置顶/延后/放逐/复活等）——不进移动效果选取池。
- 常驻监听，条件满足即触发；**每次匹配事件都触发**（无每轮上限，非"自己揭晓时一次"）。
- 仍是卡：可被强化、计入墓地计数、可被其他卡谓词读取；**只有移动效果排除它**，数值效果不排除。
- 类比：roguelike 遗物，但以卡的形式参与卡交互。

## 2. 代码事实（2026-08-29 实测）

- `CardScript.isPassive` 已随第 2 步预留（默认 false、零行为）；复活选取已排除它（`ReviveEffect.BuildRevivePool`）。
- 洗牌合成点：`StartCardShuffleEffect.ExecuteShuffleEffect`（:45-90）——分离 start card → 洗其余 → AlwaysBottom 分支 start card `Insert(0)`；另有 `ShuffleOrderOverride` 自定义顺序分支。战斗开始与每轮边界走同一入口。
- 墓地侧不变量：index < startCardIndex；揭晓从牌堆顶弹出、触发后放回 index 0、回合结束 = 揭晓指针到达 start card。
- **放置推导**：被动必须垫在 start card **之下**（index 更小），而非其上——若放在 start card 之上（index 1..k），回合内它们会先于 start card 被指针扫到并被揭晓。垫底后 [被动.., start, 活区..]：揭晓队列只含活区，指针到达 start card 即回合结束，被动永不被弹出；回合内揭晓卡插 index 0 会把被动压得更深，index 恒 < startCardIndex ✓（规格括号里的 index < startCardIndex 由此成立）。
- 移动效果选取池（需补 isPassive 的点，逐处实测）：
  - `StageEffect` ×9 处过滤（StageMyCards / StageCardsWithTag / StageMyTokens / StageMyCardsWithTag / StageTheirCardsWithTag / StageAllFriendlyMinion / StageMyCards_BasedOnIntSO / StageTheirSpecificCard / StageCardWithMaxAttack / StageCardWithMostStatusEffect）
  - `CardManipulationEffect.GetCardsByOwner`（DelayMyCards/DelayTheirCards 共用）
  - `ExileEffect` ×6（ExileMyCards / ExileTheirCards / ExileRandomCards / ExileMyCardsWithTag / ExileTheirCardsWithTag / ExileCardsWithTag）
  - `BuryEffect` 池已含 `IsCardBelowStartCard` 排除——被动恒在墓地侧，**天然免疫**；`BuryNextXCards` 只作用活区顶部；唯一绕过路径是 `ignoreStartCardBoundary` 测试旗标 → 仍补一条 isPassive 显式检查（纵深防御）
- `CanBeAffectedByEffects`（CardScript.cs:40）当前**无调用方**——不动它。
- Linger 先例：卡片监听器在 OnEnable 注册、常驻整个战斗、无每轮限额 → 被动"常驻 + 每次匹配事件触发"与现有事件模型**天然一致，零引擎改动**。
- 墓地计数：`ValueTrackerManager.ownerInGraveAmountRef` 统计 index < startCardIndex 的卡 → 被动自动计入 ✓（符合规格"计入墓地计数"）。
- 被动卡清单（迭代文档，均未配置 prefab）：15 RELIC_HIVE、19 RELIC_DEATH_KNELL、38 RELIC_TALLY、43 RELIC_CHAIN_BURIAL、52 RELIC_TAINT、57 RELIC_CURSE_HASTE、58 WEAPON_SPIRIT、59 RELIC_CURSE_REVIVAL、60 RELIC_BLOOD_PACT、66 RELIC_CURSE_GRAVE、76 RELIC_ATTACK_HEX、85 RELIC_RIFT_OVERRIDE、98 RELIC_GRAVE_CURSE、103 RELIC_GRAVE_LORD、105 RELIC_TRAINER（uncommon 38/43/105；其余 rare）。

## 3. 引擎设计

### 3.1 放置（本计划唯一的新逻辑）

洗牌后布局 = `[被动×k（index 0..k-1）, start card（index k）, 洗牌活区（index k+1..）]`

- `StartCardShuffleEffect`：分离 start card 后**再分离 isPassive**；其余照常洗牌；合成时被动垫底、start card 落在 passiveCount 位。
- 两个分支都处理：AlwaysBottom 与 ShuffleOrderOverride（被动不参与自定义顺序，仍强制垫底）。
- 战斗开始走 start card 开场洗牌 → 同一入口自动覆盖 ✓。
- 回合内无需二次放置（见 §2 推导）。

### 3.2 免疫（选取池补 isPassive 排除）

- §2 清单逐处加 `cardScript.isPassive` 条件（Stage ×9、Delay ×1、Exile ×6、Bury 防御性 ×2）。
- 按现有惯例各效果类持私有 helper、就地加条件，不做跨类重构。

### 3.3 常驻监听 + 每次事件触发：零改动（§2 事实）

### 3.4 明确不做（依赖第 4/5 步，列出不阻塞）

| 卡 | 依赖 |
|----|------|
| 38 RELIC_TALLY | 回合结束事件（第 4 步） |
| 57 RELIC_CURSE_HASTE | 攻击次数授予（第 4 步） |
| 98 RELIC_GRAVE_CURSE / 103 RELIC_GRAVE_LORD | 攻击力解析源（第 4 步） |
| 60 RELIC_BLOOD_PACT | 伤害管线重写（第 4/5 步，需单独拍板） |
| 85 RELIC_RIFT_OVERRIDE | 信徒语义重写（第 5 步） |
| 19 RELIC_DEATH_KNELL | 「触发其遗言」新动词（第 4/5 步） |
| 52 RELIC_TAINT | 「相邻」谓词——需先定义相邻语义（开放问题 #3） |
| 全部 14+1 张 | prefab 配置（第 5 步批量） |

## 4. 实施步骤（获「修改代码」后按序）

1. `StartCardShuffleEffect` 放置逻辑（含 ShuffleOrderOverride 分支）
2. 选取池补 isPassive（Stage / Delay / Exile / Bury）
3. 测试（§5）+ 全量 EditMode 回归
4. spec §Passive Cards 状态注记（引擎部分 ❌→✅，注明配置属第 5 步）

## 5. 测试计划（EditMode，HeadlessCombatTestFixture）

| 用例 | 断言 |
|------|------|
| Shuffle_PlacesPassivesBelowStartCard | 洗牌后布局 = [被动.., start, rest..] |
| Shuffle_KeepsPassivesBelowStartCard_AcrossRounds | 连续两轮洗牌后布局仍成立 |
| Passive_NeverEntersLiveZone | 模拟 N 次「揭晓+放回 index 0」后被动仍在墓地侧 |
| StageMyCards_SkipsPassives | 被动在牌堆中也不被置顶选取 |
| DelayMyCards_SkipsPassives / ExileMyCards_SkipsPassives | 延后/放逐选取跳过被动 |
| Revive_AlreadyExcludesPassives | 第 2 步回归确认 |
| Passive_CountsTowardGraveCount | ownerInGraveAmountRef 计入被动 |
| Passive_ListenerFiresEveryEvent | 同一事件 raise 两次 → 监听触发两次（per-event firing） |
| CustomShuffleOrder_ExcludesPassives | 自定义顺序不包含被动（若开放问题 #1 通过） |

## 6. 开放问题（默认值即建议）

1. **ShuffleOrderOverride 遇到被动卡**：建议被动不参与自定义顺序、无条件垫底（自定义顺序是测试/演出特性，被动的位置不变量优先级更高）。
2. **被动物理表现**：被动恒在墓地侧 = 视觉牌堆底部、背面朝上。预计现有 cascade/arc/float 布局自动成立，实施后做一次视觉验证（如需修，按 VISUAL-FIX 规范走）。
3. **RELIC_TAINT「相邻」语义**：牌堆是栈不是网格——相邻 = combinedDeckZone 索引相邻？跨阵营如何界定？属第 5 步卡配置前置设计，不阻塞本引擎。
