// Decompiled from Particles/VertexSpikeDissolve
// sharedassets1 path_id 79, VS 00 / PS 01 (SOFT keyword stripped; this is SOFT off).
//
// Pass: Cull Off, ZTest Always, ZWrite Off, ColorMask RGB
//       Blend One OneMinusSrcAlpha, BlendOp [_BlendOp]
//
// TEXCOORD0.xy = mesh UV, .z = AgePercent (streams 00 01 03 04 15)
// VS: o.z = age - _Delay
//     spikeOn = Noise(uv*_Scale) >= _Spike
//     pos += spikeOn * normal * _Growth + normal * age * _Stretch
// PS: n = (NoiseFrag(uv*_ExtraScale) + Extra(uv*_FragScale)) * 0.5
//     s = smoothstep(0,1, saturate((n - age*_Cutoff) / _Fuzziness))
//     rgba = s * (Extra*_EdgeColor + lerp(_Tint, _Color, s)) * vertex
//
// Melee4After: sphere mesh, _Spike=0, _Growth=0.71, _Stretch=1, _Tint.a=0.

Shader "Particles/VertexSpikeDissolve"
{
	Properties
	{
		_Noise ("Vert Noise Texture", 2D) = "white" {}
		_Scale ("Vert Noise Scale", Range(0, 10)) = 0.5
		_NoiseFrag ("Frag Noise Texture", 2D) = "white" {}
		_FragScale ("Frag Noise Scale", Range(0, 2)) = 0.5
		_ExtraNoise ("Overlay Noise Texture", 2D) = "white" {}
		_ExtraScale ("Overlay Noise Scale", Range(0, 2)) = 0.5
		[HDR] _Color ("Overlay Noise Color", Color) = (1, 0.5, 0, 0)
		[HDR] _Tint ("Tint", Color) = (1, 1, 0, 0)
		[HDR] _EdgeColor ("Edge", Color) = (1, 0.5, 0, 0)
		_Fuzziness ("Fuzziness", Range(0, 2)) = 0.3
		_Stretch ("Dissolve Stretch", Range(0, 4)) = 2
		_Growth ("Growth", Range(0, 2)) = 0
		_Spike ("Spike", Range(0, 5)) = 1
		_Cutoff ("Cutoff", Range(0, 1)) = 0.9
		_Delay ("Dissolve Delay", Range(0, 2)) = 0
		[Toggle(SOFT)] _SOFT ("Soft Spikes", Float) = 0
		[Enum(UnityEngine.Rendering.BlendOp)] _BlendOp ("Blend Op", Float) = 0
		[HideInInspector] _FragBlur ("Explosion Tex Blur", Range(0, 8)) = 0
		[HideInInspector] _CircleClip ("Clip To Circle (2D)", Float) = 0
		[HideInInspector] _UniformExpand ("Uniform Expand (no needles)", Float) = 0
	}

	HLSLINCLUDE
	#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

	TEXTURE2D(_Noise);
	SAMPLER(sampler_Noise);
	TEXTURE2D(_NoiseFrag);
	SAMPLER(sampler_NoiseFrag);
	TEXTURE2D(_ExtraNoise);
	SAMPLER(sampler_ExtraNoise);

	CBUFFER_START(UnityPerMaterial)
		float4 _Noise_ST;
		float4 _Color;
		float4 _Tint;
		float4 _EdgeColor;
		float _Scale;
		float _FragScale;
		float _ExtraScale;
		float _Fuzziness;
		float _Stretch;
		float _Growth;
		float _Spike;
		float _Cutoff;
		float _Delay;
		float _SOFT;
		float _FragBlur;
		float _CircleClip;
		float _UniformExpand;
	CBUFFER_END

	struct Attributes
	{
		float4 positionOS : POSITION;
		float3 normalOS   : NORMAL;
		float4 uv         : TEXCOORD0;
		float4 color      : COLOR;
	};

	struct Varyings
	{
		float4 positionCS : SV_POSITION;
		float3 uvAge      : TEXCOORD0;
		float4 color      : COLOR;
	};

	Varyings SpikeVert(Attributes input)
	{
		Varyings output;
		float2 st = input.uv.xy * _Noise_ST.xy + _Noise_ST.zw;
		float age = input.uv.z;
		float2 nUV = input.uv.xy * _Scale;
		float n = SAMPLE_TEXTURE2D_LOD(_Noise, sampler_Noise, nUV, 1.0).x;
		float spikeOn = n >= _Spike ? 1.0 : 0.0;
		float3 pos = input.positionOS.xyz;
		pos += spikeOn * input.normalOS * _Growth;
		pos += input.normalOS * age * _Stretch;
		output.positionCS = TransformObjectToHClip(pos);
		output.uvAge = float3(st, age - _Delay);
		output.color = input.color;
		return output;
	}

	float4 SpikeFrag(Varyings input) : SV_Target
	{
		float2 uvFrag = input.uvAge.xy * _ExtraScale;
		float2 uvExtra = input.uvAge.xy * _FragScale;
		float n0 = SAMPLE_TEXTURE2D(_NoiseFrag, sampler_NoiseFrag, uvFrag).x;
		float4 extra = SAMPLE_TEXTURE2D(_ExtraNoise, sampler_ExtraNoise, uvExtra);
		float n = (n0 + extra.x) * 0.5 - input.uvAge.z * _Cutoff;
		float s = saturate(n / max(_Fuzziness, 1e-5));
		s = s * s * (3.0 - 2.0 * s);
		float4 col = lerp(_Tint, _Color, s);
		col = extra * _EdgeColor + col;
		col *= s;
		return col * input.color;
	}
	ENDHLSL

	SubShader
	{
		Tags
		{
			"RenderPipeline"="UniversalPipeline"
			"RenderType"="Transparent"
			"Queue"="Transparent"
			"IgnoreProjector"="True"
			"PreviewType"="Plane"
		}
		Cull Off
		ZWrite Off
		ZTest Always
		ColorMask RGB
		Blend One OneMinusSrcAlpha
		BlendOp [_BlendOp]

		Pass
		{
			Name "SRPDefaultUnlit"
			Tags { "LightMode"="SRPDefaultUnlit" }
			HLSLPROGRAM
			#pragma vertex SpikeVert
			#pragma fragment SpikeFrag
			ENDHLSL
		}
		Pass
		{
			Name "Universal2D"
			Tags { "LightMode"="Universal2D" }
			HLSLPROGRAM
			#pragma vertex SpikeVert
			#pragma fragment SpikeFrag
			ENDHLSL
		}
		Pass
		{
			Name "UniversalForward"
			Tags { "LightMode"="UniversalForward" }
			HLSLPROGRAM
			#pragma vertex SpikeVert
			#pragma fragment SpikeFrag
			ENDHLSL
		}
	}
}
