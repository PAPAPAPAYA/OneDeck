using System;
using DG.Tweening;
using MilkShake;
using TMPro;
using UnityEngine;

public class CardPhysObjScript : MonoBehaviour
{
	public bool isPhysicalStartCard;
	public CardScript cardImRepresenting;
	private CombatUXManager _combatUXManager;

	[Header("Phase Ref")]
	[SerializeField] private GamePhaseSO currentGamePhaseRef;

	[Header("Shop Settings")]
	[Tooltip("商店物品索引，-1表示不是商店物品")]
	public int shopItemIndex = -1;
	[Tooltip("长按购买所需时间（秒）")]
	public float holdTimeRequired = 0.5f;

	[Header("Shake Settings")]
	[Tooltip("子物体上的Shaker组件")]
	[SerializeField] private Shaker cardShaker;
	[Tooltip("震动预设")]
	[SerializeField] private ShakePreset cardShakePreset;

	[Header("LOOK")]
	public SpriteRenderer cardFace;
	public SpriteRenderer cardEdge;
	public TextMeshPro cardNamePrint;
	public TextMeshPro cardDescPrint;
	public TextMeshPro cardPricePrint;

	[Header("COLOR")]
	public Color ownerCardColor;
	public Color ownerCardEdgeColor;
	public Color opponentCardColor;
	public Color opponentCardEdgeColor;

	// ========== 动画目标位置 ==========
	[Header("ANIMATION")]
	public Vector3 TargetPosition { get; private set; }
	public Vector3 TargetScale { get; private set; }

	// ========== 长按购买相关 ==========
	private bool _isHolding = false;
	private float _holdTimer = 0f;

	// ========== 震动相关 ==========
	private ShakeInstance _currentShakeInstance;
	private bool _isShaking = false;

	// ========== 卡片放大相关 ==========
	private Vector3 _originalPosition;
	private Vector3 _originalScale;
	private bool _isEnlarged = false;
	private bool _hasClickProcessed = false; // 防止单击和长按冲突
	private float _enlargeCooldown = 0f; // 放大冷却时间
	private const float ENLARGE_COOLDOWN_TIME = 0.5f; // 冷却时间（秒）

	// ========== Stage/Bury 特殊动画 ==========
	[Header("Stage/Bury Animation")]
	[Tooltip("是否正在播放特殊动画")]
	public bool isPlayingSpecialAnimation = false;
	[Tooltip("左移距离")]
	public float sideOffset = 2.5f;
	[Tooltip("左移动画持续时间")]
	public float sideMoveDuration = 0.3f;
	[Tooltip("插入动画持续时间")]
	public float insertDuration = 0.4f;
	[Tooltip("动画期间的额外缩放倍数")]
	public float animationScaleMultiplier = 1.3f;
	[Tooltip("动画期间的旋转角度")]
	public float animationRotationAngle = 20f;

	private Tween _currentSpecialTween;
	private Vector3 _specialAnimOriginalScale;
	private Vector3 _specialAnimOriginalRotation;

	// ========== 卡组整体动画（用于呼吸效果）==========
	[Header("Deck Group Animation")]
	[Tooltip("是否是正在被移动的卡片（主角卡片）")]
	public bool isMainAnimationCard = false;
	[Tooltip("卡组缩小的倍数")]
	public float deckShrinkMultiplier = 0.85f;
	[Tooltip("卡组右移距离")]
	public float deckRightOffset = 1.5f;
	[Tooltip("卡组动画持续时间")]
	public float deckAnimDuration = 0.35f;

	private Tween _currentDeckGroupTween;
	private Vector3 _deckAnimBasePosition;
	private Vector3 _deckAnimBaseScale;
	private bool _isInDeckGroupAnimation = false;

	// ========== DOTween 动画 ==========
	[Header("DOTween Animation")]
	[Tooltip("移动到目标位置的动画持续时间")]
	public float moveDuration = 0.3f;
	[Tooltip("移动动画的缓动类型")]
	public Ease moveEase = Ease.OutQuad;
	
