# 卡片描述与 GameEventListener Response 对应检查报告

**检查范围**：`Assets/Prefabs/Cards/3.0 no cost (current)` 下全部卡片

**检查逻辑**：
1. 将 `cardDesc` 拆分为独立触发段落。
2. 根据段落推断期望的触发事件与效果语义类别（如 `造成 X 伤害` → 伤害、`置顶友方` → 置顶友方等）。
3. 读取每个 `GameEventListener` 的触发事件及其 `Response` 实际调用的 `CostNEffectContainer`，并提取 `effectEvent` 方法。
4. 若某段描述的效果在对应事件的 Listener/Container 中找不到匹配的方法，则标记为问题。

## 摘要

| 项目 | 数量 |
|---|---|
| 检查卡片总数 | 78 |
| 无问题 | 67 |
| 存在疑似不匹配 | 11 |

## 疑似不匹配卡片

### 1. RELIC_GRAVE_LORD

**路径**：`Assets/Prefabs/Cards/4.0/2_Rare/RELIC_GRAVE_LORD.prefab`

**描述**：
```
被动:墓地中的友方生物攻击力+1
```

**未在描述中体现的 Listener**：

- `arm grave aura` (AfterShuffle)
  - ValueSetterEffect->SetIntSO(0)

### 2. EXILE_BERSERKER

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

### 3. FINAL_ESCORT

**路径**：`Assets/Prefabs/Cards/4.0/1_Uncommon/FINAL_ESCORT.prefab`

**描述**：
```
攻击;遗言:回合结束:置顶 <b>1</b> 友方攻击力最高生物
```

**描述段落与 Response 不匹配**：

| 描述段落 | 期望事件 | 期望效果 | 问题 | 实际 Container |
|---|---|---|---|---|
| 遗言:回合结束:置顶 1 友方攻击力最高生物 | OnMeBuried | STAGE_FRIENDLY | 效果类型不匹配 | arm round end escort (OnMeBuried): StageEffect->ArmRoundEndStageMaxAttackCreature |

**未在描述中体现的 Listener**：

- `round end stage creature` (OnRoundEnd)
  - StageEffect->StageMaxAttackCreatureIfArmed(0)

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

### 5. BURY

**路径**：`Assets/Prefabs/Cards/4.0/-1_Test/BURY.prefab`

**描述**：
```
揭晓时:埋葬 <b>1</b> 友方
```

**生物标记与绑定不一致（isCreature）**：

- 显式 `isCreature=1`，但绑定推导为 非生物（无攻击/伤害绑定）

### 6. DECIMATION

**路径**：`Assets/Prefabs/Cards/4.0/2_Rare/DECIMATION.prefab`

**描述**：
```
埋葬6友方,本回合每埋葬过1友方,埋葬数-1;攻击x3
```

**描述段落与 Response 不匹配**：

| 描述段落 | 期望事件 | 期望效果 | 问题 | 实际 Container |
|---|---|---|---|---|
| 埋葬6友方,本回合每埋葬过1友方,埋葬数-1 | OnMeRevealed | BURY_FRIENDLY | 效果类型不匹配 | decimate (OnMeRevealed): BuryEffect->BuryMyCards_CountBasedOnBuried<br>attack x3 (OnMeRevealed): AttackEffect->AttackTimes |

### 7. RELIC_CURSE_HASTE

**路径**：`Assets/Prefabs/Cards/4.0/2_Rare/RELIC_CURSE_HASTE.prefab`

**描述**：
```
被动:敌方诅咒攻击次数<b>+1</b>
```

**未在描述中体现的 Listener**：

- `curse times 1` (OnHostileCurseRevealed)
  - AttackTimesGiverEffect->GiveRevealedCurseAttackTimes(1)

### 8. DEATHBED_PORTER

**路径**：`Assets/Prefabs/Cards/4.0/1_Uncommon/DEATHBED_PORTER.prefab`

**描述**：
```
攻击;遗言:攻击;置顶1友方非生物
```

**描述段落与 Response 不匹配**：

| 描述段落 | 期望事件 | 期望效果 | 问题 | 实际 Container |
|---|---|---|---|---|
| 置顶1友方非生物 | OnMeRevealed | STAGE_FRIENDLY | 效果类型不匹配 | attack (OnMeRevealed): AttackEffect->Attack |

### 9. RELIC_TALLY

**路径**：`Assets/Prefabs/Cards/4.0/1_Uncommon/RELIC_TALLY.prefab`

**描述**：
```
被动:回合结束:本回合每埋葬 <b>1</b> 生物,强化 <b>1</b> 敌方[诅咒]
```

**描述段落与 Response 不匹配**：

| 描述段落 | 期望事件 | 期望效果 | 问题 | 实际 Container |
|---|---|---|---|---|
| 被动:回合结束:本回合每埋葬 1 生物,强化 1 敌方[诅咒] | OnMeRevealed | ENHANCE_CURSE | 缺少对应触发事件的 Listener | 无 |

**未在描述中体现的 Listener**：

- `tally enhance curses` (OnRoundEnd)
  - CurseEffect->EnhanceCurseTimes_BasedOnIntSO(0)

### 10. MILLBLADE

**路径**：`Assets/Prefabs/Cards/4.0/2_Rare/MILLBLADE.prefab`

**描述**：
```
攻击;每有1攻击力,埋葬卡组顶1卡
```

**描述段落与 Response 不匹配**：

| 描述段落 | 期望事件 | 期望效果 | 问题 | 实际 Container |
|---|---|---|---|---|
| 每有1攻击力,埋葬卡组顶1卡 | OnMeRevealed | BURY_NEXT | 效果类型不匹配 | attack (OnMeRevealed): AttackEffect->Attack<br>mill per atk (OnMeRevealed): BuryEffect->BuryNextXCards_BasedOnAttack |

### 11. COMBO_STARTER

**路径**：`Assets/Prefabs/Cards/4.0/1_Uncommon/COMBO_STARTER.prefab`

**描述**：
```
攻击;被强化:攻击次数<b>+1</b>
```

**描述段落与 Response 不匹配**：

| 描述段落 | 期望事件 | 期望效果 | 问题 | 实际 Container |
|---|---|---|---|---|
| 被强化:攻击次数+1 | OnMeGainedAttack | DAMAGE_OPPONENT | 效果类型不匹配 | react times 1 (OnMeGainedAttack): AttackTimesGiverEffect->GiveSelfAttackTimes |

---

**注意**：本报告基于关键词与方法名的语义匹配，复杂表述、多效果组合或特殊逻辑可能需要人工复核。
