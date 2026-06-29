# OneDeck 每回合伤害分析（蒙特卡洛模拟）

## 模拟参数
- 卡组：真实 3.0 no cost 卡池，敌我双方各随机 6 张，可重复。
- 回合：只看稳态，两次 Start Card 触发之间为一回合。
- Start Card 位置：按 Unity 代码中的高斯分布（mean=中间，std=deckSize×0.15， clamp 不到顶）。
- 计入 Power/Counter 效果（Counter/Rest 跳过未建模）。
- 统计视角：对敌方玩家造成的伤害，包括敌方 JU_ON 等诅咒卡对自己的伤害。
- 模拟量：**100 个独立 session × 每 session 500 个记录回合 = 69,900 回合**。

## 关键近似与限制
1. 为了控制运行时间和防止状态爆炸，对每方卡牌总数做了软上限（约 12~28 张），RIFT/JU_ON/SLIME 复制等 token 超过上限后会被丢弃。
2. Power 单层上限设为 20，防止 WEAPON_SPIRIT、CURSE_ENCHANTMENT 等互动出现指数爆炸。
3. Graveyard 按“每回合内友方被埋葬数量”近似（回合结束重置）。
4. 未实现 Counter、Rest 跳过、Shield、敌方 AI 差异等细节。
5. 一些高成本卡（RIFT_DRAGON、DR_MANHATTAN 等）因 token/Power 资源不足，模拟中很少触发，结果可能偏低。

## 结果说明
- **Avg Dmg/Round (when present)**：该卡在场时，平均每回合对敌方造成的伤害。
- **Prob Dmg/Round (when present)**：该卡在场时，每回合至少造成一次伤害的概率。
- **Present Rounds**：该卡（包括复制/token）在所有 session 中累计在场的回合数。


# OneDeck Damage Per Round (steady state, random 6v6, with replacement)
Sessions: 100, rounds per session: 500, warmup per session: 200
Total rounds recorded: 69,900
Unique card instances tracked: 23805
Unique CIDs in totals: 74
Total damage to enemy per round (both sides): 52.445

