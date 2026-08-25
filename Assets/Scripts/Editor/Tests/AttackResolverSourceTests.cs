using DefaultNamespace;
using DefaultNamespace.SOScripts;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode tests for AttackResolverSource (attack = Y 常态结算 engine):
/// live resolver sums, faction-relative reading, enemy-side aggregates, multi-term
/// cards (ALMIGHTY), and resolver cleanup on disable.
/// </summary>
public class AttackResolverSourceTests : HeadlessCombatTestFixture
{
	private AttackResolverSource.Term AddTerm(AttackResolverSource resolver, AttackResolverSource.Source source, string cardTypeID = null)
	{
		var term = new AttackResolverSource.Term { source = source, cardTypeID = cardTypeID };
		resolver.terms.Add(term);
		return term;
	}

	private GameObject AddDeckCard(bool isOwner, string name, string cardTypeID, int printedAttack)
	{
		var card = CreateCard(isOwner, name, cardTypeID);
		card.GetComponent<CardScript>().printedAttack = printedAttack;
		CombatManager.combinedDeckZone.Add(card);
		return card;
	}

	[Test]
	public void FriendlyCardTotal_SumsFriendlyAttackOnly()
	{
		AddDeckCard(true, "F1", "A", 2);
		AddDeckCard(true, "F2", "B", 3);
		AddDeckCard(false, "E1", "C", 9); // enemy attack must not leak in

		var card = CreateCard(true, "Carrier");
		var resolver = card.AddComponent<AttackResolverSource>();
		AddTerm(resolver, AttackResolverSource.Source.FriendlyCardTotal);
		resolver.RefreshAttackResolver();

		var cs = card.GetComponent<CardScript>();
		Assert.AreEqual(5, cs.GetAttack(), "Sum of friendly attacks only (enemy excluded)");
	}

	[Test]
	public void FriendlyCardTotal_IsRelativeToResolverFaction()
	{
		AddDeckCard(true, "F1", "A", 2);
		AddDeckCard(false, "E1", "C", 7);

		var enemyCard = CreateCard(false, "EnemyCarrier");
		var resolver = enemyCard.AddComponent<AttackResolverSource>();
		AddTerm(resolver, AttackResolverSource.Source.FriendlyCardTotal);
		resolver.RefreshAttackResolver();

		Assert.AreEqual(7, enemyCard.GetComponent<CardScript>().GetAttack(), "Enemy resolver sums enemy-side attack");
	}

	[Test]
	public void FriendlyCardCount_CountsFriendlyCards()
	{
		AddDeckCard(true, "F1", "A", 0);
		AddDeckCard(true, "F2", "B", 1);
		AddDeckCard(false, "E1", "C", 1);

		var card = CreateCard(true, "Carrier");
		var resolver = card.AddComponent<AttackResolverSource>();
		AddTerm(resolver, AttackResolverSource.Source.FriendlyCardCount);
		resolver.RefreshAttackResolver();

		Assert.AreEqual(2, card.GetComponent<CardScript>().GetAttack(), "Friendly card count (faction-relative)");
	}

	[Test]
	public void GraveyardFriendlyCount_CountsFriendlyCardsBelowStartCard()
	{
		var startCard = CreateStartCard();

		// Deck layout: index 0 = bottom (buried grave), start card at index 1,
		// further cards above it — only cards BELOW the start card count as the grave.
		CombatManager.combinedDeckZone.Clear();
		AddDeckCard(true, "Buried", "A", 0);   // index 0 = below the start card (grave)
		CombatManager.combinedDeckZone.Add(startCard); // index 1
		AddDeckCard(true, "Top", "B", 0);      // index 2 — above the start card
		AddDeckCard(false, "EnemyBuried", "C", 0); // index 3

		var card = CreateCard(true, "Carrier");
		var resolver = card.AddComponent<AttackResolverSource>();
		AddTerm(resolver, AttackResolverSource.Source.GraveyardFriendlyCount);
		resolver.RefreshAttackResolver();

		Assert.AreEqual(1, card.GetComponent<CardScript>().GetAttack(), "Friendly cards below the start card");
	}

	[Test]
	public void FriendlyRiftCount_FiltersByCardTypeID()
	{
		AddDeckCard(true, "Rift1", "RIFT", 0);
		AddDeckCard(true, "Rift2", "RIFT", 0);
		AddDeckCard(true, "Other", "NOT_RIFT", 0);
		AddDeckCard(false, "EnemyRift", "RIFT", 0); // enemy rift must not count

		var card = CreateCard(true, "Carrier");
		var resolver = card.AddComponent<AttackResolverSource>();
		AddTerm(resolver, AttackResolverSource.Source.FriendlyRiftCount, "RIFT");
		resolver.RefreshAttackResolver();

		Assert.AreEqual(2, card.GetComponent<CardScript>().GetAttack(), "Friendly RIFT-typed cards only");
	}

