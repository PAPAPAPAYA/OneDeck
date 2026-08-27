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
| 检查卡片总数 | 90 |
| 无问题 | 89 |
| 存在疑似不匹配 | 1 |

## 疑似不匹配卡片

### 1. ZOMBIE

**路径**：`Assets/Prefabs/Cards/3.0 no cost (current)/_DONT INCLUDE/_Default cards/ZOMBIE.prefab`

**描述**：
```

```

**未在描述中体现的 Listener**：

- `deal dmg (1)` (OnMeRevealed)
  - AttackEffect->Attack(0)

---

**注意**：本报告基于关键词与方法名的语义匹配，复杂表述、多效果组合或特殊逻辑可能需要人工复核。
