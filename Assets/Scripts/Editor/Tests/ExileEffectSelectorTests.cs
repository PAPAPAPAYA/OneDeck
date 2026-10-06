using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DefaultNamespace;
using DefaultNamespace.SOScripts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// EditMode tests for the ExileEffect selector path (plan Step 4). Parity locks under test
/// (appendix A.2): legacy Exile reaches the WHOLE deck including the grave side, has NO
/// minion filter on main entries, NO self exclusion, and only the main/tag entries exclude
/// passives (Minion/WithTypeID entries do not). Real-prefab flip: RIFT_MONSTER
/// (ExileMyCardsWithTypeID, cardTypeIDSO=RIFT).
/// </summary>
public class ExileEffectSelectorTests : HeadlessCombatTestFixture
{
	private readonly List<GameObject> _instantiated = new List<GameObject>();
	private Scene _prefabScene;

	[OneTimeSetUp]
	public void CreatePrefabScene()
	{
		_prefabScene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
	}

	[OneTimeTearDown]
	public void DestroyPrefabScene()
	{
		foreach (var go in _instantiated)
		{
			if (go != null) Object.DestroyImmediate(go);
		}
		_instantiated.Clear();
		if (_prefabScene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(_prefabScene);
	}

	private ExileEffect InstantiatePrefabExiler(string path, out GameObject cardGO)
	{
		var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
		Assert.IsNotNull(prefab, "prefab missing: " + path);
		var clone = Object.Instantiate(prefab);
		SceneManager.MoveGameObjectToScene(clone, _prefabScene);
		_instantiated.Add(clone);
		cardGO = clone;

		var cardScript = clone.GetComponent<CardScript>();
		Assert.IsNotNull(cardScript, "root CardScript missing on " + path);
		cardScript.myStatusRef = OwnerStatus;
		cardScript.theirStatusRef = EnemyStatus;
		if (cardScript.myStatusEffects == null) cardScript.myStatusEffects = new List<EnumStorage.StatusEffect>();
		if (cardScript.myTags == null) cardScript.myTags = new List<EnumStorage.Tag>();

		var effect = clone.GetComponentInChildren<ExileEffect>(true);
		Assert.IsNotNull(effect, "ExileEffect not found on " + path);
		typeof(EffectScript).GetField("myCard", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(effect, clone);
		typeof(EffectScript).GetField("myCardScript", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(effect, cardScript);
		typeof(EffectScript).GetField("combatManager", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(effect, CombatManager);
		return effect;
	}

	private ExileEffect CreateExilerOn(GameObject sourceCard, bool useSelector)
	{
		var effect = CreateEffect<ExileEffect>(sourceCard);
		effect.useTargetSelector = useSelector;
		return effect;
	}

	private GameObject FriendlyCard(string name, string typeID = null)
	{
		return CreateCard(true, name, typeID);
	}

	[Test]
	public void ExileSelector_MyCards_ParityShape()
	{
		var graveF = FriendlyCard("GraveF");
		var start = CreateStartCard();
		var liveMinion = CreateMinion(true, "LiveMinion");
		var livePassive = CreateCard(true, "LivePassive"); livePassive.GetComponent<CardScript>().isPassive = true;
		var liveNormal = FriendlyCard("LiveNormal");
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { graveF, start, liveMinion, livePassive, liveNormal });
		var source = FriendlyCard("Source");
		var effect = CreateExilerOn(source, useSelector: true);

		effect.ExileMyCards(3);

		var remaining = CombatManager.combinedDeckZone.Select(c => c.name).ToList();
		CollectionAssert.AreEquivalent(new[] { "StartCard", "LivePassive" }, remaining,
			"exiled = grave-side friendly + minion + normal (zone Anywhere, minionMode Any); remaining = Start Card + passive");
	}

	[Test]
	public void ExileSelector_TheirCards_EnemyOnly()
	{
		var start = CreateStartCard();
		var f1 = FriendlyCard("F1");
		var e1 = CreateCard(false, "E1");
		var e2 = CreateCard(false, "E2");
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { start, f1, e1, e2 });
		var source = FriendlyCard("Source");
		var effect = CreateExilerOn(source, useSelector: true);

		effect.ExileTheirCards(2);

		CollectionAssert.AreEquivalent(new[] { "StartCard", "F1" }, CombatManager.combinedDeckZone.Select(c => c.name).ToList(),
			"both enemy cards exiled, friendly untouched");
	}

	[Test]
	public void ExileSelector_RandomCards_BothSides()
	{
		var start = CreateStartCard();
		var f1 = FriendlyCard("F1");
		var e1 = CreateCard(false, "E1");
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { start, f1, e1 });
		var source = FriendlyCard("Source");
		var effect = CreateExilerOn(source, useSelector: true);

		effect.ExileRandomCards(2);

		var remaining = CombatManager.combinedDeckZone.Select(c => c.name).ToList();
		CollectionAssert.AreEquivalent(new[] { "StartCard" }, remaining,
			"Both-side pool = both side cards; amount 2 exiles both, the neutral Start Card stays");
	}

	[Test]
	public void ExileSelector_MinionEntries_OnlyAndPassiveMinionIncluded()
	{
		var start = CreateStartCard();
		var passiveMinion = CreateMinion(true, "PassiveMinion"); passiveMinion.GetComponent<CardScript>().isPassive = true;
		var normalMinion = CreateMinion(true, "NormalMinion");
		var notMinion = FriendlyCard("NotMinion");
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { start, passiveMinion, normalMinion, notMinion });
		var source = FriendlyCard("Source");
		var effect = CreateExilerOn(source, useSelector: true);

		effect.ExileMyMinions(2);

		var remaining = CombatManager.combinedDeckZone.Select(c => c.name).ToList();
		Assert.AreEqual(2, remaining.Count);
		Assert.That(remaining, Does.Contain("NotMinion"), "non-minion untouched (minionMode Only)");
		Assert.That(remaining, Does.Contain("StartCard"), "Start Card untouched (neutral)");
		// Both minions gone — including the PASSIVE one: legacy Minion entries never excluded
		// passives (excludePassive=false parity).
	}

