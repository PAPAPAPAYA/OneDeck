using System;
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
	
	// 便捷构造方法
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

	// 物理卡片列表（根�?combined deck zone 更新�?
	public List<GameObject> physicalCardsInDeck = new();
	
	// 揭晓区域的物理卡片（单独存储，防止与 deck 混淆�?
	public GameObject physicalCardInRevealZone;

	// CardScript �?Physical Card 的字典（维护这个映射�?
	private Dictionary<CardScript, GameObject> _cardScriptToPhysicalCache = new();

	private void OnEnable()
	{
		if (combatManager == null)
			combatManager = CombatManager.Me;
	}

	#region Responsibility 1: Update physical card list based on logical zone

	/// <summary>
	/// 根据 combined deck zone 更新 physicalCardsInDeck 的顺�?
	/// Note: Cards in revealZone are not added to this list, managed separately by physicalCardInRevealZone
	/// </summary>
	public void SyncPhysicalCardsWithCombinedDeck()
	{
		if (physicalCardsInDeck.Count == 0 && physicalCardInRevealZone == null) return;

		// 重建字典（包含 deck 和 reveal zone 的卡牌）
		BuildCardScriptToPhysicalDictionary();

		// 根据 combinedDeckZone 重新排序 physicalCardsInDeck
		physicalCardsInDeck.Clear();
		foreach (var logicalCard in combatManager.combinedDeckZone)
		{
			var cardScript = logicalCard.GetComponent<CardScript>();
			if (cardScript != null && _cardScriptToPhysicalCache.TryGetValue(cardScript, out var physicalCard))
			{
				physicalCardsInDeck.Add(physicalCard);
			}
		}

		// 注意：revealZone 中的卡不加入 physicalCardsInDeck
		// 它由 physicalCardInRevealZone 单独管理，位置由单独的 reveal 逻辑控制
	}

	/// <summary>
	/// Move card from deck to reveal zone
	/// </summary>
	public void MovePhysicalCardToRevealZone(GameObject physicalCard)
	{
		// 从牌组移�?
		physicalCardsInDeck.Remove(physicalCard);

		// 存储到揭晓区�?
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
	/// <param name="card">逻辑卡片 GameObject</param>
	/// <param name="onComplete">Animation complete callback（可选）</param>
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

		// 通过逻辑卡找物理�?
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

		// 如果有配置 showPos，使用通用动画系统
		if (showPos != null)
		{
			// [Key Fix] When calculating target position, consider that one card will be revealed
			// At this time physicalCardsInDeck contains the card about to be revealed, but it will be removed when animation completes
			// So effectiveCount = physicalCardsInDeck.Count - 1 is needed to calculate correct position
			int effectiveCount = physicalCardsInDeck.Count - 1;
			if (effectiveCount < 1) effectiveCount = 1; // 至少为1，避免计算错误
			
			Vector3 targetPos = new Vector3(
				physicalCardDeckPos.position.x + xOffset * (effectiveCount - 1),
				physicalCardDeckPos.position.y + yOffset * (effectiveCount - 1),
				physicalCardDeckPos.position.z - zOffset * 0
			);

			var config = new CardMoveConfig
			{
				moveType = CardMoveType.ToPosition, // 使用 ToPosition 以使用修正后的位置
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
	/// <param name="logicalCard">逻辑卡片 GameObject</param>
	/// <param name="config">移动配置</param>
	public void MoveCardWithAnimation(GameObject logicalCard, CardMoveConfig config)
	{
		if (logicalCard == null || config == null) return;

		var cardScript = logicalCard.GetComponent<CardScript>();
		if (cardScript == null) return;

		// 获取物理卡片
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
	/// <param name="logicalCards">逻辑卡片列表</param>
	/// <param name="config">移动配置</param>
	/// <param name="onAllComplete">所有Animation complete后的回调</param>
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
	private Vector3 CalculatePositionAtIndex(int index)
	{
		var count = physicalCardsInDeck.Count;
		// index=0（物理卡组底部）偏移最大，index=count-1（顶部）偏移最小
		return new Vector3(
			physicalCardDeckPos.position.x + xOffset * (count - 1 - index),
			physicalCardDeckPos.position.y + yOffset * (count - 1 - index),
			physicalCardDeckPos.position.z - zOffset * index
		);
	}

	/// <summary>
	/// Play Start Card exit animation and execute follow-up
	/// Resolve conflict between Start Card animation and Shuffle
	/// </summary>
	/// <param name="logicalCard">Start Card 逻辑卡片</param>
	/// <param name="onAnimationComplete">Animation complete后的回调（通常传入 Shuffle 逻辑）</param>
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

		// 获取物理卡片
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
	/// <param name="startCard">Start Card 逻辑卡片</param>
	/// <param name="otherCards">其他卡片的逻辑列表（未 Shuffle）</param>
	/// <param name="onComplete">所有Animation complete后的回调</param>
	public void PlayStartCardExitWithShuffleAnimation(GameObject startCard, List<GameObject> otherCards, Action onComplete)
	{
		// Block player input
		if (combatManager != null)
			combatManager.blockPlayerInput = true;
			
		if (startCard == null)
		{
			// 没有 Start Card，只执行普通 Shuffle 动画
			PlayShuffleAnimation(otherCards, onComplete);
			return;
		}

		// 获取 Start Card 的物理卡片
		BuildCardScriptToPhysicalDictionary();
		var startPhysicalCard = GetPhysicalCardFromLogicalCard(startCard.GetComponent<CardScript>());
		
		// 从物理列表中移除 Start Card（它不参与 Shuffle）
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
				// 恢复玩家输入
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
	/// <param name="startCard">Start Card 逻辑卡片</param>
	/// <param name="shuffledCards">已经洗好的牌组列表（包含 Start Card，顺序已确定）</param>
	/// <param name="onComplete">所有Animation complete后的回调</param>
	public void PlayStartCardShuffleAnimation(GameObject startCard, List<GameObject> shuffledCards, Action onComplete)
	{
		// Block player input
		if (combatManager != null)
			combatManager.blockPlayerInput = true;
			
		// 获取 Start Card 的物理卡片
		BuildCardScriptToPhysicalDictionary();
		var startPhysicalCard = GetPhysicalCardFromLogicalCard(startCard.GetComponent<CardScript>());
		
		// 从 reveal zone 移除 Start Card
		if (physicalCardInRevealZone == startPhysicalCard)
		{
			physicalCardInRevealZone = null;
		}

		// 1. 计算每张卡的目标位置（基于已知的洗牌结果）
		var shuffleTargets = CalculateShuffleTargets(shuffledCards);

		// 2. 同时播放所有卡片的移动动画
		// Start Card 从 Reveal Zone 直接飞到新位置
		// 其他卡片从当前位置飞到新位置
		PlayShuffleAnimationInternal(shuffleTargets, () =>
		{
			// 3. Animation complete后，重建物理卡片列表以匹配逻辑顺序
			RebuildPhysicalDeckFromShuffledList(shuffledCards);
			// 恢复玩家输入
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
	/// <param name="cards">卡片逻辑列表（未 Shuffle）</param>
	/// <param name="onComplete">Animation complete后的回调</param>
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
			// 恢复玩家输入
			if (combatManager != null)
				combatManager.blockPlayerInput = false;
			onComplete?.Invoke();
		});
	}

	/// <summary>
	/// Calculate target position for each card after Shuffle
	/// </summary>
	/// <param name="shuffledCards">Shuffle 后的卡片顺序</param>
	/// <returns>每张物理卡片对应的目标位置</returns>
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
	/// <param name="shuffleTargets">每张物理卡片的目标位置</param>
	/// <param name="onComplete">Animation complete后的回调</param>
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

			// 使用弧形轨迹或直接移动
			Sequence moveSequence = DOTween.Sequence();

			// 添加随机延迟（如果使用 staggered 动画）
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
				// 直接移动
				moveSequence.Append(
					physicalCard.transform.DOMove(targetPos, shuffleDuration).SetEase(Ease.InOutQuad)
				);
			}

			// 同步缩放
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

		// 只更新目标位置，不排序（排序�?Shuffle 时的 SyncPhysicalCardsWithCombinedDeck 处理�?
		UpdateAllPhysicalCardTargets();
	}

	#endregion

	#region 职责2：维�?CardScript �?Physical Card 的字�?

	/// <summary>
	/// 从牌组构�?CardScript -> Physical Card 映射
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

		// 包含 reveal zone 中的卡片
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
		// 更新牌组中的卡片位置
		for (int i = 0; i < physicalCardsInDeck.Count; i++)
		{
			var card = physicalCardsInDeck[i];
			var physScript = card.GetComponent<CardPhysObjScript>();
			if (physScript == null) continue;

			// Calculate target position
			Vector3 targetPos = CalculatePositionAtIndex(i);
			
			// Set target position和缩放（卡片自己在Update 中处理动画）
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

	#region Cleanup

	/// <summary>
	/// Destroy all physical cards and clear lists
	/// </summary>
	public void ClearAllPhysicalCards()
	{
		// Stop all special animations that may be playing
		StopAllSpecialAnimations();
		
		// 销毁牌组中的物理卡�?
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
	/// 销毁指定的物理卡牌（立即销毁，无动画）
	/// </summary>
	public void DestroyPhysicalCard(GameObject physicalCard)
	{
		if (physicalCard == null) return;

		// 从牌组列表中移除
		physicalCardsInDeck.Remove(physicalCard);

		// 从字典缓存中移除
		var physScript = physicalCard.GetComponent<CardPhysObjScript>();
		if (physScript?.cardImRepresenting != null)
		{
			_cardScriptToPhysicalCache.Remove(physScript.cardImRepresenting);
		}

		// 销毁 GameObject
		Destroy(physicalCard);
	}

	/// <summary>
	/// 统一销毁卡片（带动画）：移动到 gravePosition 并缩小，然后销毁 physical 和 logical card
	/// </summary>
	/// <param name="logicalCard">逻辑卡牌 GameObject</param>
	/// <param name="onComplete">Animation complete callback</param>
	public void DestroyCardWithAnimation(GameObject logicalCard, System.Action onComplete = null)
	{
		if (logicalCard == null)
		{
			onComplete?.Invoke();
			return;
		}

		// 获取 CardScript
		var cardScript = logicalCard.GetComponent<CardScript>();
		if (cardScript == null)
		{
			// 没有 CardScript，直接销毁逻辑卡
			Destroy(logicalCard);
			onComplete?.Invoke();
			return;
		}

		// 获取对应的 physical card
		var physicalCard = GetPhysicalCardFromLogicalCard(cardScript);
		
		// 从 combined deck 中移除逻辑卡
		if (combatManager != null && combatManager.combinedDeckZone.Contains(logicalCard))
		{
			combatManager.combinedDeckZone.Remove(logicalCard);
		}

		// 如果没有 physical card，直接销毁逻辑卡
		if (physicalCard == null)
		{
			Destroy(logicalCard);
			onComplete?.Invoke();
			return;
		}

		// 从牌组列表和缓存中移除（防止动画期间被其他逻辑使用）
		physicalCardsInDeck.Remove(physicalCard);
		_cardScriptToPhysicalCache.Remove(cardScript);

		// Create exit animation
		Sequence destroySequence = DOTween.Sequence();

		// 移动到 grave position（如果设置了）
		if (gravePosition != null)
		{
			destroySequence.Append(
				physicalCard.transform.DOMove(gravePosition.position, cardDestroyAnimDuration)
					.SetEase(Ease.InQuad)
			);
		}

		// 缩小
		destroySequence.Join(
			physicalCard.transform.DOScale(cardDestroyTargetSize, cardDestroyAnimDuration)
				.SetEase(Ease.InQuad)
		);

		// Animation complete后销毁
		destroySequence.OnComplete(() =>
		{
			Destroy(physicalCard);
			Destroy(logicalCard);
			onComplete?.Invoke();
		});
	}

	/// <summary>
	/// Play Start Card exit animation：移动到 newCardPos 并缩小，完成后执行回调
	/// 注意：现在推荐使用 DestroyCardWithAnimation 作为统一的卡片销毁方法
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

		// 停止该卡牌上可能正在进行的动画
		physScript.SetPositionImmediate(physicalCard.transform.position);
		physScript.SetScaleImmediate(physicalCard.transform.localScale);

		// Create exit animation序列
		Sequence exitSequence = DOTween.Sequence();

		// 移动到 newCardPos
		exitSequence.Append(
			physicalCard.transform.DOMove(physicalCardNewTempCardPos.position, 0.3f)
				.SetEase(Ease.InOutQuad)
		);

		// 同步缩小
		exitSequence.Join(
			physicalCard.transform.DOScale(physicalCardNewTempCardSize, 0.3f)
				.SetEase(Ease.InOutQuad)
		);

		// Animation complete后执行回调
		exitSequence.OnComplete(() =>
		{
			onComplete?.Invoke();
		});
	}
	
	/// <summary>
	/// 停止所有物理卡片的特殊动画
	/// </summary>
	public void StopAllSpecialAnimations()
	{
		// 停止所有属于本对象�?DOTween 延迟调用
		DOTween.Kill(this);
		
		// 恢复玩家输入
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
	/// 为逻辑卡片创建对应的物理卡片并插入�?deck �?
	/// </summary>
	public void AddPhysicalCardToDeck(GameObject logicalCard)
	{
		CardScript cardScript = logicalCard.GetComponent<CardScript>();

		// 根据卡片类型选择预制体
		GameObject prefabToUse = physicalCardPrefab;
		if (cardScript != null)
		{
			if (cardScript.isMinion)
				prefabToUse = minionPhysicalPrefab;
		}

		// 创建物理卡片
		GameObject newPhysicalCard = Instantiate(prefabToUse);
		CardPhysObjScript physScript = newPhysicalCard.GetComponent<CardPhysObjScript>();

		physScript.cardImRepresenting = cardScript;
		newPhysicalCard.name = logicalCard.name + "'s physical card";
		physScript.cardNamePrint.text = logicalCard.name;
		physScript.cardDescPrint.text = cardScript.cardDesc;

		// 设置初始缩放
		physScript.SetScaleImmediate(physicalCardDeckSize);

		// 插入到物理卡片列�?
		physicalCardsInDeck.Insert(0, newPhysicalCard);

		// 设置初始位置 (new card appears at physical card new temp card pos)
		Vector3 startPos = physicalCardNewTempCardPos.position;
		physScript.SetPositionImmediate(startPos);
		// set initial size
		Vector3 startSize = physicalCardNewTempCardSize;
		physScript.SetScaleImmediate(startSize);


		// 更新所有卡片目标位置（触发移动动画�?
		UpdateAllPhysicalCardTargets();
	}

	#region 初始化

	/// <summary>
	/// 实例化所有物理卡片（包括 Start Card�?
	/// </summary>
	public void InstantiateAllPhysicalCards()
	{
		if (physicalCardsInDeck.Count > 0) return;

		foreach (var card in combatManager.combinedDeckZone)
		{
			CardScript cardScript = card.GetComponent<CardScript>();
			
			// 根据卡片类型选择预制体
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
			
			// 普通卡片设置名称和描述
			if (cardScript != null && !cardScript.isStartCard)
			{
				physScript.cardNamePrint.text = card.name;
				physScript.cardDescPrint.text = cardScript.cardDesc;
			}

			// 立即设置初始位置和缩�?
			physScript.SetScaleImmediate(physicalCardDeckSize);

			physicalCardsInDeck.Add(newPhysicalCard);
		}

		// 设置初始位置
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

		// 重建字典映射
		BuildCardScriptToPhysicalDictionary();
	}

	#endregion

	#region Status Effect 飞行特效系统

	[Header("STATUS EFFECT PROJECTILE")]
	[Tooltip("状态效果飞行特效预制体（可以是Sprite、粒子系统或简单的GameObject）")]
	public GameObject statusEffectProjectilePrefab;
	[Tooltip("特效飞行持续时间")]
	public float projectileDuration = 0.4f;
	[Tooltip("抛物线高度")]
	public float projectileArcHeight = 2f;
	[Tooltip("特效起始位置偏移")]
	public Vector3 projectileStartOffset = new Vector3(0, 0.5f, 0);
	[Tooltip("特效目标位置偏移")]
	public Vector3 projectileEndOffset = new Vector3(0, 0.5f, 0);
	[Tooltip("多个特效错开播放的间隔时间（秒）")]
	public float projectileStaggerDelay = 0.05f;

	/// <summary>
	/// 播放状态效果从给予者飞向被给予者的抛物线特效
	/// 特效飞到目标后才执行 onComplete 回调
	/// </summary>
	/// <param name="giverCard">给予者逻辑卡片</param>
	/// <param name="receiverCard">被给予者逻辑卡片</param>
	/// <param name="onComplete">特效Complete callback（特效到达目标后执行）</param>
	public void PlayStatusEffectProjectile(GameObject giverCard, GameObject receiverCard, Action onComplete = null)
	{
		if (statusEffectProjectilePrefab == null || giverCard == null || receiverCard == null)
		{
			onComplete?.Invoke();
			return;
		}

		// 获取物理卡片位置
		BuildCardScriptToPhysicalDictionary();
		
		Vector3 startPos = GetCardWorldPosition(giverCard) + projectileStartOffset;
		Vector3 endPos = GetCardWorldPosition(receiverCard) + projectileEndOffset;
		print("end pos: "+endPos);

		// 创建特效实例
		GameObject projectile = Instantiate(statusEffectProjectilePrefab, startPos, Quaternion.identity);
		
		// 计算抛物线中间点
		Vector3 midPoint = Vector3.Lerp(startPos, endPos, 0.5f) + Vector3.up * projectileArcHeight;

		// 创建抛物线动画
		Sequence projectileSequence = DOTween.Sequence();
		
		// 第一阶段：从起点到中间点（上升）
		projectileSequence.Append(
			projectile.transform.DOMove(midPoint, projectileDuration * 0.5f)
				.SetEase(Ease.OutQuad)
		);
		
		// 第二阶段：从中间点到终点（下降）
		projectileSequence.Append(
			projectile.transform.DOMove(endPos, projectileDuration * 0.5f)
				.SetEase(Ease.InQuad)
		);
		
		// 同步旋转：让特效始终朝向目标
		projectile.transform.LookAt(endPos);

		// Animation complete：销毁特效并执行回调
		projectileSequence.OnComplete(() =>
		{
			Destroy(projectile);
			onComplete?.Invoke();
		});

		projectileSequence.Play();
	}

	/// <summary>
	/// 播放多个状态效果投射物动画，支持错开播放
	/// 特效飞到每个目标后执行对应的回调，全部完成后执行最终回调
	/// </summary>
	/// <param name="giverCard">给予者逻辑卡片</param>
	/// <param name="targetCards">目标卡片列表（CardScript）</param>
	/// <param name="onEachComplete">每个特效完成时的回调（参数为目标CardScript）</param>
	/// <param name="onAllComplete">所有特效完成后的回调</param>
	/// <param name="customStaggerDelay">自定义错开时间（null则使用默认值）</param>
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

		// 如果没有配置预制体，直接执行效果（无动画）
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
			
			// 错开播放时间
			DOVirtual.DelayedCall(i * staggerDelay, () =>
			{
				PlayStatusEffectProjectile(
					giverCard, 
					targetCardScript.gameObject, 
					() =>
					{
						// 单个特效完成，执行该目标的效果
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
	/// 获取卡片的实际世界位置（优先使用物理卡片）
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
