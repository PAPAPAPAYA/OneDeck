# Zombie-Baseline Card Balance Audit (Full-Cycle Model)

> Generated 2026-08-01. Data sources: Notion `one deck demo cards` (rarity / category / intent, updated 2026-07-31) + live prefab values extracted by `tools/scripts/extract_card_prefabs.py` (output: `tools/outputs/card_prefab_extract.txt`).
> Model: Start Card fixed at deck bottom (`StartCardPlacement.AlwaysBottom`) => every card reveals exactly once per round (q=1). Baseline: **ZOMBIE = 2.0 dmg/round**. HP 20, combat window R~4 rounds.

## 1. Valuation Conventions (per-round value, zombie = 2.0)

| Effect | Value | Basis |
|--------|-------|-------|
| Direct damage X | X | face value |
| Bury 1 hostile | +2.0 | denies one enemy reveal this round (~avg enemy card) |
| Bury 1 friendly | -1.2 | -2.0 denied reveal + ~0.8 expected DeathRattle (30% concentration x ~2.5) |
| Stage 1 friendly | +1.5 | recycle/double-trigger potential (~full trigger half the time) |
| Give 1 Power (random friendly) | +0.8 | utilization U~0.4 x ~2.5 remaining rounds, amortized |
| Give 1 Power (self, damage card) | +1.5 | pays per remaining reveal |
| Give Power to next-X (faction-blind) | ~0 (can be negative) | symmetric decks: leaks ~half to enemy |
| Add 1 Rift token | +1.0 | ~50% consumed as ammo (1.5) + ~50% self-exile recycle (0.75) |
| Consume 1 Rift token | -1.0 | ammo cost |
| Enhance 1 Curse | +1.2 | ramp: +1 curse dmg/round for remaining rounds, amortized over R=4 |
| Consume hostile Curse 1 Power | -1.2 | spends your own weapon's future damage |
| Exile friendly (non-rift) | -2.0 | permanent loss |
| Linger trigger (graveyard-checked) | x0.5 uptime | active only after own reveal each round |
| Global listener, NO zone check | x1.0 uptime | fires from any zone (see §3) |
| Counter "every 2 reveals" | x0.5 | full cycle = once per 2 rounds |
| Conditional trigger (buried/staged/exiled) | x0.3 unsupported / x0.8 supported | concentration-limited |

Verdict bands (per-round value vs zombie 2.0):
- **common**: target 1.6-2.4 (0.8-1.2x)
- **uncommon**: target 2.4-4.0 (1.2-2.0x), conditional cards judged at supported value
- **rare**: target 4.0-8.0 (2-4x) OR high-variance build-around
- Verdicts: `++` severely over, `+` over, `=` in band, `-` under, `!` negative/anti-value. `C` = global-listener engine (no zone gate).

## 2. Audit Table

### Common (17)

| Card | Effect (current prefab) | Value | Verdict |
|------|------------------------|-------|---------|
| 行尸走肉 ZOMBIE (baseline) | deal 2 | 2.0 | = (reference) |
| 尸爆 GRAVE_PUNCH | bury 1 friendly; deal 2 x2 | 4.0-1.2 = **2.8** | + |
| 同路人 GRAVE_TOGETHER | bury 1 friendly + bury 2 hostile | 4.0-1.2 = **2.8** | + |
| 冥界裂缝 GRAVE_PORTAL | bury 1 hostile; buried: stage 1 friendly | 2.0+0.45 = **2.45** | = |
| 骷髅士兵 SOLDIER_SKELETON | deal 3; buried: stage self | 3.0+0.9 = **3.9** | + |
| 不死诅咒者 UNDEAD_CURSER | enhance 2; buried: enhance 1 | 2.4+0.36 = **2.8** | + |
| 坠入裂缝 FALL_INTO_RIFT | add 1 rift; bury 1 hostile | 1.0+2.0 = **3.0** | + |
| 次元虫 RIFT_INSECT | deal 2; add 1 rift | 2.0+1.0 = **3.0** | + |
| 咒师 POISONER | enhance 1; deal 3 | 1.2+3.0 = **4.2** | + |
| 异次元的诅咒 RIFT_CURSE | enhance 1; add 1 rift | 1.2+1.0 = **2.2** | = |
| 献祭诅咒 SACRIFICIAL_CURSE | bury 1 friendly; enhance 3 | 3.6-1.2 = **2.4** | = (ramp) |
| 咒食的召唤师 CURSE_THIRST_SUMMONER | stage 1 friendly; on hostile curse revealed: stage self (global) | **1.5 / 3.0** (in curse deck) | =/+ C |
| 疯狂科学家 MAD_SCIENTIST | next 3 cards +2 Power (faction-blind) | **~0 (±1.2)** | ! |
| 铁匠 BLACKSMITH | deal 3; give 1 friendly +1 Power | 3.0+0.8 = **3.8** | + |
| 献祭剑 SACRIFICIAL_SWORD | bury 1 friendly; give 1 friendly +2 Power | 1.6-1.2 = **0.4** | - |
| 有副作用的传送门 SIDE_EFFECT_PORTAL | enhance OWN curse 1 (self-damage); stage 1 friendly | 1.5-1.2 = **0.3** | - |
| 愚者 THE_FOOL | stage highest-Power hostile (~-1: accelerates enemy); deal 4 | 4.0-1.0 = **3.0** | + |
| 棺材制造者 COFFIN_MAKER | deal 3; bury 1 hostile | 3.0+2.0 = **5.0** | ++ |

