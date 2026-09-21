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
		// The rig's shared dummy UI lives in a preview scene (never saved, so no GameScene dirt);
		// without this call the object still leaks across test classes.
		HeadlessCombatRig.DestroySharedResources();
	}

	[Test]
	public void LoopingSpecimen_ReportsInfinite()
	{
		// Positive control for the flag criterion, on the SYNTHETIC specimen
		// (plan-infinity-specimen-synthetic 2026-09-21, option 2B): TEST_LOOP_HUB x2 — an ungated
		// revive pair that loops forever and deals no damage. The kill-semantics half that used to
		// ride on the lethal sample lives in InfiniteDeckTerminationTests.LethalInfiniteDeck_KillsStubOpponent
		// (kept on the OLD specimen, which still kills), so this test only pins the flag.
		// Criterion v2 (§26) requires the flag run to STARVE the round, so it must be measured
		// against an unkillable dummy: a normal-HP dummy ends nothing here (the hub deals no damage),
		// so the L0 caps conclude the run. Fatigue clock off per §23.5 — production fatigue injects
		// inert cards that break the arrangement and hide the repetition.
		var specimen = LoadSampleDeck("test infinite loop");
		var options = RunBudgetSim.Options.LoopDetection();
		options.GuardTotal = 400;

		var flagRun = RunBudgetSim.RunVsDummy(specimen, dummySize: 3, dummyHp: InfinityAttribution.DefaultDummyHp,
			seed: 4242, options: options);

		Debug.Log("[RunBudgetSim] flag run: " + flagRun.Summary);

		Assert.IsTrue(flagRun.SuspectedInfinite,
			"against an unkillable dummy the hub pair starves the round, so the flag criterion must hold: " + flagRun.Summary);
		Assert.IsTrue(flagRun.TripRoundStarved,
			"criterion v2: the trip carries starvation evidence: " + flagRun.Summary);
		Assert.GreaterOrEqual(flagRun.TripCycles, 8,
			"criterion v2: the tripping run repeats well past the cycle threshold: " + flagRun.Summary);
		Assert.GreaterOrEqual(flagRun.MaxSightingsOfOneArrangement, 3,
			"the report must carry how often the arrangement repeated in a single round: " + flagRun.Summary);
		Assert.Less(flagRun.TripRevealsInRound, flagRun.TotalReveals,
			"detection must land well before the run ends: " + flagRun.Summary);
		Assert.IsTrue(flagRun.BudgetCapConcluded,
			"against an UNKILLABLE dummy and a damage-free loop nothing else can end it, so the L0 caps must be the one that does — "
			+ "the trip is the flag criterion, the conclusion is the harm (§16.3): " + flagRun.Summary);
	}

	[Test]
	public void NonLethalSample_NoLongerLoops_AndTerminatesNaturally()
	{
		// Inverted 2026-09-20 (plan §26 + plan-revive-loop-mitigation §8): the once-per-round
		// revive gate fixed the non-lethal loop (GRAVE_HEXER is gated), so the flag criterion must
		// now read "not infinite" — flagging this deck would be the gate-bounded false positive the
		// v2 criterion removes. Kept as the gate's regression guard.
		var report = RunBudgetSim.RunVsDummy(LoadSampleDeck("non-lethal infinite test"), dummySize: 3, dummyHp: 30, seed: 4242);

		Debug.Log("[RunBudgetSim] " + report.Summary);

		Assert.IsFalse(report.SuspectedInfinite,
			"the gated revive loop is bounded: the arrangement-cycle criterion must not fire on a gate-bounded repetition: " + report.Summary);
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
	public void LoopingSpecimen_VsRealEnemyDeck_ReportsInfinite()
	{
		// Covers the deck-vs-deck entry point with a REAL enemy deck. The hub pair does not care
		// what the enemy plays — the loop spins either way. No kill assertion here: the hub deals
		// no damage, so the run ends on whichever side the production fatigue clock kills —
		// seed-dependent and irrelevant to the flag (the old lethal specimen's kill claim now lives
		// in InfiniteDeckTerminationTests, on the preserved old deck).
		var report = RunBudgetSim.Run(LoadSampleDeck("test infinite loop"), CreateSingleCurseDeck(), seed: 4242);

		Debug.Log("[RunBudgetSim] real-enemy run: " + report.Summary);

		Assert.IsTrue(report.SuspectedInfinite,
			"the loop must still be detected against a real enemy deck: " + report.Summary);
		Assert.GreaterOrEqual(report.MaxSightingsOfOneArrangement, 3,
			"repeat-count evidence must be present: " + report.Summary);
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
