using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// P5 back-catalog scan (plans/plan-infinity-detection-2026-09-17.md §23): run every known deck
/// through the same headless simulation the gate trusts, and report which ones loop on their own.
///
/// The question P5 answers IS the serving question — "would this deck, as an opponent, never
/// finish?" — so it needs only the enemy-vs-dummy leg of §4's attribution triple: an inert dummy
/// with a huge HP pool outlives any self-terminating loop, which is what exposes an unbounded one
/// (a real opponent would be killed by the same pump that makes the loop look finite).
///
/// Sources, both optional:
/// - server decks: the read-only dump produced by tools/outputs/dump_decks.py
///   (tools/outputs/decks_current.json) — these carry a deck_id, so a verdict is actionable;
/// - local recorded decks: DeckSO assets under Assets/SORefs/Decks/Recorded — design feedback
///   only, they have no server row to flag.
///
/// This class never talks to the network and never writes to the server. It computes and writes a
/// report to disk; posting a report is tools/outputs/post_loop_reports.js's job, on purpose, so
/// the data-affecting step can be dry-run and counted separately.
///
/// Runs synchronously and stalls the editor for the duration (one sim per deduplicated deck, plus
/// ddmin on the loops). Editor-only by necessity: RunBudgetSim needs SerializedObject +
/// AssetDatabase.
/// </summary>
public static class InfinityBatchScan
{
	public const string CardsRoot = "Assets/Prefabs/Cards";
	public const string RecordedDecksRoot = "Assets/SORefs/Decks/Recorded";
	public const string DumpedDecksPath = "tools/outputs/decks_current.json";
	public const string OutputDir = "tools/outputs";

	public const string StatusInfinite = "infinite";
	public const string StatusClean = "clean";
	public const string StatusUnresolved = "unresolved";
	public const string StatusAlreadyFlagged = "alreadyFlagged";
	public const string StatusDuplicate = "duplicate";

	/// <summary>Seeds every candidate is checked against. §4 requires a minimal set to be robust across seeds.</summary>
	public static readonly int[] DefaultSeeds = { 4242, 7 };

	// ------------------------------------------------------------------ inputs

	[Serializable]
	public class DumpedDeck
	{
		public int deckId;
		public string username;
		public string gameVersion;
		public int sessionNum;
		public int hpMax;
		public int flag;
		public List<string> cardTypeIDs;
	}

	[Serializable]
	public class DumpedDecks
	{
		public string generatedUtc;
		public string source;
		public int count;
		public List<DumpedDeck> decks;
	}

	public class Candidate
	{
		public int DeckId;
		public string Label;
		public string GameVersion;
		public List<string> CardTypeIDs;
		/// <summary>"server" or "recorded".</summary>
		public string Source;
		public bool AlreadyFlagged;
	}

	// ------------------------------------------------------------------ output

	[Serializable]
	public class DeckResult
	{
		public int deckId;
		public string label;
		public string source;
		public string gameVersion;
		/// <summary>infinite / clean / unresolved / alreadyFlagged / duplicate.</summary>
		public string status;
		public string[] cards;
		/// <summary>Card type ids that resolved to no prefab (status = unresolved).</summary>
		public string[] missingCards;
		public int reveals;
		public int rounds;
		public int repeats;
		public bool oneMinimal;
		public bool truncated;
		public bool multiSeedStable;
		/// <summary>The LoopReport JSON, verbatim — what post_loop_reports.js sends. Empty unless status = infinite.</summary>
		public string reportJson;
	}

	[Serializable]
	public class ScanReport
	{
		public string generatedUtc;
		public string unityVersion;
		public int dummySize;
		public int dummyHp;
		public int[] seeds;
		public int candidates;
		public int scanned;
		public int infinite;
		public int clean;
		public int unresolved;
		public int skippedFlagged;
		public int deduped;
		public List<string> warnings = new List<string>();
		public List<DeckResult> decks = new List<DeckResult>();
	}

	// ------------------------------------------------------------------ entry points

