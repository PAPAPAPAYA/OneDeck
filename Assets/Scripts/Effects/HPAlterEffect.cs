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
		if (HPAlterAmount < 0)
		{
			
		}
	}

	public void AlterTheirHP(int HPAlterAmount)
	{
		_myCardScript.theirStatusRef.hp += HPAlterAmount;
		if (HPAlterAmount < 0) // dealing dmg
		{
			if (_myCardScript.myStatusRef == CombatManager.Me.ownerPlayerStatusRef) // if card owner is player, then player is dealing dmg to enemy
			{
				GameEventStorage.me.onPlayerDealtDmgToEnemy?.Raise(); // timepoint
				// display info
				CombatInfoDisplayer.me.effectResultDisplay.text += "["+_myCardScript.cardName + "] dealt [" + Mathf.Abs(HPAlterAmount) + "] damage to Enemy";
			}
			else // if card owner is enemy, then enemy is dealing dmg to player
			{
			
			}
		}
		else // healing
		{
			
		}
	}

	// currently used by infected resolver since its effect isn't a card
	public void AlterHP(int amount, PlayerStatusSO playerStatus)
	{
		playerStatus.hp += amount;
		if (amount < 0)
		{
			LingeringEffectManager.Me?.InvokeOnDmgDealtEvent(_myCardScript.myStatusRef,
				playerStatus); // TIMEPOINT
		}
	}
}