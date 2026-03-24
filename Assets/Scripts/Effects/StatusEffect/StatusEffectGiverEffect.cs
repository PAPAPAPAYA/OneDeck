using System.Collections.Generic;
using UnityEngine;

namespace DefaultNamespace.Effects
{
	public class StatusEffectGiverEffect : EffectScript
	{
		[Header("Status Effect Related Refs")]
		public GameObject myStatusEffectResolverScript;
		public bool canStatusEffectBeStacked = false;
		[Tooltip("if this is none, then won't run give status effect")]
		public EnumStorage.StatusEffect statusEffectToGive;
		[Tooltip("this is used for GiveStatusEffectBasedOnStatusEffectCount()")]
		public EnumStorage.StatusEffect statusEffectToCount;
		public bool spreadEvenly = false;
		[Tooltip("only applies to GiveStatusEffect(): whose cards the status effect will be given to")]
		public EnumStorage.TargetType target; // whose cards the status effect will be given to
		[Tooltip("if true, will include the card itself in reveal zone when giving status effect")]
		public bool includeSelf = false;
		
		[Header("Particle System")]
		[Tooltip("获得状态效果时播放的粒子系统预制体")]
		public ParticleSystem statusEffectParticlePrefab;
		[Tooltip("粒子系统的Y轴偏移量")]
		public float particleYOffset = 0f;

		/// <summary>
		/// 给自身卡片添加状态效果
		/// </summary>
		/// <param name="amount">添加的状态效果层数</param>
		public virtual void GiveSelfStatusEffect(int amount)
		{
			for (int i = 0; i < amount; i++)
			{
				// give status effect
				myCardScript.myStatusEffects.Add(statusEffectToGive);
				// display effect info
				var thisCardOwnerString = CombatInfoDisplayer.me.ReturnCardOwnerInfo(myCardScript.myStatusRef);
				string thisCardColor = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? "#87CEEB" : "orange";
				effectResultString.value +=
					"// " + thisCardOwnerString + // tag giver owner card
					" [<color=" + thisCardColor + ">" + myCard.name + "</color>] gave" + // tag giver card name 
					" it" + // status effect receiver card
					" <color=yellow>1</color> [" + statusEffectToGive + "]\n"; // status effect
				// make statue effect resolver
				if (myStatusEffectResolverScript == null) continue;
				var tagResolver = Instantiate(myStatusEffectResolverScript, myCard.transform);
				GameEventStorage.me.onThisTagResolverAttached.RaiseSpecific(tagResolver);
				// play particle effect at card position
				PlayStatusEffectParticle(myCard.transform);
				// trigger tint effect
				TriggerTintForStatusEffect(myCardScript, statusEffectToGive);
			}
		}
		
