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
    public bool isToken = false;
    public IntSO price;
    [HideInInspector]
    public PlayerStatusSO myStatusRef;
    [HideInInspector]
    public PlayerStatusSO theirStatusRef;
    [Header("Status Effects")]
    public List<EnumStorage.StatusEffect> myStatusEffects;
    [Header("Tags")]
    public List<EnumStorage.Tag> myTags;

    [Header("Delay Cost")]
    [Tooltip("发动时，将N张己方卡往后推迟1位")]
    public int delayCost;

    [Header("Token Cost")]
    [Tooltip("发动时需要消耗的token卡数量")]
    public int tokenCostCount;
    [Tooltip("消耗的token卡类型ID（如'fly'），空字符串表示不限制类型")]
    public string tokenCostCardTypeID;
    [Tooltip("消耗的token卡所属：Me=己方, Them=敌方, Random=随机")]
    public EnumStorage.TargetType tokenCostOwner = EnumStorage.TargetType.Me;

    private void OnEnable()
    {
        cardID = CardIDRetriever.Me.RetrieveCardID();
    }
}