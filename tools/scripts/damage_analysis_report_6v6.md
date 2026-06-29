# OneDeck 每回合伤害分析（蒙特卡洛模拟）

## 模拟参数
- 卡组：真实 3.0 no cost 卡池，敌我双方各随机 6 张，可重复。
- 回合：只看稳态，两次 Start Card 触发之间为一回合。
- Start Card 位置：按 Unity 代码中的高斯分布（mean=中间，std=deckSize×0.15， clamp 不到顶）。
- 计入 Power/Counter 效果（Counter/Rest 跳过未建模）。
- [Linger] 卡牌已按 Unity 的 `CheckCost_IndexBeforeStartCard` 条件判定：效果只在卡牌位于 Start Card 之前（index 更小、更靠近牌底）时触发。
- 统计视角：对敌方玩家造成的伤害，包括敌方 JU_ON 等诅咒卡对自己的伤害。
- 模拟量：**100 个独立 session × 每 session 500 个记录回合**。

## 关键近似与限制
1. 为了控制运行时间和防止状态爆炸，对每方卡牌总数做了软上限（约 12~28 张），RIFT/JU_ON/SLIME 复制等 token 超过上限后会被丢弃。
2. Power 单层上限设为 20，防止 WEAPON_SPIRIT、CURSE_ENCHANTMENT 等互动出现指数爆炸。
3. Graveyard 按“每回合内友方被埋葬数量”近似（回合结束重置）。
4. 未实现 Counter、Rest 跳过、Shield、敌方 AI 差异等细节。
5. 一些高成本卡（RIFT_DRAGON、DR_MANHATTAN 等）因 token/Power 资源不足，模拟中很少触发，结果可能偏低。

## 结果说明
- **Card / Display Name / Rarity**：卡牌 ID、显示名称（从 prefab 解析）和稀有度（Common=普通，Uncommon=稀有，Rare=罕见）。
- **Avg Dmg/Round (when present)**：该卡在场时，平均每回合对敌方造成的伤害。
- **Prob Dmg/Round (when present)**：该卡在场时，每回合至少造成一次伤害的概率。
- **Present Rounds**：该卡（包括复制/token）在所有 session 中累计在场的回合数。


# OneDeck Damage Per Round (steady state, random 6v6, with replacement)
Sessions: 100, rounds per session: 500, warmup per session: 200
Total rounds recorded: 69,900
Unique card instances tracked: 24022
Unique CIDs in totals: 74
Total damage to enemy per round (both sides): 69.287

