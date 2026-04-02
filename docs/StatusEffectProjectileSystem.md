# 状态效果飞行特效系统实现方案

## 一、目标

当卡片给予其他卡片 Status Effect 时，播放一个从给予者抛物线飞向被给予者的特效，**特效到达目标后才执行实际的效果**。

## 二、系统架构

```
StatusEffectGiverEffect (给予者)
        ↓ 调用
CombatUXManager.PlayStatusEffectProjectile()
        ↓ 播放动画
Projectile GameObject (抛物线飞行)
        ↓ 动画完成回调
执行实际的状态效果添加逻辑
```

## 三、代码实现

### 1. CombatUXManager.cs 新增代码

在文件末尾 `#endregion`（Cleanup 区域结束）后，添加以下内容：

```csharp
#region Status Effect 飞行特效系统

[Header("STATUS EFFECT PROJECTILE")]
[Tooltip("状态效果飞行特效预制体（可以是Sprite、粒子系统或简单的GameObject）")]
public GameObject statusEffectProjectilePrefab;
[Tooltip("特效飞行持续时间")]
public float projectileDuration = 0.4f;
[Tooltip("抛物线高度")]
public float projectileArcHeight = 2f;
[Tooltip("特效起始位置偏移")]
public Vector3 projectileStartOffset = new Vector3(0, 0.5f, 0);
[Tooltip("特效目标位置偏移")]
public Vector3 projectileEndOffset = new Vector3(0, 0.5f, 0);

/// <summary>
/// 播放状态效果从给予者飞向被给予者的抛物线特效
/// 特效飞到目标后才执行 onComplete 回调
/// </summary>
/// <param name="giverCard">给予者逻辑卡片</param>
/// <param name="receiverCard">被给予者逻辑卡片</param>
/// <param name="onComplete">特效完成回调（特效到达目标后执行）</param>
public void PlayStatusEffectProjectile(GameObject giverCard, GameObject receiverCard, Action onComplete = null)
{
    if (statusEffectProjectilePrefab == null || giverCard == null || receiverCard == null)
    {
        onComplete?.Invoke();
        return;
    }

    // 获取物理卡片位置
    BuildCardScriptToPhysicalDictionary();
    
    Vector3 startPos = GetCardWorldPosition(giverCard) + projectileStartOffset;
    Vector3 endPos = GetCardWorldPosition(receiverCard) + projectileEndOffset;

    // 创建特效实例
    GameObject projectile = Instantiate(statusEffectProjectilePrefab, startPos, Quaternion.identity);
    
    // 计算抛物线中间点
    Vector3 midPoint = Vector3.Lerp(startPos, endPos, 0.5f) + Vector3.up * projectileArcHeight;

    // 创建抛物线动画
    Sequence projectileSequence = DOTween.Sequence();
    
    // 第一阶段：从起点到中间点（上升）
    projectileSequence.Append(
        projectile.transform.DOMove(midPoint, projectileDuration * 0.5f)
            .SetEase(Ease.OutQuad)
    );
    
    // 第二阶段：从中间点到终点（下降）
    projectileSequence.Append(
        projectile.transform.DOMove(endPos, projectileDuration * 0.5f)
            .SetEase(Ease.InQuad)
    );
    
    // 同步旋转：让特效始终朝向目标
    projectile.transform.LookAt(endPos);

    // 动画完成：销毁特效并执行回调
    projectileSequence.OnComplete(() =>
    {
        Destroy(projectile);
        onComplete?.Invoke();
    });

    projectileSequence.Play();
}

/// <summary>
/// 获取卡片的实际世界位置（优先使用物理卡片）
/// </summary>
private Vector3 GetCardWorldPosition(GameObject card)
{
    var cardScript = card.GetComponent<CardScript>();
    if (cardScript != null)
    {
        var physicalCard = GetPhysicalCardFromLogicalCard(cardScript);
        if (physicalCard != null)
        {
            return physicalCard.transform.position;
        }
    }
    return card.transform.position;
}

#endregion
```

