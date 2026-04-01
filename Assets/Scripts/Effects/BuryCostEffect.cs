using System.Collections.Generic;
using DefaultNamespace.Managers;
using UnityEngine;

public class BuryCostEffect : EffectScript
{
	public void ExecuteBuryCost()
	{
		int costCount = myCardScript.buryCost;
		
		if (costCount <= 0) return;

		var combinedDeck = combatManager.combinedDeckZone;

		// 收集己方卡（排除当前正在发动的卡）
		var eligibleCards = new List<GameObject>();
		foreach (var card in combinedDeck)
		{
			if (card == null) continue;
			
			var cardScript = card.GetComponent<CardScript>();
			if (cardScript == null) continue;
			
			// 跳过中立卡（Start Card 等）
			if (CombatManager.ShouldSkipEffectProcessing(cardScript)) continue;
			
			// 排除当前正在发动的卡
			if (card == myCard) continue;
			
			// 只收集己方卡
			if (cardScript.myStatusRef != myCardScript.myStatusRef) continue;
			
			eligibleCards.Add(card);
		}

		// 检查是否有足够的己方卡
		if (eligibleCards.Count < costCount)
		{
			// 显示失败信息并阻止效果发动
			string myColor = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? "#87CEEB" : "orange";
			string failMessage = $"// [<color={myColor}>{myCard.name}</color>] bury cost failed: need <color=yellow>{costCount}</color> ally card(s), found <color=yellow>{eligibleCards.Count}</color>\n";
			
			var container = GetComponent<CostNEffectContainer>();
			if (container != null)
			{
				container.SetCostNotMet(failMessage);
			}
			return;
		}

		// 随机打乱并选择要置底的卡
		eligibleCards = UtilityFuncManagerScript.ShuffleList(eligibleCards);
		var cardsToBury = eligibleCards.GetRange(0, costCount);

		// 修改逻辑列表：将选中的卡移到底部
		var buriedCards = new List<GameObject>();
		foreach (var card in cardsToBury)
		{
			if (combinedDeck.Contains(card))
			{
				combinedDeck.Remove(card);
				combinedDeck.Insert(0, card);  // 插入到底部
				buriedCards.Add(card);
				
				var targetScript = card.GetComponent<CardScript>();
				string myColor = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? "#87CEEB" : "orange";
				string targetColor = targetScript.myStatusRef == combatManager.ownerPlayerStatusRef ? "#87CEEB" : "orange";
				effectResultString.value += $"// [<color={myColor}>{myCard.name}</color>] bury cost: buried [<color={targetColor}>{targetScript.name}</color>] to the bottom\n";
			}
		}

		// 播放弧形轨迹动画（移动卡片到底部）
		foreach (var card in buriedCards)
		{
			CombatUXManager.me.MoveCardToBottom(card, duration: 0.5f, useArc: true);
		}
		
		// 触发友方卡被 bury 事件
		foreach (var card in buriedCards)
		{
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
	}
}
