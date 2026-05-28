using DefaultNamespace;
using DefaultNamespace.Effects;
using NUnit.Framework;
using UnityEngine;

public class ExileEffectTests : HeadlessCombatTestFixture
{
	[Test]
	public void ExileSelf_RemovesCardFromDeck()
	{
		var card = CreateCard(true, "ExileCard");
		CombatManager.combinedDeckZone.Add(card);
		Assert.AreEqual(1, CombatManager.combinedDeckZone.Count);

		var exile = CreateEffect<ExileEffect>(card);
		EffectChainManager.MakeANewEffectRecorder(card, exile.gameObject);
		exile.ExileSelf();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(0, CombatManager.combinedDeckZone.Count, "Card should be removed from deck");
	}

	[Test]
	public void ExileSelf_CapturesDestroyAnimationRequest()
	{
		var card = CreateCard(true, "ExileCard");
		CombatManager.combinedDeckZone.Add(card);

		var exile = CreateEffect<ExileEffect>(card);
		EffectChainManager.MakeANewEffectRecorder(card, exile.gameObject);
		exile.ExileSelf();

		var recorder = EffectChainManager.currentEffectRecorder.GetComponent<EffectRecorder>();
		Assert.AreEqual(2, recorder.animationRequests.Count, "Should capture 2 requests (PopUp + Destroy)");
		Assert.AreEqual(AnimationRequestType.PopUp, recorder.animationRequests[0].type, "First should be PopUp");
		Assert.AreEqual(card, recorder.animationRequests[0].targetCard, "PopUp target should be the exiled card");
		Assert.AreEqual(AnimationRequestType.Destroy, recorder.animationRequests[1].type, "Second should be Destroy type");
		Assert.AreEqual(card, recorder.animationRequests[1].targetCard, "Destroy target should be the exiled card");

		EffectChainManager.Me.CloseOpenedChain();
	}

	[Test]
	public void ExileSelf_ClearsRevealZoneWhenRevealed()
	{
		var card = CreateCard(true, "ExileCard");
		CombatManager.combinedDeckZone.Add(card);
		CombatManager.revealZone = card;

		var exile = CreateEffect<ExileEffect>(card);
		EffectChainManager.MakeANewEffectRecorder(card, exile.gameObject);
		exile.ExileSelf();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.IsNull(CombatManager.revealZone, "revealZone should be cleared when exiling revealed card");
	}

	[Test]
	public void ExileTheirCards_RemovesEnemyCards()
	{
		var exileCard = CreateCard(true, "ExileCard");
		var enemy1 = CreateCard(false, "Enemy1");
		var enemy2 = CreateCard(false, "Enemy2");
		var friendly = CreateCard(true, "Friendly");

		CombatManager.combinedDeckZone.Add(enemy1);
		CombatManager.combinedDeckZone.Add(enemy2);
		CombatManager.combinedDeckZone.Add(friendly);

		var exile = CreateEffect<ExileEffect>(exileCard);
		EffectChainManager.MakeANewEffectRecorder(exileCard, exile.gameObject);
		exile.ExileTheirCards(2);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(1, CombatManager.combinedDeckZone.Count, "Should remove 2 enemy cards");
		Assert.IsTrue(CombatManager.combinedDeckZone.Contains(friendly), "Friendly card should remain");
	}

	[Test]
	public void ExileTheirCards_CapturesDestroyAnimationRequests()
	{
		var exileCard = CreateCard(true, "ExileCard");
		var enemy1 = CreateCard(false, "Enemy1");
		var enemy2 = CreateCard(false, "Enemy2");

		CombatManager.combinedDeckZone.Add(enemy1);
		CombatManager.combinedDeckZone.Add(enemy2);

		var exile = CreateEffect<ExileEffect>(exileCard);
		EffectChainManager.MakeANewEffectRecorder(exileCard, exile.gameObject);
		exile.ExileTheirCards(2);

		var recorder = EffectChainManager.currentEffectRecorder.GetComponent<EffectRecorder>();
		Assert.AreEqual(4, recorder.animationRequests.Count, "Should capture 4 requests (PopUp+Destroy for each of 2 cards)");
		Assert.AreEqual(AnimationRequestType.PopUp, recorder.animationRequests[0].type);
		Assert.AreEqual(AnimationRequestType.Destroy, recorder.animationRequests[1].type);
		Assert.AreEqual(AnimationRequestType.PopUp, recorder.animationRequests[2].type);
		Assert.AreEqual(AnimationRequestType.Destroy, recorder.animationRequests[3].type);

		EffectChainManager.Me.CloseOpenedChain();
	}

