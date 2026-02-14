# OneDeck - AI Agent Documentation

OneDeck 是一个 Unity 卡牌游戏（肉鸽牌组构建），特色是双方牌组混合、洗牌后逐张翻牌触发效果。使用 ScriptableObject 事件系统和 UnityEvents 实现灵活的卡牌效果组合。

## 核心循环

1. **商店阶段**: 购买/出售卡牌构建牌组
2. **战斗阶段**: 混合双方牌组，逐张翻牌触发效果
3. **结果阶段**: 胜负结算，记录心和胜场

## 技术栈

- Unity 6000.0.x (Unity 6)
- Universal Render Pipeline (URP) 17.0.4
- TextMesh Pro

## 项目结构

```
Assets/
├── Scripts/
│   ├── Managers/           # 单例管理器
│   ├── Effects/            # 卡牌效果实现
│   ├── Card/               # 核心卡牌组件
│   ├── SOScripts/          # ScriptableObject 定义
│   └── Editor/             # 编辑器工具
├── Prefabs/Cards/          # 按类型组织的卡牌预制体
├── SORefs/                 # SO 实例（GameEvents、Decks、PlayerRefs等）
└── Scenes/
```

## 核心架构

### 设计模式
- **单例模式**: 所有主要管理器
- **ScriptableObject 模式**: 游戏事件、玩家状态、牌组数据
- **事件驱动架构**: 自定义 GameEvent SO
- **组件化卡牌系统**: 多效果组件组合

### 核心系统

#### 1. 战斗系统 (`CombatManager`)
状态: `GatherDeckLists` → `ShuffleDeck` → `Reveal`
- 合并双方牌组
- 管理区域: 合并牌组、揭示区、墓地
- 超时机制: 一定回合后加入疲劳卡

#### 2. 阶段系统 (`PhaseManager`)
循环: `Shop` → `Combat` → `Result` → `Shop`

#### 3. 效果系统
- **EffectChainManager**: 防止无限循环（最大深度99），追踪效果链
- **CostNEffectContainer**: 成本检查与效果触发
- **EffectScript**: 所有效果的基类

#### 4. 事件系统 (`GameEvent`)
事件类型:
- `RaiseSpecific(card)` - 特定卡牌
- `RaiseOwner()` - 拥有者卡牌
- `RaiseOpponent()` - 敌方卡牌  
- `Raise()` - 全局

常用事件:
- `onMeRevealed` / `onAnyCardRevealed` - 翻牌
- `onMeSentToGrave` / `onAnyCardSentToGrave` - 进入墓地
- `onAnyCardRevived` - 从墓地复活
- `afterShuffle` - 洗牌后
- `onMyPlayerTookDmg` / `onTheirPlayerTookDmg` - 受伤事件

#### 5. 状态效果系统
状态效果枚举 (`StatusEffect`):
- `None`, `Infected`, `Mana`, `Power`, `HeartChanged`, `Rest`, `Revive`

**状态效果解析器** (`ResolverScript`): 附加到卡牌上监听事件

#### 6. 商店系统 (`ShopManager`)
- 购买: 数字键 1-6
- 出售: S 切换出售模式，然后数字键
- 重Roll: R
- Ctrl+S / Ctrl+L / Ctrl+W - 保存/加载/清除牌组

## 关键枚举

```csharp
GamePhase { Combat, Shop, Result }
CombatState { GatherDeckLists, ShuffleDeck, Reveal }
TargetType { Me, Them, Random }
StatusEffect { None, Infected, Mana, HeartChanged, Power, Rest, Revive }
```

## 卡牌结构

```
CardPrefab
├── CardScript              # 核心数据
├── GameEventListener(s)    # 事件监听
└── EffectContainer(s)
    ├── CostNEffectContainer  # 成本与效果
    └── Effect Script(s)      # 效果逻辑
```

