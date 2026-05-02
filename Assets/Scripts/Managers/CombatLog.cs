using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Combat log collector. Logic layer (Effects, CostNEffectContainer) pushes log entries here.
/// CombatInfoDisplayer reads from this and renders to UI.
/// </summary>
public class CombatLog : MonoBehaviour
{
	public static CombatLog me;

	private readonly List<string> _entries = new List<string>();

	private void Awake()
	{
		me = this;
	}

	/// <summary>
	/// Append a log entry. Logic layer should use this instead of writing to effectResultString directly.
	/// </summary>
	public void Append(string entry)
	{
		if (string.IsNullOrEmpty(entry)) return;
		_entries.Add(entry);
	}

	/// <summary>
	/// Render all log entries into a single display string.
	/// </summary>
	public string GetRenderedText()
	{
		if (_entries.Count == 0) return "";
		StringBuilder sb = new StringBuilder();
		foreach (var entry in _entries)
		{
			sb.Append(entry);
			if (!entry.EndsWith("\n"))
				sb.Append("\n");
		}
		return sb.ToString();
	}

	/// <summary>
	/// Clear all log entries.
	/// </summary>
	public void Clear()
	{
		_entries.Clear();
	}
}
