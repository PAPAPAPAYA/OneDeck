using DefaultNamespace;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode tests for roadmap step 4 batch 3: E5 FriendlyHighest resolver term plus
/// the three pure-config resolver cards GRAVE_GIANT (墓地友方数量), CURSE_EATER
/// (敌方诅咒攻击力) and MIMIC_BLADE (友方最高攻击力). These verify the engine term
/// semantics the cards bind to.
/// </summary>
public class Batch3ResolverTermTests : HeadlessCombatTestFixture
{
	private AttackResolverSource.Term AddTerm(AttackResolverSource resolver, AttackResolverSource.Source source, string cardTypeID = null)
	{
		var term = new AttackResolverSource.Term { source = source, cardTypeID = cardTypeID };
		resolver.terms.Add(term);
		return term;
	}

	private GameObject AddDeckCard(bool isOwner, string name, string cardTypeID, int printedAttack, bool isCreature = false)
	{
		var card = CreateCard(isOwner, name, cardTypeID);
		var cs = card.GetComponent<CardScript>();
		cs.printedAttack = printedAttack;
		cs.cardType = isCreature ? EnumStorage.CardType.Creature : EnumStorage.CardType.None;
		CombatManager.combinedDeckZone.Add(card);
		return card;
	}

	[Test]
	public void FriendlyHighest_TakesMaxFriendlyAttack_ExcludesCarrierAndEnemy()
	{
		AddDeckCard(true, "F1", "A", 2);
		AddDeckCard(true, "F2", "B", 5);
		AddDeckCard(false, "E1", "C", 99); // enemy must never win a friendly max

		var carrier = CreateCard(true, "Carrier");
		carrier.GetComponent<CardScript>().printedAttack = 100; // carrier itself excluded
		var resolver = carrier.AddComponent<AttackResolverSource>();
		AddTerm(resolver, AttackResolverSource.Source.FriendlyHighest);
		resolver.RefreshAttackResolver();

		Assert.AreEqual(5, carrier.GetComponent<CardScript>().GetAttack(),
			"highest friendly attack excluding the carrier and all enemies");
	}

	[Test]
	public void FriendlyHighest_ZeroWithNoEligibleFriendly()
	{
		var carrier = CreateCard(true, "Carrier");
		var resolver = carrier.AddComponent<AttackResolverSource>();
		AddTerm(resolver, AttackResolverSource.Source.FriendlyHighest);
		resolver.RefreshAttackResolver();

		Assert.AreEqual(0, carrier.GetComponent<CardScript>().GetAttack());
	}

	[Test]
	public void GraveyardFriendlyCount_CountsFriendlyOnlyBelowStartCard()
	{
		var startCard = CreateCard(true, "StartCard");
		startCard.GetComponent<CardScript>().isStartCard = true;
		var grave1 = AddDeckCard(true, "G1", "A", 0);
		var grave2 = AddDeckCard(true, "G2", "B", 0);
		AddDeckCard(false, "E1", "C", 0); // enemy in grave region must not count
		AddDeckCard(true, "Live", "D", 0); // friendly above start card must not count

		// deck order (index 0 = bottom): [grave1, grave2, enemy, startCard, live]
		CombatManager.combinedDeckZone.Clear();
		CombatManager.combinedDeckZone.Add(grave1);
		CombatManager.combinedDeckZone.Add(grave2);
		CombatManager.combinedDeckZone.Add(AddDeckCard(false, "E1", "C", 0));
		CombatManager.combinedDeckZone.Add(startCard);
		CombatManager.combinedDeckZone.Add(AddDeckCard(true, "Live", "D", 0));

		var giant = CreateCard(true, "Giant");
		var resolver = giant.AddComponent<AttackResolverSource>();
		AddTerm(resolver, AttackResolverSource.Source.GraveyardFriendlyCount);
		resolver.RefreshAttackResolver();

		Assert.AreEqual(2, giant.GetComponent<CardScript>().GetAttack(),
			"friendly cards below the start card only (enemy and live-zone excluded)");
	}

	[Test]
	public void EnemyNegativeTotal_SumsEnemyCurseAttack()
	{
		GameEventStorage.curseCardTypeID.value = "JU_ON";
		var curse1 = AddDeckCard(false, "Curse1", "JU_ON", 4);
		var curse2 = AddDeckCard(false, "Curse2", "JU_ON", 3);
		AddDeckCard(false, "Other", "OTHER", 50); // enemy non-curse must not count
		AddDeckCard(true, "MyCurse", "JU_ON", 10); // own-side curse must not count

		var eater = CreateCard(true, "Eater");
		var resolver = eater.AddComponent<AttackResolverSource>();
		AddTerm(resolver, AttackResolverSource.Source.EnemyNegativeTotal, "JU_ON");
		resolver.RefreshAttackResolver();

		Assert.AreEqual(7, eater.GetComponent<CardScript>().GetAttack(),
			"sum of enemy curse attack only");
	}
}
