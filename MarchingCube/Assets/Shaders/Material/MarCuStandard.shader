﻿Shader "Custom/MarCuStandard"
{
	 Properties
    {
        _Color("Color", Color) = (1,1,1,1)
        _MainTex("Albedo (RGB)", 2D) = "white" {}
        _Glossiness("Smoothness", Range(0,1)) = 0.5
        _Metallic("Metallic", Range(0,1)) = 0.0
    }

	SubShader
	{
        Tags { "LightMode" = "ForwardBase" }

		Cull Off

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"
			#include "../Includes/Triangle.hlsl"

			sampler2D _MainTex;
			float4 _Color;
            float _Glossiness;
            float _Metallic;

			StructuredBuffer<Triangle> triangleBuffer;

			struct appdata
            {
                uint vertexID : SV_VertexID;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 normal : TEXCOORD1;
            };

			v2f vert(appdata v)
            {
				v2f o;

                int triIdx = v.vertexID / 3;
                int vertIdx = v.vertexID % 3;

                float3 v0 = triangleBuffer[triIdx].vertexA;
                float3 v1 = triangleBuffer[triIdx].vertexB;
                float3 v2 = triangleBuffer[triIdx].vertexC;

                float3 normal = normalize(cross(v1 - v0, v2 - v0));

                float3 vertex;
                if (vertIdx == 0) vertex = v0;
                else if (vertIdx == 1) vertex = v1;
                else vertex = v2;

                o.pos = UnityWorldToClipPos(float4(vertex, 1.0));
                o.worldPos = vertex;
                o.normal = normal;
                return o;
            }

			float4 frag (v2f i) : SV_Target
			{
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float3 normal = normalize(i.normal);
                float NdotL = abs(dot(normal, lightDir));

                float3 albedo = tex2D(_MainTex, float2(0.5, 0.5)).rgb * _Color.rgb;

                float3 diffuse = albedo * float3(1,1,1) * NdotL;

                return float4(diffuse, _Color.a);
			}
			ENDCG
		}
	}
}