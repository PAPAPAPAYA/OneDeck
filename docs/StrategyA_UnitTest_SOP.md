# Strategy A: Programmatic Unit Test SOP

## 1. Purpose & Scope

This SOP defines a reusable, copy-paste workflow for running **Editor Mode programmatic unit tests** on any combat card via `unity-MCP execute_code`.  
It eliminates UI, animation, and scene dependency, making iteration fast (seconds per test run).

**Applicable to:** Any card whose effect logic can be isolated and invoked manually (e.g. `HPAlterEffect`, `ShieldAlterEffect`, `BuryEffect`, etc.).

---

## 2. Core Challenges & Solutions

| # | Challenge | Root Cause | Solution |
|---|-----------|------------|----------|
| 1 | `EffectScript` protected fields (`myCard`, `myCardScript`, `combatManager`) are `null` | `OnEnable()` is unreliable in `execute_code`; `transform.parent` may be null when component is added | Manually set fields via **reflection** after parenting is complete |
| 2 | Singleton static fields (`CombatManager.Me`, `ValueTrackerManager.me`, etc.) are stale or null | `Awake()` does not run reliably in `execute_code`; old scene instances may still exist | **Destroy existing singletons** + **manually assign static fields** |
| 3 | `CardScript.OnEnable()` crashes with NRE | It calls `CardIDRetriever.Me.RetrieveCardID()` but the singleton may be missing | Create `CardIDRetriever` and assign its static `Me` field before creating any cards |
| 4 | `ValueTrackerManager.UpdateAllTrackers()` returns zero counts | `CombatManager.Me` was not set, causing early `return` in tracker methods | Ensure `CombatManager.Me = cm` **before** calling any tracker update |
| 5 | `codedom` compiler rejects modern C# syntax | The legacy compiler only supports C# 6 | Avoid `$""` interpolation, file-level `using`, and `?.` null-conditional operator |
| 6 | StringSO lives in `DefaultNamespace.SOScripts` | Type resolution fails without full namespace | Use `typeof(DefaultNamespace.SOScripts.StringSO)` or fully-qualified names |

---

## 3. Standard Environment Setup Template

Copy the following block **at the very beginning** of every test script.  
It tears down stale singletons, creates fresh ones, and wires all mandatory references.

