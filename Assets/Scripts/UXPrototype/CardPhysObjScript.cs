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
	[Tooltip("Shop item index, -1 means not a shop item")]
	public int shopItemIndex = -1;
	[Tooltip("Long press duration required to purchase (seconds)")]
	public float holdTimeRequired = 0.5f;

	[Header("Shake Settings")]
	[Tooltip("Shaker component on child object")]
	[SerializeField] private Shaker cardShaker;
	[Tooltip("Shake preset")]
	[SerializeField] private ShakePreset cardShakePreset;

	[Header("LOOK")]
	public SpriteRenderer cardFace;
	public SpriteRenderer cardEdge;
	public TextMeshPro cardCostPrint;
	public TextMeshPro cardNamePrint;
	public TextMeshPro cardDescPrint;
	public TextMeshPro cardPricePrint;
	public TextMeshPro cardRarityPrint;
	public TextMeshPro cardTagPrint;

	[Header("COLOR")]
	public Color ownerCardColor;
	public Color ownerCardEdgeColor;
	public Color opponentCardColor;
	public Color opponentCardEdgeColor;

	[Header("TINT - Infected")]
	[Tooltip("Tint color for Infected state")]
	public Color infectedTintColor = new Color(0.4f, 0.8f, 0.2f);
	[Tooltip("Tint intensity for Infected state")]
	[Range(0f, 1f)]
	public float infectedTintIntensity = 0.5f;

	[Header("TINT - Power")]
	[Tooltip("Tint color for Power state")]
	public Color powerTintColor = new Color(1f, 0.6f, 0.1f);
	[Tooltip("Tint intensity for Power state")]
	[Range(0f, 1f)]
	public float powerTintIntensity = 0.5f;

	[Header("TINT - Settings")]
	[Tooltip("Tint duration (seconds)")]
	public float tintDuration = 1.5f;
	[Tooltip("Tint color transition speed (higher is faster)")]
	public float tintTransitionSpeed = 5f;

	// Runtime state
	private TintState _currentTintState = TintState.None;
	private float _tintTimer = 0f;
	private float _currentTintIntensity = 0f; // Currently displayed tint intensity (used for smooth transition)

	public enum TintState { None, Infected, Power }

	// ========== Animation target position ==========
	[Header("ANIMATION")]
	public Vector3 TargetPosition { get; private set; }
	public Vector3 TargetScale { get; private set; }

	// ========== Long press purchase related ==========
	private bool _isHolding = false;
	private float _holdTimer = 0f;

	// ========== Shake related ==========
	private ShakeInstance _currentShakeInstance;
	private bool _isShaking = false;

	// ========== Card enlarge related ==========
	private Vector3 _originalPosition;
	private Vector3 _originalScale;
	private bool _isEnlarged = false;
	private bool _hasClickProcessed = false; // Prevent click and long press conflict
	private float _enlargeCooldown = 0f; // Enlarge cooldown time
	private const float ENLARGE_COOLDOWN_TIME = 0.5f; // Cooldown time (seconds)

	// ========== Stage/Bury special animation ==========
	[Header("Stage/Bury Animation")]
	[Tooltip("Is playing special animation")]
	public bool isPlayingSpecialAnimation = false;
	[Tooltip("Left move distance")]
	public float sideOffset = 2.5f;
	[Tooltip("Left move animation duration")]
	public float sideMoveDuration = 0.3f;
	[Tooltip("Insert animation duration")]
	public float insertDuration = 0.4f;
	[Tooltip("Extra scale multiplier during animation")]
	public float animationScaleMultiplier = 1.3f;
	[Tooltip("Rotation angle during animation")]
	public float animationRotationAngle = 20f;

	private Tween _currentSpecialTween;
	private Vector3 _specialAnimOriginalScale;
	private Vector3 _specialAnimOriginalRotation;

	// ========== Deck group animation (used for breathing effect) ==========
	[Header("Deck Group Animation")]
	[Tooltip("Whether this is the card being moved (main card)")]
	public bool isMainAnimationCard = false;
	[Tooltip("Deck shrink multiplier")]
	public float deckShrinkMultiplier = 0.85f;
	[Tooltip("Deck right move distance")]
	public float deckRightOffset = 1.5f;
	[Tooltip("Deck animation duration")]
	public float deckAnimDuration = 0.35f;

	private Tween _currentDeckGroupTween;
	private Vector3 _deckAnimBasePosition;
	private Vector3 _deckAnimBaseScale;
	private bool _isInDeckGroupAnimation = false;

	// ========== DOTween animation ==========
	[Header("DOTween Animation")]
	[Tooltip("Animation duration to move to target position")]
	public float moveDuration = 0.3f;
	[Tooltip("Ease type for move animation")]
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
		UpdateMotion(); // Handle animation in Update
		UpdateStatusEffectDisplay();
		UpdatePriceDisplay();
		UpdateCostDisplay();
		UpdateRarityDisplay();
		UpdateTagDisplay();
		UpdateTintTimer();

		// Long press detection
		HandleHoldToBuy();

		// Detect click again to restore card
		HandleClickToRestore();

		// Update cooldown time
		if (_enlargeCooldown > 0)
		{
			_enlargeCooldown -= Time.deltaTime;
		}
	}

	/// <summary>
	/// Detect click again to restore card
	/// </summary>
	private void HandleClickToRestore()
	{
		if (!_isEnlarged) return;

		// If left mouse button is clicked, restore card
		if (Input.GetMouseButtonDown(0))
		{
			RestoreCard();
			// Set cooldown to prevent immediate re-enlarge
			_enlargeCooldown = ENLARGE_COOLDOWN_TIME;
		}
	}

	/// <summary>
	/// Update price display, only shown in Shop Phase
	/// </summary>
	private void UpdatePriceDisplay()
	{
		// If no price text component, return directly
		if (cardPricePrint == null) return;

		// If not Shop Phase, hide price display
		if (currentGamePhaseRef == null || currentGamePhaseRef.Value() != EnumStorage.GamePhase.Shop)
		{
			cardPricePrint.gameObject.SetActive(false);
			return;
		}

		// If card data is null, hide price display
		if (cardImRepresenting == null)
		{
			cardPricePrint.gameObject.SetActive(false);
			return;
		}

		// Show price
		cardPricePrint.gameObject.SetActive(true);

		// Shop cards show original price, player deck cards show half price
		int displayPrice = shopItemIndex >= 0 ? cardImRepresenting.price.value : cardImRepresenting.price.value / 2;
		cardPricePrint.text = $"<color=yellow>${displayPrice}</color>";
	}

	/// <summary>
	/// Update Cost display
	/// </summary>
	private void UpdateCostDisplay()
	{
		// Hide cost display
		if (cardCostPrint != null)
			cardCostPrint.gameObject.SetActive(false);
	}

	/// <summary>
	/// Update Tag display, tags wrapped in brackets separated by spaces
	/// </summary>
	private void UpdateTagDisplay()
	{
		if (cardTagPrint == null || cardImRepresenting == null) return;

		if (cardImRepresenting.myTags == null || cardImRepresenting.myTags.Count == 0)
		{
			cardTagPrint.gameObject.SetActive(false);
			return;
		}

		System.Text.StringBuilder sb = new System.Text.StringBuilder();
		bool hasVisibleTag = false;
		for (int i = 0; i < cardImRepresenting.myTags.Count; i++)
		{
			EnumStorage.Tag tag = cardImRepresenting.myTags[i];
			if (tag == EnumStorage.Tag.None) continue;
			if (hasVisibleTag)
			{
				sb.Append(" ");
			}
			sb.Append("[");
			sb.Append(tag.ToString());
			sb.Append("]");
			hasVisibleTag = true;
		}

		if (hasVisibleTag)
		{
			cardTagPrint.gameObject.SetActive(true);
			cardTagPrint.text = sb.ToString();
		}
		else
		{
			cardTagPrint.gameObject.SetActive(false);
		}
	}

	/// <summary>
	/// Update Rarity display using star count
	/// </summary>
	private void UpdateRarityDisplay()
	{
		if (cardRarityPrint == null || cardImRepresenting == null) return;

		int starCount;
		switch (cardImRepresenting.rarity)
		{
			case EnumStorage.Rarity.Common:
				starCount = 1;
				break;
			case EnumStorage.Rarity.Uncommon:
				starCount = 2;
				break;
			case EnumStorage.Rarity.Rare:
				starCount = 3;
				break;
			default:
				starCount = 1;
				break;
		}

		cardRarityPrint.text = new string('*', starCount);
	}

	/// <summary>
	/// Handle long press buy/sell logic
	/// </summary>
	private void HandleHoldToBuy()
	{
		// Only detect in Shop Phase
		if (currentGamePhaseRef == null || currentGamePhaseRef.Value() != EnumStorage.GamePhase.Shop)
			return;

		if (_isHolding)
		{
			_holdTimer += Time.deltaTime;

			// Long press time reached, trigger purchase or sell
			if (_holdTimer >= holdTimeRequired)
			{
				if (shopItemIndex >= 0)
				{
					// Shop item: purchase
					TryPurchase();
				}
				else if (shopItemIndex == -1)
				{
					// Player deck card: sell
					TrySell();
				}
				_isHolding = false;
				_holdTimer = 0f;
			}
		}
	}

	/// <summary>
	/// Try to purchase this card
	/// </summary>
	private void TryPurchase()
	{
		if (ShopManager.me != null)
		{
			ShopManager.me.BuyFunc(shopItemIndex);
		}
	}

	/// <summary>
	/// Try to sell this card
	/// </summary>
	private void TrySell()
	{
		if (ShopManager.me == null || cardImRepresenting == null) return;

		// Get the index of this card in player deck
		int cardIndex = GetPlayerCardIndex();
		if (cardIndex >= 0)
		{
			ShopManager.me.SellFunc(cardIndex, this.gameObject);
		}
	}

	/// <summary>
	/// Get the index of this card in player deck
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
			cardNamePrint.text = $"<size=12>{statusEffectText}\n</size><b>{cardImRepresenting.GetDisplayName()}</b>";
		}
		else
		{
			cardNamePrint.text = cardImRepresenting.GetDisplayName();
		}
	}


	/// <summary>
	/// Set target position (called by CombatUXManager), uses DOTween animation
	/// </summary>
	public void SetTargetPosition(Vector3 target)
	{
		TargetPosition = target;

		// If special animation or deck group animation is playing, do not start DOTween
		if (isPlayingSpecialAnimation || _isInDeckGroupAnimation) return;

		// Start DOTween position animation
		StartPositionTween();
	}

	/// <summary>
	/// Set target scale (called by CombatUXManager), uses DOTween animation
	/// </summary>
	public void SetTargetScale(Vector3 target)
	{
		TargetScale = target;

		// If special animation or deck group animation is playing, do not start DOTween
		if (isPlayingSpecialAnimation || _isInDeckGroupAnimation) return;

		// Start DOTween scale animation
		StartScaleTween();
	}

	/// <summary>
	/// Start position DOTween animation
	/// </summary>
	private void StartPositionTween()
	{
		// If already animating and target is the same, do not restart
		if (_positionTween != null && _positionTween.IsActive() && _positionTween.IsPlaying())
		{
			// Check if current animation target is already TargetPosition
			// DOTween has no direct way to get target, so Kill and restart
			_positionTween.Kill();
		}

		_positionTween = transform.DOMove(TargetPosition, moveDuration)
			.SetEase(moveEase)
			.SetUpdate(UpdateType.Normal, true);
	}

	/// <summary>
	/// Start scale DOTween animation
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
	/// Set position immediately (no animation)
	/// </summary>
	public void SetPositionImmediate(Vector3 position)
	{
		// Stop ongoing DOTween position animation
		if (_positionTween != null && _positionTween.IsActive())
		{
			_positionTween.Kill();
			_positionTween = null;
		}

		TargetPosition = position;
		transform.position = position;
	}

	/// <summary>
	/// Set scale immediately (no animation)
	/// </summary>
	public void SetScaleImmediate(Vector3 scale)
	{
		// Stop ongoing DOTween scale animation
		if (_scaleTween != null && _scaleTween.IsActive())
		{
			_scaleTween.Kill();
			_scaleTween = null;
		}

		TargetScale = scale;
		transform.localScale = scale;
	}

	#region Stage/Bury Special Animation (Main Card Animation)

	/// <summary>
	/// Play main card phase 1 animation: left move + enlarge + rotate
	/// Used for the first phase of Stage/Bury operation
	/// </summary>
	/// <param name="onComplete">Animation complete callback</param>
	public void PlayMainCardPhase1(TweenCallback onComplete = null)
	{
		// If special animation is playing, stop first
		_currentSpecialTween?.Kill();

		isPlayingSpecialAnimation = true;
		isMainAnimationCard = true;

		// Save original scale and rotation
		_specialAnimOriginalScale = transform.localScale;
		_specialAnimOriginalRotation = transform.eulerAngles;

		// Calculate left side middle position (left of deck)
		Vector3 sidePosition = new Vector3(
		    transform.position.x - sideOffset,
		    transform.position.y,
		    transform.position.z
		);

		// Create animation sequence
		Sequence sequence = DOTween.Sequence();

		// Phase 1: left move + rotate + enlarge (parallel)
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

		// Animation complete callback
		sequence.OnComplete(() =>
		{
			onComplete?.Invoke();
		});

		_currentSpecialTween = sequence;
		sequence.Play();
	}

	/// <summary>
	/// Play main card phase 3 animation: insert to target position + restore
	/// Used for the third phase of Stage/Bury operation (simultaneous with deck restore)
	/// </summary>
	/// <param name="finalTarget">Final target position</param>
	/// <param name="onComplete">Animation complete callback</param>
	public void PlayMainCardPhase3(Vector3 finalTarget, TweenCallback onComplete = null)
	{
		// Create insert animation
		Sequence sequence = DOTween.Sequence();

		// Insert to final position + restore rotation + restore scale
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

		// Animation complete callback
		sequence.OnComplete(() =>
		{
			isPlayingSpecialAnimation = false;
			isMainAnimationCard = false;
			// Sync TargetPosition to prevent jump after DOTween animation completes
			TargetPosition = finalTarget;
			// Ensure final state is correct
			transform.eulerAngles = _specialAnimOriginalRotation;
			onComplete?.Invoke();
		});

		_currentSpecialTween = sequence;
		sequence.Play();
	}

	/// <summary>
	/// Stop special animation (used for combat phase switch or interrupt during shuffle)
	/// </summary>
	public void StopSpecialAnimation()
	{
		if (_currentSpecialTween != null && _currentSpecialTween.IsActive())
		{
			_currentSpecialTween.Kill(complete: false); // Do not complete animation, stop directly
			_currentSpecialTween = null;
		}

		if (isPlayingSpecialAnimation)
		{
			isPlayingSpecialAnimation = false;
			isMainAnimationCard = false;
			// Restore to original state
			if (_specialAnimOriginalScale != Vector3.zero)
			{
				transform.localScale = _specialAnimOriginalScale;
				transform.eulerAngles = _specialAnimOriginalRotation;
			}
		}

		// Also stop deck group animation
		StopDeckGroupAnimation();
	}

	#endregion

	#region Deck Group Animation (Breathing Effect)

	/// <summary>
	/// Play deck group animation (shrink + right move)
	/// Used when other cards are staged/buried, deck makes room
	/// </summary>
	/// <param name="basePosition">Deck base position (position to restore after animation)</param>
	public void PlayDeckGroupShrinkAnimation(Vector3 basePosition)
	{
		// Stop previous deck animation
		_currentDeckGroupTween?.Kill();

		_isInDeckGroupAnimation = true;
		_deckAnimBasePosition = basePosition;
		_deckAnimBaseScale = TargetScale;

		// Calculate position after right move
		Vector3 rightPosition = new Vector3(
		    basePosition.x + deckRightOffset,
		    basePosition.y,
		    basePosition.z
		);

		// Create animation sequence
		Sequence sequence = DOTween.Sequence();

		// 1. Phase 1: right move + shrink
		sequence.Append(
		    transform.DOMove(rightPosition, deckAnimDuration)
			.SetEase(Ease.OutQuad)
		);
		sequence.Join(
		    transform.DOScale(TargetScale * deckShrinkMultiplier, deckAnimDuration)
			.SetEase(Ease.OutQuad)
		);

		// Animation complete (keep shrunk state at this time, waiting for restore command)
		sequence.OnComplete(() =>
		{
			// Do not mark animation end, keep state until restore
		});

		_currentDeckGroupTween = sequence;
		sequence.Play();
	}

	/// <summary>
	/// Restore deck group animation (restore to original size and position)
	/// </summary>
	public void PlayDeckGroupRestoreAnimation()
	{
		if (!_isInDeckGroupAnimation) return;

		_currentDeckGroupTween?.Kill();

		// Create restore animation
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
			// Sync TargetPosition
			TargetPosition = _deckAnimBasePosition;
		});

		_currentDeckGroupTween = sequence;
		sequence.Play();
	}

	/// <summary>
	/// Stop deck group animation
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
			// Restore state
			if (_deckAnimBaseScale != Vector3.zero)
			{
				transform.localScale = _deckAnimBaseScale;
			}
		}
	}

	/// <summary>
	/// Check if deck group animation is playing
	/// </summary>
	public bool IsInDeckGroupAnimation()
	{
		return _isInDeckGroupAnimation;
	}

	#endregion

	/// <summary>
	/// Handle animation-related logic in Update
	/// Note: Position/scale animations are now handled by DOTween, this method only handles special logic
	/// </summary>
	private void UpdateMotion()
	{
		// If special animation or deck group animation is playing, stop regular DOTween animations
		if (isPlayingSpecialAnimation || _isInDeckGroupAnimation)
		{
			_positionTween?.Kill();
			_scaleTween?.Kill();
			_positionTween = null;
			_scaleTween = null;
			return;
		}

		// DOTween handles animation automatically, no extra Lerp needed here
	}

	private void ApplyColor()
	{
		if (isPhysicalStartCard) return;
		// Start Card has no cardImRepresenting, keep default color or special handling
		if (cardImRepresenting == null)
		{
			// Start Card can set a special color, or keep as is
			return;
		}

		// Determine base color
		Color baseFaceColor;
		Color baseEdgeColor = ownerCardEdgeColor;

		if (cardImRepresenting.myStatusRef == null)
		{
			baseFaceColor = ownerCardColor;
		}
		else if (cardImRepresenting.myStatusRef != CombatManager.Me?.ownerPlayerStatusRef)
		{
			baseFaceColor = opponentCardColor;
		}
		else
		{
			baseFaceColor = ownerCardColor;
		}

		// Calculate target tint intensity
		float targetIntensity = (_currentTintState != TintState.None) ? 1f : 0f;

		// Smoothly transition to target intensity
		_currentTintIntensity = Mathf.Lerp(_currentTintIntensity, targetIntensity, Time.deltaTime * tintTransitionSpeed);

		// Apply Tint
		Color finalFaceColor = baseFaceColor;
		Color finalEdgeColor = baseEdgeColor;

		if (_currentTintIntensity > 0.01f)
		{
			Color tintColor;
			float intensity;

			switch (_currentTintState)
			{
				case TintState.Infected:
					tintColor = infectedTintColor;
					intensity = infectedTintIntensity;
					break;
				case TintState.Power:
					tintColor = powerTintColor;
					intensity = powerTintIntensity;
					break;
				default:
					tintColor = Color.white;
					intensity = 0f;
					break;
			}

			float appliedIntensity = intensity * _currentTintIntensity;
			finalFaceColor = Color.Lerp(baseFaceColor, baseFaceColor * tintColor, appliedIntensity);
			finalEdgeColor = Color.Lerp(baseEdgeColor, baseEdgeColor * tintColor, appliedIntensity);
		}

		cardFace.color = finalFaceColor;
		cardEdge.color = finalEdgeColor;
	}

	/// <summary>
	/// Trigger Tint effect (called when card gains StatusEffect)
	/// </summary>
	public void TriggerTint(TintState state)
	{
		_currentTintState = state;
		_tintTimer = tintDuration;
		// Reset tint intensity to fade in smoothly from 0
		_currentTintIntensity = 0f;
	}

	/// <summary>
	/// Trigger corresponding Tint based on StatusEffect type
	/// </summary>
	public void TriggerTintForStatusEffect(EnumStorage.StatusEffect effect)
	{
		switch (effect)
		{
			case EnumStorage.StatusEffect.Infected:
				TriggerTint(TintState.Infected);
				break;
			case EnumStorage.StatusEffect.Power:
				TriggerTint(TintState.Power);
				break;
		}
	}

	/// <summary>
	/// Clear Tint (restore to None state)
	/// </summary>
	public void ClearTint()
	{
		_currentTintState = TintState.None;
		_tintTimer = 0f;
	}

	/// <summary>
	/// Update Tint timer
	/// </summary>
	private void UpdateTintTimer()
	{
		if (_tintTimer > 0f)
		{
			_tintTimer -= Time.deltaTime;
			if (_tintTimer <= 0f)
			{
				ClearTint();
			}
		}
	}

	private void OnMouseDown()
	{
		// Check if in Shop Phase
		if (currentGamePhaseRef != null && currentGamePhaseRef.Value() == EnumStorage.GamePhase.Shop)
		{
			// Start long press detection (both shop items and player deck cards)
			_isHolding = true;
			_holdTimer = 0f;
			_hasClickProcessed = false;

			// Start shake
			StartCardShake();
		}
	}

	private void OnMouseUp()
	{
		// If holding and not reached purchase time, treat as click, trigger enlarge
		if (_isHolding && _holdTimer < holdTimeRequired && !_hasClickProcessed)
		{
			//if (shopItemIndex >= 0)
			{
				EnlargeCard();
				_hasClickProcessed = true;
			}
		}

		// Cancel long press
		_isHolding = false;
		_holdTimer = 0f;

		// Stop shake
		StopCardShake();
	}

	/// <summary>
	/// Enlarge card
	/// </summary>
	private void EnlargeCard()
	{
		// Check cooldown - prevent enlarge immediately after restore
		if (_enlargeCooldown > 0) return;

		// Save original position and scale
		_originalPosition = TargetPosition;
		_originalScale = TargetScale;

		// Get enlarge settings from ShopUXManager
		if (ShopUXManager.Instance != null)
		{
			float enlargeSize = ShopUXManager.Instance.physCardEnlargeSize;
			SetTargetScale(new Vector3(enlargeSize, enlargeSize, enlargeSize));
			SetTargetPosition(ShopUXManager.Instance.enlargedPosition);
		}
		else
		{
			SetTargetScale(new Vector3(2f, 2f, 2f)); // Default enlarge multiplier
			SetTargetPosition(Vector3.zero); // Default position
		}

		_isEnlarged = true;
		Debug.Log($"[CardPhysObjScript] Card enlarged: {cardImRepresenting?.gameObject.name}");
	}

	/// <summary>
	/// Restore card to original state
	/// </summary>
	public void RestoreCard()
	{
		if (!_isEnlarged) return;

		// Restore to original position and scale
		SetTargetPosition(_originalPosition);
		SetTargetScale(_originalScale);

		_isEnlarged = false;
		Debug.Log($"[CardPhysObjScript] Card restored: {cardImRepresenting?.gameObject.name}");
	}

	/// <summary>
	/// Get whether card is enlarged
	/// </summary>
	public bool IsEnlarged()
	{
		return _isEnlarged;
	}

	private void OnMouseExit()
	{
		// Mouse exit, cancel long press
		_isHolding = false;
		_holdTimer = 0f;

		// Stop shake
		StopCardShake();
	}

	/// <summary>
	/// Start card shake
	/// </summary>
	private void StartCardShake()
	{
		if (cardShaker == null || cardShakePreset == null || _isShaking) return;

		_currentShakeInstance = cardShaker.Shake(cardShakePreset);
		_isShaking = true;
	}

	/// <summary>
	/// Stop card shake
	/// </summary>
	private void StopCardShake()
	{
		if (!_isShaking || _currentShakeInstance == null) return;

		// Stop shake, use preset fadeOut time
		_currentShakeInstance.Stop(cardShakePreset.FadeOut, true);
		_isShaking = false;
		_currentShakeInstance = null;
	}

	private void OnDestroy()
	{
		// Stop all DOTween animations to prevent access after object destruction
		_positionTween?.Kill();
		_scaleTween?.Kill();
		_currentSpecialTween?.Kill();
		_currentDeckGroupTween?.Kill();

		_positionTween = null;
		_scaleTween = null;
		_currentSpecialTween = null;
		_currentDeckGroupTween = null;
	}
}
