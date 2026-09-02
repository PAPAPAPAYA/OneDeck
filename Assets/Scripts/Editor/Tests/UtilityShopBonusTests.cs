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
		Assert.AreEqual(0f, bonus.oddsBonusPercent);
		Assert.AreEqual(0, bonus.rerollDiscounts.Count);
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
		// C-tier U slot: utilityValue2 <= 0 => first board only
		MakeCard(EnumStorage.UtilityKind.RaritySlotU, 0, 0, typeId: "SU_C");
		// U-tier U slot: utilityValue2 = 1 => every board
		MakeCard(EnumStorage.UtilityKind.RaritySlotU, 0, 1, typeId: "SU_U");
		// R slot: every 3 boards
		MakeCard(EnumStorage.UtilityKind.RaritySlotR, 0, 3, typeId: "SR_A");
		// Tag slot: revive, every 3 boards
		MakeCard(EnumStorage.UtilityKind.ReservedTag, 0, 3, typeId: "TAG_R", tag: EnumStorage.Tag.Revive);

		var bonus = UtilityShopBonus.Compute(_created);
		Assert.AreEqual(4, bonus.reservedSlots.Count);

		var suC = bonus.reservedSlots.Find(s => s.kind == EnumStorage.UtilityKind.RaritySlotU && s.firstBoardOnly);
			Assert.IsNotNull(suC);
			// Guaranteed rarity is kind-driven (RaritySlotU -> Uncommon), not the utility card's own rarity.
			Assert.AreEqual(EnumStorage.Rarity.Uncommon, suC.rarity);

			var suU = bonus.reservedSlots.Find(s => s.kind == EnumStorage.UtilityKind.RaritySlotU && !s.firstBoardOnly);
			Assert.IsNotNull(suU);
			Assert.AreEqual(EnumStorage.Rarity.Uncommon, suU.rarity);
			Assert.AreEqual(1, suU.everyBoards);

			var sr = bonus.reservedSlots.Find(s => s.kind == EnumStorage.UtilityKind.RaritySlotR);
			Assert.IsNotNull(sr);
			Assert.AreEqual(EnumStorage.Rarity.Rare, sr.rarity);
			Assert.AreEqual(3, sr.everyBoards);

		var tag = bonus.reservedSlots.Find(s => s.kind == EnumStorage.UtilityKind.ReservedTag);
		Assert.IsNotNull(tag);
		Assert.AreEqual(EnumStorage.Tag.Revive, tag.tag);
		Assert.AreEqual(3, tag.everyBoards);
	}

	[Test]
	public void DiscountSpecs_ParsedWithCadence()
	{
		MakeCard(EnumStorage.UtilityKind.RerollDiscount, 1, 4, typeId: "DIS_C");
		MakeCard(EnumStorage.UtilityKind.RerollDiscount, 2, 3, typeId: "DIS_U");

		var bonus = UtilityShopBonus.Compute(_created);
		Assert.AreEqual(2, bonus.rerollDiscounts.Count);
		Assert.IsTrue(bonus.rerollDiscounts.Exists(d => d.goldOff == 1 && d.everyRerolls == 4));
		Assert.IsTrue(bonus.rerollDiscounts.Exists(d => d.goldOff == 2 && d.everyRerolls == 3));
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
	public void OddsCards_ForceFlagVsPercentBonus()
	{
		// ODDS_1 form: utilityValue2 > 0 -> force the visit's first board utility
		MakeCard(EnumStorage.UtilityKind.OddsUtility, 100, 1, typeId: "ODDS_F");
		// ODDS_2 form: utilityValue2 = 0 -> plain +% chance bonus
		MakeCard(EnumStorage.UtilityKind.OddsUtility, 15, 0, typeId: "ODDS_P");

		var bonus = UtilityShopBonus.Compute(_created);
		Assert.IsTrue(bonus.firstBoardUtilityForce);
		Assert.AreEqual(15f, bonus.oddsBonusPercent, 0.001f);
	}

	[Test]
	public void PaydayFormula_BasePlusSessionPlusBonus()
	{
		MakeCard(EnumStorage.UtilityKind.Income, 5, 0, typeId: "INC_F");
		var bonus = UtilityShopBonus.Compute(_created);
		Assert.AreEqual(12 + 2 * 3 + 5, UtilityShopBonus.ComputePayday(12, 3, 2, bonus));
	}

	[Test]
	public void HpMaxFormula_BasePlusSessionPlusBonus()
	{
		MakeCard(EnumStorage.UtilityKind.HpMax, 4, 0, typeId: "HP_F");
		var bonus = UtilityShopBonus.Compute(_created);
		Assert.AreEqual(30 + 2 * 2 + 4, UtilityShopBonus.ComputeHpMax(30, 2, 2, bonus));
	}

	[Test]
	public void DeckSize_ClampsToCeilingAndFloor()
	{
		// 3 + 1*2 + 14 = 19 -> ceiling 16
		Assert.AreEqual(16, UtilityShopBonus.ComputeDeckSize(3, 2, 1, 14, 16));
		// negative purchases are defensively treated as 0, not as shrinkage
		Assert.AreEqual(3, UtilityShopBonus.ComputeDeckSize(3, 0, 0, -50, 16));
		// floor: og below 1 clamps up to 1
		Assert.AreEqual(1, UtilityShopBonus.ComputeDeckSize(0, 0, 0, 0, 16));
	}

	[Test]
	public void DeckSlotPrice_EscalatesWithPurchases()
	{
		Assert.AreEqual(4, UtilityShopBonus.GetDeckSlotPrice(4, 2, 0));
		Assert.AreEqual(10, UtilityShopBonus.GetDeckSlotPrice(4, 2, 3));
	}
}
