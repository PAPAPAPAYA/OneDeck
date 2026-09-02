using System;
using System.Collections.Generic;
using UnityEngine;

	/// <summary>
	/// Pure-static shop board generation pipeline (plans/plan-utility-passive-shop-pipeline-2026-08-31.md, step 3).
	/// Stages: 0 board-type roll (combat vs utility board; session table chance + OddsUtility bonus;
	/// skipped - always combat - when the classified utility pool is empty) -> 2 wave filters
	/// (creature/spell, combat board generic slots only) -> 3 weighted generic rolls (weight
	/// delegate supplied by ShopManager). Reserved guarantee slots are appended on top and never
	/// displace generic slots; they fire regardless of board type and bypass rarity weights.
	/// Purity rules (2026-09-02 ruling): owned utility type ids are excluded from both pools AND
	/// reserved candidates; reserved candidates come from the CLASSIFIED board pool, so combat
	/// boards only ever show combat cards and utility boards only ever show utility cards; deck-size
	/// cards are excluded everywhere once the deck-size ceiling is reached.
	/// Determinism for tests: all randomness flows through the injected System.Random; chance 0/100
	/// and wave 0/100 produce fully deterministic boards.
	/// </summary>
public static class ShopBoardPipeline
{
	/// <summary>
	/// Built-in utility board chance fallback when the caller has no session entry configured.
	/// Scene deserialization wipes ShopManager's list field initializer, so the fallback must
	/// live here rather than on the manager (a missing table silently means 10%).
	/// </summary>
	public const float DefaultUtilityBoardChancePercent = 10f;

	public class BoardResult
	{
		public List<GameObject> cards = new List<GameObject>();
		public bool isUtilityBoard;
	}

	/// <param name="stagedChancePercent">Session-table utility board chance; negative = no config, use DefaultUtilityBoardChancePercent.</param>
	/// <param name="deckSizeAtCeiling">True once deck size hit the static ceiling: deck-size meter cards stop being offered.</param>
	public static BoardResult GenerateBoard(
		IEnumerable<GameObject> fullPool,
		Func<CardScript, float> weightOf,
		UtilityShopBonus.Bonus bonus,
		int boardIndex,
		float stagedChancePercent,
		int combatSlots,
		int utilitySlots,
		bool deckSizeAtCeiling,
		System.Random rng)
	{
		if (rng == null) rng = new System.Random();

		var combatPool = new List<GameObject>();
		var utilityPool = new List<GameObject>();
		ClassifyPools(fullPool, bonus, deckSizeAtCeiling, combatPool, utilityPool);

		var result = new BoardResult();
		float chance = stagedChancePercent < 0f ? DefaultUtilityBoardChancePercent : stagedChancePercent;
		if (bonus != null) chance += bonus.oddsBonusPercent;
		chance = Mathf.Clamp(chance, 0f, 100f);
		// ODDS_1-style force: the visit's first board is always a utility board (pool permitting).
		bool forcedUtility = bonus != null && bonus.firstBoardUtilityForce && boardIndex == 0;
		// A utility board requires a non-empty utility pool. The pool typically runs dry when
		// every utility passive is owned (own-once rule) AND the deck-size card is ceiling-excluded
		// - from then on the board-type roll falls through to combat so no visit wastes slots
		// on a blank utility board.
		result.isUtilityBoard = utilityPool.Count > 0 && (forcedUtility || rng.NextDouble() * 100.0 < chance);

		// Wave filters: combat board generic slots only; reserved slots unaffected.
		List<GameObject> genericPool = result.isUtilityBoard ? utilityPool : combatPool;
		if (!result.isUtilityBoard && bonus != null)
		{
			genericPool = ApplyWaveFilters(genericPool, bonus, rng);
		}

		// Weighted generic rolls. An empty pool yields fewer cards, never a crash.
		int genericSlotCount = result.isUtilityBoard ? utilitySlots : combatSlots;
		for (int i = 0; i < genericSlotCount; i++)
		{
			var card = RollWeighted(genericPool, weightOf, rng);
			if (card != null) result.cards.Add(card);
		}

		// Reserved guarantee slots, appended last so they never displace generic slots.
		// Candidates come from the CLASSIFIED board pool (board purity, 2026-09-02 ruling):
		// combat boards guarantee combat cards, utility boards guarantee utility cards.
		if (bonus != null && bonus.reservedSlots != null)
		{
			List<GameObject> reservedPool = result.isUtilityBoard ? utilityPool : combatPool;
			foreach (var spec in bonus.reservedSlots)
			{
				if (spec == null || !ReservedSlotFires(spec, boardIndex)) continue;
				var candidate = RollReservedCandidate(reservedPool, spec, rng);
				// Utility-board drought (small pool; utility passives are own-once): don't force
				// the rarity/tag - fall back to a normal weighted roll of any utility card.
				// Combat boards keep skip-on-empty (their pools never realistically run dry).
				if (candidate == null && result.isUtilityBoard)
				{
					candidate = RollWeighted(utilityPool, weightOf, rng);
				}
				if (candidate != null) result.cards.Add(candidate);
			}
		}
		return result;
	}

