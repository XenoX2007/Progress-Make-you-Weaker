// Skybox đêm: gradient trời, ánh sáng chân trời, mặt trăng và dải Ngân Hà.
Shader "Night/Skybox"
{
    Properties
    {
        _ZenithColor ("Zenith Color", Color) = (0.008, 0.012, 0.04, 1)
        _SkyColor ("Sky Color", Color) = (0.03, 0.05, 0.14, 1)
        _HorizonColor ("Horizon Color", Color) = (0.12, 0.14, 0.32, 1)
        _HorizonGlowColor ("Horizon Glow Color", Color) = (0.32, 0.2, 0.45, 1)
        _HorizonGlowStrength ("Horizon Glow Strength", Range(0, 2)) = 0.55
        _GroundColor ("Below Horizon Color", Color) = (0.02, 0.025, 0.06, 1)

        [Header(Moon)]
        _MoonDirection ("Moon Direction", Vector) = (0.441, 0.469, 0.765, 0)
        [HDR] _MoonColor ("Moon Color", Color) = (2.6, 2.5, 2.2, 1)
        _MoonSize ("Moon Size (degrees)", Range(0.5, 10)) = 3.2
        [HDR] _MoonGlowColor ("Moon Glow Color", Color) = (0.3, 0.36, 0.6, 1)
        _MoonGlowFalloff ("Moon Glow Falloff", Range(1, 100)) = 28

        [Header(Milky Way)]
        _BandNormal ("Milky Way Plane Normal", Vector) = (-0.791, 0.361, -0.493, 0)
        _BandColor ("Milky Way Color", Color) = (0.3, 0.26, 0.55, 1)
        _BandWidth ("Milky Way Sharpness", Range(1, 60)) = 16
        _BandStrength ("Milky Way Strength", Range(0, 2)) = 0.45
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ZenithColor;
                float4 _SkyColor;
                float4 _HorizonColor;
                float4 _HorizonGlowColor;
                float _HorizonGlowStrength;
                float4 _GroundColor;
                float4 _MoonDirection;
                float4 _MoonColor;
                float _MoonSize;
                float4 _MoonGlowColor;
                float _MoonGlowFalloff;
                float4 _BandNormal;
                float4 _BandColor;
                float _BandWidth;
                float _BandStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 directionOS : TEXCOORD0;
            };

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float ValueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float n000 = Hash31(i);
                float n100 = Hash31(i + float3(1, 0, 0));
                float n010 = Hash31(i + float3(0, 1, 0));
                float n110 = Hash31(i + float3(1, 1, 0));
                float n001 = Hash31(i + float3(0, 0, 1));
                float n101 = Hash31(i + float3(1, 0, 1));
                float n011 = Hash31(i + float3(0, 1, 1));
                float n111 = Hash31(i + float3(1, 1, 1));

                float x00 = lerp(n000, n100, f.x);
                float x10 = lerp(n010, n110, f.x);
                float x01 = lerp(n001, n101, f.x);
                float x11 = lerp(n011, n111, f.x);
                return lerp(lerp(x00, x10, f.y), lerp(x01, x11, f.y), f.z);
            }

            float Fbm(float3 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                for (int k = 0; k < 5; k++)
                {
                    value += amplitude * ValueNoise(p);
                    p = p * 2.03 + 17.1;
                    amplitude *= 0.5;
                }
                return value;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.directionOS = input.positionOS.xyz;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.directionOS);
                float y = dir.y;
                float up = saturate(y);

                // Gradient: chân trời -> trời -> thiên đỉnh
                float3 color = lerp(_HorizonColor.rgb, _SkyColor.rgb, smoothstep(0.0, 0.35, up));
                color = lerp(color, _ZenithColor.rgb, smoothstep(0.25, 1.0, up));

                // Ánh tím mờ sát chân trời
                color += _HorizonGlowColor.rgb * exp(-abs(y) * 9.0) * _HorizonGlowStrength;

                // Dưới đường chân trời (thường bị núi che)
                color = lerp(color, _GroundColor.rgb, smoothstep(0.0, -0.15, y));

                // Dải Ngân Hà
                float3 bandNormal = normalize(_BandNormal.xyz);
                float bandDistance = dot(dir, bandNormal);
                float band = exp(-bandDistance * bandDistance * _BandWidth);
                float clouds = Fbm(dir * 6.0);
                float dust = Fbm(dir * 14.0 + 3.7);
                float milkyWay = band * saturate(clouds * 1.7 - 0.4) * (0.6 + 0.4 * dust);
                // Vệt bụi tối ở giữa dải
                milkyWay *= 1.0 - 0.6 * exp(-bandDistance * bandDistance * _BandWidth * 6.0) * smoothstep(0.45, 0.7, dust);
                color += _BandColor.rgb * milkyWay * _BandStrength * smoothstep(-0.02, 0.25, y);

                // Mặt trăng + quầng sáng
                float3 moonDir = normalize(_MoonDirection.xyz);
                float angle = acos(clamp(dot(dir, moonDir), -1.0, 1.0));
                float moonRadius = radians(_MoonSize) * 0.5;
                float disc = 1.0 - smoothstep(moonRadius * 0.9, moonRadius, angle);

                float maria = Fbm(dir * 60.0);
                float limb = sqrt(saturate(1.0 - pow(angle / moonRadius, 2.0)));
                float3 moon = _MoonColor.rgb * (0.72 + 0.35 * maria) * (0.75 + 0.25 * limb);

                float halo = exp(-angle * _MoonGlowFalloff) + 0.15 * exp(-angle * _MoonGlowFalloff * 0.2);
                color += _MoonGlowColor.rgb * halo;
                color = lerp(color, moon, disc);

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
