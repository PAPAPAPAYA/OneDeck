using System.Collections.Generic;
using DefaultNamespace.Managers;
using UnityEngine;

public class ExposeCostEffect : EffectScript
{
	public void ExecuteExposeCost()
	{
		int costCount = myCardScript.exposeCost;
		if (costCount <= 0) return;

		var combinedDeck = combatManager.combinedDeckZone;

		// Collect enemy cards (exclude neutral cards)
		var enemyCards = new List<GameObject>();
		foreach (var card in combinedDeck)
		{
			if (card == null) continue;
			
			var cardScript = card.GetComponent<CardScript>();
			if (cardScript == null) continue;
			
			// Skip neutral cards (Start Card, etc.)
			if (CombatManager.ShouldSkipEffectProcessing(cardScript)) continue;
			
			// Only collect enemy cards
			if (cardScript.myStatusRef == myCardScript.myStatusRef) continue;
			
			enemyCards.Add(card);
		}

		// Soft constraint: expose as many as possible
		int actualCount = Mathf.Min(costCount, enemyCards.Count);
		if (actualCount <= 0) return;

		// Randomly select and stage to top
		enemyCards = UtilityFuncManagerScript.ShuffleList(enemyCards);
		var cardsToExpose = enemyCards.GetRange(0, actualCount);
		
		var exposedCards = new List<GameObject>();
		foreach (var card in cardsToExpose)
		{
			if (combinedDeck.Contains(card))
			{
				combinedDeck.Remove(card);
				combinedDeck.Add(card);  // Add to end = stage to top
				exposedCards.Add(card);
				
				var targetScript = card.GetComponent<CardScript>();
				string myColor = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? "#87CEEB" : "orange";
				string targetColor = targetScript.myStatusRef == combatManager.ownerPlayerStatusRef ? "#87CEEB" : "orange";
				effectResultString.value += $"// [<color={myColor}>{myCard.name}</color>]暴露消耗: 将[<color={targetColor}>{targetScript.name}</color>]置顶\n";
			}
		}

		// Sync physical card positions
		if (exposedCards.Count > 0)
		{
			CombatUXManager.me.SyncPhysicalCardsWithCombinedDeck();
			CombatUXManager.me.UpdateAllPhysicalCardTargets();
		}
	}
}
