using System.Collections.Generic;
using DefaultNamespace;
using DefaultNamespace.Managers;
using DefaultNamespace.SOScripts;
using UnityEngine;

public class BuryEffect : EffectScript
{
	private List<GameObject> _combinedDeck;

	[Header("Tag Configuration")]
	public List<EnumStorage.Tag> tagsToCheck;

	[Header("Self Exclusion")]
	[Tooltip("If true, the source card will not be selected when burying multiple cards")]
	public bool excludeSelf = true;

	[Header("Based on IntSO")]
	[Tooltip("IntSO used when this card belongs to the owner/player")]
	public IntSO ownerIntSO;
	[Tooltip("IntSO used when this card belongs to the enemy")]
	public IntSO enemyIntSO;

	/// <summary>
	/// Get card owner's color tag (Player=#87CEEB, Enemy=orange)
	/// </summary>
	private string GetCardColorTag(GameObject card)
	{
		var cardStatus = card.GetComponent<CardScript>().myStatusRef;
		return cardStatus == combatManager.ownerPlayerStatusRef ? "#87CEEB" : "orange";
	}

	/// <summary>
	/// Get current card's color tag
	/// </summary>
	private string GetMyCardColorTag()
	{
		return myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? "#87CEEB" : "orange";
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
	/// Check if card is at bottom of deck (index = 0)
	/// </summary>
	private bool IsCardAtBottom(GameObject card)
	{
		int index = GetCardIndexInCombinedDeck(card);
		return index == 0;
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
	/// Check if card is below the Start Card in deck order (index < startCardIndex).
	/// Always returns false when no Start Card is found.
	/// </summary>
	private bool IsCardBelowStartCard(GameObject card)
	{
		int startCardIndex = GetStartCardIndex();
		if (startCardIndex < 0) return false;
		return GetCardIndexInCombinedDeck(card) < startCardIndex;
	}

	public void BurySelf() // put self at the bottom of the deck
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardToBury = transform.parent.gameObject;
		// If already at bottom, no need to bury
		if (IsCardAtBottom(cardToBury)) return;
		// Do not bury cards below the Start Card boundary
		if (IsCardBelowStartCard(cardToBury)) return;
		var cardsToBury = new List<GameObject> { cardToBury };
		BuryChosenCards(cardsToBury, 1);
	}

	/// <summary>
	/// Check if card's tags intersect with tagsToCheck list
	/// </summary>
	private bool CardHasAnyMatchingTag(CardScript cardScript)
	{
		if (tagsToCheck == null || tagsToCheck.Count == 0) return false;
		foreach (var tag in tagsToCheck)
		{
			if (cardScript.myTags.Contains(tag)) return true;
		}
		return false;
	}

	public void BuryCardsWithTag(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsWithTag = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsWithTag, true);

		// Filter cards that have any of the specified tags and are not at the bottom
		for (int i = cardsWithTag.Count - 1; i >= 0; i--)
		{
			var card = cardsWithTag[i];
			var cardScript = card.GetComponent<CardScript>();
			if (!CardHasAnyMatchingTag(cardScript) || IsCardAtBottom(card) || cardScript.isMinion || CombatManager.ShouldSkipEffectProcessing(cardScript) || (excludeSelf && card == myCard) || IsCardBelowStartCard(card))
			{
				cardsWithTag.RemoveAt(i);
			}
		}

		cardsWithTag = UtilityFuncManagerScript.ShuffleList(cardsWithTag);
		BuryChosenCards(cardsWithTag, amount);
	}

	public void BuryMyCards(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var myCards = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, myCards, true);

		// Filter cards that belong to this card's owner and are not at the bottom
		for (int i = myCards.Count - 1; i >= 0; i--)
		{
			var card = myCards[i];
			var cardScript = card.GetComponent<CardScript>();
			if (CombatManager.ShouldSkipEffectProcessing(cardScript) || cardScript.myStatusRef != myCardScript.myStatusRef || IsCardAtBottom(card) || cardScript.isMinion || (excludeSelf && card == myCard) || IsCardBelowStartCard(card))
			{
				myCards.RemoveAt(i);
			}
		}

