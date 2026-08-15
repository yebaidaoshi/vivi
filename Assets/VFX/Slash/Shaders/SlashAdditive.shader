Shader "Vivi/Slash/Additive"
{
    Properties
    {
        [MainTexture] _MainTex ("Slash", 2D) = "white" {}
        _Noise ("Dissolve Noise", 2D) = "gray" {}
        [HDR] [MainColor] _Tint ("Tint", Color) = (0.72, 0.82, 1, 1)
        _Intensity ("Intensity", Range(0, 8)) = 2.2
        _SoftEdge ("Soft Edge", Range(0, 0.5)) = 0.1

        _Progress ("Progress (Path)", Range(0, 1)) = 1
        _TailLength ("Tail Length", Range(0.02, 1.5)) = 0.75
        _ScanSoftness ("Scan Softness", Range(0.001, 0.4)) = 0.06
        [Toggle] _ReverseDirection ("Reverse Direction", Float) = 0
        [Enum(AlongU, 0, Radial, 1)] _WipeMode ("Wipe Mode", Float) = 0

        _Opacity ("Opacity", Range(0, 1)) = 1
        _StartOpacity ("Start Opacity Power", Range(0, 80)) = 24
        [Toggle] _SideOpacity ("Side Opacity", Float) = 1
        _SideOpacityPower ("Side Opacity Power", Range(0, 80)) = 20

        _Dissolve ("Dissolve", Range(0, 1)) = 0
        _DissolveSoftness ("Dissolve Softness", Range(0.001, 0.4)) = 0.06
        _DissolveEdgeWidth ("Dissolve Edge Width", Range(0, 0.4)) = 0.08
        [HDR] _DissolveEdgeColor ("Dissolve Edge", Color) = (1, 0.55, 0.95, 1)
        _DissolveEdgeIntensity ("Dissolve Edge Intensity", Range(0, 8)) = 3.5
        _NoiseTiling ("Noise Tiling", Vector) = (2.2, 1.4, 0, 0)
        _NoiseSpeed ("Noise Speed", Vector) = (-0.35, 0.12, 0, 0)

        [HideInInspector] _Surface ("__surface", Float) = 1
        [HideInInspector] _Blend ("__blend", Float) = 2
        [HideInInspector] _Cull ("__cull", Float) = 0
        [HideInInspector] _SrcBlend ("__src", Float) = 5
        [HideInInspector] _DstBlend ("__dst", Float) = 1
        [HideInInspector] _ZWrite ("__zw", Float) = 0
        [HideInInspector] _AlphaClip ("__clip", Float) = 0
        [HideInInspector] _QueueOffset ("Queue offset", Float) = 0
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    TEXTURE2D(_MainTex);
    SAMPLER(sampler_MainTex);
    TEXTURE2D(_Noise);
    SAMPLER(sampler_Noise);

    CBUFFER_START(UnityPerMaterial)
        float4 _MainTex_ST;
        float4 _Tint;
        float4 _DissolveEdgeColor;
        float4 _NoiseTiling;
        float4 _NoiseSpeed;
        float _Intensity;
        float _SoftEdge;
        float _Progress;
        float _TailLength;
        float _ScanSoftness;
        float _ReverseDirection;
        float _WipeMode;
        float _Opacity;
        float _StartOpacity;
        float _SideOpacity;
        float _SideOpacityPower;
        float _Dissolve;
        float _DissolveSoftness;
        float _DissolveEdgeWidth;
        float _DissolveEdgeIntensity;
    CBUFFER_END

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

    Varyings SlashVert(Attributes input)
    {
        Varyings output;
        output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
        output.uv = TRANSFORM_TEX(input.uv, _MainTex);
        output.color = input.color;
        return output;
    }

    float Hash21(float2 p)
    {
        p = frac(p * float2(123.34, 345.45));
        p += dot(p, p + 34.345);
        return frac(p.x * p.y);
    }

    float SampleNoise(float2 uv)
    {
        float2 nuv = uv * _NoiseTiling.xy + _Time.y * _NoiseSpeed.xy;
        float texN = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, nuv).r;
        float proc = Hash21(nuv * 8.0);
        return saturate(texN * 0.75 + proc * 0.25);
    }

    float WipeMask(float2 uv)
    {
        float axis = uv.x;
        if (_WipeMode > 0.5)
        {
            axis = length(uv - 0.5) * 1.41421356;
        }
        if (_ReverseDirection > 0.5)
        {
            axis = 1.0 - axis;
        }

        float head = _Progress;
        float tail = head - max(_TailLength, 0.02);
        float soft = max(_ScanSoftness, 0.001);
        float enter = smoothstep(tail, tail + soft, axis);
        float leave = 1.0 - smoothstep(head - soft, head + soft * 0.25, axis);
        return saturate(enter * leave);
    }

    float SideMask(float2 uv)
    {
        if (_SideOpacity < 0.5)
        {
            return 1.0;
        }

        float v = _WipeMode > 0.5 ? length(uv - 0.5) * 2.0 : uv.y;
        float e = saturate(v) * saturate(1.0 - v) * 4.0;
        return pow(saturate(e), max(_SideOpacityPower, 0.0001) * 0.05);
    }

    float StartMask(float2 uv)
    {
        float axis = _WipeMode > 0.5 ? length(uv - 0.5) * 1.41421356 : uv.x;
        if (_ReverseDirection > 0.5)
        {
            axis = 1.0 - axis;
        }
        return saturate(axis * max(_StartOpacity, 0.0) * 0.04);
    }

    float4 SlashFrag(Varyings input) : SV_Target
    {
        float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
        float srcA = max(tex.a, max(tex.r, max(tex.g, tex.b)));
        float edge = smoothstep(0.0, _SoftEdge + 1e-4, srcA);

        float wipe = WipeMask(input.uv);
        float side = SideMask(input.uv);
        float startFade = StartMask(input.uv);

        float n = SampleNoise(input.uv);
        float d = saturate(_Dissolve);
        float soft = max(_DissolveSoftness, 0.001);
        float keep = smoothstep(d - soft, d + soft, n);
        float edgeBand = saturate(1.0 - abs(n - d) / max(_DissolveEdgeWidth, 1e-4));
        edgeBand *= step(0.001, d) * step(d, 0.999);

        float alpha = srcA * _Tint.a * input.color.a * _Opacity * edge * wipe * side * startFade * keep;
        // TrailRGB1 is an R/G/B layer mask; grayscale trails still work via max().
        float3 col = _Tint.rgb * input.color.rgb * _Intensity;
        col += _DissolveEdgeColor.rgb * edgeBand * _DissolveEdgeIntensity * srcA;
        return float4(col, saturate(alpha));
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
        LOD 100
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha One

        // 2D Renderer draws MeshRenderer with this pass.
        Pass
        {
            Name "SRPDefaultUnlit"
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex SlashVert
            #pragma fragment SlashFrag
            #pragma target 2.0
            ENDHLSL
        }

        // SpriteRenderer / 2D lights path.
        Pass
        {
            Name "Universal2D"
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex SlashVert
            #pragma fragment SlashFrag
            #pragma target 2.0
            ENDHLSL
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex SlashVert
            #pragma fragment SlashFrag
            #pragma target 2.0
            ENDHLSL
        }
    }
    FallBack Off
}