	/// <summary>
	/// Interactive entry point. Report only — it never posts anything; run
	/// tools/outputs/post_loop_reports.js afterwards if the report looks right.
	/// </summary>
	[MenuItem("Tools/Infinity/Batch Scan (report only)")]
	public static void ScanFromMenu()
	{
		ScanReport report = Run(null, DefaultSeeds, InfinityAttribution.DefaultDummySize,
			InfinityAttribution.DefaultDummyHp, null, true);
		Debug.Log("[InfinityBatchScan] " + (report != null
			? "done: " + report.scanned + " scanned, " + report.infinite + " infinite, "
				+ report.unresolved + " unresolved, " + report.skippedFlagged + " already flagged, "
				+ report.deduped + " duplicate(s)"
			: "nothing to scan (no dump file and no recorded decks)"));
	}

	/// <summary>
	/// Batch-mode entry point for a card-change / release step:
	///   Unity.exe -batchmode -quit -executeMethod InfinityBatchScan.ScanFromBatch
	/// Exits non-zero when the scan itself failed, so a pipeline can stop on it.
	/// </summary>
	public static void ScanFromBatch()
	{
		try
		{
			ScanReport report = Run(null, DefaultSeeds, InfinityAttribution.DefaultDummySize,
				InfinityAttribution.DefaultDummyHp, null, true);
			Debug.Log("[InfinityBatchScan] batch scan finished: "
				+ (report != null ? report.scanned + " scanned" : "nothing to scan"));
		}
		catch (Exception error)
		{
			Debug.LogError("[InfinityBatchScan] batch scan failed: " + error);
			EditorApplication.Exit(1);
		}
	}

	// ------------------------------------------------------------------ core

	/// <summary>
	/// Scans the candidates (null = both default sources). Pure computation plus a report file;
	/// nothing is uploaded. Returns null when there is no input at all, so a caller can tell
	/// "nothing to do" from "scanned zero".
	/// </summary>
	public static ScanReport Run(List<Candidate> candidates, int[] seeds, int dummySize, int dummyHp,
		RunBudgetSim.Options options, bool writeReport)
	{
		// The rig clears the live singletons when it is built, so a scan during Play mode would
		// tear down the running game (plan §19.6.2 has the same root).
		if (EditorApplication.isPlaying)
		{
			throw new InvalidOperationException(
				"[InfinityBatchScan] refusing to scan while the editor is in Play mode: the headless rig cleans up the live singletons.");
		}

		if (options == null) options = RunBudgetSim.Options.Production();
		if (seeds == null || seeds.Length == 0) seeds = DefaultSeeds;

		var warnings = new List<string>();
		if (candidates == null) candidates = LoadDefaultCandidates(warnings);
		if (candidates.Count == 0) return null;

		Dictionary<string, GameObject> prefabs = BuildCardPrefabMap(warnings);

		var report = new ScanReport
		{
			generatedUtc = DateTime.UtcNow.ToString("o"),
			unityVersion = Application.unityVersion,
			dummySize = dummySize,
			dummyHp = dummyHp,
			seeds = seeds,
			candidates = candidates.Count,
		};

		var seen = new HashSet<string>();
		foreach (Candidate candidate in candidates)
		{
			if (candidate == null || candidate.CardTypeIDs == null || candidate.CardTypeIDs.Count == 0) continue;

			var result = new DeckResult
			{
				deckId = candidate.DeckId,
				label = candidate.Label,
				source = candidate.Source,
				gameVersion = candidate.GameVersion,
				cards = candidate.CardTypeIDs.ToArray(),
			};
			report.decks.Add(result);

			// Dedupe by content: the same card multiset loops identically, so scanning it twice
			// buys nothing (deck rows are per-upload, so duplicates are common).
			string contentKey = ContentKey(candidate.CardTypeIDs);
			if (!seen.Add(contentKey))
			{
				result.status = StatusDuplicate;
				report.deduped++;
				continue;
			}

			if (candidate.AlreadyFlagged)
			{
				result.status = StatusAlreadyFlagged;
				report.skippedFlagged++;
				continue;
			}

			var cardPrefabs = new List<GameObject>();
			var missing = new List<string>();
			foreach (string typeID in candidate.CardTypeIDs)
			{
				if (prefabs.TryGetValue(typeID, out GameObject prefab) && prefab != null) cardPrefabs.Add(prefab);
				else missing.Add(typeID);
			}
			if (missing.Count > 0)
			{
				// Never simulate a partial deck: a dropped card can turn a loop into a non-loop,
				// and a wrong verdict here flags a real deck.
				result.status = StatusUnresolved;
				result.missingCards = missing.ToArray();
				report.unresolved++;
				Debug.LogWarning("[InfinityBatchScan] " + candidate.Label + ": " + missing.Count
					+ " card type id(s) resolve to no prefab — skipped: " + string.Join(",", missing.ToArray()));
				continue;
			}

			ScanCandidate(candidate, cardPrefabs, result, seeds, dummySize, dummyHp, options);
			report.scanned++;
			if (result.status == StatusInfinite) report.infinite++;
			else report.clean++;

			Debug.Log("[InfinityBatchScan] " + candidate.Label + ": " + result.status
				+ " (reveals=" + result.reveals + " rounds=" + result.rounds + " repeats=" + result.repeats + ")");
		}

		report.warnings = warnings;
		if (writeReport) WriteReport(report);
		return report;
	}

