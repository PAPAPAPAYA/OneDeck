using System.Collections.Generic;
using System.Linq;
using DefaultNamespace;
using DefaultNamespace.Managers;
using DefaultNamespace.SOScripts;
using UnityEngine;

/// <summary>
/// 4.0 revive/awaken engine (plans/plan-4.0-revive-awaken-2026-08-29.md).
/// Revive = grave -> deck top (or Start Card tail for delayed revive); raises the awaken
/// event family. Selection is grave-only: index < startCardIndex, excluding neutral cards,
/// minions, passive cards (isPassive) and the reveal-zone card. Empty grave = fizzle.
/// 苏醒 semantics: onMeRevived is raised ONLY from ReviveChosenCards — Stage / bounce / R2
/// never trigger the awaken family.
/// </summary>
public class ReviveEffect : EffectScript
{
	public enum ReviveTargetSide { MyCards, TheirCards }
	public enum CreatureFilter { Any, Creature, NonCreature }
	public enum ReviveSortBy { None, MaxAttack, MaxExtraAttackTimes }
	public enum ReviveRarityFilter { Any, Common, Uncommon, Rare }

	private List<GameObject> _combinedDeck;

	[Header("Selection Configuration")]
	[Tooltip("Side used by the generic selection; the ReviveMy*/ReviveTheir* entry points pick it explicitly")]
	public ReviveTargetSide reviveTargetSide = ReviveTargetSide.MyCards;
	[Tooltip("Any / Creature only / NonCreature only")]
	public CreatureFilter creatureFilter = CreatureFilter.Any;
	[Tooltip("Non-empty filters by exact cardTypeID (e.g. RIFT for believers)")]
	public string typeIDFilter = "";
	[Tooltip("None = random order; MaxAttack / MaxExtraAttackTimes pick the highest (stable sort over a shuffled pool, ties stay random)")]
	public ReviveSortBy sortBy = ReviveSortBy.None;
	[Tooltip("Filters by the prefab rarity field")]
	public ReviveRarityFilter rarityFilter = ReviveRarityFilter.Any;
	[Tooltip("True = land at the Start Card tail (index startCardIndex + 1, R2-bounce slot) instead of the deck top")]
	public bool delayedRevive = false;
	[Tooltip("True = only cards with permanent attack growth > 0 (4.0 【被强化】) are eligible (ELITE_REVIVER)")]
	public bool onlyEnhanced = false;

	[Header("Tag Configuration")]
	public List<EnumStorage.Tag> tagsToCheck;

	[Header("Self Exclusion")]
	[Tooltip("If true, the source card will not be selected when reviving multiple cards")]
	public bool excludeSelf = true;

	/// <summary>
	/// Get card owner's color tag (delegates to base palette-aware helper)
	/// </summary>
	private string GetCardColorTag(GameObject card)
	{
		var cardStatus = card.GetComponent<CardScript>().myStatusRef;
		return GetCardOwnerColor(cardStatus);
	}

	/// <summary>
	/// Get current card's color tag
	/// </summary>
	private string GetMyCardColorTag()
	{
		return GetMyCardOwnerColor();
	}

