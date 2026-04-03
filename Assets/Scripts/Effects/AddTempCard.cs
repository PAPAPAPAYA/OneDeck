using DefaultNamespace.Managers;
using UnityEngine;

namespace DefaultNamespace.Effects
{
	public class AddTempCard : EffectScript
	{
		public int cardCount = 1;

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
	}
}