### 2. StatusEffectGiverEffect.cs 修改

修改 `GiveStatusEffect()` 方法，使用 Coroutine 或回调方式等待特效完成：

#### 方案 A：使用 Coroutine（推荐，简单直观）

```csharp
public virtual void GiveStatusEffect(int amount)
{
    if (statusEffectToGive == EnumStorage.StatusEffect.None) return;
    
    // 收集目标卡片逻辑...
    // ...原有筛选代码...
    
    if (cardsToGiveTag.Count <= 0) return;
    
    // 确定实际目标卡片
    var targetCards = new List<CardScript>();
    for (var i = 0; i < amount; i++)
    {
        CardScript targetCardScript;
        if (spreadEvenly)
            targetCardScript = cardsToGiveTag[i].GetComponent<CardScript>();
        else
            targetCardScript = cardsToGiveTag[Random.Range(0, cardsToGiveTag.Count)].GetComponent<CardScript>();
        targetCards.Add(targetCardScript);
    }
    
    // 播放特效并等待完成后执行效果
    if (CombatUXManager.me != null && CombatUXManager.me.statusEffectProjectilePrefab != null)
    {
        StartCoroutine(GiveStatusEffectWithProjectile(targetCards));
    }
    else
    {
        // 无特效，直接执行
        ApplyStatusEffectsToTargets(targetCards);
    }
}

/// <summary>
/// 协程：播放飞行特效，完成后执行效果
/// </summary>
private System.Collections.IEnumerator GiveStatusEffectWithProjectile(List<CardScript> targetCards)
{
    int completedCount = 0;
    int totalCount = targetCards.Count;
    bool allComplete = false;

    for (int i = 0; i < targetCards.Count; i++)
    {
        var target = targetCards[i];
        int index = i; // 捕获索引
        
        // 错开播放时间
        DOVirtual.DelayedCall(i * 0.05f, () =>
        {
            CombatUXManager.me.PlayStatusEffectProjectile(
                myCard, 
                target.gameObject, 
                () => 
                {
                    // 单个特效完成，执行该目标的效果
                    ApplyStatusEffectToSingleTarget(target);
                    
                    completedCount++;
                    if (completedCount >= totalCount)
                    {
                        allComplete = true;
                    }
                }
            );
        });
    }

    // 等待所有特效完成
    yield return new WaitUntil(() => allComplete);
    
    // 刷新UI
    CombatInfoDisplayer.me.RefreshDeckInfo();
}

/// <summary>
/// 对单个目标应用状态效果
/// </summary>
private void ApplyStatusEffectToSingleTarget(CardScript targetCardScript)
{
    targetCardScript.myStatusEffects.Add(statusEffectToGive);
    
    // 输出效果信息
    var targetCardOwnerString = targetCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? "<color=#87CEEB>Your</color> [" : "<color=orange>Enemy's</color> [";
    var thisCardOwnerString = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? "<color=#87CEEB>Your</color> [" : "<color=orange>Enemy's</color> [";
    string thisCardColor = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? "#87CEEB" : "orange";
    string targetCardColor = targetCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? "#87CEEB" : "orange";
    
    effectResultString.value +=
        "// " + thisCardOwnerString +
        "<color=" + thisCardColor + ">" + myCard.name + "</color>] gave " +
        targetCardOwnerString +
        "<color=" + targetCardColor + ">" + targetCardScript.gameObject.name + "</color>] " +
        "<color=yellow>1</color> [" + statusEffectToGive + "]\n";
    
    // 创建状态效果解析器
    if (myStatusEffectResolverScript != null)
    {
        var tagResolver = Instantiate(myStatusEffectResolverScript, targetCardScript.transform);
        GameEventStorage.me.onThisTagResolverAttached.RaiseSpecific(tagResolver);
    }
    
    // 播放粒子效果
    PlayStatusEffectParticle(targetCardScript.transform);
    
    // 触发tint效果
    TriggerTintForStatusEffect(targetCardScript, statusEffectToGive);
}
```

