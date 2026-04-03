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
		for (int i = 0; i < _combinedDeck.Count; i++)
		{
			var card = _combinedDeck[i];
			var cardScript = card.GetComponent<CardScript>();
			// 跳过中立卡和 Start Card
			if (CombatManager.ShouldSkipEffectProcessing(cardScript)) continue;
			bool isOwner = cardScript.myStatusRef == myCardScript.myStatusRef;
			// 只返回归属正确且在 index > 0 的卡（index 0 的卡无法 delay）
			if (isOwner == isMyCards && i > 0)
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
		var delayedCards = new List<(GameObject card, int newIndex)>();

		for (int i = 0; i < amount; i++)
		{
			var card = candidates[i];
			int index = _combinedDeck.IndexOf(card);

			// index 检查已在 GetCardsByOwner 中完成，这里做防御性检查
			if (index <= 0) continue;

			_combinedDeck.RemoveAt(index);
			int newIndex = index - 1;
			_combinedDeck.Insert(newIndex, card);
			movedCount++;
			delayedCards.Add((card, newIndex));

			var targetScript = card.GetComponent<CardScript>();
			string targetColor = GetCardColorTag(card);
			effectResultString.value += $"// [<color={myColor}>{myCard.name}</color>] delayed [<color={targetColor}>{targetScript.name}</color>]\n";
		}

		if (movedCount > 0)
		{
			// 同步物理卡片列表
			CombatUXManager.me.SyncPhysicalCardsWithCombinedDeck();
			
			// 为每张移动的卡片播放动画
			foreach (var (card, newIndex) in delayedCards)
			{
				CombatUXManager.me.MoveCardToIndex(card, newIndex, duration: 0.3f, useArc: false);
			}
			
			// 更新其他卡片位置（确保所有卡片位置正确）
			CombatUXManager.me.UpdateAllPhysicalCardTargets();
		}
	}
	#endregion

	#region DESTROY MINION
	/// <summary>
	/// 销毁指定数量的己方 Minion 卡
	/// </summary>
	public void DestroyMyMinions(int amount)
	{
		var minions = GetMinionsByOwner(isMyMinions: true);
		ExecuteDestroyMinions(minions, amount);
	}
	
	/// <summary>
	/// 销毁指定数量的敌方 Minion 卡
	/// </summary>
	public void DestroyTheirMinions(int amount)
	{
		var minions = GetMinionsByOwner(isMyMinions: false);
		ExecuteDestroyMinions(minions, amount);
	}
	
	/// <summary>
	/// 销毁指定数量的任意 Minion 卡（随机选择）
	/// </summary>
	public void DestroyRandomMinions(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var minions = new List<GameObject>();
		
		foreach (var card in _combinedDeck)
		{
			if (card == null) continue;
			var cardScript = card.GetComponent<CardScript>();
			if (cardScript == null) continue;
			if (cardScript.isMinion)
			{
				minions.Add(card);
			}
		}
		
		ExecuteDestroyMinions(minions, amount);
	}
	
	/// <summary>
	/// 销毁指定数量的指定 Tag 的 Minion 卡
	/// </summary>
	public void DestroyMinionsWithTag(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var minionsWithTag = new List<GameObject>();
		
		foreach (var card in _combinedDeck)
		{
			if (card == null) continue;
			var cardScript = card.GetComponent<CardScript>();
			if (cardScript == null) continue;
			if (cardScript.isMinion && cardScript.myTags.Contains(tagToCheck))
			{
				minionsWithTag.Add(card);
			}
		}
		
		ExecuteDestroyMinions(minionsWithTag, amount);
	}
	
	/// <summary>
	/// 获取指定归属的 Minion 卡列表
	/// </summary>
	private List<GameObject> GetMinionsByOwner(bool isMyMinions)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var result = new List<GameObject>();
		
		foreach (var card in _combinedDeck)
		{
			if (card == null) continue;
			var cardScript = card.GetComponent<CardScript>();
			if (cardScript == null) continue;
			if (!cardScript.isMinion) continue;
			
			bool isOwner = cardScript.myStatusRef == myCardScript.myStatusRef;
			if (isOwner == isMyMinions)
			{
				result.Add(card);
			}
		}
		
		return result;
	}
	
	/// <summary>
	/// 执行销毁 Minion 卡（带动画）
	/// </summary>
	private void ExecuteDestroyMinions(List<GameObject> minions, int amount)
	{
		if (amount <= 0 || minions.Count == 0) return;
		
		minions = UtilityFuncManagerScript.ShuffleList(minions);
		amount = Mathf.Min(amount, minions.Count);
		
		string myColor = GetMyCardColorTag();
		
		for (int i = 0; i < amount; i++)
		{
			var minion = minions[i];
			var minionScript = minion.GetComponent<CardScript>();
			string minionColor = GetCardColorTag(minion);
			
			// 使用统一销毁方法（带动画）
			CombatUXManager.me.DestroyCardWithAnimation(minion);
			
			effectResultString.value += $"// [<color={myColor}>{myCard.name}</color>] destroyed minion [<color={minionColor}>{minionScript.name}</color>]\n";
		}
		
		// 同步剩余物理卡牌位置
		if (amount > 0)
		{
			CombatUXManager.me.SyncPhysicalCardsWithCombinedDeck();
			CombatUXManager.me.UpdateAllPhysicalCardTargets();
		}
	}
	#endregion

	#region IntSO Based Effects

	public void DestroyTheirMinions_BasedOnIntSO(IntSO intSO)
	{
		if (intSO == null) return;
		DestroyTheirMinions(intSO.value);
	}

	public void DestroyMyMinions_BasedOnIntSO(IntSO intSO)
	{
		if (intSO == null) return;
		DestroyMyMinions(intSO.value);
	}

	#endregion
}
