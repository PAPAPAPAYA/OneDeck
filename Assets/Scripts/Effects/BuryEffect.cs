using System.Collections.Generic;
using DefaultNamespace.Managers;
using DefaultNamespace.SOScripts;
using UnityEngine;

public class BuryEffect : EffectScript
{
	private List<GameObject> _combinedDeck;

	[Header("Tag Configuration")]
	public EnumStorage.Tag tagToCheck;

	/// <summary>
	/// 获取卡牌所属者的颜色标签（玩家=#87CEEB，敌人=orange）
	/// </summary>
	private string GetCardColorTag(GameObject card)
	{
		var cardStatus = card.GetComponent<CardScript>().myStatusRef;
		return cardStatus == combatManager.ownerPlayerStatusRef ? "#87CEEB" : "orange";
	}

	/// <summary>
	/// 获取当前卡牌的颜色标签
	/// </summary>
	private string GetMyCardColorTag()
	{
		return myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? "#87CEEB" : "orange";
	}

	/// <summary>
	/// 获取卡牌在 combinedDeck 中的索引
	/// </summary>
	private int GetCardIndexInCombinedDeck(GameObject card)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		return _combinedDeck.IndexOf(card);
	}

	/// <summary>
	/// 检查卡牌是否在牌组底部（index = 0）
	/// </summary>
	private bool IsCardAtBottom(GameObject card)
	{
		int index = GetCardIndexInCombinedDeck(card);
		return index == 0;
	}

	public void BurySelf() // put self at the bottom of the deck
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardToBury = transform.parent.gameObject;
		// 如果已经在底部，不需要 bury
		if (IsCardAtBottom(cardToBury)) return;
		var cardsToBury = new List<GameObject> { cardToBury };
		BuryChosenCards(cardsToBury, 1);
	}

	public void BuryCardsWithTag(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsWithTag = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsWithTag, true);

		// Filter cards that have the specified tag and are not at the bottom
		for (int i = cardsWithTag.Count - 1; i >= 0; i--)
		{
			var card = cardsWithTag[i];
			var cardScript = card.GetComponent<CardScript>();
			if (!cardScript.myTags.Contains(tagToCheck) || IsCardAtBottom(card) || cardScript.isMinion || CombatManager.ShouldSkipEffectProcessing(cardScript))
			{
				cardsWithTag.RemoveAt(i);
			}
		}

		cardsWithTag = UtilityFuncManagerScript.ShuffleList(cardsWithTag);
		BuryChosenCards(cardsWithTag, amount);
	}

	public void BuryMyCards(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var myCards = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, myCards, true);

		// Filter cards that belong to this card's owner and are not at the bottom
		for (int i = myCards.Count - 1; i >= 0; i--)
		{
			var card = myCards[i];
			var cardScript = card.GetComponent<CardScript>();
			if (CombatManager.ShouldSkipEffectProcessing(cardScript) || cardScript.myStatusRef != myCardScript.myStatusRef || IsCardAtBottom(card) || cardScript.isMinion)
			{
				myCards.RemoveAt(i);
			}
		}

		myCards = UtilityFuncManagerScript.ShuffleList(myCards);
		BuryChosenCards(myCards, amount);
	}

	public void BuryMyCardsWithTag(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsWithTag = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsWithTag, true);

		// Filter cards that have the specified tag, belong to this card's owner, and are not at the bottom
		for (int i = cardsWithTag.Count - 1; i >= 0; i--)
		{
			var card = cardsWithTag[i];
			var cardScript = card.GetComponent<CardScript>();
			if (!cardScript.myTags.Contains(tagToCheck) || CombatManager.ShouldSkipEffectProcessing(cardScript) || cardScript.myStatusRef != myCardScript.myStatusRef || IsCardAtBottom(card) || cardScript.isMinion)
			{
				cardsWithTag.RemoveAt(i);
			}
		}

		cardsWithTag = UtilityFuncManagerScript.ShuffleList(cardsWithTag);
		BuryChosenCards(cardsWithTag, amount);
	}

	public void BuryTheirCards(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var theirCards = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, theirCards, true);

		// Filter cards that belong to the opponent and are not at the bottom
		for (int i = theirCards.Count - 1; i >= 0; i--)
		{
			var card = theirCards[i];
			var cardScript = card.GetComponent<CardScript>();
			if (CombatManager.ShouldSkipEffectProcessing(cardScript) || cardScript.myStatusRef == myCardScript.myStatusRef || IsCardAtBottom(card) || cardScript.isMinion)
			{
				theirCards.RemoveAt(i);
			}
		}

		theirCards = UtilityFuncManagerScript.ShuffleList(theirCards);
		BuryChosenCards(theirCards, amount);
	}

	public void BuryTheirCardsWithTag(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsWithTag = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsWithTag, true);

		// Filter cards that have the specified tag, belong to the opponent, and are not at the bottom
		for (int i = cardsWithTag.Count - 1; i >= 0; i--)
		{
			var card = cardsWithTag[i];
			var cardScript = card.GetComponent<CardScript>();
			if (!cardScript.myTags.Contains(tagToCheck) || CombatManager.ShouldSkipEffectProcessing(cardScript) || cardScript.myStatusRef == myCardScript.myStatusRef || IsCardAtBottom(card) || cardScript.isMinion)
			{
				cardsWithTag.RemoveAt(i);
			}
		}

		cardsWithTag = UtilityFuncManagerScript.ShuffleList(cardsWithTag);
		BuryChosenCards(cardsWithTag, amount);
	}

	public void BuryTheirCards_BasedOnIntSO(IntSO intSO)
	{
		if (intSO == null) return;
		BuryTheirCards(intSO.value);
	}

	public void BuryMyCards_BasedOnIntSO(IntSO intSO)
	{
		if (intSO == null) return;
		BuryMyCards(intSO.value);
	}

	private void BuryChosenCards(List<GameObject> cardsToBury, int amount)
	{
		amount = Mathf.Clamp(amount, 0, cardsToBury.Count);
		if (amount == 0) return;

		// 1. 先修改逻辑列表，并收集成功移动的卡片
		var buriedCards = new List<GameObject>();
		for (var i = 0; i < amount; i++)
		{
			var targetCard = cardsToBury[i];
			var targetCardScript = targetCard.GetComponent<CardScript>();

			if (_combinedDeck.Contains(targetCard))
			{
				_combinedDeck.Remove(targetCard);
				_combinedDeck.Insert(0, targetCard);  // 插入到底部
				buriedCards.Add(targetCard);
				
				string myColor = GetMyCardColorTag();
				string targetColor = GetCardColorTag(targetCard);
				effectResultString.value += "// [<color=" + myColor + ">" + myCard.gameObject.name + "</color>] buried [<color=" + targetColor + ">" +
					targetCardScript.gameObject.name + "</color>] to the bottom of the deck\n";
			}
		}
		
		// 2. 播放弧形轨迹动画（移动卡片到底部）
		foreach (var card in buriedCards)
		{
			CombatUXManager.me.MoveCardToBottom(card, duration: 0.5f, useArc: true);
		}
		
		// 3. 触发卡被 bury 事件
		foreach (var card in buriedCards)
		{
			// 触发自身被bury事件
			GameEventStorage.me.onMeBuried.RaiseSpecific(card);
			// 触发任意卡被bury事件
			GameEventStorage.me.onAnyCardBuried.Raise();
			// 触发友方卡被bury事件
			var cardStatus = card.GetComponent<CardScript>()?.myStatusRef;
			if (cardStatus != null && GameEventStorage.me.onFriendlyCardBuried != null)
			{
				if (cardStatus == combatManager.ownerPlayerStatusRef)
				{
					GameEventStorage.me.onFriendlyCardBuried.RaiseOwner();
				}
				else
				{
					GameEventStorage.me.onFriendlyCardBuried.RaiseOpponent();
				}
			}
		}
		
		// 4. 同步物理卡片列表并更新所有卡片位置
		if (buriedCards.Count > 0)
		{
			CombatUXManager.me.SyncPhysicalCardsWithCombinedDeck();
			CombatUXManager.me.UpdateAllPhysicalCardTargets();
		}
	}
}
