using System.Collections.Generic;
using DefaultNamespace.Managers;
using UnityEngine;

public class BuryCostEffect : EffectScript
{
	public void ExecuteBuryCost()
	{
		int costCount = myCardScript.buryCost;
		
		if (costCount <= 0) return;

		var combinedDeck = combatManager.combinedDeckZone;

		// Collect own cards (exclude currently activating card)
		var eligibleCards = new List<GameObject>();
		foreach (var card in combinedDeck)
		{
			if (card == null) continue;
			
			var cardScript = card.GetComponent<CardScript>();
			if (cardScript == null) continue;
			
			// Skip neutral cards (Start Card, etc.)
			if (CombatManager.ShouldSkipEffectProcessing(cardScript)) continue;
			
			// Exclude currently activating card
			if (card == myCard) continue;
			
			// Only collect own cards
			if (cardScript.myStatusRef != myCardScript.myStatusRef) continue;
			
			eligibleCards.Add(card);
		}

		// Check if there are enough own cards
		if (eligibleCards.Count < costCount)
		{
			// Display failure message and prevent effect activation
			string myColor = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? "#87CEEB" : "orange";
			string failMessage = $"// [<color={myColor}>{myCard.name}</color>]埋葬消耗失败: 需要<color=yellow>{costCount}</color>张友方卡牌，找到<color=yellow>{eligibleCards.Count}</color>张\n";
			
			var container = GetComponent<CostNEffectContainer>();
			if (container != null)
			{
				container.SetCostNotMet(failMessage);
			}
			return;
		}

		// Randomly shuffle and select cards to bury
		eligibleCards = UtilityFuncManagerScript.ShuffleList(eligibleCards);
		var cardsToBury = eligibleCards.GetRange(0, costCount);

		// Modify logical list: move selected cards to bottom
		var buriedCards = new List<GameObject>();
		foreach (var card in cardsToBury)
		{
			if (combinedDeck.Contains(card))
			{
				combinedDeck.Remove(card);
				combinedDeck.Insert(0, card);  // Insert at bottom
				buriedCards.Add(card);
				
				var targetScript = card.GetComponent<CardScript>();
				string myColor = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? "#87CEEB" : "orange";
				string targetColor = targetScript.myStatusRef == combatManager.ownerPlayerStatusRef ? "#87CEEB" : "orange";
				effectResultString.value += $"// [<color={myColor}>{myCard.name}</color>]埋葬消耗: 将[<color={targetColor}>{targetScript.name}</color>]埋入牌库底端\n";
			}
		}

		// Play arc trajectory animation (move cards to bottom)
		foreach (var card in buriedCards)
		{
			CombatUXManager.me.MoveCardToBottom(card, duration: 0.5f, useArc: true);
		}
		
		// Trigger friendly card buried event
		foreach (var card in buriedCards)
		{
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
	}
}
