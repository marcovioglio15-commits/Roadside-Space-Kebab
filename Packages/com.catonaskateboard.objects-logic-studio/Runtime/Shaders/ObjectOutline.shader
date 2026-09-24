Shader "Objects Logic Studio/Outline"
{
    Properties
    {
        _OutlineColor ("Outline color and opacity", Color) = (0, 0, 0, 1)
        _OutlineThickness ("World-space shell thickness in metres", Float) = 0.004
        [Enum(UnityEngine.Rendering.CompareFunction)]
        _DepthTest ("Depth test: LessEqual for occlusion, Always for through walls", Float) = 4
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+1" }
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite Off
            ZTest [_DepthTest]
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineThickness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            /// <summary>Extrudes in world space so differently scaled objects use the same thickness unit.</summary>
            /// <param name="input">Original surface position and normal.</param>
            /// <returns>Expanded clip-space position with instance and stereo state.</returns>
            Varyings OutlineVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(positionWS + normalWS * _OutlineThickness);
                return output;
            }

            /// <summary>Colors the outline while the original renderer supplies the textured surface.</summary>
            /// <param name="input">Interpolated shell vertex data.</param>
            /// <returns>Outline tint and alpha.</returns>
            half4 OutlineFragment(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
    CustomEditor "CatOnASkateboard.ObjectsLogicStudio.Editor.OutlineShaderEditor"
}
