using System.Collections.Generic;
using DefaultNamespace.Managers;
using UnityEngine;

// CardManipulationEffect 和 GameEventStorage 在全局命名空间

public class MinionCostEffect : EffectScript
{
	public void ExecuteMinionCost()
	{
		int costCount = myCardScript.minionCostCount;
		string costCardTypeID = myCardScript.minionCostCardTypeID;
		var costOwner = myCardScript.minionCostOwner;

		if (costCount <= 0) return;

		var combinedDeck = combatManager.combinedDeckZone;

		// 收集符合条件的卡（指定所属，指定类型）
		var eligibleCards = new List<GameObject>();
		foreach (var card in combinedDeck)
		{
			if (card == null) continue;
			
			var cardScript = card.GetComponent<CardScript>();
			if (cardScript == null) continue;
			
			// 跳过中立卡和 Start Card
			if (CombatManager.ShouldSkipEffectProcessing(cardScript)) continue;
			
			// 检查是否为 minion 卡
			if (!cardScript.isMinion) continue;
			
			// 检查所属玩家
			bool isMyCard = cardScript.myStatusRef == myCardScript.myStatusRef;
			switch (costOwner)
			{
				case EnumStorage.TargetType.Me:
					if (!isMyCard) continue;
					break;
				case EnumStorage.TargetType.Them:
					if (isMyCard) continue;
					break;
				case EnumStorage.TargetType.Random:
					// 任意所属都符合条件
					break;
			}
			
			// 检查卡类型（如果指定了类型）
			if (!string.IsNullOrEmpty(costCardTypeID))
			{
				if (cardScript.cardTypeID != costCardTypeID) continue;
			}
			
			eligibleCards.Add(card);
		}

		// 检查是否有足够符合条件的卡
		if (eligibleCards.Count < costCount)
		{
			// 显示失败信息并阻止效果发动
			string myColor = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? "#87CEEB" : "orange";
			string typeInfo = string.IsNullOrEmpty(costCardTypeID) ? "card(s)" : $"[{costCardTypeID}]";
			string ownerInfo = GetOwnerDescription(costOwner);
			string failMessage = $"// [<color={myColor}>{myCard.name}</color>] token cost failed: need <color=yellow>{costCount}</color> {ownerInfo} {typeInfo}, found <color=yellow>{eligibleCards.Count}</color>\n";
			
			var container = GetComponent<CostNEffectContainer>();
			if (container != null)
			{
				container.SetCostNotMet(failMessage);
			}
			return;
		}

		// 随机打乱并选择要消耗的卡
		eligibleCards = UtilityFuncManagerScript.ShuffleList(eligibleCards);
		var cardsToConsume = eligibleCards.GetRange(0, costCount);

		// 从卡组中消耗这些卡（使用统一销毁方法，带动画）
		int destroyedCount = 0;
		foreach (var card in cardsToConsume)
		{
			var cardScript = card.GetComponent<CardScript>();
			bool isMyCard = cardScript.myStatusRef == myCardScript.myStatusRef;
			
			// 触发 onFriendlyCardExiled 事件（如果是友方卡被消耗）
			// 根据被消耗卡的归属触发对应一方的事件：玩家方用RaiseOwner，敌方用RaiseOpponent
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
			
			// 触发 OnFriendlyFlyExiled 事件（如果是友方fly卡）
			// 根据被消耗fly的归属触发对应一方的事件：玩家方用RaiseOwner，敌方用RaiseOpponent
			if (isMyCard && cardScript.cardTypeID == "FLY")
			{
				if (GameEventStorage.me.onFriendlyFlyExiled != null)
				{
					if (cardScript.myStatusRef == combatManager.ownerPlayerStatusRef)
					{
						GameEventStorage.me.onFriendlyFlyExiled.RaiseOwner();
					}
					else
					{
						GameEventStorage.me.onFriendlyFlyExiled.RaiseOpponent();
					}
				}
			}
			
			CombatUXManager.me.DestroyCardWithAnimation(card, onComplete: () =>
			{
				destroyedCount++;
			});
		}

		// 同步剩余物理卡牌位置
		if (cardsToConsume.Count > 0)
		{
			CombatUXManager.me.SyncPhysicalCardsWithCombinedDeck();
			CombatUXManager.me.UpdateAllPhysicalCardTargets();
		}

		// 显示消耗信息
		string ownerColor = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? "#87CEEB" : "orange";
		string cardTypeInfo = string.IsNullOrEmpty(costCardTypeID) ? "card(s)" : $"[{costCardTypeID}]";
		string consumedOwnerInfo = GetOwnerDescription(costOwner);
		effectResultString.value += $"// [<color={ownerColor}>{myCard.name}</color>] minion cost: consumed <color=yellow>{cardsToConsume.Count}</color> {consumedOwnerInfo} {cardTypeInfo}\n";
	}

	private string GetOwnerDescription(EnumStorage.TargetType owner)
	{
		return owner switch
		{
			EnumStorage.TargetType.Me => "ally minion",
			EnumStorage.TargetType.Them => "enemy minion",
			EnumStorage.TargetType.Random => "any minion",
			_ => ""
		};
	}
}