	private Tweener _positionTween;
	private Tweener _scaleTween;

	void OnEnable()
	{
		_combatUXManager = CombatUXManager.me;
	}

	void Update()
	{
		ApplyColor();
		UpdateMotion(); // 在 Update 中处理动画
		UpdateStatusEffectDisplay();
		UpdatePriceDisplay();

		// 长按检测
		HandleHoldToBuy();

		// 检测再次点击恢复卡片
		HandleClickToRestore();

		// 更新冷却时间
		if (_enlargeCooldown > 0)
		{
			_enlargeCooldown -= Time.deltaTime;
		}
	}

	/// <summary>
	/// 检测再次点击恢复卡片
	/// </summary>
	private void HandleClickToRestore()
	{
		if (!_isEnlarged) return;

		// 如果点击了鼠标左键，恢复卡片
		if (Input.GetMouseButtonDown(0))
		{
			RestoreCard();
			// 设置冷却时间，防止立即再次放大
			_enlargeCooldown = ENLARGE_COOLDOWN_TIME;
		}
	}

	/// <summary>
	/// 更新价格显示，仅在 Shop Phase 显示
	/// </summary>
	private void UpdatePriceDisplay()
	{
		// 如果没有价格文本组件，直接返回
		if (cardPricePrint == null) return;

		// 如果不是 Shop Phase，隐藏价格显示
		if (currentGamePhaseRef == null || currentGamePhaseRef.Value() != EnumStorage.GamePhase.Shop)
		{
			cardPricePrint.gameObject.SetActive(false);
			return;
		}

		// 如果卡牌数据为空，隐藏价格显示
		if (cardImRepresenting == null)
		{
			cardPricePrint.gameObject.SetActive(false);
			return;
		}

		// 显示价格
		cardPricePrint.gameObject.SetActive(true);

		// 商店卡片显示原价，玩家卡组中的卡片价格除以2
		int displayPrice = shopItemIndex >= 0 ? cardImRepresenting.price.value : cardImRepresenting.price.value / 2;
		cardPricePrint.text = $"<color=yellow>${displayPrice}</color>";
	}

	/// <summary>
	/// 处理长按购买/卖出逻辑
	/// </summary>
	private void HandleHoldToBuy()
	{
		// 只有在 Shop Phase 才检测
		if (currentGamePhaseRef == null || currentGamePhaseRef.Value() != EnumStorage.GamePhase.Shop)
			return;

		if (_isHolding)
		{
			_holdTimer += Time.deltaTime;

			// 达到长按时间，触发购买或卖出
			if (_holdTimer >= holdTimeRequired)
			{
				if (shopItemIndex >= 0)
				{
					// 商店物品：购买
					TryPurchase();
				}
				else if (shopItemIndex == -1)
				{
					// 玩家卡组中的卡片：卖出
					TrySell();
				}
				_isHolding = false;
				_holdTimer = 0f;
			}
		}
	}

	/// <summary>
	/// 尝试购买此卡片
	/// </summary>
	private void TryPurchase()
	{
		if (ShopManager.me != null)
		{
			ShopManager.me.BuyFunc(shopItemIndex);
		}
	}

	/// <summary>
	/// 尝试卖出此卡片
	/// </summary>
	private void TrySell()
	{
		if (ShopManager.me == null || cardImRepresenting == null) return;

		// 获取此卡片在玩家卡组中的索引
		int cardIndex = GetPlayerCardIndex();
		if (cardIndex >= 0)
		{
			ShopManager.me.SellFunc(cardIndex, this.gameObject);
		}
	}

	/// <summary>
	/// 获取此卡片在玩家卡组中的索引
	/// </summary>
	private int GetPlayerCardIndex()
	{
		if (ShopManager.me == null || cardImRepresenting == null) return -1;

		var playerDeck = ShopManager.me.playerDeckRef;
		if (playerDeck == null || playerDeck.deck == null) return -1;

		for (int i = 0; i < playerDeck.deck.Count; i++)
		{
			if (playerDeck.deck[i] == cardImRepresenting.gameObject)
			{
				return i;
			}
		}
		return -1;
	}

