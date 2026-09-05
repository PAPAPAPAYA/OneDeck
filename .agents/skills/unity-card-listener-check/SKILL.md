---
name: unity-card-listener-check
last_reviewed: 2026-09-05
description: Verify that OneDeck card descriptions (cardDesc) match the actual GameEventListener -> CostNEffectContainer -> Effect method bindings. Use when asked to check card descriptions against listeners, validate card response mappings, audit GameEventListener configurations, or re-run the card-desc-vs-response check.
---

# OneDeck Card GameEventListener Configuration Checker

## Purpose

Compare each card's `cardDesc` text with the actual `GameEventListener` components on its prefab, including:

- Which `GameEvent` each listener subscribes to.
- Which `CostNEffectContainer` its `Response` invokes (one listener may call several containers).
- Which effect methods the container's `effectEvent` actually calls.

The check flags:

- Descriptions that imply a trigger event with no matching listener.
- Descriptions whose effect semantics do not match the bound effect methods.
- Listeners bound to events not mentioned in the description.

## Files

| File | Role |
|------|------|
| `docs/check_card_desc_vs_responses.py` | Python matcher / report generator |
| `docs/CardDesc_Response_Check.txt` | Raw extraction from Unity (input) |
| `docs/CardDesc_Response_Mismatch_Report.md` | Human-readable report (output) |

## Workflow

### Step 1: Extract Listener Bindings from Unity

Run the C# snippet below via Unity MCP `execute_code` (`compiler: "auto"`, the default, resolves to Roslyn / C# 12+). It scans all prefabs under `Assets/Prefabs/Cards/4.0` (the live shop pool; `FindAssets` also picks up `-1_Test/` — filter there if you need to exclude it), reads every `GameEventListener`, follows its `Response` to each `CostNEffectContainer`, and dumps the bound effect/cost methods to `docs/CardDesc_Response_Check.txt`.

