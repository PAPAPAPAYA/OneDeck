using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// P2 pipeline tests (plan-infinity-detection §4): attribution tri-run, ddmin minimization and
/// the combo-library entry. Runs entirely through RunBudgetSim, so these tests exercise the same
/// path the batch scan (P5) and the runtime attribution hook (P2 wiring) will use.
/// Specimens: the lethal sample for "loops", the 09-13 ring for "no longer loops" (§6/§19.4), and
/// a single JU_ON enemy deck — the reachable maximum curse count (§17) — for the non-looping
/// opponent side.
/// PairOnly has no test: §7.1 records that no cross-side-only specimen is currently known, and
/// inventing one would be testing a fiction.
/// </summary>
public class InfinityPipelineTests
{
	private const string DeckFolder = "Assets/SORefs/Decks/test decks/chain tests/4.0";
	private const string JuOnPrefabPath = "Assets/Prefabs/Cards/3.0 no cost (current)/_DONT INCLUDE/Token/JU_ON.prefab";
	private const string BurialPath = "Assets/Prefabs/Cards/4.0/1_Uncommon/RELIC_CHAIN_BURIAL.prefab";
	private const string GrantPath = "Assets/Prefabs/Cards/4.0/1_Uncommon/DEATHBED_GRANT.prefab";
	private const string SkeletonPath = "Assets/Prefabs/Cards/4.0/0_Common/SOLDIER_SKELETON_4.0.prefab";
	private const string UtilityIncomePath = "Assets/Prefabs/Cards/4.0/0_Common/UTILITY_INCOME_1.prefab";
	private const string UtilitySlotPath = "Assets/Prefabs/Cards/4.0/1_Uncommon/UTILITY_SLOT_U_2.prefab";

	private static readonly int[] Seeds = { 4242, 7 };

	/// <summary>
	/// Keep the minimizer cheap: every predicate evaluation is a full combat per seed, and the
	/// 1e8-HP dummy would otherwise push each run to the 1500-reveal cap. The arrangement trip
	/// lands around reveal 7-19, so a 300 cap is far past the signal.
	/// </summary>
	private static RunBudgetSim.Options FastOptions()
	{
		var options = new RunBudgetSim.Options();
		options.GuardTotal = 300;
		return options;
	}

	private readonly List<Object> _tempObjects = new List<Object>();

	[TearDown]
	public void TearDown()
	{
		foreach (var obj in _tempObjects)
		{
			if (obj != null) Object.DestroyImmediate(obj);
		}
		_tempObjects.Clear();
	}

	[OneTimeTearDown]
	public void OneTimeTearDown()
	{
		HeadlessCombatRig.DestroySharedResources();
	}

	// ---- attribution (§4 tri-run) ----

	[Test]
	public void Attribution_EnemyDeckLoopsAlone_IsFlagged()
	{
		var lethal = LoadSampleDeck("lethal infinite test");
		var result = InfinityAttribution.Attribute(lethal, lethal, seed: 4242, options: FastOptions());

		Debug.Log("[P2] " + result);

		Assert.AreEqual(InfinityResponsibility.EnemyDeck, result.Responsibility,
			"the enemy deck loops on its own against the dummy: " + result.Explain());
		Assert.IsTrue(result.ShouldFlagEnemy, "an enemy-side combo is the only flaggable verdict");
		Assert.IsNotNull(result.EnemyVsDummy, "step-1 evidence must be kept");
		Assert.IsNull(result.PairReplay, "the tri-run must stop at the first conclusive step");
	}

	[Test]
	public void Attribution_OwnerDeckLoopsAlone_FlagsNothing()
	{
		var lethal = LoadSampleDeck("lethal infinite test");
		var singleCurseEnemy = BuildSingleCurseDeck();
		var result = InfinityAttribution.Attribute(lethal, singleCurseEnemy, seed: 4242, options: FastOptions());

		Debug.Log("[P2] " + result);

		Assert.AreEqual(InfinityResponsibility.OwnerDeck, result.Responsibility,
			"the player-side combo triggers with an innocent enemy: " + result.Explain());
		Assert.IsFalse(result.ShouldFlagEnemy,
			"§1.2/§9: the player side is allowed — nothing may be flagged for serving");
		Assert.IsTrue(result.HasOneSidedCombo, "the owner-side core still belongs in the library");
	}

