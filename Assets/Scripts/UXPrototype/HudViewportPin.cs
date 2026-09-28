using UnityEngine;

// Pins this widget to the camera viewport's top-right corner for as long as the shop
// chrome root is active (plan-shop-options-viewport-pin-2026-09-28). User ruling
// 2026-09-28: this widget is the ONLY chrome element exempt from the 2026-09-21
// "no pinned elements" world-scroll ruling (plan-shop-topbar-world-scroll-2026-09-21) —
// it survives wheel scroll while the rest of the band scrolls away with the page.
//
// World-space transforms have no screen anchors, so the corner is recomputed every
// frame from Camera.main: ortho half width = orthographicSize x aspect. The reader runs
// in LateUpdate because the shop wheel scroll writes the camera from ShopUXManager
// .Update (HandleCameraScroll) — zero frame lag. Insets use EDGE-GAP semantics measured
// against the widget's BoxCollider2D footprint (the v5 chrome is a plain-Transform
// sprite hierarchy — there is no RectTransform to measure; the collider is the authored
// clickable footprint and is deterministic, unlike per-frame renderer bounds). Without a
// collider the insets apply to the transform origin itself. Z is never written (page
// plane and sibling draw order preserved). Camera.main null keeps the last position
// (headless/batch safe). Authored on the OptionsButton node of ShopHudPage.prefab;
// ShopChrome/the loader stay untouched (placement is prefab data, v5 convention).
public class HudViewportPin : MonoBehaviour
{
	[Tooltip("Gap between the viewport RIGHT edge and this widget's BoxCollider2D right edge, world units.")]
	public float insetFromRight = 0.25f;

	[Tooltip("Gap between the viewport TOP edge and this widget's BoxCollider2D top edge, world units. Default 0.105 keeps the v5 band row (bandInsetFromTop 0.5 - collider top delta 0.395).")]
	public float insetFromTop = 0.105f;

	private BoxCollider2D _footprint;

	private void Awake()
	{
		_footprint = GetComponent<BoxCollider2D>();
	}

	private void OnEnable()
	{
		// Re-pin before the first render of a re-shown chrome so the authored prefab
		// position never flashes (OnEnable and LateUpdate both run before the frame's
		// render pass).
		ApplyPin();
	}

	private void LateUpdate()
	{
		ApplyPin();
	}

	private void ApplyPin()
	{
		Camera cam = Camera.main;
		if (cam == null)
		{
			return; // headless/batch: keep the last position
		}
		Vector3 lossy = transform.lossyScale;
		float edgeX = 0f;
		float edgeY = 0f;
		if (_footprint != null)
		{
			edgeX = (_footprint.offset.x + _footprint.size.x * 0.5f) * Mathf.Abs(lossy.x);
			edgeY = (_footprint.offset.y + _footprint.size.y * 0.5f) * Mathf.Abs(lossy.y);
		}
		Vector3 pos = transform.position;
		float cornerX = cam.transform.position.x + cam.orthographicSize * cam.aspect;
		float cornerY = cam.transform.position.y + cam.orthographicSize;
		transform.position = new Vector3(cornerX - insetFromRight - edgeX, cornerY - insetFromTop - edgeY, pos.z);
	}
}
