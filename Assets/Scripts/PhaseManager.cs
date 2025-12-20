using System;
using SOScripts;
using UnityEngine;

public class PhaseManager : MonoBehaviour
{
        public GamePhaseSO currentGamePhaseRef;
        public IntSO roundCurrent;
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
        }
}
