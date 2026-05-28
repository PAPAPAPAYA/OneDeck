# OneDeck Master TODO List
# Generated: 2026-05-28
# Based on: DevLog.cs (active items) + summary HTMLs (recommendations)

# ---------------------------------------------------------------
# 优先级说明
#   P0 — 阻塞发布 / 架构根基不稳
#   P1 — 核心功能 / 直接影响玩家体验
#   P2 — 质量提升 / 开发效率 / 技术债
#   P3 — 内容填充 / 长期价值
#   —    已标记完成 (后续可归档)
# ---------------------------------------------------------------

# ===============================================================
# P0 — 架构根基（当前最高优先级，2-3周内完成）
# ===============================================================

[P0-01] EffectRecorder 动画系统收尾
    - 验证所有现有动画走 EffectRecorder 路径
    - 消除旧动画代码路径（Lerp/DOTween legacy）
    - 确保 Play Mode 中复杂组合（SACRIFICIAL_SPIRIT + DUMMY、RIFT_DRAGON + RIFT）表现正确
    - 来源: dev_progress_report, 05-16, 05-19, 05-24

[P0-02] Headless 测试全面运行并接入 CI
    - 运行全部 Edit Mode 测试（~79 用例），确认全部通过
    - 注意 delay 测试已移除（05-24），需确认不影响覆盖率
    - 建立"测试失败阻断提交"规则
    - 来源: 05-16, 05-19, dev_progress_report

[P0-03] Pre Effect Event 废弃代码清理
    - 所有卡牌已迁移至 check cost + effect 模式（05-14 完成）
    - 彻底移除废弃的 pre effect event 相关代码引用
    - 验证无残留调用点
    - 来源: DevLog.cs (refactor), weekly_summary, 05-19, 05-24

[P0-04] CombatManager 职责拆分（DevLog: refactor）
    - 将 deck sync / 动画调度 / 位置计算 / focus 管理 从 CombatUXManager 继续外抽
    - 参考 CardMoveConfig / DeckPositionCalculator 的拆分模式
    - 降低 CombatManager.cs / CombatUXManager.cs 的修改频率
    - 来源: DevLog.cs (refactor), dev_progress_report


# ===============================================================
# P1 — 核心功能缺失 / 直接玩家体验
# ===============================================================

[P1-01] 疲劳 / Overtime 机制实现（DevLog: feature / design）
    - CombatManager 中已有疲劳触发逻辑（roundNumRef > overtimeRoundThreshold）
    - 需要完整设计：疲劳卡牌效果、视觉反馈、UI 提示
    - 与现有 DeckTester 集成验证平衡性
    - 来源: DevLog.cs (feature), 05-19, 05-24, dev_progress_report

[P1-02] 商店系统基于稀有度迭代（DevLog: design / feature）
    - CardScript 已有 rarity 字段
    - 实现基于稀有度的 roll 权重（shopRollWeightMultiplier）
    - 迭代商店 UI 和购买体验
    - 来源: DevLog.cs (feature/design), 05-19, 05-24

[P1-03] Enemy Deck Recorder 实现（DevLog: tools）
    - PRD 已完成（prd-deck-enemy-recorder-2026-05-27.md）
    - 需要实现 EnemyDeckRecorder.cs，放在 Managers/WriteRead/
    - 对接 DeckSaver 的 cardTypeToPrefabCache
    - 支持自动录制（战斗结束触发）和手动录制
    - 来源: DevLog.cs (tools), 本周更新

[P1-04] 状态效果视觉反馈补齐
    - infected 状态：替换卡牌材质 + 毒液喷射粒子效果
    - better power wisp 粒子
    - power gain 卡牌粒子效果
    - consume 效果视觉反馈
    - 来源: DevLog.cs (viscom), 05-19, 05-24

[P1-05] 卡牌移动动画优化
    - change z dynamically（更自然的深度变化）
    - 成本不足时抖动卡牌（shake revealed card if cost not met）
    - 事件触发时卡牌放大（enlarge card when event triggered）
    - 来源: DevLog.cs (viscom)


# ===============================================================
# P2 — 测试体系 / 开发工具 / 配置验证
# ===============================================================

[P2-01] Play Mode 集成测试（Strategy B）
    - 利用 soldier_skeleton test、exile + stage test 等牌组
    - 验证 PopUpBatch / MoveToTopPopUpBatch 在真机表现
    - 覆盖 SACRIFICIAL_SPIRIT + DUMMY、RIFT_DRAGON + RIFT 等复杂组合
    - 来源: 05-19, 05-24, dev_progress_report

[P2-02] 回归测试套件建立
    - 维护 RegressionChecklist.md/html
    - 每次视觉修复后跑完整回归列表
    - 加入 CI 自动化
    - 来源: 05-24, dev_progress_report

[P2-03] 配置自动化验证工具
    - 基于 CardIDRetriever 扩展：检测 cardTypeID 重复
    - 检测多 effect instance 循环风险
    - 比对卡牌描述与 prefab 配置的一致性
    - 来源: DevLog.cs (feature), 05-19, 05-24

[P2-04] Type B 卡牌 edge cases 排查
    - 检查非 1 effect 1 recorder 模式的卡牌
    - 确保多 effect 共享 recorder 时动画顺序正确
    - 来源: weekly_summary, 05-13

[P2-05] 测试策略实施（DevLog: anything else）
    - Strategy A: simulate logic in editor mode（Headless）
    - Strategy B: simulate logic in play mode（Play Mode）
    - Strategy C: regression batch test（DeckTester 自动化跑多配置）
    - 来源: DevLog.cs (anything else)


# ===============================================================
# P3 — 技术债 / 代码质量 / 可维护性
# ===============================================================

