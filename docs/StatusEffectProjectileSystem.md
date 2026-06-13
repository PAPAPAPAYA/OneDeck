# 状态效果飞行特效系统实现方案

> **更新日期：2026-06-14**
>
> 本文档已在 2026-06 的动画补全工作中更新，以反映当前 **Recorder-Driven** 实现。
> 早期方案（直接调用 `CombatUXManager.PlayStatusEffectProjectile` + Coroutine/回调）已被废弃；当前所有状态效果飞行特效都通过 `AnimationRequest` 捕获，并由 `RecorderAnimationPlayer` 统一播放。

---

## 一、目标

当卡片给予、消耗或转移 Status Effect 时，播放一个从来源飞向目标的特效，**特效到达目标后才提交视觉状态变更**（如状态图标、染色、文本）。

系统遵循项目的 **Two-Phase Execution Model**：
- **逻辑阶段**：同步结算状态层数、触发事件、创建 resolver、捕获动画请求。
- **动画阶段**：`RecorderAnimationPlayer` 按 `EffectRecorder` 树顺序播放请求。

---

## 二、系统架构

```
StatusEffectGiverEffect / CurseEffect / ConsumeStatusEffect / TransferStatusEffectEffect
        ↓ 同步调用
ApplyStatusEffectCore（逻辑结算 + 捕获 StatusEffectChange）
        ↓ 捕获
AnimationRequest { type = StatusEffectProjectile, attackerCard, targetCards, ... }
        ↓ 动画阶段
RecorderAnimationPlayer.PlayRequestCoroutine
        ↓ 调用
ICombatVisuals.PlayMultiStatusEffectProjectile / PlayStatusEffectProjectileToPosition
        ↓ 动画完成回调
提交 deferred StatusEffectChange（更新状态图标/染色）
```

---

## 三、AnimationRequest 字段

```csharp
public enum AnimationRequestType
{
    // ...
    StatusEffectProjectile,
    // ...
}

public class AnimationRequest
{
    public AnimationRequestType type;

    // 发射源（giver / consumer / transfer target）
    public GameObject attackerCard;
    public List<GameObject> attackerCards; // 多来源转移时使用

    // 目标：单目标或多目标二选一，不要同时填充
    public GameObject targetCard;
    public List<GameObject> targetCards;

    // 每层状态对应一个 projectile；默认值 1
    public int projectileCount = 1;

    // 非均匀层数时使用（例如 A 卡失去 2 层、B 卡失去 1 层）
    public List<int> projectileCountsPerTarget;

    // true：projectile 从 target 飞回 attacker（吸收/消耗效果）
    public bool reverseProjectile;

    // 自定义终点世界坐标（例如飞向 statusEffectConsumePos）
    public Vector3? customProjectileEndPosition;

    // 起点随机偏移、发射错开时间（由 CombatUXManager 提供默认值）
    public Vector2 projectileStartRandomOffsetRange;
    public Vector2 projectileStartTimeStaggerRange;
}
```

### 字段语义

| 字段 | 用途 | 典型使用场景 |
|------|------|--------------|
| `attackerCard` | 单来源 | `CurseEffect.EnhanceCurse`、`StatusEffectGiverEffect.GiveStatusEffect` |
| `attackerCards` | 多来源 | `TransferStatusEffectEffect` 多 source → 单 target |
| `targetCard` | 单目标 | `CurseEffect` 给单张诅咒卡加 Power |
| `targetCards` | 多目标 | `StatusEffectGiverEffect` 给多个友军加 buff |
| `projectileCount` | 每个目标生成几个 projectile | 给目标加 3 层 Power 时生成 3 个 |
| `projectileCountsPerTarget` | 每个目标不同层数 | `CurseEffect.ConsumeHostileCursePower` 各目标消耗层数不同 |
| `reverseProjectile` | 反向飞行 | `ConsumeRandomEnemyCardsStatusEffect` 从目标吸回 source |
| `customProjectileEndPosition` | 自定义终点 | `ConsumeOwnStatusEffect` 飞向 `statusEffectConsumePos` |

---

## 四、RecorderAnimationPlayer 处理流程

