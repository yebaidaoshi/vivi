// Decompiled from Shader Graphs/DistortionMaintex (Unity 2021.2.7f1).
// sharedassets13 path_id 344, Sprite Unlit PS (blob 01).
//
// Proven:
//   Cull Off, ZTest LEqual, ZWrite Off
//   Blend SrcAlpha OneMinusSrcAlpha
//   Does not sample _MainTex. Distortion is Gradient Noise (289 hash).
//   UV = (meshUV-0.5)*objectScale + Noise_Scale.xy*time
//        + rotate(meshUV-0.5, length*10)
//   offset = Strength * 0.01 * (noise - 0.5)
//   sample _CameraSortingLayerTexture(screenUV + offset)
//   rgb = scene * vertex * _RendererColor, a = vertex.a * _RendererColor.a
//
// DistortionTest: Strength (10,10), Noise_Scale (0.5,0.5,0,0), vertex a=0.235.

Shader "Shader Graphs/DistortionMaintex"
{
	Properties
	{
		[NoScaleOffset] _MainTex ("_MainTex", 2D) = "white" {}
		Velocity ("Velocity", Vector) = (0, 0, 0, 0)
		Noise_Scale ("Noise Scale", Vector) = (0.5, 0.5, 0, 0)
		Strength ("Strength", Vector) = (10, 10, 0, 0)
	}

	HLSLINCLUDE
	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

	TEXTURE2D(_CameraSortingLayerTexture);
	SAMPLER(sampler_CameraSortingLayerTexture);
	TEXTURE2D(_CameraOpaqueTexture);
	SAMPLER(sampler_CameraOpaqueTexture);

	CBUFFER_START(UnityPerMaterial)
		float4 Velocity;
		float4 Noise_Scale;
		float4 Strength;
	CBUFFER_END

	float4 _RendererColor;

	struct Attributes
	{
		float4 positionOS : POSITION;
		float2 uv         : TEXCOORD0;
		float4 color      : COLOR;
	};

	struct Varyings
	{
		float4 positionCS : SV_POSITION;
		float3 positionWS : TEXCOORD0;
		float2 uv         : TEXCOORD1;
		float4 color      : COLOR;
	};

	float2 UnityGradientNoiseDir(float2 p)
	{
		p = p - 289.0 * floor(p / 289.0);
		float x = (34.0 * p.x + 1.0) * p.x;
		x = x - 289.0 * floor(x / 289.0) + p.y;
		x = (34.0 * x + 1.0) * x;
		x = x - 289.0 * floor(x / 289.0);
		x = frac(x * 0.0243902439) * 2.0 - 1.0;
		return normalize(float2(x - floor(x + 0.5), abs(x) - 0.5));
	}

	float UnityGradientNoise(float2 uv)
	{
		float2 ip = floor(uv);
		float2 fp = frac(uv);
		float d00 = dot(UnityGradientNoiseDir(ip), fp);
		float d01 = dot(UnityGradientNoiseDir(ip + float2(0, 1)), fp - float2(0, 1));
		float d10 = dot(UnityGradientNoiseDir(ip + float2(1, 0)), fp - float2(1, 0));
		float d11 = dot(UnityGradientNoiseDir(ip + float2(1, 1)), fp - float2(1, 1));
		fp = fp * fp * fp * (fp * (fp * 6.0 - 15.0) + 10.0);
		return lerp(lerp(d00, d01, fp.y), lerp(d10, d11, fp.y), fp.x) + 0.5;
	}

	Varyings DistVert(Attributes input)
	{
		Varyings output;
		output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
		output.positionCS = TransformWorldToHClip(output.positionWS);
		output.uv = input.uv;
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

	float4 DistFrag(Varyings input) : SV_Target
	{
		float sx = length(UNITY_MATRIX_M._m00_m10_m20);
		float sy = length(UNITY_MATRIX_M._m01_m11_m21);
		float2 scaled = input.uv * float2(sx, sy) - float2(sx, sy) * 0.5;

		float t = _TimeParameters.x * 0.001;
		t = (t >= -t ? frac(abs(t)) : -frac(abs(t))) * 1000.0;
		scaled += Noise_Scale.xy * t;

		float2 d = input.uv - 0.5;
		float ang = length(d) * 10.0;
		float s, c;
		sincos(ang, s, c);
		float2 rot = float2(d.x * c - d.y * s, d.y * c + d.x * s);
		float2 nUV = scaled + rot;

		// Vector2 Noise_Scale serializes z=0; the graph used .z as Gradient Scale.
		float gscale = Noise_Scale.z > 1e-5 ? Noise_Scale.z : max(Noise_Scale.x, 0.5);
		float nX = UnityGradientNoise((nUV + 1.5) * gscale);
		float nY = UnityGradientNoise((nUV + 0.5) * gscale);
		float2 offset = Strength.xy * 0.01 * float2(nX - 0.5, nY - 0.5);

		float4 clipPos = TransformWorldToHClip(input.positionWS);
		float4 screen = ComputeScreenPos(clipPos);
		float2 screenUV = screen.xy / max(screen.w, 1e-5);
		float4 scene = SampleGrab(screenUV + offset);
		scene.a = 1.0;

		float4 tint = input.color * _RendererColor;
		return scene * tint;
	}
	ENDHLSL

	SubShader
	{
		Tags
		{
			"RenderPipeline"="UniversalPipeline"
			"Queue"="Transparent"
			"RenderType"="Transparent"
			"IgnoreProjector"="True"
			"ShaderGraphShader"="true"
			"UniversalMaterialType"="Unlit"
		}
		Cull Off
		ZWrite Off
		ZTest LEqual
		Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

		Pass
		{
			Name "Sprite Unlit"
			Tags { "LightMode"="Universal2D" }
			Cull Off
			ZTest LEqual
			HLSLPROGRAM
			#pragma vertex DistVert
			#pragma fragment DistFrag
			#pragma target 3.0
			ENDHLSL
		}

		Pass
		{
			Name "Sprite Unlit Forward"
			Tags { "LightMode"="UniversalForward" }
			Cull Off
			ZTest LEqual
			HLSLPROGRAM
			#pragma vertex DistVert
			#pragma fragment DistFrag
			#pragma target 3.0
			ENDHLSL
		}

		Pass
		{
			Name "SRPDefaultUnlit"
			Tags { "LightMode"="SRPDefaultUnlit" }
			Cull Off
			ZTest LEqual
			HLSLPROGRAM
			#pragma vertex DistVert
			#pragma fragment DistFrag
			#pragma target 3.0
			ENDHLSL
		}
	}
	Fallback "Hidden/Shader Graph/FallbackError"
	CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
}
