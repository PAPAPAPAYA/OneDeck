using System.Collections.Generic;
using System.Linq;
using DefaultNamespace.Managers;
using UnityEngine;

/// <summary>
/// Static solver for CardSelector (plan-card-selector-targeting-2026-09-01.md §3.2), in the
/// DeckCascadeLayout tradition: no lifecycle, unit-testable. Combat state is read-only —
/// the deck snapshot is passed in by the caller; revealZone is read via CombatManager.Me
/// (existing singleton convention). Builds ordered target pools only: no events, no
/// animation capture, no deck mutation — movement/animation/event sequencing stays in the
/// calling effect classes (BuryChosenCards / StageChosenCards / ReviveChosenCards shapes).
///
/// Zone membership (2026-10-06 calibration): predicates are defined over DECK MEMBERS only.
/// GraveSide = 0 &lt;= index &lt; startCardIndex; DeckSide = index &gt; startCardIndex; Anywhere =
/// any deck member. A candidate absent from the deck (IndexOf == -1: revealed / exiled /
/// destroyed) matches NO zone — never reuse IsCardBelowStartCard's comparison as a zone
/// test, because -1 &lt; startCardIndex lumps deck-external cards into the grave side. The
/// Start Card itself sits at index == startCardIndex, in neither zone; excludeNeutral
/// catches it in Anywhere pools. No Start Card (startCardIndex == -1): GraveSide = empty
/// pool (Revive fizzle parity); DeckSide / Anywhere = boundary inert, whole deck eligible.
///
/// Sort order is fixed: shuffle first, then a stable OrderBy — ties keep their shuffled
/// (random) order, replicating ReviveEffect.SortOrShufflePool. KeepOrder skips both.
/// </summary>
public static class CardSelectorSolver
{
	/// <summary>
	/// Build the target pool for spec. Returns the ordered pool (caller takes the first N).
	/// startCardIndex reports the Start Card position (-1 when absent) for caller-side logic.
	/// </summary>
	public static List<GameObject> Select(List<GameObject> combinedDeck, CardScript source,
		CardSelector spec, out int startCardIndex)
	{
		startCardIndex = FindStartCardIndex(combinedDeck);
		var pool = new List<GameObject>();
		if (combinedDeck != null) pool.AddRange(combinedDeck); // snapshot; caller's list untouched
		int deckCount = pool.Count;

		for (int i = pool.Count - 1; i >= 0; i--)
		{
			var card = pool[i];
			var cs = card != null ? card.GetComponent<CardScript>() : null;
			int index = combinedDeck != null ? combinedDeck.IndexOf(card) : -1;

			if (!PassesZone(spec.zone, index, startCardIndex)
				|| !PassesFixedBlock(spec, cs, index, deckCount, isDeckMember: true)
				|| !PassesSide(spec, cs, source)
				|| (spec.excludeSelf && source != null && card == source.gameObject)
				|| !PassesPredicates(spec, cs))
			{
				pool.RemoveAt(i);
			}
		}

		if (spec.includeRevealZone)
		{
			TryAppendRevealCard(pool, source, spec, deckCount);
		}

		return ApplySort(pool, spec.sort);
	}

