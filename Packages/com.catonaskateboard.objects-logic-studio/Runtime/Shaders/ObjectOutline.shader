Shader "Objects Logic Studio/Edge Glow"
{
    Properties
    {
        [HideInInspector]
        _ObjectGlowColor ("Per-object glow radiance", Color) = (1, 1, 1, 1)
        [HideInInspector]
        _ObjectGlowWidth ("Inner silhouette width in render pixels", Range(0, 16)) = 3
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Visible Surfaces"
            Cull Off
            ZWrite Off
            ZTest Equal
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SurfaceVertex
            #pragma fragment SurfaceFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _ObjectGlowColor;
                float _ObjectGlowWidth;
            CBUFFER_END
            struct SurfaceInput
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct SurfaceVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            struct SurfaceOutput
            {
                half4 colorWidth : SV_Target0;
                float depth : SV_Target1;
            };
            /// <summary>Projects the original mesh without extrusion or scale changes.</summary>
            /// <param name="input">Original, possibly skinned, vertex data.</param>
            /// <returns>Original clip position with stereo state.</returns>
            SurfaceVaryings SurfaceVertex(SurfaceInput input)
            {
                // Match the original vertex transform so the depth test remains aligned.
                SurfaceVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }
            /// <summary>Stores only surfaces that passed the camera's real depth test.</summary>
            /// <param name="input">Visible original surface.</param>
            /// <returns>Radiance, silhouette width and the original surface depth.</returns>
            SurfaceOutput SurfaceFragment(SurfaceVaryings input)
            {
                // The equality test rejects clipped texels and faces absent from the source draw.
                SurfaceOutput output;
                output.colorWidth = half4(_ObjectGlowColor.rgb, _ObjectGlowWidth);
                output.depth = input.positionCS.z;
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
            TEXTURE2D_X_FLOAT(_ObjectGlowDepth);
            static const float2 directions[8] =
            {
                float2(1, 0), float2(-1, 0), float2(0, 1), float2(0, -1),
                float2(0.70710678, 0.70710678), float2(-0.70710678, 0.70710678),
                float2(0.70710678, -0.70710678), float2(-0.70710678, -0.70710678)
            };

            /// <summary>Converts a depth sample for perspective and orthographic cameras.</summary>
            /// <param name="depth">Raw depth from the scene or outlined surface.</param>
            /// <returns>Distance along the camera's view axis.</returns>
            float EyeDepth(float depth)
            {
                // Orthographic depth is already linear before conversion to view units.
                return unity_OrthoParams.w > 0 ? LinearDepthToEyeDepth(depth) : LinearEyeDepth(depth, _ZBufferParams);
            }

            /// <summary>Finds background beyond the visible silhouette in one screen direction.</summary>
            /// <param name="uv">Current surface pixel.</param>
            /// <param name="direction">Unit pixel-space sampling direction.</param>
            /// <param name="width">Inner silhouette width in render pixels.</param>
            /// <param name="depth">Center eye-space depth.</param>
            /// <param name="gradient">Planar depth gradient used to reject ordinary sloping surfaces.</param>
            /// <returns>A soft edge contribution from zero to one.</returns>
            float FindSilhouette(float2 uv, float2 direction, float width, float depth, float2 gradient)
            {
                // Four radial steps approximate a feathered band with bounded texture work.
                [unroll]
                for (int index = 1; index <= 4; index++)
                {
                    float2 delta = direction * (width * index * 0.25);
                    float2 sampleUv = uv + delta * _BlitTexture_TexelSize.xy;
                    // Leaving the viewport is clipping, not an object silhouette.
                    if (any(sampleUv < 0) || any(sampleUv > _BlitScaleBias.xy))
                        continue;
                    // Coverage alone identifies the boundary; normals and internal depth gaps are ignored.
                    if (SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, sampleUv, 0).a > 0)
                        continue;
                    // Foreground occluders must not create an artificial outline across the surface.
                    if (EyeDepth(SampleSceneDepth(sampleUv)) > depth + dot(gradient, delta) + max(0.003, depth * 0.003))
                        return exp2(-8.0 * index * index / 16.0);
                }
                return 0;
            }

            /// <summary>Adds a soft inner silhouette band without lighting the surface interior.</summary>
            /// <param name="input">Fullscreen coordinates supplied by URP's blitter.</param>
            /// <returns>Additive edge radiance with zero alpha contribution.</returns>
            half4 GlowFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float4 center = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0);
                // The camera depth copy can precede transparent cutouts; use their actual mask depth instead.
                float depth = EyeDepth(SAMPLE_TEXTURE2D_X_LOD(_ObjectGlowDepth, sampler_PointClamp, uv, 0).r);
                float2 gradient = float2(ddx(depth), ddy(depth));
                // Depth derivatives crossing a mask boundary cannot describe the original surface slope.
                gradient *= float2(ddx(center.a) == 0, ddy(center.a) == 0);
                // Non-outlined pixels perform no neighborhood sampling and receive no light through walls.
                if (center.w <= 0)
                    return 0;
                float glow = 0;
                // Diagonal samples keep the band continuous around oblique silhouettes.
                [unroll]
                for (int index = 0; index < 8; index++)
                    glow = max(glow, FindSilhouette(uv, directions[index], center.a, depth, gradient));
                return half4(center.rgb * glow, 0);
            }
            ENDHLSL
        }
    }
}