#### 方案 B：使用回调（无需 Coroutine，适合已有异步架构）

```csharp
public virtual void GiveStatusEffect(int amount)
{
    if (statusEffectToGive == EnumStorage.StatusEffect.None) return;
    
    // 收集目标卡片...
    // ...原有筛选代码...
    
    if (cardsToGiveTag.Count <= 0) return;
    
    var targetCards = new List<CardScript>();
    for (var i = 0; i < amount; i++)
    {
        CardScript targetCardScript;
        if (spreadEvenly)
            targetCardScript = cardsToGiveTag[i].GetComponent<CardScript>();
        else
            targetCardScript = cardsToGiveTag[Random.Range(0, cardsToGiveTag.Count)].GetComponent<CardScript>();
        targetCards.Add(targetCardScript);
    }
    
    // 使用回调链方式
    GiveStatusEffectSequentially(targetCards, 0);
}

/// <summary>
/// 顺序给予状态效果，每个都有独立的飞行特效
/// </summary>
private void GiveStatusEffectSequentially(List<CardScript> targets, int index)
{
    if (index >= targets.Count)
    {
        // 所有目标处理完成
        CombatInfoDisplayer.me.RefreshDeckInfo();
        return;
    }
    
    var target = targets[index];
    
    if (CombatUXManager.me != null && CombatUXManager.me.statusEffectProjectilePrefab != null)
    {
        // 播放特效，完成后处理当前目标并递归处理下一个
        CombatUXManager.me.PlayStatusEffectProjectile(
            myCard,
            target.gameObject,
            () =>
            {
                ApplyStatusEffectToSingleTarget(target);
                // 处理下一个
                GiveStatusEffectSequentially(targets, index + 1);
            }
        );
    }
    else
    {
        // 无特效，直接执行
        ApplyStatusEffectToSingleTarget(target);
        GiveStatusEffectSequentially(targets, index + 1);
    }
}
```

### 3. 其他方法也需要类似修改

同样的模式需要应用到 `StatusEffectGiverEffect` 中的其他给予方法：

- `GiveSelfStatusEffect()` - 给自己添加时可能不需要特效
- `GiveAllFriendlyStatusEffect()` - 给所有友方卡片添加
- `GiveStatusEffectToLastXCards()` - 给后面X张卡添加

**GiveAllFriendlyStatusEffect() 示例：**

```csharp
public virtual void GiveAllFriendlyStatusEffect(int amount)
{
    // ...原有收集友方卡片代码...
    
    if (cardsToGive.Count <= 0) return;
    
    // 顺序播放特效并给予效果
    GiveToFriendlySequentially(cardsToGive, 0, amount);
}

private void GiveToFriendlySequentially(List<GameObject> cards, int index, int amount)
{
    if (index >= cards.Count)
    {
        CombatInfoDisplayer.me.RefreshDeckInfo();
        return;
    }
    
    var targetCard = cards[index];
    var targetCardScript = targetCard.GetComponent<CardScript>();
    
    // 检查是否可以叠加
    if (!canStatusEffectBeStacked && targetCardScript.myStatusEffects.Contains(statusEffectToGive))
    {
        GiveToFriendlySequentially(cards, index + 1, amount);
        return;
    }
    
    // 播放特效
    if (CombatUXManager.me != null && CombatUXManager.me.statusEffectProjectilePrefab != null)
    {
        CombatUXManager.me.PlayStatusEffectProjectile(
            myCard,
            targetCard,
            () =>
            {
                // 特效完成，执行效果
                for (int i = 0; i < amount; i++)
                {
                    targetCardScript.myStatusEffects.Add(statusEffectToGive);
                }
                // ...输出信息、创建resolver、播放粒子等...
                
                // 处理下一个
                GiveToFriendlySequentially(cards, index + 1, amount);
            }
        );
    }
    else
    {
        // 直接执行
        for (int i = 0; i < amount; i++)
        {
            targetCardScript.myStatusEffects.Add(statusEffectToGive);
        }
        // ...输出信息、创建resolver、播放粒子等...
        
        GiveToFriendlySequentially(cards, index + 1, amount);
    }
}
```

