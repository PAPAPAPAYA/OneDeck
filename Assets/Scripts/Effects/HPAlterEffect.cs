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
    }

    public void AlterTheirHP(int HPAlterAmount)
    {
        _myCardScript.theirStatusRef.hp += HPAlterAmount;
    }

    public void AlterHP(int amount, PlayerStatusSO playerStatus)
    {
        playerStatus.hp += amount;
    }
}