using System.Collections.Generic;
using DefaultNamespace.Managers;
using DefaultNamespace.SOScripts;
using UnityEngine;

public class ExileEffect : EffectScript
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

	public void ExileSelf() // exile self from the deck
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsToExile = new List<GameObject> { myCard };
		ExileChosenCards(cardsToExile, 1);
	}

	public void ExileMyCards(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsToExile = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsToExile, true);

		// Filter: Own cards, exclude cards to skip
		for (int i = cardsToExile.Count - 1; i >= 0; i--)
		{
			var card = cardsToExile[i];
			var cardScript = card.GetComponent<CardScript>();
			if (CombatManager.ShouldSkipEffectProcessing(cardScript) || cardScript.myStatusRef != myCardScript.myStatusRef)
			{
				cardsToExile.RemoveAt(i);
			}
		}

		cardsToExile = UtilityFuncManagerScript.ShuffleList(cardsToExile);
		ExileChosenCards(cardsToExile, amount);
	}

	public void ExileTheirCards(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsToExile = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsToExile, true);

		// Filter: Enemy cards, exclude cards to skip
		for (int i = cardsToExile.Count - 1; i >= 0; i--)
		{
			var card = cardsToExile[i];
			var cardScript = card.GetComponent<CardScript>();
			if (CombatManager.ShouldSkipEffectProcessing(cardScript) || cardScript.myStatusRef == myCardScript.myStatusRef)
			{
				cardsToExile.RemoveAt(i);
			}
		}

		cardsToExile = UtilityFuncManagerScript.ShuffleList(cardsToExile);
		ExileChosenCards(cardsToExile, amount);
	}

	public void ExileRandomCards(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsToExile = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsToExile, true);

		// Filter: Exclude cards to skip
		for (int i = cardsToExile.Count - 1; i >= 0; i--)
		{
			var card = cardsToExile[i];
			var cardScript = card.GetComponent<CardScript>();
			if (CombatManager.ShouldSkipEffectProcessing(cardScript))
			{
				cardsToExile.RemoveAt(i);
			}
		}

		cardsToExile = UtilityFuncManagerScript.ShuffleList(cardsToExile);
		ExileChosenCards(cardsToExile, amount);
	}

	public void ExileMyCardsWithTag(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsToExile = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsToExile, true);

		// Filter: Own cards with specified tag, exclude cards to skip
		for (int i = cardsToExile.Count - 1; i >= 0; i--)
		{
			var card = cardsToExile[i];
			var cardScript = card.GetComponent<CardScript>();
			if (!cardScript.myTags.Contains(tagToCheck) || 
			    CombatManager.ShouldSkipEffectProcessing(cardScript) || 
			    cardScript.myStatusRef != myCardScript.myStatusRef)
			{
				cardsToExile.RemoveAt(i);
			}
		}

		cardsToExile = UtilityFuncManagerScript.ShuffleList(cardsToExile);
		ExileChosenCards(cardsToExile, amount);
	}

	public void ExileTheirCardsWithTag(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsToExile = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsToExile, true);

		// Filter: Enemy cards with specified tag, exclude cards to skip
		for (int i = cardsToExile.Count - 1; i >= 0; i--)
		{
			var card = cardsToExile[i];
			var cardScript = card.GetComponent<CardScript>();
			if (!cardScript.myTags.Contains(tagToCheck) || 
			    CombatManager.ShouldSkipEffectProcessing(cardScript) || 
			    cardScript.myStatusRef == myCardScript.myStatusRef)
			{
				cardsToExile.RemoveAt(i);
			}
		}

		cardsToExile = UtilityFuncManagerScript.ShuffleList(cardsToExile);
		ExileChosenCards(cardsToExile, amount);
	}

	public void ExileCardsWithTag(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsToExile = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsToExile, true);

		// Filter: With specified tag, exclude cards to skip
		for (int i = cardsToExile.Count - 1; i >= 0; i--)
		{
			var card = cardsToExile[i];
			var cardScript = card.GetComponent<CardScript>();
			if (!cardScript.myTags.Contains(tagToCheck) || CombatManager.ShouldSkipEffectProcessing(cardScript))
			{
				cardsToExile.RemoveAt(i);
			}
		}

		cardsToExile = UtilityFuncManagerScript.ShuffleList(cardsToExile);
		ExileChosenCards(cardsToExile, amount);
	}

	public void ExileMyMinions(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var minions = new List<GameObject>();

		// Filter: Own Minion cards
		foreach (var card in _combinedDeck)
		{
			if (card == null) continue;
			var cardScript = card.GetComponent<CardScript>();
			if (cardScript == null) continue;
			if (CombatManager.ShouldSkipEffectProcessing(cardScript)) continue;
			if (cardScript.isMinion && cardScript.myStatusRef == myCardScript.myStatusRef)
			{
				minions.Add(card);
			}
		}

		minions = UtilityFuncManagerScript.ShuffleList(minions);
		ExileChosenCards(minions, amount);
	}

	public void ExileTheirMinions(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var minions = new List<GameObject>();

		// Filter: Enemy Minion cards
		foreach (var card in _combinedDeck)
		{
			if (card == null) continue;
			var cardScript = card.GetComponent<CardScript>();
			if (cardScript == null) continue;
			if (CombatManager.ShouldSkipEffectProcessing(cardScript)) continue;
			if (cardScript.isMinion && cardScript.myStatusRef != myCardScript.myStatusRef)
			{
				minions.Add(card);
			}
		}

		minions = UtilityFuncManagerScript.ShuffleList(minions);
		ExileChosenCards(minions, amount);
	}

	public void ExileMyCards_BasedOnIntSO(IntSO intSO)
	{
		if (intSO == null) return;
		ExileMyCards(intSO.value);
	}

	public void ExileTheirCards_BasedOnIntSO(IntSO intSO)
	{
		if (intSO == null) return;
		ExileTheirCards(intSO.value);
	}

	private void ExileChosenCards(List<GameObject> cardsToExile, int amount)
	{
		amount = Mathf.Clamp(amount, 0, cardsToExile.Count);
		if (amount == 0) return;

		string myColor = GetMyCardColorTag();
		var exiledCards = new List<GameObject>();

		for (var i = 0; i < amount; i++)
		{
			var targetCard = cardsToExile[i];
			var targetCardScript = targetCard.GetComponent<CardScript>();
			string targetColor = GetCardColorTag(targetCard);

			// Use unified destroy method (with animation) - Exile effect is similar to destroy, both remove card from game
			CombatUXManager.me.DestroyCardWithAnimation(targetCard);

			effectResultString.value += "// [<color=" + myColor + ">" + myCard.gameObject.name + "</color>]放逐了[<color=" + targetColor + ">" +
				targetCardScript.gameObject.name + "</color>]\n";

			exiledCards.Add(targetCard);
		}

		// Trigger onFriendlyCardExiled event (check if friendly card was exiled)
		foreach (var card in exiledCards)
		{
			var cardScript = card.GetComponent<CardScript>();
			bool isMyCard = cardScript.myStatusRef == myCardScript.myStatusRef;
			if (isMyCard)
			{
				if (GameEventStorage.me.onFriendlyCardExiled != null)
				{
					if (cardScript.myStatusRef == combatManager.ownerPlayerStatusRef)
					{
						GameEventStorage.me.onFriendlyCardExiled.RaiseOwner();
					}
					else
					{
						GameEventStorage.me.onFriendlyCardExiled.RaiseOpponent();
					}
				}
			}
		}

		// Sync remaining physical card positions
		if (exiledCards.Count > 0)
		{
			CombatUXManager.me.SyncPhysicalCardsWithCombinedDeck();
			CombatUXManager.me.UpdateAllPhysicalCardTargets();
		}
	}
}
