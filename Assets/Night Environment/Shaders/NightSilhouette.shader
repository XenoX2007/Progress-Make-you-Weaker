// Bóng núi/cây ở xa: không nhận ánh sáng, tô màu theo độ cao (world Y) và thêm sương ở chân núi.
Shader "Night/Silhouette"
{
    Properties
    {
        _TopColor ("Ridge Color", Color) = (0.08, 0.1, 0.2, 1)
        _BottomColor ("Base Color", Color) = (0.04, 0.05, 0.12, 1)
        _TopHeight ("Ridge Height (world Y)", Float) = 40
        _BottomHeight ("Base Height (world Y)", Float) = 0
        _HazeColor ("Haze Color", Color) = (0.12, 0.12, 0.26, 1)
        _HazeHeight ("Haze Thickness", Float) = 10
        _HazeStrength ("Haze Strength", Range(0, 1)) = 0.5
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    CBUFFER_START(UnityPerMaterial)
        float4 _TopColor;
        float4 _BottomColor;
        float _TopHeight;
        float _BottomHeight;
        float4 _HazeColor;
        float _HazeHeight;
        float _HazeStrength;
    CBUFFER_END
    ENDHLSL

    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "Silhouette"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float heightWS : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.heightWS = positionWS.y;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float t = saturate((input.heightWS - _BottomHeight) / max(0.001, _TopHeight - _BottomHeight));
                float3 color = lerp(_BottomColor.rgb, _TopColor.rgb, t * t);

                float haze = (1.0 - saturate((input.heightWS - _BottomHeight) / max(0.001, _HazeHeight))) * _HazeStrength;
                color = lerp(color, _HazeColor.rgb, haze);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            Cull Off
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            float4 DepthVert(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half DepthFrag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