	[Test]
	public void ExileSelector_WithTag_ReadsSpec_MainEntryIgnoresIt()
	{
		var start = CreateStartCard();
		var tagged = FriendlyCard("Tagged"); tagged.GetComponent<CardScript>().myTags.Add(EnumStorage.Tag.DeathRattle);
		var untagged = FriendlyCard("Untagged");
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { start, tagged, untagged });
		var source = FriendlyCard("Source");
		var effect = CreateExilerOn(source, useSelector: true);
		effect.targetSelector.tagFilter = new List<EnumStorage.Tag> { EnumStorage.Tag.DeathRattle };

		effect.ExileMyCardsWithTag(1);
		Assert.AreEqual(2, CombatManager.combinedDeckZone.Count, "tagged card exiled via spec tagFilter");

		CombatManager.combinedDeckZone = new List<GameObject> { start, tagged, untagged };
		effect.ExileMyCards(2);
		CollectionAssert.AreEquivalent(new[] { "StartCard" }, CombatManager.combinedDeckZone.Select(c => c.name).ToList(),
			"main entry must ignore the serialized tagFilter (both non-neutral cards exiled)");
	}

	[Test]
	public void ExileSelector_WithTypeID_FromSpec()
	{
		var start = CreateStartCard();
		var rift = FriendlyCard("Rift", "RIFT");
		var other = FriendlyCard("Other", "OTHER");
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { start, rift, other });
		var source = FriendlyCard("Source");
		var effect = CreateExilerOn(source, useSelector: true);
		effect.targetSelector.typeIDFilter = "RIFT";

		effect.ExileMyCardsWithTypeID(1);

		CollectionAssert.AreEquivalent(new[] { "StartCard", "Other" }, CombatManager.combinedDeckZone.Select(c => c.name).ToList(),
			"only the RIFT card exiled");
	}

	[Test]
	public void ExileLegacy_IgnoresSelectorFields()
	{
		var start = CreateStartCard();
		var rift = FriendlyCard("Rift", "RIFT");
		var other = FriendlyCard("Other", "OTHER");
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { start, rift, other });
		var source = FriendlyCard("Source");
		var effect = CreateExilerOn(source, useSelector: false);
		var riftSO = CreateScriptableObject<StringSO>();
		riftSO.value = "RIFT";
		effect.cardTypeIDSO = riftSO; // wire the legacy StringSO (a fresh component has none)
		effect.targetSelector.typeIDFilter = "OTHER"; // hostile config must be dead
		effect.targetSelector.side = CardSelector.SelectorSide.Enemy;

		effect.ExileMyCardsWithTypeID(1);

		CollectionAssert.AreEquivalent(new[] { "StartCard", "Other" }, CombatManager.combinedDeckZone.Select(c => c.name).ToList(),
			"legacy path reads the StringSO (RIFT), not the serialized spec");
	}

	// ---------- Real-prefab flip ----------

	[Test]
	public void PrefabFlip_RIFT_MONSTER_ExileMyCardsWithTypeID()
	{
		GameObject cardGO;
		var effect = InstantiatePrefabExiler("Assets/Prefabs/Cards/3.0 no cost (current)/Conjure/1_Uncommon/RIFT_MONSTER.prefab", out cardGO);
		Assert.IsNotNull(effect.cardTypeIDSO, "prefab config: cardTypeIDSO wired");

		var start = CreateStartCard();
		var rift = FriendlyCard("Rift", "RIFT");
		var other = FriendlyCard("Other", "OTHER");

		// Round 1: legacy (StringSO = RIFT).
		CombatManager.combinedDeckZone = new List<GameObject> { start, rift, other };
		effect.ExileMyCardsWithTypeID(1);
		CollectionAssert.AreEquivalent(new[] { "StartCard", "Other" }, CombatManager.combinedDeckZone.Select(c => c.name).ToList(),
			"legacy: the RIFT card is exiled");

		// Round 2: flip with the mirrored spec (cardTypeIDSO.value → typeIDFilter, §3.3 mapping).
		CombatManager.combinedDeckZone = new List<GameObject> { start, rift, other };
		effect.useTargetSelector = true;
		var spec = new CardSelector();
		spec.zone = CardSelector.SelectorZone.Anywhere;
		spec.excludeSelf = false;
		spec.minionMode = CardSelector.SelectorMinion.Any;
		spec.excludePassive = false; // WithTypeID legacy parity
		spec.side = CardSelector.SelectorSide.Friendly;
		spec.typeIDFilter = effect.cardTypeIDSO.value;
		effect.targetSelector = spec;
		effect.ExileMyCardsWithTypeID(1);
		CollectionAssert.AreEquivalent(new[] { "StartCard", "Other" }, CombatManager.combinedDeckZone.Select(c => c.name).ToList(),
			"flipped: same card exiled");
	}
}
