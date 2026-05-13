using System.Collections.Generic;
using DefaultNamespace.Managers;
using UnityEngine;

// CardManipulationEffect and GameEventStorage are in the global namespace

public class ExileCostEffect : EffectScript
{
	public void ExecuteExileCost()
	{
		int costCount = myCardScript.exileCostCount;
		string costCardTypeID = myCardScript.exileCostCardTypeID;
		var costOwner = myCardScript.exileCostOwner;

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

			// Exclude currently activating card
			if (card == myCard) continue;

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
			string failMessage = $"// [<color={myColor}>{myCard.name}</color>] exile cost failed: need <color=yellow>{costCount}</color> {ownerInfo} {typeInfo}, found <color=yellow>{eligibleCards.Count}</color>\n";

			var container = GetComponent<CostNEffectContainer>();
			if (container != null)
			{
				container.SetCostNotMet(failMessage);
			}
			return;
		}

		// Randomly shuffle and select cards to exile
		eligibleCards = UtilityFuncManagerScript.ShuffleList(eligibleCards);
		var cardsToExile = eligibleCards.GetRange(0, costCount);

		// Exile these cards from deck (using unified destroy method with animation)
		int destroyedCount = 0;
		foreach (var card in cardsToExile)
		{
			var cardScript = card.GetComponent<CardScript>();
			bool isMyCard = cardScript.myStatusRef == myCardScript.myStatusRef;

			// Trigger onFriendlyCardExiled event (if friendly card is exiled)
			// Trigger corresponding side's event based on exiled card's ownership: RaiseOwner for player side, RaiseOpponent for enemy side
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
			// Trigger corresponding side's event based on exiled fly's ownership: RaiseOwner for player side, RaiseOpponent for enemy side
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

			combatManager.visuals.DestroyCardWithAnimation(card, onComplete: () =>
			{
				destroyedCount++;
			});
		}

		// Sync remaining physical card positions
		if (cardsToExile.Count > 0)
		{
			combatManager.visuals.SyncPhysicalCardsWithCombinedDeck();
			combatManager.visuals.UpdateAllPhysicalCardTargets();
		}

		// Display exile info
		string ownerColor = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? "#87CEEB" : "orange";
		string cardTypeInfo = string.IsNullOrEmpty(costCardTypeID) ? "card" : $"[{costCardTypeID}]";
		string exiledOwnerInfo = GetOwnerDescription(costOwner);
		AppendLog($"// [<color={ownerColor}>{myCard.name}</color>] exile cost: exiled <color=yellow>{cardsToExile.Count}</color> {exiledOwnerInfo} {cardTypeInfo}");
	}

	private string GetOwnerDescription(EnumStorage.TargetType owner)
	{
		return owner switch
		{
			EnumStorage.TargetType.Me => "friendly",
			EnumStorage.TargetType.Them => "enemy",
			EnumStorage.TargetType.Random => "random",
			_ => ""
		};
	}
}
