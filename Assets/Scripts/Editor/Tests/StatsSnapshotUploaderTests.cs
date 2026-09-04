using System.Collections.Generic;
using DefaultNamespace.Managers;
using NUnit.Framework;
using TestWriteRead;
using UnityEngine;

/// <summary>
/// Batch C EditMode tests (plan §4): stats snapshot bucket-to-row mapping, request
/// shape, and the dirty-flag guards. No test touches the network (guards keep
/// UploadIfDirty from ever reaching DeckNetworkClient here).
/// </summary>
public class StatsSnapshotUploaderTests
{
	private ServerConfig config;

	[SetUp]
	public void SetUp()
	{
		config = ScriptableObject.CreateInstance<ServerConfig>();
		config.enabled = true;
		config.uploadStatsSnapshots = true;
		ServerConfig.Active = config;
		StatsSnapshotUploader.Dirty = false;
	}

	[TearDown]
	public void TearDown()
	{
		StatsSnapshotUploader.Dirty = false;
		ServerConfig.Active = null;
		if (config != null) UnityEngine.Object.DestroyImmediate(config);
	}

	[Test]
	public void BuildRequest_MapsShopAndWinrateBuckets()
	{
		var shopBuckets = new List<ShopSessionStats>
		{
			new ShopSessionStats { cardTypeID = "wolf", sessionNum = 3, appear = 4, bought = 2, utilAppear = 1, utilBought = 1 },
			new ShopSessionStats { cardTypeID = "shrine", sessionNum = 0, appear = 1, bought = 0, utilAppear = 0, utilBought = 0 },
			null
		};
		var winrateBuckets = new List<SessionCardStats>
		{
			new SessionCardStats { cardTypeID = "wolf", sessionNum = 3, combats = 5, wins = 3, losses = 2 },
			null
		};

		StatsSnapshotRequest request = StatsSnapshotUploader.BuildRequest(
			"pid-1", "0.1.0", shopBuckets, winrateBuckets, 9, 4,
			new OpponentDeckCache.EnemySourceCounters { server = 6, local = 2, pool = 1 });

		Assert.AreEqual(2, request.shop.Count);
		Assert.AreEqual("wolf", request.shop[0].cardTypeID);
		Assert.AreEqual(3, request.shop[0].sessionNum);
		Assert.AreEqual(4, request.shop[0].appear);
		Assert.AreEqual(2, request.shop[0].bought);
		Assert.AreEqual(1, request.shop[0].utilAppear);
		Assert.AreEqual(1, request.shop[0].utilBought);

		Assert.AreEqual(1, request.winrate.Count);
		Assert.AreEqual("wolf", request.winrate[0].cardTypeID);
		Assert.AreEqual(5, request.winrate[0].combats);
		Assert.AreEqual(3, request.winrate[0].wins);
		Assert.AreEqual(2, request.winrate[0].losses);

		Assert.AreEqual(9, request.meta.totalShopVisits);
		Assert.AreEqual(4, request.meta.totalRerolls);
		Assert.AreEqual(6, request.meta.enemySource.server);
		Assert.AreEqual(2, request.meta.enemySource.local);
		Assert.AreEqual(1, request.meta.enemySource.pool);
	}

	[Test]
	public void BuildRequest_NullInputs_ProduceEmptyRowsAndZeroSource()
	{
		StatsSnapshotRequest request = StatsSnapshotUploader.BuildRequest("pid-1", "0.1.0", null, null, 0, 0, null);

		Assert.AreEqual(0, request.shop.Count);
		Assert.AreEqual(0, request.winrate.Count);
		Assert.AreEqual(0, request.meta.enemySource.server);
	}

	[Test]
	public void BuildRequest_JsonContainsServerFieldNames()
	{
		var shopBuckets = new List<ShopSessionStats>
		{
			new ShopSessionStats { cardTypeID = "wolf", sessionNum = 1, appear = 2, bought = 1 }
		};
		StatsSnapshotRequest request = StatsSnapshotUploader.BuildRequest(
			"pid-1", "0.1.0", shopBuckets, new List<SessionCardStats>(), 3, 1,
			new OpponentDeckCache.EnemySourceCounters());
		string json = JsonUtility.ToJson(request);

		StringAssert.Contains("\"cardTypeID\":\"wolf\"", json);
		StringAssert.Contains("\"utilAppear\":0", json);
		StringAssert.Contains("\"enemySource\"", json);
	}

	[Test]
	public void DirtyFlag_GuardsKeepDirtyWithoutNetwork()
	{
		StatsSnapshotUploader.MarkDirty();
		Assert.IsTrue(StatsSnapshotUploader.Dirty);

		// Master switch off: UploadIfDirty must bail before touching any network object.
		config.enabled = false;
		StatsSnapshotUploader.UploadIfDirty();
		Assert.IsTrue(StatsSnapshotUploader.Dirty);

		// Per-kind switch off: same.
		config.enabled = true;
		config.uploadStatsSnapshots = false;
		StatsSnapshotUploader.UploadIfDirty();
		Assert.IsTrue(StatsSnapshotUploader.Dirty);

		// Everything on, but no DeckNetworkClient host exists in EditMode: still dirty.
		config.uploadStatsSnapshots = true;
		StatsSnapshotUploader.UploadIfDirty();
		Assert.IsTrue(StatsSnapshotUploader.Dirty);
	}
}
