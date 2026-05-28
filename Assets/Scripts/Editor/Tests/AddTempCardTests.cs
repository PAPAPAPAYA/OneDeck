using DefaultNamespace;
using DefaultNamespace.Effects;
using DefaultNamespace.SOScripts;
using NUnit.Framework;
using UnityEngine;

public class AddTempCardTests : HeadlessCombatTestFixture
{
	[Test]
	public void AddCardToMe_AddsCardToOwnerDeck()
	{
		var sourceCard = CreateCard(true, "SourceCard");
		var cardToAdd = CreateCard(true, "CardToAdd");
		int deckCountBefore = CombatManager.combinedDeckZone.Count;

		var addTemp = CreateEffect<AddTempCard>(sourceCard);
		addTemp.cardCount = 1;

		EffectChainManager.MakeANewEffectRecorder(sourceCard, addTemp.gameObject);
		addTemp.AddCardToMe(cardToAdd);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(deckCountBefore + 1, CombatManager.combinedDeckZone.Count, "Should add 1 card to deck");
		var addedCard = CombatManager.combinedDeckZone[0];
		Assert.AreEqual(OwnerStatus, addedCard.GetComponent<CardScript>().myStatusRef, "Added card should belong to owner");
	}

	[Test]
	public void AddCardToMe_AddsMultipleCards()
	{
		var sourceCard = CreateCard(true, "SourceCard");
		var cardToAdd = CreateCard(true, "CardToAdd");
		int deckCountBefore = CombatManager.combinedDeckZone.Count;

		var addTemp = CreateEffect<AddTempCard>(sourceCard);
		addTemp.cardCount = 3;

		EffectChainManager.MakeANewEffectRecorder(sourceCard, addTemp.gameObject);
		addTemp.AddCardToMe(cardToAdd);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(deckCountBefore + 3, CombatManager.combinedDeckZone.Count, "Should add 3 cards to deck");
	}

	[Test]
	public void AddCardToThem_AddsCardToEnemyDeck()
	{
		var sourceCard = CreateCard(true, "SourceCard");
		var cardToAdd = CreateCard(false, "CardToAdd");
		int deckCountBefore = CombatManager.combinedDeckZone.Count;

		var addTemp = CreateEffect<AddTempCard>(sourceCard);
		addTemp.cardCount = 1;

		EffectChainManager.MakeANewEffectRecorder(sourceCard, addTemp.gameObject);
		addTemp.AddCardToThem(cardToAdd);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(deckCountBefore + 1, CombatManager.combinedDeckZone.Count, "Should add 1 card to deck");
		var addedCard = CombatManager.combinedDeckZone[0];
		Assert.AreEqual(EnemyStatus, addedCard.GetComponent<CardScript>().myStatusRef, "Added card should belong to enemy");
	}

	[Test]
	public void AddSelfToMe_AddsCopiesOfSelf()
	{
		var sourceCard = CreateCard(true, "SourceCard");
		int deckCountBefore = CombatManager.combinedDeckZone.Count;

		var addTemp = CreateEffect<AddTempCard>(sourceCard);
		addTemp.cardCount = 2;

		EffectChainManager.MakeANewEffectRecorder(sourceCard, addTemp.gameObject);
		addTemp.AddSelfToMe();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(deckCountBefore + 2, CombatManager.combinedDeckZone.Count, "Should add 2 copies to deck");
		foreach (var card in CombatManager.combinedDeckZone)
		{
			if (card == sourceCard) continue;
			Assert.AreEqual(OwnerStatus, card.GetComponent<CardScript>().myStatusRef, "Copies should belong to owner");
		}
	}

	[Test]
	public void AddSelfToThem_AddsCopiesOfSelfToEnemy()
	{
		var sourceCard = CreateCard(true, "SourceCard");
		int deckCountBefore = CombatManager.combinedDeckZone.Count;

		var addTemp = CreateEffect<AddTempCard>(sourceCard);
		addTemp.cardCount = 1;

		EffectChainManager.MakeANewEffectRecorder(sourceCard, addTemp.gameObject);
		addTemp.AddSelfToThem();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(deckCountBefore + 1, CombatManager.combinedDeckZone.Count, "Should add 1 copy to deck");
		var addedCard = CombatManager.combinedDeckZone[0];
		Assert.AreEqual(EnemyStatus, addedCard.GetComponent<CardScript>().myStatusRef, "Copy should belong to enemy");
	}

