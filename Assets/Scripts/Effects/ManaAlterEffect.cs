using System;
using UnityEngine;

public class ManaAlterEffect : MonoBehaviour
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
	public void AlterMyMana(int manaAlterAmount)
	{
		_myCardScript.myStatusRef.mana += manaAlterAmount;
		if (manaAlterAmount < 0) // consuming mana
		{
			if (_myCardScript.myStatusRef == CombatManager.Me.ownerPlayerStatusRef) // player consuming
			{
				CombatInfoDisplayer.me.effectResultDisplay.text += "["+_myCardScript.cardName + "] consumed [" + Mathf.Abs(manaAlterAmount) + "] mana from You\n";
			}
			else // enemy consuming
			{
				CombatInfoDisplayer.me.effectResultDisplay.text += "["+_myCardScript.cardName + "] consumed [" + Mathf.Abs(manaAlterAmount) + "] mana from Enemy\n";
			}
		}
		else // gaining mana
		{
			if (_myCardScript.myStatusRef == CombatManager.Me.ownerPlayerStatusRef) // player gaining
			{
				CombatInfoDisplayer.me.effectResultDisplay.text += "You gained [" + manaAlterAmount + "] mana from [" + _myCardScript.cardName + "]\n";
			}
			else // enemy gaining
			{
				CombatInfoDisplayer.me.effectResultDisplay.text += "Enemy gained [" + manaAlterAmount + "] mana from [" + _myCardScript.cardName + "]\n";
			}
		}
	}
}