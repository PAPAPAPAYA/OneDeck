# EffectRecorder 架构审视与批评

> 文档目的：记录对当前 EffectRecorder / AnimationRequest / RecorderAnimationPlayer 体系的分析结论，供后续迭代参考。
> 产生时间：2026-06-10
> 基于代码版本：当前工作目录 HEAD

---

## 1. 设计的价值（应保留的核心理念）

1. **逻辑与表现分离（Two-Phase Execution）**
   - 逻辑阶段同步结算 HP、牌库、状态；动画阶段再按 `AnimationRequest` 顺序播放。
   - 这是支撑 Headless 测试（`NullCombatVisuals`）的关键前提。

2. **因果关系树（Causality Tree）**
   - 通过父子层级把 reactive effect（如 `onMeBuried -> StageSelf`）挂到触发它的 recorder 下。
   - `RecorderAnimationPlayer` 先播父级、再递归子级，玩家能直观看到连锁链条。

3. **批量动画与快照索引**
   - `BuryEffect` / `StageEffect` 在逻辑阶段对 `targetIndices` 做快照，以处理连锁中牌序被提前修改的场景。
   - 避免了动画播放时因 deck 顺序已变而导致的目标位置错乱。

4. **延迟状态提交（Deferred Display Commit）**
   - `deferDisplayCommit` 让状态图标/染色在 `StatusEffectProjectile` 命中后才刷新，避免“状态先变、动画后到”的违和感。

---

## 2. 结构性缺陷

### 2.1 EffectRecorder 是“数据型 MonoBehaviour”

```csharp
// Assets/Scripts/Managers/EffectRecorder.cs
public class EffectRecorder : MonoBehaviour
{
	public int sessionID;
	public int chainID;
	public string processedEffectID;
	public GameObject cardObject;
	public GameObject effectObject;
	public List<AnimationRequest> animationRequests = new List<AnimationRequest>();
	public bool animationPlayed = false;
}
```

**问题**
- 无任何行为，纯粹是数据容器，却因要利用 `Transform.parent` 维护树而被做成 `MonoBehaviour`。
- 每个效果都 `Instantiate(effectRecorderPrefab)`，增加场景对象、GC、生命周期管理成本。
- 遍历子 recorder 必须写 `transform.GetChild(i).GetComponent<EffectRecorder>()`，可读性差。

**后续改进方向**
- 改为普通 C# 类，内部显式维护 `List<EffectRecorder> children` 与 `EffectRecorder parent`。
- `EffectChainManager` 维护一个对象池或显式树列表，彻底脱离 `GameObject` 生命周期。

---

### 2.2 完全开放的 public 字段，没有封装

- `animationRequests` 被效果类直接 `Add`；
- `animationPlayed` 被 `RecorderAnimationPlayer` 直接改写；
- `cardObject` / `effectObject` 可被外部随意替换，破坏 loop guard 的不变式。

**后续改进方向**
- 字段改为 `private readonly` 或通过构造函数/Builder 创建。
- `animationRequests` 提供受控的 `AddRequest(AnimationRequest)` 方法，并在内部做类型校验。

---

## 3. AnimationRequest 的“万能袋子”问题

```csharp
// Assets/Scripts/Managers/AnimationRequest.cs
public class AnimationRequest
{
	public AnimationRequestType type;
	public GameObject attackerCard;
	public bool isAttackingEnemy;
	public GameObject targetCard;
	public List<GameObject> targetCards;
	public Action onHit;
	public Action onComplete;
	public float duration = 0.5f;
	public bool useArc = true;
	public int targetIndex;
	public List<int> targetIndices;        // Semi-deprecated 注释
	public int snapshotDeckSize;           // 仅 debug
	public EnumStorage.StatusEffect statusEffect;
	public int statusEffectAmount;
	public ParticleSystem statusEffectParticlePrefab;
	public float statusEffectParticleYOffset;
	public GameObject sourceCard;
	public bool deferDisplayCommit = false;
}
```

