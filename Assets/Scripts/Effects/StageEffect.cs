using System.Collections.Generic;
using DefaultNamespace.Managers;
using DefaultNamespace.SOScripts;
using UnityEngine;

public class StageEffect : EffectScript
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
	/// 检查卡牌是否在牌组顶部（index = count - 1）
	/// </summary>
	private bool IsCardAtTop(GameObject card)
	{
		int index = GetCardIndexInCombinedDeck(card);
		return index >= 0 && index == _combinedDeck.Count - 1;
	}

	public void StageSelf() // put self on top of the deck
	{
		_combinedDeck = combatManager.combinedDeckZone;
		// 如果已经在顶部，不需要 stage
		if (IsCardAtTop(myCard)) return;
		var cardsToStage = new List<GameObject> { myCard };
		StageChosenCards(cardsToStage, 1);
	}

	public void StageCardsWithTag(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsWithTag = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsWithTag, true);

		// Filter cards that have the specified tag and are not at the top
		for (int i = cardsWithTag.Count - 1; i >= 0; i--)
		{
			var card = cardsWithTag[i];
			var cardScript = card.GetComponent<CardScript>();
			if (!cardScript.myTags.Contains(tagToCheck) || IsCardAtTop(card) || cardScript.isMinion || CombatManager.ShouldSkipEffectProcessing(cardScript))
			{
				cardsWithTag.RemoveAt(i);
			}
		}

		cardsWithTag = UtilityFuncManagerScript.ShuffleList(cardsWithTag);
		StageChosenCards(cardsWithTag, amount);
	}

	public void StageMyCards(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var myCards = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, myCards, true);

		// Filter cards that belong to this card's owner and are not at the top
		for (int i = myCards.Count - 1; i >= 0; i--)
		{
			var card = myCards[i];
			var cardScript = card.GetComponent<CardScript>();
			if (CombatManager.ShouldSkipEffectProcessing(cardScript) || cardScript.myStatusRef != myCardScript.myStatusRef || IsCardAtTop(card) || cardScript.isMinion)
			{
				myCards.RemoveAt(i);
			}
		}

		myCards = UtilityFuncManagerScript.ShuffleList(myCards);
		StageChosenCards(myCards, amount);
	}

	public void StageMyTokens(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var myTokens = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, myTokens, true);

		// Filter: 己方 Minion 卡，且不在顶部
		for (int i = myTokens.Count - 1; i >= 0; i--)
		{
			var card = myTokens[i];
			var cardScript = card.GetComponent<CardScript>();
			if (CombatManager.ShouldSkipEffectProcessing(cardScript) || cardScript.myStatusRef != myCardScript.myStatusRef || IsCardAtTop(card) || !cardScript.isMinion)
			{
				myTokens.RemoveAt(i);
			}
		}

		myTokens = UtilityFuncManagerScript.ShuffleList(myTokens);
		StageChosenCards(myTokens, amount);
	}

	public void StageMyCardsWithTag(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsWithTag = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsWithTag, true);

		// Filter cards that have the specified tag, belong to this card's owner, and are not at the top
		for (int i = cardsWithTag.Count - 1; i >= 0; i--)
		{
			var card = cardsWithTag[i];
			var cardScript = card.GetComponent<CardScript>();
			if (!cardScript.myTags.Contains(tagToCheck) || CombatManager.ShouldSkipEffectProcessing(cardScript) || cardScript.myStatusRef != myCardScript.myStatusRef || IsCardAtTop(card) || cardScript.isMinion)
			{
				cardsWithTag.RemoveAt(i);
			}
		}

		cardsWithTag = UtilityFuncManagerScript.ShuffleList(cardsWithTag);
		StageChosenCards(cardsWithTag, amount);
	}

	public void StageTheirCardsWithTag(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsWithTag = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsWithTag, true);

		// Filter cards that have the specified tag, belong to the opponent, and are not at the top
		for (int i = cardsWithTag.Count - 1; i >= 0; i--)
		{
			var card = cardsWithTag[i];
			var cardScript = card.GetComponent<CardScript>();
			if (!cardScript.myTags.Contains(tagToCheck) || CombatManager.ShouldSkipEffectProcessing(cardScript) || cardScript.myStatusRef == myCardScript.myStatusRef || IsCardAtTop(card) || cardScript.isMinion)
			{
				cardsWithTag.RemoveAt(i);
			}
		}

		cardsWithTag = UtilityFuncManagerScript.ShuffleList(cardsWithTag);
		StageChosenCards(cardsWithTag, amount);
	}

	/// <summary>
	/// Stage 所有符合条件的己方 Minion 卡
	/// </summary>
	/// <param name="targetCardTypeID">目标卡牌类型ID，为空字符串时匹配所有己方 Minion 卡</param>
	public void StageAllFriendlyMinion(string targetCardTypeID)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var friendlyMinions = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, friendlyMinions, true);

		// Filter: 己方 Minion 卡，且不在顶部
		for (int i = friendlyMinions.Count - 1; i >= 0; i--)
		{
			var card = friendlyMinions[i];
			var cardScript = card.GetComponent<CardScript>();
			
			// 排除非 Minion 卡、非己方卡、已在顶部的卡、以及需要跳过的卡
			if (!cardScript.isMinion || 
			    CombatManager.ShouldSkipEffectProcessing(cardScript) || 
			    cardScript.myStatusRef != myCardScript.myStatusRef || 
			    IsCardAtTop(card))
			{
				friendlyMinions.RemoveAt(i);
				continue;
			}
			
			// 如果指定了 card type id，则只匹配对应类型的卡
			if (!string.IsNullOrEmpty(targetCardTypeID) && cardScript.cardTypeID != targetCardTypeID)
			{
				friendlyMinions.RemoveAt(i);
			}
		}

		StageChosenCards(friendlyMinions, friendlyMinions.Count);
	}

	public void StageMyCards_BasedOnIntSO(IntSO intSO)
	{
		if (intSO == null) return;
		StageMyCards(intSO.value);
	}

	private void StageChosenCards(List<GameObject> cardsToStage, int amount)
	{
		amount = Mathf.Clamp(amount, 0, cardsToStage.Count);
		if (amount == 0) return;

		// 1. 先修改逻辑列表，并收集成功移动的卡片
		var stagedCards = new List<GameObject>();
		for (var i = 0; i < amount; i++)
		{
			var targetCard = cardsToStage[i];
			var targetCardScript = targetCard.GetComponent<CardScript>();

			if (_combinedDeck.Contains(targetCard))
			{
				_combinedDeck.Remove(targetCard);
				_combinedDeck.Add(targetCard);  // 添加到顶部
				stagedCards.Add(targetCard);
				
				string myColor = GetMyCardColorTag();
				string targetColor = GetCardColorTag(targetCard);
				effectResultString.value += "// [<color=" + myColor + ">" + myCard.gameObject.name + "</color>] staged [<color=" + targetColor + ">" +
					targetCardScript.gameObject.name + "</color>] to the top of the deck\n";
			}
		}
		
		// 2. 播放弧形轨迹动画（移动卡片到顶部）
		foreach (var card in stagedCards)
		{
			CombatUXManager.me.MoveCardToTop(card, duration: 0.5f, useArc: true);
		}
	}
}
