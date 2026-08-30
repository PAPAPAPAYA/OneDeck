using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dynamic attack source for "attack = Y (常态结算)" cards: ALL_FOR_ONE, FLESH_COMBINATION,
/// ALMIGHTY, and future 镜面诅咒 / 咒蚀之眠 / 战争英雄 / 王牌.
/// Injects a live resolver into CardScript.SetAttackResolver; the resolver sums the
/// configured terms every time GetAttack() is called, so the card face display and the
/// damage settlement share the same entry point. Terms may read enemy-side aggregates
/// (enemy negative/curse attack), which is what makes enemy-side 常态结算 possible.
///
/// Aggregation semantics: sum/highest terms never include the carrier itself (the exclusion
/// lives at the GetAttack()-calling aggregating sites, not in card collection, so count
/// terms keep counting the carrier when it is a valid member); resolver graphs are
/// cycle-safe via CardScript.GetAttack()'s reentrancy guard (reentry resolves to base).
/// </summary>
public class AttackResolverSource : MonoBehaviour
{
	public enum Source
	{
		/// <summary>Sum of all friendly cards' attack (人人为我).</summary>
		FriendlyCardTotal,
		/// <summary>Number of friendly cards (血肉聚集体).</summary>
		FriendlyCardCount,
		/// <summary>Friendly cards below the start card, i.e. the buried "graveyard" (全能人 / 尸眠).</summary>
		GraveyardFriendlyCount,
		/// <summary>Friendly cards with the rift cardTypeID (全能人 友方[次元裂缝]数).</summary>
		FriendlyRiftCount,
		/// <summary>Sum of attack on enemy cards with the negative/curse cardTypeID (镜面诅咒 / 全能人).</summary>
		EnemyNegativeTotal,
		/// <summary>Highest attack among enemy cards with the negative/curse cardTypeID (咒蚀之眠).</summary>
		EnemyNegativeHighest,
		/// <summary>Highest attack among friendly cards, carrier excluded (MIMIC_BLADE 攻击力=友方最高攻击力).</summary>
		FriendlyHighest,
		/// <summary>This round's friendly revives (REANIMATOR 攻击力=本回合复活友方数).</summary>
		RevivedFriendlyThisRoundCount,
	}

	[System.Serializable]
	public class Term
	{
		[Tooltip("Aggregate source for this term")]
		public Source source;
		[Tooltip("Card type ID for FriendlyRiftCount / EnemyNegativeTotal / EnemyNegativeHighest (e.g. RIFT / JU_ON)")]
		public string cardTypeID;
	}

	[Tooltip("Terms summed live by the resolver; order does not matter")]
	public List<Term> terms = new List<Term>();

	private CardScript _cardScript;
	private CombatManager _combatManager;

	private void OnEnable()
	{
		RefreshAttackResolver();
	}

	/// <summary>
	/// (Re)bind the live resolver. Called by OnEnable; also callable after manual setup —
	/// EditMode tests add components at runtime where lifecycle callbacks do not fire.
	/// NOTE: myStatusRef is assigned by CardFactory.CreateLogicalCard AFTER Instantiate,
	/// so the bind must not depend on it; the faction-relative terms are evaluated at
	/// Resolve() time, when statusRef is already populated.
	/// </summary>
	public void RefreshAttackResolver()
	{
		_cardScript = GetComponent<CardScript>();
		if (_cardScript == null) return;
		_combatManager = CombatManager.Me;
		_cardScript.SetAttackResolver(Resolve);
	}

	/// <summary>
	/// Unbind the live resolver. Called by OnDisable; also callable to simulate it.
	/// </summary>
	public void ClearAttackResolver()
	{
		if (_cardScript != null)
		{
			_cardScript.SetAttackResolver(null);
		}
	}

	private void OnDisable()
	{
		ClearAttackResolver();
	}

	private int Resolve()
	{
		if (terms == null || terms.Count == 0 || _combatManager == null) return 0;

		int total = 0;
		foreach (var term in terms)
		{
			if (term == null) continue;
			switch (term.source)
			{
				case Source.FriendlyCardTotal:
					total += SumCardsWithAttack(myCardFaction: true, typeID: null);
					break;
				case Source.FriendlyCardCount:
					total += CountCards(myCardFaction: true, typeID: null);
					break;
				case Source.GraveyardFriendlyCount:
					total += CountGraveyardCards();
					break;
				case Source.FriendlyRiftCount:
					total += CountCards(myCardFaction: true, typeID: term.cardTypeID);
					break;
				case Source.EnemyNegativeTotal:
					total += SumCardsWithAttack(myCardFaction: false, typeID: term.cardTypeID);
					break;
				case Source.EnemyNegativeHighest:
					total += HighestAttack(myCardFaction: false, typeID: term.cardTypeID);
					break;
				case Source.FriendlyHighest:
					total += HighestAttack(myCardFaction: true, typeID: null);
					break;
				case Source.RevivedFriendlyThisRoundCount:
					total += RevivedFriendlyCount();
					break;
			}
		}
		return total;
	}