	private void UpdateStatusEffectDisplay()
	{
		if (cardImRepresenting == null || cardNamePrint == null) return;

		var statusEffectText = CombatInfoDisplayer.me?.ProcessStatusEffectInfo(cardImRepresenting);
		if (!string.IsNullOrEmpty(statusEffectText))
		{
			cardNamePrint.text = $"<size=12>{statusEffectText}\n</size><b>{cardImRepresenting.gameObject.name}</b>";
		}
		else
		{
			cardNamePrint.text = cardImRepresenting.gameObject.name;
		}
	}

	/// <summary>
	/// 设置目标位置（由 CombatUXManager 调用），使用 DOTween 动画
	/// </summary>
	public void SetTargetPosition(Vector3 target)
	{
		TargetPosition = target;
		
		// 如果正在播放特殊动画或卡组整体动画，不启动 DOTween
		if (isPlayingSpecialAnimation || _isInDeckGroupAnimation) return;
		
		// 启动 DOTween 位置动画
		StartPositionTween();
	}

	/// <summary>
	/// 设置目标缩放（由 CombatUXManager 调用），使用 DOTween 动画
	/// </summary>
	public void SetTargetScale(Vector3 target)
	{
		TargetScale = target;
		
		// 如果正在播放特殊动画或卡组整体动画，不启动 DOTween
		if (isPlayingSpecialAnimation || _isInDeckGroupAnimation) return;
		
		// 启动 DOTween 缩放动画
		StartScaleTween();
	}

	/// <summary>
	/// 启动位置 DOTween 动画
	/// </summary>
	private void StartPositionTween()
	{
		// 如果已经在动画中且目标相同，不重复启动
		if (_positionTween != null && _positionTween.IsActive() && _positionTween.IsPlaying())
		{
			// 检查当前动画的目标是否已经是 TargetPosition
			// DOTween 没有直接获取目标的方法，所以直接 Kill 并重新开始
			_positionTween.Kill();
		}
		
		_positionTween = transform.DOMove(TargetPosition, moveDuration)
			.SetEase(moveEase)
			.SetUpdate(UpdateType.Normal, true);
	}

	/// <summary>
	/// 启动缩放 DOTween 动画
	/// </summary>
	private void StartScaleTween()
	{
		if (_scaleTween != null && _scaleTween.IsActive() && _scaleTween.IsPlaying())
		{
			_scaleTween.Kill();
		}
		
		_scaleTween = transform.DOScale(TargetScale, moveDuration)
			.SetEase(moveEase)
			.SetUpdate(UpdateType.Normal, true);
	}

	/// <summary>
	/// 立即设置位置（无动画）
	/// </summary>
	public void SetPositionImmediate(Vector3 position)
	{
		// 停止正在进行的 DOTween 位置动画
		if (_positionTween != null && _positionTween.IsActive())
		{
			_positionTween.Kill();
			_positionTween = null;
		}
		
		TargetPosition = position;
		transform.position = position;
	}

	/// <summary>
	/// 立即设置缩放（无动画）
	/// </summary>
	public void SetScaleImmediate(Vector3 scale)
	{
		// 停止正在进行的 DOTween 缩放动画
		if (_scaleTween != null && _scaleTween.IsActive())
		{
			_scaleTween.Kill();
			_scaleTween = null;
		}
		
		TargetScale = scale;
		transform.localScale = scale;
	}

	#region Stage/Bury 特殊动画（主角卡片动画）

