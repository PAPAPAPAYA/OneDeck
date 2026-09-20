using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The deferred P2 path (plan-infinity-detection §19.5, user ruling 2026-09-19: no simulation
/// inside the combat frame). The detector only queues a trip into InfinityTripJournal — which
/// lives in the RUNTIME assembly so a shipped build can capture evidence — and the editor-side
/// processor does the expensive attribution later.
/// </summary>
public class InfinityTripJournalTests
{
	private const string DeckFolder = "Assets/SORefs/Decks/test decks/chain tests/4.0";
	private const string JuOnPrefabPath = "Assets/Prefabs/Cards/3.0 no cost (current)/_DONT INCLUDE/Token/JU_ON.prefab";

	private readonly List<Object> _tempObjects = new List<Object>();

	[SetUp]
	public void SetUp()
	{
		InfinityTripJournal.Clear();
	}

	[TearDown]
	public void TearDown()
	{
		InfinityTripJournal.Clear();
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

	[Test]
	public void Journal_QueuesOldestFirst_AndDrainClears()
	{
		var deck = LoadSampleDeck("lethal infinite test");
		InfinityTripJournal.Record(0xAAAAu, 1, 5, 11, deck, deck, 0);
		InfinityTripJournal.Record(0xBBBBu, 2, 9, 22, deck, deck, 909);

		Assert.AreEqual(2, InfinityTripJournal.Count);
		Assert.AreEqual(0xAAAAu, InfinityTripJournal.Pending[0].TripHash, "oldest first");
		Assert.AreEqual(0xBBBBu, InfinityTripJournal.Pending[1].TripHash);

		var drained = InfinityTripJournal.Drain();
		Assert.AreEqual(2, drained.Count);
		Assert.AreEqual(0, InfinityTripJournal.Count, "draining must empty the queue");
		Assert.AreEqual(22, drained[1].CombatSeed, "the seed must survive the round trip");
		Assert.AreEqual(909, drained[1].EnemyDeckId, "the accused ghost deck row must survive too (§20.3)");
		Assert.AreEqual(0, drained[0].EnemyDeckId, "0 = local pool enemy, nothing to flag");
		Assert.IsNotEmpty(drained[0].CapturedUtc);
	}

	[Test]
	public void Journal_IsBounded_DroppingOldest()
	{
		var deck = LoadSampleDeck("lethal infinite test");
		for (int i = 0; i < InfinityTripJournal.MaxEntries + 5; i++)
		{
			InfinityTripJournal.Record((uint)i, 1, 1, i, deck, deck, 0);
		}

		Assert.AreEqual(InfinityTripJournal.MaxEntries, InfinityTripJournal.Count,
			"a runaway combat must not grow the queue without bound");
		Assert.AreEqual(5, InfinityTripJournal.Pending[0].CombatSeed,
			"the five oldest entries must have been dropped");
	}

	[Test]
	public void Processor_TurnsAQueuedTripIntoAComboEntry()
	{
		var lethal = LoadSampleDeck("lethal infinite test");
		// The enemy side carries the looping deck, so the expected verdict is EnemyDeck.
		InfinityTripJournal.Record(0x1234u, 1, 7, 4242, lethal, lethal, 505);

		var options = new RunBudgetSim.Options();
		options.GuardTotal = 300;

		var reports = InfinityAttributionProcessor.ProcessPending(new[] { 4242 }, options);

		Assert.AreEqual(1, reports.Count, "one queued trip must yield exactly one entry");
		var report = reports[0];
		Debug.Log("[P2] deferred report: " + report.Summary());
		Debug.Log("[P2] deferred live signal: " + report.liveTripSignal);

		Assert.AreEqual("EnemyDeck", report.responsibility);
		Assert.AreEqual(0, InfinityTripJournal.Count, "processing must drain the queue");
		StringAssert.Contains("combatSeed=4242", report.liveTripSignal,
			"the entry must carry the LIVE trip's seed, not the reproduction's");
		StringAssert.Contains("1234", report.liveTripSignal, "and the live arrangement hash");
		Assert.AreEqual(2, report.mySide.Length, "the minimized responsible set is both combo cards");
	}

	[Test]
	public void Processor_EmptyQueue_ProducesNothing()
	{
		Assert.AreEqual(0, InfinityAttributionProcessor.ProcessPending(new[] { 4242 }).Count,
			"no queued trip means no work and no invented entry");
	}

	[Test]
	public void Processor_NonReproducingTrip_YieldsZeroEntries()
	{
		// Characterization pin: a live trip is only a SUSPICION. When the headless reproduction
		// cannot reproduce the loop (here both sides are the inert single-JU_ON deck — the
		// reachable maximum curse count per §17), attribution says None and the processor drops
		// the trip: never a guess, no combo entry.
		var inert = BuildSingleCurseDeck();
		InfinityTripJournal.Record(0x9999u, 1, 5, 4242, inert, inert, 505);

		var options = new RunBudgetSim.Options();
		options.GuardTotal = 300;

		var reports = InfinityAttributionProcessor.ProcessPending(new[] { 4242 }, options);

		Assert.AreEqual(0, reports.Count, "a trip that does not reproduce headlessly must yield no entry");
		Assert.AreEqual(0, InfinityTripJournal.Count, "the queue is still drained — the suspicion was adjudicated, not kept");
	}

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
		var deck = ScriptableObject.CreateInstance<DeckSO>();
		_tempObjects.Add(deck);
		deck.name = "inert-enemy(1xJU_ON)";
		deck.deck = new List<GameObject> { juOn };
		return deck;
	}
}
