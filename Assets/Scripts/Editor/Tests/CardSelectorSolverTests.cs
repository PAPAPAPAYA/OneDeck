using System.Collections.Generic;
using System.Linq;
using DefaultNamespace;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode tests for CardSelector + CardSelectorSolver (plan-card-selector-targeting-2026-09-01.md
/// §4 Step 1). Deck layout convention: indices 0..startCardIndex-1 = grave side, then the
/// Start Card, then the live zone. Each golden from the plan's Step 1 list has a test here.
/// </summary>
public class CardSelectorSolverTests : HeadlessCombatTestFixture
{
	private CardScript _source;

	public override void SetUp()
	{
		base.SetUp();
		_source = CreateCard(true, "Source").GetComponent<CardScript>();
	}

	private static CardSelector Spec()
	{
		return new CardSelector();
	}

	/// <summary>Assert pool contents by card name, order-insensitive.</summary>
	private static void AssertPool(System.Collections.Generic.IEnumerable<string> expected, List<GameObject> pool)
	{
		CollectionAssert.AreEquivalent(expected, pool.Select(c => c.name), "pool: " + string.Join(",", pool.Select(c => c.name)));
	}

	private static void AssertOrder(List<string> expected, List<GameObject> pool)
	{
		CollectionAssert.AreEqual(expected, pool.Select(c => c.name).ToList(), "pool order: " + string.Join(",", pool.Select(c => c.name)));
	}

	// ---------- Zone membership / Start Card boundary ----------

	[Test]
	public void Zone_GraveSide_And_DeckSide_SplitAtStartCard()
	{
		var grave = CreateCard(true, "Grave");
		var start = CreateStartCard();
		var live = CreateCard(true, "Live");
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { grave, start, live });

		var graveSpec = Spec(); graveSpec.zone = CardSelector.SelectorZone.GraveSide;
		int sci;
		AssertPool(new[] { "Grave" }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, graveSpec, out sci));
		Assert.AreEqual(1, sci, "startCardIndex out param");

		var deckSpec = Spec(); deckSpec.zone = CardSelector.SelectorZone.DeckSide;
		AssertPool(new[] { "Live" }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, deckSpec, out sci));

		var anywhereSpec = Spec(); anywhereSpec.zone = CardSelector.SelectorZone.Anywhere;
		AssertPool(new[] { "Grave", "Live" }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, anywhereSpec, out sci));
	}

	[Test]
	public void Zone_StartCardItself_InNeitherGraveNorDeckSide()
	{
		var start = CreateStartCard();
		var live = CreateCard(true, "Live");
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { start, live });

		var graveSpec = Spec(); graveSpec.zone = CardSelector.SelectorZone.GraveSide;
		int sci;
		AssertPool(new string[] { }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, graveSpec, out sci));
		var deckSpec = Spec(); deckSpec.zone = CardSelector.SelectorZone.DeckSide;
		AssertPool(new[] { "Live" }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, deckSpec, out sci));
	}

	[Test]
	public void NoStartCard_GraveSideEmpty_DeckSideAndAnywhereFull()
	{
		var a = CreateCard(true, "A");
		var b = CreateCard(true, "B");
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { a, b });

		var graveSpec = Spec(); graveSpec.zone = CardSelector.SelectorZone.GraveSide;
		int sci;
		AssertPool(new string[] { }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, graveSpec, out sci));
		Assert.AreEqual(-1, sci, "no Start Card reports -1");

		var deckSpec = Spec();
		AssertPool(new[] { "A", "B" }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, deckSpec, out sci));
		var anywhereSpec = Spec(); anywhereSpec.zone = CardSelector.SelectorZone.Anywhere;
		AssertPool(new[] { "A", "B" }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, anywhereSpec, out sci));
	}

	[Test]
	public void RevealedCard_MatchesNoZone_UnlessExplicitlyIncluded()
	{
		var start = CreateStartCard();
		var live = CreateCard(true, "Live");
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { start, live });
		RevealTopCard(); // pops Live into revealZone — deck-external candidate (IndexOf == -1)

		int sci;
		var graveSpec = Spec(); graveSpec.zone = CardSelector.SelectorZone.GraveSide;
		AssertPool(new string[] { }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, graveSpec, out sci));

		var anywhereSpec = Spec(); anywhereSpec.zone = CardSelector.SelectorZone.Anywhere;
		AssertPool(new string[] { }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, anywhereSpec, out sci));

		var includeSpec = Spec(); includeSpec.zone = CardSelector.SelectorZone.Anywhere; includeSpec.includeRevealZone = true;
		AssertPool(new[] { "Live" }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, includeSpec, out sci));
	}

	// ---------- Fixed exclusion block ----------

	[Test]
	public void ExcludeNeutral_TogglesStartCardInOut()
	{
		var a = CreateCard(true, "A");
		var start = CreateStartCard();
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { a, start });

		var spec = Spec(); spec.zone = CardSelector.SelectorZone.Anywhere;
		int sci;
		AssertPool(new[] { "A" }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, spec, out sci));

		// A neutral card has myStatusRef == null, so it can never match a Friendly/Enemy side
		// filter (legacy parity: side comparisons drop it before excludeNeutral even matters).
		// Only a Both-side spec can actually admit it when excludeNeutral is off.
		spec.side = CardSelector.SelectorSide.Both;
		spec.excludeNeutral = false;
		AssertPool(new[] { "A", "StartCard" }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, spec, out sci));
	}

	[Test]
	public void ExcludePassive_And_MinionMode_ThreeStates()
	{
		var normal = CreateCard(true, "Normal");
		var passive = CreateCard(true, "Passive"); passive.GetComponent<CardScript>().isPassive = true;
		var minion = CreateMinion(true, "Minion");
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { passive, minion, normal });

		int sci;
		var spec = Spec(); spec.zone = CardSelector.SelectorZone.Anywhere;
		AssertPool(new[] { "Normal" }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, spec, out sci));

		spec.minionMode = CardSelector.SelectorMinion.Any;
		AssertPool(new[] { "Normal", "Minion" }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, spec, out sci));

		spec.minionMode = CardSelector.SelectorMinion.Only;
		AssertPool(new[] { "Minion" }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, spec, out sci));

		spec.minionMode = CardSelector.SelectorMinion.Any;
		spec.excludePassive = false;
		AssertPool(new[] { "Normal", "Minion", "Passive" }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, spec, out sci));
	}

	[Test]
	public void BoundarySlotExclusions_DropIndexZeroAndTop()
	{
		var bottom = CreateCard(true, "Bottom");
		var start = CreateStartCard();
		var mid = CreateCard(true, "Mid");
		var top = CreateCard(true, "Top");
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { bottom, start, mid, top });

		int sci;
		var spec = Spec(); spec.zone = CardSelector.SelectorZone.Anywhere; spec.excludeBottomSlot = true; spec.excludeTopSlot = true;
		AssertPool(new[] { "Mid" }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, spec, out sci));

		spec.excludeBottomSlot = false;
		AssertPool(new[] { "Bottom", "Mid" }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, spec, out sci));
	}

	[Test]
	public void ExcludeSelf_RemovesSourceWhenItIsADeckMember()
	{
		var start = CreateStartCard();
		var other = CreateCard(true, "Other");
		// _source itself is a deck member here (normally it is the out-of-deck revealed card).
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { start, _source.gameObject, other });

		var spec = Spec();
		int sci;
		AssertPool(new[] { "Other" }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, spec, out sci));

		spec.excludeSelf = false;
		AssertPool(new[] { "Source", "Other" }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, spec, out sci));
	}

	// ---------- Faction ----------

	[Test]
	public void Side_Friendly_Enemy_Both()
	{
		var my = CreateCard(true, "My");
		var their = CreateCard(false, "Their");
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { my, their });

		int sci;
		var spec = Spec(); spec.zone = CardSelector.SelectorZone.Anywhere;
		AssertPool(new[] { "My" }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, spec, out sci));

		spec.side = CardSelector.SelectorSide.Enemy;
		AssertPool(new[] { "Their" }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, spec, out sci));

		spec.side = CardSelector.SelectorSide.Both;
		AssertPool(new[] { "My", "Their" }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, spec, out sci));
	}

	// ---------- Predicates ----------

	[Test]
	public void CreatureFilter_FourCardTypeBuckets()
	{
		var creature = CreateCard(true, "Creature"); creature.GetComponent<CardScript>().cardType = EnumStorage.CardType.Creature;
		var phenomenon = CreateCard(true, "Phenomenon"); phenomenon.GetComponent<CardScript>().cardType = EnumStorage.CardType.None;
		var token = CreateCard(true, "Token"); token.GetComponent<CardScript>().cardType = EnumStorage.CardType.Token;
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { creature, phenomenon, token });

		int sci;
		var spec = Spec(); spec.zone = CardSelector.SelectorZone.Anywhere; spec.creatureFilter = EffectScript.EffectCreatureFilter.Creature;
		AssertPool(new[] { "Creature" }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, spec, out sci));

		spec.creatureFilter = EffectScript.EffectCreatureFilter.Phenomenon;
		AssertPool(new[] { "Phenomenon" }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, spec, out sci));

		spec.creatureFilter = EffectScript.EffectCreatureFilter.Token;
		AssertPool(new[] { "Token" }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, spec, out sci));
	}

	[Test]
	public void DamagerFilter_CreatureOrAttackHolder()
	{
		var creature = CreateCard(true, "Creature"); creature.GetComponent<CardScript>().cardType = EnumStorage.CardType.Creature;
		var curseWithAttack = CreateCard(true, "CurseWithAttack"); curseWithAttack.GetComponent<CardScript>().printedAttack = 2;
		var plainNone = CreateCard(true, "PlainNone"); plainNone.GetComponent<CardScript>().cardType = EnumStorage.CardType.None;
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { creature, curseWithAttack, plainNone });

		var spec = Spec(); spec.zone = CardSelector.SelectorZone.Anywhere;
		spec.creatureFilter = EffectScript.EffectCreatureFilter.Damager;
		int sci;
		AssertPool(new[] { "Creature", "CurseWithAttack" }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, spec, out sci));
	}

	[Test]
	public void WildcardTypeFilter_BypassesTypeLineOnly()
	{
		var wild = CreateCard(true, "Wild");
		wild.GetComponent<CardScript>().cardType = EnumStorage.CardType.None;
		wild.GetComponent<CardScript>().wildcardTypeFilter = true;
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { wild });

		int sci;
		var spec = Spec(); spec.zone = CardSelector.SelectorZone.Anywhere;
		spec.creatureFilter = EffectScript.EffectCreatureFilter.Creature;
		AssertPool(new[] { "Wild" }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, spec, out sci));

		wild.GetComponent<CardScript>().wildcardTypeFilter = true;
		wild.GetComponent<CardScript>().attackGrowth = 0;
		spec.enhancedOnly = true; // non-type gates still apply to wildcard cards
		AssertPool(new string[] { }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, spec, out sci));
	}

	[Test]
	public void EnhancedOnly_ZeroGrowthBoundary()
	{
		var grown = CreateCard(true, "Grown"); grown.GetComponent<CardScript>().attackGrowth = 1;
		var untouched = CreateCard(true, "Untouched"); untouched.GetComponent<CardScript>().attackGrowth = 0;
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { grown, untouched });

		var spec = Spec(); spec.zone = CardSelector.SelectorZone.Anywhere; spec.enhancedOnly = true;
		int sci;
		AssertPool(new[] { "Grown" }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, spec, out sci));
	}

	[Test]
	public void RarityAndTypeID_ExactMatch()
	{
		var common = CreateCard(true, "Common", "RIFT"); common.GetComponent<CardScript>().rarity = EnumStorage.Rarity.Common;
		var rare = CreateCard(true, "Rare", "JU_ON"); rare.GetComponent<CardScript>().rarity = EnumStorage.Rarity.Rare;
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { common, rare });

		var spec = Spec(); spec.zone = CardSelector.SelectorZone.Anywhere; spec.rarityFilter = CardSelector.SelectorRarity.Common;
		int sci;
		AssertPool(new[] { "Common" }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, spec, out sci));

		spec.rarityFilter = CardSelector.SelectorRarity.Any;
		spec.typeIDFilter = "JU_ON";
		AssertPool(new[] { "Rare" }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, spec, out sci));

		spec.typeIDFilter = "NOPE";
		AssertPool(new string[] { }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, spec, out sci));
	}

	[Test]
	public void TagFilter_AnyMatch_NullAndEmptyTolerated()
	{
		var tagged = CreateCard(true, "Tagged"); tagged.GetComponent<CardScript>().myTags.Add(EnumStorage.Tag.DeathRattle);
		var untagged = CreateCard(true, "Untagged");
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { tagged, untagged });

		var spec = Spec(); spec.zone = CardSelector.SelectorZone.Anywhere;
		spec.tagFilter = new List<EnumStorage.Tag> { EnumStorage.Tag.DeathRattle, EnumStorage.Tag.Linger };
		int sci;
		AssertPool(new[] { "Tagged" }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, spec, out sci));

		spec.tagFilter = new List<EnumStorage.Tag>();
		AssertPool(new[] { "Tagged", "Untagged" }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, spec, out sci));

		spec.tagFilter = null;
		AssertPool(new[] { "Tagged", "Untagged" }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, spec, out sci));
	}

	// ---------- Sort ----------

	[Test]
	public void Sort_MaxAttack_MinAttack_KeepOrder()
	{
		var a = CreateCard(true, "A"); a.GetComponent<CardScript>().printedAttack = 1;
		var b = CreateCard(true, "B"); b.GetComponent<CardScript>().printedAttack = 3;
		var c = CreateCard(true, "C"); c.GetComponent<CardScript>().printedAttack = 2;
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { a, b, c });

		int sci;
		var spec = Spec(); spec.zone = CardSelector.SelectorZone.Anywhere; spec.sort = CardSelector.SelectorSort.MaxAttack;
		AssertOrder(new List<string> { "B", "C", "A" }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, spec, out sci));

		spec.sort = CardSelector.SelectorSort.MinAttack;
		AssertOrder(new List<string> { "A", "C", "B" }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, spec, out sci));

		spec.sort = CardSelector.SelectorSort.KeepOrder;
		AssertOrder(new List<string> { "A", "B", "C" }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, spec, out sci));
	}

	[Test]
	public void Sort_MaxExtraAttackTimes_And_RandomKeepsSet()
	{
		var a = CreateCard(true, "A"); a.GetComponent<CardScript>().extraAttackTimes = 1;
		var b = CreateCard(true, "B"); b.GetComponent<CardScript>().extraAttackTimes = 4;
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { a, b });

		int sci;
		var spec = Spec(); spec.zone = CardSelector.SelectorZone.Anywhere; spec.sort = CardSelector.SelectorSort.MaxExtraAttackTimes;
		AssertOrder(new List<string> { "B", "A" }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, spec, out sci));

		spec.sort = CardSelector.SelectorSort.Random;
		AssertPool(new[] { "A", "B" }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, spec, out sci));
	}

	[Test]
	public void Sort_PositiveAttackOnly_MinAttackSkipsZeroAttack()
	{
		var zero = CreateCard(true, "Zero"); zero.GetComponent<CardScript>().printedAttack = 0;
		var low = CreateCard(true, "Low"); low.GetComponent<CardScript>().printedAttack = 2;
		var high = CreateCard(true, "High"); high.GetComponent<CardScript>().printedAttack = 3;
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { zero, low, high });

		var spec = Spec(); spec.zone = CardSelector.SelectorZone.Anywhere;
		spec.sort = CardSelector.SelectorSort.MinAttack; spec.positiveAttackOnly = true;
		int sci;
		AssertOrder(new List<string> { "Low", "High" }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, spec, out sci));
	}

	// ---------- includeRevealZone ----------

	[Test]
	public void IncludeRevealZone_AppendsWithDedup_AndAppliesFilters()
	{
		var start = CreateStartCard();
		var live = CreateCard(true, "Live");
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { start, live });
		RevealTopCard(); // pops Live into revealZone

		var spec = Spec(); spec.zone = CardSelector.SelectorZone.Anywhere; spec.includeRevealZone = true;
		int sci;
		AssertPool(new[] { "Live" }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, spec, out sci));

		// Filters apply to the appended card too: an enemy revealed card is dropped by side.
		var enemyReveal = CreateCard(false, "EnemyReveal");
		CombatManager.revealZone = enemyReveal;
		AssertPool(new string[] { }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, spec, out sci));
	}

	[Test]
	public void IncludeRevealZone_ExcludeSelfInteraction()
	{
		var start = CreateStartCard();
		var live = CreateCard(true, "Live");
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { start, live });
		// The source IS the revealed card (the normal on-reveal situation).
		_source = RevealTopCard();

		var spec = Spec(); spec.zone = CardSelector.SelectorZone.Anywhere; spec.includeRevealZone = true;
		spec.excludeSelf = true;
		int sci;
		AssertPool(new string[] { }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, spec, out sci));

		spec.excludeSelf = false;
		AssertPool(new[] { "Live" }, CardSelectorSolver.Select(CombatManager.combinedDeckZone, _source, spec, out sci));
	}

	// ---------- WalkFromSource ----------

	[Test]
	public void Walk_BoundedStopsAtStartCard()
	{
		var g2 = CreateCard(true, "G2");
		var g1 = CreateCard(true, "G1");
		var start = CreateStartCard();
		var l1 = CreateCard(true, "L1");
		var l2 = CreateCard(true, "L2");
		var source = CreateCard(true, "Source");
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { g1, g2, start, l1, l2, source });

		var spec = Spec();
		var pool = CardSelectorSolver.WalkFromSource(CombatManager.combinedDeckZone, source.GetComponent<CardScript>(), spec, 3, 2, passiveSourceStartsFromTop: true);
		// Walk order: L2 (index 4), L1 (index 3), then the boundary at index 2 stops it even with amount 3.
		AssertOrder(new List<string> { "L2", "L1" }, pool);
	}

	[Test]
	public void Walk_UnboundedDigsPastStartCard_SkipsNeutralAndBottom()
	{
		var g2 = CreateCard(true, "G2");
		var g1 = CreateCard(true, "G1");
		var start = CreateStartCard();
		var l1 = CreateCard(true, "L1");
		var source = CreateCard(true, "Source");
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { g1, g2, start, l1, source });

		var spec = Spec();
		spec.excludeBottomSlot = true; // BuryNextXCards parity: index 0 is never a walk target
		var pool = CardSelectorSolver.WalkFromSource(CombatManager.combinedDeckZone, source.GetComponent<CardScript>(), spec, 10, 0, passiveSourceStartsFromTop: false);
		// lowerBound 0 (LastX shape): digs past the Start Card (neutral-skipped); index 0 dropped by excludeBottomSlot.
		AssertOrder(new List<string> { "L1", "G2" }, pool);
	}

	[Test]
	public void Walk_RevealSourceStartsFromTop()
	{
		var start = CreateStartCard();
		var l1 = CreateCard(true, "L1");
		var l2 = CreateCard(true, "L2");
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { start, l1, l2 });
		var revealed = RevealTopCard(); // pops L2; walk must start at the new top (L1)

		var spec = Spec();
		var pool = CardSelectorSolver.WalkFromSource(CombatManager.combinedDeckZone, revealed, spec, 5, 0, passiveSourceStartsFromTop: false);
		AssertOrder(new List<string> { "L1" }, pool);
	}

	[Test]
	public void Walk_PassiveSource_BifurcatesOnFlag()
	{
		var g1 = CreateCard(true, "G1");
		var source = CreateCard(true, "Source"); source.GetComponent<CardScript>().isPassive = true;
		var start = CreateStartCard();
		var l1 = CreateCard(true, "L1");
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { g1, source, start, l1 });

		var spec = Spec();
		// BuryNextXCards rule (RELIC_CHAIN_BURIAL): passive source targets the live-zone top.
		var fromTop = CardSelectorSolver.WalkFromSource(CombatManager.combinedDeckZone, source.GetComponent<CardScript>(), spec, 1, 0, passiveSourceStartsFromTop: true);
		AssertOrder(new List<string> { "L1" }, fromTop);
		// LastX family rule: no passive rerouting — walks from its own position into the grave.
		var fromSelf = CardSelectorSolver.WalkFromSource(CombatManager.combinedDeckZone, source.GetComponent<CardScript>(), spec, 1, 0, passiveSourceStartsFromTop: false);
		AssertOrder(new List<string> { "G1" }, fromSelf);
	}

	[Test]
	public void Walk_DeckExternalNonRevealSource_Fizzles()
	{
		var start = CreateStartCard();
		var live = CreateCard(true, "Live");
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { start, live });

		// _source is not in the deck and not the reveal card (e.g. exiled mid-chain).
		var spec = Spec();
		var pool = CardSelectorSolver.WalkFromSource(CombatManager.combinedDeckZone, _source, spec, 3, 0, passiveSourceStartsFromTop: false);
		Assert.AreEqual(0, pool.Count, "out-of-deck, non-reveal source must fizzle");
	}
}