	/// <summary>
	/// Get card's index in combinedDeck
	/// </summary>
	private int GetCardIndexInCombinedDeck(GameObject card)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		return _combinedDeck.IndexOf(card);
	}

	/// <summary>
	/// Find the current index of the Start Card in combinedDeck.
	/// Returns -1 if no Start Card is present.
	/// </summary>
	private int GetStartCardIndex()
	{
		_combinedDeck = combatManager.combinedDeckZone;
		for (int i = 0; i < _combinedDeck.Count; i++)
		{
			var cardScript = _combinedDeck[i].GetComponent<CardScript>();
			if (cardScript != null && cardScript.isStartCard)
				return i;
		}
		return -1;
	}

	/// <summary>
	/// Check if card's tags intersect with tagsToCheck list
	/// </summary>
	private bool CardHasAnyMatchingTag(CardScript cardScript)
	{
		if (tagsToCheck == null) return false;
		foreach (var tag in tagsToCheck)
		{
			if (cardScript.myTags.Contains(tag)) return true;
		}
		return false;
	}

	/// <summary>
	/// Predicate filters other than faction and tags: creature, cardTypeID, rarity.
	/// </summary>
	private bool PassesPredicateFilters(CardScript cardScript)
	{
		if (creatureFilter == CreatureFilter.Creature && !cardScript.isCreature) return false;
		if (creatureFilter == CreatureFilter.NonCreature && cardScript.isCreature) return false;
		if (onlyEnhanced && cardScript.attackGrowth <= 0) return false;
		if (!string.IsNullOrEmpty(typeIDFilter) && cardScript.cardTypeID != typeIDFilter) return false;
		if (rarityFilter != ReviveRarityFilter.Any)
		{
			var wanted = rarityFilter == ReviveRarityFilter.Common ? EnumStorage.Rarity.Common
				: rarityFilter == ReviveRarityFilter.Uncommon ? EnumStorage.Rarity.Uncommon
				: EnumStorage.Rarity.Rare;
			if (cardScript.rarity != wanted) return false;
		}
		return true;
	}

	/// <summary>
	/// Build the revivable pool: grave side only (index < startCardIndex), excluding neutral
	/// cards, minions, passive cards, the reveal-zone card and (optionally) the source card.
	/// Returns an empty list (fizzle) when no Start Card is present.
	/// </summary>
	private List<GameObject> BuildRevivePool(bool friendly, bool useTags = false)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var pool = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, pool, true);

		int startCardIndex = GetStartCardIndex();
		if (startCardIndex < 0) return new List<GameObject>();

		for (int i = pool.Count - 1; i >= 0; i--)
		{
			var card = pool[i];
			var cardScript = card.GetComponent<CardScript>();
			int index = _combinedDeck.IndexOf(card);

			if (index < 0 || index >= startCardIndex) { pool.RemoveAt(i); continue; } // grave-only
			if (CombatManager.ShouldSkipEffectProcessing(cardScript)) { pool.RemoveAt(i); continue; } // neutral / start card
			if (cardScript.isPassive) { pool.RemoveAt(i); continue; } // 4.0 passive cards are never revivable
			if (cardScript.isMinion) { pool.RemoveAt(i); continue; }
			if (card == combatManager.revealZone) { pool.RemoveAt(i); continue; } // the reveal-zone card can never be revived
			if (friendly != (cardScript.myStatusRef == myCardScript.myStatusRef)) { pool.RemoveAt(i); continue; }
			if (useTags && !CardHasAnyMatchingTag(cardScript)) { pool.RemoveAt(i); continue; }
			if (excludeSelf && card == myCard) { pool.RemoveAt(i); continue; }
			if (!PassesPredicateFilters(cardScript)) { pool.RemoveAt(i); continue; }
		}
		return pool;
	}

	/// <summary>
	/// Randomize tie order first, then apply a stable descending sort so equal keys stay random.
	/// </summary>
	private List<GameObject> SortOrShufflePool(List<GameObject> pool)
	{
		pool = UtilityFuncManagerScript.ShuffleList(pool);
		if (sortBy == ReviveSortBy.MaxAttack)
			return pool.OrderByDescending(c => c.GetComponent<CardScript>().GetAttack()).ToList();
		if (sortBy == ReviveSortBy.MaxExtraAttackTimes)
			return pool.OrderByDescending(c => c.GetComponent<CardScript>().extraAttackTimes).ToList();
		return pool;
	}

	public void ReviveSelf()
	{
		_combinedDeck = combatManager.combinedDeckZone;
		int startCardIndex = GetStartCardIndex();
		if (startCardIndex < 0) return;
		int idx = GetCardIndexInCombinedDeck(myCard);
		if (idx < 0 || idx >= startCardIndex) return; // exiled/destroyed or not in the grave
		ReviveChosenCards(new List<GameObject> { myCard }, 1);
	}

	public void ReviveMyCards(int amount)
	{
		ReviveChosenCards(SortOrShufflePool(BuildRevivePool(true)), amount);
	}

	public void ReviveMyCardsWithTag(int amount)
	{
		ReviveChosenCards(SortOrShufflePool(BuildRevivePool(true, true)), amount);
	}

	public void ReviveTheirCards(int amount)
	{
		ReviveChosenCards(SortOrShufflePool(BuildRevivePool(false)), amount);
	}

	public void ReviveTheirCardsWithTag(int amount)
	{
		ReviveChosenCards(SortOrShufflePool(BuildRevivePool(false, true)), amount);
	}

	private void ReviveChosenCards(List<GameObject> cardsToRevive, int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		amount = Mathf.Clamp(amount, 0, cardsToRevive.Count);
		if (amount == 0) return;

		// 1. First modify logical list, and collect successfully moved cards
		var revivedCards = new List<GameObject>();
		for (var i = 0; i < amount; i++)
		{
			var targetCard = cardsToRevive[i];
			var targetCardScript = targetCard.GetComponent<CardScript>();

			if (!_combinedDeck.Contains(targetCard)) continue; // exiled/destroyed meanwhile
			if (targetCard == combatManager.revealZone) continue; // defensive re-check at move time

			if (delayedRevive)
			{
				// Land at the Start Card tail (index startCardIndex + 1); no-op when the
				// Start Card is absent (shuffle window) — the card then stays where it is.
				combatManager.MoveCardToThroneZone(targetCard);
			}
			else
			{
				_combinedDeck.Remove(targetCard);
				_combinedDeck.Add(targetCard);  // add to bottom of list, top of deck
			}
			revivedCards.Add(targetCard);

			// Track revive counts (cumulative + per-round)
			if (ValueTrackerManager.me != null)
			{
				if (targetCardScript.myStatusRef == combatManager.ownerPlayerStatusRef)
				{
					if (ValueTrackerManager.me.ownerRevivedCountRef != null)
						ValueTrackerManager.me.ownerRevivedCountRef.value++;
					if (ValueTrackerManager.me.ownerRevivedCountThisRoundRef != null)
						ValueTrackerManager.me.ownerRevivedCountThisRoundRef.value++;
				}
				else
				{
					if (ValueTrackerManager.me.enemyRevivedCountRef != null)
						ValueTrackerManager.me.enemyRevivedCountRef.value++;
					if (ValueTrackerManager.me.enemyRevivedCountThisRoundRef != null)
						ValueTrackerManager.me.enemyRevivedCountThisRoundRef.value++;
				}
			}

			string myColor = GetMyCardColorTag();
			string targetColor = GetCardColorTag(targetCard);
			AppendLog("// [<color=" + myColor + ">" + myCard.gameObject.name + "</color>]将[<color=" + targetColor + ">" +
				targetCardScript.gameObject.name + "</color>]" + (delayedRevive ? "延迟复活至Start Card前" : "复活至牌库顶"));
		}
		if (revivedCards.Count == 0) return;

		// 2. Snapshot post-move indices BEFORE raising awaken events: reactive chains
		//    (苏醒 -> effects) may modify deck order before animation playback.
		//    Physical reordering stays deferred to RecorderAnimationPlayer.ApplyAnimationResult
		//    (same invariant as BuryEffect/StageEffect).
		var revivedTargetIndices = new List<int>();
		foreach (var card in revivedCards)
		{
			int idx = _combinedDeck.IndexOf(card);
			revivedTargetIndices.Add(idx >= 0 ? idx : _combinedDeck.Count - 1);
		}

		// 3. Capture animation requests BEFORE raising events: reactions to onMeRevived may
		//    close the current recorder before our requests are written (BuryEffect lesson).
		var recorderGo = EffectChainManager.Me != null ? EffectChainManager.Me.currentEffectRecorder : null;
		var recorder = recorderGo != null ? recorderGo.GetComponent<EffectRecorder>() : null;
		string reqInfo = "ReviveBatch cards=" + revivedCards.Count + " indices=" + string.Join(",", revivedTargetIndices) + " deckSize=" + _combinedDeck.Count;
		TestManager.Log("[ReviveEffect] Capture request to recorder=" + (recorder != null ? "chain#" + recorder.chainID + "[" + recorder.cardObject.name + "]" : "null") + " " + reqInfo);
		if (recorder != null)
		{
			if (delayedRevive)
			{
				// Per-card MoveToIndex (BuryEffect bounce shape): the Start Card tail is not
				// the deck top, so the pop-up-peak batch helper does not apply.
				for (int i = 0; i < revivedCards.Count; i++)
				{
					recorder.animationRequests.Add(new AnimationRequest {
						type = AnimationRequestType.MoveToIndex,
						targetCard = revivedCards[i],
						targetIndex = revivedTargetIndices[i],
						duration = CombatUXManager.me != null ? CombatUXManager.me.deckMoveArcDuration : 0.5f,
						useArc = true
					});
				}
			}
			else
			{
				// Same shape as StageEffect: arc via pop-up peak, then slot in at the deck top.
				recorder.animationRequests.Add(new AnimationRequest {
					type = AnimationRequestType.MoveToTopPopUpBatch,
					targetCards = new List<GameObject>(revivedCards),
					targetIndices = revivedTargetIndices,
					snapshotDeckSize = _combinedDeck.Count,
					duration = CombatUXManager.me != null ? CombatUXManager.me.deckMoveArcDuration : 0.5f,
					useArc = true
				});
			}
		}

		// 4. Raise awaken events in logic phase. onMeRevived is raised ONLY from this path:
		//    Stage / bounce / R2 placement never fire the awaken family.
		foreach (var card in revivedCards)
		{
			GameEventStorage.me.onMeRevived.RaiseSpecific(card);
			GameEventStorage.me.onAnyCardRevived.Raise();
			var cardScript = card.GetComponent<CardScript>();
			if (GameEventStorage.me.onFriendlyCardRevived != null)
			{
				if (cardScript.myStatusRef == combatManager.ownerPlayerStatusRef)
				{
					GameEventStorage.me.onFriendlyCardRevived.RaiseOwner();
				}
				else
				{
					GameEventStorage.me.onFriendlyCardRevived.RaiseOpponent();
				}
			}
		}
	}
}
