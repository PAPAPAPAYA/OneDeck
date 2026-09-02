# CURSE_SUMMONER(替人叫魂的喇叭)Prefab 配置方案

> 2026-09-02 落盘。执行环境:**新会话 + Unity MCP `execute_code`**(Unity Editor 需开启并连上 unity-mcp)。
> Notion 部分不依赖会话 MCP,走 `tools/outputs/notion_curse_summoner_finish.js`(OAuth 自包含)。

## 0. 已拍板决策

| 项 | 决策 |
|---|---|
| 中文名 | **替人叫魂的喇叭**(已于 2026-09-02 写入 Notion 并经 notion-fetch 验证) |
| 底板 | 克隆 `GRAVE_HEXER.prefab`(两个 listener 都是 OnMeRevealed,免改事件绑定) |
| GRAVE_HEXER 遗留 bug | 顺带修掉(Step 3) |
| desc 规范 | `复活1友方;复活1敌方诅咒`(半角分号、ASCII 数字,同已配置卡惯例) |
| 池子注册 | **不做**。ShopPoolRef/ShopTestPoolRef 均不含近期批量 4.0 卡(NECROMANCER 等属早期测试注册),统一留给第 5 步批量 |

## 1. 卡牌数据(Notion 4.0 card database,source of truth)

- Page id:`3cf827b8-c3c1-8060-a783-e1b0120b8eaf`,userDefined:ID 117,created 2026-09-02
- 非生物 / rarity=uncommon(prefab 序列化值 `1`) / tag `["诅咒","复活"]` / ATK 无
- card desc:`复活1友方；复活1敌方诅咒`(DB 全角分号 → prefab 用半角 `;`)
- 当前 `Unity 配置状态` = 可直接配置 → Step 5 完成后翻为 `已配置`

## 2. 调研结论(配置事实依据)

1. **复辟标准写法** = `ReviveEffect.ReviveTheirCards(1)` + `typeIDFilter: JU_ON`。双证:CURSE_REVIVER(洗不掉的印子)、CURSE_GARDENER(疯长的绿萝),均为已配置卡。
2. **为什么不能用 tag 过滤**:「敌方诅咒」实体是 JU_ON token(`Assets/Prefabs/3.0 no cost (current)/_DONT INCLUDE/Token/JU_ON.prefab`,displayName=诅咒,isCreature=1),其 `myTags` 为空,`ReviveTheirCardsWithTag` 匹配不到。
3. **GRAVE_HEXER 异常**:其「复活1友方」容器 `ReviveMyCards(1)` 带 `typeIDFilter: JU_ON`,与 desc 不符(全池 23 张复活卡中唯一),系克隆自 CURSE_REVIVER 的残留 → 本方案顺带清空。
4. **Tag 枚举**(EnumStorage.cs,追加式):8=Curse,11=Revive → `myTags` 序列化 `080000000b000000` = [8,11],GRAVE_HEXER/CURSE_REVIVER 同值,克隆即得,无需改。
5. 事件资产:`OnMeRevealed.asset` guid `b9291c6dab76d934a8dfea097c0df6b4`(模板两个 listener 已挂,不动)。容器的 `cursedCardTypeID` → `Assets/SORefs/CombatRefs/CurseCardTypeID.asset`(guid `07a2aa...`),克隆自动保留。
6. 复活引擎语义(ReviveEffect.cs):复活=墓地(Start Card 之前)→牌库顶,空墓地 fizzle;`onMeRevived` 苏醒事件族仅由 ReviveChosenCards 触发;REVIVE 自身排除(reveal 区卡天然不可选中)。

## 3. Step 1 — execute_code 建卡(整段一次执行)

