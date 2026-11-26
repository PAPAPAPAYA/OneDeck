/// <summary>
/// One settled combat's outcome, resolved exactly once per combat by PhaseManager at the
/// settlement point (plans/plan-forced-draw-reporting-2026-11-26.md Step 0). Append-only:
/// never renumber, never remove — the upload DTOs (MatchReportRequest.drawKind,
/// RunCombatEntry.drawKind) and the local win-rate buckets derive from it.
/// Abnormal is unreachable by construction (the only combatFinished writers are the HP check
/// and the forced conclusion) and is kept as an assertable error state so an engine anomaly
/// cannot silently cost a heart: it records nothing and uploads as an ordinary non-draw loss.
/// </summary>
public enum CombatOutcome
{
	Win,
	Loss,
	/// <summary>Both sides at HP &lt;= 0 at settlement.</summary>
	DrawNatural,
	/// <summary>Both sides alive but the CombatBudgetGuard conclusion latch tripped (L0 cap).</summary>
	DrawForced,
	Abnormal
}

/// <summary>
/// Pure resolver for <see cref="CombatOutcome"/> (static helper per the Code Placement Guide —
/// no manager state). Settlement HP is authoritative; the latch only decides the both-alive case.
/// </summary>
public static class CombatOutcomeResolver
{
	/// <summary>
	/// Resolution order: double KO -&gt; natural draw, player dead -&gt; loss, enemy dead -&gt; win,
	/// both alive with the L0 conclusion latch tripped -&gt; forced draw, both alive clean -&gt; abnormal.
	/// </summary>
	public static CombatOutcome Resolve(int playerHp, int enemyHp, bool forcedConcludeLatched)
	{
		if (playerHp <= 0 && enemyHp <= 0) return CombatOutcome.DrawNatural;
		if (playerHp <= 0) return CombatOutcome.Loss;
		if (enemyHp <= 0) return CombatOutcome.Win;
		return forcedConcludeLatched ? CombatOutcome.DrawForced : CombatOutcome.Abnormal;
	}

	/// <summary>
	/// Wire value for MatchReportRequest.drawKind / run_combats.draw_kind
	/// ("" = not a draw, "natural", "forced").
	/// </summary>
	public static string DrawKind(CombatOutcome outcome)
	{
		switch (outcome)
		{
			case CombatOutcome.DrawNatural: return "natural";
			case CombatOutcome.DrawForced: return "forced";
			default: return string.Empty;
		}
	}
}
