using System;
using System.Collections;
using System.Collections.Generic;
using DefaultNamespace;
using DG.Tweening;
using UnityEngine;
using DefaultNamespace.Managers;

public class CombatUXManager : MonoBehaviour, ICombatVisuals
{
	#region SINGLETON
	public static CombatUXManager me;
	/// <summary>
	/// Interface reference for decoupled access from logic layer.
	/// Logic scripts should use this instead of 'me'.
	/// </summary>
	public static ICombatVisuals visuals;
	void Awake()
	{
		me = this;
		visuals = this;

		_deckOffsetProvider = new DeckLayoutOffsetProvider
		{
			PositionOffsetRange = randomDeckPositionOffsetRange,
			RotationOffsetRange = randomDeckRotationOffsetRange
		};
	}
	#endregion

	[Header("REFERENCES")]
	[SerializeField] private CombatManager combatManager;
	public float zOffset;

	[Header("DECK LAYOUT OFFSET")]
	[Tooltip("Random position offset range for cards in the deck (XY = plane, Z = depth)")]
	[SerializeField] private Vector3 randomDeckPositionOffsetRange = new Vector3(0.05f, 0.05f, 0f);
	[Tooltip("Random rotation offset range for cards in the deck (mainly Z-axis in degrees)")]
	[SerializeField] private Vector3 randomDeckRotationOffsetRange = new Vector3(0f, 0f, 5f);
	[Tooltip("Re-randomize offsets after Start Card shuffle")]
	[SerializeField] private bool randomizeOffsetOnShuffle = true;
	[Tooltip("Re-randomize offset when a revealed card returns to the bottom of the deck")]
	[SerializeField] private bool randomizeOffsetOnReturnFromReveal = false;


	[Header("ANIMATION SETTINGS")]
	[Tooltip("Whether shuffle animation uses random staggered timing")]
	public bool useStaggeredShuffleAnimation = true;
	[Tooltip("Maximum random delay for shuffle animation (seconds)")]
	public float shuffleStaggerMaxDelay = 0.3f;
	[Tooltip("Deck card X-axis offset (rightward offset per card)")]
	public float xOffset;
	[Tooltip("Deck card Y-axis offset (upward offset per card)")]
	public float yOffset;

	[Header("CASCADE DECK LAYOUT")]
	[Tooltip("Enable the Smooth Curve Cascade deck layout. When false, the legacy linear fan is used byte-for-byte.")]
	public bool enableCascadeDeckLayout = true;
	[Tooltip("Demo px to world unit conversion; tune so the 150px demo card matches the current physical card width")]
	public float cascadePxToWorld = 0.01f;
	[Tooltip("Front segment length in cards (demo: 6)")]
	public int cascadeShrinkCount = 6;
	[Tooltip("Smallest card scale at the tail (demo: 0.55)")]
	public float cascadeMinScale = 0.55f;
	[Tooltip("Scale falloff steepness (demo: 2)")]
	public float cascadeScalePower = 2f;
	[Tooltip("Front spacing in demo px (demo: 60, 70)")]
	public Vector2 cascadeStartSpacing = new Vector2(60f, 70f);
	[Tooltip("Tail spacing in demo px (demo: 8, 12)")]
	public Vector2 cascadeMinSpacing = new Vector2(8f, 12f);
	[Tooltip("Spacing falloff steepness (demo: 2)")]
	public float cascadeSpacingPower = 2f;
	[Tooltip("Tail return strength (demo curveWidth: 0.55)")]
	[Range(0f, 1f)] public float cascadeTailReturn = 0.55f;
	[Tooltip("Per-component sign mirror of the canonical curve; (-1, +1) = front up-left (demo)")]
	public Vector2 cascadeDirection = new Vector2(-1f, 1f);
	[Tooltip("Mirror = tail bends toward the opposite side (demo); Same = tail keeps the front direction")]
	public CascadeTailBend cascadeTailBend = CascadeTailBend.Mirror;
	[Tooltip("Scale the position jitter by the card's cascade scale so the tail stays clean")]
	public bool cascadeScaleJitterWithCard = true;
	[Tooltip("Coverage normalization (Plan B): stretch per-card steps so small decks still reach the curve's hook")]
	public bool cascadeCoverageNormalize = true;
	[Tooltip("Target walk coverage of the curve; ~0.62 keeps the 20-card layout unchanged")]
	[Range(0.3f, 1f)] public float cascadeCoverageTarget = 0.62f;
	[Tooltip("Max step stretch factor for small decks (demo: 2.5)")]
	[Range(1f, 4f)] public float cascadeCoverageCap = 2.5f;
	[Tooltip("Reveal-zone card counts as the cascade front card (cascadeIndex 0); deck cards sit one cascade step deeper while a card is revealed")]
	public bool revealCardCountsAsDeckFront = true;

	[Header("NEW CARD")]
	public Transform physicalCardNewTempCardPos;
	public Vector3 physicalCardNewTempCardSize;

	[Header("STATUS EFFECT CONSUME")]
	[Tooltip("World position where the consumed status effect projectile should fly to. Used by ConsumeOwnStatusEffect.")]
	public Transform statusEffectConsumePos;
	[Tooltip("Duration for new card to fly in from temp pos to peak position")]
	public float newCardFlyInDuration = 0.25f;

	[Header("DECK")]
	public GameObject physicalCardPrefab;
	public GameObject startCardPhysicalPrefab; // Start Card physical prefab (different appearance)
	public GameObject minionPhysicalPrefab; // Minion card physical prefab (different appearance)
	public Transform physicalCardDeckPos;
	public Vector3 physicalCardDeckSize;

	[Header("REVEAL")]
	public Transform physicalCardRevealPos;
	public Vector3 physicalCardRevealSize;
	[Tooltip("Min z gap the reveal-zone card keeps in front of the deck's front-most card (smaller z = closer to camera). 0 = auto: uses |zOffset|.")]
	public float revealZoneZGap = 0f;
	
	[Header("REVEAL TO DECK ANIMATION")]
	[Tooltip("Midpoint when card goes from reveal zone to deck bottom (arc trajectory)")]
	public Transform showPos;
	[Tooltip("Arc trajectory animation duration")]
	public float revealToDeckAnimDuration = 0.5f;
	[Tooltip("Arc trajectory ease type")]
	public Ease revealToDeckEase = Ease.InOutQuad;
	
	[Header("DECK MOVE")]
	[Tooltip("Arc fly duration for bury/stage deck-move animations")]
	public float deckMoveArcDuration = 0.5f;
	
	[Header("DESTROY")]
	[Tooltip("Target position for card destroy animation (graveyard position)")]
	public Transform gravePosition;
	[Tooltip("Card destroy animation duration")]
	public float cardDestroyAnimDuration = 0.3f;
	[Tooltip("Target size when card is destroyed")]
	public Vector3 cardDestroyTargetSize = new Vector3(0.1f, 0.1f, 0.1f);

	[Header("POP UP / SLOT IN")]
	[Tooltip("Vertical lift distance for Pop Up animation (world units)")]
	public float popUpYOffset = 1.5f;
	[Tooltip("Z offset toward camera for Pop Up (negative = closer/frontmost)")]
	public float popUpZBoost = -1.0f;
	[Tooltip("Scale multiplier at Pop Up peak")]
	public float popUpScaleMultiplier = 1.15f;
	[Tooltip("Time to reach Pop Up peak position")]
	public float popUpDuration = 0.25f;
	[Tooltip("Time to hold at Pop Up peak position before onComplete fires")]
	public float popUpHoldDuration = 0.25f;
	[Tooltip("Easing for Pop Up movement")]
	public Ease popUpEase = Ease.OutQuad;
	[Tooltip("Time to return to deck position in Slot In")]
	public float slotInDuration = 0.35f;
	[Tooltip("Easing for Slot In movement")]
	public Ease slotInEase = Ease.InOutQuad;
	[Tooltip("Optional custom curve for Slot In. If assigned, overrides slotInEase.")]
	public AnimationCurve slotInCurve;

	[Header("DECK FOCUS / PEEL")]
	public Transform deckFocusTargetPos;
	public float peelSlideDistance = 4f;
	public float deckShiftDuration = 0.3f;
	public float peelCardDuration = 0.18f;
	public float peelStaggerDelay = 0.04f;
	[Tooltip("Distance to move reveal zone card downward out of screen when peeling starts")]
	public float revealCardExitDistance = 6f;

	[Tooltip("Enable PeelDeck focus animation during attack")]
	public bool enablePeelDeck = true;

	// Physical card list (synced with combined deck zone updates)
	public List<GameObject> physicalCardsInDeck = new();
	
	// Physical card in reveal zone (stored separately to avoid confusion with deck)
	public GameObject physicalCardInRevealZone;

	// Dictionary mapping CardScript to Physical Card (maintain this mapping)
	private Dictionary<CardScript, GameObject> _cardScriptToPhysicalCache = new();
	private bool _cardScriptCacheDirty = true;

	// Deck focus runtime state
	private bool _isDeckFocused = false;
	private CardScript _currentFocusCard = null;
	private Vector3 _deckFocusOffset = Vector3.zero;

	// Deck layout offset provider: keeps messy-deck decisions separate from pure position calculation.
	private DeckLayoutOffsetProvider _deckOffsetProvider;
	private List<GameObject> _peeledCards = new List<GameObject>();
	public bool IsDeckFocused => _isDeckFocused;

	private void OnEnable()
	{
		if (combatManager == null)
			combatManager = CombatManager.Me;
		
	}

	#region Responsibility 1: Update physical card list based on logical zone

	/// <summary>
	/// Update physicalCardsInDeck order based on combined deck zone
	/// Note: Cards in revealZone are not added to this list, managed separately by physicalCardInRevealZone
	/// </summary>
	public void SyncPhysicalCardsWithCombinedDeck()
	{
		if (physicalCardsInDeck.Count == 0 && physicalCardInRevealZone == null)
		{
			// Debug.Log("[CombatUXManager] SyncPhysicalCardsWithCombinedDeck SKIPPED (empty)");
			return;
		}

		string deckBefore = "";
		for (int i = 0; i < physicalCardsInDeck.Count; i++)
		{
			deckBefore += "[" + i + "]" + physicalCardsInDeck[i].name + " ";
		}
		TestManager.Log("[CombatUXManager] SyncPhysicalCardsWithCombinedDeck START deckBefore=" + deckBefore);

		// Rebuild dictionary (includes cards from deck and reveal zone)
		BuildCardScriptToPhysicalDictionary();

		// Reorder physicalCardsInDeck based on combinedDeckZone
		physicalCardsInDeck.Clear();
		foreach (var logicalCard in combatManager.combinedDeckZone)
		{
			var cardScript = logicalCard.GetComponent<CardScript>();
			if (cardScript != null && _cardScriptToPhysicalCache.TryGetValue(cardScript, out var physicalCard))
			{
				physicalCardsInDeck.Add(physicalCard);
			}
		}

		// Note: Cards in reveal zone are not added to physicalCardsInDeck
		// It is managed separately by physicalCardInRevealZone, position controlled by separate reveal logic

		// FIX: If a card is in logical revealZone but not in physical reveal zone, sync it
		// (e.g. StageEffect sets revealZone directly without notifying UXManager)
		if (combatManager.revealZone != null)
		{
			var revealCardScript = combatManager.revealZone.GetComponent<CardScript>();
			if (revealCardScript != null && _cardScriptToPhysicalCache.TryGetValue(revealCardScript, out var revealPhysicalCard))
			{
				physicalCardsInDeck.Remove(revealPhysicalCard);
				physicalCardInRevealZone = revealPhysicalCard;
			}
		}

		string deckList = "";
		for (int i = 0; i < physicalCardsInDeck.Count; i++)
		{
			deckList += "[" + i + "]" + physicalCardsInDeck[i].name + " ";
		}
		TestManager.Log("[CombatUXManager] SyncPhysicalCardsWithCombinedDeck done. deckCount=" + physicalCardsInDeck.Count + " revealZone=" + (physicalCardInRevealZone != null ? physicalCardInRevealZone.name : "null") + " deck=" + deckList);
	}

	/// <summary>
	/// Move card from deck to reveal zone
	/// </summary>
	public void MovePhysicalCardToRevealZone(GameObject physicalCard, Action onComplete = null)
	{
		var physScript = physicalCard != null ? physicalCard.GetComponent<CardPhysObjScript>() : null;

		// Debug.Log("[CombatUXManager] MovePhysicalCardToRevealZone physical=" + (physicalCard != null ? physicalCard.name : "null") + " deckCountBefore=" + physicalCardsInDeck.Count);
		
		// Remove from deck
		physicalCardsInDeck.Remove(physicalCard);
		InvalidateCardScriptCache();

		// Store to reveal zone
		physicalCardInRevealZone = physicalCard;

		// Set reveal position
		if (physScript != null)
		{
			// If card is playing special animation, set pending reveal zone move
			// so that when special animation finishes, it goes directly to reveal zone
			// Reveal zone cards should be displayed cleanly without deck layout offset
			physScript.SetRotationImmediate(Quaternion.identity);

			if (physScript.isPlayingSpecialAnimation)
			{
				physScript.pendingRevealZoneMove = true;
				physScript.pendingRevealPosition = GetRevealZonePosition();
				physScript.pendingRevealScale = physicalCardRevealSize;
				onComplete?.Invoke();
			}
			else
			{
				// VISUAL-FIX(2026-06-19): Block input and auto-reveal during reveal-zone flight
				//   Cause:    When a card in reveal zone is exiled, the next card is auto-revealed.
				//             If autoReveal is enabled, the card could be triggered while still
				//             tweening to the reveal zone, causing visual mismatch.
				//   Fix:      Treat reveal-zone movement like effect animations: block input and
				//             set isPlayingEffectAnimations until the card reaches reveal zone.
				//   Affects:  CombatUXManager, CombatManager, RevealCards flow
				//   Regress:  Exile the revealed card with autoReveal enabled; verify the next card
				//             reaches reveal zone before its effect triggers. Also verify normal
				//             reveal and Round Start Start Card shuffle still work.
				//   Related:  PRD exile-reveal-zone-lock-2026-06-19
				bool wasAlreadyLocked = combatManager != null && combatManager.isPlayingEffectAnimations;
				if (!wasAlreadyLocked && combatManager != null)
				{
					BlockInput(this);
					combatManager.isPlayingEffectAnimations = true;
				}

				Action wrappedOnComplete = () =>
				{
					if (!wasAlreadyLocked && combatManager != null)
					{
						UnblockInput(this);
						combatManager.isPlayingEffectAnimations = false;
					}
					onComplete?.Invoke();
				};

				physScript.SetTargetPosition(GetRevealZonePosition(), wrappedOnComplete);
				physScript.SetTargetScale(physicalCardRevealSize);
				physScript.SetTargetRotation(Quaternion.identity);
			}
		}
		else
		{
			onComplete?.Invoke();
		}
		// Update positions of remaining cards in deck
		UpdateAllPhysicalCardTargets();
	}

	/// <summary>
	/// Wait for special animation to finish, then move card to reveal zone
	/// </summary>
	private System.Collections.IEnumerator MoveToRevealZoneWhenReady(CardPhysObjScript physScript)
	{
		float timeout = 2f;
		float timer = 0f;
		while (physScript.isPlayingSpecialAnimation && timer < timeout)
		{
			yield return null;
			timer += Time.deltaTime;
		}
		physScript.SetTargetPosition(GetRevealZonePosition());
		physScript.SetTargetScale(physicalCardRevealSize);
	}