```csharp
var srcPath = "Assets/Prefabs/Cards/4.0/1_Uncommon/GRAVE_HEXER.prefab";
var dstPath = "Assets/Prefabs/Cards/4.0/1_Uncommon/CURSE_SUMMONER.prefab";
if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(dstPath) != null)
	throw new System.InvalidOperationException("target prefab already exists: " + dstPath);

var go = UnityEditor.PrefabUtility.LoadPrefabContents(srcPath);
try {
	// --- root CardScript identity (rarity=1 Uncommon, myTags=[8,11]=[Curse,Revive] stay as cloned) ---
	var card = go.GetComponent<CardScript>();
	card.cardTypeID = "CURSE_SUMMONER";
	card.displayName = "替人叫魂的喇叭";
	card.cardDesc = "复活1友方;复活1敌方诅咒";

	// --- identify the two effect children (both listeners on root already -> OnMeRevealed) ---
	UnityEngine.Transform reviveChild = null, curseChild = null;
	foreach (UnityEngine.Transform t in go.transform) {
		if (t.GetComponent<ReviveEffect>() != null) reviveChild = t; else curseChild = t;
	}
	if (reviveChild == null || curseChild == null) throw new System.InvalidOperationException("template children not found");

	// --- child1: 复活1友方 = ReviveMyCards(1) with EMPTY typeIDFilter (do not inherit the HEXER residue) ---
	reviveChild.GetComponent<ReviveEffect>().typeIDFilter = "";
	reviveChild.name = "revive 1 friend";

	// --- child2: 复活1敌方诅咒 = swap CurseEffect -> ReviveEffect(typeIDFilter=JU_ON), rebind to ReviveTheirCards(1) ---
	curseChild.name = "revive enemy curse";
	var curseType = System.Type.GetType("DefaultNamespace.Effects.CurseEffect, Assembly-CSharp");
	var oldCurse = curseChild.GetComponent(curseType);
	if (oldCurse != null) UnityEngine.Object.DestroyImmediate(oldCurse);
	var rev2 = curseChild.AddComponent<ReviveEffect>();
	rev2.typeIDFilter = "JU_ON"; // all other fields keep class defaults = sibling parity

	var cont2 = curseChild.GetComponent<CostNEffectContainer>();
	var so = new UnityEditor.SerializedObject(cont2);
	var calls = so.FindProperty("effectEvent.m_PersistentCalls.m_Calls");
	calls.arraySize = 1;
	var c0 = calls.GetArrayElementAtIndex(0);
	c0.FindPropertyRelative("m_Target").objectReferenceValue = rev2;
	c0.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = "ReviveEffect, Assembly-CSharp";
	c0.FindPropertyRelative("m_MethodName").stringValue = "ReviveTheirCards";
	c0.FindPropertyRelative("m_Mode").intValue = 3; // int argument
	c0.FindPropertyRelative("m_IntArgument").intValue = 1;
	c0.FindPropertyRelative("m_CallState").intValue = 2;
	so.ApplyModifiedProperties();

	var saved = UnityEditor.PrefabUtility.SaveAsPrefabAsset(go, dstPath);
	if (saved == null) throw new System.InvalidOperationException("SaveAsPrefabAsset failed");
} finally {
	UnityEngine.Object.DestroyImmediate(go);
}
UnityEditor.AssetDatabase.SaveAssets();
UnityEngine.Debug.Log("[CURSE_SUMMONER] prefab created at " + dstPath);
```

## 4. Step 2 — execute_code 读回自检(整段一次执行)

```csharp
var p = "Assets/Prefabs/Cards/4.0/1_Uncommon/CURSE_SUMMONER.prefab";
var go = UnityEditor.PrefabUtility.LoadPrefabContents(p);
try {
	var card = go.GetComponent<CardScript>();
	UnityEngine.Debug.Log("[check] id=" + card.cardTypeID + " | name=" + card.displayName
		+ " | desc=" + card.cardDesc + " | rarity=" + (int)card.rarity
		+ " | tags=[" + string.Join(",", card.myTags) + "]"
		+ " | creature=" + card.isCreature + " | printedAtk=" + card.printedAttack);
	foreach (var l in go.GetComponents<DefaultNamespace.GameEventListener>())
		UnityEngine.Debug.Log("[check] listener -> " + (l.event != null ? l.event.name : "NULL"));
	foreach (var cont in go.GetComponentsInChildren<CostNEffectContainer>(true)) {
		var so = new UnityEditor.SerializedObject(cont);
		var calls = so.FindProperty("effectEvent.m_PersistentCalls.m_Calls");
		for (int i = 0; i < calls.arraySize; i++) {
			var c = calls.GetArrayElementAtIndex(i);
			var target = c.FindPropertyRelative("m_Target").objectReferenceValue as MonoBehaviour;
			UnityEngine.Debug.Log("[check] " + cont.gameObject.name + " -> "
				+ c.FindPropertyRelative("m_MethodName").stringValue
				+ "(" + c.FindPropertyRelative("m_IntArgument").intValue + ") on "
				+ (target != null ? target.GetType().Name : "NULL"));
		}
	}
	foreach (var re in go.GetComponentsInChildren<ReviveEffect>(true))
		UnityEngine.Debug.Log("[check] " + re.gameObject.name + " ReviveEffect typeIDFilter='" + re.typeIDFilter + "'");
} finally {
	UnityEngine.Object.DestroyImmediate(go);
}
```

