---
name: unity-card-listener-check
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

Run the C# snippet below via Unity MCP `execute_code` (default `codedom` compiler). It scans all prefabs under `Assets/Prefabs/Cards/3.0 no cost (current)`, reads every `GameEventListener`, follows its `Response` to each `CostNEffectContainer`, and dumps the bound effect/cost methods to `docs/CardDesc_Response_Check.txt`.

```csharp
string root = "Assets/Prefabs/Cards/3.0 no cost (current)";
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
			System.Reflection.FieldInfo intArgField = args.GetType().GetField("m_IntArgument");
			if (intArgField != null) arg = (int)intArgField.GetValue(args);
		}
		string typeName = target != null ? target.GetType().FullName : "null";
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
		UnityEngine.Component cardComp = prefab.GetComponent(cardScriptType);
		if (cardComp != null && cardDescField != null)
		{
			object descObj = cardDescField.GetValue(cardComp);
			if (descObj != null) desc = (string)descObj;
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
		sb.Append("CARD|").Append(prefab.name).Append("|").Append(path).Append("|cardDesc=").Append(desc).Append("|bindings=").Append(bindingCount).Append(lb.ToString()).AppendLine();
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