	/// <summary>
	/// 播放主角卡片阶段1动画：左移 + 放大 + 旋转
	/// 用于 Stage/Bury 操作的第一阶段
	/// </summary>
	/// <param name="onComplete">动画完成回调</param>
	public void PlayMainCardPhase1(TweenCallback onComplete = null)
	{
		// 如果正在播放特殊动画，先停止
		_currentSpecialTween?.Kill();

		isPlayingSpecialAnimation = true;
		isMainAnimationCard = true;

		// 保存原始缩放和旋转
		_specialAnimOriginalScale = transform.localScale;
		_specialAnimOriginalRotation = transform.eulerAngles;

		// 计算左侧中间位置（卡组左边）
		Vector3 sidePosition = new Vector3(
		    transform.position.x - sideOffset,
		    transform.position.y,
		    transform.position.z
		);

		// 创建动画序列
		Sequence sequence = DOTween.Sequence();

		// 阶段一：左移 + 旋转 + 放大（并行执行）
		sequence.Append(
		    transform.DOMove(sidePosition, sideMoveDuration)
			.SetEase(Ease.OutQuad)
		);
		sequence.Join(
		    transform.DORotate(new Vector3(0, 0, animationRotationAngle), sideMoveDuration)
			.SetEase(Ease.OutQuad)
		);
		sequence.Join(
		    transform.DOScale(_specialAnimOriginalScale * animationScaleMultiplier, sideMoveDuration)
			.SetEase(Ease.OutQuad)
		);

		// 动画完成回调
		sequence.OnComplete(() =>
		{
			onComplete?.Invoke();
		});

		_currentSpecialTween = sequence;
		sequence.Play();
	}

	/// <summary>
	/// 播放主角卡片阶段3动画：插入到目标位置 + 恢复
	/// 用于 Stage/Bury 操作的第三阶段（与卡组恢复同时进行）
	/// </summary>
	/// <param name="finalTarget">最终目标位置</param>
	/// <param name="onComplete">动画完成回调</param>
	public void PlayMainCardPhase3(Vector3 finalTarget, TweenCallback onComplete = null)
	{
		// 创建插入动画
		Sequence sequence = DOTween.Sequence();

		// 插入到最终位置 + 恢复旋转 + 恢复缩放
		sequence.Append(
		    transform.DOMove(finalTarget, insertDuration)
			.SetEase(Ease.InOutCubic)
		);
		sequence.Join(
		    transform.DORotate(_specialAnimOriginalRotation, insertDuration)
			.SetEase(Ease.InOutCubic)
		);
		sequence.Join(
		    transform.DOScale(TargetScale, insertDuration)
			.SetEase(Ease.InOutCubic)
		);

		// 动画完成回调
		sequence.OnComplete(() =>
		{
			isPlayingSpecialAnimation = false;
			isMainAnimationCard = false;
			// 同步 TargetPosition，防止 DOTween 动画完成后跳变
			TargetPosition = finalTarget;
			// 确保最终状态正确
			transform.eulerAngles = _specialAnimOriginalRotation;
			onComplete?.Invoke();
		});

		_currentSpecialTween = sequence;
		sequence.Play();
	}

	/// <summary>
	/// 停止特殊动画（用于战斗阶段切换或洗牌时中断）
	/// </summary>
	public void StopSpecialAnimation()
	{
		if (_currentSpecialTween != null && _currentSpecialTween.IsActive())
		{
			_currentSpecialTween.Kill(complete: false); // 不完成动画，直接停止
			_currentSpecialTween = null;
		}

		if (isPlayingSpecialAnimation)
		{
			isPlayingSpecialAnimation = false;
			isMainAnimationCard = false;
			// 恢复到原始状态
			if (_specialAnimOriginalScale != Vector3.zero)
			{
				transform.localScale = _specialAnimOriginalScale;
				transform.eulerAngles = _specialAnimOriginalRotation;
			}
		}

		// 同时停止卡组整体动画
		StopDeckGroupAnimation();
	}

	#endregion

	#region 卡组整体动画（呼吸效果）

