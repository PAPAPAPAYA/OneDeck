# BasedOnIntSO / IntSO 参数型 Effect 方法整理与优化计划

> 本文档汇总了项目中所有以 `_BasedOnIntSO` 命名，或直接接收 `IntSO` 参数的 effect 方法，供后续统一优化使用。
> 生成时间：2026-06-19（补充于同日）

---

## 1. 概述

项目中所有通过 `IntSO` 动态驱动 effect 数量的方法均已统一为字段型：

| 风格 | 说明 | 代表类 |
|------|------|--------|
| **字段型（owner/enemy）** | 类上声明 `ownerIntSO` / `enemyIntSO` 字段，方法内部根据卡牌阵营自动选择 | `HPAlterEffect`, `BuryEffect`, `StageEffect`, `StatusEffectGiverEffect`, `CurseEffect`, `ShieldAlterEffect`, `ExileEffect`, `CardManipulationEffect` |

注意：`CurseEffect` 同时存在 `EnhanceCurse_BasedOnIntSO()` 和 `EnhanceCurseWithCoefficient_BasedOnIntSO()` 两个字段型方法。

---

## 2. 已改造为字段型的方法

以下方法均不再接收 `IntSO` 参数，而是通过类上的 `ownerIntSO` / `enemyIntSO` 字段，根据卡牌阵营自动选择。

### 2.1 HPAlterEffect

文件：`Assets/Scripts/Effects/HPAlterEffect.cs`

| 方法 | 签名 | 实现摘要 | 使用卡片 |
|------|------|----------|----------|
| `DecreaseTheirHp_BasedOnIntSO` | `()` | 根据阵营选择 `ownerIntSO` 或 `enemyIntSO`，`extraDmg += intSO.value; DecreaseTheirHp(); extraDmg = 0;` | `ALL_FOR_ONE`（人人为我） |
| `DecreaseMyHp_BasedOnIntSO` | `()` | 根据阵营选择 `ownerIntSO` 或 `enemyIntSO`，伤害目标为己方 | 暂无 |
| `IncreaseTheirHp_BasedOnIntSO` | `()` | 根据阵营选择 `ownerIntSO` 或 `enemyIntSO`，调用 `IncreaseTheirHp(intSO.value);` | 暂无 |
| `IncreaseMyHp_BasedOnIntSO` | `()` | 根据阵营选择 `ownerIntSO` 或 `enemyIntSO`，调用 `IncreaseMyHp(intSO.value);` | 暂无 |
| `DecreaseTheirHpTimes_BasedOnIntSO` | `()` | 根据阵营选择 `ownerIntSO` 或 `enemyIntSO`，调用 `DecreaseTheirHpTimesX(intSO.value)` | 暂无 |

**注意**：
- `Decrease*` 系列会叠加 `baseDmg.value`（见类头警告），因此 `DecreaseTheirHp_BasedOnIntSO` 实际伤害 = `baseDmg + intSO.value`。
- `DecreaseTheirHpTimesIntSO` 的每次伤害也会叠加 `baseDmg + extraDmg`，容易与 `_BasedOnIntSO` 方法混淆。

---

### 2.2 CurseEffect

文件：`Assets/Scripts/Effects/CurseEffect.cs`

| 方法 | 签名 | 实现摘要 | 使用卡片 |
|------|------|----------|----------|
| `EnhanceCurseWithCoefficient_BasedOnIntSO` | `()` | 根据阵营选择 `ownerIntSO` 或 `enemyIntSO`，`EnhanceCurse(intSO.value / powerCoefficient);` | `DETERIORATION` |

---

### 2.3 ShieldAlterEffect

文件：`Assets/Scripts/Effects/ShieldAlterEffect.cs`

| 方法 | 签名 | 实现摘要 | 使用卡片 |
|------|------|----------|----------|
| `UpMyShield_BasedOnIntSO` | `()` | 根据阵营选择 `ownerIntSO` 或 `enemyIntSO`，调用 `UpMyShield(intSO.value);` | 暂无 |

---

### 2.4 ExileEffect

文件：`Assets/Scripts/Effects/ExileEffect.cs`

