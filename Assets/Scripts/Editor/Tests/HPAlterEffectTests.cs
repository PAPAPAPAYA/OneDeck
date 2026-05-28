using DefaultNamespace;
using NUnit.Framework;
using UnityEngine;

public class HPAlterEffectTests : HeadlessCombatTestFixture
{
	[Test]
	public void DecreaseTheirHp_DamagesEnemy()
	{
		var card = CreateCard(true, "Attacker");
		var hpa = CreateEffect<HPAlterEffect>(card);
		hpa.baseDmg = CreateScriptableObject<IntSO>();
		hpa.baseDmg.value = 5;

		EffectChainManager.MakeANewEffectRecorder(card, hpa.gameObject);
		hpa.DecreaseTheirHp();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(95, EnemyStatus.hp, "Enemy should take 5 damage");
	}

	[Test]
	public void DecreaseTheirHp_CapturesAttackAnimationRequest()
	{
		var card = CreateCard(true, "Attacker");
		var hpa = CreateEffect<HPAlterEffect>(card);
		hpa.baseDmg = CreateScriptableObject<IntSO>();
		hpa.baseDmg.value = 3;

		EffectChainManager.MakeANewEffectRecorder(card, hpa.gameObject);
		hpa.DecreaseTheirHp();

		var recorder = EffectChainManager.currentEffectRecorder.GetComponent<EffectRecorder>();
		Assert.AreEqual(1, recorder.animationRequests.Count, "Should capture 1 animation request");
		Assert.AreEqual(AnimationRequestType.Attack, recorder.animationRequests[0].type, "Should be Attack type");
		Assert.AreEqual(card, recorder.animationRequests[0].attackerCard, "Attacker should be the source card");

		EffectChainManager.Me.CloseOpenedChain();
	}

	[Test]
	public void DecreaseTheirHp_StatusEffectDamage_SkipsAnimationCapture()
	{
		var card = CreateCard(true, "Attacker");
		var hpa = CreateEffect<HPAlterEffect>(card);
		hpa.baseDmg = CreateScriptableObject<IntSO>();
		hpa.baseDmg.value = 3;
		hpa.isStatusEffectDamage = true;

		EffectChainManager.MakeANewEffectRecorder(card, hpa.gameObject);
		hpa.DecreaseTheirHp();

		var recorder = EffectChainManager.currentEffectRecorder.GetComponent<EffectRecorder>();
		Assert.AreEqual(0, recorder.animationRequests.Count, "Status effect damage should not capture animation");
		Assert.AreEqual(97, EnemyStatus.hp, "Damage should still resolve");

		EffectChainManager.Me.CloseOpenedChain();
	}

	[Test]
	public void DecreaseTheirHp_PowerStatusEffect_IncreasesDamage()
	{
		var card = CreateCard(true, "Attacker");
		card.GetComponent<CardScript>().myStatusEffects.Add(EnumStorage.StatusEffect.Power);

		var hpa = CreateEffect<HPAlterEffect>(card);
		hpa.baseDmg = CreateScriptableObject<IntSO>();
		hpa.baseDmg.value = 3;

		EffectChainManager.MakeANewEffectRecorder(card, hpa.gameObject);
		hpa.DecreaseTheirHp();
		EffectChainManager.Me.CloseOpenedChain();

		// baseDmg 3 + Power 1 = 4 damage
		Assert.AreEqual(96, EnemyStatus.hp, "Power should add +1 damage");
	}

	[Test]
	public void DecreaseMyHp_DamagesSelf()
	{
		var card = CreateCard(true, "SelfDamager");
		var hpa = CreateEffect<HPAlterEffect>(card);
		hpa.baseDmg = CreateScriptableObject<IntSO>();
		hpa.baseDmg.value = 5;

		EffectChainManager.MakeANewEffectRecorder(card, hpa.gameObject);
		hpa.DecreaseMyHp();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(95, OwnerStatus.hp, "Owner should take 5 self-damage");
	}