	/// <summary>
	/// Move card from reveal zone back to bottom of deck
	/// Use arc trajectory through showPos
	/// </summary>
	/// <param name="card">Logical card GameObject</param>
	/// <param name="onComplete">Animation complete callback (optional)</param>
	public void MoveRevealedCardToBottom(GameObject card, Action onComplete = null)
	{
		GameObject physicalCard;

		// Determine if input is physical or logical card
		var cardScript = card.GetComponent<CardScript>();
		if (cardScript == null)
		{
			// Debug.LogWarning($"MoveRevealedCardToBottom: Card {card.name} has no CardScript");
			onComplete?.Invoke();
			return;
		}

		// Find physical card from logical card
		BuildCardScriptToPhysicalDictionary();
		physicalCard = GetPhysicalCardFromLogicalCard(cardScript);
		if (physicalCard == null)
		{
			// Debug.LogWarning($"MoveRevealedCardToBottom: Could not find physical card for {card.name}");
			onComplete?.Invoke();
			return;
		}

		// Clear reveal zone reference
		if (physicalCardInRevealZone == physicalCard)
		{
			physicalCardInRevealZone = null;
		}

		// Add to bottom of deck (index 0)
		physicalCardsInDeck.Insert(0, physicalCard);
		InvalidateCardScriptCache();
		// Debug.Log("[CombatUXManager] MoveRevealedCardToBottom inserted " + physicalCard.name + " at index 0 deckCount=" + physicalCardsInDeck.Count);

		var physScript = physicalCard.GetComponent<CardPhysObjScript>();

		// If showPos is configured, use universal animation system
		if (showPos != null)
		{
			// [Key Fix] When calculating target position, consider that one card will be revealed
			// At this time physicalCardsInDeck contains the card about to be revealed, but it will be removed when animation completes
			// So effectiveCount = physicalCardsInDeck.Count - 1 is needed to calculate correct position
			int effectiveCount = physicalCardsInDeck.Count - 1;
			if (effectiveCount < 1) effectiveCount = 1; // At least 1 to avoid calculation errors
			// Cascade + reveal-front counting: the -1 above (next card leaving for the reveal zone)
			// and the +1 from that card occupying the cascade front slot cancel out,
			// so the returned card lands at the deepest slot of the unchanged curve.
			if (enableCascadeDeckLayout && revealCardCountsAsDeckFront)
				effectiveCount = physicalCardsInDeck.Count;
			
			// VISUAL-FIX(2026-07-17): Reveal-to-bottom must land on the cascade curve, not the raw linear fan.
			//   Cause:    This site duplicated the linear formula inline (xOffset*(effectiveCount-1)),
			//             bypassing the DeckPositionCalculator seam. Rerouted through the calculator
			//             (index 0 = deck bottom); legacy path is numerically identical.
			//   Affects:  MoveRevealedCardToBottom (second-click reveal-to-bottom arc).
			//   Regress:  Reveal a card and click again; the card arcs to the cascade tail end
			//             (or the legacy linear bottom when enableCascadeDeckLayout = false).
			Vector3 targetPos = DeckPositionCalculator.CalculatePositionAtIndex(
				0, effectiveCount, physicalCardDeckPos.position, xOffset, yOffset, zOffset, BuildCascadeConfig());

			// Apply per-card layout offset (scaled down for deep cascade cards)
			if (physScript != null)
			{
				if (randomizeOffsetOnReturnFromReveal)
				{
					_deckOffsetProvider.AssignOffset(physScript);
				}
				targetPos += _deckOffsetProvider.GetPositionOffset(physScript) * GetCascadeJitterScale(0, effectiveCount);
			}
			// Debug.Log("[CombatUXManager] MoveRevealedCardToBottom targetPos=" + targetPos + " effectiveCount=" + effectiveCount);

			Action wrappedOnComplete = () =>
			{
				if (physScript != null)
				{
					physScript.SetTargetRotation(GetFinalDeckRotationForCard(physScript));
				}
				onComplete?.Invoke();
			};

			var config = new CardMoveConfig
			{
				moveType = CardMoveType.ToPosition, // Use ToPosition to apply the corrected position
				customTarget = targetPos,
				duration = CombatAnimationSpeed.ScaleDuration(revealToDeckAnimDuration),
				useArc = true,
				arcMidpoint = showPos,
				ease = revealToDeckEase,
				// Cascade: land at the deepest tail scale (uniform deck size when the flag is off)
				targetScaleOverride = GetDeckScaleAtIndex(0, effectiveCount),
				onComplete = wrappedOnComplete
			};
			MoveCardWithAnimation(card, config);
		}
		else
		{
			// showPos not configured, use normal animation
			UpdateAllPhysicalCardTargets();
			onComplete?.Invoke();
		}
	}

	#endregion

	#region Universal card move animation system

	/// <summary>
	/// Universal card move method - move card based on configuration
	/// </summary>
	/// <param name="logicalCard">Logical card GameObject</param>
	/// <param name="config">Move config</param>
	public void MoveCardWithAnimation(GameObject logicalCard, CardMoveConfig config)
	{
		if (logicalCard == null || config == null) return;

		var cardScript = logicalCard.GetComponent<CardScript>();
		if (cardScript == null) return;

		// Get physical card
		BuildCardScriptToPhysicalDictionary();
		var physicalCard = GetPhysicalCardFromLogicalCard(cardScript);
		if (physicalCard == null)
		{
			// Debug.LogWarning("[CombatUXManager] MoveCardWithAnimation physicalCard NOT FOUND for " + logicalCard.name);
			return;
		}

		var physScript = physicalCard.GetComponent<CardPhysObjScript>();
		if (physScript == null) return;

		// Kill existing tweens from UpdateAllPhysicalCardTargets to prevent conflict with special animation
		physScript.KillTweens();

		// Calculate target position
		Vector3 targetPosition;
		switch (config.moveType)
		{
			case CardMoveType.ToTop:
				// if reveal zone is me, move to reveal zone instead of deck top
				if (combatManager.revealZone == physScript.cardImRepresenting.gameObject)
				{
					targetPosition = GetRevealZonePosition();
				}
				else
				{
					targetPosition = GetFinalDeckPositionForCard(physScript, physicalCardsInDeck.Count - 1);
				}
				break;
			case CardMoveType.ToBottom:
			{
				targetPosition = GetFinalDeckPositionForCard(physScript, 0);
				break;
			}
			case CardMoveType.ToIndex:
				targetPosition = GetFinalDeckPositionForCard(physScript, config.targetIndex);
				break;
			case CardMoveType.ToPosition:
				targetPosition = config.customTarget ?? physicalCard.transform.position;
				break;
			case CardMoveType.ToGrave:
				targetPosition = gravePosition != null ? gravePosition.position : physicalCard.transform.position;
				break;
			default:
				targetPosition = physicalCard.transform.position;
				break;
		}

		// Determine arc midpoint
		Transform arcPoint = config.arcMidpoint ?? showPos;
		bool shouldUseArc = config.useArc && arcPoint != null && config.moveType != CardMoveType.ToGrave;

		// Callback: Animation start
		config.onStart?.Invoke();

		// Mark that special animation is playing
		physScript.isPlayingSpecialAnimation = true;

		// Block input during effect animation
		BlockInput(this);

		AnimationStateTracker.me?.RegisterAnimation();

		int actualPhysIndex = physicalCardsInDeck.IndexOf(physicalCard);
		TestManager.Log("[CombatUXManager] MoveCardWithAnimation START logical=" + logicalCard.name + " moveType=" + config.moveType + " targetIndex=" + config.targetIndex + " targetPos=" + targetPosition + " physical=" + physicalCard.name + " actualPhysIndex=" + actualPhysIndex + " deckCount=" + physicalCardsInDeck.Count + " physCurrentPos=" + physicalCard.transform.position + " isPending=" + physScript.isPendingSlotIn);

		// Create animation sequence
		Sequence moveSequence = DOTween.Sequence();
		float scaledDuration = CombatAnimationSpeed.ScaleDuration(config.duration);

		if (shouldUseArc)
		{
			// Arc trajectory: Current -> Midpoint -> Target
			// VISUAL-FIX(2026-06-14): showPos z was fixed at -80, causing cards to jump far away mid-arc.
			//   Fix: use midpoint z between current card and target, keep showPos x/y.
			//   Affects: MoveCardToTop, MoveCardToBottom, MoveCardToIndex, MoveRevealedCardToBottom.
			//   Regress: Stage/Bury/Reveal-to-bottom animations should remain visible and land in correct order.
			float halfDuration = scaledDuration * 0.5f;
			Vector3 arcMidpoint = GetArcMidpoint(arcPoint.position, physicalCard.transform.position, targetPosition);
			moveSequence.Append(
				physicalCard.transform.DOMove(arcMidpoint, halfDuration).SetEase(config.ease)
			);
			moveSequence.Append(
				physicalCard.transform.DOMove(targetPosition, halfDuration).SetEase(config.ease)
			);
		}
		else
		{
			// Straight trajectory
			moveSequence.Append(
				physicalCard.transform.DOMove(targetPosition, scaledDuration).SetEase(config.ease)
			);
		}

		// VISUAL-FIX(2026-07-17): Deck-bound moves must land at the card's cascade depth scale.
		//   Cause:    Every move tweened scale to the uniform physicalCardDeckSize, stomping the
		//             per-index cascade scale (card visibly "breathes" after landing).
		//   Affects:  MoveCardWithAnimation (Bury/Stage/Delay ToTop/ToBottom/ToIndex), ToPosition override.
		//   Regress:  With cascade on, Bury/Stage a card; its scale lands at its depth scale directly.
		Vector3 targetScale;
		switch (config.moveType)
		{
			case CardMoveType.ToGrave:
				targetScale = cardDestroyTargetSize;
				break;
			case CardMoveType.ToTop:
				// Reveal-zone bound subcase keeps the legacy uniform deck size;
				// the completion handler swaps in pendingRevealScale anyway.
				targetScale = (combatManager.revealZone == physScript.cardImRepresenting.gameObject)
					? physicalCardDeckSize
					: GetDeckScaleAtIndex(physicalCardsInDeck.Count - 1);
				break;
			case CardMoveType.ToBottom:
				targetScale = GetDeckScaleAtIndex(0);
				break;
			case CardMoveType.ToIndex:
				targetScale = GetDeckScaleAtIndex(config.targetIndex);
				break;
			default: // ToPosition
				targetScale = config.targetScaleOverride ?? physicalCardDeckSize;
				break;
		}
		moveSequence.Join(
			physicalCard.transform.DOScale(targetScale, scaledDuration).SetEase(config.ease)
		);

		// Animation complete callback
		moveSequence.OnComplete(() =>
		{
			// Debug.Log("[CombatUXManager] MoveCardWithAnimation COMPLETE logical=" + logicalCard.name + " moveType=" + config.moveType + " targetIndex=" + config.targetIndex + " finalPos=" + physicalCard.transform.position + " finalTargetPos=" + targetPosition);
			AnimationStateTracker.me?.CompleteAnimation();
			UnblockInput(this);

			physScript.isPlayingSpecialAnimation = false;
			physScript.isPoppedUp = false;

			// If pending reveal zone move, go directly to reveal zone instead of default target
			if (physScript.pendingRevealZoneMove)
			{
				physScript.SetTargetPosition(physScript.pendingRevealPosition);
				physScript.SetTargetScale(physScript.pendingRevealScale);
				physScript.pendingRevealZoneMove = false;
			}
			else
			{
				physScript.SetTargetPosition(targetPosition);
				physScript.SetTargetScale(targetScale);
				// Apply deck layout rotation only when landing back in the deck
				bool landingInRevealZone = combatManager.revealZone == physScript.cardImRepresenting.gameObject;
				if (config.moveType != CardMoveType.ToGrave
				    && config.moveType != CardMoveType.ToPosition
				    && !landingInRevealZone)
				{
					physScript.SetTargetRotation(GetFinalDeckRotationForCard(physScript));
				}
			}

			if (config.destroyAfterMove)
			{
				Destroy(physicalCard);
			}

			config.onComplete?.Invoke();
		});

		moveSequence.Play();
	}

	/// <summary>
	/// Move card to top of deck
	/// </summary>
	public void MoveCardToTop(GameObject logicalCard, float duration = 0.5f, bool useArc = true, Action onComplete = null)
	{
		MoveCardWithAnimation(logicalCard, CardMoveConfig.ToTop(duration, useArc, onComplete));
	}

	/// <summary>
	/// Move card to bottom of deck
	/// </summary>
	public void MoveCardToBottom(GameObject logicalCard, float duration = 0.5f, bool useArc = true, Action onComplete = null)
	{
		MoveCardWithAnimation(logicalCard, CardMoveConfig.ToBottom(duration, useArc, onComplete));
	}

	/// <summary>
	/// Move card to specified index position
	/// </summary>
	public void MoveCardToIndex(GameObject logicalCard, int index, float duration = 0.5f, bool useArc = true, Action onComplete = null)
	{
		// Debug.Log("[CombatUXManager] MoveCardToIndex called logical=" + (logicalCard != null ? logicalCard.name : "null") + " requestedIndex=" + index + " deckCount=" + physicalCardsInDeck.Count);
		MoveCardWithAnimation(logicalCard, CardMoveConfig.ToIndex(index, duration, useArc, onComplete));
	}

	/// <summary>
	/// Returns showPos's x/y but with z set to the midpoint between start and target.
	/// Used for arc trajectory so cards don't fly to the fixed scene z of showPos.
	/// </summary>
	/// <summary>
	/// Returns a midpoint whose x/y come from baseMidpointPosition but z is the midpoint
	/// between startPosition and targetPosition. Used for arc trajectory so cards don't fly
	/// to the fixed scene z of showPos.
	/// </summary>
	private Vector3 GetArcMidpoint(Vector3 baseMidpointPosition, Vector3 startPosition, Vector3 targetPosition)
	{
		Vector3 mid = baseMidpointPosition;
		mid.z = (startPosition.z + targetPosition.z) * 0.5f;
		return mid;
	}


