using System.Collections.Generic;
using System.Reflection;
using DefaultNamespace.Managers;
using DefaultNamespace.SOScripts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// The PhaseManager settlement wiring (plans/plan-forced-draw-reporting-2026-11-26.md §5.1):
/// the resolved CombatOutcome drives every bookkeeping branch. Win/Loss keep their exact old
/// behavior; both draw kinds show DRAW with no heart/win change but still advance the
/// automation session counter and the per-session HP arrays (otherwise an all-draw autoSpace
/// batch never terminates); Abnormal changes nothing and only logs an error (expected via
/// LogAssert). Update() is invoked directly: Edit Mode does not pump MonoBehaviour.Update.
/// </summary>
public class PhaseManagerOutcomeTests : HeadlessCombatTestFixture
{
	private PhaseManager phaseManager;
	private IntSO hearts;
	private IntSO wins;
	private IntSO winCon;
	private IntSO sessionNum;
	private IntSO heartMax;

	[SetUp]
	public override void SetUp()
	{
		base.SetUp();

		// No stale guard singleton from another test class: this fixture builds its own when a
		// test needs a latched conclusion.
		typeof(CombatBudgetGuard)
			.GetProperty("Me", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
			?.SetValue(null, null);

		RunRecorder.ResetForTests();
		ServerConfig.Active = null; // ReportMatchResult/UploadIfDirty become no-ops: nothing touches the real outbox file
		StatsSnapshotUploader.Dirty = false;
		CombatCompletionGate.ResetForTests();
		TestWriteRead.CardWinRateTracker.Me = null;

		DeckTester.deckAHPs = new List<int>();
		DeckTester.deckBHPs = new List<int>();

		hearts = CreateScriptableObject<IntSO>();
		hearts.value = 3;
		heartMax = CreateScriptableObject<IntSO>();
		heartMax.value = 3;
		wins = CreateScriptableObject<IntSO>();
		wins.value = 0;
		winCon = CreateScriptableObject<IntSO>();
		winCon.value = 5;
		sessionNum = CreateScriptableObject<IntSO>();
		sessionNum.value = 0;

		var phaseSo = CreateScriptableObject<GamePhaseSO>();
		phaseSo.currentGamePhase = EnumStorage.GamePhase.Combat;
		var finishedSo = CreateScriptableObject<BoolSO>();
		finishedSo.value = true;

		var pmObj = CreateGameObject("TestPhaseManager");
		phaseManager = pmObj.AddComponent<PhaseManager>();
		phaseManager.currentGamePhaseRef = phaseSo;
		phaseManager.combatFinished = finishedSo;
		phaseManager.hearts = hearts;
		phaseManager.heartMax = heartMax;
		phaseManager.wins = wins;
		phaseManager.winCon = winCon;
		phaseManager.sessionNum = sessionNum;
		phaseManager.playerStatusRef = OwnerStatus;
		phaseManager.enemyStatusRef = EnemyStatus;
	}

	[TearDown]
	public override void TearDown()
	{
		CombatCompletionGate.ResetForTests();
		base.TearDown();
	}

	private void InvokeUpdate()
	{
		typeof(PhaseManager)
			.GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance)
			.Invoke(phaseManager, null);
	}

	/// <summary>Latches a forced conclusion on a real guard (EditMode: NotifyReveal only latches).</summary>
	private CombatBudgetGuard LatchForcedConclusion()
	{
		var guard = CreateGameObject("TestBudgetGuard").AddComponent<CombatBudgetGuard>();
		guard.maxRevealsPerRound = 0;
		guard.maxTotalReveals = 0;
		guard.maxRounds = 60;
		typeof(CombatBudgetGuard)
			.GetProperty("Me", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
			.SetValue(null, guard);
		guard.NotifyReveal(1, 1, 10, 60);
		Assert.IsTrue(guard.ConclusionTripped, "setup: the guard must be latched");
		return guard;
	}

	[Test]
	public void Loss_KeepsTheLegacyBookkeeping()
	{
		OwnerStatus.hp = 0;
		EnemyStatus.hp = 5;

		InvokeUpdate();

		Assert.AreEqual(2, hearts.value, "a loss costs a heart");
		Assert.AreEqual(0, wins.value);
		Assert.AreEqual(1, DeckTester.currentSessionAmount);
		Assert.AreEqual(1, DeckTester.deckAHPs.Count);
		Assert.AreEqual(0, DeckTester.deckAHPs[0]);
		Assert.AreEqual(5, DeckTester.deckBHPs[0]);
	}

	[Test]
	public void Win_KeepsTheLegacyBookkeeping()
	{
		OwnerStatus.hp = 5;
		EnemyStatus.hp = 0;

		InvokeUpdate();

		Assert.AreEqual(3, hearts.value, "a win costs no heart");
		Assert.AreEqual(1, wins.value);
		Assert.AreEqual(1, DeckTester.currentSessionAmount);
		Assert.AreEqual(5, DeckTester.deckAHPs[0]);
		Assert.AreEqual(0, DeckTester.deckBHPs[0]);
	}

	[Test]
	public void NaturalDraw_NoHeartNoWin_ButAdvancesTheSessionCounter()
	{
		OwnerStatus.hp = 0;
		EnemyStatus.hp = 0;

		InvokeUpdate();

		Assert.AreEqual(3, hearts.value, "a draw costs no heart");
		Assert.AreEqual(0, wins.value, "a draw counts no win");
		Assert.AreEqual(1, DeckTester.currentSessionAmount,
			"draws must advance the automation session counter or all-draw autoSpace batches never terminate");
		Assert.AreEqual(new[] { 0 }, DeckTester.deckAHPs.ToArray());
		Assert.AreEqual(new[] { 0 }, DeckTester.deckBHPs.ToArray());
	}

	[Test]
	public void ForcedDraw_BothAliveAndLatched_NoHeartNoWin_SessionAdvances()
	{
		OwnerStatus.hp = 5;
		EnemyStatus.hp = 7;
		LatchForcedConclusion();

		InvokeUpdate();

		Assert.AreEqual(3, hearts.value, "a forced draw costs no heart (L0 = draw, draw plan D1)");
		Assert.AreEqual(0, wins.value);
		Assert.AreEqual(1, DeckTester.currentSessionAmount);
		Assert.AreEqual(new[] { 5 }, DeckTester.deckAHPs.ToArray());
		Assert.AreEqual(new[] { 7 }, DeckTester.deckBHPs.ToArray());
	}

	[Test]
	public void Abnormal_BothAliveNoLatch_ChangesNothing_AndLogsAnError()
	{
		OwnerStatus.hp = 5;
		EnemyStatus.hp = 5;

		// The engine-anomaly branch logs an error by design; expect it so the test does not fail
		// on the unexpected-error check.
		LogAssert.Expect(LogType.Error,
			new System.Text.RegularExpressions.Regex(".*no forced-conclusion latch.*"));

		InvokeUpdate();

		Assert.AreEqual(3, hearts.value, "Abnormal must not cost a heart");
		Assert.AreEqual(0, wins.value);
		Assert.AreEqual(0, DeckTester.currentSessionAmount, "Abnormal records nothing");
		Assert.AreEqual(0, DeckTester.deckAHPs.Count);
		Assert.AreEqual(0, DeckTester.deckBHPs.Count);
	}
}
