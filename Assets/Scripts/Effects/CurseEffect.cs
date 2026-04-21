using System.Collections.Generic;
using DefaultNamespace.Managers;
using DefaultNamespace.SOScripts;
using UnityEngine;

namespace DefaultNamespace.Effects
{
	public class CurseEffect : EffectScript
	{
		[Header("Curse Config")]
		[Tooltip("Type ID of the curse target card")]
		public StringSO cardTypeID;
		
		[Tooltip("Card prefab to spawn when no target card exists in deck")]
		public GameObject cardPrefab;
		
		[Header("Status Effect Config")]
		[Tooltip("Status effect resolver prefab (optional)")]
		public GameObject statusEffectResolverPrefab;
		
		[Tooltip("Particle system to play when applying status effect (optional)")]
		public ParticleSystem statusEffectParticlePrefab;
		
		[Tooltip("Y-axis offset for the particle system")]
		public float particleYOffset = 0f;

		[Header("Coefficient Config")]
		[Tooltip("Coefficient: for every this much IntSO value, enhance enemy curse by 1")]
		public int powerCoefficient = 1;

		/// <summary>
		/// Enhances curse: if no enemy card with the specified cardTypeID exists in combinedDeckZone,
		/// spawns one of that type, then applies Power status effect to that enemy card.
		/// </summary>
		/// <param name="powerAmount">Amount of Power stacks to apply.</param>
		public void EnhanceCurse(int powerAmount)
		{
			if (cardTypeID == null || string.IsNullOrEmpty(cardTypeID.value))
			{
				Debug.LogWarning("[CurseEffect] cardTypeID is not set!");
				return;
			}

			if (powerAmount <= 0)
			{
				return;
			}

			// Find enemy card with specified cardTypeID in combinedDeckZone
			CardScript targetCard = FindEnemyCardWithTypeID(cardTypeID.value);

			// If not found, spawn one
			if (targetCard == null)
			{
				if (cardPrefab == null)
				{
					Debug.LogWarning($"[CurseEffect] Card prefab is not set! Cannot create card with typeID: {cardTypeID.value}");
					return;
				}
				targetCard = CreateEnemyCard(cardPrefab);
			}

			// Apply Power status effect to target card
			if (targetCard != null)
			{
				ApplyPowerToCardWithProjectile(targetCard, powerAmount);
			}
		}

		/// <summary>
		/// Enhances curse: enhances enemy curse based on IntSO value.
		/// </summary>
		/// <param name="powerAmountSO">IntSO storing Power stack amount.</param>
		public void EnhanceCurseBasedOnIntSO(IntSO powerAmountSO)
		{
			if (powerAmountSO == null) return;
			EnhanceCurse(powerAmountSO.value);
		}

		/// <summary>
		/// Enhances curse (with coefficient): calculates enhancement stacks from IntSO value and coefficient,
		/// enhancing curse by 1 for every powerCoefficient points.
		/// </summary>
		/// <param name="powerAmountSO">IntSO storing Power stack amount.</param>
		public void EnhanceCurseWithCoefficient(IntSO powerAmountSO)
		{
			if (powerAmountSO == null) return;
			if (powerCoefficient <= 0)
			{
				Debug.LogWarning("[CurseEffect] powerCoefficient must be greater than 0!");
				return;
			}

			int calculatedPower = powerAmountSO.value / powerCoefficient;
			EnhanceCurse(calculatedPower);
		}

		/// <summary>
		/// Enhances friendly curse: if no friendly card with the specified cardTypeID exists in combinedDeckZone,
		/// spawns one of that type, then applies Power status effect to that friendly card.
		/// </summary>
		/// <param name="powerAmount">Amount of Power stacks to apply.</param>
		public void EnhanceFriendlyCurse(int powerAmount)
		{
			if (cardTypeID == null || string.IsNullOrEmpty(cardTypeID.value))
			{
				Debug.LogWarning("[CurseEffect] cardTypeID is not set!");
				return;
			}

			if (powerAmount <= 0)
			{
				return;
			}

			// Find friendly card with specified cardTypeID in combinedDeckZone
			CardScript targetCard = FindFriendlyCardWithTypeID(cardTypeID.value);

			// If not found, spawn one
			if (targetCard == null)
			{
				if (cardPrefab == null)
				{
					Debug.LogWarning($"[CurseEffect] Card prefab is not set! Cannot create card with typeID: {cardTypeID.value}");
					return;
				}
				targetCard = CreateFriendlyCard(cardPrefab);
			}

			// Apply Power status effect to target card
			if (targetCard != null)
			{
				ApplyPowerToCardWithProjectile(targetCard, powerAmount);
			}
		}