Common mean ≈ 2.8 — the common floor sits clearly ABOVE zombie; starting deck is the weakest common-tier content.

### Uncommon (35)

| Card | Effect | Value | Verdict |
|------|--------|-------|---------|
| 献祭仪式 SACRIFICE_RITUAL | bury 1 friendly; add 2 rifts | 2.0-1.2 = **0.8** | - |
| 咒食的大召唤师 CURSE_THIRST_ARCH_SUMMONER | consume 2 curse Power (-2.4): stage 1 friendly (1.5); next card +2 Power (faction-blind ~0) | **-0.9** | ! |
| 高等传送门 ADVANCE_PORTAL | every 2 reveals: stage 2 friendly | 3.0x0.5 = **1.5** | - |
| 不稳定传送门 UNSTABLE_PORTAL | stage 1 friendly; bury 1 friendly | 1.5-1.2 = **0.3** | - |
| 血肉聚集体 FLESH_COMBINATION | deal dmg = friendly count (P) | **6~10** (P=6~10) | ++ |
| 小范围死亡 SMALL_SCALE_DEATH | bury next 2 (~1F/1E: +0.8); enhance 1 | 0.8+1.2 = **2.0** | = |
| 快速响应协议 QUICK_RESPONSE_PROTOCOL | Linger: per 4 hostile reveals, stage 1 friendly | ~1.5x1.5x0.5 = **1.1** | - |
| 人人为我 ALL_FOR_ONE | deal dmg = total Power on all cards | **1~3** (6+ in power deck) | = |
| 冥界大炮 CORPSE_CANON | bury 1 friendly; on ANY friendly buried: deal 2 (global, self-chain incl.) | 2x3burials-1.2 = **3~5** | + C |
| 冥界邀请 GRAVE_INVITATION | deal 4; per friendly in grave: bury 1 hostile | 4+2x(2~4) = **6~8** | ++ |
| 对生物兵器 ANTI_CREATURE_WEAPON | bury 2 hostile | **4.0** | = (top) |
| 被诅咒的骷髅 CURSED_SKELETON | enhance = friendly grave count (0~9, position-sensitive) | **~3.6 avg / 9+ late** | =/+ |
| 力量渴求者 POWER_CRAVER | deal 3; doubles own Power gains | **3~6** (supported) | = |
| 裂缝召唤师 RIFT_SUMMONER | consume 1 rift: stage 2 friendly | 3.0-1.0 = **2.0** | = |
| 拔苗助长 PREMATURE | consume 1 curse Power: stage the curse (recycle self-damage if already revealed) | **2~4** (needs curse) | = |
| 曼哈顿博士 DR_MANHATTAN | consume 2 own Power: stage 2 friendly + bury 2 hostile | **5.0 / 0** (no Power = dead) | = (high var) |
| 次元引导者 RIFT_GUIDE | consume 1 rift: bury 2 hostile | 4.0-1.0 = **3.0** | = |
| 次元龙 RIFT_DRAGON | deal 4; add 1 rift; staged: add 1 rift | 4.0+1.0 = **5.0** | + |
| 次元兽 RIFT_MONSTER | consume 1 rift: deal 6 | 5.0 x ammo(0.5~0.75) = **2.5~3.75** | = |
| 飞蛾人 MOTH_MAN | on hostile curse Power gain: stage 1 friendly (GLOBAL, no zone check) | **0 / 3~4.5** (in curse deck) | + C |
| 咒食的萨满 CURSE_THIRST_SHAMAN | give X friendly +1 Power, X = hostile curse Power | **0~4.8** (conditional) | = |
| 哥布林冲锋部队 GOBLIN_CHARGE_TEAM | deal 2; staged: deal 4 | 2.0+1.2 = **3.2** | = |
| 武器精灵 WEAPON_SPIRIT | Linger: friendly gains Power => +1 more | **0.8** (2+ supported) | - |
| 咒食的野兽 CURSE_THIRST_BEAST | deal 4; on hostile curse revealed: stage self (global) => deal 4 again | **4 / 8** | + C |
| 针刺骷髅 SPIKE_SKELETON | deal 3; buried: deal 2 x2 | 3.0+1.2 = **4.2** | + |
| 哥布林暗杀部队 GOBLIN_ASSASSIN_TEAM | deal 4; staged: bury 1 hostile | 4.0+0.6 = **4.6** | + |
| 力量转移 POWER_TRANSFER | remove 2 hostile Power; give 2 friendly +1 | **1.6~3.2** | = |
| 碎骨聚集体 BONE_COMBINATION | deal 1 x hostiles buried this round | **1.5** (4+ in bury deck) | - |
| 屠夫 SNATCHER | deal 3; staged: stage 1; buried: bury 1 hostile | 3.0+0.45+0.6 = **4.05** | + |
| 战术爆破手 TACTICAL_BREACHER | deal 4; staged: self +1 Power | 4.0+0.45 = **4.45** | + |
| 能量迸发 POWER_SURGE | deal 3; staged: 2 friendly +1 Power | 3.0+0.5 = **3.5** | = |
| 错乱传送术士 CONFUSED_PORTALMANCER | bury 1 friendly; buried: add 3 rifts | **-0.3 / 2.0 supported** | - |
| 被诅咒的尸体 CURSED_CORPSE | enhance 1; buried: deal 1 x3 | 1.2+0.9 = **2.1** | = |
| 替死鬼 SCAPEGOAT | buried only: deal 5 + stage 1 friendly (no on-reveal effect) | **0 / 6.5** | - (coin-flip) |
| 次元棺材 RIFT_COFFIN | Linger: on friendly exiled, bury 1 hostile | 2x0.5x(2~3) = **2~3** | = |