| Card | Display Name | Rarity | Avg Dmg/Round (when present) | Prob Dmg/Round | Total Dmg | Present Rounds |
|---|---|---|---|---|---|---|
| ETERNAL_GHOST | 不散的恶灵 | Rare | 65.243 | 0.583 | 1094520.0 | 16,776 |
| ALL_FOR_ONE | 人人为我 | Uncommon | 63.419 | 0.413 | 930928.0 | 14,679 |
| POWER_SIPHONER | 力量虹吸人 | Rare | 15.307 | 0.450 | 213988.0 | 13,980 |
| BODY_CANON | 人间大炮 | Rare | 10.616 | 0.480 | 126150.0 | 11,883 |
| POWER_CRAVER | 力量渴求者 | Uncommon | 8.889 | 0.446 | 118060.0 | 13,281 |
| GRAVE_PUNCH | 尸爆 | Common | 8.612 | 0.466 | 114376.0 | 13,281 |
| GRAVE_KEEPER | 守墓人 | Rare | 7.504 | 0.450 | 94410.0 | 12,582 |
| CORPSE_CANON | 冥界大炮 | Uncommon | 7.124 | 0.431 | 79672.0 | 11,184 |
| FLESH_COMBINATION | 血肉聚集体 | Uncommon | 6.978 | 0.454 | 97558.0 | 13,980 |
| AVENGER | 复仇者 | Uncommon | 6.802 | 0.464 | 118867.0 | 17,475 |
| GRAVE_INVITATION | 冥界邀请 | Uncommon | 6.527 | 0.445 | 63870.0 | 9,786 |
| JU_ON | 诅咒 | Unknown | 6.479 | 0.380 | 515062.0 | 79,492 |
| GOBLIN_ASSASSIN_TEAM | GOBLIN_ASSASSIN_TEAM | Unknown | 6.342 | 0.457 | 62065.0 | 9,786 |
| GOBLIN_CHARGE_TEAM | 哥布林冲锋部队 | Uncommon | 6.070 | 0.453 | 76374.0 | 12,582 |
| TACTICAL_BREACHER | 战术爆破手 | Uncommon | 6.008 | 0.441 | 88190.0 | 14,679 |
| SOLDIER_SKELETON | 骷髅士兵 | Common | 5.783 | 0.435 | 72757.0 | 12,582 |
| POISONER | 咒师 | Common | 5.439 | 0.453 | 68433.0 | 12,582 |
| CURSE_THIRST_BEAST | 咒食的野兽 | Uncommon | 5.169 | 0.436 | 68651.0 | 13,281 |
| THE_FOOL | 愚者 | Common | 5.151 | 0.445 | 43207.0 | 8,388 |
| UNFINISHED_ROBOT | 未完成的机器人 | Rare | 5.090 | 0.256 | 64048.0 | 12,582 |
| POWER_SURGE | 能量迸发 | Uncommon | 4.877 | 0.436 | 64772.0 | 13,281 |
| SLIME | 史莱姆 | Rare | 4.835 | 0.438 | 295738.0 | 61,170 |
| SPIKE_SKELETON | 针刺骷髅 | Uncommon | 4.657 | 0.502 | 39065.0 | 8,388 |
| BLACKSMITH | 铁匠 | Common | 4.575 | 0.433 | 67150.0 | 14,679 |
| COFFIN_MAKER | 棺材制造者 | Common | 4.497 | 0.451 | 66010.0 | 14,679 |
| BONE_COMBINATION | 碎骨聚集体 | Uncommon | 4.232 | 0.245 | 59160.0 | 13,980 |
| SNATCHER | 屠夫 | Uncommon | 4.108 | 0.449 | 68919.0 | 16,776 |
| RIFT_DEVOURER | 次元吞噬者 | Rare | 3.177 | 0.449 | 17766.0 | 5,592 |
| ALMIGHTY | 全能人 | Rare | 2.012 | 0.218 | 19685.0 | 9,786 |
| CURSED_CORPSE | 被诅咒的尸体 | Uncommon | 1.468 | 0.065 | 13341.0 | 9,087 |
| RIFT_MONSTER | 次元兽 | Uncommon | 1.130 | 0.082 | 12640.0 | 11,184 |
| SCAPEGOAT | 替死鬼 | Uncommon | 0.528 | 0.055 | 7756.0 | 14,679 |
| SIDE_EFFECT_PORTAL | 有副作用的传送门 | Common | 0.000 | 0.000 | 0.0 | 11,883 |
| DR_MANHATTAN | 曼哈顿博士 | Uncommon | 0.000 | 0.000 | 0.0 | 13,281 |
| UNDEAD_CURSER | 不死诅咒者 | Common | 0.000 | 0.000 | 0.0 | 11,883 |
| RIFT_INSECT | 次元虫 | Common | 0.000 | 0.000 | 0.0 | 12,582 |
| RIFT_COFFIN | 次元棺材 | Uncommon | 0.000 | 0.000 | 0.0 | 12,582 |
| BLIND_COMBAT_PRIEST | 盲眼战斗牧师 | Unknown | 0.000 | 0.000 | 0.0 | 6,990 |
| RIFT | 次元裂缝 | Common | 0.000 | 0.000 | 0.0 | 48,481 |
| CROW_CROWD | 乌合之众 | Rare | 0.000 | 0.000 | 0.0 | 11,184 |
| SACRIFICE_RITUAL | 献祭仪式 | Uncommon | 0.000 | 0.000 | 0.0 | 13,281 |
| WISE_BURIAL | 不愚蠢的埋葬 | Rare | 0.000 | 0.000 | 0.0 | 11,184 |
| DEATHBED_CURSE | 临终诅咒 | Rare | 0.000 | 0.000 | 0.0 | 8,388 |
| RIFT_GUIDE | 次元引导者 | Uncommon | 0.000 | 0.000 | 0.0 | 9,786 |
| CURSE_THIRST_SUMMONER | 咒食的召唤师 | Uncommon | 0.000 | 0.000 | 0.0 | 11,883 |
| SMALL_SCALE_DEATH | 小范围死亡 | Uncommon | 0.000 | 0.000 | 0.0 | 9,087 |
| PROLIFERATING_CURSE | 增殖的厄运 | Rare | 0.000 | 0.000 | 0.0 | 11,883 |
| QUICK_RESPONSE_PROTOCOL | 快速响应协议 | Uncommon | 0.000 | 0.000 | 0.0 | 13,281 |
| RIFT_DRAGON | 次元龙 | Uncommon | 0.000 | 0.000 | 0.0 | 6,990 |
| MARTYR | 殉道者 | Rare | 0.000 | 0.000 | 0.0 | 13,281 |
| ANTI_CREATURE_WEAPON | 对生物兵器 | Uncommon | 0.000 | 0.000 | 0.0 | 12,582 |
| UNSTABLE_PORTAL | 不稳定传送门 | Uncommon | 0.000 | 0.000 | 0.0 | 17,475 |
| PREMATURE | 拔苗助长 | Uncommon | 0.000 | 0.000 | 0.0 | 8,388 |
| FALL_INTO_RIFT | 坠入裂缝 | Common | 0.000 | 0.000 | 0.0 | 16,776 |
| BOOSTER | 推进器 | Rare | 0.000 | 0.000 | 0.0 | 14,679 |
| CONFUSED_PORTALMANCER | 错乱传送术士 | Uncommon | 0.000 | 0.000 | 0.0 | 10,485 |
| WEAPON_SPIRIT | 武器精灵 | Uncommon | 0.000 | 0.000 | 0.0 | 16,077 |
| CURSED_SKELETON | 被诅咒的骷髅 | Uncommon | 0.000 | 0.000 | 0.0 | 6,990 |
| LARGE_SCALE_DEATH | 大范围死亡 | Rare | 0.000 | 0.000 | 0.0 | 6,291 |
| GRAVE_PORTAL | 冥界裂缝 | Common | 0.000 | 0.000 | 0.0 | 9,786 |
| MOTH_MAN | 飞蛾人 | Uncommon | 0.000 | 0.000 | 0.0 | 14,679 |
| GRAVE_TOGETHER | 同路人 | Unknown | 0.000 | 0.000 | 0.0 | 10,485 |
| ADVANCE_PORTAL | 高等传送门 | Uncommon | 0.000 | 0.000 | 0.0 | 8,388 |
| RIFT_SUMMONER | 裂缝召唤师 | Uncommon | 0.000 | 0.000 | 0.0 | 8,388 |
| POWER_TRANSFER | 力量转移 | Uncommon | 0.000 | 0.000 | 0.0 | 11,883 |
| DETERIORATION | 恶化 | Rare | 0.000 | 0.000 | 0.0 | 11,883 |
| CURSE_ENCHANTMENT | 诅咒附魔 | Rare | 0.000 | 0.000 | 0.0 | 6,990 |
| MAD_SCIENTIST | 疯狂科学家 | Unknown | 0.000 | 0.000 | 0.0 | 14,679 |
| CURSE_THIRST_SHAMAN | 咒食的萨满 | Uncommon | 0.000 | 0.000 | 0.0 | 7,689 |
| CURSE_SUMMONER | CURSE_SUMMONER | Unknown | 0.000 | 0.000 | 0.0 | 11,883 |
| SACRIFICIAL_SWORD | 献祭剑 | Common | 0.000 | 0.000 | 0.0 | 7,689 |
| RIFT_CURSE | 异次元的诅咒 | Common | 0.000 | 0.000 | 0.0 | 6,990 |
| SACRIFICIAL_CURSE | 献祭诅咒 | Common | 0.000 | 0.000 | 0.0 | 10,485 |
| ELDER_SORCERER | 远古魔法使用者 | Rare | 0.000 | 0.000 | 0.0 | 9,087 |