using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode tests for roadmap step 4 batch 2: E3 round-end event (raise order vs round
/// start + per-round counter reset), E4 causer-based creature-burial counters, E6
/// creatureOnly filter on StageCardWithMaxAttack, and FINAL_ESCORT's flag-gated
/// one-shot round-end stage.
/// </summary>
public class RoundEndAndBurialCountersTests : HeadlessCombatTestFixture
{
	[Test]
	public void MyCardBuriesEnemyCreature_CountsForOwnerCauser()
	{
		var buryCard = CreateCard(true, "Burier");
		var enemyCreature = CreateCard(false, "EnemyCreature");
		enemyCreature.GetComponent<CardScript>().isCreature = true;
		CombatManager.combinedDeckZone.Add(buryCard);
		CombatManager.combinedDeckZone.Add(enemyCreature);

		var bury = CreateEffect<BuryEffect>(buryCard);
		EffectChainManager.MakeANewEffectRecorder(buryCard, bury.gameObject);
		bury.BuryTheirCards(1);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(1, ValueTrackerManager.creaturesBuriedByOwnerThisRoundRef.value, "owner-caused creature burial");
		Assert.AreEqual(0, ValueTrackerManager.creaturesBuriedByEnemyThisRoundRef.value, "enemy causer counter untouched");
		Assert.AreEqual(1, ValueTrackerManager.enemyCardsBuriedCountRef.value, "legacy victim-side counter still tracks the victim faction");
	}

	[Test]
	public void EnemyCardBuriesMyCreature_CountsForEnemyCauserOnly()
	{
		var enemyBurier = CreateCard(false, "EnemyBurier");
		var myCreature = CreateCard(true, "MyCreature");
		myCreature.GetComponent<CardScript>().isCreature = true;
		CombatManager.combinedDeckZone.Add(enemyBurier);
		CombatManager.combinedDeckZone.Add(myCreature);

		var bury = CreateEffect<BuryEffect>(enemyBurier);
		EffectChainManager.MakeANewEffectRecorder(enemyBurier, bury.gameObject);
		bury.BuryTheirCards(1);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(1, ValueTrackerManager.creaturesBuriedByEnemyThisRoundRef.value, "enemy-caused creature burial");
		Assert.AreEqual(0, ValueTrackerManager.creaturesBuriedByOwnerThisRoundRef.value, "enemy-caused burials never count for my side");
	}

	[Test]
	public void MySacrificeOfMyOwnCreature_CountsForMe()
	{
		var buryCard = CreateCard(true, "Sacrificer");
		var myCreature = CreateCard(true, "MyCreature");
		myCreature.GetComponent<CardScript>().isCreature = true;
		CombatManager.combinedDeckZone.Add(buryCard);
		CombatManager.combinedDeckZone.Add(myCreature);

		var bury = CreateEffect<BuryEffect>(buryCard);
		EffectChainManager.MakeANewEffectRecorder(buryCard, bury.gameObject);
		bury.BuryMyCards(1);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(1, ValueTrackerManager.creaturesBuriedByOwnerThisRoundRef.value, "my own sacrificed creatures count for me (D2 ruling)");
	}

	[Test]
	public void BuryNonCreature_DoesNotTouchCreatureCounters()
	{
		var buryCard = CreateCard(true, "Burier");
		var enemyNonCreature = CreateCard(false, "EnemyCurseCard");
		CombatManager.combinedDeckZone.Add(buryCard);
		CombatManager.combinedDeckZone.Add(enemyNonCreature);

		var bury = CreateEffect<BuryEffect>(buryCard);
		EffectChainManager.MakeANewEffectRecorder(buryCard, bury.gameObject);
		bury.BuryTheirCards(1);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(0, ValueTrackerManager.creaturesBuriedByOwnerThisRoundRef.value);
		Assert.AreEqual(0, ValueTrackerManager.creaturesBuriedByEnemyThisRoundRef.value);
		Assert.AreEqual(1, ValueTrackerManager.enemyCardsBuriedCountRef.value, "legacy counter counts every card");
	}

	[Test]
	public void RoundEnd_FiresBeforeRoundStart_AndBeforeCounterReset()
	{
		var eventOrder = new List<string>();
		RegisterEventCallback(GameEventStorage.onRoundEnd, () => eventOrder.Add("roundEnd"));
		RegisterEventCallback(GameEventStorage.beforeRoundStart, () => eventOrder.Add("roundStart"));

		ValueTrackerManager.creaturesBuriedByOwnerThisRoundRef.value = 3;
		ValueTrackerManager.creaturesBuriedByEnemyThisRoundRef.value = 2;
		CombatManager.combinedDeckZone.Add(CreateCard(true, "DeckFiller"));

		CombatManager.OnStartCardShuffleAnimationComplete();

		Assert.AreEqual(new[] { "roundEnd", "roundStart" }, eventOrder.ToArray(),
			"round end must fire before round start (HandleNewRoundStart)");
		Assert.AreEqual(0, ValueTrackerManager.creaturesBuriedByOwnerThisRoundRef.value, "causer counters reset after round end effects could read them");
		Assert.AreEqual(0, ValueTrackerManager.creaturesBuriedByEnemyThisRoundRef.value);
		Assert.AreEqual(0, ValueTrackerManager.creatureAttackTimesAuraOwnerThisRoundRef.value, "aura also resets at round start");
	}

	[Test]
	public void StageCardWithMaxAttack_CreatureOnly_SkipsHigherAttackNonCreature()
	{
		var stager = CreateCard(true, "EscortHolder");
		var creature = CreateCard(true, "BigCreature");
		creature.GetComponent<CardScript>().isCreature = true;
		creature.GetComponent<CardScript>().printedAttack = 5;
		var nonCreature = CreateCard(true, "BiggerNonCreature");
		nonCreature.GetComponent<CardScript>().printedAttack = 9;
		var filler = CreateCard(true, "TopFiller");

		// deck order: [creature, nonCreature, filler] — filler sits at the top slot and is
		// excluded by IsCardAtTop, so the choice is between the other two.
		CombatManager.combinedDeckZone.Add(creature);
		CombatManager.combinedDeckZone.Add(nonCreature);
		CombatManager.combinedDeckZone.Add(filler);

		var stage = CreateEffect<StageEffect>(stager);
		stage.targetFriendly = true;
		stage.creatureOnly = true;

		EffectChainManager.MakeANewEffectRecorder(stager, stage.gameObject);
		stage.StageCardWithMaxAttack();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(creature, CombatManager.combinedDeckZone[CombatManager.combinedDeckZone.Count - 1],
			"creatureOnly stages the creature even though the non-creature has higher attack");
	}

	[Test]
	public void StageMaxAttackCreatureIfArmed_StageOnceThenDisarm()
	{
		var escort = CreateCard(true, "Escort");
		var bigCreature = CreateCard(true, "BigCreature");
		bigCreature.GetComponent<CardScript>().isCreature = true;
		bigCreature.GetComponent<CardScript>().printedAttack = 5;
		CombatManager.combinedDeckZone.Add(escort);
		CombatManager.combinedDeckZone.Add(bigCreature);
		CombatManager.combinedDeckZone.Add(CreateCard(true, "TopFiller"));

		var stage = CreateEffect<StageEffect>(escort);
		stage.targetFriendly = true;
		stage.creatureOnly = true;

		// not armed: no-op
		EffectChainManager.MakeANewEffectRecorder(escort, stage.gameObject);
		stage.StageMaxAttackCreatureIfArmed();
		EffectChainManager.Me.CloseOpenedChain();
		Assert.AreNotEqual(bigCreature, CombatManager.combinedDeckZone[CombatManager.combinedDeckZone.Count - 1],
			"unarmed round-end stage is a no-op");

		// armed: stages exactly once
		stage.ArmRoundEndStageMaxAttackCreature();
		EffectChainManager.MakeANewEffectRecorder(escort, stage.gameObject);
		stage.StageMaxAttackCreatureIfArmed();
		EffectChainManager.Me.CloseOpenedChain();
		Assert.AreEqual(bigCreature, CombatManager.combinedDeckZone[CombatManager.combinedDeckZone.Count - 1],
			"armed round-end stage moves the highest-attack friendly creature to the deck top");

		// disarmed after firing
		int count = CombatManager.combinedDeckZone.Count;
		EffectChainManager.MakeANewEffectRecorder(escort, stage.gameObject);
		stage.StageMaxAttackCreatureIfArmed();
		EffectChainManager.Me.CloseOpenedChain();
		Assert.AreEqual(bigCreature, CombatManager.combinedDeckZone[CombatManager.combinedDeckZone.Count - 1],
			"flag disarmed: repeat call does not restage");
		Assert.AreEqual(count, CombatManager.combinedDeckZone.Count);
	}

	[Test]
	public void EnhanceCurseTimes_EnhancesOncePerCountPoint()
	{
		GameEventStorage.curseCardTypeID.value = "JU_ON";
		var tallyCard = CreateCard(true, "Tally");
		var juOn = CreateCard(false, "Curse", "JU_ON");
		CombatManager.combinedDeckZone.Add(tallyCard);
		CombatManager.combinedDeckZone.Add(juOn);

		var curse = CreateEffect<DefaultNamespace.Effects.CurseEffect>(tallyCard);
		curse.cardTypeID = GameEventStorage.curseCardTypeID;
		curse.ownerIntSO = ValueTrackerManager.creaturesBuriedByOwnerThisRoundRef;
		curse.ownerIntSO.value = 3;

		EffectChainManager.MakeANewEffectRecorder(tallyCard, curse.gameObject);
		curse.EnhanceCurseTimes_BasedOnIntSO();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(3, juOn.GetComponent<CardScript>().attackGrowth,
			"count of 3 burials means 3 separate +1 enhancements on the enemy curse");
	}
}
