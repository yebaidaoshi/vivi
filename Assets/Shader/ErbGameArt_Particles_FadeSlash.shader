// Decompiled from ErbGameArt/Particles/FadeSlash
// sharedassets13 path_id 359, FORWARD PS blob 01.
//
// Pass 0 is GrabPass { "_GrabTexture" }.
// FORWARD: Cull Off, ZTest LEqual, ZWrite Off,
//          Blend SrcAlpha OneMinusSrcAlpha (NOT additive).
//
// TEXCOORD1 = particle Custom1 (Path.x, Length.y, Cut.z)
// discard if noise.x * Custom1.z < 0.5
// path = (Custom1.x + PathSet)*-1.5+2.5;  path^5 * uv.x wipe
// body = _MainTex at (wipe, uv.y)
// rgb  = dot(tex.rgb, Emission.yzw) * vertex * Tint * Emission.x
//      + Usedistortion * saturate(grab.rgb at (uv.y, (sat(noise)*Distortion)^2))

Shader "ErbGameArt/Particles/FadeSlash"
{
	Properties
	{
		_MainTex ("MainTex", 2D) = "white" {}
		_TintColor ("Color", Vector) = (0, 0.5019608, 1, 1)
		_EmissionRGB ("Emission/R/G/B", Vector) = (1, 0.4, 0.4, 1)
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

	HLSLINCLUDE
	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

	TEXTURE2D(_MainTex);
	SAMPLER(sampler_MainTex);
	TEXTURE2D(_Noise);
	SAMPLER(sampler_Noise);
	TEXTURE2D(_CameraSortingLayerTexture);
	SAMPLER(sampler_CameraSortingLayerTexture);
	TEXTURE2D(_CameraOpaqueTexture);
	SAMPLER(sampler_CameraOpaqueTexture);

	CBUFFER_START(UnityPerMaterial)
		float4 _MainTex_ST;
		float4 _Noise_ST;
		float4 _TintColor;
		float4 _EmissionRGB;
		float _Startopacity;
		float _Sideopacity;
		float _Sideopacitypower;
		float _Finalopacity;
		float _Usedistortion;
		float _Distortionpower;
		float _LenghtSet1ifyouuseinPS;
		float _PathSet0ifyouuseinPS;
	CBUFFER_END

	struct Attributes
	{
		float4 positionOS : POSITION;
		float2 uv         : TEXCOORD0;
		float4 custom     : TEXCOORD1;
		float4 color      : COLOR;
	};

	struct Varyings
	{
		float4 positionCS : SV_POSITION;
		float2 uv         : TEXCOORD0;
		float4 custom     : TEXCOORD1;
		float4 color      : COLOR;
	};

	Varyings FadeVert(Attributes input)
	{
		Varyings output;
		output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
		output.uv = input.uv;
		output.custom = input.custom;
		output.color = input.color;
		return output;
	}

	float4 SampleGrab(float2 uv)
	{
		float4 scene = SAMPLE_TEXTURE2D(_CameraSortingLayerTexture, sampler_CameraSortingLayerTexture, uv);
		float w = max(max(scene.r, scene.g), scene.b) + scene.a;
		if (w < 0.0001)
		{
			scene = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv);
		}
		return scene;
	}

	float4 FadeFrag(Varyings input) : SV_Target
	{
		float2 noiseUV = input.uv * _Noise_ST.xy + _Noise_ST.zw;
		float noise = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, noiseUV).x;
		if (noise * input.custom.z - 0.5 < 0.0)
		{
			discard;
		}

		float dist = saturate(noise) * _Distortionpower;
		float dist2 = dist * dist;

		float path = input.custom.x + _PathSet0ifyouuseinPS;
		path = path * -1.5 + 2.5;
		float path5 = path * path * path * path * path;

		float lenTerm = 1.0 - _LenghtSet1ifyouuseinPS * input.custom.y;
		float wipe = saturate(path5 * input.uv.x - lenTerm);
		float soft = 1.0 / (1.0 - 0.999 * lenTerm);
		float wipeSoft = saturate(wipe * soft);
		float vSat = saturate(input.uv.y);

		float2 bodyUV = float2(wipeSoft, vSat) * _MainTex_ST.xy + _MainTex_ST.zw;
		float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, bodyUV);

		float startFade = saturate((1.0 - wipeSoft) * _Startopacity) * saturate(wipeSoft * _Startopacity);

		float side = saturate((1.0 - input.uv.y) * _Sideopacitypower);
		side = 1.0 + _Sideopacity * (side - 1.0);

		float layer = dot(tex.rgb, _EmissionRGB.yzw);
		float3 body = layer * input.color.rgb * _TintColor.rgb * _EmissionRGB.x;

		float3 grab = saturate(SampleGrab(float2(input.uv.y, dist2)).rgb);
		float3 rgb = _Usedistortion * grab + body;

		float alpha = tex.a * input.color.a * _TintColor.a * startFade * side * _Finalopacity;
		return float4(rgb, alpha);
	}
	ENDHLSL

	SubShader
	{
		Tags
		{
			"RenderPipeline"="UniversalPipeline"
			"Queue"="Transparent"
			"IgnoreProjector"="True"
			"RenderType"="Transparent"
			"PreviewType"="Plane"
		}
		LOD 200
		Cull Off
		ZWrite Off
		ZTest LEqual
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			Name "FORWARD"
			Tags { "LightMode"="SRPDefaultUnlit" }
			HLSLPROGRAM
			#pragma vertex FadeVert
			#pragma fragment FadeFrag
			ENDHLSL
		}
		Pass
		{
			Name "Universal2D"
			Tags { "LightMode"="Universal2D" }
			HLSLPROGRAM
			#pragma vertex FadeVert
			#pragma fragment FadeFrag
			ENDHLSL
		}
		Pass
		{
			Name "UniversalForward"
			Tags { "LightMode"="UniversalForward" }
			HLSLPROGRAM
			#pragma vertex FadeVert
			#pragma fragment FadeFrag
			ENDHLSL
		}
	}
	Fallback Off
}
