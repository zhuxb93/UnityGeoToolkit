Shader "GeoToolkit/RadarVoxelSolid"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.2, 0.2, 0.8)
        _EdgeColor ("Edge Color", Color) = (1, 0.5, 0.5, 1)
        _EdgeWidth ("Edge Width", Range(0, 1)) = 0.1
        _EdgeIntensity ("Edge Intensity", Range(0, 5)) = 1.5
        _Transparency ("Transparency", Range(0, 1)) = 0.8
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        LOD 200
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
                float2 uv : TEXCOORD2;
                UNITY_FOG_COORDS(3)
            };

            float4 _Color;
            float4 _EdgeColor;
            float _EdgeWidth;
            float _EdgeIntensity;
            float _Transparency;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = normalize(_WorldSpaceCameraPos - worldPos);
                o.uv = v.uv;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 网格线效果 - 计算到边缘的距离
                float2 edgeDist = min(i.uv, 1.0 - i.uv); // 到最近边缘的距离
                float gridLine = min(edgeDist.x, edgeDist.y);

                // 在边缘附近为1，中心为0
                gridLine = 1.0 - smoothstep(0.0, _EdgeWidth, gridLine);

                // Fresnel边缘高亮
                float fresnel = 1.0 - saturate(dot(normalize(i.viewDir), normalize(i.worldNormal)));
                fresnel = pow(fresnel, 1.5);

                // 组合边缘
                float edge = max(gridLine, fresnel * 0.3);
                edge = saturate(edge * _EdgeIntensity);

                // 混合颜色
                float4 finalColor = lerp(_Color, _EdgeColor, edge);
                finalColor.a = _Transparency;

                // 应用雾效
                UNITY_APPLY_FOG(i.fogCoord, finalColor);

                return finalColor;
            }
            ENDCG
        }
    }

    FallBack "Diffuse"
}