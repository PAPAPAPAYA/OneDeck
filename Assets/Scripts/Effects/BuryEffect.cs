using System.Collections.Generic;
using DefaultNamespace.Managers;
using DefaultNamespace.SOScripts;
using UnityEngine;

public class BuryEffect : EffectScript
{
	private List<GameObject> _combinedDeck;

	[Header("Tag Configuration")]
	public EnumStorage.Tag tagToCheck;

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

	public void BurySelf() // put self at the bottom of the deck
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardToBury = transform.parent.gameObject;
		// If already at bottom, no need to bury
		if (IsCardAtBottom(cardToBury)) return;
		var cardsToBury = new List<GameObject> { cardToBury };
		BuryChosenCards(cardsToBury, 1);
	}

	public void BuryCardsWithTag(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsWithTag = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsWithTag, true);

		// Filter cards that have the specified tag and are not at the bottom
		for (int i = cardsWithTag.Count - 1; i >= 0; i--)
		{
			var card = cardsWithTag[i];
			var cardScript = card.GetComponent<CardScript>();
			if (!cardScript.myTags.Contains(tagToCheck) || IsCardAtBottom(card) || cardScript.isMinion || CombatManager.ShouldSkipEffectProcessing(cardScript))
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
			if (CombatManager.ShouldSkipEffectProcessing(cardScript) || cardScript.myStatusRef != myCardScript.myStatusRef || IsCardAtBottom(card) || cardScript.isMinion)
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

		// Filter cards that have the specified tag, belong to this card's owner, and are not at the bottom
		for (int i = cardsWithTag.Count - 1; i >= 0; i--)
		{
			var card = cardsWithTag[i];
			var cardScript = card.GetComponent<CardScript>();
			if (!cardScript.myTags.Contains(tagToCheck) || CombatManager.ShouldSkipEffectProcessing(cardScript) || cardScript.myStatusRef != myCardScript.myStatusRef || IsCardAtBottom(card) || cardScript.isMinion)
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
			if (CombatManager.ShouldSkipEffectProcessing(cardScript) || cardScript.myStatusRef == myCardScript.myStatusRef || IsCardAtBottom(card) || cardScript.isMinion)
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

		// Filter cards that have the specified tag, belong to the opponent, and are not at the bottom
		for (int i = cardsWithTag.Count - 1; i >= 0; i--)
		{
			var card = cardsWithTag[i];
			var cardScript = card.GetComponent<CardScript>();
			if (!cardScript.myTags.Contains(tagToCheck) || CombatManager.ShouldSkipEffectProcessing(cardScript) || cardScript.myStatusRef == myCardScript.myStatusRef || IsCardAtBottom(card) || cardScript.isMinion)
			{
				cardsWithTag.RemoveAt(i);
			}
		}

		cardsWithTag = UtilityFuncManagerScript.ShuffleList(cardsWithTag);
		BuryChosenCards(cardsWithTag, amount);
	}

	public void BuryTheirCards_BasedOnIntSO(IntSO intSO)
	{
		if (intSO == null) return;
		BuryTheirCards(intSO.value);
	}

	public void BuryMyCards_BasedOnIntSO(IntSO intSO)
	{
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
			if (CombatManager.ShouldSkipEffectProcessing(cardScript) || cardScript.myStatusRef != myCardScript.myStatusRef || IsCardAtBottom(card) || cardScript.isMinion)
			{
				myCards.RemoveAt(i);
			}
		}

		BuryChosenCards(myCards, myCards.Count);
	}

	/// <summary>
	/// Bury the last X cards in the combined deck (cards before this card in deck order).
	/// Iterates backwards from the current card's position and buries each valid target.
	/// Skips cards that should be ignored, are minions, or are already at the bottom.
	/// If this card is in the reveal zone, starts from the bottom of the deck instead.
	/// </summary>
	/// <param name="amount">Number of cards to bury</param>
	public void BuryLastXCards(int amount)
	{
		if (amount <= 0) return;
		_combinedDeck = combatManager.combinedDeckZone;
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
			startIndex = currentIndex - 1;
		}
		var cardsToBury = new List<GameObject>();
		int cardsFound = 0;
		for (int i = startIndex; i >= 0 && cardsFound < amount; i--)
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
			BuryChosenCards(cardsToBury, cardsToBury.Count);
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
				effectResultString.value += "// [<color=" + myColor + ">" + myCard.gameObject.name + "</color>]将[<color=" + targetColor + ">" +
					targetCardScript.gameObject.name + "</color>]埋入牌库底端\n";
			}
		}
		
		// 2. Play arc trajectory animation (move cards to bottom)
		foreach (var card in buriedCards)
		{
			CombatUXManager.me.MoveCardToBottom(card, duration: 0.5f, useArc: true);
		}
		
		// 3. Trigger card buried event
		foreach (var card in buriedCards)
		{
			// Trigger specific card buried event
			GameEventStorage.me.onMeBuried.RaiseSpecific(card);
			// Trigger any card buried event
			GameEventStorage.me.onAnyCardBuried.Raise();
			// Trigger friendly card buried event
			var cardStatus = card.GetComponent<CardScript>()?.myStatusRef;
			if (cardStatus != null && GameEventStorage.me.onFriendlyCardBuried != null)
			{
				if (cardStatus == combatManager.ownerPlayerStatusRef)
				{
					GameEventStorage.me.onFriendlyCardBuried.RaiseOwner();
				}
				else
				{
					GameEventStorage.me.onFriendlyCardBuried.RaiseOpponent();
				}
			}
		}
		
		// 4. Sync physical card list and update all card positions
		if (buriedCards.Count > 0)
		{
			CombatUXManager.me.SyncPhysicalCardsWithCombinedDeck();
			CombatUXManager.me.UpdateAllPhysicalCardTargets();
		}
	}
}
