using DefaultNamespace.SOScripts;
using UnityEngine;

public class ValueTrackerManager : MonoBehaviour
{
	public static ValueTrackerManager me;

	[Header("Tracker Refs")]
	public IntSO friendlyInGraveAmountRef;
	public IntSO hostileCursePowerCount;

	[Header("Curse Card Config")]
	[Tooltip("Cursed card type ID，用于统计敌方对应卡的Power总数")]
	public StringSO curseCardTypeId;

	private void Awake()
	{
		me = this;
	}

	/// <summary>
	/// 在效果执行前统一刷新所有追踪数值
	/// </summary>
	public void UpdateAllTrackers()
	{
		UpdateFriendlyInGraveAmount();
		UpdateHostileCursePowerCount();
	}

	/// <summary>
	/// 更新 FriendlyInGraveAmount：统计 combinedDeckZone 中位于 Start Card 下方（index 更小）的友方卡数量。
	/// 这些卡将在 Start Card 之后被揭晓，视为处于"墓地"中。
	/// </summary>
	private void UpdateFriendlyInGraveAmount()
	{
		if (friendlyInGraveAmountRef == null || CombatManager.Me == null) return;

		var deck = CombatManager.Me.combinedDeckZone;
		int startCardIndex = -1;

		// 找到 Start Card 的 index
		for (int i = 0; i < deck.Count; i++)
		{
			var cardScript = deck[i].GetComponent<CardScript>();
			if (cardScript != null && cardScript.isStartCard)
			{
				startCardIndex = i;
				break;
			}
		}

		// 如果没有找到 Start Card，则计数为 0
		if (startCardIndex < 0)
		{
			friendlyInGraveAmountRef.value = 0;
			return;
		}

		// 统计 index 比 Start Card 小的友方卡数量
		int count = 0;
		var ownerStatus = CombatManager.Me.ownerPlayerStatusRef;
		for (int i = 0; i < startCardIndex; i++)
		{
			var cardScript = deck[i].GetComponent<CardScript>();
			if (cardScript != null && cardScript.myStatusRef == ownerStatus)
			{
				count++;
			}
		}

		friendlyInGraveAmountRef.value = count;
	}

	/// <summary>
	/// 更新 HostileCursePowerCount：统计敌方card type id为curseCardTypeId的卡拥有的Power status effect的总和
	/// </summary>
	private void UpdateHostileCursePowerCount()
	{
		if (hostileCursePowerCount == null || CombatManager.Me == null) return;

		// 如果没有设置Cursed card type ID，计数为0
		if (curseCardTypeId == null || string.IsNullOrEmpty(curseCardTypeId.value))
		{
			hostileCursePowerCount.value = 0;
			return;
		}

		var deck = CombatManager.Me.combinedDeckZone;
		var ownerStatus = CombatManager.Me.ownerPlayerStatusRef;
		int totalPower = 0;

		foreach (var cardObj in deck)
		{
			var cardScript = cardObj.GetComponent<CardScript>();
			if (cardScript == null) continue;

			// 检查是否为敌方卡且card type id匹配
			bool isHostileCard = cardScript.myStatusRef != ownerStatus;
			bool isMatchingType = cardScript.cardTypeID == curseCardTypeId?.value;

			if (isHostileCard && isMatchingType)
			{
				// 统计该卡上的Power status effect数量
				int powerCount = EnumStorage.GetStatusEffectCount(
					cardScript.myStatusEffects, 
					EnumStorage.StatusEffect.Power
				);
				totalPower += powerCount;
			}
		}

		hostileCursePowerCount.value = totalPower;
	}
}