| 方法 | 签名 | 实现摘要 | 使用卡片 |
|------|------|----------|----------|
| `ExileMyCards_BasedOnIntSO` | `()` | 根据阵营选择 `ownerIntSO` 或 `enemyIntSO`，调用 `ExileMyCards(intSO.value);` | 暂无 |
| `ExileTheirCards_BasedOnIntSO` | `()` | 根据阵营选择 `ownerIntSO` 或 `enemyIntSO`，调用 `ExileTheirCards(intSO.value);` | 暂无 |

---

### 2.5 CardManipulationEffect

文件：`Assets/Scripts/Effects/CardManipulationEffect.cs`

| 方法 | 签名 | 实现摘要 | 使用卡片 |
|------|------|----------|----------|
| `DestroyTheirMinions_BasedOnIntSO` | `()` | 根据阵营选择 `ownerIntSO` 或 `enemyIntSO`，调用 `DestroyTheirMinions(intSO.value);` | 暂无 |
| `DestroyMyMinions_BasedOnIntSO` | `()` | 根据阵营选择 `ownerIntSO` 或 `enemyIntSO`，调用 `DestroyMyMinions(intSO.value);` | 暂无 |

---

## 3. 通过 ownerIntSO / enemyIntSO 字段的方法

这些方法的共同特征是：方法签名不带 `IntSO` 参数，而是读取类字段 `ownerIntSO` / `enemyIntSO`，并根据 `myCardScript.myStatusRef` 判断阵营后取值。

### 3.1 BuryEffect

文件：`Assets/Scripts/Effects/BuryEffect.cs`

| 方法 | 签名 | 实现摘要 | 使用卡片 |
|------|------|----------|----------|
| `BuryTheirCards_BasedOnIntSO` | `()` | 根据阵营选择 `ownerIntSO` 或 `enemyIntSO`，调用 `BuryTheirCards(intSO.value)` | `GRAVE_INVITATION`（冥界邀请） |
| `BuryMyCards_BasedOnIntSO` | `()` | 同上，目标为己方 | 暂无 |

---

### 3.2 StageEffect

文件：`Assets/Scripts/Effects/StageEffect.cs`

| 方法 | 签名 | 实现摘要 | 使用卡片 |
|------|------|----------|----------|
| `StageMyCards_BasedOnIntSO` | `()` | 根据阵营选择 `ownerIntSO` 或 `enemyIntSO`，调用 `StageMyCards(intSO.value)` | 暂无 |

---

### 3.3 StatusEffectGiverEffect

文件：`Assets/Scripts/Effects/StatusEffect/StatusEffectGiverEffect.cs`

| 方法 | 签名 | 实现摘要 | 使用卡片 |
|------|------|----------|----------|
| `GiveStatusEffectToXFriendly_BasedOnIntSO` | `()` | 根据阵营选择 `ownerIntSO` / `enemyIntSO`，临时把 `xFriendlyCount` 设为 `intSO.value`、`yFriendlyLayerCount` 设为 1，调用 `GiveStatusEffectToXFriendly()`，最后恢复字段 | `CURSE_THIRST_SHAMAN`（咒食的萨满） |

---

### 3.4 CurseEffect

文件：`Assets/Scripts/Effects/CurseEffect.cs`

| 方法 | 签名 | 实现摘要 | 使用卡片 |
|------|------|----------|----------|
| `EnhanceCurse_BasedOnIntSO` | `()` | 根据阵营选择 `ownerIntSO` / `enemyIntSO`，调用 `EnhanceCurse(intSO.value)` | `CURSED_SKELETON`（被诅咒的骷髅） |

---

## 4. 已知问题与优化点

### 4.1 阵营选择逻辑重复

以下方法都包含几乎一致的 `ownerIntSO / enemyIntSO` 选择代码：

```csharp
IntSO intSO = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef
    ? ownerIntSO
    : enemyIntSO;
```

已改为使用基类 `GetIntSOForOwner(ownerIntSO, enemyIntSO)` 的方法：
- `BuryEffect.BuryTheirCards_BasedOnIntSO()`
- `BuryEffect.BuryMyCards_BasedOnIntSO()`
- `StatusEffectGiverEffect.GiveStatusEffectToXFriendly_BasedOnIntSO()`
- `CurseEffect.EnhanceCurse_BasedOnIntSO()`

