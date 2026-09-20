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

	[Test]
	public void Scan_IncludeFlagged_ReMeasuresTheDeck()
	{
		// §26 (2026-09-20): the stale-flag path needs an already-flagged deck to be re-measured — a
		// card change can fix a loop, and the pre-§26 scan skipped those rows outright, so an
		// outdated flag could never be retired on evidence.
		InfinityBatchScan.Candidate candidate = CandidateFromSampleDeck("lethal infinite test", deckId: 201);
		candidate.AlreadyFlagged = true;

		InfinityBatchScan.ScanReport report = InfinityBatchScan.Run(
			new List<InfinityBatchScan.Candidate> { candidate }, Seeds,
			InfinityAttribution.DefaultDummySize, InfinityAttribution.DefaultDummyHp, null, false, true);

		Assert.AreEqual(0, report.skippedFlagged, "includeFlagged must not skip the row");
		Assert.AreEqual(1, report.scanned);
		Assert.AreEqual(InfinityBatchScan.StatusInfinite, report.decks[0].status);
		Assert.IsTrue(report.decks[0].unbounded, "the re-measurement carries the criterion v2 verdict");
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

	// ------------------------------------------------------------------ ring traces

	[Test]
	public void FindTailPeriod_FindsTheShortestRepeatingWindow()
	{
		// The lethal specimen's ring: gardener reveals, the revived curse reveals, repeat.
		var alternating = new List<string>();
		for (int i = 0; i < 20; i++) { alternating.Add("O:CURSE_GARDENER"); alternating.Add("E:JU_ON"); }
		Assert.AreEqual(2, InfinityBatchScan.FindTailPeriod(alternating, out int alternatingRepeats));
		Assert.AreEqual(20, alternatingRepeats, "every period counts, including the final partial one");

		// A single-card ring (deck 109's GRAVE_HEXER x2 in the scan trace).
		var constant = new List<string> { "O:GRAVE_HEXER", "O:GRAVE_HEXER", "O:GRAVE_HEXER", "O:GRAVE_HEXER", "O:GRAVE_HEXER", "O:GRAVE_HEXER" };
		Assert.AreEqual(1, InfinityBatchScan.FindTailPeriod(constant, out int constantRepeats));
		Assert.AreEqual(6, constantRepeats);

		// Warm-up that never settles: the detector must say so rather than invent a period.
		var ramping = new List<string> { "A", "A", "B", "A", "B", "C", "A", "B", "C", "D", "A", "B" };
		Assert.AreEqual(0, InfinityBatchScan.FindTailPeriod(ramping, out int rampingRepeats));
		Assert.AreEqual(0, rampingRepeats);

		Assert.AreEqual(0, InfinityBatchScan.FindTailPeriod(new List<string> { "A", "B" }, out int tinyRepeats),
			"too short to claim a period");
		Assert.AreEqual(0, tinyRepeats);
	}

	[Test]
	public void RingsToMarkdown_ShowsTheWindowAndTheBindingEvidence()
	{
		var report = new InfinityBatchScan.RingTraceReport
		{
			generatedUtc = "2026-09-19T00:00:00Z",
			dummySize = 3,
			dummyHp = 100000000,
			rings = new List<InfinityBatchScan.RingTrace>
			{
				new InfinityBatchScan.RingTrace
				{
					deckId = 66, label = "deck 66", status = "proven",
					minimalSet = new[] { "CURSE_GARDENER", "RELIC_CURSE_REVIVAL" },
					reveals = 1500, rounds = 8, repeats = 100, tripHashRepeats = 100,
					period = 2, periodRepeats = 97,
					window = new[] { "O:CURSE_GARDENER", "E:JU_ON" },
					tail = new[] { "O:CURSE_GARDENER", "E:JU_ON" },
					roles = new[] { "CURSE_GARDENER <Engine> [OnMeRevealed -> EnhanceCurse,ReviveTheirCards]" },
				},
			},
		};

		string markdown = InfinityBatchScan.RingsToMarkdown(report);
		StringAssert.Contains("deck 66  [proven]", markdown);
		StringAssert.Contains("**repeating window: period 2, repeated x97**", markdown);
		StringAssert.Contains("O:CURSE_GARDENER -> E:JU_ON", markdown, "the ring itself must be legible");
		StringAssert.Contains("OnMeRevealed -> EnhanceCurse", markdown, "and the binding evidence for checking it");
		StringAssert.Contains("fatigue OFF", markdown);
	}

	[Test]
	public void RingsToMarkdown_ReportsRecurrenceSpacingForWideRings()
	{
		// Deck 88/89 shape: the tripping arrangement recurs, but NOT adjacently, so there is no
		// adjacent period and the report must say where it recurs rather than implying "no loop".
		var report = new InfinityBatchScan.RingTraceReport
		{
			generatedUtc = "2026-09-19T00:00:00Z", dummySize = 3, dummyHp = 100000000,
			rings = new List<InfinityBatchScan.RingTrace>
			{
				new InfinityBatchScan.RingTrace
				{
					deckId = 88, label = "deck 88", status = "infinite",
					minimalSet = new[] { "CURSE_REVIVER", "GRAVE_HEXER" },
					reveals = 1500, rounds = 15, repeats = 65, tripHashRepeats = 65,
					period = 0, periodRepeats = 0,
					recurrenceIndices = new[] { 12, 25, 41, 58 },
					recurrenceGaps = "13,16,17",
					tripHashRepeatsInRound = 65,
				},
			},
		};

		string markdown = InfinityBatchScan.RingsToMarkdown(report);
		StringAssert.Contains("no ADJACENT repeating window", markdown);
		StringAssert.Contains("recurs at reveals [12,25,41,58]", markdown);
		StringAssert.Contains("gaps between recurrences: 13,16,17", markdown,
			"spacing is what separates a wide ring from a tight one");
	}

	[Test]
	public void LoopDetectionOptions_DisableTheFatigueClock()
	{
		// The flag criterion asks "is the recursion real?". Fatigue injects inert cards that break
		// the arrangement, so measuring with it on produced false negatives (decks 107/111, §23.4).
		var production = RunBudgetSim.Options.Production();
		Assert.IsTrue(production.EnableOvertimeFatigue, "production options keep the harm-side clock");

		var detection = RunBudgetSim.Options.LoopDetection();
		Assert.IsFalse(detection.EnableOvertimeFatigue, "loop detection must not let fatigue break the loop");
		Assert.AreEqual(production.GuardTotal, detection.GuardTotal, "everything else stays production-identical");
		Assert.AreEqual(production.GuardRounds, detection.GuardRounds);
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
