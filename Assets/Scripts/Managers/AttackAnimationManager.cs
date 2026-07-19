using System;
using System.Collections;
using System.Collections.Generic;
using DefaultNamespace.Managers;
using DG.Tweening;
using MilkShake;
using UnityEngine;

/// <summary>
/// Attack animation manager - manages queued playback of card attack animations
/// </summary>
public class AttackAnimationManager : MonoBehaviour
{
	#region SINGLETON
	public static AttackAnimationManager me;
	void Awake()
	{
		me = this;
	}
	#endregion

	[Header("ATTACK TARGET POSITIONS")]
	[Tooltip("Enemy position (target position when taking damage)")]
	public Transform enemyTargetPos;
	[Tooltip("Player position (target position when taking damage)")]
	public Transform playerTargetPos;

	[Header("ANIMATION SETTINGS")]
	[Tooltip("Scale multiplier before attack")]
	public float attackScaleMultiplier = 1.4f;
	[Tooltip("Scale-up animation duration")]
	public float scaleUpDuration = 0.2f;
	[Tooltip("Charge animation duration")]
	public float chargeDuration = 0.2f;
	[Tooltip("Scale multiplier during charge (relative to original size)")]
	public float chargeScaleMultiplier = 0.85f;

	[Header("SCREEN SHAKE")]
	[Tooltip("Camera shaker component (for screen shake)")]
	public Shaker cameraShaker;
	[Tooltip("Screen shake preset on damage resolution")]
	public ShakePreset hitShakePreset;
	[Tooltip("Overshoot distance (distance past the target)")]
	public float overshootDistance = 2.0f;
	[Tooltip("Bounce-back animation duration")]
	public float bounceBackDuration = 0.15f;

	[Tooltip("Wind-up distance (charge-up distance)")]
	public float windUpDistance = 0.5f;
	[Tooltip("Wind-up animation duration")]
	public float windUpDuration = 0.1f;
	[Tooltip("Pause duration at target position")]
	public float pauseAtTarget = 0.05f;
	[Tooltip("Return-to-reveal animation duration")]
	public float returnToRevealDuration = 0.2f;

	[Header("STATE")]
	[Tooltip("Whether attack animation is currently playing")]
	public bool isPlayingAttackAnimation = false;

	// Animation queue
	private Queue<AttackAnimData> _attackQueue = new();
	private bool _isProcessingQueue = false;
	private bool _queueStartPending = false;

	private CombatManager _combatManager => CombatManager.Me;
	private CombatUXManager _combatUXManager => CombatUXManager.me;

	// Deck focus hold counter: when > 0, ProcessQueue will not auto-restore deck focus
	private int _deckFocusHoldCount = 0;

	/// <summary>
	/// Hold deck focus during a batch of attack animations. Prevents ProcessQueue from restoring focus between individual animations.
	/// Must be paired with ReleaseDeckFocus.
	/// </summary>
public void HoldDeckFocus()
	{
		_deckFocusHoldCount++;

	}

	/// <summary>
	/// Release a deck focus hold. If count reaches zero and deck is still focused, triggers restore.
	/// </summary>
public void ReleaseDeckFocus()
	{
		_deckFocusHoldCount--;

		if (_deckFocusHoldCount <= 0)
		{
			_deckFocusHoldCount = 0;
			if (_combatUXManager != null && _combatUXManager.IsDeckFocused)
			{
	
				StartCoroutine(_combatUXManager.RestoreDeckFocusCoroutine());
			}
			else
			{
	
			}
		}
	}

	/// <summary>
	/// Request attack animation playback (add to queue)
	/// </summary>
	/// <param name="attackerCard">Attacker card (logical card)</param>
	/// <param name="isAttackingEnemy">true=attack enemy, false=attack self</param>
	/// <param name="onHit">Triggered during pause at target position (phase 4), used for damage resolution</param>
	/// <param name="onComplete">Triggered when animation fully ends</param>
	public void RequestAttackAnimation(GameObject attackerCard, bool isAttackingEnemy, Action onHit = null, Action onComplete = null)
	{
		var data = new AttackAnimData
		{
			attackerLogicalCard = attackerCard,
			isAttackingEnemy = isAttackingEnemy,
			onHit = onHit,
			onComplete = onComplete
		};
		
		_attackQueue.Enqueue(data);
		
		if (!_isProcessingQueue && !_queueStartPending)
		{
			_queueStartPending = true;
			StartCoroutine(DelayedStartQueue());
		}
	}

