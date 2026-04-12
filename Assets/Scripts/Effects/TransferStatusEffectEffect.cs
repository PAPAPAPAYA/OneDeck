using System.Collections.Generic;
using DefaultNamespace.SOScripts;
using UnityEngine;

namespace DefaultNamespace.Effects
{
	public class TransferStatusEffectEffect : EffectScript
	{
		[Header("Transfer Config")]
		[Tooltip("true = 从友方转移, false = 从敌方转移")]
		public bool isFromFriendly = true;

		[Tooltip("要转移的状态效果类型")]
		public EnumStorage.StatusEffect statusEffectToTransfer;

		[Tooltip("敌方诅咒卡的Card Type ID")]
		public StringSO curseCardTypeID;

		[Header("Status Effect Config")]
		[Tooltip("状态效果解析器脚本（可选）")]
		public GameObject statusEffectResolverPrefab;

		[Tooltip("获得状态效果时播放的粒子系统（可选）")]
		public ParticleSystem statusEffectParticlePrefab;

		[Tooltip("粒子系统的Y轴偏移量")]
		public float particleYOffset = 0f;

		/// <summary>
		/// 将所有指定的状态效果从友方/敌方转移到敌方的诅咒卡上
		/// </summary>
		public void TransferAllStatusEffectToHostileCurse()
		{
			if (statusEffectToTransfer == EnumStorage.StatusEffect.None)
			{
				Debug.LogWarning("[TransferStatusEffectEffect] statusEffectToTransfer is None!");
				return;
			}

			if (curseCardTypeID == null || string.IsNullOrEmpty(curseCardTypeID.value))
			{
				Debug.LogWarning("[TransferStatusEffectEffect] curseCardTypeID is not set!");
				return;
			}

			// 查找目标诅咒卡
			CardScript targetCurseCard = FindHostileCurseCard();
			if (targetCurseCard == null)
			{
				Debug.LogWarning($"[TransferStatusEffectEffect] Cannot find hostile curse card with typeID: {curseCardTypeID.value}");
				return;
			}

			// 收集源卡牌上的指定状态效果
			List<CardScript> sourceCards = FindSourceCardsWithStatusEffect();
			if (sourceCards.Count == 0)
			{
				// 没有找到带有指定状态效果的源卡牌，直接返回
				return;
			}

			// 计算总共要转移的状态效果数量
			int totalTransferCount = 0;
			foreach (var card in sourceCards)
			{
				totalTransferCount += EnumStorage.GetStatusEffectCount(card.myStatusEffects, statusEffectToTransfer);
			}

			if (totalTransferCount <= 0)
			{
				return;
			}

			// 执行转移
			TransferStatusEffects(sourceCards, targetCurseCard, totalTransferCount);
		}

		/// <summary>
		/// 查找敌方的诅咒卡
		/// </summary>
		private CardScript FindHostileCurseCard()
		{
			foreach (var card in combatManager.combinedDeckZone)
			{
				var cardScript = card.GetComponent<CardScript>();
				if (cardScript == null) continue;

				// 跳过中立卡
				if (CombatManager.ShouldSkipEffectProcessing(cardScript)) continue;

				// 检查是否是敌人的卡且cardTypeID匹配
				if (cardScript.myStatusRef == myCardScript.theirStatusRef &&
				    cardScript.cardTypeID == curseCardTypeID.value)
				{
					return cardScript;
				}
			}
			return null;
		}

		/// <summary>
		/// 查找带有指定状态效果的源卡牌（友方或敌方）
		/// </summary>
		private List<CardScript> FindSourceCardsWithStatusEffect()
		{
			var result = new List<CardScript>();
			PlayerStatusSO targetStatusRef = isFromFriendly ? myCardScript.myStatusRef : myCardScript.theirStatusRef;

			foreach (var card in combatManager.combinedDeckZone)
			{
				var cardScript = card.GetComponent<CardScript>();
				if (cardScript == null) continue;

				// 跳过中立卡
				if (CombatManager.ShouldSkipEffectProcessing(cardScript)) continue;

				// 检查是否是目标方（友方或敌方）的卡
				if (cardScript.myStatusRef != targetStatusRef) continue;

				// 检查是否有指定的状态效果
				if (EnumStorage.GetStatusEffectCount(cardScript.myStatusEffects, statusEffectToTransfer) > 0)
				{
					result.Add(cardScript);
				}
			}

			return result;
		}

