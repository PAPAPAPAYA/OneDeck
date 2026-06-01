using System.Collections.Generic;
using DefaultNamespace;
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
			// Debug.Log("[CurseEffect] EnhanceCurse START powerAmount=" + powerAmount + " myCard=" + (myCard != null ? myCard.name : "null"));
			if (cardTypeID == null || string.IsNullOrEmpty(cardTypeID.value))
			{
				// Debug.LogWarning("[CurseEffect] cardTypeID is not set!");
				return;
			}

			if (powerAmount <= 0)
			{
				return;
			}

			// Find enemy card with specified cardTypeID in combinedDeckZone
			CardScript targetCard = FindEnemyCardWithTypeID(cardTypeID.value);

			// If not found, spawn one
			bool isNewlyCreated = false;
			if (targetCard == null)
			{
				if (cardPrefab == null)
				{
					// Debug.LogWarning($"[CurseEffect] Card prefab is not set! Cannot create card with typeID: {cardTypeID.value}");
					return;
				}
				targetCard = CreateEnemyCard(cardPrefab);
				isNewlyCreated = true;
			}

			// Apply Power status effect to target card
			if (targetCard != null)
			{
				ApplyPowerToCardWithProjectile(targetCard, powerAmount, isNewlyCreated);
			}
			// Debug.Log("[CurseEffect] EnhanceCurse END myCard=" + (myCard != null ? myCard.name : "null"));
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
				// Debug.LogWarning("[CurseEffect] powerCoefficient must be greater than 0!");
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
				// Debug.LogWarning("[CurseEffect] cardTypeID is not set!");
				return;
			}

			if (powerAmount <= 0)
			{
				return;
			}

			// Find friendly card with specified cardTypeID in combinedDeckZone
			CardScript targetCard = FindFriendlyCardWithTypeID(cardTypeID.value);

			// If not found, spawn one
			bool isNewlyCreated = false;
			if (targetCard == null)
			{
				if (cardPrefab == null)
				{
					// Debug.LogWarning($"[CurseEffect] Card prefab is not set! Cannot create card with typeID: {cardTypeID.value}");
					return;
				}
				targetCard = CreateFriendlyCard(cardPrefab);
				isNewlyCreated = true;
			}

			// Apply Power status effect to target card
			if (targetCard != null)
			{
				ApplyPowerToCardWithProjectile(targetCard, powerAmount, isNewlyCreated);
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
					"<color=#87CEEB>你的</color>[" : "<color=orange>敌方的</color>[";
				string thisCardColor = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? 
					"#87CEEB" : "orange";
				
				AppendLog(
					"// " + thisCardOwnerString +
					"<color=" + thisCardColor + ">" + myCard.name + "</color>]诅咒并创建了" +
					"<color=#87CEEB>友方</color>[<color=#87CEEB>" + newCard.name + "</color>]");
				
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
					"<color=#87CEEB>你的</color>[" : "<color=orange>敌方的</color>[";
				string thisCardColor = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? 
					"#87CEEB" : "orange";
				
				AppendLog(
					"// " + thisCardOwnerString +
					"<color=" + thisCardColor + ">" + myCard.name + "</color>]诅咒并创建了" +
					"<color=orange>敌方</color>[<color=orange>" + newCard.name + "</color>]");
				
				return newCardScript;
			}
			return null;
		}

		/// <summary>
		/// Applies Power status effect to the specified card using a projectile animation.
		/// The actual effect executes after the VFX reaches the target.
		/// </summary>
		public void ApplyPowerToCardWithProjectile(CardScript targetCard, int amount, bool isNewlyCreated = false)
		{
			if (targetCard == null || amount <= 0) return;

			// Execute logic immediately so AnimationRequest is captured in the current recorder
			ApplyPowerToCardInternal(targetCard, amount);

			// Capture projectile animation into AnimationRequest
			var recorderGo = EffectChainManager.Me != null ? EffectChainManager.Me.currentEffectRecorder : null;
			var recorder = recorderGo != null ? recorderGo.GetComponent<EffectRecorder>() : null;
			if (recorder != null)
			{
				// VISUAL-FIX(2026-05-24): Newly created curse card's projectile flies off-screen
				//   Cause:    PopUpCard computes peak from current physical position (newCardPosition,
				//             which is off-screen), so the projectile endPos is also off-screen.
				//   Affects:  CurseEffect, PopUpCard, StatusEffectProjectile, MoveToPopUpPosition
				//   Regress:  Reveal a card that enhances a curse type not present in deck (e.g. JU_ON)
				//             and verify the projectile flies to the visible deck peak, not off-screen.
				//   Related:  Any curse card with EnhanceCurse/EnhanceFriendlyCurse when target absent
				if (isNewlyCreated)
				{
					// New card: fly from newCardPosition to deck peak (like AddTempCard)
					int deckIndex = CombatManager.Me != null ? CombatManager.Me.combinedDeckZone.IndexOf(targetCard.gameObject) : -1;
					if (deckIndex < 0) deckIndex = 0;

					recorder.animationRequests.Add(new AnimationRequest
					{
						type = AnimationRequestType.MoveToPopUpPosition,
						targetCard = targetCard.gameObject,
						targetIndex = deckIndex
					});
				}
				else
				{
					// Existing card: Pop Up from current deck position
					recorder.animationRequests.Add(new AnimationRequest
					{
						type = AnimationRequestType.PopUp,
						targetCard = targetCard.gameObject
					});
				}

				// 2. Play projectile while card is at peak
				recorder.animationRequests.Add(new AnimationRequest
				{
					type = AnimationRequestType.StatusEffectProjectile,
					attackerCard = myCard,
					targetCard = targetCard.gameObject
				});

				// 3. Slot In after projectile completes
				recorder.animationRequests.Add(new AnimationRequest
				{
					type = AnimationRequestType.SlotIn,
					targetCard = targetCard.gameObject
				});
			}
		}

		/// <summary>
		/// Internal method: actually applies the Power effect (used as projectile animation callback).
		/// </summary>
		private void ApplyPowerToCardInternal(CardScript targetCard, int amount)
		{
			// Debug.Log("[CurseEffect] ApplyPowerToCardInternal target=" + (targetCard != null ? targetCard.name : "null") + " amount=" + amount + " myCard=" + (myCard != null ? myCard.name : "null"));
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
				// Debug.LogWarning("[CurseEffect] cardTypeID is not set!");
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
				int removedFromThisCard = 0;

				// Snapshot display state before mutating so card text updates are deferred until animation completes
				var recorderGo = EffectChainManager.Me != null ? EffectChainManager.Me.currentEffectRecorder : null;
				var recorder = recorderGo != null ? recorderGo.GetComponent<EffectRecorder>() : null;
				if (recorder != null && RecorderAnimationPlayer.me != null)
				{
					card.SnapshotDisplayState();
				}

				for (int i = card.myStatusEffects.Count - 1; i >= 0 && removeFromThisCard > 0; i--)
				{
					if (card.myStatusEffects[i] == EnumStorage.StatusEffect.Power)
					{
						card.myStatusEffects.RemoveAt(i);
						removeFromThisCard--;
						amountToRemove--;
						removedFromThisCard++;
					}
				}

				CaptureStatusEffectChangeAnimationRequest(card.gameObject, EnumStorage.StatusEffect.Power, -removedFromThisCard);
			}

			// Output effect info
			var thisCardOwnerString = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? 
				"<color=#87CEEB>Your</color> [" : "<color=orange>Enemy's</color> [";
			string thisCardColor = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? 
				"#87CEEB" : "orange";

			AppendLog(
				"// " + thisCardOwnerString +
				"<color=" + thisCardColor + ">" + myCard.name + "</color>]从被诅咒的卡牌中吸收了" +
				"<color=yellow>" + amount + "</color>层[力量]");

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
