# 卡片逻辑层与表现层架构整理

> 整理日期: 2026-05-01  
> 更新日期: 2026-05-02  
> 范围: `Assets/Scripts/Card/`, `Assets/Scripts/Effects/`, `Assets/Scripts/Managers/`, `Assets/Scripts/UXPrototype/`

---

## 一、当前架构全景

### 1.1 运行时双对象模型

战斗开始时，`CombatManager` 先实例化逻辑卡，随后 `CombatUXManager` 再实例化对应的物理卡。运行时始终维持两套并行的对象体系：

| 维度 | 逻辑层 (Logic Layer) | 表现层 (Presentation Layer) |
|------|----------------------|----------------------------|
| **核心类** | `CardScript` | `CardPhysObjScript` |
| **卡组列表** | `CombatManager.combinedDeckZone` | `CombatUXManager.physicalCardsInDeck` |
| **映射标识** | `cardID` | `Dictionary<CardScript, GameObject>` |
| **预制体来源** | `Assets/Prefabs/Cards/...` (设计预制体) | `Assets/Prefabs/UXPrototype/...` (视觉预制体) |
| **权威状态** | ✅ 卡组顺序、归属、费用、效果 | ✅ 屏幕位置、颜色、文字、动画 |

```
┌─────────────────────────────────────────────────────────────┐
│                      逻 辑 层                                │
│  ┌─────────────┐  ┌─────────────────────┐  ┌─────────────┐ │
│  │ CardScript  │  │ CostNEffectContainer│  │EffectScript │ │
│  │  (数据模型)  │  │ (费用→效果触发)      │  │  (效果基类)  │ │
│  └─────────────┘  └─────────────────────┘  └──────┬──────┘ │
│         ▲                                          │        │
│         │                                          ▼        │
│  ┌─────────────┐  ┌─────────────────────┐  ┌─────────────┐ │
│  │CombatManager│  │   CombatFuncs       │  │HPAlterEffect│ │
│  │ (状态机)     │  │ (中途增删卡)        │  │StageEffect  │ │
│  └─────────────┘  └─────────────────────┘  │BuryEffect   │ │
│                                            │ExileEffect  │ │
│                                            └─────────────┘ │
└─────────────────────────────────────────────────────────────┘
                              ▲
                              │ 双向直接调用 + 手动同步
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                   表 现 层                                   │
│  ┌─────────────────────┐  ┌─────────────────────────────┐  │
│  │  CombatUXManager    │  │   CardPhysObjScript         │  │
│  │  (物理卡列表/映射     │  │   (单卡视觉: 位移/变色/文字) │  │
│  │   动画/洗牌/投射物)   │  │                             │  │
│  └─────────────────────┘  └─────────────────────────────┘  │
│  ┌─────────────────────┐  ┌─────────────────────────────┐  │
│  │AttackAnimationManager│  │   CombatInfoDisplayer        │  │
│  │  (攻击冲刺动画队列)    │  │   (HP/护盾/卡组文字UI)        │  │
│  └─────────────────────┘  └─────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

### 1.2 预制体结构 (Editor-time)

```
CardDesignPrefab (GameObject + CardScript)
├── Child: "deal dmg"
│   ├── CostNEffectContainer
│   │   ├── checkCostEvent  (UnityEvent)
│   │   └── effectEvent     (UnityEvent)
│   └── HPAlterEffect
├── Child: "stage self"
│   ├── CostNEffectContainer
│   └── StageEffect
└── Child: "bury"
    ├── CostNEffectContainer
    └── BuryEffect
