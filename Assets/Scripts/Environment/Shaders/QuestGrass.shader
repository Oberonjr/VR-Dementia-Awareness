// Quest Grass - a deliberately cheap grass shader for standalone Quest (URP).
//
// Why it exists: the URP Terrain pack's TerrainGrass shader does full per-pixel lighting + SSS on every
// blade pixel, which pinned the Quest 3 GPU at 100% (10-15 fps). An unlit test got 50 fps. This keeps
// that cost and adds back what the test lost:
//   - wind: lean + sway in the wind direction, a travelling wave across the lawn, and a small
//     per-clump flutter so neighbouring clumps don't move in lockstep. All in the vertex shader.
//   - a fake "ambient occlusion": the base of each clump is darker than the tips.
//   - a tint colour.
//   - clean cut-out edges (alpha-to-coverage when MSAA is on, plain cut-out when it isn't).
// No real lighting (no lights, probes, shadows) on purpose - that's where the cost was.
//
// Wind settings use the same names as TerrainGrass (Wind_Yaw / Wind_Intensity / Wind_Speed), so the
// scene's WindMaster drives these materials too after "Find Grass Materials".
//
// Works with GrassScatter (GPU instancing) and single-pass instanced stereo (both eyes on Quest).
Shader "Environment/Quest Grass"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Color (alpha = cut-out)", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff", Range(0.05, 0.95)) = 0.5

        [Header(Shading)]
        _BaseDarkness ("Darker At Base (fake AO)", Range(0, 1)) = 0.35
        _Brightness ("Brightness", Range(0.2, 2)) = 1

        [Header(Wind (WindMaster writes the first three))]
        Wind_Yaw ("Wind Direction (degrees)", Float) = 0
        Wind_Intensity ("Wind Strength (m at the tip)", Range(0, 0.5)) = 0.08
        Wind_Speed ("Wind Speed", Range(0, 10)) = 2
        _BendHeight ("Bend Height (m) - full sway above this", Range(0.05, 2)) = 0.4
        _WaveScale ("Wave Size Across Lawn", Range(0, 2)) = 0.35
        _Flutter ("Flutter (per-clump jitter)", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "IgnoreProjector" = "True"
        }

        HLSLINCLUDE
        #define _ALPHATEST_ON 1   // always cut-out; also unlocks URP's AlphaClip helper
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4  _BaseColor;
            half   _Cutoff;
            half   _BaseDarkness;
            half   _Brightness;
            float  Wind_Yaw;
            float  Wind_Intensity;
            float  Wind_Speed;
            float  _BendHeight;
            float  _WaveScale;
            float  _Flutter;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 uv         : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        // Moves a vertex with the wind. Returns world position; heightMask = 0 at the root, 1 at the tip.
        float3 WindWorldPos(float3 positionOS, out half heightMask)
        {
            float3 posWS  = TransformObjectToWorld(positionOS);
            float3 origin = UNITY_MATRIX_M._m03_m13_m23;           // this clump's position (per instance)

            float h = saturate(positionOS.y / _BendHeight);
            heightMask = (half)h;
            float bend = h * h;                                      // roots stay planted, tips move most

            float  yaw = radians(Wind_Yaw);
            float2 dir = float2(sin(yaw), cos(yaw));
            float  t   = _Time.y * Wind_Speed;

            // Random number per clump from its position (0..1)
            float rnd = frac(sin(dot(origin.xz, float2(12.9898, 78.233))) * 43758.5453);

            // Big slow wave rolling across the lawn + small fast per-clump flutter
            float wave    = 0.5 + 0.5 * sin(t - dot(origin.xz, dir) * _WaveScale);
            float flutter = sin(t * 2.7 + rnd * 6.2831 + positionOS.x * 3.0) * _Flutter;

            posWS.xz += dir * (Wind_Intensity * bend * (wave + flutter));
            return posWS;
        }
        ENDHLSL

        // ---------------- Main colour pass ----------------
        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Cull Off
            ZWrite On
            AlphaToMask On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half   shade      : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                half heightMask;
                float3 posWS = WindWorldPos(v.positionOS.xyz, heightMask);
                o.positionCS = TransformWorldToHClip(posWS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.shade = lerp(1.0h - _BaseDarkness, 1.0h, heightMask) * _Brightness;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                // URP helper: sharp alpha-to-coverage with MSAA, plain cut-out without it.
                half alpha = AlphaClip(tex.a * _BaseColor.a, _Cutoff);
                return half4(tex.rgb * _BaseColor.rgb * i.shade, alpha);
            }
            ENDHLSL
        }

        // ---------------- Depth only (needed when URP builds a depth texture / depth prepass) ----------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            Cull Off
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                half heightMask;
                o.positionCS = TransformWorldToHClip(WindWorldPos(v.positionOS.xyz, heightMask));
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a - _Cutoff);
                return i.positionCS.z;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