	[Test]
	public void ExileMyCards_RemovesOwnCards()
	{
		var exileCard = CreateCard(true, "ExileCard");
		var friendly1 = CreateCard(true, "Friendly1");
		var friendly2 = CreateCard(true, "Friendly2");
		var enemy = CreateCard(false, "Enemy");

		CombatManager.combinedDeckZone.Add(friendly1);
		CombatManager.combinedDeckZone.Add(friendly2);
		CombatManager.combinedDeckZone.Add(enemy);

		var exile = CreateEffect<ExileEffect>(exileCard);
		EffectChainManager.MakeANewEffectRecorder(exileCard, exile.gameObject);
		exile.ExileMyCards(2);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(1, CombatManager.combinedDeckZone.Count, "Should remove 2 friendly cards");
		Assert.IsTrue(CombatManager.combinedDeckZone.Contains(enemy), "Enemy card should remain");
	}

	[Test]
	public void ExileRandomCards_RemovesAnyCards()
	{
		var exileCard = CreateCard(true, "ExileCard");
		var card1 = CreateCard(true, "Card1");
		var card2 = CreateCard(false, "Card2");
		var card3 = CreateCard(true, "Card3");

		CombatManager.combinedDeckZone.Add(card1);
		CombatManager.combinedDeckZone.Add(card2);
		CombatManager.combinedDeckZone.Add(card3);

		var exile = CreateEffect<ExileEffect>(exileCard);
		EffectChainManager.MakeANewEffectRecorder(exileCard, exile.gameObject);
		exile.ExileRandomCards(2);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(1, CombatManager.combinedDeckZone.Count, "Should remove 2 random cards");
	}

	[Test]
	public void ExileTheirCards_EnemyCard_RemovesFriendlyCards()
	{
		var exileCard = CreateCard(false, "EnemyExileCard");
		var friendly1 = CreateCard(true, "Friendly1");
		var friendly2 = CreateCard(true, "Friendly2");
		var enemy = CreateCard(false, "Enemy");

		CombatManager.combinedDeckZone.Add(friendly1);
		CombatManager.combinedDeckZone.Add(friendly2);
		CombatManager.combinedDeckZone.Add(enemy);

		var exile = CreateEffect<ExileEffect>(exileCard);
		EffectChainManager.MakeANewEffectRecorder(exileCard, exile.gameObject);
		exile.ExileTheirCards(2);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(1, CombatManager.combinedDeckZone.Count, "Enemy should remove 2 friendly cards");
		Assert.IsTrue(CombatManager.combinedDeckZone.Contains(enemy), "Enemy card should remain");
	}

	[Test]
	public void ExileMyCards_EnemyCard_RemovesEnemyCards()
	{
		var exileCard = CreateCard(false, "EnemyExileCard");
		var enemy1 = CreateCard(false, "Enemy1");
		var enemy2 = CreateCard(false, "Enemy2");
		var friendly = CreateCard(true, "Friendly");

		CombatManager.combinedDeckZone.Add(enemy1);
		CombatManager.combinedDeckZone.Add(enemy2);
		CombatManager.combinedDeckZone.Add(friendly);

		var exile = CreateEffect<ExileEffect>(exileCard);
		EffectChainManager.MakeANewEffectRecorder(exileCard, exile.gameObject);
		exile.ExileMyCards(2);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(1, CombatManager.combinedDeckZone.Count, "Enemy should remove 2 enemy cards");
		Assert.IsTrue(CombatManager.combinedDeckZone.Contains(friendly), "Friendly card should remain");
	}

	[Test]
	public void ExileTheirMinions_EnemyCard_RemovesFriendlyMinions()
	{
		var exileCard = CreateCard(false, "EnemyExileCard");
		var friendlyMinion1 = CreateMinion(true, "FriendlyMinion1");
		var friendlyMinion2 = CreateMinion(true, "FriendlyMinion2");
		var enemyMinion = CreateMinion(false, "EnemyMinion");

		CombatManager.combinedDeckZone.Add(friendlyMinion1);
		CombatManager.combinedDeckZone.Add(friendlyMinion2);
		CombatManager.combinedDeckZone.Add(enemyMinion);

		var exile = CreateEffect<ExileEffect>(exileCard);
		EffectChainManager.MakeANewEffectRecorder(exileCard, exile.gameObject);
		exile.ExileTheirMinions(2);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(1, CombatManager.combinedDeckZone.Count, "Should remove 2 friendly minions");
		Assert.IsTrue(CombatManager.combinedDeckZone.Contains(enemyMinion), "Enemy minion should remain");
	}
}
