# OneDeck - AI Agent Documentation

Unity 肉鸽卡牌游戏。双方牌组混合、洗牌后逐张翻牌触发效果。

## 开发注意事项

### PowerShell 命令分隔符
Windows PowerShell 不支持 `&&` 连接命令，应使用 `;` 分隔：
```powershell
# ❌ 错误
command1 && command2

# ✅ 正确
command1 ; command2
```

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
│   │   ├── MinionCostEffect.cs   # Minion Cost
│   │   ├── BuryCostEffect.cs     # Bury Cost
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
| `Bury Cost` | 发动时将 N 张己方卡置底（见下文） |

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
| `MinionCostEffect` | `Assets/Scripts/Effects/MinionCostEffect.cs` |
| `BuryCostEffect` | `Assets/Scripts/Effects/BuryCostEffect.cs` |
| `GameEvent` | `Assets/Scripts/SOScripts/GameEvent.cs` |

## Minion Cost 机制

Minion Cost 是一种特殊的预效果代价（pre-effect cost）：

- **配置位置**: `CardScript` 的 Minion Cost 相关字段
  - `minionCostCount`: 需要消耗的卡数量
  - `minionCostCardTypeID`: 消耗的卡牌类型ID（如"fly"），空字符串表示不限制类型
  - `minionCostOwner`: 消耗的卡牌所属（`Me`=己方, `Them`=敌方, `Random`=任意）
- **执行组件**: `MinionCostEffect` 挂载到 `CostNEffectContainer.preEffectEvent`
- **规则**:
  - 发动时从 `combinedDeckZone` 中寻找符合条件的卡
  - **必须满足**: `CardScript.isMinion == true`（只有 minion 卡能被消耗）
  - 需同时满足所属和类型限制
  - 符合条件的卡不足时，效果不发动
  - 多张符合条件时随机选择
- **消耗**: 符合条件的卡从牌组中移除并销毁

## Bury Cost 机制

Bury Cost 是一种特殊的预效果代价（pre-effect cost）：

- **配置位置**: `CardScript` 的 Bury Cost 相关字段
  - `buryCost`: 需要置底的己方卡数量
- **执行组件**: `BuryCostEffect` 挂载到 `CostNEffectContainer.preEffectEvent`
- **规则**:
  - 发动时从 `combinedDeckZone` 中寻找己方卡
  - 排除当前正在发动的卡
  - 己方卡不足时，效果不发动
  - 多张符合条件时随机选择
- **操作**: 符合条件的卡从牌组中移除并插入到底部（置底）

## 攻击动画系统

### 组件
| 组件 | 路径 | 说明 |
|------|------|------|
| `AttackAnimationManager` | `Assets/Scripts/Managers/AttackAnimationManager.cs` | 管理攻击动画队列 |

### 动画流程
1. **放大+旋转**: 卡片放大到 `attackScaleMultiplier` (默认1.4倍)，同时旋转使**顶部朝向目标**
2. **冲撞+缩小**: 向目标位置冲撞，冲到 `overshoot` 位置（冲过目标），同时缩小到原始大小的85%
3. **停顿**: 在 overshoot 位置停顿
4. **回弹**: 从 overshoot **回弹到目标位置**（就是你配置的 EnemyTargetPos / PlayerTargetPos），同时恢复大小和旋转
5. **伤害计算**: 执行伤害计算
6. **去底部**: 卡片通过 `CombatUXManager.MoveRevealedCardToBottom()` 去底部

### HPAlterEffect 动画触发
- **普通伤害**: 触发攻击动画，动画完成后执行伤害
- **Status Effect伤害**: 设置 `isStatusEffectDamage = true` 跳过动画
- **攻击目标判断**: 根据 `theirStatusRef` 与 `ownerPlayerStatusRef` 对比判断攻击敌人还是自己

### Unity Inspector 配置
在场景中的某个 GameObject 上挂载 `AttackAnimationManager`，配置：
- **Enemy Target Pos**: 敌人位置的 Transform
- **Player Target Pos**: 玩家位置的 Transform
- **Attack Scale Multiplier**: 1.3 (攻击前放大倍数)
- **Scale Up Duration**: 0.15 (放大持续时间)
- **Charge Duration**: 0.2 (冲撞持续时间)
- **Overshoot Distance**: 0.5 (冲过目标的距离)
- **Bounce Back Duration**: 0.1 (回弹持续时间)
- **Pause After Attack**: 0.05 (攻击后停顿时间)

## 卡片移动动画系统

### 概述
通用卡片移动动画系统，支持 Reveal、Stage、Bury、Delay、Start Card 等各种卡片操作的动画。

