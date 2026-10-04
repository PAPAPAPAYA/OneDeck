using UnityEngine;

// Pins this widget to a camera viewport corner for as long as the shop chrome root is
// active (plan-shop-options-viewport-pin-2026-09-28). User ruling 2026-09-28: this widget
// is the ONLY chrome element exempt from the 2026-09-21 "no pinned elements" world-scroll
// ruling (plan-shop-topbar-world-scroll-2026-09-21).
// 2026-10-03 user ruling (v1.1 round): the pin holds whenever this widget is active —
// no demo-style two-home flight. RegressionChecklist row 144.
// 2026-10-04 user ruling: the combat phase gets its own home — viewport BOTTOM-right,
// governed by insetFromBottom (renamed from insetFromTop, same 0.105 value; insetFromRight
// 0.25 serves both corners). Shop + Result keep the top-right home, and a travel keeps the
// departure corner until landing (!PhaseTransitionDriver.IsTransitioning — the phase flips
// to Combat at travel START), so the corner swap is a single instant jump at the combat
// landing, never a mid-flight move. RegressionChecklist row 146.
//
// World-space transforms have no screen anchors, so the corner is recomputed every
// frame from Camera.main: ortho half height = orthographicSize, half width = size x aspect.
// The reader runs in LateUpdate because the shop wheel scroll writes the camera from
// ShopUXManager.Update (HandleCameraScroll) — zero frame lag. Insets use EDGE-GAP
// semantics measured against the widget's BoxCollider2D footprint (the v5 chrome is a
// plain-Transform sprite hierarchy — there is no RectTransform to measure; the collider is
// the authored clickable footprint and is deterministic, unlike per-frame renderer bounds).
// Without a collider the insets apply to the transform origin itself. Z is never written
// (page plane and sibling draw order preserved). Camera.main null keeps the last position
// (headless/batch safe). PhaseManager is a cached scene lookup and fully null-safe: no
// manager (headless rigs) keeps the top-right home. Authored on the OptionsButton node of
// ShopHudPage.prefab; ShopChrome/the loader stay untouched (placement is prefab data, v5
// convention).
public class HudViewportPin : MonoBehaviour
{
	[Tooltip("Gap between the viewport RIGHT edge and this widget's BoxCollider2D right edge, world units.")]
	public float insetFromRight = 0.25f;

	[Tooltip("Vertical edge gap of the active home, world units: measured from the viewport TOP edge on the top-right home (shop/Result/travel), from the BOTTOM edge on the combat bottom-right home (2026-10-04). Renamed from insetFromTop; value 0.105 unchanged.")]
	public float insetFromBottom = 0.105f;

	private BoxCollider2D _footprint;
	private PhaseManager _phaseManager;

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
		//   Amended 2026-10-04 (user request): the corner clause above is superseded — the
		//   combat home is now the BOTTOM-right corner (row 146); shop/Result/travel keep
		//   the top-right home exactly as ruled here.
		Camera cam = Camera.main;
		if (cam == null)
		{
			return; // headless/batch: keep the last position
		}
		if (_phaseManager == null)
		{
			_phaseManager = FindFirstObjectByType<PhaseManager>();
		}
		// Combat-phase home (2026-10-04): bottom-right once the camera has landed on the
		// combat page. The phase flips to Combat at travel START, so IsTransitioning holds
		// the departure (top-right) corner through the flight; shop, Result and every
		// travel read top-right.
		bool pinBottom = _phaseManager != null
			&& _phaseManager.currentGamePhaseRef != null
			&& _phaseManager.currentGamePhaseRef.Value() == EnumStorage.GamePhase.Combat
			&& !PhaseTransitionDriver.IsTransitioning;
		Vector3 lossy = transform.lossyScale;
		float edgeX = 0f;
		float topEdgeY = 0f;
		float bottomEdgeY = 0f;
		if (_footprint != null)
		{
			edgeX = (_footprint.offset.x + _footprint.size.x * 0.5f) * Mathf.Abs(lossy.x);
			topEdgeY = (_footprint.offset.y + _footprint.size.y * 0.5f) * Mathf.Abs(lossy.y);
			bottomEdgeY = (_footprint.size.y * 0.5f - _footprint.offset.y) * Mathf.Abs(lossy.y);
		}
		Vector3 pos = transform.position;
		float cornerX = cam.transform.position.x + cam.orthographicSize * cam.aspect;
		float y;
		if (pinBottom)
		{
			float bottomY = cam.transform.position.y - cam.orthographicSize;
			y = bottomY + insetFromBottom + bottomEdgeY;
		}
		else
		{
			float topY = cam.transform.position.y + cam.orthographicSize;
			y = topY - insetFromBottom - topEdgeY;
		}
		transform.position = new Vector3(cornerX - insetFromRight - edgeX, y, pos.z);
	}
}
