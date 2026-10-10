using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// S2 EditMode tests for the key-card selection rule (plan
/// plan-opponent-select-page-2026-10-06 §4.3, 2026-10-10 ruling): cardTypeID dedupe,
/// utility-passive exclusion, rarity-tier fill with random-within-tier (seeded rounds,
/// 09-13手法), display ordering, and the short-deck edge.
/// </summary>
public class OpponentKeyCardSelectorTests
{
	private readonly List<GameObject> spawned = new List<GameObject>();

	[TearDown]
	public void TearDown()
	{
		foreach (GameObject go in spawned)
		{
			if (go != null) UnityEngine.Object.DestroyImmediate(go);
		}
		spawned.Clear();
	}

	private CardScript MakeCard(string typeID, EnumStorage.Rarity rarity, bool utilityPassive = false)
	{
		GameObject go = new GameObject("card_" + typeID + "_" + spawned.Count);
		spawned.Add(go);
		CardScript card = go.AddComponent<CardScript>();
		card.cardTypeID = typeID;
		card.rarity = rarity;
		if (utilityPassive)
		{
			card.isPassive = true;
			card.utilityKind = EnumStorage.UtilityKind.Income;
		}
		return card;
	}

	[Test]
	public void DedupesByTypeId_KeepingHighestDisplayPriority()
	{
		CardScript rareA = MakeCard("A", EnumStorage.Rarity.Rare);
		MakeCard("A", EnumStorage.Rarity.Uncommon);  // duplicate typeID: one slot only
		CardScript commonB = MakeCard("B", EnumStorage.Rarity.Common);

		List<CardScript> picked = OpponentKeyCardSelector.Select(
			new List<CardScript> { rareA, MakeCard("A", EnumStorage.Rarity.Uncommon), commonB });

		Assert.AreEqual(2, picked.Count);
		Assert.AreEqual("A", picked[0].cardTypeID);
		Assert.AreEqual("B", picked[1].cardTypeID);
		Assert.AreEqual(EnumStorage.Rarity.Rare, picked[0].rarity);
		Assert.AreSame(rareA, picked[0]);  // the rare copy wins the display slot
	}

	[Test]
	public void ExcludesUtilityPassives()
	{
		CardScript meter = MakeCard("M", EnumStorage.Rarity.Rare, utilityPassive: true);
		CardScript commonB = MakeCard("B", EnumStorage.Rarity.Common);
		CardScript commonC = MakeCard("C", EnumStorage.Rarity.Common);

		List<CardScript> picked = OpponentKeyCardSelector.Select(
			new List<CardScript> { meter, commonB, commonC });

		Assert.AreEqual(2, picked.Count);
		CollectionAssert.Contains(picked, commonB);
		CollectionAssert.Contains(picked, commonC);
		CollectionAssert.DoesNotContain(picked, meter);
	}

	[Test]
	public void FillsByRarityTiers_RareFirstThenLowerTiers()
	{
		CardScript rare = MakeCard("R", EnumStorage.Rarity.Rare);
		CardScript c1 = MakeCard("C1", EnumStorage.Rarity.Common);
		CardScript c2 = MakeCard("C2", EnumStorage.Rarity.Common);
		CardScript c3 = MakeCard("C3", EnumStorage.Rarity.Common);

		List<CardScript> picked = OpponentKeyCardSelector.Select(
			new List<CardScript> { c1, rare, c2, c3 });

		Assert.AreEqual(3, picked.Count);
		Assert.AreSame(rare, picked[0], "the rare tier fills first");
		// The two remaining slots come from the common tier — WHICH two of the three is
		// the tier random's call, so only tier membership and distinctness are asserted.
		Assert.IsTrue(commons(c1, c2, c3).Contains(picked[1]) && commons(c1, c2, c3).Contains(picked[2]) && !ReferenceEquals(picked[1], picked[2]),
			"slots 2-3 must be two distinct commons");
	}

	private static List<CardScript> commons(params CardScript[] cards)
	{
		return new List<CardScript>(cards);
	}

	[Test]
	public void UncommonTier_FillsBeforeCommon()
	{
		CardScript rare = MakeCard("R", EnumStorage.Rarity.Rare);
		CardScript uncommon = MakeCard("U", EnumStorage.Rarity.Uncommon);
		CardScript common = MakeCard("C", EnumStorage.Rarity.Common);

		List<CardScript> picked = OpponentKeyCardSelector.Select(
			new List<CardScript> { common, uncommon, rare });

		Assert.AreEqual(3, picked.Count);
		Assert.AreSame(rare, picked[0]);
		Assert.AreSame(uncommon, picked[1]);
		Assert.AreSame(common, picked[2]);
	}

	[Test]
	public void RandomizesWithinTier_AcrossSeededRounds()
	{
		// 5 commons compete for 2 remaining slots behind the fixed rare: across 40 fresh
		// rounds under a fixed seed the picked pair must not always be the same
		// ((1/C(5,2))^40 odds against).
		UnityEngine.Random.InitState(20261010);
		bool hitVariety = false;
		var firstPicked = new HashSet<string>();
		for (int round = 0; round < 40 && !hitVariety; round++)
		{
			var deck = new List<CardScript> { MakeCard("R" + round, EnumStorage.Rarity.Rare) };
			for (int k = 1; k <= 5; k++) deck.Add(MakeCard("C" + round + "_" + k, EnumStorage.Rarity.Common));
			List<CardScript> picked = OpponentKeyCardSelector.Select(deck);
			Assert.AreEqual(3, picked.Count);
			Assert.AreEqual("R" + round, picked[0].cardTypeID);
			string pairKey = picked[1].cardTypeID + "|" + picked[2].cardTypeID;
			if (!firstPicked.Add(pairKey)) continue;
			if (firstPicked.Count > 1) hitVariety = true;
		}
		Assert.IsTrue(hitVariety, "the within-tier random never varied across 40 seeded rounds");
	}

	[Test]
	public void DisplayOrder_RarityDescThenDeckOrder()
	{
		// Deck order deliberately interleaves rarities; all three are picked, so the
		// random plays no part — the display order must sort them.
		CardScript commonA = MakeCard("CA", EnumStorage.Rarity.Common);
		CardScript rare = MakeCard("R", EnumStorage.Rarity.Rare);
		CardScript commonB = MakeCard("CB", EnumStorage.Rarity.Common);

		List<CardScript> picked = OpponentKeyCardSelector.Select(
			new List<CardScript> { commonA, rare, commonB });

		CollectionAssert.AreEqual(
			new[] { "R", "CA", "CB" },
			picked.ConvertAll(c => c.cardTypeID));
	}

	[Test]
	public void ShortDeck_ReturnsActualCount()
	{
		CardScript a = MakeCard("A", EnumStorage.Rarity.Uncommon);
		CardScript b = MakeCard("B", EnumStorage.Rarity.Uncommon);

		List<CardScript> picked = OpponentKeyCardSelector.Select(new List<CardScript> { a, b });

		Assert.AreEqual(2, picked.Count);
	}

	[Test]
	public void NullOrEmptyDeck_ReturnsEmpty()
	{
		Assert.AreEqual(0, OpponentKeyCardSelector.Select(null).Count);
		Assert.AreEqual(0, OpponentKeyCardSelector.Select(new List<CardScript>()).Count);
	}
}
