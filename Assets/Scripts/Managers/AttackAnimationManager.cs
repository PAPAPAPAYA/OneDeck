using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using MilkShake;
using UnityEngine;

/// <summary>
/// 攻击动画管理器 - 管理卡片攻击动画的队列播放
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
	[Tooltip("敌人位置（受到伤害的目标位置）")]
	public Transform enemyTargetPos;
	[Tooltip("玩家位置（受到伤害的目标位置）")]
	public Transform playerTargetPos;

	[Header("ANIMATION SETTINGS")]
	[Tooltip("攻击前放大倍数")]
	public float attackScaleMultiplier = 1.4f;
	[Tooltip("放大Animation duration")]
	public float scaleUpDuration = 0.2f;
	[Tooltip("冲撞Animation duration")]
	public float chargeDuration = 0.2f;
	[Tooltip("冲撞时缩小系数（相对于原始大小的比例）")]
	public float chargeScaleMultiplier = 0.85f;

	[Header("SCREEN SHAKE")]
	[Tooltip("相机Shaker组件（用于屏幕震动）")]
	public Shaker cameraShaker;
	[Tooltip("伤害结算时的屏幕震动预设")]
	public ShakePreset hitShakePreset;
	[Tooltip("Overshoot距离（冲过目标的距离）")]
	public float overshootDistance = 2.0f;
	[Tooltip("回弹Animation duration")]
	public float bounceBackDuration = 0.15f;

	[Tooltip("后退距离（蓄力距离）")]
	public float windUpDistance = 0.5f;
	[Tooltip("后退Animation duration")]
	public float windUpDuration = 0.1f;
	[Tooltip("在target pos的停顿时间")]
	public float pauseAtTarget = 0.05f;
	[Tooltip("返回Reveal位置的Animation duration")]
	public float returnToRevealDuration = 0.2f;

	[Header("STATE")]
	[Tooltip("是否正在播放攻击动画")]
	public bool isPlayingAttackAnimation = false;

	// 动画队列
	private Queue<AttackAnimData> _attackQueue = new();
	private bool _isProcessingQueue = false;

	private CombatManager _combatManager;
	private CombatUXManager _combatUXManager;

	void OnEnable()
	{
		_combatManager = CombatManager.Me;
		_combatUXManager = CombatUXManager.me;
	}

	void Start()
	{
		// 确保引用已初始化（应对 OnEnable 执行顺序问题）
		if (_combatManager == null)
			_combatManager = CombatManager.Me;
		if (_combatUXManager == null)
			_combatUXManager = CombatUXManager.me;
	}

	/// <summary>
	/// 请求播放攻击动画（加入队列）
	/// </summary>
	/// <param name="attackerCard">攻击卡片（逻辑卡）</param>
	/// <param name="isAttackingEnemy">true=攻击敌人, false=攻击自己</param>
	/// <param name="onHit">在Target Pos停顿（阶段4）时触发，用于伤害结算</param>
	/// <param name="onComplete">动画完全结束时触发</param>
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
	/// 处理动画队列
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
	}

	/// <summary>
	/// 播放单个攻击动画协程
	/// </summary>
	private IEnumerator PlayAttackAnimationCoroutine(AttackAnimData data)
	{
		isPlayingAttackAnimation = true;
		
		// Block player input
		if (_combatManager != null)
		{
			_combatManager.blockPlayerInput = true;
		}

		// 获取物理卡片
		CardScript cardScript = data.attackerLogicalCard.GetComponent<CardScript>();
		if (cardScript == null)
		{
			Debug.LogWarning("[AttackAnimationManager] Card has no CardScript");
			data.onComplete?.Invoke();
			isPlayingAttackAnimation = false;
			ReleasePlayerInput();
			yield break;
		}

		// 确保 _combatUXManager 已初始化
		if (_combatUXManager == null)
		{
			_combatUXManager = CombatUXManager.me;
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

		Vector3 startPos = physicalCard.transform.position;
		Vector3 targetPos = targetTransform.position;
		Vector3 originalScale = physicalCard.transform.localScale;

		// Mark that special animation is playing，防止 CardPhysObjScript 覆盖 DOTween
		physScript.isPlayingSpecialAnimation = true;

		try
		{
			// ========== 阶段1+2: 放大+旋转+后退蓄力（同时进行）==========
			Vector3 windUpPos = CalculateWindUpPosition(startPos, targetPos);
			yield return ScaleUpAndWindUpAnimation(physicalCard, originalScale, targetPos, windUpPos);

			// ========== 阶段3: 冲撞至Target Pos（敌人/玩家位置）Scale down simultaneously ==========
			yield return ChargeToTargetAnimation(physicalCard, targetPos, originalScale);

			// ========== 阶段4: 在Target Pos停顿，触发伤害结算和屏幕震动 ==========
			data.onHit?.Invoke();
			
			// 触发屏幕震动（只震动相机）
			if (cameraShaker != null && hitShakePreset != null)
			{
				cameraShaker.Shake(hitShakePreset);
			}
			
			yield return new WaitForSeconds(pauseAtTarget);

			// ========== 阶段5: 冲至Overshoot位置（冲过目标）==========
			Vector3 overshootPos = CalculateOvershootPosition(targetPos, startPos);
			yield return OvershootAnimation(physicalCard, overshootPos);

			// ========== 阶段6: 从Overshoot直接返回Reveal位置，同时恢复大小和旋转 ==========
			Vector3 revealPos = _combatUXManager.physicalCardRevealPos.position;
			Vector3 revealSize = _combatUXManager.physicalCardRevealSize;
			yield return ReturnToRevealFromOvershootAnimation(physicalCard, revealPos, revealSize, originalScale);
		}
		finally
		{
			// 确保特殊动画标记总是被恢复
			physScript.isPlayingSpecialAnimation = false;
			
			// 更新 CardPhysObjScript 的目标位置，防止跳变
			// 使用reveal位置作为目标
			Vector3 revealPos = _combatUXManager.physicalCardRevealPos.position;
			Vector3 revealSize = _combatUXManager.physicalCardRevealSize;
			physScript.SetTargetPosition(revealPos);
			physScript.SetTargetScale(revealSize);
		}

		// 标记Animation complete
		isPlayingAttackAnimation = false;
		
		// 恢复玩家输入
		ReleasePlayerInput();

		// 触发回调（让卡片继续去到底部）
		data.onComplete?.Invoke();
	}

	/// <summary>
	/// 放大+旋转+后退蓄力动画（同时进行）
	/// </summary>
	private IEnumerator ScaleUpAndWindUpAnimation(GameObject physicalCard, Vector3 originalScale, Vector3 targetPos, Vector3 windUpPos)
	{
		bool completed = false;
		
		// 计算朝向目标的角度
		float targetAngle = CalculateRotationTowardsTarget(physicalCard.transform.position, targetPos);
		
		// 使用较长的持续时间确保旋转覆盖整个过程
		float rotationDuration = Mathf.Max(scaleUpDuration, windUpDuration);
		
		// 创建序列：放大 + 旋转 + 后退（同时进行）
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
	/// 计算卡片朝向目标的角度（顶部指向目标）
	/// </summary>
	private float CalculateRotationTowardsTarget(Vector3 cardPos, Vector3 targetPos)
	{
		// 计算从卡片指向目标的方向
		Vector3 direction = targetPos - cardPos;
		
		// 使用 Atan2 计算角度（弧度转角度）
		// +90 是因为卡片默认顶部朝上，需要调整
		float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
		
		return angle;
	}

	/// <summary>
	/// 计算后退蓄力位置
	/// </summary>
	private Vector3 CalculateWindUpPosition(Vector3 startPos, Vector3 targetPos)
	{
		Vector3 direction = (targetPos - startPos).normalized;
		return startPos - direction * windUpDistance;
	}

	/// <summary>
	/// 计算Overshoot位置（从target继续向前冲）
	/// </summary>
	private Vector3 CalculateOvershootPosition(Vector3 targetPos, Vector3 startPos)
	{
		Vector3 direction = (targetPos - startPos).normalized;
		return targetPos + direction * overshootDistance;
	}

	/// <summary>
	/// 冲撞至Target Pos，Scale down simultaneously
	/// </summary>
	private IEnumerator ChargeToTargetAnimation(GameObject physicalCard, Vector3 targetPos, Vector3 originalScale)
	{
		bool completed = false;

		// 计算冲撞时的缩小目标
		Vector3 chargeTargetScale = originalScale * chargeScaleMultiplier;

		// 创建序列：移动 + 缩小
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
	/// 从Target Pos冲至Overshoot位置
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
	/// 回弹动画（从Overshoot往回走一部分），同时恢复大小
	/// </summary>
	private IEnumerator BounceBackAnimation(GameObject physicalCard, Vector3 bounceBackPos, Vector3 originalScale)
	{
		bool completed = false;
		
		// 创建序列：回弹位置 + 恢复缩放（旋转保持，到reveal时再旋转）
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
	/// 从Overshoot位置返回Reveal位置的动画，同时恢复大小和旋转归零
	/// </summary>
	private IEnumerator ReturnToRevealFromOvershootAnimation(GameObject physicalCard, Vector3 revealPos, Vector3 revealSize, Vector3 originalScale)
	{
		bool completed = false;
		
		// 创建序列：移动到reveal位置 + 恢复reveal大小 + 旋转归零（与移动同步）
		Sequence returnSequence = DOTween.Sequence();
		
		returnSequence.Append(
			physicalCard.transform.DOMove(revealPos, returnToRevealDuration)
				.SetEase(Ease.OutQuad)
		);
		
		returnSequence.Join(
			physicalCard.transform.DOScale(revealSize, returnToRevealDuration)
				.SetEase(Ease.OutQuad)
		);
		
		// 旋转归零，持续时间和移动一致，确保同步完成
		returnSequence.Join(
			physicalCard.transform.DORotate(Vector3.zero, returnToRevealDuration)
				.SetEase(Ease.OutQuad)
		);
		
		returnSequence.OnComplete(() => completed = true);

		yield return new WaitUntil(() => completed);
	}

	/// <summary>
	/// 释放玩家输入
	/// </summary>
	private void ReleasePlayerInput()
	{
		if (_combatManager != null && !IsAnyAnimationPlaying())
		{
			_combatManager.blockPlayerInput = false;
		}
	}

	/// <summary>
	/// 检查是否有任何动画正在播放
	/// </summary>
	private bool IsAnyAnimationPlaying()
	{
		return isPlayingAttackAnimation || _attackQueue.Count > 0;
	}

	/// <summary>
	/// Stop all attack animations（用于战斗结束或阶段切换）
	/// </summary>
	public void StopAllAttackAnimations()
	{
		StopAllCoroutines();
		_attackQueue.Clear();
		_isProcessingQueue = false;
		isPlayingAttackAnimation = false;
		
		// 恢复玩家输入
		if (_combatManager != null)
		{
			_combatManager.blockPlayerInput = false;
		}
	}

	/// <summary>
	/// 检查Whether there are pending attack animations
	/// </summary>
	public bool HasPendingAnimations()
	{
		return _attackQueue.Count > 0 || isPlayingAttackAnimation;
	}
}

/// <summary>
/// 攻击动画数据
/// </summary>
public struct AttackAnimData
{
	public GameObject attackerLogicalCard;
	public bool isAttackingEnemy; // true=攻击敌人, false=攻击自己
	public Action onHit; // 在Target Pos停顿（阶段4）时触发，用于伤害结算
	public Action onComplete; // 动画完全结束时触发
}