	/// <summary>
	/// Positional walk from the source card toward index 0 (BuryNextXCards /
	/// GiveStatusEffectToLastXCards family) — a selection strategy, not a predicate pool.
	/// Applies the fixed exclusion block plus spec predicates per visited card; side, zone
	/// and excludeTopSlot do NOT apply (the walk's reach is bounded by lowerBound instead).
	/// The source itself is never special-cased: normal sources start below their own index,
	/// a revealed source is not a deck member, and a passive source is dropped by
	/// spec.excludePassive — callers keep those semantics through the spec.
	/// </summary>
	/// <param name="lowerBound">Inclusive walk floor: BuryNextXCards passes startCardIndex
	/// (grave boundary), LastX-family callers pass 0 (no boundary).</param>
	/// <param name="passiveSourceStartsFromTop">BuryNextXCards-specific rule
	/// (RELIC_CHAIN_BURIAL): passives live below the Start Card permanently, so their
	/// "bury N from deck top" targets the live-zone top. LastX-family callers pass false —
	/// a passive source then walks from its own position deeper into the grave, as legacy does.</param>
	public static List<GameObject> WalkFromSource(List<GameObject> combinedDeck, CardScript source,
		CardSelector spec, int amount, int lowerBound, bool passiveSourceStartsFromTop)
	{
		var result = new List<GameObject>();
		if (amount <= 0 || combinedDeck == null || combinedDeck.Count == 0 || source == null) return result;

		int startIndex;
		var cm = CombatManager.Me;
		bool sourceIsReveal = cm != null && cm.revealZone == source.gameObject;
		if (sourceIsReveal || (source.isPassive && passiveSourceStartsFromTop))
		{
			startIndex = combinedDeck.Count - 1;
		}
		else
		{
			int currentIndex = combinedDeck.IndexOf(source.gameObject);
			if (currentIndex < 0) return result; // deck-external, non-reveal source → fizzle
			startIndex = currentIndex - 1;
		}

		int deckCount = combinedDeck.Count;
		for (int i = startIndex; i >= lowerBound && i >= 0 && result.Count < amount; i--)
		{
			var targetCard = combinedDeck[i];
			var cs = targetCard != null ? targetCard.GetComponent<CardScript>() : null;
			if (!PassesFixedBlock(spec, cs, i, deckCount, isDeckMember: true)) continue;
			if (!PassesPredicates(spec, cs)) continue;
			result.Add(targetCard);
		}
		return result;
	}

	private static int FindStartCardIndex(List<GameObject> combinedDeck)
	{
		if (combinedDeck == null) return -1;
		for (int i = 0; i < combinedDeck.Count; i++)
		{
			var card = combinedDeck[i];
			if (card == null) continue;
			var cs = card.GetComponent<CardScript>();
			if (cs != null && cs.isStartCard) return i;
		}
		return -1;
	}

	private static bool PassesZone(CardSelector.SelectorZone zone, int index, int startCardIndex)
	{
		if (index < 0) return false; // deck-external candidates match no zone (see class header)
		switch (zone)
		{
			case CardSelector.SelectorZone.Anywhere: return true;
			case CardSelector.SelectorZone.GraveSide:
				return startCardIndex >= 0 && index < startCardIndex;
			case CardSelector.SelectorZone.DeckSide:
				return startCardIndex < 0 || index > startCardIndex;
			default: return false;
		}
	}

	private static bool PassesFixedBlock(CardSelector spec, CardScript cs, int index, int deckCount, bool isDeckMember)
	{
		if (cs == null) return false;
		if (spec.excludeNeutral && CombatManager.ShouldSkipEffectProcessing(cs)) return false;
		if (spec.excludePassive && cs.isPassive) return false;
		if (spec.minionMode == CardSelector.SelectorMinion.Exclude && cs.isMinion) return false;
		if (spec.minionMode == CardSelector.SelectorMinion.Only && !cs.isMinion) return false;
		if (isDeckMember)
		{
			if (spec.excludeBottomSlot && index == 0) return false;
			if (spec.excludeTopSlot && index == deckCount - 1) return false;
		}
		return true;
	}

	private static bool PassesSide(CardSelector spec, CardScript cs, CardScript source)
	{
		if (spec.side == CardSelector.SelectorSide.Both) return true;
		if (cs == null || cs.myStatusRef == null || source == null || source.myStatusRef == null) return false;
		bool sameSide = cs.myStatusRef == source.myStatusRef;
		return spec.side == CardSelector.SelectorSide.Friendly ? sameSide : !sameSide;
	}

