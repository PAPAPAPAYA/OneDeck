using System.Text;

/// <summary>
/// Result of one headless combat run (plans/plan-infinity-detection-2026-09-17.md §2:
/// RunBudgetSim -> BudgetTripReport). Deliberately separable from RunBudgetSim so the P2
/// attribution pass, the P5 batch scan and the editor tests can all consume one shape.
/// Two independent verdicts, matching the plan's split:
/// - SuspectedInfinite = the arrangement-cycle detector tripped. That is the FLAG criterion
///   (§3/§4, restated 2026-09-18 and corrected 2026-09-19 in §15): unbounded recursion, not
///   "ran out of budget".
/// - BudgetCapConcluded = the L0 hard caps had to force the combat to end. That is the
///   player-visible harm ("can't finish"), a telemetry/budget signal, not the flag criterion.
/// [Serializable] so a batch scan can emit JsonUtility-formatted reports without a second type.
/// </summary>
[System.Serializable]
public class BudgetTripReport
{
	// ---- identity / reproduction ----
	public string DeckA = "";
	public string DeckB = "";
	public int Seed;

	// ---- loop evidence: the flag criterion ----
	public bool ArrangementCycleTripped;
	public uint TripHash;
	public int TripCount;
	/// <summary>Most sightings of one identical arrangement inside a single round (telemetry since criterion v2).</summary>
	public int MaxSightingsOfOneArrangement;
	/// <summary>Distinct arrangements seen in the round that contained the most of them.</summary>
	public int DistinctArrangementsInWorstRound;
	/// <summary>Criterion v2 (§26): cycle repetitions of the periodic run that tripped.</summary>
	public int TripCycles;
	/// <summary>Criterion v2 (§26): period of the tripping run.</summary>
	public int TripPeriod;
	/// <summary>Criterion v2 (§26): reveals sampled in the round the trip happened in.</summary>
	public int TripRevealsInRound;
	/// <summary>Criterion v2 (§26): the round was starved (reveals &gt; 3 x round-start pool), i.e. the cycle did not need the round boundary to continue.</summary>
	public bool TripRoundStarved;
	/// <summary>Criterion v2 (§26): combined-deck size captured at the tripping round's start.</summary>
	public int TripRoundStartPool;

	// ---- engine budget evidence (L0 "can't finish") ----
	public bool RoundForceClearUsed;
	public bool BudgetCapConcluded;
	public int PeakCascadeDepth;

	// ---- shape ----
	public int Iterations;
	public int TotalReveals;
	public int Rounds;
	public int FinalDeckSize;
	public int PeakRevealsInOneRound;
	public int PeakPoolSize;

	// ---- outcome ----
	public bool EnemyDied;
	public bool OwnerDied;
	public int OwnerHpFinal;
	public int EnemyHpFinal;

	/// <summary>
	/// Every reveal in order, as "side:cardTypeID" (side O/E/N, a leading * marks the Start Card).
	/// Off by default (Options.RecordRevealTrace) because it is only worth its memory when someone
	/// wants to READ the loop rather than trust the hash — the P5 ring check and design reviews.
	/// </summary>
	public System.Collections.Generic.List<string> RevealTrace = new System.Collections.Generic.List<string>();

	/// <summary>
	/// The arrangement hash sampled after each reveal (same guard as RevealTrace). RevealTrace says
	/// WHICH cards came out; this says WHEN the arrangement repeated — and the difference matters: a
	/// tight loop repeats the same hash consecutively, while a wide loop (decks 88/89) recurs with
	/// other reveals in between, so "repeats = 65" does not mean "65 in a row".
	/// </summary>
	public System.Collections.Generic.List<uint> RevealHashes = new System.Collections.Generic.List<uint>();

	/// <summary>The plan's flag criterion (§4/§15): was an unbounded recursion detected?</summary>
	public bool SuspectedInfinite
	{
		get { return ArrangementCycleTripped; }
	}

	/// <summary>Worth handing to the P2 attribution pass: either signal fired.</summary>
	public bool NeedsAttribution
	{
		get { return ArrangementCycleTripped || BudgetCapConcluded; }
	}

	/// <summary>False when the L0 caps had to force the end — the "can't finish" harm.</summary>
	public bool FinishedNaturally
	{
		get { return !BudgetCapConcluded; }
	}

	/// <summary>Compact one-liner for logs and scan reports.</summary>
	public string Summary
	{
		get
		{
			var sb = new StringBuilder();
			sb.Append(DeckA).Append(" vs ").Append(DeckB)
				.Append(" seed=").Append(Seed)
				.Append(" infinite=").Append(SuspectedInfinite ? "YES" : "no");
			if (ArrangementCycleTripped)
				sb.Append(" [cycle hash=").Append(RngDigest.ToHex(TripHash))
					.Append(" cycles=").Append(TripCycles)
					.Append("(p=").Append(TripPeriod).Append(")")
					.Append(" starved=").Append(TripRoundStarved)
					.Append(" repeats=").Append(MaxSightingsOfOneArrangement)
					.Append(" trippedAt=round-internal]");
			sb.Append(" reveals=").Append(TotalReveals)
				.Append(" rounds=").Append(Rounds)
				.Append(" iters=").Append(Iterations)
				.Append(" deck=").Append(FinalDeckSize)
				.Append(" oHp=").Append(OwnerHpFinal)
				.Append(" eHp=").Append(EnemyHpFinal);
			if (BudgetCapConcluded) sb.Append(" L0-CONCLUDED");
			if (RoundForceClearUsed) sb.Append(" perRoundClear=yes");
			if (PeakCascadeDepth > 0) sb.Append(" cascadePeak=").Append(PeakCascadeDepth);
			return sb.ToString();
		}
	}

	public override string ToString()
	{
		return Summary;
	}
}
