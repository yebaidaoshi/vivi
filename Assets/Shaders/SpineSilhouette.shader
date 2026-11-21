// Vivi/SpineSilhouette
// 残影用的"纯色剪影"Shader：只取 Spine 图集贴图的 alpha 来抠形状，RGB 输出 _Color 的纯色。
// 注意：不能完全不采样贴图 —— Spine 图集里每个区域都是矩形，不采样 alpha 会画出一块块方块。
// 使用方式见 PlayerEvade（运行时按图集贴图建材质，不需要手动建 .mat）。
//
// _Flat = 1：硬边剪影（alpha 低于 _Cutoff 的丢弃，其余像素输出一致的不透明度），
//            配合模板缓冲，同一张残影里身体部件叠在一起的地方不会颜色变深，整体就是一块纯色。
// _Flat = 0：软边（保留图集 alpha），部件叠加处会更实。
//
// 渲染管线：没有写 LightMode 标签，走 URP 2D Renderer 的 SRPDefaultUnlit 通道（与 Spine 自带的 Spine/Skeleton 一样）。
Shader "Vivi/SpineSilhouette"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Spine Atlas Page (only alpha is used)", 2D) = "white" {}
        _Color ("Silhouette Color (A = opacity)", Color) = (0.55, 0.82, 1, 0.5)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [Toggle] _Flat ("Flat silhouette (hard edge, no overlap darkening)", Float) = 1
        [HideInInspector] _StencilRef ("Stencil Ref", Float) = 32
        [HideInInspector] _StencilComp ("Stencil Comp (6 = NotEqual, 8 = Always)", Float) = 6
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
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Stencil
        {
            Ref [_StencilRef]
            ReadMask 255
            WriteMask 255
            Comp [_StencilComp]
            Pass Replace
        }

        Pass
        {
            Name "Silhouette"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color;
            fixed _Cutoff;
            fixed _Flat;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR; // Spine 鎶?slot alpha 鍐欏湪椤剁偣鑹查噷
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed vertexAlpha : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.vertexAlpha = v.color.a;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // PMA / 闈?PMA 鍥鹃泦鐨?alpha 閫氶亾鍚箟涓€鑷达紝鍙敤瀹?
                fixed a = tex2D(_MainTex, i.uv).a * i.vertexAlpha;
                clip(a - _Cutoff);

                fixed outAlpha = lerp(a, 1.0, _Flat) * _Color.a;
                return fixed4(_Color.rgb, outAlpha);
            }
            ENDCG
        }
    }

    Fallback Off
}
