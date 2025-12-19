using System;
using UnityEngine;

// this script functions as a variable storage in combat
public class CombatManager : MonoBehaviour
{
        #region SINGLETON
        public static CombatManager instance;
        private void Awake()
        {
                instance = this;
        }
        #endregion
        
        public int playerMana;
        public int playerHP;
        public GamePhaseSO currentGamePhaseRef;
        public EnumStorage.CombatState currentCombatState;
        // todo: list of combined deck
        public static void EnterCombat()
        {
                print("Entering combat");
        }
        private void Update()
        {
                if (currentGamePhaseRef.Value() != EnumStorage.GamePhase.Combat) return;

                switch (currentCombatState)
                {
                        case EnumStorage.CombatState.GatherDeckLists:
                                
                                break;
                }
        }
        private void GatherDecks()
        {
                
        }
}