	[Test]
	public void EnemyNegativeTotal_SumsEnemyCurseAttack()
	{
		AddDeckCard(false, "Curse1", "JU_ON", 2);
		AddDeckCard(false, "Curse2", "JU_ON", 3);
		AddDeckCard(false, "Other", "X", 9); // non-curse enemy attack must not count
		AddDeckCard(true, "MyCurse", "JU_ON", 8); // friendly curse must not count

		var card = CreateCard(true, "Carrier");
		var resolver = card.AddComponent<AttackResolverSource>();
		AddTerm(resolver, AttackResolverSource.Source.EnemyNegativeTotal, "JU_ON");
		resolver.RefreshAttackResolver();

		Assert.AreEqual(5, card.GetComponent<CardScript>().GetAttack(), "Enemy JU_ON attack sum");
	}

	[Test]
	public void EnemyNegativeHighest_TakesMaxEnemyCurseAttack()
	{
		AddDeckCard(false, "Curse1", "JU_ON", 2);
		AddDeckCard(false, "Curse2", "JU_ON", 7);

		var card = CreateCard(true, "Carrier");
		var resolver = card.AddComponent<AttackResolverSource>();
		AddTerm(resolver, AttackResolverSource.Source.EnemyNegativeHighest, "JU_ON");
		resolver.RefreshAttackResolver();

		Assert.AreEqual(7, card.GetComponent<CardScript>().GetAttack(), "Highest enemy curse attack");
	}

	[Test]
	public void MultiTermResolver_SumsAllTermsLikeAlmighty()
	{
		// ALMIGHTY: 墓地友方卡数 + 友方[次元裂缝]数 + 敌方[负面]攻击力总和
		var startCard = CreateStartCard();
		CombatManager.combinedDeckZone.Clear();
		AddDeckCard(true, "Buried", "A", 0);    // grave friendly -> 1
		CombatManager.combinedDeckZone.Add(startCard);
		AddDeckCard(true, "Rift1", "RIFT", 0);  // rift -> 1
		AddDeckCard(true, "Rift2", "RIFT", 0);  // rift -> 1
		AddDeckCard(false, "Curse", "JU_ON", 3); // enemy negative -> 3

		var card = CreateCard(true, "Almighty");
		var resolver = card.AddComponent<AttackResolverSource>();
		AddTerm(resolver, AttackResolverSource.Source.GraveyardFriendlyCount);
		AddTerm(resolver, AttackResolverSource.Source.FriendlyRiftCount, "RIFT");
		AddTerm(resolver, AttackResolverSource.Source.EnemyNegativeTotal, "JU_ON");
		resolver.RefreshAttackResolver();

		Assert.AreEqual(6, card.GetComponent<CardScript>().GetAttack(), "1 grave + 2 rifts + 3 enemy curse attack");
	}

	[Test]
	public void ResolverReadsLiveChanges()
	{
		AddDeckCard(true, "F1", "A", 2);

		var card = CreateCard(true, "Carrier");
		var resolver = card.AddComponent<AttackResolverSource>();
		AddTerm(resolver, AttackResolverSource.Source.FriendlyCardTotal);
		resolver.RefreshAttackResolver();

		var cs = card.GetComponent<CardScript>();
		Assert.AreEqual(2, cs.GetAttack(), "Initial sum");

		AddDeckCard(true, "F2", "B", 4);
		Assert.AreEqual(6, cs.GetAttack(), "Resolver re-reads the deck on every GetAttack()");
	}

	[Test]
	public void ClearAttackResolver_RestoresBaseAttack()
	{
		AddDeckCard(true, "F1", "A", 2);

		var card = CreateCard(true, "Carrier");
		card.GetComponent<CardScript>().printedAttack = 1;
		var resolver = card.AddComponent<AttackResolverSource>();
		AddTerm(resolver, AttackResolverSource.Source.FriendlyCardTotal);
		resolver.RefreshAttackResolver();

		var cs = card.GetComponent<CardScript>();
		Assert.AreEqual(2, cs.GetAttack(), "Resolver active");

		// Simulates OnDisable (lifecycle callbacks do not fire for runtime-added components in EditMode).
		resolver.ClearAttackResolver();
		Assert.AreEqual(1, cs.GetAttack(), "Resolver removed -> base attack");

		resolver.RefreshAttackResolver();
		Assert.AreEqual(2, cs.GetAttack(), "Resolver restored");
	}

