using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode tests for the pure shop-utility resolver and baseline growth formulas
/// (plans/plan-utility-passive-shop-pipeline-2026-08-31.md, step 2).
/// </summary>
public class UtilityShopBonusTests : HeadlessCombatTestFixture
{
	private readonly List<GameObject> _created = new List<GameObject>();

	[TearDown]
	public void TearDownCreatedCards()
	{
		foreach (var go in _created)
		{
			if (go != null)
			{
				Object.DestroyImmediate(go);
			}
		}
		_created.Clear();
	}

	private CardScript MakeCard(EnumStorage.UtilityKind kind, int v = 0, int v2 = 0,
		string typeId = null, EnumStorage.Tag tag = EnumStorage.Tag.None)
	{
		var card = CreateCard(true, "util_" + kind + "_" + _created.Count, typeId);
		_created.Add(card);
		var script = card.GetComponent<CardScript>();
		script.isPassive = true;
		script.utilityKind = kind;
		script.utilityValue = v;
		script.utilityValue2 = v2;
		script.reservedTag = tag;
		script.rarity = EnumStorage.Rarity.Common;
		return script;
	}

	private CardScript MakeNormalCard(string typeId)
	{
		var card = CreateCard(true, "normal_" + _created.Count, typeId);
		_created.Add(card);
		var script = card.GetComponent<CardScript>();
		script.utilityKind = EnumStorage.UtilityKind.None;
		return script;
	}

	[Test]
	public void EmptyDeck_AllBonusesZero()
	{
		var bonus = UtilityShopBonus.Compute(new List<GameObject>());
		Assert.AreEqual(0, bonus.paydayBonus);
		Assert.AreEqual(0, bonus.extraShopOptions);
		Assert.AreEqual(0, bonus.freeRerolls);
		Assert.AreEqual(0, bonus.hpMaxBonus);
		Assert.AreEqual(0, bonus.extraBoardSlots);
		Assert.AreEqual(0, bonus.extraBoardSlotsChancePercent);
		Assert.AreEqual(0, bonus.boardDiscounts.Count);
		Assert.AreEqual(0, bonus.reservedSlots.Count);
		Assert.AreEqual(0, bonus.ownedUtilityTypeIds.Count);
	}

	[Test]
	public void Income_Option_FreeReroll_HpMax_SumAcrossCopies()
	{
		MakeCard(EnumStorage.UtilityKind.Income, 2, typeId: "INC_A");
		MakeCard(EnumStorage.UtilityKind.Income, 3, typeId: "INC_B");
		MakeCard(EnumStorage.UtilityKind.ShopOption, 1, typeId: "OPT_A");
		MakeCard(EnumStorage.UtilityKind.FreeReroll, 1, typeId: "RER_A");
		MakeCard(EnumStorage.UtilityKind.HpMax, 4, typeId: "HP_A");

		var bonus = UtilityShopBonus.Compute(_created);
		Assert.AreEqual(5, bonus.paydayBonus);
		Assert.AreEqual(1, bonus.extraShopOptions);
		Assert.AreEqual(1, bonus.freeRerolls);
		Assert.AreEqual(4, bonus.hpMaxBonus);
	}

	[Test]
	public void ShopOptionChance_SumsSlotsAndChance()
	{
		MakeCard(EnumStorage.UtilityKind.ShopOptionChance, 1, 25, typeId: "OPT_P_A");
		MakeCard(EnumStorage.UtilityKind.ShopOptionChance, 2, 0, typeId: "OPT_P_B");

		var bonus = UtilityShopBonus.Compute(_created);
		Assert.AreEqual(3, bonus.extraBoardSlots);
		Assert.AreEqual(25, bonus.extraBoardSlotsChancePercent);
		Assert.IsTrue(bonus.ownedUtilityTypeIds.Contains("OPT_P_A"));
	}

	[Test]
	public void ShopOptionChance_NegativeSlotsClampedToZero()
	{
		MakeCard(EnumStorage.UtilityKind.ShopOptionChance, -1, 50, typeId: "OPT_P_NEG");

		var bonus = UtilityShopBonus.Compute(_created);
		Assert.AreEqual(0, bonus.extraBoardSlots);
		Assert.AreEqual(50, bonus.extraBoardSlotsChancePercent);
	}

	[Test]
	public void OwnedTypeIds_TracksPassiveUtilitiesOnly()
	{
		MakeCard(EnumStorage.UtilityKind.Income, 2, typeId: "INC_A");
		MakeNormalCard("NORMAL_A");

		var bonus = UtilityShopBonus.Compute(_created);
		Assert.IsTrue(bonus.ownedUtilityTypeIds.Contains("INC_A"));
		Assert.IsFalse(bonus.ownedUtilityTypeIds.Contains("NORMAL_A"));
	}

