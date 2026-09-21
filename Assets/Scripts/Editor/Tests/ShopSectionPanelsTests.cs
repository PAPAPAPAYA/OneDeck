using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode tests for the pure helpers of ShopSectionPanels (shop page panels port,
/// plans/plan-shop-panels-port-2026-09-18.md Task 4): content-bounds math, the
/// zero-padded deck slot counter format, and the deck-SO-backed occupied-slot count
/// feeding that counter (VISUAL-FIX 2026-09-21).
/// </summary>
public class ShopSectionPanelsTests
{
	private readonly System.Collections.Generic.List<Object> _cleanup = new System.Collections.Generic.List<Object>();

	[SetUp]
	public void SetUp()
	{
		// CardScript.OnEnable touches CardIDRetriever.Me (same rig as DuplicateSlotCountTests)
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

	[Test]
	public void ComputeContentBounds_SingleCenter_ExpandsByGivenHalfExtents()
	{
		var centers = new System.Collections.Generic.List<Vector3> { new Vector3(1f, 2f, 0f) };
		Bounds b = ShopSectionPanels.ComputeContentBounds(centers, 1.4f, 2.0f, 2.5f);
		// Assert the edges, which is what FitPanel consumes. The extents are deliberately
		// asymmetric (2.0 above / 2.5 below, the extra below being the price-button
		// allowance), so b.center sits 0.25 BELOW the input center — asserting center
		// == input center only holds for symmetric extents and is not the contract.
		Assert.AreEqual(1f - 1.4f, b.min.x, 0.001f);
		Assert.AreEqual(1f + 1.4f, b.max.x, 0.001f);
		Assert.AreEqual(2f - 2.5f, b.min.y, 0.001f);
		Assert.AreEqual(2f + 2.0f, b.max.y, 0.001f);
		Assert.AreEqual(2.8f, b.size.x, 0.001f);
		Assert.AreEqual(4.5f, b.size.y, 0.001f);
	}

	[Test]
	public void ComputeContentBounds_MultiRow_UsesExtremes()
	{
		var centers = new System.Collections.Generic.List<Vector3>
		{
			new Vector3(-3.2f, 0f, 0f), new Vector3(3.2f, 0f, 0f), new Vector3(0f, -4.5f, 0f)
		};
		Bounds b = ShopSectionPanels.ComputeContentBounds(centers, 1.4f, 2.0f, 2.5f);
		Assert.AreEqual(-3.2f - 1.4f, b.min.x, 0.001f);
		Assert.AreEqual(3.2f + 1.4f, b.max.x, 0.001f);
		Assert.AreEqual(0f + 2.0f, b.max.y, 0.001f);
		Assert.AreEqual(-4.5f - 2.5f, b.min.y, 0.001f);
	}

	[Test]
	public void ComputeContentBounds_EmptyList_ReturnsZeroBounds()
	{
		var centers = new System.Collections.Generic.List<Vector3>();
		Bounds b = ShopSectionPanels.ComputeContentBounds(centers, 1.4f, 2.0f, 2.5f);
		Assert.AreEqual(Vector3.zero, b.center);
		Assert.AreEqual(Vector3.zero, b.size);
	}

	[Test]
	public void FormatSlotCount_PadsToTwoDigits()
	{
		Assert.AreEqual("03/05", ShopSectionPanels.FormatSlotCount(3, 5));
		Assert.AreEqual("12/12", ShopSectionPanels.FormatSlotCount(12, 12));
	}

	// VISUAL-FIX(2026-09-21): the counter's used term must come from the deck SO. The old
	// formula subtracted the spawned empty slots (one recessed frame per grid slot = deckSize
	// of them), which pinned the display to 00/NN; a helper test keeps that regression class
	// (a visual slot list standing in for the deck's own numbers) from coming back.
	[Test]
	public void CountUsedSlots_CountsSlotOccupyingCardsOnly()
	{
		var deck = CreateDeck(CreateCard("imp"), CreateCard("hex"), CreateCard("charm", occupiesDeckSlot: false));
		Assert.AreEqual(2, ShopSectionPanels.CountUsedSlots(deck, false));
	}

	[Test]
	public void CountUsedSlots_DuplicateShareRule_CountsOneSlotPerTypeID()
	{
		var deck = CreateDeck(CreateCard("imp"), CreateCard("imp"), CreateCard("hex"));
		Assert.AreEqual(3, ShopSectionPanels.CountUsedSlots(deck, false));
		Assert.AreEqual(2, ShopSectionPanels.CountUsedSlots(deck, true));
	}

	[Test]
	public void CountUsedSlots_EmptyOrNullDeck_IsZero()
	{
		Assert.AreEqual(0, ShopSectionPanels.CountUsedSlots(CreateDeck(), false));
		Assert.AreEqual(0, ShopSectionPanels.CountUsedSlots(null, false));
	}

	[Test]
	public void FormatSlotCount_WithCountedSlots_ReadsOccupancyOverCapacity()
	{
		var deck = CreateDeck(CreateCard("imp"));
		Assert.AreEqual("01/03", ShopSectionPanels.FormatSlotCount(ShopSectionPanels.CountUsedSlots(deck, false), 3));
	}

	private GameObject CreateCard(string cardTypeID, bool occupiesDeckSlot = true)
	{
		var go = new GameObject("TestCard");
		var cardScript = go.AddComponent<CardScript>();
		cardScript.cardTypeID = cardTypeID;
		cardScript.occupiesDeckSlot = occupiesDeckSlot;
		_cleanup.Add(go);
		return go;
	}

	private DeckSO CreateDeck(params GameObject[] cards)
	{
		var deck = ScriptableObject.CreateInstance<DeckSO>();
		deck.deck = new System.Collections.Generic.List<GameObject>(cards);
		_cleanup.Add(deck);
		return deck;
	}
}