	/// <summary>
	/// Delay ProcessQueue start to next frame so all synchronous RequestAttackAnimation calls in current frame can enqueue first.
	/// This prevents AnimationStateTracker from delaying subsequent onMeBuried events before they get a chance to enqueue.
	/// </summary>
	private IEnumerator DelayedStartQueue()
	{
		yield return null; // Wait until next frame
		_queueStartPending = false;
		if (!_isProcessingQueue && _attackQueue.Count > 0)
		{
			_isProcessingQueue = true;
			StartCoroutine(ProcessQueue());
		}
	}

	/// <summary>
	/// Process animation queue
	/// </summary>
	private IEnumerator ProcessQueue()
	{
		_isProcessingQueue = true;
		AnimationStateTracker.me?.RegisterAnimation();

		while (_attackQueue.Count > 0)
		{
			var data = _attackQueue.Dequeue();

			yield return StartCoroutine(PlayAttackAnimationCoroutine(data));
		}

		_isProcessingQueue = false;

		// All attack animations done: restore deck focus (unless held by RecorderAnimationPlayer batch)

		if (_deckFocusHoldCount <= 0 && _combatUXManager != null && _combatUXManager.IsDeckFocused)
		{

			yield return StartCoroutine(_combatUXManager.RestoreDeckFocusCoroutine());
		}
		else
		{

		}

		AnimationStateTracker.me?.CompleteAnimation();
	}

