using System;
using UnityEngine;

[CreateAssetMenu]
public class GamePhaseSO : ScriptableObject
{
        public EnumStorage.GamePhase currentGamePhase;
        
        private void OnEnable()
        {
                currentGamePhase = EnumStorage.GamePhase.Shop;
        }

        public EnumStorage.GamePhase Value()
        {
                return currentGamePhase;
        }
}
