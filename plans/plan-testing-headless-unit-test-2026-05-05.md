# 无头单元测试实施计划

> 目标：利用已实现的 `NullCombatVisuals` + `ICombatVisuals` 接口，搭建可在 Edit Mode 下独立运行的无头战斗单元测试。  
> 前提：文档 `Architecture_CardLogic_vs_Presentation.md` 中的 P0~P5 重构已完成，`NullCombatVisuals` / `NullCombatVisualsBehaviour` 已存在，但项目中尚无调用它们的单元测试。

---

## 一、现状诊断

| 项目 | 现状 | 问题 |
|------|------|------|
| `CardEffectUnitTests.cs` | 手写静态 `Run()` 方法 | 非 NUnit 标准测试；仍创建 `CombatUXManager` + 物理卡；需手动调用 |
| `NullCombatVisuals.cs` | 已实现 | 没有任何测试在使用它 |
| `CombatManager.visualsOverride` | 仅支持 Inspector 注入（`[SerializeField] private`） | 代码中无法直接注入，测试里只能用反射 hack |
| 测试框架 | `com.unity.test-framework` 1.6.0 已安装 | 只有空壳 `PlayModeTests.asmdef`，无 EditMode 测试，且未引用 `Assembly-CSharp` |
| 依赖链 | `CombatManager` → `CombatInfoDisplayer`（需要 `TextMeshProUGUI`） | EditMode 下若 UI 字段为 null 会 NullRef |

---

## 二、总体思路

```
┌─────────────────────────────────────────────────────────────┐
│                     Edit Mode Test Runner                    │
│                          (NUnit)                             │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│              HeadlessCombatTestFixture (基类)                │
│  • SetUp:   创建 CombatManager + 注入 NullCombatVisuals      │
│  • SetUp:   创建所有必需单例 (GES/VTM/ECM/Log/Factory)       │
│  • SetUp:   创建 dummy UI 对象避免 NullRef                   │
│  • TearDown: 销毁所有 GameObject + 清理单例                  │
│  • Helpers: CreateCard() / CreateEffect<T>() / Reveal()      │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│              具体测试类 (继承基类)                            │
│  • EffectTests:   HPAlter, Stage, Bury, Exile, Curse...      │
│  • CostTests:     Mana, Minion, Bury, Delay, Expose...       │
│  • FlowTests:     GatherDecks → Reveal → Trigger → Shuffle   │
│  • ChainTests:    效果链深度、循环守卫、同卡多效果...         │
└─────────────────────────────────────────────────────────────┘
```

---

## 三、实施步骤

### Step 1：创建 Edit Mode 测试程序集

**目标**：让测试代码能访问项目主程序（`Assembly-CSharp`）。

| 动作 | 文件 |
|------|------|
| 新建文件夹 | `Assets/Tests/EditMode/` |
| 创建 asmdef | `Assets/Tests/EditMode/EditModeTests.asmdef` |

**`EditModeTests.asmdef` 关键配置**：
- `references`: `UnityEngine.TestRunner`, `UnityEditor.TestRunner`, `Assembly-CSharp`
- `defineConstraints`: `UNITY_INCLUDE_TESTS`
- **不要**勾选 `Test Assemblies`（Unity 2023+ 的 UI 选项），保持 `autoReferenced: true`

> 注：现有 `PlayModeTests.asmdef` 无引用且无实际测试代码，可保留或后续合并。

---

### Step 2：给 `CombatManager` 添加代码注入入口（最小改动）

**目标**：让测试基类可以在代码中注入 `NullCombatVisuals`，而不必依赖反射。

**改动文件**：`Assets/Scripts/Managers/CombatManager.cs`

**新增方法**（放在 `visuals` property 下方）：

```csharp
/// <summary>
/// Test-only helper to inject a custom ICombatVisuals implementation.
/// Clears the cached visuals reference so the new one takes effect immediately.
/// </summary>
public void SetVisualsOverride(ICombatVisuals visuals)
{
    _visuals = visuals;
}
```

