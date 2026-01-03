using System;
using UnityEngine;
using UnityEngine.Events;

// this script is used to package, or in other words, to associate effects with their corresponding costs
// all cost functions need to implement here as they all aim to change variable [costCanBePayed]
// this way, we can assign effects and their costs via UnityEvent, even as UnityEvents can't return values in a straight forward way
public class CostNEffectContainer : MonoBehaviour
{
    #region GET MY CARD SCRIPT

    private CardScript _myCardScript;

    private void OnEnable()
    {
        _myCardScript = GetComponent<CardScript>();
    }

    #endregion

    [Header("Basic Info")]
    [Tooltip("don't assign identical effect name to effects in the same card")]
    public string effectName;

    [Header("Cost and Effect Events")]
    public UnityEvent checkCostEvent;
    public UnityEvent effectEvent;

    private bool costCanBePayed = false;

    public void InvokeEffectEvent()
    {
        // check if effect already in chain
        if (EffectChainManager.Me.CheckEffectAndRecord(_myCardScript.cardID + effectName)) // concatenate card id and this effect's name
        {
            checkCostEvent?.Invoke(); // check if cost is met or can be met
            if (!costCanBePayed) return; // if cost can't be met, return
            effectEvent?.Invoke(); // if cost can be met, invoke effect
            print(GetComponent<CardScript>().cardName+" is triggered");
            costCanBePayed = false; // reset flag
        }
        else
        {
            print("effect already processed once, don't loop");
        }
    }

    #region check cost funcs

    public void CheckCost_noCost()
    {
        costCanBePayed = true;
    }

    public void CheckCost_Mana(int mana)
    {
        if (_myCardScript.myStatusRef.mana >= mana)
        {
            costCanBePayed = true;
        }
        else
        {
            print("not enough mana");
        }
    }

    public void CheckCost_InGrave()
    {
        if (CombatManager.Me.graveZone.Contains(gameObject))
        {
            costCanBePayed = true;
            print("card in grave");
        }
        else
        {
            print("not in grave");
        }
    }

    #endregion
}