```csharp
case AnimationRequestType.StatusEffectProjectile:
{
    // 1. 构建目标 CardScript 列表
    var targetCardScripts = new List<CardScript>();
    if (request.targetCards != null && request.targetCards.Count > 0)
    {
        foreach (var t in request.targetCards) { /* 取 CardScript 加入 */ }
    }
    else if (request.targetCard != null)
    {
        /* 单目标加入 */
    }

    // 2. 确定 giver / 终点
    GameObject giver = request.reverseProjectile ? null : request.attackerCard;
    Vector3? customEnd = request.customProjectileEndPosition;

    // 3. 播放 projectile（视觉层处理并行、错开、反向）
    bool done = false;
    visuals.PlayMultiStatusEffectProjectile(
        giver,
        targetCardScripts,
        onEachComplete: null,        // 逻辑已在逻辑阶段完成
        onAllComplete: () => done = true,
        projectileCount: request.projectileCount,
        projectileStartRandomOffsetRange: request.projectileStartRandomOffsetRange,
        projectileStartTimeStaggerRange: request.projectileStartTimeStaggerRange,
        reverseDirection: request.reverseProjectile,
        customEndPosition: customEnd,
        projectileCountsPerTarget: request.projectileCountsPerTarget
    );

    yield return new WaitUntil(() => done);

    // 4. 提交 deferred StatusEffectChange（如果有）
    CommitDeferredStatusEffectChanges(request);
    break;
}
```

### 与 `deferDisplayCommit` 的协作

- `ApplyStatusEffectCore` 在逻辑阶段会调用 `targetCardScript.SnapshotDisplayState()`。
- 捕获的 `StatusEffectChange` 请求可设置 `deferDisplayCommit = true`。
- `RecorderAnimationPlayer` 在 `StatusEffectProjectile` 落地后，统一调用 `CommitDeferredStatusEffectChanges`，刷新状态图标与染色。
- 这避免了“状态文本先变、projectile 后到”的违和感。

---

## 五、各效果的捕获模式

### 5.1 给予状态效果（`StatusEffectGiverEffect`）

所有给予方法遵循同一模式：

1. 同步调用 `ApplyStatusEffectCore`（自动捕获 `StatusEffectChange`）。
2. 捕获 `PopUpBatch` + `StatusEffectProjectile` + `SlotInBatch`。

```csharp
// GiveStatusEffect / GiveAllFriendlyStatusEffect / GiveStatusEffectToLastXCards / GiveStatusEffectToXFriendly
foreach (var target in targetCards)
{
    ApplyStatusEffectCore(target, statusEffectToGive, amount, ...);
}
CaptureBatchStatusEffectAnimation(targetCards, projectileCount);
```

`GiveSelfStatusEffect` 同样会捕获 `PopUpBatch` + `StatusEffectProjectile` + `SlotInBatch`（source = self，target = self）。

### 5.2 诅咒增强（`CurseEffect.ApplyPowerToCardWithProjectile`）

单目标：

```csharp
recorder.animationRequests.Add(new AnimationRequest { type = PopUp, targetCard = targetCard });
recorder.animationRequests.Add(new AnimationRequest {
    type = StatusEffectProjectile,
    attackerCard = myCard,
    targetCard = targetCard.gameObject
});
recorder.animationRequests.Add(new AnimationRequest { type = SlotIn, targetCard = targetCard });
```

### 5.3 消耗自身状态效果（`ConsumeStatusEffect.ConsumeOwnStatusEffect`）

自环/吸收表现：projectile 从卡牌自身飞向 `statusEffectConsumePos`。

```csharp
Vector3 consumePos = CombatUXManager.me.statusEffectConsumePos.position;
recorder.animationRequests.Add(new AnimationRequest { type = PopUp, targetCard = myCard });
recorder.animationRequests.Add(new AnimationRequest {
    type = StatusEffectProjectile,
    attackerCard = myCard,
    targetCard = myCard,
    customProjectileEndPosition = consumePos,
    projectileCount = amountRemoved
});
recorder.animationRequests.Add(new AnimationRequest {
    type = StatusEffectChange,
    targetCard = myCard,
    statusEffect = statusEffectToConsume,
    statusEffectAmount = -amountRemoved,
    deferDisplayCommit = true
});
recorder.animationRequests.Add(new AnimationRequest { type = SlotIn, targetCard = myCard });
```

### 5.4 消耗敌方卡牌状态效果（`ConsumeStatusEffect.ConsumeRandomEnemyCardsStatusEffect`）

批量吸收：

```csharp
CaptureBatchStatusEffectConsumeAnimation(myCard, selectedTargets, statusEffectToConsume, 1);
```

内部顺序：
1. `StatusEffectChange`（defer）
2. `PopUpBatch`
3. `StatusEffectProjectile`（`reverseProjectile = true`）
4. `SlotInBatch`

### 5.5 消耗敌方诅咒卡力量（`CurseEffect.ConsumeHostileCursePower`）

支持每目标不同消耗层数，projectile 飞向 `statusEffectConsumePos`：

```csharp
CaptureBatchStatusEffectConsumeAnimation(
    myCard, affectedTargets, Power, removedAmounts, statusEffectConsumePos);
```

### 5.6 转移状态效果（`TransferStatusEffectEffect`）

多来源 → 单目标：

```csharp
CaptureBatchStatusEffectTransferAnimation(sourceCards, targetCard, effect, amountsPerSource);
```