**问题**
- 不同 `type` 使用完全不同的字段组合，没有类型安全。
- `targetCard` 与 `targetCards` 同时存在，注释警告不要同时填，但无运行时检查。
- `targetIndices` / `snapshotDeckSize` 被标为 semi-deprecated / debug only，却仍留在核心类型里，增加心智负担。
- 新增动画类型时，必须回到这个中心类加字段，所有效果都可见这些字段。

**后续改进方向**
- 如果 C# 版本允许，使用**类层次结构**：抽象基类 `AnimationRequest` + 派生 `AttackRequest`、`MoveBatchRequest`、`StatusEffectProjectileRequest` 等。
- 若不想引入太多类，至少把不同 concern 拆成内嵌 struct，并通过工厂方法约束构造。

---

## 4. 逻辑阶段与视觉阶段的边界仍然模糊

```csharp
// Assets/Scripts/Effects/BuryEffect.cs:309
combatManager.visuals.SyncPhysicalCardsWithCombinedDeck();

// Assets/Scripts/Effects/StageEffect.cs:356
combatManager.visuals.SyncPhysicalCardsWithCombinedDeck();
```

**问题**
- 理论上逻辑阶段只应改 `combinedDeckZone` 等模型数据，但 `BuryEffect` / `StageEffect` 在逻辑阶段仍同步物理牌堆。
- 旁边带有 `VISUAL-FIX(2026-05-18)` 注释，说明这是为修复“distance-zero tweens”而打的补丁。
- 这破坏了“logic before animation”的分层承诺，也让 Headless 测试和未来的网络同步难以做（Headless 不应调用任何 `visuals.*` 方法）。
- 正因为有提前同步，才需要后面一堆 `ApplyAnimationResult` + `UpdateAllPhysicalCardTargets` 的补偿逻辑，以及 `targetIndices` 快照的不断补丁。

**后续改进方向**
- 逻辑阶段彻底不碰 `physicalCardsInDeck`。
- 动画阶段统一通过 `ApplyAnimationResult(request)` 来推进物理牌序。
- 若老动画路径依赖提前同步，应修复老路径本身，而非在逻辑阶段打补丁。

---

## 5. EffectChainManager 的可靠性隐患

### 5.1 PopCurrentRecorder 没有配对校验

```csharp
// Assets/Scripts/Managers/EffectChainManager.cs:157-169
public void PopCurrentRecorder()
{
	if (recorderStack.Count > 0)
	{
		var popped = recorderStack[recorderStack.Count - 1];
		recorderStack.RemoveAt(recorderStack.Count - 1);
		// ...
	}
}
```

**问题**
- 只管栈顶。如果效果在 `InvokeEffectEvent` 里抛出异常、提前 return、或分支遗漏 push/pop，栈就会错位。
- `CostNEffectContainer.InvokeEffectEvent` 的 push/pop 是手工编码的，任何中间异常都会让 `currentEffectRecorder` 永久指向错误层级。

**后续改进方向**
- 把 `MakeANewEffectRecorder` + `PopCurrentRecorder` 包装成 `try/finally` 辅助方法，或返回一个 `IDisposable` scope，保证对称。

---

### 5.2 chainDepth 与 recorderStack 不同步

```csharp
// Assets/Scripts/Managers/EffectChainManager.cs:152-154
currentRec.processedEffectID = effectID;
chainDepth++;
return true;
```

**问题**
- `chainDepth` 在 `EffectCanBeInvoked` 里 ++，但异常路径、loop guard 提前返回、或 chain 关闭时只一次性 `chainDepth = 0` 归零。
- 它并不跟随 `recorderStack` 的实际深度，容易发生“深度已加、但 recorder 没推进”的漂移。

**后续改进方向**
- 让 `chainDepth` 直接反映 `recorderStack.Count` 或 `openedEffectRecorders` 的嵌套深度，而不是独立维护。

---

### 5.3 processedEffectID 和 loop guard 语义不一致

```csharp
// Assets/Scripts/Managers/EffectChainManager.cs:128-131
if (wipChainScript.cardObject == myCard &&
    wipChainScript.effectObject == myEffect &&
    !string.IsNullOrEmpty(wipChainScript.processedEffectID))
```