### Rare (18)

| Card | Effect | Value | Verdict |
|------|--------|-------|---------|
| 未完成的机器人 UNFINISHED_ROBOT | deal 0; double self Power (2^r) | **0 solo / 8+ with transfer** | build-around |
| 力量虹吸人 POWER_SIPHONER | drain 1 Power from each friendly to self; deal (2+P) x2 | **4 / 10+** | + |
| 全能人 ALMIGHTY | every 2 reveals: deal 1 + stage 1 + bury 1 hostile + give 1 Power + add 1 rift + enhance 1 | 7.5x0.5 = **3.75** | = (weak for rare) |
| 人间大炮 BODY_CANON | bury ALL friendly; deal 1 x friendly-in-grave | gross P-1; net **2~7** (position) | =/+ |
| 推进器 BOOSTER | after shuffle: stage 2 friendly + bury 1 friendly | 3.0-1.2 = **1.8** | - |
| 不散的恶灵 ETERNAL_GHOST | Linger: on enemy damaged, deal 1 | 3x0.5 = **1.5** | - |
| 大范围死亡 LARGE_SCALE_DEATH | bury next 4 cards (both factions) | ~1.6+1.5 DR = **3.1** (6+ supported) | = |
| 殉道者 MARTYR | buried: ALL friendly +1 Power | 0.3xPx0.8 = **2.4 / 8+ directed** | build-around |
| 守墓人 GRAVE_KEEPER | on ANY friendly buried (global): deal 6 AND stage self (re-reveal => 6 more) | 2~3 burials => **12~18** | ++ C |
| 远古魔法使用者 ELDER_SORCERER | per friendly staged this round: give 1 Power | (2~3)x0.8 = **1.6~2.4** | - |
| 增殖的厄运 PROLIFERATING_CURSE | copy hostile curse (copies Power) | **3~6** (needs curse) | = |
| 恶化 DETERIORATION | per 2 curse Power: enhance 1 | **2.4~4.8** (snowball) | = |
| 临终诅咒 DEATHBED_CURSE | Linger: on friendly exiled, enhance 1 | (2~3)x1.2x0.5 = **1.2~1.8** | - |
| 次元吞噬者 RIFT_DEVOURER | deal 2; on ANY friendly exile (global): +1 self Power | 2+accumulate => **4~8 ramp** | + C |
| 史莱姆 SLIME | deal 3; buried 2x: add copy of self | **3~6** | = |
| 不愚蠢的埋葬 WISE_BURIAL | staged: bury 1 DeathRattle/Linger friendly (directed) | **0.75** (tool card) | - (enabler) |
| 诅咒附魔 CURSE_ENCHANTMENT | Linger: on enemy damaged, enhance 1 | 3x1.2x0.5 = **1.8** | - |
| 乌合之众 CROW_CROWD | transfer ALL friendly Power to hostile curse | **2~5** | = |

