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
/// Baseline growth formulas (step growth: a step lands at session N, 2N, ...;
/// sessionsPerStep = 1 degenerates to the legacy every-session slope):
/// payday = payCheck + incomeGrowthPerStep * (session / sessionsPerIncomeStep);
/// hpMax = hpMaxOg + hpMaxGrowthPerStep * (session / sessionsPerHpMaxStep) + sum(HP cards);
/// deckSize = deckSizeOg + deckSizeGrowthPerStep * (session / sessionsPerDeckSizeStep) + slotPurchases, clamped [1, ceiling].
/// </summary>
public static class UtilityShopBonus
{
	/// <summary>
	/// Step growth: growthPerStep applied every sessionsPerGrowth sessions (integer floor).
	/// session 0..N-1 = 0 steps, N..2N-1 = 1 step, etc. Non-positive growth = 0;
	/// non-positive interval clamps to 1 (every-session slope).
	/// </summary>
	public static int StepGrowth(int session, int growthPerStep, int sessionsPerGrowth)
	{
		if (growthPerStep <= 0) return 0;
		return growthPerStep * (Mathf.Max(0, session) / Mathf.Max(1, sessionsPerGrowth));
	}

	/// <summary>Aggregated utility contribution of a deck. Recompute on every deck change.</summary>
	public class Bonus
	{
		public int paydayBonus;
		public int extraShopOptions;
		public int freeRerolls;
		public int hpMaxBonus;
		public float creatureWaveChancePercent;
		public float spellWaveChancePercent;
		public List<BoardDiscountSpec> boardDiscounts = new List<BoardDiscountSpec>();
		public List<ReservedSlotSpec> reservedSlots = new List<ReservedSlotSpec>();
		public Dictionary<EnumStorage.Rarity, float> rarityWeightMults = new Dictionary<EnumStorage.Rarity, float>();
		public HashSet<string> ownedUtilityTypeIds = new HashSet<string>();
	}

	public class BoardDiscountSpec
	{
		/// <summary>Independent roll per generated board (initial board + every reroll); 0 or less = DefaultReservedChancePercent.</summary>
		public int chancePercent;
		/// <summary>Percent of the card's price taken off; half price = 50. Sibling specs stack, clamped at 100.</summary>
		public int percentOff;
	}

	/// <summary>Fallback roll chance when a spec's chancePercent is unconfigured (0 or less).</summary>
	public const int DefaultReservedChancePercent = 25;

