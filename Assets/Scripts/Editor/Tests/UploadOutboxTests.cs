using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// UploadOutbox flush-failure classification (2026-09-20 review fix): the queue historically
/// stopped at the first failed send and retried the same head forever. A PERMANENT rejection
/// (HTTP 4xx — /api/loop-reports answers 401 unknown_player / 400 own_deck / 404 deck_not_found)
/// can never succeed on retry, so one such entry stalled every later upload behind it. 4xx now
/// drops the poison head (with a warning) and continues; transient failures (transport error,
/// 5xx) keep the stop-and-retry-later behavior so ordering is preserved.
/// Sends are faked through UploadOutbox.SendOverrideForTests and the flush is pumped through
/// UploadOutbox.FlushSynchronouslyForTests: Edit Mode has no coroutine pump, and Flush() would
/// lazily create DeckNetworkClient in the active scene.
/// </summary>
public class UploadOutboxTests
{
	private string tempDir;

	[SetUp]
	public void SetUp()
	{
		tempDir = Path.Combine(Path.GetTempPath(), "onedeck_outbox_test_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempDir);
		UploadOutbox.OverrideFilePathForTests = Path.Combine(tempDir, "outbox.json");
		UploadOutbox.DiscardAll();
		UploadOutbox.ResetCacheForTests();
		UploadOutbox.SendOverrideForTests = null;
	}

	[TearDown]
	public void TearDown()
	{
		UploadOutbox.SendOverrideForTests = null;
		UploadOutbox.DiscardAll();
		UploadOutbox.OverrideFilePathForTests = null;
		UploadOutbox.ResetCacheForTests();
		if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
	}

	[Test]
	public void Flush_PermanentRejection_DropsPoisonHead_AndSendsNextEntry()
	{
		UploadOutbox.EnqueueRaw("LoopReport", "/api/loop-reports", "{\"verdict\":\"EnemyDeck\"}");
		UploadOutbox.EnqueueRaw("RunRecord", "/api/runs", "{\"runId\":\"r-1\"}");

		var sent = new List<string>();
		UploadOutbox.SendOverrideForTests = (path, payload, onOk, onFail) =>
		{
			if (path == "/api/loop-reports") onFail("HTTP/1.1 401 Unauthorized", 401);
			else { sent.Add(path); onOk("{}"); }
		};

		UploadOutbox.FlushSynchronouslyForTests();

		Assert.AreEqual(0, UploadOutbox.PendingCount,
			"the 401 head is a permanent rejection: it must be dropped, not retried forever");
		Assert.AreEqual(1, sent.Count,
			"the entry behind the poison head must still be sent in the same flush");
		Assert.AreEqual("/api/runs", sent[0]);
	}

	[Test]
	public void Flush_TransientFailure_KeepsHead_AndStopsBeforeNextEntry()
	{
		UploadOutbox.EnqueueRaw("LoopReport", "/api/loop-reports", "{\"verdict\":\"EnemyDeck\"}");
		UploadOutbox.EnqueueRaw("RunRecord", "/api/runs", "{\"runId\":\"r-1\"}");

		var attempts = new List<string>();
		UploadOutbox.SendOverrideForTests = (path, payload, onOk, onFail) =>
		{
			attempts.Add(path);
			if (path == "/api/loop-reports") onFail("HTTP/1.1 503 Service Unavailable", 503);
			else onOk("{}");
		};

		UploadOutbox.FlushSynchronouslyForTests();

		Assert.AreEqual(2, UploadOutbox.PendingCount,
			"a transient failure keeps the head queued for the next flush trigger");
		Assert.AreEqual(1, attempts.Count,
			"the flush stops at the transient failure — ordering is preserved, no skip");
		Assert.AreEqual("/api/loop-reports", attempts[0]);
	}

	// ---------------------------------------------------------------- draw plan Step 5: required thinning + 413 fallback

	/// <summary>Enabled config so UploadOutbox.Enqueue captures RunRecords (fresh SO per test).</summary>
	private ServerConfig CreateEnabledConfig()
	{
		ServerConfig config = ScriptableObject.CreateInstance<ServerConfig>();
		config.enabled = true;
		config.uploadRunRecords = true;
		ServerConfig.Active = config;
		return config;
	}

	private static RunUploadRequest BuildRunWithSeries(int sampleCount)
	{
		var run = new RunUploadRequest
		{
			runId = "run-thin-test",
			playerId = "pid-test",
			gameVersion = "v1",
			result = "victory",
			combats = new List<RunCombatEntry>()
		};
		var combat = new RunCombatEntry { sessionNum = 1, won = true, series = new List<RunCombatSample>() };
		for (int i = 0; i < sampleCount; i++)
		{
			combat.series.Add(new RunCombatSample
			{
				revealIndex = i + 1, roundNum = 1, ownerHP = 20, enemyHP = 20,
				side = RunCombatSample.SideOwner, cardTypeID = "wolf"
			});
		}
		run.combats.Add(combat);
		return run;
	}

	[Test]
	public void Enqueue_RunRecord_CapsSeriesToFirst450AndLast50()
	{
		ServerConfig config = CreateEnabledConfig();
		try
		{
			UploadOutbox.Enqueue(NetUploadKind.RunRecord, BuildRunWithSeries(1000));

			UploadOutbox.ResetCacheForTests();
			string payload = ReadHeadPayload();
			Assert.AreEqual(500, CountOccurrences(payload, "\"revealIndex\":"),
				"the queued run must carry exactly MaxSeriesSamples samples");
			Assert.IsTrue(payload.Contains("\"revealIndex\":1"), "curve start kept");
			Assert.IsTrue(payload.Contains("\"revealIndex\":450"), "the 450th sample is the head boundary");
			Assert.IsFalse(payload.Contains("\"revealIndex\":451"), "the middle is thinned away");
			Assert.IsFalse(payload.Contains("\"revealIndex\":950"), "the middle is thinned away");
			Assert.IsTrue(payload.Contains("\"revealIndex\":951"), "the tail keeps the end state");
			Assert.IsTrue(payload.Contains("\"revealIndex\":1000"), "curve end kept");
		}
		finally
		{
			ServerConfig.Active = null;
			if (config != null) UnityEngine.Object.DestroyImmediate(config);
		}
	}

	[Test]
	public void Enqueue_ShortSeries_IsUntouched()
	{
		ServerConfig config = CreateEnabledConfig();
		try
		{
			UploadOutbox.Enqueue(NetUploadKind.RunRecord, BuildRunWithSeries(12));

			UploadOutbox.ResetCacheForTests();
			string payload = ReadHeadPayload();
			Assert.AreEqual(12, CountOccurrences(payload, "\"revealIndex\":"),
				"a series below the cap passes through unchanged");
		}
		finally
		{
			ServerConfig.Active = null;
			if (config != null) UnityEngine.Object.DestroyImmediate(config);
		}
	}

	private string ReadHeadPayload()
	{
		var items = JsonUtility.FromJson<OutboxPeek>(File.ReadAllText(UploadOutbox.OverrideFilePathForTests));
		return items != null && items.items != null && items.items.Count > 0 ? items.items[0].jsonPayload : null;
	}

	[Serializable]
	private class OutboxPeek
	{
		public List<UploadOutbox.PendingRequest> items = new List<UploadOutbox.PendingRequest>();
	}

	private static int CountOccurrences(string text, string needle)
	{
		if (text == null) return 0;
		int count = 0;
		int idx = 0;
		while ((idx = text.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
		{
			count++;
			idx += needle.Length;
		}
		return count;
	}

	[Test]
	public void Flush_413OnRunRecord_RetriesOnceWithSeriesStripped_ThenSucceeds()
	{
		UploadOutbox.EnqueueRaw("RunRecord", "/api/runs", "{\"runId\":\"r-fat\",\"combats\":[{\"sessionNum\":1,\"series\":[{\"revealIndex\":1}]}]}");

		var payloads = new List<string>();
		UploadOutbox.SendOverrideForTests = (path, payload, onOk, onFail) =>
		{
			payloads.Add(payload);
			if (payloads.Count == 1) onFail("HTTP/1.1 413 Payload Too Large", 413);
			else onOk("{}");
		};

		UploadOutbox.FlushSynchronouslyForTests();

		Assert.AreEqual(2, payloads.Count, "the 413 head must be retried once with the series stripped");
		Assert.IsTrue(payloads[0].Contains("\"revealIndex\":1"), "the first attempt still carries the series");
		Assert.IsFalse(payloads[1].Contains("\"revealIndex\""), "the retry must have every series stripped");
		Assert.IsTrue(payloads[1].Contains("\"runId\":\"r-fat\""), "the counters and rest of the record survive the strip");
		Assert.AreEqual(0, UploadOutbox.PendingCount, "the stripped retry succeeds");
	}

	[Test]
	public void Flush_Second413OnRunRecord_DropsHeadWithOneErrorLog()
	{
		UploadOutbox.EnqueueRaw("RunRecord", "/api/runs", "{\"runId\":\"r-stubborn\"}");
		UploadOutbox.EnqueueRaw("LoopReport", "/api/loop-reports", "{\"verdict\":\"EnemyDeck\"}");

		// The second-413 drop is an error BY DESIGN (plan Step 5); expect it so the test's
		// unexpected-error check does not fire.
		LogAssert.Expect(LogType.Error, new Regex("\\[UploadOutbox\\] dropping RunRecord.*413"));
		var attempts = new List<string>();
		UploadOutbox.SendOverrideForTests = (path, payload, onOk, onFail) =>
		{
			attempts.Add(path);
			if (path == "/api/runs") onFail("HTTP/1.1 413 Payload Too Large", 413);
			else onOk("{}");
		};

		UploadOutbox.FlushSynchronouslyForTests();

		Assert.AreEqual(3, attempts.Count, "run 413 -> strip retry -> run 413 (dropped) -> loop report goes out");
		Assert.AreEqual(0, UploadOutbox.PendingCount,
			"a second 413 must drop the head instead of looping forever");
		Assert.AreEqual("/api/loop-reports", attempts[2], "the entry behind it still goes out");
	}

	[Test]
	public void Flush_413OnNonRunRecord_StillDropsTheHead()
	{
		UploadOutbox.EnqueueRaw("MatchReport", "/api/matches/report", "{\"reportId\":\"m-1\"}");
		UploadOutbox.EnqueueRaw("RunRecord", "/api/runs", "{\"runId\":\"r-2\"}");

		UploadOutbox.SendOverrideForTests = (path, payload, onOk, onFail) =>
		{
			if (path == "/api/matches/report") onFail("HTTP/1.1 413 Payload Too Large", 413);
			else onOk("{}");
		};

		UploadOutbox.FlushSynchronouslyForTests();

		Assert.AreEqual(0, UploadOutbox.PendingCount, "a non-RunRecord 413 keeps the drop-and-continue behavior");
	}
}
