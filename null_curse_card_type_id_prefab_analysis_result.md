# Prefab 分析报告

## 概述

共分析 `Assets/Prefabs/Cards/3.0 no cost (current)` 文件夹中的 **38** 个 prefab 文件。

---

## 一、Prefab 子Object结构

### Bury and buried/DeathRattle/ (7个)

| 卡片名称 | 子Object层级 |
|---------|-------------|
| **不稳定传送门** | L-- bury hostile<br>L-- stage friendly<br>L-- "不稳定传送门" |
| **冥界裂缝** | L-- stage friendly<br>L-- stage friendly (1)<br>L-- "冥界裂缝" |
| **史莱姆** | L-- deal dmg<br>L-- add a copy of self<br>L-- add counter<br>L-- "史莱姆" |
| **复仇者** | L-- deal dmg<br>L-- gain power<br>L-- "复仇者" |
| **殉道者** | L-- give all friendly power<br>L-- "殉道者" |
| **针刺骷髅** | L-- deal dmg<br>L-- deal dmg (1)<br>L-- "针刺骷髅" |
| **骷髅士兵** | L-- deal dmg<br>L-- stage self<br>L-- "骷髅士兵" |

### Bury and buried/ (7个)

| 卡片名称 | 子Object层级 |
|---------|-------------|
| **不愚蠢的埋葬** | L-- 'linger: apply 2 power to 2 next cards'<br>L-- "不愚蠢的埋葬" |
| **人间大炮** | L-- deal 3 dmg x friendly after start card<br>L-- bury 12 friendly cards<br>L-- "人间大炮" |
| **冥界邀请** | L-- deal dmg and bury hostile based on friendly in grave<br>L-- "冥界邀请" |
| **同路人** | L-- bury hostile<br>L-- bury friendly<br>L-- "同路人" |
| **守墓人** | L-- deal dmg<br>L-- stage self<br>L-- "守墓人" |
| **尸爆** | L-- deal dmg<br>L-- bury<br>L-- deal dmg (1)<br>L-- "尸爆" |
| **怨恨** | L-- 'linger: apply 2 power to 2 next cards'<br>L-- "怨恨" |

### Conjure/ (11个)

| 卡片名称 | 子Object层级 |
|---------|-------------|
| **临终诅咒** | L-- 'if in grave: hostile [curse] + power'<br>L-- "临终诅咒" |
| **坠入裂缝** | L-- bury hostile<br>L-- "坠入裂缝" |
| **次元兽** | L-- deal dmg<br>L-- "次元兽" |
| **次元吞噬者** | L-- deal dmg<br>L-- gain power<br>L-- "次元吞噬者" |
| **次元引导者** | L-- exile rift and bury hostile<br>L-- "次元引导者" |
| **次元棺材** | L-- bury 1 hostile<br>L-- "次元棺材" |
| **次元虫** | L-- 'add 1 [rift]'<br>L-- "次元虫" |
| **次元裂缝** | L-- stage friendly & exile self<br>L-- "次元裂缝" |
| **次元龙** | L-- deal dmg<br>L-- "次元龙" |
| **献祭仪式** | L-- 'bury friendly and add [rift]'<br>L-- "献祭仪式" |
| **裂缝召唤师** | L-- stage friendly<br>L-- "裂缝召唤师" |

### Curse/ (12个)

| 卡片名称 | 子Object层级 |
|---------|-------------|
| **咒食的萨满** | L-- "咒食的萨满"<br>L-- apply power to friendly = hostile curse power |
| **咒食的野兽** | L-- "咒食的野兽"<br>L-- deal dmg<br>L-- stage self |
| **增殖的厄运** | L-- "增殖的厄运"<br>L-- copy 1 hostile curse |
| **巫师** | L-- "巫师"<br>L-- enhence hostile [ju-on], deal dmg |
| **异次元的诅咒** | L-- "异次元的诅咒"<br>L-- enhance hostile [ju-on], stage friendly |
| **拔苗助长** | L-- "拔苗助长"<br>L-- consume 1 hostile [ju-on] power, stage it |
| **献祭精灵** | L-- "献祭精灵"<br>L-- bury friendly; enhance hostile curse |
| **被诅咒的尸体** | L-- "被诅咒的尸体"<br>L-- enhence hostile [ju-on], deal dmg multiple times |
| **被诅咒的骷髅** | L-- "被诅咒的骷髅"<br>L-- enhance hostile curse for each friendly in grave |
| **诅咒** | L-- "诅咒"<br>L-- deal dmg to self based on power |
| **诅咒召唤师** | L-- "诅咒召唤师"<br>L-- enhance hostile [ju-on], stage friendly |
| **飞蛾人** | L-- "飞蛾人"<br>L-- stage friendly |