	/// <summary>
	/// Play single attack animation coroutine
	/// </summary>
	private IEnumerator PlayAttackAnimationCoroutine(AttackAnimData data)
	{
		isPlayingAttackAnimation = true;

		// Get physical card
		CardScript cardScript = data.attackerLogicalCard.GetComponent<CardScript>();

		TestManager.Log("[RecorderAnimationPlayer] PlayAttackAnimationCoroutine START attacker=" + (cardScript != null ? cardScript.name : "null") + " isInRevealZone=" + (_combatManager != null && _combatManager.revealZone == data.attackerLogicalCard) + " time=" + Time.time);
		
		// Block player input
		if (_combatManager != null)
		{
			_combatManager.visuals.BlockInput(this);
		}

		// Get physical card
		if (cardScript == null)
		{

			data.onComplete?.Invoke();
			isPlayingAttackAnimation = false;
			ReleasePlayerInput();
			yield break;
		}

		if (_combatUXManager == null)
		{

			data.onComplete?.Invoke();
			isPlayingAttackAnimation = false;
			ReleasePlayerInput();
			yield break;
		}

		GameObject physicalCard = _combatUXManager.GetPhysicalCardFromLogicalCard(cardScript);
		if (physicalCard == null)
		{

			data.onComplete?.Invoke();
			isPlayingAttackAnimation = false;
			ReleasePlayerInput();
			yield break;
		}

		CardPhysObjScript physScript = physicalCard.GetComponent<CardPhysObjScript>();
		if (physScript == null)
		{

			data.onComplete?.Invoke();
			isPlayingAttackAnimation = false;
			ReleasePlayerInput();
			yield break;
		}

		// Determine target position
		Transform targetTransform = data.isAttackingEnemy ? enemyTargetPos : playerTargetPos;
		if (targetTransform == null)
		{

			data.onComplete?.Invoke();
			isPlayingAttackAnimation = false;
			ReleasePlayerInput();
			yield break;
		}

		// Determine if attacker is in reveal zone
		bool isInRevealZone = _combatManager != null && _combatManager.revealZone == data.attackerLogicalCard;


		// If NOT in reveal zone, focus deck on this card first
		if (!isInRevealZone)
		{

			TestManager.Log("[RecorderAnimationPlayer] PlayAttackAnimationCoroutine calling FocusOnCardCoroutine attacker=" + cardScript.name + " time=" + Time.time);
			yield return StartCoroutine(_combatUXManager.FocusOnCardCoroutine(cardScript));
			TestManager.Log("[RecorderAnimationPlayer] PlayAttackAnimationCoroutine FocusOnCardCoroutine DONE attacker=" + cardScript.name + " time=" + Time.time);

		}
		else
		{

			// Reveal zone card should be the visual focus: restore deck focus so reveal zone card returns to center
			if (_combatUXManager != null && _combatUXManager.IsDeckFocused)
			{
	
				yield return StartCoroutine(_combatUXManager.RestoreDeckFocusCoroutine());
	
			}
		}

		Vector3 startPos = physicalCard.transform.position;
		Vector3 targetPos = targetTransform.position;
		// Keep the whole attack in the attacker's current z plane: wind-up already uses
		// startPos.z, and charge/overshoot derive from targetPos, so pinning targetPos.z
		// here removes any z drift mid-flight (VISUAL-FIX 2026-07-18, reveal-z family).
		targetPos.z = startPos.z;
		Vector3 originalScale = physicalCard.transform.localScale;

		// Capture popup state so we can keep the card at peak if it was popped up for an off-reveal effect.
		bool wasPoppedUp = physScript.isPoppedUp;

		// Mark that special animation is playing to prevent CardPhysObjScript from overriding DOTween
		physScript.isPlayingSpecialAnimation = true;

		try
		{
			// ========== Phase 1+2: Scale up + Rotate + Wind up (simultaneous) ==========
			Vector3 windUpPos = CalculateWindUpPosition(startPos, targetPos);
			yield return ScaleUpAndWindUpAnimation(physicalCard, originalScale, targetPos, windUpPos);

			// ========== Phase 3: Charge to target pos (enemy/player position), scale down simultaneously ==========
			yield return ChargeToTargetAnimation(physicalCard, targetPos, originalScale);

			// ========== Phase 4: Pause at target pos, trigger damage resolution and screen shake ==========
			data.onHit?.Invoke();
			
			// Trigger screen shake (camera only)
			if (cameraShaker != null && hitShakePreset != null)
			{
				cameraShaker.Shake(hitShakePreset);
			}
			
			yield return new WaitForSeconds(CombatAnimationSpeed.ScaleDuration(pauseAtTarget));

			// ========== Phase 5: Charge to overshoot position (past target) ==========
			Vector3 overshootPos = CalculateOvershootPosition(targetPos, startPos);
			yield return OvershootAnimation(physicalCard, overshootPos);

			// ========== Phase 6: Return to appropriate position ==========
			if (isInRevealZone)
			{
				// Return to reveal position
				// VISUAL-FIX(2026-07-18): must use the dynamically z-clamped reveal position
				// (see CombatUXManager.GetRevealZonePosition); the raw transform z sits behind
				// large decks and the returning attacker gets occluded by deck front cards.
				Vector3 revealPos = _combatUXManager.GetRevealZonePosition();
				Vector3 revealSize = _combatUXManager.physicalCardRevealSize;
				yield return ReturnToRevealFromOvershootAnimation(physicalCard, revealPos, revealSize, originalScale);
			}
			else
			{
				// Return to deck position (respects focus offset), or back to popup peak if the card
				// was popped up for an off-reveal effect animation.
				Vector3 deckPos;
				if (wasPoppedUp)
				{
					deckPos = startPos;
				}
				else
				{
					int deckIndex = _combatUXManager.GetPhysicalCardDeckIndex(physicalCard);
					deckPos = deckIndex >= 0
						? _combatUXManager.CalculatePositionAtIndex(deckIndex)
						: startPos;
				}
				yield return ReturnToDeckFromOvershootAnimation(physicalCard, deckPos, originalScale);
			}
		}
		finally
		{
			// Ensure special animation flag is always restored, unless the card should remain at
			// the popup peak until the caller slots it back in.
			if (!wasPoppedUp)
				physScript.isPlayingSpecialAnimation = false;
			
			// Update CardPhysObjScript target position to prevent snapping
			if (isInRevealZone)
			{
				// VISUAL-FIX(2026-07-18): dynamically z-clamped reveal position, same as above.
				Vector3 revealPos = _combatUXManager.GetRevealZonePosition();
				Vector3 revealSize = _combatUXManager.physicalCardRevealSize;

				physScript.SetTargetPosition(revealPos);
				physScript.SetTargetScale(revealSize);
			}
			else if (wasPoppedUp)
			{
				// Keep target at the popup peak so the final SlotIn can animate from the correct position.
				physScript.SetTargetPosition(startPos);
				physScript.SetTargetScale(originalScale);
			}
			else
			{
				int deckIndex = _combatUXManager.GetPhysicalCardDeckIndex(physicalCard);
				if (deckIndex >= 0)
				{
					Vector3 deckPos = _combatUXManager.CalculatePositionAtIndex(deckIndex);
					physScript.SetTargetPosition(deckPos);
					// Cascade: restore the card's per-depth scale (uniform deck size when cascade is disabled)
					physScript.SetTargetScale(_combatUXManager.GetDeckScaleAtIndex(deckIndex));
				}
			}
		}

		// Mark animation complete
		isPlayingAttackAnimation = false;

		TestManager.Log("[RecorderAnimationPlayer] PlayAttackAnimationCoroutine END attacker=" + (cardScript != null ? cardScript.name : "null") + " time=" + Time.time);
		
		// Restore player input
		ReleasePlayerInput();

		// Trigger callback (let card continue to bottom)
		data.onComplete?.Invoke();
	}

