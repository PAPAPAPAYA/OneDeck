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
	public void SendRandomCardsToGrave(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsToSend = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsToSend, true);
		cardsToSend = UtilityFuncManagerScript.ShuffleList(cardsToSend);
		amount = Mathf.Clamp(amount, 0, _combinedDeck.Count);
		for (var i = 0; i < amount; i++)
		{
			var targetCardScript = cardsToSend[i].GetComponent<CardScript>();
			var targetCardOwnerString = targetCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? "Your [" : "Enemy's [";
			effectResultString.value +=
				"// [" + myCard.gameObject.name + "] sent " +
				targetCardOwnerString +
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
			var targetCardOwnerString = targetCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? "Your [" : "Enemy's [";
			effectResultString.value +=
				"// [" + myCard.gameObject.name + "] put " +
				targetCardOwnerString +
				targetCardScript.gameObject.name + "] back to deck\n";
			CombatFuncs.me.MoveCard_FromGraveToDeck(cardsToPut[i]);
		}
	}
	
	public void StageSelf() // put self on top of the deck
	{
		if (!_combinedDeck.Contains(myCard)) return;
		_combinedDeck.Remove(myCard);
		_combinedDeck.Add(myCard);
		effectResultString.value += "// ["+myCard.name+"] is staged to the top of the deck\n";
	}

	public void StageCardsWithStatusEffect(int amount, EnumStorage.StatusEffect statusEffectToCheck) //todo put random cards with statusEffectToCheck on top of the deck
	{
	}

	public void BurySelf() // put self at the bottom of the deck
	{
		if (!_combinedDeck.Contains(transform.parent.gameObject)) return;
		_combinedDeck.Remove(transform.parent.gameObject);
		_combinedDeck.Insert(0, transform.parent.gameObject);
		effectResultString.value += "// ["+myCard.name+"] is buried to the bottom of the deck\n";
	}
}