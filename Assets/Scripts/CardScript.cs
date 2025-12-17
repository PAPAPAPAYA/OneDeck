using System;
using UnityEngine;

public class CardScript : MonoBehaviour
{
        public string cardName;
        [TextArea]
        public string cardDesc;

        // for testing
        private void Update()
        {
                if (Input.GetKeyDown(KeyCode.T))
                {
                        GetComponent<CardEventTrigger>().CardActivateEvent?.Invoke();
                }
        }
}