	[Test]
	public void CopyEnemyCurseCardToThem_CopiesMatchingCurseCard()
	{
		var sourceCard = CreateCard(true, "SourceCard");

		// Use CardFactory to create a proper enemy curse card parented to enemyDeckParent
		var cursePrefab = CreateCard(false, "CursePrefab");
		cursePrefab.GetComponent<CardScript>().cardTypeID = "curse_type";
		cursePrefab.GetComponent<CardScript>().myStatusEffects.Add(EnumStorage.StatusEffect.Power);
		var enemyCurseCard = CardFactory.SpawnCardForPlayer(cursePrefab, EnemyStatus, deckIndex: 0, triggerMinionEvent: false);

		var addTemp = CreateEffect<AddTempCard>(sourceCard);
		addTemp.cardCount = 1;
		addTemp.curseCardTypeID = CreateScriptableObject<StringSO>();
		addTemp.curseCardTypeID.value = "curse_type";

		int deckCountBefore = CombatManager.combinedDeckZone.Count;

		EffectChainManager.MakeANewEffectRecorder(sourceCard, addTemp.gameObject);
		addTemp.CopyEnemyCurseCardToThem();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(deckCountBefore + 1, CombatManager.combinedDeckZone.Count, "Should copy curse card to deck");
		var copiedCard = CombatManager.combinedDeckZone[0];
		Assert.AreEqual(EnemyStatus, copiedCard.GetComponent<CardScript>().myStatusRef, "Copied card should belong to enemy (the opponent of source)");
		Assert.IsTrue(copiedCard.GetComponent<CardScript>().myStatusEffects.Contains(EnumStorage.StatusEffect.Power), "Copied card should retain original status effects");
	}

	[Test]
	public void CopyEnemyCurseCardToThem_NoMatch_DoesNothing()
	{
		var sourceCard = CreateCard(true, "SourceCard");
		var enemyCard = CreateCard(false, "EnemyCard");
		enemyCard.GetComponent<CardScript>().cardTypeID = "other_type";
		CombatManager.combinedDeckZone.Add(enemyCard);

		var addTemp = CreateEffect<AddTempCard>(sourceCard);
		addTemp.curseCardTypeID = CreateScriptableObject<StringSO>();
		addTemp.curseCardTypeID.value = "curse_type";

		int deckCountBefore = CombatManager.combinedDeckZone.Count;

		EffectChainManager.MakeANewEffectRecorder(sourceCard, addTemp.gameObject);
		addTemp.CopyEnemyCurseCardToThem();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(deckCountBefore, CombatManager.combinedDeckZone.Count, "Should not add any card when no match found");
	}

	[Test]
	public void AddCardToMe_CapturesAnimationRequests()
	{
		var sourceCard = CreateCard(true, "SourceCard");
		var cardToAdd = CreateCard(true, "CardToAdd");

		var addTemp = CreateEffect<AddTempCard>(sourceCard);
		addTemp.cardCount = 1;

		EffectChainManager.MakeANewEffectRecorder(sourceCard, addTemp.gameObject);
		addTemp.AddCardToMe(cardToAdd);

		var recorder = EffectChainManager.currentEffectRecorder.GetComponent<EffectRecorder>();
		Assert.AreEqual(2, recorder.animationRequests.Count, "Should capture 2 animation requests per card");
		Assert.AreEqual(AnimationRequestType.MoveToPopUpPosition, recorder.animationRequests[0].type, "First should be MoveToPopUpPosition");
		Assert.AreEqual(AnimationRequestType.SlotIn, recorder.animationRequests[1].type, "Second should be SlotIn");

		EffectChainManager.Me.CloseOpenedChain();
	}

	[Test]
	public void AddCardToMe_EnemyCard_AddsToEnemyDeck()
	{
		var sourceCard = CreateCard(false, "EnemySource");
		var cardToAdd = CreateCard(false, "CardToAdd");
		int deckCountBefore = CombatManager.combinedDeckZone.Count;

		var addTemp = CreateEffect<AddTempCard>(sourceCard);
		addTemp.cardCount = 1;

		EffectChainManager.MakeANewEffectRecorder(sourceCard, addTemp.gameObject);
		addTemp.AddCardToMe(cardToAdd);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(deckCountBefore + 1, CombatManager.combinedDeckZone.Count, "Should add 1 card to deck");
		var addedCard = CombatManager.combinedDeckZone[0];
		Assert.AreEqual(EnemyStatus, addedCard.GetComponent<CardScript>().myStatusRef, "Enemy card should add to enemy deck");
	}

	[Test]
	public void AddSelfToThem_EnemyCard_AddsCopiesToOwnerDeck()
	{
		var sourceCard = CreateCard(false, "EnemySource");
		int deckCountBefore = CombatManager.combinedDeckZone.Count;

		var addTemp = CreateEffect<AddTempCard>(sourceCard);
		addTemp.cardCount = 1;

		EffectChainManager.MakeANewEffectRecorder(sourceCard, addTemp.gameObject);
		addTemp.AddSelfToThem();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(deckCountBefore + 1, CombatManager.combinedDeckZone.Count, "Should add 1 copy to deck");
		var addedCard = CombatManager.combinedDeckZone[0];
		Assert.AreEqual(OwnerStatus, addedCard.GetComponent<CardScript>().myStatusRef, "Enemy AddSelfToThem should add to owner deck");
	}
}
