using DG.Tweening;
using UnityEngine;

// Pins this widget to a camera viewport corner for as long as the shop chrome root is
// active (plan-shop-options-viewport-pin-2026-09-28). User ruling 2026-09-28: this widget
// is the ONLY chrome element exempt from the 2026-09-21 "no pinned elements" world-scroll
// ruling (plan-shop-topbar-world-scroll-2026-09-21).
// 2026-10-03 user ruling (v1.1 round): the pin holds whenever this widget is active.
// RegressionChecklist row 144.
// 2026-10-04 user ruling: the combat phase gets its own home — viewport BOTTOM-right,
// governed by insetFromBottom (renamed from insetFromTop, same 0.105 value; insetFromRight
// 0.25 serves both corners). Shop + Result keep the top-right home. Rows 145-146.
// 2026-10-04 follow-up user request: the Shop→Combat home change is a SMOOTH MOVE, not an
// instant swap — while the camera travels to the combat page (Travel == ToCombat; the
// phase already reads Combat at travel start) the button glides between the two viewport
// corners on the shared transDur + ApplyEase, arriving at the bottom-right exactly at the
// landing. Implementation is a per-frame LERP between the two LIVE corner positions driven
// by a 0→1 _cornerBlend tween — NOT a world-space tween — so camera travel, overshoot and
// shop wheel scroll keep working mid-flight. Result entry still snaps back to the top-right
// instantly (return-direction smoothness not requested). RegressionChecklist row 148.
//
// World-space transforms have no screen anchors, so the corners are recomputed every
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
	// 0 = top-right home, 1 = combat bottom-right home. Tweened across the Shop→Combat
	// travel (2026-10-04 smooth move), snapped everywhere else.
	private float _cornerBlend;
	private Tween _blendTween;

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

	private void OnDisable()
	{
		KillBlendTween();
	}

	private void LateUpdate()
	{
		if (_phaseManager == null)
		{
			_phaseManager = FindFirstObjectByType<PhaseManager>();
		}
		UpdateCornerBlend();
		ApplyPin();
	}

	private void UpdateCornerBlend()
	{
		bool combatHome = _phaseManager != null
			&& _phaseManager.currentGamePhaseRef != null
			&& _phaseManager.currentGamePhaseRef.Value() == EnumStorage.GamePhase.Combat;
		if (!combatHome)
		{
			// Shop + Result + the Result→Shop travel: top-right home, restored instantly
			// (return-direction smoothness was not requested).
			KillBlendTween();
			_cornerBlend = 0f;
			return;
		}
		if (PhaseTransitionDriver.Travel == PhaseTransitionDriver.TransitionTravel.ToCombat)
		{
			// Fly during the camera travel: start once, same duration + shared ease as the
			// rig DOMoveY so the button reaches the bottom-right exactly at the landing
			// (OutBack overshoot momentarily carries it past the corner, like every flight).
			if (_cornerBlend < 1f && (_blendTween == null || !_blendTween.IsActive()))
			{
				var cfg = PhaseTransitionConfigSO.Me;
				float dur = cfg != null ? Mathf.Max(0.05f, cfg.transDur) : 0.8f;
				_blendTween = DOTween.To(() => _cornerBlend, v => _cornerBlend = v, 1f, dur);
				if (cfg != null)
				{
					cfg.ApplyEase(_blendTween);
				}
				else
				{
					_blendTween.SetEase(Ease.OutBack);
				}
				_blendTween.SetUpdate(UpdateType.Normal, true);
			}
			return;
		}
		// Combat without a ToCombat travel: legacy hard cut (band hidden there anyway),
		// tutorial start, or the blend tween already finished before the travel flag
		// dropped. Snap to the combat home.
		KillBlendTween();
		_cornerBlend = 1f;
	}

	private void KillBlendTween()
	{
		if (_blendTween != null && _blendTween.IsActive())
		{
			_blendTween.Kill();
		}
		_blendTween = null;
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
		//   Amended 2026-10-04 again (user request): the landing swap became a smooth glide
		//   across the travel (row 148) — the corner positions below are blended live via
		//   _cornerBlend; see UpdateCornerBlend.
		Camera cam = Camera.main;
		if (cam == null)
		{
			return; // headless/batch: keep the last position
		}
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
		float topY = cam.transform.position.y + cam.orthographicSize - insetFromBottom - topEdgeY;
		float bottomY = cam.transform.position.y - cam.orthographicSize + insetFromBottom + bottomEdgeY;
		float y = Mathf.Lerp(topY, bottomY, _cornerBlend);
		transform.position = new Vector3(cornerX - insetFromRight - edgeX, y, pos.z);
	}
}
