using System.Collections.Generic;
using DefaultNamespace.Managers;
using UnityEngine;

public class DelayCostEffect : EffectScript
{
	public void ExecuteDelayCost()
	{
		int cost = myCardScript.delayCost;
		if (cost <= 0) return;

		var combinedDeck = combatManager.combinedDeckZone;

		// Collect own cards (exclude currently activating card, it's already in revealZone)
		var myCards = new List<GameObject>();
		for (int i = 0; i < combinedDeck.Count; i++)
		{
			var cardScript = combinedDeck[i].GetComponent<CardScript>();
			// Skip neutral cards and Start Card, only collect own cards
			if (!CombatManager.ShouldSkipEffectProcessing(cardScript) && cardScript.myStatusRef == myCardScript.myStatusRef)
			{
				myCards.Add(combinedDeck[i]);
			}
		}

		if (myCards.Count == 0) return;

		// Randomly shuffle
		myCards = UtilityFuncManagerScript.ShuffleList(myCards);

		// Execute delay
		int movedCount = 0;
		for (int i = 0; i < myCards.Count && movedCount < cost; i++)
		{
			var card = myCards[i];

			// Find current index (because previous moves may have changed order)
			int currentIndex = combinedDeck.IndexOf(card);
			if (currentIndex < 0) continue; // Card no longer in deck (shouldn't happen)

			// If card is already at bottom (index 0), cannot delay further
			if (currentIndex <= 0) continue;

			// Move: remove then insert to index-1 (move toward bottom, delay reveal)
			combinedDeck.RemoveAt(currentIndex);
			combinedDeck.Insert(currentIndex - 1, card);
			movedCount++;
		}

		// Sync physical card positions
		if (movedCount > 0)
		{
			combatManager.visuals.SyncPhysicalCardsWithCombinedDeck();
			combatManager.visuals.UpdateAllPhysicalCardTargets();
		}

		// Display info
		if (movedCount > 0)
		{
			string myColor = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? "#87CEEB" : "orange";
			effectResultString.value += $"// [<color={myColor}>{myCard.name}</color>]延迟消耗: 将<color=yellow>{movedCount}</color>张卡牌后置\n";
		}
	}
}
