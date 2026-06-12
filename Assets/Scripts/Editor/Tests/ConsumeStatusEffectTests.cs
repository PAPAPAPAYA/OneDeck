using DefaultNamespace;
using DefaultNamespace.Effects;
using NUnit.Framework;
using UnityEngine;

public class ConsumeStatusEffectTests : HeadlessCombatTestFixture
{
	[Test]
	public void ConsumeOwnStatusEffect_RemovesSpecifiedAmount()
	{
		var card = CreateCard(true, "Consumer");
		var cardScript = card.GetComponent<CardScript>();
		cardScript.myStatusEffects.Add(EnumStorage.StatusEffect.Power);
		cardScript.myStatusEffects.Add(EnumStorage.StatusEffect.Power);
		cardScript.myStatusEffects.Add(EnumStorage.StatusEffect.Infected);

		var consume = CreateEffect<ConsumeStatusEffect>(card);
		consume.statusEffectToConsume = EnumStorage.StatusEffect.Power;

		EffectChainManager.MakeANewEffectRecorder(card, consume.gameObject);
		consume.ConsumeOwnStatusEffect(2);
		EffectChainManager.Me.CloseOpenedChain();

		int powerCount = EnumStorage.GetStatusEffectCount(cardScript.myStatusEffects, EnumStorage.StatusEffect.Power);
		Assert.AreEqual(0, powerCount, "Should remove 2 Power stacks");
		Assert.IsTrue(cardScript.myStatusEffects.Contains(EnumStorage.StatusEffect.Infected), "Infected should remain");
	}

	[Test]
	public void ConsumeOwnStatusEffect_NotEnough_DoesNothing()
	{
		var card = CreateCard(true, "Consumer");
		var cardScript = card.GetComponent<CardScript>();
		cardScript.myStatusEffects.Add(EnumStorage.StatusEffect.Power);

		var consume = CreateEffect<ConsumeStatusEffect>(card);
		consume.statusEffectToConsume = EnumStorage.StatusEffect.Power;

		EffectChainManager.MakeANewEffectRecorder(card, consume.gameObject);
		consume.ConsumeOwnStatusEffect(2);
		EffectChainManager.Me.CloseOpenedChain();

		int powerCount = EnumStorage.GetStatusEffectCount(cardScript.myStatusEffects, EnumStorage.StatusEffect.Power);
		Assert.AreEqual(1, powerCount, "Should not consume when not enough stacks");
	}

	[Test]
	public void ConsumeOwnStatusEffect_CapturesAnimationRequests()
	{
		var card = CreateCard(true, "Consumer");
		var cardScript = card.GetComponent<CardScript>();
		cardScript.myStatusEffects.Add(EnumStorage.StatusEffect.Power);
		cardScript.myStatusEffects.Add(EnumStorage.StatusEffect.Power);

		var consume = CreateEffect<ConsumeStatusEffect>(card);
		consume.statusEffectToConsume = EnumStorage.StatusEffect.Power;

		EffectChainManager.MakeANewEffectRecorder(card, consume.gameObject);
		consume.ConsumeOwnStatusEffect(2);

		var recorder = EffectChainManager.currentEffectRecorder.GetComponent<EffectRecorder>();
		Assert.AreEqual(4, recorder.animationRequests.Count, "Should capture 4 animation requests (PopUp + StatusEffectProjectile + StatusEffectChange + SlotIn)");
		Assert.AreEqual(AnimationRequestType.PopUp, recorder.animationRequests[0].type, "First should be PopUp");
		Assert.AreEqual(AnimationRequestType.StatusEffectProjectile, recorder.animationRequests[1].type, "Second should be StatusEffectProjectile");
		Assert.AreEqual(2, recorder.animationRequests[1].projectileCount, "Projectile count should match consumed stacks");
		Assert.AreEqual(AnimationRequestType.StatusEffectChange, recorder.animationRequests[2].type, "Third should be StatusEffectChange");
		Assert.AreEqual(AnimationRequestType.SlotIn, recorder.animationRequests[3].type, "Fourth should be SlotIn");
		Assert.AreEqual(-2, recorder.animationRequests[2].statusEffectAmount, "StatusEffectChange should reflect -2 amount");

		EffectChainManager.Me.CloseOpenedChain();
	}

	[Test]
	public void ConsumeRandomEnemyCardsStatusEffect_RemovesFromEnemyCards()
	{
		var consumer = CreateCard(true, "Consumer");
		var enemy1 = CreateCard(false, "Enemy1");
		enemy1.GetComponent<CardScript>().myStatusEffects.Add(EnumStorage.StatusEffect.Power);
		var enemy2 = CreateCard(false, "Enemy2");
		enemy2.GetComponent<CardScript>().myStatusEffects.Add(EnumStorage.StatusEffect.Power);
		var friendly = CreateCard(true, "Friendly");
		friendly.GetComponent<CardScript>().myStatusEffects.Add(EnumStorage.StatusEffect.Power);

		CombatManager.combinedDeckZone.Add(enemy1);
		CombatManager.combinedDeckZone.Add(enemy2);
		CombatManager.combinedDeckZone.Add(friendly);

		var consume = CreateEffect<ConsumeStatusEffect>(consumer);
		consume.statusEffectToConsume = EnumStorage.StatusEffect.Power;

		EffectChainManager.MakeANewEffectRecorder(consumer, consume.gameObject);
		consume.ConsumeRandomEnemyCardsStatusEffect(2);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.IsFalse(enemy1.GetComponent<CardScript>().myStatusEffects.Contains(EnumStorage.StatusEffect.Power), "Enemy1 should lose Power");
		Assert.IsFalse(enemy2.GetComponent<CardScript>().myStatusEffects.Contains(EnumStorage.StatusEffect.Power), "Enemy2 should lose Power");
		Assert.IsTrue(friendly.GetComponent<CardScript>().myStatusEffects.Contains(EnumStorage.StatusEffect.Power), "Friendly should retain Power");
	}

