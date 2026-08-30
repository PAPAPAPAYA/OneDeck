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

	[Header("Life")]
	[Tooltip("How many times this card may be revealed per round. 0 = current behavior (once per round).")]
	public int lifeMax = 0;
	[HideInInspector]
	public int currentLife = 0;

	[Header("Attack")]
	[Tooltip("Base attack printed on the card face (per-prefab constant).")]
	public int printedAttack;
	[Tooltip("Permanent attack growth (merges the former Power / attack-growth mechanic). Persists across rounds within a combat; not persisted across combats.")]
	[HideInInspector]
	public int attackGrowth;
	[Tooltip("This-round temporary attack modifier (e.g. -2 this round). Cleared at each round start.")]
	[HideInInspector]
	public int attackModThisRound;
	[Tooltip("Permanent extra attack segments (attack +N times). Stackable; preserved through bury/stage.")]
	public int extraAttackTimes;

	/// <summary>
	/// Creature marker (4.0 spec 生物): card with the attack attribute, including attack 0.
	/// Invariant: 生物 ⟺ ATK column non-empty. Explicit flag — never inferred from components —
	/// so target predicates (强化N友方, 埋葬N敌方生物, onlyTargetEnemyDamagingCards, ...) stay robust.
	/// </summary>
	public bool isCreature = false;

	/// <summary>
	/// Passive marker (4.0 spec 被动, engine step 3): never revealed, immovable, excluded from
	/// movement-effect selection pools. Flag ships ahead of the passive engine — movement effects
	/// (ReviveEffect) check it now; the rest of the passive behavior lands in step 3.
	/// </summary>
	public bool isPassive = false;

	[System.NonSerialized]
	private Func<int> _attackResolver;

	// Reentrancy guard for dynamic-attack graphs: set while this card's own resolver is on
	// the stack; a second entry (cycle) resolves to the base attack instead of recursing.
	[System.NonSerialized]
	private bool _resolvingAttack;

	[System.NonSerialized]
	private int? _displayAttack;

	/// <summary>
	/// Whether this card shows an attack value on its face. Legacy cards (all-zero attack) keep the old face.
	/// </summary>
	public bool HasAttackDisplay => _attackResolver != null || printedAttack != 0 || attackGrowth != 0 || attackModThisRound != 0 || extraAttackTimes != 0;

	/// <summary>
	/// Current attack value (single settlement entry point). Dynamic attack (attack = Y, resolved live)
	/// overrides base + growth + this-round modifier.
	/// </summary>
	public int GetAttack()
	{
		if (_attackResolver == null) return printedAttack + attackGrowth + attackModThisRound;
		// Cycle cut: a reentry while this card's own resolver is still on the stack (a resolver
		// graph reading this card's attack, e.g. two FriendlyCardTotal carriers reading each
		// other) resolves to the base attack instead of recursing forever. The flag is cleared
		// even if the resolver throws.
		if (_resolvingAttack) return printedAttack + attackGrowth + attackModThisRound;
		_resolvingAttack = true;
		try { return _attackResolver(); }
		finally { _resolvingAttack = false; }
	}

	/// <summary>
	/// Set a dynamic attack resolver (attack = Y, resolved at settlement time). Pass null to restore base + growth.
	/// </summary>
	public void SetAttackResolver(Func<int> resolver)
	{
		_attackResolver = resolver;
	}

	/// <summary>
	/// Attack value for display. Returns the frozen snapshot while a display snapshot is
	/// active (logic phase / animation playback), otherwise the live GetAttack() — so the
	/// card face never jumps at logic time (attack changes commit per animation request).
	/// </summary>
	public int GetAttackForDisplay()
	{
		return _displayAttack ?? GetAttack();
	}

	/// <summary>
	/// Apply a signed attack delta to the frozen display value (attack-attribute counterpart
	/// of ApplyDisplayDelta). Commits happen as AttackChange requests play; the display
	/// snapshot is cleared by CommitDisplayState so the face falls back to live GetAttack().
	/// </summary>
	public void CommitAttackDisplayDelta(int delta)
	{
		_displayAttack = (_displayAttack ?? GetAttack()) + delta;
	}

	/// <summary>
	/// Find the card with the highest current attack among the deck + reveal zone
	/// candidates passing the filter (位置谓词 "(最高攻击力)"). Ties are broken randomly.
	/// Returns null when no candidate matches.
	/// </summary>
	public static CardScript FindCardWithMaxAttack(List<GameObject> deck, GameObject revealZone, Predicate<CardScript> filter)
	{
		return FindCardWithExtremeAttack(deck, revealZone, filter, preferMax: true);
	}

	/// <summary>
	/// Find the card with the lowest current attack among the deck + reveal zone
	/// candidates passing the filter (位置谓词 "(最低攻击力)"). Ties are broken randomly.
	/// Returns null when no candidate matches.
	/// </summary>
	public static CardScript FindCardWithMinAttack(List<GameObject> deck, GameObject revealZone, Predicate<CardScript> filter)
	{
		return FindCardWithExtremeAttack(deck, revealZone, filter, preferMax: false);
	}

	private static CardScript FindCardWithExtremeAttack(List<GameObject> deck, GameObject revealZone, Predicate<CardScript> filter, bool preferMax)
	{
		var candidates = new List<CardScript>();
		if (deck != null)
		{
			foreach (var cardObj in deck)
			{
				if (cardObj == null) continue;
				var cardScript = cardObj.GetComponent<CardScript>();
				if (cardScript == null || (filter != null && !filter(cardScript))) continue;
				candidates.Add(cardScript);
			}
		}
		if (revealZone != null)
		{
			var revealCardScript = revealZone.GetComponent<CardScript>();
			if (revealCardScript != null && !candidates.Contains(revealCardScript) &&
			    (filter == null || filter(revealCardScript)))
			{
				candidates.Add(revealCardScript);
			}
		}
		if (candidates.Count == 0) return null;

		int extreme = preferMax ? int.MinValue : int.MaxValue;
		foreach (var card in candidates)
		{
			int attack = card.GetAttack();
			if (preferMax ? attack > extreme : attack < extreme) extreme = attack;
		}

		var tied = new List<CardScript>();
		foreach (var card in candidates)
		{
			if (card.GetAttack() == extreme) tied.Add(card);
		}

		tied = UtilityFuncManagerScript.ShuffleList(tied);
		return tied.Count > 0 ? tied[0] : null;
	}

	/// <summary>
	/// Permanent attack change (enhance / weaken / transfer / siphon). Stackable, kept until combat ends.
	/// </summary>
	public void ModifyAttack(int delta)
	{
		attackGrowth += delta;
	}

	/// <summary>
	/// This-round attack change (e.g. -2 this round); cleared at each round start.
	/// </summary>
	public void ModifyAttackThisRound(int delta)
	{
		attackModThisRound += delta;
	}

	/// <summary>
	/// Number of attack segments per attack action (1 + permanent extra segments).
	/// </summary>
	public int GetAttackTimes()
	{
		return 1 + extraAttackTimes;
	}

	/// <summary>
	/// Permanent attack segment change (attack +N times). Stackable; preserved through bury/stage.
	/// </summary>
	public void ModifyAttackTimes(int delta)
	{
		extraAttackTimes += delta;
	}

	/// <summary>
	/// Clear this-round temporary attack modifiers. Called by CombatManager at each round start.
	/// </summary>
	public void ResetRoundAttackModifiers()
	{
		attackModThisRound = 0;
	}

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
		_displayAttack = GetAttack();
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
		_displayAttack = null;
		_hasDisplaySnapshot = false;
	}

	/// <summary>
	/// Set the display baseline to the provided list and lock the display snapshot.
	/// Called by RecorderAnimationPlayer before playing animations so that
	/// GetStatusEffectsForDisplay() returns the state before any pending animations.
	/// attackBaseline (optional) freezes the attack print at the pre-animation value;
	/// pass null to keep the existing attack snapshot (e.g. one captured by
	/// SnapshotDisplayState for consume/transfer paths).
	/// </summary>
	public void SetDisplayBaseline(List<EnumStorage.StatusEffect> baseline, int? attackBaseline = null)
	{
		if (displayMyStatusEffects == null)
			displayMyStatusEffects = new List<EnumStorage.StatusEffect>();
		displayMyStatusEffects.Clear();
		if (baseline != null)
			displayMyStatusEffects.AddRange(baseline);
		_displayCardDesc = ComputeDynamicCardDesc(displayMyStatusEffects);
		if (attackBaseline.HasValue)
		{
			_displayAttack = attackBaseline.Value;
		}
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
	/// &lt;counter&gt; with current Counter status effect count as an optional suffix,
	/// &lt;tag:EnumName&gt; with the tag's bracketed display name.
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

		desc = ReplaceTagPlaceholders(desc);

		return AppendDynamicDamageSuffix(desc, statusEffects);
	}

	/// <summary>
	/// Replaces &lt;tag:EnumName&gt; placeholders with the tag's display name
	/// (e.g. &lt;tag:DeathRattle&gt; -&gt; 亡语) via TagTooltipDatabaseSO, so
	/// renaming a tag's display name syncs every card description. No brackets
	/// are added — authors write [ ] around the placeholder when they want the
	/// bracketed style. Unparseable placeholders are left as-is with a warning.
	/// </summary>
	private static string ReplaceTagPlaceholders(string desc)
	{
		if (string.IsNullOrEmpty(desc) || desc.IndexOf("<tag:") < 0)
			return desc;

		System.Text.StringBuilder sb = new System.Text.StringBuilder();
		int i = 0;
		while (i < desc.Length)
		{
			int start = desc.IndexOf("<tag:", i, System.StringComparison.Ordinal);
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
			string tagName = desc.Substring(start + 5, end - start - 5);
			EnumStorage.Tag tag;
			if (System.Enum.TryParse(tagName, out tag) && System.Enum.IsDefined(typeof(EnumStorage.Tag), tag) && tag != EnumStorage.Tag.None)
			{
				sb.Append(TagTooltipDatabaseSO.GetTagDisplayName(tag));
			}
			else
			{
				TestManager.LogWarning("[TagDisplay] ReplaceTagPlaceholders could not resolve placeholder=" + placeholder);
				sb.Append(placeholder);
			}

			i = end + 1;
		}

		return sb.ToString();
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
