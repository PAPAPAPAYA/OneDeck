using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class CombatFlowTests : HeadlessCombatTestFixture
{
	[Test]
	public void GatherDecks_CombinesPlayerAndEnemyDecks()
	{
		var playerCardPrefab = CreateCard(true, "PlayerCardPrefab");
		var enemyCardPrefab = CreateCard(false, "EnemyCardPrefab");

		var playerDeck = CreateDeckSO(new List<GameObject> { playerCardPrefab, playerCardPrefab });
		var enemyDeck = CreateDeckSO(new List<GameObject> { enemyCardPrefab });

		CombatManager.playerDeck = playerDeck;
		CombatManager.enemyDeck = enemyDeck;
		CombatManager.startCardPrefab = CreateStartCard();

		CombatManager.GatherDecks();

		Assert.AreEqual(4, CombatManager.combinedDeckZone.Count, "Should have 2 player + 1 enemy + 1 start cards");
	}

	[Test]
	public void GatherDecks_StartCardIsAtTop()
	{
		var playerCardPrefab = CreateCard(true, "PlayerCardPrefab");
		var enemyCardPrefab = CreateCard(false, "EnemyCardPrefab");

		var playerDeck = CreateDeckSO(new List<GameObject> { playerCardPrefab });
		var enemyDeck = CreateDeckSO(new List<GameObject> { enemyCardPrefab });

		CombatManager.playerDeck = playerDeck;
		CombatManager.enemyDeck = enemyDeck;
		CombatManager.startCardPrefab = CreateStartCard();

		CombatManager.GatherDecks();

		var topCard = CombatManager.combinedDeckZone[CombatManager.combinedDeckZone.Count - 1].GetComponent<CardScript>();
		Assert.IsTrue(topCard.isStartCard, "Start Card should be at top (last index)");
	}

	[Test]
	public void RevealTopCard_MovesTopCardToRevealZone()
	{
		var card = CreateCard(true, "TestCard");
		CombatManager.combinedDeckZone.Add(card);

		var revealed = RevealTopCard();

		Assert.IsNotNull(revealed);
		Assert.AreEqual(card, revealed.gameObject);
		Assert.AreEqual(0, CombatManager.combinedDeckZone.Count);
		Assert.IsNotNull(CombatManager.revealZone);
	}

	[Test]
	public void PutRevealedCardToBottom_MovesCardToIndexZero()
	{
		var card = CreateCard(true, "TestCard");
		CombatManager.combinedDeckZone.Add(card);
		RevealTopCard();

		PutRevealedCardToBottom();

		Assert.IsNull(CombatManager.revealZone);
		Assert.AreEqual(1, CombatManager.combinedDeckZone.Count);
		Assert.AreEqual(card, CombatManager.combinedDeckZone[0]);
	}

	[Test]
	public void TriggerRevealedCard_RaisesOnAnyCardRevealed()
	{
		var card = CreateCard(true, "TestCard");
		CombatManager.combinedDeckZone.Add(card);
		RevealTopCard();

		bool raised = false;
		RegisterEventCallback(GameEventStorage.onAnyCardRevealed, () => raised = true);

		TriggerRevealedCard();

		Assert.IsTrue(raised, "onAnyCardRevealed should be raised");
	}

	[Test]
	public void TriggerRevealedCard_RaisesOnHostileCardRevealedForEnemyCard()
	{
		var card = CreateCard(false, "EnemyCard");
		CombatManager.combinedDeckZone.Add(card);
		RevealTopCard();

		bool raised = false;
		RegisterEventCallback(GameEventStorage.onHostileCardRevealed, () => raised = true);

		TriggerRevealedCard();

		Assert.IsTrue(raised, "onHostileCardRevealed should be raised for enemy card");
	}

}
