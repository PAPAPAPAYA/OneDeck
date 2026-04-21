using System.Collections.Generic;
using DefaultNamespace.SOScripts;
using UnityEngine;

namespace DefaultNamespace.Effects
{
	public class TransferStatusEffectEffect : EffectScript
	{
		[Header("Transfer Config")]
		[Tooltip("true = transfer from friendly, false = transfer from hostile")]
		public bool isFromFriendly = true;

		[Tooltip("Status effect type to transfer")]
		public EnumStorage.StatusEffect statusEffectToTransfer;

		[Tooltip("Hostile curse card's Card Type ID")]
		public StringSO curseCardTypeID;

		[Header("Status Effect Config")]
		[Tooltip("Status effect resolver script (optional)")]
		public GameObject statusEffectResolverPrefab;

		[Tooltip("Particle system played when gaining status effect (optional)")]
		public ParticleSystem statusEffectParticlePrefab;

		[Tooltip("Y-axis offset of the particle system")]
		public float particleYOffset = 0f;

		/// <summary>
		/// Transfer all specified status effects from friendly/hostile to the hostile curse card.
		/// </summary>
		public void TransferAllStatusEffectToHostileCurse()
		{
			if (statusEffectToTransfer == EnumStorage.StatusEffect.None)
			{
				Debug.LogWarning("[TransferStatusEffectEffect] statusEffectToTransfer is None!");
				return;
			}

			if (curseCardTypeID == null || string.IsNullOrEmpty(curseCardTypeID.value))
			{
				Debug.LogWarning("[TransferStatusEffectEffect] curseCardTypeID is not set!");
				return;
			}

			// Find target curse card
			CardScript targetCurseCard = FindHostileCurseCard();
			if (targetCurseCard == null)
			{
				Debug.LogWarning($"[TransferStatusEffectEffect] Cannot find hostile curse card with typeID: {curseCardTypeID.value}");
				return;
			}

			// Collect specified status effects from source cards
			List<CardScript> sourceCards = FindSourceCardsWithStatusEffect();
			if (sourceCards.Count == 0)
			{
				// No source cards with the specified status effect found, return directly
				return;
			}

			// Calculate total number of status effects to transfer
			int totalTransferCount = 0;
			foreach (var card in sourceCards)
			{
				totalTransferCount += EnumStorage.GetStatusEffectCount(card.myStatusEffects, statusEffectToTransfer);
			}

			if (totalTransferCount <= 0)
			{
				return;
			}

			// Execute transfer
			TransferStatusEffects(sourceCards, targetCurseCard, totalTransferCount);
		}

		/// <summary>
		/// Transfer 1 layer of the specified status effect from each friendly/hostile card to self.
		/// </summary>
		/// <param name="fromFriendly">true = transfer from friendly cards, false = transfer from hostile cards</param>
		public void TransferOneStatusEffectToSelf(bool fromFriendly)
		{
			if (statusEffectToTransfer == EnumStorage.StatusEffect.None)
			{
				Debug.LogWarning("[TransferStatusEffectEffect] statusEffectToTransfer is None!");
				return;
			}

			PlayerStatusSO targetStatusRef = fromFriendly ? myCardScript.myStatusRef : myCardScript.theirStatusRef;

			// Collect source cards with the specified status effect
			List<CardScript> sourceCards = new List<CardScript>();

			foreach (var card in combatManager.combinedDeckZone)
			{
				var cardScript = card.GetComponent<CardScript>();
				if (cardScript == null) continue;
				if (CombatManager.ShouldSkipEffectProcessing(cardScript)) continue;
				if (cardScript.myStatusRef != targetStatusRef) continue;
				if (cardScript == myCardScript) continue; // exclude self

				int count = EnumStorage.GetStatusEffectCount(cardScript.myStatusEffects, statusEffectToTransfer);
				if (count > 0)
				{
					sourceCards.Add(cardScript);
				}
			}

			// Check revealZone
			if (combatManager.revealZone != null)
			{
				var revealCardScript = combatManager.revealZone.GetComponent<CardScript>();
				if (revealCardScript != null &&
				    !CombatManager.ShouldSkipEffectProcessing(revealCardScript) &&
				    revealCardScript.myStatusRef == targetStatusRef &&
				    revealCardScript != myCardScript)
				{
					int count = EnumStorage.GetStatusEffectCount(revealCardScript.myStatusEffects, statusEffectToTransfer);
					if (count > 0 && !sourceCards.Exists(c => c.gameObject == combatManager.revealZone))
					{
						sourceCards.Add(revealCardScript);
					}
				}
			}

			int totalTransferCount = sourceCards.Count;
			if (totalTransferCount <= 0)
			{
				return;
			}

			// Remove 1 status effect layer from each source card
			foreach (var card in sourceCards)
			{
				for (int i = card.myStatusEffects.Count - 1; i >= 0; i--)
				{
					if (card.myStatusEffects[i] == statusEffectToTransfer)
					{
						card.myStatusEffects.RemoveAt(i);
						break; // remove only 1 layer
					}
				}
				TriggerTintForStatusEffect(card, statusEffectToTransfer);
			}

			// Apply status effects to self (1 layer per source card)
			ApplyStatusEffectCore(
				myCardScript, statusEffectToTransfer, totalTransferCount,
				statusEffectResolverPrefab, statusEffectParticlePrefab, particleYOffset, totalTransferCount,
				suppressLog: true);

			// Log effect
			LogTransferToSelfEffect(sourceCards, totalTransferCount, fromFriendly);

			CombatInfoDisplayer.me?.RefreshDeckInfo();
		}

