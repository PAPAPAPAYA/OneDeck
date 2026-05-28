using System.Collections.Generic;
using DefaultNamespace;
using DefaultNamespace.Effects;
using DefaultNamespace.SOScripts;
using NUnit.Framework;
using UnityEngine;

public class StatusEffectTests : HeadlessCombatTestFixture
{
	[Test]
	public void EnhanceCurse_AppliesPowerToEnemyCard()
	{
		var curseCard = CreateCard(true, "Curser");
		var target = CreateCard(false, "CursedEnemy");
		target.GetComponent<CardScript>().cardTypeID = "curse_target";
		CombatManager.combinedDeckZone.Add(target);

		var curse = CreateEffect<CurseEffect>(curseCard);
		curse.cardTypeID = CreateScriptableObject<StringSO>();
		curse.cardTypeID.value = "curse_target";

		EffectChainManager.MakeANewEffectRecorder(curseCard, curse.gameObject);
		curse.EnhanceCurse(2);
		EffectChainManager.Me.CloseOpenedChain();

		var targetScript = target.GetComponent<CardScript>();
		int powerCount = 0;
		foreach (var effect in targetScript.myStatusEffects)
		{
			if (effect == EnumStorage.StatusEffect.Power) powerCount++;
		}
		Assert.AreEqual(2, powerCount, "Should apply 2 Power stacks to enemy card");
	}

	[Test]
	public void EnhanceCurse_CapturesStatusEffectProjectileRequest()
	{
		var curseCard = CreateCard(true, "Curser");
		var target = CreateCard(false, "CursedEnemy");
		target.GetComponent<CardScript>().cardTypeID = "curse_target";
		CombatManager.combinedDeckZone.Add(target);

		var curse = CreateEffect<CurseEffect>(curseCard);
		curse.cardTypeID = CreateScriptableObject<StringSO>();
		curse.cardTypeID.value = "curse_target";

		EffectChainManager.MakeANewEffectRecorder(curseCard, curse.gameObject);
		curse.EnhanceCurse(1);

		var recorder = EffectChainManager.currentEffectRecorder.GetComponent<EffectRecorder>();
		bool hasProjectile = false;
		foreach (var req in recorder.animationRequests)
		{
			if (req.type == AnimationRequestType.StatusEffectProjectile)
			{
				hasProjectile = true;
				Assert.AreEqual(curseCard, req.attackerCard, "Projectile should originate from curse card");
				Assert.AreEqual(target, req.targetCard, "Projectile should target the cursed card");
			}
		}
		Assert.IsTrue(hasProjectile, "Should capture StatusEffectProjectile animation request");

		EffectChainManager.Me.CloseOpenedChain();
	}

	[Test]
	public void EnhanceCurse_CapturesStatusEffectChangeRequest()
	{
		var curseCard = CreateCard(true, "Curser");
		var target = CreateCard(false, "CursedEnemy");
		target.GetComponent<CardScript>().cardTypeID = "curse_target";
		CombatManager.combinedDeckZone.Add(target);

		var curse = CreateEffect<CurseEffect>(curseCard);
		curse.cardTypeID = CreateScriptableObject<StringSO>();
		curse.cardTypeID.value = "curse_target";

		EffectChainManager.MakeANewEffectRecorder(curseCard, curse.gameObject);
		curse.EnhanceCurse(3);

		var recorder = EffectChainManager.currentEffectRecorder.GetComponent<EffectRecorder>();
		bool hasChange = false;
		foreach (var req in recorder.animationRequests)
		{
			if (req.type == AnimationRequestType.StatusEffectChange)
			{
				hasChange = true;
				Assert.AreEqual(target, req.targetCard, "Should target the cursed card");
				Assert.AreEqual(EnumStorage.StatusEffect.Power, req.statusEffect, "Should be Power status effect");
				Assert.AreEqual(3, req.statusEffectAmount, "Should apply 3 stacks");
			}
		}
		Assert.IsTrue(hasChange, "Should capture StatusEffectChange animation request");

		EffectChainManager.Me.CloseOpenedChain();
	}

