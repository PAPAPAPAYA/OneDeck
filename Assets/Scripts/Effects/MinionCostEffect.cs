using System.Collections.Generic;
using DefaultNamespace.Managers;
using UnityEngine;

// CardManipulationEffect and GameEventStorage are in the global namespace

public class MinionCostEffect : EffectScript
{
	public void ExecuteMinionCost()
	{
		int costCount = myCardScript.minionCostCount;
		string costCardTypeID = myCardScript.minionCostCardTypeID;
		var costOwner = myCardScript.minionCostOwner;

		if (costCount <= 0) return;

		var combinedDeck = combatManager.combinedDeckZone;

		// Collect eligible cards (specified owner, specified type)
		var eligibleCards = new List<GameObject>();
		foreach (var card in combinedDeck)
		{
			if (card == null) continue;
			
			var cardScript = card.GetComponent<CardScript>();
			if (cardScript == null) continue;
			
			// Skip neutral cards and Start Card
			if (CombatManager.ShouldSkipEffectProcessing(cardScript)) continue;
			
			// Check if it's a minion card
			if (!cardScript.isMinion) continue;
			
			// Check card owner
			bool isMyCard = cardScript.myStatusRef == myCardScript.myStatusRef;
			switch (costOwner)
			{
				case EnumStorage.TargetType.Me:
					if (!isMyCard) continue;
					break;
				case EnumStorage.TargetType.Them:
					if (isMyCard) continue;
					break;
				case EnumStorage.TargetType.Random:
					// Any owner is eligible
					break;
			}
			
			// Check card type (if type is specified)
			if (!string.IsNullOrEmpty(costCardTypeID))
			{
				if (cardScript.cardTypeID != costCardTypeID) continue;
			}
			
			eligibleCards.Add(card);
		}

		// Check if there are enough eligible cards
		if (eligibleCards.Count < costCount)
		{
			// Display failure message and prevent effect activation
			string myColor = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? "#87CEEB" : "orange";
			string typeInfo = string.IsNullOrEmpty(costCardTypeID) ? "card(s)" : $"[{costCardTypeID}]";
			string ownerInfo = GetOwnerDescription(costOwner);
			string failMessage = $"// [<color={myColor}>{myCard.name}</color>] token cost failed: need <color=yellow>{costCount}</color> {ownerInfo} {typeInfo}, found <color=yellow>{eligibleCards.Count}</color>\n";
			
			var container = GetComponent<CostNEffectContainer>();
			if (container != null)
			{
				container.SetCostNotMet(failMessage);
			}
			return;
		}

		// Randomly shuffle and select cards to consume
		eligibleCards = UtilityFuncManagerScript.ShuffleList(eligibleCards);
		var cardsToConsume = eligibleCards.GetRange(0, costCount);

		// Consume these cards from deck (using unified destroy method with animation)
		int destroyedCount = 0;
		foreach (var card in cardsToConsume)
		{
			var cardScript = card.GetComponent<CardScript>();
			bool isMyCard = cardScript.myStatusRef == myCardScript.myStatusRef;
			
			// Trigger onFriendlyCardExiled event (if friendly card is consumed)
			// Trigger corresponding side's event based on consumed card's ownership: RaiseOwner for player side, RaiseOpponent for enemy side
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
			
			// Trigger OnFriendlyFlyExiled event (if friendly fly card)
			// Trigger corresponding side's event based on consumed fly's ownership: RaiseOwner for player side, RaiseOpponent for enemy side
			if (isMyCard && cardScript.cardTypeID == "FLY")
			{
				if (GameEventStorage.me.onFriendlyFlyExiled != null)
				{
					if (cardScript.myStatusRef == combatManager.ownerPlayerStatusRef)
					{
						GameEventStorage.me.onFriendlyFlyExiled.RaiseOwner();
					}
					else
					{
						GameEventStorage.me.onFriendlyFlyExiled.RaiseOpponent();
					}
				}
			}
			
			CombatUXManager.me.DestroyCardWithAnimation(card, onComplete: () =>
			{
				destroyedCount++;
			});
		}

		// Sync remaining physical card positions
		if (cardsToConsume.Count > 0)
		{
			CombatUXManager.me.SyncPhysicalCardsWithCombinedDeck();
			CombatUXManager.me.UpdateAllPhysicalCardTargets();
		}

		// Display consumption info
		string ownerColor = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? "#87CEEB" : "orange";
		string cardTypeInfo = string.IsNullOrEmpty(costCardTypeID) ? "card(s)" : $"[{costCardTypeID}]";
		string consumedOwnerInfo = GetOwnerDescription(costOwner);
		effectResultString.value += $"// [<color={ownerColor}>{myCard.name}</color>] minion cost: consumed <color=yellow>{cardsToConsume.Count}</color> {consumedOwnerInfo} {cardTypeInfo}\n";
	}

	private string GetOwnerDescription(EnumStorage.TargetType owner)
	{
		return owner switch
		{
			EnumStorage.TargetType.Me => "ally minion",
			EnumStorage.TargetType.Them => "enemy minion",
			EnumStorage.TargetType.Random => "any minion",
			_ => ""
		};
	}
}