	/// <summary>
	/// Scale up + rotate + wind up animation (simultaneous)
	/// </summary>
	private IEnumerator ScaleUpAndWindUpAnimation(GameObject physicalCard, Vector3 originalScale, Vector3 targetPos, Vector3 windUpPos)
	{
		bool completed = false;
		
		// Calculate angle towards target
		float targetAngle = CalculateRotationTowardsTarget(physicalCard.transform.position, targetPos);
		
		// Use longer duration to ensure rotation covers the whole process
		float scaledScaleUpDuration = CombatAnimationSpeed.ScaleDuration(scaleUpDuration);
		float scaledWindUpDuration = CombatAnimationSpeed.ScaleDuration(windUpDuration);
		float rotationDuration = Mathf.Max(scaledScaleUpDuration, scaledWindUpDuration);
		
		// Create sequence: scale up + rotate + wind up (simultaneous)
		Sequence sequence = DOTween.Sequence();
		
		sequence.Append(
			physicalCard.transform.DOScale(originalScale * attackScaleMultiplier, scaledScaleUpDuration)
				.SetEase(Ease.OutQuad)
		);
		
		sequence.Join(
			physicalCard.transform.DORotate(new Vector3(0, 0, targetAngle), rotationDuration)
				.SetEase(Ease.OutQuad)
		);
		
		sequence.Join(
			physicalCard.transform.DOMove(windUpPos, scaledWindUpDuration)
				.SetEase(Ease.OutQuad)
		);
		
		sequence.OnComplete(() => completed = true);

		yield return new WaitUntil(() => completed);
	}
	
	/// <summary>
	/// Calculate card rotation angle towards target (top points to target)
	/// </summary>
	private float CalculateRotationTowardsTarget(Vector3 cardPos, Vector3 targetPos)
	{
		// Calculate direction from card to target
		Vector3 direction = targetPos - cardPos;
		
		// Use Atan2 to calculate angle (radians to degrees)
		// +90 because card default top faces up, needs adjustment
		float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
		
		return angle;
	}

	/// <summary>
	/// Calculate wind-up position (ignores z-axis, works in xy-plane only)
	/// </summary>
	private Vector3 CalculateWindUpPosition(Vector3 startPos, Vector3 targetPos)
	{
		Vector2 direction2D = (new Vector2(targetPos.x, targetPos.y) - new Vector2(startPos.x, startPos.y)).normalized;
		return new Vector3(
			startPos.x - direction2D.x * windUpDistance,
			startPos.y - direction2D.y * windUpDistance,
			startPos.z
		);
	}

	/// <summary>
	/// Calculate overshoot position (continue past target, ignores z-axis)
	/// </summary>
	private Vector3 CalculateOvershootPosition(Vector3 targetPos, Vector3 startPos)
	{
		Vector2 direction2D = (new Vector2(targetPos.x, targetPos.y) - new Vector2(startPos.x, startPos.y)).normalized;
		return new Vector3(
			targetPos.x + direction2D.x * overshootDistance,
			targetPos.y + direction2D.y * overshootDistance,
			targetPos.z
		);
	}

	/// <summary>
	/// Charge to target position, scale down simultaneously
	/// </summary>
	private IEnumerator ChargeToTargetAnimation(GameObject physicalCard, Vector3 targetPos, Vector3 originalScale)
	{
		bool completed = false;

		// Calculate scale target during charge
		Vector3 chargeTargetScale = originalScale * chargeScaleMultiplier;

		// Create sequence: move + scale down
		Sequence chargeSequence = DOTween.Sequence();
		
		float scaledChargeDuration = CombatAnimationSpeed.ScaleDuration(chargeDuration);
		
		chargeSequence.Append(
			physicalCard.transform.DOMove(targetPos, scaledChargeDuration)
				.SetEase(Ease.InCubic)
		);
		
		chargeSequence.Join(
			physicalCard.transform.DOScale(chargeTargetScale, scaledChargeDuration)
				.SetEase(Ease.InQuad)
		);
		
		chargeSequence.OnComplete(() => completed = true);

		yield return new WaitUntil(() => completed);
	}

