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

    public bool triggerLingerEffect = true; // decide whether the effect trigger lingering effects

    public void AlterMyHP(int HPAlterAmount)
    {
        _myCardScript.myStatusRef.hp += HPAlterAmount;
        if (!triggerLingerEffect) return;
        if (HPAlterAmount < 0)
        {
            LingeringEffectManager.Me?.InvokeOnDmgDealtEvent(_myCardScript.myStatusRef, _myCardScript.myStatusRef); // TIMEPOINT
        }
    }

    public void AlterTheirHP(int HPAlterAmount)
    {
        _myCardScript.theirStatusRef.hp += HPAlterAmount;
        if (!triggerLingerEffect) return;
        if (HPAlterAmount < 0)
        {
            LingeringEffectManager.Me?.InvokeOnDmgDealtEvent(_myCardScript.myStatusRef, _myCardScript.theirStatusRef); // TIMEPOINT
        }
    }

    public void AlterHP(int amount, PlayerStatusSO playerStatus)
    {
        playerStatus.hp += amount;
        if (!triggerLingerEffect) return;
        if (amount < 0)
        {
            LingeringEffectManager.Me?.InvokeOnDmgDealtEvent(_myCardScript.myStatusRef, playerStatus); // TIMEPOINT
        }
    }
}