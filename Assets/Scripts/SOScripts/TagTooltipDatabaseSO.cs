using System;
using System.Collections.Generic;
using DefaultNamespace.SOScripts;
using UnityEngine;

/// <summary>
/// Maps each visible card Tag to its display name and tooltip explanation text
/// (StringSO each). The display name is the single source of truth for every
/// user-visible tag text: the in-card tag print, the hover tooltip title, and
/// &lt;tag:EnumName&gt; placeholders inside cardDesc all resolve through
/// GetTagDisplayName. Access via the static lazy-loaded singleton:
/// TagTooltipDatabaseSO.Me (asset must live at
/// Assets/Resources/TagTooltipDatabase.asset).
/// Display-name and description StringSO assets must have reset = false,
/// otherwise StringSO.OnEnable wipes the text.
/// </summary>
[CreateAssetMenu(fileName = "TagTooltipDatabase", menuName = "SORefs/TagTooltipDatabase")]
public class TagTooltipDatabaseSO : ScriptableObject
{
	private static TagTooltipDatabaseSO _me;

	/// <summary>Lazy-loaded singleton. Loads "TagTooltipDatabase" from Resources on first access.</summary>
	public static TagTooltipDatabaseSO Me
	{
		get
		{
			if (_me == null)
			{
				_me = Resources.Load<TagTooltipDatabaseSO>("TagTooltipDatabase");
				if (_me == null)
				{
					Debug.LogError("[TagTooltipDatabaseSO] No TagTooltipDatabase asset found in Resources. Expected at Assets/Resources/TagTooltipDatabase.asset");
				}
			}
			return _me;
		}
	}

	[Serializable]
	public class Entry
	{
		public EnumStorage.Tag tag;
		public StringSO description;
		public StringSO displayName;
	}

	public List<Entry> entries = new List<Entry>();

	/// <summary>First matching entry's description, or null when the tag has none.</summary>
	public StringSO GetDescription(EnumStorage.Tag tag)
	{
		if (entries == null) return null;
		for (int i = 0; i < entries.Count; i++)
		{
			if (entries[i] != null && entries[i].tag == tag)
			{
				return entries[i].description;
			}
		}
		return null;
	}

	/// <summary>
	/// Display name for the tag: the configured StringSO value when present and
	/// non-empty, otherwise the enum name.
	/// </summary>
	public string GetDisplayName(EnumStorage.Tag tag)
	{
		if (entries != null)
		{
			for (int i = 0; i < entries.Count; i++)
			{
				if (entries[i] != null && entries[i].tag == tag
					&& entries[i].displayName != null && !string.IsNullOrEmpty(entries[i].displayName.value))
				{
					return entries[i].displayName.value;
				}
			}
		}
		return tag.ToString();
	}

	/// <summary>
	/// Static convenience accessor: display name via the lazy singleton, falling
	/// back to the enum name when the database asset is missing.
	/// </summary>
	public static string GetTagDisplayName(EnumStorage.Tag tag)
	{
		TagTooltipDatabaseSO db = Me;
		return db != null ? db.GetDisplayName(tag) : tag.ToString();
	}
}
