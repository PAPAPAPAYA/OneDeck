using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Batch A EditMode tests (plan §4): DTO serialization round-trips, outbox
/// enqueue / cap / discard semantics, and the per-kind switch matrix.
/// No test touches the network.
/// </summary>
public class NetClientBatchATests
{
	private string outboxPath;
	private ServerConfig config;

	[SetUp]
	public void SetUp()
	{
		outboxPath = Path.Combine(Path.GetTempPath(), "onedeck_outbox_test_" + Guid.NewGuid().ToString("N") + ".json");
		UploadOutbox.OverrideFilePathForTests = outboxPath;

		config = ScriptableObject.CreateInstance<ServerConfig>();
		config.enabled = true;
		ServerConfig.Active = config;
	}

	[TearDown]
	public void TearDown()
	{
		UploadOutbox.DiscardAll();
		UploadOutbox.OverrideFilePathForTests = null;
		ServerConfig.Active = null;
		if (config != null) UnityEngine.Object.DestroyImmediate(config);
	}

	// --- DTO round-trips ---

	[Test]
	public void DeckUploadRequest_RoundTrip_KeepsFieldsAndCasing()
	{
		DeckUploadRequest request = new DeckUploadRequest
		{
			playerId = "pid-1",
			gameVersion = "0.1.0",
			sessionNum = 3,
			hpMax = 30,
			winAmount = 5,
			heartLeft = 2,
			cardTypeIDs = new List<string> { "wolf", "shrine" }
		};
		string json = JsonUtility.ToJson(request);
		StringAssert.Contains("\"cardTypeIDs\"", json);
		StringAssert.Contains("\"hpMax\":30", json);

		DeckUploadRequest parsed = JsonUtility.FromJson<DeckUploadRequest>(json);
		Assert.AreEqual("pid-1", parsed.playerId);
		Assert.AreEqual(3, parsed.sessionNum);
		Assert.AreEqual(new[] { "wolf", "shrine" }, parsed.cardTypeIDs.ToArray());
	}

	[Test]
	public void RunUploadRequest_RoundTrip_KeepsNestedCombatFields()
	{
		RunUploadRequest request = new RunUploadRequest
		{
			playerId = "pid-1",
			runId = "run-1",
			gameVersion = "0.1.0",
			result = "victory",
			finalSession = 5,
			heartsLeft = 3,
			finalDeck = new List<string> { "wolf" },
			seenPoolPct = 0.42f,
			combats = new List<RunCombatEntry>
			{
				new RunCombatEntry
				{
					sessionNum = 3,
					won = true,
					heartsLeft = 4,
					rounds = 7,
					opponentDeckId = 12,
					perCard = new List<RunCombatPerCard>
					{
						new RunCombatPerCard { cardTypeID = "wolf", triggers = 3, damageToOpponent = 12, damageToSelf = 2 }
					}
				}
			}
		};
		RunCombatEntry combat = JsonUtility.FromJson<RunUploadRequest>(JsonUtility.ToJson(request)).combats[0];
		Assert.AreEqual(7, combat.rounds);
		Assert.AreEqual(12, combat.opponentDeckId);
		Assert.IsTrue(combat.won);
		Assert.AreEqual(2, combat.perCard[0].damageToSelf);
	}

	[Test]
	public void StatsSnapshotRequest_IncludesEnemySourceCounters()
	{
		StatsSnapshotRequest request = new StatsSnapshotRequest
		{
			playerId = "pid-1",
			gameVersion = "0.1.0",
			meta = new StatsMeta
			{
				totalShopVisits = 9,
				totalRerolls = 4,
				enemySource = new StatsEnemySource { server = 6, local = 2, pool = 1 }
			}
		};
		string json = JsonUtility.ToJson(request);
		StringAssert.Contains("\"enemySource\":{\"server\":6,\"local\":2,\"pool\":1}", json);
	}

	// --- ServerConfig ---

	[Test]
	public void BaseUrl_FollowsEnvironmentAndTrimsTrailingSlash()
	{
		config.environment = ServerEnvironment.Local;
		config.localBaseUrl = "http://127.0.0.1:3000/";
		Assert.AreEqual("http://127.0.0.1:3000", config.BaseUrl);

		config.environment = ServerEnvironment.Production;
		config.productionBaseUrl = "http://8.153.150.197";
		Assert.AreEqual("http://8.153.150.197", config.BaseUrl);
	}