		myCards = UtilityFuncManagerScript.ShuffleList(myCards);
		BuryChosenCards(myCards, amount);
	}

	public void BuryMyCardsWithTag(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsWithTag = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsWithTag, true);

		// Filter cards that have any of the specified tags, belong to this card's owner, and are not at the bottom
		for (int i = cardsWithTag.Count - 1; i >= 0; i--)
		{
			var card = cardsWithTag[i];
			var cardScript = card.GetComponent<CardScript>();
			if (!CardHasAnyMatchingTag(cardScript) || CombatManager.ShouldSkipEffectProcessing(cardScript) || cardScript.myStatusRef != myCardScript.myStatusRef || IsCardAtBottom(card) || cardScript.isMinion || (excludeSelf && card == myCard) || IsCardBelowStartCard(card))
			{
				cardsWithTag.RemoveAt(i);
			}
		}

		cardsWithTag = UtilityFuncManagerScript.ShuffleList(cardsWithTag);
		BuryChosenCards(cardsWithTag, amount);
	}

	public void BuryTheirCards(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var theirCards = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, theirCards, true);

		// Filter cards that belong to the opponent and are not at the bottom
		for (int i = theirCards.Count - 1; i >= 0; i--)
		{
			var card = theirCards[i];
			var cardScript = card.GetComponent<CardScript>();
			if (CombatManager.ShouldSkipEffectProcessing(cardScript) || cardScript.myStatusRef == myCardScript.myStatusRef || IsCardAtBottom(card) || cardScript.isMinion || IsCardBelowStartCard(card))
			{
				theirCards.RemoveAt(i);
			}
		}

		theirCards = UtilityFuncManagerScript.ShuffleList(theirCards);
		BuryChosenCards(theirCards, amount);
	}

	public void BuryTheirCardsWithTag(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsWithTag = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsWithTag, true);

		// Filter cards that have any of the specified tags, belong to the opponent, and are not at the bottom
		for (int i = cardsWithTag.Count - 1; i >= 0; i--)
		{
			var card = cardsWithTag[i];
			var cardScript = card.GetComponent<CardScript>();
			if (!CardHasAnyMatchingTag(cardScript) || CombatManager.ShouldSkipEffectProcessing(cardScript) || cardScript.myStatusRef == myCardScript.myStatusRef || IsCardAtBottom(card) || cardScript.isMinion || IsCardBelowStartCard(card))
			{
				cardsWithTag.RemoveAt(i);
			}
		}

		cardsWithTag = UtilityFuncManagerScript.ShuffleList(cardsWithTag);
		BuryChosenCards(cardsWithTag, amount);
	}

	public void BuryTheirCards_BasedOnIntSO()
	{
		IntSO intSO = GetIntSOForOwner(ownerIntSO, enemyIntSO);
		if (intSO == null) return;
		BuryTheirCards(intSO.value);
	}

	public void BuryMyCards_BasedOnIntSO()
	{
		IntSO intSO = GetIntSOForOwner(ownerIntSO, enemyIntSO);
		if (intSO == null) return;
		BuryMyCards(intSO.value);
	}

	public void BuryAllMyCards()
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var myCards = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, myCards, true);

		// Filter cards that belong to this card's owner and are not at the bottom
		for (int i = myCards.Count - 1; i >= 0; i--)
		{
			var card = myCards[i];
			var cardScript = card.GetComponent<CardScript>();
			if (CombatManager.ShouldSkipEffectProcessing(cardScript) || cardScript.myStatusRef != myCardScript.myStatusRef || IsCardAtBottom(card) || cardScript.isMinion || (excludeSelf && card == myCard) || IsCardBelowStartCard(card))
			{
				myCards.RemoveAt(i);
			}
		}

		BuryChosenCards(myCards, myCards.Count);
	}

	/// <summary>
	/// Bury the next X cards in deck order (cards before this card in combined deck, i.e. closer to bottom).
	/// Iterates backwards from the current card's position and buries each valid target.
	/// Skips cards that should be ignored, are minions, are already at the bottom, or are below the Start Card.
	/// If this card is in the reveal zone, starts from the bottom of the deck instead.
	/// </summary>
	/// <param name="amount">Number of cards to bury</param>
	public void BuryNextXCards(int amount)
	{
		if (amount <= 0) return;
		_combinedDeck = combatManager.combinedDeckZone;
		TestManager.Log("[BuryEffect] BuryNextXCards START amount=" + amount + " myCard=" + myCard.name + " inReveal=" + (combatManager.revealZone != null && combatManager.revealZone == myCard) + " deckCount=" + _combinedDeck.Count);
		int startIndex;
		if (combatManager.revealZone != null && combatManager.revealZone == myCard)
		{
			startIndex = _combinedDeck.Count - 1;
		}
		else
		{
			int currentIndex = -1;
			for (int i = 0; i < _combinedDeck.Count; i++)
			{
				if (_combinedDeck[i] == myCard)
				{
					currentIndex = i;
					break;
				}
			}
			if (currentIndex < 0) return;
			// If this card is already below the Start Card, it cannot bury anything toward the bottom
			if (IsCardBelowStartCard(myCard)) return;
			startIndex = currentIndex - 1;
		}
		int startCardIndex = GetStartCardIndex();
		int loopLowerBound = startCardIndex >= 0 ? startCardIndex : 0;
		var cardsToBury = new List<GameObject>();
		int cardsFound = 0;
		for (int i = startIndex; i >= loopLowerBound && cardsFound < amount; i--)
		{
			var targetCard = _combinedDeck[i];
			var targetCardScript = targetCard.GetComponent<CardScript>();
			if (CombatManager.ShouldSkipEffectProcessing(targetCardScript)) continue;
			if (targetCardScript.isMinion) continue;
			if (IsCardAtBottom(targetCard)) continue;
			cardsToBury.Add(targetCard);
			cardsFound++;
		}
		if (cardsToBury.Count > 0)
		{
			TestManager.Log("[BuryEffect] BuryNextXCards found cardsToBury=" + cardsToBury.Count + " cards=" + string.Join(",", cardsToBury.ConvertAll(c => c.name)));
			BuryChosenCards(cardsToBury, cardsToBury.Count);
		}
		else
		{
			TestManager.Log("[BuryEffect] BuryNextXCards found NO cards to bury");
		}
	}

	private void BuryChosenCards(List<GameObject> cardsToBury, int amount)
	{
		amount = Mathf.Clamp(amount, 0, cardsToBury.Count);
		if (amount == 0) return;

		// 1. First modify logical list, and collect successfully moved cards
		var buriedCards = new List<GameObject>();
		for (var i = 0; i < amount; i++)
		{
			var targetCard = cardsToBury[i];
			var targetCardScript = targetCard.GetComponent<CardScript>();

			if (_combinedDeck.Contains(targetCard))
			{
				_combinedDeck.Remove(targetCard);
				_combinedDeck.Insert(0, targetCard);  // Insert at bottom
				buriedCards.Add(targetCard);
				
				// Track buried counts
				if (ValueTrackerManager.me != null)
				{
					if (targetCardScript.myStatusRef == combatManager.ownerPlayerStatusRef)
					{
						if (ValueTrackerManager.me.ownerCardsBuriedCountRef != null)
							ValueTrackerManager.me.ownerCardsBuriedCountRef.value++;
					}
					else
					{
						if (ValueTrackerManager.me.enemyCardsBuriedCountRef != null)
							ValueTrackerManager.me.enemyCardsBuriedCountRef.value++;
					}
				}
				
				string myColor = GetMyCardColorTag();
				string targetColor = GetCardColorTag(targetCard);
				AppendLog("// [<color=" + myColor + ">" + myCard.gameObject.name + "</color>]将[<color=" + targetColor + ">" +
					targetCardScript.gameObject.name + "</color>]埋入牌库底端");
			}
		}
		
		// VISUAL-FIX(2026-06-13): Remove logic-phase deck sync in Bury to keep animation indices consistent
		//   Cause:    SyncPhysicalCardsWithCombinedDeck in logic phase pre-moves physical cards to final
		//             positions, corrupting snapshot indices for a preceding consume effect's SlotInBatch
		//             and causing distance-zero tweens for the bury animation itself.
		//   Fix:      Physical deck reordering is deferred to RecorderAnimationPlayer via ApplyAnimationResult.
		//   Affects:  BuryEffect, ApplyAnimationResult, RecorderAnimationPlayer
		//   Regress:  StoneShell / grave_punch: verify PopUpBatch + MoveToBottomBatch animate with visible movement.
		//   Related:  PRD stage-sync-removal-ju-on-slot-in-2026-06-13

		// VISUAL-FIX(2026-05-15): Bury-then-Stage reactive chain causes wrong animation target index
		//   Cause:    onMeBuried -> StageSelf modifies deck order AFTER bury logic but BEFORE
		//             animation playback; without snapshot the animation uses stale indices
		//   Affects:  BuryEffect, StageEffect, reactive chains, ApplyAnimationResult
		//   Regress:  Reveal StoneShell (BuryNext2Cards) then reveal RisingFlame (StageSelf on bury)
		//   Related:  Card_StoneShell, Card_RisingFlame
		var buriedTargetIndices = new List<int>();
		foreach (var card in buriedCards)
		{
			int idx = _combinedDeck.IndexOf(card);
			buriedTargetIndices.Add(idx >= 0 ? idx : 0);
		}

		// VISUAL-FIX(2026-06-10): Bury animation not played when buried card triggers reactive effects
		//   Cause:    BuryChosenCards captured AnimationRequests AFTER raising onMeBuried, but
		//             reactive effects (e.g. counter -> add a copy) called CloseOpenedChain,
		//             destroying the current recorder before requests were written.
		//   Affects:  BuryEffect, EffectChainManager, RecorderAnimationPlayer
		//   Regress:  Deck: grave_punch, slime, start card. Reveal grave_punch (BuryNextXCards).
		//             Verify slime plays PopUp + MoveToBottomBatch animation visibly.
		//   Related:  grave_punch, slime
		// 2. Capture animation requests BEFORE raising events, because reactive effects
		// (e.g. onMeBuried -> counter -> add a copy) may call CloseOpenedChain and destroy
		// the current recorder before we get a chance to write our requests.
		var recorderGo = EffectChainManager.Me != null ? EffectChainManager.Me.currentEffectRecorder : null;
		var recorder = recorderGo != null ? recorderGo.GetComponent<EffectRecorder>() : null;
		string recorderInfo = recorder != null ? "chain#" + recorder.chainID + "[" + recorder.cardObject.name + "]" : "null";
		string reqInfo = "BuryBatch cards=" + buriedCards.Count + " indices=" + string.Join(",", buriedTargetIndices) + " deckSize=" + _combinedDeck.Count;
		TestManager.Log("[BuryEffect] Capture request to recorder=" + recorderInfo + " " + reqInfo);
		if (recorder != null)
		{
			// PopUp so player can see which cards are being buried
			recorder.animationRequests.Add(new AnimationRequest {
				type = AnimationRequestType.PopUpBatch,
				targetCards = new List<GameObject>(buriedCards)
			});

			recorder.animationRequests.Add(new AnimationRequest {
				type = AnimationRequestType.MoveToBottomBatch,
				targetCards = new List<GameObject>(buriedCards),
				targetIndices = buriedTargetIndices,
				snapshotDeckSize = _combinedDeck.Count,
				duration = CombatUXManager.me != null ? CombatUXManager.me.deckMoveArcDuration : 0.5f,
				useArc = true
			});
		}

		// 3. Raise events in logic phase
		foreach (var buriedCard in buriedCards)
		{
			GameEventStorage.me.onMeBuried.RaiseSpecific(buriedCard);
			GameEventStorage.me.onAnyCardBuried.Raise();
			var buriedCardScript = buriedCard.GetComponent<CardScript>();
			if (buriedCardScript != null && GameEventStorage.me.onFriendlyCardBuried != null)
			{
				if (buriedCardScript.myStatusRef == combatManager.ownerPlayerStatusRef)
				{
					GameEventStorage.me.onFriendlyCardBuried.RaiseOwner();
				}
				else
				{
					GameEventStorage.me.onFriendlyCardBuried.RaiseOpponent();
				}
			}
		}
	}
}