	/// <summary>Runs one candidate: enemy-vs-dummy, then ddmin when it loops.</summary>
	private static void ScanCandidate(Candidate candidate, List<GameObject> cardPrefabs, DeckResult result,
		int[] seeds, int dummySize, int dummyHp, RunBudgetSim.Options options)
	{
		var deck = ScriptableObject.CreateInstance<DeckSO>();
		deck.name = "scan-" + (candidate.DeckId > 0 ? candidate.DeckId.ToString() : candidate.Label);
		deck.deck = new List<GameObject>(cardPrefabs);
		try
		{
			BudgetTripReport trip = RunBudgetSim.RunVsDummy(deck, dummySize, dummyHp, seeds[0], options);
			result.reveals = trip.TotalReveals;
			result.rounds = trip.Rounds;
			result.repeats = trip.MaxSightingsOfOneArrangement;

			if (!trip.SuspectedInfinite)
			{
				result.status = StatusClean;
				return;
			}

			MinimizeResult minimized = ComboMinimizer.Minimize(deck, seeds, dummySize, dummyHp, options);
			result.oneMinimal = minimized.IsOneMinimal;
			result.truncated = minimized.Truncated;
			result.multiSeedStable = minimized.MultiSeedStable;

			// Reuse the runtime pipeline's builder so a scan entry has exactly the shape a live
			// report has (roles evidence included). The verdict is the EnemyDeck leg by
			// construction: this deck looped against an inert dummy, i.e. on its own.
			var attribution = new InfinityAttributionResult
			{
				OwnerDeckName = "batch-scan",
				EnemyDeckName = candidate.Label,
				Seed = seeds[0],
				Responsibility = InfinityResponsibility.EnemyDeck,
				EnemyVsDummy = trip,
			};
			LoopReport report = LoopReportBuilder.Build(attribution, minimized, seeds, trip);
			result.reportJson = report.ToJson();
			result.status = StatusInfinite;
		}
		finally
		{
			UnityEngine.Object.DestroyImmediate(deck);
		}
	}

	// ------------------------------------------------------------------ sources

	/// <summary>Server dump first, then the local recorded decks. Missing inputs are warnings, not failures.</summary>
	public static List<Candidate> LoadDefaultCandidates(List<string> warnings)
	{
		var candidates = new List<Candidate>();
		candidates.AddRange(LoadDumpedDecks(warnings));
		candidates.AddRange(LoadRecordedDecks(warnings));
		return candidates;
	}