```csharp
// ==========================================
// 0. Destroy existing singletons & clear static refs
// ==========================================
if (CombatManager.Me != null)
{
    UnityEngine.Object.DestroyImmediate(CombatManager.Me.gameObject);
}
CombatManager.Me = null;

if (ValueTrackerManager.me != null)
{
    UnityEngine.Object.DestroyImmediate(ValueTrackerManager.me.gameObject);
}
ValueTrackerManager.me = null;

if (GameEventStorage.me != null)
{
    UnityEngine.Object.DestroyImmediate(GameEventStorage.me.gameObject);
}
GameEventStorage.me = null;

if (DefaultNamespace.Managers.DeckTester.me != null)
{
    UnityEngine.Object.DestroyImmediate(DefaultNamespace.Managers.DeckTester.me.gameObject);
}
DefaultNamespace.Managers.DeckTester.me = null;

if (DefaultNamespace.Managers.CardIDRetriever.Me != null)
{
    UnityEngine.Object.DestroyImmediate(DefaultNamespace.Managers.CardIDRetriever.Me.gameObject);
}
DefaultNamespace.Managers.CardIDRetriever.Me = null;

// ==========================================
// 1. Create Player Statuses
// ==========================================
PlayerStatusSO ownerStatus = (PlayerStatusSO)ScriptableObject.CreateInstance(typeof(PlayerStatusSO));
ownerStatus.hp = 100;
ownerStatus.hpMax = 100;
ownerStatus.shield = 0;

PlayerStatusSO enemyStatus = (PlayerStatusSO)ScriptableObject.CreateInstance(typeof(PlayerStatusSO));
enemyStatus.hp = 100;
enemyStatus.hpMax = 100;
enemyStatus.shield = 0;

// ==========================================
// 2. Create & register CombatManager
// ==========================================
GameObject cmObj = new GameObject("TestCombatManager");
CombatManager cm = cmObj.AddComponent<CombatManager>();
CombatManager.Me = cm;                          // MANDATORY: override static singleton
cm.ownerPlayerStatusRef = ownerStatus;
cm.enemyPlayerStatusRef = enemyStatus;
cm.combinedDeckZone = new System.Collections.Generic.List<GameObject>();
cm.revealZone = null;

// ==========================================
// 3. Create & register ValueTrackerManager
// ==========================================
GameObject vtmObj = new GameObject("TestVTM");
ValueTrackerManager vtm = vtmObj.AddComponent<ValueTrackerManager>();
ValueTrackerManager.me = vtm;                   // MANDATORY
cm.ownerCardCountInDeckRef = (IntSO)ScriptableObject.CreateInstance(typeof(IntSO));
cm.enemyCardCountInDeckRef = (IntSO)ScriptableObject.CreateInstance(typeof(IntSO));
vtm.ownerCardCountInDeckRef = cm.ownerCardCountInDeckRef;
vtm.enemyCardCountInDeckRef = cm.enemyCardCountInDeckRef;

// ==========================================
// 4. Create & register CardIDRetriever
// ==========================================
GameObject cirObj = new GameObject("TestCIR");
DefaultNamespace.Managers.CardIDRetriever cir = cirObj.AddComponent<DefaultNamespace.Managers.CardIDRetriever>();
DefaultNamespace.Managers.CardIDRetriever.Me = cir;

// ==========================================
// 5. Create & register GameEventStorage
// ==========================================
GameObject gesObj = new GameObject("TestGES");
GameEventStorage ges = gesObj.AddComponent<GameEventStorage>();
GameEventStorage.me = ges;
// Minimal event set (expand if your card triggers more)
ges.onMyPlayerTookDmg     = (GameEvent)ScriptableObject.CreateInstance(typeof(GameEvent));
ges.onTheirPlayerTookDmg  = (GameEvent)ScriptableObject.CreateInstance(typeof(GameEvent));
ges.onMyPlayerHealed      = (GameEvent)ScriptableObject.CreateInstance(typeof(GameEvent));
ges.onTheirPlayerHealed   = (GameEvent)ScriptableObject.CreateInstance(typeof(GameEvent));
ges.onAnyCardRevealed     = (GameEvent)ScriptableObject.CreateInstance(typeof(GameEvent));
ges.onHostileCardRevealed = (GameEvent)ScriptableObject.CreateInstance(typeof(GameEvent));
ges.afterShuffle          = (GameEvent)ScriptableObject.CreateInstance(typeof(GameEvent));
ges.beforeRoundStart      = (GameEvent)ScriptableObject.CreateInstance(typeof(GameEvent));
ges.onAnyCardGotPower     = (GameEvent)ScriptableObject.CreateInstance(typeof(GameEvent));
ges.onFriendlyCardGotPower= (GameEvent)ScriptableObject.CreateInstance(typeof(GameEvent));
ges.onEnemyCardGotPower   = (GameEvent)ScriptableObject.CreateInstance(typeof(GameEvent));
ges.onMeGotStatusEffect   = (GameEvent)ScriptableObject.CreateInstance(typeof(GameEvent));
ges.onMeGotPower          = (GameEvent)ScriptableObject.CreateInstance(typeof(GameEvent));
ges.onMeRevealed          = (GameEvent)ScriptableObject.CreateInstance(typeof(GameEvent));
ges.onMeStaged            = (GameEvent)ScriptableObject.CreateInstance(typeof(GameEvent));
ges.onThisTagResolverAttached = (GameEvent)ScriptableObject.CreateInstance(typeof(GameEvent));
ges.onMyPlayerShieldUpped = (GameEvent)ScriptableObject.CreateInstance(typeof(GameEvent));
ges.onTheirPlayerShieldUpped = (GameEvent)ScriptableObject.CreateInstance(typeof(GameEvent));

// ==========================================
// 6. Create & register DeckTester
// ==========================================
GameObject dtObj = new GameObject("TestDT");
DefaultNamespace.Managers.DeckTester dt = dtObj.AddComponent<DefaultNamespace.Managers.DeckTester>();
DefaultNamespace.Managers.DeckTester.me = dt;
dt.deckADmgOutputs_ToOpp = new System.Collections.Generic.List<float>();
dt.deckADmgOutputs_ToSelf = new System.Collections.Generic.List<float>();
dt.deckBDmgOutputs_ToOpp = new System.Collections.Generic.List<float>();
dt.deckBDmgOutputs_ToSelf = new System.Collections.Generic.List<float>();

// ==========================================
// 7. Shared StringSO for effect logging
// ==========================================
DefaultNamespace.SOScripts.StringSO effectResultStr =
    (DefaultNamespace.SOScripts.StringSO)ScriptableObject.CreateInstance(typeof(DefaultNamespace.SOScripts.StringSO));
```