	[Test]
	public void RarityWeight_MultipliesPerRarity_AcrossCards()
	{
		var a = MakeCard(EnumStorage.UtilityKind.RarityWeight, typeId: "W_A");
		a.utilityRarityWeightMults.Add(new CardScript.UtilityRarityWeightMult
		{
			rarity = EnumStorage.Rarity.Uncommon,
			mult = 2f,
		});
		var b = MakeCard(EnumStorage.UtilityKind.RarityWeight, typeId: "W_B");
		b.utilityRarityWeightMults.Add(new CardScript.UtilityRarityWeightMult
		{
			rarity = EnumStorage.Rarity.Uncommon,
			mult = 1.5f,
		});
		b.utilityRarityWeightMults.Add(new CardScript.UtilityRarityWeightMult
		{
			rarity = EnumStorage.Rarity.Rare,
			mult = 3f,
		});

		var bonus = UtilityShopBonus.Compute(_created);
		Assert.AreEqual(3f, bonus.rarityWeightMults[EnumStorage.Rarity.Uncommon], 0.001f);
		Assert.AreEqual(3f, bonus.rarityWeightMults[EnumStorage.Rarity.Rare], 0.001f);
		Assert.IsFalse(bonus.rarityWeightMults.ContainsKey(EnumStorage.Rarity.Common));
	}

	[Test]
	public void ReservedSlots_RarityLadder_AndTagSpecs()
	{
		// Uncommon slot: unconfigured chance (0) -> DefaultReservedChancePercent at fire time
		MakeCard(EnumStorage.UtilityKind.RaritySlotU, 0, 0, typeId: "SU_A");
		// Uncommon slot: explicit 25% per-board roll
		MakeCard(EnumStorage.UtilityKind.RaritySlotU, 0, 25, typeId: "SU_B");
		// R slot: 25% per-board roll
		MakeCard(EnumStorage.UtilityKind.RaritySlotR, 0, 25, typeId: "SR_A");
		// Tag slot: revive, 25% per-board roll
		MakeCard(EnumStorage.UtilityKind.ReservedTag, 0, 25, typeId: "TAG_R", tag: EnumStorage.Tag.Revive);

		var bonus = UtilityShopBonus.Compute(_created);
		Assert.AreEqual(4, bonus.reservedSlots.Count);

		var suA = bonus.reservedSlots.Find(s => s.kind == EnumStorage.UtilityKind.RaritySlotU && s.chancePercent == 0);
		Assert.IsNotNull(suA);
		// Guaranteed rarity is kind-driven (RaritySlotU -> Uncommon), not the utility card's own rarity.
		Assert.AreEqual(EnumStorage.Rarity.Uncommon, suA.rarity);

		var suB = bonus.reservedSlots.Find(s => s.kind == EnumStorage.UtilityKind.RaritySlotU && s.chancePercent == 25);
		Assert.IsNotNull(suB);
		Assert.AreEqual(EnumStorage.Rarity.Uncommon, suB.rarity);

		var sr = bonus.reservedSlots.Find(s => s.kind == EnumStorage.UtilityKind.RaritySlotR);
		Assert.IsNotNull(sr);
		Assert.AreEqual(EnumStorage.Rarity.Rare, sr.rarity);
		Assert.AreEqual(25, sr.chancePercent);

		var tag = bonus.reservedSlots.Find(s => s.kind == EnumStorage.UtilityKind.ReservedTag);
		Assert.IsNotNull(tag);
		Assert.AreEqual(EnumStorage.Tag.Revive, tag.tag);
		Assert.AreEqual(25, tag.chancePercent);
	}

	[Test]
	public void BoardDiscountSpecs_ParsedWithChanceAndPercent()
	{
		// utilityValue = percent off, utilityValue2 = per-board roll chance
		MakeCard(EnumStorage.UtilityKind.RerollDiscount, 50, 25, typeId: "DIS_C");
		MakeCard(EnumStorage.UtilityKind.RerollDiscount, 50, 100, typeId: "DIS_U");

		var bonus = UtilityShopBonus.Compute(_created);
		Assert.AreEqual(2, bonus.boardDiscounts.Count);
		Assert.IsTrue(bonus.boardDiscounts.Exists(d => d.percentOff == 50 && d.chancePercent == 25));
		Assert.IsTrue(bonus.boardDiscounts.Exists(d => d.percentOff == 50 && d.chancePercent == 100));
	}

