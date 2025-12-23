using System;
using TMPro;
using UnityEngine;

public class InfoDisplayManager : MonoBehaviour
{
        [Header("TMP Objects")]
        public GameObject playerInfoDisplay;
        public GameObject enemyInfoDisplay;
        
        [Header("Status Refs")]
        public PlayerStatusSO playerStatus;
        public PlayerStatusSO enemyStatus;

        [Header("Flow Refs")]
        public GamePhaseSO gamePhase;
        private void Update()
        {
                if (gamePhase.Value() == EnumStorage.GamePhase.Combat)
                {
                        playerInfoDisplay.GetComponent<TextMeshProUGUI>().text =
                                "Your HP: " + playerStatus.hp + "\n" +
                                "Your Mana: " + playerStatus.mana
                                ;
                        enemyInfoDisplay.GetComponent<TextMeshProUGUI>().text =
                                "Their HP: " + enemyStatus.hp +"\n" +
                                "Their Mana: " + enemyStatus.mana
                                ;
                }
                else
                {
                        playerInfoDisplay.GetComponent<TextMeshProUGUI>().text = "";
                        enemyInfoDisplay.GetComponent<TextMeshProUGUI>().text = "";
                }
        }
}
