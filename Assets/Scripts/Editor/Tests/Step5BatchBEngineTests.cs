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
	public void BuryMyCards_CountBasedOnAnyBuried_ReducesWithTotalRoundBurials()
	{
		var decimation = CreateCard(true, "Decimation");
		AddCard(true, "F1", "A", 1, true);
		AddCard(true, "F2", "B", 1, true);
		AddCard(true, "F3", "C", 1, true);
		AddCard(true, "F4", "D", 1, true);
		CombatManager.combinedDeckZone.Add(CreateCard(false, "EnemyTopFiller")); // enemy: never in the friendly bury pool
		ValueTrackerManager.ownerCardsBuriedCountRef.value = 2; // my cards buried
		ValueTrackerManager.enemyCardsBuriedCountRef.value = 1; // enemy cards buried (any burier)

		var bury = CreateEffect<BuryEffect>(decimation);
		EffectChainManager.MakeANewEffectRecorder(decimation, bury.gameObject);
		bury.BuryMyCards_CountBasedOnAnyBuried(6);
		EffectChainManager.Me.CloseOpenedChain();
		// 6 - (2 + 1) = 3 friendly cards buried this call; they land in deck slots 0-2
		int buriedBottom = CombatManager.combinedDeckZone.Take(3)
			.Count(c => c.GetComponent<CardScript>().myStatusRef == OwnerStatus);
		Assert.AreEqual(3, buriedBottom, "6 minus 3 total round burials (incl. enemy cards) = 3 buried");
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

	[Test]
	public void TriggerAllGraveyardFriendlyDeathrattles_TargetsGraveyardFriendlyOnly()
	{
		var lastRites = CreateCard(true, "LastRites");
		var startCard = CreateCard(true, "StartCard");
		startCard.GetComponent<CardScript>().isStartCard = true;
		// grave side (below start card): two friendly with deathrattle listeners + one enemy
		var graveA = CreateCard(true, "GraveA", "A");
		var graveB = CreateCard(true, "GraveB", "B");
		var graveEnemy = CreateCard(false, "GraveEnemy", "E");
		CombatManager.combinedDeckZone.Add(graveA);
		CombatManager.combinedDeckZone.Add(graveB);
		CombatManager.combinedDeckZone.Add(graveEnemy);
		CombatManager.combinedDeckZone.Add(startCard);
		// living zone: friendly with deathrattle listener + last Rites itself in living zone
		var liveFriendly = CreateCard(true, "LiveFriendly", "L");
		CombatManager.combinedDeckZone.Add(liveFriendly);
		CombatManager.combinedDeckZone.Add(lastRites);

		int aFired = 0, bFired = 0, eFired = 0, lFired = 0;
		RegisterOnMeBuried(graveA, () => aFired++);
		RegisterOnMeBuried(graveB, () => bFired++);
		RegisterOnMeBuried(graveEnemy, () => eFired++);
		RegisterOnMeBuried(liveFriendly, () => lFired++);

		var trigger = CreateEffect<DeathrattleTriggerEffect>(lastRites);
		EffectChainManager.MakeANewEffectRecorder(lastRites, trigger.gameObject);
		trigger.TriggerAllGraveyardFriendlyDeathrattles();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(1, aFired, "graveyard friendly A triggered");
		Assert.AreEqual(1, bFired, "graveyard friendly B triggered");
		Assert.AreEqual(0, eFired, "graveyard enemy not triggered");
		Assert.AreEqual(0, lFired, "living-zone friendly not triggered");
	}

	[Test]
	public void TwoLastRites_InGrave_FireOnceEach_NoInfiniteLoop()
	{
		var startCard = CreateCard(true, "StartCard");
		startCard.GetComponent<CardScript>().isStartCard = true;
		// two LAST_RITES machines both in the grave — each triggers the other on deathrattle
		var ritesA = CreateCard(true, "RitesA", "RA");
		var ritesB = CreateCard(true, "RitesB", "RB");
		CombatManager.combinedDeckZone.Add(ritesA);
		CombatManager.combinedDeckZone.Add(ritesB);
		CombatManager.combinedDeckZone.Add(startCard);

		var triggerA = CreateEffect<DeathrattleTriggerEffect>(ritesA);
		var triggerB = CreateEffect<DeathrattleTriggerEffect>(ritesB);

		// Wire like the real LAST_RITES prefab: onMeBuried listener -> container ->
		// InvokeEffectEventVoid (only THIS path passes the effect-chain loop guard).
		int aFired = 0, bFired = 0;
		var containerA = ritesA.AddComponent<CostNEffectContainer>();
		containerA.effectEvent = new UnityEngine.Events.UnityEvent();
		containerA.checkCostEvent = new UnityEngine.Events.UnityEvent();
		// EditMode: OnEnable never runs, so _myCardScript (set via GetComponentInParent) must be injected.
		var cardField = typeof(CostNEffectContainer).GetField("_myCardScript", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		cardField.SetValue(containerA, ritesA.GetComponent<CardScript>());
		containerA.effectEvent.AddListener(() => { aFired++; triggerA.TriggerAllGraveyardFriendlyDeathrattles(); });
		var containerB = ritesB.AddComponent<CostNEffectContainer>();
		containerB.effectEvent = new UnityEngine.Events.UnityEvent();
		containerB.checkCostEvent = new UnityEngine.Events.UnityEvent();
		cardField.SetValue(containerB, ritesB.GetComponent<CardScript>());
		containerB.effectEvent.AddListener(() => { bFired++; triggerB.TriggerAllGraveyardFriendlyDeathrattles(); });
		var listenerA = RegisterOnMeBuried(ritesA, () => containerA.InvokeEffectEventVoid());
		var listenerB = RegisterOnMeBuried(ritesB, () => containerB.InvokeEffectEventVoid());

		// Simulate the real trigger: BuryEffect raises onMeBuried on A, A's listener runs
		// the container (the ONLY path that passes the effect-chain loop guard).
		GameEventStorage.onMeBuried.RaiseSpecific(ritesA);

		Assert.AreEqual(1, aFired, "A fires once (its own re-raise is blocked by the loop guard)");
		Assert.AreEqual(1, bFired, "B fires once (chain terminates, no overflow)");
	}

	[Test]
	public void BuriedCreatureAttackEffect_FriendlyBuriedCreatureStrikesWithItsAttack()
	{
		var grant = CreateCard(true, "DeathbedGrant");
		AddCard(false, "EnemyBottom", "Z", 0, true); // index 0 shield: buryable pool excludes the bottom slot
		var victim = AddCard(true, "BigCreature", "A", 3, true);
		CreateEffect<AttackEffect>(victim); // victim's own attack container (attack attribute settlement)

		var bury = CreateEffect<BuryEffect>(grant);
		EffectChainManager.MakeANewEffectRecorder(grant, bury.gameObject);
		bury.BuryMyCards(1);
		EffectChainManager.Me.CloseOpenedChain();
		Assert.AreEqual(victim.GetComponent<CardScript>(), CombatManager.lastCardBuried, "lastCardBuried context set by BuryEffect");

		// The passive reaction: buried creature strikes with its own attack.
		var reaction = CreateEffect<BuriedCreatureAttackEffect>(grant);
		EffectChainManager.MakeANewEffectRecorder(grant, reaction.gameObject);
		reaction.AttackLastBuriedFriendlyCreature();
		EffectChainManager.Me.CloseOpenedChain();
		Assert.AreEqual(97, EnemyStatus.hp, "buried creature strikes with its own attack (3)");
	}

	[Test]
	public void BuriedCreatureAttackEffect_SkipsNonCreatureAndEnemyVictims()
	{
		var grant = CreateCard(true, "DeathbedGrant");
		var effect = CreateEffect<BuriedCreatureAttackEffect>(grant);

		// non-creature friendly victim: no attack
		var nonCreature = AddCard(true, "CurseCard", "J", 5, false);
		CombatManager.lastCardBuried = nonCreature.GetComponent<CardScript>();
		EffectChainManager.MakeANewEffectRecorder(grant, effect.gameObject);
		effect.AttackLastBuriedFriendlyCreature();
		EffectChainManager.Me.CloseOpenedChain();
		Assert.AreEqual(100, EnemyStatus.hp, "non-creature buried card does not strike");

		// enemy creature victim: out of faction
		var enemyCreature = AddCard(false, "EnemyCreature", "E", 9, true);
		CombatManager.lastCardBuried = enemyCreature.GetComponent<CardScript>();
		EffectChainManager.MakeANewEffectRecorder(grant, effect.gameObject);
		effect.AttackLastBuriedFriendlyCreature();
		EffectChainManager.Me.CloseOpenedChain();
		Assert.AreEqual(100, EnemyStatus.hp, "enemy buried card never strikes for my side");
	}

	[Test]
	public void GravePuppeteer_StrikesWithGraveyardCreature_AndLeavesItInGrave()
	{
		var puppeteer = CreateCard(true, "Puppeteer");
		var startCard = CreateCard(true, "StartCard");
		startCard.GetComponent<CardScript>().isStartCard = true;
		var graveCreature = AddCard(true, "GraveBody", "A", 4, true);
		CreateEffect<AttackEffect>(graveCreature);
		CombatManager.combinedDeckZone.Add(startCard);
		int graveIndex = CombatManager.combinedDeckZone.IndexOf(graveCreature);

		var effect = CreateEffect<GravePuppeteerEffect>(puppeteer);
		EffectChainManager.MakeANewEffectRecorder(puppeteer, effect.gameObject);
		effect.RaiseGraveCreatureOrBuryFallback();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(96, EnemyStatus.hp, "graveyard creature strikes with its own attack (4)");
		Assert.AreEqual(graveIndex, CombatManager.combinedDeckZone.IndexOf(graveCreature),
			"the creature stays in the grave (slot unchanged)");
		Assert.Less(CombatManager.combinedDeckZone.IndexOf(graveCreature),
			CombatManager.combinedDeckZone.IndexOf(startCard), "still below the start card");
	}

	[Test]
	public void GravePuppeteer_NoGraveyardCreature_BuriesFriendlyDeathrattleCard()
	{
		var puppeteer = CreateCard(true, "Puppeteer");
		var startCard = CreateCard(true, "StartCard");
		startCard.GetComponent<CardScript>().isStartCard = true;
		var nonCreature = AddCard(true, "CurseOnly", "J", 0, false); // grave: no creature, so no attack
		CombatManager.combinedDeckZone.Add(startCard);
		var deathrattleCard = AddCard(true, "DeathrattleGuy", "D", 0, false); // living zone, carries the 遗言 tag
		deathrattleCard.GetComponent<CardScript>().myTags.Add(EnumStorage.Tag.DeathRattle);

		var effect = CreateEffect<GravePuppeteerEffect>(puppeteer);
		var fallback = CreateEffect<BuryEffect>(puppeteer);
		fallback.tagsToCheck = new System.Collections.Generic.List<EnumStorage.Tag> { EnumStorage.Tag.DeathRattle };
		effect.fallbackBurier = fallback;
		EffectChainManager.MakeANewEffectRecorder(puppeteer, effect.gameObject);
		effect.RaiseGraveCreatureOrBuryFallback();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(100, EnemyStatus.hp, "no creature in grave -> no attack");
		Assert.AreEqual(deathrattleCard, CombatManager.combinedDeckZone[0],
			"fallback buries the friendly deathrattle card to the bottom");
	}

	[Test]
	public void WeaponSpirit_AmplifiesLastGainedFriendlyCreatureOnce()
	{
		var ws = CreateCard(true, "WeaponSpirit");
		var creature = AddCard(true, "Enhanced", "A", 1, true);
		var giver = CreateEffect<DefaultNamespace.Effects.AttackGiverEffect>(ws);
		CombatManager.lastCardGainedAttack = creature.GetComponent<CardScript>();

		EffectChainManager.MakeANewEffectRecorder(ws, giver.gameObject);
		giver.GiveAttackToLastGainedAttack(1);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(1, creature.GetComponent<CardScript>().attackGrowth, "enhanced creature gets amplified by 1");
		// non-creature / enemy targets are refused
		var nonCreature = AddCard(true, "CurseCard", "J", 1, false);
		CombatManager.lastCardGainedAttack = nonCreature.GetComponent<CardScript>();
		EffectChainManager.MakeANewEffectRecorder(ws, giver.gameObject);
		giver.GiveAttackToLastGainedAttack(1);
		EffectChainManager.Me.CloseOpenedChain();
		Assert.AreEqual(0, nonCreature.GetComponent<CardScript>().attackGrowth, "non-creature not amplified");
	}

	[Test]
	public void DeathKnell_TriggersDeathrattleOfLastRevivedFriendly()
	{
		var knell = CreateCard(true, "DeathKnell");
		var revived = CreateCard(true, "Revived", "A");
		CombatManager.lastCardRevived = revived.GetComponent<CardScript>();
		int fired = 0;
		RegisterOnMeBuried(revived, () => fired++);

		var trigger = CreateEffect<DeathrattleTriggerEffect>(knell);
		EffectChainManager.MakeANewEffectRecorder(knell, trigger.gameObject);
		trigger.TriggerDeathrattleOfLastRevivedFriendly();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(1, fired, "last revived friendly's deathrattle fires");
	}

	[Test]
	public void GraveCurse_EnemyCurseReportsGraveyardFriendlyCount_CoveringGrowth()
	{
		GameEventStorage.curseCardTypeID.value = "JU_ON";
		var startCard = CreateCard(true, "StartCard");
		startCard.GetComponent<CardScript>().isStartCard = true;
		AddCard(true, "G1", "A", 0, false);
		AddCard(true, "G2", "B", 0, false);
		CombatManager.combinedDeckZone.Add(startCard);
		var enemyCurse = CreateCard(false, "Curse", "JU_ON");
		enemyCurse.GetComponent<CardScript>().attackGrowth = 5; // prior enhancements
		CombatManager.combinedDeckZone.Add(enemyCurse);

		ValueTrackerManager.curseAttackOverrideOwnerThisRoundRef.value = 1;
		Assert.AreEqual(2, enemyCurse.GetComponent<CardScript>().GetAttack(),
			"coverage: 2 graveyard friendly cards overlap any grown attack");
		// not armed -> base + growth shows again
		ValueTrackerManager.curseAttackOverrideOwnerThisRoundRef.value = 0;
		Assert.AreEqual(5 + 0, enemyCurse.GetComponent<CardScript>().GetAttack(), "flag off: growth visible again");
	}

	[Test]
	public void GraveRobber_SnapshotsStrongestEnemyAttack()
	{
		var robber = CreateCard(true, "GraveRobber");
		var startCard = CreateCard(true, "StartCard");
		startCard.GetComponent<CardScript>().isStartCard = true;
		var enemyA = CreateCard(false, "BigEnemy", "A");
		enemyA.GetComponent<CardScript>().printedAttack = 7;
		enemyA.GetComponent<CardScript>().isCreature = true;
		var enemyB = CreateCard(false, "SmallEnemy", "B");
		enemyB.GetComponent<CardScript>().printedAttack = 3;
		enemyB.GetComponent<CardScript>().isCreature = true;
		CombatManager.combinedDeckZone.Add(enemyA);
		CombatManager.combinedDeckZone.Add(enemyB);
		CombatManager.combinedDeckZone.Add(startCard);

		var rob = CreateEffect<GraveRobberEffect>(robber);
		var revive = CreateEffect<ReviveEffect>(robber);
		revive.sortBy = ReviveEffect.ReviveSortBy.MaxAttack;
		rob.reviveEngine = revive;

		EffectChainManager.MakeANewEffectRecorder(robber, rob.gameObject);
		rob.ReviveStrongestEnemyAndSnapshot();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(7, robber.GetComponent<CardScript>().GetAttack(), "snapshot of the strongest revived enemy (7)");
		enemyA.GetComponent<CardScript>().ModifyAttack(10);
		Assert.AreEqual(7, robber.GetComponent<CardScript>().GetAttack(),
			"snapshot: later growth on the resurrected enemy does not ripple back");
	}

	[Test]
	public void MassSacrifice_BuriesAllFriendlyAndSpawnsEqualBelievers()
	{
		var sacrificeCard = CreateCard(true, "MassSacrifice");
		var startCard = CreateCard(true, "StartCard");
		startCard.GetComponent<CardScript>().isStartCard = true;
		CombatManager.combinedDeckZone.Add(startCard);
		AddCard(true, "F1", "A", 1, true);
		AddCard(true, "F2", "B", 1, true);
		AddCard(false, "Enemy", "E", 1, true); // enemy must not be buried

		var effect = CreateEffect<MassSacrificeEffect>(sacrificeCard);
		effect.buryEngine = CreateEffect<BuryEffect>(sacrificeCard);
		effect.addEngine = CreateEffect<DefaultNamespace.Effects.AddTempCard>(sacrificeCard);
		effect.riftToken = CreateCard(true, "RiftSpy", "RIFT"); // instance stands in for the token prefab

		int riftsBefore = CombatManager.combinedDeckZone.Count(c => c.GetComponent<CardScript>().cardTypeID == "RIFT");
		EffectChainManager.MakeANewEffectRecorder(sacrificeCard, effect.gameObject);
		effect.SacrificeAllThenSpawnBelievers();
		EffectChainManager.Me.CloseOpenedChain();

		int riftsAfter = CombatManager.combinedDeckZone.Count(c => c.GetComponent<CardScript>().cardTypeID == "RIFT");
		Assert.AreEqual(riftsBefore + 2, riftsAfter, "2 friendly buried -> 2 believers spawned");
		int startIndex = CombatManager.combinedDeckZone.IndexOf(startCard);
		Assert.Less(CombatManager.combinedDeckZone.IndexOf(CombatManager.combinedDeckZone.Find(c => c.GetComponent<CardScript>().cardTypeID == "A")), startIndex,
			"friend A buried (graveyard side)");
		Assert.Less(CombatManager.combinedDeckZone.IndexOf(CombatManager.combinedDeckZone.Find(c => c.GetComponent<CardScript>().cardTypeID == "B")), startIndex,
			"friend B buried (graveyard side)");
		Assert.Greater(CombatManager.combinedDeckZone.IndexOf(CombatManager.combinedDeckZone.Find(c => c.GetComponent<CardScript>().cardTypeID == "E")), startIndex,
			"enemy card untouched in the living zone");
	}

	[Test]
	public void BloodPact_ConvertsEnemyDamage_KeepsAttackEventAndSelfDamage()
	{
		GameEventStorage.curseCardTypeID.value = "JU_ON";
		var attacker = CreateCard(true, "Attacker");
		attacker.GetComponent<CardScript>().printedAttack = 3;
		attacker.GetComponent<CardScript>().isCreature = true;
		var juOn = CreateCard(false, "Curse", "JU_ON");
		CombatManager.combinedDeckZone.Add(attacker);
		CombatManager.combinedDeckZone.Add(juOn);

		var atk = CreateEffect<AttackEffect>(attacker);
		var curse = CreateEffect<DefaultNamespace.Effects.CurseEffect>(attacker);
		curse.cardTypeID = GameEventStorage.curseCardTypeID;
		atk.curseEngine = curse;

		int attackEvents = 0;
		RegisterEventCallback(GameEventStorage.onAnyCardAttacked, () => attackEvents++);

		ValueTrackerManager.bloodPactOwnerThisRoundRef.value = 1;
		EffectChainManager.MakeANewEffectRecorder(attacker, atk.gameObject);
		atk.Attack();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(100, EnemyStatus.hp, "no HP loss under blood pact");
		Assert.AreEqual(3, juOn.GetComponent<CardScript>().attackGrowth, "3 attack -> 3 curse enhancement");
		Assert.AreEqual(1, attackEvents, "attack action still raises its event");

		// unarmed: normal damage returns
		ValueTrackerManager.bloodPactOwnerThisRoundRef.value = 0;
		EffectChainManager.MakeANewEffectRecorder(attacker, atk.gameObject);
		atk.Attack();
		EffectChainManager.Me.CloseOpenedChain();
		Assert.AreEqual(97, EnemyStatus.hp, "without the pact, damage applies");

		// self-damage (AttackSelf) is never converted
		ValueTrackerManager.bloodPactOwnerThisRoundRef.value = 1;
		EffectChainManager.MakeANewEffectRecorder(attacker, atk.gameObject);
		atk.AttackSelf();
		EffectChainManager.Me.CloseOpenedChain();
		Assert.AreEqual(97, OwnerStatus.hp, "self-damage unaffected by the pact (3 dmg on own player)");
	}

	private DefaultNamespace.GameEventListener RegisterOnMeBuried(GameObject target, System.Action callback)
	{
		var listener = target.AddComponent<DefaultNamespace.GameEventListener>();
		listener.@event = GameEventStorage.onMeBuried;
		// UnityAction has its own delegate type; wrap the System.Action via lambda.
		listener.response.AddListener(() => callback());
		GameEventStorage.onMeBuried.RegisterListener(listener);
		return listener;
	}
}
