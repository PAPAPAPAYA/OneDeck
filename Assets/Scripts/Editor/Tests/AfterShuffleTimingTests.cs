using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DefaultNamespace;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Headless tests for afterShuffle event timing and isPlayingEffectAnimations guard behaviour.
/// Covers the fixes from 2026-06-08 (base timing) and 2026-06-09 (callback + auto-play).
/// </summary>
public class AfterShuffleTimingTests : HeadlessCombatTestFixture
{
	private bool _afterShuffleRaised;

	[SetUp]
	public override void SetUp()
	{
		base.SetUp();
		_afterShuffleRaised = false;
	}

	[TearDown]
	public override void TearDown()
	{
		// Ensure no stale singletons from created recorders
		if (RecorderAnimationPlayer.me != null)
		{
			var rap = RecorderAnimationPlayer.me;
			RecorderAnimationPlayer.me = null;
			if (rap != null && rap.gameObject != null) Object.DestroyImmediate(rap.gameObject);
		}
		base.TearDown();
	}

	// Helper: invoke private RevealCards via reflection
	private void InvokeRevealCards()
	{
		var method = typeof(CombatManager).GetMethod("RevealCards", BindingFlags.NonPublic | BindingFlags.Instance);
		Assert.IsNotNull(method, "RevealCards method should exist");
		method.Invoke(CombatManager, null);
	}

	// Helper: setup post-shuffle state so Round Start path executes
	private void SetupPostShuffleState()
	{
		CombatManager.revealZone = null;
		CombatManager.cardsRevealedThisRound = 0;
		CombatManager.awaitingRevealConfirm = false;
	}

	#region afterShuffle Event Timing

	[Test]
	public void AfterShuffle_RaisedAfterRevealZoneMovementCompletes()
	{
		var playerCard = CreateCard(true, "PlayerCard");
		var enemyCard = CreateCard(false, "EnemyCard");
		var startCard = CreateStartCard();

		CombatManager.playerDeck = CreateDeckSO(new List<GameObject> { playerCard });
		CombatManager.enemyDeck = CreateDeckSO(new List<GameObject> { enemyCard });
		CombatManager.startCardPrefab = startCard;
		CombatManager.GatherDecks();

		// Simulate Start Card shuffle completion
		SetupPostShuffleState();
		CombatManager.SetRaiseAfterShuffleOnNextReveal(true);

		// Register afterShuffle listener
		RegisterEventCallback(GameEventStorage.afterShuffle, () => _afterShuffleRaised = true);

		InvokeRevealCards();

		Assert.IsTrue(_afterShuffleRaised, "afterShuffle should be raised in Round Start path");
		Assert.IsNotNull(CombatManager.revealZone, "Next card should be revealed after Start Card");
	}

	[UnityTest]
	public IEnumerator AfterShuffle_AutoPlaysRecorderAnimations()
	{
		var playerCard = CreateCard(true, "PlayerCard");
		var enemyCard = CreateCard(false, "EnemyCard");
		var startCard = CreateStartCard();

		CombatManager.playerDeck = CreateDeckSO(new List<GameObject> { playerCard });
		CombatManager.enemyDeck = CreateDeckSO(new List<GameObject> { enemyCard });
		CombatManager.startCardPrefab = startCard;
		CombatManager.GatherDecks();

		SetupPostShuffleState();
		CombatManager.SetRaiseAfterShuffleOnNextReveal(true);

		// Bind afterShuffle to create a recorder (simulates BOOSTER's Stage effect)
		RegisterEventCallback(GameEventStorage.afterShuffle, () =>
		{
			_afterShuffleRaised = true;
			var recorderGo = CreateGameObject("AfterShuffleRecorder");
			var recorder = recorderGo.AddComponent<EffectRecorder>();
			recorder.animationRequests.Add(new AnimationRequest { type = AnimationRequestType.MoveToTopPopUpBatch });
			EffectChainManager.MakeANewEffectRecorder(recorderGo, recorderGo);
		});

		InvokeRevealCards();

		// Immediately after RevealCards returns, Round Start path has set the flag to true
		// and started PlayRecorderAnimationsAndWait.
		Assert.IsTrue(CombatManager.isPlayingEffectAnimations, "Flag should be true after Round Start auto-plays afterShuffle animations");
		Assert.IsTrue(_afterShuffleRaised, "afterShuffle should have been raised before coroutine completes");

		// Wait one frame for PlayRecorderAnimationsAndWait coroutine to complete
		yield return null;

		Assert.IsFalse(CombatManager.isPlayingEffectAnimations, "Flag should be false after auto-play completes");
	}

	#endregion

	#region isPlayingEffectAnimations Guards

	[Test]
	public void IsPlayingEffectAnimations_BlocksRoundStart()
	{
		var card = CreateCard(true, "Card");
		CombatManager.combinedDeckZone.Add(card);
		CombatManager.revealZone = null;
		CombatManager.cardsRevealedThisRound = 0;
		CombatManager.isPlayingEffectAnimations = true;

		InvokeRevealCards();

		Assert.IsNull(CombatManager.revealZone, "Round Start should not reveal card while animations are playing");
	}

	[Test]
	public void IsPlayingEffectAnimations_BlocksPhase1_AutoReveal()
	{
		var card = CreateCard(true, "Card");
		CombatManager.combinedDeckZone.Add(card);
		CombatManager.awaitingRevealConfirm = true;
		CombatManager.revealZone = null;
		CombatManager.isPlayingEffectAnimations = true;

		InvokeRevealCards();

		Assert.IsNull(CombatManager.revealZone, "Phase 1 auto-reveal should not advance while animations are playing");
	}

	[Test]
	public void IsPlayingEffectAnimations_BlocksPhase2()
	{
		var card = CreateCard(true, "Card");
		CombatManager.revealZone = card;
		CombatManager.awaitingRevealConfirm = false;
		CombatManager.isPlayingEffectAnimations = true;

		InvokeRevealCards();

		Assert.IsFalse(CombatManager.awaitingRevealConfirm, "Phase 2 should not transition to awaitingRevealConfirm=true while animations are playing");
		Assert.AreEqual(card, CombatManager.revealZone, "RevealZone card should remain unchanged");
	}

	#endregion

	#region PlayRecorderAnimationsAndWait Timing

	[UnityTest]
	public IEnumerator PlayRecorderAnimationsAndWait_ResetsFlagAfterUpdateAllPhysicalCardTargets()
	{
		var card = CreateCard(true, "Card");
		var effectObj = CreateGameObject("EffectObj");
		effectObj.transform.SetParent(card.transform);
		EffectChainManager.MakeANewEffectRecorder(card, effectObj);
		EffectChainManager.Me.CloseOpenedChain();

		int updateTargetCallsBefore = NullVisuals.updateTargetCalls;

		var method = typeof(CombatManager).GetMethod("PlayRecorderAnimationsAndWait", BindingFlags.NonPublic | BindingFlags.Instance);
		Assert.IsNotNull(method, "PlayRecorderAnimationsAndWait method should exist");
		var enumerator = (IEnumerator)method.Invoke(CombatManager, null);

		yield return CombatManager.StartCoroutine(enumerator);

		Assert.IsFalse(CombatManager.isPlayingEffectAnimations, "Flag should be false after coroutine completes");
		Assert.Greater(NullVisuals.updateTargetCalls, updateTargetCallsBefore, "UpdateAllPhysicalCardTargets should have been called");
	}

	#endregion
}
