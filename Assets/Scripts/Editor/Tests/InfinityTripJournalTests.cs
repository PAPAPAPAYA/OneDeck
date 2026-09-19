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
		InfinityTripJournal.Record(0xAAAAu, 1, 5, 11, deck, deck);
		InfinityTripJournal.Record(0xBBBBu, 2, 9, 22, deck, deck);

		Assert.AreEqual(2, InfinityTripJournal.Count);
		Assert.AreEqual(0xAAAAu, InfinityTripJournal.Pending[0].TripHash, "oldest first");
		Assert.AreEqual(0xBBBBu, InfinityTripJournal.Pending[1].TripHash);

		var drained = InfinityTripJournal.Drain();
		Assert.AreEqual(2, drained.Count);
		Assert.AreEqual(0, InfinityTripJournal.Count, "draining must empty the queue");
		Assert.AreEqual(22, drained[1].CombatSeed, "the seed must survive the round trip");
		Assert.IsNotEmpty(drained[0].CapturedUtc);
	}

	[Test]
	public void Journal_IsBounded_DroppingOldest()
	{
		var deck = LoadSampleDeck("lethal infinite test");
		for (int i = 0; i < InfinityTripJournal.MaxEntries + 5; i++)
		{
			InfinityTripJournal.Record((uint)i, 1, 1, i, deck, deck);
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
		InfinityTripJournal.Record(0x1234u, 1, 7, 4242, lethal, lethal);

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

	private DeckSO LoadSampleDeck(string assetName)
	{
		var deck = AssetDatabase.LoadAssetAtPath<DeckSO>(DeckFolder + "/" + assetName + ".asset");
		Assert.IsNotNull(deck, "sample deck missing: " + assetName);
		return deck;
	}
}
