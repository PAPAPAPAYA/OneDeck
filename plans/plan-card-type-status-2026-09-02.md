# 卡牌类型 CardType：诅咒改属 Status 类型（2026-09-02）

## 背景

- 引擎「诅咒」= 单一 typeID `JU_ON`（`GameEventStorage.curseCardTypeID` → `Assets/SORefs/CombatRefs/CurseCardTypeID.asset`），prefab 在 `_DONT INCLUDE/Token/JU_ON.prefab`：`isCreature: 1`、`printedAttack: 0`、`myTags` 空。
- 「生物」= `CardScript.isCreature` 显式标记（4.0 规约：生物 ⟺ ATK 列非空）。isCreature 身兼「卡牌类型」与「会攻击」两职。
- 揭晓攻击为 `AttackEffect` 组件驱动（非引擎按 isCreature 自动结算）——诅咒摘出生物后照常造成伤害，诅咒轴（力量×揭晓×数量）无损。

## 拍板（2026-09-02）

1. **方案 A**：`CardType` 枚举 {None, Creature, Status} 替换 `isCreature` bool（~90 prefab 批量迁移）。
2. 阵营光环（BATTLE_HORN 攻击次数 / RELIC_GRAVE_LORD 墓地攻击）**不加特判**：诅咒随类型自然失去。
3. 0 攻诅咒**仍显示攻击角标**：`HasAttackDisplay = cardType != None || HasAttackAttribute`（Creature/Status 恒显示）。
4. RELIC_TALLY「每埋葬1生物强化1敌方诅咒」：埋诅咒本身不再计数（BuryEffect 计数器只认 Creature），**认**。
5. JU_ON **不补** `Tag.Curse`（与 RIFT token「token 无 tag」拍板一致）。

## 实施步骤

### Step 1 枚举与字段
- `EnumStorage` 新增 `CardType { None=0, Creature=1, Status=2 }`（append-only 注释）。
- `CardScript.isCreature` → `cardType` 字段 + `IsCreature` 属性；`HasAttackDisplay` / `GetAttackTimes` / `GetGraveCreatureAttackAura` 迁移。

### Step 2 调用点迁移（类型语义 → IsCreature，零行为变化）
- BuryEffect / StageEffect / ReviveEffect 生物过滤、BuryEffect TALLY 计数（:452）、AttackTimesGiverEffect 池、BuriedCreatureAttackEffect、GravePuppeteerEffect、AttackGiverEffect 强化池（:274/:335/:344）、ShopBoardPipeline 分板（:126/:131）、ValueTrackerManager tooltip。

### Step 3 伤害语义修正（唯一语义变更点）
- `StatusEffectGiverEffect.PassesDamageFilter`（`onlyTargetEnemyDamagingCards`，默认 true，10+ 张 3.0 卡在用）：`isCreature` → `IsCreature || HasAttackAttribute`。
  - 16 张 0 攻生物（CURSE_EATER / GRAVE_GIANT / HEXBLADE 等）经 `IsCreature` 保持原行为，**零回归**。
  - 增强后的诅咒（attackGrowth>0）按伤害能力纳入；0 攻新诅咒排除（尚无伤害能力）。

### Step 4 选区扩展
- `EffectScript.EffectCreatureFilter` 与 `ReviveEffect.CreatureFilter` 尾部追加 `Status`；Bury/Stage/Revive 过滤加 Status 分支（未来「状态」文案可正选）。

### Step 5 WEAKENING_FIELD 死逻辑清理
- `ModifyAllCreatureAttackThisRoundExceptCurse` 删除 curseCardTypeID 例外（诅咒已非生物，循环自然跳过）；**方法名保留**（WEAKENING_FIELD.prefab UnityEvent 按名序列化绑定，改名断绑定）。

### Step 6 prefab 批量迁移
- python 二进制替换 `Assets/**/*.prefab|*.unity`：`isCreature: 1` → `cardType: 1`、`isCreature: 0` → `cardType: 0`（CRLF 保持）；JU_ON 特置 `cardType: 2`。
- 验证：无 `isCreature` 残留；`cardType: 1` 计 84、`cardType: 2` 计 1。

### Step 7 测试迁移（6 文件）
- 机械迁移：`isCreature = true` → `cardType = EnumStorage.CardType.Creature`；helper 保留 bool 形参、内部换算。
- `Step5BatchBEngineTests.ModifyAllCreatureAttackThisRoundExceptCurse_SkipsCurseAndNonCreature`：curse 卡改为 Status 类型驱动豁免（typeID 例外已删）。
- `EnemyDamagingTargetFilterTests`：注释/断言文案随伤害语义更新。

### Step 8 文档
- AGENTS.md Critical Rules 一行 CardType 说明（守 32KB 上限）。
- docs/GameRules.md 类型定义核对。

### Step 9 验证
- refresh_unity 编译 → SaveScene 清 dirty → EditMode 全量测试。

## 执行状态

- [x] Step 1 枚举与字段（EnumStorage.CardType / CardScript.cardType + IsCreature / HasAttackDisplay 恒显 / aura 两处）
- [x] Step 2 调用点迁移（Bury/Stage/Revive 过滤、TALLY 计数、AttackTimesGiver 池、BuriedCreatureAttack、GravePuppeteer、AttackGiver 强化池、ShopBoardPipeline、VTM tooltip）
- [x] Step 3 PassesDamageFilter → `IsCreature || HasAttackAttribute`（16 张 0 攻生物经 IsCreature 零回归）
- [x] Step 4 EffectCreatureFilter / ReviveEffect.CreatureFilter 尾部追加 Status + 三处过滤分支
- [x] Step 5 WEAKENING_FIELD curseTypeID 例外删除（方法名保留，prefab UnityEvent 按名绑定）
- [x] Step 6 prefab 迁移：131 文件（85→cardType:1、46→cardType:0、JU_ON→cardType:2），零 isCreature 残留
- [x] Step 7 测试迁移：6 文件 21 处机械迁移 + EnemyDamaging 文案 + Step5 WEAKENING 测试改 Status 驱动豁免（curse 置 cardType=Status 而非依赖 typeID）
- [x] Step 8 文档：GameRules.md 无「生物」定义无需改；AGENTS.md 32,419 B（余量 349 B，低于 1 KB 规则）不加行，待整体裁剪
- [x] Step 9 编译 + EditMode 全量验证：426 跑 / 425 过 / 0 失败 / 1 pre-existing Ignore（RecorderAnimationPlayer 嵌套协程时序，非本次引入）。运行时抽验：JU_ON `cardType=Status, IsCreature=False, HasAttackDisplay=True`；SOLDIER_SKELETON_4.0 / BLACKSMITH `cardType=Creature`。
