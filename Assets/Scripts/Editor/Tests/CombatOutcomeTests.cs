using NUnit.Framework;

/// <summary>
/// Outcome resolution table (plans/plan-forced-draw-reporting-2026-11-26.md §5.1): the five
/// CombatOutcome states from (playerHp, enemyHp, latch) plus the drawKind wire mapping.
/// Pure math — no scene, no singletons. The Abnormal row is the "no heart or stats change"
/// contract's input side: PhaseManager keys all bookkeeping off this value.
/// </summary>
public class CombatOutcomeTests
{
	[Test]
	public void Resolve_DoubleKO_IsNaturalDraw_RegardlessOfLatch()
	{
		Assert.AreEqual(CombatOutcome.DrawNatural, CombatOutcomeResolver.Resolve(0, 0, false));
		Assert.AreEqual(CombatOutcome.DrawNatural, CombatOutcomeResolver.Resolve(0, 0, true));
		Assert.AreEqual(CombatOutcome.DrawNatural, CombatOutcomeResolver.Resolve(-3, -1, false));
	}

	[Test]
	public void Resolve_PlayerDead_EnemyAlive_IsLoss()
	{
		Assert.AreEqual(CombatOutcome.Loss, CombatOutcomeResolver.Resolve(0, 5, false));
		Assert.AreEqual(CombatOutcome.Loss, CombatOutcomeResolver.Resolve(-1, 30, true),
			"an HP death outranks the latch");
	}

	[Test]
	public void Resolve_EnemyDead_PlayerAlive_IsWin()
	{
		Assert.AreEqual(CombatOutcome.Win, CombatOutcomeResolver.Resolve(5, 0, false));
		Assert.AreEqual(CombatOutcome.Win, CombatOutcomeResolver.Resolve(1, -2, true),
			"an HP death outranks the latch");
	}

	[Test]
	public void Resolve_BothAlive_LatchTripped_IsForcedDraw()
	{
		Assert.AreEqual(CombatOutcome.DrawForced, CombatOutcomeResolver.Resolve(1, 1, true));
		Assert.AreEqual(CombatOutcome.DrawForced, CombatOutcomeResolver.Resolve(30, 7, true));
	}

	[Test]
	public void Resolve_BothAlive_NoLatch_IsAbnormal()
	{
		Assert.AreEqual(CombatOutcome.Abnormal, CombatOutcomeResolver.Resolve(5, 5, false));
		Assert.AreEqual(CombatOutcome.Abnormal, CombatOutcomeResolver.Resolve(999, 999, false));
	}

	[Test]
	public void DrawKind_MapsOnlyTheTwoDrawStates()
	{
		Assert.AreEqual("", CombatOutcomeResolver.DrawKind(CombatOutcome.Win));
		Assert.AreEqual("", CombatOutcomeResolver.DrawKind(CombatOutcome.Loss));
		Assert.AreEqual("", CombatOutcomeResolver.DrawKind(CombatOutcome.Abnormal),
			"Abnormal uploads as an ordinary non-draw loss");
		Assert.AreEqual("natural", CombatOutcomeResolver.DrawKind(CombatOutcome.DrawNatural));
		Assert.AreEqual("forced", CombatOutcomeResolver.DrawKind(CombatOutcome.DrawForced));
	}

	[Test]
	public void BudgetCapConclusion_ForcedReason_MapsTheCapKinds()
	{
		Assert.AreEqual("rounds", new BudgetCapConclusion(BudgetCapKind.Rounds, 60, 900, 40, 3).ForcedReason);
		Assert.AreEqual("totalReveals", new BudgetCapConclusion(BudgetCapKind.TotalReveals, 25, 1500, 40, 3).ForcedReason);
		Assert.AreEqual("", new BudgetCapConclusion(BudgetCapKind.None, 0, 0, 0, 0).ForcedReason);
	}
}
