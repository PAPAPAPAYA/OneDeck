using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class DeckSO : ScriptableObject
{
        public List<GameObject> deck;
        public bool resetOnStart;
        private void OnEnable()
        {
                if (resetOnStart) deck.Clear();
        }
}
