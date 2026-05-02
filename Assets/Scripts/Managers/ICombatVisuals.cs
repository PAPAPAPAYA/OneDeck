using System;
using System.Collections.Generic;
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
	void MoveCardToRevealZone(GameObject logicalCard);

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