```

- 每个效果是一个独立的子 GameObject，通过 Inspector 中的 UnityEvent 进行绑定。
- 运行时 `Instantiate()` 后，`CardScript` 与 `CostNEffectContainer` 位于同一 prefab 实例上。

### 1.3 运行时实例化流程

1. **`CombatManager.GatherDecks()`** — 从 `DeckSO` 实例化 prefab 到 `combinedDeckZone`，设置 `myStatusRef` / `theirStatusRef`。
2. **`CombatUXManager.InstantiateAllPhysicalCards()`** — 根据 `combinedDeckZone` 创建视觉对象，建立 `_cardScriptToPhysicalCache` 映射。
3. **`CardPhysObjScript.cardImRepresenting`** — 每个物理卡持有对应的逻辑卡引用。
4. 此后两套对象通过 `Dictionary` 和 `cardImRepresenting` 双向查找。

---

## 二、耦合问题清单

### 2.1 逻辑层直接操控视觉（最严重）

逻辑效果类里直接写死了对表现层单例的调用，导致效果逻辑无法脱离视觉独立运行。

| 调用方 | 被调用方 | 具体行为 | 所在文件 |
|--------|----------|----------|----------|
| `HPAlterEffect` | `AttackAnimationManager` | `RequestAttackAnimation()` — 伤害逻辑直接请求攻击动画 | `Assets/Scripts/Effects/HPAlterEffect.cs` |
| `StageEffect` | `CombatUXManager` | `MoveCardToTop()` — 置顶逻辑直接调用飞行动画 | `Assets/Scripts/Effects/StageEffect.cs` |
| `BuryEffect` | `CombatUXManager` | `MoveCardToBottom()` — 埋底逻辑直接调用飞行动画 | `Assets/Scripts/Effects/BuryEffect.cs` |
| `ExileEffect` | `CombatUXManager` | `DestroyCardWithAnimation()` + 清空 `physicalCardInRevealZone` | `Assets/Scripts/Effects/ExileEffect.cs` |
| `EffectScript`(基类) | `CombatUXManager` / `CardPhysObjScript` | `GetPhysicalCardWorldPosition()` + `TriggerTintForStatusEffect()` | `Assets/Scripts/Effects/EffectScript.cs` |
| `CombatFuncs` | `CombatUXManager` | `AddPhysicalCardToDeck()` — 加卡逻辑必须同时 spawn 视觉对象 | `Assets/Scripts/Managers/CombatFuncs.cs` |

**影响**：
- 无法做无头逻辑测试（headless combat simulation）。
- 替换动画系统需要改动所有效果脚本。
- 逻辑层的单元测试必须 mock 整个 `CombatUXManager`。

---

### 2.2 CombatManager 既当状态机又当 UI 导演

`CombatManager` 内部直接编排 `CombatUXManager` 的调用时机，状态转换与视觉节奏强绑定。

```csharp
// CombatManager.cs 中的典型调用链
GatherDecks()
    → InstantiateAllPhysicalCards()
    → ClearAllPhysicalCards()
    → MovePhysicalCardToRevealZone()
    → PlayStartCardShuffleAnimation()
    → MoveRevealedCardToBottom()
