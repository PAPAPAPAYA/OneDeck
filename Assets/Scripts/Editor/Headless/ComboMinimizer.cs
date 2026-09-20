using System.Collections.Generic;
using UnityEngine;

/// <summary>Outcome of the ddmin pass (plan-infinity-detection §4 "组合最小化").</summary>
public class MinimizeResult
{
	/// <summary>The minimized card set — still loops, and no single card can be removed.</summary>
	public List<GameObject> Cards = new List<GameObject>();
	public int RunsPerformed;
	/// <summary>True when the run budget ran out; the set is a candidate, not a proven minimum.</summary>
	public bool Truncated;
	/// <summary>True when removing any single remaining card stops the loop (1-minimal).</summary>
	public bool IsOneMinimal;
	/// <summary>False when the input deck did not loop on EVERY supplied seed — nothing ingested.</summary>
	public bool MultiSeedStable;

	/// <summary>
	/// Utility passives (and the HP-max meter card) that ddmin could not remove. They do nothing in
	/// combat — but they DO occupy slots in the arrangement hash, so a "1-minimal" set can keep one
	/// for structural reasons. Such a card must never reach a combo key: the key is a containment
	/// test, so a passive in it would hide every variant that runs the same loop with a different
	/// passive (2026-09-19 user ruling; measured on decks 110/111, where ddmin kept two).
	/// </summary>
	public List<GameObject> StrippedCards = new List<GameObject>();
	/// <summary>
	/// True when the passive-free remainder was re-verified to still loop (on every seed) and was
	/// adopted as the result. False with a non-empty StrippedCards means the passives turned out to
	/// be load-bearing and the ORIGINAL set was kept — evidence, not a silent strip.
	/// </summary>
	public bool StripVerified;

	public string CardIds()
	{
		var ids = new List<string>();
		foreach (var c in Cards)
		{
			var cs = c != null ? c.GetComponent<CardScript>() : null;
			ids.Add(cs != null ? cs.cardTypeID : "?");
		}
		return string.Join(",", ids.ToArray());
	}

	/// <summary>Card type ids of the stripped utility passives (empty when nothing was stripped).</summary>
	public string StrippedIds()
	{
		var ids = new List<string>();
		foreach (var c in StrippedCards)
		{
			var cs = c != null ? c.GetComponent<CardScript>() : null;
			ids.Add(cs != null ? cs.cardTypeID : "?");
		}
		return string.Join(",", ids.ToArray());
	}
}

/// <summary>
/// ddmin over a deck's card list (plan §4): repeatedly try to drop subsets of cards and keep the
/// result if the remainder STILL loops. Produces a 1-minimal set — removing any single remaining
/// card breaks the loop.
/// The loop predicate is deliberately multi-seed: §4 requires the minimal set to be robust across
/// seeds, because "only loops on one seed" is how a combinatorial coincidence (not a real combo)
/// gets ingested. Seeds are the caller's; the batch scan should pass several.
/// Cost: every predicate evaluation is a full headless combat per seed. The run budget exists
/// because a production ghost deck (30-60 cards) would otherwise burn hundreds of sims; when it
/// trips, the result is reported as Truncated and must not be ingested as a minimum.
/// </summary>
public static class ComboMinimizer
{
	public const int DefaultMaxRuns = 400;