尚未使用基类方法、仍保留内联表达式的：
- `StageEffect.StageMyCards_BasedOnIntSO()`

### 4.2 `HPAlterEffect` 的 `extraDmg` 临时修改模式

```csharp
extraDmg += intSO.value;
DecreaseTheirHp();
extraDmg = 0;
```

风险：
- 如果 `DecreaseTheirHp()` 内部触发连锁效果并再次进入 `HPAlterEffect`，`extraDmg` 可能被覆盖。
- 与 `Increase*` 系列直接传参的风格不一致。

**优化建议**：将 `DecreaseTheirHp()` 重构为接受可选 `additionalDmg` 参数，避免修改共享字段。

### 4.3 `StatusEffectGiverEffect` 临时修改字段值

```csharp
int originalXFriendlyCount = xFriendlyCount;
int originalYFriendlyLayerCount = yFriendlyLayerCount;
xFriendlyCount = intSO.value;
yFriendlyLayerCount = 1;
GiveStatusEffectToXFriendly();
xFriendlyCount = originalXFriendlyCount;
yFriendlyLayerCount = originalYFriendlyLayerCount;
```

风险：
- 同样存在重入覆盖问题。
- 可读性差，难以一眼看出最终层数。

**优化建议**：将 `GiveStatusEffectToXFriendly()` 重构为接受 `count` 和 `layers` 参数，或新增一个内部重载。

### 4.4 命名不一致

- 大多数方法使用 `_BasedOnIntSO` 下划线前缀（如 `DecreaseTheirHp_BasedOnIntSO`）。
- `CurseEffect` 中 `EnhanceCurseWithCoefficient(IntSO)` 已改造为字段型 `EnhanceCurseWithCoefficient_BasedOnIntSO()`。
- `HPAlterEffect` 中 `DecreaseTheirHpTimesIntSO` 也未使用 `BasedOnIntSO` 命名。

**优化建议**：统一命名规范，例如：
- 字段型：`Xxx_BasedOnIntSO()`
- 带系数字段型：`Xxx_BasedOnIntSOWithCoefficient()` 或 `XxxWithCoefficient_BasedOnIntSO()`
- 多段伤害字段型：`DecreaseTheirHpTimes_BasedOnIntSO()`

### 4.5 已记录的 Bug

`Assets/DevLog.cs` 第 225 行已标记：

```csharp
//// curse thirst shaman: basedOnIntSO issue
//todo other value tracking and basedOnIntSO()
    // grave_invitation
    // body_canon
```

说明 `CURSE_THIRST_SHAMAN` 的 `GiveStatusEffectToXFriendly_BasedOnIntSO` 以及 `GRAVE_INVITATION` 的 `BuryTheirCards_BasedOnIntSO` 可能存在 value tracking 相关问题，需要重点验证。

> **验证状态（2026-06-19）**：已在 `Assets/Scripts/Editor/Tests/IntSOBasedEffectFactionTests.cs` 中补充对应 headless 测试：
> - `CURSE_THIRST_SHAMAN`：覆盖 `IntSO.value = 0`、`value > 可目标卡片数`、阵营反转，以及 `ValueTrackerManager.lastAppliedStatusEffectRef` / `lastAppliedStatusEffectAmountRef` 是否正确更新。
> - `GRAVE_INVITATION`：覆盖 `IntSO.value = 0`、`value > 可目标卡片数`、阵营反转，以及 `ValueTrackerManager.enemyCardsBuriedCountRef` / `ownerCardsBuriedCountRef` 是否正确更新。

### 4.6 空值检查

`intSO == null` 的空值检查已下沉到基类 `EffectScript.GetIntSOForOwner()`。各字段型方法中保留 `intSO.value <= 0` 的提前返回，用于避免无意义调用（如 0 次伤害、0 次放逐等）。

### 4.7 `DecreaseTheirHpTimesIntSO` 与 `_BasedOnIntSO` 的语义重叠

