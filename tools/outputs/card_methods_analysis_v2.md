# OneDeck 卡片库方法整理

基于 Obsidian 卡片库中 75 张卡片的效果描述提取。

## 一、触发器类型

### 主动触发（自身揭晓/洗牌）（29 种模板）

- **[揭晓时] → 造成 {N}伤害**  —— 使用卡片：ALMIGHTY, AVENGER, BLACKSMITH, COFFIN_MAKER, CURSE_THIRST_BEAST, GOBLIN_ASSASSIN_TEAM, GOBLIN_CHARGE_TEAM, GRAVE_INVITATION, GRAVE_KEEPER, POISONER, POWER_CRAVER, POWER_SURGE, RIFT_DEVOURER, RIFT_DRAGON, RIFT_INSECT, RIFT_MONSTER, SLIME, SNATCHER, SOLDIER_SKELETON, SPIKE_SKELETON, TACTICAL_BREACHER, THE_FOOL, UNFINISHED_ROBOT
- **[揭晓时] → 埋葬 {N}友方**  —— 使用卡片：BOOSTER, CONFUSED_PORTALMANCER, CORPSE_CANON, GRAVE_PUNCH, GRAVE_TOGETHER, SACRIFICE_RITUAL, SACRIFICIAL_CURSE, SACRIFICIAL_SWORD, UNSTABLE_PORTAL
- **[揭晓时] → 埋葬 {N}敌方**  —— 使用卡片：ALMIGHTY, ANTI_CREATURE_WEAPON, COFFIN_MAKER, DR_MANHATTAN, FALL_INTO_RIFT, GRAVE_PORTAL, GRAVE_TOGETHER, RIFT_GUIDE
- **[揭晓时] → 置顶 {N}友方**  —— 使用卡片：ALMIGHTY, CURSE_SUMMONER, CURSE_THIRST_SUMMONER, DR_MANHATTAN, RIFT_SUMMONER, SIDE_EFFECT_PORTAL, UNSTABLE_PORTAL
- **[揭晓时] → 生成 {N}[次元裂缝]**  —— 使用卡片：ALMIGHTY, FALL_INTO_RIFT, RIFT_CURSE, RIFT_DRAGON, RIFT_INSECT, SACRIFICE_RITUAL
- **[揭晓时] → 增强 {N}敌方[诅咒]**  —— 使用卡片：ALMIGHTY, CURSED_CORPSE, POISONER, RIFT_CURSE, SACRIFICIAL_CURSE, SMALL_SCALE_DEATH
- **[揭晓时] → 给予下{X}卡 {N}力量**  —— 使用卡片：BLIND_COMBAT_PRIEST, CURSE_THIRST_SUMMONER, MAD_SCIENTIST
- **[揭晓时] → 造成 {N}伤害 x {N}**  —— 使用卡片：GRAVE_PUNCH, POWER_SIPHONER
- **[揭晓时] → 埋葬后{X}卡**  —— 使用卡片：LARGE_SCALE_DEATH, SMALL_SCALE_DEATH
- **[揭晓时] → 造成所有卡的力量数量的伤害**  —— 使用卡片：ALL_FOR_ONE
- **[揭晓时] → 给予 {N}友方力量**  —— 使用卡片：ALMIGHTY
- **[揭晓时] → 给予一个友方 {N}力量**  —— 使用卡片：BLACKSMITH
- **[揭晓时] → 造成 {N}伤害 x 本回合被埋葬的敌方数量**  —— 使用卡片：BONE_COMBINATION
- **[洗牌后] → 置顶 {N}友方**  —— 使用卡片：BOOSTER
- **[揭晓时] → 将所有友方的力量(排除友方[诅咒])转移到敌方的[诅咒]**  —— 使用卡片：CROW_CROWD
- **[揭晓时 | 敌方[诅咒]每有 **1** 力量] → 给予 {N}友方 {N}力量**  —— 使用卡片：CURSE_THIRST_SHAMAN
- **[揭晓时 | 敌方[诅咒]每有 **2** 力量] → 增强 {N}敌方[诅咒]**  —— 使用卡片：DETERIORATION
- **[揭晓时] → 造成友方数量的伤害**  —— 使用卡片：FLESH_COMBINATION
- **[揭晓时] → 墓地每有 {N}友方:埋葬 {N}敌方**  —— 使用卡片：GRAVE_INVITATION
- **[揭晓时] → 转移所有友方的 {N}力量到自身**  —— 使用卡片：POWER_SIPHONER
- **[揭晓时] → 去除 {N}敌方 {N}力量**  —— 使用卡片：POWER_TRANSFER
- **[揭晓时] → 给予友方 {N}力量 {N}次**  —— 使用卡片：POWER_TRANSFER
- **[揭晓时] → 置顶敌方[诅咒]**  —— 使用卡片：PREMATURE
- **[揭晓时] → 复制敌方 {N}[诅咒]**  —— 使用卡片：PROLIFERATING_CURSE
- **[揭晓时] → 给予 {N}友方 {N}力量**  —— 使用卡片：SACRIFICIAL_SWORD
- **[揭晓时] → 增强自身诅咒 {N}**  —— 使用卡片：SIDE_EFFECT_PORTAL
- **[揭晓时] → 置顶力量最多的敌方**  —— 使用卡片：THE_FOOL
- **[揭晓时] → 增强敌方[诅咒] {N}**  —— 使用卡片：UNDEAD_CURSER
- **[揭晓时] → 翻倍自身力量**  —— 使用卡片：UNFINISHED_ROBOT