```

**影响**：
- 修改动画时序必须改 `CombatManager`。
- 状态机代码与视觉代码混在一起，阅读和维护困难。

---

### 2.3 CardPhysObjScript 跨阶段多职责

同一个脚本同时承担战斗和商店两个阶段的职责：

| 战斗阶段 | 商店阶段 |
|----------|----------|
| 感染/力量状态染色 (`TriggerTint`) | 长按购买/出售 (`TryPurchase` / `TrySell`) |
| DOTween 位移跟随 (`SetTargetPosition`) | 点击放大查看 (`OnMouseDown`) |
| 状态效果文字显示 (`UpdateStatusEffectsDisplay`) | 价格显示 (`UpdatePriceDisplay`) |
| 稀有度星星显示 | 相机滚轮控制 |

**影响**：
- 违反单一职责原则。
- 改商店交互可能意外破坏战斗视觉。
- 测试困难（需要同时模拟两个阶段的上下文）。

---

### 2.4 费用检查里嵌了显示逻辑

`CostNEffectContainer.InvokeEffectEvent()` 中：

```csharp
// 伪代码示意 — 费用检查逻辑里判断卡片是否在 revealZone 来决定是否写失败信息
if (CombatManager.Me.revealZone != transform.parent.gameObject)
{
    // 才往 effectResultString 里追加失败提示
}
```

**影响**：
- 费用检查返回的结果类型被 UI 显示需求污染。
- `effectResultString`（UI 文本 SO）被逻辑层直接写入。

---

### 2.5 没有统一工厂

当前有三处独立实例化逻辑卡 + 物理卡的代码：

| 场景 | 逻辑卡创建 | 物理卡创建 |
|------|-----------|-----------|
| 战斗初始化 | `CombatManager.GatherDecks()` | `CombatUXManager.InstantiateAllPhysicalCards()` |
| 战斗中途加卡 | `CombatFuncs.AddCardInTheMiddleOfCombat()` | `CombatUXManager.AddPhysicalCardToDeck()` |
| 商店展示 | `ShopUXManager` 内部 | `ShopUXManager` 内部 |

**影响**：
- 任何一处忘了同步 spawn 物理卡，就会出现"有逻辑卡但看不见"的 bug。
- 新增实例化场景时需要重复写两套代码。

---

### 2.6 表现层反向控制逻辑层状态

| 调用方 | 被控制的状态 | 说明 | 状态 |
|--------|-------------|------|------|
| `CombatUXManager` | `CombatManager.blockPlayerInput` | 动画播放期间锁输入 | ✅ 已修复 |
| `AttackAnimationManager` | `CombatManager.blockPlayerInput` | 攻击动画期间锁输入 | ✅ 已修复 |
| `CardPhysObjScript` | `ShopManager.BuyFunc/SellFunc` | 视觉输入直接触发商店交易 | — |

**已修复**：`blockPlayerInput` 已改为私有 `IsInputBlocked`，表现层通过 `ICombatVisuals.BlockInput/UnblockInput` 请求锁，由 `CombatManager` 用引用计数统一管理。

**未修复**：`CardPhysObjScript` 仍直接触发商店交易（不在本次重构范围内）。

**影响**：
- ~~表现层可以直接冻结/释放玩家输入，逻辑层失去了对输入控制的权威。~~（已修复）
- 视觉组件 (`CardPhysObjScript`) 变成了输入控制器。（商店阶段仍保留）

---

## 三、整理方案

### 方案 A：事件驱动解耦（推荐，改动量中等）

**核心思路**：逻辑层只发事件，表现层订阅事件。

```
┌─────────────────────────────────────────────┐
│                  逻 辑 层                    │
│  CombatManager / Effects / CardScript        │
│       │                                      │
│       ▼ 触发事件                              │
│  ┌─────────────────┐                        │
│  │  GameEvent (SO) │  ←── onCardRevealed    │
│  │  onCardStaged   │  ←── onCardMoved       │
│  │  onCardExiled   │  ←── onDamageDealt     │
│  │  onDamageDealt  │  ←── (带 onHit 回调)    │
│  └─────────────────┘                        │
└─────────────────────────────────────────────┘
                        │
                        ▼ 订阅处理
┌─────────────────────────────────────────────┐
│                  表 现 层                    │
│  CombatUXManager / AttackAnimationManager   │
│  CardPhysObjScript / CombatInfoDisplayer    │
└─────────────────────────────────────────────┘
```

**新增事件清单**：

| 事件名 | 参数 | 替代的直接调用 |
|--------|------|---------------|
| `onCardRevealed` | `CardScript card` | `CombatManager → MovePhysicalCardToRevealZone` |
| `onCardMoved` | `CardScript card, Zone from, Zone to, Action onComplete` | `StageEffect/BuryEffect → MoveCardToTop/Bottom` |
| `onCardExiled` | `CardScript card, Action onComplete` | `ExileEffect → DestroyCardWithAnimation` |
| `onDamageDealt` | `AttackAnimData data` | `HPAlterEffect → RequestAttackAnimation` |
| `onStatusEffectApplied` | `CardScript card, StatusEffect effect` | `EffectScript → TriggerTintForStatusEffect` |
| `onCardAddedToDeck` | `CardScript card, int index` | `CombatFuncs → AddPhysicalCardToDeck` |

**优点**：
- 逻辑层完全不知道表现层存在。
- 可轻松注入 `NullVisualEventHandler` 做无头测试。
- 表现层可以按需订阅，方便扩展新视觉效果。

**缺点**：
- 需要维护事件参数结构。
- 回调链调试比直接调用复杂。

---

### 方案 B：接口抽象层（改动量较小，适合逐步迁移）

**核心思路**：不推翻现有结构，先加一层接口切断直接依赖。

```
┌─────────────────┐         ┌─────────────────────────┐
│   逻辑层         │         │      ICombatVisuals     │
│  Effects        │◄───────►│  (接口/抽象类)           │
│  CombatManager  │         │  • MoveCard()           │
│  CombatFuncs    │         │  • PlayAttackAnim()     │
│  CostNEffect... │         │  • DestroyCard()        │
└─────────────────┘         │  • ApplyTint()          │
                            └─────────────────────────┘
                                        │
                                        ▼ 实现
                            ┌─────────────────────────┐
                            │   CombatVisualManager   │
                            │  (原 CombatUXManager    │
                            │   + AttackAnimationMan) │
                            └─────────────────────────┘
