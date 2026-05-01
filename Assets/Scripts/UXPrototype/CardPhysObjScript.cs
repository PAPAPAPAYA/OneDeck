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
	public Color ownerTextColor;
	public Color opponentTextColor;

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

	// ========== Shake related ==========
	private ShakeInstance _currentShakeInstance;
	private bool _isShaking = false;

	[Header("Special Animation")]
	[Tooltip("Is playing special animation")]
	public bool isPlayingSpecialAnimation = false;
	[Tooltip("Insert animation duration")]
	public float insertDuration = 0.4f;

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

	private Tweener _positionTween;
	private Tweener _scaleTween;

	void OnEnable()
	{
		_combatUXManager = CombatUXManager.me;
	}

	void Update()
	{
		ApplyColor();
		UpdateStatusEffectDisplay();
		UpdateCostDisplay();
		UpdateRarityDisplay();
		UpdateTagDisplay();
		UpdateTintTimer();
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

	private void UpdateStatusEffectDisplay()
	{
		if (cardImRepresenting == null || cardNamePrint == null) return;

		var statusEffectText = CombatInfoDisplayer.me?.ProcessStatusEffectInfo(cardImRepresenting);
		if (!string.IsNullOrEmpty(statusEffectText))
		{
			cardNamePrint.text = "<size=12>" + statusEffectText + "\n</size><b>" + cardImRepresenting.GetDisplayName() + "</b>";
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

		// If special animation is playing, do not start DOTween
		if (isPlayingSpecialAnimation) return;

		// Start DOTween position animation
		StartPositionTween();
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
	/// Start position DOTween animation
	/// </summary>
	private void StartPositionTween()
	{
		// If already animating and target is the same, do not restart
		if (_positionTween != null && _positionTween.IsActive() && _positionTween.IsPlaying())
		{
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
	}

	#endregion

	/// <summary>
	/// Kill active DOTween tweens for position and scale.
	/// Called by CombatCardView when special animation is playing.
	/// </summary>
	public void KillTweens()
	{
		_positionTween?.Kill();
		_scaleTween?.Kill();
		_positionTween = null;
		_scaleTween = null;
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

		// Apply text color based on ownership
		Color textColor = baseFaceColor == ownerCardColor ? ownerTextColor : opponentTextColor;
		if (cardNamePrint != null) cardNamePrint.color = textColor;
		if (cardDescPrint != null) cardDescPrint.color = textColor;
		if (cardCostPrint != null) cardCostPrint.color = textColor;
		if (cardTagPrint != null) cardTagPrint.color = textColor;
		if (cardRarityPrint != null) cardRarityPrint.color = textColor;
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

	private void OnDestroy()
	{
		// Stop all DOTween animations to prevent access after object destruction
		_positionTween?.Kill();
		_scaleTween?.Kill();

		_positionTween = null;
		_scaleTween = null;
	}
}
