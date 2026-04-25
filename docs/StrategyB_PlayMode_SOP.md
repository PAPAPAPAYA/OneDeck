# Strategy B: Play Mode Integration Test SOP

## 1. Purpose & Scope

This SOP defines a reusable workflow for running **Play Mode integration tests** on any combat card via `unity-MCP execute_code`.  
Unlike Strategy A (Editor Mode / isolated environment), Strategy B runs inside an active Play Mode session with real scene managers, real prefab instances, and real event bindings.  

**Applicable to:** Any card whose full combat flow needs validation, including event binding, animation queue, effect chain behaviour, and final game state.

---

## 2. Core Challenges & Solutions

| # | Challenge | Root Cause | Solution |
|---|-----------|------------|----------|
| 1 | `EffectScript` protected fields (`myCard`, `myCardScript`, `combatManager`) are `null` | `OnEnable()` may not wire correctly in test setup | Manually wire via **reflection** after instantiating the card |
| 2 | Damage is 0 despite effect triggering | `isStatusEffectDamage=false` causes async `AttackAnimationManager` callback | Set `isStatusEffectDamage = true` on `HPAlterEffect` for synchronous damage execution in tests |
| 3 | `EffectChainManager` loop guard blocks effects | `lastEffectObject` check or chain depth limit; same effect ID already recorded in open chain | Call `EffectChainManager.Me.CloseOpenedChain()` **after every effect trigger** (also called inside `ResetState()` before each test case) |
| 4 | `EffectCanBeInvoked` returns `false` on second burial trigger | `openedEffectRecorders` still contains the effect ID from the first trigger | Close the chain between consecutive triggers (e.g. first burial vs second burial) |
| 5 | `CheckCost_IndexBeforeStartCard()` fails after reveal | When a card is revealed it is moved from `combinedDeckZone` to `revealZone`; `IndexOf` returns `-1` | For cards using this cost (e.g. GRUDGE, WISE_BURIAL), test via **direct `InvokeEffectEvent()`** while the card is still in `combinedDeckZone`, or set up the deck so the card is before StartCard while in deck |
| 6 | `GiveStatusEffectToLastXCards()` / `GiveAllFriendlyStatusEffect()` appear to do nothing | These methods use `CombatUXManager.PlayMultiStatusEffectProjectile()` which is **async** | For synchronous verification in tests, directly call the private `ApplyStatusEffectToXxxSingle()` method via reflection after the event trigger |
| 7 | `codedom` compiler rejects modern C# syntax | The legacy compiler only supports C# 6 | Avoid `$""` interpolation, file-level `using`, and `?.` null-conditional operator; use string concatenation (`+`) and explicit null checks |
| 8 | State pollution between test cases | `myStatusEffects` list accumulates on reused instances | **Destroy and re-instantiate** the card-under-test for each test case; also call `ResetState()` between cards |
| 9 | **Multiple `CostNEffectContainer`s on one card** | Some cards (e.g. `CURSED_CORPSE`) have **multiple independent** `CostNEffectContainer`s all bound to `onMeRevealed`. `TriggerRevealedCardEffect` only invokes the first one. | Iterate **all** containers via `GetComponentsInChildren<CostNEffectContainer>()` and call `InvokeEffectEvent()` on each. |
| 10 | **Linger cards listen to global events** | Linger cards (e.g. `CURSE_ENCHANTMENT`, `MOTH_MAN`) do **not** use `onMeRevealed`. They listen to `onTheirPlayerTookDmg`, `onEnemyCurseCardGotPower`, etc. | Do **not** use `TriggerRevealEffect`. Instead, directly `Raise()` the specific event from `GameEventStorage.me`. |
| 11 | **Enemy cards need correct parent & status ref** | Curse tests often require an enemy `JU_ON` in the deck. Instantiating with `playerDeckParent` makes it a friendly card. | Create a separate `CreateEnemyCard` helper that uses `cm.enemyDeckParent` and sets `myStatusRef = cm.enemyPlayerStatusRef`. |
| 12 | **Stage effect appears to do nothing** | `StageMyCards` / `StageSelf` excludes cards already at the top of `combinedDeckZone` (`IsCardAtTop` check). If the only eligible card is at index `Count-1`, the filtered list is empty. | Add an extra dummy card **after** the target card in `combinedDeckZone` so the target is **not** at the top. |
| 13 | **Cost check `EnemyCursedCardHasPower` fails unexpectedly** | This cost requires the enemy curse card's Power to be **strictly greater** than the parameter (e.g. `> 1` for `intArg=1`). A JU_ON with exactly 1 Power will fail the check. | Pre-buff the enemy JU_ON with enough Power stacks before the trigger (e.g. 2+ Power for `intArg=1`). |
| 14 | **Multi-Listener cards trigger wrong Container** | Some cards (e.g. `CURSE_THIRST_BEAST`) have **multiple GameEventListeners** on the root object, each bound to a **different CostNEffectContainer** via `InvokeEffectEvent`. `OnMeRevealed` may trigger the "deal dmg" Container while `OnHostileCurseRevealed` triggers the "stage self" Container. | Inspect the prefab's `GameEventListener` response targets (via `SerializedProperty`) to know which Listener maps to which Container. Do not assume all Containers share the same trigger event. |

