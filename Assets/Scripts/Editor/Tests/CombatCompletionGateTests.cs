using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TestWriteRead;
using UnityEngine;

/// <summary>
/// Upload-gate EditMode tests (plan §4 of
/// plans/plan-combat-completion-upload-gate-2026-09-06.md): sentinel arming,
/// persistence, idempotence, and the deferred card-catalog backfill on first
/// completion. All hermetic via the directory overrides - no network, the catalog
/// lands in a file-backed outbox.
/// </summary>
public class CombatCompletionGateTests
{
	private string tempDir;
	private ServerConfig config;
	private readonly List<GameObject> scaffoldObjects = new List<GameObject>();

	private string FlagPath
	{
		get { return Path.Combine(tempDir, "has_completed_combat.flag"); }
	}

	private string CatalogVersionPath
	{
		get { return Path.Combine(tempDir, "catalog_version.txt"); }
	}

	[SetUp]
	public void SetUp()
	{
		tempDir = Path.Combine(Path.GetTempPath(), "onedeck_gate_test_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempDir);
		CombatCompletionGate.OverrideDirectoryForTests = tempDir;
		CombatCompletionGate.ResetForTests();
		CardCatalogUploader.OverrideDirectoryForTests = tempDir;
		UploadOutbox.OverrideFilePathForTests = Path.Combine(tempDir, "outbox.json");
		UploadOutbox.ResetCacheForTests();

		config = ScriptableObject.CreateInstance<ServerConfig>();
		config.enabled = true;
		config.uploadCardCatalog = true;
		ServerConfig.Active = config;
	}

	[TearDown]
	public void TearDown()
	{
		ServerConfig.Active = null;
		foreach (GameObject go in scaffoldObjects)
		{
			if (go != null) UnityEngine.Object.DestroyImmediate(go);
		}
		scaffoldObjects.Clear();
		DeckSaver.Me = null;
		PlayerIdentity.OverrideDirectoryForTests = tempDir;
		PlayerIdentity.ResetForTests();
		PlayerIdentity.OverrideDirectoryForTests = null;
		UploadOutbox.ResetCacheForTests();
		UploadOutbox.OverrideFilePathForTests = null;
		CardCatalogUploader.OverrideDirectoryForTests = null;
		CombatCompletionGate.ResetForTests();
		CombatCompletionGate.OverrideDirectoryForTests = null;
		if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
		if (config != null) UnityEngine.Object.DestroyImmediate(config);
	}

	[Test]
	public void FreshState_GateClosedAndNoFlagFile()
	{
		Assert.IsFalse(CombatCompletionGate.HasCompletedCombat);
		Assert.IsFalse(File.Exists(FlagPath));
	}

	[Test]
	public void MarkCompleted_WritesFlagAndSurvivesReload()
	{
		CombatCompletionGate.MarkCompleted();

		Assert.IsTrue(CombatCompletionGate.HasCompletedCombat);
		Assert.IsTrue(File.Exists(FlagPath));

		CombatCompletionGate.ResetForTests();  // force the next read back to disk
		Assert.IsTrue(CombatCompletionGate.HasCompletedCombat);
	}

	[Test]
	public void MarkCompleted_RepeatedCallsStayArmed()
	{
		CombatCompletionGate.MarkCompleted();
		CombatCompletionGate.MarkCompleted();

		CombatCompletionGate.ResetForTests();
		Assert.IsTrue(CombatCompletionGate.HasCompletedCombat);
	}

	[Test]
	public void OverrideDirectory_IsolatesFlagFiles()
	{
		CombatCompletionGate.MarkCompleted();
		Assert.IsTrue(CombatCompletionGate.HasCompletedCombat);

		// A different (fresh) override dir must read as untouched.
		string otherDir = Path.Combine(Path.GetTempPath(), "onedeck_gate_other_" + Guid.NewGuid().ToString("N"));
		try
		{
			Directory.CreateDirectory(otherDir);
			CombatCompletionGate.OverrideDirectoryForTests = otherDir;
			CombatCompletionGate.ResetForTests();
			Assert.IsFalse(CombatCompletionGate.HasCompletedCombat);
		}
		finally
		{
			Directory.Delete(otherDir, true);
		}
	}

	[Test]
	public void FirstCompletion_TriggersDeferredCatalogUpload()
	{
		// Hermetic identity: a hand-written identity file flips HasIdentity true with
		// no network call (PlayerIdentity reads this exact file shape).
		File.WriteAllText(Path.Combine(tempDir, "player_identity.json"),
			"{\"playerId\":\"pid-1\",\"username\":\"tester\"}");
		PlayerIdentity.OverrideDirectoryForTests = tempDir;
		PlayerIdentity.ResetForTests();

		// Minimal pool scaffold so CardCatalogUploader finds cards and reaches the outbox.
		DeckSO pool = ScriptableObject.CreateInstance<DeckSO>();
		pool.deck = new List<GameObject> { CreateCardScaffold("wolf") };
		GameObject saverGo = CreateScaffoldObject("saver");
		DeckSaver saver = saverGo.AddComponent<DeckSaver>();
		saver.shopPoolRef = pool;
		DeckSaver.Me = saver;

		// Gate closed: MaybeUpload is a no-op - nothing queued, no version stamped.
		CardCatalogUploader.MaybeUpload();
		Assert.IsFalse(File.Exists(CatalogVersionPath));
		Assert.AreEqual(0, UploadOutbox.PendingCount);

		// First completion arms the gate and backfills the catalog in the same call.
		CombatCompletionGate.MarkCompleted();
		Assert.IsTrue(File.Exists(CatalogVersionPath));
		Assert.AreEqual(1, UploadOutbox.PendingCount);

		// The queued item is a card catalog for the current version.
		Assert.AreEqual(NetUploadKind.CardCatalog.ToString(), ReadOutboxKind());
	}

	[Test]
	public void GateAlreadyOpen_CatalogUploadsAgainOnlyOnVersionDrift()
	{
		File.WriteAllText(Path.Combine(tempDir, "player_identity.json"),
			"{\"playerId\":\"pid-1\",\"username\":\"tester\"}");
		PlayerIdentity.OverrideDirectoryForTests = tempDir;
		PlayerIdentity.ResetForTests();

		DeckSO pool = ScriptableObject.CreateInstance<DeckSO>();
		pool.deck = new List<GameObject> { CreateCardScaffold("wolf") };
		GameObject saverGo = CreateScaffoldObject("saver");
		DeckSaver saver = saverGo.AddComponent<DeckSaver>();
		saver.shopPoolRef = pool;
		DeckSaver.Me = saver;

		CombatCompletionGate.MarkCompleted();
		Assert.AreEqual(1, UploadOutbox.PendingCount);

		// Version idempotence: an already-stamped version uploads nothing more.
		CombatCompletionGate.MarkCompleted();
		Assert.AreEqual(1, UploadOutbox.PendingCount);
	}

	private GameObject CreateScaffoldObject(string name)
	{
		GameObject go = new GameObject(name);
		scaffoldObjects.Add(go);
		return go;
	}

	private GameObject CreateCardScaffold(string typeID)
	{
		GameObject card = CreateScaffoldObject(typeID);
		CardScript script = card.AddComponent<CardScript>();
		script.cardTypeID = typeID;
		return card;
	}

	private string ReadOutboxKind()
	{
		string json = File.ReadAllText(UploadOutbox.OverrideFilePathForTests);
		// Light touch: the kind field is the first payload identity we need here.
		const string marker = "\"kind\":\"";
		int start = json.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
		int end = json.IndexOf('"', start);
		return json.Substring(start, end - start);
	}
}
