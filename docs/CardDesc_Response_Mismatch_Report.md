# 卡片描述与 GameEventListener Response 对应检查报告

**检查范围**：`Assets/Prefabs/Cards/4.0` 下全部卡片

**检查逻辑**：
1. 将 `cardDesc` 拆分为独立触发段落。
2. 根据段落推断期望的触发事件与效果语义类别（如 `造成 X 伤害` → 伤害、`置顶友方` → 置顶友方等）。
3. 读取每个 `GameEventListener` 的触发事件及其 `Response` 实际调用的 `CostNEffectContainer`，并提取 `effectEvent` 方法。
4. 若某段描述的效果在对应事件的 Listener/Container 中找不到匹配的方法，则标记为问题。

## 摘要

| 项目 | 数量 |
|---|---|
| 检查卡片总数 | 110 |
| 无问题 | 91 |
| 存在疑似不匹配 | 19 |

## 疑似不匹配卡片

### 1. IncreaseDeckSizeLite

**路径**：`Assets/Prefabs/Cards/4.0/0_Common/IncreaseDeckSizeLite.prefab`

**描述**：
```
购买后卡位上限 <b>+1</b>,本卡即刻自我消耗(不入卡组);价格随本次冒险的已购次数递增,达上限后停售
```

**未在描述中体现的 Listener**：

- `Increase 1 deck size` (OnMeBought)
  - DeckSizeIncreaseEffect->IncreaseDeckSizeBy(1)

### 2. GRAVE_ROBBER

**路径**：`Assets/Prefabs/Cards/4.0/1_Uncommon/GRAVE_ROBBER.prefab`

**描述**：
```
复活1攻击力最高敌方;攻击力变为该卡攻击力
```

**生物标记与绑定不一致（isCreature）**：

- 显式 `isCreature=1`，但绑定推导为 非生物（无攻击/伤害绑定）

### 3. EXILE_BERSERKER

**路径**：`Assets/Prefabs/Cards/4.0/1_Uncommon/EXILE_BERSERKER.prefab`

**描述**：
```
攻击;本回合每放逐 <b>1</b> 友方,攻击次数<b>+1</b>
```

**描述段落与 Response 不匹配**：

| 描述段落 | 期望事件 | 期望效果 | 问题 | 实际 Container |
|---|---|---|---|---|
| 本回合每放逐 1 友方,攻击次数+1 | OnMeRevealed | EXILE_FRIENDLY | 效果类型不匹配 | attack (OnMeRevealed): AttackEffect->Attack |

**未在描述中体现的 Listener**：

- `react times 1` (OnFriendlyCardExiled)
  - AttackTimesGiverEffect->GiveSelfAttackTimes(1)

### 4. REANIMATOR

**路径**：`Assets/Prefabs/Cards/4.0/1_Uncommon/REANIMATOR.prefab`

**描述**：
```
攻击;攻击力=本回合复活友方数
```

**描述段落与 Response 不匹配**：

| 描述段落 | 期望事件 | 期望效果 | 问题 | 实际 Container |
|---|---|---|---|---|
| 攻击力=本回合复活友方数 | OnMeRevealed | REVIVE | 效果类型不匹配 | attack (OnMeRevealed): AttackEffect->Attack |

### 5. ELITE_REVIVER

**路径**：`Assets/Prefabs/Cards/4.0/0_Common/ELITE_REVIVER.prefab`

**描述**：
```
攻击;复活1被强化友方生物
```

**描述段落与 Response 不匹配**：

| 描述段落 | 期望事件 | 期望效果 | 问题 | 实际 Container |
|---|---|---|---|---|
| 复活1被强化友方生物 | OnMeRevealed | GIVE_ATTACK_FRIENDLY | 效果类型不匹配 | attack (OnMeRevealed): AttackEffect->Attack<br>revive enhanced (OnMeRevealed): ReviveEffect->ReviveMyCards |

### 6. BURY

**路径**：`Assets/Prefabs/Cards/4.0/-1_Test/BURY.prefab`

**描述**：
```
揭晓时:埋葬 <b>1</b> 友方
```

**生物标记与绑定不一致（isCreature）**：

- 显式 `isCreature=1`，但绑定推导为 非生物（无攻击/伤害绑定）