[P3-01] 中文硬编码迁移为 StringSO（DevLog: refactor）
    - localization stringSO to store chinese translation
    - 避免编码错误
    - 来源: DevLog.cs (refactor)

[P3-02] Debug 日志开关（DevLog: refactor）
    - clean up or rather add a switch to toggle debug messages
    - 统一使用 #if DEBUG 或 configurable log level
    - 来源: DevLog.cs (refactor)

[P3-03] 注释后空格规范化（DevLog: anything else）
    - add space after annotations
    - 低风险，可持续进行
    - 来源: DevLog.cs (anything else)

[P3-04] 代码清理：废弃字段审计
    - 继续清理 DeprecatedFields_Audit.md 中标记的 semi-retired 字段
    - snapshotDeckSize、targetIndices 迁移评估
    - 来源: 05-24, dev_progress_report

[P3-05] moveToTopPopUpBatch deck-focus restoration 统一（DevLog: refactor）
    - moveToTopPopUpBatch 有自己的 deck-focus restoration 逻辑
    - 与 PlayRequestCoroutine 统一
    - 来源: DevLog.cs (refactor)

[P3-06] CombatUXManager 进一步拆分
    - 当前仍承担 deck sync、动画播放、位置计算、focus 管理
    - 参考 CardMoveConfig / DeckPositionCalculator 模式继续外抽
    - 来源: 05-24, dev_progress_report


# ===============================================================
# — 已标记完成（近期 DevLog 更新）
# ===============================================================

[✓] clean up pre effect event — 废弃代码已清理（05-19）
[✓] more headless tests — ~79 用例已建（05-19）
[✓] clean up and organize logic and visual — 逻辑-视觉分离完成（05-02）
[✓] dead state - can't enter shuffle state; TriggerStartCardEffect() — 已修复
[✓] power reaction effect & when gain power — ValueTrackerManager 已实现
[✓] make all chinese into english — 卡牌名已英文化
[✓] curse card type id: make it a stringSO — 已完成（04-12）
[✓] rarity attibute in card script — 已有 rarity 字段
[✓] show card tag (deathrattle, linger) — 已实现
[✓] cards tweaks — 已完成多轮
[✓] change rift to not be minion — 已完成
[✓] check desc format — 已完成
[✓] chang blood-letting summoner — 已改为"有副作用的传送门"
[✓] check unstable_portal behaviour — 已验证
[✓] change cardScript to use tab to indent — 已规范
[✓] consider refactor start card — 已完成
[✓] simplify and split up CombatUXManager — 已提取 CardMoveConfig / DeckPositionCalculator
[✓] ApplyStatusEffectCore() 相关重构 — 已完成
[✓] add and refine animation — CombatEffectAnimations.md 已整理
[✓] effect chain manager issue — 已修复
[✓] skill to check infinite card combos — 已实现
[✓] tool to validate no duplicate card type id — 已实现
[✓] skills and SOPs to generate test plan — 已建立流程
[✓] script name check — 已完成
[✓] iterate on shop based on rarity — 已有 shopRollWeightMultiplier
[✓] pop up + slot in animation — 已完成（05-20 ~ 05-24）
[✓] status effect animations — 大部分已完成
[✓] add and refine animation — 已完成
[✓] stage/bury and deck shift 同步 — 已修复
[✓] effect recorder based animation system — 原型 + 完整实现（05-10 ~ 05-18）
[✓] give power gives 2 times the power — 已修复
[✓] make other value tracker related headless test — 已完成
[✓] power_craver headless test — 已完成
[✓] almighty test — 已完成
[✓] swtich + goblin charge team + hostile soldier skeleton — 已修复
[✓] make ALMIGHTY + reactive cards test — 已完成
[✓] soldier_skeleton staging — 已修复
[✓] pre effect event cost 迁移 — 所有卡牌已迁移（05-14）
[✓] counter's translation — 已修复
[✓] slime — 已修复
[✓] exile — 已修复
[✓] stage — 已修复
[✓] test if dmg is correct — 已验证
[✓] GRAVE_Punch + SPIKE_SKELETON + ETERNAL_GHOST — 已测试
[✓] transition focus coroutine bug — 已修复
[✓] auto reveal in phase 1 — 已实现
[✓] test SPIKE_SKELETON — 已完成
[✓] fixed copycurse — 已修复
[✓] power wisp destroy timing — 已修复
[✓] some effects don't include card in reveal zone — 已修复
[✓] bury: ai tested — 已完成
[✓] generate test plans — bury / deathrattle / general / conjure / curse — 已完成
[✓] when curse is added, power wisp target pos — 已修复


# ===============================================================
# 节奏建议（来自 dev_progress_report）
# ===============================================================

开发节奏调整：
    1. 改为 2 周 Sprint 制
    2. 周末强制休息
    3. 每晚 22:00 前结束编码
    4. 新功能开发前先写 1 页技术方案
    5. 每次提交配清晰完成描述

里程碑：
    Closed Beta  — 目标 2026-06-30（EffectRecorder 落地 + Headless 核心覆盖 + 80 张卡 + 无阻塞 Bug）
    Open Beta   — 目标 2026-08（新手引导 + 排行榜 + 云存档 + 100 张卡 + 数据验证平衡性）
    v1.0 正式版 — 目标 2026-Q4（完整美术 + 音效 + 多语言 + 赛季系统）


# ===============================================================
# 本周建议（Immediate — 接下来 3-5 天）
# ===============================================================

本周 Focus：
    1. 运行全部 Headless 测试，确认通过
    2. 清理 pre effect event 废弃代码
    3. Play Mode 跑 soldier_skeleton test / almighty test 验证视觉效果
    4. 开始 Enemy Deck Recorder 实现（PRD 已就绪）
    5. 评估疲劳机制设计文档
