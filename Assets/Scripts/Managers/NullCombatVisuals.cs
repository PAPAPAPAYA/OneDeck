using System;
using System.Collections.Generic;
using DefaultNamespace;
using UnityEngine;

/// <summary>
/// Null-object implementation of ICombatVisuals for headless testing.
/// Records all visual calls without creating any GameObjects or playing animations.
/// </summary>
public class NullCombatVisuals : ICombatVisuals
{
	public List<string> callLog = new();
	public int moveCardToTopCalls = 0;
	public int moveCardToBottomCalls = 0;
	public int moveCardToIndexCalls = 0;
	public int destroyCardCalls = 0;
	public int playAttackAnimCalls = 0;
	public int syncDeckCalls = 0;
	public int updateTargetCalls = 0;
	public int addCardCalls = 0;
	public int popUpCardCalls = 0;
	public int slotInCardCalls = 0;
	public int moveCardToPopUpPositionCalls = 0;

	public void MoveCardToTop(GameObject logicalCard, float duration = 0.5f, bool useArc = true, Action onComplete = null)
	{
		moveCardToTopCalls++;
		callLog.Add("MoveCardToTop: " + (logicalCard?.name ?? "null"));
		onComplete?.Invoke();
	}

	public void MoveCardToBottom(GameObject logicalCard, float duration = 0.5f, bool useArc = true, Action onComplete = null)
	{
		moveCardToBottomCalls++;
		callLog.Add("MoveCardToBottom: " + (logicalCard?.name ?? "null"));
		onComplete?.Invoke();
	}

	public void MoveCardToIndex(GameObject logicalCard, int index, float duration = 0.5f, bool useArc = true, Action onComplete = null)
	{
		moveCardToIndexCalls++;
		callLog.Add("MoveCardToIndex: " + (logicalCard?.name ?? "null") + " -> " + index);
		onComplete?.Invoke();
	}

	public void DestroyCardWithAnimation(GameObject logicalCard, Action onComplete = null)
	{
		destroyCardCalls++;
		callLog.Add("DestroyCard: " + (logicalCard?.name ?? "null"));
		// Note: does NOT destroy the logical card — test code may want to inspect it afterwards
		onComplete?.Invoke();
	}

	public void PlayAttackAnimation(GameObject attackerCard, bool isAttackingEnemy, Action onHit = null, Action onComplete = null)
	{
		playAttackAnimCalls++;
		callLog.Add("PlayAttackAnimation: " + (attackerCard?.name ?? "null"));
		onHit?.Invoke();
		onComplete?.Invoke();
	}

	public void PlayMultiStatusEffectProjectile(GameObject giverCard, List<CardScript> targetCards, Action<CardScript> onEachComplete, Action onAllComplete = null, float? customStaggerDelay = null, int projectileCount = 1, Vector2? projectileStartRandomOffsetRange = null, Vector2? projectileStartTimeStaggerRange = null, bool reverseDirection = false, Vector3? customEndPosition = null, List<int> projectileCountsPerTarget = null)
	{
		callLog.Add("PlayMultiStatusEffectProjectile: " + (giverCard?.name ?? "null") + " -> " + (targetCards?.Count ?? 0) + " targets x" + projectileCount + " reverseDirection=" + reverseDirection);
		if (targetCards != null)
		{
			for (int t = 0; t < targetCards.Count; t++)
			{
				var target = targetCards[t];
				if (target == null) continue;
				int count = (projectileCountsPerTarget != null && t < projectileCountsPerTarget.Count)
					? projectileCountsPerTarget[t]
					: projectileCount;
				for (int i = 0; i < count; i++)
				{
					onEachComplete?.Invoke(target);
				}
			}
		}
		onAllComplete?.Invoke();
	}

	public void PlayStatusEffectProjectileToPosition(GameObject giverCard, Vector3 endPosition, Action onComplete = null, int projectileCount = 1, Vector2? projectileStartRandomOffsetRange = null, Vector2? projectileStartTimeStaggerRange = null)
	{
		callLog.Add("PlayStatusEffectProjectileToPosition: " + (giverCard?.name ?? "null") + " -> " + endPosition + " x" + projectileCount);
		onComplete?.Invoke();
	}

