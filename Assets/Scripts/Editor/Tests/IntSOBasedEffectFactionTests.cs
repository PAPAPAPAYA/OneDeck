using System.Collections.Generic;
using DefaultNamespace;
using DefaultNamespace.Effects;
using DefaultNamespace.SOScripts;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Headless tests for IntSO-based field-style effect methods.
/// Covers faction reversal (owner vs enemy) and value-tracking boundaries for the cards listed in
/// docs/BasedOnIntSO_EffectMethods_RefactorPlan.md section 6.
/// </summary>
public class IntSOBasedEffectFactionTests : HeadlessCombatTestFixture
{
	#region Helpers

	private IntSO CreateIntSO(int value)
	{
		var so = CreateScriptableObject<IntSO>();
		so.value = value;
		return so;
	}

	private int CountStatusEffect(GameObject card, EnumStorage.StatusEffect effect)
	{
		return EnumStorage.GetStatusEffectCount(card.GetComponent<CardScript>().myStatusEffects, effect);
	}

	private bool HasStatusEffect(GameObject card, EnumStorage.StatusEffect effect)
	{
		return card.GetComponent<CardScript>().myStatusEffects.Contains(effect);
	}

	#endregion

	#region ALL_FOR_ONE - HPAlterEffect.DecreaseTheirHp_BasedOnIntSO

	[Test]
	public void AllForOne_OwnerCard_UsesOwnerIntSOAndDamagesEnemy()
	{
		var card = CreateCard(true, "OwnerAllForOne");
		var hpa = CreateEffect<HPAlterEffect>(card);
		hpa.baseDmg = CreateIntSO(0);
		hpa.ownerIntSO = CreateIntSO(5);
		hpa.enemyIntSO = CreateIntSO(9);

		EffectChainManager.MakeANewEffectRecorder(card, hpa.gameObject);
		hpa.DecreaseTheirHp_BasedOnIntSO();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(95, EnemyStatus.hp, "Owner ALL_FOR_ONE should deal ownerIntSO=5 damage to enemy");
		Assert.AreEqual(100, OwnerStatus.hp, "Owner should not take damage");
	}

	[Test]
	public void AllForOne_EnemyCard_UsesEnemyIntSOAndDamagesOwner()
	{
		var card = CreateCard(false, "EnemyAllForOne");
		var hpa = CreateEffect<HPAlterEffect>(card);
		hpa.baseDmg = CreateIntSO(0);
		hpa.ownerIntSO = CreateIntSO(5);
		hpa.enemyIntSO = CreateIntSO(9);

		EffectChainManager.MakeANewEffectRecorder(card, hpa.gameObject);
		hpa.DecreaseTheirHp_BasedOnIntSO();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(91, OwnerStatus.hp, "Enemy ALL_FOR_ONE should deal enemyIntSO=9 damage to owner");
		Assert.AreEqual(100, EnemyStatus.hp, "Enemy should not take damage");
	}

	[Test]
	public void AllForOne_IntSOValueZero_DoesNoDamage()
	{
		var card = CreateCard(true, "OwnerAllForOne");
		var hpa = CreateEffect<HPAlterEffect>(card);
		hpa.baseDmg = CreateIntSO(0);
		hpa.ownerIntSO = CreateIntSO(0);
		hpa.enemyIntSO = CreateIntSO(0);

		EffectChainManager.MakeANewEffectRecorder(card, hpa.gameObject);
		hpa.DecreaseTheirHp_BasedOnIntSO();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(100, EnemyStatus.hp, "Zero IntSO value should deal no damage");
		Assert.AreEqual(100, OwnerStatus.hp, "Owner HP should remain unchanged");
	}

	#endregion

	#region BODY_CANON - HPAlterEffect.DecreaseTheirHpTimes_BasedOnIntSO