	// --- Switch matrix: a disabled kind is never captured (plan §3.1) ---

	[Test]
	public void Enqueue_MasterSwitchOff_NeverQueues()
	{
		config.enabled = false;
		UploadOutbox.Enqueue(NetUploadKind.DeckSnapshot, new DeckUploadRequest());
		Assert.AreEqual(0, UploadOutbox.PendingCount);
	}

	[Test]
	public void Enqueue_PerKindSwitchOff_NeverQueues()
	{
		config.uploadRunRecords = false;
		UploadOutbox.Enqueue(NetUploadKind.RunRecord, new RunUploadRequest());
		Assert.AreEqual(0, UploadOutbox.PendingCount);

		config.uploadRunRecords = true;
		UploadOutbox.Enqueue(NetUploadKind.RunRecord, new RunUploadRequest());
		Assert.AreEqual(1, UploadOutbox.PendingCount);
	}

	[Test]
	public void IsUploadEnabled_MapsEveryKindToItsSwitch()
	{
		ToggleAll(false);
		Assert.IsFalse(UploadOutbox.IsUploadEnabled(NetUploadKind.DeckSnapshot));
		Assert.IsFalse(UploadOutbox.IsUploadEnabled(NetUploadKind.MatchReport));
		Assert.IsFalse(UploadOutbox.IsUploadEnabled(NetUploadKind.StatsSnapshot));
		Assert.IsFalse(UploadOutbox.IsUploadEnabled(NetUploadKind.CardCatalog));

		ToggleAll(true);
		Assert.IsTrue(UploadOutbox.IsUploadEnabled(NetUploadKind.DeckSnapshot));
		Assert.IsTrue(UploadOutbox.IsUploadEnabled(NetUploadKind.CardCatalog));
		Assert.AreEqual("/api/decks", UploadOutbox.EndpointFor(NetUploadKind.DeckSnapshot));
		Assert.AreEqual("/api/runs", UploadOutbox.EndpointFor(NetUploadKind.RunRecord));
	}

	// --- Outbox queue semantics (plan §2.3) ---

	[Test]
	public void Enqueue_PersistsAcrossReload()
	{
		UploadOutbox.EnqueueRaw("RunRecord", "/api/runs", "itemA");
		Assert.AreEqual(1, UploadOutbox.PendingCount);

		UploadOutbox.ResetCacheForTests();  // next read must come from disk, not memory
		Assert.AreEqual(1, UploadOutbox.PendingCount);
	}

	[Test]
	public void Enqueue_CapOverflow_DropsOldest()
	{
		for (int i = 0; i < UploadOutbox.MaxItems + 2; i++)
		{
			UploadOutbox.EnqueueRaw("RunRecord", "/api/runs", "item" + i);
		}
		Assert.AreEqual(UploadOutbox.MaxItems, UploadOutbox.PendingCount);
	}

	[Test]
	public void DiscardAll_RemovesFileAndCache()
	{
		UploadOutbox.EnqueueRaw("MatchReport", "/api/matches/report", "payload");
		Assert.AreEqual(1, UploadOutbox.PendingCount);

		UploadOutbox.DiscardAll();
		Assert.AreEqual(0, UploadOutbox.PendingCount);
		Assert.IsFalse(File.Exists(outboxPath));
	}

	// --- PlayerIdentity ---

	[Test]
	public void Register_UsernameTooShort_FailsSynchronouslyWithoutNetwork()
	{
		bool called = false;
		PlayerIdentity.Register("x", (ok, message) =>
		{
			called = true;
			Assert.IsFalse(ok);
			Assert.AreEqual("invalid_username", message);
		});
		Assert.IsTrue(called);
	}

	[Test]
	public void ApplyTestPrefix_FollowsMarkAsTestSwitch()
	{
		config.markAsTest = true;
		Assert.AreEqual("test_smoke", PlayerIdentity.ApplyTestPrefix("smoke"));
		Assert.AreEqual("test_smoke", PlayerIdentity.ApplyTestPrefix("test_smoke"));  // idempotent

		config.markAsTest = false;
		Assert.AreEqual("smoke", PlayerIdentity.ApplyTestPrefix("smoke"));
	}

	private void ToggleAll(bool value)
	{
		config.uploadDeckSnapshots = value;
		config.uploadMatchReports = value;
		config.uploadStatsSnapshots = value;
		config.uploadRunRecords = value;
		config.uploadCardCatalog = value;
	}
}
