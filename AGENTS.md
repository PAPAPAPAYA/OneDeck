# OneDeck - AI Agent Documentation

Unity 卡牌游戏（肉鸽牌组构建），双方牌组混合、洗牌后逐张翻牌触发效果。

## 核心循环

`Shop` → `Combat` → `Result` → `Shop`

## 项目结构

```
Assets/
├── Scripts/
│   ├── Managers/           # 单例管理器
│   │   ├── CombatManager.cs      # 战斗核心（GatherDeck → Shuffle → Reveal）
│   │   ├── PhaseManager.cs       # 阶段控制
│   │   ├── ShopManager.cs        # 商店系统
│   │   ├── EffectChainManager.cs # 效果链（防循环，最大深度99）
│   │   └── WriteRead/            # 数据持久化
│   ├── Effects/            # 卡牌效果
│   │   ├── EffectScript.cs       # 基类
│   │   ├── HPAlterEffect.cs      # 伤害/治疗
│   │   ├── ShieldAlter.cs        # 护盾
│   │   ├── CardManipulationEffect.cs # 放逐/复活/置顶/置底
│   │   ├── ChangeCardTarget.cs   # 换心
│   │   └── StatusEffect/         # 状态效果
│   ├── Card/
│   │   ├── CardScript.cs         # 卡牌数据
│   │   └── CostNEffectContainer.cs # 成本检查+效果触发
│   └── SOScripts/          # ScriptableObject
│       ├── GameEvent.cs          # 事件系统
│       ├── DeckSO.cs             # 牌组数据
│       └── PlayerStatusSO.cs     # 玩家状态
├── Prefabs/Cards/          # 卡牌预制体（按类型分目录）
└── SORefs/                 # SO 实例（Events、Decks、Refs）
```

## 核心架构

- **单例模式**: `CombatManager.Me`, `ShopManager.me` 等
- **事件驱动**: `GameEvent` SO + `GameEventListener` 组件
- **组件化卡牌**: CardScript + EffectContainers + Effects

## 关键系统

### 1. 战斗系统
状态: `GatherDeckLists` → `ShuffleDeck` → `Reveal`

区域:
- `combinedDeckZone` - 合并牌组
- `revealZone` - 当前揭示卡
- `graveZone` - 墓地

疲劳: 超过阈值回合后，每回合向双方添加疲劳卡

### 2. 效果系统

**触发流程**: `CostNEffectContainer.InvokeEffectEvent()`
1. 检查成本 (`checkCostEvent`)
2. 效果链检查 (`EffectChainManager`)
3. 执行效果 (`effectEvent`)

**成本类型**:
- `CheckCost_Mana(n)` - 需要 n 层法力
- `CheckCost_Rested()` - 消耗 Rest 状态
- `CheckCost_InGrave()` - 必须在墓地
- `CheckCost_Revive(n)` - 需要 n 层复活
- `CheckCost_HasEnemyCardInCombinedDeck(n)` - 需要 n 张敌方卡在牌组
- `CheckCost_HasOwnerCardInGrave(n)` - 需要 n 张己方卡在墓地

### 3. 事件系统

事件方法:
- `Raise()` - 全局
- `RaiseOwner()` - 拥有者
- `RaiseOpponent()` - 敌方
- `RaiseSpecific(card)` - 特定卡牌

常用事件:
| 事件 | 时机 |
|------|------|
| `onMeRevealed` / `onAnyCardRevealed` | 翻牌 |
| `onMeSentToGrave` / `onAnyCardSentToGrave` | 进墓地 |
| `onAnyCardRevived` | 复活 |
| `afterShuffle` | 洗牌后 |
| `beforeRoundStart` | 回合开始前 |
| `onMyPlayerTookDmg` / `onTheirPlayerTookDmg` | 受伤 |
| `onMyPlayerHealed` / `onTheirPlayerHealed` | 治疗 |

### 4. 状态效果

```csharp
enum StatusEffect { None, Infected, Mana, HeartChanged, Power, Rest, Revive }
enum Tag { None, Linger, ManaX }
```

| 效果 | 说明 |
|------|------|
| `Infected` | 感染 |
| `Mana` | 法力（资源） |
| `Power` | 力量（伤害+1） |
| `HeartChanged` | 换心（归属改变） |
| `Rest` | 休息（跳过触发） |
| `Revive` | 复活（墓地可触发） |

### 5. 数据统计

| 系统 | 功能 | 快捷键 |
|------|------|--------|
| `DeckSaver` | 卡组存档/加载 | `Ctrl+S/L/W/D` |
| `CardWinRateTracker` | 单卡胜率统计 | `Ctrl+Shift+P/E/C` |
| `ShopStatsManager` | 商店购买统计 | `Ctrl+Shift+P/E/R` |

## 关键类参考

| 类 | 路径 |
|----|------|
| `CombatManager` | `Assets/Scripts/Managers/CombatManager.cs` |
| `PhaseManager` | `Assets/Scripts/Managers/PhaseManager.cs` |
| `ShopManager` | `Assets/Scripts/Managers/ShopManager.cs` |
| `EffectChainManager` | `Assets/Scripts/Managers/EffectChainManager.cs` |
| `CardScript` | `Assets/Scripts/Card/CardScript.cs` |
| `CostNEffectContainer` | `Assets/Scripts/Card/CostNEffectContainer.cs` |
| `EffectScript` | `Assets/Scripts/Effects/EffectScript.cs` |
| `HPAlterEffect` | `Assets/Scripts/Effects/HPAlterEffect.cs` |
| `CardManipulationEffect` | `Assets/Scripts/Effects/CardManipulationEffect.cs` |
| `GameEvent` | `Assets/Scripts/SOScripts/GameEvent.cs` |
| `DeckSaver` | `Assets/Scripts/Managers/WriteRead/DeckSaver.cs` |

## 重要注意事项

1. **HPAlterEffect 伤害**: 所有伤害方法自动加 `baseDmg.value`，传入具体值时请将 `baseDmg` 设 0
2. **cardTypeID**: 使用此字段而非实例 ID 进行存档/统计
3. **可循环效果**: 同一卡牌不要放多个带循环效果的实例，会堆栈溢出
4. **修改牌组后**: 必须重新洗牌才能生效
5. **牌组/墓地效果**: 不显示失败信息（设计选择）

## 颜色标签规范

| 类型 | 标签 |
|------|------|
| 伤害 | `<color=red>` |
| 治疗 | `<color=#90EE90>` |
| 护盾 | `<color=grey>` |
| 数值 | `<color=yellow>` |
| 己方 | `<color=#87CEEB>` |
| 敌方 | `<color=orange>` |

## 第三方库文档

| 库 | 在线文档 | 本地文档 |
|----|---------|---------|
| DOTween | http://dotween.demigiant.com/documentation.php | `Assets/DOTween/DOTween.XML` |

---

**Glob 搜索**: 使用 `Assets/**/FileName.cs` 而非 `**/FileName.cs`
