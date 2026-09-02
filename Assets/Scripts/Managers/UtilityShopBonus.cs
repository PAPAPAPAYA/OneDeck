using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure-static resolver for shop utility passives and baseline growth
/// (plans/plan-utility-passive-shop-pipeline-2026-08-31.md, step 2).
/// All shop-domain bonuses are DERIVED from the player deck composition on
/// demand - no accumulated state, so selling a utility card automatically
/// removes its bonus. The deck-size meter is the single documented exception:
/// it uses a run-persistent purchase counter owned by ShopManager.
/// Baseline growth formulas: payday = payCheck + incomePerSession * session;
/// hpMax = hpMaxOg + hpMaxPerSession * session + sum(HP cards);
/// deckSize = deckSizeOg + deckSizePerSession * session + slotPurchases, clamped [1, ceiling].
/// </summary>
public static class UtilityShopBonus
{
	// Baseline growth fallbacks; ShopManager serializes its own tunables and passes them in.
	public const int DefaultIncomePerSession = 2;
	public const int DefaultHpMaxPerSession = 2;
	public const int DefaultDeckSizePerSession = 1;
	public const int DefaultDeckSlotBasePrice = 4;
	public const int DefaultDeckSlotPriceStep = 2;

	/// <summary>Aggregated utility contribution of a deck. Recompute on every deck change.</summary>
	public class Bonus
	{
		public int paydayBonus;
		public int extraShopOptions;
		public int freeRerolls;
		public int hpMaxBonus;
		public float oddsBonusPercent;
		/// <summary>ODDS_1-style card held: the visit's FIRST board is always a utility board (pool permitting).</summary>
		public bool firstBoardUtilityForce;
		public float creatureWaveChancePercent;
		public float spellWaveChancePercent;
		public List<RerollDiscountSpec> rerollDiscounts = new List<RerollDiscountSpec>();
		public List<ReservedSlotSpec> reservedSlots = new List<ReservedSlotSpec>();
		public Dictionary<EnumStorage.Rarity, float> rarityWeightMults = new Dictionary<EnumStorage.Rarity, float>();
		public HashSet<string> ownedUtilityTypeIds = new HashSet<string>();
	}

	public class RerollDiscountSpec
	{
		public int goldOff;
		public int everyRerolls;
	}

	/// <summary>
	/// One guaranteed appearance slot. Cadence model: boardIndex counts boards generated this
	/// visit starting at 0 (initial board = 0). firstBoardOnly fires on board 0; otherwise the
	/// slot fires when boardIndex % everyBoards == everyBoards - 1 (everyBoards=1 = every board,
	/// everyBoards=3 = boards 2, 5, 8 ... so the initial board is never a cadence hit).
	/// </summary>
	public class ReservedSlotSpec
	{
		public EnumStorage.UtilityKind kind;
		public EnumStorage.Rarity rarity;
		public EnumStorage.Tag tag;
		public int everyBoards = 1;
		public bool firstBoardOnly;
	}

