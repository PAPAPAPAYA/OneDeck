using System;
using UnityEngine;
using UnityEngine.Events;

// control the overall flow of one session, currently dictating current phase is shop or combat
public class PhaseManager : MonoBehaviour
{
        [Header("Flow Refs")]
        public GamePhaseSO currentGamePhaseRef;
        public IntSO roundCurrent;
        
        [Header("Status Refs")]
        public PlayerStatusSO playerStatusRef;
        public PlayerStatusSO enemyStatusRef;

        [Header("Phase Enter Events")]
        public UnityEvent onEnterCombatPhase;
        private void InvokeEnterCombatPhaseEvent()
        {
                onEnterCombatPhase?.Invoke();
        }
        public UnityEvent onEnterShopPhase;
        private void InvokeEnterShopPhaseEvent()
        {
                onEnterShopPhase?.Invoke();
        }
        private void OnEnable()
        {
                EnteringShopPhase();
        }
        private void Update()
        {
                if (currentGamePhaseRef.Value() == EnumStorage.GamePhase.Shop)
                {
                        if (Input.GetKeyDown(KeyCode.C))
                        { 
                                EnteringCombatPhase();
                        }
                }
                else if (currentGamePhaseRef.Value() == EnumStorage.GamePhase.Combat)
                {
                        if (playerStatusRef.hp <= 0)
                        {
                                print("you lose");
                                EnteringShopPhase();
                        }
                        else if (enemyStatusRef.hp <= 0)
                        {
                                print("you win");
                                EnteringShopPhase();
                        }
                }
        }

        private void EnteringCombatPhase()
        {
                InvokeEnterCombatPhaseEvent();
                currentGamePhaseRef.currentGamePhase = EnumStorage.GamePhase.Combat;
        }
        private void EnteringShopPhase()
        {
                playerStatusRef.Reset();
                enemyStatusRef.Reset();
                InvokeEnterShopPhaseEvent();
                currentGamePhaseRef.currentGamePhase = EnumStorage.GamePhase.Shop;
        }
}
