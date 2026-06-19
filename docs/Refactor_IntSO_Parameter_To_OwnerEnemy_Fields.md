# IntSO 参数型 Effect 方法 → ownerIntSO/enemyIntSO 字段型 改造指南

> 本文档以 `HPAlterEffect.DecreaseTheirHpTimesIntSO(IntSO)` → `DecreaseTheirHpTimes_BasedOnIntSO()` 的改造为范例，供后续将其他“直接接收 IntSO 参数”的 effect 方法改造为“按阵营自动选择 ownerIntSO/enemyIntSO 字段”的字段型方法时参考。

---

## 1. 改造目标

将类似下面的**参数型**方法：

```csharp
public void SomeEffect_BasedOnIntSO(IntSO intSO)
{
    if (intSO == null) return;
    SomeEffect(intSO.value);
}
```

改造为**字段型**方法：

```csharp
public virtual void SomeEffect_BasedOnIntSO()
{
    IntSO intSO = GetIntSOForOwner(ownerIntSO, enemyIntSO);
    if (intSO == null) return;
    if (intSO.value <= 0) return;

    SomeEffect(intSO.value);
}
```

字段型方法的特点：
- 方法签名**不带 `IntSO` 参数**。
- 类上声明 `ownerIntSO` / `enemyIntSO` 字段。
- 方法内部根据卡牌阵营自动选择 IntSO：己方卡用 `ownerIntSO`，敌方卡用 `enemyIntSO`。
- 调用方可直接通过 `CostNEffectContainer` 的 UnityEvent 无参绑定。

---

## 2. 代码改造步骤

### 2.1 在 Effect 类中新增字段

在目标 effect 类（如 `HPAlterEffect`、`CurseEffect`、`ShieldAlterEffect` 等）的字段区域新增：

```csharp
[Header("Based on IntSO")]
[Tooltip("IntSO used when this card belongs to the owner/player")]
public IntSO ownerIntSO;
[Tooltip("IntSO used when this card belongs to the enemy")]
public IntSO enemyIntSO;
```

字段顺序建议放在该类的配置字段之后、其他不相关 `Header` 之前，便于 Inspector 阅读。

### 2.2 替换/新增方法

找到原来的参数型方法，按以下模板替换：

```csharp
/// <summary>
/// Based on ownerIntSO/enemyIntSO, [功能简述].
/// Uses ownerIntSO when this card belongs to the owner, otherwise enemyIntSO.
/// </summary>
public virtual void [原方法名]_BasedOnIntSO()
{
    IntSO intSO = GetIntSOForOwner(ownerIntSO, enemyIntSO);
    if (intSO == null) return;
    if (intSO.value <= 0) return;

    [底层方法名](intSO.value);
}
```

命名建议：
- 如果原方法名已经以 `_BasedOnIntSO` 结尾（如 `DecreaseTheirHp_BasedOnIntSO(IntSO)`），改造后仍保持同名但去掉参数。
- 如果原方法名不含 `_BasedOnIntSO`（如 `DecreaseTheirHpTimesIntSO`），建议改名为 `DecreaseTheirHpTimes_BasedOnIntSO`，与项目风格统一。

### 2.3 使用 `GetIntSOForOwner`

`GetIntSOForOwner(ownerIntSO, enemyIntSO)` 已在 `EffectScript` 基类中实现，可直接调用：

```csharp
protected IntSO GetIntSOForOwner(IntSO ownerIntSO, IntSO enemyIntSO)
{
    if (myCardScript == null || combatManager == null) return null;
    return myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef
        ? ownerIntSO
        : enemyIntSO;
}
```

### 2.4 删除旧方法

要求不保留旧方法，直接删除原参数型方法。

---

## 3. Prefab / UnityEvent 改造步骤

### 3.1 确定原 IntSO 引用的含义

打开使用旧方法的 prefab，找到 UnityEvent 中传入的 `IntSO` 参数，记录其 GUID。判断该 IntSO 在旧逻辑中是：
- 仅用于己方（如 `OwnerInGraveAmountRef`）
- 仅用于敌方（如 `EnemyInGraveAmountRef`）
- 双方共用同一个 IntSO

### 3.2 给 effect 组件字段赋值

在 prefab 的 effect 组件 YAML 中，新增：

```yaml
ownerIntSO: {fileID: 11400000, guid: <owner_guid>, type: 2}
enemyIntSO: {fileID: 11400000, guid: <enemy_guid>, type: 2}
```

如果旧逻辑双方共用同一个 IntSO，仍需根据阵营语义分别指向对应的 IntSO 资产（例如 `OwnerInGraveAmountRef` / `EnemyInGraveAmountRef`），而不是简单复制同一个 GUID。

### 3.3 修改 UnityEvent 绑定

找到 `CostNEffectContainer` 中绑定旧方法的 PersistentCall，做以下修改：

