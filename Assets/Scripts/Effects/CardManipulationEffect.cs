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
	
	[Header("Tag Configuration")]
	public EnumStorage.Tag tagToCheck;

	// choose cards logic in here, move cards logic in CombatFuncs
	public void ExileMyCards(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsToSend = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsToSend, true);
		for (int i = cardsToSend.Count - 1; i >= 0; i--)
		{
			// take out opponent's cards
			if (cardsToSend[i].GetComponent<CardScript>().myStatusRef != myCardScript.myStatusRef) // if card doesn't belong to this card's owner
			{
				cardsToSend.RemoveAt(i);
			}
		}
		cardsToSend = UtilityFuncManagerScript.ShuffleList(cardsToSend);
		ExileChosenCards(cardsToSend, amount);
	}
	public void ExileRandomCards(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsToSend = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsToSend, true);
		cardsToSend = UtilityFuncManagerScript.ShuffleList(cardsToSend);
		ExileChosenCards(cardsToSend, amount);
	}

	public void ExileTheirCards(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsToSend = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsToSend, true);
		for (int i = cardsToSend.Count - 1; i >= 0; i--)
		{
			// take out card owner's cards
			if (cardsToSend[i].GetComponent<CardScript>().myStatusRef == myCardScript.myStatusRef) // if card belongs to this card's owner
			{
				cardsToSend.RemoveAt(i);
			}
		}
		cardsToSend = UtilityFuncManagerScript.ShuffleList(cardsToSend);
		ExileChosenCards(cardsToSend, amount);
	}

	private void ExileChosenCards(List<GameObject> cardsToSend, int amount)
	{
		amount = Mathf.Clamp(amount, 0, _combinedDeck.Count);
		if (amount == 0) return;
		if (cardsToSend.Count == 0) return;
		for (var i = 0; i < amount; i++)
		{
			var targetCardScript = cardsToSend[i].GetComponent<CardScript>();
			effectResultString.value +=
				"// [" + myCard.gameObject.name + "] sent " +
				targetCardScript.gameObject.name + "] to grave\n";
			CombatFuncs.me.MoveCard_FromDeckToGrave(cardsToSend[i]);
		}
	}

	public void ReviveRandomCards(int amount)
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

	public void StageCardsWithTag(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsWithTag = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsWithTag, true);
		
		// Filter cards that have the specified tag
		for (int i = cardsWithTag.Count - 1; i >= 0; i--)
		{
			var cardScript = cardsWithTag[i].GetComponent<CardScript>();
			if (!cardScript.myTags.Contains(tagToCheck))
			{
				cardsWithTag.RemoveAt(i);
			}
		}
		
		cardsWithTag = UtilityFuncManagerScript.ShuffleList(cardsWithTag);
		StageChosenCards(cardsWithTag, amount);
	}

	public void StageMyCardsWithTag(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsWithTag = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsWithTag, true);
		
		// Filter cards that have the specified tag and belong to this card's owner
		for (int i = cardsWithTag.Count - 1; i >= 0; i--)
		{
			var cardScript = cardsWithTag[i].GetComponent<CardScript>();
			if (!cardScript.myTags.Contains(tagToCheck) || cardScript.myStatusRef != myCardScript.myStatusRef)
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
		
		// Filter cards that have the specified tag and belong to the opponent
		for (int i = cardsWithTag.Count - 1; i >= 0; i--)
		{
			var cardScript = cardsWithTag[i].GetComponent<CardScript>();
			if (!cardScript.myTags.Contains(tagToCheck) || cardScript.myStatusRef == myCardScript.myStatusRef)
			{
				cardsWithTag.RemoveAt(i);
			}
		}
		
		cardsWithTag = UtilityFuncManagerScript.ShuffleList(cardsWithTag);
		StageChosenCards(cardsWithTag, amount);
	}

	private void StageChosenCards(List<GameObject> cardsToStage, int amount)
	{
		amount = Mathf.Clamp(amount, 0, cardsToStage.Count);
		if (amount == 0) return;
		
		// Stage selected cards to the top of the deck (remove then add to end)
		for (var i = 0; i < amount; i++)
		{
			var targetCard = cardsToStage[i];
			var targetCardScript = targetCard.GetComponent<CardScript>();
			
			if (_combinedDeck.Contains(targetCard))
			{
				_combinedDeck.Remove(targetCard);
				_combinedDeck.Add(targetCard);
				effectResultString.value += "// [" + myCard.gameObject.name + "] staged [" + 
					targetCardScript.gameObject.name + "] to the top of the deck\n";
			}
		}
	}

	public void BurySelf() // put self at the bottom of the deck
	{
		if (!_combinedDeck.Contains(transform.parent.gameObject)) return;
		_combinedDeck.Remove(transform.parent.gameObject);
		_combinedDeck.Insert(0, transform.parent.gameObject);
		effectResultString.value += "// [" + myCard.name + "] is buried to the bottom of the deck\n";
	}

	public void BuryCardsWithTag(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsWithTag = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsWithTag, true);
		
		// Filter cards that have the specified tag
		for (int i = cardsWithTag.Count - 1; i >= 0; i--)
		{
			var cardScript = cardsWithTag[i].GetComponent<CardScript>();
			if (!cardScript.myTags.Contains(tagToCheck))
			{
				cardsWithTag.RemoveAt(i);
			}
		}
		
		cardsWithTag = UtilityFuncManagerScript.ShuffleList(cardsWithTag);
		BuryChosenCards(cardsWithTag, amount);
	}

	public void BuryMyCardsWithTag(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsWithTag = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsWithTag, true);
		
		// Filter cards that have the specified tag and belong to this card's owner
		for (int i = cardsWithTag.Count - 1; i >= 0; i--)
		{
			var cardScript = cardsWithTag[i].GetComponent<CardScript>();
			if (!cardScript.myTags.Contains(tagToCheck) || cardScript.myStatusRef != myCardScript.myStatusRef)
			{
				cardsWithTag.RemoveAt(i);
			}
		}
		
		cardsWithTag = UtilityFuncManagerScript.ShuffleList(cardsWithTag);
		BuryChosenCards(cardsWithTag, amount);
	}

	public void BuryTheirCardsWithTag(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsWithTag = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsWithTag, true);
		
		// Filter cards that have the specified tag and belong to the opponent
		for (int i = cardsWithTag.Count - 1; i >= 0; i--)
		{
			var cardScript = cardsWithTag[i].GetComponent<CardScript>();
			if (!cardScript.myTags.Contains(tagToCheck) || cardScript.myStatusRef == myCardScript.myStatusRef)
			{
				cardsWithTag.RemoveAt(i);
			}
		}
		
		cardsWithTag = UtilityFuncManagerScript.ShuffleList(cardsWithTag);
		BuryChosenCards(cardsWithTag, amount);
	}

	private void BuryChosenCards(List<GameObject> cardsToBury, int amount)
	{
		amount = Mathf.Clamp(amount, 0, cardsToBury.Count);
		if (amount == 0) return;
		
		// Bury selected cards to the bottom of the deck (remove then insert at beginning)
		for (var i = 0; i < amount; i++)
		{
			var targetCard = cardsToBury[i];
			var targetCardScript = targetCard.GetComponent<CardScript>();
			
			if (_combinedDeck.Contains(targetCard))
			{
				_combinedDeck.Remove(targetCard);
				_combinedDeck.Insert(0, targetCard);
				effectResultString.value += "// [" + myCard.gameObject.name + "] buried [" + 
					targetCardScript.gameObject.name + "] to the bottom of the deck\n";
			}
		}
	}

	public void ReviveRandomMyCardsFromGrave(int amount)
	{
		_graveDeck = combatManager.graveZone;
		var cardsToRevive = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_graveDeck, cardsToRevive, true);
		for (int i = cardsToRevive.Count - 1; i >= 0; i--)
		{
			// take out opponent's cards
			if (cardsToRevive[i].GetComponent<CardScript>().myStatusRef != myCardScript.myStatusRef) // if card doesn't belong to this card's owner
			{
				cardsToRevive.RemoveAt(i);
			}
		}
		cardsToRevive = UtilityFuncManagerScript.ShuffleList(cardsToRevive);
		amount = Mathf.Clamp(amount, 0, cardsToRevive.Count);
		if (amount == 0) return;
		for (var i = 0; i < amount; i++)
		{
			var targetCardScript = cardsToRevive[i].GetComponent<CardScript>();
			effectResultString.value +=
				"// [" + myCard.gameObject.name + "] revived " +
				"[" + targetCardScript.gameObject.name + "] from grave to deck\n";
			CombatFuncs.me.MoveCard_FromGraveToDeck(cardsToRevive[i]);
		}
	}

	public void ReviveRandomTheirCardsFromGrave(int amount)
	{
		_graveDeck = combatManager.graveZone;
		var cardsToRevive = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_graveDeck, cardsToRevive, true);
		for (int i = cardsToRevive.Count - 1; i >= 0; i--)
		{
			// take out card owner's cards
			if (cardsToRevive[i].GetComponent<CardScript>().myStatusRef == myCardScript.myStatusRef) // if card belongs to this card's owner
			{
				cardsToRevive.RemoveAt(i);
			}
		}
		cardsToRevive = UtilityFuncManagerScript.ShuffleList(cardsToRevive);
		amount = Mathf.Clamp(amount, 0, cardsToRevive.Count);
		if (amount == 0) return;
		for (var i = 0; i < amount; i++)
		{
			var targetCardScript = cardsToRevive[i].GetComponent<CardScript>();
			effectResultString.value +=
				"// [" + myCard.gameObject.name + "] revived " +
				"[" + targetCardScript.gameObject.name + "] from grave to deck\n";
			CombatFuncs.me.MoveCard_FromGraveToDeck(cardsToRevive[i]);
		}
	}
}