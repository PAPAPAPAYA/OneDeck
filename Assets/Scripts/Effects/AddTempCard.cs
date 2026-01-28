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
				// todo implement a method to return "your" or "their"
				effectResultString.value += "// [" + myCard.name + "] added 1 [" + cardToAdd.name + "] to You\n";
			}
			else // if this card belong to enemy
			{
				effectResultString.value += "// [" + myCard.name + "] added 1 [" + cardToAdd.name + "] to Enemy\n";
			}
			combatManager.Shuffle();
		}
		public void AddCardToThem(GameObject cardToAdd)
		{
			//CombatFuncs.me.AddCardInTheMiddleOfCombat(cardToAdd, false);
			CombatFuncs.me.AddCard_TargetSpecific(cardToAdd, myCardScript.theirStatusRef);
			if (myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef) // if this card belongs to player
			{
				effectResultString.value += "// [" + myCard.name + "] added 1 [" + cardToAdd.name + "] to Enemy\n";
			}
			else // if this card belong to enemy
			{
				effectResultString.value += "// [" + myCard.name + "] added 1 [" + cardToAdd.name + "] to You\n";
			}
			combatManager.Shuffle();
		}
	}
}