	[Test]
	public void WaveChances_SumIndependently()
	{
		MakeCard(EnumStorage.UtilityKind.RerollCreatureWave, 20, 0, typeId: "CW_A");
		MakeCard(EnumStorage.UtilityKind.RerollSpellWave, 20, 0, typeId: "SW_A");

		var bonus = UtilityShopBonus.Compute(_created);
		Assert.AreEqual(20f, bonus.creatureWaveChancePercent);
		Assert.AreEqual(20f, bonus.spellWaveChancePercent);
	}

	[Test]
	public void OddsCards_DeepFirstBoardForm_VsPerBoardChanceForm()
	{
		// Deep form: utilityValue2 > 0 -> FIRST board guaranteed one utility card
		MakeCard(EnumStorage.UtilityKind.OddsUtility, 100, 1, typeId: "ODDS_F");
		// Chance form: utilityValue2 = 0 -> per-board chance roll (utilityValue = percent)
		MakeCard(EnumStorage.UtilityKind.OddsUtility, 25, 0, typeId: "ODDS_P");

		var bonus = UtilityShopBonus.Compute(_created);
		Assert.AreEqual(2, bonus.reservedSlots.Count);

		var deep = bonus.reservedSlots.Find(s => s.wantsUtilityCard && s.firstBoardOnly);
		Assert.IsNotNull(deep);
		Assert.AreEqual(100, deep.chancePercent);

		var chance = bonus.reservedSlots.Find(s => s.wantsUtilityCard && !s.firstBoardOnly);
		Assert.IsNotNull(chance);
		Assert.AreEqual(25, chance.chancePercent);
	}

	[Test]
	public void ChanceOrPercent_ZeroOrNegativeFallsBackToDefault()
	{
		Assert.AreEqual(25, UtilityShopBonus.ChanceOrPercent(0));
		Assert.AreEqual(25, UtilityShopBonus.ChanceOrPercent(-5));
		Assert.AreEqual(25, UtilityShopBonus.ChanceOrPercent(UtilityShopBonus.DefaultReservedChancePercent));
		Assert.AreEqual(40, UtilityShopBonus.ChanceOrPercent(40));
		Assert.AreEqual(100, UtilityShopBonus.ChanceOrPercent(100));
		Assert.AreEqual(100, UtilityShopBonus.ChanceOrPercent(150));
	}

	[Test]
	public void GoldOffForPercent_HalfPriceRoundsUp()
	{
		// Half price rounds up: 3 -> pay 2, 5 -> pay 3, 4 -> pay 2
		Assert.AreEqual(1, UtilityShopBonus.GoldOffForPercent(3, 50));
		Assert.AreEqual(2, UtilityShopBonus.GoldOffForPercent(5, 50));
		Assert.AreEqual(2, UtilityShopBonus.GoldOffForPercent(4, 50));
		// 100% off -> free
		Assert.AreEqual(12, UtilityShopBonus.GoldOffForPercent(12, 100));
		// Defensive edges
		Assert.AreEqual(0, UtilityShopBonus.GoldOffForPercent(4, 0));
		Assert.AreEqual(0, UtilityShopBonus.GoldOffForPercent(4, -10));
		Assert.AreEqual(0, UtilityShopBonus.GoldOffForPercent(0, 50));
	}

	[Test]
	public void RollBoardDiscountOffPercent_BothHit_ClampsTo100()
	{
		MakeCard(EnumStorage.UtilityKind.RerollDiscount, 50, 100, typeId: "DIS_A");
		MakeCard(EnumStorage.UtilityKind.RerollDiscount, 60, 100, typeId: "DIS_B");
		var bonus = UtilityShopBonus.Compute(_created);
		Assert.AreEqual(100, UtilityShopBonus.RollBoardDiscountOffPercent(bonus, new System.Random(7)));
		Assert.AreEqual(0, UtilityShopBonus.RollBoardDiscountOffPercent(null, new System.Random(7)));
	}

	[Test]
	public void RollBoardDiscountOffPercent_DefaultChance_FiresSometimes()
	{
		// chancePercent 0 -> default 25%: over 300 seeded rolls the spec must both hit and miss.
		MakeCard(EnumStorage.UtilityKind.RerollDiscount, 50, 0, typeId: "DIS_A");
		var bonus = UtilityShopBonus.Compute(_created);
		int hits = 0;
		for (int i = 0; i < 300; i++)
		{
			if (UtilityShopBonus.RollBoardDiscountOffPercent(bonus, new System.Random(i)) > 0) hits++;
		}
		Assert.Greater(hits, 300 / 5, "default 25% chance should hit sometimes");
		Assert.Less(hits, 300 * 4 / 5, "default 25% chance should miss sometimes");
	}

