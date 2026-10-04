using UnityEngine;

/// <summary>
/// Pure math for the phase transition (plan-phase-transition-world-camera-2026-09-21; demo:
/// docs/demo/PhaseTransitionDemo.html). All demo-px conversions and flight-schedule formulas
/// live here and only here so EditMode goldens can pin them 1:1 against the demo.
/// Demo page: 420x740 px, camera covers exactly one page; Unity page height = 2*orthoSize.
/// </summary>
public static class PhaseFlightPlanner
{
	/// <summary>Demo page height in px (PAGE_H, PhaseTransitionDemo.html:536).</summary>
	public const float DemoPageHeightPx = 740f;

	/// <summary>World height of one page: the orthographic camera's full vertical view.</summary>
	public static float PageHeightWorld(float orthographicSize)
	{
		return 2f * orthographicSize;
	}

	/// <summary>Demo px to world units: the demo page maps exactly onto one screen height.</summary>
	public static float PxToWorld(float demoPx, float pageHeightWorld)
	{
		return demoPx / DemoPageHeightPx * pageHeightWorld;
	}

	/// <summary>Demo px to canvas reference px (same page fraction; refHeight = Screen.height / canvas.scaleFactor).</summary>
	public static float DemoPxToCanvasPx(float demoPx, float canvasRefHeightPx)
	{
		return demoPx / DemoPageHeightPx * canvasRefHeightPx;
	}

	/// <summary>
	/// Flight arc apex (demo fly(): midpoint lifted by cardArc, offset 0.5, :676-682).
	/// World y-up: the apex is ABOVE the from/to midpoint.
	/// </summary>
	public static Vector3 ArcApex(Vector3 from, Vector3 to, float arcWorld)
	{
		Vector3 mid = (from + to) * 0.5f;
		mid.y += arcWorld;
		return mid;
	}

	/// <summary>Per-card stagger delay (demo: delay = i * cardStagger, :719).</summary>
	public static float FlightDelay(int index, float stagger)
	{
		return index * stagger;
	}

	/// <summary>Face-down flip time within one flight (demo flipAtMid: delay + transDur/2, :692-694).
	/// With the 2026-10-04 animated flip this is the PINCH moment (FlipRoot scaleX = 0, the back
	/// first appears), not an instant swap.</summary>
	public static float FlipTime(float delay, float duration)
	{
		return delay + duration * 0.5f;
	}

	/// <summary>
	/// Animated-flip START time (2026-10-04 user ruling, supersedes the demo's instant apex
	/// swap / 2026-10-02 fix 6): the squash flip is CENTERED on the arc apex, so the scaleX
	/// pinch — the moment the back first appears — stays at the demo's flipAtMid point while
	/// the back finishes opening during the descent, before the card lands. Clamped so the
	/// flip can never start before the card's own launch, and so it always completes by
	/// landing (delay + duration) whenever the flight is at least one flip long.
	/// </summary>
	public static float FlipStart(float delay, float duration, float flipDuration)
	{
		float ideal = FlipTime(delay, duration) - flipDuration * 0.5f;
		float latest = delay + duration - flipDuration;
		return Mathf.Max(delay, Mathf.Min(ideal, latest));
	}

	/// <summary>Total transition wait: camera duration plus the stagger tail (demo: transDur + 2*cardStagger, :739).</summary>
	public static float TotalDuration(float duration, float stagger)
	{
		return duration + 2f * stagger;
	}

	/// <summary>
	/// Count-aware total transition wait (2026-10-02 audit F3): dummy i lands at
	/// i*stagger + duration, so the wait must cover (cardCount - 1) stagger steps.
	/// cardCount = 3 reproduces the demo wait (:739) byte-for-byte — the fixed 3-card
	/// overload above stays for the demo-heritage 1:1 golden.
	/// </summary>
	public static float TotalDuration(float duration, float stagger, int cardCount)
	{
		return duration + Mathf.Max(0, cardCount - 1) * stagger;
	}

	/// <summary>
	/// World home of a canvas HUD element at a page's camera height (2026-10-02 re-audit
	/// fixes 2/3/7): the Screen Space Camera canvas plane rides the camera, so a rect's
	/// world position when the plane sits at pageY is its current world position shifted
	/// by the PLANE's own Y distance to that page. planeY must be the carrying canvas
	/// root's current world Y — NOT the camera rig's: within the travel-start frame the
	/// rig tween has already moved while the canvas root still lags at its last synced
	/// spot (VISUAL-FIX(2026-10-03) in CombatIconPresenter), and the rig's in-flight
	/// offset would land the flight short by exactly that lag. Pure Y translation —
	/// the transition camera only ever travels vertically.
	/// </summary>
	public static Vector3 HudHomeAtPage(Vector3 rectWorldPos, float pageY, float planeY)
	{
		return rectWorldPos + new Vector3(0f, pageY - planeY, 0f);
	}
}
