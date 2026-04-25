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
| 9 | **Multiple `CostNEffectContainer`s on one card** | Some cards (e.g. `CURSED_CORPSE`) have **multiple independent** `CostNEffectContainer`s all bound to `onMeRevealed`. `TriggerRevealedCardEffect` only invokes the first one. | Iterate **all** containers via `GetComponentsInChildren<CostNEffectContainer>()` and call `InvokeEffectEvent()` on each. |
| 10 | **Linger cards listen to global events** | Linger cards (e.g. `CURSE_ENCHANTMENT`, `MOTH_MAN`) do **not** use `onMeRevealed`. They listen to `onTheirPlayerTookDmg`, `onEnemyCurseCardGotPower`, etc. | Do **not** use `TriggerRevealEffect`. Instead, directly `Raise()` the specific event from `GameEventStorage.me`. |
| 11 | **Enemy cards need correct parent & status ref** | Curse tests often require an enemy `JU_ON` in the deck. Instantiating with `playerDeckParent` makes it a friendly card. | Create a separate `CreateEnemyCard` helper that uses `cm.enemyDeckParent` and sets `myStatusRef = cm.enemyPlayerStatusRef`. |
| 12 | **Stage effect appears to do nothing** | `StageMyCards` / `StageSelf` excludes cards already at the top of `combinedDeckZone` (`IsCardAtTop` check). If the only eligible card is at index `Count-1`, the filtered list is empty. | Add an extra dummy card **after** the target card in `combinedDeckZone` so the target is **not** at the top. |
| 13 | **Cost check `EnemyCursedCardHasPower` fails unexpectedly** | This cost requires the enemy curse card's Power to be **strictly greater** than the parameter (e.g. `> 1` for `intArg=1`). A JU_ON with exactly 1 Power will fail the check. | Pre-buff the enemy JU_ON with enough Power stacks before the trigger (e.g. 2+ Power for `intArg=1`). |
| 14 | **Multi-Listener cards trigger wrong Container** | Some cards (e.g. `CURSE_THIRST_BEAST`) have **multiple GameEventListeners** on the root object, each bound to a **different CostNEffectContainer** via `InvokeEffectEvent`. `OnMeRevealed` may trigger the "deal dmg" Container while `OnHostileCurseRevealed` triggers the "stage self" Container. | Inspect the prefab's `GameEventListener` response targets (via `SerializedProperty`) to know which Listener maps to which Container. Do not assume all Containers share the same trigger event. |

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

### Create Enemy Card (for curse / hostile targets)