	private static bool PassesPredicates(CardSelector spec, CardScript cs)
	{
		if (cs == null) return false;
		// Fatigue wildcard (2026-09-16): bypasses ONLY the type lines so revive loops keep
		// surfacing fatigue cards; every other gate still applies (ReviveEffect parity).
		if (!cs.wildcardTypeFilter)
		{
			switch (spec.creatureFilter)
			{
				case EffectScript.EffectCreatureFilter.Creature:
					if (!cs.IsCreature) return false;
					break;
				case EffectScript.EffectCreatureFilter.Phenomenon:
					if (cs.cardType != EnumStorage.CardType.None) return false;
					break;
				case EffectScript.EffectCreatureFilter.Token:
					if (cs.cardType != EnumStorage.CardType.Token) return false;
					break;
				case EffectScript.EffectCreatureFilter.Damager:
					if (!(cs.IsCreature || cs.HasAttackAttribute)) return false;
					break;
			}
		}
		if (spec.enhancedOnly && cs.attackGrowth <= 0) return false;
		if (spec.positiveAttackOnly && cs.GetAttack() <= 0) return false;
		if (!string.IsNullOrEmpty(spec.typeIDFilter) && cs.cardTypeID != spec.typeIDFilter) return false;
		if (spec.rarityFilter != CardSelector.SelectorRarity.Any)
		{
			var wanted = spec.rarityFilter == CardSelector.SelectorRarity.Common ? EnumStorage.Rarity.Common
				: spec.rarityFilter == CardSelector.SelectorRarity.Uncommon ? EnumStorage.Rarity.Uncommon
				: EnumStorage.Rarity.Rare;
			if (cs.rarity != wanted) return false;
		}
		if (spec.tagFilter != null && spec.tagFilter.Count > 0
			&& (cs.myTags == null || !cs.myTags.Any(spec.tagFilter.Contains))) return false;
		return true;
	}

	/// <summary>
	/// Giver/picker semantics: merge the revealed card into the pool with dedup. The appended
	/// card passes the same fixed block / side / predicate gates as deck members; zone is
	/// bypassed because the reveal card is a deck-external candidate by definition. The
	/// excludeSelf interaction follows GiveStatusEffect: the revealed SOURCE card only enters
	/// when excludeSelf is false.
	/// </summary>
	private static void TryAppendRevealCard(List<GameObject> pool, CardScript source, CardSelector spec, int deckCount)
	{
		var cm = CombatManager.Me;
		var reveal = cm != null ? cm.revealZone : null;
		if (reveal == null) return;
		if (spec.excludeSelf && source != null && reveal == source.gameObject) return;

		var revealCs = reveal.GetComponent<CardScript>();
		if (revealCs == null) return;
		if (pool.Contains(reveal)) return;
		if (!PassesFixedBlock(spec, revealCs, -1, deckCount, isDeckMember: false)) return;
		if (!PassesSide(spec, revealCs, source)) return;
		if (!PassesPredicates(spec, revealCs)) return;
		pool.Add(reveal);
	}

	private static List<GameObject> ApplySort(List<GameObject> pool, CardSelector.SelectorSort sort)
	{
		switch (sort)
		{
			case CardSelector.SelectorSort.KeepOrder:
				return pool;
			case CardSelector.SelectorSort.MaxAttack:
				return UtilityFuncManagerScript.ShuffleList(pool)
					.OrderByDescending(c => c.GetComponent<CardScript>().GetAttack()).ToList();
			case CardSelector.SelectorSort.MinAttack:
				return UtilityFuncManagerScript.ShuffleList(pool)
					.OrderBy(c => c.GetComponent<CardScript>().GetAttack()).ToList();
			case CardSelector.SelectorSort.MaxExtraAttackTimes:
				return UtilityFuncManagerScript.ShuffleList(pool)
					.OrderByDescending(c => c.GetComponent<CardScript>().extraAttackTimes).ToList();
			default: // Random
				return UtilityFuncManagerScript.ShuffleList(pool);
		}
	}
}