---

## 4. Reusable Helper Functions

### 4.1 Create a Generic Card

```csharp
System.Func<bool, GameObject> CreateCard = (System.Func<bool, GameObject>)((isOwner) =>
{
    GameObject card = new GameObject(isOwner ? "FriendlyCard" : "EnemyCard");
    CardScript cs = card.AddComponent<CardScript>();
    cs.myStatusRef = isOwner ? ownerStatus : enemyStatus;
    cs.theirStatusRef = isOwner ? enemyStatus : ownerStatus;
    cs.myStatusEffects = new System.Collections.Generic.List<EnumStorage.StatusEffect>();
    cs.myTags = new System.Collections.Generic.List<EnumStorage.Tag>();
    return card;
});
```

### 4.2 Create an Effect with Reflection Wiring

> **Critical:** Call this **after** the effect GameObject has been parented to a card that already has `CardScript`.

```csharp
System.Func<GameObject, HPAlterEffect> CreateWiredHPAlterEffect =
(System.Func<GameObject, HPAlterEffect>)((parentCard) =>
{
    GameObject effectObj = new GameObject("HPAlterEffect");
    effectObj.transform.SetParent(parentCard.transform);
    HPAlterEffect hae = effectObj.AddComponent<HPAlterEffect>();

    CardScript parentCs = parentCard.GetComponent<CardScript>();

    // Wire protected fields via reflection
    System.Reflection.FieldInfo myCardField =
        typeof(EffectScript).GetField("myCard", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    myCardField.SetValue(hae, parentCard);

    System.Reflection.FieldInfo myCardScriptField =
        typeof(EffectScript).GetField("myCardScript", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    myCardScriptField.SetValue(hae, parentCs);

    System.Reflection.FieldInfo combatManagerField =
        typeof(EffectScript).GetField("combatManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    combatManagerField.SetValue(hae, cm);

    // Common defaults for Strategy A (bypass animation, neutralise baseDmg)
    IntSO baseDmg = (IntSO)ScriptableObject.CreateInstance(typeof(IntSO));
    baseDmg.value = 2;
    hae.baseDmg = baseDmg;
    hae.extraDmg = -2;
    hae.isStatusEffectDamage = true;   // skips AttackAnimationManager
    hae.effectResultString = effectResultStr;

    return hae;
});
```

> **Note:** For other effect types (e.g. `ShieldAlterEffect`, `BuryEffect`), replace `HPAlterEffect` with the target type and adjust the default fields accordingly.

### 4.3 Clean Up Deck Between Test Cases

```csharp
System.Action CleanupDeck = (System.Action)(() =>
{
    foreach (GameObject card in cm.combinedDeckZone)
    {
        if (card != null) UnityEngine.Object.DestroyImmediate(card);
    }
    cm.combinedDeckZone.Clear();
    if (cm.revealZone != null)
    {
        UnityEngine.Object.DestroyImmediate(cm.revealZone);
        cm.revealZone = null;
    }
});
```

### 4.4 Refresh Counts Manually (if `vtm.UpdateAllTrackers()` is flaky)

If you observe `UpdateAllTrackers()` returning stale zeros, use this inline refresh instead.  
It duplicates the exact counting logic from `ValueTrackerManager` without method-call overhead.