**期望输出**:id=CURSE_SUMMONER / name=替人叫魂的喇叭 / desc=复活1友方;复活1敌方诅咒 / rarity=1 / tags=[8,11] / creature=False / printedAtk=0;两条 listener → OnMeRevealed;`revive 1 friend -> ReviveMyCards(1)`;`revive enemy curse -> ReviveTheirCards(1)`;前者 typeIDFilter=''、后者 'JU_ON'。

## 5. Step 3 — 顺带修 GRAVE_HEXER(用户已拍板)

```csharp
var p = "Assets/Prefabs/Cards/4.0/1_Uncommon/GRAVE_HEXER.prefab";
var go = UnityEditor.PrefabUtility.LoadPrefabContents(p);
try {
	var re = go.GetComponentInChildren<ReviveEffect>(true); // the only ReviveEffect = "revive 1 friend"
	if (re == null) throw new System.InvalidOperationException("ReviveEffect not found");
	UnityEngine.Debug.Log("[fix] before: typeIDFilter='" + re.typeIDFilter + "'");
	re.typeIDFilter = ""; // desc = 复活1友方 (generic friendly revive), JU_ON residue removed
	UnityEditor.PrefabUtility.SaveAsPrefabAsset(go, p);
} finally {
	UnityEngine.Object.DestroyImmediate(go);
}
UnityEditor.AssetDatabase.SaveAssets();
```

修完后该卡「复活1友方」恢复为任意友方(与 NECROMANCER 等同构);「强化2敌方诅咒」容器不受影响。

## 6. Step 4 — 可选验证

1. **listener 校验**:跑 `unity-card-listener-check` skill,确认 desc「复活1友方;复活1敌方诅咒」↔ 双容器绑定一致。
2. **EditMode 冒烟**(可选):HeadlessCombatTestFixture 体系可加 resurrect+复辟用例;若用 runner 跑测试,先 SaveScene 清 dirty(否则保存框阻塞 → init 假失败)。
3. **不跑 Play Mode**(AGENTS.md:仅在用户明确要求时)。

## 7. Step 5 — Notion 收尾(prefab 验证通过后)

```powershell
cd "D:\Unity Projects\OneDeck\tools\outputs"; node notion_curse_summoner_finish.js
```

- 默认模式:补写 中文名(幂等)+ `Unity 配置状态 → 已配置`
- 回滚:`node notion_curse_summoner_finish.js revert`(状态退回 可直接配置)
- 验证:用 notion-fetch 抓该页(**不要信 SQL 查询——视图对新写列有陈旧缓存**),应见 `中文名=替人叫魂的喇叭`、`Unity 配置状态=已配置`

## 8. 回滚

| 对象 | 操作 |
|---|---|
| CURSE_SUMMONER.prefab | `UnityEditor.AssetDatabase.DeleteAsset("Assets/Prefabs/Cards/4.0/1_Uncommon/CURSE_SUMMONER.prefab")` |
| GRAVE_HEXER.prefab | Step 3 代码里把 `re.typeIDFilter = ""` 换回 `"JU_ON"` 重跑(仅当需要还原 bug 现状) |
| Notion | `node notion_curse_summoner_finish.js revert`(中文名保留,无害) |

## 9. 坑位提醒(新会话执行时)

- execute_code:Roslyn(C# 12)可用;**dynamic 不可用**;类型解析失败用 `System.Type.GetType("xxx, Assembly-CSharp")`;编译失败会保持旧域,先看 Console。
- SerializedObject 直改 UnityEvent 是 A 组批量验证过的路径(UnityEventTools 反射绑法易踩 NonPublic 坑,本方案不用)。
- `SaveAsPrefabAsset` 后无需手动刷新(资产已注册),`SaveAssets()` 落盘即可;若 Console 无新 prefab,执行一次 `AssetDatabase.Refresh()`。
- prefab 结构纪律:根 = CardScript + 2×GameEventListener;效果容器与 Effect 在**子物体**上;`_myCardScript` 不序列化(运行期注入),不要手动建字段。
- 疲劳/测试相关:EditMode 测试里疲劳阈值设 999 等惯例见 `plans/plan-4.0-revive-awaken-2026-08-29.md`。
