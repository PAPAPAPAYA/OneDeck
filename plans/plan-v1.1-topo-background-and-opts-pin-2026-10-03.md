# v1.1 World Topo Background + OptionsButton Always-Pin Implementation Plan

> **For agentic workers:** implement task-by-task in order; steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port the demo's v1.1 world topo background (ONE sheet spanning both pages + PAD headroom, red band = enemy HP display area) so the OutBack overshoot past either page edge reveals more background instead of a void (2026-10-03 user report: the compare bar's enemy half detached from the screen top at the overshoot peak). Give the ❚❚ OptionsButton its v1.1 resolution per the user ruling: it never flies and never moves — the shop viewport corner serves combat as-is, with the same pressable feel.

**Architecture:** Two seams. (1) The current background is SCREEN-FIXED: a Global-Canvas RawImage (`Mat_BackGroundMotion3D`, line-only Shader Graph, bg alpha 0) over the Main Camera's solid clear color (0.741, 0.729, 0.702). It cannot do world-locked parallax or a world-Y region split, and Shader Graph assets are not script-editable — so a new hand-written world-basis shader (`Custom/ContourLinesBackgroundWorld`, noise family copied verbatim from `Custom/ContourLinesBackground`) renders on ONE runtime-built world quad (`WorldTopoBackground`, driver-style `RuntimeInitializeOnLoadMethod` boot): sheet spans shop bottom − PAD → combat top + PAD, red region above the combat page center. The old RawImage is disabled in the scene (rollback = re-enable); the camera clear color stays as the final fallback. (2) `HudViewportPin` drops its settled-shop-only gate (2026-10-02 fix 4) per the 2026-10-03 ruling: the pin holds whenever the widget is active, so the button stays at the corner through travels, combat and result instead of scrolling with the band.

**Tech Stack:** Unity 6, URP HLSL (hand-written, no Shader Graph), `GameColorPalette.HpBarEnemyColor`, `PhaseTransitionConfigSO.pagePadDemoPx` (PAD 200 px — documented "v1.1 world topo only"), `PhaseFlightPlanner.PageHeightWorld` / `PxToWorld`, driver-style `RuntimeInitializeOnLoadMethod.AfterSceneLoad` boot.

**Spec:** `docs/demo/PhaseTransitionDemo.html` — `buildTopo` (:544-580): one continuous sheet, `PAD = 200` px beyond each page edge ("overshoot headroom"), red rect from the WORLD-TOP (PAD included) down to `COMBAT_TOP + 407` (top ~55% of the combat page), contour strokes dark-red inside the red region — i.e. the overshoot past the combat page reveals MORE RED, never void. User rulings 2026-10-03 (this round):

- **R1 (❚❚):** no flight, no second home — the shop viewport-pinned coordinates serve combat directly; keep the same pressable feel (PhysButton + ShopInputGate semantics unchanged; combat presses hit the `ShopHudBinder` Options placeholder log, gate is free outside transitions).
- **R2 (red):** band base color = `GameColorPalette.HpBarEnemyColor` (palette-bound, set-once at Awake — same runtime convention as the HP presenters).
- **R3 (split):** red/gray boundary at **50%** of the combat page (= combat page center world Y), not the demo's 55%.

## Global Constraints