	/// <summary>
	/// Batch animation: arc via showPos to pop-up peak, then slot in to deck top.
	/// Phase 1: all cards arc in parallel to their pop-up peaks.
	/// Phase 2: all cards slot in in parallel to their final deck top positions.
	/// </summary>
	/// <remarks>
	/// targetIndices here are the FINAL deck indices (computed by RecorderAnimationPlayer
	/// after ApplyAnimationResult). This method cannot resolve indices itself because
	/// Phase 1 needs every card's final position up-front to calculate pop-up peaks.
	/// </remarks>
	public void MoveCardToTopPopUpBatch(List<GameObject> logicalCards, List<int> targetIndices,
	    float duration, Action onComplete = null)
	{
		if (logicalCards == null || logicalCards.Count == 0)
		{
			onComplete?.Invoke();
			return;
		}

		int totalCount = logicalCards.Count;
		int phase1Done = 0;
		int phase2Done = 0;

		// VISUAL-FIX(2026-06-09): MoveCardToTopPopUpBatch registers N animations but only completes 1
		//   Cause:    RegisterAnimation and BlockInput were inside the Phase 1 loop (called per card),
		//             but CompleteAnimation and UnblockInput were only called once when the whole batch finished.
		//             This caused AnimationStateTracker.pending to grow and IsInputBlocked to stay true forever.
		//   Affects:  MoveCardToTopPopUpBatch, AnimationStateTracker, CombatManager.IsInputBlocked
		//   Regress:  Stage 2+ cards (e.g. BOOSTER afterShuffle→Stage); verify pending returns to 0
		//             and input is unblocked after animation completes.
		// Register a single animation batch for the entire pop-up + slot-in operation.
		// Do NOT call per-card: RegisterAnimation/BlockInput must balance with CompleteAnimation/UnblockInput.
		AnimationStateTracker.me?.RegisterAnimation();
		BlockInput(this);

		// Phase 1: Arc to pop-up peak (parallel)
		for (int i = 0; i < totalCount; i++)
		{
			var logicalCard = logicalCards[i];
			int finalIndex = targetIndices[i];

			var cardScript = logicalCard.GetComponent<CardScript>();
			if (cardScript == null) { phase1Done++; phase2Done++; continue; }

			BuildCardScriptToPhysicalDictionary();
			var physicalCard = GetPhysicalCardFromLogicalCard(cardScript);
			if (physicalCard == null) { phase1Done++; phase2Done++; continue; }

			var physScript = physicalCard.GetComponent<CardPhysObjScript>();
			if (physScript == null) { phase1Done++; phase2Done++; continue; }

			physScript.KillTweens();
			physScript.isPlayingSpecialAnimation = true;

			// Compute peak from FINAL deck position
			Vector3 deckPos = CalculateAnimationPositionAtIndex(finalIndex);
			Vector3 peakPos = deckPos + Vector3.up * popUpYOffset;
			peakPos.z += popUpZBoost;
			Vector3 peakScale = physicalCardDeckSize * popUpScaleMultiplier;

			// Arc via showPos
			Sequence arcSeq = DOTween.Sequence();
			float scaledDuration = CombatAnimationSpeed.ScaleDuration(duration);
			float halfDuration = scaledDuration * 0.5f;

			if (showPos != null)
			{
				// VISUAL-FIX(2026-06-14): showPos z fixed at -80 caused Stage arc to jump far away.
				//   Fix: arc midpoint z = midpoint of current card z and peak z.
				Vector3 arcMidpoint = GetArcMidpoint(showPos.position, physicalCard.transform.position, peakPos);
				arcSeq.Append(physicalCard.transform.DOMove(arcMidpoint, halfDuration).SetEase(Ease.OutQuad));
				arcSeq.Append(physicalCard.transform.DOMove(peakPos, halfDuration).SetEase(Ease.InOutQuad));
			}
			else
			{
				// showPos is null: straight line to peak
				arcSeq.Append(physicalCard.transform.DOMove(peakPos, scaledDuration).SetEase(Ease.OutQuad));
			}

			arcSeq.Join(physicalCard.transform.DOScale(peakScale, scaledDuration).SetEase(Ease.OutQuad));

			arcSeq.OnComplete(() =>
			{
				phase1Done++;
				if (phase1Done >= totalCount)
				{
					// All cards reached peak — start Phase 2
					StartSlotInPhase();
				}
			});
			arcSeq.Play();
		}

		void StartSlotInPhase()
		{
			for (int i = 0; i < totalCount; i++)
			{
				var logicalCard = logicalCards[i];
				int finalIndex = targetIndices[i];

				var cardScript = logicalCard.GetComponent<CardScript>();
				if (cardScript == null) { phase2Done++; continue; }

				var physicalCard = GetPhysicalCardFromLogicalCard(cardScript);
				if (physicalCard == null) { phase2Done++; continue; }

				var physScript = physicalCard.GetComponent<CardPhysObjScript>();
				if (physScript == null) { phase2Done++; continue; }

				// Cascade: GetFinalDeckPositionForCard consolidates layout + scale-aware jitter;
				// slot-in must land at the card's cascade depth scale, not the uniform deck size.
				Vector3 targetPos = GetFinalDeckPositionForCard(physScript, finalIndex);
				Quaternion targetRot = GetFinalDeckRotationForCard(physScript);
				Vector3 targetScale = GetDeckScaleAtIndex(finalIndex);
				float scaledSlotInDuration = CombatAnimationSpeed.ScaleDuration(slotInDuration);

				Sequence slotSeq = DOTween.Sequence();
				slotSeq.Append(ApplySlotInEase(physicalCard.transform.DOMove(targetPos, scaledSlotInDuration)));
				slotSeq.Join(ApplySlotInEase(physicalCard.transform.DOScale(targetScale, scaledSlotInDuration)));
				slotSeq.Join(ApplySlotInEase(physicalCard.transform.DOLocalRotate(targetRot.eulerAngles, scaledSlotInDuration)));
				slotSeq.OnComplete(() =>
				{
					physScript.isPlayingSpecialAnimation = false;
					physScript.isPoppedUp = false;
					physScript.SetTargetPosition(targetPos);
					physScript.SetTargetScale(targetScale);
					physScript.SetTargetRotation(targetRot);

					phase2Done++;
					if (phase2Done >= totalCount)
					{
						AnimationStateTracker.me?.CompleteAnimation();
						UnblockInput(this);
						onComplete?.Invoke();
					}
				});
				slotSeq.Play();
			}
		}
	}

	#region Cascade Deck Layout helpers

	/// <summary>
	/// Build the pure-math cascade params from the serialized Inspector fields.
	/// </summary>
	private DeckCascadeLayout.Params BuildCascadeLayoutParams()
	{
		return new DeckCascadeLayout.Params
		{
			shrinkCount = cascadeShrinkCount,
			minScale = cascadeMinScale,
			scalePower = cascadeScalePower,
			startSpacingX = cascadeStartSpacing.x,
			startSpacingY = cascadeStartSpacing.y,
			minSpacingX = cascadeMinSpacing.x,
			minSpacingY = cascadeMinSpacing.y,
			spacingPower = cascadeSpacingPower,
			tailReturn = cascadeTailReturn,
			tailBendSign = cascadeTailBend == CascadeTailBend.Mirror ? 1f : -1f,
			arcSamples = 300,
			coverageNormalize = cascadeCoverageNormalize,
			coverageTarget = cascadeCoverageTarget,
			coverageCap = cascadeCoverageCap
		};
	}

	/// <summary>
	/// Build the cascade config carrier for DeckPositionCalculator. Null-safe:
	/// when enableCascadeDeckLayout is false the calculator runs the legacy linear formula.
	/// </summary>
	private DeckPositionCalculator.CascadeConfig BuildCascadeConfig()
	{
		return new DeckPositionCalculator.CascadeConfig
		{
			enabled = enableCascadeDeckLayout,
			pxToWorld = cascadePxToWorld,
			direction = cascadeDirection,
			layoutParams = BuildCascadeLayoutParams()
		};
	}

	/// <summary>
	/// Effective cascade deck count. With revealCardCountsAsDeckFront on, the reveal-zone card
	/// occupies cascadeIndex 0 (the front slot), so every deck card sits one cascade step deeper.
	/// Legacy linear path is unaffected: all cascade helpers early-return when cascade is disabled.
	/// </summary>
	// VISUAL-FIX(2026-07-17): Deck re-laid out (slide + curve reshape) on every reveal/return cycle.
	//   Cause:    Cascade count excluded the reveal-zone card; revealing dropped the count by 1,
	//             recomputing the whole curve and shifting every remaining card one step forward.
	//   Affects:  All cascade position/scale/jitter callers (layout, popup peaks, slot-in, peel focus).
	//   Regress:  With the toggle on, revealing a card must NOT move the rest of the deck;
	//             returning it to the bottom slides the deck forward exactly one step.
	//             Toggle revealCardCountsAsDeckFront off to restore the legacy per-reveal re-layout.
	private int GetCascadeDeckCount()
	{
		int count = physicalCardsInDeck.Count;
		if (enableCascadeDeckLayout && revealCardCountsAsDeckFront && physicalCardInRevealZone != null)
			count++;
		return count;
	}

	/// <summary>
	/// Deck scale for the card at the given unity deck index (0 = bottom, count-1 = top).
	/// Cascade mode: physicalCardDeckSize * per-depth cascade scale. Legacy mode: uniform physicalCardDeckSize.
	/// </summary>
	public Vector3 GetDeckScaleAtIndex(int unityIndex)
	{
		return GetDeckScaleAtIndex(unityIndex, GetCascadeDeckCount());
	}

	/// <summary>
	/// Count-parameterized overload for callers computing against a future deck size
	/// (e.g. MoveRevealedCardToBottom uses effectiveCount before the reveal resolves).
	/// </summary>
	private Vector3 GetDeckScaleAtIndex(int unityIndex, int count)
	{
		if (!enableCascadeDeckLayout) return physicalCardDeckSize;
		if (count <= 0) return physicalCardDeckSize;
		int clamped = Mathf.Clamp(unityIndex, 0, count - 1);
		int cascadeIndex = count - 1 - clamped;
		return physicalCardDeckSize * DeckCascadeLayout.ComputeScale(cascadeIndex, count, BuildCascadeLayoutParams());
	}

	/// <summary>
	/// Jitter amplitude multiplier for a deck index. Cascade mode scales the position jitter
	/// by the card's cascade scale so the tight tail spacing does not look ragged.
	/// </summary>
	private float GetCascadeJitterScale(int unityIndex, int count)
	{
		if (!enableCascadeDeckLayout || !cascadeScaleJitterWithCard || count <= 1) return 1f;
		int clamped = Mathf.Clamp(unityIndex, 0, count - 1);
		int cascadeIndex = count - 1 - clamped;
		return DeckCascadeLayout.ComputeScale(cascadeIndex, count, BuildCascadeLayoutParams());
	}

	#endregion

	/// <summary>
	/// Calculate position coordinates at specified index using full deck count.
	/// Use this for global deck layout updates (UpdateAllPhysicalCardTargets, shuffle, focus).
	/// Pending cards are included because they still physically occupy space in the deck.
	/// </summary>
	public Vector3 CalculatePositionAtIndex(int index)
	{
		var count = GetCascadeDeckCount();
		var basePos = physicalCardDeckPos.position + _deckFocusOffset;
		Vector3 result = DeckPositionCalculator.CalculatePositionAtIndex(
			index, count, basePos, xOffset, yOffset, zOffset, BuildCascadeConfig());
		TestManager.Log("[CombatUXManager] CalculatePositionAtIndex index=" + index + " count=" + count + " result=" + result);
		return result;
	}

	/// <summary>
	/// Calculate position coordinates at specified index for animation target positions.
	/// Uses full deck count because pending cards still occupy slots in the final layout.
	/// All callers pass logical indices based on combinedDeckZone.Count, so deckCount
	/// must match that full count to avoid index/count mismatch.
	/// </summary>
	// VISUAL-FIX(2026-05-24): Stage/Bury peak and slot-in position offset when pending cards exist
	//   Cause:    CalculateAnimationPositionAtIndex used activeCount excluding pending cards,
	//             but callers pass logical indices based on full deck size. This caused
	//             index/count mismatch: e.g. index=2 with deckCount=2 produced wrong position.
	//   Affects:  MoveCardToTopPopUpBatch, MoveCardToIndex, MoveCardToBottom, MoveCardWithAnimation
	//   Regress:  Reveal sacrificial_spirit (creates pending JU_ON) then soldier_skeleton (StageSelf);
	//             verify SOLDIER_SKELETON's peak and slot-in positions match its logical top index.
	//   Related:  sacrificial_spirit + soldier_skeleton, any card that creates pending then stages
	private Vector3 CalculateAnimationPositionAtIndex(int index)
	{
		int fullCount = GetCascadeDeckCount();
		var basePos = physicalCardDeckPos.position + _deckFocusOffset;
		Vector3 result = DeckPositionCalculator.CalculatePositionAtIndex(
			index, fullCount, basePos, xOffset, yOffset, zOffset, BuildCascadeConfig());
		TestManager.Log("[CombatUXManager] CalculateAnimationPositionAtIndex index=" + index + " fullCount=" + fullCount + " result=" + result);
		return result;
	}

	/// <summary>
	/// Calculate position for a pending card (e.g. PopUp / SlotIn).
	/// Uses full deck count because pending cards still occupy slots in the final layout.
	/// </summary>
	// VISUAL-FIX(2026-05-24): Pending cards (RIFT/AddTempCard) have wrong pop-up peak and slot-in position
	//   Cause:    CalculateAnimationPositionAtIndex uses activeCount which excludes pending cards.
	//             Pending cards need full deck count to reflect their actual position in final layout.
	//   Affects:  AddTempCard, PopUp, SlotIn, MoveToPopUpPosition, CalculatePositionForPendingCard
	//   Regress:  Play RIFT_INSECT or BLACKSMITH; verify new card's pop-up peak and slot-in
	//             target match its logical deck index within the complete deck.
	//   Related:  RIFT_INSECT, BLACKSMITH

	/// <summary>
	/// Get the final deck position for a specific card, including its layout offset.
	/// Cascade mode scales the position jitter by the card's cascade scale (cascadeScaleJitterWithCard).
	/// </summary>
	private Vector3 GetFinalDeckPositionForCard(CardPhysObjScript physScript, int index)
	{
		Vector3 basePos = CalculatePositionAtIndex(index);
		if (physScript == null) return basePos;
		return basePos + _deckOffsetProvider.GetPositionOffset(physScript) * GetCascadeJitterScale(index, GetCascadeDeckCount());
	}



	/// <summary>
	/// Re-randomize layout offsets for every card currently in the deck.
	/// </summary>
	private void RandomizeDeckLayoutOffsetsForAllCards()
	{
		for (int i = 0; i < physicalCardsInDeck.Count; i++)
		{
			var card = physicalCardsInDeck[i];
			if (card == null) continue;
			var physScript = card.GetComponent<CardPhysObjScript>();
			if (physScript == null) continue;
			_deckOffsetProvider.AssignOffset(physScript);
		}
	}
	/// <summary>
	/// Apply the current deck layout rotation to every card in the deck.
	/// Called after shuffle or any other full-deck reset.
	/// </summary>
	private void ApplyDeckLayoutRotationToAllCards()
	{
		for (int i = 0; i < physicalCardsInDeck.Count; i++)
		{
			var card = physicalCardsInDeck[i];
			if (card == null) continue;
			var physScript = card.GetComponent<CardPhysObjScript>();
			if (physScript == null) continue;
			physScript.SetTargetRotation(GetFinalDeckRotationForCard(physScript));
		}
	}
	/// <summary>
	/// Get the final deck rotation for a specific card, including its layout offset.
	/// </summary>
	private Quaternion GetFinalDeckRotationForCard(CardPhysObjScript physScript)
	{
		if (physScript == null) return Quaternion.identity;
		return _deckOffsetProvider.GetRotationOffset(physScript);
	}
	private Vector3 CalculatePositionForPendingCard(int index)
	{
		int fullCount = GetCascadeDeckCount();
		var basePos = physicalCardDeckPos.position + _deckFocusOffset;
		Vector3 result = DeckPositionCalculator.CalculatePositionAtIndex(
			index, fullCount, basePos, xOffset, yOffset, zOffset, BuildCascadeConfig());
		TestManager.Log("[CombatUXManager] CalculatePositionForPendingCard index=" + index + " fullCount=" + fullCount + " result=" + result);
		return result;
	}

	public void PlayStartCardShuffleAnimation(GameObject startCard, List<GameObject> shuffledCards, Action onComplete)
	{
		// Block player input
		BlockInput(this);
			
		// Get Start Card physical card
		BuildCardScriptToPhysicalDictionary();
		var startPhysicalCard = GetPhysicalCardFromLogicalCard(startCard.GetComponent<CardScript>());
		
		// Remove Start Card from reveal zone
		if (physicalCardInRevealZone == startPhysicalCard)
		{
			physicalCardInRevealZone = null;
		}

		// 1. Calculate target position for each card (based on known shuffle result)
		var shuffleTargets = CalculateShuffleTargets(shuffledCards, out var shuffleIndices);

		// 2. Play move animations for all cards simultaneously
		// Start Card flies from Reveal Zone directly to new position
		// Other cards fly from current position to new position
		PlayShuffleAnimationInternal(shuffleTargets, shuffleIndices, () =>
		{
			// 3. After animation completes, rebuild physical card list to match logical order
			RebuildPhysicalDeckFromShuffledList(shuffledCards);

			// Re-randomize messy-deck offsets if configured, then sync rotations
			if (randomizeOffsetOnShuffle)
			{
				RandomizeDeckLayoutOffsetsForAllCards();
			}
			ApplyDeckLayoutRotationToAllCards();
			UpdateAllPhysicalCardTargets();

			// Restore player input
			UnblockInput(this);
			onComplete?.Invoke();
		});
	}

	/// <summary>
	/// Rebuild physical card list based on shuffled logical list
	/// </summary>
	private void RebuildPhysicalDeckFromShuffledList(List<GameObject> shuffledCards)
	{
		physicalCardsInDeck.Clear();
		
		foreach (var logicalCard in shuffledCards)
		{
			var cardScript = logicalCard.GetComponent<CardScript>();
			if (cardScript != null && _cardScriptToPhysicalCache.TryGetValue(cardScript, out var physicalCard))
			{
				physicalCardsInDeck.Add(physicalCard);
			}
		}
		InvalidateCardScriptCache();
	}

	/// <summary>
	/// Calculate target position for each card after Shuffle.
	/// Also outputs each card's shuffled deck index so the animation can tween
	/// to the card's cascade depth scale (uniform when cascade is disabled).
	/// </summary>
	/// <param name="shuffledCards">Card order after shuffle</param>
	/// <param name="shuffleIndices">Output: shuffled deck index per physical card</param>
	/// <returns>Target position for each physical card</returns>
	private Dictionary<GameObject, Vector3> CalculateShuffleTargets(List<GameObject> shuffledCards, out Dictionary<GameObject, int> shuffleIndices)
	{
		var targets = new Dictionary<GameObject, Vector3>();
		shuffleIndices = new Dictionary<GameObject, int>();

		for (int i = 0; i < shuffledCards.Count; i++)
		{
			var logicalCard = shuffledCards[i].GetComponent<CardScript>();
			if (logicalCard == null) continue;

			var physicalCard = GetPhysicalCardFromLogicalCard(logicalCard);
			if (physicalCard == null) continue;

			var physScript = physicalCard.GetComponent<CardPhysObjScript>();
			targets[physicalCard] = GetFinalDeckPositionForCard(physScript, i);
			shuffleIndices[physicalCard] = i;
		}

		return targets;
	}

