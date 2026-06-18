using System.Collections.Generic;
using UnityEngine;

namespace DefaultNamespace.Effects
{
	public class StatusEffectGiverEffect : EffectScript
	{
		[Header("Status Effect Related Refs")]
		public GameObject myStatusEffectResolverScript;
		public bool canStatusEffectBeStacked = true;
		[Tooltip("if this is none, then won't run give status effect")]
		public EnumStorage.StatusEffect statusEffectToGive;
		[Tooltip("this is used for GiveStatusEffectBasedOnStatusEffectCount()")]
		public EnumStorage.StatusEffect statusEffectToCount;
		public bool spreadEvenly = false;
		[Tooltip("only applies to GiveStatusEffect(): whose cards the status effect will be given to")]
		public EnumStorage.TargetType target;
		[Tooltip("if true, will include the card itself in reveal zone when giving status effect")]
		public bool includeSelf = false;

		[Header("Apply to Last X Cards")]
		[Tooltip("Apply status effects to the X cards after the current card (index decreasing direction)")]
		public int lastXCardsCount = 0;
		[Tooltip("Number of status effect layers to apply to each card")]
		public int statusEffectLayerCount = 1;

		[Header("Apply to X Friendly Cards")]
		[Tooltip("Number of randomly selected friendly cards")]
		public int xFriendlyCount = 0;
		[Tooltip("Number of status effect layers to apply to each friendly card")]
		public int yFriendlyLayerCount = 1;

		[Header("Based on IntSO")]
		[Tooltip("IntSO used when this card belongs to the owner/player")]
		public IntSO ownerIntSO;
		[Tooltip("IntSO used when this card belongs to the enemy")]
		public IntSO enemyIntSO;

		[Header("Particle System")]
		[Tooltip("Particle system prefab to play when receiving status effect")]
		public ParticleSystem statusEffectParticlePrefab;
		[Tooltip("Y-axis offset for the particle system")]
		public float particleYOffset = 0f;

		#region Helper Methods - Card Filtering
		protected bool ShouldSkipCard(CardScript cardScript)
		{
			return CombatManager.ShouldSkipEffectProcessing(cardScript);
		}

		protected bool MatchesTargetFilter(CardScript cardScript, EnumStorage.TargetType targetFilter)
		{
			if (targetFilter == EnumStorage.TargetType.Me && cardScript.myStatusRef != myCardScript.myStatusRef)
				return false;
			if (targetFilter == EnumStorage.TargetType.Them && cardScript.myStatusRef == myCardScript.myStatusRef)
				return false;
			return true;
		}

		protected bool CanReceiveStatusEffect(CardScript cardScript, EnumStorage.StatusEffect effect)
		{
			if (effect == EnumStorage.StatusEffect.None) return false;
			if (!canStatusEffectBeStacked && cardScript.myStatusEffects.Contains(effect))
				return false;
			return true;
		}
		#endregion

		#region Helper Methods - Common Operations
		protected void ApplyStatusEffectToCard(CardScript targetCardScript, int amount)
		{
			ApplyStatusEffectCore(targetCardScript, statusEffectToGive, amount,
				myStatusEffectResolverScript, statusEffectParticlePrefab, particleYOffset,
				canStatusEffectBeStacked ? amount : 1);
		}

		protected List<CardScript> CollectFriendlyCards(bool filterCanReceive = false)
		{
			var result = new List<CardScript>();
			var combinedDeck = combatManager.combinedDeckZone;
			foreach (var card in combinedDeck)
			{
				var cardScript = card.GetComponent<CardScript>();
				if (ShouldSkipCard(cardScript)) continue;
				if (cardScript.myStatusRef != myCardScript.myStatusRef) continue;
				if (filterCanReceive && !CanReceiveStatusEffect(cardScript, statusEffectToGive)) continue;
				result.Add(cardScript);
			}
			if (combatManager.revealZone != null)
			{
				var revealCardScript = combatManager.revealZone.GetComponent<CardScript>();
				if (!ShouldSkipCard(revealCardScript) &&
				    revealCardScript.myStatusRef == myCardScript.myStatusRef)
				{
					if (!filterCanReceive || CanReceiveStatusEffect(revealCardScript, statusEffectToGive))
					{
						bool alreadyExists = result.Exists(c => c.gameObject == combatManager.revealZone);
						if (!alreadyExists)
							result.Add(revealCardScript);
					}
				}
			}
			return result;
		}