- Line endings CRLF, tab indentation, English comments/docs (AGENTS.md).
- New/edited visual code in `UXPrototype/` carries `VISUAL-FIX(2026-10-03):` blocks; append RegressionChecklist rows 143, 144 (141/142 taken); annotate (not delete) row 135's superseded OptionsButton clause.
- After every `.cs` edit: `refresh_unity` (`compile: request`), confirm `isCompiling == false` AND `Assembly-CSharp.dll` mtime > edited source mtime before `run_tests`. Shader edits: `refresh_unity` + `read_console` error check (shader errors surface in the console, not the C# gate).
- Before every `run_tests`: `EditorSceneManager.SaveOpenScenes()` (pre-approved) as the immediately preceding step; EditMode full suite `init_timeout: 180000`.
- Scene edit (Task 3) via `execute_code` + `SaveOpenScenes` (pre-approved); disable, never delete, the old RawImage.
- Commit straight to `main`, one commit per task.
- All transition tweens run unscaled time — untouched here; the sheet is static geometry with zero per-frame work.
- Deterministic-RNG rule untouched: the shader noise is seeded implicitly by its hash function (no `UnityEngine.Random` anywhere).

## Review Focus

1. **Quad Z sign:** the float stack steps BACK slots at `-0.01f * i`, so "farther from camera" = more negative Z; the sheet sits at `deckZ - 2f`. If the camera convention is ever inverted the sheet would cover the combat content (symptom: cards invisible) — Play matrix step 3 catches it; flip the offset sign if so.
2. **Split basis:** `rigY` is captured at `AfterSceneLoad` BEFORE any wheel scroll — the same basis `PhaseTransitionDriver.Awake` uses for `ShopPageY`. Play matrix step 2 confirms the band bottom lands at the viewport center on combat landing.
3. **Look parity:** the contour density/character is a starting value set (Levels 6 / world-scaled noise / Intensity 0.5) that will need ONE eyeball-tune pass against the retired BackGroundMotion3D look (`_Levels` / `_WorldScale` / `_Intensity` on the material; Play matrix step 6). This is the only non-deterministic item in the plan.
4. **Bypass parity:** in the legacy hard-cut path the whole band hides at shop exit, so bypass combat has NO OptionsButton (unchanged); the sheet still renders (red band one page above the shop viewport — off-screen) and the pattern basis changes from screen-locked to world-locked (intended, Play matrix step 7).
5. **Result phase:** the band root stays active through Result (driver path), so the pinned button remains at the corner, behind the Result overlay where they overlap — matches the demo's placement semantics; no code gates it.
6. **Palette timing:** set-once at Awake; a `HpBarEnemyColor` retune applies on the next play session (identical to how the HP presenters bind strip colors at creation).

---

### Task 1: `Custom/ContourLinesBackgroundWorld` shader (new file)

**Files:**
- Create: `Assets/Shaders/ContourLinesBackgroundWorld.shader`

- [ ] **Step 1: Write the shader**

World-basis port of `Custom/ContourLinesBackground` (noise functions copied verbatim) plus the enemy band. Region colors: player side = the current camera-clear gray with the teal-tinted line color pre-blended from the retired graph's values (`lerp(0.741/0.729/0.702, 0.102/0.188/0.216, 0.749)`); enemy side defaults = demo `#a02332` / `#7d1826` (overwritten from the palette at runtime).

```shader
Shader "Custom/ContourLinesBackgroundWorld"
{
	Properties
	{
		_BgColor("Player Region Background", Color) = (0.7411765, 0.7294118, 0.7019608, 1)
		_LineColor("Player Region Line Color", Color) = (0.2625, 0.3239, 0.3378, 1)
		_RedColor("Enemy Region Background", Color) = (0.627451, 0.137255, 0.196078, 1)
		_LineColorRed("Enemy Region Line Color", Color) = (0.345098, 0.075498, 0.107843, 1)
		_SplitWorldY("Enemy Band Bottom World Y", Float) = 12.12
		_WorldScale("Noise Cycles Per World Unit", Float) = 0.357
		_Levels("Contour Levels", Float) = 6
		_LineWidth("Line Width", Float) = 3
		_NoiseScale("Noise Scale", Float) = 4.33
		_Speed("Morph Speed", Float) = 0.05
		_Intensity("Line Intensity", Range(0, 1)) = 0.5
	}

	SubShader
	{
		Tags
		{
			"RenderType" = "Transparent"
			"Queue" = "Transparent"
			"RenderPipeline" = "UniversalPipeline"
			"IgnoreProjector" = "True"
			"PreviewType" = "Plane"
		}

		Pass
		{
			Name "ContourLinesBackgroundWorld"

			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			Cull Off

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			struct Attributes
			{
				float4 positionOS : POSITION;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float3 positionWS : TEXCOORD0;
			};

			CBUFFER_START(UnityPerMaterial)
				float4 _BgColor;
				float4 _LineColor;
				float4 _RedColor;
				float4 _LineColorRed;
				float _SplitWorldY;
				float _WorldScale;
				float _Levels;
				float _LineWidth;
				float _NoiseScale;
				float _Speed;
				float _Intensity;
			CBUFFER_END

			// Hash-based pseudo-random gradient in [-1, 1]^2 (iq-style hash22) — identical
			// noise family to Custom/ContourLinesBackground so the contour character carries over.
			float2 Hash22(float2 p)
			{
				float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
				p3 += dot(p3, p3.yzx + 33.33);
				return frac((p3.xx + p3.yz) * p3.zy) * 2.0 - 1.0;
			}

			// Classic 2D gradient (Perlin-style) noise, roughly in [-1, 1].
			float GradientNoise(float2 p)
			{
				float2 i = floor(p);
				float2 f = frac(p);
				float2 u = f * f * (3.0 - 2.0 * f);
				float a = dot(Hash22(i), f);
				float b = dot(Hash22(i + float2(1.0, 0.0)), f - float2(1.0, 0.0));
				float c = dot(Hash22(i + float2(0.0, 1.0)), f - float2(0.0, 1.0));
				float d = dot(Hash22(i + float2(1.0, 1.0)), f - float2(1.0, 1.0));
				return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
			}

			// 3-octave fbm for the terrain height field. Gentle amplitude decay keeps contours smooth and rounded.
			float Fbm(float2 p)
			{
				float sum = 0.0;
				float amp = 0.5;
				for (int octave = 0; octave < 3; octave++)
				{
					sum += amp * GradientNoise(p);
					p = p * 2.03 + float2(17.3, 9.1);
					amp *= 0.35;
				}
				return sum;
			}

			Varyings vert(Attributes input)
			{
				Varyings output;
				output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
				output.positionCS = TransformWorldToHClip(output.positionWS);
				return output;
			}

			half4 frag(Varyings input) : SV_Target
			{
				// World-space basis: the pattern is world-locked — the camera travel carries it
				// like page content and the shop wheel scroll gains the demo's parallax. Density
				// is normalized by the binder (_WorldScale = _NoiseScale / pageH) so the look
				// matches the retired screen-fixed backdrop at the default ortho.
				float2 p = input.positionWS.xy * _WorldScale;

				// Slow in-place morph: bounded sinusoidal domain warp, no net drift (kept from
				// the retired backdrop — an accepted static-ness deviation from the demo).
				float t = _Time.y * _Speed;
				float2 warpPhase = float2(sin(t), cos(t * 0.83)) * 1.5;
				float2 warp = float2(
					GradientNoise(p * 0.5 + warpPhase),
					GradientNoise(p * 0.5 - warpPhase + float2(31.7, 11.3)));

				float h = Fbm(p + 0.35 * warp) * 0.5 + 0.5;

				// Anti-aliased iso-lines at _Levels height intervals.
				float v = h * _Levels;
				float fw = fwidth(v);
				float distToLine = abs(frac(v + 0.5) - 0.5);
				float lineMask = 1.0 - smoothstep(0.0, fw * _LineWidth, distToLine);

				// Enemy band: world Y above the split (combat page center, 50% — user ruling
				// 2026-10-03). fwidth AA on the boundary keeps the edge clean under overshoot.
				float aa = fwidth(input.positionWS.y);
				float redMask = smoothstep(_SplitWorldY - aa, _SplitWorldY + aa, input.positionWS.y);
				float3 bg = lerp(_BgColor.rgb, _RedColor.rgb, redMask);
				float3 lineCol = lerp(_LineColor.rgb, _LineColorRed.rgb, redMask);

				float3 col = lerp(bg, lineCol, lineMask * _Intensity);
				return half4(col, _BgColor.a);
			}
			ENDHLSL
		}
	}
}
```

- [ ] **Step 2: Refresh + console check**

`refresh_unity` (`mode: force`, `scope: all`); `read_console` errors — expected: no shader compile errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/Shaders/ContourLinesBackgroundWorld.shader
git commit -m "v1.1: world-basis contour background shader with enemy band (red pad headroom)"
```

### Task 2: `WorldTopoBackground` runtime sheet builder (new file)

**Files:**
- Create: `Assets/Scripts/UXPrototype/WorldTopoBackground.cs`

**Interfaces:**
- Consumes: `PhaseTransitionConfigSO.Me.pagePadDemoPx`; `PhaseFlightPlanner.PageHeightWorld` / `PxToWorld`; `CombatUXManager.me.physicalCardDeckPos` (Z basis only); `GameColorPalette.HpBarEnemyColor`.
- Produces: nothing public. Static boot mirrors `PhaseTransitionDriver.AutoCreate`.

- [ ] **Step 1: Write the component**

```csharp
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
/// (ruling: palette-bound), contour lines a fixed 0.55 multiply of it (demo #7d1826 on #a02332);
/// gray below is the player region (camera-clear gray + teal-tinted lines matching the retired
/// BackGroundMotion3D look). Static geometry: built once at AfterSceneLoad (the same timing and
/// rig basis as PhaseTransitionDriver.Awake's page capture — before any wheel scroll), never
/// moves, zero per-frame work. Bypass (headless/seed): the red band sits one page above the shop
/// viewport — off-screen; the sheet still renders as the plain background. Replaces the
/// screen-fixed BackGroundMotion3D RawImage under the Global Canvas (disabled in the scene
/// 2026-10-03; the camera clear color stays as the final fallback behind everything).
/// </summary>
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
		BuildSheet(cam, pageH, rigY);
		if (_material == null) return;
		BindMaterial(pageH, rigY);
		BindRedColor();
	}

	// Sheet spans shop page bottom - PAD -> combat page top + PAD (demo WORLD_H = 2 pages
	// + 2 PADs). Width covers any practical aspect with margin. Z sits two units farther
	// from the camera than the deck plane (the float stack's -z back direction), so every
	// opaque card/chrome pixel depth-tests over the sheet.
	private void BuildSheet(Camera cam, float pageH, float rigY)
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
		t.position = new Vector3(cam.transform.position.x, rigY + pageH * 0.5f, deckZ - 2f);
		t.localScale = new Vector3(pageH * Mathf.Max(2.5f, cam.aspect + 1f), 2f * pageH + 2f * pad, 1f);
		Shader shader = Shader.Find("Custom/ContourLinesBackgroundWorld");
		if (shader == null)
		{
			Debug.LogError("[WorldTopoBackground] ContourLinesBackgroundWorld shader not found; background not built.");
			Destroy(quad);
			enabled = false;
			return;
		}
		_material = new Material(shader);
		quad.GetComponent<MeshRenderer>().sharedMaterial = _material;
	}

	// Split = combat page center (50%, user ruling 2026-10-03) = shop page origin + pageH —
	// the same value PhaseTransitionDriver.CombatPageY captures. World pattern density
	// normalizes the screen-basis noise scale by the page height so the look matches the
	// retired screen-fixed backdrop at the default ortho.
	private void BindMaterial(float pageH, float rigY)
	{
		_material.SetFloat("_SplitWorldY", rigY + pageH);
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
```

- [ ] **Step 2: Refresh + compile gate + full EditMode suite**

`refresh_unity` (`compile: request`) → `isCompiling == false` AND assembly mtime > source mtime → `SaveOpenScenes()` → `run_tests` (EditMode full, `init_timeout: 180000`). Expected: green, same totals (676 passed / 1 pre-existing skip; nothing exercises this class).

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UXPrototype/WorldTopoBackground.cs
git commit -m "v1.1: WorldTopoBackground runtime sheet builder (both pages + PAD, palette-bound band)"
```

### Task 3: Retire the screen-fixed backdrop (scene edit)

**Files:**
- Modify: `Assets/Scenes/GameScene.unity` (disable GameObject `background`, id 567038032, under Global Canvas)

- [ ] **Step 1: Disable via execute_code + save (pre-approved)**

```csharp
GameObject bg = GameObject.Find("background");
if (bg == null) return "background NOT FOUND";
string info = "active=" + bg.activeSelf + " parent=" + bg.transform.parent.name;
bg.SetActive(false);
UnityEditor.EditorUtility.SetDirty(bg);
return info + " -> disabled";
```
then `EditorSceneManager.SaveOpenScenes()` and `git status` must show `Assets/Scenes/GameScene.unity` modified.

- [ ] **Step 2: Commit**

```bash
git add Assets/Scenes/GameScene.unity
git commit -m "v1.1: retire screen-fixed background RawImage (world topo sheet replaces it; rollback = re-enable)"
```

### Task 4: OptionsButton always-pin (user ruling R1)

**Files:**
- Modify: `Assets/Scripts/UXPrototype/HudViewportPin.cs` (`ApplyPin` gate, class doc header)

**Interfaces:**
- Consumes: nothing new. `PhysButton` press semantics (`ShopInputGate.Blocked` checks) are untouched — the gate is free outside transitions, so combat presses reach the `ShopHudBinder` Options placeholder log with the identical hover/press feel.

- [ ] **Step 1: Ungate `ApplyPin` + update the class doc**

In the class header comment, replace the sentence "it survives wheel scroll while the rest of the band scrolls away with the page." with:

```
// 2026-10-03 user ruling (v1.1 round): the pin holds whenever this widget is active —
// the shop viewport corner serves combat as-is (no demo-style two-home flight; the button
// never moves), with unchanged press semantics. RegressionChecklist row 144.
```

Replace the `ApplyPin` head (the 2026-10-02 comment block + the two early-returns, `HudViewportPin.cs:50-61`) with:

```csharp
	private void ApplyPin()
	{
		// 2026-10-03 user ruling (v1.1 round): the button NEVER moves — its shop viewport
		// corner serves combat as-is (no demo-style two-home flight), with the same
		// pressable PhysButton/ShopInputGate feel. This supersedes both the settled-shop-only
		// scope (2026-10-02 fix 4) and the mid-travel scroll-with-the-band behavior: the pin
		// holds whenever this widget is active (chrome band = shop, travels, driver-path
		// combat + result). The legacy hard-cut path still hides the whole band at shop exit,
		// so bypass combat has no button — byte-identical to before (Review Focus 4).
		Camera cam = Camera.main;
		if (cam == null)
		{
			return; // headless/batch: keep the last position
		}
```

(the rest of `ApplyPin` — collider edge math and the corner write — is unchanged.)

- [ ] **Step 2: Refresh + compile gate + full EditMode suite**

Same gate as Task 2 Step 2. Expected: green, same totals.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UXPrototype/HudViewportPin.cs
git commit -m "v1.1: OptionsButton always-pinned (2026-10-03 ruling — shop corner serves combat, no flight)"
```

### Task 5: Docs + regression rows

**Files:**
- Modify: `docs/PhaseTransition.md` (world model, deviations 1/5, arbitration)
- Modify: `docs/RegressionChecklist.md` (rows 143, 144; row 135 annotation)

- [ ] **Step 1: `docs/PhaseTransition.md`**

- World model section, after the transition paragraph: add — "The v1.1 world topo background (2026-10-03) is ONE world-space sheet (`WorldTopoBackground` + `Custom/ContourLinesBackgroundWorld`) spanning both pages plus the config PAD (`pagePadDemoPx`, overshoot headroom). The region above the combat page center (50%, user ruling) is the enemy HP display area — `GameColorPalette.HpBarEnemyColor` base, dark-red contour lines; gray below is the player region. The pattern is world-locked (the travel carries it; shop wheel scroll gains the demo's parallax). It replaces the screen-fixed BackGroundMotion3D RawImage under the Global Canvas (disabled; camera clear stays as fallback), and is what makes the overshoot reveal more background instead of a void."
- Accepted deviation 1: replace with — "1. **❚❚ options button — superseded 2026-10-03 (user ruling, v1.1 round):** it never flies AND never moves — `HudViewportPin` is ungated (always pinned while the chrome band is active), so the shop viewport corner serves combat as-is with unchanged press semantics. RegressionChecklist row 144."
- Accepted deviation 5: replace the "v1.1 backlog:" sentence with — "v1.1 status (updated 2026-10-03): the world topo background is LANDED (row 143); the ❚❚ flight was superseded by the always-pin ruling (item 1); still open: the combat→shop card return flight. The 2026-10-03 overshoot-peek note is resolved by the sheet (its PAD headroom = `pagePadDemoPx`)."
- Arbitration "Shop chrome visibility (fix 4)" bullet: replace "`HudViewportPin` (OptionsButton) holds only in a settled shop — mid-travel/outside the shop the button scrolls with the band and re-pins on the next settled frame." with "`HudViewportPin` (OptionsButton) is always pinned while the band is active (2026-10-03 ruling — row 144); mid-travel and in combat the button stays at the viewport corner while the rest of the band scrolls with the page."

- [ ] **Step 2: Regression rows**

Append after row 142 (same column format):

- Row 143 — "Visual fix (2026-10-03): at the Shop→Combat overshoot peak the compare bar's enemy half detached from the screen top, revealing plain background (user report + screenshot) — the bar is world-pinned page content while the old backdrop was a screen-fixed RawImage, and nothing world-side existed above the combat page edge. The demo covers this with its red PAD (buildTopo: the red rect starts at the world-sheet top, PAD included)" | `WorldTopoBackground` (new, both pages + `pagePadDemoPx` PAD), `Custom/ContourLinesBackgroundWorld` (world-basis noise, red band above combat page center 50%, palette-bound), GameScene (`background` RawImage disabled) | 2026-10-03 | ⚠️ | **Step:** 离开商店 (Overshoot) — at the peak the area above the bar is band-red, no void; Result→shop — at the descent overshoot the area below the shop page is gray topo; contours parallax with the travel and with shop wheel scroll (new).<br>**Check:** Combat/shop content draws over the sheet; enemy band red matches the bar (palette); look parity vs the retired backdrop within one tune pass (`_Levels`/`_WorldScale`/`_Intensity`); bypass (`-odseed 5`) hard cut — no band on the shop page, background still renders. |
- Row 144 — "Behavior change (2026-10-03 user ruling, v1.1 round): OptionsButton never moves — shop viewport corner serves combat; the 2026-10-02 settled-shop-only pin scope and the mid-travel scroll-with-the-band behavior are superseded (demo's two-home ❚❚ flight rejected)" | `HudViewportPin` (`ApplyPin` ungated — pin holds while the chrome band is active) | 2026-10-03 | ⚠️ | **Step:** 离开商店 → combat → Result → shop, twice.<br>**Check:** The button stays at the viewport corner through the whole loop (no band scroll-away, no landing re-pin pop); combat press hits the Options placeholder log with unchanged hover/press feel; legacy hard cut (driver off) still hides it in combat (bypass parity). |

Also annotate row 135's Check cell: append "(OptionsButton clause superseded by row 144, 2026-10-03)" after "no floating OptionsButton over combat." — the row itself stays intact.

- [ ] **Step 3: Commit**

```bash
git add docs/PhaseTransition.md docs/RegressionChecklist.md
git commit -m "docs: v1.1 topo sheet + OptionsButton always-pin (PhaseTransition.md, RegressionChecklist 143-144)"
```

### Task 6: User Play verification (manual — agent does NOT run Play Mode)

- [ ] 1. 离开商店 (Overshoot): at the peak the area above the compare bar is band-red — no void; contours are world-locked (they ride the travel like the cards do).
- [ ] 2. Band-bottom landing: the red/gray boundary sits at the viewport center on combat landing (50% ruling).
- [ ] 3. Combat: cards / reveal zone / HUD all draw over the sheet (Review Focus 1 — if cards are INVISIBLE the Z sign is flipped: report immediately); combat feedback visuals unchanged.
- [ ] 4. Result→Shop: at the descent overshoot the area below the shop page is gray topo — no void; the button rides the corner the whole way.
- [ ] 5. ❚❚: same corner in shop and combat, pressable in combat (console shows the Options placeholder log), hover/press feel identical; in Result it sits behind the overlay where they overlap.
- [ ] 6. Look tune (one pass, on the TopoSheet material): if contour density/character differs noticeably from the retired backdrop, adjust `_Levels` / `_WorldScale` / `_Intensity` (Review Focus 3).
- [ ] 7. Bypass `-odseed 5`: hard cut; no red band on the shop page (it is one page up); background pattern present (now world-basis).
- [ ] 8. Palette: change `HpBarEnemyColor` → re-enter Play → the band follows (matches the bar again).

---

## Self-Review Log

- **Spec coverage:** demo buildTopo (PAD headroom + red-through-PAD) → Tasks 1-2 (sheet + world-basis shader, boundary at 50% per R3, red palette-bound per R2); ❚❚ per R1 (no flight, always pinned) → Task 4; docs/rows → Task 5; the combat→shop card return flight is deliberately NOT in this round (accepted deviation 2 stands; v1.1 item stays open).
- **Placeholder scan:** every code step is the full file/diff; the only tune item (contour look) is an explicit Play step (6), not a code TBD.
- **Type consistency:** `PhaseTransitionConfigSO.Me` / `pagePadDemoPx`, `PhaseFlightPlanner.PageHeightWorld(float)` / `PxToWorld(float, float)`, `CombatUXManager.me.physicalCardDeckPos`, `GameColorPalette.HpBarEnemyColor`, `ShopHudBinder` Options placeholder — all verified to exist at the cited members during plan research (2026-10-03).
- **Review Focus:** 6 items; each has a pinning step (Z-sign watch + Play 3, split basis + Play 2, look tune + Play 6, bypass + Play 7, Result behavior + Play 5, palette timing + Play 8).
