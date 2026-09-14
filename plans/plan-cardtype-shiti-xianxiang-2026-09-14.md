# Plan: cardDesc 谓词改名(生物→实体/非生物→现象)+ hover 卡片类型显示 + CardType.Token

> 2026-09-14 · 状态:**八项已拍板,待「修改代码」**
> 依据:v3 世界观 `docs/OneDeck_Worldview_Naming_v3_CultPulp.md` §1 已把生物定义为「实体」、非生物定义为「现象」;本次让 desc 谓词与世界观词汇对齐,并新增 token衍生物 类型收纳信徒/诅咒 token。

## 0. 拍板记录(2026-09-14)

| # | 决定 |
|---|---|
| Q1 | 只动 4.0 活跃池 25 张;3.0 退役池(ANTI_CREATURE_WEAPON)不动 |
| Q2 | 信徒 token、诅咒 token **不显示**卡片类型;只有实体/现象显示 |
| Q3 | Start Card 同样不显示卡片类型;其余所有 face-up 卡 hover 弹类型 |
| Q4 | 解释文案定稿:「可攻击」/「不可攻击」 |
| Q5 | 文案走 SO(对齐 TagTooltipDatabaseSO 单一来源模式) |
| Q6 | Notion desc 回写**本次不做**(25 行 prefab↔DB desc 漂移=已知欠账,dbsync 工具会报) |
| Q7 | 代码注释「生物/非生物」批改纳入 |
| Q8 | **新增 `CardType.Token`(token衍生物),RIFT 信徒 + JU_ON 诅咒并入;hover 不显示类型。落地形=Status 标识符改名 Token(并桶),非追加新值** |

## 1. Part 1:prefab cardDesc 批量替换(25 张,4.0 池)

替换规则(顺序敏感):先 `非生物`→`现象`,再 `生物`→`实体`;只动 cardDesc 行;裸写指代规范不变(信徒/诅咒照旧)。

| # | 卡 | 改动片段(→ 后为改后) |
|---|---|---|
| 1 | BEAST_REVIVER | 复活 1 生物友方 → 复活 1 **实体**友方 |
| 2 | BLACKSMITH_4.0 | 强化 1 友方生物 → 友方**实体** |
| 3 | ELITE_REVIVER | 被强化过的友方生物 → 友方**实体** |
| 4 | LAST_GIFT | 强化 2 友方生物 → 友方**实体** |
| 5 | SPIRIT_CALLER | 复活 1 友方非生物 → 友方**现象** |
| 6 | WAR_TRAINER | 强化 2 友方生物 → 友方**实体** |
| 7 | BATTLE_HORN | 友方生物攻击次数+1 → 友方**实体** |
| 8 | COMBO_GRANTER | 1 友方生物攻击次数+1 → 友方**实体** |
| 9 | CURSE_THIRST_SHAMAN_4.0 | 强化 1 友方生物 → 友方**实体** |
| 10 | DEATHBED_GRANT | 友方生物被埋葬…该友方生物攻击 → 两处**实体** |
| 11 | DEATHBED_PORTER | 置顶 1 友方非生物 → 友方**现象** |
| 12 | FINAL_ESCORT | 复活 1 墓地最高攻友方生物 → 友方**实体** |
| 13 | FLURRY_REVIVER | 攻击次数最多友方生物 → 友方**实体** |
| 14 | GRAVE_PUPPETEER | 墓地 1 友方生物攻击 → 友方**实体** |
| 15 | RELIC_TALLY | 每埋葬 1 生物 → 埋葬 1 **实体** |
| 16 | RELIC_TRAINER | 攻击力最低友方生物 → 友方**实体** |
| 17 | RELIC_WHITE_BANNER | 友方攻击力最高生物 → 攻击力最高**实体** |
| 18 | RIFT_GUIDE | 埋葬 2 敌方非生物 → 敌方**现象** |
| 19 | RIFT_PRIEST | 生成 2 信徒;强化 1 友方生物 → 友方**实体** |
| 20 | RIFT_REVIVER | 复活 2 友方非生物 → 友方**现象** |
| 21 | UTILITY_CREATURES_1 | 只掷出生物卡 → **实体**卡 |
| 22 | UTILITY_SPELLS_1 | 只掷出非生物卡 → **现象**卡 |
| 23 | WEAKENING_FIELD | 所有生物 → 所有**实体** |
| 24 | RELIC_GRAVE_LORD | 墓地中的友方生物攻击力+1 → 友方**实体** |
| 25 | RIFT_REAPER | 强化 1 友方生物 → 友方**实体** |

## 2. Part 2:CardType.Token(token衍生物)

### 2.1 枚举落地形:Status 标识符改名 Token(并桶)

