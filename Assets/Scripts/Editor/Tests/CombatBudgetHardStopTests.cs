using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode tests for the L0 hard-stop budget guard (2026-09-17,
/// plans/plan-infinity-detection-2026-09-17.md): per-round reveal force-clear, global
/// total-reveal / round caps with a swappable placeholder conclusion, and the passive
/// telemetry peak tracking. Unit-level state machine tests; the reveal-flow wiring is
/// covered by Play validation.
/// </summary>
public class CombatBudgetHardStopTests : HeadlessCombatTestFixture
{
	private CombatBudgetGuard CreateGuard(int perRound, int total, int rounds)
	{
		var go = CreateGameObject("BudgetGuard");
		var guard = go.AddComponent<CombatBudgetGuard>();
		guard.maxRevealsPerRound = perRound;
		guard.maxTotalReveals = total;
		guard.maxRounds = rounds;
		return guard;
	}

	[Test]
	public void RoundForceClear_TripsAtCap_AndResetsNextRound()
	{
		var guard = CreateGuard(3, 0, 0);

		guard.NotifyReveal(2, 2, 10, 1);
		Assert.IsFalse(guard.RoundForceClearActive, "below the cap: effects keep processing");

		guard.NotifyReveal(3, 3, 10, 1);
		Assert.IsTrue(guard.RoundForceClearActive, "per-round cap hit: rest of the round skips reveal effects");

		guard.NotifyRoundStart();
		Assert.IsFalse(guard.RoundForceClearActive, "new round resets the per-round force-clear");
	}

	[Test]
	public void GlobalTotalReveals_TripsConclude_Once()
	{
		var guard = CreateGuard(0, 100, 0);

		guard.NotifyReveal(1, 100, 10, 1);
		Assert.IsTrue(guard.ConcludeRequested, "total-reveal cap trips the forced conclusion");
		Assert.IsTrue(guard.RoundForceClearActive, "concluded combat also clears remaining rounds");

		Assert.IsTrue(guard.ConsumeConcludeRequest(), "pending request consumed");
		Assert.IsFalse(guard.ConsumeConcludeRequest(), "consume is one-shot");

		guard.NotifyReveal(2, 101, 10, 1);
		Assert.IsFalse(guard.ConcludeRequested, "concluded latch: no re-fire after consumption");
	}

	[Test]
	public void GlobalRounds_TripsConclude()
	{
		var guard = CreateGuard(0, 0, 30);

		guard.NotifyReveal(1, 5, 10, 29);
		Assert.IsFalse(guard.ConcludeRequested, "round 29 below the cap");

		guard.NotifyReveal(1, 5, 10, 30);
		Assert.IsTrue(guard.ConcludeRequested, "round cap trips the forced conclusion");
	}

	[Test]
	public void DisabledCaps_NeverTrip()
	{
		var guard = CreateGuard(0, 0, 0);

		guard.NotifyReveal(99999, 99999, 9999, 999);
		Assert.IsFalse(guard.RoundForceClearActive, "0 disables the per-round cap");
		Assert.IsFalse(guard.ConcludeRequested, "0 disables the global caps");
	}

	[Test]
	public void CascadeDepth_PeakTracked_AndResetStateClears()
	{
		var guard = CreateGuard(0, 0, 0);

		guard.NotifyCascadeDepth(6);
		guard.NotifyCascadeDepth(3);
		Assert.AreEqual(6, guard.PeakCascadeDepth, "peak keeps the maximum cascade depth");

		guard.ResetState();
		Assert.AreEqual(0, guard.PeakCascadeDepth, "combat cleanup resets the telemetry");
		Assert.IsFalse(guard.RoundForceClearActive);
		Assert.IsFalse(guard.ConcludeRequested);
	}
}