### 被动触发（响应事件）（30 种模板）

- **[被埋葬] → 置顶 {N}友方**  —— 使用卡片：GRAVE_PORTAL, SCAPEGOAT
- **[被埋葬] → 获得 {N}力量**  —— 使用卡片：AVENGER
- **[被埋葬] → 生成 {N}[次元裂缝]**  —— 使用卡片：CONFUSED_PORTALMANCER
- **[友方被埋葬] → 造成 {N}伤害**  —— 使用卡片：CORPSE_CANON
- **[被埋葬] → 造成 {N} x {N}伤害**  —— 使用卡片：CURSED_CORPSE
- **[当敌人受到伤害时] → 增强 {N}敌方[诅咒]**  —— 使用卡片：CURSE_ENCHANTMENT
- **[当敌方[诅咒]揭晓时] → 置顶自身**  —— 使用卡片：CURSE_THIRST_BEAST
- **[当友方被去除时] → 敌方[诅咒]获得 {N}力量**  —— 使用卡片：DEATHBED_CURSE
- **[当敌人受到伤害时] → 造成 {N}伤害**  —— 使用卡片：ETERNAL_GHOST
- **[被置顶] → 埋葬 {N}敌方**  —— 使用卡片：GOBLIN_ASSASSIN_TEAM
- **[被置顶] → 造成 {N}伤害**  —— 使用卡片：GOBLIN_CHARGE_TEAM
- **[当卡被埋葬时] → 置顶自身**  —— 使用卡片：GRAVE_KEEPER
- **[被埋葬] → 所有友方获得 {N}力量**  —— 使用卡片：MARTYR
- **[当敌方[诅咒]获得力量时] → 置顶 {N}友方**  —— 使用卡片：MOTH_MAN
- **[获得力量时] → 获得 {N}倍力量**  —— 使用卡片：POWER_CRAVER
- **[被置顶] → 给予 {N}友方 {N}力量**  —— 使用卡片：POWER_SURGE
- **[再有**3<counter>**敌人被揭晓] → 置顶 {N}友方**  —— 使用卡片：QUICK_RESPONSE_PROTOCOL
- **[当友方被去除时] → 埋葬 {N}敌方**  —— 使用卡片：RIFT_COFFIN
- **[当友方被去除时] → 获得 {N}力量**  —— 使用卡片：RIFT_DEVOURER
- **[被置顶] → 生成 {N}[次元裂缝]**  —— 使用卡片：RIFT_DRAGON
- **[被埋葬] → 造成 {N}伤害**  —— 使用卡片：SCAPEGOAT
- **[被埋葬] → 添加自身到卡组中**  —— 使用卡片：SLIME
- **[被置顶] → 置顶 {N}友方**  —— 使用卡片：SNATCHER
- **[被埋葬] → 埋葬 {N}敌方**  —— 使用卡片：SNATCHER
- **[被埋葬] → 置顶自身**  —— 使用卡片：SOLDIER_SKELETON
- **[被埋葬] → 造成 {N}伤害 x {N}**  —— 使用卡片：SPIKE_SKELETON
- **[被置顶] → 获得 {N}力量**  —— 使用卡片：TACTICAL_BREACHER
- **[被埋葬] → 增强敌方[诅咒] {N}**  —— 使用卡片：UNDEAD_CURSER
- **[当友方获得力量时] → 给予该友方 {N}力量**  —— 使用卡片：WEAPON_SPIRIT
- **[被置顶] → 埋葬 {N}友方[亡语]/[萦绕]**  —— 使用卡片：WISE_BURIAL

