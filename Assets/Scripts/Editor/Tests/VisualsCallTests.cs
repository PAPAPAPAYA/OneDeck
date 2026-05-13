using DefaultNamespace.Effects;
using NUnit.Framework;
using UnityEngine;

public class VisualsCallTests : HeadlessCombatTestFixture
{
	[Test]
	public void StageEffect_CallsMoveCardToTop()
	{
		var card = CreateCard(true, "StageCard");
		var other = CreateCard(true, "OtherCard");
		var target = CreateCard(true, "TargetCard");
		// Target must NOT be at top (index != Count-1) to be eligible for staging
		CombatManager.combinedDeckZone.Add(other);
		CombatManager.combinedDeckZone.Add(target);

		var stageEffect = CreateEffect<StageEffect>(card);
		stageEffect.StageMyCards(1);

		Assert.AreEqual(1, NullVisuals.moveCardToTopCalls, "StageEffect should call MoveCardToTop");
	}

	[Test]
	public void BuryEffect_CallsMoveCardToBottom()
	{
		var card = CreateCard(true, "BuryCard");
		var other = CreateCard(false, "OtherCard");
		var target = CreateCard(false, "TargetCard");
		// Target must NOT be at bottom (index != 0) to be eligible for burying
		CombatManager.combinedDeckZone.Add(target);
		CombatManager.combinedDeckZone.Add(other);

		var buryEffect = CreateEffect<BuryEffect>(card);
		buryEffect.BuryTheirCards(1);

		Assert.AreEqual(1, NullVisuals.moveCardToBottomCalls, "BuryEffect should call MoveCardToBottom");
	}

	[Test]
	public void ExileEffect_CallsDestroyCard()
	{
		var card = CreateCard(true, "ExileCard");
		CombatManager.combinedDeckZone.Add(card);

		var exileEffect = CreateEffect<ExileEffect>(card);
		exileEffect.ExileSelf();

		Assert.AreEqual(1, NullVisuals.destroyCardCalls, "ExileEffect should call DestroyCardWithAnimation");
	}

	[Test]
	public void MinionCostEffect_CallsDestroyCardAndSyncDeck()
	{
		var card = CreateCard(true, "CostCard");
		card.GetComponent<CardScript>().minionCostCount = 1;
		card.GetComponent<CardScript>().minionCostOwner = EnumStorage.TargetType.Me;

		var minion = CreateMinion(true, "Minion");
		CombatManager.combinedDeckZone.Add(minion);

		var costEffect = CreateEffect<MinionCostEffect>(card);
		costEffect.ExecuteMinionCost();

		Assert.AreEqual(1, NullVisuals.destroyCardCalls, "MinionCostEffect should call DestroyCardWithAnimation");
		Assert.AreEqual(1, NullVisuals.syncDeckCalls, "MinionCostEffect should call SyncPhysicalCardsWithCombinedDeck");
		Assert.AreEqual(1, NullVisuals.updateTargetCalls, "MinionCostEffect should call UpdateAllPhysicalCardTargets");
	}

	[Test]
	public void ExileCostEffect_CallsDestroyCardAndSyncDeck()
	{
		var card = CreateCard(true, "CostCard");
		card.GetComponent<CardScript>().exileCostCount = 1;
		card.GetComponent<CardScript>().exileCostOwner = EnumStorage.TargetType.Me;

		var target = CreateCard(true, "TargetCard");
		CombatManager.combinedDeckZone.Add(target);

		var costEffect = CreateEffect<ExileCostEffect>(card);
		costEffect.ExecuteExileCost();

		Assert.AreEqual(1, NullVisuals.destroyCardCalls, "ExileCostEffect should call DestroyCardWithAnimation");
		Assert.AreEqual(1, NullVisuals.syncDeckCalls, "ExileCostEffect should call SyncPhysicalCardsWithCombinedDeck");
		Assert.AreEqual(1, NullVisuals.updateTargetCalls, "ExileCostEffect should call UpdateAllPhysicalCardTargets");
	}

	[Test]
	public void ExileCostEffect_WithTypeID_FilterWorks()
	{
		var card = CreateCard(true, "CostCard");
		card.GetComponent<CardScript>().exileCostCount = 1;
		card.GetComponent<CardScript>().exileCostOwner = EnumStorage.TargetType.Me;
		card.GetComponent<CardScript>().exileCostCardTypeID = "FLY";

		var fly = CreateCard(true, "FlyCard", "FLY");
		var other = CreateCard(true, "OtherCard", "OTHER");
		CombatManager.combinedDeckZone.Add(fly);
		CombatManager.combinedDeckZone.Add(other);

		var costEffect = CreateEffect<ExileCostEffect>(card);
		costEffect.ExecuteExileCost();

		Assert.AreEqual(1, NullVisuals.destroyCardCalls, "ExileCostEffect should exile exactly 1 matching card");
	}

	[Test]
	public void BuryEffect_CallsSyncDeckAndUpdateTargets()
	{
		var card = CreateCard(true, "BuryCard");
		var other = CreateCard(false, "OtherCard");
		var target = CreateCard(false, "TargetCard");
		CombatManager.combinedDeckZone.Add(target);
		CombatManager.combinedDeckZone.Add(other);

		var buryEffect = CreateEffect<BuryEffect>(card);
		buryEffect.BuryTheirCards(1);

		Assert.AreEqual(1, NullVisuals.syncDeckCalls, "BuryEffect should call SyncPhysicalCardsWithCombinedDeck");
		Assert.AreEqual(1, NullVisuals.updateTargetCalls, "BuryEffect should call UpdateAllPhysicalCardTargets");
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