**影响**：零行为变更；正常场景不会调用此方法。

---

### Step 3：编写测试基类 `HeadlessCombatTestFixture`

**目标**：一次性解决所有依赖搭建和清理问题，让具体测试只关心业务断言。

**文件**：`Assets/Tests/EditMode/HeadlessCombatTestFixture.cs`

**核心职责**：

| 阶段 | 内容 |
|------|------|
| `[SetUp]` | 1. 清理残留单例（DestroyImmediate + 置 null）<br>2. 创建 `PlayerStatusSO` ×2（owner / enemy，HP=100）<br>3. 创建 `CombatManager` GameObject + `AddComponent<CombatManager>`<br>4. 注入 `new NullCombatVisuals()` 到 `CombatManager.SetVisualsOverride()`<br>5. 创建 `GameEventStorage`、`ValueTrackerManager`、`EffectChainManager`、`CombatLog`、`CardFactory`、`DeckTester`（部分效果依赖 `DeckTester.me.autoSpace`）<br>6. 创建 dummy `TextMeshProUGUI` 对象并绑定到 `CombatInfoDisplayer` 的各显示字段（防止 `RefreshDeckInfo` 等 NullRef）<br>7. 创建 `GamePhaseSO` 并设为 `Shop`（防止 `CombatManager.Update` 进入 Combat 状态机干扰测试）<br>8. 初始化 `EffectChainManager` 的 recorder 列表和 prefab |
| `[TearDown]` | 1. `Object.DestroyImmediate` 所有创建的 GameObject<br>2. 所有静态单例置 null |
| Helper: `CreateCard(...)` | 创建 GameObject + `CardScript`，设置 owner/enemy status、可选 cardTypeID、isMinion、statusEffects、tags |
| Helper: `CreateEffect<T>(...)` | 在指定 card 下创建子 GameObject + Effect 组件，通过反射注入 `myCard` / `myCardScript` / `combatManager` |
| Helper: `CreateCostContainer(...)` | 创建 `CostNEffectContainer` 并注入 `_myCardScript` |
| Helper: `RevealTopCard()` | 模拟抽顶：将 `combinedDeckZone[^1]` 移入 `revealZone`，触发相关事件 |
| Helper: `TriggerRevealedCard()` | 模拟玩家点击触发：调用 `TriggerRevealedCardEffect` 逻辑 |

**关于 dummy UI**：
- `CombatInfoDisplayer` 在 `GatherDecks()` 和 `RevealCards()` 中会被调用 `RefreshDeckInfo()`、`ShowCardInfo()`、`ClearInfo()`。
- 这些方法会读写 `TextMeshProUGUI.text`，只要字段不为 null 即可安全执行（不需要 Canvas）。
- 因此基类 SetUp 中只需 `new GameObject("DummyText").AddComponent<TextMeshProUGUI>()` 并赋值给各字段。

---

### Step 4：迁移现有 `CardEffectUnitTests` → NUnit 无头测试

**目标**：把现有 `CardEffectUnitTests.cs` 中的 12 个用例全部迁移到 EditMode 测试，并去掉 `CombatUXManager` 的创建。

**现有用例清单**：
- `DEATHBED_CURSE A-1` ~ `A-4`
- `FALL_INTO_RIFT A-1` ~ `A-3`
- `RIFT A-1` ~ `A-3`

**迁移动作**：
1. 新建 `Assets/Tests/EditMode/EffectTests/DeathbedCurseTests.cs`
2. 新建 `Assets/Tests/EditMode/EffectTests/FallIntoRiftTests.cs`
3. 新建 `Assets/Tests/EditMode/EffectTests/RiftTests.cs`
4. 每个用例改为独立的 `[Test]` 方法，继承 `HeadlessCombatTestFixture`
5. 用基类 helper 替代手写 Reflection 注入
6. **去掉** `CombatUXManager` 和 `CardPhysObjScript` 的创建代码
7. 断言方式保持 `Assert.That(..., Is.True)` 或 `Assert.AreEqual`

