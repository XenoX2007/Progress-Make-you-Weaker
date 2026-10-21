// Vệt sáng cộng màu cho LineRenderer (sao băng). Dùng màu đỉnh của LineRenderer, mờ dần ở hai mép.
Shader "Night/Glow Line"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Float) = 4
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "GlowLine"
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float across = 1.0 - abs(input.uv.y * 2.0 - 1.0);
                float soft = across * across;
                float3 color = input.color.rgb * _Color.rgb * (_Intensity * input.color.a * soft);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
