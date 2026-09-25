using UnityEngine;

/// <summary>
/// Authored root of the prefab shop top bar (plan-shop-hud-prefab-widgets §3.6; mirror
/// prefab-ized 2026-09-24, plan-shop-mirror-prefab). Carries the three layout-contract
/// values that cannot be expressed by the prefab transform alone (root placement needs
/// runtime rig/ortho capture), applies the one-shot root placement (ex-ShopChrome
/// .ApplyLayout :249-253 formula), and draws an EDITOR-ONLY gizmo: the bandHeight
/// clearance line. The avatar/HP widgets are authored children (Tpl_Avatar / Tpl_HpPill)
/// and need no placeholder frames anymore. Zero runtime behavior beyond OpenPage.
/// </summary>
public class ShopHudPage : MonoBehaviour
{
	[Header("Layout Contract (shelf limit + root placement; hand-keep in sync with visual height)")]
	[Tooltip("Reserved clearance zone at the page top in world units; also the shelf contract limit via ShopChrome.BandBottomWorldY.")]
	public float bandHeight = ShopChrome.BandHeightDefault;
	[Tooltip("Page root center below the viewport top edge, world units.")]
	public float bandInsetFromTop = ShopChrome.BandInsetFromTopDefault;
	[Tooltip("Page z distance in front of the camera, world units.")]
	public float cameraForwardOffset = ShopChrome.CameraForwardOffsetDefault;

	/// <summary>World Y of the band bottom after OpenPage (the shelf layout contract limit).</summary>
	public float BandBottomWorldY { get; private set; } = float.MaxValue;

	/// <summary>
	/// One-shot root placement at build time (page content, scroll-safe): the old
	/// ApplyLayout root formula verbatim, plus the captured band-bottom contract value.
	/// </summary>
	public void OpenPage(Vector3 rigPos, float orthoSize)
	{
		transform.position = new Vector3(rigPos.x, rigPos.y + orthoSize - bandInsetFromTop, rigPos.z + cameraForwardOffset);
		BandBottomWorldY = rigPos.y + orthoSize - bandHeight;
	}

#if UNITY_EDITOR
	private void OnDrawGizmos()
	{
		Vector3 origin = transform.position;
		const float halfW = DesignOrthoSize * 16f / 9f;
		// Band clearance line: the shelf must stay below it (bandHeight is a hand-kept contract).
		Gizmos.color = new Color(1f, 0.6f, 0f, 0.9f);
		Gizmos.DrawLine(
			origin + new Vector3(-halfW, bandInsetFromTop - bandHeight, 0f),
			origin + new Vector3(halfW, bandInsetFromTop - bandHeight, 0f));
	}

	private const float DesignOrthoSize = 6.06f;
#endif
}
