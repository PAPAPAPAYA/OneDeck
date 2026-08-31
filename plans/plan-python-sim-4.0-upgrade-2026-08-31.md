# Python Sim 升级到 4.0 口径 — 实施计划

日期:2026-08-31
状态:待确认(按 step-gate 协议,每步完成后停下汇报,等确认再继续)
目标文件:`tools/scripts/one_deck_damage_sim.py`(现有 3.0 口径,1590 行)

## 1. 背景与目标

现有 `one_deck_damage_sim.py` 是 3.0 卡池的蒙特卡洛模拟器:随机组牌、输出每卡每轮期望伤害与胜率。但它读 `Assets/Prefabs/Cards/3.0 no cost (current)/`,只建模 3.0 机制;4.0 的 87 张卡(C15/U45/R27,见 `docs/4.0_Rarity_Iteration_StS2_2026-08-28.md`)的复活/苏醒/信徒/被动等轴完全不支持。

目标:将模拟器升级为 4.0 口径,使其能对 4.0 卡池产出可信的数值强度数据(每卡期望伤害、出场贡献、胜率),作为后续"两卡组合枚举找异常"和"特征回归归因"的数据底座。

**非目标**:不做 Unity headless 批量模拟路线;不做商店环境建模;不做组合枚举与 ML(后续独立计划)。

## 2. 现状盘点

现有模拟器已具备(可复用):

- GameState 骨架:双方卡组、起始卡位置、揭晓循环、回合重置、warmup 统计
- 基础动作:`bury_card` / `stage_card` / `exile_card` / `add_card` / `give_power` / `damage_enemy`
- 事件骨架:`on_me_buried` / `on_friendly_exiled` / `on_enemy_curse_revealed` / `after_shuffle` 等
- 诅咒(=4.0 信徒前身 RIFT)已有 own/enemy curse 的 enhance/consume 模型
- 3.0 遗言集合 `DEATHRATTLE_CIDS`、Linger 门控 `LINGER_CIDS`

主要缺口(对照 `docs/4.0_Glossary.md`):

| 缺口 | 说明 |
|------|------|
| Power → 攻击力 | 3.0 的 Power 概念需迁移为 4.0 ATK(含 攻击力=X 计算型、翻倍、多段 攻击xN) |
| 复活/苏醒 | 墓地(index < startCardIndex)拉回卡组顶;苏醒触发;延迟复活变体 |
| 信徒 token | RIFT token 的生成/揭晓/消耗;`攻击力=本回合放逐的信徒数量` 型计数器 |
| 被动卡 | 19 张 RELIC_:洗牌后固定置于起始卡后、永不被揭晓、每事件触发 |
| 攻击事件 | 友方攻击时/有卡攻击时(RELIC_HIVE / RELIC_ATTACK_HEX) |
| 新触发 | 被强化、友方被强化、友方被埋葬、有卡被埋葬、回合开始/结束、苏醒、友方苏醒、信徒揭晓 |
| 新动词 | 强化N(随机目标)、置顶、延后、生成N信徒、复制自身、攻击次数+N、回响X、触发X的遗言、交换、让墓地友方攻击 |
| 4.0 数据层 | prefab 读取路径指向 3.0 目录;需改读 `Cards/4.0/{0_Common,1_Uncommon,2_Rare}` |

## 3. 语义基准(单一事实源)

- 机制定义:`docs/4.0_Glossary.md`(2026-08-28 版,含所有 Q&A 裁定)
- 近期裁定落在 git 提交:MASS_SACRIFICE 无延迟、BLOOD_PACT 攻击转化为强化、RIFT_OVERRIDE 信徒效果改写、GRAVE_PUPPETEER 墓地打击、GRAVE_ROBBER 快照、DECIMATION 总配额等(18252b2 及此前若干提交)
- 模拟器建模的是**设计语义**(含引擎尚未实现的部分,如苏醒/被动),不是当前 Unity 实现快照——这正是做这层模拟的价值

## 4. 实施步骤

### Step 1 — 数据层:4.0 卡表加载

- `_load_card_info()` 改读 `Assets/Prefabs/Cards/4.0/` 三个稀有度子目录
- 提取字段:`cardTypeID`、`displayName`、printed ATK、rarity(按目录)、生物 flag(ATK 列非空)
- 产出一张 4.0 卡表(脚本内 dict),并与 Notion DB 的 87 张对数,缺失/多余显式报出
- 已知数据风险:`GRAVE_PUNCH` printedAttack=2 存在用户本地未定改动,建模时单独标注
- 验收:卡表数量与池子一致,ATK 缺失卡清单人工过目

### Step 2 — 状态与区域引擎升级

