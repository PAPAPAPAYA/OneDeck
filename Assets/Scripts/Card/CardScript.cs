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
    
    /// <summary>
    /// 是否为中立卡（无归属，不参与效果计算）
    /// </summary>
    public bool IsNeutralCard => isStartCard;
    
    /// <summary>
    /// 检查此卡是否能被效果影响（有归属且不是中立卡）
    /// </summary>
    public bool CanBeAffectedByEffects => !IsNeutralCard && myStatusRef != null;
    
    public bool isMinion = false;
    public IntSO price;
    //[HideInInspector]
    public PlayerStatusSO myStatusRef;
    //[HideInInspector]
    public PlayerStatusSO theirStatusRef;
    [Header("Status Effects")]
    public List<EnumStorage.StatusEffect> myStatusEffects;
    [Header("Tags")]
    public List<EnumStorage.Tag> myTags;

    [Header("Delay Cost")]
    [Tooltip("发动时，将N张己方卡往后推迟1位")]
    public int delayCost;

    [Header("Bury Cost")]
    [Tooltip("发动时，将N张己方卡置底")]
    public int buryCost;

    [Header("Expose Cost")]
    [Tooltip("发动时，将N张敌方卡置顶")]
    public int exposeCost;

    [Header("Minion Cost")]
    [Tooltip("发动时需要消耗的minion卡数量")]
    public int minionCostCount;
    [Tooltip("消耗的minion卡类型ID（如'fly'），空字符串表示不限制类型")]
    public string minionCostCardTypeID;
    [Tooltip("消耗的minion卡所属：Me=己方, Them=敌方, Random=随机")]
    public EnumStorage.TargetType minionCostOwner = EnumStorage.TargetType.Me;

    private void OnEnable()
    {
        cardID = CardIDRetriever.Me.RetrieveCardID();
    }
}