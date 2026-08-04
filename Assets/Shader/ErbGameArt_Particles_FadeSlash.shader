// Reconstructed from compiled-shader metadata (properties + render state). Original HLSL
// lost to AssetRipper's dummy export. Functional, parameter-faithful approximation: a
// tinted, emissive, alpha-blended particle slash with edge (side) opacity falloff.
Shader "ErbGameArt/Particles/FadeSlash"
{
	Properties
	{
		_MainTex ("MainTex", 2D) = "white" {}
		_TintColor ("Color", Vector) = (0,0.5019608,1,1)
		_EmissionRGB ("Emission/R/G/B", Vector) = (1,0.4,0.4,1)
		_Startopacity ("Start opacity", Float) = 40
		[MaterialToggle] _Sideopacity ("Side opacity", Float) = 1
		_Sideopacitypower ("Side opacity power", Float) = 40
		_Finalopacity ("Final opacity", Range(0, 1)) = 1
		[MaterialToggle] _Usedistortion ("Use distortion?", Float) = 0
		_Distortionpower ("Distortion power", Range(0, 2)) = 1
		_Noise ("Noise", 2D) = "black" {}
		_LenghtSet1ifyouuseinPS ("Lenght(Set 1 if you use in PS)", Range(0, 1)) = 1
		_PathSet0ifyouuseinPS ("Path(Set 0 if you use in PS)", Range(0, 1)) = 0
	}
	SubShader
	{
		Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "RenderPipeline"="UniversalPipeline" }
		LOD 200

		Pass
		{
			Tags { "LightMode"="Universal2D" }
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			Cull Off
			ZTest LEqual

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			sampler2D _MainTex;  float4 _MainTex_ST;
			sampler2D _Noise;
			fixed4 _TintColor, _EmissionRGB;
			float _Finalopacity, _Sideopacity, _Sideopacitypower, _Startopacity;
			float _Usedistortion, _Distortionpower;

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
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.color = v.color;
				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				float2 uv = i.uv;
				if (_Usedistortion > 0.5)
				{
					float n = tex2D(_Noise, uv).r;
					uv += (n - 0.5) * 0.1 * _Distortionpower;
				}
				fixed4 tex = tex2D(_MainTex, uv);

				// Side (edge) opacity: fade toward the U edges of the slash strip.
				float side = 1.0;
				if (_Sideopacity > 0.5)
				{
					float e = saturate(i.uv.x) * saturate(1.0 - i.uv.x) * 4.0;
					side = pow(saturate(e), max(_Sideopacitypower, 0.0001) * 0.05);
				}

				fixed3 col = tex.rgb * _TintColor.rgb * i.color.rgb;
				col += _EmissionRGB.rgb * tex.a;

				float alpha = tex.a * _TintColor.a * i.color.a * _Finalopacity * side;
				return fixed4(col, saturate(alpha));
			}
			ENDCG
		}
	}
	Fallback Off
}
