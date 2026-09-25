using UnityEngine;

/// <summary>
/// Per-widget shop scale shared with the canvas HUD pieces (plan-shop-mirror-prefab
/// §3.4): the authored Tpl_Avatar / Tpl_HpPill roots carry the scale the canvas
/// avatar/HP pieces glide to in the Shop phase — position AND scale now have one home
/// (the prefab widget), with ShopChrome forwarding the value. Replaces the deleted
/// ShopLayoutConfigSO playerIconShopScale / hpDisplayShopScale fields.
/// </summary>
public class ShopMirrorScale : MonoBehaviour
{
	[Tooltip("Shop-phase scale the canvas avatar/HP piece glides to (single source: edit here, both systems follow).")]
	public float canvasShopScale = 0.41f;
}
