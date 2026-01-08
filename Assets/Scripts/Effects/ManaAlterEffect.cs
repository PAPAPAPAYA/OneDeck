using System;
using DefaultNamespace.SOScripts;
using UnityEngine;

public class ManaAlterEffect : EffectScript
{
	public void AlterMyMana(int manaAlterAmount)
	{
		myCardScript.myStatusRef.mana += manaAlterAmount;
		if (manaAlterAmount < 0) // consuming mana
		{
			if (myCardScript.myStatusRef == CombatManager.Me.ownerPlayerStatusRef) // player consuming
			{
				effectResultString.value += "["+myCardScript.cardName + "] consumed [" + Mathf.Abs(manaAlterAmount) + "] mana from You\n";
			}
			else // enemy consuming
			{
				effectResultString.value += "["+myCardScript.cardName + "] consumed [" + Mathf.Abs(manaAlterAmount) + "] mana from Enemy\n";
			}
		}
		else // gaining mana
		{
			if (myCardScript.myStatusRef == CombatManager.Me.ownerPlayerStatusRef) // player gaining
			{
				effectResultString.value += "You gained [" + manaAlterAmount + "] mana from [" + myCardScript.cardName + "]\n";
			}
			else // enemy gaining
			{
				effectResultString.value += "Enemy gained [" + manaAlterAmount + "] mana from [" + myCardScript.cardName + "]\n";
			}
		}
	}
}