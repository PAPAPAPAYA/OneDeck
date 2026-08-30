using System.Linq;
using DefaultNamespace;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode tests for 4.0 step-5 batch B engine gaps: bury predicate/attack pickers,
/// per-attack loops (curse/believer/token), non-creature filters, onlyEnhanced revive,
/// grave-creature aura, revived-count resolver term, self-exile counter and the
/// WEAKENING_FIELD this-round modifier.
/// </summary>
public class Step5BatchBEngineTests : HeadlessCombatTestFixture
{
	private GameObject AddCard(bool isOwner, string name, string cardTypeID, int printedAttack, bool isCreature)
	{
		var card = CreateCard(isOwner, name, cardTypeID);
		var cs = card.GetComponent<CardScript>();
		cs.printedAttack = printedAttack;
		cs.isCreature = isCreature;
		CombatManager.combinedDeckZone.Add(card);
		return card;
	}

	[Test]
	public void BuryCardWithMaxAttack_BuriesHighestAttackEnemy()
	{
		var stager = CreateCard(true, "Kingslayer");
		AddCard(false, "E1", "A", 3, true);
		AddCard(false, "E2", "B", 7, true);
		AddCard(false, "E3", "C", 2, true);
		AddCard(true, "F1", "D", 9, true);
		CombatManager.combinedDeckZone.Add(CreateCard(true, "TopFiller"));

		var bury = CreateEffect<BuryEffect>(stager);
		bury.targetFriendly = false;
		EffectChainManager.MakeANewEffectRecorder(stager, bury.gameObject);
		bury.BuryCardWithMaxAttack();
		EffectChainManager.Me.CloseOpenedChain();
		var result = CombatManager.combinedDeckZone;
		Assert.IsTrue(result[0].GetComponent<CardScript>().cardTypeID == "B", "highest-attack enemy buried to the bottom");
	}

	[Test]
	public void BuryCardWithMinAttack_BuriesLowestPositiveAttackFriendly()
	{
		var stager = CreateCard(true, "Sacrificer");
		AddCard(true, "BottomFiller", "Z", 0, true); // index 0 shield (never buryable by IsCardAtBottom)
		AddCard(true, "F1", "A", 2, true);
		AddCard(true, "F2", "B", 0, true);
		AddCard(true, "F3", "C", 5, true);
		CombatManager.combinedDeckZone.Add(CreateCard(true, "TopFiller"));

		var bury = CreateEffect<BuryEffect>(stager);
		bury.targetFriendly = true;
		EffectChainManager.MakeANewEffectRecorder(stager, bury.gameObject);
		bury.BuryCardWithMinAttack();
		EffectChainManager.Me.CloseOpenedChain();
		Assert.IsTrue(CombatManager.combinedDeckZone[0].GetComponent<CardScript>().cardTypeID == "A",
			"lowest POSITIVE-attack friendly card (attack 2, F1) is buried");
	}

	[Test]
	public void BuryNextXCards_BasedOnAttack_HasAttackBuryCount()
	{
		var mill = CreateCard(true, "Millblade");
		mill.GetComponent<CardScript>().printedAttack = 3;
		AddCard(true, "A", "A", 0, false);
		AddCard(true, "B", "B", 0, false);
		AddCard(true, "C", "C", 0, false);
		AddCard(true, "D", "D", 0, false);
		CombatManager.combinedDeckZone.Add(mill); // source card at the deck top; startIndex = millIndex - 1

		var bury = CreateEffect<BuryEffect>(mill);
		EffectChainManager.MakeANewEffectRecorder(mill, bury.gameObject);
		bury.BuryNextXCards_BasedOnAttack();
		EffectChainManager.Me.CloseOpenedChain();
		var order = CombatManager.combinedDeckZone.Take(3).Select(c => c.GetComponent<CardScript>().cardTypeID).ToArray();
		Assert.AreEqual(new[] { "B", "C", "D" }, order, "top 3 cards (D,C,B) buried into slots 0-2");
	}

	[Test]
	public void BuryMyCards_CountBasedOnBuried_ReducesWithRoundBurials()
	{
		var decimation = CreateCard(true, "Decimation");
		AddCard(true, "F1", "A", 1, true);
		AddCard(true, "F2", "B", 1, true);
		AddCard(true, "F3", "C", 1, true);
		AddCard(true, "F4", "D", 1, true);
		CombatManager.combinedDeckZone.Add(CreateCard(true, "TopFiller"));
		ValueTrackerManager.ownerCardsBuriedCountRef.value = 2;

		var bury = CreateEffect<BuryEffect>(decimation);
		EffectChainManager.MakeANewEffectRecorder(decimation, bury.gameObject);
		bury.BuryMyCards_CountBasedOnBuried(6);
		EffectChainManager.Me.CloseOpenedChain();
		// 6 - 2 = 4 buried; deck has exactly 4 eligible friendly cards
		var order = CombatManager.combinedDeckZone.Take(4).Select(c => c.GetComponent<CardScript>().cardTypeID).ToArray();
		Assert.AreEqual(4, order.Distinct().Count(), "4 friendly cards buried this round (6 minus 2 already buried)");
	}

