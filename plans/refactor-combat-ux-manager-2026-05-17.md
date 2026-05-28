# CombatUXManager 重构计划

> 状态：阶段1 已完成（2026-05-17）  
> 目标：将 1968 行的上帝类拆分为职责清晰的子系统

---

## 一、现状诊断

`CombatUXManager.cs` 当前 **1968 行**，混合了至少 7 个独立职责域，违反单一职责原则（SRP）：

| 职责域 | 代码行数（估算） | 说明 |
|--------|----------------|------|
| 物理卡牌生命周期管理 | ~150 | 创建、销毁、字典映射、清理 |
| 逻辑↔物理牌组同步 | ~200 | SyncPhysicalCards、ApplyAnimationResult |
| 通用卡牌动画系统 | ~300 | MoveCardWithAnimation、洗牌动画 |
| Deck Focus / Peel 系统 | ~350 | 牌组聚焦、卡片揭开动画 |
| 状态效果投射物 | ~180 | 抛物线飞行特效 |
| 攻击动画委托 | ~50 | 转交给 AttackAnimationManager |
| ICombatVisuals 接口实现 | ~100 | 统一对外暴露 |

### 核心问题

1. **协作冲突率高**：不同功能修改同一文件，Git 冲突频繁
2. **测试困难**：无法单独测试洗牌动画或 Focus 系统
3. **代码重复**：`StartPeelCoroutine` 与 `TransitionFocusCoroutine` 有重复位移计算
4. **字典重建过于频繁**：`BuildCardScriptToPhysicalDictionary()` 在几乎每个方法开头被调用

---

## 二、阶段1：零风险提取（已完成）

### 2.1 已完成改动

- [x] **CardMoveConfig / CardMoveType 独立文件**
  - 新建 `Assets/Scripts/UXPrototype/CardMoveConfig.cs`
  - 从 CombatUXManager 顶部提取 85 行

- [x] **提取 DeckPositionCalculator 纯静态类**
  - 新建 `Assets/Scripts/UXPrototype/DeckPositionCalculator.cs`
  - 核心位置计算公式抽离，CombatUXManager 保留薄包装

- [x] **减少字典重建（缓存短路）**
  - 新增 `_cardScriptCacheDirty` 字段
  - `BuildCardScriptToPhysicalDictionary()` 缓存有效时直接 return
  - `GetPhysicalCardFromLogicalCard()` 增加自动重建安全网
  - 在 7 个内部修改点调用 `InvalidateCardScriptCache()`

### 2.2 阶段1 成果

| 指标 | 变化 |
|------|------|
| CombatUXManager 行数 | 2040 → **1968**（-72） |
| 职责清晰度 | 位置计算、移动配置已剥离 |
| Build 字典调用次数 | 单次操作链中从 N 次 → 1 次 |

---

## 三、阶段2：职责分离（建议执行）

### 3.1 拆分方案总览

```
CombatUXManager (MonoBehaviour)
├── [SerializeField] PhysicalCardManager
├── [SerializeField] DeckSynchronizer
├── [SerializeField] CardAnimationController
├── [SerializeField] DeckFocusController
├── [SerializeField] StatusEffectProjectileSystem
└── ICombatVisuals 实现 → 委托给各子系统
```

目标：`CombatUXManager` 只剩单例/引用字段 + 子系统组合 + 接口委托转发。

### 3.2 各子系统详细设计

#### A. PhysicalCardManager

**职责**：物理卡牌的创建、销毁、CardScript↔GameObject 字典维护

**从 CombatUXManager 提取**：
- `physicalCardsInDeck` List（改为私有，通过属性/方法访问）
- `physicalCardInRevealZone`
- `_cardScriptToPhysicalCache` + `_cardScriptCacheDirty`
- `BuildCardScriptToPhysicalDictionary()`
- `InvalidateCardScriptCache()`
- `GetPhysicalCardFromLogicalCard()`
- `GetPhysicalCard()` (ICombatVisuals)
- `AddPhysicalCardToDeck()`
- `InstantiateAllPhysicalCards()`
- `ClearAllPhysicalCards()`
- `DestroyCardWithAnimation()`
- `StopAllSpecialAnimations()`

**接口设计**：
```csharp
public class PhysicalCardManager : MonoBehaviour
{
    public IReadOnlyList<GameObject> PhysicalCardsInDeck { get; }
    public GameObject PhysicalCardInRevealZone { get; }
    
    public GameObject GetPhysicalCard(CardScript logicalCard);
    public void RegisterPhysicalCard(GameObject logicalCard, int deckIndex = 0);
    public void RemovePhysicalCard(GameObject physicalCard);
    public void ClearAll();
    public void DestroyCard(GameObject logicalCard, Action onComplete = null);
}
```

---

#### B. DeckSynchronizer

**职责**：逻辑牌组（`combinedDeckZone`）与物理牌组（`physicalCardsInDeck`）的状态同步，以及动画结果应用

**从 CombatUXManager 提取**：
- `SyncPhysicalCardsWithCombinedDeck()`
- `ApplyAnimationResult(AnimationRequest)`
- `UpdateAllPhysicalCardTargets()`
- `ReviveAllPhysicalCards()`
- `MovePhysicalCardToRevealZone()`
- `MoveRevealedCardToBottom()`
- `RebuildPhysicalDeckFromShuffledList()`

