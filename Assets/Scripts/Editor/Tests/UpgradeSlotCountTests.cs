using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode tests for the upgrade-slot cap counting helper:
/// UtilityFuncManagerScript.CountUpgradeCards (plans/plan-upgrade-slot-cap-2026-09-30.md).
/// Each utility-passive copy counts 1; slot-occupying cards and non-passives never count.
/// </summary>
public class UpgradeSlotCountTests
{
	private readonly List<Object> _cleanup = new List<Object>();

	[SetUp]
	public void SetUp()
	{
		// CardScript.OnEnable touches CardIDRetriever.Me; provide one defensively
		var idObj = new GameObject("TestCardIDRetriever");
		_cleanup.Add(idObj);
		DefaultNamespace.Managers.CardIDRetriever.Me =
			idObj.AddComponent<DefaultNamespace.Managers.CardIDRetriever>();
	}

	[TearDown]
	public void TearDown()
	{
		DefaultNamespace.Managers.CardIDRetriever.Me = null;
		foreach (var obj in _cleanup)
		{
			if (obj != null)
			{
				Object.DestroyImmediate(obj);
			}
		}
		_cleanup.Clear();
	}

	private GameObject CreateUpgradeCard(string cardTypeID)
	{
		var go = new GameObject("TestUpgradeCard");
		var cardScript = go.AddComponent<CardScript>();
		cardScript.cardTypeID = cardTypeID;
		cardScript.isPassive = true;
		cardScript.utilityKind = EnumStorage.UtilityKind.Income;
		cardScript.occupiesDeckSlot = false;
		_cleanup.Add(go);
		return go;
	}

	private GameObject CreateCombatCard(string cardTypeID)
	{
		var go = new GameObject("TestCombatCard");
		var cardScript = go.AddComponent<CardScript>();
		cardScript.cardTypeID = cardTypeID;
		cardScript.isPassive = false;
		cardScript.utilityKind = EnumStorage.UtilityKind.None;
		cardScript.occupiesDeckSlot = true;
		_cleanup.Add(go);
		return go;
	}

	private DeckSO CreateDeck(params GameObject[] cards)
	{
		var deck = ScriptableObject.CreateInstance<DeckSO>();
		deck.deck = new List<GameObject>(cards);
		_cleanup.Add(deck);
		return deck;
	}

	[Test]
	public void NullDeck_ReturnsZero()
	{
		Assert.AreEqual(0, UtilityFuncManagerScript.CountUpgradeCards(null));
	}

	[Test]
	public void EmptyDeck_ReturnsZero()
	{
		Assert.AreEqual(0, UtilityFuncManagerScript.CountUpgradeCards(CreateDeck()));
	}

	[Test]
	public void SingleUpgrade_CountsOne()
	{
		var deck = CreateDeck(CreateUpgradeCard("UPGRADE_A"));
		Assert.AreEqual(1, UtilityFuncManagerScript.CountUpgradeCards(deck));
	}

	[Test]
	public void DuplicateUpgrades_CountEachCopy()
	{
		// No cardTypeID dedup on the upgrade side: two copies of one upgrade occupy 2 slots.
		var deck = CreateDeck(CreateUpgradeCard("UPGRADE_A"), CreateUpgradeCard("UPGRADE_A"));
		Assert.AreEqual(2, UtilityFuncManagerScript.CountUpgradeCards(deck));
	}

	[Test]
	public void SlotOccupyingCards_NeverCount()
	{
		var deck = CreateDeck(CreateCombatCard("COMBAT_A"), CreateUpgradeCard("UPGRADE_A"));
		Assert.AreEqual(1, UtilityFuncManagerScript.CountUpgradeCards(deck));
	}

	[Test]
	public void PassiveWithoutUtilityKind_IsNotAnUpgrade()
	{
		// IsUtilityPassive = isPassive && utilityKind != None; a passive with kind None
		// (e.g. a plain system card) must not count.
		var go = new GameObject("TestPlainPassive");
		var cardScript = go.AddComponent<CardScript>();
		cardScript.cardTypeID = "PLAIN_PASSIVE";
		cardScript.isPassive = true;
		cardScript.utilityKind = EnumStorage.UtilityKind.None;
		_cleanup.Add(go);
		var deck = CreateDeck(go);
		Assert.AreEqual(0, UtilityFuncManagerScript.CountUpgradeCards(deck));
	}
}
