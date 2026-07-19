---
name: unity-card-playmode-test
description: Execute Strategy B (Play Mode Integration Test) for any OneDeck card prefab. Use when the user wants to run a real Play Mode test on a card's effects, verify event bindings, animation behavior, and end-to-end combat flow. Canonical Strategy B reference (absorbs docs/StrategyB_PlayMode_SOP.md).
last_reviewed: 2026-07-18
---

# Unity Card Play Mode Integration Test (Strategy B)

This skill is the **single source of truth** for running **Strategy B — Play Mode Integration Tests** on any combat card in the OneDeck project. It supersedes `docs/StrategyB_PlayMode_SOP.md` (now a redirect stub).

## 1. What is Strategy B?

| Aspect | Description |
|--------|-------------|
| **Purpose** | Validate the full combat flow including event binding, animation queue, console logs, and final game state |
| **Environment** | Unity Play Mode (real scene, real managers, real prefab instances) |
| **Trigger** | `execute_code` in Play Mode |
| **Scope** | End-to-end effect invocation via `GameEvent.RaiseSpecific()` |

### 1.1 Strategy B vs. EditMode NUnit — pick the right tool

The project has `com.unity.test-framework` installed and 20+ EditMode test classes under `Assets/Scripts/Editor/Tests/` built on `HeadlessCombatTestFixture`, which provides a full headless combat environment via `NullCombatVisuals` (HP reset, deck cleanup, chain closing, card creation and wiring). Do **not** hand-write that setup in Play Mode when an NUnit test can cover it.

| Need | Tool |
|------|------|
| Logic / numeric validation (damage formula, cost checks, deck-order math) | EditMode NUnit on `HeadlessCombatTestFixture` — repeatable, CI-able, TearDown-safe (drive via the `run_tests` MCP tool) |
| Prefab serialized-binding audits (which listener invokes which container) | `execute_code` in Edit Mode — see the `unity-read-prefab-serialized` skill |
| `RecorderAnimationPlayer` animation-queue behavior, UX / visual verification, end-to-end event wiring in the real scene | **Strategy B** (this skill) |

## 2. Preconditions & Safety

