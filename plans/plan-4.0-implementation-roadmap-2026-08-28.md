# 4.0 卡片实施推进顺序（Roadmap）

日期：2026-08-28
上游文档：`plans/plan-4.0-core-cards-2026-08-27.md`（核心池配置）、`docs/4.0_Rarity_Iteration_StS2_2026-08-28.md`（稀有度阶梯 + 删并）、`docs/4.0_CardDesc_Spec.md`（机制规格）

## 1. 现状盘点

已完成：

- 核心池 26 张 prefab 就位（`Assets/Prefabs/Cards/4.0/`；commit 6259c66 / a38283e / 8108902）
- `isCreature` 标记落地；攻击谓词引擎已就位（ddce501：resolver engine / attack predicates / throne zone）
- 稀有度迭代定稿：Notion DB 107 行 → 87 启用 + 3 备用 + 17 已删，DB 已加「状态」列

今日删并的落地偏差（prefab 目录尚未跟上 DB）：

| 卡 | 现状 | 应为 | 依据 |
|----|------|------|------|
| RIFT_TWINS (ID47) | prefab 存在于 `0_Common/` | 已删（与 63 RIFT_PRIEST 孪生） | 迭代 §6.1 |
| COMBO_WARRIOR (ID55) | prefab 存在于 `1_Uncommon/` | 已删（与 92 SNOWBALL 孪生） | 迭代 §6.1 |
| RIFT_MONSTER (ID31) | prefab 存在于 `1_Uncommon/` | 已删（放逐转换四连删并） | 迭代 §6.1 |
| SPIKE_SKELETON (ID1) | `1_Uncommon/`、`rarity: 1` | normal：`0_Common/`、`rarity: 0` | 迭代 §5.6 |
| GRAVE_TOGETHER (ID34) | `0_Common/`、`rarity: 0` | uncommon：`1_Uncommon/`、`rarity: 1` | 迭代 §5.6 |

## 2. 推进顺序

### 第 0 步：对齐删并（小改动，先做）

- 删除（或移入搁置目录）3 张已删卡 prefab；删前检查 commit a38283e 引入的 shop test pool 是否引用
- SPIKE_SKELETON、GRAVE_TOGETHER 移动目录 + 改 `rarity` 字段
- 产出：prefab 目录与 DB「状态 / 稀有度」一致
- 同步 Notion「Unity 配置状态」列（需 Notion 授权）

### 第 1 步：现有 26 张静态验证（只读，不改代码）

- `unity-card-listener-check` 全量（核心池计划验证项第 6 步，无执行记录）
- `unity-card-infinity-check` 跑 4.0 池（引擎/反应卡密度高，引擎扩展前先摸底）
- 产出：两份检查报告；问题清单并入对应批次修复

### 第 2 步（主线）：复活/苏醒引擎（ReviveEffect + onMeRevived）

最大解锁点，信徒 token 真文本的前提（现为 stage 近似）。

- 解锁约 26 张：18 复活源 + 8 苏醒卡 + 复活×诅咒腿（54/65/70/80/85）
- 设计已批准（spec §Revive & Awaken）：
  - 复活 = 墓地 → 卡组顶；延迟复活 = 墓地 → start card 前（复用 R2 bounce 落位）
  - 苏醒 = "when revived"，仅由复活效果触发（Stage / bounce 不触发）
  - 选取：仅 `index < startCardIndex`；排除被动 / 中立 / 已放逐 / 揭晓区；空墓地 = fizzle
  - 复活时：raise onMeRevived + faction 事件 + 每回合复活计数器
  - 前置：清理 legacy Revive 残留（`StatusEffect.Revive`、`CheckCost_Revive`、相关显示串）
- 流程：先写 `plans/plan-4.0-revive-awaken-*.md` 计划文档，审过后明示「修改代码」再实施
- 随此步切换信徒 token（RIFT prefab）为真复活文案/机制

### 第 3 步：被动卡引擎（isPassive）

- 解锁约 15 张 RELIC_*（rare 层主力）
- 设计已批准（spec §Passive Cards）：
  - 无独立区域：普通卡 + `isPassive` 标记
  - 每次洗牌（含战斗开始）置于 start card 后（墓地侧）；永不揭晓、不占揭晓位、不吃疲劳
  - 免疫一切移动效果（埋葬 / 置顶 / 延后 / 放逐 / 复活，且不进移动效果选取池）
  - 常驻监听，条件满足即触发（类 Linger，但位置固定）
  - 仍是卡：可被强化、计入墓地计数、可被谓词读取（仅移动效果排除它）
- 同样先计划文档后实施

### 第 4 步：引擎长尾（打包一个计划）

- 攻击次数授予：62 BATTLE_HORN / 74 COMBO_STARTER / 95 COMBO_GRANTER / 67 EXILE_BERSERKER / 57 RELIC_CURSE_HASTE
- 回合结束事件：21 FINAL_ESCORT / 38 RELIC_TALLY
- 稀有度谓词：86 MASS_REVIVER / 99 DUO_REVIVER
- 存量计数：42 GRAVE_GIANT（墓地友方数）/ 91 CURSE_EATER（敌方诅咒攻击力）
- 攻击力引用：81 MIMIC_BLADE

### 第 5 步：剩余卡批量配置 + 显示层

- 87 启用 − 26 已配 = 61 张；其中「可直接配置 / 需小改」的不必等引擎，可与第 2–4 步并行先配
- 分布依据：Notion DB「Unity 配置状态」列（授权后拉取）
- displayName 中文命名 + cardDesc 显示版统一最后做（现为 CARD_TYPE_ID 占位）
- 3 张备用卡本期不做：51 DELAYER（延后）/ 78 CURSE_ECHO（回响）/ 102 SOUL_SWAPPER（交换）

## 3. 验证轨道（贯穿各步）

| 检查 | 时机 | 备注 |
|------|------|------|
| `unity-card-listener-check` | 每批 prefab 后 | skill 现成 |
| `unity-card-infinity-check` | 第 1 步 + 每个引擎落地后 | 新引擎卡是无限循环高发区 |
| Strategy B Play Mode | 用户明确要求时 | 抽查：GRAVE_PUNCH（埋葬+遗言链）、DETERIORATION（诅咒缩放）、SNOWBALL（被强化反应）；引擎落地后各加 1 张复活/被动卡 |

## 4. 约束与提醒

- AGENTS.md：代码修改需用户明示「修改代码」；Play Mode 测试需用户明示
- Notion 授权后第一件事：把已配置 26 张写入 DB「Unity 配置状态」列
- 被强化轴最薄（强化源 19 : 反应 5，迭代 §6.3 标注待补）——属卡设计问题，不阻塞实施
