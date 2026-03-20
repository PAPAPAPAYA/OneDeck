using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Video;

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
	/// </summary>
	public void MoveRevealedCardToBottom(GameObject card)
	{
		GameObject physicalCard;

		// 判断输入是物理卡片还是逻辑卡片
		var cardScript = card.GetComponent<CardScript>();
		if (cardScript == null)
		{
			Debug.LogWarning($"MoveRevealedCardToBottom: Card {card.name} has no CardScript");
			return;
		}

		// 通过逻辑卡找物理�?
		BuildCardScriptToPhysicalDictionary();
		physicalCard = GetPhysicalCardFromLogicalCard(cardScript);
		if (physicalCard == null)
		{
			Debug.LogWarning($"MoveRevealedCardToBottom: Could not find physical card for {card.name}");
			return;
		}

		// 清空揭晓区域引用
		if (physicalCardInRevealZone == physicalCard)
		{
			physicalCardInRevealZone = null;
		}

		// 添加到牌组底部（index 0�?
		physicalCardsInDeck.Insert(0, physicalCard);

		// 更新目标位置
		UpdateAllPhysicalCardTargets();
	}

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
	
	#region Stage/Bury 特殊动画（协调主角卡片和卡组整体动画�?
	
	/// <summary>
	/// �?Stage/Bury 操作播放特殊动画（包含卡组整体呼吸效果）
	/// 动画流程�?
	/// 1. 卡组其他卡片：缩�?+ 右移
	/// 2. 主角卡片：左�?+ 放大 + 旋转
	/// 3. 停顿
	/// 4. 同时恢复：主角卡片插入到目标位置，卡组其他卡片恢�?
	/// </summary>
	/// <param name="affectedCards">受影响的逻辑卡片列表（主角卡片）</param>
	/// <param name="isStage">true=置顶, false=置底</param>
	public void PlayStageBuryAnimation(List<GameObject> affectedCards, bool isStage)
	{
		if (affectedCards == null || affectedCards.Count == 0) return;

		// 如果禁用了动画，直接同步位置
		if (!enableStageBuryAnimation)
		{
			SyncPhysicalCardsWithCombinedDeck();
			UpdateAllPhysicalCardTargets();
			return;
		}
		
		// 屏蔽玩家输入，防止动画期间揭晓下一张卡
		if (combatManager != null)
		{
			combatManager.blockPlayerInput = true;
		}
		
		// 先构建字典（确保能正确映射）
		BuildCardScriptToPhysicalDictionary();
		
		// 收集所有主角卡片的物理对象
		List<CardPhysObjScript> mainCardScripts = new List<CardPhysObjScript>();
		List<Vector3> mainCardFinalTargets = new List<Vector3>();
		
		foreach (var logicalCard in affectedCards)
		{
			if (logicalCard == null) continue;
			
			var cardScript = logicalCard.GetComponent<CardScript>();
			if (cardScript == null) continue;
			
			var physicalCard = GetPhysicalCardFromLogicalCard(cardScript);
			if (physicalCard == null) continue;
			
			var physScript = physicalCard.GetComponent<CardPhysObjScript>();
			if (physScript == null) continue;
			
			mainCardScripts.Add(physScript);
			mainCardFinalTargets.Add(CalculateCardTargetPosition(logicalCard, isStage));
		}
		
		if (mainCardScripts.Count == 0) return;
		
		// 收集卡组中的其他卡片（非主角卡片�?
		List<CardPhysObjScript> otherCardScripts = new List<CardPhysObjScript>();
		List<Vector3> otherCardBasePositions = new List<Vector3>();
		
		foreach (var physicalCard in physicalCardsInDeck)
		{
			if (physicalCard == null) continue;
			
			var physScript = physicalCard.GetComponent<CardPhysObjScript>();
			if (physScript == null) continue;
			
			// 跳过主角卡片
			bool isMainCard = false;
			foreach (var mainScript in mainCardScripts)
			{
				if (mainScript == physScript)
				{
					isMainCard = true;
					break;
				}
			}
			if (isMainCard) continue;
			
			otherCardScripts.Add(physScript);
			otherCardBasePositions.Add(physScript.transform.position);
		}
		
		// 包含 reveal zone 中的卡片（如果存在且不是主角卡片�?
		if (physicalCardInRevealZone != null)
		{
			var revealPhysScript = physicalCardInRevealZone.GetComponent<CardPhysObjScript>();
			if (revealPhysScript != null)
			{
				// 检查是否是主角卡片
				bool isMainCard = false;
				foreach (var mainScript in mainCardScripts)
				{
					if (mainScript == revealPhysScript)
					{
						isMainCard = true;
						break;
					}
				}
				
				// 如果不是主角卡片，也添加到卡组动画列�?
				if (!isMainCard)
				{
					otherCardScripts.Add(revealPhysScript);
					otherCardBasePositions.Add(revealPhysScript.transform.position);
				}
			}
		}
		
		// ========== 阶段 1：卡组缩�?+ 主角卡片左移（同时进行）==========
		
		// 卡组其他卡片：缩�?+ 右移
		for (int i = 0; i < otherCardScripts.Count; i++)
		{
			otherCardScripts[i].PlayDeckGroupShrinkAnimation(otherCardBasePositions[i]);
		}
		
		// 主角卡片：错开左移 + 放大（从最后一张开始）
		float staggerDelay = 0.3f;
		int lastIndex = mainCardScripts.Count - 1;
		for (int i = 0; i < mainCardScripts.Count; i++)
		{
			int index = i; // 捕获索引
				       // 从后往前计算延迟：最后一�?秒，倒数第二�?.1秒，以此类推
			float delay = i * staggerDelay;
			//float delay = (lastIndex - i) * staggerDelay;
			DOVirtual.DelayedCall(delay, () => {
				mainCardScripts[index].PlayMainCardPhase1();
			}).SetId(this);
		}
		
		// 计算阶段1持续时间
		// 卡组动画时间和主角卡片动画时间的最大�?
		// 由于最后一张最先开始，最后一张的动画完成时间就是整体完成时间
		float deckAnimDuration = otherCardScripts.Count > 0 ? otherCardScripts[0].deckAnimDuration : 0f;
		float mainCardPhase1Duration = mainCardScripts[0].sideMoveDuration; // 最后一�?延迟开�?
		float phase1Duration = Mathf.Max(deckAnimDuration, mainCardPhase1Duration);
		
		// ========== 阶段 2：停�?+ 阶段3：同时恢�?==========
		// 使用延迟调用在正确的时间触发恢复
		float phase2Pause = 0.15f;
		float restoreDelay = phase1Duration + phase2Pause;
		
		DOVirtual.DelayedCall(restoreDelay, () => {
			// 主角卡片：插入到目标位置
			for (int i = 0; i < mainCardScripts.Count; i++)
			{
				int index = i;
				mainCardScripts[index].PlayMainCardPhase3(mainCardFinalTargets[index]);
			}
			
			// 卡组其他卡片：恢�?
			foreach (var cardScript in otherCardScripts)
			{
				cardScript.PlayDeckGroupRestoreAnimation();
			}
		}).SetId(this);
		
		// 动画全部完成后的回调（用于同步列表）
		float totalDuration = restoreDelay + mainCardScripts[0].insertDuration;
		DOVirtual.DelayedCall(totalDuration, () => {
			// 恢复玩家输入
			if (combatManager != null)
			{
				combatManager.blockPlayerInput = false;
			}
			
			// 同步物理卡片列表
			SyncPhysicalCardsWithCombinedDeck();
			UpdateAllPhysicalCardTargets();
		}).SetId(this);
	}
	
	/// <summary>
	/// �?Stage/Bury 操作播放特殊动画（兼容旧版调用）
	/// </summary>
	/// <param name="affectedCard">单张受影响的逻辑卡片</param>
	/// <param name="isStage">true=置顶, false=置底</param>
	public void PlayStageBuryAnimation(GameObject affectedCard, bool isStage)
	{
		if (affectedCard == null) return;
		PlayStageBuryAnimation(new List<GameObject> { affectedCard }, isStage);
	}
	
	/// <summary>
	/// 计算卡片�?Stage/Bury 后的目标位置
	/// </summary>
	private Vector3 CalculateCardTargetPosition(GameObject logicalCard, bool isStage)
	{
		// 找到卡片�?combinedDeckZone 中的索引
		int targetIndex = combatManager.combinedDeckZone.IndexOf(logicalCard);
		
		// 如果找不到，根据 isStage 确定默认位置
		if (targetIndex < 0)
		{
			if (isStage)
			{
				// 置顶：放�?combinedDeckZone 的最后位�?
				targetIndex = combatManager.combinedDeckZone.Count - 1;
			}
			else
			{
				// 置底：放到第 0 �?
				targetIndex = 0;
			}
		}
		
		// targetIndex=0（顶部）偏移最大，targetIndex=count-1（底部）偏移最小
			var count = combatManager.combinedDeckZone.Count;
			return new Vector3(
				physicalCardDeckPos.position.x + xOffset * (count - targetIndex),
				physicalCardDeckPos.position.y + yOffset * (count - targetIndex),
				physicalCardDeckPos.position.z - zOffset * targetIndex
			);
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
