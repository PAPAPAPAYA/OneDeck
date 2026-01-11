using System;
using System.Collections.Generic;
using DefaultNamespace.SOScripts;
using UnityEngine;

public class HPAlterEffect : EffectScript
{
	public int dmgAmountAlter = 0;
	public int healAmountAlter = 0;

	public void DecreaseMyHp(int dmgAmount)
	{
		GameEventStorage.me.beforeIDealDmg?.RaiseSpecific(myCard); // timepoint
		myCardScript.myStatusRef.hp -= dmgAmount + dmgAmountAlter;
		myCardScript.myStatusRef.hp = Mathf.Clamp(myCardScript.myStatusRef.hp, 0, myCardScript.myStatusRef.hpMax);
		CheckDmgTargets(dmgAmount);
	}

	public void IncreaseMyHp(int healAmount)
	{
		myCardScript.myStatusRef.hp += healAmount + healAmountAlter;
		myCardScript.myStatusRef.hp = Mathf.Clamp(myCardScript.myStatusRef.hp, 0, myCardScript.myStatusRef.hpMax);
		CheckHealTargets(healAmount);
	}

	public void DecreaseTheirHp(int dmgAmount)
	{
		GameEventStorage.me.beforeIDealDmg?.RaiseSpecific(myCard); // timepoint
		myCardScript.theirStatusRef.hp -= dmgAmount + dmgAmountAlter;
		myCardScript.theirStatusRef.hp = Mathf.Clamp(myCardScript.theirStatusRef.hp, 0, myCardScript.theirStatusRef.hpMax);
		CheckDmgTargets(dmgAmount);
	}
	public void IncreaseTheirHp(int healAmount)
	{
		myCardScript.theirStatusRef.hp -= healAmount + healAmountAlter;
		myCardScript.theirStatusRef.hp = Mathf.Clamp(myCardScript.theirStatusRef.hp, 0, myCardScript.theirStatusRef.hpMax);
		CheckHealTargets(healAmount);
	}

	private void CheckDmgTargets(int dmgAmount)
	{
		if (myCardScript.theirStatusRef == combatManager.ownerPlayerStatusRef) // enemy dealt dmg to player
		{
			GameEventStorage.me.onPlayerTookDmg?.Raise(); // timepoint
			effectResultString.value += "// [" + myCardScript.cardName + "] dealt [" + Mathf.Abs(dmgAmount) + "] damage to You\n";
		}
		else // player dealt dmg to enemy
		{
			GameEventStorage.me.onEnemyTookDmg?.Raise(); // timepoint
			effectResultString.value += "// [" + myCardScript.cardName + "] dealt [" + Mathf.Abs(dmgAmount) + "] damage to Enemy\n";
		}
	}

	private void CheckHealTargets(int healAmount)
	{
		if (myCardScript.theirStatusRef == combatManager.ownerPlayerStatusRef) // enemy healed player
		{
			GameEventStorage.me.onPlayerHealed?.Raise(); // timepoint
			effectResultString.value += "// [" + myCardScript.cardName + "] healed You for [" + healAmount + "]\n";
		}
		else // player healed enemy
		{
			GameEventStorage.me.onEnemyHealed?.Raise(); // timepoint
			effectResultString.value += "// [" + myCardScript.cardName + "] healed Enemy for [" + healAmount + "]\n";
		}
	}
}