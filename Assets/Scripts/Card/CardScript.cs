using System;
using System.Collections.Generic;
using DefaultNamespace.Managers;
using UnityEngine;

public class CardScript : MonoBehaviour
{
    [Header("Card Info")]
    [HideInInspector]
    public int cardID;
    [TextArea]
    public string cardDesc;
    public bool takeUpSpace = true; // whether this card takes up deck size
    public int price = 4;
    [HideInInspector]
    public PlayerStatusSO myStatusRef;
    [HideInInspector]
    public PlayerStatusSO theirStatusRef;
    [Header("Tags")]
    public List<EnumStorage.StatusEffect> myStatusEffects;

    private void OnEnable()
    {
        cardID = CardIDRetriever.Me.RetrieveCardID();
    }
}