**迁移后验证**：
- 在 Unity `Test Runner` 窗口运行 EditMode 测试，确认全部通过。
- 原 `Assets/Scripts/CardEffectUnitTests.cs` 可保留作为遗留手动测试，或标记 `[Obsolete]`。

---

### Step 5：新增无头测试覆盖

#### 5A. 战斗流程测试 (`CombatFlowTests.cs`)

| 用例 | 断言 |
|------|------|
| `GatherDecks_CombinesPlayerAndEnemyDecks` | `combinedDeckZone.Count` == playerDeck.Count + enemyDeck.Count + 1 (Start Card) |
| `GatherDecks_StartCardIsAtBottom` | `combinedDeckZone[0]` 是 Start Card |
| `RevealNextCard_MovesTopCardToRevealZone` | revealZone != null，combinedDeckZone.Count 减 1 |
| `TriggerNormalCard_RaisesOnAnyCardRevealed` | `GameEventStorage.me.onAnyCardRevealed` 被触发（可用 mock listener 或 flag） |
| `TriggerStartCard_ShufflesDeckAndIncrementsRound` | roundNumRef.value 增加，combinedDeckZone 顺序改变，cardsRevealedThisRound 归零 |
| `PutRevealedCardToBottom_MovesCardToIndexZero` | revealZone 变 null，combinedDeckZone[0] 是刚才的 revealZone card |

#### 5B. 费用检查测试 (`CostCheckTests.cs`)

| 用例 | 断言 |
|------|------|
| `ManaCost_Met_WhenEnoughManaStacks` | `CostNEffectContainer.CheckCost()` 返回成功 |
| `ManaCost_NotMet_WhenInsufficientMana` | 返回失败，且 `CostCheckResult` 包含正确失败信息 |
| `MinionCost_ConsumesFriendlyMinion` | 调用后 combinedDeckZone 中 minion 数量减少 |
| `BuryCost_PlacesFriendlyCardsAtBottom` | 调用后指定数量的友方卡被移到 index 0 |
| `DelayCost_SkipsCardsByOnePosition` | 被 delay 的卡位置后移 1 位 |
| `ExposeCost_MovesEnemyCardsToTop` | 被 expose 的敌方卡被移到 deck 顶 |

#### 5C. 效果链与防循环测试 (`EffectChainTests.cs`)

| 用例 | 断言 |
|------|------|
| `SameEffectID_CannotBeInvokedTwiceInOpenChain` | 第二次调用被阻断 |
| `ChainDepthExceeds99_BlocksFurtherEffects` | depth > 99 后效果不再执行 |
| `DifferentEffectObjects_StartNewChain` | 新 chain 正常开启，旧 chain 关闭 |

#### 5D. `NullCombatVisuals` 调用记录测试 (`VisualsCallTests.cs`)

| 用例 | 断言 |
|------|------|
| `StageEffect_CallsMoveCardToTop` | `NullVisuals.moveCardToTopCalls` == 1 |
| `BuryEffect_CallsMoveCardToBottom` | `NullVisuals.moveCardToBottomCalls` == 1 |
| `ExileEffect_CallsDestroyCard` | `NullVisuals.destroyCardCalls` == 1 |
| `HPAlterEffect_CallsPlayAttackAnimation` | `NullVisuals.playAttackAnimCalls` == 1 |
| `CombatManager_GatherDecks_DoesNotInstantiatePhysicalCards` | `NullVisuals.InstantiateAllPhysicalCards` 未被调用（GatherDecks 本身不调用，Reveal 时才调用） |

---

### Step 6：运行入口与 CI 友好化

