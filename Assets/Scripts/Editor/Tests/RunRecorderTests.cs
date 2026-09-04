using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Batch D EditMode tests (plan §4): run-journal event flow, run_end upload payload,
/// and crash recovery (unfinished run re-uploads as abandoned). Network is never
/// touched: the outbox only enqueues, and Flush is a no-op without a live client host.
/// </summary>
public class RunRecorderTests
{
	private string tempDir;
	private string identityPath;
	private string outboxPath;
	private ServerConfig config;

	[SetUp]
	public void SetUp()
	{
		tempDir = Path.Combine(Path.GetTempPath(), "onedeck_run_rec_test_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempDir);
		identityPath = Path.Combine(tempDir, "player_identity.json");
		outboxPath = Path.Combine(tempDir, "outbox.json");

		RunRecorder.OverrideDirectoryForTests = tempDir;
		UploadOutbox.OverrideFilePathForTests = outboxPath;
		PlayerIdentity.OverrideDirectoryForTests = tempDir;

		config = ScriptableObject.CreateInstance<ServerConfig>();
		config.enabled = true;
		ServerConfig.Active = config;

		WriteIdentity("pid-test");
	}

	[TearDown]
	public void TearDown()
	{
		RunRecorder.ResetForTests();
		UploadOutbox.DiscardAll();
		UploadOutbox.OverrideFilePathForTests = null;
		PlayerIdentity.ResetForTests();
		PlayerIdentity.OverrideDirectoryForTests = null;
		ServerConfig.Active = null;
		if (config != null) UnityEngine.Object.DestroyImmediate(config);
		if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
	}

	private void WriteIdentity(string playerId)
	{
		File.WriteAllText(identityPath, "{\"playerId\":\"" + playerId + "\",\"username\":\"tester\"}", new UTF8Encoding(false));
		PlayerIdentity.ResetForTests();
	}

	private string JournalPath
	{
		get { return Path.Combine(tempDir, "current_run.jsonl"); }
	}

	[Test]
	public void FullRunFlow_ProducesCompleteUploadPayload()
	{
		RunRecorder.StartRun();
		RunRecorder.OnShopEnter(10);
		RunRecorder.OnPayday(15);
		RunRecorder.OnCardOffered("wolf", false);
		RunRecorder.OnCardOffered("shrine", true);
		RunRecorder.OnCardBought("wolf");
		RunRecorder.OnReroll();
		RunRecorder.CloseShopVisit(5, 1);
		RunRecorder.RecordCombatEnd(1, true, 4, 6, 12);
		RunRecorder.CloseRun(RunRecorder.ResultVictory, 1, 4, new List<string> { "wolf", "shrine" });

		Assert.AreEqual(1, UploadOutbox.PendingCount, "run record must be enqueued");
		UploadOutbox.ResetCacheForTests();
		string payload = UploadOutbox.PendingCount > 0 ? ReadHeadPayload() : null;

		StringAssert.Contains("\"runId\"", payload);
		StringAssert.Contains("\"result\":\"victory\"", payload);
		StringAssert.Contains("\"goldEnter\":10", payload);
		StringAssert.Contains("\"goldAfterPayday\":15", payload);
		StringAssert.Contains("\"goldExit\":5", payload);
		StringAssert.Contains("\"rerollCount\":1", payload);
		StringAssert.Contains("\"bought\":[\"wolf\"]", payload);
		StringAssert.Contains("\"utilityOffered\":[\"shrine\"]", payload);
		StringAssert.Contains("\"rounds\":6", payload);
		StringAssert.Contains("\"opponentDeckId\":12", payload);
		StringAssert.Contains("\"playerId\":\"pid-test\"", payload);
	}

	private string ReadHeadPayload()
	{
		// re-read the head item through the public seam: enqueue happens-before, cache was reset
		var items = JsonUtility.FromJson<OutboxPeek>(File.ReadAllText(outboxPath));
		return items != null && items.items != null && items.items.Count > 0 ? items.items[0].jsonPayload : null;
	}

	[Serializable]
	private class OutboxPeek
	{
		public List<UploadOutbox.PendingRequest> items = new List<UploadOutbox.PendingRequest>();
	}

	[Test]
	public void UnregisteredRun_StaysInJournal_ForLaterRecovery()
	{
		File.Delete(identityPath);
		PlayerIdentity.ResetForTests();

		RunRecorder.StartRun();
		RunRecorder.CloseShopVisit(0, 0);
		RunRecorder.CloseRun(RunRecorder.ResultVictory, 0, 3, new List<string> { "wolf" });

		Assert.AreEqual(0, UploadOutbox.PendingCount, "no identity: nothing to attach the run to");
		Assert.IsTrue(File.Exists(JournalPath), "journal must survive for the next start");
		Assert.IsTrue(File.ReadAllText(JournalPath).Contains("\"result\":\"victory\""));
	}

	[Test]
	public void Recovery_UnfinishedRun_ReuploadsAsAbandoned()
	{
		string oldRun = "{\"runId\":\"run-old\",\"playerId\":\"\",\"gameVersion\":\"0.1.0\",\"result\":\"\",\"startedAt\":\"t\",\"finalSession\":4}";
		File.WriteAllText(JournalPath, oldRun + "\n", new UTF8Encoding(false));

		RunRecorder.StartRun();

		Assert.AreEqual(1, UploadOutbox.PendingCount, "unfinished previous run must be re-uploaded");
		string payload = ReadHeadPayload();
		StringAssert.Contains("\"runId\":\"run-old\"", payload);
		StringAssert.Contains("\"result\":\"abandoned\"", payload);
		StringAssert.Contains("\"playerId\":\"pid-test\"", payload);
		StringAssert.Contains("\"finalSession\":4", payload);

		// the new run opened afterwards must have a different id
		Assert.IsFalse(File.ReadAllText(JournalPath).Contains("run-old"));
	}

	[Test]
	public void Recovery_TornTailLine_IsSkipped()
	{
		string good = "{\"runId\":\"run-good\",\"playerId\":\"pid-test\",\"gameVersion\":\"0.1.0\",\"result\":\"defeat\",\"startedAt\":\"t\"}";
		File.WriteAllText(JournalPath, good + "\n{\"runId\":\"run-torn\",\"resu", new UTF8Encoding(false));

		RunRecorder.StartRun();

		Assert.AreEqual(1, UploadOutbox.PendingCount);
		StringAssert.Contains("\"runId\":\"run-good\"", ReadHeadPayload());
		StringAssert.Contains("\"result\":\"defeat\"", ReadHeadPayload());
	}
}
