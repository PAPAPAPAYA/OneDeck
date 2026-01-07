using System;
using System.Collections.Generic;
using DefaultNamespace.SOScripts;
using UnityEngine;

public class HPAlterEffect : MonoBehaviour
{
	#region GET MY CARD SCRIPT
	private CardScript _myCardScript;

	private void OnEnable()
	{
		if (GetComponentInParent<CardScript>())
		{
			_myCardScript = GetComponentInParent<CardScript>();
		}
	}
	#endregion
	public StringSO effectResultString;

	public void AlterMyHP(int HPAlterAmount) // alter [my status ref]
	{
		_myCardScript.myStatusRef.hp += HPAlterAmount;
		_myCardScript.myStatusRef.hp = Mathf.Clamp(_myCardScript.myStatusRef.hp, 0, _myCardScript.myStatusRef.hpMax);
		if (HPAlterAmount < 0) // dealing dmg to self
		{
			if (_myCardScript.myStatusRef == CombatManager.Me.ownerPlayerStatusRef) // player is dealing dmg to self
			{
				GameEventStorage.me.onPlayerTookDmg?.Raise(); // timepoint
				effectResultString.value += "[" + _myCardScript.cardName + "] dealt [" + Mathf.Abs(HPAlterAmount) + "] damage to You\n";
			}
			else // enemy is dealing dmg to themselves
			{
				GameEventStorage.me.onEnemyTookDmg?.Raise(); // timepoint
				effectResultString.value += "[" + _myCardScript.cardName + "] dealt [" + Mathf.Abs(HPAlterAmount) + "] damage to Enemy\n";
			}
		}
		else // healing
		{
			if (_myCardScript.myStatusRef == CombatManager.Me.ownerPlayerStatusRef) // player is healing self
			{
				GameEventStorage.me.onPlayerHealed?.Raise();
				effectResultString.value += "[" + _myCardScript.cardName + "] healed You for [" + HPAlterAmount + "]\n";
			}
			else // enemy is healing themselves
			{
				GameEventStorage.me.onEnemyHealed?.Raise();
				effectResultString.value += "[" + _myCardScript.cardName + "] healed Enemy for [" + HPAlterAmount + "]\n";
			}
		}
	}

	public void AlterTheirHP(int HPAlterAmount) // alter [their status ref]
	{
		_myCardScript.theirStatusRef.hp += HPAlterAmount;
		_myCardScript.theirStatusRef.hp = Mathf.Clamp(_myCardScript.theirStatusRef.hp, 0, _myCardScript.theirStatusRef.hpMax);

		if (HPAlterAmount < 0) // dealing dmg
		{
			if (_myCardScript.theirStatusRef == CombatManager.Me.ownerPlayerStatusRef) // enemy dealt dmg to player
			{
				GameEventStorage.me.onPlayerTookDmg?.Raise(); // timepoint
				effectResultString.value += "[" + _myCardScript.cardName + "] dealt [" + Mathf.Abs(HPAlterAmount) + "] damage to You\n";
			}
			else // player dealt dmg to enemy
			{
				GameEventStorage.me.onEnemyTookDmg?.Raise(); // timepoint
				effectResultString.value += "[" + _myCardScript.cardName + "] dealt [" + Mathf.Abs(HPAlterAmount) + "] damage to Enemy\n";
			}
		}
		else // healing
		{
			if (_myCardScript.theirStatusRef == CombatManager.Me.ownerPlayerStatusRef) // enemy healed player
			{
				GameEventStorage.me.onPlayerHealed?.Raise(); // timepoint
				effectResultString.value += "[" + _myCardScript.cardName + "] healed You for [" + HPAlterAmount + "]\n";
			}
			else // player healed enemy
			{
				GameEventStorage.me.onEnemyHealed?.Raise(); // timepoint
				effectResultString.value += "[" + _myCardScript.cardName + "] healed Enemy for [" + HPAlterAmount + "]\n";
			}
		}
	}
}