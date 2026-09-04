using System.Collections.Generic;
using DefaultNamespace.Managers;
using TestWriteRead;
using UnityEngine;

/// <summary>
/// Builds and uploads the lifetime cumulative stats snapshot (plan §2.7).
/// Counter-shaped upload: never queued in the outbox - on shop exit, when dirty, the
/// latest full bucket state is POSTed directly and the server upserts (replace) it.
/// Failed uploads only re-arm the dirty flag; the next trigger retries with fresh data.
/// </summary>
public static class StatsSnapshotUploader
{
	/// <summary>Set by the trackers on every record; cleared only after a successful upload.</summary>
	public static bool Dirty;

	public static void MarkDirty()
	{
		Dirty = true;
	}

	/// <summary>
	/// Current session number, read from the shared sessionNumber IntSO wired on DeckSaver.
	/// Combat results are recorded before the session increments, so this is the session
	/// being shopped for / just fought.
	/// </summary>
	public static int CurrentSessionNum()
	{
		if (DeckSaver.Me != null && DeckSaver.Me.sessionNumber != null)
		{
			return DeckSaver.Me.sessionNumber.value;
		}
		return 0;
	}

	/// <summary>Shop-exit trigger: uploads when dirty; silently skips every offline case.</summary>
	public static void UploadIfDirty()
	{
		if (!Dirty) return;
		ServerConfig config = ServerConfig.Active;
		if (config == null || !config.enabled || !config.uploadStatsSnapshots) return;
		if (!PlayerIdentity.HasIdentity) return;
		if (!DeckNetworkClient.HasInstance) return;

		Dirty = false;
		string json = JsonUtility.ToJson(BuildRequest(
			PlayerIdentity.PlayerId,
			DeckNetworkClient.GameVersion,
			ShopStatsManager.Me != null ? ShopStatsManager.Me.GetSessionStatsForUpload() : null,
			CardWinRateTracker.Me != null ? CardWinRateTracker.Me.GetSessionStatsForUpload() : null,
			ShopStatsManager.Me != null ? ShopStatsManager.Me.GetTotalShopVisitsForUpload() : 0,
			ShopStatsManager.Me != null ? ShopStatsManager.Me.GetTotalRerollsForUpload() : 0,
			OpponentDeckCache.SourceCounters));
		DeckNetworkClient.Me.PostJson("/api/stats/snapshot", json,
			onOk: null,
			onFail: (error, statusCode) => Dirty = true);
	}

	/// <summary>Pure mapping for tests and upload - tracker buckets to wire DTOs.</summary>
	public static StatsSnapshotRequest BuildRequest(
		string playerId,
		string gameVersion,
		List<ShopSessionStats> shopBuckets,
		List<SessionCardStats> winrateBuckets,
		int totalShopVisits,
		int totalRerolls,
		OpponentDeckCache.EnemySourceCounters sourceCounters)
	{
		StatsSnapshotRequest request = new StatsSnapshotRequest
		{
			playerId = playerId,
			gameVersion = gameVersion,
			shop = new List<StatsShopRow>(),
			winrate = new List<StatsWinrateRow>(),
			meta = new StatsMeta
			{
				totalShopVisits = totalShopVisits,
				totalRerolls = totalRerolls,
				enemySource = new StatsEnemySource
				{
					server = sourceCounters != null ? sourceCounters.server : 0,
					local = sourceCounters != null ? sourceCounters.local : 0,
					pool = sourceCounters != null ? sourceCounters.pool : 0
				}
			}
		};
		if (shopBuckets != null)
		{
			foreach (ShopSessionStats bucket in shopBuckets)
			{
				if (bucket == null) continue;
				request.shop.Add(new StatsShopRow
				{
					cardTypeID = bucket.cardTypeID,
					sessionNum = bucket.sessionNum,
					appear = bucket.appear,
					bought = bucket.bought,
					utilAppear = bucket.utilAppear,
					utilBought = bucket.utilBought
				});
			}
		}
		if (winrateBuckets != null)
		{
			foreach (SessionCardStats bucket in winrateBuckets)
			{
				if (bucket == null) continue;
				request.winrate.Add(new StatsWinrateRow
				{
					cardTypeID = bucket.cardTypeID,
					sessionNum = bucket.sessionNum,
					combats = bucket.combats,
					wins = bucket.wins,
					losses = bucket.losses
				});
			}
		}
		return request;
	}
}
