# OneDeck - AI Agent Documentation

Unity 肉鸽卡牌游戏。双方牌组混合、洗牌后逐张翻牌触发效果。

## 核心循环

`Shop` → `Combat` → `Result` → `Shop`

## 项目结构

```
Assets/
├── Scripts/
│   ├── Managers/
│   │   ├── CombatManager.cs      # 战斗核心
│   │   ├── PhaseManager.cs       # 阶段控制
│   │   ├── ShopManager.cs        # 商店系统
│   │   └── EffectChainManager.cs # 效果链（防循环，最大深度99）
│   ├── Effects/
│   │   ├── EffectScript.cs       # 效果基类
│   │   ├── HPAlterEffect.cs      # 伤害/治疗
│   │   ├── ShieldAlter.cs        # 护盾
│   │   ├── CardManipulationEffect.cs # 放逐/置顶/置底
│   │   ├── ChangeCardTarget.cs   # 换心
│   │   └── StatusEffect/         # 状态效果
│   ├── Card/
│   │   ├── CardScript.cs         # 卡牌数据
│   │   └── CostNEffectContainer.cs # 成本检查+效果触发
│   └── SOScripts/
│       ├── GameEvent.cs          # 事件系统
│       ├── DeckSO.cs             # 牌组数据
│       └── PlayerStatusSO.cs     # 玩家状态
```

## 核心架构

- **单例模式**: `CombatManager.Me`, `ShopManager.me`
- **事件驱动**: `GameEvent` SO + `GameEventListener`
- **组件化卡牌**: CardScript + EffectContainers + Effects

## 战斗系统

### 流程
1. **GatherDecks**: 双方牌组合并，添加 Start Card 到底部
2. **Reveal**: 逐张翻牌
3. **Start Card**: 翻到时触发洗牌 + 新回合

### 区域
- `combinedDeckZone` - 合并牌组
- `revealZone` - 当前揭晓卡

### 操作
- 第1次点击：揭晓下一张卡
- 第2次点击：触发效果，卡放入牌组底部

### 疲劳
超过阈值回合后，每回合向双方添加疲劳卡

## 效果系统

### 触发流程
`CostNEffectContainer.InvokeEffectEvent()`:
1. 检查成本 (`checkCostEvent`)
2. 效果链检查 (`EffectChainManager`)
3. 执行效果 (`effectEvent`)

### 成本类型
| 方法 | 说明 |
|------|------|
| `CheckCost_Mana(n)` | 需要 n 层法力 |
| `CheckCost_Rested()` | 消耗 Rest 状态 |
| `CheckCost_Revive(n)` | 需要 n 层复活 |
| `CheckCost_HasEnemyCardInCombinedDeck(n)` | 需要 n 张敌方卡在牌组 |
| `Token Cost` | 需要并从牌组中消耗 N 张指定类型的己方卡（见下文） |

## 状态效果

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
| `Revive` | 复活 |

## 事件系统

| 事件 | 时机 |
|------|------|
| `onMeRevealed` / `onAnyCardRevealed` | 翻牌 |
| `afterShuffle` | 洗牌后 |
| `beforeRoundStart` | 回合开始前 |
| `onMyPlayerTookDmg` / `onMyPlayerHealed` | 受伤/治疗 |

## 关键类

| 类 | 路径 |
|----|------|
| `CombatManager` | `Assets/Scripts/Managers/CombatManager.cs` |
| `PhaseManager` | `Assets/Scripts/Managers/PhaseManager.cs` |
| `ShopManager` | `Assets/Scripts/Managers/ShopManager.cs` |
| `CardScript` | `Assets/Scripts/Card/CardScript.cs` |
| `CostNEffectContainer` | `Assets/Scripts/Card/CostNEffectContainer.cs` |
| `EffectScript` | `Assets/Scripts/Effects/EffectScript.cs` |
| `GameEvent` | `Assets/Scripts/SOScripts/GameEvent.cs` |

## Token Cost 机制

Token Cost 是一种特殊的预效果代价（pre-effect cost）：

- **配置位置**: `CardScript` 的 Token Cost 相关字段
  - `tokenCostCount`: 需要消耗的卡数量
  - `tokenCostCardTypeID`: 消耗的卡牌类型ID（如"fly"），空字符串表示不限制类型
  - `tokenCostOwner`: 消耗的卡牌所属（`Me`=己方, `Them`=敌方, `Random`=任意）
- **执行组件**: `TokenCostEffect` 挂载到 `CostNEffectContainer.preEffectEvent`
- **规则**:
  - 发动时从 `combinedDeckZone` 中寻找符合条件的卡
  - **必须满足**: `CardScript.isToken == true`（只有 token 卡能被消耗）
  - 需同时满足所属和类型限制
  - 符合条件的卡不足时，效果不发动
  - 多张符合条件时随机选择
- **消耗**: 符合条件的卡从牌组中移除并销毁

## 注意事项

1. **缩进**: 使用 **Tab** 而非空格
2. **HPAlterEffect**: 自动加 `baseDmg.value`，传具体值时设 `baseDmg` 为 0
3. **cardTypeID**: 用于存档/统计（非实例 ID）
4. **防循环**: 同卡不放多个循环效果实例
5. **墓地机制**: 已移除（废弃）

## 颜色标签

| 类型 | 标签 |
|------|------|
| 伤害 | `<color=red>` |
| 治疗 | `<color=#90EE90>` |
| 护盾 | `<color=grey>` |
| 数值 | `<color=yellow>` |
| 己方 | `<color=#87CEEB>` |
| 敌方 | `<color=orange>` |

## 第三方库

| 库 | 在线文档 | 本地文档 |
|----|---------|---------|
| DOTween | http://dotween.demigiant.com/documentation.php | `Assets/DOTween/DOTween.XML` |

---

**Glob**: 使用 `Assets/**/FileName.cs` 而非 `**/FileName.cs`
