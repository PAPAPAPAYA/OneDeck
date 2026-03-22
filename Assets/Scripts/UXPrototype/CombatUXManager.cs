using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// 卡片移动类型
/// </summary>
public enum CardMoveType
{
	ToTop,          // 移动到牌组顶部（最后一张）
	ToBottom,       // 移动到牌组底部（第一张）
	ToIndex,        // 移动到指定索引
	ToPosition,     // 移动到指定世界坐标
	ToGrave,        // 移动到墓地（销毁位置）
}

/// <summary>
/// 卡片移动配置
/// </summary>
[Serializable]
public class CardMoveConfig
{
	public CardMoveType moveType = CardMoveType.ToBottom;
	public int targetIndex;                    // ToIndex 时使用
	public Vector3? customTarget;              // ToPosition 时使用
	public bool useArc = true;                 // 是否使用弧形轨迹
	public Transform arcMidpoint;              // 弧形轨迹中间点（null 则使用 showPos）
	public float duration = 0.5f;              // 动画持续时间
	public Ease ease = Ease.InOutQuad;         // 缓动类型
	public bool destroyAfterMove = false;      // 移动后是否销毁
	public Action onComplete;                  // 动画完成回调
	public Action onStart;                     // 动画开始回调
	
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
	[Tooltip("是否启用 Stage/Bury 卡片动画")]
	public bool enableStageBuryAnimation = true;
	[Tooltip("洗牌动画是否使用随机先后顺序（staggered）")]
	public bool useStaggeredShuffleAnimation = true;
	[Tooltip("洗牌动画最大随机延迟时间（秒）")]
	public float shuffleStaggerMaxDelay = 0.3f;
	[Tooltip("Deck卡牌X轴偏移（每张卡牌向右偏移量）")]
	public float xOffset;
	[Tooltip("Deck卡牌Y轴偏移（每张卡牌向上偏移量）")]
	public float yOffset;
	[Header("NEW CARD")]
	public Transform physicalCardNewTempCardPos;
	public Vector3 physicalCardNewTempCardSize;

	[Header("DECK")]
	public GameObject physicalCardPrefab;
	public GameObject startCardPhysicalPrefab; // Start Card 的物理预制体（外观不同）
	public GameObject minionPhysicalPrefab; // Minion 卡片的物理预制体（外观不同）
	public Transform physicalCardDeckPos;
	public Vector3 physicalCardDeckSize;

	[Header("REVEAL")]
	public Transform physicalCardRevealPos;
	public Vector3 physicalCardRevealSize;
	
	[Header("REVEAL TO DECK ANIMATION")]
	[Tooltip("卡牌从reveal zone去牌组底时经过的中间点（弧形轨迹）")]
	public Transform showPos;
	[Tooltip("弧形轨迹动画持续时间")]
	public float revealToDeckAnimDuration = 0.5f;
	[Tooltip("弧形轨迹缓动类型")]
	public Ease revealToDeckEase = Ease.InOutQuad;
	
	[Header("DESTROY")]
	[Tooltip("卡片销毁动画的目标位置（墓地位置）")]
	public Transform gravePosition;
	[Tooltip("卡片销毁动画持续时间")]
	public float cardDestroyAnimDuration = 0.3f;
	[Tooltip("卡片销毁时的目标大小")]
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

	#region 职责1：根据逻辑区域更新物理卡片列表

	/// <summary>
	/// 根据 combined deck zone 更新 physicalCardsInDeck 的顺�?
	/// 注意：revealZone 中的卡不加入此列表，它由 physicalCardInRevealZone 单独管理
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
	/// 将卡片从牌组移到揭晓区域
	/// </summary>
	public void MovePhysicalCardToRevealZone(GameObject physicalCard)
	{
		// 从牌组移�?
		physicalCardsInDeck.Remove(physicalCard);

		// 存储到揭晓区�?
		physicalCardInRevealZone = physicalCard;

		// 设置揭晓位置
		var physScript = physicalCard.GetComponent<CardPhysObjScript>();
		if (physScript != null)
		{
			physScript.SetTargetPosition(physicalCardRevealPos.position);
			physScript.SetTargetScale(physicalCardRevealSize);
		}

		// 更新牌组中剩余卡片的位置
		UpdateAllPhysicalCardTargets();
	}

