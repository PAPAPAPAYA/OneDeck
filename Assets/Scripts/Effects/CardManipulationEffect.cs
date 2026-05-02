using System;
using System.Collections.Generic;
using DefaultNamespace.Managers;
using DefaultNamespace.SOScripts;
using Unity.VisualScripting;
using UnityEngine;

public class CardManipulationEffect : EffectScript
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


	#region REVIVE
	public void ReviveRandomMyCardsFromGrave(int amount)
	{
		// [Deprecated] Graveyard mechanic removed
		return;
	}

	public void ReviveRandomTheirCardsFromGrave(int amount)
	{
		// [Deprecated] Graveyard mechanic removed
		return;
	}
	public void ReviveRandomCards(int amount)
	{
		// [Deprecated] Graveyard mechanic removed
		return;
	}

	public void ReviveSelf() // put self back from grave to deck
	{
		// [Deprecated] Graveyard mechanic removed
		return;
	}
	#endregion
	
	#region DELAY
	public void DelayMyCards(int amount)
	{
		var myCards = GetCardsByOwner(isMyCards: true);
		ExecuteDelay(myCards, amount);
	}

	public void DelayTheirCards(int amount)
	{
		var theirCards = GetCardsByOwner(isMyCards: false);
		ExecuteDelay(theirCards, amount);
	}

	private List<GameObject> GetCardsByOwner(bool isMyCards)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var result = new List<GameObject>();
		for (int i = 0; i < _combinedDeck.Count; i++)
		{
			var card = _combinedDeck[i];
			var cardScript = card.GetComponent<CardScript>();
			// Skip neutral cards and Start Card
			if (CombatManager.ShouldSkipEffectProcessing(cardScript)) continue;
			bool isOwner = cardScript.myStatusRef == myCardScript.myStatusRef;
			// Only return cards with correct ownership and index > 0 (cards at index 0 cannot be delayed)
			if (isOwner == isMyCards && i > 0)
			{
				result.Add(card);
			}
		}
		return result;
	}

	private void ExecuteDelay(List<GameObject> candidates, int amount)
	{
		if (amount <= 0 || candidates.Count == 0) return;

		candidates = UtilityFuncManagerScript.ShuffleList(candidates);
		amount = Mathf.Min(amount, candidates.Count);

		int movedCount = 0;
		string myColor = GetMyCardColorTag();
		var delayedCards = new List<(GameObject card, int newIndex)>();

		for (int i = 0; i < amount; i++)
		{
			var card = candidates[i];
			int index = _combinedDeck.IndexOf(card);

			// Index check already done in GetCardsByOwner, this is a defensive check
			if (index <= 0) continue;

			_combinedDeck.RemoveAt(index);
			int newIndex = index - 1;
			_combinedDeck.Insert(newIndex, card);
			movedCount++;
			delayedCards.Add((card, newIndex));

			var targetScript = card.GetComponent<CardScript>();
			string targetColor = GetCardColorTag(card);
			AppendLog($"// [<color={myColor}>{myCard.name}</color>]延迟了[<color={targetColor}>{targetScript.name}</color>]");
		}

		if (movedCount > 0)
		{
			// Sync physical card list
			combatManager.visuals.SyncPhysicalCardsWithCombinedDeck();
			
			// Play animation for each moved card
			foreach (var (card, newIndex) in delayedCards)
			{
				combatManager.visuals.MoveCardToIndex(card, newIndex, duration: 0.3f, useArc: false);
			}
			
			// Update other card positions (ensure all card positions are correct)
			combatManager.visuals.UpdateAllPhysicalCardTargets();
		}
	}
	#endregion

	#region DESTROY MINION
	/// <summary>
	/// Destroy specified number of own Minion cards
	/// </summary>
	public void DestroyMyMinions(int amount)
	{
		var minions = GetMinionsByOwner(isMyMinions: true);
		ExecuteDestroyMinions(minions, amount);
	}
	
	/// <summary>
	/// Destroy specified number of enemy Minion cards
	/// </summary>
	public void DestroyTheirMinions(int amount)
	{
		var minions = GetMinionsByOwner(isMyMinions: false);
		ExecuteDestroyMinions(minions, amount);
	}
	
	/// <summary>
	/// Destroy specified number of any Minion cards (random selection)
	/// </summary>
	public void DestroyRandomMinions(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var minions = new List<GameObject>();
		
		foreach (var card in _combinedDeck)
		{
			if (card == null) continue;
			var cardScript = card.GetComponent<CardScript>();
			if (cardScript == null) continue;
			if (cardScript.isMinion)
			{
				minions.Add(card);
			}
		}
		
		ExecuteDestroyMinions(minions, amount);
	}
	
	/// <summary>
	/// Destroy specified number of Minion cards with specified Tag
	/// </summary>
	public void DestroyMinionsWithTag(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var minionsWithTag = new List<GameObject>();
		
		foreach (var card in _combinedDeck)
		{
			if (card == null) continue;
			var cardScript = card.GetComponent<CardScript>();
			if (cardScript == null) continue;
			if (cardScript.isMinion && cardScript.myTags.Contains(tagToCheck))
			{
				minionsWithTag.Add(card);
			}
		}
		
		ExecuteDestroyMinions(minionsWithTag, amount);
	}
	
	/// <summary>
	/// Get Minion card list with specified ownership
	/// </summary>
	private List<GameObject> GetMinionsByOwner(bool isMyMinions)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var result = new List<GameObject>();
		
		foreach (var card in _combinedDeck)
		{
			if (card == null) continue;
			var cardScript = card.GetComponent<CardScript>();
			if (cardScript == null) continue;
			if (!cardScript.isMinion) continue;
			
			bool isOwner = cardScript.myStatusRef == myCardScript.myStatusRef;
			if (isOwner == isMyMinions)
			{
				result.Add(card);
			}
		}
		
		return result;
	}
	
	/// <summary>
	/// Execute Minion card destruction (with animation)
	/// </summary>
	private void ExecuteDestroyMinions(List<GameObject> minions, int amount)
	{
		if (amount <= 0 || minions.Count == 0) return;
		
		minions = UtilityFuncManagerScript.ShuffleList(minions);
		amount = Mathf.Min(amount, minions.Count);
		
		string myColor = GetMyCardColorTag();
		
		for (int i = 0; i < amount; i++)
		{
			var minion = minions[i];
			var minionScript = minion.GetComponent<CardScript>();
			string minionColor = GetCardColorTag(minion);
			
			// Use unified destroy method (with animation)
			combatManager.visuals.DestroyCardWithAnimation(minion);
			
			AppendLog($"// [<color={myColor}>{myCard.name}</color>]摧毁了随从[<color={minionColor}>{minionScript.name}</color>]");
		}
		
		// Sync remaining physical card positions
		if (amount > 0)
		{
			combatManager.visuals.SyncPhysicalCardsWithCombinedDeck();
			combatManager.visuals.UpdateAllPhysicalCardTargets();
		}
	}
	#endregion

	#region IntSO Based Effects

	public void DestroyTheirMinions_BasedOnIntSO(IntSO intSO)
	{
		if (intSO == null) return;
		DestroyTheirMinions(intSO.value);
	}

	public void DestroyMyMinions_BasedOnIntSO(IntSO intSO)
	{
		if (intSO == null) return;
		DestroyMyMinions(intSO.value);
	}

	#endregion
}
