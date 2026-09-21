using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Regression lock for the synthetic positive-control specimen
/// (plans/plan-infinity-specimen-synthetic-2026-09-21.md §4): the 14 pipeline tests depend on
/// TEST_LOOP_HUB staying an UNGATED revive hub and on the specimen deck staying exactly two
/// copies of it. A future gate sweep that gives the hub a per-round limit, or a deck edit that
/// changes the copy count, must fail here instead of silently starving the positive control
/// again (the 2026-09-20 incident: gating RELIC_CURSE_REVIVAL bounded the only real loop
/// specimen and 14 tests went red).
/// </summary>
public class InfinitySpecimenTests
{
	private const string HubPath = "Assets/Prefabs/Cards/4.0/-1_Test/TEST_LOOP_HUB.prefab";
	private const string DeckPath = "Assets/SORefs/Decks/test decks/chain tests/4.0/test infinite loop.asset";

	[Test]
	public void HubIsAnUngatedReviveHub()
	{
		var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(HubPath);
		Assert.IsNotNull(prefab, "specimen prefab missing: " + HubPath);
		Assert.AreEqual("TEST_LOOP_HUB", prefab.GetComponent<CardScript>().cardTypeID);

		ReviveEffect[] effects = prefab.GetComponentsInChildren<ReviveEffect>(true);
		Assert.AreEqual(1, effects.Length, "the hub must carry exactly one ReviveEffect");
		Assert.AreEqual(0, effects[0].oncePerRound,
			"the specimen exists BECAUSE it is ungated — a per-round limit here starves the positive control");
		Assert.AreEqual(0, (int)effects[0].creatureFilter,
			"an Any-creature filter is what lets the two hubs revive each other");
		Assert.IsTrue(effects[0].excludeSelf,
			"excludeSelf only skips the source instance, so the pair still pulls each other");
	}

	[Test]
	public void SpecimenDeckIsExactlyTwoHubs()
	{
		var deck = UnityEditor.AssetDatabase.LoadAssetAtPath<DeckSO>(DeckPath);
		Assert.IsNotNull(deck, "specimen deck missing: " + DeckPath);
		Assert.AreEqual(2, deck.deck.Count, "the minimal same-round loop is exactly two copies");

		foreach (var card in deck.deck)
		{
			Assert.IsNotNull(card, "deck entries must resolve");
			Assert.AreEqual("TEST_LOOP_HUB", card.GetComponent<CardScript>().cardTypeID,
				"every entry must be the hub card itself");
		}
		Assert.AreEqual(deck.deck[0], deck.deck[1], "both entries are the same prefab asset");
	}
}