`EnumStorage.CardType` → `{ None, Creature, Token }`(C# 标识符 `Status`→`Token`;**序列化值 2 不动**,append-only 的目的=序列化值稳定,不破;属槽位语义演化,以本 plan 留痕)。`CardType.Status` 的代码消费点已全部核实(§5):仅 2 个测试文件 + 3 处 filter 比对,跟随改名即可。

同步改名(均为"仅标识符、序列化 int 不动"):
- `EffectScript.EffectCreatureFilter.Status` → `.Token`(**全 prefab 零消费者**,`creatureFilter: 3` 全库 0 次出现,零风险);
- `ReviveEffect.CreatureFilter.Status` → `.Token`(同上);
- 相关注释(EffectScript.cs:13、ReviveEffect.cs:20、EnumStorage.cs:62、CardScript.cs:78-83)。

prefab 数据变化仅 1 行:**RIFT.prefab `cardType: 0 → 2`**;JU_ON.prefab 免改(已是 2)。敌方诅咒副本=AddTempCard 按 cardTypeID 复制 JU_ON,类型自动跟随。

### 2.2 攻击显示规则改写(唯一真回归点)

现状 `HasAttackDisplay => cardType != None || HasAttackAttribute` 依赖"Status 恒显示"(JU_ON 0 攻显示=2026-09-02 裁定)。信徒并入 Token 后按类型恒显示会让信徒卡面冒 0 攻。改写:

```csharp
public bool alwaysShowAttack = false;   // 新字段,JU_ON.prefab 置 true(承载 09-02 裁定)
public bool HasAttackDisplay => cardType == EnumStorage.CardType.Creature
	|| HasAttackAttribute || alwaysShowAttack;
```

对现存 None/Creature 卡行为逐一等价;JU_ON 显示不变;信徒不显示(维持现状)。

### 2.3 hover 类型行(CardTagTooltip)

在 `CardTagTooltip.BuildTooltipText` 最前面拼类型块,样式与 tag 块一致。**显示门禁**:

1. `cardType == Token` → 不显示(Q2;数据驱动:SO 不配 Token 条目即天然排除,日后想开=只加资产);
2. `card.isStartCard` → 不显示(Q3);
3. 其余按类型查 `CardTypeTooltipDatabaseSO`,查不到条目→无块(无 tag 则 tooltip 不弹,保留现有早退语义)。

| CardType | 显示 | 解释行 |
|---|---|---|
| Creature | `[实体]` | 可攻击 |
| None | `[现象]` | 不可攻击 |
| Token | (SO 无条目) | — 不显示 |

既有 hover 规则全不动:0.1s 延迟、face-down 不弹、换阶段/翻面/销毁强隐。上一版方案的 `isTokenCard` bool **废弃不再需要**。

### 2.4 CardTypeTooltipDatabaseSO

- 新 SO 类型,懒加载单例 `Me`,模式对齐 `TagTooltipDatabaseSO`:`List<Entry> { EnumStorage.CardType type; StringSO displayName; StringSO description; }`。
- 资产:`Assets/Resources/CardTypeTooltipDatabase.asset`;StringSO 放 `Assets/SORefs/Strings/CardTypeNames/`(实体/现象)与 `Assets/SORefs/Strings/CardTypeTooltips/`(可攻击/不可攻击),全部 `reset = false`。
- 只配 Creature/None 两条;显示名查不到回退枚举名。

## 3. Part 3:文档与注释同步(Q6 Notion 不做)

1. **词表文档**:`4.0_Glossary.md`(谓词行「生物 / 非生物」→「实体 / 现象」,数据库字段表「生物」行改口径)+ `CardDesc_TagReference_Convention_v2.md`(行 19 谓词列表、行 80 WEAKENING_FIELD 示例改新文)。
2. **AGENTS.md**:CardType 条目改写(`{None, Creature, Token}`,Status→Token 演化留痕,注意 32KB 限额、改后跑 `wc -c`)。
3. **代码注释批改**(Q7):「生物/非生物」→「实体/现象」+ Status→Token 相关注释,零风险。
4. **测试跟随**:`EnemyDamagingTargetFilterTests.cs:115`、`Step5BatchBEngineTests.cs:255` 的 `CardType.Status`→`CardType.Token`。
5. **Notion 4.0 DB desc 暂不动**(Q6,已知欠账)。

## 4. 实施步骤(待「修改代码」)

1. Python 批量替换 25 张 prefab cardDesc(非生物先行)+ grep 验证 4.0 池 0 残留。
2. `CardType.Status`→`Token` 标识符改名(EnumStorage + EffectScript + ReviveEffect + 2 测试文件 + 注释)。
3. `CardScript.alwaysShowAttack` 字段 + HasAttackDisplay 规则改写;JU_ON.prefab 置 true;RIFT.prefab cardType 0→2。
4. `CardTypeTooltipDatabaseSO` + Resources 资产 + 4 个 StringSO 资产;`CardTagTooltip` 门禁与类型块。
5. Glossary / Convention v2 / AGENTS.md 文档更新 + 其余注释批改。
6. 验证:csproj 离线编译 0 错;EditMode 套件受影响用例改后重跑(不主动跑 Play Mode);hover 观感由你 Play 截图确认。

## 5. 现状事实备忘(2026-09-14 核实)

- desc 替换纯文本零耦合:全库无运行时字符串匹配「生物」;谓词走代码池/selector;`<tag:X>` 占位系统只认 `tag为[...]` 短语。
- **`CardType.Status` 消费点全清单**:测试 2 处(EnemyDamagingTargetFilterTests:115、Step5BatchBEngineTests:255,均为赋值)+ filter 比对 3 处(BuryEffect:169、StageEffect:333、ReviveEffect:112,均为 `==` 读)+ 注释;**无运行时赋值点,无其他分支**。
- **Status 选择器零 prefab 消费者**:`creatureFilter: 3` 全库 0 次;实际诅咒选择=`typeIDFilter: JU_ON` ×3(CURSE_REVIVER/CURSE_GARDENER/CURSE_SUMMONER),typeID 字符串与 cardType 无关。
- **`cardType: 2` 全库唯一=JU_ON**;信徒 RIFT(ct=0,cardTypeID=RIFT,路径 `Assets/Prefabs/Cards/3.0 no cost (current)/_DONT INCLUDE/Token/`);敌方诅咒副本=AddTempCard 按 cardTypeID 复制 JU_ON(类型自动跟随)。
- `HasAttackDisplay` 是唯一受 None→Token 迁移影响的逻辑(全库 `CardType.None` 显式消费仅此一处);JU_ON 的 0 攻显示为 2026-09-02 裁定,由 `alwaysShowAttack` 承载。
- 3.0 退役池 ANTI_CREATURE_WEAPON 含「非生物」,按 Q1 不动。
