using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using TestWriteRead;

/// <summary>
/// S1 select-page EditMode tests for the DeckSaver reservation branch (plan
/// plan-opponent-select-page-2026-10-06 §4.2): pick-time populate from the reserved
/// entry, the Current-refill idempotency guard, and flow parity without a pick. Fully
/// hermetic — the card database is one in-test fake card inside a hand-wired
/// shopPoolRef; no scene assets, no network.
/// </summary>
public class OpponentSelectReservationTests
{
	private string tempDir;
	private ServerConfig config;
	private DeckSaver saver;
	private GameObject saverGo;
	private DeckSO enemyDeck;
	private PlayerStatusSO enemyStatus;
	private IntSO sessionNumber;
	private GameObject fakeCard;

	[SetUp]
	public void SetUp()
	{
		tempDir = Path.Combine(Path.GetTempPath(), "onedeck_opp_select_test_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempDir);
		OpponentDeckCache.OverrideDirectoryForTests = tempDir;
		OpponentDeckCache.ResetCacheForTests();

		config = ScriptableObject.CreateInstance<ServerConfig>();
		config.enabled = true;
		config.fetchOpponentDecks = true;
		ServerConfig.Active = config;

		enemyDeck = ScriptableObject.CreateInstance<DeckSO>();
		enemyDeck.deck = new List<GameObject>();
		enemyStatus = ScriptableObject.CreateInstance<PlayerStatusSO>();
		enemyStatus.hpMaxOg = 20;
		enemyStatus.ResetHpMax();
		sessionNumber = ScriptableObject.CreateInstance<IntSO>();
		sessionNumber.value = 3;

		fakeCard = new GameObject("FakeWolf");
		fakeCard.AddComponent<CardScript>().cardTypeID = "wolf";

		DeckSO shopPool = ScriptableObject.CreateInstance<DeckSO>();
		shopPool.deck = new List<GameObject> { fakeCard };

		saverGo = new GameObject("DeckSaver");
		saver = saverGo.AddComponent<DeckSaver>();
		saver.enemyDeckToPopulate = enemyDeck;
		saver.enemyStatusRef = enemyStatus;
		saver.sessionNumber = sessionNumber;
		saver.shopPoolRef = shopPool;
	}

	[TearDown]
	public void TearDown()
	{
		ServerConfig.Active = null;
		OpponentDeckCache.OverrideDirectoryForTests = null;
		OpponentDeckCache.ResetCacheForTests();
		if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
		if (config != null) UnityEngine.Object.DestroyImmediate(config);
		if (enemyDeck != null) UnityEngine.Object.DestroyImmediate(enemyDeck);
		if (enemyStatus != null) UnityEngine.Object.DestroyImmediate(enemyStatus);
		if (sessionNumber != null) UnityEngine.Object.DestroyImmediate(sessionNumber);
		if (fakeCard != null) UnityEngine.Object.DestroyImmediate(fakeCard);
		if (saverGo != null) UnityEngine.Object.DestroyImmediate(saverGo);
	}

	private static OpponentDeckEntry MakeDeck(int deckId, int sessionNum)
	{
		return new OpponentDeckEntry
		{
			deckId = deckId,
			sessionNum = sessionNum,
			username = "ghost" + deckId,
			cardTypeIDs = new List<string> { "wolf" },
			hpMax = 25
		};
	}

	[Test]
	public void Reservation_PopulatesPickedContent_AndClearsReservation()
	{
		OpponentDeckCache.InjectForTests(MakeDeck(1, 3));
		OpponentDeckCache.ConsumeCandidate(3, 1);

		saver.PopulateEnemyDeckBySessionNumber();

		Assert.AreEqual(1, enemyDeck.deck.Count);
		Assert.AreEqual(fakeCard, enemyDeck.deck[0]);
		Assert.AreEqual(25, enemyStatus.hpMax);
		Assert.AreEqual(25, enemyStatus.hp);
		Assert.AreEqual(1, OpponentDeckCache.Current.deckId);
		Assert.IsFalse(OpponentDeckCache.HasReservedEntry);
	}

	[Test]
	public void RepeatedPopulate_SameSession_KeepsPickedOpponent()
	{
		// The picked opponent must never change: after the reservation was consumed at
		// pick-populate, the leg2 EnterCombatPhase pass re-fills from Current (plan §4.2)
		// instead of taking a fresh candidate.
		OpponentDeckCache.InjectForTests(MakeDeck(1, 3));
		OpponentDeckCache.InjectForTests(MakeDeck(2, 3));
		OpponentDeckCache.ConsumeCandidate(3, 1);
		saver.PopulateEnemyDeckBySessionNumber();

		saver.PopulateEnemyDeckBySessionNumber();  // the idempotent second pass

		Assert.AreEqual(1, enemyDeck.deck.Count);
		Assert.AreEqual(fakeCard, enemyDeck.deck[0]);
		Assert.AreEqual(1, OpponentDeckCache.Current.deckId);
	}

	[Test]
	public void NoReservation_TakesFreshCandidateFromCache()
	{
		// Flow parity with today: without a pick, populate still walks the take chain.
		OpponentDeckCache.InjectForTests(MakeDeck(1, 3));

		saver.PopulateEnemyDeckBySessionNumber();

		Assert.AreEqual(1, enemyDeck.deck.Count);
		Assert.AreEqual(fakeCard, enemyDeck.deck[0]);
		Assert.AreEqual(1, OpponentDeckCache.Current.deckId);
	}

	[Test]
	public void NoReservation_NoCache_DoesNotTouchEnemyDeck()
	{
		saver.PopulateEnemyDeckBySessionNumber();

		Assert.AreEqual(0, enemyDeck.deck.Count);
		Assert.IsNull(OpponentDeckCache.Current);
	}
}
