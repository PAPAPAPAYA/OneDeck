using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// used to store a bunch of cards
[CreateAssetMenu(fileName = "DeckSO", menuName = "SORefs/DeckSO")]
public class DeckSO : ScriptableObject
{
    public List<GameObject> deck;
    public DeckSO defaultDeck;
    public bool resetOnStart;
    [TextArea]
    public string description;

    private void OnEnable()
    {
        if (!resetOnStart) return;
        deck.Clear();
        if (defaultDeck)
        {
            UtilityFuncManagerScript.CopyGameObjectList(defaultDeck.deck, deck, true);
        }
    }
}