### CardScript 关键属性
```csharp
int cardID;                    // 唯一ID
string cardDesc;               // 卡牌描述（支持颜色标签）
bool takeUpSpace;              // 是否占用牌组空间
int price;                     // 价格
PlayerStatusSO myStatusRef;    // 拥有者状态
List<StatusEffect> myStatusEffects; // 状态效果列表
```

### 描述颜色规范

| 类型 | 标签 | 用途 |
|------|------|------|
| 红色 | `<color=red>` | 伤害数值 |
| 浅绿 | `<color=#90EE90>` | 治疗数值 |
| 灰色 | `<color=grey>` | 护盾数值 |
| 黄色 | `<color=yellow>` | 其他数值（费用、数量等）|
| 浅蓝 | `<color=#87CEEB>` | 玩家/己方相关 |
| 橙色 | `<color=orange>` | 敌方相关 |

## 核心效果类型

| 效果 | 描述 |
|------|------|
| `HPAlterEffect` | 伤害/治疗（支持Power加成、护盾处理）|
| `ShieldAlter` | 增减护盾 |
| `CardManipulationEffect` | 移动卡牌: Stage(置顶), Bury(置底), Exile(放逐), Revive(复活) |
| `ChangeCardTarget` | 改变卡牌归属（换心）|
| `AddTempCard` | 生成临时卡 |
| `StatusEffectGiverEffect` | 基础状态效果类 |
| `InfectionEffect` / `ManaAlterEffect` / `GivePowerStatusEffectEffect` | 具体状态效果 |

### 成本类型 (CostNEffectContainer)
- `CheckCost_Mana(int)` - 需要X点法力
- `CheckCost_InGrave()` - 必须在墓地
- `CheckCost_Rested()` - 需要无Rest状态
- `CheckCost_HasEnemyCardInCombinedDeck(int)` - 需要X张敌方卡在合并牌组中
- 等等

## 重要实现注意事项

1. **效果链安全**: 
   - 同卡牌不同效果、等待玩家输入、链深度>99 时关闭链
   - 通过 `EffectRecorder` GameObjects 追踪

2. **禁止 `beforeIDealDmg` 事件**: 已移除以防止堆栈溢出；`HPAlterEffect` 在造成伤害前计算伤害

3. **状态效果伤害归属**: 计入状态效果拥有者的卡牌伤害

4. **实例ID警告**: 卡牌ID 43514 是 `[Meditate]`，注意实例ID变化可能影响存档

5. **换心策略**: 全是换心卡会过强，需设计成本

6. **可循环效果警告**: 同一卡牌不要放多个带可循环效果的实例，会堆栈溢出；多个可循环效果放同一实例内

7. **牌组/墓地效果**: 不显示失败信息（设计选择）

8. **修改牌组后**: 必须重新洗牌

## 快速参考

| 用途 | 路径 |
|------|------|
| 战斗逻辑 | `Assets/Scripts/Managers/CombatManager.cs` |
| 阶段控制 | `Assets/Scripts/Managers/PhaseManager.cs` |
| 卡牌定义 | `Assets/Scripts/Card/CardScript.cs` |
| 效果基类 | `Assets/Scripts/Effects/EffectScript.cs` |
| 事件SO | `Assets/Scripts/SOScripts/GameEvent.cs` |
| 牌组SO | `Assets/Scripts/SOScripts/DeckSO.cs` |
| 存档系统 | `Assets/TestWriteRead/DeckSaver.cs` |
| 开发日志 | `Assets/DevLog.cs` |

---

## AI Agent 工具规范

**Glob 搜索:**
- ❌ 不要用 `**/FileName.cs`（搜索整个项目）
- ✅ 用 `Assets/**/FileName.cs`（限定范围）

**PowerShell:**
```powershell
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; $OutputEncoding = [System.Text.Encoding]::UTF8
```
- 避免用 `findstr`，改用 `Select-String` 或 Grep 工具
