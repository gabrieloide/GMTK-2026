// Marks the pickup/drop-off points for the order system: an open-top cylinder that reads
// as a column of rising light, the way a boss's soul beam does when it dies in Ocarina of
// Time. One shader drives both colours (orange pickups, blue drop-offs) via two material
// instances so tuning the effect in one place tunes it everywhere.
//
// How it works: the beam's shape (base-to-tip gradient, fade-to-nothing at the rim, rising
// energy bands) is computed entirely from object-space height, not from the mesh's UVs.
// The source mesh (point.fbx "Cylinder") unwraps its cap and its side wall as separate UV
// islands that do not agree on which axis is "up" in texture space - sampling a texture
// for the shape read as coherent on one island and sideways on the other. Object-space
// height has no such seam: every fragment on the surface, regardless of which UV island it
// came from, agrees on how far up the mesh it sits. _LocalHeight is the mesh's own extent
// along its native up axis (Z before the +270 deg X rotation that stands it upright in the
// scene) - it is what turns raw object-space Z into a clean 0-1 base-to-tip factor.
Shader "GMTK/Order Beacon"
{
    Properties
    {
        [HDR] _ColorBottom ("Base Color", Color) = (1, 0.45, 0.05, 1)
        [HDR] _ColorTop ("Tip Color", Color) = (1, 0.85, 0.4, 1)
        _Intensity ("Intensity", Float) = 2.5
        // Mesh extent along its native up axis (local Z), centred on the pivot - defines
        // what object-space height maps to the 0-1 base-to-tip factor.
        _LocalHeight ("Local Mesh Height", Float) = 0.48
        _BandCount ("Rising Band Count", Float) = 4
        _ScrollSpeed ("Scroll Speed", Float) = 0.6
        _PulseSpeed ("Pulse Speed", Float) = 2.5
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.2
        _FresnelPower ("Fresnel Power", Float) = 2.5
        _FresnelIntensity ("Fresnel Intensity", Float) = 1.2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _ColorBottom;
            half4 _ColorTop;
            float _Intensity;
            float _LocalHeight;
            float _BandCount;
            float _ScrollSpeed;
            float _PulseSpeed;
            float _PulseAmount;
            float _FresnelPower;
            float _FresnelIntensity;
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
            float3 normalWS   : TEXCOORD0;
            float3 positionWS : TEXCOORD1;
            float heightNorm  : TEXCOORD2;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings vert(Attributes IN)
        {
            Varyings OUT = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(IN);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

            OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
            OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
            OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);

            float halfHeight = max(_LocalHeight, 0.0001) * 0.5;
            OUT.heightNorm = saturate((IN.positionOS.z + halfHeight) / (halfHeight * 2.0));
            return OUT;
        }

        half4 ShadeBeacon(Varyings IN)
        {
            // Solid at the base, dissolves to nothing by the rim - this is what makes an
            // ordinary capped cylinder read as an open-top column: the cap is still there,
            // it is just faded to invisible along with the rest of the tip.
            half baseFade = 1.0 - smoothstep(0.0, 1.0, IN.heightNorm);

            // A handful of soft bands riding the same 0-1 height, endlessly scrolling
            // upward - the "energy climbing the column" read, built from math instead of
            // a texture so it can never disagree with itself across the mesh.
            float bandPhase = frac(IN.heightNorm * _BandCount - _Time.y * _ScrollSpeed);
            float band = smoothstep(0.0, 0.5, bandPhase) * smoothstep(1.0, 0.5, bandPhase);
            half mask = baseFade * lerp(0.55, 1.0, band);

            half3 gradient = lerp(_ColorBottom.rgb, _ColorTop.rgb, IN.heightNorm);

            float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount;

            float3 viewDirWS = normalize(GetCameraPositionWS() - IN.positionWS);
            float3 normalWS = normalize(IN.normalWS);
            float fresnel = pow(saturate(1.0 - abs(dot(normalWS, viewDirWS))), _FresnelPower) * _FresnelIntensity;

            half3 color = gradient * (mask * _Intensity * pulse + fresnel);
            half alpha = saturate(mask * pulse + fresnel * 0.5);
            return half4(color, alpha);
        }
        ENDHLSL

        Pass
        {
            Name "OrderBeacon"
            Tags { "LightMode" = "UniversalForward" }

            // Additive so overlapping fragments (the open top, seen from inside the tube)
            // pile up into brighter light instead of fighting over draw order.
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend One One

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            half4 frag(Varyings IN) : SV_Target
            {
                return ShadeBeacon(IN);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