	[Test]
	public void FriendlyCardTotal_CarrierInDeck_ExcludesSelf()
	{
		AddDeckCard(true, "F1", "A", 2);
		AddDeckCard(true, "F2", "B", 3);

		var carrier = CreateCard(true, "Carrier");
		carrier.GetComponent<CardScript>().printedAttack = 2;
		CombatManager.combinedDeckZone.Add(carrier);

		var resolver = carrier.AddComponent<AttackResolverSource>();
		AddTerm(resolver, AttackResolverSource.Source.FriendlyCardTotal);
		resolver.RefreshAttackResolver();

		Assert.AreEqual(5, carrier.GetComponent<CardScript>().GetAttack(),
			"Sum of OTHER friendly attacks (2+3), never the carrier itself (regression: self-inclusion stack overflow)");
	}

	[Test]
	public void FriendlyCardTotal_TwoCarriersSameSide_NoRecursion()
	{
		AddDeckCard(true, "C", "C", 2);
		AddDeckCard(true, "D", "D", 3);

		var carrierA = CreateCard(true, "CarrierA");
		var carrierB = CreateCard(true, "CarrierB");
		CombatManager.combinedDeckZone.Add(carrierA);
		CombatManager.combinedDeckZone.Add(carrierB);

		var resolverA = carrierA.AddComponent<AttackResolverSource>();
		AddTerm(resolverA, AttackResolverSource.Source.FriendlyCardTotal);
		resolverA.RefreshAttackResolver();
		var resolverB = carrierB.AddComponent<AttackResolverSource>();
		AddTerm(resolverB, AttackResolverSource.Source.FriendlyCardTotal);
		resolverB.RefreshAttackResolver();

		var csA = carrierA.GetComponent<CardScript>();
		var csB = carrierB.GetComponent<CardScript>();
		Assert.AreEqual(10, csA.GetAttack(),
			"A reads B (cut to base 0 inside A's evaluation) + C(2) + D(3) — the nested result, not B's base");
		Assert.AreEqual(10, csB.GetAttack(),
			"B queried as its own evaluation reads A (cut to base 0) + C(2) + D(3) — either order terminates");
	}

	[Test]
	public void Resolver_GuardResetsAfterEvaluation()
	{
		AddDeckCard(true, "F1", "A", 2);

		var carrier = CreateCard(true, "Carrier");
		carrier.GetComponent<CardScript>().printedAttack = 1;
		var resolver = carrier.AddComponent<AttackResolverSource>();
		AddTerm(resolver, AttackResolverSource.Source.FriendlyCardTotal);
		resolver.RefreshAttackResolver();

		var cs = carrier.GetComponent<CardScript>();
		Assert.AreEqual(2, cs.GetAttack(), "First evaluation");
		Assert.AreEqual(2, cs.GetAttack(), "Second evaluation — the reentrancy flag cleared after the first");
	}

	[Test]
	public void UpdateAllTrackers_WithResolverCardInDeck_NoOverflow()
	{
		ValueTrackerManager.totalPowerCountInDeckRef = CreateScriptableObject<IntSO>();

		AddDeckCard(true, "F1", "A", 2);
		AddDeckCard(true, "F2", "B", 3);
		var carrier = CreateCard(true, "Carrier");
		CombatManager.combinedDeckZone.Add(carrier);
		var resolver = carrier.AddComponent<AttackResolverSource>();
		AddTerm(resolver, AttackResolverSource.Source.FriendlyCardTotal);
		resolver.RefreshAttackResolver();

		ValueTrackerManager.UpdateAllTrackers();

		Assert.AreEqual(5, ValueTrackerManager.totalPowerCountInDeckRef.value,
			"Every-effect tracker path terminates with a resolver card in the deck (self excluded)");
	}

	[Test]
	public void EnemyNegativeTotal_EnemyCarrierCarriesResolver_NoRecursion()
	{
		var friendly = CreateCard(true, "FriendlyCarrier", "F_Y");
		CombatManager.combinedDeckZone.Add(friendly);
		var resolverF = friendly.AddComponent<AttackResolverSource>();
		AddTerm(resolverF, AttackResolverSource.Source.EnemyNegativeTotal, "E_X");
		resolverF.RefreshAttackResolver();

		var enemy = CreateCard(false, "EnemyCarrier", "E_X");
		CombatManager.combinedDeckZone.Add(enemy);
		var resolverE = enemy.AddComponent<AttackResolverSource>();
		AddTerm(resolverE, AttackResolverSource.Source.EnemyNegativeTotal, "F_Y");
		resolverE.RefreshAttackResolver();

		Assert.AreEqual(0, friendly.GetComponent<CardScript>().GetAttack(),
			"Friendly carrier reads enemy carrier (cut to base 0 inside the evaluation) — cross-faction cycle terminates");
		Assert.AreEqual(0, enemy.GetComponent<CardScript>().GetAttack(),
			"Enemy carrier queried directly reads friendly carrier (cut to base 0) — terminates");
	}
}