	/// <summary>
	/// Internal method: Play Shuffle move animation
	/// </summary>
	/// <param name="shuffleTargets">Target position for each physical card</param>
	/// <param name="shuffleIndices">Shuffled deck index per physical card (for cascade scale)</param>
	/// <param name="onComplete">Callback after animation completes</param>
	private void PlayShuffleAnimationInternal(Dictionary<GameObject, Vector3> shuffleTargets, Dictionary<GameObject, int> shuffleIndices, Action onComplete)
	{
		if (shuffleTargets.Count == 0)
		{
			onComplete?.Invoke();
			return;
		}

		AnimationStateTracker.me?.RegisterAnimation();

		int completedCount = 0;
		int totalCount = shuffleTargets.Count;
		float shuffleDuration = CombatAnimationSpeed.ScaleDuration(0.5f); // Shuffle Animation duration
		float scaledShuffleStaggerMaxDelay = CombatAnimationSpeed.ScaleDuration(shuffleStaggerMaxDelay);

		// Generate random delay time for each card
		var cardDelays = new Dictionary<GameObject, float>();
		foreach (var kvp in shuffleTargets)
		{
			float delay = useStaggeredShuffleAnimation ? UnityEngine.Random.Range(0f, scaledShuffleStaggerMaxDelay) : 0f;
			cardDelays[kvp.Key] = delay;
		}

		foreach (var kvp in shuffleTargets)
		{
			var physicalCard = kvp.Key;
			var targetPos = kvp.Value;
			float delay = cardDelays[physicalCard];

			if (physicalCard == null) 
			{
				completedCount++;
				if (completedCount >= totalCount)
					onComplete?.Invoke();
				continue;
			}

			var physScript = physicalCard.GetComponent<CardPhysObjScript>();
			if (physScript != null)
			{
				physScript.isPlayingSpecialAnimation = true;
			}

			// Use arc trajectory or direct move
			Sequence moveSequence = DOTween.Sequence();

			// Add random delay (if using staggered animation)
			if (delay > 0)
			{
				moveSequence.AppendInterval(delay);
			}

			if (showPos != null)
			{
				// Use arc trajectory through showPos
				// VISUAL-FIX(2026-06-14): showPos z fixed at -80 caused shuffle arc to jump far away.
				//   Fix: arc midpoint z = midpoint of current card z and target z.
				Vector3 arcMidpoint = GetArcMidpoint(showPos.position, physicalCard.transform.position, targetPos);
				moveSequence.Append(
					physicalCard.transform.DOMove(arcMidpoint, shuffleDuration * 0.5f).SetEase(Ease.OutQuad)
				);
				moveSequence.Append(
					physicalCard.transform.DOMove(targetPos, shuffleDuration * 0.5f).SetEase(Ease.InQuad)
				);
			}
			else
			{
				// Direct move
				moveSequence.Append(
					physicalCard.transform.DOMove(targetPos, shuffleDuration).SetEase(Ease.InOutQuad)
				);
			}

			// Sync scale: cascade mode tweens each card to its own depth scale (uniform when disabled)
			Vector3 targetScale = (shuffleIndices != null && shuffleIndices.TryGetValue(physicalCard, out int deckIndex))
				? GetDeckScaleAtIndex(deckIndex)
				: physicalCardDeckSize;
			moveSequence.Join(
				physicalCard.transform.DOScale(targetScale, shuffleDuration).SetEase(Ease.InOutQuad)
			);

			moveSequence.OnComplete(() =>
			{
				if (physScript != null)
				{
					physScript.isPlayingSpecialAnimation = false;
					physScript.isPoppedUp = false;
					physScript.SetTargetPosition(targetPos);
					physScript.SetTargetScale(targetScale);
				}

				completedCount++;
				if (completedCount >= totalCount)
				{
					AnimationStateTracker.me?.CompleteAnimation();
					onComplete?.Invoke();
				}
			});

			moveSequence.Play();
		}
	}

	#endregion

	#region Responsibility 1 extension: Card reset and sync

	/// <summary>
	/// Reset all cards (used for new round start)
	/// </summary>
	public void ReviveAllPhysicalCards()
	{
		// If any card is still in reveal zone, first move back to bottom of deck (index 0)
		if (physicalCardInRevealZone != null)
		{
			physicalCardsInDeck.Insert(0, physicalCardInRevealZone);
			physicalCardInRevealZone = null;
			InvalidateCardScriptCache();
		}

		// Only update target positions, do not sort (sorting is handled by SyncPhysicalCardsWithCombinedDeck during shuffle)
		UpdateAllPhysicalCardTargets();
	}

	#endregion

	#region Responsibility 2: Maintain CardScript to Physical Card dictionary

	/// <summary>
	/// Build CardScript -> Physical Card mapping from deck.
	/// Skips rebuild if cache is still valid.
	/// </summary>
	public void BuildCardScriptToPhysicalDictionary()
	{
		if (!_cardScriptCacheDirty) return;
		_cardScriptCacheDirty = false;
		_cardScriptToPhysicalCache.Clear();

		foreach (var physicalCard in physicalCardsInDeck)
		{
			var physCardScript = physicalCard.GetComponent<CardPhysObjScript>();
			if (physCardScript?.cardImRepresenting != null)
			{
				_cardScriptToPhysicalCache[physCardScript.cardImRepresenting] = physicalCard;
			}
		}

		// Include card in reveal zone
		if (physicalCardInRevealZone != null)
		{
			var physCardScript = physicalCardInRevealZone.GetComponent<CardPhysObjScript>();
			if (physCardScript?.cardImRepresenting != null)
			{
				_cardScriptToPhysicalCache[physCardScript.cardImRepresenting] = physicalCardInRevealZone;
			}
		}
	}

	/// <summary>
	/// Mark the CardScript -> Physical Card cache as needing a rebuild.
	/// Call this after any modification to physicalCardsInDeck or physicalCardInRevealZone.
	/// </summary>
	private void InvalidateCardScriptCache()
	{
		_cardScriptCacheDirty = true;
	}

	/// <summary>
	/// Get physical card from logical card.
	/// Automatically rebuilds cache if it is dirty.
	/// </summary>
	public GameObject GetPhysicalCardFromLogicalCard(CardScript logicalCard)
	{
		if (_cardScriptCacheDirty)
			BuildCardScriptToPhysicalDictionary();
		
		if (_cardScriptToPhysicalCache.TryGetValue(logicalCard, out var physicalCard))
			return physicalCard;
		return null;
	}

	#endregion

	#region Responsibility 3: Tell Physical Card target position based on list order

	// VISUAL-FIX(2026-07-18): Reveal-zone card occluded by the deck once deck count grows large
	//   Cause:    Deck z = basePos.z - zOffset*index (smaller z = closer to camera), so the deck
	//             front card moves toward the camera as deck count grows, while the reveal-zone
	//             position z was a fixed transform value. Large decks end up in front of the
	//             revealed card.
	//   Affects:  GetRevealZonePosition, all reveal-zone position callers,
	//             UpdateAllPhysicalCardTargets (continuous tracking half of this fix)
	//   Regress:  Small deck: reveal card sits exactly at physicalCardRevealPos (z unchanged).
	//             Large deck (30+ cards, or grown mid-combat via AddTempCard): reveal card stays
	//             revealZoneZGap in front of the deck front card. Peel focus exit/restore and
	//             Start Card shuffle reveal behave as before.
	/// <summary>
	/// Reveal zone position with z clamped in front of the deck's front-most card (smallest z).
	/// Min-clamp only: small decks keep the configured z byte-for-byte; large decks stay
	/// revealZoneZGap in front of the front card. Scans TargetPosition (tween-final values),
	/// mirroring the backMostZ scan in AddPhysicalCardToDeck. Cards mid-animation
	/// (popup peak, pending slot-in) are excluded: only cards resting in the deck occlude.
	/// </summary>
	public Vector3 GetRevealZonePosition()
	{
		Vector3 revealPos = physicalCardRevealPos.position;

		float frontMostZ = float.MaxValue;
		foreach (var card in physicalCardsInDeck)
		{
			if (card == null) continue;
			var phys = card.GetComponent<CardPhysObjScript>();
			if (phys == null) continue;
			if (phys.isPlayingSpecialAnimation || phys.isPendingSlotIn || phys.isPoppedUp) continue;
			frontMostZ = Mathf.Min(frontMostZ, phys.TargetPosition.z);
		}
		if (frontMostZ == float.MaxValue)
			return revealPos; // Deck empty (or fully mid-animation): no occlusion possible

		float gap = revealZoneZGap > 0.0001f ? revealZoneZGap : Mathf.Abs(zOffset);
		revealPos.z = Mathf.Min(revealPos.z, frontMostZ - gap);
		return revealPos;
	}

	/// <summary>
	/// Update all cards' target positions based on physicalCardsInDeck order
	/// </summary>
	public void UpdateAllPhysicalCardTargets()
	{
		// Guard: skip update when deck is focused to prevent interference
		if (_isDeckFocused)
		{
			return;
		}
		TestManager.Log("[CombatUXManager] UpdateAllPhysicalCardTargets START deckCount=" + physicalCardsInDeck.Count);
		// Update card positions in deck
		for (int i = 0; i < physicalCardsInDeck.Count; i++)
		{
			var card = physicalCardsInDeck[i];
			var physScript = card.GetComponent<CardPhysObjScript>();
			if (physScript == null) continue;

			// Calculate target position including per-card layout offset
			Vector3 targetPos = GetFinalDeckPositionForCard(physScript, i);
			Quaternion targetRot = GetFinalDeckRotationForCard(physScript);

			TestManager.Log("[CombatUXManager] UpdateAllPhysicalCardTargets card=" + card.name + " index=" + i + " isPending=" + physScript.isPendingSlotIn + " currentPos=" + card.transform.position + " targetPos=" + targetPos);

			// Set target position, rotation and scale (card handles animation in its own Update)
			// Cascade mode: per-index depth scale (uniform physicalCardDeckSize when disabled)
			physScript.SetTargetPosition(targetPos);
			physScript.SetTargetScale(GetDeckScaleAtIndex(i));
			physScript.SetTargetRotation(targetRot);
		}
		// VISUAL-FIX(2026-07-18): continuous half of the reveal-zone occlusion fix (see above).
		// Deck count changes mid-combat (AddTempCard, Stage, Bury...), so the reveal card's
		// target z is re-clamped on every deck layout update. Front-only (never push back),
		// and skipped while a position tween is playing: StartPositionTween kills a restarted
		// tween without firing its completion callback (reveal-entry input unblock would be lost).
		if (physicalCardInRevealZone != null)
		{
			var revealPhys = physicalCardInRevealZone.GetComponent<CardPhysObjScript>();
			if (revealPhys != null && !revealPhys.isPlayingSpecialAnimation && !revealPhys.IsPositionTweenPlaying)
			{
				float clampedZ = GetRevealZonePosition().z;
				Vector3 revealTarget = revealPhys.TargetPosition;
				if (clampedZ < revealTarget.z - 0.0001f)
				{
					revealTarget.z = clampedZ;
					revealPhys.SetTargetPosition(revealTarget);
				}
			}
		}
		TestManager.Log("[CombatUXManager] UpdateAllPhysicalCardTargets END");
	}

	/// <summary>
	/// Apply animation result to physical deck ordering so subsequent animations
	/// see the correct intermediate state.
	/// </summary>
	public void ApplyAnimationResult(AnimationRequest request)
	{
		if (request == null) return;

		string deckBefore = "";
		for (int i = 0; i < physicalCardsInDeck.Count; i++)
		{
			var p = physicalCardsInDeck[i].GetComponent<CardPhysObjScript>();
			deckBefore += "[" + i + "]" + physicalCardsInDeck[i].name + "(pending=" + (p != null && p.isPendingSlotIn) + ") ";
		}
		string targetIndicesStr = request.targetIndices != null ? string.Join(",", request.targetIndices) : "null";
		TestManager.Log("[CombatUXManager] ApplyAnimationResult START type=" + request.type + " targetIndices=" + targetIndicesStr + " snapshotDeckSize=" + request.snapshotDeckSize + " deckBefore=" + deckBefore);

		switch (request.type)
		{
			case AnimationRequestType.MoveToBottomBatch:
				if (request.targetCards == null) break;
				foreach (var card in request.targetCards)
				{
					var phys = GetPhysicalCard(card);
					if (phys != null)
					{
						physicalCardsInDeck.Remove(phys);
						// VISUAL-FIX(2026-05-24): ApplyAnimationResult inserts moved cards before pending slot-in cards
						//   Cause:    Pending cards from AddTempCard (later chain) are still in physicalCardsInDeck
						//             but must be skipped when inserting cards moved by bury/stage/apply.
						//   Affects:  ApplyAnimationResult, MoveToBottomBatch, MoveToTopBatch, MoveToBottom, MoveToTop
						//   Regress:  Chain AddTempCard then Bury in same combat; verify buried card lands after pending cards
						//   Related:  RIFT_INSECT + any Bury card
						int insertIndex = 0;
						for (int i = 0; i < physicalCardsInDeck.Count; i++)
						{
							var pendingPhys = physicalCardsInDeck[i].GetComponent<CardPhysObjScript>();
							if (pendingPhys != null && !pendingPhys.isPendingSlotIn)
							{
								break;
							}
							insertIndex = i + 1;
						}
						physicalCardsInDeck.Insert(insertIndex, phys);
						// Debug.Log("[CombatUXManager] ApplyAnimationResult MoveToBottomBatch inserted " + phys.name + " at index=" + insertIndex + " (skipped " + insertIndex + " pending cards)");
					}
					else
					{
						// Debug.LogWarning("[CombatUXManager] ApplyAnimationResult MoveToBottomBatch physical not found for " + card.name);
					}
				}
				break;
			case AnimationRequestType.MoveToTopBatch:
				if (request.targetCards == null) break;
				foreach (var card in request.targetCards)
				{
					var phys = GetPhysicalCard(card);
					if (phys != null)
					{
						physicalCardsInDeck.Remove(phys);
						// VISUAL-FIX(2026-05-24): ApplyAnimationResult appends moved cards before pending slot-in cards
						//   Cause:    Same as MoveToBottomBatch skip logic: pending cards must not block append position.
						//   Affects:  ApplyAnimationResult, MoveToTopBatch, MoveToTop
						//   Regress:  Same as MoveToBottomBatch skip logic
						//   Related:  RIFT_INSECT + any Stage card
						int appendIndex = physicalCardsInDeck.Count;
						for (int i = physicalCardsInDeck.Count - 1; i >= 0; i--)
						{
							var pendingPhys = physicalCardsInDeck[i].GetComponent<CardPhysObjScript>();
							if (pendingPhys != null && !pendingPhys.isPendingSlotIn)
							{
								appendIndex = i + 1;
								break;
							}
						}
						physicalCardsInDeck.Insert(appendIndex, phys);
						// Debug.Log("[CombatUXManager] ApplyAnimationResult MoveToTopBatch inserted " + phys.name + " at index=" + appendIndex + " (skipped " + (physicalCardsInDeck.Count - 1 - appendIndex) + " pending cards)");
					}
					else
					{
						// Debug.LogWarning("[CombatUXManager] ApplyAnimationResult MoveToTopBatch physical not found for " + card.name);
					}
				}
				break;
			case AnimationRequestType.MoveToTopPopUpBatch:
				// Same as MoveToTopBatch: remove each target card and append after non-pending cards
				if (request.targetCards == null) break;
				foreach (var card in request.targetCards)
				{
					var phys = GetPhysicalCard(card);
					if (phys != null)
					{
						physicalCardsInDeck.Remove(phys);
						int appendIndex = physicalCardsInDeck.Count;
						for (int i = physicalCardsInDeck.Count - 1; i >= 0; i--)
						{
							var pendingPhys = physicalCardsInDeck[i].GetComponent<CardPhysObjScript>();
							if (pendingPhys != null && !pendingPhys.isPendingSlotIn)
							{
								appendIndex = i + 1;
								break;
							}
						}
						physicalCardsInDeck.Insert(appendIndex, phys);
					}
				}
				break;
			case AnimationRequestType.MoveToBottom:
				if (request.targetCard != null)
				{
					var phys = GetPhysicalCard(request.targetCard);
					if (phys != null)
					{
						physicalCardsInDeck.Remove(phys);
						// Skip over cards pending their own slot-in animation
						int insertIndex = 0;
						for (int i = 0; i < physicalCardsInDeck.Count; i++)
						{
							var pendingPhys = physicalCardsInDeck[i].GetComponent<CardPhysObjScript>();
							if (pendingPhys != null && !pendingPhys.isPendingSlotIn)
							{
								break;
							}
							insertIndex = i + 1;
						}
						physicalCardsInDeck.Insert(insertIndex, phys);
						// Debug.Log("[CombatUXManager] ApplyAnimationResult MoveToBottom inserted " + phys.name + " at index=" + insertIndex);
					}
				}
				break;
			case AnimationRequestType.MoveToTop:
				if (request.targetCard != null)
				{
					var phys = GetPhysicalCard(request.targetCard);
					if (phys != null)
					{
						physicalCardsInDeck.Remove(phys);
						// Append after all non-special-animation cards
						int appendIndex = physicalCardsInDeck.Count;
						for (int i = physicalCardsInDeck.Count - 1; i >= 0; i--)
						{
							var pendingPhys = physicalCardsInDeck[i].GetComponent<CardPhysObjScript>();
							if (pendingPhys != null && !pendingPhys.isPendingSlotIn)
							{
								appendIndex = i + 1;
								break;
							}
						}
						physicalCardsInDeck.Insert(appendIndex, phys);
						// Debug.Log("[CombatUXManager] ApplyAnimationResult MoveToTop inserted " + phys.name + " at index=" + appendIndex);
					}
				}
				break;
			case AnimationRequestType.MoveToIndex:
				if (request.targetCard != null)
				{
					var phys = GetPhysicalCard(request.targetCard);
					if (phys != null)
					{
						physicalCardsInDeck.Remove(phys);
						int idx = Mathf.Clamp(request.targetIndex, 0, physicalCardsInDeck.Count);
						physicalCardsInDeck.Insert(idx, phys);
						// Debug.Log("[CombatUXManager] ApplyAnimationResult MoveToIndex inserted " + phys.name + " at index=" + idx + " requested=" + request.targetIndex);
					}
				}
				break;
			case AnimationRequestType.Destroy:
				if (request.targetCard != null)
				{
					var phys = GetPhysicalCard(request.targetCard);
					if (phys != null)
					{
						physicalCardsInDeck.Remove(phys);
						// Debug.Log("[CombatUXManager] ApplyAnimationResult Destroy removed " + phys.name);
					}
				}
				break;
			case AnimationRequestType.Shuffle:
				// Update physicalCardsInDeck order to match shuffled logical order.
				// Actual transform movement is handled by PlayShuffleAnimationInternal.
				RebuildPhysicalDeckFromShuffledList(request.targetCards);
				break;
		}

		InvalidateCardScriptCache();

		string deckAfter = "";
		for (int i = 0; i < physicalCardsInDeck.Count; i++)
		{
			var p = physicalCardsInDeck[i].GetComponent<CardPhysObjScript>();
			deckAfter += "[" + i + "]" + physicalCardsInDeck[i].name + "(pending=" + (p != null && p.isPendingSlotIn) + ") ";
		}
		TestManager.Log("[CombatUXManager] ApplyAnimationResult END deckCount=" + physicalCardsInDeck.Count + " deckAfter=" + deckAfter);
	}