### General/ (1个)

| 卡片名称 | 子Object层级 |
|---------|-------------|
| **铁匠** | L-- deal dmg<br>L-- apply power to friendly<br>L-- "铁匠" |

---

## 二、CursedCardTypeID 为 NULL 的 CostNEffectContainer 组件

共 **27** 个组件的 `cursedCardTypeID` 为 null。

### Bury and buried/DeathRattle/

| 卡片 | GameObject | GO ID | Comp ID |
|------|-----------|-------|---------|
| 不稳定传送门 | bury hostile | 46887848 | 28747679 |
| 冥界裂缝 | stage friendly | 69671618 | 23219885 |
| 史莱姆 | deal dmg | 27730282 | 48952256 |
| 复仇者 | deal dmg | 28183383 | 14059765 |
| 殉道者 | give all friendly power | 28183383 | 14059765 |
| 针刺骷髅 | deal dmg | 28183383 | 14059765 |
| 骷髅士兵 | deal dmg | 28183383 | 14059765 |

### Bury and buried/

| 卡片 | GameObject | GO ID | Comp ID |
|------|-----------|-------|---------|
| 不愚蠢的埋葬 | 'linger: apply 2 power to 2 next cards' | 56790325 | 88717014 |
| 人间大炮 | deal 3 dmg x friendly after start card | 28183383 | 14059765 |
| 冥界邀请 | deal dmg and bury hostile based on friendly in grave | 56790325 | 88717014 |
| 同路人 | bury hostile | 46887848 | 28747679 |
| 守墓人 | deal dmg | 28183383 | 14059765 |
| 尸爆 | deal dmg | 96543339 | 81209046 |
| 怨恨 | 'linger: apply 2 power to 2 next cards' | 56790325 | 88717014 |

### Conjure/

| 卡片 | GameObject | GO ID | Comp ID |
|------|-----------|-------|---------|
| 临终诅咒 | 'if in grave: hostile [curse] + power' | 28183383 | 14059765 |
| 坠入裂缝 | bury hostile | 28183383 | 14059765 |
| 次元兽 | deal dmg | 28183383 | 14059765 |
| 次元吞噬者 | deal dmg | 28183383 | 14059765 |
| 次元引导者 | exile rift and bury hostile | 28183383 | 14059765 |
| 次元棺材 | bury 1 hostile | 28183383 | 14059765 |
| 次元虫 | 'add 1 [rift]' | 28183383 | 14059765 |
| 次元裂缝 | stage friendly & exile self | 28183383 | 14059765 |
| 次元龙 | deal dmg | 28183383 | 14059765 |
| 献祭仪式 | 'bury friendly and add [rift]' | 28183383 | 14059765 |
| 裂缝召唤师 | stage friendly | 28183383 | 14059765 |

### Curse/

| 卡片 | GameObject | GO ID | Comp ID |
|------|-----------|-------|---------|
| 诅咒 | "诅咒" | 87896563 | 63894489 |

### General/

| 卡片 | GameObject | GO ID | Comp ID |
|------|-----------|-------|---------|
| 铁匠 | deal dmg | 28183383 | 14059765 |

---

## 三、说明

### CursedCardTypeID 为 null 的原因

- **Curse/文件夹中大部分卡片**（11个）的 `cursedCardTypeID` 都**已设置**，因此不在 null 列表中
- **只有"诅咒"卡片**的 `cursedCardTypeID` 为 null，因为它是**被诅咒的卡**（受害者），而不是去诅咒别人的卡
- 其他卡片（Bury and buried、Conjure、General）因为不涉及诅咒机制，所以该字段为 null 是正常的

### 组件ID重复说明

注意有些 GameObject 的组件ID（Comp ID）相同（如 `14059765`），这是因为这些卡片使用了相同的 prefab 模板或共享组件引用。
