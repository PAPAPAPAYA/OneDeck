using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using UnityEngine;
using UnityEngine.Events;
using Random = UnityEngine.Random;

public static class UtilityFuncManagerScript
{
	#region SINGLETON

	// public static UtilityFuncManagerScript me;
	//
	// private void Awake()
	// {
	// 	me = this;
	// }

	#endregion
	
	public static float ConvertV2ToAngle(Vector2 dir)
	{
		return Mathf.Atan2(dir.x, dir.y) * (180 / Mathf.PI);
	}

	// shuffle given list
	// Deterministic: reads from the Rng service channel instead of UnityEngine.Random.
	// Default Target = effect target picks; deck-order callers pass RngChannel.Deck explicitly.
	public static List<T> ShuffleList<T>(List<T> list, RngChannel channel = RngChannel.Target)
	{
		return Rng.Shuffle(channel, list);
	}

	// copy game object list
	public static void CopyGameObjectList(List<GameObject> from, List<GameObject> to, bool clearTargetList)
	{
		if (clearTargetList) to.Clear();
		foreach (var gO in from)
		{
			to.Add(gO);
		}
	}

	/// <summary>
	/// Count how many cards in a DeckSO occupy a deck-size slot.
	/// </summary>
	public static int CountSlotOccupyingCards(DeckSO deck)
	{
		return CountSlotOccupyingCards(deck, false);
	}

	/// <summary>
	/// Count how many cards in a DeckSO occupy a deck-size slot.
	/// When duplicatesShareSlot is true, cards sharing a non-empty cardTypeID count as a
	/// single slot (first copy takes the slot, further copies are free).
	/// Cards with a null/empty cardTypeID are never deduplicated.
	/// Slot-free cards (occupiesDeckSlot = false, e.g. utility passives) are never counted.
	/// </summary>
	public static int CountSlotOccupyingCards(DeckSO deck, bool duplicatesShareSlot)
	{
		if (deck == null || deck.deck == null) return 0;

		int count = 0;
		HashSet<string> countedTypeIDs = duplicatesShareSlot ? new HashSet<string>() : null;
		foreach (var card in deck.deck)
		{
			if (card == null) continue;
			var cardScript = card.GetComponent<CardScript>();
			if (cardScript == null || !cardScript.occupiesDeckSlot) continue;
			if (countedTypeIDs != null && !string.IsNullOrEmpty(cardScript.cardTypeID)
				&& !countedTypeIDs.Add(cardScript.cardTypeID))
			{
				continue; // duplicate copy: free
			}
			count++;
		}
		return count;
	}

	/// <summary>
	/// Check whether a DeckSO contains any card with the given cardTypeID.
	/// </summary>
	public static bool DeckContainsCardType(DeckSO deck, string cardTypeID)
	{
		if (deck == null || deck.deck == null || string.IsNullOrEmpty(cardTypeID)) return false;

		foreach (var card in deck.deck)
		{
			if (card == null) continue;
			var cardScript = card.GetComponent<CardScript>();
			if (cardScript != null && cardScript.cardTypeID == cardTypeID)
			{
				return true;
			}
		}
		return false;
	}

	// copy generic type list
	public static void CopyList<T>(List<T> from, List<T> to, bool clearTargetList)
	{
		if (clearTargetList) to.Clear();
		foreach (var gO in from)
		{
			to.Add(gO);
		}
	}

	/// <summary>
	/// Generates a random number following Gaussian (normal) distribution using Box-Muller transform.
	/// Deterministic: reads from the given Rng service channel.
	/// </summary>
	/// <param name="mean">Center of the distribution.</param>
	/// <param name="stdDev">Standard deviation (spread). Higher = more dispersed.</param>
	/// <param name="channel">Rng channel to consume from (deck-order callers pass RngChannel.Deck).</param>
	public static float GaussianRandom(float mean, float stdDev, RngChannel channel)
	{
		return Rng.Gaussian(channel, mean, stdDev);
	}

	// get a random point on a circle
	public static Vector3 RandomPointOnUnitCircle(float radius)
	{
		float angle = Random.Range(0f, Mathf.PI * 2);
		float x = Mathf.Sin(angle) * radius;
		float y = Mathf.Cos(angle) * radius;

		return new Vector3(x, y, 0);
	}
}