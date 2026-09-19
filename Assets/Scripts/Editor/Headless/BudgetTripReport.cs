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
	/// <summary>Most sightings of one identical arrangement inside a single round.</summary>
	public int MaxSightingsOfOneArrangement;
	/// <summary>Distinct arrangements seen in the round that contained the most of them.</summary>
	public int DistinctArrangementsInWorstRound;

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
