using System;
using System.Collections.Generic;
using UnityEngine;

// used to store a bunch of cards
[CreateAssetMenu]
public class DeckSO : ScriptableObject
{
        public List<GameObject> deck;
        public DeckSO defaultDeck;
        public bool resetOnStart;
        private void OnEnable()
        {
                if (resetOnStart)
                {
                        deck.Clear();
                        if (defaultDeck)
                        {
                                UtilityFuncManagerScript.CopyGameObjectList(defaultDeck.deck, deck);
                        }
                }
        }
}