	[Test]
	public void ConsumeRandomEnemyCardsStatusEffect_NoEligibleCards_DoesNothing()
	{
		var consumer = CreateCard(true, "Consumer");
		var enemy1 = CreateCard(false, "Enemy1");
		// No Power status effect on enemy
		CombatManager.combinedDeckZone.Add(enemy1);

		var consume = CreateEffect<ConsumeStatusEffect>(consumer);
		consume.statusEffectToConsume = EnumStorage.StatusEffect.Power;

		EffectChainManager.MakeANewEffectRecorder(consumer, consume.gameObject);
		consume.ConsumeRandomEnemyCardsStatusEffect(2);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(0, enemy1.GetComponent<CardScript>().myStatusEffects.Count, "Enemy should remain unchanged");
	}

	[Test]
	public void ConsumeRandomEnemyCardsStatusEffect_CapturesAnimationRequests()
	{
		var consumer = CreateCard(true, "Consumer");
		var enemy1 = CreateCard(false, "Enemy1");
		enemy1.GetComponent<CardScript>().myStatusEffects.Add(EnumStorage.StatusEffect.Power);
		CombatManager.combinedDeckZone.Add(enemy1);

		var consume = CreateEffect<ConsumeStatusEffect>(consumer);
		consume.statusEffectToConsume = EnumStorage.StatusEffect.Power;

		EffectChainManager.MakeANewEffectRecorder(consumer, consume.gameObject);
		consume.ConsumeRandomEnemyCardsStatusEffect(1);

		var recorder = EffectChainManager.currentEffectRecorder.GetComponent<EffectRecorder>();
		Assert.AreEqual(3, recorder.animationRequests.Count, "Should capture 3 animation requests per target");
		Assert.AreEqual(AnimationRequestType.PopUp, recorder.animationRequests[0].type, "First should be PopUp");
		Assert.AreEqual(AnimationRequestType.StatusEffectChange, recorder.animationRequests[1].type, "Second should be StatusEffectChange");
		Assert.AreEqual(AnimationRequestType.SlotIn, recorder.animationRequests[2].type, "Third should be SlotIn");

		EffectChainManager.Me.CloseOpenedChain();
	}

	[Test]
	public void ConsumeOwnStatusEffect_EnemyCard_ConsumesEnemyStatusEffect()
	{
		var card = CreateCard(false, "EnemyConsumer");
		var cardScript = card.GetComponent<CardScript>();
		cardScript.myStatusEffects.Add(EnumStorage.StatusEffect.Power);
		cardScript.myStatusEffects.Add(EnumStorage.StatusEffect.Power);

		var consume = CreateEffect<ConsumeStatusEffect>(card);
		consume.statusEffectToConsume = EnumStorage.StatusEffect.Power;

		EffectChainManager.MakeANewEffectRecorder(card, consume.gameObject);
		consume.ConsumeOwnStatusEffect(2);
		EffectChainManager.Me.CloseOpenedChain();

		int powerCount = EnumStorage.GetStatusEffectCount(cardScript.myStatusEffects, EnumStorage.StatusEffect.Power);
		Assert.AreEqual(0, powerCount, "Enemy card should consume its own Power stacks");
	}

	[Test]
	public void ConsumeRandomEnemyCardsStatusEffect_EnemyCard_RemovesFromFriendlyCards()
	{
		var consumer = CreateCard(false, "EnemyConsumer");
		var friendly1 = CreateCard(true, "Friendly1");
		friendly1.GetComponent<CardScript>().myStatusEffects.Add(EnumStorage.StatusEffect.Power);
		var enemy1 = CreateCard(false, "Enemy1");
		enemy1.GetComponent<CardScript>().myStatusEffects.Add(EnumStorage.StatusEffect.Power);

		CombatManager.combinedDeckZone.Add(friendly1);
		CombatManager.combinedDeckZone.Add(enemy1);

		var consume = CreateEffect<ConsumeStatusEffect>(consumer);
		consume.statusEffectToConsume = EnumStorage.StatusEffect.Power;

		EffectChainManager.MakeANewEffectRecorder(consumer, consume.gameObject);
		consume.ConsumeRandomEnemyCardsStatusEffect(1);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.IsFalse(friendly1.GetComponent<CardScript>().myStatusEffects.Contains(EnumStorage.StatusEffect.Power), "Friendly should lose Power (enemy's 'enemy' is friendly)");
		Assert.IsTrue(enemy1.GetComponent<CardScript>().myStatusEffects.Contains(EnumStorage.StatusEffect.Power), "Enemy should retain Power");
	}
}
