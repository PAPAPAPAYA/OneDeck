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
	[Tooltip("Display name for this card. If left empty, the GameObject name will be used.")]
	public string displayName;
	[TextArea]
	public string cardDesc;
	public EnumStorage.Rarity rarity;
	[Tooltip("Shop roll weight multiplier for this specific card. Applied on top of rarity weight. 1 = default, 0 = never appears, 2 = twice as likely")]
	public float shopRollWeightMultiplier = 1f;

	/// <summary>
	/// Returns the display name for this card. Uses displayName if set, otherwise falls back to GameObject name.
	/// </summary>
	public string GetDisplayName()
	{
		return string.IsNullOrEmpty(displayName) ? gameObject.name : displayName;
	}
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
	
	[HideInInspector]
	public bool isMinion = false;
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