	/// <summary>
	/// Charge from target position to overshoot position
	/// </summary>
	private IEnumerator OvershootAnimation(GameObject physicalCard, Vector3 overshootPos)
	{
		bool completed = false;

		physicalCard.transform.DOMove(overshootPos, CombatAnimationSpeed.ScaleDuration(bounceBackDuration * 0.5f))
			.SetEase(Ease.OutQuad)
			.OnComplete(() => completed = true);

		yield return new WaitUntil(() => completed);
	}

	/// <summary>
	/// Return from overshoot position to reveal position animation, restore size and zero rotation simultaneously
	/// </summary>
	private IEnumerator ReturnToRevealFromOvershootAnimation(GameObject physicalCard, Vector3 revealPos, Vector3 revealSize, Vector3 originalScale)
	{
		bool completed = false;
		
		// Create sequence: move to reveal position + restore reveal size + zero rotation (sync with move)
		Sequence returnSequence = DOTween.Sequence();
		
		float scaledReturnDuration = CombatAnimationSpeed.ScaleDuration(returnToRevealDuration);
		
		returnSequence.Append(
			physicalCard.transform.DOMove(revealPos, scaledReturnDuration)
				.SetEase(Ease.OutQuad)
		);
		
		returnSequence.Join(
			physicalCard.transform.DOScale(revealSize, scaledReturnDuration)
				.SetEase(Ease.OutQuad)
		);
		
		// Zero rotation, duration matches movement to ensure synchronous completion
		returnSequence.Join(
			physicalCard.transform.DORotate(Vector3.zero, scaledReturnDuration)
				.SetEase(Ease.OutQuad)
		);
		
		returnSequence.OnComplete(() => completed = true);

		yield return new WaitUntil(() => completed);
	}

	/// <summary>
	/// Return from overshoot position to deck position animation, restore original scale and zero rotation
	/// </summary>
	private IEnumerator ReturnToDeckFromOvershootAnimation(GameObject physicalCard, Vector3 deckPos, Vector3 originalScale)
	{
		bool completed = false;
		
		Sequence returnSequence = DOTween.Sequence();
		
		float scaledReturnDuration = CombatAnimationSpeed.ScaleDuration(returnToRevealDuration);
		
		returnSequence.Append(
			physicalCard.transform.DOMove(deckPos, scaledReturnDuration)
				.SetEase(Ease.OutQuad)
		);
		
		returnSequence.Join(
			physicalCard.transform.DOScale(originalScale, scaledReturnDuration)
				.SetEase(Ease.OutQuad)
		);
		
		returnSequence.Join(
			physicalCard.transform.DORotate(Vector3.zero, scaledReturnDuration)
				.SetEase(Ease.OutQuad)
		);
		
		returnSequence.OnComplete(() => completed = true);

		yield return new WaitUntil(() => completed);
	}

	/// <summary>
	/// Release player input
	/// </summary>
	private void ReleasePlayerInput()
	{
		if (_combatManager != null)
		{
			_combatManager.visuals.UnblockInput(this);
		}
	}

	/// <summary>
	/// Stop all attack animations (used for combat end or phase transition)
	/// </summary>
	public void StopAllAttackAnimations()
	{
		StopAllCoroutines();
		_attackQueue.Clear();
		_isProcessingQueue = false;
		isPlayingAttackAnimation = false;
		
		// If pending attack animations are cancelled, release any HP display snapshots
		// so the UI does not stay stuck on stale values.
		CombatInfoDisplayer.me?.ClearHpDisplayLocks();
		
		// Restore deck focus if active (unless held by RecorderAnimationPlayer batch)
		if (_deckFocusHoldCount <= 0 && _combatUXManager != null && _combatUXManager.IsDeckFocused)
		{
			StartCoroutine(_combatUXManager.RestoreDeckFocusCoroutine());
		}
		
		// Restore player input
		if (_combatManager != null)
		{
			_combatManager.visuals.UnblockInput(this);
		}
	}

	/// <summary>
	/// Check whether there are pending attack animations
	/// </summary>
	public bool HasPendingAnimations()
	{
		return _attackQueue.Count > 0 || isPlayingAttackAnimation;
	}
}

/// <summary>
/// Attack animation data
/// </summary>
public struct AttackAnimData
{
	public GameObject attackerLogicalCard;
	public bool isAttackingEnemy; // true=attack enemy, false=attack self
	public Action onHit; // Triggered during pause at target position (phase 4), used for damage resolution
	public Action onComplete; // Triggered when animation fully ends
}
