using System.Collections.Generic;
using DefaultNamespace.Managers;
using UnityEngine;

public class DelayCostEffect : EffectScript
{
	public void ExecuteDelayCost()
	{
		int cost = myCardScript.delayCost;
		if (cost <= 0) return;

		var combinedDeck = combatManager.combinedDeckZone;

		// 收集己方卡（排除当前正在发动的卡，它已经在revealZone了）
		var myCards = new List<GameObject>();
		for (int i = 0; i < combinedDeck.Count; i++)
		{
			var cardScript = combinedDeck[i].GetComponent<CardScript>();
			// 跳过中立卡和 Start Card，只收集己方卡
			if (!CombatManager.ShouldSkipEffectProcessing(cardScript) && cardScript.myStatusRef == myCardScript.myStatusRef)
			{
				myCards.Add(combinedDeck[i]);
			}
		}

		if (myCards.Count == 0) return;

		// 随机打乱
		myCards = UtilityFuncManagerScript.ShuffleList(myCards);

		// 执行推迟
		int movedCount = 0;
		for (int i = 0; i < myCards.Count && movedCount < cost; i++)
		{
			var card = myCards[i];

			// 找到当前index（因为前面的移动可能改变了顺序）
			int currentIndex = combinedDeck.IndexOf(card);
			if (currentIndex < 0) continue; // 卡已不在牌组中（不应该发生）

			// 如果卡已经在最底部（index 0），无法继续推迟
			if (currentIndex <= 0) continue;

			// 移动：remove后insert到index-1（往底部移，推迟揭晓）
			combinedDeck.RemoveAt(currentIndex);
			combinedDeck.Insert(currentIndex - 1, card);
			movedCount++;
		}

		// 同步物理卡牌位置
		if (movedCount > 0)
		{
			CombatUXManager.me.SyncPhysicalCardsWithCombinedDeck();
			CombatUXManager.me.UpdateAllPhysicalCardTargets();
		}

		// 显示信息
		if (movedCount > 0)
		{
			string myColor = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? "#87CEEB" : "orange";
			effectResultString.value += $"// [<color={myColor}>{myCard.name}</color>] delay cost: moved <color=yellow>{movedCount}</color> card(s) back\n";
		}
	}
}