**问题**
- 循环保护实际用的是 GameObject reference 相等，`processedEffectID` 只是用来判断“已处理过”，真正防循环的是 `cardObject` + `effectObject`。
- `processedEffectID` 字段基本只用于 debug，徒增认知负担。
- 如果同一预制体在两张卡上复用，`effectObject` 引用不同，guard 不会误判；但如果一张卡上有多个相同 `Effect` 组件实例，guard 按 GameObject 比较可能产生不直观的通过/拒绝行为。

**后续改进方向**
- 明确 loop guard 的语义文档，或改用 `(cardObject, effectObject, effectID)` 三元组作为防循环键。
- 若 `processedEffectID` 仅用于 debug，可改为内部 debug log 的临时变量，不必作为持久字段。

---

## 6. RecorderAnimationPlayer 的实现问题

### 6.1 巨型 switch：开闭原则被违反

`PlayRequestCoroutine` 里有一个近 20 case 的 switch，接近 350 行（`Assets/Scripts/Managers/RecorderAnimationPlayer.cs:195-522`）。

**问题**
- 新增一种动画类型必须修改这个中心类，而不是“添加一个策略/处理器”。
- 中心类持续膨胀，合并冲突风险增加。

**后续改进方向**
- 把每种请求类型注册到 `Dictionary<AnimationRequestType, IAnimationHandler>`，或让多态 `AnimationRequest` 各自实现 `Play()`。
- 新动画类型独立成文件，不影响已有逻辑。

---

### 6.2 Batch 动画的并发计数依赖闭包 mutable state

```csharp
// Assets/Scripts/Managers/RecorderAnimationPlayer.cs:218-251
int completedCount = 0;
int totalCount = request.targetCards != null ? request.targetCards.Count : 0;
// ...
visuals.MoveCardToIndex(card, targetIndex, request.duration, request.useArc, () =>
{
	completedCount++;
	if (request.onComplete != null && completedCount >= totalCount) request.onComplete();
});
yield return new WaitUntil(() => completedCount >= totalCount);
```

**问题**
- `completedCount` 是被闭包捕获的局部变量，每次 batch 都产生分配。
- 如果某张卡没有对应 physical card，动画回调可能不被调用，`WaitUntil` 会永远挂起。
- `request.onComplete` 只应在整体完成时触发一次，但这里每个卡片的回调都要判断 `completedCount >= totalCount`，逻辑分散。

**后续改进方向**
- 封装 `BatchAnimationToken`（内部维护计数和 `onComplete`），避免每个卡片闭包都判断。
- 或者让 `ICombatVisuals` 增加“批量移动并返回统一回调”的接口，由视觉层自己保证 completion。

---

### 6.3 对 CombatUXManager 的下转型破坏了抽象

```csharp
// Assets/Scripts/Managers/RecorderAnimationPlayer.cs:188-193
var combatUX = visuals as CombatUXManager;
if (combatUX != null && combatUX.IsDeckFocused)
{
	yield return combatUX.StartCoroutine(combatUX.RestoreDeckFocusCoroutine());
}
```

以及多处 `var combatUX2 = visuals as CombatUXManager;` 去取 `physicalCardsInDeck`、`GetPhysicalCardDeckIndex`。

**问题**
- `ICombatVisuals` 本应是抽象边界，但实现细节不断向上泄漏。
- 换一套视觉实现（例如 2D UI、网络对战、Headless）时，这些路径会静默失败。

**后续改进方向**
- 在 `ICombatVisuals` 里增加 `IsDeckFocused`、`GetPhysicalCardDeckIndex` 等接口，或拆出 `ICombatDeckVisuals`。
- `RecorderAnimationPlayer` 只应依赖接口，不应 down-cast 到具体实现。

---

## 7. 生命周期与异常处理

### 7.1 AnimationStateTracker 与 RecorderAnimationPlayer 双轨并行

```csharp
// Assets/Scripts/Managers/CombatManager.cs:416-420
while (AnimationStateTracker.me != null && AnimationStateTracker.me.HasActiveBatch)
{
	Debug.Log("...waiting for HasActiveBatch...");
	yield return null;
}
```