	/// <summary>
	/// Board-split classification: utility kinds (except OddsUtility) and deck-size cards are
	/// utility-board-only; OddsUtility is exempt and may appear on both boards; everything else
	/// is combat-pool. Owned utility type ids are removed before classification, so they leave
	/// both pools.
	/// </summary>
	private static void ClassifyPools(IEnumerable<GameObject> fullPool, UtilityShopBonus.Bonus bonus, bool deckSizeAtCeiling, List<GameObject> combatPool, List<GameObject> utilityPool)
	{
		if (fullPool == null) return;
		foreach (var card in fullPool)
		{
			if (card == null) continue;
			var script = card.GetComponent<CardScript>();
			if (script == null) continue;
			bool isDeckSlotCard = script.GetComponent<DeckSizeIncreaseEffect>() != null;
			if (deckSizeAtCeiling && isDeckSlotCard) continue;
			if (bonus != null && bonus.ownedUtilityTypeIds != null && bonus.ownedUtilityTypeIds.Contains(script.cardTypeID)) continue;

			bool utilityOnly = (script.utilityKind != EnumStorage.UtilityKind.None && script.utilityKind != EnumStorage.UtilityKind.OddsUtility) || isDeckSlotCard;
			if (utilityOnly)
			{
				utilityPool.Add(card);
			}
			else
			{
				combatPool.Add(card);
				if (script.utilityKind == EnumStorage.UtilityKind.OddsUtility) utilityPool.Add(card);
			}
		}
	}

	/// <summary>
	/// Creature/spell wave roll. Both families held: creature is judged first and wins on a hit;
	/// the spell wave is only rolled on a creature miss. An empty filtered pool falls back to the
	/// unfiltered pool so a wave can never blank the shop.
	/// </summary>
	private static List<GameObject> ApplyWaveFilters(List<GameObject> combatPool, UtilityShopBonus.Bonus bonus, System.Random rng)
	{
		if (combatPool.Count == 0) return combatPool;
		if (rng.NextDouble() * 100.0 < Mathf.Clamp(bonus.creatureWaveChancePercent, 0f, 100f))
		{
			var creatures = combatPool.FindAll(c => c.GetComponent<CardScript>().IsCreature);
			return creatures.Count > 0 ? creatures : combatPool;
		}
		if (rng.NextDouble() * 100.0 < Mathf.Clamp(bonus.spellWaveChancePercent, 0f, 100f))
		{
			var nonCreatures = combatPool.FindAll(c => !c.GetComponent<CardScript>().IsCreature);
			return nonCreatures.Count > 0 ? nonCreatures : combatPool;
		}
		return combatPool;
	}

	private static GameObject RollWeighted(List<GameObject> pool, Func<CardScript, float> weightOf, System.Random rng)
	{
		if (pool == null || pool.Count == 0) return null;
		float total = 0f;
		foreach (var card in pool)
		{
			float w = GetWeight(card, weightOf);
			if (w > 0f) total += w;
		}
		if (total <= 0f) return null;

		double roll = rng.NextDouble() * total;
		float cumulative = 0f;
		GameObject lastPositive = null;
		foreach (var card in pool)
		{
			float w = GetWeight(card, weightOf);
			if (w <= 0f) continue;
			cumulative += w;
			lastPositive = card;
			if (roll < cumulative) return card;
		}
		return lastPositive; // floating point precision fallback
	}

	private static float GetWeight(GameObject card, Func<CardScript, float> weightOf)
	{
		var script = card.GetComponent<CardScript>();
		if (script == null) return 0f;
		return weightOf != null ? weightOf(script) : 1f;
	}

	/// <summary>
	/// Cadence model (matches UtilityShopBonus.ReservedSlotSpec docs): boardIndex counts boards
	/// generated this visit starting at 0 (initial board = 0). firstBoardOnly fires on board 0
	/// only; otherwise boardIndex % everyBoards == everyBoards - 1 (everyBoards=3 -> boards
	/// 2, 5, 8..., so the initial board is never a cadence hit).
	/// </summary>
	private static bool ReservedSlotFires(UtilityShopBonus.ReservedSlotSpec spec, int boardIndex)
	{
		if (spec.firstBoardOnly) return boardIndex == 0;
		int every = Mathf.Max(1, spec.everyBoards);
		return boardIndex % every == every - 1;
	}

	/// <summary>
	/// Reserved candidates come from the already-classified board pool (owned-utility dedup and
	/// the deck-size ceiling exclusion are baked into the classification), by predicate: rarity
	/// slots by spec.rarity, tag slots by myTags. Weights are bypassed - a guarantee is not a
	/// weighted roll. No matching candidate -> null (caller decides skip vs utility fallback).
	/// </summary>
	private static GameObject RollReservedCandidate(List<GameObject> boardPool, UtilityShopBonus.ReservedSlotSpec spec, System.Random rng)
	{
		if (spec.kind == EnumStorage.UtilityKind.ReservedTag && spec.tag == EnumStorage.Tag.None) return null; // misconfigured tag slot
		var candidates = new List<GameObject>();
		if (boardPool != null)
		{
			foreach (var card in boardPool)
			{
				var script = card.GetComponent<CardScript>();
				if (script == null) continue;
				bool matches = spec.kind == EnumStorage.UtilityKind.ReservedTag
					? script.myTags != null && script.myTags.Contains(spec.tag)
					: script.rarity == spec.rarity;
				if (matches) candidates.Add(card);
			}
		}
		if (candidates.Count == 0) return null;
		return candidates[rng.Next(candidates.Count)];
	}
}
