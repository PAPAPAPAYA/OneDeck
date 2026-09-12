using System;
using System.Collections.Generic;
using DefaultNamespace.Managers;
using UnityEngine;

	/// <summary>
	/// Pure-static shop board generation pipeline (plans/plan-utility-passive-shop-pipeline-2026-08-31.md, step 3;
	/// probability + mixed-pool rework 2026-09-11: plans/plan-shop-utility-probability-mixedpool-2026-09-11.md).
	/// Split mode (mixedPool = false): 0 board-type roll (combat vs utility board; session table chance;
	/// skipped - always combat - when the classified utility pool is empty) -> 2 wave filters
	/// (creature/spell, combat board generic slots only) -> 3 weighted generic rolls (weight
	/// delegate supplied by ShopManager). Mixed mode (mixedPool = true, ShopManager default): no
	/// board-type roll - utility and combat cards share one merged pool for the generic slots and
	/// utilitySlots is ignored. Reserved guarantee slots are appended on top and never displace
	/// generic slots; each fires via an independent per-board chance roll (no cadence, no pity)
	/// and bypasses rarity weights.
	/// Purity rules (2026-09-02 ruling, split mode only): owned utility type ids are excluded from
	/// both pools AND reserved candidates; reserved candidates come from the CLASSIFIED board pool;
	/// deck-size cards are excluded everywhere once the deck-size ceiling is reached. Mixed mode
	/// draws reserved candidates from the merged pool (OddsUtility cards reference-deduped).
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

	/// <param name="stagedChancePercent">Session-table utility board chance; negative = no config, use DefaultUtilityBoardChancePercent. Ignored in mixed mode.</param>
	/// <param name="mixedPool">True = no board-type split: utility + combat cards share one merged pool (utilitySlots ignored).</param>
	/// <param name="deckSizeAtCeiling">True once deck size hit the static ceiling: deck-size meter cards stop being offered.</param>
	public static BoardResult GenerateBoard(
		IEnumerable<GameObject> fullPool,
		Func<CardScript, float> weightOf,
		UtilityShopBonus.Bonus bonus,
		int boardIndex,
		float stagedChancePercent,
		int combatSlots,
		int utilitySlots,
		bool mixedPool,
		bool deckSizeAtCeiling,
		System.Random rng)
	{
		if (rng == null) rng = new System.Random();

		var combatPool = new List<GameObject>();
		var utilityPool = new List<GameObject>();
		ClassifyPools(fullPool, bonus, deckSizeAtCeiling, combatPool, utilityPool);

		var result = new BoardResult();
		// Wave filters: combat board generic slots only in split mode; merged pool in mixed
		// mode. Reserved slots are unaffected by waves either way.
		List<GameObject> mergedPool = null;
		List<GameObject> genericPool;
		if (mixedPool)
		{
			mergedPool = MergePools(combatPool, utilityPool);
			genericPool = bonus != null ? ApplyWaveFilters(mergedPool, bonus, rng) : mergedPool;
		}
		else
		{
			float chance = stagedChancePercent < 0f ? DefaultUtilityBoardChancePercent : stagedChancePercent;
			chance = Mathf.Clamp(chance, 0f, 100f);
			// A utility board requires a non-empty utility pool. The pool typically runs dry when
			// every utility passive is owned (own-once rule) AND the deck-size card is ceiling-excluded
			// - from then on the board-type roll falls through to combat so no visit wastes slots
			// on a blank utility board.
			result.isUtilityBoard = utilityPool.Count > 0 && rng.NextDouble() * 100.0 < chance;

			genericPool = result.isUtilityBoard ? utilityPool : combatPool;
			if (!result.isUtilityBoard && bonus != null)
			{
				genericPool = ApplyWaveFilters(genericPool, bonus, rng);
			}
		}

		// Weighted generic rolls. An empty pool yields fewer cards, never a crash.
		int genericSlotCount = mixedPool ? combatSlots : (result.isUtilityBoard ? utilitySlots : combatSlots);
		for (int i = 0; i < genericSlotCount; i++)
		{
			var card = RollWeighted(genericPool, weightOf, rng);
			if (card != null) result.cards.Add(card);
		}

		// Reserved guarantee slots, appended last so they never displace generic slots.
		// Rarity/tag candidates come from the CLASSIFIED board pool (board purity, 2026-09-02
		// ruling); utility-card promises (ODDS forms) always draw from the utility pool; mixed
		// mode draws everything from the merged pool.
		if (bonus != null && bonus.reservedSlots != null)
		{
			foreach (var spec in bonus.reservedSlots)
			{
				if (spec == null || !ReservedSlotFires(spec, boardIndex, rng)) continue;
				// Utility-card promises (ODDS forms) draw from the utility pool on any board -
				// the combat pool only holds OddsUtility-kind cards, which would starve the
				// guarantee. Everything else keeps the classified board pool (board purity).
				List<GameObject> specPool = mixedPool
					? mergedPool
					: (spec.wantsUtilityCard ? utilityPool : (result.isUtilityBoard ? utilityPool : combatPool));
				var candidate = RollReservedCandidate(specPool, spec, rng);
				// Utility-board drought (small pool; utility passives are own-once): don't force
				// the rarity/tag - fall back to a normal weighted roll of any utility card.
				// Combat and mixed boards keep skip-on-empty (no utility board to backfill from).
				if (candidate == null && !mixedPool && result.isUtilityBoard)
				{
					candidate = RollWeighted(utilityPool, weightOf, rng);
				}
				if (candidate != null) result.cards.Add(candidate);
			}
		}

		// DIAG-LOG(2026-09-06): board-type roll + pool classification trace
		// (report: deck-size card allegedly offered on a combat board).
		string combatAnomalies = "";
		foreach (var card in combatPool)
		{
			var script = card.GetComponent<CardScript>();
			if (script != null && ((script.utilityKind != EnumStorage.UtilityKind.None && script.utilityKind != EnumStorage.UtilityKind.OddsUtility) || card.GetComponentInChildren<DeckSizeIncreaseEffect>(true) != null))
			{
				combatAnomalies += script.cardTypeID + " ";
			}
		}
		var boardIdList = new List<string>();
		foreach (var card in result.cards)
		{
			var boardScript = card != null ? card.GetComponent<CardScript>() : null;
			boardIdList.Add(boardScript != null ? boardScript.cardTypeID : "null");
		}
		int fullPoolCount = (fullPool as System.Collections.ICollection)?.Count ?? -1;
		TestManager.Log("[ShopBoard] board#" + boardIndex
			+ " type=" + (result.isUtilityBoard ? "UTILITY" : "COMBAT")
			+ (mixedPool
				? " MIXED"
				: " utilityChance=" + Mathf.Clamp(stagedChancePercent < 0f ? DefaultUtilityBoardChancePercent : stagedChancePercent, 0f, 100f).ToString("F0") + "%")
			+ " fullPool=" + fullPoolCount
			+ " combatPool=" + combatPool.Count + " utilityPool=" + utilityPool.Count
			+ " genericPool=" + genericPool.Count
			+ (combatAnomalies.Length > 0 ? " ANOMALY_IN_COMBAT_POOL=[" + combatAnomalies.TrimEnd() + "]" : " combatPoolClean")
			+ " board=[" + string.Join(", ", boardIdList) + "]");

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
			// The deck-size effect may live on a child GameObject (IncreaseDeckSizeLite prefab):
			// a root-only GetComponent misses it and misclassifies the card into the combat pool.
			bool isDeckSlotCard = script.GetComponentInChildren<DeckSizeIncreaseEffect>(true) != null;
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
	/// Mixed-pool merge of the classified pools. OddsUtility cards sit in BOTH pools by
	/// classification (board exemption), so the merge reference-dedups to keep their roll
	/// weight from doubling.
	/// </summary>
	private static List<GameObject> MergePools(List<GameObject> combatPool, List<GameObject> utilityPool)
	{
		var merged = new List<GameObject>(combatPool);
		var seen = new HashSet<GameObject>(combatPool);
		foreach (var card in utilityPool)
		{
			if (seen.Add(card)) merged.Add(card);
		}
		return merged;
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
	/// Probability model (matches UtilityShopBonus.ReservedSlotSpec docs): every generated board
	/// rolls chancePercent once, independently - no cadence, no pity. firstBoardOnly fires on
	/// board 0 only and skips the roll (ODDS deep form).
	/// </summary>
	private static bool ReservedSlotFires(UtilityShopBonus.ReservedSlotSpec spec, int boardIndex, System.Random rng)
	{
		if (spec.firstBoardOnly) return boardIndex == 0;
		return rng.NextDouble() * 100.0 < UtilityShopBonus.ChanceOrPercent(spec.chancePercent);
	}

	/// <summary>
	/// Reserved candidates come from the already-classified board pool (owned-utility dedup and
	/// the deck-size ceiling exclusion are baked into the classification), by predicate: rarity
	/// slots by spec.rarity, tag slots by myTags, ODDS-style slots (wantsUtilityCard) by
	/// utilityKind. Weights are bypassed - a guarantee is not a weighted roll. No matching
	/// candidate -> null (caller decides skip vs utility fallback).
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
				bool matches = spec.wantsUtilityCard
					? script.utilityKind != EnumStorage.UtilityKind.None
					: spec.kind == EnumStorage.UtilityKind.ReservedTag
						? script.myTags != null && script.myTags.Contains(spec.tag)
						: script.rarity == spec.rarity;
				if (matches) candidates.Add(card);
			}
		}
		if (candidates.Count == 0) return null;
		return candidates[rng.Next(candidates.Count)];
	}
}
