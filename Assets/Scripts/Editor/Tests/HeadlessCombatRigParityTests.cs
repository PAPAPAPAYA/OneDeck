using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Production-parity pins for HeadlessCombatRig's reveal primitives — the P5 batch scan and the
/// P2 attribution sims only measure production faithfully when the rig's arrangement evolution
/// matches CombatManager's. R2 grave bounce (2026-09-20 review finding): production
/// PutRevealedCardToBottom routes through CombatManager.ResolveGravePlacement — a card with
/// currentLife > 0 bounces to just above the Start Card (startCardIndex + 1, so it is revealed
/// again this round) and consumes 1 life; only life-less cards go to index 0, and everything
/// goes to index 0 in the R13 window (Start Card unlocatable). The rig historically inserted
/// every revealed card at index 0, so decks with life-bearing cards simulated a different
/// arrangement evolution than production.
/// </summary>
public class HeadlessCombatRigParityTests
{
	[OneTimeTearDown]
	public void OneTimeTearDown()
	{
		HeadlessCombatRig.DestroySharedResources();
	}

	[Test]
	public void PutToBottom_LifeCard_BouncesAboveStartCard_AndConsumesOneLife()
	{
		using (var rig = HeadlessCombatRig.Create())
		{
			var cm = rig.CombatManager;
			var startCard = CreateStartCard(rig);
			var filler = rig.CreateCard(true, "Filler", "FILLER");
			var lifeCard = rig.CreateCard(true, "LifeCard", "LIFE_CARD");
			var lifeScript = lifeCard.GetComponent<CardScript>();
			lifeScript.lifeMax = 2;
			lifeScript.currentLife = 2;
			// Deck order: index 0 = bottom (last revealed), index Count-1 = top (revealed next).
			cm.combinedDeckZone.Add(startCard);
			cm.combinedDeckZone.Add(filler);
			cm.combinedDeckZone.Add(lifeCard);

			Assert.AreSame(lifeCard.GetComponent<CardScript>(), rig.RevealTopCard(),
				"the top card is the one revealed");
			rig.PutRevealedCardToBottom();

			Assert.AreEqual(0, cm.combinedDeckZone.IndexOf(startCard), "the Start Card stays at the bottom");
			Assert.AreEqual(1, cm.combinedDeckZone.IndexOf(lifeCard),
				"R2: a card with life left bounces to startCardIndex + 1 (just above the Start Card), not the grave");
			Assert.AreEqual(2, cm.combinedDeckZone.IndexOf(filler));
			Assert.AreEqual(1, lifeScript.currentLife,
				"the bounce consumes 1 life (production: ResolveGravePlacement)");
		}
	}

	[Test]
	public void PutToBottom_LifelessCard_GoesToIndex0()
	{
		using (var rig = HeadlessCombatRig.Create())
		{
			var cm = rig.CombatManager;
			var startCard = CreateStartCard(rig);
			var filler = rig.CreateCard(true, "Filler", "FILLER");
			var plain = rig.CreateCard(true, "PlainCard", "PLAIN_CARD");
			cm.combinedDeckZone.Add(startCard);
			cm.combinedDeckZone.Add(filler);
			cm.combinedDeckZone.Add(plain);

			rig.RevealTopCard();
			rig.PutRevealedCardToBottom();

			Assert.AreEqual(0, cm.combinedDeckZone.IndexOf(plain),
				"a card without life goes to the grave (index 0), below the Start Card");
			Assert.AreEqual(0, plain.GetComponent<CardScript>().currentLife);
		}
	}

	[Test]
	public void PutToBottom_ExhaustedLife_GoesToIndex0()
	{
		using (var rig = HeadlessCombatRig.Create())
		{
			var cm = rig.CombatManager;
			var startCard = CreateStartCard(rig);
			var lifeCard = rig.CreateCard(true, "LifeCard", "LIFE_CARD");
			var lifeScript = lifeCard.GetComponent<CardScript>();
			lifeScript.lifeMax = 1;
			lifeScript.currentLife = 1;
			cm.combinedDeckZone.Add(startCard);
			cm.combinedDeckZone.Add(lifeCard);

			// First put: the bounce consumes the last life, so the card lands above the Start Card.
			rig.RevealTopCard();
			rig.PutRevealedCardToBottom();
			Assert.AreEqual(1, cm.combinedDeckZone.IndexOf(lifeCard));
			Assert.AreEqual(0, lifeScript.currentLife);

			// Second put: no life left -> grave (index 0), matching production.
			rig.RevealTopCard();
			rig.PutRevealedCardToBottom();
			Assert.AreEqual(0, cm.combinedDeckZone.IndexOf(lifeCard),
				"once currentLife hits 0 the card stops bouncing and goes to the grave");
		}
	}

	[Test]
	public void PutToBottom_NoStartCard_GoesToIndex0_WithoutConsumingLife()
	{
		using (var rig = HeadlessCombatRig.Create())
		{
			var cm = rig.CombatManager;
			var filler = rig.CreateCard(true, "Filler", "FILLER");
			var lifeCard = rig.CreateCard(true, "LifeCard", "LIFE_CARD");
			var lifeScript = lifeCard.GetComponent<CardScript>();
			lifeScript.lifeMax = 2;
			lifeScript.currentLife = 2;
			// R13 (shuffle window): the Start Card is not in the deck, so no bounce target exists.
			cm.combinedDeckZone.Add(filler);
			cm.combinedDeckZone.Add(lifeCard);

			rig.RevealTopCard();
			rig.PutRevealedCardToBottom();

			Assert.AreEqual(0, cm.combinedDeckZone.IndexOf(lifeCard),
				"R13: with the Start Card unlocatable the card goes to the grave");
			Assert.AreEqual(2, lifeScript.currentLife,
				"production decrements life only when the bounce actually happens");
		}
	}

	private static GameObject CreateStartCard(HeadlessCombatRig rig)
	{
		var startCard = rig.CreateCard(true, "Start Card Stub", "START_CARD_STUB");
		startCard.GetComponent<CardScript>().isStartCard = true;
		return startCard;
	}
}
