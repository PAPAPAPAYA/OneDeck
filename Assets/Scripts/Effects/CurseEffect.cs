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

		[Header("Based on IntSO")]
		[Tooltip("IntSO used when this card belongs to the owner/player")]
		public IntSO ownerIntSO;
		[Tooltip("IntSO used when this card belongs to the enemy")]
		public IntSO enemyIntSO;

		/// <summary>
		/// Enhances curse: if no enemy card with the specified cardTypeID exists in combinedDeckZone,
		/// spawns one of that type, then grants permanent attack to that enemy card
		/// (attack-attribute redesign; formerly Power stacks).
		/// </summary>
		/// <param name="attackAmount">Amount of attack to grant.</param>
		public void EnhanceCurse(int attackAmount)
		{
			// Debug.Log("[CurseEffect] EnhanceCurse START attackAmount=" + attackAmount + " myCard=" + (myCard != null ? myCard.name : "null"));
			if (cardTypeID == null || string.IsNullOrEmpty(cardTypeID.value))
			{
				// Debug.LogWarning("[CurseEffect] cardTypeID is not set!");
				return;
			}

			if (attackAmount <= 0)
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

			// Grant attack to target card
			if (targetCard != null)
			{
				ApplyAttackToCardWithProjectile(targetCard, attackAmount, isNewlyCreated);
			}
			// Debug.Log("[CurseEffect] EnhanceCurse END myCard=" + (myCard != null ? myCard.name : "null"));
		}

		/// <summary>
		/// Enhances curse based on IntSO value.
		/// Uses ownerIntSO when this card belongs to the owner, otherwise enemyIntSO.
		/// </summary>
		public virtual void EnhanceCurse_BasedOnIntSO()
		{
			IntSO intSO = GetIntSOForOwner(ownerIntSO, enemyIntSO);
			if (intSO == null) return;
			EnhanceCurse(intSO.value);
		}

		/// <summary>
		/// Repeats the IntSO value as a COUNT: calls EnhanceCurse(1) once per point, each
		/// independently finding-or-creating an enemy curse (RELIC_TALLY
		/// "本回合每埋葬1生物，强化1敌方诅咒" — B burials mean B separate +1 enhancements,
		/// not one curse +B). Uses ownerIntSO/enemyIntSO like EnhanceCurse_BasedOnIntSO.
		/// </summary>
		public virtual void EnhanceCurseTimes_BasedOnIntSO()
		{
			IntSO intSO = GetIntSOForOwner(ownerIntSO, enemyIntSO);
			if (intSO == null) return;
			int times = intSO.value;
			for (int i = 0; i < times; i++)
			{
				EnhanceCurse(1);
			}
		}

		/// <summary>
		/// Enhances curse (with coefficient) based on ownerIntSO/enemyIntSO.
		/// Uses ownerIntSO when this card belongs to the owner, otherwise enemyIntSO.
		/// Calculates enhancement stacks from IntSO value and coefficient,
		/// enhancing curse by 1 for every powerCoefficient points.
		/// </summary>
		public virtual void EnhanceCurseWithCoefficient_BasedOnIntSO()
		{
			IntSO intSO = GetIntSOForOwner(ownerIntSO, enemyIntSO);
			if (intSO == null) return;
			if (powerCoefficient <= 0)
			{
				// Debug.LogWarning("[CurseEffect] powerCoefficient must be greater than 0!");
				return;
			}

			int calculatedPower = intSO.value / powerCoefficient;
			EnhanceCurse(calculatedPower);
		}

		/// <summary>
		/// Enhances friendly curse: if no friendly card with the specified cardTypeID exists in combinedDeckZone,
		/// spawns one of that type, then grants permanent attack to that friendly card.
		/// </summary>
		/// <param name="attackAmount">Amount of attack to grant.</param>
		public void EnhanceFriendlyCurse(int attackAmount)
		{
			if (cardTypeID == null || string.IsNullOrEmpty(cardTypeID.value))
			{
				// Debug.LogWarning("[CurseEffect] cardTypeID is not set!");
				return;
			}

			if (attackAmount <= 0)
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

			// Grant attack to target card
			if (targetCard != null)
			{
				ApplyAttackToCardWithProjectile(targetCard, attackAmount, isNewlyCreated);
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
			CombatFuncs.me.AddCard_TargetSpecific(cardToCreate, myCardScript.myStatusRef, myCardScript);
			
			// Get the newly added card (at the first position of combinedDeckZone)
			if (combatManager.combinedDeckZone.Count > 0)
			{
				var newCard = combatManager.combinedDeckZone[0];
				var newCardScript = newCard.GetComponent<CardScript>();
				
				// Output effect info
				var thisCardOwnerString = GetMyCardOwnerPrefix();
				string thisCardColor = GetMyCardOwnerColor();
				
				AppendLog(
					"// " + thisCardOwnerString +
					"<color=" + thisCardColor + ">" + myCard.name + "</color>]诅咒并创建了" +
					GameColorPalette.Me.friendly.OpenTag + "友方</color>[" + GameColorPalette.Me.friendly.OpenTag + newCard.name + "</color>]");
				
				return newCardScript;
			}
			return null;
		}

		/// <summary>
		/// Spawns a card for the enemy.
		/// </summary>
		private CardScript CreateEnemyCard(GameObject cardToCreate)
		{
			CombatFuncs.me.AddCard_TargetSpecific(cardToCreate, myCardScript.theirStatusRef, myCardScript);
			
			// Get the newly added card (at the first position of combinedDeckZone)
			if (combatManager.combinedDeckZone.Count > 0)
			{
				var newCard = combatManager.combinedDeckZone[0];
				var newCardScript = newCard.GetComponent<CardScript>();
				
				// Output effect info
				var thisCardOwnerString = GetMyCardOwnerPrefix();
				string thisCardColor = GetMyCardOwnerColor();
				
				AppendLog(
					"// " + thisCardOwnerString +
					"<color=" + thisCardColor + ">" + myCard.name + "</color>]诅咒并创建了" +
					GameColorPalette.Me.enemy.OpenTag + "敌方</color>[" + GameColorPalette.Me.enemy.OpenTag + newCard.name + "</color>]");
				
				return newCardScript;
			}
			return null;
		}

		/// <summary>
		/// Grants permanent attack to the specified card using a projectile animation
		/// (attack-attribute redesign; formerly Power stacks).
		/// The actual effect executes after the VFX reaches the target.
		/// </summary>
		public void ApplyAttackToCardWithProjectile(CardScript targetCard, int amount, bool isNewlyCreated = false)
		{
			if (targetCard == null || amount <= 0) return;

			// Execute logic immediately so AnimationRequest is captured in the current recorder
			ApplyAttackToCardInternal(targetCard, amount);

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
					targetCard = targetCard.gameObject,
					projectileCount = amount
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
		/// Internal method: actually grants the attack (used as projectile animation callback).
		/// </summary>
		private void ApplyAttackToCardInternal(CardScript targetCard, int amount)
		{
			// Debug.Log("[CurseEffect] ApplyAttackToCardInternal target=" + (targetCard != null ? targetCard.name : "null") + " amount=" + amount + " myCard=" + (myCard != null ? myCard.name : "null"));
			ApplyAttackCore(targetCard, amount, statusEffectParticlePrefab, particleYOffset);

			// Check if curse card gained attack, trigger event
			if (targetCard.cardTypeID == GameEventStorage.me?.curseCardTypeID?.value)
			{
				if (targetCard.myStatusRef == combatManager.enemyPlayerStatusRef)
				{
					GameEventStorage.me?.onEnemyCurseCardGainedAttack?.RaiseOwner();
				}
				else
				{
					GameEventStorage.me?.onEnemyCurseCardGainedAttack?.RaiseOpponent();
				}
			}
		}

		// VISUAL-FIX(2026-06-13): ConsumeHostileCursePower has no PopUp/SlotIn/Projectile animation
		//   Cause:    ConsumeHostileCursePower only captured StatusEffectChange per target, so players
		//             could barely see Power being absorbed from enemy curse cards.
		//   Affects:  CurseEffect, EffectScript, RecorderAnimationPlayer, CombatUXManager
		//   Regress:  Reveal CURSE_SUMMONER or PREMATURE when enemy curse cards carry Power
		//             Check: target curse cards pop up together, projectiles fly from each target
		//             toward statusEffectConsumePos with one projectile per consumed layer, status
		//             text updates after projectiles land, then all targets slot back in together.
		/// <summary>
		/// Consumes permanent attack from enemy cards matching cardTypeID
		/// (attack-attribute redesign; formerly Power stacks).
		/// </summary>
		/// <param name="amount">Amount of attack to consume.</param>
		public void ConsumeEnemyCurseAttack(int amount)
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

			// Calculate total attack on these cards
			int totalAttack = 0;
			foreach (var card in targetCards)
			{
				totalAttack += card.GetAttack();
			}

			// Check if there is enough attack to consume
			if (totalAttack < amount) return;

			// Snapshot display state for all affected targets before mutating so card face updates
			// are deferred until the projectile animation completes.
			var recorderGo = EffectChainManager.Me != null ? EffectChainManager.Me.currentEffectRecorder : null;
			var recorder = recorderGo != null ? recorderGo.GetComponent<EffectRecorder>() : null;
			if (recorder != null && RecorderAnimationPlayer.me != null)
			{
				foreach (var card in targetCards)
				{
					card.SnapshotDisplayState();
				}
			}

			// Consume attack (one point at a time in round-robin across targets) and record
			// how much was removed from each target so the animation can spawn the correct number
			// of projectiles.
			int amountToRemove = amount;
			var affectedTargets = new List<CardScript>();
			var removedAmounts = new List<int>();
			while (amountToRemove > 0)
			{
				bool removedAny = false;
				foreach (var card in targetCards)
				{
					if (amountToRemove <= 0) break;

					if (card.GetAttack() <= 0) continue;

					// Remove one attack point from this card
					card.ModifyAttack(-1);
					amountToRemove--;
					removedAny = true;

					int existingIndex = affectedTargets.IndexOf(card);
					if (existingIndex >= 0)
					{
						removedAmounts[existingIndex]++;
					}
					else
					{
						affectedTargets.Add(card);
						removedAmounts.Add(1);
					}
				}

				if (!removedAny) break;
			}

			// Capture batched consume animation: PopUp -> Projectile -> SlotIn
			if (affectedTargets.Count > 0)
			{
				Vector3 consumePos = CombatUXManager.me != null && CombatUXManager.me.statusEffectConsumePos != null
					? CombatUXManager.me.statusEffectConsumePos.position
					: myCardScript.transform.position;

				CaptureBatchStatusEffectConsumeAnimation(myCard, affectedTargets, EnumStorage.StatusEffect.Power, removedAmounts, consumePos);
			}

			// Output effect info
			var thisCardOwnerString = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ?
				GameColorPalette.Me.friendly.OpenTag + "Your</color> [" : GameColorPalette.Me.enemy.OpenTag + "Enemy's</color> [";
			string thisCardColor = GetMyCardOwnerColor();

			AppendLog(
				"// " + thisCardOwnerString +
				"<color=" + thisCardColor + ">" + myCard.name + "</color>]从被诅咒的卡牌中吸收了" +
				GameColorPalette.Me.highlight.OpenTag + amount + "</color>点攻击力");

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
