Shader "Hidden/TwoPolygons/SkyToOctaConvert"
{
	Properties
	{
		_MainTex  ("", 2D) = "white" {}
		_BlueNoise("", 2D) = "black" {}
	}
	SubShader
	{
		Name "Convert To Octa"
		Pass
		{
CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#pragma multi_compile _SOURCE_CUBE _SOURCE_LATLON _SOURCE_LATLON_HALF
#pragma multi_compile _OUT_HALF _OUT_FULL
#pragma shader_feature _TONEMAP

#include "UnityCG.cginc"

sampler2D _MainTex;
samplerCUBE _CubeMap;

float _Exposure;
float _ToneMap_Shoulder;

float3 OctaHemiEnc(float2 coord)
{
	coord = float2(coord.x + coord.y, coord.x - coord.y) * 0.5;
	float3 vec = float3(coord.x, 1.0 - saturate(dot(float2(1.0, 1.0), abs(coord.xy))), coord.y);
return vec;
}

float3 OctaSphereEnc(float2 coord)
{
	float3 vec = float3(coord.x, 1.0 - dot(1.0, abs(coord)), coord.y);
	if (vec.y < 0.0)
	{
		float2 flip = vec.xz >= 0 ? float2(1.0, 1.0) : float2(-1.0, -1.0);
		vec.xz = (1.0 - abs(vec.zx)) * flip;
	}
return vec;
}

float3 ACESFilm(float3 hdr)
{
	float A = 2.51;
	float B = 0.06;// was 0.03
	float C = 2.43;
	float D = 0.59;
	float E = 0.14;
	D = _ToneMap_Shoulder;

return clamp((hdr * (A * hdr + B)) / (hdr * (C * hdr + D) + E), 0.0, 1.0);
}

float4 frag(v2f_img i) : SV_Target
{
	float2 coords = i.uv * 2.0 - 1.0;
	float3 octaCoords = 0.0;
#if _OUT_HALF
	octaCoords = normalize(OctaHemiEnc(coords));
#elif _OUT_FULL
	octaCoords = normalize(OctaSphereEnc(coords));
#endif

	float2 latLon = float2(atan2(octaCoords.x, octaCoords.z) + UNITY_PI, acos(-octaCoords.y)) / float2(2.0 * UNITY_PI, UNITY_PI);
#if _SOURCE_LATLON_HALF
	latLon.y = latLon.y * 2.0;
#endif

	float3 col = 0.0;

#if _SOURCE_CUBE
	col = texCUBElod(_CubeMap, float4(octaCoords, 0.0)).rgb;
#else
	col = tex2Dlod(_MainTex, float4(latLon, 0.0, 0.0)).rgb;
#endif

#if _TONEMAP
	col = ACESFilm(col * _Exposure);
#endif

return float4(col, 1.0);
}
ENDCG
		}

		Name "Blit"
		Pass
		{
CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#pragma shader_feature _ADD_BLUENOISE

#include "UnityCG.cginc"

sampler2D _MainTex;		float4 _MainTex_TexelSize;
sampler2D _BlueNoise;	float4 _BlueNoise_TexelSize;

float  _BlueNoiseIntensity;

float4 frag(v2f_img i) : SV_Target
{
	float3 col = tex2D(_MainTex, i.uv).rgb;

#if _ADD_BLUENOISE
	float blueNoise = tex2D(_BlueNoise, i.uv * _BlueNoise_TexelSize.xy * _MainTex_TexelSize.zw * 0.5).r * 2.0 - 1.0;
	col.rgb += blueNoise * _BlueNoiseIntensity * (1.0 / 256.0);
#endif

return float4(col, 1.0);
}
ENDCG
		}
	}
}
