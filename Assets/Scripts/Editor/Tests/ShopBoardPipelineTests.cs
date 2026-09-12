using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode tests for the pure shop board generation pipeline
/// (plans/plan-utility-passive-shop-pipeline-2026-08-31.md, step 3; probability + mixed-pool
/// rework 2026-09-11).
/// Determinism rules: board type pinned via chance 0/100, waves pinned via 100/0 percents,
/// reserved slots pinned via chancePercent 100 (statistical bounds for the default-25 roll) -
/// no assertion depends on an unknown draw.
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

	private static UtilityShopBonus.ReservedSlotSpec RaritySpec(EnumStorage.UtilityKind kind, int chancePercent, bool firstBoardOnly)
	{
		return new UtilityShopBonus.ReservedSlotSpec
		{
			kind = kind,
			// Same kind-driven rarity rule as UtilityShopBonus.BuildRaritySlotSpec.
			rarity = kind == EnumStorage.UtilityKind.RaritySlotR ? EnumStorage.Rarity.Rare : EnumStorage.Rarity.Uncommon,
			chancePercent = chancePercent,
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
		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, null, 0, 0f, 2, 3, false, false, NewRng());
		Assert.IsFalse(board.isUtilityBoard);
		Assert.AreEqual(2, board.cards.Count);
		Assert.IsTrue(board.cards.Contains(combat.gameObject));
	}

	[Test]
	public void Chance100_AlwaysUtilityBoard_FromUtilityPool()
	{
		var income = MakeCard(EnumStorage.UtilityKind.Income, typeId: "INC");
		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, null, 0, 100f, 2, 3, false, false, NewRng());
		Assert.IsTrue(board.isUtilityBoard);
		Assert.AreEqual(3, board.cards.Count);
		Assert.IsTrue(board.cards.Contains(income.gameObject));
	}

	[Test]
	public void OddsDeepForm_FirstBoardGuaranteesUtilityCard_Board0Only()
	{
		// Deep form (wantsUtilityCard + firstBoardOnly): board 0 appends one utility card even
		// on a split-mode combat board (candidates come from the utility pool); later boards
		// fire nothing.
		MakeCard(EnumStorage.UtilityKind.Income, typeId: "INC");
		MakeCard(typeId: "PLAIN");
		var bonus = new UtilityShopBonus.Bonus();
		bonus.reservedSlots.Add(new UtilityShopBonus.ReservedSlotSpec
		{
			kind = EnumStorage.UtilityKind.OddsUtility,
			wantsUtilityCard = true,
			chancePercent = 100,
			firstBoardOnly = true,
		});

		var first = ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 0, 0f, 0, 0, false, false, NewRng());
		Assert.IsFalse(first.isUtilityBoard);
		Assert.AreEqual(1, first.cards.Count);
		Assert.AreEqual("INC", first.cards[0].GetComponent<CardScript>().cardTypeID);

		var second = ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 1, 0f, 0, 0, false, false, NewRng());
		Assert.AreEqual(0, second.cards.Count);
	}

	[Test]
	public void OddsDeepForm_NoUtilityCandidate_Skips()
	{
		MakeCard(typeId: "PLAIN"); // no utility card anywhere -> guarantee skips, never backfills
		var bonus = new UtilityShopBonus.Bonus();
		bonus.reservedSlots.Add(new UtilityShopBonus.ReservedSlotSpec
		{
			kind = EnumStorage.UtilityKind.OddsUtility,
			wantsUtilityCard = true,
			chancePercent = 100,
			firstBoardOnly = true,
		});

		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 0, 0f, 1, 0, false, false, NewRng());
		Assert.IsFalse(board.isUtilityBoard);
		Assert.AreEqual(1, board.cards.Count); // generic roll only
		Assert.AreEqual("PLAIN", board.cards[0].GetComponent<CardScript>().cardTypeID);
	}

	[Test]
	public void Classification_UtilityOnly_NeverOnCombatGenericSlots()
	{
		MakeCard(EnumStorage.UtilityKind.Income, typeId: "INC");
		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, null, 0, 0f, 2, 2, false, false, NewRng());
		Assert.IsFalse(board.isUtilityBoard);
		Assert.AreEqual(0, board.cards.Count);
	}

	[Test]
	public void Classification_PlainCombatCard_NeverOnUtilityBoard()
	{
		MakeCard(EnumStorage.UtilityKind.OddsUtility, typeId: "ODDS"); // keeps the utility pool non-empty
		MakeCard(typeId: "PLAIN");
		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, null, 0, 100f, 2, 2, false, false, NewRng());
		Assert.IsTrue(board.isUtilityBoard);
		Assert.AreEqual(2, board.cards.Count);
		Assert.IsTrue(board.cards.TrueForAll(c => c.GetComponent<CardScript>().cardTypeID == "ODDS"));
	}

	[Test]
	public void Classification_OddsUtility_ExemptOnBothBoards()
	{
		MakeCard(EnumStorage.UtilityKind.OddsUtility, typeId: "ODD");
		var combatBoard = ShopBoardPipeline.GenerateBoard(_created, s => 1f, null, 0, 0f, 1, 1, false, false, NewRng());
		Assert.IsFalse(combatBoard.isUtilityBoard);
		Assert.AreEqual(1, combatBoard.cards.Count);

		var utilityBoard = ShopBoardPipeline.GenerateBoard(_created, s => 1f, null, 0, 100f, 1, 1, false, false, NewRng());
		Assert.IsTrue(utilityBoard.isUtilityBoard);
		Assert.AreEqual(1, utilityBoard.cards.Count);
	}

	[Test]
	public void Reserved_FirstBoardOnly_FiresOnlyOnBoard0()
	{
		MakeCard(rarity: EnumStorage.Rarity.Uncommon, typeId: "U_TARGET");
		var bonus = new UtilityShopBonus.Bonus();
		bonus.reservedSlots.Add(RaritySpec(EnumStorage.UtilityKind.RaritySlotU, 100, true));

		var first = ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 0, 0f, 0, 0, false, false, NewRng());
		Assert.AreEqual(1, first.cards.Count);

		var second = ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 1, 0f, 0, 0, false, false, NewRng());
		Assert.AreEqual(0, second.cards.Count);
	}

	[Test]
	public void Reserved_Chance100_FiresEveryBoardIncludingInitial()
	{
		MakeCard(rarity: EnumStorage.Rarity.Rare, typeId: "R_TARGET");
		var bonus = new UtilityShopBonus.Bonus();
		bonus.reservedSlots.Add(RaritySpec(EnumStorage.UtilityKind.RaritySlotR, 100, false));

		for (int boardIndex = 0; boardIndex < 4; boardIndex++)
		{
			Assert.AreEqual(1, ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, boardIndex, 0f, 0, 0, false, false, NewRng()).cards.Count,
				"chance 100 fires on every generated board, initial board included");
		}
	}

	[Test]
	public void Reserved_Chance25_RollsIndependentlyPerBoard()
	{
		MakeCard(rarity: EnumStorage.Rarity.Rare, typeId: "R_TARGET");
		var bonus = new UtilityShopBonus.Bonus();
		bonus.reservedSlots.Add(RaritySpec(EnumStorage.UtilityKind.RaritySlotR, 25, false));

		// Per-board independent roll, no cadence/pity: ~25% fire rate, board 0 participates.
		int fired = 0;
		const int runs = 300;
		for (int i = 0; i < runs; i++)
		{
			var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 0, 0f, 0, 0, false, false, new System.Random(i));
			if (board.cards.Count == 1) fired++;
		}
		Assert.Greater(fired, runs / 5, "25% chance must fire well above a 20% floor over 300 boards");
		Assert.Less(fired, runs * 4 / 5, "25% chance must miss well above a 20% floor over 300 boards");
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
			chancePercent = 100,
		});

		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 2, 0f, 0, 0, false, false, NewRng());
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
			chancePercent = 100,
		});

		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 2, 0f, 0, 0, false, false, NewRng());
		Assert.AreEqual(0, board.cards.Count);
	}

	[Test]
	public void Reserved_NoCandidate_SkipsSlot()
	{
		MakeCard(rarity: EnumStorage.Rarity.Common, typeId: "ONLY_C");
		var bonus = new UtilityShopBonus.Bonus();
		bonus.reservedSlots.Add(RaritySpec(EnumStorage.UtilityKind.RaritySlotR, 100, false));

		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 2, 0f, 1, 1, false, false, NewRng());
		Assert.AreEqual(1, board.cards.Count); // generic roll only; reserved skipped
	}

	[Test]
	public void Reserved_CombatBoard_OnlyCombatCandidates()
	{
		MakeCard(EnumStorage.UtilityKind.Income, EnumStorage.Rarity.Uncommon, typeId: "UTIL_U"); // utility-only card must not satisfy a combat-board guarantee
		MakeCard(rarity: EnumStorage.Rarity.Uncommon, typeId: "COMBAT_U");
		var bonus = new UtilityShopBonus.Bonus();
		bonus.reservedSlots.Add(RaritySpec(EnumStorage.UtilityKind.RaritySlotU, 100, true));

		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 0, 0f, 1, 0, false, false, NewRng());
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
		bonus.reservedSlots.Add(RaritySpec(EnumStorage.UtilityKind.RaritySlotR, 100, false));

		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 2, 100f, 0, 0, false, false, NewRng());
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
		bonus.reservedSlots.Add(RaritySpec(EnumStorage.UtilityKind.RaritySlotR, 100, false));

		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 2, 100f, 0, 0, false, false, NewRng());
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
		bonus.reservedSlots.Add(RaritySpec(EnumStorage.UtilityKind.RaritySlotR, 100, false));

		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 2, 0f, 1, 0, false, false, NewRng());
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

		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 0, 100f, 2, 3, false, true, NewRng());
		Assert.IsFalse(board.isUtilityBoard); // classified utility pool is empty: board-type roll falls through to combat
		Assert.AreEqual(2, board.cards.Count);
		Assert.IsTrue(board.cards.TrueForAll(c => c == plain.gameObject));
	}

	[Test]
	public void DeckSizeCeiling_ExcludesDeckSlotCards()
	{
		var slotCard = MakeCard(withDeckSizeEffect: true, typeId: "SLOT_CARD");

		// Below ceiling: the deck-size card is utility-board-eligible (deck-size card = utility only).
		var below = ShopBoardPipeline.GenerateBoard(_created, s => 1f, null, 0, 100f, 1, 1, false, false, NewRng());
		Assert.IsTrue(below.isUtilityBoard);
		Assert.AreEqual(1, below.cards.Count);
		Assert.IsTrue(below.cards.Contains(slotCard.gameObject));

		// At ceiling: excluded from pools and reserved candidates alike.
		var atCeiling = ShopBoardPipeline.GenerateBoard(_created, s => 1f, null, 0, 100f, 1, 1, false, true, NewRng());
		Assert.AreEqual(0, atCeiling.cards.Count);
	}

	[Test]
	public void MixedPool_DeckSizeCeiling_ExcludesSlotCardFromMergedPool()
	{
		// Mixed mode + ceiling: the ceiling exclusion is baked into classification before the
		// merge, so the deck-size card leaves the merged pool too (mode-independent) while
		// other cards keep rolling.
		MakeCard(withDeckSizeEffect: true, typeId: "SLOT_CARD");
		var plain = MakeCard(typeId: "PLAIN");

		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, null, 0, 0f, 2, 0, true, true, NewRng());
		Assert.IsFalse(board.isUtilityBoard);
		Assert.AreEqual(2, board.cards.Count);
		Assert.IsTrue(board.cards.TrueForAll(c => c == plain.gameObject));
	}

	[Test]
	public void MixedPool_DeckSizeCeiling_ExcludesSlotCardFromReservedCandidates()
	{
		// Mixed mode + ceiling: reserved guarantees draw from the merged pool, which no longer
		// holds the ceiling-excluded deck-size card - the slot skips instead of guaranteeing it.
		MakeCard(withDeckSizeEffect: true, rarity: EnumStorage.Rarity.Uncommon, typeId: "SLOT_CARD");
		MakeCard(typeId: "PLAIN");
		var bonus = new UtilityShopBonus.Bonus();
		bonus.reservedSlots.Add(RaritySpec(EnumStorage.UtilityKind.RaritySlotU, 100, false));

		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 0, 0f, 1, 0, true, true, NewRng());
		Assert.IsFalse(board.isUtilityBoard);
		Assert.AreEqual(1, board.cards.Count); // generic roll only; reserved U slot skips, never backfilled
		Assert.AreEqual("PLAIN", board.cards[0].GetComponent<CardScript>().cardTypeID);
	}

	[Test]
	public void DeckSizeEffect_OnChildGameObject_StillClassifiedUtilityOnly()
	{
		// IncreaseDeckSizeLite keeps its DeckSizeIncreaseEffect on a child object ("Increase 1 deck
		// size"), not the prefab root - classification must still see it (root-only GetComponent
		// miss fixed 2026-09-07: the card used to be offered on combat boards).
		var slotScript = MakeCard(typeId: "SLOT_CHILD");
		var effectHost = new GameObject("board_effect_child");
		effectHost.transform.SetParent(slotScript.transform);
		effectHost.AddComponent<DeckSizeIncreaseEffect>();
		var plain = MakeCard(typeId: "PLAIN");

		// Combat board: the child-effect deck-size card must never be offered.
		var combat = ShopBoardPipeline.GenerateBoard(_created, s => 1f, null, 0, 0f, 1, 1, false, false, NewRng());
		Assert.IsFalse(combat.isUtilityBoard);
		Assert.AreEqual(1, combat.cards.Count);
		Assert.IsTrue(combat.cards.Contains(plain.gameObject));

		// Utility board: it is the utility pool's occupant.
		var utility = ShopBoardPipeline.GenerateBoard(_created, s => 1f, null, 0, 100f, 1, 1, false, false, NewRng());
		Assert.IsTrue(utility.isUtilityBoard);
		Assert.AreEqual(1, utility.cards.Count);
		Assert.IsTrue(utility.cards.Contains(slotScript.gameObject));

		// At deck-size ceiling: excluded everywhere - the utility pool runs dry and the
		// board-type roll falls through to combat (empty-pool rule), leaving only PLAIN.
		var atCeiling = ShopBoardPipeline.GenerateBoard(_created, s => 1f, null, 0, 100f, 1, 1, false, true, NewRng());
		Assert.IsFalse(atCeiling.isUtilityBoard);
		Assert.AreEqual(1, atCeiling.cards.Count);
		Assert.IsTrue(atCeiling.cards.Contains(plain.gameObject));
	}

	[Test]
	public void Wave_Creature100_CombatGenericSlotsAllCreatures()
	{
		var creature = MakeCard(isCreature: true, typeId: "CREATURE");
		MakeCard(typeId: "SPELL");
		var bonus = new UtilityShopBonus.Bonus { creatureWaveChancePercent = 100f };

		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 0, 0f, 2, 0, false, false, NewRng());
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

		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 0, 0f, 2, 0, false, false, NewRng());
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

		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 0, 0f, 2, 0, false, false, NewRng());
		Assert.AreEqual(2, board.cards.Count);
		Assert.IsTrue(board.cards.Contains(creature.gameObject));
	}

	[Test]
	public void Wave_DoesNotAffectUtilityBoard()
	{
		MakeCard(EnumStorage.UtilityKind.Income, typeId: "INC"); // non-creature utility card
		var bonus = new UtilityShopBonus.Bonus { creatureWaveChancePercent = 100f, spellWaveChancePercent = 100f };

		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 0, 100f, 0, 2, false, false, NewRng());
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
			var board = ShopBoardPipeline.GenerateBoard(_created, s => s.cardTypeID == "ZERO_W" ? 0f : 1f, null, 0, 0f, 1, 0, false, false, NewRng());
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
		bonus.reservedSlots.Add(RaritySpec(EnumStorage.UtilityKind.RaritySlotU, 100, true));

		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 0, 0f, 2, 0, false, false, NewRng());
		Assert.AreEqual(3, board.cards.Count); // 2 generic + 1 reserved
	}

	[Test]
	public void MixedPool_NoBoardTypeRoll_UtilityOfferedOnGenericSlots()
	{
		// Mixed mode: no board-type roll; a utility card is offerable on the (single) board.
		// A pure-utility offer on generic slots is impossible in split mode, where combat
		// generic slots exclude utility cards entirely.
		MakeCard(EnumStorage.UtilityKind.Income, typeId: "INC");
		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, null, 0, 0f, 1, 0, true, false, NewRng());
		Assert.IsFalse(board.isUtilityBoard);
		Assert.AreEqual(1, board.cards.Count);
		Assert.AreEqual("INC", board.cards[0].GetComponent<CardScript>().cardTypeID);
	}

	[Test]
	public void MixedPool_CombatCardsStillOffered()
	{
		var plain = MakeCard(typeId: "PLAIN");
		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, null, 0, 0f, 2, 0, true, false, NewRng());
		Assert.IsFalse(board.isUtilityBoard);
		Assert.AreEqual(2, board.cards.Count);
		Assert.IsTrue(board.cards.Contains(plain.gameObject));
	}

	[Test]
	public void MixedPool_WaveFiltersApplyToMergedPool()
	{
		// Creature wave on the merged pool: the utility card is pool-eligible but filtered out.
		var creature = MakeCard(isCreature: true, typeId: "CREATURE");
		MakeCard(EnumStorage.UtilityKind.Income, typeId: "INC");
		var bonus = new UtilityShopBonus.Bonus { creatureWaveChancePercent = 100f };

		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 0, 0f, 2, 0, true, false, NewRng());
		Assert.IsFalse(board.isUtilityBoard);
		Assert.AreEqual(2, board.cards.Count);
		Assert.IsTrue(board.cards.Contains(creature.gameObject));
		Assert.IsFalse(board.cards.Exists(c => c.GetComponent<CardScript>().cardTypeID == "INC"));
	}

	[Test]
	public void MixedPool_ReservedCandidatesFromMergedPool()
	{
		// Rarity guarantee draws from the merged pool: the Rare utility card satisfies it.
		MakeCard(EnumStorage.UtilityKind.Income, EnumStorage.Rarity.Rare, typeId: "UTIL_R");
		MakeCard(rarity: EnumStorage.Rarity.Common, typeId: "PLAIN");
		var bonus = new UtilityShopBonus.Bonus();
		bonus.reservedSlots.Add(RaritySpec(EnumStorage.UtilityKind.RaritySlotR, 100, false));

		var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, bonus, 0, 0f, 0, 0, true, false, NewRng());
		Assert.AreEqual(1, board.cards.Count);
		Assert.AreEqual("UTIL_R", board.cards[0].GetComponent<CardScript>().cardTypeID);
	}

	[Test]
	public void MixedPool_OddsUtilityNotDuplicatedInMerge()
	{
		// OddsUtility sits in both classified pools; the merge must dedup by reference.
		// With equal weights and one slot, the ODDS share must stay ~1/2 (a doubled entry
		// would push it to ~2/3 - outside the bounds below).
		MakeCard(EnumStorage.UtilityKind.OddsUtility, typeId: "ODD");
		MakeCard(typeId: "PLAIN");
		int oddsPicks = 0;
		const int runs = 300;
		for (int i = 0; i < runs; i++)
		{
			var board = ShopBoardPipeline.GenerateBoard(_created, s => 1f, null, 0, 0f, 1, 0, true, false, new System.Random(i));
			Assert.AreEqual(1, board.cards.Count);
			if (board.cards[0].GetComponent<CardScript>().cardTypeID == "ODD") oddsPicks++;
		}
		Assert.Greater(oddsPicks, runs * 35 / 100, "ODDS share far below 1/2: something is filtering the pool");
		Assert.Less(oddsPicks, runs * 65 / 100, "ODDS share far above 1/2: the merge likely doubled the exempt card");
	}
}