		/// <summary>
		/// Finds a friendly card with the specified cardTypeID in combinedDeckZone.
		/// </summary>
		private CardScript FindFriendlyCardWithTypeID(string typeID)
		{
			foreach (var card in combatManager.combinedDeckZone)
			{
				var cardScript = card.GetComponent<CardScript>();
				if (cardScript == null) continue;
				
				// Skip neutral cards
				if (CombatManager.ShouldSkipEffectProcessing(cardScript)) continue;
				
				// Check if it is a friendly card and cardTypeID matches
				if (cardScript.myStatusRef == myCardScript.myStatusRef && 
				    cardScript.cardTypeID == typeID)
				{
					return cardScript;
				}
			}

			// Check revealZone
			if (combatManager.revealZone != null)
			{
				var revealCardScript = combatManager.revealZone.GetComponent<CardScript>();
				if (revealCardScript != null &&
				    !CombatManager.ShouldSkipEffectProcessing(revealCardScript) &&
				    revealCardScript.myStatusRef == myCardScript.myStatusRef &&
				    revealCardScript.cardTypeID == typeID)
				{
					return revealCardScript;
				}
			}

			return null;
		}

		/// <summary>
		/// Finds an enemy card with the specified cardTypeID in combinedDeckZone.
		/// </summary>
		private CardScript FindEnemyCardWithTypeID(string typeID)
		{
			foreach (var card in combatManager.combinedDeckZone)
			{
				var cardScript = card.GetComponent<CardScript>();
				if (cardScript == null) continue;
				
				// Skip neutral cards
				if (CombatManager.ShouldSkipEffectProcessing(cardScript)) continue;
				
				// Check if it is an enemy card and cardTypeID matches
				if (cardScript.myStatusRef == myCardScript.theirStatusRef && 
				    cardScript.cardTypeID == typeID)
				{
					return cardScript;
				}
			}

			// Check revealZone
			if (combatManager.revealZone != null)
			{
				var revealCardScript = combatManager.revealZone.GetComponent<CardScript>();
				if (revealCardScript != null &&
				    !CombatManager.ShouldSkipEffectProcessing(revealCardScript) &&
				    revealCardScript.myStatusRef == myCardScript.theirStatusRef &&
				    revealCardScript.cardTypeID == typeID)
				{
					return revealCardScript;
				}
			}

			return null;
		}

		/// <summary>
		/// Spawns a card for the friendly side.
		/// </summary>
		private CardScript CreateFriendlyCard(GameObject cardToCreate)
		{
			CombatFuncs.me.AddCard_TargetSpecific(cardToCreate, myCardScript.myStatusRef);
			
			// Get the newly added card (at the first position of combinedDeckZone)
			if (combatManager.combinedDeckZone.Count > 0)
			{
				var newCard = combatManager.combinedDeckZone[0];
				var newCardScript = newCard.GetComponent<CardScript>();
				
				// Output effect info
				var thisCardOwnerString = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? 
					"<color=#87CEEB>Your</color> [" : "<color=orange>Enemy's</color> [";
				string thisCardColor = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? 
					"#87CEEB" : "orange";
				
				effectResultString.value +=
					"// " + thisCardOwnerString +
					"<color=" + thisCardColor + ">" + myCard.name + "</color>] cursed and created " +
					"<color=#87CEEB>Friendly</color> [<color=#87CEEB>" + newCard.name + "</color>]\n";
				
				return newCardScript;
			}
			return null;
		}

		/// <summary>
		/// Spawns a card for the enemy.
		/// </summary>
		private CardScript CreateEnemyCard(GameObject cardToCreate)
		{
			CombatFuncs.me.AddCard_TargetSpecific(cardToCreate, myCardScript.theirStatusRef);
			
			// Get the newly added card (at the first position of combinedDeckZone)
			if (combatManager.combinedDeckZone.Count > 0)
			{
				var newCard = combatManager.combinedDeckZone[0];
				var newCardScript = newCard.GetComponent<CardScript>();
				
				// Output effect info
				var thisCardOwnerString = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? 
					"<color=#87CEEB>Your</color> [" : "<color=orange>Enemy's</color> [";
				string thisCardColor = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? 
					"#87CEEB" : "orange";
				
				effectResultString.value +=
					"// " + thisCardOwnerString +
					"<color=" + thisCardColor + ">" + myCard.name + "</color>] cursed and created " +
					"<color=orange>Enemy's</color> [<color=orange>" + newCard.name + "</color>]\n";
				
				return newCardScript;
			}
			return null;
		}

		/// <summary>
		/// Applies Power status effect to the specified card using a projectile animation.
		/// The actual effect executes after the VFX reaches the target.
		/// </summary>
		public void ApplyPowerToCardWithProjectile(CardScript targetCard, int amount)
		{
			if (targetCard == null || amount <= 0) return;

			var targetCards = new List<CardScript> { targetCard };
			
			CombatUXManager.me?.PlayMultiStatusEffectProjectile(
				myCard,
				targetCards,
				(card) => ApplyPowerToCardInternal(card, amount),
				null
			);
		}

