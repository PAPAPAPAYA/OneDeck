---
name: unity-card-playmode-test
description: Execute Strategy B (Play Mode Integration Test) for any OneDeck card prefab. Use when the user wants to run a real Play Mode test on a card's effects, verify event bindings, animation behavior, and end-to-end combat flow.
---

# Unity Card Play Mode Integration Test (Strategy B)

This skill provides a reusable workflow for running **Strategy B — Play Mode Integration Tests** on any combat card in the OneDeck project.

## 1. What is Strategy B?

| Aspect | Description |
|--------|-------------|
| **Purpose** | Validate the full combat flow including event binding, animation queue, console logs, and final game state |
| **Environment** | Unity Play Mode (real scene, real managers, real prefab instances) |
| **Trigger** | `execute_code` in Play Mode |
| **Scope** | End-to-end effect invocation via `GameEvent.RaiseSpecific()` |

## 2. Core Challenges & Solutions

| # | Challenge | Root Cause | Solution |
|---|-----------|------------|----------|
| 1 | `EffectScript` protected fields (`myCard`, `myCardScript`, `combatManager`) are `null` after `Instantiate` | `OnEnable()` may not wire them correctly in test setup | Manually wire via **reflection** after instantiating the card |
| 2 | Damage is 0 despite effect triggering | `isStatusEffectDamage=false` causes async `AttackAnimationManager` callback | Set `isStatusEffectDamage = true` on `HPAlterEffect` for synchronous damage execution in tests |
| 3 | `codedom` rejects `yield return`, `$""`, `?.` | Legacy C# 6 compiler | Use string concatenation (`+`), explicit null checks, and no coroutines |
| 4 | `GameEventListener` type not found in `execute_code` | Namespace resolution issues | Use `System.Type.GetType("GameEventListener, Assembly-CSharp")` |
| 5 | `SerializedProperty` name mismatch | Field declared as `public GameEvent @event;` | Use `so.FindProperty("event")`, not `"gameEvent"` |
| 6 | State pollution between tests | `myStatusEffects` list accumulates on reused instances | **Destroy and re-instantiate** the card-under-test for each test case |
| 7 | `EffectChainManager` loop guard blocks effects | `lastEffectObject` check or chain depth limit | Call `EffectChainManager.Me.CloseOpenedChain()` before invoking effects |
| 8 | `GamePhaseSO` has no `SetValue()` method | API mismatch | Directly assign `cm.currentGamePhaseRef.currentGamePhase = EnumStorage.GamePhase.Combat;` |

## 3. Standard Play Mode Setup Template

Copy this block at the beginning of every Strategy B test script.

```csharp
// ==========================================
// 0. Ensure Play Mode is active
// ==========================================
if (!UnityEditor.EditorApplication.isPlaying)
{
    UnityEngine.Debug.Log("[TEST FAIL] Must be in Play Mode");
    return 1;
}

// ==========================================
// 1. Get CombatManager
// ==========================================
CombatManager cm = CombatManager.Me;
if (cm == null)
{
    UnityEngine.Debug.Log("[TEST FAIL] CombatManager.Me is null");
    return 1;
}

// ==========================================
// 2. Reset player HP
// ==========================================
if (cm.ownerPlayerStatusRef != null)
{
    cm.ownerPlayerStatusRef.hp = 100;
    cm.ownerPlayerStatusRef.hpMax = 100;
    cm.ownerPlayerStatusRef.shield = 0;
}
if (cm.enemyPlayerStatusRef != null)
{
    cm.enemyPlayerStatusRef.hp = 100;
    cm.enemyPlayerStatusRef.hpMax = 100;
    cm.enemyPlayerStatusRef.shield = 0;
}

// ==========================================
// 3. Reset deck & reveal zone
// ==========================================
cm.combinedDeckZone.Clear();
if (cm.revealZone != null)
{
    UnityEngine.Object.DestroyImmediate(cm.revealZone);
    cm.revealZone = null;
}

// ==========================================
// 4. Set game phase to Combat
// ==========================================
if (cm.currentGamePhaseRef != null)
{
    cm.currentGamePhaseRef.currentGamePhase = EnumStorage.GamePhase.Combat;
}

// ==========================================
// 5. Close any open effect chains
// ==========================================
if (EffectChainManager.Me != null)
{
    EffectChainManager.Me.CloseOpenedChain();
    EffectChainManager.Me.lastEffectObject = null;
}
```

## 4. Card Instantiation & Wiring Helper

