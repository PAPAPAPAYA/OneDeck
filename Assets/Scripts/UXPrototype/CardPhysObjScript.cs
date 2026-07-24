using System;
using DG.Tweening;
using MilkShake;
using TMPro;
using UnityEngine;
using DefaultNamespace.Managers;

public class CardPhysObjScript : MonoBehaviour
{
	public bool isPhysicalStartCard;
	public CardScript cardImRepresenting;
	private CombatUXManager _combatUXManager;

	[Header("Phase Ref")]
	[SerializeField] public GamePhaseSO currentGamePhaseRef;

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
	public SpriteRenderer cardImg;
	public TextMeshPro cardCostPrint;
	public TextMeshPro cardNamePrint;
	public TextMeshPro cardDescPrint;
	public TextMeshPro cardPricePrint;
	public TextMeshPro cardRarityPrint;
	public TextMeshPro cardTagPrint;
	public TextMeshPro cardStatusEffectPrint;

	[Header("COLOR")]
	public ColorSO ownerCardColor;
	public ColorSO ownerCardEdgeColor;
	public ColorSO opponentCardColor;
	public ColorSO opponentCardEdgeColor;
	public ColorSO ownerTextColor;
	public ColorSO opponentTextColor;

	[Header("CARD ART")]
	[Tooltip("Card face sprite used when this card is owned by the player")]
	public Sprite ownerCardFaceSprite;
	[Tooltip("Card face sprite used when this card is owned by the opponent")]
	public Sprite opponentCardFaceSprite;

	[Header("TINT - Infected")]
	[Tooltip("Tint color for Infected state")]
	public ColorSO infectedTintColor;
	[Tooltip("Tint intensity for Infected state")]
	[Range(0f, 1f)]
	public float infectedTintIntensity = 0.5f;

	[Header("TINT - Power")]
	[Tooltip("Tint color for Power state")]
	public ColorSO powerTintColor;
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
	private string _lastLoggedStatusEffectText;

	public enum TintState { None, Infected, Power }

	// ========== Animation target position ==========
	[Header("ANIMATION")]
	public Vector3 TargetPosition { get; private set; }
	public Vector3 TargetScale { get; private set; }
	public Quaternion TargetRotation { get; private set; }

	/// <summary>
	/// True while a DOTween position tween is actively playing (e.g. reveal-entry flight).
	/// CombatUXManager uses this to avoid restarting such tweens: a restart kills the
	/// in-flight tween and its completion callback would never fire (VISUAL-FIX 2026-07-18).
	/// </summary>
	public bool IsPositionTweenPlaying => _positionTween != null && _positionTween.IsActive() && _positionTween.IsPlaying();

	// ========== Shake related ==========
	private ShakeInstance _currentShakeInstance;
	private bool _isShaking = false;

	[Header("Custom Shake")]
	[Tooltip("Max Z-rotation angle for cost-fail shake (degrees).")]
	public float customShakeAngle = 15f;
	[Tooltip("Duration for one side of the shake (seconds). Total ~4x this value.")]
	public float customShakeHalfDuration = 0.1f;

	private Tween _shakeTween;

	[Header("Special Animation")]
	[Tooltip("Is playing special animation")]
	public bool isPlayingSpecialAnimation = false;
	[Tooltip("Is pending slot-in animation (e.g. new card added by AddTempCard waiting for its SlotIn). Used by ApplyAnimationResult and the position calculators; pending cards are INCLUDED in the full deck count for layout (VISUAL-FIX 2026-05-24).")]
	public bool isPendingSlotIn = false;
	[Tooltip("Is currently popped up to peak position (PopUpCard/MoveCardToPopUpPosition). Cleared by SlotInCard or any deck-move animation that ends at a deck position.")]
	public bool isPoppedUp = false;

	// ========== Face Down / Flip ==========
	[Header("FLIP")]
	[Tooltip("Total flip duration (seconds); scaled by combat animation speed in Combat phase")]
	public float flipDuration = 0.3f;

	/// <summary>False while the card shows its back (static in the combat deck).</summary>
	public bool isFaceUp { get; private set; } = true;
	/// <summary>True once the card has been shown face-up. Rule: such cards are never covered again (shuffle overrides via force + ClearRevealedMemory).</summary>
	public bool everRevealed { get; private set; }

	private Transform _flipRoot;
	private SpriteRenderer _cardBackRenderer;
	private Transform[] _faceElements;
	private Tween _flipTween;

	[HideInInspector]
	public Vector3 popUpOriginalPosition;
	[HideInInspector]
	public Vector3 popUpOriginalScale;