```csharp
string root = "Assets/Prefabs/Cards/4.0";
string outputPath = "docs/CardDesc_Response_Check.txt";
string[] guids = UnityEditor.AssetDatabase.FindAssets("t:GameObject", new string[] { root });
System.Array.Sort(guids);
System.Text.StringBuilder sb = new System.Text.StringBuilder();
System.Type listenerType = System.Type.GetType("DefaultNamespace.GameEventListener, Assembly-CSharp");
System.Type containerType = System.Type.GetType("CostNEffectContainer, Assembly-CSharp");
System.Type cardScriptType = System.Type.GetType("CardScript, Assembly-CSharp");
if (listenerType == null) return "GameEventListener type not found";
if (containerType == null) return "CostNEffectContainer type not found";
if (cardScriptType == null) return "CardScript type not found";
System.Reflection.FieldInfo eventField = listenerType.GetField("event");
System.Reflection.FieldInfo responseField = listenerType.GetField("response");
System.Reflection.FieldInfo effectEventField = containerType.GetField("effectEvent");
System.Reflection.FieldInfo checkCostEventField = containerType.GetField("checkCostEvent");
System.Reflection.FieldInfo cardDescField = cardScriptType.GetField("cardDesc");
System.Reflection.FieldInfo isPassiveField = cardScriptType.GetField("isPassive");

// Mode-signature validation: a persistent call's mode must match the bound method's
// signature (Void=1, Object=2, Int=3, Float=4, String=5, Bool=6). A mismatch shows as
// <Missing ...> in the Inspector and silently no-ops at runtime.
// PersistentListenerMode: EventDefined=0, Void=1, Object=2, Int=3, Float=4, String=5, Bool=6.
int modeErrors = 0;
System.Func<UnityEngine.Object, string, int> resolveCallMode = (target, method) =>
{
	var mi = target.GetType().GetMethod(method, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, System.Type.EmptyTypes, null);
	if (mi != null)
	{
		var ps = mi.GetParameters();
		if (ps.Length == 0) return 1;
		return -1; // parameterized: resolve by first parameter below
	}
	var miAny = target.GetType().GetMethod(method, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
	if (miAny == null) return -2; // method gone entirely
	var p0 = miAny.GetParameters().Length > 0 ? miAny.GetParameters()[0].ParameterType : null;
	if (p0 == typeof(int)) return 3;
	if (p0 == typeof(float)) return 4;
	if (p0 == typeof(string)) return 5;
	if (p0 == typeof(bool)) return 6;
	if (typeof(UnityEngine.Object).IsAssignableFrom(p0)) return 2;
	if (p0 == null) return 1;
	return -1;
};

System.Action<UnityEngine.Events.UnityEvent, System.Collections.Generic.List<string>> extractCalls = delegate(UnityEngine.Events.UnityEvent evt, System.Collections.Generic.List<string> list)
{
	if (evt == null) return;
	System.Type t = typeof(UnityEngine.Events.UnityEventBase);
	System.Reflection.FieldInfo f = t.GetField("m_PersistentCalls", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
	if (f == null) return;
	object pc = f.GetValue(evt);
	if (pc == null) return;
	System.Type pcType = pc.GetType();
	System.Reflection.FieldInfo callsField = pcType.GetField("m_Calls", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
	if (callsField == null) return;
	System.Collections.IList calls = (System.Collections.IList)callsField.GetValue(pc);
	foreach (object call in calls)
	{
		System.Type callType = call.GetType();
		System.Reflection.PropertyInfo targetProp = callType.GetProperty("target");
		System.Reflection.PropertyInfo methodProp = callType.GetProperty("methodName");
		System.Reflection.PropertyInfo argsProp = callType.GetProperty("arguments");
		if (targetProp == null || methodProp == null) continue;
		UnityEngine.Object target = (UnityEngine.Object)targetProp.GetValue(call);
		string method = (string)methodProp.GetValue(call);
		object args = argsProp != null ? argsProp.GetValue(call) : null;
		int arg = 0;
		if (args != null)
		{
			// ArgumentCache fields are non-public: without NonPublic|Instance, GetField returns null and the arg silently prints as 0.
			System.Reflection.FieldInfo intArgField = args.GetType().GetField("m_IntArgument", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			if (intArgField != null) arg = (int)intArgField.GetValue(args);
		}
		string typeName = target != null ? target.GetType().FullName : "null";
		// mode-signature validation: stored mode must match the method's actual signature
		if (target != null)
		{
			int want = resolveCallMode(target, method);
			if (want == -2)
			{
				list.Add("MODE-ERROR->" + method + "(method missing)");
				modeErrors++;
			}
			else if (want >= 0)
			{
				var modeProp = callType.GetProperty("mode");
				int storedMode = modeProp != null ? (int)modeProp.GetValue(call) : want;
				if (storedMode != want)
				{
					list.Add("MODE-ERROR->" + method + "(storedMode=" + storedMode + ",expected=" + want + ")");
					modeErrors++;
				}
			}
		}
		list.Add(typeName + "->" + method + "(" + arg + ")");
	}
};

int processed = 0;
foreach (string guid in guids)
{
	string path = "";
	try
	{
		path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
		UnityEngine.GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(path);
		if (prefab == null) continue;
		string desc = "";
		modeErrors = 0; // per-card mode-signature error counter
		bool isCreature = false;
		bool isPassive = false;
		UnityEngine.Component cardComp = prefab.GetComponent(cardScriptType);
		if (cardComp != null && cardDescField != null)
		{
			object descObj = cardDescField.GetValue(cardComp);
			if (descObj != null) desc = (string)descObj;
			// The matcher's line regex hard-requires an isCreature= token, but CardScript.isCreature
			// was removed 2026-09-02 (cardType enum). Emit it from cardType == Creature(1).
			System.Reflection.FieldInfo cardTypeField = cardScriptType.GetField("cardType");
			if (cardTypeField != null)
			{
				object ct = cardTypeField.GetValue(cardComp);
				isCreature = ct != null && (int)ct == 1;
			}
			if (isPassiveField != null) isPassive = (bool)isPassiveField.GetValue(cardComp);
		}
		if (desc == null) desc = "";
		desc = desc.Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n").Replace("|", "\\|");
		UnityEngine.Component[] listeners = prefab.GetComponentsInChildren(listenerType, true);
		System.Text.StringBuilder lb = new System.Text.StringBuilder();
		int bindingCount = 0;
		foreach (UnityEngine.Component listener in listeners)
		{
			string eventName = "NONE";
			object evtObj = eventField.GetValue(listener);
			if (evtObj != null)
			{
				UnityEngine.ScriptableObject so = (UnityEngine.ScriptableObject)evtObj;
				eventName = so.name;
			}
			UnityEngine.Events.UnityEvent response = (UnityEngine.Events.UnityEvent)responseField.GetValue(listener);
			bool foundContainer = false;
			if (response != null)
			{
				int responseCount = response.GetPersistentEventCount();
				for (int i = 0; i < responseCount; i++)
				{
					UnityEngine.Object target = response.GetPersistentTarget(i);
					if (target != null && containerType.IsAssignableFrom(target.GetType()))
					{
						foundContainer = true;
						string containerName = target.name;
						System.Collections.Generic.List<string> effectCalls = new System.Collections.Generic.List<string>();
						System.Collections.Generic.List<string> costCalls = new System.Collections.Generic.List<string>();
						UnityEngine.Events.UnityEvent effectEvent = (UnityEngine.Events.UnityEvent)effectEventField.GetValue(target);
						UnityEngine.Events.UnityEvent checkCostEvent = (UnityEngine.Events.UnityEvent)checkCostEventField.GetValue(target);
						extractCalls(effectEvent, effectCalls);
						extractCalls(checkCostEvent, costCalls);
						lb.Append("|[LISTENER event=").Append(eventName)
						  .Append(" container=").Append(containerName)
						  .Append(" effects=").Append(string.Join(",", effectCalls.ToArray()))
						  .Append(" costs=").Append(string.Join(",", costCalls.ToArray()))
						  .Append("]");
						bindingCount++;
					}
				}
			}
			if (!foundContainer)
			{
				lb.Append("|[LISTENER event=").Append(eventName)
				  .Append(" container= effects= costs=]");
				bindingCount++;
			}
		}
		sb.Append("CARD|").Append(prefab.name).Append("|").Append(path).Append("|isCreature=").Append(isCreature ? "1" : "0").Append("|isPassive=").Append(isPassive ? "1" : "0").Append("|cardDesc=").Append(desc).Append("|bindings=").Append(bindingCount).Append("|modeErr=").Append(modeErrors).Append(lb.ToString()).AppendLine();
		processed++;
	}
	catch (System.Exception ex)
	{
		return "Error at " + path + ": " + ex.Message + "\n" + ex.StackTrace;
	}
}
System.IO.File.WriteAllText(outputPath, sb.ToString(), System.Text.Encoding.UTF8);
return "Extracted " + processed + " cards to " + outputPath;
```

