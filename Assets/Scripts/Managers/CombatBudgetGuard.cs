using DefaultNamespace.Managers;
using UnityEngine;

/// <summary>Which global budget cap produced the forced conclusion (L0).</summary>
public enum BudgetCapKind
{
	None = 0,
	Rounds,
	TotalReveals
}

/// <summary>
/// Immutable facts latched at the frame a global budget cap trips (draw plan Step 1a).
/// HP is deliberately absent: settlement HP is authoritative and two snapshots would be
/// ambiguous. Lives only until CombatBudgetGuard.ResetState (CombatManager.ExitCombat);
/// ExitCombat copies it into CombatManager.lastCombatConclusion before the reset.
/// </summary>
public class BudgetCapConclusion
{
	public readonly BudgetCapKind kind;
	public readonly int round;
	public readonly int totalReveals;
	public readonly int poolSize;
	public readonly int peakCascadeDepth;

	public BudgetCapConclusion(BudgetCapKind kind, int round, int totalReveals, int poolSize, int peakCascadeDepth)
	{
		this.kind = kind;
		this.round = round;
		this.totalReveals = totalReveals;
		this.poolSize = poolSize;
		this.peakCascadeDepth = peakCascadeDepth;
	}

	/// <summary>run_combats.forced_reason value ("rounds" / "totalReveals"; "" when not a cap trip).</summary>
	public string ForcedReason
	{
		get
		{
			switch (kind)
			{
				case BudgetCapKind.Rounds: return "rounds";
				case BudgetCapKind.TotalReveals: return "totalReveals";
				default: return string.Empty;
			}
		}
	}

	/// <summary>Human-readable trip reason for the FORCE CONCLUDE COMBAT log line.</summary>
	public string DescribeReason()
	{
		switch (kind)
		{
			case BudgetCapKind.Rounds: return "global round cap hit (round=" + round + ")";
			case BudgetCapKind.TotalReveals: return "global total-reveal cap hit (totalReveals=" + totalReveals + ")";
			default: return "global budget cap hit";
		}
	}
}

/// <summary>
/// L0 hard-stop budget for infinity defense (plans/plan-infinity-detection-2026-09-17.md,
/// 2026-09-17). Auto-created on the CombatManager GameObject in CombatManager.Awake.
/// Three independent fuses guarantee every combat terminates in bounded time:
/// - Per-round reveal cap: past the cap the rest of the round flips cards WITHOUT effect
///   processing (the Start Card shuffle still fires so the round boundary survives).
/// - Global total-reveal cap / global round cap: force-conclude combat. The conclusion is a
///   DRAW (plans/plan-forced-draw-reporting-2026-11-26.md; replaces the §7.7 HP-ratio
///   placeholder): no HP is touched and no heart is lost — PhaseManager resolves
///   CombatOutcome.DrawForced from the ConclusionTripped latch.
/// Also feeds passive budget telemetry (per-round reveal peak / cascade depth peak /
/// combined pool peak) into the DeterminismDigest for threshold calibration.
/// The per-round bury-cascade cap was dropped by ruling (2026-09-17): the chainDepth fuse
/// already bounds within-cascade recursion.
/// </summary>
public class CombatBudgetGuard : MonoBehaviour
{
	public static CombatBudgetGuard Me { get; private set; }

	private void Awake()
	{
		Me = this;
	}

	[Header("Hard-Stop Budget (0 = disabled)")]
	[Tooltip("Per-round reveal cap; past it the round continues with all non-Start-Card reveal effects skipped.")]
	public int maxRevealsPerRound = 200;
	[Tooltip("Global total-reveal cap for the whole combat; hitting it force-concludes combat.")]
	public int maxTotalReveals = 1500;
	[Tooltip("Global round cap for the whole combat; hitting it force-concludes combat.")]
	public int maxRounds = 60;

	/// <summary>True once the per-round cap tripped: reveal effects are skipped until the next round.</summary>
	public bool RoundForceClearActive { get; private set; }

	/// <summary>Set when a global cap tripped; consumed by CombatManager.ForceConcludeCombat.</summary>
	public bool ConcludeRequested { get; private set; }