	/// <summary>
	/// Sum GetAttack() over deck + reveal cards of the requested faction (relative to this
	/// card's owner), optionally restricted to a cardTypeID. Neutral/start cards are skipped.
	/// The carrier itself is never included — a "sum of friendly attack" reads OTHER cards.
	/// </summary>
	private int SumCardsWithAttack(bool myCardFaction, string typeID)
	{
		int total = 0;
		foreach (var card in CollectCards(myCardFaction, typeID))
		{
			if (card == _cardScript) continue;
			total += card.GetAttack();
		}
		return total;
	}

	/// <summary>
	/// Count deck + reveal cards of the requested faction (relative to this card's owner),
	/// optionally restricted to a cardTypeID. Neutral/start cards are skipped.
	/// </summary>
	private int CountCards(bool myCardFaction, string typeID)
	{
		return CollectCards(myCardFaction, typeID).Count;
	}

	private List<CardScript> CollectCards(bool myCardFaction, string typeID)
	{
		var result = new List<CardScript>();
		var deck = _combatManager.combinedDeckZone;
		if (deck != null)
		{
			foreach (var cardObj in deck)
			{
				if (cardObj == null) continue;
				var cardScript = cardObj.GetComponent<CardScript>();
				if (!CardMatches(cardScript, myCardFaction, typeID)) continue;
				result.Add(cardScript);
			}
		}
		if (_combatManager.revealZone != null)
		{
			var revealCardScript = _combatManager.revealZone.GetComponent<CardScript>();
			if (CardMatches(revealCardScript, myCardFaction, typeID) && !result.Contains(revealCardScript))
			{
				result.Add(revealCardScript);
			}
		}
		return result;
	}

	private bool CardMatches(CardScript cardScript, bool myCardFaction, string typeID)
	{
		if (cardScript == null) return false;
		if (CombatManager.ShouldSkipEffectProcessing(cardScript)) return false;
		if (myCardFaction)
		{
			if (cardScript.myStatusRef != _cardScript.myStatusRef) return false;
		}
		else
		{
			if (cardScript.myStatusRef == _cardScript.myStatusRef) return false;
		}
		if (!string.IsNullOrEmpty(typeID) && cardScript.cardTypeID != typeID) return false;
		return true;
	}

	/// <summary>
	/// Count friendly cards with index below the start card (the buried "graveyard"),
	/// mirroring ValueTrackerManager's grave tracker but relative to this card's faction.
	/// </summary>
	private int CountGraveyardCards()
	{
		var deck = _combatManager.combinedDeckZone;
		if (deck == null) return 0;

		int startCardIndex = -1;
		for (int i = 0; i < deck.Count; i++)
		{
			var cardScript = deck[i].GetComponent<CardScript>();
			if (cardScript != null && cardScript.isStartCard)
			{
				startCardIndex = i;
				break;
			}
		}
		if (startCardIndex < 0) return 0;

		int count = 0;
		for (int i = 0; i < startCardIndex; i++)
		{
			var cardScript = deck[i].GetComponent<CardScript>();
			if (cardScript != null && !CombatManager.ShouldSkipEffectProcessing(cardScript) &&
			    cardScript.myStatusRef == _cardScript.myStatusRef)
			{
				count++;
			}
		}
		return count;
	}

	/// <summary>
	/// Highest GetAttack() among enemy cards of the given cardTypeID (0 when none eligible).
	/// The carrier itself is never a candidate (it is friendly for these terms anyway).
	/// </summary>
	private int HighestAttack(bool myCardFaction, string typeID)
	{
		int highest = 0;
		foreach (var card in CollectCards(myCardFaction, typeID))
		{
			if (card == _cardScript) continue;
			int attack = card.GetAttack();
			if (attack > highest) highest = attack;
		}
		return highest;
	}

	/// <summary>
	/// This round's friendly revive count, read from ValueTrackerManager (REANIMATOR).
	/// </summary>
	private int RevivedFriendlyCount()
	{
		var tracker = ValueTrackerManager.me;
		if (tracker == null || _cardScript == null || _combatManager == null) return 0;
		var refValue = _cardScript.myStatusRef == _combatManager.ownerPlayerStatusRef
			? tracker.ownerRevivedCountThisRoundRef
			: tracker.enemyRevivedCountThisRoundRef;
		return refValue != null ? refValue.value : 0;
	}
}
