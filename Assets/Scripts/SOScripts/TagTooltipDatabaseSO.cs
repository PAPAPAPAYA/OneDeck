using System;
using System.Collections.Generic;
using DefaultNamespace.SOScripts;
using UnityEngine;

/// <summary>
/// Maps each visible card Tag to its tooltip explanation text (StringSO).
/// Access via the static lazy-loaded singleton: TagTooltipDatabaseSO.Me
/// (asset must live at Assets/Resources/TagTooltipDatabase.asset).
/// Description StringSO assets must have reset = false, otherwise
/// StringSO.OnEnable wipes the text.
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
}