两者都接收 `IntSO`，但一个表示“次数”，一个表示“额外伤害”。命名上容易混淆，文档和 Inspector 中需要明确区分。

---

## 5. 推荐优化方向

1. **基类辅助**：在 `EffectScript` 中确认/增加 `GetIntSOForOwner(...)` 和 `GetIntSOValue(...)`，统一空值与阵营判断。
2. **方法重载**：让底层方法（如 `DecreaseTheirHp`、`GiveStatusEffectToXFriendly`）接受动态数量参数，避免临时修改字段。
3. **命名统一**：
   - 直接参数型统一为 `_BasedOnIntSO`。
   - 带系数或多段的，使用更明确的后缀（如 `_TimesIntSO`、`_WithCoefficient`）。
4. **~~补充测试~~**：针对 `CURSE_THIRST_SHAMAN` 和 `GRAVE_INVITATION` 增加基于 `IntSO` 的边界测试（`IntSO.value = 0`、`value > 可目标卡片数`、阵营反转等）。**已完成**，见 `Assets/Scripts/Editor/Tests/IntSOBasedEffectFactionTests.cs`。
5. **字段型 vs 参数型**：决定 `ownerIntSO/enemyIntSO` 字段型是否应统一改为参数型，或反之。参数型更直观、更容易在 UnityEvent 中复用；字段型可以减少每个卡牌的配置冗余。
6. **区分伤害次数与伤害数值**：`HPAlterEffect` 中同时存在字段型 `DecreaseTheirHpTimes_BasedOnIntSO`（次数）与 `DecreaseTheirHp_BasedOnIntSO`（额外伤害），命名已能区分；但参数型 `DecreaseTheirHpTimesIntSO` 仍缺少 `_BasedOnIntSO` 后缀，容易与字段型混用。建议：
   - 将参数型 `DecreaseTheirHpTimesIntSO` 重命名为 `DecreaseTheirHpTimes_IntSOParam` 或统一改造为字段型 `DecreaseTheirHpTimes_BasedOnIntSO`；
   - 在 Inspector/文档中明确标注 `_Times` 后缀表示“伤害次数”，无此后缀表示“伤害数值/额外伤害”，避免配置时混淆。

---

## 6. 附录：实际被卡片引用的方法

| 卡片 | Prefab 路径 | 绑定方法 | 方法风格 |
|------|-------------|----------|----------|
| `ALL_FOR_ONE`（人人为我） | `Assets/Prefabs/Cards/3.0 no cost (current)/General/1_Uncommon/ALL_FOR_ONE.prefab` | `HPAlterEffect.DecreaseTheirHp_BasedOnIntSO` | 字段型 |
| `BODY_CANON`（人体大炮） | `Assets/Prefabs/Cards/3.0 no cost (current)/Bury and buried/Bury/2_Rare/BODY_CANON.prefab` | `HPAlterEffect.DecreaseTheirHpTimes_BasedOnIntSO` | 字段型 |
| `CURSE_THIRST_SHAMAN`（咒食的萨满） | `Assets/Prefabs/Cards/3.0 no cost (current)/Curse/1_Uncommon/CURSE_THIRST_SHAMAN.prefab` | `StatusEffectGiverEffect.GiveStatusEffectToXFriendly_BasedOnIntSO` | 字段型 |
| `CURSED_SKELETON`（被诅咒的骷髅） | `Assets/Prefabs/Cards/3.0 no cost (current)/Curse/1_Uncommon/CURSED_SKELETON.prefab` | `CurseEffect.EnhanceCurse_BasedOnIntSO` | 字段型 |
| `DETERIORATION` | `Assets/Prefabs/Cards/3.0 no cost (current)/Curse/2_Rare/DETERIORATION.prefab` | `CurseEffect.EnhanceCurseWithCoefficient_BasedOnIntSO` | 字段型 |
| `GRAVE_INVITATION`（冥界邀请） | `Assets/Prefabs/Cards/3.0 no cost (current)/Bury and buried/Bury/1_Uncommon/GRAVE_INVITATION.prefab` | `BuryEffect.BuryTheirCards_BasedOnIntSO` | 字段型 |

---

*End of document*
