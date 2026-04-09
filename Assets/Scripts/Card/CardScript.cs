using System;
using System.Collections.Generic;
using DefaultNamespace.Managers;
using UnityEngine;

public class CardScript : MonoBehaviour
{
    [Header("Card Info")]
    [HideInInspector]
    public int cardID;
    [Tooltip("Unique identifier for card type, used for win rate statistics (renaming does not affect)")]
    public string cardTypeID;
    [TextArea]
    public string cardDesc;
    public bool takeUpSpace = true; // whether this card takes up deck size
    [Tooltip("Whether this is the round start marker card (Start Card)")]
    public bool isStartCard = false;
    
    /// <summary>
    /// Whether this is a neutral card (no owner, not affected by effects)
    /// </summary>
    public bool IsNeutralCard => isStartCard;
    
    /// <summary>
    /// Check if this card can be affected by effects (has owner and is not neutral)
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
    [Tooltip("When activated, delay N own cards by 1 position")]
    public int delayCost;

    [Header("Bury Cost")]
    [Tooltip("When activated, bury N own cards to the bottom")]
    public int buryCost;

    [Header("Expose Cost")]
    [Tooltip("When activated, expose N enemy cards to the top")]
    public int exposeCost;

    [Header("Minion Cost")]
    [Tooltip("Number of minion cards required to activate")]
    public int minionCostCount;
    [Tooltip("Minion card type ID to consume (e.g., 'fly'), empty string means no type restriction")]
    public string minionCostCardTypeID;
    [Tooltip("Owner of consumed minion cards: Me=ally, Them=enemy, Random=random")]
    public EnumStorage.TargetType minionCostOwner = EnumStorage.TargetType.Me;

    private void OnEnable()
    {
        cardID = CardIDRetriever.Me.RetrieveCardID();
    }
}