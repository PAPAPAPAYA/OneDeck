Shader "Custom/CardRoundedRectWorldSpaceLit"
{
	Properties
	{
		[MainTexture] _BaseMap("Sprite Texture", 2D) = "white" {}
		[MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
		_CornerRadius("Corner Radius", Range(0, 0.5)) = 0.15
		_MeshHalfSize("Mesh Half Size", Vector) = (0.5, 0.5, 0, 0)
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
			"CanUseSpriteAtlas" = "True"
		}

		// Forward Lit Pass
		Pass
		{
			Name "ForwardLit"
			Tags { "LightMode" = "UniversalForward" }

			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			ZTest LEqual
			Cull Off

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
			#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
			#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
			#pragma multi_compile _ _SHADOWS_SOFT
			#pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

			struct Attributes
			{
				float4 positionOS : POSITION;
				float2 uv : TEXCOORD0;
				float4 color : COLOR;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float2 uv : TEXCOORD0;
				float4 color : COLOR;
				float2 positionRelWS : TEXCOORD1;
				float2 halfSizeWS : TEXCOORD2;
				float3 positionWS : TEXCOORD3;
			};

			TEXTURE2D(_BaseMap);
			SAMPLER(sampler_BaseMap);

			CBUFFER_START(UnityPerMaterial)
				float4 _BaseMap_ST;
				float4 _BaseColor;
				float _CornerRadius;
				float2 _MeshHalfSize;
			CBUFFER_END

			Varyings vert(Attributes input)
			{
				Varyings output;
				output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
				output.positionCS = TransformWorldToHClip(output.positionWS);
				output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
				output.color = input.color;

				float3 scale = float3(
					length(unity_ObjectToWorld._m00_m01_m02),
					length(unity_ObjectToWorld._m10_m11_m12),
					length(unity_ObjectToWorld._m20_m21_m22)
				);

				float3 centerWS = unity_ObjectToWorld._m03_m13_m23;
				output.positionRelWS = output.positionWS.xy - centerWS.xy;
				output.halfSizeWS = _MeshHalfSize * scale.xy;

				return output;
			}

			float RoundedRectAlpha(float2 relPos, float2 halfSize, float radiusRatio)
			{
				float maxRadius = min(halfSize.x, halfSize.y);
				float radius = radiusRatio * maxRadius;

				float2 absPos = abs(relPos);
				float2 q = absPos - halfSize + radius;
				float d = length(max(q, 0.0)) - radius;

				float2 grad = float2(ddx(d), ddy(d));
				float pixelSize = length(grad);
				float edge = max(pixelSize * 1.5, 0.001);

				return 1.0 - smoothstep(0.0, edge, d);
			}

			half4 frag(Varyings input) : SV_Target
			{
				half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
				half4 finalColor = texColor * input.color * _BaseColor;

				// Rounded rect clip
				float alpha = RoundedRectAlpha(input.positionRelWS, input.halfSizeWS, _CornerRadius);
				finalColor.a *= alpha;

				// Lighting
				#if defined(_MAIN_LIGHT_SHADOWS)
					float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
					Light mainLight = GetMainLight(shadowCoord);
				#else
					Light mainLight = GetMainLight();
				#endif

				// Diffuse lighting
				float3 normalWS = float3(0, 1, 0);
				half NdotL = saturate(dot(normalWS, mainLight.direction));
				half3 diffuse = mainLight.color * (mainLight.shadowAttenuation * NdotL + mainLight.distanceAttenuation * (1 - NdotL));

				// Ambient
				half3 ambient = SampleSH(normalWS);

				finalColor.rgb *= (diffuse + ambient);

				return finalColor;
			}
			ENDHLSL
		}

		// ShadowCaster Pass
		Pass
		{
			Name "ShadowCaster"
			Tags { "LightMode" = "ShadowCaster" }

			ZWrite On
			ZTest LEqual
			ColorMask 0
			Cull Off

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			struct Attributes
			{
				float4 positionOS : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float2 uv : TEXCOORD0;
				float2 positionRelWS : TEXCOORD1;
				float2 halfSizeWS : TEXCOORD2;
			};

			float _CornerRadius;
			float2 _MeshHalfSize;

			Varyings vert(Attributes input)
			{
				Varyings output;
				float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
				output.positionCS = TransformWorldToHClip(positionWS);
				output.uv = input.uv;

				float3 scale = float3(
					length(unity_ObjectToWorld._m00_m01_m02),
					length(unity_ObjectToWorld._m10_m11_m12),
					length(unity_ObjectToWorld._m20_m21_m22)
				);

				float3 centerWS = unity_ObjectToWorld._m03_m13_m23;
				output.positionRelWS = positionWS.xy - centerWS.xy;
				output.halfSizeWS = _MeshHalfSize * scale.xy;

				return output;
			}

			float RoundedRectClip(float2 relPos, float2 halfSize, float radiusRatio)
			{
				float maxRadius = min(halfSize.x, halfSize.y);
				float radius = radiusRatio * maxRadius;

				float2 absPos = abs(relPos);
				float2 q = absPos - halfSize + radius;
				float d = length(max(q, 0.0)) - radius;

				return d;
			}

			void frag(Varyings input)
			{
				float d = RoundedRectClip(input.positionRelWS, input.halfSizeWS, _CornerRadius);
				clip(-d);
			}
			ENDHLSL
		}
	}

	FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
