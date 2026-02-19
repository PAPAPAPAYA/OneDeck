using System.Collections.Generic;
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
	public GameObject startCardPrefab;
	public GameObject physicalCardPrefab;
	public Transform physicalCardDeckPos;
	public Vector3 physicalCardDeckSize;


	[Header("GRAVE")]
	public Transform physicalCardGravePos;
	public Vector3 physicalCardGraveSize;

	// 物理卡片列表（根据 combined deck zone 和 grave zone 更新）
	public List<GameObject> physicalCardsInDeck = new();
	public List<GameObject> physicalCardsInGrave = new();

	// CardScript 到 Physical Card 的字典（维护这个映射）
	private Dictionary<CardScript, GameObject> _cardScriptToPhysicalCache = new();

	private void OnEnable()
	{
		if (combatManager == null)
			combatManager = CombatManager.Me;
	}

	#region 职责1：根据逻辑区域更新物理卡片列表

	/// <summary>
	/// 根据 combined deck zone 更新 physicalCardsInDeck 的顺序
	/// </summary>
	public void SyncPhysicalCardsWithCombinedDeck()
	{
		if (physicalCardsInDeck.Count == 0) return;

		// 找到 StartCard（没有 CardPhysObjScript 或 cardImRepresenting 为 null）
		GameObject startCard = null;
		List<GameObject> actualCards = new();
		foreach (var physicalCard in physicalCardsInDeck)
		{
			var physCardScript = physicalCard.GetComponent<CardPhysObjScript>();
			if (physCardScript == null || physCardScript.cardImRepresenting == null)
			{
				startCard = physicalCard;
			}
			else
			{
				actualCards.Add(physicalCard);
			}
		}

		// 重建字典
		BuildCardScriptToPhysicalDictionary();

		// 根据 combinedDeckZone 重新排序 physicalCardsInDeck
		physicalCardsInDeck.Clear();
		foreach (var logicalCard in combatManager.combinedDeckZone)
		{
			var cardScript = logicalCard.GetComponent<CardScript>();
			if (_cardScriptToPhysicalCache.TryGetValue(cardScript, out var physicalCard))
			{
				physicalCardsInDeck.Add(physicalCard);
			}
		}

		// 如果有 revealZone 中的卡，放到最后（但在 startCard 之前）
		if (combatManager.revealZone != null)
		{
			var revealedCardScript = combatManager.revealZone.GetComponent<CardScript>();
			if (revealedCardScript != null && 
			    _cardScriptToPhysicalCache.TryGetValue(revealedCardScript, out var revealedPhysicalCard))
			{
				physicalCardsInDeck.Add(revealedPhysicalCard);
			}
		}

		// 如果有 StartCard，放到最后
		if (startCard != null)
		{
			physicalCardsInDeck.Add(startCard);
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

		// 更新墓地位置基准（只更新 z 轴）
		float baseZ = physicalCardsInGrave.Count > 1
		    ? physicalCardsInGrave[0].transform.position.z
		    : physicalCard.transform.position.z;
		physicalCardGravePos.position = new Vector3(
		    physicalCardGravePos.position.x,
		    physicalCardGravePos.position.y,
		    baseZ - physicalCardsInGrave.Count * zOffset
		);

		UpdateAllPhysicalCardTargets();
	}


	/// <summary>
	/// 将所有卡片从墓地移回牌组
	/// </summary>
	public void ReviveAllPhysicalCards()
	{
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
		// Debug: 打印字典内容
		Debug.Log($"=== CardScriptToPhysical Dictionary (Total: {_cardScriptToPhysicalCache.Count} entries) ===");
		int index = 0;
		foreach (var kvp in _cardScriptToPhysicalCache)
		{
			string keyName = kvp.Key != null ? kvp.Key.name : "NULL_KEY";
			string valueName = kvp.Value != null ? kvp.Value.name : "NULL_VALUE";
			Debug.Log($"[{index}] {keyName} -> {valueName}");
			index++;
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
		// 销毁牌组中的物理卡片
		foreach (var physicalCard in physicalCardsInDeck)
		{
			if (physicalCard != null)
			{
				Destroy(physicalCard);
			}
		}
		physicalCardsInDeck.Clear();

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
	/// 实例化所有物理卡片
	/// </summary>
	public void InstantiateAllPhysicalCards()
	{
		if (physicalCardsInDeck.Count > 0) return;

		foreach (var card in combatManager.combinedDeckZone)
		{
			CardScript cardScript = card.GetComponent<CardScript>();
			GameObject newPhysicalCard = Instantiate(physicalCardPrefab);
			CardPhysObjScript physScript = newPhysicalCard.GetComponent<CardPhysObjScript>();

			physScript.cardImRepresenting = cardScript;
			newPhysicalCard.name = card.name + "'s physical card";
			physScript.cardNamePrint.text = card.name;
			physScript.cardDescPrint.text = cardScript.cardDesc;

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

		CreateStartCard();
	}

	private void CreateStartCard()
	{
		var startCard = Instantiate(startCardPrefab);
		var physScript = startCard.GetComponent<CardPhysObjScript>();

		// Start Card 没有对应的逻辑卡片
		physScript.cardImRepresenting = null;
		startCard.name = "Start Card";

		// 设置初始位置和缩放（通过 CardPhysObjScript 以支持动画）
		Vector3 pos = new(
		    physicalCardDeckPos.position.x,
		    physicalCardDeckPos.position.y,
		    physicalCardDeckPos.position.z - zOffset * physicalCardsInDeck.Count
		);
		physScript.SetPositionImmediate(pos);
		physScript.SetScaleImmediate(physicalCardDeckSize);

		physicalCardsInDeck.Add(startCard);
	}

	#endregion
}
