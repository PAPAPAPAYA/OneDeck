using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode tests for the duplicate-copies-share-slot rule:
/// UtilityFuncManagerScript.CountCardsTakingUpSpace(deck, duplicatesShareSlot)
/// and UtilityFuncManagerScript.DeckContainsCardType.
/// </summary>
public class DuplicateSlotCountTests
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

	private GameObject CreateCard(string cardTypeID, bool takeUpSpace = true)
	{
		var go = new GameObject("TestCard");
		var cardScript = go.AddComponent<CardScript>();
		cardScript.cardTypeID = cardTypeID;
		cardScript.takeUpSpace = takeUpSpace;
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
	public void ToggleOff_CountsEveryCopy()
	{
		var deck = CreateDeck(CreateCard("imp"), CreateCard("imp"), CreateCard("imp"));
		Assert.AreEqual(3, UtilityFuncManagerScript.CountCardsTakingUpSpace(deck, false));
	}

	[Test]
	public void LegacySingleArg_MatchesToggleOff()
	{
		var deck = CreateDeck(CreateCard("imp"), CreateCard("imp"), CreateCard("bat"));
		Assert.AreEqual(
			UtilityFuncManagerScript.CountCardsTakingUpSpace(deck, false),
			UtilityFuncManagerScript.CountCardsTakingUpSpace(deck));
	}

	[Test]
	public void ToggleOn_DuplicatesCountOnce()
	{
		var deck = CreateDeck(CreateCard("imp"), CreateCard("imp"), CreateCard("imp"));
		Assert.AreEqual(1, UtilityFuncManagerScript.CountCardsTakingUpSpace(deck, true));
	}

	[Test]
	public void ToggleOn_MixedTypes_CountDistinctTypes()
	{
		var deck = CreateDeck(CreateCard("imp"), CreateCard("bat"), CreateCard("imp"), CreateCard("bat"), CreateCard("orc"));
		Assert.AreEqual(3, UtilityFuncManagerScript.CountCardsTakingUpSpace(deck, true));
	}

	[Test]
	public void ToggleOn_EmptyCardTypeID_NeverDeduplicated()
	{
		var deck = CreateDeck(CreateCard(""), CreateCard(""), CreateCard(null));
		Assert.AreEqual(3, UtilityFuncManagerScript.CountCardsTakingUpSpace(deck, true));
	}

	[Test]
	public void TakeUpSpaceFalse_ExcludedInBothModes()
	{
		var deck = CreateDeck(CreateCard("imp"), CreateCard("imp", false), CreateCard("imp", false));
		Assert.AreEqual(1, UtilityFuncManagerScript.CountCardsTakingUpSpace(deck, true));
		Assert.AreEqual(1, UtilityFuncManagerScript.CountCardsTakingUpSpace(deck, false));
	}

	[Test]
	public void NullDeckAndNullEntries_ReturnZeroOrSkip()
	{
		Assert.AreEqual(0, UtilityFuncManagerScript.CountCardsTakingUpSpace(null, true));
		var deck = CreateDeck(null, CreateCard("imp"));
		Assert.AreEqual(1, UtilityFuncManagerScript.CountCardsTakingUpSpace(deck, true));
	}

	[Test]
	public void CardWithoutCardScript_Skipped()
	{
		var plain = new GameObject("PlainObject");
		_cleanup.Add(plain);
		var deck = CreateDeck(plain, CreateCard("imp"));
		Assert.AreEqual(1, UtilityFuncManagerScript.CountCardsTakingUpSpace(deck, true));
	}

	[Test]
	public void DeckContainsCardType_FindsMatch()
	{
		var deck = CreateDeck(CreateCard("imp"), CreateCard("bat"));
		Assert.IsTrue(UtilityFuncManagerScript.DeckContainsCardType(deck, "bat"));
		Assert.IsFalse(UtilityFuncManagerScript.DeckContainsCardType(deck, "orc"));
	}

	[Test]
	public void DeckContainsCardType_NullOrEmptyInput_ReturnsFalse()
	{
		var deck = CreateDeck(CreateCard("imp"));
		Assert.IsFalse(UtilityFuncManagerScript.DeckContainsCardType(null, "imp"));
		Assert.IsFalse(UtilityFuncManagerScript.DeckContainsCardType(deck, ""));
		Assert.IsFalse(UtilityFuncManagerScript.DeckContainsCardType(deck, null));
	}
}
