// Fullscreen pixelation for URP Full Screen Pass Renderer Feature.
// Algorithm ported 1:1 from the classic "Pixelation" image effect (block-center sampling).
Shader "Custom/PixelationFullscreen"
{
	Properties
	{
		_BlockCount ("Block Count", Range(64, 512)) = 128
	}
	SubShader
	{
		Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
		LOD 100
		ZWrite Off
		Cull Off
		ZTest Always

		Pass
		{
			Name "PixelationFullscreen"

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

			float _BlockCount;

			half4 Frag(Varyings input) : SV_Target
			{
				// Aspect-corrected block grid: BlockCount columns, BlockCount / aspect rows.
				float aspect = _ScreenParams.x / _ScreenParams.y;
				float2 count = float2(_BlockCount, _BlockCount / aspect);
				float2 blockPos = floor(input.texcoord * count);
				float2 blockCenter = (blockPos + 0.5) / count;
				return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, blockCenter, 0);
			}
			ENDHLSL
		}
	}
}
