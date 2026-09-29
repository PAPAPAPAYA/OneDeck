using DefaultNamespace.Managers;
using UnityEngine;

/// <summary>
/// Authored root of the prefab shop top bar (plan-shop-hud-prefab-widgets §3.6; mirror
/// prefab-ized 2026-09-24, plan-shop-mirror-prefab). Since 2026-09-29 the ROOT PLACEMENT
/// IS THE PREFAB'S OWN TRANSFORM: OpenPage no longer overwrites transform.position (the
/// ex-ApplyLayout camera formula is retired) — the authored root position must match the
/// shop camera at scroll 0 (scene Camera Man (0,0,-100) + ortho 6.06 → (0, 5.56, -98)),
/// and the bar then scrolls away with the page exactly as before (one-shot placement:
/// the camera moves, the bar stays). OpenPage only derives the band-bottom shelf
/// contract from the final root position and draws an EDITOR-ONLY gizmo: the bandHeight
/// clearance line. The avatar/HP widgets are authored children (Tpl_Avatar / Tpl_HpPill).
/// Zero runtime behavior beyond OpenPage.
/// </summary>
public class ShopHudPage : MonoBehaviour
{
	[Header("Layout Contract (shelf limit + gizmo; hand-keep in sync with visual height)")]
	[Tooltip("Reserved clearance zone at the page top in world units; also the shelf contract limit via ShopChrome.BandBottomWorldY (root.y + bandInsetFromTop - bandHeight).")]
	public float bandHeight = ShopChrome.BandHeightDefault;
	[Tooltip("Legend only (retired camera-formula value; no longer drives placement): keeps the gizmo band line at root.y + bandInsetFromTop - bandHeight.")]
	public float bandInsetFromTop = ShopChrome.BandInsetFromTopDefault;
	[Tooltip("Legend only (retired camera-formula value; no longer drives placement): page z distance the formula added in front of the camera.")]
	public float cameraForwardOffset = ShopChrome.CameraForwardOffsetDefault;

	/// <summary>World Y of the band bottom after OpenPage (the shelf layout contract limit).</summary>
	public float BandBottomWorldY { get; private set; } = float.MaxValue;

	/// <summary>
	/// One-shot build contract capture (page content, scroll-safe). Placement is the
	/// authored prefab root transform (2026-09-29); this derives the band-bottom shelf
	/// contract from it — numerically identical to the retired camera formula. A root
	/// left at the origin can never be a valid placement (the shop camera sits at
	/// z -100), so it warns.
	/// </summary>
	public void OpenPage()
	{
		if (transform.position == Vector3.zero)
		{
			TestManager.LogWarning("[ShopChrome] hudPagePrefab root transform is at the origin — authored placement lost; set the ShopHudPage root position to the shop camera scroll-0 spot");
		}
		BandBottomWorldY = transform.position.y + bandInsetFromTop - bandHeight;
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
