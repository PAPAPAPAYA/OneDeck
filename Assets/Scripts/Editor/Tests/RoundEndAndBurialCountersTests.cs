using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode tests for roadmap step 4 batch 2: E3 round-end event (raise order vs round
/// start + per-round counter reset), E4 causer-based friendly-burial counters
/// (RELIC_TALLY; 2026-09-14 redesign: counts ALL friendly card types buried by a side,
/// not just creatures — enemy cards buried by me no longer count), E6
/// creatureOnly filter on StageCardWithMaxAttack, and the 2026-09-17 stage/grave split
/// (stage cannot reach cards below the Start Card; revive is the only grave-side channel).
/// FINAL_ESCORT's former flag-gated round-end stage tests were replaced by the
/// grave-exclusion guards — the card moved to the ReviveEffect round-end arm pattern.
/// </summary>
public class RoundEndAndBurialCountersTests : HeadlessCombatTestFixture
{
	[Test]
	public void MyCardBuriesEnemyCreature_DoesNotCountForEitherCauser()
	{
		var buryCard = CreateCard(true, "Burier");
		var enemyCreature = CreateCard(false, "EnemyCreature");
		enemyCreature.GetComponent<CardScript>().cardType = EnumStorage.CardType.Creature;
		CombatManager.combinedDeckZone.Add(buryCard);
		CombatManager.combinedDeckZone.Add(enemyCreature);

		var bury = CreateEffect<BuryEffect>(buryCard);
		EffectChainManager.MakeANewEffectRecorder(buryCard, bury.gameObject);
		bury.BuryTheirCards(1);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(0, ValueTrackerManager.creaturesBuriedByOwnerThisRoundRef.value, "enemy card buried by me is not friendly: no longer feeds the causer counter (2026-09-14 redesign)");
		Assert.AreEqual(0, ValueTrackerManager.creaturesBuriedByEnemyThisRoundRef.value, "enemy causer counter untouched");
		Assert.AreEqual(1, ValueTrackerManager.enemyCardsBuriedCountRef.value, "legacy victim-side counter still tracks the victim faction");
	}

	[Test]
	public void EnemyCardBuriesMyCreature_DoesNotCountForEitherCauser()
	{
		var enemyBurier = CreateCard(false, "EnemyBurier");
		var myCreature = CreateCard(true, "MyCreature");
		myCreature.GetComponent<CardScript>().cardType = EnumStorage.CardType.Creature;
		CombatManager.combinedDeckZone.Add(enemyBurier);
		CombatManager.combinedDeckZone.Add(myCreature);

		var bury = CreateEffect<BuryEffect>(enemyBurier);
		EffectChainManager.MakeANewEffectRecorder(enemyBurier, bury.gameObject);
		bury.BuryTheirCards(1);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(0, ValueTrackerManager.creaturesBuriedByEnemyThisRoundRef.value, "my card buried by the enemy is not enemy-friendly: no longer feeds the enemy causer counter (2026-09-14 redesign)");
		Assert.AreEqual(0, ValueTrackerManager.creaturesBuriedByOwnerThisRoundRef.value, "owner-caused counter untouched");
	}

	[Test]
	public void EnemySacrificesOwnCreature_CountsForEnemyCauser()
	{
		var enemyBurier = CreateCard(false, "EnemyBurier");
		var enemyCreature = CreateCard(false, "EnemyCreature");
		enemyCreature.GetComponent<CardScript>().cardType = EnumStorage.CardType.Creature;
		CombatManager.combinedDeckZone.Add(enemyBurier);
		CombatManager.combinedDeckZone.Add(enemyCreature);

		var bury = CreateEffect<BuryEffect>(enemyBurier);
		EffectChainManager.MakeANewEffectRecorder(enemyBurier, bury.gameObject);
		bury.BuryMyCards(1);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(1, ValueTrackerManager.creaturesBuriedByEnemyThisRoundRef.value, "enemy's own sacrificed creature counts for the enemy causer");
		Assert.AreEqual(0, ValueTrackerManager.creaturesBuriedByOwnerThisRoundRef.value, "owner-caused counter untouched");
	}

	[Test]
	public void MySacrificeOfMyOwnCreature_CountsForMe()
	{
		var buryCard = CreateCard(true, "Sacrificer");
		var myCreature = CreateCard(true, "MyCreature");
		myCreature.GetComponent<CardScript>().cardType = EnumStorage.CardType.Creature;
		CombatManager.combinedDeckZone.Add(buryCard);
		CombatManager.combinedDeckZone.Add(myCreature);

		var bury = CreateEffect<BuryEffect>(buryCard);
		EffectChainManager.MakeANewEffectRecorder(buryCard, bury.gameObject);
		bury.BuryMyCards(1);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(1, ValueTrackerManager.creaturesBuriedByOwnerThisRoundRef.value, "my own sacrificed creatures count for me (D2 ruling)");
	}

	[Test]
	public void MySacrificeOfMyOwnNonCreature_CountsForMe()
	{
		var buryCard = CreateCard(true, "Burier");
		var myCurseToken = CreateCard(true, "MyCurseToken");
		myCurseToken.GetComponent<CardScript>().cardType = EnumStorage.CardType.Token;
		CombatManager.combinedDeckZone.Add(buryCard);
		CombatManager.combinedDeckZone.Add(myCurseToken);

		var bury = CreateEffect<BuryEffect>(buryCard);
		EffectChainManager.MakeANewEffectRecorder(buryCard, bury.gameObject);
		bury.BuryMyCards(1);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(1, ValueTrackerManager.creaturesBuriedByOwnerThisRoundRef.value, "friendly non-creature burials count too (2026-09-14 redesign: all friendly card types)");
		Assert.AreEqual(0, ValueTrackerManager.creaturesBuriedByEnemyThisRoundRef.value);
		Assert.AreEqual(1, ValueTrackerManager.ownerCardsBuriedCountRef.value, "legacy victim-side counter tracks the victim faction");
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
		creature.GetComponent<CardScript>().cardType = EnumStorage.CardType.Creature;
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
	public void StageChosenCards_GraveSideCardsOutOfReach()
	{
		// deck order: [graveCreature, startCard, liveCreature, topFiller] — the grave-side
		// creature (index 0, below the Start Card) has the highest attack but is revive-only
		// territory (2026-09-17 stage/grave split).
		var graveCreature = CreateCard(true, "GraveCreature");
		graveCreature.GetComponent<CardScript>().cardType = EnumStorage.CardType.Creature;
		graveCreature.GetComponent<CardScript>().printedAttack = 9;
		CombatManager.combinedDeckZone.Add(graveCreature);
		CombatManager.combinedDeckZone.Add(CreateStartCard());
		var liveCreature = CreateCard(true, "LiveCreature");
		liveCreature.GetComponent<CardScript>().cardType = EnumStorage.CardType.Creature;
		liveCreature.GetComponent<CardScript>().printedAttack = 5;
		CombatManager.combinedDeckZone.Add(liveCreature);
		var filler = CreateCard(true, "TopFiller");
		CombatManager.combinedDeckZone.Add(filler);

		var stager = CreateCard(true, "Stager");
		var stage = CreateEffect<StageEffect>(stager);
		stage.targetFriendly = true;
		stage.creatureOnly = true;

		EffectChainManager.MakeANewEffectRecorder(stager, stage.gameObject);
		stage.StageCardWithMaxAttack();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(liveCreature, CombatManager.combinedDeckZone[CombatManager.combinedDeckZone.Count - 1],
			"stage cannot reach grave-side cards: the live creature is staged even though the grave one has higher attack");
	}

	[Test]
	public void StageSelf_FromGraveSide_IsNoOp()
	{
		var buriedCard = CreateCard(true, "BuriedSelf");
		CombatManager.combinedDeckZone.Add(buriedCard); // index 0, below the Start Card
		CombatManager.combinedDeckZone.Add(CreateStartCard());

		var stage = CreateEffect<StageEffect>(buriedCard);

		EffectChainManager.MakeANewEffectRecorder(buriedCard, stage.gameObject);
		stage.StageSelf();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(buriedCard, CombatManager.combinedDeckZone[0],
			"StageSelf from the grave side is a no-op: revive is the only grave-side channel");
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