### 7. SNOWBALL

**路径**：`Assets/Prefabs/Cards/4.0/1_Uncommon/SNOWBALL.prefab`

**描述**：
```
揭晓时:攻击x2;强化反应:强化自身
    <b>1</b>
```

**描述段落与 Response 不匹配**：

| 描述段落 | 期望事件 | 期望效果 | 问题 | 实际 Container |
|---|---|---|---|---|
| 强化反应:强化自身 | OnMeRevealed | GIVE_ATTACK_SELF | 效果类型不匹配 | deal dmg (OnMeRevealed): AttackEffect->Attack |

**未在描述中体现的 Listener**：

- `gain double power` (OnMeGainedAttack)
  - AttackGainReactionEffect->GiveSelfAttack(1)

### 8. RELIC_ATTACK_BURIAL

**路径**：`Assets/Prefabs/Cards/4.0/1_Uncommon/RELIC_ATTACK_BURIAL.prefab`

**描述**：
```
被动:友方每次攻击时,埋葬卡组顶 <b>1</b> 卡
```

**描述段落与 Response 不匹配**：

| 描述段落 | 期望事件 | 期望效果 | 问题 | 实际 Container |
|---|---|---|---|---|
| 被动:友方每次攻击时,埋葬卡组顶 1 卡 | OnMeRevealed | BURY_NEXT | 缺少对应触发事件的 Listener | 无 |

**未在描述中体现的 Listener**：

- `friendly attack bury top 1` (onAnyFriendlyCardAttacked)
  - BuryEffect->BuryNextXCards(1)

### 9. DEATHBED_GRANT

**路径**：`Assets/Prefabs/Cards/4.0/1_Uncommon/DEATHBED_GRANT.prefab`

**描述**：
```
被动:友方生物被埋葬时:该友方生物攻击
```

**未在描述中体现的 Listener**：

- `buried creature strikes` (OnFriendlyCardBuried)
  - BuriedCreatureAttackEffect->AttackLastBuriedFriendlyCreature(0)

### 10. DECIMATION

**路径**：`Assets/Prefabs/Cards/4.0/2_Rare/DECIMATION.prefab`

**描述**：
```
埋葬6友方,本回合每埋葬过1友方,埋葬数-1;攻击x3
```

**描述段落与 Response 不匹配**：

| 描述段落 | 期望事件 | 期望效果 | 问题 | 实际 Container |
|---|---|---|---|---|
| 埋葬6友方,本回合每埋葬过1友方,埋葬数-1 | OnMeRevealed | BURY_FRIENDLY | 效果类型不匹配 | decimate (OnMeRevealed): BuryEffect->BuryMyCards_CountBasedOnAnyBuried<br>attack x3 (OnMeRevealed): AttackEffect->AttackTimes |

### 11. RELIC_RIFT_OVERRIDE

**路径**：`Assets/Prefabs/Cards/4.0/2_Rare/RELIC_RIFT_OVERRIDE.prefab`

**描述**：
```
被动:友方信徒效果变为:复活1敌方诅咒;放逐自身
```

**描述段落与 Response 不匹配**：

| 描述段落 | 期望事件 | 期望效果 | 问题 | 实际 Container |
|---|---|---|---|---|
| 被动:友方信徒效果变为:复活1敌方诅咒 | OnMeRevealed | REVIVE | 缺少对应触发事件的 Listener | 无 |
| 放逐自身 | OnMeRevealed | EXILE_SELF | 缺少对应触发事件的 Listener | 无 |

### 12. RELIC_CURSE_HASTE

**路径**：`Assets/Prefabs/Cards/4.0/2_Rare/RELIC_CURSE_HASTE.prefab`

**描述**：
```
被动:敌方诅咒攻击次数<b>+1</b>
```

**未在描述中体现的 Listener**：

- `curse times 1` (OnHostileCurseRevealed)
  - AttackTimesGiverEffect->GiveRevealedCurseAttackTimes(1)

### 13. DEATHBED_PORTER

**路径**：`Assets/Prefabs/Cards/4.0/1_Uncommon/DEATHBED_PORTER.prefab`

**描述**：
```
攻击;遗言:攻击;置顶1友方非生物
```