		protected void CaptureBatchStatusEffectAnimation(List<CardScript> targetCards, int projectileCount = 1)
		{
			var recorderGo = EffectChainManager.Me != null ? EffectChainManager.Me.currentEffectRecorder : null;
			var recorder = recorderGo != null ? recorderGo.GetComponent<EffectRecorder>() : null;
			if (recorder == null || targetCards.Count <= 0) return;

			var targetGameObjects = new List<GameObject>();
			foreach (var t in targetCards)
			{
				if (t != null) targetGameObjects.Add(t.gameObject);
			}

			recorder.animationRequests.Add(new AnimationRequest
			{
				type = AnimationRequestType.PopUpBatch,
				targetCards = targetGameObjects
			});

			recorder.animationRequests.Add(new AnimationRequest
			{
				type = AnimationRequestType.StatusEffectProjectile,
				attackerCard = myCard,
				targetCards = targetGameObjects,
				projectileCount = projectileCount
			});

			recorder.animationRequests.Add(new AnimationRequest
			{
				type = AnimationRequestType.SlotInBatch,
				targetCards = targetGameObjects
			});
		}
		#endregion

		#region Public Effect Methods
		// VISUAL-FIX(2026-06-10): GiveSelfStatusEffect has no projectile animation
		//   Cause:    GiveSelfStatusEffect only called ApplyStatusEffectCore, which captures
		//             StatusEffectChange but not StatusEffectProjectile
		//   Affects:  StatusEffectGiverEffect, RecorderAnimationPlayer, CombatUXManager
		//   Regress:  Reveal a card whose effect calls GiveSelfStatusEffect (e.g. self-Power)
		//             Check: card pops up, projectile flies in, then slots back in
		public virtual void GiveSelfStatusEffect(int amount)
		{
			ApplyStatusEffectToCard(myCardScript, amount);
			CaptureBatchStatusEffectAnimation(new List<CardScript> { myCardScript }, amount);
		}

		public virtual void GiveStatusEffect(int amount)
		{
			if (statusEffectToGive == EnumStorage.StatusEffect.None) return;

			// --- 1. Target selection (unchanged) ---
			var cardsToGiveTag = new List<GameObject>();
			UtilityFuncManagerScript.CopyGameObjectList(combatManager.combinedDeckZone, cardsToGiveTag, true);
			if (includeSelf) cardsToGiveTag.Add(myCard);
			if (combatManager.revealZone != null && !cardsToGiveTag.Contains(combatManager.revealZone))
			{
				if (combatManager.revealZone != myCard || includeSelf)
					cardsToGiveTag.Add(combatManager.revealZone);
			}
			cardsToGiveTag = UtilityFuncManagerScript.ShuffleList(cardsToGiveTag);
			for (var i = cardsToGiveTag.Count - 1; i >= 0; i--)
			{
				var targetCardScript = cardsToGiveTag[i].GetComponent<CardScript>();
				if (ShouldSkipCard(targetCardScript) || !MatchesTargetFilter(targetCardScript, target))
					cardsToGiveTag.RemoveAt(i);
			}
			if (!canStatusEffectBeStacked)
			{
				for (var i = cardsToGiveTag.Count - 1; i >= 0; i--)
				{
					if (cardsToGiveTag[i].GetComponent<CardScript>().myStatusEffects.Contains(statusEffectToGive))
						cardsToGiveTag.RemoveAt(i);
				}
			}
			if (cardsToGiveTag.Count <= 0) return;
			if (spreadEvenly) amount = Mathf.Clamp(amount, 0, cardsToGiveTag.Count);

			// --- 2. Synchronous logic execution ---
			var targetCards = new List<CardScript>();
			for (var i = 0; i < amount; i++)
			{
				CardScript targetCardScript = spreadEvenly
					? cardsToGiveTag[i].GetComponent<CardScript>()
					: cardsToGiveTag[Random.Range(0, cardsToGiveTag.Count)].GetComponent<CardScript>();
				targetCards.Add(targetCardScript);
			}

			foreach (var t in targetCards)
			{
				ApplyStatusEffectCore(t, statusEffectToGive, 1,
					myStatusEffectResolverScript, statusEffectParticlePrefab, particleYOffset, 1, 1);
			}

			// --- 3. Refresh display (synchronous) ---
			CombatInfoDisplayer.me?.RefreshDeckInfo();

			// --- 4. Capture batch projectile animation ---
			CaptureBatchStatusEffectAnimation(targetCards, 1);
		}

