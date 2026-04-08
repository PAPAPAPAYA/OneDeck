using System.Collections.Generic;
using DefaultNamespace.Managers;
using UnityEngine;

namespace DefaultNamespace.Effects
{
	public class CurseEffect : EffectScript
	{
		[Header("Curse Config")]
		[Tooltip("诅咒目标卡牌的类型ID")]
		public string cardTypeID;
		
		[Tooltip("需要生成的卡牌预制体（当牌组中没有目标卡时使用）")]
		public GameObject cardPrefab;
		
		[Header("Status Effect Config")]
		[Tooltip("状态效果解析器脚本（可选）")]
		public GameObject statusEffectResolverPrefab;
		
		[Tooltip("获得状态效果时播放的粒子系统（可选）")]
		public ParticleSystem statusEffectParticlePrefab;
		
		[Tooltip("粒子系统的Y轴偏移量")]
		public float particleYOffset = 0f;

		/// <summary>
		/// 增强诅咒：如果combinedDeckZone中没有敌人的指定cardTypeID的卡，
		/// 则生成一张该类型的卡，然后给这张敌人的卡赋予power状态效果
		/// </summary>
		/// <param name="powerAmount">赋予的power层数</param>
		public void EnhanceCurse(int powerAmount)
		{
			if (string.IsNullOrEmpty(cardTypeID))
			{
				Debug.LogWarning("[CurseEffect] cardTypeID is not set!");
				return;
			}

			if (powerAmount <= 0)
			{
				return;
			}

			// 查找combinedDeckZone中敌人的指定cardTypeID的卡
			CardScript targetCard = FindEnemyCardWithTypeID(cardTypeID);

			// 如果没有找到，生成一张
			if (targetCard == null)
			{
				if (cardPrefab == null)
				{
					Debug.LogWarning($"[CurseEffect] Card prefab is not set! Cannot create card with typeID: {cardTypeID}");
					return;
				}
				targetCard = CreateEnemyCard(cardPrefab);
			}

			// 给目标卡赋予power状态效果
			if (targetCard != null)
			{
				ApplyPowerToCardWithProjectile(targetCard, powerAmount);
			}
		}

		/// <summary>
		/// 在combinedDeckZone中查找敌人的指定cardTypeID的卡
		/// </summary>
		private CardScript FindEnemyCardWithTypeID(string typeID)
		{
			foreach (var card in combatManager.combinedDeckZone)
			{
				var cardScript = card.GetComponent<CardScript>();
				if (cardScript == null) continue;
				
				// 跳过中立卡
				if (CombatManager.ShouldSkipEffectProcessing(cardScript)) continue;
				
				// 检查是否是敌人的卡且cardTypeID匹配
				if (cardScript.myStatusRef == myCardScript.theirStatusRef && 
				    cardScript.cardTypeID == typeID)
				{
					return cardScript;
				}
			}
			return null;
		}

		/// <summary>
		/// 为敌人生成一张卡牌
		/// </summary>
		private CardScript CreateEnemyCard(GameObject cardToCreate)
		{
			CombatFuncs.me.AddCard_TargetSpecific(cardToCreate, myCardScript.theirStatusRef);
			
			// 获取刚添加的卡（在combinedDeckZone的第一个位置）
			if (combatManager.combinedDeckZone.Count > 0)
			{
				var newCard = combatManager.combinedDeckZone[0];
				var newCardScript = newCard.GetComponent<CardScript>();
				
				// 输出效果信息
				var thisCardOwnerString = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? 
					"<color=#87CEEB>Your</color> [" : "<color=orange>Enemy's</color> [";
				string thisCardColor = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? 
					"#87CEEB" : "orange";
				
				effectResultString.value +=
					"// " + thisCardOwnerString +
					"<color=" + thisCardColor + ">" + myCard.name + "</color>] cursed and created " +
					"<color=orange>Enemy's</color> [<color=orange>" + newCard.name + "</color>]\n";
				
				return newCardScript;
			}
			return null;
		}

		/// <summary>
		/// 使用投射物动画给指定卡牌赋予power状态效果
		/// 特效飞到目标后才执行实际效果
		/// </summary>
		public void ApplyPowerToCardWithProjectile(CardScript targetCard, int amount)
		{
			if (targetCard == null || amount <= 0) return;

			var targetCards = new List<CardScript> { targetCard };
			
			CombatUXManager.me?.PlayMultiStatusEffectProjectile(
				myCard,
				targetCards,
				(card) => ApplyPowerToCardInternal(card, amount),
				null
			);
		}

		/// <summary>
		/// 内部方法：实际执行添加Power效果（用于投射物动画回调）
		/// </summary>
		private void ApplyPowerToCardInternal(CardScript targetCard, int amount)
		{
			// 添加power状态效果
			for (int i = 0; i < amount; i++)
			{
				targetCard.myStatusEffects.Add(EnumStorage.StatusEffect.Power);
			}

			// 输出效果信息
			var targetCardOwnerString = targetCard.myStatusRef == combatManager.ownerPlayerStatusRef ? 
				"<color=#87CEEB>Your</color> [" : "<color=orange>Enemy's</color> [";
			var thisCardOwnerString = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? 
				"<color=#87CEEB>Your</color> [" : "<color=orange>Enemy's</color> [";
			string thisCardColor = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? 
				"#87CEEB" : "orange";
			string targetCardColor = targetCard.myStatusRef == combatManager.ownerPlayerStatusRef ? 
				"#87CEEB" : "orange";

			effectResultString.value +=
				"// " + thisCardOwnerString +
				"<color=" + thisCardColor + ">" + myCard.name + "</color>] gave " +
				targetCardOwnerString +
				"<color=" + targetCardColor + ">" + targetCard.gameObject.name + "</color>] " +
				"<color=yellow>" + amount + "</color> [Power]\n";

			// 创建状态效果解析器
			if (statusEffectResolverPrefab != null)
			{
				for (int i = 0; i < amount; i++)
				{
					var resolver = Instantiate(statusEffectResolverPrefab, targetCard.transform);
					GameEventStorage.me.onThisTagResolverAttached.RaiseSpecific(resolver);
				}
			}

			// 播放粒子效果
			if (statusEffectParticlePrefab != null)
			{
				for (int i = 0; i < amount; i++)
				{
					Vector3 spawnPosition = GetPhysicalCardWorldPosition(targetCard.transform) + Vector3.up * particleYOffset;
					ParticleSystem particle = Instantiate(statusEffectParticlePrefab, spawnPosition, Quaternion.identity, targetCard.transform);
					particle.Play();
				}
			}

			// 触发tint效果
			TriggerTintForPower(targetCard);
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
		/// 触发Power状态的tint效果
		/// </summary>
		private void TriggerTintForPower(CardScript targetCard)
		{
			if (CombatUXManager.me == null) return;

			CombatUXManager.me.BuildCardScriptToPhysicalDictionary();
			var physicalCard = CombatUXManager.me.GetPhysicalCardFromLogicalCard(targetCard);
			if (physicalCard != null)
			{
				var cardPhysObj = physicalCard.GetComponent<CardPhysObjScript>();
				if (cardPhysObj != null)
				{
					cardPhysObj.TriggerTintForStatusEffect(EnumStorage.StatusEffect.Power);
				}
			}
		}
	}
}
