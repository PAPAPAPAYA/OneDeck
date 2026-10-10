using UnityEngine;

/// <summary>
/// v1.1 world topo background (demo PhaseTransitionDemo.html buildTopo; docs/PhaseTransition.md
/// "World model"): ONE world-space quad spans both pages plus the config PAD
/// (PhaseTransitionConfigSO.pagePadDemoPx — overshoot headroom), so the OutBack overshoot past
/// either page edge reveals more background instead of a void (2026-10-03 user report: the
/// compare bar's enemy half detached from the screen top at the overshoot peak). The contour
/// pattern is sampled in WORLD space (Custom/ContourLinesBackgroundWorld) and is therefore
/// world-locked — the camera travel carries it like page content and the shop wheel scroll
/// gains the demo's parallax. The region above the combat page center (50% split, user ruling
/// 2026-10-03) is the ENEMY HP display area: base color bound to GameColorPalette.HpBarEnemyColor
/// (ruling: palette-bound); contour lines a fixed 0.55 multiply of it — deliberately darker
/// than the demo's per-channel ~0.78/0.69/0.76 dark red (#7d1826 on #a02332), and overwritten
/// from the palette at runtime anyway. Gray below is the player region (camera-clear gray +
/// teal-tinted lines pre-blended from the retired BackGroundMotion3D material). Static
/// geometry: built once at AfterSceneLoad (the same timing and rig basis as
/// PhaseTransitionDriver.Awake's page capture — before any wheel scroll), never moves, zero
/// per-frame work. Bypass (headless/seed): the red band sits one page above the shop
/// viewport — off-screen; the sheet still renders as the plain background. Replaces the
/// screen-fixed BackGroundMotion3D RawImage under the Global Canvas (disabled in the scene
/// 2026-10-03; the camera clear color stays as the final fallback behind everything).
/// </summary>
// VISUAL-FIX(2026-10-03): Shop→Combat OutBack overshoot peak revealed plain camera background
//   above the HP compare bar's enemy half (user report + screenshot) — the bar is world-pinned
//   page content while the old backdrop was a screen-fixed RawImage, and nothing world-side
//   existed above the combat page edge; the demo covers this with its red PAD (buildTopo: the
//   red rect starts at the world-sheet top, PAD included).
//   Cause:    a screen-fixed backdrop cannot provide world-locked parallax or a world-Y region
//             split, and Shader Graph assets are not script-editable — hence the hand-written
//             world-basis shader + this runtime sheet.
//   Affects:  WorldTopoBackground, Custom/ContourLinesBackgroundWorld, GameScene `background`
//             (disabled). Companion ruling: HudViewportPin row 144 (not this file).
//   Regress:  离开商店 (Overshoot) — at the peak the area above the bar is band-red, no void;
//             Result→shop descent — below the shop page is gray topo; combat cards / reveal
//             zone / HUD all draw OVER the sheet (RegressionChecklist row 143).
public class WorldTopoBackground : MonoBehaviour
{
	private static WorldTopoBackground Me;

	private Material _material;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void AutoCreate()
	{
		if (Me != null) return;
		GameObject go = new GameObject(nameof(WorldTopoBackground));
		go.AddComponent<WorldTopoBackground>();
	}

	private void Awake()
	{
		if (Me != null && Me != this)
		{
			Destroy(gameObject);
			return;
		}
		Me = this;
		Camera cam = Camera.main;
		if (cam == null)
		{
			Debug.LogError("[WorldTopoBackground] No Main Camera at scene load; background not built.");
			enabled = false;
			return;
		}
		float pageH = PhaseFlightPlanner.PageHeightWorld(cam.orthographicSize);
		float rigY = cam.transform.parent != null ? cam.transform.parent.position.y : cam.transform.position.y;
		// Selection page (plan-opponent-select-page-2026-10-06 4.4): the sheet grows one page
		// and the red/gray split climbs to the combat page so the middle page reads all-player.
		float pages = SelectionPages();
		BuildSheet(cam, pageH, rigY, pages);
		if (_material == null) return;
		BindMaterial(pageH, rigY, pages);
		BindRedColor();
	}