	[Test]
	public void BodyCanon_OwnerCard_UsesOwnerIntSOAndDamagesEnemyMultipleTimes()
	{
		var card = CreateCard(true, "OwnerBodyCanon");
		var hpa = CreateEffect<HPAlterEffect>(card);
		hpa.baseDmg = CreateIntSO(2);
		hpa.ownerIntSO = CreateIntSO(3);
		hpa.enemyIntSO = CreateIntSO(4);

		EffectChainManager.MakeANewEffectRecorder(card, hpa.gameObject);
		hpa.DecreaseTheirHpTimes_BasedOnIntSO();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(94, EnemyStatus.hp, "Owner BODY_CANON should hit 3 times for 2 damage each");
		Assert.AreEqual(100, OwnerStatus.hp, "Owner should not take damage");
	}

	[Test]
	public void BodyCanon_EnemyCard_UsesEnemyIntSOAndDamagesOwnerMultipleTimes()
	{
		var card = CreateCard(false, "EnemyBodyCanon");
		var hpa = CreateEffect<HPAlterEffect>(card);
		hpa.baseDmg = CreateIntSO(2);
		hpa.ownerIntSO = CreateIntSO(3);
		hpa.enemyIntSO = CreateIntSO(4);

		EffectChainManager.MakeANewEffectRecorder(card, hpa.gameObject);
		hpa.DecreaseTheirHpTimes_BasedOnIntSO();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(92, OwnerStatus.hp, "Enemy BODY_CANON should hit 4 times for 2 damage each");
		Assert.AreEqual(100, EnemyStatus.hp, "Enemy should not take damage");
	}

	[Test]
	public void BodyCanon_IntSOValueZero_DoesNoDamage()
	{
		var card = CreateCard(true, "OwnerBodyCanon");
		var hpa = CreateEffect<HPAlterEffect>(card);
		hpa.baseDmg = CreateIntSO(2);
		hpa.ownerIntSO = CreateIntSO(0);
		hpa.enemyIntSO = CreateIntSO(0);

		EffectChainManager.MakeANewEffectRecorder(card, hpa.gameObject);
		hpa.DecreaseTheirHpTimes_BasedOnIntSO();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(100, EnemyStatus.hp, "Zero IntSO value should result in no hits");
	}

	#endregion

	#region CURSE_THIRST_SHAMAN - StatusEffectGiverEffect.GiveStatusEffectToXFriendly_BasedOnIntSO

	[Test]
	public void CurseThirstShaman_OwnerCard_UsesOwnerIntSOAndBuffsFriendlyCards()
	{
		var shaman = CreateCard(true, "OwnerCurseThirstShaman");
		var friendly1 = CreateCard(true, "Friendly1");
		var friendly2 = CreateCard(true, "Friendly2");
		var enemyCard = CreateCard(false, "EnemyCard");
		CombatManager.combinedDeckZone.Add(friendly1);
		CombatManager.combinedDeckZone.Add(friendly2);
		CombatManager.combinedDeckZone.Add(enemyCard);

		var giver = CreateEffect<StatusEffectGiverEffect>(shaman);
		giver.statusEffectToGive = EnumStorage.StatusEffect.Power;
		giver.ownerIntSO = CreateIntSO(1);
		giver.enemyIntSO = CreateIntSO(2);

		EffectChainManager.MakeANewEffectRecorder(shaman, giver.gameObject);
		giver.GiveStatusEffectToXFriendly_BasedOnIntSO();
		EffectChainManager.Me.CloseOpenedChain();

		int buffedFriendlyCount = 0;
		if (HasStatusEffect(friendly1, EnumStorage.StatusEffect.Power)) buffedFriendlyCount++;
		if (HasStatusEffect(friendly2, EnumStorage.StatusEffect.Power)) buffedFriendlyCount++;

		Assert.AreEqual(1, buffedFriendlyCount, "Owner CURSE_THIRST_SHAMAN should buff ownerIntSO=1 friendly card");
		Assert.IsFalse(HasStatusEffect(enemyCard, EnumStorage.StatusEffect.Power), "Enemy card should not be buffed");
	}

