using DefaultNamespace.Managers;
using UnityEngine;

namespace DefaultNamespace.Effects
{
	public class AddTempCard : EffectScript
	{
		public void AddCardToMe(GameObject cardToAdd)
		{
			//CombatFuncs.me.AddCardInTheMiddleOfCombat(cardToAdd, true);
			CombatFuncs.me.AddCard_TargetSpecific(cardToAdd, myCardScript.myStatusRef);
			if (myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef) // if this card belongs to player
			{
				effectResultString.value += "// [<color=#87CEEB>" + myCard.name + "</color>] added <color=yellow>1</color> [<color=#87CEEB>" + cardToAdd.name + "</color>] to <color=#87CEEB>You</color>\n";
			}
			else // if this card belong to enemy
			{
				effectResultString.value += "// [<color=orange>" + myCard.name + "</color>] added <color=yellow>1</color> [<color=orange>" + cardToAdd.name + "</color>] to <color=orange>Enemy</color>\n";
			}
			//combatManager.Shuffle();
		}
		public void AddCardToThem(GameObject cardToAdd)
		{
			//CombatFuncs.me.AddCardInTheMiddleOfCombat(cardToAdd, false);
			CombatFuncs.me.AddCard_TargetSpecific(cardToAdd, myCardScript.theirStatusRef);
			if (myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef) // if this card belongs to player
			{
				effectResultString.value += "// [<color=#87CEEB>" + myCard.name + "</color>] added <color=yellow>1</color> [<color=orange>" + cardToAdd.name + "</color>] to <color=orange>Enemy</color>\n";
			}
			else // if this card belong to enemy
			{
				effectResultString.value += "// [<color=orange>" + myCard.name + "</color>] added <color=yellow>1</color> [<color=#87CEEB>" + cardToAdd.name + "</color>] to <color=#87CEEB>You</color>\n";
			}
			//combatManager.Shuffle();
		}
	}
}