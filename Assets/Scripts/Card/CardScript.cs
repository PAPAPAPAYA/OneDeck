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
	public List<EnumStorage.StatusEffect> displayMyStatusEffects;
	private bool _hasDisplaySnapshot = false;
	[Header("Tags")]
	public List<EnumStorage.Tag> myTags;

	private string _displayCardDesc;
	private HPAlterEffect _cachedHpAlterEffect;

	/// <summary>
	/// Capture a snapshot of current myStatusEffects for display purposes.
	/// Once snapped, GetStatusEffectsForDisplay() returns the snapshotted list
	/// until CommitDisplayState() is called (typically after animation completes).
	/// </summary>
	public void SnapshotDisplayState()
	{
		if (_hasDisplaySnapshot) return;
		if (displayMyStatusEffects == null)
			displayMyStatusEffects = new List<EnumStorage.StatusEffect>();
		displayMyStatusEffects.Clear();
		displayMyStatusEffects.AddRange(myStatusEffects);
		_displayCardDesc = ComputeDynamicCardDesc();
		_hasDisplaySnapshot = true;
	}

	/// <summary>
	/// Commit the display state to match the current myStatusEffects.
	/// Called after StatusEffectChange animation completes.
	/// </summary>
	public void CommitDisplayState()
	{
		if (displayMyStatusEffects == null)
			displayMyStatusEffects = new List<EnumStorage.StatusEffect>();
		displayMyStatusEffects.Clear();
		displayMyStatusEffects.AddRange(myStatusEffects);
		_displayCardDesc = null;
		_hasDisplaySnapshot = false;
	}

	/// <summary>
	/// Returns the status effects list that should be used for visual display.
	/// If a display snapshot is active (during animation), returns the snapshot;
	/// otherwise returns the live myStatusEffects list.
	/// </summary>
	public List<EnumStorage.StatusEffect> GetStatusEffectsForDisplay()
	{
		return _hasDisplaySnapshot ? displayMyStatusEffects : myStatusEffects;
	}

	/// <summary>
	/// Returns the card description that should be used for visual display.
	/// If a display snapshot is active (during animation), returns the snapshot;
	/// otherwise returns the live computed description with placeholders resolved.
	/// </summary>
	public string GetCardDescForDisplay()
	{
		if (_hasDisplaySnapshot)
			return _displayCardDesc ?? cardDesc;
		return ComputeDynamicCardDesc();
	}

	/// <summary>
	/// Computes the dynamic card description by replacing placeholders:
	/// &lt;dmg&gt; with base damage plus Power status effect count,
	/// &lt;counter&gt; with current Counter status effect count as an optional suffix.
	/// </summary>
	private string ComputeDynamicCardDesc()
	{
		if (string.IsNullOrEmpty(cardDesc))
			return cardDesc;

		string desc = cardDesc;

		// Replace <dmg> with computed damage value
		if (desc.Contains("<dmg>"))
		{
			if (_cachedHpAlterEffect == null)
				_cachedHpAlterEffect = GetComponentInChildren<HPAlterEffect>();
			var hpAlter = _cachedHpAlterEffect;
			if (hpAlter != null && hpAlter.baseDmg != null)
			{
				int baseDmg = hpAlter.baseDmg.value + hpAlter.extraDmg;
				int powerCount = 0;
				foreach (var se in myStatusEffects)
				{
					if (se == EnumStorage.StatusEffect.Power)
						powerCount++;
				}

				string dmgStr = baseDmg.ToString();
				if (powerCount > 0)
					dmgStr += " (+" + powerCount + ")";

				desc = desc.Replace("<dmg>", dmgStr);
			}
		}

		// Replace <counter> with optional Counter suffix
		if (desc.Contains("<counter>"))
		{
			int counterCount = 0;
			foreach (var se in myStatusEffects)
			{
				if (se == EnumStorage.StatusEffect.Counter)
					counterCount++;
			}

			string counterStr = counterCount > 0 ? " (-" + counterCount + ")" : "";
			desc = desc.Replace("<counter>", counterStr);
		}

		return desc;
	}

	private void OnEnable()
	{
		cardID = CardIDRetriever.Me.RetrieveCardID();
		if (displayMyStatusEffects == null)
			displayMyStatusEffects = new List<EnumStorage.StatusEffect>();
	}
}