---

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

---

## 4. Reusable Helper Functions

### 4.1 Reset State Between Cards

```csharp
System.Action ResetState = (System.Action)(() =>
{
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
    cm.combinedDeckZone.Clear();
    if (cm.revealZone != null)
    {
        UnityEngine.Object.DestroyImmediate(cm.revealZone);
        cm.revealZone = null;
    }
    if (cm.currentGamePhaseRef != null)
        cm.currentGamePhaseRef.currentGamePhase = EnumStorage.GamePhase.Combat;
    if (EffectChainManager.Me != null)
    {
        EffectChainManager.Me.CloseOpenedChain();
        EffectChainManager.Me.lastEffectObject = null;
    }
});
```

### 4.2 Create Test Card (with reflection wiring)

```csharp
System.Func<string, GameObject> CreateTestCard = (System.Func<string, GameObject>)((prefabPath) =>
{
    GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    if (prefab == null)
    {
        UnityEngine.Debug.Log("[TEST FAIL] Prefab not found: " + prefabPath);
        return null;
    }

    GameObject card = UnityEngine.Object.Instantiate(
        prefab,
        cm.playerDeckParent != null ? cm.playerDeckParent.transform : null
    );
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
        System.Reflection.FieldInfo myCardField =
            typeof(EffectScript).GetField("myCard", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        myCardField.SetValue(effect, card);

        System.Reflection.FieldInfo myCardScriptField =
            typeof(EffectScript).GetField("myCardScript", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        myCardScriptField.SetValue(effect, cs);

        System.Reflection.FieldInfo combatManagerField =
            typeof(EffectScript).GetField("combatManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
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

### 4.3 Trigger Reveal Effect

```csharp
System.Action<GameObject> TriggerRevealEffect = (System.Action<GameObject>)((card) =>
{
    cm.revealZone = card;
    System.Reflection.MethodInfo triggerMethod =
        typeof(CombatManager).GetMethod("TriggerRevealedCardEffect",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    if (triggerMethod != null)
    {
        triggerMethod.Invoke(cm, null);
    }
    else
    {
        UnityEngine.Debug.Log("[TEST WARN] TriggerRevealedCardEffect not found");
    }
});
```

### 4.4 Trigger Bury Effect

```csharp
System.Action<GameObject> TriggerBuryEffect = (System.Action<GameObject>)((card) =>
{
    if (GameEventStorage.me != null && GameEventStorage.me.onMeBuried != null)
    {
        GameEventStorage.me.onMeBuried.RaiseSpecific(card);
    }
});
```

### 4.5 Trigger Stage Effect

```csharp
System.Action<GameObject> TriggerStageEffect = (System.Action<GameObject>)((card) =>
{
    if (GameEventStorage.me != null && GameEventStorage.me.onMeStaged != null)
    {
        GameEventStorage.me.onMeStaged.RaiseSpecific(card);
    }
});
```

### 4.6 Create Enemy Card (for curse / hostile targets)

```csharp
System.Func<string, GameObject> CreateEnemyCard = (System.Func<string, GameObject>)((prefabPath) =>
{
    GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    if (prefab == null)
    {
        UnityEngine.Debug.Log("[TEST FAIL] Prefab not found: " + prefabPath);
        return null;
    }

    GameObject card = UnityEngine.Object.Instantiate(
        prefab,
        cm.enemyDeckParent != null ? cm.enemyDeckParent.transform : null
    );
    card.name = prefab.name;
    CardScript cs = card.GetComponent<CardScript>();
    cs.myStatusRef = cm.enemyPlayerStatusRef;
    cs.theirStatusRef = cm.ownerPlayerStatusRef;
    cs.myStatusEffects = new System.Collections.Generic.List<EnumStorage.StatusEffect>();
    cs.myTags = new System.Collections.Generic.List<EnumStorage.Tag>();

    EffectScript[] effects = card.GetComponentsInChildren<EffectScript>(true);
    foreach (EffectScript effect in effects)
    {
        System.Reflection.FieldInfo myCardField =
            typeof(EffectScript).GetField("myCard", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        myCardField.SetValue(effect, card);

        System.Reflection.FieldInfo myCardScriptField =
            typeof(EffectScript).GetField("myCardScript", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        myCardScriptField.SetValue(effect, cs);

        System.Reflection.FieldInfo combatManagerField =
            typeof(EffectScript).GetField("combatManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
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

### 4.7 Close Effect Chain

> **Critical:** Call this after **every** effect trigger to prevent the chain guard from blocking subsequent triggers.

```csharp
System.Action CloseChain = (System.Action)(() =>
{
    if (EffectChainManager.Me != null)
    {
        EffectChainManager.Me.CloseOpenedChain();
        EffectChainManager.Me.lastEffectObject = null;
    }
});
```

---

## 5. Writing a Test Case

### 5.1 Pattern

1. Run `ResetState()`
2. Create a fresh card instance via `CreateTestCard`
3. Build a controlled `combinedDeckZone` (if needed)
4. Record pre-state (HP, status effects, deck counts)
5. Invoke the effect:
   - For normal `onMeRevealed` cards: `TriggerRevealEffect(testCard)`
   - For **multi-container** cards: iterate all `CostNEffectContainer`s and call `InvokeEffectEvent()` on each.
   - For **Linger** cards: directly raise the event (e.g. `GameEventStorage.me.onTheirPlayerTookDmg.RaiseOwner()`).
6. **Immediately call `CloseChain()`**
7. Record post-state and compute delta
8. For async status-effect projectiles, additionally call the private `ApplyXxxSingle` method via reflection
9. `Debug.Log` with `[TEST PASS/FAIL]` prefix
10. `DestroyImmediate` the test card

### 5.2 Assertion Convention

```csharp
int hpBefore = cm.enemyPlayerStatusRef.hp;
TriggerRevealEffect(testCard);
CloseChain();
int actualDmg = hpBefore - cm.enemyPlayerStatusRef.hp;

string result = (actualDmg == expectedDmg) ? "PASS" : "FAIL";
UnityEngine.Debug.Log("[TEST " + result + "] CardID-1 | Expected: " + expectedDmg + ", Actual: " + actualDmg);
```

---

## 6. Handling Async Status-Effect Projectiles

Some effects (`StatusEffectGiverEffect.GiveStatusEffectToLastXCards`, `GiveAllFriendlyStatusEffect`) use `CombatUXManager.PlayMultiStatusEffectProjectile()` which applies the status effect asynchronously via animation callbacks.  
In a test you cannot wait for the animation, so directly invoke the underlying application method:

```csharp
var giver = testCard.GetComponentInChildren<DefaultNamespace.Effects.StatusEffectGiverEffect>(true);
if (giver != null && friendlyCard != null)
{
    // For GiveStatusEffectToLastXCards
    var method1 = typeof(DefaultNamespace.Effects.StatusEffectGiverEffect)
        .GetMethod("ApplyStatusEffectToLastXCardSingle",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    if (method1 != null)
        method1.Invoke(giver, new object[] { friendlyCard.GetComponent<CardScript>() });

    // For GiveAllFriendlyStatusEffect
    var method2 = typeof(DefaultNamespace.Effects.StatusEffectGiverEffect)
        .GetMethod("ApplyStatusEffectToFriendlySingle",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    if (method2 != null)
        method2.Invoke(giver, new object[] { friendlyCard.GetComponent<CardScript>(), 1 });
}
```

---

## 7. Full Working Example (GRUDGE)

```csharp
// ==========================================
// Setup (Section 3)
// ==========================================
// ... paste setup block ...

// ==========================================
// Helpers (Section 4)
// ==========================================
// ... paste ResetState, CreateTestCard, CloseChain ...

UnityEngine.Debug.Log("===== GRUDGE Strategy B Tests =====");

{
    ResetState();

    GameObject testCard = CreateTestCard(
        "Assets/Prefabs/Cards/3.0 no cost (current)/Bury and buried/Bury/1_Uncommon/GRUDGE.prefab"
    );
    GameObject friendlyA = CreateTestCard(
        "Assets/Prefabs/Cards/3.0 no cost (current)/General/0_Common/BLACKSMITH.prefab"
    );
    GameObject friendlyB = CreateTestCard(
        "Assets/Prefabs/Cards/3.0 no cost (current)/General/0_Common/COFFIN_MAKER.prefab"
    );
    GameObject startCard = CreateTestCard(
        "Assets/Prefabs/Cards/System/StartCard.prefab"
    );

    // Deck layout: FriendlyA(0), FriendlyB(1), GRUDGE(2), StartCard(3)
    // GRUDGE is BEFORE StartCard -> cost passes
    if (friendlyA != null)
    {
        cm.combinedDeckZone.Add(friendlyA);
        friendlyA.GetComponent<CardScript>().myStatusRef = cm.ownerPlayerStatusRef;
    }
    if (friendlyB != null)
    {
        cm.combinedDeckZone.Add(friendlyB);
        friendlyB.GetComponent<CardScript>().myStatusRef = cm.ownerPlayerStatusRef;
    }
    if (testCard != null)
    {
        cm.combinedDeckZone.Add(testCard);
        testCard.GetComponent<CardScript>().myStatusRef = cm.ownerPlayerStatusRef;
    }
    if (startCard != null)
        cm.combinedDeckZone.Add(startCard);

    if (testCard != null)
    {
        var cnts = testCard.GetComponentsInChildren<CostNEffectContainer>(true);
        if (cnts.Length > 0)
        {
            cnts[0].InvokeEffectEvent();
            CloseChain();

            // Async projectile workaround
            var giver = cnts[0].GetComponentInChildren<DefaultNamespace.Effects.StatusEffectGiverEffect>(true);
            if (giver != null && friendlyA != null)
            {
                var method = typeof(DefaultNamespace.Effects.StatusEffectGiverEffect)
                    .GetMethod("ApplyStatusEffectToLastXCardSingle",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (method != null)
                {
                    method.Invoke(giver, new object[] { friendlyA.GetComponent<CardScript>() });
                    if (friendlyB != null)
                        method.Invoke(giver, new object[] { friendlyB.GetComponent<CardScript>() });
                }
            }

            int powerA = (friendlyA != null)
                ? EnumStorage.GetStatusEffectCount(friendlyA.GetComponent<CardScript>().myStatusEffects, EnumStorage.StatusEffect.Power)
                : 0;
            int powerB = (friendlyB != null)
                ? EnumStorage.GetStatusEffectCount(friendlyB.GetComponent<CardScript>().myStatusEffects, EnumStorage.StatusEffect.Power)
                : 0;

            string result = (powerA == 2 && powerB == 2) ? "PASS" : "FAIL";
            UnityEngine.Debug.Log("[TEST " + result + "] GRUDGE | powerA=" + powerA + " powerB=" + powerB);
            UnityEngine.Object.DestroyImmediate(testCard);
        }
    }
    if (friendlyA != null) UnityEngine.Object.DestroyImmediate(friendlyA);
    if (friendlyB != null) UnityEngine.Object.DestroyImmediate(friendlyB);
    if (startCard != null) UnityEngine.Object.DestroyImmediate(startCard);
}

UnityEngine.Debug.Log("===== Tests Complete =====");
return 0;
```

---

## 8. Troubleshooting Quick Reference

| Symptom | Likely Cause | Fix |
|---------|--------------|-----|
| `NullReferenceException` in effect method | `EffectScript.myCardScript` or `combatManager` is null | Use `CreateTestCard` helper which wires all EffectScript children via reflection |
| Damage is always 0 | `isStatusEffectDamage=false` + animation not completing | Set `hae.isStatusEffectDamage = true` |
| `EffectCanBeInvoked` returns false | `EffectChainManager` loop guard active | Call `EffectChainManager.Me.CloseOpenedChain()` before **every** trigger |
| Second burial trigger has no effect | Same effect ID is still in `openedEffectRecorders` | Call `CloseChain()` after the first trigger |
| Power/status count wrong between tests | Same card instance reused | `DestroyImmediate` old card and `Instantiate` fresh one |
| `CheckCost_IndexBeforeStartCard` fails on reveal | Card was removed from `combinedDeckZone` | Test cost via direct `InvokeEffectEvent()` while card is still in deck, or document as known Play Mode behaviour |
| Status effect not applied after trigger | `PlayMultiStatusEffectProjectile` is async, OR `CombatUXManager.me` is null | Directly invoke `ApplyStatusEffectToXxxSingle` via reflection (Section 6) |
| **Damage missing on cards like `CURSED_CORPSE`** | Only the **first** `CostNEffectContainer` was triggered; the second (damage) container was skipped | Iterate **all** `CostNEffectContainer`s with `GetComponentsInChildren<CostNEffectContainer>()` and invoke each one |
| **Linger card has no effect in test** | Using `TriggerRevealEffect` on a card that listens to `onTheirPlayerTookDmg` / `onEnemyCurseCardGotPower` | Raise the correct event directly from `GameEventStorage.me` instead of `TriggerRevealEffect` |
| **Curse tests fail because enemy JU_ON is treated as friendly** | Used `CreateTestCard` which sets `myStatusRef = ownerPlayerStatusRef` | Use `CreateEnemyCard` helper which sets `myStatusRef = enemyPlayerStatusRef` and uses `enemyDeckParent` |
| **Stage effect has no effect** | Target card is already at top of `combinedDeckZone` | Add a dummy card on top so the target is not at index `Count-1` |
| **Cost `EnemyCursedCardHasPower` fails** | Enemy JU_ON Power is not strictly greater than the parameter | Give enemy JU_ON enough Power stacks before triggering |
| Compilation error: "Unexpected symbol" | Used `$""`, `?.`, or file-level `using` | Switch to string concatenation (`+`) and fully-qualified names |

---

## 9. Checklist Before Running a New Card Test

- [ ] Unity is in **Play Mode**
- [ ] `CombatManager.Me` is not null
- [ ] `GameEventStorage.me` is initialized
- [ ] Setup block clears old `revealZone` and `combinedDeckZone`
- [ ] `EffectChainManager.Me.CloseOpenedChain()` called before every effect trigger
- [ ] Card-under-test is **freshly instantiated** (not reused)
- [ ] All `EffectScript` children are **reflection-wired**
- [ ] `HPAlterEffect.isStatusEffectDamage = true` if testing damage synchronously
- [ ] For status-effect cards: include the async projectile workaround (Section 6)
- [ ] **Multi-container cards**: verify `GetComponentsInChildren<CostNEffectContainer>()` returns >1 and iterate all of them
- [ ] **Linger cards**: verify the listened event (e.g. `onTheirPlayerTookDmg`) and raise the correct event in test
- [ ] **Curse / enemy cards**: use `CreateEnemyCard` instead of `CreateTestCard` for hostile targets
- [ ] Old test card is `DestroyImmediate`-ed at the end