	[Test]
	public void EnhanceCurse_SpawnsCardWhenNoneExists()
	{
		var curseCard = CreateCard(true, "Curser");
		// No target card in deck

		var curse = CreateEffect<CurseEffect>(curseCard);
		curse.cardTypeID = CreateScriptableObject<StringSO>();
		curse.cardTypeID.value = "curse_target";
		// Provide a simple prefab for spawning
		curse.cardPrefab = CreateCard(false, "SpawnedCursedCard");
		curse.cardPrefab.GetComponent<CardScript>().cardTypeID = "curse_target";

		int deckCountBefore = CombatManager.combinedDeckZone.Count;

		EffectChainManager.MakeANewEffectRecorder(curseCard, curse.gameObject);
		curse.EnhanceCurse(1);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.Greater(CombatManager.combinedDeckZone.Count, deckCountBefore, "Should spawn a new card into deck");

		// Verify the spawned card has Power
		bool foundPower = false;
		foreach (var card in CombatManager.combinedDeckZone)
		{
			var cs = card.GetComponent<CardScript>();
			if (cs.cardTypeID == "curse_target" && cs.myStatusEffects.Contains(EnumStorage.StatusEffect.Power))
			{
				foundPower = true;
				break;
			}
		}
		Assert.IsTrue(foundPower, "Spawned card should have Power status effect");
	}

	[Test]
	public void EnhanceFriendlyCurse_AppliesPowerToFriendlyCard()
	{
		var curseCard = CreateCard(true, "Curser");
		var target = CreateCard(true, "CursedFriendly");
		target.GetComponent<CardScript>().cardTypeID = "curse_target";
		CombatManager.combinedDeckZone.Add(target);

		var curse = CreateEffect<CurseEffect>(curseCard);
		curse.cardTypeID = CreateScriptableObject<StringSO>();
		curse.cardTypeID.value = "curse_target";

		EffectChainManager.MakeANewEffectRecorder(curseCard, curse.gameObject);
		curse.EnhanceFriendlyCurse(2);
		EffectChainManager.Me.CloseOpenedChain();

		var targetScript = target.GetComponent<CardScript>();
		int powerCount = 0;
		foreach (var effect in targetScript.myStatusEffects)
		{
			if (effect == EnumStorage.StatusEffect.Power) powerCount++;
		}
		Assert.AreEqual(2, powerCount, "Should apply 2 Power stacks to friendly card");
	}

	[Test]
	public void EnhanceCurse_RaisesOnAnyCardGotPowerEvent()
	{
		var curseCard = CreateCard(true, "Curser");
		var target = CreateCard(false, "CursedEnemy");
		target.GetComponent<CardScript>().cardTypeID = "curse_target";
		CombatManager.combinedDeckZone.Add(target);

		var curse = CreateEffect<CurseEffect>(curseCard);
		curse.cardTypeID = CreateScriptableObject<StringSO>();
		curse.cardTypeID.value = "curse_target";

		bool eventRaised = false;
		RegisterEventCallback(GameEventStorage.onAnyCardGotPower, () => eventRaised = true);

		EffectChainManager.MakeANewEffectRecorder(curseCard, curse.gameObject);
		curse.EnhanceCurse(1);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.IsTrue(eventRaised, "onAnyCardGotPower should be raised when applying Power");
	}

	[Test]
	public void AmplifyStatusEffectGain_DirectCall_AmplifiesPower()
	{
		var card = CreateCard(true, "AmplifierCard");
		var cardScript = card.GetComponent<CardScript>();

		var amplifier = CreateEffect<StatusEffectAmplifierEffect>(card);
		amplifier.statusEffectToCount = EnumStorage.StatusEffect.Power;
		amplifier.statusEffectToGive = EnumStorage.StatusEffect.Power;
		amplifier.statusEffectMultiplier = 3;
		amplifier.canStatusEffectBeStacked = true;

		CombatManager.lastCardGotStatusEffect = cardScript;
		ValueTrackerManager.lastAppliedStatusEffectRef.value = EnumStorage.StatusEffect.Power;
		ValueTrackerManager.lastAppliedStatusEffectAmountRef.value = 2;

		amplifier.AmplifyStatusEffectGain();

		int powerCount = EnumStorage.GetStatusEffectCount(cardScript.myStatusEffects, EnumStorage.StatusEffect.Power);
		Assert.AreEqual(4, powerCount, "Multiplier=3, baseAmount=2, extra=2*(3-1)=4");
	}

