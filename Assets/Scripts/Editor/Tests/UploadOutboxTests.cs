using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

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
}