	[Test]
	public void CurseThirstShaman_EnemyCard_UsesEnemyIntSOAndBuffsEnemyFriendlyCards()
	{
		var shaman = CreateCard(false, "EnemyCurseThirstShaman");
		var enemyFriendly1 = CreateCard(false, "EnemyFriendly1");
		var enemyFriendly2 = CreateCard(false, "EnemyFriendly2");
		var ownerCard = CreateCard(true, "OwnerCard");
		CombatManager.combinedDeckZone.Add(enemyFriendly1);
		CombatManager.combinedDeckZone.Add(enemyFriendly2);
		CombatManager.combinedDeckZone.Add(ownerCard);

		var giver = CreateEffect<StatusEffectGiverEffect>(shaman);
		giver.statusEffectToGive = EnumStorage.StatusEffect.Power;
		giver.ownerIntSO = CreateIntSO(1);
		giver.enemyIntSO = CreateIntSO(2);

		EffectChainManager.MakeANewEffectRecorder(shaman, giver.gameObject);
		giver.GiveStatusEffectToXFriendly_BasedOnIntSO();
		EffectChainManager.Me.CloseOpenedChain();

		int buffedEnemyFriendlyCount = 0;
		if (HasStatusEffect(enemyFriendly1, EnumStorage.StatusEffect.Power)) buffedEnemyFriendlyCount++;
		if (HasStatusEffect(enemyFriendly2, EnumStorage.StatusEffect.Power)) buffedEnemyFriendlyCount++;

		Assert.AreEqual(2, buffedEnemyFriendlyCount, "Enemy CURSE_THIRST_SHAMAN should buff enemyIntSO=2 enemy-friendly cards");
		Assert.IsFalse(HasStatusEffect(ownerCard, EnumStorage.StatusEffect.Power), "Owner card should not be buffed");
	}

	[Test]
	public void CurseThirstShaman_IntSOValueExceedsFriendlyCount_BuffsAllAvailable()
	{
		var shaman = CreateCard(true, "OwnerCurseThirstShaman");
		var friendly1 = CreateCard(true, "Friendly1");
		CombatManager.combinedDeckZone.Add(friendly1);

		var giver = CreateEffect<StatusEffectGiverEffect>(shaman);
		giver.statusEffectToGive = EnumStorage.StatusEffect.Power;
		giver.ownerIntSO = CreateIntSO(99);
		giver.enemyIntSO = CreateIntSO(99);

		EffectChainManager.MakeANewEffectRecorder(shaman, giver.gameObject);
		giver.GiveStatusEffectToXFriendly_BasedOnIntSO();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.IsTrue(HasStatusEffect(friendly1, EnumStorage.StatusEffect.Power), "Should buff the only available friendly card");
	}

	[Test]
	public void CurseThirstShaman_IntSOValueZero_DoesNothing()
	{
		var shaman = CreateCard(true, "OwnerCurseThirstShaman");
		var friendly1 = CreateCard(true, "Friendly1");
		CombatManager.combinedDeckZone.Add(friendly1);

		var giver = CreateEffect<StatusEffectGiverEffect>(shaman);
		giver.statusEffectToGive = EnumStorage.StatusEffect.Power;
		giver.ownerIntSO = CreateIntSO(0);
		giver.enemyIntSO = CreateIntSO(0);

		EffectChainManager.MakeANewEffectRecorder(shaman, giver.gameObject);
		giver.GiveStatusEffectToXFriendly_BasedOnIntSO();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.IsFalse(HasStatusEffect(friendly1, EnumStorage.StatusEffect.Power), "Zero IntSO value should not buff");
	}

	[Test]
	public void CurseThirstShaman_UpdatesLastAppliedStatusEffectTracker()
	{
		var shaman = CreateCard(true, "OwnerCurseThirstShaman");
		var friendly1 = CreateCard(true, "Friendly1");
		CombatManager.combinedDeckZone.Add(friendly1);

		var giver = CreateEffect<StatusEffectGiverEffect>(shaman);
		giver.statusEffectToGive = EnumStorage.StatusEffect.Power;
		giver.ownerIntSO = CreateIntSO(1);
		giver.enemyIntSO = CreateIntSO(1);

		EffectChainManager.MakeANewEffectRecorder(shaman, giver.gameObject);
		giver.GiveStatusEffectToXFriendly_BasedOnIntSO();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(EnumStorage.StatusEffect.Power, ValueTrackerManager.lastAppliedStatusEffectRef.value,
			"lastAppliedStatusEffectRef should track Power");
		Assert.AreEqual(1, ValueTrackerManager.lastAppliedStatusEffectAmountRef.value,
			"lastAppliedStatusEffectAmountRef should track 1 layer per target");
	}

