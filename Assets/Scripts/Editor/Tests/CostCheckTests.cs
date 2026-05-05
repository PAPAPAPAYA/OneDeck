using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class CostCheckTests : HeadlessCombatTestFixture
{
	[Test]
	public void ManaCost_Met_WhenEnoughManaStacks()
	{
		var card = CreateCard(true, "ManaCard");
		card.GetComponent<CardScript>().myStatusEffects.Add(EnumStorage.StatusEffect.Mana);
		card.GetComponent<CardScript>().myStatusEffects.Add(EnumStorage.StatusEffect.Mana);

		var cnt = CreateCostContainer(card);
		cnt.checkCostEvent.AddListener(() => cnt.CheckCost_Mana(2));

		var result = cnt.InvokeEffectEvent();

		Assert.IsTrue(result.success, "Mana cost should be met with 2 Mana stacks");
	}

	[Test]
	public void ManaCost_NotMet_WhenInsufficientMana()
	{
		var card = CreateCard(true, "ManaCard");
		card.GetComponent<CardScript>().myStatusEffects.Add(EnumStorage.StatusEffect.Mana);

		var cnt = CreateCostContainer(card);
		cnt.checkCostEvent.AddListener(() => cnt.CheckCost_Mana(2));

		var result = cnt.InvokeEffectEvent();

		Assert.IsFalse(result.success, "Mana cost should not be met with only 1 Mana stack");
		Assert.IsNotEmpty(result.failMessages, "Should contain failure message");
	}

	[Test]
	public void RestedCost_ConsumesRestAndBlocksEffect()
	{
		var card = CreateCard(true, "RestCard");
		card.GetComponent<CardScript>().myStatusEffects.Add(EnumStorage.StatusEffect.Rest);

		var cnt = CreateCostContainer(card);
		cnt.checkCostEvent.AddListener(() => cnt.CheckCost_Rested());

		var result = cnt.InvokeEffectEvent();

		Assert.IsFalse(result.success, "Rest cost should block effect activation");
		Assert.IsFalse(card.GetComponent<CardScript>().myStatusEffects.Contains(EnumStorage.StatusEffect.Rest),
			"Rest status effect should be consumed");
	}

	[Test]
	public void IndexBeforeStartCard_Met_WhenCardIsBeforeStartCard()
	{
		var startCard = CreateStartCard();
		var testCard = CreateCard(true, "TestCard");

		CombatManager.combinedDeckZone.Add(startCard);
		CombatManager.combinedDeckZone.Insert(0, testCard);

		var cnt = CreateCostContainer(testCard);
		cnt.checkCostEvent.AddListener(() => cnt.CheckCost_IndexBeforeStartCard());

		var result = cnt.InvokeEffectEvent();

		Assert.IsTrue(result.success, "Cost should be met when card is before Start Card");
	}

	[Test]
	public void IndexBeforeStartCard_NotMet_WhenCardIsAfterStartCard()
	{
		var startCard = CreateStartCard();
		var testCard = CreateCard(true, "TestCard");

		CombatManager.combinedDeckZone.Add(startCard);
		CombatManager.combinedDeckZone.Add(testCard);

		var cnt = CreateCostContainer(testCard);
		cnt.checkCostEvent.AddListener(() => cnt.CheckCost_IndexBeforeStartCard());

		var result = cnt.InvokeEffectEvent();

		Assert.IsFalse(result.success, "Cost should not be met when card is after Start Card");
	}

	[Test]
	public void HasEnemyCard_Met_WhenEnoughEnemyCardsInDeck()
	{
		var testCard = CreateCard(true, "TestCard");
		CombatManager.combinedDeckZone.Add(CreateCard(false, "Enemy1"));
		CombatManager.combinedDeckZone.Add(CreateCard(false, "Enemy2"));

		var cnt = CreateCostContainer(testCard);
		cnt.checkCostEvent.AddListener(() => cnt.CheckCost_HasEnemyCardInCombinedDeck(2));

		var result = cnt.InvokeEffectEvent();

		Assert.IsTrue(result.success, "Cost should be met with 2 enemy cards");
	}

	[Test]
	public void HasEnemyCard_NotMet_WhenNotEnoughEnemyCards()
	{
		var testCard = CreateCard(true, "TestCard");
		CombatManager.combinedDeckZone.Add(CreateCard(false, "Enemy1"));

		var cnt = CreateCostContainer(testCard);
		cnt.checkCostEvent.AddListener(() => cnt.CheckCost_HasEnemyCardInCombinedDeck(2));

		var result = cnt.InvokeEffectEvent();

		Assert.IsFalse(result.success, "Cost should not be met with only 1 enemy card");
	}

	[Test]
	public void CounterCost_Met_WhenEnoughCounterStacks()
	{
		var card = CreateCard(true, "CounterCard");
		card.GetComponent<CardScript>().myStatusEffects.Add(EnumStorage.StatusEffect.Counter);
		card.GetComponent<CardScript>().myStatusEffects.Add(EnumStorage.StatusEffect.Counter);

		var cnt = CreateCostContainer(card);
		cnt.checkCostEvent.AddListener(() => cnt.CheckCost_Counter(2));

		var result = cnt.InvokeEffectEvent();

		Assert.IsTrue(result.success, "Counter cost should be met with 2 Counter stacks");
	}

	[Test]
	public void CounterCost_NotMet_WhenInsufficientCounter()
	{
		var card = CreateCard(true, "CounterCard");
		card.GetComponent<CardScript>().myStatusEffects.Add(EnumStorage.StatusEffect.Counter);

		var cnt = CreateCostContainer(card);
		cnt.checkCostEvent.AddListener(() => cnt.CheckCost_Counter(2));

		var result = cnt.InvokeEffectEvent();

		Assert.IsFalse(result.success, "Counter cost should not be met with only 1 Counter stack");
	}
}