	[Test]
	public void Attribution_TwoInertSides_ReportsNone()
	{
		var inert = BuildSingleCurseDeck();
		var result = InfinityAttribution.Attribute(inert, inert, seed: 4242, options: FastOptions());

		Assert.AreEqual(InfinityResponsibility.None, result.Responsibility,
			"nothing loops here, so attribution must not invent a culprit: " + result.Explain());
		Assert.IsFalse(result.ShouldFlagEnemy);
	}

	// ---- minimization (§4 ddmin) ----

	[Test]
	public void Minimize_LethalSample_KeepsBothComboCards()
	{
		var lethal = LoadSampleDeck("lethal infinite test");
		var result = ComboMinimizer.Minimize(lethal, Seeds, dummySize: 3, dummyHp: 100000000, options: FastOptions());

		Debug.Log("[P2] minimize lethal -> " + result.CardIds() + " runs=" + result.RunsPerformed
			+ " oneMinimal=" + result.IsOneMinimal + " stable=" + result.MultiSeedStable);

		Assert.IsTrue(result.MultiSeedStable, "the full deck must loop on every seed before minimizing");
		Assert.AreEqual(2, result.Cards.Count,
			"neither half of the lethal pair loops without the other, so the minimum is both cards: " + result.CardIds());
		Assert.IsTrue(result.IsOneMinimal, "removing either card must break the loop: " + result.CardIds());
		Assert.IsFalse(result.Truncated, "the budget must be ample for a two-card deck");
		StringAssert.Contains("RELIC_CURSE_REVIVAL", result.CardIds());
		StringAssert.Contains("CURSE_GARDENER", result.CardIds());
	}

	[Test]
	public void Minimize_NonLoopingRing_IsNotIngested()
	{
		var ring = BuildRingDeck();
		var result = ComboMinimizer.Minimize(ring, Seeds, dummySize: 3, dummyHp: 100000000, options: FastOptions());

		Assert.IsFalse(result.MultiSeedStable,
			"§4: a deck that does not loop must never produce a combo entry");
		Assert.IsFalse(result.IsOneMinimal, "nothing was minimized");
	}

	[Test]
	public void Minimize_StripsUtilityPassives_AndReverifiesTheRemainder()
	{
		// A real deck's shape: the lethal pair plus utility passives. ddmin measures the ARRANGEMENT,
		// and a passive changes it without doing anything, so a "1-minimal" set can keep one for
		// structural reasons — measured on decks 110/111, where ddmin kept two (2026-09-19). Those
		// must not reach a combo key: the key is a containment test, so a passive inside it would
		// hide every variant that runs the same loop with a different passive.
		var cards = new List<GameObject>();
		var lethal = LoadSampleDeck("lethal infinite test");
		foreach (var card in lethal.deck) cards.Add(card);
		foreach (var path in new[] { UtilityIncomePath, UtilitySlotPath })
		{
			var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
			Assert.IsNotNull(prefab, "utility prefab missing: " + path);
			cards.Add(prefab);
		}

		var result = ComboMinimizer.Minimize(NewDeck("lethal+passives", cards), Seeds,
			dummySize: 3, dummyHp: 100000000, options: FastOptions());
		Debug.Log("[P2] minimize lethal+passives -> " + result.CardIds()
			+ " stripped=" + result.StrippedIds() + " verified=" + result.StripVerified);

		// What matters is the END STATE: a combo key must never contain a utility passive, whether
		// ddmin dropped it (small decks — it can, since the pair loops alone) or the strip did
		// (large decks, where removing a passive breaks the trip — decks 110/111 in the field).
		Assert.AreEqual(2, result.Cards.Count, "the adopted minimum is the two combo cards: " + result.CardIds());
		Assert.IsTrue(result.IsOneMinimal, "1-minimality holds on the adopted set");
		Assert.IsFalse(result.CardIds().Contains("UTILITY_"), "no utility passive may survive: " + result.CardIds());
		if (result.StrippedCards.Count > 0)
		{
			Assert.IsTrue(result.StripVerified,
				"a stripped set is adopted only after re-verifying that it still loops: " + result.StrippedIds());
		}

		// The adopted set is what the combo key is built from, so the payload must explain itself
		// whenever the strip (not ddmin) did the removal.
		var attribution = new InfinityAttributionResult
		{
			OwnerDeckName = "test", EnemyDeckName = "lethal+passives", Seed = Seeds[0],
			Responsibility = InfinityResponsibility.EnemyDeck, EnemyVsDummy = new BudgetTripReport(),
		};
		var report = LoopReportBuilder.Build(attribution, result, Seeds, attribution.EnemyVsDummy);
		Assert.AreEqual(2, report.mySide.Length);
		Assert.IsFalse(string.Join(",", report.mySide).Contains("UTILITY_"));
		if (result.StrippedCards.Count > 0)
		{
			StringAssert.Contains("stripped utility passive", report.stripNote);
			StringAssert.Contains("re-verified", report.stripNote);
		}
	}

