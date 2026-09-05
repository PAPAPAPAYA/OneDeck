using System;
using System.IO;
using DefaultNamespace.Managers;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Per-visit staging of shop stats (2026-09-05 no-combat-run exclusion): Record* calls
/// land in a staging buffer and only merge into the lifetime counters on
/// CommitStagedVisit. Files are redirected to a temp dir via
/// ShopStatsManager.OverrideDirectoryForTests so the real shop_stats.json is untouched.
/// </summary>
public class ShopStatsManagerTests
{
	private string tempDir;
	private ShopStatsManager manager;

	[SetUp]
	public void SetUp()
	{
		tempDir = Path.Combine(Path.GetTempPath(), "onedeck_shop_stats_test_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempDir);
		ShopStatsManager.OverrideDirectoryForTests = tempDir;
		StatsSnapshotUploader.Dirty = false;

		var go = new GameObject("ShopStatsManagerTests");
		manager = go.AddComponent<ShopStatsManager>();
	}

	[TearDown]
	public void TearDown()
	{
		if (manager != null) UnityEngine.Object.DestroyImmediate(manager.gameObject);
		ShopStatsManager.Me = null;
		ShopStatsManager.OverrideDirectoryForTests = null;
		StatsSnapshotUploader.Dirty = false;
		if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
	}

	[Test]
	public void Records_StayStaged_UntilCommit()
	{
		manager.RecordShopVisit();
		manager.RecordCardAppeared("wolf", "Wolf");
		manager.RecordCardBought("wolf", "Wolf", true);
		manager.RecordReroll();

		Assert.AreEqual(0, manager.GetTotalShopVisitsForUpload(), "nothing reaches the lifetime counters before CommitStagedVisit");
		Assert.AreEqual(0, manager.GetTotalRerollsForUpload());
		Assert.AreEqual(0, manager.GetSessionStatsForUpload().Count);
		Assert.IsFalse(StatsSnapshotUploader.Dirty, "staging must not arm the upload dirty flag");
	}

	[Test]
	public void CommitStagedVisit_MergesIntoLifetimeCounters()
	{
		manager.RecordShopVisit();
		manager.RecordCardAppeared("wolf", "Wolf");
		manager.RecordCardAppeared("wolf", "Wolf");
		manager.RecordCardBought("wolf", "Wolf", true);
		manager.RecordReroll();
		manager.RecordReroll();

		manager.CommitStagedVisit();

		Assert.AreEqual(1, manager.GetTotalShopVisitsForUpload());
		Assert.AreEqual(2, manager.GetTotalRerollsForUpload());
		ShopSessionStats bucket = manager.GetSessionStatsForUpload().Find(s => s.cardTypeID == "wolf");
		Assert.IsNotNull(bucket);
		Assert.AreEqual(2, bucket.appear);
		Assert.AreEqual(1, bucket.bought);
		Assert.AreEqual(1, bucket.utilBought);
		Assert.AreEqual(0, bucket.utilAppear);
		Assert.IsTrue(StatsSnapshotUploader.Dirty, "commit arms the upload dirty flag");

		// committing again with nothing staged must be a no-op
		StatsSnapshotUploader.Dirty = false;
		manager.CommitStagedVisit();
		Assert.AreEqual(1, manager.GetTotalShopVisitsForUpload(), "second commit with an empty buffer must not double count");
		Assert.IsFalse(StatsSnapshotUploader.Dirty);
	}

	[Test]
	public void TwoVisits_AccumulateAcrossCommits()
	{
		manager.RecordCardAppeared("wolf", "Wolf");
		manager.CommitStagedVisit();
		manager.RecordCardAppeared("wolf", "Wolf");
		manager.RecordCardBought("wolf", "Wolf");
		manager.CommitStagedVisit();

		CardShopStats stat = manager.GetCardStats("wolf");
		Assert.IsNotNull(stat);
		Assert.AreEqual(2, stat.appearCount);
		Assert.AreEqual(1, stat.boughtCount);
	}

	[Test]
	public void StagedOnlyData_IsNotPersistedOnFlush()
	{
		manager.RecordShopVisit();
		manager.RecordCardAppeared("wolf", "Wolf");
		manager.Flush();

		Assert.IsFalse(File.Exists(Path.Combine(tempDir, "shop_stats.json")), "a staged-only visit must not reach the stats file");
	}
}
