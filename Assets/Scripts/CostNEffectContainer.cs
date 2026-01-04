using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

// this script is used to package, or in other words, to associate effects with their corresponding costs
// all cost functions need to implement here as they all aim to change variable [costCanBePayed]
// this way, we can assign effects and their costs via UnityEvent, even as UnityEvents can't return values in a straight forward way
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
        [Tooltip("this and card name will combine to show what happened")]
        public string effectDescription;

        [Header("Cost and Effect Events")] public UnityEvent checkCostEvent;
        public UnityEvent effectEvent;

        private bool costCanBePayed = false;
        
        public void InvokeEffectEvent()
        {
                checkCostEvent?.Invoke(); // check if cost is met or can be met
                if (costCanBePayed)
                {
	                if (EffectChainManager.Me.CheckEffectAndRecord("card " + _myCardScript.cardID + ": " + effectName)) // check if effect already in chain
	                {
		                effectEvent?.Invoke(); // if cost can be met, invoke effect
		                print(_myCardScript.cardName + " is triggered");
		                CombatInfoDisplayer.me.effectResultDisplay.text = _myCardScript.cardName + " " + effectName;
		                costCanBePayed = false; // reset flag
	                }
	                else
	                {
		                print("same effect triggered");
	                }
                }
                else
                {
	                if (CombatManager.Me.revealZone == transform.parent.gameObject)
	                {
		                CombatInfoDisplayer.me.effectResultDisplay.text = _myCardScript.cardName + " " + effectName + "'s cost can not be met";
	                }
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
                if (CombatManager.Me.graveZone.Contains(transform.parent.gameObject))
                {
                        costCanBePayed = true;
                        print("card in grave");
                }
                else
                {
                        print("not in grave");
                }
        }
        public void CheckCost_InReveal()
        {
	        if (CombatManager.Me.revealZone == transform.parent.gameObject)
	        {
		        costCanBePayed = true;
	        }
        }
        #endregion
}