```csharp
System.Action RefreshCounts = (System.Action)(() =>
{
    // Owner count
    int ownerCount = 0;
    foreach (GameObject cardObj in cm.combinedDeckZone)
    {
        CardScript cs = cardObj.GetComponent<CardScript>();
        if (cs != null && cs.myStatusRef == cm.ownerPlayerStatusRef) ownerCount++;
    }
    if (cm.revealZone != null)
    {
        CardScript cs = cm.revealZone.GetComponent<CardScript>();
        if (cs != null && cs.myStatusRef == cm.ownerPlayerStatusRef) ownerCount++;
    }
    vtm.ownerCardCountInDeckRef.value = ownerCount;

    // Enemy count
    int enemyCount = 0;
    foreach (GameObject cardObj in cm.combinedDeckZone)
    {
        CardScript cs = cardObj.GetComponent<CardScript>();
        if (cs != null && cs.myStatusRef == cm.enemyPlayerStatusRef) enemyCount++;
    }
    if (cm.revealZone != null)
    {
        CardScript cs = cm.revealZone.GetComponent<CardScript>();
        if (cs != null && cs.myStatusRef == cm.enemyPlayerStatusRef) enemyCount++;
    }
    vtm.enemyCardCountInDeckRef.value = enemyCount;
});
```

---

## 5. Writing a Test Case

### 5.1 Pattern

1. `CleanupDeck()`
2. Reset HP values (`enemyStatus.hp = 100; ownerStatus.hp = 100;`)
3. Populate `cm.combinedDeckZone` with controlled cards
4. Create the card-under-test, wire its effect via `CreateWiredHPAlterEffect`
5. Place the card-under-test into `cm.revealZone` (if simulating a revealed card)
6. Call `RefreshCounts()` (or `vtm.UpdateAllTrackers()`)
7. Record HP before, invoke the effect method, compute delta
8. `Debug.Log` with `[TEST PASS/FAIL]` prefix
9. `DestroyImmediate` the card-under-test

### 5.2 Assertion Convention

```csharp
int hpBefore = enemyStatus.hp;
hae.DecreaseTheirHp_BasedOnFriendlyCardCountInDeck();
int actualDmg = hpBefore - enemyStatus.hp;

string result = (actualDmg == expectedDmg) ? "PASS" : "FAIL";
Debug.Log("[TEST " + result + "] TestCaseID | Expected: " + expectedDmg + ", Actual: " + actualDmg);
```

---

## 6. Full Environment Teardown

Place at the end of the test script to prevent leaked GameObjects from interfering with subsequent runs.

```csharp
CleanupDeck();
UnityEngine.Object.DestroyImmediate(cmObj);
UnityEngine.Object.DestroyImmediate(vtmObj);
UnityEngine.Object.DestroyImmediate(gesObj);
UnityEngine.Object.DestroyImmediate(dtObj);
UnityEngine.Object.DestroyImmediate(cirObj);
```

---

## 7. Complete Working Example (FleshCombination)

The following snippet is a **minimal, copy-pasteable** test for `血肉聚集体`.  
It demonstrates every step above in one contiguous block.

