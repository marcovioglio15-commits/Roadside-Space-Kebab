Shader "Hidden/Player Studio/Visual Draft"
{
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            // Only geometry is consumed; no source shader properties or textures are changed.
            struct Attributes
            {
                float4 Position : POSITION;
            };

            struct Varyings
            {
                float4 Position : SV_POSITION;
            };

            /// <summary>Projects the cached mesh with the submitted proposal matrix.</summary>
            /// <param name="input">Shared mesh vertex.</param>
            /// <returns>Clip-space position for the preview camera.</returns>
            Varyings Vert(Attributes input)
            {
                // Graphics.DrawMeshNow supplies the proposed object matrix for this draw only.
                Varyings output;
                output.Position = UnityObjectToClipPos(input.Position);
                return output;
            }

            /// <summary>Marks proposed geometry with a consistent translucent amber tint.</summary>
            /// <param name="input">Rasterized vertex output.</param>
            /// <returns>Fixed preview color; no material configuration is exposed.</returns>
            half4 Frag(Varyings input) : SV_Target
            {
                // An unlit proposal stays legible without copying the source material.
                return half4(1.0, 0.62, 0.12, 0.58);
            }
            ENDHLSL
        }
    }
}
