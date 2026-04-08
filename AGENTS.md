# OneDeck - AI Agent Documentation

Unity 肉鸽卡牌游戏。双方牌组混合、洗牌后逐张翻牌触发效果。

## 开发规范

| 项目 | 要求 |
|------|------|
| **换行符** | `\r\n` (CRLF) |
| **缩进** | Tab (`\t`)，严禁空格 |
| **命令分隔** | PowerShell 用 `;` 而非 `&&` |

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
│   │   └── EffectChainManager.cs # 效果链（最大深度99）
│   ├── Effects/
│   │   ├── EffectScript.cs       # 效果基类
│   │   ├── HPAlterEffect.cs      # 伤害/治疗
│   │   ├── ShieldAlter.cs        # 护盾
│   │   ├── CardManipulationEffect.cs # 放逐/置顶/置底
│   │   ├── MinionCostEffect.cs   # Minion Cost
│   │   └── BuryCostEffect.cs     # Bury Cost
│   ├── Card/
│   │   ├── CardScript.cs         # 卡牌数据
│   │   └── CostNEffectContainer.cs # 成本检查+效果触发
│   └── SOScripts/
│       ├── GameEvent.cs          # 事件系统
│       └── PlayerStatusSO.cs     # 玩家状态
├── Prefabs/
│   └── Cards/
│       └── 3.0 no cost (current)  # 当前在用卡片
```

## 核心架构

- **单例**: `CombatManager.Me`, `ShopManager.me`
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

## 效果系统

### 触发流程
`CostNEffectContainer.InvokeEffectEvent()`: 检查成本 → 效果链检查 → 执行效果

### 成本类型
| 方法 | 说明 |
|------|------|
| `Mana(n)` | 需要 n 层法力 |
| `Rested()` | 消耗 Rest 状态 |
| `Revive(n)` | 需要 n 层复活 |
| `HasEnemyCard(n)` | 需要 n 张敌方卡在牌组 |
| `Token Cost` | 消耗 N 张指定类型己方 Minion 卡 |
| `Bury Cost` | 发动时将 N 张己方卡置底 |

### 状态效果
```csharp
enum StatusEffect { None, Infected, Mana, HeartChanged, Power, Rest, Revive }
```

| 效果 | 说明 |
|------|------|
| `Power` | 伤害+1 |
| `HeartChanged` | 归属改变 |
| `Rest` | 跳过触发 |

### 事件
| 事件 | 时机 |
|------|------|
| `onMeRevealed` | 翻牌 |
| `afterShuffle` | 洗牌后 |
| `onMyPlayerTookDmg` | 受伤 |

## 关键类

| 类 | 路径 |
|----|------|
| `CombatManager` | `Assets/Scripts/Managers/CombatManager.cs` |
| `HPAlterEffect` | `Assets/Scripts/Effects/HPAlterEffect.cs` |
| `CardScript` | `Assets/Scripts/Card/CardScript.cs` |
| `CombatUXManager` | `Assets/Scripts/UXPrototype/CombatUXManager.cs` |

## Minion Cost 机制

发动时从 `combinedDeckZone` 中消耗 N 张符合条件的 Minion 卡（`isMinion == true`）。条件不足则效果不发动。

## 动画系统

### 攻击动画
`AttackAnimationManager` 队列播放，流程：放大旋转 → 冲撞 → 回弹 → 伤害计算。
- Status Effect 伤害设置 `isStatusEffectDamage = true` 跳过动画

### 卡片移动
`CombatUXManager` 提供方法：
- `MoveCardToBottom(card, onComplete)` - 置底
- `MoveCardToTop(card, onComplete)` - 置顶
- `MoveCardToIndex(card, index)` - 指定位置

### Start Card + Shuffle
使用 `PlayStartCardExitWithShuffleAnimation()` 实现 Start Card 退场与 Shuffle 同时进行。

## 注意事项

1. **HPAlterEffect**: 自动加 `baseDmg.value`，传具体值时设 `baseDmg` 为 0
2. **cardTypeID**: 用于存档/统计（非实例 ID）
3. **防循环**: 同卡不放多个循环效果实例

## 颜色标签

| 类型 | 标签 |
|------|------|
| 伤害 | `<color=red>` |
| 治疗 | `<color=#90EE90>` |
| 护盾 | `<color=grey>` |
| 己方 | `<color=#87CEEB>` |
| 敌方 | `<color=orange>` |

---

**Glob**: 使用 `Assets/**/FileName.cs` 而非 `**/FileName.cs`