### 延迟/计数器触发（4 种模板）

- **[每揭晓**2<counter>**次] → 置顶 {N}友方**  —— 使用卡片：ADVANCE_PORTAL
- **[揭晓时 | 墓地每有一个友方] → 造成 {N}次 {N}伤害**  —— 使用卡片：BODY_CANON
- **[揭晓时 | 墓地每有 **1** 友方] → 增强 {N}敌方[诅咒]**  —— 使用卡片：CURSED_SKELETON
- **[揭晓时 | 本回合每置顶过 **1** 友方] → 给予 {N}友方 {N}力量**  —— 使用卡片：ELDER_SORCERER

### 无触发（系统效果）（2 种模板）

- **[] → 卡位增加 {N}**  —— 使用卡片：IncreaseDeckSize, IncreaseDeckSizeLite
- **[] → 生命值上限增加 {N}**  —— 使用卡片：IncreaseHpMax

## 二、按动词分类的方法

### 造成（12 种模板）

- [揭晓时] → 造成 {N}伤害  —— ALMIGHTY, AVENGER, BLACKSMITH, COFFIN_MAKER, CURSE_THIRST_BEAST 等
- [揭晓时] → 造成 {N}伤害 x {N}  —— GRAVE_PUNCH, POWER_SIPHONER
- [揭晓时] → 造成所有卡的力量数量的伤害  —— ALL_FOR_ONE
- [揭晓时 | 墓地每有一个友方] → 造成 {N}次 {N}伤害  —— BODY_CANON
- [揭晓时] → 造成 {N}伤害 x 本回合被埋葬的敌方数量  —— BONE_COMBINATION
- [友方被埋葬] → 造成 {N}伤害  —— CORPSE_CANON
- [被埋葬] → 造成 {N} x {N}伤害  —— CURSED_CORPSE
- [当敌人受到伤害时] → 造成 {N}伤害  —— ETERNAL_GHOST
- [揭晓时] → 造成友方数量的伤害  —— FLESH_COMBINATION
- [被置顶] → 造成 {N}伤害  —— GOBLIN_CHARGE_TEAM
- [被埋葬] → 造成 {N}伤害  —— SCAPEGOAT
- [被埋葬] → 造成 {N}伤害 x {N}  —— SPIKE_SKELETON

### 置顶（12 种模板）

- [揭晓时] → 置顶 {N}友方  —— ALMIGHTY, CURSE_SUMMONER, CURSE_THIRST_SUMMONER, DR_MANHATTAN, RIFT_SUMMONER 等
- [被埋葬] → 置顶 {N}友方  —— GRAVE_PORTAL, SCAPEGOAT
- [每揭晓**2<counter>**次] → 置顶 {N}友方  —— ADVANCE_PORTAL
- [洗牌后] → 置顶 {N}友方  —— BOOSTER
- [当敌方[诅咒]揭晓时] → 置顶自身  —— CURSE_THIRST_BEAST
- [当卡被埋葬时] → 置顶自身  —— GRAVE_KEEPER
- [当敌方[诅咒]获得力量时] → 置顶 {N}友方  —— MOTH_MAN
- [揭晓时] → 置顶敌方[诅咒]  —— PREMATURE
- [再有**3<counter>**敌人被揭晓] → 置顶 {N}友方  —— QUICK_RESPONSE_PROTOCOL
- [被置顶] → 置顶 {N}友方  —— SNATCHER
- [被埋葬] → 置顶自身  —— SOLDIER_SKELETON
- [揭晓时] → 置顶力量最多的敌方  —— THE_FOOL

### 给予（9 种模板）

- [揭晓时] → 给予下{X}卡 {N}力量  —— BLIND_COMBAT_PRIEST, CURSE_THIRST_SUMMONER, MAD_SCIENTIST
- [揭晓时] → 给予 {N}友方力量  —— ALMIGHTY
- [揭晓时] → 给予一个友方 {N}力量  —— BLACKSMITH
- [揭晓时 | 敌方[诅咒]每有 **1** 力量] → 给予 {N}友方 {N}力量  —— CURSE_THIRST_SHAMAN
- [揭晓时 | 本回合每置顶过 **1** 友方] → 给予 {N}友方 {N}力量  —— ELDER_SORCERER
- [被置顶] → 给予 {N}友方 {N}力量  —— POWER_SURGE
- [揭晓时] → 给予友方 {N}力量 {N}次  —— POWER_TRANSFER
- [揭晓时] → 给予 {N}友方 {N}力量  —— SACRIFICIAL_SWORD
- [当友方获得力量时] → 给予该友方 {N}力量  —— WEAPON_SPIRIT

