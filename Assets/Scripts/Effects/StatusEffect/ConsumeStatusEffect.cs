using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace DefaultNamespace.Effects
{
	public class ConsumeStatusEffect : EffectScript
	{
		public EnumStorage.StatusEffect statusEffectToConsume;
		public void ConsumeOwnStatusEffect(int amount)
		{
			// first check if amount is met
			if (!EnumStorage.DoesListContainAmountOfStatusEffect(myCardScript.myStatusEffects, amount, statusEffectToConsume)) return;
			// then remove status effect
			var amountRemoved = 0;
			for (var i = myCardScript.myStatusEffects.Count - 1; i >= 0; i--)
			{
				if (myCardScript.myStatusEffects[i] == statusEffectToConsume && amountRemoved < amount)
				{
					myCardScript.myStatusEffects.RemoveAt(i);
					amountRemoved++;
				}
			}
			// lastly, refresh info display
			CombatInfoDisplayer.me.RefreshDeckInfo();
		}

		/// <summary>
		/// Randomly consume 1 statusEffectToConsume from X enemy cards in the combined deck.
		/// </summary>
		/// <param name="amount">Number of enemy cards to target</param>
		public void ConsumeRandomEnemyCardsStatusEffect(int amount)
		{
			var eligibleCards = new List<CardScript>();
			foreach (var card in CombatManager.Me.combinedDeckZone)
			{
				if (card == null) continue;
				var cardScript = card.GetComponent<CardScript>();
				if (cardScript == null) continue;
				if (CombatManager.ShouldSkipEffectProcessing(cardScript)) continue;
				if (cardScript.myStatusRef == myCardScript.myStatusRef) continue; // must be enemy card
				if (!cardScript.myStatusEffects.Contains(statusEffectToConsume)) continue; // must have the status effect
				eligibleCards.Add(cardScript);
			}

			if (eligibleCards.Count == 0) return;

			eligibleCards = UtilityFuncManagerScript.ShuffleList(eligibleCards);
			var targetCount = Mathf.Min(amount, eligibleCards.Count);
			for (var i = 0; i < targetCount; i++)
			{
				var targetCard = eligibleCards[i];
				targetCard.myStatusEffects.Remove(statusEffectToConsume);
			}

			CombatInfoDisplayer.me.RefreshDeckInfo();
		}

		// caution: only used by status effect resolver to destroy self after resolving
		public void DestroySelf()
		{
			Destroy(gameObject);
		}
	}
}