	[Test]
	public void PaydayFormula_BasePlusSessionPlusBonus()
	{
		MakeCard(EnumStorage.UtilityKind.Income, 5, 0, typeId: "INC_F");
		var bonus = UtilityShopBonus.Compute(_created);
		Assert.AreEqual(12 + 2 * 3 + 5, UtilityShopBonus.ComputePayday(12, 3, 2, 1, bonus));
	}

	[Test]
	public void PaydayFormula_IntervalSpacing()
	{
		// growth 2 / interval 2: session 3 = 1 step, session 4 = 2 steps
		Assert.AreEqual(12 + 2 * 1, UtilityShopBonus.ComputePayday(12, 3, 2, 2, null));
		Assert.AreEqual(12 + 2 * 2, UtilityShopBonus.ComputePayday(12, 4, 2, 2, null));
	}

	[Test]
	public void HpMaxFormula_BasePlusSessionPlusBonus()
	{
		MakeCard(EnumStorage.UtilityKind.HpMax, 4, 0, typeId: "HP_F");
		var bonus = UtilityShopBonus.Compute(_created);
		Assert.AreEqual(30 + 2 * 2 + 4, UtilityShopBonus.ComputeHpMax(30, 2, 2, 1, bonus));
	}

	[Test]
	public void DeckSize_ClampsToCeilingAndFloor()
	{
		// 3 + 1*2 + 14 = 19 -> ceiling 16
		Assert.AreEqual(16, UtilityShopBonus.ComputeDeckSize(3, 2, 1, 1, 14, 16));
		// negative purchases are defensively treated as 0, not as shrinkage
		Assert.AreEqual(3, UtilityShopBonus.ComputeDeckSize(3, 0, 0, 1, -50, 16));
		// floor: og below 1 clamps up to 1
		Assert.AreEqual(1, UtilityShopBonus.ComputeDeckSize(0, 0, 0, 1, 0, 16));
	}

	[Test]
	public void DeckSize_IntervalGrowthStillClamped()
	{
		// growth 1 / interval 2: session 4 = 2 steps -> 3 + 2 = 5
		Assert.AreEqual(5, UtilityShopBonus.ComputeDeckSize(3, 4, 1, 2, 0, 16));
		// interval growth still respects the ceiling: 3 + 2 + 14 -> 16
		Assert.AreEqual(16, UtilityShopBonus.ComputeDeckSize(3, 4, 1, 2, 14, 16));
	}

	[Test]
	public void StepGrowth_IntervalFloorSemantics()
	{
		// growth 1 / interval 2: growth lands at sessions 2, 4, 6...
		Assert.AreEqual(0, UtilityShopBonus.StepGrowth(0, 1, 2));
		Assert.AreEqual(0, UtilityShopBonus.StepGrowth(1, 1, 2));
		Assert.AreEqual(1, UtilityShopBonus.StepGrowth(2, 1, 2));
		Assert.AreEqual(1, UtilityShopBonus.StepGrowth(3, 1, 2));
		Assert.AreEqual(2, UtilityShopBonus.StepGrowth(4, 1, 2));
		// growth 2 / interval 3: sessions 1-2 flat, 3-5 +2 each... lands at 3 (one step)
		Assert.AreEqual(0, UtilityShopBonus.StepGrowth(2, 2, 3));
		Assert.AreEqual(2, UtilityShopBonus.StepGrowth(3, 2, 3));
		Assert.AreEqual(4, UtilityShopBonus.StepGrowth(6, 2, 3));
	}

	[Test]
	public void StepGrowth_DefensiveInputs()
	{
		// non-positive growth = no growth regardless of interval
		Assert.AreEqual(0, UtilityShopBonus.StepGrowth(10, 0, 2));
		Assert.AreEqual(0, UtilityShopBonus.StepGrowth(10, -1, 2));
		// non-positive interval clamps to 1 -> legacy every-session slope
		Assert.AreEqual(10, UtilityShopBonus.StepGrowth(10, 1, 0));
		Assert.AreEqual(10, UtilityShopBonus.StepGrowth(10, 1, -3));
		// negative session clamps to 0 growth
		Assert.AreEqual(0, UtilityShopBonus.StepGrowth(-5, 1, 1));
	}

	[Test]
	public void DeckSlotPrice_EscalatesWithPurchases()
	{
		Assert.AreEqual(4, UtilityShopBonus.GetDeckSlotPrice(4, 2, 0));
		Assert.AreEqual(10, UtilityShopBonus.GetDeckSlotPrice(4, 2, 3));
	}
}
