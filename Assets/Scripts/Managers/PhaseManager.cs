using System;
using SOScripts;
using UnityEngine;

public class PhaseManager : MonoBehaviour
{
        public GamePhaseSO currentGamePhaseRef;
        public IntSO roundCurrent;
        
        [Header("Status Refs")]
        public PlayerStatusSO playerStatusRef;
        public PlayerStatusSO enemyStatusRef;
        private void Update()
        {
                if (currentGamePhaseRef.Value() == EnumStorage.GamePhase.Shop)
                {
                        if (Input.GetKeyDown(KeyCode.C))
                        {
                                CombatManager.EnterCombat();
                                currentGamePhaseRef.currentGamePhase = EnumStorage.GamePhase.Combat;
                        }
                }
                else if (currentGamePhaseRef.Value() == EnumStorage.GamePhase.Combat)
                {
                        if (playerStatusRef.hp <= 0)
                        {
                                EnteringShopPhase();
                        }
                        else if (enemyStatusRef.hp <= 0)
                        {
                                EnteringShopPhase();
                        }
                }
        }

        private void EnteringShopPhase()
        {
                playerStatusRef.Reset();
                enemyStatusRef.Reset();
                print("entering shop");
                currentGamePhaseRef.currentGamePhase = EnumStorage.GamePhase.Shop;
        }
}
