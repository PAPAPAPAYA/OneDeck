using System.Collections.Generic;
using System.Reflection;
using DefaultNamespace;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode prefab-level smoke tests for CURSE_SUMMONER and the GRAVE_HEXER friendly-revive
/// residue cleanup (plans/plan-curse-summoner-prefab-2026-09-02.md).
/// Instantiates the REAL prefabs and drives the serialized listener -> CostNEffectContainer ->
/// ReviveEffect chain, so the prefab wiring itself (bindings, typeIDFilter values) is what's tested.
/// Deck layout convention: indices 0..startCardIndex-1 = grave side, then the Start Card, then the live zone.
/// </summary>
public class CurseSummonerPrefabSmokeTests : HeadlessCombatTestFixture
{
	private const string SummonerPath = "Assets/Prefabs/Cards/4.0/1_Uncommon/CURSE_SUMMONER.prefab";
	private const string HexerPath = "Assets/Prefabs/Cards/4.0/1_Uncommon/GRAVE_HEXER.prefab";

	private readonly List<GameObject> _instantiated = new List<GameObject>();

	public override void TearDown()
	{
		foreach (var obj in _instantiated)
		{
			if (obj != null)
				Object.DestroyImmediate(obj);
		}
		_instantiated.Clear();
		base.TearDown();
	}

	/// <summary>
	/// Edit Mode skips persistent UnityEvent calls marked RuntimeOnly (callState=2), which is how
	/// the shipped prefabs bind their chains. Flip callState to EditorAndRuntime (1) on the
	/// INSTANTIATED COPY only — the prefab asset keeps its shipped value — so the real serialized
	/// chain (listener response -> container -> effect) runs under Edit Mode tests.
	/// </summary>
	private static void ForceEditorCallState(UnityEngine.Object owner, string fieldPath)
	{
		var so = new UnityEditor.SerializedObject(owner);
		var calls = so.FindProperty(fieldPath + ".m_PersistentCalls.m_Calls");
		if (calls == null) return;
		for (int i = 0; i < calls.arraySize; i++)
		{
			var state = calls.GetArrayElementAtIndex(i).FindPropertyRelative("m_CallState");
			if (state != null) state.intValue = 1;
		}
		so.ApplyModifiedPropertiesWithoutUndo();
	}

	/// <summary>
	/// Instantiate a real card prefab and inject the runtime-only refs the game wires at spawn time:
	/// EffectScript.myCard/myCardScript/combatManager, CostNEffectContainer._myCardScript, card
	/// status refs. Listeners are re-pointed to the fixture's onMeRevealed instance because Edit
	/// Mode never runs OnEnable (no automatic GameEvent registration).
	/// </summary>
	private GameObject InstantiateCardPrefab(string path)
	{
		var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
		Assert.IsNotNull(prefab, "prefab missing: " + path);
		var card = Object.Instantiate(prefab);
		_instantiated.Add(card);

		var cardScript = card.GetComponent<CardScript>();
		Assert.IsNotNull(cardScript, "root CardScript missing on " + path);
		cardScript.myStatusRef = OwnerStatus;
		cardScript.theirStatusRef = EnemyStatus;
		cardScript.myStatusEffects = new List<EnumStorage.StatusEffect>();
		cardScript.myTags = new List<EnumStorage.Tag>();

		var myCardField = typeof(EffectScript).GetField("myCard", BindingFlags.NonPublic | BindingFlags.Instance);
		var myCardScriptField = typeof(EffectScript).GetField("myCardScript", BindingFlags.NonPublic | BindingFlags.Instance);
		var combatManagerField = typeof(EffectScript).GetField("combatManager", BindingFlags.NonPublic | BindingFlags.Instance);
		foreach (var effect in card.GetComponentsInChildren<EffectScript>(true))
		{
			myCardField.SetValue(effect, card);
			myCardScriptField.SetValue(effect, cardScript);
			combatManagerField.SetValue(effect, CombatManager);
		}

		var containerField = typeof(CostNEffectContainer).GetField("_myCardScript", BindingFlags.NonPublic | BindingFlags.Instance);
		foreach (var container in card.GetComponentsInChildren<CostNEffectContainer>(true))
		{
			containerField.SetValue(container, cardScript);
			ForceEditorCallState(container, "effectEvent");
			ForceEditorCallState(container, "checkCostEvent");
		}

		foreach (var listener in card.GetComponentsInChildren<GameEventListener>(true))
		{
			ForceEditorCallState(listener, "response");
			listener.@event = GameEventStorage.onMeRevealed;
			GameEventStorage.onMeRevealed.RegisterListener(listener);
		}
		return card;
	}

	[Test]
	public void CurseSummoner_OnRevealed_RevivesFriendlyAndEnemyCurse()
	{
		var enemyCurse = CreateCard(false, "EnemyCurse", "JU_ON");
		var friendlyGrave = CreateCard(true, "FriendlyGrave");
		var start = CreateStartCard();
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { enemyCurse, friendlyGrave, start });
		var summoner = InstantiateCardPrefab(SummonerPath);
		CombatManager.combinedDeckZone.Add(summoner);

		Assert.AreSame(summoner, RevealTopCard().gameObject, "summoner sits on deck top before the reveal");
		TriggerRevealedCard();

		Assert.AreEqual(3, CombatManager.combinedDeckZone.Count, "Revive must not change deck size");
		Assert.AreSame(start, CombatManager.combinedDeckZone[0], "Start Card boundary intact");
		Assert.AreSame(friendlyGrave, CombatManager.combinedDeckZone[1], "friendly half revived out of the grave");
		Assert.AreSame(enemyCurse, CombatManager.combinedDeckZone[2], "enemy-curse half (复辟) revived to deck top");
	}

	[Test]
	public void CurseSummoner_EnemyCurseHalf_SkipsNonCurseEnemyGrave()
	{
		var enemyOther = CreateCard(false, "EnemyOther", "NOT_CURSE");
		var enemyCurse = CreateCard(false, "EnemyCurse", "JU_ON");
		var start = CreateStartCard();
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { enemyOther, enemyCurse, start });
		var summoner = InstantiateCardPrefab(SummonerPath);
		CombatManager.combinedDeckZone.Add(summoner);

		RevealTopCard();
		TriggerRevealedCard();

		Assert.AreSame(enemyCurse, CombatManager.combinedDeckZone[CombatManager.combinedDeckZone.Count - 1],
			"typeIDFilter JU_ON must pick only the curse token from the enemy grave");
		Assert.AreSame(enemyOther, CombatManager.combinedDeckZone[0], "non-curse enemy grave card stays buried");
	}

	[Test]
	public void GraveHexer_RevivesFriendlyGrave_AfterResidueCleanup()
	{
		var enemyCurse = CreateCard(false, "EnemyCurse", "JU_ON");
		var friendlyGrave = CreateCard(true, "FriendlyGrave");
		var start = CreateStartCard();
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { enemyCurse, friendlyGrave, start });
		var hexer = InstantiateCardPrefab(HexerPath);
		CombatManager.combinedDeckZone.Add(hexer);

		RevealTopCard();
		TriggerRevealedCard();

		Assert.AreSame(friendlyGrave, CombatManager.combinedDeckZone[CombatManager.combinedDeckZone.Count - 1],
			"friendly revive must pick any friendly grave card (JU_ON residue cleared)");
		Assert.AreSame(enemyCurse, CombatManager.combinedDeckZone[0], "enemy grave untouched by the friendly half");
	}
}