```csharp
System.Func<string, GameObject> CreateTestCard = (System.Func<string, GameObject>)((prefabPath) =>
{
    GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    if (prefab == null)
    {
        UnityEngine.Debug.Log("[TEST FAIL] Prefab not found: " + prefabPath);
        return null;
    }

    GameObject card = UnityEngine.Object.Instantiate(prefab, cm.playerDeckParent != null ? cm.playerDeckParent.transform : null);
    card.name = prefab.name;
    CardScript cs = card.GetComponent<CardScript>();
    cs.myStatusRef = cm.ownerPlayerStatusRef;
    cs.theirStatusRef = cm.enemyPlayerStatusRef;
    cs.myStatusEffects = new System.Collections.Generic.List<EnumStorage.StatusEffect>();
    cs.myTags = new System.Collections.Generic.List<EnumStorage.Tag>();

    // Wire all EffectScript children
    EffectScript[] effects = card.GetComponentsInChildren<EffectScript>(true);
    foreach (EffectScript effect in effects)
    {
        System.Reflection.FieldInfo myCardField = typeof(EffectScript).GetField("myCard", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        myCardField.SetValue(effect, card);
        System.Reflection.FieldInfo myCardScriptField = typeof(EffectScript).GetField("myCardScript", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        myCardScriptField.SetValue(effect, cs);
        System.Reflection.FieldInfo combatManagerField = typeof(EffectScript).GetField("combatManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        combatManagerField.SetValue(effect, cm);
    }

    // For HPAlterEffect: set synchronous damage mode for testing
    HPAlterEffect[] hpaEffects = card.GetComponentsInChildren<HPAlterEffect>(true);
    foreach (HPAlterEffect hae in hpaEffects)
    {
        hae.isStatusEffectDamage = true;
    }

    return card;
});
```

## 5. Effect Invocation Helper

```csharp
System.Action<GameObject> TriggerRevealEffect = (System.Action<GameObject>)((card) =>
{
    cm.revealZone = card;
    System.Reflection.MethodInfo triggerMethod = typeof(CombatManager).GetMethod("TriggerRevealedCardEffect", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    if (triggerMethod != null)
    {
        triggerMethod.Invoke(cm, null);
    }
    else
    {
        UnityEngine.Debug.Log("[TEST WARN] TriggerRevealedCardEffect not found");
    }
});

System.Action<GameObject> TriggerBuryEffect = (System.Action<GameObject>)((card) =>
{
    if (GameEventStorage.me != null && GameEventStorage.me.onMeBuried != null)
    {
        GameEventStorage.me.onMeBuried.RaiseSpecific(card);
    }
});
```

## 6. Writing a Test Case

### 6.1 Pattern

1. Run the **Setup Template** (Section 3)
2. Create a fresh card instance via `CreateTestCard`
3. Record pre-state (HP, status effects, deck counts)
4. Invoke the effect via `TriggerRevealEffect` or `TriggerBuryEffect`
5. Record post-state and compute delta
6. `Debug.Log` with `[TEST PASS/FAIL]` prefix
7. `DestroyImmediate` the test card

### 6.2 Assertion Convention

```csharp
int hpBefore = cm.enemyPlayerStatusRef.hp;
TriggerRevealEffect(testCard);
int actualDmg = hpBefore - cm.enemyPlayerStatusRef.hp;

string result = (actualDmg == expectedDmg) ? "PASS" : "FAIL";
UnityEngine.Debug.Log("[TEST " + result + "] CardID-1 | Expected: " + expectedDmg + ", Actual: " + actualDmg);
```

## 7. Full Working Example (AVENGER)

