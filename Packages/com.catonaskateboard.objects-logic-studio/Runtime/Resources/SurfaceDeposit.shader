Shader "Objects Logic Studio/Surface Deposit"
{
    Properties
    {
        [HideInInspector]
        DepositColor ("Primary deposit colour", Color) = (1, 1, 1, 1)
        [HideInInspector]
        DepositBlend ("Secondary deposit colour", Color) = (1, 1, 1, 1)
        [HideInInspector]
        DepositShape ("Silhouette, irregularity, random seed, volume mode", Vector) = (0, 0.5, 0, 0)
        [HideInInspector]
        DepositSurface ("Liquidity, gloss, colour blend, opacity", Vector) = (0.5, 0.8, 0.5, 1)
        [HideInInspector]
        DepositTiming ("Birth time, lifetime, fade duration, reserved", Vector) = (0, 10, 3, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" }
        Pass
        {
            Name "Surface Deposit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepositVertex
            #pragma fragment DepositFragment
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 DepositColor;
                float4 DepositBlend;
                float4 DepositShape;
                float4 DepositSurface;
                float4 DepositTiming;
            CBUFFER_END

            struct DepositInput
            {
                float4 Position : POSITION;
                float3 Normal : NORMAL;
                float2 UV : TEXCOORD0;
            };
            struct DepositVaryings
            {
                float4 Position : SV_POSITION;
                float3 World : TEXCOORD0;
                float3 Normal : TEXCOORD1;
                float2 UV : TEXCOORD2;
                float Fog : TEXCOORD3;
            };

            // Maps existing surface vertices to the current receiver pose without moving gameplay geometry.
            DepositVaryings DepositVertex(DepositInput input)
            {
                DepositVaryings output;
                output.World = TransformObjectToWorld(input.Position.xyz);
                output.Position = TransformWorldToHClip(output.World);
                output.Normal = TransformObjectToWorldNormal(input.Normal);
                output.UV = input.UV;
                output.Fog = ComputeFogFactor(output.Position.z);
                return output;
            }

            // Produces deterministic cell variation from a per-deposit seed.
            float Grain(float2 cell)
            {
                return frac(sin(dot(cell, float2(31.71, 93.43)) + DepositShape.z * 7.13) * 17831.17);
            }

            // Blends neighbouring samples so colour variation does not expose a rectangular grid.
            float Composition(float2 coordinate)
            {
                float2 cell = floor(coordinate);
                float2 blend = frac(coordinate);
                blend = blend * blend * (3.0 - 2.0 * blend);
                return lerp(lerp(Grain(cell), Grain(cell + float2(1.0, 0.0)), blend.x),
                    lerp(Grain(cell + float2(0.0, 1.0)), Grain(cell + 1.0), blend.x), blend.y);
            }

            // Scatters differently rotated fragments inside the patch without requiring texture assets.
            float Fragments(float2 coordinate)
            {
                float2 cell = floor(coordinate);
                float random = Grain(cell);
                float2 offset = float2(Grain(cell + 17.0), Grain(cell + 43.0)) - 0.5;
                float2 local = frac(coordinate) - 0.5 - offset * 0.6;
                float turn = random * 6.283185;
                float2 rotated = float2(local.x * cos(turn) - local.y * sin(turn), local.x * sin(turn) + local.y * cos(turn));
                float distance = length(rotated * float2(1.5, 0.8));
                float radius = lerp(0.1, 0.42, random);
                return (1.0 - smoothstep(radius - 0.04, radius, distance)) * smoothstep(0.1, 0.3, random);
            }

            // Shades dry fragments and glossy liquid with the same continuously fading surface mask.
            half4 DepositFragment(DepositVaryings input) : SV_Target
            {
                float2 centered = input.UV * 2.0 - 1.0;
                float angle = atan2(centered.y, centered.x);
                float lobes = sin(angle * 5.0 + DepositShape.z) * 0.6 + sin(angle * 9.0 - DepositShape.z) * 0.4;
                float radius = 0.8 + lobes * DepositShape.y * (DepositShape.x > 0.5 ? 0.18 : 0.04);
                float distance = length(centered);
                float mask = 1.0 - smoothstep(radius - 0.08, radius, distance);
                float grain = Composition(input.UV * 7.0);
                float fragments = Fragments(input.UV * 10.0);
                float dry = DepositShape.x > 2.5 ? 1.0 : (1.0 - DepositSurface.x) * 0.7;
                mask *= lerp(1.0, fragments, dry);
                if (DepositShape.x > 1.5 && DepositShape.x < 2.5)
                    mask *= 1.0 - smoothstep(0.1, 1.0, abs(centered.y));
                if (DepositShape.w > 0.5)
                    mask = 1.0;
                float remaining = DepositTiming.y - (_Time.y - DepositTiming.x);
                float fade = smoothstep(0.0, max(0.001, DepositTiming.z), remaining);
                float blend = saturate((grain * 0.4 + sin(input.UV.x * 7.0 + input.UV.y * 11.0 + DepositShape.z) * 0.3 + 0.3) * DepositSurface.z);
                half4 tint = lerp(DepositColor, DepositBlend, blend);
                half opacity = mask * fade * DepositSurface.w * tint.a;
                clip(opacity - 0.001);
                float3 normal = normalize(input.Normal);
                float3 view = GetWorldSpaceNormalizeViewDir(input.World);
                Light light = GetMainLight(TransformWorldToShadowCoord(input.World));
                half diffuse = saturate(dot(normal, light.direction));
                half3 illumination = max(SampleSH(normal), half3(0.08, 0.08, 0.08))
                    + light.color * diffuse * light.distanceAttenuation * light.shadowAttenuation;
                float wet = DepositSurface.x * DepositSurface.y;
                half highlight = pow(saturate(dot(normal, normalize(light.direction + view))), lerp(8.0, 160.0, wet)) * wet;
                half3 color = tint.rgb * illumination + light.color * highlight * light.shadowAttenuation * 0.35;
                return half4(MixFog(color, input.Fog), opacity);
            }
            ENDHLSL
        }
    }
}