	#endregion

	#region Deck Focus / Peel System

	/// <summary>
	/// Focus on a card in the deck by peeling cards in front of it
	/// </summary>
	public IEnumerator FocusOnCardCoroutine(CardScript targetCard)
	{

		if (!enablePeelDeck)
		{

			yield break;
		}
		if (targetCard == null)
		{

			yield break;
		}

		// Get physical card
		BuildCardScriptToPhysicalDictionary();
		GameObject physicalCard = GetPhysicalCardFromLogicalCard(targetCard);
		if (physicalCard == null)
		{
			// Debug.LogWarning("[CombatUXManager] FocusOnCardCoroutine: No physical card found for " + targetCard.name);
			yield break;
		}

		int targetIndex = GetPhysicalCardDeckIndex(physicalCard);
		if (targetIndex < 0)
		{

			yield break;
		}

		// Already focused on this card
		if (_isDeckFocused && _currentFocusCard == targetCard)
		{

			yield break;
		}

		TestManager.Log("[CombatUXManager] FocusOnCardCoroutine START target=" + targetCard.name + " targetIndex=" + targetIndex + " isFocused=" + _isDeckFocused + " currentFocus=" + (_currentFocusCard != null ? _currentFocusCard.name : "null") + " time=" + Time.time);

		if (!_isDeckFocused)
		{

			// Start new peel
			yield return StartCoroutine(StartPeelCoroutine(targetIndex));
		}
		else
		{
			// Transition focus to different card
			GameObject currentPhysical = GetPhysicalCardFromLogicalCard(_currentFocusCard);
			int currentIndex = currentPhysical != null ? GetPhysicalCardDeckIndex(currentPhysical) : -1;

			if (currentIndex < 0)
			{
				// Current focus card no longer in deck, start fresh

				yield return StartCoroutine(StartPeelCoroutine(targetIndex));
			}
			else
			{

				yield return StartCoroutine(TransitionFocusCoroutine(targetIndex, currentIndex));
			}
		}

		_currentFocusCard = targetCard;
		_isDeckFocused = true;

		TestManager.Log("[CombatUXManager] FocusOnCardCoroutine END target=" + targetCard.name + " time=" + Time.time);

	}

	/// <summary>
	/// Start peeling deck to expose target card at specified index
	/// </summary>
	private IEnumerator StartPeelCoroutine(int targetIndex)
	{

		TestManager.Log("[CombatUXManager] StartPeelCoroutine START targetIndex=" + targetIndex + " time=" + Time.time);
		
		var count = physicalCardsInDeck.Count;
		if (count == 0 || targetIndex < 0 || targetIndex >= count)
			yield break;

		// Mark deck as focused early to prevent UpdateAllPhysicalCardTargets from interfering during peel
		_isDeckFocused = true;

		AnimationStateTracker.me?.RegisterAnimation();

		// Compute deck focus offset first so reveal zone can follow the same offset
		// VISUAL-FIX(2026-07-17): Peel focus must derive from the cascade position, not raw linear math.
		//   Cause:    noOffsetX/Y duplicated the linear formula inline, bypassing the layout seam;
		//             in cascade mode the focused card missed deckFocusTargetPos.
		//   Affects:  StartPeelCoroutine (deck focus offset computation).
		//   Regress:  Trigger an off-reveal Attack with cascade on; the focused card lands on deckFocusTargetPos.
		float desiredX = deckFocusTargetPos != null ? deckFocusTargetPos.position.x : physicalCardDeckPos.position.x;
		// Layout count includes the reveal-zone card as the cascade front slot (GetCascadeDeckCount);
		// the local 'count' stays the physical list size for bounds checks and card iteration.
		Vector3 noOffsetPos = DeckPositionCalculator.CalculatePositionAtIndex(
			targetIndex, GetCascadeDeckCount(), physicalCardDeckPos.position, xOffset, yOffset, zOffset, BuildCascadeConfig());
		float offsetX = desiredX - noOffsetPos.x;
		float offsetY = physicalCardDeckPos.position.y - noOffsetPos.y;
		_deckFocusOffset = new Vector3(offsetX, offsetY, 0f);

		// Animate all cards to their final positions (parallel) including reveal zone exit
		int animCompletedCount = 0;
		int animTotalCount = 0;

		// Move reveal zone card out of screen downward (respecting deck offset to keep relative XY unchanged)
		if (physicalCardInRevealZone != null)
		{

			var revealPhysScript = physicalCardInRevealZone.GetComponent<CardPhysObjScript>();
			if (revealPhysScript != null)
			{
				revealPhysScript.isPlayingSpecialAnimation = true;
				revealPhysScript.SetRotationImmediate(Quaternion.identity);
			}

			Vector3 offsetRevealPos = GetRevealZonePosition() + _deckFocusOffset;
			Vector3 exitPos = offsetRevealPos + Vector3.down * revealCardExitDistance;
			animTotalCount++;
			physicalCardInRevealZone.transform.DOMove(exitPos, CombatAnimationSpeed.ScaleDuration(peelCardDuration))
				.SetEase(Ease.InOutQuad)
				.OnComplete(() =>
				{
					animCompletedCount++;
				});
		}

		_peeledCards.Clear();

		for (int i = 0; i < count; i++)
		{
			var card = physicalCardsInDeck[i];
			if (card == null) continue;
			var physScript = card.GetComponent<CardPhysObjScript>();
			if (physScript == null) continue;

			Vector3 finalPos = GetFinalDeckPositionForCard(physScript, i);
			bool willPeel = i > targetIndex;

			if (willPeel)
			{
				// Peel this card
				_peeledCards.Add(card);

				Vector3 peelDirection = new Vector3(0f, -1f, 0f).normalized;
				Vector3 peelPos = finalPos + peelDirection * peelSlideDistance;

				float peelDelay = CombatAnimationSpeed.ScaleDuration((count - i) * peelStaggerDelay);
				animTotalCount++;
				physScript.isPlayingSpecialAnimation = true;
				card.transform.DOMove(peelPos, CombatAnimationSpeed.ScaleDuration(peelCardDuration))
					.SetEase(Ease.InOutQuad)
					.SetDelay(peelDelay)
					.OnComplete(() =>
					{
						animCompletedCount++;
						// Ensure TargetPosition is synced to peelPos before releasing special animation flag
						physScript.SetTargetPosition(peelPos);
						physScript.SetTargetRotation(GetFinalDeckRotationForCard(physScript));
						physScript.isPlayingSpecialAnimation = false;
					});
			}
			else
			{
				// Shift this card to deck position
				animTotalCount++;
				physScript.isPlayingSpecialAnimation = true;
				physScript.SetTargetPosition(finalPos);
				physScript.SetTargetRotation(GetFinalDeckRotationForCard(physScript));
				card.transform.DOMove(finalPos, CombatAnimationSpeed.ScaleDuration(deckShiftDuration))
					.SetEase(Ease.OutQuad)
					.OnComplete(() =>
					{
						animCompletedCount++;
						physScript.isPlayingSpecialAnimation = false;
					});
			}
		}

		yield return new WaitUntil(() => animCompletedCount >= animTotalCount);

		AnimationStateTracker.me?.CompleteAnimation();

		TestManager.Log("[CombatUXManager] StartPeelCoroutine END targetIndex=" + targetIndex + " time=" + Time.time);
	}

	/// <summary>
	/// Transition focus from one card to another
	/// </summary>
	private IEnumerator TransitionFocusCoroutine(int newTargetIndex, int currentTargetIndex)
	{

		TestManager.Log("[CombatUXManager] TransitionFocusCoroutine START newIndex=" + newTargetIndex + " currentIndex=" + currentTargetIndex + " time=" + Time.time);

		var count = physicalCardsInDeck.Count;
		if (count == 0 || newTargetIndex < 0 || newTargetIndex >= count)
			yield break;

		AnimationStateTracker.me?.RegisterAnimation();

		// Recompute deck focus offset for new target
		// VISUAL-FIX(2026-07-17): Focus transition must derive from the cascade position (same as StartPeelCoroutine).
		//   Affects:  TransitionFocusCoroutine (deck focus offset recomputation).
		//   Regress:  Chain two off-reveal effects with cascade on; focus lands on deckFocusTargetPos both times.
		float desiredX = deckFocusTargetPos != null ? deckFocusTargetPos.position.x : physicalCardDeckPos.position.x;
		// Layout count includes the reveal-zone card as the cascade front slot (GetCascadeDeckCount);
		// the local 'count' stays the physical list size for bounds checks and card iteration.
		Vector3 noOffsetPos = DeckPositionCalculator.CalculatePositionAtIndex(
			newTargetIndex, GetCascadeDeckCount(), physicalCardDeckPos.position, xOffset, yOffset, zOffset, BuildCascadeConfig());
		float offsetX = desiredX - noOffsetPos.x;
		float offsetY = physicalCardDeckPos.position.y - noOffsetPos.y;
		_deckFocusOffset = new Vector3(offsetX, offsetY, 0f);

		// Determine which cards should be peeled at the end
		var shouldBePeeled = new HashSet<GameObject>();
		for (int i = newTargetIndex + 1; i < count; i++)
		{
			if (physicalCardsInDeck[i] != null)
				shouldBePeeled.Add(physicalCardsInDeck[i]);
		}

		// Animate all cards to their final positions (parallel)
		int animCompletedCount = 0;
		int animTotalCount = 0;

		var newPeeledCards = new List<GameObject>();

		for (int i = 0; i < count; i++)
		{
			var card = physicalCardsInDeck[i];
			if (card == null) continue;
			var physScript = card.GetComponent<CardPhysObjScript>();
			if (physScript == null) continue;

			Vector3 finalPos = GetFinalDeckPositionForCard(physScript, i);
			physScript.SetTargetPosition(finalPos);
			physScript.SetTargetRotation(GetFinalDeckRotationForCard(physScript));

			if (shouldBePeeled.Contains(card))
			{
				// This card should be peeled
				Vector3 peelDirection = new Vector3(0f, -1f, 0f).normalized;
				Vector3 peelPos = finalPos + peelDirection * peelSlideDistance;

				newPeeledCards.Add(card);

				float transDelay = CombatAnimationSpeed.ScaleDuration((count - 1 - i) * peelStaggerDelay);
				animTotalCount++;
				physScript.isPlayingSpecialAnimation = true;
				card.transform.DOMove(peelPos, CombatAnimationSpeed.ScaleDuration(peelCardDuration))
					.SetEase(Ease.InOutQuad)
					.SetDelay(transDelay)
					.OnComplete(() =>
					{
						animCompletedCount++;
						// Ensure TargetPosition is synced to peelPos before releasing special animation flag
						physScript.SetTargetPosition(peelPos);
						physScript.SetTargetRotation(GetFinalDeckRotationForCard(physScript));
						physScript.isPlayingSpecialAnimation = false;
					});
			}
			else
			{
				// This card stays in deck
				animTotalCount++;
				physScript.isPlayingSpecialAnimation = true;
				physScript.SetTargetPosition(finalPos);
				card.transform.DOMove(finalPos, CombatAnimationSpeed.ScaleDuration(deckShiftDuration))
					.SetEase(Ease.OutQuad)
					.OnComplete(() =>
					{
						animCompletedCount++;
						physScript.isPlayingSpecialAnimation = false;
					});
			}
		}

		yield return new WaitUntil(() => animCompletedCount >= animTotalCount);

		AnimationStateTracker.me?.CompleteAnimation();

		// Update peeled state
		_peeledCards = newPeeledCards;

		TestManager.Log("[CombatUXManager] TransitionFocusCoroutine END newIndex=" + newTargetIndex + " currentIndex=" + currentTargetIndex + " time=" + Time.time);
	}

