using DefaultNamespace;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode tests for the 4.0 attack-times engine (roadmap step 4, batch 1):
/// E1 this-round segments on CardScript, E7 faction creature attack-times aura,
/// E2 AttackTimesGiverEffect grants, and the onFriendlyCardExiled self-side-exile
/// semantics EXILE_BERSERKER binds to.
/// </summary>
public class AttackTimesGiverEffectTests : HeadlessCombatTestFixture
{
	[Test]
	public void GetAttackTimes_BaseIsOne()
	{
		var card = CreateCard(true, "Plain");
		Assert.AreEqual(1, card.GetComponent<CardScript>().GetAttackTimes());
	}

	[Test]
	public void GetAttackTimes_PermanentAndThisRoundStack_ResetClearsThisRoundOnly()
	{
		var card = CreateCard(true, "Combo");
		var cs = card.GetComponent<CardScript>();
		cs.extraAttackTimes = 1;
		cs.ModifyAttackTimesThisRound(2);
		Assert.AreEqual(4, cs.GetAttackTimes(), "1 + 1 permanent + 2 this-round");

		cs.ResetRoundAttackModifiers();
		Assert.AreEqual(2, cs.GetAttackTimes(), "permanent survives, this-round cleared");
	}

	[Test]
	public void ModifyAttackTimesThisRound_RejectsNegative()
	{
		var card = CreateCard(true, "Guarded");
		var cs = card.GetComponent<CardScript>();
		cs.ModifyAttackTimesThisRound(-1);
		Assert.AreEqual(1, cs.GetAttackTimes(), "segment count never drops below 1");
	}

	[Test]
	public void CreatureAura_CoversLaterCreatedCreatures_AndIgnoresNonCreatures()
	{
		var aura = ValueTrackerManager.creatureAttackTimesAuraOwnerThisRoundRef;
		var creature1 = CreateCard(true, "Creature1");
		creature1.GetComponent<CardScript>().isCreature = true;
		Assert.AreEqual(1, creature1.GetComponent<CardScript>().GetAttackTimes());

		aura.value = 1; // BATTLE_HORN bump
		Assert.AreEqual(2, creature1.GetComponent<CardScript>().GetAttackTimes());

		// Retroactive coverage: a creature created after the bump reads the same aura.
		var creature2 = CreateCard(true, "Creature2");
		creature2.GetComponent<CardScript>().isCreature = true;
		Assert.AreEqual(2, creature2.GetComponent<CardScript>().GetAttackTimes());

		// Non-creatures ignore the aura.
		var horn = CreateCard(true, "Horn");
		Assert.AreEqual(1, horn.GetComponent<CardScript>().GetAttackTimes());

		// Enemy creatures read the enemy aura only (still 0 here).
		var enemyCreature = CreateCard(false, "EnemyCreature");
		enemyCreature.GetComponent<CardScript>().isCreature = true;
		Assert.AreEqual(1, enemyCreature.GetComponent<CardScript>().GetAttackTimes());
	}

	[Test]
	public void BumpFriendlyCreatureAttackTimesAura_BumpsFactionAuraNotPerCard()
	{
		var creature = CreateCard(true, "Creature");
		creature.GetComponent<CardScript>().isCreature = true;
		var giver = CreateEffect<DefaultNamespace.Effects.AttackTimesGiverEffect>(creature);

		giver.BumpFriendlyCreatureAttackTimesAura(1);

		Assert.AreEqual(1, ValueTrackerManager.creatureAttackTimesAuraOwnerThisRoundRef.value, "aura incremented");
		Assert.AreEqual(0, creature.GetComponent<CardScript>().attackTimesModThisRound, "aura bump must not touch per-card state");
		Assert.AreEqual(2, creature.GetComponent<CardScript>().GetAttackTimes());
	}

	[Test]
	public void GiveSelfAttackTimes_GrantsThisRound_AndCapturesFlaggedAttackChange()
	{
		var card = CreateCard(true, "ComboStarter");
		card.GetComponent<CardScript>().isCreature = true;
		card.GetComponent<CardScript>().printedAttack = 2;
		var giver = CreateEffect<DefaultNamespace.Effects.AttackTimesGiverEffect>(card);

		EffectChainManager.MakeANewEffectRecorder(card, giver.gameObject);
		giver.GiveSelfAttackTimes(1);
		var recorder = EffectChainManager.currentEffectRecorder.GetComponent<EffectRecorder>();
		var attackChange = recorder.animationRequests.Find(r => r.type == AnimationRequestType.AttackChange);
		Assert.IsNotNull(attackChange, "grant should capture an AttackChange request");
		Assert.IsTrue(attackChange.attackTimesChange, "request must be flagged attackTimesChange so the attack print stays put");
		EffectChainManager.Me.CloseOpenedChain();

		var cs = card.GetComponent<CardScript>();
		Assert.AreEqual(1, cs.attackTimesModThisRound);
		Assert.AreEqual(0, cs.extraAttackTimes, "self grant is this-round, not permanent");
		Assert.AreEqual(2, cs.GetAttackTimes());
	}