		/// <summary>
		/// Internal method: actually applies the Power effect (used as projectile animation callback).
		/// </summary>
		private void ApplyPowerToCardInternal(CardScript targetCard, int amount)
		{
			ApplyStatusEffectCore(
				targetCard, EnumStorage.StatusEffect.Power, amount,
				statusEffectResolverPrefab, statusEffectParticlePrefab, particleYOffset, amount);

			// Check if curse card gained Power, trigger event
			if (targetCard.cardTypeID == GameEventStorage.me?.curseCardTypeID?.value)
			{
				if (targetCard.myStatusRef == combatManager.enemyPlayerStatusRef)
				{
					GameEventStorage.me?.onEnemyCurseCardGotPower?.RaiseOwner();
				}
				else
				{
					GameEventStorage.me?.onEnemyCurseCardGotPower?.RaiseOpponent();
				}
			}
		}

		/// <summary>
		/// Consumes Power status effect from enemy cards matching cardTypeID.
		/// </summary>
		/// <param name="amount">Amount of Power stacks to consume.</param>
		public void ConsumeHostileCursePower(int amount)
		{
			if (cardTypeID == null || string.IsNullOrEmpty(cardTypeID.value))
			{
				Debug.LogWarning("[CurseEffect] cardTypeID is not set!");
				return;
			}

			if (amount <= 0) return;

			// Find all enemy cards matching cardTypeID
			var targetCards = FindAllEnemyCardsWithTypeID(cardTypeID.value);
			if (targetCards.Count == 0) return;

			// Calculate total Power stacks on these cards
			int totalPower = 0;
			foreach (var card in targetCards)
			{
				totalPower += EnumStorage.GetStatusEffectCount(card.myStatusEffects, EnumStorage.StatusEffect.Power);
			}

			// Check if there is enough Power to consume
			if (totalPower < amount) return;

			// Consume Power (remove layer by layer from each card)
			int amountToRemove = amount;
			foreach (var card in targetCards)
			{
				if (amountToRemove <= 0) break;

				int cardPowerCount = EnumStorage.GetStatusEffectCount(card.myStatusEffects, EnumStorage.StatusEffect.Power);
				int removeFromThisCard = Mathf.Min(cardPowerCount, amountToRemove);

				for (int i = card.myStatusEffects.Count - 1; i >= 0 && removeFromThisCard > 0; i--)
				{
					if (card.myStatusEffects[i] == EnumStorage.StatusEffect.Power)
					{
						card.myStatusEffects.RemoveAt(i);
						removeFromThisCard--;
						amountToRemove--;
					}
				}

				// Refresh visual display for this card
				TriggerTintForStatusEffect(card, EnumStorage.StatusEffect.Power);
			}

			// Output effect info
			var thisCardOwnerString = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? 
				"<color=#87CEEB>Your</color> [" : "<color=orange>Enemy's</color> [";
			string thisCardColor = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? 
				"#87CEEB" : "orange";

			effectResultString.value +=
				"// " + thisCardOwnerString +
				"<color=" + thisCardColor + ">" + myCard.name + "</color>] consumed " +
				"<color=yellow>" + amount + "</color> [Power] from cursed cards\n";

			// Refresh info display
			CombatInfoDisplayer.me?.RefreshDeckInfo();
		}

		/// <summary>
		/// Finds all enemy cards matching the specified cardTypeID.
		/// </summary>
		private List<CardScript> FindAllEnemyCardsWithTypeID(string typeID)
		{
			var result = new List<CardScript>();
			foreach (var card in combatManager.combinedDeckZone)
			{
				var cardScript = card.GetComponent<CardScript>();
				if (cardScript == null) continue;
				
				// Skip neutral cards
				if (CombatManager.ShouldSkipEffectProcessing(cardScript)) continue;
				
				// Check if it is an enemy card and cardTypeID matches
				if (cardScript.myStatusRef == myCardScript.theirStatusRef && 
				    cardScript.cardTypeID == typeID)
				{
					result.Add(cardScript);
				}
			}

			// Check revealZone
			if (combatManager.revealZone != null)
			{
				var revealCardScript = combatManager.revealZone.GetComponent<CardScript>();
				if (revealCardScript != null &&
				    !CombatManager.ShouldSkipEffectProcessing(revealCardScript) &&
				    revealCardScript.myStatusRef == myCardScript.theirStatusRef &&
				    revealCardScript.cardTypeID == typeID &&
				    !result.Exists(c => c.gameObject == combatManager.revealZone))
				{
					result.Add(revealCardScript);
				}
			}

			return result;
		}
	}
}
