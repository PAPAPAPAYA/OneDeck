Shader "Custom/CardRoundedRect"
{
	Properties
	{
		[MainTexture] _MainTex("Sprite Texture", 2D) = "white" {}
		[MainColor] _Color("Tint", Color) = (1,1,1,1)
		_CornerRadius("Corner Radius", Range(0, 0.5)) = 0.08
		_AspectRatio("Aspect Ratio", Float) = 0.7
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
			};

			TEXTURE2D(_MainTex);
			SAMPLER(sampler_MainTex);

			CBUFFER_START(UnityPerMaterial)
				float4 _MainTex_ST;
				float4 _Color;
				float _CornerRadius;
				float _AspectRatio;
			CBUFFER_END

			Varyings vert(Attributes input)
			{
				Varyings output;
				output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
				output.uv = TRANSFORM_TEX(input.uv, _MainTex);
				output.color = input.color;
				return output;
			}

			float RoundedRectAlpha(float2 uv, float radius, float aspect)
			{
				float2 scale = float2(1.0, aspect);
				float2 halfSize = scale * 0.5;

				float maxRadius = min(halfSize.x, halfSize.y);
				radius = min(radius, maxRadius);

				float2 absPos = abs((uv - 0.5) * scale);
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

				float alpha = RoundedRectAlpha(input.uv, _CornerRadius, _AspectRatio);
				finalColor.a *= alpha;

				return finalColor;
			}
			ENDHLSL
		}
	}

	FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