		/// <summary>
		/// 给目标卡片添加状态效果
		/// 目标由target字段决定：Me(自己), Them(敌人), Random(随机)
		/// </summary>
		/// <param name="amount">添加的状态效果层数</param>
		public virtual void GiveStatusEffect(int amount)
		{
			if (statusEffectToGive == EnumStorage.StatusEffect.None) return;
			var cardsToGiveTag = new List<GameObject>();
			UtilityFuncManagerScript.CopyGameObjectList(combatManager.combinedDeckZone, cardsToGiveTag, true);
			// [已废弃] 墓地机制已移除 // UtilityFuncManagerScript.CopyGameObjectList(combatManager.graveZone, cardsToGiveTag, false);
			if (includeSelf)
			{
				cardsToGiveTag.Add(myCard);
			}
			cardsToGiveTag = UtilityFuncManagerScript.ShuffleList(cardsToGiveTag);
			for (var i = cardsToGiveTag.Count - 1; i >= 0; i--)
			{
				var targetCardScript = cardsToGiveTag[i].GetComponent<CardScript>();
				// 跳过中立卡和 Start Card
				if (CombatManager.ShouldSkipEffectProcessing(targetCardScript))
				{
					cardsToGiveTag.RemoveAt(i);
					continue;
				}
				if (targetCardScript.myStatusRef != myCardScript.myStatusRef)
				{
					if (target == EnumStorage.TargetType.Me) cardsToGiveTag.RemoveAt(i);
				}
				else
				{
					if (target == EnumStorage.TargetType.Them) cardsToGiveTag.RemoveAt(i);
				}
			}
			if (!canStatusEffectBeStacked)
			{
				for (var i = cardsToGiveTag.Count - 1; i >= 0; i--)
				{
					if (cardsToGiveTag[i].GetComponent<CardScript>().myStatusEffects.Contains(statusEffectToGive))
					{
						cardsToGiveTag.RemoveAt(i);
					}
				}
			}
			if (cardsToGiveTag.Count <= 0) return;
			if (spreadEvenly)
			{
				amount = Mathf.Clamp(amount, 0, cardsToGiveTag.Count);
			}

			for (var i = 0; i < amount; i++)
			{
				CardScript targetCardScript;
				if (spreadEvenly)
				{
					targetCardScript = cardsToGiveTag[i].GetComponent<CardScript>();
				}
				else
				{
					targetCardScript = cardsToGiveTag[Random.Range(0, cardsToGiveTag.Count)].GetComponent<CardScript>();
				}
				targetCardScript.myStatusEffects.Add(statusEffectToGive);
				var targetCardOwnerString = targetCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? "<color=#87CEEB>Your</color> [" : "<color=orange>Enemy's</color> [";
				var thisCardOwnerString = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? "<color=#87CEEB>Your</color> [" : "<color=orange>Enemy's</color> [";
				string thisCardColor = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? "#87CEEB" : "orange";
				string targetCardColor = targetCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? "#87CEEB" : "orange";
				effectResultString.value +=
					"// " + thisCardOwnerString + // tag giver owner card
					"<color=" + thisCardColor + ">" + myCard.name + "</color>] gave " + // tag giver card name 
					targetCardOwnerString + // status effect receiver card owner
					"<color=" + targetCardColor + ">" + targetCardScript.gameObject.name + "</color>] " + // status effect receiver card
					"<color=yellow>1</color> [" + statusEffectToGive + "]\n"; // status effect
				if (myStatusEffectResolverScript == null) continue;
				var tagResolver = Instantiate(myStatusEffectResolverScript, targetCardScript.transform);
				GameEventStorage.me.onThisTagResolverAttached.RaiseSpecific(tagResolver);
				// play particle effect at card position
				PlayStatusEffectParticle(targetCardScript.transform);
				// trigger tint effect
				TriggerTintForStatusEffect(targetCardScript, statusEffectToGive);
			}
			CombatInfoDisplayer.me.RefreshDeckInfo();
		}

		/// <summary>
		/// 在指定位置播放状态效果粒子系统
		/// 使用物理卡牌的实际世界位置，而非逻辑卡牌的位置
		/// </summary>
		/// <param name="cardTransform">逻辑卡牌Transform</param>
		protected virtual void PlayStatusEffectParticle(Transform cardTransform)
		{
			if (statusEffectParticlePrefab == null) return;
			
			// 获取物理卡牌的实际位置
			Vector3 spawnPosition = GetPhysicalCardWorldPosition(cardTransform) + Vector3.up * particleYOffset;
			ParticleSystem particleInstance = Instantiate(statusEffectParticlePrefab, spawnPosition, Quaternion.identity, cardTransform);
			particleInstance.Play();
		}
		
		/// <summary>
		/// 获取卡牌的世界位置
		/// 优先使用物理卡牌的位置，如果找不到则使用逻辑卡牌位置
		/// </summary>
		/// <param name="cardTransform">逻辑卡牌Transform</param>
		/// <returns>世界位置</returns>
		protected virtual Vector3 GetPhysicalCardWorldPosition(Transform cardTransform)
		{
			// 尝试通过 CombatUXManager 获取物理卡牌
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
			
			// 回退：使用逻辑卡牌位置（可能不准确）
			return cardTransform.position;
		}

		/// <summary>
		/// 触发卡片的 tint 效果
		/// </summary>
		protected virtual void TriggerTintForStatusEffect(CardScript targetCard, EnumStorage.StatusEffect effect)
		{
			// 只处理 Infected 和 Power 两种 tint
			if (effect != EnumStorage.StatusEffect.Infected && effect != EnumStorage.StatusEffect.Power)
				return;

			if (CombatUXManager.me == null) return;

			CombatUXManager.me.BuildCardScriptToPhysicalDictionary();
			var physicalCard = CombatUXManager.me.GetPhysicalCardFromLogicalCard(targetCard);
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
		/// 根据卡上指定status effect的数量，给予statusEffectToGive指定的status effect
		/// 目标由target字段决定：Me(自己), Them(敌人), Random(随机)
		/// </summary>
		public void GiveStatusEffectBasedOnStatusEffectCount()
		{
			// 统计卡上指定status effect的数量
			int count = 0;
			foreach (var effect in myCardScript.myStatusEffects)
			{
				if (effect == statusEffectToCount)
				{
					count++;
				}
			}
			
			// 如果数量小于等于0或要给予的status effect为None，则不执行
			if (count <= 0 || statusEffectToGive == EnumStorage.StatusEffect.None) return;
			
			// 根据统计的数量给予status effect
			GiveStatusEffect(count);
		}
	}
}