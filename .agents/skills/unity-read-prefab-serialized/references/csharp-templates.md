# C# Code Templates for Prefab Inspection

All snippets assume `compiler: "codedom"`. Wrap code in `{ ... return 0; }` to satisfy the return-value requirement.

## Template 1: Full Card Prefab Inspection

Reads `CardScript` on root + every `CostNEffectContainer` child, its `HPAlterEffect` fields, and `effectEvent` UnityEvent bindings.

```csharp
{
    var prefabPath = "Assets/Prefabs/Cards/3.0 no cost (current)/General/血肉聚集体.prefab";
    var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(prefabPath);
    if (prefab == null)
    {
        UnityEngine.Debug.LogError("Prefab not found at " + prefabPath);
        return 0;
    }

    // Root CardScript
    var cardScript = prefab.GetComponent<CardScript>();
    if (cardScript != null)
    {
        UnityEngine.Debug.Log("[CardScript] cardTypeID=" + cardScript.cardTypeID
            + ", isMinion=" + cardScript.isMinion
            + ", buryCost=" + cardScript.buryCost
            + ", delayCost=" + cardScript.delayCost
            + ", exposeCost=" + cardScript.exposeCost
            + ", minionCostCount=" + cardScript.minionCostCount);
    }

    // Children
    foreach (UnityEngine.Transform child in prefab.transform)
    {
        var container = child.GetComponent<CostNEffectContainer>();
        if (container != null)
        {
            UnityEngine.Debug.Log("=== CostNEffectContainer: " + child.name + " ===");
            
            var so = new UnityEditor.SerializedObject(container);
            var effectEventProp = so.FindProperty("effectEvent");
            if (effectEventProp != null)
            {
                var persistentCalls = effectEventProp.FindPropertyRelative("m_PersistentCalls.m_Calls");
                if (persistentCalls != null)
                {
                    UnityEngine.Debug.Log("EffectEvent call count: " + persistentCalls.arraySize);
                    for (int i = 0; i < persistentCalls.arraySize; i++)
                    {
                        var call = persistentCalls.GetArrayElementAtIndex(i);
                        string targetType = call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue;
                        string methodName = call.FindPropertyRelative("m_MethodName").stringValue;
                        UnityEngine.Debug.Log("  Call " + i + ": Target=" + targetType + ", Method=" + methodName);
                    }
                }
            }

            var hpAlter = child.GetComponent<HPAlterEffect>();
            if (hpAlter != null)
            {
                UnityEngine.Debug.Log("[HPAlterEffect] baseDmg="
                    + (hpAlter.baseDmg != null ? hpAlter.baseDmg.value.ToString() : "null")
                    + ", isStatusEffectDamage=" + hpAlter.isStatusEffectDamage
                    + ", extraDmg=" + hpAlter.extraDmg);
            }
        }
    }
    return 0;
}
```

## Template 2: Read Any Serialized Property by Name

```csharp
{
    var prefabPath = "Assets/Prefabs/Cards/3.0 no cost (current)/General/血肉聚集体.prefab";
    var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(prefabPath);
    var component = prefab.GetComponentInChildren<HPAlterEffect>();
    
    var so = new UnityEditor.SerializedObject(component);
    var prop = so.FindProperty("baseDmg");
    if (prop != null)
    {
        // For object reference (ScriptableObject)
        var objRef = prop.objectReferenceValue;
        UnityEngine.Debug.Log("baseDmg ref=" + (objRef != null ? objRef.name : "null"));
    }
    return 0;
}
```

## Template 3: Read Array / List Field

```csharp
{
    var prefabPath = "Assets/Prefabs/Cards/3.0 no cost (current)/General/血肉聚集体.prefab";
    var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(prefabPath);
    var cardScript = prefab.GetComponent<CardScript>();
    
    var so = new UnityEditor.SerializedObject(cardScript);
    var listProp = so.FindProperty("myStatusEffects");
    if (listProp != null)
    {
        UnityEngine.Debug.Log("myStatusEffects count: " + listProp.arraySize);
        for (int i = 0; i < listProp.arraySize; i++)
        {
            var elem = listProp.GetArrayElementAtIndex(i);
            UnityEngine.Debug.Log("  [" + i + "]=" + elem.enumDisplayNames[elem.enumValueIndex]);
        }
    }
    return 0;
}
```

## Template 4: Read Enum Value

```csharp
{
    var prefabPath = "Assets/Prefabs/Cards/3.0 no cost (current)/General/血肉聚集体.prefab";
    var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(prefabPath);
    var cardScript = prefab.GetComponent<CardScript>();
    
    var so = new UnityEditor.SerializedObject(cardScript);
    var enumProp = so.FindProperty("minionCostOwner");
    if (enumProp != null)
    {
        UnityEngine.Debug.Log("minionCostOwner=" + enumProp.enumDisplayNames[enumProp.enumValueIndex]);
    }
    return 0;
}
```

## Template 5: Batch Inspect Multiple Prefabs

```csharp
{
    string[] paths = new string[] {
        "Assets/Prefabs/Cards/3.0 no cost (current)/General/血肉聚集体.prefab",
        "Assets/Prefabs/Cards/3.0 no cost (current)/General/SomeOtherCard.prefab"
    };
    
    foreach (var path in paths)
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(path);
        if (prefab == null) continue;
        var cardScript = prefab.GetComponent<CardScript>();
        UnityEngine.Debug.Log("[" + prefab.name + "] cardTypeID=" + (cardScript != null ? cardScript.cardTypeID : "N/A"));
    }
    return 0;
}
```

## Field Type Cheat Sheet

| Inspector Type | SerializedProperty Access |
|----------------|---------------------------|
| int | `.intValue` |
| float | `.floatValue` |
| bool | `.boolValue` |
| string | `.stringValue` |
| Enum | `.enumValueIndex` + `.enumDisplayNames[]` |
| UnityEngine.Object (SO, GameObject) | `.objectReferenceValue` |
| Array/List | `.arraySize` + `.GetArrayElementAtIndex(i)` |
| Vector3 | `.FindPropertyRelative("x")` etc. |
