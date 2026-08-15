Shader "Particles/Anim Additive"
{
    Properties
    {
        [HDR] _TintColor ("Tint Color", Color) = (0.5, 0.5, 0.5, 0.5)
        _MainTex ("Particle Texture", 2D) = "white" {}
        _InvFade ("Soft Particles Factor", Range(0.01, 3)) = 1
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    TEXTURE2D(_MainTex);
    SAMPLER(sampler_MainTex);

    CBUFFER_START(UnityPerMaterial)
        float4 _MainTex_ST;
        float4 _TintColor;
        float _InvFade;
    CBUFFER_END

    struct Attributes
    {
        float4 positionOS : POSITION;
        float4 uv : TEXCOORD0;
        float blend : TEXCOORD1;
        float4 color : COLOR;
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float4 uv : TEXCOORD0;
        float blend : TEXCOORD1;
        float4 color : COLOR;
    };

    Varyings AnimVert(Attributes input)
    {
        Varyings output;
        output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
        output.uv = float4(TRANSFORM_TEX(input.uv.xy, _MainTex), TRANSFORM_TEX(input.uv.zw, _MainTex));
        output.blend = input.blend;
        output.color = input.color;
        return output;
    }

    float4 AnimFrag(Varyings input) : SV_Target
    {
        float4 a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv.xy);
        float4 b = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv.zw);
        float4 tex = lerp(a, b, saturate(input.blend));
        float4 col = 2.0 * input.color * _TintColor * tex;
        return col;
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
        }
        Cull Off
        ZWrite Off
        Blend SrcAlpha One

        Pass
        {
            Name "SRPDefaultUnlit"
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex AnimVert
            #pragma fragment AnimFrag
            ENDHLSL
        }
        Pass
        {
            Name "Universal2D"
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex AnimVert
            #pragma fragment AnimFrag
            ENDHLSL
        }
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex AnimVert
            #pragma fragment AnimFrag
            ENDHLSL
        }
    }
}
