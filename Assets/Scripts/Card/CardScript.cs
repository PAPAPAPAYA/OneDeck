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
	/// Set the display baseline to the provided list and lock the display snapshot.
	/// Called by RecorderAnimationPlayer before playing animations so that
	/// GetStatusEffectsForDisplay() returns the state before any pending animations.
	/// </summary>
	public void SetDisplayBaseline(List<EnumStorage.StatusEffect> baseline)
	{
		if (displayMyStatusEffects == null)
			displayMyStatusEffects = new List<EnumStorage.StatusEffect>();
		displayMyStatusEffects.Clear();
		if (baseline != null)
			displayMyStatusEffects.AddRange(baseline);
		_displayCardDesc = null;
		_hasDisplaySnapshot = true;
	}

	/// <summary>
	/// Apply a signed status effect delta to the display list.
	/// Positive delta adds layers; negative delta removes layers.
	/// </summary>
	public void ApplyDisplayDelta(EnumStorage.StatusEffect effect, int delta)
	{
		ApplyStatusEffectDeltaToList(displayMyStatusEffects, effect, delta);
		_displayCardDesc = null;
	}

	/// <summary>
	/// Helper that applies a signed status effect delta to any status effect list.
	/// Positive delta adds layers; negative delta removes layers.
	/// </summary>
	public static void ApplyStatusEffectDeltaToList(List<EnumStorage.StatusEffect> list, EnumStorage.StatusEffect effect, int delta)
	{
		if (list == null) return;
		if (delta > 0)
		{
			for (int i = 0; i < delta; i++)
				list.Add(effect);
		}
		else if (delta < 0)
		{
			for (int i = 0; i < -delta; i++)
				list.Remove(effect);
		}
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
	/// Also appends an optional parenthesized damage suffix configured on HPAlterEffect.
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

		return AppendDynamicDamageSuffix(desc);
	}

	/// <summary>
	/// Appends a parenthesized real-time damage estimate to the description
	/// when the attached HPAlterEffect requests it.
	/// Returns the original description if no source is configured or data is unavailable.
	/// </summary>
	private string AppendDynamicDamageSuffix(string desc)
	{
		if (_cachedHpAlterEffect == null)
			_cachedHpAlterEffect = GetComponentInChildren<HPAlterEffect>();
		var hpAlter = _cachedHpAlterEffect;
		if (hpAlter == null)
			return desc;

		var source = hpAlter.dynamicDmgDisplaySource;
		if (source == HPAlterEffect.DynamicDmgDisplaySource.None)
			return desc;

		if (ValueTrackerManager.me == null || CombatManager.Me == null)
			return desc;

		int selfPowerCount = 0;
		if (myStatusEffects != null)
		{
			foreach (var se in myStatusEffects)
			{
				if (se == EnumStorage.StatusEffect.Power)
					selfPowerCount++;
			}
		}

		int baseValue = 0;
		switch (source)
		{
			case HPAlterEffect.DynamicDmgDisplaySource.TotalPowerCount:
				if (ValueTrackerManager.me.totalPowerCountInDeckRef != null)
					baseValue = ValueTrackerManager.me.totalPowerCountInDeckRef.value;
				break;
			case HPAlterEffect.DynamicDmgDisplaySource.FriendlyCardCount:
				if (myStatusRef == CombatManager.Me.ownerPlayerStatusRef)
				{
					if (ValueTrackerManager.me.ownerCardCountInDeckRef != null)
						baseValue = ValueTrackerManager.me.ownerCardCountInDeckRef.value;
				}
				else
				{
					if (ValueTrackerManager.me.enemyCardCountInDeckRef != null)
						baseValue = ValueTrackerManager.me.enemyCardCountInDeckRef.value;
				}
				break;
			case HPAlterEffect.DynamicDmgDisplaySource.OpponentBuriedCount:
				if (myStatusRef == CombatManager.Me.ownerPlayerStatusRef)
				{
					if (ValueTrackerManager.me.enemyCardsBuriedCountRef != null)
						baseValue = ValueTrackerManager.me.enemyCardsBuriedCountRef.value;
				}
				else
				{
					if (ValueTrackerManager.me.ownerCardsBuriedCountRef != null)
						baseValue = ValueTrackerManager.me.ownerCardsBuriedCountRef.value;
				}
				break;
		}

		int totalDmg = hpAlter.dynamicDmgDisplayMultiplyByPower
			? baseValue * (1 + selfPowerCount)
			: baseValue + selfPowerCount;

		return desc + "(当前总伤害:" + totalDmg + ")";
	}

	private void OnEnable()
	{
		cardID = CardIDRetriever.Me.RetrieveCardID();
		if (displayMyStatusEffects == null)
			displayMyStatusEffects = new List<EnumStorage.StatusEffect>();
	}
}
