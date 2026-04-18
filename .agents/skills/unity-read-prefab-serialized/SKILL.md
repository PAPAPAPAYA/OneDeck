---
name: unity-read-prefab-serialized
description: Read Unity .prefab serialized component data via unity-MCP execute_code. Use when Kimi needs to inspect prefab configurations programmatically, including UnityEvent bindings, component field values, serialized references, and array/list contents. Covers CardScript, CostNEffectContainer, HPAlterEffect, and any MonoBehaviour fields exposed in the Inspector.
---

# Unity Prefab Serialized Data Inspector

Inspect Unity prefab component configurations through unity-MCP's `execute_code` tool. This avoids manual Editor clicking and enables batch/card-by-card automation.

## Constraints (execute_code + codedom)

The Unity-MCP `execute_code` uses the **codedom** compiler (C# 6) with strict rules:

| Rule | Fix |
|------|-----|
| No file-level `using` declarations | Use fully-qualified names (`UnityEngine.Debug.Log`) or wrap in a code block with `{ ... }` and place `using` inside (not recommended; fully-qualified is safer) |
| `return` must return a value on **all** paths | End every execution with `return 0;` (or any value convertible to `object`) |
| No `return;` (void return) | Always `return <value>;` |
| No `$""` string interpolation | Use `+` concatenation or `string.Format` |
| No `?.` null-conditional operator | Use explicit `!= null ? ... : ...` |

**Always** set `compiler: "codedom"` (or omit, as it is the default).

## Workflow

1. **Load prefab** with `UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path)`
2. **Get components** via `GetComponent<T>()` or `GetComponentsInChildren<T>()`
3. **Read fields** via direct property access for public fields, or via `UnityEditor.SerializedObject` for private/internal/UnityEvent data
4. **Log results** with `UnityEngine.Debug.Log(...)`
5. **Retrieve logs** with `read_console` tool

## Key APIs

### Direct field access (public fields)
```csharp
var hpAlter = child.GetComponent<HPAlterEffect>();
UnityEngine.Debug.Log("baseDmg=" + (hpAlter.baseDmg != null ? hpAlter.baseDmg.value.ToString() : "null"));
```

### SerializedObject access (private fields, UnityEvent, arrays)
```csharp
var so = new UnityEditor.SerializedObject(targetComponent);
var prop = so.FindProperty("fieldName");
UnityEngine.Debug.Log("value=" + prop.intValue);  // or .stringValue, .boolValue, .objectReferenceValue, etc.
```

### UnityEvent persistent calls
```csharp
var eventProp = so.FindProperty("effectEvent");
var calls = eventProp.FindPropertyRelative("m_PersistentCalls.m_Calls");
for (int i = 0; i < calls.arraySize; i++)
{
    var call = calls.GetArrayElementAtIndex(i);
    string targetType = call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue;
    string methodName = call.FindPropertyRelative("m_MethodName").stringValue;
}
```

## Tool Sequence

1. Call `execute_code` with the C# snippet
2. Call `read_console` (action=get, types=["log"]) to collect output
3. Parse output for assertions or documentation

## Variations

For ready-to-use templates covering:
- **Card prefab full inspection** (CardScript + all child CostNEffectContainers + HPAlterEffect bindings)
- **Reading array/list fields**
- **Reading ScriptableObject references**
- **Reading enum values**

See [references/csharp-templates.md](references/csharp-templates.md).
