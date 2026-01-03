using System.Collections.Generic;
using UnityEngine;

public class HPAlterEffect : MonoBehaviour
{
    #region GET MY CARD SCRIPT

    private CardScript _myCardScript;

    private void OnEnable()
    {
        if (GetComponent<CardScript>())
        {
            _myCardScript = GetComponent<CardScript>();
        }
    }

    #endregion
    
    public void AlterMyHP(int HPAlterAmount)
    {
        _myCardScript.myStatusRef.hp += HPAlterAmount;
        if (HPAlterAmount < 0)
        {
            //LingeringEffectManager.Me?.InvokeOnDmgDealtEvent(_myCardScript.myStatusRef, _myCardScript.myStatusRef); // TIMEPOINT
        }
    }

    public void AlterTheirHP(int HPAlterAmount)
    {
        _myCardScript.theirStatusRef.hp += HPAlterAmount;
        if (HPAlterAmount < 0)
        {
            if (_myCardScript.myStatusRef == CombatManager.Me.ownerPlayerStatusRef) // if card owner is player, then player is dealing dmg to enemy
            {
                var tempList = new  List<GameObject>();
                UtilityFuncManagerScript.CopyGameObjectList(CombatManager.Me.combinedDeckZone, tempList, true);
                UtilityFuncManagerScript.CopyGameObjectList(CombatManager.Me.graveZone, tempList, false);
                foreach (var card in tempList)
                {
                    card.GetComponent<CardEventTrigger>()?.InvokeOwnerDealtDmgToEnemyEvent(); // TIMEPOINT
                }
            }
        }
    }

    public void AlterHP(int amount, PlayerStatusSO playerStatus)
    {
        playerStatus.hp += amount;
        if (amount < 0)
        {
            LingeringEffectManager.Me?.InvokeOnDmgDealtEvent(_myCardScript.myStatusRef, playerStatus); // TIMEPOINT
        }
    }
}