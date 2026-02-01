using System;
using System.Collections.Generic;
using DefaultNamespace.Managers;
using DefaultNamespace.SOScripts;
using Unity.VisualScripting;
using UnityEngine;

public class CardManipulationEffect : EffectScript
{
	private List<GameObject> _combinedDeck;
	private List<GameObject> _graveDeck;

	// choose cards logic in here, move cards logic in CombatFuncs
	public void SendRandomMyCardsToGrave(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsToSend = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsToSend, true);
		for (int i = cardsToSend.Count - 1; i >= 0; i--)
		{
			if (cardsToSend[i].GetComponent<CardScript>().myStatusRef != myCardScript.myStatusRef) // if card doesn't belong to this card's owner
			{
				cardsToSend.RemoveAt(i);
			}
		}
		cardsToSend = UtilityFuncManagerScript.ShuffleList(cardsToSend);
		SendChosenCardsToGrave(cardsToSend, amount);
	}
	public void SendRandomCardsToGrave(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsToSend = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsToSend, true);
		cardsToSend = UtilityFuncManagerScript.ShuffleList(cardsToSend);
		SendChosenCardsToGrave(cardsToSend, amount);
	}

	private void SendChosenCardsToGrave(List<GameObject> cardsToSend, int amount)
	{
		if (amount == 0) return;
		amount = Mathf.Clamp(amount, 0, _combinedDeck.Count);
		for (var i = 0; i < amount; i++)
		{
			var targetCardScript = cardsToSend[i].GetComponent<CardScript>();
			effectResultString.value +=
				"// [" + myCard.gameObject.name + "] sent " +
				targetCardScript.gameObject.name + "] to grave\n";
			CombatFuncs.me.MoveCard_FromDeckToGrave(cardsToSend[i]);
		}
	}

	public void PutCardsBackToDeck(int amount)
	{
		_graveDeck = combatManager.graveZone;
		var cardsToPut = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_graveDeck, cardsToPut, true);
		cardsToPut = UtilityFuncManagerScript.ShuffleList(cardsToPut);
		amount = Mathf.Clamp(amount, 0, _graveDeck.Count);
		for (var i = 0; i < amount; i++)
		{
			var targetCardScript = cardsToPut[i].GetComponent<CardScript>();
			var targetCardOwnerString = CombatInfoDisplayer.me.ReturnCardOwnerInfo(targetCardScript.myStatusRef);
			var thisCardOwnerString = CombatInfoDisplayer.me.ReturnCardOwnerInfo(myCardScript.myStatusRef);
			effectResultString.value +=
				"// "+
				thisCardOwnerString +
				" [" + myCard.gameObject.name + "] put " +
				targetCardOwnerString +
				" [" + targetCardScript.gameObject.name + "] back to deck\n";
			CombatFuncs.me.MoveCard_FromGraveToDeck(cardsToPut[i]);
		}
	}

	public void ReviveSelf() // put self back from grave to deck
	{
		if (!combatManager.graveZone.Contains(transform.parent.gameObject)) return;
		CombatFuncs.me.MoveCard_FromGraveToDeck(myCard);
		// show info
		effectResultString.value += "// [" + myCard.name + "] is put back to deck\n";
	}

	public void StageSelf() // put self on top of the deck
	{
		if (!_combinedDeck.Contains(myCard)) return;
		_combinedDeck.Remove(myCard);
		_combinedDeck.Add(myCard);
		effectResultString.value += "// [" + myCard.name + "] is staged to the top of the deck\n";
	}

	public void StageCardsWithStatusEffect(int amount, EnumStorage.StatusEffect statusEffectToCheck) //todo put random cards with statusEffectToCheck on top of the deck
	{
	}

	public void BurySelf() // put self at the bottom of the deck
	{
		if (!_combinedDeck.Contains(transform.parent.gameObject)) return;
		_combinedDeck.Remove(transform.parent.gameObject);
		_combinedDeck.Insert(0, transform.parent.gameObject);
		effectResultString.value += "// [" + myCard.name + "] is buried to the bottom of the deck\n";
	}
}