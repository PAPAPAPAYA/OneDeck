using UnityEngine;

/// <summary>
/// Stores a single color. All project colors should reference a ColorSO asset
/// so colors are defined once and editable in a single place.
/// </summary>
[CreateAssetMenu(fileName = "ColorSO", menuName = "SORefs/ColorSO")]
public class ColorSO : ScriptableObject
{
	public Color value;
	[TextArea]
	public string description;

	/// <summary>Rich-text hex with '#', e.g. "#87CEEB". Used for TMP &lt;color=...&gt; tags.</summary>
	public string Hex => "#" + ColorUtility.ToHtmlStringRGBA(value);

	/// <summary>Opening rich-text tag, e.g. "&lt;color=#87CEEB&gt;". Pair with "&lt;/color&gt;".</summary>
	public string OpenTag => "<color=" + Hex + ">";
}
