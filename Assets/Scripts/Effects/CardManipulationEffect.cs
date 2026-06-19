using System;
using System.Collections.Generic;
using DefaultNamespace;
using DefaultNamespace.Managers;
using DefaultNamespace.SOScripts;
using Unity.VisualScripting;
using UnityEngine;

public class CardManipulationEffect : EffectScript
{
	private List<GameObject> _combinedDeck;

	[Header("Tag Configuration")]
	public EnumStorage.Tag tagToCheck;

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
			
			// Capture animation requests
			var recorderGo = EffectChainManager.Me != null ? EffectChainManager.Me.currentEffectRecorder : null;
			var recorder = recorderGo != null ? recorderGo.GetComponent<EffectRecorder>() : null;
			if (recorder != null)
			{
				foreach (var (card, newIndex) in delayedCards)
				{
					recorder.animationRequests.Add(new AnimationRequest
					{
						type = AnimationRequestType.MoveToIndex,
						targetCard = card,
						targetIndex = newIndex,
						duration = 0.3f,
						useArc = false
					});
				}
			}
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
		
		// Remove from combined deck in logic phase so SyncPhysicalCards sees correct state
		for (int i = 0; i < amount; i++)
		{
			var minion = minions[i];
			if (combatManager.combinedDeckZone.Contains(minion))
			{
				combatManager.combinedDeckZone.Remove(minion);
			}
		}
		
		// Sync physical card list order with logical deck
		combatManager.visuals.SyncPhysicalCardsWithCombinedDeck();
		
		// Capture animation requests
		var recorderGo = EffectChainManager.Me != null ? EffectChainManager.Me.currentEffectRecorder : null;
		var recorder = recorderGo != null ? recorderGo.GetComponent<EffectRecorder>() : null;
		if (recorder != null)
		{
			for (int i = 0; i < amount; i++)
			{
				var minion = minions[i];
				var minionScript = minion.GetComponent<CardScript>();
				string minionColor = GetCardColorTag(minion);
				
				bool isLast = (i == amount - 1);
				recorder.animationRequests.Add(new AnimationRequest
				{
					type = AnimationRequestType.Destroy,
					targetCard = minion,
					onComplete = isLast ? (Action)(() => combatManager.visuals.UpdateAllPhysicalCardTargets()) : null
				});
				
				AppendLog($"// [<color={myColor}>{myCard.name}</color>]摧毁了随从[<color={minionColor}>{minionScript.name}</color>]");
			}
		}
	}
	#endregion

	#region IntSO Based Effects

	/// <summary>
	/// Based on ownerIntSO/enemyIntSO, destroy enemy Minion cards.
	/// Uses ownerIntSO when this card belongs to the owner, otherwise enemyIntSO.
	/// </summary>
	public virtual void DestroyTheirMinions_BasedOnIntSO()
	{
		IntSO intSO = GetIntSOForOwner(ownerIntSO, enemyIntSO);
		if (intSO == null) return;
		if (intSO.value <= 0) return;

		DestroyTheirMinions(intSO.value);
	}

	/// <summary>
	/// Based on ownerIntSO/enemyIntSO, destroy friendly Minion cards.
	/// Uses ownerIntSO when this card belongs to the owner, otherwise enemyIntSO.
	/// </summary>
	public virtual void DestroyMyMinions_BasedOnIntSO()
	{
		IntSO intSO = GetIntSOForOwner(ownerIntSO, enemyIntSO);
		if (intSO == null) return;
		if (intSO.value <= 0) return;

		DestroyMyMinions(intSO.value);
	}

	#endregion
}