	/// <summary>Reads tools/outputs/decks_current.json (written by tools/outputs/dump_decks.py).</summary>
	public static List<Candidate> LoadDumpedDecks(List<string> warnings)
	{
		var candidates = new List<Candidate>();
		string path = Path.Combine(ProjectRoot(), DumpedDecksPath);
		if (!File.Exists(path))
		{
			if (warnings != null)
			{
				warnings.Add("no server deck dump at " + DumpedDecksPath
					+ " — run tools/outputs/dump_decks.py --prod first (local recorded decks still scanned)");
			}
			return candidates;
		}

		DumpedDecks dumped = null;
		try
		{
			dumped = JsonUtility.FromJson<DumpedDecks>(File.ReadAllText(path, Encoding.UTF8));
		}
		catch (Exception error)
		{
			if (warnings != null) warnings.Add("deck dump unreadable (" + error.Message + ")");
			return candidates;
		}
		if (dumped == null || dumped.decks == null) return candidates;

		foreach (DumpedDeck deck in dumped.decks)
		{
			if (deck == null || deck.cardTypeIDs == null || deck.cardTypeIDs.Count == 0) continue;
			candidates.Add(new Candidate
			{
				DeckId = deck.deckId,
				Label = "deck " + deck.deckId + " (" + deck.username + ", " + deck.gameVersion
					+ ", session " + deck.sessionNum + ")",
				GameVersion = deck.gameVersion,
				CardTypeIDs = deck.cardTypeIDs,
				Source = "server",
				AlreadyFlagged = deck.flag != 0,
			});
		}
		return candidates;
	}

	/// <summary>Every DeckSO under Assets/SORefs/Decks/Recorded (dev test decks; no server row to flag).</summary>
	public static List<Candidate> LoadRecordedDecks(List<string> warnings)
	{
		var candidates = new List<Candidate>();
		if (!AssetDatabase.IsValidFolder(RecordedDecksRoot))
		{
			if (warnings != null) warnings.Add("no recorded deck folder at " + RecordedDecksRoot);
			return candidates;
		}

		foreach (string guid in AssetDatabase.FindAssets("t:DeckSO", new[] { RecordedDecksRoot }))
		{
			string assetPath = AssetDatabase.GUIDToAssetPath(guid);
			var deck = AssetDatabase.LoadAssetAtPath<DeckSO>(assetPath);
			if (deck == null || deck.deck == null || deck.deck.Count == 0) continue;

			var ids = new List<string>();
			foreach (GameObject card in deck.deck)
			{
				if (card == null) continue;
				var script = card.GetComponent<CardScript>();
				if (script == null) continue;
				ids.Add(!string.IsNullOrEmpty(script.cardTypeID) ? script.cardTypeID : script.name);
			}
			if (ids.Count == 0) continue;

			candidates.Add(new Candidate
			{
				DeckId = 0,
				Label = deck.name,
				GameVersion = Application.version,
				CardTypeIDs = ids,
				Source = "recorded",
			});
		}
		return candidates;
	}

	// ------------------------------------------------------------------ card prefab map

