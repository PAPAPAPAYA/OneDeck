using System.Collections.Generic;
using DefaultNamespace;
using NUnit.Framework;
using UnityEngine;

public class CardManipulationTests : HeadlessCombatTestFixture
{
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

	[Test]
	public void DestroyTheirMinions_EnemyCard_RemovesFriendlyMinions()
	{
		var destroyer = CreateCard(false, "EnemyDestroyer");
		var friendlyMinion1 = CreateMinion(true, "FriendlyMinion1");
		var friendlyMinion2 = CreateMinion(true, "FriendlyMinion2");
		var enemyMinion = CreateMinion(false, "EnemyMinion");

		CombatManager.combinedDeckZone.Add(friendlyMinion1);
		CombatManager.combinedDeckZone.Add(friendlyMinion2);
		CombatManager.combinedDeckZone.Add(enemyMinion);

		var manip = CreateEffect<CardManipulationEffect>(destroyer);
		EffectChainManager.MakeANewEffectRecorder(destroyer, manip.gameObject);
		manip.DestroyTheirMinions(2);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(1, CombatManager.combinedDeckZone.Count, "Enemy should remove 2 friendly minions");
		Assert.IsTrue(CombatManager.combinedDeckZone.Contains(enemyMinion), "Enemy minion should remain");
	}

	[Test]
	public void DestroyMyMinions_EnemyCard_RemovesEnemyMinions()
	{
		var destroyer = CreateCard(false, "EnemyDestroyer");
		var enemyMinion1 = CreateMinion(false, "EnemyMinion1");
		var enemyMinion2 = CreateMinion(false, "EnemyMinion2");
		var friendlyMinion = CreateMinion(true, "FriendlyMinion");

		CombatManager.combinedDeckZone.Add(enemyMinion1);
		CombatManager.combinedDeckZone.Add(enemyMinion2);
		CombatManager.combinedDeckZone.Add(friendlyMinion);

		var manip = CreateEffect<CardManipulationEffect>(destroyer);
		EffectChainManager.MakeANewEffectRecorder(destroyer, manip.gameObject);
		manip.DestroyMyMinions(2);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(1, CombatManager.combinedDeckZone.Count, "Enemy should remove 2 enemy minions");
		Assert.IsTrue(CombatManager.combinedDeckZone.Contains(friendlyMinion), "Friendly minion should remain");
	}
}