	/// <summary>
	/// Scans the deck and aggregates every utility passive's contribution.
	/// Only deck-resident passives count (isPassive + kind != None); their type ids
	/// feed ownedUtilityTypeIds for the shop-offer dedup.
	/// </summary>
	public static Bonus Compute(IEnumerable<GameObject> deck)
	{
		var bonus = new Bonus();
		if (deck == null) return bonus;
		foreach (var card in deck)
		{
			if (card == null) continue;
			var script = card.GetComponent<CardScript>();
			if (script == null || script.utilityKind == EnumStorage.UtilityKind.None) continue;
			if (!script.isPassive) continue;
			bonus.ownedUtilityTypeIds.Add(script.cardTypeID);
			switch (script.utilityKind)
			{
				case EnumStorage.UtilityKind.HpMax:
					bonus.hpMaxBonus += script.utilityValue;
					break;
				case EnumStorage.UtilityKind.Income:
					bonus.paydayBonus += script.utilityValue;
					break;
				case EnumStorage.UtilityKind.ShopOption:
					bonus.extraShopOptions += script.utilityValue;
					break;
				case EnumStorage.UtilityKind.FreeReroll:
					bonus.freeRerolls += script.utilityValue;
					break;
				case EnumStorage.UtilityKind.RaritySlotU:
				case EnumStorage.UtilityKind.RaritySlotR:
					bonus.reservedSlots.Add(BuildRaritySlotSpec(script));
					break;
				case EnumStorage.UtilityKind.RarityWeight:
					MergeRarityWeightMults(bonus, script);
					break;
				case EnumStorage.UtilityKind.RerollDiscount:
					bonus.rerollDiscounts.Add(new RerollDiscountSpec
					{
						goldOff = script.utilityValue,
						everyRerolls = Mathf.Max(1, script.utilityValue2),
					});
					break;
				case EnumStorage.UtilityKind.OddsUtility:
					// utilityValue2 > 0 marks the ODDS_1 form: force the visit's first board to be a
					// utility board (utilityValue then unused); v2 = 0 is the plain +% chance form.
					if (script.utilityValue2 > 0)
					{
						bonus.firstBoardUtilityForce = true;
					}
					else
					{
						bonus.oddsBonusPercent += script.utilityValue;
					}
					break;
				case EnumStorage.UtilityKind.ReservedTag:
					bonus.reservedSlots.Add(new ReservedSlotSpec
					{
						kind = EnumStorage.UtilityKind.ReservedTag,
						tag = script.reservedTag,
						everyBoards = Mathf.Max(1, script.utilityValue2),
					});
					break;
				case EnumStorage.UtilityKind.RerollCreatureWave:
					bonus.creatureWaveChancePercent += script.utilityValue;
					break;
				case EnumStorage.UtilityKind.RerollSpellWave:
					bonus.spellWaveChancePercent += script.utilityValue;
					break;
			}
		}
		return bonus;
	}

	private static ReservedSlotSpec BuildRaritySlotSpec(CardScript script)
	{
		var spec = new ReservedSlotSpec
		{
			kind = script.utilityKind,
			// Guaranteed rarity comes from the KIND, not the utility card's own rarity:
			// RaritySlotU guarantees Uncommon offers, RaritySlotR guarantees Rare offers.
			rarity = script.utilityKind == EnumStorage.UtilityKind.RaritySlotR
				? EnumStorage.Rarity.Rare
				: EnumStorage.Rarity.Uncommon,
			everyBoards = Mathf.Max(1, script.utilityValue2),
		};
		// RaritySlotU with utilityValue2 <= 0 is the first-board-only C-tier entry card;
		// RaritySlotR always uses the cadence (the initial board is never a cadence hit).
		if (script.utilityKind == EnumStorage.UtilityKind.RaritySlotU && script.utilityValue2 <= 0)
		{
			spec.firstBoardOnly = true;
		}
		return spec;
	}

	private static void MergeRarityWeightMults(Bonus bonus, CardScript script)
	{
		if (script.utilityRarityWeightMults == null) return;
		foreach (var entry in script.utilityRarityWeightMults)
		{
			if (entry == null || entry.mult <= 0f) continue;
			float current;
			bonus.rarityWeightMults.TryGetValue(entry.rarity, out current);
			bonus.rarityWeightMults[entry.rarity] = (current <= 0f ? 1f : current) * entry.mult;
		}
	}

	public static int ComputePayday(int payCheckBase, int sessionNum, int incomePerSession, Bonus bonus)
	{
		return payCheckBase + Mathf.Max(0, incomePerSession) * Mathf.Max(0, sessionNum) + (bonus != null ? bonus.paydayBonus : 0);
	}

	public static int ComputeHpMax(int hpMaxOg, int sessionNum, int hpMaxPerSession, Bonus bonus)
	{
		return hpMaxOg + Mathf.Max(0, hpMaxPerSession) * Mathf.Max(0, sessionNum) + (bonus != null ? bonus.hpMaxBonus : 0);
	}

	public static int ComputeDeckSize(int deckSizeOg, int sessionNum, int deckSizePerSession, int slotPurchases, int ceiling)
	{
		int value = deckSizeOg + Mathf.Max(0, deckSizePerSession) * Mathf.Max(0, sessionNum) + Mathf.Max(0, slotPurchases);
		return Mathf.Clamp(value, 1, Mathf.Max(1, ceiling));
	}

	public static int GetDeckSlotPrice(int basePrice, int priceStepPerPurchase, int purchasesAlreadyMade)
	{
		return basePrice + Mathf.Max(0, priceStepPerPurchase) * Mathf.Max(0, purchasesAlreadyMade);
	}
}