| 字段 | 旧值 | 新值 |
|------|------|------|
| `m_MethodName` | `SomeEffect_BasedOnIntSO` 或 `SomeEffectIntSO` | `SomeEffect_BasedOnIntSO` |
| `m_Mode` | `2`（Object 参数） | `1`（Void 无参） |
| `m_Arguments.m_ObjectArgument` | `{fileID: ...}` | `{fileID: 0}` |
| `m_Arguments.m_ObjectArgumentAssemblyTypeName` | `IntSO, Assembly-CSharp` | `UnityEngine.Object, UnityEngine` |

修改后示例：

```yaml
- m_Target: {fileID: <effect_component_fileid>}
  m_TargetAssemblyTypeName: HPAlterEffect, Assembly-CSharp
  m_MethodName: DecreaseTheirHpTimes_BasedOnIntSO
  m_Mode: 1
  m_Arguments:
    m_ObjectArgument: {fileID: 0}
    m_ObjectArgumentAssemblyTypeName: UnityEngine.Object, UnityEngine
    m_IntArgument: 0
    m_FloatArgument: 0
    m_StringArgument: 
    m_BoolArgument: 0
  m_CallState: 2
```

---

## 4. 完整范例：HPAlterEffect

### 改造前

```csharp
// HPAlterEffect.cs
public void DecreaseTheirHpTimesIntSO(IntSO timesIntSO)
{
    if (timesIntSO == null) return;

    int times = timesIntSO.value;
    for (int i = 0; i < times; i++)
    {
        DecreaseTheirHp();
    }
}
```

### 改造后

```csharp
// HPAlterEffect.cs
[Header("Based on IntSO")]
[Tooltip("IntSO used when this card belongs to the owner/player")]
public IntSO ownerIntSO;
[Tooltip("IntSO used when this card belongs to the enemy")]
public IntSO enemyIntSO;

public virtual void DecreaseTheirHpTimes_BasedOnIntSO()
{
    IntSO intSO = GetIntSOForOwner(ownerIntSO, enemyIntSO);
    if (intSO == null) return;
    if (intSO.value <= 0) return;

    DecreaseTheirHpTimesX(intSO.value);
}
```

### BODY_CANON.prefab 改造后

```yaml
// HPAlterEffect 组件字段
ownerIntSO: {fileID: 11400000, guid: 809d1d05286f984409aaabe7bfb9fc17, type: 2}
enemyIntSO: {fileID: 11400000, guid: 6a71e8c2b741fd84abafa02a92d21951, type: 2}

// CostNEffectContainer effectEvent
- m_Target: {fileID: 1359544507321340075}
  m_TargetAssemblyTypeName: HPAlterEffect, Assembly-CSharp
  m_MethodName: DecreaseTheirHpTimes_BasedOnIntSO
  m_Mode: 1
  m_Arguments:
    m_ObjectArgument: {fileID: 0}
    m_ObjectArgumentAssemblyTypeName: UnityEngine.Object, UnityEngine
    ...
```

---

## 5. 批量改造检查清单

对每个待改造的方法，按以下清单执行：

- [ ] 在 effect 类中新增 `ownerIntSO` / `enemyIntSO` 字段。
- [ ] 新增/替换为无参的 `_BasedOnIntSO()` 方法，使用 `GetIntSOForOwner`。
- [ ] 删除旧的参数型方法（或标记 `[Obsolete]` 保留转发）。
- [ ] 查找所有调用旧方法的 prefab / scene。
- [ ] 为每个 prefab 的 effect 组件正确赋值 `ownerIntSO` / `enemyIntSO`（注意阵营语义，不要简单复制同一个 GUID）。
- [ ] 修改 `CostNEffectContainer` 的 UnityEvent 绑定：`m_Mode` 改为 `1`，清空 `m_ObjectArgument`。
- [ ] 编译验证。
- [ ] 在相关测试卡上验证：己方使用时读取 `ownerIntSO`，敌方使用时读取 `enemyIntSO`。
- [ ] 更新 `docs/BasedOnIntSO_EffectMethods_RefactorPlan.md` 中的方法签名与附录表。

---

## 6. 常见注意事项

1. **阵营判断依据**：`myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef`。不要误用 `theirStatusRef` 或其他引用。
2. **`intSO.value <= 0` 提前返回**：避免产生 0 次调用、空日志或无意义事件。
3. **字段型与参数型不要混用**：同一个方法不要同时提供带参和无参版本，否则 Inspector 中容易出现误绑定。
4. **prefab 中 IntSO 字段顺序**：Unity 序列化顺序与脚本中字段声明顺序一致，新增字段应插在合适位置，避免破坏现有 YAML 结构。
5. **双方共用同一个 IntSO 的情况**：改造后仍需拆成 `ownerIntSO` / `enemyIntSO` 两个字段，分别指向对应阵营的 IntSO 资产（如 `OwnerInGraveAmountRef` / `EnemyInGraveAmountRef`）。

---

*基于 2026-06-19 `HPAlterEffect.DecreaseTheirHpTimes_BasedOnIntSO` 改造实践整理。*