	/// <summary>
	/// 将揭晓区域的卡片移回牌组底部
	/// 使用弧形轨迹经过 showPos
	/// </summary>
	/// <param name="card">逻辑卡片 GameObject</param>
	/// <param name="onComplete">动画完成回调（可选）</param>
	public void MoveRevealedCardToBottom(GameObject card, Action onComplete = null)
	{
		GameObject physicalCard;

		// 判断输入是物理卡片还是逻辑卡片
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

		// 清空揭晓区域引用
		if (physicalCardInRevealZone == physicalCard)
		{
			physicalCardInRevealZone = null;
		}

		// 添加到牌组底部（index 0）
		physicalCardsInDeck.Insert(0, physicalCard);

		// 如果有配置 showPos，使用弧形轨迹动画
		if (showPos != null)
		{
			PlayArcAnimationToDeckBottom(physicalCard, onComplete);
		}
		else
		{
			// 没有配置 showPos，使用普通动画
			UpdateAllPhysicalCardTargets();
			onComplete?.Invoke();
		}
	}

	/// <summary>
	/// 播放弧形轨迹动画：从当前位置 -> showPos -> 牌组底部
	/// </summary>
	/// <param name="physicalCard">物理卡片 GameObject</param>
	/// <param name="onComplete">动画完成回调（可选）</param>
	private void PlayArcAnimationToDeckBottom(GameObject physicalCard, Action onComplete = null)
	{
		var physScript = physicalCard.GetComponent<CardPhysObjScript>();
		if (physScript == null) 
		{
			onComplete?.Invoke();
			return;
		}

		// 计算牌组底部的最终位置
		int deckIndex = 0; // 插入到底部，index 为 0
		var count = physicalCardsInDeck.Count;
		Vector3 finalTarget = new(
			physicalCardDeckPos.position.x + xOffset * (count - 1 - deckIndex),
			physicalCardDeckPos.position.y + yOffset * (count - 1 - deckIndex),
			physicalCardDeckPos.position.z - zOffset * deckIndex
		);

		// 标记正在播放特殊动画，阻止常规 SetTargetPosition 动画
		physScript.isPlayingSpecialAnimation = true;

		// 创建弧形轨迹动画序列
		Sequence arcSequence = DOTween.Sequence();

		// 阶段1：从当前位置移动到 showPos
		arcSequence.Append(
			physicalCard.transform.DOMove(showPos.position, revealToDeckAnimDuration * 0.5f)
				.SetEase(revealToDeckEase)
		);

		// 阶段2：从 showPos 移动到最终目标位置
		arcSequence.Append(
			physicalCard.transform.DOMove(finalTarget, revealToDeckAnimDuration * 0.5f)
				.SetEase(revealToDeckEase)
		);

		// 同步缩放动画：从 reveal size 缩放到 deck size
		arcSequence.Join(
			physicalCard.transform.DOScale(physicalCardDeckSize, revealToDeckAnimDuration)
				.SetEase(revealToDeckEase)
		);

		// 动画完成后恢复常规动画控制
		arcSequence.OnComplete(() =>
		{
			physScript.isPlayingSpecialAnimation = false;
			// 同步 TargetPosition 和 TargetScale，防止跳变
			physScript.SetTargetPosition(finalTarget);
			physScript.SetTargetScale(physicalCardDeckSize);
			// 更新其他卡片的位置
			UpdateAllPhysicalCardTargets();
			// 调用完成回调
			onComplete?.Invoke();
		});

		arcSequence.Play();
	}

	#endregion

	#region 通用卡片移动动画系统

	/// <summary>
	/// 通用卡片移动方法 - 根据配置移动卡片
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

		// 计算目标位置
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

		// 确定弧形中间点
		Transform arcPoint = config.arcMidpoint ?? showPos;
		bool shouldUseArc = config.useArc && arcPoint != null && config.moveType != CardMoveType.ToGrave;

		// 回调：动画开始
		config.onStart?.Invoke();

		// 标记正在播放特殊动画
		physScript.isPlayingSpecialAnimation = true;

		// 创建动画序列
		Sequence moveSequence = DOTween.Sequence();

		if (shouldUseArc)
		{
			// 弧形轨迹：当前位置 -> 中间点 -> 目标位置
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
			// 直线轨迹
			moveSequence.Append(
				physicalCard.transform.DOMove(targetPosition, config.duration).SetEase(config.ease)
			);
		}

