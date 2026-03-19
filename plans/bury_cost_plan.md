# Bury Cost 实现计划

## 概述

新增一种 cost 类型——**Bury Cost**：卡牌发动前，根据 cost 数值，将对应数量的己方卡置底。

## 设计思路

### 与现有 Cost 系统的对比

| Cost 类型 | 执行时机 | 实现方式 | 说明 |
|-----------|----------|----------|------|
| Mana Cost | `checkCostEvent` | `CheckCost_Mana(n)` | 检查型：只检查条件 |
| Revive Cost | `checkCostEvent` | `CheckCost_Revive(n)` | 检查型：只检查条件 |
| Rest Cost | `checkCostEvent` | `CheckCost_Rested()` | 检查型：消耗状态 |
| Token Cost | `preEffectEvent` | `TokenCostEffect` | 执行型：实际消耗卡牌 |
| **Bury Cost** | `preEffectEvent` | `BuryCostEffect` | 执行型：实际移动卡牌 |

### 为什么选择 preEffectEvent？

1. **需要实际移动卡牌**：bury cost 需要从牌组中移除卡牌并置底，这是实际操作而非简单检查
2. **与 Token Cost 一致**：Token Cost 也在 `preEffectEvent` 中执行，模式统一
3. **支持失败回滚**：如果己方卡不足，可以通过 `SetCostNotMet` 阻止后续效果发动

## 实现步骤

### 1. 在 CardScript 中添加字段

**文件**: `Assets/Scripts/Card/CardScript.cs`

```csharp
[Header("Bury Cost")]
[Tooltip("发动时，将N张己方卡置底")]
public int buryCost;
```

### 2. 创建 BuryCostEffect 脚本

**文件**: `Assets/Scripts/Effects/BuryCostEffect.cs`

核心逻辑：
- 继承 `EffectScript`
- 实现 `ExecuteBuryCost()` 方法
- 从 `combinedDeckZone` 收集己方卡（排除当前卡）
- 检查数量是否足够
- 不足时调用 `SetCostNotMet` 阻止效果
- 足够时随机选择并置底
- 使用 `PlayStageBuryAnimation` 播放动画

### 3. 更新 AGENTS.md 文档

在 Cost 类型表格中添加 Bury Cost 说明。

## 详细实现

### BuryCostEffect.cs 完整代码

```csharp
using System.Collections.Generic;
using DefaultNamespace.Managers;
using UnityEngine;

public class BuryCostEffect : EffectScript
{
    public void ExecuteBuryCost()
    {
        int costCount = myCardScript.buryCost;
        
        if (costCount <= 0) return;

        var combinedDeck = combatManager.combinedDeckZone;

        // 收集己方卡（排除当前正在发动的卡）
        var eligibleCards = new List<GameObject>();
        foreach (var card in combinedDeck)
        {
            if (card == null) continue;
            
            var cardScript = card.GetComponent<CardScript>();
            if (cardScript == null) continue;
            
            // 排除当前正在发动的卡
            if (card == myCard) continue;
            
            // 只收集己方卡
            if (cardScript.myStatusRef != myCardScript.myStatusRef) continue;
            
            eligibleCards.Add(card);
        }

        // 检查是否有足够的己方卡
        if (eligibleCards.Count < costCount)
        {
            // 显示失败信息并阻止效果发动
            string myColor = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? "#87CEEB" : "orange";
            string failMessage = $"// [<color={myColor}>{myCard.name}</color>] bury cost failed: need <color=yellow>{costCount}</color> ally card(s), found <color=yellow>{eligibleCards.Count}</color>\n";
            
            var container = GetComponent<CostNEffectContainer>();
            if (container != null)
            {
                container.SetCostNotMet(failMessage);
            }
            return;
        }

        // 随机打乱并选择要置底的卡
        eligibleCards = UtilityFuncManagerScript.ShuffleList(eligibleCards);
        var cardsToBury = eligibleCards.GetRange(0, costCount);

        // 修改逻辑列表：将选中的卡移到底部
        var buriedCards = new List<GameObject>();
        foreach (var card in cardsToBury)
        {
            if (combinedDeck.Contains(card))
            {
                combinedDeck.Remove(card);
                combinedDeck.Insert(0, card);  // 插入到底部
                buriedCards.Add(card);
                
                var targetScript = card.GetComponent<CardScript>();
                string myColor = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? "#87CEEB" : "orange";
                string targetColor = targetScript.myStatusRef == combatManager.ownerPlayerStatusRef ? "#87CEEB" : "orange";
                effectResultString.value += $"// [<color={myColor}>{myCard.name}</color>] bury cost: buried [<color={targetColor}>{targetScript.name}</color>] to the bottom\n";
            }
        }

        // 播放置底动画
        if (buriedCards.Count > 0)
        {
            CombatUXManager.me.PlayStageBuryAnimation(buriedCards, isStage: false);
        }
    }
}
```

## 使用方式

### 在 Unity Editor 中配置

1. 选择一张卡牌 Prefab
2. 在 `CardScript` 组件中设置 `buryCost` 字段（例如：2）
3. 在卡牌的 `CostNEffectContainer` 组件中：
   - 将 `BuryCostEffect.ExecuteBuryCost` 添加到 `preEffectEvent`
   - 将实际效果添加到 `effectEvent`

### 示例配置

假设一张卡牌需要消耗 2 张己方卡置底才能发动：

```
CardScript:
  - buryCost: 2

CostNEffectContainer:
  - preEffectEvent: BuryCostEffect.ExecuteBuryCost
  - effectEvent: HPAlterEffect.DealDmg (或其他效果)
```

## 边界情况处理

1. **己方卡不足**：调用 `SetCostNotMet`，显示失败信息，阻止效果发动
2. **buryCost 为 0 或负数**：直接返回，不执行任何操作
3. **排除当前卡**：正在发动的卡不会被置底（避免逻辑混乱）
4. **空牌组**：如果牌组中没有己方卡，视为不足

## 动画效果

使用现有的 `CombatUXManager.me.PlayStageBuryAnimation` 方法，该方法会：
- 将卡牌移动到牌组底部位置
- 播放平滑的移动动画
- 同步物理卡牌位置

## 测试建议

1. 创建一张测试卡牌，设置 `buryCost = 1`
2. 验证发动时是否将 1 张己方卡置底
3. 测试己方卡不足时的失败提示
4. 测试 `buryCost = 0` 时的行为
5. 测试当前卡是否被正确排除

## 相关文件

- `Assets/Scripts/Card/CardScript.cs` - 添加 buryCost 字段
- `Assets/Scripts/Effects/BuryCostEffect.cs` - 新建文件
- `Assets/Scripts/Card/CostNEffectContainer.cs` - 参考实现模式
- `Assets/Scripts/Effects/TokenCostEffect.cs` - 参考实现模式
- `Assets/Scripts/Effects/CardManipulationEffect.cs` - 参考 bury 逻辑
- `AGENTS.md` - 更新文档