		public void GiveStatusEffectBasedOnStatusEffectCount()
		{
			int count = 0;
			foreach (var effect in myCardScript.myStatusEffects)
			{
				if (effect == statusEffectToCount) count++;
			}
			if (count <= 0 || statusEffectToGive == EnumStorage.StatusEffect.None) return;
			GiveStatusEffect(count);
		}

		public void GiveSelfStatusEffectBasedOnStatusEffectCount()
		{
			int count = 0;
			foreach (var effect in myCardScript.myStatusEffects)
			{
				if (effect == statusEffectToCount) count++;
			}
			if (count <= 0 || statusEffectToGive == EnumStorage.StatusEffect.None) return;
			GiveSelfStatusEffect(count);
		}

		public virtual void GiveAllFriendlyStatusEffect(int amount)
		{
			if (statusEffectToGive == EnumStorage.StatusEffect.None) return;
			if (amount <= 0) return;

			var targetCards = CollectFriendlyCards(filterCanReceive: true);
			if (targetCards.Count <= 0) return;

			foreach (var card in targetCards)
			{
				ApplyStatusEffectToCard(card, amount);
			}

			CombatInfoDisplayer.me?.RefreshDeckInfo();
			CaptureBatchStatusEffectAnimation(targetCards, amount);
		}

		/// <summary>
		/// Gives status effects to the last X cards in the combined deck (cards before this card in deck order).
		/// Iterates backwards from the current card's position and applies statusEffectLayerCount layers of statusEffectToGive to each valid target.
		/// Skips cards that should be ignored or cannot receive the status effect.
		/// If this card is in the reveal zone, starts from the bottom of the deck instead.
		/// </summary>
		public virtual void GiveStatusEffectToLastXCards()
		{
			if (statusEffectToGive == EnumStorage.StatusEffect.None) return;
			if (lastXCardsCount <= 0 || statusEffectLayerCount <= 0) return;
			var combinedDeck = combatManager.combinedDeckZone;
			int startIndex;
			if (combatManager.revealZone != null && combatManager.revealZone == myCard)
			{
				startIndex = combinedDeck.Count - 1;
			}
			else
			{
				int currentIndex = -1;
				for (int i = 0; i < combinedDeck.Count; i++)
				{
					if (combinedDeck[i] == myCard)
					{
						currentIndex = i;
						break;
					}
				}
				if (currentIndex < 0) return;
				startIndex = currentIndex - 1;
			}
			var targetCards = new List<CardScript>();
			int cardsGiven = 0;
			for (int i = startIndex; i >= 0 && cardsGiven < lastXCardsCount; i--)
			{
				var targetCard = combinedDeck[i];
				var targetCardScript = targetCard.GetComponent<CardScript>();
				if (ShouldSkipCard(targetCardScript)) continue;
				if (!CanReceiveStatusEffect(targetCardScript, statusEffectToGive)) continue;
				targetCards.Add(targetCardScript);
				cardsGiven++;
			}
			if (targetCards.Count <= 0) return;

			foreach (var t in targetCards)
			{
				ApplyStatusEffectToCard(t, statusEffectLayerCount);
			}

			CombatInfoDisplayer.me?.RefreshDeckInfo();
			CaptureBatchStatusEffectAnimation(targetCards, statusEffectLayerCount);
		}

