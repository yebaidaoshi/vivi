Shader "Live2D Cubism/Unlit" {
	Properties {
		[PerRendererData] _MainTex ("Main Texture", 2D) = "white" {}
		[PerRendererData] cubism_ModelOpacity ("Model Opacity", Float) = 1
		[PerRendererData] cubism_MultiplyColor ("Multiply Color", Vector) = (1,1,1,1)
		[PerRendererData] cubism_ScreenColor ("Screen Color", Vector) = (0,0,0,1)
		_SrcColor ("Source Color", Float) = 0
		_DstColor ("Destination Color", Float) = 0
		_SrcAlpha ("Source Alpha", Float) = 0
		_DstAlpha ("Destination Alpha", Float) = 0
		_Cull ("Culling", Float) = 0
		[Toggle(CUBISM_MASK_ON)] cubism_MaskOn ("Mask?", Float) = 0
		[Toggle(CUBISM_INVERT_ON)] cubism_InvertOn ("Inverted?", Float) = 0
		[PerRendererData] cubism_MaskTexture ("cubism_Internal", 2D) = "white" {}
		[PerRendererData] cubism_MaskTile ("cubism_Internal", Vector) = (0,0,0,0)
		[PerRendererData] cubism_MaskTransform ("cubism_Internal", Vector) = (0,0,0,0)
	}
	//DummyShaderTextExporter
	SubShader{
		Tags { "RenderType"="Opaque" }
		LOD 200

		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;
			float4 _MainTex_ST;

			struct Vertex_Stage_Input
			{
				float4 pos : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct Vertex_Stage_Output
			{
				float2 uv : TEXCOORD0;
				float4 pos : SV_POSITION;
			};

			Vertex_Stage_Output vert(Vertex_Stage_Input input)
			{
				Vertex_Stage_Output output;
				output.uv = (input.uv.xy * _MainTex_ST.xy) + _MainTex_ST.zw;
				output.pos = mul(unity_MatrixVP, mul(unity_ObjectToWorld, input.pos));
				return output;
			}

			Texture2D<float4> _MainTex;
			SamplerState sampler_MainTex;

			struct Fragment_Stage_Input
			{
				float2 uv : TEXCOORD0;
			};

			float4 frag(Fragment_Stage_Input input) : SV_TARGET
			{
				return _MainTex.Sample(sampler_MainTex, input.uv.xy);
			}

			ENDHLSL
		}
	}
}