# OneDeck 卡片库独立方法整理

基于 Obsidian 卡片库中 72 张非系统卡片的效果描述提取。
规则：**方法是独立的触发器、动作或条件，不是组合。**
格式规则：**方法文本中不含空格。**

## 一、触发器方法（14 种）

- **揭晓时**  —— 使用卡片：ALL_FOR_ONE, ALMIGHTY, ANTI_CREATURE_WEAPON, AVENGER, BLACKSMITH, BLIND_COMBAT_PRIEST, BODY_CANON, BONE_COMBINATION, BOOSTER, COFFIN_MAKER 等61张
- **被埋葬**  —— 使用卡片：AVENGER, CONFUSED_PORTALMANCER, CURSED_CORPSE, GRAVE_PORTAL, MARTYR, SCAPEGOAT, SLIME, SNATCHER, SOLDIER_SKELETON, SPIKE_SKELETON 等11张
- **被置顶**  —— 使用卡片：GOBLIN_ASSASSIN_TEAM, GOBLIN_CHARGE_TEAM, POWER_SURGE, RIFT_DRAGON, SNATCHER, TACTICAL_BREACHER, WISE_BURIAL
- **当友方被去除时**  —— 使用卡片：DEATHBED_CURSE, RIFT_COFFIN, RIFT_DEVOURER
- **当敌人受到伤害时**  —— 使用卡片：CURSE_ENCHANTMENT, ETERNAL_GHOST
- **每揭晓**2<counter>**次**  —— 使用卡片：ADVANCE_PORTAL
- **洗牌后**  —— 使用卡片：BOOSTER
- **友方被埋葬**  —— 使用卡片：CORPSE_CANON
- **当敌方[诅咒]揭晓时**  —— 使用卡片：CURSE_THIRST_BEAST
- **当卡被埋葬时**  —— 使用卡片：GRAVE_KEEPER
- **当敌方[诅咒]获得力量时**  —— 使用卡片：MOTH_MAN
- **获得力量时**  —— 使用卡片：POWER_CRAVER
- **再有**3<counter>**敌人被揭晓**  —— 使用卡片：QUICK_RESPONSE_PROTOCOL
- **当友方获得力量时**  —— 使用卡片：WEAPON_SPIRIT

## 二、动作方法（25 种）

