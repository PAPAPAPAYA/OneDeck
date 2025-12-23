using System;
using UnityEngine;

public class CardScript : MonoBehaviour
{
        [Header("Basic Info")]
        public string cardName;
        [TextArea]
        public string cardDesc;
        [Header("Status Refs")]
        public PlayerStatusSO myStatusRef;
        public PlayerStatusSO theirStatusRef;

        // for testing
        private void Update()
        {
                if (Input.GetKeyDown(KeyCode.T))
                {
                        GetComponent<CardEventTrigger>().CardActivateEvent?.Invoke();
                }
        }
}