| Card | Avg Dmg/Round (when present) | Prob Dmg/Round | Total Dmg | Present Rounds |
|---|---|---|---|---|
| ETERNAL_GHOST | 120.038 | 0.729 | 755160.0 | 6,291 |
| ALL_FOR_ONE | 24.448 | 0.415 | 341784.0 | 13,980 |
| POWER_SIPHONER | 14.592 | 0.444 | 234600.0 | 16,077 |
| POWER_CRAVER | 10.738 | 0.471 | 82567.0 | 7,689 |
| BODY_CANON | 10.328 | 0.471 | 129946.0 | 12,582 |
| GRAVE_PUNCH | 7.669 | 0.456 | 80410.0 | 10,485 |
| AVENGER | 6.735 | 0.432 | 56495.0 | 8,388 |
| JU_ON | 6.342 | 0.380 | 618260.0 | 97,486 |
| RIFT_DEVOURER | 6.338 | 0.430 | 93034.0 | 14,679 |
| GRAVE_KEEPER | 6.313 | 0.452 | 88254.0 | 13,980 |
| CURSE_THIRST_BEAST | 6.175 | 0.453 | 69060.0 | 11,184 |
| BLACKSMITH | 5.858 | 0.438 | 49139.0 | 8,388 |
| TACTICAL_BREACHER | 5.793 | 0.460 | 72887.0 | 12,582 |
| POISONER | 5.623 | 0.456 | 51100.0 | 9,087 |
| SPIKE_SKELETON | 5.421 | 0.489 | 49260.0 | 9,087 |
| GOBLIN_CHARGE_TEAM | 5.010 | 0.454 | 66544.0 | 13,281 |
| FLESH_COMBINATION | 4.883 | 0.441 | 61437.0 | 12,582 |
| GRAVE_INVITATION | 4.720 | 0.462 | 52791.0 | 11,184 |
| SNATCHER | 4.598 | 0.455 | 54638.0 | 11,883 |
| CORPSE_CANON | 4.455 | 0.298 | 59164.0 | 13,281 |
| UNFINISHED_ROBOT | 4.226 | 0.214 | 47264.0 | 11,184 |
| GOBLIN_ASSASSIN_TEAM | 4.225 | 0.434 | 62018.0 | 14,679 |
| SLIME | 3.647 | 0.433 | 245705.0 | 67,368 |
| SOLDIER_SKELETON | 3.599 | 0.437 | 42767.0 | 11,883 |
| COFFIN_MAKER | 3.478 | 0.441 | 34037.0 | 9,786 |
| BONE_COMBINATION | 3.337 | 0.255 | 46655.0 | 13,980 |
| THE_FOOL | 3.283 | 0.437 | 27536.0 | 8,388 |
| POWER_SURGE | 2.507 | 0.444 | 38550.0 | 15,378 |
| CURSED_CORPSE | 1.782 | 0.068 | 19929.0 | 11,184 |
| ALMIGHTY | 1.709 | 0.229 | 19114.0 | 11,184 |
| RIFT_MONSTER | 0.609 | 0.051 | 7237.0 | 11,883 |
| SCAPEGOAT | 0.540 | 0.070 | 7552.0 | 13,980 |
| RIFT_DRAGON | 0.132 | 0.010 | 1017.0 | 7,689 |
| DETERIORATION | 0.000 | 0.000 | 0.0 | 9,786 |
| MARTYR | 0.000 | 0.000 | 0.0 | 8,388 |
| RIFT_COFFIN | 0.000 | 0.000 | 0.0 | 11,184 |
| PROLIFERATING_CURSE | 0.000 | 0.000 | 0.0 | 9,786 |
| GRAVE_PORTAL | 0.000 | 0.000 | 0.0 | 15,378 |
| RIFT | 0.000 | 0.000 | 0.0 | 47,933 |
| UNSTABLE_PORTAL | 0.000 | 0.000 | 0.0 | 7,689 |
| MOTH_MAN | 0.000 | 0.000 | 0.0 | 17,475 |
| BLIND_COMBAT_PRIEST | 0.000 | 0.000 | 0.0 | 11,184 |
| CURSE_THIRST_SHAMAN | 0.000 | 0.000 | 0.0 | 16,776 |
| WISE_BURIAL | 0.000 | 0.000 | 0.0 | 16,077 |
| DR_MANHATTAN | 0.000 | 0.000 | 0.0 | 16,776 |
| QUICK_RESPONSE_PROTOCOL | 0.000 | 0.000 | 0.0 | 13,980 |
| CURSE_THIRST_SUMMONER | 0.000 | 0.000 | 0.0 | 10,485 |
| RIFT_SUMMONER | 0.000 | 0.000 | 0.0 | 15,378 |
| POWER_TRANSFER | 0.000 | 0.000 | 0.0 | 9,087 |
| SACRIFICE_RITUAL | 0.000 | 0.000 | 0.0 | 13,281 |
| GRAVE_TOGETHER | 0.000 | 0.000 | 0.0 | 14,679 |
| ADVANCE_PORTAL | 0.000 | 0.000 | 0.0 | 9,087 |
| CURSED_SKELETON | 0.000 | 0.000 | 0.0 | 16,077 |
| SACRIFICIAL_SWORD | 0.000 | 0.000 | 0.0 | 12,582 |
| SIDE_EFFECT_PORTAL | 0.000 | 0.000 | 0.0 | 12,582 |
| UNDEAD_CURSER | 0.000 | 0.000 | 0.0 | 11,184 |
| MAD_SCIENTIST | 0.000 | 0.000 | 0.0 | 7,689 |
| DEATHBED_CURSE | 0.000 | 0.000 | 0.0 | 11,184 |
| BOOSTER | 0.000 | 0.000 | 0.0 | 6,990 |
| RIFT_INSECT | 0.000 | 0.000 | 0.0 | 8,388 |
| CURSE_SUMMONER | 0.000 | 0.000 | 0.0 | 11,883 |
| CONFUSED_PORTALMANCER | 0.000 | 0.000 | 0.0 | 12,582 |
| PREMATURE | 0.000 | 0.000 | 0.0 | 11,184 |
| FALL_INTO_RIFT | 0.000 | 0.000 | 0.0 | 10,485 |
| SMALL_SCALE_DEATH | 0.000 | 0.000 | 0.0 | 9,087 |
| CROW_CROWD | 0.000 | 0.000 | 0.0 | 6,990 |
| RIFT_CURSE | 0.000 | 0.000 | 0.0 | 11,184 |
| RIFT_GUIDE | 0.000 | 0.000 | 0.0 | 11,184 |
| LARGE_SCALE_DEATH | 0.000 | 0.000 | 0.0 | 11,184 |
| ELDER_SORCERER | 0.000 | 0.000 | 0.0 | 11,883 |
| ANTI_CREATURE_WEAPON | 0.000 | 0.000 | 0.0 | 10,485 |
| SACRIFICIAL_CURSE | 0.000 | 0.000 | 0.0 | 12,582 |
| WEAPON_SPIRIT | 0.000 | 0.000 | 0.0 | 12,582 |
| CURSE_ENCHANTMENT | 0.000 | 0.000 | 0.0 | 11,883 |