```csharp
// ==========================================
// Environment Setup (see Section 3)
// ==========================================
// ... (paste the full setup block here) ...

// ==========================================
// Helpers (see Section 4)
// ==========================================
// ... (paste CreateCard, CreateWiredHPAlterEffect, CleanupDeck, RefreshCounts here) ...

Debug.Log("===== FleshCombination Strategy A Tests =====");

// ---------- Test A-2: One friendly card in deck + self in revealZone ----------
{
    CleanupDeck();
    enemyStatus.hp = 100;

    GameObject friendlyCard = CreateCard(true);
    cm.combinedDeckZone.Add(friendlyCard);

    GameObject fleshParent = new GameObject("FleshCombination");
    CardScript fleshCs = fleshParent.AddComponent<CardScript>();
    fleshCs.myStatusRef = ownerStatus;
    fleshCs.theirStatusRef = enemyStatus;
    fleshCs.myStatusEffects = new System.Collections.Generic.List<EnumStorage.StatusEffect>();
    fleshCs.myTags = new System.Collections.Generic.List<EnumStorage.Tag>();

    HPAlterEffect hae = CreateWiredHPAlterEffect(fleshParent);
    cm.revealZone = fleshParent;
    RefreshCounts();

    int hpBefore = enemyStatus.hp;
    hae.DecreaseTheirHp_BasedOnFriendlyCardCountInDeck();
    int actualDmg = hpBefore - enemyStatus.hp;

    // revealZone IS counted by current code (2 = 1 deck + 1 self)
    int expected = 2;
    string result = (actualDmg == expected) ? "PASS" : "FAIL";
    Debug.Log("[TEST " + result + "] A-2 | Expected: " + expected + ", Actual: " + actualDmg);

    UnityEngine.Object.DestroyImmediate(fleshParent);
}

// ---------- Test A-4: Power buff ----------
{
    CleanupDeck();
    enemyStatus.hp = 100;

    for (int i = 0; i < 2; i++)
    {
        cm.combinedDeckZone.Add(CreateCard(true));
    }

    GameObject fleshParent = new GameObject("FleshCombination");
    CardScript fleshCs = fleshParent.AddComponent<CardScript>();
    fleshCs.myStatusRef = ownerStatus;
    fleshCs.theirStatusRef = enemyStatus;
    fleshCs.myStatusEffects = new System.Collections.Generic.List<EnumStorage.StatusEffect>();
    fleshCs.myStatusEffects.Add(EnumStorage.StatusEffect.Power);  // +1 dmg
    fleshCs.myTags = new System.Collections.Generic.List<EnumStorage.Tag>();

    HPAlterEffect hae = CreateWiredHPAlterEffect(fleshParent);
    cm.revealZone = fleshParent;
    RefreshCounts();

    int hpBefore = enemyStatus.hp;
    hae.DecreaseTheirHp_BasedOnFriendlyCardCountInDeck();
    int actualDmg = hpBefore - enemyStatus.hp;

    // 2 deck + 1 self in revealZone + 1 Power = 4
    int expected = 4;
    string result = (actualDmg == expected) ? "PASS" : "FAIL";
    Debug.Log("[TEST " + result + "] A-4 | Expected: " + expected + ", Actual: " + actualDmg);

    UnityEngine.Object.DestroyImmediate(fleshParent);
}

// ==========================================
// Teardown (see Section 6)
// ==========================================
CleanupDeck();
UnityEngine.Object.DestroyImmediate(cmObj);
UnityEngine.Object.DestroyImmediate(vtmObj);
UnityEngine.Object.DestroyImmediate(gesObj);
UnityEngine.Object.DestroyImmediate(dtObj);
UnityEngine.Object.DestroyImmediate(cirObj);

Debug.Log("===== Tests Complete =====");
return "Done";
```

---

## 8. Troubleshooting Quick Reference

| Symptom | Likely Cause | Fix |
|---------|--------------|-----|
| `NullReferenceException` inside `DecreaseTheirHp()` | `EffectScript.myCardScript` or `combatManager` is null | Ensure reflection wiring (Section 4.2) is called **after** `SetParent` |
| `NullReferenceException` inside `DmgCalculator()` | `baseDmg` IntSO is null | Assign `hae.baseDmg = (IntSO)ScriptableObject.CreateInstance(typeof(IntSO))` |
| Damage is always zero | `ValueTrackerManager.me` is null or `CombatManager.Me` is null | Manually assign both static singletons (Section 3) |
| `UpdateAllTrackers()` returns 0 counts despite cards in deck | `CombatManager.Me` was not set when VTM method ran | Assign `CombatManager.Me` before creating VTM; use `RefreshCounts()` as fallback |
| Card creation crashes | `CardIDRetriever.Me` is null | Create and assign `CardIDRetriever.Me` before any `AddComponent<CardScript>()` |
| Compilation error: "Unexpected symbol" | Used `$""`, `?.`, or file-level `using` | Switch to string concatenation (`+`), null checks, and fully-qualified type names |
| Assertion fails because revealZone is counted | Document expects old behaviour | **Current code counts revealZone** -- update test expectation, not code |

---

## 9. Checklist Before Running a New Card Test

- [ ] Singleton teardown block (Section 3 step 0) is at the top of the script
- [ ] `CombatManager.Me` is manually assigned immediately after `AddComponent`
- [ ] `ValueTrackerManager.me` is manually assigned
- [ ] `CardIDRetriever.Me` is manually assigned before any card creation
- [ ] Effect GameObject is parented to the card **before** calling reflection wiring
- [ ] `RefreshCounts()` (or `UpdateAllTrackers()`) is called after the deck is built and `revealZone` is set
- [ ] `isStatusEffectDamage = true` is set if you want to skip the animation queue
- [ ] `baseDmg` and `extraDmg` are configured to cancel out (e.g. `2` and `-2`) when testing pure count-based damage
- [ ] Teardown block (Section 6) exists at the end