	/// <summary>
	/// Restore deck focus: return all peeled cards and reset offset
	/// </summary>
	public IEnumerator RestoreDeckFocusCoroutine()
	{
		if (!enablePeelDeck)
			yield break;
		if (!_isDeckFocused)
			yield break;

		AnimationStateTracker.me?.RegisterAnimation();

		// Clear offset so all cards calculate their final normal positions
		_deckFocusOffset = Vector3.zero;

		int completedCount = 0;
		int totalCount = 0;

		// Animate all deck cards to their final normal positions in parallel
		for (int i = 0; i < physicalCardsInDeck.Count; i++)
		{
			var card = physicalCardsInDeck[i];
			if (card == null) continue;
			var physScript = card.GetComponent<CardPhysObjScript>();
			if (physScript == null) continue;

			Vector3 finalPos = GetFinalDeckPositionForCard(physScript, i);
			physScript.isPlayingSpecialAnimation = true;
			physScript.SetTargetPosition(finalPos);
			physScript.SetTargetRotation(GetFinalDeckRotationForCard(physScript));

			if (_peeledCards.Contains(card))
			{
				// Peeled card: return from peel position with stagger
				float delay = CombatAnimationSpeed.ScaleDuration(i * peelStaggerDelay);
				totalCount++;
				card.transform.DOMove(finalPos, CombatAnimationSpeed.ScaleDuration(peelCardDuration))
					.SetEase(Ease.InOutQuad)
					.SetDelay(delay)
					.OnComplete(() =>
					{
						completedCount++;
						physScript.isPlayingSpecialAnimation = false;
					});
			}
			else
			{
				// Normal card: shift back to normal position
				totalCount++;
				card.transform.DOMove(finalPos, CombatAnimationSpeed.ScaleDuration(deckShiftDuration))
					.SetEase(Ease.OutQuad)
					.OnComplete(() =>
					{
						completedCount++;
						physScript.isPlayingSpecialAnimation = false;
					});
			}
		}

		// Calculate when the last deck card starts its reset animation
		float lastDeckCardStartDelay = 0f;
		if (physicalCardsInDeck.Count > 0)
		{
			int lastIndex = physicalCardsInDeck.Count - 1;
			var lastCard = physicalCardsInDeck[lastIndex];
			if (lastCard != null)
			{
				var lastPhysScript = lastCard.GetComponent<CardPhysObjScript>();
				if (lastPhysScript != null && _peeledCards.Contains(lastCard))
				{
					lastDeckCardStartDelay = lastIndex * peelStaggerDelay;
				}
			}
		}
		float revealCardDelay = CombatAnimationSpeed.ScaleDuration(lastDeckCardStartDelay + peelStaggerDelay);

		// Restore reveal zone card back to reveal position after last deck card starts + stagger
		if (physicalCardInRevealZone != null)
		{
			Vector3 revealPos = GetRevealZonePosition();
			var revealPhysScript = physicalCardInRevealZone.GetComponent<CardPhysObjScript>();
			if (revealPhysScript != null)
			{
				revealPhysScript.isPlayingSpecialAnimation = true;
			}

			totalCount++;
			physicalCardInRevealZone.transform.DOMove(revealPos, CombatAnimationSpeed.ScaleDuration(peelCardDuration))
				.SetEase(Ease.InOutQuad)
				.SetDelay(revealCardDelay)
				.OnComplete(() =>
				{
					completedCount++;
					if (revealPhysScript != null)
					{
						revealPhysScript.SetTargetPosition(revealPos);
						revealPhysScript.SetTargetScale(physicalCardRevealSize);
						revealPhysScript.SetTargetRotation(Quaternion.identity);
						revealPhysScript.isPlayingSpecialAnimation = false;
					}
				});
		}

		if (totalCount > 0)
		{
			yield return new WaitUntil(() => completedCount >= totalCount);
		}

		AnimationStateTracker.me?.CompleteAnimation();

		// Clear focus state
		_isDeckFocused = false;
		_currentFocusCard = null;
		_peeledCards.Clear();

		// Update all physical card targets to ensure they are in sync
		UpdateAllPhysicalCardTargets();
	}

	/// <summary>
	/// Get index of physical card in physicalCardsInDeck, or -1
	/// </summary>
	public int GetPhysicalCardDeckIndex(GameObject physicalCard)
	{
		if (physicalCard == null)
			return -1;
		return physicalCardsInDeck.IndexOf(physicalCard);
	}

	#endregion

	#region Cleanup

	/// <summary>
	/// Destroy all physical cards and clear lists
	/// </summary>
	public void ClearAllPhysicalCards()
	{
		// Stop all special animations that may be playing
		StopAllSpecialAnimations();
		
		// Destroy physical cards in deck
		foreach (var physicalCard in physicalCardsInDeck)
		{
			if (physicalCard != null)
			{
				Destroy(physicalCard);
			}
		}
		physicalCardsInDeck.Clear();

		// Destroy physical cards in reveal zone
		if (physicalCardInRevealZone != null)
		{
			Destroy(physicalCardInRevealZone);
			physicalCardInRevealZone = null;
		}

		// Clear dictionary cache
		_cardScriptToPhysicalCache.Clear();

		// Clear deck layout offsets
		_deckOffsetProvider?.Clear();
	}

	/// <summary>
	/// Uniformly destroy card (with animation): move to gravePosition and shrink, then destroy physical and logical card
	/// </summary>
	/// <param name="logicalCard">Logical card GameObject</param>
	/// <param name="onComplete">Animation complete callback</param>
	public void DestroyCardWithAnimation(GameObject logicalCard, System.Action onComplete = null)
	{
		if (logicalCard == null)
		{
			onComplete?.Invoke();
			return;
		}

		// Get CardScript
		var cardScript = logicalCard.GetComponent<CardScript>();
		if (cardScript == null)
		{
			// No CardScript, destroy logical card directly
			Destroy(logicalCard);
			onComplete?.Invoke();
			return;
		}

		// Get corresponding physical card
		BuildCardScriptToPhysicalDictionary();
		var physicalCard = GetPhysicalCardFromLogicalCard(cardScript);

		// Fallback: card may have been removed from deck by SyncPhysicalCardsWithCombinedDeck before animation phase
		if (physicalCard == null)
		{
			var allPhysScripts = UnityEngine.Object.FindObjectsByType<CardPhysObjScript>(FindObjectsSortMode.None);
			foreach (var physScript in allPhysScripts)
			{
				if (physScript.cardImRepresenting == cardScript)
				{
					physicalCard = physScript.gameObject;
					break;
				}
			}
		}

		// Debug.Log("[CombatUXManager] DestroyCardWithAnimation logical=" + logicalCard.name + " physicalFound=" + (physicalCard != null ? physicalCard.name : "NULL") + " inDeck=" + physicalCardsInDeck.Contains(physicalCard) + " inReveal=" + (physicalCardInRevealZone == physicalCard));
		
		// Remove logical card from combined deck
		if (combatManager != null && combatManager.combinedDeckZone.Contains(logicalCard))
		{
			combatManager.combinedDeckZone.Remove(logicalCard);
		}

		// If no physical card, destroy logical card directly
		if (physicalCard == null)
		{
			// Debug.LogWarning("[CombatUXManager] DestroyCardWithAnimation: no physical card found for " + logicalCard.name + ", destroying immediately");
			Destroy(logicalCard);
			onComplete?.Invoke();
			return;
		}

		// Remove from deck list and cache (prevent use by other logic during animation)
		physicalCardsInDeck.Remove(physicalCard);
		_cardScriptToPhysicalCache.Remove(cardScript);

		// Clear reveal zone reference if this card is currently revealed
		if (physicalCardInRevealZone == physicalCard)
		{
			physicalCardInRevealZone = null;
		}

		AnimationStateTracker.me?.RegisterAnimation();

		// Create exit animation
		Sequence destroySequence = DOTween.Sequence();
		float scaledDestroyDuration = CombatAnimationSpeed.ScaleDuration(cardDestroyAnimDuration);

		// Move to grave position (if set)
		if (gravePosition != null)
		{
			destroySequence.Append(
				physicalCard.transform.DOMove(gravePosition.position, scaledDestroyDuration)
					.SetEase(Ease.InQuad)
			);
		}

		// Shrink
		destroySequence.Join(
			physicalCard.transform.DOScale(cardDestroyTargetSize, scaledDestroyDuration)
				.SetEase(Ease.InQuad)
		);

		// Destroy after animation completes
		destroySequence.OnComplete(() =>
		{
			AnimationStateTracker.me?.CompleteAnimation();
			Destroy(physicalCard);
			Destroy(logicalCard);
			onComplete?.Invoke();
		});
	}

	/// <summary>
	/// Stop all special animations on physical cards
	/// </summary>
	public void StopAllSpecialAnimations()
	{
		// Stop all DOTween delayed calls belonging to this object
		DOTween.Kill(this);
		
		// Restore player input
		combatManager?.ResetInputBlock();
		
		foreach (var physicalCard in physicalCardsInDeck)
		{
			if (physicalCard != null)
			{
				var physScript = physicalCard.GetComponent<CardPhysObjScript>();
				physScript?.StopSpecialAnimation();
			}
		}
	}

	#endregion
	

	/// <summary>
	/// Create corresponding physical card for logical card and insert into deck
	/// </summary>
	public void AddPhysicalCardToDeck(GameObject logicalCard)
	{
		CardScript cardScript = logicalCard.GetComponent<CardScript>();

		// Choose prefab based on card type
		GameObject prefabToUse = physicalCardPrefab;
		if (cardScript != null)
		{
			if (cardScript.isMinion)
				prefabToUse = minionPhysicalPrefab;
		}

		// Create physical card
		GameObject newPhysicalCard = Instantiate(prefabToUse);
		CardPhysObjScript physScript = newPhysicalCard.GetComponent<CardPhysObjScript>();
		newPhysicalCard.AddComponent<CombatCardView>();

		physScript.cardImRepresenting = cardScript;
		newPhysicalCard.name = logicalCard.name + "'s physical card";
		physScript.cardNamePrint.text = cardScript != null ? cardScript.GetDisplayName() : logicalCard.name;
		physScript.cardDescPrint.text = cardScript != null ? cardScript.GetCardDescForDisplay() : string.Empty;

		// Set initial scale
		physScript.SetScaleImmediate(physicalCardDeckSize);

		// Assign deck layout offset so SlotIn lands with the correct messy-deck pose
		_deckOffsetProvider.AssignOffset(physScript);

		// Insert into physical card list
		physicalCardsInDeck.Insert(0, newPhysicalCard);
		InvalidateCardScriptCache();

		// Set initial position (new card appears at physical card new temp card pos)
		Vector3 startPos = physicalCardNewTempCardPos.position;

		// z is smaller towards the camera (front), larger away from camera (back).
		// New cards must spawn BEHIND all existing cards, so we find the largest Z
		// (furthest from camera) among existing cards and place the new card even further back.
		float backMostZ = float.MinValue;
		foreach (var card in physicalCardsInDeck)
		{
			if (card == newPhysicalCard) continue;
			var phys = card.GetComponent<CardPhysObjScript>();
			if (phys != null)
				backMostZ = Mathf.Max(backMostZ, phys.TargetPosition.z);
		}
		if (backMostZ == float.MinValue)
			backMostZ = physicalCardDeckPos.position.z;

		float zBump = Mathf.Abs(zOffset) > 0.001f ? Mathf.Abs(zOffset) : 0.5f;
		startPos.z = backMostZ + zBump * 2f;

		physScript.SetPositionImmediate(startPos);
		// set initial size
		Vector3 startSize = physicalCardNewTempCardSize;
		physScript.SetScaleImmediate(startSize);

		string deckAfterInsert = "";
		for (int i = 0; i < physicalCardsInDeck.Count; i++)
		{
			deckAfterInsert += "[" + i + "]" + physicalCardsInDeck[i].name + " pos=" + physicalCardsInDeck[i].transform.position + " ";
		}
		// Debug.Log("[CombatUXManager] AddPhysicalCardToDeck logical=" + logicalCard.name + " deckCountAfterInsert=" + physicalCardsInDeck.Count + " insertedAtIndex=0 deck=" + deckAfterInsert);

		// VISUAL-FIX(2026-05-17): AddPhysicalCardToDeck pre-moves existing cards causing distance-zero tweens
		//   Cause:    UpdateAllPhysicalCardTargets in logic phase starts tweens for all existing cards.
		//             When bury/stage animation plays, cards are already at final positions (distance=0).
		//   Affects:  AddPhysicalCardToDeck, AddTempCard, UpdateAllPhysicalCardTargets, ApplyAnimationResult
		//   Regress:  Play a card that adds temp cards (e.g. RIFT_INSECT) then Bury; verify existing cards
		//             animate with visible movement instead of snapping instantly.
		//   Related:  RIFT_INSECT, BLACKSMITH
		for (int i = 0; i < physicalCardsInDeck.Count; i++)
		{
			var cardAtIndex = physicalCardsInDeck[i];
			var cardPhys = cardAtIndex.GetComponent<CardPhysObjScript>();
			if (cardPhys == null) continue;

			Vector3 targetPos = GetFinalDeckPositionForCard(cardPhys, i);
			Quaternion targetRot = GetFinalDeckRotationForCard(cardPhys);
			if (cardAtIndex == newPhysicalCard)
			{
				// If inside an active effect chain, do NOT auto-tween the new card.
				// The effect will capture PopUp + SlotIn AnimationRequests instead.
				bool insideEffectChain = EffectChainManager.Me != null && EffectChainManager.Me.currentEffectRecorder != null;
				if (!insideEffectChain)
				{
					// New card: start tween immediately so it enters the deck visually.
					// Place it behind all existing cards so later spawns never overlap earlier ones.
					targetPos.z = backMostZ + zBump;
					cardPhys.SetTargetPosition(targetPos);
					cardPhys.SetTargetScale(GetDeckScaleAtIndex(i));
					cardPhys.SetTargetRotation(targetRot);
					// Debug.Log("[CombatUXManager] AddPhysicalCardToDeck new card tween START card=" + cardAtIndex.name + " index=" + i + " targetPos=" + targetPos);
				}
				else
				{
					// Debug.Log("[CombatUXManager] AddPhysicalCardToDeck new card inside effect chain, skipping auto-tween card=" + cardAtIndex.name);
					// Prevent UpdateAllPhysicalCardTargets from prematurely tweening this card into the deck
					// before its MoveToPopUpPosition + SlotIn recorder animation plays.
					cardPhys.isPlayingSpecialAnimation = true;
					cardPhys.isPendingSlotIn = true;
				}
			}
			else
			{
				// Existing card: keep its messy-deck rotation in sync
				cardPhys.SetTargetPosition(targetPos);
				cardPhys.SetTargetRotation(targetRot);
			}
		}
	}

	#region Initialization

	/// <summary>
	/// Instantiate all physical cards (including Start Card)
	/// </summary>
	public void InstantiateAllPhysicalCards()
	{
		if (physicalCardsInDeck.Count > 0) return;

		foreach (var card in combatManager.combinedDeckZone)
		{
			CardScript cardScript = card.GetComponent<CardScript>();
			
			// Choose prefab based on card type
			GameObject prefabToUse = physicalCardPrefab;
			if (cardScript != null)
			{
				if (cardScript.isStartCard)
					prefabToUse = startCardPhysicalPrefab;
				else if (cardScript.isMinion)
					prefabToUse = minionPhysicalPrefab;
			}
			
			GameObject newPhysicalCard = Instantiate(prefabToUse);
			CardPhysObjScript physScript = newPhysicalCard.GetComponent<CardPhysObjScript>();
			newPhysicalCard.AddComponent<CombatCardView>();

			physScript.cardImRepresenting = cardScript;
			newPhysicalCard.name = card.name + "'s physical card";
			
			// Normal cards: set name and description
			if (cardScript != null && !cardScript.isStartCard)
			{
				physScript.cardNamePrint.text = cardScript != null ? cardScript.GetDisplayName() : card.name;
				physScript.cardDescPrint.text = cardScript.GetCardDescForDisplay();
			}

			// Set initial position and scale immediately
			physScript.SetScaleImmediate(physicalCardDeckSize);

			physicalCardsInDeck.Add(newPhysicalCard);
		}
		InvalidateCardScriptCache();

		// Set initial position and rotation, including per-card layout offset
		for (int i = 0; i < physicalCardsInDeck.Count; i++)
		{
			var physScript = physicalCardsInDeck[i].GetComponent<CardPhysObjScript>();
			if (physScript == null) continue;
			_deckOffsetProvider.AssignOffset(physScript);
			Vector3 pos = GetFinalDeckPositionForCard(physScript, i);
			Quaternion rot = GetFinalDeckRotationForCard(physScript);
			physScript.SetPositionImmediate(pos);
			physScript.SetRotationImmediate(rot);
			// Cascade: per-depth scale immediately so the deck never opens with a uniform-scale frame
			physScript.SetScaleImmediate(GetDeckScaleAtIndex(i));
		}

		// Rebuild dictionary mapping
		BuildCardScriptToPhysicalDictionary();
	}

	#endregion

	#region Status Effect Projectile System