	[Test]
	public void BuryTheirCards_NonCreatureFilter_SkipsCreatures()
	{
		var burier = CreateCard(true, "Guide");
		AddCard(false, "EnemyCreature", "A", 1, true);
		AddCard(false, "EnemyCurse", "B", 1, false);
		CombatManager.combinedDeckZone.Add(CreateCard(true, "TopFiller"));

		var bury = CreateEffect<BuryEffect>(burier);
		bury.creatureFilter = EffectScript.EffectCreatureFilter.NonCreature;
		EffectChainManager.MakeANewEffectRecorder(burier, bury.gameObject);
		bury.BuryTheirCards(2);
		EffectChainManager.Me.CloseOpenedChain();
		Assert.IsTrue(CombatManager.combinedDeckZone[0].GetComponent<CardScript>().cardTypeID == "B",
			"only the non-creature enemy card is eligible for burial");
	}

	[Test]
	public void StageMyCards_NonCreatureFilter_SkipsCreatures()
	{
		var porter = CreateCard(true, "Porter");
		AddCard(true, "Creature", "A", 1, true);
		AddCard(true, "NonCreature", "B", 1, false);
		CombatManager.combinedDeckZone.Add(CreateCard(true, "TopFiller"));

		var stage = CreateEffect<StageEffect>(porter);
		stage.creatureFilter = EffectScript.EffectCreatureFilter.NonCreature;
		EffectChainManager.MakeANewEffectRecorder(porter, stage.gameObject);
		stage.StageMyCards(1);
		EffectChainManager.Me.CloseOpenedChain();
		Assert.AreEqual("B", CombatManager.combinedDeckZone[CombatManager.combinedDeckZone.Count - 1].GetComponent<CardScript>().cardTypeID,
			"non-creature staged to the deck top");
	}

	[Test]
	public void ReviveEffect_OnlyEnhanced_CreatureOnly()
	{
		var reviver = CreateCard(true, "EliteReviver");
		var startCard = CreateCard(true, "StartCard");
		startCard.GetComponent<CardScript>().isStartCard = true;
		var enhanced = AddCard(true, "Enhanced", "A", 1, true);
		enhanced.GetComponent<CardScript>().attackGrowth = 2;
		var plain = AddCard(true, "Plain", "B", 1, true);
		CombatManager.combinedDeckZone.Add(startCard);

		var revive = CreateEffect<ReviveEffect>(reviver);
		revive.creatureFilter = ReviveEffect.CreatureFilter.Creature;
		revive.onlyEnhanced = true;
		EffectChainManager.MakeANewEffectRecorder(reviver, revive.gameObject);
		revive.ReviveMyCards(1);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual("A", CombatManager.combinedDeckZone[CombatManager.combinedDeckZone.Count - 1].GetComponent<CardScript>().cardTypeID,
			"only the enhanced friendly creature is eligible");
	}

	[Test]
	public void EnhanceCurseTimes_BasedOnAttack_OnePerAttackPoint()
	{
		GameEventStorage.curseCardTypeID.value = "JU_ON";
		var hexblade = CreateCard(true, "Hexblade");
		hexblade.GetComponent<CardScript>().printedAttack = 3;
		var juOn = CreateCard(false, "Curse", "JU_ON");
		CombatManager.combinedDeckZone.Add(hexblade);
		CombatManager.combinedDeckZone.Add(juOn);

		var curse = CreateEffect<DefaultNamespace.Effects.CurseEffect>(hexblade);
		curse.cardTypeID = GameEventStorage.curseCardTypeID;
		EffectChainManager.MakeANewEffectRecorder(hexblade, curse.gameObject);
		curse.EnhanceCurseTimes_BasedOnAttack();
		EffectChainManager.Me.CloseOpenedChain();
		Assert.AreEqual(3, juOn.GetComponent<CardScript>().attackGrowth, "3 attack -> 3 separate +1 enhancements");
	}

