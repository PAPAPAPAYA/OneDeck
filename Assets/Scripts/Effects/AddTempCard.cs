using DefaultNamespace.Managers;
using UnityEngine;

namespace DefaultNamespace.Effects
{
	public class AddTempCard : EffectScript
	{
		//todo: bug: 
		// 1. didn't implement to show text
		// 2. if card's owner is changed, cards are not added to correct owner's deck
		public void AddCardToMe(GameObject cardToAdd)
		{
			//CombatFuncs.me.AddCardInTheMiddleOfCombat(cardToAdd, true);
			CombatFuncs.me.AddCard_TargetSpecific(cardToAdd, myCardScript.myStatusRef);
			if (myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef) // if this card belongs to player
			{
				effectResultString.value += "// [" + myCardScript.cardName + "] added 1 [" + cardToAdd.GetComponent<CardScript>().cardName + "] to You\n";
			}
			else // if this card belong to enemy
			{
				effectResultString.value += "// [" + myCardScript.cardName + "] added 1 [" + cardToAdd.GetComponent<CardScript>().cardName + "] to Enemy\n";
			}
			combatManager.Shuffle();
		}
		public void AddCardToThem(GameObject cardToAdd)
		{
			//CombatFuncs.me.AddCardInTheMiddleOfCombat(cardToAdd, false);
			CombatFuncs.me.AddCard_TargetSpecific(cardToAdd, myCardScript.theirStatusRef);
			if (myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef) // if this card belongs to player
			{
				effectResultString.value += "// [" + myCardScript.cardName + "] added 1 [" + cardToAdd.GetComponent<CardScript>().cardName + "] to Enemy\n";
			}
			else // if this card belong to enemy
			{
				effectResultString.value += "// [" + myCardScript.cardName + "] added 1 [" + cardToAdd.GetComponent<CardScript>().cardName + "] to You\n";
			}
			combatManager.Shuffle();
		}
	}
}