### 核心组件
| 组件 | 路径 | 说明 |
|------|------|------|
| `CombatUXManager` | `Assets/Scripts/UXPrototype/CombatUXManager.cs` | 管理所有卡片移动动画 |
| `CardMoveConfig` | 同上 | 卡片移动配置类 |
| `CardMoveType` | 同上 | 移动类型枚举 |

### 移动类型
| 类型 | 说明 |
|------|------|
| `ToTop` | 移动到牌组顶部（最后一张） |
| `ToBottom` | 移动到牌组底部（第一张） |
| `ToIndex` | 移动到指定索引位置 |
| `ToPosition` | 移动到指定世界坐标 |
| `ToGrave` | 移动到墓地（销毁位置） |

### 使用方法

#### 1. 通用移动方法
```csharp
// 使用配置对象
var config = new CardMoveConfig {
    moveType = CardMoveType.ToBottom,
    useArc = true,
    duration = 0.5f,
    onComplete = () => { /* 动画完成后执行 */ }
};
CombatUXManager.me.MoveCardWithAnimation(logicalCard, config);

// 使用便捷方法
CombatUXManager.me.MoveCardToBottom(logicalCard, onComplete: callback);
CombatUXManager.me.MoveCardToTop(logicalCard, onComplete: callback);
CombatUXManager.me.MoveCardToIndex(logicalCard, index: 5, onComplete: callback);
CombatUXManager.me.MoveCardToPosition(logicalCard, position, onComplete: callback);
CombatUXManager.me.MoveCardToGrave(logicalCard, onComplete: callback);
```

#### 2. 批量移动
```csharp
CombatUXManager.me.MoveCardsWithAnimation(cardList, config, onAllComplete: callback);
```

#### 3. Start Card 特殊处理
```csharp
// 播放退场动画，动画完成后执行 Shuffle
CombatUXManager.me.PlayStartCardExitAnimationWithCallback(startCard, () => {
    Shuffle();
    HandleNewRoundStart();
});
```

### 动画流程
1. **计算目标位置**: 根据 `CardMoveType` 计算最终位置
2. **弧形轨迹** (可选): 经过 `showPos` 中间点
3. **同步缩放**: 从当前大小缩放到目标大小
4. **完成回调**: 动画完成后执行回调，同步 `TargetPosition`

### 各操作使用方式

| 操作 | 使用方法 | 弧形轨迹 |
|------|---------|---------|
| Reveal → Bottom | `MoveRevealedCardToBottom(card, onComplete)` | ✅ |
| Start Card (销毁) | `PlayStartCardExitWithShuffleAnimation(card, others, callback)` | ❌ |
| Start Card (保留) | `PlayStartCardShuffleAnimation(card, allCards, callback)` | ✅ |
| Stage (置顶) | `MoveCardToTop(card, onComplete)` | ✅ |
| Bury (置底) | `MoveCardToBottom(card, onComplete)` | ✅ |
| Delay | `MoveCardToIndex(card, newIndex, useArc: false)` | ❌ |
| Bury Cost | `MoveCardToBottom(card, onComplete)` | ✅ |

### Start Card 与 Shuffle 并发动画

**旧问题**: Start Card 先移动到底部，然后瞬间跳走到随机位置，动画很怪

**新实现**: 
- Start Card 的动画和 Shuffle 动画同时进行
- 所有卡片（包括 Start Card）同时开始移动，同时到达新位置

**两种模式:**

1. **销毁模式** (`removeStartCardInsteadOfShuffle = true`):
   ```csharp
   // Start Card 退场去墓地，其他卡片 Shuffle
   PlayStartCardExitWithShuffleAnimation(startCard, otherCards, onComplete);
   ```

2. **保留模式** (`removeStartCardInsteadOfShuffle = false`):
   ```csharp
   // Start Card 直接移动到 Shuffle 后的随机位置
   PlayStartCardShuffleAnimation(startCard, allCards, onComplete);
   ```

**动画流程**:
1. 计算每张卡片在 Shuffle 后的目标位置
2. 所有卡片同时开始移动
3. Start Card 从 Reveal Zone 直接移动到目标位置（不经过底部）
4. 其他卡片从当前位置移动到 Shuffle 后的位置
5. 使用弧形轨迹（经过 `showPos`）增加视觉效果

## 注意事项

1. **缩进**: 使用 **Tab** 而非空格
2. **HPAlterEffect**: 自动加 `baseDmg.value`，传具体值时设 `baseDmg` 为 0
3. **cardTypeID**: 用于存档/统计（非实例 ID）
4. **防循环**: 同卡不放多个循环效果实例
5. **墓地机制**: 已移除（废弃）
6. **攻击动画**: 通过 `AttackAnimationManager` 队列播放，每个伤害效果独立播放一次

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
