using System.Collections.Generic;
using System.Linq;
using DefaultNamespace.Managers;
using DefaultNamespace.SOScripts;
using UnityEngine;

namespace DefaultNamespace.Effects
{
	public class AddTempCard : EffectScript
	{
		public int cardCount = 1;
		
		[Header("Curse Card Copy")]
		[Tooltip("Cursed card type ID to copy (empty string means disabled)")]
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
		/// Copy an enemy card matching curseCardTypeID to self.
		/// If multiple enemy cards match, randomly select one.
		/// Copies will retain all original status effects.
		/// </summary>
		public void CopyEnemyCurseCardToMe()
		{
			// If curseCardTypeID is empty, do not execute
			if (curseCardTypeID == null || string.IsNullOrEmpty(curseCardTypeID.value))
			{
				Debug.LogWarning($"[{myCard.name}] CopyEnemyCurseCardToMe: curseCardTypeID is empty or null");
				return;
			}
			
			// Get all enemy cards
			List<CardScript> enemyCards = CombatFuncs.me.ReturnEnemyCardScripts();
			
			// Filter cards matching curseCardTypeID
			List<CardScript> matchingCards = enemyCards
				.Where(card => card.cardTypeID == curseCardTypeID?.value)
				.ToList();
			
			// If no matching cards, do not execute
			if (matchingCards.Count == 0)
			{
				Debug.Log($"[{myCard.name}] CopyEnemyCurseCardToMe: no enemy card with typeID '{curseCardTypeID?.value}' found");
				return;
			}
			
			// Randomly select one matching card
			CardScript selectedCard = matchingCards[Random.Range(0, matchingCards.Count)];
			GameObject cardPrefab = selectedCard.gameObject;
			
			// Save original card's status effects
			List<EnumStorage.StatusEffect> originalStatusEffects = selectedCard.myStatusEffects;
			
			// Copy selected card to self and copy status effects
			for (int i = 0; i < cardCount; i++)
			{
				GameObject newCard = CombatFuncs.me.AddCard_TargetSpecific(cardPrefab, myCardScript.myStatusRef);
				
				// Copy status effects to new card
				if (newCard != null)
				{
					CardScript newCardScript = newCard.GetComponent<CardScript>();
					if (newCardScript != null && originalStatusEffects != null)
					{
						newCardScript.myStatusEffects = new List<EnumStorage.StatusEffect>(originalStatusEffects);
					}
				}
			}
			
			// Log result
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