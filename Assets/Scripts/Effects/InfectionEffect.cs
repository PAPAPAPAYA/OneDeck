using System.Collections.Generic;
using UnityEngine;

public class InfectionEffect : MonoBehaviour
{
    private CombatManager _cm;
    
    public void InfectRandom(int amount)
    {
        _cm = CombatManager.Me;
        var cardsToInfect = new List<GameObject>();
        UtilityFuncManagerScript.CopyGameObjectList(_cm.combinedDeckZone, cardsToInfect, true);
        UtilityFuncManagerScript.CopyGameObjectList(_cm.graveZone, cardsToInfect, false);
        cardsToInfect = UtilityFuncManagerScript.ShuffleList(cardsToInfect);
        for (var i = cardsToInfect.Count - 1; i >= 0; i--)
        {
            if (cardsToInfect[i].GetComponent<CardScript>().myTags.Contains(EnumStorage.Tag.Infected))
            {
                cardsToInfect.RemoveAt(i);
            }
        }
        if (cardsToInfect.Count <= 0) return;
        for (var i = 0; i < amount; i++)
        {
            cardsToInfect[i].GetComponent<CardScript>().myTags.Add(EnumStorage.Tag.Infected);
        }
    }
}