using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DefaultNamespace.Managers;
using NUnit.Framework;
using TestWriteRead;
using UnityEngine;

/// <summary>
/// Draw-aware card win-rate stats (plans/plan-forced-draw-reporting-2026-11-26.md §5.5):
/// win / loss / natural / forced counts for both player and enemy cards (enemy inverts only
/// Win &lt;-&gt; Loss), Abnormal records nothing, legacy JSON loads the new counters as 0, the CSV
/// header and rows have the same column count (the old header had a CardName column the rows
/// never emitted), and the snapshot upload mapping carries the draw counters.
/// The tracker's Awake never runs in Edit Mode, so the data/file paths are injected via
/// reflection into a temp dir (nothing touches the real persistentDataPath stats).
/// </summary>
public class CardWinRateDrawTests : HeadlessCombatTestFixture
{
	private string tempDir;
	private CardWinRateTracker tracker;

	[SetUp]
	public override void SetUp()
	{
		base.SetUp();
		tempDir = Path.Combine(Path.GetTempPath(), "onedeck_cardwinrate_draw_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempDir);

		var trackerObj = CreateGameObject("TestCardWinRateTracker");
		tracker = trackerObj.AddComponent<CardWinRateTracker>();
		tracker.switchOnTracking = true;
		SetPrivateField(tracker, "_data", new CardWinRateData());
		SetPrivateField(tracker, "_jsonPath", Path.Combine(tempDir, "card_winrate.json"));
		SetPrivateField(tracker, "_csvPath", Path.Combine(tempDir, "card_winrate.csv"));
		CardWinRateTracker.Me = tracker;
		StatsSnapshotUploader.Dirty = false;
	}

	[TearDown]
	public override void TearDown()
	{
		CardWinRateTracker.Me = null;
		StatsSnapshotUploader.Dirty = false;
		if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
		base.TearDown();
	}

	private static void SetPrivateField(object target, string name, object value)
	{
		typeof(CardWinRateTracker)
			.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
			.SetValue(target, value);
	}

	/// <summary>Deck snapshots: one player card + one enemy card, recorded like GatherDecks does.</summary>
	private void RecordDecks()
	{
		var playerPrefab = CreateCard(true, "Wolf", "P_WOLF");
		var enemyPrefab = CreateCard(false, "Shrine", "E_SHRINE");
		tracker.RecordPlayerDeckSnapshot(new List<GameObject> { playerPrefab });
		tracker.RecordEnemyDeckSnapshot(new List<GameObject> { enemyPrefab });
	}

	[Test]
	public void Win_AndLoss_CountPlayerAsIs_AndInvertEnemy()
	{
		RecordDecks();

		tracker.RecordCombatResult(CombatOutcome.Win, 1);
		tracker.RecordCombatResult(CombatOutcome.Loss, 1);

		CardStats player = tracker.GetCardStats("P_WOLF");
		Assert.AreEqual(2, player.totalCombats);
		Assert.AreEqual(1, player.wins);
		Assert.AreEqual(1, player.losses);
		Assert.AreEqual(0, player.drawsNatural);

		CardStats enemy = tracker.GetCardStats("E_SHRINE");
		Assert.AreEqual(2, enemy.totalCombats);
		Assert.AreEqual(1, enemy.wins, "enemy inverts: the player's loss is the enemy's win");
		Assert.AreEqual(1, enemy.losses, "enemy inverts: the player's win is the enemy's loss");
	}

	[Test]
	public void Draws_CountBothSides_ByKind()
	{
		RecordDecks();

		tracker.RecordCombatResult(CombatOutcome.DrawNatural, 1);
		tracker.RecordCombatResult(CombatOutcome.DrawForced, 1);

		CardStats player = tracker.GetCardStats("P_WOLF");
		Assert.AreEqual(2, player.totalCombats, "draws count into combats");
		Assert.AreEqual(0, player.wins);
		Assert.AreEqual(0, player.losses);
		Assert.AreEqual(1, player.drawsNatural);
		Assert.AreEqual(1, player.drawsForced);

		CardStats enemy = tracker.GetCardStats("E_SHRINE");
		Assert.AreEqual(2, enemy.totalCombats);
		Assert.AreEqual(0, enemy.wins);
		Assert.AreEqual(0, enemy.losses);
		Assert.AreEqual(1, enemy.drawsNatural, "both sides get the matching draw counter");
		Assert.AreEqual(1, enemy.drawsForced);

		// The snapshot buckets carry the same split (StatsSnapshotUploader reads these).
		var buckets = tracker.GetSessionStatsForUpload();
		Assert.AreEqual(2, buckets.Count);
		foreach (SessionCardStats bucket in buckets)
		{
			Assert.AreEqual(2, bucket.combats);
			Assert.AreEqual(1, bucket.drawsNatural);
			Assert.AreEqual(1, bucket.drawsForced);
		}
	}

	[Test]
	public void Abnormal_RecordsNothing()
	{
		RecordDecks();

		tracker.RecordCombatResult(CombatOutcome.Abnormal, 1);

		Assert.IsNull(tracker.GetCardStats("P_WOLF"), "Abnormal must not create player rows");
		Assert.IsNull(tracker.GetCardStats("E_SHRINE"), "Abnormal must not create enemy rows");
		Assert.AreEqual(0, tracker.GetSessionStatsForUpload().Count);
	}

	[Test]
	public void SessionBuckets_SplitBySession_AndCarryDraws()
	{
		RecordDecks();

		tracker.RecordCombatResult(CombatOutcome.DrawNatural, 1);
		tracker.RecordCombatResult(CombatOutcome.DrawNatural, 2);

		var session1 = tracker.GetSessionStatsForUpload().Find(s => s.sessionNum == 1);
		var session2 = tracker.GetSessionStatsForUpload().Find(s => s.sessionNum == 2);
		Assert.IsNotNull(session1);
		Assert.IsNotNull(session2);
		Assert.AreEqual(1, session1.drawsNatural);
		Assert.AreEqual(1, session2.drawsNatural);
	}

	[Test]
	public void LegacyJson_LoadsWithZeroDraws()
	{
		string legacy = "{\"allCardStats\":[{\"cardTypeID\":\"old_card\",\"totalCombats\":5,\"wins\":2,\"losses\":3}]," +
			"\"sessionCardStats\":[{\"cardTypeID\":\"old_card\",\"sessionNum\":1,\"combats\":5,\"wins\":2,\"losses\":3}],\"lastUpdated\":\"x\"}";

		CardWinRateData data = JsonUtility.FromJson<CardWinRateData>(legacy);

		Assert.IsNotNull(data);
		Assert.AreEqual(1, data.allCardStats.Count);
		Assert.AreEqual(5, data.allCardStats[0].totalCombats);
		Assert.AreEqual(0, data.allCardStats[0].drawsNatural, "missing JSON fields must load as 0");
		Assert.AreEqual(0, data.allCardStats[0].drawsForced);
		Assert.AreEqual(0, data.sessionCardStats[0].drawsNatural);
		Assert.AreEqual(0, data.sessionCardStats[0].drawsForced);
	}

	[Test]
	public void CsvExport_HeaderAndRows_HaveTheSameColumnCount()
	{
		RecordDecks();
		tracker.RecordCombatResult(CombatOutcome.DrawForced, 1);
		tracker.ExportToCSV();

		string csvPath = Path.Combine(tempDir, "card_winrate.csv");
		Assert.IsTrue(File.Exists(csvPath), "the CSV must be written to the injected temp path");
		string[] lines = File.ReadAllLines(csvPath);
		Assert.GreaterOrEqual(lines.Length, 2, "header + at least one row");
		int headerColumns = lines[0].Split(',').Length;
		Assert.IsFalse(lines[0].Contains("CardName"),
			"the header-only CardName column is gone (rows never emitted it)");
		Assert.IsTrue(lines[0].Contains("DrawsNatural") && lines[0].Contains("DrawsForced"),
			"the header carries both draw columns: " + lines[0]);
		for (int i = 1; i < lines.Length; i++)
		{
			Assert.AreEqual(headerColumns, lines[i].Split(',').Length,
				"row " + i + " must have the same column count as the header");
		}
	}

	[Test]
	public void BuildRequest_MapsTheDrawCountersIntoTheWinrateRows()
	{
		var buckets = new List<SessionCardStats>
		{
			new SessionCardStats { cardTypeID = "P_WOLF", sessionNum = 1, combats = 4, wins = 2, losses = 1, drawsNatural = 1, drawsForced = 0 }
		};

		StatsSnapshotRequest request = StatsSnapshotUploader.BuildRequest(
			"pid", "v1", null, buckets, 0, 0, null);

		Assert.AreEqual(1, request.winrate.Count);
		Assert.AreEqual(4, request.winrate[0].combats);
		Assert.AreEqual(1, request.winrate[0].drawsNatural);
		Assert.AreEqual(0, request.winrate[0].drawsForced);
	}
}
