using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Editor-side deferred processor for the P2 attribution queue
/// (plans/plan-infinity-detection-2026-09-17.md §19.5; user ruling 2026-09-19: no simulation
/// inside the combat frame). The detector only QUEUES a trip (InfinityTripJournal, runtime);
/// this drains it later and does the expensive work.
/// Editor-only by necessity: RunBudgetSim needs SerializedObject + AssetDatabase, so a shipped
/// build cannot attribute at all — it uploads the queue and the attribution happens offline
/// (P5 batch scan / server side). Callers in the editor decide WHEN: a menu item, an editor
/// update tick that first checks "no combat is running", or a test.
/// </summary>
public static class InfinityAttributionProcessor
{
	public static readonly int[] DefaultSeeds = { 4242, 7 };

	/// <summary>One attributed trip: the combo entry plus the ghost deck row it accuses.</summary>
	public class AttributedEntry
	{
		public LoopReport Report;
		/// <summary>decks.deck_id of the accused ghost; 0 when the enemy was a local pool deck.</summary>
		public int EnemyDeckId;
	}

	/// <summary>
	/// Drains the journal and turns each trip into a combo-library entry.
	/// A trip that does not reproduce headlessly yields NOTHING (never a guess), and a pair-only
	/// verdict yields an entry with an empty mySide — recorded per §7.1, flagging nobody.
	/// </summary>
	public static List<AttributedEntry> ProcessPendingEntries(int[] seeds = null, RunBudgetSim.Options options = null,
		int dummySize = InfinityAttribution.DefaultDummySize, int dummyHp = InfinityAttribution.DefaultDummyHp)
	{
		if (seeds == null || seeds.Length == 0) seeds = DefaultSeeds;
		if (options == null) options = RunBudgetSim.Options.Production();

		var entries = new List<AttributedEntry>();
		foreach (var entry in InfinityTripJournal.Drain())
		{
			var attribution = InfinityAttribution.Attribute(entry.PlayerDeck, entry.EnemyDeck, entry.CombatSeed,
				options, dummySize, dummyHp);

			if (attribution.Responsibility == InfinityResponsibility.None)
			{
				continue;
			}

			DeckSO responsible = null;
			BudgetTripReport simEvidence = null;
			switch (attribution.Responsibility)
			{
				case InfinityResponsibility.EnemyDeck:
					responsible = entry.EnemyDeck;
					simEvidence = attribution.EnemyVsDummy;
					break;
				case InfinityResponsibility.OwnerDeck:
					responsible = entry.PlayerDeck;
					simEvidence = attribution.OwnerVsDummy;
					break;
				default:
					// PairOnly: nothing to minimize and nobody to flag (§7.1).
					simEvidence = attribution.PairReplay;
					break;
			}

			MinimizeResult minimized = responsible != null
				? ComboMinimizer.Minimize(responsible, seeds, dummySize, dummyHp, options)
				: null;

			var report = LoopReportBuilder.Build(attribution, minimized, seeds, simEvidence);
			report.liveTripSignal = "arrangement-cycle " + RngDigest.ToHex(entry.TripHash)
				+ " repeats=" + entry.SightingsThisRound
				+ " combatSeed=" + entry.CombatSeed
				+ " captured=" + entry.CapturedUtc;
			entries.Add(new AttributedEntry { Report = report, EnemyDeckId = entry.EnemyDeckId });
		}
		return entries;
	}

	/// <summary>Attribution only — no upload. Keeps tests and offline analysis out of the outbox.</summary>
	public static List<LoopReport> ProcessPending(int[] seeds = null, RunBudgetSim.Options options = null,
		int dummySize = InfinityAttribution.DefaultDummySize, int dummyHp = InfinityAttribution.DefaultDummyHp)
	{
		var reports = new List<LoopReport>();
		foreach (var entry in ProcessPendingEntries(seeds, options, dummySize, dummyHp))
		{
			reports.Add(entry.Report);
		}
		return reports;
	}

	/// <summary>
	/// Attribution + upload (plan §20.3): every attributed entry is queued for POST
	/// /api/loop-reports, which is what actually flags a ghost deck's content server-side. The
	/// queue is drained by the existing outbox flush triggers, not here.
	/// Caller decides WHEN (§19.6.2 is still open) and must not run this while the live game owns
	/// the singletons — HeadlessCombatRig.Create clears them (CombatManager.Me etc).
	/// </summary>
	public static List<LoopReport> ProcessPendingAndUpload(int[] seeds = null, RunBudgetSim.Options options = null,
		int dummySize = InfinityAttribution.DefaultDummySize, int dummyHp = InfinityAttribution.DefaultDummyHp)
	{
		var reports = new List<LoopReport>();
		foreach (var entry in ProcessPendingEntries(seeds, options, dummySize, dummyHp))
		{
			var report = entry.Report;
			reports.Add(report);
			// Only an "infinite by itself" verdict may flag (the server ignores the rest), and only
			// a server-backed ghost has a row to flag. Local-pool adversaries are skipped here.
			string verdict = entry.Report.responsibility;
			bool queued = LoopReportUploader.EnqueueEvidence(entry.EnemyDeckId,
				verdict == "EnemyDeck" ? LoopReportUploader.VerdictEnemyDeck : verdict,
				entry.Report.reproSeeds.Length > 0 ? entry.Report.reproSeeds[0] : 0,
				report.Summary() + " | " + report.liveTripSignal, report.ToJson());
			if (!queued)
			{
				Debug.Log("[InfinityAttribution] report not queued for upload (local-pool enemy, "
					+ "server disabled, switch off, or no identity): " + report.Summary());
			}
		}
		return reports;
	}
}
