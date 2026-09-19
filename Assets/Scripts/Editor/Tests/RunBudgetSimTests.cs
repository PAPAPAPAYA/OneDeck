using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Pins RunBudgetSim (plans/plan-infinity-detection-2026-09-17.md §2, §10 P1) against the
/// sample decks the plan's §13/§15 acceptance work already characterised, plus the property the
/// abstraction exists for: reproducibility under a fixed seed (attribution replays the same
/// pairing and isolates it against a dummy, which is only meaningful if the seed pins the run).
/// Independent cross-check: ArrangementCycleDetectorTests and InfiniteDeckTerminationTests run
/// the same specimens through the test fixture's own driver. Two implementations agreeing is
/// the point — do not "unify" them without keeping one as the other's check.
/// Deliberately NOT derived from HeadlessCombatTestFixture: the whole purpose of the rig is to
/// work outside that fixture, so deriving here would hide exactly the failure this guards.
/// </summary>
public class RunBudgetSimTests
{
	private const string DeckFolder = "Assets/SORefs/Decks/test decks/chain tests/4.0";
	private const string JuOnPrefabPath = "Assets/Prefabs/Cards/3.0 no cost (current)/_DONT INCLUDE/Token/JU_ON.prefab";

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
		// The rig's shared dummy UI lives in the active scene; without this the class leaves an
		// orphan behind, GameScene stays dirty, and the NEXT Test Runner run hits the save modal.
		HeadlessCombatRig.DestroySharedResources();
	}

	[Test]
	public void LethalSample_ReportsInfinite_AndKillsWithoutL0()
	{
		var report = RunBudgetSim.RunVsDummy(LoadSampleDeck("lethal infinite test"), dummySize: 3, dummyHp: 30, seed: 4242);

		Debug.Log("[RunBudgetSim] " + report.Summary);

		Assert.IsTrue(report.SuspectedInfinite,
			"the lethal revive loop repeats its arrangement inside one round, so the flag criterion must hold: " + report.Summary);
		Assert.GreaterOrEqual(report.MaxSightingsOfOneArrangement, 3,
			"the report must carry how often the arrangement repeated in a single round: " + report.Summary);
		Assert.IsTrue(report.EnemyDied,
			"the loop's own pump kills the stub opponent: " + report.Summary);
		Assert.IsFalse(report.BudgetCapConcluded,
			"the kill converges long before the L0 caps, which must stay unused: " + report.Summary);
		Assert.AreEqual(1, report.Rounds,
			"the whole lethal combat lives inside round 1 (round-starved loop): " + report.Summary);
	}

	[Test]
	public void NonLethalSample_ReportsInfinite()
	{
		var report = RunBudgetSim.RunVsDummy(LoadSampleDeck("non-lethal infinite test"), dummySize: 3, dummyHp: 30, seed: 4242);

		Debug.Log("[RunBudgetSim] " + report.Summary);

		Assert.IsTrue(report.SuspectedInfinite,
			"the non-lethal revive loop starves the round boundary too, so the cycle rule must trip: " + report.Summary);
		Assert.IsTrue(report.EnemyDied,
			"the combat still converges by death (the detector is observation-only): " + report.Summary);
		Assert.IsFalse(report.BudgetCapConcluded,
			"the L0 caps must stay unused: " + report.Summary);
	}

	[Test]
	public void SameSeed_ReproducesTheSameCombat()
	{
		var deckA = LoadSampleDeck("lethal infinite test");
		var deckB = LoadSampleDeck("non-lethal infinite test");

		var first = RunBudgetSim.Run(deckA, deckB, seed: 77);
		var second = RunBudgetSim.Run(deckA, deckB, seed: 77);

		Assert.AreEqual(first.TotalReveals, second.TotalReveals, "same seed must reveal the same number of cards");
		Assert.AreEqual(first.Rounds, second.Rounds, "same seed must end in the same round");
		Assert.AreEqual(first.Iterations, second.Iterations, "same seed must take the same number of steps");
		Assert.AreEqual(first.TripHash, second.TripHash, "same seed must trip on the same arrangement");
		Assert.AreEqual(first.ArrangementCycleTripped, second.ArrangementCycleTripped);
		Assert.AreEqual(first.EnemyHpFinal, second.EnemyHpFinal, "same seed must leave the same HP");
		Assert.AreEqual(first.OwnerHpFinal, second.OwnerHpFinal, "same seed must leave the same HP");

		Debug.Log("[RunBudgetSim] reproducibility check: " + first.Summary);
	}

	[Test]
	public void LethalSample_VsSeededCurseEnemy_ReportsInfinite()
	{
		// Covers the deck-vs-deck entry point with a REAL enemy deck. One JU_ON is the
		// reachable maximum (plan §17): only CurseEffect.EnhanceCurse creates curses and only
		// when none exists, so the 2xJU_ON stub this test used to carry was a state the game
		// cannot produce.
		var report = RunBudgetSim.Run(LoadSampleDeck("lethal infinite test"), CreateSingleCurseDeck(), seed: 4242);

		Debug.Log("[RunBudgetSim] seeded-curse enemy: " + report.Summary);

		Assert.IsTrue(report.SuspectedInfinite,
			"the loop must still be detected against a real curse-carrying enemy: " + report.Summary);
		Assert.GreaterOrEqual(report.MaxSightingsOfOneArrangement, 3,
			"repeat-count evidence must be present: " + report.Summary);
		Assert.LessOrEqual(report.EnemyHpFinal, 0, "the pump must kill the enemy: " + report.Summary);
	}

	// ---- fixtures ----

	private DeckSO LoadSampleDeck(string assetName)
	{
		var deck = AssetDatabase.LoadAssetAtPath<DeckSO>(DeckFolder + "/" + assetName + ".asset");
		Assert.IsNotNull(deck, "sample deck missing: " + assetName);
		return deck;
	}

	//// <summary>
	/// A real enemy deck carrying ONE JU_ON - the reachable maximum (plan §17). Lives only for
	/// the test, hence a runtime-created DeckSO rather than an asset.
	/// </summary>
	private DeckSO CreateSingleCurseDeck()
	{
		var juOn = AssetDatabase.LoadAssetAtPath<GameObject>(JuOnPrefabPath);
		Assert.IsNotNull(juOn, "JU_ON curse token prefab missing: " + JuOnPrefabPath);
		var deck = ScriptableObject.CreateInstance<DeckSO>();
		_tempObjects.Add(deck);
		deck.name = "curse-enemy(1xJU_ON)";
		deck.deck = new List<GameObject> { juOn };
		return deck;
	}
}
