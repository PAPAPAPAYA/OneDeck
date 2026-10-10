using System.Collections.Generic;

/// <summary>
/// Key-card selection for the select-page candidate panels (plan
/// plan-opponent-select-page-2026-10-06 §4.3, 2026-10-10 ruling): from one ghost deck's
/// resolved cards — dedupe by cardTypeID, drop utility passives, then fill up to three
/// slots by rarity tier from Rare down. Random WITHIN a tier (RngChannel.Setup — the
/// TakeCandidate-matching-class random, plan §4.1), deterministic across tiers. Fewer
/// than three candidates total returns what exists (the panel hides empty slots).
/// Result order = display order: rarity descending, deck order within a tier.
/// </summary>
public static class OpponentKeyCardSelector
{
	public const int KeyCardCount = 3;

	/// <summary>Pure selection over resolved card scripts; returns display order.</summary>
	public static List<CardScript> Select(List<CardScript> deckCards, int count = KeyCardCount)
	{
		var result = new List<CardScript>();
		if (deckCards == null || deckCards.Count == 0 || count <= 0) return result;

		// Dedupe by cardTypeID (empty falls back to the GameObject name — DeckSaver's
		// GetCardTypeID rule) and drop utility passives: meter cards are not a deck's
		// signature.
		var seen = new HashSet<string>();
		var pool = new List<CardScript>();
		foreach (CardScript card in deckCards)
		{
			if (card == null || card.IsUtilityPassive) continue;
			string key = !string.IsNullOrEmpty(card.cardTypeID) ? card.cardTypeID : card.gameObject.name;
			if (!seen.Add(key)) continue;
			pool.Add(card);
		}

		// Rarity tiers from Rare down; the shuffle decides WHICH cards of a tier, the
		// tier loop decides the ORDER of filling — deterministic across tiers.
		for (int rarity = (int)EnumStorage.Rarity.Rare; rarity >= 0 && result.Count < count; rarity--)
		{
			List<CardScript> tier = pool.FindAll(c => c != null && (int)c.rarity == rarity);
			if (tier.Count == 0) continue;
			Rng.Shuffle(RngChannel.Setup, tier);
			foreach (CardScript card in tier)
			{
				if (result.Count >= count) break;
				result.Add(card);
			}
		}

		// Display order: rarity descending, deck order within a tier — the random pick
		// decides WHICH cards show, the deck order decides WHERE they sit. The comparator
		// is a total order, so the unstable List.Sort stays deterministic.
		result.Sort((a, b) =>
		{
			int byRarity = b.rarity.CompareTo(a.rarity);
			return byRarity != 0 ? byRarity : deckCards.IndexOf(a).CompareTo(deckCards.IndexOf(b));
		});
		return result;
	}
}