	public static MinimizeResult Minimize(DeckSO deck, IList<int> seeds, int dummySize, int dummyHp,
		RunBudgetSim.Options options = null, int maxRuns = DefaultMaxRuns)
	{
		var result = new MinimizeResult();
		if (deck == null || deck.deck == null) return result;

		var all = new List<GameObject>(deck.deck);
		if (seeds == null || seeds.Count == 0) seeds = new List<int> { 0 };

		int runs = 0;
		result.MultiSeedStable = LoopsOnAllSeeds(all, seeds, dummySize, dummyHp, options, ref runs);
		if (!result.MultiSeedStable)
		{
			// The full deck is not robustly infinite, so there is no minimal core to extract.
			result.Cards = all;
			result.RunsPerformed = runs;
			return result;
		}

		var current = all;
		int granularity = 2;
		bool truncated = false;

		while (current.Count >= 2)
		{
			bool reduced = false;
			var chunks = Chunk(current, granularity);

			foreach (var complement in Complements(chunks))
			{
				if (complement.Count == 0) continue;
				if (runs >= maxRuns) { truncated = true; break; }
				if (LoopsOnAllSeeds(complement, seeds, dummySize, dummyHp, options, ref runs))
				{
					current = complement;
					granularity = Mathf.Max(granularity - 1, 2);
					reduced = true;
					break;
				}
			}

			if (truncated) break;
			if (reduced) continue;
			if (granularity >= current.Count) break;
			granularity = Mathf.Min(granularity * 2, current.Count);
		}

		result.Cards = current;
		result.Truncated = truncated;
		result.RunsPerformed = runs;

		// Drop utility passives before claiming a minimum (2026-09-19 ruling). ddmin measures the
		// ARRANGEMENT, and a passive changes the arrangement without doing anything — so it can
		// survive on structure alone. Verify the remainder first: if the loop dies without them they
		// were load-bearing (or the measurement is structure-sensitive) and the original set stands,
		// recorded as evidence rather than silently trimmed.
		if (!truncated)
		{
			var stripped = new List<GameObject>();
			var remainder = new List<GameObject>();
			foreach (var card in current)
			{
				var cs = card != null ? card.GetComponent<CardScript>() : null;
				if (cs != null && cs.IsUtilityPassive) stripped.Add(card);
				else remainder.Add(card);
			}
			if (stripped.Count > 0 && remainder.Count > 0)
			{
				result.StrippedCards = stripped;
				if (runs < maxRuns && LoopsOnAllSeeds(remainder, seeds, dummySize, dummyHp, options, ref runs))
				{
					result.StripVerified = true;
					current = remainder;
					granularity = 2;
				}
			}
			else if (stripped.Count > 0)
			{
				// Every remaining card is a passive: no combo to register at all.
				result.StrippedCards = stripped;
			}
		}

		result.Cards = current;

		// 1-minimality check: with the set this small it is cheap, and it is the property the
		// combo library actually relies on ("these cards, no fewer").
		if (!truncated)
		{
			result.IsOneMinimal = true;
			foreach (var card in current)
			{
				if (runs >= maxRuns) { result.Truncated = true; result.IsOneMinimal = false; break; }
				var probe = new List<GameObject>(current);
				probe.Remove(card);
				if (probe.Count == 0) { result.IsOneMinimal = false; break; }
				if (LoopsOnAllSeeds(probe, seeds, dummySize, dummyHp, options, ref runs))
				{
					result.IsOneMinimal = false;
					break;
				}
			}
		}

		result.RunsPerformed = runs;
		return result;
	}

	private static bool LoopsOnAllSeeds(List<GameObject> cards, IList<int> seeds,
		int dummySize, int dummyHp, RunBudgetSim.Options options, ref int runs)
	{
		if (cards == null || cards.Count == 0) return false;

		var temp = ScriptableObject.CreateInstance<DeckSO>();
		temp.name = "minimizer-candidate";
		temp.deck = new List<GameObject>(cards);
		try
		{
			foreach (var seed in seeds)
			{
				runs++;
				if (!RunBudgetSim.RunVsDummy(temp, dummySize, dummyHp, seed, options).SuspectedInfinite)
				{
					return false;
				}
			}
			return true;
		}
		finally
		{
			Object.DestroyImmediate(temp);
		}
	}

	private static List<List<GameObject>> Chunk(List<GameObject> cards, int n)
	{
		int chunkSize = (cards.Count + n - 1) / n;
		if (chunkSize < 1) chunkSize = 1;

		var chunks = new List<List<GameObject>>();
		for (int i = 0; i < cards.Count; i += chunkSize)
		{
			var chunk = new List<GameObject>();
			for (int j = i; j < i + chunkSize && j < cards.Count; j++) chunk.Add(cards[j]);
			if (chunk.Count > 0) chunks.Add(chunk);
		}
		return chunks;
	}

	/// <summary>Every "all chunks except one" candidate ddmin tries removing.</summary>
	private static List<List<GameObject>> Complements(List<List<GameObject>> chunks)
	{
		var result = new List<List<GameObject>>();
		for (int skip = 0; skip < chunks.Count; skip++)
		{
			var complement = new List<GameObject>();
			for (int i = 0; i < chunks.Count; i++)
			{
				if (i == skip) continue;
				complement.AddRange(chunks[i]);
			}
			result.Add(complement);
		}
		return result;
	}
}
