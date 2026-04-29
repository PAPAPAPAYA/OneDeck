using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Card Move Type
/// </summary>
public enum CardMoveType
{
	ToTop,          // Move to top of deck (last card)
	ToBottom,       // Move to bottom of deck (first card)
	ToIndex,        // Move to specified index
	ToPosition,     // Move to specified world position
	ToGrave,        // Move to graveyard (destroy position)
}

/// <summary>
/// Card Move Config
/// </summary>
[Serializable]
public class CardMoveConfig
{
	public CardMoveType moveType = CardMoveType.ToBottom;
	public int targetIndex;                    // Used when ToIndex
	public Vector3? customTarget;              // Used when ToPosition
	public bool useArc = true;                 // Whether to use arc trajectory
	public Transform arcMidpoint;              // Arc midpoint (use showPos if null)
	public float duration = 0.5f;              // Animation duration
	public Ease ease = Ease.InOutQuad;         // Ease type
	public bool destroyAfterMove = false;      // Whether to destroy after move
	public Action onComplete;                  // Animation complete callback
	public Action onStart;                     // Animation start callback
	
	// Convenient constructors
	public static CardMoveConfig ToTop(float duration = 0.5f, bool useArc = true, Action onComplete = null)
	{
		return new CardMoveConfig 
		{ 
			moveType = CardMoveType.ToTop, 
			duration = duration, 
			useArc = useArc,
			onComplete = onComplete 
		};
	}
	
	public static CardMoveConfig ToBottom(float duration = 0.5f, bool useArc = true, Action onComplete = null)
	{
		return new CardMoveConfig 
		{ 
			moveType = CardMoveType.ToBottom, 
			duration = duration, 
			useArc = useArc,
			onComplete = onComplete 
		};
	}
	
	public static CardMoveConfig ToIndex(int index, float duration = 0.5f, bool useArc = true, Action onComplete = null)
	{
		return new CardMoveConfig 
		{ 
			moveType = CardMoveType.ToIndex, 
			targetIndex = index,
			duration = duration, 
			useArc = useArc,
			onComplete = onComplete 
		};
	}
	
	public static CardMoveConfig ToPosition(Vector3 position, float duration = 0.5f, bool useArc = true, Action onComplete = null)
	{
		return new CardMoveConfig 
		{ 
			moveType = CardMoveType.ToPosition, 
			customTarget = position,
			duration = duration, 
			useArc = useArc,
			onComplete = onComplete 
		};
	}
	
	public static CardMoveConfig ToGrave(float duration = 0.3f, Action onComplete = null)
	{
		return new CardMoveConfig 
		{ 
			moveType = CardMoveType.ToGrave, 
			duration = duration, 
			useArc = false,
			destroyAfterMove = true,
			onComplete = onComplete 
		};
	}
}

public class CombatUXManager : MonoBehaviour
{
	#region SINGLETON
	public static CombatUXManager me;
	void Awake()
	{
		me = this;
	}
	#endregion

	[Header("REFERENCES")]
	[SerializeField] private CombatManager combatManager;
	public float zOffset;

	[Header("ANIMATION SETTINGS")]
	[Tooltip("Whether to enable Stage/Bury card animation")]
	public bool enableStageBuryAnimation = true;
	[Tooltip("Whether shuffle animation uses random staggered timing")]
	public bool useStaggeredShuffleAnimation = true;
	[Tooltip("Maximum random delay for shuffle animation (seconds)")]
	public float shuffleStaggerMaxDelay = 0.3f;
	[Tooltip("Deck card X-axis offset (rightward offset per card)")]
	public float xOffset;
	[Tooltip("Deck card Y-axis offset (upward offset per card)")]
	public float yOffset;
	[Header("NEW CARD")]
	public Transform physicalCardNewTempCardPos;
	public Vector3 physicalCardNewTempCardSize;

	[Header("DECK")]
	public GameObject physicalCardPrefab;
	public GameObject startCardPhysicalPrefab; // Start Card physical prefab (different appearance)
	public GameObject minionPhysicalPrefab; // Minion card physical prefab (different appearance)
	public Transform physicalCardDeckPos;
	public Vector3 physicalCardDeckSize;

	[Header("REVEAL")]
	public Transform physicalCardRevealPos;
	public Vector3 physicalCardRevealSize;
	
	[Header("REVEAL TO DECK ANIMATION")]
	[Tooltip("Midpoint when card goes from reveal zone to deck bottom (arc trajectory)")]
	public Transform showPos;
	[Tooltip("Arc trajectory animation duration")]
	public float revealToDeckAnimDuration = 0.5f;
	[Tooltip("Arc trajectory ease type")]
	public Ease revealToDeckEase = Ease.InOutQuad;
	
	[Header("DESTROY")]
	[Tooltip("Target position for card destroy animation (graveyard position)")]
	public Transform gravePosition;
	[Tooltip("Card destroy animation duration")]
	public float cardDestroyAnimDuration = 0.3f;
	[Tooltip("Target size when card is destroyed")]
	public Vector3 cardDestroyTargetSize = new Vector3(0.1f, 0.1f, 0.1f);

	[Header("DECK FOCUS / PEEL")]
	public Transform deckFocusTargetPos;
	public float peelSlideDistance = 4f;
	public float deckShiftDuration = 0.3f;
	public float peelCardDuration = 0.18f;
	public float peelStaggerDelay = 0.04f;
	[Tooltip("Distance to move reveal zone card downward out of screen when peeling starts")]
	public float revealCardExitDistance = 6f;
	public float secondaryLiftHeight = 0.4f;
	public float secondaryLiftDuration = 0.25f;
	[Tooltip("Enable LiftCardInDeck secondary animation")]
	public bool enableLiftCardInDeck = false;
	[Tooltip("Enable PeelDeck focus animation during attack")]
	public bool enablePeelDeck = true;

	// Physical card list (synced with combined deck zone updates)
	public List<GameObject> physicalCardsInDeck = new();
	
	// Physical card in reveal zone (stored separately to avoid confusion with deck)
	public GameObject physicalCardInRevealZone;

	// Dictionary mapping CardScript to Physical Card (maintain this mapping)
	private Dictionary<CardScript, GameObject> _cardScriptToPhysicalCache = new();

