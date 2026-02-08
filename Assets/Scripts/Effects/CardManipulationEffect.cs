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

	/// <summary>
	/// 获取卡牌所属者的颜色标签（玩家=#87CEEB，敌人=orange）
	/// </summary>
	private string GetCardColorTag(GameObject card)
	{
		var cardStatus = card.GetComponent<CardScript>().myStatusRef;
		return cardStatus == combatManager.ownerPlayerStatusRef ? "#87CEEB" : "orange";
	}

	/// <summary>
	/// 获取当前卡牌的颜色标签
	/// </summary>
	private string GetMyCardColorTag()
	{
		return myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? "#87CEEB" : "orange";
	}

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
		string myColor = GetMyCardColorTag();
		for (var i = 0; i < amount; i++)
		{
			var targetCardScript = cardsToSend[i].GetComponent<CardScript>();
			string targetColor = GetCardColorTag(cardsToSend[i]);
			effectResultString.value +=
				"// [<color=" + myColor + ">" + myCard.gameObject.name + "</color>] sent [<color=" + targetColor + ">" +
				targetCardScript.gameObject.name + "</color>] to grave\n";
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
		string myColor = GetMyCardColorTag();
		for (var i = 0; i < amount; i++)
		{
			var targetCardScript = cardsToPut[i].GetComponent<CardScript>();
			string targetColor = GetCardColorTag(cardsToPut[i]);
			var targetCardOwnerString = CombatInfoDisplayer.me.ReturnCardOwnerInfo(targetCardScript.myStatusRef);
			var thisCardOwnerString = CombatInfoDisplayer.me.ReturnCardOwnerInfo(myCardScript.myStatusRef);
			effectResultString.value +=
				"// "+
				thisCardOwnerString +
				" [<color=" + myColor + ">" + myCard.gameObject.name + "</color>] put " +
				targetCardOwnerString +
				" [<color=" + targetColor + ">" + targetCardScript.gameObject.name + "</color>] back to deck\n";
			CombatFuncs.me.MoveCard_FromGraveToDeck(cardsToPut[i]);
		}
	}

	public void ReviveSelf() // put self back from grave to deck
	{
		if (!combatManager.graveZone.Contains(transform.parent.gameObject)) return;
		CombatFuncs.me.MoveCard_FromGraveToDeck(myCard);
		// show info
		string myColor = GetMyCardColorTag();
		effectResultString.value += "// [<color=" + myColor + ">" + myCard.name + "</color>] is put back to deck\n";
	}

	public void StageSelf() // put self on top of the deck
	{
		_combinedDeck = combatManager.combinedDeckZone;
		if (!_combinedDeck.Contains(myCard)) return;
		_combinedDeck.Remove(myCard);
		_combinedDeck.Add(myCard);
		string myColor = GetMyCardColorTag();
		effectResultString.value += "// [<color=" + myColor + ">" + myCard.name + "</color>] is staged to the top of the deck\n";
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
				string myColor = GetMyCardColorTag();
				string targetColor = GetCardColorTag(targetCard);
				effectResultString.value += "// [<color=" + myColor + ">" + myCard.gameObject.name + "</color>] staged [<color=" + targetColor + ">" + 
					targetCardScript.gameObject.name + "</color>] to the top of the deck\n";
			}
		}
	}

	public void BurySelf() // put self at the bottom of the deck
	{
		_combinedDeck = combatManager.combinedDeckZone;
		if (!_combinedDeck.Contains(transform.parent.gameObject)) return;
		_combinedDeck.Remove(transform.parent.gameObject);
		_combinedDeck.Insert(0, transform.parent.gameObject);
		string myColor = GetMyCardColorTag();
		effectResultString.value += "// [<color=" + myColor + ">" + myCard.name + "</color>] is buried to the bottom of the deck\n";
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
				string myColor = GetMyCardColorTag();
				string targetColor = GetCardColorTag(targetCard);
				effectResultString.value += "// [<color=" + myColor + ">" + myCard.gameObject.name + "</color>] buried [<color=" + targetColor + ">" + 
					targetCardScript.gameObject.name + "</color>] to the bottom of the deck\n";
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
		string myColor = GetMyCardColorTag();
		for (var i = 0; i < amount; i++)
		{
			var targetCardScript = cardsToRevive[i].GetComponent<CardScript>();
			string targetColor = GetCardColorTag(cardsToRevive[i]);
			effectResultString.value +=
				"// [<color=" + myColor + ">" + myCard.gameObject.name + "</color>] revived " +
				"[<color=" + targetColor + ">" + targetCardScript.gameObject.name + "</color>] from grave to deck\n";
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
		string myColor = GetMyCardColorTag();
		for (var i = 0; i < amount; i++)
		{
			var targetCardScript = cardsToRevive[i].GetComponent<CardScript>();
			string targetColor = GetCardColorTag(cardsToRevive[i]);
			effectResultString.value +=
				"// [<color=" + myColor + ">" + myCard.gameObject.name + "</color>] revived " +
				"[<color=" + targetColor + ">" + targetCardScript.gameObject.name + "</color>] from grave to deck\n";
			CombatFuncs.me.MoveCard_FromGraveToDeck(cardsToRevive[i]);
		}
	}
}