		/// <summary>
		/// 执行状态效果转移
		/// </summary>
		private void TransferStatusEffects(List<CardScript> sourceCards, CardScript targetCard, int totalCount)
		{
			// 从源卡牌移除状态效果
			foreach (var card in sourceCards)
			{
				for (int i = card.myStatusEffects.Count - 1; i >= 0; i--)
				{
					if (card.myStatusEffects[i] == statusEffectToTransfer)
					{
						card.myStatusEffects.RemoveAt(i);
					}
				}

				// 刷新视觉显示（如果是Power或Infected）
				TriggerTintRefresh(card, statusEffectToTransfer);
			}

			// 添加到目标卡
			for (int i = 0; i < totalCount; i++)
			{
				targetCard.myStatusEffects.Add(statusEffectToTransfer);
			}

			// 创建状态效果解析器
			if (statusEffectResolverPrefab != null)
			{
				for (int i = 0; i < totalCount; i++)
				{
					var resolver = Instantiate(statusEffectResolverPrefab, targetCard.transform);
					GameEventStorage.me?.onThisTagResolverAttached.RaiseSpecific(resolver);
				}
			}

			// 播放粒子效果
			if (statusEffectParticlePrefab != null)
			{
				for (int i = 0; i < totalCount; i++)
				{
					Vector3 spawnPosition = GetPhysicalCardWorldPosition(targetCard.transform) + Vector3.up * particleYOffset;
					ParticleSystem particle = Instantiate(statusEffectParticlePrefab, spawnPosition, Quaternion.identity, targetCard.transform);
					particle.Play();
				}
			}

			// 刷新目标卡的视觉显示
			TriggerTintRefresh(targetCard, statusEffectToTransfer);

			// 输出效果信息
			LogTransferEffect(sourceCards, targetCard, totalCount);

			// 刷新信息显示
			CombatInfoDisplayer.me?.RefreshDeckInfo();
		}

		/// <summary>
		/// 刷新指定卡牌的Tint效果
		/// </summary>
		private void TriggerTintRefresh(CardScript card, EnumStorage.StatusEffect effect)
		{
			if (effect != EnumStorage.StatusEffect.Infected && effect != EnumStorage.StatusEffect.Power) return;
			if (CombatUXManager.me == null) return;

			CombatUXManager.me.BuildCardScriptToPhysicalDictionary();
			var physicalCard = CombatUXManager.me.GetPhysicalCardFromLogicalCard(card);
			if (physicalCard != null)
			{
				var cardPhysObj = physicalCard.GetComponent<CardPhysObjScript>();
				if (cardPhysObj != null)
				{
					cardPhysObj.TriggerTintForStatusEffect(effect);
				}
			}
		}

		/// <summary>
		/// 获取卡牌的世界位置
		/// </summary>
		private Vector3 GetPhysicalCardWorldPosition(Transform cardTransform)
		{
			if (CombatUXManager.me != null)
			{
				var cardScript = cardTransform.GetComponent<CardScript>();
				if (cardScript != null)
				{
					CombatUXManager.me.BuildCardScriptToPhysicalDictionary();
					var physicalCard = CombatUXManager.me.GetPhysicalCardFromLogicalCard(cardScript);
					if (physicalCard != null)
					{
						return physicalCard.transform.position;
					}
				}
			}
			return cardTransform.position;
		}

		/// <summary>
		/// 记录转移效果日志
		/// </summary>
		private void LogTransferEffect(List<CardScript> sourceCards, CardScript targetCard, int totalCount)
		{
			string sourceOwner = isFromFriendly ?
				(myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? "Your" : "Enemy's") :
				(myCardScript.theirStatusRef == combatManager.ownerPlayerStatusRef ? "Your" : "Enemy's");

			string thisCardOwnerString = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ?
				"<color=#87CEEB>Your</color> [" : "<color=orange>Enemy's</color> [";
			string thisCardColor = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ?
				"#87CEEB" : "orange";
			string targetCardColor = targetCard.myStatusRef == combatManager.ownerPlayerStatusRef ?
				"#87CEEB" : "orange";

			effectResultString.value +=
				"// " + thisCardOwnerString +
				"<color=" + thisCardColor + ">" + myCard.name + "</color>] transferred " +
				"<color=yellow>" + totalCount + "</color> [" + statusEffectToTransfer + "] from " +
				"<color=" + (isFromFriendly ? "#87CEEB" : "orange") + ">" + (isFromFriendly ? "friendly" : "hostile") + "</color> cards to " +
				"<color=" + targetCardColor + ">" + targetCard.name + "</color>]\n";
		}
	}
}
