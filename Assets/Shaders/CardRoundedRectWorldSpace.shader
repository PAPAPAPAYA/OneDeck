Shader "Custom/CardRoundedRectWorldSpace"
{
	Properties
	{
		[MainTexture] _MainTex("Sprite Texture", 2D) = "white" {}
		[MainColor] _Color("Tint", Color) = (1,1,1,1)
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

		Pass
		{
			Name "Unlit"

			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			ZTest LEqual
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
				float2 uv : TEXCOORD0;
				float4 color : COLOR;
				float2 positionRelWS : TEXCOORD1;
				float2 halfSizeWS : TEXCOORD2;
			};

			TEXTURE2D(_MainTex);
			SAMPLER(sampler_MainTex);

			CBUFFER_START(UnityPerMaterial)
				float4 _MainTex_ST;
				float4 _Color;
				float _CornerRadius;
				float2 _MeshHalfSize;
			CBUFFER_END

			Varyings vert(Attributes input)
			{
				Varyings output;
				output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
				output.uv = TRANSFORM_TEX(input.uv, _MainTex);
				output.color = input.color;

				float3 scale = float3(
					length(unity_ObjectToWorld._m00_m01_m02),
					length(unity_ObjectToWorld._m10_m11_m12),
					length(unity_ObjectToWorld._m20_m21_m22)
				);

				float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
				float3 centerWS = unity_ObjectToWorld._m03_m13_m23;

				output.positionRelWS = worldPos.xy - centerWS.xy;
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
				half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
				half4 finalColor = texColor * input.color * _Color;

				float alpha = RoundedRectAlpha(input.positionRelWS, input.halfSizeWS, _CornerRadius);
				finalColor.a *= alpha;

				return finalColor;
			}
			ENDHLSL
		}
	}

	FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
