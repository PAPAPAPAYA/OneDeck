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

	private static OpponentDeckEntry MakeDeck(int deckId, int sessionNum)
	{
		return new OpponentDeckEntry
		{
			deckId = deckId,
			sessionNum = sessionNum,
			username = "ghost" + deckId,
			cardTypeIDs = new List<string> { "wolf", "shrine" },
			hpMax = 25
		};
	}

	[Test]
	public void TakeCandidate_SessionMatch_MarksUsedWithinRun()
	{
		OpponentDeckCache.InjectForTests(MakeDeck(1, 3));
		OpponentDeckCache.InjectForTests(MakeDeck(2, 3));

		Assert.AreEqual(1, OpponentDeckCache.TakeCandidate(3).deckId);
		// Same run: the used deck never comes back; the other candidate does.
		Assert.AreEqual(2, OpponentDeckCache.TakeCandidate(3).deckId);
		Assert.IsNull(OpponentDeckCache.TakeCandidate(3));
	}

	[Test]
	public void TakeCandidate_SessionMiss_ReturnsNull()
	{
		OpponentDeckCache.InjectForTests(MakeDeck(1, 2));
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