## 四、Unity Inspector 配置

在场景的 **CombatUXManager** GameObject 上配置：

| 字段 | 建议值 | 说明 |
|------|--------|------|
| Status Effect Projectile Prefab | 你的特效预制体 | 见下方制作指南 |
| Projectile Duration | 0.4 | 飞行时间（秒） |
| Projectile Arc Height | 2 | 抛物线最高点高度 |
| Projectile Start Offset | (0, 0.5, 0) | 从给予者卡片上方发射 |
| Projectile End Offset | (0, 0.5, 0) | 落到被给予者卡片上方 |

## 五、特效预制体制作指南

### 基础版（简单球体 + 拖尾）

1. 在 Hierarchy 中创建空 GameObject，命名为 `StatusEffectProjectile`
2. 添加子物体 Sphere（或 Sprite）
   - 缩放：0.3, 0.3, 0.3
   - 材质：自发光材质（颜色根据状态效果类型）
3. 添加 Trail Renderer 组件
   - Time: 0.3
   - Width: 从 0.2 渐变到 0
   - Material: 拖尾材质
4. 保存为 Prefab，拖到 CombatUXManager 的字段中

### 进阶版（按状态效果类型区分颜色）

创建脚本 `StatusEffectVisual.cs` 挂载到预制体上：

```csharp
using UnityEngine;

public class StatusEffectVisual : MonoBehaviour
{
    public SpriteRenderer spriteRenderer;
    public TrailRenderer trailRenderer;
    
    public void Setup(EnumStorage.StatusEffect effect)
    {
        Color color = GetColorForEffect(effect);
        if (spriteRenderer != null) spriteRenderer.color = color;
        if (trailRenderer != null) trailRenderer.startColor = color;
    }
    
    private Color GetColorForEffect(EnumStorage.StatusEffect effect)
    {
        return effect switch
        {
            EnumStorage.StatusEffect.Infected => new Color(0.2f, 0.8f, 0.2f), // 绿色
            EnumStorage.StatusEffect.Mana => new Color(0.2f, 0.5f, 1f),       // 蓝色
            EnumStorage.StatusEffect.Power => new Color(1f, 0.2f, 0.2f),      // 红色
            EnumStorage.StatusEffect.Shield => new Color(0.9f, 0.8f, 0.2f),   // 金色
            _ => Color.white
        };
    }
}
```

然后在 `CombatUXManager.PlayStatusEffectProjectile()` 中实例化后调用 Setup。

## 六、注意事项

1. **阻塞问题**：特效播放期间会阻塞后续效果，确保动画时间不要过长（建议 0.3-0.5 秒）

2. **多个目标**：如果一次给予多个卡片，建议使用错开时间（stagger）或并行播放

3. **物理卡片不存在**：如果目标卡片没有对应的物理卡片（如在手牌、墓地等），`GetCardWorldPosition` 会回退到逻辑位置

4. **回调地狱**：如果使用回调方案处理多个目标，注意递归深度（一般不会超过几十层）

5. **协程生命周期**：使用协程方案时，确保卡片销毁时停止协程：

```csharp
private void OnDisable()
{
    StopAllCoroutines();
}
```

## 七、文件修改清单

- [ ] `Assets/Scripts/UXPrototype/CombatUXManager.cs` - 添加飞行特效系统
- [ ] `Assets/Scripts/Effects/StatusEffect/StatusEffectGiverEffect.cs` - 修改给予逻辑为异步
- [ ] 创建 `StatusEffectProjectile` 预制体
- [ ] Unity 场景配置 - 在 CombatUXManager 上配置参数