	/// <summary>
	/// 播放卡组整体动画（缩小 + 右移）
	/// 用于当其他卡片被 stage/bury 时，卡组给让出空间
	/// </summary>
	/// <param name="basePosition">卡组基准位置（动画结束后要恢复的位置）</param>
	public void PlayDeckGroupShrinkAnimation(Vector3 basePosition)
	{
		// 停止之前的卡组动画
		_currentDeckGroupTween?.Kill();

		_isInDeckGroupAnimation = true;
		_deckAnimBasePosition = basePosition;
		_deckAnimBaseScale = TargetScale;

		// 计算右移后的位置
		Vector3 rightPosition = new Vector3(
		    basePosition.x + deckRightOffset,
		    basePosition.y,
		    basePosition.z
		);

		// 创建动画序列
		Sequence sequence = DOTween.Sequence();

		// 1. 阶段一：右移 + 缩小
		sequence.Append(
		    transform.DOMove(rightPosition, deckAnimDuration)
			.SetEase(Ease.OutQuad)
		);
		sequence.Join(
		    transform.DOScale(TargetScale * deckShrinkMultiplier, deckAnimDuration)
			.SetEase(Ease.OutQuad)
		);

		// 动画完成（此时保持缩小状态，等待恢复指令）
		sequence.OnComplete(() =>
		{
			// 不标记动画结束，保持状态直到恢复
		});

		_currentDeckGroupTween = sequence;
		sequence.Play();
	}

	/// <summary>
	/// 恢复卡组整体动画（恢复到原始大小和位置）
	/// </summary>
	public void PlayDeckGroupRestoreAnimation()
	{
		if (!_isInDeckGroupAnimation) return;

		_currentDeckGroupTween?.Kill();

		// 创建恢复动画
		Sequence sequence = DOTween.Sequence();

		sequence.Append(
		    transform.DOMove(_deckAnimBasePosition, insertDuration)
			.SetEase(Ease.InOutCubic)
		);
		sequence.Join(
		    transform.DOScale(_deckAnimBaseScale, insertDuration)
			.SetEase(Ease.InOutCubic)
		);

		sequence.OnComplete(() =>
		{
			_isInDeckGroupAnimation = false;
			// 同步 TargetPosition
			TargetPosition = _deckAnimBasePosition;
		});

		_currentDeckGroupTween = sequence;
		sequence.Play();
	}

	/// <summary>
	/// 停止卡组整体动画
	/// </summary>
	public void StopDeckGroupAnimation()
	{
		if (_currentDeckGroupTween != null && _currentDeckGroupTween.IsActive())
		{
			_currentDeckGroupTween.Kill(complete: false);
			_currentDeckGroupTween = null;
		}

		if (_isInDeckGroupAnimation)
		{
			_isInDeckGroupAnimation = false;
			// 恢复状态
			if (_deckAnimBaseScale != Vector3.zero)
			{
				transform.localScale = _deckAnimBaseScale;
			}
		}
	}

	/// <summary>
	/// 检查是否正在播放卡组整体动画
	/// </summary>
	public bool IsInDeckGroupAnimation()
	{
		return _isInDeckGroupAnimation;
	}

	#endregion

	/// <summary>
	/// 在 Update 中处理动画相关逻辑
	/// 注意：现在位置/缩放动画由 DOTween 处理，此方法只处理特殊逻辑
	/// </summary>
	private void UpdateMotion()
	{
		// 如果正在播放特殊动画或卡组整体动画，停止常规 DOTween 动画
		if (isPlayingSpecialAnimation || _isInDeckGroupAnimation)
		{
			_positionTween?.Kill();
			_scaleTween?.Kill();
			_positionTween = null;
			_scaleTween = null;
			return;
		}
		
		// DOTween 自动处理动画，这里不需要额外的 Lerp
	}

