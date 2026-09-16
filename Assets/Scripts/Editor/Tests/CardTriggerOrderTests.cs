using System.Collections.Generic;
using DefaultNamespace;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode tests for CardTriggerOrderManager (feature added 2026-09-15).
/// Covers the instantiation permutation semantics and the reverse-instantiation trigger
/// order of GameEvent that the feature relies on.
/// </summary>
public class CardTriggerOrderTests
{
	private readonly List<Object> _created = new List<Object>();

	[TearDown]
	public void TearDown()
	{
		foreach (var obj in _created)
		{
			if (obj != null)
				Object.DestroyImmediate(obj);
		}
		_created.Clear();
	}

	private GameObject CreateDeckCard(string cardTypeId)
	{
		var go = new GameObject(cardTypeId);
		var cardScript = go.AddComponent<CardScript>();
		cardScript.cardTypeID = cardTypeId;
		_created.Add(go);
		return go;
	}

	private CardTriggerOrderConfig CreateConfig(params (string typeId, int order)[] entries)
	{
		var config = ScriptableObject.CreateInstance<CardTriggerOrderConfig>();
		var list = new List<CardTriggerOrderConfig.Entry>();
		foreach (var (typeId, order) in entries)
			list.Add(new CardTriggerOrderConfig.Entry { cardTypeID = typeId, triggerOrder = order });
		config.entries = list.ToArray();
		_created.Add(config);
		return config;
	}

	[Test]
	public void BuildInstantiationOrder_NullOrEmptyConfig_ReturnsOriginalOrder()
	{
		var deck = new List<GameObject> { CreateDeckCard("A"), CreateDeckCard("B") };

		CollectionAssert.AreEqual(new[] { 0, 1 }, CardTriggerOrderManager.BuildInstantiationOrder(deck, null));

		var emptyConfig = CreateConfig();
		CollectionAssert.AreEqual(new[] { 0, 1 }, CardTriggerOrderManager.BuildInstantiationOrder(deck, emptyConfig));
	}

	[Test]
	public void BuildInstantiationOrder_ConfiguredCardsInstantiateAfterUnconfigured_InDescendingTriggerOrder()
	{
		// Deck order: TRAINER(0), FILLER(1), BANNER(2). BANNER has the bigger triggerOrder so it
		// must instantiate earlier than TRAINER (later instantiation triggers earlier), and both
		// must instantiate after the unconfigured FILLER.
		var deck = new List<GameObject>
		{
			CreateDeckCard("RELIC_TRAINER"),
			CreateDeckCard("FILLER"),
			CreateDeckCard("RELIC_WHITE_BANNER"),
		};
		var config = CreateConfig(("RELIC_WHITE_BANNER", 20), ("RELIC_TRAINER", 10));

		CollectionAssert.AreEqual(new[] { 1, 2, 0 }, CardTriggerOrderManager.BuildInstantiationOrder(deck, config));
	}

	[Test]
	public void BuildInstantiationOrder_DuplicateCopiesKeepRelativeDeckOrder()
	{
		var deck = new List<GameObject>
		{
			CreateDeckCard("SOME_CARD"),
			CreateDeckCard("SOME_CARD"),
			CreateDeckCard("FILLER"),
			CreateDeckCard("OTHER_CARD"),
		};
		var config = CreateConfig(("OTHER_CARD", 20), ("SOME_CARD", 10));

		// FILLER first; then OTHER_CARD(20) before the two SOME_CARD copies (tie keeps deck order).
		CollectionAssert.AreEqual(new[] { 2, 3, 0, 1 }, CardTriggerOrderManager.BuildInstantiationOrder(deck, config));
	}

	[Test]
	public void BuildInstantiationOrder_EntryForCardNotInDeck_ChangesNothing()
	{
		var deck = new List<GameObject> { CreateDeckCard("A"), CreateDeckCard("B") };
		var config = CreateConfig(("NOT_IN_DECK", 10));

		CollectionAssert.AreEqual(new[] { 0, 1 }, CardTriggerOrderManager.BuildInstantiationOrder(deck, config));
	}

	[Test]
	public void GameEvent_TriggersInReverseInstantiationOrder()
	{
		var gameEvent = ScriptableObject.CreateInstance<GameEvent>();
		_created.Add(gameEvent);
		var raised = new List<string>();

		foreach (var cardName in new[] { "BANNER", "TRAINER" })
		{
			var go = new GameObject(cardName);
			var listener = go.AddComponent<GameEventListener>();
			listener.@event = gameEvent;
			string captured = cardName;
			listener.response.AddListener(() => raised.Add(captured));
			// EditMode does not run OnEnable; register manually so each listener registers exactly once.
			gameEvent.RegisterListener(listener);
			_created.Add(go);
		}

		gameEvent.Raise();

		// BANNER registered first, TRAINER second; Raise iterates backwards, so TRAINER fires first.
		CollectionAssert.AreEqual(new[] { "TRAINER", "BANNER" }, raised);
	}
}