	#endregion

	#region CURSED_SKELETON - CurseEffect.EnhanceCurse_BasedOnIntSO

	[Test]
	public void CursedSkeleton_OwnerCard_UsesOwnerIntSOAndPowersEnemyCurseCard()
	{
		var curser = CreateCard(true, "OwnerCursedSkeleton");
		var enemyCurse = CreateCard(false, "EnemyCurseCard");
		enemyCurse.GetComponent<CardScript>().cardTypeID = "curse_target";
		CombatManager.combinedDeckZone.Add(enemyCurse);

		var curse = CreateEffect<CurseEffect>(curser);
		curse.cardTypeID = CreateScriptableObject<StringSO>();
		curse.cardTypeID.value = "curse_target";
		curse.ownerIntSO = CreateIntSO(2);
		curse.enemyIntSO = CreateIntSO(5);

		EffectChainManager.MakeANewEffectRecorder(curser, curse.gameObject);
		curse.EnhanceCurse_BasedOnIntSO();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(2, CountStatusEffect(enemyCurse, EnumStorage.StatusEffect.Power),
			"Owner CURSED_SKELETON should add ownerIntSO=2 Power to enemy curse card");
	}

	[Test]
	public void CursedSkeleton_EnemyCard_UsesEnemyIntSOAndPowersOwnerCurseCard()
	{
		var curser = CreateCard(false, "EnemyCursedSkeleton");
		var ownerCurse = CreateCard(true, "OwnerCurseCard");
		ownerCurse.GetComponent<CardScript>().cardTypeID = "curse_target";
		CombatManager.combinedDeckZone.Add(ownerCurse);

		var curse = CreateEffect<CurseEffect>(curser);
		curse.cardTypeID = CreateScriptableObject<StringSO>();
		curse.cardTypeID.value = "curse_target";
		curse.ownerIntSO = CreateIntSO(2);
		curse.enemyIntSO = CreateIntSO(5);

		EffectChainManager.MakeANewEffectRecorder(curser, curse.gameObject);
		curse.EnhanceCurse_BasedOnIntSO();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(5, CountStatusEffect(ownerCurse, EnumStorage.StatusEffect.Power),
			"Enemy CURSED_SKELETON should add enemyIntSO=5 Power to owner curse card");
	}

	#endregion

	#region DETERIORATION - CurseEffect.EnhanceCurseWithCoefficient_BasedOnIntSO

	[Test]
	public void Deterioration_OwnerCard_UsesOwnerIntSOWithCoefficient()
	{
		var curser = CreateCard(true, "OwnerDeterioration");
		var enemyCurse = CreateCard(false, "EnemyCurseCard");
		enemyCurse.GetComponent<CardScript>().cardTypeID = "curse_target";
		CombatManager.combinedDeckZone.Add(enemyCurse);

		var curse = CreateEffect<CurseEffect>(curser);
		curse.cardTypeID = CreateScriptableObject<StringSO>();
		curse.cardTypeID.value = "curse_target";
		curse.powerCoefficient = 2;
		curse.ownerIntSO = CreateIntSO(6);
		curse.enemyIntSO = CreateIntSO(9);

		EffectChainManager.MakeANewEffectRecorder(curser, curse.gameObject);
		curse.EnhanceCurseWithCoefficient_BasedOnIntSO();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(3, CountStatusEffect(enemyCurse, EnumStorage.StatusEffect.Power),
			"Owner DETERIORATION should add ownerIntSO=6 / coefficient=2 = 3 Power");
	}

