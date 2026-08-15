// Decompiled from Shader Graphs/SlashShaderGraph (Unity 2021.2.7f1).
// Player has no .shadergraph JSON. Source: data.unity3d / sharedassets13 path_id 348.
//
// Proven from DXBC (D3DCompiler_47, PS 16 / PS 35 / VS 00):
//   Target: URP 2D Sprite Lit (Universal2D / NormalsRendering / UniversalForward)
//   Props: Color_39BB2441 (HDR), _MainTex, Texture2D_4AF1F502 (Mask)
//   Cull Off, ZTest LEqual, ZWrite Off, Blend SrcAlpha OneMinusSrcAlpha
//   TEXCOORD0.xy = mesh UV, TEXCOORD1 = Custom1, COLOR = vertex
//
// User graph (PS 35, then Sprite Lit multiplies vertex again):
//   Voronoi 2021.2 (hash 15.27/47.63/99.41/89.98, no 46839.32)
//   Angle = Custom1.z (Length 1.66); Density = Custom1.w (15.88)
//   powered = pow(MainTex * voronoi, Custom1.x)   // Path 0→3.32 dissolve
//   mid = powered * Color * Custom1.y             // y = 0.01
//   graph = vertex * mid + powered.a * Mask.a * vertex.a
//   color = graph * vertex; discard if alpha == 0
//
// Path is an exponent, not a wipe. Path=0 → every non-black texel becomes 1.

Shader "Shader Graphs/SlashShaderGraph"
{
	Properties
	{
		[HDR] Color_39BB2441 ("Color", Color) = (1,1,1,0)
		[NoScaleOffset] _MainTex ("MainTexture", 2D) = "white" {}
		[NoScaleOffset] Texture2D_4AF1F502 ("Mask", 2D) = "white" {}
	}

	HLSLINCLUDE
	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
	#include "SlashShaderGraph_Decompiled.hlsl"

	TEXTURE2D(_MainTex);
	SAMPLER(sampler_MainTex);
	TEXTURE2D(Texture2D_4AF1F502);
	SAMPLER(sampler_Texture2D_4AF1F502);

	CBUFFER_START(UnityPerMaterial)
		float4 Color_39BB2441;
	CBUFFER_END

	struct Attributes
	{
		float4 positionOS : POSITION;
		float4 uv         : TEXCOORD0;
		float4 custom1    : TEXCOORD1;
		float4 color      : COLOR;
	};

	struct Varyings
	{
		float4 positionCS : SV_POSITION;
		float2 uv         : TEXCOORD0;
		float4 custom1    : TEXCOORD1;
		float4 color      : COLOR;
	};

	Varyings SlashVert(Attributes input)
	{
		Varyings output;
		output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
		output.uv = input.uv.xy;
		output.custom1 = input.custom1;
		output.color = input.color;
		return output;
	}

	float4 SlashFrag(Varyings input) : SV_Target
	{
		float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
		float4 mask = SAMPLE_TEXTURE2D(Texture2D_4AF1F502, sampler_Texture2D_4AF1F502, input.uv);
		float4 graph = SlashShaderGraphUser(input.uv, input.custom1, input.color, tex, mask, Color_39BB2441);
		// Sprite Lit / Sprite Forward both do color *= vertex after the graph.
		float4 color = graph * input.color;
		if (color.a == 0.0)
		{
			discard;
		}
		return max(color, 0.0);
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
			"UniversalMaterialType"="Lit"
		}
		LOD 100
		Cull Off
		ZWrite Off
		ZTest LEqual
		Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

		Pass
		{
			Name "Sprite Lit"
			Tags { "LightMode"="Universal2D" }
			Cull Off
			ZTest LEqual
			HLSLPROGRAM
			#pragma vertex SlashVert
			#pragma fragment SlashFrag
			#pragma target 3.0
			ENDHLSL
		}

		Pass
		{
			Name "Sprite Forward"
			Tags { "LightMode"="UniversalForward" }
			Cull Off
			ZTest LEqual
			HLSLPROGRAM
			#pragma vertex SlashVert
			#pragma fragment SlashFrag
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
			#pragma vertex SlashVert
			#pragma fragment SlashFrag
			#pragma target 3.0
			ENDHLSL
		}
	}
	Fallback "Hidden/Shader Graph/FallbackError"
	CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
}
