Shader "Objects Logic Studio/Sauce Stream"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" }
        Pass
        {
            Name "Sauce Column"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SauceVertex
            #pragma fragment SauceFragment
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct SauceInput
            {
                float4 Position : POSITION;
                float3 Normal : NORMAL;
                half4 Color : COLOR;
                float2 Surface : TEXCOORD0;
            };
            struct SauceVaryings
            {
                float4 Position : SV_POSITION;
                float3 World : TEXCOORD0;
                float3 Normal : TEXCOORD1;
                half4 Color : COLOR;
                float2 Surface : TEXCOORD2;
            };

            // Carries per-sample colour and gloss through one shared, dynamically connected mesh.
            SauceVaryings SauceVertex(SauceInput input)
            {
                SauceVaryings output;
                output.World = TransformObjectToWorld(input.Position.xyz);
                output.Position = TransformWorldToHClip(output.World);
                output.Normal = TransformObjectToWorldNormal(input.Normal);
                output.Color = input.Color;
                output.Surface = float2(input.Surface.x, ComputeFogFactor(output.Position.z));
                return output;
            }

            // Uses the actual column normals for a continuous wet highlight instead of particle silhouettes.
            half4 SauceFragment(SauceVaryings input) : SV_Target
            {
                clip(input.Color.a - 0.001);
                float3 normal = normalize(input.Normal);
                float3 view = GetWorldSpaceNormalizeViewDir(input.World);
                Light light = GetMainLight(TransformWorldToShadowCoord(input.World));
                half3 illumination = max(SampleSH(normal), half3(0.12, 0.12, 0.12))
                    + light.color * saturate(dot(normal, light.direction)) * light.distanceAttenuation * light.shadowAttenuation;
                half gloss = saturate(input.Surface.x);
                half highlight = pow(saturate(dot(normal, normalize(light.direction + view))), lerp(12.0, 120.0, gloss)) * gloss;
                half3 color = input.Color.rgb * illumination + light.color * highlight * light.shadowAttenuation * 0.45;
                return half4(MixFog(color, input.Surface.y), input.Color.a);
            }
            ENDHLSL
        }
    }
}