	// ---- combo entry (§4 schema) ----

	[Test]
	public void LoopReport_CarriesSchemaAndBindingEvidence()
	{
		var lethal = LoadSampleDeck("lethal infinite test");
		var attribution = InfinityAttribution.Attribute(lethal, lethal, seed: 4242, options: FastOptions());
		var minimized = ComboMinimizer.Minimize(lethal, Seeds, dummySize: 3, dummyHp: 100000000, options: FastOptions());
		var report = LoopReportBuilder.Build(attribution, minimized, Seeds, attribution.EnemyVsDummy);

		Debug.Log("[P2] " + report.Summary());
		foreach (var role in report.roles) Debug.Log("[P2] evidence " + role);

		Assert.AreEqual("EnemyDeck", report.responsibility);
		Assert.AreEqual("candidate", report.status, "§4 lifecycle starts at candidate");
		Assert.AreEqual(2, report.mySide.Length);
		Assert.AreEqual(report.mySide.Length, report.roles.Length, "one evidence line per card, same order");

		// Cross-side is reserved and must stay empty until §7.1 is revisited.
		Assert.AreEqual(0, report.enemySide.Length);
		Assert.AreEqual(Seeds.Length, report.reproSeeds.Length);

		// Evidence is the load-bearing part (the role labels are a documented heuristic): assert the
		// REAL bindings, read off the prefabs, not the classifier's verdict.
		string joined = string.Join(" | ", report.roles);
		StringAssert.Contains("OnMeRevealed", joined);
		StringAssert.Contains("EnhanceCurse", joined);
		StringAssert.Contains("OnHostileCurseRevealed", joined);
		StringAssert.Contains("ReviveMyCards", joined);

		StringAssert.Contains("arrangement-cycle", report.tripSignal);

		string json = report.ToJson();
		StringAssert.Contains("mySide", json);
		StringAssert.Contains("candidate", json);
	}

	// ---- fixtures ----

	private DeckSO LoadSampleDeck(string assetName)
	{
		var deck = AssetDatabase.LoadAssetAtPath<DeckSO>(DeckFolder + "/" + assetName + ".asset");
		Assert.IsNotNull(deck, "sample deck missing: " + assetName);
		return deck;
	}

	/// <summary>A real deck holding one JU_ON — the reachable maximum (§17) — as an inert enemy.</summary>
	private DeckSO BuildSingleCurseDeck()
	{
		var juOn = AssetDatabase.LoadAssetAtPath<GameObject>(JuOnPrefabPath);
		Assert.IsNotNull(juOn, "JU_ON prefab missing: " + JuOnPrefabPath);
		return NewDeck("inert-enemy(1xJU_ON)", new List<GameObject> { juOn });
	}

	private DeckSO BuildRingDeck()
	{
		var cards = new List<GameObject>();
		foreach (var path in new[] { BurialPath, GrantPath, SkeletonPath })
		{
			var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
			Assert.IsNotNull(prefab, "ring card prefab missing: " + path);
			cards.Add(prefab);
		}
		return NewDeck("ring-0913(3 cards)", cards);
	}

	private DeckSO NewDeck(string name, List<GameObject> cards)
	{
		var deck = ScriptableObject.CreateInstance<DeckSO>();
		_tempObjects.Add(deck);
		deck.name = name;
		deck.deck = cards;
		return deck;
	}
}