- **造成{N}伤害**  —— 使用卡片：ALMIGHTY, AVENGER, BLACKSMITH, COFFIN_MAKER, CORPSE_CANON, CURSE_THIRST_BEAST, ETERNAL_GHOST, GOBLIN_ASSASSIN_TEAM 等26张
- **置顶{N}友方**  —— 使用卡片：ADVANCE_PORTAL, ALMIGHTY, BOOSTER, CURSE_SUMMONER, CURSE_THIRST_SUMMONER, DR_MANHATTAN, GRAVE_PORTAL, MOTH_MAN 等14张
- **埋葬{N}敌方**  —— 使用卡片：ALMIGHTY, ANTI_CREATURE_WEAPON, COFFIN_MAKER, DR_MANHATTAN, FALL_INTO_RIFT, GOBLIN_ASSASSIN_TEAM, GRAVE_INVITATION, GRAVE_PORTAL 等12张
- **给予友方{N}力量**  —— 使用卡片：ALMIGHTY, AVENGER, BLACKSMITH, CURSE_THIRST_SHAMAN, ELDER_SORCERER, MARTYR, POWER_SURGE, POWER_TRANSFER 等12张
- **增强敌方[诅咒]{N}**  —— 使用卡片：ALMIGHTY, CURSED_CORPSE, CURSED_SKELETON, CURSE_ENCHANTMENT, DETERIORATION, POISONER, RIFT_CURSE, SACRIFICIAL_CURSE 等10张
- **埋葬{N}友方**  —— 使用卡片：BOOSTER, CONFUSED_PORTALMANCER, CORPSE_CANON, GRAVE_PUNCH, GRAVE_TOGETHER, SACRIFICE_RITUAL, SACRIFICIAL_CURSE, SACRIFICIAL_SWORD 等9张
- **生成{N}[次元裂缝]**  —— 使用卡片：ALMIGHTY, CONFUSED_PORTALMANCER, FALL_INTO_RIFT, RIFT_CURSE, RIFT_DRAGON, RIFT_INSECT, SACRIFICE_RITUAL
- **造成{N}伤害 x{M}**  —— 使用卡片：BODY_CANON, CURSED_CORPSE, GRAVE_PUNCH, POWER_SIPHONER, SPIKE_SKELETON
- **给予下{X}卡{N}力量**  —— 使用卡片：BLIND_COMBAT_PRIEST, CURSE_THIRST_SUMMONER, MAD_SCIENTIST
- **置顶自身**  —— 使用卡片：CURSE_THIRST_BEAST, GRAVE_KEEPER, SOLDIER_SKELETON
- **埋葬后{N}卡**  —— 使用卡片：LARGE_SCALE_DEATH, SMALL_SCALE_DEATH
- **力量倍化{N}**  —— 使用卡片：POWER_CRAVER, UNFINISHED_ROBOT
- **造成所有卡的力量数量的伤害**  —— 使用卡片：ALL_FOR_ONE
- **造成{N}伤害 x 本回合被埋葬的敌方数量**  —— 使用卡片：BONE_COMBINATION
- **将所有友方的力量(排除友方[诅咒])转移到敌方的[诅咒]**  —— 使用卡片：CROW_CROWD
- **给予敌方{N}力量**  —— 使用卡片：DEATHBED_CURSE
- **造成友方数量的伤害**  —— 使用卡片：FLESH_COMBINATION
- **转移所有友方的{N}力量到自身**  —— 使用卡片：POWER_SIPHONER
- **减少敌方{N}力量{N}张**  —— 使用卡片：POWER_TRANSFER
- **置顶敌方[诅咒]**  —— 使用卡片：PREMATURE
- **生成[诅咒]{N}**  —— 使用卡片：PROLIFERATING_CURSE
- **增强自身[诅咒]{N}**  —— 使用卡片：SIDE_EFFECT_PORTAL
- **添加自身到卡组中**  —— 使用卡片：SLIME
- **置顶力量最多的敌方**  —— 使用卡片：THE_FOOL
- **埋葬{N}友方[亡语]/[萦绕]**  —— 使用卡片：WISE_BURIAL

## 三、前置条件方法（消耗/去除类，3 种）

- **去除{N}[次元裂缝]**  —— 使用卡片：RIFT_GUIDE, RIFT_MONSTER, RIFT_SUMMONER
- **消耗敌方[诅咒]{N}力量**  —— 使用卡片：CURSE_SUMMONER, CURSE_THIRST_SUMMONER
- **消耗{N}力量**  —— 使用卡片：DR_MANHATTAN

## 四、循环条件方法（3 种）

- **墓地每有{N}友方**  —— 使用卡片：BODY_CANON, CURSED_SKELETON, GRAVE_INVITATION
- **敌方[诅咒]每有{N}力量**  —— 使用卡片：CURSE_THIRST_SHAMAN, DETERIORATION
- **本回合每置顶过{N}友方**  —— 使用卡片：ELDER_SORCERER

## 五、示例卡片方法清单

### BODY_CANON
- 揭晓时
- 造成{N}伤害 x{M}
- 墓地每有{N}友方

### CURSE_THIRST_BEAST
- 当敌方[诅咒]揭晓时
- 置顶自身
- 揭晓时
- 造成{N}伤害

### ALMIGHTY
- 揭晓时
- 造成{N}伤害
- 置顶{N}友方
- 埋葬{N}敌方
- 给予友方{N}力量
- 生成{N}[次元裂缝]
- 增强敌方[诅咒]{N}

### COFFIN_MAKER
- 揭晓时
- 造成{N}伤害
- 埋葬{N}敌方