	// Deck focus runtime state
	private bool _isDeckFocused = false;
	private CardScript _currentFocusCard = null;
	private Vector3 _deckFocusOffset = Vector3.zero;
	private List<GameObject> _peeledCards = new List<GameObject>();
	private Dictionary<GameObject, Vector3> _peeledCardOriginalPositions = new Dictionary<GameObject, Vector3>();
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
		if (physicalCardsInDeck.Count == 0 && physicalCardInRevealZone == null) return;

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
	}

	/// <summary>
	/// Move card from deck to reveal zone
	/// </summary>
	public void MovePhysicalCardToRevealZone(GameObject physicalCard)
	{
		UnityEngine.Debug.Log("[PEEL_REVEAL] MovePhysicalCardToRevealZone called for " + physicalCard.name);
		// Remove from deck
		physicalCardsInDeck.Remove(physicalCard);

		// Store to reveal zone
		physicalCardInRevealZone = physicalCard;

		// Set reveal position
		var physScript = physicalCard.GetComponent<CardPhysObjScript>();
		if (physScript != null)
		{
			physScript.SetTargetPosition(physicalCardRevealPos.position);
			physScript.SetTargetScale(physicalCardRevealSize);
		}

		// Update positions of remaining cards in deck
		UpdateAllPhysicalCardTargets();
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
			Debug.LogWarning($"MoveRevealedCardToBottom: Card {card.name} has no CardScript");
			onComplete?.Invoke();
			return;
		}

		// Find physical card from logical card
		BuildCardScriptToPhysicalDictionary();
		physicalCard = GetPhysicalCardFromLogicalCard(cardScript);
		if (physicalCard == null)
		{
			Debug.LogWarning($"MoveRevealedCardToBottom: Could not find physical card for {card.name}");
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

		// If showPos is configured, use universal animation system
		if (showPos != null)
		{
			// [Key Fix] When calculating target position, consider that one card will be revealed
			// At this time physicalCardsInDeck contains the card about to be revealed, but it will be removed when animation completes
			// So effectiveCount = physicalCardsInDeck.Count - 1 is needed to calculate correct position
			int effectiveCount = physicalCardsInDeck.Count - 1;
			if (effectiveCount < 1) effectiveCount = 1; // At least 1 to avoid calculation errors
			
			Vector3 targetPos = new Vector3(
				physicalCardDeckPos.position.x + xOffset * (effectiveCount - 1),
				physicalCardDeckPos.position.y + yOffset * (effectiveCount - 1),
				physicalCardDeckPos.position.z - zOffset * 0
			);

			var config = new CardMoveConfig
			{
				moveType = CardMoveType.ToPosition, // Use ToPosition to apply the corrected position
				customTarget = targetPos,
				duration = revealToDeckAnimDuration,
				useArc = true,
				arcMidpoint = showPos,
				ease = revealToDeckEase,
				onComplete = onComplete
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
		if (physicalCard == null) return;

		var physScript = physicalCard.GetComponent<CardPhysObjScript>();
		if (physScript == null) return;

		// Calculate target position
		Vector3 targetPosition;
		switch (config.moveType)
		{
			case CardMoveType.ToTop:
				targetPosition = CalculatePositionAtIndex(combatManager.combinedDeckZone.Count - 1);
				break;
			case CardMoveType.ToBottom:
				targetPosition = CalculatePositionAtIndex(0);
				break;
			case CardMoveType.ToIndex:
				targetPosition = CalculatePositionAtIndex(config.targetIndex);
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

		// Create animation sequence
		Sequence moveSequence = DOTween.Sequence();

		if (shouldUseArc)
		{
			// Arc trajectory: Current -> Midpoint -> Target
			float halfDuration = config.duration * 0.5f;
			moveSequence.Append(
				physicalCard.transform.DOMove(arcPoint.position, halfDuration).SetEase(config.ease)
			);
			moveSequence.Append(
				physicalCard.transform.DOMove(targetPosition, halfDuration).SetEase(config.ease)
			);
		}
		else
		{
			// Straight trajectory
			moveSequence.Append(
				physicalCard.transform.DOMove(targetPosition, config.duration).SetEase(config.ease)
			);
		}

		// Scale animation: Final size determined by target type
		Vector3 targetScale = config.moveType == CardMoveType.ToGrave 
			? cardDestroyTargetSize 
			: physicalCardDeckSize;
		moveSequence.Join(
			physicalCard.transform.DOScale(targetScale, config.duration).SetEase(config.ease)
		);

		// Animation complete callback
		moveSequence.OnComplete(() =>
		{
			physScript.isPlayingSpecialAnimation = false;
			physScript.SetTargetPosition(targetPosition);
			physScript.SetTargetScale(targetScale);

			if (config.destroyAfterMove)
			{
				Destroy(physicalCard);
			}

			config.onComplete?.Invoke();
			UpdateAllPhysicalCardTargets();
		});

		moveSequence.Play();
	}

	/// <summary>
	/// Batch move multiple cards (for Stage/Bury operations)
	/// </summary>
	/// <param name="logicalCards">Logical card list</param>
	/// <param name="config">Move config</param>
	/// <param name="onAllComplete">Callback after all animations complete</param>
	public void MoveCardsWithAnimation(List<GameObject> logicalCards, CardMoveConfig config, Action onAllComplete = null)
	{
		if (logicalCards == null || logicalCards.Count == 0)
		{
			onAllComplete?.Invoke();
			return;
		}

		int completedCount = 0;
		int totalCount = logicalCards.Count;

		Action onSingleComplete = () =>
		{
			completedCount++;
			if (completedCount >= totalCount)
			{
				onAllComplete?.Invoke();
			}
		};

		// Create config copy for each card (because callbacks differ)
		foreach (var card in logicalCards)
		{
			var cardConfig = new CardMoveConfig
			{
				moveType = config.moveType,
				targetIndex = config.targetIndex,
				customTarget = config.customTarget,
				useArc = config.useArc,
				arcMidpoint = config.arcMidpoint,
				duration = config.duration,
				ease = config.ease,
				destroyAfterMove = config.destroyAfterMove,
				onComplete = onSingleComplete
			};
			MoveCardWithAnimation(card, cardConfig);
		}
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
		MoveCardWithAnimation(logicalCard, CardMoveConfig.ToIndex(index, duration, useArc, onComplete));
	}

	/// <summary>
	/// Move card to specified world position
	/// </summary>
	public void MoveCardToPosition(GameObject logicalCard, Vector3 position, float duration = 0.5f, bool useArc = true, Action onComplete = null)
	{
		MoveCardWithAnimation(logicalCard, CardMoveConfig.ToPosition(position, duration, useArc, onComplete));
	}

	/// <summary>
	/// Move card to graveyard (destroy position)
	/// </summary>
	public void MoveCardToGrave(GameObject logicalCard, float duration = 0.3f, Action onComplete = null)
	{
		MoveCardWithAnimation(logicalCard, CardMoveConfig.ToGrave(duration, onComplete));
	}

	/// <summary>
	/// Calculate position coordinates at specified index
	/// </summary>
	public Vector3 CalculatePositionAtIndex(int index)
	{
		var count = physicalCardsInDeck.Count;
		var basePos = physicalCardDeckPos.position + _deckFocusOffset;
		// index=0 (bottom of physical deck) has largest offset, index=count-1 (top) has smallest offset
		return new Vector3(
			basePos.x + xOffset * (count - 1 - index),
			basePos.y + yOffset * (count - 1 - index),
			basePos.z - zOffset * index
		);
	}

	/// <summary>
	/// Play Start Card exit animation and execute follow-up
	/// Resolve conflict between Start Card animation and Shuffle
	/// </summary>
	/// <param name="logicalCard">Start Card logical card</param>
	/// <param name="onAnimationComplete">Callback after animation completes (usually pass shuffle logic)</param>
	public void PlayStartCardExitAnimationWithCallback(GameObject logicalCard, Action onAnimationComplete)
	{
		if (logicalCard == null)
		{
			onAnimationComplete?.Invoke();
			return;
		}

		var cardScript = logicalCard.GetComponent<CardScript>();
		if (cardScript == null || !cardScript.isStartCard)
		{
			onAnimationComplete?.Invoke();
			return;
		}

		// Get physical card
		BuildCardScriptToPhysicalDictionary();
		var physicalCard = GetPhysicalCardFromLogicalCard(cardScript);
		if (physicalCard == null)
		{
			onAnimationComplete?.Invoke();
			return;
		}

		// Remove from deck list (no longer participate in position sync)
		physicalCardsInDeck.Remove(physicalCard);
		if (physicalCardInRevealZone == physicalCard)
		{
			physicalCardInRevealZone = null;
		}

		// Determine target position
		Vector3 targetPos = gravePosition != null 
			? gravePosition.position 
			: physicalCardNewTempCardPos.position;
		Vector3 targetSize = cardDestroyTargetSize;

		var physScript = physicalCard.GetComponent<CardPhysObjScript>();
		if (physScript != null)
		{
			physScript.isPlayingSpecialAnimation = true;
		}

		// Create exit animation
		Sequence exitSequence = DOTween.Sequence();
		exitSequence.Append(
			physicalCard.transform.DOMove(targetPos, cardDestroyAnimDuration).SetEase(Ease.InQuad)
		);
		exitSequence.Join(
			physicalCard.transform.DOScale(targetSize, cardDestroyAnimDuration).SetEase(Ease.InQuad)
		);

		exitSequence.OnComplete(() =>
		{
			Destroy(physicalCard);
			onAnimationComplete?.Invoke();
		});

		exitSequence.Play();
	}

	/// <summary>
	/// Play Start Card exit animation and other cards' Shuffle animation simultaneously
	/// Start Card goes directly to graveyard, other cards shuffle
	/// </summary>
	/// <param name="startCard">Start Card logical card</param>
	/// <param name="otherCards">Logical list of other cards (not shuffled)</param>
	/// <param name="onComplete">Callback after all animations complete</param>
	public void PlayStartCardExitWithShuffleAnimation(GameObject startCard, List<GameObject> otherCards, Action onComplete)
	{
		// Block player input
		if (combatManager != null)
			combatManager.blockPlayerInput = true;
			
		if (startCard == null)
		{
			// No Start Card, only play normal shuffle animation
			PlayShuffleAnimation(otherCards, onComplete);
			return;
		}

		// Get Start Card physical card
		BuildCardScriptToPhysicalDictionary();
		var startPhysicalCard = GetPhysicalCardFromLogicalCard(startCard.GetComponent<CardScript>());
		
		// Remove Start Card from physical list (it does not participate in shuffle)
		if (startPhysicalCard != null)
		{
			physicalCardsInDeck.Remove(startPhysicalCard);
			if (physicalCardInRevealZone == startPhysicalCard)
			{
				physicalCardInRevealZone = null;
			}
		}

		// 1. First sync other cards' physical list (after removing Start Card)
		SyncPhysicalCardsWithCombinedDeck();

		// 2. Calculate other cards' positions after Shuffle
		var shuffledCards = UtilityFuncManagerScript.ShuffleList(new List<GameObject>(otherCards));
		var shuffleTargets = CalculateShuffleTargets(shuffledCards);

		// 3. Calculate Start Card's target position (graveyard)
		Vector3 startCardTarget = gravePosition != null 
			? gravePosition.position 
			: physicalCardNewTempCardPos.position;

		// 4. Play both animations simultaneously
		int completedAnimations = 0;
		int totalAnimations = 1 + (startPhysicalCard != null ? 1 : 0); // Shuffle + Start Card

		Action onOneComplete = () =>
		{
			completedAnimations++;
			if (completedAnimations >= totalAnimations)
			{
				// Restore player input
				if (combatManager != null)
					combatManager.blockPlayerInput = false;
				onComplete?.Invoke();
			}
		};

		// Play other cards' Shuffle animation
		PlayShuffleAnimationInternal(shuffleTargets, onOneComplete);

		// Play Start Card exit animation
		if (startPhysicalCard != null)
		{
			var physScript = startPhysicalCard.GetComponent<CardPhysObjScript>();
			if (physScript != null)
			{
				physScript.isPlayingSpecialAnimation = true;
			}

			Sequence exitSequence = DOTween.Sequence();
			exitSequence.Append(
				startPhysicalCard.transform.DOMove(startCardTarget, cardDestroyAnimDuration).SetEase(Ease.InQuad)
			);
			exitSequence.Join(
				startPhysicalCard.transform.DOScale(cardDestroyTargetSize, cardDestroyAnimDuration).SetEase(Ease.InQuad)
			);
			exitSequence.OnComplete(() =>
			{
				Destroy(startPhysicalCard);
				onOneComplete?.Invoke();
			});
			exitSequence.Play();
		}
		else
		{
			onOneComplete?.Invoke();
		}
	}

	/// <summary>
	/// Play Start Card move to random position and other cards' Shuffle animation simultaneously
	/// [Plan B] Logical shuffle completed, play animation based on known shuffle result
	/// </summary>
	/// <param name="startCard">Start Card logical card</param>
	/// <param name="shuffledCards">Already shuffled deck list (contains Start Card, order is determined)</param>
	/// <param name="onComplete">Callback after all animations complete</param>
	public void PlayStartCardShuffleAnimation(GameObject startCard, List<GameObject> shuffledCards, Action onComplete)
	{
		// Block player input
		if (combatManager != null)
			combatManager.blockPlayerInput = true;
			
		// Get Start Card physical card
		BuildCardScriptToPhysicalDictionary();
		var startPhysicalCard = GetPhysicalCardFromLogicalCard(startCard.GetComponent<CardScript>());
		
		// Remove Start Card from reveal zone
		if (physicalCardInRevealZone == startPhysicalCard)
		{
			physicalCardInRevealZone = null;
		}

		// 1. Calculate target position for each card (based on known shuffle result)
		var shuffleTargets = CalculateShuffleTargets(shuffledCards);

		// 2. Play move animations for all cards simultaneously
		// Start Card flies from Reveal Zone directly to new position
		// Other cards fly from current position to new position
		PlayShuffleAnimationInternal(shuffleTargets, () =>
		{
			// 3. After animation completes, rebuild physical card list to match logical order
			RebuildPhysicalDeckFromShuffledList(shuffledCards);
			// Restore player input
			if (combatManager != null)
				combatManager.blockPlayerInput = false;
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
	}

	/// <summary>
	/// Play normal Shuffle animation (without Start Card special handling)
	/// </summary>
	/// <param name="cards">Logical card list (not shuffled)</param>
	/// <param name="onComplete">Callback after animation completes</param>
	public void PlayShuffleAnimation(List<GameObject> cards, Action onComplete)
	{
		// Block player input
		if (combatManager != null)
			combatManager.blockPlayerInput = true;
			
		// First sync physical list
		SyncPhysicalCardsWithCombinedDeck();

		// Calculate positions after Shuffle
		var shuffledCards = UtilityFuncManagerScript.ShuffleList(new List<GameObject>(cards));
		var shuffleTargets = CalculateShuffleTargets(shuffledCards);

		// Play animation
		PlayShuffleAnimationInternal(shuffleTargets, () =>
		{
			// Restore player input
			if (combatManager != null)
				combatManager.blockPlayerInput = false;
			onComplete?.Invoke();
		});
	}

	/// <summary>
	/// Calculate target position for each card after Shuffle
	/// </summary>
	/// <param name="shuffledCards">Card order after shuffle</param>
	/// <returns>Target position for each physical card</returns>
	private Dictionary<GameObject, Vector3> CalculateShuffleTargets(List<GameObject> shuffledCards)
	{
		var targets = new Dictionary<GameObject, Vector3>();
		var count = shuffledCards.Count;

		for (int i = 0; i < shuffledCards.Count; i++)
		{
			var logicalCard = shuffledCards[i].GetComponent<CardScript>();
			if (logicalCard == null) continue;

			var physicalCard = GetPhysicalCardFromLogicalCard(logicalCard);
			if (physicalCard == null) continue;

			Vector3 targetPos = CalculatePositionAtIndex(i);

			targets[physicalCard] = targetPos;
		}

		return targets;
	}

	/// <summary>
	/// Internal method: Play Shuffle move animation
	/// </summary>
	/// <param name="shuffleTargets">Target position for each physical card</param>
	/// <param name="onComplete">Callback after animation completes</param>
	private void PlayShuffleAnimationInternal(Dictionary<GameObject, Vector3> shuffleTargets, Action onComplete)
	{
		if (shuffleTargets.Count == 0)
		{
			onComplete?.Invoke();
			return;
		}

		int completedCount = 0;
		int totalCount = shuffleTargets.Count;
		float shuffleDuration = 0.5f; // Shuffle Animation duration

		// Generate random delay time for each card
		var cardDelays = new Dictionary<GameObject, float>();
		foreach (var kvp in shuffleTargets)
		{
			float delay = useStaggeredShuffleAnimation ? UnityEngine.Random.Range(0f, shuffleStaggerMaxDelay) : 0f;
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
				moveSequence.Append(
					physicalCard.transform.DOMove(showPos.position, shuffleDuration * 0.5f).SetEase(Ease.OutQuad)
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

			// Sync scale
			moveSequence.Join(
				physicalCard.transform.DOScale(physicalCardDeckSize, shuffleDuration).SetEase(Ease.InOutQuad)
			);

			moveSequence.OnComplete(() =>
			{
				if (physScript != null)
				{
					physScript.isPlayingSpecialAnimation = false;
					physScript.SetTargetPosition(targetPos);
					physScript.SetTargetScale(physicalCardDeckSize);
				}

				completedCount++;
				if (completedCount >= totalCount)
					onComplete?.Invoke();
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
		}

		// Only update target positions, do not sort (sorting is handled by SyncPhysicalCardsWithCombinedDeck during shuffle)
		UpdateAllPhysicalCardTargets();
	}

	#endregion

	#region Responsibility 2: Maintain CardScript to Physical Card dictionary

	/// <summary>
	/// Build CardScript -> Physical Card mapping from deck
	/// </summary>
	public void BuildCardScriptToPhysicalDictionary()
	{
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
	/// Get physical card from logical card
	/// </summary>
	public GameObject GetPhysicalCardFromLogicalCard(CardScript logicalCard)
	{
		if (_cardScriptToPhysicalCache.TryGetValue(logicalCard, out var physicalCard))
			return physicalCard;
		return null;
	}

	#endregion

	#region Responsibility 3: Tell Physical Card target position based on list order

	/// <summary>
	/// Update all cards' target positions based on physicalCardsInDeck order
	/// </summary>
	public void UpdateAllPhysicalCardTargets()
	{
		// Guard: skip update when deck is focused to prevent interference
		if (_isDeckFocused)
			return;

		// Update card positions in deck
		for (int i = 0; i < physicalCardsInDeck.Count; i++)
		{
			var card = physicalCardsInDeck[i];
			var physScript = card.GetComponent<CardPhysObjScript>();
			if (physScript == null) continue;

			// Calculate target position
			Vector3 targetPos = CalculatePositionAtIndex(i);
			
			// Set target position and scale (card handles animation in its own Update)
			physScript.SetTargetPosition(targetPos);
			physScript.SetTargetScale(physicalCardDeckSize);
		}
	}

	/// <summary>
	/// Reset all card positions immediately (no animation)
	/// </summary>
	public void ResetPhysicalCardsPositionImmediate()
	{
		for (int i = 0; i < physicalCardsInDeck.Count; i++)
		{
			var physScript = physicalCardsInDeck[i].GetComponent<CardPhysObjScript>();
			if (physScript == null) continue;

			Vector3 pos = new(
			    physicalCardDeckPos.position.x + xOffset * (i + 1),
			    physicalCardDeckPos.position.y + yOffset * (i + 1),
			    physicalCardDeckPos.position.z - zOffset * i
			);

			physScript.SetPositionImmediate(pos);
			physScript.SetScaleImmediate(physicalCardDeckSize);
		}
	}

	#endregion

	#region Deck Focus / Peel System

	/// <summary>
	/// Focus on a card in the deck by peeling cards in front of it
	/// </summary>
	public IEnumerator FocusOnCardCoroutine(CardScript targetCard)
	{
		if (!enablePeelDeck)
			yield break;
		if (targetCard == null)
			yield break;

		// Get physical card
		BuildCardScriptToPhysicalDictionary();
		GameObject physicalCard = GetPhysicalCardFromLogicalCard(targetCard);
		if (physicalCard == null)
		{
			Debug.LogWarning("[CombatUXManager] FocusOnCardCoroutine: No physical card found for " + targetCard.name);
			yield break;
		}

		int targetIndex = GetPhysicalCardDeckIndex(physicalCard);
		if (targetIndex < 0)
		{
			Debug.LogWarning("[CombatUXManager] FocusOnCardCoroutine: Card not found in physical deck");
			yield break;
		}

		// Already focused on this card
		if (_isDeckFocused && _currentFocusCard == targetCard)
			yield break;

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
	}

	/// <summary>
	/// Start peeling deck to expose target card at specified index
	/// </summary>
	private IEnumerator StartPeelCoroutine(int targetIndex)
	{
		var count = physicalCardsInDeck.Count;
		if (count == 0 || targetIndex < 0 || targetIndex >= count)
			yield break;

		// Compute deck focus offset first so reveal zone can follow the same offset
		float desiredX = deckFocusTargetPos != null ? deckFocusTargetPos.position.x : physicalCardDeckPos.position.x;
		float noOffsetX = physicalCardDeckPos.position.x + xOffset * (count - 1 - targetIndex);
		float noOffsetY = physicalCardDeckPos.position.y + yOffset * (count - 1 - targetIndex);
		float offsetX = desiredX - noOffsetX;
		float offsetY = physicalCardDeckPos.position.y - noOffsetY;
		_deckFocusOffset = new Vector3(offsetX, offsetY, 0f);

		// Animate all cards to their final positions (parallel) including reveal zone exit
		int animCompletedCount = 0;
		int animTotalCount = 0;

		// Move reveal zone card out of screen downward (respecting deck offset to keep relative XY unchanged)
		if (physicalCardInRevealZone != null)
		{
			UnityEngine.Debug.Log("[PEEL_REVEAL] StartPeelCoroutine: Moving reveal zone card out. Current pos=" + physicalCardInRevealZone.transform.position);
			var revealPhysScript = physicalCardInRevealZone.GetComponent<CardPhysObjScript>();
			if (revealPhysScript != null)
			{
				revealPhysScript.isPlayingSpecialAnimation = true;
			}

			Vector3 offsetRevealPos = physicalCardRevealPos.position + _deckFocusOffset;
			Vector3 exitPos = offsetRevealPos + Vector3.down * revealCardExitDistance;
			animTotalCount++;
			physicalCardInRevealZone.transform.DOMove(exitPos, peelCardDuration)
				.SetEase(Ease.InOutQuad)
				.SetDelay(peelStaggerDelay)
				.OnComplete(() =>
				{
					animCompletedCount++;
					UnityEngine.Debug.Log("[PEEL_REVEAL] StartPeelCoroutine: Reveal zone card moved out. New pos=" + physicalCardInRevealZone.transform.position);
				});
		}

		_peeledCards.Clear();
		_peeledCardOriginalPositions.Clear();

		for (int i = 0; i < count; i++)
		{
			var card = physicalCardsInDeck[i];
			if (card == null) continue;
			var physScript = card.GetComponent<CardPhysObjScript>();
			if (physScript == null) continue;

			Vector3 basePos = CalculatePositionAtIndex(i);

			if (i > targetIndex)
			{
				// Peel this card
				_peeledCardOriginalPositions[card] = basePos;
				_peeledCards.Add(card);

				Vector3 peelDirection = new Vector3(0f, -1f, 0f).normalized;
				Vector3 peelPos = basePos + peelDirection * peelSlideDistance;

				float peelDelay = (count - 1 - i) * peelStaggerDelay;
				float peelDist = Vector3.Distance(card.transform.position, peelPos);
				UnityEngine.Debug.Log(string.Format("[PEEL_START] index={0} name={1} dist={2:F2} duration={3:F2} delay={4:F2} peeled={5}", i, card.name, peelDist, peelCardDuration, peelDelay, true));
				animTotalCount++;
				physScript.isPlayingSpecialAnimation = true;
				card.transform.DOMove(peelPos, peelCardDuration)
					.SetEase(Ease.InOutQuad)
					.SetDelay(peelDelay)
					.OnComplete(() =>
					{
						animCompletedCount++;
						physScript.isPlayingSpecialAnimation = false;
					});
			}
			else
			{
				// Shift this card to deck position
				float shiftDist = Vector3.Distance(card.transform.position, basePos);
				UnityEngine.Debug.Log(string.Format("[PEEL_START] index={0} name={1} dist={2:F2} duration={3:F2} delay={4:F2} peeled={5}", i, card.name, shiftDist, deckShiftDuration, 0f, false));
				animTotalCount++;
				physScript.isPlayingSpecialAnimation = true;
				physScript.SetTargetPosition(basePos);
				card.transform.DOMove(basePos, deckShiftDuration)
					.SetEase(Ease.OutQuad)
					.OnComplete(() =>
					{
						animCompletedCount++;
						physScript.isPlayingSpecialAnimation = false;
					});
			}
		}

		yield return new WaitUntil(() => animCompletedCount >= animTotalCount);
	}

	/// <summary>
	/// Transition focus from one card to another
	/// </summary>
	private IEnumerator TransitionFocusCoroutine(int newTargetIndex, int currentTargetIndex)
	{
		var count = physicalCardsInDeck.Count;
		if (count == 0 || newTargetIndex < 0 || newTargetIndex >= count)
			yield break;

		// Recompute deck focus offset for new target
		float desiredX = deckFocusTargetPos != null ? deckFocusTargetPos.position.x : physicalCardDeckPos.position.x;
		float noOffsetX = physicalCardDeckPos.position.x + xOffset * (count - 1 - newTargetIndex);
		float noOffsetY = physicalCardDeckPos.position.y + yOffset * (count - 1 - newTargetIndex);
		float offsetX = desiredX - noOffsetX;
		float offsetY = physicalCardDeckPos.position.y - noOffsetY;
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
		var newPeeledOriginalPositions = new Dictionary<GameObject, Vector3>();

		for (int i = 0; i < count; i++)
		{
			var card = physicalCardsInDeck[i];
			if (card == null) continue;
			var physScript = card.GetComponent<CardPhysObjScript>();
			if (physScript == null) continue;

			Vector3 basePos = CalculatePositionAtIndex(i);
			physScript.SetTargetPosition(basePos);

			if (shouldBePeeled.Contains(card))
			{
				// This card should be peeled
				Vector3 peelDirection = new Vector3(0f, -1f, 0f).normalized;
				Vector3 peelPos = basePos + peelDirection * peelSlideDistance;

				newPeeledCards.Add(card);
				newPeeledOriginalPositions[card] = basePos;

				float transDelay = (count - 1 - i) * peelStaggerDelay;
				float transDist = Vector3.Distance(card.transform.position, peelPos);
				UnityEngine.Debug.Log(string.Format("[PEEL_TRANS] index={0} name={1} dist={2:F2} duration={3:F2} delay={4:F2} peeled={5}", i, card.name, transDist, peelCardDuration, transDelay, true));
				animTotalCount++;
				physScript.isPlayingSpecialAnimation = true;
				card.transform.DOMove(peelPos, peelCardDuration)
					.SetEase(Ease.InOutQuad)
					.SetDelay(transDelay)
					.OnComplete(() =>
					{
						animCompletedCount++;
						physScript.isPlayingSpecialAnimation = false;
					});
			}
			else
			{
				// This card stays in deck
				animTotalCount++;
				physScript.isPlayingSpecialAnimation = true;
				physScript.SetTargetPosition(basePos);
				card.transform.DOMove(basePos, deckShiftDuration)
					.SetEase(Ease.OutQuad)
					.OnComplete(() =>
					{
						animCompletedCount++;
						physScript.isPlayingSpecialAnimation = false;
					});
			}
		}

		yield return new WaitUntil(() => animCompletedCount >= animTotalCount);

		// Update peeled state
		_peeledCards = newPeeledCards;
		_peeledCardOriginalPositions = newPeeledOriginalPositions;
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

		// Step 1: peeled cards return to deck position first (offset still applied)
		int step1Completed = 0;
		int step1Total = 0;

		for (int i = 0; i < physicalCardsInDeck.Count; i++)
		{
			var card = physicalCardsInDeck[i];
			if (card == null) continue;
			if (!_peeledCards.Contains(card)) continue;

			var physScript = card.GetComponent<CardPhysObjScript>();
			if (physScript == null) continue;

			Vector3 deckPosWithOffset = _peeledCardOriginalPositions.ContainsKey(card)
				? _peeledCardOriginalPositions[card]
				: CalculatePositionAtIndex(i);
			physScript.isPlayingSpecialAnimation = true;
			physScript.SetTargetPosition(deckPosWithOffset);

			float delay1 = i * peelStaggerDelay;
			float dist1 = Vector3.Distance(card.transform.position, deckPosWithOffset);
			UnityEngine.Debug.Log(string.Format("[PEEL_RESTORE] Step1 index={0} name={1} dist={2:F2} duration={3:F2} delay={4:F2} peeled={5}", i, card.name, dist1, peelCardDuration, delay1, true));
			step1Total++;
			card.transform.DOMove(deckPosWithOffset, peelCardDuration)
				.SetEase(Ease.InOutQuad)
				.SetDelay(delay1)
				.OnComplete(() =>
				{
					step1Completed++;
					physScript.isPlayingSpecialAnimation = false;
				});
		}

		if (step1Total > 0)
		{
			yield return new WaitUntil(() => step1Completed >= step1Total);
		}

		// Step 2: all cards shift back to normal position together (offset cleared)
		_deckFocusOffset = Vector3.zero;

		int step2Completed = 0;
		int step2Total = 0;

		for (int i = 0; i < physicalCardsInDeck.Count; i++)
		{
			var card = physicalCardsInDeck[i];
			if (card == null) continue;
			var physScript = card.GetComponent<CardPhysObjScript>();
			if (physScript == null) continue;

			Vector3 finalPos = CalculatePositionAtIndex(i);
			physScript.isPlayingSpecialAnimation = true;
			physScript.SetTargetPosition(finalPos);

			float dist2 = Vector3.Distance(card.transform.position, finalPos);
			UnityEngine.Debug.Log(string.Format("[PEEL_RESTORE] Step2 index={0} name={1} dist={2:F2} duration={3:F2} delay={4:F2} peeled={5}", i, card.name, dist2, deckShiftDuration, 0f, _peeledCards.Contains(card)));
			step2Total++;
			card.transform.DOMove(finalPos, deckShiftDuration)
				.SetEase(Ease.OutQuad)
				.OnComplete(() =>
				{
					step2Completed++;
					physScript.isPlayingSpecialAnimation = false;
				});
		}

		// Restore reveal zone card back to reveal position (parallel with step 2)
		if (physicalCardInRevealZone != null)
		{
			Vector3 revealPos = physicalCardRevealPos.position;
			var revealPhysScript = physicalCardInRevealZone.GetComponent<CardPhysObjScript>();

			step2Total++;
			physicalCardInRevealZone.transform.DOMove(revealPos, peelCardDuration)
				.SetEase(Ease.InOutQuad)
				.SetDelay(peelStaggerDelay)
				.OnComplete(() =>
				{
					step2Completed++;
					if (revealPhysScript != null)
					{
						revealPhysScript.SetTargetPosition(revealPos);
						revealPhysScript.SetTargetScale(physicalCardRevealSize);
						revealPhysScript.isPlayingSpecialAnimation = false;
					}
				});
		}

		yield return new WaitUntil(() => step2Completed >= step2Total);

		// Clear focus state
		_isDeckFocused = false;
		_currentFocusCard = null;
		_peeledCards.Clear();
		_peeledCardOriginalPositions.Clear();

		// Update all physical card targets to ensure they are in sync
		UpdateAllPhysicalCardTargets();
	}

	/// <summary>
	/// Lift a card in the deck slightly for secondary animation (receiver)
	/// Does NOT interfere with PeelDeck focus state
	/// </summary>
	public IEnumerator LiftCardInDeckCoroutine(CardScript targetCard)
	{
		if (!enableLiftCardInDeck)
			yield break;
		if (targetCard == null)
			yield break;

		BuildCardScriptToPhysicalDictionary();
		GameObject physicalCard = GetPhysicalCardFromLogicalCard(targetCard);
		if (physicalCard == null)
			yield break;

		Vector3 originalPos = physicalCard.transform.position;
		Vector3 liftPos = originalPos + Vector3.up * secondaryLiftHeight;

		bool liftCompleted = false;
		physicalCard.transform.DOMove(liftPos, secondaryLiftDuration)
			.SetEase(Ease.OutQuad)
			.OnComplete(() => liftCompleted = true);
		yield return new WaitUntil(() => liftCompleted);

		// Hold briefly then return
		yield return new WaitForSeconds(0.1f);

		bool lowerCompleted = false;
		physicalCard.transform.DOMove(originalPos, secondaryLiftDuration)
			.SetEase(Ease.InOutQuad)
			.OnComplete(() => lowerCompleted = true);
		yield return new WaitUntil(() => lowerCompleted);
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
	}

	/// <summary>
	/// Destroy specified physical card (immediate, no animation)
	/// </summary>
	public void DestroyPhysicalCard(GameObject physicalCard)
	{
		if (physicalCard == null) return;

		// Remove from deck list
		physicalCardsInDeck.Remove(physicalCard);

		// Remove from dictionary cache
		var physScript = physicalCard.GetComponent<CardPhysObjScript>();
		if (physScript?.cardImRepresenting != null)
		{
			_cardScriptToPhysicalCache.Remove(physScript.cardImRepresenting);
		}

		// Destroy GameObject
		Destroy(physicalCard);
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
		var physicalCard = GetPhysicalCardFromLogicalCard(cardScript);
		
		// Remove logical card from combined deck
		if (combatManager != null && combatManager.combinedDeckZone.Contains(logicalCard))
		{
			combatManager.combinedDeckZone.Remove(logicalCard);
		}

		// If no physical card, destroy logical card directly
		if (physicalCard == null)
		{
			Destroy(logicalCard);
			onComplete?.Invoke();
			return;
		}

		// Remove from deck list and cache (prevent use by other logic during animation)
		physicalCardsInDeck.Remove(physicalCard);
		_cardScriptToPhysicalCache.Remove(cardScript);

		// Create exit animation
		Sequence destroySequence = DOTween.Sequence();

		// Move to grave position (if set)
		if (gravePosition != null)
		{
			destroySequence.Append(
				physicalCard.transform.DOMove(gravePosition.position, cardDestroyAnimDuration)
					.SetEase(Ease.InQuad)
			);
		}

		// Shrink
		destroySequence.Join(
			physicalCard.transform.DOScale(cardDestroyTargetSize, cardDestroyAnimDuration)
				.SetEase(Ease.InQuad)
		);

		// Destroy after animation completes
		destroySequence.OnComplete(() =>
		{
			Destroy(physicalCard);
			Destroy(logicalCard);
			onComplete?.Invoke();
		});
	}

	/// <summary>
	/// Play Start Card exit animation: move to newCardPos and shrink, execute callback when complete
	/// Note: Now recommended to use DestroyCardWithAnimation as the unified card destruction method
	/// </summary>
	public void PlayStartCardExitAnimation(GameObject physicalCard, System.Action onComplete)
	{
		if (physicalCard == null)
		{
			onComplete?.Invoke();
			return;
		}

		var physScript = physicalCard.GetComponent<CardPhysObjScript>();
		if (physScript == null)
		{
			onComplete?.Invoke();
			return;
		}

		// Remove from deck list (no longer participate in position sync)
		physicalCardsInDeck.Remove(physicalCard);

		// Stop any ongoing animation on this card
		physScript.SetPositionImmediate(physicalCard.transform.position);
		physScript.SetScaleImmediate(physicalCard.transform.localScale);

		// Create exit animation sequence
		Sequence exitSequence = DOTween.Sequence();

		// Move to newCardPos
		exitSequence.Append(
			physicalCard.transform.DOMove(physicalCardNewTempCardPos.position, 0.3f)
				.SetEase(Ease.InOutQuad)
		);

		// Sync shrink
		exitSequence.Join(
			physicalCard.transform.DOScale(physicalCardNewTempCardSize, 0.3f)
				.SetEase(Ease.InOutQuad)
		);

		// Execute callback after animation completes
		exitSequence.OnComplete(() =>
		{
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
		if (combatManager != null)
		{
			combatManager.blockPlayerInput = false;
		}
		
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

		physScript.cardImRepresenting = cardScript;
		newPhysicalCard.name = logicalCard.name + "'s physical card";
		physScript.cardNamePrint.text = cardScript != null ? cardScript.GetDisplayName() : logicalCard.name;
		physScript.cardDescPrint.text = cardScript.cardDesc;

		// Set initial scale
		physScript.SetScaleImmediate(physicalCardDeckSize);

		// Insert into physical card list
		physicalCardsInDeck.Insert(0, newPhysicalCard);

		// Set initial position (new card appears at physical card new temp card pos)
		Vector3 startPos = physicalCardNewTempCardPos.position;
		physScript.SetPositionImmediate(startPos);
		// set initial size
		Vector3 startSize = physicalCardNewTempCardSize;
		physScript.SetScaleImmediate(startSize);


		// Update all card target positions (trigger move animation)
		UpdateAllPhysicalCardTargets();
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

			physScript.cardImRepresenting = cardScript;
			newPhysicalCard.name = card.name + "'s physical card";
			
			// Normal cards: set name and description
			if (cardScript != null && !cardScript.isStartCard)
			{
				physScript.cardNamePrint.text = cardScript != null ? cardScript.GetDisplayName() : card.name;
				physScript.cardDescPrint.text = cardScript.cardDesc;
			}

			// Set initial position and scale immediately
			physScript.SetScaleImmediate(physicalCardDeckSize);

			physicalCardsInDeck.Add(newPhysicalCard);
		}

		// Set initial position
		for (int i = 0; i < physicalCardsInDeck.Count; i++)
		{
			var physScript = physicalCardsInDeck[i].GetComponent<CardPhysObjScript>();
			var count = physicalCardsInDeck.Count;
			Vector3 pos = new(
			    physicalCardDeckPos.position.x + xOffset * (count - 1 - i),
			    physicalCardDeckPos.position.y + yOffset * (count - 1 - i),
			    physicalCardDeckPos.position.z - zOffset * i
			);
			physScript.SetPositionImmediate(pos);
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

	/// <summary>
	/// Play parabolic projectile effect of status effect flying from giver to receiver
	/// onComplete callback is executed after the effect reaches the target
	/// </summary>
	/// <param name="giverCard">Giver logical card</param>
	/// <param name="receiverCard">Receiver logical card</param>
	/// <param name="onComplete">Effect complete callback (executed after effect reaches target)</param>
	public void PlayStatusEffectProjectile(GameObject giverCard, GameObject receiverCard, Action onComplete = null)
	{
		if (statusEffectProjectilePrefab == null || giverCard == null || receiverCard == null)
		{
			onComplete?.Invoke();
			return;
		}

		// Get physical card positions
		BuildCardScriptToPhysicalDictionary();
		
		Vector3 startPos = GetCardWorldPosition(giverCard) + projectileStartOffset;
		Vector3 endPos = GetCardWorldPosition(receiverCard) + projectileEndOffset;
		print("end pos: "+endPos);

		// Create effect instance
		GameObject projectile = Instantiate(statusEffectProjectilePrefab, startPos, Quaternion.identity);
		
		// Calculate parabolic midpoint
		Vector3 midPoint = Vector3.Lerp(startPos, endPos, 0.5f) + Vector3.up * projectileArcHeight;

		// Create parabolic animation
		Sequence projectileSequence = DOTween.Sequence();
		
		// Phase 1: From start to midpoint (ascending)
		projectileSequence.Append(
			projectile.transform.DOMove(midPoint, projectileDuration * 0.5f)
				.SetEase(Ease.OutQuad)
		);
		
		// Phase 2: From midpoint to end (descending)
		projectileSequence.Append(
			projectile.transform.DOMove(endPos, projectileDuration * 0.5f)
				.SetEase(Ease.InQuad)
		);
		
		// Sync rotation: keep effect facing target
		projectile.transform.LookAt(endPos);

		// Animation complete: destroy effect and execute callback
		projectileSequence.OnComplete(() =>
		{
			
			Destroy(projectile);
			print("projectile destroyed");
			onComplete?.Invoke();
		});

		projectileSequence.Play();
	}

	/// <summary>
	/// Play multiple status effect projectile animations, supports staggered playback
	/// Execute corresponding callback after effect reaches each target, final callback after all complete
	/// </summary>
	/// <param name="giverCard">Giver logical card</param>
	/// <param name="targetCards">Target card list (CardScript)</param>
	/// <param name="onEachComplete">Callback when each effect completes (parameter is target CardScript)</param>
	/// <param name="onAllComplete">Callback after all effects complete</param>
	/// <param name="customStaggerDelay">Custom stagger delay (null uses default value)</param>
	public void PlayMultiStatusEffectProjectile(
		GameObject giverCard,
		List<CardScript> targetCards,
		System.Action<CardScript> onEachComplete,
		System.Action onAllComplete = null,
		float? customStaggerDelay = null)
	{
		if (targetCards == null || targetCards.Count == 0)
		{
			onAllComplete?.Invoke();
			return;
		}

		// If prefab is not configured, execute effect directly (no animation)
		if (statusEffectProjectilePrefab == null || giverCard == null)
		{
			foreach (var target in targetCards)
			{
				onEachComplete?.Invoke(target);
			}
			onAllComplete?.Invoke();
			return;
		}

		float staggerDelay = customStaggerDelay ?? projectileStaggerDelay;
		int completedCount = 0;
		int totalCount = targetCards.Count;

		for (int i = 0; i < targetCards.Count; i++)
		{
			var targetCardScript = targetCards[i];
			
			// Staggered playback time
			DOVirtual.DelayedCall(i * staggerDelay, () =>
			{
				PlayStatusEffectProjectile(
					giverCard, 
					targetCardScript.gameObject, 
					() =>
					{
						// Single effect complete, execute effect for this target
						onEachComplete?.Invoke(targetCardScript);
						
						completedCount++;
						if (completedCount >= totalCount)
						{
							onAllComplete?.Invoke();
						}
					}
				);
			});
		}
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
}
