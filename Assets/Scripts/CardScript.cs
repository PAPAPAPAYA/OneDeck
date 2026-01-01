using System;
using System.Collections.Generic;
using UnityEngine;

public class CardScript : MonoBehaviour
{
    [Header("Basic Info")]
    public string cardName;
    [TextArea]
    public string cardDesc;
    public bool takeUpSpace = true; // whether this card takes up deck size
    public int price;
    [HideInInspector]
    public PlayerStatusSO myStatusRef;
    [HideInInspector]
    public PlayerStatusSO theirStatusRef;
    [Header("Tags")]
    public List<EnumStorage.Tag> myTags;

    //todo for testing
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            GetComponent<CardEventTrigger>().cardActivateEvent?.Invoke();
        }
    }
}