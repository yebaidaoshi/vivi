// Reconstructed from the original compiled-shader metadata (properties + render state).
// The original Shader Graph source was lost during AssetRipper export (only a flat,
// opaque dummy stub remained). This is a functional, parameter-faithful approximation:
// an unlit, alpha-blended 2D slash tinted by the HDR Color and shaped by the Mask.
Shader "Shader Graphs/SlashShaderGraph"
{
	Properties
	{
		[HDR] Color_39BB2441 ("Color", Color) = (1,1,1,1)
		[NoScaleOffset] _MainTex ("MainTexture", 2D) = "white" {}
		[NoScaleOffset] Texture2D_4AF1F502 ("Mask", 2D) = "white" {}

		[NoScaleOffset] _BrushTex ("Brush Texture", 2D) = "white" {}
		_BrushStrength ("Brush Strength", Range(0,1)) = 0.7
		_BrushSpeed ("Brush Speed", Float) = 0.0
		_EmissionStrength ("Emission Strength", Float) = 2.5
		_Alpha ("Alpha", Range(0,1)) = 1.0
	}
	SubShader
	{
		Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" "RenderPipeline"="UniversalPipeline" }
		LOD 100

		Pass
		{
			// Matches original compiled state: standard alpha blend, no depth write, two-sided.
			Tags { "LightMode"="Universal2D" }
			Blend SrcAlpha One
			ZWrite Off
			Cull Off
			ZTest LEqual

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			sampler2D _MainTex;
			sampler2D Texture2D_4AF1F502;
			fixed4 Color_39BB2441;

			sampler2D _BrushTex;
			float _BrushStrength;
			float _BrushSpeed;
			float _EmissionStrength;
			float _Alpha;

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv     : TEXCOORD0;
				fixed4 color  : COLOR;
			};

			struct v2f
			{
				float4 pos   : SV_POSITION;
				float2 uv    : TEXCOORD0;
				fixed4 color : COLOR;
			};

			v2f vert (appdata v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				o.color = v.color;
				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 tex = tex2D(_MainTex, i.uv);
				fixed maskValue = tex2D(Texture2D_4AF1F502, i.uv).r;

				float2 brushUV = i.uv;
				brushUV.x += _Time.y * _BrushSpeed;

				fixed brush = tex2D(_BrushTex, brushUV).r;

				fixed brightness = max(max(tex.r, tex.g), tex.b);
				brightness *= brush;

				fixed alpha = brightness * maskValue * Color_39BB2441.a * i.color.a * _Alpha;
				fixed3 emission = Color_39BB2441.rgb * i.color.rgb * brightness * _EmissionStrength;

				return fixed4(emission, alpha);
			}
			ENDCG
		}
	}
	Fallback Off
}
