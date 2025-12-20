using System;
using UnityEngine;
using SOScripts;
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
        
        public string effectName;
        public bool costCanBePayed = false;
        
        public UnityEvent checkCostEvent;
        public UnityEvent effectEvent;
        public void InvokeEffectEvent()
        {
                checkCostEvent?.Invoke();
                if (costCanBePayed)
                {
                        effectEvent?.Invoke();
                        costCanBePayed = false;
                }
        }
        #region check cost funcs
        public void CheckCost_noCost()
        {
                costCanBePayed = true;
        }
        public void CheckCost_Mana1()
        {
                if (_myCardScript.myStatusRef.mana >= 1)
                {
                        costCanBePayed = true;
                }
                else
                {
                        print("not enough mana");
                }
        }
        #endregion
}