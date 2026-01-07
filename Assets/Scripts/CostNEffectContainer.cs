using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

// this script is used to package, or in other words, to associate effects with their corresponding costs
// all cost functions need to implement here as they all aim to change variable [costCanBePayed]
// this way, we can assign effects and their costs via UnityEvent, even as UnityEvents can't return values in a straight forward way
// so this script is responsible for checking effect cost
public class CostNEffectContainer: MonoBehaviour
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

        [Header("Basic Info")] [Tooltip("don't assign identical effect name to effects in the same card")]
        public string effectName;
        [TextArea]
        public string effectDescription;

        [Header("Cost and Effect Events")] public UnityEvent checkCostEvent;
        public UnityEvent effectEvent;

        //private bool _costCanBePayed = true;
        private int _costNotMetFlag = 0;
        
        public void InvokeEffectEvent()
        {
	        // check cost
	        _costNotMetFlag = 0;
                checkCostEvent?.Invoke();
                
                // invoke effect
                if (_costNotMetFlag > 0) return; // if cost can not be met, return
                if (EffectChainManager.Me.CheckEffectAndRecord("card " + _myCardScript.cardID + ": " + effectName)) // check if effect already in chain
                {
	                effectEvent?.Invoke(); // invoke effects
                }
        }

        #region check cost funcs
        public void CheckCost_noCost() // obsoleted since default value of _costCanBePayed is true
        {
                //_costCanBePayed = true;
        }

        public void CheckCost_Mana(int mana)
        {
                if (_myCardScript.myStatusRef.mana >= mana)
                {
                        //_costCanBePayed = true;
                }
                else
                {
	                _costNotMetFlag++;
	                if (CombatManager.Me.revealZone != transform.parent.gameObject) return;
	                CombatInfoDisplayer.me.effectResultDisplay.text += "Not enough mana to activate [" + _myCardScript.cardName + "]";
                }
        }

        public void CheckCost_InGrave()
        {
                if (CombatManager.Me.graveZone.Contains(transform.parent.gameObject))
                {
                        //_costCanBePayed = true;
                }
                else
                {
	                _costNotMetFlag++;
                        print("not in grave");
                }
        }
        public void CheckCost_InReveal()
        {
	        if (CombatManager.Me.revealZone == transform.parent.gameObject)
	        {
		        //_costCanBePayed = true;
	        }
	        else
	        {
		        _costNotMetFlag++;
	        }
        }
        #endregion
}