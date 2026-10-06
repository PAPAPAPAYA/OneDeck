using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DefaultNamespace;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// EditMode tests for the ReviveEffect selector path (plan-card-selector-targeting Step 2):
/// switch-off legacy parity, selector-path goldens per the appendix A.2 ReviveEffect row,
/// and the real-prefab flip comparison (ELITE_REVIVER) + prefab pool/order parity
/// (FINAL_ESCORT, MaxAttack sequence). Deck layout: indices below the Start Card = grave.
/// </summary>
public class ReviveEffectSelectorTests : HeadlessCombatTestFixture
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

	private ReviveEffect CreateReviverOn(GameObject sourceCard, bool useSelector)
	{
		var effect = CreateEffect<ReviveEffect>(sourceCard);
		effect.useTargetSelector = useSelector;
		return effect;
	}

	/// <summary>
	/// Instantiate a real card prefab into the preview scene and wire the protected EffectScript
	/// fields (Edit Mode never runs OnEnable). Resets the switch so tests start from legacy.
	/// </summary>
	private ReviveEffect InstantiatePrefabReviver(string path, out GameObject cardGO)
	{
		var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
		Assert.IsNotNull(prefab, "prefab missing: " + path);
		var clone = Object.Instantiate(prefab);
		SceneManager.MoveGameObjectToScene(clone, _prefabScene);
		_instantiated.Add(clone);
		cardGO = clone;

		var cardScript = clone.GetComponent<CardScript>();
		Assert.IsNotNull(cardScript, "root CardScript missing on " + path);
		// CardFactory assigns these at spawn time; the asset serializes them empty.
		cardScript.myStatusRef = OwnerStatus;
		cardScript.theirStatusRef = EnemyStatus;
		if (cardScript.myStatusEffects == null) cardScript.myStatusEffects = new List<EnumStorage.StatusEffect>();
		if (cardScript.myTags == null) cardScript.myTags = new List<EnumStorage.Tag>();

		var effect = clone.GetComponentInChildren<ReviveEffect>(true);
		Assert.IsNotNull(effect, "ReviveEffect not found on " + path);
		effect.useTargetSelector = false;
		effect.SelectorTypeIDOverride = null;
		typeof(EffectScript).GetField("myCard", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(effect, clone);
		typeof(EffectScript).GetField("myCardScript", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(effect, cardScript);
		typeof(EffectScript).GetField("combatManager", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(effect, CombatManager);
		return effect;
	}

	/// <summary>
	/// Test-side mirror of "configure targetSelector to match the legacy fields" — the manual
	/// step a prefab flip performs. Production code never reads legacy fields in selector mode.
	/// NOTE ReviveSortBy values (None/MaxAttack/MaxExtraAttackTimes = 0/1/2) do NOT line up with
	/// SelectorSort ordinals — mapped explicitly.
	/// </summary>
	private static CardSelector MirrorLegacy(ReviveEffect effect, bool friendly, bool useTags)
	{
		var spec = new CardSelector();
		spec.zone = CardSelector.SelectorZone.GraveSide;
		spec.side = friendly ? CardSelector.SelectorSide.Friendly : CardSelector.SelectorSide.Enemy;
		spec.minionMode = CardSelector.SelectorMinion.Exclude;
		spec.excludeNeutral = true;
		spec.excludePassive = true;
		spec.includeRevealZone = false;
		spec.creatureFilter = (EffectScript.EffectCreatureFilter)effect.creatureFilter; // Any/Creature/Phenomenon/Token = 0..3 on both enums
		spec.rarityFilter = (CardSelector.SelectorRarity)effect.rarityFilter;           // Any/Common/Uncommon/Rare = 0..3 on both enums
		switch (effect.sortBy)
		{
			case ReviveEffect.ReviveSortBy.MaxAttack: spec.sort = CardSelector.SelectorSort.MaxAttack; break;
			case ReviveEffect.ReviveSortBy.MaxExtraAttackTimes: spec.sort = CardSelector.SelectorSort.MaxExtraAttackTimes; break;
			default: spec.sort = CardSelector.SelectorSort.Random; break;
		}
		spec.typeIDFilter = effect.typeIDFilter;
		spec.enhancedOnly = effect.onlyEnhanced;
		spec.tagFilter = useTags && effect.tagsToCheck != null ? new List<EnumStorage.Tag>(effect.tagsToCheck) : null;
		spec.excludeSelf = effect.excludeSelf;
		return spec;
	}

	private GameObject CreatureCard(string name, int attack, int growth, EnumStorage.CardType type = EnumStorage.CardType.Creature, bool wildcard = false)
	{
		var card = CreateCard(true, name);
		var cs = card.GetComponent<CardScript>();
		cs.cardType = type;
		cs.printedAttack = attack;
		cs.attackGrowth = growth;
		cs.wildcardTypeFilter = wildcard;
		return card;
	}

	// ---------- Selector path goldens ----------

	[Test]
	public void SelectorPath_ReviveMyCards_GraveSideFactionParity()
	{
		var graveF = CreatureCard("GraveF", 1, 0);
		var graveE = CreateCard(false, "GraveE");
		var start = CreateStartCard();
		var live = CreatureCard("Live", 1, 0);
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { graveF, graveE, start, live });
		var source = CreateCard(true, "Source");
		var effect = CreateReviverOn(source, useSelector: true);

		effect.ReviveMyCards(1);

		Assert.AreSame(graveF, CombatManager.combinedDeckZone[CombatManager.combinedDeckZone.Count - 1],
			"selector path must revive only the friendly grave card");
	}

	[Test]
	public void SelectorPath_TagEntries_PassSerializedFilter_NonTagEntriesStripIt()
	{
		var tagged = CreatureCard("Tagged", 1, 0);
		tagged.GetComponent<CardScript>().myTags.Add(EnumStorage.Tag.DeathRattle);
		var start = CreateStartCard();
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { tagged, start });
		var source = CreateCard(true, "Source");
		var effect = CreateReviverOn(source, useSelector: true);
		effect.targetSelector.tagFilter = new List<EnumStorage.Tag> { EnumStorage.Tag.DeathRattle };

		effect.ReviveMyCardsWithTag(1);
		Assert.AreSame(tagged, CombatManager.combinedDeckZone[CombatManager.combinedDeckZone.Count - 1],
			"*WithTag entries pass the serialized tagFilter through");

		// Same deck reset; the plain entry strips tags — the only grave card qualifies anyway.
		CombatManager.combinedDeckZone = new List<GameObject> { tagged, start };
		effect.ReviveMyCards(1);
		Assert.AreSame(tagged, CombatManager.combinedDeckZone[CombatManager.combinedDeckZone.Count - 1],
			"non-tag entries must ignore the serialized tagFilter");

		// An untagged grave card is invisible to the *WithTag entry.
		var untagged = CreatureCard("Untagged", 1, 0);
		CombatManager.combinedDeckZone = new List<GameObject> { untagged, start };
		effect.ReviveMyCardsWithTag(1);
		Assert.AreSame(untagged, CombatManager.combinedDeckZone[0], "tag filter must exclude the untagged card");
		Assert.AreEqual(2, CombatManager.combinedDeckZone.Count, "*WithTag entry must fizzle when nothing matches");
	}

	[Test]
	public void SelectorPath_SortComesFromSerializedTargetSelector()
	{
		var weak = CreatureCard("Weak", 1, 0);
		var strong = CreatureCard("Strong", 3, 0);
		var start = CreateStartCard();
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { weak, strong, start });
		var source = CreateCard(true, "Source");
		var effect = CreateReviverOn(source, useSelector: true);
		effect.targetSelector.sort = CardSelector.SelectorSort.MaxAttack;

		effect.ReviveMyCards(1);

		Assert.AreSame(strong, CombatManager.combinedDeckZone[CombatManager.combinedDeckZone.Count - 1],
			"selector mode sorts by targetSelector.sort, not the legacy sortBy field");
	}

	[Test]
	public void SelectorPath_CreatureAndEnhancedPredicatesFromSerialized()
	{
		var curseEnhanced = CreatureCard("CurseEnhanced", 2, 2, EnumStorage.CardType.None);
		var plainCreature = CreatureCard("PlainCreature", 1, 0);
		var enhancedCreature = CreatureCard("EnhancedCreature", 1, 2);
		var start = CreateStartCard();
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { curseEnhanced, plainCreature, enhancedCreature, start });
		var source = CreateCard(true, "Source");
		var effect = CreateReviverOn(source, useSelector: true);
		effect.targetSelector.creatureFilter = EffectScript.EffectCreatureFilter.Creature;
		effect.targetSelector.enhancedOnly = true;

		effect.ReviveMyCards(1);

		Assert.AreSame(enhancedCreature, CombatManager.combinedDeckZone[CombatManager.combinedDeckZone.Count - 1],
			"creature + enhanced gates compose: curse-enhanced is blocked by the type line, plain creature by growth");
	}

	[Test]
	public void SelectorPath_NoStartCard_FizzlesLikeLegacy()
	{
		var a = CreatureCard("A", 1, 0);
		var b = CreatureCard("B", 1, 0);
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { a, b });
		var source = CreateCard(true, "Source");
		var effect = CreateReviverOn(source, useSelector: true);

		Assert.DoesNotThrow(() => effect.ReviveMyCards(1), "empty grave (no Start Card) must fizzle");
		Assert.AreEqual(2, CombatManager.combinedDeckZone.Count);
		Assert.AreSame(a, CombatManager.combinedDeckZone[0], "no card moved");
	}

	[Test]
	public void SelectorPath_OncePerRoundGateStillApplies()
	{
		var g1 = CreatureCard("G1", 1, 0);
		var g2 = CreatureCard("G2", 1, 0);
		var start = CreateStartCard();
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { g1, g2, start });
		var source = CreateCard(true, "Source");
		var effect = CreateReviverOn(source, useSelector: true);
		effect.oncePerRound = 1;

		effect.ReviveMyCards(1);
		var orderAfterFirst = CombatManager.combinedDeckZone.Select(c => c.name).ToList();
		effect.ReviveMyCards(1);

		CollectionAssert.AreEqual(orderAfterFirst, CombatManager.combinedDeckZone.Select(c => c.name).ToList(),
			"oncePerRound is execution-side: selector mode must not bypass the gate (second revive fizzles)");
	}

	[Test]
	public void SelectorPath_WildcardTypeBypass()
	{
		var wild = CreatureCard("Wild", 1, 0, EnumStorage.CardType.None, wildcard: true);
		var start = CreateStartCard();
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { wild, start });
		var source = CreateCard(true, "Source");
		var effect = CreateReviverOn(source, useSelector: true);
		effect.targetSelector.creatureFilter = EffectScript.EffectCreatureFilter.Creature;

		effect.ReviveMyCards(1);

		Assert.AreSame(wild, CombatManager.combinedDeckZone[CombatManager.combinedDeckZone.Count - 1],
			"wildcardTypeFilter bypasses the type line in selector mode too");
	}

	[Test]
	public void SelectorPath_ExcludeSelfInsideGrave()
	{
		var source = CreatureCard("Source", 1, 0);
		var start = CreateStartCard();
		var live = CreatureCard("Live", 1, 0);
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { source, start, live });
		// The source sits in the grave itself; excludeSelf leaves an empty pool → fizzle.
		var effect = CreateReviverOn(source, useSelector: true);

		effect.ReviveMyCards(1);
		Assert.AreEqual(3, CombatManager.combinedDeckZone.Count);
		Assert.AreSame(source, CombatManager.combinedDeckZone[0], "self must not be revived while excludeSelf is on");

		CombatManager.combinedDeckZone = new List<GameObject> { source, start, live };
		effect.targetSelector.excludeSelf = false;
		effect.ReviveMyCards(1);
		Assert.AreSame(source, CombatManager.combinedDeckZone[CombatManager.combinedDeckZone.Count - 1],
			"excludeSelf=false (serialized on targetSelector) revives the source from the grave");
	}

	[Test]
	public void LegacyPath_IgnoresSelectorFields()
	{
		var weak = CreatureCard("Weak", 1, 0);
		var strong = CreatureCard("Strong", 3, 0);
		var start = CreateStartCard();
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { weak, strong, start });
		var source = CreateCard(true, "Source");
		var effect = CreateReviverOn(source, useSelector: false);
		effect.sortBy = ReviveEffect.ReviveSortBy.MaxAttack;
		// Hostile selector config must be dead while the switch is off:
		effect.targetSelector.sort = CardSelector.SelectorSort.MinAttack;
		effect.targetSelector.side = CardSelector.SelectorSide.Enemy;

		effect.ReviveMyCards(1);

		Assert.AreSame(strong, CombatManager.combinedDeckZone[CombatManager.combinedDeckZone.Count - 1],
			"legacy path reads sortBy (max attack, friendly) and ignores targetSelector entirely");
	}

	// ---------- Real-prefab parity ----------

	[Test]
	public void PrefabFlip_ELITE_REVIVER_BehaviorParity()
	{
		GameObject cardGO;
		var effect = InstantiatePrefabReviver("Assets/Prefabs/Cards/4.0/0_Common/ELITE_REVIVER.prefab", out cardGO);
		var weak = CreatureCard("Weak", 1, 0);
		var strong = CreatureCard("Strong", 1, 2);
		var start = CreateStartCard();

		// Round 1: legacy path (creature + onlyEnhanced → only Strong is revivable).
		CombatManager.combinedDeckZone = new List<GameObject> { weak, strong, start };
		effect.ReviveMyCards(1);
		Assert.AreSame(strong, CombatManager.combinedDeckZone[CombatManager.combinedDeckZone.Count - 1],
			"legacy: only the enhanced creature is revivable");

		// Round 2: flip the switch with a mirrored spec; bump the round to reopen oncePerRound.
		CombatManager.combinedDeckZone = new List<GameObject> { weak, strong, start };
		CombatManager.roundNumRef.value++;
		effect.useTargetSelector = true;
		effect.targetSelector = MirrorLegacy(effect, friendly: true, useTags: false);
		effect.ReviveMyCards(1);
		Assert.AreSame(strong, CombatManager.combinedDeckZone[CombatManager.combinedDeckZone.Count - 1],
			"flipped: the selector path must revive the same card the legacy path does");
	}

	[Test]
	public void PrefabPool_ELITE_REVIVER_SetParity()
	{
		GameObject cardGO;
		var effect = InstantiatePrefabReviver("Assets/Prefabs/Cards/4.0/0_Common/ELITE_REVIVER.prefab", out cardGO);
		var curseEnhanced = CreatureCard("CurseEnhanced", 2, 2, EnumStorage.CardType.None);
		var plain = CreatureCard("Plain", 1, 0);
		var enhanced = CreatureCard("Enhanced", 1, 2);
		var start = CreateStartCard();
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { curseEnhanced, plain, enhanced, start });

		var legacyPool = (List<GameObject>)typeof(ReviveEffect).GetMethod("BuildRevivePool", BindingFlags.NonPublic | BindingFlags.Instance)
			.Invoke(effect, new object[] { true, false });
		legacyPool = (List<GameObject>)typeof(ReviveEffect).GetMethod("SortOrShufflePool", BindingFlags.NonPublic | BindingFlags.Instance)
			.Invoke(effect, new object[] { legacyPool });

		int sci;
		var solverPool = CardSelectorSolver.Select(CombatManager.combinedDeckZone, cardGO.GetComponent<CardScript>(),
			MirrorLegacy(effect, friendly: true, useTags: false), out sci);

		Assert.AreEqual(1, solverPool.Count, "creature+enhanced must leave exactly the enhanced creature");
		Assert.AreSame(enhanced, solverPool[0]);
		CollectionAssert.AreEquivalent(legacyPool.Select(c => c.name).ToList(), solverPool.Select(c => c.name).ToList(),
			"solver pool must equal the legacy pool");
	}

	[Test]
	public void PrefabOrder_FINAL_ESCORT_MaxAttackParity()
	{
		GameObject cardGO;
		var effect = InstantiatePrefabReviver("Assets/Prefabs/Cards/4.0/1_Uncommon/FINAL_ESCORT.prefab", out cardGO);
		Assert.AreEqual(ReviveEffect.ReviveSortBy.MaxAttack, effect.sortBy, "FINAL_ESCORT prefab config");
		var a = CreatureCard("A", 1, 0);
		var b = CreatureCard("B", 3, 0);
		var c = CreatureCard("C", 2, 0);
		var start = CreateStartCard();
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { a, b, c, start });

		var legacyPool = (List<GameObject>)typeof(ReviveEffect).GetMethod("BuildRevivePool", BindingFlags.NonPublic | BindingFlags.Instance)
			.Invoke(effect, new object[] { true, false });
		legacyPool = (List<GameObject>)typeof(ReviveEffect).GetMethod("SortOrShufflePool", BindingFlags.NonPublic | BindingFlags.Instance)
			.Invoke(effect, new object[] { legacyPool });

		int sci;
		var solverPool = CardSelectorSolver.Select(CombatManager.combinedDeckZone, cardGO.GetComponent<CardScript>(),
			MirrorLegacy(effect, friendly: true, useTags: false), out sci);

		CollectionAssert.AreEqual(legacyPool.Select(x => x.name).ToList(), solverPool.Select(x => x.name).ToList(),
			"MaxAttack ordering must match (shuffle → stable descending, distinct keys)");
	}
}
