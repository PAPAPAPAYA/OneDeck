using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class LingeringEffectManager : MonoBehaviour
{
    #region SINGLETON

    public static LingeringEffectManager Me;

    private void Awake()
    {
        Me = this;
    }

    #endregion SINGLETON

    [Header("Context")]
    public PlayerStatusSO sessionOwnerStatusRef;
    
    [Header("Events")]
    public GameEvent onOwnerDealDmgToEnemy;
    //public UnityEvent onOwnerDealAnyDmg;
    //public UnityEvent onOwnerDealDmgToEnemy;
    //public UnityEvent onOwnerDealDmgToSelf;
    //public UnityEvent onEnemyDealAnyDmg;
    //public UnityEvent onEnemyDealDmgToOwner;

    [Header("Linger Effect Resolvers")]
    public List<GameObject> lingerEffectResolvers;
    public void InvokeOnDmgDealtEvent(PlayerStatusSO dmgDealer, PlayerStatusSO dmgReceiver)
    {
        if (dmgDealer == sessionOwnerStatusRef) // session owner dealt dmg
        {
            if (dmgReceiver == sessionOwnerStatusRef) // session owner self harm
            {
                //onOwnerDealDmgToSelf.Invoke();
            }
            else // session owner dealt dmg to enemy
            {
                onOwnerDealDmgToEnemy.Raise();
            }
            //onOwnerDealAnyDmg.Invoke();
        }
        else // enemy dealt dmg
        {
            if (dmgReceiver == sessionOwnerStatusRef) // enemy dealt dmg to session owner
            {
                //onEnemyDealDmgToOwner.Invoke();
            }
            else // enemy dealt dmg to themselves
            {
                throw new NotImplementedException();
            }

            //onEnemyDealAnyDmg.Invoke();
        }
    }
}