	// Sheet spans shop page bottom - PAD -> combat page top + PAD (demo WORLD_H = 2 pages
	// + 2 PADs). Width covers any practical aspect with margin. Z sits two units BEHIND the
	// deck plane on the +z side: the project convention is "smaller z = closer to camera"
	// (CombatUXManager deck z = basePos.z - zOffset * index, scene zOffset 0.5; camera rig at
	// z -100, deck anchor z 0, reveal zone z -5), the deck's back plane is the anchor z and
	// transient new-card spawns reach anchor z + 1, so anchor z + 2 keeps every opaque
	// card/chrome pixel depth-testing over the sheet. (The first draft's deckZ - 2f landed
	// BETWEEN the reveal zone and the deck — in front of every card — and the alpha-1 sheet
	// covered combat content. F1, Review Focus 1.)
	private float SelectionPages()
	{
		var cfg = PhaseTransitionConfigSO.Me;
		return cfg != null && cfg.selectionPageEnabled ? 3f : 2f;
	}

	private void BuildSheet(Camera cam, float pageH, float rigY, float pages)
	{
		var cfg = PhaseTransitionConfigSO.Me;
		float padPx = cfg != null ? cfg.pagePadDemoPx : 200f;
		float pad = PhaseFlightPlanner.PxToWorld(padPx, pageH);
		GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
		Object.Destroy(quad.GetComponent<Collider>());
		quad.name = "TopoSheet";
		Transform t = quad.transform;
		t.SetParent(transform, false);
		float deckZ = 0f;
		var ux = CombatUXManager.me;
		if (ux != null && ux.physicalCardDeckPos != null) deckZ = ux.physicalCardDeckPos.position.z;
		t.position = new Vector3(cam.transform.position.x, rigY + (pages - 1f) * pageH * 0.5f, deckZ + 2f);
		t.localScale = new Vector3(pageH * Mathf.Max(2.5f, cam.aspect + 1f), pages * pageH + 2f * pad, 1f);
		// Build inclusion (load-bearing, F2): the shader is referenced ONLY by this
		// Resources-loaded material — a bare Shader.Find target would be stripped from player
		// builds and the sheet would silently vanish outside the editor. Instantiate before
		// mutating (SetFloat/SetColor below) so the shared Resources asset stays pristine.
		Material loaded = Resources.Load<Material>("Materials/WorldTopoBackground");
		if (loaded == null)
		{
			Debug.LogError("[WorldTopoBackground] Resources material 'Materials/WorldTopoBackground' not found; background not built.");
			Destroy(quad);
			enabled = false;
			return;
		}
		_material = new Material(loaded);
		quad.GetComponent<MeshRenderer>().sharedMaterial = _material;
	}

	// Split = combat page center (50%, user ruling 2026-10-03) = shop page origin + pageH —
	// the same value PhaseTransitionDriver.CombatPageY captures. World pattern density
	// normalizes the screen-basis noise scale by the page height so the look matches the
	// retired screen-fixed backdrop at the default ortho (Levels/NoiseScale/Speed/Intensity
	// defaults mirror Mat_BackGroundMotion3D's serialized values — Review Focus 3, F5).
	private void BindMaterial(float pageH, float rigY, float pages)
	{
		_material.SetFloat("_SplitWorldY", rigY + (pages - 1f) * pageH);
		_material.SetFloat("_WorldScale", _material.GetFloat("_NoiseScale") / pageH);
	}

	// Set-once at Awake — the same runtime convention as the HP presenters (strip colors
	// bind when created); a palette retune applies on the next play session (Review Focus 6).
	private void BindRedColor()
	{
		Color red = GameColorPalette.HpBarEnemyColor;
		_material.SetColor("_RedColor", red);
		_material.SetColor("_LineColorRed", red * 0.55f);
	}
}
