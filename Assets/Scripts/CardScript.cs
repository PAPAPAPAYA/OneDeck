using System;
using System.Collections.Generic;
using DefaultNamespace.Managers;
using UnityEngine;

public class CardScript : MonoBehaviour
{
    [Header("Basic Info")]
    public string cardName;
    public int cardID;
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

    private void Start()
    {
        cardID = CardIDRetriever.Me.RetrieveCardID();
    }

    //todo for testing
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            GetComponent<CardEventTrigger>().cardActivateEvent?.Invoke();
        }
    }
}