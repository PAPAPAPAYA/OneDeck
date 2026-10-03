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
