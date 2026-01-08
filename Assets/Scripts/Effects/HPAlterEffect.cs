using System;
using System.Collections.Generic;
using DefaultNamespace.SOScripts;
using UnityEngine;

public class HPAlterEffect : EffectScript
{ 
	public void AlterMyHP(int HPAlterAmount) // alter [my status ref]
	{
		myCardScript.myStatusRef.hp += HPAlterAmount;
		myCardScript.myStatusRef.hp = Mathf.Clamp(myCardScript.myStatusRef.hp, 0, myCardScript.myStatusRef.hpMax);
		if (HPAlterAmount < 0) // dealing dmg to self
		{
			if (myCardScript.myStatusRef == CombatManager.Me.ownerPlayerStatusRef) // player is dealing dmg to self
			{
				GameEventStorage.me.onPlayerTookDmg?.Raise(); // timepoint
				effectResultString.value += "[" + myCardScript.cardName + "] dealt [" + Mathf.Abs(HPAlterAmount) + "] damage to You\n";
			}
			else // enemy is dealing dmg to themselves
			{
				GameEventStorage.me.onEnemyTookDmg?.Raise(); // timepoint
				effectResultString.value += "[" + myCardScript.cardName + "] dealt [" + Mathf.Abs(HPAlterAmount) + "] damage to Enemy\n";
			}
		}
		else // healing
		{
			if (myCardScript.myStatusRef == CombatManager.Me.ownerPlayerStatusRef) // player is healing self
			{
				GameEventStorage.me.onPlayerHealed?.Raise();
				effectResultString.value += "[" + myCardScript.cardName + "] healed You for [" + HPAlterAmount + "]\n";
			}
			else // enemy is healing themselves
			{
				GameEventStorage.me.onEnemyHealed?.Raise();
				effectResultString.value += "[" + myCardScript.cardName + "] healed Enemy for [" + HPAlterAmount + "]\n";
			}
		}
	}

	public void AlterTheirHP(int HPAlterAmount) // alter [their status ref]
	{
		myCardScript.theirStatusRef.hp += HPAlterAmount;
		myCardScript.theirStatusRef.hp = Mathf.Clamp(myCardScript.theirStatusRef.hp, 0, myCardScript.theirStatusRef.hpMax);

		if (HPAlterAmount < 0) // dealing dmg
		{
			if (myCardScript.theirStatusRef == CombatManager.Me.ownerPlayerStatusRef) // enemy dealt dmg to player
			{
				GameEventStorage.me.onPlayerTookDmg?.Raise(); // timepoint
				effectResultString.value += "[" + myCardScript.cardName + "] dealt [" + Mathf.Abs(HPAlterAmount) + "] damage to You\n";
			}
			else // player dealt dmg to enemy
			{
				GameEventStorage.me.onEnemyTookDmg?.Raise(); // timepoint
				effectResultString.value += "[" + myCardScript.cardName + "] dealt [" + Mathf.Abs(HPAlterAmount) + "] damage to Enemy\n";
			}
		}
		else // healing
		{
			if (myCardScript.theirStatusRef == CombatManager.Me.ownerPlayerStatusRef) // enemy healed player
			{
				GameEventStorage.me.onPlayerHealed?.Raise(); // timepoint
				effectResultString.value += "[" + myCardScript.cardName + "] healed You for [" + HPAlterAmount + "]\n";
			}
			else // player healed enemy
			{
				GameEventStorage.me.onEnemyHealed?.Raise(); // timepoint
				effectResultString.value += "[" + myCardScript.cardName + "] healed Enemy for [" + HPAlterAmount + "]\n";
			}
		}
	}
}