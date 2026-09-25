using TMPro;
using UnityEngine;

/// <summary>
/// Read-only text slot of a prefab HUD widget (plan-shop-hud-prefab-widgets §3.3): one
/// TMP_Text fed under a named key. format is a plain prefab string supporting {0}/{1}
/// args and TMP rich text (<size>/<color>) — mixed-size composition is data, not code;
/// several bindings on one chip (each with its own TMP) give compound layouts. Apply()
/// carries the per-slot diff guard from the old ShopChrome.Refresh: rewrite only on
/// change, so refresh ticks never re-layout a label (no flicker).
/// </summary>
public class HudTextBinding : MonoBehaviour
{
	public enum BindingKey
	{
		Money,
		Income,
		RarityCommon,
		RarityUncommon,
		RarityRare,
		Wins,
		Hearts,
		Username,
		// Reserved for later plans (§8) — enum stays append-only.
		DeckUsed,
		DeckSize,
		RerollPrice,
	}

	public BindingKey key = BindingKey.Money;
	[TextArea] public string format = "{0}";
	public TMP_Text target;

	private string _lastArg0;
	private string _lastArg1;
	private bool _hasApplied;

	/// <summary>Formats and pushes the text; no-op while both args are unchanged (diff guard).</summary>
	public void Apply(string arg0, string arg1)
	{
		if (target == null) return;
		if (_hasApplied && arg0 == _lastArg0 && arg1 == _lastArg1) return;
		_lastArg0 = arg0;
		_lastArg1 = arg1;
		_hasApplied = true;
		target.text = string.Format(format, arg0, arg1);
	}
}
