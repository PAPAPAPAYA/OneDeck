using System.Collections.Generic;
using System.Linq;
using DefaultNamespace.Managers;
using UnityEngine;

namespace DefaultNamespace.Effects
{
	public class AddTempCard : EffectScript
	{
		public int cardCount = 1;
		
		[Header("Curse Card Copy")]
		[Tooltip("要复制的Cursed card type ID（空字符串表示不启用此功能）")]
		public StringSO curseCardTypeID;

		public void AddCardToMe(GameObject cardToAdd)
		{
			for (int i = 0; i < cardCount; i++)
			{
				CombatFuncs.me.AddCard_TargetSpecific(cardToAdd, myCardScript.myStatusRef);
			}
			if (myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef) // if this card belongs to player
			{
				effectResultString.value += "// [<color=#87CEEB>" + myCard.name + "</color>] added <color=yellow>" + cardCount + "</color> [<color=#87CEEB>" + cardToAdd.name + "</color>] to <color=#87CEEB>You</color>\n";
			}
			else // if this card belong to enemy
			{
				effectResultString.value += "// [<color=orange>" + myCard.name + "</color>] added <color=yellow>" + cardCount + "</color> [<color=orange>" + cardToAdd.name + "</color>] to <color=orange>Enemy</color>\n";
			}
		}

		public void AddCardToThem(GameObject cardToAdd)
		{
			for (int i = 0; i < cardCount; i++)
			{
				CombatFuncs.me.AddCard_TargetSpecific(cardToAdd, myCardScript.theirStatusRef);
			}
			if (myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef) // if this card belongs to player
			{
				effectResultString.value += "// [<color=#87CEEB>" + myCard.name + "</color>] added <color=yellow>" + cardCount + "</color> [<color=orange>" + cardToAdd.name + "</color>] to <color=orange>Enemy</color>\n";
			}
			else // if this card belong to enemy
			{
				effectResultString.value += "// [<color=orange>" + myCard.name + "</color>] added <color=yellow>" + cardCount + "</color> [<color=#87CEEB>" + cardToAdd.name + "</color>] to <color=#87CEEB>You</color>\n";
			}
		}

		public void AddSelfToMe()
		{
			for (int i = 0; i < cardCount; i++)
			{
				GameObject selfCopy = Instantiate(myCard);
				CombatFuncs.me.AddCard_TargetSpecific(selfCopy, myCardScript.myStatusRef);
			}
			if (myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef) // if this card belongs to player
			{
				effectResultString.value += "// [<color=#87CEEB>" + myCard.name + "</color>] added <color=yellow>" + cardCount + "</color> [<color=#87CEEB>" + myCard.name + "</color>] to <color=#87CEEB>You</color>\n";
			}
			else // if this card belong to enemy
			{
				effectResultString.value += "// [<color=orange>" + myCard.name + "</color>] added <color=yellow>" + cardCount + "</color> [<color=orange>" + myCard.name + "</color>] to <color=orange>Enemy</color>\n";
			}
		}

		public void AddSelfToThem()
		{
			for (int i = 0; i < cardCount; i++)
			{
				GameObject selfCopy = Instantiate(myCard);
				CombatFuncs.me.AddCard_TargetSpecific(selfCopy, myCardScript.theirStatusRef);
			}
			if (myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef) // if this card belongs to player
			{
				effectResultString.value += "// [<color=#87CEEB>" + myCard.name + "</color>] added <color=yellow>" + cardCount + "</color> [<color=orange>" + myCard.name + "</color>] to <color=orange>Enemy</color>\n";
			}
			else // if this card belong to enemy
			{
				effectResultString.value += "// [<color=orange>" + myCard.name + "</color>] added <color=yellow>" + cardCount + "</color> [<color=#87CEEB>" + myCard.name + "</color>] to <color=#87CEEB>You</color>\n";
			}
		}
		
		/// <summary>
		/// 复制一张敌方的符合 curseCardTypeID 的卡到己方
		/// 如果敌方有多张符合条件的卡，随机选择一张
		/// 复制时会保留原卡的所有 status effects
		/// </summary>
		public void CopyEnemyCurseCardToMe()
		{
			// 如果 curseCardTypeID 为空，则不执行
			if (curseCardTypeID == null || string.IsNullOrEmpty(curseCardTypeID.value))
			{
				Debug.LogWarning($"[{myCard.name}] CopyEnemyCurseCardToMe: curseCardTypeID is empty or null");
				return;
			}
			
			// 获取敌方所有卡片
			List<CardScript> enemyCards = CombatFuncs.me.ReturnEnemyCardScripts();
			
			// 筛选出符合 curseCardTypeID 的卡
			List<CardScript> matchingCards = enemyCards
				.Where(card => card.cardTypeID == curseCardTypeID?.value)
				.ToList();
			
			// 如果没有符合条件的卡，则不执行
			if (matchingCards.Count == 0)
			{
				Debug.Log($"[{myCard.name}] CopyEnemyCurseCardToMe: no enemy card with typeID '{curseCardTypeID?.value}' found");
				return;
			}
			
			// 随机选择一张符合条件的卡
			CardScript selectedCard = matchingCards[Random.Range(0, matchingCards.Count)];
			GameObject cardPrefab = selectedCard.gameObject;
			
			// 保存原卡的 status effects
			List<EnumStorage.StatusEffect> originalStatusEffects = selectedCard.myStatusEffects;
			
			// 复制选中的卡到己方，并复制 status effects
			for (int i = 0; i < cardCount; i++)
			{
				GameObject newCard = CombatFuncs.me.AddCard_TargetSpecific(cardPrefab, myCardScript.myStatusRef);
				
				// 复制 status effects 到新卡
				if (newCard != null)
				{
					CardScript newCardScript = newCard.GetComponent<CardScript>();
					if (newCardScript != null && originalStatusEffects != null)
					{
						newCardScript.myStatusEffects = new List<EnumStorage.StatusEffect>(originalStatusEffects);
					}
				}
			}
			
			// 记录日志
			if (myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef)
			{
				effectResultString.value += $"// [<color=#87CEEB>{myCard.name}</color>] copied <color=yellow>{cardCount}</color> [<color=orange>{cardPrefab.name}</color>] from Enemy to <color=#87CEEB>You</color>\n";
			}
			else
			{
				effectResultString.value += $"// [<color=orange>{myCard.name}</color>] copied <color=yellow>{cardCount}</color> [<color=#87CEEB>{cardPrefab.name}</color>] from You to <color=orange>Enemy</color>\n";
			}
		}
	}
}