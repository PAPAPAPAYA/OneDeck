using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Batch B EditMode tests (plan §4): opponent-cache take / discard / per-run dedup
/// semantics and the enemy-source telemetry counters. No test touches the network -
/// prefetch paths are exercised only with the master switch off.
/// </summary>
public class OpponentDeckCacheTests
{
	private string tempDir;
	private ServerConfig config;

	[SetUp]
	public void SetUp()
	{
		tempDir = Path.Combine(Path.GetTempPath(), "onedeck_opp_cache_test_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempDir);
		OpponentDeckCache.OverrideDirectoryForTests = tempDir;

		config = ScriptableObject.CreateInstance<ServerConfig>();
		config.enabled = true;
		config.fetchOpponentDecks = true;
		ServerConfig.Active = config;
	}

	[TearDown]
	public void TearDown()
	{
		ServerConfig.Active = null;
		OpponentDeckCache.OverrideDirectoryForTests = null;
		OpponentDeckCache.ResetCacheForTests();
		if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
		if (config != null) UnityEngine.Object.DestroyImmediate(config);
	}

	private static OpponentDeckEntry MakeDeck(int deckId, int sessionNum, string username = null)
	{
		return new OpponentDeckEntry
		{
			deckId = deckId,
			sessionNum = sessionNum,
			username = username ?? ("ghost" + deckId),
			cardTypeIDs = new List<string> { "wolf", "shrine" },
			hpMax = 25
		};
	}

	/// <summary>Wipes both the in-memory and the on-disk cache for a fresh round.</summary>
	private void ResetCacheDisk()
	{
		OpponentDeckCache.ResetCacheForTests();
		string cacheFile = Path.Combine(tempDir, "opponent_cache.json");
		if (File.Exists(cacheFile)) File.Delete(cacheFile);
	}

	[Test]
	public void TakeCandidate_SessionMatch_MarksUsedWithinRun()
	{
		OpponentDeckCache.InjectForTests(MakeDeck(1, 3));
		OpponentDeckCache.InjectForTests(MakeDeck(2, 3));

		// Selection is randomized, so only dedup is asserted order-independently:
		// two takes drain both candidates in any order, the third finds nothing.
		int first = OpponentDeckCache.TakeCandidate(3).deckId;
		int second = OpponentDeckCache.TakeCandidate(3).deckId;
		CollectionAssert.AreEquivalent(new[] { 1, 2 }, new[] { first, second });
		Assert.IsNull(OpponentDeckCache.TakeCandidate(3));
	}

	[Test]
	public void TakeCandidate_SessionMiss_ReturnsNull()
	{
		OpponentDeckCache.InjectForTests(MakeDeck(1, 2));
		Assert.IsNull(OpponentDeckCache.TakeCandidate(3));
	}

	[Test]
	public void TakeCandidate_RandomizesAmongSameSessionCandidates()
	{
		// Fixed seed keeps the run deterministic; across 40 fresh rounds the take must
		// land on a non-insertion-first slot at least once ((1/4)^40 odds against all-first).
		UnityEngine.Random.InitState(20260913);
		bool hitNonFirst = false;
		for (int round = 0; round < 40 && !hitNonFirst; round++)
		{
			ResetCacheDisk();
			for (int k = 1; k <= 4; k++) OpponentDeckCache.InjectForTests(MakeDeck(round * 10 + k, 3));
			if (OpponentDeckCache.TakeCandidate(3).deckId != round * 10 + 1) hitNonFirst = true;
		}
		Assert.IsTrue(hitNonFirst);
	}

	[Test]
	public void MergeResponse_IncludeSelfOff_PurgesLegacySelfDecks()
	{
		Assume.That(PlayerIdentity.HasIdentity, "ownership filter tests need a local identity");
		string selfName = PlayerIdentity.Username;

		// Leftovers from the fightOwnGhostsOnly era: own decks sitting in the cache.
		OpponentDeckCache.InjectForTests(MakeDeck(1, 3, selfName));
		OpponentDeckCache.InjectForTests(MakeDeck(2, 3, "someoneElse"));
		config.opponentsIncludeSelf = false;

		OpponentDecksResponse response = new OpponentDecksResponse
		{
			decks = new List<OpponentDeckEntry>
			{
				MakeDeck(3, 3, "someoneElse"),
				MakeDeck(4, 3, selfName)  // a misbehaving server: must be refused, not re-added
			}
		};
		OpponentDeckCache.MergeResponse(response);

		// Draining the session must yield exactly the two foreign decks: the legacy self
		// entry was purged and the response's self deck was skipped.
		List<int> taken = new List<int>();
		for (int i = 0; i < 3; i++)
		{
			OpponentDeckEntry entry = OpponentDeckCache.TakeCandidate(3);
			if (entry != null) taken.Add(entry.deckId);
		}
		CollectionAssert.AreEquivalent(new[] { 2, 3 }, taken);
	}

	[Test]
	public void MergeResponse_FlaggedDeckIds_PurgesCachedCopies()
	{
		// Prefetched before the flag existed: three ghosts are already in the disk cache...
		OpponentDeckCache.InjectForTests(MakeDeck(1, 3));
		OpponentDeckCache.InjectForTests(MakeDeck(2, 3));
		OpponentDeckCache.InjectForTests(MakeDeck(3, 3));

		// ...then a later prefetch reports deck 2 as flagged (plan §20.3) and brings a fresh ghost.
		OpponentDecksResponse response = new OpponentDecksResponse
		{
			decks = new List<OpponentDeckEntry> { MakeDeck(4, 3) },
			flaggedDeckIds = new List<int> { 2 }
		};
		OpponentDeckCache.MergeResponse(response);

		List<int> taken = new List<int>();
		for (int i = 0; i < 5; i++)
		{
			OpponentDeckEntry entry = OpponentDeckCache.TakeCandidate(3);
			if (entry != null) taken.Add(entry.deckId);
		}
		CollectionAssert.AreEquivalent(new[] { 1, 3, 4 }, taken,
			"the flagged deck is gone; everything else - including the newly fetched ghost - stays");
	}

	[Test]
	public void MergeResponse_FlagListAbsent_KeepsCacheIntact()
	{
		// An older server sends no flaggedDeckIds field at all: the purge must not throw or
		// empty the cache (JsonUtility leaves the list null for a response without the key).
		OpponentDeckCache.InjectForTests(MakeDeck(1, 3));
		OpponentDeckCache.MergeResponse(new OpponentDecksResponse { decks = new List<OpponentDeckEntry>() });
		Assert.AreEqual(1, OpponentDeckCache.TakeCandidate(3).deckId);
	}

	[Test]
	public void MergeResponse_IncludeSelfOn_KeepsSelfDecks()
	{
		Assume.That(PlayerIdentity.HasIdentity, "ownership filter tests need a local identity");
		string selfName = PlayerIdentity.Username;
		config.opponentsIncludeSelf = true;

		OpponentDecksResponse response = new OpponentDecksResponse
		{
			decks = new List<OpponentDeckEntry> { MakeDeck(1, 3, selfName) }
		};
		OpponentDeckCache.MergeResponse(response);

		OpponentDeckEntry kept = OpponentDeckCache.TakeCandidate(3);
		Assert.IsNotNull(kept);
		Assert.AreEqual(1, kept.deckId);
	}

	[Test]
	public void TakeCandidate_OnlyOwnDecks_RestrictsToSelfAndReturnsNullWhenDry()
	{
		Assume.That(PlayerIdentity.HasIdentity, "ownership filter tests need a local identity");
		string selfName = PlayerIdentity.Username;
		OpponentDeckCache.InjectForTests(MakeDeck(1, 3, "someoneElse"));
		OpponentDeckCache.InjectForTests(MakeDeck(2, 3, selfName));
		OpponentDeckCache.OnlyOwnDecks = true;

		Assert.AreEqual(2, OpponentDeckCache.TakeCandidate(3).deckId);
		// Own pool dry: the foreign entry stays untouchable and the take returns null
		// (DeckSaver cache-dry semantics: onlyGhostEnemyDeck clears the enemy deck).
		Assert.IsNull(OpponentDeckCache.TakeCandidate(3));
	}

	[Test]
	public void OnRunStarted_ClearsPerRunDedupAndStashedOpponent()
	{
		config.enabled = false;  // keep the prefetch inside OnRunStarted offline

		OpponentDeckCache.InjectForTests(MakeDeck(1, 3));
		OpponentDeckCache.SetCurrentOpponent(MakeDeck(1, 3));
		Assert.IsNotNull(OpponentDeckCache.TakeCandidate(3));

		OpponentDeckCache.OnRunStarted();

		Assert.IsNull(OpponentDeckCache.Current);
		Assert.IsNotNull(OpponentDeckCache.TakeCandidate(3));  // dedup reset: usable again
	}

	[Test]
	public void DiscardCandidate_RemovesWholeDeck()
	{
		OpponentDeckCache.InjectForTests(MakeDeck(1, 3));
		OpponentDeckCache.InjectForTests(MakeDeck(2, 3));

		OpponentDeckCache.DiscardCandidate(1);
		Assert.AreEqual(2, OpponentDeckCache.TakeCandidate(3).deckId);
		Assert.IsNull(OpponentDeckCache.TakeCandidate(3));
	}

	[Test]
	public void SetCurrentOpponent_StashesAndClears()
	{
		OpponentDeckCache.SetCurrentOpponent(MakeDeck(7, 1));
		Assert.AreEqual(7, OpponentDeckCache.Current.deckId);
		Assert.AreEqual("ghost7", OpponentDeckCache.Current.username);

		OpponentDeckCache.SetCurrentOpponent(null);
		Assert.IsNull(OpponentDeckCache.Current);
	}

	[Test]
	public void RecordEnemySource_CountsAndPersistsAcrossReload()
	{
		OpponentDeckCache.RecordEnemySource(OpponentDeckCache.SourceServer);
		OpponentDeckCache.RecordEnemySource(OpponentDeckCache.SourceServer);
		OpponentDeckCache.RecordEnemySource(OpponentDeckCache.SourcePool);

		OpponentDeckCache.ResetCacheForTests();  // next read must come from disk

		OpponentDeckCache.EnemySourceCounters counters = OpponentDeckCache.SourceCounters;
		Assert.AreEqual(2, counters.server);
		Assert.AreEqual(0, counters.local);
		Assert.AreEqual(1, counters.pool);
	}

	[Test]
	public void StagedEnemySource_CountsOnlyAfterCommit()
	{
		// Staging is memory-only: a reload (e.g. a new run's cache read) drops it.
		OpponentDeckCache.StageEnemySource(OpponentDeckCache.SourceServer);
		OpponentDeckCache.ResetCacheForTests();
		Assert.AreEqual(0, OpponentDeckCache.SourceCounters.server);

		// Only the settlement commit reaches the lifetime counters.
		OpponentDeckCache.StageEnemySource(OpponentDeckCache.SourceServer);
		OpponentDeckCache.CommitStagedEnemySource();
		Assert.AreEqual(1, OpponentDeckCache.SourceCounters.server);

		// Commit consumes the staged source; a second settlement never double-counts.
		OpponentDeckCache.CommitStagedEnemySource();
		Assert.AreEqual(1, OpponentDeckCache.SourceCounters.server);
	}

	[Test]
	public void FetchEnabled_FollowsMasterAndPerKindSwitch()
	{
		Assert.IsTrue(OpponentDeckCache.FetchEnabled);

		config.enabled = false;
		Assert.IsFalse(OpponentDeckCache.FetchEnabled);

		config.enabled = true;
		config.fetchOpponentDecks = false;
		Assert.IsFalse(OpponentDeckCache.FetchEnabled);
	}

	[Test]
	public void CacheFile_PersistsAcrossReload()
	{
		OpponentDeckCache.InjectForTests(MakeDeck(5, 1));
		Assert.AreEqual(5, OpponentDeckCache.TakeCandidate(1).deckId);

		OpponentDeckCache.ResetCacheForTests();
		// usedDeckIds also round-trip: the taken deck stays used after a reload
		Assert.IsNull(OpponentDeckCache.TakeCandidate(1));
	}
}