### 埋葬（7 种模板）

- [揭晓时] → 埋葬 {N}友方  —— BOOSTER, CONFUSED_PORTALMANCER, CORPSE_CANON, GRAVE_PUNCH, GRAVE_TOGETHER 等
- [揭晓时] → 埋葬 {N}敌方  —— ALMIGHTY, ANTI_CREATURE_WEAPON, COFFIN_MAKER, DR_MANHATTAN, FALL_INTO_RIFT 等
- [揭晓时] → 埋葬后{X}卡  —— LARGE_SCALE_DEATH, SMALL_SCALE_DEATH
- [被置顶] → 埋葬 {N}敌方  —— GOBLIN_ASSASSIN_TEAM
- [当友方被去除时] → 埋葬 {N}敌方  —— RIFT_COFFIN
- [被埋葬] → 埋葬 {N}敌方  —— SNATCHER
- [被置顶] → 埋葬 {N}友方[亡语]/[萦绕]  —— WISE_BURIAL

### 增强（7 种模板）

- [揭晓时] → 增强 {N}敌方[诅咒]  —— ALMIGHTY, CURSED_CORPSE, POISONER, RIFT_CURSE, SACRIFICIAL_CURSE 等
- [揭晓时 | 墓地每有 **1** 友方] → 增强 {N}敌方[诅咒]  —— CURSED_SKELETON
- [当敌人受到伤害时] → 增强 {N}敌方[诅咒]  —— CURSE_ENCHANTMENT
- [揭晓时 | 敌方[诅咒]每有 **2** 力量] → 增强 {N}敌方[诅咒]  —— DETERIORATION
- [揭晓时] → 增强自身诅咒 {N}  —— SIDE_EFFECT_PORTAL
- [揭晓时] → 增强敌方[诅咒] {N}  —— UNDEAD_CURSER
- [被埋葬] → 增强敌方[诅咒] {N}  —— UNDEAD_CURSER

### 其他（6 种模板）

- [] → 卡位增加 {N}  —— IncreaseDeckSize, IncreaseDeckSizeLite
- [揭晓时] → 将所有友方的力量(排除友方[诅咒])转移到敌方的[诅咒]  —— CROW_CROWD
- [当友方被去除时] → 敌方[诅咒]获得 {N}力量  —— DEATHBED_CURSE
- [揭晓时] → 墓地每有 {N}友方:埋葬 {N}敌方  —— GRAVE_INVITATION
- [] → 生命值上限增加 {N}  —— IncreaseHpMax
- [被埋葬] → 所有友方获得 {N}力量  —— MARTYR

### 获得（4 种模板）

- [被埋葬] → 获得 {N}力量  —— AVENGER
- [获得力量时] → 获得 {N}倍力量  —— POWER_CRAVER
- [当友方被去除时] → 获得 {N}力量  —— RIFT_DEVOURER
- [被置顶] → 获得 {N}力量  —— TACTICAL_BREACHER

### 生成（3 种模板）

- [揭晓时] → 生成 {N}[次元裂缝]  —— ALMIGHTY, FALL_INTO_RIFT, RIFT_CURSE, RIFT_DRAGON, RIFT_INSECT 等
- [被埋葬] → 生成 {N}[次元裂缝]  —— CONFUSED_PORTALMANCER
- [被置顶] → 生成 {N}[次元裂缝]  —— RIFT_DRAGON

### 转移（1 种模板）

- [揭晓时] → 转移所有友方的 {N}力量到自身  —— POWER_SIPHONER

### 去除（1 种模板）

- [揭晓时] → 去除 {N}敌方 {N}力量  —— POWER_TRANSFER

### 复制（1 种模板）

- [揭晓时] → 复制敌方 {N}[诅咒]  —— PROLIFERATING_CURSE

### 添加（1 种模板）

- [被埋葬] → 添加自身到卡组中  —— SLIME

### 翻倍（1 种模板）

- [揭晓时] → 翻倍自身力量  —— UNFINISHED_ROBOT

