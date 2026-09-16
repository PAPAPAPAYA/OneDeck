using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Applies the designer-configured trigger order (CardTriggerOrderConfig) by reshaping the
/// card instantiation sequence inside CombatManager.GatherDecks.
/// Rationale: each GameEventListener registers in OnEnable and GameEvent.Raise iterates the
/// listener list backwards, so a card that instantiates later triggers earlier.
/// </summary>
public static class CardTriggerOrderManager
{
	private const string LogPrefix = "[CardTriggerOrder]";

	/// <summary>
	/// Returns a permutation of deck indices: the order in which the cards should be
	/// instantiated. Unconfigured cards keep their original relative order first, then
	/// configured cards follow in descending triggerOrder (later instantiation triggers
	/// earlier, so the smallest triggerOrder ends up triggering first among the group).
	/// </summary>
	public static int[] BuildInstantiationOrder(List<GameObject> deck, CardTriggerOrderConfig config)
	{
		int count = deck?.Count ?? 0;
		var order = new int[count];
		for (var i = 0; i < count; i++)
			order[i] = i;

		if (count == 0 || config == null || config.entries == null || config.entries.Length == 0)
			return order;

		var triggerOrderByTypeId = new Dictionary<string, int>();
		foreach (var entry in config.entries)
		{
			if (entry == null || string.IsNullOrEmpty(entry.cardTypeID))
				continue;
			if (triggerOrderByTypeId.ContainsKey(entry.cardTypeID))
			{
				Debug.LogWarning($"{LogPrefix} Duplicate entry for '{entry.cardTypeID}', keeping the first one.");
				continue;
			}
			triggerOrderByTypeId.Add(entry.cardTypeID, entry.triggerOrder);
		}

		if (triggerOrderByTypeId.Count == 0)
			return order;

		var unconfigured = new List<int>(count);
		var configured = new List<int>();
		var configuredOrderByIndex = new Dictionary<int, int>();
		for (var i = 0; i < count; i++)
		{
			var card = deck[i];
			var cardScript = card != null ? card.GetComponent<CardScript>() : null;
			if (cardScript == null || string.IsNullOrEmpty(cardScript.cardTypeID) ||
				!triggerOrderByTypeId.TryGetValue(cardScript.cardTypeID, out var triggerOrder))
			{
				unconfigured.Add(i);
				continue;
			}
			configured.Add(i);
			configuredOrderByIndex.Add(i, triggerOrder);
		}

		if (configured.Count == 0)
			return order;

		// Later instantiation triggers earlier: bigger triggerOrder must be instantiated first.
		// Ties (duplicate copies of the same cardTypeID) keep their original deck order.
		configured.Sort((a, b) =>
		{
			int byOrder = configuredOrderByIndex[b].CompareTo(configuredOrderByIndex[a]);
			return byOrder != 0 ? byOrder : a.CompareTo(b);
		});
		unconfigured.AddRange(configured);

		var matchedLog = new List<string>();
		for (var i = 0; i < count; i++)
		{
			var deckIndex = unconfigured[i];
			order[i] = deckIndex;
			if (configuredOrderByIndex.TryGetValue(deckIndex, out var triggerOrder))
				matchedLog.Add($"{deck[deckIndex].GetComponent<CardScript>().cardTypeID}(order {triggerOrder})");
		}

		Debug.Log($"{LogPrefix} Instantiation order set for {matchedLog.Count} configured card(s); " +
			"they trigger in reverse of this order: " + string.Join(", ", matchedLog));
		return order;
	}
}
