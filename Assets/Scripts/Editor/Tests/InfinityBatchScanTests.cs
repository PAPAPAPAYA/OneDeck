using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// P5 batch scan tests (plan §23). The scan is the offline confirmation path that turns the
/// serving gate into production protection, so what matters here is that a verdict is only ever
/// produced from a fully-resolved deck, and that a loop is reported with a proven minimum.
/// Cost: one ddmin run on the lethal sample plus one clean run — kept deliberately small.
/// </summary>
public class InfinityBatchScanTests
{
	private const string DeckFolder = "Assets/SORefs/Decks/test decks/chain tests/4.0";

	private static readonly int[] Seeds = { 4242 };

	[OneTimeTearDown]
	public void OneTimeTearDown()
	{
		HeadlessCombatRig.DestroySharedResources();
	}

	// ------------------------------------------------------------------ core verdicts

	[Test]
	public void Scan_LeapingDeck_IsReportedInfiniteWithAProvenMinimum()
	{
		InfinityBatchScan.Candidate candidate = CandidateFromSampleDeck("lethal infinite test", deckId: 42);

		InfinityBatchScan.ScanReport report = InfinityBatchScan.Run(
			new List<InfinityBatchScan.Candidate> { candidate }, Seeds,
			InfinityAttribution.DefaultDummySize, InfinityAttribution.DefaultDummyHp, null, false);

		Assert.IsNotNull(report);
		Assert.AreEqual(1, report.scanned);
		Assert.AreEqual(1, report.infinite);

		InfinityBatchScan.DeckResult deck = report.decks[0];
		Assert.AreEqual(InfinityBatchScan.StatusInfinite, deck.status);
		Assert.AreEqual(42, deck.deckId, "the server deck row must survive into the report (it is the accusation)");
		Assert.IsTrue(deck.oneMinimal, "a posted verdict must carry a 1-minimal set");
		Assert.IsFalse(deck.truncated, "a truncated minimum must never be posted (§4)");
		Assert.IsTrue(deck.multiSeedStable);
		Assert.Greater(deck.repeats, 0, "the arrangement must have repeated inside a round");

		// The payload is what post_loop_reports.js sends verbatim, so it has to parse back.
		var payload = JsonUtility.FromJson<LoopReport>(deck.reportJson);
		Assert.IsNotNull(payload);
		Assert.AreEqual("EnemyDeck", payload.responsibility, "a deck that loops against an inert dummy is the enemy-side verdict");
		Assert.AreEqual(2, payload.mySide.Length, "the lethal sample's minimum is both combo cards");
		Assert.IsTrue(payload.oneMinimal);
		Assert.IsTrue(payload.multiSeedStable, "the report states the ingest-gate evidence explicitly");
		Assert.IsNotEmpty(payload.roles, "raw binding evidence rides along for falsification");
		Assert.IsEmpty(payload.liveTripSignal, "a scan has no live trip — that field belongs to the runtime path");
		Debug.Log("[BatchScanTest] " + payload.Summary());
	}

	[Test]
	public void Scan_SingleCardDeck_IsClean()
	{
		// Half of the lethal pair: resolvable, but on its own there is no loop.
		InfinityBatchScan.Candidate candidate = CandidateFromSampleDeck("lethal infinite test", deckId: 7, limit: 1);

		InfinityBatchScan.ScanReport report = InfinityBatchScan.Run(
			new List<InfinityBatchScan.Candidate> { candidate }, Seeds,
			InfinityAttribution.DefaultDummySize, InfinityAttribution.DefaultDummyHp, null, false);

		Assert.AreEqual(0, report.infinite);
		Assert.AreEqual(1, report.clean);
		Assert.AreEqual(InfinityBatchScan.StatusClean, report.decks[0].status);
		Assert.IsTrue(string.IsNullOrEmpty(report.decks[0].reportJson), "a clean deck produces nothing to post");
	}

	[Test]
	public void Scan_UnresolvedCardId_IsSkippedAndNeverSimulated()
	{
		var candidate = new InfinityBatchScan.Candidate
		{
			DeckId = 9,
			Label = "unresolvable",
			GameVersion = "4.0",
			Source = "server",
			CardTypeIDs = new List<string> { "NO_SUCH_CARD_TYPE_ID" },
		};

		InfinityBatchScan.ScanReport report = InfinityBatchScan.Run(
			new List<InfinityBatchScan.Candidate> { candidate }, Seeds,
			InfinityAttribution.DefaultDummySize, InfinityAttribution.DefaultDummyHp, null, false);

		Assert.AreEqual(InfinityBatchScan.StatusUnresolved, report.decks[0].status);
		Assert.AreEqual(1, report.unresolved);
		Assert.AreEqual(0, report.scanned, "a deck with an unknown card must not be measured at all — a dropped card can turn a loop into a non-loop");
		Assert.IsNotEmpty(report.decks[0].missingCards);
	}

	[Test]
	public void Scan_DuplicateContent_IsMeasuredOnce()
	{
		InfinityBatchScan.Candidate first = CandidateFromSampleDeck("lethal infinite test", deckId: 100);
		InfinityBatchScan.Candidate twin = CandidateFromSampleDeck("lethal infinite test", deckId: 101, label: "twin");

		InfinityBatchScan.ScanReport report = InfinityBatchScan.Run(
			new List<InfinityBatchScan.Candidate> { first, twin }, Seeds,
			InfinityAttribution.DefaultDummySize, InfinityAttribution.DefaultDummyHp, null, false);

		Assert.AreEqual(1, report.scanned, "deck rows are per-upload, so duplicates are common — scan the content once");
		Assert.AreEqual(1, report.deduped);
		Assert.AreEqual(InfinityBatchScan.StatusDuplicate, report.decks[1].status);
	}

