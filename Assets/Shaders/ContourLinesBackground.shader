Shader "Custom/ContourLinesBackground"
{
	Properties
	{
		_BgColor("Background Color", Color) = (0.102, 0.188, 0.216, 1)
		_LineColor("Line Color", Color) = (0.851, 0.835, 0.792, 1)
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
			Name "ContourLinesBackground"

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
				float2 uv : TEXCOORD0;
				float4 color : COLOR;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float4 color : COLOR;
			};

			CBUFFER_START(UnityPerMaterial)
				float4 _BgColor;
				float4 _LineColor;
				float _Levels;
				float _LineWidth;
				float _NoiseScale;
				float _Speed;
				float _Intensity;
			CBUFFER_END

			// Hash-based pseudo-random gradient in [-1, 1]^2 (iq-style hash22).
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
				output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
				output.color = input.color;
				return output;
			}

			half4 frag(Varyings input) : SV_Target
			{
				// Screen-space UV keeps the pattern isotropic regardless of the RawImage rect size.
				float2 screenUV = input.positionCS.xy / _ScreenParams.xy;
				float2 p = (screenUV - 0.5) * float2(_ScreenParams.x / _ScreenParams.y, 1.0) * _NoiseScale;

				// Slow in-place morph: bounded sinusoidal domain warp, no net drift.
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

				float3 col = lerp(_BgColor.rgb, _LineColor.rgb, lineMask * _Intensity);
				col *= input.color.rgb;
				return half4(col, _BgColor.a * input.color.a);
			}
			ENDHLSL
		}
	}
}
