using DefaultNamespace;
using DefaultNamespace.Effects;
using NUnit.Framework;
using UnityEngine;

public class VisualsCallTests : HeadlessCombatTestFixture
{
	[Test]
	public void StageEffect_CapturesMoveToTopBatchRequest()
	{
		var card = CreateCard(true, "StageCard");
		var other = CreateCard(true, "OtherCard");
		var target = CreateCard(true, "TargetCard");
		// Target must NOT be at top (index != Count-1) to be eligible for staging
		CombatManager.combinedDeckZone.Add(other);
		CombatManager.combinedDeckZone.Add(target);

		var stageEffect = CreateEffect<StageEffect>(card);
		EffectChainManager.MakeANewEffectRecorder(card, stageEffect.gameObject);
		stageEffect.StageMyCards(1);

		var recorder = EffectChainManager.currentEffectRecorder.GetComponent<EffectRecorder>();
		Assert.AreEqual(1, recorder.animationRequests.Count, "StageEffect should capture 1 animation request");
		Assert.AreEqual(AnimationRequestType.MoveToTopPopUpBatch, recorder.animationRequests[0].type, "Should be MoveToTopPopUpBatch");
		Assert.AreEqual(1, recorder.animationRequests[0].targetCards.Count, "Should stage 1 card");

		EffectChainManager.Me.CloseOpenedChain();
	}

	[Test]
	public void BuryEffect_CapturesMoveToBottomBatchRequest()
	{
		var card = CreateCard(true, "BuryCard");
		var other = CreateCard(false, "OtherCard");
		var target = CreateCard(false, "TargetCard");
		// Target must NOT be at bottom (index != 0) to be eligible for burying
		CombatManager.combinedDeckZone.Add(target);
		CombatManager.combinedDeckZone.Add(other);

		var buryEffect = CreateEffect<BuryEffect>(card);
		EffectChainManager.MakeANewEffectRecorder(card, buryEffect.gameObject);
		buryEffect.BuryTheirCards(1);

		var recorder = EffectChainManager.currentEffectRecorder.GetComponent<EffectRecorder>();
		Assert.AreEqual(2, recorder.animationRequests.Count, "BuryEffect should capture 2 animation requests (PopUpBatch + MoveToBottomBatch)");
		Assert.AreEqual(AnimationRequestType.PopUpBatch, recorder.animationRequests[0].type, "First should be PopUpBatch");
		Assert.AreEqual(AnimationRequestType.MoveToBottomBatch, recorder.animationRequests[1].type, "Second should be MoveToBottomBatch");
		Assert.AreEqual(1, recorder.animationRequests[1].targetCards.Count, "Should bury 1 card");

		EffectChainManager.Me.CloseOpenedChain();
	}

	[Test]
	public void ExileEffect_CapturesDestroyRequest()
	{
		var card = CreateCard(true, "ExileCard");
		CombatManager.combinedDeckZone.Add(card);

		var exileEffect = CreateEffect<ExileEffect>(card);
		EffectChainManager.MakeANewEffectRecorder(card, exileEffect.gameObject);
		exileEffect.ExileSelf();

		var recorder = EffectChainManager.currentEffectRecorder.GetComponent<EffectRecorder>();
		Assert.AreEqual(2, recorder.animationRequests.Count, "ExileEffect should capture 2 requests (PopUp + Destroy)");
		Assert.AreEqual(AnimationRequestType.PopUp, recorder.animationRequests[0].type, "First should be PopUp");
		Assert.AreEqual(AnimationRequestType.Destroy, recorder.animationRequests[1].type, "Second should be Destroy type");

		EffectChainManager.Me.CloseOpenedChain();
	}

	[Test]
	public void BuryEffect_CallsSyncDeckOnce()
	{
		var card = CreateCard(true, "BuryCard");
		var other = CreateCard(false, "OtherCard");
		var target = CreateCard(false, "TargetCard");
		CombatManager.combinedDeckZone.Add(target);
		CombatManager.combinedDeckZone.Add(other);

		var buryEffect = CreateEffect<BuryEffect>(card);
		EffectChainManager.MakeANewEffectRecorder(card, buryEffect.gameObject);

		int syncCallsBefore = NullVisuals.syncDeckCalls;
		buryEffect.BuryTheirCards(1);
		int syncCallsAfter = NullVisuals.syncDeckCalls;

		Assert.AreEqual(1, syncCallsAfter - syncCallsBefore, "BuryEffect should call SyncPhysicalCardsWithCombinedDeck once");

		EffectChainManager.Me.CloseOpenedChain();
	}

	[Test]
	public void NullVisuals_GetPhysicalCard_ReturnsNull()
	{
		var card = CreateCard(true, "TestCard");
		var physical = NullVisuals.GetPhysicalCard(card);

		Assert.IsNull(physical, "GetPhysicalCard should return null in headless mode");
	}

	[Test]
	public void NullVisuals_IsPlayingAttackAnimation_ReturnsFalse()
	{
		Assert.IsFalse(NullVisuals.IsPlayingAttackAnimation(), "Should return false in headless mode");
	}

	[Test]
	public void NullVisuals_HasPendingAnimations_ReturnsFalse()
	{
		Assert.IsFalse(NullVisuals.HasPendingAnimations(), "Should return false in headless mode");
	}
}
