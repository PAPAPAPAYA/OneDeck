using System;
using System.Collections.Generic;
using DefaultNamespace.SOScripts;
using UnityEngine;

public class HPAlterEffect : EffectScript
{
	[HideInInspector]
	public int dmgAmountAlter = 0;
	[HideInInspector]
	public int healAmountAlter = 0;

	public void DecreaseMyHp(int dmgAmount)
	{
		GameEventStorage.me.beforeIDealDmg?.RaiseSpecific(myCard); // timepoint
		myCardScript.myStatusRef.hp -= dmgAmount + dmgAmountAlter;
		myCardScript.myStatusRef.hp = Mathf.Clamp(myCardScript.myStatusRef.hp, 0, myCardScript.myStatusRef.hpMax);
		CheckDmgTargets_DealingDmgToSelf(dmgAmount);
		dmgAmountAlter = 0;
	}

	public void IncreaseMyHp(int healAmount)
	{
		myCardScript.myStatusRef.hp += healAmount + healAmountAlter;
		myCardScript.myStatusRef.hp = Mathf.Clamp(myCardScript.myStatusRef.hp, 0, myCardScript.myStatusRef.hpMax);
		CheckHealTargets_HealingSelf(healAmount);
		healAmountAlter = 0;
	}

	public void DecreaseTheirHp(int dmgAmount)
	{
		GameEventStorage.me.beforeIDealDmg?.RaiseSpecific(myCard); // timepoint
		myCardScript.theirStatusRef.hp -= dmgAmount + dmgAmountAlter;
		myCardScript.theirStatusRef.hp = Mathf.Clamp(myCardScript.theirStatusRef.hp, 0, myCardScript.theirStatusRef.hpMax);
		CheckDmgTargets_DealingDmgToOpponent(dmgAmount);
		dmgAmountAlter = 0;
	}
	public void IncreaseTheirHp(int healAmount)
	{
		myCardScript.theirStatusRef.hp -= healAmount + healAmountAlter;
		myCardScript.theirStatusRef.hp = Mathf.Clamp(myCardScript.theirStatusRef.hp, 0, myCardScript.theirStatusRef.hpMax);
		CheckHealTargets_HealingOpponent(healAmount);
		healAmountAlter = 0;
	}

	// check damage from and to to raise corresponding events and show text info
	private void CheckDmgTargets_DealingDmgToOpponent(int dmgAmount)
	{
		if (myCardScript.theirStatusRef == combatManager.ownerPlayerStatusRef) // enemy dealt dmg to player
		{
			effectResultString.value += "// [" + myCardScript.cardName + "] dealt [" + (dmgAmount + dmgAmountAlter) + "] damage to You\n";
			GameEventStorage.me.onPlayerTookDmg?.Raise(); // timepoint
		}
		else // player dealt dmg to enemy
		{
			effectResultString.value += "// [" + myCardScript.cardName + "] dealt [" + (dmgAmount + dmgAmountAlter) + "] damage to Enemy\n";
			GameEventStorage.me.onEnemyTookDmg?.Raise(); // timepoint
		}
	}

	private void CheckDmgTargets_DealingDmgToSelf(int dmgAmount)
	{
		if (myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef) // player dealt dmg to player
		{
			effectResultString.value += "// [" + myCardScript.cardName + "] dealt [" + (dmgAmount + dmgAmountAlter) + "] damage to You\n";
			GameEventStorage.me.onPlayerTookDmg?.Raise(); // timepoint
		}
		else // enemy dealt dmg to enemy
		{
			effectResultString.value += "// [" + myCardScript.cardName + "] dealt [" + (dmgAmount + dmgAmountAlter) + "] damage to Enemy\n";
			GameEventStorage.me.onEnemyTookDmg?.Raise(); // timepoint
		}
	}

	// check heal from and to to raise events and show text info
	private void CheckHealTargets_HealingSelf(int healAmount)
	{
		if (myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef) // player healed player
		{
			effectResultString.value += "// [" + myCardScript.cardName + "] healed You for [" + (healAmount + healAmountAlter) + "]\n";
			GameEventStorage.me.onPlayerHealed?.Raise(); // timepoint
		}
		else // enemy healed enemy
		{
			effectResultString.value += "// [" + myCardScript.cardName + "] healed Enemy for [" + (healAmount + healAmountAlter) + "]\n";
			GameEventStorage.me.onEnemyHealed?.Raise(); // timepoint
		}
	}
	private void CheckHealTargets_HealingOpponent(int healAmount)
	{
		if (myCardScript.theirStatusRef == combatManager.ownerPlayerStatusRef) // enemy healed player
		{
			effectResultString.value += "// [" + myCardScript.cardName + "] healed You for [" + (healAmount + healAmountAlter) + "]\n";
			GameEventStorage.me.onPlayerHealed?.Raise(); // timepoint
		}
		else // player healed enemy
		{
			effectResultString.value += "// [" + myCardScript.cardName + "] healed Enemy for [" + (healAmount + healAmountAlter) + "]\n";
			GameEventStorage.me.onEnemyHealed?.Raise(); // timepoint
		}
	}
}