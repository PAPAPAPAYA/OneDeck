using System.Collections.Generic;
using DefaultNamespace.Managers;
using DefaultNamespace.SOScripts;
using UnityEngine;

public class ExileEffect : EffectScript
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

	public void ExileSelf() // exile self from the deck
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsToExile = new List<GameObject> { myCard };
		ExileChosenCards(cardsToExile, 1);
	}

	public void ExileMyCards(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsToExile = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsToExile, true);

		// Filter: 己方卡，排除需要跳过的卡
		for (int i = cardsToExile.Count - 1; i >= 0; i--)
		{
			var card = cardsToExile[i];
			var cardScript = card.GetComponent<CardScript>();
			if (CombatManager.ShouldSkipEffectProcessing(cardScript) || cardScript.myStatusRef != myCardScript.myStatusRef)
			{
				cardsToExile.RemoveAt(i);
			}
		}

		cardsToExile = UtilityFuncManagerScript.ShuffleList(cardsToExile);
		ExileChosenCards(cardsToExile, amount);
	}

	public void ExileTheirCards(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsToExile = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsToExile, true);

		// Filter: 敌方卡，排除需要跳过的卡
		for (int i = cardsToExile.Count - 1; i >= 0; i--)
		{
			var card = cardsToExile[i];
			var cardScript = card.GetComponent<CardScript>();
			if (CombatManager.ShouldSkipEffectProcessing(cardScript) || cardScript.myStatusRef == myCardScript.myStatusRef)
			{
				cardsToExile.RemoveAt(i);
			}
		}

		cardsToExile = UtilityFuncManagerScript.ShuffleList(cardsToExile);
		ExileChosenCards(cardsToExile, amount);
	}

	public void ExileRandomCards(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsToExile = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsToExile, true);

		// Filter: 排除需要跳过的卡
		for (int i = cardsToExile.Count - 1; i >= 0; i--)
		{
			var card = cardsToExile[i];
			var cardScript = card.GetComponent<CardScript>();
			if (CombatManager.ShouldSkipEffectProcessing(cardScript))
			{
				cardsToExile.RemoveAt(i);
			}
		}

		cardsToExile = UtilityFuncManagerScript.ShuffleList(cardsToExile);
		ExileChosenCards(cardsToExile, amount);
	}

	public void ExileMyCardsWithTag(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsToExile = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsToExile, true);

		// Filter: 己方卡且有指定tag，排除需要跳过的卡
		for (int i = cardsToExile.Count - 1; i >= 0; i--)
		{
			var card = cardsToExile[i];
			var cardScript = card.GetComponent<CardScript>();
			if (!cardScript.myTags.Contains(tagToCheck) || 
			    CombatManager.ShouldSkipEffectProcessing(cardScript) || 
			    cardScript.myStatusRef != myCardScript.myStatusRef)
			{
				cardsToExile.RemoveAt(i);
			}
		}

		cardsToExile = UtilityFuncManagerScript.ShuffleList(cardsToExile);
		ExileChosenCards(cardsToExile, amount);
	}

	public void ExileTheirCardsWithTag(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsToExile = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsToExile, true);

		// Filter: 敌方卡且有指定tag，排除需要跳过的卡
		for (int i = cardsToExile.Count - 1; i >= 0; i--)
		{
			var card = cardsToExile[i];
			var cardScript = card.GetComponent<CardScript>();
			if (!cardScript.myTags.Contains(tagToCheck) || 
			    CombatManager.ShouldSkipEffectProcessing(cardScript) || 
			    cardScript.myStatusRef == myCardScript.myStatusRef)
			{
				cardsToExile.RemoveAt(i);
			}
		}

		cardsToExile = UtilityFuncManagerScript.ShuffleList(cardsToExile);
		ExileChosenCards(cardsToExile, amount);
	}

	public void ExileCardsWithTag(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsToExile = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsToExile, true);

		// Filter: 有指定tag，排除需要跳过的卡
		for (int i = cardsToExile.Count - 1; i >= 0; i--)
		{
			var card = cardsToExile[i];
			var cardScript = card.GetComponent<CardScript>();
			if (!cardScript.myTags.Contains(tagToCheck) || CombatManager.ShouldSkipEffectProcessing(cardScript))
			{
				cardsToExile.RemoveAt(i);
			}
		}

		cardsToExile = UtilityFuncManagerScript.ShuffleList(cardsToExile);
		ExileChosenCards(cardsToExile, amount);
	}

	public void ExileMyMinions(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var minions = new List<GameObject>();

		// Filter: 己方 Minion 卡
		foreach (var card in _combinedDeck)
		{
			if (card == null) continue;
			var cardScript = card.GetComponent<CardScript>();
			if (cardScript == null) continue;
			if (CombatManager.ShouldSkipEffectProcessing(cardScript)) continue;
			if (cardScript.isMinion && cardScript.myStatusRef == myCardScript.myStatusRef)
			{
				minions.Add(card);
			}
		}

		minions = UtilityFuncManagerScript.ShuffleList(minions);
		ExileChosenCards(minions, amount);
	}

	public void ExileTheirMinions(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var minions = new List<GameObject>();

		// Filter: 敌方 Minion 卡
		foreach (var card in _combinedDeck)
		{
			if (card == null) continue;
			var cardScript = card.GetComponent<CardScript>();
			if (cardScript == null) continue;
			if (CombatManager.ShouldSkipEffectProcessing(cardScript)) continue;
			if (cardScript.isMinion && cardScript.myStatusRef != myCardScript.myStatusRef)
			{
				minions.Add(card);
			}
		}

		minions = UtilityFuncManagerScript.ShuffleList(minions);
		ExileChosenCards(minions, amount);
	}

	public void ExileMyCards_BasedOnIntSO(IntSO intSO)
	{
		if (intSO == null) return;
		ExileMyCards(intSO.value);
	}

	public void ExileTheirCards_BasedOnIntSO(IntSO intSO)
	{
		if (intSO == null) return;
		ExileTheirCards(intSO.value);
	}

	private void ExileChosenCards(List<GameObject> cardsToExile, int amount)
	{
		amount = Mathf.Clamp(amount, 0, cardsToExile.Count);
		if (amount == 0) return;

		string myColor = GetMyCardColorTag();
		var exiledCards = new List<GameObject>();

		for (var i = 0; i < amount; i++)
		{
			var targetCard = cardsToExile[i];
			var targetCardScript = targetCard.GetComponent<CardScript>();
			string targetColor = GetCardColorTag(targetCard);

			// 使用统一销毁方法（带动画）- 放逐效果与销毁类似，都是将卡牌从游戏中移除
			CombatUXManager.me.DestroyCardWithAnimation(targetCard);

			effectResultString.value += "// [<color=" + myColor + ">" + myCard.gameObject.name + "</color>] exiled [<color=" + targetColor + ">" +
				targetCardScript.gameObject.name + "</color>]\n";

			exiledCards.Add(targetCard);
		}

		// 触发 onFriendlyCardExiled 事件（检查是否有友方卡被放逐）
		foreach (var card in exiledCards)
		{
			var cardScript = card.GetComponent<CardScript>();
			bool isMyCard = cardScript.myStatusRef == myCardScript.myStatusRef;
			if (isMyCard)
			{
				if (GameEventStorage.me.onFriendlyCardExiled != null)
				{
					if (cardScript.myStatusRef == combatManager.ownerPlayerStatusRef)
					{
						GameEventStorage.me.onFriendlyCardExiled.RaiseOwner();
					}
					else
					{
						GameEventStorage.me.onFriendlyCardExiled.RaiseOpponent();
					}
				}
			}
		}

		// 同步剩余物理卡牌位置
		if (exiledCards.Count > 0)
		{
			CombatUXManager.me.SyncPhysicalCardsWithCombinedDeck();
			CombatUXManager.me.UpdateAllPhysicalCardTargets();
		}
	}
}
