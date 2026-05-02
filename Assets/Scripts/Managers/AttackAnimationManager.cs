using System;
using System.Collections;
using System.Collections.Generic;
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

	private CombatManager _combatManager => CombatManager.Me;
	private CombatUXManager _combatUXManager => CombatUXManager.me;

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
		
		if (!_isProcessingQueue)
		{
			StartCoroutine(ProcessQueue());
		}
	}

	/// <summary>
	/// Process animation queue
	/// </summary>
	private IEnumerator ProcessQueue()
	{
		_isProcessingQueue = true;

		while (_attackQueue.Count > 0)
		{
			var data = _attackQueue.Dequeue();
			yield return StartCoroutine(PlayAttackAnimationCoroutine(data));
		}

		_isProcessingQueue = false;

		// All attack animations done: restore deck focus
		if (_combatUXManager != null && _combatUXManager.IsDeckFocused)
		{
			yield return StartCoroutine(_combatUXManager.RestoreDeckFocusCoroutine());
		}
	}

	/// <summary>
	/// Play single attack animation coroutine
	/// </summary>
	private IEnumerator PlayAttackAnimationCoroutine(AttackAnimData data)
	{
		isPlayingAttackAnimation = true;
		
		// Block player input
		if (_combatManager != null)
		{
			_combatManager.visuals.BlockInput(this);
		}

		// Get physical card
		CardScript cardScript = data.attackerLogicalCard.GetComponent<CardScript>();
		if (cardScript == null)
		{
			Debug.LogWarning("[AttackAnimationManager] Card has no CardScript");
			data.onComplete?.Invoke();
			isPlayingAttackAnimation = false;
			ReleasePlayerInput();
			yield break;
		}

		if (_combatUXManager == null)
		{
			Debug.LogWarning("[AttackAnimationManager] CombatUXManager is not available");
			data.onComplete?.Invoke();
			isPlayingAttackAnimation = false;
			ReleasePlayerInput();
			yield break;
		}

		GameObject physicalCard = _combatUXManager.GetPhysicalCardFromLogicalCard(cardScript);
		if (physicalCard == null)
		{
			Debug.LogWarning($"[AttackAnimationManager] No physical card found for {data.attackerLogicalCard.name}");
			data.onComplete?.Invoke();
			isPlayingAttackAnimation = false;
			ReleasePlayerInput();
			yield break;
		}

		CardPhysObjScript physScript = physicalCard.GetComponent<CardPhysObjScript>();
		if (physScript == null)
		{
			Debug.LogWarning("[AttackAnimationManager] Physical card has no CardPhysObjScript");
			data.onComplete?.Invoke();
			isPlayingAttackAnimation = false;
			ReleasePlayerInput();
			yield break;
		}

		// Determine target position
		Transform targetTransform = data.isAttackingEnemy ? enemyTargetPos : playerTargetPos;
		if (targetTransform == null)
		{
			Debug.LogWarning("[AttackAnimationManager] Target position not set");
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
			yield return StartCoroutine(_combatUXManager.FocusOnCardCoroutine(cardScript));
		}

		Vector3 startPos = physicalCard.transform.position;
		Vector3 targetPos = targetTransform.position;
		Vector3 originalScale = physicalCard.transform.localScale;

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
			
			yield return new WaitForSeconds(pauseAtTarget);

			// ========== Phase 5: Charge to overshoot position (past target) ==========
			Vector3 overshootPos = CalculateOvershootPosition(targetPos, startPos);
			yield return OvershootAnimation(physicalCard, overshootPos);

			// ========== Phase 6: Return to appropriate position ==========
			if (isInRevealZone)
			{
				// Return to reveal position
				Vector3 revealPos = _combatUXManager.physicalCardRevealPos.position;
				Vector3 revealSize = _combatUXManager.physicalCardRevealSize;
				yield return ReturnToRevealFromOvershootAnimation(physicalCard, revealPos, revealSize, originalScale);
			}
			else
			{
				// Return to deck position (respects focus offset)
				int deckIndex = _combatUXManager.GetPhysicalCardDeckIndex(physicalCard);
				Vector3 deckPos = deckIndex >= 0
					? _combatUXManager.CalculatePositionAtIndex(deckIndex)
					: startPos;
				yield return ReturnToDeckFromOvershootAnimation(physicalCard, deckPos, originalScale);
			}
		}
		finally
		{
			// Ensure special animation flag is always restored
			physScript.isPlayingSpecialAnimation = false;
			
			// Update CardPhysObjScript target position to prevent snapping
			if (isInRevealZone)
			{
				Vector3 revealPos = _combatUXManager.physicalCardRevealPos.position;
				Vector3 revealSize = _combatUXManager.physicalCardRevealSize;

				physScript.SetTargetPosition(revealPos);
				physScript.SetTargetScale(revealSize);
			}
			else
			{
				int deckIndex = _combatUXManager.GetPhysicalCardDeckIndex(physicalCard);
				if (deckIndex >= 0)
				{
					Vector3 deckPos = _combatUXManager.CalculatePositionAtIndex(deckIndex);
					physScript.SetTargetPosition(deckPos);
					physScript.SetTargetScale(_combatUXManager.physicalCardDeckSize);
				}
			}
		}

		// Mark animation complete
		isPlayingAttackAnimation = false;
		
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
		float rotationDuration = Mathf.Max(scaleUpDuration, windUpDuration);
		
		// Create sequence: scale up + rotate + wind up (simultaneous)
		Sequence sequence = DOTween.Sequence();
		
		sequence.Append(
			physicalCard.transform.DOScale(originalScale * attackScaleMultiplier, scaleUpDuration)
				.SetEase(Ease.OutQuad)
		);
		
		sequence.Join(
			physicalCard.transform.DORotate(new Vector3(0, 0, targetAngle), rotationDuration)
				.SetEase(Ease.OutQuad)
		);
		
		sequence.Join(
			physicalCard.transform.DOMove(windUpPos, windUpDuration)
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
		
		chargeSequence.Append(
			physicalCard.transform.DOMove(targetPos, chargeDuration)
				.SetEase(Ease.InCubic)
		);
		
		chargeSequence.Join(
			physicalCard.transform.DOScale(chargeTargetScale, chargeDuration)
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

		physicalCard.transform.DOMove(overshootPos, bounceBackDuration * 0.5f)
			.SetEase(Ease.OutQuad)
			.OnComplete(() => completed = true);

		yield return new WaitUntil(() => completed);
	}

	/// <summary>
	/// Bounce-back animation (move back partway from overshoot), restore size simultaneously
	/// </summary>
	private IEnumerator BounceBackAnimation(GameObject physicalCard, Vector3 bounceBackPos, Vector3 originalScale)
	{
		bool completed = false;
		
		// Create sequence: bounce back position + restore scale (rotation kept, will rotate back at reveal)
		Sequence bounceSequence = DOTween.Sequence();
		
		bounceSequence.Append(
			physicalCard.transform.DOMove(bounceBackPos, bounceBackDuration)
				.SetEase(Ease.OutQuad)
		);
		
		bounceSequence.Join(
			physicalCard.transform.DOScale(originalScale, bounceBackDuration)
				.SetEase(Ease.OutQuad)
		);
		
		bounceSequence.OnComplete(() => completed = true);

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
		
		returnSequence.Append(
			physicalCard.transform.DOMove(revealPos, returnToRevealDuration)
				.SetEase(Ease.OutQuad)
		);
		
		returnSequence.Join(
			physicalCard.transform.DOScale(revealSize, returnToRevealDuration)
				.SetEase(Ease.OutQuad)
		);
		
		// Zero rotation, duration matches movement to ensure synchronous completion
		returnSequence.Join(
			physicalCard.transform.DORotate(Vector3.zero, returnToRevealDuration)
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
		
		returnSequence.Append(
			physicalCard.transform.DOMove(deckPos, returnToRevealDuration)
				.SetEase(Ease.OutQuad)
		);
		
		returnSequence.Join(
			physicalCard.transform.DOScale(originalScale, returnToRevealDuration)
				.SetEase(Ease.OutQuad)
		);
		
		returnSequence.Join(
			physicalCard.transform.DORotate(Vector3.zero, returnToRevealDuration)
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
		if (_combatManager != null && !IsAnyAnimationPlaying())
		{
			_combatManager.visuals.UnblockInput(this);
		}
	}

	/// <summary>
	/// Check if any animation is currently playing
	/// </summary>
	private bool IsAnyAnimationPlaying()
	{
		return isPlayingAttackAnimation || _attackQueue.Count > 0;
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
		
		// Restore deck focus if active
		if (_combatUXManager != null && _combatUXManager.IsDeckFocused)
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