```

**接口定义示意**：

```csharp
public interface ICombatVisuals
{
    void MoveCard(CardScript card, Zone from, Zone to, Action onComplete);
    void PlayAttackAnimation(CardScript attacker, CardScript target, int damage, Action onHit, Action onComplete);
    void DestroyCard(CardScript card, Action onComplete);
    void ApplyStatusTint(CardScript card, StatusEffect effect);
    void AddCard(CardScript card, int deckIndex);
    GameObject GetPhysicalCard(CardScript card);
}
```

**关键改动**：
1. 新建 `ICombatVisuals` 接口。
2. `CombatUXManager` 实现接口，把现有公开方法收敛到接口中。
3. `CombatManager` 持有 `ICombatVisuals visuals` 引用（通过 Inspector 注入或单例）。
4. 逻辑层全部改为调用 `CombatManager.visuals.xxx()` 或 `ICombatVisuals.Instance.xxx()`。

**优点**：
- 改动范围可控，逐步替换即可。
- 可注入 `NullCombatVisuals` 做无头测试。
- 不破坏现有的单例模式。

**缺点**：
- 只是封装了一层，本质上还是直接调用。
- 没有解决 "谁该控制输入锁" 这类权责问题。

---

### 方案 C：MVP 模式重构（改动量大，最干净）

**核心思路**：把 `CardScript` 变成纯数据模型，表现层通过 Presenter 驱动。

```
Model (纯数据)              Presenter (逻辑)            View (表现)
┌─────────────┐            ┌─────────────────┐        ┌─────────────────┐
│ CardDataSO  │◄───────────│ CardPresenter   │───────►│ CardView        │
│ (Scriptable)│            │ - 持有 CardData │        │ - 视觉表现       │
└─────────────┘            │ - 处理效果逻辑   │        │ - 动画播放       │
                           │ - 调用 View     │        │ - 输入响应       │
                           └─────────────────┘        └─────────────────┘