	/// <summary>
	/// One guaranteed appearance slot. Probability model (2026-09-11 rework): every generated
	/// board (initial board + every reroll) rolls chancePercent once, independently - no
	/// cadence, no pity. chancePercent 0 or less falls back to DefaultReservedChancePercent;
	/// 100 = every board. firstBoardOnly (ODDS deep form) fires on board 0 only and skips the roll.
	/// </summary>
	public class ReservedSlotSpec
	{
		public EnumStorage.UtilityKind kind;
		public EnumStorage.Rarity rarity;
		public EnumStorage.Tag tag;
		/// <summary>ODDS-style predicate: candidate = any utility card; rarity/tag ignored when set.</summary>
		public bool wantsUtilityCard;
		public int chancePercent;
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
					bonus.boardDiscounts.Add(new BoardDiscountSpec
					{
						chancePercent = script.utilityValue2,
						percentOff = script.utilityValue,
					});
					break;
				case EnumStorage.UtilityKind.OddsUtility:
					// utilityValue2 > 0 marks the deep form: the visit's FIRST board is guaranteed
					// one utility card (utilityValue then unused); v2 = 0 is the per-board chance
					// form (utilityValue = roll percent). Both use the reserved-slot pipeline now.
					if (script.utilityValue2 > 0)
					{
						bonus.reservedSlots.Add(new ReservedSlotSpec
						{
							kind = EnumStorage.UtilityKind.OddsUtility,
							wantsUtilityCard = true,
							chancePercent = 100,
							firstBoardOnly = true,
						});
					}
					else
					{
						bonus.reservedSlots.Add(new ReservedSlotSpec
						{
							kind = EnumStorage.UtilityKind.OddsUtility,
							wantsUtilityCard = true,
							chancePercent = script.utilityValue,
						});
					}
					break;
				case EnumStorage.UtilityKind.ReservedTag:
					bonus.reservedSlots.Add(new ReservedSlotSpec
					{
						kind = EnumStorage.UtilityKind.ReservedTag,
						tag = script.reservedTag,
						chancePercent = script.utilityValue2,
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
		return new ReservedSlotSpec
		{
			kind = script.utilityKind,
			// Guaranteed rarity comes from the KIND, not the utility card's own rarity:
			// RaritySlotU guarantees Uncommon offers, RaritySlotR guarantees Rare offers.
			rarity = script.utilityKind == EnumStorage.UtilityKind.RaritySlotR
				? EnumStorage.Rarity.Rare
				: EnumStorage.Rarity.Uncommon,
			// utilityValue2 carries the per-board roll chance; unconfigured (0 or less, legacy
			// cadence values included) falls back to the default at fire time.
			chancePercent = script.utilityValue2,
		};
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

	/// <summary>Resolves a spec's configured chance percent; unconfigured (0 or less) falls back to the default, 100+ clamps to 100.</summary>
	public static int ChanceOrPercent(int chancePercent)
	{
		return chancePercent > 0 ? Mathf.Min(100, chancePercent) : DefaultReservedChancePercent;
	}

	/// <summary>
	/// Rolls every board discount spec once (per generated board - initial board and every
	/// reroll) and returns the summed percent-off, clamped to [0, 100]. Pure: pass the injected
	/// System.Random for determinism.
	/// </summary>
	public static int RollBoardDiscountOffPercent(Bonus bonus, System.Random rng)
	{
		if (bonus == null || bonus.boardDiscounts == null || bonus.boardDiscounts.Count == 0 || rng == null) return 0;
		int total = 0;
		foreach (var spec in bonus.boardDiscounts)
		{
			if (spec == null) continue;
			if (rng.NextDouble() * 100.0 < ChanceOrPercent(spec.chancePercent))
			{
				total += Mathf.Clamp(spec.percentOff, 0, 100);
			}
		}
		return Mathf.Clamp(total, 0, 100);
	}

	/// <summary>
	/// Gold off for a price under a percent-off discount, HALF PRICE ROUNDS UP:
	/// pay = Ceil(price * (100 - percent) / 100), off = price - pay (3 @ 50% -> pay 2, off 1).
	/// </summary>
	public static int GoldOffForPercent(int price, int percentOff)
	{
		if (price <= 0 || percentOff <= 0) return 0;
		int clamped = Mathf.Clamp(percentOff, 0, 100);
		int pay = Mathf.CeilToInt(price * (100 - clamped) / 100f);
		return price - pay;
	}

	public static int ComputePayday(int payCheckBase, int sessionNum, int incomeGrowthPerStep, int sessionsPerIncomeStep, Bonus bonus)
	{
		return payCheckBase + StepGrowth(sessionNum, incomeGrowthPerStep, sessionsPerIncomeStep) + (bonus != null ? bonus.paydayBonus : 0);
	}

	public static int ComputeHpMax(int hpMaxOg, int sessionNum, int hpMaxGrowthPerStep, int sessionsPerHpMaxStep, Bonus bonus)
	{
		return hpMaxOg + StepGrowth(sessionNum, hpMaxGrowthPerStep, sessionsPerHpMaxStep) + (bonus != null ? bonus.hpMaxBonus : 0);
	}

	public static int ComputeDeckSize(int deckSizeOg, int sessionNum, int deckSizeGrowthPerStep, int sessionsPerDeckSizeStep, int slotPurchases, int ceiling)
	{
		int value = deckSizeOg + StepGrowth(sessionNum, deckSizeGrowthPerStep, sessionsPerDeckSizeStep) + Mathf.Max(0, slotPurchases);
		return Mathf.Clamp(value, 1, Mathf.Max(1, ceiling));
	}

	public static int GetDeckSlotPrice(int basePrice, int priceStepPerPurchase, int purchasesAlreadyMade)
	{
		return basePrice + Mathf.Max(0, priceStepPerPurchase) * Mathf.Max(0, purchasesAlreadyMade);
	}
}
