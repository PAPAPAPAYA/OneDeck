using UnityEngine;

// Pins this widget to the camera viewport's top-right corner for as long as the shop
// chrome root is active (plan-shop-options-viewport-pin-2026-09-28). User ruling
// 2026-09-28: this widget is the ONLY chrome element exempt from the 2026-09-21
// "no pinned elements" world-scroll ruling (plan-shop-topbar-world-scroll-2026-09-21).
// 2026-10-03 user ruling (v1.1 round): the pin holds whenever this widget is active —
// the shop viewport corner serves combat as-is (no demo-style two-home flight; the button
// never moves), with unchanged press semantics. RegressionChecklist row 144.
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
		// VISUAL-FIX(2026-10-03): OptionsButton scrolled with the band mid-travel and was
		//   absent in driver-path combat/Result (settled-shop-only pin, 2026-10-02 fix 4).
		//   2026-10-03 user ruling (v1.1 round): the button NEVER moves — its shop viewport
		//   corner serves combat as-is (no demo-style two-home flight), with the same
		//   pressable PhysButton/ShopInputGate feel. This supersedes the settled-shop-only
		//   scope: the pin holds whenever this widget is active (chrome band = shop, travels,
		//   driver-path combat + result). The legacy hard-cut path still hides the whole band
		//   at shop exit, so bypass combat has no button — byte-identical to before
		//   (Review Focus 4). Known side effect (Review Focus 7): the press also fires
		//   CombatManager's any-click confirm in combat/Result — an open product decision,
		//   not handled here.
		//   Cause:    2026-10-02 fix 4 turned the band into scroll-away page content; the
		//             settled-shop pin gate then un-pinned the button for the whole non-shop
		//             stretch of the loop.
		//   Affects:  HudViewportPin.ApplyPin; RegressionChecklist rows 135 (clause
		//             annotated) and 144.
		//   Regress:  离开商店 → combat → Result → shop ×2 — the button stays at the viewport
		//             corner the whole loop (no scroll-away, no landing re-pin pop); a combat
		//             press logs the Options placeholder (see Focus 7 for the reveal side
		//             effect); legacy hard cut (driver off) still hides it in combat.
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
