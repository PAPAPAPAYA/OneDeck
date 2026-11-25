using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using DefaultNamespace.SOScripts;
using UnityEngine;

/// <summary>
/// Pure helpers behind the card-reference block of the hover tooltip
/// (CardTagTooltip). A card gets one block per CardTooltipDatabaseSO entry whose
/// card it names by a bare display name in its raw cardDesc: a bold bare title
/// (the referenced card's display name) and the referenced card's own cardDesc
/// as the body. This mirrors the cardDesc notation
/// (docs/CardDesc_TagReference_Convention_v2.md): a bracketed [X] phrase is a
/// tag reference and never triggers a card block, a bare name is a card
/// reference. No MonoBehaviour and no scene state, so every member is testable
/// in Edit Mode.
/// </summary>
public static class CardRefTooltipBlocks
{
	/// <summary>Every &lt;...&gt; span: rich text plus placeholders such as &lt;tag:Believer&gt;.</summary>
	private static readonly Regex MarkupSpan = new Regex("<[^<>]*>", RegexOptions.Compiled);

	/// <summary>Every [...] phrase: a tag phrase by convention.</summary>
	private static readonly Regex BracketPhrase = new Regex(@"\[[^\[\]]*\]", RegexOptions.Compiled);

	/// <summary>
	/// True when rawCardDesc names the card by its BARE display name: after
	/// removing every &lt;...&gt; span (rich text and placeholders) and every
	/// [...] tag phrase, the name still occurs.
	/// </summary>
	public static bool DescReferencesCard(string rawCardDesc, string cardDisplayName)
	{
		if (string.IsNullOrEmpty(rawCardDesc) || string.IsNullOrEmpty(cardDisplayName))
		{
			return false;
		}
		string plain = BracketPhrase.Replace(MarkupSpan.Replace(rawCardDesc, string.Empty), string.Empty);
		return plain.IndexOf(cardDisplayName, StringComparison.Ordinal) >= 0;
	}

	/// <summary>
	/// Database entries (in database order) whose card the given card's raw
	/// cardDesc references by bare name. Empty when the card or the database is
	/// null or nothing matches.
	/// </summary>
	public static List<CardTooltipDatabaseSO.Entry> Resolve(CardScript card, CardTooltipDatabaseSO db)
	{
		List<CardTooltipDatabaseSO.Entry> result = new List<CardTooltipDatabaseSO.Entry>();
		if (card == null || db == null || db.entries == null)
		{
			return result;
		}
		string rawDesc = card.cardDesc;
		for (int i = 0; i < db.entries.Count; i++)
		{
			CardTooltipDatabaseSO.Entry entry = db.entries[i];
			CardScript referenced = GetReferencedCard(entry);
			if (referenced == null)
			{
				continue;
			}
			if (DescReferencesCard(rawDesc, referenced.GetDisplayName()))
			{
				result.Add(entry);
			}
		}
		return result;
	}

	/// <summary>
	/// Block title for one entry: the referenced card's display name, passed
	/// through the database's titleFormat when one is configured.
	/// </summary>
	public static string GetTitle(CardTooltipDatabaseSO db, CardTooltipDatabaseSO.Entry entry)
	{
		CardScript referenced = GetReferencedCard(entry);
		if (referenced == null)
		{
			return string.Empty;
		}
		string name = referenced.GetDisplayName();
		StringSO format = db != null && db.cardBlock != null ? db.cardBlock.titleFormat : null;
		if (format != null && !string.IsNullOrEmpty(format.value))
		{
			return string.Format(format.value, name);
		}
		return name;
	}

	/// <summary>
	/// Block body for one entry: descriptionOverride when it is non-empty,
	/// otherwise the referenced card's raw cardDesc (GetCardDescForDisplay is
	/// deliberately not used: it has runtime side effects on live instances).
	/// </summary>
	public static string GetBody(CardTooltipDatabaseSO.Entry entry)
	{
		if (entry == null)
		{
			return string.Empty;
		}
		if (entry.descriptionOverride != null && !string.IsNullOrEmpty(entry.descriptionOverride.value))
		{
			return entry.descriptionOverride.value;
		}
		CardScript referenced = GetReferencedCard(entry);
		return referenced != null ? referenced.cardDesc ?? string.Empty : string.Empty;
	}

	/// <summary>
	/// Full card-reference text for the hovered card: one block per resolved
	/// entry, "&lt;b&gt;title&lt;/b&gt;" followed by a newline and the body when the
	/// body is non-empty; blocks are separated by a blank line. Empty when the
	/// card references no configured card.
	/// </summary>
	public static string BuildCardRefText(CardScript card, CardTooltipDatabaseSO db)
	{
		List<CardTooltipDatabaseSO.Entry> resolved = Resolve(card, db);
		if (resolved.Count == 0)
		{
			return string.Empty;
		}
		ColorSO titleColor = db.cardBlock != null ? db.cardBlock.titleColor : null;
		ColorSO bodyColor = db.cardBlock != null ? db.cardBlock.bodyColor : null;

		StringBuilder sb = new StringBuilder();
		for (int i = 0; i < resolved.Count; i++)
		{
			if (i > 0)
			{
				sb.Append("\n\n");
			}
			string title = GetTitle(db, resolved[i]);
			if (titleColor != null)
			{
				title = titleColor.OpenTag + title + "</color>";
			}
			sb.Append("<b>").Append(title).Append("</b>");
			string body = GetBody(resolved[i]);
			if (!string.IsNullOrEmpty(body))
			{
				sb.Append("\n");
				if (bodyColor != null)
				{
					body = bodyColor.OpenTag + body + "</color>";
				}
				sb.Append(body);
			}
		}
		return sb.ToString();
	}

	/// <summary>Overload using the Resources singleton CardTooltipDatabaseSO.Me.</summary>
	public static string BuildCardRefText(CardScript card)
	{
		return BuildCardRefText(card, CardTooltipDatabaseSO.Me);
	}

	private static CardScript GetReferencedCard(CardTooltipDatabaseSO.Entry entry)
	{
		if (entry == null || entry.cardPrefab == null)
		{
			return null;
		}
		return entry.cardPrefab.GetComponent<CardScript>();
	}
}