		/// <summary>
		/// Log transfer effect to self.
		/// </summary>
		private void LogTransferToSelfEffect(List<CardScript> sourceCards, int totalCount, bool fromFriendly)
		{
			string thisCardOwnerString = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ?
				"<color=#87CEEB>Your</color> [" : "<color=orange>Enemy's</color> [";
			string thisCardColor = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ?
				"#87CEEB" : "orange";

			effectResultString.value +=
				"// " + thisCardOwnerString +
				"<color=" + thisCardColor + ">" + myCard.name + "</color>] absorbed " +
				"<color=yellow>" + totalCount + "</color> [" + statusEffectToTransfer + "] from " +
				"<color=" + (fromFriendly ? "#87CEEB" : "orange") + ">" + (fromFriendly ? "friendly" : "hostile") + "</color> cards\n";
		}

		/// <summary>
		/// Find the hostile curse card.
		/// </summary>
		private CardScript FindHostileCurseCard()
		{
			foreach (var card in combatManager.combinedDeckZone)
			{
				var cardScript = card.GetComponent<CardScript>();
				if (cardScript == null) continue;

				// Skip neutral cards
				if (CombatManager.ShouldSkipEffectProcessing(cardScript)) continue;

				// Check if it's an enemy card and cardTypeID matches
				if (cardScript.myStatusRef == myCardScript.theirStatusRef &&
				    cardScript.cardTypeID == curseCardTypeID.value)
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
				    revealCardScript.cardTypeID == curseCardTypeID.value)
				{
					return revealCardScript;
				}
			}

			return null;
		}

		/// <summary>
		/// Find source cards with the specified status effect (friendly or hostile).
		/// </summary>
		private List<CardScript> FindSourceCardsWithStatusEffect()
		{
			var result = new List<CardScript>();
			PlayerStatusSO targetStatusRef = isFromFriendly ? myCardScript.myStatusRef : myCardScript.theirStatusRef;

			foreach (var card in combatManager.combinedDeckZone)
			{
				var cardScript = card.GetComponent<CardScript>();
				if (cardScript == null) continue;

				// Skip neutral cards
				if (CombatManager.ShouldSkipEffectProcessing(cardScript)) continue;

				// Check if it's a target side (friendly or hostile) card
				if (cardScript.myStatusRef != targetStatusRef) continue;

				// Check if it has the specified status effect
				if (EnumStorage.GetStatusEffectCount(cardScript.myStatusEffects, statusEffectToTransfer) > 0)
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
				    revealCardScript.myStatusRef == targetStatusRef &&
				    EnumStorage.GetStatusEffectCount(revealCardScript.myStatusEffects, statusEffectToTransfer) > 0 &&
				    !result.Exists(c => c.gameObject == combatManager.revealZone))
				{
					result.Add(revealCardScript);
				}
			}

			return result;
		}

		/// <summary>
		/// Execute status effect transfer.
		/// </summary>
		private void TransferStatusEffects(List<CardScript> sourceCards, CardScript targetCard, int totalCount)
		{
			// Remove status effects from source cards
			foreach (var card in sourceCards)
			{
				for (int i = card.myStatusEffects.Count - 1; i >= 0; i--)
				{
					if (card.myStatusEffects[i] == statusEffectToTransfer)
					{
						card.myStatusEffects.RemoveAt(i);
					}
				}

				// Refresh visual display (if Power or Infected)
				TriggerTintForStatusEffect(card, statusEffectToTransfer);
			}

			// Use core method to add status effects to target card (trigger events and visuals)
			ApplyStatusEffectCore(
				targetCard, statusEffectToTransfer, totalCount,
				statusEffectResolverPrefab, statusEffectParticlePrefab, particleYOffset, totalCount,
				suppressLog: true);

			// Output effect info
			LogTransferEffect(sourceCards, targetCard, totalCount);

			// Refresh info display
			CombatInfoDisplayer.me?.RefreshDeckInfo();
		}

		/// <summary>
		/// Log transfer effect.
		/// </summary>
		private void LogTransferEffect(List<CardScript> sourceCards, CardScript targetCard, int totalCount)
		{
			string sourceOwner = isFromFriendly ?
				(myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? "Your" : "Enemy's") :
				(myCardScript.theirStatusRef == combatManager.ownerPlayerStatusRef ? "Your" : "Enemy's");

			string thisCardOwnerString = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ?
				"<color=#87CEEB>Your</color> [" : "<color=orange>Enemy's</color> [";
			string thisCardColor = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ?
				"#87CEEB" : "orange";
			string targetCardColor = targetCard.myStatusRef == combatManager.ownerPlayerStatusRef ?
				"#87CEEB" : "orange";

			effectResultString.value +=
				"// " + thisCardOwnerString +
				"<color=" + thisCardColor + ">" + myCard.name + "</color>] transferred " +
				"<color=yellow>" + totalCount + "</color> [" + statusEffectToTransfer + "] from " +
				"<color=" + (isFromFriendly ? "#87CEEB" : "orange") + ">" + (isFromFriendly ? "friendly" : "hostile") + "</color> cards to " +
				"<color=" + targetCardColor + ">" + targetCard.name + "</color>]\n";
		}
	}
}