```

**优点**：
- 逻辑和表现彻底分离，可独立测试。
- 方便做多人联机（逻辑在服务器，表现层在客户端）。

**缺点**：
- 改动面太广，涉及所有卡牌预制体、效果系统、战斗状态机。
- 当前项目阶段投入产出比低。

**建议**：暂不执行，留作远期目标。

---

## 四、建议执行顺序

| 优先级 | 动作 | 预期收益 | 风险 | 估算工作量 |
|--------|------|----------|------|-----------|
| **P0** | 提取 `ICombatVisuals` 接口，让 Effect 不再直接调 `CombatUXManager` | 切断最危险的耦合 | 低，纯封装 | 1-2 天 |
| **P1** | 用事件/接口替代 `HPAlterEffect → AttackAnimationManager` 的直接调用 | 伤害逻辑和动画解耦 | 中，需要回调机制 | 1 天 |
| **P2** | `CombatManager` 不再直接调 `CombatUXManager`，改为走接口/事件 | 状态机和视觉节奏分离 | 中，需梳理所有调用点 | 2 天 |
| **P3** | 把 `CardPhysObjScript` 拆成 `CombatCardView` + `ShopCardView` | 单职责 | 中，商店和战斗都要测 | 2-3 天 |
| **P4** | 统一 `CardFactory` 负责逻辑卡+物理卡成对创建 | 消灭同步遗漏 bug | 低，集中化 | 1 天 |
| **P5** | `CostNEffectContainer` 返回 `CostResult` 结构体，UI 文字由 Presenter 写入 | 费用逻辑和显示解耦 | 低 | 0.5 天 |

---

## 五、关键文件索引

| 职责 | 文件路径 |
|------|----------|
| 逻辑卡数据 | `Assets/Scripts/Card/CardScript.cs` |
| 费用与效果触发 | `Assets/Scripts/Card/CostNEffectContainer.cs` |
| 效果基类 | `Assets/Scripts/Effects/EffectScript.cs` |
| 伤害/治疗效果 | `Assets/Scripts/Effects/HPAlterEffect.cs` |
| 置顶/埋底/放逐 | `Assets/Scripts/Effects/StageEffect.cs`, `BuryEffect.cs`, `ExileEffect.cs` |
| 战斗状态机 | `Assets/Scripts/Managers/CombatManager.cs` |
| 中途增删卡 | `Assets/Scripts/Managers/CombatFuncs.cs` |
| 攻击动画 | `Assets/Scripts/Managers/AttackAnimationManager.cs` |
| 视觉总控 | `Assets/Scripts/UXPrototype/CombatUXManager.cs` |
| 单卡视觉 | `Assets/Scripts/UXPrototype/CardPhysObjScript.cs` |
| 商店视觉 | `Assets/Scripts/UXPrototype/ShopUXManager.cs` |
| 战斗信息UI | `Assets/Scripts/Managers/CombatInfoDisplayer.cs` |

---

## 六、检查清单（实施时参考）

- [x] 任何 `Assets/Scripts/Effects/` 下的脚本不再引用 `CombatUXManager`。
- [x] `CombatManager` 中不再出现 `CombatUXManager.me.xxx()` 的直接调用。
- [x] 新增/删除卡片时，只需调用一个工厂方法，无需分别创建逻辑卡和物理卡。
- [x] 费用检查返回纯数据结构，不直接写 UI 文本。
- [x] 表现层可以在不修改逻辑层的情况下替换动画系统（通过 `ICombatVisuals` 注入）。
- [x] 输入锁由 CombatManager 统一管理，表现层通过 `ICombatVisuals.BlockInput/UnblockInput` 请求，禁止直接赋值。
- [ ] 可以运行一个不实例化物理卡的"无头"战斗测试（`NullCombatVisuals` 已实现 + 3 组单元测试）。

---

## 七、实现状态检查

> 检查日期: 2026-05-02

### 已完成的重构

| 优先级 | 重构项 | 状态 | 关键文件 |
|--------|--------|------|----------|
| P0 | 提取 `ICombatVisuals` 接口，Effect 不再直接调 `CombatUXManager` | ✅ 完成 | `ICombatVisuals.cs`, `NullCombatVisuals.cs`, `CombatUXManager.cs` |
| P1 | 用事件替代 `HPAlterEffect → AttackAnimationManager` 直接调用 | ✅ 完成 | `HPAlterEffect.cs`, `CombatManager.cs` (onDamageDealt 事件) |
| P2 | `CombatManager` 不再直接调 `CombatUXManager`，改为走接口 | ✅ 完成 | `CombatManager.cs` (visuals 属性) |
| P3 | `CardPhysObjScript` 拆分为 `CombatCardView` + `ShopCardView` | ✅ 基本完成 | `CombatCardView.cs`, `ShopCardView.cs` |
| P4 | 统一 `CardFactory` 负责逻辑卡+物理卡成对创建 | ✅ 完成 | `CardFactory.cs`, `CombatFuncs.cs` |
| P5 | `CostNEffectContainer` 返回 `CostResult` 结构体，UI 文字由 Presenter 写入 | ✅ 完成 | `CostNEffectContainer.cs`, `CombatLog.cs`, `CombatInfoDisplayer.cs` |
| — | 效果结果日志与 UI 解耦 | ✅ 完成 | 所有 Effect 子类 |
| — | 输入锁控制权责分离 | ✅ 完成 | `CombatManager.cs`, `ICombatVisuals.cs`, `CombatUXManager.cs`, `AttackAnimationManager.cs` |

### 未完成的重构

| 优先级 | 重构项 | 状态 | 问题位置 | 说明 |
|--------|--------|------|----------|------|
| — | 无头单元测试 | ❌ 未实现 | — | `NullCombatVisuals` 已实现，但项目中无实际调用它的单元测试代码；`DeckTester.cs` 是对战统计器而非单元测试 |

### 遗留细节

1. **`CardPhysObjScript` 数据残留**：仍保留 `shopItemIndex`、`holdTimeRequired`、`cardPricePrint` 等商店专属字段（虽然业务逻辑已拆分到 `ShopCardView`）。
2. **`EffectScript.GetPhysicalCardWorldPosition()`**：~~逻辑层仍依赖物理卡位置来生成粒子特效，虽然走了 `ICombatVisuals` 接口，但本质仍是逻辑层关心视觉坐标。~~ ✅ **已修复**（见下方实现说明）
3. **`CombatManager.visuals` 懒加载**：~~仍通过 `CombatUXManager.visuals` 单例获取实现，未改为 Inspector 注入或工厂创建。~~ ✅ **已修复**（见下方实现说明）

#### 遗留细节 2 实现说明

**改动文件**：
- `Assets/Scripts/Managers/ICombatVisuals.cs`
- `Assets/Scripts/UXPrototype/CombatUXManager.cs`
- `Assets/Scripts/Managers/NullCombatVisuals.cs`
- `Assets/Scripts/Managers/NullCombatVisualsBehaviour.cs`
- `Assets/Scripts/Effects/EffectScript.cs`

**实现方式**：
- `ICombatVisuals` 新增 `PlayStatusEffectParticle(CardScript, ParticleSystem, float, int)` 接口方法。
- `CombatUXManager` 实现该方法：负责查询物理卡世界坐标、`Instantiate` 粒子系统并播放。逻辑层不再直接 `Instantiate`。
- `NullCombatVisuals` / `NullCombatVisualsBehaviour` 实现空方法并记录调用日志，便于无头测试断言。
- `EffectScript` 中 `ApplyStatusEffectCore()` 的粒子生成逻辑改为调用 `CombatManager.Me?.visuals?.PlayStatusEffectParticle(...)`。
- `EffectScript.GetPhysicalCardWorldPosition()` 方法已删除，逻辑层不再关心物理坐标。

#### 遗留细节 3 实现说明

**改动文件**：
- `Assets/Scripts/Managers/CombatManager.cs`
- `Assets/Scripts/Managers/NullCombatVisualsBehaviour.cs`（新增）

**实现方式**：
- `CombatManager` 新增 `[SerializeField] MonoBehaviour visualsOverride` 字段，支持在 Inspector 中拖拽任意实现了 `ICombatVisuals` 的 `MonoBehaviour`。
- `visuals` getter 优先检查 `visualsOverride`：若不为 null 且实现了 `ICombatVisuals`，则使用 override；否则 fallback 到原有的 `CombatUXManager.visuals`。
- 新增 `NullCombatVisualsBehaviour`（`MonoBehaviour` 包装器），内部持有 `NullCombatVisuals` 实例并委托所有接口调用。无头测试场景只需创建一个空 GameObject 挂上该脚本，然后拖入 `CombatManager` 的 `visualsOverride` 槽位即可。

**使用方式**：
1. 正常战斗场景：`visualsOverride` 留空，`CombatManager` 自动使用 `CombatUXManager`（行为与之前完全一致）。
2. 无头测试场景：创建一个 GameObject 并添加 `NullCombatVisualsBehaviour` 组件，将其拖拽到 `CombatManager` 的 `Visuals Override` 字段中。
