using DefaultNamespace;
using NUnit.Framework;

/// <summary>
/// EditMode tests for the throne zone (王座区 = Start Card 前 N 张) engine:
/// the N cards directly above the Start Card — the deck tail revealed last.
/// </summary>
public class ThroneZoneTests : HeadlessCombatTestFixture
{
	[Test]
	public void GetStartCardIndex_ReturnsStartCardPosition()
	{
		CombatManager.combinedDeckZone.Add(CreateCard(true, "A"));
		var startCard = CreateStartCard();
		CombatManager.combinedDeckZone.Add(startCard);

		Assert.AreEqual(1, CombatManager.GetStartCardIndex(), "Start card at index 1 after one card below it");
	}

	[Test]
	public void GetStartCardIndex_ReturnsMinusOneWhenAbsent()
	{
		CombatManager.combinedDeckZone.Add(CreateCard(true, "A"));

		Assert.AreEqual(-1, CombatManager.GetStartCardIndex(), "No start card in deck");
	}

	[Test]
	public void GetThroneZoneCards_ReturnsCardsDirectlyAboveStartCard()
	{
		var startCard = CreateStartCard();
		CombatManager.combinedDeckZone.Add(startCard); // index 0
		var a = CreateCard(true, "A");
		CombatManager.combinedDeckZone.Add(a); // index 1
		var b = CreateCard(true, "B");
		CombatManager.combinedDeckZone.Add(b); // index 2
		var c = CreateCard(true, "C");
		CombatManager.combinedDeckZone.Add(c); // index 3

		var zone = CombatManager.GetThroneZoneCards(2);
		Assert.AreEqual(2, zone.Count, "Throne zone of size 2");
		Assert.AreEqual(a, zone[0], "First throne card = directly above the start card");
		Assert.AreEqual(b, zone[1], "Second throne card");
	}

	[Test]
	public void GetThroneZoneCards_ReturnsFewerWhenZoneShort()
	{
		var startCard = CreateStartCard();
		CombatManager.combinedDeckZone.Add(startCard);
		var a = CreateCard(true, "A");
		CombatManager.combinedDeckZone.Add(a);

		var zone = CombatManager.GetThroneZoneCards(5);
		Assert.AreEqual(1, zone.Count, "Only one card sits above the start card");
	}

	[Test]
	public void IsCardInThroneZone_ChecksZoneMembership()
	{
		var startCard = CreateStartCard();
		CombatManager.combinedDeckZone.Add(startCard);
		var a = CreateCard(true, "A");
		CombatManager.combinedDeckZone.Add(a);
		var b = CreateCard(true, "B");
		CombatManager.combinedDeckZone.Add(b);

		Assert.IsTrue(CombatManager.IsCardInThroneZone(a, 2), "Card A is in the throne zone");
		Assert.IsFalse(CombatManager.IsCardInThroneZone(b, 1), "Card B is outside a size-1 throne zone");
		Assert.IsFalse(CombatManager.IsCardInThroneZone(startCard, 5), "Start card itself is never in the throne zone");
	}

	[Test]
	public void MoveCardToThroneZone_MovesCardFromAbove()
	{
		var startCard = CreateStartCard();
		CombatManager.combinedDeckZone.Add(startCard); // 0
		var a = CreateCard(true, "A");
		CombatManager.combinedDeckZone.Add(a); // 1
		var b = CreateCard(true, "B");
		CombatManager.combinedDeckZone.Add(b); // 2

		CombatManager.MoveCardToThroneZone(b);

		Assert.AreEqual(startCard, CombatManager.combinedDeckZone[0], "Start card stays at the bottom");
		Assert.AreEqual(b, CombatManager.combinedDeckZone[1], "Moved card sits directly above the start card");
		Assert.AreEqual(a, CombatManager.combinedDeckZone[2], "Other card pushed up");
	}

	[Test]
	public void MoveCardToThroneZone_MovesCardFromGrave()
	{
		var buried = CreateCard(true, "Buried");
		CombatManager.combinedDeckZone.Add(buried); // 0 (grave, below start card)
		var startCard = CreateStartCard();
		CombatManager.combinedDeckZone.Add(startCard); // 1
		var a = CreateCard(true, "A");
		CombatManager.combinedDeckZone.Add(a); // 2

		CombatManager.MoveCardToThroneZone(buried);

		Assert.AreEqual(startCard, CombatManager.combinedDeckZone[0], "Start card remains the bottom card");
		Assert.AreEqual(buried, CombatManager.combinedDeckZone[1], "Grave card promoted into the throne zone");
		Assert.AreEqual(a, CombatManager.combinedDeckZone[2], "Card above unchanged");
	}

	[Test]
	public void MoveCardToThroneZone_NoOpWhenAlreadyInPlace()
	{
		var startCard = CreateStartCard();
		CombatManager.combinedDeckZone.Add(startCard); // 0
		var a = CreateCard(true, "A");
		CombatManager.combinedDeckZone.Add(a); // 1

		CombatManager.MoveCardToThroneZone(a);

		Assert.AreEqual(2, CombatManager.combinedDeckZone.Count, "Deck size unchanged");
		Assert.AreEqual(a, CombatManager.combinedDeckZone[1], "Card stays in the throne zone");
	}
}