内部顺序：
1. `PopUpBatch`（sources）
2. `PopUp`（target）
3. `StatusEffectProjectile`（`attackerCards = sources`, `targetCard = target`, `reverseProjectile = true`）
4. `SlotInBatch`（sources）
5. `SlotIn`（target）
6. `StatusEffectChange`（sources 减少）

---

## 六、ICombatVisuals 接口

```csharp
void PlayMultiStatusEffectProjectile(
    GameObject giverCard,
    List<CardScript> targetCards,
    Action<CardScript> onEachComplete,
    Action onAllComplete = null,
    float? customStaggerDelay = null,
    int projectileCount = 1,
    Vector2? projectileStartRandomOffsetRange = null,
    Vector2? projectileStartTimeStaggerRange = null,
    bool reverseDirection = false,
    Vector3? customEndPosition = null,
    List<int> projectileCountsPerTarget = null);

void PlayStatusEffectProjectileToPosition(
    GameObject giverCard,
    Vector3 endPosition,
    Action onComplete = null,
    int projectileCount = 1,
    Vector2? projectileStartRandomOffsetRange = null,
    Vector2? projectileStartTimeStaggerRange = null);
```

- `PlayMultiStatusEffectProjectile`：用于有明确目标卡牌的场景。
- `PlayStatusEffectProjectileToPosition`：用于目标不是卡牌而是世界坐标的场景（如 `statusEffectConsumePos`）。

---

## 七、Unity Inspector 配置

在场景的 **CombatUXManager** GameObject 上配置：

| 字段 | 建议值 | 说明 |
|------|--------|------|
| Status Effect Projectile Prefab | 你的特效预制体 | 见下方制作指南 |
| Projectile Duration | 0.4 | 单次飞行时间（秒） |
| Projectile Arc Height | 2 | 抛物线最高点高度 |
| Projectile Start Offset | (0, 0.5, 0) | 从来源卡片上方发射 |
| Projectile End Offset | (0, 0.5, 0) | 落到目标卡片上方 |
| Projectile Start Random Offset Range | (0.3, 0.3) | 多层 projectile 的起点随机扩散 |
| Projectile Start Time Stagger Range | (0, 0.15) | 多层 projectile 的发射错开时间 |
| Status Effect Consume Pos | Transform | 消耗效果时 projectile 的终点（如屏幕中央） |

---

## 八、特效预制体制作指南

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

创建脚本 `StatusEffectVisual.cs` 挂载到预制体上，在 `CombatUXManager` 实例化后调用 `Setup(effect)` 设置颜色。

---

## 九、注意事项

1. **阻塞问题**：projectile 是动画阶段的一部分，`RecorderAnimationPlayer` 会等待全部 projectile 落地后再继续下一个请求。
2. **多层 projectile**：`projectileCount` 和 `projectileCountsPerTarget` 让视觉效果与逻辑层数一致。
3. **反向飞行**：`reverseProjectile = true` 表示从 target 飞回 source，用于吸收/消耗效果。
4. **自定义终点**：`customProjectileEndPosition` 让 projectile 飞向非卡牌位置（如 `statusEffectConsumePos`）。
5. **Headless 测试**：`NullCombatVisuals` 会立即调用 `onAllComplete`，不影响逻辑执行。

---

## 十、文件修改清单

- [x] `Assets/Scripts/Managers/AnimationRequest.cs` — 添加 projectile 相关字段
- [x] `Assets/Scripts/Managers/ICombatVisuals.cs` — 扩展 `PlayMultiStatusEffectProjectile`，新增 `PlayStatusEffectProjectileToPosition`
- [x] `Assets/Scripts/Managers/NullCombatVisuals.cs` / `NullCombatVisualsBehaviour.cs` — Headless 实现
- [x] `Assets/Scripts/Managers/RecorderAnimationPlayer.cs` — 处理 `StatusEffectProjectile` 请求
- [x] `Assets/Scripts/UXPrototype/CombatUXManager.cs` — 实现 projectile 视觉播放
- [x] `Assets/Scripts/Effects/EffectScript.cs` — 添加 `CaptureBatchStatusEffectConsumeAnimation`、`CaptureBatchStatusEffectTransferAnimation`
- [x] `Assets/Scripts/Effects/StatusEffect/StatusEffectGiverEffect.cs` — 迁移为 Recorder-Driven
- [x] `Assets/Scripts/Effects/CurseEffect.cs` — `ApplyPowerToCardWithProjectile`、`ConsumeHostileCursePower`
- [x] `Assets/Scripts/Effects/StatusEffect/ConsumeStatusEffect.cs` — `ConsumeOwnStatusEffect`、`ConsumeRandomEnemyCardsStatusEffect`
- [x] `Assets/Scripts/Effects/TransferStatusEffectEffect.cs` — `TransferAllStatusEffectToHostileCurse`、`TransferOneStatusEffectToSelf`