	private void ApplyColor()
	{
		if (isPhysicalStartCard) return;
		// Start Card 没有 cardImRepresenting，保持默认颜色或特殊处理
		if (cardImRepresenting == null)
		{
			// Start Card 可以设置一个特殊颜色，或者保持原样
			return;
		}

		// myStatusRef 为空时，使用 ownerCardColor
		if (cardImRepresenting.myStatusRef == null)
		{
			cardEdge.color = ownerCardEdgeColor;
			cardFace.color = ownerCardColor;
		}
		else if (cardImRepresenting.myStatusRef != CombatManager.Me?.ownerPlayerStatusRef)
		{
			cardEdge.color = opponentCardEdgeColor;
			cardFace.color = opponentCardColor;
		}
		else
		{
			cardEdge.color = ownerCardEdgeColor;
			cardFace.color = ownerCardColor;
		}
	}

	private void OnMouseDown()
	{
		// 检查是否在 Shop Phase
		if (currentGamePhaseRef != null && currentGamePhaseRef.Value() == EnumStorage.GamePhase.Shop)
		{
			// 开始长按检测（商店物品和玩家卡组中的卡片都可以）
			_isHolding = true;
			_holdTimer = 0f;
			_hasClickProcessed = false;

			// 开始震动
			StartCardShake();
		}
	}

	private void OnMouseUp()
	{
		// 如果正在长按且未达到购买时间，视为单击，触发放大
		if (_isHolding && _holdTimer < holdTimeRequired && !_hasClickProcessed)
		{
			//if (shopItemIndex >= 0)
			{
				EnlargeCard();
				_hasClickProcessed = true;
			}
		}

		// 取消长按
		_isHolding = false;
		_holdTimer = 0f;

		// 停止震动
		StopCardShake();
	}

	/// <summary>
	/// 放大卡片
	/// </summary>
	private void EnlargeCard()
	{
		// 检查冷却时间 - 防止 restore 后立即 enlarge
		if (_enlargeCooldown > 0) return;

		// 保存原始位置和缩放
		_originalPosition = TargetPosition;
		_originalScale = TargetScale;

		// 获取 ShopUXManager 中的放大设置
		if (ShopUXManager.Instance != null)
		{
			float enlargeSize = ShopUXManager.Instance.physCardEnlargeSize;
			SetTargetScale(new Vector3(enlargeSize, enlargeSize, enlargeSize));
			SetTargetPosition(ShopUXManager.Instance.enlargedPosition);
		}
		else
		{
			SetTargetScale(new Vector3(2f, 2f, 2f)); // 默认放大倍数
			SetTargetPosition(Vector3.zero); // 默认位置
		}

		_isEnlarged = true;
		Debug.Log($"[CardPhysObjScript] Card enlarged: {cardImRepresenting?.gameObject.name}");
	}

	/// <summary>
	/// 恢复卡片到原始状态
	/// </summary>
	public void RestoreCard()
	{
		if (!_isEnlarged) return;

		// 恢复到原始位置和缩放
		SetTargetPosition(_originalPosition);
		SetTargetScale(_originalScale);

		_isEnlarged = false;
		Debug.Log($"[CardPhysObjScript] Card restored: {cardImRepresenting?.gameObject.name}");
	}

	/// <summary>
	/// 获取卡片是否处于放大状态
	/// </summary>
	public bool IsEnlarged()
	{
		return _isEnlarged;
	}

	private void OnMouseExit()
	{
		// 鼠标移出，取消长按
		_isHolding = false;
		_holdTimer = 0f;

		// 停止震动
		StopCardShake();
	}

	/// <summary>
	/// 开始卡片震动
	/// </summary>
	private void StartCardShake()
	{
		if (cardShaker == null || cardShakePreset == null || _isShaking) return;

		_currentShakeInstance = cardShaker.Shake(cardShakePreset);
		_isShaking = true;
	}

	/// <summary>
	/// 停止卡片震动
	/// </summary>
	private void StopCardShake()
	{
		if (!_isShaking || _currentShakeInstance == null) return;

		// 停止震动，使用预设的fadeOut时间
		_currentShakeInstance.Stop(cardShakePreset.FadeOut, true);
		_isShaking = false;
		_currentShakeInstance = null;
	}
}
