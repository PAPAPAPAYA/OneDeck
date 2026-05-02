using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MonoBehaviour wrapper for NullCombatVisuals.
/// Attach this to a GameObject and drag it into CombatManager.visualsOverride
/// to run combat in headless mode (no animations, no physical cards).
/// </summary>
public class NullCombatVisualsBehaviour : MonoBehaviour, ICombatVisuals
{
	private readonly NullCombatVisuals _nullVisuals = new NullCombatVisuals();

	public List<string> CallLog => _nullVisuals.callLog;
	public int MoveCardToTopCalls => _nullVisuals.moveCardToTopCalls;
	public int MoveCardToBottomCalls => _nullVisuals.moveCardToBottomCalls;
	public int MoveCardToIndexCalls => _nullVisuals.moveCardToIndexCalls;
	public int DestroyCardCalls => _nullVisuals.destroyCardCalls;
	public int PlayAttackAnimCalls => _nullVisuals.playAttackAnimCalls;
	public int SyncDeckCalls => _nullVisuals.syncDeckCalls;
	public int UpdateTargetCalls => _nullVisuals.updateTargetCalls;
	public int AddCardCalls => _nullVisuals.addCardCalls;

	public void MoveCardToTop(GameObject logicalCard, float duration = 0.5f, bool useArc = true, Action onComplete = null)
		=> _nullVisuals.MoveCardToTop(logicalCard, duration, useArc, onComplete);

	public void MoveCardToBottom(GameObject logicalCard, float duration = 0.5f, bool useArc = true, Action onComplete = null)
		=> _nullVisuals.MoveCardToBottom(logicalCard, duration, useArc, onComplete);

	public void MoveCardToIndex(GameObject logicalCard, int index, float duration = 0.5f, bool useArc = true, Action onComplete = null)
		=> _nullVisuals.MoveCardToIndex(logicalCard, index, duration, useArc, onComplete);

	public void DestroyCardWithAnimation(GameObject logicalCard, Action onComplete = null)
		=> _nullVisuals.DestroyCardWithAnimation(logicalCard, onComplete);

	public void PlayAttackAnimation(GameObject attackerCard, bool isAttackingEnemy, Action onHit = null, Action onComplete = null)
		=> _nullVisuals.PlayAttackAnimation(attackerCard, isAttackingEnemy, onHit, onComplete);

	public void PlayMultiStatusEffectProjectile(GameObject giverCard, List<CardScript> targetCards, Action<CardScript> onEachComplete, Action onAllComplete = null, float? customStaggerDelay = null)
		=> _nullVisuals.PlayMultiStatusEffectProjectile(giverCard, targetCards, onEachComplete, onAllComplete, customStaggerDelay);

	public void ApplyStatusTint(CardScript targetCard, EnumStorage.StatusEffect effect)
		=> _nullVisuals.ApplyStatusTint(targetCard, effect);

	public void PlayStatusEffectParticle(CardScript targetCard, ParticleSystem particlePrefab, float particleYOffset, int amount)
		=> _nullVisuals.PlayStatusEffectParticle(targetCard, particlePrefab, particleYOffset, amount);

	public void SyncPhysicalCardsWithCombinedDeck()
		=> _nullVisuals.SyncPhysicalCardsWithCombinedDeck();

	public void UpdateAllPhysicalCardTargets()
		=> _nullVisuals.UpdateAllPhysicalCardTargets();

	public GameObject GetPhysicalCard(GameObject logicalCard)
		=> _nullVisuals.GetPhysicalCard(logicalCard);

	public void MoveCardToRevealZone(GameObject logicalCard)
		=> _nullVisuals.MoveCardToRevealZone(logicalCard);

	public void MoveRevealedCardToBottom(GameObject logicalCard, Action onComplete = null)
		=> _nullVisuals.MoveRevealedCardToBottom(logicalCard, onComplete);

	public void InstantiateAllPhysicalCards()
		=> _nullVisuals.InstantiateAllPhysicalCards();

	public void ClearAllPhysicalCards()
		=> _nullVisuals.ClearAllPhysicalCards();

	public void ReviveAllPhysicalCards()
		=> _nullVisuals.ReviveAllPhysicalCards();

	public void PlayShuffleAnimation(GameObject startCard, List<GameObject> shuffledCards, Action onComplete)
		=> _nullVisuals.PlayShuffleAnimation(startCard, shuffledCards, onComplete);

	public void AddCardToDeckVisual(GameObject logicalCard)
		=> _nullVisuals.AddCardToDeckVisual(logicalCard);

	public void StopAllAnimations()
		=> _nullVisuals.StopAllAnimations();

	public bool IsPlayingAttackAnimation()
		=> _nullVisuals.IsPlayingAttackAnimation();

	public bool HasPendingAnimations()
		=> _nullVisuals.HasPendingAnimations();

	public void BlockInput(object requester)
		=> _nullVisuals.BlockInput(requester);

	public void UnblockInput(object requester)
		=> _nullVisuals.UnblockInput(requester);
}
