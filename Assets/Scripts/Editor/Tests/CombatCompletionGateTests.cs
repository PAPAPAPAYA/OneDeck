using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TestWriteRead;
using UnityEngine;

/// <summary>
/// Upload-gate EditMode tests (plan §4 + §6 of
/// plans/plan-combat-completion-upload-gate-2026-09-06.md): per-run gate semantics
/// (closed at every run start, opened by the first completed combat), the deferred
/// card-catalog backfill, and the per-run deck-snapshot block. All hermetic via
/// directory overrides - no network, uploads land in a file-backed outbox.
/// </summary>
public class CombatCompletionGateTests
{
	private string tempDir;
	private ServerConfig config;
	private readonly List<UnityEngine.Object> scaffoldObjects = new List<UnityEngine.Object>();

	private string CatalogVersionPath
	{
		get { return Path.Combine(tempDir, "catalog_version.txt"); }
	}

	[SetUp]
	public void SetUp()
	{
		tempDir = Path.Combine(Path.GetTempPath(), "onedeck_gate_test_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempDir);
		CombatCompletionGate.ResetForTests();
		CardCatalogUploader.OverrideDirectoryForTests = tempDir;
		UploadOutbox.OverrideFilePathForTests = Path.Combine(tempDir, "outbox.json");
		UploadOutbox.ResetCacheForTests();

		config = ScriptableObject.CreateInstance<ServerConfig>();
		config.enabled = true;
		config.uploadCardCatalog = true;
		config.uploadDeckSnapshots = true;
		ServerConfig.Active = config;
	}

	[TearDown]
	public void TearDown()
	{
		ServerConfig.Active = null;
		foreach (UnityEngine.Object obj in scaffoldObjects)
		{
			if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
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
		if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
		if (config != null) UnityEngine.Object.DestroyImmediate(config);
	}

	[Test]
	public void FreshRun_GateClosed()
	{
		Assert.IsFalse(CombatCompletionGate.HasCompletedCombatThisRun);
	}

	[Test]
	public void MarkCompleted_OpensGateForTheRestOfTheRun()
	{
		CombatCompletionGate.MarkCompleted();
		Assert.IsTrue(CombatCompletionGate.HasCompletedCombatThisRun);

		CombatCompletionGate.MarkCompleted();
		Assert.IsTrue(CombatCompletionGate.HasCompletedCombatThisRun);
	}

	[Test]
	public void OnRunStarted_ClosesGateAgainForTheNewRun()
	{
		CombatCompletionGate.MarkCompleted();
		Assert.IsTrue(CombatCompletionGate.HasCompletedCombatThisRun);

		// New run (scene start / ResetRun): the gate closes again.
		CombatCompletionGate.OnRunStarted();
		Assert.IsFalse(CombatCompletionGate.HasCompletedCombatThisRun);

		// And the new run arms independently.
		CombatCompletionGate.MarkCompleted();
		Assert.IsTrue(CombatCompletionGate.HasCompletedCombatThisRun);
	}

	[Test]
	public void FirstCompletion_TriggersDeferredCatalogUpload()
	{
		InstallHermeticIdentity();
		InstallPoolScaffold();

		// Gate closed: MaybeUpload is a no-op - nothing queued, no version stamped.
		CardCatalogUploader.MaybeUpload();
		Assert.IsFalse(File.Exists(CatalogVersionPath));
		Assert.AreEqual(0, UploadOutbox.PendingCount);

		// First completed combat opens the run's gate and backfills the catalog.
		CombatCompletionGate.MarkCompleted();
		Assert.IsTrue(File.Exists(CatalogVersionPath));
		Assert.AreEqual(1, UploadOutbox.PendingCount);
		Assert.AreEqual(NetUploadKind.CardCatalog.ToString(), ReadOutboxKind());
	}

	[Test]
	public void GateAlreadyOpen_CatalogUploadsAgainOnlyOnContentDrift()
	{
		InstallHermeticIdentity();
		InstallPoolScaffold();

		CombatCompletionGate.MarkCompleted();
		Assert.AreEqual(1, UploadOutbox.PendingCount);

		// Content idempotence: an unchanged pool fingerprints identically, so an
		// open gate alone uploads nothing more.
		CombatCompletionGate.MarkCompleted();
		Assert.AreEqual(1, UploadOutbox.PendingCount);
	}

	[Test]
	public void ComputeFingerprint_SensitiveToEveryCatalogField()
	{
		string baseline = CardCatalogUploader.ComputeFingerprint(SampleEntries());

		// Same entries, different list order: order-insensitive.
		List<CatalogCardEntry> reordered = SampleEntries();
		reordered.Reverse();
		Assert.AreEqual(baseline, CardCatalogUploader.ComputeFingerprint(reordered));

		// Same tags, different serialization order: still order-insensitive.
		List<CatalogCardEntry> tagsReordered = SampleEntries();
		tagsReordered[0].tags.Reverse();
		Assert.AreEqual(baseline, CardCatalogUploader.ComputeFingerprint(tagsReordered));

		// Any single field mutation changes the fingerprint.
		Assert.AreNotEqual(baseline, CardCatalogUploader.ComputeFingerprint(
			MutateEntry(0, entry => entry.name = "owl_renamed")), "name drift must change the fingerprint");
		Assert.AreNotEqual(baseline, CardCatalogUploader.ComputeFingerprint(
			MutateEntry(1, entry => entry.tags.Remove("DeathRattle"))), "tag content drift must change the fingerprint");
		Assert.AreNotEqual(baseline, CardCatalogUploader.ComputeFingerprint(
			MutateEntry(0, entry => entry.rarity = "Uncommon")), "rarity drift must change the fingerprint");
		Assert.AreNotEqual(baseline, CardCatalogUploader.ComputeFingerprint(
			MutateEntry(1, entry => entry.cost += 1)), "cost drift must change the fingerprint");

		// Pool membership changes (add / remove) change the fingerprint.
		List<CatalogCardEntry> withExtra = SampleEntries();
		withExtra.Add(new CatalogCardEntry { cardTypeID = "boar", name = "boar_name", tags = new List<string>(), rarity = "Common", cost = 2 });
		Assert.AreNotEqual(baseline, CardCatalogUploader.ComputeFingerprint(withExtra));

		List<CatalogCardEntry> minusOne = SampleEntries();
		minusOne.RemoveAt(1);
		Assert.AreNotEqual(baseline, CardCatalogUploader.ComputeFingerprint(minusOne));
	}

	[Test]
	public void LegacyVersionSentinel_MigratesToFirstFingerprintUpload()
	{
		InstallHermeticIdentity();
		InstallPoolScaffold();

		// Pre-upgrade sentinel: the old format stored the packaged game version.
		File.WriteAllText(CatalogVersionPath, DeckNetworkClient.GameVersion);

		// A version string never equals a fingerprint: the first gated upload
		// re-sends once, then the freshly stamped fingerprint goes quiet.
		CombatCompletionGate.MarkCompleted();
		Assert.AreEqual(1, UploadOutbox.PendingCount);

		CardCatalogUploader.MaybeUpload();
		Assert.AreEqual(1, UploadOutbox.PendingCount);
	}

	[Test]
	public void GateAlreadyOpen_ContentDriftReuploadsViaSentinelMismatch()
	{
		InstallHermeticIdentity();
		InstallPoolScaffold();

		CombatCompletionGate.MarkCompleted();
		Assert.AreEqual(1, UploadOutbox.PendingCount);

		// Simulate content drift without touching prefab assets: poison the sentinel
		// so it cannot match the freshly computed fingerprint (the same path a real
		// rename / retag / price change takes).
		File.WriteAllText(CatalogVersionPath, "stale-fingerprint-from-earlier-content");

		CombatCompletionGate.MarkCompleted();
		Assert.AreEqual(2, UploadOutbox.PendingCount);

		// Re-stamped after the re-upload: quiet again.
		CombatCompletionGate.MarkCompleted();
		Assert.AreEqual(2, UploadOutbox.PendingCount);
	}

	[Test]
	public void DeckSnapshot_DeferredOpeningDeckUploadsWithFirstPostCombatSnapshot()
	{
		InstallHermeticIdentity();
		InstallDeckSaverScaffold();

		// Opening deck of a fresh run: deferred, not uploaded.
		DeckSaver.Me.SavePlayerDeckSnapshot();
		Assert.AreEqual(0, UploadOutbox.PendingCount);

		// First completed combat opens the gate; the next snapshot (the second shop
		// exit) carries the deferred opening deck plus the current deck, in order.
		CombatCompletionGate.MarkCompleted();
		DeckSaver.Me.SavePlayerDeckSnapshot();
		Assert.AreEqual(2, UploadOutbox.PendingCount);
		Assert.AreEqual(NetUploadKind.DeckSnapshot.ToString(), ReadOutboxKind());

		// New run: blocked again - snapshots defer instead of uploading.
		CombatCompletionGate.OnRunStarted();
		DeckSaver.Me.SavePlayerDeckSnapshot();
		Assert.AreEqual(2, UploadOutbox.PendingCount);
	}

	[Test]
	public void DeckSnapshot_RunStartCleanupDropsDeferredOpeningDeck()
	{
		InstallHermeticIdentity();
		InstallDeckSaverScaffold();

		// Run 1 defers its opening deck, then ends before the gate ever opens.
		DeckSaver.Me.SavePlayerDeckSnapshot();
		// Run-start cleanup (PhaseManager pairs ClearDeferredSnapshot with OnRunStarted).
		DeckSaver.Me.ClearDeferredSnapshot();
		CombatCompletionGate.OnRunStarted();

		// Run 2 completes a combat: only its own deck uploads - run 1's deferred
		// opening deck must not leak into the new run.
		CombatCompletionGate.MarkCompleted();
		DeckSaver.Me.SavePlayerDeckSnapshot();
		Assert.AreEqual(1, UploadOutbox.PendingCount);
	}

	// ------------------------------------------------------------------ scaffolding

	/// <summary>Fresh fingerprint fixtures per call: mutations in one test never leak into another.</summary>
	private static List<CatalogCardEntry> SampleEntries()
	{
		return new List<CatalogCardEntry>
		{
			new CatalogCardEntry { cardTypeID = "owl", name = "owl_name", tags = new List<string> { "Linger", "ManaX" }, rarity = "Rare", cost = 7 },
			new CatalogCardEntry { cardTypeID = "wolf", name = "wolf_name", tags = new List<string> { "DeathRattle" }, rarity = "Common", cost = 3 }
		};
	}

	private static List<CatalogCardEntry> MutateEntry(int index, Action<CatalogCardEntry> mutation)
	{
		List<CatalogCardEntry> entries = SampleEntries();
		mutation(entries[index]);
		return entries;
	}

	private void InstallHermeticIdentity()
	{
		// A hand-written identity file flips HasIdentity true with no network call
		// (PlayerIdentity reads this exact file shape).
		File.WriteAllText(Path.Combine(tempDir, "player_identity.json"),
			"{\"playerId\":\"pid-1\",\"username\":\"tester\"}");
		PlayerIdentity.OverrideDirectoryForTests = tempDir;
		PlayerIdentity.ResetForTests();
	}

	private void InstallPoolScaffold()
	{
		DeckSO pool = ScriptableObject.CreateInstance<DeckSO>();
		Track(pool);
		pool.deck = new List<GameObject> { CreateCardScaffold("wolf") };

		GameObject saverGo = CreateScaffoldObject("saver");
		DeckSaver saver = saverGo.AddComponent<DeckSaver>();
		saver.shopPoolRef = pool;
		DeckSaver.Me = saver;
	}

	private void InstallDeckSaverScaffold()
	{
		DeckSO playerDeck = ScriptableObject.CreateInstance<DeckSO>();
		Track(playerDeck);
		playerDeck.deck = new List<GameObject> { CreateCardScaffold("wolf") };

		IntSO winAmount = ScriptableObject.CreateInstance<IntSO>();
		Track(winAmount);
		IntSO heartLeft = ScriptableObject.CreateInstance<IntSO>();
		Track(heartLeft);
		IntSO sessionNumber = ScriptableObject.CreateInstance<IntSO>();
		Track(sessionNumber);
		PlayerStatusSO playerStatus = ScriptableObject.CreateInstance<PlayerStatusSO>();
		Track(playerStatus);

		GameObject saverGo = CreateScaffoldObject("saver");
		DeckSaver saver = saverGo.AddComponent<DeckSaver>();
		saver.playerDeck = playerDeck;
		saver.winAmount = winAmount;
		saver.heartLeft = heartLeft;
		saver.sessionNumber = sessionNumber;
		saver.playerStatusRef = playerStatus;
		DeckSaver.Me = saver;
	}

	private void Track(UnityEngine.Object obj)
	{
		scaffoldObjects.Add(obj);
	}

	private GameObject CreateScaffoldObject(string name)
	{
		GameObject go = new GameObject(name);
		Track(go);
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
		// Light touch: the kind field is the only payload identity we need here.
		const string marker = "\"kind\":\"";
		int start = json.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
		int end = json.IndexOf('"', start);
		return json.Substring(start, end - start);
	}
}
