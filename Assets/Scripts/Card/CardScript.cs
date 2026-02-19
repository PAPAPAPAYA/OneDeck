using System;
using System.Collections.Generic;
using DefaultNamespace.Managers;
using UnityEngine;

public class CardScript : MonoBehaviour
{
    [Header("Card Info")]
    [HideInInspector]
    public int cardID;
    [Tooltip("唯一标识卡类型，用于胜率统计等（改名不影响）")]
    public string cardTypeID;
    [TextArea]
    public string cardDesc;
    public bool takeUpSpace = true; // whether this card takes up deck size
    [Tooltip("是否是回合开始标记卡（Start Card）")]
    public bool isStartCard = false;
    public IntSO price;
    [HideInInspector]
    public PlayerStatusSO myStatusRef;
    [HideInInspector]
    public PlayerStatusSO theirStatusRef;
    [Header("Status Effects")]
    public List<EnumStorage.StatusEffect> myStatusEffects;
    [Header("Tags")]
    public List<EnumStorage.Tag> myTags;

    private void OnEnable()
    {
        cardID = CardIDRetriever.Me.RetrieveCardID();
    }
}