	[Test]
	public void DecreaseMyHp_CapturesAttackAnimationRequest()
	{
		var card = CreateCard(true, "SelfDamager");
		var hpa = CreateEffect<HPAlterEffect>(card);
		hpa.baseDmg = CreateScriptableObject<IntSO>();
		hpa.baseDmg.value = 4;

		EffectChainManager.MakeANewEffectRecorder(card, hpa.gameObject);
		hpa.DecreaseMyHp();

		var recorder = EffectChainManager.currentEffectRecorder.GetComponent<EffectRecorder>();
		Assert.AreEqual(1, recorder.animationRequests.Count, "Should capture 1 animation request for self-damage");
		Assert.AreEqual(AnimationRequestType.Attack, recorder.animationRequests[0].type);

		EffectChainManager.Me.CloseOpenedChain();
	}

	[Test]
	public void DecreaseTheirHp_ShieldAbsorbsDamageFirst()
	{
		EnemyStatus.shield = 5;
		var card = CreateCard(true, "Attacker");
		var hpa = CreateEffect<HPAlterEffect>(card);
		hpa.baseDmg = CreateScriptableObject<IntSO>();
		hpa.baseDmg.value = 3;

		EffectChainManager.MakeANewEffectRecorder(card, hpa.gameObject);
		hpa.DecreaseTheirHp();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(2, EnemyStatus.shield, "Shield should absorb 3 damage");
		Assert.AreEqual(100, EnemyStatus.hp, "HP should remain unchanged");
	}

	[Test]
	public void DecreaseTheirHp_ShieldBreaksAndDamagesHp()
	{
		EnemyStatus.shield = 2;
		var card = CreateCard(true, "Attacker");
		var hpa = CreateEffect<HPAlterEffect>(card);
		hpa.baseDmg = CreateScriptableObject<IntSO>();
		hpa.baseDmg.value = 5;

		EffectChainManager.MakeANewEffectRecorder(card, hpa.gameObject);
		hpa.DecreaseTheirHp();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(0, EnemyStatus.shield, "Shield should be depleted");
		Assert.AreEqual(97, EnemyStatus.hp, "Remaining 3 damage should go to HP");
	}

	[Test]
	public void DecreaseTheirHp_DoesNotGoBelowZero()
	{
		EnemyStatus.hp = 2;
		var card = CreateCard(true, "Attacker");
		var hpa = CreateEffect<HPAlterEffect>(card);
		hpa.baseDmg = CreateScriptableObject<IntSO>();
		hpa.baseDmg.value = 10;

		EffectChainManager.MakeANewEffectRecorder(card, hpa.gameObject);
		hpa.DecreaseTheirHp();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(0, EnemyStatus.hp, "HP should not go below zero");
	}

	[Test]
	public void DecreaseTheirHp_BasedOnLostHp_CalculatesCorrectly()
	{
		OwnerStatus.hp = 60; // lost 40 hp
		var card = CreateCard(true, "Attacker");
		var hpa = CreateEffect<HPAlterEffect>(card);
		hpa.baseDmg = CreateScriptableObject<IntSO>();
		hpa.baseDmg.value = 0;

		EffectChainManager.MakeANewEffectRecorder(card, hpa.gameObject);
		hpa.DecreaseTheirHp_BasedOnLostHp(0);
		EffectChainManager.Me.CloseOpenedChain();

		// (100 - 60) / 2 = 20 extra damage
		Assert.AreEqual(80, EnemyStatus.hp, "Damage should be based on lost HP / 2");
	}

