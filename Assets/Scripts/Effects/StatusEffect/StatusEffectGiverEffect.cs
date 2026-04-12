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

		[Header("Particle System")]
		[Tooltip("Particle system prefab to play when receiving status effect")]
		public ParticleSystem statusEffectParticlePrefab;
		[Tooltip("Y-axis offset for the particle system")]
		public float particleYOffset = 0f;

		#region Helper Methods - Card Ownership & Colors
		protected string GetCardOwnerPrefix(PlayerStatusSO statusRef)
		{
			return statusRef == combatManager.ownerPlayerStatusRef ? "<color=#87CEEB>Your</color> [" : "<color=orange>Enemy's</color> [";
		}

		protected string GetCardOwnerColor(PlayerStatusSO statusRef)
		{
			return statusRef == combatManager.ownerPlayerStatusRef ? "#87CEEB" : "orange";
		}

		protected string GetMyCardOwnerPrefix()
		{
			return GetCardOwnerPrefix(myCardScript.myStatusRef);
		}

		protected string GetMyCardOwnerColor()
		{
			return GetCardOwnerColor(myCardScript.myStatusRef);
		}
		#endregion

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

		#region Helper Methods - Status Effect Application Core
		protected void ApplyStatusEffectCore(CardScript targetCardScript, EnumStorage.StatusEffect effect, int amount, int? logAmount = null)
		{
			if (effect == EnumStorage.StatusEffect.None) return;
			int actualLogAmount = logAmount ?? amount;

			// Raise Power-related events when a card gains Power
			if (effect == EnumStorage.StatusEffect.Power)
			{
				combatManager.lastCardGotPower = targetCardScript;
				GameEventStorage.me?.onAnyCardGotPower?.Raise();
				GameEventStorage.me?.onMeGotPower?.RaiseSpecific(targetCardScript.gameObject);
				if (targetCardScript.myStatusRef == combatManager.ownerPlayerStatusRef)
				{
					GameEventStorage.me?.onFriendlyCardGotPower?.RaiseOwner();
					GameEventStorage.me?.onEnemyCardGotPower?.RaiseOpponent();
				}
				else
				{
					GameEventStorage.me?.onFriendlyCardGotPower?.RaiseOpponent();
					GameEventStorage.me?.onEnemyCardGotPower?.RaiseOwner();
				}
			}

			for (int i = 0; i < amount; i++)
			{
				targetCardScript.myStatusEffects.Add(effect);
			}
			CreateStatusEffectResolvers(targetCardScript, amount);
			PlayStatusEffectParticles(targetCardScript.transform, amount);
			TriggerTintForStatusEffect(targetCardScript, effect);
			LogStatusEffectGiven(targetCardScript, effect, actualLogAmount);
		}

		protected void CreateStatusEffectResolvers(CardScript targetCardScript, int amount)
		{
			if (myStatusEffectResolverScript == null) return;
			int resolverCount = canStatusEffectBeStacked ? amount : 1;
			for (int i = 0; i < resolverCount; i++)
			{
				var tagResolver = Instantiate(myStatusEffectResolverScript, targetCardScript.transform);
				GameEventStorage.me.onThisTagResolverAttached.RaiseSpecific(tagResolver);
			}
		}

		protected void PlayStatusEffectParticles(Transform cardTransform, int count)
		{
			for (int i = 0; i < count; i++)
			{
				PlayStatusEffectParticle(cardTransform);
			}
		}

		protected void LogStatusEffectGiven(CardScript targetCardScript, EnumStorage.StatusEffect effect, int amount)
		{
			string targetCardOwnerString = GetCardOwnerPrefix(targetCardScript.myStatusRef);
			string targetCardColor = GetCardOwnerColor(targetCardScript.myStatusRef);
			string thisCardOwnerString = GetMyCardOwnerPrefix();
			string thisCardColor = GetMyCardOwnerColor();
			effectResultString.value +=
				"// " + thisCardOwnerString +
				"<color=" + thisCardColor + ">" + myCard.name + "</color>] gave " +
				targetCardOwnerString +
				"<color=" + targetCardColor + ">" + targetCardScript.gameObject.name + "</color>] " +
				"<color=yellow>" + amount + "</color> [" + effect + "]\n";
		}

		protected void LogSelfStatusEffect(EnumStorage.StatusEffect effect, int amount)
		{
			string thisCardOwnerString = CombatInfoDisplayer.me.ReturnCardOwnerInfo(myCardScript.myStatusRef);
			string thisCardColor = GetMyCardOwnerColor();
			effectResultString.value +=
				"// " + thisCardOwnerString +
				" [<color=" + thisCardColor + ">" + myCard.name + "</color>] gave" +
				" it" +
				" <color=yellow>" + amount + "</color> [" + effect + "]\n";
		}
		#endregion

		#region Public Effect Methods
		public virtual void GiveSelfStatusEffect(int amount)
		{
			for (int i = 0; i < amount; i++)
			{
				myCardScript.myStatusEffects.Add(statusEffectToGive);
				LogSelfStatusEffect(statusEffectToGive, 1);
				if (myStatusEffectResolverScript == null) continue;
				var tagResolver = Instantiate(myStatusEffectResolverScript, myCard.transform);
				GameEventStorage.me.onThisTagResolverAttached.RaiseSpecific(tagResolver);
				PlayStatusEffectParticle(myCard.transform);
				TriggerTintForStatusEffect(myCardScript, statusEffectToGive);
			}
		}

		public virtual void GiveStatusEffect(int amount)
		{
			if (statusEffectToGive == EnumStorage.StatusEffect.None) return;
			var cardsToGiveTag = new List<GameObject>();
			UtilityFuncManagerScript.CopyGameObjectList(combatManager.combinedDeckZone, cardsToGiveTag, true);
			if (includeSelf) cardsToGiveTag.Add(myCard);
			cardsToGiveTag = UtilityFuncManagerScript.ShuffleList(cardsToGiveTag);
			for (var i = cardsToGiveTag.Count - 1; i >= 0; i--)
			{
				var targetCardScript = cardsToGiveTag[i].GetComponent<CardScript>();
				if (ShouldSkipCard(targetCardScript))
				{
					cardsToGiveTag.RemoveAt(i);
					continue;
				}
				if (!MatchesTargetFilter(targetCardScript, target))
				{
					cardsToGiveTag.RemoveAt(i);
				}
			}
			if (!canStatusEffectBeStacked)
			{
				for (var i = cardsToGiveTag.Count - 1; i >= 0; i--)
				{
					if (cardsToGiveTag[i].GetComponent<CardScript>().myStatusEffects.Contains(statusEffectToGive))
					{
						cardsToGiveTag.RemoveAt(i);
					}
				}
			}
			if (cardsToGiveTag.Count <= 0) return;
			if (spreadEvenly) amount = Mathf.Clamp(amount, 0, cardsToGiveTag.Count);
			var targetCards = new List<CardScript>();
			for (var i = 0; i < amount; i++)
			{
				CardScript targetCardScript;
				if (spreadEvenly) targetCardScript = cardsToGiveTag[i].GetComponent<CardScript>();
				else targetCardScript = cardsToGiveTag[Random.Range(0, cardsToGiveTag.Count)].GetComponent<CardScript>();
				targetCards.Add(targetCardScript);
			}
			CombatUXManager.me?.PlayMultiStatusEffectProjectile(
				myCard,
				targetCards,
				ApplyStatusEffectToSingleTarget,
				() => CombatInfoDisplayer.me?.RefreshDeckInfo()
			);
		}

		private void ApplyStatusEffectToSingleTarget(CardScript targetCardScript)
		{
			ApplyStatusEffectCore(targetCardScript, statusEffectToGive, 1, 1);
		}

		private void ApplyStatusEffectsToTargets(List<CardScript> targetCards)
		{
			foreach (var targetCardScript in targetCards)
			{
				ApplyStatusEffectCore(targetCardScript, statusEffectToGive, 1, 1);
			}
			CombatInfoDisplayer.me.RefreshDeckInfo();
		}

		protected virtual void PlayStatusEffectParticle(Transform cardTransform)
		{
			if (statusEffectParticlePrefab == null) return;
			Vector3 spawnPosition = GetPhysicalCardWorldPosition(cardTransform) + Vector3.up * particleYOffset;
			ParticleSystem particleInstance = Instantiate(statusEffectParticlePrefab, spawnPosition, Quaternion.identity, cardTransform);
			particleInstance.Play();
		}

		protected virtual Vector3 GetPhysicalCardWorldPosition(Transform cardTransform)
		{
			if (CombatUXManager.me != null)
			{
				var cardScript = cardTransform.GetComponent<CardScript>();
				if (cardScript != null)
				{
					CombatUXManager.me.BuildCardScriptToPhysicalDictionary();
					var physicalCard = CombatUXManager.me.GetPhysicalCardFromLogicalCard(cardScript);
					if (physicalCard != null) return physicalCard.transform.position;
				}
			}
			return cardTransform.position;
		}

		protected virtual void TriggerTintForStatusEffect(CardScript targetCard, EnumStorage.StatusEffect effect)
		{
			if (effect != EnumStorage.StatusEffect.Infected && effect != EnumStorage.StatusEffect.Power) return;
			if (CombatUXManager.me == null) return;
			CombatUXManager.me.BuildCardScriptToPhysicalDictionary();
			var physicalCard = CombatUXManager.me.GetPhysicalCardFromLogicalCard(targetCard);
			if (physicalCard != null)
			{
				var cardPhysObj = physicalCard.GetComponent<CardPhysObjScript>();
				if (cardPhysObj != null) cardPhysObj.TriggerTintForStatusEffect(effect);
			}
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

		public virtual void GiveAllFriendlyStatusEffect(int amount)
		{
			if (statusEffectToGive == EnumStorage.StatusEffect.None) return;
			if (amount <= 0) return;
			var cardsToGive = new List<GameObject>();
			var combinedDeck = combatManager.combinedDeckZone;
			foreach (var card in combinedDeck)
			{
				var cardScript = card.GetComponent<CardScript>();
				if (ShouldSkipCard(cardScript)) continue;
				if (cardScript.myStatusRef == myCardScript.myStatusRef) cardsToGive.Add(card);
			}
			if (cardsToGive.Count <= 0) return;
			var targetCardScripts = new List<CardScript>();
			foreach (var card in cardsToGive)
			{
				var cardScript = card.GetComponent<CardScript>();
				if (!CanReceiveStatusEffect(cardScript, statusEffectToGive)) continue;
				targetCardScripts.Add(cardScript);
			}
			CombatUXManager.me?.PlayMultiStatusEffectProjectile(
				myCard,
				targetCardScripts,
				(target) => ApplyStatusEffectToFriendlySingle(target, amount),
				() => CombatInfoDisplayer.me?.RefreshDeckInfo()
			);
		}

		private void ApplyStatusEffectToFriendlySingle(CardScript targetCardScript, int amount)
		{
			ApplyStatusEffectCore(targetCardScript, statusEffectToGive, amount);
		}

		private void ApplyStatusEffectsToFriendly(List<GameObject> cardsToGive, int amount)
		{
			foreach (var targetCard in cardsToGive)
			{
				var targetCardScript = targetCard.GetComponent<CardScript>();
				if (!CanReceiveStatusEffect(targetCardScript, statusEffectToGive)) continue;
				ApplyStatusEffectToFriendlySingle(targetCardScript, amount);
			}
			CombatInfoDisplayer.me.RefreshDeckInfo();
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
			CombatUXManager.me?.PlayMultiStatusEffectProjectile(
				myCard,
				targetCards,
				ApplyStatusEffectToLastXCardSingle,
				() => CombatInfoDisplayer.me?.RefreshDeckInfo()
			);
		}

		private void ApplyStatusEffectToLastXCardSingle(CardScript targetCardScript)
		{
			ApplyStatusEffectCore(targetCardScript, statusEffectToGive, statusEffectLayerCount);
		}

		private void ApplyStatusEffectsToLastXCards(List<CardScript> targetCards)
		{
			foreach (var targetCardScript in targetCards)
			{
				ApplyStatusEffectToLastXCardSingle(targetCardScript);
			}
		}

		public virtual void GiveStatusEffectToXFriendly()
		{
			if (statusEffectToGive == EnumStorage.StatusEffect.None) return;
			if (xFriendlyCount <= 0 || yFriendlyLayerCount <= 0) return;
			var combinedDeck = combatManager.combinedDeckZone;
			var friendlyCards = new List<CardScript>();
			foreach (var card in combinedDeck)
			{
				var cardScript = card.GetComponent<CardScript>();
				if (ShouldSkipCard(cardScript)) continue;
				if (cardScript.myStatusRef == myCardScript.myStatusRef)
				{
					if (!CanReceiveStatusEffect(cardScript, statusEffectToGive)) continue;
					friendlyCards.Add(cardScript);
				}
			}
			if (friendlyCards.Count <= 0) return;
			friendlyCards = UtilityFuncManagerScript.ShuffleList(friendlyCards);
			var targetCards = new List<CardScript>();
			int actualCount = Mathf.Min(xFriendlyCount, friendlyCards.Count);
			for (int i = 0; i < actualCount; i++) targetCards.Add(friendlyCards[i]);
			if (targetCards.Count <= 0) return;
			CombatUXManager.me?.PlayMultiStatusEffectProjectile(
				myCard,
				targetCards,
				ApplyStatusEffectToXFriendlySingle,
				() => CombatInfoDisplayer.me?.RefreshDeckInfo()
			);
		}

		private void ApplyStatusEffectToXFriendlySingle(CardScript targetCardScript)
		{
			ApplyStatusEffectCore(targetCardScript, statusEffectToGive, yFriendlyLayerCount);
		}

		private void ApplyStatusEffectsToXFriendly(List<CardScript> targetCards)
		{
			foreach (var targetCardScript in targetCards)
			{
				ApplyStatusEffectToXFriendlySingle(targetCardScript);
			}
		}

		/// <summary>
		/// Based on the passed IntSO value, apply status effects to the same number of random friendly cards, each card receives 1 layer
		/// </summary>
		/// <param name="intSO">IntSO containing the number of friendly cards</param>
		public virtual void GiveStatusEffectToXFriendly_BasedOnIntSO(IntSO intSO)
		{
			if (intSO == null) return;
			int originalXFriendlyCount = xFriendlyCount;
			int originalYFriendlyLayerCount = yFriendlyLayerCount;
			xFriendlyCount = intSO.value;
			yFriendlyLayerCount = 1;
			GiveStatusEffectToXFriendly();
			xFriendlyCount = originalXFriendlyCount;
			yFriendlyLayerCount = originalYFriendlyLayerCount;
		}
		#endregion
	}
}
