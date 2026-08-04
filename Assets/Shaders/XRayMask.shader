// Stamps the car's silhouette into the stencil buffer so XRayOutline can punch the
// body out of its own outline and only leave the ring. Writes no colour and no depth:
// this pass exists purely to say "the car covers these pixels", which is why it tests
// ZTest Always -- the whole point is that it runs even when a building is in front.
//
// Queue sits at Transparent-400 (2600) so every mask in the rig is stamped before the
// first outline at 2601 reads it, no matter how many meshes the car model is split into.
Shader "GMTK/X-Ray Mask"
{
    Properties
    {
        [IntRange] _StencilBit ("Stencil Bit", Range(1, 255)) = 128
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent-400"
        }

        Pass
        {
            Name "XRayMask"
            Tags { "LightMode" = "UniversalForward" }

            Stencil
            {
                Ref [_StencilBit]
                ReadMask [_StencilBit]
                WriteMask [_StencilBit]
                Comp Always
                Pass Replace
            }

            Cull Off
            ZWrite Off
            ZTest Always
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _StencilBit;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