	[Test]
	public void DecreaseTheirHp_BasedOnInfectedCardsOwned_CountsCorrectly()
	{
		var attacker = CreateCard(true, "Attacker");
		var infected1 = CreateCard(true, "Infected1");
		infected1.GetComponent<CardScript>().myStatusEffects.Add(EnumStorage.StatusEffect.Infected);
		var infected2 = CreateCard(true, "Infected2");
		infected2.GetComponent<CardScript>().myStatusEffects.Add(EnumStorage.StatusEffect.Infected);
		var enemyCard = CreateCard(false, "Enemy");

		CombatManager.combinedDeckZone.Add(infected1);
		CombatManager.combinedDeckZone.Add(infected2);
		CombatManager.combinedDeckZone.Add(enemyCard);

		var hpa = CreateEffect<HPAlterEffect>(attacker);
		hpa.baseDmg = CreateScriptableObject<IntSO>();
		hpa.baseDmg.value = 0;

		EffectChainManager.MakeANewEffectRecorder(attacker, hpa.gameObject);
		hpa.DecreaseTheirHp_BasedOnInfectedCardsOwned(0);
		EffectChainManager.Me.CloseOpenedChain();

		// 2 infected cards = 2 extra damage
		Assert.AreEqual(98, EnemyStatus.hp, "Should deal damage equal to infected card count");
	}

	[Test]
	public void IncreaseMyHp_HealsSelf()
	{
		OwnerStatus.hp = 50;
		var card = CreateCard(true, "Healer");
		var hpa = CreateEffect<HPAlterEffect>(card);

		EffectChainManager.MakeANewEffectRecorder(card, hpa.gameObject);
		hpa.IncreaseMyHp(20);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(70, OwnerStatus.hp, "Should heal 20 HP");
	}

	[Test]
	public void IncreaseMyHp_DoesNotExceedMaxHp()
	{
		OwnerStatus.hp = 95;
		var card = CreateCard(true, "Healer");
		var hpa = CreateEffect<HPAlterEffect>(card);

		EffectChainManager.MakeANewEffectRecorder(card, hpa.gameObject);
		hpa.IncreaseMyHp(20);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(100, OwnerStatus.hp, "Should cap at max HP");
	}

	[Test]
	public void DecreaseTheirHp_EnemyCard_DamagesOwner()
	{
		var card = CreateCard(false, "EnemyAttacker");
		var hpa = CreateEffect<HPAlterEffect>(card);
		hpa.baseDmg = CreateScriptableObject<IntSO>();
		hpa.baseDmg.value = 5;

		EffectChainManager.MakeANewEffectRecorder(card, hpa.gameObject);
		hpa.DecreaseTheirHp();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(95, OwnerStatus.hp, "Enemy card should deal damage to owner");
		Assert.AreEqual(100, EnemyStatus.hp, "Enemy HP should remain unchanged");
	}

	[Test]
	public void DecreaseMyHp_EnemyCard_DamagesEnemySelf()
	{
		var card = CreateCard(false, "EnemySelfDamager");
		var hpa = CreateEffect<HPAlterEffect>(card);
		hpa.baseDmg = CreateScriptableObject<IntSO>();
		hpa.baseDmg.value = 5;

		EffectChainManager.MakeANewEffectRecorder(card, hpa.gameObject);
		hpa.DecreaseMyHp();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(95, EnemyStatus.hp, "Enemy card should deal self-damage to enemy");
		Assert.AreEqual(100, OwnerStatus.hp, "Owner HP should remain unchanged");
	}

	[Test]
	public void IncreaseMyHp_EnemyCard_HealsEnemySelf()
	{
		EnemyStatus.hp = 50;
		var card = CreateCard(false, "EnemyHealer");
		var hpa = CreateEffect<HPAlterEffect>(card);

		EffectChainManager.MakeANewEffectRecorder(card, hpa.gameObject);
		hpa.IncreaseMyHp(20);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(70, EnemyStatus.hp, "Enemy card should heal enemy self");
		Assert.AreEqual(100, OwnerStatus.hp, "Owner HP should remain unchanged");
	}

	[Test]
	public void IncreaseTheirHp_EnemyCard_HealsOwner()
	{
		OwnerStatus.hp = 50;
		var card = CreateCard(false, "EnemyHealer");
		var hpa = CreateEffect<HPAlterEffect>(card);

		EffectChainManager.MakeANewEffectRecorder(card, hpa.gameObject);
		hpa.IncreaseTheirHp(20);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(70, OwnerStatus.hp, "Enemy card should heal owner");
		Assert.AreEqual(100, EnemyStatus.hp, "Enemy HP should remain unchanged");
	}
}
