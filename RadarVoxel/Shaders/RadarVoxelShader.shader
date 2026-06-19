Shader "GeoToolkit/RadarVoxel"
{
    Properties
    {
        _Color ("Color", Color) = (0.2, 0.8, 1, 0.3)
        _EdgeColor ("Edge Color", Color) = (0.5, 1, 1, 1)
        _EdgeWidth ("Edge Width", Range(0, 1)) = 0.15
        _EdgeIntensity ("Edge Intensity", Range(0, 5)) = 2.0
        _Transparency ("Transparency", Range(0, 1)) = 0.3
        _FresnelPower ("Fresnel Power", Range(0.1, 5)) = 2.0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
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
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 viewDir : TEXCOORD2;
                float2 uv : TEXCOORD3;
                UNITY_FOG_COORDS(4)
            };

            float4 _Color;
            float4 _EdgeColor;
            float _EdgeWidth;
            float _EdgeIntensity;
            float _Transparency;
            float _FresnelPower;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = normalize(_WorldSpaceCameraPos - o.worldPos);
                o.uv = v.uv;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Fresnel效果（边缘高亮）
                float fresnel = 1.0 - saturate(dot(normalize(i.viewDir), normalize(i.worldNormal)));
                fresnel = pow(fresnel, _FresnelPower);

                // 网格线效果 - 计算到边缘的距离
                float2 edgeDist = min(i.uv, 1.0 - i.uv); // 到最近边缘的距离，范围0-0.5
                float gridLine = min(edgeDist.x, edgeDist.y); // 取最小值

                // 在边缘附近为1，中心为0
                gridLine = 1.0 - smoothstep(0.0, _EdgeWidth, gridLine);

                // 组合边缘效果
                float edge = max(fresnel * 0.5, gridLine);
                edge = saturate(edge * _EdgeIntensity);

                // 混合中心颜色和边缘颜色
                float4 finalColor = lerp(_Color, _EdgeColor, edge);

                // 设置透明度 - 边缘更不透明，中心更透明
                finalColor.a = lerp(_Transparency, 1.0, edge);

                // 应用雾效
                UNITY_APPLY_FOG(i.fogCoord, finalColor);

                return finalColor;
            }
            ENDCG
        }
    }

    FallBack "Transparent/Diffuse"
}