		// 缩放动画：根据目标类型决定最终大小
		Vector3 targetScale = config.moveType == CardMoveType.ToGrave 
			? cardDestroyTargetSize 
			: physicalCardDeckSize;
		moveSequence.Join(
			physicalCard.transform.DOScale(targetScale, config.duration).SetEase(config.ease)
		);

		// 动画完成回调
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
		});

		moveSequence.Play();
	}

	/// <summary>
	/// 批量移动多张卡片（用于 Stage/Bury 等操作）
	/// </summary>
	/// <param name="logicalCards">逻辑卡片列表</param>
	/// <param name="config">移动配置</param>
	/// <param name="onAllComplete">所有动画完成后的回调</param>
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

		// 为每张卡片创建配置副本（因为回调不同）
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
	/// 移动卡片到牌组顶部
	/// </summary>
	public void MoveCardToTop(GameObject logicalCard, float duration = 0.5f, bool useArc = true, Action onComplete = null)
	{
		MoveCardWithAnimation(logicalCard, CardMoveConfig.ToTop(duration, useArc, onComplete));
	}

	/// <summary>
	/// 移动卡片到牌组底部
	/// </summary>
	public void MoveCardToBottom(GameObject logicalCard, float duration = 0.5f, bool useArc = true, Action onComplete = null)
	{
		MoveCardWithAnimation(logicalCard, CardMoveConfig.ToBottom(duration, useArc, onComplete));
	}

	/// <summary>
	/// 移动卡片到指定索引位置
	/// </summary>
	public void MoveCardToIndex(GameObject logicalCard, int index, float duration = 0.5f, bool useArc = true, Action onComplete = null)
	{
		MoveCardWithAnimation(logicalCard, CardMoveConfig.ToIndex(index, duration, useArc, onComplete));
	}

	/// <summary>
	/// 移动卡片到指定世界坐标
	/// </summary>
	public void MoveCardToPosition(GameObject logicalCard, Vector3 position, float duration = 0.5f, bool useArc = true, Action onComplete = null)
	{
		MoveCardWithAnimation(logicalCard, CardMoveConfig.ToPosition(position, duration, useArc, onComplete));
	}

	/// <summary>
	/// 移动卡片到墓地（销毁位置）
	/// </summary>
	public void MoveCardToGrave(GameObject logicalCard, float duration = 0.3f, Action onComplete = null)
	{
		MoveCardWithAnimation(logicalCard, CardMoveConfig.ToGrave(duration, onComplete));
	}

	/// <summary>
	/// 计算指定索引位置的坐标
	/// </summary>
	private Vector3 CalculatePositionAtIndex(int index)
	{
		var count = combatManager.combinedDeckZone.Count;
		// index=0（底部）偏移最大，index=count-1（顶部）偏移最小
		return new Vector3(
			physicalCardDeckPos.position.x + xOffset * (count - 1 - index),
			physicalCardDeckPos.position.y + yOffset * (count - 1 - index),
			physicalCardDeckPos.position.z - zOffset * index
		);
	}

	/// <summary>
	/// 播放 Start Card 退场动画并执行后续操作
	/// 解决 Start Card 动画与 Shuffle 冲突的问题
	/// </summary>
	/// <param name="logicalCard">Start Card 逻辑卡片</param>
	/// <param name="onAnimationComplete">动画完成后的回调（通常传入 Shuffle 逻辑）</param>
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

		// 从牌组列表中移除（不再参与位置同步）
		physicalCardsInDeck.Remove(physicalCard);
		if (physicalCardInRevealZone == physicalCard)
		{
			physicalCardInRevealZone = null;
		}

		// 确定目标位置
		Vector3 targetPos = gravePosition != null 
			? gravePosition.position 
			: physicalCardNewTempCardPos.position;
		Vector3 targetSize = cardDestroyTargetSize;

		var physScript = physicalCard.GetComponent<CardPhysObjScript>();
		if (physScript != null)
		{
			physScript.isPlayingSpecialAnimation = true;
		}

		// 创建退场动画
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
	/// 同时播放 Start Card 退场动画和其他卡片的 Shuffle 动画
	/// Start Card 直接去墓地，其他卡片进行 Shuffle
	/// </summary>
	/// <param name="startCard">Start Card 逻辑卡片</param>
	/// <param name="otherCards">其他卡片的逻辑列表（未 Shuffle）</param>
	/// <param name="onComplete">所有动画完成后的回调</param>
	public void PlayStartCardExitWithShuffleAnimation(GameObject startCard, List<GameObject> otherCards, Action onComplete)
	{
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

		// 1. 先同步其他卡片的物理列表（移除 Start Card 后）
		SyncPhysicalCardsWithCombinedDeck();

		// 2. 计算其他卡片 Shuffle 后的位置
		var shuffledCards = UtilityFuncManagerScript.ShuffleList(new List<GameObject>(otherCards));
		var shuffleTargets = CalculateShuffleTargets(shuffledCards);

		// 3. 计算 Start Card 的目标位置（墓地）
		Vector3 startCardTarget = gravePosition != null 
			? gravePosition.position 
			: physicalCardNewTempCardPos.position;

		// 4. 同时播放两个动画
		int completedAnimations = 0;
		int totalAnimations = 1 + (startPhysicalCard != null ? 1 : 0); // Shuffle + Start Card

		Action onOneComplete = () =>
		{
			completedAnimations++;
			if (completedAnimations >= totalAnimations)
			{
				onComplete?.Invoke();
			}
		};

		// 播放其他卡片的 Shuffle 动画
		PlayShuffleAnimationInternal(shuffleTargets, onOneComplete);

		// 播放 Start Card 退场动画
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
	/// 同时播放 Start Card 移动到随机位置和其他卡片的 Shuffle 动画
	/// 【方案B】逻辑洗牌已完成，基于已知的洗牌结果播放动画
	/// </summary>
	/// <param name="startCard">Start Card 逻辑卡片</param>
	/// <param name="shuffledCards">已经洗好的牌组列表（包含 Start Card，顺序已确定）</param>
	/// <param name="onComplete">所有动画完成后的回调</param>
	public void PlayStartCardShuffleAnimation(GameObject startCard, List<GameObject> shuffledCards, Action onComplete)
	{
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
			// 3. 动画完成后，重建物理卡片列表以匹配逻辑顺序
			RebuildPhysicalDeckFromShuffledList(shuffledCards);
			onComplete?.Invoke();
		});
	}

	/// <summary>
	/// 根据洗牌后的逻辑列表重建物理卡片列表
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
	/// 播放普通 Shuffle 动画（不包含 Start Card 特殊处理）
	/// </summary>
	/// <param name="cards">卡片逻辑列表（未 Shuffle）</param>
	/// <param name="onComplete">动画完成后的回调</param>
	public void PlayShuffleAnimation(List<GameObject> cards, Action onComplete)
	{
		// 先同步物理列表
		SyncPhysicalCardsWithCombinedDeck();

		// 计算 Shuffle 后的位置
		var shuffledCards = UtilityFuncManagerScript.ShuffleList(new List<GameObject>(cards));
		var shuffleTargets = CalculateShuffleTargets(shuffledCards);

		// 播放动画
		PlayShuffleAnimationInternal(shuffleTargets, onComplete);
	}

	/// <summary>
	/// 计算 Shuffle 后每张卡片的目标位置
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

			// i=0（底部）偏移最大，i=count-1（顶部）偏移最小
			Vector3 targetPos = new(
				physicalCardDeckPos.position.x + xOffset * (count - 1 - i),
				physicalCardDeckPos.position.y + yOffset * (count - 1 - i),
				physicalCardDeckPos.position.z - zOffset * i
			);

			targets[physicalCard] = targetPos;
		}

		return targets;
	}

	/// <summary>
	/// 内部方法：播放 Shuffle 移动动画
	/// </summary>
	/// <param name="shuffleTargets">每张物理卡片的目标位置</param>
	/// <param name="onComplete">动画完成后的回调</param>
	private void PlayShuffleAnimationInternal(Dictionary<GameObject, Vector3> shuffleTargets, Action onComplete)
	{
		if (shuffleTargets.Count == 0)
		{
			onComplete?.Invoke();
			return;
		}

		int completedCount = 0;
		int totalCount = shuffleTargets.Count;
		float shuffleDuration = 0.5f; // Shuffle 动画持续时间

		// 为每张卡片生成随机延迟时间
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
				// 使用弧形轨迹经过 showPos
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

	#region 职责1扩展：卡片复位与同步

	/// <summary>
	/// 将所有卡片复位（用于新回合开始）
	/// </summary>
	public void ReviveAllPhysicalCards()
	{
		// 如果有卡还在揭晓区域，先移回牌组底部（index 0）
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
	/// 根据逻辑卡片获取物理卡片
	/// </summary>
	public GameObject GetPhysicalCardFromLogicalCard(CardScript logicalCard)
	{
		if (_cardScriptToPhysicalCache.TryGetValue(logicalCard, out var physicalCard))
			return physicalCard;
		return null;
	}

	#endregion

	#region 职责3：根据列表顺序告�?Physical Card 目标位置

	/// <summary>
	/// 根据 physicalCardsInDeck 的顺序，更新所有卡片的目标位置
	/// </summary>
	public void UpdateAllPhysicalCardTargets()
	{
		// 更新牌组中的卡片位置
		for (int i = 0; i < physicalCardsInDeck.Count; i++)
		{
			var card = physicalCardsInDeck[i];
			var physScript = card.GetComponent<CardPhysObjScript>();
			if (physScript == null) continue;

			// 计算目标位置
			// i=0（顶部）偏移最大，i=count-1（底部）偏移最小
			var count = physicalCardsInDeck.Count;
			Vector3 targetPos = new(
			    physicalCardDeckPos.position.x + xOffset * (count - i),
			    physicalCardDeckPos.position.y + yOffset * (count - i),
			    physicalCardDeckPos.position.z - zOffset * i
			);

			// 设置目标位置和缩放（卡片自己�?Update 中处理动画）
			physScript.SetTargetPosition(targetPos);
			physScript.SetTargetScale(physicalCardDeckSize);
		}
	}

	/// <summary>
	/// 立即重置所有卡片位置（无动画）
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

	#region 清理

	/// <summary>
	/// 销毁所有物理卡片并清空列表
	/// </summary>
	public void ClearAllPhysicalCards()
	{
		// 停止所有可能正在播放的特殊动画
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

		// 销毁揭晓区域的物理卡片
		if (physicalCardInRevealZone != null)
		{
			Destroy(physicalCardInRevealZone);
			physicalCardInRevealZone = null;
		}

		// 清空字典缓存
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
	/// <param name="onComplete">动画完成回调</param>
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

		// 创建退场动画
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

		// 动画完成后销毁
		destroySequence.OnComplete(() =>
		{
			Destroy(physicalCard);
			Destroy(logicalCard);
			onComplete?.Invoke();
		});
	}

	/// <summary>
	/// 播放 Start Card 退场动画：移动到 newCardPos 并缩小，完成后执行回调
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

		// 从牌组列表中移除（不再参与位置同步）
		physicalCardsInDeck.Remove(physicalCard);

		// 停止该卡牌上可能正在进行的动画
		physScript.SetPositionImmediate(physicalCard.transform.position);
		physScript.SetScaleImmediate(physicalCard.transform.localScale);

		// 创建退场动画序列
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

		// 动画完成后执行回调
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
	
	#region Stage/Bury 动画（已改用通用弧形轨迹动画）
	
	/// <summary>
	/// Stage/Bury 动画 - 已改用通用弧形轨迹动画
	/// 请直接使用 MoveCardToTop / MoveCardToBottom 方法
	/// </summary>
	[Obsolete("请直接使用 MoveCardToTop 或 MoveCardToBottom 方法")]
	public void PlayStageBuryAnimation(List<GameObject> affectedCards, bool isStage)
	{
		if (affectedCards == null || affectedCards.Count == 0) return;

		// 直接调用通用弧形轨迹动画
		foreach (var card in affectedCards)
		{
			if (isStage)
				MoveCardToTop(card, duration: 0.5f, useArc: true);
			else
				MoveCardToBottom(card, duration: 0.5f, useArc: true);
		}
	}
	
	/// <summary>
	/// Stage/Bury 动画 - 已改用通用弧形轨迹动画（兼容旧版调用）
	/// </summary>
	[Obsolete("请直接使用 MoveCardToTop 或 MoveCardToBottom 方法")]
	public void PlayStageBuryAnimation(GameObject affectedCard, bool isStage)
	{
		if (affectedCard == null) return;
		
		if (isStage)
			MoveCardToTop(affectedCard, duration: 0.5f, useArc: true);
		else
			MoveCardToBottom(affectedCard, duration: 0.5f, useArc: true);
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

	#region 初始�?

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
}