**依赖**：需要 `PhysicalCardManager` 获取映射，需要 `DeckPositionCalculator` 计算位置

---

#### C. CardAnimationController

**职责**：通用卡牌移动动画、洗牌动画

**从 CombatUXManager 提取**：
- `MoveCardWithAnimation()`
- `MoveCardToTop()` / `MoveCardToBottom()` / `MoveCardToIndex()`
- `PlayStartCardShuffleAnimation()`
- `CalculateShuffleTargets()`
- `PlayShuffleAnimationInternal()`

**依赖**：需要 `PhysicalCardManager` 查找物理卡

---

#### D. DeckFocusController

**职责**：Deck Peel / Focus 动画系统

**从 CombatUXManager 提取**：
- `FocusOnCardCoroutine()`
- `StartPeelCoroutine()`
- `TransitionFocusCoroutine()`
- `RestoreDeckFocusCoroutine()`
- `GetPhysicalCardDeckIndex()`
- `_isDeckFocused` / `_currentFocusCard` / `_deckFocusOffset` / `_peeledCards`

**关键优化**：
- `StartPeelCoroutine` 与 `TransitionFocusCoroutine` 中有大量重复代码（位移计算、DOTween 动画），提取为 `AnimateCardToPeelPosition()` 和 `AnimateCardToDeckPosition()` 私有方法

---

#### E. StatusEffectProjectileSystem

**职责**：状态效果抛物线投射物动画

**从 CombatUXManager 提取**：
- `statusEffectProjectilePrefab` 等配置字段
- `PlayStatusEffectProjectile()`
- `PlayMultiStatusEffectProjectile()`
- `GetCardWorldPosition()`

**复杂度**：低，依赖最少，可优先拆分

---

### 3.3 迁移后的 CombatUXManager

```csharp
public class CombatUXManager : MonoBehaviour, ICombatVisuals
{
    [Header("REFERENCES")]
    [SerializeField] private CombatManager combatManager;
    
    [Header("SUBSYSTEMS")]
    [SerializeField] private PhysicalCardManager physicalCardManager;
    [SerializeField] private DeckSynchronizer deckSynchronizer;
    [SerializeField] private CardAnimationController animationController;
    [SerializeField] private DeckFocusController focusController;
    [SerializeField] private StatusEffectProjectileSystem projectileSystem;
    
    // 保留的配置字段（全局动画参数）
    public float zOffset;
    public float xOffset;
    public float yOffset;
    public bool enableStageBuryAnimation;
    // ... 其他序列化字段
    
    // ICombatVisuals 实现 → 直接委托
    public void MoveCardToTop(...) => animationController.MoveCardToTop(...);
    public void ApplyAnimationResult(...) => deckSynchronizer.ApplyAnimationResult(...);
    // ...
}
```

---

## 四、阶段3：细节打磨（可选）

### 4.1 日志降噪

当前 CombatUXManager 中散布着大量 `Debug.Log` 字符串拼接。建议：

```csharp
[System.Diagnostics.Conditional("UNITY_EDITOR")]
private void LogDeckState(string context) { ... }
```

或引入统一的日志包装器，支持按模块开关。

### 4.2 DOTween 动画生命周期统一

目前 `BlockInput` / `UnblockInput` 和 `AnimationStateTracker.Register/Complete` 散落在各个动画方法中。建议封装：

```csharp
public class AnimationHandle
{
    public static AnimationHandle Begin(object owner);
    public void Complete();
    // 自动处理 input block + state tracker
}
```

### 4.3 PhysicalCardsInDeck 封装

当前 `physicalCardsInDeck` 是 `public List<GameObject>`，外部可直接修改，破坏缓存一致性。建议：

```csharp
public class PhysicalCardManager : MonoBehaviour
{
    private List<GameObject> _physicalCardsInDeck = new();
    public IReadOnlyList<GameObject> PhysicalCardsInDeck => _physicalCardsInDeck;
    
    // 所有修改必须通过受控方法
    public void InsertCard(int index, GameObject card);
    public void RemoveCard(GameObject card);
    public void ClearDeck();
}
```

---

## 五、执行优先级建议

| 优先级 | 任务 | 风险 | 预估工作量 |
|--------|------|------|-----------|
| P0 | 提取 `StatusEffectProjectileSystem` | 极低 | 1h |
| P1 | 提取 `PhysicalCardManager` + 封装 List | 低 | 2-3h |
| P2 | 提取 `DeckFocusController` | 中 | 2-3h |
| P3 | 提取 `DeckSynchronizer` | 中 | 3-4h |
| P4 | 提取 `CardAnimationController` | 中 | 2h |
| P5 | 统一 DOTween 生命周期 + 日志降噪 | 低 | 2h |

---

## 六、注意事项

1. **分步验证**：每提取一个子系统后，应在 Unity 中运行一次基础战斗流程验证
2. **保留接口**：`ICombatVisuals` 的签名在阶段2中**不应改变**，CombatUXManager 只做委托转发
3. **Editor 引用**：Scene 中 CombatUXManager 的 Inspector 引用在拆分为子组件后需要重新拖拽赋值
4. **Headless 测试**：项目已有 `NullCombatVisualsBehaviour` 用于测试，重构时应确保该路径不受影响
