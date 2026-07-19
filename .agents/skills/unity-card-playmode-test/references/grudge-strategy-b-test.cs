// GRUDGE Strategy B Test — unity-card-playmode-test
//
// Demonstrates the DIRECT invocation path: GRUDGE uses CheckCost_IndexBeforeStartCard,
// which requires the card to be inside combinedDeckZone. The reveal event path would
// move it to revealZone and break the cost, so direct InvokeEffectEvent() is correct
// here — and because listeners are bypassed, there is no double-trigger risk.
// Also demonstrates the async status-effect projectile workaround (reflection on the
// private ApplyStatusEffectToLastXCardSingle).
//
// Usage:
// 1. Enter Play Mode in GameScene, at combat start (or a dedicated test scene).
// 2. Paste this ENTIRE file into execute_code (compiler: "auto" / omitted).
// 3. Collect results via read_console filtered on "[TEST".
//
// Expected output:
//   [TEST PASS] GRUDGE | powerA=2 powerB=2
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

// ==========================================
// 3. Test case (state reset + snapshot wrapper)
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

	UnityEngine.Debug.Log("===== GRUDGE Strategy B Test =====");

	GameObject testCard = CreateTestCard("Assets/Prefabs/Cards/3.0 no cost (current)/Bury and buried/Bury/1_Uncommon/GRUDGE.prefab", false);
	GameObject friendlyA = CreateTestCard("Assets/Prefabs/Cards/3.0 no cost (current)/General/0_Common/BLACKSMITH.prefab", false);
	GameObject friendlyB = CreateTestCard("Assets/Prefabs/Cards/3.0 no cost (current)/General/0_Common/COFFIN_MAKER.prefab", false);
	GameObject startCard = CreateTestCard("Assets/Prefabs/Cards/System/StartCard.prefab", false);

	// Deck layout: FriendlyA(0), FriendlyB(1), GRUDGE(2), StartCard(3)
	// GRUDGE is BEFORE StartCard -> cost passes
	if (friendlyA != null) cm.combinedDeckZone.Add(friendlyA);
	if (friendlyB != null) cm.combinedDeckZone.Add(friendlyB);
	if (testCard != null) cm.combinedDeckZone.Add(testCard);
	if (startCard != null) cm.combinedDeckZone.Add(startCard);

	if (testCard != null)
	{
		var cnts = testCard.GetComponentsInChildren<CostNEffectContainer>(true);
		if (cnts.Length > 0)
		{
			cnts[0].InvokeEffectEvent();
			CloseChain();

			// Async projectile workaround: apply the status effects synchronously
			var giver = cnts[0].GetComponentInChildren<DefaultNamespace.Effects.StatusEffectGiverEffect>(true);
			if (giver != null)
			{
				var method = typeof(DefaultNamespace.Effects.StatusEffectGiverEffect)
					.GetMethod("ApplyStatusEffectToLastXCardSingle",
						System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				if (method != null)
				{
					if (friendlyA != null) method.Invoke(giver, new object[] { friendlyA.GetComponent<CardScript>() });
					if (friendlyB != null) method.Invoke(giver, new object[] { friendlyB.GetComponent<CardScript>() });
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
		}
	}

	if (testCard != null) UnityEngine.Object.DestroyImmediate(testCard);
	if (friendlyA != null) UnityEngine.Object.DestroyImmediate(friendlyA);
	if (friendlyB != null) UnityEngine.Object.DestroyImmediate(friendlyB);
	if (startCard != null) UnityEngine.Object.DestroyImmediate(startCard);

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
