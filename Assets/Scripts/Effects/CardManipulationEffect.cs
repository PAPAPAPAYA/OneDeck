using System;
using System.Collections.Generic;
using UnityEngine;

public class CardManipulationEffect : MonoBehaviour
{
    public DeckSO playerDeck;
    private CombatManager _cm;

    private void OnEnable()
    {
        _cm = CombatManager.Me;
    }

    public void TakeSelfOut() // remove self from deck list
    {
        if (playerDeck.deck.Contains(gameObject))
        {
            playerDeck.deck.Remove(gameObject);
        }
    }

    public void StageSelf() // put self on top of the deck
    {
        if (!_cm.combinedDeckZone.Contains(gameObject))return;
        // var tempList = _cm.combinedDeckZone;
        // tempList.Remove(gameObject);
        // tempList.Add(gameObject);
        // _cm.combinedDeckZone = tempList;
        _cm.combinedDeckZone.Remove(gameObject);
        _cm.combinedDeckZone.Add(gameObject);
    }

    public void StageTag(int amount, EnumStorage.Tag tagToCheck) // put random cards with tagToCheck on top of the deck
    {
        
    }
    
    public void BurySelf() // put self at the bottom of the deck
    {
        if (!_cm.combinedDeckZone.Contains(gameObject)) return;
        _cm.combinedDeckZone.Remove(gameObject);
        _cm.combinedDeckZone.Insert(0, gameObject);
    }
}