	[Header("STATUS EFFECT PROJECTILE")]
	[Tooltip("Status effect projectile prefab (can be Sprite, particle system, or simple GameObject)")]
	public GameObject statusEffectProjectilePrefab;
	[Tooltip("Projectile flight duration")]
	public float projectileDuration = 0.4f;
	[Tooltip("Arc height")]
	public float projectileArcHeight = 2f;
	[Tooltip("Projectile start position offset")]
	public Vector3 projectileStartOffset = new Vector3(0, 0.5f, 0);
	[Tooltip("Projectile end position offset")]
	public Vector3 projectileEndOffset = new Vector3(0, 0.5f, 0);
	[Tooltip("Stagger delay between multiple projectiles (seconds)")]
	public float projectileStaggerDelay = 0.05f;
	[Tooltip("Default random XY offset range applied to each projectile start position")]
	public Vector2 projectileStartRandomOffsetRange = new Vector2(0.2f, 0.2f);
	[Tooltip("Default random delay range for staggering projectile launches (x=min, y=max)")]
	public Vector2 projectileStartTimeStaggerRange = new Vector2(0f, 0.1f);
	[Tooltip("Maximum total projectiles allowed per request (safety cap for high stacks)")]
	public int maxProjectilesPerRequest = 30;

	/// <summary>
	/// Play parabolic projectile effect of status effect flying from giver to receiver
	/// onComplete callback is executed after the effect reaches the target
	/// </summary>
	/// <param name="giverCard">Giver logical card</param>
	/// <param name="receiverCard">Receiver logical card</param>
	/// <param name="onComplete">Effect complete callback (executed after effect reaches target)</param>
	public void PlayStatusEffectProjectile(GameObject giverCard, GameObject receiverCard, Action onComplete = null)
	{
		TestManager.Log("[CombatUXManager] PlayStatusEffectProjectile for receiver=" + (receiverCard?.name ?? "null") + " — NO PopUp here, only parabolic projectile.");
		if (statusEffectProjectilePrefab == null || giverCard == null || receiverCard == null)
		{
			TestManager.Log("[CombatUXManager] PlayStatusEffectProjectile early return: prefab=" + (statusEffectProjectilePrefab != null) + " giver=" + (giverCard != null) + " receiver=" + (receiverCard != null));
			onComplete?.Invoke();
			return;
		}

		// Get physical card positions
		BuildCardScriptToPhysicalDictionary();

		Vector3 startPos = GetCardWorldPosition(giverCard) + projectileStartOffset;
		Vector3 endPos = GetCardWorldPosition(receiverCard) + projectileEndOffset;

		SpawnProjectile(startPos, endPos, onComplete);
	}

	/// <summary>
	/// Play multiple status effect projectile animations, supports staggered playback
	/// Execute corresponding callback after effect reaches each target, final callback after all complete
	/// </summary>
	/// <param name="giverCard">Giver logical card (or receiver when reverseDirection is true)</param>
	/// <param name="targetCards">Target card list (CardScript)</param>
	/// <param name="onEachComplete">Callback when each effect completes (parameter is target CardScript)</param>
	/// <param name="onAllComplete">Callback after all effects complete</param>
	/// <param name="customStaggerDelay">Custom stagger delay (null uses default value)</param>
	/// <param name="reverseDirection">If true, projectiles fly from each target back to the giver card (absorb).</param>
	/// <param name="customEndPosition">Optional override for the projectile end position. When reverseDirection is true,
	/// projectiles fly from each target to this position instead of to the giver card.</param>
	/// <param name="projectileCountsPerTarget">Optional per-target projectile counts. When null, projectileCount is used for every target.</param>
	public void PlayMultiStatusEffectProjectile(
		GameObject giverCard,
		List<CardScript> targetCards,
		System.Action<CardScript> onEachComplete,
		System.Action onAllComplete = null,
		float? customStaggerDelay = null,
		int projectileCount = 1,
		Vector2? projectileStartRandomOffsetRange = null,
		Vector2? projectileStartTimeStaggerRange = null,
		bool reverseDirection = false,
		Vector3? customEndPosition = null,
		List<int> projectileCountsPerTarget = null)
	{
		TestManager.Log("[CombatUXManager] PlayMultiStatusEffectProjectile START — REAL-TIME path, " + (targetCards?.Count ?? 0) + " targets. reverseDirection=" + reverseDirection + ". NO PopUp/SlotIn here!");
		if (targetCards == null || targetCards.Count == 0)
		{
			TestManager.Log("[CombatUXManager] PlayMultiStatusEffectProjectile early return: no targetCards");
			onAllComplete?.Invoke();
			return;
		}

		// Resolve per-target projectile counts. Fall back to projectileCount uniformly when not provided.
		var effectiveCounts = new List<int>();
		for (int i = 0; i < targetCards.Count; i++)
		{
			int count = (projectileCountsPerTarget != null && i < projectileCountsPerTarget.Count)
				? projectileCountsPerTarget[i]
				: projectileCount;
			effectiveCounts.Add(Mathf.Max(0, count));
		}

		int totalProjectiles = 0;
		foreach (var c in effectiveCounts) totalProjectiles += c;
		if (totalProjectiles <= 0)
		{
			TestManager.Log("[CombatUXManager] PlayMultiStatusEffectProjectile early return: totalProjectiles <= 0");
			onAllComplete?.Invoke();
			return;
		}

		// If prefab is not configured, execute effect directly (no animation)
		if (statusEffectProjectilePrefab == null || giverCard == null)
		{
			TestManager.Log("[CombatUXManager] PlayMultiStatusEffectProjectile early return: prefab=" + (statusEffectProjectilePrefab != null) + " giver=" + (giverCard != null));
			for (int t = 0; t < targetCards.Count; t++)
			{
				var target = targetCards[t];
				if (target == null) continue;
				for (int i = 0; i < effectiveCounts[t]; i++)
				{
					onEachComplete?.Invoke(target);
				}
			}
			onAllComplete?.Invoke();
			return;
		}

		Vector2 effectiveOffsetRange = projectileStartRandomOffsetRange ?? this.projectileStartRandomOffsetRange;
		Vector2 effectiveStaggerRange = projectileStartTimeStaggerRange ?? this.projectileStartTimeStaggerRange;
		// Fallback to legacy fixed stagger if no random range was provided explicitly.
		if (projectileStartTimeStaggerRange == null && customStaggerDelay.HasValue)
		{
			effectiveStaggerRange = new Vector2(0f, customStaggerDelay.Value);
		}
		float minStagger = CombatAnimationSpeed.ScaleDuration(Mathf.Min(effectiveStaggerRange.x, effectiveStaggerRange.y));
		float maxStagger = CombatAnimationSpeed.ScaleDuration(Mathf.Max(effectiveStaggerRange.x, effectiveStaggerRange.y));

		// Safety cap: high stack counts can spawn an excessive number of projectiles.
		if (totalProjectiles > maxProjectilesPerRequest)
		{
			float scale = (float)maxProjectilesPerRequest / totalProjectiles;
			for (int i = 0; i < effectiveCounts.Count; i++)
			{
				if (effectiveCounts[i] > 0)
				{
					effectiveCounts[i] = Mathf.Max(1, Mathf.RoundToInt(effectiveCounts[i] * scale));
				}
			}
			totalProjectiles = 0;
			foreach (var c in effectiveCounts) totalProjectiles += c;
		}

		// VISUAL-FIX(2026-06-14): Disable projectile start randomness for single-layer status effects
		//   Cause:    When an effect only gives/absorbs/consumes 1 status effect layer, the single
		//             projectile should fly straight from the source card; random offset makes it
		//             look off-center or miss the target.
		//   Affects:  CombatUXManager, all status-effect giver/consumers, RecorderAnimationPlayer
		//   Regress:  Reveal a card that gives/consumes exactly 1 status effect layer
		//             Check: projectile starts at the center of the source card and lands cleanly.
		if (totalProjectiles == 1)
		{
			effectiveOffsetRange = Vector2.zero;
		}

		TestManager.Log("[CombatUXManager] PlayMultiStatusEffectProjectile spawning " + totalProjectiles + " projectiles for giver=" + giverCard.name);
		BlockInput(this);
		AnimationStateTracker.me?.RegisterAnimation();

		int completedCount = 0;

		for (int t = 0; t < targetCards.Count; t++)
		{
			var targetCardScript = targetCards[t];
			if (targetCardScript == null) continue;

			int countForTarget = effectiveCounts[t];
			for (int p = 0; p < countForTarget; p++)
			{
				var capturedTarget = targetCardScript;
				float delay = UnityEngine.Random.Range(minStagger, maxStagger);

				DOVirtual.DelayedCall(delay, () =>
				{
					// Defensive: giver or target may have been destroyed (e.g. exiled) during the delay.
					if (giverCard == null || capturedTarget == null || capturedTarget.gameObject == null)
					{
						completedCount++;
						TryCompleteAll();
						return;
					}

					BuildCardScriptToPhysicalDictionary();
					Vector3 startPos;
					Vector3 endPos;
					if (reverseDirection)
					{
						startPos = GetCardWorldPosition(capturedTarget.gameObject) + projectileStartOffset + GetRandomProjectileStartOffset(effectiveOffsetRange);
						endPos = customEndPosition.HasValue
							? customEndPosition.Value + projectileEndOffset
							: GetCardWorldPosition(giverCard) + projectileEndOffset;
					}
					else
					{
						startPos = GetCardWorldPosition(giverCard) + projectileStartOffset + GetRandomProjectileStartOffset(effectiveOffsetRange);
						endPos = GetCardWorldPosition(capturedTarget.gameObject) + projectileEndOffset;
					}

					SpawnProjectile(startPos, endPos, () =>
					{
						// Single effect complete, execute effect for this target
						onEachComplete?.Invoke(capturedTarget);

						completedCount++;
						TryCompleteAll();
					});
				});
			}
		}

		void TryCompleteAll()
		{
			if (completedCount >= totalProjectiles)
			{
				UnblockInput(this);
				AnimationStateTracker.me?.CompleteAnimation();
				onAllComplete?.Invoke();
			}
		}
	}

	/// <summary>
	/// Play status effect projectile(s) from giver card to a custom world position.
	/// Used when the projectile target is not a card (e.g. fly to newCardPos).
	/// </summary>
	public void PlayStatusEffectProjectileToPosition(
		GameObject giverCard,
		Vector3 endPosition,
		Action onComplete = null,
		int projectileCount = 1,
		Vector2? projectileStartRandomOffsetRange = null,
		Vector2? projectileStartTimeStaggerRange = null)
	{
		TestManager.Log("[CombatUXManager] PlayStatusEffectProjectileToPosition to " + endPosition + " count=" + projectileCount + " — NO PopUp here, only parabolic projectile.");
		if (projectileCount <= 0)
		{
			TestManager.Log("[CombatUXManager] PlayStatusEffectProjectileToPosition early return: projectileCount <= 0");
			onComplete?.Invoke();
			return;
		}
		if (statusEffectProjectilePrefab == null || giverCard == null)
		{
			TestManager.Log("[CombatUXManager] PlayStatusEffectProjectileToPosition early return: prefab=" + (statusEffectProjectilePrefab != null) + " giver=" + (giverCard != null));
			onComplete?.Invoke();
			return;
		}

		Vector2 effectiveOffsetRange = projectileStartRandomOffsetRange ?? this.projectileStartRandomOffsetRange;
		Vector2 effectiveStaggerRange = projectileStartTimeStaggerRange ?? this.projectileStartTimeStaggerRange;
		float minStagger = CombatAnimationSpeed.ScaleDuration(Mathf.Min(effectiveStaggerRange.x, effectiveStaggerRange.y));
		float maxStagger = CombatAnimationSpeed.ScaleDuration(Mathf.Max(effectiveStaggerRange.x, effectiveStaggerRange.y));

		int cappedProjectileCount = Mathf.Min(projectileCount, maxProjectilesPerRequest);
		if (cappedProjectileCount <= 0) cappedProjectileCount = 1;

		// Single-layer status effect: do not randomize projectile start position.
		if (cappedProjectileCount == 1)
		{
			effectiveOffsetRange = Vector2.zero;
		}

		// Single projectile: keep the old simple path (one AnimationStateTracker registration).
		if (cappedProjectileCount == 1)
		{
			TestManager.Log("[CombatUXManager] PlayStatusEffectProjectileToPosition spawning 1 projectile");
			BuildCardScriptToPhysicalDictionary();
			Vector3 startPos = GetCardWorldPosition(giverCard) + projectileStartOffset + GetRandomProjectileStartOffset(effectiveOffsetRange);
			Vector3 endPos = endPosition + projectileEndOffset;
			SpawnProjectile(startPos, endPos, onComplete);
			return;
		}

		// Multiple projectiles: block input and wait for all to finish.
		TestManager.Log("[CombatUXManager] PlayStatusEffectProjectileToPosition spawning " + cappedProjectileCount + " projectiles");
		BlockInput(this);
		AnimationStateTracker.me?.RegisterAnimation();

		int completedCount = 0;
		int totalCount = cappedProjectileCount;

		for (int i = 0; i < cappedProjectileCount; i++)
		{
			float delay = UnityEngine.Random.Range(minStagger, maxStagger);
			DOVirtual.DelayedCall(delay, () =>
			{
				// Defensive: giver may have been destroyed during the delay.
				if (giverCard == null)
				{
					completedCount++;
					TryCompleteAll();
					return;
				}

				BuildCardScriptToPhysicalDictionary();
				Vector3 startPos = GetCardWorldPosition(giverCard) + projectileStartOffset + GetRandomProjectileStartOffset(effectiveOffsetRange);
				Vector3 endPos = endPosition + projectileEndOffset;

				SpawnProjectile(startPos, endPos, () =>
				{
					completedCount++;
					TryCompleteAll();
				});
			});
		}

		void TryCompleteAll()
		{
			if (completedCount >= totalCount)
			{
				UnblockInput(this);
				AnimationStateTracker.me?.CompleteAnimation();
				onComplete?.Invoke();
			}
		}
	}

	/// <summary>
	/// Spawn a single parabolic projectile from startPos to endPos and invoke onComplete when done.
	/// Handles one AnimationStateTracker registration/complete pair.
	/// </summary>
	private void SpawnProjectile(Vector3 startPos, Vector3 endPos, Action onComplete)
	{
		TestManager.Log("[CombatUXManager] SpawnProjectile START startPos=" + startPos + " endPos=" + endPos + " distance=" + Vector3.Distance(startPos, endPos));
		GameObject projectile = Instantiate(statusEffectProjectilePrefab, startPos, Quaternion.identity);

		// Calculate parabolic midpoint
		Vector3 midPoint = Vector3.Lerp(startPos, endPos, 0.5f) + Vector3.up * projectileArcHeight;

		AnimationStateTracker.me?.RegisterAnimation();

		// Create parabolic animation
		Sequence projectileSequence = DOTween.Sequence();
		float scaledProjectileDuration = CombatAnimationSpeed.ScaleDuration(projectileDuration);

		// Phase 1: From start to midpoint (ascending)
		projectileSequence.Append(
			projectile.transform.DOMove(midPoint, scaledProjectileDuration * 0.5f)
				.SetEase(Ease.OutQuad)
		);

		// Phase 2: From midpoint to end (descending)
		projectileSequence.Append(
			projectile.transform.DOMove(endPos, scaledProjectileDuration * 0.5f)
				.SetEase(Ease.InQuad)
		);

		// Sync rotation: keep effect facing target
		projectile.transform.LookAt(endPos);

		// Safety kill: if the projectile GameObject is destroyed externally before the tween finishes,
		// DOTween would throw a missing-Transform warning. Kill the sequence gracefully instead.
		projectileSequence.OnUpdate(() =>
		{
			if (projectile == null)
			{
				projectileSequence.Kill(true);
			}
		});

		// Animation complete: destroy effect and execute callback
		projectileSequence.OnComplete(() =>
		{
			TestManager.Log("[CombatUXManager] SpawnProjectile COMPLETE startPos=" + startPos + " endPos=" + endPos);
			AnimationStateTracker.me?.CompleteAnimation();
			Destroy(projectile);
			onComplete?.Invoke();
		});

		projectileSequence.Play();
	}

