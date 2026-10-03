using DG.Tweening;
using UnityEngine;

/// <summary>
/// World-space flight of one canvas HUD element during a phase transition (2026-10-02
/// re-audit fixes 2/3/7, docs/PhaseTransition.md): the demo (PhaseTransitionDemo.html
/// flyShared, :701-729) flies the shared HUD pieces — player avatar + HP pill, enemy HUD —
/// as WORLD content between their two page homes with no scale change, so the camera's
/// overshoot carries them on screen like any other world object. Unity's HUD is
/// canvas-rendered, so the flight tweens a WORLD point and Tick() (call from LateUpdate)
/// re-projects it onto the canvas rect every frame (world-locked rendering). Writes go to
/// RectTransform.position only — anchoredPosition tracks the world writes, so the normal
/// phase placement writes resume seamlessly once the flight hands back.
/// Home math: PhaseFlightPlanner.HudHomeAtPage (the canvas plane rides the camera, so a
/// page-home world position is the rect's current world position shifted by the rig's Y
/// distance to that page). Element homes: ShopChrome.TryGetAvatarWorldCenter /
/// TryGetHpPillWorldCenter (shop top bar) and the captured combat anchors (combat page).
/// </summary>
public class HudWorldFlight
{
	private readonly RectTransform _rt;
	private readonly Canvas _canvas;
	private readonly RectTransform _canvasRoot;
	private readonly Camera _cam;
	// Pivot -> rect center in world space, captured at Begin. Constant for the whole
	// flight: no rotation and (fix 7) no scale change, so one capture serves.
	private Vector3 _centerOffset;
	// Tweened rect-center world point.
	private Vector3 _center;
	private Tween _tween;
	private bool _following;

	public bool Active => _following;

	public HudWorldFlight(RectTransform rt, Canvas canvas)
	{
		_rt = rt;
		_canvas = canvas != null ? canvas.rootCanvas : null;
		_canvasRoot = _canvas != null ? _canvas.transform as RectTransform : null;
		_cam = ResolveCanvasCamera(_canvas);
	}

	/// <summary>Projection camera: the canvas' own camera, else Camera.main. Null only headless (flights are skipped there).</summary>
	private static Camera ResolveCanvasCamera(Canvas canvas)
	{
		if (canvas != null && canvas.worldCamera != null) return canvas.worldCamera;
		return Camera.main;
	}

	/// <summary>
	/// Starts a straight world flight of the rect center from -> to (shared ease, unscaled
	/// time — phase-boundary motion is never combat-speed-scaled). No scale change (demo
	/// flyShared): the rect renders at whatever scale the caller snapped it to beforehand.
	/// onComplete (optional) fires when the flight tween completes — the 2026-10-03 handoff
	/// seam uses it for the same-frame mirror swap at flight end.
	/// </summary>
	public void Begin(Vector3 fromCenterWorld, Vector3 toCenterWorld, float duration, PhaseTransitionConfigSO cfg, System.Action onComplete = null)
	{
		Kill();
		if (_rt == null || _canvasRoot == null || _cam == null) return;
		_centerOffset = _rt.TransformPoint(_rt.rect.center) - _rt.position;
		_center = fromCenterWorld;
		ApplyWorldPoint();
		_following = true;
		Tween tween = DOTween.To(() => _center, v => _center = v, toCenterWorld, Mathf.Max(0.05f, duration));
		if (cfg != null) cfg.ApplyEase(tween);
		else tween.SetEase(Ease.OutBack);
		_tween = tween.SetUpdate(UpdateType.Normal, true);
		_tween.OnComplete(() =>
		{
			// Clear the ownership flag on completion (kept from before the 2026-10-03 handoff
			// seam — leaving it set made the anchored placement stand-down bookkeeping read a
			// stale Active after landing), then run the caller's hook (mirror swap).
			_following = false;
			if (onComplete != null) onComplete();
		});
	}

	/// <summary>
	/// World-locks the rect at a fixed world point (no tween) until Kill() — the camera's
	/// own motion then carries the element exactly like page content (2026-10-03 compare-bar
	/// pin). Same re-projection path as Begin.
	/// </summary>
	public void Pin(Vector3 centerWorld)
	{
		Kill();
		if (_rt == null || _canvasRoot == null || _cam == null) return;
		_centerOffset = _rt.TransformPoint(_rt.rect.center) - _rt.position;
		_center = centerWorld;
		ApplyWorldPoint();
		_following = true;
	}

	/// <summary>Stops the flight. The rect keeps its current rendered pose; normal placement writes take over.</summary>
	public void Kill()
	{
		if (_tween != null && _tween.IsActive()) _tween.Kill();
		_tween = null;
		_following = false;
	}

	/// <summary>Call from LateUpdate while active: re-project the tweened world point onto the canvas rect.</summary>
	public void Tick()
	{
		if (_following) ApplyWorldPoint();
	}

	private void ApplyWorldPoint()
	{
		Vector3 screen = _cam.WorldToScreenPoint(_center - _centerOffset);
		// Overlay canvases take a null camera in the rect mapping; Screen Space - Camera
		// needs the canvas camera — the raw screen-pixel assignment is the known SSC
		// positioning bug (CardTagTooltip, 2026-09).
		Camera uiCam = _canvas != null && _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _cam;
		if (RectTransformUtility.ScreenPointToWorldPointInRectangle(_canvasRoot, new Vector2(screen.x, screen.y), uiCam, out Vector3 world))
		{
			_rt.position = world;
		}
	}

	/// <summary>World Y of the camera rig (the driver's rig resolution: camera parent, else the camera itself).</summary>
	public static float RigY
	{
		get
		{
			Camera cam = Camera.main;
			if (cam == null) return 0f;
			Transform rig = cam.transform.parent != null ? cam.transform.parent : cam.transform;
			return rig.position.y;
		}
	}
}