	[Test]
	public void Deterioration_EnemyCard_UsesEnemyIntSOWithCoefficient()
	{
		var curser = CreateCard(false, "EnemyDeterioration");
		var ownerCurse = CreateCard(true, "OwnerCurseCard");
		ownerCurse.GetComponent<CardScript>().cardTypeID = "curse_target";
		CombatManager.combinedDeckZone.Add(ownerCurse);

		var curse = CreateEffect<CurseEffect>(curser);
		curse.cardTypeID = CreateScriptableObject<StringSO>();
		curse.cardTypeID.value = "curse_target";
		curse.powerCoefficient = 2;
		curse.ownerIntSO = CreateIntSO(6);
		curse.enemyIntSO = CreateIntSO(9);

		EffectChainManager.MakeANewEffectRecorder(curser, curse.gameObject);
		curse.EnhanceCurseWithCoefficient_BasedOnIntSO();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(4, CountStatusEffect(ownerCurse, EnumStorage.StatusEffect.Power),
			"Enemy DETERIORATION should add enemyIntSO=9 / coefficient=2 = 4 Power");
	}

	[Test]
	public void Deterioration_IntSOValueZero_DoesNothing()
	{
		var curser = CreateCard(true, "OwnerDeterioration");
		var enemyCurse = CreateCard(false, "EnemyCurseCard");
		enemyCurse.GetComponent<CardScript>().cardTypeID = "curse_target";
		CombatManager.combinedDeckZone.Add(enemyCurse);

		var curse = CreateEffect<CurseEffect>(curser);
		curse.cardTypeID = CreateScriptableObject<StringSO>();
		curse.cardTypeID.value = "curse_target";
		curse.powerCoefficient = 2;
		curse.ownerIntSO = CreateIntSO(0);
		curse.enemyIntSO = CreateIntSO(0);

		EffectChainManager.MakeANewEffectRecorder(curser, curse.gameObject);
		curse.EnhanceCurseWithCoefficient_BasedOnIntSO();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(0, CountStatusEffect(enemyCurse, EnumStorage.StatusEffect.Power),
			"Zero IntSO value should add no Power");
	}

	#endregion

	#region GRAVE_INVITATION - BuryEffect.BuryTheirCards_BasedOnIntSO

	[Test]
	public void GraveInvitation_OwnerCard_UsesOwnerIntSOAndBuriesEnemyCards()
	{
		var inviter = CreateCard(true, "OwnerGraveInvitation");
		var dummyBottom = CreateCard(true, "DummyBottom");
		var enemyCard1 = CreateCard(false, "EnemyCard1");
		var enemyCard2 = CreateCard(false, "EnemyCard2");
		var ownerCard = CreateCard(true, "OwnerCard");

		CombatManager.combinedDeckZone.Add(dummyBottom);
		CombatManager.combinedDeckZone.Add(enemyCard1);
		CombatManager.combinedDeckZone.Add(enemyCard2);
		CombatManager.combinedDeckZone.Add(ownerCard);

		var bury = CreateEffect<BuryEffect>(inviter);
		bury.ownerIntSO = CreateIntSO(1);
		bury.enemyIntSO = CreateIntSO(2);

		EffectChainManager.MakeANewEffectRecorder(inviter, bury.gameObject);
		bury.BuryTheirCards_BasedOnIntSO();
		EffectChainManager.Me.CloseOpenedChain();

		var bottomCard = CombatManager.combinedDeckZone[0];
		Assert.IsTrue(bottomCard == enemyCard1 || bottomCard == enemyCard2,
			"Buried enemy card should move to bottom");
		Assert.IsTrue(CombatManager.combinedDeckZone.Contains(ownerCard), "Owner card should remain in deck");
	}

