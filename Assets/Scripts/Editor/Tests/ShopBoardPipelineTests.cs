using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode tests for the pure shop board generation pipeline
/// (plans/plan-utility-passive-shop-pipeline-2026-08-31.md, step 3).
/// Determinism rules: board type pinned via chance 0/100, waves pinned via 100/0 percents,
/// reserved cadence asserted per boardIndex - no assertion depends on a random draw.
/// </summary>
public class ShopBoardPipelineTests : HeadlessCombatTestFixture
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

	private CardScript MakeCard(EnumStorage.UtilityKind kind = EnumStorage.UtilityKind.None,
		EnumStorage.Rarity rarity = EnumStorage.Rarity.Common,
		bool isCreature = false,
		EnumStorage.Tag tag = EnumStorage.Tag.None,
		string typeId = null,
		bool withDeckSizeEffect = false)
	{
		var card = CreateCard(true, "board_" + _created.Count, typeId);
		_created.Add(card);
		var script = card.GetComponent<CardScript>();
		script.utilityKind = kind;
		script.rarity = rarity;
		script.cardType = isCreature ? EnumStorage.CardType.Creature : EnumStorage.CardType.None;
		if (tag != EnumStorage.Tag.None)
		{
			script.myTags.Add(tag);
		}
		if (withDeckSizeEffect)
		{
			card.AddComponent<DeckSizeIncreaseEffect>();
		}
		return script;
	}

	private static UtilityShopBonus.ReservedSlotSpec RaritySpec(EnumStorage.UtilityKind kind, int everyBoards, bool firstBoardOnly)
	{
		return new UtilityShopBonus.ReservedSlotSpec
		{
			kind = kind,
			// Same kind-driven rarity rule as UtilityShopBonus.BuildRaritySlotSpec.
			rarity = kind == EnumStorage.UtilityKind.RaritySlotR ? EnumStorage.Rarity.Rare : EnumStorage.Rarity.Uncommon,
			everyBoards = everyBoards,
			firstBoardOnly = firstBoardOnly,
		};
	}

	private static System.Random NewRng()
	{
		return new System.Random(12345);
	}

	[Test]
	public void ChanceZero_AlwaysCombatBoard_FromCombatPool()
	{
		var combat = MakeCard(typeId: "PLAIN");
		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, null, 0, 0f, 2, 3, false, NewRng());
		Assert.IsFalse(board.isUtilityBoard);
		Assert.AreEqual(2, board.cards.Count);
		Assert.IsTrue(board.cards.Contains(combat.gameObject));
	}

	[Test]
	public void Chance100_AlwaysUtilityBoard_FromUtilityPool()
	{
		var income = MakeCard(EnumStorage.UtilityKind.Income, typeId: "INC");
		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, null, 0, 100f, 2, 3, false, NewRng());
		Assert.IsTrue(board.isUtilityBoard);
		Assert.AreEqual(3, board.cards.Count);
		Assert.IsTrue(board.cards.Contains(income.gameObject));
	}

	[Test]
	public void OddsBonus_AddsToBoardChance()
	{
		MakeCard(EnumStorage.UtilityKind.OddsUtility, typeId: "ODD");
		// staged 0% + 100% odds bonus clamps to a guaranteed utility board
		var bonus = new UtilityShopBonus.Bonus { oddsBonusPercent = 100f };
		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 0, 0f, 1, 1, false, NewRng());
		Assert.IsTrue(board.isUtilityBoard);
		Assert.AreEqual(1, board.cards.Count);
	}

	[Test]
	public void OddsForce_FirstBoardUtility_LaterBoardsNot()
	{
		MakeCard(EnumStorage.UtilityKind.OddsUtility, typeId: "ODD"); // keeps the utility pool non-empty
		var bonus = new UtilityShopBonus.Bonus { firstBoardUtilityForce = true };

		// boardIndex 0: forced utility even at 0% table chance
		var first = ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 0, 0f, 0, 1, false, NewRng());
		Assert.IsTrue(first.isUtilityBoard);
		Assert.AreEqual(1, first.cards.Count);

		// boardIndex 1: no force, 0% table chance -> combat board
		var second = ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 1, 0f, 1, 0, false, NewRng());
		Assert.IsFalse(second.isUtilityBoard);
	}

	[Test]
	public void OddsForce_DoesNotOverrideEmptyUtilityPool()
	{
		MakeCard(typeId: "PLAIN"); // utility pool empty
		var bonus = new UtilityShopBonus.Bonus { firstBoardUtilityForce = true };

		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 0, 100f, 1, 1, false, NewRng());
		Assert.IsFalse(board.isUtilityBoard); // empty-pool fallthrough wins over the force
	}

	[Test]
	public void Classification_UtilityOnly_NeverOnCombatGenericSlots()
	{
		MakeCard(EnumStorage.UtilityKind.Income, typeId: "INC");
		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, null, 0, 0f, 2, 2, false, NewRng());
		Assert.IsFalse(board.isUtilityBoard);
		Assert.AreEqual(0, board.cards.Count);
	}

	[Test]
	public void Classification_PlainCombatCard_NeverOnUtilityBoard()
	{
		MakeCard(EnumStorage.UtilityKind.OddsUtility, typeId: "ODDS"); // keeps the utility pool non-empty
		MakeCard(typeId: "PLAIN");
		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, null, 0, 100f, 2, 2, false, NewRng());
		Assert.IsTrue(board.isUtilityBoard);
		Assert.AreEqual(2, board.cards.Count);
		Assert.IsTrue(board.cards.TrueForAll(c => c.GetComponent<CardScript>().cardTypeID == "ODDS"));
	}

	[Test]
	public void Classification_OddsUtility_ExemptOnBothBoards()
	{
		MakeCard(EnumStorage.UtilityKind.OddsUtility, typeId: "ODD");
		var combatBoard = ShopBoardPipeline.GenerateBoard(_created, s => 1f, null, 0, 0f, 1, 1, false, NewRng());
		Assert.IsFalse(combatBoard.isUtilityBoard);
		Assert.AreEqual(1, combatBoard.cards.Count);

		var utilityBoard = ShopBoardPipeline.GenerateBoard(_created, s => 1f, null, 0, 100f, 1, 1, false, NewRng());
		Assert.IsTrue(utilityBoard.isUtilityBoard);
		Assert.AreEqual(1, utilityBoard.cards.Count);
	}

	[Test]
	public void Reserved_FirstBoardOnly_FiresOnlyOnBoard0()
	{
		MakeCard(rarity: EnumStorage.Rarity.Uncommon, typeId: "U_TARGET");
		var bonus = new UtilityShopBonus.Bonus();
		bonus.reservedSlots.Add(RaritySpec(EnumStorage.UtilityKind.RaritySlotU, 1, true));

		var first = ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 0, 0f, 0, 0, false, NewRng());
		Assert.AreEqual(1, first.cards.Count);

		var second = ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 1, 0f, 0, 0, false, NewRng());
		Assert.AreEqual(0, second.cards.Count);
	}

	[Test]
	public void Reserved_Cadence_Every3_FiresOnBoards2And5Only()
	{
		MakeCard(rarity: EnumStorage.Rarity.Rare, typeId: "R_TARGET");
		var bonus = new UtilityShopBonus.Bonus();
		bonus.reservedSlots.Add(RaritySpec(EnumStorage.UtilityKind.RaritySlotR, 3, false));

		Assert.AreEqual(0, ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 0, 0f, 0, 0, false, NewRng()).cards.Count);
		Assert.AreEqual(0, ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 1, 0f, 0, 0, false, NewRng()).cards.Count);
		Assert.AreEqual(1, ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 2, 0f, 0, 0, false, NewRng()).cards.Count);
		Assert.AreEqual(0, ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 3, 0f, 0, 0, false, NewRng()).cards.Count);
		Assert.AreEqual(0, ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 4, 0f, 0, 0, false, NewRng()).cards.Count);
		Assert.AreEqual(1, ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 5, 0f, 0, 0, false, NewRng()).cards.Count);
	}

	[Test]
	public void Reserved_TagSlot_MatchesMyTags()
	{
		MakeCard(tag: EnumStorage.Tag.Revive, typeId: "REVIVE_TARGET");
		MakeCard(rarity: EnumStorage.Rarity.Rare, typeId: "NOT_REVIVE");
		var bonus = new UtilityShopBonus.Bonus();
		bonus.reservedSlots.Add(new UtilityShopBonus.ReservedSlotSpec
		{
			kind = EnumStorage.UtilityKind.ReservedTag,
			tag = EnumStorage.Tag.Revive,
			everyBoards = 1,
		});

		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 2, 0f, 0, 0, false, NewRng());
		Assert.AreEqual(1, board.cards.Count);
		Assert.AreEqual("REVIVE_TARGET", board.cards[0].GetComponent<CardScript>().cardTypeID);
	}

	[Test]
	public void Reserved_TagSlotNone_Skips()
	{
		MakeCard(typeId: "PLAIN");
		var bonus = new UtilityShopBonus.Bonus();
		bonus.reservedSlots.Add(new UtilityShopBonus.ReservedSlotSpec
		{
			kind = EnumStorage.UtilityKind.ReservedTag,
			tag = EnumStorage.Tag.None,
			everyBoards = 1,
		});

		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 2, 0f, 0, 0, false, NewRng());
		Assert.AreEqual(0, board.cards.Count);
	}

	[Test]
	public void Reserved_NoCandidate_SkipsSlot()
	{
		MakeCard(rarity: EnumStorage.Rarity.Common, typeId: "ONLY_C");
		var bonus = new UtilityShopBonus.Bonus();
		bonus.reservedSlots.Add(RaritySpec(EnumStorage.UtilityKind.RaritySlotR, 1, false));

		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 2, 0f, 1, 1, false, NewRng());
		Assert.AreEqual(1, board.cards.Count); // generic roll only; reserved skipped
	}

	[Test]
	public void Reserved_CombatBoard_OnlyCombatCandidates()
	{
		MakeCard(EnumStorage.UtilityKind.Income, EnumStorage.Rarity.Uncommon, typeId: "UTIL_U"); // utility-only card must not satisfy a combat-board guarantee
		MakeCard(rarity: EnumStorage.Rarity.Uncommon, typeId: "COMBAT_U");
		var bonus = new UtilityShopBonus.Bonus();
		bonus.reservedSlots.Add(RaritySpec(EnumStorage.UtilityKind.RaritySlotU, 1, true));

		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 0, 0f, 1, 0, false, NewRng());
		Assert.IsFalse(board.isUtilityBoard);
		Assert.AreEqual(2, board.cards.Count); // 1 generic + 1 reserved
		Assert.IsFalse(board.cards.Exists(c => c.GetComponent<CardScript>().cardTypeID == "UTIL_U"),
			"combat-board reserved slots must draw from the combat pool only");
	}

	[Test]
	public void Reserved_UtilityBoard_OnlyUtilityCandidates()
	{
		MakeCard(EnumStorage.UtilityKind.Income, EnumStorage.Rarity.Rare, typeId: "UTIL_R");
		MakeCard(rarity: EnumStorage.Rarity.Rare, typeId: "COMBAT_R"); // combat card must not satisfy a utility-board guarantee
		var bonus = new UtilityShopBonus.Bonus();
		bonus.reservedSlots.Add(RaritySpec(EnumStorage.UtilityKind.RaritySlotR, 3, false));

		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 2, 100f, 0, 0, false, NewRng());
		Assert.IsTrue(board.isUtilityBoard);
		Assert.AreEqual(1, board.cards.Count);
		Assert.AreEqual("UTIL_R", board.cards[0].GetComponent<CardScript>().cardTypeID);
	}

	[Test]
	public void Reserved_UtilityBoard_NoRarityMatch_FallsBackToAnyUtilityCard()
	{
		MakeCard(EnumStorage.UtilityKind.Income, EnumStorage.Rarity.Common, typeId: "INC_C"); // utility pool has no R left
		MakeCard(rarity: EnumStorage.Rarity.Rare, typeId: "COMBAT_R"); // must not backfill, and must not be offered
		var bonus = new UtilityShopBonus.Bonus();
		bonus.reservedSlots.Add(RaritySpec(EnumStorage.UtilityKind.RaritySlotR, 3, false));

		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 2, 100f, 0, 0, false, NewRng());
		Assert.IsTrue(board.isUtilityBoard);
		Assert.AreEqual(1, board.cards.Count);
		Assert.AreEqual("INC_C", board.cards[0].GetComponent<CardScript>().cardTypeID); // any-rarity fallback, still utility-only
	}

	[Test]
	public void Reserved_CombatBoard_NoMatch_SkipsNoFallback()
	{
		MakeCard(EnumStorage.UtilityKind.Income, EnumStorage.Rarity.Rare, typeId: "UTIL_R");
		MakeCard(rarity: EnumStorage.Rarity.Common, typeId: "COMBAT_C"); // combat pool has no R
		var bonus = new UtilityShopBonus.Bonus();
		bonus.reservedSlots.Add(RaritySpec(EnumStorage.UtilityKind.RaritySlotR, 3, false));

		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 2, 0f, 1, 0, false, NewRng());
		Assert.IsFalse(board.isUtilityBoard);
		Assert.AreEqual(1, board.cards.Count); // generic roll only; reserved skipped, never backfilled by a utility card
		Assert.AreEqual("COMBAT_C", board.cards[0].GetComponent<CardScript>().cardTypeID);
	}

	[Test]
	public void UtilityPoolExhausted_NeverUtilityBoard()
	{
		MakeCard(EnumStorage.UtilityKind.Income, EnumStorage.Rarity.Uncommon, typeId: "OWNED_INC"); // owned -> deduped
		MakeCard(withDeckSizeEffect: true, typeId: "SLOT_CARD"); // at ceiling -> excluded
		var plain = MakeCard(typeId: "PLAIN");
		var bonus = new UtilityShopBonus.Bonus();
		bonus.ownedUtilityTypeIds.Add("OWNED_INC");

		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 0, 100f, 2, 3, true, NewRng());
		Assert.IsFalse(board.isUtilityBoard); // classified utility pool is empty: board-type roll falls through to combat
		Assert.AreEqual(2, board.cards.Count);
		Assert.IsTrue(board.cards.TrueForAll(c => c == plain.gameObject));
	}

	[Test]
	public void DeckSizeCeiling_ExcludesDeckSlotCards()
	{
		var slotCard = MakeCard(withDeckSizeEffect: true, typeId: "SLOT_CARD");

		// Below ceiling: the deck-size card is utility-board-eligible (deck-size card = utility only).
		var below = ShopBoardPipeline.GenerateBoard(_created, s => 1f, null, 0, 100f, 1, 1, false, NewRng());
		Assert.IsTrue(below.isUtilityBoard);
		Assert.AreEqual(1, below.cards.Count);
		Assert.IsTrue(below.cards.Contains(slotCard.gameObject));

		// At ceiling: excluded from pools and reserved candidates alike.
		var atCeiling = ShopBoardPipeline.GenerateBoard(_created, s => 1f, null, 0, 100f, 1, 1, true, NewRng());
		Assert.AreEqual(0, atCeiling.cards.Count);
	}

	[Test]
	public void Wave_Creature100_CombatGenericSlotsAllCreatures()
	{
		var creature = MakeCard(isCreature: true, typeId: "CREATURE");
		MakeCard(typeId: "SPELL");
		var bonus = new UtilityShopBonus.Bonus { creatureWaveChancePercent = 100f };

		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 0, 0f, 2, 0, false, NewRng());
		Assert.IsFalse(board.isUtilityBoard);
		Assert.AreEqual(2, board.cards.Count);
		Assert.IsTrue(board.cards.Contains(creature.gameObject));
		Assert.IsFalse(board.cards.Exists(c => c.GetComponent<CardScript>().cardTypeID == "SPELL"));
	}

	[Test]
	public void Wave_Spell100_AppliesOnCreatureMiss()
	{
		MakeCard(isCreature: true, typeId: "CREATURE");
		var spell = MakeCard(typeId: "SPELL");
		var bonus = new UtilityShopBonus.Bonus { spellWaveChancePercent = 100f };

		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 0, 0f, 2, 0, false, NewRng());
		Assert.AreEqual(2, board.cards.Count);
		Assert.IsTrue(board.cards.Contains(spell.gameObject));
		Assert.IsFalse(board.cards.Exists(c => c.GetComponent<CardScript>().cardTypeID == "CREATURE"));
	}

	[Test]
	public void Wave_BothHeld_CreatureWinsOnHit()
	{
		var creature = MakeCard(isCreature: true, typeId: "CREATURE");
		MakeCard(typeId: "SPELL");
		var bonus = new UtilityShopBonus.Bonus { creatureWaveChancePercent = 100f, spellWaveChancePercent = 100f };

		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 0, 0f, 2, 0, false, NewRng());
		Assert.AreEqual(2, board.cards.Count);
		Assert.IsTrue(board.cards.Contains(creature.gameObject));
	}

	[Test]
	public void Wave_DoesNotAffectUtilityBoard()
	{
		MakeCard(EnumStorage.UtilityKind.Income, typeId: "INC"); // non-creature utility card
		var bonus = new UtilityShopBonus.Bonus { creatureWaveChancePercent = 100f, spellWaveChancePercent = 100f };

		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 0, 100f, 0, 2, false, NewRng());
		Assert.IsTrue(board.isUtilityBoard);
		Assert.AreEqual(2, board.cards.Count);
	}

	[Test]
	public void Weights_ZeroWeight_NeverRolled()
	{
		var zeroWeight = MakeCard(typeId: "ZERO_W");
		var normal = MakeCard(typeId: "NORMAL_W");

		for (int i = 0; i < 20; i++)
		{
			var board = ShopBoardPipeline.GenerateBoard(_created, s => s.cardTypeID == "ZERO_W" ? 0f : 1f, null, 0, 0f, 1, 0, false, NewRng());
			Assert.AreEqual(1, board.cards.Count);
			Assert.IsFalse(board.cards.Contains(zeroWeight.gameObject), "zero-weight card must never be rolled");
			Assert.IsTrue(board.cards.Contains(normal.gameObject));
		}
	}

	[Test]
	public void ReservedSlots_Appended_NotDisplacingGenericSlots()
	{
		MakeCard(rarity: EnumStorage.Rarity.Uncommon, typeId: "U_TARGET");
		MakeCard(typeId: "PLAIN");
		var bonus = new UtilityShopBonus.Bonus();
		bonus.reservedSlots.Add(RaritySpec(EnumStorage.UtilityKind.RaritySlotU, 1, true));

		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 0, 0f, 2, 0, false, NewRng());
		Assert.AreEqual(3, board.cards.Count); // 2 generic + 1 reserved
	}
}
