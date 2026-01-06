using System;
using System.Collections.Generic;
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

	public void AlterMyHP(int HPAlterAmount)
	{
		_myCardScript.myStatusRef.hp += HPAlterAmount;
		_myCardScript.myStatusRef.hp = Mathf.Clamp(_myCardScript.myStatusRef.hp, 0, _myCardScript.myStatusRef.hpMax);
		if (HPAlterAmount < 0) // dealing dmg to self
		{
			if (_myCardScript.myStatusRef == CombatManager.Me.ownerPlayerStatusRef) // if card owner is player, then player is dealing dmg to self
			{
				GameEventStorage.me.onPlayerTookDmg?.Raise(); // timepoint
				CombatInfoDisplayer.me.effectResultDisplay.text += "["+_myCardScript.cardName + "] dealt [" + Mathf.Abs(HPAlterAmount) + "] damage to You\n";
			}
			else // if card owner is enemy, then enemy is dealing dmg to themselves
			{
				GameEventStorage.me.onEnemyTookDmg?.Raise(); // timepoint
				CombatInfoDisplayer.me.effectResultDisplay.text += "["+_myCardScript.cardName + "] dealt [" + Mathf.Abs(HPAlterAmount) + "] damage to Enemy\n";
			}
		}
		else // healing
		{
			if (_myCardScript.myStatusRef == CombatManager.Me.ownerPlayerStatusRef) // if card owner is player, then player is healing
			{
				CombatInfoDisplayer.me.effectResultDisplay.text += "["+_myCardScript.cardName + "] healed You for [" + HPAlterAmount + "]\n";
			}
			else // if card owner is enemy, then enemy is healing themselves
			{
				CombatInfoDisplayer.me.effectResultDisplay.text += "["+_myCardScript.cardName + "] healed Enemy for [" + HPAlterAmount + "]\n";
			}
		}
	}

	public void AlterTheirHP(int HPAlterAmount)
	{
		_myCardScript.theirStatusRef.hp += HPAlterAmount;
		_myCardScript.theirStatusRef.hp = Mathf.Clamp(_myCardScript.theirStatusRef.hp, 0, _myCardScript.theirStatusRef.hpMax);
		if (HPAlterAmount < 0) // dealing dmg
		{
			if (_myCardScript.myStatusRef == CombatManager.Me.ownerPlayerStatusRef) // if card owner is player, then player is dealing dmg to enemy
			{
				GameEventStorage.me.onEnemyTookDmg?.Raise(); // timepoint
				// display info
				CombatInfoDisplayer.me.effectResultDisplay.text += "["+_myCardScript.cardName + "] dealt [" + Mathf.Abs(HPAlterAmount) + "] damage to Enemy\n";
			}
			else // if card owner is enemy, then enemy is dealing dmg to player
			{
				GameEventStorage.me.onPlayerTookDmg?.Raise(); // timepoint
				CombatInfoDisplayer.me.effectResultDisplay.text += "["+_myCardScript.cardName + "] dealt [" + Mathf.Abs(HPAlterAmount) + "] damage to You\n";
			}
		}
		else // healing the opponent
		{
			throw new NotImplementedException();
		}
	}

	// currently used by infected resolver since its effect isn't a card
	public void AlterHP(int amount, PlayerStatusSO targetPlayerStatus)
	{
		targetPlayerStatus.hp += amount;
		if (amount < 0) // dealing dmg
		{
			if (_myCardScript.myStatusRef == CombatManager.Me.ownerPlayerStatusRef) // card belongs to player
			{
				if (targetPlayerStatus == CombatManager.Me.ownerPlayerStatusRef) // if card owner is player, then player is dealing dmg to enemy
				{
					//todo implement
				}
			}
			else // card belongs to enemy
			{
				
			}
		}
		else // healing
		{
			
		}
	}
}