- 卡对象增加 `atk`(替代 power 语义)、`attackTimes`、`enhancedFlag`(【被强化】谓词)、`isBeliever`(RIFT token)、`isPassive`、`echoCounter`(回响)
- 区域不变式落地:墓地 = index < startCardIndex;被动卡每次洗牌后强制置于起始卡后;回响回弹卡不进墓地、不可复活
- 攻击结算统一走一个 `resolve_attack()` 入口(对齐 Unity AttackResolverSource 单入口思路),支持多 term 数值来源(固定值/自身攻击力/计数器/引用)
- 事件骨架扩展:新增苏醒、攻击、被强化、回合开始/结束等触发点

### Step 3 — 通用动词库

按术语表逐词实现为纯函数,每词一个单元可测:

攻击/攻击xN、强化N(随机目标+「额外强化」沿用上一目标)、弱化(仅 WEAKENING_FIELD 特例)、埋葬N[目标]、埋葬卡组顶N、置顶(仅未揭晓区)、延后、放逐、生成N信徒、复制自身、攻击力翻倍、攻击力=X、复活N、延迟复活(落起始卡前一格)、攻击次数+N、回响X、触发X的遗言(目标留墓地、不产生埋葬事件)、交换、让墓地友方攻击(留墓地直接结算、触发攻击事件)

同时实现规范扩展写法:遗言触发两次(RELIC_REQUIEM)、给予遗言永久化(洗牌不失效)、攻击转化为强化(RELIC_BLOOD_PACT)、信徒效果改写(RELIC_RIFT_OVERRIDE)、相邻检查(RELIC_TAINT,任何顺序变化后逐事件检查)、墓地攻击光环(RELIC_GRAVE_LORD)

### Step 4 — 触发框架

- 触发注册表:key = 时机词,value = 卡 + handler;每次事件发生遍历注册表(对齐被动卡"每事件触发、无每回合上限"裁定)
- 触发来源谓词:阵营/生物/信徒/诅咒/遗言/稀有度/攻击力最高最低/【被强化】/除了【X】
- 嵌套触发支持:外层触发后内层子句挂起为一次性监听(FINAL_ESCORT)
- 反循环:沿用现有 chain depth 上限思路,同卡同事件单次触发;上限对齐 Unity 的 99(当前 sim 为 12,改为可配置并默认调高)

### Step 5 — 逐卡建模(87 张)

- 按 cardTypeID 手写 handler(不做 desc NLP 解析),desc 原文以注释贴在 handler 旁供 review
- 顺序:U(45)→C(15)→R(27),每批完成后跑 Step 6 的 sanity 检查再进下一批
- **近似台账** `docs/Sim4_Approximation_Ledger.md`:每张卡记录 建模精度(精确/近似/未建模) + 近似点说明;台账是报告可信度声明的一部分,沿用旧 sim 文件头"approximated"做法但显式成文
- 验收:87 张全部有 handler 或台账条目,零静默跳过

### Step 6 — 校准与报告

- Sanity 检查清单:总伤害守恒(双方 HP 流向对账)、无卡时基准伤害、单卡全同卡组(12×X)压力用例不发散、已知无限循环组合(参照 `docs/Infinity_Check_4.0_2026-08-28.md`)确认被 chain 上限截断
- 输出报告沿用现有格式,字段升级为 4.0 口径:每卡 Avg Dmg/Round、触发次数、苏醒次数、复活拉回数、信徒生成/消耗数、被动触发数
- 与 3.0 sim 的重合卡(若有同 ID)做方向性对比,差异大则回查建模
- 最终产出:一次全池 run(建议 6v6 与 10v10、HP25 与无 HP 四配置),数据存 `tools/outputs/sim4/`

## 5. 风险与对策

| 风险 | 对策 |
|------|------|
| 双源漂移:Unity 侧继续迭代,裁定变化导致 sim 过时 | 台账 + 语义基准章节显式记录"以 glossary + 裁定提交为准";每次 Unity 侧裁定变更后回查对应 handler |
| 被动卡每事件触发在高频事件下性能/发散 | 全局事件计数 + 单局步数上限(超限记为发散并单独上报,不计入统计) |
| 87 张 handler 工作量大、易抄错 desc | handler 旁贴 desc 原文;分批走 step-gate;写批量 diff 脚本核对 handler 内数值与卡表 ATK |
| 高方差卡(类似 3.0 ETERNAL_GHOST)导致数字不稳 | 报告附带置信区间(现 sim 已有基础,补上分位数) |

## 6. 执行协议

按 step-gate 协议执行:每步完成后汇报结果并停下,等确认再进下一步。Step 5 内部按 U/C/R 三批各自过 gate。
