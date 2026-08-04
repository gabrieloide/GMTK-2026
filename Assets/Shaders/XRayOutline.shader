// Draws the car's contour only where something else is already in front of it, so the
// player never loses the car behind a building with a camera that cannot be moved.
//
// How it works: the hull is the car's mesh pushed outwards, drawn with ZTest Greater so
// it survives only on pixels a building already owns, and masked by the stencil bit that
// XRayMask stamped over the car's real silhouette. Greater alone would flood the whole
// body with colour; the stencil is what turns that flood into a ring.
//
// The push direction blends the vertex normal with a radial direction out of the mesh
// centre. Pure normals tear the ring apart at the hard edges of a low-poly car (split
// normals give neighbouring triangles different offsets); the radial term only depends on
// position, so shared vertices always move together and the hull stays watertight.
Shader "GMTK/X-Ray Outline"
{
    Properties
    {
        [HDR] _OutlineColor ("Outline Color", Color) = (1, 0.85, 0.25, 1)
        _OutlineWidth ("Outline Width (world units)", Float) = 0.35
        _RadialBlend ("Normal <-> Radial Blend", Range(0, 1)) = 0.65
        _FillColor ("Fill Color (alpha 0 = outline only)", Color) = (1, 0.85, 0.25, 0)
        _CenterOS ("Object Space Centre", Vector) = (0, 0, 0, 0)
        [IntRange] _StencilBit ("Stencil Bit", Range(1, 255)) = 128
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent-399"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _OutlineColor;
            half4 _FillColor;
            float4 _CenterOS;
            float _OutlineWidth;
            float _RadialBlend;
            float _StencilBit;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS   : NORMAL;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            UNITY_VERTEX_OUTPUT_STEREO
        };
        ENDHLSL

        Pass
        {
            Name "XRayOutline"
            Tags { "LightMode" = "UniversalForward" }

            Stencil
            {
                Ref [_StencilBit]
                ReadMask [_StencilBit]
                WriteMask 0
                Comp NotEqual
            }

            // Front faces are culled so the hull cannot lie on top of the car's own body:
            // what is left is its far side, which is exactly the ring around the silhouette.
            Cull Front
            ZWrite Off
            ZTest Greater
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 positionOS = IN.positionOS.xyz;
                float3 normalOS = normalize(IN.normalOS);

                float3 radialOS = positionOS - _CenterOS.xyz;
                float radialLength = length(radialOS);
                radialOS = radialLength > 1e-5 ? radialOS / radialLength : normalOS;

                float3 directionOS = normalize(lerp(normalOS, radialOS, _RadialBlend));

                // Offsetting in world space keeps the width in metres, so the ring does not
                // change thickness when the car's parent scale is retuned.
                float3 positionWS = TransformObjectToWorld(positionOS);
                float3 directionWS = TransformObjectToWorldDir(directionOS);
                positionWS += directionWS * _OutlineWidth;

                OUT.positionCS = TransformWorldToHClip(positionWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }

        // Optional tint over the hidden body. Alpha 0 by default, which costs one invisible
        // draw and gives a dial for "just the ring" versus "ring plus a ghost of the car".
        Pass
        {
            Name "XRayFill"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Stencil
            {
                Ref [_StencilBit]
                ReadMask [_StencilBit]
                WriteMask 0
                Comp Equal
            }

            Cull Back
            ZWrite Off
            ZTest Greater
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

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
                return _FillColor;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
