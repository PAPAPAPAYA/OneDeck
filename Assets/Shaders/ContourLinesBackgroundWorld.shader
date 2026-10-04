Shader "Custom/ContourLinesBackgroundWorld"
{
	Properties
	{
		_BgColor("Player Region Background", Color) = (0.7411765, 0.7294118, 0.7019608, 1)
		_LineColor("Player Region Line Color", Color) = (0.2625, 0.3239, 0.3378, 1)
		_RedColor("Enemy Region Background", Color) = (0.627451, 0.137255, 0.196078, 1)
		_LineColorRed("Enemy Region Line Color", Color) = (0.345098, 0.075490, 0.107843, 1)
		_SplitWorldY("Enemy Band Bottom World Y", Float) = 12.12
		_WorldScale("Noise Cycles Per World Unit", Float) = 0.2475
		_Levels("Contour Levels", Float) = 2
		_LineWidth("Line Width", Float) = 2
		_NoiseScale("Noise Scale", Float) = 3
		_Speed("Morph Speed", Float) = 0.08
		_FBM2Freq("Field Bias (x 0.5)", Float) = 2
		_Intensity("Line Intensity", Range(0, 1)) = 1
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
				float _FBM2Freq;
				float _Intensity;
			CBUFFER_END

			// Hash: map an integer 3D lattice coordinate to a pseudo-random gradient
			// direction in [-1, 1]^3. Deterministic: same input, same output.
			// (iq-style hash33 without sin(); ported verbatim from
			// Assets/VFX/BackGroundMotion/SliceNoise3D.hlsl so the contour character
			// matches the retired BackGroundMotion3D backdrop exactly — user report
			// 2026-10-03: the first port's 3-octave fbm family did not.)
			float3 SliceHash3(float3 p)
			{
				p = frac(p * float3(0.1031, 0.1030, 0.0973));
				p += dot(p, p.yxz + 33.33);
				return frac((p.xxy + p.yxx) * p.zyx) * 2.0 - 1.0;
			}

			// 3D gradient (Perlin-style) noise: dot products of corner gradients with the
			// local offset, trilinearly interpolated with a quintic fade. SINGLE octave —
			// the original backdrop's field is one smooth Perlin slice along the time axis
			// (its "FBM" branch is muted to a constant bias; see frag). Rescaled to [0, 1]
			// so Levels/LineWidth keep the retired material's tuning meaning.
			float VNoise3D(float3 pos)
			{
				float3 i = floor(pos);
				float3 f = frac(pos);
				float3 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
				float n000 = dot(SliceHash3(i), f);
				float n100 = dot(SliceHash3(i + float3(1.0, 0.0, 0.0)), f - float3(1.0, 0.0, 0.0));
				float n010 = dot(SliceHash3(i + float3(0.0, 1.0, 0.0)), f - float3(0.0, 1.0, 0.0));
				float n110 = dot(SliceHash3(i + float3(1.0, 1.0, 0.0)), f - float3(1.0, 1.0, 0.0));
				float n001 = dot(SliceHash3(i + float3(0.0, 0.0, 1.0)), f - float3(0.0, 0.0, 1.0));
				float n101 = dot(SliceHash3(i + float3(1.0, 0.0, 1.0)), f - float3(1.0, 0.0, 1.0));
				float n011 = dot(SliceHash3(i + float3(0.0, 1.0, 1.0)), f - float3(0.0, 1.0, 1.0));
				float n111 = dot(SliceHash3(i + float3(1.0, 1.0, 1.0)), f - float3(1.0, 1.0, 1.0));
				float n = lerp(lerp(lerp(n000, n100, u.x), lerp(n010, n110, u.x), u.y),
					lerp(lerp(n001, n101, u.x), lerp(n011, n111, u.x), u.y),
					u.z);
				return clamp(n * 0.6667 + 0.5, 0.0, 1.0);
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
				// World-space basis: the pattern is world-locked — the camera travel carries
				// it like page content and the shop wheel scroll gains the demo's parallax.
				// Density is normalized by the binder (_WorldScale = _NoiseScale / pageH) so
				// the field spans the same number of noise cells per page height as the
				// retired screen-fixed backdrop; its screen aspect correction is unnecessary
				// here because world units are already isotropic.
				float2 p = input.positionWS.xy * _WorldScale;

				// Slow morph: the time axis SLICES through the 3D noise field, so contour
				// lines are born and die smoothly instead of drifting. No domain warp, no
				// fbm — the original look is ONE smooth Perlin slice. The retired graph's
				// second VNoise3D branch is muted (pos *= 0) and survives only as the
				// constant bias _FBM2Freq * 0.5, ported as-is: it shifts WHICH iso-line
				// family shows (default 2 = the mid-height line, matching the shipped
				// Mat_BackGroundMotion3D material).
				float3 pos = float3(p.x, p.y, _Time.y * _Speed);
				float h = VNoise3D(pos) + _FBM2Freq * 0.5;

				// Anti-aliased iso-lines at INTEGER v (the original's phase: dist =
				// frac(v), not the half-integer variant), width in fwidth(v) units.
				float v = h * _Levels;
				float fw = fwidth(v);
				float distToLine = frac(v);
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
