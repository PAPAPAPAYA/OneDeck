using System;
using UnityEngine;

public class ShopManager : MonoBehaviour
{
        [Header("flow ref")]
        public GamePhaseSO gamePhaseRef;
        [Header("deck ref")]
        public DeckSO playerDeckRef;
        public DeckSO shopPoolRef;
        private void Update()
        {
                if (gamePhaseRef.currentGamePhase != EnumStorage.GamePhase.Shop) return;
                ShowDeck();
        }
        private void ShowDeck()
        {
                
        }
}