	/// <summary>
	/// cardTypeID -> prefab, scanning every card prefab in the project. There is no reusable
	/// editor-side resolver (DeckSaver's is runtime and pool-driven, so a card the pool does not
	/// list would silently be "unknown"), so this mirrors CardTypeIDValidator's AssetDatabase scan.
	/// </summary>
	public static Dictionary<string, GameObject> BuildCardPrefabMap(List<string> warnings)
	{
		var map = new Dictionary<string, GameObject>();
		foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { CardsRoot }))
		{
			string path = AssetDatabase.GUIDToAssetPath(guid);
			var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
			if (prefab == null) continue;
			var script = prefab.GetComponent<CardScript>();
			if (script == null) continue;

			string typeID = !string.IsNullOrEmpty(script.cardTypeID) ? script.cardTypeID : prefab.name;
			if (string.IsNullOrEmpty(typeID)) continue;

			if (map.ContainsKey(typeID))
			{
				if (warnings != null) warnings.Add("duplicate cardTypeID '" + typeID + "' — keeping " + path);
				continue;
			}
			map[typeID] = prefab;
		}
		return map;
	}

	// ------------------------------------------------------------------ report output

	private static string ProjectRoot()
	{
		// Application.dataPath = <project>/Assets
		return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
	}

	public static string WriteReport(ScanReport report)
	{
		string dir = Path.Combine(ProjectRoot(), OutputDir);
		Directory.CreateDirectory(dir);
		string stamp = report.generatedUtc.Replace(":", "-").Replace(".", "-");
		string jsonPath = Path.Combine(dir, "infinity_scan_" + stamp + ".json");
		File.WriteAllText(jsonPath, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
		File.WriteAllText(Path.Combine(dir, "infinity_scan_" + stamp + ".md"), ToMarkdown(report), new UTF8Encoding(false));
		Debug.Log("[InfinityBatchScan] report written: " + jsonPath);
		return jsonPath;
	}

	public static string ToMarkdown(ScanReport report)
	{
		var sb = new StringBuilder();
		sb.Append("# Infinity batch scan — ").AppendLine(report.generatedUtc);
		sb.AppendLine();
		sb.Append("- candidates: ").Append(report.candidates)
			.Append(" | scanned: ").Append(report.scanned)
			.Append(" | **infinite: ").Append(report.infinite).Append("**")
			.Append(" (proven: ").Append(CountProven(report)).Append(" / unproven: ").Append(report.infinite - CountProven(report)).Append(")")
			.Append(" | clean: ").Append(report.clean)
			.Append(" | unresolved: ").Append(report.unresolved)
			.Append(" | already flagged: ").Append(report.skippedFlagged)
			.Append(" | duplicates: ").Append(report.deduped).AppendLine();
		sb.Append("- dummy: ").Append(report.dummySize).Append(" cards @ ").Append(report.dummyHp).Append(" HP");
		sb.Append(" | seeds: ").Append(string.Join(",", Array.ConvertAll(report.seeds, s => s.ToString())));
		sb.Append(" | unity: ").AppendLine(report.unityVersion);
		sb.AppendLine();

		if (report.warnings != null && report.warnings.Count > 0)
		{
			sb.AppendLine("## Warnings");
			foreach (string warning in report.warnings) sb.Append("- ").AppendLine(warning);
			sb.AppendLine();
		}

		sb.AppendLine("## Infinite — proven minimum (postable: tools/outputs/post_loop_reports.js)");
		AppendRows(sb, report, StatusInfinite, IsProven);
		sb.AppendLine("## Infinite — unproven (NOT posted by default: single-seed, or ddmin found no 1-minimal core)");
		AppendRows(sb, report, StatusInfinite, deck => !IsProven(deck));
		sb.AppendLine("## Already flagged on the server (skipped)");
		AppendRows(sb, report, StatusAlreadyFlagged, null);
		sb.AppendLine("## Unresolved (a card type id has no prefab — never simulated)");
		AppendRows(sb, report, StatusUnresolved, null);
		sb.AppendLine("## Clean");
		AppendRows(sb, report, StatusClean, null);
		return sb.ToString();
	}

	/// <summary>
	/// The §4/§21 ingest gate, mirrored by post_loop_reports.js: only a 1-minimal, multi-seed
	/// stable, untruncated verdict is strong enough to accuse a real player's deck.
	/// </summary>
	public static bool IsProven(DeckResult deck)
	{
		return deck != null && deck.oneMinimal && !deck.truncated && deck.multiSeedStable;
	}

	private static int CountProven(ScanReport report)
	{
		int count = 0;
		foreach (DeckResult deck in report.decks)
		{
			if (deck.status == StatusInfinite && IsProven(deck)) count++;
		}
		return count;
	}

	private static void AppendRows(StringBuilder sb, ScanReport report, string status, Func<DeckResult, bool> predicate)
	{
		int count = 0;
		sb.AppendLine();
		sb.AppendLine("| deck | source | cards | reveals | rounds | repeats | 1-min | truncated | multi-seed |");
		sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
		foreach (DeckResult deck in report.decks)
		{
			if (deck.status != status) continue;
			if (predicate != null && !predicate(deck)) continue;
			count++;
			sb.Append("| ").Append(deck.deckId > 0 ? deck.deckId.ToString() : deck.label)
				.Append(" | ").Append(deck.source)
				.Append(" | ").Append(deck.cards != null ? string.Join(",", deck.cards) : "")
				.Append(" | ").Append(deck.reveals)
				.Append(" | ").Append(deck.rounds)
				.Append(" | ").Append(deck.repeats)
				.Append(" | ").Append(deck.oneMinimal)
				.Append(" | ").Append(deck.truncated)
				.Append(" | ").Append(deck.multiSeedStable).AppendLine(" |");
		}
		if (count == 0) sb.AppendLine("(none)");
		sb.AppendLine();
	}

	/// <summary>Multiset key: sorted card ids, joined. Two decks with the same key loop identically.</summary>
	public static string ContentKey(List<string> cardTypeIDs)
	{
		var sorted = new List<string>(cardTypeIDs);
		sorted.Sort(StringComparer.Ordinal);
		return string.Join("|", sorted.ToArray());
	}

	// ------------------------------------------------------------------ ring traces

	/// <summary>
	/// One deck's loop made readable: the minimal set, the reveal sequence, and the shortest
	/// repeating window found in it. The hash proves a repeat; this shows WHAT repeats.
	/// </summary>
	[Serializable]
	public class RingTrace
	{
		public int deckId;
		public string label;
		public string status;
		public string[] minimalSet;
		public int reveals;
		public int rounds;
		public int repeats;
		public int tripHashRepeats;
		public int period;
		public int periodRepeats;
		public string[] window;
		public string[] tail;
		public string[] roles;
	}

	[Serializable]
	public class RingTraceReport
	{
		public string generatedUtc;
		public int dummySize;
		public int dummyHp;
		public int periodSeeds;
		public List<RingTrace> rings = new List<RingTrace>();
	}

	/// <summary>
	/// Traces the rings of the decks in the newest scan report: for each entry the scanner called
	/// infinite, re-run its MINIMAL set (that is the ring — the full deck is just padding) with the
	/// reveal trace on, then find the repeating window. Report only, like the scan itself.
	/// </summary>
	[MenuItem("Tools/Infinity/Trace Rings From Last Scan")]
	public static void TraceRingsFromMenu()
	{
		string path = TraceRingsFromLastScan(null, DefaultSeeds,
			InfinityAttribution.DefaultDummySize, InfinityAttribution.DefaultDummyHp, null, true);
		Debug.Log("[InfinityBatchScan] ring trace " + (path != null ? "written: " + path : "skipped (no scan report)"));
	}

	public static string TraceRingsFromLastScan(ScanReport scan, int[] seeds, int dummySize, int dummyHp,
		RunBudgetSim.Options options, bool writeReport)
	{
		if (EditorApplication.isPlaying)
		{
			throw new InvalidOperationException(
				"[InfinityBatchScan] refusing to trace while the editor is in Play mode: the rig cleans up the live singletons.");
		}
		if (scan == null) scan = LoadNewestScanReport();
		if (scan == null || scan.decks == null)
		{
			Debug.LogWarning("[InfinityBatchScan] no scan report to trace — run the batch scan first");
			return null;
		}

		if (options == null) options = RunBudgetSim.Options.Production();
		// Fatigue OFF for the trace (the scan itself keeps production options): with the
		// reveal-count clock running, the engine injects SYSTEM_FATIGUE cards into the deck every
		// few reveals, and a period search over that trace latches onto the fatigue cadence
		// instead of the loop — measured 2026-09-19: period 1 "O:SYSTEM_FATIGUE" for a deck whose
		// real ring is CURSE_GARDENER <-> RELIC_CURSE_REVIVAL. The ring is what repeats; the
		// fatigue clock is the thing that eventually stops it.
		options.EnableOvertimeFatigue = false;
		options.RecordRevealTrace = true;
		if (seeds == null || seeds.Length == 0) seeds = DefaultSeeds;

		List<string> warnings = null;
		Dictionary<string, GameObject> prefabs = BuildCardPrefabMap(warnings);

		var report = new RingTraceReport
		{
			generatedUtc = DateTime.UtcNow.ToString("o"),
			dummySize = dummySize,
			dummyHp = dummyHp,
			periodSeeds = seeds.Length,
		};

		foreach (DeckResult deck in scan.decks)
		{
			if (deck.status != StatusInfinite || string.IsNullOrEmpty(deck.reportJson)) continue;

			var payload = JsonUtility.FromJson<LoopReport>(deck.reportJson);
			List<string> minimal = payload != null && payload.mySide != null && payload.mySide.Length > 0
				? new List<string>(payload.mySide)
				: new List<string>(deck.cards);

			var entry = new RingTrace
			{
				deckId = deck.deckId,
				label = deck.label,
				status = IsProven(deck) ? "proven" : "unproven",
				minimalSet = minimal.ToArray(),
				repeats = deck.repeats,
				roles = payload != null ? payload.roles : new string[0],
			};

			var prefabList = new List<GameObject>();
			bool resolved = true;
			foreach (string typeID in minimal)
			{
				if (prefabs.TryGetValue(typeID, out GameObject prefab) && prefab != null) prefabList.Add(prefab);
				else { resolved = false; break; }
			}
			if (!resolved)
			{
				entry.period = 0;
				entry.window = new string[] { "(a card in the minimal set has no prefab)" };
				report.rings.Add(entry);
				continue;
			}

			var traceDeck = ScriptableObject.CreateInstance<DeckSO>();
			traceDeck.name = "ring-" + (deck.deckId > 0 ? deck.deckId.ToString() : deck.label);
			traceDeck.deck = prefabList;
			try
			{
				BudgetTripReport trip = RunBudgetSim.RunVsDummy(traceDeck, dummySize, dummyHp, seeds[0], options);
				entry.reveals = trip.TotalReveals;
				entry.rounds = trip.Rounds;
				entry.tripHashRepeats = trip.MaxSightingsOfOneArrangement;

				List<string> trace = trip.RevealTrace;
				int periodRepeats = 0;
				entry.period = FindTailPeriod(trace, out periodRepeats);
				entry.periodRepeats = periodRepeats;
				entry.window = periodRepeats > 0
					? Slice(trace, trace.Count - entry.period, trace.Count)
					: new string[0];
				entry.tail = Slice(trace, Math.Max(0, trace.Count - 3 * Math.Max(entry.period, 8)), trace.Count);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(traceDeck);
			}

			report.rings.Add(entry);
			Debug.Log("[InfinityBatchScan] ring " + entry.label + ": period=" + entry.period
				+ " x" + entry.periodRepeats + " reveals=" + entry.reveals);
		}

		if (!writeReport) return null;

		string dir = Path.Combine(ProjectRoot(), OutputDir);
		Directory.CreateDirectory(dir);
		string stamp = report.generatedUtc.Replace(":", "-").Replace(".", "-");
		string jsonPath = Path.Combine(dir, "infinity_rings_" + stamp + ".json");
		File.WriteAllText(jsonPath, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
		File.WriteAllText(Path.Combine(dir, "infinity_rings_" + stamp + ".md"), RingsToMarkdown(report), new UTF8Encoding(false));
		return jsonPath;
	}

	/// <summary>
	/// Shortest window that repeats back-to-back at the end of the trace, and how many times it
	/// does. A tail check (not a whole-trace check) on purpose: a pumping loop can ramp up before
	/// it settles into the repeat, and the settling point is the ring.
	/// </summary>
	public static int FindTailPeriod(List<string> trace, out int repeats)
	{
		repeats = 0;
		if (trace == null || trace.Count < 6) return 0;

		for (int p = 1; p <= trace.Count / 3; p++)
		{
			bool matches = true;
			for (int i = trace.Count - 2 * p; i < trace.Count - p; i++)
			{
				if (trace[i] != trace[i + p]) { matches = false; break; }
			}
			if (!matches) continue;

			int matched = 2 * p;
			for (int i = trace.Count - 2 * p - 1; i >= 0 && trace[i] == trace[i + p]; i--) matched++;
			repeats = matched / p;
			return p;
		}
		return 0;
	}

	private static string[] Slice(List<string> source, int from, int to)
	{
		if (source == null || from >= to) return new string[0];
		var slice = new List<string>();
		for (int i = Math.Max(0, from); i < to && i < source.Count; i++) slice.Add(source[i]);
		return slice.ToArray();
	}

	private static ScanReport LoadNewestScanReport()
	{
		string dir = Path.Combine(ProjectRoot(), OutputDir);
		if (!Directory.Exists(dir)) return null;
		string newest = null;
		foreach (string file in Directory.GetFiles(dir, "infinity_scan_*.json"))
		{
			if (newest == null || File.GetLastWriteTimeUtc(file) > File.GetLastWriteTimeUtc(newest)) newest = file;
		}
		if (newest == null) return null;
		try { return JsonUtility.FromJson<ScanReport>(File.ReadAllText(newest, Encoding.UTF8)); }
		catch (Exception error)
		{
			Debug.LogWarning("[InfinityBatchScan] scan report unreadable: " + error.Message);
			return null;
		}
	}

	public static string RingsToMarkdown(RingTraceReport report)
	{
		var sb = new StringBuilder();
		sb.Append("# Infinity ring traces — ").AppendLine(report.generatedUtc);
		sb.AppendLine();
		sb.Append("- dummy ").Append(report.dummySize).Append(" cards @ ").Append(report.dummyHp)
			.Append(" HP | each ring is the MINIMAL set alone (the full deck is padding)").AppendLine();
		sb.Append("- traced with overtime fatigue OFF, so the sequence shows the loop itself rather ")
			.AppendLine("than the fatigue clock that eventually stops it (the scan keeps production options)");
		sb.AppendLine();
		foreach (RingTrace ring in report.rings)
		{
			sb.Append("## deck ").Append(ring.deckId > 0 ? ring.deckId.ToString() : ring.label)
				.Append("  [").Append(ring.status).Append(']').AppendLine();
			sb.AppendLine();
			sb.Append("- minimal set: `").Append(string.Join("`, `", ring.minimalSet)).AppendLine("`");
			sb.Append("- reveals: ").Append(ring.reveals).Append(" | rounds: ").Append(ring.rounds)
				.Append(" | arrangement repeats (trip evidence): ").Append(ring.tripHashRepeats).AppendLine();
			if (ring.period > 0)
			{
				sb.Append("- **repeating window: period ").Append(ring.period).Append(", repeated x")
					.Append(ring.periodRepeats).Append("**").AppendLine();
				sb.AppendLine();
				sb.Append("  ```").AppendLine();
				sb.Append("  ").AppendLine(string.Join(" -> ", ring.window));
				sb.Append("  ```").AppendLine();
			}
			else
			{
				sb.Append("- no strict tail period (the loop may still be ramping at the cap)").AppendLine();
			}
			if (ring.tail != null && ring.tail.Length > 0)
			{
				sb.AppendLine();
				sb.Append("  last ").Append(ring.tail.Length).Append(" reveals: `")
					.Append(string.Join(" ", ring.tail)).AppendLine("`");
			}
			if (ring.roles != null && ring.roles.Length > 0)
			{
				sb.AppendLine();
				sb.AppendLine("  binding evidence:");
				foreach (string role in ring.roles) sb.Append("  - `").Append(role).AppendLine("`");
			}
			sb.AppendLine();
		}
		return sb.ToString();
	}
}
