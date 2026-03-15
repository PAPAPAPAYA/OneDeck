using System;
using System.Collections.Generic;
using DefaultNamespace.Managers;
using DefaultNamespace.SOScripts;
using Unity.VisualScripting;
using UnityEngine;

public class CardManipulationEffect : EffectScript
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

	#region EXILE
	// choose cards logic in here, move cards logic in CombatFuncs
	public void ExileMyCards(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsToSend = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsToSend, true);
		for (int i = cardsToSend.Count - 1; i >= 0; i--)
		{
			// take out opponent's cards
			if (cardsToSend[i].GetComponent<CardScript>().myStatusRef != myCardScript.myStatusRef) // if card doesn't belong to this card's owner
			{
				cardsToSend.RemoveAt(i);
			}
		}
		//cardsToSend = UtilityFuncManagerScript.ShuffleList(cardsToSend);
		ExileChosenCards(cardsToSend, amount);
	}
	public void ExileRandomCards(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsToSend = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsToSend, true);
		//cardsToSend = UtilityFuncManagerScript.ShuffleList(cardsToSend);
		ExileChosenCards(cardsToSend, amount);
	}

	public void ExileTheirCards(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsToSend = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsToSend, true);
		for (int i = cardsToSend.Count - 1; i >= 0; i--)
		{
			// take out card owner's cards
			if (cardsToSend[i].GetComponent<CardScript>().myStatusRef == myCardScript.myStatusRef) // if card belongs to this card's owner
			{
				cardsToSend.RemoveAt(i);
			}
		}
		//cardsToSend = UtilityFuncManagerScript.ShuffleList(cardsToSend);
		ExileChosenCards(cardsToSend, amount);
	}

	private void ExileChosenCards(List<GameObject> cardsToSend, int amount)
	{
		amount = Mathf.Clamp(amount, 0, _combinedDeck.Count);
		if (amount == 0) return;
		if (cardsToSend.Count == 0) return;
		string myColor = GetMyCardColorTag();
		for (var i = 0; i < amount; i++)
		{
			var targetCardScript = cardsToSend[i].GetComponent<CardScript>();
			string targetColor = GetCardColorTag(cardsToSend[i]);
			effectResultString.value +=
				"// [<color=" + myColor + ">" + myCard.gameObject.name + "</color>] exiled [<color=" + targetColor + ">" +
				targetCardScript.gameObject.name + "</color>]\n";
			// [已废弃] 放逐效果暂不可用
		}
	}
	#endregion
	
	#region REVIVE
	public void ReviveRandomMyCardsFromGrave(int amount)
	{
		// [已废弃] 墓地机制已移除
		return;
	}

	public void ReviveRandomTheirCardsFromGrave(int amount)
	{
		// [已废弃] 墓地机制已移除
		return;
	}
	public void ReviveRandomCards(int amount)
	{
		// [已废弃] 墓地机制已移除
		return;
	}

	public void ReviveSelf() // put self back from grave to deck
	{
		// [已废弃] 墓地机制已移除
		return;
	}
	#endregion
	
	#region STAGE
	public void StageSelf() // put self on top of the deck
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsToStage = new List<GameObject> { myCard };
		StageChosenCards(cardsToStage, 1);
	}

	public void StageCardsWithTag(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsWithTag = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsWithTag, true);

		// Filter cards that have the specified tag
		for (int i = cardsWithTag.Count - 1; i >= 0; i--)
		{
			var cardScript = cardsWithTag[i].GetComponent<CardScript>();
			if (!cardScript.myTags.Contains(tagToCheck))
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

		// Filter cards that belong to this card's owner
		for (int i = myCards.Count - 1; i >= 0; i--)
		{
			var cardScript = myCards[i].GetComponent<CardScript>();
			if (cardScript.myStatusRef != myCardScript.myStatusRef)
			{
				myCards.RemoveAt(i);
			}
		}

		myCards = UtilityFuncManagerScript.ShuffleList(myCards);
		StageChosenCards(myCards, amount);
	}

	public void StageMyCardsWithTag(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsWithTag = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsWithTag, true);

		// Filter cards that have the specified tag and belong to this card's owner
		for (int i = cardsWithTag.Count - 1; i >= 0; i--)
		{
			var cardScript = cardsWithTag[i].GetComponent<CardScript>();
			if (!cardScript.myTags.Contains(tagToCheck) || cardScript.myStatusRef != myCardScript.myStatusRef)
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

		// Filter cards that have the specified tag and belong to the opponent
		for (int i = cardsWithTag.Count - 1; i >= 0; i--)
		{
			var cardScript = cardsWithTag[i].GetComponent<CardScript>();
			if (!cardScript.myTags.Contains(tagToCheck) || cardScript.myStatusRef == myCardScript.myStatusRef)
			{
				cardsWithTag.RemoveAt(i);
			}
		}

		cardsWithTag = UtilityFuncManagerScript.ShuffleList(cardsWithTag);
		StageChosenCards(cardsWithTag, amount);
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
		
		// 2. 播放特殊动画（替代原来的 Sync + Update）
		CombatUXManager.me.PlayStageBuryAnimation(stagedCards, isStage: true);
	}
	#endregion
	
	#region BURY
	public void BurySelf() // put self at the bottom of the deck
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsToBury = new List<GameObject> { transform.parent.gameObject };
		BuryChosenCards(cardsToBury, 1);
	}

	public void BuryCardsWithTag(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsWithTag = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsWithTag, true);

		// Filter cards that have the specified tag
		for (int i = cardsWithTag.Count - 1; i >= 0; i--)
		{
			var cardScript = cardsWithTag[i].GetComponent<CardScript>();
			if (!cardScript.myTags.Contains(tagToCheck))
			{
				cardsWithTag.RemoveAt(i);
			}
		}

		cardsWithTag = UtilityFuncManagerScript.ShuffleList(cardsWithTag);
		BuryChosenCards(cardsWithTag, amount);
	}

	public void BuryMyCardsWithTag(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsWithTag = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsWithTag, true);

		// Filter cards that have the specified tag and belong to this card's owner
		for (int i = cardsWithTag.Count - 1; i >= 0; i--)
		{
			var cardScript = cardsWithTag[i].GetComponent<CardScript>();
			if (!cardScript.myTags.Contains(tagToCheck) || cardScript.myStatusRef != myCardScript.myStatusRef)
			{
				cardsWithTag.RemoveAt(i);
			}
		}

		cardsWithTag = UtilityFuncManagerScript.ShuffleList(cardsWithTag);
		BuryChosenCards(cardsWithTag, amount);
	}

	public void BuryTheirCardsWithTag(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsWithTag = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsWithTag, true);

		// Filter cards that have the specified tag and belong to the opponent
		for (int i = cardsWithTag.Count - 1; i >= 0; i--)
		{
			var cardScript = cardsWithTag[i].GetComponent<CardScript>();
			if (!cardScript.myTags.Contains(tagToCheck) || cardScript.myStatusRef == myCardScript.myStatusRef)
			{
				cardsWithTag.RemoveAt(i);
			}
		}

		cardsWithTag = UtilityFuncManagerScript.ShuffleList(cardsWithTag);
		BuryChosenCards(cardsWithTag, amount);
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
		
		// 2. 播放特殊动画（替代原来的 Sync + Update）
		CombatUXManager.me.PlayStageBuryAnimation(buriedCards, isStage: false);
	}
	#endregion

	#region DELAY
	public void DelayMyCards(int amount)
	{
		var myCards = GetCardsByOwner(isMyCards: true);
		ExecuteDelay(myCards, amount);
	}

	public void DelayTheirCards(int amount)
	{
		var theirCards = GetCardsByOwner(isMyCards: false);
		ExecuteDelay(theirCards, amount);
	}

	private List<GameObject> GetCardsByOwner(bool isMyCards)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var result = new List<GameObject>();
		foreach (var card in _combinedDeck)
		{
			var cardScript = card.GetComponent<CardScript>();
			bool isOwner = cardScript.myStatusRef == myCardScript.myStatusRef;
			if (isOwner == isMyCards)
			{
				result.Add(card);
			}
		}
		return result;
	}

	private void ExecuteDelay(List<GameObject> candidates, int amount)
	{
		if (amount <= 0 || candidates.Count == 0) return;

		candidates = UtilityFuncManagerScript.ShuffleList(candidates);
		amount = Mathf.Min(amount, candidates.Count);

		int movedCount = 0;
		string myColor = GetMyCardColorTag();

		for (int i = 0; i < amount; i++)
		{
			var card = candidates[i];
			int index = _combinedDeck.IndexOf(card);

			if (index <= 0) continue;

			_combinedDeck.RemoveAt(index);
			_combinedDeck.Insert(index - 1, card);
			movedCount++;

			var targetScript = card.GetComponent<CardScript>();
			string targetColor = GetCardColorTag(card);
			effectResultString.value += $"// [<color={myColor}>{myCard.name}</color>] delayed [<color={targetColor}>{targetScript.name}</color>]\n";
		}

		if (movedCount > 0)
		{
			CombatUXManager.me.SyncPhysicalCardsWithCombinedDeck();
			CombatUXManager.me.UpdateAllPhysicalCardTargets();
		}
	}
	#endregion
}