	[Test]
	public void GraveInvitation_EnemyCard_UsesEnemyIntSOAndBuriesOwnerCards()
	{
		var inviter = CreateCard(false, "EnemyGraveInvitation");
		var dummyBottom = CreateCard(false, "DummyBottom");
		var ownerCard1 = CreateCard(true, "OwnerCard1");
		var ownerCard2 = CreateCard(true, "OwnerCard2");
		var enemyCard = CreateCard(false, "EnemyCard");

		CombatManager.combinedDeckZone.Add(dummyBottom);
		CombatManager.combinedDeckZone.Add(ownerCard1);
		CombatManager.combinedDeckZone.Add(ownerCard2);
		CombatManager.combinedDeckZone.Add(enemyCard);

		var bury = CreateEffect<BuryEffect>(inviter);
		bury.ownerIntSO = CreateIntSO(1);
		bury.enemyIntSO = CreateIntSO(2);

		EffectChainManager.MakeANewEffectRecorder(inviter, bury.gameObject);
		bury.BuryTheirCards_BasedOnIntSO();
		EffectChainManager.Me.CloseOpenedChain();

		var bottomTwo = new HashSet<GameObject>
		{
			CombatManager.combinedDeckZone[0],
			CombatManager.combinedDeckZone[1]
		};
		Assert.IsTrue(bottomTwo.Contains(ownerCard1) && bottomTwo.Contains(ownerCard2),
			"Both buried owner cards should be at the bottom");
		Assert.IsTrue(CombatManager.combinedDeckZone.Contains(enemyCard), "Enemy card should remain in deck");
	}

	[Test]
	public void GraveInvitation_IntSOValueExceedsTargetCount_BuriesAllAvailable()
	{
		var inviter = CreateCard(true, "OwnerGraveInvitation");
		var dummyBottom = CreateCard(true, "DummyBottom");
		var enemyCard1 = CreateCard(false, "EnemyCard1");
		CombatManager.combinedDeckZone.Add(dummyBottom);
		CombatManager.combinedDeckZone.Add(enemyCard1);

		var bury = CreateEffect<BuryEffect>(inviter);
		bury.ownerIntSO = CreateIntSO(99);
		bury.enemyIntSO = CreateIntSO(99);

		EffectChainManager.MakeANewEffectRecorder(inviter, bury.gameObject);
		bury.BuryTheirCards_BasedOnIntSO();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(enemyCard1, CombatManager.combinedDeckZone[0], "Should bury the only available target");
	}

	[Test]
	public void GraveInvitation_IntSOValueZero_DoesNothing()
	{
		var inviter = CreateCard(true, "OwnerGraveInvitation");
		var dummyBottom = CreateCard(true, "DummyBottom");
		var enemyCard1 = CreateCard(false, "EnemyCard1");
		CombatManager.combinedDeckZone.Add(dummyBottom);
		CombatManager.combinedDeckZone.Add(enemyCard1);

		var bury = CreateEffect<BuryEffect>(inviter);
		bury.ownerIntSO = CreateIntSO(0);
		bury.enemyIntSO = CreateIntSO(0);

		EffectChainManager.MakeANewEffectRecorder(inviter, bury.gameObject);
		bury.BuryTheirCards_BasedOnIntSO();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(dummyBottom, CombatManager.combinedDeckZone[0], "Zero IntSO value should not bury");
		Assert.AreEqual(enemyCard1, CombatManager.combinedDeckZone[1], "Target card should remain in place");
	}

	[Test]
	public void GraveInvitation_UpdatesBuriedCountTracker()
	{
		var inviter = CreateCard(true, "OwnerGraveInvitation");
		var dummyBottom = CreateCard(true, "DummyBottom");
		var enemyCard1 = CreateCard(false, "EnemyCard1");
		var enemyCard2 = CreateCard(false, "EnemyCard2");
		CombatManager.combinedDeckZone.Add(dummyBottom);
		CombatManager.combinedDeckZone.Add(enemyCard1);
		CombatManager.combinedDeckZone.Add(enemyCard2);

		ValueTrackerManager.enemyCardsBuriedCountRef.value = 0;

		var bury = CreateEffect<BuryEffect>(inviter);
		bury.ownerIntSO = CreateIntSO(2);
		bury.enemyIntSO = CreateIntSO(2);

		EffectChainManager.MakeANewEffectRecorder(inviter, bury.gameObject);
		bury.BuryTheirCards_BasedOnIntSO();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(2, ValueTrackerManager.enemyCardsBuriedCountRef.value,
			"Burying 2 enemy cards should increase enemyCardsBuriedCountRef by 2");
	}

	#endregion
}