	/// <summary>
	/// Single forced-conclusion latch (draw plan Step 0 / 1a): true from the trip frame until
	/// ResetState. The ONLY source of truth for a forced conclusion — PhaseManager resolves
	/// CombatOutcome.DrawForced from it and CombatManager.RevealCards retries settlement off it;
	/// there is no pending-conclusion copy anywhere else. Cleared only by ResetState.
	/// </summary>
	public bool ConclusionTripped { get; private set; }

	/// <summary>Facts latched at the trip frame; non-null exactly while ConclusionTripped.</summary>
	public BudgetCapConclusion Conclusion { get; private set; }

	/// <summary>Cascade depth peak seen since the last reset (telemetry; tests read it too).</summary>
	public int PeakCascadeDepth { get; private set; }

	private bool _concluded;

	public void ResetState()
	{
		RoundForceClearActive = false;
		ConcludeRequested = false;
		ConclusionTripped = false;
		Conclusion = null;
		_concluded = false;
		PeakCascadeDepth = 0;
	}

	public void NotifyRoundStart()
	{
		RoundForceClearActive = false;
	}

	/// <summary>Cascade depth reported by EffectChainManager.ResetGenerationGuards before clearing.</summary>
	public void NotifyCascadeDepth(int depth)
	{
		if (depth > PeakCascadeDepth) PeakCascadeDepth = depth;
	}

	/// <summary>
	/// Called once per revealed card (CombatManager.RevealNextCardCore). Updates the
	/// passive telemetry peaks, then evaluates the per-round and global fuses.
	/// </summary>
	public void NotifyReveal(int cardsRevealedThisRound, int totalCardsRevealed, int poolSize, int currentRound)
	{
		DeterminismTracker.RecordBudgetPeaks(cardsRevealedThisRound, PeakCascadeDepth, poolSize);

		if (_concluded) return;

		// Global caps first: they outrank the per-round clear.
		if ((maxTotalReveals > 0 && totalCardsRevealed >= maxTotalReveals)
			|| (maxRounds > 0 && currentRound >= maxRounds))
		{
			_concluded = true;
			RoundForceClearActive = true;
			ConcludeRequested = true;
			// Draw plan Step 1a: latch the facts at the trip frame. Both caps can trip on the
			// same reveal; the round cap is the one reported then.
			ConclusionTripped = true;
			Conclusion = new BudgetCapConclusion(
				maxRounds > 0 && currentRound >= maxRounds ? BudgetCapKind.Rounds : BudgetCapKind.TotalReveals,
				currentRound, totalCardsRevealed, poolSize, PeakCascadeDepth);
			TestManager.Log("[CombatBudgetGuard] Global budget cap hit (totalReveals=" + totalCardsRevealed + "/" + maxTotalReveals
				+ ", round=" + currentRound + "/" + maxRounds + ") -> force conclude requested");
			if (Application.isPlaying && CombatManager.Me != null)
			{
				CombatManager.Me.ForceConcludeCombat("[CombatBudgetGuard] " + Conclusion.DescribeReason());
			}
			return;
		}

		if (!RoundForceClearActive && maxRevealsPerRound > 0 && cardsRevealedThisRound >= maxRevealsPerRound)
		{
			RoundForceClearActive = true;
			TestManager.Log("[CombatBudgetGuard] Per-round reveal cap hit (" + cardsRevealedThisRound + " >= " + maxRevealsPerRound
				+ "): skipping reveal effects for the rest of the round (Start Card shuffle still fires)");
			DrainChains();
		}
	}

	/// <summary>Test/play hook: consume the pending forced-conclusion request.</summary>
	public bool ConsumeConcludeRequest()
	{
		if (!ConcludeRequested) return false;
		ConcludeRequested = false;
		return true;
	}

	private static void DrainChains()
	{
		if (EffectChainManager.Me == null) return;
		EffectChainManager.Me.CloseOpenedChain();
		EffectChainManager.Me.ResetGenerationGuards();
	}
}