| 动作 | 说明 |
|------|------|
| EditMode Test Runner | 在 Unity `Window → General → Test Runner` 中直接运行 |
| 菜单项快捷运行 | 可选：在 `Assets/Scripts/Editor/` 中新增 `RunHeadlessTestsMenu.cs`，调用 `UnityEditor.TestTools.TestRunner.Api` 一键执行 EditMode 测试 |
| 命令行 / CI | 使用 Unity CLI `-runTests -testPlatform EditMode -testResults result.xml` 即可在 CI 中运行 |

---

## 四、文件结构

```
Assets/
├── Scripts/
│   ├── Managers/
│   │   └── CombatManager.cs              (+ SetVisualsOverride)
│   └── CardEffectUnitTests.cs            (保留或标记 obsolete)
└── Tests/
    ├── PlayMode/
    │   └── PlayModeTests.asmdef          (已有)
    └── EditMode/
        ├── EditModeTests.asmdef
        ├── HeadlessCombatTestFixture.cs  (测试基类 + helpers)
        ├── EffectTests/
        │   ├── DeathbedCurseTests.cs
        │   ├── FallIntoRiftTests.cs
        │   ├── RiftTests.cs
        │   ├── HPAlterTests.cs
        │   ├── StageBuryExileTests.cs
        │   └── StatusEffectTests.cs
        ├── CostTests/
        │   └── CostCheckTests.cs
        ├── FlowTests/
        │   └── CombatFlowTests.cs
        └── ChainTests/
            └── EffectChainTests.cs
```

---

## 五、风险与对策

| 风险 | 对策 |
|------|------|
| `TextMeshProUGUI` 在 EditMode 下 AddComponent 失败 | 预先用一个小脚本验证；若失败则改用反射给字段赋 `new FakeTextMeshPro()`（继承 `TextMeshProUGUI` 的空类） |
| `EffectScript` 内部仍有未被接口化的直接调用 | 运行测试时若报 `CombatUXManager` null，定位到该调用点并补走 `ICombatVisuals` |
| `DeckTester.me` 被某些效果硬引用 | 基类 SetUp 中继续创建 dummy `DeckTester`，但不做任何物理卡初始化 |
| 某些效果依赖 `GameEvent` 的 UnityEvent Inspector 绑定 | 测试中通过代码直接 `AddListener` 或反射设置委托；若效果使用 `Raise()` / `RaiseSpecific()`，只要事件 SO 实例存在即可工作 |
| `CardFactory.CreateLogicalCard` 内部 `Instantiate(prefab)` 在 EditMode 下可能异常 | 这是正常的，测试中应传入简单的 `GameObject`（如 `new GameObject("CardPrefab")` + `AddComponent<CardScript>()`）作为 prefab，而非引用 Assets 中的 Prefab 文件 |

---

## 六、工作量估算

| 步骤 | 估算 |
|------|------|
| Step 1: 创建 EditMode 程序集 | 10 分钟 |
| Step 2: `CombatManager` 添加注入方法 | 5 分钟 |
| Step 3: 编写 `HeadlessCombatTestFixture` | 2 小时 |
| Step 4: 迁移现有 12 个用例 | 1.5 小时 |
| Step 5: 新增测试（约 15~20 个用例） | 3 小时 |
| Step 6: 菜单项 + 验证全部通过 | 30 分钟 |
| **总计** | **约 1 个工作日** |

---

## 七、验收标准

- [ ] `Window → Test Runner → EditMode` 中能看到所有测试类。
- [ ] 运行全部 EditMode 测试，通过率为 100%。
- [ ] 测试运行期间**不创建任何物理卡**（`NullCombatVisuals.InstantiateAllPhysicalCards` 仅在 `CombatManager.RevealCards` 正常流程中被调用一次，但不会产生真实 GameObject）。
- [ ] 测试运行期间**不依赖任何场景**（纯代码创建对象）。
- [ ] 原 `CardEffectUnitTests.Run()` 的所有旧用例逻辑，在新测试中有等价覆盖。
- [ ] `Architecture_CardLogic_vs_Presentation.md` 检查清单最后一项 `[ ] 可以运行一个不实例化物理卡的"无头"战斗测试` 更新为 `[x]`。
