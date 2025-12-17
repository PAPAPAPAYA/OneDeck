using UnityEngine;
using UnityEngine.Events;

// this script is used to package, or in other words, to associate effects with their corresponding costs
// all cost functions need to implement here as they all aim to change variable [costCanBePayed]
// this way, we can assign effects and their costs via UnityEvent, even as UnityEvents can't return values in a straight forward way
public class CostNEffectContainer : MonoBehaviour
{
        public string effectName;
        public bool costCanBePayed = false;
        
        public UnityEvent checkCostEvent;
        public UnityEvent effectEvent;
        public void InvokeEffectEvent()
        {
                checkCostEvent.Invoke();
                if (costCanBePayed)
                {
                        effectEvent.Invoke();
                        costCanBePayed = false;
                }
        }
        public void CheckCost_Mana1()
        {
                costCanBePayed = CombatManager.instance.playerMana > 0;
        }
}