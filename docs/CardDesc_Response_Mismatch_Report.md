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
| 检查卡片总数 | 88 |
| 无问题 | 88 |
| 存在疑似不匹配 | 0 |

## 结果

所有卡片的描述与 Listener Response 均匹配，未发现明显不匹配。
