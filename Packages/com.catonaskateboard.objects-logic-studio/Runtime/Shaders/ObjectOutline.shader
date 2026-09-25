Shader "Objects Logic Studio/Edge Glow"
{
    Properties
    {
        [HideInInspector]
        _ObjectGlowColor ("Per-object glow radiance", Color) = (1, 1, 1, 1)
        [HideInInspector]
        _ObjectGlowShape ("Pixel width and crease cosine", Vector) = (3, 0.8660254, 0, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Visible Surfaces"
            Cull Back
            ZWrite Off
            ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SurfaceVertex
            #pragma fragment SurfaceFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _ObjectGlowColor;
                float4 _ObjectGlowShape;
            CBUFFER_END
            struct SurfaceInput
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct SurfaceVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            struct SurfaceOutput
            {
                half4 normalWidth : SV_Target0;
                half4 colorAngle : SV_Target1;
            };
            /// <summary>Projects the original mesh without extrusion or scale changes.</summary>
            /// <param name="input">Original, possibly skinned, vertex data.</param>
            /// <returns>Original clip position and world normal.</returns>
            SurfaceVaryings SurfaceVertex(SurfaceInput input)
            {
                SurfaceVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            /// <summary>Stores only surfaces that passed the camera's real depth test.</summary>
            /// <param name="input">Visible original surface.</param>
            /// <returns>Normal/width and premultiplied radiance/crease threshold.</returns>
            SurfaceOutput SurfaceFragment(SurfaceVaryings input)
            {
                SurfaceOutput output;
                output.normalWidth = half4(normalize(input.normalWS), _ObjectGlowShape.x);
                output.colorAngle = half4(_ObjectGlowColor.rgb, _ObjectGlowShape.y);
                return output;
            }
            ENDHLSL
        }
        Pass
        {
            Name "Visible Edge Glow"
            Cull Off
            ZWrite Off
            ZTest Always
            Blend One One, Zero One
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment GlowFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D_X(_ObjectGlowColors);

            /// <summary>Finds the nearest visible crease or silhouette along one screen direction.</summary>
            /// <param name="uv">Current surface pixel.</param>
            /// <param name="direction">Unit pixel-space sampling direction.</param>
            /// <param name="center">Center normal and authored pixel width.</param>
            /// <param name="depth">Center eye-space depth.</param>
            /// <param name="gradient">Planar depth gradient used to reject ordinary sloping surfaces.</param>
            /// <param name="angle">Minimum accepted normal cosine.</param>
            /// <returns>A soft edge contribution from zero to one.</returns>
            float FindEdge(float2 uv, float2 direction, float4 center, float depth, float2 gradient, float angle)
            {
                float result = 0;
                // Four radial steps approximate a feathered band with bounded texture work.
                [unroll]
                for (int index = 1; index <= 4; index++)
                {
                    float radius = center.w * (index * 0.25);
                    float2 delta = direction * radius;
                    float2 sampleUv = uv + delta * _BlitTexture_TexelSize.xy;
                    float4 neighbor = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, sampleUv, 0);
                    float neighborDepth = LinearEyeDepth(SampleSceneDepth(sampleUv), _ZBufferParams);
                    float expected = depth + dot(gradient, delta);
                    float tolerance = max(0.003, depth * 0.003);
                    bool crease = neighbor.w > 0 && dot(center.xyz, neighbor.xyz) < angle;
                    bool boundary = neighbor.w <= 0 && neighborDepth > expected + tolerance;
                    bool gap = neighbor.w > 0 && abs(neighborDepth - expected) > tolerance * 4;
                    if (crease || boundary || gap)
                        result = max(result, exp2(-8.0 * index * index / 16.0));
                }
                return result;
            }

            /// <summary>Adds glow on original visible surfaces, leaving occluders and hidden edges untouched.</summary>
            /// <param name="input">Fullscreen coordinates supplied by URP's blitter.</param>
            /// <returns>Additive edge radiance with zero alpha contribution.</returns>
            half4 GlowFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float4 center = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0);
                float depth = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                float2 gradient = float2(ddx(depth), ddy(depth));
                // Non-outlined pixels perform no neighborhood sampling and receive no light through walls.
                if (center.w <= 0)
                    return 0;
                float4 color = SAMPLE_TEXTURE2D_X_LOD(_ObjectGlowColors, sampler_PointClamp, uv, 0);
                float glow = 0;
                glow = max(glow, FindEdge(uv, float2(1, 0), center, depth, gradient, color.a));
                glow = max(glow, FindEdge(uv, float2(-1, 0), center, depth, gradient, color.a));
                glow = max(glow, FindEdge(uv, float2(0, 1), center, depth, gradient, color.a));
                glow = max(glow, FindEdge(uv, float2(0, -1), center, depth, gradient, color.a));
                return half4(color.rgb * glow, 0);
            }
            ENDHLSL
        }
    }
}
