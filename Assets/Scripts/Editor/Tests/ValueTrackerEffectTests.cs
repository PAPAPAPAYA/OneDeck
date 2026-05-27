using System.Collections.Generic;
using DefaultNamespace;
using DefaultNamespace.Effects;
using DefaultNamespace.SOScripts;
using NUnit.Framework;
using UnityEngine;

public class ValueTrackerEffectTests : HeadlessCombatTestFixture
{
	#region Bury Count Tracking

	[Test]
	public void BuryMyCards_IncrementsOwnerBuriedCount()
	{
		var buryCaster = CreateCard(true, "BuryCaster");
		var padding = CreateCard(true, "Padding");
		var friendly1 = CreateCard(true, "Friendly1");
		var friendly2 = CreateCard(true, "Friendly2");
		CombatManager.combinedDeckZone.Add(padding);
		CombatManager.combinedDeckZone.Add(friendly1);
		CombatManager.combinedDeckZone.Add(friendly2);

		var buryEffect = CreateEffect<BuryEffect>(buryCaster);

		EffectChainManager.MakeANewEffectRecorder(buryCaster, buryEffect.gameObject);
		buryEffect.BuryMyCards(2);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(2, ValueTrackerManager.ownerCardsBuriedCountRef.value, "Burying 2 friendly cards should increment ownerCardsBuriedCountRef by 2");
		Assert.AreEqual(0, ValueTrackerManager.enemyCardsBuriedCountRef.value, "Enemy buried count should remain 0");
	}

	[Test]
	public void BuryTheirCards_IncrementsEnemyBuriedCount()
	{
		var buryCaster = CreateCard(true, "BuryCaster");
		var padding = CreateCard(false, "Padding");
		var enemy1 = CreateCard(false, "Enemy1");
		var enemy2 = CreateCard(false, "Enemy2");
		CombatManager.combinedDeckZone.Add(padding);
		CombatManager.combinedDeckZone.Add(enemy1);
		CombatManager.combinedDeckZone.Add(enemy2);

		var buryEffect = CreateEffect<BuryEffect>(buryCaster);

		EffectChainManager.MakeANewEffectRecorder(buryCaster, buryEffect.gameObject);
		buryEffect.BuryTheirCards(2);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(0, ValueTrackerManager.ownerCardsBuriedCountRef.value, "Owner buried count should remain 0");
		Assert.AreEqual(2, ValueTrackerManager.enemyCardsBuriedCountRef.value, "Burying 2 enemy cards should increment enemyCardsBuriedCountRef by 2");
	}

	[Test]
	public void BuryMixedCards_IncrementsBothCounts()
	{
		var buryCaster = CreateCard(true, "BuryCaster");
		var padding = CreateCard(true, "Padding");
		var friendly = CreateCard(true, "Friendly");
		var enemy = CreateCard(false, "Enemy");
		CombatManager.combinedDeckZone.Add(padding);
		CombatManager.combinedDeckZone.Add(friendly);
		CombatManager.combinedDeckZone.Add(enemy);

		var buryEffect = CreateEffect<BuryEffect>(buryCaster);

		EffectChainManager.MakeANewEffectRecorder(buryCaster, buryEffect.gameObject);
		buryEffect.BuryMyCards(1);
		EffectChainManager.Me.CloseOpenedChain();

		EffectChainManager.MakeANewEffectRecorder(buryCaster, buryEffect.gameObject);
		buryEffect.BuryTheirCards(1);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(1, ValueTrackerManager.ownerCardsBuriedCountRef.value);
		Assert.AreEqual(1, ValueTrackerManager.enemyCardsBuriedCountRef.value);
	}

	#endregion

	#region HP Alter Based on Buried Count

	[Test]
	public void DecreaseTheirHp_BasedOnOpponentBuriedCount_DealsCorrectDamage()
	{
		ValueTrackerManager.enemyCardsBuriedCountRef.value = 3;

		var attacker = CreateCard(true, "Attacker");
		var hpAlter = CreateEffect<HPAlterEffect>(attacker);
		hpAlter.baseDmg = CreateScriptableObject<IntSO>();
		hpAlter.baseDmg.value = 0;

		EffectChainManager.MakeANewEffectRecorder(attacker, hpAlter.gameObject);
		hpAlter.DecreaseTheirHp_BasedOnOpponentBuriedCount();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(97, EnemyStatus.hp, "Should deal 3 damage based on enemy buried count");
	}

	[Test]
	public void DecreaseTheirHp_BasedOnOpponentBuriedCount_EnemyCardUsesOwnerBuriedCount()
	{
		ValueTrackerManager.ownerCardsBuriedCountRef.value = 5;

		var attacker = CreateCard(false, "EnemyAttacker");
		var hpAlter = CreateEffect<HPAlterEffect>(attacker);
		hpAlter.baseDmg = CreateScriptableObject<IntSO>();
		hpAlter.baseDmg.value = 0;

		EffectChainManager.MakeANewEffectRecorder(attacker, hpAlter.gameObject);
		hpAlter.DecreaseTheirHp_BasedOnOpponentBuriedCount();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(95, OwnerStatus.hp, "Enemy card should deal damage equal to ownerCardsBuriedCountRef");
	}