	[Header("Reveal Zone Pending")]
	[Tooltip("When special animation finishes, move to reveal zone instead of default target")]
	public bool pendingRevealZoneMove = false;
	public Vector3 pendingRevealPosition;
	public Vector3 pendingRevealScale;

	// ========== DOTween animation ==========
	[Header("DOTween Animation")]
	[Tooltip("Animation duration to move to target position")]
	public float moveDuration = 0.3f;
	[Tooltip("Ease type for move animation")]
	public Ease moveEase = Ease.OutQuad;
	[Tooltip("Animation duration to rotate to target rotation")]
	public float rotationDuration = 0.3f;
	[Tooltip("Ease type for rotation animation")]
	public Ease rotationEase = Ease.OutQuad;
	[Tooltip("Use local rotation for deck layout rotation tween")]
	public bool useLocalRotation = true;

	private Tweener _positionTween;
	private Tweener _scaleTween;
	private Tweener _rotationTween;

	void Awake()
	{
		BuildFlipRoot();
	}

	void OnEnable()
	{
		_combatUXManager = CombatUXManager.me;
	}

	void Update()
	{
		// Handle pending reveal zone move when special animation ends
		if (!isPlayingSpecialAnimation && pendingRevealZoneMove)
		{

			SetTargetPosition(pendingRevealPosition);
			SetTargetScale(pendingRevealScale);
			pendingRevealZoneMove = false;
		}

		// Face-down cards skip all face-content writers (name/desc/status/tint/colors)
		// so no information leaks onto the card back; the back only tracks ownership color.
		if (isFaceUp)
		{
			ApplyColor();
			UpdateStatusEffectDisplay();
			UpdateCardDescription();
			UpdateCostDisplay();
			UpdatePriceDisplay();
			UpdateRarityDisplay();
			UpdateTagDisplay();
		}
		else
		{
			ApplyBackColor();
		}
		UpdateTintTimer();
		UpdateHover();
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
	/// Update Price display, only shown in Shop Phase.
	/// </summary>
	private void UpdatePriceDisplay()
	{
		if (cardPricePrint == null) return;

		if (currentGamePhaseRef == null || currentGamePhaseRef.Value() != EnumStorage.GamePhase.Shop)
		{
			cardPricePrint.gameObject.SetActive(false);
		}
	}

	/// <summary>
	/// Update Tag display, tags wrapped in brackets separated by spaces
	/// </summary>
	private void UpdateTagDisplay()
	{
		if (cardTagPrint == null || cardImRepresenting == null) return;

		string tagText = GetTagText();
		if (string.IsNullOrEmpty(tagText))
		{
			cardTagPrint.gameObject.SetActive(false);
			return;
		}

		cardTagPrint.gameObject.SetActive(true);
		cardTagPrint.text = tagText;
	}

	/// <summary>
	/// Build the tag display text ("[Tag] [Tag]"), skipping Tag.None.
	/// Shared by the in-card tag print and the hover tooltip (single source of truth).
	/// Returns an empty string when there are no visible tags.
	/// </summary>
	public string GetTagText()
	{
		if (cardImRepresenting == null || cardImRepresenting.myTags == null || cardImRepresenting.myTags.Count == 0)
		{
			return string.Empty;
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

		return hasVisibleTag ? sb.ToString() : string.Empty;
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

	private void UpdateStatusEffectDisplay()
	{
		if (cardImRepresenting == null) return;

		var statusEffectsForDisplay = cardImRepresenting.GetStatusEffectsForDisplay();
		var statusEffectText = CombatInfoDisplayer.me?.ProcessStatusEffectInfo(statusEffectsForDisplay);

		if (cardStatusEffectPrint != null)
		{
			if (!string.IsNullOrEmpty(statusEffectText))
			{
				cardStatusEffectPrint.gameObject.SetActive(true);
				cardStatusEffectPrint.text = statusEffectText;
			}
			else
			{
				cardStatusEffectPrint.gameObject.SetActive(false);
			}
		}

		if (cardNamePrint == null) return;

		if (cardStatusEffectPrint != null)
		{
			cardNamePrint.text = "<b>" + cardImRepresenting.GetDisplayName() + "</b>";
		}
		else
		{
			// Fallback for prefabs without cardStatusEffectPrint
			if (!string.IsNullOrEmpty(statusEffectText))
			{
				cardNamePrint.text = "<size=12>" + statusEffectText + "\n</size><b>" + cardImRepresenting.GetDisplayName() + "</b>";
			}
			else
			{
				cardNamePrint.text = cardImRepresenting.GetDisplayName();
			}
		}

		// Log only when status effect text actually changes to avoid Update() spam.
		if (statusEffectText != _lastLoggedStatusEffectText)
		{
			TestManager.Log("[StatusEffectDisplay] UpdateStatusEffectDisplay card=" + cardImRepresenting.GetDisplayName() +
				" hasSnapshot=" + cardImRepresenting.HasDisplaySnapshot +
				" count=" + (statusEffectsForDisplay != null ? statusEffectsForDisplay.Count : 0) +
				" old=[" + (_lastLoggedStatusEffectText ?? "null") + "]" +
				" new=[" + (statusEffectText ?? "null") + "]");
			_lastLoggedStatusEffectText = statusEffectText;
		}
	}

	/// <summary>
	/// Update Card Description display, resolves &lt;dmg&gt; placeholders dynamically
	/// based on current Power status effects.
	/// </summary>
	private void UpdateCardDescription()
	{
		if (cardDescPrint == null || cardImRepresenting == null) return;

		string displayDesc = cardImRepresenting.GetCardDescForDisplay();
		cardDescPrint.text = displayDesc;

		if (displayDesc != null && CardScript.ContainsAnyDamagePlaceholder(displayDesc) && cardImRepresenting.HasDisplaySnapshot)
		{
			TestManager.LogWarning("[DynamicDamageDisplay] UpdateCardDescription showing raw <dmg> during snapshot card=" + cardImRepresenting.GetDisplayName() + " cardDesc=[" + cardImRepresenting.cardDesc + "]");
		}
	}

	/// <summary>
	/// Set target position (called by CombatUXManager), uses DOTween animation
	/// </summary>
	public void SetTargetPosition(Vector3 target, Action onComplete = null)
	{
		TestManager.Log("[CardPhysObjScript] SetTargetPosition card=" + name + " currentPos=" + transform.position + " newTarget=" + target + " isPlayingSpecial=" + isPlayingSpecialAnimation);
		TargetPosition = target;

		// If special animation is playing, do not start DOTween
		if (isPlayingSpecialAnimation)
		{
			onComplete?.Invoke();
			return;
		}

		// Start DOTween position animation
		StartPositionTween(onComplete);
	}

	/// <summary>
	/// Update target position without starting a DOTween.
	/// Used when deck count changes (e.g. AddPhysicalCardToDeck) to keep target positions
	/// correct for existing cards without pre-moving them before bury/stage animations.
	/// </summary>
	public void UpdateTargetPositionOnly(Vector3 target)
	{
		// Debug.Log("[CardPhysObjScript] UpdateTargetPositionOnly card=" + name + " currentPos=" + transform.position + " newTarget=" + target);
		TargetPosition = target;

		// VISUAL-FIX(2026-05-15): Cards pre-moved by UpdateTargetPositionOnly cause distance-zero tweens
		//   Cause:    Restarting tween for cards already in the deck pre-moves them to final position.
		//             Bury/stage animations then have no visible movement (distance=0).
		//   Affects:  CardPhysObjScript, UpdateTargetPositionOnly, AddPhysicalCardToDeck
		//   Regress:  Add a card to deck then trigger Bury/Stage; verify existing cards animate visibly
		//   Related:  RIFT_INSECT, BLACKSMITH, any Bury/Stage card
		bool isIncomingFlight = transform.position.y < -2f;
		if (isIncomingFlight && _positionTween != null && _positionTween.IsActive() && _positionTween.IsPlaying())
		{
			StartPositionTween();
		}
	}

	/// <summary>
	/// Set target scale (called by CombatUXManager), uses DOTween animation
	/// </summary>
	public void SetTargetScale(Vector3 target)
	{
		TargetScale = target;

		// If special animation is playing, do not start DOTween
		if (isPlayingSpecialAnimation) return;

		// Start DOTween scale animation
		StartScaleTween();
	}

	/// <summary>
	/// Set target local rotation (called by CombatUXManager), uses DOTween animation.
	/// </summary>
	public void SetTargetRotation(Quaternion target, Action onComplete = null)
	{
		TargetRotation = target;

		// If special animation is playing, do not start DOTween
		if (isPlayingSpecialAnimation)
		{
			onComplete?.Invoke();
			return;
		}

		// Start DOTween rotation animation
		StartRotationTween(onComplete);
	}

	/// <summary>
	/// Update target local rotation without starting a DOTween.
	/// </summary>
	public void UpdateTargetRotationOnly(Quaternion target)
	{
		TargetRotation = target;
	}

	/// <summary>
	/// Start position DOTween animation
	/// </summary>
	private void StartPositionTween(Action onComplete = null)
	{
		// If already animating and target is the same, do not restart
		if (_positionTween != null && _positionTween.IsActive() && _positionTween.IsPlaying())
		{
			// Debug.Log("[CardPhysObjScript] StartPositionTween KILLING existing tween card=" + name);
			_positionTween.Kill();
		}

		TestManager.Log("[CardPhysObjScript] StartPositionTween START card=" + name + " from=" + transform.position + " to=" + TargetPosition + " duration=" + moveDuration);
		float scaledDuration = GetCombatScaledDuration(moveDuration);
		var tween = transform.DOMove(TargetPosition, scaledDuration)
			.SetEase(moveEase)
			.SetUpdate(UpdateType.Normal, true);
		if (onComplete != null)
		{
			tween.OnComplete(() => onComplete.Invoke());
		}
		_positionTween = tween;
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

		_scaleTween = transform.DOScale(TargetScale, GetCombatScaledDuration(moveDuration))
			.SetEase(moveEase)
			.SetUpdate(UpdateType.Normal, true);
	}

	/// <summary>
	/// Start rotation DOTween animation
	/// </summary>
	private void StartRotationTween(Action onComplete = null)
	{
		if (_rotationTween != null && _rotationTween.IsActive() && _rotationTween.IsPlaying())
		{
			_rotationTween.Kill();
		}

		float scaledDuration = GetCombatScaledDuration(rotationDuration);
		Vector3 targetEuler = TargetRotation.eulerAngles;
		Tweener tween;
		if (useLocalRotation)
		{
			tween = transform.DOLocalRotate(targetEuler, scaledDuration)
				.SetEase(rotationEase)
				.SetUpdate(UpdateType.Normal, true);
		}
		else
		{
			tween = transform.DORotate(targetEuler, scaledDuration)
				.SetEase(rotationEase)
				.SetUpdate(UpdateType.Normal, true);
		}

		if (onComplete != null)
		{
			tween.OnComplete(() => onComplete.Invoke());
		}
		_rotationTween = tween;
	}

	/// <summary>
	/// Returns the combat-scaled duration if the current phase is Combat, otherwise the base duration.
	/// Used to keep Shop card animations unaffected by the global combat speed scaler.
	/// </summary>
	private float GetCombatScaledDuration(float baseDuration)
	{
		bool isCombat = currentGamePhaseRef != null && currentGamePhaseRef.Value() == EnumStorage.GamePhase.Combat;
		return isCombat ? CombatAnimationSpeed.ScaleDuration(baseDuration) : baseDuration;
	}

	/// <summary>
	/// Set position immediately (no animation)
	/// </summary>
	public void SetPositionImmediate(Vector3 position)
	{
		// Debug.Log("[CardPhysObjScript] SetPositionImmediate card=" + name + " pos=" + position);
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

	/// <summary>
	/// Set local rotation immediately (no animation)
	/// </summary>
	public void SetRotationImmediate(Quaternion rotation)
	{
		// Stop ongoing DOTween rotation animation
		if (_rotationTween != null && _rotationTween.IsActive())
		{
			_rotationTween.Kill();
			_rotationTween = null;
		}

		TargetRotation = rotation;
		transform.localRotation = rotation;
	}

	#region Face Down / Flip

	/// <summary>
	/// Builds the FlipRoot container and the placeholder card back at runtime (no prefab edits).
	/// Flip tweens act only on FlipRoot.localScale.x so they never fight the root transform
	/// tweens owned by deck layout / move animations. FlipRoot is parented next to the face
	/// elements (under the shaker child) so card shakes still apply to the face content.
	/// </summary>
	private void BuildFlipRoot()
	{
		if (cardFace == null) return; // e.g. start card prefab: flip disabled
		// VISUAL-FIX(2026-07-24): NullReferenceException in Awake for shop empty-slot placeholders
		//   Cause:    EmptyCardSpaceParent.prefab wires cardFace but none of the ColorSO fields,
		//             so the cardFace guard passed and ownerCardColor.value threw (shop empty
		//             slot instantiation aborted mid-Awake).
		//   Affects:  CardPhysObjScript.BuildFlipRoot (shop emptyCardSpacePrefab)
		//   Regress:  Enter the shop with empty deck slots; empty slots spawn with no NRE and
		//             render their normal placeholder face (flip/back machinery skipped).
		if (ownerCardColor == null || opponentCardColor == null) return;

		var faces = new System.Collections.Generic.List<Transform>();
		if (cardFace != null) faces.Add(cardFace.transform);
		if (cardEdge != null) faces.Add(cardEdge.transform);
		if (cardImg != null) faces.Add(cardImg.transform);
		if (cardNamePrint != null) faces.Add(cardNamePrint.transform);
		if (cardDescPrint != null) faces.Add(cardDescPrint.transform);
		if (cardCostPrint != null) faces.Add(cardCostPrint.transform);
		if (cardPricePrint != null) faces.Add(cardPricePrint.transform);
		if (cardRarityPrint != null) faces.Add(cardRarityPrint.transform);
		if (cardTagPrint != null) faces.Add(cardTagPrint.transform);
		if (cardStatusEffectPrint != null) faces.Add(cardStatusEffectPrint.transform);

		var flipRootGo = new GameObject("FlipRoot");
		_flipRoot = flipRootGo.transform;
		_flipRoot.SetParent(cardFace.transform.parent, false);
		var faceParent = cardFace.transform.parent; // capture before reparenting (it becomes _flipRoot below)
		foreach (var t in faces)
		{
			// worldPositionStays: true keeps the exact current pose regardless of parent depth
			t.SetParent(_flipRoot, true);
		}
		_faceElements = faces.ToArray();

		// Shadows squash with the flip but are NOT part of the face visibility toggle:
		// the card back keeps its silhouette/drop shadow when face-down.
		var bigShadow = faceParent.Find("PhysicalCardBigShadow");
		if (bigShadow != null) bigShadow.SetParent(_flipRoot, true);
		var rimShadow = faceParent.Find("PhysicalCardShadow");
		if (rimShadow != null) rimShadow.SetParent(_flipRoot, true);

		// Placeholder back: same sprite as the face with a neutral tint. Real back art can
		// replace this later without touching code.
		// VISUAL-FIX(2026-07-20): Card back renders tiny / effectively invisible
		//   Cause:    CardBack was created via AddComponent<SpriteRenderer> with default
		//             drawMode=Simple. The face uses Sliced drawMode with explicit size
		//             (6.4 x 9.2); the sprite is 256px at 256 PPU (1 unit native), so the
		//             back rendered at ~0.4 units and the deck looked like bare shadows.
		//   Affects:  CardPhysObjScript, BuildFlipRoot
		//   Regress:  Cover any card; the back must render at the same size as the face
		//             (drawMode/size/sharedMaterial are copied from cardFace).
		//   Related:  plan-card-flip-face-down-2026-07-20
		var backGo = new GameObject("CardBack");
		_cardBackRenderer = backGo.AddComponent<SpriteRenderer>();
		_cardBackRenderer.sprite = cardFace.sprite;
		_cardBackRenderer.color = ownerCardColor.value;
		_cardBackRenderer.sortingLayerID = cardFace.sortingLayerID;
		_cardBackRenderer.sortingOrder = cardFace.sortingOrder;
		_cardBackRenderer.drawMode = cardFace.drawMode;
		if (cardFace.drawMode == SpriteDrawMode.Sliced)
		{
			_cardBackRenderer.size = cardFace.size;
		}
		_cardBackRenderer.sharedMaterial = cardFace.sharedMaterial;
		var backTransform = _cardBackRenderer.transform;
		backTransform.SetParent(_flipRoot, false);
		backTransform.localPosition = cardFace.transform.localPosition;
		backTransform.localRotation = cardFace.transform.localRotation;
		backTransform.localScale = cardFace.transform.localScale;
		backGo.SetActive(false);
	}

	/// <summary>
	/// Flip the card face-up / face-down with a 2D squash flip on FlipRoot.
	/// Rules: a card that was ever revealed is never covered again (cover calls are skipped);
	/// the shuffle rule bypasses this via force=true plus ClearRevealedMemory().
	/// </summary>
	/// <param name="faceUp">True = show face, false = show back.</param>
	/// <param name="animated">True = squash flip tween; false = instant swap.</param>
	/// <param name="force">Bypass the everRevealed cover guard (shuffle rule only).</param>
	public void SetFaceUp(bool faceUp, bool animated, bool force = false, System.Action onComplete = null)
	{
		if (isFaceUp == faceUp)
		{
			onComplete?.Invoke();
			return;
		}
		// Revealed cards never cover again (hard rule; shuffle bypasses via force).
		if (!faceUp && !force && everRevealed)
		{
			onComplete?.Invoke();
			return;
		}

		isFaceUp = faceUp;
		if (faceUp) everRevealed = true;

		if (_flipRoot == null)
		{
			onComplete?.Invoke();
			return;
		}

		KillFlipTween();
		if (!animated)
		{
			ApplyFaceVisibility();
			onComplete?.Invoke();
			return;
		}

		float halfDuration = GetCombatScaledDuration(flipDuration * 0.5f);
		Sequence flipSeq = DOTween.Sequence();
		flipSeq.Append(_flipRoot.DOScaleX(0f, halfDuration).SetEase(Ease.InQuad));
		flipSeq.AppendCallback(() => ApplyFaceVisibility());
		flipSeq.Append(_flipRoot.DOScaleX(1f, halfDuration).SetEase(Ease.OutQuad));
		flipSeq.SetUpdate(UpdateType.Normal, true);
		flipSeq.OnComplete(() =>
		{
			_flipTween = null;
			onComplete?.Invoke();
		});
		_flipTween = flipSeq;
	}

	/// <summary>
	/// Clear the "was revealed" memory. Called by the shuffle force-cover rule so a
	/// shuffled card counts as fresh hidden information again.
	/// </summary>
	public void ClearRevealedMemory()
	{
		everRevealed = false;
	}

	private void ApplyFaceVisibility()
	{
		if (_faceElements != null)
		{
			for (int i = 0; i < _faceElements.Length; i++)
			{
				if (_faceElements[i] != null) _faceElements[i].gameObject.SetActive(isFaceUp);
			}
		}
		if (_cardBackRenderer != null)
		{
			_cardBackRenderer.gameObject.SetActive(!isFaceUp);
		}
	}

	/// <summary>
	/// Tint the card back by ownership (mirrors the ownership check in ApplyColor).
	/// Called every frame while face-down so ownership changes (HeartChanged) show up on the back.
	/// </summary>
	private void ApplyBackColor()
	{
		if (_cardBackRenderer == null) return;

		Color backColor;
		if (cardImRepresenting == null || cardImRepresenting.myStatusRef == null
			|| cardImRepresenting.myStatusRef == CombatManager.Me?.ownerPlayerStatusRef)
		{
			backColor = ownerCardColor.value;
		}
		else
		{
			backColor = opponentCardColor.value;
		}
		_cardBackRenderer.color = backColor;
	}

	/// <summary>
	/// Kill the flip tween. NOT part of KillTweens() on purpose: CombatCardView calls
	/// KillTweens() every frame during special animations, which would freeze a flip
	/// mid-squash. The flip tween lives on FlipRoot and is managed by SetFaceUp only.
	/// </summary>
	private void KillFlipTween()
	{
		if (_flipTween != null && _flipTween.IsActive())
		{
			_flipTween.Kill();
		}
		_flipTween = null;
		if (_flipRoot != null)
		{
			_flipRoot.localScale = Vector3.one;
		}
	}

	#endregion

	#region Special Animation

	/// <summary>
	/// Stop special animation (used for combat phase switch or interrupt during shuffle)
	/// </summary>
	public void StopSpecialAnimation()
	{
		if (isPlayingSpecialAnimation)
		{
			isPlayingSpecialAnimation = false;
		}
		isPendingSlotIn = false;
	}

	#endregion

	/// <summary>
	/// Kill active DOTween tweens for position, scale and rotation.
	/// Called by CombatCardView when special animation is playing.
	/// </summary>
	public void KillTweens()
	{
		_positionTween?.Kill();
		_scaleTween?.Kill();
		_rotationTween?.Kill();
		_positionTween = null;
		_scaleTween = null;
		_rotationTween = null;
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
		Color baseEdgeColor = ownerCardEdgeColor.value;
		bool isOwner = true;

		if (cardImRepresenting.myStatusRef == null)
		{
			baseFaceColor = ownerCardColor.value;
		}
		else if (cardImRepresenting.myStatusRef != CombatManager.Me?.ownerPlayerStatusRef)
		{
			baseFaceColor = opponentCardColor.value;
			isOwner = false;
		}
		else
		{
			baseFaceColor = ownerCardColor.value;
		}

		// Update card face art based on ownership
		if (cardImg != null)
		{
			Sprite targetSprite = isOwner ? ownerCardFaceSprite : opponentCardFaceSprite;
			if (targetSprite != null)
			{
				cardImg.sprite = targetSprite;
			}
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
					tintColor = infectedTintColor.value;
					intensity = infectedTintIntensity;
					break;
				case TintState.Power:
					tintColor = powerTintColor.value;
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

		// Apply text color based on ownership
		Color textColor = baseFaceColor == ownerCardColor.value ? ownerTextColor.value : opponentTextColor.value;
		if (cardNamePrint != null) cardNamePrint.color = textColor;
		if (cardDescPrint != null) cardDescPrint.color = textColor;
		if (cardCostPrint != null) cardCostPrint.color = textColor;
		if (cardTagPrint != null) cardTagPrint.color = textColor;
		if (cardRarityPrint != null) cardRarityPrint.color = textColor;
		if (cardStatusEffectPrint != null) cardStatusEffectPrint.color = textColor;
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

	/// <summary>
	/// Start card shake
	/// </summary>
	public void StartCardShake()
	{
		if (cardShaker == null || cardShakePreset == null || _isShaking) return;

		_currentShakeInstance = cardShaker.Shake(cardShakePreset);
		_isShaking = true;
	}

	/// <summary>
	/// Stop card shake
	/// </summary>
	public void StopCardShake()
	{
		if (!_isShaking || _currentShakeInstance == null) return;

		// Stop shake, use preset fadeOut time
		_currentShakeInstance.Stop(cardShakePreset.FadeOut, true);
		_isShaking = false;
		_currentShakeInstance = null;
	}

	/// <summary>
	/// Play a simple left-right shake using DOTween (no MilkShake).
	/// Sequence: center -> left -> right -> center.
	/// </summary>
	public void PlayCustomShake(Action onComplete = null)
	{
		if (_shakeTween != null && _shakeTween.IsActive() && _shakeTween.IsPlaying()) return;

		_shakeTween?.Kill();

		float shakeHalfDuration = GetCombatScaledDuration(customShakeHalfDuration);
		var seq = DOTween.Sequence();
		seq.Append(transform.DOLocalRotate(new Vector3(0, 0, customShakeAngle), shakeHalfDuration).SetEase(Ease.OutQuad));
		seq.Append(transform.DOLocalRotate(new Vector3(0, 0, -customShakeAngle), shakeHalfDuration * 2f).SetEase(Ease.InOutQuad));
		seq.Append(transform.DOLocalRotate(Vector3.zero, shakeHalfDuration).SetEase(Ease.OutQuad));
		if (onComplete != null)
			seq.OnComplete(() => onComplete());
		_shakeTween = seq;
	}

	#region Hover Tag Tooltip

	[Header("HOVER")]
	[Tooltip("Delay before the tag tooltip appears (seconds). Not scaled by combat animation speed.")]
	public float hoverDelay = 0.2f;

	/// <summary>
	/// Static hover owner: only the frontmost card under the cursor reacts. Unity fires
	/// OnMouseEnter for every collider under the cursor, so overlapping cascade cards
	/// arbitrate here: owner = strictly smaller world z (deck front has the smallest z).
	/// </summary>
	private static CardPhysObjScript _currentHoverOwner;

	private bool _hoverActive;
	private bool _hoverPoppedUp;
	private bool _savedAutoRevealValid;
	private bool _savedAutoReveal;
	private float _hoverTooltipTimer = -1f;

	void OnMouseEnter()
	{
		if (cardImRepresenting == null)
		{
			TestManager.Log("[Hover] OnMouseEnter SKIP card=" + name + " reason=no cardImRepresenting (start card)");
			return;
		}
		if (!isFaceUp)
		{
			TestManager.Log("[Hover] OnMouseEnter SKIP card=" + name + " reason=face-down (Rule 1)");
			return;
		}
		if (IsHoverBlockedByCombatState())
		{
			TestManager.Log("[Hover] OnMouseEnter SKIP card=" + name + " reason=animation/input blocked");
			return;
		}

		// Z arbitration: ownership transfers only to a strictly closer card;
		// equal or deeper cards under the cursor do nothing.
		if (_currentHoverOwner != null && _currentHoverOwner != this)
		{
			if (transform.position.z >= _currentHoverOwner.transform.position.z)
			{
				TestManager.Log("[Hover] OnMouseEnter SKIP card=" + name + " reason=not owner (myZ=" + transform.position.z + " ownerZ=" + _currentHoverOwner.transform.position.z + " owner=" + _currentHoverOwner.name + ")");
				return;
			}
			TestManager.Log("[Hover] ownership transfer " + _currentHoverOwner.name + " -> " + name);
			_currentHoverOwner.EndHover("ownership lost to " + name);
		}
		_currentHoverOwner = this;
		BeginHover();
	}

	// NOTE: OnMouseExit is intentionally NOT used to end the hover. PopUpCard moves the
	// card out from under the cursor, which would fire OnMouseExit immediately and undo
	// the pop-up / cancel the pending tooltip. UpdateHover() polls the cursor position
	// against the collider every frame instead.

	private Collider2D _hoverCollider;
	private Camera _hoverCamera;

	private bool IsCursorOverCard()
	{
		if (_hoverCollider == null) return true; // no collider: cannot test, stay hovered
		if (_hoverCamera == null)
		{
			_hoverCamera = Camera.main;
			if (_hoverCamera == null) return true; // no camera: cannot test, stay hovered
		}
		Vector3 screenPos = Input.mousePosition;
		screenPos.z = _hoverCamera.WorldToScreenPoint(transform.position).z;
		Vector3 worldPos = _hoverCamera.ScreenToWorldPoint(screenPos);
		return _hoverCollider.OverlapPoint(worldPos);
	}

	private bool IsRevealZoneCard()
	{
		return _combatUXManager != null && _combatUXManager.physicalCardInRevealZone == gameObject;
	}

	private bool IsInCombatPhase()
	{
		return currentGamePhaseRef != null && currentGamePhaseRef.Value() == EnumStorage.GamePhase.Combat;
	}

	private static bool IsHoverBlockedByCombatState()
	{
		var cm = CombatManager.Me;
		if (cm == null) return false;
		return cm.isPlayingEffectAnimations || cm.IsInputBlocked;
	}

	private void BeginHover()
	{
		_hoverActive = true;
		_hoverTooltipTimer = hoverDelay;
		_hoverCollider = GetComponent<Collider2D>();
		TestManager.Log("[Hover] BeginHover card=" + name + " faceUp=" + isFaceUp + " revealZone=" + IsRevealZoneCard() + " combat=" + IsInCombatPhase() + " tags=[" + GetTagText() + "]");

		if (!IsInCombatPhase()) return; // Shop: tooltip only, no pop-up / autoReveal pause

		// Pause autoReveal immediately so it cannot advance during hoverDelay.
		var cm = CombatManager.Me;
		if (cm != null && cm.autoReveal)
		{
			_savedAutoReveal = cm.autoReveal;
			_savedAutoRevealValid = true;
			cm.autoReveal = false;
		}

		// The reveal-zone card is already fully displayed; pop-up would be redundant.
		if (IsRevealZoneCard()) return;

		if (CombatUXManager.visuals != null)
		{
			_hoverPoppedUp = true;
			CombatUXManager.visuals.PopUpCard(cardImRepresenting.gameObject);
		}
	}

	/// <summary>
	/// End the hover: restore autoReveal, slot the card back in, cancel/hide the tooltip.
	/// Safe to call when not hovering.
	/// </summary>
	private void EndHover(string reason = "")
	{
		if (!_hoverActive) return;
		TestManager.Log("[Hover] EndHover card=" + name + " reason=" + reason + " poppedUp=" + _hoverPoppedUp + " restoreAutoReveal=" + _savedAutoRevealValid);
		_hoverActive = false;
		_hoverTooltipTimer = -1f;
		CardTagTooltip.HideFor(this);

		if (_savedAutoRevealValid)
		{
			_savedAutoRevealValid = false;
			var cm = CombatManager.Me;
			if (cm != null) cm.autoReveal = _savedAutoReveal;
		}

		if (_hoverPoppedUp)
		{
			_hoverPoppedUp = false;
			if (CombatUXManager.visuals != null && cardImRepresenting != null)
			{
				CombatUXManager.visuals.SlotInCard(cardImRepresenting.gameObject);
			}
		}
	}

	private void UpdateHover()
	{
		if (!_hoverActive) return;

		// Force-hide: card flipped face-down, animation playback started, input blocked
		// by something OTHER than our own pop-up (PopUpCard blocks input itself), or
		// the phase changed away from Combat while popped up.
		var cm = CombatManager.Me;
		bool animPlaying = cm != null && cm.isPlayingEffectAnimations;
		bool externallyBlocked = cm != null && cm.IsInputBlocked && !_hoverPoppedUp;
		if (!isFaceUp || animPlaying || externallyBlocked || (_hoverPoppedUp && !IsInCombatPhase()))
		{
			if (_currentHoverOwner == this) _currentHoverOwner = null;
			EndHover("force-hide (faceDown=" + !isFaceUp + " animPlaying=" + animPlaying + " externallyBlocked=" + externallyBlocked + " phaseLeft=" + (_hoverPoppedUp && !IsInCombatPhase()) + ")");
			return;
		}

		// Cursor left the card (the card may have moved to the pop-up peak, so this
		// poll replaces OnMouseExit).
		if (!IsCursorOverCard())
		{
			if (_currentHoverOwner == this) _currentHoverOwner = null;
			EndHover("cursor left card");
			return;
		}

		if (_hoverTooltipTimer >= 0f)
		{
			_hoverTooltipTimer -= Time.deltaTime;
			if (_hoverTooltipTimer < 0f)
			{
				TestManager.Log("[Hover] tooltip delay elapsed, ShowFor card=" + name + " tags=[" + GetTagText() + "]");
				CardTagTooltip.ShowFor(this);
			}
		}
	}

	#endregion

	private void OnDestroy()
	{
		if (_currentHoverOwner == this)
		{
			_currentHoverOwner = null;
		}
		EndHover("OnDestroy");

		// Stop all DOTween animations to prevent access after object destruction
		_positionTween?.Kill();
		_scaleTween?.Kill();
		_rotationTween?.Kill();
		_shakeTween?.Kill();
		_flipTween?.Kill();

		_positionTween = null;
		_scaleTween = null;
		_rotationTween = null;
		_shakeTween = null;
		_flipTween = null;
	}
}
