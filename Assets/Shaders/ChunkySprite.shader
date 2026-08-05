// Chunky (ASCII-art style) effect for a single SpriteRenderer.
// Ported from the classic "Chunky" fullscreen image effect: the sprite is divided
// into blocks, each block's grayscale is quantized to 16 levels, and the block is
// redrawn using the matching frame of a 16-frame chunky character atlas.
Shader "Custom/ChunkySprite"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		_SprTex ("Chunky Atlas", 2D) = "white" {}
		_Color ("Tint", Color) = (1,1,1,1)
		_BlockCount ("Block Count", Range(8, 128)) = 32
		_Brightness ("Brightness", Range(0, 1)) = 1
	}
	SubShader
	{
		Tags
		{
			"Queue" = "Transparent"
			"IgnoreProjector" = "True"
			"RenderType" = "Transparent"
			"PreviewType" = "Plane"
			"CanUseSpriteAtlas" = "True"
		}

		Cull Off
		Lighting Off
		ZWrite Off
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			sampler2D _MainTex;
			sampler2D _SprTex;
			fixed4 _Color;
			float _BlockCount;
			float _Brightness;

			struct appdata_t
			{
				float4 vertex : POSITION;
				float4 color : COLOR;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				fixed4 color : COLOR;
				float2 texcoord : TEXCOORD0;
			};

			v2f vert(appdata_t input)
			{
				v2f output;
				output.vertex = UnityObjectToClipPos(input.vertex);
				output.texcoord = input.texcoord;
				output.color = input.color * _Color;
				return output;
			}

			fixed4 frag(v2f input) : SV_Target
			{
				// (1) Block grid over the sprite, sample each block's center.
				float2 count = float2(_BlockCount, _BlockCount);
				float2 blockPos = floor(input.texcoord * count);
				float2 blockCenter = (blockPos + 0.5) / count;

				// (2) Grayscale of the block, scaled by the brightness control.
				float4 tex = tex2D(_MainTex, blockCenter);
				tex.rgb *= _Brightness;
				float grayscale = clamp(dot(tex.rgb, float3(0.3, 0.59, 0.11)), 0.0, 1.0);

				// (3) Quantize to one of the 16 atlas frames.
				float frame = floor(grayscale * 16.0);

				// (4) Position inside the block, mapped into the chosen atlas frame.
				float2 local = frac(input.texcoord * count);
				float2 sprUV = float2((local.x + frame) / 16.0, local.y);
				fixed4 chunky = tex2D(_SprTex, sprUV);

				// (5) Keep the sprite silhouette (block alpha) and the vertex tint.
				return chunky * tex.a * input.color;
			}
			ENDCG
		}
	}
}
