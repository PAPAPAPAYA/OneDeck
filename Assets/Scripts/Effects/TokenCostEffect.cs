using System.Collections.Generic;
using DefaultNamespace.Managers;
using UnityEngine;

public class TokenCostEffect : EffectScript
{
	public void ExecuteTokenCost()
	{
		int costCount = myCardScript.tokenCostCount;
		string costCardTypeID = myCardScript.tokenCostCardTypeID;
		var costOwner = myCardScript.tokenCostOwner;

		if (costCount <= 0) return;

		var combinedDeck = combatManager.combinedDeckZone;

		// 收集符合条件的卡（指定所属，指定类型）
		var eligibleCards = new List<GameObject>();
		foreach (var card in combinedDeck)
		{
			if (card == null) continue;
			
			var cardScript = card.GetComponent<CardScript>();
			if (cardScript == null) continue;
			
			// 检查是否为 token 卡
			if (!cardScript.isToken) continue;
			
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

		// 从卡组中移除这些卡（消耗）
		foreach (var card in cardsToConsume)
		{
			combinedDeck.Remove(card);
			Destroy(card);
		}

		// 同步物理卡牌
		if (cardsToConsume.Count > 0)
		{
			CombatUXManager.me.SyncPhysicalCardsWithCombinedDeck();
			CombatUXManager.me.UpdateAllPhysicalCardTargets();
		}

		// 显示消耗信息
		string ownerColor = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? "#87CEEB" : "orange";
		string cardTypeInfo = string.IsNullOrEmpty(costCardTypeID) ? "card(s)" : $"[{costCardTypeID}]";
		string consumedOwnerInfo = GetOwnerDescription(costOwner);
		effectResultString.value += $"// [<color={ownerColor}>{myCard.name}</color>] token cost: consumed <color=yellow>{cardsToConsume.Count}</color> {consumedOwnerInfo} {cardTypeInfo}\n";
	}

	private string GetOwnerDescription(EnumStorage.TargetType owner)
	{
		return owner switch
		{
			EnumStorage.TargetType.Me => "ally",
			EnumStorage.TargetType.Them => "enemy",
			EnumStorage.TargetType.Random => "random",
			_ => ""
		};
	}
}
