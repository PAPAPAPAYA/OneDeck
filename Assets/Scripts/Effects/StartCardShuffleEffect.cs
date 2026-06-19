using System.Collections.Generic;
using DefaultNamespace;
using DefaultNamespace.Managers;
using UnityEngine;

/// <summary>
/// Encapsulates the shuffle logic that is triggered when the Start Card is revealed.
/// Previously hard-coded in CombatManager.TriggerStartCardEffect(), now unified into
/// the EffectRecorder -> RecorderAnimationPlayer pipeline via CostNEffectContainer.
/// </summary>
public class StartCardShuffleEffect : MonoBehaviour
{
	[Tooltip("Standard deviation factor relative to deck size for Start Card placement. " +
	         "Higher = more random; Lower = more centered around the middle. " +
	         "e.g. 0.15 means ~68% of placements fall within +/-15% of deck size from center.")]
	[SerializeField] private float startCardPositionStdDevFactor = 0.15f;

	public void ExecuteShuffleEffect()
	{
		var cm = CombatManager.Me;
		var startCard = cm.revealZone;
		cm.revealZone = null;

		// Logic: return Start Card to deck
		cm.combinedDeckZone.Add(startCard);

		// Logic: shuffle (with Custom Shuffle Order support)
		var shuffleOverride = cm.GetComponent<ShuffleOrderOverride>();
		if (shuffleOverride != null && shuffleOverride.useCustomOrder
		    && shuffleOverride.customOrderPrefabs != null
		    && shuffleOverride.customOrderPrefabs.Count > 0)
		{
			cm.combinedDeckZone = cm.ApplyCustomShuffleOrder(
				cm.combinedDeckZone, shuffleOverride.customOrderPrefabs);
		}
		else
		{
			// Separate Start Card and shuffle remaining cards
			var otherCards = new List<GameObject>();
			foreach (var card in cm.combinedDeckZone)
			{
				if (card != startCard)
					otherCards.Add(card);
			}
			otherCards = UtilityFuncManagerScript.ShuffleList(otherCards);

			// Determine Start Card position using Gaussian distribution centered on deck middle
			int totalSize = otherCards.Count + 1;
			float mean = (totalSize - 1) / 2.0f;
			float stdDev = Mathf.Max(1f, totalSize * startCardPositionStdDevFactor);
			int targetIndex = Mathf.RoundToInt(UtilityFuncManagerScript.GaussianRandom(mean, stdDev));
			// Prevent Start Card from being placed at the top of the deck (index Count - 1),
			// otherwise it would be revealed immediately and trigger another shuffle.
			targetIndex = Mathf.Clamp(targetIndex, 0, Mathf.Max(0, totalSize - 2));

			// Insert Start Card at the computed position
			otherCards.Insert(targetIndex, startCard);
			cm.combinedDeckZone = otherCards;
		}

		// Logic: set post-shuffle flags (moved from TriggerStartCardEffect callback)
		cm.SetRaiseAfterShuffleOnNextReveal(true);
		cm.ResetShuffleTrackersPublic();

		// Capture animation request into current EffectRecorder
		var recorderGo = EffectChainManager.Me.currentEffectRecorder;
		if (recorderGo != null)
		{
			var recorder = recorderGo.GetComponent<EffectRecorder>();
			recorder.animationRequests.Add(new AnimationRequest
			{
				type = AnimationRequestType.Shuffle,
				sourceCard = startCard,
				targetCards = new List<GameObject>(cm.combinedDeckZone),
				onComplete = () => cm.OnStartCardShuffleAnimationComplete()
			});
		}
	}
}
