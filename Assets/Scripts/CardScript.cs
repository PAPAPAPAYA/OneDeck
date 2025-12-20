using System;
using SOScripts;
using UnityEngine;

public class CardScript : MonoBehaviour
{
        public string cardName;
        [TextArea]
        public string cardDesc;
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