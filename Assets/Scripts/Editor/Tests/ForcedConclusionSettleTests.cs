using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// The D1 fix end-to-end (plans/plan-forced-draw-reporting-2026-11-26.md §5.2/§5.3): a real
/// budget-cap trip must SETTLE (combatFinished flips) with no player click, driven by the
/// guard latch retry in CombatManager.RevealCards — not by the play-mode-only call in
/// NotifyReveal that used to be swallowed by the effect-animation / confirmation gates
/// (draw plan §2.2). Uses the production HeadlessCombatRig: real RevealCards pumping with
/// the guard wired, exactly like the plan's EditMode reachability requirement.
/// </summary>
public class ForcedConclusionSettleTests
{
	private HeadlessCombatRig rig;

	[SetUp]
	public void SetUp()
	{
		RunRecorder.ResetForTests();
		ServerConfig.Active = null;
		// Per-round and total caps disabled; the round cap at 1 is the trip under test.
		rig = HeadlessCombatRig.Create(0, 0, 1);
		rig.SetCombatSeed(42);
	}

	[TearDown]
	public void TearDown()
	{
		// rig.Dispose destroys the rig's TestManager; GatherDecks reads the override seed via
		// TestManager.Me, which cleanup nulls — nothing leaks into later tests.
		if (rig != null) rig.Dispose();
	}

	[OneTimeTearDown]
	public void OneTimeTearDown()
	{
		HeadlessCombatRig.DestroySharedResources();
	}

	/// <summary>Plain effect-less decks + production GatherDecks (no Start Card prefab: the
	/// shuffle's round increment is mirrored directly on the production fields below).</summary>
	private void BuildCombat()
	{
		var playerCards = new List<GameObject>();
		var enemyCards = new List<GameObject>();
		for (int i = 0; i < 3; i++)
		{
			playerCards.Add(rig.CreateCard(true, "PlayerCard" + i, "P_CARD_" + i));
			enemyCards.Add(rig.CreateCard(false, "EnemyCard" + i, "E_CARD_" + i));
		}
		rig.CombatManager.playerDeck = rig.CreateDeckSO(playerCards);
		rig.CombatManager.enemyDeck = rig.CreateDeckSO(enemyCards);
		rig.CombatManager.startCardPrefab = null;
		rig.CombatManager.GatherDecks();
		rig.BridgeAllCards();

		// Mirror the facts StartCardShuffleEffect.ExecuteShuffleEffect leaves behind at the
		// round boundary (round++, per-round counter zeroed, reveal zone emptied, afterShuffle
		// armed): the next reveal IS the automatic round-start reveal of round 1.
		rig.CombatManager.roundNumRef.value = 1;
		rig.CombatManager.cardsRevealedThisRound = 0;
		rig.CombatManager.SetRaiseAfterShuffleOnNextReveal(true);
	}

	private static void PumpRevealCards(CombatManager cm)
	{
		typeof(CombatManager)
			.GetMethod("RevealCards", BindingFlags.NonPublic | BindingFlags.Instance)
			.Invoke(cm, null);
	}

	/// <summary>Trip + settle, the exact production sequence: reveal (trips, animation flag
	/// set) -> animations clear -> next RevealCards tick settles off the latch.</summary>
	private void TripAndAssertSwallowedState()
	{
		BuildCombat();

		// Round-start reveal: trips the round cap (1/1). Production's afterShuffle branch sets
		// isPlayingEffectAnimations right at the trip — the exact swallowed state of §2.2.
		PumpRevealCards(rig.CombatManager);

		Assert.IsTrue(rig.Guard.ConclusionTripped, "the round cap trip must latch ConclusionTripped");
		Assert.IsNotNull(rig.Guard.Conclusion, "the trip must latch the conclusion record");
		Assert.AreEqual(BudgetCapKind.Rounds, rig.Guard.Conclusion.kind);
		Assert.AreEqual(1, rig.Guard.Conclusion.round);
		Assert.AreEqual(1, rig.Guard.Conclusion.totalReveals);
		Assert.IsFalse(rig.CombatManager.combatFinished.value,
			"pre-fix behavior: the trip landed while effect animations played and did not settle");
		Assert.IsTrue(rig.CombatManager.isPlayingEffectAnimations,
			"the round-start afterShuffle branch must be the animation gate that swallowed the trip");
	}

	[Test]
	public void RoundCapTrip_OnRoundStartReveal_SettlesAsForcedWithoutClick()
	{
		TripAndAssertSwallowedState();

		// The animations finish; the very next RevealCards tick must settle with NO click
		// (ShouldAutoConfirm skipped on the forced path).
		rig.CombatManager.isPlayingEffectAnimations = false;
		PumpRevealCards(rig.CombatManager);

		Assert.IsTrue(rig.CombatManager.combatFinished.value,
			"D1: the forced conclusion must settle on the latch retry, with no player input");
		// The latch itself stays live until ExitCombat (PhaseManager resolves the outcome from it).
		BudgetCapConclusion conclusion;
		Assert.IsTrue(rig.CombatManager.TryGetForcedConclusion(out conclusion),
			"the latch must stay readable after the settle");
		Assert.AreEqual(BudgetCapKind.Rounds, conclusion.kind);
	}

	[Test]
	public void AfterForcedSettle_ExitEnterCombat_LatchCleared_NoCrossCombatLeak()
	{
		TripAndAssertSwallowedState();
		rig.CombatManager.isPlayingEffectAnimations = false;
		PumpRevealCards(rig.CombatManager);
		Assert.IsTrue(rig.CombatManager.combatFinished.value);

		// ExitCombat (bound to onExitCombatPhase in the scene) must copy the facts out BEFORE
		// ResetState clears the latch. It cleans up with Object.Destroy, which logs an error in
		// Edit Mode (production runs in play mode) — ignore just those messages for the call.
		LogAssert.ignoreFailingMessages = true;
		rig.CombatManager.ExitCombat();
		LogAssert.ignoreFailingMessages = false;

		Assert.IsFalse(rig.Guard.ConclusionTripped, "ResetState must clear the conclusion latch");
		Assert.IsNull(rig.Guard.Conclusion);
		Assert.IsNotNull(rig.CombatManager.lastCombatConclusion, "the conclusion holder must survive ExitCombat");
		Assert.AreEqual(BudgetCapKind.Rounds, rig.CombatManager.lastCombatConclusion.kind);
		Assert.AreEqual(1, rig.CombatManager.roundsLastCombat);
		Assert.AreEqual(1, rig.CombatManager.revealsLastCombat);
		Assert.AreEqual(42, rig.CombatManager.lastCombatSeed);
		BudgetCapConclusion cleared;
		Assert.IsFalse(rig.CombatManager.TryGetForcedConclusion(out cleared), "no latch after ExitCombat");

		// Next combat: EnterCombat only clears combatFinished; one RevealCards tick must NOT
		// force-conclude again (no cross-combat leak).
		rig.CombatManager.EnterCombat();
		Assert.IsFalse(rig.CombatManager.combatFinished.value);
		PumpRevealCards(rig.CombatManager);
		Assert.IsFalse(rig.CombatManager.combatFinished.value,
			"the cleared latch must not force-conclude the next combat");
		Assert.IsFalse(rig.Guard.ConclusionTripped);
	}
}
