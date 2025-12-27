using System;
using UnityEngine;

public class CardScript : MonoBehaviour
{
    [Header("Basic Info")] public string cardName;
    [TextArea] public string cardDesc;
    public bool takeUpSpace = true; // whether this card takes up deck size
    public int price;
    [Header("Status Refs")] public PlayerStatusSO myStatusRef;
    public PlayerStatusSO theirStatusRef;

    //todo for testing
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            GetComponent<CardEventTrigger>().cardActivateEvent?.Invoke();
        }
    }
}