	[Test]
	public void AmplifyStatusEffectGain_EventTriggered_AmplifiesPower()
	{
		var card = CreateCard(true, "AmplifierCard");
		var cardScript = card.GetComponent<CardScript>();

		var container = CreateCostContainer(card);

		var amplifier = CreateEffect<StatusEffectAmplifierEffect>(card);
		amplifier.statusEffectToCount = EnumStorage.StatusEffect.Power;
		amplifier.statusEffectToGive = EnumStorage.StatusEffect.Power;
		amplifier.statusEffectMultiplier = 2;
		amplifier.canStatusEffectBeStacked = true;

		container.effectEvent.AddListener(() => amplifier.AmplifyStatusEffectGain());

		var listenerObj = CreateGameObject("StatusEffectListener");
		listenerObj.transform.SetParent(card.transform);
		var listener = listenerObj.AddComponent<GameEventListener>();
		listener.@event = GameEventStorage.onMeGotStatusEffect;
		listener.response.AddListener(() => container.InvokeEffectEventVoid());
		GameEventStorage.onMeGotStatusEffect.RegisterListener(listener);

		var giver = CreateEffect<StatusEffectGiverEffect>(card);
		giver.statusEffectToGive = EnumStorage.StatusEffect.Power;

		EffectChainManager.MakeANewEffectRecorder(card, giver.gameObject);
		giver.GiveSelfStatusEffect(1);
		EffectChainManager.Me.PopCurrentRecorder();
		EffectChainManager.Me.CloseOpenedChain();

		int powerCount = EnumStorage.GetStatusEffectCount(cardScript.myStatusEffects, EnumStorage.StatusEffect.Power);
		Assert.AreEqual(2, powerCount, "Should have 1 base + 1 amplified Power");
	}

	[Test]
	public void EnhanceCurse_EnemyCard_AppliesPowerToFriendlyCard()
	{
		var curseCard = CreateCard(false, "EnemyCurser");
		var target = CreateCard(true, "FriendlyTarget");
		target.GetComponent<CardScript>().cardTypeID = "curse_target";
		CombatManager.combinedDeckZone.Add(target);

		var curse = CreateEffect<CurseEffect>(curseCard);
		curse.cardTypeID = CreateScriptableObject<StringSO>();
		curse.cardTypeID.value = "curse_target";

		EffectChainManager.MakeANewEffectRecorder(curseCard, curse.gameObject);
		curse.EnhanceCurse(2);
		EffectChainManager.Me.CloseOpenedChain();

		var targetScript = target.GetComponent<CardScript>();
		int powerCount = 0;
		foreach (var effect in targetScript.myStatusEffects)
		{
			if (effect == EnumStorage.StatusEffect.Power) powerCount++;
		}
		Assert.AreEqual(2, powerCount, "Enemy EnhanceCurse should apply Power to friendly (player) card");
	}

	[Test]
	public void EnhanceFriendlyCurse_EnemyCard_AppliesPowerToEnemyCard()
	{
		var curseCard = CreateCard(false, "EnemyCurser");
		var target = CreateCard(false, "EnemyTarget");
		target.GetComponent<CardScript>().cardTypeID = "curse_target";
		CombatManager.combinedDeckZone.Add(target);

		var curse = CreateEffect<CurseEffect>(curseCard);
		curse.cardTypeID = CreateScriptableObject<StringSO>();
		curse.cardTypeID.value = "curse_target";

		EffectChainManager.MakeANewEffectRecorder(curseCard, curse.gameObject);
		curse.EnhanceFriendlyCurse(2);
		EffectChainManager.Me.CloseOpenedChain();

		var targetScript = target.GetComponent<CardScript>();
		int powerCount = 0;
		foreach (var effect in targetScript.myStatusEffects)
		{
			if (effect == EnumStorage.StatusEffect.Power) powerCount++;
		}
		Assert.AreEqual(2, powerCount, "Enemy EnhanceFriendlyCurse should apply Power to enemy (self) card");
	}
}
