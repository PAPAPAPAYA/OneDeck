using System.Collections.Generic;
using DefaultNamespace.Managers;
using DefaultNamespace.SOScripts;
using UnityEngine;

public class StageEffect : EffectScript
{
	private List<GameObject> _combinedDeck;

	[Header("Tag Configuration")]
	public EnumStorage.Tag tagToCheck;

	[Header("Status Effect Stage Configuration")]
	[Tooltip("True = friendly cards, False = enemy cards")]
	public bool targetFriendly;
	public EnumStorage.StatusEffect statusEffectToCheck;

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
	/// Check if card is at top of deck (index = count - 1)
	/// </summary>
	private bool IsCardAtTop(GameObject card)
	{
		int index = GetCardIndexInCombinedDeck(card);
		return index >= 0 && index == _combinedDeck.Count - 1;
	}

	public void StageSelf() // put self on top of the deck
	{
		_combinedDeck = combatManager.combinedDeckZone;
		// If already at top, no need to stage
		if (IsCardAtTop(myCard)) return;
		var cardsToStage = new List<GameObject> { myCard };
		StageChosenCards(cardsToStage, 1);
	}

	public void StageCardsWithTag(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsWithTag = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsWithTag, true);

		// Filter cards that have the specified tag and are not at the top
		for (int i = cardsWithTag.Count - 1; i >= 0; i--)
		{
			var card = cardsWithTag[i];
			var cardScript = card.GetComponent<CardScript>();
			if (!cardScript.myTags.Contains(tagToCheck) || IsCardAtTop(card) || cardScript.isMinion || CombatManager.ShouldSkipEffectProcessing(cardScript))
			{
				cardsWithTag.RemoveAt(i);
			}
		}

