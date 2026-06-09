using System;
using System.Collections.Generic;
using DefaultNamespace;
using UnityEngine;

/// <summary>
/// Abstraction layer between combat logic and combat visuals.
/// All logic scripts (Effects, CombatManager, CombatFuncs) should reference
/// this interface instead of calling CombatUXManager directly.
/// </summary>
public interface ICombatVisuals
{
	#region Card Movement in Deck

	/// <summary>
	/// Move logical card to top of deck (last index) with animation.
	/// </summary>
	void MoveCardToTop(GameObject logicalCard, float duration = 0.5f, bool useArc = true, Action onComplete = null);

	/// <summary>
	/// Move logical card to bottom of deck (index 0) with animation.
	/// </summary>
	void MoveCardToBottom(GameObject logicalCard, float duration = 0.5f, bool useArc = true, Action onComplete = null);

	/// <summary>
	/// Move logical card to specific deck index with animation.
	/// </summary>
	void MoveCardToIndex(GameObject logicalCard, int index, float duration = 0.5f, bool useArc = true, Action onComplete = null);

	#endregion

	#region Card Destruction

	/// <summary>
	/// Destroy card with graveyard animation, then destroy the logical card.
	/// </summary>
	void DestroyCardWithAnimation(GameObject logicalCard, Action onComplete = null);

	#endregion

	#region Attack Animation

	/// <summary>
	/// Request attack animation playback (queued).
	/// onHit is triggered at the animation pause point (damage resolution).
	/// onComplete is triggered when animation fully ends.
	/// </summary>
	void PlayAttackAnimation(GameObject attackerCard, bool isAttackingEnemy, Action onHit = null, Action onComplete = null);

	/// <summary>
	/// Pop Up a card from its current position so the player can see it clearly.
	/// Sets isPlayingSpecialAnimation=true. Card remains at peak until SlotIn is called.
	/// </summary>
	void PopUpCard(GameObject logicalCard, Action onComplete = null);

	/// <summary>
	/// Slot In a card from its pop-up position back to its correct deck position.
	/// Clears isPlayingSpecialAnimation and syncs target position/scale.
	/// </summary>
	void SlotInCard(GameObject logicalCard, Action onComplete = null);

	/// <summary>
	/// Move card from its current spawn position to the pop-up peak position
	/// calculated from the specified deck index. Used for new cards entering
	/// the deck so they fly in and arrive at the pop-up peak, ready for SlotIn.
	/// </summary>
	void MoveCardToPopUpPosition(GameObject logicalCard, int deckIndex, Action onComplete = null);

	/// <summary>
	/// Batch animation: arc via showPos to pop-up peak, then slot in to deck top.
	/// Phase 1: all cards arc in parallel to their pop-up peaks.
	/// Phase 2: all cards slot in in parallel to their final deck top positions.
	/// </summary>
	/// <remarks>
	/// targetIndices here are the FINAL deck indices (computed by RecorderAnimationPlayer
	/// after ApplyAnimationResult). The implementation cannot resolve indices itself because
	/// Phase 1 needs every card's final position up-front to calculate pop-up peaks.
	/// </remarks>
	void MoveCardToTopPopUpBatch(List<GameObject> logicalCards, List<int> targetIndices,
	    float duration, Action onComplete = null);

	#endregion

	#region Animation Control

	/// <summary>
	/// Stop all playing and pending animations immediately.
	/// </summary>
	void StopAllAnimations();

	/// <summary>
	/// Check if an attack animation is currently playing.
	/// </summary>
	bool IsPlayingAttackAnimation();

	/// <summary>
	/// Check if there are pending animations in the queue.
	/// </summary>
	bool HasPendingAnimations();

	#endregion

	#region Status Effect Visuals

	/// <summary>
	/// Play projectile VFX from giver card to each target card, invoking callbacks.
	/// </summary>
	void PlayMultiStatusEffectProjectile(
		GameObject giverCard,
		List<CardScript> targetCards,
		Action<CardScript> onEachComplete,
		Action onAllComplete = null,
		float? customStaggerDelay = null);

	/// <summary>
	/// Apply visual tint for a status effect (Infected/Power) to the target card.
	/// </summary>
	void ApplyStatusTint(CardScript targetCard, EnumStorage.StatusEffect effect);

	/// <summary>
	/// Play particle effect for a status effect applied to a card.
	/// Logic layer should call this instead of instantiating particles directly.
	/// </summary>
	void PlayStatusEffectParticle(CardScript targetCard, ParticleSystem particlePrefab, float particleYOffset, int amount);

	#endregion

	#region Deck Synchronization

	/// <summary>
	/// Rebuild physical card list order to match logical combinedDeckZone.
	/// </summary>
	void SyncPhysicalCardsWithCombinedDeck();

	/// <summary>
	/// Update all physical cards' target positions based on current list order.
	/// </summary>
	void UpdateAllPhysicalCardTargets();

	/// <summary>
	/// Apply the result of a played animation request to the physical deck ordering.
	/// Called by RecorderAnimationPlayer after each request completes so that
	/// subsequent animations see the correct deck state.
	/// </summary>
	void ApplyAnimationResult(AnimationRequest request);

	#endregion

	#region Physical Card Lookup

	/// <summary>
	/// Get physical card GameObject from logical card GameObject.
	/// Returns null if no physical card is found.
	/// </summary>
	GameObject GetPhysicalCard(GameObject logicalCard);

	#endregion

	#region Reveal Zone

	/// <summary>
	/// Move the physical card corresponding to the logical card into the reveal zone.
	/// </summary>
	void MoveCardToRevealZone(GameObject logicalCard, Action onComplete = null);

	/// <summary>
	/// Move the card currently in reveal zone back to bottom of deck.
	/// </summary>
	void MoveRevealedCardToBottom(GameObject logicalCard, Action onComplete = null);

	#endregion

	#region Lifecycle

	/// <summary>
	/// Instantiate physical cards for all cards currently in combinedDeckZone.
	/// </summary>
	void InstantiateAllPhysicalCards();

	/// <summary>
	/// Destroy all physical cards and clear internal lists.
	/// </summary>
	void ClearAllPhysicalCards();

	/// <summary>
	/// Reset physical cards for new round (move reveal zone card back to deck).
	/// </summary>
	void ReviveAllPhysicalCards();

	#endregion

	#region Shuffle

	/// <summary>
	/// Play Start Card shuffle animation.
	/// </summary>
	void PlayShuffleAnimation(GameObject startCard, List<GameObject> shuffledCards, Action onComplete);

	#endregion

	#region Mid-Combat Add

	/// <summary>
	/// Create and add a physical card for a logical card inserted mid-combat.
	/// </summary>
	void AddCardToDeckVisual(GameObject logicalCard);

	#endregion

	#region Input Block

	/// <summary>
	/// Request to block player input. Reference counted; multiple concurrent animations are safe.
	/// </summary>
	void BlockInput(object requester);

	/// <summary>
	/// Request to unblock player input. Must match a previous BlockInput call from the same requester.
	/// </summary>
	void UnblockInput(object requester);

	#endregion
}
