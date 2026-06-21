# 卡片描述与 GameEventListener 对应检查报告

**检查范围**：`Assets/Prefabs/Cards/3.0 no cost (current)` 下全部卡片

**检查逻辑**：
1. 将 `cardDesc` 按 `;` 或换行拆分为独立触发段落。
2. 根据段落中的中文关键词推断期望的 GameEvent（如 `揭晓时` → `OnMeRevealed`、`被埋葬` → `OnMeBuried` 等）。
3. 对比卡片上实际挂载的 `GameEventListener` 所订阅的事件。
4. 若某段落期望的事件未在 Listener 中出现，或 Listener 中的事件未被描述覆盖，则标记为问题。

## 摘要

| 项目 | 数量 |
|---|---|
| 检查卡片总数 | 88 |
| 无问题 | 86 |
| 存在疑似不匹配 | 2 |

## 疑似不匹配卡片

### 1. POWER_SURGE

**路径**：`Assets/Prefabs/Cards/3.0 no cost (current)/General/1_Uncommon/POWER_SURGE.prefab`

**描述**：
```
揭晓时:造成 <b>4</b> 伤害;
被置顶:给予 <b>2</b> 友方 <b>1</b> 力量
```

**实际 Listener**：
- `OnMeRevealed` → ``
- `OnMeRevealed` → ``

**描述段落缺少对应 Listener**：

| 描述段落 | 期望事件 | 实际事件 |
|---|---|---|
| 被置顶:给予 <b>2</b> 友方 <b>1</b> 力量 | OnMeStaged | OnMeRevealed |

### 2. ELDER_SORCERER

**路径**：`Assets/Prefabs/Cards/3.0 no cost (current)/General/2_Rare/ELDER_SORCERER.prefab`

**描述**：
```
揭晓时,本回合每置顶过 <b>1</b> 友方:给予 <b>1</b> 友方 <b>1</b> 力量
```

**实际 Listener**：
- `OnMeRevealed` → ``
- `OnMeGotStatusEffect` → ``

**Listener 事件未被描述覆盖**：

- `OnMeGotStatusEffect` → ``

---

**注意**：本报告基于关键词匹配，部分语义特殊或表述省略的卡片可能需要人工复核。
