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
	public SpriteRenderer cardImg;
	public TextMeshPro cardCostPrint;
	public TextMeshPro cardNamePrint;
	public TextMeshPro cardDescPrint;
	public TextMeshPro cardPricePrint;
	public TextMeshPro cardRarityPrint;
	public TextMeshPro cardTagPrint;
	public TextMeshPro cardStatusEffectPrint;
	public TextMeshPro cardAttackPrint;

	[Header("CARD ART")]
	[Tooltip("Card face sprite used when this card is owned by the player")]
	public Sprite ownerCardFaceSprite;
	[Tooltip("Card face sprite used when this card is owned by the opponent")]
	public Sprite opponentCardFaceSprite;

	[Header("TINT - Infected")]
	[Tooltip("Tint intensity for Infected state")]
	[Range(0f, 1f)]
	public float infectedTintIntensity = 0.5f;

	[Header("TINT - Power")]
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

	/// <summary>Active PopUpCard/SlotInCard DOTween sequence plus its onComplete callback.
	/// Owned by CombatUXManager; lets the opposite operation interrupt a mid-flight seq and run
	/// its completion bookkeeping exactly once (VISUAL-FIX 2026-07-31, see CombatUXManager).</summary>
	[HideInInspector]
	public Tween activePopUpSlotInSeq;
	[HideInInspector]
	public Action activePopUpSlotInOnComplete;

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
		// VISUAL-FIX(2026-08-15): Start Card's big shadow always visible in the deck.
		//   Cause:    The start card prefab wires no cardFace, so BuildFlipRoot returns
		//             early and its big-shadow auto-wire never runs; bigShadowRenderer
		//             stayed null, so SetBigShadowSuppressed and the reveal shadow drive
		//             both no-opped and the prefab's PhysicalCardBigShadow child rendered
		//             forever (also true for any prefab skipping BuildFlipRoot).
		//   Affects:  CardPhysObjScript.Awake (start card / unwired prefabs).
		//   Regress:  Float Stack mode: the start card's big shadow is hidden in the deck,
		//             drives to slot 0 on its reveal, fades out on the shuffle, and stays
		//             hidden afterwards; normal cards are unaffected (already auto-wired).
		if (bigShadowRenderer == null)
		{
			var shadow = FindDeepChild(transform, "PhysicalCardBigShadow");
			if (shadow != null) bigShadowRenderer = shadow.GetComponent<SpriteRenderer>();
		}
	}

	/// <summary>
	/// Recursive child search by name (Transform.Find only walks direct paths).
	/// </summary>
	private static Transform FindDeepChild(Transform root, string childName)
	{
		for (int i = 0; i < root.childCount; i++)
		{
			var child = root.GetChild(i);
			if (child.name == childName) return child;
			var found = FindDeepChild(child, childName);
			if (found != null) return found;
		}
		return null;
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
			// Float Stack: pending reveal entry also drives the big shadow to the anchor.
			if (_combatUXManager != null) _combatUXManager.TryDriveRevealBigShadow(this);
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
			RefreshAttackDisplay();
		}
		else
		{
			ApplyBackColor();
		}
		UpdateTintTimer();
		UpdatePendingHover();
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
			sb.Append(TagTooltipDatabaseSO.GetTagDisplayName(tag));
			sb.Append("]");
			hasVisibleTag = true;
		}

		return hasVisibleTag ? sb.ToString() : string.Empty;
	}

	/// <summary>
	/// Update the attack attribute display (bottom-right of the card face).
	/// Hidden for legacy cards with no attack; shows "X" or "X×N" (N = attack times).
	/// Reads GetAttackForDisplay() so the value stays frozen at the display snapshot
	/// during the logic phase and commits per animation request.
	/// Public so attack gains/losses (AttackChange animations) can refresh it in place.
	/// </summary>
	public void RefreshAttackDisplay()
	{
		if (cardAttackPrint == null || cardImRepresenting == null) return;

		if (!cardImRepresenting.HasAttackDisplay)
		{
			cardAttackPrint.gameObject.SetActive(false);
			return;
		}

		cardAttackPrint.gameObject.SetActive(true);
		int times = cardImRepresenting.GetAttackTimes();
		int attack = cardImRepresenting.GetAttackForDisplay();
		cardAttackPrint.text = times > 1
			? attack + "×" + times
			: attack.ToString();
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

		// R1: life display — remaining reveals this round (combat), or lifeMax on shop cards
		int lifeToShow = cardImRepresenting.currentLife;
		if (lifeToShow <= 0 && GetComponent<ShopCardView>() != null)
			lifeToShow = cardImRepresenting.lifeMax;
		if (lifeToShow > 0)
		{
			string lifeText = "<color=" + GameColorPalette.Me.damage.Hex + ">❤</color> " + lifeToShow;
			statusEffectText = string.IsNullOrEmpty(statusEffectText) ? lifeText : statusEffectText + "\n" + lifeText;
		}

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
	/// Set target scale with an explicit ease and duration (e.g. Ease.OutBack for
	/// overshoot spawn pop). Used by ShopUXManager for empty-slot spawn animation.
	/// </summary>
	public void SetTargetScale(Vector3 target, Ease easeOverride, float durationOverride, float delay = 0f)
	{
		TargetScale = target;

		// If special animation is playing, do not start DOTween
		if (isPlayingSpecialAnimation) return;

		// Start DOTween scale animation with overrides
		StartScaleTween(easeOverride, durationOverride, delay);
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
	private void StartScaleTween(Ease? easeOverride = null, float? durationOverride = null, float delay = 0f)
	{
		if (_scaleTween != null && _scaleTween.IsActive() && _scaleTween.IsPlaying())
		{
			_scaleTween.Kill();
		}

		float duration = durationOverride.HasValue ? durationOverride.Value : GetCombatScaledDuration(moveDuration);
		_scaleTween = transform.DOScale(TargetScale, duration)
			.SetEase(easeOverride.HasValue ? easeOverride.Value : moveEase)
			.SetDelay(delay)
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
		if (cardFace == null) return; // e.g. StartCardParent historically, EmptyCardSpaceParent: flip machinery skipped
		// Colors are palette-driven since 2026-08-17 (GameColorPalette statics, no serialized
		// ColorSO fields); the old null-color guard (VISUAL-FIX 2026-07-24) is obsolete.

		var faces = new System.Collections.Generic.List<Transform>();
		if (cardFace != null) faces.Add(cardFace.transform);
		if (cardImg != null) faces.Add(cardImg.transform);
		if (cardNamePrint != null) faces.Add(cardNamePrint.transform);
		if (cardDescPrint != null) faces.Add(cardDescPrint.transform);
		if (cardCostPrint != null) faces.Add(cardCostPrint.transform);
		if (cardPricePrint != null) faces.Add(cardPricePrint.transform);
		if (cardRarityPrint != null) faces.Add(cardRarityPrint.transform);
		if (cardTagPrint != null) faces.Add(cardTagPrint.transform);
		if (cardStatusEffectPrint != null) faces.Add(cardStatusEffectPrint.transform);
		if (cardAttackPrint != null) faces.Add(cardAttackPrint.transform);

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
		if (bigShadow != null)
		{
			bigShadow.SetParent(_flipRoot, true);
			// Float Stack layout: CombatUXManager drives this shadow to the deck anchor
			// while this card is revealed; auto-wire the reference here (prefab untouched).
			if (bigShadowRenderer == null)
				bigShadowRenderer = bigShadow.GetComponent<SpriteRenderer>();
		}
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
		_cardBackRenderer.color = GameColorPalette.OwnerCardColor;
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

		// Attack attribute print (bottom-right corner placeholder; real UI comes later).
		// Created at runtime so no prefab edits are needed; joins the flip faces.
		if (cardAttackPrint == null && cardDescPrint != null && cardFace != null)
		{
			var attackGo = new GameObject("AttackPrint");
			cardAttackPrint = attackGo.AddComponent<TextMeshPro>();
			cardAttackPrint.font = cardDescPrint.font;
			cardAttackPrint.fontSize = cardDescPrint.fontSize * 1.5f;
			cardAttackPrint.fontStyle = FontStyles.Bold;
			cardAttackPrint.alignment = TextAlignmentOptions.Center;
			cardAttackPrint.enableWordWrapping = false;
			var attackTransform = attackGo.transform;
			attackTransform.SetParent(_flipRoot, false);
			Vector3 faceCenter = cardFace.transform.localPosition;
			attackTransform.localPosition = new Vector3(
				faceCenter.x + cardFace.size.x * 0.5f - 0.55f,
				faceCenter.y - cardFace.size.y * 0.5f + 0.55f,
				-0.02f);
			faces.Add(attackTransform);
		}
	}

	#region Big Shadow drive (Float Stack layout)

	[Header("Big Shadow (Float Stack layout)")]
	[Tooltip("PhysicalCardBigShadow renderer. Auto-wired by name in BuildFlipRoot when left empty; CombatUXManager drives it to the deck anchor while this card is revealed in Float Stack mode.")]
	public SpriteRenderer bigShadowRenderer;

	private bool _bigShadowDriven;
	private bool _bigShadowSuppressed;
	private Vector3 _bigShadowHomeLocalPos;
	private Vector3 _bigShadowHomeLocalScale;
	private Quaternion _bigShadowHomeLocalRot;
	private float _bigShadowHomeAlpha;
	private BigShadowFollower _bigShadowFollower;

	public bool IsBigShadowDriven => _bigShadowDriven;

	/// <summary>
	/// Float Stack mode suppresses deck cards' big shadows (only the revealed card keeps
	/// one, driven to the deck anchor). Idempotent; never touches a driven shadow.
	/// </summary>
	public void SetBigShadowSuppressed(bool suppressed)
	{
		_bigShadowSuppressed = suppressed;
		if (_bigShadowDriven) return;
		if (bigShadowRenderer != null && bigShadowRenderer.gameObject.activeSelf == suppressed)
			bigShadowRenderer.gameObject.SetActive(!suppressed);
	}

	/// <summary>
	/// Begin driving the big shadow to an external anchor pose: re-parents to anchorParent
	/// keeping the current world pose (the in-deck follow pose), fades in over the first 40%
	/// of moveDuration, and attaches/inits a BigShadowFollower that tracks the revealed card's
	/// live pose every frame (plan-float-stack-shadow-follow-2026-08-16 §2). baseScale is the
	/// caller's target card world scale (reveal-card rest scale); the follower renders the
	/// shadow at the card's live scale times its own baked local scale, and keeps the rest
	/// offset proportional to the live/rest scale ratio.
	/// </summary>
	// VISUAL-FIX(2026-08-16): Float Stack big shadow grows on every play-mode Inspector edit.
	//   Cause:    targetLocalScale was t.localScale * scaleMultiplier, capturing the card's
	//             LIVE scale after SetParent(anchor, true). The first drive runs at reveal
	//             entry while the card still sits at deck-pose scale (correct), but the
	//             OnValidate re-drive captures the card's settled reveal scale
	//             (deckSize * revealScale * globalScale), compounding the revealScale factor
	//             per edit; large decks (globalScale < 1) also rendered the shadow smaller
	//             than the reveal card (an extra * globalScale).
	//   Affects:  CombatUXManager.TryDriveRevealBigShadow / OnValidate (Float Stack mode)
	//   Regress:  Float Stack mode: reveal a card, then in play mode drag floatStackShadowOffset
	//             or floatStackRevealScale — the shadow only moves / tracks the reveal card size
	//             and never grows; large decks keep the shadow equal to the reveal card; the
	//             next reveal and the return-flight fade (2026-08-15) stay unchanged.
	public void DriveBigShadowToPose(Transform anchorParent, Vector3 targetLocalPos, Vector3 baseScale, float targetAlpha, Vector3 followOffsetWorld, bool followRotation, Vector3 liftOffsetWorld)
	{
		if (bigShadowRenderer == null || anchorParent == null) return;
		if (_bigShadowDriven) RestoreBigShadowFromDrive(true);
		_bigShadowDriven = true;
		var t = bigShadowRenderer.transform;
		_bigShadowHomeLocalPos = t.localPosition;
		_bigShadowHomeLocalScale = t.localScale;
		_bigShadowHomeLocalRot = t.localRotation;
		_bigShadowHomeAlpha = bigShadowRenderer.color.a;
		t.SetParent(anchorParent, true); // keeps the in-deck world pose
		bigShadowRenderer.gameObject.SetActive(true);
		var startColor = bigShadowRenderer.color;
		startColor.a = 0f;
		bigShadowRenderer.color = startColor;
		float duration = GetCombatScaledDuration(moveDuration);
		// Follow-mode drive: the follower applies the slot-0 home pose (targetLocalPos.z = the
		// pinned front-gap z) plus the card's live offset every frame. Enable/disable (never
		// destroy + re-add): a same-frame release+drive (OnValidate re-drive) would otherwise
		// re-Init a component that is already marked for destruction.
		if (_bigShadowFollower == null)
			_bigShadowFollower = bigShadowRenderer.GetComponent<BigShadowFollower>();
		if (_bigShadowFollower == null)
			_bigShadowFollower = bigShadowRenderer.gameObject.AddComponent<BigShadowFollower>();
		// Stable size base: baseScale (reveal-card rest world scale) + the shadow's baked home
		// scale, captured at drive start — not t.localScale, which varies with the card's live
		// (possibly mid-flight) scale, so re-drives would compound the revealScale factor.
		_bigShadowFollower.Init(transform, _flipRoot, anchorParent, followOffsetWorld, baseScale, _bigShadowHomeLocalScale, targetLocalPos.z, followRotation, liftOffsetWorld, duration, moveEase);
		bigShadowRenderer.DOFade(targetAlpha, duration * 0.4f).SetUpdate(UpdateType.Normal, true);
	}

	/// <summary>
	/// Stop driving the big shadow: fade out, then re-parent back to the card and restore
	/// the recorded home pose. instant = true skips the fade and restores immediately.
	/// The follower stays ENABLED during the fade so the shadow keeps tracking the return
	/// arc while fading out; it is only disabled inside the restore callback.
	/// </summary>
	public void RestoreBigShadowFromDrive(bool instant)
	{
		if (!_bigShadowDriven || bigShadowRenderer == null) return;
		_bigShadowDriven = false;
		var t = bigShadowRenderer.transform;
		t.DOKill();
		bigShadowRenderer.DOKill();
		System.Action restore = () =>
		{
			if (this == null || bigShadowRenderer == null) return; // card destroyed mid-fade
			// Disable (not destroy) the follower BEFORE the re-parent: a same-frame
			// release+drive (OnValidate re-drive) re-Inits this same component, and a live
			// follower would otherwise overwrite the restored home pose on its next LateUpdate.
			// If the card is destroyed mid-drive, the follower self-destructs the whole shadow
			// GameObject, so no orphan cleanup is needed here.
			if (_bigShadowFollower != null) _bigShadowFollower.enabled = false;
			t.SetParent(_flipRoot != null ? _flipRoot : transform, false);
			t.localPosition = _bigShadowHomeLocalPos;
			t.localScale = _bigShadowHomeLocalScale;
			t.localRotation = _bigShadowHomeLocalRot;
			var c = bigShadowRenderer.color;
			c.a = _bigShadowHomeAlpha;
			bigShadowRenderer.color = c;
			bigShadowRenderer.gameObject.SetActive(!_bigShadowSuppressed);
		};
		if (instant)
		{
			restore();
		}
		else
		{
			bigShadowRenderer.DOFade(0f, GetCombatScaledDuration(0.25f)).SetUpdate(UpdateType.Normal, true).OnComplete(() => restore());
		}
	}

	/// <summary>
	/// Lift hook for AttackAnimationManager (wind-up/return) and RecorderAnimationPlayer
	/// (emphasize pulse): ramps the driven big shadow's anti-light lift offset
	/// (0 = rest, 1 = fully lifted). No-op unless the shadow is currently driven.
	/// </summary>
	public void SetBigShadowLift(float target, float duration)
	{
		if (!_bigShadowDriven || _bigShadowFollower == null) return;
		_bigShadowFollower.SetLift(target, duration);
	}

	#endregion
	
	/// <summary>
	/// Flip the card face-up / face-down with a 2D squash flip on FlipRoot.
	/// Rules: a card that was ever revealed is never covered again (cover calls are skipped);
	/// the Start Card never covers either (it has no hidden info and spawns face-up);
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
		// VISUAL-FIX(2026-08-02): Start Card silently became face-down, then hover pop-up stopped working
		//   Cause:    The Start Card spawns face-up WITHOUT a flip, so the early-return above
		//             (isFaceUp == faceUp) means everRevealed is never set for it. The never-cover
		//             guard only checked everRevealed, so the first cover trigger after spawn
		//             (hover SlotInCard, MoveCardWithAnimation ToBottom/ToIndex/ToTop,
		//             MoveRevealedCardToBottom) covered the Start Card, and the face-down hover
		//             gate (OnMouseEnter, Rule 1) then blocked its pop-up intermittently.
		//             Design rule: the Start Card has no hidden info and must always keep its
		//             face (InstantiateAllPhysicalCards spawn, shuffle skip) — so cover calls on
		//             it are now skipped outright (force still wins, consistent with everRevealed).
		//   Affects:  CardPhysObjScript.SetFaceUp (all cover paths), CardPhysObjScript hover
		//   Regress:  In Combat, hover-pop the Start Card and let it slot back in; also move it
		//             via Bury/Stage/Delay: it must stay face-up after every move and remain
		//             hover-poppable. Other cards' cover / never-cover behavior unchanged.
		// Revealed cards never cover again (hard rule; shuffle bypasses via force).
		if (!faceUp && !force && (everRevealed || isPhysicalStartCard))
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
		if (isPhysicalStartCard)
		{
			backColor = GameColorPalette.StartCardColor;
		}
		else if (cardImRepresenting == null || cardImRepresenting.myStatusRef == null
			|| cardImRepresenting.myStatusRef == CombatManager.Me?.ownerPlayerStatusRef)
		{
			backColor = GameColorPalette.OwnerCardColor;
		}
		else
		{
			backColor = GameColorPalette.OpponentCardColor;
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
		// Start Card has no cardImRepresenting and no ownership; its color/text come from
		// the dedicated start-card palette slots.
		if (!isPhysicalStartCard && cardImRepresenting == null)
		{
			// Shop empty-slot placeholders keep their baked prefab colors.
			return;
		}

		// Determine base color (single source: GameColorPalette)
		Color baseFaceColor;
		bool isOwner = true;

		if (isPhysicalStartCard)
		{
			baseFaceColor = GameColorPalette.StartCardColor;
		}
		else if (cardImRepresenting.myStatusRef == null)
		{
			baseFaceColor = GameColorPalette.OwnerCardColor;
		}
		else if (cardImRepresenting.myStatusRef != CombatManager.Me?.ownerPlayerStatusRef)
		{
			baseFaceColor = GameColorPalette.OpponentCardColor;
			isOwner = false;
		}
		else
		{
			baseFaceColor = GameColorPalette.OwnerCardColor;
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

		if (_currentTintIntensity > 0.01f)
		{
			Color tintColor;
			float intensity;

			switch (_currentTintState)
			{
				case TintState.Infected:
					tintColor = GameColorPalette.InfectedTintColor;
					intensity = infectedTintIntensity;
					break;
				case TintState.Power:
					tintColor = GameColorPalette.PowerTintColor;
					intensity = powerTintIntensity;
					break;
				default:
					tintColor = Color.white;
					intensity = 0f;
					break;
			}

			float appliedIntensity = intensity * _currentTintIntensity;
			finalFaceColor = Color.Lerp(baseFaceColor, baseFaceColor * tintColor, appliedIntensity);
		}

		cardFace.color = finalFaceColor;

		// Apply text color based on ownership
		Color textColor = isPhysicalStartCard ? GameColorPalette.StartCardTextColor
			: isOwner ? GameColorPalette.OwnerTextColor : GameColorPalette.OpponentTextColor;
		if (cardNamePrint != null) cardNamePrint.color = textColor;
		if (cardDescPrint != null) cardDescPrint.color = textColor;
		if (cardCostPrint != null) cardCostPrint.color = textColor;
		if (cardTagPrint != null) cardTagPrint.color = textColor;
		if (cardRarityPrint != null) cardRarityPrint.color = textColor;
		if (cardStatusEffectPrint != null) cardStatusEffectPrint.color = textColor;
		if (cardAttackPrint != null) cardAttackPrint.color = textColor;
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

	/// <summary>
	/// While true, hover is suspended (shuffle window): OnMouseEnter does not arm pending
	/// hovers and UpdatePendingHover waits. VISUAL-FIX(2026-08-16): a face-up card sweeping
	/// under a stationary cursor during the shuffle flight armed _hoverPending, which resumed
	/// after the shuffle and popped the card up without the player hovering it.
	/// </summary>
	private static bool hoverSuspended;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	private static void ResetHoverSuspensionStatics()
	{
		// Editor play sessions can skip domain reload, so statics persist between sessions;
		// a stale suspension would silently disable combat hover.
		hoverSuspended = false;
	}

	private bool _hoverActive;
	private bool _hoverPoppedUp;
	private bool _hoverPending;
	private bool _savedAutoRevealValid;
	private bool _savedAutoReveal;
	private float _hoverTooltipTimer = -1f;
	private float _hoverPopUpTimer = -1f;

	// VISUAL-FIX(2026-08-16): After a shuffle, the Start Card (the only card that stays
	//   face-up) popped up on its own.
	//   Cause:    During the shuffle flight face-up cards sweep under a stationary cursor;
	//             OnMouseEnter's blocked-state branch (isPlayingEffectAnimations / input
	//             block) queued the hover (_hoverPending) instead of skipping it. When the
	//             shuffle ended and the gates opened, UpdatePendingHover resumed the stale
	//             pending on the Start Card (it landed under the cursor, face-up) and popped
	//             it up with no new cursor interaction. Deck cards self-cleared via the
	//             face-down cover; the shuffle path itself never touched hover state.
	//   Affects:  PlayStartCardShuffleAnimation (shuffle suspend/resume), OnMouseEnter,
	//             UpdatePendingHover, RuntimeInitialize static reset
	//   Regress:  Park the cursor over the deck during the Start Card shuffle (or overtime
	//             re-shuffle) and keep it still: no card pops up after the shuffle; moving
	//             the cursor onto the face-up Start Card afterwards pops it up normally.
	/// <summary>
	/// Shuffle boundary hover reset: end the active hover, clear every combat card's pending
	/// hover, and suspend/unsuspend hover arming while the shuffle animation plays. The
	/// shuffle re-arranges every card under the cursor, so any pre-shuffle hover state is
	/// stale; arming new pending hovers mid-flight would pop cards up without a real hover.
	/// </summary>
	public static void ResetAllCombatHovers(bool suspend)
	{
		hoverSuspended = suspend;
		if (_currentHoverOwner != null)
			_currentHoverOwner.EndHover("shuffle reset");
		var mgr = CombatUXManager.me;
		if (mgr == null) return;
		if (mgr.physicalCardsInDeck != null)
		{
			for (int i = 0; i < mgr.physicalCardsInDeck.Count; i++)
			{
				var card = mgr.physicalCardsInDeck[i];
				if (card == null) continue;
				var phys = card.GetComponent<CardPhysObjScript>();
				if (phys != null) phys._hoverPending = false;
			}
		}
		if (mgr.physicalCardInRevealZone != null)
		{
			var phys = mgr.physicalCardInRevealZone.GetComponent<CardPhysObjScript>();
			if (phys != null) phys._hoverPending = false;
		}
	}

	void OnMouseEnter()
	{
		// VISUAL-FIX(2026-07-31): a moving card can re-cross a stationary cursor (its collider sweeps
		// through the cursor during pop-up/slot-in or another card's move), firing OnMouseEnter again
		// for a card that is already hovering. Re-running BeginHover would pop the card up a second
		// time mid-flight and make it climb without bound.
		if (_hoverActive)
		{
			TestManager.Log("[Hover] OnMouseEnter SKIP card=" + name + " reason=already hovering (duplicate enter)");
			return;
		}
		// VISUAL-FIX(2026-08-16): never arm pending hovers during a suspended window
		// (shuffle flight) — a card that merely sweeps under the cursor must not pop up
		// after the shuffle without a real hover.
		if (hoverSuspended)
		{
			TestManager.Log("[Hover] OnMouseEnter SKIP card=" + name + " reason=hover suspended");
			return;
		}
		// VISUAL-FIX(2026-08-02): Hovering the Start Card popped up the cards behind it
		//   Cause:    Three gaps removed the Start Card from combat hover: (1) StartCardParent.prefab
		//             had no BoxCollider2D (PhysicalCardParent/MinionPhysicalCardParent both have one),
		//             so OnMouseEnter never fired on it and it never joined the _currentHoverOwner
		//             z-arbitration; (2) the hover gates early-returned on cardImRepresenting == null,
		//             classifying the Start Card as non-hoverable; (3) the prefab's currentGamePhaseRef
		//             was unwired (null), so IsInCombatPhase() was false and BeginHover took the
		//             tooltip-only shop path — the Start Card claimed ownership (occluding cards
		//             behind it) but never popped up. Fix: prefab gained a BoxCollider2D (size copied
		//             from PhysicalCardParent) and the shared GamePhaseSO reference; the gates now
		//             exempt isPhysicalStartCard so the Start Card hovers (and pops up) like any card.
		//   Affects:  StartCardParent.prefab, CardPhysObjScript hover (OnMouseEnter, UpdatePendingHover)
		//   Regress:  In Combat, hover the Start Card: it pops up itself and cards behind it stay
		//             slotted; move the cursor from the Start Card onto a behind card's visible area:
		//             that card pops up; hover->leave->hover keeps the Start Card face-up after slot-in.
		if (cardImRepresenting == null && !isPhysicalStartCard)
		{
			TestManager.Log("[Hover] OnMouseEnter SKIP card=" + name + " reason=no cardImRepresenting");
			return;
		}
		if (!isFaceUp)
		{
			TestManager.Log("[Hover] OnMouseEnter SKIP card=" + name + " reason=face-down (Rule 1)");
			return;
		}
		// VISUAL-FIX(2026-07-31): Fast hover A->B left card B dead until cursor re-entry
		//   Cause:    (1) PopUpCard/SlotInCard hold an input block (CombatUXManager), so a fast
		//             A->B gesture rejected B's only OnMouseEnter via the combat-state gate or
		//             z-arbitration, and (2) nothing re-checked B afterwards — Unity never re-fires
		//             OnMouseEnter while the cursor stays on the card. Fixes: (a) the combat-state
		//             gate ignores PopUpCard/SlotInCard input blocks (IsInputBlockedByNonPopUp), so
		//             a newly hovered card pops up immediately while another card's pop-up/slot-in
		//             is still playing; (b) _hoverPending + UpdatePendingHover cover the remaining
		//             transient rejections (real input blocks, z-arbitration loss).
		//   Affects:  CardPhysObjScript hover (combat pop-up + tag tooltip)
		//   Regress:  Hover face-up card A, move to face-up card B mid-pop-up: B pops up
		//             immediately while A slots back in parallel; reveal-entry/shuffle/effect
		//             animations still block hover.
		if (IsHoverBlockedByCombatState())
		{
			TestManager.Log("[Hover] OnMouseEnter PENDING card=" + name + " reason=animation/input blocked");
			_hoverPending = true;
			return;
		}

		// Z arbitration: ownership transfers only to a strictly closer card;
		// equal or deeper cards under the cursor do nothing.
		if (_currentHoverOwner != null && _currentHoverOwner != this)
		{
			if (transform.position.z >= _currentHoverOwner.transform.position.z)
			{
				TestManager.Log("[Hover] OnMouseEnter PENDING card=" + name + " reason=not owner (myZ=" + transform.position.z + " ownerZ=" + _currentHoverOwner.transform.position.z + " owner=" + _currentHoverOwner.name + ")");
				_hoverPending = true;
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
		// Lazy: pending-hover cards (VISUAL-FIX 2026-07-31) never reached BeginHover, which used
		// to be the only place that assigned _hoverCollider.
		if (_hoverCollider == null)
		{
			_hoverCollider = GetComponent<Collider2D>();
			if (_hoverCollider == null) return true; // no collider: cannot test, stay hovered
		}
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
		return cm.isPlayingEffectAnimations || IsInputBlockedByNonPopUp(cm);
	}

	/// <summary>
	/// True when combat input is blocked by anything OTHER than PopUpCard/SlotInCard sequences
	/// (VISUAL-FIX 2026-07-31). Pop-up/slot-in blocks are hover-initiated (or recorder-driven,
	/// already covered by isPlayingEffectAnimations), so they must not gate hover: a newly
	/// hovered card pops up immediately while another card's pop-up/slot-in is still playing.
	/// </summary>
	private static bool IsInputBlockedByNonPopUp(CombatManager cm)
	{
		if (cm == null || !cm.IsInputBlocked) return false;
		int popUpBlocks = CombatUXManager.me != null ? CombatUXManager.me.PopUpSlotInInputBlockCount : 0;
		return cm.InputBlockCount - Mathf.Min(popUpBlocks, cm.InputBlockCount) > 0;
	}

	private void BeginHover()
	{
		_hoverActive = true;
		_hoverPending = false;
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

		// cardImRepresenting is expected to be set for the Start Card too (InstantiateAllPhysicalCards);
		// the null guard keeps a genuinely cardless physical Start Card as an occluder-only hover
		// (claims ownership, no pop-up) instead of throwing.
		if (CombatUXManager.visuals != null && cardImRepresenting != null)
		{
			float delay = CombatUXManager.me != null ? CombatUXManager.me.hoverPopUpDelay : 0.1f;
			if (delay <= 0f)
			{
				_hoverPoppedUp = true;
				CombatUXManager.visuals.PopUpCard(cardImRepresenting.gameObject);
			}
			else
			{
				// Counted down in UpdateHover; the force-hide / cursor-left checks there run
				// first, so leaving the card (or any force-hide condition) before the delay
				// elapses cancels the pop-up via EndHover.
				_hoverPopUpTimer = delay;
			}
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
		_hoverPopUpTimer = -1f;
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

	/// <summary>
	/// Retry a hover that was rejected at OnMouseEnter time by a transient gate (input block,
	/// effect animations, or z-arbitration loss) — see the VISUAL-FIX(2026-07-31) block in
	/// OnMouseEnter. Re-runs the same gates every frame; resumes through the normal ownership
	/// path once they pass. Cleared if the cursor leaves the card before the gates open.
	/// </summary>
	private void UpdatePendingHover()
	{
		if (!_hoverPending) return;
		if (hoverSuspended) return; // shuffle window: pendings were cleared by the reset; arming is skipped
		if ((cardImRepresenting == null && !isPhysicalStartCard) || !isFaceUp || !IsCursorOverCard())
		{
			TestManager.Log("[Hover] pending cleared card=" + name + " cardGone=" + (cardImRepresenting == null) + " faceUp=" + isFaceUp + " cursorOver=" + IsCursorOverCard());
			_hoverPending = false;
			return;
		}
		if (IsHoverBlockedByCombatState()) return;
		if (_currentHoverOwner != null && _currentHoverOwner != this && transform.position.z >= _currentHoverOwner.transform.position.z) return;

		if (_currentHoverOwner != null && _currentHoverOwner != this)
		{
			TestManager.Log("[Hover] pending ownership transfer " + _currentHoverOwner.name + " -> " + name);
			_currentHoverOwner.EndHover("ownership lost to " + name + " (pending)");
		}
		TestManager.Log("[Hover] pending resume card=" + name);
		_currentHoverOwner = this;
		_hoverPending = false;
		BeginHover();
	}

	private void UpdateHover()
	{
		if (!_hoverActive) return;

		// Force-hide: card flipped face-down, animation playback started, input blocked by
		// something OTHER than pop-up/slot-in sequences (those never gate hover — see
		// IsInputBlockedByNonPopUp, VISUAL-FIX 2026-07-31), or the phase changed away from
		// Combat while popped up.
		var cm = CombatManager.Me;
		bool animPlaying = cm != null && cm.isPlayingEffectAnimations;
		bool externallyBlocked = IsInputBlockedByNonPopUp(cm) && !_hoverPoppedUp;
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

		if (_hoverPopUpTimer >= 0f)
		{
			_hoverPopUpTimer -= Time.deltaTime;
			if (_hoverPopUpTimer < 0f && !_hoverPoppedUp
				&& CombatUXManager.visuals != null && cardImRepresenting != null)
			{
				TestManager.Log("[Hover] pop-up delay elapsed, PopUpCard card=" + name);
				_hoverPoppedUp = true;
				CombatUXManager.visuals.PopUpCard(cardImRepresenting.gameObject);
			}
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