```csharp
// === Setup (Section 3) ===
// ... paste setup block ...

// === Helper (Section 4 + 5) ===
// ... paste CreateTestCard, TriggerRevealEffect, TriggerBuryEffect ...

UnityEngine.Debug.Log("===== AVENGER Strategy B Tests =====");

// ---------- AVENGER-1: Reveal Damage ----------
{
    string prefabPath = "Assets/Prefabs/Cards/3.0 no cost (current)/Bury and buried/DeathRattle/1_Uncommon/AVENGER.prefab";
    GameObject testCard = CreateTestCard(prefabPath);
    if (testCard == null) return 1;

    cm.enemyPlayerStatusRef.hp = 100;
    int hpBefore = cm.enemyPlayerStatusRef.hp;

    TriggerRevealEffect(testCard);

    int actualDmg = hpBefore - cm.enemyPlayerStatusRef.hp;
    int expectedDmg = 3; // baseDmg 2 + extraDmg 1
    string result = (actualDmg == expectedDmg) ? "PASS" : "FAIL";
    UnityEngine.Debug.Log("[TEST " + result + "] AVENGER-1 | Expected: " + expectedDmg + ", Actual: " + actualDmg);

    UnityEngine.Object.DestroyImmediate(testCard);
}

// ---------- AVENGER-2: Buried Power ----------
{
    string prefabPath = "Assets/Prefabs/Cards/3.0 no cost (current)/Bury and buried/DeathRattle/1_Uncommon/AVENGER.prefab";
    GameObject testCard = CreateTestCard(prefabPath);
    if (testCard == null) return 1;

    CardScript cs = testCard.GetComponent<CardScript>();
    int powerBefore = EnumStorage.GetStatusEffectCount(cs.myStatusEffects, EnumStorage.StatusEffect.Power);

    TriggerBuryEffect(testCard);

    int powerAfter = EnumStorage.GetStatusEffectCount(cs.myStatusEffects, EnumStorage.StatusEffect.Power);
    int powerGained = powerAfter - powerBefore;

    // Read actual bound parameter from prefab
    var giver = testCard.GetComponentInChildren<DefaultNamespace.Effects.StatusEffectGiverEffect>(true);
    int expectedPower = 1;
    if (giver != null)
    {
        var container = giver.GetComponent<CostNEffectContainer>();
        if (container != null)
        {
            var cso = new UnityEditor.SerializedObject(container);
            var effectEvent = cso.FindProperty("effectEvent");
            if (effectEvent != null)
            {
                var calls = effectEvent.FindPropertyRelative("m_PersistentCalls.m_Calls");
                if (calls != null && calls.arraySize > 0)
                {
                    var args = calls.GetArrayElementAtIndex(0).FindPropertyRelative("m_Arguments");
                    if (args != null) expectedPower = args.FindPropertyRelative("m_IntArgument").intValue;
                }
            }
        }
    }

    string result = (powerGained == expectedPower) ? "PASS" : "FAIL";
    UnityEngine.Debug.Log("[TEST " + result + "] AVENGER-2 | Expected Power: " + expectedPower + ", Gained: " + powerGained);

    UnityEngine.Object.DestroyImmediate(testCard);
}

UnityEngine.Debug.Log("===== Tests Complete =====");
return 0;
```

## 8. Per-Effect-Type Adaptation

| Effect Type | Extra Setup Required |
|-------------|----------------------|
| **HPAlterEffect** | Set `isStatusEffectDamage = true` for synchronous execution |
| **ShieldAlterEffect** | Check `shield` value on target `PlayerStatusSO` |
| **BuryEffect / StageEffect** | Populate `cm.combinedDeckZone` with target cards before invoking; verify deck order after |
| **StatusEffectGiverEffect** | Read `m_IntArgument` from `SerializedObject` to know the actual `amount` bound in the prefab |
| **CurseEffect** | Ensure enemy deck contains a curse card; verify `myStatusEffects` on the curse target |
| **MinionCostEffect** | Populate deck with matching `isMinion=true` cards before the reveal trigger |
| **AddTempCard** | Check `cm.combinedDeckZone.Count` before and after |

## 9. Troubleshooting Quick Reference

| Symptom | Likely Cause | Fix |
|---------|--------------|-----|
| `NullReferenceException` in effect method | `EffectScript.myCardScript` or `combatManager` is null | Use `CreateTestCard` helper which wires all EffectScript children |
| Damage is always 0 | `isStatusEffectDamage=false` + animation not completing | Set `hae.isStatusEffectDamage = true` |
| `EffectCanBeInvoked` returns false | `EffectChainManager` loop guard active | Call `EffectChainManager.Me.CloseOpenedChain()` before test |
| Power/status count wrong between tests | Same card instance reused | `DestroyImmediate` old card and `Instantiate` fresh one |
| `GameEventListener` event is null | Wrong `SerializedProperty` name | Use `"event"`, not `"gameEvent"` |
| Compilation error: "Unexpected symbol" | Used `$""`, `?.`, or file-level `using` | Switch to string concatenation and fully-qualified names |

## 10. Checklist Before Running

- [ ] Unity is in **Play Mode**
- [ ] `CombatManager.Me` is not null
- [ ] `GameEventStorage.me` is initialized
- [ ] Setup block clears old `revealZone` and `combinedDeckZone`
- [ ] `EffectChainManager.Me.CloseOpenedChain()` called
- [ ] Card-under-test is **freshly instantiated** (not reused)
- [ ] All `EffectScript` children are **reflection-wired**
- [ ] `HPAlterEffect.isStatusEffectDamage = true` if testing damage synchronously
- [ ] Old test card is `DestroyImmediate`-ed at the end