## 3. Key Findings

1. **Severely over (`++`)**: 守墓人 (12-18, zero-variance global engine), 血肉聚集体 (6-10 guaranteed), 冥界邀请 (6-8), 棺材制造者 (5.0 as a *common*).
2. **Over (`+`)**: 咒食的野兽 (8 in curse deck), 次元吞噬者 (global ramp), 次元龙 (5.0), 冥界大炮 (3-5 global), 飞蛾人 (global in curse deck), plus a cluster of commons at 2.8-4.2 (咒师 4.2, 骷髅士兵 3.9, 铁匠 3.8).
3. **Anti-value / near-zero (`!`)**: 咒食的大召唤师 (-0.9: spends 2 curse Power = own weapon, buys stage+faction-blind buff), 疯狂科学家 (~0 symmetric, negative if enemy damage density higher).
4. **Under (`-`)**: 献祭剑 0.4, 有副作用的传送门 0.3, 献祭仪式 0.8, 不稳定传送门 0.3, 高等传送门 1.5, 快速响应协议 1.1, 武器精灵 0.8, 错乱传送术士 -0.3, 碎骨聚集体 1.5, 替死鬼 coin-flip; rare-tier underperformers: 推进器 1.8, 不散的恶灵 1.5, 远古魔法使用者 1.6-2.4, 临终诅咒 1.2-1.8, 诅咒附魔 1.8, 全能人 3.75 (weak rare).
5. **Global-listener engines (`C`)**: 守墓人, 冥界大炮, 飞蛾人, 咒食的野兽, 咒食的召唤师, 次元吞噬者 — triggers fire from ANY zone with no cost check (verified: `InvokeEffectEvent` has no zone gate; Linger check is opt-in). These are the only truly order-invariant cards and cluster at the top of the value table. Audit rule: `Trigger = global event AND no Check` => price as "fires every round, N times".
6. **Rarity inversion**: several rares (推进器, 不散的恶灵, 临终诅咒, 诅咒附魔, 远古魔法使用者) rate below the common mean (2.8). Rares are acting as "conditional build-arounds", not "powerful cards" — see §4.
7. **Zombie is below the common floor**: nearly every common beats 2.0 unconditional value. Fine for progression feel, but it means zombie should NOT be the design mid-point; treat zombie as the *floor of pity*, and 3.0/round as the de-facto common mean.

## 4. Rarity Configuration Assessment

Current mapping: common = resource/condition enablers, uncommon = conditional mid-power, rare = powerful-general OR strong-synergy.

**What works**: rarity = shop appearance rate, so synergy-dependent/high-variance cards SHOULD be rare (you likely own the enablers by the time they show up), and self-sufficient floor cards SHOULD be common. The pool's actual distribution already follows this.

**Problems**:
1. "Powerful general" and "strong-synergy" should not share a rarity. A powerful general rare is an auto-pick for every deck — rarity then just adds shop luck. The audit shows the pool has essentially NO powerful-general rares anyway; rares are all build-arounds. Recommendation: formally redefine **rare = build-around / high-variance**; if a powerful-general card is wanted, cap it at ~1.5x zombie and put it at uncommon.
2. Rarity should track **variance / synergy-dependence**, not power. Suggested numeric bands (per-round, zombie=2): common 1.6-2.4 self-sufficient; uncommon 2.4-4.0 with achievable conditions; rare = uncapped but must require setup or carry real whiff risk. The audit table flags every out-of-band card.
3. Watch the common resource-generator ratio: generators are dead cards without consumers. Currently ~7/17 commons are resource/conditional — borderline. Keep self-sufficient commons >= 60% so the early-deck floor holds.
4. Category (`无条件/必定能满足/需要满足条件/产出/消耗`) already encodes the variance axis — consider making rarity and category consistent by rule (e.g. 消耗资源 cards should not be common), then enforce it in review.

## 5. Regeneration

```
python tools/scripts/extract_card_prefabs.py > tools/outputs/card_prefab_extract.txt
```
Recompute values per §1 conventions after any prefab rebalance. Keep this file overwritten in place (no versioned copies).