	/// <summary>
	/// Returns a random XY offset in the given range. Z is always 0.
	/// </summary>
	private Vector3 GetRandomProjectileStartOffset(Vector2 range)
	{
		if (range == Vector2.zero) return Vector3.zero;
		return new Vector3(UnityEngine.Random.Range(-range.x, range.x), UnityEngine.Random.Range(-range.y, range.y), 0f);
	}

	/// <summary>
	/// Get actual world position of card (prefer physical card)
	/// </summary>
	private Vector3 GetCardWorldPosition(GameObject card)
	{
		var cardScript = card.GetComponent<CardScript>();
		if (cardScript != null)
		{
			var physicalCard = GetPhysicalCardFromLogicalCard(cardScript);
			if (physicalCard != null)
			{
				return physicalCard.transform.position;
			}
		}
		return card.transform.position;
	}

	#endregion

	#region ICombatVisuals Implementation

	/// <summary>
	/// ICombatVisuals: Get physical card from logical card GameObject.
	/// </summary>
	public GameObject GetPhysicalCard(GameObject logicalCard)
	{
		if (logicalCard == null) return null;
		var cardScript = logicalCard.GetComponent<CardScript>();
		if (cardScript == null) return null;
		BuildCardScriptToPhysicalDictionary();
		return GetPhysicalCardFromLogicalCard(cardScript);
	}

	/// <summary>
	/// ICombatVisuals: Move logical card to reveal zone.
	/// </summary>
	public void MoveCardToRevealZone(GameObject logicalCard, Action onComplete = null)
	{
		if (logicalCard == null)
		{
			onComplete?.Invoke();
			return;
		}
		var cardScript = logicalCard.GetComponent<CardScript>();
		if (cardScript == null)
		{
			onComplete?.Invoke();
			return;
		}
		BuildCardScriptToPhysicalDictionary();
		var physicalCard = GetPhysicalCardFromLogicalCard(cardScript);
		if (physicalCard != null)
		{
			MovePhysicalCardToRevealZone(physicalCard, onComplete);
		}
		else
		{
			onComplete?.Invoke();
		}
	}

	/// <summary>
	/// ICombatVisuals: Request attack animation (delegates to AttackAnimationManager).
	/// </summary>
	public void PlayAttackAnimation(GameObject attackerCard, bool isAttackingEnemy, Action onHit = null, Action onComplete = null)
	{
		if (AttackAnimationManager.me != null)
		{
			AttackAnimationManager.me.RequestAttackAnimation(attackerCard, isAttackingEnemy, onHit, onComplete);
		}
		else
		{
			// No animation manager available, execute callbacks immediately
			onHit?.Invoke();
			onComplete?.Invoke();
		}
	}

	/// <summary>
	/// ICombatVisuals: Stop all playing and pending animations immediately.
	/// </summary>
	public void StopAllAnimations()
	{
		AttackAnimationManager.me?.StopAllAttackAnimations();
	}

	/// <summary>
	/// ICombatVisuals: Check if an attack animation is currently playing.
	/// </summary>
	public bool IsPlayingAttackAnimation()
	{
		return AttackAnimationManager.me != null && AttackAnimationManager.me.isPlayingAttackAnimation;
	}

	/// <summary>
	/// ICombatVisuals: Check if there are pending animations in the queue.
	/// </summary>
	public bool HasPendingAnimations()
	{
		return AttackAnimationManager.me != null && AttackAnimationManager.me.HasPendingAnimations();
	}

	/// <summary>
	/// ICombatVisuals: Apply status effect tint to the target card.
	/// </summary>
	public void ApplyStatusTint(CardScript targetCard, EnumStorage.StatusEffect effect)
	{
		if (effect != EnumStorage.StatusEffect.Infected && effect != EnumStorage.StatusEffect.Power) return;
		BuildCardScriptToPhysicalDictionary();
		var physicalCard = GetPhysicalCardFromLogicalCard(targetCard);
		if (physicalCard != null)
		{
			var cardPhysObj = physicalCard.GetComponent<CardPhysObjScript>();
			if (cardPhysObj != null) cardPhysObj.TriggerTintForStatusEffect(effect);
		}
	}

	/// <summary>
	/// ICombatVisuals: Play particle effect for a status effect applied to a card.
	/// </summary>
	public void PlayStatusEffectParticle(CardScript targetCard, ParticleSystem particlePrefab, float particleYOffset, int amount)
	{
		if (particlePrefab == null || targetCard == null) return;
		BuildCardScriptToPhysicalDictionary();
		var physicalCard = GetPhysicalCardFromLogicalCard(targetCard);
		Vector3 basePosition = physicalCard != null ? physicalCard.transform.position : targetCard.transform.position;
		for (int i = 0; i < amount; i++)
		{
			Vector3 spawnPosition = basePosition + Vector3.up * particleYOffset;
			ParticleSystem particleInstance = Instantiate(particlePrefab, spawnPosition, Quaternion.identity, targetCard.transform);
			particleInstance.Play();
		}
	}

	/// <summary>
	/// ICombatVisuals: Add physical card for a logical card inserted mid-combat.
	/// </summary>
	public void AddCardToDeckVisual(GameObject logicalCard)
	{
		AddPhysicalCardToDeck(logicalCard);
	}

	/// <summary>
	/// ICombatVisuals: Play Start Card shuffle animation.
	/// </summary>
	public void PlayShuffleAnimation(GameObject startCard, List<GameObject> shuffledCards, Action onComplete)
	{
		PlayStartCardShuffleAnimation(startCard, shuffledCards, onComplete);
	}

	/// <summary>
	/// ICombatVisuals: Pop Up a card from its current position so the player can see it clearly.
	/// Sets isPlayingSpecialAnimation=true and isPoppedUp=true. Card remains at peak until SlotIn is called.
	/// </summary>
	public void PopUpCard(GameObject logicalCard, Action onComplete = null)
	{
		if (logicalCard == null) { onComplete?.Invoke(); return; }

		var cardScript = logicalCard.GetComponent<CardScript>();
		if (cardScript == null) { onComplete?.Invoke(); return; }

		BuildCardScriptToPhysicalDictionary();
		var physicalCard = GetPhysicalCardFromLogicalCard(cardScript);
		if (physicalCard == null) { onComplete?.Invoke(); return; }

		var physScript = physicalCard.GetComponent<CardPhysObjScript>();
		if (physScript == null) { onComplete?.Invoke(); return; }

		// Kill existing tweens to prevent conflicts
		physScript.KillTweens();

		// Save original transform so SlotIn can restore it for reveal-zone cards
		physScript.popUpOriginalPosition = physicalCard.transform.position;
		physScript.popUpOriginalScale = physicalCard.transform.localScale;

		// Compute peak position from CURRENT world position
		Vector3 currentPos = physicalCard.transform.position;
		Vector3 peakPos = currentPos + Vector3.up * popUpYOffset;
		peakPos.z += popUpZBoost;

		Vector3 peakScale = physicalCardDeckSize * popUpScaleMultiplier;

		physScript.isPlayingSpecialAnimation = true;
		physScript.isPoppedUp = true;
		physScript.SetTargetPosition(peakPos);
		physScript.SetTargetScale(peakScale);
		AnimationStateTracker.me?.RegisterAnimation();
		BlockInput(this);

		float scaledPopUpDuration = CombatAnimationSpeed.ScaleDuration(popUpDuration);
		float scaledPopUpHoldDuration = CombatAnimationSpeed.ScaleDuration(popUpHoldDuration);
		Sequence seq = DOTween.Sequence();
		seq.Append(physicalCard.transform.DOMove(peakPos, scaledPopUpDuration).SetEase(popUpEase));
		seq.Join(physicalCard.transform.DOScale(peakScale, scaledPopUpDuration).SetEase(popUpEase));
		seq.AppendInterval(scaledPopUpHoldDuration);
		seq.OnComplete(() =>
		{
			AnimationStateTracker.me?.CompleteAnimation();
			UnblockInput(this);
			onComplete?.Invoke();
		});
		seq.Play();
	}

	/// <summary>
	/// ICombatVisuals: Slot In a card from its pop-up position back to its correct deck position.
	/// Clears isPlayingSpecialAnimation/isPoppedUp and syncs target position/scale.
	/// </summary>
	public void SlotInCard(GameObject logicalCard, Action onComplete = null)
	{
		if (logicalCard == null) { onComplete?.Invoke(); return; }

		var cardScript = logicalCard.GetComponent<CardScript>();
		if (cardScript == null) { onComplete?.Invoke(); return; }

		BuildCardScriptToPhysicalDictionary();
		var physicalCard = GetPhysicalCardFromLogicalCard(cardScript);
		if (physicalCard == null) { onComplete?.Invoke(); return; }

		var physScript = physicalCard.GetComponent<CardPhysObjScript>();
		if (physScript == null) { onComplete?.Invoke(); return; }

		// Guard against deck position updates interfering with the slot-in tween
		physScript.isPlayingSpecialAnimation = true;

		// Find current deck index
		int deckIndex = physicalCardsInDeck.IndexOf(physicalCard);
		if (deckIndex < 0)
		{
			// Card not in deck (e.g. reveal zone) — tween back to pop-up original position
			Vector3 originalPos = physScript.popUpOriginalPosition;
			Vector3 originalScale = physScript.popUpOriginalScale;

			// Fallback: if never saved (legacy path), just release flag
			if (originalPos == Vector3.zero && originalScale == Vector3.zero)
			{
				physScript.isPlayingSpecialAnimation = false;
				physScript.isPendingSlotIn = false;
				physScript.isPoppedUp = false;
				onComplete?.Invoke();
				return;
			}

			AnimationStateTracker.me?.RegisterAnimation();
			BlockInput(this);

			float scaledFallbackSlotInDuration = CombatAnimationSpeed.ScaleDuration(slotInDuration);
			Sequence fallbackSeq = DOTween.Sequence();
			fallbackSeq.Append(ApplySlotInEase(physicalCard.transform.DOMove(originalPos, scaledFallbackSlotInDuration)));
			fallbackSeq.Join(ApplySlotInEase(physicalCard.transform.DOScale(originalScale, scaledFallbackSlotInDuration)));
			fallbackSeq.OnComplete(() =>
			{
				physScript.isPlayingSpecialAnimation = false;
				physScript.isPendingSlotIn = false;
				physScript.isPoppedUp = false;
				physScript.SetTargetPosition(originalPos);
				physScript.SetTargetScale(originalScale);
				AnimationStateTracker.me?.CompleteAnimation();
				UnblockInput(this);
				onComplete?.Invoke();
			});
			fallbackSeq.Play();
			return;
		}

		// VISUAL-FIX(2026-05-24): Pending card slot-in uses wrong position (activeCount instead of fullCount)
		//   Cause:    Same root cause as CalculatePositionForPendingCard: activeCount excludes pending cards.
		//   Affects:  SlotInCard, MoveToPopUpPosition
		//   Regress:  Same as CalculatePositionForPendingCard
		//   Related:  RIFT_INSECT, BLACKSMITH
		// Consolidated 2026-07-17: GetFinalDeckPositionForCard keeps the same FULL-count semantics
		// and adds cascade scale-aware jitter; slot-in lands at the cascade depth scale.
		Vector3 targetPos = GetFinalDeckPositionForCard(physScript, deckIndex);
		Quaternion targetRot = GetFinalDeckRotationForCard(physScript);
		Vector3 targetScale = GetDeckScaleAtIndex(deckIndex);
		TestManager.Log("[CombatUXManager] SlotInCard logical=" + logicalCard.name + " deckIndex=" + deckIndex + " targetPos=" + targetPos + " isPending=" + physScript.isPendingSlotIn);

		AnimationStateTracker.me?.RegisterAnimation();
		BlockInput(this);

		float scaledSlotInDuration = CombatAnimationSpeed.ScaleDuration(slotInDuration);
		Sequence seq = DOTween.Sequence();
		seq.Append(ApplySlotInEase(physicalCard.transform.DOMove(targetPos, scaledSlotInDuration)));
		seq.Join(ApplySlotInEase(physicalCard.transform.DOScale(targetScale, scaledSlotInDuration)));
		seq.Join(ApplySlotInEase(physicalCard.transform.DOLocalRotate(targetRot.eulerAngles, scaledSlotInDuration)));
		seq.OnComplete(() =>
		{
			physScript.isPlayingSpecialAnimation = false;
			physScript.isPendingSlotIn = false;
			physScript.isPoppedUp = false;
			physScript.SetTargetPosition(targetPos);
			physScript.SetTargetScale(targetScale);
			physScript.SetTargetRotation(targetRot);
			AnimationStateTracker.me?.CompleteAnimation();
			UnblockInput(this);
			onComplete?.Invoke();
		});
		seq.Play();
	}

	/// <summary>
	/// Applies either the custom AnimationCurve (if assigned) or the configured Ease to a tween.
	/// </summary>
	private Tween ApplySlotInEase(Tween tween)
	{
		if (slotInCurve != null && slotInCurve.length > 0)
		{
			return tween.SetEase(slotInCurve);
		}
		return tween.SetEase(slotInEase);
	}

	/// <summary>
	/// ICombatVisuals: Move card from its current spawn position to the pop-up peak position
	/// calculated from the specified deck index. Used for new cards entering the deck.
	/// </summary>
	public void MoveCardToPopUpPosition(GameObject logicalCard, int deckIndex, Action onComplete = null)
	{
		if (logicalCard == null) { onComplete?.Invoke(); return; }

		var cardScript = logicalCard.GetComponent<CardScript>();
		if (cardScript == null) { onComplete?.Invoke(); return; }

		BuildCardScriptToPhysicalDictionary();
		var physicalCard = GetPhysicalCardFromLogicalCard(cardScript);
		if (physicalCard == null) { onComplete?.Invoke(); return; }

		var physScript = physicalCard.GetComponent<CardPhysObjScript>();
		if (physScript == null) { onComplete?.Invoke(); return; }
		// Calculate peak position based on deck index (same formula as PopUpCard).
		// VISUAL-FIX(2026-05-24): Pending card pop-up peak is offset by missing deck size
		//   Cause:    Same root cause as CalculatePositionForPendingCard: activeCount excludes pending cards.
		//   Affects:  MoveCardToPopUpPosition, PopUp
		//   Regress:  Same as CalculatePositionForPendingCard
		//   Related:  RIFT_INSECT, BLACKSMITH

		Vector3 deckPos = CalculatePositionForPendingCard(deckIndex);
		Vector3 peakPos = deckPos + Vector3.up * popUpYOffset;
		peakPos.z += popUpZBoost;
		Vector3 peakScale = physicalCardDeckSize * popUpScaleMultiplier;

		TestManager.Log("[CombatUXManager] MoveCardToPopUpPosition logical=" + logicalCard.name + " deckIndex=" + deckIndex + " deckPos=" + deckPos + " peakPos=" + peakPos + " isPending=" + physScript.isPendingSlotIn);

		physScript.isPlayingSpecialAnimation = true;
		physScript.isPoppedUp = true;
		physScript.SetTargetPosition(peakPos);
		physScript.SetTargetScale(peakScale);
		AnimationStateTracker.me?.RegisterAnimation();
		BlockInput(this);

		float scaledFlyInDuration = CombatAnimationSpeed.ScaleDuration(newCardFlyInDuration);
		float scaledPopUpHoldDuration = CombatAnimationSpeed.ScaleDuration(popUpHoldDuration);

		// Straight line move to peak position, scaling to peak scale
		Sequence seq = DOTween.Sequence();
		seq.Append(physicalCard.transform.DOMove(peakPos, scaledFlyInDuration).SetEase(popUpEase));
		seq.Join(physicalCard.transform.DOScale(peakScale, scaledFlyInDuration).SetEase(popUpEase));
		seq.AppendInterval(scaledPopUpHoldDuration);
		seq.OnComplete(() =>
		{
			physScript.isPlayingSpecialAnimation = false;
			AnimationStateTracker.me?.CompleteAnimation();
			UnblockInput(this);
			onComplete?.Invoke();
		});
		seq.Play();
	}

	/// <summary>
	/// ICombatVisuals: Request to block player input.
	/// </summary>
	public void BlockInput(object requester)
	{
		combatManager?.BlockInput(requester);
	}

	/// <summary>
	/// ICombatVisuals: Request to unblock player input.
	/// </summary>
	public void UnblockInput(object requester)
	{
		combatManager?.UnblockInput(requester);
	}

	#endregion
}
