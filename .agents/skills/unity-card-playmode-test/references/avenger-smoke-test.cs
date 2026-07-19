// AVENGER Strategy B Smoke Test — unity-card-playmode-test
//
// Purpose: minimal end-to-end regression check for the skill's own template.
// Run it after editing the skill or after combat-system refactors.
//
// Usage:
// 1. Enter Play Mode in GameScene, at combat start (or a dedicated test scene).
// 2. Paste this ENTIRE file into execute_code (compiler: "auto" / omitted).
// 3. Collect results via read_console filtered on "[TEST".
//
// Expected output (verified live 2026-07-18):
//   [TEST PASS] AVENGER-1 Reveal Damage | Expected: 3, Actual: 3
//   [TEST PASS] AVENGER-2 Buried Power | Expected: 2, Gained: 2
//   [TEST CLEANUP] State restored (HP/phase/chains)

// ==========================================
// 0. Ensure Play Mode is active
// ==========================================
if (!UnityEditor.EditorApplication.isPlaying)
{
	UnityEngine.Debug.Log("[TEST FAIL] Must be in Play Mode");
	return 1;
}

CombatManager cm = CombatManager.Me;
if (cm == null)
{
	UnityEngine.Debug.Log("[TEST FAIL] CombatManager.Me is null");
	return 1;
}

// ==========================================
// 1. Snapshot live state (restored in finally)
// ==========================================
var prevPhase = cm.currentGamePhaseRef != null ? cm.currentGamePhaseRef.currentGamePhase : EnumStorage.GamePhase.Shop;
int prevOwnerHp = cm.ownerPlayerStatusRef.hp, prevOwnerHpMax = cm.ownerPlayerStatusRef.hpMax, prevOwnerShield = cm.ownerPlayerStatusRef.shield;
int prevEnemyHp = cm.enemyPlayerStatusRef.hp, prevEnemyHpMax = cm.enemyPlayerStatusRef.hpMax, prevEnemyShield = cm.enemyPlayerStatusRef.shield;

// ==========================================
// 2. Helpers
// ==========================================
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

	foreach (HPAlterEffect hae in card.GetComponentsInChildren<HPAlterEffect>(true))
	{
		hae.isStatusEffectDamage = true;
	}

	return card;
};

System.Action CloseChain = () =>
{
	if (EffectChainManager.Me != null)
	{
		EffectChainManager.Me.CloseOpenedChain();
		EffectChainManager.Me.lastEffectObject = null;
	}
};

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

// ==========================================
// 3. Test cases (state reset + snapshot wrapper)
// ==========================================
try
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

	UnityEngine.Debug.Log("===== AVENGER Strategy B Smoke Test =====");
	string prefabPath = "Assets/Prefabs/Cards/3.0 no cost (current)/Bury and buried/DeathRattle/1_Uncommon/AVENGER.prefab";

	// ---------- AVENGER-1: Reveal Damage (event path only) ----------
	{
		GameObject testCard = CreateTestCard(prefabPath, false);
		if (testCard == null) return 1;

		cm.enemyPlayerStatusRef.hp = 100;
		int hpBefore = cm.enemyPlayerStatusRef.hp;

		TriggerRevealEffect(testCard);

		int actualDmg = hpBefore - cm.enemyPlayerStatusRef.hp;
		int expectedDmg = 3; // baseDmg 2 + extraDmg 1
		string result = (actualDmg == expectedDmg) ? "PASS" : "FAIL";
		UnityEngine.Debug.Log("[TEST " + result + "] AVENGER-1 Reveal Damage | Expected: " + expectedDmg + ", Actual: " + actualDmg);

		if (cm.revealZone == testCard) cm.revealZone = null;
		UnityEngine.Object.DestroyImmediate(testCard);
	}

	CloseChain();

	// ---------- AVENGER-2: Buried Power ----------
	{
		GameObject testCard = CreateTestCard(prefabPath, false);
		if (testCard == null) return 1;

		CardScript cs = testCard.GetComponent<CardScript>();
		int powerBefore = EnumStorage.GetStatusEffectCount(cs.myStatusEffects, EnumStorage.StatusEffect.Power);

		TriggerBuryEffect(testCard);

		int powerGained = EnumStorage.GetStatusEffectCount(cs.myStatusEffects, EnumStorage.StatusEffect.Power) - powerBefore;

		// Read the parameter actually bound in the prefab (AVENGER binds 2)
		int expectedPower = 2;
		var giver = testCard.GetComponentInChildren<DefaultNamespace.Effects.StatusEffectGiverEffect>(true);
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
		UnityEngine.Debug.Log("[TEST " + result + "] AVENGER-2 Buried Power | Expected: " + expectedPower + ", Gained: " + powerGained);

		UnityEngine.Object.DestroyImmediate(testCard);
	}

	UnityEngine.Debug.Log("===== Tests Complete =====");
}
finally
{
	cm.ownerPlayerStatusRef.hp = prevOwnerHp; cm.ownerPlayerStatusRef.hpMax = prevOwnerHpMax; cm.ownerPlayerStatusRef.shield = prevOwnerShield;
	cm.enemyPlayerStatusRef.hp = prevEnemyHp; cm.enemyPlayerStatusRef.hpMax = prevEnemyHpMax; cm.enemyPlayerStatusRef.shield = prevEnemyShield;
	if (cm.currentGamePhaseRef != null) cm.currentGamePhaseRef.currentGamePhase = prevPhase;
	CloseChain();
	UnityEngine.Debug.Log("[TEST CLEANUP] State restored (HP/phase/chains)");
}
return 0;