	[Test]
	public void EnhanceCurseTimes_BasedOnTypeIDCount_CountsFriendlyRifts()
	{
		GameEventStorage.curseCardTypeID.value = "JU_ON";
		var cursercat = CreateCard(true, "SwarmCurser");
		var riftType = UnityEngine.ScriptableObject.CreateInstance<DefaultNamespace.SOScripts.StringSO>();
		riftType.value = "RIFT";
		var juOn = CreateCard(false, "Curse", "JU_ON");
		CombatManager.combinedDeckZone.Add(cursercat);
		CombatManager.combinedDeckZone.Add(juOn);
		AddCard(true, "Rift1", "RIFT", 0, false);
		AddCard(true, "Rift2", "RIFT", 0, false);
		AddCard(false, "EnemyRift", "RIFT", 0, false);

		var curse = CreateEffect<DefaultNamespace.Effects.CurseEffect>(cursercat);
		curse.cardTypeID = GameEventStorage.curseCardTypeID;
		curse.countTypeID = riftType;
		EffectChainManager.MakeANewEffectRecorder(cursercat, curse.gameObject);
		curse.EnhanceCurseTimes_BasedOnTypeIDCount();
		EffectChainManager.Me.CloseOpenedChain();
		Assert.AreEqual(2, juOn.GetComponent<CardScript>().attackGrowth, "2 friendly believers only (enemy rift excluded)");
	}

	[Test]
	public void SelfExile_IncrementsCauserCounter()
	{
		var exiler = CreateCard(true, "RiftReaper");
		CombatManager.combinedDeckZone.Add(exiler);
		CombatManager.combinedDeckZone.Add(CreateCard(true, "Rift1", "RIFT"));
		CombatManager.combinedDeckZone.Add(CreateCard(true, "Rift2", "RIFT"));
		var riftType = UnityEngine.ScriptableObject.CreateInstance<DefaultNamespace.SOScripts.StringSO>();
		riftType.value = "RIFT";

		var exile = CreateEffect<ExileEffect>(exiler);
		exile.cardTypeIDSO = riftType;
		EffectChainManager.MakeANewEffectRecorder(exiler, exile.gameObject);
		exile.ExileMyCardsWithTypeID(99);
		EffectChainManager.Me.CloseOpenedChain();
		Assert.AreEqual(2, ValueTrackerManager.friendlyExiledByOwnerThisRoundRef.value,
			"self-exiling two friendly believers counts both for the causer side");
	}

	[Test]
	public void GraveCreatureAura_AddsToGraveyardCreatureAttackOnly()
	{
		var startCard = CreateCard(true, "StartCard");
		startCard.GetComponent<CardScript>().isStartCard = true;
		var graveCreature = AddCard(true, "GraveCreature", "A", 4, true);
		CombatManager.combinedDeckZone.Add(startCard);
		var liveCreature = AddCard(true, "LiveCreature", "B", 4, true); // above the start card = living zone

		ValueTrackerManager.graveCreatureAuraOwnerThisRoundRef.value = 1;
		Assert.AreEqual(5, graveCreature.GetComponent<CardScript>().GetAttack(), "graveyard creature +1");
		Assert.AreEqual(4, liveCreature.GetComponent<CardScript>().GetAttack(), "living creature unaffected");
	}

	[Test]
	public void RevivedFriendlyThisRoundCount_TermResolves()
	{
		var reanimator = CreateCard(true, "Reanimator");
		var resolver = reanimator.AddComponent<AttackResolverSource>();
		var term = new AttackResolverSource.Term { source = AttackResolverSource.Source.RevivedFriendlyThisRoundCount };
		resolver.terms.Add(term);
		resolver.RefreshAttackResolver();
		ValueTrackerManager.ownerRevivedCountThisRoundRef.value = 3;
		Assert.AreEqual(3, reanimator.GetComponent<CardScript>().GetAttack());
	}

	[Test]
	public void ModifyAllCreatureAttackThisRoundExceptCurse_SkipsCurseAndNonCreature()
	{
		GameEventStorage.curseCardTypeID.value = "JU_ON";
		var field = CreateCard(true, "WeakeningField");
		var creature = AddCard(true, "Creature", "A", 5, true);
		var curse = AddCard(true, "Curse", "JU_ON", 5, true);
		var nonCreature = AddCard(true, "NonCreature", "B", 5, false);
		CombatManager.combinedDeckZone.Add(field);

		var giver = CreateEffect<DefaultNamespace.Effects.AttackGiverEffect>(field);
		EffectChainManager.MakeANewEffectRecorder(field, giver.gameObject);
		giver.ModifyAllCreatureAttackThisRoundExceptCurse(-1);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(4, creature.GetComponent<CardScript>().GetAttack(), "creature -1 this round");
		Assert.AreEqual(5, curse.GetComponent<CardScript>().GetAttack(), "curse spared");
		Assert.AreEqual(5, nonCreature.GetComponent<CardScript>().GetAttack(), "non-creature spared");
		Assert.AreEqual(0, creature.GetComponent<CardScript>().attackGrowth, "this-round modifier, not permanent growth");
	}
}
