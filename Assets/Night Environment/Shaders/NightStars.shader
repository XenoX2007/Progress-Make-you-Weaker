// Sao nhấp nháy: mỗi sao là 1 quad billboard (4 đỉnh cùng tâm).
// UV0 = góc quad (-1..1), UV1 = (pha, tốc độ, độ nhấp nháy, cỡ), màu đỉnh: rgb = màu sao, a = độ sáng.
Shader "Night/Stars"
{
    Properties
    {
        _Size ("Star Size (world units)", Float) = 3
        _Brightness ("Brightness", Float) = 3
        _TwinkleSpeed ("Twinkle Speed", Float) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent-50" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "Stars"
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Size;
                float _Brightness;
                float _TwinkleSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 corner : TEXCOORD0;
                float4 data : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 corner : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                // Mở rộng quad trong view space để luôn quay mặt về camera
                float3 centerWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 positionVS = TransformWorldToView(centerWS);
                positionVS.xy += input.corner * _Size * input.data.w;
                output.positionCS = TransformWViewToHClip(positionVS);

                float t = _Time.y * _TwinkleSpeed * input.data.y;
                float a = 0.5 + 0.5 * sin(t + input.data.x);
                float b = 0.5 + 0.5 * sin(t * 1.618 + input.data.x * 2.3);
                float flicker = saturate(a * 0.6 + b * 0.4);
                flicker = flicker * flicker * flicker;
                float twinkle = 1.0 - input.data.z * (1.0 - flicker);

                output.color = float4(input.color.rgb, input.color.a * twinkle);
                output.corner = input.corner;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float d2 = dot(input.corner, input.corner);
                float core = exp(-d2 * 10.0);
                float halo = exp(-d2 * 3.0) * 0.25;
                float spikes = (exp(-abs(input.corner.x) * 18.0) + exp(-abs(input.corner.y) * 18.0)) * saturate(1.0 - d2) * 0.2;
                float intensity = (core + halo + spikes) * input.color.a * _Brightness;
                return half4(input.color.rgb * intensity, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
