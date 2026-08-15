// Pixel-exact user graph from shipped SlashShaderGraph
// data.unity3d / sharedassets13 Shader path_id 348
// PS blob 16 (Sprite Lit, no shape lights) and blob 35 (Sprite Forward)
// Unity 2021.2.7f1 Shader Graph Voronoi (no 46839.32 hash).
//
// TEXCOORD0.xy = mesh UV
// TEXCOORD1    = particle Custom1 (Path, ScaleY, Length, Density)
// COLOR        = particle color
//
// ViviSlasher Custom1: x = 0→3.32, y = 0.01, z = 1.66, w = 15.88
//
// Graph (before Sprite Lit `color *= vertex`):
//   voronoi = Voronoi(UV, Angle=Length, Density)
//   powered = pow(MainTex * voronoi, Path)          // log/exp
//   mid     = powered * Color * ScaleY
//   addA    = powered.a * Mask.a * vertex.a         // scalar, added to all channels
//   graph   = vertex * mid + addA
// Pass then: color = graph * vertex; if (a==0) discard;

#ifndef SLASH_SHADERGRAPH_DECOMPILED_INCLUDED
#define SLASH_SHADERGRAPH_DECOMPILED_INCLUDED

// Unity 2021.2.7 VoronoiNode.cs — Unity_Voronoi_RandomVector
float2 SlashVoronoiRandomVector(float2 uv, float angleOffset)
{
	float2x2 m = float2x2(15.27, 47.63, 99.41, 89.98);
	uv = frac(sin(mul(uv, m)));
	return float2(sin(uv.y * angleOffset) * 0.5 + 0.5, cos(uv.x * angleOffset) * 0.5 + 0.5);
}

float SlashVoronoiF1(float2 uv, float angleOffset, float cellDensity)
{
	float2 g = floor(uv * cellDensity);
	float2 f = frac(uv * cellDensity);
	float minD = 8.0;
	[loop]
	for (int y = -1; y <= 1; y++)
	{
		[loop]
		for (int x = -1; x <= 1; x++)
		{
			float2 lattice = float2((float)x, (float)y);
			float2 offset = SlashVoronoiRandomVector(lattice + g, angleOffset);
			minD = min(minD, distance(lattice + offset, f));
		}
	}
	return minD;
}

float4 SlashPowExact(float4 v, float p)
{
	// ASM is log/exp. Zero stays 0 so Path=0 fills every non-black texel to 1.
	float4 o = 0;
	o.x = v.x > 0.0 ? exp(log(v.x) * p) : 0.0;
	o.y = v.y > 0.0 ? exp(log(v.y) * p) : 0.0;
	o.z = v.z > 0.0 ? exp(log(v.z) * p) : 0.0;
	o.w = v.w > 0.0 ? exp(log(v.w) * p) : 0.0;
	return o;
}

float4 SlashShaderGraphUser(float2 uv, float4 custom, float4 vertexColor, float4 mainTex, float4 mask, float4 hdrColor)
{
	float path = custom.x;
	float scaleY = custom.y;
	float angle = custom.z;
	float density = custom.w;
	if (scaleY < 1e-6)
	{
		scaleY = 0.01;
	}
	if (angle < 1e-6)
	{
		angle = 1.66;
	}
	if (density < 1e-6)
	{
		density = 15.88;
	}

	float voronoi = SlashVoronoiF1(uv, angle, density);
	float4 powered = SlashPowExact(mainTex * voronoi, path);
	float4 mid = powered * hdrColor * scaleY;
	float addA = powered.w * mask.w * vertexColor.w;
	return vertexColor * mid + addA;
}

void SlashShaderGraphUser_float(float2 UV, float4 Custom1, float4 VertexColor, float4 MainTex, float4 Mask, float4 Color, out float3 BaseColor, out float Alpha)
{
	float4 o = SlashShaderGraphUser(UV, Custom1, VertexColor, MainTex, Mask, Color);
	BaseColor = o.rgb;
	Alpha = o.a;
}

void SlashParticleCustom1_float(float2 UV1, out float4 Custom1)
{
	Custom1 = float4(UV1.x, UV1.y > 1e-6 ? UV1.y : 0.01, 1.66, 15.88);
}

#endif
