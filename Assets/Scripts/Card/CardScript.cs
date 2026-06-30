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
	[System.NonSerialized]
	private List<HPAlterEffect> _cachedHpAlterEffects;
	[System.NonSerialized]
	private Dictionary<string, HPAlterEffect> _hpAlterByKey;
	[System.NonSerialized]
	private HPAlterEffect _defaultHpAlterEffect;

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

		TestManager.Log("[DynamicDamageDisplay] SnapshotDisplayState card=" + GetDisplayName() + " hasSnapshot=" + _hasDisplaySnapshot + " desc=[" + (_displayCardDesc ?? cardDesc) + "]");
		if (_displayCardDesc != null && ContainsAnyDamagePlaceholder(_displayCardDesc))
		{
			TestManager.LogWarning("[DynamicDamageDisplay] SnapshotDisplayState contains raw <dmg>! card=" + GetDisplayName() + " cardDesc=[" + cardDesc + "]");
		}
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
		_displayCardDesc = ComputeDynamicCardDesc(displayMyStatusEffects);
		_hasDisplaySnapshot = true;

		TestManager.Log("[DynamicDamageDisplay] SetDisplayBaseline card=" + GetDisplayName() + " baselineCount=" + (baseline != null ? baseline.Count : 0) + " _displayCardDesc recomputed=[" + (_displayCardDesc ?? "null") + "]");
		if (_displayCardDesc != null && ContainsAnyDamagePlaceholder(_displayCardDesc))
		{
			TestManager.LogWarning("[DynamicDamageDisplay] SetDisplayBaseline recomputed desc still contains raw <dmg>! card=" + GetDisplayName() + " cardDesc=[" + cardDesc + "]");
		}
	}

	/// <summary>
	/// Apply a signed status effect delta to the display list.
	/// Positive delta adds layers; negative delta removes layers.
	/// </summary>
	public void ApplyDisplayDelta(EnumStorage.StatusEffect effect, int delta)
	{
		ApplyStatusEffectDeltaToList(displayMyStatusEffects, effect, delta);
		_displayCardDesc = ComputeDynamicCardDesc(displayMyStatusEffects);
		TestManager.Log("[StatusEffectDisplay] ApplyDisplayDelta card=" + GetDisplayName() +
			" effect=" + effect + " delta=" + delta +
			" displayCount=" + (displayMyStatusEffects != null ? displayMyStatusEffects.Count : 0));
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
	/// Whether a display snapshot is currently active.
	/// </summary>
	public bool HasDisplaySnapshot => _hasDisplaySnapshot;

	/// <summary>
	/// Caches all HPAlterEffect components on this card and indexes them by damageDisplayKey.
	/// The first effect with an empty key (or the first effect overall) becomes the default &lt;dmg&gt; source.
	/// </summary>
	private void CacheHpAlterEffects()
	{
		// Rescan if cache is missing or was previously populated empty.
		if (_cachedHpAlterEffects != null && _cachedHpAlterEffects.Count > 0) return;
		HPAlterEffect[] found = GetComponentsInChildren<HPAlterEffect>(true);
		_cachedHpAlterEffects = new List<HPAlterEffect>(found != null ? found : new HPAlterEffect[0]);
		_hpAlterByKey = new Dictionary<string, HPAlterEffect>();
		_defaultHpAlterEffect = null;
		foreach (var hpAlter in _cachedHpAlterEffects)
		{
			if (hpAlter == null) continue;
			string key = hpAlter.damageDisplayKey ?? "";
			// Tolerate designers entering 'dmg:foo' instead of 'foo' for <dmg:foo> placeholders.
			if (!string.IsNullOrEmpty(key) && key.StartsWith("dmg:"))
				key = key.Substring(4);
			if (string.IsNullOrEmpty(key))
			{
				if (_defaultHpAlterEffect == null)
					_defaultHpAlterEffect = hpAlter;
			}
			else if (!_hpAlterByKey.ContainsKey(key))
			{
				_hpAlterByKey.Add(key, hpAlter);
			}
		}
		if (_defaultHpAlterEffect == null && _cachedHpAlterEffects.Count > 0)
			_defaultHpAlterEffect = _cachedHpAlterEffects[0];
		TestManager.Log("[DynamicDamageDisplay] CacheHpAlterEffects card=" + GetDisplayName() + " go='" + gameObject.name + "' instanceID=" + gameObject.GetInstanceID() + " childCount=" + transform.childCount + " rawFound=" + (found != null ? found.Length : -1) + " cached=" + _cachedHpAlterEffects.Count + " " + GetHpAlterDiagnosticString());
	}

	/// <summary>
	/// Returns the HPAlterEffect that should be used for a damage placeholder.
	/// Empty key returns the default source; named key returns the matching effect.
	/// </summary>
	private HPAlterEffect GetHpAlterEffectForPlaceholder(string key)
	{
		CacheHpAlterEffects();
		if (string.IsNullOrEmpty(key))
			return _defaultHpAlterEffect;
		HPAlterEffect result;
		if (_hpAlterByKey != null && _hpAlterByKey.TryGetValue(key, out result))
			return result;
		return null;
	}

	/// <summary>
	/// Builds a diagnostic string listing all cached HPAlterEffects and the default source.
	/// Does NOT trigger a cache rebuild to avoid recursion.
	/// </summary>
	private string GetHpAlterDiagnosticString()
	{
		int count = _cachedHpAlterEffects != null ? _cachedHpAlterEffects.Count : -1;
		System.Text.StringBuilder sb = new System.Text.StringBuilder();
		sb.Append("HPAlterCount=").Append(count);
		if (_cachedHpAlterEffects != null)
		{
			sb.Append(" [");
			for (int i = 0; i < _cachedHpAlterEffects.Count; i++)
			{
				var hpAlter = _cachedHpAlterEffects[i];
				if (hpAlter == null) continue;
				if (i > 0) sb.Append(", ");
				sb.Append("{");
				sb.Append("idx=").Append(i);
				sb.Append(" key='").Append(hpAlter.damageDisplayKey ?? "<empty>").Append("'");
				sb.Append(" baseDmg=").Append(hpAlter.baseDmg != null ? hpAlter.baseDmg.value.ToString() : "NULL");
				sb.Append(" go='").Append(hpAlter.gameObject.name).Append("'");
				sb.Append("}");
			}
			sb.Append("]");
		}
		sb.Append(" default='").Append(_defaultHpAlterEffect != null ? (_defaultHpAlterEffect.damageDisplayKey ?? "<empty>") : "NULL").Append("'");
		return sb.ToString();
	}

	/// <summary>
	/// Logs a one-shot diagnostic dump for dynamic damage display issues.
	/// Safe to call from UI input handlers (e.g. shop card enlarge).
	/// </summary>
	public void LogDynamicDamageDiagnostics(string context)
	{
		CacheHpAlterEffects();
		string computedDesc = GetCardDescForDisplay();
		HPAlterEffect[] freshEffects = GetComponentsInChildren<HPAlterEffect>(true);
		TestManager.Log("[DynamicDamageDisplay] DIAGNOSTIC context=" + context +
			" card=" + GetDisplayName() +
			" go='" + gameObject.name + "'" +
			" instanceID=" + gameObject.GetInstanceID() +
			" childCount=" + transform.childCount +
			" freshHpAlterCount=" + (freshEffects != null ? freshEffects.Length : -1) +
			" cachedHpAlterCount=" + (_cachedHpAlterEffects != null ? _cachedHpAlterEffects.Count : -1) +
			"\ncardDesc=[" + cardDesc + "]\ncomputed=[" + computedDesc + "]\n" +
			GetHpAlterDiagnosticString());
	}

	/// <summary>
	/// Returns true if the description contains any unresolved &lt;dmg&gt; or &lt;dmg:key&gt; placeholder.
	/// </summary>
	public static bool ContainsAnyDamagePlaceholder(string desc)
	{
		return !string.IsNullOrEmpty(desc) && desc.IndexOf("<dmg") >= 0;
	}

	/// <summary>
	/// Returns the card description that should be used for visual display.
	/// If a display snapshot is active (during animation), returns the snapshot;
	/// otherwise returns the live computed description with placeholders resolved.
	/// </summary>
	public string GetCardDescForDisplay()
	{
		string result = _hasDisplaySnapshot
			? (_displayCardDesc ?? ComputeDynamicCardDesc(displayMyStatusEffects))
			: ComputeDynamicCardDesc();
		// Per-frame placeholder warning removed; use LogDynamicDamageDiagnostics() on demand (e.g. shop card enlarge).
		return result;
	}

	/// <summary>
	/// Computes the dynamic card description using the live myStatusEffects list.
	/// </summary>
	private string ComputeDynamicCardDesc()
	{
		return ComputeDynamicCardDesc(myStatusEffects);
	}

	/// <summary>
	/// Computes the dynamic card description by replacing placeholders:
	/// &lt;dmg&gt; with base damage plus Power status effect count,
	/// &lt;counter&gt; with current Counter status effect count as an optional suffix.
	/// Also appends an optional parenthesized damage suffix configured on HPAlterEffect.
	/// The provided statusEffects list is used for Power/Counter counting so that
	/// display snapshots and baselines stay consistent.
	/// </summary>
	private string ComputeDynamicCardDesc(List<EnumStorage.StatusEffect> statusEffects)
	{
		if (string.IsNullOrEmpty(cardDesc))
			return cardDesc;

		string desc = ReplaceDamagePlaceholders(cardDesc, statusEffects);

		// Replace <counter> with optional Counter suffix
		if (desc.Contains("<counter>"))
		{
			int counterCount = 0;
			if (statusEffects != null)
			{
				foreach (var se in statusEffects)
				{
					if (se == EnumStorage.StatusEffect.Counter)
						counterCount++;
				}
			}

			string counterStr = counterCount > 0 ? " (-" + counterCount + ")" : "";
			desc = desc.Replace("<counter>", counterStr);
		}

		return AppendDynamicDamageSuffix(desc, statusEffects);
	}

	/// <summary>
	/// Replaces &lt;dmg&gt; and &lt;dmg:key&gt; placeholders with computed damage values.
	/// Empty key maps to the default HPAlterEffect; named keys map to matching effects.
	/// </summary>
	private string ReplaceDamagePlaceholders(string desc, List<EnumStorage.StatusEffect> statusEffects)
	{
		if (string.IsNullOrEmpty(desc) || desc.IndexOf("<dmg") < 0)
			return desc;

		System.Text.StringBuilder sb = new System.Text.StringBuilder();
		int i = 0;
		while (i < desc.Length)
		{
			int start = desc.IndexOf("<dmg", i);
			if (start < 0)
			{
				sb.Append(desc.Substring(i));
				break;
			}

			sb.Append(desc.Substring(i, start - i));

			int end = desc.IndexOf(">", start);
			if (end < 0)
			{
				sb.Append(desc.Substring(start));
				break;
			}

			string placeholder = desc.Substring(start, end - start + 1);
			string key = "";
			if (placeholder.Length > 5 && placeholder[4] == ':')
				key = placeholder.Substring(5, placeholder.Length - 6);

			var hpAlter = GetHpAlterEffectForPlaceholder(key);
			if (hpAlter != null && hpAlter.baseDmg != null)
			{
				int baseDmg = hpAlter.baseDmg.value + hpAlter.extraDmg;
				int powerCount = 0;
				if (statusEffects != null)
				{
					foreach (var se in statusEffects)
					{
						if (se == EnumStorage.StatusEffect.Power)
							powerCount++;
					}
				}

				string dmgStr = baseDmg.ToString();
				if (powerCount > 0)
					dmgStr += " (+" + powerCount + ")";

				TestManager.Log("[DynamicDamageDisplay] ReplaceDamagePlaceholders resolved placeholder=" + placeholder + " key='" + key + "' to dmg=" + dmgStr + " on card=" + GetDisplayName());
				sb.Append(dmgStr);
			}
			else
			{
				sb.Append(placeholder);
				// Per-frame placeholder warning removed; use LogDynamicDamageDiagnostics() on demand.
			}

			i = end + 1;
		}

		return sb.ToString();
	}

	/// <summary>
	/// Appends a parenthesized real-time damage estimate to the description
	/// when the attached HPAlterEffect requests it.
	/// Returns the original description if no source is configured or data is unavailable.
	/// </summary>
	private string AppendDynamicDamageSuffix(string desc)
	{
		return AppendDynamicDamageSuffix(desc, myStatusEffects);
	}

	/// <summary>
	/// Appends a parenthesized real-time damage estimate using the provided status effect list.
	/// </summary>
	private string AppendDynamicDamageSuffix(string desc, List<EnumStorage.StatusEffect> statusEffects)
	{
		CacheHpAlterEffects();
		var hpAlter = _defaultHpAlterEffect;
		if (hpAlter == null)
			return desc;

		var source = hpAlter.dynamicDmgDisplaySource;
		if (source == HPAlterEffect.DynamicDmgDisplaySource.None)
			return desc;

		if (ValueTrackerManager.me == null || CombatManager.Me == null)
			return desc;

		int selfPowerCount = 0;
		if (statusEffects != null)
		{
			foreach (var se in statusEffects)
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