	[Test]
	public void GiveRandomFriendlyCreatureAttackTimes_GrantsToExactlyOneCreature()
	{
		var granter = CreateCard(true, "Granter");
		granter.GetComponent<CardScript>().isCreature = true;
		var a = CreateCard(true, "A");
		a.GetComponent<CardScript>().isCreature = true;
		var b = CreateCard(true, "B");
		b.GetComponent<CardScript>().isCreature = true;
		CombatManager.combinedDeckZone.Add(a);
		CombatManager.combinedDeckZone.Add(b);

		var giver = CreateEffect<DefaultNamespace.Effects.AttackTimesGiverEffect>(granter);
		giver.GiveRandomFriendlyCreatureAttackTimes(1);

		int granted = (a.GetComponent<CardScript>().attackTimesModThisRound > 0 ? 1 : 0)
			+ (b.GetComponent<CardScript>().attackTimesModThisRound > 0 ? 1 : 0)
			+ (granter.GetComponent<CardScript>().attackTimesModThisRound > 0 ? 1 : 0);
		Assert.AreEqual(1, granted, "exactly one friendly creature receives the grant");
	}

	[Test]
	public void GiveRandomFriendlyCreatureAttackTimes_FizzlesOnEmptyPool()
	{
		var granter = CreateCard(true, "Granter"); // non-creature: the pool only holds creatures
		var giver = CreateEffect<DefaultNamespace.Effects.AttackTimesGiverEffect>(granter);

		Assert.DoesNotThrow(() => giver.GiveRandomFriendlyCreatureAttackTimes(1));
		Assert.AreEqual(0, granter.GetComponent<CardScript>().attackTimesModThisRound);
	}

	[Test]
	public void GiveRevealedCurseAttackTimes_GrantsPermanentToEnemyCurseInRevealZone()
	{
		GameEventStorage.curseCardTypeID.value = "JU_ON";
		var giverCard = CreateCard(true, "HasteRelic");
		var curse = CreateCard(false, "Curse", "JU_ON");
		CombatManager.revealZone = curse;

		var giver = CreateEffect<DefaultNamespace.Effects.AttackTimesGiverEffect>(giverCard);
		giver.GiveRevealedCurseAttackTimes(1);

		var curseCs = curse.GetComponent<CardScript>();
		Assert.AreEqual(1, curseCs.extraAttackTimes, "curse grant is permanent on the instance");
		Assert.AreEqual(0, curseCs.attackTimesModThisRound, "curse grant is not this-round");
	}

	[Test]
	public void GiveRevealedCurseAttackTimes_SkipsNonCurseAndFriendlyCards()
	{
		GameEventStorage.curseCardTypeID.value = "JU_ON";
		var giverCard = CreateCard(true, "HasteRelic");
		var giver = CreateEffect<DefaultNamespace.Effects.AttackTimesGiverEffect>(giverCard);

		var nonCurse = CreateCard(false, "NonCurse", "SOMETHING_ELSE");
		CombatManager.revealZone = nonCurse;
		giver.GiveRevealedCurseAttackTimes(1);
		Assert.AreEqual(0, nonCurse.GetComponent<CardScript>().extraAttackTimes, "non-curse reveal is skipped");

		var friendlyCurse = CreateCard(true, "FriendlyCurse", "JU_ON");
		CombatManager.revealZone = friendlyCurse;
		giver.GiveRevealedCurseAttackTimes(1);
		Assert.AreEqual(0, friendlyCurse.GetComponent<CardScript>().extraAttackTimes, "own-side curse is skipped");
	}

	[Test]
	public void SelfSideExile_FiresOnFriendlyCardExiled()
	{
		var exiler = CreateCard(true, "Exiler");
		CombatManager.combinedDeckZone.Add(exiler);

		int fired = 0;
		RegisterEventCallback(GameEventStorage.onFriendlyCardExiled, () => fired++);

		var exile = CreateEffect<ExileEffect>(exiler);
		EffectChainManager.MakeANewEffectRecorder(exiler, exile.gameObject);
		exile.ExileSelf();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(1, fired, "self-side exile fires the event for that side");
	}

	[Test]
	public void EnemyExilingMyCard_DoesNotFireOnFriendlyCardExiled()
	{
		var enemyExiler = CreateCard(false, "EnemyExiler");
		var myCard = CreateCard(true, "MyCard");
		CombatManager.combinedDeckZone.Add(enemyExiler);
		CombatManager.combinedDeckZone.Add(myCard);

		int fired = 0;
		RegisterEventCallback(GameEventStorage.onFriendlyCardExiled, () => fired++);

		var exile = CreateEffect<ExileEffect>(enemyExiler);
		EffectChainManager.MakeANewEffectRecorder(enemyExiler, exile.gameObject);
		exile.ExileTheirCards(1);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(0, fired, "enemy-caused exile of my cards must not fire the self-side exile event");
		Assert.IsFalse(CombatManager.combinedDeckZone.Contains(myCard), "my card is still exiled");
	}
}