		public virtual void GiveStatusEffectToXFriendly()
		{
			if (statusEffectToGive == EnumStorage.StatusEffect.None) return;
			if (xFriendlyCount <= 0 || yFriendlyLayerCount <= 0) return;

			var friendlyCards = CollectFriendlyCards(filterCanReceive: true);
			if (friendlyCards.Count <= 0) return;

			friendlyCards = UtilityFuncManagerScript.ShuffleList(friendlyCards);
			var targetCards = new List<CardScript>();
			int actualCount = Mathf.Min(xFriendlyCount, friendlyCards.Count);
			for (int i = 0; i < actualCount; i++) targetCards.Add(friendlyCards[i]);
			if (targetCards.Count <= 0) return;

			foreach (var t in targetCards)
			{
				ApplyStatusEffectToCard(t, yFriendlyLayerCount);
			}

			CombatInfoDisplayer.me?.RefreshDeckInfo();
			CaptureBatchStatusEffectAnimation(targetCards, yFriendlyLayerCount);
		}

		/// <summary>
		/// Based on ownerIntSO/enemyIntSO, apply status effects to the same number of random friendly cards,
		/// each card receives 1 layer. Uses ownerIntSO when this card belongs to the owner, otherwise enemyIntSO.
		/// </summary>
		public virtual void GiveStatusEffectToXFriendly_BasedOnIntSO()
		{
			IntSO intSO = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef
				? ownerIntSO
				: enemyIntSO;

			if (intSO == null) return;

			int originalXFriendlyCount = xFriendlyCount;
			int originalYFriendlyLayerCount = yFriendlyLayerCount;
			xFriendlyCount = intSO.value;
			yFriendlyLayerCount = 1;
			GiveStatusEffectToXFriendly();
			xFriendlyCount = originalXFriendlyCount;
			yFriendlyLayerCount = originalYFriendlyLayerCount;
		}

		/// <summary>
		/// Gives status effects to X random friendly cards based on ValueTrackerManager staged values.
		/// If this card belongs to the owner, X is stagedOwnerRef value; otherwise X is stagedEnemyRef value.
		/// Each selected friendly card receives the specified number of layers.
		/// </summary>
		/// <param name="layerCount">Number of status effect layers to apply to each friendly card</param>
		public virtual void GiveStatusEffectToXFriendly_BasedOnStaged(int layerCount)
		{
			if (statusEffectToGive == EnumStorage.StatusEffect.None) return;
			if (layerCount <= 0) return;
			if (ValueTrackerManager.me == null) return;

			int xCount = 0;
			if (myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef)
			{
				if (ValueTrackerManager.me.stagedOwnerRef != null)
					xCount = ValueTrackerManager.me.stagedOwnerRef.value;
			}
			else
			{
				if (ValueTrackerManager.me.stagedEnemyRef != null)
					xCount = ValueTrackerManager.me.stagedEnemyRef.value;
			}

			if (xCount <= 0) return;

			int originalXFriendlyCount = xFriendlyCount;
			int originalYFriendlyLayerCount = yFriendlyLayerCount;
			xFriendlyCount = xCount;
			yFriendlyLayerCount = layerCount;
			GiveStatusEffectToXFriendly();
			xFriendlyCount = originalXFriendlyCount;
			yFriendlyLayerCount = originalYFriendlyLayerCount;
		}
		#endregion
	}
}