	[Test]
	public void Scan_AlreadyFlaggedDeck_IsSkipped()
	{
		InfinityBatchScan.Candidate candidate = CandidateFromSampleDeck("lethal infinite test", deckId: 200);
		candidate.AlreadyFlagged = true;

		InfinityBatchScan.ScanReport report = InfinityBatchScan.Run(
			new List<InfinityBatchScan.Candidate> { candidate }, Seeds,
			InfinityAttribution.DefaultDummySize, InfinityAttribution.DefaultDummyHp, null, false);

		Assert.AreEqual(1, report.skippedFlagged);
		Assert.AreEqual(0, report.scanned);
		Assert.AreEqual(InfinityBatchScan.StatusAlreadyFlagged, report.decks[0].status);
	}

	// ------------------------------------------------------------------ report shape

	[Test]
	public void Scan_WritesAReportThatSurvivesSerialization()
	{
		InfinityBatchScan.Candidate candidate = CandidateFromSampleDeck("lethal infinite test", deckId: 300);

		InfinityBatchScan.ScanReport report = InfinityBatchScan.Run(
			new List<InfinityBatchScan.Candidate> { candidate }, Seeds,
			InfinityAttribution.DefaultDummySize, InfinityAttribution.DefaultDummyHp, null, false);

		// The JSON is the contract with post_loop_reports.js and with a later reader of the file.
		string json = JsonUtility.ToJson(report, true);
		var roundTrip = JsonUtility.FromJson<InfinityBatchScan.ScanReport>(json);
		Assert.AreEqual(report.infinite, roundTrip.infinite);
		Assert.AreEqual(report.decks.Count, roundTrip.decks.Count);
		Assert.AreEqual(report.decks[0].deckId, roundTrip.decks[0].deckId);
		Assert.AreEqual(report.decks[0].reportJson, roundTrip.decks[0].reportJson, "the payload must survive verbatim");
		Assert.IsNotEmpty(roundTrip.decks[0].cards);

		Assert.IsTrue(InfinityBatchScan.IsProven(report.decks[0]),
			"a multi-seed 1-minimal verdict is what may be posted");

		string markdown = InfinityBatchScan.ToMarkdown(report);
		StringAssert.Contains("infinity batch scan", markdown.ToLowerInvariant());
		StringAssert.Contains("infinite: 1", markdown);
		StringAssert.Contains("## Infinite — proven minimum", markdown);
		StringAssert.Contains("## Infinite — unproven", markdown,
			"the report must separate what may be posted from what may not — the first scan found 4 such decks");
		StringAssert.Contains("(proven: 1 / unproven: 0)", markdown);
		StringAssert.Contains("300", markdown, "the deck row is listed so a human can trace it");
	}

	[Test]
	public void ContentKey_IsOrderIndependentButMultisetSensitive()
	{
		var a = new List<string> { "X", "Y", "X" };
		var b = new List<string> { "X", "X", "Y" };
		var c = new List<string> { "X", "Y" };

		Assert.AreEqual(InfinityBatchScan.ContentKey(a), InfinityBatchScan.ContentKey(b),
			"card order in the deck does not change what it is");
		Assert.AreNotEqual(InfinityBatchScan.ContentKey(a), InfinityBatchScan.ContentKey(c),
			"two copies are not one");
	}

	[Test]
	public void PrefabMap_ResolvesTheComboCards()
	{
		List<string> warnings = null;
		Dictionary<string, GameObject> map = InfinityBatchScan.BuildCardPrefabMap(warnings);

		Assert.IsNotEmpty(map, "the card prefab scan must find the project's cards");
		Assert.IsTrue(map.ContainsKey("RELIC_CURSE_REVIVAL"), "a 4.0 card must resolve by its cardTypeID");
		Assert.IsTrue(map.ContainsKey("CURSE_GARDENER"));
	}

	// ------------------------------------------------------------------ helpers

	private static InfinityBatchScan.Candidate CandidateFromSampleDeck(string assetName, int deckId,
		int limit = 0, string label = null)
	{
		var deck = AssetDatabase.LoadAssetAtPath<DeckSO>(DeckFolder + "/" + assetName + ".asset");
		Assert.IsNotNull(deck, "sample deck missing: " + assetName);

		var ids = new List<string>();
		foreach (GameObject card in deck.deck)
		{
			if (card == null) continue;
			var script = card.GetComponent<CardScript>();
			if (script == null) continue;
			ids.Add(!string.IsNullOrEmpty(script.cardTypeID) ? script.cardTypeID : script.name);
			if (limit > 0 && ids.Count >= limit) break;
		}
		Assert.IsNotEmpty(ids, "sample deck resolved to no card type ids");

		return new InfinityBatchScan.Candidate
		{
			DeckId = deckId,
			Label = label ?? (assetName + " as deck " + deckId),
			GameVersion = "4.0",
			Source = "server",
			CardTypeIDs = ids,
		};
	}
}
