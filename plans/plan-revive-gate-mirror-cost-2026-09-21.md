# Plan: RIFT_REVIVER 闸镜像 cost 检查(2026-09-21)

日期:2026-09-21
状态:**已拍板方向(方案 1A),待执行**。
上游:`plans/plan-revive-loop-mitigation-2026-09-19.md` §10(第二波加闸)的遗留事项 1。

## 1. 问题定义

RIFT_REVIVER 的容器结构:`checkCostEvent(CheckCost_HasOwnCardOfType)` → effectEvent `[ExileEffect(放逐1信徒), ReviveEffect(复活2现象, oncePerRound=2)]`。

闸在 `ReviveEffect` 内部(`ReviveChosenCards` 入口),而放逐是排在前面的独立 effectEvent。后果:**闸关闭后 RR 每次揭晓仍放逐一张信徒但不复活**——cost 支付与效果限次分离,卡面描述(「放逐:复活」因果)失实。

其余 5 张第二波卡(SPIRIT_CALLER / ELITE / MASS / DUO / RELIC_CURSE_REVIVAL)全是单效果或无伴随 cost,无此瑕疵。**本期只处理 RR。**

## 2. 机制基础(已实测)

- `CostNEffectContainer.checkCostEvent` 是 UnityEvent,**支持挂多条持久化检查**;任一检查失败则 `_costNotMetFlag > 0`,整个容器不发动(`CostNEffectContainer.cs:82-113`)。
- cost 失败走标准表现路径:摇卡动画 + `CostResultPresenter` 提示 —— 等于免费获得「本回合复活次数已用完」的玩家可见反馈。
- 现有 CheckCost 方法的文案风格:`_costNotMetFlag++; _costFailMessages.Add("// [卡名]…")`,中文。
- `OncePerRoundSpent()` 是只读判定(惰性戳,不消耗),可以安全暴露。

## 3. 改动清单

### 3.1 `Assets/Scripts/Effects/ReviveEffect.cs`(+约 3 行)

公开闸状态的只读访问器,放在 `OncePerRoundSpent()` 旁:

```csharp
/// <summary>
/// Public read for container-level cost checks (gate-mirror): true when this component
/// instance's per-round charges are spent. Read-only; never consumes a charge.
/// </summary>
public bool IsGateSpent()
{
	return OncePerRoundSpent();
}
```

### 3.2 `Assets/Scripts/Card/CostNEffectContainer.cs`(+约 14 行)

新增序列化引用 + 检查方法:

```csharp
[Header("Gate Mirror (4.0)")]
[Tooltip("Optional: cost fails while this ReviveEffect's per-round gate is spent, so paired cost-paying effects (e.g. exile) do not fire without their payoff.")]
public ReviveEffect gateSource;
```

```csharp
/// <summary>
/// Gate-mirror check (RIFT_REVIVER): while the referenced ReviveEffect's per-round gate
/// is spent, fail the cost so the whole container (exile + revive) stays inert.
/// </summary>
public void CheckCost_ReviveGateOpen()
{
	if (gateSource == null) return; // no mirror configured -> always open
	if (!gateSource.IsGateSpent()) return;
	_costNotMetFlag++;
	_costFailMessages.Add("// [" + _myCardScript.gameObject.name + "]本回合复活次数已用完\n");
}
```

### 3.3 `Assets/Prefabs/Cards/4.0/1_Uncommon/RIFT_REVIVER.prefab`

子物体 `exile 1 rift` 上:

1. 容器的 `gateSource` 序列化引用 → 同一子物体上的 ReviveEffect 组件 fileID;
2. `checkCostEvent.m_PersistentCalls` 增加第二条持久化调用 → 容器组件的 `CheckCost_ReviveGateOpen`。

用脚本落地(沿用 apply_revive_gate_prefabs*.py 的 fileID+GUID 双重定位 + 严格断言),新脚本 `tools/scripts/apply_rr_gate_mirror.py`。需要 Container 脚本 GUID(执行时从 `CostNEffectContainer.cs.meta` 取)。

### 3.4 cardDesc

不变。「放逐 <b>1</b> 友方信徒: 每回合两次,复活 <b>2</b> 友方现象」在 1A 之后字面诚实(闸闭=整容器不发动=不放逐)。Notion 无需同步。

## 4. 测试

1. `ReviveOncePerRoundGateTests` 加 1 例:构造 RR 形同构容器(cost = 有信徒 + 闸镜像;效果 = 放逐 + 复活,闸 2):
	- 闸开着:放逐与复活都发生;
	- 第三次(闸满):两者都不发生,信徒仍在卡组中。
2. `ReviveOncePerRoundPrefabTests` 加 1 例(RiftReviver_GateMirrorWired):RR 的 `checkCostEvent.GetPersistentEventCount() == 2`,第二条 `GetPersistentMethodName() == "CheckCost_ReviveGateOpen"`,且 `gateSource` 非空。

## 5. 副作用与边界

- 闸闭的 RR 每次揭晓走 cost-fail(摇卡+提示)。视为免费的「次数用完」UX,接受;若实战嫌吵,给 CostCheckResult 加 silent 标志另案。
- `IsGateSpent()` 不消耗电荷,不在新回合边界外产生状态写。

## 6. 回归

目标类(ReviveOncePerRoundGateTests / ReviveOncePerRoundPrefabTests)+ 全量 EditMode。14 个既知红灯(lethal 标本族,见 plan-infinity-specimen-synthetic-2026-09-21.md)不受本期影响。

## 7. 明确不做

- 不把其余 14 个闸位迁到容器层(1B 备选存档:未来「cost+效果」复合限次卡增多时再说);
- 不动 desc / Notion / 服务器 catalog;
- 不动 RR 以外的卡。

## 8. 执行顺序

1. 引擎两处(3.1 / 3.2)+ refresh 编译确认;
2. `apply_rr_gate_mirror.py` 落 prefab;
3. §4 两组测试;
4. 全量回归;
5. 执行记录回填本节下方。