		cardsWithTag = UtilityFuncManagerScript.ShuffleList(cardsWithTag);
		StageChosenCards(cardsWithTag, amount);
	}

	public void StageMyCards(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var myCards = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, myCards, true);

		// Filter cards that belong to this card's owner and are not at the top
		for (int i = myCards.Count - 1; i >= 0; i--)
		{
			var card = myCards[i];
			var cardScript = card.GetComponent<CardScript>();
			if (CombatManager.ShouldSkipEffectProcessing(cardScript) || cardScript.myStatusRef != myCardScript.myStatusRef || IsCardAtTop(card) || cardScript.isMinion)
			{
				myCards.RemoveAt(i);
			}
		}

		myCards = UtilityFuncManagerScript.ShuffleList(myCards);
		StageChosenCards(myCards, amount);
	}

	public void StageMyTokens(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var myTokens = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, myTokens, true);

		// Filter: own Minion cards, not at the top
		for (int i = myTokens.Count - 1; i >= 0; i--)
		{
			var card = myTokens[i];
			var cardScript = card.GetComponent<CardScript>();
			if (CombatManager.ShouldSkipEffectProcessing(cardScript) || cardScript.myStatusRef != myCardScript.myStatusRef || IsCardAtTop(card) || !cardScript.isMinion)
			{
				myTokens.RemoveAt(i);
			}
		}

		myTokens = UtilityFuncManagerScript.ShuffleList(myTokens);
		StageChosenCards(myTokens, amount);
	}

	public void StageMyCardsWithTag(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsWithTag = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsWithTag, true);

		// Filter cards that have the specified tag, belong to this card's owner, and are not at the top
		for (int i = cardsWithTag.Count - 1; i >= 0; i--)
		{
			var card = cardsWithTag[i];
			var cardScript = card.GetComponent<CardScript>();
			if (!cardScript.myTags.Contains(tagToCheck) || CombatManager.ShouldSkipEffectProcessing(cardScript) || cardScript.myStatusRef != myCardScript.myStatusRef || IsCardAtTop(card) || cardScript.isMinion)
			{
				cardsWithTag.RemoveAt(i);
			}
		}

		cardsWithTag = UtilityFuncManagerScript.ShuffleList(cardsWithTag);
		StageChosenCards(cardsWithTag, amount);
	}

	public void StageTheirCardsWithTag(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsWithTag = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsWithTag, true);

		// Filter cards that have the specified tag, belong to the opponent, and are not at the top
		for (int i = cardsWithTag.Count - 1; i >= 0; i--)
		{
			var card = cardsWithTag[i];
			var cardScript = card.GetComponent<CardScript>();
			if (!cardScript.myTags.Contains(tagToCheck) || CombatManager.ShouldSkipEffectProcessing(cardScript) || cardScript.myStatusRef == myCardScript.myStatusRef || IsCardAtTop(card) || cardScript.isMinion)
			{
				cardsWithTag.RemoveAt(i);
			}
		}

		cardsWithTag = UtilityFuncManagerScript.ShuffleList(cardsWithTag);
		StageChosenCards(cardsWithTag, amount);
	}

	/// <summary>
	/// Stage all eligible friendly Minion cards
	/// </summary>
	/// <param name="targetCardTypeID">Target card type ID, empty string matches all friendly Minion cards</param>
	public void StageAllFriendlyMinion(string targetCardTypeID)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var friendlyMinions = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, friendlyMinions, true);

		// Filter: own Minion cards, not at the top
		for (int i = friendlyMinions.Count - 1; i >= 0; i--)
		{
			var card = friendlyMinions[i];
			var cardScript = card.GetComponent<CardScript>();
			
			// Exclude non-Minion cards, non-own cards, cards already at top, and cards to skip
			if (!cardScript.isMinion || 
			    CombatManager.ShouldSkipEffectProcessing(cardScript) || 
			    cardScript.myStatusRef != myCardScript.myStatusRef || 
			    IsCardAtTop(card))
			{
				friendlyMinions.RemoveAt(i);
				continue;
			}
			
			// If card type id is specified, only match cards of that type
			if (!string.IsNullOrEmpty(targetCardTypeID) && cardScript.cardTypeID != targetCardTypeID)
			{
				friendlyMinions.RemoveAt(i);
			}
		}

		StageChosenCards(friendlyMinions, friendlyMinions.Count);
	}

	public void StageMyCards_BasedOnIntSO(IntSO intSO)
	{
		if (intSO == null) return;
		StageMyCards(intSO.value);
	}

	/// <summary>
	/// Stage a random card matching specified Card Type ID from enemy deck to top
	/// </summary>
	/// <param name="targetCardTypeID">Target card type ID</param>
	public void StageTheirSpecificCard(string targetCardTypeID)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var matchingCards = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, matchingCards, true);

		// Filter: Enemy cards, matching specified cardTypeID, not at top, not Minion, don't skip effect processing
		for (int i = matchingCards.Count - 1; i >= 0; i--)
		{
			var card = matchingCards[i];
			var cardScript = card.GetComponent<CardScript>();
			if (CombatManager.ShouldSkipEffectProcessing(cardScript) || 
			    cardScript.myStatusRef == myCardScript.myStatusRef || 
			    IsCardAtTop(card) || 
			    cardScript.isMinion ||
			    cardScript.cardTypeID != targetCardTypeID)
			{
				matchingCards.RemoveAt(i);
			}
		}

		// If no eligible cards, display failure message
		if (matchingCards.Count == 0)
		{
			string myColor = GetMyCardColorTag();
			effectResultString.value += $"// [<color={myColor}>{myCard.gameObject.name}</color>] failed to stage enemy card (no matching card with ID '{targetCardTypeID}')\n";
			return;
		}

		// Randomly select one to stage to top
		matchingCards = UtilityFuncManagerScript.ShuffleList(matchingCards);
		StageChosenCards(matchingCards, 1);
	}

	/// <summary>
	/// Stage 1 card with the most target status effects. Randomly choose if multiple cards tie.
	/// </summary>
	public void StageCardWithMostStatusEffect()
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var eligibleCards = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, eligibleCards, true);

		// Filter: matching owner, not at top, not Minion, don't skip effect processing
		for (int i = eligibleCards.Count - 1; i >= 0; i--)
		{
			var card = eligibleCards[i];
			var cardScript = card.GetComponent<CardScript>();
			bool isFriendly = cardScript.myStatusRef == myCardScript.myStatusRef;
			if (CombatManager.ShouldSkipEffectProcessing(cardScript) ||
			    isFriendly != targetFriendly ||
			    IsCardAtTop(card) ||
			    cardScript.isMinion)
			{
				eligibleCards.RemoveAt(i);
			}
		}

		if (eligibleCards.Count == 0) return;

		// Find the maximum count of the target status effect
		int maxCount = -1;
		foreach (var card in eligibleCards)
		{
			var cardScript = card.GetComponent<CardScript>();
			int count = EnumStorage.GetStatusEffectCount(cardScript.myStatusEffects, statusEffectToCheck);
			if (count > maxCount)
			{
				maxCount = count;
			}
		}

		// Collect all cards that have the max count
		var topCards = new List<GameObject>();
		foreach (var card in eligibleCards)
		{
			var cardScript = card.GetComponent<CardScript>();
			int count = EnumStorage.GetStatusEffectCount(cardScript.myStatusEffects, statusEffectToCheck);
			if (count == maxCount)
			{
				topCards.Add(card);
			}
		}

		// Randomly select one and stage it
		topCards = UtilityFuncManagerScript.ShuffleList(topCards);
		StageChosenCards(topCards, 1);
	}

	private void StageChosenCards(List<GameObject> cardsToStage, int amount)
	{
		amount = Mathf.Clamp(amount, 0, cardsToStage.Count);
		if (amount == 0) return;

		// 1. First modify logical list, and collect successfully moved cards
		var stagedCards = new List<GameObject>();
		for (var i = 0; i < amount; i++)
		{
			var targetCard = cardsToStage[i];
			var targetCardScript = targetCard.GetComponent<CardScript>();

			if (_combinedDeck.Contains(targetCard))
			{
				_combinedDeck.Remove(targetCard);
				_combinedDeck.Add(targetCard);  // add to bottom of list, top of deck
				stagedCards.Add(targetCard);
				
				string myColor = GetMyCardColorTag();
				string targetColor = GetCardColorTag(targetCard);
				effectResultString.value += "// [<color=" + myColor + ">" + myCard.gameObject.name + "</color>] staged [<color=" + targetColor + ">" +
					targetCardScript.gameObject.name + "</color>] to the top of the deck\n";
			}
		}
		
		// 2. Play arc trajectory animation (move cards to top)
		foreach (var card in stagedCards)
		{
			CombatUXManager.me.MoveCardToTop(card, duration: 0.5f, useArc: true);
		}

		// 3. Trigger card staged event
		foreach (var card in stagedCards)
		{
			// Trigger specific card staged event
			GameEventStorage.me.onMeStaged.RaiseSpecific(card);
		}
	}
}