	public void ApplyStatusTint(CardScript targetCard, EnumStorage.StatusEffect effect)
	{
		callLog.Add("ApplyStatusTint: " + (targetCard?.name ?? "null") + " -> " + effect);
	}

	public void PlayStatusEffectParticle(CardScript targetCard, ParticleSystem particlePrefab, float particleYOffset, int amount)
	{
		callLog.Add("PlayStatusEffectParticle: " + (targetCard?.name ?? "null") + " x" + amount);
	}

	public void SyncPhysicalCardsWithCombinedDeck()
	{
		syncDeckCalls++;
		callLog.Add("SyncPhysicalCardsWithCombinedDeck");
	}

	public void UpdateAllPhysicalCardTargets()
	{
		updateTargetCalls++;
		callLog.Add("UpdateAllPhysicalCardTargets");
	}

	public void ApplyAnimationResult(AnimationRequest request)
	{
		callLog.Add("ApplyAnimationResult: " + request.type);
	}

	public GameObject GetPhysicalCard(GameObject logicalCard)
	{
		callLog.Add("GetPhysicalCard: " + (logicalCard?.name ?? "null"));
		return null; // No physical card in headless mode
	}

	public void MoveCardToRevealZone(GameObject logicalCard, Action onComplete = null)
	{
		callLog.Add("MoveCardToRevealZone: " + (logicalCard?.name ?? "null"));
		onComplete?.Invoke();
	}

	public void MoveRevealedCardToBottom(GameObject logicalCard, Action onComplete = null)
	{
		callLog.Add("MoveRevealedCardToBottom: " + (logicalCard?.name ?? "null"));
		onComplete?.Invoke();
	}

	public void InstantiateAllPhysicalCards()
	{
		callLog.Add("InstantiateAllPhysicalCards");
	}

	public void ClearAllPhysicalCards()
	{
		callLog.Add("ClearAllPhysicalCards");
	}

	public void ReviveAllPhysicalCards()
	{
		callLog.Add("ReviveAllPhysicalCards");
	}

	public void PlayShuffleAnimation(GameObject startCard, List<GameObject> shuffledCards, Action onComplete)
	{
		callLog.Add("PlayShuffleAnimation");
		onComplete?.Invoke();
	}

	public void AddCardToDeckVisual(GameObject logicalCard)
	{
		addCardCalls++;
		callLog.Add("AddCardToDeckVisual: " + (logicalCard?.name ?? "null"));
	}

	public void PopUpCard(GameObject logicalCard, Action onComplete = null)
	{
		popUpCardCalls++;
		callLog.Add("PopUpCard: " + (logicalCard?.name ?? "null"));
		onComplete?.Invoke();
	}

	public void SlotInCard(GameObject logicalCard, Action onComplete = null)
	{
		slotInCardCalls++;
		callLog.Add("SlotInCard: " + (logicalCard?.name ?? "null"));
		onComplete?.Invoke();
	}

	public void MoveCardToPopUpPosition(GameObject logicalCard, int deckIndex, Action onComplete = null)
	{
		moveCardToPopUpPositionCalls++;
		callLog.Add("MoveCardToPopUpPosition: " + (logicalCard?.name ?? "null") + " -> " + deckIndex);
		onComplete?.Invoke();
	}

	public void MoveCardToTopPopUpBatch(List<GameObject> logicalCards, List<int> targetIndices,
	    float duration, Action onComplete = null)
	{
		callLog.Add("MoveCardToTopPopUpBatch: " + (logicalCards?.Count ?? 0) + " cards");
		onComplete?.Invoke();
	}

	public void StopAllAnimations()
	{
		callLog.Add("StopAllAnimations");
	}

	public bool IsPlayingAttackAnimation()
	{
		callLog.Add("IsPlayingAttackAnimation");
		return false;
	}

	public bool HasPendingAnimations()
	{
		callLog.Add("HasPendingAnimations");
		return false;
	}

	public void BlockInput(object requester)
	{
		callLog.Add("BlockInput: " + (requester?.GetType().Name ?? "null"));
	}

	public void UnblockInput(object requester)
	{
		callLog.Add("UnblockInput: " + (requester?.GetType().Name ?? "null"));
	}
}
