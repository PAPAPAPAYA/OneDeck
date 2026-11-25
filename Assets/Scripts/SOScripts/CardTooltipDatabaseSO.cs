using System;
using System.Collections.Generic;
using DefaultNamespace.SOScripts;
using UnityEngine;

/// <summary>
/// Cards that the hover tooltip can explain by reference. Each entry is one card
/// (today only the RIFT believer token) that other cards name in their cardDesc.
/// A card whose raw cardDesc names an entry's card by its bare display name
/// (outside [...] tag phrases and &lt;...&gt; markup) gets a trailing
/// card-reference block in CardTagTooltip: the referenced card's display name as
/// a bare bold title and the referenced card's own cardDesc as the body.
/// Access via the static lazy-loaded singleton: CardTooltipDatabaseSO.Me (asset
/// must live at Assets/Resources/CardTooltipDatabase.asset). The resolution
/// logic lives in CardRefTooltipBlocks; this type only holds the data. Any
/// StringSO assigned here must have reset = false, otherwise StringSO.OnEnable
/// wipes the text.
/// </summary>
[CreateAssetMenu(fileName = "CardTooltipDatabase", menuName = "SORefs/CardTooltipDatabase")]
public class CardTooltipDatabaseSO : ScriptableObject
{
	private static CardTooltipDatabaseSO _me;

	/// <summary>Lazy-loaded singleton. Loads "CardTooltipDatabase" from Resources on first access.</summary>
	public static CardTooltipDatabaseSO Me
	{
		get
		{
			if (_me == null)
			{
				_me = Resources.Load<CardTooltipDatabaseSO>("CardTooltipDatabase");
				if (_me == null)
				{
					Debug.LogError("[CardTooltipDatabaseSO] No CardTooltipDatabase asset found in Resources. Expected at Assets/Resources/CardTooltipDatabase.asset");
				}
			}
			return _me;
		}
	}

	[Serializable]
	public class Entry
	{
		[Tooltip("The referenced card. Its CardScript supplies the display name (block title) and the effect text (block body).")]
		public GameObject cardPrefab;
		[Tooltip("Optional: non-empty text replaces the block body. Use it for a referenced card whose raw cardDesc holds dynamic placeholders.")]
		public StringSO descriptionOverride;
	}

	[Serializable]
	public class BlockStyle
	{
		[Tooltip("Optional format of the block title; {0} = the referenced card's display name. Null or empty = bare display name.")]
		public StringSO titleFormat;
		[Tooltip("Optional title color. Null = GameColorPalette.TooltipTextColor.")]
		public ColorSO titleColor;
		[Tooltip("Optional body color. Null = GameColorPalette.TooltipTextColor.")]
		public ColorSO bodyColor;
		[Tooltip("Card block font size. 0 or below = same as the type/tag text.")]
		public float fontSize = 0f;
		[Tooltip("Extra pixels above the card block. 0 or below = none (the default gap equals the blank line between the blocks above).")]
		public float spacingBefore = 0f;
	}

	public List<Entry> entries = new List<Entry>();
	public BlockStyle cardBlock = new BlockStyle();
}
