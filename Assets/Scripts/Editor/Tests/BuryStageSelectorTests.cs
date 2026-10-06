using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DefaultNamespace;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// EditMode tests for the BuryEffect / StageEffect selector paths (plan Step 3): per-entry
/// parity for the appendix A.2 exclusion-set drift (passives excluded only where legacy does),
/// the BuryNextXCards positional walk (bounded / passive-source / TEST flag mapping), the
/// StageChosenCards grave-side choke interplay, and real-prefab flip comparisons
/// (RIFT_GUIDE_4.0 Phenomenon bury, KINGSLAYER enemy max-attack, RELIC_WHITE_BANNER
/// creature-only stage). Deck layout: indices below the Start Card = grave side.
/// </summary>
public class BuryStageSelectorTests : HeadlessCombatTestFixture
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

	private T InstantiatePrefabEffect<T>(string path, out GameObject cardGO) where T : EffectScript
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

		var effect = clone.GetComponentInChildren<T>(true);
		Assert.IsNotNull(effect, typeof(T).Name + " not found on " + path);
		typeof(EffectScript).GetField("myCard", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(effect, clone);
		typeof(EffectScript).GetField("myCardScript", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(effect, cardScript);
		typeof(EffectScript).GetField("combatManager", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(effect, CombatManager);
		return effect;
	}

	/// <summary>Test-side mirror of a Bury pool entry's spec (the manual flip mapping).</summary>
	private static CardSelector MirrorBurySpec(BuryEffect effect, CardSelector.SelectorSide side,
		EffectScript.EffectCreatureFilter creatureFilter, bool excludePassive, List<EnumStorage.Tag> tagFilter = null)
	{
		var spec = new CardSelector();
		spec.zone = CardSelector.SelectorZone.DeckSide;
		spec.excludeBottomSlot = true;
		spec.excludeTopSlot = false;
		spec.includeRevealZone = false;
		spec.minionMode = CardSelector.SelectorMinion.Exclude;
		spec.side = side;
		spec.creatureFilter = creatureFilter;
		spec.excludePassive = excludePassive;
		spec.tagFilter = tagFilter;
		spec.excludeSelf = effect.excludeSelf;
		return spec;
	}

	private GameObject CreatureCard(string name, int attack, EnumStorage.CardType type = EnumStorage.CardType.Creature)
	{
		var card = CreateCard(true, name);
		card.GetComponent<CardScript>().cardType = type;
		card.GetComponent<CardScript>().printedAttack = attack;
		return card;
	}

	// ---------- Bury selector path ----------

	[Test]
	public void BurySelector_MyCards_IncludePassives_LiveDeckOnly()
	{
		var start = CreateStartCard();
		var liveF = CreatureCard("LiveF", 1);
		var liveE = CreateCard(false, "LiveE");
		var passiveF = CreatureCard("PassiveF", 1); passiveF.GetComponent<CardScript>().isPassive = true;
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { start, liveF, liveE, passiveF });
		var source = CreateCard(true, "Source");
		var effect = CreateBuryOn(source, useSelector: true);

		effect.BuryMyCards(3); // bury the whole friendly live pool

		var deck = CombatManager.combinedDeckZone;
		Assert.AreEqual(4, deck.Count);
		CollectionAssert.AreEquivalent(new[] { "LiveF", "PassiveF" }, new[] { deck[0].name, deck[1].name },
			"buried pair = the friendly live cards; passives are NOT excluded on BuryMyCards (legacy parity)");
		Assert.AreSame(start, deck[2], "Start Card untouched");
		Assert.AreSame(liveE, deck[3], "enemy card untouched (DeckSide + faction)");
	}

	[Test]
	public void BurySelector_TagEntries_IgnoreCreatureFilter()
	{
		var start = CreateStartCard();
		var creatureNoTag = CreatureCard("CreatureNoTag", 1);
		var taggedCurse = CreatureCard("TaggedCurse", 1, EnumStorage.CardType.None);
		taggedCurse.GetComponent<CardScript>().myTags.Add(EnumStorage.Tag.DeathRattle);
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { start, creatureNoTag, taggedCurse });
		var source = CreateCard(true, "Source");
		var effect = CreateBuryOn(source, useSelector: true);
		effect.targetSelector.creatureFilter = EffectScript.EffectCreatureFilter.Creature; // must be ignored by the tag entry
		effect.targetSelector.tagFilter = new List<EnumStorage.Tag> { EnumStorage.Tag.DeathRattle };

		effect.BuryMyCardsWithTag(2);

		Assert.AreSame(taggedCurse, CombatManager.combinedDeckZone[0],
			"tag entry picks by serialized tagFilter and ignores the serialized creatureFilter (legacy parity)");
	}

	[Test]
	public void BurySelector_TheirCards()
	{
		var start = CreateStartCard();
		var f1 = CreatureCard("F1", 1);
		var e1 = CreateCard(false, "E1"); e1.GetComponent<CardScript>().printedAttack = 1;
		var e2 = CreateCard(false, "E2"); e2.GetComponent<CardScript>().printedAttack = 1;
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { start, f1, e1, e2 });
		var source = CreateCard(true, "Source");
		var effect = CreateBuryOn(source, useSelector: true);

		effect.BuryTheirCards(2);

		var deck = CombatManager.combinedDeckZone;
		Assert.AreEqual(4, deck.Count);
		CollectionAssert.AreEquivalent(new[] { "E1", "E2" }, new[] { deck[0].name, deck[1].name }, "both enemy live cards buried");
		Assert.AreSame(start, deck[2]);
		Assert.AreSame(f1, deck[3], "friendly card untouched");
	}

	[Test]
	public void BurySelector_AllMyCards_KeepOrder_ExcludePassive()
	{
		var start = CreateStartCard();
		var a = CreatureCard("A", 1);
		var b = CreatureCard("B", 1);
		var passiveF = CreatureCard("PassiveF", 1); passiveF.GetComponent<CardScript>().isPassive = true;
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { start, a, b, passiveF });
		var source = CreateCard(true, "Source");
		var effect = CreateBuryOn(source, useSelector: true);

		effect.BuryAllMyCards();

		var deck = CombatManager.combinedDeckZone;
		Assert.AreSame(b, deck[0], "KeepOrder: pool order A,B lands B on top after the two bottom inserts");
		Assert.AreSame(a, deck[1]);
		Assert.AreSame(start, deck[2]);
		Assert.AreSame(passiveF, deck[3], "BuryAllMyCards DOES exclude passives (legacy parity)");
	}

	[Test]
	public void BurySelector_MaxAndMinAttackPickers()
	{
		var source = CreateCard(true, "Source");
		var effect = CreateBuryOn(source, useSelector: true);

		var start = CreateStartCard();
		var low = CreatureCard("Low", 1);
		var high = CreatureCard("High", 3);
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { start, low, high });
		effect.BuryCardWithMaxAttack();
		Assert.AreSame(high, CombatManager.combinedDeckZone[0], "max attack buried");

		CombatManager.combinedDeckZone = new List<GameObject> { start, low, high };
		effect.BuryCardWithMinAttack();
		Assert.AreSame(low, CombatManager.combinedDeckZone[0], "min attack buried");

		var zero = CreatureCard("Zero", 0);
		CombatManager.combinedDeckZone = new List<GameObject> { start, zero, low };
		effect.BuryCardWithMinAttack();
		Assert.AreSame(low, CombatManager.combinedDeckZone[0], "positiveAttackOnly: the 0-attack card is skipped, low buried");
		Assert.AreSame(start, CombatManager.combinedDeckZone[1], "Start Card shifts up as low leaves");
		Assert.AreSame(zero, CombatManager.combinedDeckZone[2], "zero-attack card untouched");
	}

	[Test]
	public void BurySelector_NextX_WalkBoundedAtStartCard()
	{
		var g1 = CreatureCard("G1", 1);
		var g2 = CreatureCard("G2", 1);
		var start = CreateStartCard();
		var l1 = CreatureCard("L1", 1);
		var source = CreatureCard("Source", 1);
		var l3 = CreatureCard("L3", 1);
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { g1, g2, start, l1, source, l3 });
		var effect = CreateBuryOn(source, useSelector: true);

		effect.BuryNextXCards(3);

		var deck = CombatManager.combinedDeckZone;
		Assert.AreEqual(6, deck.Count);
		Assert.AreSame(l1, deck[0], "walk buries L1 then stops at the Start Card boundary (lowerBound = startCardIndex)");
		Assert.AreSame(source, deck[4], "source unmoved");
		Assert.AreSame(l3, deck[5], "deck-top card untouched");
	}

	[Test]
	public void BurySelector_NextX_PassiveSourceWalksFromTop()
	{
		var g1 = CreatureCard("G1", 1);
		var source = CreatureCard("Source", 1); source.GetComponent<CardScript>().isPassive = true;
		var start = CreateStartCard();
		var l1 = CreatureCard("L1", 1);
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { g1, source, start, l1 });
		var effect = CreateBuryOn(source, useSelector: true);

		effect.BuryNextXCards(1);

		Assert.AreSame(l1, CombatManager.combinedDeckZone[0],
			"passiveSourceStartsFromTop=true: the passive source targets the live-zone top (RELIC_CHAIN_BURIAL parity)");
	}

	[Test]
	public void BurySelector_NextX_IgnoreBoundaryFlagMapping()
	{
		var g1 = CreatureCard("G1", 1);
		var start = CreateStartCard();
		var g2 = CreatureCard("G2", 1);
		var source = CreatureCard("Source", 1);
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { g1, start, g2, source });
		var effect = CreateBuryOn(source, useSelector: true);

		// Flag off (default): boundary holds — only G2 is reachable, the Start Card is skipped.
		effect.BuryNextXCards(2);
		var deck = CombatManager.combinedDeckZone;
		Assert.AreSame(g2, deck[0], "flag off: G2 buried");
		Assert.AreSame(g1, deck[1], "flag off: below-boundary G1 untouched");
		Assert.AreSame(start, deck[2], "flag off: Start Card not a target");

		// Flag on (TEST-ONLY): excludeNeutral=false + lowerBound=0 — Start Card and both grave
		// cards are walk targets. Pool order [g2, start]; each insert(0) lands the LAST pool
		// card at index 0, so the final deck is [start, g2, g1, source].
		CombatManager.combinedDeckZone = new List<GameObject> { g1, start, g2, source };
		effect.ignoreStartCardBoundary = true;
		effect.BuryNextXCards(2);
		deck = CombatManager.combinedDeckZone;
		Assert.AreSame(start, deck[0], "flag on: the Start Card itself became a valid target (last insert lands at 0)");
		Assert.AreSame(g2, deck[1], "flag on: g2 buried (second-to-last insert)");
		Assert.AreSame(g1, deck[2], "flag on: G1 untouched (amount 2 spent before reaching it)");
		Assert.AreSame(source, deck[3], "source unmoved");
	}

	[Test]
	public void BurySelector_NextX_BasedOnAttackCount()
	{
		var g1 = CreatureCard("G1", 1);
		var start = CreateStartCard();
		var l1 = CreatureCard("L1", 1);
		var l2 = CreatureCard("L2", 1);
		var source = CreatureCard("Source", 2); // attack = 2 → amount 2
		var l3 = CreatureCard("L3", 1);
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { g1, start, l1, l2, source, l3 });
		var effect = CreateBuryOn(source, useSelector: true);

		effect.BuryNextXCards_BasedOnAttack();

		var deck = CombatManager.combinedDeckZone;
		Assert.AreSame(l1, deck[0], "L1 buried (deeper of the two)");
		Assert.AreSame(l2, deck[1], "L2 buried");
		Assert.AreSame(g1, deck[2], "grave card untouched (boundary at the Start Card)");
		Assert.AreSame(start, deck[3]);
	}

	[Test]
	public void BuryLegacy_IgnoresSelectorFields()
	{
		var start = CreateStartCard();
		var f1 = CreatureCard("F1", 1);
		var e1 = CreatureCard("E1", 1);
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { start, f1, e1 });
		var source = CreateCard(true, "Source");
		var effect = CreateBuryOn(source, useSelector: false);
		effect.targetSelector.side = CardSelector.SelectorSide.Enemy; // hostile config must be dead

		effect.BuryMyCards(1);

		// Legacy buries ONE random FRIENDLY card (F1/E1 pool). If the hostile selector config
		// leaked, the pool would be empty and the untouched deck would leave StartCard on top.
		var top = CombatManager.combinedDeckZone[0];
		Assert.That(new[] { "F1", "E1" }, Does.Contain(top.name),
			"legacy path reads legacy filters only (random friendly pick)");
	}

	// ---------- Stage selector path ----------

	[Test]
	public void StageSelector_MyCards_TopExcluded()
	{
		var start = CreateStartCard();
		var a = CreatureCard("A", 1);
		var b = CreatureCard("B", 1);
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { start, a, b });
		var source = CreateCard(true, "Source");
		var effect = CreateStageOn(source, useSelector: true);

		effect.StageMyCards(1);

		var deck = CombatManager.combinedDeckZone;
		Assert.AreSame(a, deck[2], "A staged to top");
		Assert.AreSame(b, deck[1], "B was the top slot — excluded from the pool (IsCardAtTop parity)");
	}

	[Test]
	public void StageSelector_TokenEntries_MinionOnly_AndTypeIDParam()
	{
		var start = CreateStartCard();
		var minionRift = CreateMinion(true, "MinionRift", "RIFT");
		var minionOther = CreateMinion(true, "MinionOther", "OTHER");
		var normal = CreatureCard("Normal", 1);
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { start, minionRift, minionOther, normal });
		var source = CreateCard(true, "Source");
		var effect = CreateStageOn(source, useSelector: true);

		effect.StageMyTokens(1);
		var topAfterTokens = CombatManager.combinedDeckZone[3];
		Assert.That(topAfterTokens.GetComponent<CardScript>().isMinion, Is.True,
			"StageMyTokens = minionMode Only: the staged card must be a minion (random pick among the two, so assert the kind)");

		// StageAllFriendlyMinion with a typeID param: only the RIFT minion is staged (KeepOrder).
		CombatManager.combinedDeckZone = new List<GameObject> { start, minionRift, minionOther, normal };
		effect.StageAllFriendlyMinion("RIFT");
		var deck = CombatManager.combinedDeckZone;
		Assert.AreSame(minionRift, deck[3], "typeID param narrows to the RIFT minion");

		// Empty param = no typeID filter: both minions staged in pool order.
		CombatManager.combinedDeckZone = new List<GameObject> { start, minionRift, minionOther, normal };
		effect.StageAllFriendlyMinion("");
		deck = CombatManager.combinedDeckZone;
		Assert.AreSame(minionRift, deck[2], "KeepOrder: first pool card lands first");
		Assert.AreSame(minionOther, deck[3], "KeepOrder: second pool card lands second");
		Assert.AreSame(normal, deck[1], "non-minion untouched");
	}

	[Test]
	public void StageSelector_TheirSpecificCard_ParamAndFailure()
	{
		var start = CreateStartCard();
		// NOT a minion: legacy StageTheirSpecificCard excludes minions, so the flip target must
		// be a plain enemy card carrying the typeID.
		var eRift = CreateCard(false, "ERift", "RIFT");
		var eOther = CreateCard(false, "EOther");
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { start, eRift, eOther });
		var source = CreateCard(true, "Source");
		var effect = CreateStageOn(source, useSelector: true);

		effect.StageTheirSpecificCard("RIFT");
		Assert.AreSame(eRift, CombatManager.combinedDeckZone[2], "typeID param picks the enemy RIFT card");

		CombatManager.combinedDeckZone = new List<GameObject> { start, eRift, eOther };
		Assert.DoesNotThrow(() => effect.StageTheirSpecificCard("NOPE"), "no match → failure message, no crash");
		Assert.AreSame(eRift, CombatManager.combinedDeckZone[1], "no match → nothing moved");

		CombatManager.combinedDeckZone = new List<GameObject> { start, eRift, eOther };
		Assert.DoesNotThrow(() => effect.StageTheirSpecificCard(""), "empty param → legacy failure path, no crash");
		Assert.AreEqual(3, CombatManager.combinedDeckZone.Count, "empty param must not stage anything (legacy parity)");
	}

	[Test]
	public void StageSelector_GraveCardsRemovedByExecutorChoke()
	{
		var g1 = CreatureCard("G1", 1);
		var start = CreateStartCard();
		var a = CreatureCard("A", 1);
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { g1, start, a });
		var source = CreateCard(true, "Source");
		var effect = CreateStageOn(source, useSelector: true);

		effect.StageMyCards(1);

		var deck = CombatManager.combinedDeckZone;
		Assert.AreSame(g1, deck[0], "grave card enters the legacy-shaped pool but the StageChosenCards choke removes it");
		Assert.AreSame(start, deck[1]);
		Assert.AreSame(a, deck[2], "top slot was excluded from the pool, so nothing is staged");
	}

	[Test]
	public void StageLegacy_IgnoresSelectorFields()
	{
		var start = CreateStartCard();
		var a = CreatureCard("A", 1);
		var b = CreatureCard("B", 1);
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { start, a, b });
		var source = CreateCard(true, "Source");
		var effect = CreateStageOn(source, useSelector: false);
		effect.targetSelector.minionMode = CardSelector.SelectorMinion.Only; // hostile config must be dead

		effect.StageMyCards(1);

		Assert.AreSame(a, CombatManager.combinedDeckZone[2], "legacy path stages the non-top friendly card");
	}

	// ---------- Real-prefab flips ----------

	[Test]
	public void PrefabFlip_RIFT_GUIDE_4_0_BuryMyCards()
	{
		GameObject cardGO;
		var effect = InstantiatePrefabEffect<BuryEffect>("Assets/Prefabs/Cards/4.0/1_Uncommon/RIFT_GUIDE_4.0.prefab", out cardGO);
		Assert.AreEqual(EffectScript.EffectCreatureFilter.Phenomenon, effect.creatureFilter, "prefab config");

		var start = CreateStartCard();
		var creature = CreatureCard("Creature", 1);
		var phenomenon = CreatureCard("Phenomenon", 1, EnumStorage.CardType.None);

		// Round 1: legacy.
		CombatManager.combinedDeckZone = new List<GameObject> { start, creature, phenomenon };
		effect.BuryMyCards(1);
		Assert.AreSame(phenomenon, CombatManager.combinedDeckZone[0], "legacy: only the phenomenon card qualifies");

		// Round 2: flip with the mirrored spec.
		CombatManager.combinedDeckZone = new List<GameObject> { start, creature, phenomenon };
		effect.useTargetSelector = true;
		effect.targetSelector = MirrorBurySpec(effect, CardSelector.SelectorSide.Friendly,
			EffectScript.EffectCreatureFilter.Phenomenon, excludePassive: false);
		effect.BuryMyCards(1);
		Assert.AreSame(phenomenon, CombatManager.combinedDeckZone[0], "flipped: same card buried");
	}

	[Test]
	public void PrefabFlip_KINGSLAYER_BuryCardWithMaxAttack()
	{
		GameObject cardGO;
		var effect = InstantiatePrefabEffect<BuryEffect>("Assets/Prefabs/Cards/4.0/1_Uncommon/KINGSLAYER.prefab", out cardGO);
		Assert.IsFalse(effect.targetFriendly, "prefab config: enemy-side max picker");

		var start = CreateStartCard();
		var eLow = CreateCard(false, "ELow"); eLow.GetComponent<CardScript>().printedAttack = 1;
		var eHigh = CreateCard(false, "EHigh"); eHigh.GetComponent<CardScript>().printedAttack = 3;
		var fX = CreatureCard("FX", 9); // friendly high attack must be ignored (enemy side)

		CombatManager.combinedDeckZone = new List<GameObject> { start, eLow, eHigh, fX };
		effect.BuryCardWithMaxAttack();
		Assert.AreSame(eHigh, CombatManager.combinedDeckZone[0], "legacy: highest-attack ENEMY card buried");

		CombatManager.combinedDeckZone = new List<GameObject> { start, eLow, eHigh, fX };
		effect.useTargetSelector = true;
		effect.targetSelector = MirrorBurySpec(effect, CardSelector.SelectorSide.Enemy, // flip maps targetFriendly=false → side=Enemy
			EffectScript.EffectCreatureFilter.Any, excludePassive: true);
		effect.targetSelector.sort = CardSelector.SelectorSort.MaxAttack;
		effect.BuryCardWithMaxAttack();
		Assert.AreSame(eHigh, CombatManager.combinedDeckZone[0], "flipped: same card buried");
	}

	[Test]
	public void PrefabFlip_RELIC_WHITE_BANNER_StageCardWithMaxAttack()
	{
		GameObject cardGO;
		var effect = InstantiatePrefabEffect<StageEffect>("Assets/Prefabs/Cards/4.0/1_Uncommon/RELIC_WHITE_BANNER.prefab", out cardGO);
		Assert.IsTrue(effect.creatureOnly, "prefab config");
		Assert.IsTrue(effect.targetFriendly, "prefab config");

		var start = CreateStartCard();
		var fCurse = CreatureCard("FCurse", 5, EnumStorage.CardType.None); // attack-holding curse: excluded by creatureOnly
		var fLow = CreatureCard("FLow", 1);
		var fMid = CreatureCard("FMid", 2); // deck top — excluded by IsCardAtTop

		CombatManager.combinedDeckZone = new List<GameObject> { start, fCurse, fLow, fMid };
		effect.StageCardWithMaxAttack();
		Assert.AreSame(fLow, CombatManager.combinedDeckZone[3], "legacy: creature-only picker stages the best creature (curse blocked, top slot excluded)");

		CombatManager.combinedDeckZone = new List<GameObject> { start, fCurse, fLow, fMid };
		effect.useTargetSelector = true;
		var spec = new CardSelector();
		spec.zone = CardSelector.SelectorZone.DeckSide;
		spec.excludeTopSlot = true;
		spec.includeRevealZone = false;
		spec.minionMode = CardSelector.SelectorMinion.Exclude;
		spec.excludePassive = true;
		spec.side = CardSelector.SelectorSide.Friendly;              // flip maps targetFriendly=true → side=Friendly
		spec.creatureFilter = EffectScript.EffectCreatureFilter.Creature; // flip maps creatureOnly → creatureFilter=Creature
		spec.sort = CardSelector.SelectorSort.MaxAttack;
		spec.excludeSelf = effect.excludeSelf;
		effect.targetSelector = spec;
		effect.StageCardWithMaxAttack();
		Assert.AreSame(fLow, CombatManager.combinedDeckZone[3], "flipped: same card staged");
	}

	// ---------- helpers ----------

	private BuryEffect CreateBuryOn(GameObject sourceCard, bool useSelector)
	{
		var effect = CreateEffect<BuryEffect>(sourceCard);
		effect.useTargetSelector = useSelector;
		return effect;
	}

	private StageEffect CreateStageOn(GameObject sourceCard, bool useSelector)
	{
		var effect = CreateEffect<StageEffect>(sourceCard);
		effect.useTargetSelector = useSelector;
		return effect;
	}
}
