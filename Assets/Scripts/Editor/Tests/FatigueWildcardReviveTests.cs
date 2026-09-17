using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode tests for the fatigue revive wildcard (2026-09-16,
/// plans/plan-fatigue-revive-wildcard-2026-09-16.md): SYSTEM_FATIGUE (cardType None +
/// wildcardTypeFilter) passes any ReviveEffect CreatureFilter type line so revive loops
/// keep surfacing it as self-recycling ammo; enhancement gates and the Bury/Stage filters
/// are untouched by the flag.
/// </summary>
public class FatigueWildcardReviveTests : HeadlessCombatTestFixture
{
	[Test]
	public void WildcardFatigue_PassesCreatureFilter()
	{
		var fatigue = CreateCard(true, "FatigueCarrier");
		fatigue.GetComponent<CardScript>().wildcardTypeFilter = true;
		CombatManager.combinedDeckZone.Add(fatigue); // index 0 = grave side
		CombatManager.combinedDeckZone.Add(CreateStartCard());

		var reviverCard = CreateCard(true, "Reviver");
		var revive = CreateEffect<ReviveEffect>(reviverCard);
		revive.creatureFilter = ReviveEffect.CreatureFilter.Creature;

		EffectChainManager.MakeANewEffectRecorder(reviverCard, revive.gameObject);
		revive.ReviveMyCards(1);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(fatigue, CombatManager.combinedDeckZone[CombatManager.combinedDeckZone.Count - 1],
			"wildcard fatigue passes the Creature type line and is revived to the deck top");
	}

	[Test]
	public void FatigueWithoutFlag_StillExcluded()
	{
		var fatigue = CreateCard(true, "FatigueNoFlag");
		CombatManager.combinedDeckZone.Add(fatigue); // index 0 = grave side
		CombatManager.combinedDeckZone.Add(CreateStartCard());

		var reviverCard = CreateCard(true, "Reviver");
		var revive = CreateEffect<ReviveEffect>(reviverCard);
		revive.creatureFilter = ReviveEffect.CreatureFilter.Creature;

		EffectChainManager.MakeANewEffectRecorder(reviverCard, revive.gameObject);
		revive.ReviveMyCards(1);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(fatigue, CombatManager.combinedDeckZone[0],
			"without the flag the None-type fatigue card stays excluded from the Creature revive pool (default behavior regression guard)");
	}

	[Test]
	public void WildcardFatigue_StillBlockedByOnlyEnhanced()
	{
		var fatigue = CreateCard(true, "FatigueCarrier");
		fatigue.GetComponent<CardScript>().wildcardTypeFilter = true;
		fatigue.GetComponent<CardScript>().attackGrowth = 0;
		CombatManager.combinedDeckZone.Add(fatigue);
		CombatManager.combinedDeckZone.Add(CreateStartCard());

		var reviverCard = CreateCard(true, "EliteReviver");
		var revive = CreateEffect<ReviveEffect>(reviverCard);
		revive.creatureFilter = ReviveEffect.CreatureFilter.Creature;
		revive.onlyEnhanced = true;

		EffectChainManager.MakeANewEffectRecorder(reviverCard, revive.gameObject);
		revive.ReviveMyCards(1);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(fatigue, CombatManager.combinedDeckZone[0],
			"the wildcard bypasses ONLY the type lines: onlyEnhanced still blocks a 0-growth fatigue card (ELITE_REVIVER semantics kept)");
	}

	[Test]
	public void WildcardFatigue_StageCreatureFilterStillExcludes()
	{
		var fatigue = CreateCard(true, "FatigueCarrier");
		fatigue.GetComponent<CardScript>().wildcardTypeFilter = true;
		CombatManager.combinedDeckZone.Add(fatigue); // index 0, grave side AND None type
		CombatManager.combinedDeckZone.Add(CreateStartCard());
		var filler = CreateCard(true, "TopFiller");
		CombatManager.combinedDeckZone.Add(filler); // top slot, excluded by IsCardAtTop

		var stagerCard = CreateCard(true, "Stager");
		var stage = CreateEffect<StageEffect>(stagerCard);
		stage.creatureFilter = EffectScript.EffectCreatureFilter.Creature;

		int deckCount = CombatManager.combinedDeckZone.Count;
		EffectChainManager.MakeANewEffectRecorder(stagerCard, stage.gameObject);
		stage.StageMyCards(1);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(fatigue, CombatManager.combinedDeckZone[0],
			"the wildcard is consulted only by ReviveEffect: Stage's Creature filter still excludes the fatigue card");
		Assert.AreEqual(deckCount, CombatManager.combinedDeckZone.Count, "nothing eligible to stage: deck unchanged");
	}
}
