using System;
using System.Collections.Generic;
using DefaultNamespace;
using UnityEngine;

/// <summary>
/// Unified target-selection spec for card effects (plan-card-selector-targeting-2026-09-01.md §3.1).
/// Pure serializable data — all resolution (faction relativity, zone membership, deck lookups)
/// happens in CardSelectorSolver at evaluation time against live combat state, mirroring the
/// AttackResolverSource "resolve relativity at evaluation" precedent.
/// Effects adopt it additively: `useTargetSelector` (default false) + `targetSelector`; the
/// hardcoded differences between entry methods of one component (exclusion sets, sort,
/// keep-order, take-all) are layered as spec patches inside each entry method per the
/// plan §3.3 construction mode. Count stays in entry-method parameters — never here.
/// </summary>
[System.Serializable]
public class CardSelector
{
	public enum SelectorSide { Friendly, Enemy, Both }
	public enum SelectorZone { Anywhere, GraveSide, DeckSide }
	public enum SelectorRarity { Any, Common, Uncommon, Rare }
	public enum SelectorSort { Random, KeepOrder, MaxAttack, MinAttack, MaxExtraAttackTimes }
	public enum SelectorMinion { Exclude, Any, Only }

	[Tooltip("Faction resolved against the source card's myStatusRef at solve time")]
	public SelectorSide side = SelectorSide.Friendly;
	public SelectorZone zone = SelectorZone.DeckSide;
	public SelectorMinion minionMode = SelectorMinion.Exclude;
	public EffectScript.EffectCreatureFilter creatureFilter = EffectScript.EffectCreatureFilter.Any;
	public SelectorRarity rarityFilter = SelectorRarity.Any;
	public SelectorSort sort = SelectorSort.Random;
	[Tooltip("Empty = no filter (exact cardTypeID match)")]
	public string typeIDFilter = "";
	[Tooltip("True = only cards with attackGrowth > 0 (enhanced, derived — never a tag)")]
	public bool enhancedOnly = false;
	[Tooltip("True = only cards with GetAttack() > 0 (min-attack pickers)")]
	public bool positiveAttackOnly = false;
	[Tooltip("Any-match tag filter; null/empty = no filter (legacy tagsToCheck semantics)")]
	public List<EnumStorage.Tag> tagFilter;
	public bool excludeSelf = true;

	// Fixed exclusion block. Legacy entry methods differ per method — Step 2-4 adoption must
	// map each entry method's set from the plan appendix A parity table, not one blanket default.
	public bool excludeNeutral = true;     // ShouldSkipEffectProcessing (Start Card / neutral)
	public bool excludePassive = true;     // isPassive (4.0 immovable passives)
	public bool excludeBottomSlot = false; // index 0 (Bury family atBottom)
	public bool excludeTopSlot = false;    // index Count-1 (Stage family atTop)
	[Tooltip("True = merge the reveal-zone card into the pool with dedup (Giver/picker semantics; it still passes side + predicates + fixed exclusions). False = never added (the revealed card is not a deck member anyway).")]
	public bool includeRevealZone = false;
}
