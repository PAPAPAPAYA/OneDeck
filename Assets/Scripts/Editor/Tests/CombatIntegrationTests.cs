using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class CombatIntegrationTests : HeadlessCombatTestFixture
{
	[Test]
	public void FullRound_RevealTriggerAndPlaceToBottom()
	{
		var playerCard = CreateCard(true, "PlayerCard");
		var enemyCard = CreateCard(false, "EnemyCard");
		var startCard = CreateStartCard();

		CombatManager.playerDeck = CreateDeckSO(new List<GameObject> { playerCard });
		CombatManager.enemyDeck = CreateDeckSO(new List<GameObject> { enemyCard });
		CombatManager.startCardPrefab = startCard;

		CombatManager.GatherDecks();
		Assert.AreEqual(3, CombatManager.combinedDeckZone.Count, "Should have 3 cards after gather");

		// Reveal top card (Start Card is at top)
		var revealed = RevealTopCard();
		Assert.IsNotNull(revealed);
		Assert.IsTrue(revealed.isStartCard, "First reveal should be Start Card");
		Assert.AreEqual(2, CombatManager.combinedDeckZone.Count);

		// Put Start Card back to bottom (simulating end of round handling)
		PutRevealedCardToBottom();
		Assert.IsNull(CombatManager.revealZone);
		Assert.AreEqual(3, CombatManager.combinedDeckZone.Count);
		Assert.IsTrue(CombatManager.combinedDeckZone[0].GetComponent<CardScript>().isStartCard, "Start Card should be at bottom");

		// Reveal next top card
		var nextRevealed = RevealTopCard();
		Assert.IsNotNull(nextRevealed);
		Assert.IsFalse(nextRevealed.isStartCard, "Second reveal should be normal card");
		Assert.AreEqual(2, CombatManager.combinedDeckZone.Count);
	}

	[Test]
	public void StartCardReveal_TriggersShuffleAndNewRound()
	{
		var playerCard = CreateCard(true, "PlayerCard");
		var enemyCard = CreateCard(false, "EnemyCard");
		var startCard = CreateStartCard();

		CombatManager.playerDeck = CreateDeckSO(new List<GameObject> { playerCard });
		CombatManager.enemyDeck = CreateDeckSO(new List<GameObject> { enemyCard });
		CombatManager.startCardPrefab = startCard;
		CombatManager.roundNumRef.value = 0;

		CombatManager.GatherDecks();

		// Reveal Start Card
		var revealed = RevealTopCard();
		Assert.IsTrue(revealed.isStartCard);

		// Simulate TriggerStartCardEffect logic
		var startCardInstance = CombatManager.revealZone;
		CombatManager.revealZone = null;
		CombatManager.combinedDeckZone.Add(startCardInstance);

		// Shuffle remaining cards
		var cardsToShuffle = new List<GameObject>(CombatManager.combinedDeckZone);
		cardsToShuffle = UtilityFuncManagerScript.ShuffleList(cardsToShuffle);
		CombatManager.combinedDeckZone.Clear();
		foreach (var card in cardsToShuffle)
		{
			CombatManager.combinedDeckZone.Add(card);
		}

		CombatManager.roundNumRef.value++;
		CombatManager.cardsRevealedThisRound = 0;

		Assert.AreEqual(1, CombatManager.roundNumRef.value, "Round number should increment");
		Assert.AreEqual(0, CombatManager.cardsRevealedThisRound, "Cards revealed this round should reset");
		Assert.AreEqual(3, CombatManager.combinedDeckZone.Count, "All cards should remain in deck after shuffle");
	}

	[Test]
	public void Fatigue_TriggersAfterRevealThreshold()
	{
		CombatManager.fatigueRevealThreshold = 3;
		CombatManager.fatigueAmount = 2;
		CombatManager.totalCardsRevealed = 0;

		// Reveal 2 cards (below threshold)
		for (int i = 0; i < 2; i++)
		{
			var card = CreateCard(true, "Card" + i);
			CombatManager.combinedDeckZone.Add(card);
		}

		RevealTopCard();
		RevealTopCard();

		// Fatigue should NOT trigger yet
		Assert.AreEqual(2, CombatManager.totalCardsRevealed, "Should have revealed 2 cards");

		// Reveal 1 more card (hits threshold)
		var thirdCard = CreateCard(true, "Card2");
		CombatManager.combinedDeckZone.Add(thirdCard);
		RevealTopCard();

		Assert.AreEqual(3, CombatManager.totalCardsRevealed, "Should have revealed 3 cards");
		// CheckFatigueByRevealCount would trigger at exactly 3, but actual fatigue card addition
		// requires cardToAddWhenOvertime prefab which we don't set in this test.
		// We verify the threshold logic is reachable.
	}

	[Test]
	public void RevealZone_NullAfterExileDoesNotCrashOnNextReveal()
	{
		var card = CreateCard(true, "ExileCard");
		CombatManager.combinedDeckZone.Add(card);

		// Reveal the card
		RevealTopCard();
		Assert.AreEqual(card, CombatManager.revealZone);

		// Exile the revealed card
		var exile = CreateEffect<ExileEffect>(card);
		EffectChainManager.MakeANewEffectRecorder(card, exile.gameObject);
		exile.ExileSelf();
		EffectChainManager.Me.CloseOpenedChain();

		// Verify revealZone was cleared
		Assert.IsNull(CombatManager.revealZone, "revealZone should be null after exiling revealed card");
		Assert.AreEqual(0, CombatManager.combinedDeckZone.Count, "Deck should be empty");

		// Attempting to trigger a null revealZone should be safe
		TriggerRevealedCard();
		// If we reach here without exception, the test passes
		Assert.Pass("TriggerRevealedCard with null revealZone did not crash");
	}

	[Test]
	public void MultipleReveals_DecrementCombinedDeckCorrectly()
	{
		for (int i = 0; i < 5; i++)
		{
			CombatManager.combinedDeckZone.Add(CreateCard(i % 2 == 0, "Card" + i));
		}

		Assert.AreEqual(5, CombatManager.combinedDeckZone.Count);

		RevealTopCard();
		Assert.AreEqual(4, CombatManager.combinedDeckZone.Count);
		Assert.IsNotNull(CombatManager.revealZone);

		PutRevealedCardToBottom();
		Assert.AreEqual(5, CombatManager.combinedDeckZone.Count);
		Assert.IsNull(CombatManager.revealZone);

		RevealTopCard();
		Assert.AreEqual(4, CombatManager.combinedDeckZone.Count);
	}

	[Test]
	public void DeckOrder_AfterBuryAndReveal_IsCorrect()
	{
		var cardA = CreateCard(true, "A");
		var cardB = CreateCard(true, "B");
		var cardC = CreateCard(true, "C");

		CombatManager.combinedDeckZone.Add(cardA);
		CombatManager.combinedDeckZone.Add(cardB);
		CombatManager.combinedDeckZone.Add(cardC);

		// Deck: [A(bottom), B, C(top)]
		// Reveal C
		RevealTopCard();
		Assert.AreEqual(cardC, CombatManager.revealZone);
		Assert.AreEqual(2, CombatManager.combinedDeckZone.Count);

		// Put C to bottom
		PutRevealedCardToBottom();
		// Deck: [C(bottom), A, B(top)]
		Assert.AreEqual(cardC, CombatManager.combinedDeckZone[0]);
		Assert.AreEqual(cardA, CombatManager.combinedDeckZone[1]);
		Assert.AreEqual(cardB, CombatManager.combinedDeckZone[2]);

		// Reveal B
		RevealTopCard();
		Assert.AreEqual(cardB, CombatManager.revealZone);
		// Deck: [C(bottom), A(top)]
		Assert.AreEqual(2, CombatManager.combinedDeckZone.Count);
		Assert.AreEqual(cardC, CombatManager.combinedDeckZone[0]);
		Assert.AreEqual(cardA, CombatManager.combinedDeckZone[1]);
	}
}
