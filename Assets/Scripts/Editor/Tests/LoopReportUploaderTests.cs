using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// P3 upload plumbing (plan §20.3): the evidence report that turns a client-side attribution into
/// a server-side flag. The endpoint and the flagging semantics live in server.js and are covered
/// by its own node tests (server/onedeck-api/tests/loopReports.test.js); what is tested here is
/// the client contract — which trips get queued, with which deck id and verdict, and that the
/// attribution-only API never touches the outbox.
/// </summary>
public class LoopReportUploaderTests
{
	private const string DeckFolder = "Assets/SORefs/Decks/test decks/chain tests/4.0";

	private string tempDir;
	private string outboxPath;
	private ServerConfig config;

	[SetUp]
	public void SetUp()
	{
		tempDir = Path.Combine(Path.GetTempPath(), "onedeck_loop_report_test_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempDir);
		outboxPath = Path.Combine(tempDir, "outbox.json");
		UploadOutbox.OverrideFilePathForTests = outboxPath;
		UploadOutbox.DiscardAll();
		UploadOutbox.ResetCacheForTests();

		config = ScriptableObject.CreateInstance<ServerConfig>();
		config.enabled = true;
		config.uploadLoopReports = true;
		ServerConfig.Active = config;

		// Hermetic identity: a hand-written identity file flips HasIdentity true with no network call.
		File.WriteAllText(Path.Combine(tempDir, "player_identity.json"),
			"{\"playerId\":\"pid-1\",\"username\":\"tester\"}");
		PlayerIdentity.OverrideDirectoryForTests = tempDir;
		PlayerIdentity.ResetForTests();

		InfinityTripJournal.Clear();
	}

	[TearDown]
	public void TearDown()
	{
		InfinityTripJournal.Clear();
		UploadOutbox.DiscardAll();
		UploadOutbox.OverrideFilePathForTests = null;
		UploadOutbox.ResetCacheForTests();
		PlayerIdentity.OverrideDirectoryForTests = null;
		PlayerIdentity.ResetForTests();
		ServerConfig.Active = null;
		if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
		if (config != null) UnityEngine.Object.DestroyImmediate(config);
	}

	[OneTimeTearDown]
	public void OneTimeTearDown()
	{
		HeadlessCombatRig.DestroySharedResources();
	}

	[Test]
	public void EnqueueEvidence_LocalPoolEnemy_IsSkipped()
	{
		// Deck id 0 = the enemy came from the local default pool: no server row exists and pool
		// decks are not player content (plan §20.1 ruling 3), so nothing may be queued.
		Assert.IsFalse(LoopReportUploader.EnqueueEvidence(0, LoopReportUploader.VerdictEnemyDeck, 4242, "sig", "{}"));
		Assert.AreEqual(0, UploadOutbox.PendingCount);
	}

	[Test]
	public void EnqueueEvidence_ServerGhost_QueuesTheAccusedDeck()
	{
		Assert.IsTrue(LoopReportUploader.EnqueueEvidence(777, LoopReportUploader.VerdictEnemyDeck, 4242,
			"arrangement-cycle deadbeef repeats=9", "{\"mySide\":[\"CARD_A\"]}"));

		Assert.AreEqual(1, UploadOutbox.PendingCount);
		List<QueuedItem> items = ReadOutbox();
		Assert.AreEqual(1, items.Count, "exactly one queued upload");
		Assert.AreEqual("LoopReport", items[0].kind, "the entry must carry its kind");
		Assert.AreEqual("/api/loop-reports", items[0].path, "and route to the evidence endpoint");

		var request = JsonUtility.FromJson<LoopReportUploadRequest>(items[0].jsonPayload);
		Assert.AreEqual("pid-1", request.playerId);
		Assert.AreEqual(777, request.opponentDeckId, "the accusation must name the ghost deck row");
		Assert.AreEqual("EnemyDeck", request.verdict, "the flag-triggering verdict");
		Assert.AreEqual(4242, request.seed, "the reproduction seed survives");
		StringAssert.Contains("repeats=9", request.signals);
	}

	[Test]
	public void EnqueueEvidence_SwitchOff_QueuesNothing()
	{
		config.uploadLoopReports = false;
		Assert.IsFalse(LoopReportUploader.EnqueueEvidence(777, LoopReportUploader.VerdictEnemyDeck, 4242, "sig", "{}"));
		Assert.AreEqual(0, UploadOutbox.PendingCount);
	}

	[Test]
	public void ProcessPendingAndUpload_QueuesTheAttributedReport()
	{
		var specimen = LoadSampleDeck("test infinite loop");
		// Enemy side carries the looping deck (so the verdict is EnemyDeck) and the live trip was
		// fought against server ghost deck 505.
		InfinityTripJournal.Record(0x1234u, 1, 7, 4242, specimen, specimen, 505);

		var options = new RunBudgetSim.Options { GuardTotal = 300 };
		var reports = InfinityAttributionProcessor.ProcessPendingAndUpload(new[] { 4242 }, options);

		Assert.AreEqual(1, reports.Count, "one queued trip must yield one report");
		Assert.AreEqual("EnemyDeck", reports[0].responsibility);
		Assert.AreEqual(1, UploadOutbox.PendingCount, "the attributed report must be queued for upload");

		var request = JsonUtility.FromJson<LoopReportUploadRequest>(ReadOutbox()[0].jsonPayload);
		Assert.AreEqual(505, request.opponentDeckId, "the queue names the ghost deck from the LIVE trip");
		Assert.AreEqual("EnemyDeck", request.verdict);
		Assert.AreEqual(4242, request.seed);
		StringAssert.Contains("combatSeed=4242", request.signals, "the LIVE trip's evidence rides in signals");
		StringAssert.Contains("TEST_LOOP_HUB", request.payload, "the minimized combo rides in the payload");
		Assert.AreEqual(0, InfinityTripJournal.Count, "processing must drain the queue");
	}

	[Test]
	public void ProcessPending_DoesNotTouchTheOutbox()
	{
		var specimen = LoadSampleDeck("test infinite loop");
		InfinityTripJournal.Record(0x1234u, 1, 7, 4242, specimen, specimen, 505);

		var options = new RunBudgetSim.Options { GuardTotal = 300 };
		var reports = InfinityAttributionProcessor.ProcessPending(new[] { 4242 }, options);

		Assert.AreEqual(1, reports.Count);
		Assert.AreEqual(0, UploadOutbox.PendingCount,
			"attribution only: tests and offline analysis must stay out of the upload queue");
	}

	private static DeckSO LoadSampleDeck(string assetName)
	{
		var deck = UnityEditor.AssetDatabase.LoadAssetAtPath<DeckSO>(DeckFolder + "/" + assetName + ".asset");
		Assert.IsNotNull(deck, "sample deck missing: " + assetName);
		return deck;
	}

	// The outbox file shape (UploadOutbox keeps its item type private): the queue is read back
	// through the same JsonUtility contract the game writes, so the assertions cover the wire
	// payload rather than a string-matching coincidence of escaping.
	[Serializable]
	private class QueuedItem
	{
		public string kind;
		public string path;
		public string jsonPayload;
		public string enqueuedAt;
	}

	[Serializable]
	private class QueuedFile
	{
		public List<QueuedItem> items;
	}

	private List<QueuedItem> ReadOutbox()
	{
		if (!File.Exists(outboxPath)) return new List<QueuedItem>();
		var file = JsonUtility.FromJson<QueuedFile>(File.ReadAllText(outboxPath));
		return file != null && file.items != null ? file.items : new List<QueuedItem>();
	}
}
