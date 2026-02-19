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
	[Header("NEW CARD")]
	public Transform physicalCardNewTempCardPos;
	public Vector3 physicalCardNewTempCardSize;

	[Header("DECK")]
	public GameObject physicalCardPrefab;
	public GameObject startCardPhysicalPrefab; // Start Card 的物理预制体（外观不同）
	public Transform physicalCardDeckPos;
	public Vector3 physicalCardDeckSize;


	[Header("GRAVE")]
	public Transform physicalCardGravePos;
	public Vector3 physicalCardGraveSize;
	private Vector3 _physicalCardGravePosOriginal; // 保存 grave zone 的原始 x,y 位置

	[Header("REVEAL")]
	public Transform physicalCardRevealPos;
	public Vector3 physicalCardRevealSize;

	// 物理卡片列表（根据 combined deck zone 和 grave zone 更新）
	public List<GameObject> physicalCardsInDeck = new();
	public List<GameObject> physicalCardsInGrave = new();
	
	// 揭晓区域的物理卡片（单独存储，防止与 deck/grave 混淆）
	public GameObject physicalCardInRevealZone;

	// CardScript 到 Physical Card 的字典（维护这个映射）
	private Dictionary<CardScript, GameObject> _cardScriptToPhysicalCache = new();

	private void OnEnable()
	{
		if (combatManager == null)
			combatManager = CombatManager.Me;
		
		// 保存 grave zone 的原始位置（只取 x, y）
		if (physicalCardGravePos != null)
			_physicalCardGravePosOriginal = physicalCardGravePos.position;
	}

	#region 职责1：根据逻辑区域更新物理卡片列表

	/// <summary>
	/// 根据 combined deck zone 更新 physicalCardsInDeck 的顺序
	/// </summary>
	public void SyncPhysicalCardsWithCombinedDeck()
	{
		if (physicalCardsInDeck.Count == 0) return;

		// 重建字典
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

		// 如果有 revealZone 中的卡，放到最后
		if (combatManager.revealZone != null)
		{
			var revealedCardScript = combatManager.revealZone.GetComponent<CardScript>();
			if (revealedCardScript != null && 
			    _cardScriptToPhysicalCache.TryGetValue(revealedCardScript, out var revealedPhysicalCard))
			{
				physicalCardsInDeck.Add(revealedPhysicalCard);
			}
		}
	}

	/// <summary>
	/// 将卡片从墓地移回牌组
	/// </summary>
	public void MovePhysicalCardFromGraveToDeck(GameObject card)
	{
		GameObject physicalCard;

		// 判断输入是物理卡片还是逻辑卡片
		var cardScript = card.GetComponent<CardScript>();
		if (cardScript == null)
		{
			physicalCard = card;
		}
		else
		{
			BuildCardScriptToPhysicalDictionary();
			physicalCard = GetPhysicalCardFromLogicalCard(cardScript);
			if (physicalCard == null)
			{
				Debug.LogWarning($"MoveCardFromGraveToDeck: Could not find physical card for {card.name}");
				return;
			}
		}

		physicalCardsInDeck.Insert(0, physicalCard);
		physicalCardsInGrave.Remove(physicalCard);
		UpdateAllPhysicalCardTargets();
	}

	/// <summary>
	/// 将卡片从牌组移到揭晓区域
	/// </summary>
	public void MovePhysicalCardToRevealZone(GameObject physicalCard)
	{
		// 从牌组移除
		physicalCardsInDeck.Remove(physicalCard);

		// 存储到揭晓区域
		physicalCardInRevealZone = physicalCard;

		// 设置揭晓位置
		var physScript = physicalCard.GetComponent<CardPhysObjScript>();
		if (physScript != null)
		{
			physScript.SetTargetPosition(physicalCardRevealPos.position);
			physScript.SetTargetScale(physicalCardRevealSize);
		}
	}

	/// <summary>
	/// 将揭晓区域的卡片移到墓地（支持逻辑卡或物理卡输入）
	/// </summary>
	public void MoveRevealedCardToGrave(GameObject card)
	{
		GameObject physicalCard;

		// 判断输入是物理卡片还是逻辑卡片
		var cardScript = card.GetComponent<CardScript>();
		if (cardScript == null)
		{
			Debug.LogWarning($"MoveRevealedCardToGrave: Card {card.name} has no CardScript");
			return;
		}

		// 通过逻辑卡找物理卡
		BuildCardScriptToPhysicalDictionary();
		physicalCard = GetPhysicalCardFromLogicalCard(cardScript);
		if (physicalCard == null)
		{
			Debug.LogWarning($"MoveRevealedCardToGrave: Could not find physical card for {card.name}");
			return;
		}

		// 清空揭晓区域引用
		if (physicalCardInRevealZone == physicalCard)
		{
			physicalCardInRevealZone = null;
		}

		// 添加到墓地
		physicalCardsInGrave.Add(physicalCard);

		// 更新墓地位置基准（只更新 z 轴，x,y 保持原始配置）
		float baseZ = _physicalCardGravePosOriginal.z;
		physicalCardGravePos.position = new Vector3(
			_physicalCardGravePosOriginal.x,
			_physicalCardGravePosOriginal.y,
			baseZ - (physicalCardsInGrave.Count - 1) * zOffset
		);

		// 更新目标位置
		UpdateAllPhysicalCardTargets();
	}

	/// <summary>
	/// 将卡片从牌组移到墓地（用于 exile 效果）
	/// </summary>
	public void MovePhysicalCardFromDeckToGrave(GameObject card)
	{
		GameObject physicalCard;

		// 判断输入是物理卡片还是逻辑卡片
		var cardScript = card.GetComponent<CardScript>();
		if (cardScript == null)
		{
			physicalCard = card;
		}
		else
		{
			BuildCardScriptToPhysicalDictionary();
			physicalCard = GetPhysicalCardFromLogicalCard(cardScript);
			if (physicalCard == null)
			{
				Debug.LogWarning($"MoveCardFromDeckToGrave: Could not find physical card for {card.name}");
				return;
			}
		}

		// 从牌组移除，添加到墓地
		physicalCardsInDeck.Remove(physicalCard);
		physicalCardsInGrave.Add(physicalCard);

		// 更新墓地位置基准（只更新 z 轴，x,y 保持原始配置）
		float baseZ = _physicalCardGravePosOriginal.z;
		physicalCardGravePos.position = new Vector3(
		    _physicalCardGravePosOriginal.x,
		    _physicalCardGravePosOriginal.y,
		    baseZ - (physicalCardsInGrave.Count - 1) * zOffset
		);

		UpdateAllPhysicalCardTargets();
	}


	/// <summary>
	/// 将所有卡片从墓地移回牌组
	/// </summary>
	public void ReviveAllPhysicalCards()
	{
		// 如果有卡还在揭晓区域，先移回牌组
		if (physicalCardInRevealZone != null)
		{
			physicalCardsInDeck.Add(physicalCardInRevealZone);
			physicalCardInRevealZone = null;
		}

		if (physicalCardsInGrave.Count <= 0) return;

		physicalCardsInDeck.AddRange(physicalCardsInGrave);
		physicalCardsInGrave.Clear();

		// 只更新目标位置，不排序（排序由 Shuffle 时的 SyncPhysicalCardsWithCombinedDeck 处理）
		UpdateAllPhysicalCardTargets();
	}

	#endregion

	#region 职责2：维护 CardScript 到 Physical Card 的字典

	/// <summary>
	/// 从牌组和墓地构建 CardScript -> Physical Card 映射
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

		foreach (var physicalCard in physicalCardsInGrave)
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

	#region 职责3：根据列表顺序告诉 Physical Card 目标位置

	/// <summary>
	/// 根据 physicalCardsInDeck 和 physicalCardsInGrave 的顺序，更新所有卡片的目标位置
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
			Vector3 targetPos = new(
			    physicalCardDeckPos.position.x,
			    physicalCardDeckPos.position.y,
			    physicalCardDeckPos.position.z - zOffset * i
			);

			// 设置目标位置和缩放（卡片自己在 Update 中处理动画）
			physScript.SetTargetPosition(targetPos);
			physScript.SetTargetScale(physicalCardDeckSize);
		}

		// 更新墓地中的卡片位置
		for (int i = 0; i < physicalCardsInGrave.Count; i++)
		{
			var card = physicalCardsInGrave[i];
			var physScript = card.GetComponent<CardPhysObjScript>();
			if (physScript == null) continue;

			Vector3 targetPos = new(
			    physicalCardGravePos.position.x,
			    physicalCardGravePos.position.y,
			    physicalCardGravePos.position.z - zOffset * i
			);

			physScript.SetTargetPosition(targetPos);
			physScript.SetTargetScale(physicalCardGraveSize);
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
			    physicalCardDeckPos.position.x,
			    physicalCardDeckPos.position.y,
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
		
		// 销毁牌组中的物理卡片
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

		// 销毁墓地中的物理卡片
		foreach (var physicalCard in physicalCardsInGrave)
		{
			if (physicalCard != null)
			{
				Destroy(physicalCard);
			}
		}
		physicalCardsInGrave.Clear();

		// 清空字典缓存
		_cardScriptToPhysicalCache.Clear();
	}
	
	/// <summary>
	/// 停止所有物理卡片的特殊动画
	/// </summary>
	public void StopAllSpecialAnimations()
	{
		// 停止所有属于本对象的 DOTween 延迟调用
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
		
		foreach (var physicalCard in physicalCardsInGrave)
		{
			if (physicalCard != null)
			{
				var physScript = physicalCard.GetComponent<CardPhysObjScript>();
				physScript?.StopSpecialAnimation();
			}
		}
	}

	#endregion
	
	#region Stage/Bury 特殊动画（协调主角卡片和卡组整体动画）
	
	/// <summary>
	/// 为 Stage/Bury 操作播放特殊动画（包含卡组整体呼吸效果）
	/// 动画流程：
	/// 1. 卡组其他卡片：缩小 + 右移
	/// 2. 主角卡片：左移 + 放大 + 旋转
	/// 3. 停顿
	/// 4. 同时恢复：主角卡片插入到目标位置，卡组其他卡片恢复
	/// </summary>
	/// <param name="affectedCards">受影响的逻辑卡片列表（主角卡片）</param>
	/// <param name="isStage">true=置顶, false=置底</param>
	public void PlayStageBuryAnimation(List<GameObject> affectedCards, bool isStage)
	{
		if (affectedCards == null || affectedCards.Count == 0) return;
		
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
		
		// 收集卡组中的其他卡片（非主角卡片）
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
		
		// 包含 reveal zone 中的卡片（如果存在且不是主角卡片）
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
				
				// 如果不是主角卡片，也添加到卡组动画列表
				if (!isMainCard)
				{
					otherCardScripts.Add(revealPhysScript);
					otherCardBasePositions.Add(revealPhysScript.transform.position);
				}
			}
		}
		
		// ========== 阶段 1：卡组缩小 + 主角卡片左移（同时进行）==========
		
		// 卡组其他卡片：缩小 + 右移
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
				       // 从后往前计算延迟：最后一张0秒，倒数第二张0.1秒，以此类推
			float delay = i * staggerDelay;
			//float delay = (lastIndex - i) * staggerDelay;
			DOVirtual.DelayedCall(delay, () => {
				mainCardScripts[index].PlayMainCardPhase1();
			}).SetId(this);
		}
		
		// 计算阶段1持续时间
		// 卡组动画时间和主角卡片动画时间的最大值
		// 由于最后一张最先开始，最后一张的动画完成时间就是整体完成时间
		float deckAnimDuration = otherCardScripts.Count > 0 ? otherCardScripts[0].deckAnimDuration : 0f;
		float mainCardPhase1Duration = mainCardScripts[0].sideMoveDuration; // 最后一张0延迟开始
		float phase1Duration = Mathf.Max(deckAnimDuration, mainCardPhase1Duration);
		
		// ========== 阶段 2：停顿 + 阶段3：同时恢复 ==========
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
			
			// 卡组其他卡片：恢复
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
	/// 为 Stage/Bury 操作播放特殊动画（兼容旧版调用）
	/// </summary>
	/// <param name="affectedCard">单张受影响的逻辑卡片</param>
	/// <param name="isStage">true=置顶, false=置底</param>
	public void PlayStageBuryAnimation(GameObject affectedCard, bool isStage)
	{
		if (affectedCard == null) return;
		PlayStageBuryAnimation(new List<GameObject> { affectedCard }, isStage);
	}
	
	/// <summary>
	/// 计算卡片在 Stage/Bury 后的目标位置
	/// </summary>
	private Vector3 CalculateCardTargetPosition(GameObject logicalCard, bool isStage)
	{
		// 找到卡片在 combinedDeckZone 中的索引
		int targetIndex = combatManager.combinedDeckZone.IndexOf(logicalCard);
		
		// 如果找不到，根据 isStage 确定默认位置
		if (targetIndex < 0)
		{
			if (isStage)
			{
				// 置顶：放到 combinedDeckZone 的最后位置
				targetIndex = combatManager.combinedDeckZone.Count - 1;
			}
			else
			{
				// 置底：放到第 0 位
				targetIndex = 0;
			}
		}
		
		return new Vector3(
			physicalCardDeckPos.position.x,
			physicalCardDeckPos.position.y,
			physicalCardDeckPos.position.z - zOffset * targetIndex
		);
	}
	
	#endregion

	/// <summary>
	/// 为逻辑卡片创建对应的物理卡片并插入到 deck 中
	/// </summary>
	public void AddPhysicalCardToDeck(GameObject logicalCard)
	{
		CardScript cardScript = logicalCard.GetComponent<CardScript>();

		// 创建物理卡片
		GameObject newPhysicalCard = Instantiate(physicalCardPrefab);
		CardPhysObjScript physScript = newPhysicalCard.GetComponent<CardPhysObjScript>();

		physScript.cardImRepresenting = cardScript;
		newPhysicalCard.name = logicalCard.name + "'s physical card";
		physScript.cardNamePrint.text = logicalCard.name;
		physScript.cardDescPrint.text = cardScript.cardDesc;

		// 设置初始缩放
		physScript.SetScaleImmediate(physicalCardDeckSize);

		// 插入到物理卡片列表
		physicalCardsInDeck.Insert(0, newPhysicalCard);

		// 设置初始位置 (new card appears at physical card new temp card pos)
		Vector3 startPos = physicalCardNewTempCardPos.position;
		physScript.SetPositionImmediate(startPos);
		// set initial size
		Vector3 startSize = physicalCardNewTempCardSize;
		physScript.SetScaleImmediate(startSize);


		// 更新所有卡片目标位置（触发移动动画）
		UpdateAllPhysicalCardTargets();
	}

	#region 初始化

	/// <summary>
	/// 实例化所有物理卡片（包括 Start Card）
	/// </summary>
	public void InstantiateAllPhysicalCards()
	{
		if (physicalCardsInDeck.Count > 0) return;

		foreach (var card in combatManager.combinedDeckZone)
		{
			CardScript cardScript = card.GetComponent<CardScript>();
			
			// Start Card 使用专门的预制体
			GameObject prefabToUse = (cardScript != null && cardScript.isStartCard) 
				? startCardPhysicalPrefab 
				: physicalCardPrefab;
			
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

			// 立即设置初始位置和缩放
			physScript.SetScaleImmediate(physicalCardDeckSize);

			physicalCardsInDeck.Add(newPhysicalCard);
		}

		// 设置初始位置
		for (int i = 0; i < physicalCardsInDeck.Count; i++)
		{
			var physScript = physicalCardsInDeck[i].GetComponent<CardPhysObjScript>();
			Vector3 pos = new(
			    physicalCardDeckPos.position.x,
			    physicalCardDeckPos.position.y,
			    physicalCardDeckPos.position.z - zOffset * i
			);
			physScript.SetPositionImmediate(pos);
		}

		// 重建字典映射
		BuildCardScriptToPhysicalDictionary();
	}

	#endregion
}