```csharp
System.Func<string, GameObject> CreateEnemyCard = (System.Func<string, GameObject>)((prefabPath) =>
{
    GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    if (prefab == null)
    {
        UnityEngine.Debug.Log("[TEST FAIL] Prefab not found: " + prefabPath);
        return null;
    }

    GameObject card = UnityEngine.Object.Instantiate(prefab, cm.enemyDeckParent != null ? cm.enemyDeckParent.transform : null);
    card.name = prefab.name;
    CardScript cs = card.GetComponent<CardScript>();
    cs.myStatusRef = cm.enemyPlayerStatusRef;
    cs.theirStatusRef = cm.ownerPlayerStatusRef;
    cs.myStatusEffects = new System.Collections.Generic.List<EnumStorage.StatusEffect>();
    cs.myTags = new System.Collections.Generic.List<EnumStorage.Tag>();

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

System.Action TriggerFriendlyCardExiled = (System.Action)(() =>
{
    if (GameEventStorage.me != null && GameEventStorage.me.onFriendlyCardExiled != null)
    {
        GameEventStorage.me.onFriendlyCardExiled.RaiseOwner();
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
| **CurseEffect** | Ensure enemy deck contains a curse card; verify `myStatusEffects` on the curse target. **Note**: `CurseEffect.EnhanceCurse` uses `PlayMultiStatusEffectProjectile` (async DOTween). Final Power state may not be observable in a single-frame test. Verify via reflection that `FindEnemyCardWithTypeID` succeeds and no exceptions are thrown. Also check `CurseEffect.cardTypeID` references a `StringSO` with `reset=false` (otherwise `value` becomes empty in Play Mode). **Use `CreateEnemyCard` for hostile JU_ON.** |
| **CurseEffect.ConsumeHostileCursePower** | This method searches all enemy JU_ONs and consumes Power layer by layer. If total Power < amount, it returns silently. The UnityEvent caller (e.g. `CostNEffectContainer`) continues to the next bound method. |
| **StageEffect** | Populate `cm.combinedDeckZone` with target cards before invoking; verify deck order after. **Critical**: `StageMyCards` / `StageSelf` excludes cards already at the top (index = Count-1). Add a dummy card above the target to prevent exclusion. |
| **Multi-Listener Cards** | Cards like `CURSE_THIRST_BEAST` have multiple `GameEventListener`s each targeting a different `CostNEffectContainer`. Use `SerializedObject` on the listener's `response` property to inspect which Container each listener invokes. |
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
| `CurseEffect` / `StringSO` config null in Play Mode | `StringSO.reset=true` clears `value` on `OnEnable` | Set `reset=false` on persistent config SOs (e.g., `CurseCardTypeID.asset`) |
| `onFriendlyCardExiled` has no listeners after creating card | `RaiseSpecific(card)` only triggers listeners on the target GameObject | Use `RaiseOwner()` for faction-scoped events like `onFriendlyCardExiled` |
| Status effect (Power) not applied after `CurseEffect` triggers | `PlayMultiStatusEffectProjectile` is async; requires frame updates | In single-frame tests, verify effect chain starts without errors instead of final state |
| **Damage missing on cards like `CURSED_CORPSE`** | Only the **first** `CostNEffectContainer` was triggered; the second (damage) container was skipped | Iterate **all** `CostNEffectContainer`s with `GetComponentsInChildren<CostNEffectContainer>()` and invoke each one |
| **Linger card has no effect in test** | Using `TriggerRevealEffect` on a card that listens to `onTheirPlayerTookDmg` / `onEnemyCurseCardGotPower` | Raise the correct event directly from `GameEventStorage.me` instead of `TriggerRevealEffect` |
| **Curse tests fail because enemy JU_ON is treated as friendly** | Used `CreateTestCard` which sets `myStatusRef = ownerPlayerStatusRef` | Use `CreateEnemyCard` helper which sets `myStatusRef = enemyPlayerStatusRef` and uses `enemyDeckParent` |
| **Stage effect has no effect** | Target card is already at top of `combinedDeckZone` | Add a dummy card on top so the target is not at index `Count-1` |
| **Cost `EnemyCursedCardHasPower` fails** | Enemy JU_ON Power is not strictly greater than the parameter | Give enemy JU-On enough Power stacks before triggering |

## 10. Checklist Before Running

- [ ] Unity is in **Play Mode**
- [ ] `CombatManager.Me` is not null
- [ ] `GameEventStorage.me` is initialized
- [ ] Setup block clears old `revealZone` and `combinedDeckZone`
- [ ] `EffectChainManager.Me.CloseOpenedChain()` called
- [ ] Card-under-test is **freshly instantiated** (not reused)
- [ ] All `EffectScript` children are **reflection-wired**
- [ ] `HPAlterEffect.isStatusEffectDamage = true` if testing damage synchronously
- [ ] **Multi-container cards**: verify `GetComponentsInChildren<CostNEffectContainer>()` returns >1 and iterate all of them
- [ ] **Linger cards**: verify the listened event (e.g. `onTheirPlayerTookDmg`) and raise the correct event in test
- [ ] **Curse / enemy cards**: use `CreateEnemyCard` instead of `CreateTestCard` for hostile targets
- [ ] Old test card is `DestroyImmediate`-ed at the end