**问题**
- 老系统（`AnimationStateTracker`）没完全下线，新系统已经上位。
- 两者维护各自的“动画是否在进行中”状态，容易出现一个认为空闲、另一个认为忙碌的情况。
- 新效果写 request，老效果可能仍直接调用 `AnimationStateTracker.RegisterAnimation`，理解成本加倍。

**后续改进方向**
- 制定下线计划，把剩余直接调用迁移到 `EffectRecorder`。
- 最终删除 `AnimationStateTracker` 或将其降为纯 debug 工具。

---

### 7.2 输入阻塞的引用计数容易漂移

`BlockInput` / `UnblockInput` 是引用计数，但 `StopAllSpecialAnimations`、场景卸载、异常路径都可能让 count 不归零。代码里不得不频繁调用 `ResetInputBlock()` 作为“钝器 reset”。

**后续改进方向**
- 将输入阻塞改为显式状态机（`Idle -> PlayingAnimation -> WaitingForInput`），而不是引用计数。
- 或在 `PlayRecorderAnimationsAndWait` 的 `try/finally` 里保证单一层面的 block/release，不再使用全局引用计数。

---

## 8. 可落地的具体改进清单

| 问题 | 建议措施 | 预估影响范围 |
|------|----------|--------------|
| MonoBehaviour 数据容器 | `EffectRecorder` 改为纯数据类，显式 `children` / `parent` | `EffectRecorder`, `EffectChainManager` |
| AnimationRequest 万能袋子 | 引入类层次结构或内嵌 struct，工厂方法约束构造 | `AnimationRequest`, 所有 Effect 类 |
| public 可变字段 | 改为 `private readonly` + Builder/构造函数 | `EffectRecorder`, `AnimationRequest` |
| 逻辑阶段调用 SyncPhysicalCardsWithCombinedDeck | 移除 `BuryEffect` / `StageEffect` 里的同步调用，让 `ApplyAnimationResult` 全权负责 | `BuryEffect`, `StageEffect`, `CombatUXManager` |
| 巨型 switch | 改成 `IAnimationRequestHandler` 注册表 | `RecorderAnimationPlayer` |
| Batch 闭包计数 | 封装 `BatchAnimationToken` 或统一批量接口 | `RecorderAnimationPlayer`, `ICombatVisuals` |
| CombatUXManager 下转型 | 在 `ICombatVisuals` 增加对应接口，或拆出 `ICombatDeckVisuals` | `ICombatVisuals`, `CombatUXManager`, `RecorderAnimationPlayer` |
| PopCurrentRecorder 无校验 | 用 `try/finally` 或 `IDisposable` scope 包装 push/pop | `EffectChainManager`, `CostNEffectContainer` |
| chainDepth 不同步 | 让 `chainDepth` 直接反映栈深度 | `EffectChainManager` |
| AnimationStateTracker 双轨 | 迁移遗留调用，最终下线 legacy tracker | `AnimationStateTracker`, `CombatManager`, 各 Effect |
| 输入阻塞引用计数漂移 | 改为显式状态机或单层 try/finally 保障 | `CombatManager` |

---

## 9. 总结

`EffectRecorder` 的**高层设计是正确的**：两阶段执行、录制动画意图、树形连锁。但实现已经逐渐被补丁和 `VISUAL-FIX` 堆成了“刚好能跑”的状态。

当前系统最大的三个风险是：

1. **数据模型与表现层仍然纠缠**（逻辑阶段 `SyncPhysicalCardsWithCombinedDeck`）。
2. **类型系统过于松散**（`AnimationRequest` 万能袋子 + `EffectRecorder` 开放字段）。
3. **生命周期和并发控制依赖手工约定**（recorder stack、chain depth、batch closure count）。

如果能先把 `EffectRecorder` 和 `AnimationRequest` 改为更严格的数据结构，再把 `RecorderAnimationPlayer` 的 switch 拆成策略表，最后移除逻辑阶段对 `visuals` 的调用，这套系统的可维护性会有一个质的提升。