	[Test]
	public void DecreaseTheirHpTimes_BasedOnOpponentBuriedCount_DealsCorrectDamage()
	{
		ValueTrackerManager.enemyCardsBuriedCountRef.value = 2;

		var attacker = CreateCard(true, "Attacker");
		var hpAlter = CreateEffect<HPAlterEffect>(attacker);
		hpAlter.baseDmg = CreateScriptableObject<IntSO>();
		hpAlter.baseDmg.value = 3;

		EffectChainManager.MakeANewEffectRecorder(attacker, hpAlter.gameObject);
		hpAlter.DecreaseTheirHpTimes_BasedOnOpponentBuriedCount();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(94, EnemyStatus.hp, "Should deal 3*2=6 damage");
	}

	#endregion

	#region Staged Count Based Status Effect

	[Test]
	public void GiveStatusEffectToXFriendly_BasedOnStaged_OwnerUsesStagedOwnerRef()
	{
		ValueTrackerManager.stagedOwnerRef.value = 2;

		var giverCard = CreateCard(true, "Giver");
		var friendly1 = CreateCard(true, "Friendly1");
		var friendly2 = CreateCard(true, "Friendly2");
		var friendly3 = CreateCard(true, "Friendly3");
		CombatManager.combinedDeckZone.Add(friendly1);
		CombatManager.combinedDeckZone.Add(friendly2);
		CombatManager.combinedDeckZone.Add(friendly3);

		var giver = CreateEffect<StatusEffectGiverEffect>(giverCard);
		giver.statusEffectToGive = EnumStorage.StatusEffect.Power;

		EffectChainManager.MakeANewEffectRecorder(giverCard, giver.gameObject);
		giver.GiveStatusEffectToXFriendly_BasedOnStaged(1);
		EffectChainManager.Me.CloseOpenedChain();

		int totalPower =
			EnumStorage.GetStatusEffectCount(friendly1.GetComponent<CardScript>().myStatusEffects, EnumStorage.StatusEffect.Power) +
			EnumStorage.GetStatusEffectCount(friendly2.GetComponent<CardScript>().myStatusEffects, EnumStorage.StatusEffect.Power) +
			EnumStorage.GetStatusEffectCount(friendly3.GetComponent<CardScript>().myStatusEffects, EnumStorage.StatusEffect.Power);

		Assert.AreEqual(2, totalPower, "Should give Power to 2 friendly cards based on stagedOwnerRef");
	}

	[Test]
	public void GiveStatusEffectToXFriendly_BasedOnStaged_EnemyUsesStagedEnemyRef()
	{
		ValueTrackerManager.stagedEnemyRef.value = 3;

		var giverCard = CreateCard(false, "EnemyGiver");
		var enemy1 = CreateCard(false, "Enemy1");
		var enemy2 = CreateCard(false, "Enemy2");
		var enemy3 = CreateCard(false, "Enemy3");
		var enemy4 = CreateCard(false, "Enemy4");
		CombatManager.combinedDeckZone.Add(enemy1);
		CombatManager.combinedDeckZone.Add(enemy2);
		CombatManager.combinedDeckZone.Add(enemy3);
		CombatManager.combinedDeckZone.Add(enemy4);

		var giver = CreateEffect<StatusEffectGiverEffect>(giverCard);
		giver.statusEffectToGive = EnumStorage.StatusEffect.Power;

		EffectChainManager.MakeANewEffectRecorder(giverCard, giver.gameObject);
		giver.GiveStatusEffectToXFriendly_BasedOnStaged(1);
		EffectChainManager.Me.CloseOpenedChain();

		int totalPower =
			EnumStorage.GetStatusEffectCount(enemy1.GetComponent<CardScript>().myStatusEffects, EnumStorage.StatusEffect.Power) +
			EnumStorage.GetStatusEffectCount(enemy2.GetComponent<CardScript>().myStatusEffects, EnumStorage.StatusEffect.Power) +
			EnumStorage.GetStatusEffectCount(enemy3.GetComponent<CardScript>().myStatusEffects, EnumStorage.StatusEffect.Power) +
			EnumStorage.GetStatusEffectCount(enemy4.GetComponent<CardScript>().myStatusEffects, EnumStorage.StatusEffect.Power);

		Assert.AreEqual(3, totalPower, "Enemy giver should give Power to 3 friendly cards based on stagedEnemyRef");
	}

	[Test]
	public void GiveStatusEffectToXFriendly_BasedOnStaged_ZeroStaged_DoesNothing()
	{
		ValueTrackerManager.stagedOwnerRef.value = 0;

		var giverCard = CreateCard(true, "Giver");
		var friendly = CreateCard(true, "Friendly");
		CombatManager.combinedDeckZone.Add(friendly);

		var giver = CreateEffect<StatusEffectGiverEffect>(giverCard);
		giver.statusEffectToGive = EnumStorage.StatusEffect.Power;

		EffectChainManager.MakeANewEffectRecorder(giverCard, giver.gameObject);
		giver.GiveStatusEffectToXFriendly_BasedOnStaged(1);
		EffectChainManager.Me.CloseOpenedChain();

		int powerCount = EnumStorage.GetStatusEffectCount(friendly.GetComponent<CardScript>().myStatusEffects, EnumStorage.StatusEffect.Power);
		Assert.AreEqual(0, powerCount, "Zero staged count should give no status effects");
	}

	#endregion
}
