# Bury Cost 测试指南

## 测试卡牌配置步骤

### 1. 创建测试卡牌 Prefab

1. 在 Unity Editor 中，复制 `Assets/Prefabs/Cards/1.0/TestCards/Test_Deal1DmgHeal1.prefab`
2. 重命名为 `Test_BuryCost1.prefab`
3. 放置在 `Assets/Prefabs/Cards/1.0/TestCards/` 目录下

### 2. 配置 CardScript 组件

在卡牌的 `CardScript` 组件中设置：

```
cardTypeID: TEST_BURY_COST_1
cardDesc: bury cost <color=yellow>1</color>; deal <color=red>1</color> dmg
buryCost: 1
```

### 3. 添加 BuryCostEffect 组件

1. 在卡牌 Prefab 上添加 `BuryCostEffect` 脚本组件
2. 确保 `effectResultString` 引用正确的 StringSO（与其他效果组件一致）

### 4. 配置 CostNEffectContainer 组件

在卡牌的 `CostNEffectContainer` 组件中：

#### preEffectEvent 配置
1. 点击 `preEffectEvent` 的 `+` 按钮添加事件
2. 拖拽 `BuryCostEffect` 组件到目标对象槽
3. 选择 `BuryCostEffect.ExecuteBuryCost` 方法

#### effectEvent 配置
1. 保留现有的效果（如 `HPAlterEffect.DecreaseTheirHp`）
2. 或添加其他效果

### 5. 完整配置示例

```
CardScript:
  - cardTypeID: TEST_BURY_COST_1
  - cardDesc: bury cost <color=yellow>1</color>; deal <color=red>1</color> dmg
  - buryCost: 1
  - takeUpSpace: true

BuryCostEffect:
  - effectResultString: [引用 StringSO]

CostNEffectContainer:
  - effectResultString: [引用 StringSO]
  - preEffectEvent:
    - BuryCostEffect.ExecuteBuryCost
  - effectEvent:
    - HPAlterEffect.DecreaseTheirHp (参数: 1)
```

## 测试场景

### 场景 1：正常发动（己方卡足够）

**前提条件**：
- 牌组中有至少 1 张己方卡（不包括当前卡）
- 当前卡的 `buryCost = 1`

**预期结果**：
1. 点击第 1 次：揭晓卡牌
2. 点击第 2 次：
   - 随机选择 1 张己方卡置底
   - 显示置底信息
   - 执行伤害效果

**验证点**：
- 被置底的卡移动到牌组底部
- 动画播放正常
- 效果日志显示正确

### 场景 2：己方卡不足（发动失败）

**前提条件**：
- 牌组中没有己方卡（或只有当前卡）
- 当前卡的 `buryCost = 1`

**预期结果**：
1. 点击第 1 次：揭晓卡牌
2. 点击第 2 次：
   - 显示失败信息：`// [卡牌名] bury cost failed: need 1 ally card(s), found 0`
   - 伤害效果不执行

**验证点**：
- 失败信息正确显示
- 没有卡牌被置底
- 伤害效果未触发

### 场景 3：buryCost = 0

**前提条件**：
- 当前卡的 `buryCost = 0`

**预期结果**：
1. 点击第 1 次：揭晓卡牌
2. 点击第 2 次：
   - 不执行置底操作
   - 直接执行伤害效果

**验证点**：
- 没有卡牌被移动
- 伤害效果正常执行

### 场景 4：多张卡置底

**前提条件**：
- 牌组中有至少 3 张己方卡
- 当前卡的 `buryCost = 2`

**预期结果**：
1. 点击第 1 次：揭晓卡牌
2. 点击第 2 次：
   - 随机选择 2 张己方卡置底
   - 显示 2 条置底信息
   - 执行伤害效果

**验证点**：
- 2 张卡被置底
- 动画播放正常
- 效果日志显示 2 条记录

### 场景 5：当前卡被排除

**前提条件**：
- 牌组中只有当前卡是己方卡
- 当前卡的 `buryCost = 1`

**预期结果**：
1. 点击第 1 次：揭晓卡牌
2. 点击第 2 次：
   - 显示失败信息（因为当前卡被排除）
   - 伤害效果不执行

**验证点**：
- 当前卡没有被置底
- 失败信息正确显示

## 调试技巧

### 1. 查看效果日志

在 `CombatInfoDisplayer` 中查看效果日志，确认：
- 置底信息是否正确显示
- 失败信息是否正确显示

### 2. 检查牌组状态

在 `CombatManager` 的 Inspector 中查看 `combinedDeckZone` 列表，确认：
- 卡牌顺序是否正确
- 被置底的卡是否在底部

### 3. 断点调试

在 `BuryCostEffect.ExecuteBuryCost()` 方法中设置断点，检查：
- `costCount` 值是否正确
- `eligibleCards` 列表是否包含正确的卡
- `cardsToBury` 列表是否正确选择

## 常见问题

### Q1: 置底动画没有播放？

**A**: 确保 `CombatUXManager.me.PlayStageBuryAnimation` 被正确调用。检查 `buriedCards` 列表是否为空。

### Q2: 效果仍然发动了，即使己方卡不足？

**A**: 确保 `BuryCostEffect` 组件正确挂载，并且 `preEffectEvent` 中配置了 `ExecuteBuryCost` 方法。

### Q3: 当前卡被置底了？

**A**: 检查 `BuryCostEffect` 中是否正确排除了 `myCard`。代码中应该有 `if (card == myCard) continue;`。

### Q4: 置底的卡不是随机选择的？

**A**: 确保调用了 `UtilityFuncManagerScript.ShuffleList(eligibleCards)` 进行随机打乱。

## 扩展测试

### 测试与其他 Cost 的组合

1. **Bury Cost + Mana Cost**
   - 配置 `buryCost = 1` 和 `CheckCost_Mana(1)`
   - 测试法力不足时是否阻止发动

2. **Bury Cost + Token Cost**
   - 配置 `buryCost = 1` 和 `tokenCostCount = 1`
   - 测试两种 cost 的执行顺序

3. **Bury Cost + Delay Cost**
   - 配置 `buryCost = 1` 和 `delayCost = 1`
   - 测试两种 cost 的执行顺序

## 测试检查清单

- [ ] 正常发动（己方卡足够）
- [ ] 发动失败（己方卡不足）
- [ ] buryCost = 0 时的行为
- [ ] 多张卡置底
- [ ] 当前卡被正确排除
- [ ] 置底动画播放正常
- [ ] 效果日志显示正确
- [ ] 与其他 Cost 组合正常
