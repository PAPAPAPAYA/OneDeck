using System.Collections.Generic;
using DefaultNamespace;
using NUnit.Framework;
using UnityEngine;

public class CardManipulationTests : HeadlessCombatTestFixture
{
	[Test]
	public void DelayMyCards_MovesCardsBackwardByOne()
	{
		var delayCard = CreateCard(true, "Delayer");
		var target = CreateCard(true, "Delayed");
		var bottom = CreateCard(true, "Bottom");

		// Deck order: bottom(index 0), target(index 1), top(index 2)
		CombatManager.combinedDeckZone.Add(bottom);
		CombatManager.combinedDeckZone.Add(target);
		var top = CreateCard(true, "Top");
		CombatManager.combinedDeckZone.Add(top);

		var manip = CreateEffect<CardManipulationEffect>(delayCard);
		EffectChainManager.MakeANewEffectRecorder(delayCard, manip.gameObject);
		manip.DelayMyCards(1);
		EffectChainManager.Me.CloseOpenedChain();

		// target should move from index 1 to index 0
		Assert.AreEqual(target, CombatManager.combinedDeckZone[0], "Delayed card should move to bottom");
		Assert.AreEqual(bottom, CombatManager.combinedDeckZone[1], "Bottom card should shift up");
	}

	[Test]
	public void DelayMyCards_DoesNotAffectBottomCard()
	{
		var delayCard = CreateCard(true, "Delayer");
		var bottom = CreateCard(true, "Bottom");
		var above = CreateCard(true, "Above");

		CombatManager.combinedDeckZone.Add(bottom);
		CombatManager.combinedDeckZone.Add(above);

		var manip = CreateEffect<CardManipulationEffect>(delayCard);
		EffectChainManager.MakeANewEffectRecorder(delayCard, manip.gameObject);
		manip.DelayMyCards(2);
		EffectChainManager.Me.CloseOpenedChain();

		// bottom card (index 0) cannot be delayed, only above moves down by 1
		Assert.AreEqual(2, CombatManager.combinedDeckZone.Count);
		Assert.AreEqual(above, CombatManager.combinedDeckZone[0], "Above card should move to index 0");
		Assert.AreEqual(bottom, CombatManager.combinedDeckZone[1], "Bottom card should shift to index 1");
	}

	[Test]
	public void DelayMyCards_CapturesMoveToIndexRequests()
	{
		var delayCard = CreateCard(true, "Delayer");
		var target1 = CreateCard(true, "Target1");
		var target2 = CreateCard(true, "Target2");
		var bottom = CreateCard(true, "Bottom");

		CombatManager.combinedDeckZone.Add(bottom);
		CombatManager.combinedDeckZone.Add(target1);
		CombatManager.combinedDeckZone.Add(target2);

		var manip = CreateEffect<CardManipulationEffect>(delayCard);
		EffectChainManager.MakeANewEffectRecorder(delayCard, manip.gameObject);
		manip.DelayMyCards(2);

		var recorder = EffectChainManager.currentEffectRecorder.GetComponent<EffectRecorder>();
		Assert.AreEqual(2, recorder.animationRequests.Count, "Should capture 2 MoveToIndex requests");
		Assert.AreEqual(AnimationRequestType.MoveToIndex, recorder.animationRequests[0].type);
		Assert.AreEqual(AnimationRequestType.MoveToIndex, recorder.animationRequests[1].type);

		EffectChainManager.Me.CloseOpenedChain();
	}

	[Test]
	public void DelayTheirCards_MovesEnemyCardsBackward()
	{
		var delayCard = CreateCard(true, "Delayer");
		var enemy = CreateCard(false, "Enemy");
		var bottom = CreateCard(false, "EnemyBottom");

		CombatManager.combinedDeckZone.Add(bottom);
		CombatManager.combinedDeckZone.Add(enemy);

		var manip = CreateEffect<CardManipulationEffect>(delayCard);
		EffectChainManager.MakeANewEffectRecorder(delayCard, manip.gameObject);
		manip.DelayTheirCards(1);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(enemy, CombatManager.combinedDeckZone[0], "Enemy card should move to bottom");
	}

	[Test]
	public void DestroyMyMinions_RemovesMinionsFromDeck()
	{
		var destroyer = CreateCard(true, "Destroyer");
		var minion1 = CreateMinion(true, "Minion1");
		var minion2 = CreateMinion(true, "Minion2");
		var normal = CreateCard(true, "Normal");

		CombatManager.combinedDeckZone.Add(minion1);
		CombatManager.combinedDeckZone.Add(minion2);
		CombatManager.combinedDeckZone.Add(normal);

		var manip = CreateEffect<CardManipulationEffect>(destroyer);
		EffectChainManager.MakeANewEffectRecorder(destroyer, manip.gameObject);
		manip.DestroyMyMinions(2);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(1, CombatManager.combinedDeckZone.Count, "Should remove 2 minions");
		Assert.IsTrue(CombatManager.combinedDeckZone.Contains(normal), "Normal card should remain");
	}

	[Test]
	public void DestroyMyMinions_CapturesDestroyAnimationRequests()
	{
		var destroyer = CreateCard(true, "Destroyer");
		var minion1 = CreateMinion(true, "Minion1");
		var minion2 = CreateMinion(true, "Minion2");

		CombatManager.combinedDeckZone.Add(minion1);
		CombatManager.combinedDeckZone.Add(minion2);

		var manip = CreateEffect<CardManipulationEffect>(destroyer);
		EffectChainManager.MakeANewEffectRecorder(destroyer, manip.gameObject);
		manip.DestroyMyMinions(2);

		var recorder = EffectChainManager.currentEffectRecorder.GetComponent<EffectRecorder>();
		Assert.AreEqual(2, recorder.animationRequests.Count, "Should capture 2 Destroy requests");
		Assert.AreEqual(AnimationRequestType.Destroy, recorder.animationRequests[0].type);
		Assert.AreEqual(AnimationRequestType.Destroy, recorder.animationRequests[1].type);
		var destroyedTargets = new List<GameObject> { recorder.animationRequests[0].targetCard, recorder.animationRequests[1].targetCard };
		Assert.IsTrue(destroyedTargets.Contains(minion1), "First destroyed target should be one of the minions");
		Assert.IsTrue(destroyedTargets.Contains(minion2), "Second destroyed target should be one of the minions");

		EffectChainManager.Me.CloseOpenedChain();
	}

	[Test]
	public void DestroyTheirMinions_RemovesEnemyMinions()
	{
		var destroyer = CreateCard(true, "Destroyer");
		var enemyMinion = CreateMinion(false, "EnemyMinion");
		var friendly = CreateCard(true, "Friendly");

		CombatManager.combinedDeckZone.Add(enemyMinion);
		CombatManager.combinedDeckZone.Add(friendly);

		var manip = CreateEffect<CardManipulationEffect>(destroyer);
		EffectChainManager.MakeANewEffectRecorder(destroyer, manip.gameObject);
		manip.DestroyTheirMinions(1);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(1, CombatManager.combinedDeckZone.Count, "Should remove 1 enemy minion");
		Assert.IsTrue(CombatManager.combinedDeckZone.Contains(friendly), "Friendly card should remain");
	}
}
