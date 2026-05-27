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
			cm.combinedDeckZone = UtilityFuncManagerScript.ShuffleList(cm.combinedDeckZone);
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