### Step 2: Run the Matcher

```bash
python docs/check_card_desc_vs_responses.py
```

### Step 3: Read the Report

Open `docs/CardDesc_Response_Mismatch_Report.md`.

## Interpreting Results

| Problem | Meaning |
|---------|---------|
| **缺少对应触发事件的 Listener** | The description mentions a trigger (e.g. `被埋葬`) but no listener is bound to that event. |
| **效果类型不匹配** | A listener exists for the trigger, but the effect methods do not match the described effect semantics (e.g. description says `放逐友方` but the method buries enemies). |
| **未在描述中体现的 Listener** | A listener is bound to an event not covered by any description segment. |

## Known Edge Cases

- A single `GameEventListener` can invoke multiple `CostNEffectContainer`s; each binding is recorded separately.
- Multiple listeners can invoke the same container with different events (e.g. `OnMeRevealed` + `OnMeBuried`).
- Container names may contain `]` (e.g. `[Deathrattle]/[Linger]`); the extractor anchors on `costs=` to avoid truncation.
- Shop-only utility effects (`卡位增加`, `生命值上限增加`) default to `OnMeBought` when no trigger phrase is present.
- The matcher ignores pure tag segments such as `萦绕` because they carry no effect semantics.
- **Round-boundary flow events (GAME FLOW) accept the whole family** (`round_boundary_events`): 「回合开始」 → {`BeforeRoundFinished`, `OnRoundEnd`}; 「回合结束」 and 「洗牌后」 also accept `OnRoundEnd`. The asset names lie — `BeforeRoundFinished.asset` is wired to `GameEventStorage.beforeRoundStart` and fires at combat start (GatherDecks, BEFORE the opening shuffle) AND at every round boundary right after `HandleNewRoundStart`'s per-round resets; `OnRoundEnd` fires after every start-card shuffle animation settles, BEFORE the resets (round-end effects read the completed round; staged cards land on top and reveal first next round); `AfterShuffle` fires after the shuffle completes and the round's first card has reached the reveal zone. Position-sensitive effects (置顶/埋葬卡组顶) must NOT bind `beforeRoundStart`: its combat-start fire happens pre-shuffle and the staged position is lost — use `OnRoundEnd` (RELIC_WHITE_BANNER, FINAL_ESCORT).
- **Engine-side passive auras** (`RELIC_GRAVE_LORD`, `RELIC_GRAVE_CURSE`, `RELIC_RIFT_OVERRIDE`, `RELIC_BLOOD_PACT`): the prefab only re-arms a per-round override flag via `AfterShuffle -> ValueSetterEffect.SetIntSO` (reset each round in `HandleNewRoundStart`); the real behavior lives in `ValueTrackerManager`/`CombatManager`. The matcher skips these listeners as orphans, but the desc segments still cannot be auto-verified — expect a remaining 疑似 entry that needs human review.
- The matcher's line regex hard-requires the `isCreature=` token before `cardDesc=`; the extractor emits it from `cardType == Creature(1)` (the `isCreature` field was removed 2026-09-02).