- **Run at combat start (right after entering Play Mode) or in a dedicated test scene.** The setup template resets both players' HP, clears `combinedDeckZone`, destroys `revealZone`, and forces the Combat phase — running it mid-combat irreversibly corrupts the ongoing session. The template below snapshots and restores state via `try/finally`; keep that wrapper.
- **`TriggerRevealEffect` fires a GLOBAL event.** `TriggerRevealedCardEffect` raises `onAnyCardRevealed` — every linger card in the scene responds, which can pollute test state. Run with a clean scene or account for collateral triggers in assertions.
- **Collect results programmatically.** After a run, call `read_console` filtered on `[TEST` to gather the `[TEST PASS/FAIL]` lines instead of eyeballing the Console.
- **Compiler:** `execute_code` with `compiler: "auto"` resolves to **Roslyn (C# 12+)** — string interpolation, `?.`, `using` declarations, and `try/finally` all work (verified 2026-07-18). `codedom` (C# 6) is a fallback only; if it is ever needed, avoid `$""`, `?.`, file-level `using`, and bare `return;`.

## 3. Core Challenges & Solutions

| # | Challenge | Root Cause | Solution | Last Verified |
|---|-----------|------------|----------|---------------|
| 1 | ~~`EffectScript` protected fields (`myCard`, `myCardScript`, `combatManager`) are `null` after `Instantiate`~~ **(obsolete)** | `EffectScript.OnEnable()` wires all three fields synchronously on `Instantiate` (`Assets/Scripts/Effects/EffectScript.cs`) — verified live 2026-07-18 | No action needed. Reflection wiring is a **fallback only** for non-standard parenting (effect object not a direct child of the card root) | 2026-07-18 (live) |
| 2 | Damage is 0 despite effect triggering | `isStatusEffectDamage=false` causes async `AttackAnimationManager` callback | Set `isStatusEffectDamage = true` on `HPAlterEffect` for synchronous damage execution in tests | 2026-07-18 (live) |
| 3 | ~~`codedom` rejects `yield return`, `$""`, `?.`~~ **(stale)** | Roslyn (C# 12+) is the default `execute_code` compiler since 2026-07-18 | Use modern C# freely; keep C# 6 constraints only as codedom fallback (see Section 2) | 2026-07-18 (live) |
| 4 | `GameEventListener` type not found in `execute_code` | Namespace resolution issues | Use `System.Type.GetType("GameEventListener, Assembly-CSharp")` | 2026-07-18 |
| 5 | `SerializedProperty` name mismatch | Field declared as `public GameEvent @event;` | Use `so.FindProperty("event")`, not `"gameEvent"` | 2026-07-18 (live) |
| 6 | State pollution between tests | `myStatusEffects` list accumulates on reused instances | **Destroy and re-instantiate** the card-under-test for each test case; call `ResetState()` between cards | 2026-07-18 |
| 7 | `EffectChainManager` loop guard blocks effects; second consecutive trigger has no effect | `lastEffectObject` check, chain depth limit, or the effect ID is still in `openedEffectRecorders` | Call `CloseChain()` (`CloseOpenedChain()` + `lastEffectObject = null`) before the first trigger **and between consecutive triggers** | 2026-07-18 (live) |
| 8 | `GamePhaseSO` has no `SetValue()` method | API mismatch | Directly assign `cm.currentGamePhaseRef.currentGamePhase = EnumStorage.GamePhase.Combat;` | 2026-07-18 (live) |
| 9 | **Event path vs. direct container invocation — pick one, never both** | `TriggerRevealedCardEffect` (`CombatManager.cs`) does **not** invoke containers directly; it raises `onAnyCardRevealed` and `onMeRevealed.RaiseSpecific(revealZone)`, firing **every** `GameEventListener` on the card. Containers on one card are typically bound to **different** events (e.g. CURSED_CORPSE: onMeRevealed -> damage, onMeBuried -> `EnhanceCurse`) | Default: use the **event path**. Use direct `InvokeEffectEvent()` **only** when the event path cannot apply (see #10) or listeners must be bypassed deliberately. **Never do both** — the event path already invoked the listeners, so a direct invocation on top double-triggers effects (double damage; spurious bury effects during a reveal test) | 2026-07-18 (live) |
| 10 | **Cost checks that require the card to be inside the deck fail after reveal** (e.g. `CheckCost_IndexBeforeStartCard` on GRUDGE, WISE_BURIAL) | A revealed card is moved from `combinedDeckZone` to `revealZone`; `IndexOf` returns `-1` | Test via **direct `InvokeEffectEvent()`** while the card is still in `combinedDeckZone`, or set up the deck so the card sits before the Start Card | 2026-07-18 |
| 11 | **Linger cards listen to global events** | Linger cards (e.g. `CURSE_ENCHANTMENT`, `MOTH_MAN`) do **not** use `onMeRevealed`. They listen to `onTheirPlayerTookDmg`, `onEnemyCurseCardGotPower`, etc. | Do **not** use `TriggerRevealEffect`. Instead, directly `Raise()` the specific event from `GameEventStorage.me` | 2026-07-18 |
| 12 | **Enemy cards need correct parent & status ref** | Curse tests often require an enemy `JU_ON` in the deck. Instantiating with `playerDeckParent` makes it a friendly card | Call `CreateTestCard(prefabPath, isEnemy: true)` — it parents under `cm.enemyDeckParent` and sets `myStatusRef = cm.enemyPlayerStatusRef` | 2026-07-18 |
| 13 | **Stage effect appears to do nothing** | `StageMyCards` / `StageSelf` excludes cards already at the top of `combinedDeckZone` (`IsCardAtTop` check). If the only eligible card is at index `Count-1`, the filtered list is empty | Add an extra dummy card **after** the target card in `combinedDeckZone` so the target is **not** at the top | 2026-07-18 |
| 14 | **Cost check `EnemyCursedCardHasPower` fails unexpectedly** | This cost requires the enemy curse card's Power to be **strictly greater** than the parameter (e.g. `> 1` for `intArg=1`). A JU_ON with exactly 1 Power will fail the check | Pre-buff the enemy JU_ON with enough Power stacks before the trigger (e.g. 2+ Power for `intArg=1`) | 2026-07-18 |
| 15 | **Multi-Listener cards trigger wrong Container** | Some cards (e.g. `CURSE_THIRST_BEAST`) have **multiple GameEventListener**s on the root object, each bound to a **different CostNEffectContainer** via `InvokeEffectEvent`. `OnMeRevealed` may trigger the "deal dmg" Container while `OnHostileCurseRevealed` triggers the "stage self" Container | Inspect the prefab's `GameEventListener` response targets (via `SerializedProperty`) to know which Listener maps to which Container. Do not assume all Containers share the same trigger event | 2026-07-18 |
| 16 | **`PlayRecorderAnimationsAndWait` not started in direct tests** | `CombatManager.RevealCards()` starts the coroutine automatically, but direct `InvokeEffectEvent()` or `TriggerRevealEffect` bypasses it | To test the full animation flow, call `StartAnimationPhase()` (Section 5.4) after your effect trigger; or use `TriggerRevealEffect` inside a real reveal cycle by setting `cm.awaitingRevealConfirm = false` first | 2026-07-18 |
| 17 | **Animation request capture not visible in single-frame tests** | `HPAlterEffect` now captures `AnimationRequest` on the recorder instead of immediately raising `onDamageDealt` | Damage is still resolved synchronously (good for tests), but the `AnimationRequest` is only visible on `EffectRecorder.animationRequests`; inspect `EffectChainManager.Me.closedEffectRecorders` after calling `CloseChain()` | 2026-07-18 |
| 18 | **Status effects not applied by giver methods in single-frame tests** | `GiveStatusEffectToLastXCards` / `GiveAllFriendlyStatusEffect` use `CombatUXManager.PlayMultiStatusEffectProjectile()` which is **async** | For synchronous verification, directly call the private `ApplyStatusEffectToXxxSingle()` method via reflection after the event trigger (Section 8) | 2026-07-18 |

> Rows not re-verified within the last 3 months are suspect — re-verify before relying on them and update the date (process: see the `unity-skill-review` skill). `(live)` = confirmed by an actual Play Mode run on that date; others verified statically against source/prefabs.

## 4. Standard Play Mode Setup Template

Copy this block at the beginning of every Strategy B test script. **Precondition:** run at combat start or in a dedicated test scene — the reset is destructive to an ongoing combat.

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
// 2. Snapshot live state (restored in finally)
// ==========================================
var prevPhase = cm.currentGamePhaseRef != null ? cm.currentGamePhaseRef.currentGamePhase : EnumStorage.GamePhase.Shop;
int prevOwnerHp = cm.ownerPlayerStatusRef.hp, prevOwnerHpMax = cm.ownerPlayerStatusRef.hpMax, prevOwnerShield = cm.ownerPlayerStatusRef.shield;
int prevEnemyHp = cm.enemyPlayerStatusRef.hp, prevEnemyHpMax = cm.enemyPlayerStatusRef.hpMax, prevEnemyShield = cm.enemyPlayerStatusRef.shield;

try
{
	// ==========================================
	// 3. Reset test state
	// ==========================================
	cm.ownerPlayerStatusRef.hp = 100; cm.ownerPlayerStatusRef.hpMax = 100; cm.ownerPlayerStatusRef.shield = 0;
	cm.enemyPlayerStatusRef.hp = 100; cm.enemyPlayerStatusRef.hpMax = 100; cm.enemyPlayerStatusRef.shield = 0;
	cm.combinedDeckZone.Clear();
	if (cm.revealZone != null)
	{
		UnityEngine.Object.DestroyImmediate(cm.revealZone);
		cm.revealZone = null;
	}
	if (cm.currentGamePhaseRef != null)
	{
		cm.currentGamePhaseRef.currentGamePhase = EnumStorage.GamePhase.Combat;
	}
	if (EffectChainManager.Me != null)
	{
		EffectChainManager.Me.CloseOpenedChain();
		EffectChainManager.Me.lastEffectObject = null;
	}

	// ... test cases go here ...
}
finally
{
	// ==========================================
	// 4. Restore snapshot
	// ==========================================
	cm.ownerPlayerStatusRef.hp = prevOwnerHp; cm.ownerPlayerStatusRef.hpMax = prevOwnerHpMax; cm.ownerPlayerStatusRef.shield = prevOwnerShield;
	cm.enemyPlayerStatusRef.hp = prevEnemyHp; cm.enemyPlayerStatusRef.hpMax = prevEnemyHpMax; cm.enemyPlayerStatusRef.shield = prevEnemyShield;
	if (cm.currentGamePhaseRef != null) cm.currentGamePhaseRef.currentGamePhase = prevPhase;
	if (EffectChainManager.Me != null)
	{
		EffectChainManager.Me.CloseOpenedChain();
		EffectChainManager.Me.lastEffectObject = null;
	}
	UnityEngine.Debug.Log("[TEST CLEANUP] State restored (HP/phase/chains)");
}
```

## 5. Reusable Helpers

### 5.1 CreateTestCard(prefabPath, isEnemy) — one parameterized helper

Covers both factions (the old `CreateTestCard` / `CreateEnemyCard` pair is merged). `EffectScript` fields are auto-wired by `OnEnable()`; the reflection block below is only a fallback for non-standard parenting.

```csharp
System.Func<string, bool, GameObject> CreateTestCard = (prefabPath, isEnemy) =>
{
	GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
	if (prefab == null)
	{
		UnityEngine.Debug.Log("[TEST FAIL] Prefab not found: " + prefabPath);
		return null;
	}

	Transform parent = isEnemy
		? (cm.enemyDeckParent != null ? cm.enemyDeckParent.transform : null)
		: (cm.playerDeckParent != null ? cm.playerDeckParent.transform : null);
	GameObject card = UnityEngine.Object.Instantiate(prefab, parent);
	card.name = prefab.name;
	CardScript cs = card.GetComponent<CardScript>();
	cs.myStatusRef = isEnemy ? cm.enemyPlayerStatusRef : cm.ownerPlayerStatusRef;
	cs.theirStatusRef = isEnemy ? cm.ownerPlayerStatusRef : cm.enemyPlayerStatusRef;
	cs.myStatusEffects = new System.Collections.Generic.List<EnumStorage.StatusEffect>();
	cs.myTags = new System.Collections.Generic.List<EnumStorage.Tag>();

	// FALLBACK only: OnEnable() wires myCard/myCardScript/combatManager on Instantiate.
	foreach (EffectScript effect in card.GetComponentsInChildren<EffectScript>(true))
	{
		var myCardField = typeof(EffectScript).GetField("myCard", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		if (myCardField != null && myCardField.GetValue(effect) == null)
		{
			myCardField.SetValue(effect, card);
			typeof(EffectScript).GetField("myCardScript", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(effect, cs);
			typeof(EffectScript).GetField("combatManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(effect, cm);
		}
	}

	// Synchronous damage mode for tests
	foreach (HPAlterEffect hae in card.GetComponentsInChildren<HPAlterEffect>(true))
	{
		hae.isStatusEffectDamage = true;
	}

	return card;
};
```

### 5.2 Trigger Helpers

> **Warning:** `TriggerRevealEffect` raises the **global** `onAnyCardRevealed` event — every linger card in the scene responds. See Section 2.

```csharp
System.Action<GameObject> TriggerRevealEffect = (card) =>
{
	cm.revealZone = card;
	var triggerMethod = typeof(CombatManager).GetMethod("TriggerRevealedCardEffect", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
	if (triggerMethod != null)
	{
		triggerMethod.Invoke(cm, null);
	}
	else
	{
		UnityEngine.Debug.Log("[TEST WARN] TriggerRevealedCardEffect not found");
	}
};

System.Action<GameObject> TriggerBuryEffect = (card) =>
{
	if (GameEventStorage.me != null && GameEventStorage.me.onMeBuried != null)
	{
		GameEventStorage.me.onMeBuried.RaiseSpecific(card);
	}
};

System.Action<GameObject> TriggerStageEffect = (card) =>
{
	if (GameEventStorage.me != null && GameEventStorage.me.onMeStaged != null)
	{
		GameEventStorage.me.onMeStaged.RaiseSpecific(card);
	}
};

System.Action TriggerFriendlyCardExiled = () =>
{
	if (GameEventStorage.me != null && GameEventStorage.me.onFriendlyCardExiled != null)
	{
		GameEventStorage.me.onFriendlyCardExiled.RaiseOwner();
	}
};
```

### 5.3 CloseChain

> **Critical:** Call this after **every** effect trigger to prevent the chain guard from blocking subsequent triggers.

```csharp
System.Action CloseChain = () =>
{
	if (EffectChainManager.Me != null)
	{
		EffectChainManager.Me.CloseOpenedChain();
		EffectChainManager.Me.lastEffectObject = null;
	}
};
```

### 5.4 StartAnimationPhase (for direct trigger tests)

`CombatManager.RevealCards()` automatically starts `PlayRecorderAnimationsAndWait()` after `TriggerRevealedCardEffect()`. Direct triggers in tests do not — start it manually when verifying animation capture or playback.

```csharp
System.Action StartAnimationPhase = () =>
{
	var playMethod = typeof(CombatManager).GetMethod("PlayRecorderAnimationsAndWait", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
	if (playMethod != null)
	{
		cm.StartCoroutine((System.Collections.IEnumerator)playMethod.Invoke(cm, null));
	}
	else
	{
		UnityEngine.Debug.Log("[TEST WARN] PlayRecorderAnimationsAndWait not found");
	}
};
```

### 5.5 ResetState (between test cases)

```csharp
System.Action ResetState = () =>
{
	cm.ownerPlayerStatusRef.hp = 100; cm.ownerPlayerStatusRef.hpMax = 100; cm.ownerPlayerStatusRef.shield = 0;
	cm.enemyPlayerStatusRef.hp = 100; cm.enemyPlayerStatusRef.hpMax = 100; cm.enemyPlayerStatusRef.shield = 0;
	cm.combinedDeckZone.Clear();
	if (cm.revealZone != null)
	{
		UnityEngine.Object.DestroyImmediate(cm.revealZone);
		cm.revealZone = null;
	}
	if (cm.currentGamePhaseRef != null)
	{
		cm.currentGamePhaseRef.currentGamePhase = EnumStorage.GamePhase.Combat;
	}
	CloseChain();
};
```

## 6. Writing a Test Case

### 6.1 Pattern

1. Run the **Setup Template** (Section 4), keeping the `try/finally` snapshot wrapper.
2. Create a fresh card instance via `CreateTestCard(prefabPath, isEnemy)`.
3. Build a controlled `combinedDeckZone` (if needed).
4. Record pre-state (HP, status effects, deck counts).
5. Invoke the effect:
	- For normal `onMeRevealed` cards: `TriggerRevealEffect(testCard)` (event path — fires **all** listeners on the card; never add direct `InvokeEffectEvent()` on top).
	- For cost checks that require the card in deck (e.g. `CheckCost_IndexBeforeStartCard`): direct `InvokeEffectEvent()` while the card is in `combinedDeckZone`.
	- For **Linger** cards: directly raise the event (e.g. `GameEventStorage.me.onTheirPlayerTookDmg.RaiseOwner()`).
6. **Immediately call `CloseChain()`** (and between any consecutive triggers).
7. *(Optional)* For **animation request capture**, inspect `EffectChainManager.Me.closedEffectRecorders` for `AnimationRequest` counts and types.
8. *(Optional)* For the **full animation phase**, call `StartAnimationPhase()` and yield frames until `AnimationStateTracker.me.HasActiveBatch == false`.
9. For async status-effect projectiles, additionally call the private `ApplyXxxSingle` method via reflection (Section 8).
10. Record post-state and compute delta.
11. `Debug.Log` with `[TEST PASS/FAIL]` prefix.
12. `DestroyImmediate` the test card.
13. After the run, collect results via `read_console` filtered on `[TEST`.

### 6.2 Assertion Convention

```csharp
int hpBefore = cm.enemyPlayerStatusRef.hp;
TriggerRevealEffect(testCard);
CloseChain();
int actualDmg = hpBefore - cm.enemyPlayerStatusRef.hp;

string result = (actualDmg == expectedDmg) ? "PASS" : "FAIL";
UnityEngine.Debug.Log("[TEST " + result + "] CardID-1 | Expected: " + expectedDmg + ", Actual: " + actualDmg);
```

## 7. Quick Smoke Test (AVENGER)

A self-contained, paste-ready script that doubles as (a) the full working example and (b) a ~30-second regression check for this skill's own template. **Run it after editing this skill or after combat-system refactors.**

- **Script:** [references/avenger-smoke-test.cs](references/avenger-smoke-test.cs) — paste the entire file into `execute_code` (Play Mode, at combat start).
- **What it covers:** setup with snapshot/restore, the merged `CreateTestCard` helper, event-path reveal trigger (AVENGER-1, expects 3 damage), bury trigger with prefab-bound parameter readback (AVENGER-2, expects 2 Power).
- **Expected output** (verified live 2026-07-18, zero console errors):

~~~
[TEST PASS] AVENGER-1 Reveal Damage | Expected: 3, Actual: 3
[TEST PASS] AVENGER-2 Buried Power | Expected: 2, Gained: 2
[TEST CLEANUP] State restored (HP/phase/chains)
~~~

If the smoke test fails after a combat-system change, treat this skill's templates as suspect until the failing row is re-verified (process: see the `unity-skill-review` skill).

## 8. Handling Async Status-Effect Projectiles

Some effects (`StatusEffectGiverEffect.GiveStatusEffectToLastXCards`, `GiveAllFriendlyStatusEffect`) use `CombatUXManager.PlayMultiStatusEffectProjectile()` which applies the status effect asynchronously via animation callbacks. In a single-frame test you cannot wait for the animation, so directly invoke the underlying application method:

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

## 9. Second Example (GRUDGE — cost-check card, direct invocation)

GRUDGE uses `CheckCost_IndexBeforeStartCard`, which requires the card to be **inside** `combinedDeckZone` (Challenge #10). The reveal event path would move it to `revealZone` and break the cost, so **direct `InvokeEffectEvent()` is the correct path here** — and because listeners are bypassed, there is no double-trigger risk. The example also demonstrates the async projectile workaround (Section 8) and a controlled 4-card `combinedDeckZone` setup.

- **Script:** [references/grudge-strategy-b-test.cs](references/grudge-strategy-b-test.cs) — paste the entire file into `execute_code` (Play Mode, at combat start).
- **Expected output:** `[TEST PASS] GRUDGE | powerA=2 powerB=2`, followed by the `[TEST CLEANUP]` line.

## 10. Per-Effect-Type Adaptation

| Effect Type | Extra Setup Required |
|-------------|----------------------|
| **HPAlterEffect** | Set `isStatusEffectDamage = true` for synchronous execution |
| **ShieldAlterEffect** | Check `shield` value on target `PlayerStatusSO` |
| **BuryEffect / StageEffect** | Populate `cm.combinedDeckZone` with target cards before invoking; verify deck order after |
| **StatusEffectGiverEffect** | Read `m_IntArgument` from `SerializedObject` to know the actual `amount` bound in the prefab |
| **CurseEffect** | Ensure enemy deck contains a curse card; verify `myStatusEffects` on the curse target. **Note**: `CurseEffect.EnhanceCurse` uses `PlayMultiStatusEffectProjectile` (async DOTween). Final Power state may not be observable in a single-frame test. Verify via reflection that `FindEnemyCardWithTypeID` succeeds and no exceptions are thrown. Also check `CurseEffect.cardTypeID` references a `StringSO` with `reset=false` (otherwise `value` becomes empty in Play Mode). **Use `CreateTestCard(path, isEnemy: true)` for hostile JU_ON.** |
| **CurseEffect.ConsumeHostileCursePower** | This method searches all enemy JU_ONs and consumes Power layer by layer. If total Power < amount, it returns silently. The UnityEvent caller (e.g. `CostNEffectContainer`) continues to the next bound method. |
| **StageEffect** | Populate `cm.combinedDeckZone` with target cards before invoking; verify deck order after. **Critical**: `StageMyCards` / `StageSelf` excludes cards already at the top (index = Count-1). Add a dummy card above the target to prevent exclusion. |
| **Multi-Listener Cards** | Cards like `CURSE_THIRST_BEAST` have multiple `GameEventListener`s each targeting a different `CostNEffectContainer`. Use `SerializedObject` on the listener's `response` property to inspect which Container each listener invokes. |
| **MinionCostEffect** | Populate deck with matching `isMinion=true` cards before the reveal trigger |
| **AddTempCard** | Check `cm.combinedDeckZone.Count` before and after |

## 11. Troubleshooting Quick Reference

| Symptom | Likely Cause | Fix |
|---------|--------------|-----|
| `NullReferenceException` in effect method | Non-standard parenting prevented `OnEnable()` from wiring `EffectScript` fields | The fallback reflection block in `CreateTestCard` wires any unwired fields; verify the effect objects are children of the card root |
| Damage is always 0 | `isStatusEffectDamage=false` + animation not completing | Set `hae.isStatusEffectDamage = true` |
| `EffectCanBeInvoked` returns false | `EffectChainManager` loop guard active | Call `CloseChain()` before the trigger |
| Second consecutive trigger has no effect | Same effect ID still in `openedEffectRecorders` | Call `CloseChain()` **between** triggers |
| Power/status count wrong between tests | Same card instance reused | `DestroyImmediate` old card and `Instantiate` fresh one |
| `GameEventListener` event is null | Wrong `SerializedProperty` name | Use `"event"`, not `"gameEvent"` |
| Compilation error: "Unexpected symbol" | Only possible on the `codedom` fallback (Roslyn default accepts modern C#) | Omit `compiler` or pass `compiler: "auto"`; on codedom use string concatenation and fully-qualified names |
| `CheckCost_IndexBeforeStartCard` fails on reveal | Card was moved from `combinedDeckZone` to `revealZone` | Test cost via direct `InvokeEffectEvent()` while the card is still in deck (Challenge #10) |
| `CurseEffect` / `StringSO` config null in Play Mode | `StringSO.reset=true` clears `value` on `OnEnable` | Set `reset=false` on persistent config SOs (e.g., `CurseCardTypeID.asset`) |
| `onFriendlyCardExiled` has no listeners after creating card | `RaiseSpecific(card)` only triggers listeners on the target GameObject | Use `RaiseOwner()` for faction-scoped events like `onFriendlyCardExiled` |
| Status effect not applied after giver trigger | `PlayMultiStatusEffectProjectile` is async, or `CombatUXManager.me` is null | Directly invoke `ApplyStatusEffectToXxxSingle` via reflection (Section 8) |
| **Effects fired twice (e.g. double damage)** | Event path and direct `InvokeEffectEvent()` used together | Pick ONE invocation path (Challenge #9). The event path already fires every listener on the card |
| **Wrong container fired on multi-container cards** | Assumed all containers share one trigger event; they are bound to different events via different listeners | Inspect listener bindings first (Challenge #15); trigger the event that maps to the intended container |
| **Linger card has no effect in test** | Using `TriggerRevealEffect` on a card that listens to `onTheirPlayerTookDmg` / `onEnemyCurseCardGotPower` | Raise the correct event directly from `GameEventStorage.me` instead of `TriggerRevealEffect` |
| **Curse tests fail because enemy JU_ON is treated as friendly** | Card created with `isEnemy: false` (friendly faction refs) | Use `CreateTestCard(path, isEnemy: true)` |
| **Stage effect has no effect** | Target card is already at top of `combinedDeckZone` | Add a dummy card on top so the target is not at index `Count-1` |
| **Cost `EnemyCursedCardHasPower` fails** | Enemy JU_ON Power is not strictly greater than the parameter | Give enemy JU_ON enough Power stacks before triggering |
| **`onDamageDealt` event not raised after damage effect** | `RecorderAnimationPlayer` is active and `HPAlterEffect` captured an `AnimationRequest` instead | Expected in the new system. Damage resolves synchronously; the event is deferred to the animation phase. Check `EffectRecorder.animationRequests` on `EffectChainManager.Me.closedEffectRecorders` |
| **`PlayRecorderAnimationsAndWait` coroutine never completes** | `AnimationStateTracker.HasActiveBatch` is stuck true, or `ICombatVisuals` callback was never invoked | Check that `CombatUXManager` is not null and that `MoveCardToBottom` / `PlayAttackAnimation` callbacks fire. In headless tests, the coroutine may hang if visuals are missing; use fallback path or mock visuals |

## 12. Checklist Before Running

- [ ] Unity is in **Play Mode**, at combat start or in a dedicated test scene
- [ ] `CombatManager.Me` is not null
- [ ] `GameEventStorage.me` is initialized
- [ ] Setup template includes the **snapshot / `try/finally` restore** wrapper
- [ ] Setup block clears old `revealZone` and `combinedDeckZone`
- [ ] `CloseChain()` called before the first trigger and between consecutive triggers
- [ ] Card-under-test is **freshly instantiated** (not reused)
- [ ] `EffectScript` fields verified auto-wired by `OnEnable()` (reflection fallback only for non-standard parenting)
- [ ] `HPAlterEffect.isStatusEffectDamage = true` if testing damage synchronously
- [ ] **Invocation path chosen**: event path (default) XOR direct `InvokeEffectEvent()` — never both
- [ ] **Multi-container cards**: listener -> container bindings inspected before choosing the trigger
- [ ] **Linger cards**: verify the listened event (e.g. `onTheirPlayerTookDmg`) and raise the correct event in test
- [ ] **Curse / enemy cards**: use `CreateTestCard(path, isEnemy: true)` for hostile targets
- [ ] Old test card is `DestroyImmediate`-ed at the end
- [ ] **Animation tests**: `CloseChain()` after trigger, then inspect `closedEffectRecorders` for captured `AnimationRequest`s
- [ ] **Coroutine tests**: `StartAnimationPhase()` called; yield enough frames for `AnimationStateTracker` to go idle
- [ ] Results collected via `read_console` filtered on `[TEST`

## 13. Related

- **Recorded deck artifacts** (`Assets/SORefs/Decks/Recorded/**`): commit guidance lives with the deck-recording workflow — see the `check-default-enemy-deck-pool` skill.
- **Prefab serialized-binding audits** (Edit Mode, no Play Mode needed): `unity-read-prefab-serialized` skill.
- **Headless logic tests**: `Assets/Scripts/Editor/Tests/` (`HeadlessCombatTestFixture` + `NullCombatVisuals`), driven via the `run_tests` MCP tool.