**描述段落与 Response 不匹配**：

| 描述段落 | 期望事件 | 期望效果 | 问题 | 实际 Container |
|---|---|---|---|---|
| 置顶1友方非生物 | OnMeRevealed | STAGE_FRIENDLY | 效果类型不匹配 | attack (OnMeRevealed): AttackEffect->Attack |

### 14. WEAPON_SPIRIT

**路径**：`Assets/Prefabs/Cards/4.0/2_Rare/WEAPON_SPIRIT.prefab`

**描述**：
```
被动:友方生物触发强化反应时:强化1该生物
```

**未在描述中体现的 Listener**：

- `amplify enhanced` (OnFriendlyCardGainedAttack)
  - AttackGiverEffect->GiveAttackToLastGainedAttack(1)

### 15. RELIC_BLOOD_PACT

**路径**：`Assets/Prefabs/Cards/4.0/2_Rare/RELIC_BLOOD_PACT.prefab`

**描述**：
```
被动:友方攻击不再造成伤害,而是强化等量敌方诅咒
```

**描述段落与 Response 不匹配**：

| 描述段落 | 期望事件 | 期望效果 | 问题 | 实际 Container |
|---|---|---|---|---|
| 被动:友方攻击不再造成伤害,而是强化等量敌方诅咒 | OnMeRevealed | DAMAGE_OPPONENT | 缺少对应触发事件的 Listener | 无 |

### 16. UNDYING_WARRIOR

**路径**：`Assets/Prefabs/Cards/4.0/1_Uncommon/UNDYING_WARRIOR.prefab`

**描述**：
```
攻击;强化反应:复活自身
```

**描述段落与 Response 不匹配**：

| 描述段落 | 期望事件 | 期望效果 | 问题 | 实际 Container |
|---|---|---|---|---|
| 强化反应:复活自身 | OnMeRevealed | REVIVE | 效果类型不匹配 | attack (OnMeRevealed): AttackEffect->AttackTimes |

**未在描述中体现的 Listener**：

- `gained attack revive self` (OnMeGainedAttack)
  - ReviveEffect->ReviveSelf(0)

### 17. MILLBLADE

**路径**：`Assets/Prefabs/Cards/4.0/2_Rare/MILLBLADE.prefab`

**描述**：
```
攻击;每有1攻击力,埋葬卡组顶1卡
```

**描述段落与 Response 不匹配**：

| 描述段落 | 期望事件 | 期望效果 | 问题 | 实际 Container |
|---|---|---|---|---|
| 每有1攻击力,埋葬卡组顶1卡 | OnMeRevealed | BURY_NEXT | 效果类型不匹配 | attack (OnMeRevealed): AttackEffect->Attack<br>mill per atk (OnMeRevealed): BuryEffect->BuryNextXCards_BasedOnAttack |

### 18. COMBO_STARTER

**路径**：`Assets/Prefabs/Cards/4.0/1_Uncommon/COMBO_STARTER.prefab`

**描述**：
```
攻击;强化反应:攻击次数<b>+1</b>
```

**未在描述中体现的 Listener**：

- `react times 1` (OnMeGainedAttack)
  - AttackTimesGiverEffect->GiveSelfAttackTimes(1)

### 19. MASS_SACRIFICE

**路径**：`Assets/Prefabs/Cards/4.0/2_Rare/MASS_SACRIFICE.prefab`

**描述**：
```
埋葬所有友方;每埋葬1友方,生成1信徒
```

**描述段落与 Response 不匹配**：

| 描述段落 | 期望事件 | 期望效果 | 问题 | 实际 Container |
|---|---|---|---|---|
| 埋葬所有友方 | OnMeRevealed | BURY_FRIENDLY | 效果类型不匹配 | sacrifice and spawn (OnMeRevealed): MassSacrificeEffect->SacrificeAllThenSpawnBelievers |
| 每埋葬1友方,生成1信徒 | OnMeRevealed | BURY_FRIENDLY | 效果类型不匹配 | sacrifice and spawn (OnMeRevealed): MassSacrificeEffect->SacrificeAllThenSpawnBelievers |

---

**注意**：本报告基于关键词与方法名的语义匹配，复杂表述、多效果组合或特殊逻辑可能需要人工复核。
