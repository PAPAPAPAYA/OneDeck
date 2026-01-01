using System;
using UnityEngine;

[CreateAssetMenu(fileName = "GamePhaseSO", menuName = "SORefs/GamePhaseSO")]
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
